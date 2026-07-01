namespace FugaPET_Dev.Tests.Tela;

public sealed class PesagemMultiplaItemFormTests
{
    [Fact]
    public void Concluir_DeveIncorporarPesoManualAindaNaoAdicionado()
    {
        string arquivo = Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "PesagemMultiplaItemForm.cs");
        string conteudo = File.ReadAllText(arquivo);

        int concluir = conteudo.IndexOf("private void Concluir()", StringComparison.Ordinal);
        int validarPendente = conteudo.IndexOf(
            "if (!string.IsNullOrWhiteSpace(_pesoManualTextBox.Text))",
            concluir,
            StringComparison.Ordinal);
        int adicionarPendente = conteudo.IndexOf(
            "AdicionarPesoManual();",
            concluir,
            StringComparison.Ordinal);
        int validarLista = conteudo.IndexOf(
            "EntradaProdutoPesagemCalculos.PossuiLeituraValida(_pesagens)",
            concluir,
            StringComparison.Ordinal);

        Assert.True(concluir >= 0);
        Assert.True(validarPendente > concluir);
        Assert.True(adicionarPendente > validarPendente);
        Assert.True(validarLista > adicionarPendente);
    }

    [Fact]
    public void ConcluirEFechar_DevemRetornarDialogResultCorreto()
    {
        string conteudo = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");

        int configurarEventos = conteudo.IndexOf("_concluirButton.Click += (_, _) => Concluir();", StringComparison.Ordinal);
        int fechar = conteudo.IndexOf("_cancelarButton.Click += (_, _) =>", configurarEventos, StringComparison.Ordinal);
        int dialogCancel = conteudo.IndexOf("DialogResult = DialogResult.Cancel;", fechar, StringComparison.Ordinal);
        int concluir = conteudo.IndexOf("private void Concluir()", StringComparison.Ordinal);
        int dialogOk = conteudo.IndexOf("DialogResult = DialogResult.OK;", concluir, StringComparison.Ordinal);

        Assert.True(configurarEventos >= 0);
        Assert.True(fechar > configurarEventos);
        Assert.True(dialogCancel > fechar);
        Assert.True(concluir > configurarEventos);
        Assert.True(dialogOk > concluir);
    }

    [Fact]
    public void TelaEntrada_DevePreservarTaraAoRelocalizarLinha()
    {
        string arquivo = Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs");
        string conteudo = File.ReadAllText(arquivo);

        int localizarLinha = conteudo.IndexOf(
            "DataGridViewRow linhaAlvo = LocalizarLinhaProducaoPorItemId(itemId) ?? linhaItem;",
            StringComparison.Ordinal);
        int preservarTara = conteudo.IndexOf(
            "linhaAlvo.Tag = tara;",
            localizarLinha,
            StringComparison.Ordinal);
        int consolidarPeso = conteudo.IndexOf(
            "AtualizarTotaisDaLinha(linhaAlvo, _leiturasPorItem[codigoItem])",
            preservarTara,
            StringComparison.Ordinal);

        Assert.True(localizarLinha >= 0);
        Assert.True(preservarTara > localizarLinha);
        Assert.True(consolidarPeso > preservarTara);
    }

    [Fact]
    public void TelaEntrada_DeveImprimirEtiquetaSomenteAposConcluirPesagemMultipla()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        int dialogCancel = conteudo.IndexOf("form.ShowDialog(this) != DialogResult.OK", StringComparison.Ordinal);
        int retornoCancelado = conteudo.IndexOf("return;", dialogCancel, StringComparison.Ordinal);
        int consolidarPeso = conteudo.IndexOf(
            "AtualizarTotaisDaLinha(linhaAlvo, _leiturasPorItem[codigoItem])",
            retornoCancelado,
            StringComparison.Ordinal);
        int atualizarContadores = conteudo.IndexOf("UpdateProductionCounters();", consolidarPeso, StringComparison.Ordinal);
        int montarEtiqueta = conteudo.IndexOf("ConstruirDadosEtiquetaMateriaPrima(linhaAlvo)", atualizarContadores, StringComparison.Ordinal);
        int imprimir = conteudo.IndexOf("TentarImprimirEtiquetaAutomaticaAsync(etiqueta", montarEtiqueta, StringComparison.Ordinal);

        Assert.True(dialogCancel >= 0);
        Assert.True(retornoCancelado > dialogCancel);
        Assert.True(consolidarPeso > retornoCancelado);
        Assert.True(atualizarContadores > consolidarPeso);
        Assert.True(montarEtiqueta > atualizarContadores);
        Assert.True(imprimir > montarEtiqueta);
    }

    [Fact]
    public void TelaEntrada_FalhaImpressaoMultiplaNaoRemovePesoConsolidado()
    {
        string conteudo = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        int consolidarPeso = conteudo.IndexOf(
            "AtualizarTotaisDaLinha(linhaAlvo, _leiturasPorItem[codigoItem])",
            StringComparison.Ordinal);
        int imprimir = conteudo.IndexOf("TentarImprimirEtiquetaAutomaticaAsync(etiqueta", consolidarPeso, StringComparison.Ordinal);
        int falha = conteudo.IndexOf("mas a etiqueta", imprimir, StringComparison.Ordinal);
        int retornoFalha = conteudo.IndexOf("return;", falha, StringComparison.Ordinal);

        Assert.True(consolidarPeso >= 0);
        Assert.True(imprimir > consolidarPeso);
        Assert.True(falha > imprimir);
        Assert.True(retornoFalha > falha);
        Assert.DoesNotContain("_leiturasPorItem.Remove", conteudo.Substring(imprimir, retornoFalha - imprimir), StringComparison.Ordinal);
        Assert.DoesNotContain("productionPesoLidoColumn\", string.Empty", conteudo.Substring(imprimir, retornoFalha - imprimir), StringComparison.Ordinal);
    }

    [Fact]
    public void TelaEntrada_DeveGravarAntesDeAlterarEstadoVisual()
    {
        string arquivo = Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs");
        string conteudo = File.ReadAllText(arquivo);
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
