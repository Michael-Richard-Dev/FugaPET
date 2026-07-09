using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente do envio CONTROLADO do consumo 261 (API_MATERIAL_DOCUMENT_SRV, OData V2, Basic Auth).
/// POST em A_MaterialDocumentHeader com fluxo CSRF (GET X-CSRF-Token: Fetch -> POST com token,
/// CookieContainer preservado pelo HttpClient). HTTPS + allowlist obrigatorios (ValidadorUrlSap).
/// NUNCA loga/expoe Authorization/senha/cookie/token/payload. Nunca propaga excecao de envio:
/// devolve sempre <see cref="ResultadoEnvioConsumoSap261"/> com etapa/status/mensagem sanitizada.
/// </summary>
public sealed class ConsumoMaterialSap261ApiClient
{
    private const string Recurso = "A_MaterialDocumentHeader";
    internal const string EtapaCsrfFetch = "CSRF_FETCH";
    internal const string EtapaPost = "POST_CONSUMO_261";
    internal const string EtapaParse = "PARSE_RESPOSTA";

    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly AuthenticationHeaderValue _autorizacao;

    public ConsumoMaterialSap261ApiClient(ConfiguracaoSap configuracao, HttpClient httpClient)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = ValidadorUrlSap.ValidarBaseUrl(configuracao.MaterialDocumentBaseUrl, configuracao.HostsPermitidos);
        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
    }

    public async Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
        ConsumoMaterialSap261Request requisicao,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        try
        {
            // ---- CSRF FETCH ----
            string? token;
            Uri urlCsrf = MontarUrlCsrf();
            using (HttpRequestMessage fetch = CriarRequisicao(HttpMethod.Get, urlCsrf))
            {
                fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "Fetch");
                using HttpResponseMessage respostaFetch = await _httpClient.SendAsync(fetch, cancellationToken);
                if (!respostaFetch.IsSuccessStatusCode)
                {
                    string corpoErro = await respostaFetch.Content.ReadAsStringAsync(cancellationToken);
                    return FalhaHttp(
                        EtapaCsrfFetch,
                        HttpMethod.Get.Method,
                        urlCsrf,
                        (int)respostaFetch.StatusCode,
                        corpoErro,
                        correlationId,
                        payloadJson: null);
                }

                token = respostaFetch.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? valores)
                    ? valores.FirstOrDefault()
                    : null;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return ResultadoEnvioConsumoSap261.Falha(
                    $"Etapa {EtapaCsrfFetch}: SAP nao retornou o token X-CSRF-Token. POST abortado.",
                    null, correlationId);
            }

            // ---- POST (payload do builder da Tarefa 6 — sem duplicar montagem) ----
            string json = ConsumoMaterialSapPayloadBuilder.SerializarPreview(requisicao);
            Uri urlCriacao = MontarUrlCriacao();
            using HttpRequestMessage post = CriarRequisicao(HttpMethod.Post, urlCriacao);
            post.Content = new StringContent(json, Encoding.UTF8, "application/json");
            post.Headers.TryAddWithoutValidation("X-CSRF-Token", token);

            using HttpResponseMessage respostaPost = await _httpClient.SendAsync(post, cancellationToken);
            string corpo = await respostaPost.Content.ReadAsStringAsync(cancellationToken);
            return respostaPost.IsSuccessStatusCode
                ? InterpretarSucesso((int)respostaPost.StatusCode, corpo, correlationId)
                : FalhaHttp(
                    EtapaPost,
                    HttpMethod.Post.Method,
                    urlCriacao,
                    (int)respostaPost.StatusCode,
                    corpo,
                    correlationId,
                    json);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Rede/TLS/timeout/URL — antes ou durante o HTTP. Sem credenciais, mas preservando tipo/mensagem.
            string detalhe = MaterialDocumentSapApiClient.SanitizarExcecaoTecnica(
                ex, _autorizacao.Parameter, _configuracao.Usuario, _configuracao.Senha);
            return ResultadoEnvioConsumoSap261.Falha(
                $"Etapa CLIENTE: falha tecnica inesperada. {detalhe}", null, correlationId);
        }
    }

    private static ResultadoEnvioConsumoSap261 InterpretarSucesso(int statusHttp, string corpo, string correlationId)
    {
        string? documento = null;
        string? exercicio = null;
        if (!string.IsNullOrWhiteSpace(corpo))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("d", out JsonElement d))
                {
                    documento = LerTextoNulo(d, "MaterialDocument");
                    exercicio = LerTextoNulo(d, "MaterialDocumentYear");
                }
            }
            catch (JsonException)
            {
                // Corpo nao parseavel: tratado abaixo como ausencia de rastreabilidade.
            }
        }

        // RIGOR: 2xx so e sucesso com MaterialDocument E MaterialDocumentYear. Sem ambos, NAO confirmar.
        if (string.IsNullOrWhiteSpace(documento) || string.IsNullOrWhiteSpace(exercicio))
        {
            return ResultadoEnvioConsumoSap261.Falha(
                $"Etapa {EtapaParse}: SAP retornou sucesso HTTP {statusHttp}, mas nao retornou documento material/ano. "
                + "Envio nao sera marcado como confirmado.",
                statusHttp, correlationId);
        }

        return ResultadoEnvioConsumoSap261.Ok(documento!, exercicio!, statusHttp, correlationId);
    }

    private static ResultadoEnvioConsumoSap261 FalhaHttp(
        string etapa,
        string metodoHttp,
        Uri endpoint,
        int statusHttp,
        string corpo,
        string correlationId,
        string? payloadJson)
    {
        SapErroDetalhado erro = SapErroODataParser.ExtrairErroSap(corpo);
        string mensagem = $"Etapa {etapa}: HTTP {statusHttp}."
            + (string.IsNullOrWhiteSpace(erro.Codigo) ? string.Empty : $" Código: {erro.Codigo}.")
            + (string.IsNullOrWhiteSpace(erro.Mensagem) ? string.Empty : $" Mensagem: {erro.Mensagem}.")
            + (string.IsNullOrWhiteSpace(erro.Detalhes) ? string.Empty : $" Detalhes: {erro.Detalhes}.");

        if (string.Equals(mensagem, $"Etapa {etapa}: HTTP {statusHttp}.", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(corpo))
        {
            mensagem += " Retorno bruto: " + corpo.Trim();
        }

        return ResultadoEnvioConsumoSap261.Falha(
            mensagem,
            statusHttp,
            correlationId,
            metodoHttp,
            endpoint.ToString(),
            corpo,
            erro.Codigo,
            erro.Mensagem,
            erro.Detalhes,
            payloadJson);
    }

    private HttpRequestMessage CriarRequisicao(HttpMethod metodo, Uri destino)
    {
        Uri destinoValidado = ValidadorUrlSap.ValidarDestino(destino, _baseUri, _configuracao.HostsPermitidos);
        HttpRequestMessage requisicao = new(metodo, destinoValidado);
        requisicao.Headers.Authorization = _autorizacao;
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return requisicao;
    }

    private Uri MontarUrlCsrf()
    {
        string query = "$top=1";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return new Uri($"{_baseUri.AbsoluteUri}{Recurso}?{query}", UriKind.Absolute);
    }

    private Uri MontarUrlCriacao()
    {
        string url = $"{_baseUri.AbsoluteUri}{Recurso}";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            url = $"{url}?sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return new Uri(url, UriKind.Absolute);
    }

    private static string? LerTextoNulo(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(valor.GetString()) ? null : valor.GetString())
            : null;
}

