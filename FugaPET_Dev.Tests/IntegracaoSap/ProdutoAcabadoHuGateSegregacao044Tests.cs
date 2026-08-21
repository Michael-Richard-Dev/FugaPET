using System.Net;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// §8-§11/§27/§28: prova a autorização ESPECÍFICA e ISOLADA do POST de HU (modo HML repetitivo). A flag
/// FUGAPET_SAP_HU_WRITE_ENABLED é independente de FUGAPET_SAP_WRITE_ENABLED, tem default false, e mesmo
/// habilitada o gateway transmite EXCLUSIVAMENTE POST /HandlingUnit (nunca outro endpoint, PATCH ou DELETE).
/// Sem SAP real — apenas <see cref="HttpMessageHandler"/> fake que conta transmissões.
/// </summary>
public sealed class ProdutoAcabadoHuGateSegregacao044Tests
{
    private const string BaseUrl = "https://sap.exemplo.local/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001";
    private const string EndpointHu = "/HandlingUnit?sap-client=110";
    private const string PathEsperado = "/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001/HandlingUnit";
    private static readonly IReadOnlyList<string> HostsPermitidos = ["sap.exemplo.local"];

    // §11/§27: o guard aceita SOMENTE POST /HandlingUnit.
    [Theory]
    [InlineData("POST", "/HandlingUnit?sap-client=110", true)]
    [InlineData("POST", "/HandlingUnit", true)]
    [InlineData("POST", "/OutroEndpoint?sap-client=110", false)]
    [InlineData("POST", "/HandlingUnitItem", false)]
    [InlineData("PATCH", "/HandlingUnit", false)]
    [InlineData("DELETE", "/HandlingUnit", false)]
    [InlineData("GET", "/HandlingUnit", false)]
    public void Guard_PermiteSomentePostHandlingUnit(string metodo, string endpoint, bool esperado)
        => Assert.Equal(esperado, ProdutoAcabadoHandlingUnitSapGateway.EscritaHuPermitida(new HttpMethod(metodo), endpoint));

    // §27: WRITE_ENABLED=false + HU=true ⇒ POST /HandlingUnit transmite; outro endpoint NÃO transmite.
    [Fact]
    public async Task GateTrue_TransmiteSomenteHandlingUnit()
    {
        HandlerContador handler = new(HttpStatusCode.Created, """{ "d": { "HandlingUnitExternalID": "HU1", "Warehouse": "" } }""");
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);

