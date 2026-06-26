using System.Net;
using System.Security.Authentication;
using System.Text;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Cliente de Movimentos de Material (API_MATERIAL_DOCUMENT_SRV): serializacao do payload OData V2
/// (movimento 101), fluxo CSRF Fetch -> POST, parse do documento criado e DIAGNOSTICO da falha
/// (etapa + status HTTP real + classificacao de rede/TLS/timeout), sempre sanitizado.
/// </summary>
public sealed class MaterialDocumentSapApiClientTests
{
    [Fact]
    public void SerializarPayload_DeveConterMovimento101EChavesDoPedido()
    {
        string json = MaterialDocumentSapApiClient.SerializarPayload(Requisicao());

        Assert.Contains("\"GoodsMovementCode\":\"01\"", json);
        Assert.Contains("\"GoodsMovementType\":\"101\"", json);
        Assert.Contains("\"GoodsMovementRefDocType\":\"B\"", json);
        Assert.Contains("\"PurchaseOrder\":\"4500001253\"", json);
        Assert.Contains("\"PurchaseOrderItem\":\"00010\"", json);
        Assert.Contains("\"QuantityInEntryUnit\":\"5.500\"", json);
        Assert.Contains("\"EntryUnit\":\"KG\"", json);
        Assert.Contains("to_MaterialDocumentItem", json);
        Assert.Contains("/Date(", json);
    }

    [Fact]
    public void FormatarDataODataV2_DeveUsarMeiaNoiteUtc()
    {
        string formatada = MaterialDocumentSapApiClient.FormatarDataODataV2(new DateTime(2024, 1, 1));

        Assert.Equal("/Date(1704067200000)/", formatada);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void FormatarDataODataV2_QualquerKind_DeveGerarMeiaNoiteUtcSemLancar(DateTimeKind kind)
    {
        DateTime data = new(2024, 1, 1, 0, 0, 0, kind);

        string formatada = MaterialDocumentSapApiClient.FormatarDataODataV2(data);

        Assert.Equal("/Date(1704067200000)/", formatada);
    }

    [Fact]
    public void FormatarDataODataV2_DataLocalComHora_NaoDeveLancarEManterODia()
    {
        // Cenario real: o controller usa DateTime.Today (Kind=Local). Antes lancava ArgumentException
        // de offset. Agora gera a meia-noite UTC do dia, sem deslocar o dia.
        DateTime localComHora = new(2024, 1, 1, 23, 30, 0, DateTimeKind.Local);

        string formatada = MaterialDocumentSapApiClient.FormatarDataODataV2(localComHora);

        Assert.Equal("/Date(1704067200000)/", formatada);
    }

    [Fact]
    public void FormatarDataODataV2_DataAtual_DeveTerFormatoValidoSemLancar()
    {
        string formatada = MaterialDocumentSapApiClient.FormatarDataODataV2(DateTime.Now);

        Assert.Matches(@"^/Date\(\d+\)/$", formatada);
    }

    [Fact]
    public void SerializarPayload_ComDatasLocaisDoLancamento_NaoDeveLancarArgumentException()
    {
        // Reproduz a montagem real do payload com PostingDate/DocumentDate = DateTime.Today (Local).
        MaterialDocumentSapRequest requisicao = Requisicao() with
        {
            PostingDate = DateTime.Today,
            DocumentDate = DateTime.Today
        };

        string json = MaterialDocumentSapApiClient.SerializarPayload(requisicao);

        Assert.Contains("/Date(", json);
        Assert.Contains("\"GoodsMovementType\":\"101\"", json);
        Assert.Contains("\"GoodsMovementRefDocType\":\"B\"", json);
    }

    [Fact]
    public async Task CsrfFetch_DeveUsarTop1EBasicAuthNoFetchENoPost()
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage criado = new(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """{"d":{"MaterialDocument":"5000000124","MaterialDocumentYear":"2026"}}""",
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, criado);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        await cliente.CriarDocumentoMaterialAsync(Requisicao());

        // GET de CSRF limita volume (nao baixa milhoes de registros)
        Assert.Contains("$top=1", handler.Urls[0]);
        Assert.StartsWith("GET", handler.Metodos[0].Method);
        // Basic Auth aplicado no CSRF Fetch E no POST
        Assert.True(handler.AuthPresenteEmTodas);
    }

