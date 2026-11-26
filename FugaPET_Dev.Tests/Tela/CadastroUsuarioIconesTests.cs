using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// OS ICONES DOS CAMPOS DA TELA DE USUARIO SAO GLIFOS, E CADA CAMPO TEM O SEU.
/// <para>
/// Medido em 26/11/2026, com a tela montada e fotografada: Cargo e Setor nao tinham glifo nenhum —
/// carregavam um BITMAP embutido no .resx (<c>cargoIconLabel.Image</c> e <c>setorIconLabel.Image</c>),
/// que desenhava dois desenhos cinza cheios de detalhe no meio de uma coluna de glifos monocromaticos
/// do Segoe MDL2. Imagem ignora o ForeColor, entao ela nunca acompanha a cor do campo — e a mesma
/// licao do icone do menu lateral.
/// </para>
/// <para>
/// No mesmo exame: Perfil de Acesso repetia o CADEADO da senha (U+E72E) e Situacao era o unico campo
/// do cartao sem icone algum, o que aparecia como um buraco na coluna.
/// </para>
/// </summary>
public sealed class CadastroUsuarioIconesTests
{
    private static readonly string[] CamposDoCartao =
    [
        "nomeIconLabel", "loginIconLabel", "emailIconLabel", "senhaIconLabel",
        "confirmarSenhaIconLabel", "perfilIconLabel", "cargoIconLabel", "setorIconLabel",
        "situacaoIconLabel",
    ];

    /// <summary>
    /// TODO CAMPO TEM ICONE, e o de Situacao e o que faltava.
    /// </summary>
    [Fact]
    public void TodoCampoDoCartao_TemIcone()
        => ComATela(c =>
        {
            foreach (string nome in CamposDoCartao)
            {
                Assert.True(c.ContainsKey(nome), $"o campo nao tem icone: {nome}");
            }
        });

    /// <summary>
    /// NENHUM ICONE DE CAMPO E IMAGEM: todos desenham um glifo, com a fonte de icones da casa.
    /// </summary>
    [Fact]
    public void NenhumIconeDeCampo_EBitmap()
        => ComATela(c =>
        {
            foreach (string nome in CamposDoCartao)
            {
                Label icone = (Label)c[nome];

                Assert.True(
                    icone.Image is null,
                    $"'{nome}' desenha uma IMAGEM; imagem ignora o ForeColor e nao acompanha a cor "
                    + "do campo — tem de ser glifo");
                Assert.Equal("Segoe MDL2 Assets", icone.Font.Name);
                Assert.False(
                    string.IsNullOrWhiteSpace(icone.Text),
                    $"'{nome}' esta sem glifo nenhum");
            }
        });

    /// <summary>
    /// O GLIFO EXISTE DE VERDADE NA FONTE. A prova e por medicao: na Segoe MDL2 Assets um codigo sem
    /// glifo cai no retangulo de ausente, que mede MENOS que um glifo real (17px contra 23px, medido
    /// em 26/11/2026). Sem esta cobranca, trocar o caractere por engano deixaria um quadradinho na
    /// tela e nenhum teste reclamaria.
    /// </summary>
    [Fact]
    public void TodoGlifoDeCampo_ExisteNaFonte()
        => ComATela(c =>
        {
            using Font fonte = new("Segoe MDL2 Assets", 12f);
            int ausente = TextRenderer.MeasureText("", fonte).Width;

            foreach (string nome in CamposDoCartao)
            {
                Label icone = (Label)c[nome];
                int medido = TextRenderer.MeasureText(icone.Text, fonte).Width;

                Assert.True(
                    medido > ausente,
                    $"'{nome}' mede {medido}px e o retangulo de glifo ausente mede {ausente}px — "
                    + "esse codigo nao tem desenho nesta fonte");
            }
        });

    /// <summary>
    /// PERFIL DE ACESSO NAO USA O CADEADO DA SENHA. Dois campos com o mesmo icone ensinam o operador
    /// a ignorar o icone.
    /// </summary>
    [Fact]
    public void OIconeDoPerfil_NaoRepeteODaSenha()
        => ComATela(c =>
        {
            string senha = ((Label)c["senhaIconLabel"]).Text;
            string perfil = ((Label)c["perfilIconLabel"]).Text;

            Assert.NotEqual(senha, perfil);
        });

    /// <summary>
    /// "ALTERAR FOTO" NAO FICA CORTADO.
    /// <para>
    /// Medido em 26/11/2026: o painel do botao tinha 114px cravados do Designer, sobravam 66px para o
    /// texto, ele QUEBRAVA em duas linhas e o rotulo tinha 20px de altura — saia cortado pela metade.
    /// A largura do botao passou a sair da fonte vigente; largura cravada nao sobrevive a outro DPI.
    /// </para>
    /// </summary>
    [Fact]
    public void OBotaoDeFoto_NaoCortaOTexto()
        => ComATela(c =>
        {
            foreach (string nome in new[] { "uploadTextLabel", "uploadHintLabel" })
            {
                Label rotulo = (Label)c[nome];
                int necessario = TextRenderer.MeasureText(
                    rotulo.Text, rotulo.Font, new Size(rotulo.Width, int.MaxValue),
                    TextFormatFlags.WordBreak).Height;

                Assert.True(
                    rotulo.Height >= necessario,
                    $"'{rotulo.Text}' tem {rotulo.Height}px de altura e o texto precisa de "
                    + $"{necessario}px em {rotulo.Width}px de largura — cortado");
            }
        });

    /// <summary>
    /// OS CAMPOS CONTINUAM DENTRO DO CARTAO.
    /// <para>
    /// Esta e a guarda da reprovacao de 17/11/2026, repetida por mim em 26/11/2026: aplicar a
    /// adaptacao de altura compartilhada nesta tela empurrou Cargo, Setor e Situacao para FORA do
    /// cartao de dados — sem erro nenhum, so sumiram. Enquanto a tela nao ganhar o ajuste de LARGURA
    /// sob medida, qualquer coisa que cresca altura aqui tem de ser barrada por este teste.
    /// </para>
    /// </summary>
    [Fact]
    public void OsCamposDeBaixo_FicamDentroDoCartao()
        => ComATela(c =>
        {
            Control cartao = c["formCard"];

            foreach (string nome in new[] { "cargoInputPanel", "setorInputPanel", "situacaoInputPanel" })
            {
                Control campo = c[nome];
                Point noCartao = cartao.PointToClient(campo.Parent!.PointToScreen(campo.Location));

                Assert.True(
                    noCartao.Y + campo.Height <= cartao.ClientSize.Height,
                    $"'{nome}' termina em y={noCartao.Y + campo.Height} e o cartao tem "
                    + $"{cartao.ClientSize.Height}px — ficou fora da tela");
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
                foreach (FieldInfo campo in tela.GetType()
                             .GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
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
