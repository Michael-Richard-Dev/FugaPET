using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// H5 - regra configuravel de centros/depositos autorizados para a Entrada de Produto.
/// </summary>
public sealed class AutorizacaoCentroDepositoEntradaTests
{
    [Fact]
    public void CentroPermitido_DeveAutorizar()
    {
        AutorizacaoCentroDepositoEntrada regra = new(centrosAutorizados: ["3007"], depositosAutorizados: []);

        Assert.True(regra.CentroAutorizado("3007"));
        Assert.True(regra.CentroAutorizado(" 3007 "));
    }

    [Fact]
    public void CentroNegado_NaoDeveAutorizar()
    {
        AutorizacaoCentroDepositoEntrada regra = new(centrosAutorizados: ["3007"], depositosAutorizados: []);

        Assert.False(regra.CentroAutorizado("1410"));
        Assert.False(regra.ItemAutorizado("1410", "141C"));
    }

    [Fact]
    public void DepositoVazio_NaoDeveAutorizar_QuandoHaRestricao()
    {
        AutorizacaoCentroDepositoEntrada regra = new(centrosAutorizados: [], depositosAutorizados: ["141C"]);

        Assert.False(regra.DepositoAutorizado(""));
        Assert.False(regra.DepositoAutorizado("   "));
        Assert.False(regra.DepositoAutorizado(null));
    }

    [Fact]
    public void DepositoNegado_NaoDeveAutorizar()
    {
        AutorizacaoCentroDepositoEntrada regra = new(centrosAutorizados: [], depositosAutorizados: ["141C"]);

        Assert.False(regra.DepositoAutorizado("999"));
        Assert.True(regra.DepositoAutorizado("141C"));
    }

    [Fact]
    public void SemConfiguracao_NaoRestringe()
    {
        AutorizacaoCentroDepositoEntrada regra = new(centrosAutorizados: [], depositosAutorizados: []);

        Assert.False(regra.RestringeCentro);
        Assert.False(regra.RestringeDeposito);
        Assert.True(regra.ItemAutorizado("qualquer", "qualquer"));
    }

    [Fact]
    public void ItemAutorizado_ExigeCentroEDeposito()
    {
        AutorizacaoCentroDepositoEntrada regra = new(centrosAutorizados: ["3007"], depositosAutorizados: ["141C"]);

        Assert.True(regra.ItemAutorizado("3007", "141C"));
        Assert.False(regra.ItemAutorizado("3007", "999"));
        Assert.False(regra.ItemAutorizado("1410", "141C"));
    }
}
