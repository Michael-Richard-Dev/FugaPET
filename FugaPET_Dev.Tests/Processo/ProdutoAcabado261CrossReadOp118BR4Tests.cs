using System.Net;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 118B-R4: a OP retornada pelo SAP nas leituras frescas precisa ser PRESERVADA e VALIDADA.
/// <para>
/// Blocker corrigido (118E/Hermes): <c>ItemOrdemProducaoSap</c> descartava ManufacturingOrder e o
/// Controller alimentava <c>NumeroOrdemItemFresco</c> com <c>fresca.NumeroOrdemConsultada</c> — o eco
/// da propria request. A checagem "OP solicitada x OP do item" comparava a request consigo mesma e
/// portanto nao provava nada.
/// </para>
/// <para>
/// Os testes de WIRING abaixo atravessam materialmente: payload SAP fake -> HttpClient ->
/// ProductionOrderSapApiClient.ConsultarEstadoFresco261Async (parsing real, $inlinecount real) ->
/// LeituraFrescaOrdem261 -> ProdutoAcabado261AllocatorCumulativo.MontarEntrada (o MESMO seam que o
/// Controller usa em runtime) -> Calcular. Nenhum SAP real, nenhum banco.
/// </para>
/// </summary>
public sealed class ProdutoAcabado261CrossReadOp118BR4Tests
{
    private const string OpSolicitada = "1000001";
    private const string OpOutra = "1000002";

    private static ConfiguracaoSap Config() => new()
    {
        ProductionOrderBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV",
        Usuario = "user",
        Senha = "pass",
        SapClient = "110",
        HostsPermitidos = ["sap.example.com"]
    };

    private static ProdutoAcabadoCaixa Caixa() => new()
    {
        NumeroOrdemProducao = OpSolicitada,
        ItemOrdemProducao = "1",
        Material = "4000174",
        Lote = "169 26",
        Centro = "3007",
        Deposito = "PA01",
        QuantidadeProdutos = 8,
        UnidadeQuantidade = "UN",
        PesoBrutoKg = 14.400m,
        TaraKg = 0.050m,
        PesoLiquidoKg = 14.350m
    };

    // ===================== payloads SAP fake =====================

    /// <summary>Item fresco; <paramref name="opItem"/> null omite ManufacturingOrder do payload.</summary>
    private static string ItemJson(string? opItem)
    {
        string op = opItem is null ? string.Empty : $"\"ManufacturingOrder\": \"{opItem}\",";
        return $$"""
        {
          "d": {
            "__count": "1",
            "results": [
              {
                {{op}}
                "ManufacturingOrderItem": "1",
                "MfgOrderItemPlannedTotalQty": "9016.000",
                "MfgOrderItemGoodsReceiptQty": "0.000",
                "ProductionUnit": "UN",
                "Material": "4000174",
                "ProductionPlant": "3007",
                "StorageLocation": "PA01",
                "Batch": "169 26"
              }
            ]
          }
        }
        """;
    }

    /// <summary>Um componente proporcional; <paramref name="opComponente"/> null omite a OP.</summary>
    private static string ComponenteJson(string? opComponente)
    {
        string op = opComponente is null ? string.Empty : $"\"ManufacturingOrder\": \"{opComponente}\",";
        return $$"""
        {
          "d": {
            "__count": "1",
            "results": [
              {
                {{op}}
                "Reservation": "3092",
                "ReservationItem": "1",
                "Material": "2000219",
                "Plant": "3007",
                "StorageLocation": "PA01",
                "Batch": "",
                "BaseUnit": "KG",
                "RequiredQuantity": "1440.000",
                "WithdrawnQuantity": "0.000",
                "ConfirmedAvailableQuantity": "1440.000",
                "QuantityIsFixed": false,
                "GoodsMovementType": "261"
              }
            ]
          }
        }
        """;
    }

    /// <summary>
    /// Executa o fluxo REAL: HTTP fake -> cliente -> leitura fresca -> MontarEntrada -> Calcular.
    /// </summary>
    private static async Task<(LeituraFrescaOrdem261 Fresca, Resultado261Cumulativo Resultado)> ExecutarFluxoAsync(
        string? opItem, string? opComponente)
    {
        HandlerPorEntidade handler = new(ItemJson(opItem), ComponenteJson(opComponente));
        using HttpClient http = new(handler);
        ProductionOrderSapApiClient cliente = new(Config(), http);

        LeituraFrescaOrdem261 fresca = await cliente.ConsultarEstadoFresco261Async(OpSolicitada);

        Entrada261Cumulativa entrada = ProdutoAcabado261AllocatorCumulativo.MontarEntrada(
            fresca, Caixa(), OpSolicitada);

        return (fresca, ProdutoAcabado261AllocatorCumulativo.Calcular(entrada));
    }

