using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

public sealed class AutorizacaoServicoTests : IDisposable
{
    public AutorizacaoServicoTests()
    {
        Environment.SetEnvironmentVariable("FUGAPET_DEV_CONEXAO_POSTGRES", "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    [Fact]
    public void PossuiPermissao_DeveNegar_QuandoNaoHaSessao()
    {
        Assert.False(AutorizacaoServico.PossuiPermissao("SEGURANCA", "USUARIO", "CRIAR"));
    }

    [Fact]
    public void PossuiPermissao_DevePermitir_AcaoExata()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("SEGURANCA", "USUARIO", "CRIAR"));

        Assert.True(AutorizacaoServico.PossuiPermissao("seguranca", "usuario", "criar"));
    }

    [Fact]
    public void PossuiPermissao_DevePermitir_AcaoQuandoUsuarioTemGerenciarNaRotina()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("SEGURANCA", "USUARIO", AutorizacaoServico.AcaoGerenciar));

        Assert.True(AutorizacaoServico.PossuiPermissao("SEGURANCA", "USUARIO", "EXCLUIR"));
    }

    [Fact]
    public void PossuiPermissao_DeveUsarPerfilAcessoComoRotinaOficial()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("SEGURANCA", "PERFIL_ACESSO", AutorizacaoServico.AcaoGerenciar));

        Assert.True(AutorizacaoServico.PossuiPermissao("SEGURANCA", "PERFIL_ACESSO", "EDITAR"));
        Assert.False(AutorizacaoServico.PossuiPermissao("SEGURANCA", "PERFIL", "EDITAR"));
    }

    [Fact]
    public void PodeAcessar_DeveUsarProcessoProducaoComoModuloOficial()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("PROCESSO_PRODUCAO", "LEITURA_PRODUCAO", "CONSULTAR"));

        Assert.True(AutorizacaoServico.PodeAcessar(AutorizacaoServico.ModuloProcesso));
    }

    [Fact]
    public void PodeAcessar_NaoDeveUsarOperacaoComoEquivalenciaDeProcesso()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("OPERACAO", "LEITURA_PRODUCAO", "CONSULTAR"));

        Assert.False(AutorizacaoServico.PodeAcessar(AutorizacaoServico.ModuloProcesso));
    }


    [Fact]
    public void PodeAcessar_NaoDevePermitirEtiquetaPorPermissaoAntigaDeCadastroEtiqueta()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("CADASTRO", "ETIQUETA", "CONSULTAR"));

        Assert.False(AutorizacaoServico.PodeAcessar(AutorizacaoServico.ModuloEtiqueta));
        Assert.False(AutorizacaoServico.PossuiPermissao("ETIQUETA", "ETIQUETA", "CONSULTAR"));
    }

    [Fact]
    public void PodeAcessar_DevePermitirEtiquetaSomentePorModuloOficial()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("ETIQUETA", "ETIQUETA", "CONSULTAR"));

        Assert.True(AutorizacaoServico.PodeAcessar(AutorizacaoServico.ModuloEtiqueta));
    }

    [Fact]
    public void PossuiPermissao_DeveNegarPerfilComumSemGerenciarPerfilAcesso()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPermissao("SEGURANCA", "PERMISSAO", "EDITAR"));

        Assert.False(AutorizacaoServico.PossuiPermissao(
            "SEGURANCA",
            "PERFIL_ACESSO",
            AutorizacaoServico.AcaoGerenciar));
    }
    [Fact]
    public void PossuiPermissao_NaoDevePermitir_PorNomeDePerfilSemPermissao()
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 10,
            Login = "supervisor_teste",
            Nome = "Supervisor Teste",
            PerfisCodigo = ["SUPERVISOR_TESTE"],
            Permissoes = [],
            IntegracaoBancoHabilitada = true
        });

        Assert.False(AutorizacaoServico.PossuiPermissao("SEGURANCA", "USUARIO", "EXCLUIR"));
    }

    public void Dispose()
    {
        EstadoSessaoUsuarioAtual.Limpar();
    }

    private static SessaoUsuarioAplicacao CriarSessaoComPermissao(string modulo, string rotina, string acao)
    {
        return new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            PerfisCodigo = ["ADMIN"],
            Permissoes =
            [
                new PermissaoSessaoAplicacao
                {
                    Modulo = modulo,
                    Rotina = rotina,
                    Acao = acao
                }
            ],
            IntegracaoBancoHabilitada = true
        };
    }
}
