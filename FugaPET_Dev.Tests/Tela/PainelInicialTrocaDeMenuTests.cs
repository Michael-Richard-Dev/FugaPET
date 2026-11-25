using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A TROCA DE ITEM DO MENU NAO PODE PISCAR (relatado pelo usuario em 25/11/2026).
/// <para>
/// "Quando eu altero entre os botoes na tela inicial, fica com um efeito meio que de gap, quebra de
/// cores... principalmente no botao de cadastro e mais perceptivel."
/// </para>
/// <para>
/// Cada guarda aqui FALHA contra o codigo antigo — conferido. Tentei tambem contar invalidacoes
/// de controle, e os testes passavam nas DUAS versoes: o WinForms ja ignora atribuicao de cor ou
/// fonte com valor igual ao atual, entao nao havia cascata a medir. Guarda que passa dos dois
/// lados nao prova nada, e saiu.
/// </para>
/// </summary>
public sealed class PainelInicialTrocaDeMenuTests
{
    /// <summary>
    /// O ITEM INATIVO PINTA O PROPRIO FUNDO, na cor da barra, em vez de ficar
    /// <c>Color.Transparent</c> — que no WinForms faz o filho pedir ao PAI que pinte atras dele, e
    /// e o que encadeava a repintura. A cor e LIDA da barra, nao um segundo literal.
    /// </summary>
    [Fact]
    public void ItemInativo_PintaOProprioFundoNaCorDaBarra()
        => ComOMenu((painel, itens, trocar) =>
        {
            trocar(itens[0]);

            Panel sidebar = Sidebar(painel);

            foreach (Panel item in itens.Skip(1))
            {
                Assert.Equal(sidebar.BackColor.ToArgb(), item.BackColor.ToArgb());
                Assert.NotEqual(Color.Transparent.ToArgb(), item.BackColor.ToArgb());
            }
        });

    /// <summary>
    /// A REGIAO ARREDONDADA NAO E REFEITA a cada troca: o reempilhamento ja a aplica a todos os
    /// itens no tamanho final. Atribuir <c>Region</c> forca o pai a repintar aquela area.
    /// </summary>
    [Fact]
    public void ARegiao_NaoERefeitaACadaTroca()
        => ComOMenu((painel, itens, trocar) =>
        {
            trocar(itens[0]);
            Region? antes = itens[1].Region;

            trocar(itens[1]);

            Assert.Same(antes, itens[1].Region);
            Assert.NotNull(itens[1].Region);   // e os itens TEM regiao, vinda do reempilhamento
        });

    /// <summary>
    /// A janela pede composicao ao Windows (WS_EX_COMPOSITED): filhos desenhados fora da tela e
    /// mostrados prontos. Sem isso, o conteudo ao lado — o caso do Cadastro — continuaria rasgando,
    /// porque arrumar so o menu nao arruma o painel que e remontado.
    /// </summary>
    [Fact]
    public void AJanela_PedeComposicaoAoWindows()
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                using PainelInicialForm painel = new();

                PropertyInfo prop = typeof(Form).GetProperty(
                    "CreateParams", BindingFlags.NonPublic | BindingFlags.Instance)!;
                CreateParams p = (CreateParams)prop.GetValue(painel)!;

                const int wsExComposited = 0x02000000;
                Assert.Equal(wsExComposited, p.ExStyle & wsExComposited);
            }
            catch (Exception ex) { erro = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (erro is not null) throw erro;
    }

    // ------------------------------------------------- apoio

    private static Panel Sidebar(PainelInicialForm painel)
        => (Panel)painel.GetType()
            .GetField("sidebarPanel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(painel)!;

    /// <summary>
    /// Monta o painel e entrega os itens do menu e um jeito de marcar o ativo. Chama-se
    /// SetActiveSidebarMenuItem direto, e nao o clique: o clique tambem troca o CONTEUDO, que vai ao
    /// banco. O que esta sob teste e o efeito visual da marcacao.
    /// </summary>
    private static void ComOMenu(Action<PainelInicialForm, Panel[], Action<Panel>> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                using PainelInicialForm painel = new();

                MethodInfo reempilhar = painel.GetType().GetMethod(
                    "ReempilharSidebar", BindingFlags.NonPublic | BindingFlags.Instance)!;
                reempilhar.Invoke(painel, null);

                MethodInfo marcar = painel.GetType().GetMethod(
                    "SetActiveSidebarMenuItem", BindingFlags.NonPublic | BindingFlags.Instance)!;

                Panel[] itens = [.. Sidebar(painel).Controls
                    .OfType<Panel>()
                    .Where(p => p.Name.StartsWith("menuItem", StringComparison.Ordinal))
                    .OrderBy(p => p.Top)];

                Assert.True(itens.Length >= 2, "o menu precisa de ao menos dois itens para trocar");

                verificar(painel, itens, item => marcar.Invoke(painel, [item]));
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                erro = ex.InnerException;
            }
            catch (Exception ex) { erro = ex; }
        });

        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        if (erro is not null)
        {
            throw erro;
        }
    }
}
