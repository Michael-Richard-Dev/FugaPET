using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 113J — EARLY BINDING do fluxo 045 + hardening do orquestrador.
///
/// 113I provou que o pipeline era INALCANÇÁVEL: IniciarFluxoAsync só era chamado no botão ENVIAR, quando a
/// caixa já estava em AGUARDANDO_AUTORIZACAO_SAP — estado recusado por fn_pa_045_iniciar_fluxo, que aceita
/// apenas EM_PESAGEM e FINALIZADA_LOCAL.
///
/// Agora, no modo PIPELINE_045, o binding é materializado DEPOIS de FinalizarLocalAsync comprovado e ANTES de
/// SalvarPreviewAsync; no modo HU_ONLY nada muda (ZERO linhas 045). No envio, o fluxo é apenas PROVADO.
/// Sem banco e sem SAP.
/// </summary>
public sealed class PaPipelineEarlyBinding113JTests
{
    // ================= fakes mínimos =================

    private sealed class RepoFake : IProdutoAcabadoRepositorio
    {
        public List<string> Log { get; } = [];
        public bool FinalizarLocalOk { get; set; } = true;
        // GATE 113M (B1): o fake passa a refletir o CONTRATO REAL — finalizar_local bem-sucedido PERSISTE
        // FINALIZADA_LOCAL, e a releitura devolve esse estado. Antes ficava fixo em AGUARDANDO, o que
        // mascarava a ausência da prova de estado persistido.
        public StatusIntegracaoCaixa Status { get; set; } = StatusIntegracaoCaixa.EmPesagem;

        private ProdutoAcabadoCaixa Snapshot(long codigo = 77) => new()
        {
            CodigoProdutoAcabadoCaixa = codigo,
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
            StatusIntegracao = Status
        };

        public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa c, CancellationToken ct = default)
        { Log.Add("registrar_caixa"); return Task.FromResult(Snapshot()); }
        public Task<long> RegistrarPesagemAsync(RegistroPesagemHuCaixa p, CancellationToken ct = default)
        { Log.Add("registrar_pesagem"); return Task.FromResult(1L); }
        public Task<bool> FinalizarLocalAsync(long c, long u, string t, CancellationToken ct = default)
        {
            Log.Add("finalizar_local");
            if (FinalizarLocalOk) { Status = StatusIntegracaoCaixa.FinalizadaLocal; }
            return Task.FromResult(FinalizarLocalOk);
        }
        public Task<bool> SalvarPreviewAsync(long c, string req, string end, CancellationToken ct = default)
        { Log.Add("salvar_preview"); Status = StatusIntegracaoCaixa.PreviewHuGerado; return Task.FromResult(true); }
        public Task<bool> AguardarAutorizacaoAsync(long c, CancellationToken ct = default)
        { Log.Add("aguardar_autorizacao"); Status = StatusIntegracaoCaixa.AguardandoAutorizacaoSap; return Task.FromResult(true); }
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
            => Task.FromResult<ProdutoAcabadoCaixa?>(Snapshot());
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

    // ================= A/B/C — EARLY BINDING =================

    [Fact] // A) HU-only: finalização cria ZERO 045 (binding null ⇒ nem é consultado).
    public async Task A_HuOnly_FinalizacaoCriaZero045()
    {
        RepoFake repo = new();
        ProdutoAcabadoHuService service = new(repo, new GatewayHuFake());

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(service.ModoPipeline045);
        Assert.True(r.Sucesso, r.Mensagem);
        Assert.Equal(
            ["registrar_caixa", "registrar_pesagem", "finalizar_local", "salvar_preview", "aguardar_autorizacao"],
            repo.Log);
    }

