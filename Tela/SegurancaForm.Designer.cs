using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela;

partial class SegurancaForm
{
    private System.ComponentModel.IContainer components = null;
    private Panel contentPanel;
    private Label sectionTitleLabel;
    private Label sectionSubtitleLabel;
    private FlowLayoutPanel modulesFlowLayoutPanel;
    private RoundedPanel usuarioCard;
    private RoundedPanel usuarioIconPanel;
    private Label usuarioIconLabel;
    private Label usuarioTitleLabel;
    private Label usuarioDescriptionLabel;
    private Label usuarioStatusLabel;
    private Label usuarioShortcutLabel;
    private Label usuarioArrowLabel;
    private RoundedPanel perfilAcessoCard;
    private RoundedPanel perfilIconPanel;
    private Label perfilIconLabel;
    private Label perfilTitleLabel;
    private Label perfilDescriptionLabel;
    private Label perfilStatusLabel;
    private Label perfilShortcutLabel;
    private Label perfilArrowLabel;
    private RoundedPanel permissaoCard;
    private RoundedPanel permissaoIconPanel;
    private Label permissaoIconLabel;
    private Label permissaoTitleLabel;
    private Label permissaoDescriptionLabel;
    private Label permissaoStatusLabel;
    private Label permissaoShortcutLabel;
    private Label permissaoArrowLabel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        contentPanel = new Panel();
        sectionTitleLabel = new Label();
        sectionSubtitleLabel = new Label();
        modulesFlowLayoutPanel = new FlowLayoutPanel();
        usuarioCard = new RoundedPanel();
        usuarioIconPanel = new RoundedPanel();
        usuarioIconLabel = new Label();
        usuarioTitleLabel = new Label();
        usuarioDescriptionLabel = new Label();
        usuarioStatusLabel = new Label();
        usuarioShortcutLabel = new Label();
        usuarioArrowLabel = new Label();
        perfilAcessoCard = new RoundedPanel();
        perfilIconPanel = new RoundedPanel();
        perfilIconLabel = new Label();
        perfilTitleLabel = new Label();
        perfilDescriptionLabel = new Label();
        perfilStatusLabel = new Label();
        perfilShortcutLabel = new Label();
        perfilArrowLabel = new Label();
        permissaoCard = new RoundedPanel();
        permissaoIconPanel = new RoundedPanel();
        permissaoIconLabel = new Label();
        permissaoTitleLabel = new Label();
        permissaoDescriptionLabel = new Label();
        permissaoStatusLabel = new Label();
        permissaoShortcutLabel = new Label();
        permissaoArrowLabel = new Label();
        contentPanel.SuspendLayout();
        modulesFlowLayoutPanel.SuspendLayout();
        usuarioCard.SuspendLayout();
        usuarioIconPanel.SuspendLayout();
        perfilAcessoCard.SuspendLayout();
        perfilIconPanel.SuspendLayout();
        permissaoCard.SuspendLayout();
        permissaoIconPanel.SuspendLayout();
        SuspendLayout();
        // 
        // contentPanel
        // 
        contentPanel.BackColor = Color.FromArgb(247, 248, 250);
        contentPanel.Controls.Add(modulesFlowLayoutPanel);
        contentPanel.Controls.Add(sectionSubtitleLabel);
        contentPanel.Controls.Add(sectionTitleLabel);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 0);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(28, 24, 28, 24);
        contentPanel.Size = new Size(1592, 814);
        contentPanel.TabIndex = 0;
        // 
        // sectionTitleLabel
        // 
        sectionTitleLabel.AutoSize = true;
        sectionTitleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sectionTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        sectionTitleLabel.Location = new Point(28, 24);
        sectionTitleLabel.Name = "sectionTitleLabel";
        sectionTitleLabel.Size = new Size(290, 32);
        sectionTitleLabel.TabIndex = 0;
        sectionTitleLabel.Text = "Módulos de Segurança";
        // 
        // sectionSubtitleLabel
        // 
        sectionSubtitleLabel.AutoSize = true;
        sectionSubtitleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
        sectionSubtitleLabel.ForeColor = Color.FromArgb(100, 116, 139);
        sectionSubtitleLabel.Location = new Point(31, 59);
        sectionSubtitleLabel.Name = "sectionSubtitleLabel";
        sectionSubtitleLabel.Size = new Size(394, 19);
        sectionSubtitleLabel.TabIndex = 1;
        sectionSubtitleLabel.Text = "Acesse usuários, perfis e permissões do sistema industrial.";
        // 
        // modulesFlowLayoutPanel
        // 
        modulesFlowLayoutPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        modulesFlowLayoutPanel.AutoScroll = true;
        modulesFlowLayoutPanel.Controls.Add(usuarioCard);
        modulesFlowLayoutPanel.Controls.Add(perfilAcessoCard);
        modulesFlowLayoutPanel.Controls.Add(permissaoCard);
        modulesFlowLayoutPanel.Location = new Point(28, 108);
        modulesFlowLayoutPanel.Name = "modulesFlowLayoutPanel";
        modulesFlowLayoutPanel.Padding = new Padding(0, 0, 8, 8);
        modulesFlowLayoutPanel.Size = new Size(1536, 682);
        modulesFlowLayoutPanel.TabIndex = 2;
        // 
        // usuarioCard
        // 
        usuarioCard.BackColor = Color.Transparent;
        usuarioCard.BorderColor = Color.FromArgb(226, 232, 240);
        usuarioCard.Controls.Add(usuarioIconPanel);
        usuarioCard.Controls.Add(usuarioTitleLabel);
        usuarioCard.Controls.Add(usuarioDescriptionLabel);
        usuarioCard.Controls.Add(usuarioStatusLabel);
        usuarioCard.Controls.Add(usuarioShortcutLabel);
        usuarioCard.Controls.Add(usuarioArrowLabel);
        usuarioCard.Cursor = Cursors.Hand;
        usuarioCard.Location = new Point(0, 0);
        usuarioCard.Margin = new Padding(0, 0, 20, 20);
        usuarioCard.Name = "usuarioCard";
        usuarioCard.ShadowBlur = 0;
        usuarioCard.ShadowOffsetY = 0;
        usuarioCard.Size = new Size(260, 250);
        usuarioCard.TabIndex = 0;
        // 
        // usuarioIconPanel
        // 
        usuarioIconPanel.BackColor = Color.Transparent;
        usuarioIconPanel.BorderRadius = 10;
        usuarioIconPanel.Controls.Add(usuarioIconLabel);
        usuarioIconPanel.Cursor = Cursors.Hand;
        usuarioIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        usuarioIconPanel.Location = new Point(102, 20);
        usuarioIconPanel.Name = "usuarioIconPanel";
        usuarioIconPanel.ShadowBlur = 0;
        usuarioIconPanel.ShadowOffsetY = 0;
        usuarioIconPanel.Size = new Size(56, 56);
        usuarioIconPanel.TabIndex = 0;
        // 
        // usuarioIconLabel
        // 
        usuarioIconLabel.BackColor = Color.Transparent;
        usuarioIconLabel.Cursor = Cursors.Hand;
        usuarioIconLabel.Dock = DockStyle.Fill;
        usuarioIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        usuarioIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        usuarioIconLabel.Location = new Point(0, 0);
        usuarioIconLabel.Name = "usuarioIconLabel";
        usuarioIconLabel.Size = new Size(56, 56);
        usuarioIconLabel.TabIndex = 0;
        usuarioIconLabel.Text = "";
        usuarioIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // usuarioTitleLabel
        // 
        usuarioTitleLabel.BackColor = Color.Transparent;
        usuarioTitleLabel.Cursor = Cursors.Hand;
        usuarioTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        usuarioTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        usuarioTitleLabel.Location = new Point(20, 92);
        usuarioTitleLabel.Name = "usuarioTitleLabel";
        usuarioTitleLabel.Size = new Size(220, 62);
        usuarioTitleLabel.TabIndex = 1;
        usuarioTitleLabel.Text = "Usuários";
        usuarioTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // usuarioDescriptionLabel
        // 
        usuarioDescriptionLabel.BackColor = Color.Transparent;
        usuarioDescriptionLabel.Cursor = Cursors.Hand;
        usuarioDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        usuarioDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        usuarioDescriptionLabel.Location = new Point(22, 156);
        usuarioDescriptionLabel.Name = "usuarioDescriptionLabel";
        usuarioDescriptionLabel.Size = new Size(216, 46);
        usuarioDescriptionLabel.TabIndex = 2;
        usuarioDescriptionLabel.Text = "Cadastro e manutenção\r\nde usuários do sistema.";
        usuarioDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // usuarioStatusLabel
        // 
        usuarioStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        usuarioStatusLabel.Cursor = Cursors.Hand;
        usuarioStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        usuarioStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        usuarioStatusLabel.Location = new Point(20, 214);
        usuarioStatusLabel.Name = "usuarioStatusLabel";
        usuarioStatusLabel.Size = new Size(82, 28);
        usuarioStatusLabel.TabIndex = 3;
        usuarioStatusLabel.Text = "Disponível";
        usuarioStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // usuarioShortcutLabel
        // 
        usuarioShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        usuarioShortcutLabel.Cursor = Cursors.Hand;
        usuarioShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        usuarioShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        usuarioShortcutLabel.Location = new Point(110, 214);
        usuarioShortcutLabel.Name = "usuarioShortcutLabel";
        usuarioShortcutLabel.Size = new Size(38, 28);
        usuarioShortcutLabel.TabIndex = 4;
        usuarioShortcutLabel.Text = "F5";
        usuarioShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // usuarioArrowLabel
        // 
        usuarioArrowLabel.BackColor = Color.Transparent;
        usuarioArrowLabel.Cursor = Cursors.Hand;
        usuarioArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        usuarioArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        usuarioArrowLabel.Location = new Point(210, 207);
        usuarioArrowLabel.Name = "usuarioArrowLabel";
        usuarioArrowLabel.Size = new Size(32, 36);
        usuarioArrowLabel.TabIndex = 5;
        usuarioArrowLabel.Text = "→";
        usuarioArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // perfilAcessoCard
        // 
        perfilAcessoCard.BackColor = Color.Transparent;
        perfilAcessoCard.BorderColor = Color.FromArgb(226, 232, 240);
        perfilAcessoCard.Controls.Add(perfilIconPanel);
        perfilAcessoCard.Controls.Add(perfilTitleLabel);
        perfilAcessoCard.Controls.Add(perfilDescriptionLabel);
        perfilAcessoCard.Controls.Add(perfilStatusLabel);
        perfilAcessoCard.Controls.Add(perfilShortcutLabel);
        perfilAcessoCard.Controls.Add(perfilArrowLabel);
        perfilAcessoCard.Cursor = Cursors.Hand;
        perfilAcessoCard.Location = new Point(280, 0);
        perfilAcessoCard.Margin = new Padding(0, 0, 20, 20);
        perfilAcessoCard.Name = "perfilAcessoCard";
        perfilAcessoCard.ShadowBlur = 0;
        perfilAcessoCard.ShadowOffsetY = 0;
        perfilAcessoCard.Size = new Size(260, 250);
        perfilAcessoCard.TabIndex = 1;
        // 
        // perfilIconPanel
        // 
        perfilIconPanel.BackColor = Color.Transparent;
        perfilIconPanel.BorderRadius = 10;
        perfilIconPanel.Controls.Add(perfilIconLabel);
        perfilIconPanel.Cursor = Cursors.Hand;
        perfilIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        perfilIconPanel.Location = new Point(102, 20);
        perfilIconPanel.Name = "perfilIconPanel";
        perfilIconPanel.ShadowBlur = 0;
        perfilIconPanel.ShadowOffsetY = 0;
        perfilIconPanel.Size = new Size(56, 56);
        perfilIconPanel.TabIndex = 0;
        // 
        // perfilIconLabel
        // 
        perfilIconLabel.BackColor = Color.Transparent;
        perfilIconLabel.Cursor = Cursors.Hand;
        perfilIconLabel.Dock = DockStyle.Fill;
        perfilIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        perfilIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        perfilIconLabel.Location = new Point(0, 0);
        perfilIconLabel.Name = "perfilIconLabel";
        perfilIconLabel.Size = new Size(56, 56);
        perfilIconLabel.TabIndex = 0;
        perfilIconLabel.Text = "";
        perfilIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // perfilTitleLabel
        // 
        perfilTitleLabel.BackColor = Color.Transparent;
        perfilTitleLabel.Cursor = Cursors.Hand;
        perfilTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        perfilTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        perfilTitleLabel.Location = new Point(20, 92);
        perfilTitleLabel.Name = "perfilTitleLabel";
        perfilTitleLabel.Size = new Size(220, 62);
        perfilTitleLabel.TabIndex = 1;
        perfilTitleLabel.Text = "Perfis de\r\nAcesso";
        perfilTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // perfilDescriptionLabel
        // 
        perfilDescriptionLabel.BackColor = Color.Transparent;
        perfilDescriptionLabel.Cursor = Cursors.Hand;
        perfilDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        perfilDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        perfilDescriptionLabel.Location = new Point(22, 156);
        perfilDescriptionLabel.Name = "perfilDescriptionLabel";
        perfilDescriptionLabel.Size = new Size(216, 46);
        perfilDescriptionLabel.TabIndex = 2;
        perfilDescriptionLabel.Text = "Níveis, grupos e perfis\r\nde autorização.";
        perfilDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // perfilStatusLabel
        // 
        perfilStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        perfilStatusLabel.Cursor = Cursors.Hand;
        perfilStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        perfilStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        perfilStatusLabel.Location = new Point(20, 214);
        perfilStatusLabel.Name = "perfilStatusLabel";
        perfilStatusLabel.Size = new Size(82, 28);
        perfilStatusLabel.TabIndex = 3;
        perfilStatusLabel.Text = "Disponível";
        perfilStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // perfilShortcutLabel
        // 
        perfilShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        perfilShortcutLabel.Cursor = Cursors.Hand;
        perfilShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        perfilShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        perfilShortcutLabel.Location = new Point(110, 214);
        perfilShortcutLabel.Name = "perfilShortcutLabel";
        perfilShortcutLabel.Size = new Size(38, 28);
        perfilShortcutLabel.TabIndex = 4;
        perfilShortcutLabel.Text = "F3";
        perfilShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // perfilArrowLabel
        // 
        perfilArrowLabel.BackColor = Color.Transparent;
        perfilArrowLabel.Cursor = Cursors.Hand;
        perfilArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        perfilArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        perfilArrowLabel.Location = new Point(210, 207);
        perfilArrowLabel.Name = "perfilArrowLabel";
        perfilArrowLabel.Size = new Size(32, 36);
        perfilArrowLabel.TabIndex = 5;
        perfilArrowLabel.Text = "→";
        perfilArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoCard
        // 
        permissaoCard.BackColor = Color.Transparent;
        permissaoCard.BorderColor = Color.FromArgb(226, 232, 240);
        permissaoCard.Controls.Add(permissaoIconPanel);
        permissaoCard.Controls.Add(permissaoTitleLabel);
        permissaoCard.Controls.Add(permissaoDescriptionLabel);
        permissaoCard.Controls.Add(permissaoStatusLabel);
        permissaoCard.Controls.Add(permissaoShortcutLabel);
        permissaoCard.Controls.Add(permissaoArrowLabel);
        permissaoCard.Cursor = Cursors.Hand;
        permissaoCard.Location = new Point(560, 0);
        permissaoCard.Margin = new Padding(0, 0, 20, 20);
        permissaoCard.Name = "permissaoCard";
        permissaoCard.ShadowBlur = 0;
        permissaoCard.ShadowOffsetY = 0;
        permissaoCard.Size = new Size(260, 250);
        permissaoCard.TabIndex = 2;
        // 
        // permissaoIconPanel
        // 
        permissaoIconPanel.BackColor = Color.Transparent;
        permissaoIconPanel.BorderRadius = 10;
        permissaoIconPanel.Controls.Add(permissaoIconLabel);
        permissaoIconPanel.Cursor = Cursors.Hand;
        permissaoIconPanel.FillColor = Color.FromArgb(254, 226, 226);
        permissaoIconPanel.Location = new Point(102, 20);
        permissaoIconPanel.Name = "permissaoIconPanel";
        permissaoIconPanel.ShadowBlur = 0;
        permissaoIconPanel.ShadowOffsetY = 0;
        permissaoIconPanel.Size = new Size(56, 56);
        permissaoIconPanel.TabIndex = 0;
        // 
        // permissaoIconLabel
        // 
        permissaoIconLabel.BackColor = Color.Transparent;
        permissaoIconLabel.Cursor = Cursors.Hand;
        permissaoIconLabel.Dock = DockStyle.Fill;
        permissaoIconLabel.Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0);
        permissaoIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        permissaoIconLabel.Location = new Point(0, 0);
        permissaoIconLabel.Name = "permissaoIconLabel";
        permissaoIconLabel.Size = new Size(56, 56);
        permissaoIconLabel.TabIndex = 0;
        permissaoIconLabel.Text = "";
        permissaoIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoTitleLabel
        // 
        permissaoTitleLabel.BackColor = Color.Transparent;
        permissaoTitleLabel.Cursor = Cursors.Hand;
        permissaoTitleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        permissaoTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        permissaoTitleLabel.Location = new Point(20, 92);
        permissaoTitleLabel.Name = "permissaoTitleLabel";
        permissaoTitleLabel.Size = new Size(220, 62);
        permissaoTitleLabel.TabIndex = 1;
        permissaoTitleLabel.Text = "Permissões";
        permissaoTitleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoDescriptionLabel
        // 
        permissaoDescriptionLabel.BackColor = Color.Transparent;
        permissaoDescriptionLabel.Cursor = Cursors.Hand;
        permissaoDescriptionLabel.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        permissaoDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        permissaoDescriptionLabel.Location = new Point(22, 156);
        permissaoDescriptionLabel.Name = "permissaoDescriptionLabel";
        permissaoDescriptionLabel.Size = new Size(216, 46);
        permissaoDescriptionLabel.TabIndex = 2;
        permissaoDescriptionLabel.Text = "Matriz de acesso por\r\nperfil e rotina.";
        permissaoDescriptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoStatusLabel
        // 
        permissaoStatusLabel.BackColor = Color.FromArgb(220, 252, 231);
        permissaoStatusLabel.Cursor = Cursors.Hand;
        permissaoStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        permissaoStatusLabel.ForeColor = Color.FromArgb(22, 163, 74);
        permissaoStatusLabel.Location = new Point(20, 214);
        permissaoStatusLabel.Name = "permissaoStatusLabel";
        permissaoStatusLabel.Size = new Size(82, 28);
        permissaoStatusLabel.TabIndex = 3;
        permissaoStatusLabel.Text = "Disponível";
        permissaoStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoShortcutLabel
        // 
        permissaoShortcutLabel.BackColor = Color.FromArgb(243, 244, 246);
        permissaoShortcutLabel.Cursor = Cursors.Hand;
        permissaoShortcutLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        permissaoShortcutLabel.ForeColor = Color.FromArgb(75, 85, 99);
        permissaoShortcutLabel.Location = new Point(110, 214);
        permissaoShortcutLabel.Name = "permissaoShortcutLabel";
        permissaoShortcutLabel.Size = new Size(38, 28);
        permissaoShortcutLabel.TabIndex = 4;
        permissaoShortcutLabel.Text = "F4";
        permissaoShortcutLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoArrowLabel
        // 
        permissaoArrowLabel.BackColor = Color.Transparent;
        permissaoArrowLabel.Cursor = Cursors.Hand;
        permissaoArrowLabel.Font = new Font("Segoe UI", 21F, FontStyle.Regular, GraphicsUnit.Point, 0);
        permissaoArrowLabel.ForeColor = Color.FromArgb(229, 27, 43);
        permissaoArrowLabel.Location = new Point(210, 207);
        permissaoArrowLabel.Name = "permissaoArrowLabel";
        permissaoArrowLabel.Size = new Size(32, 36);
        permissaoArrowLabel.TabIndex = 5;
        permissaoArrowLabel.Text = "→";
        permissaoArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // SegurancaForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        Controls.Add(contentPanel);
        Name = "SegurancaForm";
        Size = new Size(1592, 814);
        contentPanel.ResumeLayout(false);
        contentPanel.PerformLayout();
        modulesFlowLayoutPanel.ResumeLayout(false);
        usuarioCard.ResumeLayout(false);
        usuarioIconPanel.ResumeLayout(false);
        perfilAcessoCard.ResumeLayout(false);
        perfilIconPanel.ResumeLayout(false);
        permissaoCard.ResumeLayout(false);
        permissaoIconPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
