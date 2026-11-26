using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A TELA DE VINCULO DE PRODUTO E ETIQUETA USA A MOLDURA DO SISTEMA.
/// <para>
/// Pedido do usuario em 26/11/2026: "ela esta bem diferente do padrao, sem rodape sem cabecalho".
/// Era verdade — a tela usava a barra de titulo do Windows, nao tinha rodape nenhum e desenhava a
/// lista e o painel de decisao sem os cartoes arredondados das demais.
/// </para>
/// <para>
/// Os testes MONTAM a tela em 1920x1080 e medem. Em especial o terceiro: os botoes de janela tem de
/// ficar DENTRO da faixa. Foi posicao cravada para 1366 que deixou a tela Campos da Etiqueta sem
/// saida, com os botoes a ~1100px fora do visivel — e aquela tela acabou aposentada.
/// </para>
/// </summary>
public sealed class VinculoProdutoEtiquetaChromeTests
{
    [Fact]
    public void ATela_NaoUsaABarraDeTituloDoWindows()
        => ComATela(f => Assert.Equal(FormBorderStyle.None, f.FormBorderStyle));

    [Fact]
    public void ATela_TemOsTresBotoesDeJanela()
        => ComATela(f =>
        {
            Label[] botoes = BotoesDeJanela(f);

            Assert.Equal(3, botoes.Length);
            Assert.All(botoes, b => Assert.True(b.Width > 0 && b.Height > 0, "botao sem tamanho"));
        });

    /// <summary>
    /// OS BOTOES FICAM DENTRO DA FAIXA, em qualquer largura. E a cobranca que a Campos da Etiqueta
    /// nao tinha: la eles eram cravados para 1366 e sumiam da tela em 1920.
    /// </summary>
    [Theory]
    [InlineData(1366, 768)]
    [InlineData(1600, 900)]
    [InlineData(1920, 1080)]
    public void OsBotoesDeJanela_FicamDentroDaFaixa(int largura, int altura)
        => ComATela(
            f =>
            {
                foreach (Label botao in BotoesDeJanela(f))
                {
                    Control faixa = botao.Parent!;

                    Assert.True(
                        botao.Right <= faixa.ClientSize.Width && botao.Left >= 0,
                        $"o botao '{botao.Text}' ocupa {botao.Left}..{botao.Right} numa faixa de "
                        + $"{faixa.ClientSize.Width}px — fora da tela, como na Campos da Etiqueta");

                    Assert.True(
                        botao.Bottom <= faixa.ClientSize.Height,
                        $"o botao '{botao.Text}' termina em {botao.Bottom} e a faixa so vai ate "
                        + $"{faixa.ClientSize.Height}");
                }
            },
            new Size(largura, altura));

    /// <summary>
    /// OS BOTOES FICAM NA FRENTE DO BLOCO DO TITULO.
    /// <para>
    /// Defeito real visto pelo usuario em 26/11/2026: a janela abria SEM os tres botoes. O bloco do
    /// titulo tem Dock=Fill, entrou antes e ficava em z=0; sendo BackColor.Transparent, ele repinta
    /// o fundo do PAI por cima de quem esta atras — e os botoes sumiam.
    /// </para>
    /// <para>
    /// O teste olha a ORDEM dos Controls, nao a imagem: o DrawToBitmap pinta em ordem diferente da
    /// tela viva e mostrava os botoes que o app real nao mostrava.
    /// </para>
    /// </summary>
    [Fact]
    public void OsBotoesDeJanela_FicamNaFrenteDoBlocoDoTitulo()
        => ComATela(f =>
        {
            Label[] botoes = BotoesDeJanela(f);
            Assert.NotEmpty(botoes);

            Control faixa = botoes[0].Parent!;
            int piorBotao = botoes.Max(b => faixa.Controls.GetChildIndex(b));

            foreach (Control irmao in faixa.Controls)
            {
                if (irmao.Dock != DockStyle.Fill)
                {
                    continue;
                }

                Assert.True(
                    faixa.Controls.GetChildIndex(irmao) > piorBotao,
                    $"o irmao {irmao.GetType().Name} com Dock=Fill esta na FRENTE dos botoes de "
                    + "janela (indice menor) e vai repintar por cima deles");
            }
        });

