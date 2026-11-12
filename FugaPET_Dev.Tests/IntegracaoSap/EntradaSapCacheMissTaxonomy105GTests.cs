using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 105G — taxonomia SAP preservada no CACHE MISS, provada END-TO-END no seam do controller
/// (ConsultarPedidoAsync → cache miss → SincronizarPedidoAsync → falha classificada →
/// ResultadoConsultaPedido). O 105D corrigiu apenas a validação fresca; o cache miss ainda
/// degradava tudo para "Pedido não encontrado no cache local e SAP indisponível no momento.".
///
/// Sem DB, sem SAP real: o serviço de integração é um fake que lança a falha TIPADA.
/// </summary>
public sealed class EntradaSapCacheMissTaxonomy105GTests
{
    // ---------------- fake de integração (cache miss + falha classificada) ----------------

    private sealed class FakeSapCacheMiss : IPedidoCompraSapServico
    {
        /// <summary>Exceção lançada por SincronizarPedidoAsync (falha técnica classificada).</summary>
        public Exception? ExcecaoSincronizacao { get; init; }

        /// <summary>Resultado quando não há exceção (para cenários NÃO técnicos).</summary>
        public ResultadoOperacao? ResultadoSincronizacao { get; init; }

        public bool SapConfiguradoValor { get; init; } = true;
        public bool EhSimuladoValor { get; init; }

        /// <summary>Marca se o cabeçalho fresco (validação de liberação) chegou a ser consultado.</summary>
        public bool CabecalhoConsultado { get; private set; }

        public bool EhSimulado => EhSimuladoValor;
        public bool SapConfigurado => SapConfiguradoValor;
        public bool EscritaSapHabilitada => false;

        // Cache SEMPRE vazio ⇒ força o caminho de cache miss.
        public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
            string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSapAgregado?>(null);

        public Task<ResultadoOperacao> SincronizarPedidoAsync(
            string numeroPedido, CancellationToken cancellationToken = default)
            => ExcecaoSincronizacao is not null
                ? Task.FromException<ResultadoOperacao>(ExcecaoSincronizacao)
                : Task.FromResult(ResultadoSincronizacao ?? ResultadoOperacao.Ok("ok"));

        // Se isto for chamado numa falha técnica, o validador de negócio foi alcançado indevidamente.
        public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(
            string numeroPedido, CancellationToken cancellationToken = default)
        {
            CabecalhoConsultado = true;
            return Task.FromResult<PedidoCompraSap?>(
                new PedidoCompraSap { Numero = numeroPedido, StatusProcessamentoCompraSap = "05" });
        }

