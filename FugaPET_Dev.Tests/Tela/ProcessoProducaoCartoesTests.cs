using System.Drawing;
using System.Windows.Forms;
using FugaPET_Dev.Tela;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// OS CARTOES DE PROCESSO DE PRODUCAO (pedido do usuario em 24/11/2026, com print).
/// <para>
/// "Faca os mesmos ajustes que foram feitos na tela de cadastro, tanto de cor dos cards quanto de
/// ajuste dos textos visiveis, tambem quero que remova os dois botoes que estao acima, historico de
/// consumo e diagnostico... pode excluir de vez, nao vou querer."
/// </para>
/// <para>
/// A tela MONTADA e medida, nao o fonte: o defeito nunca esteve no que o codigo diz, e sim no
/// tamanho e na cor que as caixas acabaram tendo.
/// </para>
/// </summary>
public sealed class ProcessoProducaoCartoesTests
{
    private static readonly Color LadrilhoEsperado = FugaPET_Dev.Tela.Comum.CartoesDeModulo.LadrilhoDoIcone;
    private static readonly Color LaranjaEscuro = FugaPET_Dev.Tela.Comum.CartoesDeModulo.LaranjaEscuro;

    // ------------------------------------------------- textos legiveis

    /// <summary>
    /// Nenhum titulo ou descricao cortado. No print, "Leitura e controle das operacoes da ordem de
    /// producao" parava em "da ordem de", porque a caixa tinha 46px e o texto pedia mais.
    /// </summary>
    [Fact]
    public void NenhumTituloOuDescricao_FicaCortado()
        => ComATelaMontada(cartoes =>
        {
            foreach ((Control _, Label titulo, Label descricao, Label[] _) in cartoes)
            {
                foreach (Label rotulo in new[] { titulo, descricao })
                {
                    int necessario = AlturaDoTexto(rotulo);

                    Assert.True(
                        rotulo.Height >= necessario,
                        $"no cartao '{Resumir(titulo)}' o rotulo \"{Resumir(rotulo)}\" precisa de {necessario}px "
                        + $"em {rotulo.Width}px e tem {rotulo.Height}px — apareceria cortado");
                }
            }
        });

    /// <summary>Texto corrigido nao pode passar por cima do selo "Disponivel".</summary>
    [Fact]
    public void OTexto_NaoInvadeALinhaDeBaixo()
        => ComATelaMontada(cartoes =>
        {
            foreach ((Control cartao, Label titulo, Label descricao, Label[] rodape) in cartoes)
            {
                Assert.True(titulo.Bottom <= descricao.Top, $"'{Resumir(titulo)}': titulo invade a descricao");
                Assert.True(
                    descricao.Bottom <= rodape.Min(r => r.Top),
                    $"'{Resumir(titulo)}': descricao invade a linha de baixo");
                Assert.True(
                    rodape.Max(r => r.Bottom) <= cartao.Height,
                    $"'{Resumir(titulo)}': a linha de baixo passa da altura do cartao");
            }
        });

    /// <summary>
    /// AS LINHAS DA GRADE NAO SE SOBREPOEM. Aqui os cartoes sao posicionados por coordenada, com
    /// passo de linha; se o ajuste de texto os fizer crescer e o passo continuar cravado em 266, a
    /// segunda linha sobe por cima da primeira.
    /// </summary>
    [Fact]
    public void AsLinhasDaGrade_NaoSeSobrepoem()
        => ComATelaMontada(cartoes =>
        {
            Control[] ordenados = [.. cartoes.Select(c => c.Cartao).OrderBy(c => c.Top).ThenBy(c => c.Left)];

            foreach (Control cartao in ordenados)
            {
                Control? abaixo = ordenados.FirstOrDefault(
                    outro => outro.Left == cartao.Left && outro.Top > cartao.Top);

                if (abaixo is not null)
                {
                    Assert.True(
                        abaixo.Top >= cartao.Bottom,
                        $"cartao em y={abaixo.Top} comeca antes de o de cima terminar em y={cartao.Bottom}");
                }
            }
        });

    // ------------------------------------------------- paleta

    /// <summary>Todo ladrilho na tinta laranja; nenhum resto do rosa de antes do reskin.</summary>
    [Fact]
    public void TodoLadrilho_UsaATintaLaranja()
        => ComATelaMontada(cartoes =>
        {
            Assert.True(cartoes.Count >= 9, $"a tela deveria ter ao menos 9 cartoes e tem {cartoes.Count}");

            foreach ((Control cartao, Label titulo, Label _, Label[] _) in cartoes)
            {
                FugaPET_Dev.Tela.Controls.RoundedPanel ladrilho =
                    cartao.Controls.OfType<FugaPET_Dev.Tela.Controls.RoundedPanel>().First();

                Assert.True(
                    ladrilho.FillColor.ToArgb() == LadrilhoEsperado.ToArgb(),
                    $"'{Resumir(titulo)}' tem ladrilho {ladrilho.FillColor} e deveria ter {LadrilhoEsperado}");
            }
        });

