using FugaPET_Dev.Controle;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela;

public partial class TrocaSenhaObrigatoriaForm : Form
{
    private readonly long _idUsuario;

    public TrocaSenhaObrigatoriaForm(long idUsuario, string loginUsuario)
    {
        _idUsuario = idUsuario;
        InitializeComponent();
        loginValueLabel.Text = loginUsuario;
    }

    private async void salvarButton_Click(object? sender, EventArgs e)
    {
        string novaSenha = novaSenhaTextBox.Text;
        string confirmacao = confirmarSenhaTextBox.Text;

        mensagemLabel.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(novaSenha))
        {
            mensagemLabel.Text = "Informe a nova senha.";
            novaSenhaTextBox.Focus();
            return;
        }

        if (!string.Equals(novaSenha, confirmacao, StringComparison.Ordinal))
        {
            mensagemLabel.Text = "As senhas informadas nao conferem.";
            confirmarSenhaTextBox.Clear();
            confirmarSenhaTextBox.Focus();
            return;
        }

        salvarButton.Enabled = false;

        try
        {
            var usuarioController = FabricaControladoresCadastro.CriarUsuarioController();
            ResultadoOperacao resultado = await usuarioController.AlterarSenhaPropriaAsync(_idUsuario, novaSenha);
            if (!resultado.Sucesso)
            {
                mensagemLabel.Text = string.IsNullOrWhiteSpace(resultado.Mensagem)
                    ? "Nao foi possivel alterar a senha."
                    : resultado.Mensagem;
                return;
            }

            EstadoSessaoUsuarioAtual.MarcarSenhaAlterada();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception)
        {
            mensagemLabel.Text = "Nao foi possivel concluir a operacao. Acione o suporte.";
        }
        finally
        {
            salvarButton.Enabled = true;
        }
    }
}
