using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela;

partial class LoginForm
{
    private System.ComponentModel.IContainer components = null;
    private RadialGradientPanel rootTableLayoutPanel;
    private RoundedPanel loginPanel;
    private RoundedPanel logoHeaderPanel;
    private PictureBox logoPictureBox;
    private Label logoSaLabel;
    private Label titleLabel;
    private Label subtitleLabel;
    private Label userLabel;
    private RoundedPanel userInputPanel;
    private Label userIconLabel;
    private TextBox userTextBox;
    private Label passwordLabel;
    private RoundedPanel passwordInputPanel;
    private Label passwordIconLabel;
    private TextBox passwordTextBox;
    private RoundedButton enterButton;
    private RoundedButton exitButton;
    private Label messageLabel;
    private Label versionLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LoginForm));
        rootTableLayoutPanel = new RadialGradientPanel();
        loginPanel = new RoundedPanel();
        logoHeaderPanel = new RoundedPanel();
        logoSaLabel = new Label();
        logoPictureBox = new PictureBox();
        titleLabel = new Label();
        subtitleLabel = new Label();
        userLabel = new Label();
        userInputPanel = new RoundedPanel();
        userIconLabel = new Label();
        userTextBox = new TextBox();
        passwordLabel = new Label();
        passwordInputPanel = new RoundedPanel();
        passwordIconLabel = new Label();
        passwordTextBox = new TextBox();
        enterButton = new RoundedButton();
        exitButton = new RoundedButton();
        messageLabel = new Label();
        versionLabel = new Label();
        rootTableLayoutPanel.SuspendLayout();
        loginPanel.SuspendLayout();
        logoHeaderPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)logoPictureBox).BeginInit();
        userInputPanel.SuspendLayout();
        passwordInputPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootTableLayoutPanel
        // 
        rootTableLayoutPanel.BackColor = Color.FromArgb(7, 15, 28);
        rootTableLayoutPanel.CenterColor = Color.FromArgb(34, 50, 72);
        rootTableLayoutPanel.Controls.Add(loginPanel);
        rootTableLayoutPanel.Dock = DockStyle.Fill;
        rootTableLayoutPanel.Location = new Point(0, 0);
        rootTableLayoutPanel.Name = "rootTableLayoutPanel";
        rootTableLayoutPanel.Size = new Size(920, 560);
        rootTableLayoutPanel.TabIndex = 0;
        // 
        // loginPanel
        // 
        loginPanel.Anchor = AnchorStyles.None;
        loginPanel.BackColor = Color.Transparent;
        loginPanel.BorderColor = Color.FromArgb(226, 232, 240);
        loginPanel.BorderRadius = 12;
        loginPanel.Controls.Add(logoHeaderPanel);
        loginPanel.Controls.Add(titleLabel);
        loginPanel.Controls.Add(subtitleLabel);
        loginPanel.Controls.Add(userLabel);
        loginPanel.Controls.Add(userInputPanel);
        loginPanel.Controls.Add(passwordLabel);
        loginPanel.Controls.Add(passwordInputPanel);
        loginPanel.Controls.Add(enterButton);
        loginPanel.Controls.Add(exitButton);
        loginPanel.Controls.Add(messageLabel);
        loginPanel.Controls.Add(versionLabel);
        loginPanel.Location = new Point(240, 21);
        loginPanel.Name = "loginPanel";
        loginPanel.ShadowBlur = 0;
        loginPanel.ShadowColor = Color.FromArgb(35, 15, 23, 42);
        loginPanel.ShadowOffsetY = 0;
        loginPanel.Size = new Size(440, 518);
        loginPanel.TabIndex = 0;
        // 
        // logoHeaderPanel
        // 
        logoHeaderPanel.BackColor = Color.Transparent;
        logoHeaderPanel.BorderColor = Color.FromArgb(17, 24, 39);
        logoHeaderPanel.BorderRadius = 10;
        logoHeaderPanel.Controls.Add(logoSaLabel);
        logoHeaderPanel.Controls.Add(logoPictureBox);
        logoHeaderPanel.FillColor = Color.FromArgb(17, 24, 39);
        logoHeaderPanel.Location = new Point(26, 24);
        logoHeaderPanel.Name = "logoHeaderPanel";
        logoHeaderPanel.ShadowBlur = 0;
        logoHeaderPanel.ShadowOffsetY = 0;
        logoHeaderPanel.Size = new Size(388, 112);
        logoHeaderPanel.TabIndex = 0;
        // 
        // logoSaLabel
        // 
        logoSaLabel.BackColor = Color.Transparent;
        logoSaLabel.Font = new Font("Cascadia Code", 6.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        logoSaLabel.ForeColor = Color.White;
        logoSaLabel.Location = new Point(315, 21);
        logoSaLabel.Name = "logoSaLabel";
        logoSaLabel.Size = new Size(42, 16);
        logoSaLabel.TabIndex = 1;
        logoSaLabel.Text = "S/A";
        logoSaLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // logoPictureBox
        // 
        logoPictureBox.BackColor = Color.Transparent;
        logoPictureBox.Image = (Image)resources.GetObject("logoPictureBox.Image");
        logoPictureBox.Location = new Point(0, 0);
        logoPictureBox.Name = "logoPictureBox";
        logoPictureBox.Padding = new Padding(62, 18, 62, 18);
        logoPictureBox.Size = new Size(388, 112);
        logoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        logoPictureBox.TabIndex = 0;
        logoPictureBox.TabStop = false;
        // 
        // titleLabel
        // 
        titleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
        titleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        titleLabel.Location = new Point(38, 154);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(364, 34);
        titleLabel.TabIndex = 1;
        titleLabel.Text = "Acesso ao Sistema";
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // subtitleLabel
        // 
        subtitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        subtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        subtitleLabel.Location = new Point(38, 189);
        subtitleLabel.Name = "subtitleLabel";
        subtitleLabel.Size = new Size(364, 22);
        subtitleLabel.TabIndex = 2;
        subtitleLabel.Text = "Informe suas credenciais para continuar";
        subtitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // userLabel
        // 
        userLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        userLabel.ForeColor = Color.FromArgb(31, 41, 55);
        userLabel.Location = new Point(52, 232);
        userLabel.Name = "userLabel";
        userLabel.Size = new Size(336, 20);
        userLabel.TabIndex = 3;
        userLabel.Text = "Usuário";
        userLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // userInputPanel
        // 
        userInputPanel.BackColor = Color.Transparent;
        userInputPanel.BorderColor = Color.FromArgb(214, 219, 226);
        userInputPanel.BorderRadius = 7;
        userInputPanel.Controls.Add(userIconLabel);
        userInputPanel.Controls.Add(userTextBox);
        userInputPanel.FillColor = Color.FromArgb(250, 251, 252);
        userInputPanel.Location = new Point(52, 255);
        userInputPanel.Name = "userInputPanel";
        userInputPanel.ShadowBlur = 0;
        userInputPanel.ShadowOffsetY = 0;
        userInputPanel.Size = new Size(336, 42);
        userInputPanel.TabIndex = 1;
        // 
        // userIconLabel
        // 
        userIconLabel.BackColor = Color.Transparent;
        userIconLabel.Font = new Font("Segoe MDL2 Assets", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        userIconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        userIconLabel.Location = new Point(12, 6);
        userIconLabel.Name = "userIconLabel";
        userIconLabel.Size = new Size(28, 27);
        userIconLabel.TabIndex = 0;
        userIconLabel.Text = "";
        userIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // userTextBox
        // 
        userTextBox.BackColor = Color.FromArgb(250, 251, 252);
        userTextBox.BorderStyle = BorderStyle.None;
        userTextBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 0);
        userTextBox.Location = new Point(46, 11);
        userTextBox.Name = "userTextBox";
        userTextBox.PlaceholderText = "Digite o usuário";
        userTextBox.Size = new Size(276, 20);
        userTextBox.TabIndex = 1;
        // 
        // passwordLabel
        // 
        passwordLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        passwordLabel.ForeColor = Color.FromArgb(31, 41, 55);
        passwordLabel.Location = new Point(52, 310);
        passwordLabel.Name = "passwordLabel";
        passwordLabel.Size = new Size(336, 20);
        passwordLabel.TabIndex = 5;
        passwordLabel.Text = "Senha";
        passwordLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // passwordInputPanel
        // 
        passwordInputPanel.BackColor = Color.Transparent;
        passwordInputPanel.BorderColor = Color.FromArgb(214, 219, 226);
        passwordInputPanel.BorderRadius = 7;
        passwordInputPanel.Controls.Add(passwordIconLabel);
        passwordInputPanel.Controls.Add(passwordTextBox);
        passwordInputPanel.FillColor = Color.FromArgb(250, 251, 252);
        passwordInputPanel.Location = new Point(52, 333);
        passwordInputPanel.Name = "passwordInputPanel";
        passwordInputPanel.ShadowBlur = 0;
        passwordInputPanel.ShadowOffsetY = 0;
        passwordInputPanel.Size = new Size(336, 42);
        passwordInputPanel.TabIndex = 2;
        // 
        // passwordIconLabel
        // 
        passwordIconLabel.BackColor = Color.Transparent;
        passwordIconLabel.Font = new Font("Segoe MDL2 Assets", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        passwordIconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        passwordIconLabel.Location = new Point(12, 6);
        passwordIconLabel.Name = "passwordIconLabel";
        passwordIconLabel.Size = new Size(28, 27);
        passwordIconLabel.TabIndex = 0;
        passwordIconLabel.Text = "";
        passwordIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // passwordTextBox
        // 
        passwordTextBox.BackColor = Color.FromArgb(250, 251, 252);
        passwordTextBox.BorderStyle = BorderStyle.None;
        passwordTextBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 0);
        passwordTextBox.Location = new Point(46, 11);
        passwordTextBox.Name = "passwordTextBox";
        passwordTextBox.PasswordChar = '*';
        passwordTextBox.PlaceholderText = "Digite a senha";
        passwordTextBox.Size = new Size(276, 20);
        passwordTextBox.TabIndex = 1;
        // 
        // enterButton
        // 
        enterButton.CorPreenchimento = Color.FromArgb(229, 27, 43);
        enterButton.FlatAppearance.BorderSize = 0;
        enterButton.FlatStyle = FlatStyle.Flat;
        enterButton.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        enterButton.ForeColor = Color.White;
        enterButton.Location = new Point(52, 397);
        enterButton.Name = "enterButton";
        enterButton.Size = new Size(336, 42);
        enterButton.TabIndex = 3;
        enterButton.Text = "Entrar";
        enterButton.UseVisualStyleBackColor = false;
        enterButton.Click += enterButton_Click;
        // 
        // exitButton
        // 
        exitButton.CorPreenchimento = Color.White;
        exitButton.DialogResult = DialogResult.Cancel;
        exitButton.FlatAppearance.BorderColor = Color.FromArgb(214, 219, 226);
        exitButton.FlatStyle = FlatStyle.Flat;
        exitButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        exitButton.ForeColor = Color.FromArgb(55, 65, 81);
        exitButton.Location = new Point(52, 445);
        exitButton.Name = "exitButton";
        exitButton.Size = new Size(336, 34);
        exitButton.TabIndex = 4;
        exitButton.Text = "Sair";
        exitButton.UseVisualStyleBackColor = false;
        exitButton.Click += exitButton_Click;
        // 
        // messageLabel
        // 
        messageLabel.Font = new Font("Segoe UI", 8.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        messageLabel.ForeColor = Color.FromArgb(184, 18, 32);
        messageLabel.Location = new Point(52, 379);
        messageLabel.Name = "messageLabel";
        messageLabel.Size = new Size(336, 16);
        messageLabel.TabIndex = 9;
        messageLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // versionLabel
        // 
        versionLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        versionLabel.ForeColor = Color.FromArgb(148, 163, 184);
        versionLabel.Location = new Point(52, 486);
        versionLabel.Name = "versionLabel";
        versionLabel.Size = new Size(336, 16);
        versionLabel.TabIndex = 10;
        versionLabel.Text = "FugaPET • Integração SAP";
        versionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // LoginForm
        // 
        AcceptButton = enterButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        CancelButton = exitButton;
        ClientSize = new Size(920, 560);
        Controls.Add(rootTableLayoutPanel);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "LoginForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Login - FugaPET Dev";
        WindowState = FormWindowState.Maximized;
        rootTableLayoutPanel.ResumeLayout(false);
        loginPanel.ResumeLayout(false);
        logoHeaderPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)logoPictureBox).EndInit();
        userInputPanel.ResumeLayout(false);
        userInputPanel.PerformLayout();
        passwordInputPanel.ResumeLayout(false);
        passwordInputPanel.PerformLayout();
        ResumeLayout(false);
    }
}





