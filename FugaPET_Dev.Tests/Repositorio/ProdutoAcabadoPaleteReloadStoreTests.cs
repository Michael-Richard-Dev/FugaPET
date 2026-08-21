using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Repositorio;

/// <summary>
/// GATE 046-E §15: reload persistente — o store RECONSTRÓI palete + caixas a partir das linhas da projeção
/// (view + hu_palete_item + hu_caixa), sem estado em memória prévio. Um executor fake fornece as linhas; nenhum
/// banco real. Também prova que payload/claim NÃO são lidos (o store nunca os projeta).
/// </summary>
public sealed class ProdutoAcabadoPaleteReloadStoreTests
{
    [Fact]
    public async Task ListaVazia_RetornaVazio()
    {
        ProdutoAcabadoPipelinePostgresStore store = new(new FakeExecutorReload([]));
        IReadOnlyList<ProdutoAcabadoPalete> paletes = await store.LerPaletesLocaisPorOrdemAsync("1001", null);
        Assert.Empty(paletes);
    }

    [Fact]
    public async Task UmPalete_VariasCaixas_ReconstroiComposicaoOrdenada()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(900, "PACK_REAL_001", "3007", "PP01", 30m, 27m, 3m, "RASCUNHO", "TERM-1", item: 5001, caixa: 101, ordem: 1, "1001", "0001", "HU-101", numeroCaixa: 1, "2000091", "LOTE1"),
            LinhaComposicao(900, "PACK_REAL_001", "3007", "PP01", 30m, 27m, 3m, "RASCUNHO", "TERM-1", item: 5002, caixa: 102, ordem: 2, "1001", "0001", "HU-102", numeroCaixa: 2, "2000091", "LOTE1"),
            LinhaComposicao(900, "PACK_REAL_001", "3007", "PP01", 30m, 27m, 3m, "RASCUNHO", "TERM-1", item: 5003, caixa: 103, ordem: 3, "1001", "0001", "HU-103", numeroCaixa: 3, "2000091", "LOTE1"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        IReadOnlyList<ProdutoAcabadoPalete> paletes = await store.LerPaletesLocaisPorOrdemAsync("1001", null);

        ProdutoAcabadoPalete palete = Assert.Single(paletes);
        Assert.Equal(900, palete.CodigoHuPalete);
        Assert.Equal("PACK_REAL_001", palete.PackagingMaterial);
        Assert.Equal("3007", palete.Plant);
        Assert.Equal("PP01", palete.StorageLocation);
        Assert.Equal(30m, palete.PesoBrutoKg);
        Assert.Equal(27m, palete.PesoLiquidoKg);
        Assert.Equal(3m, palete.TaraKg);
        Assert.Equal("RASCUNHO", palete.StatusSap);
        Assert.Equal(1, palete.PrimeiraCaixa);
        Assert.Equal(3, palete.UltimaCaixa);
        Assert.Equal([1, 2, 3], palete.Caixas.Select(c => c.NumeroCaixa)); // ordem_item preservada
        Assert.Equal("HU-101", palete.Caixas[0].HandlingUnitExternalId);
        Assert.Equal("2000091", palete.Caixas[0].Material);
        Assert.Equal("LOTE1", palete.Caixas[0].Lote);
        Assert.Equal("1001", palete.Caixas[0].NumeroOrdemProducao);
    }

    [Fact] // GATE 046-AQ-F1: caixa persistida CONFIRMADA_SAP é reconstruída como ConfirmadaSap e o preview NÃO rejeita por status
    public async Task CaixaConfirmadaSap_ReconstroiConfirmadaSap_PreviewNaoRejeitaPorStatus()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(3, "PACK_REAL_001", "3007", "PP01", 20m, 18m, 2m, "RASCUNHO", "TERM-1", 5004, 4, 1, "1002024", "0001", "300014352", 4, "2000091", "LOTE1", statusCaixa: "CONFIRMADA_SAP"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete palete = Assert.Single(await store.LerPaletesLocaisPorOrdemAsync("1002024", null));
        ProdutoAcabadoCaixa caixa = Assert.Single(palete.Caixas);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, caixa.StatusIntegracao); // ← o estado real, não o default EmPesagem
        Assert.Equal("300014352", caixa.HandlingUnitExternalId);

