using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Modelo;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Regra definitiva da etiqueta de Entrada: UMA etiqueta por pesagem individual (peso = pesagem.PesoLiquidoKg),
/// total só na linha (consulta) e no SAP. Cobre montagem por pesagem, impressão individual comportamental,
/// listagem persistida por pesagem e a fiação da tela.
/// </summary>
public sealed class EntradaImpressaoPorPesagemTests
{
    // ---- Montagem da etiqueta por pesagem (serviço, puro) ----

    [Fact]
    public void MontarEtiquetaPorPesagem_UsaPesoLiquidoDaPesagem_NaoOTotal()
    {
        EntradaProdutoItemPersistido item = new()
        {
            Material = "MP-1",
            DescricaoMaterial = "Couro",
            NumeroPedido = "4500001",
            NumeroItem = "10",
            Fornecedor = "F1",
            PesoLiquidoTotalKg = 100m
        };
        EntradaProdutoPesagem pesagem = new() { PesoBrutoKg = 52m, PesoTaraKg = 2m, PesoLiquidoKg = 50m };

        DadosEtiquetaMateriaPrima porPesagem = ImpressaoEntradaServico.MontarEtiquetaPorPesagem(item, pesagem, "31/12/2026");
        DadosEtiquetaMateriaPrima total = ImpressaoEntradaServico.MontarEtiqueta(item, "31/12/2026");

        Assert.Equal("50", porPesagem.Peso);   // pesagem individual = 50, NUNCA 100
        Assert.Equal("100", total.Peso);        // total permanece só para consulta
        Assert.NotEqual(total.Peso, porPesagem.Peso);
        // Demais campos preservados.
        Assert.Equal("MP-1", porPesagem.CodigoProduto);
        Assert.Equal("4500001", porPesagem.NumeroPedido);
        Assert.Equal("10", porPesagem.NumeroItem);
    }

    [Fact]
    public void MontarEtiquetaPorPesagem_ComTara_UsaLiquido48()
    {
        EntradaProdutoItemPersistido item = new() { Material = "MP", PesoLiquidoTotalKg = 999m };
        EntradaProdutoPesagem pesagem = new() { PesoBrutoKg = 50m, PesoTaraKg = 2m, PesoLiquidoKg = 48m };
        Assert.Equal("48", ImpressaoEntradaServico.MontarEtiquetaPorPesagem(item, pesagem, "").Peso);
    }

    // ---- Comportamental: duas pesagens de 50 kg imprimem 2 etiquetas de 50 (nenhuma de 100) ----

    [Fact]
    public async Task Dialogo_DuasPesagens50_ImprimemDuasEtiquetasDe50_TotalPreservado100()
    {
        List<EntradaProdutoPesagem> impressas = new();
        TaraCadastro tara = new() { NomeTara = "T", PesoKg = 0m, CodigoTara = 1 };

        // Ctor interno com repositório null-backed: o caminho manual não toca no repo/leitor.
        BalancaLeituraServico balanca = new(new BalancaRepositorio(null!), new LeitorBalancaSerialServico());
        using PesagemMultiplaItemForm form = new(
            balanca,
            "4500001/10",
            tara,
            null,
            Array.Empty<EntradaProdutoPesagem>(),
            imprimirPesagemAsync: p => { impressas.Add(p); return Task.FromResult(true); });

        await AdicionarManualAsync(form, "50");
        await AdicionarManualAsync(form, "50");

        // Duas impressões automáticas, cada uma de 50 kg líquido; nenhuma de 100.
        Assert.Equal(2, impressas.Count);
        Assert.All(impressas, p => Assert.Equal(50m, p.PesoLiquidoKg));
        Assert.DoesNotContain(impressas, p => p.PesoLiquidoKg == 100m);
        // Total preservado apenas para consulta.
        Assert.Equal(100m, form.PesoTotal);
        Assert.Equal(2, form.Pesagens.Count);
    }

    [Fact]
    public async Task Dialogo_FalhaImpressao_MantemPesagemNaLista()
    {
        TaraCadastro tara = new() { NomeTara = "T", PesoKg = 0m, CodigoTara = 1 };
        BalancaLeituraServico balanca = new(new BalancaRepositorio(null!), new LeitorBalancaSerialServico());
        using PesagemMultiplaItemForm form = new(
            balanca, "1/1", tara, null, Array.Empty<EntradaProdutoPesagem>(),
            imprimirPesagemAsync: _ => Task.FromResult(false)); // impressora "falha"

        await AdicionarManualAsync(form, "50");

        // Falha de impressão NÃO remove a pesagem.
        Assert.Single(form.Pesagens);
        Assert.Equal(50m, form.Pesagens[0].PesoLiquidoKg);
    }

    [Fact]
    public void NovoFluxoLotes_DeveBloquearImpressaoEnquantoNaoPersistido()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string leituraBalanca = ExtrairTrecho(form, "private async void ReadWeightLegend_Click", "private static string GetFriendlyErrorMessage");
        string leituraManual = ExtrairTrecho(form, "private async void LeituraManual_Click", "private void UpdateProductionState");
        string pesagemMultipla = ExtrairTrecho(form, "private async Task AbrirPesagemMultiplaParaLinhaAsync", "private async Task<bool> TentarReimprimirEtiquetaPesagemAsync");

