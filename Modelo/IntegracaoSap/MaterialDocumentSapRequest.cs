namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Cabecalho do documento de material (movimento 101) a ser criado no SAP via
/// API_MATERIAL_DOCUMENT_SRV (POST A_MaterialDocumentHeader). Um documento por lancamento de
/// Entrada de Produto, com N itens elegiveis (peso liquido &gt; 0, unidade KG).
/// </summary>
public sealed record MaterialDocumentSapRequest
{
    /// <summary>GoodsMovementCode — fixo "01" (entrada de mercadoria para pedido de compra).</summary>
    public string GoodsMovementCode { get; init; } = "01";

    /// <summary>Data de lancamento (PostingDate). Serializada em OData V2 /Date(ms)/ pelo cliente.</summary>
    public DateTime PostingDate { get; init; }

    /// <summary>Data do documento (DocumentDate). Serializada em OData V2 /Date(ms)/ pelo cliente.</summary>
    public DateTime DocumentDate { get; init; }

    /// <summary>Texto de cabecalho curto e sanitizado (pedido + lancamento).</summary>
    public string MaterialDocumentHeaderText { get; init; } = string.Empty;

    /// <summary>Itens do documento (to_MaterialDocumentItem.results).</summary>
    public IReadOnlyList<MaterialDocumentSapItemRequest> Itens { get; init; } = [];
}