    [Fact]
    public async Task CsrfFetchSemToken_DeveAbortarSemEnviarPost()
    {
        RespostaHandler handler = new(new HttpResponseMessage(HttpStatusCode.OK));
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaCsrfFetch, resultado.Etapa);
        Assert.Contains("token", resultado.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.QuantidadeRequisicoes);
        Assert.DoesNotContain(handler.Metodos, metodo => metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task CsrfFetch_Http401_DeveRegistrarStatus401SemPost()
    {
        RespostaHandler handler = new(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            ReasonPhrase = "Nicht autorisiert"
        });
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Equal(401, resultado.StatusHttp);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaCsrfFetch, resultado.Etapa);
        Assert.Contains("HTTP 401", resultado.MensagemSanitizada);
        Assert.Equal(1, handler.QuantidadeRequisicoes);
        Assert.DoesNotContain(handler.Metodos, metodo => metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task Post_Http400_DeveRegistrarStatus400EMensagemSanitizada()
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage erro = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":{"message":{"value":"Campo obrigatorio ausente"}}}""",
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, erro);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Equal(400, resultado.StatusHttp);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaPost, resultado.Etapa);
        Assert.Contains("HTTP 400", resultado.MensagemSanitizada);
        Assert.Equal(2, handler.QuantidadeRequisicoes); // fetch + post
    }

    [Fact]
    public async Task ExcecaoTlsSemResposta_DeveIndicarTlsComStatusNull()
    {
        RespostaHandler handler = new(
            new HttpRequestException("falha tls", new AuthenticationException("certificado")));
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.StatusHttp);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaCsrfFetch, resultado.Etapa);
        Assert.Contains("TLS", resultado.MensagemSanitizada);
        Assert.DoesNotContain(handler.Metodos, metodo => metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task Http201_DeveRetornarSucessoComNumeroDoDocumento()
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage criado = new(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """{"d":{"MaterialDocument":"5000000124","MaterialDocumentYear":"2024","to_MaterialDocumentItem":{"results":[{"MaterialDocumentItem":"0001"}]}}}""",
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, criado);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.True(resultado.Sucesso);
        Assert.Equal(201, resultado.StatusHttp);
        Assert.Equal("5000000124", resultado.MaterialDocument);
        Assert.Equal("2024", resultado.MaterialDocumentYear);
        Assert.Contains(handler.Metodos, metodo => metodo == HttpMethod.Post);
        Assert.Equal("token-ok", handler.TokenCsrfPost);
    }

    [Fact]
    public async Task Post_ErroODataMmIm_DeveSintetizarCodeMsgTxnTimestampEDetalhes()
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage erro = new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":{"code":"MM_IM_ODATA_API_MDOC/014","message":{"value":"Material Document processing failed"},"innererror":{"transactionid":"ABC123TXN","timestamp":"20260625120000","errordetails":[{"code":"M7021","message":"Deficit of stock 5.5 KG","severity":"error"},{"code":"M7022","message":"Check storage location PP01","severity":"error"}]}}}""",
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, erro);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Equal(400, resultado.StatusHttp);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaPost, resultado.Etapa);
        Assert.Contains("MM_IM_ODATA_API_MDOC/014", resultado.MensagemSanitizada);
        Assert.Contains("ABC123TXN", resultado.MensagemSanitizada);       // transactionid p/ /IWFND/ERROR_LOG
        Assert.Contains("20260625120000", resultado.MensagemSanitizada);  // timestamp
        Assert.Contains("Deficit of stock", resultado.MensagemSanitizada); // detalhe util
        Assert.True(resultado.MensagemSanitizada.Length <= 1000);
    }

    [Fact]
    public void DescreverPayloadSanitizado_DeveConterCamposFuncionaisSemSegredo()
    {
        string descricao = MaterialDocumentSapApiClient.DescreverPayloadSanitizado(Requisicao());

        Assert.Contains("GoodsMovementCode=01", descricao);
        Assert.Contains("GoodsMovementType=101", descricao);
        Assert.Contains("Material=3500027", descricao);
        Assert.Contains("Plant=3007", descricao);
        Assert.Contains("StorageLocation=PP01", descricao);
        Assert.Contains("EntryUnit=KG", descricao);
        Assert.Contains("PurchaseOrder=4500001253", descricao);
        Assert.Contains("PurchaseOrderItem=00010", descricao);
        Assert.Contains("MaterialDocumentHeaderText=", descricao);
        Assert.DoesNotContain("usuario-teste", descricao); // sem credencial/segredo
        Assert.DoesNotContain("senha-teste", descricao);
        Assert.True(descricao.Length <= 1000);
    }

    [Fact]
    public async Task Post2xxSemMaterialDocument_DeveFalharComEtapaParseResposta()
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage criado = new(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """{"d":{"MaterialDocumentYear":"2026"}}""", // sem MaterialDocument
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, criado);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso); // 2xx sem documento NAO e sucesso local
        Assert.Equal(MaterialDocumentSapApiClient.EtapaParse, resultado.Etapa);
        Assert.Equal(201, resultado.StatusHttp);
        Assert.Contains("MaterialDocument", resultado.MensagemSanitizada);
    }

    [Fact]
    public async Task Post2xxSemMaterialDocumentYear_DeveFalharComEtapaParseResposta()
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage criado = new(HttpStatusCode.Created)
        {
            Content = new StringContent(
                """{"d":{"MaterialDocument":"5000000124"}}""", // sem MaterialDocumentYear
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, criado);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaParse, resultado.Etapa);
        Assert.Equal(201, resultado.StatusHttp);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task PostHttpErro_DeveRetornarStatusEMensagemSanitizada(HttpStatusCode status)
    {
        HttpResponseMessage fetch = new(HttpStatusCode.OK);
        fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "token-ok");
        HttpResponseMessage erro = new(status)
        {
            Content = new StringContent(
                """{"error":{"message":{"value":"Authorization: Basic segredo-nao-pode-vazar"}}}""",
                Encoding.UTF8, "application/json")
        };
        RespostaHandler handler = new(fetch, erro);
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoMaterialDocumentSap resultado =
            await cliente.CriarDocumentoMaterialAsync(Requisicao());

        Assert.False(resultado.Sucesso);
        Assert.Equal((int)status, resultado.StatusHttp);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaPost, resultado.Etapa);
        Assert.DoesNotContain(
            "segredo-nao-pode-vazar",
            resultado.MensagemSanitizada,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construcao_ComUrlForaDaAllowlist_DeveLancarComEtapaValidacaoUrl()
    {
        ConfiguracaoSap configuracao = CriarConfiguracao(
            "https://host-nao-autorizado.local/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/");
        using HttpClient http = new(new RespostaHandler());

        MaterialDocumentClienteException erro = Assert.Throws<MaterialDocumentClienteException>(
            () => new MaterialDocumentSapApiClient(configuracao, http));

        Assert.Equal(MaterialDocumentSapApiClient.EtapaValidacaoUrl, erro.Etapa);
        Assert.NotNull(erro.InnerException);
    }

    [Fact]
    public void Construcao_ComUrlNaoHttps_DeveLancarComEtapaValidacaoUrl()
    {
        ConfiguracaoSap configuracao = CriarConfiguracao(
            "http://sap.exemplo.local/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/");
        using HttpClient http = new(new RespostaHandler());

        MaterialDocumentClienteException erro = Assert.Throws<MaterialDocumentClienteException>(
            () => new MaterialDocumentSapApiClient(configuracao, http));

        Assert.Equal(MaterialDocumentSapApiClient.EtapaValidacaoUrl, erro.Etapa);
    }

    [Fact]
    public void SanitizarExcecaoTecnica_DevePreservarMessageUtilERedigirSegredo()
    {
        ArgumentException excecao = new(
            "Senha invalida: senha=hunter2; Authorization: Basic AbCdEf123. (Parameter 'requestUri')");

        string texto = MaterialDocumentSapApiClient.SanitizarExcecaoTecnica(excecao, "hunter2");

        // tipo + Message util preservada (parametro), mas segredos redigidos
        Assert.Contains("ArgumentException", texto);
        Assert.Contains("Parameter 'requestUri'", texto);
        Assert.DoesNotContain("hunter2", texto);
        Assert.DoesNotContain("AbCdEf123", texto);
        Assert.True(texto.Length <= 500);
    }

    [Fact]
    public void SanitizarExcecaoTecnica_DeveIncluirInnerException()
    {
        Exception excecao = new ArgumentException(
            "falha externa", new InvalidOperationException("causa raiz interna"));

        string texto = MaterialDocumentSapApiClient.SanitizarExcecaoTecnica(excecao);

        Assert.Contains("ArgumentException", texto);
        Assert.Contains("Inner InvalidOperationException", texto);
        Assert.Contains("causa raiz interna", texto);
    }

    private static MaterialDocumentSapRequest Requisicao()
        => new()
        {
            GoodsMovementCode = "01",
            PostingDate = new DateTime(2024, 1, 1),
            DocumentDate = new DateTime(2024, 1, 1),
            MaterialDocumentHeaderText = "Entrada Pedido 4500001253 via FugaPET",
            Itens =
            [
                new MaterialDocumentSapItemRequest
                {
                    Material = "3500027",
                    Plant = "3007",
                    StorageLocation = "PP01",
                    GoodsMovementType = "101",
                    GoodsMovementRefDocType = "B",
                    QuantityInEntryUnit = "5.500",
                    EntryUnit = "KG",
                    PurchaseOrder = "4500001253",
                    PurchaseOrderItem = "00010"
                }
            ]
        };

    private static ConfiguracaoSap CriarConfiguracao(string? materialDocumentBaseUrl = null)
        => new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            MaterialDocumentBaseUrl = materialDocumentBaseUrl
                ?? "https://sap.exemplo.local/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            SapClient = "110",
            HostsPermitidos = ["sap.exemplo.local"],
            EscritaHabilitada = true
        };

    private sealed class RespostaHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respostas;
        private readonly Exception? _excecaoNaPrimeira;

        public RespostaHandler(params HttpResponseMessage[] respostas)
            => _respostas = new Queue<HttpResponseMessage>(respostas);

        public RespostaHandler(Exception excecaoNaPrimeira)
        {
            _respostas = new Queue<HttpResponseMessage>();
            _excecaoNaPrimeira = excecaoNaPrimeira;
        }

        public int QuantidadeRequisicoes { get; private set; }
        public List<HttpMethod> Metodos { get; } = [];
        public List<string> Urls { get; } = [];
        public string? TokenCsrfPost { get; private set; }
        public bool AuthPresenteEmTodas { get; private set; } = true;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            QuantidadeRequisicoes++;
            Metodos.Add(request.Method);
            Urls.Add(request.RequestUri?.ToString() ?? string.Empty);
            if (request.Headers.Authorization is null)
            {
                AuthPresenteEmTodas = false;
            }

            if (request.Method == HttpMethod.Post)
            {
                TokenCsrfPost = request.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? tokens)
                    ? tokens.Single()
                    : null;
            }

            if (_excecaoNaPrimeira is not null)
            {
                return Task.FromException<HttpResponseMessage>(_excecaoNaPrimeira);
            }

            return Task.FromResult(_respostas.Dequeue());
        }
    }
}
