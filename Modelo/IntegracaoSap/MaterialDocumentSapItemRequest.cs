using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Item do documento de material (movimento 101) a ser criado no SAP via API_MATERIAL_DOCUMENT_SRV.
/// Os valores ja vem normalizados/validados pelo servico (numero do item com 5 digitos, quantidade
/// formatada na cultura invariante). Nao contem segredo nem peso bruto: o SAP recebe so o liquido.
/// </summary>
public sealed record MaterialDocumentSapItemRequest
{
    /// <summary>Material â€” codigo do material do item do pedido/cache local.</summary>
    public string Material { get; init; } = string.Empty;

    /// <summary>Plant â€” centro do item.</summary>
    public string Plant { get; init; } = string.Empty;

    /// <summary>StorageLocation â€” deposito de entrada do item.</summary>
    public string StorageLocation { get; init; } = string.Empty;

    /// <summary>GoodsMovementType â€” tipo de movimento; fixo "101" (entrada por pedido de compra).</summary>
    public string GoodsMovementType { get; init; } = "101";

    /// <summary>
    /// GoodsMovementRefDocType â€” tipo do documento de referencia do movimento. "B" = Pedido de Compra.
    /// Obrigatorio para o SAP aceitar PurchaseOrder/PurchaseOrderItem no movimento 101 (sem ele,
    /// retorna "Property PURCHASEORDER is not supported for GoodsMovementType 101").
    /// </summary>
    [JsonPropertyName("GoodsMovementRefDocType")]
    public string? GoodsMovementRefDocType { get; init; }

    /// <summary>QuantityInEntryUnit â€” peso liquido consolidado (string, cultura invariante).</summary>
    public string QuantityInEntryUnit { get; init; } = string.Empty;

    /// <summary>EntryUnit â€” unidade de medida do item (apenas KG suportado no envio automatico).</summary>
    public string EntryUnit { get; init; } = string.Empty;

    /// <summary>PurchaseOrder â€” numero do pedido de compra.</summary>
    public string? PurchaseOrder { get; init; }

    /// <summary>PurchaseOrderItem â€” item do pedido normalizado para o SAP (5 digitos, ex.: "00010").</summary>
    public string? PurchaseOrderItem { get; init; }

    public string? ManufacturingOrder { get; init; }

    public string? ManufacturingOrderItem { get; init; }

    public string? Batch { get; init; }

    public string? MaterialDocumentItemText { get; init; }

    /// <summary>
    /// ManufactureDate — data civil de fabricação do lote. Opcional; só deve ser enviada quando houver
    /// fonte funcional autorizada. Serializada como data OData V2 sem deslocamento de fuso.
    /// </summary>
    [JsonPropertyName("ManufactureDate")]
    public DateTime? ManufactureDate { get; init; }

    /// <summary>
    /// ShelfLifeExpirationDate — data civil de validade do lote. Opcional; não deve ser calculada por
    /// atalho técnico. Serializada como data OData V2 sem deslocamento de fuso.
    /// </summary>
    [JsonPropertyName("ShelfLifeExpirationDate")]
    public DateTime? ShelfLifeExpirationDate { get; init; }
}