    // ===================== §8 MAPPING: ManufacturingOrder preservada =====================

    [Fact]
    public async Task Mapping_Json_PreservaManufacturingOrderDoItem()
    {
        (LeituraFrescaOrdem261 fresca, _) = await ExecutarFluxoAsync(OpSolicitada, OpSolicitada);

        Assert.True(fresca.Disponivel, fresca.Mensagem);
        Assert.NotNull(fresca.Item);
        Assert.Equal(OpSolicitada, fresca.Item!.NumeroOrdem);
    }

    [Fact]
    public async Task Mapping_Json_SemManufacturingOrder_NaoViraEcoDaRequest()
    {
        (LeituraFrescaOrdem261 fresca, Resultado261Cumulativo r) =
            await ExecutarFluxoAsync(opItem: null, opComponente: OpSolicitada);

        // A OP consultada (eco) existe no envelope, mas NAO foi copiada para o item.
        Assert.Equal(OpSolicitada, fresca.NumeroOrdemConsultada);
        Assert.Equal(string.Empty, fresca.Item!.NumeroOrdem);
        Assert.NotEqual(fresca.NumeroOrdemConsultada, fresca.Item.NumeroOrdem);

        // E a ausencia bloqueia de verdade.
        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
    }

    [Fact]
    public void Mapping_Xml_PreservaManufacturingOrderDoItem()
    {
        OrdemProducaoSap? ordem = ProductionOrderSapApiClient.MapearOrdem(AtomComItem(OpOutra));

        Assert.NotNull(ordem);
        ItemOrdemProducaoSap item = Assert.Single(ordem!.Itens);
        Assert.Equal(OpOutra, item.NumeroOrdem);
    }

    [Fact]
    public void Mapping_Xml_SemManufacturingOrder_NaoViraEcoDaRequest()
    {
        OrdemProducaoSap? ordem = ProductionOrderSapApiClient.MapearOrdem(AtomComItem(null));

        Assert.NotNull(ordem);
        ItemOrdemProducaoSap item = Assert.Single(ordem!.Itens);
        Assert.Equal(string.Empty, item.NumeroOrdem);
    }

    private static string AtomComItem(string? opItem)
    {
        string op = opItem is null ? string.Empty : $"<d:ManufacturingOrder>{opItem}</d:ManufacturingOrder>";
        return $"""
        <?xml version="1.0" encoding="utf-8"?>
        <entry xmlns="http://www.w3.org/2005/Atom"
               xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
               xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices">
          <content type="application/xml">
            <m:properties>
              <d:ManufacturingOrder>{OpSolicitada}</d:ManufacturingOrder>
              <d:ProductionUnit>UN</d:ProductionUnit>
            </m:properties>
          </content>
          <link title="to_ProductionOrderItem">
            <m:inline>
              <feed xmlns="http://www.w3.org/2005/Atom">
                <entry>
                  <content type="application/xml">
                    <m:properties>
                      {op}
                      <d:ManufacturingOrderItem>1</d:ManufacturingOrderItem>
                      <d:MfgOrderItemPlannedTotalQty>9016.000</d:MfgOrderItemPlannedTotalQty>
                      <d:MfgOrderItemGoodsReceiptQty>0.000</d:MfgOrderItemGoodsReceiptQty>
                      <d:ProductionUnit>UN</d:ProductionUnit>
                    </m:properties>
                  </content>
                </entry>
              </feed>
            </m:inline>
          </link>
        </entry>
        """;
    }

    // ===================== §9 WIRING REAL — casos A a E =====================

    [Fact]
    public async Task CasoA_ItemDivergente_Bloqueia_SemComandos261E101()
    {
        // REQUEST 1000001 / ITEM SAP 1000002 / COMPONENTS 1000001
        (LeituraFrescaOrdem261 fresca, Resultado261Cumulativo r) =
            await ExecutarFluxoAsync(opItem: OpOutra, opComponente: OpSolicitada);

        Assert.Equal(OpOutra, fresca.Item!.NumeroOrdem);   // o mapeamento realmente trouxe a outra OP
        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("ITEM_MANUFACTURING_ORDER_MISMATCH", r.Mensagem, StringComparison.Ordinal);

        AssertNenhumComandoDoPipeline(r);
    }

