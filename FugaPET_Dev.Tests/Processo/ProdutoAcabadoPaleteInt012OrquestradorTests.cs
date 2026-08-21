using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// FAST-TRACK HML REV7: wiring PRODUTIVO do palete â†’ INT012. Prova vincular â†’ claim â†’ UM POST_FORMACAO â†’
/// registrar (sucesso/erro/timeout), fail-closed por packaging/allowlist/claim, zero retry. Fakes: operaÃ§Ãµes 045
/// + gateway INT012 (sem banco/CPI reais). O critÃ©rio de sucesso 2xx+Status S+UC+sem Tipo E Ã© do gateway (testado
/// Ã  parte); aqui mapeia-se cada EstadoPaleteInt012 para o registro correto.
/// </summary>
public sealed class ProdutoAcabadoPaleteInt012OrquestradorTests
{
    private const long Usuario = 42;
    private const string Terminal = "TERM-01";
    private const long CodigoPaleteCriado = 900;

    private sealed class FakeOpsPalete : IProdutoAcabadoPipeline045Operacoes
    {
        public bool SuportaPersistenciaDefinitiva => true;
        public List<string> Log { get; } = [];
        public int Vinculos { get; private set; }
        public bool ClaimObtido { get; set; } = true;
        public long? CodigoCriado { get; set; } = CodigoPaleteCriado;
        public List<long> PaletesUsados { get; } = [];
        public string? PrimeiroParametroCriacao { get; private set; }
        public bool VinculoOk { get; set; } = true;
        public Guid ClaimTokenRetornado { get; set; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        public int TentativaRetornada { get; set; } = 1;
        public Guid? ClaimEsperadoNoFechamento { get; set; }
        public Guid? ClaimSucessoRecebido { get; private set; }
        public int? TentativaSucessoRecebida { get; private set; }

        // GATE 047-I-B: controla o desfecho de registrar_sucesso (fechamento local) para reproduzir os cenarios.
        public enum ComportamentoSucesso { RetornaTrue, RetornaFalse, Lanca }
        public ComportamentoSucesso Sucesso { get; set; } = ComportamentoSucesso.RetornaTrue;

        public Task<long?> CriarPaleteAsync(
            string codigoPaleteLocal,
            string plant,
            string storageLocation,
            decimal pesoBrutoKg,
            decimal pesoLiquidoKg,
            decimal taraKg,
            long usuario,
            string terminal,
            CancellationToken ct = default)
        {
            PrimeiroParametroCriacao = codigoPaleteLocal;
            Log.Add("criar");
            return Task.FromResult(CodigoCriado);
        }

        public Task<bool> VincularCaixaPaleteAsync(long p, long cx, int seq, long u, string t, CancellationToken ct = default)
        { Vinculos++; PaletesUsados.Add(p); Log.Add($"vincular:{p}:{cx}:{seq}"); return Task.FromResult(VinculoOk); }
        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long p, string r, string e, long u, string t, CancellationToken ct = default)
        { PaletesUsados.Add(p); Log.Add($"claim:{p}"); return Task.FromResult(ClaimObtido ? new ResultadoClaim045(true, ClaimTokenRetornado, TentativaRetornada) : ResultadoClaim045.NaoObtido); }
        public Task<bool> RegistrarSucessoPaleteAsync(long p, int h, string uc, string r, string e, long u, string t, CancellationToken ct = default)
            => RegistrarSucessoPaleteAsync(p, 0, Guid.Empty, h, uc, r, e, u, t, ct);
        public Task<bool> RegistrarSucessoPaleteAsync(long p, int tentativa, Guid claimToken, int h, string uc, string r, string e, long u, string t, CancellationToken ct = default)
        {
            TentativaSucessoRecebida = tentativa;
            ClaimSucessoRecebido = claimToken;
            PaletesUsados.Add(p); Log.Add($"sucesso:{p}");
            if (ClaimEsperadoNoFechamento is Guid esperado && esperado != claimToken) { return Task.FromResult(false); }
            return Sucesso switch
            {
                ComportamentoSucesso.RetornaFalse => Task.FromResult(false),
                ComportamentoSucesso.Lanca => throw new InvalidOperationException("Falha simulada no fechamento local (registrar_sucesso)."),
                _ => Task.FromResult(true)
            };
        }
        public Task<bool> RegistrarErroPaleteAsync(long p, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default)
            => RegistrarErroPaleteAsync(p, 0, Guid.Empty, h, r, erro, e, u, t, ct);
        public Task<bool> RegistrarErroPaleteAsync(long p, int tentativa, Guid claimToken, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default)
        { PaletesUsados.Add(p); Log.Add($"erro:{p}"); return Task.FromResult(true); }
        public Task<bool> RegistrarTimeoutPaleteAsync(long p, string erro, string e, long u, string t, CancellationToken ct = default)
            => RegistrarTimeoutPaleteAsync(p, 0, Guid.Empty, erro, e, u, t, ct);
        public Task<bool> RegistrarTimeoutPaleteAsync(long p, int tentativa, Guid claimToken, string erro, string e, long u, string t, CancellationToken ct = default)
        { PaletesUsados.Add(p); Log.Add($"timeout:{p}"); return Task.FromResult(true); }

