using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Item do documento de material (movimento 101) a ser criado no SAP via API_MATERIAL_DOCUMENT_SRV.
/// Os valores ja vem normalizados/validados pelo servico (numero do item com 5 digitos, quantidade
/// formatada na cultura invariante). Nao contem segredo nem peso bruto: o SAP recebe so o liquido.
/// </summary>
public sealed record MaterialDocumentSapItemRequest
{
    /// <summary>Material — codigo do material do item do pedido/cache local.</summary>
    public string Material { get; init; } = string.Empty;

    /// <summary>Plant — centro do item.</summary>
    public string Plant { get; init; } = string.Empty;

    /// <summary>StorageLocation — deposito de entrada do item.</summary>
    public string StorageLocation { get; init; } = string.Empty;

    /// <summary>GoodsMovementType — tipo de movimento; fixo "101" (entrada por pedido de compra).</summary>
    public string GoodsMovementType { get; init; } = "101";

    /// <summary>
    /// GoodsMovementRefDocType — tipo do documento de referencia do movimento. "B" = Pedido de Compra.
    /// Obrigatorio para o SAP aceitar PurchaseOrder/PurchaseOrderItem no movimento 101 (sem ele,
    /// retorna "Property PURCHASEORDER is not supported for GoodsMovementType 101").
    /// </summary>
    [JsonPropertyName("GoodsMovementRefDocType")]
    public string GoodsMovementRefDocType { get; init; } = "B";

    /// <summary>QuantityInEntryUnit — peso liquido consolidado (string, cultura invariante).</summary>
    public string QuantityInEntryUnit { get; init; } = string.Empty;

    /// <summary>EntryUnit — unidade de medida do item (apenas KG suportado no envio automatico).</summary>
    public string EntryUnit { get; init; } = string.Empty;

    /// <summary>PurchaseOrder — numero do pedido de compra.</summary>
    public string PurchaseOrder { get; init; } = string.Empty;

    /// <summary>PurchaseOrderItem — item do pedido normalizado para o SAP (5 digitos, ex.: "00010").</summary>
    public string PurchaseOrderItem { get; init; } = string.Empty;
}