        Assert.Contains("Impressão do novo fluxo de lotes ainda não habilitada", form, StringComparison.Ordinal);
        Assert.Contains("imprimirPesagemAsync: null", pesagemMultipla, StringComparison.Ordinal);
        Assert.Contains("reimprimirPesagemAsync: null", pesagemMultipla, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarImprimirEtiquetaAposLeituraAsync", leituraBalanca, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarImprimirEtiquetaAposLeituraAsync", leituraManual, StringComparison.Ordinal);
        Assert.Contains("TentarReimprimirEtiquetaPesagemAsync", form, StringComparison.Ordinal);
    }
    // ---- Repositório: lista cada pesagem (não SUM), ordenada, parametrizada ----

    [Fact]
    public void Repositorio_ListarPesagensPersistidas_NaoUsaSum_OrdenaPorSequencia_Parametrizada()
    {
        string repo = LerArquivo("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");
        int inicio = repo.IndexOf("public async Task<IReadOnlyList<EntradaProdutoPesagem>> ListarPesagensPersistidasAsync", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "ListarPesagensPersistidasAsync não encontrado.");
        // Delimita no início do PRÓXIMO método público (ListarItensParaEnvioSapAsync) para não capturar o SUM dele.
        int fim = repo.IndexOf("ListarItensParaEnvioSapAsync", inicio + 10, StringComparison.Ordinal);
        if (fim < 0) fim = repo.Length;
        string metodo = repo[inicio..fim];

        Assert.DoesNotContain("SUM(", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY pesagem.sequencia, pesagem.pesado_em", metodo, StringComparison.Ordinal);
        Assert.Contains("@codigo_lancamento", metodo, StringComparison.Ordinal);
        Assert.Contains("@codigo_sap_item", metodo, StringComparison.Ordinal);
        Assert.Contains("codigo_entrada_produto_pesagem", metodo, StringComparison.Ordinal);
    }

    // ---- Tela: leitura direta e peso manual imprimem por pesagem (líquido), não pelo total ----

    [Fact]
    public void Tela_LeituraDireta_RegistraEmMemoriaENaoImprimeNestaFase()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string leitura = ExtrairTrecho(form, "private async void ReadWeightLegend_Click", "private static string GetFriendlyErrorMessage");

        Assert.Contains("RegistrarPesoLidoOperacaoComLotesAsync", leitura, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoPesagemCalculos.OrigemBalanca", leitura, StringComparison.Ordinal);
        Assert.Contains("Impressão do novo fluxo de lotes ainda não habilitada", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirEtiquetaPorPesagem(linhaItem", leitura, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarImprimirEtiquetaAposLeituraAsync", leitura, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PesoManual_RegistraEmMemoriaENaoImprimeNestaFase()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string manual = ExtrairTrecho(form, "private async void LeituraManual_Click", "private void UpdateProductionState");

        Assert.Contains("RegistrarPesoLidoOperacaoComLotesAsync", manual, StringComparison.Ordinal);
        Assert.Contains("EntradaProdutoPesagemCalculos.OrigemManual", manual, StringComparison.Ordinal);
        Assert.Contains("Impressão do novo fluxo de lotes ainda não habilitada", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirEtiquetaPorPesagem(selectedRow", manual, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarImprimirEtiquetaAposLeituraAsync", manual, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ConstruirEtiquetaPorPesagem_UsaPesoLiquidoDaPesagem()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        int m = form.IndexOf("private DadosEtiquetaMateriaPrima ConstruirEtiquetaPorPesagem", StringComparison.Ordinal);
        Assert.True(m >= 0);
        string trecho = form.Substring(m, 260);
        Assert.Contains("pesagem.PesoLiquidoKg", trecho, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ReprintColuna_RenomeadaParaPesagens()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        Assert.Contains("HeaderText = \"Pesagens\"", form, StringComparison.Ordinal);
        Assert.Contains("Ver pesagens e reimprimir etiquetas", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_Persistido_ConsultaReimpressao_UsaListagemPorPesagem_NaoTotal()
    {
        string form = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        int m = form.IndexOf("private async Task AbrirPesagensPersistidasParaLinhaAsync", StringComparison.Ordinal);
        Assert.True(m >= 0);
        int fim = form.IndexOf("\n    private ", m + 10, StringComparison.Ordinal);
        string metodo = form.Substring(m, (fim < 0 ? form.Length : fim) - m);
        Assert.Contains("ListarPesagensPersistidasAsync", metodo, StringComparison.Ordinal);
        Assert.Contains("MontarEtiquetaPorPesagem(itemPersistido, pesagem", metodo, StringComparison.Ordinal);
        Assert.Contains("somenteConsulta: true", metodo, StringComparison.Ordinal);
    }

    private static string ExtrairTrecho(string fonte, string inicio, string fim)
    {
        int indiceInicio = fonte.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(indiceInicio >= 0, $"Início não encontrado: {inicio}");
        int indiceFim = fonte.IndexOf(fim, indiceInicio + 1, StringComparison.Ordinal);
        Assert.True(indiceFim > indiceInicio, $"Fim não encontrado: {fim}");
        return fonte[indiceInicio..indiceFim];
    }
    private static async Task AdicionarManualAsync(PesagemMultiplaItemForm form, string peso)
    {
        TextBox tb = (TextBox)typeof(PesagemMultiplaItemForm)
            .GetField("_pesoManualTextBox", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;
        tb.Text = peso;
        MethodInfo mi = typeof(PesagemMultiplaItemForm)
            .GetMethod("AdicionarPesoManualAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)mi.Invoke(form, null)!;
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
