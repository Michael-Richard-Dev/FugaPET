using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 113M — correção dos DOIS blockers estáticos do 113K.
///
/// B1: o bool de FinalizarLocalAsync não prova o estado PERSISTIDO. Agora a caixa é RELIDA e
/// comprovada em FINALIZADA_LOCAL (mesmo código) ANTES do early binding 045.
///
/// B2: PossuiVinculoPipeline045Async retornava false quando as operações 045 eram nulas ou sem
/// persistência definitiva — e false LIBERAVA a HU direta. Agora existe um tri-estado
/// (SemVinculo / ComVinculo / ConsultaIndisponivel) e o indeterminado BLOQUEIA, com mensagem própria.
///
/// Sem banco e sem SAP.
/// </summary>
public sealed class PaPipeline113MBlockersTests
{
    // ================= fake de repositório com ESTADO =================

    private sealed class RepoFake : IProdutoAcabadoRepositorio
    {
        public List<string> Log { get; } = [];
        public bool FinalizarLocalOk { get; set; } = true;

        /// <summary>Estado persistido simulado; FinalizarLocalAsync bem-sucedido o move para FINALIZADA_LOCAL.</summary>
        public StatusIntegracaoCaixa StatusPersistido { get; set; } = StatusIntegracaoCaixa.EmPesagem;
        public bool SnapshotAusente { get; set; }
        public bool LeituraLanca { get; set; }
        public long CodigoPersistido { get; set; } = 77;
        /// <summary>Quando definido, a RELEITURA devolve outro código (simula snapshot de caixa divergente).</summary>
        public long? CodigoNaReleitura { get; set; }
        public int LeiturasPorCodigo { get; private set; }

        private ProdutoAcabadoCaixa Snapshot() => new()
        {
            CodigoProdutoAcabadoCaixa = CodigoPersistido,
            CodigoCaixaLocal = "CX-1000210-0008",
            NumeroOrdemProducao = "1000210",
            Material = "4000174",
            Centro = "3007",
            Deposito = "PP01",
            MaterialEmbalagem = "3000046",
            PesoBrutoKg = 10m,
            PesoLiquidoKg = 9m,
            TaraKg = 1m,
            UnidadePeso = "KG",
            QuantidadeProdutos = 8,
            UnidadeQuantidade = "UN",
            OrigemPesagem = "MANUAL",
            Terminal = "T1",
            CodigoUsuario = 1,
            StatusIntegracao = StatusPersistido
        };

