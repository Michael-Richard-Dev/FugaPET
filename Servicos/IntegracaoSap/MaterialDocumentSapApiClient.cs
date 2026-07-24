using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente da API SAP de Movimentos de Material (API_MATERIAL_DOCUMENT_SRV, OData V2, Basic Auth).
/// Cria documento de material (movimento 101) por POST em A_MaterialDocumentHeader, fazendo o fluxo
/// CSRF do SAP (GET X-CSRF-Token: Fetch -> POST com o token). HTTPS + allowlist de host obrigatorios;
/// nunca loga Authorization/senha/cookie/payload. Usa uma BaseUrl propria, independente do pedido.
///
/// Diagnostico: NUNCA propaga excecao tecnica do envio. Sempre devolve um resultado com a ETAPA
/// (CLIENTE_CONSTRUCAO, VALIDACAO_URL, AUTH_HEADER, MONTAGEM_REQUISICAO, CSRF_FETCH,
/// POST_DOCUMENTO_MATERIAL, PARSE_RESPOSTA), o status HTTP real (quando houver), reason phrase e um
/// trecho de resposta sanitizado e limitado. Excecoes tecnicas (inclusive antes do HTTP) viram
/// mensagem sanitizada com tipo + Message + inner, sem expor Authorization/senha/cookie/token/payload.
/// Falhas de construcao do cliente (URL/auth) sobem como <see cref="MaterialDocumentClienteException"/>.
/// </summary>
public sealed class MaterialDocumentSapApiClient
{
    private const string RecursoCriacao = "A_MaterialDocumentHeader";

    internal const string EtapaClienteConstrucao = "CLIENTE_CONSTRUCAO";
    internal const string EtapaValidacaoUrl = "VALIDACAO_URL";
    internal const string EtapaAuthHeader = "AUTH_HEADER";
    internal const string EtapaMontagemRequisicao = "MONTAGEM_REQUISICAO";
    internal const string EtapaCsrfFetch = "CSRF_FETCH";
    internal const string EtapaPost = "POST_DOCUMENTO_MATERIAL";
    internal const string EtapaParse = "PARSE_RESPOSTA";

    private const int LimiteMensagemExcecao = 500;
    private const int LimiteMensagemLog = 1000;

    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly AuthenticationHeaderValue _autorizacao;

    public MaterialDocumentSapApiClient(ConfiguracaoSap configuracao, HttpClient httpClient)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        // VALIDACAO_URL: HTTPS, allowlist de host, sem credencial na URL, sem query no base.
        try
        {
            _baseUri = ValidadorUrlSap.ValidarBaseUrl(
                configuracao.MaterialDocumentBaseUrl,
                configuracao.HostsPermitidos);
        }
        catch (Exception ex)
        {
            throw new MaterialDocumentClienteException(EtapaValidacaoUrl, ex);
        }

