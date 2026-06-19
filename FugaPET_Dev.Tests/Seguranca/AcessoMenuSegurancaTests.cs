using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Comportamento de acesso ao menu Seguranca para um perfil que SO tem permissoes de seguranca.
/// Prova que a separacao Cadastro x Seguranca funciona: o administrador de seguranca acessa as
/// telas de seguranca sem depender de nenhuma permissao de CADASTRO.
/// </summary>
public sealed class AcessoMenuSegurancaTests : IDisposable
{
    public AcessoMenuSegurancaTests()
    {
        // Liga a integracao de banco para que a autorizacao seja efetivamente exigida.
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
        DefinirSessaoSomenteSeguranca();
    }

    [Fact] // #1
    public void UsuarioSomenteSeguranca_PodeAcessarMenuSeguranca()
    {
        Assert.True(AutorizacaoServico.PodeAcessar(PermissoesSistema.Modulos.Seguranca));
    }

    [Fact] // #2
    public void UsuarioSomenteSeguranca_NaoDependeDoMenuCadastro()
    {
        // Nao tem CADASTRO...
        Assert.False(AutorizacaoServico.PodeAcessar(PermissoesSistema.Modulos.Cadastro));

        // ...e ainda assim consegue consultar as 3 telas de seguranca.
        Assert.True(AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Consultar));
        Assert.True(AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Consultar));
        Assert.True(AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Consultar));
    }

    private static void DefinirSessaoSomenteSeguranca()
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 7,
            Login = "admin_seguranca",
            Nome = "Administrador de Seguranca",
            PerfisCodigo = ["SEGURANCA"],
            Permissoes =
            [
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "USUARIO", Acao = "CONSULTAR" },
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "PERFIL_ACESSO", Acao = "CONSULTAR" },
                new PermissaoSessaoAplicacao { Modulo = "SEGURANCA", Rotina = "PERMISSAO", Acao = "CONSULTAR" }
            ],
            IntegracaoBancoHabilitada = true
        });
    }

    public void Dispose()
    {
        EstadoSessaoUsuarioAtual.Limpar();
    }
}
