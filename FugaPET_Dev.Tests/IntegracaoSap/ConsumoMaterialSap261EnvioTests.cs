using System.Net;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Envio CONTROLADO do consumo 261 (API_MATERIAL_DOCUMENT_SRV): WRITE_ENABLED, CSRF, POST,
/// parse rigoroso, bloqueio de reenvio e confirmacao local. Tudo com fakes â€” sem SAP real.
/// </summary>
public sealed class ConsumoMaterialSap261EnvioTests
{
    private static readonly DateTime DataUtc = new(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc);

    private static ConfiguracaoSap Config(bool escrita)
        => new()
        {
            MaterialDocumentBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
            Usuario = "user",
            Senha = "pass",
            SapClient = "110",
            HostsPermitidos = ["sap.example.com"],
            EscritaHabilitada = escrita
        };

    private static ConfiguracaoSap ConfigSemMaterialDocument(bool escrita = true)
        => new()
        {
            Usuario = "user",
            Senha = "pass",
            SapClient = "110",
            HostsPermitidos = ["sap.example.com"],
            EscritaHabilitada = escrita
        };

    private static ConfiguracaoSap ConfigConfirmacao(bool escrita)
        => new()
        {
            ProductionOrderConfirmationBaseUrl = "https://sap.example.com:44300/sap/opu/odata/sap/API_PROD_ORDER_CONFIRMATION_2_SRV/",
            Usuario = "user",
            Senha = "pass",
            SapClient = "110",
            HostsPermitidos = ["sap.example.com"],
            EscritaHabilitada = escrita
        };

    private static OperacaoConfirmacaoSap OperacaoConfirmacao(
        string internalId = "6",
        string orderOperation = "0060",
        string sequence = "0")
        => new()
        {
            OrderId = "1000009",
            Sequence = sequence,
            OrderOperation = orderOperation,
            OrderOperationInternalId = internalId,
            Plant = "3007",
            WorkCenter = "6",
            ConfirmationUnit = "KG"
        };

    private static ConsumoMaterialSap261Request Request()
        => new()
        {
            GoodsMovementCode = "03",
            PostingDate = DataUtc,
            DocumentDate = DataUtc,
            MaterialDocumentHeaderText = "FP CONS 1000009",
            ToMaterialDocumentItem =
            [
                new ConsumoMaterialSap261ItemRequest
                {
                    Material = "QM002", Plant = "3007", StorageLocation = "PP01",
                    GoodsMovementType = "261", QuantityInEntryUnit = "5.500", EntryUnit = "KG",
                    ManufacturingOrder = "1000009", Reservation = "6676", ReservationItem = "2", Batch = string.Empty
                }
            ]
        };

    // ---------- Client ----------

