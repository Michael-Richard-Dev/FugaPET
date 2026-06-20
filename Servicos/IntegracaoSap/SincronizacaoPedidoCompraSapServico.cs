using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;
using System.Diagnostics;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Orquestra a carga dos pedidos de compra no SAP e grava no cache local
/// sap_pedido_compra. Tambem expoe a leitura dos numeros para a tela.
/// </summary>
public sealed class SincronizacaoPedidoCompraSapServico : IPedidoCompraSapServico
{
    private readonly ConfiguracaoSap _configuracaoSap;
    private readonly SapPedidoCompraRepositorio _repositorio;
    private readonly Lazy<PedidoCompraSapApiClient> _clienteSap;
    private readonly ILogIntegracaoSapServico _logIntegracaoSapServico;

    // Mensagem unica ao operador quando o pedido nao pode ser usado na entrada: nao encontrado no
    // SAP OU fora do escopo autorizado. Nao distingue os dois casos de proposito (nao revela ao
    // operador se o numero existe no SAP); o motivo tecnico fica registrado no log de integracao.
    internal const string MensagemPedidoNaoLiberado = "Pedido nao liberado para entrada.";

    public SincronizacaoPedidoCompraSapServico()
        : this(
            LeitorConfiguracaoSap.Carregar(),
            new SapPedidoCompraRepositorio(new FabricaConexaoPostgreSql(LeitorConfiguracaoBancoPostgreSql.Carregar())))
    {
    }

    public SincronizacaoPedidoCompraSapServico(
        ConfiguracaoSap configuracaoSap,
        SapPedidoCompraRepositorio repositorio)
        : this(configuracaoSap, repositorio, clienteSap: null, logIntegracaoSapServico: null)
    {
    }

    internal SincronizacaoPedidoCompraSapServico(
        ConfiguracaoSap configuracaoSap,
        SapPedidoCompraRepositorio repositorio,
        ILogIntegracaoSapServico logIntegracaoSapServico)
        : this(configuracaoSap, repositorio, clienteSap: null, logIntegracaoSapServico)
    {
    }

