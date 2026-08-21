using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// DTO tipado do POST de criação de Handling Unit de caixa (API_HANDLINGUNIT, OData V4/0001), com os
/// nomes JSON EXATOS confirmados pelo Ares. HandlingUnitExternalID temporário = "$1"; Warehouse sempre
/// vazio; WeightUnit="KG"/WeightUnitISOCode="KGM". Decimais serializam como NÚMERO JSON. NÃO carrega
/// OP/correlation_id/usuario/terminal/tara — esses permanecem apenas locais. Nunca contém credenciais.
/// </summary>
public sealed class HandlingUnitCaixaRequest
{
    [JsonPropertyName("HandlingUnitExternalID")]
    public string HandlingUnitExternalId { get; init; } = "$1";

    [JsonPropertyName("GrossWeight")]
    public decimal GrossWeight { get; init; }

    [JsonPropertyName("NetWeight")]
    public decimal NetWeight { get; init; }

    [JsonPropertyName("WeightUnit")]
    public string WeightUnit { get; init; } = "KG";

    [JsonPropertyName("WeightUnitISOCode")]
    public string WeightUnitIsoCode { get; init; } = "KGM";

    [JsonPropertyName("Warehouse")]
    public string Warehouse { get; init; } = string.Empty;

    [JsonPropertyName("Plant")]
    public string Plant { get; init; } = string.Empty;

    [JsonPropertyName("StorageLocation")]
    public string StorageLocation { get; init; } = string.Empty;

    [JsonPropertyName("PackagingMaterial")]
    public string PackagingMaterial { get; init; } = string.Empty;

    [JsonPropertyName("_HandlingUnitItem")]
    public IReadOnlyList<HandlingUnitCaixaItemRequest> HandlingUnitItem { get; init; } = [];
}

/// <summary>Item da HU (conteúdo). HandlingUnitTypeOfContent="1"; HandlingUnitQuantity é número JSON.</summary>
public sealed class HandlingUnitCaixaItemRequest
{
    [JsonPropertyName("HandlingUnitExternalID")]
    public string HandlingUnitExternalId { get; init; } = "$1";

    [JsonPropertyName("HandlingUnitTypeOfContent")]
    public string HandlingUnitTypeOfContent { get; init; } = "1";

    [JsonPropertyName("Plant")]
    public string Plant { get; init; } = string.Empty;

    [JsonPropertyName("StorageLocation")]
    public string StorageLocation { get; init; } = string.Empty;

    [JsonPropertyName("Material")]
    public string Material { get; init; } = string.Empty;

    [JsonPropertyName("Batch")]
    public string Batch { get; init; } = string.Empty;

    [JsonPropertyName("HandlingUnitQuantity")]
    public decimal HandlingUnitQuantity { get; init; }

    [JsonPropertyName("HandlingUnitQuantityUnit")]
    public string HandlingUnitQuantityUnit { get; init; } = string.Empty;
}
