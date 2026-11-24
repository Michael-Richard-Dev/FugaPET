namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// A APARENCIA E O LAYOUT DOS CARTOES DE MODULO, num lugar so.
/// <para>
/// Os cartoes de Cadastro e os de Processo de Producao sao o mesmo desenho: ladrilho com icone,
/// titulo, descricao e uma linha de baixo com situacao, atalho e seta. Antes cada tela resolvia
/// cor e geometria por conta propria, e as duas acumularam os mesmos defeitos em momentos
/// diferentes — quatro tons de ladrilho numa grade, textos cortados por caixa herdada de um texto
/// curto. Duas copias sao duas chances de divergir; o usuario pediu a mesma correcao duas vezes.
/// </para>
/// </summary>
internal static class CartoesDeModulo
{
    /// <summary>O ladrilho atras do icone: tinta clara do laranja da paleta.</summary>
    internal static readonly Color LadrilhoDoIcone = Color.FromArgb(253, 228, 211);

    /// <summary>O laranja escuro da paleta, usado no icone e na seta.</summary>
    internal static readonly Color LaranjaEscuro = Color.FromArgb(200, 78, 10);

    /// <summary>A respiracao entre o icone, o titulo, a descricao e a linha de baixo.</summary>
    internal const int Folga = 8;

    /// <summary>
    /// Os cartoes dentro de um container, achados pela ESTRUTURA e nao por nome: um cartao e um
    /// controle que tem um ladrilho (RoundedPanel) e ao menos tres rotulos. Assim um cartao novo
    /// entra nos ajustes sem ninguem lembrar de cadastra-lo, e o que nao e cartao (o titulo da
    /// secao, por exemplo) fica de fora sozinho.
    /// </summary>
    internal static IReadOnlyList<Control> Localizar(Control container)
        => [.. container.Controls
            .OfType<Control>()
            .Where(c => c.Controls.OfType<Controls.RoundedPanel>().Any()
                     && c.Controls.OfType<Label>().Count() >= 3)];

    /// <summary>
    /// Pinta o ladrilho, o icone e a seta com a paleta laranja do sistema.
    /// <para>
    /// O selo "Disponivel" NAO e tocado: foi a unica cor que o usuario mandou preservar, e e ela
    /// que distingue situacao do resto do cartao. Os rotulos com fundo proprio (o selo e o atalho)
    /// ficam como estao; so os transparentes da linha de baixo — a seta — recebem o laranja.
    /// </para>
    /// </summary>
    internal static void AplicarPaleta(IEnumerable<Control> cartoes)
    {
        foreach (Control cartao in cartoes)
        {
            foreach (Controls.RoundedPanel ladrilho in cartao.Controls.OfType<Controls.RoundedPanel>())
            {
                ladrilho.FillColor = LadrilhoDoIcone;

                foreach (Label glifo in ladrilho.Controls.OfType<Label>())
                {
                    glifo.ForeColor = LaranjaEscuro;
                }
            }

            foreach (Label rotulo in cartao.Controls.OfType<Label>())
            {
                if (rotulo.BackColor == Color.Transparent && EhSeta(rotulo))
                {
                    rotulo.ForeColor = LaranjaEscuro;
                }
            }
        }
    }

    /// <summary>
    /// Da ao titulo e a descricao de cada cartao a altura que o texto REALMENTE ocupa e reempilha os
    /// dois entre o icone e a linha de baixo.
    /// <para>
    /// POR QUE EXISTE: as caixas vinham de textos curtos — "Etiqueta" numa caixa de uma linha — e o
    /// que passava disso era cortado, sem erro nenhum. "Vinculo de Produto" virava "Vinculo de";
    /// "Leitura e controle das operacoes da ordem de producao" parava em "da ordem de".
    /// </para>
    /// <para>
    /// Se o texto nao couber no espaco existente, os cartoes CRESCEM, todos pela mesma medida, e a
    /// linha de baixo desce junto. Cortar em silencio foi o defeito; trocar corte por sobreposicao
    /// nao seria corrigir.
    /// </para>
    /// </summary>
    /// <returns>Quanto os cartoes cresceram, para quem posiciona a grade acompanhar.</returns>
    internal static int AjustarTextos(IEnumerable<Control> cartoes)
    {
        List<(Control Cartao, Label Titulo, Label Descricao, int Topo, int Base)> partes = [];

        foreach (Control cartao in cartoes)
        {
            Controls.RoundedPanel? ladrilho = cartao.Controls.OfType<Controls.RoundedPanel>().FirstOrDefault();
            Label[] rotulos = [.. cartao.Controls.OfType<Label>().OrderBy(r => r.Top)];

            // Cartao fora do desenho esperado fica como esta: melhor um cartao sem ajuste do que uma
            // tela remontada as cegas.
            if (ladrilho is null || rotulos.Length < 3)
            {
                continue;
            }

            partes.Add((cartao, rotulos[0], rotulos[1], ladrilho.Bottom, rotulos.Skip(2).Min(r => r.Top)));
        }

        int crescimento = 0;
        foreach ((Control cartao, Label titulo, Label descricao, int topo, int baseDoBloco) in partes)
        {
            int largura = LarguraUtil(cartao, titulo);
            int necessario = topo
                + Folga + AlturaMedida(titulo, largura)
                + Folga + AlturaMedida(descricao, largura)
                + Folga;

            crescimento = Math.Max(crescimento, necessario - baseDoBloco);
        }

        crescimento = Math.Max(0, crescimento);

        foreach ((Control cartao, Label titulo, Label descricao, int topo, int baseDoBloco) in partes)
        {
            int margem = titulo.Left;
            int largura = LarguraUtil(cartao, titulo);

            titulo.SetBounds(margem, topo + Folga, largura, AlturaMedida(titulo, largura));
            descricao.SetBounds(margem, titulo.Bottom + Folga, largura, AlturaMedida(descricao, largura));

            if (crescimento == 0)
            {
                continue;
            }

            foreach (Label rodape in cartao.Controls.OfType<Label>().Where(r => r.Top >= baseDoBloco))
            {
                rodape.Top += crescimento;
            }

            cartao.Height += crescimento;
        }

        return crescimento;
    }

    /// <summary>A largura util do texto: a caixa do cartao menos a margem que o proprio rotulo usa.</summary>
    internal static int LarguraUtil(Control cartao, Label titulo)
        => Math.Max(40, cartao.Width - (titulo.Left * 2));

    /// <summary>
    /// A altura que o texto ocupa EMBRULHADO nessa largura, na fonte que o rotulo tem agora.
    /// <para>
    /// SEM <c>NoPadding</c>, de proposito: o Label PINTA com padding, entao com essa flag a medida
    /// vem uma linha curta e a ultima palavra some. Foi exatamente o que aconteceu com as descricoes
    /// de Tolerancia — 34px medidos contra 51px reais.
    /// </para>
    /// </summary>
    internal static int AlturaMedida(Label rotulo, int largura)
        => Math.Max(
            1,
            TextRenderer.MeasureText(
                rotulo.Text, rotulo.Font, new Size(largura, int.MaxValue), TextFormatFlags.WordBreak).Height);

    /// <summary>A seta da linha de baixo, reconhecida pelo proprio simbolo que ela desenha.</summary>
    private static bool EhSeta(Label rotulo)
        => rotulo.Text.Trim() is "→" or "➔" or "->";
}