    [Fact]
    public async Task CasoB_ItemSemOp_Bloqueia()
    {
        (_, Resultado261Cumulativo r) = await ExecutarFluxoAsync(opItem: null, opComponente: OpSolicitada);

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("ITEM_MANUFACTURING_ORDER_MISSING", r.Mensagem, StringComparison.Ordinal);

        AssertNenhumComandoDoPipeline(r);
    }

    [Fact]
    public async Task CasoC_ComponenteDivergente_Bloqueia()
    {
        (_, Resultado261Cumulativo r) = await ExecutarFluxoAsync(opItem: OpSolicitada, opComponente: OpOutra);

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("COMPONENT_MANUFACTURING_ORDER_MISMATCH", r.Mensagem, StringComparison.Ordinal);

        AssertNenhumComandoDoPipeline(r);
    }

    [Fact]
    public async Task CasoD_ComponenteSemOp_Bloqueia()
    {
        (_, Resultado261Cumulativo r) = await ExecutarFluxoAsync(opItem: OpSolicitada, opComponente: null);

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("COMPONENT_MANUFACTURING_ORDER_MISSING", r.Mensagem, StringComparison.Ordinal);

        AssertNenhumComandoDoPipeline(r);
    }

    [Fact]
    public async Task CasoE_TodosCoerentes_CrossReadPassa_EAlocadorProssegue()
    {
        (LeituraFrescaOrdem261 fresca, Resultado261Cumulativo r) =
            await ExecutarFluxoAsync(opItem: OpSolicitada, opComponente: OpSolicitada);

        Assert.Equal(OpSolicitada, fresca.Item!.NumeroOrdem);
        Assert.True(fresca.ComponentesCompletos);
        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);

