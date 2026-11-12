using System.Net;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 095F (Ares 095D / Dédalo 095E-R1) — resolver SAP via API_PRODUCTION_ROUTING;v=3 + correlação de
/// ocorrência (CorrelacionadorOcorrenciaRoteiroSap) entre o resolver e o Marcador (Op-only, PP_FORM).
/// Cobre C01–C14 e os pontos T01–T22 delegados ao correlator/cliente (não ao Marcador nem ao HTTP).
/// </summary>
public sealed class ControleApontamentosRoutingV3095FTests
{
    // ============================ CORRELACIONADOR (C01–C08, C12) ============================

    [Fact] // C01
    public void C01_ExactMatchNormalized()
    {
        RoteiroProducaoSap roteiro = Roteiro(("40", "3007", "3007015", "PP_FORM", true));
        ResultadoCorrelacaoOcorrencia r = CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", "3007", "3007015"));
        Assert.Equal(EstadoCorrelacaoOcorrencia.Correlacionada, r.Estado);
        Assert.Equal(1, r.Encontrados);
        Assert.Single(r.RoteiroReduzido!.Operacoes);
    }

    [Fact] // C02
    public void C02_WorkCenterDifferent_FailClosed()
    {
        RoteiroProducaoSap roteiro = Roteiro(("40", "3007", "9999999", "PP_FORM", true));
        ResultadoCorrelacaoOcorrencia r = CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", "3007", "3007015"));
        Assert.Equal(EstadoCorrelacaoOcorrencia.NaoEncontrada, r.Estado);
        Assert.Null(r.RoteiroReduzido);
    }

    [Fact] // C03
    public void C03_PlantDifferent_FailClosed()
    {
        RoteiroProducaoSap roteiro = Roteiro(("40", "9999", "3007015", "PP_FORM", true));
        ResultadoCorrelacaoOcorrencia r = CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", "3007", "3007015"));
        Assert.Equal(EstadoCorrelacaoOcorrencia.NaoEncontrada, r.Estado);
    }

    [Fact] // C04
    public void C04_DuplicateOperationDifferentWorkCenter_NaoAmbiguo()
    {
        RoteiroProducaoSap roteiro = Roteiro(
            ("40", "3007", "3007015", "PP_FORM", true),
            ("40", "3007", "9999999", "PP_FORM", true));
        ResultadoCorrelacaoOcorrencia r = CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", "3007", "3007015"));
        Assert.Equal(EstadoCorrelacaoOcorrencia.Correlacionada, r.Estado);
        Assert.Equal("3007015", Assert.Single(r.RoteiroReduzido!.Operacoes).WorkCenter);
    }

    [Fact] // C05
    public void C05_DuplicateExact_FailClosed()
    {
        RoteiroProducaoSap roteiro = Roteiro(
            ("40", "3007", "3007015", "PP_FORM", true),
            ("40", "3007", "3007015", "PP_FORM", true));
        ResultadoCorrelacaoOcorrencia r = CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", "3007", "3007015"));
        Assert.Equal(EstadoCorrelacaoOcorrencia.Ambigua, r.Estado);
        Assert.Equal(2, r.Encontrados);
    }

    [Fact] // C06 — correlator + marker
    public void C06_PpForm_Manual()
    {
        RoteiroProducaoSap reduzido = Correlacionar(("40", "3007", "3007015", " PP_FORM ", true));
        Assert.Equal(ClassificacaoOperacaoManual.Manual,
            MarcadorOperacaoManualSap.ClassificarOperacao(reduzido, "0040"));
    }

    [Fact] // C07
    public void C07_StandardTextPresentEmpty_Automatica()
    {
        RoteiroProducaoSap reduzido = Correlacionar(("40", "3007", "3007015", "", true));
        Assert.Equal(ClassificacaoOperacaoManual.Automatica,
            MarcadorOperacaoManualSap.ClassificarOperacao(reduzido, "0040"));
    }