    [Fact] // B) Pipeline: binding ocorre APÓS finalizar_local e ANTES de salvar_preview.
    public async Task B_Pipeline_BindingEntreFinalizarLocalEPreview()
    {
        RepoFake repo = new();
        List<string> ordem = [];
        ProdutoAcabadoHuService service = new(repo, new GatewayHuFake(), requestBuilder: null,
            estabelecerBindingPipeline045: (_, _, _, _) => { ordem.Add("binding_045"); return Task.FromResult(true); });

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.True(service.ModoPipeline045);
        Assert.True(r.Sucesso, r.Mensagem);
        Assert.Single(ordem);

        int posFinalizar = repo.Log.IndexOf("finalizar_local");
        int posPreview = repo.Log.IndexOf("salvar_preview");
        Assert.True(posFinalizar >= 0 && posPreview > posFinalizar, string.Join(",", repo.Log));
        // O binding foi chamado exatamente uma vez, e o preview aconteceu depois dele.
        Assert.Equal(
            ["registrar_caixa", "registrar_pesagem", "finalizar_local", "salvar_preview", "aguardar_autorizacao"],
            repo.Log);
    }

    [Fact] // C) IniciarFluxo=false ⇒ preview/aguardar NÃO ocorrem; zero SAP.
    public async Task C_BindingFalso_BloqueiaPreviewEAguardar()
    {
        RepoFake repo = new();
        ProdutoAcabadoHuService service = new(repo, new GatewayHuFake(), requestBuilder: null,
            estabelecerBindingPipeline045: (_, _, _, _) => Task.FromResult(false));

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(r.Sucesso);
        Assert.Equal(ProdutoAcabadoHuService.MensagemBindingPipeline045NaoEstabelecido, r.Mensagem);
        Assert.DoesNotContain("salvar_preview", repo.Log);
        Assert.DoesNotContain("aguardar_autorizacao", repo.Log);
        Assert.DoesNotContain("claim_envio", repo.Log);
        Assert.Equal(["registrar_caixa", "registrar_pesagem", "finalizar_local"], repo.Log);
    }

    [Fact] // O binding não é tentado quando a finalização local não é comprovada.
    public async Task BindingNaoOcorreSemFinalizarLocalComprovado()
    {
        RepoFake repo = new() { FinalizarLocalOk = false };
        bool chamou = false;
        ProdutoAcabadoHuService service = new(repo, new GatewayHuFake(), requestBuilder: null,
            estabelecerBindingPipeline045: (_, _, _, _) => { chamou = true; return Task.FromResult(true); });

        ResultadoFinalizacaoHu r = await service.RegistrarEFinalizarCaixaAsync(CaixaParaFinalizar(), 1, "T1");

        Assert.False(r.Sucesso);
        Assert.False(chamou);
        Assert.DoesNotContain("salvar_preview", repo.Log);
    }

    // ================= composição do binding (modo) =================

    private static ConfiguracaoSap Config(bool pipeline) => new() { ProdutoAcabadoPipelineHabilitado = pipeline };