        // O preview não pode mais reprovar por "precisa estar CONFIRMADA_SAP".
        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(palete);
        Assert.True(preview.Sucesso, preview.Mensagem);
        Assert.DoesNotContain("CONFIRMADA_SAP", preview.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // A regra NÃO enfraquece: caixa realmente não-ConfirmadaSap ⇒ preview continua rejeitando
    public async Task CaixaNaoConfirmada_PreviewContinuaRejeitando()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(3, "PACK_REAL_001", "3007", "PP01", 20m, 18m, 2m, "RASCUNHO", "TERM-1", 5004, 4, 1, "1002024", "0001", "300014352", 4, "2000091", "LOTE1", statusCaixa: "ERRO_SAP"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete palete = Assert.Single(await store.LerPaletesLocaisPorOrdemAsync("1002024", null));
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, palete.Caixas[0].StatusIntegracao);

        ResultadoPreviewProdutoAcabadoPalete preview = new ProdutoAcabadoPaletePayloadBuilder().MontarPreview(palete);
        Assert.False(preview.Sucesso);
        Assert.Contains("CONFIRMADA_SAP", preview.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // status ausente ⇒ NÃO vira ConfirmadaSap por default (fail-closed: preview reprova)
    public async Task StatusCaixaAusente_NaoViraConfirmadaSap()
    {
        FakeExecutorReload exec = new([LinhaComposicao(3, "PACK_REAL_001", "3007", "PP01", 20m, 18m, 2m, "RASCUNHO", "TERM-1", 5004, 4, 1, "1002024", "0001", "300014352", 4, "2000091", "LOTE1", statusCaixa: "")]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete palete = Assert.Single(await store.LerPaletesLocaisPorOrdemAsync("1002024", null));
        Assert.NotEqual(StatusIntegracaoCaixa.ConfirmadaSap, palete.Caixas[0].StatusIntegracao);
    }

    [Fact]
    public async Task MultiplosPaletes_AgrupadosPorCodigo()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(900, "PACK_A", "3007", "PP01", 20m, 18m, 2m, "RASCUNHO", "TERM-1", 5001, 101, 1, "1001", "0001", "HU-101", 1, "MAT", "L1"),
            LinhaComposicao(900, "PACK_A", "3007", "PP01", 20m, 18m, 2m, "RASCUNHO", "TERM-1", 5002, 102, 2, "1001", "0001", "HU-102", 2, "MAT", "L1"),
            LinhaComposicao(901, "PACK_B", "3007", "PP01", 10m, 9m, 1m, "RASCUNHO", "TERM-1", 5003, 103, 1, "1001", "0001", "HU-103", 3, "MAT", "L1"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        IReadOnlyList<ProdutoAcabadoPalete> paletes = await store.LerPaletesLocaisPorOrdemAsync("1001", null);

        Assert.Equal(2, paletes.Count);
        Assert.Equal(900, paletes[0].CodigoHuPalete);
        Assert.Equal(2, paletes[0].Caixas.Count);
        Assert.Equal(901, paletes[1].CodigoHuPalete);
        Assert.Single(paletes[1].Caixas);
    }

    [Fact]
    public async Task FiltroPorTerminal_RepassadoAoExecutor()
    {
        FakeExecutorReload exec = new([]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        await store.LerPaletesLocaisPorOrdemAsync("1001", "TERM-7");
        Assert.Equal("1001", exec.UltimaOrdem);
        Assert.Equal("TERM-7", exec.UltimoTerminal);

        await store.LerPaletesLocaisPorOrdemAsync("1001", "   ");
        Assert.Null(exec.UltimoTerminal); // whitespace ⇒ sem filtro de terminal
    }

    [Fact]
    public async Task Reload_ComEstadoEmMemoriaVazio_ReconstroiDoBanco()
    {
        // Nenhum estado em memória: o store parte apenas das linhas persistidas.
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(900, "PACK_REAL_001", "3007", "PP01", 10m, 9m, 1m, "RASCUNHO", "TERM-1", 5001, 101, 1, "1001", "0001", "HU-101", 1, "2000091", "LOTE1"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        IReadOnlyList<ProdutoAcabadoPalete> paletes = await store.LerPaletesLocaisPorOrdemAsync("1001", null);

        ProdutoAcabadoPalete palete = Assert.Single(paletes);
        Assert.Equal("RASCUNHO", palete.StatusSap);
        Assert.Single(palete.Caixas);
    }

    [Fact]
    public void ExecutorReal_NaoProjetaPayloadNemClaim()
    {
        // §8: a projeção real (SELECT explícito) não menciona payload/claim/lease/recovery e não usa SELECT *
        // nem schema hardcoded. Isola o literal SQL da consulta de composição.
        string src = File.ReadAllText(CaminhoFonte("AcessoDados", "Repositorio", "ProdutoAcabadoPipeline045NpgsqlExecutor.cs"));
        int inicioSql = src.IndexOf("const string sql = \"\"\"", StringComparison.Ordinal);
        Assert.True(inicioSql >= 0);
        int fimSql = src.IndexOf("\"\"\";", inicioSql, StringComparison.Ordinal);
        Assert.True(fimSql > inicioSql);
        string sql = src[inicioSql..fimSql];
        Assert.Contains("vw_pa_045_palete_estado_runtime", sql, StringComparison.Ordinal);
        Assert.Contains("hu_palete_item", sql, StringComparison.Ordinal);
        Assert.Contains("hu_caixa", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY h.codigo_hu_palete, i.ordem_item", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("payload", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claim", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lease", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recovery", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("desenvolvimento", sql, StringComparison.Ordinal); // sem schema hardcoded
    }

    [Fact] // GATE 047-AB: reload por TERMINAL (sem OP) reconstrói paletes persistidos, incluindo RASCUNHO (palete 9).
    public async Task ReloadPorTerminal_ReconstroiPaleteRascunho_SemFiltroDeOp()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(9, "PALLET01", "3007", "PP01", 3m, 2m, 1m, "RASCUNHO", "TERM-1", 5001, 101, 1, "1001", "0001", "300014404", 1, "2000091", "LOTE1"),
            LinhaComposicao(9, "PALLET01", "3007", "PP01", 3m, 2m, 1m, "RASCUNHO", "TERM-1", 5002, 102, 2, "1001", "0001", "300014405", 2, "2000091", "LOTE1"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete palete = Assert.Single(await store.LerPaletesLocaisPorTerminalAsync("TERM-1"));

        Assert.Equal(9, palete.CodigoHuPalete);
        Assert.Equal("RASCUNHO", palete.StatusSap);
        Assert.Equal(2, palete.Caixas.Count);
        Assert.Equal("TERM-1", exec.UltimoTerminal);
        Assert.Null(exec.UltimaOrdem); // reload por terminal NÃO filtra por OP
    }

    [Fact] // GATE 047-AD: CONFIRMADO_SAP com HU pai persistida ⇒ HandlingUnitPalete real (não "-").
    public async Task Confirmado_ComHuPai_MapeiaHandlingUnitPalete()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(2, "PACK", "3007", "PP01", 20m, 18m, 2m, "CONFIRMADO_SAP", "TERM-1", 5001, 101, 1, "1001", "0001", "HU-101", 1, "MAT", "L1", huPalete: "300014401"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete p = Assert.Single(await store.LerPaletesLocaisPorTerminalAsync("TERM-1"));
        Assert.Equal("CONFIRMADO_SAP", p.StatusSap);
        Assert.Equal("300014401", p.HandlingUnitPalete); // HU pai real, não "-"
    }

    [Fact] // GATE 047-AD: RASCUNHO sem HU pai ⇒ HandlingUnitPalete vazio (grid exibe "-"). Não fabricar HU.
    public async Task Rascunho_SemHuPai_HandlingUnitPaleteVazio()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(9, "PALLET01", "3007", "PP01", 3m, 2m, 1m, "RASCUNHO", "TERM-1", 5001, 101, 1, "1001", "0001", "300014404", 1, "MAT", "L1", huPalete: null),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete p = Assert.Single(await store.LerPaletesLocaisPorTerminalAsync("TERM-1"));
        Assert.Equal("RASCUNHO", p.StatusSap);
        Assert.Equal(string.Empty, p.HandlingUnitPalete);
    }

    [Fact] // GATE 047-AD: mesma reconstrução por OP (RELOAD_APOS_ENVIO) também mapeia a HU pai.
    public async Task ReloadPorOrdem_Confirmado_MapeiaHuPai()
    {
        FakeExecutorReload exec = new(
        [
            LinhaComposicao(5, "PACK", "3007", "PP01", 20m, 18m, 2m, "CONFIRMADO_SAP", "TERM-1", 5001, 101, 1, "1001", "0001", "HU-1", 1, "MAT", "L1", huPalete: "300014402"),
        ]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        ProdutoAcabadoPalete p = Assert.Single(await store.LerPaletesLocaisPorOrdemAsync("1001", "TERM-1"));
        Assert.Equal("300014402", p.HandlingUnitPalete);
    }

    [Fact] // GATE 047-AD: a projeção traz a HU pai da view de forma FAIL-SAFE (to_jsonb; NULL se ausente ⇒ sem 42703).
    public void ExecutorReal_ProjetaHuPaiFailSafe()
    {
        string src = File.ReadAllText(CaminhoFonte("AcessoDados", "Repositorio", "ProdutoAcabadoPipeline045NpgsqlExecutor.cs"));
        Assert.Contains("AS handling_unit_palete", src, StringComparison.Ordinal);
        Assert.Contains("to_jsonb(h)->>'handling_unit_external_id'", src, StringComparison.Ordinal);
        Assert.Contains("to_jsonb(h)->>'hu_palete'", src, StringComparison.Ordinal);
    }

    [Fact] // terminal vazio ⇒ fail-closed (não lista tudo, não abre consulta)
    public async Task ReloadPorTerminal_TerminalVazio_FailClosed()
    {
        FakeExecutorReload exec = new([LinhaComposicao(9, "PALLET01", "3007", "PP01", 3m, 2m, 1m, "RASCUNHO", "TERM-1", 5001, 101, 1, "1001", "0001", "HU", 1, "MAT", "L1")]);
        ProdutoAcabadoPipelinePostgresStore store = new(exec);

        Assert.Empty(await store.LerPaletesLocaisPorTerminalAsync("   "));
        Assert.Null(exec.UltimoTerminal); // nem chegou a consultar
    }

    private static Linha045 LinhaComposicao(
        long codigoHuPalete, string material, string centro, string deposito,
        decimal bruto, decimal liquido, decimal tara, string status, string terminal,
        long item, long caixa, int ordem, string numeroOrdem, string itemOrdem, string hu,
        int numeroCaixa, string materialCaixa, string lote, string statusCaixa = "CONFIRMADA_SAP",
        string? huPalete = null)
        => new(new Dictionary<string, object?>
        {
            ["status_hu_caixa"] = statusCaixa,
            ["codigo_hu_palete"] = codigoHuPalete,
            ["handling_unit_palete"] = huPalete,
            ["material_embalagem"] = material,
            ["centro"] = centro,
            ["deposito"] = deposito,
            ["peso_bruto"] = bruto,
            ["peso_liquido"] = liquido,
            ["peso_tara"] = tara,
            ["unidade_peso"] = "KG",
            ["status_hu_palete"] = status,
            ["terminal"] = terminal,
            ["codigo_hu_palete_item"] = item,
            ["codigo_hu_caixa"] = caixa,
            ["ordem_item"] = ordem,
            ["status_vinculo"] = "VINCULADA",
            ["numero_ordem_producao"] = numeroOrdem,
            ["item_ordem_producao"] = itemOrdem,
            ["hu_caixa"] = hu,
            ["handling_unit_external_id"] = hu,
            ["numero_caixa"] = numeroCaixa,
            ["material"] = materialCaixa,
            ["lote"] = lote,
        });

    private static string CaminhoFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return Path.Combine(dir, Path.Combine(partes));
    }

    private sealed class FakeExecutorReload : IProdutoAcabadoPipeline045Executor
    {
        private readonly IReadOnlyList<Linha045> _linhas;
        public FakeExecutorReload(IReadOnlyList<Linha045> linhas) => _linhas = linhas;
        public string? UltimaOrdem { get; private set; }
        public string? UltimoTerminal { get; private set; }

        public bool Disponivel => true;

        public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(string numeroOrdemProducao, string? terminal, CancellationToken cancellationToken = default)
        {
            UltimaOrdem = numeroOrdemProducao;
            UltimoTerminal = terminal;
            return Task.FromResult(_linhas);
        }

        // GATE 047-AB: reload por terminal (sem OP) — captura o terminal e devolve a composição canônica.
        public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        {
            UltimaOrdem = null;
            UltimoTerminal = terminal;
            return Task.FromResult(_linhas);
        }

        public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<T> ExecutarEmTransacaoAsync<T>(Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
