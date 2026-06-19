namespace FugaPET_Dev.Modelo.IntegracaoSap;

public sealed record ResultadoConsultaPedidosSap(
    IReadOnlyList<PedidoCompraSap> Pedidos,
    int PaginasProcessadas,
    int ItensProcessados);
