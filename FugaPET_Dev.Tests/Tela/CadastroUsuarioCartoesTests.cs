using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// A TELA DE USUARIO NO PADRAO DAS DEMAIS DE CADASTRO: lista que cabe o que mostra, cabecalho sem
/// enfeite e campos no tamanho do cartao.
/// <para>
/// Tres pedidos do usuario em 26/11/2026, sobre a tela aberta com os 37 usuarios reais: "a
/// informacao de cargo na grid quebra", "tem um card de conexao SAP no cabecalho que preciso que
/// remova" e "o card grande do meio as informacoes nao estao em harmonia com os outros cadastros".
/// </para>
/// </summary>
public sealed class CadastroUsuarioCartoesTests
{
    /// <summary>
    /// A COLUNA DE CARGO CABE O MAIOR NOME DO CATALOGO.
    /// <para>
    /// MEDIDO: ela tinha 172px e "Administrador do Sistema" precisa de 208px — saia cortado em
    /// "Administrador do". No mesmo cartao a coluna de Login tinha 236px para um login de ~110px: o
    /// corte estava na REPARTICAO, nao na falta de espaco.
    /// </para>
    /// </summary>
    [Fact]
    public void AColunaDeCargo_CabeOMaiorNomeDoCatalogo()
        => ComATela(c =>
        {
            Label cargo = (Label)c["profileRow1CargoLabel"];
            int precisa = TextRenderer.MeasureText("Administrador do Sistema", cargo.Font).Width;

            Assert.True(
                cargo.Width >= precisa,
                $"a coluna de Cargo tem {cargo.Width}px e o maior cargo do catalogo precisa de "
                + $"{precisa}px — o nome sai cortado");
        });

    /// <summary>
    /// E O NOME MAIOR AINDA TERMINA EM RETICENCIAS. Nenhuma largura serve para um texto livre de
    /// cadastro; sem isto, basta alguem cadastrar um cargo com nome longo para voltar o corte seco.
    /// </summary>
    [Fact]
    public void OCargoLongo_TerminaEmReticencias()
        => ComATela(c =>
        {
            foreach (string nome in new[]
            {
                "profileRow1CargoLabel", "profileRow2CargoLabel",
                "profileRow3CargoLabel", "profileRow4CargoLabel",
            })
            {
                Assert.True(((Label)c[nome]).AutoEllipsis, $"'{nome}' corta seco em vez de reticencias");
            }
        });

    /// <summary>
    /// O CABECALHO NAO TEM O CARTAO DE CONEXAO SAP. Pedido direto do usuario; o teste olha a arvore
    /// de controles, nao o fonte, para o cartao nao voltar por outro caminho.
    /// </summary>
    [Fact]
    public void OCabecalho_NaoTemOCartaoDeSap()
        => ComATela(c =>
        {
            foreach (KeyValuePair<string, Control> par in c)
            {
                Assert.DoesNotContain("sapStatus", par.Key, StringComparison.OrdinalIgnoreCase);
            }
        });

    /// <summary>
    /// OS CAMPOS CRESCEM COM O CARTAO.
    /// <para>
    /// As dez linhas da grade eram Absolute (18 + 38, cinco vezes) com teto de 360: num cartao de
    /// 962px os campos ficavam miudos e espremidos no topo. Agora acompanham o DPI, com teto — nem
    /// miudos, nem esticados ate o fim do cartao, que foi a primeira tentativa e abriu vaos de 100px
    /// entre um par e o proximo.
    /// </para>
    /// </summary>
    [Fact]
    public void OsCampos_CrescemComOCartao()
        => ComATela(c =>
        {
            Control cartao = c["formCard"];
            Control grade = c["formGrid"];
            Control campo = c["nomeInputPanel"];

            Assert.True(cartao.Height > 900, $"o teste supoe o cartao alto; veio {cartao.Height}");
            Assert.True(
                campo.Height > 38,
                $"o campo tem {campo.Height}px num cartao de {cartao.Height}px — continua no tamanho "
                + "cravado do Designer");
            Assert.True(
                grade.Bottom < cartao.Height,
                $"a grade termina em {grade.Bottom} e o cartao tem {cartao.Height} — transbordou");
        });

    private static void ComATela(Action<Dictionary<string, Control>> verificar)
    {
        Exception? erro = null;
        Thread t = new(() =>
        {
            try
            {
                Type tipo = typeof(FugaPET_Dev.Tela.Cadastro.SetorForm).Assembly
                    .GetType("FugaPET_Dev.Tela.Cadastro.CadastroUsuarioForm")!;
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
                tela.Refresh();

                Dictionary<string, Control> controles = [];
                foreach (FieldInfo campo in tipo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (campo.GetValue(tela) is Control ctrl)
                    {
                        controles[campo.Name] = ctrl;
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
}
