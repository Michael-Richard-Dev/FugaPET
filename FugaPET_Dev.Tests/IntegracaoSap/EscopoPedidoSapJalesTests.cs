using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Regra de escopo Jales: grupo de compras 700 (cabecalho) + item no centro 3007 nao totalmente
/// entregue (_PurchaseOrderItem). So traz os itens elegiveis.
/// </summary>
public sealed class EscopoPedidoSapJalesTests
{
    [Fact]
    public void PedidoElegivel_GrupoECentroNaoEntregue_DeveSerElegivel()
        => Assert.True(EscopoPedidoSapJales.PedidoElegivel(
            Pedido("700", Item("3007", entregue: false))));

    [Fact]
    public void PedidoElegivel_GrupoDiferente_NaoDeveSerElegivel()
        => Assert.False(EscopoPedidoSapJales.PedidoElegivel(
            Pedido("001", Item("3007", entregue: false))));

    [Fact]
    public void PedidoElegivel_CentroDiferente_NaoDeveSerElegivel()
        => Assert.False(EscopoPedidoSapJales.PedidoElegivel(
            Pedido("700", Item("1410", entregue: false))));

    [Fact]
    public void PedidoElegivel_UnicoItemEntregue_NaoDeveSerElegivel()
        => Assert.False(EscopoPedidoSapJales.PedidoElegivel(
            Pedido("700", Item("3007", entregue: true))));

    [Fact]
    public void PedidoElegivel_EntregaNula_NaoDeveSerElegivel()
        => Assert.False(EscopoPedidoSapJales.PedidoElegivel(
            Pedido("700", Item("3007", entregue: null))));

    [Fact]
    public void FiltrarItensElegiveis_DeveTrazerApenasItensNaoEntreguesDoCentro()
    {
        PedidoCompraSap pedido = Pedido(
            "700",
            Item("3007", entregue: false, numero: "10"),
            Item("3007", entregue: true, numero: "20"),   // entregue → fora
            Item("1410", entregue: false, numero: "30"),  // outro centro → fora
            Item("3007", entregue: false, numero: "40"));

        PedidoCompraSap filtrado = EscopoPedidoSapJales.FiltrarItensElegiveis(pedido);

        Assert.Equal(["10", "40"], filtrado.Itens.Select(item => item.NumeroItem));
    }

    [Fact]
    public void ExpressaoFiltroPedido_DeveCombinarGrupoECentroNaoEntregue()
    {
        string filtro = EscopoPedidoSapJales.ExpressaoFiltroPedido();

        Assert.Contains("PurchasingGroup eq '700'", filtro, StringComparison.Ordinal);
        Assert.Contains("_PurchaseOrderItem/any(d:d/Plant eq '3007' and d/IsCompletelyDelivered eq false)", filtro, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpressaoFiltroItem_DeveExigirCentroNaoEntregue()
        => Assert.Equal("Plant eq '3007' and IsCompletelyDelivered eq false", EscopoPedidoSapJales.ExpressaoFiltroItem());

    private static PedidoCompraSap Pedido(string grupoCompra, params PedidoCompraSapItem[] itens)
        => new() { Numero = "4500000010", GrupoCompra = grupoCompra, Itens = itens };

    private static PedidoCompraSapItem Item(string centro, bool? entregue, string numero = "10")
        => new() { NumeroItem = numero, Centro = centro, CompletamenteEntregue = entregue };
}
