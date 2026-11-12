using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;
using Xunit;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 101E — testes comportamentais da AUTORIDADE da capability runtime SAP 261 (bound a codigo_lancamento) e da
/// cerimônia de habilitação 261. Cobrem: aquisição bound (A/B), concorrência one-shot (C), TTL (D), auditoria
/// fail-closed (E), reconciliação/one-shot, e a semântica usada pelo writer (H/I/J: capability ausente/errada ⇒ não
/// autoriza; correta ⇒ um writer). Reset por logout/session change (F/G) via singleton em coleção serializada.
/// </summary>
public sealed class RuntimeSapWriteCapability261101ETests
{
    private const long Pk8 = 8;
    private const long Pk9 = 9;

    private static RuntimeSapWriteCapability261Service Nova(TimeSpan? ttl = null, Func<DateTimeOffset>? relogio = null)
        => new(ttl ?? TimeSpan.FromSeconds(120), relogio ?? (() => DateTimeOffset.UtcNow));

    private static async Task<RuntimeSapWriteCapability261Service> ArmadaAsync(long pk, TimeSpan? ttl = null, Func<DateTimeOffset>? relogio = null)
    {
        RuntimeSapWriteCapability261Service cap = Nova(ttl, relogio);
        ResultadoOperacao r = await cap.SolicitarHabilitacao261Async(pk, autorizado: true, _ => Task.CompletedTask);
        Assert.True(r.Sucesso);
        return cap;
    }

    // A —
    [Fact]
    public async Task A_ArmadaPk8_TryAdquirir8_True()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        Assert.True(cap.EstaArmado261(Pk8));
        Assert.True(cap.TryAdquirir261(Pk8));
        Assert.Equal(EstadoCapabilitySap261.Consumida261, cap.ObterEstado().Estado);
    }

    // B —
    [Fact]
    public async Task B_ArmadaPk8_TryAdquirir9_False()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        Assert.False(cap.EstaArmado261(Pk9));
        Assert.False(cap.TryAdquirir261(Pk9));
        Assert.Equal(EstadoCapabilitySap261.Armada261, cap.ObterEstado().Estado); // não consumiu
    }

    // J — capability de PK diferente ⇒ zero autorização (mesma prova de B, ênfase writer)
    [Fact]
    public async Task J_CapabilityDePkDiferente_NaoAutoriza()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        Assert.False(cap.TryAdquirir261(999));
    }

    // C — dois callers concorrentes para PK8 → exatamente 1 vence
    [Fact]
    public async Task C_ConcorrenciaMesmaPk_ExatamenteUmVence()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        using Barrier barreira = new(2);
        int[] vitorias = new int[2];

        async Task Rodar(int i)
        {
            barreira.SignalAndWait();
            if (cap.TryAdquirir261(Pk8)) vitorias[i] = 1;
        }

        await Task.WhenAll(Task.Run(() => Rodar(0)), Task.Run(() => Rodar(1)));
        Assert.Equal(1, vitorias.Sum());
        Assert.Equal(EstadoCapabilitySap261.Consumida261, cap.ObterEstado().Estado);
    }

    // D — TTL expirado ⇒ zero aquisição
    [Fact]
    public async Task D_TtlExpirado_ZeroAquisicao()
    {
        DateTimeOffset agora = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset relogioAtual = agora;
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8, TimeSpan.FromSeconds(120), () => relogioAtual);

        relogioAtual = agora.AddSeconds(121); // após TTL
        Assert.False(cap.EstaArmado261(Pk8));
        Assert.False(cap.TryAdquirir261(Pk8));
        Assert.Equal(EstadoCapabilitySap261.Desabilitada, cap.ObterEstado().Estado);
    }

    // I — env=false + capability correta ⇒ exatamente um writer permitido (uma aquisição)
    [Fact]
    public async Task I_CapabilityCorreta_UmaUnicaAquisicao()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        Assert.True(cap.TryAdquirir261(Pk8));   // 1º writer permitido
        Assert.False(cap.TryAdquirir261(Pk8));  // sem rearm/refund: 2º negado
    }

    // H — capability ausente (DESABILITADA) ⇒ nenhuma autorização (writer não faz HTTP)
    [Fact]
    public void H_CapabilityAusente_NaoAutoriza()
    {
        RuntimeSapWriteCapability261Service cap = Nova();
        Assert.False(cap.EstaArmado261(Pk8));
        Assert.False(cap.TryAdquirir261(Pk8));
    }

    // Reconciliação: CONSUMIDA(pk) → RECONCILIACAO(pk); sem rearm; bloqueia habilitação subsequente
    [Fact]
    public async Task Reconciliacao_ConsumidaVaiParaReconciliacao_SemRearm()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        Assert.True(cap.TryAdquirir261(Pk8));
        cap.MarcarReconciliacao(Pk8);
        Assert.Equal(EstadoCapabilitySap261.ReconciliacaoRequerida, cap.ObterEstado().Estado);

        // Nova habilitação enquanto RECONCILIACAO ⇒ recusada (fail-closed), sem rearm silencioso.
        ResultadoOperacao r = await cap.SolicitarHabilitacao261Async(Pk8, autorizado: true, _ => Task.CompletedTask);
        Assert.False(r.Sucesso);
        Assert.Equal(EstadoCapabilitySap261.ReconciliacaoRequerida, cap.ObterEstado().Estado);
    }

    // Habilitação — não autorizado ⇒ não arma
    [Fact]
    public async Task Habilitacao_NaoAutorizado_NaoArma()
    {
        RuntimeSapWriteCapability261Service cap = Nova();
        ResultadoOperacao r = await cap.SolicitarHabilitacao261Async(Pk8, autorizado: false, _ => Task.CompletedTask);
        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado261(Pk8));
        Assert.Equal(EstadoCapabilitySap261.Desabilitada, cap.ObterEstado().Estado);
    }

    // E — auditoria durável FALHA ⇒ zero armamento (fail-closed)
    [Fact]
    public async Task E_AuditoriaFalha_ZeroArmamento()
    {
        RuntimeSapWriteCapability261Service cap = Nova();
        ResultadoOperacao r = await cap.SolicitarHabilitacao261Async(
            Pk8, autorizado: true, _ => throw new InvalidOperationException("auditoria indisponível"));
        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado261(Pk8));
        Assert.Equal(EstadoCapabilitySap261.Desabilitada, cap.ObterEstado().Estado);
    }

    // Habilitação bound ao lançamento errado não é reaproveitada
    [Fact]
    public async Task Habilitacao_ArmadaParaOutroLancamento_NaoAutorizaEsse()
    {
        RuntimeSapWriteCapability261Service cap = await ArmadaAsync(Pk8);
        Assert.False(cap.TryAdquirir261(Pk9));
        Assert.True(cap.EstaArmado261(Pk8)); // permanece armada só para 8
    }

    // ============ Cerimônia HabilitacaoEscritaSap261Servico (policy/audit injetáveis, DB-free) ============

    [Fact]
    public async Task Cerimonia_AutorizadoEAuditoriaOk_ArmaParaPk()
    {
        RuntimeSapWriteCapability261Service cap = Nova();
        HabilitacaoEscritaSap261Servico servico = ComSessao(cap, autorizado: true, auditaThrow: false);

        ResultadoOperacao r = await servico.HabilitarAsync(Pk8);

        Assert.True(r.Sucesso);
        Assert.True(cap.EstaArmado261(Pk8));
    }

    [Fact]
    public async Task Cerimonia_SemPermissao_NaoArma()
    {
        RuntimeSapWriteCapability261Service cap = Nova();
        HabilitacaoEscritaSap261Servico servico = ComSessao(cap, autorizado: false, auditaThrow: false);

        ResultadoOperacao r = await servico.HabilitarAsync(Pk8);

        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado261(Pk8));
    }

    [Fact]
    public async Task Cerimonia_AuditoriaDuravelFalha_NaoArma()
    {
        RuntimeSapWriteCapability261Service cap = Nova();
        HabilitacaoEscritaSap261Servico servico = ComSessao(cap, autorizado: true, auditaThrow: true);

        ResultadoOperacao r = await servico.HabilitarAsync(Pk8);

        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado261(Pk8));
    }

    [Fact]
    public async Task Cerimonia_SessaoAusente_NaoArma()
    {
        EstadoSessaoUsuarioAtual.Limpar();
        RuntimeSapWriteCapability261Service cap = Nova();
        HabilitacaoEscritaSap261Servico servico = new(cap, () => true, (_, _, _, _) => Task.CompletedTask, (_, _, _, _) => Task.CompletedTask);

        ResultadoOperacao r = await servico.HabilitarAsync(Pk8);

        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado261(Pk8));
    }

    private static HabilitacaoEscritaSap261Servico ComSessao(
        IRuntimeSapWriteCapability261Service cap, bool autorizado, bool auditaThrow)
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1, Login = "it101e", Nome = "IT 101E", IntegracaoBancoHabilitada = false
        });
        Func<string, string, string, CancellationToken, Task> auditarDuravel = auditaThrow
            ? ((_, _, _, _) => throw new InvalidOperationException("auditoria durável indisponível"))
            : ((_, _, _, _) => Task.CompletedTask);
        return new HabilitacaoEscritaSap261Servico(cap, () => autorizado, auditarDuravel, (_, _, _, _) => Task.CompletedTask);
    }
}

