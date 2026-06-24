using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// C12 - finalizacao local separada do envio CONTROLADO ao SAP. Garante que o Finalizar nao chama
/// PATCH e que o envio so executa PATCH com TODAS as travas (HOMOLOGACAO + permissao + escrita).
/// </summary>
public sealed class EnvioControladoSapEntradaC12Tests : IDisposable
{
    public EnvioControladoSapEntradaC12Tests()
    {
        // Connection string habilita o banco -> PossuiPermissao passa a honrar a sessao.
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    [Fact]
    public async Task FinalizarLeitura_NaoDeveChamarPatchSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new();
        EntradaProdutoController controller = CriarController(sap, ehHomologacao: true, itens: [Item()]);

        await controller.FinalizarLeituraAsync(Lancamento());

        Assert.Equal(0, sap.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_ComTodasAsTravas_DeveChamarPatchSomenteDosItens()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = true };
        EntradaProdutoController controller = CriarController(sap, ehHomologacao: true, itens: [Item("10"), Item("20")]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(2, sap.PatchChamadas);
        Assert.Equal(2, resultado.Enviados);
        Assert.True(resultado.StatusLocalAtualizado);
    }

    [Fact]
    public async Task Diagnosticar_ComTodasAsTravas_DeveLiberarEnvio()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = true };
        EntradaProdutoController controller =
            CriarController(sap, ehHomologacao: true, itens: [Item()]);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.True(diagnostico.PodeEnviar);
        Assert.Null(diagnostico.MotivoBloqueio);
        Assert.Equal(1, diagnostico.TotalItensPersistidos);
        Assert.True(diagnostico.IntegracaoSapAtiva);
    }

    [Fact]
    public async Task Diagnosticar_SemPermissao_DeveBloquear()
    {
        DefinirSessao(comEnviarSap: false);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            ehHomologacao: true,
            itens: [Item()]);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.False(diagnostico.PodeEnviar);
        Assert.False(diagnostico.UsuarioTemPermissao);
        Assert.Contains("ENVIAR_SAP", diagnostico.MotivoBloqueio);
    }

    [Fact]
    public async Task Diagnosticar_IntegracaoInativa_DeveBloquear()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            ehHomologacao: true,
            itens: [Item()],
            integracaoAtiva: false);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.False(diagnostico.PodeEnviar);
        Assert.False(diagnostico.IntegracaoSapAtiva);
        Assert.Contains("inativa", diagnostico.MotivoBloqueio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Diagnosticar_SapNaoConfigurado_DeveBloquear()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico
            {
                Configurado = false,
                EscritaHabilitada = true
            },
            ehHomologacao: true,
            itens: [Item()]);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.False(diagnostico.PodeEnviar);
        Assert.False(diagnostico.SapConfigurado);
        Assert.Contains("Configuração SAP", diagnostico.MotivoBloqueio);
    }

    [Fact]
    public async Task Diagnosticar_SemLancamentoOuItens_DeveBloquear()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            ehHomologacao: true,
            itens: []);

        DiagnosticoEnvioSapEntrada semLancamento =
            await controller.DiagnosticarEnvioSapEntradaAsync(null);
        DiagnosticoEnvioSapEntrada semItens =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.False(semLancamento.PodeEnviar);
        Assert.Contains("Finalize", semLancamento.MotivoBloqueio);
        Assert.False(semItens.PodeEnviar);
        Assert.Equal(0, semItens.TotalItensPersistidos);
    }

    [Fact]
    public async Task Enviar_SemPermissaoEnviarSap_NaoDeveChamarPatch()
    {
        DefinirSessao(comEnviarSap: false);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = true };
        EntradaProdutoController controller = CriarController(sap, ehHomologacao: true, itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.SemPermissao, resultado.Cenario);
        Assert.Equal(0, sap.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_AmbienteDiferenteDeHomologacao_NaoDeveChamarPatch()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = true };
        EntradaProdutoController controller = CriarController(sap, ehHomologacao: false, itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.AmbienteNaoHomologacao, resultado.Cenario);
        Assert.Equal(0, sap.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_EscritaDesabilitada_NaoDeveChamarPatch()
    {
        // EscritaHabilitada=false equivale a FUGAPET_SAP_WRITE_ENABLED ausente/false.
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = false };
        EntradaProdutoController controller = CriarController(sap, ehHomologacao: true, itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.EscritaDesabilitada, resultado.Cenario);
        Assert.Equal(0, sap.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_LancamentoSemItens_NaoDeveChamarPatch()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = true };
        EntradaProdutoController controller = CriarController(sap, ehHomologacao: true, itens: []);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoSemItens, resultado.Cenario);
        Assert.Equal(0, sap.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_SucessoTotal_DeveAtualizarStatusLocal()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new() { EscritaHabilitada = true };
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        EntradaProdutoController controller = CriarController(
            sap,
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, cenario, _) =>
            {
                cenarioPersistido = cenario;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado =
            await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Enviado, cenarioPersistido);
        Assert.True(resultado.StatusLocalAtualizado);
    }

    [Fact]
    public async Task Enviar_FalhaTotal_DeveAtualizarStatusLocalComoErro()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new()
        {
            EscritaHabilitada = true,
            PatchSucesso = false
        };
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        EntradaProdutoController controller = CriarController(
            sap,
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, cenario, _) =>
            {
                cenarioPersistido = cenario;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado =
            await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Falha, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Falha, cenarioPersistido);
        Assert.True(resultado.StatusLocalAtualizado);
    }

    [Fact]
    public async Task Enviar_RespostaSapComFalhaPersistencia_DeveSinalizarDivergenciaCritica()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, _, _) => Task.FromResult(
                ResultadoOperacao.Falha("Falha local.")));

        ResultadoEnvioSapEntrada resultado =
            await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.FalhaPersistenciaLocal, resultado.Cenario);
        Assert.False(resultado.StatusLocalAtualizado);
        Assert.NotNull(resultado.MensagemCritica);
    }

    [Fact]
    public async Task Enviar_Parcial_DevePersistirItensSemStatusParcialNoLancamento()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico sap = new()
        {
            EscritaHabilitada = true,
            ItensComFalha = new HashSet<string> { "20" }
        };
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        IReadOnlyList<ResultadoItemEnvioSap>? itensPersistidos = null;
        EntradaProdutoController controller = CriarController(
            sap,
            ehHomologacao: true,
            itens: [Item("10"), Item("20")],
            atualizarStatus: (_, itens, cenario, _) =>
            {
                cenarioPersistido = cenario;
                itensPersistidos = itens;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado =
            await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Parcial, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Parcial, cenarioPersistido);
        Assert.NotNull(itensPersistidos);
        Assert.Contains(itensPersistidos, item => item.NumeroItem == "10" && item.Sucesso);
        Assert.Contains(itensPersistidos, item => item.NumeroItem == "20" && !item.Sucesso);
    }

    private static EntradaProdutoController CriarController(
        FakePedidoCompraSapServico sap,
        bool ehHomologacao,
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens,
        bool integracaoAtiva = true,
        Func<
            long,
            IReadOnlyList<ResultadoItemEnvioSap>,
            CenarioEnvioSapEntrada,
            CancellationToken,
            Task<ResultadoOperacao>>? atualizarStatus = null)
        => new(
            new IntegracaoEntradaSapServico(sap),
            new EntradaProdutoServico(null!, null!, null!, null, new AutorizacaoCentroDepositoEntrada([], []), null),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            new AutorizacaoCentroDepositoEntrada([], []),
            FabricaControladoresCadastro.CriarTaraController(),
            ehAmbienteHomologacao: () => ehHomologacao,
            carregarItensParaEnvio: (_, _) => Task.FromResult(itens),
            diagnosticarIntegracaoSap: _ => Task.FromResult(
                new DiagnosticoProntidaoIntegracaoSap(
                    AmbienteOperacional: true,
                    IntegracaoAtiva: integracaoAtiva,
                    SapConfigurado: sap.SapConfigurado,
                    EscritaSapHabilitada: sap.EscritaSapHabilitada,
                    MotivoBloqueio: integracaoAtiva
                        ? null
                        : "Integração SAP inativa.")),
            atualizarStatusAposEnvioSap: atualizarStatus
                ?? ((_, _, _, _) => Task.FromResult(ResultadoOperacao.Ok())));

    private static void DefinirSessao(bool comEnviarSap)
    {
        IReadOnlyList<PermissaoSessaoAplicacao> permissoes = comEnviarSap
            ? [new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.ProcessoProducao,
                Rotina = PermissoesSistema.Rotinas.EntradaProduto,
                Acao = PermissoesSistema.Acoes.EnviarSap
            }]
            : [];

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 7,
            Login = "operador_teste",
            Nome = "Operador Teste",
            PerfisCodigo = ["OPERADOR"],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        });
    }

    private static EntradaProdutoItemEnvioSap Item(string numeroItem = "10")
        => new() { NumeroPedido = "4500000010", NumeroItem = numeroItem, PesoLiquidoKg = 8m, PesoBrutoKg = 10m };

    private static EntradaProdutoLancamento Lancamento()
        => new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem
            {
                NumeroItem = "10",
                Pesagens = [new EntradaProdutoPesagem
                {
                    PesoBrutoKg = 10m, PesoTaraKg = 2m, PesoLiquidoKg = 8m, Origem = "BALANCA", StatusPesagem = "VALIDA"
                }]
            }]
        };

    private sealed class FakePedidoCompraSapServico : IPedidoCompraSapServico
    {
        public bool Configurado { get; init; } = true;
        public bool EscritaHabilitada { get; init; }
        public bool PatchSucesso { get; init; } = true;
        public IReadOnlySet<string> ItensComFalha { get; init; } = new HashSet<string>();
        public int PatchChamadas { get; private set; }

        public bool EhSimulado => false;
        public bool SapConfigurado => Configurado;
        public bool EscritaSapHabilitada => EscritaHabilitada;

        public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
            string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto, CancellationToken cancellationToken = default)
        {
            PatchChamadas++;
            bool sucesso = PatchSucesso && !ItensComFalha.Contains(numeroItem);
            return Task.FromResult(
                sucesso
                    ? ResultadoOperacao.Ok("Peso atualizado no SAP.")
                    : ResultadoOperacao.Falha("SAP recusou o peso."));
        }

        public Task<ResultadoOperacao> SincronizarPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoOperacao.Ok());
        public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSapAgregado?>(null);
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
