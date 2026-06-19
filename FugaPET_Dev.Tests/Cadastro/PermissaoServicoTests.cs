using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class PermissaoServicoTests : IDisposable
{
    public PermissaoServicoTests()
    {
        Environment.SetEnvironmentVariable("FUGAPET_DEV_CONEXAO_POSTGRES", "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes =
            [
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "PERMISSAO", Acao = "CRIAR" },
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "PERMISSAO", Acao = "EDITAR" }
            ],
            IntegracaoBancoHabilitada = true
        });
    }

    [Fact]
    public async Task InserirAsync_DeveValidarModuloAntesDeAcessarRepositorio()
    {
        PermissaoServico servico = new(null!, null!, null!);
        PermissaoCadastro permissao = new()
        {
            RotinaPermissao = "USUARIO",
            AcaoPermissao = "CRIAR"
        };

        ResultadoOperacao resultado = await servico.InserirAsync(permissao);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Modulo da permissao", resultado.Mensagem);
    }

    [Fact]
    public async Task AtualizarAsync_DeveValidarIdAntesDeAcessarRepositorio()
    {
        PermissaoServico servico = new(null!, null!, null!);
        PermissaoCadastro permissao = new()
        {
            ModuloPermissao = "SEGURANCA",
            RotinaPermissao = "USUARIO",
            AcaoPermissao = "CRIAR"
        };

        ResultadoOperacao resultado = await servico.AtualizarAsync(permissao);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Id da permissao invalido", resultado.Mensagem);
    }

    [Fact]
    public async Task SincronizarPermissoesDoPerfilAsync_DeveExigirPerfilSelecionado()
    {
        // A sessao (no construtor) tem SEGURANCA/PERMISSAO CRIAR+EDITAR; para passar a
        // autorizacao de GERENCIAR precisamos conceder essa acao na rotina PERFIL_ACESSO.
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes =
            [
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "PERFIL_ACESSO", Acao = AutorizacaoServico.AcaoGerenciar }
            ],
            IntegracaoBancoHabilitada = true
        });

        PermissaoServico servico = new(null!, null!, null!);

        // codigoPerfil <= 0: a guarda retorna antes de tocar repositorio/auditoria.
        ResultadoOperacao resultado = await servico.SincronizarPermissoesDoPerfilAsync(0, Array.Empty<long>());

        Assert.False(resultado.Sucesso);
        Assert.Contains("Selecione um perfil", resultado.Mensagem);
    }


    public void Dispose()
    {
        EstadoSessaoUsuarioAtual.Limpar();
    }
}
