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

    internal PedidoCompraSapMockServico(bool usoAutorizado)
    {
        if (!usoAutorizado)
        {
            throw new InvalidOperationException(
                "Mock SAP proibido fora de ambiente demonstrativo com banco desabilitado.");
        }
    }

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

    public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PedidoCompraSapAgregado?>(
            EhPedidoDemonstracao(numeroPedido)
                ? new PedidoCompraSapAgregado
                {
                    NumeroPedido = NumeroPedidoDemonstracao,
                    Fornecedor = "FORNECEDOR DEMO",
                    DataPedido = DateOnly.FromDateTime(DateTime.Today),
                    TipoPedido = "NB",
                    Itens = Itens
                }
                : null);

    public Task<string> ObterFornecedorPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult(EhPedidoDemonstracao(numeroPedido) ? "FORNECEDOR DEMO" : string.Empty);

    // Tarefa Entrada 23.1: no modo demonstracao o pedido demo entra como LIBERADO (status "05") para nao
    // travar a demo; qualquer outro numero retorna null (o validador bloqueia por seguranca).
    public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => Task.FromResult<PedidoCompraSap?>(
            EhPedidoDemonstracao(numeroPedido)
                ? new PedidoCompraSap
                {
                    Numero = NumeroPedidoDemonstracao,
                    StatusProcessamentoCompraSap = "05",
                    LiberacaoNaoConcluidaSap = false
                }
                : null);

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
