namespace FugaPET_Dev.Tela.Processo;

partial class DiagnosticoConsumoSap261Form
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel rootLayout;
    private FlowLayoutPanel resumoPanel;
    private Label testeLocalCard;
    private Label previewCard;
    private Label envioSapCard;
    private DataGridView itensGridView;
    private FlowLayoutPanel botoesPanel;
    private Button executarButton;
    private Button copiarButton;
    private Button fecharButton;
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
        components = new System.ComponentModel.Container();
        rootLayout = new TableLayoutPanel();
        resumoPanel = new FlowLayoutPanel();
        testeLocalCard = new Label();
        previewCard = new Label();
        envioSapCard = new Label();
        itensGridView = new DataGridView();
        botoesPanel = new FlowLayoutPanel();
        executarButton = new Button();
        copiarButton = new Button();
        fecharButton = new Button();
        mensagemLabel = new Label();
        rootLayout.SuspendLayout();
        resumoPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)itensGridView).BeginInit();
        botoesPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootLayout
        // 
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(resumoPanel, 0, 0);
        rootLayout.Controls.Add(itensGridView, 0, 1);
        rootLayout.Controls.Add(botoesPanel, 0, 2);
        rootLayout.Controls.Add(mensagemLabel, 0, 3);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(16);
        rootLayout.RowCount = 4;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        rootLayout.Size = new Size(980, 640);
        rootLayout.TabIndex = 0;
        // 
        // resumoPanel
        // 
        resumoPanel.Controls.Add(testeLocalCard);
        resumoPanel.Controls.Add(previewCard);
        resumoPanel.Controls.Add(envioSapCard);
        resumoPanel.Dock = DockStyle.Fill;
        resumoPanel.Location = new Point(19, 19);
        resumoPanel.Name = "resumoPanel";
        resumoPanel.Size = new Size(942, 68);
        resumoPanel.TabIndex = 0;
        resumoPanel.WrapContents = false;
        // 
        // testeLocalCard
        // 
        testeLocalCard.BackColor = Color.FromArgb(243, 244, 246);
        testeLocalCard.BorderStyle = BorderStyle.FixedSingle;
        testeLocalCard.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        testeLocalCard.Location = new Point(3, 3);
        testeLocalCard.Margin = new Padding(3, 3, 12, 3);
        testeLocalCard.Name = "testeLocalCard";
        testeLocalCard.Size = new Size(230, 58);
        testeLocalCard.TabIndex = 0;
        testeLocalCard.Text = "Teste local: -";
        testeLocalCard.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // previewCard
        // 
        previewCard.BackColor = Color.FromArgb(243, 244, 246);
        previewCard.BorderStyle = BorderStyle.FixedSingle;
        previewCard.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        previewCard.Location = new Point(248, 3);
        previewCard.Margin = new Padding(3, 3, 12, 3);
        previewCard.Name = "previewCard";
        previewCard.Size = new Size(230, 58);
        previewCard.TabIndex = 1;
        previewCard.Text = "Preview 261: -";
        previewCard.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // envioSapCard
        // 
        envioSapCard.BackColor = Color.FromArgb(243, 244, 246);
        envioSapCard.BorderStyle = BorderStyle.FixedSingle;
        envioSapCard.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        envioSapCard.Location = new Point(493, 3);
        envioSapCard.Margin = new Padding(3, 3, 12, 3);
        envioSapCard.Name = "envioSapCard";
        envioSapCard.Size = new Size(230, 58);
        envioSapCard.TabIndex = 2;
        envioSapCard.Text = "Envio SAP 261: -";
        envioSapCard.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // itensGridView
        // 
        itensGridView.AllowUserToAddRows = false;
        itensGridView.AllowUserToDeleteRows = false;
        itensGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        itensGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        itensGridView.Dock = DockStyle.Fill;
        itensGridView.Location = new Point(19, 93);
        itensGridView.MultiSelect = false;
        itensGridView.Name = "itensGridView";
        itensGridView.ReadOnly = true;
        itensGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        itensGridView.Size = new Size(942, 450);
        itensGridView.TabIndex = 1;
        // 
        // botoesPanel
        // 
        botoesPanel.Controls.Add(executarButton);
        botoesPanel.Controls.Add(copiarButton);
        botoesPanel.Controls.Add(fecharButton);
        botoesPanel.Dock = DockStyle.Fill;
        botoesPanel.Location = new Point(19, 549);
        botoesPanel.Name = "botoesPanel";
        botoesPanel.Size = new Size(942, 38);
        botoesPanel.TabIndex = 2;
        // 
        // executarButton
        // 
        executarButton.Location = new Point(3, 4);
        executarButton.Margin = new Padding(3, 4, 8, 3);
        executarButton.Name = "executarButton";
        executarButton.Size = new Size(150, 28);
        executarButton.TabIndex = 0;
        executarButton.Text = "Executar Diagnóstico";
        executarButton.UseVisualStyleBackColor = true;
        // 
        // copiarButton
        // 
        copiarButton.Location = new Point(164, 4);
        copiarButton.Margin = new Padding(3, 4, 8, 3);
        copiarButton.Name = "copiarButton";
        copiarButton.Size = new Size(130, 28);
        copiarButton.TabIndex = 1;
        copiarButton.Text = "Copiar Resultado";
        copiarButton.UseVisualStyleBackColor = true;
        // 
        // fecharButton
        // 
        fecharButton.Location = new Point(305, 4);
        fecharButton.Margin = new Padding(3, 4, 8, 3);
        fecharButton.Name = "fecharButton";
        fecharButton.Size = new Size(80, 28);
        fecharButton.TabIndex = 2;
        fecharButton.Text = "Fechar";
        fecharButton.UseVisualStyleBackColor = true;
        // 
        // mensagemLabel
        // 
        mensagemLabel.Dock = DockStyle.Fill;
        mensagemLabel.ForeColor = Color.FromArgb(75, 85, 99);
        mensagemLabel.Location = new Point(19, 590);
        mensagemLabel.Name = "mensagemLabel";
        mensagemLabel.Size = new Size(942, 34);
        mensagemLabel.TabIndex = 3;
        mensagemLabel.Text = "Execute o diagnóstico para verificar a prontidão do Consumo SAP 261.";
        mensagemLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // DiagnosticoConsumoSap261Form
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(980, 640);
        Controls.Add(rootLayout);
        MinimumSize = new Size(900, 560);
        Name = "DiagnosticoConsumoSap261Form";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Diagnóstico de Consumo SAP 261";
        rootLayout.ResumeLayout(false);
        resumoPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)itensGridView).EndInit();
        botoesPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