        // NÃ£o usados por este orquestrador â€” mÃ­nimos.
        public Task<bool> IniciarFluxoAsync(long c, long u, string t, string? o = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> PrepararEtapaAsync(long c, string et, string p, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long c, string et, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoEtapaAsync(long c, string et, int h, string md, string y, string r, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroEtapaAsync(long c, string et, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutEtapaAsync(long c, string et, string erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long c, string et, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long c, string et, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long c, string et, string res, int? h, string? md, string? y, string r, string? erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long c, string et, string tipo, string m, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(new ResultadoClaim045(true, Guid.NewGuid(), 1));
        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
    }

    private sealed class FakeInt012 : IProdutoAcabadoPaleteInt012Gateway
    {
        private readonly ResultadoPaleteInt012 _r;
        public int Posts { get; private set; }
        public FakeInt012(ResultadoPaleteInt012 r, bool autorizado = true) { _r = r; EnvioAutorizado = autorizado; }
        public bool EnvioAutorizado { get; }
        public Task<ResultadoPaleteInt012> EnviarPaleteAsync(ProdutoAcabadoPaleteRequest req, CancellationToken ct = default)
        { Posts++; return Task.FromResult(_r); }
    }

    private static ResultadoPaleteInt012 Estado(EstadoPaleteInt012 e, string? uc = "300099999", int? http = 200)
        => new() { Estado = e, UcGerada = uc, HttpStatus = http, MensagemSanitizada = e.ToString() };

    private static ProdutoAcabadoPaleteRequest Req(string? packaging = "PACK_TEST_001", bool comHu = true) => new()
    {
        HandlingUnitExternalID = "PLT-1", GrossWeight = 20m, NetWeight = 18m, TareWeight = 2m, WeightUnit = "KG",
        Plant = "3007", StorageLocation = "PA01", PackagingMaterial = packaging!,
        HandlingUnitItems = [new() { HandlingUnit = comHu ? "300010001" : string.Empty }]
    };

    private static Task<ResultadoPalete045> Executar(FakeOpsPalete ops, FakeInt012 gw, ProdutoAcabadoPaleteRequest? req = null)
        => new ProdutoAcabadoPaleteInt012Orquestrador(ops, gw)
            .ExecutarAsync([-1, -2], req ?? Req(), Usuario, Terminal);

    [Fact]
    public async Task Sucesso_Vincula_Claim_Post_RegistrarSucesso()
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));
        ResultadoPalete045 r = await Executar(ops, gw);
        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal("300099999", r.UcGerada);
        Assert.Equal(2, ops.Vinculos);
        Assert.Equal(1, gw.Posts);
        Assert.Equal(["criar", "vincular:900:-1:1", "vincular:900:-2:2", "claim:900", "sucesso:900"], ops.Log);
        Assert.All(ops.PaletesUsados, p => Assert.Equal(CodigoPaleteCriado, p));
    }

    [Fact]
    public async Task FalhaNaCriacao_ImpedeVinculacaoClaimEPost()
    {
        FakeOpsPalete ops = new() { CodigoCriado = null };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.False(r.Executou);
        Assert.Equal(["criar"], ops.Log);
        Assert.Equal(0, ops.Vinculos);
        Assert.Equal(0, gw.Posts);
    }
    [Fact]
    public async Task CreateUsaPackagingMaterial()
    {
        FakeOpsPalete ops = new();
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw, Req(packaging: "PACK_REAL_045"));

        Assert.True(r.Executou);
        Assert.Equal("PACK_REAL_045", ops.PrimeiroParametroCriacao);
        Assert.NotEqual("PLT-1", ops.PrimeiroParametroCriacao);
    }

    [Fact]
    public async Task VinculoFalse_ZeroClaim_ZeroPost()
    {
        FakeOpsPalete ops = new() { VinculoOk = false };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.False(r.Executou);
        Assert.Equal(1, ops.Vinculos);
        Assert.DoesNotContain(ops.Log, item => item.StartsWith("claim:", StringComparison.Ordinal));
        Assert.Equal(0, gw.Posts);
    }

