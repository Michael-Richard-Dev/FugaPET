using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Cliente do catálogo de produtos por centro (API_PRODUCT_SRV).
/// <para>
/// Contrato verificado contra o SAP da instalação em 16/11/2026: o centro 3007 responde HTTP 200
/// com 155 materiais, e <c>$expand=to_Product</c> responde 404 — A_ProductPlant não navega para o
/// cadastro do material aqui. Daí as duas consultas.
/// </para>
/// </summary>
public sealed class CatalogoProdutoSapApiClientTests
{
    private static readonly Uri Base = new("https://exemplo.invalido/sap/opu/odata/sap/API_PRODUCT_SRV/");

    // ---------------------------------------------------------------- URL do centro

    [Fact]
    public void UrlDoCentro_FiltraPeloCentroEPedeOsCamposNecessarios()
    {
        string url = Uri.UnescapeDataString(
            CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(Base, "3007", "110").ToString());

        Assert.Contains("/A_ProductPlant?", url, StringComparison.Ordinal);
        Assert.Contains("Plant eq '3007'", url, StringComparison.Ordinal);
        Assert.Contains("IsBatchManagementRequired", url, StringComparison.Ordinal);
        Assert.Contains("sap-client=110", url, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlDoCentro_PreservaZerosAEsquerda()
    {
        // Centro e TEXTO. Tratar como numero comeria o zero de "0300" e consultaria outro centro.
        string url = Uri.UnescapeDataString(
            CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(Base, " 0300 ").ToString());
        Assert.Contains("Plant eq '0300'", url, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlDoCentro_RecusaCentroVazio()
        => Assert.Throws<ArgumentException>(
            () => CatalogoProdutoSapApiClient.MontarUrlProdutosDoCentro(Base, "   "));

    // ---------------------------------------------------------------- URL dos dados mestres

    [Fact]
    public void UrlDoMestre_MontaUmOrPorCodigo()
    {
        string url = Uri.UnescapeDataString(
            CatalogoProdutoSapApiClient.MontarUrlProdutosMestre(Base, ["1000090", "1000036"], "110").ToString());

        Assert.Contains("/A_Product?", url, StringComparison.Ordinal);
        Assert.Contains("Product eq '1000090' or Product eq '1000036'", url, StringComparison.Ordinal);
        Assert.Contains("ProductType", url, StringComparison.Ordinal);
        Assert.Contains("BaseUnit", url, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlDoMestre_EscapaApostrofo()
    {
        // Em literal OData a aspa simples se escreve dobrada. Sem isso o filtro quebra -- ou muda de
        // sentido, que e pior.
        string url = Uri.UnescapeDataString(
            CatalogoProdutoSapApiClient.MontarUrlProdutosMestre(Base, ["AB'CD"]).ToString());
        Assert.Contains("Product eq 'AB''CD'", url, StringComparison.Ordinal);
    }

    [Fact]
    public void UrlDoMestre_RecusaLoteVazioEAcimaDoTeto()
    {
        Assert.Throws<ArgumentException>(
            () => CatalogoProdutoSapApiClient.MontarUrlProdutosMestre(Base, []));

        string[] demais = [.. Enumerable.Range(0, CatalogoProdutoSapApiClient.TamanhoLoteCodigos + 1)
            .Select(i => "P" + i)];
        Assert.Throws<ArgumentException>(
            () => CatalogoProdutoSapApiClient.MontarUrlProdutosMestre(Base, demais));
    }

    // ---------------------------------------------------------------- paginação

    [Fact]
    public void Lotes_RespeitamOTetoERemovemDuplicatas()
    {
        string[] codigos = [.. Enumerable.Range(0, 95).Select(i => "P" + i), "P1", "P2", "  ", "P3"];
        IReadOnlyList<IReadOnlyList<string>> lotes = CatalogoProdutoSapApiClient.PartirEmLotes(codigos);

        Assert.Equal(95, lotes.Sum(l => l.Count));
        Assert.All(lotes, l => Assert.InRange(l.Count, 1, CatalogoProdutoSapApiClient.TamanhoLoteCodigos));
    }

    // ---------------------------------------------------------------- parse do centro

    [Fact]
    public void PaginaDoCentro_LeItensEProximaPagina()
    {
        const string json = """
            {"d":{"results":[
              {"Product":"1000036","Plant":"3007","IsBatchManagementRequired":true},
              {"Product":"1000090","Plant":"3007","IsBatchManagementRequired":false}
            ],"__next":"https://exemplo.invalido/segue"}}
            """;

        PaginaCatalogoProdutoSap p = CatalogoProdutoSapApiClient.InterpretarPaginaDoCentro(json);

        Assert.True(p.Valida);
        Assert.Equal(2, p.Itens.Count);
        Assert.Equal("1000036", p.Itens[0].Produto);
        Assert.True(p.Itens[0].AdministradoPorLote);
        Assert.False(p.Itens[1].AdministradoPorLote);
        Assert.Equal("https://exemplo.invalido/segue", p.ProximaPagina);
    }

    [Fact]
    public void PaginaDoCentro_SemNext_EUltimaPagina()
    {
        PaginaCatalogoProdutoSap p = CatalogoProdutoSapApiClient.InterpretarPaginaDoCentro(
            """{"d":{"results":[{"Product":"1000090","Plant":"3007"}]}}""");

        Assert.True(p.Valida);
        Assert.Null(p.ProximaPagina);
        Assert.Null(p.Itens[0].AdministradoPorLote);   // ausente => indeterminado, nunca false
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao e json")]
    [InlineData("""{"sem_d":1}""")]
    [InlineData("""{"d":{"results":"nao e array"}}""")]
    public void PaginaDoCentro_EnvelopeInvalido_NaoViraCentroSemProdutos(string json)
    {
        // A diferenca importa: "nao consegui ler" nao pode virar "este centro nao tem produtos",
        // que apagaria o catalogo numa sincronizacao.
        PaginaCatalogoProdutoSap p = CatalogoProdutoSapApiClient.InterpretarPaginaDoCentro(json);
        Assert.False(p.Valida);
        Assert.Empty(p.Itens);
        Assert.False(string.IsNullOrWhiteSpace(p.Motivo));
    }

    [Fact]
    public void PaginaDoCentro_IgnoraLinhaSemMaterial()
    {
        PaginaCatalogoProdutoSap p = CatalogoProdutoSapApiClient.InterpretarPaginaDoCentro(
            """{"d":{"results":[{"Plant":"3007"},{"Product":"1000090","Plant":"3007"}]}}""");

        Assert.True(p.Valida);
        Assert.Single(p.Itens);
        Assert.Equal("1000090", p.Itens[0].Produto);
    }

    // ---------------------------------------------------------------- parse do mestre

    [Fact]
    public void Mestre_LeOsCamposQueOCentroNaoTraz()
    {
        const string json = """
            {"d":{"results":[
              {"Product":"1000090","ProductType":"ROH","BaseUnit":"KG",
               "ProductGroup":"000010524","CrossPlantStatus":"","IsMarkedForDeletion":false}
            ]}}
            """;

        ProdutoMestreSapCatalogo m = CatalogoProdutoSapApiClient.InterpretarProdutosMestre(json)[0];

        Assert.Equal("ROH", m.TipoMaterial);
        Assert.Equal("KG", m.UnidadeBase);
        Assert.Equal("000010524", m.GrupoMercadoria);
        Assert.Null(m.StatusMaterial);               // vazio no SAP => nulo aqui, nao string vazia
        Assert.False(m.MarcadoParaEliminacao);
    }

    [Fact]
    public void Mestre_BooleanoComoTexto_TambemEInterpretado()
    {
        // OData V2 as vezes serializa Edm.Boolean como "true"/"false" em texto.
        ProdutoMestreSapCatalogo m = CatalogoProdutoSapApiClient.InterpretarProdutosMestre(
            """{"d":{"results":[{"Product":"X","IsMarkedForDeletion":"true"}]}}""")[0];
        Assert.True(m.MarcadoParaEliminacao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("quebrado")]
    [InlineData("""{"d":{}}""")]
    public void Mestre_EnvelopeInvalido_DevolveVazio(string json)
        => Assert.Empty(CatalogoProdutoSapApiClient.InterpretarProdutosMestre(json));
}
