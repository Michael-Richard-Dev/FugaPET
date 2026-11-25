using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O ICONE DE SEGURANCA NO MENU LATERAL (pedido do usuario em 25/11/2026, com print).
/// <para>
/// "Melhore o visual do icone de seguranca da tela inicial, veja que ele parece apagado perto dos
/// outros, deixe ele com o mesmo aspecto dos outros."
/// </para>
/// <para>
/// CAUSA: os outros tres desenham um GLIFO da fonte Segoe MDL2 Assets (U+E80F casa, U+E8FA pessoa,
/// U+E7B8 caixa), pintado pela ForeColor do rotulo. O de Seguranca desenhava um PNG
/// (seguranca_24x_white): bitmap de 24px esticado numa caixa de 28x40, com tracos finos e
/// antialiasing proprios, que NAO obedece a ForeColor. Ao lado de tres vetores nitidos, parecia
/// apagado.
/// </para>
/// <para>
/// O teste RENDERIZA os quatro icones da tela montada e compara o peso de tinta. Auditar o fonte
/// diria que a propriedade mudou, nao que o icone ficou parecido com os vizinhos.
/// </para>
/// </summary>
public sealed class PainelInicialIconeSegurancaTests
{
    /// <summary>
    /// O icone de Seguranca vem da MESMA FONTE e da MESMA COR dos outros, e nao de uma imagem. Uma
    /// imagem ignora a ForeColor, entao ela nunca acompanharia o menu se a cor mudar.
    /// </summary>
    [Fact]
    public void OIcone_EGlifoDaMesmaFonteECor_NaoImagem()
        => ComOsIcones(icones =>
        {
            Label seguranca = icones["menuSegurancaIcon"];
            Label referencia = icones["menuInicioIcon"];

            Assert.Null(seguranca.Image);
            Assert.False(string.IsNullOrEmpty(seguranca.Text), "o icone ficou sem glifo nenhum");
            Assert.Equal(referencia.Font.FontFamily.Name, seguranca.Font.FontFamily.Name);
            Assert.Equal(referencia.Font.Size, seguranca.Font.Size);
            Assert.Equal(
                icones["menuCadastroIcon"].ForeColor.ToArgb(),
                seguranca.ForeColor.ToArgb());
        });

    /// <summary>
    /// O GLIFO EXISTE NA FONTE. Um ponto de codigo invalido na area de uso privado nao da erro: a
    /// fonte desenha um retangulo vazio, e o menu ficaria com um quadradinho no lugar do cadeado.
    /// Aqui se compara com dois pontos de codigo sabidamente ausentes, que caem no MESMO desenho.
    /// </summary>
    [Fact]
    public void OGlifo_ExisteNaFonte()
        => ComOsIcones(icones =>
        {
            Label seguranca = icones["menuSegurancaIcon"];

            int ausente = Tinta("", seguranca.Font);
            int outroAusente = Tinta("", seguranca.Font);

            Assert.Equal(ausente, outroAusente);   // os dois caem no mesmo "glifo ausente"
            Assert.NotEqual(ausente, Tinta(seguranca.Text, seguranca.Font));
        });

    /// <summary>
    /// NAO FICA APAGADO: o peso de tinta do cadeado esta na mesma faixa dos tres vizinhos. Era
    /// exatamente isso que o usuario viu — um icone mais fraco que os outros.
    /// </summary>
    [Fact]
    public void OIcone_TemOMesmoPesoDosVizinhos()
        => ComOsIcones(icones =>
        {
            string[] vizinhos = ["menuInicioIcon", "menuCadastroIcon", "menuLeituraIcon"];
            int[] pesos = [.. vizinhos.Select(n => Tinta(icones[n].Text, icones[n].Font))];

            Label seguranca = icones["menuSegurancaIcon"];
            int peso = Tinta(seguranca.Text, seguranca.Font);

            Assert.InRange(peso, pesos.Min() / 2, pesos.Max() * 2);
        });

    // ------------------------------------------------- apoio

    /// <summary>Pixels acesos ao desenhar o glifo em branco sobre fundo preto.</summary>
    private static int Tinta(string glifo, Font fonte)
    {
        using Bitmap bmp = new(40, 40);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            TextRenderer.DrawText(
                g, glifo, fonte, new Rectangle(0, 0, 40, 40), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        int acesos = 0;
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                if (bmp.GetPixel(x, y).R > 60)
                {
                    acesos++;
                }
            }
        }

        return acesos;
    }

    private static void ComOsIcones(Action<Dictionary<string, Label>> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                using PainelInicialForm painel = new();

                Panel sidebar = (Panel)painel.GetType()
                    .GetField("sidebarPanel", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(painel)!;

                Dictionary<string, Label> icones = sidebar.Controls
                    .OfType<Panel>()
                    .Where(p => p.Name.StartsWith("menuItem", StringComparison.Ordinal))
                    .SelectMany(p => p.Controls.OfType<Label>())
                    .Where(l => l.Name.EndsWith("Icon", StringComparison.Ordinal))
                    .ToDictionary(l => l.Name, l => l);

                verificar(icones);
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
}
