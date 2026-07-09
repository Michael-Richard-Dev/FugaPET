using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.9.1: separação Matéria-Prima × Químico pelos COMPONENTES da OP (ProductType do Product Master),
/// não pelo produto produzido. Cobre classificador, descrições, DTO/modelo, cliente Product Master e enriquecimento.
/// </summary>
public sealed class ConsumoClassificacaoComponente2291Tests
{
    // ---- Ajuste 5: classificador de componente por ProductType ----

    [Theory]
    [InlineData("ROH", ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData("roh", ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData("HIBE", ClassificacaoConsumoMaterial.Quimico)]
    [InlineData("VERP", ClassificacaoConsumoMaterial.Embalagem)]
    [InlineData("FERT", ClassificacaoConsumoMaterial.Outro)]
    [InlineData("HALB", ClassificacaoConsumoMaterial.Outro)]
    public void ClassificarComponente_DeveMapearPorProductType(string productType, ClassificacaoConsumoMaterial esperado)
        => Assert.Equal(esperado, ClassificadorComponenteConsumo.ClassificarComponenteParaConsumo(productType, productGroup: null));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ClassificarComponente_SemProductType_DeveSerIndefinido(string? productType)
        => Assert.Equal(
            ClassificacaoConsumoMaterial.Indefinido,
            ClassificadorComponenteConsumo.ClassificarComponenteParaConsumo(productType, productGroup: null));

    // ---- Ajuste 4: descrição amigável (mas a regra técnica usa o CÓDIGO) ----

    [Theory]
    [InlineData("ROH", "Matéria-prima")]
    [InlineData("HIBE", "Suprimento operacional / consumível")]
    [InlineData("VERP", "Material de embalagem")]
    [InlineData("FERT", "Produto acabado")]
    [InlineData("ZZZ", "Tipo SAP não mapeado")]
    public void ObterDescricaoTipoMaterialSap_DeveDescreverProductType(string productType, string esperado)
        => Assert.Equal(esperado, ClassificadorComponenteConsumo.ObterDescricaoTipoMaterialSap(productType));

    // ---- Ajuste 3: DTO/modelo do Product Master (preserva código, sem conversão numérica) ----

    [Fact]
    public void ProdutoSapMestre_DeDto_DevePreservarCamposEZeros()
    {
        SapProductMasterDto dto = new()
        {
            Product = " 000100 ",
            ProductType = " ROH ",
            ProductGroup = " QUIM ",
            BaseUnit = " KG "
        };

        ProdutoSapMestre mestre = ProdutoSapMestre.DeDto(dto);

        Assert.Equal("000100", mestre.CodigoProduto);
        Assert.Equal("ROH", mestre.TipoMaterialSap);
        Assert.Equal("QUIM", mestre.GrupoMaterialSap);
        Assert.Equal("KG", mestre.UnidadeBaseSap);
        Assert.True(mestre.Consultado);
    }

    // ---- Ajuste 2: cliente Product Master (URL + parse), sem POST/PATCH ----

    [Fact]
    public void MontarUrlProduto_DeveGerarGetPorChaveComSelect()
    {
        Uri url = ProductMasterSapApiClient.MontarUrlProduto(
            new Uri("https://sap.exemplo.com/API_PRODUCT_SRV/"), "000100", sapClient: "700");

        string texto = url.ToString();
        Assert.Contains("/API_PRODUCT_SRV/A_Product('000100')", texto, StringComparison.Ordinal);
        Assert.Contains("$select=Product,ProductType,ProductGroup,BaseUnit", texto, StringComparison.Ordinal);
        Assert.Contains("$format=json", texto, StringComparison.Ordinal);
        Assert.Contains("sap-client=700", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void ParsearProduto_DeveLerEnvelopeODataV2()
    {
        const string json = """
        {"d":{"Product":"000100","ProductType":"ROH","ProductGroup":"QUIM","BaseUnit":"KG"}}
        """;

        SapProductMasterDto? dto = ProductMasterSapApiClient.ParsearProduto(json);

        Assert.NotNull(dto);
        Assert.Equal("000100", dto!.Product);
        Assert.Equal("ROH", dto.ProductType);
        Assert.Equal("QUIM", dto.ProductGroup);
        Assert.Equal("KG", dto.BaseUnit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é json")]
    public void ParsearProduto_EntradaInvalida_DeveRetornarNull(string? json)
        => Assert.Null(ProductMasterSapApiClient.ParsearProduto(json));

    // ---- Ajuste 2/5/10: enriquecimento do componente + fallback quando não consultado ----

    [Fact]
    public void Enriquecer_ComProductType_DeveClassificarEDescrever()
    {
        ComponenteConsumoMaterial roh = new() { CodigoMaterial = "MP1" };
        ComponenteConsumoMaterial hibe = new() { CodigoMaterial = "QM1" };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            [roh, hibe],
            codigo => codigo == "MP1"
                ? new ProdutoSapMestre { CodigoProduto = "MP1", TipoMaterialSap = "ROH", Consultado = true }
                : new ProdutoSapMestre { CodigoProduto = "QM1", TipoMaterialSap = "HIBE", Consultado = true });

        Assert.True(roh.TipoMaterialConsultado);
        Assert.Equal(ClassificacaoConsumoMaterial.MateriaPrima, roh.ClassificacaoConsumo);
        Assert.Equal("Matéria-prima", roh.DescricaoTipoMaterial);
        Assert.Equal(ClassificacaoConsumoMaterial.Quimico, hibe.ClassificacaoConsumo);
    }

    [Fact]
    public void Enriquecer_SemConsultarProductMaster_DeveFicarIndefinidoNaoLiberaPorChute()
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "X1" };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial([componente], _ => null);

        Assert.False(componente.TipoMaterialConsultado);
        Assert.Equal(ClassificacaoConsumoMaterial.Indefinido, componente.ClassificacaoConsumo);
    }
}
