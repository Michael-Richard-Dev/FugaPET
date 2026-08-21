using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Cliente A_ProductPlant (API_PRODUCT_SRV): montagem de URL (chave composta, material/centro
/// preservados) e parse de IsBatchManagementRequired (true/false/ausente), OData V2. Somente leitura.
/// </summary>
public sealed class ProductPlantSapApiClientTests
{
    private static readonly Uri Base = new("https://sap.exemplo.local/sap/opu/odata/sap/API_PRODUCT_SRV/");

    [Fact]
    public void MontarUrl_DevePreservarMaterialECentroComChaveCompostaESelect()
    {
        Uri url = ProductPlantSapApiClient.MontarUrlProdutoCentro(Base, "1000395", "3007", "100");

        string texto = url.ToString();
        Assert.Contains("A_ProductPlant(Product='1000395',Plant='3007')", texto);
        Assert.Contains("$select=Product,Plant,IsBatchManagementRequired", texto);
        Assert.Contains("$format=json", texto);
        Assert.Contains("sap-client=100", texto);
    }

    [Fact]
    public void MontarUrl_NaoDevePerderZerosAEsquerdaDoMaterial()
    {
        Uri url = ProductPlantSapApiClient.MontarUrlProdutoCentro(Base, "0000000000001000395", "3007");
        Assert.Contains("Product='0000000000001000395'", url.ToString());
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Parsear_DObjeto_DeveLerIsBatchManagementRequired(string valorBool, bool esperado)
    {
        string json = "{\"d\":{\"Product\":\"1000395\",\"Plant\":\"3007\",\"IsBatchManagementRequired\":" + valorBool + "}}";

        SapProductPlantDto? dto = ProductPlantSapApiClient.ParsearProdutoCentro(json);

        Assert.NotNull(dto);
        Assert.Equal("1000395", dto!.Product);
        Assert.Equal("3007", dto.Plant);
        Assert.Equal(esperado, dto.IsBatchManagementRequired);
    }

    [Fact]
    public void Parsear_SemAFlag_DeveDeixarIsBatchManagementRequiredNulo()
    {
        string json = """{"d":{"Product":"1000395","Plant":"3007"}}""";

        SapProductPlantDto? dto = ProductPlantSapApiClient.ParsearProdutoCentro(json);

        Assert.NotNull(dto);
        Assert.Null(dto!.IsBatchManagementRequired);
    }

    [Fact]
    public void Parsear_DResults_DeveUsarOPrimeiroRegistro()
    {
        string json = """{"d":{"results":[{"Product":"1000395","Plant":"3007","IsBatchManagementRequired":false}]}}""";

        SapProductPlantDto? dto = ProductPlantSapApiClient.ParsearProdutoCentro(json);

        Assert.NotNull(dto);
        Assert.False(dto!.IsBatchManagementRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ nao json")]
    [InlineData("""{"d":{"results":[]}}""")]
    public void Parsear_EntradaInvalidaOuVazia_DeveRetornarNulo(string? json)
        => Assert.Null(ProductPlantSapApiClient.ParsearProdutoCentro(json));

    [Fact]
    public void ProdutoCentroSapMestre_AdministracaoLoteDefinida_SoQuandoConsultadoEComFlag()
    {
        Assert.True(new ProdutoCentroSapMestre { Consultado = true, IsBatchManagementRequired = true }.AdministracaoLoteDefinida);
        Assert.True(new ProdutoCentroSapMestre { Consultado = true, IsBatchManagementRequired = false }.AdministracaoLoteDefinida);
        Assert.False(new ProdutoCentroSapMestre { Consultado = true, IsBatchManagementRequired = null }.AdministracaoLoteDefinida);
        Assert.False(new ProdutoCentroSapMestre { Consultado = false, IsBatchManagementRequired = true }.AdministracaoLoteDefinida);
    }
}
