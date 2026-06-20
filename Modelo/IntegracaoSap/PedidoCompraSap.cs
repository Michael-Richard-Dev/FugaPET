namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Cabecalho de um pedido de compra vindo do SAP (API_PURCHASEORDER_2), preparado para a
/// carga no cache local <c>desenvolvimento.sap_pedido_compra</c>.
/// </summary>
public sealed record PedidoCompraSap
{
    /// <summary>PurchaseOrder — numero do pedido (mapeia <c>numero_pedido</c>).</summary>
    public string Numero { get; init; } = string.Empty;

    /// <summary>Supplier — codigo SAP do fornecedor, usado para resolver a FK <c>codigo_sap_fornecedor</c>.</summary>
    public string? FornecedorCodigoSap { get; init; }

    /// <summary>PurchaseOrderDate (mapeia <c>data_pedido</c>).</summary>
    public DateOnly? DataPedido { get; init; }

    /// <summary>DocumentCurrency (mapeia <c>moeda</c>).</summary>
    public string? Moeda { get; init; }

    /// <summary>PurchaseOrderType (mapeia <c>tipo_pedido</c>), limitado a 4 caracteres.</summary>
    public string? TipoPedido { get; init; }

    /// <summary>Status do pedido no SAP, quando disponivel (mapeia <c>status_pedido</c>).</summary>
    public string? Status { get; init; }

    /// <summary>PurchasingGroup — grupo de compras do cabecalho. Usado no filtro de escopo (Jales).</summary>
    public string? GrupoCompra { get; init; }

    /// <summary>IncotermsClassification do cabecalho (ex.: CIF), preservado para consulta.</summary>
    public string? IncotermsClassification { get; init; }

    /// <summary>IncotermsTransferLocation do cabecalho, preservado para consulta.</summary>
    public string? IncotermsTransferLocation { get; init; }

    /// <summary>IncotermsLocation1 do cabecalho, preservado para consulta.</summary>
    public string? IncotermsLocation1 { get; init; }

    /// <summary>JSON original do cabecalho retornado pela API (vai para <c>payload_original</c>).</summary>
    public string? PayloadOriginalJson { get; init; }

    /// <summary>Itens do pedido retornados pela navegacao _PurchaseOrderItem.</summary>
    public IReadOnlyList<PedidoCompraSapItem> Itens { get; init; } = [];
}
