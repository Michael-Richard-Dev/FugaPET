using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A HARMONIA DAS TELAS DE CADASTRO EM 1920x1080 A 100% (pedidos do usuario em 25/11/2026).
/// <para>
/// "A tela nao esta harmonizada, a quebras de informacoes, linhas sobrescrevendo, botoes
/// quebrados... para a resolucao 1920 x 1080 por 100% de escala."
/// </para>
/// <para>
/// CAUSA, medida e nao suposta: o GATE 127C grava <c>MinimumSize.Height</c> com a altura que o
/// texto precisa NA LARGURA DAQUELE MOMENTO — e nas telas de cadastro isso acontece com o cartao
/// ainda no tamanho do Designer. Depois o cartao e esticado e a fonte e reajustada para baixo, mas
/// o minimo velho continua prendendo a caixa. "Resumo do Setor" ficava numa caixa de 62px calculada
/// para TRES linhas a 18pt em 200px de largura, quando o real era uma linha a 12pt em 285px; e
/// "Situacao", centralizado numa caixa de 84px calculada para QUATRO linhas, descia ~25px e parecia
/// outra linha da tabela.
/// </para>
/// <para>
/// Os testes MONTAM a tela em 1920x1080 e medem os Bounds. Foi assim que a causa apareceu — pelos
/// pixels eu estava inferindo errado, e a primeira correcao que escrevi piorou a tela.
/// </para>
/// </summary>
public sealed class CadastroLayoutHarmoniaTests
{
    /// <summary>
    /// OS DOIS CABECALHOS DA LISTA NA MESMA LINHA. Era a "linha sobrescrevendo" do relato: mesma
    /// posicao Y, mas caixas de alturas diferentes (33 contra 84) e texto centralizado.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void OsCabecalhosDaLista_FicamNaMesmaLinha(string tela)
        => ComATela(tela, c =>
        {
            Label setor = (Label)c["profilesHeaderProfileLabel"];
            Label situacao = (Label)c["profilesHeaderStatusLabel"];

            Assert.Equal(setor.Top, situacao.Top);
            Assert.Equal(setor.Height, situacao.Height);
        });

    /// <summary>
    /// NENHUM ROTULO FICA COM CAIXA INFLADA: a altura nao pode passar muito do que o texto precisa
    /// na largura que o rotulo REALMENTE tem. Era exatamente o estrago do minimo obsoleto, e e por
    /// isso que a medicao usa a largura final, nao a do Designer.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void NenhumRotulo_FicaComCaixaInflada(string tela)
        => ComATela(tela, c =>
        {
            foreach (string nome in new[]
            {
                "summaryTitleLabel", "summaryPerfilCaptionLabel", "summaryPerfilValueLabel",
                "tipTextLabel", "profilesHeaderProfileLabel", "profilesHeaderStatusLabel",
            })
            {
                Label rotulo = (Label)c[nome];
                int necessario = TextRenderer.MeasureText(
                    rotulo.Text, rotulo.Font, new Size(rotulo.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                Assert.True(
                    rotulo.Height <= necessario + 24,
                    $"'{nome}' tem {rotulo.Height}px para um texto que precisa de {necessario}px "
                    + $"em {rotulo.Width}px de largura — caixa inflada por minimo obsoleto");
            }
        });

    /// <summary>
    /// E NADA FICA CORTADO: inflar era o defeito, mas encolher demais seria trocar um por outro.
    /// Esta guarda e o contrapeso da anterior.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void NenhumRotulo_FicaCortado(string tela)
        => ComATela(tela, c =>
        {
            foreach (string nome in new[]
            {
                "summaryTitleLabel", "summaryPerfilCaptionLabel", "summaryPerfilValueLabel",
                "profilesHeaderProfileLabel", "profilesHeaderStatusLabel",
            })
            {
                Label rotulo = (Label)c[nome];
                int necessario = TextRenderer.MeasureText(
                    rotulo.Text, rotulo.Font, new Size(rotulo.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                Assert.True(
                    rotulo.Height >= necessario,
                    $"'{nome}' tem {rotulo.Height}px e o texto precisa de {necessario}px — cortaria");
            }
        });

    /// <summary>
    /// Os divisores do cartao de resumo NAO SE SOBREPOEM aos blocos de texto. Linha passando por
    /// cima de texto foi uma das quebras que o usuario viu.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void OsDivisores_NaoCruzamOsTextos(string tela)
        => ComATela(tela, c =>
        {
            Control[] divisores =
            [
                c["summaryDividerLabel"], c["summaryDividerLabel2"],
                c["summaryDividerLabel3"], c["summaryDividerLabel4"],
            ];

            Control[] textos =
            [
                c["summaryTitleLabel"], c["summaryPerfilCaptionLabel"],
                c["summaryPerfilValueLabel"], c["tipTextLabel"],
            ];

            foreach (Control divisor in divisores)
            {
                foreach (Control texto in textos)
                {
                    bool cruza = divisor.Top > texto.Top && divisor.Top < texto.Bottom;
                    Assert.False(
                        cruza,
                        $"um divisor em y={divisor.Top} cruza '{texto.Text}' ({texto.Top}..{texto.Bottom})");
                }
            }
        });

    /// <summary>
    /// A limpeza do minimo obsoleto acontece ANTES de posicionar, e a protecao contra texto cortado
    /// e REAPLICADA depois — com a largura e a fonte reais. Sem a reaplicacao, a tela ficaria sem a
    /// guarda do GATE 127C.
    /// </summary>
    [Theory]
    [InlineData("SetorForm")]
    [InlineData("CargoForm")]
    public void OMinimoELimpoAntes_EAProtecaoReaplicadaDepois(string tela)
    {
        string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Cadastro", tela + ".cs"));

        Assert.Equal(3, Ocorrencias(fonte, "LimparAlturaMinimaDosRotulos("));
        Assert.Contains("ReaplicarProtecaoDeAltura(", fonte, StringComparison.Ordinal);
        Assert.Contains("AdaptarAlturasParaFonte(cartao)", fonte, StringComparison.Ordinal);
    }

    // ------------------------------------------------- apoio

    private static int Ocorrencias(string texto, string agulha)
        => texto.Split(agulha).Length - 1;

    private static void ComATela(string nomeDaTela, Action<Dictionary<string, Control>> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly
                    .GetType("FugaPET_Dev.Tela.Cadastro." + nomeDaTela)
                    ?? throw new InvalidOperationException("tela " + nomeDaTela + " nao encontrada");
                // Os dois construtores recebem controller e auditoria OPCIONAIS; CreateInstance sem
                // argumentos nao preenche opcionais e falharia por "no parameterless constructor".
                // Passar nulos faz a tela montar as proprias dependencias, como em producao.
                using Form tela = (Form)Activator.CreateInstance(tipo, [null, null])!;
                tela.StartPosition = FormStartPosition.Manual;
                tela.Location = new Point(-5000, -5000);
                tela.Size = new Size(1920, 1080);
                tela.ShowInTaskbar = false;
                tela.Show();
                tela.Refresh();

                Dictionary<string, Control> controles = [];
                foreach (FieldInfo campo in tela.GetType()
                             .GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (campo.GetValue(tela) is Control c)
                    {
                        controles[campo.Name] = c;
                    }
                }

                verificar(controles);
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