    [Fact]
    public async Task ClaimNaoObtido_ZeroPost_NaoRegistra()
    {
        FakeOpsPalete ops = new() { ClaimObtido = false }; FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));
        ResultadoPalete045 r = await Executar(ops, gw);
        Assert.False(r.Executou);
        Assert.Equal(0, gw.Posts);
        Assert.DoesNotContain("sucesso", ops.Log);
        Assert.DoesNotContain("erro", ops.Log);
    }

    [Theory]
    [InlineData(EstadoPaleteInt012.ErroStatus)]        // HTTP200 + Status=E / mensagem Tipo=E
    [InlineData(EstadoPaleteInt012.ContratoStatusPendente)] // HTTP200 + UC vazia / sem Status S
    [InlineData(EstadoPaleteInt012.ErroHttp)]          // HTTP 500
    public async Task ResultadosDeErro_RegistramErro_UmPost(EstadoPaleteInt012 estado)
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(estado, http: estado == EstadoPaleteInt012.ErroHttp ? 500 : 200));
        ResultadoPalete045 r = await Executar(ops, gw);
        Assert.True(r.Executou);
        Assert.Equal(1, gw.Posts);
        Assert.Contains(ops.Log, item => item.StartsWith("erro:", StringComparison.Ordinal));
        Assert.DoesNotContain("sucesso", ops.Log);
    }

    [Fact]
    public async Task Timeout_RegistraTimeout_ZeroRetry()
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.IndeterminadoTimeout, uc: null, http: null));
        ResultadoPalete045 r = await Executar(ops, gw);
        Assert.True(r.Executou);
        Assert.Equal(1, gw.Posts); // UM POST, zero retry cego
        Assert.Contains(ops.Log, item => item.StartsWith("timeout:", StringComparison.Ordinal));
        Assert.DoesNotContain("sucesso", ops.Log);
    }

    [Fact]
    public async Task CaixaSemHu_ZeroPost_FailClosed()
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));
        ResultadoPalete045 r = await Executar(ops, gw, Req(comHu: false));
        Assert.False(r.Executou);
        Assert.Equal(0, gw.Posts);
        Assert.Empty(ops.Log); // nem vincula, nem claim
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PackagingMaterialNuloOuVazio_FailClosed_ZeroPost(string? packaging)
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));
        ResultadoPalete045 r = await Executar(ops, gw, Req(packaging: packaging));
        Assert.False(r.Executou);
        Assert.Equal(0, gw.Posts);
        Assert.Empty(ops.Log);
    }

    [Fact]
    public async Task PackagingMaterialPallet01_NaoBloqueiaPorPlaceholder_NaoSubstitui()
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));
        ResultadoPalete045 r = await Executar(ops, gw, Req(packaging: "PALLET01"));
        Assert.True(r.Executou);
        Assert.Equal("PALLET01", ops.PrimeiroParametroCriacao);
        Assert.Equal(1, gw.Posts);
        Assert.Contains("claim:900", ops.Log);
    }

    [Fact]
    public async Task PackagingMaterialOutroCodigoValido_NaoBloqueiaPorNome()
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));
        ResultadoPalete045 r = await Executar(ops, gw, Req(packaging: "PACK_REAL_999"));
        Assert.True(r.Executou);
        Assert.Equal("PACK_REAL_999", ops.PrimeiroParametroCriacao);
        Assert.Equal(1, gw.Posts);
    }


    [Fact]
    public void Store_ClaimPalete_LeTentativasPluralDaCapability()
    {
        string src = File.ReadAllText(CaminhoFonte("Servicos", "Operacao", "ProdutoAcabadoPipelinePostgresStore.cs"));
        Assert.Contains("ObterInt(\"tentativas\")", src, StringComparison.Ordinal);
    }
    [Fact]
    public async Task AllowlistCpiAusente_GatewayNaoAutorizado_FailClosed_ZeroPost()
    {
        FakeOpsPalete ops = new(); FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado), autorizado: false);
        ResultadoPalete045 r = await Executar(ops, gw);
        Assert.False(r.Executou);
        Assert.Equal(0, gw.Posts);
        Assert.Empty(ops.Log);
    }

    [Fact]
    public void Recovery_NaoPostaNemIncrementaTentativaHttp()
    {
        // Â§9: recovery Ã© aquisiÃ§Ã£o de capability no store (fn_pa_045_palete_adquirir_recovery), NUNCA POST.
        // O orquestrador de POST nÃ£o expÃµe recovery; o gateway INT012 nÃ£o Ã© tocado num fluxo de recovery.
        string src = File.ReadAllText(CaminhoFonte("Servicos", "Operacao", "ProdutoAcabadoPaleteInt012Orquestrador.cs"));
        Assert.DoesNotContain("AdquirirRecoveryPalete", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Reassumir", src, StringComparison.Ordinal);
    }

    // ---------------- GATE 047-I-B: reproducao do fechamento pos-POST ----------------

    [Fact] // POST 2xx + Status S + UC + registrar_sucesso=TRUE => Confirmado (fechamento local comprovado)
    public async Task Fechamento_RegistrarSucessoTrue_ResultaConfirmado()
    {
        FakeOpsPalete ops = new() { Sucesso = FakeOpsPalete.ComportamentoSucesso.RetornaTrue };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal("300099999", r.UcGerada);
        Assert.Contains("sucesso:900", ops.Log);
        Assert.Equal(1, gw.Posts);
    }

    [Fact] // registrar_sucesso=FALSE => Indeterminado, UC preservada, ZERO reenvio automatico
    public async Task Fechamento_RegistrarSucessoFalse_ResultaIndeterminado_SemReenvio()
    {
        FakeOpsPalete ops = new() { Sucesso = FakeOpsPalete.ComportamentoSucesso.RetornaFalse };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.IndeterminadoTimeout, r.Estado);
        Assert.Equal("300099999", r.UcGerada); // UC preservada para reconciliacao
        Assert.Equal(1, gw.Posts);             // UM POST, sem reenvio automatico
    }

    [Fact] // excecao no fechamento => Indeterminado (nao mascara sucesso), ZERO reenvio automatico
    public async Task Fechamento_RegistrarSucessoLanca_ResultaIndeterminado_SemReenvio()
    {
        FakeOpsPalete ops = new() { Sucesso = FakeOpsPalete.ComportamentoSucesso.Lanca };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.IndeterminadoTimeout, r.Estado);
        Assert.Equal(1, gw.Posts);
    }


    [Fact]
    public async Task Fechamento_UsaTentativaEClaimRetornadosPeloClaimAtual()
    {
        Guid claimAtual = Guid.Parse("d5b3e438-6914-49b8-97f6-b6187c6029ef");
        FakeOpsPalete ops = new()
        {
            TentativaRetornada = 1,
            ClaimTokenRetornado = claimAtual,
            ClaimEsperadoNoFechamento = claimAtual
        };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal(1, ops.TentativaSucessoRecebida);
        Assert.Equal(claimAtual, ops.ClaimSucessoRecebido);
        Assert.NotEqual(Guid.Empty, ops.ClaimSucessoRecebido);
        Assert.Equal(1, gw.Posts);
    }

    [Fact]
    public async Task Fechamento_ClaimDivergenteNaoConsideraSucesso_ZeroRetry()
    {
        FakeOpsPalete ops = new()
        {
            TentativaRetornada = 1,
            ClaimTokenRetornado = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ClaimEsperadoNoFechamento = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        };
        FakeInt012 gw = new(Estado(EstadoPaleteInt012.Confirmado));

        ResultadoPalete045 r = await Executar(ops, gw);

        Assert.True(r.Executou);
        Assert.Equal(EstadoPaleteInt012.IndeterminadoTimeout, r.Estado);
        Assert.Equal(1, ops.TentativaSucessoRecebida);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), ops.ClaimSucessoRecebido);
        Assert.Equal(1, gw.Posts);
    }
    [Fact] // GATE 047-I-B §5: diagnostico de fechamento presente e sanitizado; evidencia NAO descartada; sem reassuncao
    public void Fechamento_DiagnosticoSanitizado_Presente_SemReassuncao()
    {
        string src = File.ReadAllText(CaminhoFonte("Servicos", "Operacao", "ProdutoAcabadoPaleteInt012Orquestrador.cs"));

        foreach (string campo in new[]
        {
            "FECHAMENTO_LOCAL_INICIO_UTC", "TENTATIVA_REAL", "CLAIM_MATCH_CONTEXT", "LEASE_RESTANTE_MS_ESTIMADO",
            "HTTP_PRESENTE", "HU_PAI_PRESENTE", "RESPONSE_JSON_OBJECT", "REGISTRAR_SUCESSO_RESULTADO",
            "SQLSTATE", "EXCEPTION_TYPE", "MENSAGEM_SANITIZADA", "FECHAMENTO_LOCAL_FIM_UTC"
        })
        {
            Assert.Contains(campo, src, StringComparison.Ordinal);
        }

        // a excecao do fechamento agora e capturada (tipo/SQLSTATE/mensagem), nao mais descartada por catch vazio
        Assert.Contains("ExtrairSqlState", src, StringComparison.Ordinal);
        Assert.Contains("catch (Exception ex)", src, StringComparison.Ordinal);
        // guardrail preservado: o orquestrador de POST NAO faz reassuncao/recovery
        Assert.DoesNotContain("Reassumir", src, StringComparison.Ordinal);
        Assert.DoesNotContain("AdquirirRecoveryPalete", src, StringComparison.Ordinal);
        // sem logar segredo
        Assert.DoesNotContain("Authorization=", src, StringComparison.Ordinal);
    }

    private static string CaminhoFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return Path.Combine(dir, Path.Combine(partes));
    }
}