/// <summary>
/// F/G — reset da capability 261 no singleton por logout/session change. Coleção serializada (sem paralelismo) para
/// não interferir com outros testes que compartilham o singleton/sessão.
/// </summary>
[Collection("CapabilitySingleton261")]
public sealed class RuntimeSapWriteCapability261ResetTests : IDisposable
{
    public RuntimeSapWriteCapability261ResetTests() => RuntimeSapWriteCapability261.Instancia.Desabilitar();
    public void Dispose()
    {
        RuntimeSapWriteCapability261.Instancia.Desabilitar();
        EstadoSessaoUsuarioAtual.Limpar();
    }

    // F — logout limpa capability
    [Fact]
    public async Task F_Logout_LimpaCapability()
    {
        await RuntimeSapWriteCapability261.Instancia.SolicitarHabilitacao261Async(8, true, _ => Task.CompletedTask);
        Assert.True(RuntimeSapWriteCapability261.Instancia.EstaArmado261(8));

        EstadoSessaoUsuarioAtual.Limpar();

        Assert.False(RuntimeSapWriteCapability261.Instancia.EstaArmado261(8));
        Assert.Equal(EstadoCapabilitySap261.Desabilitada, RuntimeSapWriteCapability261.Instancia.ObterEstado().Estado);
    }

    // G — session change (login) limpa capability
    [Fact]
    public async Task G_SessionChange_LimpaCapability()
    {
        await RuntimeSapWriteCapability261.Instancia.SolicitarHabilitacao261Async(8, true, _ => Task.CompletedTask);
        Assert.True(RuntimeSapWriteCapability261.Instancia.EstaArmado261(8));

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao { IdUsuario = 2, Login = "outro", Nome = "Outro" });

        Assert.False(RuntimeSapWriteCapability261.Instancia.EstaArmado261(8));
    }
}

[CollectionDefinition("CapabilitySingleton261", DisableParallelization = true)]
public sealed class CapabilitySingleton261Collection { }
