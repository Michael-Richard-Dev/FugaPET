namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 046-E REV2 (promoção HML): certifica na Form que BANCO é fonte autoritativa do palete local —
/// recarga persistente ao criar/carregar OP e AUSÊNCIA do fallback de memória `_paletesMontados.Add(palete)`
/// no caminho sucesso+reload-falhou. Fonte-scan (sem instanciar WinForms).
/// </summary>
public sealed class ProdutoAcabadoPaleteFormRev2Tests
{
    [Fact]
    public void Form_BancoAutoritativo_SemFallbackDeMemoria()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // Reload persistente presente (criar + carregar OP).
        Assert.Contains("private async Task<bool> RecarregarPaletesPersistidosAsync()", form, StringComparison.Ordinal);
        Assert.Contains("await _controller.RecarregarPaletesLocaisAsync(_ordemAtual.NumeroOrdem, terminal)", form, StringComparison.Ordinal);
        Assert.Contains("await RecarregarPaletesPersistidosAsync();", form, StringComparison.Ordinal);

        // Blocker 2: fallback de memória REMOVIDO; fail-closed com mensagem clara quando o reload falha.
        Assert.DoesNotContain("_paletesMontados.Add(palete)", form, StringComparison.Ordinal);
        Assert.Contains("Palete PERSISTIDO em RASCUNHO, mas a atualização da tela falhou", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Form_ReloadDaCaixa_RefletirVinculoDoBanco_SemInferenciaPorIntervalo()
    {
        string form = LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // GATE 046-H: existe a reconciliação e ela é aplicada ao carregar OP e após criar (só quando o reload deu certo).
        Assert.Contains("private void AplicarVinculoPaleteDasCaixasReconstruido()", form, StringComparison.Ordinal);
        Assert.Contains("if (paletesRecarregados) { AplicarVinculoPaleteDasCaixasReconstruido(); }", form, StringComparison.Ordinal);
        Assert.Contains("if (recarregou) { AplicarVinculoPaleteDasCaixasReconstruido(); }", form, StringComparison.Ordinal);

        // Autoridade: casa pela composição persistida via identificador (CodigoProdutoAcabadoCaixa), grava CodigoPaleteLocal do palete.
        Assert.Contains("caixaGrade.CodigoProdutoAcabadoCaixa == codigoCaixa", form, StringComparison.Ordinal);
        Assert.Contains("caixaGrade.CodigoPaleteLocal = palete.CodigoPaleteLocal", form, StringComparison.Ordinal);
        // Não infere vínculo por intervalo/primeira-última dentro da reconciliação.
        int corpo = form.IndexOf('{', form.IndexOf("private void AplicarVinculoPaleteDasCaixasReconstruido()", StringComparison.Ordinal));
        string metodo = form.Substring(corpo, 900);
        Assert.DoesNotContain("PrimeiraCaixa", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("UltimaCaixa", metodo, StringComparison.Ordinal);
    }

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}
