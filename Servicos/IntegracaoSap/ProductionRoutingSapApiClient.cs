using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 095F (contrato Ares 095D / arquitetura 095E-R1): cliente READ-ONLY (GET, Basic Auth) do roteiro
/// autoritativo via API_PRODUCTION_ROUTING;v=3 — SEM API_PRODUCTION_VERSION. Resolve em dois passos:
///   1) ProductionRoutingMatlAssgmt por (Product + Plant) → UMA tupla única (ProductionRoutingGroup, ProductionRouting);
///   2) ProductionRoutingOperation por (Group + Routing) → operações do roteiro (Operation, Plant, WorkCenter,
///      OperationStandardTextCode).
/// Zero heurística: 0 ou &gt;1 tuplas de rota ⇒ null (fail-closed). O cliente NÃO escolhe a ocorrência corrente,
/// NÃO aplica PP_FORM e NÃO faz routing local. Sem POST/PATCH/CSRF.
/// </summary>
public sealed class ProductionRoutingSapApiClient
{
    private static readonly XNamespace DadosOData = "http://schemas.microsoft.com/ado/2007/08/dataservices";

    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseRoteiro;
    private readonly AuthenticationHeaderValue _autorizacao;
    private readonly Action<string> _registrarDiagnostico;

    public ProductionRoutingSapApiClient(
        ConfiguracaoSap configuracao,
        HttpClient httpClient,
        Action<string>? registrarDiagnostico = null)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        // Fonte ÚNICA V3: API_PRODUCTION_ROUTING;v=3. Nenhuma URL concorrente (ProductionVersion removida).
        _baseRoteiro = ValidadorUrlSap.ValidarBaseUrl(configuracao.ProductionRoutingBaseUrlEfetiva, configuracao.HostsPermitidos);

