using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O MENU LATERAL DEPOIS DE TIRAR "ETIQUETAS" (pedido do usuario em 25/11/2026, com print).
/// <para>
/// "Remova esse botao de etiqueta que esta na tela inicial, ajuste os outros botoes para cima, para
/// nao ficar com um buraco no meio entre eles."
/// </para>
/// <para>
/// As duas metades importam: o item tem de sumir E os de baixo tem de subir. Remover sem
/// reempilhar deixaria exatamente o buraco que ele mandou evitar.
/// </para>
/// </summary>
public sealed class PainelInicialMenuSemEtiquetasTests
{
    /// <summary>O item "Etiquetas" nao existe mais no menu — nem escondido.</summary>
    [Fact]
    public void Etiquetas_NaoEstaMaisNoMenu()
        => ComAMenuMontado(itens =>
            Assert.DoesNotContain(itens, item => Texto(item).Contains("Etiqueta", StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// NAO FICA BURACO: o vao entre itens consecutivos e sempre o mesmo. Era por aqui que a remocao
    /// poderia dar errado — tirar o controle e deixar os de baixo onde estavam.
    /// </summary>
    [Fact]
    public void NaoSobraBuracoEntreOsItens()
        => ComAMenuMontado(itens =>
        {
            Control[] ordenados = [.. itens.OrderBy(i => i.Top)];
            int[] vaos = [.. ordenados.Zip(ordenados.Skip(1), (cima, baixo) => baixo.Top - cima.Bottom)];

            Assert.NotEmpty(vaos);
            Assert.True(
                vaos.Distinct().Count() == 1,
                $"os vaos entre os itens deveriam ser todos iguais e sao: {string.Join(", ", vaos)}");
            Assert.All(vaos, vao => Assert.True(vao >= 0, "itens do menu nao podem se sobrepor"));
        });

    /// <summary>
    /// Os OUTROS modulos continuam no menu. Sem esta guarda, um menu vazio passaria nas duas
    /// primeiras: nao ha "Etiquetas" e nao ha buraco.
    /// </summary>
    [Theory]
    [InlineData("Início")]
    [InlineData("Cadastro")]
    [InlineData("Processo de Produção")]
    [InlineData("Histórico")]
    [InlineData("Relatórios")]
    [InlineData("Integração SAP")]
    [InlineData("Configurações")]
    [InlineData("Segurança")]
    public void OsDemaisModulos_ContinuamNoMenu(string modulo)
        => ComAMenuMontado(itens =>
            Assert.Contains(itens, item => Texto(item).Contains(modulo, StringComparison.Ordinal)));

    /// <summary>
    /// Nada ficou pela metade: o item sumiu do Designer e do codigo, e o metodo que abria a consulta
    /// de etiqueta foi junto. Metodo privado sem quem o chame e codigo morto.
    /// </summary>
    [Fact]
    public void NaoSobrouFiacaoOrfa()
    {
        foreach (string arquivo in new[] { "PainelInicialForm.cs", "PainelInicialForm.Designer.cs" })
        {
            string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", arquivo));

            Assert.DoesNotContain("menuItemEtiquetas", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("menuEtiquetasIcon", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("menuEtiquetasText", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("OpenConsultaEtiquetaAsync", fonte, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------- apoio

    private static string Texto(Control item)
        => string.Join(" ", item.Controls.OfType<Label>().Select(l => l.Text));

    /// <summary>
    /// Monta o painel e roda o reempilhamento. Ele so acontece no Shown, entao sem chama-lo o teste
    /// leria as posicoes do Designer — e nao o que a tela de fato mostra.
    /// </summary>
    private static void ComAMenuMontado(Action<Control[]> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                using PainelInicialForm painel = new();

                MethodInfo reempilhar = painel.GetType().GetMethod(
                    "ReempilharSidebar", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? throw new InvalidOperationException("ReempilharSidebar nao encontrado");

                reempilhar.Invoke(painel, null);

                Panel sidebar = (Panel)painel.GetType()
                    .GetField("sidebarPanel", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(painel)!;

                // Os itens de menu sao os paineis que hospedam icone + rotulo. O bloco do usuario,
                // no rodape, nao e item de menu e fica de fora pelo nome.
                Control[] itens = [.. sidebar.Controls
                    .OfType<Panel>()
                    .Where(p => p.Name.StartsWith("menuItem", StringComparison.Ordinal))];

                verificar(itens);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                erro = ex.InnerException;
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });

        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();

        if (erro is not null)
        {
            throw erro;
        }
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
