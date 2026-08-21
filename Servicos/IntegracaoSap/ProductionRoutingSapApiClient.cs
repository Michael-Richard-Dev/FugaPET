using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 048-E REV2: cliente READ-ONLY (GET, Basic Auth) do roteiro AUTORITATIVO. Resolve a partir da
/// ProductionVersion da OP: API_PRODUCTION_VERSION (A_ProductionVersion, chave Material+Plant+ProductionVersion)
/// → BillOfOperationsGroup/Variant → API_PRODUCTION_ROUTING (A_ProductionRoutingOperation) →
/// OperationStandardTextCode. Nenhuma escolha por heuristica: 0 ou >1 versao/rota ⇒ null (fail-closed).
/// Sem POST/PATCH/CSRF. Reaproveita o parser de colecao (JSON/Atom) do <see cref="ProductionOrderSapApiClient"/>.
/// </summary>
public sealed class ProductionRoutingSapApiClient
{
    private static readonly XNamespace DadosOData = "http://schemas.microsoft.com/ado/2007/08/dataservices";

    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly Uri _baseVersao;
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
        _baseVersao = ValidadorUrlSap.ValidarBaseUrl(configuracao.ProductionVersionBaseUrlEfetiva, configuracao.HostsPermitidos);
        _baseRoteiro = ValidadorUrlSap.ValidarBaseUrl(configuracao.ProductionRoutingBaseUrlEfetiva, configuracao.HostsPermitidos);