    [Fact] // C08
    public void C08_StandardTextMissing_ContratoNaoResolvido()
    {
        RoteiroProducaoSap reduzido = Correlacionar(("40", "3007", "3007015", "", false));
        Assert.Equal(ClassificacaoOperacaoManual.ContratoNaoResolvido,
            MarcadorOperacaoManualSap.ClassificarOperacao(reduzido, "0040"));
    }

    [Fact] // C12
    public void C12_WorkCenterNaoTransformado()
    {
        RoteiroProducaoSap reduzido = Correlacionar(("40", "3007", "3007015", "PP_FORM", true));
        Assert.Equal("3007015", Assert.Single(reduzido.Operacoes).WorkCenter);
    }

    // ============================ MARCADOR / BOUNDARY (C09, C10) ============================

    [Fact] // C09 — assinatura pública preservada (Op-only)
    public void C09_MarkerSignaturePreserved()
    {
        var metodo = typeof(MarcadorOperacaoManualSap).GetMethod(nameof(MarcadorOperacaoManualSap.ClassificarOperacao));
        Assert.NotNull(metodo);
        var parametros = metodo!.GetParameters();
        Assert.Equal(2, parametros.Length);
        Assert.Equal(typeof(RoteiroProducaoSap), parametros[0].ParameterType);
        Assert.Equal(typeof(string), parametros[1].ParameterType);
    }

    [Fact] // C10 — boundary do serviço SAP preservado
    public void C10_ServiceBoundaryPreserved()
    {
        var metodo = typeof(IProductionRoutingSapServico).GetMethod("ResolverRoteiroDaOrdemAsync");
        Assert.NotNull(metodo);
        Assert.Equal(typeof(Task<RoteiroProducaoSap?>), metodo!.ReturnType);
        Assert.Equal(typeof(OrdemProducaoSap), metodo.GetParameters()[0].ParameterType);
    }

    // ============================ DTO / MAPPER V3 (C11) ============================

