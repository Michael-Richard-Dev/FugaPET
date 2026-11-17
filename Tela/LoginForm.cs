using FugaPET_Dev.Controle;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela;

public partial class LoginForm : Form
{
    private const string WindowIconPath = "Servicos\\icone\\fugapet.ico";

    public LoginForm()
    {
        InitializeComponent();
        Text = global::FugaPET_Dev.MarcaProduto.NomeCompleto;
        LoadVisualAssets();
        ConfigureRuntimeVisuals();
        global::FugaPET_Dev.Tela.Comum.ModoDemonstracaoHelper.AplicarFaixaSeModoDemonstracao(this);

        // Legibilidade: duplica (2.0x) as fontes de TODA a tela de Login e reacomoda o card central (cresce as
        // caixas de usuário/senha, os botões e o próprio card para caber tudo com harmonia). O card tem
        // Anchor=None, então permanece centralizado ao maximizar mesmo após crescer.
        SuspendLayout();
        global::FugaPET_Dev.Tela.Processo.LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(
            this, global::FugaPET_Dev.Tela.Processo.LegibilidadeRecebimentoMercadoria.Escala);
        AjustarCardLogin();
        ResumeLayout(true);
        PerformLayout();
    }

    private static int Arredondar(float valor) => (int)Math.Round(valor, MidpointRounding.AwayFromZero);

    /// <summary>
    /// GATE 129A — largura do texto mais largo do card, medida na fonte que cada rotulo REALMENTE tem
    /// no momento (ja ampliada e ja renderizada no DPI da maquina). E isso que impede o corte.
    /// </summary>
    private int LarguraDoTextoMaisLargo()
    {
        Label[] textos = [titleLabel, subtitleLabel, userLabel, passwordLabel, messageLabel, versionLabel];
        int maior = 0;

        foreach (Label rotulo in textos)
        {
            if (string.IsNullOrWhiteSpace(rotulo.Text))
            {
                continue;
            }

            int largura = TextRenderer.MeasureText(rotulo.Text, rotulo.Font).Width;
            if (largura > maior)
            {
                maior = largura;
            }
        }

        // A mensagem de erro so aparece depois de uma tentativa, entao ela NAO esta no Text agora.
        // Reservar a largura dela aqui evita o card "pular" de tamanho quando o erro aparece — e era
        // justamente essa frase que tambem cortava no notebook.
        int larguraMensagem = TextRenderer.MeasureText(
            "Nao foi possivel concluir a operacao. Acione o suporte.", messageLabel.Font).Width;

        return Math.Max(maior, larguraMensagem);
    }

    // Reflow do card de login para a fonte 2.0x: empilha os elementos com altura preferida, cresce as caixas de
    // input (com o TextBox/ícone dentro) e os botões, alarga tudo e redimensiona o card central.
    private void AjustarCardLogin()
    {
        // GATE 129A — a largura do card NAO pode ser um numero de pixels cravado.
        //
        // Antes era `larguraCard = 600`, o que dava 512px de caixa para os rotulos. As FONTES
        // acompanham o DPI da maquina; 512 pixels nao acompanham. Nesta estacao, a 100%, o titulo
        // "Acesso ao Sistema" na fonte 2x ocupa 440 de 512 — passa com 14% de folga. Num notebook
        // 2560x1600 a 150% o mesmo texto pede 660px, e o final da frase sumia: a tela mostrava
        // "Acesso ao". Mesma causa para o subtitulo e para a mensagem de erro.
        //
        // Agora a caixa e DERIVADA do conteudo: parte do desenho original corrigido pelo DPI e
        // cresce ate caber o texto mais largo. Isso se corrige sozinho em qualquer DPI e tambem se
        // a escala de fonte mudar.
        float fatorDpi = DeviceDpi / 96f;
        int margem = Arredondar(44 * fatorDpi);
        int larguraMinimaItem = Arredondar(512 * fatorDpi);

        int larguraItem = Math.Max(larguraMinimaItem, LarguraDoTextoMaisLargo());
        int larguraCard = larguraItem + (margem * 2);
        int y = Arredondar(28 * fatorDpi);

        // Logo no topo, centralizado (mantém o tamanho da imagem).
        logoHeaderPanel.Left = (larguraCard - logoHeaderPanel.Width) / 2;
        logoHeaderPanel.Top = y;
        y = logoHeaderPanel.Bottom + 14;

        void Faixa(Control c, int alturaMin, int gap)
        {
            c.Left = margem;
            c.Width = larguraItem;
            c.Height = Math.Max(alturaMin, c.PreferredSize.Height);
            c.Top = y;
            y = c.Bottom + gap;
        }

        Faixa(titleLabel, 44, 6);
        Faixa(subtitleLabel, 26, 16);
        Faixa(userLabel, 26, 6);
        AjustarInputLogin(userInputPanel, userIconLabel, userTextBox, margem, larguraItem, ref y, 16);
        Faixa(passwordLabel, 26, 6);
        AjustarInputLogin(passwordInputPanel, passwordIconLabel, passwordTextBox, margem, larguraItem, ref y, 14);
        Faixa(messageLabel, 24, 8);
        Faixa(enterButton, 56, 10);
        Faixa(exitButton, 52, 12);
        Faixa(versionLabel, 24, 0);

        loginPanel.Size = new Size(larguraCard, y + Arredondar(24 * fatorDpi));

        // Centraliza o card no container de forma explícita (o Anchor=None fica impreciso após crescer o card).
        Control host = loginPanel.Parent ?? this;
        void Centralizar()
        {
            loginPanel.Left = Math.Max(0, (host.ClientSize.Width - loginPanel.Width) / 2);
            loginPanel.Top = Math.Max(0, (host.ClientSize.Height - loginPanel.Height) / 2);
        }
        host.Resize += (_, _) => Centralizar();
        Centralizar();
    }

    private static void AjustarInputLogin(
        Control painel, Control icone, TextBox textbox, int margem, int larguraItem, ref int y, int gap)
    {
        const int altura = 60;
        painel.Left = margem;
        painel.Width = larguraItem;
        painel.Height = altura;
        painel.Top = y;

        icone.Left = 14;
        icone.Top = Math.Max(2, (altura - icone.Height) / 2);

        textbox.Left = icone.Right + 12;
        textbox.Height = Math.Min(textbox.PreferredSize.Height, altura - 8);
        textbox.Top = Math.Max(2, (altura - textbox.Height) / 2);
        textbox.Width = larguraItem - textbox.Left - 16;

        y = painel.Bottom + gap;
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
        Color focusBorder = Color.FromArgb(200, 78, 10);

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
