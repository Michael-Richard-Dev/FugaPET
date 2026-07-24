using System.Net;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Cliente SAP de Ordens de Producao (API_PRODUCTION_ORDER_2_SRV): montagem da URL do GET e
/// mapeamento da resposta OData V2 (cabecalho + componentes/operacoes/itens). Sem POST/PATCH.
/// </summary>
public sealed class ProductionOrderSapApiClientTests
{
    private static ConfiguracaoSap Config() => new()
    {
        ProductionOrderBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV",
        Usuario = "user",
        Senha = "pass",
        SapClient = "110",
        HostsPermitidos = ["sap.example.com"]
    };

    [Fact]
    public async Task ConsultarOrdem_DeveMontarUrlDoServicoComExpandSapClientEBasicAuth()
    {
        // Corpo com 1 componente para NAO disparar o fallback (a URL capturada deve ser a do expand).
        HandlerCaptura handler = new(
            HttpStatusCode.OK,
            "{\"d\":{\"ManufacturingOrder\":\"1000001234\",\"to_ProductionOrderComponent\":{\"results\":[{\"Material\":\"COMP-1\"}]}}}");
        using HttpClient http = new(handler);
        ProductionOrderSapApiClient cliente = new(Config(), http);

        await cliente.ConsultarOrdemAsync("1000001234");

        string url = handler.UltimaRequisicao!.RequestUri!.ToString();
        Assert.Contains("API_PRODUCTION_ORDER_2_SRV/A_ProductionOrder_2('1000001234')", url, StringComparison.Ordinal);
        Assert.Contains("$expand=to_ProductionOrderComponent,to_ProductionOrderOperation,to_ProductionOrderItem", url, StringComparison.Ordinal);
        Assert.Contains("sap-client=110", url, StringComparison.Ordinal);
        Assert.Equal("Basic", handler.UltimaRequisicao.Headers.Authorization?.Scheme);
        Assert.Equal(HttpMethod.Get, handler.UltimaRequisicao.Method);
    }

    [Fact]
    public void ProductionOrderBaseUrlEfetiva_DeveDerivarDoMaterialDocumentQuandoVazia()
    {
        ConfiguracaoSap config = new()
        {
            MaterialDocumentBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            Usuario = "user",
            Senha = "pass",
            HostsPermitidos = ["sap.example.com"]
        };

        Assert.Equal(
            "https://sap.example.com:44300/sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/",
            config.ProductionOrderBaseUrlEfetiva);
        Assert.True(config.ProductionOrderConfigurado);
    }

    [Fact]
    public void ProductionOrderBaseUrlEfetiva_DevePreferirUrlExplicita()
    {
        ConfiguracaoSap config = new()
        {
            ProductionOrderBaseUrl = "https://sap.example.com/sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/",
            MaterialDocumentBaseUrl = "https://outro.example.com/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/"
        };

        Assert.Equal(
            "https://sap.example.com/sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/",
            config.ProductionOrderBaseUrlEfetiva);
    }

    [Fact]
    public async Task ConsultarOrdem_Status404_DeveRetornarNull()
    {
        HandlerCaptura handler = new(HttpStatusCode.NotFound, string.Empty);
        using HttpClient http = new(handler);
        ProductionOrderSapApiClient cliente = new(Config(), http);

        OrdemProducaoSap? ordem = await cliente.ConsultarOrdemAsync("9999999999");

        Assert.Null(ordem);
    }