    [Fact]
    public async Task Client_DeveBuscarCsrfAntesDoPostEmMaterialDocumentHeader()
    {
        HandlerSap261 handler = new();
        using HttpClient http = new(handler);
        ConsumoMaterialSap261ApiClient cliente = new(Config(escrita: true), http);

        ResultadoEnvioConsumoSap261 r = await cliente.EnviarConsumo261Async(Request(), "corr-1");

        Assert.True(r.Sucesso);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);   // CSRF fetch
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);  // POST
        Assert.Contains("A_MaterialDocumentHeader", handler.Requests[1].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementCode\": \"03\"", handler.CorposPost[0], StringComparison.Ordinal);   // payload 03
        Assert.Contains("\"GoodsMovementType\": \"261\"", handler.CorposPost[0], StringComparison.Ordinal);  // item 261
    }

    [Fact]
    public async Task Client_RetornoComDocumentoEAno_MarcaSucesso()
    {
        HandlerSap261 handler = new();
        using HttpClient http = new(handler);
        ResultadoEnvioConsumoSap261 r = await new ConsumoMaterialSap261ApiClient(Config(true), http)
            .EnviarConsumo261Async(Request(), "corr");

        Assert.True(r.Sucesso);
        Assert.Equal("5000000124", r.DocumentoMaterialSap);
        Assert.Equal("2026", r.ExercicioDocumentoMaterialSap);
    }

    [Fact]
    public async Task Client_Http2xxSemDocumento_NaoMarcaSucesso()
    {
        HandlerSap261 handler = new() { CorpoPost = "{\"d\":{}}" };
        using HttpClient http = new(handler);
        ResultadoEnvioConsumoSap261 r = await new ConsumoMaterialSap261ApiClient(Config(true), http)
            .EnviarConsumo261Async(Request(), "corr");

        Assert.False(r.Sucesso);
        Assert.Contains("nao retornou documento material/ano", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_Http400_RetornaFalhaSanitizada()
    {
        HandlerSap261 handler = new()
        {
            StatusPost = HttpStatusCode.BadRequest,
            CorpoPost = "{\"error\":{\"code\":\"MM/123\",\"message\":{\"value\":\"Movimento invalido\"}}}"
        };
        using HttpClient http = new(handler);
        ResultadoEnvioConsumoSap261 r = await new ConsumoMaterialSap261ApiClient(Config(true), http)
            .EnviarConsumo261Async(Request(), "corr");

        Assert.False(r.Sucesso);
        Assert.Equal(400, r.StatusHttp);
        Assert.DoesNotContain("user", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pass", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Servico (WRITE_ENABLED) ----------

    [Fact]
    public async Task Servico_WriteDesabilitado_BloqueiaSemCsrfNemPost()
    {
        HandlerSap261 handler = new();
        using HttpClient http = new(handler);
        ConsumoMaterialSap261ApiClient cliente = new(Config(escrita: false), http);
        ConsumoMaterialSap261Servico servico = new(Config(escrita: false), null, cliente);

        ResultadoEnvioConsumoSap261 r = await servico.EnviarConsumo261Async(Request(), "chave");

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialSap261Servico.MensagemEscritaBloqueada, r.Mensagem);
        Assert.Empty(handler.Requests); // nem CSRF nem POST
    }

    [Fact]
    public void Servico_ValidarProntoParaEnvio_WriteDesabilitado_NaoChamaHttp()
    {
        HandlerSap261 handler = new();
        using HttpClient http = new(handler);
        ConsumoMaterialSap261ApiClient cliente = new(Config(escrita: false), http);
        ConsumoMaterialSap261Servico servico = new(Config(escrita: false), null, cliente);

        ResultadoEnvioConsumoSap261 r = servico.ValidarProntoParaEnvio();

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialSap261Servico.MensagemEscritaBloqueada, r.Mensagem);
        Assert.Empty(handler.Requests); // nem CSRF nem POST
    }

    [Fact]
    public void Servico_ValidarProntoParaEnvio_MaterialDocumentNaoConfigurado_BloqueiaSemHttp()
    {
        ConsumoMaterialSap261Servico servico = new(ConfigSemMaterialDocument());

        ResultadoEnvioConsumoSap261 r = servico.ValidarProntoParaEnvio();

        Assert.False(r.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemMaterialDocumentNaoConfigurado, r.Mensagem);
    }

    [Fact]
    public async Task Servico_WriteHabilitado_FazCsrfEPost()
    {
        HandlerSap261 handler = new();
        using HttpClient http = new(handler);
        ConsumoMaterialSap261ApiClient cliente = new(Config(escrita: true), http);
        ConsumoMaterialSap261Servico servico = new(Config(escrita: true), null, cliente);

        ResultadoEnvioConsumoSap261 r = await servico.EnviarConsumo261Async(Request(), "chave");

        Assert.True(r.Sucesso);
        Assert.Equal(2, handler.Requests.Count); // CSRF + POST
    }

    // ---------- Service de operacao (reenvio + confirmacao) ----------

    private static ConsumoMaterialLancamento LancamentoPersistido(string status = "PENDENTE_SAP", string? documento = null)
        => new()
        {
            Codigo = 55,
            NumeroOrdem = "1000009",
            Centro = "3007",
            StatusLancamento = status,
            DocumentoMaterialSap = documento,
            Itens =
            [
                new ConsumoMaterialItem
                {
                    NumeroOrdem = "1000009", CodigoMaterial = "QM002", Centro = "3007", DepositoConsumo = "PP01",
                    NumeroReserva = "6676", ItemReserva = "2", QuantidadeConsumidaLocal = 5.5m, Unidade = "KG",
                    TipoMovimentoSap = "261", StatusItem = "PENDENTE_SAP", Lote = "LOTE-261"
                }
            ]
        };

    private static ComponenteConsumoMaterial ComponenteElegibilidade(
        bool backflush = false,
        bool finalizado = false,
        bool eliminacao = false,
        bool granel = false,
        string deposito = "PP01",
        string tipoMovimento = "261",
        decimal pendente = 5.5m,
        string unidade = "KG",
        string lote = "LOTE-261")
        => new()
        {
            CodigoMaterial = "QM002",
            Centro = "3007",
            DepositoConsumo = deposito,
            NumeroReserva = "6676",
            ItemReserva = "2",
            QuantidadePendente = pendente,
            UnidadeMedida = unidade,
            TipoMovimento = tipoMovimento,
            Lote = lote,
            BackflushSap = backflush,
            ReservaFinalizada = finalizado,
            MarcadoParaEliminacao = eliminacao,
            MaterialGranel = granel
        };

    private sealed class FakeSap261 : IConsumoMaterialSap261Servico
    {
        public int Chamadas { get; private set; }
        public int Validacoes { get; private set; }
        public ResultadoEnvioConsumoSap261? ResultadoValidacao { get; init; }
        public ResultadoEnvioConsumoSap261 Resultado { get; init; } =
            ResultadoEnvioConsumoSap261.Ok("5000000124", "2026", 201, "corr");

        public bool EhSimulado => false;
        public bool Configurado => true;

        public ResultadoEnvioConsumoSap261 ValidarProntoParaEnvio()
        {
            Validacoes++;
            return ResultadoValidacao ?? new ResultadoEnvioConsumoSap261
            {
                Sucesso = true,
                Mensagem = "OK"
            };
        }

        public Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
            ConsumoMaterialSap261Request requisicao, string chaveNegocio, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(Resultado);
        }
    }

    private sealed class FakeRepo : IConsumoMaterialRepositorio
    {
        public ConsumoMaterialLancamento? Lancamento { get; init; }
        public bool ReservaSucesso { get; init; } = true;
        public int Reservas { get; private set; }
        public int Confirmacoes { get; private set; }
        public int Falhas { get; private set; }
        public string? DocumentoConfirmado { get; private set; }
        public string? ExercicioConfirmado { get; private set; }

        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento l, CancellationToken ct = default) => Task.FromResult(1L);
        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long c, CancellationToken ct = default) => Task.FromResult(Lancamento);
        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
            ConsultaConsumoMaterialFiltro filtro, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ResumoConsumoMaterialLancamento>>([]);
        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(long c, CancellationToken ct = default)
            => Task.FromResult(Lancamento);

        public Task<bool> TentarReservarEnvioSapAsync(long c, DateTime reservadoEmUtc, CancellationToken ct = default)
        {
            Reservas++;
            return Task.FromResult(ReservaSucesso);
        }

        public Task MarcarFalhaSapAsync(long c, CancellationToken ct = default)
        {
            Falhas++;
            return Task.CompletedTask;
        }

        public Task MarcarConsumoConfirmadoSapAsync(long c, string? doc, string? ex, DateTime enviado, CancellationToken ct = default)
        {
            Confirmacoes++;
            DocumentoConfirmado = doc;
            ExercicioConfirmado = ex;
            return Task.CompletedTask;
        }
    }

    private static ConsumoMaterialServico Servico(FakeRepo repo, FakeSap261 sap261)
        => new(new FakeProdOrder(), () => repo, () => sap261);

    private sealed class FakeProdOrder : IProductionOrderSapServico
    {
        public bool EhSimulado => false; public bool Configurado => true;
        public ResultadoConsultaOrdemProducaoSap? Resultado { get; init; }

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(string n, CancellationToken ct = default)
            => Task.FromResult(Resultado ?? ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemElegivel()));

        private static OrdemProducaoSap OrdemElegivel()
            => new()
            {
                NumeroOrdem = "1000009",
                Centro = "3007",
                Liberada = true,
                Componentes =
                [
                    new ComponenteOrdemProducaoSap
                    {
                        NumeroOrdem = "1000009",
                        Material = "QM002",
                        Centro = "3007",
                        Deposito = "PP01",
                        Reserva = "6676",
                        ItemReserva = "2",
                        QuantidadeNecessaria = 10m,
                        QuantidadeRetirada = 0m,
                        UnidadeBase = "KG",
                        TipoMovimento = "261",
                        Lote = "LOTE-261",
                        OrderOperationInternalId = "000000001234"
                    }
                ]
            };
    }

    [Fact]
    public async Task Enviar_StatusCanceladoLocal_Bloqueia()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido(status: "CANCELADO_LOCAL") };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemNaoPendente, r.Mensagem);
        Assert.Equal(0, sap.Chamadas);
        Assert.Equal(0, repo.Reservas);
    }

    [Fact]
    public async Task Enviar_StatusEnviando_BloqueiaReenvio()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido(status: "ENVIANDO_SAP") };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemJaEnviando, r.Mensagem);
        Assert.Equal(0, sap.Chamadas);
    }

    [Fact]
    public async Task Enviar_StatusFalhaSap_BloqueiaAteReprocessamento()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido(status: "FALHA_SAP") };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemFalhaSapBloqueado, r.Mensagem);
        Assert.Equal(0, sap.Chamadas);
    }

    [Fact]
    public async Task Enviar_DocumentoJaPreenchido_BloqueiaReenvio()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido(status: "CONFIRMADO_SAP", documento: "5000000124") };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemReenvioBloqueado, r.Mensagem);
        Assert.Equal(0, sap.Chamadas);
    }

    [Fact]
    public async Task Enviar_ReservaFalha_NaoChamaSap()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido(), ReservaSucesso = false };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemReservaFalhou, r.Mensagem);
        Assert.Equal(1, repo.Reservas);
        Assert.Equal(0, sap.Chamadas); // reserva falhou -> SAP NAO e chamado
    }

    [Fact]
    public async Task Enviar_LoteVazio_BloqueiaAntesDeReservarENaoChamaSap()
    {
        ConsumoMaterialLancamento lancamento = LancamentoPersistido();
        lancamento.Itens[0].Lote = string.Empty;
        FakeRepo repo = new() { Lancamento = lancamento };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Contains("itens", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task Enviar_WriteDesabilitado_BloqueiaAntesDeReservarENaoMarcaFalha()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new()
        {
            ResultadoValidacao = ResultadoEnvioConsumoSap261.Falha(ConsumoMaterialSap261Servico.MensagemEscritaBloqueada)
        };

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialSap261Servico.MensagemEscritaBloqueada, r.Mensagem);
        Assert.Equal(1, sap.Validacoes);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
        Assert.Equal(0, repo.Falhas);
        Assert.Equal(0, repo.Confirmacoes);
    }

    [Fact]
    public async Task Enviar_MaterialDocumentNaoConfigurado_BloqueiaAntesDeReservar()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new()
        {
            ResultadoValidacao = ResultadoEnvioConsumoSap261.Falha(ConfiguracaoSap.MensagemMaterialDocumentNaoConfigurado)
        };

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemMaterialDocumentNaoConfigurado, r.Mensagem);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task Enviar_ModoDemonstrativo_BloqueiaAntesDeReservar()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new()
        {
            ResultadoValidacao = ResultadoEnvioConsumoSap261.Falha(
                "Envio de consumo 261 indisponÃ­vel em ambiente demonstrativo.")
        };

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Contains("ambiente demonstrativo", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task Enviar_SucessoSap_ReservaEntaoConfirma()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new();

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.True(r.Sucesso);
        Assert.Equal(1, sap.Validacoes);
        Assert.Equal(1, repo.Reservas);   // reservou antes do POST
        Assert.Equal(1, sap.Chamadas);
        Assert.Equal(1, repo.Confirmacoes);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task Enviar_FalhaSapAposReserva_MarcaFalhaSapENaoConfirma()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new()
        {
            Resultado = ResultadoEnvioConsumoSap261.Falha("Etapa POST_CONSUMO_261: HTTP 400.", 400, "corr")
        };

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(1, repo.Reservas);
        Assert.Equal(1, sap.Chamadas);
        Assert.Equal(0, repo.Confirmacoes); // NAO confirma
        Assert.Equal(1, repo.Falhas);       // marca FALHA_SAP
    }

    [Fact]
    public async Task Enviar_ErroSapM7018_RetornaMensagemAmigavel()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new()
        {
            Resultado = ResultadoEnvioConsumoSap261.Falha(
                "Etapa POST_CONSUMO_261: HTTP 400. SAP code = M7/018 msg = Enter Batch",
                400,
                "corr")
        };

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemSapExigeLote, r.Mensagem);
        Assert.Equal(1, repo.Reservas);
        Assert.Equal(1, sap.Chamadas);
        Assert.Equal(1, repo.Falhas);
    }

    [Fact]
    public void Elegibilidade_BackflushTrue_Bloqueia261Direto()
    {
        bool elegivel = ConsumoMaterialServico.AvaliarElegibilidadeMaterialDocument261Direto(
            ComponenteElegibilidade(backflush: true),
            out string motivo);

        Assert.False(elegivel);
        Assert.Contains("backflush", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Elegibilidade_ReservaFinalizada_Bloqueia261Direto()
    {
        bool elegivel = ConsumoMaterialServico.AvaliarElegibilidadeMaterialDocument261Direto(
            ComponenteElegibilidade(finalizado: true),
            out string motivo);

        Assert.False(elegivel);
        Assert.Contains("finalizado", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Elegibilidade_ComponenteEliminado_Bloqueia261Direto()
    {
        bool elegivel = ConsumoMaterialServico.AvaliarElegibilidadeMaterialDocument261Direto(
            ComponenteElegibilidade(eliminacao: true),
            out string motivo);

        Assert.False(elegivel);
        Assert.Contains("eliminação", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Elegibilidade_DepositoAusente_Bloqueia261Direto()
    {
        bool elegivel = ConsumoMaterialServico.AvaliarElegibilidadeMaterialDocument261Direto(
            ComponenteElegibilidade(deposito: string.Empty),
            out string motivo);

        Assert.False(elegivel);
        Assert.Contains("depósito", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Elegibilidade_TipoMovimentoDiferente261_Bloqueia()
    {
        bool elegivel = ConsumoMaterialServico.AvaliarElegibilidadeMaterialDocument261Direto(
            ComponenteElegibilidade(tipoMovimento: "262"),
            out string motivo);

        Assert.False(elegivel);
        Assert.Contains("261", motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Elegibilidade_ComponenteValidoKgPendenteDepositoLote_Permite()
    {
        bool elegivel = ConsumoMaterialServico.AvaliarElegibilidadeMaterialDocument261Direto(
            ComponenteElegibilidade(),
            out string motivo);

        Assert.True(elegivel);
        Assert.Equal(string.Empty, motivo);
    }

    [Fact]
    public async Task Enviar_BackflushBloqueiaAntesDeReservarPostENaoMarcaFalha()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new();
        ResultadoConsultaOrdemProducaoSap resultadoOp = ResultadoConsultaOrdemProducaoSap.Encontrada(new OrdemProducaoSap
        {
            NumeroOrdem = "1000009",
            Centro = "3007",
            Liberada = true,
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1000009",
                    Material = "QM002",
                    Centro = "3007",
                    Deposito = "PP01",
                    Reserva = "6676",
                    ItemReserva = "2",
                    QuantidadeNecessaria = 10m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "KG",
                    TipoMovimento = "261",
                    BackflushSap = true
                }
            ]
        });
        ConsumoMaterialServico servico = new(new FakeProdOrder { Resultado = resultadoOp }, () => repo, () => sap);

        ResultadoEnvioConsumoSap261 r = await servico.EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        // Tarefa 12: tom operacional (orientacao p/ Confirmacao de Producao), nao mais "nao elegivel".
        Assert.Equal(ConsumoMaterialServico.MensagemBackflushUseConfirmacao, r.Mensagem);
        Assert.Contains("Backflush", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Confirmação de Produção", r.Mensagem, StringComparison.Ordinal);
        Assert.Equal(1, sap.Validacoes);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
        Assert.Equal(0, repo.Falhas);                 // NAO marca FALHA_SAP
        Assert.Equal(0, repo.Confirmacoes);
        Assert.Equal("PENDENTE_SAP", repo.Lancamento!.StatusLancamento); // permanece PENDENTE_SAP
    }

    // ---------- Tarefa 12: classificacao + preview de Confirmacao de Producao (Backflush) ----------

    [Fact]
    public void Classificacao_BackflushTrue_RequerConfirmacaoProducao()
    {
        ClassificacaoEnvioConsumo261 c =
            ConsumoMaterialServico.ClassificarEnvioConsumo261(ComponenteElegibilidade(backflush: true));

        Assert.Equal(ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao, c);
        Assert.Equal("REQUER_CONFIRMACAO_PRODUCAO", c.Codigo());
    }

    [Fact]
    public void Classificacao_NaoBackflushElegivel_MaterialDocument261Direto()
    {
        ClassificacaoEnvioConsumo261 c =
            ConsumoMaterialServico.ClassificarEnvioConsumo261(ComponenteElegibilidade());

        Assert.Equal(ClassificacaoEnvioConsumo261.MaterialDocument261Direto, c);
        Assert.Equal("MATERIAL_DOCUMENT_261_DIRETO", c.Codigo());
    }

    [Fact]
    public void Classificacao_InelegivelNaoBackflush_Bloqueado()
    {
        ClassificacaoEnvioConsumo261 c =
            ConsumoMaterialServico.ClassificarEnvioConsumo261(ComponenteElegibilidade(finalizado: true));

        Assert.Equal(ClassificacaoEnvioConsumo261.Bloqueado, c);
        Assert.Equal("BLOQUEADO", c.Codigo());
    }

    [Fact]
    public void PreviewConfirmacao_Backflush_UsaOperacaoDoComponente()
    {
        ComponenteConsumoMaterial componente = ComponenteElegibilidade(backflush: true);
        componente.CodigoMaterial = "QM002";
        componente.Operacao = "0060";              // operacao do componente (ManufacturingOrderOperation)
        OrdemProducaoConsumo ordem = new()
        {
            NumeroOrdem = "1000166",
            Operacoes = [new OperacaoOrdemConsumo { Operacao = "0010" }] // 1a operacao da OP (NAO deve ser usada)
        };

        ResultadoPreviewConfirmacaoProducao preview =
            new ConsumoMaterialServico(new FakeProdOrder()).GerarPreviewConfirmacaoProducao(ordem, componente, 5.5m);

        Assert.True(preview.Sucesso);
        Assert.Equal("0060", preview.Preview!.Operacao);                 // usa a do componente
        Assert.Equal("COMPONENTE", preview.Preview.OrigemOperacao);
        Assert.Contains("\"Operacao\": \"0060\"", preview.PreviewJson, StringComparison.Ordinal);
        Assert.Contains("COMPONENTE", preview.PreviewJson, StringComparison.Ordinal);
        Assert.Contains("1000166", preview.PreviewJson, StringComparison.Ordinal);
        Assert.Contains("Preview técnico — não enviado ao SAP", preview.PreviewJson, StringComparison.Ordinal);
        // NAO caiu no fallback da primeira operacao da OP.
        Assert.DoesNotContain("\"Operacao\": \"0010\"", preview.PreviewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("FALLBACK", preview.PreviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewConfirmacao_Backflush_OperacaoVazia_UsaFallbackDaPrimeiraOperacaoDaOp()
    {
        ComponenteConsumoMaterial componente = ComponenteElegibilidade(backflush: true);
        componente.Operacao = string.Empty;        // componente SEM operacao
        OrdemProducaoConsumo ordem = new()
        {
            NumeroOrdem = "1000166",
            Operacoes = [new OperacaoOrdemConsumo { Operacao = "0010" }]
        };

        ResultadoPreviewConfirmacaoProducao preview =
            new ConsumoMaterialServico(new FakeProdOrder()).GerarPreviewConfirmacaoProducao(ordem, componente, 5.5m);

        Assert.True(preview.Sucesso);
        Assert.Equal("0010", preview.Preview!.Operacao);                          // fallback da OP
        Assert.Equal("FALLBACK_PRIMEIRA_OPERACAO_OP", preview.Preview.OrigemOperacao);
        Assert.Contains("fallback técnico", preview.Preview.ObservacaoOperacao, StringComparison.Ordinal);
        Assert.Contains("FALLBACK_PRIMEIRA_OPERACAO_OP", preview.PreviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewConfirmacao_NaoBackflush_NaoMonta()
    {
        ResultadoPreviewConfirmacaoProducao preview = new ConsumoMaterialServico(new FakeProdOrder())
            .GerarPreviewConfirmacaoProducao(
                new OrdemProducaoConsumo { NumeroOrdem = "1000166" },
                ComponenteElegibilidade(),
                5.5m);

        Assert.False(preview.Sucesso);
        Assert.Equal(string.Empty, preview.PreviewJson);
    }

    [Fact]
    public void PreviewConfirmacao_FontesNaoUsamPostCsrfPatch()
    {
        string builder = File.ReadAllText(Path.Combine(RaizProjeto(),
            "Servicos", "IntegracaoSap", "ConfirmacaoProducaoPreviewBuilder.cs"));

        foreach (string proibido in new[] { "HttpMethod.Post", "HttpMethod.Patch", "X-CSRF-Token", "HttpClient", "SendAsync" })
        {
            Assert.DoesNotContain(proibido, builder, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Enviar_ErroSapM7509_RetornaMensagemAmigavel()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeSap261 sap = new()
        {
            Resultado = ResultadoEnvioConsumoSap261.Falha(
                "Etapa POST_CONSUMO_261: HTTP 400. SAP code = M7/509 msg = For reservation 0000011237 0004, no movements can be posted",
                400,
                "corr")
        };

        ResultadoEnvioConsumoSap261 r = await Servico(repo, sap).EnviarConsumoSap261Async(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemSapReservaNaoPermiteMovimento, r.Mensagem);
        Assert.Equal(1, repo.Reservas);
        Assert.Equal(1, sap.Chamadas);
        Assert.Equal(1, repo.Falhas);
    }

    // ---------- Tarefa 16: classificação de rota + estrutura preparatória de Confirmação ----------

    [Fact]
    public void ClassificarRota_TodosBackflush_BackflushConfirmacao()
        => Assert.Equal(
            RotaEnvioConsumo.BackflushConfirmacao,
            ConsumoMaterialServico.ClassificarRotaEnvio(new[]
            {
                ComponenteElegibilidade(backflush: true), ComponenteElegibilidade(backflush: true)
            }));

    [Fact]
    public void ClassificarRota_TodosDireto_261Direto()
        => Assert.Equal(
            RotaEnvioConsumo.Direto261,
            ConsumoMaterialServico.ClassificarRotaEnvio(new[] { ComponenteElegibilidade() }));

    [Fact]
    public void ClassificarRota_Misto_Misto()
        => Assert.Equal(
            RotaEnvioConsumo.Misto,
            ConsumoMaterialServico.ClassificarRotaEnvio(new[]
            {
                ComponenteElegibilidade(), ComponenteElegibilidade(backflush: true)
            }));

    [Fact]
    public void ClassificarRota_SemLote_Bloqueado()
    {
        ComponenteConsumoMaterial c = ComponenteElegibilidade();
        c.Lote = string.Empty;
        Assert.Equal(RotaEnvioConsumo.Bloqueado, ConsumoMaterialServico.ClassificarRotaEnvio(new[] { c }));
    }

    [Fact]
    public void ClassificarRota_Vazio_Bloqueado()
        => Assert.Equal(
            RotaEnvioConsumo.Bloqueado,
            ConsumoMaterialServico.ClassificarRotaEnvio(System.Array.Empty<ComponenteConsumoMaterial>()));

    [Fact]
    public void PayloadConfirmacao_Backflush_MontaProdnOrdConf2ComItem261()
    {
        ConsumoMaterialLancamento lancamento = LancamentoPersistido();

        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(lancamento, OperacaoConfirmacao(), DataUtc);

        Assert.True(preview.Sucesso);
        Assert.NotNull(preview.Payload);
        Assert.Equal("1000009", preview.Payload!.OrderID);
        Assert.Equal("1000009", preview.Payload.ManufacturingOrder);
        Assert.Equal("0", preview.Payload.Sequence);
        Assert.Equal("0060", preview.Payload.OrderOperation);
        Assert.Equal("6", preview.Payload.OrderOperationInternalID);
        // Tarefa 17.11: yield e a quantidade APONTADA da operacao (FugaPET nao aponta) -> 0.000, NAO a consumida.
        Assert.Equal("0.000", preview.Payload.ConfirmationYieldQuantity);
        Assert.Equal("KG", preview.Payload.ConfirmationUnit);
        Assert.False(preview.Payload.IsFinalConfirmation);
        Assert.Single(preview.Payload.ToProdnOrdConfMatlDocItm);
        Assert.Equal("261", preview.Payload.ToProdnOrdConfMatlDocItm[0].GoodsMovementType);
        Assert.Equal("LOTE-261", preview.Payload.ToProdnOrdConfMatlDocItm[0].Batch);
        Assert.Equal("1000009", preview.Payload.ToProdnOrdConfMatlDocItm[0].ManufacturingOrder);
        Assert.Equal("0000006676", preview.Payload.ToProdnOrdConfMatlDocItm[0].Reservation);
        Assert.Equal("0002", preview.Payload.ToProdnOrdConfMatlDocItm[0].ReservationItem);
        Assert.Contains("\"to_ProdnOrdConfMatlDocItm\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"results\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"OrderID\": \"1000009\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"Sequence\": \"0\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"OrderOperation\": \"0060\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OrderOperationInternalID\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GoodsMovementIsFinallyPosted", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("APIConfHasNoGoodsMovements", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementType\": \"261\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"ConfirmationYieldQuantity\": \"0.000\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"QuantityInEntryUnit\": \"5.500\"", preview.PayloadJson, StringComparison.Ordinal); // consumida no item
        Assert.Contains("\"ConfirmationUnit\": \"KG\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"IsFinalConfirmation\": false", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"PostingDate\": \"/Date(", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"Material\": \"QM002\"", preview.PayloadJson, StringComparison.Ordinal);
        // Tarefa 17.6: ManufacturingOrder NAO deve aparecer no item (SAP recusa). OP fica em OrderID no cabecalho.
        Assert.DoesNotContain("\"ManufacturingOrder\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"Reservation\": \"0000006676\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"ReservationItem\": \"0002\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"QuantityInEntryUnit\": \"5.500\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"EntryUnit\": \"KG\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"Plant\": \"3007\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"StorageLocation\": \"PP01\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"Batch\": \"LOTE-261\"", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadConfirmacao_NormalizaReservaEItemReserva()
    {
        ConsumoMaterialLancamento lancamento = LancamentoPersistido();
        lancamento.Itens[0].NumeroReserva = "11237";
        lancamento.Itens[0].ItemReserva = "4";

        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(lancamento, OperacaoConfirmacao(), DataUtc);

        Assert.True(preview.Sucesso);
        Assert.Contains("\"Reservation\": \"0000011237\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"ReservationItem\": \"0004\"", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmacaoClient_MapeiaOperacoesJson()
    {
        IReadOnlyList<OperacaoConfirmacaoSap> operacoes = ConfirmacaoProducaoSapApiClient.MapearOperacoesConfirmacao(
            "{\"d\":{\"results\":[{\"OrderID\":\"1000909\",\"Sequence\":\"0\",\"OrderOperation\":\"0010\",\"OrderOperationInternalID\":\"1\",\"Plant\":\"3007\",\"WorkCenter\":\"1\",\"ConfirmationUnit\":\"KG\"},{\"OrderID\":\"1000909\",\"Sequence\":\"0\",\"OrderOperation\":\"0060\",\"OrderOperationInternalID\":\"6\",\"Plant\":\"3007\",\"WorkCenter\":\"6\",\"ConfirmationUnit\":\"KG\"}]}}");

        Assert.Equal(2, operacoes.Count);
        Assert.Equal("1000909", operacoes[0].OrderId);
        Assert.Equal("0010", operacoes[0].OrderOperation);
        Assert.Equal("1", operacoes[0].OrderOperationInternalId);
        Assert.Equal("0060", operacoes[1].OrderOperation);
        Assert.Equal("6", operacoes[1].OrderOperationInternalId);
    }

    [Fact]
    public void ConfirmacaoClient_MapeiaOperacoesXmlAtom()
    {
        IReadOnlyList<OperacaoConfirmacaoSap> operacoes = ConfirmacaoProducaoSapApiClient.MapearOperacoesConfirmacao("""
        <feed xmlns="http://www.w3.org/2005/Atom"
              xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"
              xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices">
          <entry><content type="application/xml"><m:properties>
            <d:OrderID>1000909</d:OrderID>
            <d:Sequence>0</d:Sequence>
            <d:OrderOperation>0060</d:OrderOperation>
            <d:OrderOperationInternalID>6</d:OrderOperationInternalID>
            <d:Plant>3007</d:Plant>
            <d:WorkCenter>6</d:WorkCenter>
            <d:ConfirmationUnit>KG</d:ConfirmationUnit>
          </m:properties></content></entry>
        </feed>
        """);

        Assert.Single(operacoes);
        Assert.Equal("1000909", operacoes[0].OrderId);
        Assert.Equal("0", operacoes[0].Sequence);
        Assert.Equal("0060", operacoes[0].OrderOperation);
        Assert.Equal("6", operacoes[0].OrderOperationInternalId);
        Assert.Equal("KG", operacoes[0].ConfirmationUnit);
    }

    [Fact]
    public void PayloadConfirmacao_SemOrderOperation_BloqueiaAntesDoPost()
    {
        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(orderOperation: ""), DataUtc);

        Assert.False(preview.Sucesso);
        Assert.Contains("Operação SAP de confirmação não informada", preview.Mensagem, StringComparison.Ordinal);
        Assert.Null(preview.Payload);
    }

    [Fact]
    public void PayloadConfirmacao_SemSequence_BloqueiaAntesDoPost()
    {
        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(sequence: ""), DataUtc);

        Assert.False(preview.Sucesso);
        Assert.Contains("Sequência SAP da operação de confirmação não informada", preview.Mensagem, StringComparison.Ordinal);
        Assert.Null(preview.Payload);
    }

    [Fact]
    public void PayloadConfirmacao_SemReserva_BloqueiaAntesDoPost()
    {
        ConsumoMaterialLancamento lancamento = LancamentoPersistido();
        lancamento.Itens[0].NumeroReserva = string.Empty;

        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(lancamento, OperacaoConfirmacao(), DataUtc);

        Assert.False(preview.Sucesso);
        Assert.Contains(preview.ErrosValidacao, erro => erro.Contains("reserva/item obrigatórios para confirmação de produção com movimento 261", StringComparison.Ordinal));
        Assert.Null(preview.Payload);
    }

    [Fact]
    public void PayloadConfirmacao_SemItemReserva_BloqueiaAntesDoPost()
    {
        ConsumoMaterialLancamento lancamento = LancamentoPersistido();
        lancamento.Itens[0].ItemReserva = string.Empty;

        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(lancamento, OperacaoConfirmacao(), DataUtc);

        Assert.False(preview.Sucesso);
        Assert.Contains(preview.ErrosValidacao, erro => erro.Contains("reserva/item obrigatórios para confirmação de produção com movimento 261", StringComparison.Ordinal));
        Assert.Null(preview.Payload);
    }

    [Fact]
    public async Task ConfirmacaoClient_DeveBuscarCsrfAntesDoPostEmProdnOrdConf2()
    {
        HandlerConfirmacao handler = new();
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.True(r.Sucesso);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Contains("ProdnOrdConf2", handler.Requests[1].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("sap-client=110", handler.Requests[1].RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"OrderID\": \"1000009\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.Contains("\"Sequence\": \"0\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.Contains("\"OrderOperation\": \"0060\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"OrderOperationInternalID\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.DoesNotContain("GoodsMovementIsFinallyPosted", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.DoesNotContain("APIConfHasNoGoodsMovements", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.Contains("\"to_ProdnOrdConfMatlDocItm\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementType\": \"261\"", handler.CorposPost[0], StringComparison.Ordinal);
        // Tarefa 17.6: POST nao envia ManufacturingOrder no item (SAP recusou). OP no cabecalho como OrderID.
        Assert.DoesNotContain("\"ManufacturingOrder\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.Contains("\"Reservation\": \"0000006676\"", handler.CorposPost[0], StringComparison.Ordinal);
        Assert.Contains("\"ReservationItem\": \"0002\"", handler.CorposPost[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmacaoClient_Http2xxComOrderId_MarcaSucesso()
    {
        HandlerConfirmacao handler = new()
        {
            CorpoPost = "{\"d\":{\"ConfirmationGroup\":\"CG001\",\"ConfirmationCount\":\"0001\",\"OrderID\":\"1000009\",\"APIConfHasNoGoodsMovements\":false,\"to_ProdnOrdConfMatlDocItm\":{\"results\":[{\"MaterialDocument\":\"5000000200\",\"GoodsMovementType\":\"261\"}]}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.True(r.Sucesso);
        Assert.Equal("CG001", r.ConfirmationGroup);
        Assert.Equal("0001", r.ConfirmationCount);
        Assert.Equal("1000009", r.ManufacturingOrder);
        Assert.True(r.GoodsMovementIsFinallyPosted);
    }

    [Fact]
    public async Task ConfirmacaoClient_Http2xxComManufacturingOrder_MarcaSucesso()
    {
        HandlerConfirmacao handler = new()
        {
            CorpoPost = "{\"d\":{\"ConfirmationGroup\":\"CG001\",\"ConfirmationCount\":\"0001\",\"ManufacturingOrder\":\"1000009\",\"to_ProdnOrdConfMatlDocItm\":{\"results\":[{\"MaterialDocument\":\"5000000200\",\"GoodsMovementType\":\"261\"}]}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.True(r.Sucesso);
        Assert.Equal("1000009", r.ManufacturingOrder);
    }

    [Fact]
    public async Task ConfirmacaoClient_Http2xxSemGrupoContadorOrdem_NaoMarcaSucesso()
    {
        HandlerConfirmacao handler = new() { CorpoPost = "{\"d\":{}}" };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.False(r.Sucesso);
        Assert.Contains("ConfirmationGroup/ConfirmationCount/OrderID ou ManufacturingOrder", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmacaoClient_ErroPropriedadeInvalida_RetornaMensagemAmigavel()
    {
        HandlerConfirmacao handler = new()
        {
            StatusPost = HttpStatusCode.BadRequest,
            CorpoPost = "{\"error\":{\"code\":\"/IWCOR/CX_DS_EP_PROPERTY_ERROR/00505692409C1ED6\",\"message\":{\"value\":\"Property 'GoodsMovementIsFinallyPosted' is invalid\"}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.False(r.Sucesso);
        Assert.Equal(
            "SAP recusou a confirmação: o payload contém uma propriedade não aceita pela API de Confirmação. Remova GoodsMovementIsFinallyPosted e envie os movimentos pela navegação de itens.",
            r.Mensagem);
    }

    [Fact]
    public async Task ConfirmacaoClient_ErroOrderOperationInternalIdSomenteLeitura_RetornaMensagemAmigavel()
    {
        HandlerConfirmacao handler = new()
        {
            StatusPost = HttpStatusCode.BadRequest,
            CorpoPost = "{\"error\":{\"code\":\"RU/373\",\"message\":{\"value\":\"Order 1000909: Property 'OrderOperationInternalID' not allowed for Create; confirmation not posted\"}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.False(r.Sucesso);
        Assert.Equal(
            "SAP recusou a confirmação: OrderOperationInternalID é somente leitura/não permitido na criação. O payload deve enviar OrderOperation e Sequence.",
            r.Mensagem);
    }

    [Fact]
    public async Task ConfirmacaoClient_ErroRu355CamposObrigatorios_RetornaMensagemAmigavel()
    {
        HandlerConfirmacao handler = new()
        {
            StatusPost = HttpStatusCode.BadRequest,
            CorpoPost = "{\"error\":{\"code\":\"RU/355\",\"message\":{\"value\":\"Not all mandatory fields are filled for the goods movement. See docum.\"}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.False(r.Sucesso);
        // Tarefa 17.8: RU/355 agora orienta validar o $metadata de ProdnOrdConfMatlDocItmType.
        Assert.Equal(
            "SAP ainda informa campos obrigatórios ausentes no movimento. Validar $metadata de ProdnOrdConfMatlDocItmType para identificar campos graváveis obrigatórios.",
            r.Mensagem);
    }

    [Fact]
    public void ConfirmacaoServico_WriteDesabilitado_BloqueiaSemHttp()
    {
        HandlerConfirmacao handler = new();
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapServico servico = new(ConfigConfirmacao(false), null, new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(false), http));

        ResultadoEnvioConfirmacaoProducao r = servico.ValidarProntoParaEnvio();

        Assert.False(r.Sucesso);
        Assert.Equal(ConfirmacaoProducaoSapServico.MensagemEscritaBloqueada, r.Mensagem);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ConfirmacaoProducaoSap_NaoHabilitada_NaoEnvia()
    {
        ConfirmacaoProducaoSapServico servico = new(new ConfiguracaoSap { EscritaHabilitada = true });
        Assert.False(servico.EnvioHabilitado);

        ResultadoEnvioConfirmacaoProducao r = await servico.EnviarConfirmacaoAsync(
            new ConfirmacaoProducaoSapRequest(),
            "TESTE");

        Assert.False(r.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemProductionOrderConfirmationNaoConfigurado, r.Mensagem);
    }

    [Fact]
    public void ConfirmacaoProducao_FontesPreparatoriasNaoUsamPostCsrfPatch()
    {
        foreach (string arquivo in new[] { "ConfirmacaoProducaoPreviewBuilder.cs" })
        {
            string texto = File.ReadAllText(Path.Combine(RaizProjeto(), "Servicos", "IntegracaoSap", arquivo));
            foreach (string proibido in new[] { "HttpMethod.Post", "HttpMethod.Patch", "X-CSRF-Token", "HttpClient", "SendAsync" })
            {
                Assert.DoesNotContain(proibido, texto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void PreviewConfirmacao_DoLancamentoSalvo_MontaJsonComDadosPersistidos()
    {
        ConsumoMaterialLancamento lancamento = new()
        {
            Codigo = 6,
            NumeroOrdem = "1000909",
            MaterialProduzido = "FG228",
            Centro = "3007",
            StatusLancamento = "PENDENTE_SAP",
            Itens = new List<ConsumoMaterialItem>
            {
                new()
                {
                    CodigoMaterial = "1000037", NumeroReserva = "11237", ItemReserva = "4",
                    Lote = "000256", DepositoConsumo = "PP01", QuantidadeConsumidaLocal = 47.5m, Unidade = "KG"
                }
            }
        };

        ResultadoPreviewConfirmacaoProducao r = ConfirmacaoProducaoPreviewBuilder.MontarDoLancamento(lancamento);

        Assert.True(r.Sucesso);
        Assert.Equal("Preview técnico — não enviado ao SAP", r.Titulo);
        Assert.Contains("1000909", r.PreviewJson, StringComparison.Ordinal);
        Assert.Contains("000256", r.PreviewJson, StringComparison.Ordinal);
        Assert.Contains("PENDENTE_SAP", r.PreviewJson, StringComparison.Ordinal);
        Assert.Contains("47.5", r.PreviewJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarConfirmacao_WriteDesabilitado_BloqueiaAntesDeReservarENaoMarcaFalha()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            ResultadoValidacao = ResultadoEnvioConfirmacaoProducao.Falha(ConfirmacaoProducaoSapServico.MensagemEscritaBloqueada)
        };

        ResultadoEnvioConfirmacaoProducao r = await ServicoConfirmacao(repo, confirmacao)
            .EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConfirmacaoProducaoSapServico.MensagemEscritaBloqueada, r.Mensagem);
        Assert.Equal(1, confirmacao.Validacoes);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
        Assert.Equal(0, repo.Falhas);
        Assert.Equal(0, repo.Confirmacoes);
    }

    [Fact]
    public async Task EnviarConfirmacao_SemOrderOperationInternalId_BloqueiaAntesDeReservarPostENaoMarcaFalha()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new() { Operacoes = [] };
        ResultadoConsultaOrdemProducaoSap resultadoOp = ResultadoConsultaOrdemProducaoSap.Encontrada(new OrdemProducaoSap
        {
            NumeroOrdem = "1000009",
            Centro = "3007",
            Liberada = true,
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1000009",
                    Material = "QM002",
                    Centro = "3007",
                    Deposito = "PP01",
                    Reserva = "6676",
                    ItemReserva = "2",
                    QuantidadeNecessaria = 10m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "KG",
                    TipoMovimento = "261",
                    BackflushSap = true,
                    Lote = "LOTE-261"
                }
            ]
        });
        ConsumoMaterialServico servico = new(new FakeProdOrder { Resultado = resultadoOp }, () => repo, () => new FakeSap261(), () => confirmacao);

        ResultadoEnvioConfirmacaoProducao r = await servico.EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Contains("Não foi possível determinar o OrderOperationInternalID.", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("Operação do componente: não informada.", r.Mensagem, StringComparison.Ordinal);
        Assert.Equal(1, confirmacao.Validacoes);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task EnviarConfirmacao_Operacao0010_ResolveInternalId1()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Operacoes =
            [
                new() { OrderId = "1000009", Sequence = "0", OrderOperation = "0010", OrderOperationInternalId = "1", Plant = "3007", ConfirmationUnit = "KG" },
                new() { OrderId = "1000009", Sequence = "0", OrderOperation = "0060", OrderOperationInternalId = "6", Plant = "3007", ConfirmationUnit = "KG" }
            ]
        };
        ConsumoMaterialServico servico = new(
            new FakeProdOrder { Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemBackflushConfirmacao("0010", "0")) },
            () => repo,
            () => new FakeSap261(),
            () => confirmacao);

        // Tarefa 17.11: resolucao da operacao verificada no PREVIEW real (envio bloqueia por yield-zero).
        ResultadoPreviewConfirmacaoProducaoSap preview = await servico.GerarPreviewConfirmacaoProducaoRealAsync(55);

        Assert.True(preview.Sucesso);
        Assert.Contains("\"Sequence\": \"0\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"OrderOperation\": \"0010\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OrderOperationInternalID\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Equal("1", preview.Payload!.OrderOperationInternalID);
    }

    [Fact]
    public async Task EnviarConfirmacao_SequenciaPreenchida_DesambiguaOperacao()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Operacoes =
            [
                new() { OrderId = "1000009", Sequence = "0", OrderOperation = "0060", OrderOperationInternalId = "6", Plant = "3007", ConfirmationUnit = "KG" },
                new() { OrderId = "1000009", Sequence = "1", OrderOperation = "0060", OrderOperationInternalId = "7", Plant = "3007", ConfirmationUnit = "KG" }
            ]
        };
        ConsumoMaterialServico servico = new(
            new FakeProdOrder { Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemBackflushConfirmacao("0060", "1")) },
            () => repo,
            () => new FakeSap261(),
            () => confirmacao);

        // Tarefa 17.11: o envio bloqueia por yield-zero; a resolucao da operacao e verificada no PREVIEW real.
        ResultadoPreviewConfirmacaoProducaoSap preview = await servico.GerarPreviewConfirmacaoProducaoRealAsync(55);

        Assert.True(preview.Sucesso);
        Assert.Contains("\"Sequence\": \"1\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"OrderOperation\": \"0060\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OrderOperationInternalID\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Equal("7", preview.Payload!.OrderOperationInternalID);
    }

    [Fact]
    public async Task EnviarConfirmacao_OperacaoNaoEncontrada_BloqueiaAntesDeReservarPost()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Operacoes = [new() { OrderId = "1000009", Sequence = "0", OrderOperation = "0010", OrderOperationInternalId = "1" }]
        };

        ResultadoEnvioConfirmacaoProducao r = await ServicoConfirmacao(repo, confirmacao)
            .EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Contains("Nenhuma operação correspondente foi encontrada em ProdnOrdConf2.", r.Mensagem, StringComparison.Ordinal);
        Assert.Equal(1, confirmacao.ConsultasOperacoes);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task EnviarConfirmacao_OperacaoAmbigua_BloqueiaAntesDeReservarPost()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Operacoes =
            [
                new() { OrderId = "1000009", Sequence = "0", OrderOperation = "0060", OrderOperationInternalId = "6" },
                new() { OrderId = "1000009", Sequence = "1", OrderOperation = "0060", OrderOperationInternalId = "7" }
            ]
        };
        ConsumoMaterialServico servico = new(
            new FakeProdOrder { Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemBackflushConfirmacao("0060", "")) },
            () => repo,
            () => new FakeSap261(),
            () => confirmacao);

        ResultadoEnvioConfirmacaoProducao r = await servico.EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Contains("Mais de uma operação corresponde ao componente.", r.Mensagem, StringComparison.Ordinal);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
    }

    [Fact]
    public async Task EnviarConfirmacao_OperacaoResolvidaSemSequence_BloqueiaAntesDeReservarPost()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Operacoes =
            [
                new() { OrderId = "1000009", Sequence = "", OrderOperation = "0060", OrderOperationInternalId = "6" }
            ]
        };
        ConsumoMaterialServico servico = new(
            new FakeProdOrder { Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemBackflushConfirmacao("0060", "")) },
            () => repo,
            () => new FakeSap261(),
            () => confirmacao);

        ResultadoEnvioConfirmacaoProducao r = await servico.EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Contains("Sequência SAP da operação de confirmação não informada", r.Mensagem, StringComparison.Ordinal);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
    }

    [Fact]
    public async Task EnviarConfirmacao_NaoConverteOperacao0060Para6PorRegraFixa()
    {
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Operacoes = [new() { OrderId = "1000009", Sequence = "0", OrderOperation = "0010", OrderOperationInternalId = "6" }]
        };

        ResultadoEnvioConfirmacaoProducao r = await ServicoConfirmacao(repo, confirmacao)
            .EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.DoesNotContain("\"OrderOperationInternalID\"", confirmacao.UltimoPayloadJson, StringComparison.Ordinal);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
    }

    [Fact]
    public async Task EnviarConfirmacao_PreCondicaoOk_ReservaAntesDoPostEConfirmaComGrupoContador()
    {
        // Tarefa 17.11: resolve a operacao (GET) mas bloqueia por yield ZERO antes de reservar/POST/confirmar.
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new();

        ResultadoEnvioConfirmacaoProducao r = await ServicoConfirmacao(repo, confirmacao)
            .EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemBackflushConfirmacaoYieldZero, r.Mensagem);
        Assert.Equal(1, confirmacao.ConsultasOperacoes); // operacao foi resolvida antes do bloqueio
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
        Assert.Equal(0, repo.Confirmacoes);
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task EnviarConfirmacao_ReservaFalhou_NaoChamaSap()
    {
        // Tarefa 17.11: com yield ZERO, a confirmacao Backflush e bloqueada ANTES de reservar/POST.
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new();

        ResultadoEnvioConfirmacaoProducao r = await ServicoConfirmacao(repo, confirmacao)
            .EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemBackflushConfirmacaoYieldZero, r.Mensagem);
        Assert.Equal(0, repo.Reservas);        // bloqueia ANTES de reservar
        Assert.Equal(0, confirmacao.Chamadas); // POST nao executado
        Assert.Equal(0, repo.Falhas);
    }

    [Fact]
    public async Task EnviarConfirmacao_FalhaSapAposReserva_MarcaFalhaSap()
    {
        // Tarefa 17.11: com yield ZERO nao ha POST — bloqueia antes, sem reservar nem marcar FALHA.
        FakeRepo repo = new() { Lancamento = LancamentoPersistido() };
        FakeConfirmacaoSap confirmacao = new()
        {
            Resultado = ResultadoEnvioConfirmacaoProducao.Falha("Etapa POST_CONFIRMACAO_PRODUCAO: HTTP 400.", 400, "corr-conf")
        };

        ResultadoEnvioConfirmacaoProducao r = await ServicoConfirmacao(repo, confirmacao)
            .EnviarConfirmacaoProducaoAsync(55, "op", default);

        Assert.False(r.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemBackflushConfirmacaoYieldZero, r.Mensagem);
        Assert.Equal(0, repo.Reservas);
        Assert.Equal(0, confirmacao.Chamadas);
        Assert.Equal(0, repo.Falhas);
        Assert.Equal(0, repo.Confirmacoes);
    }

    [Fact]
    public void FluxoConsumo_NaoIntroduzPatchNemAlteraMaterialDocument101()
    {
        string servico = File.ReadAllText(Path.Combine(RaizProjeto(), "Servicos", "Operacao", "ConsumoMaterialServico.cs"));
        string entrada = File.ReadAllText(Path.Combine(RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));
        string modeloItem101 = File.ReadAllText(Path.Combine(RaizProjeto(), "Modelo", "IntegracaoSap", "MaterialDocumentSapItemRequest.cs"));

        Assert.DoesNotContain("HttpMethod.Patch", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API_PURCHASEORDER_2", servico, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GoodsMovementRefDocType", modeloItem101, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", entrada, StringComparison.Ordinal);
    }

    // ---------- Tarefa 17.6: ManufacturingOrder fora do item + mensagem amigável ----------

    [Fact]
    public async Task ConfirmacaoClient_ErroManufacturingOrderInvalido_MapeiaMensagemAmigavel()
    {
        HandlerConfirmacao handler = new()
        {
            StatusPost = HttpStatusCode.BadRequest,
            CorpoPost = "{\"error\":{\"code\":\"/IWCOR/CX_DS_EP_PROPERTY_ERROR\",\"message\":{\"value\":\"Property 'ManufacturingOrder' is invalid\"}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.False(r.Sucesso);
        Assert.Contains("ManufacturingOrder não é aceito no item", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("deve ficar no cabeçalho como OrderID", r.Mensagem, StringComparison.Ordinal);
        // POST nao enviou ManufacturingOrder no item.
        Assert.DoesNotContain("\"ManufacturingOrder\"", handler.CorposPost[0], StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadConfirmacao_Item_ReasonCode0_SemRefDocTypeNoPost()
    {
        ResultadoPreviewConfirmacaoProducaoSap preview = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc);

        Assert.True(preview.Sucesso);
        Assert.Equal("0", preview.Payload!.ToProdnOrdConfMatlDocItm[0].GoodsMovementReasonCode);

        Assert.Contains("\"GoodsMovementReasonCode\": \"0\"", preview.PayloadJson, StringComparison.Ordinal);
        // Tarefa 17.11: GoodsMovementRefDocType NAO e enviado no POST (mantido fora ate validacao $metadata).
        Assert.DoesNotContain("\"GoodsMovementRefDocType\"", preview.PayloadJson, StringComparison.Ordinal);
        // E continua sem os campos rejeitados.
        Assert.DoesNotContain("\"ManufacturingOrder\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"OrderOperationInternalID\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GoodsMovementIsFinallyPosted", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmacaoClient_GrupoSemItemMaterial_FalhaFuncionalNaoConfirma()
    {
        // Tarefa 17.11: POST cria ConfirmationGroup/Count mas results vazio e sem MaterialDocument -> NAO e sucesso 261.
        HandlerConfirmacao handler = new()
        {
            CorpoPost = "{\"d\":{\"ConfirmationGroup\":\"5420\",\"ConfirmationCount\":\"2\",\"OrderID\":\"1000909\",\"to_ProdnOrdConfMatlDocItm\":{\"results\":[]}}}"
        };
        using HttpClient http = new(handler);
        ConfirmacaoProducaoSapRequest payload = new ConfirmacaoProducaoSapPayloadBuilder()
            .MontarPreview(LancamentoPersistido(), OperacaoConfirmacao(), DataUtc)
            .Payload!;

        ConfirmacaoProducaoSapResponse r = await new ConfirmacaoProducaoSapApiClient(ConfigConfirmacao(true), http)
            .EnviarConfirmacaoAsync(payload, "corr-conf");

        Assert.False(r.Sucesso);
        Assert.Contains("não lançou o consumo 261", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("nenhum item de material foi gerado", r.Mensagem, StringComparison.Ordinal);
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }

    private sealed class HandlerSap261 : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> CorposPost { get; } = [];
        public HttpStatusCode StatusPost { get; init; } = HttpStatusCode.Created;
        public string CorpoPost { get; init; } =
            "{\"d\":{\"MaterialDocument\":\"5000000124\",\"MaterialDocumentYear\":\"2026\"}}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Method == HttpMethod.Get)
            {
                HttpResponseMessage csrf = new(HttpStatusCode.OK);
                csrf.Headers.TryAddWithoutValidation("X-CSRF-Token", "tok123");
                return csrf;
            }

            string corpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            CorposPost.Add(corpo);
            return new HttpResponseMessage(StatusPost) { Content = new StringContent(CorpoPost) };
        }
    }

    private sealed class HandlerConfirmacao : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> CorposPost { get; } = [];
        public HttpStatusCode StatusPost { get; init; } = HttpStatusCode.Created;
        public string CorpoPost { get; init; } =
            "{\"d\":{\"ConfirmationGroup\":\"CG001\",\"ConfirmationCount\":\"0001\",\"ManufacturingOrder\":\"1000009\",\"MaterialDocument\":\"5000000124\",\"MaterialDocumentYear\":\"2026\",\"GoodsMovementIsFinallyPosted\":true}}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Method == HttpMethod.Get
                && request.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? csrfValores)
                && csrfValores.Contains("Fetch"))
            {
                HttpResponseMessage csrf = new(HttpStatusCode.OK);
                csrf.Headers.TryAddWithoutValidation("X-CSRF-Token", "tok-conf");
                return csrf;
            }

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"d\":{\"results\":[{\"OrderID\":\"1000909\",\"Sequence\":\"0\",\"OrderOperation\":\"0010\",\"OrderOperationInternalID\":\"1\",\"Plant\":\"3007\",\"WorkCenter\":\"1\",\"ConfirmationUnit\":\"KG\"},{\"OrderID\":\"1000909\",\"Sequence\":\"0\",\"OrderOperation\":\"0060\",\"OrderOperationInternalID\":\"6\",\"Plant\":\"3007\",\"WorkCenter\":\"6\",\"ConfirmationUnit\":\"KG\"}]}}")
                };
            }

            string corpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            CorposPost.Add(corpo);
            return new HttpResponseMessage(StatusPost) { Content = new StringContent(CorpoPost) };
        }
    }

    private sealed class FakeConfirmacaoSap : IConfirmacaoProducaoSapClient
    {
        public int Chamadas { get; private set; }
        public int Validacoes { get; private set; }
        public int ConsultasOperacoes { get; private set; }
        public string UltimoPayloadJson { get; private set; } = string.Empty;
        public IReadOnlyList<OperacaoConfirmacaoSap> Operacoes { get; init; } =
        [
            new()
            {
                OrderId = "1000009",
                Sequence = "0",
                OrderOperation = "0060",
                OrderOperationInternalId = "6",
                Plant = "3007",
                WorkCenter = "6",
                ConfirmationUnit = "KG"
            }
        ];
        public bool EnvioHabilitado => true;
        public ResultadoEnvioConfirmacaoProducao? ResultadoValidacao { get; init; }
        public ResultadoEnvioConfirmacaoProducao Resultado { get; init; } = new()
        {
            Sucesso = true,
            Mensagem = "Confirmação de Produção enviada ao SAP. Grupo CG001/0001.",
            ConfirmationGroup = "CG001",
            ConfirmationCount = "0001",
            ManufacturingOrder = "1000009",
            DocumentoMaterialSap = "5000000124",
            ExercicioDocumentoMaterialSap = "2026",
            StatusHttp = 201,
            CorrelationId = "corr-conf"
        };

        public ResultadoEnvioConfirmacaoProducao ValidarProntoParaEnvio()
        {
            Validacoes++;
            return ResultadoValidacao ?? new ResultadoEnvioConfirmacaoProducao
            {
                Sucesso = true,
                Mensagem = "OK"
            };
        }

        public Task<ResultadoEnvioConfirmacaoProducao> EnviarConfirmacaoAsync(
            ConfirmacaoProducaoSapRequest requisicao,
            string chaveNegocio,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            UltimoPayloadJson = ConfirmacaoProducaoSapPayloadBuilder.SerializarPreview(requisicao);
            return Task.FromResult(Resultado);
        }

        public Task<IReadOnlyList<OperacaoConfirmacaoSap>> ConsultarOperacoesConfirmacaoAsync(
            string numeroOrdem,
            CancellationToken cancellationToken = default)
        {
            ConsultasOperacoes++;
            return Task.FromResult(Operacoes);
        }
    }

    private static ConsumoMaterialServico ServicoConfirmacao(FakeRepo repo, FakeConfirmacaoSap confirmacao)
        => new(
            new FakeProdOrder { Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemBackflushConfirmacao()) },
            () => repo,
            () => new FakeSap261(),
            () => confirmacao);

    private static OrdemProducaoSap OrdemBackflushConfirmacao(string operacao = "0060", string sequencia = "0")
        => new()
        {
            NumeroOrdem = "1000009",
            Centro = "3007",
            Liberada = true,
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1000009",
                    Material = "QM002",
                    Centro = "3007",
                    Deposito = "PP01",
                    Reserva = "6676",
                    ItemReserva = "2",
                    QuantidadeNecessaria = 10m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "KG",
                    TipoMovimento = "261",
                    BackflushSap = true,
                    Lote = "LOTE-261",
                    Operacao = operacao,
                    SequenciaOperacao = sequencia
                }
            ]
        };
}