    [Fact] // C11 — Plant + WorkCenter materializados; StandardText presente/ausente distinguíveis
    public void C11_V3DtoMaterializaPlantWorkCenter()
    {
        OperacaoRoteiroSap presente = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"0040","Plant":"3007","WorkCenter":"3007015","OperationStandardTextCode":"PP_FORM"}"""));
        Assert.Equal("3007", presente.Plant);
        Assert.Equal("3007015", presente.WorkCenter);
        Assert.True(presente.TextoPadraoObtido);
        Assert.Equal("PP_FORM", presente.CodigoTextoPadrao);

        OperacaoRoteiroSap ausente = ProductionRoutingSapApiClient.MapearOperacaoRoteiroJson(
            Json("""{"Operation":"0040","Plant":"3007","WorkCenter":"3007015"}"""));
        Assert.False(ausente.TextoPadraoObtido); // campo ausente != vazio explícito
    }

    // ============================ CLIENTE V3 — HTTP (T01–T06, T16, T17, T22/C14) ============================

    [Fact] // T01 + T22/C14 — usa API_PRODUCTION_ROUTING;v=3 + entidades V3; somente GET
    public async Task T01_UsaRotingV3_EntidadesV3_GetOnly()
    {
        HandlerV3 handler = new(
            discovery: Colecao("""{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}"""),
            operacoes: Colecao("""{"Operation":"0040","Plant":"3007","WorkCenter":"3007015","OperationStandardTextCode":"PP_FORM"}"""));
        RoteiroProducaoSap? roteiro = await Resolver(handler);

        Assert.NotNull(roteiro);
        Assert.All(handler.Requisicoes, r => Assert.Equal(HttpMethod.Get, r.Method));
        Assert.Contains(handler.Urls, u => u.Contains("API_PRODUCTION_ROUTING;v=3", StringComparison.Ordinal));
        Assert.Contains(handler.Urls, u => u.Contains("ProductionRoutingMatlAssgmt", StringComparison.Ordinal));
        Assert.Contains(handler.Urls, u => u.Contains("ProductionRoutingOperation", StringComparison.Ordinal));
    }

    [Fact] // T02 / C13 — zero request a API_PRODUCTION_VERSION / A_ProductionVersion
    public async Task T02_ZeroProductionVersionRequest()
    {
        HandlerV3 handler = new(
            discovery: Colecao("""{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}"""),
            operacoes: Colecao("""{"Operation":"0040","Plant":"3007","WorkCenter":"3007015","OperationStandardTextCode":"PP_FORM"}"""));
        await Resolver(handler);

        Assert.DoesNotContain(handler.Urls, u => u.Contains("A_ProductionVersion", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(handler.Urls, u => u.Contains("API_PRODUCTION_VERSION", StringComparison.OrdinalIgnoreCase));
    }

    [Fact] // T03
    public async Task T03_DiscoveryZero_FailClosed()
    {
        HandlerV3 handler = new(discovery: Colecao(), operacoes: Colecao());
        Assert.Null(await Resolver(handler));
    }

    [Fact] // T04
    public async Task T04_DiscoveryOne_Resolves()
    {
        HandlerV3 handler = new(
            discovery: Colecao("""{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}"""),
            operacoes: Colecao("""{"Operation":"0040","Plant":"3007","WorkCenter":"3007015","OperationStandardTextCode":"PP_FORM"}"""));
        RoteiroProducaoSap? roteiro = await Resolver(handler);
        Assert.Equal("50000000", roteiro!.BillOfOperationsGroup);
        Assert.Equal("1", roteiro.BillOfOperationsVariant);
    }

    [Fact] // T05
    public async Task T05_DiscoveryMany_FailClosed()
    {
        HandlerV3 handler = new(
            discovery: Colecao(
                """{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}""",
                """{"ProductionRoutingGroup":"50000001","ProductionRouting":"2"}"""),
            operacoes: Colecao());
        Assert.Null(await Resolver(handler));
    }

    [Fact] // T06
    public async Task T06_DiscoveryDuplicateIdentical_Collapses()
    {
        HandlerV3 handler = new(
            discovery: Colecao(
                """{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}""",
                """{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}"""),
            operacoes: Colecao("""{"Operation":"0040","Plant":"3007","WorkCenter":"3007015","OperationStandardTextCode":"PP_FORM"}"""));
        RoteiroProducaoSap? roteiro = await Resolver(handler);
        Assert.NotNull(roteiro);
        Assert.Equal("50000000", roteiro!.BillOfOperationsGroup);
    }

    [Fact] // T16
    public async Task T16_ZeroOperations_FailClosed()
    {
        HandlerV3 handler = new(
            discovery: Colecao("""{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}"""),
            operacoes: Colecao());
        Assert.Null(await Resolver(handler));
    }

    [Fact] // T17
    public async Task T17_HttpNaoSucesso_FailClosed()
    {
        HandlerV3 handler = new(discovery: Colecao(), operacoes: Colecao(), discoveryStatus: HttpStatusCode.Forbidden);
        Assert.Null(await Resolver(handler));
    }

    // ============================ PROVA OP1000164 (§19) ============================

    [Fact]
    public async Task Prova_OP1000164_0040_Manual_WC3007015()
    {
        HandlerV3 handler = new(
            discovery: Colecao("""{"ProductionRoutingGroup":"50000000","ProductionRouting":"1"}"""),
            operacoes: Colecao(
                """{"Operation":"0040","Plant":"3007","WorkCenter":"3007015","OperationStandardTextCode":"PP_FORM"}""",
                """{"Operation":"0050","Plant":"3007","WorkCenter":"3007016","OperationStandardTextCode":"PP_FORM"}"""));
        RoteiroProducaoSap? roteiro = await Resolver(handler, product: "2000205", plant: "3007");

        ResultadoCorrelacaoOcorrencia correlacao =
            CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", "3007", "3007015"));
        Assert.Equal(EstadoCorrelacaoOcorrencia.Correlacionada, correlacao.Estado);
        Assert.Equal("3007015", Assert.Single(correlacao.RoteiroReduzido!.Operacoes).WorkCenter);
        Assert.Equal(ClassificacaoOperacaoManual.Manual,
            MarcadorOperacaoManualSap.ClassificarOperacao(correlacao.RoteiroReduzido, "0040"));
    }

    // ============================ OBSERVABILIDADE — CONFIG_NAO_DISPONIVEL (095F-R1) ============================

    [Fact]
    public async Task R1_ConfigNaoDisponivel_FailClosed_ComCodigoSanitizado()
    {
        System.Text.StringBuilder capturado = new();
        var listener = new System.Diagnostics.TextWriterTraceListener(new System.IO.StringWriter(capturado));
        System.Diagnostics.Trace.Listeners.Add(listener);
        RoteiroProducaoSap? roteiro;
        try
        {
            // ConfiguracaoSap vazia ⇒ ProductionRoutingConfigurado = false ⇒ CONFIG_NAO_DISPONIVEL, fail-closed.
            ProductionRoutingSapServico wrapper = new(new ConfiguracaoSap());
            roteiro = await wrapper.ResolverRoteiroDaOrdemAsync(
                new OrdemProducaoSap { MaterialProduzido = "2000205", Centro = "3007" }, CancellationToken.None);
        }
        finally
        {
            System.Diagnostics.Trace.Flush();
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }

        Assert.Null(roteiro);
        string log = capturado.ToString();
        Assert.Contains("CONFIG_NAO_DISPONIVEL", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", log, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Basic ", log, StringComparison.Ordinal);
    }

    // ============================ helpers ============================

    private static RoteiroProducaoSap Correlacionar((string op, string plant, string wc, string std, bool obtido) linha)
    {
        RoteiroProducaoSap roteiro = Roteiro(linha);
        ResultadoCorrelacaoOcorrencia r = CorrelacionadorOcorrenciaRoteiroSap.Correlacionar(roteiro, Ocorrencia("0040", linha.plant, linha.wc));
        Assert.Equal(EstadoCorrelacaoOcorrencia.Correlacionada, r.Estado);
        return r.RoteiroReduzido!;
    }

    private static RoteiroProducaoSap Roteiro(params (string op, string plant, string wc, string std, bool obtido)[] linhas)
        => new()
        {
            BillOfOperationsGroup = "50000000",
            BillOfOperationsVariant = "1",
            Operacoes = linhas.Select(l => new OperacaoRoteiroSap
            {
                Operacao = l.op,
                Plant = l.plant,
                WorkCenter = l.wc,
                CodigoTextoPadrao = l.std,
                TextoPadraoObtido = l.obtido
            }).ToList()
        };

    private static OperacaoOrdemProducaoSap Ocorrencia(string operacao, string plant, string workCenter)
        => new() { Operacao = operacao, Centro = plant, CentroTrabalho = workCenter, Sequencia = "000000" };

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private static string Colecao(params string[] objetos)
        => "{\"d\":{\"results\":[" + string.Join(",", objetos) + "]}}";

    private static ConfiguracaoSap Config() => new()
    {
        MaterialDocumentBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
        Usuario = "u",
        Senha = "p",
        HostsPermitidos = ["sap.example.com"]
    };

    private static Task<RoteiroProducaoSap?> Resolver(HandlerV3 handler, string product = "2000205", string plant = "3007")
    {
        ConfiguracaoSap config = Config();
        HttpClient http = new(handler);
        ProductionRoutingSapApiClient cliente = new(config, http);
        OrdemProducaoSap ordem = new() { MaterialProduzido = product, Centro = plant };
        return cliente.ResolverRoteiroDaOrdemAsync(ordem, CancellationToken.None);
    }

    private sealed class HandlerV3 : HttpMessageHandler
    {
        private readonly string _discovery;
        private readonly string _operacoes;
        private readonly HttpStatusCode _discoveryStatus;

        public HandlerV3(string discovery, string operacoes, HttpStatusCode discoveryStatus = HttpStatusCode.OK)
        {
            _discovery = discovery;
            _operacoes = operacoes;
            _discoveryStatus = discoveryStatus;
        }

        public List<HttpRequestMessage> Requisicoes { get; } = [];
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicoes.Add(request);
            string url = request.RequestUri!.ToString();
            Urls.Add(url);

            if (url.Contains("ProductionRoutingMatlAssgmt", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(_discoveryStatus) { Content = new StringContent(_discovery) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_operacoes) });
        }
    }
}