    [Fact]
    public async Task ConsultarOrdem_QuandoExpandFalha_DeveCairNoFallbackSeparado()
    {
        HandlerRoteado handler = new();
        using HttpClient http = new(handler);
        List<string> diagnosticos = [];
        ProductionOrderSapApiClient cliente = new(Config(), http, m => diagnosticos.Add(m));

        OrdemProducaoSap? ordem = await cliente.ConsultarOrdemAsync("1000009");

        Assert.NotNull(ordem);
        Assert.Equal("1000009", ordem!.NumeroOrdem);
        Assert.True(ordem.Liberada);
        Assert.Single(ordem.Componentes);
        Assert.Equal("COMP-5678", ordem.Componentes[0].Material);
        // Diagnostico do expand foi registrado e o fallback chamou a colecao de componentes.
        Assert.Contains(diagnosticos, d => d.Contains("GET_EXPAND", StringComparison.Ordinal));
        Assert.Contains(handler.UrlsChamadas, u => u.Contains("A_ProductionOrderComponent_2", StringComparison.Ordinal));
        Assert.Contains(handler.UrlsChamadas, u => u.Contains("A_ProductionOrderOperation_2", StringComparison.Ordinal));
        Assert.Contains(handler.UrlsChamadas, u => u.Contains("A_ProductionOrderItem_2", StringComparison.Ordinal));
        string urlOperacoes = Assert.Single(handler.UrlsChamadas, u => u.Contains("A_ProductionOrderOperation_2", StringComparison.Ordinal));
        Assert.Contains("$orderby=ManufacturingOrderSequence,ManufacturingOrderOperation", urlOperacoes, StringComparison.Ordinal);
        Assert.DoesNotContain("$orderby=ProductionOrderOperation", urlOperacoes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultarOrdem_ExpandComComponentes_NaoUsaFallback()
    {
        HandlerExpand handler = new(corpoExpand: RetornoV2Completo, corpoComponentesFallback: "{\"d\":{\"results\":[]}}");
        using HttpClient http = new(handler);
        ProductionOrderSapApiClient cliente = new(Config(), http);

        OrdemProducaoSap? ordem = await cliente.ConsultarOrdemAsync("1000001234");

        Assert.NotNull(ordem);
        Assert.Equal(2, ordem!.Componentes.Count);
        // Expand ja trouxe componentes: NAO chama as colecoes separadas do fallback.
        Assert.DoesNotContain(handler.UrlsChamadas, u => u.Contains("A_ProductionOrderComponent_2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConsultarOrdem_ExpandSemComponentes_UsaFallback()
    {
        const string expandSemComponentes =
            "{\"d\":{\"ManufacturingOrder\":\"1000009\",\"OrderIsReleased\":\"X\",\"to_ProductionOrderComponent\":{\"results\":[]}}}";
        const string componentesFallback =
            "{\"d\":{\"results\":[{\"Material\":\"COMP-5678\",\"RequiredQuantity\":\"50.000\",\"BaseUnit\":\"PC\",\"GoodsMovementType\":\"261\"}]}}";
        HandlerExpand handler = new(expandSemComponentes, componentesFallback);
        using HttpClient http = new(handler);
        List<string> diagnosticos = [];
        ProductionOrderSapApiClient cliente = new(Config(), http, m => diagnosticos.Add(m));

        OrdemProducaoSap? ordem = await cliente.ConsultarOrdemAsync("1000009");

        Assert.NotNull(ordem);
        Assert.Single(ordem!.Componentes);
        Assert.Equal("COMP-5678", ordem.Componentes[0].Material);
        Assert.Contains(diagnosticos, d => d.Contains("GET_EXPAND_SEM_COMPONENTES", StringComparison.Ordinal));
        Assert.Contains(handler.UrlsChamadas, u => u.Contains("A_ProductionOrderComponent_2", StringComparison.Ordinal));
    }

    [Fact]
    public void MapearColecao_DeveLerJsonResultsEXmlFeed()
    {
        const string json = "{\"d\":{\"results\":[{\"Material\":\"A\"},{\"Material\":\"B\"}]}}";
        IReadOnlyList<string> doJson = ProductionOrderSapApiClient.MapearColecao(
            json,
            e => e.GetProperty("Material").GetString() ?? "",
            x => x.Element(System.Xml.Linq.XName.Get("Material", "http://schemas.microsoft.com/ado/2007/08/dataservices"))?.Value ?? "");
        Assert.Equal(["A", "B"], doJson);

        const string xml = """
        <feed xmlns="http://www.w3.org/2005/Atom"
              xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
              xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices">
          <entry><content><m:properties><d:Material>X</d:Material></m:properties></content></entry>
        </feed>
        """;
        IReadOnlyList<string> doXml = ProductionOrderSapApiClient.MapearColecao(
            xml,
            e => e.GetProperty("Material").GetString() ?? "",
            x => x.Element(System.Xml.Linq.XName.Get("Material", "http://schemas.microsoft.com/ado/2007/08/dataservices"))?.Value ?? "");
        Assert.Equal(["X"], doXml);
    }

    [Fact]
    public void MapearOrdem_DeveMapearCabecalhoComponentesOperacoesEItens()
    {
        OrdemProducaoSap? ordem = ProductionOrderSapApiClient.MapearOrdem(RetornoV2Completo);

        Assert.NotNull(ordem);
        Assert.Equal("1000001234", ordem!.NumeroOrdem);
        Assert.Equal("MAT-12345", ordem.MaterialProduzido);
        Assert.Equal("1000", ordem.Centro);
        Assert.Equal(100m, ordem.QuantidadePrevista);
        Assert.Equal("PC", ordem.Unidade);
        Assert.True(ordem.Liberada);   // OrderIsReleased = "X"
        Assert.False(ordem.Excluida);
        // Tarefa 14.1: data da OP mapeada do JSON (/Date(ms)/).
        Assert.True(ordem.DataOrdem.HasValue);
        Assert.Equal("MfgOrderScheduledStartDate", ordem.OrigemDataOrdem);

        Assert.Equal(2, ordem.Componentes.Count);
        ComponenteOrdemProducaoSap comp = ordem.Componentes[0];
        Assert.Equal("0000123456", comp.Reserva);
        Assert.Equal("0001", comp.ItemReserva);
        Assert.Equal("COMP-5678", comp.Material);
        Assert.Equal("0001", comp.Deposito);
        Assert.Equal(50m, comp.QuantidadeNecessaria);
        Assert.Equal(0m, comp.QuantidadeRetirada);
        Assert.Equal("PC", comp.UnidadeBase);
        Assert.Equal("261", comp.TipoMovimento);
        Assert.True(comp.BackflushSap);
        Assert.True(comp.ReservaFinalizada);
        Assert.True(comp.MarcadoParaEliminacao);
        Assert.True(comp.MaterialGranel);
        Assert.Equal("X", comp.TipoSplitLote);
        // Tarefa 12.1: operacao do componente (ManufacturingOrderOperation) mapeada do JSON.
        Assert.Equal("0060", comp.Operacao);
        Assert.Equal("000000001234", comp.OrderOperationInternalId);

        Assert.Single(ordem.Operacoes);
        Assert.Equal("0010", ordem.Operacoes[0].Operacao);
        Assert.Equal("000000001234", ordem.Operacoes[0].OrderOperationInternalId);
        Assert.Equal("MONT01", ordem.Operacoes[0].CentroTrabalho);

        Assert.Single(ordem.Itens);
        Assert.Equal("0001", ordem.Itens[0].ItemOrdem);
        Assert.Equal(100m, ordem.Itens[0].QuantidadePrevista);
    }

    [Fact]
    public void MapearOrdem_DevePriorizarCamposOficiaisDaOperacaoJson()
    {
        OrdemProducaoSap? ordem = ProductionOrderSapApiClient.MapearOrdem(RetornoOperacaoOficialJson);

        Assert.NotNull(ordem);
        OperacaoOrdemProducaoSap operacao = Assert.Single(ordem!.Operacoes);
        Assert.Equal("0050", operacao.Operacao);
        Assert.Equal("0", operacao.Sequencia);
        Assert.Equal("", operacao.Suboperacao);
        Assert.Equal("PESAGEM UMIDA", operacao.Descricao);
        Assert.Equal("3007", operacao.Centro);
        Assert.Equal("1", operacao.CentroTrabalho);
        Assert.Equal("00000005", operacao.OrderOperationInternalId);
        Assert.Equal(1000m, operacao.QuantidadePrevista);
        Assert.Equal(0m, operacao.QuantidadeConfirmada);
        Assert.Equal("KG", operacao.Unidade);
    }

    [Fact]
    public void MapearOrdem_DevePriorizarCamposOficiaisDaOperacaoXml()
    {
        OrdemProducaoSap? ordem = ProductionOrderSapApiClient.MapearOrdem(RetornoOperacaoOficialXml);

        Assert.NotNull(ordem);
        OperacaoOrdemProducaoSap operacao = Assert.Single(ordem!.Operacoes);
        Assert.Equal("0050", operacao.Operacao);
        Assert.Equal("0", operacao.Sequencia);
        Assert.Equal("", operacao.Suboperacao);
        Assert.Equal("PESAGEM UMIDA", operacao.Descricao);
        Assert.Equal("3007", operacao.Centro);
        Assert.Equal("1", operacao.CentroTrabalho);
        Assert.Equal("00000005", operacao.OrderOperationInternalId);
    }

    [Fact]
    public void MapearOrdem_DeveMapearRespostaXmlAtomComComponentesExpandidos()
    {
        OrdemProducaoSap? ordem = ProductionOrderSapApiClient.MapearOrdem(RetornoAtomXml);

        Assert.NotNull(ordem);
        Assert.Equal("1000009", ordem!.NumeroOrdem);
        Assert.Equal("YBM1", ordem.TipoOrdem);
        Assert.True(ordem.Liberada);   // OrderIsReleased = X
        Assert.False(ordem.Excluida);
        Assert.Single(ordem.Componentes);
        Assert.Equal("COMP-5678", ordem.Componentes[0].Material);
        Assert.Equal(50m, ordem.Componentes[0].QuantidadeNecessaria);
        Assert.Equal("261", ordem.Componentes[0].TipoMovimento);
        Assert.True(ordem.Componentes[0].BackflushSap);
        Assert.True(ordem.Componentes[0].ReservaFinalizada);
        // Tarefa 12.1: operacao do componente (ManufacturingOrderOperation) mapeada do XML.
        Assert.Equal("0060", ordem.Componentes[0].Operacao);
        Assert.Equal("000000001234", ordem.Componentes[0].OrderOperationInternalId);
        // Tarefa 14.1: data da OP mapeada do XML.
        Assert.True(ordem.DataOrdem.HasValue);
        Assert.Equal("MfgOrderScheduledStartDate", ordem.OrigemDataOrdem);
    }

    [Theory]
    [InlineData("/Date(1719792000000)/")]
    [InlineData("2026-06-30")]
    [InlineData("2026-06-30T00:00:00")]
    public void TentarParsearDataSap_AceitaFormatosSap(string entrada)
        => Assert.True(ProductionOrderSapApiClient.TentarParsearDataSap(entrada, out _));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-data")]
    public void TentarParsearDataSap_RejeitaInvalido(string entrada)
        => Assert.False(ProductionOrderSapApiClient.TentarParsearDataSap(entrada, out _));

    private const string RetornoAtomXml = """
    <?xml version="1.0" encoding="utf-8"?>
    <entry xmlns="http://www.w3.org/2005/Atom"
           xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
           xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices">
      <link rel="http://schemas.microsoft.com/ado/2007/08/dataservices/related/to_ProductionOrderComponent"
            title="to_ProductionOrderComponent" type="application/atom+xml;type=feed">
        <m:inline>
          <feed>
            <entry>
              <content type="application/xml">
                <m:properties>
                  <d:Reservation>0000123456</d:Reservation>
                  <d:ReservationItem>0001</d:ReservationItem>
                  <d:Material>COMP-5678</d:Material>
                  <d:Plant>1000</d:Plant>
                  <d:StorageLocation>0001</d:StorageLocation>
                  <d:RequiredQuantity>50.000</d:RequiredQuantity>
                  <d:BaseUnit>PC</d:BaseUnit>
                  <d:WithdrawnQuantity>0.000</d:WithdrawnQuantity>
                  <d:GoodsMovementType>261</d:GoodsMovementType>
                  <d:ReservationIsFinallyIssued>true</d:ReservationIsFinallyIssued>
                  <d:MatlCompIsMarkedForBackflush>X</d:MatlCompIsMarkedForBackflush>
                  <d:ManufacturingOrderOperation>0060</d:ManufacturingOrderOperation>
                  <d:OrderOperationInternalID>000000001234</d:OrderOperationInternalID>
                  <d:ManufacturingOrderSequence>0</d:ManufacturingOrderSequence>
                </m:properties>
              </content>
            </entry>
          </feed>
        </m:inline>
      </link>
      <content type="application/xml">
        <m:properties>
          <d:ManufacturingOrder>1000009</d:ManufacturingOrder>
          <d:ManufacturingOrderType>YBM1</d:ManufacturingOrderType>
          <d:Material>MAT-12345</d:Material>
          <d:ProductionPlant>1000</d:ProductionPlant>
          <d:TotalQuantity>100.000</d:TotalQuantity>
          <d:ProductionUnit>PC</d:ProductionUnit>
          <d:StorageLocation>0001</d:StorageLocation>
          <d:Batch></d:Batch>
          <d:OrderIsReleased>X</d:OrderIsReleased>
          <d:OrderIsConfirmed></d:OrderIsConfirmed>
          <d:OrderIsDeleted></d:OrderIsDeleted>
          <d:MfgOrderScheduledStartDate>2026-06-30T00:00:00</d:MfgOrderScheduledStartDate>
        </m:properties>
      </content>
    </entry>
    """;

    private const string RetornoOperacaoOficialXml = """
    <?xml version="1.0" encoding="utf-8"?>
    <entry xmlns="http://www.w3.org/2005/Atom"
           xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
           xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices">
      <link rel="http://schemas.microsoft.com/ado/2007/08/dataservices/related/to_ProductionOrderOperation"
            title="to_ProductionOrderOperation" type="application/atom+xml;type=feed">
        <m:inline>
          <feed>
            <entry>
              <content type="application/xml">
                <m:properties>
                  <d:ManufacturingOrderOperation>0050</d:ManufacturingOrderOperation>
                  <d:ManufacturingOrderSequence>0</d:ManufacturingOrderSequence>
                  <d:ManufacturingOrderSubOperation></d:ManufacturingOrderSubOperation>
                  <d:MfgOrderOperationText>PESAGEM UMIDA</d:MfgOrderOperationText>
                  <d:ProductionPlant>3007</d:ProductionPlant>
                  <d:WorkCenter>1</d:WorkCenter>
                  <d:OrderInternalBillOfOperations>0000000001</d:OrderInternalBillOfOperations>
                  <d:OrderIntBillOfOperationsItem>00000005</d:OrderIntBillOfOperationsItem>
                  <d:OpPlannedTotalQuantity>1000.000</d:OpPlannedTotalQuantity>
                  <d:OpTotalConfirmedYieldQty>0.000</d:OpTotalConfirmedYieldQty>
                  <d:OperationUnit>KG</d:OperationUnit>
                </m:properties>
              </content>
            </entry>
          </feed>
        </m:inline>
      </link>
      <content type="application/xml">
        <m:properties>
          <d:ManufacturingOrder>1001732</d:ManufacturingOrder>
          <d:ManufacturingOrderType>ZP01</d:ManufacturingOrderType>
          <d:Material>2000091</d:Material>
          <d:ProductionPlant>3007</d:ProductionPlant>
          <d:TotalQuantity>1000.000</d:TotalQuantity>
          <d:ProductionUnit>KG</d:ProductionUnit>
          <d:OrderIsReleased>X</d:OrderIsReleased>
        </m:properties>
      </content>
    </entry>
    """;

    private const string RetornoV2Completo = """
    {
      "d": {
        "ManufacturingOrder": "1000001234",
        "ManufacturingOrderType": "PP01",
        "Material": "MAT-12345",
        "ProductionPlant": "1000",
        "TotalQuantity": "100.000",
        "ProductionUnit": "PC",
        "StorageLocation": "0001",
        "Batch": "",
        "OrderIsReleased": "X",
        "OrderIsConfirmed": "",
        "OrderIsDeleted": "",
        "MfgOrderScheduledStartDate": "/Date(1719792000000)/",
        "to_ProductionOrderComponent": {
          "results": [
            {
              "Reservation": "0000123456",
              "ReservationItem": "0001",
              "Material": "COMP-5678",
              "Plant": "1000",
              "StorageLocation": "0001",
              "RequiredQuantity": "50.000",
              "BaseUnit": "PC",
              "WithdrawnQuantity": "0.000",
              "ConfirmedAvailableQuantity": "50.000",
              "GoodsMovementType": "261",
              "ReservationIsFinallyIssued": true,
              "MatlCompIsMarkedForDeletion": "X",
              "IsBulkMaterialComponent": "true",
              "MatlCompIsMarkedForBackflush": "X",
              "BatchSplitType": "X",
              "Batch": "",
              "BOMItem": "0010",
              "BOMItemCategory": "L",
              "ManufacturingOrderOperation": "0060",
              "OrderOperationInternalID": "000000001234",
              "ManufacturingOrderSequence": "0"
            },
            {
              "Reservation": "0000123456",
              "ReservationItem": "0002",
              "Material": "COMP-9012",
              "Plant": "1000",
              "StorageLocation": "0001",
              "RequiredQuantity": "25.000",
              "BaseUnit": "KG",
              "WithdrawnQuantity": "10.000",
              "ConfirmedAvailableQuantity": "15.000"
            }
          ]
        },
        "to_ProductionOrderOperation": {
          "results": [
            {
              "ProductionOrderOperation": "0010",
              "OrderOperationInternalID": "000000001234",
              "ProductionOrderSequence": "0",
              "WorkCenter": "MONT01",
              "Plant": "1000",
              "OperationText": "Montagem Principal",
              "OpPlannedTotalQuantity": "100.000",
              "OperationUnit": "PC"
            }
          ]
        },
        "to_ProductionOrderItem": {
          "results": [
            {
              "ManufacturingOrderItem": "0001",
              "Material": "MAT-12345",
              "StorageLocation": "0001",
              "MfgOrderItemPlannedTotalQty": "100.000",
              "MfgOrderItemActualDeliveryQty": "0.000",
              "Batch": ""
            }
          ]
        }
      }
    }
    """;

    private const string RetornoOperacaoOficialJson = """
    {
      "d": {
        "ManufacturingOrder": "1001732",
        "ManufacturingOrderType": "ZP01",
        "Material": "2000091",
        "ProductionPlant": "3007",
        "TotalQuantity": "1000.000",
        "ProductionUnit": "KG",
        "OrderIsReleased": "X",
        "to_ProductionOrderOperation": {
          "results": [
            {
              "ManufacturingOrder": "1001732",
              "ManufacturingOrderSequence": "0",
              "ManufacturingOrderOperation": "0050",
              "ManufacturingOrderSubOperation": "",
              "MfgOrderOperationText": "PESAGEM UMIDA",
              "ProductionPlant": "3007",
              "WorkCenter": "1",
              "OrderInternalBillOfOperations": "0000000001",
              "OrderIntBillOfOperationsItem": "00000005",
              "OpPlannedTotalQuantity": "1000.000",
              "OpTotalConfirmedYieldQty": "0.000",
              "OperationUnit": "KG"
            }
          ]
        }
      }
    }
    """;

    private sealed class HandlerCaptura : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _corpo;

        public HandlerCaptura(HttpStatusCode status, string corpo)
        {
            _status = status;
            _corpo = corpo;
        }

        public HttpRequestMessage? UltimaRequisicao { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaRequisicao = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_corpo)
            });
        }
    }

    // Roteia por URL: recusa o $expand (400) e responde 200 nas chamadas separadas do fallback.
    private sealed class HandlerRoteado : HttpMessageHandler
    {
        public List<string> UrlsChamadas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            UrlsChamadas.Add(url);
            (HttpStatusCode status, string corpo) = Responder(url);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(corpo) });
        }

        private static (HttpStatusCode, string) Responder(string url)
        {
            if (url.Contains("%24expand", StringComparison.Ordinal) || url.Contains("$expand", StringComparison.Ordinal))
            {
                return (HttpStatusCode.BadRequest,
                    "{\"error\":{\"message\":{\"value\":\"Expand nao suportado\"}}}");
            }

            if (url.Contains("A_ProductionOrderComponent_2", StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK,
                    "{\"d\":{\"results\":[{\"Material\":\"COMP-5678\",\"RequiredQuantity\":\"50.000\",\"BaseUnit\":\"PC\",\"GoodsMovementType\":\"261\"}]}}");
            }

            if (url.Contains("A_ProductionOrderOperation_2", StringComparison.Ordinal)
                || url.Contains("A_ProductionOrderItem_2", StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, "{\"d\":{\"results\":[]}}");
            }

            // Cabecalho (A_ProductionOrder_2('...') sem expand).
            return (HttpStatusCode.OK, "{\"d\":{\"ManufacturingOrder\":\"1000009\",\"OrderIsReleased\":\"X\"}}");
        }
    }

    // Responde o GET com expand (200) e, no fallback, as colecoes separadas.
    private sealed class HandlerExpand : HttpMessageHandler
    {
        private readonly string _corpoExpand;
        private readonly string _corpoComponentesFallback;

        public HandlerExpand(string corpoExpand, string corpoComponentesFallback)
        {
            _corpoExpand = corpoExpand;
            _corpoComponentesFallback = corpoComponentesFallback;
        }

        public List<string> UrlsChamadas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            UrlsChamadas.Add(url);

            string corpo;
            if (url.Contains("%24expand", StringComparison.Ordinal) || url.Contains("$expand", StringComparison.Ordinal))
            {
                corpo = _corpoExpand;
            }
            else if (url.Contains("A_ProductionOrderComponent_2", StringComparison.Ordinal))
            {
                corpo = _corpoComponentesFallback;
            }
            else if (url.Contains("A_ProductionOrderOperation_2", StringComparison.Ordinal)
                || url.Contains("A_ProductionOrderItem_2", StringComparison.Ordinal))
            {
                corpo = "{\"d\":{\"results\":[]}}";
            }
            else
            {
                corpo = "{\"d\":{\"ManufacturingOrder\":\"1000009\",\"OrderIsReleased\":\"X\"}}";
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(corpo) });
        }
    }
}
