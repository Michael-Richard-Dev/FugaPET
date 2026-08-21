using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente do SAP Product Plant (API_PRODUCT_SRV / A_ProductPlant, OData V2). Apenas GET de
/// <c>IsBatchManagementRequired</c> por material × centro (sem POST/PATCH/CSRF). Montagem de URL e
/// parse ficam isolados/testaveis; a composicao HTTP/config fica na camada de servico (mesmo
/// ValidadorUrlSap/host das demais integracoes). Fonte EXCLUSIVA de A_ProductPlant — nao mistura
/// com A_Product.
/// </summary>
public static class ProductPlantSapApiClient
{
    private const string CaminhoEntidade = "A_ProductPlant";
    private const string Select = "$select=Product,Plant,IsBatchManagementRequired";

    /// <summary>
    /// GET por chave composta: A_ProductPlant(Product='MAT',Plant='CENTRO')?$select=...&amp;$format=json.
    /// Material e centro sao preservados (Trim, sem conversao numerica, sem perder zeros a esquerda).
    /// </summary>
    public static Uri MontarUrlProdutoCentro(Uri baseUri, string material, string centro, string? sapClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        string mat = (material ?? string.Empty).Trim();
        string plant = (centro ?? string.Empty).Trim();
        string baseSemBarra = baseUri.ToString().TrimEnd('/');
        string cliente = string.IsNullOrWhiteSpace(sapClient) ? string.Empty : $"&sap-client={sapClient.Trim()}";
        string chave = $"Product='{Uri.EscapeDataString(mat)}',Plant='{Uri.EscapeDataString(plant)}'";
        return new Uri($"{baseSemBarra}/{CaminhoEntidade}({chave})?{Select}&$format=json{cliente}");
    }

    /// <summary>
    /// Parse do JSON OData V2. Aceita <c>d</c> (objeto por chave) ou <c>d.results[]</c> (primeiro).
    /// Nunca lanca; null quando nao deserializavel/ausente.
    /// </summary>
    public static SapProductPlantDto? ParsearProdutoCentro(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;

            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("d", out JsonElement d))
            {
                raiz = d;
            }

            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("results", out JsonElement results)
                && results.ValueKind == JsonValueKind.Array)
            {
                if (results.GetArrayLength() == 0)
                {
                    return null;
                }

                raiz = results[0];
            }

            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new SapProductPlantDto
            {
                Product = LerTexto(raiz, "Product"),
                Plant = LerTexto(raiz, "Plant"),
                IsBatchManagementRequired = LerBooleano(raiz, "IsBatchManagementRequired")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? LerBooleano(JsonElement obj, string propriedade)
    {
        if (!obj.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => InterpretarTextoBooleano(valor.GetString()),
            _ => null
        };
    }

    // OData V2 Edm.Boolean chega como true/false; toleramos variacoes textuais ("true"/"false"/"X"/"1"/"0").
    private static bool? InterpretarTextoBooleano(string? texto)
    {
        string t = (texto ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            return null;
        }

        if (bool.TryParse(t, out bool resultado))
        {
            return resultado;
        }

        return t.Equals("X", StringComparison.OrdinalIgnoreCase) || t == "1"
            ? true
            : t == "0"
                ? false
                : null;
    }

    private static string LerTexto(JsonElement obj, string propriedade)
        => obj.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? (valor.GetString() ?? string.Empty).Trim()
            : valor.ValueKind == JsonValueKind.Number
                ? valor.GetRawText().Trim()
                : string.Empty;
}
