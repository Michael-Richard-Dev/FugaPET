namespace FugaPET_Dev.Tela.Processo;

partial class ProcessoConsumoMaterialHistoricoForm
{
    private System.ComponentModel.IContainer components = null!;
    private TableLayoutPanel rootLayout;
    private FlowLayoutPanel filtrosPanel;
    private Label ordemLabel;
    private TextBox ordemTextBox;
    private Label statusLabel;
    private ComboBox statusComboBox;
    private Label dataInicialLabel;
    private DateTimePicker dataInicialPicker;
    private Label dataFinalLabel;
    private DateTimePicker dataFinalPicker;
    private Button pesquisarButton;
    private Button atualizarButton;
    private Button fecharButton;
    private DataGridView lancamentosGridView;
    private SplitContainer detalheSplitContainer;
    private DataGridView itensGridView;
    private DataGridView pesagensGridView;
    private FlowLayoutPanel acoesPanel;
    private Button visualizarPayloadButton;
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
        filtrosPanel = new FlowLayoutPanel();
        ordemLabel = new Label();
        ordemTextBox = new TextBox();
        statusLabel = new Label();
        statusComboBox = new ComboBox();
        dataInicialLabel = new Label();
        dataInicialPicker = new DateTimePicker();
        dataFinalLabel = new Label();
        dataFinalPicker = new DateTimePicker();
        pesquisarButton = new Button();
        atualizarButton = new Button();
        fecharButton = new Button();
        lancamentosGridView = new DataGridView();
        detalheSplitContainer = new SplitContainer();
        itensGridView = new DataGridView();
        pesagensGridView = new DataGridView();
        acoesPanel = new FlowLayoutPanel();
        visualizarPayloadButton = new Button();
        mensagemLabel = new Label();
        rootLayout.SuspendLayout();
        filtrosPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)lancamentosGridView).BeginInit();
        ((System.ComponentModel.ISupportInitialize)detalheSplitContainer).BeginInit();
        detalheSplitContainer.Panel1.SuspendLayout();
        detalheSplitContainer.Panel2.SuspendLayout();
        detalheSplitContainer.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)itensGridView).BeginInit();
        ((System.ComponentModel.ISupportInitialize)pesagensGridView).BeginInit();
        acoesPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootLayout
        // 
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(filtrosPanel, 0, 0);
        rootLayout.Controls.Add(lancamentosGridView, 0, 1);
        rootLayout.Controls.Add(detalheSplitContainer, 0, 2);
        rootLayout.Controls.Add(acoesPanel, 0, 3);
        rootLayout.Controls.Add(mensagemLabel, 0, 4);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(16);
        rootLayout.RowCount = 5;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        rootLayout.Size = new Size(1184, 721);
        rootLayout.TabIndex = 0;
        // 
        // filtrosPanel
        // 
        filtrosPanel.Controls.Add(ordemLabel);
        filtrosPanel.Controls.Add(ordemTextBox);
        filtrosPanel.Controls.Add(statusLabel);
        filtrosPanel.Controls.Add(statusComboBox);
        filtrosPanel.Controls.Add(dataInicialLabel);
        filtrosPanel.Controls.Add(dataInicialPicker);
        filtrosPanel.Controls.Add(dataFinalLabel);
        filtrosPanel.Controls.Add(dataFinalPicker);
        filtrosPanel.Controls.Add(pesquisarButton);
        filtrosPanel.Controls.Add(atualizarButton);
        filtrosPanel.Controls.Add(fecharButton);
        filtrosPanel.Dock = DockStyle.Fill;
        filtrosPanel.Location = new Point(19, 19);
        filtrosPanel.Name = "filtrosPanel";
        filtrosPanel.Size = new Size(1146, 40);
        filtrosPanel.TabIndex = 0;
        filtrosPanel.WrapContents = false;
        // 
        // ordemLabel
        // 
        ordemLabel.AutoSize = true;
        ordemLabel.Location = new Point(3, 8);
        ordemLabel.Margin = new Padding(3, 8, 3, 0);
        ordemLabel.Name = "ordemLabel";
        ordemLabel.Size = new Size(68, 15);
        ordemLabel.TabIndex = 0;
        ordemLabel.Text = "Número OP";
        // 
        // ordemTextBox
        // 
        ordemTextBox.Location = new Point(77, 5);
        ordemTextBox.Margin = new Padding(3, 5, 12, 3);
        ordemTextBox.MaxLength = 40;
        ordemTextBox.Name = "ordemTextBox";
        ordemTextBox.Size = new Size(130, 23);
        ordemTextBox.TabIndex = 1;
        // 
        // statusLabel
        // 
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(222, 8);
        statusLabel.Margin = new Padding(3, 8, 3, 0);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(39, 15);
        statusLabel.TabIndex = 2;
        statusLabel.Text = "Status";
        // 
        // statusComboBox
        // 
        statusComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        statusComboBox.FormattingEnabled = true;
        statusComboBox.Location = new Point(267, 5);
        statusComboBox.Margin = new Padding(3, 5, 12, 3);
        statusComboBox.Name = "statusComboBox";
        statusComboBox.Size = new Size(150, 23);
        statusComboBox.TabIndex = 3;
        // 
        // dataInicialLabel
        // 
        dataInicialLabel.AutoSize = true;
        dataInicialLabel.Location = new Point(432, 8);
        dataInicialLabel.Margin = new Padding(3, 8, 3, 0);
        dataInicialLabel.Name = "dataInicialLabel";
        dataInicialLabel.Size = new Size(43, 15);
        dataInicialLabel.TabIndex = 4;
        dataInicialLabel.Text = "De";
        // 
        // dataInicialPicker
        // 
        dataInicialPicker.Format = DateTimePickerFormat.Short;
        dataInicialPicker.Location = new Point(481, 5);
        dataInicialPicker.Margin = new Padding(3, 5, 12, 3);
        dataInicialPicker.Name = "dataInicialPicker";
        dataInicialPicker.Size = new Size(105, 23);
        dataInicialPicker.TabIndex = 5;
        // 
        // dataFinalLabel
        // 
        dataFinalLabel.AutoSize = true;
        dataFinalLabel.Location = new Point(601, 8);
        dataFinalLabel.Margin = new Padding(3, 8, 3, 0);
        dataFinalLabel.Name = "dataFinalLabel";
        dataFinalLabel.Size = new Size(24, 15);
        dataFinalLabel.TabIndex = 6;
        dataFinalLabel.Text = "Até";
        // 
        // dataFinalPicker
        // 
        dataFinalPicker.Format = DateTimePickerFormat.Short;
        dataFinalPicker.Location = new Point(631, 5);
        dataFinalPicker.Margin = new Padding(3, 5, 12, 3);
        dataFinalPicker.Name = "dataFinalPicker";
        dataFinalPicker.Size = new Size(105, 23);
        dataFinalPicker.TabIndex = 7;
        // 
        // pesquisarButton
        // 
        pesquisarButton.Location = new Point(751, 4);
        pesquisarButton.Margin = new Padding(3, 4, 3, 3);
        pesquisarButton.Name = "pesquisarButton";
        pesquisarButton.Size = new Size(90, 27);
        pesquisarButton.TabIndex = 8;
        pesquisarButton.Text = "Pesquisar";
        pesquisarButton.UseVisualStyleBackColor = true;
        // 
        // atualizarButton
        // 
        atualizarButton.Location = new Point(847, 4);
        atualizarButton.Margin = new Padding(3, 4, 3, 3);
        atualizarButton.Name = "atualizarButton";
        atualizarButton.Size = new Size(85, 27);
        atualizarButton.TabIndex = 9;
        atualizarButton.Text = "Atualizar";
        atualizarButton.UseVisualStyleBackColor = true;
        // 
        // fecharButton
        // 
        fecharButton.Location = new Point(938, 4);
        fecharButton.Margin = new Padding(3, 4, 3, 3);
        fecharButton.Name = "fecharButton";
        fecharButton.Size = new Size(75, 27);
        fecharButton.TabIndex = 10;
        fecharButton.Text = "Fechar";
        fecharButton.UseVisualStyleBackColor = true;
        // 
        // lancamentosGridView
        // 
        lancamentosGridView.AllowUserToAddRows = false;
        lancamentosGridView.AllowUserToDeleteRows = false;
        lancamentosGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        lancamentosGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        lancamentosGridView.Dock = DockStyle.Fill;
        lancamentosGridView.Location = new Point(19, 65);
        lancamentosGridView.MultiSelect = false;
        lancamentosGridView.Name = "lancamentosGridView";
        lancamentosGridView.ReadOnly = true;
        lancamentosGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        lancamentosGridView.Size = new Size(1146, 264);
        lancamentosGridView.TabIndex = 1;
        // 
        // detalheSplitContainer
        // 
        detalheSplitContainer.Dock = DockStyle.Fill;
        detalheSplitContainer.Location = new Point(19, 335);
        detalheSplitContainer.Name = "detalheSplitContainer";
        // 
        // detalheSplitContainer.Panel1
        // 
        detalheSplitContainer.Panel1.Controls.Add(itensGridView);
        // 
        // detalheSplitContainer.Panel2
        // 
        detalheSplitContainer.Panel2.Controls.Add(pesagensGridView);
        detalheSplitContainer.Size = new Size(1146, 320);
        detalheSplitContainer.SplitterDistance = 570;
        detalheSplitContainer.TabIndex = 2;
        // 
        // itensGridView
        // 
        itensGridView.AllowUserToAddRows = false;
        itensGridView.AllowUserToDeleteRows = false;
        itensGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        itensGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        itensGridView.Dock = DockStyle.Fill;
        itensGridView.Location = new Point(0, 0);
        itensGridView.MultiSelect = false;
        itensGridView.Name = "itensGridView";
        itensGridView.ReadOnly = true;
        itensGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        itensGridView.Size = new Size(570, 320);
        itensGridView.TabIndex = 0;
        // 
        // pesagensGridView
        // 
        pesagensGridView.AllowUserToAddRows = false;
        pesagensGridView.AllowUserToDeleteRows = false;
        pesagensGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        pesagensGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        pesagensGridView.Dock = DockStyle.Fill;
        pesagensGridView.Location = new Point(0, 0);
        pesagensGridView.MultiSelect = false;
        pesagensGridView.Name = "pesagensGridView";
        pesagensGridView.ReadOnly = true;
        pesagensGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        pesagensGridView.Size = new Size(572, 320);
        pesagensGridView.TabIndex = 0;
        // 
        // acoesPanel
        // 
        acoesPanel.Controls.Add(visualizarPayloadButton);
        acoesPanel.Dock = DockStyle.Fill;
        acoesPanel.Location = new Point(19, 661);
        acoesPanel.Name = "acoesPanel";
        acoesPanel.Size = new Size(1146, 38);
        acoesPanel.TabIndex = 3;
        // 
        // visualizarPayloadButton
        // 
        visualizarPayloadButton.Location = new Point(3, 4);
        visualizarPayloadButton.Margin = new Padding(3, 4, 3, 3);
        visualizarPayloadButton.Name = "visualizarPayloadButton";
        visualizarPayloadButton.Size = new Size(190, 28);
        visualizarPayloadButton.TabIndex = 0;
        visualizarPayloadButton.Text = "Visualizar Payload SAP 261";
        visualizarPayloadButton.UseVisualStyleBackColor = true;
        // 
        // mensagemLabel
        // 
        mensagemLabel.Dock = DockStyle.Fill;
        mensagemLabel.ForeColor = Color.FromArgb(75, 85, 99);
        mensagemLabel.Location = new Point(19, 702);
        mensagemLabel.Name = "mensagemLabel";
        mensagemLabel.Size = new Size(1146, 3);
        mensagemLabel.TabIndex = 4;
        mensagemLabel.Text = "Use os filtros para consultar lançamentos de consumo.";
        mensagemLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // ProcessoConsumoMaterialHistoricoForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(1184, 721);
        Controls.Add(rootLayout);
        MinimumSize = new Size(1000, 650);
        Name = "ProcessoConsumoMaterialHistoricoForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Histórico de Consumo de Matéria-Prima";
        rootLayout.ResumeLayout(false);
        filtrosPanel.ResumeLayout(false);
        filtrosPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)lancamentosGridView).EndInit();
        detalheSplitContainer.Panel1.ResumeLayout(false);
        detalheSplitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)detalheSplitContainer).EndInit();
        detalheSplitContainer.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)itensGridView).EndInit();
        ((System.ComponentModel.ISupportInitialize)pesagensGridView).EndInit();
        acoesPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