    internal SincronizacaoPedidoCompraSapServico(
        ConfiguracaoSap configuracaoSap,
        SapPedidoCompraRepositorio repositorio,
        PedidoCompraSapApiClient? clienteSap,
        ILogIntegracaoSapServico? logIntegracaoSapServico = null)
    {
        _configuracaoSap = configuracaoSap;
        _repositorio = repositorio;
        _logIntegracaoSapServico =
            logIntegracaoSapServico ?? LogIntegracaoSapNuloServico.Instancia;
        _clienteSap = new Lazy<PedidoCompraSapApiClient>(
            () => clienteSap ?? new PedidoCompraSapApiClient(
                    _configuracaoSap,
                    FabricaHttpClientSap.Criar(_configuracaoSap)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<ResultadoOperacao> SincronizarPedidoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        Stopwatch cronometro = Stopwatch.StartNew();

        if (!_configuracaoSap.Configurado)
        {
            ResultadoOperacao falha =
                ResultadoOperacao.Falha(ConfiguracaoSap.MensagemConfiguracaoAusente);
            await RegistrarLogAsync(
                "CONSULTA_PEDIDO",
                numeroPedido,
                correlationId,
                cronometro,
                "BLOQUEADO",
                null,
                falha.Mensagem);
            return falha;
        }

        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return ResultadoOperacao.Falha("Informe o numero do pedido de compra.");
        }

        try
        {
            PedidoCompraSap? pedido = await _clienteSap.Value.ConsultarPedidoAsync(
                numeroPedido,
                cancellationToken);
            if (pedido is null)
            {
                await RegistrarLogAsync(
                    "CONSULTA_PEDIDO",
                    numeroPedido,
                    correlationId,
                    cronometro,
                    "NAO_ENCONTRADO",
                    404,
                    "Pedido nao encontrado no SAP.");
                return ResultadoOperacao.Falha(MensagemPedidoNaoLiberado);
            }

            if (pedido.Itens.Count == 0)
            {
                ResultadoOperacao semItens =
                    ResultadoOperacao.Falha("Pedido de compra encontrado, mas sem itens.");
                await RegistrarLogAsync(
                    "CONSULTA_PEDIDO",
                    numeroPedido,
                    correlationId,
                    cronometro,
                    "BLOQUEADO",
                    200,
                    semItens.Mensagem);
                return semItens;
            }

            // Escopo Jales: so persiste no cache pedidos dentro do escopo autorizado (grupo de
            // compras 700, nao totalmente entregue e com item no centro 3007). GET por chave do
            // OData nao aceita $filter, entao a regra e reaplicada aqui (fonte unica de escopo).
            if (!EscopoPedidoSapJales.PedidoElegivel(pedido))
            {
                await RegistrarLogAsync(
                    "CONSULTA_PEDIDO",
                    numeroPedido,
                    correlationId,
                    cronometro,
                    "BLOQUEADO",
                    200,
                    "Pedido fora do escopo autorizado (grupo de compras / centro / entrega).");
                return ResultadoOperacao.Falha(MensagemPedidoNaoLiberado);
            }

            // Traz para o cache somente os itens elegiveis (centro 3007 e nao totalmente entregues).
            PedidoCompraSap pedidoElegivel = EscopoPedidoSapJales.FiltrarItensElegiveis(pedido);
            int processados = await _repositorio.SincronizarAsync([pedidoElegivel], cancellationToken);
            ResultadoOperacao sucesso =
                ResultadoOperacao.Ok("Pedido de compra atualizado pelo SAP.", processados);
            await RegistrarLogAsync(
                "CONSULTA_PEDIDO",
                numeroPedido,
                correlationId,
                cronometro,
                "SUCESSO",
                200,
                null);
            return sucesso;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarLogAsync(
                "CONSULTA_PEDIDO",
                numeroPedido,
                correlationId,
                cronometro,
                "CANCELADO",
                null,
                "Consulta cancelada.");
            throw;
        }
        catch (TaskCanceledException)
        {
            ResultadoOperacao timeout =
                ResultadoOperacao.Falha("A consulta do pedido no SAP excedeu o tempo limite.");
            await RegistrarLogAsync(
                "CONSULTA_PEDIDO",
                numeroPedido,
                correlationId,
                cronometro,
                "TIMEOUT",
                null,
                timeout.Mensagem);
            return timeout;
        }
        catch
        {
            ResultadoOperacao erro = ResultadoOperacao.Falha(
                "Nao foi possivel consultar o pedido de compra no SAP.");
            await RegistrarLogAsync(
                "CONSULTA_PEDIDO",
                numeroPedido,
                correlationId,
                cronometro,
                "ERRO",
                null,
                "Falha tecnica durante a consulta do pedido.");
            return erro;
        }
    }

    /// <summary>
    /// Infraestrutura administrativa para uma futura carga completa autorizada.
    /// Nao pertence ao contrato usado pela Tela de Entrada e nao e chamada automaticamente.
    /// </summary>
    internal async Task<ResultadoOperacao> SincronizarCargaCompletaAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_configuracaoSap.Configurado)
        {
            return ResultadoOperacao.Falha(ConfiguracaoSap.MensagemConfiguracaoAusente);
        }

        Guid identificadorExecucao = Guid.NewGuid();
        Stopwatch cronometro = Stopwatch.StartNew();
        long codigoExecucao = 0;
        int paginas = 0;
        int pedidosRecebidos = 0;
        int itensRecebidos = 0;