        ResultadoPostHandlingUnit ok = await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);
        Assert.Equal(CenarioPostHandlingUnit.Confirmado, ok.Cenario);
        Assert.Equal(1, handler.Posts);

        // Endpoint não-HU: bloqueado ANTES da transmissão (contador não avança).
        ResultadoPostHandlingUnit bloqueado = await gateway.CriarHandlingUnitCaixaAsync(Request(), "/OutroEndpoint?sap-client=110");
        Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, bloqueado.Cenario);
        Assert.Equal(1, handler.Posts); // nenhuma transmissão nova
    }

    // §8/§14: a URI ABSOLUTA resolve dentro da base OData (.../0001/HandlingUnit) — nunca ao host raiz.
    [Fact]
    public async Task Uri_Post_E_Csrf_ResolvemDentroDaBaseOData()
    {
        HandlerContador handler = new(HttpStatusCode.Created, """{ "d": { "HandlingUnitExternalID": "HU1", "Warehouse": "" } }""");
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);

        await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);

        Assert.NotNull(handler.UriPost);
        Assert.NotNull(handler.UriCsrf);
        // PATH exato (nunca "https://sap.exemplo.local/HandlingUnit").
        Assert.Equal(PathEsperado, handler.UriPost!.AbsolutePath);
        Assert.Equal(PathEsperado, handler.UriCsrf!.AbsolutePath);
        Assert.Equal("sap.exemplo.local", handler.UriPost.Host);
        string uriPost = Uri.UnescapeDataString(handler.UriPost.AbsoluteUri);
        string uriCsrf = Uri.UnescapeDataString(handler.UriCsrf.AbsoluteUri);
        Assert.True(uriPost.Contains("sap-client=110", StringComparison.Ordinal), $"POST={uriPost}");
        // REV4-B1: GET CSRF alinhado ao fluxo real HML-2 comprovado ($top=1), não mais $top=0.
        Assert.True(uriCsrf.Contains("$top=1", StringComparison.Ordinal), $"CSRF={uriCsrf}");
        Assert.False(uriCsrf.Contains("$top=0", StringComparison.Ordinal), $"CSRF={uriCsrf}");
        Assert.True(uriCsrf.Contains("sap-client=110", StringComparison.Ordinal), $"CSRF={uriCsrf}");
        Assert.NotEqual("/HandlingUnit", handler.UriPost.AbsolutePath); // não perdeu o path OData
    }

    // REV4-B1/§3: URI EXATA do GET CSRF — PATH .../0001/HandlingUnit e QUERY contém $top=1 + sap-client=110.
    [Fact]
    public async Task Csrf_Uri_Exata_Top1_SapClient110()
    {
        HandlerContador handler = new(HttpStatusCode.Created, """{ "d": { "HandlingUnitExternalID": "HU1", "Warehouse": "" } }""");
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);

        await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);

        Assert.NotNull(handler.UriCsrf);
        Assert.Equal(PathEsperado, handler.UriCsrf!.AbsolutePath);
        Assert.Equal("sap.exemplo.local", handler.UriCsrf.Host);
        string csrf = Uri.UnescapeDataString(handler.UriCsrf.AbsoluteUri);
        Assert.Contains("$top=1", csrf, StringComparison.Ordinal);
        Assert.Contains("sap-client=110", csrf, StringComparison.Ordinal);
        Assert.DoesNotContain("$top=0", csrf, StringComparison.Ordinal);
    }

    // §9/§15: allowlist/HTTPS/userinfo — gateway fail-closed quando a base é inválida.
    [Theory]
    [InlineData("https://evil.example/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001")] // host fora da allowlist
    [InlineData("http://sap.exemplo.local/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001")] // não HTTPS
    [InlineData("https://user:pass@sap.exemplo.local/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001")] // userinfo
    public async Task Allowlist_BaseInvalida_NaoTransmite(string baseInvalida)
    {
        HandlerContador handler = new(HttpStatusCode.Created, "{}");
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            baseInvalida, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);
        Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, r.Cenario);
        Assert.Equal(0, handler.Posts);
    }

    // §11/§18: timeout / conexão interrompida APÓS a tentativa de POST ⇒ Timeout (não reprocessável), 1 POST.
    [Theory]
    [InlineData(ModoFalha.Timeout)]
    [InlineData(ModoFalha.ConexaoInterrompida)]
    public async Task FalhaDeRede_PosPost_Timeout_SemReprocessar(ModoFalha modo)
    {
        HandlerContador handler = new(HttpStatusCode.Created, "{}", modo);
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);
        Assert.Equal(CenarioPostHandlingUnit.Timeout, r.Cenario);
        Assert.False(r.PodeReprocessar);
        Assert.Equal(1, handler.Posts); // uma tentativa, sem retry
    }

    // §12/§18: 500/503/401/403 — sem retry automático; erro definitivo NÃO reprocessável (500/503).
    [Theory]
    [InlineData(500, CenarioPostHandlingUnit.ErroDefinitivo, false)]
    [InlineData(503, CenarioPostHandlingUnit.ErroDefinitivo, false)]
    [InlineData(401, CenarioPostHandlingUnit.NaoAutorizado, false)]
    [InlineData(403, CenarioPostHandlingUnit.NaoAutorizado, false)]
    public async Task StatusHttp_SemRetry_NaoReprocessavel(int status, CenarioPostHandlingUnit esperado, bool podeReprocessar)
    {
        HandlerContador handler = new((HttpStatusCode)status, "{}");
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);
        Assert.Equal(esperado, r.Cenario);
        Assert.Equal(podeReprocessar, r.PodeReprocessar);
        Assert.Equal(1, handler.Posts);
    }

    // §6/§7 (REV3-B3): CSRF FAIL-CLOSED — só transmite o POST se o GET CSRF retornou 200 + token não vazio.
    // Qualquer falha do GET CSRF ⇒ POST_COUNT=0, sem 2º POST, resultado definitivo NÃO reprocessável.
    public enum FalhaCsrf { Nenhuma, RedeException, Timeout }

    [Theory]
    // status CSRF, token CSRF, falha de transporte no GET, POST esperado
    [InlineData(200, "token-ok", FalhaCsrf.Nenhuma, 1)]   // 200 + token ⇒ POST=1
    [InlineData(200, "", FalhaCsrf.Nenhuma, 0)]           // 200 sem token ⇒ POST=0
    [InlineData(401, "token-ok", FalhaCsrf.Nenhuma, 0)]   // 401 ⇒ POST=0
    [InlineData(403, "token-ok", FalhaCsrf.Nenhuma, 0)]   // 403 ⇒ POST=0
    [InlineData(500, "token-ok", FalhaCsrf.Nenhuma, 0)]   // 500 ⇒ POST=0
    [InlineData(0, "", FalhaCsrf.RedeException, 0)]        // HttpRequestException ⇒ POST=0
    [InlineData(0, "", FalhaCsrf.Timeout, 0)]              // timeout ⇒ POST=0
    public async Task Csrf_FailClosed_PostSomenteCom200ETokenNaoVazio(int statusCsrf, string tokenCsrf, FalhaCsrf falha, int postEsperado)
    {
        HandlerCsrf handler = new(statusCsrf, tokenCsrf, falha);
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: true, fabricaHandler: () => handler);

        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);

        Assert.Equal(1, handler.GetsCsrf);          // GET CSRF sempre é tentado exatamente uma vez
        Assert.Equal(postEsperado, handler.Posts);  // POST só quando CSRF 200 + token
        if (postEsperado == 0)
        {
            // Falha determinística ANTES do POST: erro definitivo, jamais reprocessável (sem 2º POST).
            Assert.Equal(CenarioPostHandlingUnit.ErroDefinitivo, r.Cenario);
            Assert.False(r.PodeReprocessar);
            // Nenhum VALOR de segredo vaza na mensagem (o nome do header pode aparecer em diagnóstico neutro).
            foreach (string proibido in new[] { "Authorization", "Basic ", "Cookie:", "token-ok" })
            {
                Assert.DoesNotContain(proibido, r.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            Assert.Equal(CenarioPostHandlingUnit.Confirmado, r.Cenario);
        }
    }

    // §13: HU=true + escrita genérica true (WRITE_ENABLED) ⇒ configuração incompatível ⇒ fail-closed.
    [Fact]
    public void GenericWriteTrue_ComHuTrue_FabricaFailClosed()
    {
        ConfiguracaoSap incompativel = new()
        {
            Usuario = "u", Senha = "p", HostsPermitidos = ["sap.exemplo.local"],
            HuWriteHabilitado = true, HandlingUnitBaseUrl = BaseUrl, EscritaHabilitada = true
        };
        Assert.False(FabricaProdutoAcabadoHandlingUnitSapServico.Criar(true, false, false, () => incompativel).EnvioAutorizado);
    }

    // §26.1: gate false ⇒ nenhuma transmissão.
    [Fact]
    public async Task GateFalse_NaoTransmite()
    {
        HandlerContador handler = new(HttpStatusCode.Created, "{}");
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUrl, HostsPermitidos, "user", "pass", envioAutorizado: false, fabricaHandler: () => handler);
        ResultadoPostHandlingUnit r = await gateway.CriarHandlingUnitCaixaAsync(Request(), EndpointHu);
        Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, r.Cenario);
        Assert.Equal(0, handler.Posts);
    }

    // §9/§28: HU write default false; habilitá-la NÃO habilita a escrita SAP genérica.
    [Fact]
    public void Config_HuWrite_DefaultFalse_IndependenteDeWriteEnabled()
    {
        ConfiguracaoSap padrao = LerConfig(env: _ => null);
        Assert.False(padrao.HuWriteHabilitado);
        Assert.False(padrao.EscritaHabilitada);

        // HU=true e WRITE_ENABLED ausente ⇒ HU habilitada, escrita genérica continua false.
        ConfiguracaoSap huLigado = LerConfig(env: nome => nome == "FUGAPET_SAP_HU_WRITE_ENABLED" ? "true" : null);
        Assert.True(huLigado.HuWriteHabilitado);
        Assert.False(huLigado.EscritaHabilitada);
    }

    // §8/§9: a Fábrica é fail-closed sem a flag/URL de HU; cria o gateway real só com HandlingUnitConfigurado.
    [Fact]
    public void Fabrica_FailClosed_SemFlagOuUrl_RealComTudoConfigurado()
    {
        ConfiguracaoSap semFlag = ConfigHu(huWrite: false, comUrl: true);
        Assert.False(FabricaProdutoAcabadoHandlingUnitSapServico.Criar(true, false, false, () => semFlag).EnvioAutorizado);

        ConfiguracaoSap semUrl = ConfigHu(huWrite: true, comUrl: false);
        Assert.False(FabricaProdutoAcabadoHandlingUnitSapServico.Criar(true, false, false, () => semUrl).EnvioAutorizado);

        ConfiguracaoSap completo = ConfigHu(huWrite: true, comUrl: true);
        IProdutoAcabadoHandlingUnitSapServico gateway = FabricaProdutoAcabadoHandlingUnitSapServico.Criar(true, false, false, () => completo);
        Assert.True(gateway.EnvioAutorizado);
        Assert.IsType<ProdutoAcabadoHandlingUnitSapGateway>(gateway);
    }

    private static ConfiguracaoSap ConfigHu(bool huWrite, bool comUrl) => new()
    {
        Usuario = "u",
        Senha = "p",
        HostsPermitidos = ["sap.exemplo.local"],
        HuWriteHabilitado = huWrite,
        HandlingUnitBaseUrl = comUrl ? BaseUrl : string.Empty
    };

    private static ConfiguracaoSap LerConfig(Func<string, string?> env)
    {
        string caminho = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(caminho, """{ "sap": { "base_url": "https://sap.exemplo.local/odata", "hosts_permitidos": ["sap.exemplo.local"] } }""");
        try
        {
            return LeitorConfiguracaoSap.Carregar(caminho, env);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    private static HandlingUnitCaixaRequest Request()
        => new ProdutoAcabadoHandlingUnitCaixaRequestBuilder().Montar(new ProdutoAcabadoCaixa
        {
            NumeroOrdemProducao = "1001951", ItemOrdemProducao = "0001", Material = "4000108", Lote = "L1",
            Centro = "3007", Deposito = "PP02", MaterialEmbalagem = "3000009",
            PesoBrutoKg = 10.5m, TaraKg = 0.5m, PesoLiquidoKg = 10.0m, UnidadePeso = "KG",
            QuantidadeProdutos = 60, UnidadeQuantidade = "UN", CorrelationId = Guid.NewGuid()
        }).Request!;

    public enum ModoFalha { Nenhum, Timeout, ConexaoInterrompida }

    private sealed class HandlerContador(HttpStatusCode status, string body, ModoFalha modo = ModoFalha.Nenhum) : HttpMessageHandler
    {
        public int Posts { get; private set; }
        public Uri? UriCsrf { get; private set; }
        public Uri? UriPost { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get) // CSRF Fetch
            {
                UriCsrf = request.RequestUri;
                HttpResponseMessage csrf = new(HttpStatusCode.OK) { Content = new StringContent("{}") };
                csrf.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-fake");
                return Task.FromResult(csrf);
            }

            UriPost = request.RequestUri;
            Posts++; // conta a TENTATIVA de POST (mesmo quando a rede falha em seguida)
            return modo switch
            {
                ModoFalha.Timeout => throw new TaskCanceledException("timeout simulado"),
                ModoFalha.ConexaoInterrompida => throw new HttpRequestException("conexão interrompida simulada"),
                _ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) })
            };
        }
    }

    // REV3-B3: handler que conta GET CSRF e POST separadamente e permite variar o resultado do GET CSRF.
    private sealed class HandlerCsrf(int statusCsrf, string tokenCsrf, FalhaCsrf falha) : HttpMessageHandler
    {
        public int GetsCsrf { get; private set; }
        public int Posts { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get) // GET CSRF fetch
            {
                GetsCsrf++;
                if (falha == FalhaCsrf.RedeException)
                {
                    throw new HttpRequestException("falha de rede no GET CSRF (simulada)");
                }

                if (falha == FalhaCsrf.Timeout)
                {
                    throw new TaskCanceledException("timeout no GET CSRF (simulado)");
                }

                HttpResponseMessage resp = new((HttpStatusCode)statusCsrf) { Content = new StringContent("{}") };
                if (!string.IsNullOrEmpty(tokenCsrf))
                {
                    resp.Headers.TryAddWithoutValidation("X-CSRF-Token", tokenCsrf);
                }

                return Task.FromResult(resp);
            }

            // Só chega aqui se o CSRF passou no gate (200 + token) — POST confirmado.
            Posts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{ "d": { "HandlingUnitExternalID": "HU-CSRF-OK", "Warehouse": "" } }""")
            });
        }
    }
}
