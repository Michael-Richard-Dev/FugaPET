using System.Net;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// FAST TRACK REV4: prova o WIRING do pipeline PA ao runtime (composiÃ§Ã£o fail-closed por gate/store), a remoÃ§Ã£o
/// das inferÃªncias do 261 (Unidade/datas/correlation), a URL CPI especÃ­fica do INT012 (nunca via HandlingUnit),
/// e a remoÃ§Ã£o produtiva do PALLET01. Sem rede/SAP real: fakes + HttpMessageHandler controlado.
/// </summary>
public sealed class ProdutoAcabadoPipelineWiringRev4Tests
{
    private static ProdutoAcabadoHuService HuServiceReal()
        => new(FabricaProdutoAcabadoRepositorio.Criar(), FabricaProdutoAcabadoHandlingUnitSapServico.Criar());

    [Fact]
    public void Compor_GateFalse_PipelineDesabilitado_SemOrquestrador()
    {
        ConfiguracaoSap cfg = new() { ProdutoAcabadoPipelineHabilitado = false };
        FabricaProdutoAcabadoIntegracaoSapOrquestrador.ResultadoComposicaoPipeline r =
            FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(cfg, HuServiceReal(), new ProdutoAcabadoPipelineStoreMemoria());
        Assert.False(r.Disponivel);
        Assert.Equal(FabricaProdutoAcabadoIntegracaoSapOrquestrador.MotivoDesabilitado, r.Motivo);
        Assert.Null(r.Orquestrador);
    }

    [Fact]
    public void Compor_GateTrue_StoreMemoria_DependenciaGaia_SemOrquestrador()
    {
        ConfiguracaoSap cfg = new() { ProdutoAcabadoPipelineHabilitado = true };
        FabricaProdutoAcabadoIntegracaoSapOrquestrador.ResultadoComposicaoPipeline r =
            FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(cfg, HuServiceReal(), new ProdutoAcabadoPipelineStoreMemoria());
        Assert.False(r.Disponivel);
        Assert.Equal(FabricaProdutoAcabadoIntegracaoSapOrquestrador.MotivoDependenciaGaia, r.Motivo);
        Assert.Null(r.Orquestrador);
    }

    [Fact]
    public void Compor_GateTrue_SemStore_DependenciaGaia()
    {
        ConfiguracaoSap cfg = new() { ProdutoAcabadoPipelineHabilitado = true };
        FabricaProdutoAcabadoIntegracaoSapOrquestrador.ResultadoComposicaoPipeline r =
            FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(cfg, HuServiceReal(), store: null);
        Assert.False(r.Disponivel);
        Assert.Equal(FabricaProdutoAcabadoIntegracaoSapOrquestrador.MotivoDependenciaGaia, r.Motivo);
    }

