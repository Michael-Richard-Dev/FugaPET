using System.Drawing;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// NENHUM TEXTO DOS CARTOES DE CADASTRO PODE APARECER CORTADO (relatado em 24/11/2026, com print).
/// <para>
/// "Vínculo de Produto" aparecia como "Vínculo de" e "Tolerância de Recebimento" como "Tolerância
/// de". Os cartoes clonados copiam a geometria do cartao de ETIQUETA, cujo titulo tem caixa de
/// 185x28 — UMA linha, porque "Etiqueta" cabe numa. Titulo de duas linhas precisa de 50px, e o que
/// passava de 28 sumia sem erro nenhum. O mesmo nas descricoes longas, numa caixa de 34px.
/// </para>
/// <para>
/// O teste MONTA a tela de verdade e mede cada rotulo contra o texto que ele carrega. Conferir o
/// codigo-fonte nao provaria nada aqui: o defeito nao estava no que o codigo diz, e sim no tamanho
/// que as caixas acabaram tendo.
/// </para>
/// </summary>
public sealed class CadastroCartoesLegibilidadeTests
{
    [Fact]
    public void NenhumTituloOuDescricao_FicaCortado()
        => ComATelaMontada(cartoes =>
        {
            foreach ((Control cartao, Label titulo, Label descricao, Label[] rodape) in cartoes)
            {
                foreach (Label rotulo in new[] { titulo, descricao })
                {
                    int necessario = AlturaDoTexto(rotulo);

                    Assert.True(
                        rotulo.Height >= necessario,
                        $"no cartao '{titulo.Text}' o rotulo \"{rotulo.Text}\" precisa de {necessario}px "
                        + $"em {rotulo.Width}px de largura e tem {rotulo.Height}px — apareceria cortado");
                }

                _ = cartao;
                _ = rodape;
            }
        });

    /// <summary>
    /// Corrigir o corte nao pode criar sobreposicao: trocar texto cortado por texto em cima do selo
    /// "Disponivel" seria o mesmo defeito com outra cara.
    /// </summary>
    [Fact]
    public void OTexto_NaoInvadeALinhaDeBaixo()
        => ComATelaMontada(cartoes =>
        {
            foreach ((Control cartao, Label titulo, Label descricao, Label[] rodape) in cartoes)
            {
                int inicioDoRodape = rodape.Min(r => r.Top);

                Assert.True(
                    titulo.Bottom <= descricao.Top,
                    $"no cartao '{titulo.Text}' o titulo invade a descricao");

                Assert.True(
                    descricao.Bottom <= inicioDoRodape,
                    $"no cartao '{titulo.Text}' a descricao termina em {descricao.Bottom} e a linha de baixo "
                    + $"comeca em {inicioDoRodape}");

                Assert.True(
                    rodape.Max(r => r.Bottom) <= cartao.Height,
                    $"no cartao '{titulo.Text}' a linha de baixo passa da altura do cartao");
            }
        });

    /// <summary>
    /// TODOS OS CARTOES TEM A MESMA ALTURA e os titulos comecam na MESMA linha. O Designer tinha duas
    /// geometrias convivendo (Setor e Cargo com titulo em y=92, os outros cinco em y=102), o que
    /// deixava os titulos desalinhados de card para card na mesma grade.
    /// </summary>
    [Fact]
    public void AGrade_FicaRegular()
        => ComATelaMontada(cartoes =>
        {
            Assert.True(cartoes.Count >= 10, $"a tela deveria ter ao menos 10 cartoes e tem {cartoes.Count}");

            Assert.Single(cartoes.Select(c => c.Cartao.Height).Distinct());
            Assert.Single(cartoes.Select(c => c.Titulo.Top).Distinct());
            Assert.Single(cartoes.Select(c => c.Titulo.Width).Distinct());
        });

    /// <summary>
    /// Os tres titulos que o usuario viu cortados aparecem INTEIROS — a prova pelo caso concreto,
    /// nao so pela regra.
    /// </summary>
    [Theory]
    [InlineData("Vínculo de Produto")]
    [InlineData("Tolerância de Recebimento")]
    [InlineData("Tolerância de Produção")]
    public void OsTitulosDoPrint_CabemInteiros(string titulo)
        => ComATelaMontada(cartoes =>
        {
            Label rotulo = cartoes.Select(c => c.Titulo).FirstOrDefault(t => t.Text == titulo)
                ?? throw new InvalidOperationException($"cartao '{titulo}' nao encontrado na tela");

            Assert.True(rotulo.Height >= AlturaDoTexto(rotulo), $"'{titulo}' nao cabe na propria caixa");
        });

    /// <summary>
    /// O CAMINHO DE DUAS LINHAS CONTINUA EXERCITADO. Ele importa porque foi exatamente o que
    /// quebrou: caixa de uma linha so. Com a largura util maior (a caixa do cartao menos a margem,
    /// e nao os 185px do modelo) "Vinculo de Produto" passou a caber numa linha — mas "Tolerancia
    /// de Recebimento" nao, e e esse cartao que mantem a guarda viva.
    /// </summary>
    [Fact]
    public void AlgumTitulo_OcupaDuasLinhasESegueInteiro()
        => ComATelaMontada(cartoes =>
        {
            Label[] comQuebra = [.. cartoes
                .Select(c => c.Titulo)
                .Where(t => AlturaDoTexto(t) > UmaLinha(t))];

            Assert.NotEmpty(comQuebra);
            Assert.All(comQuebra, t => Assert.True(t.Height >= AlturaDoTexto(t), $"'{t.Text}' cortado"));
        });

    // ------------------------------------------------- apoio

    private static int AlturaDoTexto(Label rotulo)
        => TextRenderer.MeasureText(
            rotulo.Text,
            rotulo.Font,
            new Size(rotulo.Width, int.MaxValue),
            TextFormatFlags.WordBreak).Height;

    private static int UmaLinha(Label rotulo)
        => TextRenderer.MeasureText("Xg", rotulo.Font, new Size(rotulo.Width, int.MaxValue),
            TextFormatFlags.WordBreak).Height;

    private static void ComATelaMontada(
        Action<List<(Control Cartao, Label Titulo, Label Descricao, Label[] Rodape)>> verificar)
        => ExecutarEmSta(() =>
        {
            using CadastroForm tela = new();

            FlowLayoutPanel grade = tela.Controls.Find("modulesFlowLayoutPanel", searchAllChildren: true)
                .OfType<FlowLayoutPanel>().Single();

            List<(Control, Label, Label, Label[])> cartoes = [];
            foreach (Control cartao in grade.Controls)
            {
                Label[] rotulos = [.. cartao.Controls.OfType<Label>().OrderBy(r => r.Top)];
                if (cartao.Controls.OfType<FugaPET_Dev.Tela.Controls.RoundedPanel>().Any() && rotulos.Length >= 3)
                {
                    cartoes.Add((cartao, rotulos[0], rotulos[1], [.. rotulos.Skip(2)]));
                }
            }

            verificar(cartoes);
        });

    private static void ExecutarEmSta(Action acao)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try { acao(); }
            catch (Exception ex) { erro = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (erro is not null) throw erro;
    }
}
