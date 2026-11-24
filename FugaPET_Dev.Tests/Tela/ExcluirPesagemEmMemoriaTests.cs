using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// "EXCLUIR PESAGEM" PRECISA EXCLUIR, OU DIZER POR QUE NÃO PODE.
/// <para>
/// Relato do usuário em 24/11/2026: clicou em "Excluir pesagem" no menu da janela de pesagens e
/// nada aconteceu. A pesagem ainda estava em memória (sem PK), e o tratador fazia um <c>return</c>
/// calado — sem apagar e sem explicar. Pior: o item do menu só era reavaliado DEPOIS de alguma
/// exclusão, então ficava no valor inicial (habilitado), convidando ao clique que não faz nada.
/// </para>
/// <para>
/// O caminho que funciona sempre existiu com outro nome — o botão "Cancelar leitura", que usa o
/// cancelamento canônico do controlador. O menu passou a usar a MESMA primitiva, em vez de remover
/// a linha por conta própria: quem manda na operação é o controlador, e apagar só na tela deixaria
/// a tela contando uma coisa e a operação, outra.
/// </para>
/// </summary>
public sealed class ExcluirPesagemEmMemoriaTests
{
    private static string Janela() => File.ReadAllText(
        Path.Combine(RaizProjeto(), "Tela", "Processo", "PesagemMultiplaItemForm.cs"));

    /// <summary>O <c>return</c> calado para pesagem sem PK não pode voltar.</summary>
    [Fact]
    public void PesagemEmMemoria_NaoCaiMaisEmRetornoCalado()
    {
        string janela = Janela();

        Assert.DoesNotContain(
            "return; // pesagem em memória (sem PK persistida) não é excluível por este fluxo.",
            janela,
            StringComparison.Ordinal);

        Assert.Contains("CancelarPesagemEmMemoriaSelecionadaAsync", janela, StringComparison.Ordinal);
    }

    /// <summary>
    /// O menu reusa o cancelamento canônico — a mesma primitiva do botão "Cancelar leitura" — e não
    /// mexe na lista por fora.
    /// </summary>
    [Fact]
    public void ExclusaoEmMemoria_ReusaOCancelamentoCanonico()
    {
        string janela = Janela();

        int inicio = janela.IndexOf(
            "private async Task CancelarPesagemEmMemoriaSelecionadaAsync", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "metodo de cancelamento em memoria nao encontrado");

        int fim = janela.IndexOf("private void AtualizarEstadoAcaoExcluirPesagem", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "fim do metodo nao encontrado");

        string corpo = janela[inicio..fim];
        Assert.Contains("CancelarPesoCanonicoAsync(indice)", corpo, StringComparison.Ordinal);
        Assert.Contains("MessageBox.Show", corpo, StringComparison.Ordinal);   // confirma antes
        Assert.DoesNotContain("_pesagensCanonicas.RemoveAt", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("_pesagens.RemoveAt", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O estado do item é recalculado AO ABRIR o menu. Sem isso ele fica no valor inicial e convida
    /// ao clique que não faz nada — que foi exatamente o relato.
    /// </summary>
    [Fact]
    public void EstadoDoItem_ERecalculadoAoAbrirOMenu()
    {
        Assert.Contains(
            "menu.Opening += (_, _) => AtualizarEstadoAcaoExcluirPesagem();",
            Janela(),
            StringComparison.Ordinal);
    }

    /// <summary>Pesagem já cancelada não se exclui de novo.</summary>
    [Fact]
    public void PesagemJaCancelada_NaoEOferecida()
    {
        string janela = Janela();

        Assert.Contains("jaCancelada", janela, StringComparison.Ordinal);
        Assert.Contains("excluivel = !jaCancelada", janela, StringComparison.Ordinal);
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