        public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa c, CancellationToken ct = default)
        { Log.Add("registrar_caixa"); StatusPersistido = StatusIntegracaoCaixa.EmPesagem; return Task.FromResult(Snapshot()); }
        public Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa p, CancellationToken ct = default)
        { Log.Add("registrar_pesagem"); return Task.FromResult(1L); }
        public Task<bool> FinalizarLocalAsync(long c, long u, string t, CancellationToken ct = default)
        {
            Log.Add("finalizar_local");
            // Contrato real: finalizar_local bem-sucedido PERSISTE FINALIZADA_LOCAL.
            if (FinalizarLocalOk) { StatusPersistido = StatusIntegracaoCaixa.FinalizadaLocal; }
            return Task.FromResult(FinalizarLocalOk);
        }
        public Task<bool> SalvarPreviewAsync(long c, string req, string end, CancellationToken ct = default)
        { Log.Add("salvar_preview"); StatusPersistido = StatusIntegracaoCaixa.PreviewHuGerado; return Task.FromResult(true); }
        public Task<bool> AguardarAutorizacaoAsync(long c, CancellationToken ct = default)
        { Log.Add("aguardar_autorizacao"); StatusPersistido = StatusIntegracaoCaixa.AguardandoAutorizacaoSap; return Task.FromResult(true); }
        public Task<bool> AutorizarEnvioAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ProdutoAcabadoCaixa?> ClaimEnvioAsync(long c, long u, string t, CancellationToken ct = default)
        { Log.Add("claim_envio"); return Task.FromResult<ProdutoAcabadoCaixa?>(null); }
        public Task<bool> RegistrarSucessoAsync(long c, int tent, Guid tok, string hu, string? wh, int http, string? resp, string? sap, string? etag, string? by, DateTimeOffset? cr, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroAsync(long c, int tent, Guid tok, int? http, string? resp, string? sap, string erro, ResultadoErroHu r, bool repro, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutAsync(long c, int tent, Guid tok, string? sap, string erro, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ConfirmarReconciliacaoAsync(long c, string hu, string? wh, int http, string? resp, string? sap, string? etag, string? by, DateTimeOffset? cr, bool ok, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarReconciliacaoNaoEncontradaAsync(long c, string hu, string? wh, int http, string? resp, string? sap, string erro, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> BloquearConfiguracaoAsync(long c, long u, string t, string erro, string? sap, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CancelarAsync(long c, long u, string t, string m, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoAsync(long c, long u, string t, string m, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long c, CancellationToken ct = default)
        {
            LeiturasPorCodigo++;
            if (LeituraLanca) { throw new InvalidOperationException("falha simulada de leitura"); }
            if (SnapshotAusente) { return Task.FromResult<ProdutoAcabadoCaixa?>(null); }

            ProdutoAcabadoCaixa lido = Snapshot();
            if (CodigoNaReleitura is long divergente)
            {
                lido.CodigoProdutoAcabadoCaixa = divergente;
            }

            return Task.FromResult<ProdutoAcabadoCaixa?>(lido);
        }
        public Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string t, CancellationToken ct = default)
            => Task.FromResult<ProdutoAcabadoCaixa?>(null);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorOrdemTerminalAsync(string o, string t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(string o, string i, string m, string l, string t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]);

        // GATE 124H: acumulado real do 261 SEM terminal. Fake sem caixas persistidas ⇒ lista vazia.
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarConfirmadasPorContextoSemTerminalAsync(string o, string i, string m, string l, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorHandlingUnitsAsync(IReadOnlyList<string> hus, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorIntervaloHandlingUnitAsync(string de, string ate, string? terminal, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProdutoAcabadoCaixa>>([]);
    }

    private sealed class GatewayHuFake : IProdutoAcabadoHandlingUnitSapServico
    {
        public bool EnvioAutorizado => false;
        public Task<ResultadoPostHandlingUnit> CriarHandlingUnitCaixaAsync(HandlingUnitCaixaRequest r, string e, CancellationToken ct = default)
            => Task.FromResult(new ResultadoPostHandlingUnit { Cenario = CenarioPostHandlingUnit.NaoEnviado });
        public Task<ResultadoReconciliacaoHandlingUnit> ReconciliarHandlingUnitAsync(string hu, string wh, CancellationToken ct = default)
            => Task.FromResult(new ResultadoReconciliacaoHandlingUnit { Cenario = CenarioReconciliacaoHandlingUnit.Indeterminado });
    }

    private static ProdutoAcabadoCaixa CaixaParaFinalizar() => new()
    {
        NumeroOrdemProducao = "1000210",
        ItemOrdemProducao = "0001",
        Material = "4000174",
        Lote = "L1",
        Centro = "3007",
        Deposito = "PP01",
        MaterialEmbalagem = "3000046",
        PesoBrutoKg = 10m,
        PesoLiquidoKg = 9m,
        TaraKg = 1m,
        UnidadePeso = "KG",
        QuantidadeProdutos = 8,
        UnidadeQuantidade = "UN",
        OrigemPesagem = "MANUAL",
        Terminal = "T1",
        CodigoUsuario = 1,
        CorrelationId = Guid.NewGuid(),
        StatusIntegracao = StatusIntegracaoCaixa.FinalizadaLocal
    };

    private static (ProdutoAcabadoHuService service, int[] bindings) Servico(RepoFake repo)
    {
        int[] contador = [0];
        ProdutoAcabadoHuService service = new(repo, new GatewayHuFake(), requestBuilder: null,
            estabelecerBindingPipeline045: (_, _, _, _) => { contador[0]++; return Task.FromResult(true); });
        return (service, contador);
    }

    // ==================================================================
    // §2/§3 B1 — PROVA PERSISTIDA DE FINALIZADA_LOCAL
    // ==================================================================

    [Fact] // D) snapshot FINALIZADA_LOCAL ⇒ binding exatamente 1 vez, e só depois o preview.
    public async Task D_SnapshotFinalizadaLocal_BindingUmaVez_DepoisPreview()
    {
        RepoFake repo = new();
        (ProdutoAcabadoHuService service, int[] bindings) = Servico(repo);

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.True(r.Sucesso, r.Mensagem);
        Assert.Equal(1, bindings[0]);
        Assert.Contains("salvar_preview", repo.Log);
        Assert.True(
            repo.Log.IndexOf("finalizar_local") < repo.Log.IndexOf("salvar_preview"),
            string.Join(",", repo.Log));
    }

    [Fact] // A) finalizar_local=true mas o snapshot fresco está em OUTRO status ⇒ bloqueio total.
    public async Task A_StatusPersistidoDivergente_ZeroBindingZeroPreview()
    {
        // FinalizarLocalOk=true, mas a leitura devolve EM_PESAGEM (persistência não acompanhou o bool).
        RepoFake repo = new();
        (ProdutoAcabadoHuService service, int[] bindings) = Servico(repo);
        // Força o status persistido de volta, simulando divergência entre bool e estado real.
        repo.StatusPersistido = StatusIntegracaoCaixa.EmPesagem;
        ProdutoAcabadoHuService servicoDivergente = new(
            new RepoDivergente(repo), new GatewayHuFake(), requestBuilder: null,
            estabelecerBindingPipeline045: (_, _, _, _) => { bindings[0]++; return Task.FromResult(true); });

        ResultadoFinalizacaoHu r = await servicoDivergente.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(r.Sucesso);
        Assert.Equal(ProdutoAcabadoHuService.MensagemFinalizacaoLocalNaoComprovada, r.Mensagem);
        Assert.Equal(0, bindings[0]);
        Assert.DoesNotContain("salvar_preview", repo.Log);
    }

    /// <summary>Repo que confirma finalizar_local mas NUNCA muda o status persistido (divergência real).</summary>
    private sealed class RepoDivergente(RepoFake interno) : IProdutoAcabadoRepositorio
    {
        public List<string> Log => interno.Log;
        public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa c, CancellationToken ct = default) => interno.RegistrarCaixaAsync(c, ct);
        public Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa p, CancellationToken ct = default) => interno.RegistrarPesagemAsync(p, ct);
        public Task<bool> FinalizarLocalAsync(long c, long u, string t, CancellationToken ct = default)
        { Log.Add("finalizar_local"); return Task.FromResult(true); } // true SEM persistir o estado
        public Task<bool> SalvarPreviewAsync(long c, string req, string e, CancellationToken ct = default) => interno.SalvarPreviewAsync(c, req, e, ct);
        public Task<bool> AguardarAutorizacaoAsync(long c, CancellationToken ct = default) => interno.AguardarAutorizacaoAsync(c, ct);
        public Task<bool> AutorizarEnvioAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ProdutoAcabadoCaixa?> ClaimEnvioAsync(long c, long u, string t, CancellationToken ct = default) => interno.ClaimEnvioAsync(c, u, t, ct);
        public Task<bool> RegistrarSucessoAsync(long c, int tent, Guid tok, string hu, string? wh, int http, string? resp, string? sap, string? etag, string? by, DateTimeOffset? cr, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroAsync(long c, int tent, Guid tok, int? http, string? resp, string? sap, string erro, ResultadoErroHu r, bool repro, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutAsync(long c, int tent, Guid tok, string? sap, string erro, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> ConfirmarReconciliacaoAsync(long c, string hu, string? wh, int http, string? resp, string? sap, string? etag, string? by, DateTimeOffset? cr, bool ok, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarReconciliacaoNaoEncontradaAsync(long c, string hu, string? wh, int http, string? resp, string? sap, string erro, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> BloquearConfiguracaoAsync(long c, long u, string t, string erro, string? sap, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CancelarAsync(long c, long u, string t, string m, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoAsync(long c, long u, string t, string m, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long c, CancellationToken ct = default) => interno.ObterPorCodigoAsync(c, ct);
        public Task<ProdutoAcabadoCaixa?> ObterAtivaPorTerminalAsync(string t, CancellationToken ct = default) => interno.ObterAtivaPorTerminalAsync(t, ct);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorOrdemTerminalAsync(string o, string t, CancellationToken ct = default) => interno.ListarPorOrdemTerminalAsync(o, t, ct);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(string o, string i, string m, string l, string t, CancellationToken ct = default) => interno.ListarPorContextoAsync(o, i, m, l, t, ct);

        // GATE 124H: delega ao fake interno, como as demais leituras.
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarConfirmadasPorContextoSemTerminalAsync(string o, string i, string m, string l, CancellationToken ct = default) => interno.ListarConfirmadasPorContextoSemTerminalAsync(o, i, m, l, ct);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorHandlingUnitsAsync(IReadOnlyList<string> hus, CancellationToken ct = default) => interno.ListarPorHandlingUnitsAsync(hus, ct);
        public Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorIntervaloHandlingUnitAsync(string de, string ate, string? terminal, CancellationToken ct = default) => interno.ListarPorIntervaloHandlingUnitAsync(de, ate, terminal, ct);
    }

    [Fact] // B) snapshot não encontrado ⇒ mesmo bloqueio.
    public async Task B_SnapshotAusente_ZeroBinding()
    {
        RepoFake repo = new() { SnapshotAusente = true };
        (ProdutoAcabadoHuService service, int[] bindings) = Servico(repo);

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(r.Sucesso);
        Assert.Equal(ProdutoAcabadoHuService.MensagemFinalizacaoLocalNaoComprovada, r.Mensagem);
        Assert.Equal(0, bindings[0]);
        Assert.DoesNotContain("salvar_preview", repo.Log);
        Assert.DoesNotContain("aguardar_autorizacao", repo.Log);
    }

    [Fact] // C) leitura fresca lança ⇒ mesmo bloqueio (exceção não escapa).
    public async Task C_LeituraFrescaFalha_ZeroBinding()
    {
        RepoFake repo = new() { LeituraLanca = true };
        (ProdutoAcabadoHuService service, int[] bindings) = Servico(repo);

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(r.Sucesso);
        Assert.Equal(ProdutoAcabadoHuService.MensagemFinalizacaoLocalNaoComprovada, r.Mensagem);
        Assert.Equal(0, bindings[0]);
        Assert.DoesNotContain("salvar_preview", repo.Log);
    }

    [Fact] // Código divergente no snapshot ⇒ bloqueio (não é a mesma caixa).
    public async Task CodigoDivergente_ZeroBinding()
    {
        RepoFake repo = new() { CodigoNaReleitura = 999 };
        (ProdutoAcabadoHuService service, int[] bindings) = Servico(repo);

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(r.Sucesso);
        Assert.Equal(0, bindings[0]);
        Assert.DoesNotContain("salvar_preview", repo.Log);
    }

    [Theory] // Pós-condição pura: só FINALIZADA_LOCAL com código igual é comprovação.
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal, 77, true)]
    [InlineData(StatusIntegracaoCaixa.EmPesagem, 77, false)]
    [InlineData(StatusIntegracaoCaixa.PreviewHuGerado, 77, false)]
    [InlineData(StatusIntegracaoCaixa.AguardandoAutorizacaoSap, 77, false)]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap, 77, false)]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal, 78, false)]
    public void PosCondicaoFinalizacaoLocal(StatusIntegracaoCaixa status, long codigo, bool esperado)
        => Assert.Equal(esperado, ProdutoAcabadoHuService.FinalizacaoLocalComprovada(
            new ProdutoAcabadoCaixa { CodigoProdutoAcabadoCaixa = codigo, StatusIntegracao = status }, 77));

    [Fact] // Snapshot nulo nunca comprova.
    public void PosCondicaoSnapshotNulo()
        => Assert.False(ProdutoAcabadoHuService.FinalizacaoLocalComprovada(null, 77));

    [Fact] // §8: HU-only segue sem releitura extra e com ZERO 045.
    public async Task HuOnly_NaoFazReleituraExtraNemBinding()
    {
        RepoFake repo = new();
        ProdutoAcabadoHuService service = new(repo, new GatewayHuFake());

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.True(r.Sucesso, r.Mensagem);
        Assert.False(service.ModoPipeline045);
        Assert.Equal(
            ["registrar_caixa", "registrar_pesagem", "finalizar_local", "salvar_preview", "aguardar_autorizacao"],
            repo.Log);
    }

    // ==================================================================
    // §4/§5/§7 B2 — CONSULTA 045 INDETERMINADA É FAIL-CLOSED
    // ==================================================================

    [Theory]
    [InlineData(EstadoVinculo045.SemVinculo, false)]
    [InlineData(EstadoVinculo045.ComVinculo, true)]
    [InlineData(EstadoVinculo045.ConsultaIndisponivel, true)]
    public void ContratoDeBloqueioHuDireta(EstadoVinculo045 estado, bool bloqueia)
        => Assert.Equal(bloqueia, ProdutoAcabadoController.BloqueiaHuDireta(estado));

    [Fact] // §5: mensagens DISTINTAS — o indeterminado não afirma vínculo.
    public void MensagensDistintasPorEstado()
    {
        string indisponivel = ProcessoProdutoAcabadoForm.MensagemBloqueioVinculo045(
            EstadoVinculo045.ConsultaIndisponivel);
        string comVinculo = ProcessoProdutoAcabadoForm.MensagemBloqueioVinculo045(
            EstadoVinculo045.ComVinculo);

        Assert.NotEqual(indisponivel, comVinculo);
        Assert.Contains("não foi possível comprovar a ausência de vínculo 045", indisponivel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("possui vínculo", indisponivel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("possui vínculo 045", comVinculo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Nenhum POST executado", indisponivel, StringComparison.Ordinal);
    }

    [Fact] // §4/§7: o tri-estado NÃO tem fail-open — não existe caminho que devolva SemVinculo sem consulta.
    public void SemVinculoSomenteComConsultaMaterial()
    {
        string src = FonteController();
        int inicio = src.IndexOf("public async Task<EstadoVinculo045> ConsultarVinculoPipeline045Async", StringComparison.Ordinal);
        Assert.True(inicio > 0);
        int fim = src.IndexOf("internal static bool BloqueiaHuDireta", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio);
        string corpo = src[inicio..fim];

        // OPS nula / sem persistência ⇒ ConsultaIndisponivel (antes era `return false`, que liberava).
        Assert.Contains("SuportaPersistenciaDefinitiva", corpo, StringComparison.Ordinal);
        Assert.Contains("return EstadoVinculo045.ConsultaIndisponivel;", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("return false;", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("return EstadoVinculo045.SemVinculo;", corpo, StringComparison.Ordinal);
        // SemVinculo só aparece no ramo condicionado à contagem material de etapas.
        Assert.Contains("etapas.Count > 0 ? EstadoVinculo045.ComVinculo : EstadoVinculo045.SemVinculo", corpo, StringComparison.Ordinal);
        // Exceção ⇒ indeterminado.
        Assert.Contains("catch (Exception ex)", corpo, StringComparison.Ordinal);
    }

    [Fact] // §6: ZERO etapas materialmente consultadas ⇒ HU-only permitido (113G congelado).
    public void ZeroEtapasConsultadas_HuOnlyPermitido()
        => Assert.False(ProdutoAcabadoController.BloqueiaHuDireta(EstadoVinculo045.SemVinculo));

    [Fact] // §7: o guard HU-only usa o tri-estado e a mensagem correta.
    public void GuardHuOnlyUsaTriEstado()
    {
        string form = FonteForm();
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync", StringComparison.Ordinal);
        int fim = form.IndexOf("private async Task SolicitarEnvioCaixaPipelineAsync", inicio, StringComparison.Ordinal);
        Assert.True(inicio > 0 && fim > inicio);
        string huOnly = form[inicio..fim];

        Assert.Contains("ConsultarVinculoPipeline045Async", huOnly, StringComparison.Ordinal);
        Assert.Contains("ProdutoAcabadoController.BloqueiaHuDireta(vinculo045)", huOnly, StringComparison.Ordinal);
        Assert.Contains("MensagemBloqueioVinculo045(vinculo045)", huOnly, StringComparison.Ordinal);
        // O método antigo (bool) não sobrevive em lugar nenhum.
        Assert.DoesNotContain("PossuiVinculoPipeline045Async", form, StringComparison.Ordinal);
        Assert.DoesNotContain("PossuiVinculoPipeline045Async", FonteController(), StringComparison.Ordinal);
    }

    [Fact] // §7: 0006/0007 com zero 045 consultado seguem HU-only históricos; nada de 045 retroativo.
    public void Caixas0006E0007_ZeroVinculoConsultado_SeguemHuOnly()
    {
        Assert.False(ProdutoAcabadoController.BloqueiaHuDireta(EstadoVinculo045.SemVinculo));
        // E nenhum caminho cria 045 para caixa existente (o único criador é o early binding).
        Assert.DoesNotContain("IniciarFluxoAsync", FonteForm(), StringComparison.Ordinal);
        Assert.DoesNotContain("IniciarFluxoAsync", FonteOrquestrador(), StringComparison.Ordinal);
    }

    // ==================================================================
    // §8 — 113J/113E NÃO REGREDIRAM
    // ==================================================================

    [Fact]
    public void Preservacao113J()
    {
        string orq = FonteOrquestrador();
        Assert.Contains("Vinculo045Valido(", orq, StringComparison.Ordinal);
        Assert.Contains("bool preparada = await _ops.PrepararEtapaAsync(", orq, StringComparison.Ordinal);
        Assert.Contains("if (!preparada)", orq, StringComparison.Ordinal);

        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(
            [new Linha045(new Dictionary<string, object?> { ["etapa"] = "261" }),
             new Linha045(new Dictionary<string, object?> { ["etapa"] = "101" })], out _));
        Assert.False(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido([], out _));
    }

    [Fact]
    public void Preservacao113E()
    {
        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
        { CodigoProdutoAcabadoCaixa = 6, StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap }));
        Assert.True(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
        { CodigoProdutoAcabadoCaixa = 6, StatusIntegracao = StatusIntegracaoCaixa.ProntaParaEnvio }));
        Assert.True(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(
            new ResultadoPipelineProdutoAcabado(true, EtapaPipelineProdutoAcabado.Concluido, "ok", null)));
    }

    // ================= leitura de fonte =================

    private static string Fonte(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 9 && raiz is not null; i++)
        {
            foreach (string cand in new[]
            {
                Path.Combine(raiz, Path.Combine(partes)),
                Path.Combine(raiz, "FugaPet_HML", Path.Combine(partes))
            })
            {
                if (File.Exists(cand)) return File.ReadAllText(cand);
            }
            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException(string.Join('/', partes));
    }

    private static string FonteForm() => Fonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
    private static string FonteController() => Fonte("Controle", "Processo", "ProdutoAcabadoController.cs");
    private static string FonteOrquestrador() => Fonte("Servicos", "Operacao", "ProdutoAcabadoPipeline045Orquestrador.cs");
}