    [Fact]
    public void ATela_TemORodapeDoSistema()
        => ComATela(f =>
        {
            string[] esperados = ["Usuário:", "Terminal:", "Empresa:", "Banco de Dados:"];
            List<string> textos = [];
            Coletar(f, textos);

            foreach (string esperado in esperados)
            {
                Assert.Contains(
                    textos,
                    t => t.StartsWith(esperado, StringComparison.Ordinal));
            }
        });

    [Fact]
    public void OCabecalho_TrazOLogoFugaPet()
        => ComATela(f =>
        {
            PictureBox? logo = null;
            Procurar(f, c =>
            {
                if (c is PictureBox p && (p.Tag as string) == global::FugaPET_Dev.Tela.Comum.IdentidadeVisualFugaPet.TagLogo)
                {
                    logo = p;
                }
            });

            Assert.NotNull(logo);
        });

    /// <summary>
    /// A LISTA E O PAINEL DE DECISAO VEM EM CARTAO ARREDONDADO, o mesmo controle das telas com
    /// Designer. Dois cartoes: um para cada.
    /// </summary>
    [Fact]
    public void OConteudo_VemEmCartoesArredondados()
        => ComATela(f =>
        {
            int cartoes = 0;
            Procurar(f, c =>
            {
                if (c is global::FugaPET_Dev.Tela.Controls.RoundedPanel)
                {
                    cartoes++;
                }
            });

            Assert.True(cartoes >= 2, $"esperava ao menos dois cartoes, achei {cartoes}");
        });

    /// <summary>
    /// O BOTAO DE VOLTAR PARA O CADASTRO, o mesmo glifo das outras telas (U+E700), e ele fica a
    /// ESQUERDA do logo — e o primeiro elemento da faixa.
    /// </summary>
    [Fact]
    public void OCabecalho_TemOBotaoDeVoltar()
        => ComATela(f =>
        {
            Label? voltar = null;
            PictureBox? logo = null;
            Procurar(f, c =>
            {
                if (c is Label rotulo && rotulo.Text == "\uE700") { voltar = rotulo; }
                if (c is PictureBox p && (p.Tag as string) == global::FugaPET_Dev.Tela.Comum.IdentidadeVisualFugaPet.TagLogo) { logo = p; }
            });

            Assert.NotNull(voltar);
            Assert.NotNull(logo);
            Assert.True(
                voltar!.Left < logo!.Left,
                $"o botao de voltar esta em x={voltar.Left} e o logo em x={logo.Left} — ele deveria vir antes");
        });

    /// <summary>
    /// A MESMA FONTE BASE DAS OUTRAS TELAS DE CADASTRO. Medido em 26/11/2026: la o Form e Cascadia
    /// Code e aqui era Segoe UI, e por isso o titulo nao tinha o mesmo desenho.
    /// </summary>
    [Fact]
    public void ATela_UsaAFonteBaseDasOutrasTelasDeCadastro()
        => ComATela(f => Assert.Equal("Cascadia Code", f.Font.Name));

    private static Label[] BotoesDeJanela(Form f)
    {
        List<Label> achados = [];
        Procurar(f, c =>
        {
            if (c is Label rotulo && rotulo.Text is "–" or "□" or "×")
            {
                achados.Add(rotulo);
            }
        });
        return [.. achados];
    }

    private static void Procurar(Control raiz, Action<Control> visitar)
    {
        visitar(raiz);
        foreach (Control filho in raiz.Controls)
        {
            Procurar(filho, visitar);
        }
    }

    private static void Coletar(Control raiz, List<string> textos)
    {
        if (raiz is Label { Text.Length: > 0 } rotulo)
        {
            textos.Add(rotulo.Text);
        }

        foreach (Control filho in raiz.Controls)
        {
            Coletar(filho, textos);
        }
    }

    private static void ComATela(Action<Form> verificar, Size? tamanho = null)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly
                    .GetType("FugaPET_Dev.Tela.Cadastro.VinculoProdutoEtiquetaForm")!;
                ConstructorInfo construtor = tipo.GetConstructors().Single();
                using Form tela = (Form)construtor.Invoke(
                    [.. construtor.GetParameters().Select(_ => (object?)null)]);
                tela.StartPosition = FormStartPosition.Manual;
                tela.Location = new Point(-5000, -5000);
                tela.Size = tamanho ?? new Size(1920, 1080);
                tela.ShowInTaskbar = false;
                tela.Show();
                tela.Refresh();

                verificar(tela);
                tela.Hide();
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
