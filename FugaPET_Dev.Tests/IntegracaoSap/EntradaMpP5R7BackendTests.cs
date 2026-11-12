using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R7BackendTests
{
    private static readonly EscopoP5 Escopo = new(new("123", "10"), 900, "SYNTHETIC");
    private static readonly HomologacaoP5 Simulacao = new(true, true, true, true, true, true,
        TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2));

    [Theory]
    [InlineData("10", "00010")]
    [InlineData("00010", "00010")]
    [InlineData("0", "00000")]
    public void ChaveCanonica(string item, string esperado) => Assert.Equal(esperado, new ChaveP5("123", item).Item);

    [Theory]
    [InlineData("1'", "10")]
    [InlineData("123", "100000")]
    [InlineData("", "10")]
    [InlineData("123", "١٠")]
    public void ChaveInvalidaBloqueia(string pedido, string item) => Assert.Throws<ArgumentException>(() => new ChaveP5(pedido, item));

    [Fact]
    public void D2UsaParametrosTipadosEViewSemTokens()
    {
        using NpgsqlConnection connection = new();
        using NpgsqlCommand command = EntradaMpP5D2Repositorio.CriarComando(EntradaMpP5D2Repositorio.SqlLocal, Escopo.Chave, null, connection);
        Assert.StartsWith("WITH entrada AS", command.CommandText);
        Assert.DoesNotContain("PREPARE ", command.CommandText);
        Assert.Contains("$1", command.CommandText);
        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal(NpgsqlDbType.Text, command.Parameters[0].NpgsqlDbType);
        Assert.Equal(NpgsqlDbType.Bigint, command.Parameters[2].NpgsqlDbType);
        Assert.Equal(DBNull.Value, command.Parameters[2].Value);
        string safe = EntradaMpP5D2Repositorio.SqlRecovery;
        Assert.Contains("FROM homologacao.vw_entrada_mp_p5_d2_recuperacao", safe);
        Assert.DoesNotContain("FROM homologacao.entrada_mp_p5_guarda", safe);
        Assert.DoesNotContain("FROM homologacao.entrada_mp_p5_tentativa", safe);
        Assert.Contains("FALSE AS prova_negativa_sap", safe);
    }

    [Fact]
    public void D2VazioOuCategoriaDesconhecidaNaoLibera()
    {
        Assert.Equal(CenarioRecebimentoLocal.Indisponivel, EntradaMpP5D2Repositorio.Classificar([], []).Cenario);
        Dictionary<string, object?> local = Local();
        local["categoria_local_p5"] = "NOVO_STATUS";
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
    }

    [Theory]
    [InlineData("ENVIO_OU_RESULTADO_REMOTO_INCERTO_BLOCK")]
    [InlineData("OUTRO_LANCAMENTO_LOCAL_BLOCK")]
    [InlineData("CANCELAMENTO_LOCAL_SEM_PROVA_ESTORNO_BLOCK")]
    [InlineData("RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK")]
    public void D2HistoricoNaoViraAusencia(string categoria)
    {
        Dictionary<string, object?> local = Local(); local["categoria_local_p5"] = categoria;
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
    }

    [Fact]
    public void D2PreparacaoNaoIgnoraFkAusenteOuDivergente()
    {
        Dictionary<string, object?> local = Local(); local["categoria_local_p5"] = "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO";
        local["eh_lancamento_atual"] = true; local["material_local"] = "SYNTHETIC";
        local["cache_itens"] = "[{\"material\":\"SYNTHETIC\",\"sap_item_id\":1}]";
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
        local["codigo_sap_pedido_compra_item"] = 2L;
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
        local["codigo_sap_pedido_compra_item"] = 1L;
        Assert.Equal(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
    }

    [Fact]
    public async Task DependenciasAusentesNaoConsultamFonteOuBanco()
    {
        FakeBackend backend = new(); Source source = new();
        ResultadoFluxoP5 result = await new CoordenadorP5R7(backend, source, new(), (_, _) => throw new Exception()).ExecutarAsync(Escopo);
        Assert.Equal(EstadoP5.Bloqueado, result.Estado);
        Assert.Equal(0, source.Consultas);
        Assert.Empty(backend.Eventos);
        NeverConnect factory = new();
        EntradaMpP5D2Repositorio d2 = new(factory, new());
        Assert.Equal(CenarioRecebimentoLocal.Indisponivel, (await d2.ConsultarAsync("123", "10", 900)).Cenario);
        EntradaMpP5BackendRepositorio repo = new(factory, d2, Simulacao);
        Assert.False(((IBackendP5)repo).ResultadoLegadoAtomicoDisponivel);
        Assert.False(((IBackendP5)repo).ReservaLegadaCoordenadaDisponivel);
        Assert.False(((IBackendP5)repo).RecoveryR6BAprovado);
        ClaimP5 claim = new(Escopo, 1, Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(EstadoP5.Bloqueado,
            await ((IBackendP5)repo).ConfirmarEnvioDuravelAsync(claim, default));
        Assert.False(await ((IBackendP5)repo).CancelarReservaPreHttpCommitAsync(claim, default));
        Assert.False(BloqueioP5R7.MutacoesHomologadas);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ReservarAsync(Escopo, Prova(), default));
        Assert.Equal(0, factory.Tentativas);
    }

    [Fact]
    public void TokensNaoSaoPublicosNemExpostosPorToString()
    {
        Guid claimToken = Guid.NewGuid(); Guid recoveryToken = Guid.NewGuid();
        ClaimP5 claim = new(Escopo, 1, claimToken, Guid.NewGuid());
        RecoveryP5 recovery = new(Escopo, 1, claim.Correlacao, recoveryToken, 1, EstadoP5.Indeterminado, DateTimeOffset.UtcNow);
        Assert.False(typeof(ClaimP5).IsPublic); Assert.False(typeof(RecoveryP5).IsPublic);
        Assert.DoesNotContain(claimToken.ToString(), claim.ToString());
        Assert.DoesNotContain(recoveryToken.ToString(), recovery.ToString());
        Assert.DoesNotContain(claimToken.ToString(), recovery.ToString());
        Assert.DoesNotContain("Refer", Prova().ToString());
    }

    [Fact]
    public async Task ConcorrenciaSimuladaPermiteUmDispatchECommitsPrecedemHttp()
    {
        FakeBackend backend = new(); int dispatches = 0;
        CoordenadorP5R7 coordinator = new(backend, new Source(), Simulacao, (_, _) =>
        {
            Assert.Contains("COMMIT_ENVIO", backend.Eventos);
            Interlocked.Increment(ref dispatches);
            return Task.FromResult(new RetornoHttpP5(201, new("DOC", "2026", "1"), true));
        });
        ResultadoFluxoP5[] results = await Task.WhenAll(Task.Run(() => coordinator.ExecutarAsync(Escopo)), Task.Run(() => coordinator.ExecutarAsync(Escopo)));
        Assert.Equal(1, dispatches);
        Assert.Single(results, result => result.Estado == EstadoP5.Confirmado);
        Assert.Equal(new[] { "COMMIT_RESERVA", "COMMIT_ENVIO", "COMMIT_RESULTADO" }, backend.Eventos);
    }

    [Theory]
    [InlineData("RESERVA", "Reservado")]
    [InlineData("ANTES_ENVIO", "Reservado")]
    [InlineData("APOS_ENVIO", "EnvioIniciado")]
    public async Task CrashOuCommitAmbiguoNaoFazDispatchNemRetry(string falha, string esperado)
    {
        FakeBackend backend = new() { Falha = falha }; int dispatches = 0;
        CoordenadorP5R7 coordinator = new(backend, new Source(), Simulacao, (_, _) => { dispatches++; return Task.FromResult(new RetornoHttpP5(null, null, false)); });
        await coordinator.ExecutarAsync(Escopo);
        await coordinator.ExecutarAsync(Escopo);
        Assert.Equal(0, dispatches); Assert.Equal(esperado, backend.Estado.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutOu500NaoPermiteSegundoEnvio(bool timeout)
    {
        FakeBackend backend = new(); int dispatches = 0;
        CoordenadorP5R7 coordinator = new(backend, new Source(), Simulacao, (_, _) =>
        {
            dispatches++;
            if (timeout) throw new TimeoutException("claim_token=SECRET_INJECTION");
            return Task.FromResult(new RetornoHttpP5(500, null, false));
        });
        ResultadoFluxoP5 result = await coordinator.ExecutarAsync(Escopo);
        await coordinator.ExecutarAsync(Escopo);
        Assert.Equal(1, dispatches); Assert.Equal(EstadoP5.Indeterminado, backend.Estado);
        Assert.DoesNotContain("SECRET_INJECTION", result.ToString());
    }

    [Fact]
    public async Task FalhaPersistenciaDepoisDoHttpMantemEnvioBloqueante()
    {
        FakeBackend backend = new() { Falha = "RESULTADO" }; int dispatches = 0;
        CoordenadorP5R7 coordinator = new(backend, new Source(), Simulacao, (_, _) =>
        { dispatches++; return Task.FromResult(new RetornoHttpP5(201, new("DOC", "2026", "1"), true)); });
        ResultadoFluxoP5 result = await coordinator.ExecutarAsync(Escopo);
        await coordinator.ExecutarAsync(Escopo);
        Assert.False(result.Persistido); Assert.Equal(EstadoP5.Indeterminado, result.Estado);
        Assert.Equal(EstadoP5.EnvioIniciado, backend.Estado); Assert.Equal(1, dispatches);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void RecoveryRejeitaProvaAnteriorOuIgualAoInicio(int segundos)
    {
        DateTimeOffset inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        ProvaSapP5 prova = Prova() with { ObtidaEm = inicio.AddSeconds(segundos) };
        Assert.False(prova.Valida(Escopo.Chave, DateTimeOffset.UtcNow, Simulacao.EvidenciaMaxAge, inicio));
    }

    [Theory]
    [InlineData("FUTURA")]
    [InlineData("EXPIRADA")]
    [InlineData("TTL")]
    [InlineData("INCOMPLETA")]
    [InlineData("ESTORNOS")]
    [InlineData("OUTRO_ITEM")]
    public void RecoveryRejeitaProvaInvalida(string cenario)
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        ProvaSapP5 prova = cenario switch
        {
            "FUTURA" => Prova() with { ObtidaEm = agora.AddMinutes(1) },
            "EXPIRADA" => Prova() with { ValidaAte = agora },
            "TTL" => Prova() with { ValidaAte = agora.AddHours(1) },
            "INCOMPLETA" => Prova() with { HistoricoCompleto = false },
            "ESTORNOS" => Prova() with { EstornosReconciliados = false },
            _ => Prova() with { Chave = new("123", "20") }
        };
        Assert.False(prova.Valida(Escopo.Chave, agora, Simulacao.EvidenciaMaxAge));
    }

    [Fact]
    public async Task RecoveryNegativoResolveSemPostEReconsulta()
    {
        FakeBackend backend = new() { Estado = EstadoP5.Indeterminado };
        ResultadoFluxoP5 result = await new RecoveryP5R7(backend, new Source(), Simulacao).ReconciliarAsync(Escopo, 1, backend.Correlacao);
        Assert.True(result.Persistido); Assert.Equal(EstadoP5.NaoContabilizado, result.Estado);
        Assert.Contains("READ_BACK", backend.Eventos);
    }

    [Fact]
    public async Task ProvaExpiradaDuranteCommitNaoFazDispatch()
    {
        AdjustableClock clock = new();
        FakeBackend backend = new() { DepoisCommitEnvio = () => clock.Agora = DateTimeOffset.UtcNow.AddHours(1) };
        int dispatches = 0;
        ResultadoFluxoP5 result = await new CoordenadorP5R7(backend, new Source(), Simulacao,
            (_, _) => { dispatches++; return Task.FromResult(new RetornoHttpP5(null, null, false)); }, clock).ExecutarAsync(Escopo);
        Assert.Equal(0, dispatches); Assert.Equal(EstadoP5.EnvioIniciado, backend.Estado);
        Assert.NotEqual(EstadoP5.Confirmado, result.Estado);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecoveryCommitAplicadoComRespostaPerdidaReconsultaSemRepetirMutacao(bool preHttp)
    {
        FakeBackend backend = new() { Estado = preHttp ? EstadoP5.Reservado : EstadoP5.Indeterminado, Falha = "RECOVERY_COMMIT" };
        ResultadoFluxoP5 result = await new RecoveryP5R7(backend, new Source(), Simulacao).ReconciliarAsync(Escopo, 1, backend.Correlacao);
        Assert.True(result.Persistido);
        Assert.Equal(preHttp ? EstadoP5.CanceladoSemEnvio : EstadoP5.NaoContabilizado, result.Estado);
        Assert.Contains("READ_BACK", backend.Eventos);
    }

    [Fact]
    public async Task RecoveryPreHttpCancelaSemCriarTentativa()
    {
        FakeBackend backend = new() { Estado = EstadoP5.Reservado };
        ResultadoFluxoP5 result = await new RecoveryP5R7(backend, new Source(), Simulacao).ReconciliarAsync(Escopo, 1, backend.Correlacao);
        Assert.True(result.Persistido); Assert.Equal(EstadoP5.CanceladoSemEnvio, result.Estado);
        Assert.DoesNotContain("COMMIT_ENVIO", backend.Eventos);
    }

    [Fact]
    public async Task RecoveryGeracaoAntigaPerdeFencingETokenEhSeparado()
    {
        FakeBackend backend = new() { Estado = EstadoP5.Indeterminado };
        RecoveryP5 antiga = await backend.AssumirRecoveryCommitAsync(Escopo, 1, backend.Correlacao, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.AssumirRecoveryCommitAsync(Escopo, 1, backend.Correlacao, default));
        backend.LeaseExpirado = true;
        RecoveryP5 nova = await backend.AssumirRecoveryCommitAsync(Escopo, 1, backend.Correlacao, default);
        Assert.True(nova.Geracao > antiga.Geracao); Assert.NotEqual(nova.Token, antiga.Token);
        Assert.NotEqual(backend.ClaimToken, nova.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => backend.FinalizarRecoveryCommitAsync(antiga, Prova(), default));
    }

    [Fact]
    public void ComandosBackendNaoChamamReconciliacaoLegadaENaoInterpolamToken()
    {
        using NpgsqlConnection connection = new();
        Guid token = Guid.NewGuid();
        using NpgsqlCommand command = EntradaMpP5BackendRepositorio.CriarComando(EntradaMpP5BackendRepositorio.Operacao.MarcarEnvio,
            connection, null, (NpgsqlDbType.Bigint, 1L), (NpgsqlDbType.Uuid, token), (NpgsqlDbType.Uuid, Guid.NewGuid()));
        Assert.Equal("SELECT * FROM homologacao.fn_entrada_mp_p5_marcar_envio($1,$2,$3)", command.CommandText);
        Assert.DoesNotContain(token.ToString(), command.CommandText);
        Assert.Equal(NpgsqlDbType.Uuid, command.Parameters[1].NpgsqlDbType);
        Assert.Equal(6, Enum.GetValues<EntradaMpP5BackendRepositorio.Operacao>().Length);
    }

    [Fact]
    public async Task TodasAsMutacoesReaisBloqueadasAntesDeAbrirConexao()
    {
        NeverConnect factory = new();
        EntradaMpP5BackendRepositorio repo = new(factory, new(factory, Simulacao), Simulacao);
        ClaimP5 claim = new(Escopo, 1, Guid.NewGuid(), Guid.NewGuid());
        RecoveryP5 recovery = new(Escopo, 1, claim.Correlacao, Guid.NewGuid(), 1,
            EstadoP5.Indeterminado, DateTimeOffset.UtcNow.AddMinutes(-2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.MarcarEnvioCommitAsync(claim, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RegistrarResultadoCommitAsync(claim, new(500, null, false), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AssumirRecoveryCommitAsync(Escopo, 1, claim.Correlacao, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.FinalizarRecoveryCommitAsync(recovery, Prova(), default));
        RecoveryP5 reservado = new(Escopo, 1, claim.Correlacao, Guid.NewGuid(), 1, EstadoP5.Reservado, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CancelarRecoveryCommitAsync(reservado, default));
        Assert.Equal(0, factory.Tentativas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task QualquerDependenciaNaoHomologadaBloqueia(int ausente)
    {
        HomologacaoP5 incompleta = ausente switch
        {
            0 => Simulacao with { QaR6 = false }, 1 => Simulacao with { Seguranca = false },
            2 => Simulacao with { FonteSap = false }, 3 => Simulacao with { Banco = false },
            4 => Simulacao with { DispatcherUnico = false }, 5 => Simulacao with { AutorizacaoHera = false },
            6 => Simulacao with { EvidenciaMaxAge = null }, _ => Simulacao with { RecoveryLease = null }
        };
        Source source = new(); FakeBackend backend = new();
        ResultadoFluxoP5 result = await new CoordenadorP5R7(backend, source, incompleta,
            (_, _) => throw new Exception("DISPATCH_FORBIDDEN")).ExecutarAsync(Escopo);
        Assert.Equal(EstadoP5.Bloqueado, result.Estado);
        Assert.Equal(0, source.Consultas); Assert.Empty(backend.Eventos);
    }

    private static ProvaSapP5 Prova() => new(Escopo.Chave, "REF_SYNTHETIC", "OFFLINE_FAKE",
        DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddMinutes(1), true, true, 0, 0);

    [Fact]
    public async Task SemAtomicidadeLegadaBloqueiaAntesDeProvaReservaEDispatch()
    {
        FakeBackend backend = new() { AtomicidadeDisponivel = false };
        Source fonte = new(); int dispatches = 0;
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, fonte, Simulacao,
            (_, _) => { dispatches++; return Task.FromResult(new RetornoHttpP5(201, new("DOC", "2026", "1"), true)); }).ExecutarAsync(Escopo);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Equal(0, fonte.Consultas); Assert.Equal(0, dispatches); Assert.Empty(backend.Eventos);
    }

    [Fact]
    public async Task SemAtomicidadeLegadaRecoveryNaoMutaNemConsultaSap()
    {
        FakeBackend backend = new() { AtomicidadeDisponivel = false, Estado = EstadoP5.Indeterminado };
        Source fonte = new();
        ResultadoFluxoP5 resultado = await new RecoveryP5R7(backend, fonte, Simulacao).ReconciliarAsync(Escopo, 1, backend.Correlacao);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Equal(EstadoP5.Indeterminado, backend.Estado);
        Assert.Equal(0, fonte.Consultas); Assert.Empty(backend.Eventos);
    }
    private static Dictionary<string, object?> Local() => new()
    {
        ["chave_valida"] = true, ["prova_negativa_sap"] = false, ["categoria_local_p5"] = "SEM_ENTRADA_LOCAL_NAO_PROVA_SAP",
        ["lancamentos_sem_item_no_pedido"] = 0L, ["cache_item_count"] = 1L
    };
    private static Dictionary<string, object?> RecoveryRow() => new()
    {
        ["chave_valida"] = true, ["prova_negativa_sap"] = false,
        ["p5_local_decision"] = "NULL_BLOCK_NEEDS_AUTHORITATIVE_SAP_PROOF_AND_ATOMIC_GUARD"
    };

    [Theory]
    [InlineData("NULL_BLOCK_NO_LOCAL_GUARD")]
    [InlineData("NULL_BLOCK_PENDING_OR_UNKNOWN_REMOTE")]
    [InlineData("NULL_BLOCK_OTHER_LOCAL_ATTEMPT")]
    public void BootstrapAusenteOuTentativaPendentePermaneceBloqueado(string decisao)
    {
        Dictionary<string, object?> recovery = RecoveryRow(); recovery["p5_local_decision"] = decisao;
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro, EntradaMpP5D2Repositorio.Classificar([Local()], [recovery]).Cenario);
    }
    [Fact]
    public async Task ReservaSemCoordenacaoLegadaNaoMutaNemDespacha()
    {
        FakeBackend backend = new() { ReservaLegadaCoordenadaDisponivel = false };
        Source fonte = new();
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, fonte, Simulacao,
            (_, _) => throw new Exception("DISPATCH_FORBIDDEN")).ExecutarAsync(Escopo);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Empty(backend.Eventos);
        Assert.Equal(0, fonte.Consultas);
    }

    [Theory]
    [InlineData(EstadoP5.Reservado)]
    [InlineData(EstadoP5.Bloqueado)]
    [InlineData(EstadoP5.Indeterminado)]
    public async Task SemConfirmacaoDuravelNaoDespachaNemCompensaCommitPossivel(object estado)
    {
        FakeBackend backend = new() { ConfirmacaoDuravel = (EstadoP5)estado };
        int dispatches = 0;
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, new Source(), Simulacao,
            (_, _) => { dispatches++; return Task.FromResult(new RetornoHttpP5(null, null, false)); }).ExecutarAsync(Escopo);
        Assert.Equal(0, dispatches);
        Assert.False(resultado.Persistido);
        Assert.Equal(0, backend.Compensacoes);
        await new CoordenadorP5R7(backend, new Source(), Simulacao,
            (_, _) => { dispatches++; return Task.FromResult(new RetornoHttpP5(null, null, false)); }).ExecutarAsync(Escopo);
        Assert.Equal(0, dispatches);
    }

    [Fact]
    public async Task CancelamentoAntesDeMarcarEnvioCompensaComTokenIndependente()
    {
        using CancellationTokenSource cancelamento = new();
        FakeBackend backend = new() { DepoisReserva = cancelamento.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CoordenadorP5R7(backend, new Source(), Simulacao,
                (_, _) => throw new Exception("DISPATCH_FORBIDDEN")).ExecutarAsync(Escopo, cancelamento.Token));
        Assert.Equal(1, backend.Compensacoes);
        Assert.Equal(EstadoP5.CanceladoSemEnvio, backend.Estado);
        Assert.DoesNotContain("COMMIT_ENVIO", backend.Eventos);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProvaExpiradaAposReservaCompensaOuPreservaIncerteza(bool compensa)
    {
        AdjustableClock clock = new();
        FakeBackend backend = new()
        {
            DepoisReserva = () => clock.Agora = DateTimeOffset.UtcNow.AddHours(1),
            CompensacaoDisponivel = compensa
        };
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, new Source(), Simulacao,
            (_, _) => throw new Exception("DISPATCH_FORBIDDEN"), clock).ExecutarAsync(Escopo);
        Assert.Equal(1, backend.Compensacoes);
        Assert.Equal(compensa ? EstadoP5.CanceladoSemEnvio : EstadoP5.Reservado, resultado.Estado);
        Assert.Equal(compensa, resultado.Persistido);
        Assert.DoesNotContain("COMMIT_ENVIO", backend.Eventos);
    }

    [Fact]
    public async Task RecoverySemR6BNaoLiberaResultadoNegativo()
    {
        FakeBackend backend = new() { RecoveryR6BAprovado = false, Estado = EstadoP5.Indeterminado };
        Source fonte = new();
        ResultadoFluxoP5 resultado = await new RecoveryP5R7(backend, fonte, Simulacao)
            .ReconciliarAsync(Escopo, 1, backend.Correlacao);
        Assert.Equal(EstadoP5.Bloqueado, resultado.Estado);
        Assert.Equal(EstadoP5.Indeterminado, backend.Estado);
        Assert.Equal(0, fonte.Consultas);
        Assert.Empty(backend.Eventos);
    }

    [Fact]
    public async Task DispatchRecebeIdentidadeDaMesmaTentativaAposConfirmacaoDuravel()
    {
        FakeBackend backend = new();
        int dispatches = 0;
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, new Source(), Simulacao,
            (claim, _) =>
            {
                Assert.Equal(1, backend.ConfirmacoesDuraveis);
                Assert.Equal(Escopo, claim.Escopo);
                Assert.Equal(1, claim.Tentativa);
                Assert.Equal(backend.Correlacao, claim.Correlacao);
                Assert.Equal(backend.ClaimToken, claim.Token);
                Assert.DoesNotContain(claim.Token.ToString(), claim.ToString());
                dispatches++;
                return Task.FromResult(new RetornoHttpP5(201, new("DOC", "2026", "1"), true));
            }).ExecutarAsync(Escopo);
        Assert.Equal(1, dispatches);
        Assert.Equal(EstadoP5.Confirmado, resultado.Estado);
    }

    [Fact]
    public void FkNulaNaoEquivaleAIdentidadeConfirmada()
    {
        Dictionary<string, object?> local = Local();
        local["categoria_local_p5"] = "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO";
        local["eh_lancamento_atual"] = true;
        local["material_local"] = "SYNTHETIC";
        local["cache_itens"] = "[{\"material\":\"SYNTHETIC\",\"sap_item_id\":1}]";
        local["codigo_sap_pedido_compra_item"] = null;
        Assert.NotEqual(CenarioRecebimentoLocal.SemRegistro,
            EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfirmadoLocalDistingueDocumentoSemConfirmacao(bool confirmado)
    {
        Dictionary<string, object?> local = Local();
        local["categoria_local_p5"] = "RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK";
        local["status_item"] = confirmado ? "CONFIRMADO_SAP" : "ENVIADO_SAP";
        Assert.Equal(confirmado ? CenarioRecebimentoLocal.Confirmado : CenarioRecebimentoLocal.Pendente,
            EntradaMpP5D2Repositorio.Classificar([local], [RecoveryRow()]).Cenario);
    }

    [Theory]
    [InlineData("RESERVA", EstadoP5.Reservado)]
    [InlineData("APOS_ENVIO", EstadoP5.EnvioIniciado)]
    [InlineData("ANTES_ENVIO", EstadoP5.Reservado)]
    public async Task CommitIncertoReconsultaCorrelacaoSemRepetirDispatch(string falha, object esperado)
    {
        FakeBackend backend = new() { Falha = falha };
        ResultadoFluxoP5 resultado = await new CoordenadorP5R7(backend, new Source(), Simulacao,
            (_, _) => throw new Exception("DISPATCH_FORBIDDEN")).ExecutarAsync(Escopo);
        Assert.Contains("READ_BACK", backend.Eventos);
        Assert.Equal((EstadoP5)esperado, resultado.Estado);
        Assert.Equal(0, backend.Compensacoes);
    }

    [Fact]
    public async Task BackendUsaRelogioInjetadoNaReservaERecovery()
    {
        AdjustableClock clock = new() { Agora = new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        NeverConnect factory = new();
        EntradaMpP5BackendRepositorio repo = new(factory, new(factory, Simulacao), Simulacao, clock);
        ProvaSapP5 prova = Prova() with
        {
            ObtidaEm = clock.Agora.AddSeconds(-1), ValidaAte = clock.Agora.AddMinutes(1)
        };
        InvalidOperationException reserva = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.ReservarAsync(Escopo, prova, default));
        Assert.Equal("P5_BACKEND_NAO_HOMOLOGADO", reserva.Message);
        RecoveryP5 recovery = new(Escopo, 1, Guid.NewGuid(), Guid.NewGuid(), 1,
            EstadoP5.Indeterminado, clock.Agora.AddMinutes(-2));
        InvalidOperationException reconciliacao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repo.FinalizarRecoveryCommitAsync(recovery, prova, default));
        Assert.Equal("P5_BACKEND_NAO_HOMOLOGADO", reconciliacao.Message);
        Assert.Equal(0, factory.Tentativas);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResultadoCommitadoComRespostaPerdidaReconsultaSemReenvio(bool confirmado)
    {
        FakeBackend backend = new() { Falha = "RESULTADO_COMMIT" };
        int dispatches = 0;
        CoordenadorP5R7 coordenador = new(backend, new Source(), Simulacao, (_, _) =>
        {
            dispatches++;
            return Task.FromResult(confirmado
                ? new RetornoHttpP5(201, new("DOC", "2026", "1"), true)
                : new RetornoHttpP5(null, null, false));
        });
        ResultadoFluxoP5 resultado = await coordenador.ExecutarAsync(Escopo);
        await coordenador.ExecutarAsync(Escopo);
        Assert.True(resultado.Persistido);
        Assert.Equal(confirmado ? EstadoP5.Confirmado : EstadoP5.Indeterminado, resultado.Estado);
        Assert.Equal(1, dispatches);
        Assert.Contains("READ_BACK", backend.Eventos);
    }

    [Fact]
    public async Task CancelamentoDaFronteiraReconsultaSemCompensarCommitPossivel()
    {
        using CancellationTokenSource cts = new();
        FakeBackend backend = new()
        {
            DepoisCommitEnvio = () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CoordenadorP5R7(backend, new Source(), Simulacao,
                (_, _) => throw new Exception("DISPATCH_FORBIDDEN")).ExecutarAsync(Escopo, cts.Token));
        Assert.Equal(EstadoP5.EnvioIniciado, backend.Estado);
        Assert.Equal(0, backend.Compensacoes);
        Assert.Contains("READ_BACK", backend.Eventos);
    }

    private sealed class Source : IFonteProvaP5
    {
        internal int Consultas;
        public Task<ProvaSapP5?> ObterValidadaAsync(EscopoP5 escopo, CancellationToken cancellationToken)
        { Consultas++; return Task.FromResult<ProvaSapP5?>(Prova()); }
    }
    private sealed class AdjustableClock : TimeProvider
    {
        internal DateTimeOffset Agora = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Agora;
    }
    private sealed class NeverConnect : IFabricaConexaoBanco
    {
        internal int Tentativas;
        public NpgsqlConnection CriarConexao() { Tentativas++; throw new Exception("REAL_DB_FORBIDDEN"); }
        public Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
        { Tentativas++; throw new Exception("REAL_DB_FORBIDDEN"); }
        public string ObterConnectionString() => throw new Exception("REAL_DB_FORBIDDEN");
    }
    private sealed class FakeBackend : IBackendP5
    {
        public bool ReservaLegadaCoordenadaDisponivel { get; init; } = true;
        public bool RecoveryR6BAprovado { get; init; } = true;
        internal EstadoP5 ConfirmacaoDuravel = EstadoP5.EnvioIniciado;
        internal int ConfirmacoesDuraveis;
        internal int Compensacoes;
        internal bool CompensacaoDisponivel = true;
        internal Action? DepoisReserva;
        public Task<EstadoP5> ConfirmarEnvioDuravelAsync(ClaimP5 claim, CancellationToken cancellationToken)
        {
            Assert.Equal(1, claim.Tentativa);
            Assert.Equal(Correlacao, claim.Correlacao);
            Assert.Equal(ClaimToken, claim.Token);
            ConfirmacoesDuraveis++;
            return Task.FromResult(ConfirmacaoDuravel);
        }
        public Task<bool> CancelarReservaPreHttpCommitAsync(ClaimP5 claim, CancellationToken cancellationToken)
        {
            Assert.False(cancellationToken.IsCancellationRequested);
            Compensacoes++;
            if (CompensacaoDisponivel) Estado = EstadoP5.CanceladoSemEnvio;
            return Task.FromResult(CompensacaoDisponivel);
        }
        internal bool AtomicidadeDisponivel = true;
        public bool ResultadoLegadoAtomicoDisponivel => AtomicidadeDisponivel;
        private readonly object _lock = new();
        internal EstadoP5 Estado = EstadoP5.Bloqueado;
        internal readonly List<string> Eventos = [];
        internal string? Falha;
        internal readonly Guid Correlacao = Guid.NewGuid();
        internal readonly Guid ClaimToken = Guid.NewGuid();
        private long _geracao;
        private Guid _recoveryToken;
        internal bool LeaseExpirado;
        private RetornoHttpP5? _resultadoPersistido;
        internal Action? DepoisCommitEnvio;
        public Task<bool> D2SemImpedimentoAsync(EscopoP5 escopo, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<ClaimP5> ReservarAsync(EscopoP5 escopo, ProvaSapP5 prova, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (Estado != EstadoP5.Bloqueado) throw new InvalidOperationException("EXISTING_ATTEMPT");
                Estado = EstadoP5.Reservado; Eventos.Add("COMMIT_RESERVA");
                DepoisReserva?.Invoke();
                if (Falha == "RESERVA")
                    throw new CommitP5IndeterminadoException(new ClaimP5(escopo, 1, ClaimToken, Correlacao));
                return Task.FromResult(new ClaimP5(escopo, 1, ClaimToken, Correlacao));
            }
        }
        public Task MarcarEnvioCommitAsync(ClaimP5 claim, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (Estado != EstadoP5.Reservado || Falha == "ANTES_ENVIO") throw new InvalidOperationException("BLOCKED");
                Estado = EstadoP5.EnvioIniciado; Eventos.Add("COMMIT_ENVIO");
                DepoisCommitEnvio?.Invoke();
                if (Falha == "APOS_ENVIO") throw new InvalidOperationException("COMMIT_UNKNOWN");
                return Task.CompletedTask;
            }
        }
        public Task RegistrarResultadoCommitAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (Falha == "RESULTADO") throw new InvalidOperationException("LOCAL_FAILED");
                Estado = retorno.Confirmado ? EstadoP5.Confirmado : EstadoP5.Indeterminado;
                _resultadoPersistido = retorno;
                if (Falha == "RESULTADO_COMMIT") throw new InvalidOperationException("COMMIT_RESPONSE_LOST");
                Eventos.Add("COMMIT_RESULTADO"); return Task.CompletedTask;
            }
        }
        public Task<EstadoP5> ConfirmarResultadoPersistidoAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                Eventos.Add("READ_BACK");
                return Task.FromResult(claim.Escopo == Escopo && claim.Tentativa == 1
                    && claim.Token == ClaimToken && claim.Correlacao == Correlacao
                    && _resultadoPersistido == retorno ? Estado : EstadoP5.Bloqueado);
            }
        }
        public Task<RecoveryP5> AssumirRecoveryCommitAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_geracao > 0 && !LeaseExpirado) throw new InvalidOperationException("CONCURRENT_OWNER");
                _geracao++; _recoveryToken = Guid.NewGuid(); LeaseExpirado = false;
                return Task.FromResult(new RecoveryP5(escopo, tentativa, correlacao, _recoveryToken, _geracao, Estado,
                    Estado == EstadoP5.Reservado ? null : DateTimeOffset.UtcNow.AddMinutes(-2)));
            }
        }
        public Task FinalizarRecoveryCommitAsync(RecoveryP5 recovery, ProvaSapP5 prova, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (recovery.Geracao != _geracao || recovery.Token != _recoveryToken) throw new InvalidOperationException("STALE_FENCE");
                Estado = prova.Negativa ? EstadoP5.NaoContabilizado : EstadoP5.Confirmado;
                _recoveryToken = Guid.Empty;
                if (Falha == "RECOVERY_COMMIT") throw new InvalidOperationException("COMMIT_RESPONSE_LOST");
                return Task.CompletedTask;
            }
        }
        public Task CancelarRecoveryCommitAsync(RecoveryP5 recovery, CancellationToken cancellationToken)
        {
            Estado = EstadoP5.CanceladoSemEnvio;
            if (Falha == "RECOVERY_COMMIT") throw new InvalidOperationException("COMMIT_RESPONSE_LOST");
            return Task.CompletedTask;
        }
        public Task<EstadoP5> ConsultarEstadoAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken)
        {
            Assert.Equal(Escopo, escopo);
            Assert.Equal(1, tentativa);
            Assert.Equal(Correlacao, correlacao);
            Assert.False(cancellationToken.IsCancellationRequested);
            Eventos.Add("READ_BACK");
            return Task.FromResult(Estado);
        }
    }
}
