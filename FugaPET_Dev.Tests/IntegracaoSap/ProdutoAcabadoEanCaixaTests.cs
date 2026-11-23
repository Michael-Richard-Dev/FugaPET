using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// EAN da CAIXA na etiqueta de Produto Acabado (pedido do usuario em 23/11/2026, com print da MM02).
/// <para>
/// Origem: <c>A_ProductUnitsOfMeasureEAN</c>, linha da unidade <c>CX</c>. O nome PLURAL da colecao e os
/// campos abaixo foram verificados em 23/11/2026 contra o documento de servico e um retorno REAL do
/// DEV110 — os JSONs deste arquivo reproduzem a forma observada, nao uma forma imaginada.
/// </para>
/// <para>
/// O QUE ESTES TESTES GUARDAM, acima de tudo: a etiqueta nunca imprime uma barra que nao seja o EAN da
/// CAIXA daquele produto. Vazio, unidade errada ou verificador que nao fecha devem resultar em AVISO, e
/// nao em barra — porque uma barra errada e lida pelo cliente como OUTRO produto, e ninguem percebe
/// olhando a etiqueta.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoEanCaixaTests
{
    // Forma REAL do retorno do DEV110 (observada em 23/11/2026), inclusive o booleano como STRING.
    private const string JsonComCx = """
        {"d":{"results":[
          {"Product":"4000548","AlternativeUnit":"UN","ProductStandardID":"7891000111111","InternationalArticleNumberCat":"HE","IsMainGlobalTradeItemNumber":"true"},
          {"Product":"4000548","AlternativeUnit":"CX","ProductStandardID":"7891000258552","InternationalArticleNumberCat":"HE","IsMainGlobalTradeItemNumber":"false"}
        ]}}
        """;

    // ---------------------------------------------------------------- escolha da linha

    [Fact]
    public void EscolheOEanDaCaixa_MesmoQuandoOPrincipalEDeOutraUnidade()
    {
        IReadOnlyList<SapProdutoEanDto> eans = ProductMasterSapApiClient.ParsearEans(JsonComCx);

        Assert.Equal(2, eans.Count);
        // A linha de UN e a "principal", e mesmo assim NAO pode vencer: e o EAN do pacote avulso.
        Assert.Equal("7891000258552", ProductMasterSapApiClient.EscolherEanDaCaixa(eans));
    }

    [Fact]
    public void SemLinhaDeCaixa_NaoCaiParaOutraUnidade()
    {
        const string soUn = """
            {"d":{"results":[
              {"Product":"X","AlternativeUnit":"UN","ProductStandardID":"7891000111111","IsMainGlobalTradeItemNumber":"true"}
            ]}}
            """;

        Assert.Equal(string.Empty, ProductMasterSapApiClient.EscolherEanDaCaixa(ProductMasterSapApiClient.ParsearEans(soUn)));
    }

    /// <summary>
    /// Em 23/11/2026 a entidade vizinha devolveu <c>009</c> onde a MM02 mostra <c>CX</c>. Se isso acontecer
    /// tambem aqui, o certo e NAO imprimir — nao adivinhar que 009 e caixa.
    /// </summary>
    [Fact]
    public void CodigoInternoDeUnidade_NaoEConfundidoComCaixa()
    {
        const string interno = """
            {"d":{"results":[{"Product":"X","AlternativeUnit":"009","ProductStandardID":"7891000258552"}]}}
            """;

        Assert.Equal(string.Empty, ProductMasterSapApiClient.EscolherEanDaCaixa(ProductMasterSapApiClient.ParsearEans(interno)));
    }

    [Theory]
    [InlineData("cx")]
    [InlineData(" CX ")]
    public void UnidadeCaixa_ComparaSemCaixaAltaEComEspacos(string unidade)
    {
        List<SapProdutoEanDto> eans = [new() { AlternativeUnit = unidade, ProductStandardID = "7891000258552" }];

        Assert.Equal("7891000258552", ProductMasterSapApiClient.EscolherEanDaCaixa(eans));
    }

    [Fact]
    public void LinhaDeCaixaSemNumero_NaoVale()
    {
        List<SapProdutoEanDto> eans = [new() { AlternativeUnit = "CX", ProductStandardID = "   " }];

        Assert.Equal(string.Empty, ProductMasterSapApiClient.EscolherEanDaCaixa(eans));
    }

    [Fact]
    public void VariasLinhasDeCaixa_PreferemAPrincipal()
    {
        List<SapProdutoEanDto> eans =
        [
            new() { AlternativeUnit = "CX", ProductStandardID = "7891000111118" },
            new() { AlternativeUnit = "CX", ProductStandardID = "7891000258552", IsMainGlobalTradeItemNumber = true }
        ];

        Assert.Equal("7891000258552", ProductMasterSapApiClient.EscolherEanDaCaixa(eans));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("isto nao e json")]
    [InlineData("{\"d\":{\"results\":[]}}")]
    public void EntradaRuimOuVazia_NaoLancaENaoInventaEan(string? json)
    {
        Assert.Equal(string.Empty, ProductMasterSapApiClient.EscolherEanDaCaixa(ProductMasterSapApiClient.ParsearEans(json)));
    }

    // ---------------------------------------------------------------- URL

    [Fact]
    public void Url_UsaAColecaoNoPluralEFiltraSoPorProduto()
    {
        Uri url = ProductMasterSapApiClient.MontarUrlEansProduto(
            new Uri("https://host/sap/opu/odata/sap/API_PRODUCT_SRV/"), "4000548", "110");

        // Uri.ToString() DESFAZ o escape, entao comparar com a forma escapada falha mesmo com a URL certa.
        // A comparacao e na forma legivel; quem carrega o escape ate o servidor e o AbsoluteUri.
        string texto = Uri.UnescapeDataString(url.ToString());
        // PLURAL: o singular rende 404, como ja aconteceu com A_Batch x Batch.
        Assert.Contains("A_ProductUnitsOfMeasureEAN", texto, StringComparison.Ordinal);
        Assert.Contains("sap-client=110", texto, StringComparison.Ordinal);
        Assert.Contains("Product eq '4000548'", texto, StringComparison.Ordinal);
        // A unidade e PEDIDA ($select, para a escolha acontecer no codigo) mas NAO FILTRADA no servidor —
        // ver o comentario de MontarUrlEansProduto. Olhar a URL inteira confundiria as duas coisas, entao
        // a assercao mira so o trecho do $filter.
        Assert.Contains("AlternativeUnit", texto, StringComparison.Ordinal);
        string filtro = texto.Split("$filter=")[1].Split('&')[0];
        Assert.DoesNotContain("AlternativeUnit", filtro, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- digito verificador

    [Fact]
    public void Ean13DoPrintDoUsuario_TemVerificadorValido()
        => Assert.True(ServicoImpressoraZebra.EhEan13Valido("7891000258552"));

    [Theory]
    [InlineData("7891000258553")]  // verificador trocado
    [InlineData("789100025855")]   // 12 digitos
    [InlineData("97908243800363")] // GTIN-14, existe neste mandante
    [InlineData("789100025855A")]
    [InlineData("")]
    [InlineData(null)]
    public void NaoEhEan13_QuandoOVerificadorNaoFechaOuOFormatoNaoBate(string? valor)
        => Assert.False(ServicoImpressoraZebra.EhEan13Valido(valor));

    // ---------------------------------------------------------------- ZPL

    private static DadosEtiquetaCaixaProdutoAcabado Etiqueta(string ean) => new()
    {
        Material = "4000548",
        DescricaoMaterial = "DOGUITOS CARNE 20 X 65 G TEST",
        Lote = "L1",
        HandlingUnitSap = "300015994",
        PesoBruto = "10,000 KG",
        PesoLiquido = "9,500 KG",
        Quantidade = "20 UN",
        EanCaixa = ean
    };

    [Fact]
    public void ComEan13_SaiBarraEanComOsDozePrimeirosDigitos()
    {
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(Etiqueta("7891000258552"));

        // ^BE = EAN-13. Os 12 digitos sao o que a impressora espera; ela calcula o 13o.
        Assert.Contains("^BEN,", zpl, StringComparison.Ordinal);
        Assert.Contains("^FD789100025855^FS", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("NAO CADASTRADO", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void SemEan_MantemOAvisoENaoImprimeBarraDeEan()
    {
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(Etiqueta(string.Empty));

        Assert.Contains("NAO CADASTRADO", zpl, StringComparison.Ordinal);
        Assert.DoesNotContain("^BEN,", zpl, StringComparison.Ordinal);
    }

    /// <summary>
    /// Um EAN que nao e EAN-13 valido ainda assim e DADO REAL do SAP: vai em Code128, com o valor exato.
    /// O que nao pode e ir em ^BE, onde a impressora recalcularia o verificador e a barra passaria a
    /// codificar um numero diferente do impresso.
    /// </summary>
    [Fact]
    public void Gtin14_VaiEmCode128ComOValorExato()
    {
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(Etiqueta("97908243800363"));

        Assert.DoesNotContain("^BEN,", zpl, StringComparison.Ordinal);
        Assert.Contains("^FD97908243800363^FS", zpl, StringComparison.Ordinal);
    }

    /// <summary>A barra da HU, que ja existia, nao pode ter sido deslocada nem perdida pela chegada do EAN.</summary>
    [Fact]
    public void BarraDaHuContinuaNaEtiqueta()
    {
        string zpl = new ServicoImpressoraZebra().ConstruirZplEtiquetaCaixaProdutoAcabado(Etiqueta("7891000258552"));

        Assert.Contains("^BCB,", zpl, StringComparison.Ordinal);
        Assert.Contains("^FD300015994^FS", zpl, StringComparison.Ordinal);
    }

    [Fact]
    public void BarraDoEanCabeNoEspacoReservado()
    {
        // Espaco real do bloco na etiqueta 8x5, calculado da mesma geometria do ZPL.
        const int larguraDisponivel = 296;
        int modulo = ServicoImpressoraZebra.EscolherModuloEan("7891000258552", larguraDisponivel);
        int largura = ServicoImpressoraZebra.LarguraEstimadaEanDots("7891000258552", modulo);

        Assert.InRange(modulo, 1, 3);
        Assert.True(
            largura + (2 * ServicoImpressoraZebra.ZonaSilencioEan) <= larguraDisponivel,
            $"EAN com modulo {modulo} ocupa {largura} pontos e estoura os {larguraDisponivel} disponiveis.");
    }

    // ---------------------------------------------------------------- montagem da etiqueta

    [Fact]
    public void EanEntraNaEtiquetaPeloServicoDeImpressao()
    {
        DadosEtiquetaCaixaProdutoAcabado e = ImpressaoProdutoAcabadoServico.CriarEtiqueta(
            CaixaConfirmada(), "DOGUITOS", " 7891000258552 ");

        Assert.Equal("7891000258552", e.EanCaixa);
    }

    [Fact]
    public void SemEanInformado_AEtiquetaNasceComEanVazio()
    {
        DadosEtiquetaCaixaProdutoAcabado e =
            ImpressaoProdutoAcabadoServico.CriarEtiqueta(CaixaConfirmada(), "DOGUITOS");

        Assert.Equal(string.Empty, e.EanCaixa);
    }

    private static ProdutoAcabadoCaixa CaixaConfirmada() => new()
    {
        CodigoProdutoAcabadoCaixa = 1,
        CodigoCaixaLocal = "CX-1",
        NumeroOrdemProducao = "1002762",
        ItemOrdemProducao = "0001",
        Material = "4000548",
        Lote = "L1",
        NumeroCaixa = 1,
        HandlingUnitExternalId = "300015994",
        StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
        Terminal = "T1"
    };
}
