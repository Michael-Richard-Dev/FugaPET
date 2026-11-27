using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O BOTAO "NOVO USUARIO" DO TOPO FUNCIONA, PARECE COM O DAS IRMAS, E SENHA FICA POR ULTIMO.
/// <para>
/// Pedidos do usuario em 27/11/2026: "o botao novo usuario nao esta funcionando, o evento nao esta
/// padrao com as outras telas de cadastro, as cores estao diferentes" e "as informacoes de senha e
/// confirmacao tem que serem as ultimas, hoje elas se desencontram".
/// </para>
/// </summary>
public sealed class CadastroUsuarioNovoEOrdemTests
{
    /// <summary>
    /// O CLIQUE NO BOTAO DO TOPO PREPARA UM CADASTRO NOVO — e o clique vale no rotulo DENTRO do
    /// painel, que e onde o mouse costuma cair. Ate 27/11/2026 nao havia evento nenhum ali: o botao
    /// era um desenho.
    /// </summary>
    [Theory]
    [InlineData("novoPerfilButtonPanel")]
    [InlineData("novoPerfilTextLabel")]
    [InlineData("novoPerfilIconLabel")]
    public void OBotaoDoTopo_PreparaUmCadastroNovo(string controleClicado)
        => ComATela((tela, c) =>
        {
            FieldInfo idAtual = tela.GetType()
                .GetField("_idUsuarioAtual", BindingFlags.NonPublic | BindingFlags.Instance)!;
            TextBox nome = (TextBox)c["nomeTextBox"];

            idAtual.SetValue(tela, 7L);
            nome.Text = "fulano em edicao";

            Clicar(c[controleClicado]);

            Assert.Equal(0L, (long)idAtual.GetValue(tela)!);
            Assert.True(
                string.IsNullOrEmpty(nome.Text),
                $"depois do clique em '{controleClicado}' o nome continuou '{nome.Text}'");
        });

    /// <summary>
    /// A COR DO BOTAO E A MESMA DAS IRMAS. Medido em 27/11/2026: aqui o texto era #1E293B e o "+"
    /// #0F172A — nem com as irmas, nem entre si.
    /// </summary>
    [Fact]
    public void OBotaoDoTopo_TemACorDasTelasIrmas()
    {
        Color corNaIrma = CorDoTextoDoBotao("SetorForm");
        Color corAqui = CorDoTextoDoBotao("CadastroUsuarioForm");

        Assert.Equal(corNaIrma, corAqui);
    }

    /// <summary>
    /// SENHA E CONFIRMAR SENHA SAO OS ULTIMOS CAMPOS, e um logo abaixo do outro. Antes eles se
    /// desencontravam: "Confirmar Senha" vinha na segunda linha, a direita, e "Senha" na terceira, a
    /// esquerda — pedir a confirmacao ANTES da senha.
    /// </summary>
    [Fact]
    public void Senha_EConfirmacao_SaoOsUltimosCampos()
        => ComATela((tela, c) =>
        {
            TableLayoutPanel grade = (TableLayoutPanel)c["formGrid"];

            int linhaSenha = grade.GetRow(c["senhaInputPanel"]);
            int linhaConfirmar = grade.GetRow(c["confirmarSenhaInputPanel"]);

            foreach (string nome in new[]
            {
                "nomeInputPanel", "loginInputPanel", "emailInputPanel",
                "cargoInputPanel", "setorInputPanel", "perfilInputPanel", "situacaoInputPanel",
            })
            {
                int linha = grade.GetRow(c[nome]);

                // MESMA LINHA E PERMITIDO: Situacao fica ao LADO da Senha, como o usuario pediu ao
                // mandar a senha para a celula do Cargo. O que nao pode e campo DEPOIS dela.
                Assert.True(
                    linha <= linhaSenha,
                    $"'{nome}' esta na linha {linha} e a Senha na {linhaSenha} — nenhum campo "
                    + "pode vir depois da senha");
            }

            Assert.True(
                linhaConfirmar > linhaSenha,
                $"a confirmacao esta na linha {linhaConfirmar} e a senha na {linhaSenha} — a "
                + "confirmacao vem DEPOIS da senha");
            Assert.Equal(grade.GetColumn(c["senhaInputPanel"]), grade.GetColumn(c["confirmarSenhaInputPanel"]));
        });

    private static void Clicar(Control controle)
        => typeof(Control)
            .GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(controle, [EventArgs.Empty]);

    private static Color CorDoTextoDoBotao(string nomeDaTela)
    {
        Color cor = Color.Empty;
        ComATela(
            (_, c) => cor = c["novoPerfilTextLabel"].ForeColor,
            nomeDaTela);
        return cor;
    }

    private static void ComATela(
        Action<Form, Dictionary<string, Control>> verificar,
        string nomeDaTela = "CadastroUsuarioForm")
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly
                    .GetType("FugaPET_Dev.Tela.Cadastro." + nomeDaTela)!;
                ConstructorInfo construtor = tipo.GetConstructors().Single();
                using Form tela = (Form)construtor.Invoke(
                    [.. construtor.GetParameters().Select(_ => (object?)null)]);
                tela.StartPosition = FormStartPosition.Manual;
                tela.WindowState = FormWindowState.Normal;
                tela.Location = new Point(-5000, -5000);
                tela.ShowInTaskbar = false;
                tela.Show();
                tela.Size = new Size(1920, 1080);
                tela.PerformLayout();

                Dictionary<string, Control> controles = [];
                foreach (FieldInfo campo in tipo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (campo.GetValue(tela) is Control ctrl)
                    {
                        controles[campo.Name] = ctrl;
                    }
                }

                verificar(tela, controles);
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
