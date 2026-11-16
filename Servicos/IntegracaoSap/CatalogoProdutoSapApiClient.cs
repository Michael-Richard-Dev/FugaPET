using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente do CATALOGO de produtos (API_PRODUCT_SRV, OData V2). Apenas GET; nenhuma escrita, nenhum
/// CSRF. Montagem de URL e parse ficam isolados e testaveis, como nos demais clientes SAP do
/// projeto; a composicao HTTP e de configuracao fica na camada de servico.
/// <para>
/// SAO DUAS CONSULTAS, de proposito. <c>A_ProductPlant</c> responde "quais materiais existem neste
/// centro", mas NAO tem navegacao para o cadastro do material nesta instalacao — verificado na
/// $metadata: as navegacoes disponiveis sao to_PlantMRPArea, to_PlantStorage e afins, nenhuma para
/// A_Product, e <c>$expand=to_Product</c> responde HTTP 404. Logo, descricao, tipo e unidade saem de
/// <c>A_Product</c>, numa segunda consulta por lotes de codigos.
/// </para>
/// </summary>
public static class CatalogoProdutoSapApiClient
{
    private const string EntidadeCentro = "A_ProductPlant";
    private const string EntidadeProduto = "A_Product";

    private const string SelectCentro = "Product,Plant,IsBatchManagementRequired";
    private const string SelectProduto =
        "Product,ProductType,BaseUnit,ProductGroup,CrossPlantStatus,IsMarkedForDeletion";

    /// <summary>
    /// Teto de codigos por consulta de dados mestres. O filtro vira uma cadeia de "Product eq 'X' or
    /// ...", e URL muito longa e recusada pelo Gateway antes de qualquer erro util. 40 mantem a URL
    /// bem abaixo de qualquer limite usual e ainda traz os 155 produtos do centro em 4 chamadas.
    /// </summary>
    public const int TamanhoLoteCodigos = 40;