    private sealed class OpsFake(bool suporta, IReadOnlyList<string>? etapas = null) : IProdutoAcabadoPipeline045Operacoes
    {
        public bool SuportaPersistenciaDefinitiva => suporta;
        public int IniciarFluxoChamadas { get; private set; }
        public Task<bool> IniciarFluxoAsync(long c, long u, string t, string? origem = null, CancellationToken ct = default)
        { IniciarFluxoChamadas++; OrigemRecebida = origem; return Task.FromResult(true); }
        public string? OrigemRecebida { get; private set; }
        public Task<bool> PrepararEtapaAsync(long c, string e, string p, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long c, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoEtapaAsync(long c, string e, int h, string md, string my, string rj, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroEtapaAsync(long c, string e, int h, string rj, string er, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutEtapaAsync(long c, string e, string er, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long c, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long c, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long c, string e, string r, int? h, string? md, string? my, string rj, string? er, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long c, string e, string tl, string m, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<long?> CriarPaleteAsync(string cp, string pl, string sl, decimal pb, decimal pli, decimal tk, long u, string t, CancellationToken ct = default) => Task.FromResult<long?>(null);
        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long c, string rj, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoPaleteAsync(long c, int h, string uc, string rj, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroPaleteAsync(long c, int h, string rj, string er, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutPaleteAsync(long c, string er, string ep, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> VincularCaixaPaleteAsync(long c, long cx, int s, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long c, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Linha045>)(etapas ?? [])
                .Select(e => new Linha045(new Dictionary<string, object?> { ["etapa"] = e }))
                .ToList());
        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long c, CancellationToken ct = default)
            => Task.FromResult((IReadOnlyList<Linha045>)[]);
    }

    [Fact] // O binding só é composto no modo PIPELINE_045 (gate ligado + persistência definitiva).
    public void BindingCompostoSomenteNoModoPipeline()
    {
        Assert.Null(ProdutoAcabadoController.MontarBindingPipeline045(Config(false), new OpsFake(true)));
        Assert.Null(ProdutoAcabadoController.MontarBindingPipeline045(Config(true), new OpsFake(false)));
        Assert.Null(ProdutoAcabadoController.MontarBindingPipeline045(Config(true), null));
        Assert.NotNull(ProdutoAcabadoController.MontarBindingPipeline045(Config(true), new OpsFake(true)));
    }

    [Fact] // O binding chama IniciarFluxoAsync com a origem rastreável do early binding.
    public async Task BindingChamaIniciarFluxoComOrigemRastreavel()
    {
        OpsFake ops = new(true);
        Func<long, long, string, CancellationToken, Task<bool>>? binding =
            ProdutoAcabadoController.MontarBindingPipeline045(Config(true), ops);

        Assert.NotNull(binding);
        Assert.True(await binding!(7, 1, "T1", CancellationToken.None));
        Assert.Equal(1, ops.IniciarFluxoChamadas);
        Assert.Equal("EARLY_BINDING_FINALIZADA_LOCAL", ops.OrigemRecebida);
    }

    // ================= D/E/F — VALIDAÇÃO DO VÍNCULO NO ENVIO =================

    private static IReadOnlyList<Linha045> Etapas(params string[] nomes)
        => [.. nomes.Select(n => new Linha045(new Dictionary<string, object?> { ["etapa"] = n }))];

    /// <summary>
    /// GATE 126B: a view runtime do DEV110 NAO expunha o apelido "etapa" — servia apenas a coluna real
    /// "etapa_sap". Este helper reproduz essa linha exatamente como ela chegava ao orquestrador.
    /// </summary>
    private static IReadOnlyList<Linha045> EtapasSomenteColunaReal(params string[] nomes)
        => [.. nomes.Select(n => new Linha045(new Dictionary<string, object?> { ["etapa_sap"] = n }))];

    // GATE 126B: o caso real do DEV110 em 16/11/2026. Sem o apelido, NomeEtapa045 devolvia null para
    // TODAS as linhas e o envio morria com "etapas=2; 261=nao; 101=nao" — duas etapas achadas e nenhuma
    // reconhecida. Ler a coluna real torna o pipeline independente da definicao da view.
    [Fact]
    public void G_VinculoCompletoSoComEtapaSap_Valido()
    {
        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(
            EtapasSomenteColunaReal("261", "101"), out string m));
        Assert.Equal(string.Empty, m);

        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(
            EtapasSomenteColunaReal("101", "261"), out _));
    }

    // A tolerancia e so para o NOME da coluna: o criterio 261+101 continua valendo igual.
    [Theory]
    [InlineData("261")]
    [InlineData("261", "261")]
    [InlineData("999", "888")]
    public void H_SoComEtapaSap_VinculoInconsistenteSegueFailClosed(params string[] nomes)
    {
        Assert.False(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(
            EtapasSomenteColunaReal(nomes), out string m));
        Assert.Contains("045", m, StringComparison.Ordinal);
    }

    // Quando a view traz os dois, "etapa" continua tendo precedencia — o QAS110 nao muda de comportamento.
    [Fact]
    public void I_ComOsDoisNomes_EtapaTemPrecedencia()
    {
        IReadOnlyList<Linha045> linhas =
        [
            new(new Dictionary<string, object?> { ["etapa"] = "261", ["etapa_sap"] = "261" }),
            new(new Dictionary<string, object?> { ["etapa"] = "101", ["etapa_sap"] = "101" })
        ];

        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(linhas, out _));

        // Apelido vazio NAO deve mascarar a coluna real: string em branco nao conta como nome.
        IReadOnlyList<Linha045> apelidoVazio =
        [
            new(new Dictionary<string, object?> { ["etapa"] = "", ["etapa_sap"] = "261" }),
            new(new Dictionary<string, object?> { ["etapa"] = "", ["etapa_sap"] = "101" })
        ];

        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(apelidoVazio, out _));
    }

