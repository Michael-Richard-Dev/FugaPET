using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Servico SAP em estado de ERRO DE CONFIGURACAO (arquivo malformado). NAO e mock e NAO executa
/// nenhuma operacao SAP: toda operacao falha com mensagem operacional segura. Garante que um JSON
/// malformado nao caia silenciosamente em "nao configurado" nem acione o servico simulado.
/// </summary>
internal sealed class PedidoCompraSapConfiguracaoInvalidaServico : IPedidoCompraSapServico
{
    public bool EhSimulado => false;
    public bool SapConfigurado => false;
    public bool EscritaSapHabilitada => false;

    public Task<ResultadoOperacao> SincronizarPedidoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoOperacao.Falha(ConfiguracaoSap.MensagemConfiguracaoInvalida));

    public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoOperacao.Falha(ConfiguracaoSap.MensagemConfiguracaoInvalida));

    public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);

    public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);

    public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);

    public Task<string> ObterFornecedorPorPedidoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);

    public Task<DateOnly?> ObterDataPorPedidoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);

    public Task<string> ObterTipoPorPedidoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);

    public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(
        string numeroPedido, CancellationToken cancellationToken = default)
        => throw new IntegracaoSapBloqueadaException(ConfiguracaoSap.MensagemConfiguracaoInvalida);
}
