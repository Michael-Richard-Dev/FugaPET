using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.9.1 (Ajuste 2): cliente do SAP Product Master (API_PRODUCT_SRV / A_Product, OData V2).
/// Apenas GET dos campos mestres do material (ProductType/ProductGroup/BaseUnit). Sem POST/PATCH/CSRF.
/// A montagem da URL e o parse ficam isolados/testáveis; a composição HTTP/config é feita pela camada de serviço
/// (a validação do host/base-url segue o mesmo ValidadorUrlSap das demais integrações).
/// </summary>
public static class ProductMasterSapApiClient
{
    private const string CaminhoEntidade = "A_Product";
    private const string Select = "$select=Product,ProductType,ProductGroup,BaseUnit";

    private const string CaminhoDescricao = "A_ProductDescription";
    private const string SelectDescricao = "$select=Product,Language,ProductDescription";

    // Tarefa Consumo 22.10 (Ajuste 5): ordem de preferência de idioma da descrição do material.
    private static readonly string[] _idiomasPreferidos = ["PT", "P", "EN"];

    /// <summary>
    /// GET por chave: A_Product('CODIGO')?$select=...&amp;$format=json&amp;sap-client=&lt;n&gt;.
    /// O código do material é preservado (Trim, sem conversão numérica, sem perder zeros).
    /// Fonte APENAS de dados técnicos (Product/ProductType/ProductGroup/BaseUnit) — NÃO de descrição.
    /// </summary>
    public static Uri MontarUrlProduto(Uri baseUri, string codigoMaterial, string? sapClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        string material = (codigoMaterial ?? string.Empty).Trim();
        string baseSemBarra = baseUri.ToString().TrimEnd('/');
        string cliente = string.IsNullOrWhiteSpace(sapClient) ? string.Empty : $"&sap-client={sapClient.Trim()}";
        string materialEscapado = Uri.EscapeDataString(material);
        return new Uri($"{baseSemBarra}/{CaminhoEntidade}('{materialEscapado}')?{Select}&$format=json{cliente}");
    }

    /// <summary>
    /// Tarefa Consumo 22.10 (Ajuste 3): GET da descrição do material em A_ProductDescription (fonte da descrição REAL).
    /// A_ProductDescription?$filter=Product eq 'CODIGO'[ and Language eq 'PT']&amp;$select=Product,Language,ProductDescription.
    /// Quando <paramref name="idioma"/> vier vazio, busca por Product e o melhor idioma é escolhido no código
    /// (<see cref="EscolherMelhorDescricao"/>). O código do material é preservado (Trim, sem conversão numérica).
    /// </summary>
    public static Uri MontarUrlDescricaoProduto(Uri baseUri, string codigoMaterial, string? idioma = null, string? sapClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        string material = (codigoMaterial ?? string.Empty).Trim();
        string baseSemBarra = baseUri.ToString().TrimEnd('/');
        string cliente = string.IsNullOrWhiteSpace(sapClient) ? string.Empty : $"&sap-client={sapClient.Trim()}";

        string filtro = $"Product eq '{material}'";
        if (!string.IsNullOrWhiteSpace(idioma))
        {
            filtro += $" and Language eq '{idioma.Trim()}'";
        }

        string filtroEscapado = Uri.EscapeDataString(filtro);
        return new Uri($"{baseSemBarra}/{CaminhoDescricao}?$filter={filtroEscapado}&{SelectDescricao}&$format=json{cliente}");
    }

    /// <summary>Parse do JSON OData V2 (d) para o DTO cru do Product Master. Null se não deserializável.</summary>
    public static SapProductMasterDto? ParsearProduto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;

            // OData V2 encapsula em "d"; alguns retornos vêm direto.
            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("d", out JsonElement d))
            {
                raiz = d;
            }

            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new SapProductMasterDto
            {
                Product = LerTexto(raiz, "Product"),
                ProductType = LerTexto(raiz, "ProductType"),
                ProductGroup = LerTexto(raiz, "ProductGroup"),
                BaseUnit = LerTexto(raiz, "BaseUnit")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tarefa Consumo 22.10 (Ajuste 1): parse do JSON OData V2 de A_ProductDescription. Aceita
    /// <c>d.results[]</c> (filtro), <c>d</c> objeto único (por chave) ou array direto. Nunca lança.
    /// </summary>
    public static IReadOnlyList<SapProductDescriptionDto> ParsearDescricoes(string? json)
    {
        List<SapProductDescriptionDto> descricoes = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return descricoes;
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(json);
            JsonElement raiz = documento.RootElement;

            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("d", out JsonElement d))
            {
                raiz = d;
            }

            if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("results", out JsonElement results))
            {
                raiz = results;
            }

            if (raiz.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in raiz.EnumerateArray())
                {
                    descricoes.Add(LerDescricao(item));
                }
            }
            else if (raiz.ValueKind == JsonValueKind.Object)
            {
                descricoes.Add(LerDescricao(raiz));
            }
        }
        catch (JsonException)
        {
            // Retorna a lista já acumulada (possivelmente vazia) — nunca lança.
        }

        return descricoes;
    }

    /// <summary>
    /// Tarefa Consumo 22.10 (Ajuste 5): escolhe a melhor descrição por idioma (PT → P → EN → primeira com texto
    /// → primeira). Ignora entradas sem ProductDescription ao aplicar a preferência de idioma. Null se lista vazia.
    /// </summary>
    public static SapProductDescriptionDto? EscolherMelhorDescricao(IEnumerable<SapProductDescriptionDto>? descricoes)
    {
        if (descricoes is null)
        {
            return null;
        }

        List<SapProductDescriptionDto> lista = [.. descricoes];
        if (lista.Count == 0)
        {
            return null;
        }

        foreach (string idioma in _idiomasPreferidos)
        {
            SapProductDescriptionDto? preferida = lista.FirstOrDefault(d =>
                string.Equals((d.Language ?? string.Empty).Trim(), idioma, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(d.ProductDescription));
            if (preferida is not null)
            {
                return preferida;
            }
        }

        // Sem idioma preferido: primeira com descrição preenchida; senão a primeira retornada (fallback controlado).
        return lista.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.ProductDescription)) ?? lista[0];
    }

    private static SapProductDescriptionDto LerDescricao(JsonElement item)
        => new()
        {
            Product = LerTexto(item, "Product"),
            Language = LerTexto(item, "Language"),
            ProductDescription = LerTexto(item, "ProductDescription")
        };

    private static string LerTexto(JsonElement obj, string propriedade)
        => obj.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? (valor.GetString() ?? string.Empty).Trim()
            : valor.ValueKind == JsonValueKind.Number
                ? valor.GetRawText().Trim()
                : string.Empty;
}
