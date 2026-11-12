using System.Drawing;
using System.Windows.Forms;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// GATE 081/082 — legibilidade da tela "Recebimento de Mercadoria". Escala SOMENTE a tipografia (2.0x),
/// NUNCA a geometria: a tela é montada com TableLayoutPanel + Anchor/Dock + AutoScaleMode.Font e janela
/// Maximizada, então o layout se readapta ao viewport (1920x1080 / 100%) sozinho quando a fonte cresce —
/// sem barras de rolagem e sem estourar a área de trabalho. Ícones/logos (PictureBox) e bordas não são
/// tocados (§9). Puro sobre Control: testável sem instanciar a tela real.
///
/// GATE 082-FIX1: removida a escala de GEOMETRIA do 081 (Location/Size ×2), que empurrava os controles
/// para fora do viewport dentro das células de layout e forçava AutoScroll/barras. A fonte 2.0x aprovada
/// por Michael Richard é preservada.
/// </summary>
public static class LegibilidadeRecebimentoMercadoria
{
    /// <summary>Fator exigido por Michael Richard: dobrar as fontes da tela (validado em 1920x1080/100%).</summary>
    public const float Escala = 2.0f;

    // Tema FugaPET congelado (laranja/branco). Mesmos valores já usados no reskin aprovado.
    public static readonly Color LaranjaFugaPet = Color.FromArgb(250, 105, 26);
    public static readonly Color LaranjaFugaPetEscuro = Color.FromArgb(200, 78, 10);

    /// <summary>
    /// Multiplica a fonte de toda a subárvore de <paramref name="raiz"/> pelo fator informado, SEM alterar
    /// Location/Size (a geometria é responsabilidade do layout responsivo da tela). Snapshot das fontes ANTES
    /// de mutar evita composição por herança (pai já escalado inflaria o filho que herda).
    /// </summary>
    public static void AplicarEscalaFonte(Control raiz, float escala)
    {
        if (raiz is null || escala <= 0f)
        {
            return;
        }

        Dictionary<Control, float> fontesBase = [];
        Coletar(raiz, fontesBase);
        Aplicar(raiz, escala, fontesBase);
    }

    private static void Coletar(Control controle, Dictionary<Control, float> fontesBase)
    {
        fontesBase[controle] = controle.Font.Size;
        foreach (Control filho in controle.Controls)
        {
            Coletar(filho, fontesBase);
        }
    }

    private static void Aplicar(Control controle, float escala, Dictionary<Control, float> fontesBase)
    {
        controle.SuspendLayout();

        foreach (Control filho in controle.Controls)
        {
            Aplicar(filho, escala, fontesBase);
        }

        // Fonte: a partir do snapshot (não do valor já herdado/mutado). PictureBox não tem texto relevante.
        if (controle is not PictureBox && fontesBase.TryGetValue(controle, out float baseFont) && baseFont > 0f)
        {
            controle.Font = new Font(controle.Font.FontFamily, baseFont * escala, controle.Font.Style, GraphicsUnit.Point);
        }

        if (controle is DataGridView grid && fontesBase.TryGetValue(grid, out float gridFont) && gridFont > 0f)
        {
            EscalarGridFonte(grid, escala, gridFont);
        }

        controle.ResumeLayout(false);
    }

    // DataGridView é a região elástica da tela (Anchor/Dock): escala a FONTE de header/células e a altura de
    // linha/header para acomodar o texto. NÃO escala larguras de coluna (evita overflow horizontal; as colunas
    // usam preenchimento/redistribuição). Se faltar espaço, a própria grade rola internamente — nunca a janela.
    private static void EscalarGridFonte(DataGridView grid, float escala, float baseFont)
    {
        Font fonte = new(grid.Font.FontFamily, baseFont * escala, grid.Font.Style, GraphicsUnit.Point);
        grid.ColumnHeadersDefaultCellStyle.Font = fonte;
        grid.DefaultCellStyle.Font = fonte;
        grid.RowsDefaultCellStyle.Font = fonte;

        grid.ColumnHeadersHeight = Arredondar(grid.ColumnHeadersHeight * escala);
        grid.RowTemplate.Height = Arredondar(grid.RowTemplate.Height * escala);
    }

    private static int Arredondar(float valor) => (int)Math.Round(valor, MidpointRounding.AwayFromZero);

