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
}