        string credenciais = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credenciais);
        _registrarDiagnostico = registrarDiagnostico ?? (_ => { });
    }

    /// <summary>
    /// Resolve o roteiro autoritativo da OP. Retorna null (fail-closed) em qualquer ausencia/ambiguidade:
    /// ProductionVersion vazia; versao nao unica; grupo/variante ausentes; ou nenhuma operacao de roteiro.
    /// </summary>
    public async Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
        OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ordem);

        string material = (ordem.MaterialProduzido ?? string.Empty).Trim();
        string plant = (ordem.Centro ?? string.Empty).Trim();
        string versao = (ordem.VersaoProducao ?? string.Empty).Trim();

        // ProductionVersion e a chave autoritativa: sem ela NAO ha resolucao segura (§3).
        if (material.Length == 0 || plant.Length == 0 || versao.Length == 0)
        {
            _registrarDiagnostico("roteiro: ProductionVersion/Material/Plant ausentes na OP — fail-closed.");
            return null;
        }

        VersaoProducaoRoteiro? versaoRoteiro = await ResolverVersaoAsync(material, plant, versao, cancellationToken);
        if (versaoRoteiro is null || versaoRoteiro.Grupo.Length == 0 || versaoRoteiro.Variante.Length == 0)
        {
            return null; // versao nao unica / grupo/variante nao correlacionados — fail-closed
        }

        IReadOnlyList<OperacaoRoteiroSap> operacoes =
            await ListarOperacoesRoteiroAsync(versaoRoteiro.Grupo, versaoRoteiro.Variante, cancellationToken);
        if (operacoes.Count == 0)
        {
            _registrarDiagnostico("roteiro: nenhuma ProductionRoutingOperation para o grupo/variante — fail-closed.");
            return null;
        }

        return new RoteiroProducaoSap
        {
            BillOfOperationsGroup = versaoRoteiro.Grupo,
            BillOfOperationsVariant = versaoRoteiro.Variante,
            Operacoes = operacoes
        };
    }

    private async Task<VersaoProducaoRoteiro?> ResolverVersaoAsync(
        string material, string plant, string versao, CancellationToken cancellationToken)
    {
        string filtro = ($"Material eq '{Escapar(material)}'"
            + $" and Plant eq '{Escapar(plant)}'"
            + $" and ProductionVersion eq '{Escapar(versao)}'").Replace(" ", "%20");
        Uri url = MontarUrl(_baseVersao, "A_ProductionVersion", $"$format=json&$filter={filtro}");

        string? corpo = await ObterAsync(url, cancellationToken);
        if (corpo is null)
        {
            return null;
        }

        IReadOnlyList<VersaoProducaoRoteiro> versoes =
            ProductionOrderSapApiClient.MapearColecao(corpo, MapearVersaoJson, MapearVersaoXml);

        // §3: mais de uma versao possivel ⇒ fail-closed (nunca escolher por heuristica).
        if (versoes.Count != 1)
        {
            _registrarDiagnostico($"roteiro: API_PRODUCTION_VERSION retornou {versoes.Count} versoes — fail-closed.");
            return null;
        }

        return versoes[0];
    }

    private async Task<IReadOnlyList<OperacaoRoteiroSap>> ListarOperacoesRoteiroAsync(
        string grupo, string variante, CancellationToken cancellationToken)
    {
        string filtro = ($"ProductionRoutingGroup eq '{Escapar(grupo)}'"
            + $" and ProductionRouting eq '{Escapar(variante)}'").Replace(" ", "%20");
        Uri url = MontarUrl(_baseRoteiro, "A_ProductionRoutingOperation", $"$format=json&$filter={filtro}");

        string? corpo = await ObterAsync(url, cancellationToken);
        return corpo is null
            ? []
            : ProductionOrderSapApiClient.MapearColecao(corpo, MapearOperacaoRoteiroJson, MapearOperacaoRoteiroXml);
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
            _registrarDiagnostico($"roteiro: HTTP {(int)resposta.StatusCode} em {url.GetLeftPart(UriPartial.Path)} — fail-closed.");
            return null;
        }

        return corpo;
    }

    private Uri MontarUrl(Uri baseUri, string entidade, string query)
    {
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        Uri destino = new($"{baseUri.AbsoluteUri.TrimEnd('/')}/{entidade}?{query}", UriKind.Absolute);
        return ValidadorUrlSap.ValidarDestino(destino, baseUri, _configuracao.HostsPermitidos);
    }

    private static string Escapar(string valor) => valor.Replace("'", "''");

    // ----- Mapeamento (internal para teste sem HTTP) -----

    /// <summary>Versao de producao com o vinculo ao roteiro (grupo/variante). So o necessario.</summary>
    internal sealed record VersaoProducaoRoteiro(string Grupo, string Variante, string Tipo);

    internal static VersaoProducaoRoteiro MapearVersaoJson(JsonElement v)
        => new(
            LerPrimeiroTextoJson(v, "BillOfOperationsGroup", "ProductionRoutingGroup"),
            LerPrimeiroTextoJson(v, "BillOfOperationsVariant", "ProductionRouting", "BillOfOperationsGroupCounter"),
            LerTextoJson(v, "BillOfOperationsType"));

    internal static VersaoProducaoRoteiro MapearVersaoXml(XElement v)
        => new(
            LerPrimeiroTextoXml(v, "BillOfOperationsGroup", "ProductionRoutingGroup"),
            LerPrimeiroTextoXml(v, "BillOfOperationsVariant", "ProductionRouting", "BillOfOperationsGroupCounter"),
            LerTextoXml(v, "BillOfOperationsType"));

    internal static OperacaoRoteiroSap MapearOperacaoRoteiroJson(JsonElement o)
    {
        bool obtido = o.TryGetProperty("OperationStandardTextCode", out JsonElement codigo);
        return new OperacaoRoteiroSap
        {
            Operacao = LerPrimeiroTextoJson(o, "OperationNumber", "ProductionRoutingOperation", "Operation"),
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
            Operacao = LerPrimeiroTextoXml(o, "OperationNumber", "ProductionRoutingOperation", "Operation"),
            CodigoTextoPadrao = (codigo?.Value ?? string.Empty).Trim(),
            TextoPadraoObtido = codigo is not null
        };
    }

    private static string LerTextoJson(JsonElement elemento, string propriedade)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static string LerPrimeiroTextoJson(JsonElement elemento, params string[] propriedades)
        => propriedades.Select(p => LerTextoJson(elemento, p))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static string LerTextoXml(XElement props, string propriedade)
        => props.Element(DadosOData + propriedade)?.Value?.Trim() ?? string.Empty;

    private static string LerPrimeiroTextoXml(XElement props, params string[] propriedades)
        => propriedades.Select(p => LerTextoXml(props, p))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
}
