using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 12E-E-B — capability runtime de escrita SAP 101 (in-memory, one-shot, TTL, sem auto-rearm).
/// Invariante de bootstrap (ValidadorAmbienteQ) preservado; escopo ÚNICO Material Document 101;
/// isolamento de 261/HU/PALLET. Provas por unidade + source-scan (sem DB/SAP/rede).
/// </summary>
public sealed class RuntimeSapWriteCapability12EEBTests : IDisposable
{
    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    // ---------- helpers ----------
    private static DateTimeOffset _agora = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static RuntimeSapWriteCapabilityService NovaCapability(TimeSpan? ttl = null)
        => new(ttl ?? TimeSpan.FromSeconds(120), () => _agora);

    private static ConfiguracaoSap ConfigQValida(bool escrita) => new()
    {
        BaseUrl = "https://vhfufqs4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/",
        HostsPermitidos = ["vhfufqs4ci.sap.fugacouros.com.br"],
        SapClient = "110",
        Usuario = "u",
        Senha = "p",
        EscritaHabilitada = escrita
    };

    private static string LerFonte(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 9 && raiz is not null; i++)
        {
            foreach (string candidato in new[]
            {
                Path.Combine(raiz, Path.Combine(partes)),
                Path.Combine(raiz, "FugaPet_HML", Path.Combine(partes))
            })
            {
                if (File.Exists(candidato))
                {
                    return File.ReadAllText(candidato);
                }
            }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException($"Fonte não encontrada: {string.Join('/', partes)}");
    }

