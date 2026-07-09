using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente da API SAP de Pedido de Compra (API_PURCHASEORDER_2, OData V4, Basic Auth).
/// Consulta a COLECAO de pedidos (somente cabecalho) para carga no cache local.
/// </summary>
public sealed class PedidoCompraSapApiClient
{
    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly AuthenticationHeaderValue _autorizacao;

    public PedidoCompraSapApiClient(ConfiguracaoSap configuracao, HttpClient httpClient)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = ValidadorUrlSap.ValidarBaseUrl(configuracao);

        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
        _maxPaginas = MaxPaginasPadrao;
    }

    // Teto de paginas como protecao contra loop/volume anormal (cada pagina ~ centenas de itens).
    private const int MaxPaginasPadrao = 1000;
    private readonly int _maxPaginas;

    internal PedidoCompraSapApiClient(
        ConfiguracaoSap configuracao,
        HttpClient httpClient,
        int maxPaginas)
        : this(configuracao, httpClient)
    {
        _maxPaginas = maxPaginas > 0
            ? maxPaginas
            : throw new ArgumentOutOfRangeException(nameof(maxPaginas));
    }

    internal async Task<IReadOnlyList<PedidoCompraSap>> ConsultarPedidosAsync(
        CancellationToken cancellationToken = default)
        => (await ConsultarCargaCompletaAsync(cancellationToken)).Pedidos;

    internal async Task<ResultadoConsultaPedidosSap> ConsultarCargaCompletaAsync(
        CancellationToken cancellationToken = default)
    {
        List<PedidoCompraSap> todos = [];
        Uri? url = MontarUrlInicial();
        int paginas = 0;
        int itens = 0;

        // Segue o server-driven paging do OData (@odata.nextLink) ate trazer TODOS os pedidos.
        while (url is not null && paginas < _maxPaginas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using HttpRequestMessage requisicao = CriarRequisicao(HttpMethod.Get, url);
            using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
            resposta.EnsureSuccessStatusCode();

            string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            (IReadOnlyList<PedidoCompraSap> pagina, string? proximaPagina) = MapearPagina(corpo);
            todos.AddRange(pagina);
            itens += pagina.Sum(pedido => pedido.Itens.Count);

            url = ResolverNextLink(proximaPagina);
            paginas++;
        }

        if (url is not null)
        {
            throw new SincronizacaoSapIncompletaException(paginas, todos.Count, itens);
        }

        return new ResultadoConsultaPedidosSap(todos, paginas, itens);
    }

    public async Task<PedidoCompraSap?> ConsultarPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return null;
        }

        Uri url = MontarUrlPedido(numeroPedido);
        using HttpRequestMessage requisicao = CriarRequisicao(HttpMethod.Get, url);
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        if (resposta.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        resposta.EnsureSuccessStatusCode();
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return MapearPedidoEspecifico(corpo);
    }

    /// <summary>
    /// PATCH no item do pedido de compra alterando apenas o peso liquido (ItemNetWeight)
    /// e o peso bruto (ItemGrossWeight). Faz o fluxo CSRF do SAP: GET com X-CSRF-Token: Fetch
    /// para obter token + cookies e, em seguida, o PATCH. Quando o SAP fornece ETag,
    /// envia If-Match com o valor real; quando o recurso nao oferece ETag, nao envia
    /// If-Match e nunca usa o curinga "*".
    /// </summary>
    public async Task<ResultadoPatchSap> AtualizarPesoItemAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        string unidadePeso = "KG",
        CancellationToken cancellationToken = default)
    {
        Uri url = MontarUrlItem(numeroPedido, numeroItem);

        // 1) Busca o CSRF token (e cookies de sessao ficam no handler do HttpClient).
        string? token;
        EntityTagHeaderValue? etag;
        using (HttpRequestMessage fetch = CriarRequisicao(HttpMethod.Get, url))
        {
            fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "Fetch");
            using HttpResponseMessage respostaFetch = await _httpClient.SendAsync(fetch, cancellationToken);
            if (SessaoInvalida(respostaFetch.StatusCode))
            {
                return new ResultadoPatchSap(
                    false,
                    (int)respostaFetch.StatusCode,
                    "Sessao SAP invalida ou expirada.");
            }

            if (!respostaFetch.IsSuccessStatusCode)
            {
                return new ResultadoPatchSap(false, (int)respostaFetch.StatusCode,
                    $"O SAP recusou a consulta do item (HTTP {(int)respostaFetch.StatusCode}).");
            }

            token = respostaFetch.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? valores)
                ? valores.FirstOrDefault()
                : null;
            etag = respostaFetch.Headers.ETag;

            if (etag is null)
            {
                string corpoFetch = await respostaFetch.Content.ReadAsStringAsync(cancellationToken);
                etag = ExtrairEtagOData(corpoFetch);
            }
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new ResultadoPatchSap(
                false,
                null,
                "O SAP nao retornou o token CSRF necessario para a operacao.");
        }

        // 2) PATCH apenas com os campos de peso. O SAP exige a unidade (ItemWeightUnit)
        //    sempre que o peso liquido/bruto e informado.
        string json = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["ItemNetWeight"] = pesoLiquido,
            ["ItemGrossWeight"] = pesoBruto,
            ["ItemWeightUnit"] = string.IsNullOrWhiteSpace(unidadePeso) ? "KG" : unidadePeso.Trim()
        });

        using HttpRequestMessage patch = CriarRequisicao(HttpMethod.Patch, url);
        patch.Content = new StringContent(json, Encoding.UTF8, "application/json");
        patch.Headers.TryAddWithoutValidation("X-CSRF-Token", token);
        if (etag is not null)
        {
            patch.Headers.IfMatch.Add(new EntityTagHeaderValue(etag.Tag, etag.IsWeak));
        }

        using HttpResponseMessage resposta = await _httpClient.SendAsync(patch, cancellationToken);
        if (SessaoInvalida(resposta.StatusCode))
        {
            return new ResultadoPatchSap(
                false,
                (int)resposta.StatusCode,
                "Sessao SAP invalida ou expirada.");
        }

        if (resposta.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
        {
            return new ResultadoPatchSap(
                false,
                (int)resposta.StatusCode,
                "O item foi alterado no SAP por outro processo. Recarregue os dados antes de tentar novamente.");
        }

        if (resposta.IsSuccessStatusCode)
        {
            return new ResultadoPatchSap(true, (int)resposta.StatusCode, "Peso atualizado no SAP.");
        }

        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        return new ResultadoPatchSap(false, (int)resposta.StatusCode, ExtrairMensagemErroSap(corpo));
    }

    private static bool SessaoInvalida(System.Net.HttpStatusCode statusCode)
        => statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden;

    private static EntityTagHeaderValue? ExtrairEtagOData(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return null;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(corpo);
            if (documento.RootElement.TryGetProperty("@odata.etag", out JsonElement etag)
                && etag.ValueKind == JsonValueKind.String
                && EntityTagHeaderValue.TryParse(etag.GetString(), out EntityTagHeaderValue? valor))
            {
                return valor;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private Uri MontarUrlItem(string numeroPedido, string numeroItem)
    {
        // OData escapa aspas simples duplicando-as.
        string pedido = numeroPedido.Trim().Replace("'", "''");
        string item = numeroItem.Trim().Replace("'", "''");
        string chave = $"PurchaseOrderItem(PurchaseOrder='{pedido}',PurchaseOrderItem='{item}')";
        string url = $"{_baseUri.AbsoluteUri}{chave}";

        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            url = $"{url}?sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return ValidarDestino(new Uri(url, UriKind.Absolute));
    }

    private Uri MontarUrlPedido(string numeroPedido)
    {
        string pedido = numeroPedido.Trim().Replace("'", "''");
        string query = $"{SelectCabecalho}&{MontarExpandItens()}";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}&{query}";
        }

        return ValidarDestino(new Uri(
            $"{_baseUri.AbsoluteUri}PurchaseOrder('{pedido}')?{query}",
            UriKind.Absolute));
    }

    // Tenta extrair a mensagem amigavel do corpo de erro OData ({ "error": { "message": "..." } }).
    private static string ExtrairMensagemErroSap(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return "O SAP recusou a alteracao do peso.";
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(corpo);
            if (documento.RootElement.TryGetProperty("error", out JsonElement erro)
                && erro.TryGetProperty("message", out JsonElement mensagem))
            {
                string? texto = mensagem.ValueKind == JsonValueKind.Object && mensagem.TryGetProperty("value", out JsonElement valor)
                    ? valor.GetString()
                    : mensagem.GetString();
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    string? mensagemSanitizada =
                        LogIntegracaoSapServico.SanitizarMensagem(texto);
                    return string.Equals(
                            mensagemSanitizada,
                            texto.Trim(),
                            StringComparison.Ordinal)
                        ? mensagemSanitizada!
                        : "O SAP recusou a alteracao do peso.";
                }
            }
        }
        catch
        {
            // Resposta tecnica nao deve ser propagada ao usuario.
        }

        return "O SAP recusou a alteracao do peso.";
    }

    // Cabecalho inclui PurchasingGroup (escopo Jales). Plant e IsCompletelyDelivered sao de ITEM:
    // filtramos tambem dentro do $expand para reduzir volume no SAP; a guarda final segue em C#.
    // Tarefa Entrada 23.1: inclui os campos de aprovacao/liberacao do cabecalho (PurchasingProcessingStatus,
    // ReleaseIsNotCompleted, PurchasingCompletenessStatus). ReleaseIsNotCompleted pode nao existir no ambiente.
    private const string SelectCabecalho = "$select=PurchaseOrder,Supplier,PurchaseOrderDate,DocumentCurrency,PurchaseOrderType,PurchasingGroup,IncotermsClassification,IncotermsTransferLocation,IncotermsLocation1,PurchasingProcessingStatus,ReleaseIsNotCompleted,PurchasingCompletenessStatus";

    private Uri MontarUrlInicial()
    {
        string filtro = MontarFiltroEscopoJales();
        string query = $"{filtro}&{SelectCabecalho}&{MontarExpandItens()}";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}&{query}";
        }

        return ValidarDestino(new Uri($"{_baseUri.AbsoluteUri}PurchaseOrder?{query}", UriKind.Absolute));
    }

    // $filter OData de CABECALHO do escopo Jales (fonte unica em EscopoPedidoSapJales). Espacos
    // percent-encoded. A selecao dos itens elegiveis (centro/entrega) e concluida em C#.
    private static string MontarFiltroEscopoJales()
        => "$filter=" + EscopoPedidoSapJales.ExpressaoFiltroPedido().Replace(" ", "%20");

    private static string MontarExpandItens()
    {
        string filtroItem = Uri.EscapeDataString(EscopoPedidoSapJales.ExpressaoFiltroItem());

        return "$expand=_PurchaseOrderItem("
            + $"$filter={filtroItem};"
            + "$select=PurchaseOrderItem,Material,PurchaseOrderItemText,OrderQuantity,PurchaseOrderQuantityUnit,ItemNetWeight,Plant,StorageLocation,MaterialGroup,IsCompletelyDelivered"
            + ")";
    }

    // O @odata.nextLink pode vir absoluto (http...) ou relativo a raiz do servico (.../0001).
    private Uri? ResolverNextLink(string? nextLink)
    {
        if (string.IsNullOrWhiteSpace(nextLink))
        {
            return null;
        }

        try
        {
            Uri destino = Uri.TryCreate(nextLink, UriKind.Absolute, out Uri? absoluta)
                ? absoluta
                : new Uri(_baseUri, nextLink);

            return ValidarDestino(destino);
        }
        catch (Exception ex) when (ex is UriFormatException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "O nextLink retornado pelo SAP foi rejeitado por seguranca.");
        }
    }

    private HttpRequestMessage CriarRequisicao(HttpMethod metodo, Uri destino)
    {
        Uri destinoValidado = ValidarDestino(destino);
        HttpRequestMessage requisicao = new(metodo, destinoValidado);
        requisicao.Headers.Authorization = _autorizacao;
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return requisicao;
    }

    private Uri ValidarDestino(Uri destino)
        => ValidadorUrlSap.ValidarDestino(destino, _baseUri, _configuracao.HostsPermitidos);

    /// <summary>
    /// Mapeia uma pagina OData (<c>{ "value": [...], "@odata.nextLink": "..." }</c>):
    /// devolve os pedidos e o link da proxima pagina (null quando e a ultima).
    /// Internal para permitir teste sem chamada HTTP.
    /// </summary>
    internal static (IReadOnlyList<PedidoCompraSap> Pedidos, string? ProximaPagina) MapearPagina(string json)
    {
        List<PedidoCompraSap> pedidos = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return (pedidos, null);
        }

        using JsonDocument documento = JsonDocument.Parse(json);
        JsonElement raiz = documento.RootElement;

        if (raiz.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                string numero = LerTexto(item, "PurchaseOrder");
                if (string.IsNullOrWhiteSpace(numero))
                {
                    continue;
                }

                pedidos.Add(new PedidoCompraSap
                {
                    Numero = numero.Trim(),
                    FornecedorCodigoSap = LerTextoNulo(item, "Supplier"),
                    DataPedido = LerData(item, "PurchaseOrderDate"),
                    Moeda = LerTextoNulo(item, "DocumentCurrency"),
                    TipoPedido = LimitarTexto(LerTextoNulo(item, "PurchaseOrderType"), 4),
                    IncotermsClassification = LerTextoNulo(item, "IncotermsClassification"),
                    IncotermsTransferLocation = LerTextoNulo(item, "IncotermsTransferLocation"),
                    IncotermsLocation1 = LerTextoNulo(item, "IncotermsLocation1"),
                    // SAP nao expoe um status de cabecalho neste $select; nulo ate confirmar o campo.
                    Status = LerTextoNulo(item, "PurchasingDocumentStatus"),
                    // Tarefa Entrada 23.1: aprovacao/liberacao do cabecalho.
                    StatusProcessamentoCompraSap = LerTexto(item, "PurchasingProcessingStatus").Trim(),
                    LiberacaoNaoConcluidaSap = LerBooleano(item, "ReleaseIsNotCompleted"),
                    StatusCompletudeCompraSap = LerTexto(item, "PurchasingCompletenessStatus").Trim(),
                    GrupoCompra = LerTextoNulo(item, "PurchasingGroup"),
                    PayloadOriginalJson = item.GetRawText(),
                    Itens = MapearItens(item)
                });
            }
        }

        string? proximaPagina =
            raiz.TryGetProperty("@odata.nextLink", out JsonElement next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;

        return (pedidos, proximaPagina);
    }

    /// <summary>Mapeia somente os pedidos de um corpo OData (sem paginacao).</summary>
    internal static IReadOnlyList<PedidoCompraSap> MapearColecao(string json) => MapearPagina(json).Pedidos;

    internal static PedidoCompraSap? MapearPedidoEspecifico(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using JsonDocument documento = JsonDocument.Parse(json);
        JsonElement pedido = documento.RootElement;
        string numero = LerTexto(pedido, "PurchaseOrder");
        if (string.IsNullOrWhiteSpace(numero))
        {
            return null;
        }

        return new PedidoCompraSap
        {
            Numero = numero.Trim(),
            FornecedorCodigoSap = LerTextoNulo(pedido, "Supplier"),
            DataPedido = LerData(pedido, "PurchaseOrderDate"),
            Moeda = LerTextoNulo(pedido, "DocumentCurrency"),
            TipoPedido = LimitarTexto(LerTextoNulo(pedido, "PurchaseOrderType"), 4),
            IncotermsClassification = LerTextoNulo(pedido, "IncotermsClassification"),
            IncotermsTransferLocation = LerTextoNulo(pedido, "IncotermsTransferLocation"),
            IncotermsLocation1 = LerTextoNulo(pedido, "IncotermsLocation1"),
            Status = LerTextoNulo(pedido, "PurchasingDocumentStatus"),
            // Tarefa Entrada 23.1: aprovacao/liberacao do cabecalho.
            StatusProcessamentoCompraSap = LerTexto(pedido, "PurchasingProcessingStatus").Trim(),
            LiberacaoNaoConcluidaSap = LerBooleano(pedido, "ReleaseIsNotCompleted"),
            StatusCompletudeCompraSap = LerTexto(pedido, "PurchasingCompletenessStatus").Trim(),
            GrupoCompra = LerTextoNulo(pedido, "PurchasingGroup"),
            PayloadOriginalJson = pedido.GetRawText(),
            Itens = MapearItens(pedido)
        };
    }

    private static IReadOnlyList<PedidoCompraSapItem> MapearItens(JsonElement pedido)
    {
        List<PedidoCompraSapItem> itens = [];
        if (!pedido.TryGetProperty("_PurchaseOrderItem", out JsonElement itensSap)
            || itensSap.ValueKind != JsonValueKind.Array)
        {
            return itens;
        }

        foreach (JsonElement item in itensSap.EnumerateArray())
        {
            string numeroItem = LerTexto(item, "PurchaseOrderItem");
            if (string.IsNullOrWhiteSpace(numeroItem))
            {
                continue;
            }

            itens.Add(new PedidoCompraSapItem
            {
                NumeroItem = numeroItem.Trim(),
                CodigoMaterial = LerTextoNulo(item, "Material"),
                Descricao = LerTextoNulo(item, "PurchaseOrderItemText"),
                Quantidade = LerDecimal(item, "OrderQuantity"),
                UnidadeMedida = LerTextoNulo(item, "PurchaseOrderQuantityUnit"),
                PesoItem = LerDecimal(item, "ItemNetWeight"),
                Centro = LerTextoNulo(item, "Plant"),
                Deposito = LerTextoNulo(item, "StorageLocation"),
                GrupoMaterial = LerTextoNulo(item, "MaterialGroup"),
                CompletamenteEntregue = LerBooleano(item, "IsCompletelyDelivered"),
                PayloadOriginalJson = item.GetRawText()
            });
        }

        return itens;
    }

    private static string LerTexto(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? string.Empty
            : string.Empty;

    private static string? LerTextoNulo(JsonElement elemento, string propriedade)
    {
        string texto = LerTexto(elemento, propriedade);
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    // IsCompletelyDelivered vem como booleano JSON (true/false); aceita string "true"/"false" por seguranca.
    private static bool? LerBooleano(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(valor.GetString(), out bool resultado) ? resultado : null,
            _ => null
        };
    }

    // OrderQuantity vem como numero JSON (ex.: 2.000); aceita tambem string por seguranca.
    private static decimal? LerDecimal(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.Number when valor.TryGetDecimal(out decimal numero) => numero,
            JsonValueKind.String when decimal.TryParse(
                valor.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal texto) => texto,
            _ => null
        };
    }

    private static string? LimitarTexto(string? texto, int tamanhoMaximo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        string valor = texto.Trim();
        return valor.Length <= tamanhoMaximo ? valor : valor[..tamanhoMaximo];
    }

    private static DateOnly? LerData(JsonElement elemento, string propriedade)
    {
        string texto = LerTexto(elemento, propriedade);
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        // SAP envia "YYYY-MM-DD" (ou ISO com hora); aceita ambos.
        if (DateOnly.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly data))
        {
            return data;
        }

        if (DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dataHora))
        {
            return DateOnly.FromDateTime(dataHora);
        }

        return null;
    }
}
