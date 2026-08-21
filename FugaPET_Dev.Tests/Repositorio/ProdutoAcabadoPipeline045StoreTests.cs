using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Repositorio;

/// <summary>
/// FAST-TRACK HML: store PostgreSQL/045 REAL sobre um executor FAKE (sem PostgreSQL real). Prova que o adapter
/// consome SOMENTE funÃ§Ãµes fn_pa_045_*, propaga o token vindo do RETORNO (nunca SELECT de token), usa o token
/// vigente em sucesso/erro/timeout, adquire recovery como nova capability, Ã© fail-closed sem executor, e habilita
/// persistÃªncia definitiva quando configurado.
/// </summary>
public sealed class ProdutoAcabadoPipeline045StoreTests
{
    private sealed class FakeExecutor045 : IProdutoAcabadoPipeline045Executor
    {
        public bool Disponivel { get; set; } = true;
        public List<(string Funcao, IReadOnlyList<Parametro045> Parametros)> Chamadas { get; } = [];
        public Dictionary<string, IReadOnlyList<Linha045>> Respostas { get; } = [];

        public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken ct = default)
        {
            Chamadas.Add((funcao, parametros));
            return Task.FromResult(Respostas.TryGetValue(funcao, out IReadOnlyList<Linha045>? r) ? r : (IReadOnlyList<Linha045>)[]);
        }

