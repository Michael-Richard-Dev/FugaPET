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

    /// <summary>
    /// AS ACOES MORAM NO CARTAO DE DADOS, nao no de resumo.
    /// <para>
    /// Decisao do usuario em 26/11/2026, depois de eu mostrar que o vazio no rodape do cartao do meio
    /// era o padrao da casa: preencher com as acoes. O resumo fica so com a informacao.
    /// </para>
    /// </summary>
    [Fact]
    public void AsAcoes_MoramNoCartaoDeDados()
        => ComATela(c =>
        {
            foreach (string nome in new[] { "salvarButton", "novoButton", "excluirButton" })
            {
                Assert.Same(c["formCard"], c[nome].Parent);
            }
        });

    /// <summary>
    /// A BARRA FICA NO RODAPE E DENTRO DO CARTAO, lado a lado e sem sobreposicao. Com "Alterar Senha"
    /// escondido (cadastro novo) sao tres botoes; eles se redistribuem em vez de deixar o buraco do
    /// quarto no meio da faixa.
    /// </summary>
    [Fact]
    public void ABarraDeAcoes_FicaNoRodapeSemSobrepor()
        => ComATela(c =>
        {
            Control cartao = c["formCard"];
            Button[] acoes =
            [
                (Button)c["salvarButton"], (Button)c["novoButton"], (Button)c["excluirButton"],
            ];

            foreach (Button acao in acoes)
            {
                Assert.True(
                    acao.Bottom <= cartao.ClientSize.Height,
                    $"'{acao.Text}' termina em {acao.Bottom} e o cartao tem {cartao.ClientSize.Height}");
                Assert.True(
                    acao.Top > c["formGrid"].Bottom,
                    $"'{acao.Text}' esta por cima da grade de campos");
            }

            Button[] ordenados = [.. acoes.OrderBy(b => b.Left)];
            for (int i = 1; i < ordenados.Length; i++)
            {
                Assert.True(
                    ordenados[i].Left >= ordenados[i - 1].Right,
                    $"'{ordenados[i].Text}' comeca em {ordenados[i].Left} e o anterior termina em "
                    + $"{ordenados[i - 1].Right} — os botoes se sobrepoem");
            }
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
