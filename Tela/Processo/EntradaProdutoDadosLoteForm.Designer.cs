using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Processo;

partial class EntradaProdutoDadosLoteForm
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel rootLayout;
    private RoundedPanel cardPanel;
    private TableLayoutPanel cardLayout;
    private Label tituloLabel;
    private Label modoLabel;
    private Label numeroLoteLabel;
    private TextBox numeroLoteTextBox;
    private Label contadorLoteLabel;
    private Label dataFabricacaoLabel;
    private DateTimePicker dataFabricacaoDateTimePicker;
    private Label dataVencimentoLabel;
    private DateTimePicker dataVencimentoDateTimePicker;
    private Label statusLabel;
    private FlowLayoutPanel actionsPanel;
    private Button confirmarButton;
    private Button cancelarButton;

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
        rootLayout = new TableLayoutPanel();
        cardPanel = new RoundedPanel();
        cardLayout = new TableLayoutPanel();
        tituloLabel = new Label();
        modoLabel = new Label();
        numeroLoteLabel = new Label();
        numeroLoteTextBox = new TextBox();
        contadorLoteLabel = new Label();
        dataFabricacaoLabel = new Label();
        dataFabricacaoDateTimePicker = new DateTimePicker();
        dataVencimentoLabel = new Label();
        dataVencimentoDateTimePicker = new DateTimePicker();
        statusLabel = new Label();
        actionsPanel = new FlowLayoutPanel();
        cancelarButton = new Button();
        confirmarButton = new Button();
        rootLayout.SuspendLayout();
        cardPanel.SuspendLayout();
        cardLayout.SuspendLayout();
        actionsPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootLayout
        // 
        rootLayout.BackColor = Color.FromArgb(248, 250, 252);
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(cardPanel, 0, 0);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(24);
        rootLayout.RowCount = 1;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Size = new Size(520, 390);
        rootLayout.TabIndex = 0;
        // 
        // cardPanel
        // 
        cardPanel.BackColor = Color.Transparent;
        cardPanel.BorderColor = Color.FromArgb(226, 232, 240);
        cardPanel.BorderRadius = 14;
        cardPanel.Controls.Add(cardLayout);
        cardPanel.Dock = DockStyle.Fill;
        cardPanel.Location = new Point(27, 27);
        cardPanel.Name = "cardPanel";
        cardPanel.Padding = new Padding(28, 12, 28, 12);
        cardPanel.ShadowBlur = 0;
        cardPanel.ShadowOffsetY = 0;
        cardPanel.Size = new Size(466, 336);
        cardPanel.TabIndex = 0;
        // 
        // cardLayout
        // 
        cardLayout.BackColor = Color.Transparent;
        cardLayout.ColumnCount = 2;
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        cardLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
        cardLayout.Controls.Add(tituloLabel, 0, 0);
        cardLayout.Controls.Add(modoLabel, 0, 1);
        cardLayout.Controls.Add(numeroLoteLabel, 0, 2);
        cardLayout.Controls.Add(numeroLoteTextBox, 0, 3);
        cardLayout.Controls.Add(contadorLoteLabel, 1, 3);
        cardLayout.Controls.Add(dataFabricacaoLabel, 0, 4);
        cardLayout.Controls.Add(dataFabricacaoDateTimePicker, 0, 5);
        cardLayout.Controls.Add(dataVencimentoLabel, 0, 6);
        cardLayout.Controls.Add(dataVencimentoDateTimePicker, 0, 7);
        cardLayout.Controls.Add(statusLabel, 0, 8);
        cardLayout.Controls.Add(actionsPanel, 0, 9);
        cardLayout.Dock = DockStyle.Fill;
        cardLayout.Location = new Point(28, 12);
        cardLayout.Name = "cardLayout";
        cardLayout.RowCount = 10;
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        cardLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        cardLayout.Size = new Size(410, 312);
        cardLayout.TabIndex = 0;
        // 
        // tituloLabel
        // 
        tituloLabel.AutoSize = true;
        cardLayout.SetColumnSpan(tituloLabel, 2);
        tituloLabel.Dock = DockStyle.Fill;
        tituloLabel.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        tituloLabel.ForeColor = Color.FromArgb(15, 23, 42);
        tituloLabel.Location = new Point(0, 0);
        tituloLabel.Margin = new Padding(0);
        tituloLabel.Name = "tituloLabel";
        tituloLabel.Size = new Size(410, 30);
        tituloLabel.TabIndex = 0;
        tituloLabel.Text = "Dados do lote";
        tituloLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // modoLabel
        // 
        modoLabel.AutoSize = true;
        cardLayout.SetColumnSpan(modoLabel, 2);
        modoLabel.Dock = DockStyle.Fill;
        modoLabel.Font = new Font("Segoe UI", 9.5F);
        modoLabel.ForeColor = Color.FromArgb(71, 85, 105);
        modoLabel.Location = new Point(0, 30);
        modoLabel.Margin = new Padding(0);
        modoLabel.Name = "modoLabel";
        modoLabel.Size = new Size(410, 34);
        modoLabel.TabIndex = 1;
        modoLabel.Text = "Informe o lote antes da pesagem.";
        modoLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // numeroLoteLabel
        // 
        numeroLoteLabel.AutoSize = true;
        cardLayout.SetColumnSpan(numeroLoteLabel, 2);
        numeroLoteLabel.Dock = DockStyle.Fill;
        numeroLoteLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        numeroLoteLabel.ForeColor = Color.FromArgb(15, 23, 42);
        numeroLoteLabel.Location = new Point(0, 64);
        numeroLoteLabel.Margin = new Padding(0);
        numeroLoteLabel.Name = "numeroLoteLabel";
        numeroLoteLabel.Size = new Size(410, 20);
        numeroLoteLabel.TabIndex = 2;
        numeroLoteLabel.Text = "Número do lote";
        numeroLoteLabel.TextAlign = ContentAlignment.BottomLeft;
        // 
        // numeroLoteTextBox
        // 
        numeroLoteTextBox.AccessibleName = "Número do lote";
        numeroLoteTextBox.CharacterCasing = CharacterCasing.Upper;
        numeroLoteTextBox.Dock = DockStyle.Fill;
        numeroLoteTextBox.Font = new Font("Segoe UI", 10F);
        numeroLoteTextBox.Location = new Point(0, 87);
        numeroLoteTextBox.Margin = new Padding(0, 3, 8, 3);
        numeroLoteTextBox.MaxLength = 10;
        numeroLoteTextBox.Name = "numeroLoteTextBox";
        numeroLoteTextBox.Size = new Size(340, 25);
        numeroLoteTextBox.TabIndex = 0;
        numeroLoteTextBox.TextChanged += NumeroLoteTextBox_TextChanged;
        // 
        // contadorLoteLabel
        // 
        contadorLoteLabel.AutoSize = true;
        contadorLoteLabel.Dock = DockStyle.Fill;
        contadorLoteLabel.Font = new Font("Segoe UI", 8.5F);
        contadorLoteLabel.ForeColor = Color.FromArgb(100, 116, 139);
        contadorLoteLabel.Location = new Point(351, 84);
        contadorLoteLabel.Name = "contadorLoteLabel";
        contadorLoteLabel.Size = new Size(56, 32);
        contadorLoteLabel.TabIndex = 3;
        contadorLoteLabel.Text = "0/10";
        contadorLoteLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // dataFabricacaoLabel
        // 
        dataFabricacaoLabel.AutoSize = true;
        cardLayout.SetColumnSpan(dataFabricacaoLabel, 2);
        dataFabricacaoLabel.Dock = DockStyle.Fill;
        dataFabricacaoLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dataFabricacaoLabel.ForeColor = Color.FromArgb(15, 23, 42);
        dataFabricacaoLabel.Location = new Point(0, 116);
        dataFabricacaoLabel.Margin = new Padding(0);
        dataFabricacaoLabel.Name = "dataFabricacaoLabel";
        dataFabricacaoLabel.Size = new Size(410, 20);
        dataFabricacaoLabel.TabIndex = 4;
        dataFabricacaoLabel.Text = "Data de fabricação";
        dataFabricacaoLabel.TextAlign = ContentAlignment.BottomLeft;
        // 
        // dataFabricacaoDateTimePicker
        // 
        dataFabricacaoDateTimePicker.AccessibleName = "Data de fabricação";
        dataFabricacaoDateTimePicker.Checked = false;
        cardLayout.SetColumnSpan(dataFabricacaoDateTimePicker, 2);
        dataFabricacaoDateTimePicker.CustomFormat = "dd/MM/yyyy";
        dataFabricacaoDateTimePicker.Dock = DockStyle.Fill;
        dataFabricacaoDateTimePicker.Font = new Font("Segoe UI", 10F);
        dataFabricacaoDateTimePicker.Format = DateTimePickerFormat.Custom;
        dataFabricacaoDateTimePicker.Location = new Point(0, 139);
        dataFabricacaoDateTimePicker.Margin = new Padding(0, 3, 0, 3);
        dataFabricacaoDateTimePicker.Name = "dataFabricacaoDateTimePicker";
        dataFabricacaoDateTimePicker.ShowCheckBox = true;
        dataFabricacaoDateTimePicker.Size = new Size(410, 25);
        dataFabricacaoDateTimePicker.TabIndex = 1;
        // 
        // dataVencimentoLabel
        // 
        dataVencimentoLabel.AutoSize = true;
        cardLayout.SetColumnSpan(dataVencimentoLabel, 2);
        dataVencimentoLabel.Dock = DockStyle.Fill;
        dataVencimentoLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dataVencimentoLabel.ForeColor = Color.FromArgb(15, 23, 42);
        dataVencimentoLabel.Location = new Point(0, 168);
        dataVencimentoLabel.Margin = new Padding(0);
        dataVencimentoLabel.Name = "dataVencimentoLabel";
        dataVencimentoLabel.Size = new Size(410, 20);
        dataVencimentoLabel.TabIndex = 5;
        dataVencimentoLabel.Text = "Data de vencimento";
        dataVencimentoLabel.TextAlign = ContentAlignment.BottomLeft;
        // 
        // dataVencimentoDateTimePicker
        // 
        dataVencimentoDateTimePicker.AccessibleName = "Data de vencimento";
        dataVencimentoDateTimePicker.Checked = false;
        cardLayout.SetColumnSpan(dataVencimentoDateTimePicker, 2);
        dataVencimentoDateTimePicker.CustomFormat = "dd/MM/yyyy";
        dataVencimentoDateTimePicker.Dock = DockStyle.Fill;
        dataVencimentoDateTimePicker.Font = new Font("Segoe UI", 10F);
        dataVencimentoDateTimePicker.Format = DateTimePickerFormat.Custom;
        dataVencimentoDateTimePicker.Location = new Point(0, 191);
        dataVencimentoDateTimePicker.Margin = new Padding(0, 3, 0, 3);
        dataVencimentoDateTimePicker.Name = "dataVencimentoDateTimePicker";
        dataVencimentoDateTimePicker.ShowCheckBox = true;
        dataVencimentoDateTimePicker.Size = new Size(410, 25);
        dataVencimentoDateTimePicker.TabIndex = 2;
        // 
        // statusLabel
        // 
        statusLabel.AutoSize = true;
        cardLayout.SetColumnSpan(statusLabel, 2);
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = new Font("Segoe UI", 8.8F, FontStyle.Bold);
        statusLabel.ForeColor = Color.FromArgb(220, 38, 38);
        statusLabel.Location = new Point(0, 220);
        statusLabel.Margin = new Padding(0);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(410, 42);
        statusLabel.TabIndex = 6;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Visible = false;
        // 
        // actionsPanel
        // 
        cardLayout.SetColumnSpan(actionsPanel, 2);
        actionsPanel.Controls.Add(cancelarButton);
        actionsPanel.Controls.Add(confirmarButton);
        actionsPanel.Dock = DockStyle.Fill;
        actionsPanel.FlowDirection = FlowDirection.RightToLeft;
        actionsPanel.Location = new Point(0, 262);
        actionsPanel.Margin = new Padding(0);
        actionsPanel.Name = "actionsPanel";
        actionsPanel.Padding = new Padding(0, 8, 0, 0);
        actionsPanel.Size = new Size(410, 50);
        actionsPanel.TabIndex = 7;
        // 
        // cancelarButton
        // 
        cancelarButton.DialogResult = DialogResult.Cancel;
        cancelarButton.FlatStyle = FlatStyle.Flat;
        cancelarButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        cancelarButton.ForeColor = Color.FromArgb(51, 65, 85);
        cancelarButton.Location = new Point(287, 8);
        cancelarButton.Margin = new Padding(8, 0, 0, 0);
        cancelarButton.Name = "cancelarButton";
        cancelarButton.Size = new Size(123, 36);
        cancelarButton.TabIndex = 4;
        cancelarButton.Text = "Cancelar";
        cancelarButton.UseVisualStyleBackColor = true;
        cancelarButton.Click += CancelarButton_Click;
        // 
        // confirmarButton
        // 
        confirmarButton.BackColor = Color.FromArgb(220, 38, 38);
        confirmarButton.FlatStyle = FlatStyle.Flat;
        confirmarButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        confirmarButton.ForeColor = Color.White;
        confirmarButton.Location = new Point(156, 8);
        confirmarButton.Margin = new Padding(8, 0, 0, 0);
        confirmarButton.Name = "confirmarButton";
        confirmarButton.Size = new Size(123, 36);
        confirmarButton.TabIndex = 3;
        confirmarButton.Text = "Confirmar";
        confirmarButton.UseVisualStyleBackColor = false;
        confirmarButton.Click += ConfirmarButton_Click;
        // 
        // EntradaProdutoDadosLoteForm
        // 
        AcceptButton = confirmarButton;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(248, 250, 252);
        CancelButton = cancelarButton;
        ClientSize = new Size(520, 390);
        Controls.Add(rootLayout);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "EntradaProdutoDadosLoteForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Dados do lote";
        rootLayout.ResumeLayout(false);
        cardPanel.ResumeLayout(false);
        cardLayout.ResumeLayout(false);
        cardLayout.PerformLayout();
        actionsPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
