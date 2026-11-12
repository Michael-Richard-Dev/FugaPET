using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal sealed class WorkCenterSapApiClient
{
    private readonly ConfiguracaoSap _configuracao;
    private readonly HttpClient _httpClient;
    private readonly AuthenticationHeaderValue _autorizacao;

    public WorkCenterSapApiClient(ConfiguracaoSap configuracao, HttpClient httpClient)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        string credencial = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{configuracao.Usuario}:{configuracao.Senha}"));
        _autorizacao = new AuthenticationHeaderValue("Basic", credencial);
    }

    public async Task<ResultadoResolucaoWorkCenterSap> ResolverWorkCentersAsync(
        OrdemProducaoSap ordem,
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoes,
        CancellationToken cancellationToken)
    {
        List<(string InternalId, string TypeCode)> chaves = [];
        foreach (OperacaoOrdemProducaoSap operacao in operacoes)
        {
            if (string.IsNullOrWhiteSpace(operacao.WorkCenterInternalId))
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenterInternalID ausente.");
            }

            if (string.IsNullOrWhiteSpace(operacao.WorkCenterTypeCode))
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenterTypeCode ausente.");
            }

            (string, string) chave = (operacao.WorkCenterInternalId.Trim(), operacao.WorkCenterTypeCode.Trim());
            if (!chaves.Any(c => c.InternalId == chave.Item1 && c.TypeCode == chave.Item2))
            {
                chaves.Add((chave.Item1, chave.Item2));
            }
        }

        IReadOnlyList<CentroTrabalhoSap> mestres = await ListarWorkCentersAsync(chaves, cancellationToken);
        List<OperacaoOrdemProducaoSap> resolvidas = [];
        foreach (OperacaoOrdemProducaoSap operacao in operacoes)
        {
            List<CentroTrabalhoSap> matches = mestres
                .Where(m => string.Equals(m.WorkCenterInternalId, operacao.WorkCenterInternalId.Trim(), StringComparison.Ordinal)
                    && string.Equals(m.WorkCenterTypeCode, operacao.WorkCenterTypeCode.Trim(), StringComparison.Ordinal))
                .ToList();

            if (matches.Count == 0)
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenter master não encontrado.");
            }

            if (matches.Count > 1)
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenter master ambíguo.");
            }

            CentroTrabalhoSap master = matches[0];
            if (master.WorkCenterIsToBeDeleted)
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenter marcado para eliminação.");
            }

            if (string.IsNullOrWhiteSpace(master.Plant))
            {
                return Falha($"Operação {operacao.Operacao}: Plant do WorkCenter ausente.");
            }

            if (string.IsNullOrWhiteSpace(master.WorkCenter))
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenter ausente no master.");
            }

            if (!string.IsNullOrWhiteSpace(operacao.CentroTrabalho)
                && !string.Equals(operacao.CentroTrabalho.Trim(), master.WorkCenter.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Falha($"Operação {operacao.Operacao}: WorkCenter divergente do master.");
            }

            string plantOperacao = string.IsNullOrWhiteSpace(operacao.Centro) ? ordem.Centro : operacao.Centro;
            if (!string.IsNullOrWhiteSpace(plantOperacao)
                && !string.Equals(plantOperacao.Trim(), master.Plant.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Falha($"Operação {operacao.Operacao}: Plant divergente do WorkCenter master.");
            }

            resolvidas.Add(operacao with { Centro = master.Plant.Trim(), CentroTrabalho = master.WorkCenter.Trim() });
        }

        return new ResultadoResolucaoWorkCenterSap(true, string.Empty, resolvidas);
    }

    private async Task<IReadOnlyList<CentroTrabalhoSap>> ListarWorkCentersAsync(
        IReadOnlyList<(string InternalId, string TypeCode)> chaves,
        CancellationToken cancellationToken)
    {
        if (chaves.Count == 0)
        {
            return [];
        }

        string filtro = string.Join(" or ", chaves.Select(c =>
            $"(WorkCenterInternalID eq '{Escapar(c.InternalId)}' and WorkCenterTypeCode eq '{Escapar(c.TypeCode)}')"));
        const string select = "$select=WorkCenterInternalID,WorkCenterTypeCode,WorkCenter,Plant,WorkCenterIsToBeDeleted";
        Uri url = MontarUrl("A_WorkCenters", $"$format=json&{select}&$filter={Uri.EscapeDataString(filtro)}");

        using HttpRequestMessage requisicao = new(HttpMethod.Get, url);
        requisicao.Headers.Authorization = _autorizacao;
        requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using HttpResponseMessage resposta = await _httpClient.SendAsync(requisicao, cancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            System.Diagnostics.Trace.TraceWarning($"[SAP][WorkCenter] HTTP {(int)resposta.StatusCode} em A_WorkCenters.");
            return [];
        }

        return ProductionOrderSapApiClient.MapearColecao(corpo, MapearWorkCenterJson, MapearWorkCenterXml);
    }

    private Uri MontarUrl(string entidade, string query)
    {
        if (!string.IsNullOrWhiteSpace(_configuracao.SapClient))
        {
            query = $"{query}&sap-client={Uri.EscapeDataString(_configuracao.SapClient.Trim())}";
        }

        Uri baseUri = new(_configuracao.WorkCenterBaseUrlEfetiva.TrimEnd('/') + "/", UriKind.Absolute);
        Uri destino = new($"{baseUri.AbsoluteUri}{entidade}?{query}", UriKind.Absolute);
        return ValidadorUrlSap.ValidarDestino(destino, baseUri, _configuracao.HostsPermitidos);
    }

    internal static CentroTrabalhoSap MapearWorkCenterJson(JsonElement w)
        => new()
        {
            WorkCenterInternalId = LerPrimeiroTextoJson(w, "WorkCenterInternalID", "WorkCenterInternalId"),
            WorkCenterTypeCode = LerPrimeiroTextoJson(w, "WorkCenterTypeCode", "WorkCenterCategoryCode"),
            WorkCenter = LerPrimeiroTextoJson(w, "WorkCenter", "WorkCenterName"),
            Plant = LerPrimeiroTextoJson(w, "Plant", "WorkCenterPlant"),
            WorkCenterIsToBeDeleted = LerFlagJson(w, "WorkCenterIsToBeDeleted")
        };

    internal static CentroTrabalhoSap MapearWorkCenterXml(XElement w)
        => new()
        {
            WorkCenterInternalId = LerXmlPrimeiroTexto(w, "WorkCenterInternalID", "WorkCenterInternalId"),
            WorkCenterTypeCode = LerXmlPrimeiroTexto(w, "WorkCenterTypeCode", "WorkCenterCategoryCode"),
            WorkCenter = LerXmlPrimeiroTexto(w, "WorkCenter", "WorkCenterName"),
            Plant = LerXmlPrimeiroTexto(w, "Plant", "WorkCenterPlant"),
            WorkCenterIsToBeDeleted = LerXmlFlag(w, "WorkCenterIsToBeDeleted")
        };

    private static ResultadoResolucaoWorkCenterSap Falha(string mensagem) => new(false, mensagem, []);

    private static string Escapar(string valor) => valor.Replace("'", "''");

    private static string LerPrimeiroTextoJson(JsonElement e, params string[] nomes)
    {
        foreach (string nome in nomes)
        {
            if (e.TryGetProperty(nome, out JsonElement valor) && valor.ValueKind != JsonValueKind.Null)
            {
                return valor.ToString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool LerFlagJson(JsonElement e, string nome)
    {
        string valor = LerPrimeiroTextoJson(e, nome);
        return valor.Equals("true", StringComparison.OrdinalIgnoreCase) || valor.Equals("X", StringComparison.OrdinalIgnoreCase);
    }

    private static string LerXmlPrimeiroTexto(XElement e, params string[] nomes)
    {
        foreach (string nome in nomes)
        {
            XElement? elemento = e.Descendants().FirstOrDefault(x => x.Name.LocalName == nome);
            if (elemento is not null)
            {
                return (elemento.Value ?? string.Empty).Trim();
            }
        }

        return string.Empty;
    }

    private static bool LerXmlFlag(XElement e, string nome)
    {
        string valor = LerXmlPrimeiroTexto(e, nome);
        return valor.Equals("true", StringComparison.OrdinalIgnoreCase) || valor.Equals("X", StringComparison.OrdinalIgnoreCase);
    }
}
