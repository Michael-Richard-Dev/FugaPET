using System.Drawing;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A PALETA DOS CARTOES DE CADASTRO (pedido do usuario em 24/11/2026, com print da tela).
/// <para>
/// Nas palavras dele: "os modulos estao com cores diferentes... preserve o Disponivel apenas com
/// sua cor verde, as demais cores tem que ser alteradas para laranja escuro e laranja claro, as
/// cores que estamos usando de paleta de cores nesse sistema".
/// </para>
/// <para>
/// Eram QUATRO tons na mesma grade: rosa #FEE2E2 (resto do vermelho de antes do reskin laranja) nos
/// sete cartoes do Designer, pessego no Vinculo, verde-agua na Tolerancia de Recebimento e azul na
/// de Producao. Os tres ultimos foram escolha minha, para os cartoes clonados nao virarem um par
/// indistinguivel — e o preco foi a tela parecer de outro sistema.
/// </para>
/// </summary>
public sealed class CadastroCartoesPaletaTests
{
    private static readonly Color LaranjaEscuro = Color.FromArgb(200, 78, 10);

    // ------------------------------------------------- o que muda

    /// <summary>
    /// O ladrilho e uma TINTA CLARA do laranja da paleta: avermelhado, bem mais claro que o laranja
    /// escuro, e nunca um tom frio. Cobra-se a familia da cor, nao o hex — trocar a tinta por outra
    /// laranja tao clara continua atendendo ao pedido.
    /// </summary>
    [Fact]
    public void OLadrilho_EUmaTintaClaraDeLaranja()
    {
        Color ladrilho = CadastroForm.LadrilhoDoIcone;

        Assert.True(ladrilho.R > ladrilho.G && ladrilho.G > ladrilho.B, "a cor tem de ser alaranjada (R > G > B)");
        Assert.True(ladrilho.GetBrightness() > 0.80, "o ladrilho e uma TINTA: claro o bastante para o icone escuro aparecer");
        Assert.InRange(ladrilho.GetHue(), 15f, 40f);
    }

    /// <summary>
    /// O ICONE CONTINUA LEGIVEL sobre o ladrilho. Foi por isso que os dois tons cheios da paleta
    /// (#FA691A de fundo com #C84E0A de icone) foram recusados: dao 2,1:1 e o icone sumiria.
    /// <para>
    /// O limite e 3:1, o da WCAG para GRAFICOS e componentes (1.4.11), nao os 4,5:1 de texto
    /// corrido — aqui o alvo e um glifo de 22pt, nao um paragrafo. E nao e folga concedida: com um
    /// icone no laranja escuro da paleta, 4,5:1 seria inalcancavel mesmo com o ladrilho BRANCO
    /// (daria 4,6 no limite teorico e qualquer tinta ja fica abaixo). Exigir 4,5 aqui obrigaria a
    /// trocar a cor do icone, que nao foi o pedido.
    /// </para>
    /// </summary>
    [Fact]
    public void OIcone_ContinuaLegivelSobreOLadrilho()
    {
        double contraste = Contraste(LaranjaEscuro, CadastroForm.LadrilhoDoIcone);

        Assert.True(contraste >= 3.0, $"contraste icone/ladrilho ficou em {contraste:0.0}:1");
    }

    /// <summary>
    /// A ALTERNATIVA LITERAL ERA PIOR, e isso fica registrado: ladrilho #FA691A com icone #C84E0A —
    /// os dois tons da paleta, ao pe da letra — nao chega nem ao limite de graficos.
    /// </summary>
    [Fact]
    public void OsDoisTonsCheios_ReprovariamNoContraste()
    {
        double contraste = Contraste(LaranjaEscuro, Color.FromArgb(250, 105, 26));

        Assert.True(contraste < 3.0, $"a alternativa recusada daria {contraste:0.0}:1");
    }

    /// <summary>
    /// A COR E APLICADA PERCORRENDO OS CARTOES, nao repetida cartao a cartao. Dez literais iguais
    /// sao dez chances de divergir — e foi assim que a grade acabou com quatro tons.
    /// </summary>
    [Fact]
    public void ACor_SaiDeUmLugarSo()
    {
        string fonte = Fonte();

        Assert.Contains("foreach (Control cartao in modulesFlowLayoutPanel.Controls)", fonte, StringComparison.Ordinal);
        Assert.Contains("ladrilho.FillColor = LadrilhoDoIcone;", fonte, StringComparison.Ordinal);

        // O parametro de cor por cartao deixou de existir: nao ha mais onde escolher um tom proprio.
        Assert.DoesNotContain("corDoIcone", fonte, StringComparison.Ordinal);
    }

