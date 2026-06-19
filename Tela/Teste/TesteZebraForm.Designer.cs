namespace FugaPET_Dev.Tela.Teste;

partial class TesteZebraForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootTableLayoutPanel;
    private Label titleLabel;
    private Label printerNameLabel;
    private TextBox printerNameTextBox;
    private Label testTextLabel;
    private TextBox testTextBox;
    private FlowLayoutPanel buttonsFlowLayoutPanel;
    private Button printTextButton;
    private Button printTestButton;
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
        printerNameLabel = new Label();
        printerNameTextBox = new TextBox();
        testTextLabel = new Label();
        testTextBox = new TextBox();
        buttonsFlowLayoutPanel = new FlowLayoutPanel();
        printTextButton = new Button();
        printTestButton = new Button();
        statusLabel = new Label();
        rootTableLayoutPanel.SuspendLayout();
        buttonsFlowLayoutPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootTableLayoutPanel
        // 
        rootTableLayoutPanel.BackColor = Color.FromArgb(247, 248, 250);
        rootTableLayoutPanel.ColumnCount = 1;
        rootTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootTableLayoutPanel.Controls.Add(titleLabel, 0, 0);
        rootTableLayoutPanel.Controls.Add(printerNameLabel, 0, 1);
        rootTableLayoutPanel.Controls.Add(printerNameTextBox, 0, 2);
        rootTableLayoutPanel.Controls.Add(testTextLabel, 0, 3);
        rootTableLayoutPanel.Controls.Add(testTextBox, 0, 4);
        rootTableLayoutPanel.Controls.Add(buttonsFlowLayoutPanel, 0, 5);
        rootTableLayoutPanel.Controls.Add(statusLabel, 0, 6);
        rootTableLayoutPanel.Dock = DockStyle.Fill;
        rootTableLayoutPanel.Location = new Point(0, 0);
        rootTableLayoutPanel.Name = "rootTableLayoutPanel";
        rootTableLayoutPanel.Padding = new Padding(24);
        rootTableLayoutPanel.RowCount = 7;
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        rootTableLayoutPanel.Size = new Size(620, 390);
        rootTableLayoutPanel.TabIndex = 0;
        // 
        // titleLabel
        // 
        titleLabel.Dock = DockStyle.Fill;
        titleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point, 0);
        titleLabel.ForeColor = Color.FromArgb(45, 49, 56);
        titleLabel.Location = new Point(27, 24);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(566, 56);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "Teste de impressao Zebra";
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // printerNameLabel
        // 
        printerNameLabel.Dock = DockStyle.Fill;
        printerNameLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        printerNameLabel.ForeColor = Color.FromArgb(75, 85, 99);
        printerNameLabel.Location = new Point(27, 80);
        printerNameLabel.Name = "printerNameLabel";
        printerNameLabel.Size = new Size(566, 28);
        printerNameLabel.TabIndex = 1;
        printerNameLabel.Text = "Nome da impressora";
        printerNameLabel.TextAlign = ContentAlignment.BottomLeft;
        // 
        // printerNameTextBox
        // 
        printerNameTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        printerNameTextBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 0);
        printerNameTextBox.Location = new Point(27, 114);
        printerNameTextBox.Name = "printerNameTextBox";
        printerNameTextBox.Size = new Size(566, 27);
        printerNameTextBox.TabIndex = 2;
        printerNameTextBox.Text = "Zebra ZT411";
        // 
        // testTextLabel
        // 
        testTextLabel.Dock = DockStyle.Fill;
        testTextLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        testTextLabel.ForeColor = Color.FromArgb(75, 85, 99);
        testTextLabel.Location = new Point(27, 150);
        testTextLabel.Name = "testTextLabel";
        testTextLabel.Size = new Size(566, 28);
        testTextLabel.TabIndex = 3;
        testTextLabel.Text = "Texto de teste";
        testTextLabel.TextAlign = ContentAlignment.BottomLeft;
        // 
        // testTextBox
        // 
        testTextBox.Dock = DockStyle.Fill;
        testTextBox.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        testTextBox.Location = new Point(27, 181);
        testTextBox.Multiline = true;
        testTextBox.Name = "testTextBox";
        testTextBox.Size = new Size(566, 91);
        testTextBox.TabIndex = 4;
        testTextBox.Text = "TESTE ZEBRA ZT411";
        // 
        // buttonsFlowLayoutPanel
        // 
        buttonsFlowLayoutPanel.Controls.Add(printTextButton);
        buttonsFlowLayoutPanel.Controls.Add(printTestButton);
        buttonsFlowLayoutPanel.Dock = DockStyle.Fill;
        buttonsFlowLayoutPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonsFlowLayoutPanel.Location = new Point(27, 278);
        buttonsFlowLayoutPanel.Name = "buttonsFlowLayoutPanel";
        buttonsFlowLayoutPanel.Size = new Size(566, 52);
        buttonsFlowLayoutPanel.TabIndex = 5;
        // 
        // printTextButton
        // 
        printTextButton.BackColor = Color.FromArgb(45, 49, 56);
        printTextButton.FlatAppearance.BorderSize = 0;
        printTextButton.FlatStyle = FlatStyle.Flat;
        printTextButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        printTextButton.ForeColor = Color.White;
        printTextButton.Location = new Point(406, 6);
        printTextButton.Margin = new Padding(8, 6, 0, 6);
        printTextButton.Name = "printTextButton";
        printTextButton.Size = new Size(160, 38);
        printTextButton.TabIndex = 0;
        printTextButton.Text = "Imprimir texto";
        printTextButton.UseVisualStyleBackColor = false;
        printTextButton.Click += printTextButton_Click;
        // 
        // printTestButton
        // 
        printTestButton.BackColor = Color.FromArgb(184, 18, 32);
        printTestButton.FlatAppearance.BorderSize = 0;
        printTestButton.FlatStyle = FlatStyle.Flat;
        printTestButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        printTestButton.ForeColor = Color.White;
        printTestButton.Location = new Point(218, 6);
        printTestButton.Margin = new Padding(8, 6, 0, 6);
        printTestButton.Name = "printTestButton";
        printTestButton.Size = new Size(180, 38);
        printTestButton.TabIndex = 1;
        printTestButton.Text = "Imprimir etiqueta";
        printTestButton.UseVisualStyleBackColor = false;
        printTestButton.Click += printTestButton_Click;
        // 
        // statusLabel
        // 
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        statusLabel.ForeColor = Color.FromArgb(75, 85, 99);
        statusLabel.Location = new Point(27, 333);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(566, 33);
        statusLabel.TabIndex = 6;
        statusLabel.Text = "Pronto para teste.";
        statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // TesteZebraForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(620, 390);
        Controls.Add(rootTableLayoutPanel);
        MinimumSize = new Size(560, 360);
        Name = "TesteZebraForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Teste de Impressao Zebra";
        rootTableLayoutPanel.ResumeLayout(false);
        rootTableLayoutPanel.PerformLayout();
        buttonsFlowLayoutPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}



