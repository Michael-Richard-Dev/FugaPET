using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// AS TELAS DE CADASTRO MONTADAS EM CODIGO USAM A MESMA MOLDURA DO SISTEMA.
/// <para>
/// Sao irmas de proposito: Vinculo de Produto x Etiqueta, Tolerancia de Recebimento e Tolerancia de
/// Producao cruzam catalogo de material ou produto com uma decisao do usuario. Quem opera uma tem de saber operar a outra sem aprender
/// nada novo — e por isso a cobranca aqui e uma so, parametrizada pelas duas.
/// </para>
/// <para>
/// Pedido do usuario em 26/11/2026 sobre a de Vinculo: "ela esta bem diferente do padrao, sem rodape
/// sem cabecalho". Era verdade nas duas — usavam a barra de titulo do Windows, nao tinham rodape e
/// desenhavam a lista e o painel de decisao sem os cartoes arredondados das demais.
/// </para>
/// <para>
/// Os testes MONTAM a tela e medem. Em especial os botoes de janela: tem de ficar DENTRO da faixa em
/// qualquer largura. Foi posicao cravada para 1366 que deixou a tela Campos da Etiqueta sem saida,
/// com os botoes a ~1100px fora do visivel — e aquela tela acabou aposentada.
/// </para>
/// </summary>
public sealed class ShellCadastroFluidoChromeTests
{
    public const string Vinculo = "FugaPET_Dev.Tela.Cadastro.VinculoProdutoEtiquetaForm";
    public const string Tolerancia = "FugaPET_Dev.Tela.Cadastro.ToleranciaRecebimentoMaterialForm";
    public const string ToleranciaProducao = "FugaPET_Dev.Tela.Cadastro.ToleranciaProcessoProducaoForm";

    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void ATela_NaoUsaABarraDeTituloDoWindows(string tipo)
        => ComATela(tipo, f => Assert.Equal(FormBorderStyle.None, f.FormBorderStyle));

    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void ATela_TemOsTresBotoesDeJanela(string tipo)
        => ComATela(tipo, f =>
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
    [InlineData(Vinculo, 1366, 768)]
    [InlineData(Vinculo, 1600, 900)]
    [InlineData(Vinculo, 1920, 1080)]
    [InlineData(Tolerancia, 1366, 768)]
    [InlineData(Tolerancia, 1600, 900)]
    [InlineData(Tolerancia, 1920, 1080)]
    [InlineData(ToleranciaProducao, 1366, 768)]
    [InlineData(ToleranciaProducao, 1600, 900)]
    [InlineData(ToleranciaProducao, 1920, 1080)]
    public void OsBotoesDeJanela_FicamDentroDaFaixa(string tipo, int largura, int altura)
        => ComATela(
            tipo,
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
    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void OsBotoesDeJanela_FicamNaFrenteDoBlocoDoTitulo(string tipo)
        => ComATela(tipo, f =>
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

    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void ATela_TemORodapeDoSistema(string tipo)
        => ComATela(tipo, f =>
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

    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void OCabecalho_TrazOLogoFugaPet(string tipo)
        => ComATela(tipo, f =>
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
    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void OConteudo_VemEmCartoesArredondados(string tipo)
        => ComATela(tipo, f =>
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
    /// NENHUM CAMPO VAZA A BORDA DO SEU CARTAO.
    /// <para>
    /// Defeito medido na Tolerancia em 26/11/2026: o TableLayoutPanel do cartao de decisao tinha
    /// ColumnCount=1 e NENHUM ColumnStyle. A coluna nasce AutoSize, se dimensiona pelo filho mais
    /// largo, e os campos sairam com 359px dentro de um cartao de 316 — caixa, texto de exemplo e
    /// botoes cortados na borda direita. Um ColumnStyle de 100% resolve, e este teste cobra.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void NenhumCampo_VazaABordaDoCartao(string tipo)
        => ComATela(tipo, f =>
        {
            List<string> vazamentos = [];
            Procurar(f, c =>
            {
                if (c is not global::FugaPET_Dev.Tela.Controls.RoundedPanel cartao)
                {
                    return;
                }

                foreach (Control filho in cartao.Controls)
                {
                    Conferir(filho, cartao, vazamentos);
                }
            });

            Assert.True(vazamentos.Count == 0, string.Join("; ", vazamentos));
        });

    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void OCabecalho_TemOBotaoDeVoltar(string tipo)
        => ComATela(tipo, f =>
        {
            Label? voltar = null;
            PictureBox? logo = null;
            Procurar(f, c =>
            {
                if (c is Label rotulo && rotulo.Text == "") { voltar = rotulo; }
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
    [Theory]
    [InlineData(Vinculo)]
    [InlineData(Tolerancia)]
    [InlineData(ToleranciaProducao)]
    public void ATela_UsaAFonteBaseDasOutrasTelasDeCadastro(string tipo)
        => ComATela(tipo, f => Assert.Equal("Cascadia Code", f.Font.Name));

    private static void Conferir(Control filho, Control cartao, List<string> vazamentos)
    {
        // Um filho mais largo que a area util do cartao vaza, por mais que o Left esteja certo.
        if (filho.Width > cartao.ClientSize.Width - cartao.Padding.Horizontal)
        {
            vazamentos.Add(
                $"{filho.GetType().Name} '{filho.Text}' tem {filho.Width}px num cartao com "
                + $"{cartao.ClientSize.Width - cartao.Padding.Horizontal}px uteis");
        }

        foreach (Control neto in filho.Controls)
        {
            if (neto is not global::FugaPET_Dev.Tela.Controls.RoundedPanel)
            {
                Conferir(neto, cartao, vazamentos);
            }
        }
    }

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

    private static void ComATela(string tipoDaTela, Action<Form> verificar, Size? tamanho = null)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly.GetType(tipoDaTela)!;
                ConstructorInfo construtor = tipo.GetConstructors().Single();
                using Form tela = (Form)construtor.Invoke(
                    [.. construtor.GetParameters().Select(_ => (object?)null)]);
                tela.StartPosition = FormStartPosition.Manual;
                tela.WindowState = FormWindowState.Normal;
                tela.Location = new Point(-5000, -5000);
                tela.ShowInTaskbar = false;
                tela.Show();

                // O TAMANHO DEPOIS DO Show: as duas telas abrem maximizadas, e atribuir Size antes faz
                // o WinForms restaurar para o MinimumSize em vez de obedecer.
                tela.Size = tamanho ?? new Size(1920, 1080);
                tela.PerformLayout();
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