    [Fact] // D) vínculo completo ⇒ válido (e o envio NÃO recria o fluxo: ver teste de sequência do 045).
    public void D_VinculoCompleto_Valido()
    {
        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(Etapas("261", "101"), out string m));
        Assert.Equal(string.Empty, m);
        Assert.True(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(Etapas("101", "261"), out _));
    }

    [Fact] // E) zero 045 ⇒ FAIL-CLOSED (caixa é HU-only; nunca promover).
    public void E_ZeroVinculo_FailClosed()
    {
        Assert.False(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(Etapas(), out string m));
        Assert.Contains("sem vínculo 045", m, StringComparison.OrdinalIgnoreCase);
        Assert.False(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(null, out _));
    }

    [Theory] // F) parcial/duplicado/desconhecido ⇒ FAIL-CLOSED.
    [InlineData("261")]
    [InlineData("101")]
    [InlineData("261", "261")]
    [InlineData("261", "101", "101")]
    [InlineData("999", "888")]
    public void F_VinculoParcialOuInconsistente_FailClosed(params string[] nomes)
    {
        Assert.False(ProdutoAcabadoPipeline045Orquestrador.Vinculo045Valido(Etapas(nomes), out string m));
        Assert.Contains("inconsistente", m, StringComparison.OrdinalIgnoreCase);
    }

    // ================= G/H — PREPARAR ETAPA =================

    [Fact] // G/H) o resultado de PrepararEtapaAsync é verificado no source (zero claim, zero POST).
    public void GH_PrepararEtapaResultadoVerificado()
    {
        string src = FonteOrquestrador();
        int inicio = src.IndexOf("private async Task<ResultadoEtapa> ExecutarEtapaAsync", StringComparison.Ordinal);
        Assert.True(inicio > 0);
        string corpo = src[inicio..];

        int posPreparar = corpo.IndexOf("bool preparada = await _ops.PrepararEtapaAsync(", StringComparison.Ordinal);
        int posGuarda = corpo.IndexOf("if (!preparada)", StringComparison.Ordinal);
        int posClaim = corpo.IndexOf("_ops.AdquirirClaimEtapaAsync(", StringComparison.Ordinal);

        Assert.True(posPreparar > 0, "PrepararEtapaAsync deve ter o retorno capturado");
        Assert.True(posGuarda > posPreparar, "a guarda deve vir depois da preparação");
        Assert.True(posClaim > posGuarda, "o claim só pode ocorrer DEPOIS da guarda de preparação");
        Assert.DoesNotContain("await _ops.PrepararEtapaAsync(codigo, etapa, requestJson, usuario, terminal, ct);\r\n", corpo, StringComparison.Ordinal);
    }

    [Fact] // §4: o envio não recria o fluxo — IniciarFluxoAsync não é mais chamado pelo orquestrador.
    public void EnvioNaoChamaIniciarFluxo()
    {
        string src = FonteOrquestrador();

        Assert.DoesNotContain("_ops.IniciarFluxoAsync(", src, StringComparison.Ordinal);
        Assert.Contains("_ops.LerEstadoEtapasAsync(", src, StringComparison.Ordinal);
        Assert.Contains("Vinculo045Valido(", src, StringComparison.Ordinal);
    }

    // ================= I/J/K/L — INVARIANTES =================