        public List<(string View, string Coluna)> Views { get; } = [];
        public Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken ct = default)
        {
            Views.Add((view, colunaFiltro));
            return Task.FromResult(Respostas.TryGetValue(view, out IReadOnlyList<Linha045>? r) ? r : (IReadOnlyList<Linha045>)[]);
        }

        public async Task<T> ExecutarEmTransacaoAsync<T>(Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao, CancellationToken ct = default)
        {
            // Transação fake que apenas encaminha as chamadas ao executor de função existente.
            EncaminhadorTx tx = new(this);
            return await operacao(tx, ct);
        }

        public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(string numeroOrdemProducao, string? terminal, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Linha045>>([]);

        private sealed class EncaminhadorTx : IExecutorFuncoes045Transacional
        {
            private readonly FakeExecutor045 _dono;
            public EncaminhadorTx(FakeExecutor045 dono) => _dono = dono;
            public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken ct = default)
                => _dono.ExecutarFuncaoAsync(funcao, parametros, ct);
        }
    }

    private static Linha045 Linha(params (string col, object? val)[] cols)
        => new(cols.ToDictionary(c => c.col, c => c.val, StringComparer.OrdinalIgnoreCase));

    private static IReadOnlyList<Linha045> LinhaBool(string funcao, bool valor) => [Linha((funcao, valor))];

    private static bool UsaUuid(FakeExecutor045 ex, string funcao, Guid token)
        => ex.Chamadas.Any(c => c.Funcao == funcao && c.Parametros.Any(p => p.Valor is Guid g && g == token));

    // ---------------- persistÃªncia definitiva / fail-closed ----------------
    [Fact]
    public void SuportaPersistenciaDefinitiva_RefleteExecutor()
    {
        Assert.True(new ProdutoAcabadoPipelinePostgresStore(new FakeExecutor045 { Disponivel = true }).SuportaPersistenciaDefinitiva);
        Assert.False(new ProdutoAcabadoPipelinePostgresStore(new FakeExecutor045 { Disponivel = false }).SuportaPersistenciaDefinitiva);
    }

    [Fact]
    public async Task ExecutorIndisponivel_FailClosed_LancaAoExecutar()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ProdutoAcabadoPipeline045ExecutorIndisponivel().ExecutarFuncaoAsync("fn_pa_045_iniciar_fluxo", []));

    [Fact]
    public void Compor_ExecutorDisponivel_PipelineComposto()
    {
        ProdutoAcabadoPipelinePostgresStore store = new(new FakeExecutor045 { Disponivel = true });
        FabricaProdutoAcabadoIntegracaoSapOrquestrador.ResultadoComposicaoPipeline r =
            FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(new ConfiguracaoSap { ProdutoAcabadoPipelineHabilitado = true }, HuServiceReal(), store);
        Assert.True(r.Disponivel);
        Assert.Equal(FabricaProdutoAcabadoIntegracaoSapOrquestrador.MotivoComposto, r.Motivo);
    }

    [Fact]
    public void Compor_ExecutorIndisponivel_FailClosed()
    {
        ProdutoAcabadoPipelinePostgresStore store = new(new FakeExecutor045 { Disponivel = false });
        FabricaProdutoAcabadoIntegracaoSapOrquestrador.ResultadoComposicaoPipeline r =
            FabricaProdutoAcabadoIntegracaoSapOrquestrador.Compor(new ConfiguracaoSap { ProdutoAcabadoPipelineHabilitado = true }, HuServiceReal(), store);
        Assert.False(r.Disponivel);
        Assert.Equal(FabricaProdutoAcabadoIntegracaoSapOrquestrador.MotivoDependenciaGaia, r.Motivo);
    }

    // ---------------- token do retorno, nunca SELECT ----------------
    [Fact]
    public async Task Claim261_PropagaTokenDoRetorno_EUsaNoSucesso()
    {
        Guid token = Guid.NewGuid();
        FakeExecutor045 ex = new();
        ex.Respostas["fn_pa_045_claim_etapa"] = [Linha(("claim_token", token), ("numero_tentativa", 1))];
        ex.Respostas["fn_pa_045_registrar_sucesso"] = LinhaBool("fn_pa_045_registrar_sucesso", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);

        ResultadoClaim045 claim = await store.AdquirirClaimEtapaAsync(-1, ProdutoAcabadoPipelinePostgresStore.Etapa261, 9, "T1");
        Assert.True(claim.Obtido);
        Assert.Equal(token, claim.Token);
        Assert.Equal(1, claim.Tentativa);

        bool ok = await store.RegistrarSucessoEtapaAsync(-1, ProdutoAcabadoPipelinePostgresStore.Etapa261, 201, "5000045261", "2026", "{}", "/MaterialDocument", 9, "T1");
        Assert.True(ok);
        Assert.True(UsaUuid(ex, "fn_pa_045_registrar_sucesso", token)); // token VIGENTE do retorno, nÃ£o um SELECT
    }

    [Fact]
    public async Task RegistrarSucesso_SemClaim_NaoChamaFuncao()
    {
        FakeExecutor045 ex = new();
        ProdutoAcabadoPipelinePostgresStore store = new(ex);
        bool ok = await store.RegistrarSucessoEtapaAsync(-1, "261", 201, "D", "2026", "{}", "/x", 9, "T1");
        Assert.False(ok);
        Assert.DoesNotContain(ex.Chamadas, c => c.Funcao == "fn_pa_045_registrar_sucesso"); // sem token â‡’ nenhuma chamada (nunca busca token)
    }

    [Fact]
    public async Task Erro_E_Timeout_UsamTokenVigente()
    {
        Guid token = Guid.NewGuid();
        FakeExecutor045 ex = new();
        ex.Respostas["fn_pa_045_claim_etapa"] = [Linha(("claim_token", token), ("numero_tentativa", 2))];
        ex.Respostas["fn_pa_045_registrar_erro"] = LinhaBool("fn_pa_045_registrar_erro", true);
        ex.Respostas["fn_pa_045_registrar_timeout"] = LinhaBool("fn_pa_045_registrar_timeout", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);
        await store.AdquirirClaimEtapaAsync(-1, "101", 9, "T1");

        Assert.True(await store.RegistrarErroEtapaAsync(-1, "101", 500, "{}", "erro", "/x", 9, "T1"));
        Assert.True(await store.RegistrarTimeoutEtapaAsync(-1, "101", "timeout", "/x", 9, "T1"));
        Assert.True(UsaUuid(ex, "fn_pa_045_registrar_erro", token));
        Assert.True(UsaUuid(ex, "fn_pa_045_registrar_timeout", token));
    }


    [Fact]
    public async Task RegistrarTimeoutComClaimExplicito_ChamaFuncaoMesmoSemCacheLocal()
    {
        Guid token = Guid.NewGuid();
        FakeExecutor045 ex = new();
        ex.Respostas["fn_pa_045_registrar_timeout"] = LinhaBool("fn_pa_045_registrar_timeout", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);

        bool ok = await store.RegistrarTimeoutEtapaAsync(-1, "261", 1, token, "timeout", "/MaterialDocument", 9, "T1", CancellationToken.None);

        Assert.True(ok);
        Assert.Contains(ex.Chamadas, c => c.Funcao == "fn_pa_045_registrar_timeout");
        Assert.True(UsaUuid(ex, "fn_pa_045_registrar_timeout", token));
        Assert.Contains(ex.Chamadas, c => c.Funcao == "fn_pa_045_registrar_timeout" && c.Parametros.Any(p => p.Valor is int tentativa && tentativa == 1));
    }
    [Fact]
    public async Task Recovery_UsaNovaCapability_RecoveryClaimToken()
    {
        Guid recovery = Guid.NewGuid();
        FakeExecutor045 ex = new();
        // adquirir_recovery retorna recovery_claim_token (NUNCA claim_token) â€” capability nova pÃ³s-restart.
        ex.Respostas["fn_pa_045_adquirir_recovery_etapa"] = [Linha(("recovery_claim_token", recovery), ("numero_tentativa", 1))];
        ex.Respostas["fn_pa_045_registrar_reconciliacao"] = LinhaBool("fn_pa_045_registrar_reconciliacao", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);

        ResultadoClaim045 rec = await store.AdquirirRecoveryEtapaAsync(-1, "261", 9, "T1");
        Assert.True(rec.Obtido);
        Assert.Equal(recovery, rec.Token);

        Assert.True(await store.RegistrarReconciliacaoEtapaAsync(-1, "261", "CONFIRMADO_NAO_EXISTE", 404, null, null, "{}", "erro", "/x", 9, "T1"));
        Assert.True(UsaUuid(ex, "fn_pa_045_registrar_reconciliacao", recovery)); // usa a capability de recovery
    }

    [Fact]
    public async Task Palete_CriacaoRetornaId_ERepassaParaVinculoClaimESucesso()
    {
        Guid token = Guid.NewGuid();
        FakeExecutor045 ex = new();
        ex.Respostas["fn_pa_045_palete_criar"] = [Linha(("codigo_hu_palete", 777L))];
        ex.Respostas["fn_pa_045_palete_vincular_caixa"] = LinhaBool("fn_pa_045_palete_vincular_caixa", true);
        ex.Respostas["fn_pa_045_palete_claim_envio"] = [Linha(("claim_token", token), ("numero_tentativa", 1))];
        ex.Respostas["fn_pa_045_palete_registrar_sucesso"] = LinhaBool("fn_pa_045_palete_registrar_sucesso", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);

        long? codigoCriado = await store.CriarPaleteAsync("PAL-LOCAL", "3007", "PP01", 12.5m, 11.5m, 1m, 9, "T1");
        Assert.True(codigoCriado > 0);
        Assert.True(await store.VincularCaixaPaleteAsync(codigoCriado!.Value, -10, 1, 9, "T1"));
        ResultadoClaim045 claim = await store.ClaimEnvioPaleteAsync(codigoCriado.Value, "{}", "/int012", 9, "T1");
        Assert.True(claim.Obtido);
        Assert.True(await store.RegistrarSucessoPaleteAsync(codigoCriado.Value, 200, "300099999", "{}", "/int012", 9, "T1"));

        Assert.Contains(ex.Chamadas, c => c.Funcao == "fn_pa_045_palete_criar");
        Assert.Contains(ex.Chamadas, c => c.Funcao == "fn_pa_045_palete_vincular_caixa" && c.Parametros[0].Valor is long id && id == codigoCriado);
        Assert.Contains(ex.Chamadas, c => c.Funcao == "fn_pa_045_palete_claim_envio" && c.Parametros[0].Valor is long id && id == codigoCriado);
        Assert.True(UsaUuid(ex, "fn_pa_045_palete_registrar_sucesso", token));
    }

    [Fact]
    public async Task IniciarFluxo_ChamaFuncaoBool_ComOrigemNula()
    {
        FakeExecutor045 ex = new();
        ex.Respostas["fn_pa_045_iniciar_fluxo"] = LinhaBool("fn_pa_045_iniciar_fluxo", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);
        Assert.True(await store.IniciarFluxoAsync(-1, 9, "T1"));
        Assert.Contains(ex.Chamadas, c => c.Funcao == "fn_pa_045_iniciar_fluxo");
        // Â§2: guard_hu_pos_101 Ã© trigger de banco â€” NUNCA aparece nas chamadas do store.
        Assert.DoesNotContain(ex.Chamadas, c => c.Funcao == "fn_pa_045_guard_hu_pos_101");
    }

    [Fact]
    public async Task SnapshotViews_LeemViewsRuntime_SemTokens()
    {
        FakeExecutor045 ex = new();
        ProdutoAcabadoPipelinePostgresStore store = new(ex);
        await store.LerEstadoEtapasAsync(-1);
        await store.LerEstadoPaleteAsync(-7);
        Assert.Contains(ex.Views, v => v.View == "vw_pa_045_etapa_estado_runtime" && v.Coluna == "codigo_hu_caixa");
        Assert.Contains(ex.Views, v => v.View == "vw_pa_045_palete_estado_runtime" && v.Coluna == "codigo_hu_palete");
    }

    [Fact]
    public async Task TodasAsChamadas_SaoFuncoes045_NuncaTabela()
    {
        Guid token = Guid.NewGuid();
        FakeExecutor045 ex = new();
        ex.Respostas["fn_pa_045_claim_etapa"] = [Linha(("claim_token", token), ("numero_tentativa", 1))];
        ex.Respostas["fn_pa_045_registrar_sucesso"] = LinhaBool("fn_pa_045_registrar_sucesso", true);
        ProdutoAcabadoPipelinePostgresStore store = new(ex);
        await store.AdquirirClaimEtapaAsync(-1, "261", 9, "T1");
        await store.RegistrarSucessoEtapaAsync(-1, "261", 201, "D", "2026", "{}", "/x", 9, "T1");
        Assert.All(ex.Chamadas, c => Assert.StartsWith("fn_pa_045_", c.Funcao));
    }

    // ---------------- garantias estÃ¡ticas (Â§3/Â§4/Â§9) ----------------
    [Fact]
    public void Store_NaoContemSelectDML_NemSelectDeToken()
    {
        string src = LerProjeto("Servicos", "Operacao", "ProdutoAcabadoPipelinePostgresStore.cs");
        Assert.DoesNotContain("SELECT", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE ", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nextval", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SET ROLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO hu_palete", src, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Executor_SoInvocaFuncao_NuncaTabela()
    {
        string src = LerProjeto("AcessoDados", "Repositorio", "ProdutoAcabadoPipeline045NpgsqlExecutor.cs");
        // Ãšnico SELECT Ã© a INVOCAÃ‡ÃƒO de funÃ§Ã£o: "SELECT * FROM {funcao}(...)". Nenhum SELECT de coluna/tabela.
        Assert.Contains("SELECT * FROM {funcao}(", src, StringComparison.Ordinal);
        Assert.DoesNotContain("claim_token", src, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controller_UsaStorePostgres_NaoMemoryStoreProdutivo()
    {
        string src = LerProjeto("Controle", "Processo", "ProdutoAcabadoController.cs");
        Assert.Contains("new ProdutoAcabadoPipelinePostgresStore(", src, StringComparison.Ordinal);
        Assert.Contains("FabricaProdutoAcabadoPipeline045Executor.Criar()", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ProdutoAcabadoPipelineStoreMemoria", src, StringComparison.Ordinal);
    }

    private static ProdutoAcabadoHuService HuServiceReal()
        => new(FabricaProdutoAcabadoRepositorio.Criar(), FabricaProdutoAcabadoHandlingUnitSapServico.Criar());

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}




