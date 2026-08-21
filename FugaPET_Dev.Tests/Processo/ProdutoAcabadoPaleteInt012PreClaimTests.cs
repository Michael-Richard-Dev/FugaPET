using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 046-AP: fluxo INT012 pré-claim. Comprova que uma confirmação de envio (a) NÃO some silenciosamente —
/// todo desfecho vira mensagem CONTROLADA via presenter — e (b) só alcança o orquestrador/claim/POST quando
/// autorizado; write gate off / snapshot inválido bloqueiam ANTES do POST, com feedback explícito. Sem SAP/CPI
/// real (gateway e executor são fakes; nenhum HTTP/banco).
/// </summary>
public sealed class ProdutoAcabadoPaleteInt012PreClaimTests
{
    private static ProdutoAcabadoController Controller(FakeGatewayInt012 gateway, FakeExecutor045Int012 executor)
        => new(new ProductionOrderSapFake(),
            configuracaoSap: new ConfiguracaoSap { ProdutoAcabadoPipelineHabilitado = true },
            pipelineStore: new ProdutoAcabadoPipelinePostgresStore(executor),
            paleteInt012Gateway: gateway);

    private static ProdutoAcabadoPalete Palete(StatusIntegracaoCaixa statusCaixa = StatusIntegracaoCaixa.ConfirmadaSap, string? packaging = "PALLET01") => new()
    {
        CodigoPaleteLocal = "PLT-1002024-0002-0003",
        PrimeiraCaixa = 2,
        UltimaCaixa = 3,
        PesoBrutoKg = 20m,
        PesoLiquidoKg = 18m,
        TaraKg = 2m,
        Plant = "3007",
        StorageLocation = "PP01",
        PackagingMaterial = packaging!,
        Caixas =
        [
            Caixa(2, "HU-A", statusCaixa),
            Caixa(3, "HU-B", StatusIntegracaoCaixa.ConfirmadaSap),
        ]
    };

    private static ProdutoAcabadoCaixa Caixa(
        int numero,
        string hu,
        StatusIntegracaoCaixa status,
        string ordem = "1002024",
        string material = "4000108",
        string lote = "67008561F") => new()
    {
        CodigoProdutoAcabadoCaixa = numero,
        NumeroCaixa = numero,
        NumeroOrdemProducao = ordem,
        Material = material,
        Lote = lote,
        StatusIntegracao = status,
        HandlingUnitExternalId = hu,
        PesoBrutoKg = 10m,
        PesoLiquidoKg = 9m,
        TaraKg = 1m
    };

    private static ProdutoAcabadoPalete PaleteComCaixas(params ProdutoAcabadoCaixa[] caixas) => new()
    {
        CodigoPaleteLocal = "PLT-1002024-0002-0003",
        PrimeiraCaixa = 2,
        UltimaCaixa = 3,
        PesoBrutoKg = 20m,
        PesoLiquidoKg = 18m,
        TaraKg = 2m,
        Plant = "3007",
        StorageLocation = "PP01",
        PackagingMaterial = "PALLET01",
        Caixas = caixas
    };

