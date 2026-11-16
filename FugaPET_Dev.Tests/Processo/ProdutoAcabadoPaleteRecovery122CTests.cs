using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 122C — TRACK B: caminho de aplicação para reconciliar palete.
/// <para>
/// O 122A provou que as primitivas 045 de recovery/reconciliação existiam no banco e no store, mas
/// NENHUMA era alcançável por código de produção. Um POST INT012 indeterminado deixava o palete
/// travado. Estes testes provam A..J do gate, com test doubles — nenhum SAP, nenhum CPI, nenhum POST.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoPaleteRecovery122CTests
{
    private const long Palete = 9001;
    private const long Usuario = 42;
    private const string Terminal = "TERM-01";

    // ===================== A: pendência é detectável =====================

    [Theory]
    [InlineData("RASCUNHO", PendenciaPalete.Limpo)]
    [InlineData("ENVIADO_SAP", PendenciaPalete.PendenteReconciliacao)]
    [InlineData("ERRO_SAP", PendenciaPalete.PendenteLiberacao)]
    [InlineData("CONFIRMADO_SAP", PendenciaPalete.ConfirmadoNoSap)]
    [InlineData("CANCELADO", PendenciaPalete.Cancelado)]
    [InlineData("rascunho", PendenciaPalete.Limpo)]
    [InlineData("  ENVIADO_SAP  ", PendenciaPalete.PendenteReconciliacao)]
    public async Task A_PendenciaEhDetectavelPeloStatusPersistido(string status, PendenciaPalete esperada)
    {
        FakeOps045 ops = new() { StatusPalete = status };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        Assert.Equal(esperada, await svc.DetectarPendenciaAsync(Palete));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ESTADO_QUE_NAO_EXISTE")]
    [InlineData(null)]
    public async Task A_StatusAusenteOuDesconhecido_EhIndeterminadoNaoLimpo(string? status)
    {
        FakeOps045 ops = new() { StatusPalete = status };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        PendenciaPalete p = await svc.DetectarPendenciaAsync(Palete);
        Assert.Equal(PendenciaPalete.Indeterminado, p);
        Assert.False(ProdutoAcabadoPaleteRecoveryServico.EnvioPermitido(p));
    }

    [Fact]
    public async Task A_SemLinhaDeEstado_EhIndeterminado()
    {
        FakeOps045 ops = new() { SemEstado = true };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        Assert.Equal(PendenciaPalete.Indeterminado, await svc.DetectarPendenciaAsync(Palete));
    }

    [Fact]
    public async Task A_FalhaDeLeitura_EhIndeterminado_NaoLibera()
    {
        FakeOps045 ops = new() { LancarNaLeitura = true };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        Assert.Equal(PendenciaPalete.Indeterminado, await svc.DetectarPendenciaAsync(Palete));
        (bool permitido, _, _) = await svc.PodeEnviarAsync(Palete);
        Assert.False(permitido);
    }

    // ===================== B: recovery adquirido uma única vez (fencing) =====================

    [Fact]
    public async Task B_RecoveryEhAdquiridoUmaVez_ESegundaTentativaEhNegada()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP", RecoveryApenasUmaVez = true };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete primeira = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);
        ResultadoRecoveryPalete segunda = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);

        Assert.True(primeira.RecoveryAdquirido);
        Assert.False(segunda.RecoveryAdquirido);
        Assert.False(segunda.Sucesso);
        Assert.Equal(ProdutoAcabadoPaleteRecoveryServico.MotivoRecoveryNaoAdquirido, segunda.Mensagem);
        Assert.Equal(1, ops.RecoveryAdquiridoVezes);
    }

    [Fact]
    public async Task B_RecoveryNegado_NaoRegistraReconciliacaoNemLibera()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP", RecoveryObtido = false };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(0, ops.ReconciliacoesRegistradas);
        Assert.Equal(0, ops.LiberacoesSolicitadas);
    }

    [Fact]
    public async Task B_RecoverySemTokenOuTentativaValidos_Bloqueia()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP", RecoveryTentativa = 0 };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.Equal(0, ops.ReconciliacoesRegistradas);
    }

    // ===================== C: claim antigo não executa novo write =====================

    [Fact]
    public async Task C_ClaimAntigoNaoAutorizaNovoPost_OrquestradorBloqueiaAntesDoClaim()
    {
        // Palete persistido em ENVIADO_SAP (claim anterior em aberto) ⇒ bloqueio ANTES do claim.
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
        FakeGatewayPalete gateway = new();
        ProdutoAcabadoPaleteInt012Orquestrador orq = new(ops, gateway);

        ResultadoPalete045 r = await orq.ExecutarAsync(
            [1L], Requisicao(), Usuario, Terminal, codigoPaleteExistente: Palete);

        Assert.False(r.Executou);
        Assert.Equal(0, ops.ClaimsSolicitados);
        Assert.Equal(0, gateway.PostsExecutados);
        Assert.Contains("reconciliacao", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== D: reconciliação positiva registra resultado =====================

    [Fact]
    public async Task D_ReconciliacaoConfirmadaNoSap_RegistraEFechaSemLiberar()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.ConfirmadoNoSap, "300000999", """{"UC":"300000999"}""",
            Usuario, Terminal);

        Assert.True(r.Sucesso);
        Assert.True(r.ReconciliacaoRegistrada);
        Assert.False(r.ReprocessamentoLiberado);           // confirmado no SAP NÃO reprocessa
        Assert.Equal(0, ops.LiberacoesSolicitadas);
        // GATE 126L: valor REAL aceito pelo banco (antes "CONFIRMADO_NO_SAP", que a funcao recusa).
        Assert.Equal("CONFIRMADO_EXISTE", ops.UltimoResultadoReconciliacao);
        Assert.Equal("300000999", ops.UltimoHuPaiReconciliacao);
    }

    [Fact]
    public async Task D_ReconciliacaoAusenciaComprovada_RegistraELibera()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, """{"evidencia":"ausente"}""",
            Usuario, Terminal);

        Assert.True(r.Sucesso);
        Assert.True(r.ReconciliacaoRegistrada);
        Assert.True(r.ReprocessamentoLiberado);
        // GATE 126L: valor REAL aceito pelo banco (antes "NAO_EXECUTADO_NO_SAP", que a funcao recusa).
        Assert.Equal("CONFIRMADO_NAO_EXISTE", ops.UltimoResultadoReconciliacao);
        Assert.Equal(
            ProdutoAcabadoPaleteRecoveryServico.TipoLiberacaoAusenciaComprovada,
            ops.UltimoTipoLiberacao);
    }

    // ===================== E: reconciliação negativa mantém bloqueio =====================

    [Fact]
    public async Task E_VeredictoIndeterminado_RegistraMasMantemBloqueio()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.Indeterminado, null, "{}", Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.True(r.ReconciliacaoRegistrada);
        Assert.False(r.ReprocessamentoLiberado);
        Assert.Equal(0, ops.LiberacoesSolicitadas);
        // GATE 126L: valor REAL aceito pelo banco (antes "INDETERMINADO", que a funcao recusa).
        Assert.Equal("DIVERGENTE", ops.UltimoResultadoReconciliacao);
        Assert.Equal(ProdutoAcabadoPaleteRecoveryServico.MotivoVeredictoIndeterminado, r.Mensagem);
    }

    [Fact]
    public async Task E_BancoNegaORegistro_MantemPendenteSemLiberar()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP", ReconciliacaoAceita = false };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.False(r.ReconciliacaoRegistrada);
        Assert.False(r.ReprocessamentoLiberado);
        Assert.Equal(0, ops.LiberacoesSolicitadas);
    }

    // ===================== F: liberação só após resultado seguro =====================

    [Fact]
    public async Task F_LiberacaoNuncaOcorreSemVeredictoDeAusencia()
    {
        foreach (VeredictoReconciliacaoPalete v in new[]
        {
            VeredictoReconciliacaoPalete.Indeterminado,
            VeredictoReconciliacaoPalete.ConfirmadoNoSap
        })
        {
            FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
            ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

            await svc.ReconciliarAsync(Palete, v, "300000999", "{}", Usuario, Terminal);

            Assert.Equal(0, ops.LiberacoesSolicitadas);
        }
    }

    [Fact]
    public async Task F_OrdemObrigatoria_RecoveryAntesDeRegistrarEDeLiberar()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);

        Assert.Equal(
            new[] { "RECOVERY", "RECONCILIACAO", "LIBERACAO" },
            ops.Ordem.ToArray());
    }

    [Fact]
    public async Task F_LiberacaoNegadaPeloBanco_MantemBloqueio()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP", LiberacaoAceita = false };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete r = await svc.ReconciliarAsync(
            Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal);

        Assert.False(r.Sucesso);
        Assert.True(r.ReconciliacaoRegistrada);
        Assert.False(r.ReprocessamentoLiberado);
        Assert.Equal(ProdutoAcabadoPaleteRecoveryServico.MotivoLiberacaoNegada, r.Mensagem);
    }

    // ===================== G: zero blind retry =====================

    [Fact]
    public async Task G_ZeroBlindRetry_NenhumPostNoCaminhoDeReconciliacao()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" };
        FakeGatewayPalete gateway = new();
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        foreach (VeredictoReconciliacaoPalete v in Enum.GetValues<VeredictoReconciliacaoPalete>())
        {
            FakeOps045 o = new() { StatusPalete = "ENVIADO_SAP" };
            await new ProdutoAcabadoPaleteRecoveryServico(o).ReconciliarAsync(
                Palete, v, "300000999", "{}", Usuario, Terminal);
        }

        // O serviço de recovery não tem gateway: por construção não existe POST nele.
        Assert.Equal(0, gateway.PostsExecutados);
        Assert.Equal(0, ops.ClaimsSolicitados);
    }

    [Fact]
    public void G_ServicoDeRecoveryNaoTemAcessoAHttpNemAGateway()
    {
        string fonte = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabadoPaleteRecoveryServico.cs"));

        foreach (string proibido in new[]
        {
            "HttpClient", "SendAsync", "PostAsync", "Int012Gateway", "EnviarPaleteAsync", "Npgsql"
        })
        {
            Assert.DoesNotContain(proibido, fonte, StringComparison.Ordinal);
        }
    }

    // ===================== H: concorrência não gera autorização dupla =====================

    [Fact]
    public async Task H_DuasTentativasConcorrentes_NaoGeramAutorizacaoDupla()
    {
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP", RecoveryApenasUmaVez = true };
        ProdutoAcabadoPaleteRecoveryServico svc = new(ops);

        ResultadoRecoveryPalete[] resultados = await Task.WhenAll(
            svc.ReconciliarAsync(Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal),
            svc.ReconciliarAsync(Palete, VeredictoReconciliacaoPalete.NaoExecutadoNoSap, null, "{}", Usuario, Terminal));

        Assert.Single(resultados, r => r.RecoveryAdquirido);
        Assert.Single(resultados, r => r.ReprocessamentoLiberado);
        Assert.Equal(1, ops.RecoveryAdquiridoVezes);
        Assert.Equal(1, ops.LiberacoesSolicitadas);
    }

    // ===================== I: recovery pendente bloqueia novo POST =====================

    [Theory]
    [InlineData("ENVIADO_SAP")]
    [InlineData("ERRO_SAP")]
    [InlineData("CONFIRMADO_SAP")]
    [InlineData("CANCELADO")]
    [InlineData("")]
    [InlineData("DESCONHECIDO")]
    public async Task I_EstadoNaoLimpoBloqueiaNovoPost(string status)
    {
        FakeOps045 ops = new() { StatusPalete = status };
        FakeGatewayPalete gateway = new();
        ProdutoAcabadoPaleteInt012Orquestrador orq = new(ops, gateway);

        ResultadoPalete045 r = await orq.ExecutarAsync(
            [1L], Requisicao(), Usuario, Terminal, codigoPaleteExistente: Palete);

        Assert.False(r.Executou);
        Assert.Equal(0, gateway.PostsExecutados);
        Assert.Equal(0, ops.ClaimsSolicitados);
    }

    [Fact]
    public async Task I_FalhaDeLeituraDoEstado_TambemBloqueiaNovoPost()
    {
        FakeOps045 ops = new() { LancarNaLeitura = true };
        FakeGatewayPalete gateway = new();
        ProdutoAcabadoPaleteInt012Orquestrador orq = new(ops, gateway);

        ResultadoPalete045 r = await orq.ExecutarAsync(
            [1L], Requisicao(), Usuario, Terminal, codigoPaleteExistente: Palete);

        Assert.False(r.Executou);
        Assert.Equal(0, gateway.PostsExecutados);
    }

    // ===================== J: fluxo normal não regride =====================

    [Fact]
    public async Task J_PaletePersistidoEmRascunho_SegueOFluxoNormalComClaimEPost()
    {
        FakeOps045 ops = new() { StatusPalete = "RASCUNHO" };
        FakeGatewayPalete gateway = new() { Estado = EstadoPaleteInt012.Confirmado, Uc = "300000999" };
        ProdutoAcabadoPaleteInt012Orquestrador orq = new(ops, gateway);

        ResultadoPalete045 r = await orq.ExecutarAsync(
            [1L], Requisicao(), Usuario, Terminal, codigoPaleteExistente: Palete);

        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal("300000999", r.UcGerada);
        Assert.Equal(1, ops.ClaimsSolicitados);
        Assert.Equal(1, gateway.PostsExecutados);
    }

    [Fact]
    public async Task J_PaleteNovoSemIdentidadePersistida_NaoPassaPeloGuardDeRecovery()
    {
        // Sem codigoPaleteExistente o palete ainda não existe: o guard não se aplica (nada a reconciliar).
        FakeOps045 ops = new() { StatusPalete = "ENVIADO_SAP" }; // seria pendente SE fosse consultado
        FakeGatewayPalete gateway = new() { Estado = EstadoPaleteInt012.Confirmado, Uc = "300001000" };
        ProdutoAcabadoPaleteInt012Orquestrador orq = new(ops, gateway);

        ResultadoPalete045 r = await orq.ExecutarAsync([1L], Requisicao(), Usuario, Terminal);

        Assert.True(r.Executou);
        Assert.Equal(0, ops.LeiturasDeEstado);   // guard não consultado no caminho de criação
        Assert.Equal(1, gateway.PostsExecutados);
    }

    [Fact]
    public async Task J_GatewayNaoAutorizado_ContinuaBloqueandoAntesDeTudo()
    {
        FakeOps045 ops = new() { StatusPalete = "RASCUNHO" };
        FakeGatewayPalete gateway = new() { Autorizado = false };
        ProdutoAcabadoPaleteInt012Orquestrador orq = new(ops, gateway);

        ResultadoPalete045 r = await orq.ExecutarAsync(
            [1L], Requisicao(), Usuario, Terminal, codigoPaleteExistente: Palete);

        Assert.False(r.Executou);
        Assert.Equal(0, ops.LeiturasDeEstado);   // nem chega ao guard
        Assert.Equal(0, gateway.PostsExecutados);
    }

    // ===================== as primitivas estão na interface =====================

    [Fact]
    public void Interface_ExpoeReconciliacaoELiberacaoDePalete()
    {
        Type t = typeof(IProdutoAcabadoPipeline045Operacoes);

        Assert.NotNull(t.GetMethod("RegistrarReconciliacaoPaleteAsync"));
        Assert.NotNull(t.GetMethod("LiberarReprocessamentoPaleteAsync"));
        Assert.NotNull(t.GetMethod("AdquirirRecoveryPaleteAsync"));
        Assert.NotNull(t.GetMethod("ReassumirClaimPaleteAsync"));
    }

    [Fact]
    public async Task Interface_DefaultDasNovasPrimitivasEhFailClosed()
    {
        // Implementação mínima que NÃO sobrescreve os defaults: não reconcilia e não libera.
        // Membro default de interface só é alcançável pela INTERFACE — é esse o caminho exercitado.
        IProdutoAcabadoPipeline045Operacoes ops = new OpsSemSuporte();

        Assert.False(await ops.RegistrarReconciliacaoPaleteAsync(
            Palete, "NAO_EXECUTADO_NO_SAP", null, "{}", null, Usuario, Terminal));
        Assert.False(await ops.LiberarReprocessamentoPaleteAsync(
            Palete, "X", "motivo", Usuario, Terminal));
    }

    // ===================== infra =====================

    private static ProdutoAcabadoPaleteRequest Requisicao()
        => new()
        {
            HandlingUnitExternalID = "$1",
            GrossWeight = 20m,
            NetWeight = 18m,
            TareWeight = 2m,
            WeightUnit = "KG",
            Plant = "3007",
            StorageLocation = "PA01",
            PackagingMaterial = "PALLET01",
            HandlingUnitItems = [new() { HandlingUnit = "300000161" }]
        };

    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        Assert.False(string.IsNullOrWhiteSpace(dir), "Raiz do projeto nao localizada.");
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static string SemComentarios(string fonte)
    {
        string semBloco = System.Text.RegularExpressions.Regex.Replace(
            fonte, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"///?[^\r\n]*", string.Empty);
    }

    /// <summary>
    /// Implementação MÍNIMA da interface: implementa só os membros obrigatórios e NÃO sobrescreve
    /// RegistrarReconciliacaoPaleteAsync nem LiberarReprocessamentoPaleteAsync — assim o teste
    /// exercita os DEFAULTS da interface, não um fake. (Herdar o FakeOps045 mascararia o default.)
    /// </summary>
    private sealed class OpsSemSuporte : IProdutoAcabadoPipeline045Operacoes
    {
        public bool SuportaPersistenciaDefinitiva => false;

        public Task<bool> IniciarFluxoAsync(long codigo, long usuario, string terminal, string? origem = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> PrepararEtapaAsync(long codigo, string etapa, string payloadJson, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<bool> RegistrarSucessoEtapaAsync(long codigo, string etapa, int http, string materialDocument, string materialDocumentYear, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RegistrarErroEtapaAsync(long codigo, string etapa, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RegistrarTimeoutEtapaAsync(long codigo, string etapa, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long codigo, string etapa, string resultado, int? http, string? materialDocument, string? materialDocumentYear, string responseJson, string? erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long codigo, string etapa, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<long?> CriarPaleteAsync(string codigoPaleteLocal, string plant, string storageLocation, decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult<long?>(null);
        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long codigoPalete, string requestJson, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<bool> RegistrarSucessoPaleteAsync(long codigoPalete, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RegistrarErroPaleteAsync(long codigoPalete, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> RegistrarTimeoutPaleteAsync(long codigoPalete, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> VincularCaixaPaleteAsync(long codigoPalete, long codigoCaixa, int sequencia, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long codigo, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Linha045>>([]);
        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long codigoPalete, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Linha045>>([]);
    }

    private class FakeOps045 : IProdutoAcabadoPipeline045Operacoes
    {
        private readonly object _trava = new();

        public bool SuportaPersistenciaDefinitiva => true;

        public string? StatusPalete { get; init; } = "RASCUNHO";
        public bool SemEstado { get; init; }
        public bool LancarNaLeitura { get; init; }
        public bool RecoveryObtido { get; init; } = true;
        public bool RecoveryApenasUmaVez { get; init; }
        public int RecoveryTentativa { get; init; } = 1;
        public bool ReconciliacaoAceita { get; init; } = true;
        public bool LiberacaoAceita { get; init; } = true;

        public int LeiturasDeEstado { get; private set; }
        public int RecoveryAdquiridoVezes { get; private set; }
        public int ReconciliacoesRegistradas { get; private set; }
        public int LiberacoesSolicitadas { get; private set; }
        public int ClaimsSolicitados { get; private set; }
        public string? UltimoResultadoReconciliacao { get; private set; }
        public string? UltimoHuPaiReconciliacao { get; private set; }
        public string? UltimoTipoLiberacao { get; private set; }
        public List<string> Ordem { get; } = [];

        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long codigoPalete, CancellationToken ct = default)
        {
            lock (_trava) { LeiturasDeEstado++; }
            if (LancarNaLeitura) { throw new InvalidOperationException("estado indisponivel"); }
            if (SemEstado) { return Task.FromResult<IReadOnlyList<Linha045>>([]); }

            Dictionary<string, object?> colunas = new(StringComparer.OrdinalIgnoreCase)
            {
                ["codigo_hu_palete"] = codigoPalete,
                ["status_hu_palete"] = StatusPalete
            };
            return Task.FromResult<IReadOnlyList<Linha045>>([new Linha045(colunas)]);
        }

        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default)
        {
            lock (_trava)
            {
                if (!RecoveryObtido || (RecoveryApenasUmaVez && RecoveryAdquiridoVezes >= 1))
                {
                    return Task.FromResult(new ResultadoClaim045(false, null, null));
                }

                RecoveryAdquiridoVezes++;
                Ordem.Add("RECOVERY");
                return Task.FromResult(new ResultadoClaim045(true, Guid.NewGuid(), RecoveryTentativa));
            }
        }

        public Task<bool> RegistrarReconciliacaoPaleteAsync(
            long codigoPalete, string resultado, string? huPai, string evidenciaJson, string? erro,
            long usuario, string terminal, CancellationToken ct = default)
        {
            lock (_trava)
            {
                if (!ReconciliacaoAceita) { return Task.FromResult(false); }
                ReconciliacoesRegistradas++;
                UltimoResultadoReconciliacao = resultado;
                UltimoHuPaiReconciliacao = huPai;
                Ordem.Add("RECONCILIACAO");
                return Task.FromResult(true);
            }
        }

        public Task<bool> LiberarReprocessamentoPaleteAsync(
            long codigoPalete, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default)
        {
            lock (_trava)
            {
                if (!LiberacaoAceita) { return Task.FromResult(false); }
                LiberacoesSolicitadas++;
                UltimoTipoLiberacao = tipoLiberacao;
                Ordem.Add("LIBERACAO");
                return Task.FromResult(true);
            }
        }

        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long codigoPalete, string requestJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        {
            lock (_trava) { ClaimsSolicitados++; }
            return Task.FromResult(new ResultadoClaim045(true, Guid.NewGuid(), 1));
        }

        public Task<long?> CriarPaleteAsync(string codigoPaleteLocal, string plant, string storageLocation, decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult<long?>(Palete);

        public Task<bool> VincularCaixaPaleteAsync(long codigoPalete, long codigoCaixa, int sequencia, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(new ResultadoClaim045(false, null, null));

        public Task<bool> RegistrarSucessoPaleteAsync(long codigoPalete, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> RegistrarErroPaleteAsync(long codigoPalete, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> RegistrarTimeoutPaleteAsync(long codigoPalete, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(true);

        // ---- etapas (261/101/HU): não exercitadas por este gate ----
        public Task<bool> IniciarFluxoAsync(long codigo, long usuario, string terminal, string? origem = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> PrepararEtapaAsync(long codigo, string etapa, string payloadJson, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(true, Guid.NewGuid(), 1));
        public Task<bool> RegistrarSucessoEtapaAsync(long codigo, string etapa, int http, string materialDocument, string materialDocumentYear, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroEtapaAsync(long codigo, string etapa, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutEtapaAsync(long codigo, string etapa, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(false, null, null));
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long codigo, string etapa, string resultado, int? http, string? materialDocument, string? materialDocumentYear, string responseJson, string? erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long codigo, string etapa, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long codigo, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Linha045>>([]);
    }

    private sealed class FakeGatewayPalete : IProdutoAcabadoPaleteInt012Gateway
    {
        public bool Autorizado { get; init; } = true;
        public EstadoPaleteInt012 Estado { get; init; } = EstadoPaleteInt012.Confirmado;
        public string? Uc { get; init; } = "300000999";
        public int PostsExecutados { get; private set; }

        public bool EnvioAutorizado => Autorizado;

        public Task<ResultadoPaleteInt012> EnviarPaleteAsync(ProdutoAcabadoPaleteRequest requisicao, CancellationToken cancellationToken = default)
        {
            PostsExecutados++;
            return Task.FromResult(Estado == EstadoPaleteInt012.Confirmado
                ? new ResultadoPaleteInt012 { Estado = Estado, UcGerada = Uc, HttpStatus = 200, MensagemSanitizada = "ok" }
                : new ResultadoPaleteInt012 { Estado = Estado, UcGerada = null, HttpStatus = null, MensagemSanitizada = "falha" });
        }
    }
}
