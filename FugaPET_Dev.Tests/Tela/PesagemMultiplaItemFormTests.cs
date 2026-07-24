namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Regra definitiva: uma etiqueta por pesagem individual; o total fica só na linha (consulta). A janela imprime
/// cada nova leitura imediatamente e NÃO imprime etiqueta consolidada ao concluir; Fechar preserva as leituras.
/// </summary>
public sealed class PesagemMultiplaItemFormTests
{
    [Fact]
    public void Concluir_IncorporaPesoManualPendente_SemImprimirConsolidado()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");

        int concluir = conteudo.IndexOf("private async Task ConcluirAsync()", StringComparison.Ordinal);
        Assert.True(concluir >= 0, "ConcluirAsync não encontrado.");
        int pendente = conteudo.IndexOf("await AdicionarPesoManualAsync();", concluir, StringComparison.Ordinal);
        int ok = conteudo.IndexOf("DialogResult = DialogResult.OK;", concluir, StringComparison.Ordinal);
        Assert.True(pendente > concluir);
        Assert.True(ok > pendente);
        // Concluir não imprime etiqueta consolidada.
        string corpoConcluir = conteudo.Substring(concluir, ok - concluir);
        Assert.DoesNotContain("PesoTotalTexto", corpoConcluir, StringComparison.Ordinal);
    }

    [Fact]
    public void Fechar_PreservaPesagens_RetornandoOk()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        // "Fechar" agora retorna OK (preserva), nunca Cancel (descarte silencioso).
        int fechar = conteudo.IndexOf("_cancelarButton.Click += (_, _) =>", StringComparison.Ordinal);
        Assert.True(fechar >= 0);
        int ok = conteudo.IndexOf("DialogResult = DialogResult.OK;", fechar, StringComparison.Ordinal);
        Assert.True(ok > fechar);
        Assert.DoesNotContain("DialogResult = DialogResult.Cancel;", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogo_ImprimeCadaNovaPesagem_ComCallbackPorPesagem()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        // Cada nova leitura dispara impressão individual (callback), não uma consolidada.
        Assert.Contains("Func<EntradaProdutoPesagem, Task<bool>>? _imprimirPesagemAsync", conteudo, StringComparison.Ordinal);
        Assert.Contains("await _imprimirPesagemAsync(nova)", conteudo, StringComparison.Ordinal);
        // Duplo clique reimprime somente aquela pesagem.
        Assert.Contains("_pesagensGrid.CellDoubleClick", conteudo, StringComparison.Ordinal);
        Assert.Contains("await _reimprimirPesagemAsync(pesagem)", conteudo, StringComparison.Ordinal);
        // Só pesagem VÁLIDA reimprime.
        Assert.Contains("Só é possível reimprimir pesagens com status VÁLIDA.", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void Dialogo_FalhaImpressao_MantemPesagem()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        int adicionar = conteudo.IndexOf("private async Task AdicionarPesoAsync", StringComparison.Ordinal);
        int addLista = conteudo.IndexOf("_pesagens.Add(nova);", adicionar, StringComparison.Ordinal);
        int imprimir = conteudo.IndexOf("await _imprimirPesagemAsync(nova)", adicionar, StringComparison.Ordinal);
        // A pesagem é adicionada ANTES da impressão; falha de impressão não a remove.
        Assert.True(addLista > adicionar && addLista < imprimir);
        int fim = conteudo.IndexOf("private async Task ReimprimirPesagemAsync", adicionar, StringComparison.Ordinal);
        string corpo = conteudo.Substring(adicionar, fim - adicionar);
        Assert.DoesNotContain("_pesagens.Remove", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("_pesagens.RemoveAt", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelarLeitura_JaImpressa_ConfirmaEOrientaDescarteFisico()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        Assert.Contains("A etiqueta desta pesagem já pode ter sido impressa. Descarte fisicamente a etiqueta cancelada.", conteudo, StringComparison.Ordinal);
        Assert.Contains("with { StatusPesagem = \"CANCELADA\" }", conteudo, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_PesagemMultipla_NaoImprimeConsolidadoAposDialogo()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        int abrir = conteudo.IndexOf("private async Task AbrirPesagemMultiplaParaLinhaAsync", StringComparison.Ordinal);
        int fim = conteudo.IndexOf("private async Task<bool> TentarReimprimirEtiquetaPesagemAsync", abrir, StringComparison.Ordinal);
        string corpo = conteudo.Substring(abrir, fim - abrir);
        // Passa callbacks por pesagem e NÃO imprime etiqueta consolidada ao concluir.
        Assert.Contains("Func<EntradaProdutoPesagem, Task<bool>> imprimirPesagem", corpo, StringComparison.Ordinal);
        Assert.Contains("ConstruirEtiquetaPorPesagem(linhaItem, pesagem)", corpo, StringComparison.Ordinal);
        Assert.Contains("Pesagens atualizadas. Total do item:", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Peso bruto total", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirDadosEtiquetaMateriaPrima(linhaAlvo)", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_DevePreservarTaraAoRelocalizarLinha()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        int localizarLinha = conteudo.IndexOf(
            "DataGridViewRow linhaAlvo = LocalizarLinhaProducaoPorItemId(itemId) ?? linhaItem;",
            StringComparison.Ordinal);
        int preservarTara = conteudo.IndexOf("linhaAlvo.Tag = tara;", localizarLinha, StringComparison.Ordinal);
        int consolidarPeso = conteudo.IndexOf(
            "AtualizarTotaisDaLinha(linhaAlvo, _leiturasPorItem[codigoItem])",
            preservarTara,
            StringComparison.Ordinal);

        Assert.True(localizarLinha >= 0);
        Assert.True(preservarTara > localizarLinha);
        Assert.True(consolidarPeso > preservarTara);
    }

    [Fact]
    public void TelaEntrada_DeveGravarAntesDeAlterarEstadoVisual()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        int parar = conteudo.IndexOf("private async void StopProduction_Click", StringComparison.Ordinal);
        int gravar = conteudo.IndexOf("await GravarPesagensAsync();", parar, StringComparison.Ordinal);
        int atualizarEstado = conteudo.IndexOf("UpdateProductionState(false);", gravar, StringComparison.Ordinal);

        Assert.True(parar >= 0);
        Assert.True(gravar > parar);
        Assert.True(atualizarEstado > gravar);
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));
}