        public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
            string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoOperacao.Ok());
        public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string> ObterFornecedorPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<DateOnly?> ObterDataPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<DateOnly?>(null);
        public Task<string> ObterTipoPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PedidoCompraSapItem>>([]);
    }

    private static EntradaProdutoController CriarController(FakeSapCacheMiss sap)
        => new(
            new IntegracaoEntradaSapServico(sap),
            new EntradaProdutoServico(null!, null!, null!, null, new AutorizacaoCentroDepositoEntrada([], []), null),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            new AutorizacaoCentroDepositoEntrada([], []),
            FabricaControladoresCadastro.CriarTaraController());

    private const string Pedido = "4500000030";

    /// <summary>Executa o cache miss com a falha técnica indicada e devolve o resultado do controller.</summary>
    private static async Task<(ResultadoConsultaPedido Resultado, FakeSapCacheMiss Sap)> ExecutarCacheMissAsync(
        CenarioFalhaConsultaSap cenario,
        int? httpStatus)
    {
        FakeSapCacheMiss sap = new()
        {
            ExcecaoSincronizacao = new ConsultaSapException(
                cenario, httpStatus, "diagnostico tecnico sanitizado", "corr-105g")
        };
        EntradaProdutoController controller = CriarController(sap);
        return (await controller.ConsultarPedidoAsync(Pedido), sap);
    }

    // Textos de negócio proibidos em qualquer falha técnica.
    private static void AssertSemVeredictoDeNegocio(ResultadoConsultaPedido r)
    {
        Assert.DoesNotContain("Pedido de Compra não liberado", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ainda não liberado/aprovado", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SAP indisponível no momento", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("não encontrado no cache local", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(r.MotivoBloqueioLiberacao);
        Assert.Empty(r.StatusProcessamento);
        Assert.Empty(r.DescricaoStatusProcessamento);
    }

    // ==================================================================
    // §11 — MATRIZ CACHE MISS OBRIGATÓRIA
    // ==================================================================

    [Fact]
    public async Task CacheMiss_401_AcessoNaoAutorizado()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.NaoAutenticado, 401);

        Assert.Equal(CenarioFalhaConsultaSap.NaoAutenticado, r.CenarioFalhaSap);
        Assert.Equal("Acesso ao SAP não autorizado", r.TituloFalha);
        Assert.Equal(401, r.HttpStatusFalha);
        Assert.False(r.ConsultaTecnicaOk);
        Assert.True(r.FalhaTecnicaSap);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado); // validador NÃO alcançado
    }

    [Fact]
    public async Task CacheMiss_403_AcessoNegado()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.SemAutorizacao, 403);

        Assert.Equal(CenarioFalhaConsultaSap.SemAutorizacao, r.CenarioFalhaSap);
        Assert.Equal("Acesso ao serviço SAP negado", r.TituloFalha);
        Assert.Equal(403, r.HttpStatusFalha);
        Assert.DoesNotContain("senha", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact]
    public async Task CacheMiss_404_PedidoNaoEncontrado()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.NaoEncontrado, 404);

        Assert.Equal(CenarioFalhaConsultaSap.NaoEncontrado, r.CenarioFalhaSap);
        Assert.Equal("Pedido de Compra não encontrado", r.TituloFalha);
        Assert.Equal(404, r.HttpStatusFalha);
        Assert.Contains(Pedido, r.Mensagem, StringComparison.Ordinal);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact]
    public async Task CacheMiss_Timeout_SapNaoRespondeu()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.Timeout, null);

        Assert.Equal(CenarioFalhaConsultaSap.Timeout, r.CenarioFalhaSap);
        Assert.Equal("SAP não respondeu", r.TituloFalha);
        Assert.DoesNotContain("credencia", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact]
    public async Task CacheMiss_Rede_FalhaDeComunicacao()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.Conectividade, null);

        Assert.Equal(CenarioFalhaConsultaSap.Conectividade, r.CenarioFalhaSap);
        Assert.Equal("Falha de comunicação com o SAP", r.TituloFalha);
        Assert.DoesNotContain("credencia", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact]
    public async Task CacheMiss_Tls_FalhaDeSeguranca()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.Tls, null);

        Assert.Equal(CenarioFalhaConsultaSap.Tls, r.CenarioFalhaSap);
        Assert.Equal("Falha de segurança na conexão com o SAP", r.TituloFalha);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task CacheMiss_5xx_SapIndisponivel_ComStatusPreservado(int status)
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.SapIndisponivel, status);

        Assert.Equal(CenarioFalhaConsultaSap.SapIndisponivel, r.CenarioFalhaSap);
        Assert.Equal("SAP indisponível", r.TituloFalha);
        Assert.Equal(status, r.HttpStatusFalha);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact]
    public async Task CacheMiss_RespostaInvalida()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.RespostaInvalida, 200);

        Assert.Equal(CenarioFalhaConsultaSap.RespostaInvalida, r.CenarioFalhaSap);
        Assert.Equal("Resposta do SAP inválida", r.TituloFalha);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact]
    public async Task CacheMiss_ConfiguracaoInvalida_PorExcecaoTipada()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.ConfiguracaoInvalida, null);

        Assert.Equal(CenarioFalhaConsultaSap.ConfiguracaoInvalida, r.CenarioFalhaSap);
        Assert.Equal("Configuração da integração SAP inválida", r.TituloFalha);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact] // IntegracaoSapBloqueadaException (config malformada) também classifica CONFIGURACAO_INVALIDA.
    public async Task CacheMiss_IntegracaoBloqueada_ClassificaConfiguracaoInvalida()
    {
        FakeSapCacheMiss sap = new()
        {
            ExcecaoSincronizacao = new IntegracaoSapBloqueadaException("configuracao invalida")
        };
        ResultadoConsultaPedido r = await CriarController(sap).ConsultarPedidoAsync(Pedido);

        Assert.Equal(CenarioFalhaConsultaSap.ConfiguracaoInvalida, r.CenarioFalhaSap);
        Assert.False(r.ConsultaTecnicaOk);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    [Fact] // Sincronização falha e a integração NÃO está configurada ⇒ CONFIGURACAO_INVALIDA (sinal estrutural).
    public async Task CacheMiss_SincronizacaoFalha_SemConfiguracao_ClassificaConfiguracaoInvalida()
    {
        FakeSapCacheMiss sap = new()
        {
            SapConfiguradoValor = false,
            EhSimuladoValor = false,
            ResultadoSincronizacao = ResultadoOperacao.Falha("configuracao da integracao invalida")
        };
        ResultadoConsultaPedido r = await CriarController(sap).ConsultarPedidoAsync(Pedido);

        Assert.Equal(CenarioFalhaConsultaSap.ConfiguracaoInvalida, r.CenarioFalhaSap);
        Assert.False(r.ConsultaTecnicaOk);
        AssertSemVeredictoDeNegocio(r);
    }

    [Fact]
    public async Task CacheMiss_HttpNaoClassificado_FailClosed()
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) =
            await ExecutarCacheMissAsync(CenarioFalhaConsultaSap.HttpNaoClassificado, 418);

        Assert.Equal(CenarioFalhaConsultaSap.HttpNaoClassificado, r.CenarioFalhaSap);
        Assert.False(r.Sucesso);            // fail-closed: não abre operação
        Assert.False(r.ConsultaTecnicaOk);
        Assert.Empty(r.ItensAutorizados);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado);
    }

    // ==================================================================
    // §11 — invariantes comuns a TODOS os cenários técnicos do cache miss
    // ==================================================================

    [Theory]
    [InlineData(CenarioFalhaConsultaSap.NaoAutenticado, 401)]
    [InlineData(CenarioFalhaConsultaSap.SemAutorizacao, 403)]
    [InlineData(CenarioFalhaConsultaSap.NaoEncontrado, 404)]
    [InlineData(CenarioFalhaConsultaSap.Timeout, null)]
    [InlineData(CenarioFalhaConsultaSap.Conectividade, null)]
    [InlineData(CenarioFalhaConsultaSap.Tls, null)]
    [InlineData(CenarioFalhaConsultaSap.SapIndisponivel, 503)]
    [InlineData(CenarioFalhaConsultaSap.RespostaInvalida, 200)]
    [InlineData(CenarioFalhaConsultaSap.ConfiguracaoInvalida, null)]
    [InlineData(CenarioFalhaConsultaSap.HttpNaoClassificado, 418)]
    public async Task CacheMiss_TodosOsCenarios_NaoAbremOperacaoNemEmitemVeredicto(
        CenarioFalhaConsultaSap cenario,
        int? httpStatus)
    {
        (ResultadoConsultaPedido r, FakeSapCacheMiss sap) = await ExecutarCacheMissAsync(cenario, httpStatus);

        Assert.False(r.Sucesso);
        Assert.False(r.ConsultaTecnicaOk);
        Assert.Equal(cenario, r.CenarioFalhaSap);
        Assert.NotEmpty(r.TituloFalha);
        Assert.NotEmpty(r.Mensagem);
        Assert.Empty(r.ItensAutorizados);
        AssertSemVeredictoDeNegocio(r);
        Assert.False(sap.CabecalhoConsultado); // ValidadorLiberacaoPedidoCompra nunca é alcançado
    }

    // ==================================================================
    // §12 — falha NÃO técnica da sincronização preserva a mensagem real
    // ==================================================================

    [Theory]
    [InlineData("Pedido de compra encontrado, mas sem itens.")]
    [InlineData("Pedido nao liberado para entrada.")] // fora do escopo autorizado (decisão funcional)
    public async Task CacheMiss_FalhaNaoTecnica_PreservaMensagemFuncional(string mensagemReal)
    {
        FakeSapCacheMiss sap = new()
        {
            ResultadoSincronizacao = ResultadoOperacao.Falha(mensagemReal)
        };
        ResultadoConsultaPedido r = await CriarController(sap).ConsultarPedidoAsync(Pedido);

        Assert.False(r.Sucesso);
        Assert.Equal(mensagemReal, r.Mensagem);
        // Não virou "SAP indisponível" nem cenário HTTP inventado.
        Assert.Equal(CenarioFalhaConsultaSap.Nenhum, r.CenarioFalhaSap);
        Assert.True(r.ConsultaTecnicaOk);
        Assert.Null(r.HttpStatusFalha);
        Assert.DoesNotContain("SAP indisponível", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(r.MotivoBloqueioLiberacao);
    }

    // ==================================================================
    // §13 — cancelamento do chamador é propagado (sem virar erro técnico)
    // ==================================================================

    [Fact]
    public async Task CacheMiss_CancelamentoDoChamador_Propaga()
    {
        FakeSapCacheMiss sap = new()
        {
            ExcecaoSincronizacao = new OperationCanceledException()
        };
        EntradaProdutoController controller = CriarController(sap);
        using CancellationTokenSource cancelamento = new();
        await cancelamento.CancelAsync();

        Exception erro = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.ConsultarPedidoAsync(
                Pedido,
                FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.MateriaPrima,
                cancelamento.Token));

        Assert.IsNotType<ConsultaSapException>(erro);
    }
}
