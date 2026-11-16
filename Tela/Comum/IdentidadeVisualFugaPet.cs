using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// GATE 127A: identidade visual FugaPET aplicada ao cabecalho de uma tela.
/// <para>
/// POR QUE EXISTE: ate 16/11/2026 so a tela de Recebimento de Mercadoria trocava o branding local
/// Fuga Couros pelo logo FugaPET; o codigo que fazia isso era PRIVADO dela. Toda tela de cadastro
/// continuava exibindo o logo antigo, e replicar o trecho em cada uma multiplicaria o mesmo bloco
/// por dezenas de arquivos — e com ele o proximo defeito.
/// </para>
/// <para>
/// Nao mexe em geometria: troca a imagem, a cor do divisor e, quando pedido, a escala da FONTE. O
/// posicionamento continua sendo do layout de cada tela.
/// </para>
/// </summary>
public static class IdentidadeVisualFugaPet
{
    /// <summary>Caminho do asset aprovado, relativo a pasta do executavel.</summary>
    private const string CaminhoLogoBranco = "Servicos\\image\\FugaPet branco sem fundo.png";

    /// <summary>Marca o controle que ja recebeu a identidade, para diagnostico e para teste.</summary>
    public const string TagLogo = "FUGAPET_LOGO";

    /// <summary>
    /// Troca o logo do cabecalho pelo FugaPET branco e leva o divisor a branco.
    /// <para>
    /// Asset ausente NAO quebra a tela: mantem o logo embutido nos recursos. Uma tela sem logo seria
    /// pior que uma tela com o logo antigo.
    /// </para>
    /// </summary>
    public static void AplicarNoCabecalho(PictureBox? logo, Control? divisor = null, Label? textoLogo = null)
    {
        if (logo is not null)
        {
            logo.Image = CarregarLogoBranco();
            logo.Tag = TagLogo;
        }

        if (divisor is not null)
        {
            divisor.BackColor = Color.White;
        }

        if (textoLogo is not null)
        {
            textoLogo.ForeColor = Color.White;
        }
    }

    /// <summary>
    /// GATE 127A: aplica a escala de fonte das telas principais a uma area de CONTEUDO.
    /// <para>
    /// Recebe a area interna, NAO o formulario: cabecalho e rodape ficam no tamanho padrao, como ja
    /// acontece na tela de Recebimento. Passar o form inteiro inflaria a barra de titulo e o rodape,
    /// que sao comuns a todas as telas.
    /// </para>
    /// <para>
    /// A escala mexe SO na fonte. Telas que calculam geometria a partir do texto precisam recalcular
    /// o proprio layout DEPOIS desta chamada — por isso o parametro <paramref name="aposEscalar"/>.
    /// </para>
    /// </summary>
    public static void AplicarEscalaDeConteudo(Control? conteudo, Action? aposEscalar = null)
    {
        if (conteudo is null)
        {
            return;
        }

        Dictionary<Control, float> iconesAntes = [];
        ColetarIcones(conteudo, iconesAntes);

        LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(
            conteudo, LegibilidadeRecebimentoMercadoria.Escala);

        LimitarEscalaDeIcones(iconesAntes);

        LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(conteudo);

        aposEscalar?.Invoke();
    }

    /// <summary>
    /// Familias de fonte que desenham SIMBOLOS, nao texto. Um glifo dessas familias e um icone: cresce
    /// para legibilidade, mas nao ate o dobro, senao domina o cartao em vez de acompanhar o titulo.
    /// </summary>
    private static readonly string[] FamiliasDeIcone =
    [
        "Segoe MDL2 Assets",
        "Segoe Fluent Icons",
        "Segoe UI Symbol",
        "Wingdings",
        "Webdings",
        "Marlett"
    ];

    /// <summary>
    /// Teto do corpo de um icone depois da escala, em pontos.
    /// <para>
    /// POR QUE 18: na tela de Recebimento — o padrao aprovado — o glifo nasce em 8,5pt e termina em 17pt
    /// depois da escala 2x, ao lado de titulos de ~15,5pt. As telas de cadastro nasceram com glifos de
    /// 14 a 18pt; dobra-los levaria a 28–36pt, muito acima da banda do padrao, e foi isso que estourou
    /// as caixas dos cartoes de Setor. Com o teto, os icones de um cartao convergem para o mesmo corpo,
    /// o que tambem os alinha entre si.
    /// </para>
    /// </summary>
    public const float TetoFonteIcone = 18f;

    private static void ColetarIcones(Control controle, Dictionary<Control, float> icones)
    {
        if (EhFonteDeIcone(controle.Font))
        {
            icones[controle] = controle.Font.Size;
        }

        foreach (Control filho in controle.Controls)
        {
            ColetarIcones(filho, icones);
        }
    }

    private static void LimitarEscalaDeIcones(Dictionary<Control, float> icones)
    {
        foreach (KeyValuePair<Control, float> par in icones)
        {
            Control controle = par.Key;
            float corpoOriginal = par.Value;

            float alvo = Math.Min(corpoOriginal * LegibilidadeRecebimentoMercadoria.Escala, TetoFonteIcone);

            // Nunca ENCOLHER abaixo do original: um icone ja maior que o teto foi decisao daquela tela.
            if (alvo < corpoOriginal)
            {
                alvo = corpoOriginal;
            }

            controle.Font = new Font(
                controle.Font.FontFamily, alvo, controle.Font.Style, GraphicsUnit.Point);
        }
    }

    /// <summary>Diz se a fonte desenha simbolos em vez de texto.</summary>
    public static bool EhFonteDeIcone(Font? fonte)
        => fonte is not null
           && Array.Exists(
               FamiliasDeIcone,
               familia => string.Equals(familia, fonte.FontFamily.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Logo FugaPET em branco. Lido do disco porque o asset e distribuido com a publicacao; ausente,
    /// cai no recurso embutido.
    /// </summary>
    public static Image CarregarLogoBranco()
    {
        string caminho = Path.Combine(AppContext.BaseDirectory, CaminhoLogoBranco);

        if (!File.Exists(caminho))
        {
            return global::FugaPET_Dev.Properties.Resources.fuga_2026_logo;
        }

        using Image imagem = Image.FromFile(caminho);
        return new Bitmap(imagem);
    }
}
