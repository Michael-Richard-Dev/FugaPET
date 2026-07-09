using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10.1: chamada e parse REAIS da A_ProductDescription (caso validado no Postman pelo Richard:
/// produto 1000046 → "26136 PROPILENOGLICOL NT"). Descrição real flui até o componente SEM alterar a classificação.
/// </summary>
public sealed class ProductDescriptionReal22101Tests
{
    // JSON EXATO retornado pelo SAP no Postman (Ajuste 9).
    private const string JsonPostman = """
    {
      "d": {
        "results": [
          {
            "Product": "1000046",
            "Language": "PT",
            "ProductDescription": "26136 PROPILENOGLICOL NT"
          }
        ]
      }
    }
    """;

    // ---- Ajuste 3/9: parse do d.results com o JSON real ----

    [Fact]
    public void ParsearDescricoes_JsonRealDoPostman_DeveExtrairCampos()
    {
        IReadOnlyList<SapProductDescriptionDto> lista = ProductMasterSapApiClient.ParsearDescricoes(JsonPostman);

        Assert.Single(lista);
        Assert.Equal("1000046", lista[0].Product);
        Assert.Equal("PT", lista[0].Language);
        Assert.Equal("26136 PROPILENOGLICOL NT", lista[0].ProductDescription);
    }

    // ---- Ajuste 2/4/7: serviço real (com costura HTTP) devolve a descrição PT ----

    [Fact]
    public async Task Servico_ComJsonRealDoPostman_DeveRetornarDescricaoPt()
    {
        ProductDescriptionSapServico servico = new(
            new ConfiguracaoSap(),
            (_, _) => Task.FromResult<string?>(JsonPostman));

        ProdutoSapMestre? mestre = await servico.ObterDescricaoAsync("1000046");

        Assert.NotNull(mestre);
        Assert.Equal("1000046", mestre!.CodigoProduto);
        Assert.Equal("26136 PROPILENOGLICOL NT", mestre.DescricaoProdutoSap);
        Assert.Equal("PT", mestre.IdiomaDescricaoSap);
        Assert.False(mestre.Consultado); // A_Product (ProductType) NÃO consultado aqui
    }

    [Fact]
    public async Task Servico_SemResultados_DeveRetornarNullParaFallback()
    {
        ProductDescriptionSapServico servico = new(
            new ConfiguracaoSap(),
            (_, _) => Task.FromResult<string?>("""{"d":{"results":[]}}"""));

        Assert.Null(await servico.ObterDescricaoAsync("1000046"));
    }

    [Fact]
    public async Task Servico_RespostaInvalida_NaoQuebraERetornaNull()
    {
        ProductDescriptionSapServico servico = new(
            new ConfiguracaoSap(),
            (_, _) => Task.FromResult<string?>("<<lixo>>"));

        Assert.Null(await servico.ObterDescricaoAsync("1000046"));
    }

    // ---- Ajuste 6: mapa Product → descrição (join lógico) ----

    [Fact]
    public async Task ObterDescricoesComponentesAsync_DeveMapearPorProduto()
    {
        ConsumoMaterialServico servico = new(
            new FakeProdOrder(),
            () => throw new InvalidOperationException("repositório não deve ser usado"),
            () => throw new InvalidOperationException("SAP 261 não deve ser usado"),
            () => throw new InvalidOperationException("confirmação não deve ser usada"),
            () => new FakeDescricaoServico());

        IReadOnlyDictionary<string, ProdutoSapMestre> mapa =
            await servico.ObterDescricoesComponentesAsync(["1000046", "1000046", "  ", "9999999"]);

        Assert.True(mapa.ContainsKey("1000046"));
        Assert.Equal("26136 PROPILENOGLICOL NT", mapa["1000046"].DescricaoProdutoSap);
        Assert.False(mapa.ContainsKey("9999999")); // sem descrição → fora do mapa (fallback na grid)
    }

    // ---- Ajustes 11/12/13/14/17/18: descrição real chega ao componente sem mudar a classificação ----

    [Fact]
    public void Enriquecer_ComMapaReal_Componente1000046_RecebeDescricaoReal()
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "1000046" };
        Dictionary<string, ProdutoSapMestre> mapa = new(StringComparer.OrdinalIgnoreCase)
        {
            ["1000046"] = new ProdutoSapMestre
            {
                CodigoProduto = "1000046",
                DescricaoProdutoSap = "26136 PROPILENOGLICOL NT",
                IdiomaDescricaoSap = "PT",
                Consultado = false
            }
        };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            [componente], codigo => mapa.GetValueOrDefault(codigo.Trim()));

        // Ajuste 11/12: descrição real aplicada (não "Descrição não retornada pelo SAP" nem "Material 1000046").
        Assert.Equal("26136 PROPILENOGLICOL NT", componente.DescricaoMaterial);
        // Ajuste 18: separação Matéria-Prima/Químico inalterada (só descrição foi consultada → Indefinido).
        Assert.Equal(ClassificacaoConsumoMaterial.Indefinido, componente.ClassificacaoConsumo);
        Assert.False(componente.TipoMaterialConsultado);
    }

    [Fact]
    public void Enriquecer_SemMapa_MantemVazioParaFallbackControlado()
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "1000046" };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial([componente], _ => null);

        Assert.Equal(string.Empty, componente.DescricaoMaterial);
    }

    // ---- Ajuste 2: URL derivada de A_ProductDescription no Product Master ----

    [Fact]
    public void ProductMasterBaseUrlEfetiva_DeveDerivarApiProductSrv()
    {
        ConfiguracaoSap cfg = new()
        {
            MaterialDocumentBaseUrl = "https://sap.exemplo.com/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV",
            Usuario = "u",
            Senha = "p",
            HostsPermitidos = ["sap.exemplo.com"]
        };

        Assert.Equal(
            "https://sap.exemplo.com/sap/opu/odata/sap/API_PRODUCT_SRV",
            cfg.ProductMasterBaseUrlEfetiva);
        Assert.True(cfg.ProductMasterConfigurado);
    }

    private sealed class FakeProdOrder : IProductionOrderSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoEncontrada());
    }

    private sealed class FakeDescricaoServico : IProductDescriptionSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ProdutoSapMestre?> ObterDescricaoAsync(string codigoProduto, CancellationToken cancellationToken = default)
            => Task.FromResult<ProdutoSapMestre?>(
                codigoProduto.Trim() == "1000046"
                    ? new ProdutoSapMestre
                    {
                        CodigoProduto = "1000046",
                        DescricaoProdutoSap = "26136 PROPILENOGLICOL NT",
                        IdiomaDescricaoSap = "PT",
                        Consultado = false
                    }
                    : null);
    }
}
