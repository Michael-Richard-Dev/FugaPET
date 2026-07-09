using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Entrada;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.Entrada;

/// <summary>
/// Pre-carregamento assincrono dos pedidos da Entrada (aquecimento do cache apos o login) e
/// prioridade de CACHE LOCAL na consulta de pedido (GET especifico no SAP apenas como fallback).
/// </summary>
public sealed class PreCarregamentoPedidosEntradaTests
{
    public PreCarregamentoPedidosEntradaTests()
        => Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");

    // ---------- Servico de pre-carregamento ----------

    [Fact]
    public async Task Executar_CargaBemSucedida_DeveConcluirERegistrarHorario()
    {
        PreCarregamentoPedidosEntradaServico servico =
            new(_ => Task.FromResult(ResultadoOperacao.Ok("ok")));

        ResultadoPreCarregamentoEntrada resultado = await servico.ExecutarAsync();

        Assert.Equal(CenarioPreCarregamentoEntrada.Concluido, resultado.Cenario);
        Assert.NotNull(servico.UltimaConclusao);
    }

    [Fact]
    public async Task Executar_CargaIndisponivel_DeveRetornarIndisponivelSemHorario()
    {
        PreCarregamentoPedidosEntradaServico servico =
            new(_ => Task.FromResult(ResultadoOperacao.Falha("SAP indisponivel")));

        ResultadoPreCarregamentoEntrada resultado = await servico.ExecutarAsync();

        Assert.Equal(CenarioPreCarregamentoEntrada.Indisponivel, resultado.Cenario);
        Assert.Null(servico.UltimaConclusao);
    }

    [Fact]
    public async Task Executar_DuasVezesConcorrentes_NaoDeveRodarSimultaneamente()
    {
        TaskCompletionSource primeiraIniciou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PreCarregamentoPedidosEntradaServico servico = new(async _ =>
        {
            primeiraIniciou.TrySetResult();
            await liberar.Task;
            return ResultadoOperacao.Ok();
        });

        Task<ResultadoPreCarregamentoEntrada> primeira = servico.ExecutarAsync();
        await primeiraIniciou.Task; // a 1a ja adquiriu a trava

        ResultadoPreCarregamentoEntrada segunda = await servico.ExecutarAsync();
        Assert.Equal(CenarioPreCarregamentoEntrada.JaEmAndamento, segunda.Cenario);

        liberar.TrySetResult();
        Assert.Equal(CenarioPreCarregamentoEntrada.Concluido, (await primeira).Cenario);
    }

    [Fact]
    public async Task Executar_ComExcecao_NaoDeveDerrubarERetornaErro()
    {
        PreCarregamentoPedidosEntradaServico servico =
            new(_ => throw new InvalidOperationException("falha simulada"));

        ResultadoPreCarregamentoEntrada resultado = await servico.ExecutarAsync();

        Assert.Equal(CenarioPreCarregamentoEntrada.Erro, resultado.Cenario);
    }

    [Fact]
    public async Task Executar_ComTimeout_DeveRetornarTimeout()
    {
        PreCarregamentoPedidosEntradaServico servico = new(
            async ct => { await Task.Delay(Timeout.Infinite, ct); return ResultadoOperacao.Ok(); },
            registrarDiagnostico: null,
            timeout: TimeSpan.FromMilliseconds(50));

        ResultadoPreCarregamentoEntrada resultado = await servico.ExecutarAsync();

        Assert.Equal(CenarioPreCarregamentoEntrada.Timeout, resultado.Cenario);
    }

    [Fact]
    public async Task Executar_AposConclusao_PodeRodarNovamente()
    {
        PreCarregamentoPedidosEntradaServico servico =
            new(_ => Task.FromResult(ResultadoOperacao.Ok()));

        Assert.Equal(CenarioPreCarregamentoEntrada.Concluido, (await servico.ExecutarAsync()).Cenario);
        Assert.Equal(CenarioPreCarregamentoEntrada.Concluido, (await servico.ExecutarAsync()).Cenario);
    }

