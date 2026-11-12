using System.Text.Json;
using System.Text.RegularExpressions;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal static class LeitorEvidenciaProdutoSapR8B
{
    private static readonly HashSet<string> Campos = new(StringComparer.Ordinal)
    {
        "Product", "ProductType", "LastChangeDateTime", "LastChangeDate", "LastChangedByUser"
    };

    internal static EvidenciaProdutoSapR8B? Ler(string? json, ContextoBackendProdutoSapR8B contexto,
        string? etagRecebidoNoHeader = null)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return null;
            if (raiz.EnumerateObject().Count(propriedade => propriedade.Name == "d") > 1) return null;
            if (raiz.TryGetProperty("d", out JsonElement dados)) raiz = dados;
            if (raiz.ValueKind != JsonValueKind.Object) return null;
            Dictionary<string, string?> valores = new(StringComparer.Ordinal);
            foreach (JsonProperty propriedade in raiz.EnumerateObject())
            {
                if (!Campos.Contains(propriedade.Name)) continue;
                if (propriedade.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) return null;
                if (!valores.TryAdd(propriedade.Name, propriedade.Value.GetString())) return null;
            }
            string produto = valores.GetValueOrDefault("Product") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(produto)) return null;
            string? alteracao = valores.GetValueOrDefault("LastChangeDateTime");
            return new(new(produto, valores.GetValueOrDefault("ProductType") ?? string.Empty,
                alteracao, ExtrairOffset(alteracao), valores.GetValueOrDefault("LastChangeDate"),
                valores.GetValueOrDefault("LastChangedByUser")), contexto,
                string.IsNullOrWhiteSpace(etagRecebidoNoHeader) ? null : etagRecebidoNoHeader);
        }
        catch (JsonException) { return null; }
    }

    private static string? ExtrairOffset(string? original)
    {
        if (original is null) return null;
        Match iso = Regex.Match(original,
            @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?<offset>Z|[+-]\d{2}:\d{2})$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (iso.Success) return iso.Groups["offset"].Value;
        Match odata = Regex.Match(original, @"^/Date\(-?\d+(?<offset>[+-]\d{4})\)/$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return odata.Success ? odata.Groups["offset"].Value : null;
    }
}
