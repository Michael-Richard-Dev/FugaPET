namespace FugaPET_Dev.Tela.Teste;

partial class BalanceTestForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootTableLayoutPanel;
    private Label titleLabel;
    private Label weightCaptionLabel;
    private Label weightValueLabel;
    private Button readWeightButton;
    private Label statusLabel;

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
        rootTableLayoutPanel = new TableLayoutPanel();
        titleLabel = new Label();
        weightCaptionLabel = new Label();
        weightValueLabel = new Label();
        readWeightButton = new Button();
        statusLabel = new Label();
        rootTableLayoutPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootTableLayoutPanel
        // 
        rootTableLayoutPanel.BackColor = Color.FromArgb(247, 248, 250);
        rootTableLayoutPanel.ColumnCount = 1;
        rootTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootTableLayoutPanel.Controls.Add(titleLabel, 0, 0);
        rootTableLayoutPanel.Controls.Add(weightCaptionLabel, 0, 1);
        rootTableLayoutPanel.Controls.Add(weightValueLabel, 0, 2);
        rootTableLayoutPanel.Controls.Add(readWeightButton, 0, 3);
        rootTableLayoutPanel.Controls.Add(statusLabel, 0, 4);
        rootTableLayoutPanel.Dock = DockStyle.Fill;
        rootTableLayoutPanel.Location = new Point(0, 0);
        rootTableLayoutPanel.Name = "rootTableLayoutPanel";
        rootTableLayoutPanel.Padding = new Padding(24);
        rootTableLayoutPanel.RowCount = 5;
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        rootTableLayoutPanel.Size = new Size(520, 320);
        rootTableLayoutPanel.TabIndex = 0;
        // 
        // titleLabel
        // 
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
        titleLabel.ForeColor = Color.FromArgb(45, 49, 56);
        titleLabel.Location = new Point(27, 24);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(466, 56);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "Teste de leitura da balanca";
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // weightCaptionLabel
        // 
        weightCaptionLabel.Dock = DockStyle.Fill;
        weightCaptionLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        weightCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        weightCaptionLabel.Location = new Point(27, 80);
        weightCaptionLabel.Name = "weightCaptionLabel";
        weightCaptionLabel.Size = new Size(466, 36);
        weightCaptionLabel.TabIndex = 1;
        weightCaptionLabel.Text = "Peso capturado";
        weightCaptionLabel.TextAlign = ContentAlignment.BottomCenter;
        // 
        // weightValueLabel
        // 
        weightValueLabel.BackColor = Color.White;
        weightValueLabel.BorderStyle = BorderStyle.FixedSingle;
        weightValueLabel.Dock = DockStyle.Fill;
        weightValueLabel.Font = new Font("Segoe UI", 34F, FontStyle.Bold, GraphicsUnit.Point, 0);
        weightValueLabel.ForeColor = Color.FromArgb(184, 18, 32);
        weightValueLabel.Location = new Point(27, 116);
        weightValueLabel.Name = "weightValueLabel";
        weightValueLabel.Size = new Size(466, 90);
        weightValueLabel.TabIndex = 2;
        weightValueLabel.Text = "--";
        weightValueLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // readWeightButton
        // 
        readWeightButton.Anchor = AnchorStyles.None;
        readWeightButton.BackColor = Color.FromArgb(45, 49, 56);
        readWeightButton.FlatAppearance.BorderSize = 0;
        readWeightButton.FlatStyle = FlatStyle.Flat;
        readWeightButton.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
        readWeightButton.ForeColor = Color.White;
        readWeightButton.Location = new Point(170, 217);
        readWeightButton.Name = "readWeightButton";
        readWeightButton.Size = new Size(180, 40);
        readWeightButton.TabIndex = 3;
        readWeightButton.Text = "Ler peso";
        readWeightButton.UseVisualStyleBackColor = false;
        readWeightButton.Click += readWeightButton_Click;
        // 
        // statusLabel
        // 
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        statusLabel.ForeColor = Color.FromArgb(75, 85, 99);
        statusLabel.Location = new Point(27, 262);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(466, 34);
        statusLabel.TabIndex = 4;
        statusLabel.Text = "Pronto para localizar a balanca.";
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // BalanceTestForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 320);
        Controls.Add(rootTableLayoutPanel);
        MinimumSize = new Size(460, 300);
        Name = "BalanceTestForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Teste de Balanca";
        rootTableLayoutPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}