    // ============================ Â§17 â€” wiring estÃ¡tico Controller/Form ============================
    [Fact]
    public void Controller_ReferenciaOrquestradorERotaPipeline()
    {
        string src = LerProjeto("Controle", "Processo", "ProdutoAcabadoController.cs");
        Assert.Contains("FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(", src, StringComparison.Ordinal);
        Assert.Contains("EnviarCaixaPipelineAsync", src, StringComparison.Ordinal);
        Assert.Contains("PipelinePaGateHabilitado", src, StringComparison.Ordinal);
        Assert.Contains("FabricaProdutoAcabadoPaleteInt012Gateway.Criar", src, StringComparison.Ordinal);
        Assert.Contains("PaleteInt012EnvioAutorizado", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_RoteiaPipeline_AntesDoHuOnly_ESemAutoPost()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        Assert.Contains("if (_controller.PipelinePaGateHabilitado)", form, StringComparison.Ordinal);
        Assert.Contains("await SolicitarEnvioCaixaPipelineAsync();", form, StringComparison.Ordinal);

        // Pipeline habilitado â‡’ retorna ANTES do caminho HU-only (nunca HU direta com pipeline=true).
        int idxGate = form.IndexOf("if (_controller.PipelinePaGateHabilitado)", StringComparison.Ordinal);
        int idxHuOnly = form.IndexOf("_controller.EnviarCaixaHandlingUnitAsync(", StringComparison.Ordinal);
        Assert.True(idxGate >= 0 && idxHuOnly >= 0 && idxGate < idxHuOnly);

        // Sem POST automÃ¡tico ao carregar OP: o pipeline sÃ³ Ã© invocado pelo handler do botÃ£o (uma Ãºnica chamada).
        int chamadas = ContarOcorrencias(form, "_controller.EnviarCaixaPipelineAsync(");
        Assert.Equal(1, chamadas);

        // Estados 261/101/HU apresentados na UI quando o pipeline estÃ¡ ativo.
        Assert.Contains("Pipeline SAP — 261:", form, StringComparison.Ordinal);
        Assert.Contains("| 101:", form, StringComparison.Ordinal);
        Assert.Contains("| HU:", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Rev5_Form_NaoDefineCorrelationTecnica()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task SolicitarEnvioCaixaPipelineAsync()");
        Assert.DoesNotContain("CorrelationId =", metodo, StringComparison.Ordinal);
        Assert.Contains("Identidade técnica", metodo, StringComparison.Ordinal);
    }

    // ============================ Â§18 â€” 261 sem inferÃªncias ============================
    private sealed class FakeClient261 : IConsumoMaterialSap261Client
    {
        public int Chamadas { get; private set; }
        public List<string> Correlations { get; } = [];
        public Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(ConsumoMaterialSap261Request req, string corr, CancellationToken ct = default)
        {
            Chamadas++; Correlations.Add(corr);
            return Task.FromResult(ResultadoEnvioConsumoSap261.Ok("490", "2026", 201, null));
        }
    }

    private static ProdutoAcabadoMovimento261Command Cmd(
        string unidade = "KG", string storage = "PP01", DateTime? posting = null, DateTime? document = null, string correlation = "PA-1-1")
        => new()
        {
            NumeroOrdem = "1001951",
            CorrelationId = correlation,
            PostingDate = posting ?? new DateTime(2026, 8, 11),
            DocumentDate = document ?? new DateTime(2026, 8, 11),
            Itens = [new() { Material = "M", Plant = "3007", StorageLocation = storage, Quantidade = 5m, Unidade = unidade, Reservation = "10", ReservationItem = "1", Batch = "L-COMP" }]
        };

    [Theory]
    [InlineData("")]        // Unidade vazia (sem fallback KG)
    public async Task Adapter261_UnidadeVazia_ZeroCliente(string unidade)
    {
        FakeClient261 cli = new();
        ResultadoMovimentoSap r = await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(Cmd(unidade: unidade));
        Assert.Equal(EstadoMovimentoSap.Erro, r.Estado);
        Assert.Equal(0, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_StorageLocationVazia_ZeroCliente()
    {
        FakeClient261 cli = new();
        Assert.Equal(EstadoMovimentoSap.Erro, (await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(Cmd(storage: ""))).Estado);
        Assert.Equal(0, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_BatchVazio_SemIndicador_PreservaVazioEChamaCliente()
    {
        FakeClient261 cli = new();
        ProdutoAcabadoMovimento261Command cmd = Cmd() with { Itens = [Cmd().Itens[0] with { Batch = string.Empty }] };
        ResultadoMovimentoSap r = await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(cmd);
        Assert.Equal(EstadoMovimentoSap.Confirmado, r.Estado);
        Assert.Equal(1, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_PostingDateNull_ZeroCliente()
    {
        FakeClient261 cli = new();
        ProdutoAcabadoMovimento261Command cmd = Cmd() with { PostingDate = null };
        Assert.Equal(EstadoMovimentoSap.Erro, (await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(cmd)).Estado);
        Assert.Equal(0, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_DocumentDateNull_ZeroCliente()
    {
        FakeClient261 cli = new();
        ProdutoAcabadoMovimento261Command cmd = Cmd() with { DocumentDate = null };
        Assert.Equal(EstadoMovimentoSap.Erro, (await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(cmd)).Estado);
        Assert.Equal(0, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_DateTimeMinValue_ZeroCliente_NuncaSerializado()
    {
        FakeClient261 cli = new();
        ProdutoAcabadoMovimento261Command cmd = Cmd() with { PostingDate = DateTime.MinValue, DocumentDate = DateTime.MinValue };
        Assert.Equal(EstadoMovimentoSap.Erro, (await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(cmd)).Estado);
        Assert.Equal(0, cli.Chamadas); // MinValue jamais chega ao cliente/serializaÃ§Ã£o
    }

    [Fact]
    public async Task Adapter261_CorrelationVazia_ZeroCliente()
    {
        FakeClient261 cli = new();
        Assert.Equal(EstadoMovimentoSap.Erro, (await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(Cmd(correlation: ""))).Estado);
        Assert.Equal(0, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_DuasCaixas_CorrelationsDiferentes()
    {
        FakeClient261 cli = new();
        ProdutoAcabadoMovimento261Adapter adapter = new(cli, true);
        await adapter.EnviarAsync(Cmd(correlation: "PA-100-1"));
        await adapter.EnviarAsync(Cmd(correlation: "PA-200-1"));
        Assert.Equal(["PA-100-1", "PA-200-1"], cli.Correlations);
        Assert.Equal(2, cli.Correlations.Distinct().Count());
    }

    [Fact]
    public void Adapter261_MapearNaoInfereKg()
    {
        ConsumoMaterialSap261Request req = ProdutoAcabadoMovimento261Adapter.MapearRequisicao(Cmd(unidade: "TO"));
        Assert.Equal("TO", req.ToMaterialDocumentItem[0].EntryUnit); // preserva a unidade real, sem forÃ§ar "KG"
    }

    // ============================ Â§6 â€” command builder ============================
    [Fact]
    public void Builder_OrigemIncompleta_Bloqueia_SemComandos()
    {
        ProdutoAcabadoPipelineOrigem origem = new() { CodigoCaixa = 1, NumeroOrdem = "1001951", CorrelationId = "PA-1-1" };
        ResultadoComandosPipeline r = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        Assert.False(r.Sucesso);
        Assert.Null(r.Comando261);
        Assert.Null(r.Comando101);
        Assert.Contains("DEPENDENCIA", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Builder_OrigemCompleta_MontaComandos()
    {
        ProdutoAcabadoPipelineOrigem origem = OrigemCompleta();
        ResultadoComandosPipeline r = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        Assert.True(r.Sucesso);
        Assert.Equal("PA-1-1", r.Comando261!.CorrelationId);
        Assert.Equal("KG", r.Comando261.Itens[0].Unidade);
        Assert.Equal("L-COMP", r.Comando261.Itens[0].Batch);
        Assert.Equal(new DateTime(2026, 8, 11), r.Comando261.PostingDate);
        Assert.Equal(new DateTime(2026, 8, 11), r.Comando261.DocumentDate);
        Assert.Equal("4000108", r.Comando101!.Material);
        Assert.Equal(new DateTime(2026, 8, 11), r.Comando101.PostingDate);
        Assert.Equal(new DateTime(2026, 8, 11), r.Comando101.DocumentDate);
    }

    [Fact]
    public void Builder_PostingDateAusente_Bloqueia_SemComandos()
    {
        ProdutoAcabadoPipelineOrigem origem = OrigemCompleta() with { PostingDate = null };
        ResultadoComandosPipeline r = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        Assert.False(r.Sucesso);
        Assert.Null(r.Comando261);
        Assert.Null(r.Comando101);
        Assert.Contains("PostingDate", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_DocumentDateAusente_BloqueiaConformeContrato261_SemComandos()
    {
        ProdutoAcabadoPipelineOrigem origem = OrigemCompleta() with { DocumentDate = null };
        ResultadoComandosPipeline r = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        Assert.False(r.Sucesso);
        Assert.Null(r.Comando261);
        Assert.Null(r.Comando101);
        Assert.Contains("DocumentDate", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Batch261Ausente_SemIndicador_NaoBloqueia_NaoInventaLote()
    {
        ProdutoAcabadoPipelineOrigem origem = OrigemCompleta() with
        {
            Componentes = [OrigemCompleta().Componentes[0] with { Batch = string.Empty }]
        };

        ResultadoComandosPipeline r = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);

        Assert.True(r.Sucesso, r.Mensagem);
        Assert.NotNull(r.Comando261);
        Assert.NotNull(r.Comando101);
        Assert.Equal(string.Empty, r.Comando261!.Itens[0].Batch);
    }

    [Fact]
    public void Unidade101Ausente_Bloqueia()
    {
        ProdutoAcabadoPipelineOrigem origem = OrigemCompleta() with { EntryUnit = string.Empty };

        ResultadoComandosPipeline r = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);

        Assert.False(r.Sucesso);
        Assert.Null(r.Comando261);
        Assert.Null(r.Comando101);
        Assert.Contains("EntryUnit", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Form_RuntimeShape_MontaOrigemRealCompleta_CommandsSucesso()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValidaRuntimeShape()));
        ResultadoConsultaProdutoAcabado consulta = await controller.ConsultarOrdemProducaoAsync("1002024");
        Assert.True(consulta.Sucesso, consulta.Mensagem);
        Assert.NotNull(consulta.Ordem);

        ProdutoAcabadoNormaEmbalagem norma = new()
        {
            Material = "4000108",
            MaterialCaixa = "EMB-CAIXA",
            QuantidadeProdutosPorCaixa = 60,
            Unidade = "UN",
            NormaValida = true
        };
        ProdutoAcabadoCaixa caixa = controller.MontarCaixa(
            consulta.Ordem!,
            norma,
            numeroCaixa: 1,
            pesoBrutoKg: 10m,
            taraKg: 1m,
            origemPesagem: "MANUAL",
            terminal: "TERM-01",
            codigoUsuario: 42);
        caixa.CodigoProdutoAcabadoCaixa = 123;

        ProdutoAcabadoPipelineOrigem origem = ProcessoProdutoAcabadoForm.MontarOrigemPipelineRuntime(
            caixa,
            consulta.Ordem,
            new DateTime(2026, 8, 12, 21, 57, 0, DateTimeKind.Utc));
        ResultadoComandosPipeline resultado = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);

        Assert.Equal(new DateTime(2026, 8, 12), origem.PostingDate);
        Assert.Equal(new DateTime(2026, 8, 12), origem.DocumentDate);
        Assert.Equal(4, origem.Componentes.Count);
        Assert.All(origem.Componentes, componente =>
        {
            Assert.False(string.IsNullOrWhiteSpace(componente.Material));
            Assert.False(string.IsNullOrWhiteSpace(componente.Plant));
            Assert.False(string.IsNullOrWhiteSpace(componente.StorageLocation));
            Assert.True(componente.Quantidade > 0m);
            Assert.False(string.IsNullOrWhiteSpace(componente.Unidade));
            Assert.False(string.IsNullOrWhiteSpace(componente.Reservation));
            Assert.False(string.IsNullOrWhiteSpace(componente.ReservationItem));

        });
        Assert.Equal("1002024", origem.NumeroOrdem);
        Assert.Equal("4000108", origem.Material101);
        Assert.Equal("3007", origem.Plant101);
        Assert.Equal("PP02", origem.StorageLocation101);
        Assert.Equal("1", origem.ManufacturingOrderItem);
        Assert.Equal("60", origem.QuantityInEntryUnit);
        Assert.Equal("UN", origem.EntryUnit);
        Assert.Equal("67008561F", origem.Batch101);
        Assert.Contains(origem.Componentes, c => !string.IsNullOrWhiteSpace(c.Batch));
        Assert.Contains(origem.Componentes, c => string.IsNullOrWhiteSpace(c.Batch));
        Assert.True(resultado.Sucesso, resultado.Mensagem);
        Assert.NotNull(resultado.Comando261);
        Assert.NotNull(resultado.Comando101);
    }

    [Fact]
    public void Form_PreencheDatasPipelineComDataOperacionalDaCaixa()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task SolicitarEnvioCaixaPipelineAsync()");
        Assert.Contains("ProdutoAcabadoPipelineOrigem origem = MontarOrigemPipelineRuntime(caixa, _ordemAtual, DateTime.UtcNow);", metodo, StringComparison.Ordinal);
        string helper = ExtrairMetodo(form, "internal static ProdutoAcabadoPipelineOrigem MontarOrigemPipelineRuntime");
        Assert.Contains("PostingDate = dataOperacionalPipeline", helper, StringComparison.Ordinal);
        Assert.Contains("DocumentDate = dataOperacionalPipeline", helper, StringComparison.Ordinal);
        Assert.Contains("Componentes = MontarComponentesPipelineRuntime(origemOrdem)", helper, StringComparison.Ordinal);
        Assert.Contains("Material101 = caixa.Material", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Now", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Today", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandBuilder_NaoIntroduzFallbackTemporal()
    {
        string builder = LerProjeto("Servicos", "Operacao", "ProdutoAcabadoPipelineCommandBuilder.cs");
        Assert.DoesNotContain("DateTime.Now", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("DateTime.Today", builder, StringComparison.Ordinal);
    }

    private static OrdemProducaoSap OrdemSapValidaRuntimeShape()
        => new()
        {
            NumeroOrdem = "1002024",
            MaterialProduzido = "4000108",
            Centro = "3007",
            Deposito = "PP02",
            QuantidadePrevista = 60m,
            Unidade = "UN",
            Lote = "67008561F",
            Liberada = true,
            Itens =
            [
                new ItemOrdemProducaoSap
                {
                    ItemOrdem = "1",
                    Material = "4000108",
                    Centro = "3007",
                    Deposito = "PP02",
                    QuantidadePrevista = 60m,
                    QuantidadeEntregue = 0m,
                    Unidade = "UN",
                    Lote = "67008561F"
                }
            ],
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1002024",
                    Material = "2000044",
                    Centro = "3007",
                    Deposito = "PP01",
                    QuantidadeNecessaria = 200m,
                    UnidadeBase = "G",
                    Reserva = "13473",
                    ItemReserva = "1",
                    Lote = "0000000140",
                    TipoMovimento = "261"
                },
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1002024",
                    Material = "3000007",
                    Centro = "3007",
                    Deposito = "PP02",
                    QuantidadeNecessaria = 200m,
                    UnidadeBase = "ST",
                    Reserva = "13473",
                    ItemReserva = "2",
                    Lote = string.Empty,
                    TipoMovimento = "261"
                },
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1002024",
                    Material = "3000008",
                    Centro = "3007",
                    Deposito = "PP02",
                    QuantidadeNecessaria = 200m,
                    UnidadeBase = "ST",
                    Reserva = "13473",
                    ItemReserva = "3",
                    Lote = string.Empty,
                    TipoMovimento = "261"
                },
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1002024",
                    Material = "3000009",
                    Centro = "3007",
                    Deposito = "PP02",
                    QuantidadeNecessaria = 200m,
                    UnidadeBase = "ST",
                    Reserva = "13473",
                    ItemReserva = "4",
                    Lote = string.Empty,
                    TipoMovimento = "261"
                }
            ]
        };
    private static ProdutoAcabadoPipelineOrigem OrigemCompleta()
        => new()
        {
            CodigoCaixa = 1,
            NumeroOrdem = "1001951",
            CorrelationId = "PA-1-1",
            PostingDate = new DateTime(2026, 8, 11),
            DocumentDate = new DateTime(2026, 8, 11),
            Componentes = [new() { Material = "M", Plant = "3007", StorageLocation = "PP01", Quantidade = 5m, Unidade = "KG", Reservation = "10", ReservationItem = "1", Batch = "L-COMP" }],
            Material101 = "4000108", Plant101 = "3007", StorageLocation101 = "PP02",
            ManufacturingOrderItem = "0001", QuantityInEntryUnit = "60", EntryUnit = "KG", Batch101 = "L1"
        };
    // ============================ Â§19 â€” INT012 base CPI especÃ­fica ============================
    private sealed class HandlerCaptura : HttpMessageHandler
    {
        public int Posts { get; private set; }
        public Uri? UltimaUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Posts++; UltimaUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("""{ "UC_gerada": "300099999", "Status": "S", "_Mensagens": [] }""") });
        }
    }

    private static ProdutoAcabadoPaleteRequest ReqPalete() => new()
    {
        HandlingUnitExternalID = "PLT-1", GrossWeight = 20m, NetWeight = 18m, TareWeight = 2m, WeightUnit = "KG",
        Plant = "3007", StorageLocation = "PA01", PackagingMaterial = "PACK_TEST_001",
        HandlingUnitItems = [new() { HandlingUnit = "300010001" }]
    };

    private const string CpiBase = "https://cpi.exemplo.local/base";
    private const string EndpointCpi = "https://cpi.exemplo.local/http/pesagem/handling_unit/CreateHUInput/1111/SAP__self.processHandlingUnitPayload";


    [Fact]
    public void Int012_CadeiaReal_LeitorFactoryController_AutorizaComGateBaseAllowlistECredenciais()
    {
        string arquivo = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(arquivo, """
            {
              "sap": {
                "base_url": "https://sap.exemplo.local/odata",
                "hosts_permitidos": ["sap.exemplo.local"],
                "pa_pipeline_habilitado": true,
                "pallet_int012_base_url": "https://cpi.exemplo.local/base",
                "pallet_int012_hosts_permitidos": ["cpi.exemplo.local"]
              }
            }
            """);
            Dictionary<string, string> ambiente = new()
            {
                ["FUGAPET_SAP_PALLET_WRITE_ENABLED"] = "true",
                ["FUGAPET_SAP_PALLET_INT012_USERNAME"] = "usuario-cpi-fake",
                ["FUGAPET_SAP_PALLET_INT012_PASSWORD"] = "senha-cpi-fake",
                ["FUGAPET_SAP_USERNAME"] = "usuario-sap-fake",
                ["FUGAPET_SAP_PASSWORD"] = "senha-sap-fake"
            };

            ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(
                arquivo,
                nome => ambiente.GetValueOrDefault(nome));
            ProdutoAcabadoController controller = new(
                new ProductionOrderSapFakeServico(OrdemSapValida()),
                configuracaoSap: configuracao,
                pipelineStore: new ProdutoAcabadoPipelineStoreMemoria());

            Assert.True(configuracao.PalletWriteHabilitado);
            Assert.True(configuracao.ProdutoAcabadoPipelineHabilitado);
            Assert.False(string.IsNullOrWhiteSpace(configuracao.PalletInt012BaseUrl));
            Assert.NotEmpty(configuracao.PalletInt012HostsPermitidos);
            Assert.True(controller.PaleteInt012EnvioAutorizado);
        }
        finally
        {
            if (File.Exists(arquivo))
            {
                File.Delete(arquivo);
            }
        }
    }
    [Fact]
    public async Task Int012_HandlingUnitBaseUrlPreenchida_MasPalletInt012Vazia_Bloqueado()
    {
        // HandlingUnitBaseUrl NÃƒO participa da construÃ§Ã£o do INT012: PalletInt012BaseUrl vazia â‡’ fail-closed.
        ConfiguracaoSap cfg = new()
        {
            PalletWriteHabilitado = true,
            HandlingUnitBaseUrl = "https://s4.exemplo.local/sap/opu/odata4/handlingunit",
            PalletInt012BaseUrl = "",
            HostsPermitidos = ["s4.exemplo.local"],
            Usuario = "u", Senha = "p"
        };
        HandlerCaptura h = new();
        IProdutoAcabadoPaleteInt012Gateway gw = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(cfg, () => h);
        Assert.False(gw.EnvioAutorizado);
        Assert.Equal(EstadoPaleteInt012.NaoEnviado, (await gw.EnviarPaleteAsync(ReqPalete())).Estado);
        Assert.Equal(0, h.Posts);
    }

    [Fact]
    public async Task Rev5_Int012_PalletBaseComHostsGerais_MasAllowlistCpiVazia_ZeroHttp()
    {
        ConfiguracaoSap cfg = new()
        {
            PalletWriteHabilitado = true,
            PalletInt012BaseUrl = CpiBase,
            HostsPermitidos = ["cpi.exemplo.local"],
            Usuario = "u",
            Senha = "p",
        };
        HandlerCaptura h = new();
        IProdutoAcabadoPaleteInt012Gateway gw = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(cfg, () => h);
        Assert.False(gw.EnvioAutorizado);
        Assert.Equal(EstadoPaleteInt012.NaoEnviado, (await gw.EnviarPaleteAsync(ReqPalete())).Estado);
        Assert.Equal(0, h.Posts);
    }

    [Fact]
    public async Task Int012_PalletInt012Valida_MasGateFalse_ZeroHttp()
    {
        ConfiguracaoSap cfg = new()
        {
            PalletWriteHabilitado = false,
            PalletInt012BaseUrl = CpiBase,
            PalletInt012HostsPermitidos = ["cpi.exemplo.local"],
            Usuario = "u", Senha = "p"
        };
        HandlerCaptura h = new();
        IProdutoAcabadoPaleteInt012Gateway gw = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(cfg, () => h);
        Assert.False(gw.EnvioAutorizado);
        Assert.Equal(EstadoPaleteInt012.NaoEnviado, (await gw.EnviarPaleteAsync(ReqPalete())).Estado);
        Assert.Equal(0, h.Posts);
    }

    [Fact]
    public async Task Int012_PalletInt012Valida_GateTrue_EndpointExatoCpi()
    {
        ConfiguracaoSap cfg = new()
        {
            PalletWriteHabilitado = true,
            EscritaHabilitada = false,
            PalletInt012BaseUrl = CpiBase,
            PalletInt012HostsPermitidos = ["cpi.exemplo.local"],
            Usuario = "u", Senha = "p"
        };
        HandlerCaptura h = new();
        IProdutoAcabadoPaleteInt012Gateway gw = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(cfg, () => h);
        Assert.True(gw.EnvioAutorizado);
        ResultadoPaleteInt012 r = await gw.EnviarPaleteAsync(ReqPalete());
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal(1, h.Posts);
        Assert.Equal(EndpointCpi, h.UltimaUri!.ToString());
    }

    [Fact]
    public async Task Int012_SemAllowlistPropria_NaoUsaFallbackSapGenerico_Bloqueado()
    {
        // REV5-Â§9: base CPI vÃ¡lida + PalletInt012HostsPermitidos VAZIA, mas HostsPermitidos (SAP genÃ©rico) presente.
        // Sem fallback â‡’ gateway fail-closed (nenhum host prÃ³prio autorizado â‡’ zero HTTP).
        ConfiguracaoSap cfg = new()
        {
            PalletWriteHabilitado = true,
            PalletInt012BaseUrl = CpiBase,
            PalletInt012HostsPermitidos = [], // allowlist prÃ³pria vazia
            HostsPermitidos = ["cpi.exemplo.local"], // allowlist SAP genÃ©rica NÃƒO deve ser usada
            Usuario = "u", Senha = "p"
        };
        HandlerCaptura h = new();
        IProdutoAcabadoPaleteInt012Gateway gw = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(cfg, () => h);
        Assert.False(gw.EnvioAutorizado);
        Assert.Equal(EstadoPaleteInt012.NaoEnviado, (await gw.EnviarPaleteAsync(ReqPalete())).Estado);
        Assert.Equal(0, h.Posts);
    }

    [Fact]
    public void Int012_MontarEndpoint_NaoUsaHandlingUnitBaseUrl()
    {
        // Prova direta: a montagem do endpoint parte da base do CPI, jamais do S/4 Handling Unit.
        string endpoint = FabricaProdutoAcabadoPaleteInt012Gateway.MontarEndpoint(CpiBase);
        Assert.Equal(EndpointCpi, endpoint);
        Assert.DoesNotContain("s4.exemplo.local", endpoint, StringComparison.OrdinalIgnoreCase);
    }

    // ============================ Â§20 â€” palete ============================
    [Fact]
    public void Form_NaoAtribuiPallet01_EmNenhumLugarProdutivo()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        Assert.DoesNotContain(".Text = \"PALLET01\"", form, StringComparison.Ordinal);
        // Em particular, AtualizarCamposPaletizacaoPadrao nÃ£o escreve PALLET01.
        string metodo = ExtrairMetodo(form, "private void AtualizarCamposPaletizacaoPadrao()");
        Assert.DoesNotContain("materialEmbalagemPaleteTextBox.Text = \"PALLET01\"", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MontarPalete_CaixaEmOutroPalete_Bloqueia_SemMutar()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa1 = CaixaConfirmada(1);
        ProdutoAcabadoCaixa caixa2 = CaixaConfirmada(2);

        // Primeiro palete montado de verdade.
        ProdutoAcabadoPalete existente = controller.MontarPalete(OrdemValida(), [caixa1, caixa2], [], 1, 2, "PACK_TEST_001");
        controller.ConfirmarVinculoPaleteLocal(existente);
        string codigoOriginal = caixa1.CodigoPaleteLocal;

        // Nova tentativa com a MESMA caixa, agora informando o palete existente â‡’ bloqueia, sem mutar.
        ProdutoAcabadoCaixa caixa1Repetida = CaixaConfirmada(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(controller.MontarPalete(OrdemValida(), [caixa1Repetida], [existente], 1, 1, "PACK_TEST_001")));
        Assert.True(string.IsNullOrEmpty(caixa1Repetida.CodigoPaleteLocal)); // nÃ£o paletizada
        Assert.Equal(codigoOriginal, caixa1.CodigoPaleteLocal); // palete original intacto
    }

    [Fact]
    public void Rev5_PreviewPaleteInvalido_NaoMutaCaixas()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa = CaixaConfirmada(1);
        ProdutoAcabadoPalete palete = controller.MontarPalete(OrdemValida(), [caixa], [], 1, 1, string.Empty);

        ResultadoPreviewProdutoAcabadoPalete preview = controller.GerarPreviewPalete(palete);

        Assert.False(preview.Sucesso);
        Assert.True(string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal));
    }

    [Fact]
    public async Task Rev5_ControllerInt012_ComGaiaAusente_FailClosed_ZeroPost()
    {
        ConfiguracaoSap cfg = new()
        {
            PalletWriteHabilitado = true,
            PalletInt012BaseUrl = CpiBase,
            PalletInt012HostsPermitidos = ["cpi.exemplo.local"],
            Usuario = "u",
            Senha = "p",
        };
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()), configuracaoSap: cfg, pipelineStore: new ProdutoAcabadoPipelineStoreMemoria());
        ProdutoAcabadoPalete palete = controller.MontarPalete(OrdemValida(), [CaixaConfirmada(1)], [], 1, 1, "PALLET01");

        ResultadoPaleteInt012 resultado = await controller.EnviarPaleteInt012Async(palete);

        Assert.Equal(EstadoPaleteInt012.NaoEnviado, resultado.Estado);
        Assert.Contains("DEPENDENCIA_GAIA", resultado.MensagemSanitizada, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ControllerUsaPaleteInt012Orquestrador()
    {
        FakeStorePalete045 store = new();
        FakePaleteGateway gateway = new(EstadoPaleteInt012.Confirmado);
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapFakeServico(OrdemSapValida()),
            configuracaoSap: new ConfiguracaoSap(),
            pipelineStore: store,
            paleteInt012Gateway: gateway);
        ProdutoAcabadoPalete palete = controller.MontarPalete(OrdemValida(), [CaixaConfirmada(1)], [], 1, 1, "PALLET01");

        ResultadoPaleteInt012 resultado = await controller.EnviarPaleteInt012Async(palete);

        Assert.Equal(EstadoPaleteInt012.Confirmado, resultado.Estado);
        Assert.Equal(1, gateway.Posts);
        Assert.Equal("PALLET01", store.PrimeiroParametroCriacao);
        Assert.Contains(store.Log, item => item.StartsWith("claim:", StringComparison.Ordinal));
    }

    // ============================ helpers ============================
    private static ProdutoAcabadoOrdem OrdemValida()
        => new() { NumeroOrdem = "1000909", Centro = "3007", DepositoDestino = "PA01" };

    private static OrdemProducaoSap OrdemSapValida()
        => new()
        {
            NumeroOrdem = "1000909", MaterialProduzido = "3500024", Centro = "3007", Liberada = true,
            QuantidadePrevista = 10m, Unidade = "KG", Deposito = "PA01", Lote = "L001"
        };

    private static ProdutoAcabadoCaixa CaixaConfirmada(int numero)
        => new()
        {
            CodigoProdutoAcabadoCaixa = numero,
            NumeroCaixa = numero,
            NumeroOrdemProducao = "1000909",
            Material = "3500024",
            Lote = "L001",
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
            HandlingUnitExternalId = $"30001{numero:0000}",
            PesoBrutoKg = 10m, PesoLiquidoKg = 9m, TaraKg = 1m
        };

    private static int ContarOcorrencias(string texto, string alvo)
    {
        int n = 0, i = 0;
        while ((i = texto.IndexOf(alvo, i, StringComparison.Ordinal)) >= 0) { n++; i += alvo.Length; }
        return n;
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura nÃ£o encontrada: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') { profundidade++; }
            else if (fonte[i] == '}') { profundidade--; if (profundidade == 0) { return fonte[inicio..(i + 1)]; } }
        }
        return fonte[inicio..];
    }

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
    private sealed class FakePaleteGateway(EstadoPaleteInt012 estado) : IProdutoAcabadoPaleteInt012Gateway
    {
        public bool EnvioAutorizado => true;
        public int Posts { get; private set; }
        public Task<ResultadoPaleteInt012> EnviarPaleteAsync(ProdutoAcabadoPaleteRequest req, CancellationToken ct = default)
        {
            Posts++;
            return Task.FromResult(new ResultadoPaleteInt012
            {
                Estado = estado,
                UcGerada = "300099999",
                HttpStatus = 200,
                MensagemSanitizada = estado.ToString()
            });
        }
    }

    private sealed class FakeStorePalete045 : IProdutoAcabadoPipelineStore, IProdutoAcabadoPipeline045Operacoes
    {
        public bool SuportaPersistenciaDefinitiva => true;
        public List<string> Log { get; } = [];
        public string? PrimeiroParametroCriacao { get; private set; }
        public ProdutoAcabadoPipelineSnapshot? Obter(long codigoCaixa) => null;
        public void Salvar(ProdutoAcabadoPipelineSnapshot snapshot) { }
        public Task<long?> CriarPaleteAsync(string codigoPaleteLocal, string plant, string storageLocation, decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg, long usuario, string terminal, CancellationToken ct = default)
        { PrimeiroParametroCriacao = codigoPaleteLocal; Log.Add($"criar:{codigoPaleteLocal}"); return Task.FromResult<long?>(777); }
        public Task<bool> VincularCaixaPaleteAsync(long c, long cx, int seq, long u, string t, CancellationToken ct = default)
        { Log.Add($"vincular:{c}:{cx}:{seq}"); return Task.FromResult(true); }
        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long c, string r, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"claim:{c}"); return Task.FromResult(new ResultadoClaim045(true, Guid.NewGuid(), 1)); }
        public Task<bool> RegistrarSucessoPaleteAsync(long c, int h, string uc, string r, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"sucesso:{c}"); return Task.FromResult(true); }
        public Task<bool> RegistrarErroPaleteAsync(long c, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"erro:{c}"); return Task.FromResult(true); }
        public Task<bool> RegistrarTimeoutPaleteAsync(long c, string erro, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"timeout:{c}"); return Task.FromResult(true); }
        public Task<bool> IniciarFluxoAsync(long c, long u, string t, string? o = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> PrepararEtapaAsync(long c, string et, string p, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long c, string et, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoEtapaAsync(long c, string et, int h, string md, string y, string r, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroEtapaAsync(long c, string et, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutEtapaAsync(long c, string et, string erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long c, string et, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long c, string et, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long c, string et, string res, int? h, string? md, string? y, string r, string? erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long c, string et, string tipo, string m, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
    }
    private sealed class ProductionOrderSapFakeServico(OrdemProducaoSap ordem) : IProductionOrderSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.Encontrada(ordem));
    }
}
















