using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Entrada unica da integracao de pedidos de compra SAP usada pela aplicacao.
/// A selecao entre implementacao real e demonstracao pertence exclusivamente
/// a composicao em <see cref="FabricaPedidoCompraSapServico"/>.
/// </summary>
public interface IPedidoCompraSapServico
{
    bool EhSimulado { get; }
    bool SapConfigurado { get; }
    bool EscritaSapHabilitada { get; }

    Task<ResultadoOperacao> SincronizarPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default);
    Task<string> ObterFornecedorPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<DateOnly?> ObterDataPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<string> ObterTipoPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default);
    Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        CancellationToken cancellationToken = default);
}
