using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class ConfirmacaoProducaoSapApiClient
{
    private const string Recurso = "ProdnOrdConf2";
    internal const string EtapaCsrfFetch = "CSRF_FETCH_CONFIRMACAO";
    internal const string EtapaPost = "POST_CONFIRMACAO_PRODUCAO";
    internal const string EtapaParse = "PARSE_RESPOSTA_CONFIRMACAO";
    internal const string EtapaConsultarOperacoes = "GET_OPERACOES_CONFIRMACAO";

    private static readonly XNamespace DataServices = "http://schemas.microsoft.com/ado/2007/08/dataservices";
    private static readonly XNamespace Metadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly AuthenticationHeaderValue _autorizacao;

    public ConfirmacaoProducaoSapApiClient(ConfiguracaoSap configuracao, HttpClient httpClient)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = ValidadorUrlSap.ValidarBaseUrl(
            configuracao.ProductionOrderConfirmationBaseUrlEfetiva,
            configuracao.HostsPermitidos);
        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
    }

    public async Task<ConfirmacaoProducaoSapResponse> EnviarConfirmacaoAsync(
        ConfirmacaoProducaoSapRequest requisicao,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);

        try
        {
            string? token;
            using (HttpRequestMessage fetch = CriarRequisicao(HttpMethod.Get, MontarUrlCsrf()))
            {
                fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "Fetch");
                using HttpResponseMessage respostaFetch = await _httpClient.SendAsync(fetch, cancellationToken);
                if (!respostaFetch.IsSuccessStatusCode)
                {
                    string corpoErro = await respostaFetch.Content.ReadAsStringAsync(cancellationToken);
                    return FalhaHttp(EtapaCsrfFetch, (int)respostaFetch.StatusCode, corpoErro, correlationId);
                }

                token = respostaFetch.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? valores)
                    ? valores.FirstOrDefault()
                    : null;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return ConfirmacaoProducaoSapResponse.Falha(
                    $"Etapa {EtapaCsrfFetch}: SAP nao retornou o token X-CSRF-Token. POST abortado.",
                    null, correlationId);
            }

            string json = ConfirmacaoProducaoSapPayloadBuilder.SerializarPreview(requisicao);
            using HttpRequestMessage post = CriarRequisicao(HttpMethod.Post, MontarUrlCriacao());
            post.Content = new StringContent(json, Encoding.UTF8, "application/json");
            post.Headers.TryAddWithoutValidation("X-CSRF-Token", token);

            using HttpResponseMessage respostaPost = await _httpClient.SendAsync(post, cancellationToken);
            string corpo = await respostaPost.Content.ReadAsStringAsync(cancellationToken);
            return respostaPost.IsSuccessStatusCode
                ? InterpretarSucesso((int)respostaPost.StatusCode, corpo, correlationId)
                : FalhaHttp(EtapaPost, (int)respostaPost.StatusCode, corpo, correlationId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConfirmacaoProducaoSapResponse.Falha(
                $"Etapa CLIENTE: falha tecnica inesperada ({ex.GetType().Name}).", null, correlationId);
        }
    }

    public async Task<IReadOnlyList<OperacaoConfirmacaoSap>> ConsultarOperacoesConfirmacaoAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        string ordem = (numeroOrdem ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(ordem))
        {
            return [];
        }

        using HttpRequestMessage get = CriarRequisicao(HttpMethod.Get, MontarUrlOperacoes(ordem));
        using HttpResponseMessage resposta = await _httpClient.SendAsync(get, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            return [];
        }

        return MapearOperacoesConfirmacao(corpo);
    }

    internal static IReadOnlyList<OperacaoConfirmacaoSap> MapearOperacoesConfirmacao(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return [];
        }

        string texto = corpo.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return texto.StartsWith("<", StringComparison.Ordinal)
            ? MapearOperacoesConfirmacaoXml(texto)
            : MapearOperacoesConfirmacaoJson(texto);
    }

    private static ConfirmacaoProducaoSapResponse InterpretarSucesso(int statusHttp, string corpo, string correlationId)
    {
        string? confirmationGroup = null;
        string? confirmationCount = null;
        string? ordem = null;
        string? materialDocument = null;
        string? materialDocumentYear = null;
        bool apiConfHasNoGoodsMovements = false;
        bool possuiItemMaterial = false;

        if (!string.IsNullOrWhiteSpace(corpo))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(corpo);
                JsonElement raiz = doc.RootElement.TryGetProperty("d", out JsonElement d) ? d : doc.RootElement;
                confirmationGroup = LerTextoNulo(raiz, "ConfirmationGroup");
                confirmationCount = LerTextoNulo(raiz, "ConfirmationCount");
                ordem = LerTextoNulo(raiz, "OrderID") ?? LerTextoNulo(raiz, "ManufacturingOrder");
                materialDocument = LerTextoNulo(raiz, "MaterialDocument");
                materialDocumentYear = LerTextoNulo(raiz, "MaterialDocumentYear");
                apiConfHasNoGoodsMovements = LerBooleano(raiz, "APIConfHasNoGoodsMovements");
                possuiItemMaterial = PossuiItemMaterialMovimento(raiz);
            }
            catch (JsonException)
            {
                // tratado abaixo como ausencia de rastreabilidade obrigatoria.
            }
        }

        if (string.IsNullOrWhiteSpace(confirmationGroup)
            || string.IsNullOrWhiteSpace(confirmationCount)
            || string.IsNullOrWhiteSpace(ordem))
        {
            return ConfirmacaoProducaoSapResponse.Falha(
                $"Etapa {EtapaParse}: SAP retornou sucesso HTTP {statusHttp}, mas nao retornou ConfirmationGroup/ConfirmationCount/OrderID ou ManufacturingOrder. Envio nao sera marcado como confirmado.",
                statusHttp,
                correlationId);
        }

        // Correcao 1 (Tarefa 17.11): confirmacao criada SEM item de material (results vazio) e sem
        // MaterialDocument NAO e sucesso de consumo 261 — falha funcional, nao marca CONFIRMADO_SAP.
        if (!possuiItemMaterial && string.IsNullOrWhiteSpace(materialDocument))
        {
            return ConfirmacaoProducaoSapResponse.Falha(
                "SAP criou a confirmação, mas não lançou o consumo 261. Como o componente é Backflush e a quantidade apontada da operação foi zero, nenhum item de material foi gerado.",
                statusHttp,
                correlationId);
        }

        return ConfirmacaoProducaoSapResponse.Ok(
            confirmationGroup!,
            confirmationCount!,
            ordem!,
            materialDocument,
            materialDocumentYear,
            !apiConfHasNoGoodsMovements,
            statusHttp,
            correlationId);
    }

    /// <summary>Tarefa 17.11: true se o retorno do POST tem item de material em to_ProdnOrdConfMatlDocItm.results.</summary>
    private static bool PossuiItemMaterialMovimento(JsonElement raiz)
    {
        if (!raiz.TryGetProperty("to_ProdnOrdConfMatlDocItm", out JsonElement navegacao))
        {
            return false;
        }

        JsonElement colecao = navegacao.TryGetProperty("results", out JsonElement results) ? results : navegacao;
        return colecao.ValueKind == JsonValueKind.Array && colecao.EnumerateArray().Any();
    }

    private static ConfirmacaoProducaoSapResponse FalhaHttp(string etapa, int statusHttp, string corpo, string correlationId)
    {
        string detalhe = MaterialDocumentSapApiClient.SintetizarErroSap(corpo);
        string mensagem = MapearMensagemAmigavel(detalhe);
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            mensagem = $"Etapa {etapa}: HTTP {statusHttp}."
                       + (string.IsNullOrEmpty(detalhe) ? " SAP recusou a confirmação de produção. Verifique operação, lote, depósito, quantidade e status da OP." : " " + detalhe);
        }

        return ConfirmacaoProducaoSapResponse.Falha(mensagem, statusHttp, correlationId);
    }

    private static string MapearMensagemAmigavel(string detalhe)
    {
        if (detalhe.Contains("ManufacturingOrder", StringComparison.OrdinalIgnoreCase)
            && detalhe.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: ManufacturingOrder não é aceito no item do movimento de material. A OP deve ficar no cabeçalho como OrderID.";
        }

        if (detalhe.Contains("GoodsMovementIsFinallyPosted", StringComparison.OrdinalIgnoreCase)
            && detalhe.Contains("invalid", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: o payload contém uma propriedade não aceita pela API de Confirmação. Remova GoodsMovementIsFinallyPosted e envie os movimentos pela navegação de itens.";
        }

        if (detalhe.Contains("OrderOperationInternalID", StringComparison.OrdinalIgnoreCase)
            && detalhe.Contains("not allowed for Create", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: OrderOperationInternalID é somente leitura/não permitido na criação. O payload deve enviar OrderOperation e Sequence.";
        }

        if (detalhe.Contains("RU/355", StringComparison.OrdinalIgnoreCase)
            || detalhe.Contains("Not all mandatory fields are filled for the goods movement", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP ainda informa campos obrigatórios ausentes no movimento. Validar $metadata de ProdnOrdConfMatlDocItmType para identificar campos graváveis obrigatórios.";
        }

        if (detalhe.Contains("batch", StringComparison.OrdinalIgnoreCase) || detalhe.Contains("lote", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: lote obrigatório ou inválido para o componente.";
        }

        if (detalhe.Contains("storage", StringComparison.OrdinalIgnoreCase) || detalhe.Contains("dep", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: depósito obrigatório ou inválido.";
        }

        if (detalhe.Contains("authorization", StringComparison.OrdinalIgnoreCase) || detalhe.Contains("not authorized", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: usuário sem autorização para a operação.";
        }

        if (detalhe.Contains("period", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP recusou a confirmação: período contábil fechado ou inválido.";
        }

        return string.Empty;
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

    private Uri MontarUrlOperacoes(string numeroOrdem)
    {
        string query = "$format=json"
            + $"&$filter=OrderID eq '{Uri.EscapeDataString(numeroOrdem.Trim())}'"
            + "&$select=ConfirmationGroup,ConfirmationCount,OrderID,Sequence,OrderOperation,OrderOperationInternalID,Plant,WorkCenter,ConfirmationUnit,ConfirmationYieldQuantity,PostingDate";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return new Uri($"{_baseUri.AbsoluteUri}{Recurso}?{query}", UriKind.Absolute);
    }

    private static IReadOnlyList<OperacaoConfirmacaoSap> MapearOperacoesConfirmacaoJson(string corpo)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(corpo);
            JsonElement raiz = doc.RootElement.TryGetProperty("d", out JsonElement d) ? d : doc.RootElement;
            JsonElement colecao = raiz.TryGetProperty("results", out JsonElement results)
                ? results
                : raiz.TryGetProperty("value", out JsonElement value) ? value : raiz;

            if (colecao.ValueKind == JsonValueKind.Array)
            {
                return colecao.EnumerateArray().Select(MapearOperacaoJson).Where(op => !string.IsNullOrWhiteSpace(op.OrderOperation)).ToList();
            }

            OperacaoConfirmacaoSap unica = MapearOperacaoJson(colecao);
            return string.IsNullOrWhiteSpace(unica.OrderOperation) ? [] : [unica];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static OperacaoConfirmacaoSap MapearOperacaoJson(JsonElement elemento)
        => new()
        {
            OrderId = LerTexto(elemento, "OrderID"),
            Sequence = LerTexto(elemento, "Sequence"),
            OrderOperation = LerTexto(elemento, "OrderOperation"),
            OrderOperationInternalId = LerTexto(elemento, "OrderOperationInternalID"),
            Plant = LerTexto(elemento, "Plant"),
            WorkCenter = LerTexto(elemento, "WorkCenter"),
            ConfirmationUnit = LerTexto(elemento, "ConfirmationUnit")
        };

    private static IReadOnlyList<OperacaoConfirmacaoSap> MapearOperacoesConfirmacaoXml(string corpo)
    {
        try
        {
            XDocument documento = XDocument.Parse(corpo);
            IEnumerable<XElement> propriedades = documento.Descendants(Metadata + "properties");
            return propriedades
                .Select(MapearOperacaoXml)
                .Where(op => !string.IsNullOrWhiteSpace(op.OrderOperation))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static OperacaoConfirmacaoSap MapearOperacaoXml(XElement propriedades)
        => new()
        {
            OrderId = LerXml(propriedades, "OrderID"),
            Sequence = LerXml(propriedades, "Sequence"),
            OrderOperation = LerXml(propriedades, "OrderOperation"),
            OrderOperationInternalId = LerXml(propriedades, "OrderOperationInternalID"),
            Plant = LerXml(propriedades, "Plant"),
            WorkCenter = LerXml(propriedades, "WorkCenter"),
            ConfirmationUnit = LerXml(propriedades, "ConfirmationUnit")
        };

    private static string LerTexto(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor)
            ? valor.ValueKind switch
            {
                JsonValueKind.String => valor.GetString() ?? string.Empty,
                JsonValueKind.Number => valor.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => string.Empty
            }
            : string.Empty;

    private static string LerXml(XElement propriedades, string nome)
        => propriedades.Element(DataServices + nome)?.Value?.Trim() ?? string.Empty;

    private static string? LerTextoNulo(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? (string.IsNullOrWhiteSpace(valor.GetString()) ? null : valor.GetString())
            : null;

    private static bool LerBooleano(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor)
           && valor.ValueKind is JsonValueKind.True or JsonValueKind.False
           && valor.GetBoolean();
}
