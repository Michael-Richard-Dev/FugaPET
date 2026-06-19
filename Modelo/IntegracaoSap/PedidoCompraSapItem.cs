namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Item de um pedido de compra vindo do SAP (API_PURCHASEORDER_2), preparado para a
/// carga no cache local <c>homologacao.sap_pedido_compra_item</c>.
/// </summary>
public sealed record PedidoCompraSapItem
{
    /// <summary>PK local do item (homologacao.sap_pedido_compra_item). 0 quando vindo da API.</summary>
    public long CodigoItem { get; init; }

    /// <summary>PurchaseOrderItem — numero do item do pedido.</summary>
    public string NumeroItem { get; init; } = string.Empty;

    /// <summary>Material — codigo do material do item.</summary>
    public string? CodigoMaterial { get; init; }

    /// <summary>PurchaseOrderItemText — descricao do material do item (mapeia <c>descricao_produto</c>).</summary>
    public string? Descricao { get; init; }

    /// <summary>OrderQuantity — quantidade pedida do item (mapeia <c>quantidade_pedida</c>).</summary>
    public decimal? Quantidade { get; init; }

    /// <summary>PurchaseOrderQuantityUnit — unidade de medida da quantidade (mapeia <c>unidade_medida</c>).</summary>
    public string? UnidadeMedida { get; init; }

    /// <summary>ItemNetWeight — peso liquido do item (mapeia <c>peso_item</c>).</summary>
    public decimal? PesoItem { get; init; }

    /// <summary>Plant — centro do item (mapeia <c>centro</c>). Usado no filtro de escopo da Entrada.</summary>
    public string? Centro { get; init; }

    /// <summary>StorageLocation — deposito do item (mapeia <c>deposito</c>).</summary>
    public string? Deposito { get; init; }

    /// <summary>MaterialGroup — grupo de material do item (mapeia <c>grupo_material</c>).</summary>
    public string? GrupoMaterial { get; init; }

    /// <summary>JSON original do item retornado pela API.</summary>
    public string? PayloadOriginalJson { get; init; }
}
