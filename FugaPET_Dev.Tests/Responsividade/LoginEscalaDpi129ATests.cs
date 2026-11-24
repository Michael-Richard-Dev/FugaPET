using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela;

namespace FugaPET_Dev.Tests.Responsividade;

/// <summary>
/// GATE 129A — a tela de login tem de caber o proprio texto em qualquer escala de tela.
/// <para>
/// POR QUE EXISTE: ate 17/11/2026 o card era montado com largura CRAVADA (600px, dando 512px de
/// caixa para os rotulos). As fontes acompanham o DPI da maquina; os 512 pixels nao. Nesta estacao,
/// a 100%, o titulo na fonte 2x ocupava 440 de 512 — passava com 14% de folga. Num notebook
/// 2560x1600 a 150% o mesmo texto pede 660px e a tela mostrava "Acesso ao" no lugar de "Acesso ao
/// Sistema"; o subtitulo e a mensagem de erro cortavam igual.
/// </para>
/// <para>
/// O teste reproduz o efeito do DPI maior do jeito que ele realmente chega na tela: a fonte de cada
/// rotulo cresce e a geometria e recalculada. Com largura derivada do conteudo, nada corta.
/// </para>
/// </summary>
public sealed class LoginEscalaDpi129ATests
{
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

    private static T Campo<T>(object alvo, string nome)
    {
        FieldInfo? f = alvo.GetType().GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(f);
        return (T)f!.GetValue(alvo)!;
    }

    private static void RecalcularCard(LoginForm form)
    {
        MethodInfo? m = typeof(LoginForm).GetMethod(
            "AjustarCardLogin", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(m);
        m!.Invoke(form, null);
    }

    /// <summary>Rotulos de texto do card. Fora o de versao, todos podem receber frase longa.</summary>
    private static readonly string[] RotulosDoCard =
        ["titleLabel", "subtitleLabel", "userLabel", "passwordLabel", "messageLabel"];

    [Theory]
    [InlineData(1.00f)]   // 100% — esta estacao
    [InlineData(1.25f)]   // 125%
    [InlineData(1.50f)]   // 150% — o notebook 2560x1600 onde a tela quebrou
    [InlineData(2.00f)]   // folga extra: 200%
    public void Login_EmQualquerEscala_NenhumRotuloCorta(float escalaDaTela)
        => ExecutarEmSta(() =>
        {
            using LoginForm form = new();

            // O DPI maior chega na tela como texto renderizado maior, mantendo o mesmo layout.
            foreach (string nome in RotulosDoCard)
            {
                Label rotulo = Campo<Label>(form, nome);
                rotulo.Font = new Font(rotulo.Font.FontFamily, rotulo.Font.Size * escalaDaTela, rotulo.Font.Style);
            }

            RecalcularCard(form);

            foreach (string nome in RotulosDoCard)
            {
                Label rotulo = Campo<Label>(form, nome);
                if (string.IsNullOrWhiteSpace(rotulo.Text))
                {
                    continue;
                }

                int necessario = TextRenderer.MeasureText(rotulo.Text, rotulo.Font).Width;
                Assert.True(
                    rotulo.Width >= necessario,
                    $"em {escalaDaTela * 100:0}% o rotulo {nome} tem {rotulo.Width}px e o texto \"{rotulo.Text}\" "
                    + $"precisa de {necessario}px — seria cortado na tela");
            }
        });

    // A mensagem de erro so aparece DEPOIS de uma tentativa de login. Se a largura nao a considerar,
    // o card cabe tudo ate o operador errar a senha — e entao a frase corta. Foi o que aconteceu: no
    // notebook aparecia "Nao foi possivel concluir a".
    [Theory]
    [InlineData(1.00f)]
    [InlineData(1.50f)]
    public void Login_ReservaLarguraParaAMensagemDeErro(float escalaDaTela)
        => ExecutarEmSta(() =>
        {
            using LoginForm form = new();

            Label mensagem = Campo<Label>(form, "messageLabel");
            mensagem.Font = new Font(mensagem.Font.FontFamily, mensagem.Font.Size * escalaDaTela, mensagem.Font.Style);
            RecalcularCard(form);

            const string textoReal = "Nao foi possivel concluir a operacao. Acione o suporte.";
            int necessario = TextRenderer.MeasureText(textoReal, mensagem.Font).Width;

            Assert.True(
                mensagem.Width >= necessario,
                $"em {escalaDaTela * 100:0}% a mensagem de erro precisa de {necessario}px e so ha {mensagem.Width}px");
        });

    // 24/11/2026 — A FRASE MAIS LONGA DO DIAGNOSTICO DE BANCO TEM DE CABER.
    //
    // O messageLabel tem AutoSize=false: ele EMBRULHA o texto e corta o que sobra por BAIXO. Enquanto
    // havia uma frase so, reservar largura bastava. Com o diagnostico de acesso ao banco a maior
    // frase ("IP nao cadastrado: <endereco> ...") nao cabe em uma linha sem esticar o card para mais
    // de mil pixels — ela ocupa duas, e o que faltava era ALTURA. Sem esta guarda o usuario leria
    // "IP nao cadastrado:" e nada do endereco, que e justamente o dado que ele precisa.
    [Theory]
    [InlineData(1.00f)]
    [InlineData(1.50f)]
    public void Login_ReservaAlturaParaAMaiorFraseDoDiagnostico(float escalaDaTela)
        => ExecutarEmSta(() =>
        {
            using LoginForm form = new();

            Label mensagem = Campo<Label>(form, "messageLabel");
            mensagem.Font = new Font(mensagem.Font.FontFamily, mensagem.Font.Size * escalaDaTela, mensagem.Font.Style);
            RecalcularCard(form);

            foreach (string frase in FugaPET_Dev.Servicos.Seguranca.DiagnosticoFalhaAcessoBanco.MensagensPossiveis)
            {
                int necessario = TextRenderer.MeasureText(
                    frase,
                    mensagem.Font,
                    new Size(mensagem.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                Assert.True(
                    mensagem.Height >= necessario,
                    $"em {escalaDaTela * 100:0}% a frase \"{frase}\" precisa de {necessario}px de altura "
                    + $"em {mensagem.Width}px de largura, e o rotulo tem {mensagem.Height}px — seria cortada");
            }
        });

    // O card cresce junto com o conteudo: se os rotulos cabem mas o card nao os comporta, o texto
    // fica fora da area branca — visualmente tao quebrado quanto cortar.
    [Fact]
    public void Login_CardComportaOsRotulosQueEleHospeda()
        => ExecutarEmSta(() =>
        {
            using LoginForm form = new();

            foreach (string nome in RotulosDoCard)
            {
                Label rotulo = Campo<Label>(form, nome);
                rotulo.Font = new Font(rotulo.Font.FontFamily, rotulo.Font.Size * 1.5f, rotulo.Font.Style);
            }

            RecalcularCard(form);

            Panel card = Campo<Panel>(form, "loginPanel");
            foreach (string nome in RotulosDoCard)
            {
                Label rotulo = Campo<Label>(form, nome);
                Assert.True(
                    rotulo.Right <= card.Width,
                    $"{nome} termina em {rotulo.Right}px e o card tem {card.Width}px");
            }
        });
}
