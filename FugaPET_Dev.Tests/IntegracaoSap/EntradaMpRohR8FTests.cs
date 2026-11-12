using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpRohR8FTests
{
    private static readonly DateTimeOffset Agora = new(2040, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly EscopoP5 Escopo = new(new("000123", "10"), 900, "SYNTHETIC");
    private static readonly IdentidadeItemProdutoSapR8B Identidade = new(Escopo, 41, 71);
    private static readonly HomologacaoP5 Homologacao = new(true, true, true, true, true, true,
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2));
    private static readonly DocumentoP5 Documento = new("SYNTHETIC", "2040", "0001");

    [Fact]
    public async Task SemBackendDisponivelNaoReservaNemDespacha()
    {
        ExecutorSimulado executor = new() { Disponivel = false };
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(executor.Eventos);
        Assert.Equal(0, executor.Posts);
    }

    [Theory]
    [InlineData("HIBE")]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    [InlineData("FK")]
    [InlineData("MATERIAL")]
    [InlineData("REVOGADA")]
    [InlineData("EXPIRADA")]
    [InlineData("VERSAO")]
    [InlineData("SEM_EVIDENCIA")]
    public async Task ClassificacaoIncompletaOuNaoRohNaoIniciaTransacao(string caso)
    {
        ExecutorSimulado executor = new();
        PedidoReservaRohR8F pedido = Pedido();
        ReciboResolucaoSqlR8D classificacao = pedido.Classificacao;
        DadosEvidenciaSqlR8D dados = classificacao.Dados;
        dados = caso switch
        {
            "FK" => dados with { Identidade = dados.Identidade with { CodigoSapItem = 72 } },
            "MATERIAL" => dados with { Product = "OUTRO" },
            "EXPIRADA" => dados with { ValidadeAte = Agora },
            "SEM_EVIDENCIA" => dados with { EvidenciaId = 0 },
            "HIBE" or "" or "UNKNOWN" => dados with { ProductType = caso },
            _ => dados
        };
        classificacao = classificacao with { Dados = dados,
            EstadoVinculo = caso == "REVOGADA" ? "REVOGADA" : "VALIDADA",
            VersaoVinculo = caso == "VERSAO" ? 0 : 1 };
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(pedido with { Classificacao = classificacao }, executor.Dispatch);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(executor.Eventos);
        Assert.Equal(0, executor.Posts);
    }

    [Fact]
    public async Task FronteiraExigeCommitsSeparadosEHttpOcorreForaDaTransacao()
    {
        ExecutorSimulado executor = new();
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Confirmado, resultado.Estado);
        Assert.True(resultado.Persistido);
        Assert.Equal(1, executor.Posts);
        Assert.False(executor.HttpDuranteTransacao);
        Assert.Equal(new[] { "BEGIN", "RESERVA", "COMMIT", "BEGIN", "MARCAR", "COMMIT",
            "BEGIN", "SELAR", "COMMIT", "BEGIN", "CONSULTAR", "COMMIT", "POST",
            "BEGIN", "RESULTADO_INTEGRAL", "COMMIT" }, executor.Eventos);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task CommitIncertoNuncaCompensaNemRepetePost(int commit)
    {
        ExecutorSimulado executor = new() { CommitIncerto = commit };
        PercursoRohR8F percurso = Criar(executor);
        ResultadoFluxoP5 resultado = await percurso.ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Indeterminado, resultado.Estado);
        Assert.False(resultado.Persistido);
        Assert.Equal(commit == 5 ? 1 : 0, executor.Posts);
        Assert.DoesNotContain("COMPENSAR", executor.Eventos);
        await percurso.ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(commit == 5 ? 1 : 0, executor.Posts);
    }

    [Theory]
    [InlineData("RESERVA")]
    [InlineData("RESULTADO_INTEGRAL")]
    public async Task FalhaNaOperacaoNaoConfirmaEstadoParcialNemRepeteHttp(string operacao)
    {
        ExecutorSimulado executor = new() { Falha = operacao };
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.False(resultado.Persistido);
        Assert.Equal(EstadoP5.Indeterminado, resultado.Estado);
        Assert.Equal("ROLLBACK_SIMULADO", executor.Eventos.Last());
        Assert.Equal(operacao == "RESERVA" ? 0 : 1, executor.Posts);
    }

    [Theory]
    [InlineData("MULTI")]
    [InlineData("COBERTURA")]
    [InlineData("TROCA_EVIDENCIA")]
    [InlineData("TROCA_VINCULO")]
    [InlineData("TROCA_PK")]
    public async Task ReciboDeReservaDivergenteNaoProduzPost(string caso)
    {
        ExecutorSimulado executor = new() { AlterarReserva = recibo => caso switch
        {
            "MULTI" => recibo with { ItensAssociados = 2 },
            "COBERTURA" => recibo with { ItensCobertos = 0 },
            "TROCA_EVIDENCIA" => recibo with { Titular = recibo.Titular with { Evidencia = 102 } },
            "TROCA_VINCULO" => recibo with { Titular = recibo.Titular with { VersaoVinculo = 2 } },
            _ => recibo with { Titular = recibo.Titular with { Identidade = new(Escopo, 42, 71) } }
        }};
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.False(resultado.Persistido);
        Assert.Equal(0, executor.Posts);
        Assert.DoesNotContain("COMMIT", executor.Eventos);
    }

    [Theory]
    [InlineData("SEM_ENVIO")]
    [InlineData("SEM_SELO")]
    [InlineData("CLAIM")]
    [InlineData("CORRELACAO")]
    [InlineData("GERACAO")]
    [InlineData("VERSAO")]
    public async Task EstadoDuravelIncompletoOuTitularDivergenteNaoAutorizaHttp(string caso)
    {
        ExecutorSimulado executor = new() { AlterarEstado = recibo => caso switch
        {
            "SEM_ENVIO" => recibo with { EnvioIniciadoEm = null },
            "SEM_SELO" => recibo with { FronteiraConfirmadaEm = null },
            "VERSAO" => recibo with { VersaoGuardaAnterior = 9 },
            _ => recibo with { Titular = TrocarClaim(recibo.Titular, caso) }
        }};
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Indeterminado, resultado.Estado);
        Assert.Equal(0, executor.Posts);
        Assert.DoesNotContain("COMPENSAR", executor.Eventos);
    }

    [Theory]
    [InlineData("TIMEOUT")]
    [InlineData("HTTP500")]
    [InlineData("CRASH")]
    public async Task ResultadoRemotoDesconhecidoBloqueiaSegundoPost(string caso)
    {
        ExecutorSimulado executor = new() { FalhaHttp = caso };
        PercursoRohR8F percurso = Criar(executor);
        ResultadoFluxoP5 primeiro = await percurso.ExecutarAsync(Pedido(), executor.Dispatch);
        ResultadoFluxoP5 segundo = await percurso.ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Indeterminado, primeiro.Estado);
        Assert.Equal(EstadoP5.Bloqueado, segundo.Estado);
        Assert.Equal(1, executor.Posts);
    }

    [Theory]
    [InlineData("DOCUMENTO")]
    [InlineData("ITEM_DOCUMENTO")]
    [InlineData("COBERTURA")]
    [InlineData("ESTADO_ITEM")]
    public async Task WriterRohExigeConfirmacaoIntegralDoDocumento(string caso)
    {
        ExecutorSimulado executor = new() { AlterarResultado = recibo => caso switch
        {
            "DOCUMENTO" => recibo with { Documento = Documento with { Numero = "OUTRO" } },
            "ITEM_DOCUMENTO" => recibo with { Documento = Documento with { Item = "0002" } },
            "COBERTURA" => recibo with { ItensCobertos = 0 },
            _ => recibo with { StatusItem = "ERRO_SAP" }
        }};
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Indeterminado, resultado.Estado);
        Assert.False(resultado.Persistido);
        Assert.Equal(1, executor.Posts);
    }

    [Theory]
    [InlineData("CLAIM")]
    [InlineData("CORRELACAO")]
    [InlineData("GERACAO")]
    [InlineData("ENVIO")]
    [InlineData("SELO")]
    [InlineData("HTTP")]
    [InlineData("COMMIT")]
    public async Task CompensacaoExigeAusenciaDeHttpEIdentidadeExata(string caso)
    {
        ExecutorSimulado executor = new() { AlterarEstado = recibo => caso switch
        {
            "ENVIO" => recibo with { EnvioIniciadoEm = Agora },
            "SELO" => recibo with { FronteiraConfirmadaEm = Agora },
            "HTTP" => recibo with { HttpTentado = true },
            "COMMIT" => recibo with { CommitIncerto = true },
            _ => recibo with { Titular = TrocarClaim(recibo.Titular, caso) }
        }};
        ResultadoFluxoP5 resultado = await Criar(executor).CompensarAsync(Reserva());
        Assert.False(resultado.Persistido);
        Assert.DoesNotContain("COMPENSAR", executor.Eventos);
        Assert.Equal(0, executor.Posts);
    }

    [Fact]
    public async Task CompensacaoPreHttpUsaConsultaEOperacaoIntegralNaMesmaUnidade()
    {
        ExecutorSimulado executor = new();
        ResultadoFluxoP5 resultado = await Criar(executor).CompensarAsync(Reserva());
        Assert.Equal(EstadoP5.CanceladoSemEnvio, resultado.Estado);
        Assert.True(resultado.Persistido);
        Assert.Equal(new[] { "BEGIN", "CONSULTAR", "COMPENSAR", "COMMIT" }, executor.Eventos);
    }

    [Fact]
    public async Task DuasChamadasConcorrentesDaMesmaEntradaNaoFazemDoisDispatches()
    {
        ExecutorSimulado executor = new();
        PercursoRohR8F percurso = Criar(executor);
        TaskCompletionSource entrou = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<ResultadoFluxoP5> primeiro = percurso.ExecutarAsync(Pedido(), async (titular, ct) =>
        {
            entrou.SetResult();
            await liberar.Task;
            return await executor.Dispatch(titular, ct);
        });
        await entrou.Task;
        ResultadoFluxoP5 segundo = await percurso.ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Bloqueado, segundo.Estado);
        liberar.SetResult();
        Assert.Equal(EstadoP5.Confirmado, (await primeiro).Estado);
        Assert.Equal(1, executor.Posts);
    }

    [Fact]
    public async Task ExecutorNpgsqlRealBloqueiaAntesDeConsultarFabrica()
    {
        FabricaSentinela fabrica = new();
        TransacaoRohR8F executor = new(fabrica, new ContratoSqlRohIndisponivelR8F());
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecutarAsync<int>((_, _) => Task.FromResult(1), default));
        Assert.Equal(0, fabrica.Aberturas);
        Assert.False(executor.Disponivel);
    }

    [Theory]
    [InlineData("PEDIDO")]
    [InlineData("IDENTIDADE")]
    [InlineData("CLASSIFICACAO")]
    public async Task EntradaSemDadosObrigatoriosRetornaBloqueioSemTransacao(string campo)
    {
        PedidoReservaRohR8F pedido = campo switch
        {
            "PEDIDO" => null!,
            "IDENTIDADE" => Pedido() with { Identidade = null! },
            _ => Pedido() with { Classificacao = null! }
        };
        ExecutorSimulado executor = new();
        Assert.Equal(EstadoP5.Bloqueado, (await Criar(executor).ExecutarAsync(pedido, executor.Dispatch)).Estado);
        Assert.Empty(executor.Eventos);
    }

    [Theory]
    [InlineData("FINALIZADO_LOCAL")]
    [InlineData("ERRO_SAP")]
    [InlineData("CONFIRMADO_SAP")]
    public async Task HttpIncertoNaoAceitaReciboQueReabraOuConfirmeLancamento(string status)
    {
        ExecutorSimulado executor = new() { FalhaHttp = "HTTP500",
            AlterarResultado = recibo => recibo with { StatusLancamento = status } };
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(EstadoP5.Indeterminado, resultado.Estado);
        Assert.False(resultado.Persistido);
        Assert.Equal(1, executor.Posts);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RelogioInjetadoExpiraProvaSemLiberarHttpAposCommits(int commit)
    {
        Relogio relogio = new();
        ExecutorSimulado executor = new() { AposCommit = numero => { if (numero == commit) relogio.AgoraAtual = Agora.AddMinutes(3); } };
        ResultadoFluxoP5 resultado = await new PercursoRohR8F(executor, Homologacao, relogio)
            .ExecutarAsync(Pedido(), executor.Dispatch);
        Assert.Equal(commit == 1 ? EstadoP5.CanceladoSemEnvio : EstadoP5.Indeterminado, resultado.Estado);
        Assert.Equal(0, executor.Posts);
        Assert.Equal(commit == 1, executor.Eventos.Contains("COMPENSAR"));
    }

    [Fact]
    public async Task CancelamentoDepoisDaReservaCompensaComTokenNaoCancelado()
    {
        using CancellationTokenSource cts = new();
        ExecutorSimulado executor = new() { AposCommit = numero => { if (numero == 1) cts.Cancel(); } };
        ResultadoFluxoP5 resultado = await Criar(executor).ExecutarAsync(Pedido(), executor.Dispatch, cts.Token);
        Assert.Equal(EstadoP5.CanceladoSemEnvio, resultado.Estado);
        Assert.True(resultado.Persistido);
        Assert.False(executor.CompensouComTokenCancelado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryNaoRecebeDispatcherNemPermissaoDeNovoPost(bool negativo)
    {
        ExecutorSimulado executor = new() { Papel = PapelP5R8.Recovery };
        PedidoRecoveryRohR8F pedido = PedidoRecovery(negativo);
        ResultadoFluxoP5 resultado = await new RecoveryRohR8F(executor, Homologacao, new Relogio()).ReconciliarAsync(pedido);
        Assert.Equal(negativo ? EstadoP5.NaoContabilizado : EstadoP5.Confirmado, resultado.Estado);
        Assert.True(resultado.Persistido);
        Assert.Equal(0, executor.Posts);
    }

    [Theory]
    [InlineData("TOKEN")]
    [InlineData("GERACAO")]
    [InlineData("CORRELACAO")]
    [InlineData("NOVO_POST")]
    [InlineData("IDENTIDADE")]
    [InlineData("COBERTURA")]
    public async Task RecoveryDivergenteNaoConfirmaNemEnvia(string campo)
    {
        ExecutorSimulado executor = new() { Papel = PapelP5R8.Recovery, RecoveryInvalido = campo };
        ResultadoFluxoP5 resultado = await new RecoveryRohR8F(executor, Homologacao, new Relogio()).ReconciliarAsync(PedidoRecovery(false));
        Assert.False(resultado.Persistido);
        Assert.Equal(0, executor.Posts);
    }

    [Fact]
    public async Task RecoveryNaoPodeUsarExecutorProvider()
    {
        ExecutorSimulado executor = new();
        ResultadoFluxoP5 resultado = await new RecoveryRohR8F(executor, Homologacao, new Relogio()).ReconciliarAsync(PedidoRecovery(false));
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(executor.Eventos);
    }

    [Fact]
    public async Task StubsSqlNuncaExecutamNemQuandoRecebemPedidoCompleto()
    {
        IContratoSqlRohR8F contrato = new ContratoSqlRohIndisponivelR8F();
        await Assert.ThrowsAsync<InvalidOperationException>(() => contrato.ReservarIntegralAsync(null!, null!, Pedido(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => contrato.RegistrarResultadoIntegralAsync(null!, null!, new(Titular(), new(201, Documento, true)), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => contrato.CompensarPreHttpIntegralAsync(null!, null!, new(Titular(), "FINALIZADO_LOCAL"), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => contrato.ReconciliarSemReenvioAsync(null!, null!, PedidoRecovery(false), default));
    }

    private static PercursoRohR8F Criar(ExecutorSimulado executor) => new(executor, Homologacao, new Relogio());
    private static PedidoReservaRohR8F Pedido() => new(Identidade, Classificacao(), 1,
        new(Escopo.Chave, "SYNTHETIC", "SYNTHETIC", Agora, Agora.AddMinutes(2), true, true, 0, 0));
    private static ReciboResolucaoSqlR8D Classificacao() => new(new(101,
        new("000123", "00010", 900, 41, 71, "SYNTHETIC"), "SYNTHETIC", "ROH", "API_PRODUCT_SRV/A_Product",
        "SYNTHETIC", "SYNTHETIC", null, null, false, null, null, "SYNTHETIC", Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Agora, Agora.AddMinutes(2), 1), 201, 1, "VALIDADA", Agora);
    private static TitularRohR8F Titular() => new(Identidade,
        new(Escopo, 1, Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"), 0, 1), 101, 201, 1);
    private static ReciboReservaRohR8F Reserva() => new(Titular(), Classificacao(), 1, EstadoP5.Reservado, 1, 1, "FINALIZADO_LOCAL");
    private static TitularRohR8F TrocarClaim(TitularRohR8F titular, string campo) => titular with { Claim =
        new(Escopo, 1, campo == "CLAIM" ? Guid.NewGuid() : titular.Claim.Token,
            campo == "CORRELACAO" ? Guid.NewGuid() : titular.Claim.Correlacao, campo == "GERACAO" ? 1 : 0, 1) };
    private static PedidoRecoveryRohR8F PedidoRecovery(bool negativo) => new(Identidade,
        new(Escopo, 1, Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"), 1, EstadoP5.Indeterminado, Agora.AddMinutes(-1)),
        new(Escopo.Chave, "SYNTHETIC", "SYNTHETIC", Agora, Agora.AddMinutes(2), true, true,
            negativo ? 0 : 1, 0, negativo ? null : Documento));
    private sealed class Relogio : TimeProvider
    {
        internal DateTimeOffset AgoraAtual = Agora;
        public override DateTimeOffset GetUtcNow() => AgoraAtual;
    }

    private sealed class FabricaSentinela : IFabricaConexaoBanco
    {
        internal int Aberturas;
        public NpgsqlConnection CriarConexao() { Aberturas++; throw new InvalidOperationException(); }
        public Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken ct = default)
        { Aberturas++; throw new InvalidOperationException(); }
        public string ObterConnectionString() => throw new InvalidOperationException();
    }

    private sealed class ExecutorSimulado : IExecutorRohR8F, ISessaoRohR8F
    {
        public bool Disponivel { get; set; } = true;
        public PapelP5R8 Papel { get; set; } = PapelP5R8.Provider;
        internal readonly List<string> Eventos = [];
        internal int Posts;
        internal bool HttpDuranteTransacao;
        internal int CommitIncerto;
        internal string? Falha;
        internal string? FalhaHttp;
        internal string? RecoveryInvalido;
        internal Action<int>? AposCommit;
        internal bool CompensouComTokenCancelado;
        internal Func<ReciboReservaRohR8F, ReciboReservaRohR8F>? AlterarReserva;
        internal Func<ReciboEstadoRohR8F, ReciboEstadoRohR8F>? AlterarEstado;
        internal Func<ReciboResultadoRohR8F, ReciboResultadoRohR8F>? AlterarResultado;
        private int _commits;
        private bool _aberta;
        private bool _envio;
        private bool _selo;
        public async Task<T> ExecutarAsync<T>(Func<ISessaoRohR8F, CancellationToken, Task<T>> acao, CancellationToken ct)
        {
            Eventos.Add("BEGIN"); _aberta = true;
            bool envioAnterior = _envio, seloAnterior = _selo;
            T resultado;
            try { resultado = await acao(this, ct); }
            catch { _envio = envioAnterior; _selo = seloAnterior; Eventos.Add("ROLLBACK_SIMULADO"); throw; }
            finally { _aberta = false; }
            if (++_commits == CommitIncerto) { Eventos.Add("COMMIT_INCERTO"); throw new CommitRohIndeterminadoR8FException(); }
            Eventos.Add("COMMIT"); AposCommit?.Invoke(_commits); return resultado;
        }
        private void Operacao(string nome) { Eventos.Add(nome); if (Falha == nome) throw new InvalidOperationException("SIMULADO"); }
        public Task<RetornoHttpP5> Dispatch(TitularRohR8F titular, CancellationToken ct)
        {
            Eventos.Add("POST"); Posts++; HttpDuranteTransacao |= _aberta;
            if (FalhaHttp == "TIMEOUT") throw new TimeoutException();
            if (FalhaHttp == "CRASH") throw new InvalidOperationException("CRASH_SIMULADO");
            return Task.FromResult(FalhaHttp == "HTTP500" ? new RetornoHttpP5(500, null, false) : new(201, Documento, true));
        }
        public Task<ReciboReservaRohR8F> ReservarIntegralAsync(PedidoReservaRohR8F pedido, CancellationToken ct)
        { Operacao("RESERVA"); ReciboReservaRohR8F recibo = Reserva(); return Task.FromResult(AlterarReserva?.Invoke(recibo) ?? recibo); }
        private ReciboEstadoRohR8F Estado()
        { ReciboEstadoRohR8F recibo = new(Titular(), 1, _envio ? EstadoP5.EnvioIniciado : EstadoP5.Reservado,
            _envio ? Agora : null, _selo ? Agora : null, false, false); return AlterarEstado?.Invoke(recibo) ?? recibo; }
        public Task<ReciboEstadoRohR8F> MarcarEnvioAsync(TitularRohR8F titular, CancellationToken ct)
        { Operacao("MARCAR"); _envio = true; return Task.FromResult(Estado()); }
        public Task<ReciboEstadoRohR8F> SelarFronteiraAsync(TitularRohR8F titular, CancellationToken ct)
        { Operacao("SELAR"); _selo = true; return Task.FromResult(Estado()); }
        public Task<ReciboEstadoRohR8F> ConsultarTentativaAsync(TitularRohR8F titular, CancellationToken ct)
        { Operacao("CONSULTAR"); return Task.FromResult(Estado()); }
        public Task<ReciboResultadoRohR8F> RegistrarResultadoIntegralAsync(PedidoResultadoRohR8F pedido, CancellationToken ct)
        {
            Operacao("RESULTADO_INTEGRAL"); bool sucesso = pedido.Retorno.Confirmado;
            ReciboResultadoRohR8F recibo = new(Titular(), 1, sucesso ? EstadoP5.Confirmado : EstadoP5.Indeterminado,
                sucesso ? "CONFIRMADO_SAP" : "ENVIADO_SAP", sucesso ? "CONFIRMADO_SAP" : "FINALIZADO_LOCAL",
                sucesso ? Documento : null, 1, 1);
            return Task.FromResult(AlterarResultado?.Invoke(recibo) ?? recibo);
        }
        public Task<ReciboCompensacaoRohR8F> CompensarPreHttpIntegralAsync(PedidoCompensacaoRohR8F pedido, CancellationToken ct)
        { Operacao("COMPENSAR"); CompensouComTokenCancelado |= ct.IsCancellationRequested; return Task.FromResult(new ReciboCompensacaoRohR8F(Titular(), 1, EstadoP5.CanceladoSemEnvio, "FINALIZADO_LOCAL", 1, 1)); }
        public Task<ReciboRecoveryRohR8F> ReconciliarSemReenvioAsync(PedidoRecoveryRohR8F pedido, CancellationToken ct)
        {
            Operacao("RECOVERY"); RecoveryP5 original = pedido.Recovery;
            RecoveryP5 recovery = new(original.Escopo, original.Tentativa,
                RecoveryInvalido == "CORRELACAO" ? Guid.NewGuid() : original.Correlacao,
                RecoveryInvalido == "TOKEN" ? Guid.NewGuid() : original.Token,
                RecoveryInvalido == "GERACAO" ? 2 : original.Geracao, original.Estado, original.EnvioIniciadoEm);
            return Task.FromResult(new ReciboRecoveryRohR8F(recovery,
                pedido.Prova.Negativa ? EstadoP5.NaoContabilizado : EstadoP5.Confirmado, pedido.Prova.Documento,
                RecoveryInvalido == "NOVO_POST", RecoveryInvalido == "IDENTIDADE" ? new(Escopo, 42, 71) : Identidade,
                1, RecoveryInvalido == "COBERTURA" ? 0 : 1));
        }
    }
}