    private static void DefinirSessao(bool autorizadoHabilitar)
    {
        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 1,
            Login = "admin",
            Nome = "Administrador",
            Permissoes = autorizadoHabilitar
                ? [new PermissaoSessaoAplicacao
                    {
                        Modulo = PermissoesSistema.Modulos.ProcessoProducao,
                        Rotina = PermissoesSistema.Rotinas.EntradaProduto,
                        Acao = PermissoesSistema.Acoes.HabilitarEscritaSap
                    }]
                : [],
            IntegracaoBancoHabilitada = true
        });
    }

    // Config que passa Configurado + MaterialDocumentConfigurado, com env write=FALSE (Q runtime).
    private static ConfiguracaoSap ConfigWriter() => new()
    {
        BaseUrl = "https://vhfufqs4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/",
        MaterialDocumentBaseUrl = "https://vhfufqs4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
        HostsPermitidos = ["vhfufqs4ci.sap.fugacouros.com.br"],
        SapClient = "110",
        Usuario = "u",
        Senha = "p",
        EscritaHabilitada = false
    };

    // Writer REAL com seam de dispatch que apenas CONTA os dispatches HTTP (sem rede, cliente real nunca tocado).
    private static MaterialDocumentSapServico WriterReal(IRuntimeSapWriteCapabilityService cap, Action aoDespachar)
        => new(
            ConfigWriter(),
            logIntegracaoSapServico: null,
            cliente: null,
            capability: cap,
            dispatchHttp: (_, _) =>
            {
                aoDespachar();
                return Task.FromResult(ResultadoMaterialDocumentSap.Falha(200, "stub-dispatch"));
            });

    private static Task<ResultadoMaterialDocumentSap> InvocarWriter(MaterialDocumentSapServico writer)
        => writer.CriarDocumentoMaterial101Async(new MaterialDocumentSapRequest(), "CHAVE/1");

    private static HabilitacaoEscritaSapServico Cerimonia(
        IRuntimeSapWriteCapabilityService capability,
        bool autorizado,
        bool auditoriaFalha = false)
        => new(
            capability,
            usuarioAutorizado: () => autorizado,
            auditarDuravelAsync: (_, _, _, _) => auditoriaFalha
                ? throw new InvalidOperationException("42501 auditoria")
                : Task.CompletedTask,
            auditarBestEffortAsync: (_, _, _, _) => Task.CompletedTask);

    // ---------- T01 / T02 — invariante de bootstrap (ValidadorAmbienteQ) ----------

    [Fact] // T01: Q startup com env write=false → PASS.
    public void T01_Startup_EnvWriteFalse_Pass()
    {
        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarSap(ConfigQValida(escrita: false));
        Assert.True(r.Valido, r.Mensagem);
    }

    [Fact] // T02: Q startup com env write=true → FAIL (gate de bootstrap intacto).
    public void T02_Startup_EnvWriteTrue_Fail()
    {
        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarSap(ConfigQValida(escrita: true));
        Assert.False(r.Valido);
        Assert.Contains("write gates devem iniciar false", r.Mensagem, StringComparison.Ordinal);
    }

    // ---------- T03..T10 — máquina de estado / one-shot / TTL / reset ----------

    [Fact] // T03: estado inicial DESABILITADA.
    public void T03_InitialDisabled()
    {
        var cap = NovaCapability();
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
        Assert.False(cap.EstaArmado101);
        Assert.False(cap.EstaArmado101);
    }

    [Fact] // T04: não autorizado não arma.
    public async Task T04_Unauthorized_DoesNotArm()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: false);
        ResultadoOperacao r = await Cerimonia(cap, autorizado: false).HabilitarAsync();
        Assert.False(r.Sucesso);
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
    }

    [Fact] // T05: falha de auditoria (fail-closed) não arma.
    public async Task T05_AuditFailure_DoesNotArm()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        ResultadoOperacao r = await Cerimonia(cap, autorizado: true, auditoriaFalha: true).HabilitarAsync();
        Assert.False(r.Sucesso);
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
    }

    [Fact] // T06: autorizado + auditoria ok arma ARMADA_101.
    public async Task T06_Authorized_Arms()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        ResultadoOperacao r = await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(r.Sucesso, r.Mensagem);
        Assert.Equal(EstadoCapabilitySap.Armada101, cap.ObterEstado().Estado);
        Assert.True(cap.EstaArmado101);
    }

    [Fact] // T07: TTL expira → volta a DESABILITADA (sem consumo).
    public async Task T07_TtlExpires()
    {
        var cap = NovaCapability(TimeSpan.FromSeconds(120));
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(cap.EstaArmado101);

        _agora = _agora.AddSeconds(121);
        try
        {
            Assert.False(cap.EstaArmado101);
            Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado);
            Assert.False(cap.TryAdquirir101());
        }
        finally
        {
            _agora = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }
    }

    [Fact] // T08: nova instância (restart) inicia DESABILITADA.
    public void T08_RestartStartsDisabled()
    {
        Assert.Equal(EstadoCapabilitySap.Desabilitada, new RuntimeSapWriteCapabilityService().ObterEstado().Estado);
    }

    [Fact] // T09: TryAdquirir one-shot (ARMADA→CONSUMIDA; segundo falha).
    public async Task T09_TryAcquireOneShot()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();

        Assert.True(cap.TryAdquirir101());
        Assert.Equal(EstadoCapabilitySap.Consumida, cap.ObterEstado().Estado);
        Assert.False(cap.TryAdquirir101());
    }

    [Fact] // T10: dupla concorrência → exatamente um vencedor.
    public async Task T10_Concurrency_SingleWinner()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();

        int vitorias = 0;
        var barreira = new Barrier(8);
        var tarefas = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            barreira.SignalAndWait();
            if (cap.TryAdquirir101())
            {
                Interlocked.Increment(ref vitorias);
            }
        }));
        await Task.WhenAll(tarefas);

        Assert.Equal(1, vitorias);
    }

    // ---------- T11..T13 — writer 101 (ENFORCEMENT POINT) ----------

    [Fact] // T11: 101 sem capability (env=false, DESABILITADA) → writer bloqueia, ZERO dispatch.
    public async Task T11_Writer101_NoCapability_Blocked()
    {
        var cap = NovaCapability(); // DESABILITADA
        int dispatches = 0;
        var writer = WriterReal(cap, () => Interlocked.Increment(ref dispatches));

        ResultadoMaterialDocumentSap r = await InvocarWriter(writer);

        Assert.False(r.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemEscritaBloqueada, r.MensagemSanitizada);
        Assert.Equal(0, dispatches);
    }

    [Fact] // T12: 101 ARMADO (env=false) → primeira invocação do writer alcança EXATAMENTE 1 dispatch.
    public async Task T12_Writer101_Armed_ReachesExactlyOneDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();

        int dispatches = 0;
        var writer = WriterReal(cap, () => Interlocked.Increment(ref dispatches));
        await InvocarWriter(writer);

        Assert.Equal(1, dispatches);
        Assert.Equal(EstadoCapabilitySap.Consumida, cap.ObterEstado().Estado);

        // Prova de wiring: o writer consome ATOMICAMENTE (TryAdquirir101) neste gate exato.
        string src = LerFonte("Servicos", "IntegracaoSap", "MaterialDocumentSapServico.cs");
        Assert.Contains("!_configuracaoSap.EscritaHabilitada && !_capability.TryAdquirir101()", src, StringComparison.Ordinal);
    }

    [Fact] // T13 (§9 CRÍTICO): SEGUNDA invocação direta do writer com CONSUMIDA → BLOCKED, TOTAL_DISPATCH=1.
    public async Task T13_SecondWriterInvocation_Blocked_TotalDispatchOne()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();

        int totalDispatches = 0;
        var writer = WriterReal(cap, () => Interlocked.Increment(ref totalDispatches));

        ResultadoMaterialDocumentSap primeira = await InvocarWriter(writer);
        ResultadoMaterialDocumentSap segunda = await InvocarWriter(writer);

        Assert.Equal(1, totalDispatches);                         // TOTAL_DISPATCH_COUNT = 1
        Assert.False(segunda.Sucesso);                            // SECOND_WRITER_INVOCATION = BLOCKED
        Assert.Equal(ConfiguracaoSap.MensagemEscritaBloqueada, segunda.MensagemSanitizada);
        Assert.Equal(EstadoCapabilitySap.Consumida, cap.ObterEstado().Estado);
        _ = primeira;
    }

    [Fact] // T25 (§10): concorrência no BOUNDARY do writer — 1 dispatch entre N callers.
    public async Task T25_Writer_Concurrency_SingleDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();

        int dispatches = 0;
        var writer = WriterReal(cap, () => Interlocked.Increment(ref dispatches));

        var barreira = new Barrier(8);
        var tarefas = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            barreira.SignalAndWait();
            await InvocarWriter(writer);
        }));
        await Task.WhenAll(tarefas);

        Assert.Equal(1, dispatches); // TOTAL_HTTP_DISPATCHES = 1
    }

    [Fact] // T26 (§11): env write=TRUE (legado) → writer despacha SEM capability (compatibilidade preservada).
    public async Task T26_Writer_EnvTrue_LegacyBypass()
    {
        var cap = NovaCapability(); // DESABILITADA
        int dispatches = 0;
        ConfiguracaoSap cfg = ConfigWriter();
        cfg = new ConfiguracaoSap
        {
            BaseUrl = cfg.BaseUrl,
            MaterialDocumentBaseUrl = cfg.MaterialDocumentBaseUrl,
            HostsPermitidos = cfg.HostsPermitidos,
            SapClient = cfg.SapClient,
            Usuario = cfg.Usuario,
            Senha = cfg.Senha,
            EscritaHabilitada = true
        };
        var writer = new MaterialDocumentSapServico(
            cfg, logIntegracaoSapServico: null, cliente: null, capability: cap,
            dispatchHttp: (_, _) => { Interlocked.Increment(ref dispatches); return Task.FromResult(ResultadoMaterialDocumentSap.Falha(200, "stub")); });

        await writer.CriarDocumentoMaterial101Async(new MaterialDocumentSapRequest(), "CHAVE/1");

        Assert.Equal(1, dispatches);
        Assert.Equal(EstadoCapabilitySap.Desabilitada, cap.ObterEstado().Estado); // capability intacta (não consumida)
    }

    // ---------- T14..T16 — isolamento de 261 / HU / PALLET ----------

    [Fact] // T14: 261 permanece gated por EscritaHabilitada (não consulta a capability 101).
    public void T14_Consumo261_Isolated()
    {
        string src = LerFonte("Servicos", "IntegracaoSap", "ConsumoMaterialSap261Servico.cs");
        Assert.Contains("_configuracaoSap.EscritaHabilitada", src, StringComparison.Ordinal);
        Assert.DoesNotContain("EscritaRuntimeAutorizada101", src, StringComparison.Ordinal);
        // GATE 101E-P2: o writer 261 usa a capability 261 (isolada da 101). Continua SEM acoplamento à 101.
        Assert.DoesNotContain("TryAdquirir101", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeSapWriteCapability.Instancia", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Armada101", src, StringComparison.Ordinal);
        Assert.Contains("RuntimeSapWriteCapability261", src, StringComparison.Ordinal);
    }

    [Fact] // T15: HU permanece gated por EscritaHabilitada (isolado da capability 101).
    public void T15_HandlingUnit_Isolated()
    {
        string src = LerFonte("Servicos", "IntegracaoSap", "FabricaProdutoAcabadoHandlingUnitSapServico.cs");
        Assert.DoesNotContain("EscritaRuntimeAutorizada101", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeSapWriteCapability", src, StringComparison.Ordinal);
    }

    [Fact] // T16: PALLET permanece gated por PalletWriteHabilitado (isolado da capability 101).
    public void T16_Pallet_Isolated()
    {
        string src = LerFonte("Servicos", "IntegracaoSap", "FabricaProdutoAcabadoPaleteInt012Gateway.cs");
        Assert.Contains("PalletWriteHabilitado", src, StringComparison.Ordinal);
        Assert.DoesNotContain("EscritaRuntimeAutorizada101", src, StringComparison.Ordinal);
    }

    // ---------- T17..T20 — indeterminado / reconciliação / sem rearm ----------

    [Fact] // T17: timeout → RECONCILIACAO_REQUERIDA (consumida permanece; sem refund).
    public async Task T17_Timeout_ReconciliationRequired()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(cap.TryAdquirir101());

        cap.MarcarReconciliacao(); // ramo de exceção/timeout do controller
        Assert.Equal(EstadoCapabilitySap.ReconciliacaoRequerida, cap.ObterEstado().Estado);
        Assert.False(cap.EstaArmado101);
        Assert.False(cap.TryAdquirir101());

        // wiring: o controller marca reconciliação nos ramos indeterminados.
        string ctrl = LerFonte("Controle", "Processo", "EntradaProdutoController.cs");
        Assert.Contains("_capability.MarcarReconciliacao();", ctrl, StringComparison.Ordinal);
    }

    [Fact] // T18: resultado indeterminado (2xx sem documento) → RECONCILIACAO_REQUERIDA.
    public async Task T18_Indeterminate_ReconciliationRequired()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(cap.TryAdquirir101());
        cap.MarcarReconciliacao();
        Assert.Equal(EstadoCapabilitySap.ReconciliacaoRequerida, cap.ObterEstado().Estado);
    }

    [Fact] // T19: sem auto-retry — após consumo não há nova aquisição sem nova habilitação humana.
    public async Task T19_NoAutoRetry()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(cap.TryAdquirir101());
        Assert.False(cap.TryAdquirir101());
        Assert.False(cap.TryAdquirir101());
    }

    [Fact] // T20: falha conclusiva não re-arma automaticamente (permanece CONSUMIDA).
    public async Task T20_ConclusiveFailure_NoAutoRearm()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(cap.TryAdquirir101());
        // "falha conclusiva": nada rearma. Nova habilitação só após Desabilitar (ação humana).
        Assert.Equal(EstadoCapabilitySap.Consumida, cap.ObterEstado().Estado);
        Assert.False(cap.EstaArmado101);
        ResultadoOperacao rearmSilencioso = await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.False(rearmSilencioso.Sucesso); // não arma sobre CONSUMIDA
    }

    // ---------- T21..T24 — diagnóstico C7 / C8 / permissões ----------

    [Fact] // T21 (12G-D): a ELEGIBILIDADE do botão NÃO depende da capability/env-write (arming é no clique).
    public void T21_ButtonEligibility_NotGatedByCapability()
    {
        string ctrl = LerFonte("Controle", "Processo", "EntradaProdutoController.cs");
        // PodeEnviar não gateia mais em escritaSapHabilitada101 (precondição e cascata):
        Assert.DoesNotContain("&& escritaSapHabilitada101", ctrl, StringComparison.Ordinal);
        Assert.DoesNotContain("Escrita SAP desabilitada. Habilite", ctrl, StringComparison.Ordinal);
        // O campo do chip continua computado (env || armada), mas só para status visual.
        Assert.Contains("integracao.EscritaSapHabilitada || _capability.EstaArmado101", ctrl, StringComparison.Ordinal);
    }

    [Fact] // T22: C8 (endpoint Material Document) continua obrigatório no diagnóstico.
    public void T22_C8_EndpointStillRequired()
    {
        string ctrl = LerFonte("Controle", "Processo", "EntradaProdutoController.cs");
        Assert.Contains("integracao.MaterialDocumentConfigurado", ctrl, StringComparison.Ordinal);
    }

    [Fact] // T23: ENVIAR_SAP continua obrigatório (governança revalida na Escrita).
    public void T23_EnviarSapStillRequired()
    {
        string estado = LerFonte("Servicos", "IntegracaoSap", "EstadoIntegracaoSapServico.cs");
        Assert.Contains("OperacaoIntegracaoSap.Escrita", estado, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.EnviarSap", estado, StringComparison.Ordinal);
    }

    [Fact] // T24: HABILITAR_ESCRITA_SAP é obrigatória para armar (permissão dedicada, não ENVIAR_SAP).
    public void T24_HabilitarEscritaSap_Required()
    {
        Assert.Equal("HABILITAR_ESCRITA_SAP", PermissoesSistema.Acoes.HabilitarEscritaSap);
        string cerimonia = LerFonte("Servicos", "IntegracaoSap", "HabilitacaoEscritaSapServico.cs");
        Assert.Contains("PermissoesSistema.Acoes.HabilitarEscritaSap", cerimonia, StringComparison.Ordinal);
        Assert.DoesNotContain("Acoes.EnviarSap", cerimonia, StringComparison.Ordinal);
    }

    // ---------- T27 / T28 — C7 pós-consumo / cross-user ----------

    [Fact] // T27 (§12): após o consumo (CONSUMIDA), C7/UI NÃO indica prontidão (EstaArmado101=false).
    public async Task T27_C7_AfterConsumption_NotReady()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarAsync();
        Assert.True(cap.EstaArmado101); // C7 pronto antes do envio

        var writer = WriterReal(cap, () => { });
        await InvocarWriter(writer); // consome no dispatch

        Assert.False(cap.EstaArmado101); // C7_RUNTIME_CAPABILITY_READY = FALSE
        Assert.Equal(EstadoCapabilitySap.Consumida, cap.ObterEstado().Estado);
    }

    [Fact] // T28 (§14): logout/login desarmam a capability AMBIENTE (cross-user fail-closed).
    public async Task T28_CrossUser_SessionResetsCapability()
    {
        // Wiring: Limpar (logout) E Definir (login) resetam a capability ambiente.
        string sessao = LerFonte("Servicos", "Seguranca", "EstadoSessaoUsuarioAtual.cs");
        Assert.Contains("RuntimeSapWriteCapability.Instancia.Desabilitar();", sessao, StringComparison.Ordinal);
        int ocorrencias = sessao.Split("RuntimeSapWriteCapability.Instancia.Desabilitar();").Length - 1;
        Assert.True(ocorrencias >= 2, "reset deve existir em Limpar E Definir");

        // Comportamental: arma a capability ambiente; logout a desarma (qualquer troca de sessão desarma).
        await RuntimeSapWriteCapability.Instancia.SolicitarHabilitacao101Async(
            autorizado: true, _ => Task.CompletedTask);
        EstadoSessaoUsuarioAtual.Limpar();
        Assert.False(RuntimeSapWriteCapability.Instancia.EstaArmado101);
    }

    // ---------- 12G-B — botão ÚNICO: habilitação integrada ao envio ----------

    [Fact] // G1: usuário SEM HABILITAR_ESCRITA_SAP → não arma / ZERO POST.
    public async Task G1_SemPermissaoHabilitar_NaoArma_ZeroPost()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: false);

        ResultadoOperacao r = await Cerimonia(cap, autorizado: false).HabilitarParaEnvioAsync();
        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado101);

        int dispatches = 0;
        await InvocarWriter(WriterReal(cap, () => Interlocked.Increment(ref dispatches)));
        Assert.Equal(0, dispatches);
    }

    [Fact] // G2: operador CANCELA a confirmação (HabilitarParaEnvio não é chamado) → ZERO POST.
    public async Task G2_CancelaConfirmacao_ZeroPost()
    {
        var cap = NovaCapability(); // permanece DESABILITADA (cancelou antes de habilitar)
        int dispatches = 0;
        await InvocarWriter(WriterReal(cap, () => Interlocked.Increment(ref dispatches)));
        Assert.Equal(0, dispatches);
    }

    [Fact] // G3: autorizado + confirmação + auditoria PASS → arma e o writer é alcançado (1 dispatch).
    public async Task G3_Autorizado_AuditoriaPass_ArmaEDespacha()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);

        ResultadoOperacao r = await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();
        Assert.True(r.Sucesso, r.Mensagem);
        Assert.True(cap.EstaArmado101);

        int dispatches = 0;
        await InvocarWriter(WriterReal(cap, () => Interlocked.Increment(ref dispatches)));
        Assert.Equal(1, dispatches);
    }

    [Fact] // G4: auditoria FAIL (fail-closed) → não arma / ZERO POST.
    public async Task G4_AuditoriaFail_NaoArma_ZeroPost()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);

        ResultadoOperacao r = await Cerimonia(cap, autorizado: true, auditoriaFalha: true).HabilitarParaEnvioAsync();
        Assert.False(r.Sucesso);
        Assert.False(cap.EstaArmado101);

        int dispatches = 0;
        await InvocarWriter(WriterReal(cap, () => Interlocked.Increment(ref dispatches)));
        Assert.Equal(0, dispatches);
    }

    [Fact] // G5: primeiro envio → exatamente 1 dispatch.
    public async Task G5_PrimeiroEnvio_UmDispatch()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();

        int dispatches = 0;
        var writer = WriterReal(cap, () => Interlocked.Increment(ref dispatches));
        await InvocarWriter(writer);
        Assert.Equal(1, dispatches);
    }

    [Fact] // G6: segunda tentativa com a MESMA capability (sem novo envio explícito) → BLOQUEADA, total 1.
    public async Task G6_SegundaTentativaMesmaCapability_Bloqueada()
    {
        var cap = NovaCapability();
        DefinirSessao(autorizadoHabilitar: true);
        await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();

        int total = 0;
        var writer = WriterReal(cap, () => Interlocked.Increment(ref total));
        await InvocarWriter(writer);
        ResultadoMaterialDocumentSap segunda = await InvocarWriter(writer);

        Assert.Equal(1, total);
        Assert.False(segunda.Sucesso);

        // NOVO envio explícito re-arma o one-shot consumido (não é auto-retry: exige nova ação humana).
        ResultadoOperacao rearmeExplicito = await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();
        Assert.True(rearmeExplicito.Sucesso, rearmeExplicito.Mensagem);
        await InvocarWriter(writer);
        Assert.Equal(2, total);

        // RECONCILIACAO permanece bloqueada (acione suporte) — NÃO re-arma.
        cap.Desabilitar();
        await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();
        Assert.True(cap.TryAdquirir101());
        cap.MarcarReconciliacao();
        ResultadoOperacao bloqueado = await Cerimonia(cap, autorizado: true).HabilitarParaEnvioAsync();
        Assert.False(bloqueado.Sucesso);
    }

    [Fact] // G7: 261/HU/Pallet permanecem isolados (não consultam a capability 101).
    public void G7_OutrosWriters_Isolados()
    {
        foreach (string arquivo in new[]
        {
            "ConsumoMaterialSap261Servico.cs",
            "FabricaProdutoAcabadoHandlingUnitSapServico.cs",
            "FabricaProdutoAcabadoPaleteInt012Gateway.cs"
        })
        {
            string src = LerFonte("Servicos", "IntegracaoSap", arquivo);
            // GATE 101E-P2: nenhum destes writers usa a capability 101 (o 261 usa a SUA própria capability 261).
            Assert.DoesNotContain("TryAdquirir101", src, StringComparison.Ordinal);
            Assert.DoesNotContain("RuntimeSapWriteCapability.Instancia", src, StringComparison.Ordinal);
            Assert.DoesNotContain("Armada101", src, StringComparison.Ordinal);
        }
    }
}