    [Fact] // I) HAS_045_ROWS ⇒ nunca HU direta (consulta independente do gate; indeterminado bloqueia).
    public void I_Has045_BloqueiaHuDireta()
    {
        string form = FonteForm();
        int inicio = form.IndexOf("private async Task SolicitarEnvioCaixaSapAsync", StringComparison.Ordinal);
        Assert.True(inicio > 0);
        int fim = form.IndexOf("private async Task SolicitarEnvioCaixaPipelineAsync", inicio, StringComparison.Ordinal);
        string huOnly = form[inicio..(fim > inicio ? fim : form.Length)];

        // GATE 113M (B2): o guard passou do bool para o tri-estado; o bloqueio segue existindo.
        Assert.Contains("ConsultarVinculoPipeline045Async", huOnly, StringComparison.Ordinal);
        Assert.Contains("BloqueiaHuDireta(vinculo045)", huOnly, StringComparison.Ordinal);

        // A consulta NÃO depende do gate do pipeline (contrário de VerificarBloqueioPipeline045Async).
        string controller = FonteController();
        int posMetodo = controller.IndexOf(
            "public async Task<EstadoVinculo045> ConsultarVinculoPipeline045Async", StringComparison.Ordinal);
        Assert.True(posMetodo > 0);
        int fimMetodo = controller.IndexOf("internal static bool BloqueiaHuDireta", posMetodo, StringComparison.Ordinal);
        Assert.True(fimMetodo > posMetodo, "delimitador do método não encontrado");
        string corpo = controller[posMetodo..fimMetodo];
        Assert.DoesNotContain("PipelinePaGateHabilitado", corpo, StringComparison.Ordinal);
        // fail-closed: indeterminado NUNCA vira "sem vínculo"
        Assert.Contains("return EstadoVinculo045.ConsultaIndisponivel;", corpo, StringComparison.Ordinal);
    }

    [Fact] // J) zero 045 + histórico PREVIEW/AGUARDANDO ⇒ nada de 045 retroativo.
    public void J_HistoricoZero045_NaoEhPromovido()
    {
        // O único criador de fluxo 045 é o early binding, que ocorre DENTRO da finalização de uma caixa NOVA.
        // Nenhum caminho cria fluxo para caixa já existente: o orquestrador não chama IniciarFluxoAsync.
        Assert.DoesNotContain("IniciarFluxoAsync", FonteOrquestrador(), StringComparison.Ordinal);
        Assert.DoesNotContain("IniciarFluxoAsync", FonteForm(), StringComparison.Ordinal);

        // E uma caixa histórica sem 045 em PREVIEW/AGUARDANDO não vira elegível ao pipeline por promoção:
        // segue valendo a allowlist de estado do 113E.
        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
        {
            CodigoProdutoAcabadoCaixa = 9,
            StatusIntegracao = StatusIntegracaoCaixa.PreviewHuGerado
        }));
    }

    [Fact] // K) 0006/0007 CONFIRMADA_SAP ⇒ zero pipeline (113E preservado).
    public void K_CaixasConfirmadas_ZeroPipeline()
    {
        foreach (string local in new[] { "CX-1000210-0006", "CX-1000210-0007" })
        {
            Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(new ProdutoAcabadoCaixa
            {
                CodigoProdutoAcabadoCaixa = 6,
                CodigoCaixaLocal = local,
                StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap
            }));
        }
    }

    [Fact] // L) a avaliacao do pipeline permanece; a etiqueta saiu daqui para a pesagem.
    public void L_PipelineAvaliadoEEtiquetaNaoDuplicada()
    {
        Assert.True(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(
            new ResultadoPipelineProdutoAcabado(true, EtapaPipelineProdutoAcabado.Concluido, "ok", null)));
        Assert.False(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(
            new ResultadoPipelineProdutoAcabado(true, EtapaPipelineProdutoAcabado.HandlingUnit, "parcial", null)));
        // A etiqueta passou a ser emitida na pesagem; o disparo ao fim do pipeline sairia duplicado.
        string form = FonteForm();
        Assert.DoesNotContain("ImprimirEtiquetaCaixaAsync(caixaConfirmadaPipeline)", form, StringComparison.Ordinal);
        Assert.Contains("ImprimirEtiquetaCaixaPesadaAsync(resultado.Caixa)", form, StringComparison.Ordinal);
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

    private static string FonteOrquestrador()
        => Fonte("Servicos", "Operacao", "ProdutoAcabadoPipeline045Orquestrador.cs");
    private static string FonteForm()
        => Fonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
    private static string FonteController()
        => Fonte("Controle", "Processo", "ProdutoAcabadoController.cs");
}
