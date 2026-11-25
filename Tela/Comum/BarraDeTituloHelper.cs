using System.Runtime.InteropServices;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// O COMPORTAMENTO DA BARRA DE TITULO PROPRIA: realce ao passar o mouse nos botoes de janela e
/// arrastar a janela pelo cabecalho.
/// <para>
/// POR QUE EXISTE (pedido do usuario em 25/11/2026): "tem esse efeito que esta funcionando na tela
/// inicial, com os botoes de minimizar, maximizar e sair, quero que coloque esse efeito em todas as
/// telas possiveis; a tela de cadastro de setor e uma das que nao funciona".
/// </para>
/// <para>
/// As telas que ja tinham o efeito carregavam uma COPIA do mesmo metodo privado, e cada copia
/// recebia a cor do realce como literal. Nove telas simplesmente nao receberam a copia — o defeito
/// relatado. Copia e o que faz uma tela ficar para tras sem ninguem notar.
/// </para>
/// </summary>
internal static class BarraDeTituloHelper
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    /// <summary>Realce do cabecalho laranja, o mesmo da tela inicial que o usuario apontou.</summary>
    private static readonly Color RealceLaranja = Color.FromArgb(160, 55, 5);

    /// <summary>Realce dos cabecalhos escuros, o mesmo que as telas de consulta ja usam.</summary>
    private static readonly Color RealceEscuro = Color.FromArgb(36, 46, 61);

    /// <summary>
    /// A cor de realce para um cabecalho, pela regra que o sistema JA pratica: cabecalho laranja
    /// ESCURECE, cabecalho escuro CLAREIA. Nos dois casos o realce se afasta da cor de fundo, que e
    /// o que faz o botao aparecer sob o mouse.
    /// <para>
    /// Os dois tons conhecidos sao devolvidos exatamente como ja estao nas telas que funcionam, para
    /// o efeito ficar identico e nao "parecido". Um cabecalho de cor nova cai na regra geral, em vez
    /// de ficar sem realce nenhum.
    /// </para>
    /// </summary>
    internal static Color RealceParaCabecalho(Color cabecalho)
    {
        if (cabecalho.ToArgb() == Color.FromArgb(200, 78, 10).ToArgb())
        {
            return RealceLaranja;
        }

        if (cabecalho.GetBrightness() < 0.25f)
        {
            return RealceEscuro;
        }

        return Escurecer(cabecalho, 0.2f);
    }

    /// <summary>
    /// Liga o realce nos botoes de janela, tirando a cor do PROPRIO cabecalho.
    /// <para>
    /// A cor nao e parametro de proposito: era assim que cada tela podia errar a sua. Passa-se o
    /// cabecalho, e a regra decide.
    /// </para>
    /// </summary>
    internal static void AplicarRealceDosBotoes(Control cabecalho, params Label[] botoes)
    {
        Color realce = RealceParaCabecalho(cabecalho.BackColor);

        foreach (Label botao in botoes)
        {
            Color normal = botao.BackColor;

            botao.MouseEnter += (_, _) => botao.BackColor = realce;
            botao.MouseLeave += (_, _) => botao.BackColor = normal;
        }
    }

    /// <summary>
    /// Permite arrastar a janela pelo cabecalho: avisa o Windows que o clique ali vale como clique
    /// na barra de titulo nativa. E o proprio gerenciador de janelas que move, entao o arrasto
    /// acompanha encaixe nas bordas e monitor secundario sem codigo nosso.
    /// </summary>
    internal static void PermitirArrastarPeloCabecalho(Form janela, params Control[] areas)
    {
        foreach (Control area in areas)
        {
            area.MouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                ReleaseCapture();
                SendMessage(janela.Handle, WmNcLButtonDown, HtCaption, 0);
            };
        }
    }

    private static Color Escurecer(Color cor, float fator)
        => Color.FromArgb(
            cor.A,
            (int)Math.Round(cor.R * (1 - fator)),
            (int)Math.Round(cor.G * (1 - fator)),
            (int)Math.Round(cor.B * (1 - fator)));
}
