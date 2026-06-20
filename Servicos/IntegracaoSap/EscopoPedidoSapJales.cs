using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Fonte UNICA do escopo de pedidos/itens autorizados para esta unidade (Jales/PET). A mesma
/// regra e aplicada de duas formas: como $filter OData na carga em massa do cache e como guarda
/// pos-fetch na sincronizacao por pedido (GET por chave do OData nao aceita $filter de topo).
///
/// Estrutura SAP (API_PURCHASEORDER_2): <c>PurchasingGroup</c> e do CABECALHO (PurchaseOrder);
/// <c>Plant</c> e <c>IsCompletelyDelivered</c> sao de ITEM (_PurchaseOrderItem). Por isso a
/// elegibilidade e avaliada por item e o pedido so e trazido se tiver ao menos um item elegivel.
///
/// Valores FIXOS, validados funcionalmente (2026-06-19): grupo de compras 700, item no centro 3007
/// e item ainda NAO totalmente entregue. Campos ausentes/nulos NAO sao elegiveis (semantica do
/// "eq" do OData, que exclui null/desconhecido).
/// </summary>
public static class EscopoPedidoSapJales
{
    public const string GrupoCompra = "700";
    public const string Centro = "3007";

    /// <summary>Item elegivel: centro 3007 e ainda nao totalmente entregue.</summary>
    public static bool ItemElegivel(PedidoCompraSapItem item)
        => item is not null
           && string.Equals(item.Centro?.Trim(), Centro, StringComparison.OrdinalIgnoreCase)
           && item.CompletamenteEntregue == false;

    /// <summary>
    /// Pedido elegivel: grupo de compras 700 (cabecalho) e com ao menos um item elegivel.
    /// </summary>
    public static bool PedidoElegivel(PedidoCompraSap pedido)
        => pedido is not null
           && string.Equals(pedido.GrupoCompra?.Trim(), GrupoCompra, StringComparison.OrdinalIgnoreCase)
           && pedido.Itens.Any(ItemElegivel);

    /// <summary>
    /// Retorna o mesmo pedido contendo SOMENTE os itens elegiveis (centro 3007, nao entregues).
    /// Use antes de persistir no cache para nao trazer itens fora do escopo.
    /// </summary>
    public static PedidoCompraSap FiltrarItensElegiveis(PedidoCompraSap pedido)
        => pedido with { Itens = pedido.Itens.Where(ItemElegivel).ToList() };

    /// <summary>
    /// Expressao OData (sem percent-encoding) para o $filter de CABECALHO da carga em massa:
    /// grupo 700 e pelo menos um item no centro 3007 nao totalmente entregue.
    /// </summary>
    public static string ExpressaoFiltroPedido()
        => $"PurchasingGroup eq '{GrupoCompra}'"
           + $" and _PurchaseOrderItem/any(d:d/Plant eq '{Centro}' and d/IsCompletelyDelivered eq false)";

    /// <summary>
    /// Expressao OData (sem percent-encoding) para o $filter dentro do $expand de itens:
    /// traz apenas itens do centro 3007 ainda nao totalmente entregues.
    /// </summary>
    public static string ExpressaoFiltroItem()
        => $"Plant eq '{Centro}' and IsCompletelyDelivered eq false";
}
