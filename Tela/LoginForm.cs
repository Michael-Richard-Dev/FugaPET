using FugaPET_Dev.Controle;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela;

public partial class LoginForm : Form
{
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";

    public LoginForm()
    {
        InitializeComponent();
        Text = global::FugaPET_Dev.MarcaProduto.NomeCompleto;
        LoadVisualAssets();
        ConfigureRuntimeVisuals();
        global::FugaPET_Dev.Tela.Comum.ModoDemonstracaoHelper.AplicarFaixaSeModoDemonstracao(this);
    }

    private void ConfigureRuntimeVisuals()
    {
        // enterButton/exitButton sao RoundedButton (quinas arredondadas via owner-draw):
        // nao aplicar Region aqui, para nao reintroduzir o corte das quinas.
        logoSaLabel.BringToFront();

        ConfigureInputFocus(userTextBox, userInputPanel);
        ConfigureInputFocus(passwordTextBox, passwordInputPanel);
    }

    private static void ConfigureInputFocus(TextBox textBox, FugaPET_Dev.Tela.Controls.RoundedPanel panel)
    {
        Color normalBorder = Color.FromArgb(214, 219, 226);
        Color focusBorder = Color.FromArgb(229, 27, 43);

        textBox.GotFocus += (_, _) =>
        {
            panel.BorderColor = focusBorder;
            panel.FillColor = Color.White;
            textBox.BackColor = Color.White;
        };

        textBox.LostFocus += (_, _) =>
        {
            panel.BorderColor = normalBorder;
            panel.FillColor = Color.FromArgb(250, 251, 252);
            textBox.BackColor = Color.FromArgb(250, 251, 252);
        };
    }

    private void LoadVisualAssets()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, WindowIconPath);
        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }

        logoPictureBox.Image = Properties.Resources.fuga_2026_logo;
        logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
    }

    private static Bitmap CropTransparentImage(Bitmap source)
    {
        Rectangle bounds = GetVisibleBounds(source);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new Bitmap(source);
        }

        Bitmap cropped = new(bounds.Width, bounds.Height);
        using Graphics graphics = Graphics.FromImage(cropped);
        graphics.DrawImage(
            source,
            new Rectangle(0, 0, cropped.Width, cropped.Height),
            bounds,
            GraphicsUnit.Pixel);

        return cropped;
    }

    private static Rectangle GetVisibleBounds(Bitmap bitmap)
    {
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A == 0)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < left || bottom < top)
        {
            return Rectangle.Empty;
        }

        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private async void enterButton_Click(object? sender, EventArgs e)
    {
        string user = userTextBox.Text.Trim();
        string password = passwordTextBox.Text;

        enterButton.Enabled = false;
        messageLabel.Text = string.Empty;

        try
        {
            var autenticacaoServico = FabricaControladoresCadastro.CriarAutenticacaoServico();
            ResultadoAutenticacao resultado = await autenticacaoServico.AutenticarAsync(user, password);

            if (!resultado.Sucesso || resultado.Sessao is null)
            {
                messageLabel.Text = string.IsNullOrWhiteSpace(resultado.Mensagem)
                    ? "Usuario ou senha invalidos."
                    : resultado.Mensagem;
                passwordTextBox.Clear();
                passwordTextBox.Focus();
                return;
            }

            EstadoSessaoUsuarioAtual.Definir(resultado.Sessao);

            if (!ValidarTrocaSenhaObrigatoria(resultado.Sessao))
            {
                EstadoSessaoUsuarioAtual.Limpar();
                passwordTextBox.Clear();
                passwordTextBox.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            try
            {
                var auditoriaServico = FabricaControladoresCadastro.CriarAuditoriaServico();
                await auditoriaServico.RegistrarErroAsync("LOGIN_ERRO", ex.ToString(), "LoginForm");
            }
            catch
            {
                // A falha de auditoria nao deve expor detalhe tecnico nem travar a tela de login.
            }

            messageLabel.Text = "Nao foi possivel concluir a operacao. Acione o suporte.";
        }
        finally
        {
            enterButton.Enabled = true;
        }
    }

    private bool ValidarTrocaSenhaObrigatoria(SessaoUsuarioAplicacao sessao)
    {
        if (!sessao.DeveTrocarSenha)
        {
            return true;
        }

        if (!sessao.IdUsuario.HasValue)
        {
            messageLabel.Text = "Nao foi possivel identificar o usuario para alterar a senha.";
            return false;
        }

        using TrocaSenhaObrigatoriaForm trocaSenhaForm = new(sessao.IdUsuario.Value, sessao.Login);
        bool senhaAlterada = trocaSenhaForm.ShowDialog(this) == DialogResult.OK;
        if (!senhaAlterada)
        {
            messageLabel.Text = "Altere a senha temporaria para acessar o sistema.";
        }

        return senhaAlterada;
    }
    private void exitButton_Click(object? sender, EventArgs e)
    {
        EstadoSessaoUsuarioAtual.Limpar();
        DialogResult = DialogResult.Cancel;
        Close();
    }
}
