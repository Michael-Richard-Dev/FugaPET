using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 126F: montagem de URL e parse do GET de LOTES no SAP (API_BATCH_SRV, OData V2).
/// <para>
/// Partes PURAS, sem rede e sem credencial: tudo aqui e exercitavel por teste. O transporte vive no
/// <see cref="BatchSapServico"/>.
/// </para>
/// <para>
/// CONTRATO VERIFICADO em 16/11/2026 contra o S/4 do DEV110, nao presumido:
/// a colecao chama-se <c>Batch</c> (pedir <c>A_Batch</c> devolve 404 "Resource not found for the
/// segment"); a chave e (Material, BatchIdentifyingPlant, Batch) e o BatchIdentifyingPlant vem
/// VAZIO neste sistema — o nivel de lote e por material, nao por centro, por isso o filtro NAO
/// inclui centro; as datas vem como <c>/Date(1787270400000)/</c>.
/// </para>
/// </summary>
internal static class BatchSapApiClient
{
    /// <summary>Nome da colecao de lotes no servico. Ver a nota de contrato na classe.</summary>
    internal const string Colecao = "Batch";

    /// <summary>
    /// URL do GET de um lote. Sem <c>$select</c> DE PROPOSITO: no OData V2 do SAP, um unico nome de
    /// campo desconhecido derruba a consulta INTEIRA com 404, e a resposta completa deste servico e
    /// pequena. Trocar robustez por alguns bytes nao se paga.
    /// </summary>
    internal static string MontarUrl(string baseUrl, string material, string lote, string? sapClient)
    {
        string raiz = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        string filtro =
            $"Material eq '{EscaparLiteralOData(material)}' and Batch eq '{EscaparLiteralOData(lote)}'";

        var url = new StringBuilder()
            .Append(raiz).Append('/').Append(Colecao)
            .Append("?$format=json&$filter=").Append(Uri.EscapeDataString(filtro));

        if (!string.IsNullOrWhiteSpace(sapClient))
        {
            url.Append("&sap-client=").Append(Uri.EscapeDataString(sapClient.Trim()));
        }

        return url.ToString();
    }

    /// <summary>
    /// Aspas simples sao o delimitador de literal no OData: dentro do valor elas DOBRAM. Sem isto, um
    /// lote com apostrofo quebraria a consulta — ou pior, mudaria o filtro.
    /// </summary>
    internal static string EscaparLiteralOData(string? valor)
        => (valor ?? string.Empty).Trim().Replace("'", "''");

    /// <summary>
    /// Interpreta <c>d.results</c>. Resposta vazia, malformada ou sem o lote devolve lista VAZIA —
    /// nunca excecao, nunca data inventada.
    /// </summary>
    internal static IReadOnlyList<LoteSapDatas> Interpretar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object
                || !raiz.TryGetProperty("d", out JsonElement d))
            {
                return [];
            }

            // O servico devolve "results" na consulta por filtro; a leitura por chave devolve o
            // objeto direto. Os dois formatos sao aceitos para que a funcao nao dependa da forma
            // da chamada.
            if (d.TryGetProperty("results", out JsonElement results)
                && results.ValueKind == JsonValueKind.Array)
            {
                var lotes = new List<LoteSapDatas>();
                foreach (JsonElement item in results.EnumerateArray())
                {
                    LoteSapDatas? lote = MapearLote(item);
                    if (lote is not null)
                    {
                        lotes.Add(lote);
                    }
                }

                return lotes;
            }

            LoteSapDatas? unico = MapearLote(d);
            return unico is null ? [] : [unico];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static LoteSapDatas? MapearLote(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string lote = LerTexto(item, "Batch");
        if (lote.Length == 0)
        {
            return null;
        }

        return new LoteSapDatas(
            Material: LerTexto(item, "Material"),
            Lote: lote,
            DataFabricacao: LerData(item, "ManufactureDate"),
            DataVencimento: LerData(item, "ShelfLifeExpirationDate"));
    }

    private static string LerTexto(JsonElement item, string propriedade)
        => item.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? (valor.GetString() ?? string.Empty).Trim()
            : string.Empty;

    /// <summary>
    /// Data do SAP em OData V2. Reaproveita o parser tolerante ja existente, que entende
    /// <c>/Date(ms)/</c> com e sem deslocamento e tambem ISO. Campo ausente, null ou ilegivel vira
    /// null: NAO se deduz data.
    /// </summary>
    private static DateTime? LerData(JsonElement item, string propriedade)
    {
        if (!item.TryGetProperty(propriedade, out JsonElement valor)
            || valor.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return ProductionOrderSapApiClient.TentarParsearDataSap(valor.GetString(), out DateTime data)
            ? data
            : null;
    }
}