        try
        {
            codigoExecucao = await _repositorio.IniciarExecucaoSincronizacaoAsync(
                identificadorExecucao,
                cancellationToken);

            ResultadoConsultaPedidosSap consulta =
                await _clienteSap.Value.ConsultarCargaCompletaAsync(cancellationToken);
            paginas = consulta.PaginasProcessadas;
            pedidosRecebidos = consulta.Pedidos.Count;
            itensRecebidos = consulta.ItensProcessados;

            // H6: NAO filtrar por Incoterms. Escopo Jales: traz apenas itens elegiveis (centro 3007
            // nao entregues) e descarta pedidos que ficaram sem item elegivel. O $filter de cabecalho
            // ja restringe por grupo/centro, mas os itens entregues ainda vem no expand.
            IReadOnlyList<PedidoCompraSap> pedidosElegiveis = consulta.Pedidos
                .Select(EscopoPedidoSapJales.FiltrarItensElegiveis)
                .Where(pedido => pedido.Itens.Count > 0)
                .ToList();
            int processados = await _repositorio.SincronizarCargaCompletaAsync(
                pedidosElegiveis,
                cancellationToken);

            cronometro.Stop();
            await _repositorio.FinalizarExecucaoSincronizacaoAsync(
                codigoExecucao,
                "SUCESSO",
                paginas,
                processados,
                itensRecebidos,
                cronometro.ElapsedMilliseconds,
                erroSanitizado: null,
                cancellationToken);
            await RegistrarLogAsync(
                "CARGA_COMPLETA",
                identificadorExecucao.ToString("D"),
                identificadorExecucao,
                cronometro,
                "SUCESSO",
                200,
                null);

            return ResultadoOperacao.Ok(
                $"Sincronizacao SAP concluida. Execucao {identificadorExecucao:D}.",
                processados);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cronometro.Stop();
            await RegistrarTerminoSeguroAsync(
                codigoExecucao,
                "CANCELADO",
                paginas,
                pedidosRecebidos,
                itensRecebidos,
                cronometro.ElapsedMilliseconds,
                "Sincronizacao cancelada.");
            await RegistrarLogAsync(
                "CARGA_COMPLETA",
                identificadorExecucao.ToString("D"),
                identificadorExecucao,
                cronometro,
                "CANCELADO",
                null,
                "Sincronizacao cancelada.");
            throw;
        }
        catch (SincronizacaoSapIncompletaException ex)
        {
            cronometro.Stop();
            paginas = ex.PaginasProcessadas;
            pedidosRecebidos = ex.RegistrosProcessados;
            itensRecebidos = ex.ItensProcessados;
            await RegistrarTerminoSeguroAsync(
                codigoExecucao,
                "PARCIAL",
                paginas,
                pedidosRecebidos,
                itensRecebidos,
                cronometro.ElapsedMilliseconds,
                ex.Message);
            await RegistrarLogAsync(
                "CARGA_COMPLETA",
                identificadorExecucao.ToString("D"),
                identificadorExecucao,
                cronometro,
                "PARCIAL",
                null,
                ex.Message);
            return ResultadoOperacao.Falha(
                "A sincronizacao SAP foi interrompida sem alterar o cache completo.");
        }
        catch
        {
            cronometro.Stop();
            await RegistrarTerminoSeguroAsync(
                codigoExecucao,
                "ERRO",
                paginas,
                pedidosRecebidos,
                itensRecebidos,
                cronometro.ElapsedMilliseconds,
                "Falha tecnica durante a sincronizacao SAP.");
            await RegistrarLogAsync(
                "CARGA_COMPLETA",
                identificadorExecucao.ToString("D"),
                identificadorExecucao,
                cronometro,
                "ERRO",
                null,
                "Falha tecnica durante a sincronizacao SAP.");
            return ResultadoOperacao.Falha("Nao foi possivel concluir a sincronizacao SAP.");
        }
    }

    private async Task RegistrarTerminoSeguroAsync(
        long codigoExecucao,
        string status,
        int paginas,
        int pedidos,
        int itens,
        long duracaoMs,
        string erroSanitizado)
    {
        if (codigoExecucao <= 0)
        {
            return;
        }

        try
        {
            await _repositorio.FinalizarExecucaoSincronizacaoAsync(
                codigoExecucao,
                status,
                paginas,
                pedidos,
                itens,
                duracaoMs,
                erroSanitizado,
                CancellationToken.None);
        }
        catch
        {
            // Falha de telemetria nao substitui a falha original da sincronizacao.
        }
    }

    /// <summary>Numeros de pedido disponiveis no cache local (para o combo da tela de Entrada).</summary>
    public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
        => _repositorio.ListarNumerosAsync(cancellationToken);

    public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
        => _repositorio.ObterPedidoAgregadoAsync(numeroPedido, cancellationToken);

    /// <summary>Fornecedor vinculado ao pedido no cache local, para preencher a tela de Entrada.</summary>
    public Task<string> ObterFornecedorPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _repositorio.ObterFornecedorPorNumeroPedidoAsync(numeroPedido, cancellationToken);

    /// <summary>Data do pedido no cache local, para preencher a tela de Entrada.</summary>
    public Task<DateOnly?> ObterDataPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _repositorio.ObterDataPorNumeroPedidoAsync(numeroPedido, cancellationToken);

    /// <summary>Tipo do pedido no cache local, para preencher a tela de Entrada.</summary>
    public Task<string> ObterTipoPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _repositorio.ObterTipoPorNumeroPedidoAsync(numeroPedido, cancellationToken);

    /// <summary>Itens (material + descricao) vinculados ao pedido no cache local, para o grid da tela.</summary>
    public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
        => _repositorio.ListarItensPorNumeroPedidoAsync(numeroPedido, cancellationToken);

    public bool EhSimulado => false;

    /// <summary>True quando ha credenciais para chamar o SAP real (PATCH so faz sentido configurado).</summary>
    public bool SapConfigurado => _configuracaoSap.Configurado;

    public bool EscritaSapHabilitada => _configuracaoSap.Configurado && _configuracaoSap.EscritaHabilitada;

    internal static void RegistrarDiagnostico(string _) { }

    /// <summary>
    /// Replica no SAP o peso capturado: PATCH no item alterando peso liquido e bruto
    /// (ItemNetWeight/ItemGrossWeight). Usado apos gravar a pesagem no banco.
    /// </summary>
    public async Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
        string numeroPedido,
        string numeroItem,
        decimal pesoLiquido,
        decimal pesoBruto,
        CancellationToken cancellationToken = default)
    {
        Guid correlationId = Guid.NewGuid();
        Stopwatch cronometro = Stopwatch.StartNew();
        string chaveNegocio = $"{numeroPedido}/{numeroItem}";

        if (!_configuracaoSap.Configurado)
        {
            return ResultadoOperacao.Falha(ConfiguracaoSap.MensagemConfiguracaoAusente);
        }

        if (!_configuracaoSap.EscritaHabilitada)
        {
            return ResultadoOperacao.Falha(ConfiguracaoSap.MensagemEscritaBloqueada);
        }

        if (!AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.EntradaProduto,
                PermissoesSistema.Acoes.EnviarSap)
            && !AutorizacaoEntradaProdutoServico.PossuiPermissao(
                PermissoesSistema.Acoes.EnviarSap))
        {
            return ResultadoOperacao.Falha(
                AutorizacaoServico.MensagemSemPermissao(
                    PermissoesSistema.Modulos.ProcessoProducao,
                    PermissoesSistema.Rotinas.EntradaProduto,
                    PermissoesSistema.Acoes.EnviarSap));
        }

        if (string.IsNullOrWhiteSpace(numeroPedido)
            || string.IsNullOrWhiteSpace(numeroItem)
            || pesoBruto <= 0
            || pesoLiquido < 0
            || pesoLiquido > pesoBruto)
        {
            return ResultadoOperacao.Falha("Dados de peso invalidos para atualizacao no SAP.");
        }

        try
        {
            ResultadoPatchSap resultado = await _clienteSap.Value.AtualizarPesoItemAsync(
                numeroPedido,
                numeroItem,
                pesoLiquido,
                pesoBruto,
                cancellationToken: cancellationToken);

            await RegistrarLogAsync(
                "ATUALIZAR_PESO_ITEM",
                chaveNegocio,
                correlationId,
                cronometro,
                resultado.Sucesso ? "SUCESSO" : "ERRO",
                resultado.HttpStatus,
                resultado.Sucesso ? null : resultado.Mensagem);

            return resultado.Sucesso
                ? ResultadoOperacao.Ok(resultado.Mensagem)
                : ResultadoOperacao.Falha(resultado.Mensagem);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RegistrarLogAsync(
                "ATUALIZAR_PESO_ITEM",
                chaveNegocio,
                correlationId,
                cronometro,
                "CANCELADO",
                null,
                "Atualizacao cancelada.");
            throw;
        }
        catch
        {
            await RegistrarLogAsync(
                "ATUALIZAR_PESO_ITEM",
                chaveNegocio,
                correlationId,
                cronometro,
                "ERRO",
                null,
                "Falha tecnica durante a atualizacao do peso.");
            return ResultadoOperacao.Falha(
                "Nao foi possivel atualizar o peso no SAP. A pesagem local foi preservada.");
        }
    }

    private async Task RegistrarLogAsync(
        string operacao,
        string? chaveNegocio,
        Guid correlationId,
        Stopwatch cronometro,
        string situacao,
        int? statusHttp,
        string? mensagemTecnica)
    {
        cronometro.Stop();
        await _logIntegracaoSapServico.RegistrarAsync(
            new RegistroLogIntegracaoSap
            {
                TipoIntegracao = "SAP_ODATA",
                Operacao = operacao,
                Entidade = "PEDIDO_COMPRA",
                ChaveNegocio = chaveNegocio,
                StatusHttp = statusHttp,
                DuracaoMs = cronometro.ElapsedMilliseconds,
                CorrelationId = correlationId,
                Situacao = situacao,
                Tentativa = 1,
                MensagemTecnicaSanitizada = mensagemTecnica,
                RegistradoEmUtc = DateTimeOffset.UtcNow
            },
            CancellationToken.None);
    }
}