        // AUTH_HEADER: montagem do cabecalho Basic.
        try
        {
            string credenciais = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
            _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
        }
        catch (Exception ex)
        {
            throw new MaterialDocumentClienteException(EtapaAuthHeader, ex);
        }
    }

    /// <summary>
    /// Cria o documento de material (movimento 101). Faz GET de CSRF (com $top=1, sem baixar volume);
    /// se o SAP nao devolver o token, ABORTA sem POST. Em sucesso (2xx) devolve o numero/exercicio do
    /// documento. Nunca lanca por falha do envio: devolve sempre um resultado com etapa/status/mensagem
    /// sanitizada (inclusive para excecoes tecnicas antes do HTTP).
    /// </summary>
    public async Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterialAsync(
        MaterialDocumentSapRequest requisicao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        if (requisicao.Itens.Count == 0)
        {
            return Falha(EtapaPost, null, "Nenhum item elegivel para criar o documento de material.");
        }

        // ---- CSRF FETCH: montagem (pode lancar ArgumentException/UriFormat antes do HTTP) ----
        HttpRequestMessage fetch;
        try
        {
            fetch = CriarRequisicao(HttpMethod.Get, MontarUrlCsrfFetch());
            fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "Fetch");
        }
        catch (Exception ex)
        {
            return FalhaTecnica(ClassificarEtapaPreHttp(ex), ex, antesDoHttp: true);
        }

        string? token;
        using (fetch)
        {
            HttpResponseMessage respostaFetch;
            try
            {
                respostaFetch = await _httpClient.SendAsync(fetch, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                return FalhaRede(EtapaCsrfFetch, ex);
            }

            using (respostaFetch)
            {
                if (!respostaFetch.IsSuccessStatusCode)
                {
                    string corpoErro = await LerCorpoSeguroAsync(respostaFetch, cancellationToken);
                    return FalhaHttp(EtapaCsrfFetch, respostaFetch, corpoErro);
                }

                token = respostaFetch.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? valores)
                    ? valores.FirstOrDefault()
                    : null;
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Falha(EtapaCsrfFetch, null,
                "O SAP respondeu 2xx ao CSRF Fetch, mas nao retornou o token X-CSRF-Token. POST abortado.");
        }

        // ---- POST: montagem (pode lancar antes do HTTP) ----
        HttpRequestMessage post;
        try
        {
            string json = SerializarPayload(requisicao);
            post = CriarRequisicao(HttpMethod.Post, MontarUrlCriacao());
            post.Content = new StringContent(json, Encoding.UTF8, "application/json");
            post.Headers.TryAddWithoutValidation("X-CSRF-Token", token);
        }
        catch (Exception ex)
        {
            return FalhaTecnica(ClassificarEtapaPreHttp(ex), ex, antesDoHttp: true);
        }

        using (post)
        {
            HttpResponseMessage respostaPost;
            try
            {
                respostaPost = await _httpClient.SendAsync(post, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                return FalhaRede(EtapaPost, ex);
            }

            using (respostaPost)
            {
                string corpo = await LerCorpoSeguroAsync(respostaPost, cancellationToken);
                return respostaPost.IsSuccessStatusCode
                    ? InterpretarSucesso((int)respostaPost.StatusCode, corpo)
                    : FalhaHttp(EtapaPost, respostaPost, corpo);
            }
        }
    }

    /// <summary>Serializa o payload OData V2 (datas em /Date(ms)/, itens em to_MaterialDocumentItem.results).</summary>
    internal static string SerializarPayload(MaterialDocumentSapRequest requisicao)
    {
        List<Dictionary<string, object?>> itens = [];
        foreach (MaterialDocumentSapItemRequest item in requisicao.Itens)
        {
            Dictionary<string, object?> itemJson = new()
            {
                ["Material"] = item.Material,
                ["Plant"] = item.Plant,
                ["StorageLocation"] = item.StorageLocation,
                ["GoodsMovementType"] = item.GoodsMovementType,
                ["QuantityInEntryUnit"] = item.QuantityInEntryUnit,
                ["EntryUnit"] = item.EntryUnit
            };

            AdicionarSePreenchido(itemJson, "GoodsMovementRefDocType", item.GoodsMovementRefDocType);
            AdicionarSePreenchido(itemJson, "PurchaseOrder", item.PurchaseOrder);
            AdicionarSePreenchido(itemJson, "PurchaseOrderItem", item.PurchaseOrderItem);
            AdicionarSePreenchido(itemJson, "ManufacturingOrder", item.ManufacturingOrder);
            AdicionarSePreenchido(itemJson, "ManufacturingOrderItem", item.ManufacturingOrderItem);
            AdicionarSePreenchido(itemJson, "Batch", item.Batch);
            AdicionarDataSePreenchida(itemJson, "ManufactureDate", item.ManufactureDate);
            AdicionarDataSePreenchida(itemJson, "ShelfLifeExpirationDate", item.ShelfLifeExpirationDate);
            AdicionarSePreenchido(itemJson, "MaterialDocumentItemText", item.MaterialDocumentItemText);

            itens.Add(itemJson);
        }

        Dictionary<string, object?> payload = new()
        {
            ["GoodsMovementCode"] = requisicao.GoodsMovementCode,
            ["PostingDate"] = FormatarDataODataV2(requisicao.PostingDate),
            ["DocumentDate"] = FormatarDataODataV2(requisicao.DocumentDate),
            ["MaterialDocumentHeaderText"] = requisicao.MaterialDocumentHeaderText,
            ["to_MaterialDocumentItem"] = new Dictionary<string, object?> { ["results"] = itens }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static void AdicionarSePreenchido(Dictionary<string, object?> destino, string nome, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            destino[nome] = valor.Trim();
        }
    }
    private static void AdicionarDataSePreenchida(Dictionary<string, object?> destino, string nome, DateTime? valor)
    {
        if (valor.HasValue)
        {
            destino[nome] = FormatarDataODataV2(valor.Value);
        }
    }

    /// <summary>
    /// Formata uma DATA em OData V2 (/Date(unixMillis)/) na meia-noite UTC do dia, de forma SEGURA
    /// para qualquer DateTimeKind (Utc/Local/Unspecified). Nunca constroi DateTimeOffset com offset
    /// incompativel â€” evita o ArgumentException "The UTC Offset of the local dateTime parameter does
    /// not match the offset argument". PostingDate/DocumentDate sao datas; o horario/fuso de origem
    /// nao deslocam o dia.
    /// </summary>
    internal static string FormatarDataODataV2(DateTime data)
    {
        // Usa apenas a parte de data, rotulada como UTC: DateTimeOffset com offset zero coerente,
        // independente do Kind de origem (Local com offset -03:00 nao lanca mais).
        DateTime diaUtc = DateTime.SpecifyKind(data.Date, DateTimeKind.Utc);
        long ms = new DateTimeOffset(diaUtc).ToUnixTimeMilliseconds();
        return $"/Date({ms.ToString(CultureInfo.InvariantCulture)})/";
    }

    internal static ResultadoMaterialDocumentSap InterpretarSucesso(int statusHttp, string corpo)
    {
        string? documento = null;
        string? exercicio = null;
        List<string> itensDocumento = [];

        if (!string.IsNullOrWhiteSpace(corpo))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(corpo);
                if (doc.RootElement.TryGetProperty("d", out JsonElement d))
                {
                    documento = LerTextoNulo(d, "MaterialDocument");
                    exercicio = LerTextoNulo(d, "MaterialDocumentYear");
                    if (d.TryGetProperty("to_MaterialDocumentItem", out JsonElement nav)
                        && nav.TryGetProperty("results", out JsonElement results)
                        && results.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement itemDoc in results.EnumerateArray())
                        {
                            string? numero = LerTextoNulo(itemDoc, "MaterialDocumentItem");
                            if (numero is not null)
                            {
                                itensDocumento.Add(numero);
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Corpo nao parseavel: tratado abaixo como ausencia de rastreabilidade.
            }
        }

        // REGRA: HTTP 2xx so e sucesso LOCAL se houver MaterialDocument E MaterialDocumentYear. Sem
        // a rastreabilidade do documento, NAO confirmar (evita CONFIRMADO_SAP sem numero do documento).
        if (string.IsNullOrWhiteSpace(documento) || string.IsNullOrWhiteSpace(exercicio))
        {
            return new ResultadoMaterialDocumentSap
            {
                Sucesso = false,
                StatusHttp = statusHttp,
                Etapa = EtapaParse,
                MaterialDocument = documento,
                MaterialDocumentYear = exercicio,
                ItensDocumento = itensDocumento,
                MensagemSanitizada =
                    $"Etapa {EtapaParse}: SAP retornou sucesso HTTP {statusHttp}, mas sem "
                    + "MaterialDocument/MaterialDocumentYear na resposta."
            };
        }

        return new ResultadoMaterialDocumentSap
        {
            Sucesso = true,
            StatusHttp = statusHttp,
            MaterialDocument = documento,
            MaterialDocumentYear = exercicio,
            ItensDocumento = itensDocumento,
            MensagemSanitizada = $"Documento de material {documento}/{exercicio} criado no SAP."
        };
    }

    // Falha com resposta HTTP: status real + reason phrase + sintese do erro OData do SAP
    // (code, message, transactionid, timestamp e ate 5 errordetails), sanitizada e limitada a 1000.
    private static ResultadoMaterialDocumentSap FalhaHttp(
        string etapa, HttpResponseMessage resposta, string corpo)
    {
        int status = (int)resposta.StatusCode;
        string reason = string.IsNullOrWhiteSpace(resposta.ReasonPhrase)
            ? string.Empty
            : " " + resposta.ReasonPhrase;
        string detalhe = SintetizarErroSap(corpo);
        string? mensagemVidaUtil = TraduzirErroVidaUtilRemanescente(detalhe);
        if (mensagemVidaUtil is not null)
        {
            detalhe = mensagemVidaUtil + " " + detalhe;
        }

        if (RejeicaoUnidadeEntradaKg(detalhe))
        {
            detalhe =
                "O SAP rejeitou a entrada em KG para este item do pedido. "
                + "Verifique se o material/pedido possui conversao ou se o pedido deve ser criado em KG. "
                + detalhe;
        }

        string mensagem = $"Etapa {etapa}: HTTP {status}{reason}."
            + (string.IsNullOrEmpty(detalhe) ? string.Empty : " " + detalhe);
        if (mensagem.Length > LimiteMensagemLog)
        {
            mensagem = mensagem[..LimiteMensagemLog];
        }

        return new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = status,
            Etapa = etapa,
            MensagemSanitizada = mensagem
        };
    }


    private static string? TraduzirErroVidaUtilRemanescente(string detalhe)
    {
        if (string.IsNullOrWhiteSpace(detalhe)
            || !detalhe.Contains("12/007", StringComparison.OrdinalIgnoreCase)
            || !detalhe.Contains("shelf life", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string exigencia = ExtrairPrimeiroGrupo(detalhe, @"current item \((\d+) Day\)");
        string diferenca = ExtrairPrimeiroGrupo(detalhe, @"Shortfall of (\d+) days?");
        string complementoExigencia = string.IsNullOrWhiteSpace(exigencia)
            ? string.Empty
            : $" Exigência SAP: {exigencia} dias.";
        string complementoDiferenca = string.IsNullOrWhiteSpace(diferenca)
            ? string.Empty
            : $" Diferença encontrada: {diferenca} dia(s).";

        return "O SAP rejeitou o lançamento porque a validade remanescente do lote é menor que a exigida para o material."
            + complementoExigencia
            + complementoDiferenca
            + " Confira o lote, a data de fabricação, a data de validade e o cadastro de vida útil do material no SAP.";
    }

    private static string ExtrairPrimeiroGrupo(string texto, string padrao)
    {
        Match match = Regex.Match(texto, padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }
    private static bool RejeicaoUnidadeEntradaKg(string detalhe)
    {
        if (string.IsNullOrWhiteSpace(detalhe))
        {
            return false;
        }

        return detalhe.Contains("KG", StringComparison.OrdinalIgnoreCase)
               && (detalhe.Contains("unit", StringComparison.OrdinalIgnoreCase)
                   || detalhe.Contains("unidade", StringComparison.OrdinalIgnoreCase)
                   || detalhe.Contains("uom", StringComparison.OrdinalIgnoreCase)
                   || detalhe.Contains("convers", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Sintetiza o corpo de erro OData V2 do SAP em uma linha util para diagnostico:
    /// code, message.value, innererror.transactionid/timestamp e ate 5 errordetails. Sanitizada
    /// (sem segredo) e neutralizando marcadores para nao ser apagada pelo sanitizador do log.
    /// Para corpo nao-OData, devolve um trecho sanitizado.
    /// </summary>
    internal static string SintetizarErroSap(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(corpo);
            if (!doc.RootElement.TryGetProperty("error", out JsonElement erro)
                || erro.ValueKind != JsonValueKind.Object)
            {
                return NeutralizarParaLog(Compactar(corpo));
            }

            List<string> partes = [];
            string? code = LerTextoNulo(erro, "code");
            if (code is not null)
            {
                partes.Add($"SAP code={code}");
            }

            if (erro.TryGetProperty("message", out JsonElement mensagemSap))
            {
                string? valor = mensagemSap.ValueKind == JsonValueKind.Object
                    ? LerTextoNulo(mensagemSap, "value")
                    : (mensagemSap.ValueKind == JsonValueKind.String ? mensagemSap.GetString() : null);
                if (!string.IsNullOrWhiteSpace(valor))
                {
                    partes.Add($"msg={valor!.Trim()}");
                }
            }

            if (erro.TryGetProperty("innererror", out JsonElement inner) && inner.ValueKind == JsonValueKind.Object)
            {
                string? txn = LerTextoNulo(inner, "transactionid");
                if (txn is not null)
                {
                    partes.Add($"txn={txn}");
                }

                string? timestamp = LerTextoNulo(inner, "timestamp");
                if (timestamp is not null)
                {
                    partes.Add($"ts={timestamp}");
                }

                if (inner.TryGetProperty("errordetails", out JsonElement detalhesSap)
                    && detalhesSap.ValueKind == JsonValueKind.Array)
                {
                    List<string> detalhes = [];
                    foreach (JsonElement item in detalhesSap.EnumerateArray())
                    {
                        if (detalhes.Count >= 5)
                        {
                            break;
                        }

                        string? sev = LerTextoNulo(item, "severity");
                        string? dcode = LerTextoNulo(item, "code");
                        string? dmsg = LerTextoNulo(item, "message");
                        string linha = string.Join(' ',
                            new[] { sev, dcode, dmsg }.Where(parte => !string.IsNullOrWhiteSpace(parte)));
                        if (linha.Length > 0)
                        {
                            detalhes.Add(linha);
                        }
                    }

                    if (detalhes.Count > 0)
                    {
                        partes.Add("detalhes=[" + string.Join(" | ", detalhes) + "]");
                    }
                }
            }

            string sintese = partes.Count > 0 ? string.Join(' ', partes) : Compactar(corpo);
            return NeutralizarParaLog(sintese);
        }
        catch (JsonException)
        {
            return NeutralizarParaLog(Compactar(corpo));
        }
    }

    // Redige segredos eventuais e NEUTRALIZA palavras-marcador (authorization/cookie/senha/...), para
    // que o sanitizador do log (que zera a mensagem inteira ao achar um marcador) nao descarte a
    // sintese util do erro SAP. Limita ao tamanho da coluna mensagem_tecnica_sanitizada (1000).
    private static string NeutralizarParaLog(string texto)
    {
        string t = Regex.Replace(texto, @"(?i)\b(Basic|Bearer)\s+\S+", "$1 ***");
        t = Regex.Replace(
            t,
            @"(?i)\b(authorization|cookie|set-cookie|x-csrf-token|password|senha|token)\b\s*[:=]\s*\S+",
            "$1=***");
        // Marcadores "soltos" trocados por equivalentes neutros (mantem legibilidade, sem segredo).
        t = Regex.Replace(t, "(?i)authorization", "autorizacao");
        t = Regex.Replace(t, "(?i)set-cookie", "sessao");
        t = Regex.Replace(t, "(?i)cookie", "sessao");
        t = Regex.Replace(t, "(?i)password", "credencial");
        t = Regex.Replace(t, "(?i)senha", "credencial");
        t = Regex.Replace(t, "(?i)basic ", "esquema ");
        t = string.Join(' ', t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length <= LimiteMensagemLog ? t : t[..LimiteMensagemLog];
    }

    private static string Compactar(string texto)
        => string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Descricao SANITIZADA do payload enviado (somente campos funcionais, sem segredo): cabecalho +
    /// itens. Para diagnostico controlado no log de integracao. Limitada a 1000 caracteres.
    /// </summary>
    internal static string DescreverPayloadSanitizado(MaterialDocumentSapRequest requisicao)
    {
        StringBuilder sb = new();
        sb.Append("Payload 101: GoodsMovementCode=").Append(requisicao.GoodsMovementCode)
          .Append(" PostingDate=").Append(FormatarDataODataV2(requisicao.PostingDate))
          .Append(" DocumentDate=").Append(FormatarDataODataV2(requisicao.DocumentDate))
          .Append(" MaterialDocumentHeaderText='").Append(requisicao.MaterialDocumentHeaderText).Append('\'');

        int indice = 0;
        foreach (MaterialDocumentSapItemRequest item in requisicao.Itens)
        {
            if (indice >= 10)
            {
                sb.Append(" | ...");
                break;
            }

            sb.Append(" | item[").Append(indice).Append("] Material=").Append(item.Material)
              .Append(" Plant=").Append(item.Plant)
              .Append(" StorageLocation=").Append(item.StorageLocation)
              .Append(" GoodsMovementType=").Append(item.GoodsMovementType)
              .Append(" GoodsMovementRefDocType=").Append(item.GoodsMovementRefDocType)
              .Append(" QuantityInEntryUnit=").Append(item.QuantityInEntryUnit)
              .Append(" EntryUnit=").Append(item.EntryUnit)
              .Append(" PurchaseOrder=").Append(item.PurchaseOrder)
              .Append(" PurchaseOrderItem=").Append(item.PurchaseOrderItem)
              .Append(" ManufacturingOrder=").Append(item.ManufacturingOrder)
              .Append(" ManufacturingOrderItem=").Append(item.ManufacturingOrderItem)
              .Append(" Batch=").Append(item.Batch)
              .Append(" ManufactureDate=").Append(item.ManufactureDate.HasValue ? FormatarDataODataV2(item.ManufactureDate.Value) : null)
              .Append(" ShelfLifeExpirationDate=").Append(item.ShelfLifeExpirationDate.HasValue ? FormatarDataODataV2(item.ShelfLifeExpirationDate.Value) : null);
            indice++;
        }

        string texto = sb.ToString();
        return texto.Length <= LimiteMensagemLog ? texto : texto[..LimiteMensagemLog];
    }

    // Falha SEM resposta HTTP (rede/TLS/timeout): status null, classificacao sanitizada.
    private static ResultadoMaterialDocumentSap FalhaRede(string etapa, Exception ex)
        => new()
        {
            Sucesso = false,
            StatusHttp = null,
            Etapa = etapa,
            MensagemSanitizada =
                $"Etapa {etapa}: falha de {ClassificarFalhaRede(ex)} ao contatar o SAP (sem resposta HTTP)."
        };

    // Falha tecnica (excecao C#, inclusive antes do HTTP): tipo + Message + inner, tudo sanitizado.
    private ResultadoMaterialDocumentSap FalhaTecnica(string etapa, Exception ex, bool antesDoHttp)
    {
        string detalhe = SanitizarExcecaoTecnica(
            ex, _autorizacao.Parameter, _configuracao.Usuario, _configuracao.Senha);
        string prefixo = antesDoHttp ? $"Etapa {etapa} (antes do HTTP): " : $"Etapa {etapa}: ";
        return new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = null,
            Etapa = etapa,
            MensagemSanitizada = prefixo + detalhe
        };
    }

    private static string ClassificarFalhaRede(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return "TIMEOUT";
        }

        for (Exception? atual = ex; atual is not null; atual = atual.InnerException)
        {
            if (atual is AuthenticationException)
            {
                return "TLS";
            }
        }

        return "REDE";
    }

    // Erros de URL/allowlist/base path -> VALIDACAO_URL; demais erros de montagem -> MONTAGEM_REQUISICAO.
    private static string ClassificarEtapaPreHttp(Exception ex)
        => ex is UriFormatException or InvalidOperationException ? EtapaValidacaoUrl : EtapaMontagemRequisicao;

    private static ResultadoMaterialDocumentSap Falha(string etapa, int? statusHttp, string mensagem)
        => new() { Sucesso = false, StatusHttp = statusHttp, Etapa = etapa, MensagemSanitizada = mensagem };

    private static async Task<string> LerCorpoSeguroAsync(
        HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            return await resposta.Content.ReadAsStringAsync(cancellationToken);
        }
        catch
        {
            // A leitura do corpo de diagnostico nunca pode mascarar o status HTTP ja obtido.
            return string.Empty;
        }
    }

    /// <summary>
    /// Texto tecnico de uma excecao para diagnostico: "Tipo: Message [| Inner Tipo: Message]",
    /// redigindo segredos (usuario/senha/credencial/token e cabecalhos Basic/Authorization/Cookie) e
    /// limitado a 500 caracteres. NAO esconde a Message util (ex.: parametro invalido, URI invalida).
    /// </summary>
    internal static string SanitizarExcecaoTecnica(Exception excecao, params string?[] segredos)
    {
        StringBuilder sb = new();
        sb.Append(excecao.GetType().Name).Append(": ")
          .Append(SanitizarMensagemExcecao(excecao.Message, segredos));
        if (excecao.InnerException is Exception inner)
        {
            sb.Append(" | Inner ").Append(inner.GetType().Name).Append(": ")
              .Append(SanitizarMensagemExcecao(inner.Message, segredos));
        }

        string texto = sb.ToString();
        return texto.Length <= LimiteMensagemExcecao ? texto : texto[..LimiteMensagemExcecao];
    }

    internal static string SanitizarMensagemExcecao(string? mensagem, params string?[] segredos)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
        {
            return "(sem mensagem)";
        }

        string texto = mensagem;
        foreach (string? segredo in segredos)
        {
            if (!string.IsNullOrEmpty(segredo))
            {
                texto = texto.Replace(segredo, "***", StringComparison.Ordinal);
            }
        }

        texto = Regex.Replace(texto, @"(?i)\b(Basic|Bearer)\s+\S+", "$1 ***");
        texto = Regex.Replace(
            texto,
            @"(?i)\b(authorization|cookie|set-cookie|x-csrf-token|password|senha|token)\b\s*[:=]\s*\S+",
            "$1=***");
        texto = string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return texto.Length <= LimiteMensagemExcecao ? texto : texto[..LimiteMensagemExcecao];
    }

    private static string? LerTextoNulo(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor)
            && valor.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(valor.GetString())
                ? valor.GetString()!.Trim()
                : null;

    // CSRF Fetch com $top=1 para o SAP devolver o token sem materializar grande volume de registros.
    private Uri MontarUrlCsrfFetch()
    {
        string url = $"{_baseUri.AbsoluteUri}{RecursoCriacao}?$top=1";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            url = $"{url}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return ValidarDestino(new Uri(url, UriKind.Absolute));
    }

    private Uri MontarUrlCriacao()
    {
        string url = $"{_baseUri.AbsoluteUri}{RecursoCriacao}";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            url = $"{url}?sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return ValidarDestino(new Uri(url, UriKind.Absolute));
    }

    private HttpRequestMessage CriarRequisicao(HttpMethod metodo, Uri destino)
    {
        HttpRequestMessage requisicao = new(metodo, ValidarDestino(destino));
        requisicao.Headers.Authorization = _autorizacao;
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return requisicao;
    }

    private Uri ValidarDestino(Uri destino)
        => ValidadorUrlSap.ValidarDestino(destino, _baseUri, _configuracao.HostsPermitidos);
}

/// <summary>
/// Falha tecnica na construcao/preparacao do cliente de Material Document (URL/auth), antes de
/// qualquer chamada HTTP. Carrega a ETAPA para diagnostico sanitizado no servico.
/// </summary>
public sealed class MaterialDocumentClienteException : Exception
{
    public string Etapa { get; }

    public MaterialDocumentClienteException(string etapa, Exception innerException)
        : base($"Falha tecnica na etapa {etapa} do cliente de Material Document.", innerException)
        => Etapa = etapa;
}


