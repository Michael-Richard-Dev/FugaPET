using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// REV8 (Hermes 14:11): roteamento de UI para os caminhos produtivos. CAIXA: quando o pipeline PA está
/// habilitado a Form roteia para EnviarCaixaPipelineAsync (nunca HU direta); indisponível ⇒ bloqueia com motivo,
/// sem fallback silencioso. PALETE: montar/preview = zero POST; integração INT012 só por ação explícita
/// (EnviarPaleteInt012Async). PrimeiraEntregaHu não torna o palete permanentemente inacessível.
/// </summary>
public sealed class ProdutoAcabadoUiRoutingRev8Tests
{
    private static string Form() => LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

    // ---------------- CAIXA ----------------
    [Fact]
    public void Caixa_RoteiaPipeline_AntesDoHuOnly()
    {
        string form = Form();
        Assert.Contains("if (_controller.PipelinePaGateHabilitado)", form, StringComparison.Ordinal);
        Assert.Contains("await SolicitarEnvioCaixaPipelineAsync();", form, StringComparison.Ordinal);
        int idxGate = form.IndexOf("if (_controller.PipelinePaGateHabilitado)", StringComparison.Ordinal);
        int idxHu = form.IndexOf("_controller.EnviarCaixaHandlingUnitAsync(", StringComparison.Ordinal);
        Assert.True(idxGate >= 0 && idxHu >= 0 && idxGate < idxHu, "roteamento do pipeline deve preceder o HU-only");
    }

    [Fact]
    public void Caixa_PipelineHabilitadoIndisponivel_BloqueiaSemFallbackHu()
    {
        string metodo = ExtrairMetodo(Form(), "private async Task SolicitarEnvioCaixaPipelineAsync()");
        Assert.Contains("if (!_controller.PipelinePaDisponivel)", metodo, StringComparison.Ordinal);
        // Bloqueia exibindo o motivo e NÃO cai em HU-only dentro do caminho do pipeline.
        Assert.Contains("PipelinePaMotivo", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarCaixaHandlingUnitAsync", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Caixa_HuOnly_ChamadaUnica_NoCaminhoLegado()
    {
        // Uma única chamada a EnviarCaixaHandlingUnitAsync (caminho legado HU), nunca no caminho do pipeline.
        Assert.Equal(1, ContarOcorrencias(Form(), "_controller.EnviarCaixaHandlingUnitAsync("));
    }

    // ---------------- PALETE ----------------
    [Fact]
    public void Palete_MontarLocal_NaoChamaInt012()
    {
        string form = Form();
        Assert.Contains("paletização saiu do Produto Acabado", form, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_criarPaleteCard.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Visible = false;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Palete_IntegracaoInt012_SoPorAcaoExplicita()
    {
        string form = Form();
        // Ação explícita: duplo clique no grid dispara IntegrarPaleteSelecionadoAsync, que chama o Controller.
        Assert.Contains("paletesDataGridView.CellDoubleClick += async", form, StringComparison.Ordinal);
        Assert.Contains("IntegrarPaleteSelecionadoAsync", form, StringComparison.Ordinal);
        string integrar = ExtrairMetodo(form, "private async Task IntegrarPaleteSelecionadoAsync(int rowIndex)");
        Assert.Contains("_controller.EnviarPaleteInt012Async(palete)", integrar, StringComparison.Ordinal);
        // Único ponto de POST de palete na Form.
        Assert.Equal(1, ContarOcorrencias(form, "_controller.EnviarPaleteInt012Async("));
    }

    [Fact]
    public void Palete_PrimeiraEntregaHu_NaoTornaInacessivel_QuandoGateOn()
    {
        string form = Form();
        // Card e montagem passam a considerar o gate do pipeline — deixam de ficar permanentemente ocultos/inativos.
        Assert.Contains("PrimeiraEntregaHu && !_controller.PipelinePaGateHabilitado", form, StringComparison.Ordinal);
    }

    // ---------------- Controller fail-closed (comportamental) ----------------
    [Fact]
    public async Task Controller_EnviarPaleteInt012_FailClosed_ZeroPost_Default()
    {
        ProdutoAcabadoController controller = new(); // default = fail-closed (sem CPI/DB configurados)
        Assert.False(controller.PaleteInt012EnvioAutorizado); // gate/base/allowlist CPI ausentes por padrão

        ProdutoAcabadoCaixa c1 = CaixaConfirmada(1);
        ProdutoAcabadoPalete palete = controller.MontarPalete(OrdemValida(), [c1], [], 1, 1, "PACK_TEST_001");
        ResultadoPaleteInt012 r = await controller.EnviarPaleteInt012Async(palete);
        Assert.NotEqual(EstadoPaleteInt012.Confirmado, r.Estado); // NaoEnviado — nenhum POST real
    }

    // ---------------- helpers ----------------
    private static ProdutoAcabadoOrdem OrdemValida() => new() { NumeroOrdem = "1000909", Centro = "3007", DepositoDestino = "PA01" };
    private static ProdutoAcabadoCaixa CaixaConfirmada(int numero)
        => new()
        {
            CodigoProdutoAcabadoCaixa = numero, NumeroCaixa = numero, NumeroOrdemProducao = "1000909",
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap, HandlingUnitExternalId = $"30001{numero:0000}",
            PesoBrutoKg = 10m, PesoLiquidoKg = 9m, TaraKg = 1m
        };

    private static int ContarOcorrencias(string texto, string alvo)
    {
        int n = 0, i = 0;
        while ((i = texto.IndexOf(alvo, i, StringComparison.Ordinal)) >= 0) { n++; i += alvo.Length; }
        return n;
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') { profundidade++; }
            else if (fonte[i] == '}') { profundidade--; if (profundidade == 0) { return fonte[inicio..(i + 1)]; } }
        }
        return fonte[inicio..];
    }

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}

