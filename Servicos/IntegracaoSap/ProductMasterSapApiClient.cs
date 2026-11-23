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

    // Aba "EANs adicionais" da MM02. Nome PLURAL, verificado no documento de servico do DEV110 em
    // 23/11/2026 — o singular rende 404, como ja aconteceu com A_Batch x Batch.
    private const string CaminhoEan = "A_ProductUnitsOfMeasureEAN";
    private const string SelectEan =
        "$select=Product,AlternativeUnit,ProductStandardID,InternationalArticleNumberCat,IsMainGlobalTradeItemNumber";

    /// <summary>
    /// Unidade alternativa cujo EAN vai na etiqueta da CAIXA. Decisao do usuario em 23/11/2026: CX, e SOMENTE
    /// CX. Sem fallback para a unidade principal nem para UN de proposito — o EAN de UN identifica o pacote
    /// avulso, e impresso na caixa ele faz o leitor do cliente ler outro produto.
    /// </summary>
    public const string UnidadeCaixa = "CX";

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

    /// <summary>
    /// GET dos EANs do material: A_ProductUnitsOfMeasureEAN?$filter=Product eq 'CODIGO'&amp;$select=...
    /// <para>
    /// O filtro é SÓ por produto, de propósito — a unidade é escolhida depois, no código
    /// (<see cref="EscolherEanDaCaixa"/>). Filtrar <c>AlternativeUnit eq 'CX'</c> no servidor devolveria
    /// zero linhas, indistinguível de "material sem EAN", se o SAP um dia responder com o código interno
    /// da unidade em vez da forma externa — e são poucas linhas por material, então não há ganho em filtrar lá.
    /// </para>
    /// </summary>
    public static Uri MontarUrlEansProduto(Uri baseUri, string codigoMaterial, string? sapClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        string material = (codigoMaterial ?? string.Empty).Trim();
        string baseSemBarra = baseUri.ToString().TrimEnd('/');
        string cliente = string.IsNullOrWhiteSpace(sapClient) ? string.Empty : $"&sap-client={sapClient.Trim()}";
        string filtroEscapado = Uri.EscapeDataString($"Product eq '{material}'");
        return new Uri($"{baseSemBarra}/{CaminhoEan}?$filter={filtroEscapado}&{SelectEan}&$format=json{cliente}");
    }

    /// <summary>
    /// Parse do JSON OData V2 das linhas de EAN. Aceita <c>d.results[]</c>, <c>d</c> objeto único ou array
    /// direto, igual ao parse das descrições. Nunca lança: JSON ruim vira lista vazia, e lista vazia faz a
    /// etiqueta manter o aviso em vez de imprimir barra.
    /// </summary>
    public static IReadOnlyList<SapProdutoEanDto> ParsearEans(string? json)
    {
        List<SapProdutoEanDto> eans = [];
        if (string.IsNullOrWhiteSpace(json))
        {
            return eans;
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
                    eans.Add(LerEan(item));
                }
            }
            else if (raiz.ValueKind == JsonValueKind.Object)
            {
                eans.Add(LerEan(raiz));
            }
        }
        catch (JsonException)
        {
            // Retorna o que acumulou — nunca lança.
        }

        return eans;
    }

    /// <summary>
    /// Escolhe o EAN que vai na etiqueta da caixa: a linha da unidade <see cref="UnidadeCaixa"/> com EAN
    /// preenchido. Entre várias linhas de CX, prefere a marcada como principal; depois a primeira.
    /// <para>
    /// Devolve string vazia quando não há CX — e isso NÃO é um defeito a ser contornado. A etiqueta
    /// mantém o aviso "EAN DO PRODUTO NAO CADASTRADO", porque uma barra com o EAN de outra unidade é lida
    /// pelo cliente como outro produto. É a mesma regra que já governa a barra da HU.
    /// </para>
    /// </summary>
    public static string EscolherEanDaCaixa(IEnumerable<SapProdutoEanDto>? eans)
    {
        if (eans is null)
        {
            return string.Empty;
        }

        List<SapProdutoEanDto> daCaixa = [.. eans.Where(e =>
            e is not null
            && string.Equals((e.AlternativeUnit ?? string.Empty).Trim(), UnidadeCaixa, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(e.ProductStandardID))];

        if (daCaixa.Count == 0)
        {
            return string.Empty;
        }

        SapProdutoEanDto escolhida =
            daCaixa.FirstOrDefault(e => e.IsMainGlobalTradeItemNumber) ?? daCaixa[0];

        return escolhida.ProductStandardID.Trim();
    }

    private static SapProdutoEanDto LerEan(JsonElement item)
        => new()
        {
            Product = LerTexto(item, "Product"),
            AlternativeUnit = LerTexto(item, "AlternativeUnit"),
            ProductStandardID = LerTexto(item, "ProductStandardID"),
            InternationalArticleNumberCat = LerTexto(item, "InternationalArticleNumberCat"),
            IsMainGlobalTradeItemNumber = LerBooleano(item, "IsMainGlobalTradeItemNumber")
        };

    /// <summary>
    /// OData V2 serializa booleano ora como <c>true</c>, ora como a STRING <c>"true"</c> — o retorno real do
    /// DEV110 em 23/11/2026 trouxe a string. Ler só <c>JsonValueKind.True</c> perderia o "EAN principal".
    /// </summary>
    private static bool LerBooleano(JsonElement obj, string propriedade)
    {
        if (!obj.TryGetProperty(propriedade, out JsonElement valor))
        {
            return false;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(valor.GetString(), out bool b) && b,
            _ => false
        };
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
