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
    private readonly Lazy<IMaterialDocumentSapServico> _materialDocument;

    public IntegracaoEntradaSapServico(
        IPedidoCompraSapServico pedidoCompra,
        IMaterialDocumentSapServico? materialDocument = null)
    {
        _pedidoCompra = pedidoCompra ?? throw new ArgumentNullException(nameof(pedidoCompra));
        // Lazy: a composicao real (fabrica) so e criada quando o envio/diagnostico de Material
        // Document e exercido — testes injetam um fake e nao tocam a fabrica/banco.
        _materialDocument = new Lazy<IMaterialDocumentSapServico>(
            () => materialDocument ?? FabricaMaterialDocumentSapServico.Criar(),
            LazyThreadSafetyMode.ExecutionAndPublication);
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

    /// <summary>True quando ha URL de Material Document para criar o movimento 101 da Entrada.</summary>
    public bool MaterialDocumentConfigurado => _materialDocument.Value.MaterialDocumentConfigurado;

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
            MaterialDocumentConfigurado,
            estado.MotivoBloqueio);
    }

    public Task<ResultadoOperacao> SincronizarPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _pedidoCompra.SincronizarPedidoAsync(numeroPedido, cancellationToken);

    /// <summary>
    /// Pre-carrega o cache local dos pedidos relevantes da Entrada (carga filtrada + paginada),
    /// usado pelo servico de pre-carregamento em segundo plano apos o login. Sem servico governado
    /// (mock), e um no-op de sucesso para nao exigir SAP em ambiente demonstrativo.
    /// </summary>
    public Task<ResultadoOperacao> PreCarregarCacheEntradaAsync(CancellationToken cancellationToken = default)
        => _pedidoCompra is PedidoCompraSapGovernadoServico governado
            ? governado.PreCarregarCacheEntradaAsync(cancellationToken)
            : Task.FromResult(ResultadoOperacao.Ok("Pre-carregamento nao aplicavel (servico simulado)."));

    public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _pedidoCompra.ObterPedidoAgregadoAsync(numeroPedido, cancellationToken);

    /// <summary>Tarefa Entrada 23.1: cabeçalho fresco do pedido (status de aprovação/liberação) para validação.</summary>
    public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _pedidoCompra.ObterCabecalhoSapParaValidacaoAsync(numeroPedido, cancellationToken);

    public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        CancellationToken cancellationToken = default)
        => _pedidoCompra.AtualizarPesoItemSapAsync(numeroPedido, numeroItem, pesoLiquido, pesoBruto, cancellationToken);

    /// <summary>
    /// Cria o documento de material (movimento 101) da Entrada de Produto via
    /// API_MATERIAL_DOCUMENT_SRV. A governanca (ambiente, integracao ativa, configuracao, permissao
    /// ENVIAR_SAP) e a escrita habilitada sao reaplicadas dentro do servico governado/real. Este
    /// caminho NAO altera o Pedido de Compra (que permanece somente consulta/cache).
    /// </summary>
    public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterialEntradaAsync(
        MaterialDocumentSapRequest requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
        => _materialDocument.Value.CriarDocumentoMaterial101Async(requisicao, chaveNegocio, cancellationToken);

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
    bool MaterialDocumentConfigurado,
    string? MotivoBloqueio);
