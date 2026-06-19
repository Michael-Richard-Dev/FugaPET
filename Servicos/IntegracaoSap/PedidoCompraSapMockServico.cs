using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Implementacao exclusiva do modo DEMONSTRACAO.</summary>
internal sealed class PedidoCompraSapMockServico : IPedidoCompraSapServico
{
    private const string NumeroPedidoDemonstracao = "4500000001";

    private static readonly IReadOnlyList<PedidoCompraSapItem> Itens =
    [
        new()
        {
            CodigoItem = 1,
            NumeroItem = "10",
            CodigoMaterial = "DEMO001",
            Descricao = "Item demonstrativo",
            Quantidade = 10m,
            UnidadeMedida = "KG",
            PesoItem = 10m
        }
    ];

    public bool EhSimulado => true;
    public bool SapConfigurado => false;
    public bool EscritaSapHabilitada => false;

    public Task<ResultadoOperacao> SincronizarPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EhPedidoDemonstracao(numeroPedido)
            ? ResultadoOperacao.Ok("Pedido demonstrativo carregado.", 1)
            : ResultadoOperacao.Falha("Pedido demonstrativo nao encontrado."));

    public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([NumeroPedidoDemonstracao]);

    public Task<string> ObterFornecedorPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EhPedidoDemonstracao(numeroPedido) ? "FORNECEDOR DEMO" : string.Empty);

    public Task<DateOnly?> ObterDataPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult<DateOnly?>(
            EhPedidoDemonstracao(numeroPedido) ? DateOnly.FromDateTime(DateTime.Today) : null);

    public Task<string> ObterTipoPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EhPedidoDemonstracao(numeroPedido) ? "NB" : string.Empty);

    public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EhPedidoDemonstracao(numeroPedido) ? Itens : []);

    public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoOperacao.Falha(
            "Modo demonstracao: nenhuma alteracao foi enviada ao SAP."));

    private static bool EhPedidoDemonstracao(string numeroPedido)
        => string.Equals(
            numeroPedido?.Trim(),
            NumeroPedidoDemonstracao,
            StringComparison.OrdinalIgnoreCase);
}