    /// <summary>Os tons rosa, pessego, verde-agua e azul nao podem voltar a esta tela.</summary>
    [Theory]
    [InlineData("254, 226, 226")]   // rosa dos sete do Designer
    [InlineData("254, 240, 221")]   // pessego do Vinculo
    [InlineData("222, 247, 236")]   // verde-agua da Tolerancia de Recebimento
    [InlineData("219, 234, 254")]   // azul da Tolerancia de Producao
    public void OsTonsAntigos_NaoVoltam(string tomAntigo)
    {
        Assert.DoesNotContain(tomAntigo, Fonte(), StringComparison.Ordinal);

        // O Designer tambem: o repinte de runtime cobriria o rosa, mas deixar o valor velho ali faz
        // o arquivo mentir para quem abre a tela no Visual Studio.
        Assert.DoesNotContain($"IconPanel.FillColor = Color.FromArgb({tomAntigo});", Designer(), StringComparison.Ordinal);
    }

    /// <summary>Os sete ladrilhos do Designer nascem ja na cor nova, e nao no rosa de antes.</summary>
    [Fact]
    public void ODesigner_NasceComOLadrilhoCerto()
    {
        Color c = CadastroForm.LadrilhoDoIcone;
        string esperado = $"IconPanel.FillColor = Color.FromArgb({c.R}, {c.G}, {c.B});";

        int quantos = Designer().Split(esperado).Length - 1;

        Assert.Equal(7, quantos);
    }

    // ------------------------------------------------- o que NAO muda

    /// <summary>
    /// O "DISPONIVEL" VERDE E INTOCADO — foi a unica cor que o usuario mandou preservar. Repintar o
    /// selo de situacao de laranja apagaria a distincao entre "disponivel" e o resto do cartao.
    /// </summary>
    [Fact]
    public void ODisponivel_ContinuaVerde()
    {
        string fonte = Fonte();
        string designer = Designer();

        // Verde no Designer (fundo #DCFCE7, texto #16A34A) e nenhum repinte no codigo.
        Assert.Contains("StatusLabel.BackColor = Color.FromArgb(220, 252, 231);", designer, StringComparison.Ordinal);
        Assert.Contains("StatusLabel.ForeColor = Color.FromArgb(22, 163, 74);", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusLabel.BackColor =", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusLabel.ForeColor =", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// O repinte alcanca SO o ladrilho do icone. Varrer o cartao inteiro trocando cor apagaria o
    /// selo verde e o titulo escuro junto.
    /// </summary>
    [Fact]
    public void ORepinte_AlcancaSoOLadrilho()
    {
        string fonte = Fonte();

        int ondeVarre = fonte.IndexOf("foreach (Control parte in cartao.Controls)", StringComparison.Ordinal);
        int fim = fonte.IndexOf("    /// <summary>", ondeVarre, StringComparison.Ordinal);
        string corpo = fonte[ondeVarre..fim];

        Assert.Contains("if (parte is Controls.RoundedPanel ladrilho)", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("ForeColor", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("BackColor", corpo, StringComparison.Ordinal);
    }

    /// <summary>
    /// O icone e a seta ja eram o laranja escuro da paleta no Designer: o defeito era so o ladrilho.
    /// Este teste existe para ninguem "corrigir" o que ja estava certo.
    /// </summary>
    [Fact]
    public void IconeESeta_JaEramOLaranjaDaPaleta()
    {
        string designer = Designer();

        Assert.Contains("IconLabel.ForeColor = Color.FromArgb(200, 78, 10);", designer, StringComparison.Ordinal);
        Assert.Contains("ArrowLabel.ForeColor = Color.FromArgb(200, 78, 10);", designer, StringComparison.Ordinal);
    }

    // ------------------------------------------------- apoio

    /// <summary>Contraste WCAG entre duas cores opacas.</summary>
    private static double Contraste(Color a, Color b)
    {
        double la = Luminancia(a);
        double lb = Luminancia(b);

        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminancia(Color c)
    {
        static double Canal(int v)
        {
            double s = v / 255d;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Canal(c.R)) + (0.7152 * Canal(c.G)) + (0.0722 * Canal(c.B));
    }

    private static string Fonte() => Ler("CadastroForm.cs");

    private static string Designer() => Ler("CadastroForm.Designer.cs");

    private static string Ler(string arquivo)
        => File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", arquivo));

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
