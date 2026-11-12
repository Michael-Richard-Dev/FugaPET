using System.Data;
using System.Data.Common;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R8Tests
{
    private static readonly EscopoP5 Escopo = new(new("4500000071", "00010"), 71, "1000111");
    private static readonly DateTimeOffset Agora = new(2040, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly HomologacaoP5 Homologacao = new(true, true, true, true, true, true,
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2));

    [Fact]
    public async Task DependenciasReaisAusentesBloqueiamAntesDeQualquerComando()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        ExecutorSimulado recovery = new(PapelP5R8.Recovery);
        EntradaMpP5BackendR8 backend = Criar(provider, recovery, new());
        Assert.False(backend.ResultadoLegadoAtomicoDisponivel);
        Assert.False(backend.ReservaLegadaCoordenadaDisponivel);
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.ReservarAsync(Escopo, Prova(), default));
        Assert.Empty(provider.Eventos);
        Assert.Empty(recovery.Eventos);
    }

    [Fact]
    public void ProviderERecoveryNaoCompartilhamExecutor()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        Assert.Throws<ArgumentException>(() => Criar(provider, provider));
        Assert.Throws<ArgumentException>(() => Criar(new(PapelP5R8.Recovery), new(PapelP5R8.Provider)));
    }

    [Fact]
    public async Task ReservaCarimboELegadoUsamUmUnicoCommit()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        ClaimP5 claim = await Criar(provider).ReservarAsync(Escopo, Prova(), default);
        Assert.Equal(Escopo, claim.Escopo);
        Assert.Equal(0, claim.GeracaoRecovery);
        Assert.Equal(2, claim.VersaoGuarda);
        Assert.Equal(new[] { "BEGIN", "Reservar", "Identidade", "StatusLegado", "CarimbarReserva", "ReservarLegado", "COMMIT" }, provider.Eventos);
    }

    [Theory]
    [InlineData("CarimbarReserva")]
    [InlineData("ReservarLegado")]
    [InlineData("Identidade")]
    public async Task FalhaNaReservaFazRollbackSemCommit(string falha)
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider) { Falha = falha };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(provider).ReservarAsync(Escopo, Prova(), default));
        Assert.Equal("ROLLBACK", provider.Eventos.Last());
        Assert.DoesNotContain("COMMIT", provider.Eventos);
    }

    [Fact]
    public async Task MarcacaoEConfirmacaoDuravelTemCommitsSeparadosESeloRepetidoNaoAutoriza()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await backend.ReservarAsync(Escopo, Prova(), default);
        provider.Eventos.Clear();
        await backend.MarcarEnvioCommitAsync(claim, default);
        Assert.Equal("COMMIT", provider.Eventos.Last());
        Assert.Equal(EstadoP5.EnvioIniciado, await backend.ConfirmarEnvioDuravelAsync(claim, default));
        Assert.Equal(EstadoP5.Bloqueado, await backend.ConfirmarEnvioDuravelAsync(claim, default));
        Assert.Equal(3, provider.Eventos.Count(item => item == "COMMIT"));
    }

    [Fact]
    public async Task GeracaoAlteradaBloqueiaAntesDaMutacao()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await backend.ReservarAsync(Escopo, Prova(), default);
        provider.Geracao = 2;
        provider.Eventos.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.MarcarEnvioCommitAsync(claim, default));
        Assert.DoesNotContain("MarcarEnvio", provider.Eventos);
    }

    [Fact]
    public async Task CompensacaoR3BPreHttpELiberacaoP5SaoAtomicas()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await backend.ReservarAsync(Escopo, Prova(), default);
        provider.Eventos.Clear();
        Assert.True(await backend.CancelarReservaPreHttpCommitAsync(claim, default));
        Assert.Equal(new[] { "BEGIN", "Identidade", "CompensarLegado", "CancelarPrePost", "COMMIT" }, provider.Eventos);
    }

    [Fact]
    public async Task ResultadoConfirmadoELegadoUsamUmCommit()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        provider.Eventos.Clear();
        await backend.RegistrarResultadoCommitAsync(claim, new(201, new("5000000001", "2040", "0001"), true), default);
        Assert.Equal(new[] { "BEGIN", "Identidade", "Resultado", "Identidade", "ConfirmarItem", "ConfirmarLegado", "COMMIT" }, provider.Eventos);
    }

    [Theory]
    [InlineData("ConfirmarItem")]
    [InlineData("ConfirmarLegado")]
    public async Task FalhaResultadoLegadoNaoComitaP5Isoladamente(string falha)
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        provider.Eventos.Clear(); provider.Falha = falha;
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.RegistrarResultadoCommitAsync(claim,
            new(201, new("5000000001", "2040", "0001"), true), default));
        Assert.DoesNotContain("COMMIT", provider.Eventos);
        Assert.Equal("ROLLBACK", provider.Eventos.Last());
    }

    [Fact]
    public async Task TimeoutPreservaIndeterminadoSemLiberarErroSap()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        provider.Eventos.Clear();
        await backend.RegistrarResultadoCommitAsync(claim, new(null, null, false), default);
        Assert.DoesNotContain("ConfirmarLegado", provider.Eventos);
        Assert.DoesNotContain("CompensarLegado", provider.Eventos);
        Assert.Equal("RESULTADO_INDETERMINADO", provider.Estado);
    }

    [Fact]
    public async Task RetornoTardioNaoAtualizaLegadoNemAfirmaConfirmacao()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        provider.Estado = "RESULTADO_INDETERMINADO";
        provider.Eventos.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.RegistrarResultadoCommitAsync(claim,
            new(201, new("5000000001", "2040", "0001"), true), default));
        Assert.Contains("RetornoTardio", provider.Eventos);
        Assert.Equal("COMMIT", provider.Eventos.Last());
        Assert.DoesNotContain("ConfirmarLegado", provider.Eventos);
    }

    [Theory]
    [InlineData("MarcarEnvio")]
    [InlineData("ConfirmarFronteira")]
    public async Task CommitAmbiguoNaoAutorizaHttpNemRetry(string operacao)
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider) { CommitAmbiguo = operacao };
        EntradaMpP5BackendR8 backend = Criar(provider);
        int posts = 0;
        CoordenadorP5R7 coordenador = new(backend, new Fonte(), Homologacao, (_, _) =>
        {
            posts++;
            return Task.FromResult(new RetornoHttpP5(201, new("5000000001", "2040", "0001"), true));
        }, new Relogio());
        ResultadoFluxoP5 primeiro = await coordenador.ExecutarAsync(Escopo);
        await coordenador.ExecutarAsync(Escopo);
        Assert.Equal(0, posts);
        Assert.NotEqual(EstadoP5.Confirmado, primeiro.Estado);
        Assert.Contains("ConsultarEstado", provider.Eventos);
    }

    [Fact]
    public async Task DuasEntradasMesmoItemSoUmaChegaAoDispatchSimulado()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        int posts = 0;
        CoordenadorP5R7 coordenador = new(backend, new Fonte(), Homologacao, (_, _) =>
        {
            Assert.Equal("COMMIT", provider.Eventos.Last());
            Interlocked.Increment(ref posts);
            return Task.FromResult(new RetornoHttpP5(null, null, false));
        }, new Relogio());
        await Task.WhenAll(coordenador.ExecutarAsync(Escopo), coordenador.ExecutarAsync(Escopo with { Lancamento = 72 }));
        Assert.Equal(1, posts);
        Assert.Equal("RESULTADO_INDETERMINADO", provider.Estado);
    }

    [Fact]
    public async Task FalhaAntesDoPostNaoDespachaECompensaNaMesmaTransacao()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        using CancellationTokenSource cts = new();
        provider.AposReserva = cts.Cancel;
        EntradaMpP5BackendR8 backend = Criar(provider);
        int posts = 0;
        CoordenadorP5R7 coordenador = new(backend, new Fonte(), Homologacao, (_, _) =>
        {
            posts++;
            return Task.FromResult(new RetornoHttpP5(null, null, false));
        }, new Relogio());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordenador.ExecutarAsync(Escopo, cts.Token));
        Assert.Equal(0, posts);
        Assert.Contains("CompensarLegado", provider.Eventos);
        Assert.Contains("CancelarPrePost", provider.Eventos);
    }

    [Fact]
    public async Task RestartConsultaIdentidadeExplicitaSemNovoDispatch()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        ClaimP5 claim = await PrepararEnvio(Criar(provider));
        provider.Eventos.Clear();
        Assert.Equal(EstadoP5.EnvioIniciado,
            await Criar(provider).ConsultarEstadoAsync(Escopo, claim.Tentativa, claim.Correlacao, default));
        Assert.DoesNotContain("Reservar", provider.Eventos);
        Assert.DoesNotContain("MarcarEnvio", provider.Eventos);
        Assert.Contains("codigo_p5_tentativa=$4", provider.UltimoSql);
        Assert.DoesNotContain("ORDER BY", provider.UltimoSql);
        Assert.DoesNotContain("LIMIT 1", provider.UltimoSql);
    }

    [Fact]
    public async Task DocumentoDivergenteNuncaEConfirmadoPorEstadoSozinho()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        await backend.RegistrarResultadoCommitAsync(claim, new(201, new("5000000001", "2040", "0001"), true), default);
        Assert.Equal(EstadoP5.Bloqueado, await backend.ConfirmarResultadoPersistidoAsync(claim,
            new(201, new("5000000002", "2040", "0001"), true), default));
        Assert.Equal(EstadoP5.Bloqueado, await backend.ConfirmarResultadoPersistidoAsync(claim,
            new(201, new("5000000001", "2040", "0001"), true), default));
    }

    [Theory]
    [InlineData("4500000072", "00010")]
    [InlineData("4500000071", "00020")]
    public async Task ProvaDeOutroPedidoOuItemNaoIniciaTransacao(string pedido, string item)
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(provider).ReservarAsync(Escopo,
            Prova() with { Chave = new(pedido, item) }, default));
        Assert.Empty(provider.Eventos);
    }

    [Fact]
    public async Task SemProvaNegativaPrimeiroRohNaoReserva()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(Criar(provider), new Fonte(false), Homologacao,
            (_, _) => throw new InvalidOperationException("POST_PROIBIDO"), new Relogio()).ExecutarAsync(Escopo);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(provider.Eventos);
    }

    [Fact]
    public async Task RecoveryContinuaBloqueadoSemCoordenacaoLegadaHomologada()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        ExecutorSimulado recovery = new(PapelP5R8.Recovery);
        EntradaMpP5BackendR8 backend = Criar(provider, recovery);
        ResultadoFluxoP5 resultado = await new RecoveryP5R7(backend, new Fonte(), Homologacao, new Relogio())
            .ReconciliarAsync(Escopo, 1, Guid.NewGuid());
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(provider.Eventos);
        Assert.Empty(recovery.Eventos);
    }

    [Fact]
    public async Task ExecutorNpgsqlRealNuncaAbreBancoEnquantoTravaEstaticaEstaFechada()
    {
        FabricaProibida fabrica = new();
        TransacaoEntradaMpP5R8 executor = new(fabrica, PapelP5R8.Provider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecutarAsync((_, _) => Task.FromResult(true), default));
        Assert.Equal(0, fabrica.Aberturas);
    }

    [Fact]
    public async Task CompensacaoDepoisDaFronteiraNaoExecutaUpdateLegado()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        provider.Eventos.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.CancelarReservaPreHttpCommitAsync(claim, default));
        Assert.DoesNotContain("CompensarLegado", provider.Eventos);
    }

    [Fact]
    public void FkLocalAusenteNaoEReconstituidaPeloCache()
    {
        Dictionary<string, object?> local = new()
        {
            ["chave_valida"] = true, ["prova_negativa_sap"] = false,
            ["categoria_local_p5"] = "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO", ["eh_lancamento_atual"] = true,
            ["lancamentos_sem_item_no_pedido"] = 0L, ["cache_item_count"] = 1L,
            ["material_local"] = "1000111", ["cache_itens"] = "[{\"material\":\"1000111\",\"sap_item_id\":71}]"
        };
        Dictionary<string, object?> recovery = new()
        {
            ["chave_valida"] = true, ["prova_negativa_sap"] = false,
            ["p5_local_decision"] = "NULL_BLOCK_NEEDS_AUTHORITATIVE_SAP_PROOF_AND_ATOMIC_GUARD"
        };
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [recovery]).Cenario);
        local["codigo_sap_pedido_compra_item"] = 71L;
        Assert.Equal(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [recovery]).Cenario);
        recovery["p5_local_decision"] = "NULL_BLOCK_NO_LOCAL_GUARD";
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [recovery]).Cenario);
    }

    [Fact]
    public void ExecutorSemFencingAtomicoNuncaDeclaraCoordenacaoDisponivel()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider) { FencingAtomicoDisponivel = false };
        Assert.False(Criar(provider).ReservaLegadaCoordenadaDisponivel);
    }

    [Fact]
    public async Task ConfirmacaoPersisteReferenciaDoItemEUsuarioSemHelperPrivadoSql()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        await backend.RegistrarResultadoCommitAsync(claim, new(201, new("5000000001", "2040", "0001"), true), default);
        Assert.Equal("0001", provider.UltimoPedido!.Documento!.Item);
        Assert.Equal(71, provider.UltimoPedido.CodigoUsuario);
        Assert.Equal(OperacaoLegadaP5R8.ConfirmarResultado, provider.UltimoPedido.Operacao);
        Assert.DoesNotContain("fn_p5_r6b_item_canonico", provider.SqlPorOperacao["StatusLegado"]);
    }

    [Fact]
    public async Task P5ConfirmadoSemProvaLegadaCoordenadaNaoConfirmaReconsulta()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await PrepararEnvio(backend);
        RetornoHttpP5 retorno = new(201, new("5000000001", "2040", "0001"), true);
        await backend.RegistrarResultadoCommitAsync(claim, retorno, default);
        Assert.Equal(EstadoP5.Bloqueado, await backend.ConfirmarResultadoPersistidoAsync(claim, retorno, default));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public async Task ReciboSemCoberturaUnicaIntegralFazRollback(int associados, int cobertos)
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider) { ItensAssociados = associados, ItensCobertos = cobertos };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Criar(provider).ReservarAsync(Escopo, Prova(), default));
        Assert.DoesNotContain("COMMIT", provider.Eventos);
        Assert.Equal("ROLLBACK", provider.Eventos.Last());
    }

    [Theory]
    [InlineData("HIBE")]
    [InlineData("UNKNOWN")]
    [InlineData("")]
    public void ClassificacaoDuravelNaoRohNaoEReclassificadaPorAusenciaDeGuarda(string tipo)
    {
        Assert.False(new ClassificacaoDuravelP5R8(Escopo, 71, tipo, "FIXTURE").CorrespondeRoh(Escopo));
        Assert.False(new ClassificacaoDuravelP5R8(Escopo, 0, "ROH", "FIXTURE").CorrespondeRoh(Escopo));
        Assert.False(new ClassificacaoDuravelP5R8(Escopo, 71, "ROH", "").CorrespondeRoh(Escopo));
        Assert.False(new ClassificacaoDuravelP5R8(Escopo, 71, "ROH", "FIXTURE")
            .CorrespondeRoh(Escopo with { Lancamento = 72 }));
        Assert.True(new ClassificacaoDuravelP5R8(Escopo, 71, "ROH", "FIXTURE").CorrespondeRoh(Escopo));
    }

    [Fact]
    public void PesoRealNaoSubstituiQuantidadeDocumentalRoh()
    {
        ResultadoAlocacao101 resultado = EntradaProdutoAlocacao101.Decidir(
            FugaPET_Dev.Modelo.Processo.ClassificacaoEntradaMaterial.MateriaPrima, 1000m, "KG", 296.5m, "1000111");
        Assert.Equal("1000.000", resultado.QuantidadePadrao);
        Assert.Equal("296.500", resultado.PesoLiquidoCustomizado);
        Assert.Equal("KG", resultado.UnidadePesoCustomizado);
    }

    [Fact]
    public void TokensVersaoERecoveryNaoSaoIntercambiaveisNemVazamNoPedido()
    {
        Guid segredo = Guid.NewGuid();
        ClaimP5 claim = new(Escopo, 71, segredo, Guid.NewGuid(), 2, 9);
        PedidoLegadoP5R8 pedido = new(OperacaoLegadaP5R8.Reservar, claim, 71, "FINALIZADO_LOCAL");
        Assert.True(pedido.Valido);
        Assert.Equal(9, claim.VersaoGuarda);
        Assert.Equal(2, claim.GeracaoRecovery);
        Assert.DoesNotContain(segredo.ToString(), pedido.ToString());
        Assert.DoesNotContain(segredo.ToString(), claim.ToString());
        Assert.False(new PedidoLegadoP5R8(OperacaoLegadaP5R8.Reservar,
            new(Escopo, 71, segredo, Guid.NewGuid(), 9, 0), 71, "FINALIZADO_LOCAL").Valido);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutOuHttp500DepoisDePossivelPostNaoPermiteSegundoDispatch(bool http500)
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        int posts = 0;
        CoordenadorP5R7 coordenador = new(Criar(provider), new Fonte(), Homologacao, (_, _) =>
        {
            posts++;
            if (!http500) throw new TimeoutException("SIMULADO_APOS_POSSIVEL_POST");
            return Task.FromResult(new RetornoHttpP5(500, null, false));
        }, new Relogio());
        ResultadoFluxoP5 resultado = await coordenador.ExecutarAsync(Escopo);
        await coordenador.ExecutarAsync(Escopo);
        Assert.Equal(1, posts);
        Assert.Equal(EstadoP5.Indeterminado, resultado.Estado);
        Assert.DoesNotContain("CompensarLegado", provider.Eventos);
        Assert.DoesNotContain("ConfirmarLegado", provider.Eventos);
    }

    [Fact]
    public async Task ErroSapNaoESuficienteParaReenvioSemD2Elegivel()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = new(provider, new ExecutorSimulado(PapelP5R8.Recovery),
            (_, _) => Task.FromResult(false), Homologacao, Simulacao, new Relogio());
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, new Fonte(), Homologacao,
            (_, _) => throw new InvalidOperationException("POST_PROIBIDO"), new Relogio()).ExecutarAsync(Escopo);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(provider.Eventos);
    }

    [Fact]
    public async Task HttpPositivoSemSeloPersisteIncertezaSemPermitirConfirmacao()
    {
        ExecutorSimulado provider = new(PapelP5R8.Provider);
        EntradaMpP5BackendR8 backend = Criar(provider);
        ClaimP5 claim = await backend.ReservarAsync(Escopo, Prova(), default);
        await backend.MarcarEnvioCommitAsync(claim, default);
        provider.Eventos.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.RegistrarResultadoCommitAsync(
            claim, new(201, new("5000000001", "2040", "0001"), true), default));
        Assert.Equal("RESULTADO_INDETERMINADO", provider.Estado);
        Assert.Equal("COMMIT", provider.Eventos.Last());
        Assert.DoesNotContain("ConfirmarLegado", provider.Eventos);
    }

    internal static DependenciasP5R8 Simulacao => new(true, true, true, true, true);
    private static EntradaMpP5BackendR8 Criar(ExecutorSimulado provider, ExecutorSimulado? recovery = null,
        DependenciasP5R8? dependencias = null) => new(provider, recovery ?? new(PapelP5R8.Recovery),
            (_, _) => Task.FromResult(true), Homologacao, dependencias ?? Simulacao, new Relogio());
    private static ProvaSapP5 Prova() => new(Escopo.Chave, "FIXTURE", "FAKE", Agora.AddSeconds(-1),
        Agora.AddMinutes(1), true, true, 0, 0);
    private static async Task<ClaimP5> PrepararEnvio(EntradaMpP5BackendR8 backend)
    {
        ClaimP5 claim = await backend.ReservarAsync(Escopo, Prova(), default);
        await backend.MarcarEnvioCommitAsync(claim, default);
        Assert.Equal(EstadoP5.EnvioIniciado, await backend.ConfirmarEnvioDuravelAsync(claim, default));
        return claim;
    }
    private sealed class Relogio : TimeProvider { public override DateTimeOffset GetUtcNow() => Agora; }
    private sealed class Fonte(bool disponivel = true) : IFonteProvaP5
    {
        public Task<ProvaSapP5?> ObterValidadaAsync(EscopoP5 escopo, CancellationToken cancellationToken)
            => Task.FromResult(disponivel ? Prova() : null);
    }
    private sealed class FabricaProibida : IFabricaConexaoBanco
    {
        internal int Aberturas;
        public NpgsqlConnection CriarConexao() => throw new InvalidOperationException("DB_PROIBIDO");
        public string ObterConnectionString() => throw new InvalidOperationException("DB_PROIBIDO");
        public Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
        { Aberturas++; throw new InvalidOperationException("DB_PROIBIDO"); }
    }

    private sealed class ExecutorSimulado(PapelP5R8 papel) : IExecutorTransacionalP5R8, ISessaoP5R8
    {
        public PapelP5R8 Papel => papel;
        public bool FencingAtomicoDisponivel { get; set; } = true;
        public long? CodigoUsuario => 71;
        internal Dictionary<string, string> SqlPorOperacao { get; } = [];
        internal PedidoLegadoP5R8? UltimoPedido { get; private set; }
        internal int ItensAssociados { get; set; } = 1;
        internal int ItensCobertos { get; set; } = 1;
        internal List<string> Eventos { get; } = [];
        internal string? Falha { get; set; }
        internal long Geracao { get; set; }
        internal string Estado { get; set; } = "RESERVADO";
        internal string? CommitAmbiguo { get; set; }
        internal string UltimoSql { get; private set; } = string.Empty;
        internal Action? AposReserva { get; set; }
        private bool _selado;
        private bool _reservado;
        private Guid _correlacao;
        private DocumentoP5? _documento;
        private long _versao = 1;
        private readonly SemaphoreSlim _mutex = new(1);
        public async Task<T> ExecutarAsync<T>(Func<ISessaoP5R8, CancellationToken, Task<T>> acao, CancellationToken ct)
        {
            await _mutex.WaitAsync(ct);
            Eventos.Add("BEGIN");
            string anterior = Estado;
            bool reservadoAntes = _reservado;
            bool seladoAntes = _selado;
            DocumentoP5? documentoAntes = _documento;
            long versaoAntes = _versao;
            bool commit = false;
            int inicio = Eventos.Count;
            try
            {
                T retorno = await acao(this, ct); Eventos.Add("COMMIT"); commit = true;
                if (retorno is ClaimP5) AposReserva?.Invoke();
                if (CommitAmbiguo is not null && Eventos.Skip(inicio).Contains(CommitAmbiguo))
                    throw new InvalidOperationException("COMMIT_SIMULADO_RESPOSTA_PERDIDA");
                return retorno;
            }
            catch
            {
                if (!commit) { Estado = anterior; _reservado = reservadoAntes; _selado = seladoAntes; _documento = documentoAntes; _versao = versaoAntes; Eventos.Add("ROLLBACK"); }
                throw;
            }
            finally { _mutex.Release(); }
        }
        public Task<T> LerUnicoAsync<T>(NpgsqlCommand comando, Func<DbDataReader, T> ler, CancellationToken ct)
        {
            UltimoSql = comando.CommandText;
            string operacao = comando.CommandText.Split('\n')[0].Replace("-- P5R8:", "").Trim();
            SqlPorOperacao[operacao] = comando.CommandText;
            Eventos.Add(operacao);
            if (Falha == operacao) throw new InvalidOperationException("SIMULADO");
            DataTable tabela = new();
            void Coluna(string nome, object valor) { tabela.Columns.Add(nome, valor.GetType()); }
            Dictionary<string, object> valores = operacao switch
            {
                "Reservar" => Reservar(),
                "Identidade" => new() { ["recovery_generation"] = Geracao, ["versao"] = _versao, ["estado_tentativa"] = Estado, ["estado_guarda"] = Estado, ["reserva_legada_status_anterior"] = "FINALIZADO_LOCAL", ["fronteira_confirmada_em"] = _selado ? Agora : DBNull.Value, ["documento_material_sap"] = (object?)_documento?.Numero ?? DBNull.Value, ["exercicio_documento_material_sap"] = (object?)_documento?.Ano ?? DBNull.Value, ["documento_material_item"] = (object?)_documento?.Item ?? DBNull.Value },
                "StatusLegado" => new() { ["status_lancamento"] = "FINALIZADO_LOCAL" },
                "CarimbarReserva" => new() { ["retorno"] = "RESERVA_LEGADA_CARIMBADA_COMMIT_ANTES_DO_UPDATE_LEGADO" },
                "MarcarEnvio" => new() { ["retorno"] = EstadoEnvio() },
                "ConfirmarFronteira" => new() { ["retorno"] = Selar() },
                "CancelarPrePost" => new() { ["retorno"] = "CANCELADO_SEM_ENVIO_REQUIRES_NEW_SAP_PROOF_FOR_RETRY" },
                "Resultado" => Resultado(comando),
                "RetornoTardio" => new() { ["retorno"] = "RETORNO_TARDIO_REGISTRADO_EVIDENCIA_REGISTRADA_PRIMEIRA_EXIGE_ESCALACAO" },
                "ConsultarEstado" => new() { ["estado_tentativa"] = Estado },
                _ => throw new InvalidOperationException(operacao)
            };
            foreach (var par in valores) Coluna(par.Key, par.Value == DBNull.Value ? string.Empty : par.Value);
            tabela.Rows.Add(valores.Values.ToArray());
            using DbDataReader reader = tabela.CreateDataReader(); reader.Read();
            return Task.FromResult(ler(reader));
        }
        public Task<ReciboLegadoP5R8> AplicarLegadoAsync(PedidoLegadoP5R8 pedido, CancellationToken ct)
        {
            Assert.True(pedido.Valido);
            Assert.Equal(_versao, pedido.Claim.VersaoGuarda);
            UltimoPedido = pedido;
            void Etapa(string nome) { Eventos.Add(nome); if (Falha == nome) throw new InvalidOperationException("SIMULADO"); }
            if (pedido.Operacao == OperacaoLegadaP5R8.Reservar)
            {
                Etapa("CarimbarReserva"); Etapa("ReservarLegado"); _versao++;
            }
            else if (pedido.Operacao == OperacaoLegadaP5R8.CompensarPreHttp) Etapa("CompensarLegado");
            else { Etapa("ConfirmarItem"); Etapa("ConfirmarLegado"); }
            return Task.FromResult(new ReciboLegadoP5R8(_versao, ItensAssociados, ItensCobertos, pedido.Documento));
        }
        private string EstadoEnvio() { Estado = "ENVIO_INICIADO"; _versao++; return "ENVIO_INICIADO_COMMIT_E_CONFIRMAR_FRONTEIRA_ANTES_DO_HTTP"; }
        private string Selar() { bool anterior = _selado; _selado = true; return anterior ? "FRONTEIRA_JA_CONFIRMADA_UM_UNICO_POST_AUTORIZADO" : "FRONTEIRA_CONFIRMADA_UM_UNICO_POST_AUTORIZADO"; }
        private Dictionary<string, object> Reservar()
        {
            if (_reservado) throw new InvalidOperationException("CHAVE_JA_RESERVADA");
            _reservado = true;
            return new() { ["tentativa_id"] = 1L, ["claim"] = Guid.NewGuid(), ["correlacao"] = _correlacao = Guid.NewGuid(), ["estado_resultado"] = "RESERVADO" };
        }
        private Dictionary<string, object> Resultado(NpgsqlCommand comando)
        {
            _versao++;
            Estado = (string)comando.Parameters[3].Value!;
            if (Estado == "CONFIRMADO_SAP") _documento = new((string)comando.Parameters[6].Value!, (string)comando.Parameters[7].Value!, (string)comando.Parameters[8].Value!);
            return new() { ["retorno"] = Estado };
        }
    }
}
