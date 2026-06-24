using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Seam unico de integracao SAP da Entrada de Produto (H9 Etapa 5). Encapsula o
/// <see cref="IPedidoCompraSapServico"/> (sincronizacao/consulta de pedido e PATCH de peso), o
/// estado de integracao (simulado/configurado) e o registro de diagnostico, para que a tela e o
/// controller nao falem direto com a implementacao SAP. A escolha mock/real continua sendo da
/// fabrica (FabricaPedidoCompraSapServico).
/// </summary>
public sealed class IntegracaoEntradaSapServico
{
    private readonly IPedidoCompraSapServico _pedidoCompra;

    public IntegracaoEntradaSapServico(IPedidoCompraSapServico pedidoCompra)
    {
        _pedidoCompra = pedidoCompra ?? throw new ArgumentNullException(nameof(pedidoCompra));
        if (_pedidoCompra.EhSimulado
            && !global::FugaPET_Dev.AcessoDados.Banco.EstadoIntegracaoBanco.PodeUsarDadosSimulados)
        {
            throw new InvalidOperationException(
                "Servico SAP simulado proibido com banco habilitado ou fora de ambiente demonstrativo.");
        }
    }

    public bool EhSimulado => _pedidoCompra.EhSimulado;

    public bool SapConfigurado => _pedidoCompra.SapConfigurado;

    /// <summary>Escrita SAP habilitada (chave FUGAPET_SAP_WRITE_ENABLED / Sap:EscritaHabilitada).</summary>
    public bool EscritaSapHabilitada => _pedidoCompra.EscritaSapHabilitada;

    public async Task<DiagnosticoProntidaoIntegracaoSap> DiagnosticarProntidaoEscritaAsync(
        CancellationToken cancellationToken = default)
    {
        DiagnosticoEstadoIntegracaoSap estado = _pedidoCompra is PedidoCompraSapGovernadoServico governado
            ? await governado.DiagnosticarAsync(cancellationToken)
            : new DiagnosticoEstadoIntegracaoSap(
                AmbienteOperacional: true,
                IntegracaoAtiva: true,
                SapConfigurado: _pedidoCompra.SapConfigurado,
                MotivoBloqueio: null);

        return new DiagnosticoProntidaoIntegracaoSap(
            estado.AmbienteOperacional,
            estado.IntegracaoAtiva,
            estado.SapConfigurado,
            _pedidoCompra.EscritaSapHabilitada,
            estado.MotivoBloqueio);
    }

    public Task<ResultadoOperacao> SincronizarPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _pedidoCompra.SincronizarPedidoAsync(numeroPedido, cancellationToken);

    public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _pedidoCompra.ObterPedidoAgregadoAsync(numeroPedido, cancellationToken);

    public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        CancellationToken cancellationToken = default)
        => _pedidoCompra.AtualizarPesoItemSapAsync(numeroPedido, numeroItem, pesoLiquido, pesoBruto, cancellationToken);

    public Task RegistrarFalhaStatusLocalAposSapAsync(
        long codigoLancamento,
        string mensagem,
        CancellationToken cancellationToken = default)
        => _pedidoCompra is PedidoCompraSapGovernadoServico governado
            ? governado.RegistrarFalhaStatusLocalAposSapAsync(
                codigoLancamento,
                mensagem,
                cancellationToken)
            : Task.CompletedTask;

    /// <summary>Registro de diagnostico (best-effort) da integracao SAP.</summary>
    public void RegistrarDiagnostico(string mensagem)
        => SincronizacaoPedidoCompraSapServico.RegistrarDiagnostico(mensagem);
}

public sealed record DiagnosticoProntidaoIntegracaoSap(
    bool AmbienteOperacional,
    bool IntegracaoAtiva,
    bool SapConfigurado,
    bool EscritaSapHabilitada,
    string? MotivoBloqueio);
