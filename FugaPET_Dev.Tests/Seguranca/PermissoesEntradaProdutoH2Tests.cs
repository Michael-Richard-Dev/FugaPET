using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// H2 - Autorizacao da Entrada de Produto (peso manual) deve ser 100% por permissao
/// (PROCESSO_PRODUCAO / ENTRADA_PRODUTO / PESO_MANUAL), nunca por nome/codigo de perfil.
/// Perfis com nomes "administrativos" NAO podem ganhar acesso sem a permissao real.
/// </summary>
public sealed class PermissoesEntradaProdutoH2Tests : IDisposable
{
    public PermissoesEntradaProdutoH2Tests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("ADMINISTRADOR")]
    [InlineData("PCP_CHAVE")]
    [InlineData("PCP CHAVE")]
    [InlineData("Administrador Geral")]
    [InlineData("Super Admin")]
    public void PesoManual_DeveNegar_QuandoNomeDePerfilParecidoMasSemPermissao(string nomePerfil)
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 50,
            Login = "usuario_teste",
            Nome = nomePerfil,
            PerfisCodigo = [nomePerfil],
            Permissoes = [],
            IntegracaoBancoHabilitada = true
        });

        Assert.False(AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual));
    }

    [Fact]
    public void PesoManual_DevePermitir_ComPermissaoReal_IndependenteDoNomeDoPerfil()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPesoManual("Operador Comum", "OPERADOR"));

        Assert.True(AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual));
    }

    [Fact]
    public void PesoManual_NomeAdministrativo_NaoInterfere_QuandoTemPermissaoReal()
    {
        EstadoSessaoUsuarioAtual.Definir(CriarSessaoComPesoManual("ADMINISTRADOR", "ADMIN"));

        Assert.True(AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual));
    }

    [Fact]
    public void PesoManual_DeveNegar_QuandoNaoHaSessao()
    {
        Assert.False(AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.PesoManual));
    }

    public void Dispose()
    {
        EstadoSessaoUsuarioAtual.Limpar();
    }

    private static SessaoUsuarioAplicacao CriarSessaoComPesoManual(string nome, string codigoPerfil)
        => new()
        {
            IdUsuario = 51,
            Login = "usuario_peso",
            Nome = nome,
            PerfisCodigo = [codigoPerfil],
            Permissoes =
            [
                new PermissaoSessaoAplicacao
                {
                    Modulo = PermissoesSistema.Modulos.ProcessoProducao,
                    Rotina = PermissoesSistema.Rotinas.EntradaProduto,
                    Acao = PermissoesSistema.Acoes.PesoManual
                }
            ],
            IntegracaoBancoHabilitada = true
        };
}
