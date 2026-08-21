using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Cliente do GET da norma/estrutura de embalagem do Produto Acabado (somente leitura, contrato INT012).
/// Montagem de URL e parse ficam isolados/testáveis; a composição HTTP/config/allowlist fica na camada de
/// serviço. A consulta é por FILTRO OData (GetPackagingSet?$filter=Material eq '...'), nunca por chave
/// ('MATERIAL'). Sem POST/PATCH/CSRF. Nunca lança no parse: null quando não desserializável. Materiais
/// preservados como string (sem conversão numérica, sem trim de zeros à esquerda).
/// </summary>
public static class ProdutoAcabadoNormaEmbalagemSapApiClient
{
    /// <summary>
    /// Monta a URI de consulta por FILTRO: {endpoint}?$filter=Material eq 'X'[ and PackagingInstruction eq
    /// 'Y'][&amp;sap-client=Z]. Usa <see cref="UriBuilder"/>; escapa o literal OData duplicando apóstrofos;
    /// preserva o material como string (zeros à esquerda). sap-client SÓ é acrescentado quando informado.
    /// Nunca gera a sintaxe por chave ('MATERIAL').
    /// </summary>
    public static Uri MontarUrlConsulta(
        Uri baseUri,
        string material,
        string? packagingInstruction = null,
        string? sapClientOpcional = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        UriBuilder builder = new(baseUri)
        {
            Fragment = string.Empty
        };
        // Endpoint exato (ex.: .../GetPackagingSet) — remove barra final para não alterar a rota.
        builder.Path = builder.Path.TrimEnd('/');

        string filtro = $"Material eq '{EscaparLiteralOData(material)}'";
        if (!string.IsNullOrWhiteSpace(packagingInstruction))
        {
            filtro += $" and PackagingInstruction eq '{EscaparLiteralOData(packagingInstruction)}'";
        }

        string query = "$filter=" + filtro;
        if (!string.IsNullOrWhiteSpace(sapClientOpcional))
        {
            query += "&sap-client=" + sapClientOpcional.Trim();
        }

        builder.Query = query;
        return builder.Uri;
    }

    /// <summary>Escapa literal de string OData: duplica apóstrofos. Preserva zeros à esquerda (não converte número).</summary>
    private static string EscaparLiteralOData(string valor)
        => (valor ?? string.Empty).Trim().Replace("'", "''", StringComparison.Ordinal);

    /// <summary>Parse tolerante. Aceita objeto simples ou embrulho OData <c>d</c>; coleção em <c>results</c>/<c>value</c>.</summary>
    public static ConsultaNormaEmbalagemSapResponse? Parsear(string? json)
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

            // Coleção por filtro: OData V2 ("results") ou V4 ("value"); pega o primeiro registro.
            if (raiz.ValueKind == JsonValueKind.Object
                && (TentarArray(raiz, "results", out JsonElement colecao) || TentarArray(raiz, "value", out colecao)))
            {
                if (colecao.GetArrayLength() == 0)
                {
                    return null;
                }

                raiz = colecao[0];
            }

            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new ConsultaNormaEmbalagemSapResponse
            {
                Material = LerTexto(raiz, "Material"),
                PackagingInstruction = LerTexto(raiz, "PackagingInstruction"),
                PkgInstructionItems = LerItens(raiz)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TentarArray(JsonElement obj, string propriedade, out JsonElement array)
    {
        if (obj.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.Array)
        {
            array = valor;
            return true;
        }

        array = default;
        return false;
    }

    private static IReadOnlyList<ConsultaNormaEmbalagemSapItemResponse> LerItens(JsonElement raiz)
    {
        // Aceita defensivamente PkgInstructionItems e _PkgInstructionItems (navegação com "_" do Integration Suite).
        if (!raiz.TryGetProperty("PkgInstructionItems", out JsonElement itens)
            && !raiz.TryGetProperty("_PkgInstructionItems", out itens))
        {
            return [];
        }

        // Array direto ou embrulho OData { "results": [...] } / { "value": [...] }.
        if (itens.ValueKind == JsonValueKind.Object
            && (TentarArray(itens, "results", out JsonElement resultados) || TentarArray(itens, "value", out resultados)))
        {
            itens = resultados;
        }

        if (itens.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return itens.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new ConsultaNormaEmbalagemSapItemResponse
            {
                Material = LerTexto(item, "Material"),
                MaterialType = LerTexto(item, "Material Type"),
                Quantity = LerTexto(item, "Quantity"),
                QtyUOM = LerTexto(item, "QtyUOM"),
                Item = LerTexto(item, "Item")
            })
            .ToArray();
    }

    private static string LerTexto(JsonElement obj, string propriedade)
        => obj.TryGetProperty(propriedade, out JsonElement valor)
            ? valor.ValueKind switch
            {
                JsonValueKind.String => (valor.GetString() ?? string.Empty).Trim(),
                JsonValueKind.Number => valor.GetRawText().Trim(),
                _ => string.Empty
            }
            : string.Empty;
}
