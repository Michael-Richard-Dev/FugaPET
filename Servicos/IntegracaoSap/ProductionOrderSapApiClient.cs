using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente da API SAP de Ordens de Producao (API_PRODUCTION_ORDER_2_SRV, OData V2, Basic Auth).
/// Apenas GET da entidade A_ProductionOrder_2('NUMERO') com $expand de componentes, operacoes e itens.
/// Sem POST/PATCH e sem CSRF (GET nao exige token). Base URL/host validados pelo <see cref="ValidadorUrlSap"/>.
/// </summary>
public sealed class ProductionOrderSapApiClient
{
    internal const string EtapaExpand = "GET_EXPAND";
    internal const string EtapaExpandSemComponentes = "GET_EXPAND_SEM_COMPONENTES";
    internal const string EtapaFallback = "GET_FALLBACK";
    internal const string EtapaParse = "PARSE_RESPOSTA";
    private const int LimiteTrechoResposta = 300;

    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly AuthenticationHeaderValue _autorizacao;
    private readonly Action<string> _registrarDiagnostico;

    private const string Expand =
        "$expand=to_ProductionOrderComponent,to_ProductionOrderOperation,to_ProductionOrderItem";

    public ProductionOrderSapApiClient(
        ConfiguracaoSap configuracao,
        HttpClient httpClient,
        Action<string>? registrarDiagnostico = null)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUri = ValidadorUrlSap.ValidarBaseUrl(
            configuracao.ProductionOrderBaseUrlEfetiva,
            configuracao.HostsPermitidos);