    // GATE 047-N/047-Z: heterogeneidade (OP/material/lote) NÃO bloqueia mais o INT012. O palete alcança claim + POST
    // normalmente (guard temporário de homogeneidade removido; proposta Gaia 047-M cancelada). Sem guard de lote inventado.
    private static async Task AssertProcedeAoGateInt012Async(ProdutoAcabadoPalete palete)
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();

        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(palete);

        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal(1, ex.Chamadas("fn_pa_045_palete_claim_envio")); // chegou ao CLAIM (não bloqueado por homogeneidade)
        Assert.Equal(1, gw.Posts);                                    // exatamente 1 POST_FORMACAO
    }
    // ===== GATE 047-CH: guard PRÉ-CLAIM do PackagingMaterial (MVP congelado = PALLET01) =====
    [Fact] // A) PALLET01 é permitido → alcança claim + POST (não bloqueado pelo guard de material).
    public async Task Pallet01_Permitido_AlcancaClaimEPost()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();
        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete(packaging: "PALLET01"));

        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal(1, ex.Chamadas("fn_pa_045_palete_claim_envio"));
        Assert.Equal(1, gw.Posts);
    }

    [Theory] // B/C) PALLET02/PALLET03 (inexistentes no A_Product) → BLOQUEADO antes do claim, mensagem operacional.
    [InlineData("PALLET02")]
    [InlineData("PALLET03")]
    [InlineData("PALLET05")]
    public async Task MaterialNaoHomologado_BloqueiaAntesDoClaim_ZeroPost(string packaging)
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();
        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete(packaging: packaging));

        Assert.Equal(EstadoPaleteInt012.NaoEnviado, r.Estado);
        Assert.Contains("Material de embalagem não autorizado", r.MensagemSanitizada, StringComparison.Ordinal);
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_criar"));         // não cria
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_claim_envio"));   // não faz claim ⇒ não incrementa tentativa
        Assert.Equal(0, gw.Posts);                                      // não produz POST_FORMACAO/INICIADO nem POST externo
    }

    [Theory] // D/E) NULL / vazio → BLOQUEADO antes do claim (sem claim, sem tentativa, sem POST).
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MaterialNuloOuVazio_BloqueiaAntesDoClaim_ZeroPost(string? packaging)
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();
        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete(packaging: packaging));

        Assert.Equal(EstadoPaleteInt012.NaoEnviado, r.Estado);
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_claim_envio")); // H) tentativa não incrementada
        Assert.Equal(0, gw.Posts);                                    // I) POST_FORMACAO/INICIADO não produzido
    }

    // ===== B) confirmação (autorizado) → orquestrador é alcançado: claim + POST exatamente 1 vez =====
    [Fact]
    public async Task Autorizado_AlcancaOrquestradorClaimEPostUmaVez()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();
        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete());

        Assert.Equal(1, ex.Chamadas("fn_pa_045_palete_criar"));       // orquestrador chegou a criar o palete
        Assert.Equal(1, ex.Chamadas("fn_pa_045_palete_claim_envio")); // ...e ao CLAIM (o ponto que Gaia viu = 0)
        Assert.Equal(1, gw.Posts);                                    // exatamente 1 POST_FORMACAO
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
    }
    [Fact]
    public async Task PaleteHomogeneo_PodeChegarAoGateInt012()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();

        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete());

        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal(1, ex.Chamadas("fn_pa_045_palete_claim_envio"));
        Assert.Equal(1, gw.Posts);
    }

    [Fact] // H) múltiplas OPs no mesmo palete NÃO bloqueia por homogeneidade (contrato 047-N).
    public async Task MultiplasOps_NaoBloqueia_ChegaAoClaimEPost()
    {
        ProdutoAcabadoPalete palete = PaleteComCaixas(
            Caixa(2, "HU-A", StatusIntegracaoCaixa.ConfirmadaSap, ordem: "1002024"),
            Caixa(3, "HU-B", StatusIntegracaoCaixa.ConfirmadaSap, ordem: "1002025"));
        await AssertProcedeAoGateInt012Async(palete);
    }

    [Fact] // I) múltiplos materiais (modo manual) NÃO bloqueia por homogeneidade.
    public async Task MultiplosMateriais_NaoBloqueia_ChegaAoClaimEPost()
    {
        ProdutoAcabadoPalete palete = PaleteComCaixas(
            Caixa(2, "HU-A", StatusIntegracaoCaixa.ConfirmadaSap, material: "4000108"),
            Caixa(3, "HU-B", StatusIntegracaoCaixa.ConfirmadaSap, material: "4000109"));
        await AssertProcedeAoGateInt012Async(palete);
    }

    [Fact] // J) múltiplos lotes NÃO são bloqueados por guard inventado (nenhum guard de lote instalado).
    public async Task MultiplosLotes_SemGuardInventado_ChegaAoClaimEPost()
    {
        ProdutoAcabadoPalete palete = PaleteComCaixas(
            Caixa(2, "HU-A", StatusIntegracaoCaixa.ConfirmadaSap, lote: "67008561F"),
            Caixa(3, "HU-B", StatusIntegracaoCaixa.ConfirmadaSap, lote: "67008562F"));
        await AssertProcedeAoGateInt012Async(palete);
    }

    // ===== GATE 046-AQ-C1: palete LOCAL já criado/vinculado → envio reutiliza a identidade persistida =====
    // Não recria (fn_pa_045_palete_criar) nem revincula (fn_pa_045_palete_vincular_caixa); vai direto ao claim + POST.
    [Fact]
    public async Task PaleteJaCriado_EnvioReutilizaIdentidade_SemRecriarNemRevincular()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();
        ProdutoAcabadoPalete palete = Palete();
        palete.CodigoHuPalete = 900; // identidade persistida (vinda do reload/persistência 046-E)

        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(palete);

        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_criar"));          // NÃO recria
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_vincular_caixa")); // NÃO revincula
        Assert.Equal(1, ex.Chamadas("fn_pa_045_palete_claim_envio"));    // chega ao CLAIM
        Assert.Equal(1, gw.Posts);                                       // 1 POST_FORMACAO
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
    }

    // ===== GATE 046-AQ-Z2-C: POST confirmado no SAP MAS fechamento local NÃO comprovado =====
    // A) registrar_sucesso retorna false ⇒ NÃO é sucesso integral: Indeterminado, UC preservada.
    [Fact]
    public async Task PostConfirmado_FechamentoLocalFalse_Indeterminado_PreservaUC_NaoConfirma()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new() { RegistrarSucessoFalha = true };
        ProdutoAcabadoPalete palete = Palete();
        palete.CodigoHuPalete = 900;

        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(palete);

        Assert.Equal(1, gw.Posts);                                  // o POST ocorreu
        Assert.NotEqual(EstadoPaleteInt012.Confirmado, r.Estado);    // NÃO classifica como sucesso integral
        Assert.Equal(EstadoPaleteInt012.IndeterminadoTimeout, r.Estado);
        Assert.Equal("UC-FAKE-001", r.UcGerada);                    // UC recebida é preservada p/ reconciliação

        ApresentacaoEnvioPaleteInt012 ap = ProdutoAcabadoPaleteEnvioPresenter.Construir("PLT-1002024-0002-0003", r);
        Assert.False(ap.Sucesso);                                              // não é sucesso integral
        Assert.Contains("UC-FAKE-001", ap.Mensagem, StringComparison.Ordinal); // UC preservada
        Assert.Contains("INDETERMINADO", ap.Mensagem, StringComparison.Ordinal);
        Assert.Contains("comprovado", ap.Mensagem, StringComparison.OrdinalIgnoreCase); // sinaliza fechamento NÃO comprovado
    }

    // B) registrar_sucesso lança exceção ⇒ orquestrador não mascara: Indeterminado, UC preservada (sem estourar p/ UI).
    [Fact]
    public async Task PostConfirmado_FechamentoLocalLanca_Indeterminado_PreservaUC()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new() { RegistrarSucessoLanca = true };
        ProdutoAcabadoPalete palete = Palete();
        palete.CodigoHuPalete = 900;

        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(palete);

        Assert.Equal(1, gw.Posts);
        Assert.Equal(EstadoPaleteInt012.IndeterminadoTimeout, r.Estado);
        Assert.Equal("UC-FAKE-001", r.UcGerada);
    }

    // ===== D) write gate off → NENHUM POST, feedback explícito, e nem chega ao claim =====
    [Fact]
    public async Task WriteGateOff_ZeroPost_ZeroClaim_MensagemExplicita()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = false };
        FakeExecutor045Int012 ex = new();
        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete());

        Assert.Equal(EstadoPaleteInt012.NaoEnviado, r.Estado);
        Assert.Equal(0, gw.Posts);
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_criar"));
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_claim_envio"));

        ApresentacaoEnvioPaleteInt012 ap = ProdutoAcabadoPaleteEnvioPresenter.Construir(r is null ? null : "PLT-1002024-0002-0003", r);
        Assert.False(ap.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(ap.Mensagem));
    }

    // ===== E) snapshot/preview inválido (caixa não CONFIRMADA_SAP) → zero POST, feedback explícito =====
    [Fact]
    public async Task SnapshotInvalido_ZeroPost_MensagemExplicita()
    {
        FakeGatewayInt012 gw = new() { EnvioAutorizado = true };
        FakeExecutor045Int012 ex = new();
        ResultadoPaleteInt012 r = await Controller(gw, ex).EnviarPaleteInt012Async(Palete(StatusIntegracaoCaixa.ErroSap));

        Assert.Equal(EstadoPaleteInt012.NaoEnviado, r.Estado);
        Assert.Equal(0, gw.Posts);
        Assert.Equal(0, ex.Chamadas("fn_pa_045_palete_criar"));
        Assert.False(string.IsNullOrWhiteSpace(r.MensagemSanitizada));
    }

    // ===== C) presenter: qualquer desfecho pré-claim vira mensagem CONTROLADA e não vazia =====
    [Fact]
    public void Presenter_Confirmado_Sucesso()
    {
        ApresentacaoEnvioPaleteInt012 ap = ProdutoAcabadoPaleteEnvioPresenter.Construir(
            "PLT-1", new ResultadoPaleteInt012 { Estado = EstadoPaleteInt012.Confirmado, UcGerada = "UC-9" });
        Assert.True(ap.Sucesso);
        Assert.Contains("UC-9", ap.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Presenter_NaoEnviado_NaoSucesso_MensagemPreservada()
    {
        ApresentacaoEnvioPaleteInt012 ap = ProdutoAcabadoPaleteEnvioPresenter.Construir(
            "PLT-1", ResultadoPaleteInt012.NaoEnviado("Envio INT012 bloqueado: gate/base/allowlist CPI."));
        Assert.False(ap.Sucesso);
        Assert.Contains("bloqueado", ap.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Presenter_MensagemVazia_UsaPadraoNaoVazio()
    {
        ApresentacaoEnvioPaleteInt012 ap = ProdutoAcabadoPaleteEnvioPresenter.Construir(
            "PLT-1", new ResultadoPaleteInt012 { Estado = EstadoPaleteInt012.NaoEnviado, MensagemSanitizada = "" });
        Assert.False(ap.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(ap.Mensagem)); // nunca "some" — sempre há texto
    }

    [Fact]
    public void Presenter_Excecao_MensagemNaoVazia()
    {
        ApresentacaoEnvioPaleteInt012 ap = ProdutoAcabadoPaleteEnvioPresenter.ParaExcecao("PLT-1");
        Assert.False(ap.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(ap.Mensagem));
    }

    // ===== A) + wiring do handler: cancelar não chama o controller; confirmar chama 1x e apresenta desfecho =====
    [Fact]
    public void Form_Handler_CancelaNaoChama_ConfirmaChamaEApresenta()
    {
        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task IntegrarPaleteSelecionadoAsync(int rowIndex)");

        // A) cancelamento retorna ANTES de chamar o controller.
        int idxGuardCancel = metodo.IndexOf("if (confirmacao != DialogResult.Yes)", StringComparison.Ordinal);
        int idxChamada = metodo.IndexOf("_controller.EnviarPaleteInt012Async(palete)", StringComparison.Ordinal);
        Assert.True(idxGuardCancel >= 0 && idxChamada >= 0 && idxGuardCancel < idxChamada);
        // exatamente 1 chamada ao envio.
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(metodo, @"_controller\.EnviarPaleteInt012Async\(palete\)").Count);
        // desfecho SEMPRE apresentado explicitamente (MessageBox), inclusive na exceção (presenter).
        Assert.Contains("ProdutoAcabadoPaleteEnvioPresenter.Construir(palete.CodigoPaleteLocal, resultado)", metodo, StringComparison.Ordinal);
        Assert.Contains("ProdutoAcabadoPaleteEnvioPresenter.ParaExcecao(palete.CodigoPaleteLocal)", metodo, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(metodo, @"MessageBox\.Show\(apresentacao\.Mensagem").Count);
    }

    // ---------------- fakes ----------------
    private sealed class FakeGatewayInt012 : IProdutoAcabadoPaleteInt012Gateway
    {
        public bool EnvioAutorizado { get; init; }
        public int Posts { get; private set; }
        public Task<ResultadoPaleteInt012> EnviarPaleteAsync(ProdutoAcabadoPaleteRequest requisicao, CancellationToken cancellationToken = default)
        {
            Posts++;
            return Task.FromResult(new ResultadoPaleteInt012
            {
                Estado = EstadoPaleteInt012.Confirmado,
                UcGerada = "UC-FAKE-001",
                StatusBruto = "S",
                MensagemSanitizada = "Palete confirmado no SAP (UC UC-FAKE-001)."
            });
        }
    }

    /// <summary>Executor 045 fake (sem banco): registra chamadas e devolve linhas canônicas por função.</summary>
    private sealed class FakeExecutor045Int012 : IProdutoAcabadoPipeline045Executor
    {
        private readonly Dictionary<string, int> _chamadas = new(StringComparer.Ordinal);
        public bool Disponivel => true;
        public int Chamadas(string funcao) => _chamadas.TryGetValue(funcao, out int n) ? n : 0;

        // GATE 046-AQ-Z2-C: simula fechamento local (registrar_sucesso) não comprovado.
        public bool RegistrarSucessoFalha { get; set; }
        public bool RegistrarSucessoLanca { get; set; }

        public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
        {
            _chamadas[funcao] = Chamadas(funcao) + 1;
            if (funcao == "fn_pa_045_palete_registrar_sucesso")
            {
                if (RegistrarSucessoLanca) { throw new InvalidOperationException("Falha de persistencia do fechamento (simulada)."); }
                if (RegistrarSucessoFalha) { return Task.FromResult<IReadOnlyList<Linha045>>([L((funcao, false))]); }
            }
            IReadOnlyList<Linha045> linhas = funcao switch
            {
                "fn_pa_045_palete_criar" => [L(("codigo_hu_palete", 900L))],
                "fn_pa_045_palete_claim_envio" => [L(("claim_token", Guid.NewGuid()), ("numero_tentativa", 1))],
                _ => [L((funcao, true))], // vincular/registrar_* => bool true
            };
            return Task.FromResult(linhas);
        }

        public Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Linha045>>([]);
        public async Task<T> ExecutarEmTransacaoAsync<T>(Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao, CancellationToken cancellationToken = default)
            => await operacao(new Tx(this), cancellationToken);
        public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(string numeroOrdemProducao, string? terminal, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Linha045>>([]);

        private static Linha045 L(params (string c, object? v)[] cols)
            => new(cols.ToDictionary(c => c.c, c => c.v, StringComparer.OrdinalIgnoreCase));

        private sealed class Tx(FakeExecutor045Int012 dono) : IExecutorFuncoes045Transacional
        {
            public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken ct = default)
                => dono.ExecutarFuncaoAsync(funcao, parametros, ct);
        }
    }

    private sealed class ProductionOrderSapFake : IProductionOrderSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;
        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoConfigurado("Fake sem consulta SAP."));
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int prof = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') { prof++; }
            else if (fonte[i] == '}') { prof--; if (prof == 0) { return fonte[inicio..(i + 1)]; } }
        }
        return fonte[inicio..];
    }

    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}


