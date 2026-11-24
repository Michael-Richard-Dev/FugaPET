using System.Drawing;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A MARCA CENTRAL DA TELA INICIAL (pedido do usuario em 24/11/2026).
/// <para>
/// Comparando as duas telas lado a lado: "a do FugaPEC esta muito mais vistosa do que a do
/// FugaPET... e replicar a ESTRUTURA, o logo continua o do FugaPET normal".
/// </para>
/// <para>
/// O que encolhia a marca aqui nao era o logo: era o Padding(62, 18, 62, 18) que o Designer poe no
/// PictureBox — numa caixa de 388x112 sobravam 264x76 para a imagem —, a caixa pequena, e o cartao
/// branco arredondado desenhado sobre o fundo cinza, que vira moldura em volta de um logo miudo.
/// </para>
/// </summary>
public sealed class PainelInicialMarcaCentralTests
{
    // ------------------------------------------------- o tamanho

    /// <summary>
    /// A 96 DPI o resultado e exatamente o desenho do FugaPEC (520x184) — a replicacao e fiel, nao
    /// uma reinterpretacao.
    /// </summary>
    [Fact]
    public void A96Dpi_ETamanhoDoFugaPec()
        => Assert.Equal(new Size(520, 184), PainelInicialForm.CalcularTamanhoDaMarca(1f, larguraDisponivel: 4000));

    /// <summary>
    /// ACOMPANHA O DPI. La o 520x184 e cravado; aqui nao, porque pixel cravado e exatamente o que
    /// quebra a tela em outra maquina — foi a causa do aviso do rodape cobrir o nome do usuario.
    /// </summary>
    [Theory]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void AcompanhaODpi(float fatorDpi)
    {
        Size tamanho = PainelInicialForm.CalcularTamanhoDaMarca(fatorDpi, larguraDisponivel: 10000);

        Assert.Equal((int)Math.Round(520 * fatorDpi, MidpointRounding.AwayFromZero), tamanho.Width);
        Assert.True(tamanho.Height > 184, "a altura tem de crescer junto com a largura");
    }

    /// <summary>
    /// Em janela estreita a marca CEDE mantendo a proporcao, em vez de ser cortada pela celula. Um
    /// logo achatado seria pior que um logo pequeno.
    /// </summary>
    [Theory]
    [InlineData(1f, 300)]
    [InlineData(1.5f, 400)]
    [InlineData(2f, 200)]
    public void EmJanelaEstreita_CedeMantendoAProporcao(float fatorDpi, int larguraDisponivel)
    {
        Size tamanho = PainelInicialForm.CalcularTamanhoDaMarca(fatorDpi, larguraDisponivel);

        Assert.True(tamanho.Width <= larguraDisponivel, "a marca nao pode passar da largura disponivel");
        Assert.True(tamanho.Width >= 1 && tamanho.Height >= 1, "tamanho tem de ser valido para o WinForms");

        double proporcao = tamanho.Width / (double)tamanho.Height;
        Assert.InRange(proporcao, (520d / 184d) - 0.05, (520d / 184d) + 0.05);
    }

    /// <summary>A marca nova e MAIOR que a caixa antiga do Designer (388x112) — era essa a queixa.</summary>
    [Fact]
    public void EMaiorQueACaixaAntigaDoDesigner()
    {
        Size tamanho = PainelInicialForm.CalcularTamanhoDaMarca(1f, larguraDisponivel: 4000);

        Assert.True(tamanho.Width > 388 && tamanho.Height > 112);
    }

    // ------------------------------------------------- a estrutura copiada do FugaPEC

    /// <summary>
    /// O PADDING DO DESIGNER E ZERADO. Era ele o principal encolhedor: 62px de cada lado e 18 em
    /// cima e embaixo saiam do espaco da imagem, nao da caixa.
    /// </summary>
    [Fact]
    public void OPictureBox_PerdeOPaddingEOcupaOPainelInteiro()
    {
        string fonte = Fonte();

        Assert.Contains("contentBrandPictureBox.Padding = Padding.Empty;", fonte, StringComparison.Ordinal);
        Assert.Contains("contentBrandPictureBox.Location = Point.Empty;", fonte, StringComparison.Ordinal);
        Assert.Contains("contentBrandPictureBox.Size = contentBrandHeaderPanel.Size;", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// ZOOM, nao Stretch: o pedido foi manter o logo do FugaPET. Stretch encheria a caixa deformando
    /// a marca, que e o oposto de "mais vistosa".
    /// </summary>
    [Fact]
    public void OLogo_NaoEDeformado()
    {
        string fonte = Fonte();

        Assert.Contains("contentBrandPictureBox.SizeMode = PictureBoxSizeMode.Zoom;", fonte, StringComparison.Ordinal);
        Assert.DoesNotContain("PictureBoxSizeMode.StretchImage", fonte, StringComparison.Ordinal);
        // O logo continua o do FugaPET: nada de trocar o asset.
        Assert.Contains("Resources.fuga_2026_logo", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// O CARTAO SOME CONTRA O FUNDO — branco sobre branco, sem raio e sem borda — em vez de ser
    /// removido do layout. A celula continua a mesma; so a moldura deixa de ser vista.
    /// </summary>
    [Fact]
    public void OCartao_SomeContraOFundoBranco()
    {
        string fonte = Fonte();

        Assert.Contains("contentScrollPanel.BackColor = Color.White;", fonte, StringComparison.Ordinal);
        Assert.Contains("contentLayout.BackColor = Color.White;", fonte, StringComparison.Ordinal);
        Assert.Contains("contentBrandHeaderPanel.FillColor = Color.White;", fonte, StringComparison.Ordinal);
        Assert.Contains("contentBrandHeaderPanel.BorderColor = Color.White;", fonte, StringComparison.Ordinal);
        Assert.Contains("contentBrandHeaderPanel.BorderRadius = 0;", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// O ajuste roda DEPOIS da escala de fonte 2.0x e do AdaptarAlturasParaFonte, que mexem nos
    /// tamanhos do corpo. Rodar antes teria a geometria sobrescrita logo em seguida.
    /// </summary>
    [Fact]
    public void OAjuste_RodaDepoisDaEscalaDeFonte()
    {
        string fonte = Fonte();

        int ondeEscala = fonte.IndexOf("AdaptarAlturasParaFonte(corpo);", StringComparison.Ordinal);
        int ondeMarca = fonte.IndexOf("AjustarMarcaCentral();", StringComparison.Ordinal);

        Assert.True(ondeEscala >= 0, "a escala de fonte sumiu");
        Assert.True(ondeMarca > ondeEscala, "a marca precisa ser ajustada DEPOIS da escala de fonte");
    }

    private static string Fonte()
        => File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "PainelInicialForm.cs"));

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
