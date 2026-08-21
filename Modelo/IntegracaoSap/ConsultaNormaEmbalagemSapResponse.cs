using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// DTO CRU da resposta da API de norma/estrutura de embalagem do Produto Acabado (somente leitura).
/// Espelha exatamente os nomes JSON retornados pelo SAP, inclusive "Material Type" (com espaço) via
/// <see cref="JsonPropertyName"/>. Nenhuma interpretação aqui — a regra fica no interpretador.
/// Materiais permanecem STRING (nunca converter para número; nunca remover zeros à esquerda).
/// </summary>
public sealed class ConsultaNormaEmbalagemSapResponse
{
    [JsonPropertyName("Material")]
    public string Material { get; init; } = string.Empty;

    [JsonPropertyName("PackagingInstruction")]
    public string PackagingInstruction { get; init; } = string.Empty;

    [JsonPropertyName("PkgInstructionItems")]
    public IReadOnlyList<ConsultaNormaEmbalagemSapItemResponse> PkgInstructionItems { get; init; } = [];
}

/// <summary>Item cru da estrutura de embalagem (P = candidato a material de embalagem; I = produto acondicionado).</summary>
public sealed class ConsultaNormaEmbalagemSapItemResponse
{
    [JsonPropertyName("Material")]
    public string Material { get; init; } = string.Empty;

    [JsonPropertyName("Material Type")]
    public string MaterialType { get; init; } = string.Empty;

    [JsonPropertyName("Quantity")]
    public string Quantity { get; init; } = string.Empty;

    [JsonPropertyName("QtyUOM")]
    public string QtyUOM { get; init; } = string.Empty;

    [JsonPropertyName("Item")]
    public string Item { get; init; } = string.Empty;
}
