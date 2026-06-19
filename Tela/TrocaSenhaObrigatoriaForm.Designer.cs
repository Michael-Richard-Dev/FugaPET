using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela;

partial class TrocaSenhaObrigatoriaForm
{
    private System.ComponentModel.IContainer components = null;
    private RoundedPanel rootPanel;
    private Label tituloLabel;
    private Label descricaoLabel;
    private Label usuarioLabel;
    private Label loginValueLabel;
    private Label novaSenhaLabel;
    private TextBox novaSenhaTextBox;
    private Label confirmarSenhaLabel;
    private TextBox confirmarSenhaTextBox;
    private Button salvarButton;
    private Label mensagemLabel;

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
        rootPanel = new RoundedPanel();
        tituloLabel = new Label();
        descricaoLabel = new Label();
        usuarioLabel = new Label();
        loginValueLabel = new Label();
        novaSenhaLabel = new Label();
        novaSenhaTextBox = new TextBox();
        confirmarSenhaLabel = new Label();
        confirmarSenhaTextBox = new TextBox();
        salvarButton = new Button();
        mensagemLabel = new Label();
        rootPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootPanel
        // 
        rootPanel.BackColor = Color.Transparent;
        rootPanel.BorderColor = Color.FromArgb(226, 232, 240);
        rootPanel.BorderRadius = 12;
        rootPanel.Controls.Add(tituloLabel);
        rootPanel.Controls.Add(descricaoLabel);
        rootPanel.Controls.Add(usuarioLabel);
        rootPanel.Controls.Add(loginValueLabel);
        rootPanel.Controls.Add(novaSenhaLabel);
        rootPanel.Controls.Add(novaSenhaTextBox);
        rootPanel.Controls.Add(confirmarSenhaLabel);
        rootPanel.Controls.Add(confirmarSenhaTextBox);
        rootPanel.Controls.Add(salvarButton);
        rootPanel.Controls.Add(mensagemLabel);
        rootPanel.FillColor = Color.White;
        rootPanel.Location = new Point(18, 18);
        rootPanel.Name = "rootPanel";
        rootPanel.ShadowBlur = 0;
        rootPanel.ShadowOffsetY = 0;
        rootPanel.Size = new Size(424, 314);
        rootPanel.TabIndex = 0;
        // 
        // tituloLabel
        // 
        tituloLabel.AutoSize = true;
        tituloLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        tituloLabel.ForeColor = Color.FromArgb(15, 23, 42);
        tituloLabel.Location = new Point(24, 22);
        tituloLabel.Name = "tituloLabel";
        tituloLabel.Size = new Size(193, 21);
        tituloLabel.TabIndex = 0;
        tituloLabel.Text = "Troca de senha obrigatoria";
        // 
        // descricaoLabel
        // 
        descricaoLabel.ForeColor = Color.FromArgb(71, 85, 105);
        descricaoLabel.Location = new Point(24, 54);
        descricaoLabel.Name = "descricaoLabel";
        descricaoLabel.Size = new Size(376, 42);
        descricaoLabel.TabIndex = 1;
        descricaoLabel.Text = "Sua senha atual e temporaria. Cadastre uma nova senha para liberar o acesso ao sistema.";
        // 
        // usuarioLabel
        // 
        usuarioLabel.AutoSize = true;
        usuarioLabel.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        usuarioLabel.ForeColor = Color.FromArgb(15, 23, 42);
        usuarioLabel.Location = new Point(24, 106);
        usuarioLabel.Name = "usuarioLabel";
        usuarioLabel.Size = new Size(49, 13);
        usuarioLabel.TabIndex = 2;
        usuarioLabel.Text = "Usuario:";
        // 
        // loginValueLabel
        // 
        loginValueLabel.AutoEllipsis = true;
        loginValueLabel.ForeColor = Color.FromArgb(71, 85, 105);
        loginValueLabel.Location = new Point(84, 106);
        loginValueLabel.Name = "loginValueLabel";
        loginValueLabel.Size = new Size(316, 18);
        loginValueLabel.TabIndex = 3;
        loginValueLabel.Text = "-";
        // 
        // novaSenhaLabel
        // 
        novaSenhaLabel.AutoSize = true;
        novaSenhaLabel.ForeColor = Color.FromArgb(15, 23, 42);
        novaSenhaLabel.Location = new Point(24, 138);
        novaSenhaLabel.Name = "novaSenhaLabel";
        novaSenhaLabel.Size = new Size(69, 13);
        novaSenhaLabel.TabIndex = 4;
        novaSenhaLabel.Text = "Nova senha";
        // 
        // novaSenhaTextBox
        // 
        novaSenhaTextBox.Location = new Point(24, 156);
        novaSenhaTextBox.Name = "novaSenhaTextBox";
        novaSenhaTextBox.PasswordChar = '*';
        novaSenhaTextBox.Size = new Size(376, 23);
        novaSenhaTextBox.TabIndex = 5;
        // 
        // confirmarSenhaLabel
        // 
        confirmarSenhaLabel.AutoSize = true;
        confirmarSenhaLabel.ForeColor = Color.FromArgb(15, 23, 42);
        confirmarSenhaLabel.Location = new Point(24, 192);
        confirmarSenhaLabel.Name = "confirmarSenhaLabel";
        confirmarSenhaLabel.Size = new Size(100, 13);
        confirmarSenhaLabel.TabIndex = 6;
        confirmarSenhaLabel.Text = "Confirmar senha";
        // 
        // confirmarSenhaTextBox
        // 
        confirmarSenhaTextBox.Location = new Point(24, 210);
        confirmarSenhaTextBox.Name = "confirmarSenhaTextBox";
        confirmarSenhaTextBox.PasswordChar = '*';
        confirmarSenhaTextBox.Size = new Size(376, 23);
        confirmarSenhaTextBox.TabIndex = 7;
        // 
        // salvarButton
        // 
        salvarButton.BackColor = Color.FromArgb(229, 27, 43);
        salvarButton.FlatAppearance.BorderSize = 0;
        salvarButton.FlatStyle = FlatStyle.Flat;
        salvarButton.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
        salvarButton.ForeColor = Color.White;
        salvarButton.Location = new Point(240, 252);
        salvarButton.Name = "salvarButton";
        salvarButton.Size = new Size(160, 36);
        salvarButton.TabIndex = 8;
        salvarButton.Text = "Alterar Senha";
        salvarButton.UseVisualStyleBackColor = false;
        salvarButton.Click += salvarButton_Click;
        // 
        // mensagemLabel
        // 
        mensagemLabel.ForeColor = Color.FromArgb(229, 27, 43);
        mensagemLabel.Location = new Point(24, 245);
        mensagemLabel.Name = "mensagemLabel";
        mensagemLabel.Size = new Size(206, 46);
        mensagemLabel.TabIndex = 9;
        // 
        // TrocaSenhaObrigatoriaForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(248, 250, 252);
        ClientSize = new Size(460, 350);
        ControlBox = false;
        Controls.Add(rootPanel);
        Font = new Font("Segoe UI", 8.25F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "TrocaSenhaObrigatoriaForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Troca de senha obrigatoria";
        rootPanel.ResumeLayout(false);
        rootPanel.PerformLayout();
        ResumeLayout(false);
    }
}
