using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Aplica a governanca central antes de delegar ao servico real.</summary>
internal sealed class PedidoCompraSapGovernadoServico : IPedidoCompraSapServico
{
    private readonly IPedidoCompraSapServico _servicoInterno;
    private readonly EstadoIntegracaoSapServico _estadoIntegracaoSapServico;

    public PedidoCompraSapGovernadoServico(
        IPedidoCompraSapServico servicoInterno,
        EstadoIntegracaoSapServico estadoIntegracaoSapServico)
    {
        _servicoInterno = servicoInterno;
        _estadoIntegracaoSapServico = estadoIntegracaoSapServico;
    }

    public bool EhSimulado => false;
    public bool SapConfigurado => _servicoInterno.SapConfigurado;
    public bool EscritaSapHabilitada => _servicoInterno.EscritaSapHabilitada;

    public Task<DiagnosticoEstadoIntegracaoSap> DiagnosticarAsync(
        CancellationToken cancellationToken = default)
        => _estadoIntegracaoSapServico.DiagnosticarAsync(cancellationToken);

    public Task RegistrarFalhaStatusLocalAposSapAsync(
        long codigoLancamento,
        string mensagem,
        CancellationToken cancellationToken = default)
        => _servicoInterno is SincronizacaoPedidoCompraSapServico servico
            ? servico.RegistrarFalhaStatusLocalAposSapAsync(
                codigoLancamento,
                mensagem,
                cancellationToken)
            : Task.CompletedTask;

    public async Task<ResultadoOperacao> SincronizarPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Sincronizacao,
            cancellationToken);
        return validacao.Sucesso
            ? await _servicoInterno.SincronizarPedidoAsync(numeroPedido, cancellationToken)
            : validacao;
    }

    public async Task<IReadOnlyList<string>> ListarNumerosAsync(
        CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _servicoInterno.ListarNumerosAsync(cancellationToken);
    }

    public async Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _servicoInterno.ObterPedidoAgregadoAsync(
            numeroPedido,
            cancellationToken);
    }

    public async Task<string> ObterFornecedorPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _servicoInterno.ObterFornecedorPorPedidoAsync(numeroPedido, cancellationToken);
    }

    public async Task<DateOnly?> ObterDataPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _servicoInterno.ObterDataPorPedidoAsync(numeroPedido, cancellationToken);
    }

    public async Task<string> ObterTipoPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _servicoInterno.ObterTipoPorPedidoAsync(numeroPedido, cancellationToken);
    }

    public async Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        await ExigirConsultaAsync(cancellationToken);
        return await _servicoInterno.ListarItensPorPedidoAsync(numeroPedido, cancellationToken);
    }

    public async Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Escrita,
            cancellationToken);
        return validacao.Sucesso
            ? await _servicoInterno.AtualizarPesoItemSapAsync(
                numeroPedido,
                numeroItem,
                pesoLiquido,
                pesoBruto,
                cancellationToken)
            : validacao;
    }

    private async Task ExigirConsultaAsync(CancellationToken cancellationToken)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Consulta,
            cancellationToken);
        if (!validacao.Sucesso)
        {
            throw new IntegracaoSapBloqueadaException(validacao.Mensagem);
        }
    }
}
