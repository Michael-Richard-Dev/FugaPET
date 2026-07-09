using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10: descrição REAL do material vem de A_ProductDescription (ProductDescription);
/// A_Product segue só como fonte técnica (ProductType/ProductGroup/BaseUnit).
/// </summary>
public sealed class ProductDescriptionSap2210Tests
{
    private static readonly Uri BaseUri = new("https://sap.exemplo.com/sap/opu/odata/sap/API_PRODUCT_SRV/");

    // ---- Ajuste 3: URL de A_ProductDescription ----

    [Fact]
    public void MontarUrlDescricao_SemIdioma_FiltraSomentePorProduct()
    {
        Uri url = ProductMasterSapApiClient.MontarUrlDescricaoProduto(BaseUri, "1000037");
        string texto = Uri.UnescapeDataString(url.ToString());

        Assert.Contains("/API_PRODUCT_SRV/A_ProductDescription?", texto, StringComparison.Ordinal);
        Assert.Contains("$filter=Product eq '1000037'", texto, StringComparison.Ordinal);
        Assert.Contains("$select=Product,Language,ProductDescription", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Language eq", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void MontarUrlDescricao_ComIdioma_UsaFiltroComposto()
    {
        Uri url = ProductMasterSapApiClient.MontarUrlDescricaoProduto(BaseUri, "1000037", "PT");
        string texto = Uri.UnescapeDataString(url.ToString());

        Assert.Contains("Product eq '1000037' and Language eq 'PT'", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void MontarUrlDescricao_DevePreservarZerosDoCodigo()
    {
        Uri url = ProductMasterSapApiClient.MontarUrlDescricaoProduto(BaseUri, " 000100 ");
        string texto = Uri.UnescapeDataString(url.ToString());

        Assert.Contains("Product eq '000100'", texto, StringComparison.Ordinal);
    }

    // ---- Ajuste 1: parse do envelope OData V2 (d.results) ----

    [Fact]
    public void ParsearDescricoes_DeveLerResultsODataV2()
    {
        const string json = """
        {"d":{"results":[
          {"Product":"1000037","Language":"PT","ProductDescription":"COURO WET BLUE"},
          {"Product":"1000037","Language":"EN","ProductDescription":"WET BLUE LEATHER"}
        ]}}
        """;

        IReadOnlyList<SapProductDescriptionDto> lista = ProductMasterSapApiClient.ParsearDescricoes(json);

        Assert.Equal(2, lista.Count);
        Assert.Equal("PT", lista[0].Language);
        Assert.Equal("COURO WET BLUE", lista[0].ProductDescription);
    }

    [Fact]
    public void ParsearDescricoes_DeveLerObjetoUnicoSobD()
    {
        const string json = """{"d":{"Product":"1000037","Language":"PT","ProductDescription":"COURO"}}""";

        IReadOnlyList<SapProductDescriptionDto> lista = ProductMasterSapApiClient.ParsearDescricoes(json);

        Assert.Single(lista);
        Assert.Equal("COURO", lista[0].ProductDescription);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("não é json")]
    public void ParsearDescricoes_EntradaInvalida_DeveRetornarListaVazia(string? json)
        => Assert.Empty(ProductMasterSapApiClient.ParsearDescricoes(json));

    // ---- Ajuste 5: escolha do melhor idioma (PT → P → EN → primeira) ----

    [Fact]
    public void EscolherMelhorDescricao_ComPt_DeveUsarPt()
    {
        SapProductDescriptionDto? escolhida = ProductMasterSapApiClient.EscolherMelhorDescricao(
        [
            new() { Language = "EN", ProductDescription = "LEATHER" },
            new() { Language = "PT", ProductDescription = "COURO" }
        ]);

        Assert.Equal("PT", escolhida!.Language);
        Assert.Equal("COURO", escolhida.ProductDescription);
    }

    [Fact]
    public void EscolherMelhorDescricao_SemPt_DeveUsarP()
    {
        SapProductDescriptionDto? escolhida = ProductMasterSapApiClient.EscolherMelhorDescricao(
        [
            new() { Language = "EN", ProductDescription = "LEATHER" },
            new() { Language = "P", ProductDescription = "COURO P" }
        ]);

        Assert.Equal("P", escolhida!.Language);
    }

    [Fact]
    public void EscolherMelhorDescricao_SemPtNemP_DeveUsarEn()
    {
        SapProductDescriptionDto? escolhida = ProductMasterSapApiClient.EscolherMelhorDescricao(
        [
            new() { Language = "DE", ProductDescription = "LEDER" },
            new() { Language = "EN", ProductDescription = "LEATHER" }
        ]);

        Assert.Equal("EN", escolhida!.Language);
    }

    [Fact]
    public void EscolherMelhorDescricao_SemIdiomaPreferido_DeveUsarPrimeiraComTexto()
    {
        SapProductDescriptionDto? escolhida = ProductMasterSapApiClient.EscolherMelhorDescricao(
        [
            new() { Language = "DE", ProductDescription = "" },
            new() { Language = "FR", ProductDescription = "CUIR" }
        ]);

        Assert.Equal("FR", escolhida!.Language);
        Assert.Equal("CUIR", escolhida.ProductDescription);
    }

    [Fact]
    public void EscolherMelhorDescricao_PtVaziaEEnComTexto_DevePularPtVazia()
    {
        SapProductDescriptionDto? escolhida = ProductMasterSapApiClient.EscolherMelhorDescricao(
        [
            new() { Language = "PT", ProductDescription = "" },
            new() { Language = "EN", ProductDescription = "LEATHER" }
        ]);

        Assert.Equal("EN", escolhida!.Language);
    }

    [Fact]
    public void EscolherMelhorDescricao_ListaVazia_DeveRetornarNull()
        => Assert.Null(ProductMasterSapApiClient.EscolherMelhorDescricao([]));

    // ---- Ajuste 2/3: agregação A_Product (técnico) + A_ProductDescription (descrição) ----

    [Fact]
    public void Agregar_DeveJuntarTecnicoComDescricaoSemMisturarConceitos()
    {
        SapProductMasterDto tecnico = new() { Product = "1000037", ProductType = "ROH", ProductGroup = "Q1", BaseUnit = "KG" };
        SapProductDescriptionDto descricao = new() { Product = "1000037", Language = "PT", ProductDescription = "COURO WET BLUE" };

        ProdutoSapMestre mestre = ProdutoSapMestre.Agregar(tecnico, descricao);

        Assert.Equal("ROH", mestre.TipoMaterialSap);
        Assert.Equal("Q1", mestre.GrupoMaterialSap);
        Assert.Equal("KG", mestre.UnidadeBaseSap);
        Assert.Equal("COURO WET BLUE", mestre.DescricaoProdutoSap);
        Assert.Equal("PT", mestre.IdiomaDescricaoSap);
        Assert.True(mestre.Consultado);
    }

    [Fact]
    public void Agregar_SemDescricao_MantemTecnicoEDescricaoVazia()
    {
        SapProductMasterDto tecnico = new() { Product = "1000037", ProductType = "ROH" };

        ProdutoSapMestre mestre = ProdutoSapMestre.Agregar(tecnico, descricao: null);

        Assert.Equal("ROH", mestre.TipoMaterialSap);
        Assert.Equal(string.Empty, mestre.DescricaoProdutoSap);
    }

    // ---- Ajuste 4/5/6: join lógico no enriquecimento do componente ----

    [Fact]
    public void Enriquecer_DeveUsarDescricaoDeProductDescription_NaoDeA_Product()
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "1000037" };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            [componente],
            _ => new ProdutoSapMestre
            {
                CodigoProduto = "1000037",
                TipoMaterialSap = "ROH",
                GrupoMaterialSap = "Q1",
                UnidadeBaseSap = "KG",
                DescricaoProdutoSap = "COURO WET BLUE",
                IdiomaDescricaoSap = "PT",
                Consultado = true
            });

        Assert.Equal("COURO WET BLUE", componente.DescricaoMaterial); // descrição de A_ProductDescription
        Assert.Equal("ROH", componente.TipoMaterialSap);              // técnico de A_Product
        Assert.Equal("Q1", componente.GrupoMaterialSap);
        Assert.Equal("KG", componente.UnidadeBaseSap);
        Assert.Equal(ClassificacaoConsumoMaterial.MateriaPrima, componente.ClassificacaoConsumo);
    }

    [Fact]
    public void Enriquecer_SemDescricaoSap_UsaDescricaoDaOp()
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "X1", DescricaoMaterial = "DESCRICAO DA OP" };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            [componente],
            _ => new ProdutoSapMestre { TipoMaterialSap = "ROH", DescricaoProdutoSap = string.Empty, Consultado = true });

        Assert.Equal("DESCRICAO DA OP", componente.DescricaoMaterial);
    }

    [Fact]
    public void Enriquecer_SemDescricaoSapNemOp_FicaVazioParaFallbackDaGrid()
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "X1" };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial([componente], _ => null);

        Assert.Equal(string.Empty, componente.DescricaoMaterial);
    }

    // ---- Ajuste 6/12: grid não usa "Material <codigo>" e cai no fallback controlado ----

    [Fact]
    public void Grid_DescricaoProduto_UsaFallbackControladoSemMaterialCodigo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private static string ObterDescricaoProdutoGrid");

        Assert.Contains("DescricaoProdutoNaoRetornada", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("Material {componente.CodigoMaterial}", metodo, StringComparison.Ordinal);
        Assert.Contains("Descrição não retornada pelo SAP", form, StringComparison.Ordinal);
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximoMetodo > inicio, $"Fim do método não encontrado: {assinatura}");

        return fonte[inicio..proximoMetodo];
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
