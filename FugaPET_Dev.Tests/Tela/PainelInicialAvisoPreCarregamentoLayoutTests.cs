using System.Drawing;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O AVISO DE PRE-CARREGAMENTO NAO PODE COBRIR O BLOCO DO USUARIO (relatado em 24/11/2026).
/// <para>
/// O usuario mandou o print da tela inicial com "Pedidos de entrada atualizados as 18:47" escrito
/// POR CIMA do nome "admin", que aparecia cortado ao meio.
/// </para>
/// <para>
/// CAUSA: o aviso era posicionado por <c>sidebarUserPanel.Top - 24</c>, pixel cravado que supunha
/// um rotulo de 18px e uma linha so. Depois do Shown a sidebar leva escala de fonte 2.0x: o rotulo
/// fica mais alto, a frase passa a ocupar duas linhas, e a segunda desce sobre o vizinho. Pixel
/// cravado nao acompanha a fonte.
/// </para>
/// </summary>
public sealed class PainelInicialAvisoPreCarregamentoLayoutTests
{
    // ------------------------------------------------- as duas garantias, em todo caminho

    /// <summary>
    /// A caixa do aviso NUNCA encosta no bloco do usuario e NUNCA sobe para dentro do menu —
    /// qualquer que seja a altura que o texto peca, inclusive maior que o espaco existente.
    /// <para>
    /// Cobra-se a PROPRIEDADE, nao a formula: a versao antiga tinha uma trava (o -24) que existia e
    /// nao valia. Aqui todo caminho passa pela verificacao.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1, 6)]      // uma linha
    [InlineData(40, 6)]     // duas linhas na fonte 2.0x — o caso do print
    [InlineData(200, 6)]    // frase absurda: tem de encolher, nunca invadir
    [InlineData(0, 6)]      // sem texto
    [InlineData(40, 9)]     // folga maior (DPI 150%)
    public void ACaixaDoAviso_FicaEntreOMenuEOBlocoDoUsuario(int alturaNecessaria, int folga)
    {
        const int limiteSuperior = 700;          // fim do ultimo item do menu
        const int topoDoBlocoDoUsuario = 800;    // inicio do bloco do usuario

        (int topo, int altura) = PainelInicialForm.CalcularCaixaDoAviso(
            topoDoBlocoDoUsuario, limiteSuperior, folga, alturaNecessaria);

        Assert.True(
            topo + altura <= topoDoBlocoDoUsuario - folga,
            $"o aviso termina em {topo + altura} e o bloco do usuario comeca em {topoDoBlocoDoUsuario} "
            + $"(folga {folga}) — era exatamente assim que ele cobria o nome do usuario");

        Assert.True(
            topo >= limiteSuperior,
            $"o aviso comeca em {topo} e o ultimo item do menu termina em {limiteSuperior} — subiu demais");

        Assert.True(altura >= 1, "a caixa nunca pode ter altura zero ou negativa");
    }

    /// <summary>
    /// Quando CABE, a caixa recebe a altura que o texto pediu: encolher sem necessidade poria
    /// reticencias numa frase que cabia inteira.
    /// </summary>
    [Fact]
    public void QuandoCabe_ACaixaRecebeAAlturaPedida()
    {
        (int _, int altura) = PainelInicialForm.CalcularCaixaDoAviso(
            topoDoBlocoDoUsuario: 800, limiteSuperior: 700, folga: 6, alturaNecessaria: 40);

        Assert.Equal(40, altura);
    }

    /// <summary>
    /// Sem espaco nenhum (menu colado no bloco do usuario) a conta ainda devolve caixa valida, em
    /// vez de altura negativa — que o WinForms rejeitaria e derrubaria a montagem da tela inicial.
    /// </summary>
    [Fact]
    public void SemEspacoNenhum_AindaDevolveCaixaValida()
    {
        (int _, int altura) = PainelInicialForm.CalcularCaixaDoAviso(
            topoDoBlocoDoUsuario: 700, limiteSuperior: 700, folga: 6, alturaNecessaria: 40);

        Assert.True(altura >= 1);
    }

    // ------------------------------------------------- a frase real, medida de verdade

    /// <summary>
    /// A FRASE DO PRINT, medida na fonte ampliada, ocupa MAIS de 24px — ou seja, o
    /// <c>sidebarUserPanel.Top - 24</c> antigo era insuficiente por construcao, nao por acidente.
    /// Se um dia a medida couber em 24px, este teste avisa que a premissa mudou.
    /// </summary>
    [Fact]
    public void AFraseDoPrint_NaFonteAmpliada_PassaDosVinteEQuatroPixels()
    {
        // 7.5pt do rotulo x 2.0 da escala da sidebar, na largura estreita da coluna.
        using Font fonte = new("Segoe UI", 15f);
        const string frase = "Pedidos de entrada atualizados às 18:47";

        int altura = TextRenderer.MeasureText(
            frase, fonte, new Size(290, int.MaxValue), TextFormatFlags.WordBreak).Height;

        Assert.True(altura > 24, $"a frase ocupa {altura}px; a reserva antiga era de 24px");
    }

    // ------------------------------------------------- o texto muda depois da montagem

    /// <summary>
    /// A geometria e refeita a cada troca de TEXTO. "Sincronizando pedidos de entrada..." cabe numa
    /// linha e a frase final nao: posicionar so na montagem deixava a frase maior, que chega
    /// segundos depois, sobrando por baixo.
    /// </summary>
    [Fact]
    public void AGeometria_ERefeitaQuandoOTextoMuda()
    {
        string fonte = Fonte();

        int ondeTexto = fonte.IndexOf("_preCarregamentoStatusLabel.Text = texto;", StringComparison.Ordinal);
        Assert.True(ondeTexto >= 0, "a atribuicao do texto sumiu");

        int ondeReposiciona = fonte.IndexOf(
            "PosicionarStatusPreCarregamentoPedidos();", ondeTexto, StringComparison.Ordinal);

        Assert.True(ondeReposiciona > ondeTexto, "o aviso nao e reposicionado depois de trocar o texto");
    }

    /// <summary>O pixel cravado nao pode voltar: a caixa sai de medida, nao de constante.</summary>
    [Fact]
    public void OPixelCravado_NaoVolta()
    {
        string fonte = Fonte();

        Assert.DoesNotContain("_preCarregamentoStatusLabel.Top = sidebarUserPanel.Top", fonte, StringComparison.Ordinal);
        Assert.Contains("TextFormatFlags.WordBreak", fonte, StringComparison.Ordinal);
        Assert.Contains("internal static (int Topo, int Altura) CalcularCaixaDoAviso(", fonte, StringComparison.Ordinal);
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
