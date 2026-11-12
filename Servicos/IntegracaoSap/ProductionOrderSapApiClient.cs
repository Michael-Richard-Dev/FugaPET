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
    private const int LimitePreCargaOrdens = 30;
    private const string PlantaPreCargaFugaPet = "3007";

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

    /// <summary>
    /// Lista cabeçalhos de OP relevantes para pré-carga em memória. GET somente leitura,
    /// sem CSRF/POST/PATCH; detalhes/componentes continuam sendo obtidos pelo GET específico.
    /// </summary>
    public async Task<IReadOnlyList<OrdemProducaoSap>> ListarOrdensRelevantesAsync(
        CancellationToken cancellationToken = default)
    {
        Uri url = MontarUrlPreCargaOrdens();
        using HttpRequestMessage requisicao = CriarRequisicao(HttpMethod.Get, url);
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            RegistrarDiagnostico(EtapaFallback, url, resposta, corpo);
            return [];
        }

        return MapearColecao(corpo, MapearOrdemResumo, MapearOrdemResumoXml)
            .Where(ordem => !string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
            .ToList();
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
            MontarUrlColecao("A_ProductionOrderOperation_2", numeroOrdem, ordenarPor: "ManufacturingOrderSequence,ManufacturingOrderOperation"),
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

    private Uri MontarUrlPreCargaOrdens()
    {
        DateTime dataMinima = DateTime.Today.AddDays(-30);
        string filtro = string.Join(" and ",
        [
            $"ProductionPlant eq '{PlantaPreCargaFugaPet}'",
            "OrderIsReleased eq 'X'",
            "OrderIsConfirmed ne 'X'",
            "OrderIsDeleted ne 'X'",
            $"MfgOrderScheduledStartDate ge datetime'{dataMinima:yyyy-MM-ddT00:00:00}'"
        ]).Replace(" ", "%20");

        string select = "$select=ManufacturingOrder,ManufacturingOrderType,Material,ProductionPlant,TotalQuantity,ProductionUnit,StorageLocation,Batch,OrderIsReleased,OrderIsConfirmed,OrderIsDeleted,MfgOrderScheduledStartDate,MfgOrderPlannedStartDate,MfgOrderActualStartDate,CreationDate,ManufacturingOrderCreationDate";
        string query = AnexarSapClient($"$format=json&$filter={filtro}&{select}&$orderby=MfgOrderScheduledStartDate desc&$top={LimitePreCargaOrdens}");
        return ValidarDestino(new Uri($"{_baseUri.AbsoluteUri}A_ProductionOrder_2?{query}", UriKind.Absolute));
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

    /// <summary>
    /// GATE 118B (contrato 118B-A1): leitura FRESCA do item e dos componentes da OP, nas rotas minimas
    /// validadas em runtime. Duas requisicoes GET separadas, $select minimo e, nos componentes,
    /// $inlinecount=allpages para COMPROVAR completude. Nao e snapshot atomico e nao faz POST.
    /// Qualquer leitura nao-2xx/nao-mapeavel => resultado INDISPONIVEL (fail-closed), nunca lista vazia
    /// silenciosa.
    /// </summary>
    public async Task<LeituraFrescaOrdem261> ConsultarEstadoFresco261Async(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        string ordem = numeroOrdem?.Trim() ?? string.Empty;
        if (ordem.Length == 0)
        {
            return LeituraFrescaOrdem261.Indisponivel(ordem, "Leitura fresca do 261 sem OP: bloqueado.");
        }

        (IReadOnlyList<ItemOrdemProducaoSap> itens, int? _, bool okItem) = await ConsultarColecaoFrescaAsync(
            MontarUrlColecaoFresca("A_ProductionOrderItem_2", ordem, SelectItemFresco),
            MapearItem, MapearItemXml, cancellationToken);

        if (!okItem)
        {
            return LeituraFrescaOrdem261.Indisponivel(
                ordem, "Leitura fresca do item da OP indisponivel no SAP: bloqueado (nenhum POST).");
        }

        ItemOrdemProducaoSap? item = itens.Count > 0 ? itens[0] : null;
        if (item is null)
        {
            return LeituraFrescaOrdem261.Indisponivel(
                ordem, "Leitura fresca do item da OP voltou vazia: bloqueado (nenhum POST).");
        }

        (IReadOnlyList<ComponenteOrdemProducaoSap> componentes, int? declarados, bool okComp) =
            await ConsultarColecaoFrescaAsync(
                MontarUrlColecaoFresca("A_ProductionOrderComponent_2", ordem, SelectComponenteFresco),
                MapearComponente, MapearComponenteXml, cancellationToken);

        if (!okComp)
        {
            return LeituraFrescaOrdem261.Indisponivel(
                ordem, "Leitura fresca dos componentes da OP indisponivel no SAP: bloqueado (nenhum POST).");
        }

        return new LeituraFrescaOrdem261
        {
            NumeroOrdemConsultada = ordem,
            Disponivel = true,
            Mensagem = "Leitura fresca item+componentes concluida.",
            Item = item,
            Componentes = componentes,
            ComponentesDeclaradosPeloServico = declarados
        };
    }

    private const string SelectItemFresco =
        "$select=ManufacturingOrder,ManufacturingOrderItem,MfgOrderItemPlannedTotalQty,"
        + "MfgOrderItemGoodsReceiptQty,ProductionUnit,Material,ProductionPlant,StorageLocation,Batch";

    private const string SelectComponenteFresco =
        "$select=Reservation,ReservationItem,ManufacturingOrder,ManufacturingOrderSequence,"
        + "ManufacturingOrderOperation,Material,Plant,StorageLocation,Batch,RequiredQuantity,"
        + "WithdrawnQuantity,ConfirmedAvailableQuantity,QuantityIsFixed,BaseUnit,GoodsMovementType,"
        + "ReservationIsFinallyIssued,MatlCompIsMarkedForDeletion";

    private Uri MontarUrlColecaoFresca(string entidade, string numeroOrdem, string select)
    {
        string ordem = numeroOrdem.Trim().Replace("'", "''");
        string filtro = $"ManufacturingOrder eq '{ordem}'".Replace(" ", "%20");
        string query = $"$format=json&$filter={filtro}&{select}&$inlinecount=allpages";
        return ValidarDestino(new Uri(
            $"{_baseUri.AbsoluteUri}{entidade}?{AnexarSapClient(query)}",
            UriKind.Absolute));
    }

    /// <summary>
    /// GET de colecao para a leitura FRESCA: diferente de <c>ConsultarColecaoAsync</c>, NAO engole
    /// falha devolvendo lista vazia — devolve ok=false para o chamador bloquear. Tambem extrai o
    /// <c>__count</c> do $inlinecount quando presente.
    /// </summary>
    private async Task<(IReadOnlyList<T> Itens, int? Declarados, bool Ok)> ConsultarColecaoFrescaAsync<T>(
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
            RegistrarDiagnostico(EtapaFallback, url, resposta, corpo);
            return ([], null, false);
        }

        return (MapearColecao(corpo, mapearJson, mapearXml), LerContagemDeclarada(corpo), true);
    }

    /// <summary>
    /// GATE 118B (§12): extrai o <c>__count</c> ($inlinecount=allpages) do corpo JSON/Atom. Ausente ou
    /// nao numerico => null, e completude NAO comprovada.
    /// </summary>
    internal static int? LerContagemDeclarada(string corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo))
        {
            return null;
        }

        string texto = corpo.TrimStart();
        if (texto.StartsWith('{'))
        {
            try
            {
                using JsonDocument documento = JsonDocument.Parse(corpo);
                JsonElement raiz = documento.RootElement;
                if (raiz.TryGetProperty("d", out JsonElement d))
                {
                    raiz = d;
                }

                if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("__count", out JsonElement contagem))
                {
                    return contagem.ValueKind switch
                    {
                        JsonValueKind.Number when contagem.TryGetInt32(out int n) => n,
                        JsonValueKind.String when int.TryParse(
                            contagem.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s) => s,
                        _ => null
                    };
                }
            }
            catch (JsonException)
            {
                return null;
            }

            return null;
        }

        try
        {
            XDocument documento = XDocument.Parse(corpo);
            string? valor = documento.Descendants(MetadadosOData + "count").FirstOrDefault()?.Value?.Trim();
            return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int xml) ? xml : null;
        }
        catch (XmlException)
        {
            return null;
        }
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
            VersaoProducao = LerTexto(raiz, "ProductionVersion"),
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
            // GATE 118B: espelhos TRI-STATE para o alocador 261 (ausente => null, nunca 0/false).
            QuantidadeNecessariaSap = LerDecimalNulo(c, "RequiredQuantity"),
            QuantidadeRetiradaSap = LerDecimalNulo(c, "WithdrawnQuantity"),
            QuantidadeDisponivelConfirmadaSap = LerDecimalNulo(c, "ConfirmedAvailableQuantity"),
            QuantidadeFixa = LerFlagNulo(c, "QuantityIsFixed"),
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
            Operacao = LerPrimeiroTexto(o, "ManufacturingOrderOperation", "ProductionOrderOperation"),
            OrderOperationInternalId = LerPrimeiroTexto(o, "OrderIntBillOfOperationsItem", "OrderOperationInternalID", "OrderOperationInternalId", "ManufacturingOrderOperationInternalID"),
            Sequencia = LerPrimeiroTexto(o, "ManufacturingOrderSequence", "ProductionOrderSequence"),
            Suboperacao = LerPrimeiroTexto(o, "ManufacturingOrderSubOperation", "ProductionOrderSubOperation"),
            CentroTrabalho = LerTexto(o, "WorkCenter"),
            WorkCenterInternalId = LerPrimeiroTexto(o, "WorkCenterInternalID", "WorkCenterInternalId"),
            WorkCenterTypeCode = LerPrimeiroTexto(o, "WorkCenterTypeCode", "WorkCenterCategoryCode"),
            Centro = LerPrimeiroTexto(o, "ProductionPlant", "Plant"),
            Descricao = LerPrimeiroTexto(o, "MfgOrderOperationText", "OperationText"),
            QuantidadePrevista = LerDecimal(o, "OpPlannedTotalQuantity"),
            QuantidadeConfirmada = LerDecimal(o, "OpTotalConfirmedYieldQty"),
            Unidade = LerTexto(o, "OperationUnit")
        };

    private static ItemOrdemProducaoSap MapearItem(JsonElement i)
        => new()
        {
            // GATE 118B-R4: OP real do item. Ausente => string vazia (ausencia preservada),
            // NUNCA a OP solicitada na request.
            NumeroOrdem = LerTexto(i, "ManufacturingOrder"),
            ItemOrdem = LerTexto(i, "ManufacturingOrderItem"),
            Material = LerTexto(i, "Material"),
            Centro = LerTexto(i, "ProductionPlant"),
            Deposito = LerTexto(i, "StorageLocation"),
            QuantidadePrevista = LerDecimal(i, "MfgOrderItemPlannedTotalQty"),
            // GATE 118B: MfgOrderItemActualDeliveryQty nao existe no contrato; leitura removida.
            // Campo autoritativo de producao recebida, em TRI-STATE:
            QuantidadeRecebidaSap = LerDecimalNulo(i, "MfgOrderItemGoodsReceiptQty"),
            QuantidadePrevistaSap = LerDecimalNulo(i, "MfgOrderItemPlannedTotalQty"),
            Unidade = LerTexto(i, "ProductionUnit"),
            Lote = LerTexto(i, "Batch")
        };

    private static OrdemProducaoSap MapearOrdemResumo(JsonElement raiz)
    {
        (DateTime? dataOrdem, string origemData) = LerDataOrdem(campo => LerTexto(raiz, campo));
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
            VersaoProducao = LerTexto(raiz, "ProductionVersion"),
            DataOrdem = dataOrdem,
            OrigemDataOrdem = origemData
        };
    }

    private static OrdemProducaoSap MapearOrdemResumoXml(XElement props)
    {
        (DateTime? dataOrdem, string origemData) = LerDataOrdem(campo => LerXmlTexto(props, campo));
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
            VersaoProducao = LerXmlTexto(props, "ProductionVersion"),
            DataOrdem = dataOrdem,
            OrigemDataOrdem = origemData
        };
    }
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
            VersaoProducao = LerXmlTexto(props, "ProductionVersion"),
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
            // GATE 118B: espelhos TRI-STATE (paridade com o caminho JSON).
            QuantidadeNecessariaSap = LerXmlDecimalNulo(p, "RequiredQuantity"),
            QuantidadeRetiradaSap = LerXmlDecimalNulo(p, "WithdrawnQuantity"),
            QuantidadeDisponivelConfirmadaSap = LerXmlDecimalNulo(p, "ConfirmedAvailableQuantity"),
            QuantidadeFixa = LerXmlFlagNulo(p, "QuantityIsFixed"),
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
            Operacao = LerXmlPrimeiroTexto(p, "ManufacturingOrderOperation", "ProductionOrderOperation"),
            OrderOperationInternalId = LerXmlPrimeiroTexto(p, "OrderIntBillOfOperationsItem", "OrderOperationInternalID", "OrderOperationInternalId", "ManufacturingOrderOperationInternalID"),
            Sequencia = LerXmlPrimeiroTexto(p, "ManufacturingOrderSequence", "ProductionOrderSequence"),
            Suboperacao = LerXmlPrimeiroTexto(p, "ManufacturingOrderSubOperation", "ProductionOrderSubOperation"),
            CentroTrabalho = LerXmlTexto(p, "WorkCenter"),
            WorkCenterInternalId = LerXmlPrimeiroTexto(p, "WorkCenterInternalID", "WorkCenterInternalId"),
            WorkCenterTypeCode = LerXmlPrimeiroTexto(p, "WorkCenterTypeCode", "WorkCenterCategoryCode"),
            Centro = LerXmlPrimeiroTexto(p, "ProductionPlant", "Plant"),
            Descricao = LerXmlPrimeiroTexto(p, "MfgOrderOperationText", "OperationText"),
            QuantidadePrevista = LerXmlDecimal(p, "OpPlannedTotalQuantity"),
            QuantidadeConfirmada = LerXmlDecimal(p, "OpTotalConfirmedYieldQty"),
            Unidade = LerXmlTexto(p, "OperationUnit")
        };

    private static ItemOrdemProducaoSap MapearItemXml(XElement p)
        => new()
        {
            // GATE 118B-R4: paridade com o caminho JSON — OP real do item, sem fallback da request.
            NumeroOrdem = LerXmlTexto(p, "ManufacturingOrder"),
            ItemOrdem = LerXmlTexto(p, "ManufacturingOrderItem"),
            Material = LerXmlTexto(p, "Material"),
            Centro = LerXmlTexto(p, "ProductionPlant"),
            Deposito = LerXmlTexto(p, "StorageLocation"),
            QuantidadePrevista = LerXmlDecimal(p, "MfgOrderItemPlannedTotalQty"),
            // GATE 118B: MfgOrderItemActualDeliveryQty nao existe no contrato; leitura removida.
            QuantidadeRecebidaSap = LerXmlDecimalNulo(p, "MfgOrderItemGoodsReceiptQty"),
            QuantidadePrevistaSap = LerXmlDecimalNulo(p, "MfgOrderItemPlannedTotalQty"),
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

    /// <summary>
    /// GATE 118B: leitura TRI-STATE de decimal no Atom/XML. Elemento ausente, m:null="true", texto
    /// vazio ou nao parseavel => null (INDETERMINADO). NUNCA colapsa em 0.
    /// </summary>
    private static decimal? LerXmlDecimalNulo(XElement props, string propriedade)
    {
        XElement? elemento = props.Element(DadosOData + propriedade);
        if (elemento is null)
        {
            return null;
        }

        if (string.Equals((string?)elemento.Attribute(MetadadosOData + "null"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return decimal.TryParse(
            elemento.Value?.Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal valor)
            ? valor
            : null;
    }

    /// <summary>
    /// GATE 118B: leitura TRI-STATE de booleano no Atom/XML. Ausente/m:null/valor nao reconhecido
    /// => null (INDETERMINADO). NUNCA colapsa em false.
    /// </summary>
    private static bool? LerXmlFlagNulo(XElement props, string propriedade)
    {
        XElement? elemento = props.Element(DadosOData + propriedade);
        if (elemento is null)
        {
            return null;
        }

        if (string.Equals((string?)elemento.Attribute(MetadadosOData + "null"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return InterpretarFlagNulo(elemento.Value);
    }

    /// <summary>
    /// GATE 118B: interpreta o texto de um booleano SAP em TRI-STATE. Reconhece APENAS as formas
    /// verdadeiras explicitas (X/true/1) e as falsas explicitas (false/0). Vazio ou qualquer outro
    /// texto e INDETERMINADO (null) em vez de virar false.
    /// </summary>
    private static bool? InterpretarFlagNulo(string? valor)
    {
        string texto = valor?.Trim() ?? string.Empty;
        if (string.Equals(texto, "X", StringComparison.OrdinalIgnoreCase)
            || string.Equals(texto, "true", StringComparison.OrdinalIgnoreCase)
            || texto == "1")
        {
            return true;
        }

        return string.Equals(texto, "false", StringComparison.OrdinalIgnoreCase) || texto == "0"
            ? false
            : null;
    }

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

    /// <summary>
    /// GATE 118B: leitura TRI-STATE de decimal no JSON. Propriedade ausente, JSON null, string vazia
    /// ou texto nao parseavel => null (INDETERMINADO). NUNCA colapsa em 0.
    /// </summary>
    private static decimal? LerDecimalNulo(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.Number when valor.TryGetDecimal(out decimal n) => n,
            JsonValueKind.String when decimal.TryParse(
                valor.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out decimal s) => s,
            _ => null
        };
    }

    /// <summary>
    /// GATE 118B: leitura TRI-STATE de booleano no JSON. Propriedade ausente, JSON null ou texto nao
    /// reconhecido => null (INDETERMINADO). NUNCA colapsa em false.
    /// </summary>
    private static bool? LerFlagNulo(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when valor.TryGetInt32(out int n) => n != 0,
            JsonValueKind.String => InterpretarFlagNulo(valor.GetString()),
            _ => null
        };
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


