using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Gateway REAL da Handling Unit de caixa (API_HANDLINGUNIT). Infraestrutura técnica: Basic auth, CSRF
/// (Fetch → uso na MESMA sessão/cookie), POST de criação, timeout, sanitização, HTTP status, mensagens SAP
/// e ETag. FAIL-CLOSED por padrão. A base é VALIDADA (HTTPS, sem credenciais na URL, host na allowlist) por
/// <see cref="ValidadorUrlSap"/> e os destinos resolvem SEMPRE dentro da base (.../0001/HandlingUnit) via
/// <see cref="ValidadorUrlSap.ValidarDestino"/> — nunca ao host raiz. NUNCA persiste/loga Authorization,
/// cookie, CSRF token ou senha.
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitSapGateway : IProdutoAcabadoHandlingUnitSapServico
{
    // Recurso relativo (SEM barra inicial): resolve dentro da base .../0001/ (nunca ao host raiz).
    private const string RecursoCriacao = "HandlingUnit?sap-client=110";
    // REV4-B1: GET CSRF alinhado ao fluxo real HML-2 já comprovado (gerou HU SAP 300014350): $top=1.
    private const string RecursoCsrf = "HandlingUnit?$top=1&sap-client=110";

    private readonly Uri? _baseUri; // normalizada e validada (termina com "/") ou null quando inválida
    private readonly IReadOnlyList<string> _hostsPermitidos;
    private readonly string _usuario;
    private readonly string _senha;
    private readonly Func<HttpMessageHandler>? _fabricaHandler;
    private readonly TimeSpan _timeout;

    public ProdutoAcabadoHandlingUnitSapGateway()
        : this(baseUrl: null, hostsPermitidos: null, usuario: string.Empty, senha: string.Empty, envioAutorizado: false, fabricaHandler: null)
    {
    }

    /// <param name="baseUrl">URL base do servico API_HANDLINGUNIT (ate .../0001). Validada por ValidadorUrlSap.</param>
    /// <param name="hostsPermitidos">Allowlist efetiva; o host da base DEVE pertencer a ela.</param>
    /// <param name="envioAutorizado">Fail-closed: só true no ambiente HML autorizado (flag HU).</param>
    /// <param name="fabricaHandler">Handler HTTP injetável (fake nos testes). Sem ele, o gateway não envia.</param>
    public ProdutoAcabadoHandlingUnitSapGateway(
        string? baseUrl,
        IReadOnlyList<string>? hostsPermitidos,
        string usuario,
        string senha,
        bool envioAutorizado,
        Func<HttpMessageHandler>? fabricaHandler,
        TimeSpan? timeout = null)
    {
        _hostsPermitidos = hostsPermitidos ?? [];
        _usuario = usuario ?? string.Empty;
        _senha = senha ?? string.Empty;
        EnvioAutorizado = envioAutorizado;
        _fabricaHandler = fabricaHandler;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);

        // §8/§9: base normalizada (.../0001/) + validada (HTTPS, sem userinfo, host na allowlist). Inválida ⇒ null (fail-closed).
        try
        {
            _baseUri = string.IsNullOrWhiteSpace(baseUrl)
                ? null
                : ValidadorUrlSap.ValidarBaseUrl(baseUrl, _hostsPermitidos);
        }
        catch (InvalidOperationException)
        {
            _baseUri = null;
        }
    }

    public bool EnvioAutorizado { get; }

    private bool PodeExecutar => EnvioAutorizado && _fabricaHandler is not null && _baseUri is not null;

    /// <summary>
    /// §11: autorização de escrita SAP ISOLADA no gateway de HU. Aceita EXCLUSIVAMENTE <c>POST /HandlingUnit</c>
    /// (a query sap-client é preservada). Bloqueia qualquer outro endpoint, PATCH e DELETE. HU habilitada
    /// NUNCA significa "escrita SAP genérica".
    /// </summary>
    public static bool EscritaHuPermitida(HttpMethod metodo, string endpointRelativo)
    {
        if (metodo != HttpMethod.Post)
        {
            return false;
        }

        string caminho = (endpointRelativo ?? string.Empty).Trim();
        int corte = caminho.IndexOf('?', StringComparison.Ordinal);
        if (corte >= 0)
        {
            caminho = caminho[..corte];
        }

        caminho = "/" + caminho.Trim().TrimStart('/').TrimEnd('/');
        return string.Equals(caminho, "/HandlingUnit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// §8: monta e VALIDA o destino HU resolvendo dentro da base (.../0001/&lt;recurso&gt;). Helper único —
    /// nenhuma concatenação de URL espalhada. Lança se o destino sair da base/allowlist (defesa).
    /// </summary>
    private Uri MontarDestinoHu(string recursoRelativoSemBarra)
    {
        Uri destino = new(_baseUri!, recursoRelativoSemBarra);
        return ValidadorUrlSap.ValidarDestino(destino, _baseUri!, _hostsPermitidos);
    }

    public async Task<ResultadoPostHandlingUnit> CriarHandlingUnitCaixaAsync(
        HandlingUnitCaixaRequest request, string endpointRelativo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!PodeExecutar)
        {
            return NaoEnviado("Gateway HU fail-closed: POST não executado (autorização/URL HU indisponível).");
        }

        // §11: mesmo autorizado, o gateway só transmite POST /HandlingUnit — nunca outro endpoint/método.
        if (!EscritaHuPermitida(HttpMethod.Post, endpointRelativo))
        {
            return NaoEnviado("Gateway HU: escrita permitida apenas para POST /HandlingUnit.");
        }

        Uri destinoPost;
        try
        {
            destinoPost = MontarDestinoHu(RecursoCriacao);
        }
        catch (InvalidOperationException)
        {
            return NaoEnviado("Gateway HU: destino do POST fora da base/allowlist autorizada.");
        }

        using HttpClient http = CriarHttpClient();

        // §6: CSRF FAIL-CLOSED — o POST /HandlingUnit só ocorre se o GET CSRF (mesma sessão/handler/cookie)
        // retornou 200 com X-CSRF-Token NÃO vazio. Qualquer falha ⇒ NENHUM POST (POST_COUNT=0).
        ResultadoCsrfHu csrf = await ObterCsrfSeguroAsync(http, cancellationToken);
        if (!csrf.Sucesso)
        {
            // Falha PRÉ-POST determinística (SendAsync do POST nunca chamado): erro NÃO reprocessável,
            // nunca "indeterminado" (não há POST que possa ter criado a HU).
            return new ResultadoPostHandlingUnit
            {
                Cenario = CenarioPostHandlingUnit.ErroDefinitivo,
                PodeReprocessar = false,
                MensagemSanitizada = $"POST /HandlingUnit não executado: {csrf.Motivo}"
            };
        }

        try
        {
            using HttpRequestMessage req = new(HttpMethod.Post, destinoPost);
            AplicarAutenticacao(req, csrf.Token);
            req.Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = await http.SendAsync(req, cancellationToken);
            string corpo = await resp.Content.ReadAsStringAsync(cancellationToken);
            int status = (int)resp.StatusCode;
            string? corpoJson = JsonOuNulo(corpo);
            string? etag = resp.Headers.ETag?.Tag;

            return status switch
            {
                201 => ClassificarSucessoOuIndeterminado(corpoJson, etag),
                401 or 403 => new ResultadoPostHandlingUnit
                {
                    Cenario = CenarioPostHandlingUnit.NaoAutorizado,
                    HttpStatus = status,
                    ResponseJsonSanitizado = corpoJson,
                    SapMessagesJson = corpoJson,
                    PodeReprocessar = false,
                    MensagemSanitizada = $"Não autorizado no POST da HU (HTTP {status})."
                },
                // §12: 500/503 (e demais não 2xx) NÃO liberam retry automático da mesma caixa.
                _ => new ResultadoPostHandlingUnit
                {
                    Cenario = CenarioPostHandlingUnit.ErroDefinitivo,
                    HttpStatus = status,
                    ResponseJsonSanitizado = corpoJson,
                    SapMessagesJson = corpoJson,
                    PodeReprocessar = false,
                    MensagemSanitizada = $"Erro no POST da HU (HTTP {status})."
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // §11: timeout APÓS possível POST ⇒ indeterminado (sem retry, sem 2º POST, nunca reprocessável).
            return new ResultadoPostHandlingUnit
            {
                Cenario = CenarioPostHandlingUnit.Timeout,
                PodeReprocessar = false,
                MensagemSanitizada = "Timeout no POST da HU: resultado indeterminado (sem retry, sem 2º POST)."
            };
        }
        catch (HttpRequestException)
        {
            // §11: conexão interrompida/resposta perdida APÓS a tentativa de POST ⇒ INDETERMINADO_TIMEOUT.
            // Nunca ErroDefinitivo reprocessável (o servidor pode ter criado a HU).
            return new ResultadoPostHandlingUnit
            {
                Cenario = CenarioPostHandlingUnit.Timeout,
                PodeReprocessar = false,
                MensagemSanitizada = "Falha de rede após a tentativa de POST da HU: resultado indeterminado (sem retry)."
            };
        }
    }

    public async Task<ResultadoReconciliacaoHandlingUnit> ReconciliarHandlingUnitAsync(
        string handlingUnitExternalId, string warehouse, CancellationToken cancellationToken = default)
    {
        if (!PodeExecutar)
        {
            return new ResultadoReconciliacaoHandlingUnit
            {
                Cenario = CenarioReconciliacaoHandlingUnit.Indeterminado,
                MensagemSanitizada = "Gateway HU fail-closed: reconciliação não executada nesta frente."
            };
        }

        string recurso =
            $"HandlingUnit(HandlingUnitExternalID='{Uri.EscapeDataString(handlingUnitExternalId)}',Warehouse='{Uri.EscapeDataString(warehouse ?? string.Empty)}')?sap-client=110";
        Uri destino;
        try
        {
            destino = MontarDestinoHu(recurso);
        }
        catch (InvalidOperationException)
        {
            return new ResultadoReconciliacaoHandlingUnit
            {
                Cenario = CenarioReconciliacaoHandlingUnit.Indeterminado,
                MensagemSanitizada = "Reconciliação indeterminada: destino fora da base/allowlist."
            };
        }

        using HttpClient http = CriarHttpClient();
        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, destino);
            AplicarAutenticacao(req, csrf: null);
            using HttpResponseMessage resp = await http.SendAsync(req, cancellationToken);
            string corpo = await resp.Content.ReadAsStringAsync(cancellationToken);
            int status = (int)resp.StatusCode;
            string? corpoJson = JsonOuNulo(corpo);

            if (status == 404)
            {
                return new ResultadoReconciliacaoHandlingUnit
                {
                    Cenario = CenarioReconciliacaoHandlingUnit.NaoEncontrado,
                    HttpStatus = 404,
                    HandlingUnitExternalId = handlingUnitExternalId,
                    Warehouse = warehouse,
                    ResponseJsonSanitizado = corpoJson,
                    MensagemSanitizada = "HU não encontrada na reconciliação (HTTP 404)."
                };
            }

            if (status == 200)
            {
                string? huRetornada = ExtrairCampo(corpoJson, "HandlingUnitExternalID");
                bool comparacaoOk = string.Equals(
                    (huRetornada ?? string.Empty).Trim(),
                    (handlingUnitExternalId ?? string.Empty).Trim(),
                    StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(huRetornada);
                return new ResultadoReconciliacaoHandlingUnit
                {
                    Cenario = CenarioReconciliacaoHandlingUnit.Confirmado,
                    HttpStatus = 200,
                    HandlingUnitExternalId = huRetornada ?? handlingUnitExternalId,
                    Warehouse = ExtrairCampo(corpoJson, "Warehouse") ?? warehouse,
                    ResponseJsonSanitizado = corpoJson,
                    Etag = resp.Headers.ETag?.Tag,
                    CreatedByUserSap = ExtrairCampo(corpoJson, "CreatedByUser"),
                    CreationDatetimeSap = ExtrairData(corpoJson, "CreationDateTime"),
                    ComparacaoAprovada = comparacaoOk,
                    MensagemSanitizada = comparacaoOk
                        ? "HU reconciliada e confirmada (HTTP 200)."
                        : "HU retornada não confere com a esperada (comparação reprovada)."
                };
            }

            return new ResultadoReconciliacaoHandlingUnit
            {
                Cenario = CenarioReconciliacaoHandlingUnit.Indeterminado,
                HttpStatus = status,
                ResponseJsonSanitizado = corpoJson,
                MensagemSanitizada = $"Reconciliação indeterminada (HTTP {status})."
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ResultadoReconciliacaoHandlingUnit
            {
                Cenario = CenarioReconciliacaoHandlingUnit.Indeterminado,
                MensagemSanitizada = $"Reconciliação indeterminada: {ex.GetType().Name}."
            };
        }
    }

    private static ResultadoPostHandlingUnit NaoEnviado(string mensagem)
        => new() { Cenario = CenarioPostHandlingUnit.NaoEnviado, MensagemSanitizada = mensagem };

    private HttpClient CriarHttpClient()
        => new(_fabricaHandler!.Invoke(), disposeHandler: true) { Timeout = _timeout };

    /// <summary>Resultado do GET CSRF: sucesso só com HTTP 200 + token não vazio.</summary>
    private readonly record struct ResultadoCsrfHu(bool Sucesso, string? Token, string Motivo);

    // §6: CSRF FAIL-CLOSED pela MESMA sessão (mesmo HttpClient/handler/cookie). Destino validado (nunca host
    // raiz). NÃO engole falha silenciosamente: qualquer condição fora de "200 + token" retorna Sucesso=false.
    private async Task<ResultadoCsrfHu> ObterCsrfSeguroAsync(HttpClient http, CancellationToken ct)
    {
        Uri destino;
        try
        {
            destino = MontarDestinoHu(RecursoCsrf);
        }
        catch (InvalidOperationException)
        {
            return new ResultadoCsrfHu(false, null, "URL do GET CSRF inválida.");
        }

        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, destino);
            AplicarAutenticacao(req, csrf: null);
            req.Headers.TryAddWithoutValidation("X-CSRF-Token", "Fetch");
            using HttpResponseMessage resp = await http.SendAsync(req, ct);
            if ((int)resp.StatusCode != 200)
            {
                return new ResultadoCsrfHu(false, null, $"GET CSRF retornou HTTP {(int)resp.StatusCode}.");
            }

            string? token = resp.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? valores)
                ? valores.FirstOrDefault()
                : null;
            return string.IsNullOrWhiteSpace(token)
                ? new ResultadoCsrfHu(false, null, "GET CSRF sem X-CSRF-Token.")
                : new ResultadoCsrfHu(true, token, string.Empty);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new ResultadoCsrfHu(false, null, "Timeout no GET CSRF.");
        }
        catch (HttpRequestException ex)
        {
            return new ResultadoCsrfHu(false, null, $"Falha de rede no GET CSRF: {ex.GetType().Name}.");
        }
    }

    private void AplicarAutenticacao(HttpRequestMessage req, string? csrf)
    {
        string credenciais = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_usuario}:{_senha}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credenciais);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(csrf))
        {
            req.Headers.TryAddWithoutValidation("X-CSRF-Token", csrf);
        }
    }

    /// <summary>
    /// §8: HTTP 201 só é CONFIRMADO com HandlingUnitExternalID NÃO vazio. Body vazio/JSON inválido/HU
    /// ausente/HU vazia ⇒ resultado INDETERMINADO (a HU pode ter sido criada no SAP) — sem retry, sem 2º POST.
    /// </summary>
    private static ResultadoPostHandlingUnit ClassificarSucessoOuIndeterminado(string? corpoJson, string? etag)
    {
        string? hu = ExtrairCampo(corpoJson, "HandlingUnitExternalID");
        if (string.IsNullOrWhiteSpace(hu))
        {
            return new ResultadoPostHandlingUnit
            {
                Cenario = CenarioPostHandlingUnit.Timeout,
                HttpStatus = 201,
                ResponseJsonSanitizado = corpoJson,
                PodeReprocessar = false,
                MensagemSanitizada = "HTTP 201 sem HandlingUnitExternalID: resultado indeterminado (sem retry, sem 2º POST)."
            };
        }

        return ConfirmadoPost(corpoJson, etag);
    }

    private static ResultadoPostHandlingUnit ConfirmadoPost(string? corpoJson, string? etag)
        => new()
        {
            Cenario = CenarioPostHandlingUnit.Confirmado,
            HttpStatus = 201,
            HandlingUnitExternalId = ExtrairCampo(corpoJson, "HandlingUnitExternalID"),
            Warehouse = ExtrairCampo(corpoJson, "Warehouse"),
            ResponseJsonSanitizado = corpoJson,
            SapMessagesJson = corpoJson,
            Etag = etag,
            CreatedByUserSap = ExtrairCampo(corpoJson, "CreatedByUser"),
            CreationDatetimeSap = ExtrairData(corpoJson, "CreationDateTime"),
            MensagemSanitizada = "HU criada com sucesso (HTTP 201)."
        };

    private static string? JsonOuNulo(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return null;
        }

        try
        {
            using JsonDocument _ = JsonDocument.Parse(corpo);
            return corpo;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Raiz(string? corpoJson)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(corpoJson);
            JsonElement raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("d", out JsonElement d))
            {
                raiz = d;
            }

            return raiz.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtrairCampo(string? corpoJson, string propriedade)
    {
        JsonElement? raiz = Raiz(corpoJson);
        if (raiz is { ValueKind: JsonValueKind.Object } obj
            && obj.TryGetProperty(propriedade, out JsonElement valor)
            && valor.ValueKind == JsonValueKind.String)
        {
            return valor.GetString();
        }

        return null;
    }

    private static DateTimeOffset? ExtrairData(string? corpoJson, string propriedade)
    {
        string? texto = ExtrairCampo(corpoJson, propriedade);
        return DateTimeOffset.TryParse(texto, out DateTimeOffset data) ? data : null;
    }
}
