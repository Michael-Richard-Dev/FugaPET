using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Comportamento de MapeamentoCampoEtiquetaServico.SalvarAsync diante de excecao inesperada
/// (nao-PostgreSQL): NAO deve propagar a excecao para a tela; deve retornar falha amigavel,
/// sem vazar o detalhe tecnico. Usa um repositorio fake que lanca e um spy de auditoria.
/// (Substitui o antigo teste "contratual" que apenas lia o codigo-fonte.)
/// </summary>
public sealed class MapeamentoCampoEtiquetaServicoTests : IDisposable
{
    public MapeamentoCampoEtiquetaServicoTests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");

        // Sessao com permissao para gerenciar o mapeamento (passa o gate de autorizacao).
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes =
            [
                new PermissaoSessaoAplicacao
                {
                    Modulo = PermissoesSistema.Modulos.Etiqueta,
                    Rotina = PermissoesSistema.Rotinas.MapeamentoCampoEtiqueta,
                    Acao = PermissoesSistema.Acoes.Gerenciar
                }
            ],
            IntegracaoBancoHabilitada = true
        });
    }

    [Fact] // #4
    public async Task SalvarAsync_DeveRetornarFalhaAmigavel_QuandoExcecaoGenerica()
    {
        MapeamentoCampoEtiquetaServico servico = new(new RepositorioQueFalha(), new AuditoriaSpy());
        MapeamentoCampoEtiquetaCadastro mapa = new() { CodigoCampoEtiqueta = 10, OrigemDado = "SAP" };

        // Nao deve lancar: o erro inesperado e tratado e convertido em falha amigavel.
        ResultadoOperacao resultado = await servico.SalvarAsync(mapa);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Acione o suporte", resultado.Mensagem);
        Assert.DoesNotContain("falha simulada", resultado.Mensagem); // nao vaza detalhe tecnico
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    // Fake: campo sem mapeamento ativo (caminho de INSERIR) e o INSERIR lanca excecao generica.
    private sealed class RepositorioQueFalha : MapeamentoCampoEtiquetaRepositorio
    {
        public RepositorioQueFalha() : base(null!) { }

        public override Task<MapeamentoCampoEtiquetaCadastro?> ObterAtivoPorCampoAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
            => Task.FromResult<MapeamentoCampoEtiquetaCadastro?>(null);

        public override Task<bool> CampoEstaAtivoAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task<long> InserirAsync(MapeamentoCampoEtiquetaCadastro mapa, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falha simulada");
    }

    private sealed class AuditoriaSpy : AuditoriaServico
    {
        public AuditoriaSpy() : base(null!) { }

        public override Task RegistrarErroAsync(string acao, string mensagem, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task RegistrarAcessoNegadoAsync(long codigoUsuario, string motivo, string? tela = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
