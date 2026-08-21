using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// FAST-TRACK HML REV6: prova o WIRING PRODUTIVO do orquestrador 045 â€” para cada etapa executa
/// iniciar â†’ preparar â†’ claim â†’ POST â†’ registrar_(sucesso|erro|timeout), com ordem estrita 261â†’101â†’HU,
/// sem claim â‡’ sem POST, timeout â‡’ zero retry e para. Fakes: operaÃ§Ãµes 045 + gateways (sem banco/SAP reais).
/// </summary>
public sealed class ProdutoAcabadoPipeline045OrquestradorTests
{
    private const long Usuario = 42;
    private const string Terminal = "TERM-01";

    // -------- fake das operaÃ§Ãµes 045 (registra a sequÃªncia exata) --------
    private sealed class FakeOps045 : IProdutoAcabadoPipeline045Operacoes
    {
        public bool SuportaPersistenciaDefinitiva => true;
        public List<string> Log { get; } = [];
        public HashSet<string> ClaimsNegados { get; } = [];
        // Â§5/Â§6: etapas cuja persistÃªncia 045 NÃƒO Ã© comprovada (registrar_* retorna false).
        public HashSet<string> SucessoNaoPersistido { get; } = [];
        public HashSet<string> SucessoComExcecao { get; } = [];
        public HashSet<string> TimeoutNaoPersistido { get; } = [];
        public HashSet<string> TimeoutComExcecao { get; } = [];
        public bool? UltimoTimeoutTokenCancelado { get; private set; }
        public Guid? UltimoTimeoutClaimToken { get; private set; }
        public int? UltimaTimeoutTentativa { get; private set; }
        public Guid? UltimoErroClaimToken { get; private set; }
        public int? UltimaErroTentativa { get; private set; }
        public Guid? UltimoSucessoClaimToken { get; private set; }
        public int? UltimaSucessoTentativa { get; private set; }
        public Dictionary<string, Guid> ErroClaimTokensPorEtapa { get; } = [];
        public Dictionary<string, int> ErroTentativasPorEtapa { get; } = [];
        public Dictionary<string, Guid> SucessoClaimTokensPorEtapa { get; } = [];
        public Dictionary<string, int> SucessoTentativasPorEtapa { get; } = [];
        public Dictionary<string, bool> SucessoTokenCanceladoPorEtapa { get; } = [];
        public Dictionary<string, ResultadoClaim045> ClaimsPorEtapa { get; } = [];
        public Task<bool> IniciarFluxoAsync(long c, long u, string t, string? origem = null, CancellationToken ct = default)
        { Log.Add("iniciar"); return Task.FromResult(true); }
        public Task<bool> PrepararEtapaAsync(long c, string etapa, string p, long u, string t, CancellationToken ct = default)
        { Log.Add($"preparar:{etapa}"); return Task.FromResult(true); }
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long c, string etapa, long u, string t, CancellationToken ct = default)
        {
            Log.Add($"claim:{etapa}");
            if (ClaimsNegados.Contains(etapa)) { return Task.FromResult(ResultadoClaim045.NaoObtido); }
            if (!ClaimsPorEtapa.TryGetValue(etapa, out ResultadoClaim045? claim))
            {
                claim = new ResultadoClaim045(true, Guid.NewGuid(), 1);
                ClaimsPorEtapa[etapa] = claim;
            }
            return Task.FromResult(claim);
        }
        public Task<bool> RegistrarSucessoEtapaAsync(long c, string etapa, int h, string md, string y, string r, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"sucesso-cache:{etapa}"); return Task.FromResult(!SucessoNaoPersistido.Contains(etapa)); }
        public Task<bool> RegistrarSucessoEtapaAsync(long c, string etapa, int tentativa, Guid claimToken, int h, string md, string y, string r, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"sucesso:{etapa}"); UltimaSucessoTentativa = tentativa; UltimoSucessoClaimToken = claimToken; SucessoTentativasPorEtapa[etapa] = tentativa; SucessoClaimTokensPorEtapa[etapa] = claimToken; SucessoTokenCanceladoPorEtapa[etapa] = ct.IsCancellationRequested; if (SucessoComExcecao.Contains(etapa)) { throw new InvalidOperationException("falha registrar sucesso"); } return Task.FromResult(!SucessoNaoPersistido.Contains(etapa)); }
        public Task<bool> RegistrarErroEtapaAsync(long c, string etapa, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"erro-cache:{etapa}"); return Task.FromResult(true); }
        public Task<bool> RegistrarErroEtapaAsync(long c, string etapa, int tentativa, Guid claimToken, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"erro:{etapa}"); UltimaErroTentativa = tentativa; UltimoErroClaimToken = claimToken; ErroTentativasPorEtapa[etapa] = tentativa; ErroClaimTokensPorEtapa[etapa] = claimToken; return Task.FromResult(true); }
        public Task<bool> RegistrarTimeoutEtapaAsync(long c, string etapa, string erro, string e, long u, string t, CancellationToken ct = default)
        { Log.Add($"timeout:{etapa}"); UltimoTimeoutTokenCancelado = ct.IsCancellationRequested; if (TimeoutComExcecao.Contains(etapa)) { throw new InvalidOperationException("falha registrar timeout"); } return Task.FromResult(!TimeoutNaoPersistido.Contains(etapa)); }
        public Task<bool> RegistrarTimeoutEtapaAsync(long c, string etapa, int tentativa, Guid claimToken, string erro, string e, long u, string t, CancellationToken ct = default)
        {
            Log.Add($"timeout:{etapa}");
            UltimoTimeoutTokenCancelado = ct.IsCancellationRequested;
            UltimoTimeoutClaimToken = claimToken;
            UltimaTimeoutTentativa = tentativa;
            if (TimeoutComExcecao.Contains(etapa)) { throw new InvalidOperationException("falha registrar timeout"); }
            return Task.FromResult(!TimeoutNaoPersistido.Contains(etapa));
        }

        // NÃ£o usados pelo ExecutarAsync (recovery/palete/snapshot): implementaÃ§Ã£o mÃ­nima.
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long c, string etapa, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long c, string etapa, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long c, string etapa, string res, int? h, string? md, string? y, string r, string? erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long c, string etapa, string tipo, string m, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<long?> CriarPaleteAsync(string cp, string plant, string sl, decimal pb, decimal pl, decimal tara, long u, string t, CancellationToken ct = default) => Task.FromResult<long?>(1);
        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long c, string r, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long c, long u, string t, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoPaleteAsync(long c, int h, string uc, string r, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroPaleteAsync(long c, int h, string r, string erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutPaleteAsync(long c, string erro, string e, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> VincularCaixaPaleteAsync(long c, long cx, int seq, long u, string t, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long c, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
    }

    private sealed class Gw261 : IProdutoAcabadoMovimento261Gateway
    {
        private readonly ResultadoMovimentoSap _r; public int Chamadas { get; private set; }
        public ProdutoAcabadoMovimento261Command? UltimoComando { get; private set; }
        public Gw261(ResultadoMovimentoSap r) => _r = r;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento261Command c, CancellationToken ct = default) { Chamadas++; UltimoComando = c; return Task.FromResult(_r); }
    }
    private sealed class Gw261Excecao : IProdutoAcabadoMovimento261Gateway
    {
        private readonly Func<Exception> _criarExcecao;
        public int Chamadas { get; private set; }
        public Gw261Excecao() : this(() => new InvalidOperationException("falha pos-claim 261")) { }
        public Gw261Excecao(Func<Exception> criarExcecao) => _criarExcecao = criarExcecao;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento261Command c, CancellationToken ct = default) { Chamadas++; throw _criarExcecao(); }
    }
    private sealed class Gw261CancelaERetornaSucesso : IProdutoAcabadoMovimento261Gateway
    {
        private readonly CancellationTokenSource _cts;
        public int Chamadas { get; private set; }
        public Gw261CancelaERetornaSucesso(CancellationTokenSource cts) => _cts = cts;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento261Command c, CancellationToken ct = default)
        {
            Chamadas++;
            _cts.Cancel();
            return Task.FromResult(Doc());
        }
    }
    private sealed class Gw261CancelaERetornaTimeout : IProdutoAcabadoMovimento261Gateway
    {
        private readonly CancellationTokenSource _cts;
        public int Chamadas { get; private set; }
        public Gw261CancelaERetornaTimeout(CancellationTokenSource cts) => _cts = cts;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento261Command c, CancellationToken ct = default)
        {
            Chamadas++;
            _cts.Cancel();
            return Task.FromResult(ResultadoMovimentoSap.Indeterminado("timeout 261 apos claim"));
        }
    }
    private sealed class Gw101 : IProdutoAcabadoMovimento101Gateway
    {
        private readonly ResultadoMovimentoSap _r; public int Chamadas { get; private set; }
        public Gw101(ResultadoMovimentoSap r) => _r = r;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento101Command c, CancellationToken ct = default) { Chamadas++; return Task.FromResult(_r); }
    }
    private sealed class Gw101CancelaERetornaSucesso : IProdutoAcabadoMovimento101Gateway
    {
        private readonly CancellationTokenSource _cts;
        public int Chamadas { get; private set; }
        public Gw101CancelaERetornaSucesso(CancellationTokenSource cts) => _cts = cts;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento101Command c, CancellationToken ct = default)
        {
            Chamadas++;
            _cts.Cancel();
            return Task.FromResult(Doc());
        }
    }
    private sealed class Gw101Excecao : IProdutoAcabadoMovimento101Gateway
    {
        private readonly Func<Exception> _criarExcecao;
        public int Chamadas { get; private set; }
        public Gw101Excecao() : this(() => new InvalidOperationException("falha pos-claim 101")) { }
        public Gw101Excecao(Func<Exception> criarExcecao) => _criarExcecao = criarExcecao;
        public Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento101Command c, CancellationToken ct = default) { Chamadas++; throw _criarExcecao(); }
    }
    private sealed class GwHu : IProdutoAcabadoHuEnvio
    {
        private readonly StatusIntegracaoCaixa _e; public int Chamadas { get; private set; }
        public GwHu(StatusIntegracaoCaixa e) => _e = e;
        public bool EnvioAutorizado => true;
        public Task<StatusIntegracaoCaixa> EnviarHuAsync(long c, long u, string t, CancellationToken ct = default) { Chamadas++; return Task.FromResult(_e); }
    }

    public static TheoryData<string, Func<Exception>> ExcecaoGateway261 => new()
    {
        { "OperationCanceledException", () => new OperationCanceledException("cancelamento pos-claim 261") },
        { "TaskCanceledException", () => new TaskCanceledException("task cancelada pos-claim 261") },
        { "HttpRequestException", () => new HttpRequestException("falha transporte pos-claim 261") }
    };

    public static TheoryData<string, Func<Exception>> ExcecaoGateway101 => new()
    {
        { "OperationCanceledException", () => new OperationCanceledException("cancelamento pos-claim 101") },
        { "TaskCanceledException", () => new TaskCanceledException("task cancelada pos-claim 101") }
    };
    private static ResultadoMovimentoSap Doc() => ResultadoMovimentoSap.Confirmado("490000123", "2026", 201);
    private static ProdutoAcabadoMovimento261Command Cmd261() => new()
    { NumeroOrdem = "1001951", CorrelationId = "PA-1-1", PostingDate = new DateTime(2026, 8, 11), DocumentDate = new DateTime(2026, 8, 11),
      Itens = [new() { Material = "M", Plant = "3007", StorageLocation = "PP01", Quantidade = 5m, Unidade = "KG", Reservation = "10", ReservationItem = "1" }] };
    private static ProdutoAcabadoMovimento101Command Cmd101() => new()
    { NumeroOrdem = "1001951", Material = "4000108", Plant = "3007", StorageLocation = "PP02", ManufacturingOrderItem = "0001", QuantityInEntryUnit = "60", EntryUnit = "KG", Batch = "L1", PostingDate = new DateTime(2026, 8, 11), DocumentDate = new DateTime(2026, 8, 11) };

    private static ProdutoAcabadoPipeline045Orquestrador Orq(FakeOps045 ops, Gw261 a, Gw101 b, GwHu h)
        => new(ops, a, b, h);

    [Fact]
    public async Task Sucesso_SequenciaExata_261_Depois_101_Depois_HU()
    {
        FakeOps045 ops = new(); Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);

        Assert.Equal(EtapaPipelineProdutoAcabado.Concluido, r.UltimaEtapa);
        Assert.Equal(1, a.Chamadas); Assert.Equal(1, b.Chamadas); Assert.Equal(1, h.Chamadas);
        Assert.Equal(
            ["iniciar", "preparar:261", "claim:261", "sucesso:261", "preparar:101", "claim:101", "sucesso:101"],
            ops.Log);
    }

    [Theory]
    [MemberData(nameof(ExcecaoGateway261))]
    public async Task ExcecaoPosClaim261_RegistraIndeterminado_NaoChama101_NemHU(string _, Func<Exception> criarExcecao)
    {
        FakeOps045 ops = new(); Gw261Excecao a = new(criarExcecao); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, a.Chamadas);
        Assert.Equal(0, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.Contains("timeout:261", ops.Log);
        Assert.Equal(false, ops.UltimoTimeoutTokenCancelado);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
        Assert.Contains("reconcilia", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(ExcecaoGateway101))]
    public async Task ExcecaoPosClaim101_RegistraIndeterminado_NaoChamaHU(string _, Func<Exception> criarExcecao)
    {
        FakeOps045 ops = new(); Gw261 a = new(Doc()); Gw101Excecao b = new(criarExcecao); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.Contains("timeout:101", ops.Log);
        Assert.Equal(false, ops.UltimoTimeoutTokenCancelado);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento101, r.UltimaEtapa);
    }

    [Fact]
    public async Task CancellationTokenChamadorCanceladoDepoisDoClaim_PersistenciaUsaTokenNaoCancelado()
    {
        using CancellationTokenSource cts = new();
        FakeOps045 ops = new();
        Gw261Excecao a = new(() => { cts.Cancel(); return new OperationCanceledException("cancelado apos claim", cts.Token); });
        Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        ResultadoPipelineProdutoAcabado r = await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal, cts.Token);

        Assert.Equal(1, a.Chamadas);
        Assert.Equal(0, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.Contains("timeout:261", ops.Log);
        Assert.Equal(false, ops.UltimoTimeoutTokenCancelado);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
    }

    [Fact]
    public async Task FalhaPersistirIndeterminado_AposExcecao261_FailClosed_ZeroAvanco()
    {
        FakeOps045 ops = new(); ops.TimeoutComExcecao.Add("261");
        Gw261Excecao a = new(); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(0, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
        Assert.Contains("tente enviar novamente", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Claim045ComCorrelation_PropagaParaAdapter261_EConstrucaoNaoBloqueiaDependenciaGaia()
    {
        Guid token = Guid.NewGuid();
        const string correlation = "PA045-CORR-CAIXA-18-T1";
        FakeOps045 ops = new();
        ops.ClaimsPorEtapa["261"] = new ResultadoClaim045(true, token, 1, correlation);
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        await Orq(ops, a, b, h).ExecutarAsync(18, Cmd261() with { CorrelationId = string.Empty }, Cmd101(), Usuario, Terminal);

        Assert.Equal(1, a.Chamadas);
        Assert.NotNull(a.UltimoComando);
        Assert.Equal(correlation, a.UltimoComando!.CorrelationId);
        Assert.DoesNotContain("DEPENDENCIA_GAIA", a.UltimoComando.CorrelationId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erro261SemCacheVolatil_UsaTentativaEClaimTokenExplicitos_Zero101ZeroHu()
    {
        Guid token = Guid.NewGuid();
        FakeOps045 ops = new();
        ops.ClaimsPorEtapa["261"] = new ResultadoClaim045(true, token, 1, "PA045-ERRO-1");
        Gw261 a = new(ResultadoMovimentoSap.Erro("erro funcional", 400)); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        await Orq(ops, a, b, h).ExecutarAsync(18, Cmd261() with { CorrelationId = string.Empty }, Cmd101(), Usuario, Terminal);

        Assert.Equal(token, ops.ErroClaimTokensPorEtapa["261"]);
        Assert.Equal(1, ops.ErroTentativasPorEtapa["261"]);
        Assert.Equal(0, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.DoesNotContain("erro-cache:261", ops.Log);
    }

    [Fact]
    public async Task Sucesso261SemCacheVolatil_UsaTentativaEClaimTokenExplicitos_AntesDo101()
    {
        Guid token = Guid.NewGuid();
        FakeOps045 ops = new();
        ops.ClaimsPorEtapa["261"] = new ResultadoClaim045(true, token, 1, "PA045-SUCESSO-1");
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        await Orq(ops, a, b, h).ExecutarAsync(18, Cmd261() with { CorrelationId = string.Empty }, Cmd101(), Usuario, Terminal);

        Assert.Equal(token, ops.SucessoClaimTokensPorEtapa["261"]);
        Assert.Equal(1, ops.SucessoTentativasPorEtapa["261"]);
        Assert.Equal(1, b.Chamadas);
        Assert.DoesNotContain("sucesso-cache:261", ops.Log);
    }
    [Fact]
    public async Task Sucesso261ComTokenChamadorCanceladoDepoisDoSap_RegistrarSucessoUsaTokenNaoCancelado()
    {
        using CancellationTokenSource cts = new();
        Guid token = Guid.NewGuid();
        FakeOps045 ops = new();
        ops.ClaimsPorEtapa["261"] = new ResultadoClaim045(true, token, 1, "PA045-SUCESSO-261");
        Gw261CancelaERetornaSucesso a = new(cts); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(18, Cmd261(), Cmd101(), Usuario, Terminal, cts.Token);

        Assert.Equal(1, a.Chamadas);
        Assert.False(ops.SucessoTokenCanceladoPorEtapa["261"]);
        Assert.Equal(token, ops.SucessoClaimTokensPorEtapa["261"]);
        Assert.Equal(1, ops.SucessoTentativasPorEtapa["261"]);
        Assert.Equal(1, b.Chamadas);
    }

    [Fact]
    public async Task Sucesso101ComTokenChamadorCanceladoDepoisDoSap_RegistrarSucessoUsaTokenNaoCancelado()
    {
        using CancellationTokenSource cts = new();
        Guid token = Guid.NewGuid();
        FakeOps045 ops = new();
        ops.ClaimsPorEtapa["101"] = new ResultadoClaim045(true, token, 2, "PA045-SUCESSO-101");
        Gw261 a = new(Doc()); Gw101CancelaERetornaSucesso b = new(cts); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(18, Cmd261(), Cmd101(), Usuario, Terminal, cts.Token);

        Assert.Equal(1, b.Chamadas);
        Assert.False(ops.SucessoTokenCanceladoPorEtapa["101"]);
        Assert.Equal(token, ops.SucessoClaimTokensPorEtapa["101"]);
        Assert.Equal(2, ops.SucessoTentativasPorEtapa["101"]);
        Assert.Equal(1, h.Chamadas);
    }

    [Fact]
    public async Task RegistrarSucesso261ComExcecao_ZeroPOST101_ZeroHU()
    {
        FakeOps045 ops = new(); ops.SucessoComExcecao.Add("261");
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        ResultadoPipelineProdutoAcabado r = await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);

        Assert.Equal(1, a.Chamadas);
        Assert.Equal(0, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
        Assert.Contains("falha em registrar_sucesso", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task Erro261_RegistraErro_NaoChama101_NemHU()
    {
        FakeOps045 ops = new(); Gw261 a = new(ResultadoMovimentoSap.Erro("400", 400)); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(0, b.Chamadas); Assert.Equal(0, h.Chamadas);
        Assert.Contains("erro:261", ops.Log);
        Assert.DoesNotContain("preparar:101", ops.Log);
    }

    [Fact]
    public async Task Timeout261_RegistraTimeout_ZeroRetry_NaoChama101()
    {
        FakeOps045 ops = new(); Gw261 a = new(ResultadoMovimentoSap.Indeterminado("timeout")); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, a.Chamadas); // UM POST, zero retry cego
        Assert.Equal(0, b.Chamadas); Assert.Equal(0, h.Chamadas);
        Assert.Contains("timeout:261", ops.Log);
    }


    [Fact]
    public async Task Timeout261ComTokenChamadorCanceladoDepoisDoClaim_UsaClaimDaTentativa_NaoChama101NemHU()
    {
        using CancellationTokenSource cts = new();
        Guid token = Guid.NewGuid();
        FakeOps045 ops = new();
        ops.ClaimsPorEtapa["261"] = new ResultadoClaim045(true, token, 1);
        Gw261CancelaERetornaTimeout a = new(cts); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);

        ResultadoPipelineProdutoAcabado r = await new ProdutoAcabadoPipeline045Orquestrador(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal, cts.Token);

        Assert.Equal(1, a.Chamadas);
        Assert.Equal(0, b.Chamadas);
        Assert.Equal(0, h.Chamadas);
        Assert.Contains("timeout:261", ops.Log);
        Assert.Equal(false, ops.UltimoTimeoutTokenCancelado);
        Assert.Equal(token, ops.UltimoTimeoutClaimToken);
        Assert.Equal(1, ops.UltimaTimeoutTentativa);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
    }
    [Fact]
    public async Task Erro101_RegistraErro_NaoChamaHU()
    {
        FakeOps045 ops = new(); Gw261 a = new(Doc()); Gw101 b = new(ResultadoMovimentoSap.Erro("400", 400)); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, b.Chamadas); Assert.Equal(0, h.Chamadas);
        Assert.Contains("erro:101", ops.Log);
    }

    [Fact]
    public async Task Timeout101_RegistraTimeout_NaoChamaHU()
    {
        FakeOps045 ops = new(); Gw261 a = new(Doc()); Gw101 b = new(ResultadoMovimentoSap.Indeterminado("timeout")); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(0, h.Chamadas);
        Assert.Contains("timeout:101", ops.Log);
    }

    [Fact]
    public async Task ClaimNegado_261_ZeroPOST_NaoRegistra_NaoAvanca()
    {
        FakeOps045 ops = new(); ops.ClaimsNegados.Add("261");
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(0, a.Chamadas); Assert.Equal(0, b.Chamadas); Assert.Equal(0, h.Chamadas); // Â§6: sem claim â‡’ sem POST
        Assert.DoesNotContain(ops.Log, s => s.StartsWith("sucesso") || s.StartsWith("erro") || s.StartsWith("timeout"));
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
    }

    [Fact]
    public async Task ClaimNegado_101_ZeroPOST_101_NaoChamaHU()
    {
        FakeOps045 ops = new(); ops.ClaimsNegados.Add("101");
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, a.Chamadas); Assert.Equal(0, b.Chamadas); Assert.Equal(0, h.Chamadas);
    }

    // ===================== Â§4: conclusÃ£o SÃ“ com HU ConfirmadaSap =====================
    [Fact]
    public async Task HuConfirmadaSap_PipelineConcluido()
    {
        FakeOps045 ops = new(); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, new(Doc()), new(Doc()), h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(EtapaPipelineProdutoAcabado.Concluido, r.UltimaEtapa);
    }

    [Theory]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    [InlineData(StatusIntegracaoCaixa.ProntaParaEnvio)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada)]
    public async Task HuNaoConfirmada_PipelineNaoConcluido(StatusIntegracaoCaixa estadoHu)
    {
        FakeOps045 ops = new(); GwHu h = new(estadoHu);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, new(Doc()), new(Doc()), h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.NotEqual(EtapaPipelineProdutoAcabado.Concluido, r.UltimaEtapa);
        Assert.Equal(EtapaPipelineProdutoAcabado.HandlingUnit, r.UltimaEtapa);
    }

    [Fact]
    public async Task HuIndeterminadoTimeout_NaoConcluido_ZeroRetry()
    {
        FakeOps045 ops = new(); GwHu h = new(StatusIntegracaoCaixa.IndeterminadoTimeout);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, new(Doc()), new(Doc()), h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.NotEqual(EtapaPipelineProdutoAcabado.Concluido, r.UltimaEtapa);
        Assert.Equal(1, h.Chamadas); // UM acionamento HU, zero retry cego
    }

    // ===================== Â§5: persistÃªncia 045 false bloqueia avanÃ§o =====================
    [Fact]
    public async Task RegistrarSucesso261False_ZeroPOST101_ZeroHU()
    {
        FakeOps045 ops = new(); ops.SucessoNaoPersistido.Add("261");
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, a.Chamadas); // POST 261 ocorreu (foi confirmado no SAP)
        Assert.Equal(0, b.Chamadas); // mas persistÃªncia nÃ£o comprovada â‡’ ZERO 101
        Assert.Equal(0, h.Chamadas); // ZERO HU
        Assert.DoesNotContain("preparar:101", ops.Log);
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento261, r.UltimaEtapa);
    }

    [Fact]
    public async Task RegistrarSucesso101False_ZeroHU()
    {
        FakeOps045 ops = new(); ops.SucessoNaoPersistido.Add("101");
        Gw261 a = new(Doc()); Gw101 b = new(Doc()); GwHu h = new(StatusIntegracaoCaixa.ConfirmadaSap);
        ResultadoPipelineProdutoAcabado r = await Orq(ops, a, b, h).ExecutarAsync(1, Cmd261(), Cmd101(), Usuario, Terminal);
        Assert.Equal(1, b.Chamadas); // POST 101 ocorreu
        Assert.Equal(0, h.Chamadas); // persistÃªncia 101 nÃ£o comprovada â‡’ ZERO HU
        Assert.Equal(EtapaPipelineProdutoAcabado.Movimento101, r.UltimaEtapa);
    }

    [Fact]
    public void Form_BloqueiaReenvioComEstado045Persistido()
    {
        string form = File.ReadAllText(CaminhoFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));
        Assert.Contains("_controller.VerificarBloqueioPipeline045Async", form, StringComparison.Ordinal);
        Assert.Contains("_codigoCaixaPipeline045Bloqueada", form, StringComparison.Ordinal);
        Assert.Contains("Não tente enviar novamente", form, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MessageBox.Show", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_ConsultaView045AutoritativaParaBloqueioDeRestart()
    {
        string controller = File.ReadAllText(CaminhoFonte("Controle", "Processo", "ProdutoAcabadoController.cs"));
        Assert.Contains("LerEstadoEtapasAsync", controller, StringComparison.Ordinal);
        Assert.Contains("LinhaPipeline045BloqueiaEnvio", controller, StringComparison.Ordinal);
        Assert.Contains("CONFIRMADO_SAP", controller, StringComparison.Ordinal);
        Assert.Contains("estado que exige", controller, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void GuardHu_NuncaChamadoPeloOrquestrador()
    {
        // Â§2/Â§7: o guard Ã© trigger de banco. O orquestrador nÃ£o invoca guard_hu_pos_101 (nÃ£o hÃ¡ operaÃ§Ã£o para isso).
        string src = File.ReadAllText(CaminhoFonte("Servicos", "Operacao", "ProdutoAcabadoPipeline045Orquestrador.cs"));
        Assert.DoesNotContain("guard_hu_pos_101", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GuardHuPos101", src, StringComparison.Ordinal);
    }

    [Fact]
    public void Orquestrador_NaoUsaSalvarSnapshotComoAutoridade()
    {
        // Â§5/Â§10: a persistÃªncia autoritativa Ã© o contrato 045 (_ops.*). O orquestrador produtivo NUNCA chama Salvar.
        string src = File.ReadAllText(CaminhoFonte("Servicos", "Operacao", "ProdutoAcabadoPipeline045Orquestrador.cs"));
        Assert.DoesNotContain(".Salvar(", src, StringComparison.Ordinal);
        Assert.Contains("_ops.AdquirirClaimEtapaAsync", src, StringComparison.Ordinal);
        Assert.Contains("_ops.RegistrarSucessoEtapaAsync", src, StringComparison.Ordinal);
    }

    private static string CaminhoFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return Path.Combine(dir, Path.Combine(partes));
    }
}
