        string credenciais = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
        _registrarDiagnostico = registrarDiagnostico ?? (_ => { });
    }

    /// <summary>
    /// GET da OP. Tenta primeiro com $expand (componentes/operacoes/itens). Se o SAP recusar o expand
    /// (ou a resposta 200 nao for mapeavel), registra diagnostico SANITIZADO e cai no FALLBACK de
    /// chamadas separadas. Retorna null em HTTP 404; lanca <see cref="ProductionOrderSapConsultaException"/>
    /// quando nem o fallback consegue (tratado no servico).
    /// </summary>
    public async Task<OrdemProducaoSap?> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroOrdem))
        {
            return null;
        }

        Uri url = MontarUrlOrdem(numeroOrdem);
        using HttpRequestMessage requisicao = CriarRequisicao(HttpMethod.Get, url);
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (resposta.IsSuccessStatusCode)
        {
            OrdemProducaoSap? ordem = MapearOrdem(corpo);

            // Expand veio com componentes: ok.
            if (ordem is not null && ordem.Componentes.Count > 0)
            {
                return ordem;
            }

            // OP valida porem SEM componentes no expand: a Tela de Consumo depende dos componentes.
            // Tenta o fallback de chamadas separadas antes de concluir.
            if (ordem is not null)
            {
                RegistrarDiagnostico(
                    EtapaExpandSemComponentes,
                    url,
                    resposta,
                    "Expand retornou OP sem componentes. Executando fallback.");
                return await ConsultarSeparadoAsync(numeroOrdem, cancellationToken);
            }

            // 200 porem sem corpo mapeavel: pode ser expand vazio/diferente — tenta fallback.
            RegistrarDiagnostico(EtapaParse, url, resposta, corpo);
            return await ConsultarSeparadoAsync(numeroOrdem, cancellationToken);
        }

        // SAP recusou o expand: diagnostico + fallback de chamadas separadas.
        RegistrarDiagnostico(EtapaExpand, url, resposta, corpo);
        return await ConsultarSeparadoAsync(numeroOrdem, cancellationToken);
    }

    /// <summary>FALLBACK: cabecalho + componentes + operacoes + itens em chamadas OData separadas.</summary>
    private async Task<OrdemProducaoSap?> ConsultarSeparadoAsync(
        string numeroOrdem,
        CancellationToken cancellationToken)
    {
        Uri urlCabecalho = MontarUrlCabecalho(numeroOrdem);
        using HttpRequestMessage requisicao = CriarRequisicao(HttpMethod.Get, urlCabecalho);
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            RegistrarDiagnostico(EtapaFallback, urlCabecalho, resposta, corpo);
            throw new ProductionOrderSapConsultaException(
                EtapaFallback,
                (int)resposta.StatusCode,
                $"Consulta OP falhou na etapa {EtapaFallback}, HTTP {(int)resposta.StatusCode}. Verifique a OP ou o serviço SAP.");
        }

        OrdemProducaoSap? ordem = MapearOrdem(corpo);
        if (ordem is null)
        {
            RegistrarDiagnostico(EtapaParse, urlCabecalho, resposta, corpo);
            return null;
        }

        IReadOnlyList<ComponenteOrdemProducaoSap> componentes = await ConsultarColecaoAsync(
            MontarUrlColecao("A_ProductionOrderComponent_2", numeroOrdem, ordenarPor: null),
            MapearComponente, MapearComponenteXml, cancellationToken);
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoes = await ConsultarColecaoAsync(
            MontarUrlColecao("A_ProductionOrderOperation_2", numeroOrdem, ordenarPor: "ProductionOrderOperation"),
            MapearOperacao, MapearOperacaoXml, cancellationToken);
        IReadOnlyList<ItemOrdemProducaoSap> itens = await ConsultarColecaoAsync(
            MontarUrlColecao("A_ProductionOrderItem_2", numeroOrdem, ordenarPor: null),
            MapearItem, MapearItemXml, cancellationToken);

        return ordem with { Componentes = componentes, Operacoes = operacoes, Itens = itens };
    }

    private async Task<IReadOnlyList<T>> ConsultarColecaoAsync<T>(
        Uri url,
        Func<JsonElement, T> mapearJson,
        Func<XElement, T> mapearXml,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage requisicao = CriarRequisicao(HttpMethod.Get, url);
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            // Colecao auxiliar: registra e segue sem derrubar a OP (cabecalho ja veio).
            RegistrarDiagnostico(EtapaFallback, url, resposta, corpo);
            return [];
        }

        return MapearColecao(corpo, mapearJson, mapearXml);
    }

    private Uri MontarUrlOrdem(string numeroOrdem)
    {
        // OData escapa aspas simples duplicando-as.
        string ordem = numeroOrdem.Trim().Replace("'", "''");
        string query = $"$format=json&{Expand}";
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        return ValidarDestino(new Uri(
            $"{_baseUri.AbsoluteUri}A_ProductionOrder_2('{ordem}')?{query}",
            UriKind.Absolute));
    }

    private Uri MontarUrlCabecalho(string numeroOrdem)
    {
        string ordem = numeroOrdem.Trim().Replace("'", "''");
        string query = AnexarSapClient("$format=json");
        return ValidarDestino(new Uri(
            $"{_baseUri.AbsoluteUri}A_ProductionOrder_2('{ordem}')?{query}",
            UriKind.Absolute));
    }

    private Uri MontarUrlColecao(string entidade, string numeroOrdem, string? ordenarPor)
    {
        string ordem = numeroOrdem.Trim().Replace("'", "''");
        string filtro = $"ManufacturingOrder eq '{ordem}'".Replace(" ", "%20");
        string query = $"$format=json&$filter={filtro}";
        if (!string.IsNullOrWhiteSpace(ordenarPor))
        {
            query = $"{query}&$orderby={ordenarPor}";
        }

        return ValidarDestino(new Uri(
            $"{_baseUri.AbsoluteUri}{entidade}?{AnexarSapClient(query)}",
            UriKind.Absolute));
    }

    private string AnexarSapClient(string query)
        => string.IsNullOrWhiteSpace(_configuracao.SapClient)
            ? query
            : $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";

    private void RegistrarDiagnostico(string etapa, Uri url, HttpResponseMessage resposta, string corpo)
    {
        // url SEM credencial (a allowlist/validador ja proibe userinfo; query so tem format/filter/sap-client).
        string urlSemCredencial = url.GetLeftPart(UriPartial.Path);
        string motivo = resposta.ReasonPhrase ?? string.Empty;
        _registrarDiagnostico(
            $"etapa {etapa}: HTTP {(int)resposta.StatusCode} {motivo} | url {urlSemCredencial} | resposta {SanitizarTrecho(corpo)}");
    }

    private static string SanitizarTrecho(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return "(vazio)";
        }

        string colapsado = string.Join(' ', corpo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return colapsado.Length <= LimiteTrechoResposta
            ? colapsado
            : colapsado[..LimiteTrechoResposta] + "...";
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
    /// Mapeia a resposta da OP. Tolerante a JSON (<c>$format=json</c>) e a XML/Atom (OData V2 padrao),
    /// porque alguns gateways SAP devolvem Atom mesmo com Accept/$format json. Internal para teste sem HTTP.
    /// </summary>
    internal static OrdemProducaoSap? MapearOrdem(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return null;
        }

        string texto = corpo.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return texto.StartsWith('<')
            ? MapearOrdemXml(texto)
            : MapearOrdemJson(texto);
    }

    // Correcao 4 (Tarefa 14.1): campos candidatos de data da OP, em ordem de prioridade. So usa os que existirem.
    private static readonly string[] CamposDataOrdem =
    {
        "MfgOrderScheduledStartDate",
        "MfgOrderPlannedStartDate",
        "MfgOrderActualStartDate",
        "CreationDate",
        "ManufacturingOrderCreationDate"
    };

    /// <summary>Le a data da OP defensivamente (primeiro campo presente/parseavel). Retorna (data, campoOrigem).</summary>
    private static (DateTime? data, string origem) LerDataOrdem(Func<string, string> lerCampo)
    {
        foreach (string campo in CamposDataOrdem)
        {
            if (TentarParsearDataSap(lerCampo(campo), out DateTime data))
            {
                return (data, campo);
            }
        }

        return (null, string.Empty);
    }

    /// <summary>Parser tolerante de data SAP: "/Date(ms)/", ISO "yyyy-MM-dd[THH:mm:ss]". Sem inventar data.</summary>
    internal static bool TentarParsearDataSap(string? bruto, out DateTime data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return false;
        }

        string texto = bruto.Trim();

        Match epoch = Regex.Match(texto, @"/Date\((-?\d+)(?:[+-]\d{4})?\)/");
        if (epoch.Success && long.TryParse(epoch.Groups[1].Value, out long ms))
        {
            data = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            return true;
        }

        return DateTime.TryParse(
            texto,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out data);
    }

    private static OrdemProducaoSap? MapearOrdemJson(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        JsonElement raiz = documento.RootElement;
        if (raiz.TryGetProperty("d", out JsonElement d))
        {
            raiz = d;
        }

        if (raiz.ValueKind != JsonValueKind.Object
            || string.IsNullOrWhiteSpace(LerTexto(raiz, "ManufacturingOrder")))
        {
            return null;
        }

        JsonElement raizData = raiz;
        (DateTime? dataOrdem, string origemData) = LerDataOrdem(campo => LerTexto(raizData, campo));

        return new OrdemProducaoSap
        {
            NumeroOrdem = LerTexto(raiz, "ManufacturingOrder"),
            TipoOrdem = LerTexto(raiz, "ManufacturingOrderType"),
            MaterialProduzido = LerTexto(raiz, "Material"),
            Centro = LerTexto(raiz, "ProductionPlant"),
            QuantidadePrevista = LerDecimal(raiz, "TotalQuantity"),
            Unidade = LerTexto(raiz, "ProductionUnit"),
            Deposito = LerTexto(raiz, "StorageLocation"),
            Lote = LerTexto(raiz, "Batch"),
            Liberada = LerFlagX(raiz, "OrderIsReleased"),
            Confirmada = LerFlagX(raiz, "OrderIsConfirmed"),
            Excluida = LerFlagX(raiz, "OrderIsDeleted"),
            DataOrdem = dataOrdem,
            OrigemDataOrdem = origemData,
            Componentes = LerColecao(raiz, "to_ProductionOrderComponent", MapearComponente),
            Operacoes = LerColecao(raiz, "to_ProductionOrderOperation", MapearOperacao),
            Itens = LerColecao(raiz, "to_ProductionOrderItem", MapearItem)
        };
    }

    private static ComponenteOrdemProducaoSap MapearComponente(JsonElement c)
        => new()
        {
            NumeroOrdem = LerTexto(c, "ManufacturingOrder"),
            Reserva = LerTexto(c, "Reservation"),
            ItemReserva = LerTexto(c, "ReservationItem"),
            Material = LerTexto(c, "Material"),
            Centro = LerTexto(c, "Plant"),
            Deposito = LerTexto(c, "StorageLocation"),
            QuantidadeNecessaria = LerDecimal(c, "RequiredQuantity"),
            UnidadeBase = LerTexto(c, "BaseUnit"),
            QuantidadeRetirada = LerDecimal(c, "WithdrawnQuantity"),
            QuantidadeDisponivelConfirmada = LerDecimal(c, "ConfirmedAvailableQuantity"),
            TipoMovimento = LerTexto(c, "GoodsMovementType"),
            Lote = LerTexto(c, "Batch"),
            ItemBOM = LerTexto(c, "BOMItem"),
            CategoriaItemBOM = LerTexto(c, "BOMItemCategory"),
            ReservaFinalizada = LerFlag(c, "ReservationIsFinallyIssued"),
            MarcadoParaEliminacao = LerFlag(c, "MatlCompIsMarkedForDeletion"),
            MaterialGranel = LerFlag(c, "IsBulkMaterialComponent"),
            BackflushSap = LerFlag(c, "MatlCompIsMarkedForBackflush"),
            TipoSplitLote = LerTexto(c, "BatchSplitType"),
            Operacao = LerTexto(c, "ManufacturingOrderOperation"),
            OrderOperationInternalId = LerPrimeiroTexto(c, "OrderOperationInternalID", "OrderOperationInternalId", "ManufacturingOrderOperationInternalID"),
            SequenciaOperacao = LerTexto(c, "ManufacturingOrderSequence")
        };

    private static OperacaoOrdemProducaoSap MapearOperacao(JsonElement o)
        => new()
        {
            Operacao = LerTexto(o, "ProductionOrderOperation"),
            OrderOperationInternalId = LerPrimeiroTexto(o, "OrderOperationInternalID", "OrderOperationInternalId", "ManufacturingOrderOperationInternalID"),
            Sequencia = LerTexto(o, "ProductionOrderSequence"),
            CentroTrabalho = LerTexto(o, "WorkCenter"),
            Centro = LerTexto(o, "Plant"),
            Descricao = LerTexto(o, "OperationText"),
            QuantidadePrevista = LerDecimal(o, "OpPlannedTotalQuantity"),
            QuantidadeConfirmada = LerDecimal(o, "OpTotalConfirmedYieldQty"),
            Unidade = LerTexto(o, "OperationUnit")
        };

    private static ItemOrdemProducaoSap MapearItem(JsonElement i)
        => new()
        {
            ItemOrdem = LerTexto(i, "ManufacturingOrderItem"),
            Material = LerTexto(i, "Material"),
            Centro = LerTexto(i, "ProductionPlant"),
            Deposito = LerTexto(i, "StorageLocation"),
            QuantidadePrevista = LerDecimal(i, "MfgOrderItemPlannedTotalQty"),
            QuantidadeEntregue = LerDecimal(i, "MfgOrderItemActualDeliveryQty"),
            Unidade = LerTexto(i, "ProductionUnit"),
            Lote = LerTexto(i, "Batch")
        };

    // ----- Parsing XML/Atom (OData V2) -----

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace MetadadosOData = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
    private static readonly XNamespace DadosOData = "http://schemas.microsoft.com/ado/2007/08/dataservices";

    private static OrdemProducaoSap? MapearOrdemXml(string xml)
    {
        XDocument documento;
        try
        {
            documento = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }

        XElement? entry = documento.Root;
        // Pode vir como feed (colecao) — usa o primeiro entry.
        if (entry is not null && entry.Name == Atom + "feed")
        {
            entry = entry.Element(Atom + "entry");
        }

        XElement? props = entry?.Element(Atom + "content")?.Element(MetadadosOData + "properties");
        if (entry is null || props is null || string.IsNullOrWhiteSpace(LerXmlTexto(props, "ManufacturingOrder")))
        {
            return null;
        }

        XElement propriedades = props;
        (DateTime? dataOrdem, string origemData) = LerDataOrdem(campo => LerXmlTexto(propriedades, campo));

        return new OrdemProducaoSap
        {
            NumeroOrdem = LerXmlTexto(props, "ManufacturingOrder"),
            TipoOrdem = LerXmlTexto(props, "ManufacturingOrderType"),
            MaterialProduzido = LerXmlTexto(props, "Material"),
            Centro = LerXmlTexto(props, "ProductionPlant"),
            QuantidadePrevista = LerXmlDecimal(props, "TotalQuantity"),
            Unidade = LerXmlTexto(props, "ProductionUnit"),
            Deposito = LerXmlTexto(props, "StorageLocation"),
            Lote = LerXmlTexto(props, "Batch"),
            Liberada = LerXmlFlagX(props, "OrderIsReleased"),
            Confirmada = LerXmlFlagX(props, "OrderIsConfirmed"),
            Excluida = LerXmlFlagX(props, "OrderIsDeleted"),
            DataOrdem = dataOrdem,
            OrigemDataOrdem = origemData,
            Componentes = LerColecaoXml(entry, "to_ProductionOrderComponent", MapearComponenteXml),
            Operacoes = LerColecaoXml(entry, "to_ProductionOrderOperation", MapearOperacaoXml),
            Itens = LerColecaoXml(entry, "to_ProductionOrderItem", MapearItemXml)
        };
    }

    private static ComponenteOrdemProducaoSap MapearComponenteXml(XElement p)
        => new()
        {
            NumeroOrdem = LerXmlTexto(p, "ManufacturingOrder"),
            Reserva = LerXmlTexto(p, "Reservation"),
            ItemReserva = LerXmlTexto(p, "ReservationItem"),
            Material = LerXmlTexto(p, "Material"),
            Centro = LerXmlTexto(p, "Plant"),
            Deposito = LerXmlTexto(p, "StorageLocation"),
            QuantidadeNecessaria = LerXmlDecimal(p, "RequiredQuantity"),
            UnidadeBase = LerXmlTexto(p, "BaseUnit"),
            QuantidadeRetirada = LerXmlDecimal(p, "WithdrawnQuantity"),
            QuantidadeDisponivelConfirmada = LerXmlDecimal(p, "ConfirmedAvailableQuantity"),
            TipoMovimento = LerXmlTexto(p, "GoodsMovementType"),
            Lote = LerXmlTexto(p, "Batch"),
            ItemBOM = LerXmlTexto(p, "BOMItem"),
            CategoriaItemBOM = LerXmlTexto(p, "BOMItemCategory"),
            ReservaFinalizada = LerXmlFlag(p, "ReservationIsFinallyIssued"),
            MarcadoParaEliminacao = LerXmlFlag(p, "MatlCompIsMarkedForDeletion"),
            MaterialGranel = LerXmlFlag(p, "IsBulkMaterialComponent"),
            BackflushSap = LerXmlFlag(p, "MatlCompIsMarkedForBackflush"),
            TipoSplitLote = LerXmlTexto(p, "BatchSplitType"),
            Operacao = LerXmlTexto(p, "ManufacturingOrderOperation"),
            OrderOperationInternalId = LerXmlPrimeiroTexto(p, "OrderOperationInternalID", "OrderOperationInternalId", "ManufacturingOrderOperationInternalID"),
            SequenciaOperacao = LerXmlTexto(p, "ManufacturingOrderSequence")
        };

    private static OperacaoOrdemProducaoSap MapearOperacaoXml(XElement p)
        => new()
        {
            Operacao = LerXmlTexto(p, "ProductionOrderOperation"),
            OrderOperationInternalId = LerXmlPrimeiroTexto(p, "OrderOperationInternalID", "OrderOperationInternalId", "ManufacturingOrderOperationInternalID"),
            Sequencia = LerXmlTexto(p, "ProductionOrderSequence"),
            CentroTrabalho = LerXmlTexto(p, "WorkCenter"),
            Centro = LerXmlTexto(p, "Plant"),
            Descricao = LerXmlTexto(p, "OperationText"),
            QuantidadePrevista = LerXmlDecimal(p, "OpPlannedTotalQuantity"),
            QuantidadeConfirmada = LerXmlDecimal(p, "OpTotalConfirmedYieldQty"),
            Unidade = LerXmlTexto(p, "OperationUnit")
        };

    private static ItemOrdemProducaoSap MapearItemXml(XElement p)
        => new()
        {
            ItemOrdem = LerXmlTexto(p, "ManufacturingOrderItem"),
            Material = LerXmlTexto(p, "Material"),
            Centro = LerXmlTexto(p, "ProductionPlant"),
            Deposito = LerXmlTexto(p, "StorageLocation"),
            QuantidadePrevista = LerXmlDecimal(p, "MfgOrderItemPlannedTotalQty"),
            QuantidadeEntregue = LerXmlDecimal(p, "MfgOrderItemActualDeliveryQty"),
            Unidade = LerXmlTexto(p, "ProductionUnit"),
            Lote = LerXmlTexto(p, "Batch")
        };

    private static IReadOnlyList<T> LerColecaoXml<T>(
        XElement entry,
        string navegacao,
        Func<XElement, T> mapear)
    {
        // OData V2 expand em Atom: <link title="to_X"><m:inline><feed><entry><content><m:properties>...
        XElement? inline = entry.Elements(Atom + "link")
            .FirstOrDefault(link => (string?)link.Attribute("title") == navegacao)
            ?.Element(MetadadosOData + "inline");
        XElement? feed = inline?.Element(Atom + "feed");
        if (feed is null)
        {
            return [];
        }

        return feed.Elements(Atom + "entry")
            .Select(item => item.Element(Atom + "content")?.Element(MetadadosOData + "properties"))
            .Where(props => props is not null)
            .Select(props => mapear(props!))
            .ToList();
    }

    private static string LerXmlTexto(XElement props, string propriedade)
        => props.Element(DadosOData + propriedade)?.Value?.Trim() ?? string.Empty;

    private static string LerXmlPrimeiroTexto(XElement props, params string[] propriedades)
        => propriedades.Select(propriedade => LerXmlTexto(props, propriedade))
            .FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor)) ?? string.Empty;

    private static bool LerXmlFlagX(XElement props, string propriedade)
        => string.Equals(LerXmlTexto(props, propriedade), "X", StringComparison.OrdinalIgnoreCase);

    private static bool LerXmlFlag(XElement props, string propriedade)
    {
        string valor = LerXmlTexto(props, propriedade);
        return string.Equals(valor, "X", StringComparison.OrdinalIgnoreCase)
               || string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(valor, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal LerXmlDecimal(XElement props, string propriedade)
        => decimal.TryParse(
            LerXmlTexto(props, propriedade),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal valor)
            ? valor
            : 0m;

    private static IReadOnlyList<T> LerColecao<T>(
        JsonElement pai,
        string navegacao,
        Func<JsonElement, T> mapear)
    {
        if (!pai.TryGetProperty(navegacao, out JsonElement nav))
        {
            return [];
        }

        // OData V2: { "results": [...] }. Tolera tambem array direto ou objeto unico (V4/variacoes).
        JsonElement colecao = nav;
        if (nav.ValueKind == JsonValueKind.Object && nav.TryGetProperty("results", out JsonElement results))
        {
            colecao = results;
        }

        if (colecao.ValueKind == JsonValueKind.Array)
        {
            return colecao.EnumerateArray().Select(mapear).ToList();
        }

        return colecao.ValueKind == JsonValueKind.Object ? [mapear(colecao)] : [];
    }

    private static string LerTexto(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static string LerPrimeiroTexto(JsonElement elemento, params string[] propriedades)
        => propriedades.Select(propriedade => LerTexto(elemento, propriedade))
            .FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor)) ?? string.Empty;

    private static bool LerFlagX(JsonElement elemento, string propriedade)
        => string.Equals(LerTexto(elemento, propriedade), "X", StringComparison.OrdinalIgnoreCase);

    private static bool LerFlag(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return false;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when valor.TryGetInt32(out int n) => n != 0,
            JsonValueKind.String => InterpretarFlag(valor.GetString()),
            _ => false
        };
    }

    private static bool InterpretarFlag(string? valor)
    {
        string texto = valor?.Trim() ?? string.Empty;
        return string.Equals(texto, "X", StringComparison.OrdinalIgnoreCase)
               || string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase)
               || texto == "1";
    }

    private static decimal LerDecimal(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return 0m;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.Number when valor.TryGetDecimal(out decimal n) => n,
            JsonValueKind.String when decimal.TryParse(
                valor.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal s) => s,
            _ => 0m
        };
    }

    /// <summary>
    /// Mapeia uma COLECAO (resposta do fallback de chamadas separadas): JSON <c>{ "d": { "results": [...] } }</c>
    /// ou XML/Atom <c>&lt;feed&gt;&lt;entry&gt;&lt;content&gt;&lt;m:properties&gt;...</c>. Internal para teste.
    /// </summary>
    internal static IReadOnlyList<T> MapearColecao<T>(
        string corpo,
        Func<JsonElement, T> mapearJson,
        Func<XElement, T> mapearXml)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return [];
        }

        string texto = corpo.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        if (texto.StartsWith('<'))
        {
            XDocument documento;
            try
            {
                documento = XDocument.Parse(texto);
            }
            catch (XmlException)
            {
                return [];
            }

            XElement? feed = documento.Root?.Name == Atom + "feed"
                ? documento.Root
                : documento.Descendants(Atom + "feed").FirstOrDefault();
            if (feed is null)
            {
                return [];
            }

            return feed.Elements(Atom + "entry")
                .Select(item => item.Element(Atom + "content")?.Element(MetadadosOData + "properties"))
                .Where(props => props is not null)
                .Select(props => mapearXml(props!))
                .ToList();
        }

        using JsonDocument documentoJson = JsonDocument.Parse(texto);
        JsonElement raiz = documentoJson.RootElement;
        if (raiz.TryGetProperty("d", out JsonElement d))
        {
            raiz = d;
        }

        JsonElement colecao = raiz;
        if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("results", out JsonElement results))
        {
            colecao = results;
        }

        return colecao.ValueKind == JsonValueKind.Array
            ? colecao.EnumerateArray().Select(mapearJson).ToList()
            : [];
    }
}

/// <summary>Falha controlada de consulta da OP (cabecalho indisponivel ate no fallback).</summary>
public sealed class ProductionOrderSapConsultaException : Exception
{
    public ProductionOrderSapConsultaException(string etapa, int? statusHttp, string mensagemSanitizada)
        : base(mensagemSanitizada)
    {
        Etapa = etapa;
        StatusHttp = statusHttp;
    }

    public string Etapa { get; }
    public int? StatusHttp { get; }
}