    /// <summary>
    /// A SETA DE TODOS NO MESMO LARANJA. A do cartao de Paletizacao ficou em #EF4444 vermelho
    /// enquanto as outras oito ja eram laranja — um resto do tema de antes, visivel no print.
    /// </summary>
    [Fact]
    public void TodaSeta_UsaOLaranjaEscuro()
        => ComATelaMontada(cartoes =>
        {
            Label[] setas = [.. cartoes
                .SelectMany(c => c.Rodape)
                .Where(r => r.Text.Trim() is "→" or "➔" or "->")];

            Assert.True(setas.Length >= 9, $"deveria haver ao menos 9 setas e ha {setas.Length}");
            Assert.All(setas, seta => Assert.Equal(LaranjaEscuro.ToArgb(), seta.ForeColor.ToArgb()));
        });

    /// <summary>
    /// O selo "Disponivel" continua VERDE — a unica cor que o usuario mandou preservar nas duas
    /// telas. Repintar o selo de laranja apagaria a distincao entre situacao e o resto do cartao.
    /// </summary>
    [Fact]
    public void ODisponivel_ContinuaVerde()
        => ComATelaMontada(cartoes =>
        {
            Label[] selos = [.. cartoes.SelectMany(c => c.Rodape).Where(r => r.Text.Trim() == "Disponível")];

            Assert.NotEmpty(selos);
            Assert.All(selos, selo =>
            {
                Assert.Equal(Color.FromArgb(220, 252, 231).ToArgb(), selo.BackColor.ToArgb());
                Assert.Equal(Color.FromArgb(22, 163, 74).ToArgb(), selo.ForeColor.ToArgb());
            });
        });

    // ------------------------------------------------- os dois botoes removidos

    /// <summary>
    /// OS BOTOES "Historico de Consumo" e "Diagnostico Consumo 261" NAO EXISTEM MAIS na tela — e
    /// nao estao apenas escondidos, que era o que "pode excluir de vez" pedia para evitar.
    /// </summary>
    [Fact]
    public void OsDoisBotoes_NaoExistemMais()
        => ExecutarEmSta(() =>
        {
            using ProcessoProducaoForm tela = new();

            Button[] botoes = [.. TodosOsControles(tela).OfType<Button>()];

            Assert.DoesNotContain(botoes, b => b.Text.Contains("Histórico de Consumo", StringComparison.Ordinal));
            Assert.DoesNotContain(botoes, b => b.Text.Contains("Diagnóstico Consumo", StringComparison.Ordinal));
        });

    /// <summary>
    /// Nada ficou pela metade: eventos, handlers e a fiacao no painel sairam junto. Um evento sem
    /// quem o dispare e codigo morto que o proximo leitor tenta entender.
    /// </summary>
    [Fact]
    public void NaoSobrouFiacaoOrfa()
    {
        foreach (string arquivo in new[] { "ProcessoProducaoForm.cs", "PainelInicialForm.cs" })
        {
            string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", arquivo));

            Assert.DoesNotContain("HistoricoConsumoMaterialRequested", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("DiagnosticoConsumoSap261Requested", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("historicoConsumoButton", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("diagnosticoConsumoButton", fonte, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------- apoio

    private static string Resumir(Label rotulo)
        => rotulo.Text.Replace("\r\n", " ", StringComparison.Ordinal);

    private static int AlturaDoTexto(Label rotulo)
        => TextRenderer.MeasureText(
            rotulo.Text, rotulo.Font, new Size(rotulo.Width, int.MaxValue), TextFormatFlags.WordBreak).Height;

    private static IEnumerable<Control> TodosOsControles(Control raiz)
    {
        foreach (Control filho in raiz.Controls)
        {
            yield return filho;

            foreach (Control neto in TodosOsControles(filho))
            {
                yield return neto;
            }
        }
    }

    private static void ComATelaMontada(
        Action<List<(Control Cartao, Label Titulo, Label Descricao, Label[] Rodape)>> verificar)
        => ExecutarEmSta(() =>
        {
            using ProcessoProducaoForm tela = new();

            List<(Control, Label, Label, Label[])> cartoes = [];
            foreach (Control cartao in TodosOsControles(tela))
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
