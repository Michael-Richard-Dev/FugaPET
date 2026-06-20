namespace FugaPET_Dev.Modelo.Entrada;

public sealed record EntradaProdutoItemPersistido
{
    public long CodigoLancamento { get; init; }
    public long CodigoSapPedidoCompraItem { get; init; }
    public string NumeroPedido { get; init; } = string.Empty;
    public string NumeroItem { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string DescricaoMaterial { get; init; } = string.Empty;
    public string Fornecedor { get; init; } = string.Empty;
    public DateOnly? DataPedido { get; init; }
    public string Terminal { get; init; } = string.Empty;
    public decimal PesoLiquidoTotalKg { get; init; }
}