    /// <summary>
    /// Primeira pagina da listagem de materiais de um centro.
    /// <c>A_ProductPlant?$filter=Plant eq 'CENTRO'&amp;$select=...&amp;$top=N&amp;$format=json</c>.
    /// O centro e preservado como texto (Trim apenas): nada de conversao numerica, que comeria zeros
    /// a esquerda de centros como "0300".
    /// </summary>
    public static Uri MontarUrlProdutosDoCentro(
        Uri baseUri, string centro, string? sapClient = null, int tamanhoPagina = 200)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        string plant = (centro ?? string.Empty).Trim();
        if (plant.Length == 0)
        {
            throw new ArgumentException("Centro obrigatorio para listar o catalogo.", nameof(centro));
        }
        if (tamanhoPagina is < 1 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(tamanhoPagina));
        }

        string filtro = Uri.EscapeDataString($"Plant eq '{Escapar(plant)}'");
        return new Uri(
            $"{Raiz(baseUri)}/{EntidadeCentro}?$filter={filtro}"
            + $"&$select={Uri.EscapeDataString(SelectCentro)}"
            + $"&$top={tamanhoPagina}&$format=json{Cliente(sapClient)}");
    }

    /// <summary>
    /// Dados mestres de um LOTE de codigos: <c>A_Product?$filter=Product eq 'A' or Product eq 'B'</c>.
    /// Recusa lote vazio ou acima de <see cref="TamanhoLoteCodigos"/> — estourar o teto em silencio
    /// produziria uma URL que o Gateway rejeita sem dizer por que.
    /// </summary>
    public static Uri MontarUrlProdutosMestre(
        Uri baseUri, IReadOnlyList<string> codigos, string? sapClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(codigos);

        string[] limpos = [.. codigos
            .Select(c => (c ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)];

        if (limpos.Length == 0)
        {
            throw new ArgumentException("Nenhum codigo de produto informado.", nameof(codigos));
        }
        if (limpos.Length > TamanhoLoteCodigos)
        {
            throw new ArgumentException(
                $"Lote de {limpos.Length} codigos excede o teto de {TamanhoLoteCodigos}.", nameof(codigos));
        }

        StringBuilder filtro = new();
        for (int i = 0; i < limpos.Length; i++)
        {
            if (i > 0) filtro.Append(" or ");
            filtro.Append("Product eq '").Append(Escapar(limpos[i])).Append('\'');
        }

        return new Uri(
            $"{Raiz(baseUri)}/{EntidadeProduto}?$filter={Uri.EscapeDataString(filtro.ToString())}"
            + $"&$select={Uri.EscapeDataString(SelectProduto)}"
            + $"&$format=json{Cliente(sapClient)}");
    }

    /// <summary>
    /// Interpreta UMA pagina de A_ProductPlant. Envelope invalido devolve
    /// <see cref="PaginaCatalogoProdutoSap.Invalida"/> — nunca lanca, nunca devolve lista vazia como
    /// se fosse "centro sem produtos".
    /// </summary>
    public static PaginaCatalogoProdutoSap InterpretarPaginaDoCentro(string? corpoJson)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
        {
            return PaginaCatalogoProdutoSap.Invalida("corpo vazio");
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(corpoJson);
            if (!documento.RootElement.TryGetProperty("d", out JsonElement d))
            {
                return PaginaCatalogoProdutoSap.Invalida("envelope sem 'd'");
            }
            if (!d.TryGetProperty("results", out JsonElement results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return PaginaCatalogoProdutoSap.Invalida("envelope sem 'results' em array");
            }

            List<ProdutoDoCentroSap> itens = [];
            foreach (JsonElement item in results.EnumerateArray())
            {
                string produto = LerTexto(item, "Product");
                if (produto.Length == 0) continue;   // linha sem material nao e produto: ignorada.
                itens.Add(new ProdutoDoCentroSap
                {
                    Produto = produto,
                    Centro = LerTexto(item, "Plant"),
                    AdministradoPorLote = LerBooleano(item, "IsBatchManagementRequired")
                });
            }

            string? proxima = d.TryGetProperty("__next", out JsonElement n)
                && n.ValueKind == JsonValueKind.String ? n.GetString() : null;

            return new PaginaCatalogoProdutoSap
            {
                Itens = itens,
                ProximaPagina = string.IsNullOrWhiteSpace(proxima) ? null : proxima,
                Valida = true
            };
        }
        catch (JsonException)
        {
            return PaginaCatalogoProdutoSap.Invalida("json invalido");
        }
    }

    /// <summary>
    /// Interpreta a resposta de A_Product. Devolve lista vazia quando o envelope nao e
    /// interpretavel; quem consome trata ausencia de mestre como dado faltante, nunca como "produto
    /// sem tipo".
    /// </summary>
    public static IReadOnlyList<ProdutoMestreSapCatalogo> InterpretarProdutosMestre(string? corpoJson)
    {
        if (string.IsNullOrWhiteSpace(corpoJson)) return [];

        try
        {
            using JsonDocument documento = JsonDocument.Parse(corpoJson);
            if (!documento.RootElement.TryGetProperty("d", out JsonElement d)
                || !d.TryGetProperty("results", out JsonElement results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            List<ProdutoMestreSapCatalogo> itens = [];
            foreach (JsonElement item in results.EnumerateArray())
            {
                string produto = LerTexto(item, "Product");
                if (produto.Length == 0) continue;
                itens.Add(new ProdutoMestreSapCatalogo
                {
                    Produto = produto,
                    TipoMaterial = TextoOuNulo(item, "ProductType"),
                    UnidadeBase = TextoOuNulo(item, "BaseUnit"),
                    GrupoMercadoria = TextoOuNulo(item, "ProductGroup"),
                    StatusMaterial = TextoOuNulo(item, "CrossPlantStatus"),
                    MarcadoParaEliminacao = LerBooleano(item, "IsMarkedForDeletion")
                });
            }
            return itens;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Parte os codigos em lotes de <see cref="TamanhoLoteCodigos"/>, sem duplicatas.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> PartirEmLotes(IEnumerable<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        string[] limpos = [.. codigos
            .Select(c => (c ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)];

        List<IReadOnlyList<string>> lotes = [];
        for (int i = 0; i < limpos.Length; i += TamanhoLoteCodigos)
        {
            lotes.Add([.. limpos.Skip(i).Take(TamanhoLoteCodigos)]);
        }
        return lotes;
    }

    // ------------------------------------------------------------------
    private static string Raiz(Uri baseUri) => baseUri.ToString().TrimEnd('/');

    private static string Cliente(string? sapClient)
        => string.IsNullOrWhiteSpace(sapClient) ? string.Empty : $"&sap-client={sapClient.Trim()}";

    /// <summary>Aspa simples em literal OData se escreve dobrada. Sem isso, um codigo com apostrofo
    /// quebraria o filtro — ou, pior, mudaria o seu sentido.</summary>
    private static string Escapar(string valor) => valor.Replace("'", "''", StringComparison.Ordinal);

    private static string LerTexto(JsonElement item, string nome)
        => item.TryGetProperty(nome, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? (v.GetString() ?? string.Empty).Trim()
            : string.Empty;

    private static string? TextoOuNulo(JsonElement item, string nome)
    {
        string texto = LerTexto(item, nome);
        return texto.Length == 0 ? null : texto;
    }

    private static bool? LerBooleano(JsonElement item, string nome)
    {
        if (!item.TryGetProperty(nome, out JsonElement v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            // OData V2 as vezes serializa Edm.Boolean como "true"/"false" em texto.
            JsonValueKind.String when bool.TryParse(v.GetString(), out bool b) => b,
            _ => null
        };
    }
}
