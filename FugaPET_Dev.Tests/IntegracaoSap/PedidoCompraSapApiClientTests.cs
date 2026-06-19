using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Trava o mapeamento do corpo OData (value[]) da API de Pedido de Compra para PedidoCompraSap,
/// sem depender de chamada HTTP real.
/// </summary>
public sealed class PedidoCompraSapApiClientTests
{
    [Fact]
    public void MapearColecao_DeveLerCamposDoCabecalho()
    {
        const string json = """
            {
              "value": [
                {
                  "PurchaseOrder": "4500000000",
                  "Supplier": "14300030",
                  "PurchaseOrderDate": "2026-06-11",
                  "DocumentCurrency": "BRL",
                  "PurchaseOrderType": "NB",
                  "IncotermsClassification": "CIF",
                  "IncotermsTransferLocation": "COTRISAL",
                  "IncotermsLocation1": "COTRISAL",
                  "_PurchaseOrderItem": [
                    {
                      "PurchaseOrderItem": "10",
                      "Material": "MAT-001",
                      "ItemNetWeight": 50.500
                    }
                  ]
                }
              ]
            }
            """;

        IReadOnlyList<PedidoCompraSap> pedidos = PedidoCompraSapApiClient.MapearColecao(json);

        Assert.Single(pedidos);
        PedidoCompraSap pedido = pedidos[0];
        Assert.Equal("4500000000", pedido.Numero);
        Assert.Equal("14300030", pedido.FornecedorCodigoSap);
        Assert.Equal(new DateOnly(2026, 6, 11), pedido.DataPedido);
        Assert.Equal("BRL", pedido.Moeda);
        Assert.Equal("NB", pedido.TipoPedido);
        Assert.Equal("CIF", pedido.IncotermsClassification);
        Assert.Equal("COTRISAL", pedido.IncotermsTransferLocation);
        Assert.Equal("COTRISAL", pedido.IncotermsLocation1);
        Assert.Single(pedido.Itens);
        Assert.Equal("10", pedido.Itens[0].NumeroItem);
        Assert.Equal("MAT-001", pedido.Itens[0].CodigoMaterial);
        Assert.Equal(50.500m, pedido.Itens[0].PesoItem);
        Assert.False(string.IsNullOrWhiteSpace(pedido.PayloadOriginalJson));
    }

    [Fact]
    public void MapearColecao_DeveIgnorarItensSemNumero()
    {
        const string json = """
            { "value": [ { "Supplier": "999" }, { "PurchaseOrder": "4500000007" } ] }
            """;

        IReadOnlyList<PedidoCompraSap> pedidos = PedidoCompraSapApiClient.MapearColecao(json);

        Assert.Single(pedidos);
        Assert.Equal("4500000007", pedidos[0].Numero);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("{ \"value\": [] }")]
    public void MapearColecao_SemRegistros_DeveRetornarVazio(string json)
    {
        IReadOnlyList<PedidoCompraSap> pedidos = PedidoCompraSapApiClient.MapearColecao(json);
        Assert.Empty(pedidos);
    }

    [Fact]
    public void MapearPagina_ComNextLink_DeveRetornarProximaPagina()
    {
        const string json = """
            {
              "value": [ { "PurchaseOrder": "4500000001" } ],
              "@odata.nextLink": "PurchaseOrder?$skiptoken=ABC&$select=PurchaseOrder"
            }
            """;

        (IReadOnlyList<PedidoCompraSap> pedidos, string? proximaPagina) = PedidoCompraSapApiClient.MapearPagina(json);

        Assert.Single(pedidos);
        Assert.Equal("PurchaseOrder?$skiptoken=ABC&$select=PurchaseOrder", proximaPagina);
    }

    [Fact]
    public void MapearPagina_UltimaPagina_NaoTemProxima()
    {
        const string json = """
            { "value": [ { "PurchaseOrder": "4500000099" } ] }
            """;

        (_, string? proximaPagina) = PedidoCompraSapApiClient.MapearPagina(json);

        Assert.Null(proximaPagina);
    }

    [Fact]
    public void MapearPedidoEspecifico_DeveLerCabecalhoEItens()
    {
        const string json = """
            {
              "PurchaseOrder": "4500000010",
              "Supplier": "14300001",
              "PurchaseOrderDate": "2025-10-20",
              "PurchaseOrderType": "NB",
              "IncotermsClassification": "CIF",
              "IncotermsTransferLocation": "COTRISAL",
              "IncotermsLocation1": "COTRISAL",
              "_PurchaseOrderItem": [
                {
                  "PurchaseOrderItem": "10",
                  "Material": "NL001",
                  "PurchaseOrderItemText": "Parafuso",
                  "OrderQuantity": 50,
                  "PurchaseOrderQuantityUnit": "ST",
                  "ItemNetWeight": 54.9
                }
              ]
            }
            """;

        PedidoCompraSap? pedido = PedidoCompraSapApiClient.MapearPedidoEspecifico(json);

        Assert.NotNull(pedido);
        Assert.Equal("4500000010", pedido!.Numero);
        Assert.Single(pedido.Itens);
        Assert.Equal("NL001", pedido.Itens[0].CodigoMaterial);
        Assert.Equal(54.9m, pedido.Itens[0].PesoItem);
    }

    [Fact]
    public void MapearPedidoEspecifico_SemItens_DeveRetornarPedidoVazio()
    {
        const string json = """{"PurchaseOrder":"4500000011","_PurchaseOrderItem":[]}""";

        PedidoCompraSap? pedido = PedidoCompraSapApiClient.MapearPedidoEspecifico(json);

        Assert.NotNull(pedido);
        Assert.Empty(pedido!.Itens);
    }
}
