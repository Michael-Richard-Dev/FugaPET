namespace FugaPET_Dev.Modelo.IntegracaoSap;

public sealed record PedidoCompraSapAgregado
{
    public string NumeroPedido { get; init; } = string.Empty;
    public string Fornecedor { get; init; } = string.Empty;
    public DateOnly? DataPedido { get; init; }
    public string TipoPedido { get; init; } = string.Empty;
    public IReadOnlyList<PedidoCompraSapItem> Itens { get; init; } = [];
}