    // ---------- Cache-first na consulta de pedido ----------

    [Fact]
    public async Task Consultar_PedidoNoCacheLocal_NaoDeveChamarSap()
    {
        FakePedidoCache pedido = new();
        pedido.RespostasAgregado.Enqueue(PedidoExemplo("4500000010"));
        EntradaProdutoController controller = CriarController(pedido);

        ResultadoConsultaPedido resultado = await controller.ConsultarPedidoAsync("4500000010");

        Assert.True(resultado.Sucesso);
        Assert.Equal(0, pedido.SincronizacoesChamadas); // cache-hit nao chama o SAP
        Assert.Equal("4500000010", resultado.NumeroPedido);
    }

    [Fact]
    public async Task Consultar_PedidoForaDoCache_DeveFazerGetEspecificoNoSap()
    {
        FakePedidoCache pedido = new();
        pedido.RespostasAgregado.Enqueue(null);                       // 1a leitura: cache miss
        pedido.RespostasAgregado.Enqueue(PedidoExemplo("4500000020")); // pos-sincronizacao
        EntradaProdutoController controller = CriarController(pedido);

        ResultadoConsultaPedido resultado = await controller.ConsultarPedidoAsync("4500000020");

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, pedido.SincronizacoesChamadas); // cache miss -> 1 GET especifico
    }

    [Fact]
    public async Task Consultar_ForaDoCacheComSapIndisponivel_DeveRetornarMensagemClara()
    {
        FakePedidoCache pedido = new() { SincronizacaoSucesso = false };
        pedido.RespostasAgregado.Enqueue(null); // cache miss
        EntradaProdutoController controller = CriarController(pedido);

        ResultadoConsultaPedido resultado = await controller.ConsultarPedidoAsync("4500000030");

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "Pedido não encontrado no cache local e SAP indisponível no momento.",
            resultado.Mensagem);
    }

    private static EntradaProdutoController CriarController(FakePedidoCache pedido)
        => new(
            new IntegracaoEntradaSapServico(pedido),
            new EntradaProdutoServico(null!, null!, null!, null, new AutorizacaoCentroDepositoEntrada([], []), null),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            new AutorizacaoCentroDepositoEntrada([], []),
            FabricaControladoresCadastro.CriarTaraController());

    private static PedidoCompraSapAgregado PedidoExemplo(string numero)
        => new()
        {
            NumeroPedido = numero,
            Fornecedor = "Fornecedor Teste",
            TipoPedido = "NB",
            Itens = [new PedidoCompraSapItem { NumeroItem = "10", Centro = "3007", Deposito = "PP01" }]
        };

    /// <summary>
    /// Fake do servico de pedido: a fila <see cref="RespostasAgregado"/> modela cache-hit (1 pedido)
    /// ou cache-miss-depois-hit (null seguido do pedido); conta as chamadas de sincronizacao (GET SAP).
    /// </summary>
    private sealed class FakePedidoCache : IPedidoCompraSapServico
    {
        public Queue<PedidoCompraSapAgregado?> RespostasAgregado { get; } = new();
        public int SincronizacoesChamadas { get; private set; }
        public bool SincronizacaoSucesso { get; init; } = true;

        public bool EhSimulado => false;
        public bool SapConfigurado => true;
        public bool EscritaSapHabilitada => false;

        public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
            string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(RespostasAgregado.Count > 0 ? RespostasAgregado.Dequeue() : null);

        public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(
            string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSap?>(
                new PedidoCompraSap { Numero = numeroPedido, StatusProcessamentoCompraSap = "05" });

        public Task<ResultadoOperacao> SincronizarPedidoAsync(
            string numeroPedido, CancellationToken cancellationToken = default)
        {
            SincronizacoesChamadas++;
            return Task.FromResult(SincronizacaoSucesso
                ? ResultadoOperacao.Ok("Pedido atualizado pelo SAP.")
                : ResultadoOperacao.Falha("SAP indisponivel."));
        }

        public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
            string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto, CancellationToken cancellationToken = default)
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
}