        Item261Alocado item = Assert.Single(r.Itens);
        Assert.Equal("3092", item.Reservation);
        Assert.Equal("1", item.ReservationItem);
        Assert.Equal("KG", item.Unidade);
        Assert.Equal(8m, r.ProducedCumulative);
        // 1440 * 8 / 9016 = 1.2777... -> 1.278 (mesma aritmetica do 118B, inalterada)
        Assert.Equal(1.278m, item.Delta261);
        Assert.NotEqual(1440.000m, item.Delta261);
    }

    /// <summary>Alocacao bloqueada => origem sem componentes => nenhum command 261/101 (logo nem HU).</summary>
    private static void AssertNenhumComandoDoPipeline(Resultado261Cumulativo bloqueada)
    {
        Assert.Empty(ProcessoProdutoAcabadoForm.MontarComponentesPipelineRuntime(bloqueada));

        ProdutoAcabadoCaixa caixa = Caixa();
        caixa.CodigoProdutoAcabadoCaixa = 1;

        ProdutoAcabadoPipelineOrigem origem = ProcessoProdutoAcabadoForm.MontarOrigemPipelineRuntime(
            caixa, null, new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc), bloqueada);

        ResultadoComandosPipeline comandos = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        Assert.False(comandos.Sucesso);
        Assert.Null(comandos.Comando261);
        Assert.Null(comandos.Comando101);
    }

    // ===================== §7 consistencia tripla (unitaria, direta) =====================

    [Theory]
    // item, componente, deve bloquear
    [InlineData("1000001", "1000001", false)]
    [InlineData("1000002", "1000001", true)]
    [InlineData("1000001", "1000002", true)]
    [InlineData("", "1000001", true)]
    [InlineData("   ", "1000001", true)]
    [InlineData("1000001", "", true)]
    [InlineData("1000001", "   ", true)]
    [InlineData("1000002", "1000002", true)]
    public void ConsistenciaTripla_SoPassaQuandoSolicitadaItemEComponenteSaoIguaisENaoVazias(
        string opItem, string opComponente, bool deveBloquear)
    {
        Componente261Fresco componente = new()
        {
            NumeroOrdem = opComponente,
            Reservation = "3092",
            ReservationItem = "1",
            Material = "2000219",
            Plant = "3007",
            StorageLocation = "PA01",
            BaseUnit = "KG",
            TipoMovimento = "261",
            RequiredQuantity = 1440.000m,
            WithdrawnQuantity = 0m,
            QuantityIsFixed = false
        };

        Entrada261Cumulativa entrada = new()
        {
            NumeroOrdem = OpSolicitada,
            NumeroOrdemItemFresco = opItem,
            PlannedProductionOp = 9016m,
            PriorProducedConfirmed = 0m,
            CurrentBoxProduction = 8m,
            ProductionUnit = "UN",
            Componentes = [componente],
            ComponentesCompletosComprovado = true,
            ConsumoLocalConfirmadoPorComponente = new Dictionary<string, decimal> { ["3092/1"] = 0m }
        };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);

        if (deveBloquear)
        {
            Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
            Assert.Empty(r.Itens);
        }
        else
        {
            Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        }
    }

    [Fact]
    public void ConsistenciaTripla_ItemDivergenteTemPrecedenciaSobreComponente()
    {
        // Com item divergente, o bloqueio e de ITEM — nao deve "passar" para a checagem de componente.
        Entrada261Cumulativa entrada = new()
        {
            NumeroOrdem = OpSolicitada,
            NumeroOrdemItemFresco = OpOutra,
            PlannedProductionOp = 9016m,
            PriorProducedConfirmed = 0m,
            CurrentBoxProduction = 8m,
            ProductionUnit = "UN",
            Componentes = [],
            ComponentesCompletosComprovado = true,
            ConsumoLocalConfirmadoPorComponente = new Dictionary<string, decimal>()
        };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);

        Assert.Contains("ITEM_MANUFACTURING_ORDER_MISMATCH", r.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("COMPONENT_MANUFACTURING_ORDER", r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== §13 SOURCE AUDIT pos-fix =====================

    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        Assert.False(string.IsNullOrWhiteSpace(dir), "Raiz do projeto nao localizada.");
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static string SemComentarios(string fonte)
    {
        string semBloco = System.Text.RegularExpressions.Regex.Replace(
            fonte, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"//[^\r\n]*", string.Empty);
    }

    [Fact]
    public void SourceAudit_SelectDoItemIncluiManufacturingOrder()
    {
        string cliente = LerFonte("Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs");
        Assert.Contains(
            "$select=ManufacturingOrder,ManufacturingOrderItem,MfgOrderItemPlannedTotalQty,",
            cliente, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_MapearItemEMapearItemXmlPreservamManufacturingOrder()
    {
        string cliente = SemComentarios(
            LerFonte("Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs"));

        Assert.Contains("NumeroOrdem = LerTexto(i, \"ManufacturingOrder\")", cliente, StringComparison.Ordinal);
        Assert.Contains("NumeroOrdem = LerXmlTexto(p, \"ManufacturingOrder\")", cliente, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_ItemOrdemProducaoSapTemPropriedadeDaOp()
    {
        string modelo = SemComentarios(LerFonte("Modelo", "IntegracaoSap", "OrdemProducaoSap.cs"));
        Assert.Contains("public string NumeroOrdem { get; init; } = string.Empty;", modelo, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_RequestEchoRemovidoDaRotaAtiva()
    {
        // O eco nao pode mais alimentar a checagem cruzada, nem no Controller nem no seam.
        string controller = SemComentarios(LerFonte("Controle", "Processo", "ProdutoAcabadoController.cs"));
        string alocador = SemComentarios(LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        Assert.DoesNotContain("NumeroOrdemItemFresco = fresca.NumeroOrdemConsultada", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("NumeroOrdemItemFresco = fresca.NumeroOrdemConsultada", alocador, StringComparison.Ordinal);
        Assert.DoesNotContain("NumeroOrdemConsultada", controller, StringComparison.Ordinal);

        // E a unica origem permitida esta explicita no seam.
        Assert.Contains("NumeroOrdemItemFresco = fresca.Item?.NumeroOrdem", alocador, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_ComponenteSemOpBloqueia()
    {
        string alocador = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        // A condicao antiga (so bloqueava quando PREENCHIDO e diferente) nao pode mais existir.
        Assert.DoesNotContain(
            "!string.IsNullOrWhiteSpace(componente.NumeroOrdem)\r\n                && !string.Equals",
            alocador, StringComparison.Ordinal);
        Assert.Contains("opComponente.Length == 0", alocador, StringComparison.Ordinal);
        Assert.Contains("MotivoComponenteOrdemAusente", alocador, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_ControllerUsaOSeamUnicoDeMontagem()
    {
        string controller = SemComentarios(LerFonte("Controle", "Processo", "ProdutoAcabadoController.cs"));
        Assert.Contains(
            "ProdutoAcabado261AllocatorCumulativo.MontarEntrada(",
            controller, StringComparison.Ordinal);
    }

    /// <summary>Handler que responde por entidade: item ou componente, conforme a URL.</summary>
    private sealed class HandlerPorEntidade(string itemJson, string componenteJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            string url = request.RequestUri!.ToString();

            string corpo = url.Contains("A_ProductionOrderComponent_2", StringComparison.Ordinal)
                ? componenteJson
                : itemJson;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            });
        }
    }
}