    /// <summary>
    /// GATE 082-FIX2 — adapta a GEOMETRIA ao CONTEÚDO (não à escala): após ampliar a fonte, cresce apenas o
    /// necessário para o texto caber. Percorre em pós-ordem (conteúdo interno cresce antes do container) e:
    ///  - em controles de texto de altura fixa (Label/Button/CheckBox/TextBox/ComboBox), garante altura mínima
    ///    igual à altura preferida da fonte (SÓ CRESCE, nunca reduz);
    ///  - em cada TableLayoutPanel, cresce as linhas de altura ABSOLUTA até caber o filho mais alto, deixando
    ///    intactas as linhas elásticas (Percent/AutoSize) e as que hospedam a DataGridView (região elástica).
    /// Larguras não são multiplicadas (evita overflow horizontal); a grade absorve o espaço vertical restante.
    /// </summary>
    public static void AdaptarAlturasParaFonte(Control raiz)
    {
        if (raiz is null)
        {
            return;
        }

        raiz.SuspendLayout();
        foreach (Control filho in raiz.Controls)
        {
            AdaptarAlturasParaFonte(filho);
        }

        GarantirAlturaDeTexto(raiz);

        if (raiz is TableLayoutPanel tabela)
        {
            CrescerLinhasAbsolutas(tabela);
        }

        raiz.ResumeLayout(false);
    }

    private static void GarantirAlturaDeTexto(Control controle)
    {
        if (controle is ComboBox combo)
        {
            combo.IntegralHeight = false;
            int necessario = combo.PreferredSize.Height;
            if (combo.Height < necessario)
            {
                combo.Height = necessario;
            }
            return;
        }

        if (controle is Label or Button or CheckBox or TextBox && !controle.AutoSize)
        {
            int necessario = controle.PreferredSize.Height;
            if (controle.Height < necessario)
            {
                controle.MinimumSize = new Size(controle.MinimumSize.Width, necessario);
                controle.Height = necessario;
            }
        }
    }

    private static void CrescerLinhasAbsolutas(TableLayoutPanel tabela)
    {
        int linhas = tabela.RowStyles.Count;
        if (linhas == 0)
        {
            return;
        }

        int[] alturaNecessaria = new int[linhas];
        bool[] linhaElastica = new bool[linhas];

        foreach (Control filho in tabela.Controls)
        {
            int linha = tabela.GetRow(filho);
            if (linha < 0 || linha >= linhas)
            {
                continue;
            }

            // Linha que hospeda a grade permanece elástica (não fixamos sua altura).
            if (filho is DataGridView)
            {
                linhaElastica[linha] = true;
            }

            int altura = filho.PreferredSize.Height + filho.Margin.Top + filho.Margin.Bottom;
            if (altura > alturaNecessaria[linha])
            {
                alturaNecessaria[linha] = altura;
            }
        }

        for (int linha = 0; linha < linhas; linha++)
        {
            if (linhaElastica[linha])
            {
                continue;
            }

            RowStyle estilo = tabela.RowStyles[linha];
            if (estilo.SizeType == SizeType.Absolute && alturaNecessaria[linha] > estilo.Height)
            {
                estilo.Height = alturaNecessaria[linha];
            }
        }
    }

    /// <summary>Altura mínima operacional que a grade (região elástica) deve manter após o orçamento (§17).</summary>
    public const int AlturaMinimaGradeOperacional = 200;

    /// <summary>
    /// GATE 082-FIX3 — orçamento de altura (puro/testável): dado o total do viewport e as alturas necessárias
    /// das regiões de conteúdo (cabeçalho, cards, toolbar, rodapé...), retorna quanto SOBRA para a grade. Se o
    /// resultado for menor que <see cref="AlturaMinimaGradeOperacional"/>, o layout não cabe sem reduzir fonte.
    /// </summary>
    public static int AlturaRestanteParaGrade(int alturaClienteTotal, params int[] alturasReservadas)
    {
        int reservado = 0;
        foreach (int altura in alturasReservadas)
        {
            reservado += Math.Max(0, altura);
        }

        return alturaClienteTotal - reservado;
    }

    /// <summary>
    /// GATE 082-FIX3 — cresce o teto de uma linha ABSOLUTA (faixa de conteúdo, ex.: cards) até
    /// <paramref name="alturaNecessaria"/>, SÓ CRESCE. A linha elástica (Percent) da grade cede o espaço.
    /// Retorna a nova altura efetiva da linha de conteúdo.
    /// </summary>
    public static float CrescerTetoDeConteudo(TableLayoutPanel raiz, int indiceLinha, int alturaNecessaria)
    {
        if (raiz is null || indiceLinha < 0 || indiceLinha >= raiz.RowStyles.Count)
        {
            return 0f;
        }

        RowStyle estilo = raiz.RowStyles[indiceLinha];
        if (estilo.SizeType == SizeType.Absolute && alturaNecessaria > estilo.Height)
        {
            estilo.Height = alturaNecessaria;
        }

        return estilo.Height;
    }
}
