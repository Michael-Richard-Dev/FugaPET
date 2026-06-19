using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Comportamento de orquestracao de UsuarioServico.AtualizarComVinculosAsync: a edicao deve
/// encaminhar ao repositorio EXATAMENTE o perfil unico informado (sem acumular). A garantia de
/// "apenas 1 perfil ativo" em si e enforced no SQL do repositorio (UsuarioRepositorio
/// .AtualizarComVinculosAsync) — isso exige teste de integracao com banco; aqui validamos o
/// contrato C#: um unico idPerfilAcesso e repassado por chamada.
/// </summary>
public sealed class UsuarioAtualizacaoVinculosTests : IDisposable
{
    public UsuarioAtualizacaoVinculosTests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes =
            [
                new PermissaoSessaoAplicacao
                {
                    Modulo = PermissoesSistema.Modulos.Seguranca,
                    Rotina = PermissoesSistema.Rotinas.Usuario,
                    Acao = PermissoesSistema.Acoes.Editar
                }
            ],
            IntegracaoBancoHabilitada = true
        });
    }

    [Fact] // #6
    public async Task AtualizarComVinculosAsync_DeveSincronizarComUmUnicoPerfilInformado()
    {
        RepositorioSpy repo = new();
        UsuarioServico servico = new(repo, new SenhaServico(), new AuditoriaSpy());

        UsuarioCadastro usuario = new()
        {
            IdUsuario = 10,
            IdCargo = 3,
            NomeUsuario = "Fulano de Tal",
            LoginUsuario = "fulano",
            SituacaoUsuario = true
        };

        ResultadoOperacao resultado = await servico.AtualizarComVinculosAsync(usuario, idPerfilAcesso: 99, idSetor: 5);

        Assert.True(resultado.Sucesso);
        // Exatamente uma sincronizacao de vinculos, com o unico perfil informado.
        Assert.Single(repo.PerfisRecebidos);
        Assert.Equal(99, repo.PerfisRecebidos[0]);
        Assert.Single(repo.SetoresRecebidos);
        Assert.Equal(5, repo.SetoresRecebidos[0]);
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    private sealed class RepositorioSpy : UsuarioRepositorio
    {
        public List<long> PerfisRecebidos { get; } = new();
        public List<long> SetoresRecebidos { get; } = new();

        public RepositorioSpy() : base(null!) { }

        public override Task<bool> ExisteLoginAsync(string login, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public override Task<UsuarioCadastro?> ObterPorIdAsync(long codigoUsuario, CancellationToken cancellationToken = default)
            => Task.FromResult<UsuarioCadastro?>(new UsuarioCadastro { IdUsuario = codigoUsuario, SituacaoUsuario = true });

        public override Task<int> AtualizarComVinculosAsync(UsuarioCadastro usuario, long codigoPerfilAcesso, long codigoSetor, CancellationToken cancellationToken = default)
        {
            PerfisRecebidos.Add(codigoPerfilAcesso);
            SetoresRecebidos.Add(codigoSetor);
            return Task.FromResult(1);
        }
    }

    private sealed class AuditoriaSpy : AuditoriaServico
    {
        public AuditoriaSpy() : base(null!) { }

        public override Task RegistrarCadastroAtualizadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarCadastroExcluidoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarCadastroReativadoAsync(string entidade, long codigoRegistro, string? descricao = null, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
