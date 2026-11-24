using Xunit;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// A TRAVA TEM DE ESTAR NO CAMINHO QUE A TELA REALMENTE USA.
/// <para>
/// Em 24/11/2026 descobriu-se que a trava de teto do incremento 069 vivia em
/// <c>AdicionarLeituraNaLinha</c>, chamado unicamente por <c>RegistrarPesoLido</c> — método que nada
/// mais chama desde a mudança para pesagem com lotes. A trava estava publicada e <b>nunca bloqueou
/// nada</b>; só o piso, cobrado na finalização, estava de pé.
/// </para>
/// <para>
/// Estes testes ancoram nas CHAMADAS, não em recortes de texto entre métodos: um recorte entre duas
/// declarações já me enganou antes, porque qualquer método novo que caia no meio muda o recorte sem
/// mudar o comportamento.
/// </para>
/// </summary>
public sealed class ToleranciaNoCaminhoVivoTests
{
    private static string Form() => File.ReadAllText(
        Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));

    /// <summary>O caminho VIVO da pesagem chama a verificação com liberação gerencial.</summary>
    [Fact]
    public void PesagemViva_ChamaAVerificacaoComLiberacao()
    {
        string form = Form();

        Assert.Contains("await ToleranciaLiberaPesagemAsync(", form, StringComparison.Ordinal);
        // E ela e chamada de dentro do metodo vivo, nao de qualquer lugar.
        int vivo = form.IndexOf(
            "private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync(",
            StringComparison.Ordinal);
        int trava = form.IndexOf("await ToleranciaLiberaPesagemAsync(", StringComparison.Ordinal);
        Assert.True(vivo >= 0, "metodo vivo da pesagem nao encontrado");
        Assert.True(trava > vivo, "a trava nao aparece depois do inicio do metodo vivo");
    }

    /// <summary>A finalização oferece a liberação, e não só a caixa de bloqueio de antes.</summary>
    [Fact]
    public void Finalizacao_ChamaAVerificacaoComLiberacao()
    {
        string form = Form();

        Assert.Contains("await ToleranciaLiberaFinalizacaoAsync()", form, StringComparison.Ordinal);
        // A chamada antiga, que bloqueava sem oferecer nada, nao pode voltar ao ponto de uso.
        Assert.DoesNotContain(
            "if (!ToleranciaPermiteFinalizar(out string motivoFinalizacao))", form, StringComparison.Ordinal);
    }

    /// <summary>
    /// O método morto continua marcado como morto. Se alguém o religar sem reler o aviso, a trava de
    /// lá volta a parecer proteção — e foi exatamente essa aparência que escondeu o defeito.
    /// </summary>
    [Fact]
    public void OCaminhoMortoSegueMarcadoComoMorto()
    {
        string form = Form();

        Assert.Contains("ESTE METODO ESTA MORTO", form, StringComparison.Ordinal);
        Assert.Contains("NUNCA BLOQUEOU NADA", form, StringComparison.Ordinal);
    }

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