        string credenciais = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
        _registrarDiagnostico = registrarDiagnostico ?? (_ => { });
    }

    /// <summary>
    /// Resolve o roteiro V3 COMPLETO da OP (todas as operações válidas). null (fail-closed) em qualquer
    /// ausência/ambiguidade: Material/Plant ausentes; 0 ou &gt;1 tuplas (Group, Routing); ou 0 operações.
    /// A correlação da ocorrência (Operation+Plant+WorkCenter) é responsabilidade do CorrelacionadorOcorrenciaRoteiroSap.
    /// </summary>
    public async Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
        OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        string product = (ordem.MaterialProduzido ?? string.Empty).Trim();
        string plant = (ordem.Centro ?? string.Empty).Trim();
        if (product.Length == 0 || plant.Length == 0)
        {
            _registrarDiagnostico("PAYLOAD_INVALIDO: Product/Plant ausentes na OP — fail-closed.");
            return null;
        }

        RotaProdutoV3? rota = await ResolverRotaPorProdutoAsync(product, plant, cancellationToken);
        if (rota is null)
        {
            return null; // ZERO_ROUTINGS / ROUTING_AMBIGUO já diagnosticados
        }

        IReadOnlyList<OperacaoRoteiroSap> operacoes =
            await ListarOperacoesRoteiroAsync(rota.Grupo, rota.Roteiro, cancellationToken);
        if (operacoes.Count == 0)
        {
            _registrarDiagnostico("ZERO_OPERATIONS: nenhuma ProductionRoutingOperation para o grupo/roteiro — fail-closed.");
            return null;
        }

        return new RoteiroProducaoSap
        {
            BillOfOperationsGroup = rota.Grupo,
            BillOfOperationsVariant = rota.Roteiro,
            Operacoes = operacoes
        };
    }

    // GET 1 — DISCOVERY: ProductionRoutingMatlAssgmt por Product + Plant. Cardinalidade sobre a tupla ÚNICA
    // (ProductionRoutingGroup, ProductionRouting); duplicatas idênticas colapsam; 0/&gt;1 ⇒ fail-closed.
    private async Task<RotaProdutoV3?> ResolverRotaPorProdutoAsync(
        string product, string plant, CancellationToken cancellationToken)
    {
        string filtro = ($"Product eq '{Escapar(product)}'"
            + $" and Plant eq '{Escapar(plant)}'").Replace(" ", "%20");
        string select = "$select=Product,Plant,ProductionRoutingGroup,ProductionRouting,ProductionRoutingMatlAssgmt";
        Uri url = MontarUrl("ProductionRoutingMatlAssgmt", $"$format=json&{select}&$filter={filtro}");

        string? corpo = await ObterAsync(url, cancellationToken);
        if (corpo is null)
        {
            return null;
        }

        IReadOnlyList<RotaProdutoV3> tuplas;
        try
        {
            tuplas = ProductionOrderSapApiClient.MapearColecao(corpo, MapearRotaJson, MapearRotaXml);
        }
        catch (Exception ex) when (ex is JsonException or System.Xml.XmlException or FormatException)
        {
            _registrarDiagnostico($"PAYLOAD_INVALIDO: discovery ({ex.GetType().Name}) — fail-closed.");
            return null;
        }

        // Descartar tuplas com Group ou Routing vazios; colapsar duplicatas IDÊNTICAS.
        List<RotaProdutoV3> unicas = tuplas
            .Where(t => t.Grupo.Length > 0 && t.Roteiro.Length > 0)
            .Distinct()
            .ToList();

        if (unicas.Count == 0)
        {
            _registrarDiagnostico("ZERO_ROUTINGS: nenhuma tupla (Group, Routing) para o produto/planta — fail-closed.");
            return null;
        }

        if (unicas.Count > 1)
        {
            _registrarDiagnostico($"ROUTING_AMBIGUO: {unicas.Count} tuplas (Group, Routing) distintas — fail-closed.");
            return null;
        }

        return unicas[0];
    }

    // GET 2 — OPERAÇÕES: ProductionRoutingOperation por Group + Routing.
    private async Task<IReadOnlyList<OperacaoRoteiroSap>> ListarOperacoesRoteiroAsync(
        string grupo, string roteiro, CancellationToken cancellationToken)
    {
        string filtro = ($"ProductionRoutingGroup eq '{Escapar(grupo)}'"
            + $" and ProductionRouting eq '{Escapar(roteiro)}'").Replace(" ", "%20");
        string select = "$select=ProductionRoutingGroup,ProductionRouting,ProductionRoutingSequence,Operation,"
            + "OperationText,OperationStandardTextCode,Plant,WorkCenter,WorkCenterInternalID,WorkCenterTypeCode";
        Uri url = MontarUrl("ProductionRoutingOperation", $"$format=json&{select}&$filter={filtro}");

        string? corpo = await ObterAsync(url, cancellationToken);
        if (corpo is null)
        {
            return [];
        }

        try
        {
            return ProductionOrderSapApiClient.MapearColecao(corpo, MapearOperacaoRoteiroJson, MapearOperacaoRoteiroXml);
        }
        catch (Exception ex) when (ex is JsonException or System.Xml.XmlException or FormatException)
        {
            _registrarDiagnostico($"PAYLOAD_INVALIDO: operações ({ex.GetType().Name}) — fail-closed.");
            return [];
        }
    }

    private async Task<string?> ObterAsync(Uri url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requisicao = new(HttpMethod.Get, url);
        requisicao.Headers.Authorization = _autorizacao;
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            _registrarDiagnostico($"HTTP_STATUS {(int)resposta.StatusCode} em {url.GetLeftPart(UriPartial.Path)} — fail-closed.");
            return null;
        }

        return corpo;
    }

    private Uri MontarUrl(string entidade, string query)
    {
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        Uri destino = new($"{_baseRoteiro.AbsoluteUri.TrimEnd('/')}/{entidade}?{query}", UriKind.Absolute);
        return ValidadorUrlSap.ValidarDestino(destino, _baseRoteiro, _configuracao.HostsPermitidos);
    }

    private static string Escapar(string valor) => valor.Replace("'", "''");

    // ----- Mapeamento (internal para teste sem HTTP) -----

    /// <summary>Tupla de rota da discovery V3 (ProductionRoutingMatlAssgmt). Só o necessário à cardinalidade.</summary>
    internal sealed record RotaProdutoV3(string Grupo, string Roteiro);

    internal static RotaProdutoV3 MapearRotaJson(JsonElement v)
        => new(LerTextoJson(v, "ProductionRoutingGroup"), LerTextoJson(v, "ProductionRouting"));

    internal static RotaProdutoV3 MapearRotaXml(XElement v)
        => new(LerTextoXml(v, "ProductionRoutingGroup"), LerTextoXml(v, "ProductionRouting"));

    internal static OperacaoRoteiroSap MapearOperacaoRoteiroJson(JsonElement o)
    {
        bool obtido = o.TryGetProperty("OperationStandardTextCode", out JsonElement codigo);
        return new OperacaoRoteiroSap
        {
            Operacao = LerTextoJson(o, "Operation"),
            Plant = LerTextoJson(o, "Plant"),
            WorkCenter = LerTextoJson(o, "WorkCenter"),
            CodigoTextoPadrao = obtido && codigo.ValueKind == JsonValueKind.String
                ? (codigo.GetString() ?? string.Empty).Trim()
                : string.Empty,
            TextoPadraoObtido = obtido
        };
    }

    internal static OperacaoRoteiroSap MapearOperacaoRoteiroXml(XElement o)
    {
        XElement? codigo = o.Element(DadosOData + "OperationStandardTextCode");
        return new OperacaoRoteiroSap
        {
            Operacao = LerTextoXml(o, "Operation"),
            Plant = LerTextoXml(o, "Plant"),
            WorkCenter = LerTextoXml(o, "WorkCenter"),
            CodigoTextoPadrao = (codigo?.Value ?? string.Empty).Trim(),
            TextoPadraoObtido = codigo is not null
        };
    }

    private static string LerTextoJson(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static string LerTextoXml(XElement props, string propriedade)
        => props.Element(DadosOData + propriedade)?.Value?.Trim() ?? string.Empty;
}
