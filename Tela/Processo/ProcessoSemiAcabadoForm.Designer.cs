namespace FugaPET_Dev.Tela.Processo;

partial class ProcessoSemiAcabadoForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootTableLayoutPanel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel apontamentoInfoPanel;
    private Panel apontamentoInfoAccentBar;
    private Label apontamentoInfoCaptionLabel;
    private Label apontamentoInfoValueLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel apontamentoChipPanel;
    private Panel apontamentoChipAccentBar;
    private Label apontamentoChipCaptionLabel;
    private Label apontamentoChipValueLabel;
    private Panel customTitleBarPanel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel sidePanel;
    private TableLayoutPanel sidePanelLayout;
    private GroupBox sideActionsGroupBox;
    private Label statusLabel;
    private DataGridView materialDataGridView;
    private DataGridView productionDataGridView;
    private Label sideReadingStatusLabel;
    private Panel startActionPanel;
    private PictureBox startActionIconLabel;
    private Label startActionTextLabel;
    private Panel stopActionPanel;
    private PictureBox stopActionIconLabel;
    private Label stopActionTextLabel;
    private Label boxesCaptionLabel;
    private Label boxesCounterLabel;
    private Label packagesCaptionLabel;
    private Label packagesCounterLabel;
    private Panel readWeightLegendPanel;
    private PictureBox readWeightLegendIconLabel;
    private Label readWeightLegendTextLabel;
    private Panel manualLotLegendPanel;
    private PictureBox manualLotLegendIconLabel;
    private Label manualLotLegendTextLabel;
    private Panel deleteLastLegendPanel;
    private PictureBox deleteLastLegendIconLabel;
    private Label deleteLastLegendTextLabel;
    private Panel deleteByCodeLegendPanel;
    private PictureBox deleteByCodeLegendIconLabel;
    private Label deleteByCodeLegendTextLabel;
    private DataGridViewTextBoxColumn materialStatusColumn;
    private DataGridViewTextBoxColumn materialCodeColumn;
    private DataGridViewTextBoxColumn materialDescriptionColumn;
    private DataGridViewTextBoxColumn materialLotColumn;
    private DataGridViewTextBoxColumn materialExpirationColumn;
    private DataGridViewTextBoxColumn materialBalanceColumn;
    private DataGridViewTextBoxColumn productionCodeColumn;
    private DataGridViewTextBoxColumn productionProductColumn;
    private DataGridViewTextBoxColumn productionQuantityColumn;
    private DataGridViewTextBoxColumn productionWeightColumn;
    private DataGridViewTextBoxColumn productionPesoLidoColumn;
    private DataGridViewTextBoxColumn productionItemIdColumn;
    private DataGridViewTextBoxColumn productionPesoOrigemColumn;
    private DataGridViewTextBoxColumn productionNumeroItemColumn;
    private Panel weightSummaryAccentBar;
    private Label weightSummaryTitleLabel;
    private Label weightSummarySubtitleLabel;
    private Label weightSummaryDividerLabel;
    private Panel weightSummaryForecastPanel;
    private Panel weightSummaryUsedPanel;

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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ProcessoSemiAcabadoForm));
        DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle5 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle6 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle11 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle7 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle8 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle9 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle10 = new DataGridViewCellStyle();
        rootTableLayoutPanel = new TableLayoutPanel();
        tableLayoutPanel1 = new TableLayoutPanel();
        tableLayoutPanel3 = new TableLayoutPanel();
        productionOrderShadowPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        productionOrderCaptionLabel = new Label();
        pedidoComboBox = new ComboBox();
        productionOrderIconPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        productionOrderSearchLabel = new PictureBox();
        lotCardPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        lotCaptionLabel = new Label();
        lotTextBox = new TextBox();
        lotIconPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        lotIconPictureBox = new PictureBox();
        stepCardPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        stepCaptionLabel = new Label();
        stepLabel = new Label();
        stepDescriptionLabel = new Label();
        finishedProductCardPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        finishedProductCaptionLabel = new Label();
        finishedProductCodeTextBox = new TextBox();
        finishedProductTextBox = new TextBox();
        finishedProductIconPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        finishedProductIconPictureBox = new PictureBox();
        tableLayoutPanel11 = new TableLayoutPanel();
        productionReadingsPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        productionReadingsTitleIconPictureBox = new PictureBox();
        productionReadingsTitleLabel = new Label();
        productionReadingsUnderlineLabel = new Label();
        productionSearchPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        productionSearchTextBox = new TextBox();
        productionSearchGlyphLabel = new Label();
        productionFilterButton = new Button();
        productionActionsButton = new Button();
        productionDataGridView = new DataGridView();
        productionCodeColumn = new DataGridViewTextBoxColumn();
        productionProductColumn = new DataGridViewTextBoxColumn();
        productionQuantityColumn = new DataGridViewTextBoxColumn();
        productionWeightColumn = new DataGridViewTextBoxColumn();
        productionPesoLidoColumn = new DataGridViewTextBoxColumn();
        productionItemIdColumn = new DataGridViewTextBoxColumn();
        productionPesoOrigemColumn = new DataGridViewTextBoxColumn();
        productionNumeroItemColumn = new DataGridViewTextBoxColumn();
        productionFooterLabel = new Label();
        productionPageLabel = new Label();
        productionPreviousPageButton = new Button();
        productionPageTextBox = new TextBox();
        productionNextPageButton = new Button();
        apontamentoInfoPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        apontamentoInfoAccentBar = new Panel();
        apontamentoInfoCaptionLabel = new Label();
        apontamentoInfoValueLabel = new Label();
        apontamentoChipPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        apontamentoChipAccentBar = new Panel();
        apontamentoChipCaptionLabel = new Label();
        apontamentoChipValueLabel = new Label();
        tableLayoutPanel5 = new TableLayoutPanel();
        groupBox2 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        dateTitleIconPictureBox = new PictureBox();
        dateTitleLabel = new Label();
        dateDividerLabel1 = new Label();
        dateDividerLabel2 = new Label();
        dateDividerLabel3 = new Label();
        tableLayoutPanel6 = new TableLayoutPanel();
        ovenExitCaptionLabel = new Label();
        classificationDateCaptionLabel = new Label();
        manufacturingDateCaptionLabel = new Label();
        expirationDateCaptionLabel = new Label();
        ovenExitTextBox = new TextBox();
        classificationDateTextBox = new TextBox();
        manufacturingDateTextBox = new TextBox();
        expirationDateTextBox = new TextBox();
        Gpb_PrevisaoLeitura = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        plannedProductionTitleIconPictureBox = new PictureBox();
        plannedProductionTitleLabel = new Label();
        plannedProductionDividerLabel1 = new Label();
        plannedProductionDividerLabel2 = new Label();
        tableLayoutPanel8 = new TableLayoutPanel();
        readForecastPackagesCaptionLabel = new Label();
        readForecastPackagesTextBox = new TextBox();
        readForecastBoxesCaptionLabel = new Label();
        readForecastBoxesTextBox = new TextBox();
        balanceCaptionLabel = new Label();
        balanceTextBox = new TextBox();
        headerPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        materialTitleIconPictureBox = new PictureBox();
        materialTitleLabel = new Label();
        materialTitleUnderlineLabel = new Label();
        materialFilterIconPictureBox = new PictureBox();
        materialViewAllLabel = new Label();
        materialViewAllChevronLabel = new Label();
        tableLayoutPanel10 = new TableLayoutPanel();
        materialDataGridView = new DataGridView();
        materialStatusColumn = new DataGridViewTextBoxColumn();
        materialCodeColumn = new DataGridViewTextBoxColumn();
        materialDescriptionColumn = new DataGridViewTextBoxColumn();
        materialLotColumn = new DataGridViewTextBoxColumn();
        materialExpirationColumn = new DataGridViewTextBoxColumn();
        materialBalanceColumn = new DataGridViewTextBoxColumn();
        productionSearchIconPictureBox = new PictureBox();
        tableLayoutPanel7 = new TableLayoutPanel();
        label1 = new Label();
        lotSearchIconLabel = new PictureBox();
        statusLabel = new Label();
        footerBar = new Panel();
        footerBarLayout = new TableLayoutPanel();
        cellUser = new Panel();
        cellUserText = new Label();
        cellUserIcon = new Label();
        cellUserDivider = new Panel();
        cellTerminal = new Panel();
        cellTerminalText = new Label();
        cellTerminalIcon = new Label();
        cellTerminalDivider = new Panel();
        cellEmpresa = new Panel();
        cellEmpresaText = new Label();
        cellEmpresaIcon = new Label();
        cellEmpresaDivider = new Panel();
        cellBanco = new Panel();
        cellBancoText = new Label();
        cellBancoIcon = new Label();
        cellBancoDivider = new Panel();
        cellHora = new Panel();
        cellHoraText = new Label();
        cellHoraIcon = new Label();
        cellHoraDivider = new Panel();
        cellData = new Panel();
        cellDataText = new Label();
        cellDataIcon = new Label();
        statusCell = new Panel();
        statusCellDivider = new Panel();
        groupBox1 = new GroupBox();
        tableLayoutPanel9 = new TableLayoutPanel();
        customTitleBarPanel = new Panel();
        logoSaLabel = new Label();
        menuHeaderLabel = new Label();
        companyLogoPictureBox = new PictureBox();
        headerDividerLabel = new Label();
        headerTitleIconPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        headerTitleIconPictureBox = new PictureBox();
        headerTitleLabel = new Label();
        sapStatusPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        sapStatusDotLabel = new Label();
        sapStatusLabel = new Label();
        minimizeWindowLabel = new Label();
        maximizeWindowLabel = new Label();
        closeWindowLabel = new Label();
        headerSubtitleLabel = new Label();
        sidePanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        sideStatusTitleLabel = new Label();
        statusCard = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        statusCardIcon = new Label();
        statusValueLabel = new Label();
        statusHintLabel = new Label();
        groupBox4 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        tableLayoutPanel12 = new TableLayoutPanel();
        weightSummaryForecastPanel = new Panel();
        boxesCaptionLabel = new Label();
        boxesCounterLabel = new Label();
        weightSummaryUsedPanel = new Panel();
        packagesCaptionLabel = new Label();
        packagesCounterLabel = new Label();
        weightSummaryAccentBar = new Panel();
        weightSummaryTitleLabel = new Label();
        weightSummarySubtitleLabel = new Label();
        weightSummaryDividerLabel = new Label();
        iniciarLeituraButton = new ActionPillButton();
        lerEtiquetaButton = new ActionPillButton();
        leituraManualButton = new ActionPillButton();
        sidePanelLayout = new TableLayoutPanel();
        groupBox3 = new GroupBox();
        sideReadingStatusLabel = new Label();
        sideActionsGroupBox = new GroupBox();
        tableLayoutPanel13 = new TableLayoutPanel();
        startActionPanel = new Panel();
        startActionIconLabel = new PictureBox();
        startActionTextLabel = new Label();
        stopActionPanel = new Panel();
        stopActionIconLabel = new PictureBox();
        stopActionTextLabel = new Label();
        deleteLastLegendPanel = new Panel();
        deleteLastLegendIconLabel = new PictureBox();
        deleteLastLegendTextLabel = new Label();
        deleteByCodeLegendPanel = new Panel();
        deleteByCodeLegendIconLabel = new PictureBox();
        deleteByCodeLegendTextLabel = new Label();
        readWeightLegendPanel = new Panel();
        readWeightLegendIconLabel = new PictureBox();
        readWeightLegendTextLabel = new Label();
        manualLotLegendPanel = new Panel();
        manualLotLegendIconLabel = new PictureBox();
        manualLotLegendTextLabel = new Label();
        tableLayoutPanel2 = new TableLayoutPanel();
        rootTableLayoutPanel.SuspendLayout();
        tableLayoutPanel1.SuspendLayout();
        tableLayoutPanel3.SuspendLayout();
        productionOrderShadowPanel.SuspendLayout();
        productionOrderIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)productionOrderSearchLabel).BeginInit();
        lotCardPanel.SuspendLayout();
        lotIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)lotIconPictureBox).BeginInit();
        stepCardPanel.SuspendLayout();
        finishedProductCardPanel.SuspendLayout();
        finishedProductIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)finishedProductIconPictureBox).BeginInit();
        tableLayoutPanel11.SuspendLayout();
        productionReadingsPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)productionReadingsTitleIconPictureBox).BeginInit();
        productionSearchPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)productionDataGridView).BeginInit();
        apontamentoInfoPanel.SuspendLayout();
        apontamentoChipPanel.SuspendLayout();
        tableLayoutPanel5.SuspendLayout();
        groupBox2.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dateTitleIconPictureBox).BeginInit();
        tableLayoutPanel6.SuspendLayout();
        Gpb_PrevisaoLeitura.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)plannedProductionTitleIconPictureBox).BeginInit();
        tableLayoutPanel8.SuspendLayout();
        headerPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)materialTitleIconPictureBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)materialFilterIconPictureBox).BeginInit();
        tableLayoutPanel10.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)materialDataGridView).BeginInit();
        ((System.ComponentModel.ISupportInitialize)productionSearchIconPictureBox).BeginInit();
        tableLayoutPanel7.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)lotSearchIconLabel).BeginInit();
        footerBar.SuspendLayout();
        footerBarLayout.SuspendLayout();
        cellUser.SuspendLayout();
        cellTerminal.SuspendLayout();
        cellEmpresa.SuspendLayout();
        cellBanco.SuspendLayout();
        cellHora.SuspendLayout();
        cellData.SuspendLayout();
        statusCell.SuspendLayout();
        groupBox1.SuspendLayout();
        customTitleBarPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).BeginInit();
        headerTitleIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).BeginInit();
        sapStatusPanel.SuspendLayout();
        sidePanel.SuspendLayout();
        statusCard.SuspendLayout();
        groupBox4.SuspendLayout();
        tableLayoutPanel12.SuspendLayout();
        weightSummaryForecastPanel.SuspendLayout();
        weightSummaryUsedPanel.SuspendLayout();
        sidePanelLayout.SuspendLayout();
        groupBox3.SuspendLayout();
        sideActionsGroupBox.SuspendLayout();
        tableLayoutPanel13.SuspendLayout();
        startActionPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)startActionIconLabel).BeginInit();
        stopActionPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)stopActionIconLabel).BeginInit();
        deleteLastLegendPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)deleteLastLegendIconLabel).BeginInit();
        deleteByCodeLegendPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)deleteByCodeLegendIconLabel).BeginInit();
        readWeightLegendPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)readWeightLegendIconLabel).BeginInit();
        manualLotLegendPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)manualLotLegendIconLabel).BeginInit();
        tableLayoutPanel2.SuspendLayout();
        SuspendLayout();
        // 
        // rootTableLayoutPanel
        // 
        rootTableLayoutPanel.BackColor = Color.FromArgb(247, 248, 250);
        rootTableLayoutPanel.ColumnCount = 1;
        rootTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootTableLayoutPanel.Controls.Add(tableLayoutPanel1, 0, 0);
        rootTableLayoutPanel.Controls.Add(tableLayoutPanel11, 0, 1);
        rootTableLayoutPanel.Dock = DockStyle.Fill;
        rootTableLayoutPanel.Location = new Point(3, 55);
        rootTableLayoutPanel.Name = "rootTableLayoutPanel";
        rootTableLayoutPanel.Padding = new Padding(6, 6, 6, 0);
        rootTableLayoutPanel.RowCount = 2;
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        rootTableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootTableLayoutPanel.Size = new Size(1133, 624);
        rootTableLayoutPanel.TabIndex = 0;
        // 
        // tableLayoutPanel1
        // 
        tableLayoutPanel1.BackColor = Color.FromArgb(247, 248, 250);
        tableLayoutPanel1.ColumnCount = 1;
        tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel1.Controls.Add(tableLayoutPanel3, 0, 0);
        tableLayoutPanel1.Dock = DockStyle.Fill;
        tableLayoutPanel1.Location = new Point(9, 9);
        tableLayoutPanel1.Name = "tableLayoutPanel1";
        tableLayoutPanel1.RowCount = 1;
        tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel1.Size = new Size(1115, 80);
        tableLayoutPanel1.TabIndex = 3;
        // 
        // tableLayoutPanel3
        // 
        tableLayoutPanel3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        tableLayoutPanel3.ColumnCount = 4;
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19F));
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39F));
        tableLayoutPanel3.Controls.Add(productionOrderShadowPanel, 0, 0);
        tableLayoutPanel3.Controls.Add(lotCardPanel, 1, 0);
        tableLayoutPanel3.Controls.Add(stepCardPanel, 2, 0);
        tableLayoutPanel3.Controls.Add(finishedProductCardPanel, 3, 0);
        tableLayoutPanel3.Location = new Point(0, 3);
        tableLayoutPanel3.Margin = new Padding(0, 3, 0, 3);
        tableLayoutPanel3.Name = "tableLayoutPanel3";
        tableLayoutPanel3.RowCount = 2;
        tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 28.8135586F));
        tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 71.18644F));
        tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        tableLayoutPanel3.Size = new Size(1115, 69);
        tableLayoutPanel3.TabIndex = 0;
        // 
        // productionOrderShadowPanel
        // 
        productionOrderShadowPanel.BackColor = Color.Transparent;
        productionOrderShadowPanel.Controls.Add(productionOrderCaptionLabel);
        productionOrderShadowPanel.Controls.Add(pedidoComboBox);
        productionOrderShadowPanel.Controls.Add(productionOrderIconPanel);
        productionOrderShadowPanel.Dock = DockStyle.Fill;
        productionOrderShadowPanel.Location = new Point(0, 2);
        productionOrderShadowPanel.Margin = new Padding(0, 2, 10, 2);
        productionOrderShadowPanel.Name = "productionOrderShadowPanel";
        tableLayoutPanel3.SetRowSpan(productionOrderShadowPanel, 2);
        productionOrderShadowPanel.ShadowBlur = 0;
        productionOrderShadowPanel.ShadowColor = Color.FromArgb(28, 15, 23, 42);
        productionOrderShadowPanel.ShadowOffsetY = 0;
        productionOrderShadowPanel.Size = new Size(201, 65);
        productionOrderShadowPanel.TabIndex = 6;
        // 
        // productionOrderCaptionLabel
        // 
        productionOrderCaptionLabel.BackColor = Color.Transparent;
        productionOrderCaptionLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionOrderCaptionLabel.ForeColor = Color.FromArgb(55, 65, 81);
        productionOrderCaptionLabel.Location = new Point(16, 6);
        productionOrderCaptionLabel.Name = "productionOrderCaptionLabel";
        productionOrderCaptionLabel.Size = new Size(132, 14);
        productionOrderCaptionLabel.TabIndex = 0;
        productionOrderCaptionLabel.Text = "PEDIDO";
        // 
        // pedidoComboBox
        // 
        pedidoComboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pedidoComboBox.BackColor = Color.White;
        pedidoComboBox.DropDownWidth = 220;
        pedidoComboBox.FlatStyle = FlatStyle.Flat;
        pedidoComboBox.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        pedidoComboBox.ForeColor = Color.FromArgb(229, 27, 43);
        pedidoComboBox.Location = new Point(16, 24);
        pedidoComboBox.Name = "pedidoComboBox";
        pedidoComboBox.Size = new Size(173, 29);
        pedidoComboBox.TabIndex = 1;
        // 
        // productionOrderIconPanel
        // 
        productionOrderIconPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionOrderIconPanel.BackColor = Color.Transparent;
        productionOrderIconPanel.Controls.Add(productionOrderSearchLabel);
        productionOrderIconPanel.FillColor = Color.FromArgb(253, 237, 240);
        productionOrderIconPanel.Location = new Point(151, 12);
        productionOrderIconPanel.Name = "productionOrderIconPanel";
        productionOrderIconPanel.ShadowBlur = 0;
        productionOrderIconPanel.ShadowOffsetY = 0;
        productionOrderIconPanel.Size = new Size(38, 38);
        productionOrderIconPanel.TabIndex = 3;
        // 
        // productionOrderSearchLabel
        // 
        productionOrderSearchLabel.BackColor = Color.Transparent;
        productionOrderSearchLabel.Dock = DockStyle.Fill;
        productionOrderSearchLabel.Image = (Image)resources.GetObject("productionOrderSearchLabel.Image");
        productionOrderSearchLabel.Location = new Point(0, 0);
        productionOrderSearchLabel.Name = "productionOrderSearchLabel";
        productionOrderSearchLabel.Size = new Size(38, 38);
        productionOrderSearchLabel.SizeMode = PictureBoxSizeMode.CenterImage;
        productionOrderSearchLabel.TabIndex = 2;
        productionOrderSearchLabel.TabStop = false;
        // 
        // lotCardPanel
        // 
        lotCardPanel.BackColor = Color.Transparent;
        lotCardPanel.Controls.Add(lotCaptionLabel);
        lotCardPanel.Controls.Add(lotTextBox);
        lotCardPanel.Controls.Add(lotIconPanel);
        lotCardPanel.Dock = DockStyle.Fill;
        lotCardPanel.Location = new Point(214, 2);
        lotCardPanel.Margin = new Padding(3, 2, 8, 2);
        lotCardPanel.Name = "lotCardPanel";
        tableLayoutPanel3.SetRowSpan(lotCardPanel, 2);
        lotCardPanel.ShadowBlur = 0;
        lotCardPanel.ShadowOffsetY = 0;
        lotCardPanel.Size = new Size(212, 65);
        lotCardPanel.TabIndex = 7;
        // 
        // lotCaptionLabel
        // 
        lotCaptionLabel.BackColor = Color.Transparent;
        lotCaptionLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lotCaptionLabel.ForeColor = Color.FromArgb(55, 65, 81);
        lotCaptionLabel.Location = new Point(16, 6);
        lotCaptionLabel.Name = "lotCaptionLabel";
        lotCaptionLabel.Size = new Size(80, 14);
        lotCaptionLabel.TabIndex = 3;
        lotCaptionLabel.Text = "FORNECEDOR";
        // 
        // lotTextBox
        // 
        lotTextBox.BackColor = Color.White;
        lotTextBox.BorderStyle = BorderStyle.None;
        lotTextBox.Font = new Font("Segoe UI", 17.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lotTextBox.ForeColor = Color.FromArgb(229, 27, 43);
        lotTextBox.Location = new Point(16, 25);
        lotTextBox.Multiline = true;
        lotTextBox.Name = "lotTextBox";
        lotTextBox.ReadOnly = true;
        lotTextBox.Size = new Size(143, 29);
        lotTextBox.TabIndex = 4;
        // 
        // lotIconPanel
        // 
        lotIconPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lotIconPanel.BackColor = Color.Transparent;
        lotIconPanel.Controls.Add(lotIconPictureBox);
        lotIconPanel.FillColor = Color.FromArgb(253, 237, 240);
        lotIconPanel.Location = new Point(164, 12);
        lotIconPanel.Name = "lotIconPanel";
        lotIconPanel.ShadowBlur = 0;
        lotIconPanel.ShadowOffsetY = 0;
        lotIconPanel.Size = new Size(38, 38);
        lotIconPanel.TabIndex = 2;
        // 
        // lotIconPictureBox
        // 
        lotIconPictureBox.BackColor = Color.Transparent;
        lotIconPictureBox.Dock = DockStyle.Fill;
        lotIconPictureBox.Image = (Image)resources.GetObject("lotIconPictureBox.Image");
        lotIconPictureBox.Location = new Point(0, 0);
        lotIconPictureBox.Name = "lotIconPictureBox";
        lotIconPictureBox.Size = new Size(38, 38);
        lotIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        lotIconPictureBox.TabIndex = 0;
        lotIconPictureBox.TabStop = false;
        // 
        // stepCardPanel
        // 
        stepCardPanel.BackColor = Color.Transparent;
        stepCardPanel.Controls.Add(stepCaptionLabel);
        stepCardPanel.Controls.Add(stepLabel);
        stepCardPanel.Controls.Add(stepDescriptionLabel);
        stepCardPanel.Dock = DockStyle.Fill;
        stepCardPanel.Location = new Point(437, 2);
        stepCardPanel.Margin = new Padding(3, 2, 8, 2);
        stepCardPanel.Name = "stepCardPanel";
        tableLayoutPanel3.SetRowSpan(stepCardPanel, 2);
        stepCardPanel.ShadowBlur = 0;
        stepCardPanel.ShadowOffsetY = 0;
        stepCardPanel.Size = new Size(234, 65);
        stepCardPanel.TabIndex = 8;
        // 
        // stepCaptionLabel
        // 
        stepCaptionLabel.BackColor = Color.Transparent;
        stepCaptionLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        stepCaptionLabel.ForeColor = Color.FromArgb(55, 65, 81);
        stepCaptionLabel.Location = new Point(16, 6);
        stepCaptionLabel.Name = "stepCaptionLabel";
        stepCaptionLabel.Size = new Size(80, 14);
        stepCaptionLabel.TabIndex = 0;
        stepCaptionLabel.Text = "DATA";
        // 
        // stepLabel
        // 
        stepLabel.BackColor = Color.Transparent;
        stepLabel.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 0);
        stepLabel.ForeColor = Color.FromArgb(229, 27, 43);
        stepLabel.Location = new Point(16, 24);
        stepLabel.Name = "stepLabel";
        stepLabel.Size = new Size(108, 18);
        stepLabel.TabIndex = 5;
        stepLabel.Text = "--/--/----";
        // 
        // stepDescriptionLabel
        // 
        stepDescriptionLabel.BackColor = Color.Transparent;
        stepDescriptionLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        stepDescriptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        stepDescriptionLabel.Location = new Point(16, 42);
        stepDescriptionLabel.Name = "stepDescriptionLabel";
        stepDescriptionLabel.Size = new Size(170, 14);
        stepDescriptionLabel.TabIndex = 6;
        stepDescriptionLabel.Text = "OP selecionada";
        stepDescriptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // finishedProductCardPanel
        // 
        finishedProductCardPanel.BackColor = Color.Transparent;
        finishedProductCardPanel.Controls.Add(finishedProductCaptionLabel);
        finishedProductCardPanel.Controls.Add(finishedProductCodeTextBox);
        finishedProductCardPanel.Controls.Add(finishedProductTextBox);
        finishedProductCardPanel.Controls.Add(finishedProductIconPanel);
        finishedProductCardPanel.Dock = DockStyle.Fill;
        finishedProductCardPanel.Location = new Point(682, 2);
        finishedProductCardPanel.Margin = new Padding(3, 2, 0, 2);
        finishedProductCardPanel.Name = "finishedProductCardPanel";
        tableLayoutPanel3.SetRowSpan(finishedProductCardPanel, 2);
        finishedProductCardPanel.ShadowBlur = 0;
        finishedProductCardPanel.ShadowOffsetY = 0;
        finishedProductCardPanel.Size = new Size(433, 65);
        finishedProductCardPanel.TabIndex = 9;
        // 
        // finishedProductCaptionLabel
        // 
        finishedProductCaptionLabel.BackColor = Color.Transparent;
        finishedProductCaptionLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        finishedProductCaptionLabel.ForeColor = Color.FromArgb(55, 65, 81);
        finishedProductCaptionLabel.Location = new Point(16, 6);
        finishedProductCaptionLabel.Name = "finishedProductCaptionLabel";
        finishedProductCaptionLabel.Size = new Size(160, 14);
        finishedProductCaptionLabel.TabIndex = 6;
        finishedProductCaptionLabel.Text = "TIPO DE PEDIDO";
        // 
        // finishedProductCodeTextBox
        // 
        finishedProductCodeTextBox.BackColor = Color.White;
        finishedProductCodeTextBox.BorderStyle = BorderStyle.None;
        finishedProductCodeTextBox.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        finishedProductCodeTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        finishedProductCodeTextBox.Location = new Point(16, 23);
        finishedProductCodeTextBox.Multiline = true;
        finishedProductCodeTextBox.Name = "finishedProductCodeTextBox";
        finishedProductCodeTextBox.ReadOnly = true;
        finishedProductCodeTextBox.Size = new Size(80, 20);
        finishedProductCodeTextBox.TabIndex = 7;
        // 
        // finishedProductTextBox
        // 
        finishedProductTextBox.BackColor = Color.White;
        finishedProductTextBox.BorderStyle = BorderStyle.None;
        finishedProductTextBox.Font = new Font("Cascadia Code", 12.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        finishedProductTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        finishedProductTextBox.Location = new Point(16, 43);
        finishedProductTextBox.Multiline = true;
        finishedProductTextBox.Name = "finishedProductTextBox";
        finishedProductTextBox.ReadOnly = true;
        finishedProductTextBox.Size = new Size(363, 20);
        finishedProductTextBox.TabIndex = 8;
        // 
        // finishedProductIconPanel
        // 
        finishedProductIconPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        finishedProductIconPanel.BackColor = Color.Transparent;
        finishedProductIconPanel.Controls.Add(finishedProductIconPictureBox);
        finishedProductIconPanel.FillColor = Color.FromArgb(253, 237, 240);
        finishedProductIconPanel.Location = new Point(385, 12);
        finishedProductIconPanel.Name = "finishedProductIconPanel";
        finishedProductIconPanel.ShadowBlur = 0;
        finishedProductIconPanel.ShadowOffsetY = 0;
        finishedProductIconPanel.Size = new Size(38, 38);
        finishedProductIconPanel.TabIndex = 3;
        // 
        // finishedProductIconPictureBox
        // 
        finishedProductIconPictureBox.BackColor = Color.Transparent;
        finishedProductIconPictureBox.Dock = DockStyle.Fill;
        finishedProductIconPictureBox.Image = (Image)resources.GetObject("finishedProductIconPictureBox.Image");
        finishedProductIconPictureBox.Location = new Point(0, 0);
        finishedProductIconPictureBox.Name = "finishedProductIconPictureBox";
        finishedProductIconPictureBox.Size = new Size(38, 38);
        finishedProductIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        finishedProductIconPictureBox.TabIndex = 0;
        finishedProductIconPictureBox.TabStop = false;
        // 
        // tableLayoutPanel11
        // 
        tableLayoutPanel11.ColumnCount = 1;
        tableLayoutPanel11.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel11.Controls.Add(productionReadingsPanel, 0, 0);
        tableLayoutPanel11.Dock = DockStyle.Fill;
        tableLayoutPanel11.Location = new Point(6, 92);
        tableLayoutPanel11.Margin = new Padding(0);
        tableLayoutPanel11.Name = "tableLayoutPanel11";
        tableLayoutPanel11.RowCount = 1;
        tableLayoutPanel11.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel11.Size = new Size(1121, 532);
        tableLayoutPanel11.TabIndex = 4;
        // 
        // productionReadingsPanel
        // 
        productionReadingsPanel.BackColor = Color.Transparent;
        productionReadingsPanel.BorderRadius = 7;
        productionReadingsPanel.Controls.Add(productionReadingsTitleIconPictureBox);
        productionReadingsPanel.Controls.Add(productionReadingsTitleLabel);
        productionReadingsPanel.Controls.Add(productionReadingsUnderlineLabel);
        productionReadingsPanel.Controls.Add(productionSearchPanel);
        productionReadingsPanel.Controls.Add(productionFilterButton);
        productionReadingsPanel.Controls.Add(productionActionsButton);
        productionReadingsPanel.Controls.Add(productionDataGridView);
        productionReadingsPanel.Controls.Add(productionFooterLabel);
        productionReadingsPanel.Controls.Add(productionPageLabel);
        productionReadingsPanel.Controls.Add(productionPreviousPageButton);
        productionReadingsPanel.Controls.Add(productionPageTextBox);
        productionReadingsPanel.Controls.Add(productionNextPageButton);
        productionReadingsPanel.Dock = DockStyle.Fill;
        productionReadingsPanel.Location = new Point(0, 0);
        productionReadingsPanel.Margin = new Padding(0);
        productionReadingsPanel.Name = "productionReadingsPanel";
        productionReadingsPanel.ShadowBlur = 0;
        productionReadingsPanel.ShadowOffsetY = 0;
        productionReadingsPanel.Size = new Size(1121, 532);
        productionReadingsPanel.TabIndex = 0;
        // 
        // productionReadingsTitleIconPictureBox
        // 
        productionReadingsTitleIconPictureBox.BackColor = Color.Transparent;
        productionReadingsTitleIconPictureBox.Image = (Image)resources.GetObject("productionReadingsTitleIconPictureBox.Image");
        productionReadingsTitleIconPictureBox.Location = new Point(17, 14);
        productionReadingsTitleIconPictureBox.Name = "productionReadingsTitleIconPictureBox";
        productionReadingsTitleIconPictureBox.Size = new Size(14, 14);
        productionReadingsTitleIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        productionReadingsTitleIconPictureBox.TabIndex = 0;
        productionReadingsTitleIconPictureBox.TabStop = false;
        // 
        // productionReadingsTitleLabel
        // 
        productionReadingsTitleLabel.BackColor = Color.Transparent;
        productionReadingsTitleLabel.Font = new Font("Cascadia Code", 7.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionReadingsTitleLabel.ForeColor = Color.FromArgb(31, 41, 55);
        productionReadingsTitleLabel.Location = new Point(34, 13);
        productionReadingsTitleLabel.Name = "productionReadingsTitleLabel";
        productionReadingsTitleLabel.Size = new Size(160, 16);
        productionReadingsTitleLabel.TabIndex = 1;
        productionReadingsTitleLabel.Text = "ITENS DO PEDIDO";
        productionReadingsTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // productionReadingsUnderlineLabel
        // 
        productionReadingsUnderlineLabel.BackColor = Color.FromArgb(229, 27, 43);
        productionReadingsUnderlineLabel.Location = new Point(17, 36);
        productionReadingsUnderlineLabel.Name = "productionReadingsUnderlineLabel";
        productionReadingsUnderlineLabel.Size = new Size(62, 2);
        productionReadingsUnderlineLabel.TabIndex = 2;
        // 
        // productionSearchPanel
        // 
        productionSearchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionSearchPanel.BackColor = Color.Transparent;
        productionSearchPanel.BorderRadius = 4;
        productionSearchPanel.Controls.Add(productionSearchTextBox);
        productionSearchPanel.Controls.Add(productionSearchGlyphLabel);
        productionSearchPanel.FillColor = Color.FromArgb(248, 250, 252);
        productionSearchPanel.Location = new Point(681, 10);
        productionSearchPanel.Name = "productionSearchPanel";
        productionSearchPanel.ShadowBlur = 0;
        productionSearchPanel.ShadowOffsetY = 0;
        productionSearchPanel.Size = new Size(215, 24);
        productionSearchPanel.TabIndex = 3;
        // 
        // productionSearchTextBox
        // 
        productionSearchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        productionSearchTextBox.BackColor = Color.FromArgb(248, 250, 252);
        productionSearchTextBox.BorderStyle = BorderStyle.None;
        productionSearchTextBox.Font = new Font("Cascadia Code", 6.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        productionSearchTextBox.ForeColor = Color.FromArgb(107, 114, 128);
        productionSearchTextBox.Location = new Point(10, 5);
        productionSearchTextBox.Name = "productionSearchTextBox";
        productionSearchTextBox.PlaceholderText = "Pesquisar itens...";
        productionSearchTextBox.Size = new Size(175, 11);
        productionSearchTextBox.TabIndex = 0;
        // 
        // productionSearchGlyphLabel
        // 
        productionSearchGlyphLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionSearchGlyphLabel.BackColor = Color.Transparent;
        productionSearchGlyphLabel.Font = new Font("Segoe MDL2 Assets", 8.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        productionSearchGlyphLabel.ForeColor = Color.FromArgb(148, 163, 184);
        productionSearchGlyphLabel.Location = new Point(190, 4);
        productionSearchGlyphLabel.Name = "productionSearchGlyphLabel";
        productionSearchGlyphLabel.Size = new Size(16, 16);
        productionSearchGlyphLabel.TabIndex = 1;
        productionSearchGlyphLabel.Text = "?";
        productionSearchGlyphLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // productionFilterButton
        // 
        productionFilterButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionFilterButton.BackColor = Color.White;
        productionFilterButton.FlatAppearance.BorderColor = Color.FromArgb(226, 231, 238);
        productionFilterButton.FlatStyle = FlatStyle.Flat;
        productionFilterButton.Font = new Font("Cascadia Code", 6.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionFilterButton.ForeColor = Color.FromArgb(31, 41, 55);
        productionFilterButton.Location = new Point(908, 12);
        productionFilterButton.Name = "productionFilterButton";
        productionFilterButton.Size = new Size(78, 24);
        productionFilterButton.TabIndex = 5;
        productionFilterButton.Text = "=  Filtros";
        productionFilterButton.UseVisualStyleBackColor = false;
        // 
        // productionActionsButton
        // 
        productionActionsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionActionsButton.BackColor = Color.White;
        productionActionsButton.FlatAppearance.BorderColor = Color.FromArgb(226, 231, 238);
        productionActionsButton.FlatStyle = FlatStyle.Flat;
        productionActionsButton.Font = new Font("Cascadia Code", 6.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionActionsButton.ForeColor = Color.FromArgb(31, 41, 55);
        productionActionsButton.Location = new Point(996, 12);
        productionActionsButton.Name = "productionActionsButton";
        productionActionsButton.Size = new Size(108, 24);
        productionActionsButton.TabIndex = 6;
        productionActionsButton.Text = "Ações  ?";
        productionActionsButton.UseVisualStyleBackColor = false;
        // 
        // productionDataGridView
        // 
        productionDataGridView.AllowUserToAddRows = false;
        productionDataGridView.AllowUserToDeleteRows = false;
        productionDataGridView.AllowUserToResizeRows = false;
        productionDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        productionDataGridView.BackgroundColor = Color.White;
        productionDataGridView.BorderStyle = BorderStyle.None;
        productionDataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dataGridViewCellStyle1.BackColor = Color.FromArgb(17, 24, 39);
        dataGridViewCellStyle1.Font = new Font("Cascadia Code", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        dataGridViewCellStyle1.ForeColor = Color.White;
        dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(17, 24, 39);
        dataGridViewCellStyle1.SelectionForeColor = Color.White;
        productionDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
        productionDataGridView.ColumnHeadersHeight = 24;
        productionDataGridView.Columns.AddRange(new DataGridViewColumn[] { productionCodeColumn, productionProductColumn, productionQuantityColumn, productionWeightColumn, productionPesoLidoColumn, productionItemIdColumn, productionPesoOrigemColumn, productionNumeroItemColumn });
        dataGridViewCellStyle5.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle5.BackColor = Color.FromArgb(250, 251, 252);
        dataGridViewCellStyle5.Font = new Font("Cascadia Code", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        dataGridViewCellStyle5.ForeColor = Color.FromArgb(45, 49, 56);
        dataGridViewCellStyle5.SelectionBackColor = Color.FromArgb(229, 27, 43);
        dataGridViewCellStyle5.SelectionForeColor = Color.White;
        dataGridViewCellStyle5.WrapMode = DataGridViewTriState.False;
        productionDataGridView.DefaultCellStyle = dataGridViewCellStyle5;
        productionDataGridView.EnableHeadersVisualStyles = false;
        productionDataGridView.GridColor = Color.FromArgb(226, 231, 238);
        productionDataGridView.Location = new Point(17, 44);
        productionDataGridView.MultiSelect = false;
        productionDataGridView.Name = "productionDataGridView";
        productionDataGridView.ReadOnly = true;
        productionDataGridView.RowHeadersVisible = false;
        productionDataGridView.RowHeadersWidth = 51;
        productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productionDataGridView.Size = new Size(1087, 444);
        productionDataGridView.TabIndex = 7;
        // 
        // productionCodeColumn
        // 
        dataGridViewCellStyle2.Font = new Font("Segoe UI", 9F);
        productionCodeColumn.DefaultCellStyle = dataGridViewCellStyle2;
        productionCodeColumn.HeaderText = "Material";
        productionCodeColumn.MinimumWidth = 6;
        productionCodeColumn.Name = "productionCodeColumn";
        productionCodeColumn.ReadOnly = true;
        productionCodeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionCodeColumn.Width = 150;
        // 
        // productionProductColumn
        // 
        productionProductColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        productionProductColumn.FillWeight = 210F;
        productionProductColumn.HeaderText = "Descrição do Material";
        productionProductColumn.MinimumWidth = 6;
        productionProductColumn.Name = "productionProductColumn";
        productionProductColumn.ReadOnly = true;
        productionProductColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        // 
        // productionQuantityColumn
        // 
        dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F);
        productionQuantityColumn.DefaultCellStyle = dataGridViewCellStyle3;
        productionQuantityColumn.HeaderText = "Quantidade";
        productionQuantityColumn.MinimumWidth = 6;
        productionQuantityColumn.Name = "productionQuantityColumn";
        productionQuantityColumn.ReadOnly = true;
        productionQuantityColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionQuantityColumn.Width = 95;
        // 
        // productionWeightColumn
        // 
        dataGridViewCellStyle4.Font = new Font("Segoe UI", 9F);
        productionWeightColumn.DefaultCellStyle = dataGridViewCellStyle4;
        productionWeightColumn.HeaderText = "Unidade";
        productionWeightColumn.MinimumWidth = 6;
        productionWeightColumn.Name = "productionWeightColumn";
        productionWeightColumn.ReadOnly = true;
        productionWeightColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionWeightColumn.Width = 110;
        // 
        // productionPesoLidoColumn
        // 
        productionPesoLidoColumn.HeaderText = "Peso";
        productionPesoLidoColumn.MinimumWidth = 6;
        productionPesoLidoColumn.Name = "productionPesoLidoColumn";
        productionPesoLidoColumn.ReadOnly = true;
        productionPesoLidoColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionPesoLidoColumn.Width = 110;
        // 
        // productionItemIdColumn
        // 
        productionItemIdColumn.HeaderText = "ItemId";
        productionItemIdColumn.Name = "productionItemIdColumn";
        productionItemIdColumn.ReadOnly = true;
        productionItemIdColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionItemIdColumn.Visible = false;
        // 
        // productionPesoOrigemColumn
        // 
        productionPesoOrigemColumn.HeaderText = "Origem";
        productionPesoOrigemColumn.Name = "productionPesoOrigemColumn";
        productionPesoOrigemColumn.ReadOnly = true;
        productionPesoOrigemColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionPesoOrigemColumn.Visible = false;
        // 
        // productionNumeroItemColumn
        // 
        productionNumeroItemColumn.HeaderText = "NumeroItem";
        productionNumeroItemColumn.Name = "productionNumeroItemColumn";
        productionNumeroItemColumn.ReadOnly = true;
        productionNumeroItemColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        productionNumeroItemColumn.Visible = false;
        // 
        // productionFooterLabel
        // 
        productionFooterLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        productionFooterLabel.BackColor = Color.Transparent;
        productionFooterLabel.Font = new Font("Segoe UI", 6.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        productionFooterLabel.ForeColor = Color.FromArgb(75, 85, 99);
        productionFooterLabel.Location = new Point(17, 499);
        productionFooterLabel.Name = "productionFooterLabel";
        productionFooterLabel.Size = new Size(220, 18);
        productionFooterLabel.TabIndex = 8;
        productionFooterLabel.Text = "Exibindo 0 de 0 itens";
        productionFooterLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // productionPageLabel
        // 
        productionPageLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        productionPageLabel.BackColor = Color.Transparent;
        productionPageLabel.Font = new Font("Segoe UI", 6.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        productionPageLabel.ForeColor = Color.FromArgb(75, 85, 99);
        productionPageLabel.Location = new Point(898, 499);
        productionPageLabel.Name = "productionPageLabel";
        productionPageLabel.Size = new Size(80, 18);
        productionPageLabel.TabIndex = 9;
        productionPageLabel.Text = "Página 1 de 20";
        productionPageLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // productionPreviousPageButton
        // 
        productionPreviousPageButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        productionPreviousPageButton.BackColor = Color.Transparent;
        productionPreviousPageButton.FlatAppearance.BorderSize = 0;
        productionPreviousPageButton.FlatStyle = FlatStyle.Flat;
        productionPreviousPageButton.Font = new Font("Cascadia Code", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionPreviousPageButton.ForeColor = Color.FromArgb(148, 163, 184);
        productionPreviousPageButton.Location = new Point(984, 494);
        productionPreviousPageButton.Name = "productionPreviousPageButton";
        productionPreviousPageButton.Size = new Size(24, 26);
        productionPreviousPageButton.TabIndex = 10;
        productionPreviousPageButton.Text = "‹";
        productionPreviousPageButton.UseVisualStyleBackColor = false;
        // 
        // productionPageTextBox
        // 
        productionPageTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        productionPageTextBox.BackColor = Color.White;
        productionPageTextBox.BorderStyle = BorderStyle.FixedSingle;
        productionPageTextBox.Font = new Font("Segoe UI", 7F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionPageTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        productionPageTextBox.Location = new Point(1011, 496);
        productionPageTextBox.Name = "productionPageTextBox";
        productionPageTextBox.ReadOnly = true;
        productionPageTextBox.Size = new Size(44, 20);
        productionPageTextBox.TabIndex = 11;
        productionPageTextBox.Text = "1";
        productionPageTextBox.TextAlign = HorizontalAlignment.Center;
        // 
        // productionNextPageButton
        // 
        productionNextPageButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        productionNextPageButton.BackColor = Color.Transparent;
        productionNextPageButton.FlatAppearance.BorderSize = 0;
        productionNextPageButton.FlatStyle = FlatStyle.Flat;
        productionNextPageButton.Font = new Font("Cascadia Code", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionNextPageButton.ForeColor = Color.FromArgb(148, 163, 184);
        productionNextPageButton.Location = new Point(1062, 494);
        productionNextPageButton.Name = "productionNextPageButton";
        productionNextPageButton.Size = new Size(24, 26);
        productionNextPageButton.TabIndex = 12;
        productionNextPageButton.Text = "›";
        productionNextPageButton.UseVisualStyleBackColor = false;
        // 
        // apontamentoInfoPanel
        // 
        apontamentoInfoPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        apontamentoInfoPanel.BackColor = Color.Transparent;
        apontamentoInfoPanel.BorderRadius = 7;
        apontamentoInfoPanel.Controls.Add(apontamentoInfoAccentBar);
        apontamentoInfoPanel.Controls.Add(apontamentoInfoCaptionLabel);
        apontamentoInfoPanel.Controls.Add(apontamentoInfoValueLabel);
        apontamentoInfoPanel.FillColor = Color.FromArgb(255, 247, 247);
        apontamentoInfoPanel.Font = new Font("Cascadia Code", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        apontamentoInfoPanel.Location = new Point(12, 544);
        apontamentoInfoPanel.Name = "apontamentoInfoPanel";
        apontamentoInfoPanel.ShadowBlur = 0;
        apontamentoInfoPanel.ShadowOffsetY = 0;
        apontamentoInfoPanel.Size = new Size(191, 48);
        apontamentoInfoPanel.TabIndex = 11;
        // 
        // apontamentoInfoAccentBar
        // 
        apontamentoInfoAccentBar.BackColor = Color.FromArgb(229, 27, 43);
        apontamentoInfoAccentBar.Location = new Point(10, 11);
        apontamentoInfoAccentBar.Name = "apontamentoInfoAccentBar";
        apontamentoInfoAccentBar.Size = new Size(6, 22);
        apontamentoInfoAccentBar.TabIndex = 0;
        // 
        // apontamentoInfoCaptionLabel
        // 
        apontamentoInfoCaptionLabel.BackColor = Color.Transparent;
        apontamentoInfoCaptionLabel.Font = new Font("Cascadia Code", 7.15F, FontStyle.Bold, GraphicsUnit.Point, 0);
        apontamentoInfoCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        apontamentoInfoCaptionLabel.Location = new Point(22, 6);
        apontamentoInfoCaptionLabel.Name = "apontamentoInfoCaptionLabel";
        apontamentoInfoCaptionLabel.Size = new Size(160, 12);
        apontamentoInfoCaptionLabel.TabIndex = 1;
        apontamentoInfoCaptionLabel.Text = "INFORMAÇÃO DE PESAGEM";
        // 
        // apontamentoInfoValueLabel
        // 
        apontamentoInfoValueLabel.BackColor = Color.Transparent;
        apontamentoInfoValueLabel.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        apontamentoInfoValueLabel.ForeColor = Color.FromArgb(17, 24, 39);
        apontamentoInfoValueLabel.Location = new Point(22, 20);
        apontamentoInfoValueLabel.Name = "apontamentoInfoValueLabel";
        apontamentoInfoValueLabel.Size = new Size(160, 16);
        apontamentoInfoValueLabel.TabIndex = 2;
        apontamentoInfoValueLabel.Text = "Apontamento Nº 70530";
        // 
        // apontamentoChipPanel
        // 
        apontamentoChipPanel.Anchor = AnchorStyles.Top;
        apontamentoChipPanel.BackColor = Color.Transparent;
        apontamentoChipPanel.BorderRadius = 14;
        apontamentoChipPanel.Controls.Add(apontamentoChipAccentBar);
        apontamentoChipPanel.Controls.Add(apontamentoChipCaptionLabel);
        apontamentoChipPanel.Controls.Add(apontamentoChipValueLabel);
        apontamentoChipPanel.FillColor = Color.FromArgb(255, 255, 255);
        apontamentoChipPanel.Location = new Point(656, 10);
        apontamentoChipPanel.Name = "apontamentoChipPanel";
        apontamentoChipPanel.ShadowBlur = 0;
        apontamentoChipPanel.ShadowOffsetY = 0;
        apontamentoChipPanel.Size = new Size(111, 37);
        apontamentoChipPanel.TabIndex = 12;
        // 
        // apontamentoChipAccentBar
        // 
        apontamentoChipAccentBar.BackColor = Color.FromArgb(229, 27, 43);
        apontamentoChipAccentBar.Location = new Point(10, 8);
        apontamentoChipAccentBar.Name = "apontamentoChipAccentBar";
        apontamentoChipAccentBar.Size = new Size(5, 20);
        apontamentoChipAccentBar.TabIndex = 0;
        // 
        // apontamentoChipCaptionLabel
        // 
        apontamentoChipCaptionLabel.BackColor = Color.Transparent;
        apontamentoChipCaptionLabel.Font = new Font("Cascadia Code", 6.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        apontamentoChipCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        apontamentoChipCaptionLabel.Location = new Point(20, 3);
        apontamentoChipCaptionLabel.Name = "apontamentoChipCaptionLabel";
        apontamentoChipCaptionLabel.Size = new Size(104, 16);
        apontamentoChipCaptionLabel.TabIndex = 1;
        apontamentoChipCaptionLabel.Text = "APONTAMENTO";
        // 
        // apontamentoChipValueLabel
        // 
        apontamentoChipValueLabel.BackColor = Color.Transparent;
        apontamentoChipValueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        apontamentoChipValueLabel.ForeColor = Color.FromArgb(17, 24, 39);
        apontamentoChipValueLabel.Location = new Point(20, 12);
        apontamentoChipValueLabel.Name = "apontamentoChipValueLabel";
        apontamentoChipValueLabel.Size = new Size(88, 22);
        apontamentoChipValueLabel.TabIndex = 2;
        apontamentoChipValueLabel.Text = "Nº 70530";
        apontamentoChipValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // tableLayoutPanel5
        // 
        tableLayoutPanel5.ColumnCount = 2;
        tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        tableLayoutPanel5.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
        tableLayoutPanel5.Controls.Add(groupBox2, 0, 0);
        tableLayoutPanel5.Controls.Add(Gpb_PrevisaoLeitura, 1, 0);
        tableLayoutPanel5.Dock = DockStyle.Fill;
        tableLayoutPanel5.Location = new Point(0, 78);
        tableLayoutPanel5.Margin = new Padding(0, 3, 0, 3);
        tableLayoutPanel5.Name = "tableLayoutPanel5";
        tableLayoutPanel5.RowCount = 1;
        tableLayoutPanel5.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel5.Size = new Size(1115, 87);
        tableLayoutPanel5.TabIndex = 2;
        // 
        // groupBox2
        // 
        groupBox2.BackColor = Color.Transparent;
        groupBox2.BorderRadius = 7;
        groupBox2.Controls.Add(dateTitleIconPictureBox);
        groupBox2.Controls.Add(dateTitleLabel);
        groupBox2.Controls.Add(dateDividerLabel1);
        groupBox2.Controls.Add(dateDividerLabel2);
        groupBox2.Controls.Add(dateDividerLabel3);
        groupBox2.Controls.Add(tableLayoutPanel6);
        groupBox2.Dock = DockStyle.Fill;
        groupBox2.Font = new Font("Cascadia Code", 9F, FontStyle.Bold);
        groupBox2.ForeColor = Color.FromArgb(75, 85, 99);
        groupBox2.Location = new Point(0, 3);
        groupBox2.Margin = new Padding(0, 3, 8, 3);
        groupBox2.Name = "groupBox2";
        groupBox2.Padding = new Padding(17, 38, 14, 7);
        groupBox2.ShadowBlur = 0;
        groupBox2.ShadowOffsetY = 0;
        groupBox2.Size = new Size(438, 81);
        groupBox2.TabIndex = 2;
        groupBox2.Visible = false;
        groupBox2.Resize += AlignDateCardLayout;
        // 
        // dateTitleIconPictureBox
        // 
        dateTitleIconPictureBox.BackColor = Color.Transparent;
        dateTitleIconPictureBox.Image = (Image)resources.GetObject("dateTitleIconPictureBox.Image");
        dateTitleIconPictureBox.Location = new Point(17, 13);
        dateTitleIconPictureBox.Name = "dateTitleIconPictureBox";
        dateTitleIconPictureBox.Size = new Size(14, 14);
        dateTitleIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        dateTitleIconPictureBox.TabIndex = 1;
        dateTitleIconPictureBox.TabStop = false;
        // 
        // dateTitleLabel
        // 
        dateTitleLabel.BackColor = Color.Transparent;
        dateTitleLabel.Font = new Font("Cascadia Code", 7.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        dateTitleLabel.ForeColor = Color.FromArgb(31, 41, 55);
        dateTitleLabel.Location = new Point(34, 12);
        dateTitleLabel.Name = "dateTitleLabel";
        dateTitleLabel.Size = new Size(70, 16);
        dateTitleLabel.TabIndex = 2;
        dateTitleLabel.Text = "DATAS";
        dateTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // dateDividerLabel1
        // 
        dateDividerLabel1.BackColor = Color.FromArgb(226, 231, 238);
        dateDividerLabel1.Location = new Point(116, 49);
        dateDividerLabel1.Name = "dateDividerLabel1";
        dateDividerLabel1.Size = new Size(1, 22);
        dateDividerLabel1.TabIndex = 3;
        // 
        // dateDividerLabel2
        // 
        dateDividerLabel2.BackColor = Color.FromArgb(226, 231, 238);
        dateDividerLabel2.Location = new Point(223, 49);
        dateDividerLabel2.Name = "dateDividerLabel2";
        dateDividerLabel2.Size = new Size(1, 22);
        dateDividerLabel2.TabIndex = 4;
        // 
        // dateDividerLabel3
        // 
        dateDividerLabel3.BackColor = Color.FromArgb(226, 231, 238);
        dateDividerLabel3.Location = new Point(330, 49);
        dateDividerLabel3.Name = "dateDividerLabel3";
        dateDividerLabel3.Size = new Size(1, 22);
        dateDividerLabel3.TabIndex = 5;
        // 
        // tableLayoutPanel6
        // 
        tableLayoutPanel6.BackColor = Color.Transparent;
        tableLayoutPanel6.ColumnCount = 4;
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
        tableLayoutPanel6.Controls.Add(ovenExitCaptionLabel, 0, 0);
        tableLayoutPanel6.Controls.Add(classificationDateCaptionLabel, 1, 0);
        tableLayoutPanel6.Controls.Add(manufacturingDateCaptionLabel, 2, 0);
        tableLayoutPanel6.Controls.Add(expirationDateCaptionLabel, 3, 0);
        tableLayoutPanel6.Controls.Add(ovenExitTextBox, 0, 1);
        tableLayoutPanel6.Controls.Add(classificationDateTextBox, 1, 1);
        tableLayoutPanel6.Controls.Add(manufacturingDateTextBox, 2, 1);
        tableLayoutPanel6.Controls.Add(expirationDateTextBox, 3, 1);
        tableLayoutPanel6.Dock = DockStyle.Fill;
        tableLayoutPanel6.Location = new Point(17, 38);
        tableLayoutPanel6.Margin = new Padding(0);
        tableLayoutPanel6.Name = "tableLayoutPanel6";
        tableLayoutPanel6.RowCount = 2;
        tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
        tableLayoutPanel6.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel6.Size = new Size(407, 36);
        tableLayoutPanel6.TabIndex = 0;
        // 
        // ovenExitCaptionLabel
        // 
        ovenExitCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        ovenExitCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ovenExitCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        ovenExitCaptionLabel.Location = new Point(3, 1);
        ovenExitCaptionLabel.Name = "ovenExitCaptionLabel";
        ovenExitCaptionLabel.Size = new Size(95, 13);
        ovenExitCaptionLabel.TabIndex = 9;
        ovenExitCaptionLabel.Text = "SAIDA ESTUFA";
        // 
        // classificationDateCaptionLabel
        // 
        classificationDateCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        classificationDateCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        classificationDateCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        classificationDateCaptionLabel.Location = new Point(104, 1);
        classificationDateCaptionLabel.Name = "classificationDateCaptionLabel";
        classificationDateCaptionLabel.Size = new Size(95, 13);
        classificationDateCaptionLabel.TabIndex = 11;
        classificationDateCaptionLabel.Text = "CLASSIFICAÇÃO";
        // 
        // manufacturingDateCaptionLabel
        // 
        manufacturingDateCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        manufacturingDateCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        manufacturingDateCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        manufacturingDateCaptionLabel.Location = new Point(205, 1);
        manufacturingDateCaptionLabel.Name = "manufacturingDateCaptionLabel";
        manufacturingDateCaptionLabel.Size = new Size(95, 13);
        manufacturingDateCaptionLabel.TabIndex = 13;
        manufacturingDateCaptionLabel.Text = "FABRICAÇÃO";
        // 
        // expirationDateCaptionLabel
        // 
        expirationDateCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        expirationDateCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        expirationDateCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        expirationDateCaptionLabel.Location = new Point(306, 1);
        expirationDateCaptionLabel.Name = "expirationDateCaptionLabel";
        expirationDateCaptionLabel.Size = new Size(98, 13);
        expirationDateCaptionLabel.TabIndex = 15;
        expirationDateCaptionLabel.Text = "VENCIMENTO";
        // 
        // ovenExitTextBox
        // 
        ovenExitTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        ovenExitTextBox.BackColor = Color.White;
        ovenExitTextBox.BorderStyle = BorderStyle.None;
        ovenExitTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        ovenExitTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        ovenExitTextBox.Location = new Point(3, 19);
        ovenExitTextBox.Multiline = true;
        ovenExitTextBox.Name = "ovenExitTextBox";
        ovenExitTextBox.ReadOnly = true;
        ovenExitTextBox.Size = new Size(95, 14);
        ovenExitTextBox.TabIndex = 10;
        ovenExitTextBox.Text = "04/05/2026";
        // 
        // classificationDateTextBox
        // 
        classificationDateTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        classificationDateTextBox.BackColor = Color.White;
        classificationDateTextBox.BorderStyle = BorderStyle.None;
        classificationDateTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        classificationDateTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        classificationDateTextBox.Location = new Point(104, 19);
        classificationDateTextBox.Multiline = true;
        classificationDateTextBox.Name = "classificationDateTextBox";
        classificationDateTextBox.ReadOnly = true;
        classificationDateTextBox.Size = new Size(95, 14);
        classificationDateTextBox.TabIndex = 12;
        classificationDateTextBox.Text = "04/05/2026";
        // 
        // manufacturingDateTextBox
        // 
        manufacturingDateTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        manufacturingDateTextBox.BackColor = Color.White;
        manufacturingDateTextBox.BorderStyle = BorderStyle.None;
        manufacturingDateTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        manufacturingDateTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        manufacturingDateTextBox.Location = new Point(205, 19);
        manufacturingDateTextBox.Multiline = true;
        manufacturingDateTextBox.Name = "manufacturingDateTextBox";
        manufacturingDateTextBox.ReadOnly = true;
        manufacturingDateTextBox.Size = new Size(95, 14);
        manufacturingDateTextBox.TabIndex = 14;
        manufacturingDateTextBox.Text = "29/04/2026";
        // 
        // expirationDateTextBox
        // 
        expirationDateTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        expirationDateTextBox.BackColor = Color.White;
        expirationDateTextBox.BorderStyle = BorderStyle.None;
        expirationDateTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        expirationDateTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        expirationDateTextBox.Location = new Point(306, 19);
        expirationDateTextBox.Multiline = true;
        expirationDateTextBox.Name = "expirationDateTextBox";
        expirationDateTextBox.ReadOnly = true;
        expirationDateTextBox.Size = new Size(98, 14);
        expirationDateTextBox.TabIndex = 16;
        expirationDateTextBox.Text = "28/04/2029";
        // 
        // Gpb_PrevisaoLeitura
        // 
        Gpb_PrevisaoLeitura.BackColor = Color.Transparent;
        Gpb_PrevisaoLeitura.BorderRadius = 7;
        Gpb_PrevisaoLeitura.Controls.Add(plannedProductionTitleIconPictureBox);
        Gpb_PrevisaoLeitura.Controls.Add(plannedProductionTitleLabel);
        Gpb_PrevisaoLeitura.Controls.Add(plannedProductionDividerLabel1);
        Gpb_PrevisaoLeitura.Controls.Add(plannedProductionDividerLabel2);
        Gpb_PrevisaoLeitura.Controls.Add(tableLayoutPanel8);
        Gpb_PrevisaoLeitura.Dock = DockStyle.Fill;
        Gpb_PrevisaoLeitura.Font = new Font("Cascadia Code", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        Gpb_PrevisaoLeitura.ForeColor = Color.FromArgb(75, 85, 99);
        Gpb_PrevisaoLeitura.Location = new Point(449, 3);
        Gpb_PrevisaoLeitura.Margin = new Padding(3, 3, 0, 3);
        Gpb_PrevisaoLeitura.Name = "Gpb_PrevisaoLeitura";
        Gpb_PrevisaoLeitura.Padding = new Padding(17, 35, 16, 5);
        Gpb_PrevisaoLeitura.ShadowBlur = 0;
        Gpb_PrevisaoLeitura.ShadowOffsetY = 0;
        Gpb_PrevisaoLeitura.Size = new Size(666, 81);
        Gpb_PrevisaoLeitura.TabIndex = 25;
        Gpb_PrevisaoLeitura.Visible = false;
        Gpb_PrevisaoLeitura.Resize += AlignPlannedProductionCardLayout;
        // 
        // plannedProductionTitleIconPictureBox
        // 
        plannedProductionTitleIconPictureBox.BackColor = Color.Transparent;
        plannedProductionTitleIconPictureBox.Image = (Image)resources.GetObject("plannedProductionTitleIconPictureBox.Image");
        plannedProductionTitleIconPictureBox.Location = new Point(17, 13);
        plannedProductionTitleIconPictureBox.Name = "plannedProductionTitleIconPictureBox";
        plannedProductionTitleIconPictureBox.Size = new Size(14, 14);
        plannedProductionTitleIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        plannedProductionTitleIconPictureBox.TabIndex = 1;
        plannedProductionTitleIconPictureBox.TabStop = false;
        // 
        // plannedProductionTitleLabel
        // 
        plannedProductionTitleLabel.BackColor = Color.Transparent;
        plannedProductionTitleLabel.Font = new Font("Cascadia Code", 7.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        plannedProductionTitleLabel.ForeColor = Color.FromArgb(31, 41, 55);
        plannedProductionTitleLabel.Location = new Point(34, 12);
        plannedProductionTitleLabel.Name = "plannedProductionTitleLabel";
        plannedProductionTitleLabel.Size = new Size(160, 16);
        plannedProductionTitleLabel.TabIndex = 2;
        plannedProductionTitleLabel.Text = "PRODUÇÃO PLANEJADA";
        plannedProductionTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // plannedProductionDividerLabel1
        // 
        plannedProductionDividerLabel1.BackColor = Color.FromArgb(226, 231, 238);
        plannedProductionDividerLabel1.Location = new Point(226, 48);
        plannedProductionDividerLabel1.Name = "plannedProductionDividerLabel1";
        plannedProductionDividerLabel1.Size = new Size(1, 22);
        plannedProductionDividerLabel1.TabIndex = 3;
        // 
        // plannedProductionDividerLabel2
        // 
        plannedProductionDividerLabel2.BackColor = Color.FromArgb(226, 231, 238);
        plannedProductionDividerLabel2.Location = new Point(446, 48);
        plannedProductionDividerLabel2.Name = "plannedProductionDividerLabel2";
        plannedProductionDividerLabel2.Size = new Size(1, 22);
        plannedProductionDividerLabel2.TabIndex = 4;
        // 
        // tableLayoutPanel8
        // 
        tableLayoutPanel8.BackColor = Color.Transparent;
        tableLayoutPanel8.ColumnCount = 3;
        tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
        tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
        tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3333321F));
        tableLayoutPanel8.Controls.Add(readForecastPackagesCaptionLabel, 1, 0);
        tableLayoutPanel8.Controls.Add(readForecastPackagesTextBox, 1, 1);
        tableLayoutPanel8.Controls.Add(readForecastBoxesCaptionLabel, 0, 0);
        tableLayoutPanel8.Controls.Add(readForecastBoxesTextBox, 0, 1);
        tableLayoutPanel8.Controls.Add(balanceCaptionLabel, 2, 0);
        tableLayoutPanel8.Controls.Add(balanceTextBox, 2, 1);
        tableLayoutPanel8.Dock = DockStyle.Fill;
        tableLayoutPanel8.Location = new Point(17, 35);
        tableLayoutPanel8.Margin = new Padding(0);
        tableLayoutPanel8.Name = "tableLayoutPanel8";
        tableLayoutPanel8.RowCount = 2;
        tableLayoutPanel8.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        tableLayoutPanel8.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel8.Size = new Size(633, 41);
        tableLayoutPanel8.TabIndex = 0;
        // 
        // readForecastPackagesCaptionLabel
        // 
        readForecastPackagesCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        readForecastPackagesCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        readForecastPackagesCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        readForecastPackagesCaptionLabel.Location = new Point(211, 1);
        readForecastPackagesCaptionLabel.Margin = new Padding(0);
        readForecastPackagesCaptionLabel.Name = "readForecastPackagesCaptionLabel";
        readForecastPackagesCaptionLabel.Size = new Size(211, 22);
        readForecastPackagesCaptionLabel.TabIndex = 19;
        readForecastPackagesCaptionLabel.Text = "QTD. PACOTES";
        // 
        // readForecastPackagesTextBox
        // 
        readForecastPackagesTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        readForecastPackagesTextBox.BackColor = Color.White;
        readForecastPackagesTextBox.BorderStyle = BorderStyle.None;
        readForecastPackagesTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        readForecastPackagesTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        readForecastPackagesTextBox.Location = new Point(211, 24);
        readForecastPackagesTextBox.Margin = new Padding(0);
        readForecastPackagesTextBox.Multiline = true;
        readForecastPackagesTextBox.Name = "readForecastPackagesTextBox";
        readForecastPackagesTextBox.ReadOnly = true;
        readForecastPackagesTextBox.Size = new Size(211, 17);
        readForecastPackagesTextBox.TabIndex = 21;
        readForecastPackagesTextBox.Text = "840";
        // 
        // readForecastBoxesCaptionLabel
        // 
        readForecastBoxesCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        readForecastBoxesCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        readForecastBoxesCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        readForecastBoxesCaptionLabel.Location = new Point(0, 1);
        readForecastBoxesCaptionLabel.Margin = new Padding(0);
        readForecastBoxesCaptionLabel.Name = "readForecastBoxesCaptionLabel";
        readForecastBoxesCaptionLabel.Size = new Size(211, 22);
        readForecastBoxesCaptionLabel.TabIndex = 18;
        readForecastBoxesCaptionLabel.Text = "QTD. CAIXAS";
        // 
        // readForecastBoxesTextBox
        // 
        readForecastBoxesTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        readForecastBoxesTextBox.BackColor = Color.White;
        readForecastBoxesTextBox.BorderStyle = BorderStyle.None;
        readForecastBoxesTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        readForecastBoxesTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        readForecastBoxesTextBox.Location = new Point(0, 24);
        readForecastBoxesTextBox.Margin = new Padding(0);
        readForecastBoxesTextBox.Multiline = true;
        readForecastBoxesTextBox.Name = "readForecastBoxesTextBox";
        readForecastBoxesTextBox.ReadOnly = true;
        readForecastBoxesTextBox.Size = new Size(211, 17);
        readForecastBoxesTextBox.TabIndex = 20;
        readForecastBoxesTextBox.Text = "35";
        // 
        // balanceCaptionLabel
        // 
        balanceCaptionLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        balanceCaptionLabel.Font = new Font("Cascadia Code", 6.6F, FontStyle.Bold, GraphicsUnit.Point, 0);
        balanceCaptionLabel.ForeColor = Color.FromArgb(75, 85, 99);
        balanceCaptionLabel.Location = new Point(422, 1);
        balanceCaptionLabel.Margin = new Padding(0);
        balanceCaptionLabel.Name = "balanceCaptionLabel";
        balanceCaptionLabel.Size = new Size(211, 22);
        balanceCaptionLabel.TabIndex = 23;
        balanceCaptionLabel.Text = "DESEMBANDEJAMENTO\r\nSALDO";
        // 
        // balanceTextBox
        // 
        balanceTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        balanceTextBox.BackColor = Color.White;
        balanceTextBox.BorderStyle = BorderStyle.None;
        balanceTextBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        balanceTextBox.ForeColor = Color.FromArgb(184, 18, 32);
        balanceTextBox.Location = new Point(422, 24);
        balanceTextBox.Margin = new Padding(0);
        balanceTextBox.Multiline = true;
        balanceTextBox.Name = "balanceTextBox";
        balanceTextBox.ReadOnly = true;
        balanceTextBox.Size = new Size(211, 17);
        balanceTextBox.TabIndex = 24;
        balanceTextBox.Text = "5,568";
        // 
        // headerPanel
        // 
        headerPanel.BackColor = Color.Transparent;
        headerPanel.BorderRadius = 7;
        headerPanel.Controls.Add(materialTitleIconPictureBox);
        headerPanel.Controls.Add(materialTitleLabel);
        headerPanel.Controls.Add(materialTitleUnderlineLabel);
        headerPanel.Controls.Add(materialFilterIconPictureBox);
        headerPanel.Controls.Add(materialViewAllLabel);
        headerPanel.Controls.Add(materialViewAllChevronLabel);
        headerPanel.Controls.Add(tableLayoutPanel10);
        headerPanel.Dock = DockStyle.Fill;
        headerPanel.Location = new Point(9, 183);
        headerPanel.Name = "headerPanel";
        headerPanel.ShadowBlur = 0;
        headerPanel.ShadowOffsetY = 0;
        headerPanel.Size = new Size(1115, 161);
        headerPanel.TabIndex = 0;
        // 
        // materialTitleIconPictureBox
        // 
        materialTitleIconPictureBox.BackColor = Color.Transparent;
        materialTitleIconPictureBox.Image = (Image)resources.GetObject("materialTitleIconPictureBox.Image");
        materialTitleIconPictureBox.Location = new Point(17, 14);
        materialTitleIconPictureBox.Name = "materialTitleIconPictureBox";
        materialTitleIconPictureBox.Size = new Size(14, 14);
        materialTitleIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        materialTitleIconPictureBox.TabIndex = 3;
        materialTitleIconPictureBox.TabStop = false;
        // 
        // materialTitleLabel
        // 
        materialTitleLabel.BackColor = Color.Transparent;
        materialTitleLabel.Font = new Font("Cascadia Code", 7.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        materialTitleLabel.ForeColor = Color.FromArgb(31, 41, 55);
        materialTitleLabel.Location = new Point(34, 13);
        materialTitleLabel.Name = "materialTitleLabel";
        materialTitleLabel.Size = new Size(150, 16);
        materialTitleLabel.TabIndex = 4;
        materialTitleLabel.Text = "INSUMOS UTILIZADOS";
        materialTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // materialTitleUnderlineLabel
        // 
        materialTitleUnderlineLabel.BackColor = Color.FromArgb(229, 27, 43);
        materialTitleUnderlineLabel.Location = new Point(17, 36);
        materialTitleUnderlineLabel.Name = "materialTitleUnderlineLabel";
        materialTitleUnderlineLabel.Size = new Size(62, 2);
        materialTitleUnderlineLabel.TabIndex = 5;
        // 
        // materialFilterIconPictureBox
        // 
        materialFilterIconPictureBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        materialFilterIconPictureBox.BackColor = Color.Transparent;
        materialFilterIconPictureBox.Image = (Image)resources.GetObject("materialFilterIconPictureBox.Image");
        materialFilterIconPictureBox.Location = new Point(977, 13);
        materialFilterIconPictureBox.Name = "materialFilterIconPictureBox";
        materialFilterIconPictureBox.Size = new Size(18, 18);
        materialFilterIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        materialFilterIconPictureBox.TabIndex = 6;
        materialFilterIconPictureBox.TabStop = false;
        // 
        // materialViewAllLabel
        // 
        materialViewAllLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        materialViewAllLabel.BackColor = Color.Transparent;
        materialViewAllLabel.Font = new Font("Cascadia Code", 6.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        materialViewAllLabel.ForeColor = Color.FromArgb(31, 41, 55);
        materialViewAllLabel.Location = new Point(1010, 13);
        materialViewAllLabel.Name = "materialViewAllLabel";
        materialViewAllLabel.Size = new Size(58, 18);
        materialViewAllLabel.TabIndex = 7;
        materialViewAllLabel.Text = "Ver todos";
        materialViewAllLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // materialViewAllChevronLabel
        // 
        materialViewAllChevronLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        materialViewAllChevronLabel.BackColor = Color.Transparent;
        materialViewAllChevronLabel.Font = new Font("Cascadia Code", 7F, FontStyle.Bold, GraphicsUnit.Point, 0);
        materialViewAllChevronLabel.ForeColor = Color.FromArgb(75, 85, 99);
        materialViewAllChevronLabel.Location = new Point(1070, 13);
        materialViewAllChevronLabel.Name = "materialViewAllChevronLabel";
        materialViewAllChevronLabel.Size = new Size(16, 18);
        materialViewAllChevronLabel.TabIndex = 8;
        materialViewAllChevronLabel.Text = "?";
        materialViewAllChevronLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // tableLayoutPanel10
        // 
        tableLayoutPanel10.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tableLayoutPanel10.ColumnCount = 1;
        tableLayoutPanel10.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel10.Controls.Add(materialDataGridView, 0, 0);
        tableLayoutPanel10.Location = new Point(17, 40);
        tableLayoutPanel10.Name = "tableLayoutPanel10";
        tableLayoutPanel10.RowCount = 1;
        tableLayoutPanel10.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel10.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel10.Size = new Size(1081, 108);
        tableLayoutPanel10.TabIndex = 2;
        // 
        // materialDataGridView
        // 
        materialDataGridView.AllowUserToAddRows = false;
        materialDataGridView.AllowUserToDeleteRows = false;
        materialDataGridView.AllowUserToResizeRows = false;
        materialDataGridView.BackgroundColor = Color.White;
        materialDataGridView.BorderStyle = BorderStyle.None;
        materialDataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dataGridViewCellStyle6.BackColor = Color.FromArgb(245, 247, 250);
        dataGridViewCellStyle6.Font = new Font("Cascadia Code", 6.75F, FontStyle.Bold);
        dataGridViewCellStyle6.ForeColor = Color.FromArgb(31, 41, 55);
        dataGridViewCellStyle6.SelectionBackColor = Color.FromArgb(245, 247, 250);
        dataGridViewCellStyle6.SelectionForeColor = Color.FromArgb(31, 41, 55);
        materialDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle6;
        materialDataGridView.ColumnHeadersHeight = 22;
        materialDataGridView.Columns.AddRange(new DataGridViewColumn[] { materialStatusColumn, materialCodeColumn, materialDescriptionColumn, materialLotColumn, materialExpirationColumn, materialBalanceColumn });
        dataGridViewCellStyle11.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle11.BackColor = Color.FromArgb(250, 251, 252);
        dataGridViewCellStyle11.Font = new Font("Cascadia Code", 6.75F);
        dataGridViewCellStyle11.ForeColor = Color.FromArgb(45, 49, 56);
        dataGridViewCellStyle11.SelectionBackColor = Color.FromArgb(229, 27, 43);
        dataGridViewCellStyle11.SelectionForeColor = Color.White;
        dataGridViewCellStyle11.WrapMode = DataGridViewTriState.False;
        materialDataGridView.DefaultCellStyle = dataGridViewCellStyle11;
        materialDataGridView.Dock = DockStyle.Fill;
        materialDataGridView.EnableHeadersVisualStyles = false;
        materialDataGridView.GridColor = Color.FromArgb(229, 231, 235);
        materialDataGridView.Location = new Point(3, 3);
        materialDataGridView.MultiSelect = false;
        materialDataGridView.Name = "materialDataGridView";
        materialDataGridView.ReadOnly = true;
        materialDataGridView.RowHeadersVisible = false;
        materialDataGridView.RowHeadersWidth = 51;
        materialDataGridView.RowTemplate.Height = 22;
        materialDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        materialDataGridView.Size = new Size(1075, 102);
        materialDataGridView.TabIndex = 1;
        // 
        // materialStatusColumn
        // 
        materialStatusColumn.HeaderText = "";
        materialStatusColumn.MinimumWidth = 6;
        materialStatusColumn.Name = "materialStatusColumn";
        materialStatusColumn.ReadOnly = true;
        materialStatusColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        materialStatusColumn.Width = 28;
        // 
        // materialCodeColumn
        // 
        dataGridViewCellStyle7.Font = new Font("Segoe UI", 6.75F);
        materialCodeColumn.DefaultCellStyle = dataGridViewCellStyle7;
        materialCodeColumn.HeaderText = "CÓDIGO";
        materialCodeColumn.MinimumWidth = 6;
        materialCodeColumn.Name = "materialCodeColumn";
        materialCodeColumn.ReadOnly = true;
        materialCodeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        materialCodeColumn.Width = 82;
        // 
        // materialDescriptionColumn
        // 
        materialDescriptionColumn.HeaderText = "DESCRIÇÃO DO INSUMO";
        materialDescriptionColumn.MinimumWidth = 6;
        materialDescriptionColumn.Name = "materialDescriptionColumn";
        materialDescriptionColumn.ReadOnly = true;
        materialDescriptionColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        materialDescriptionColumn.Width = 365;
        // 
        // materialLotColumn
        // 
        dataGridViewCellStyle8.Font = new Font("Segoe UI", 6.75F);
        materialLotColumn.DefaultCellStyle = dataGridViewCellStyle8;
        materialLotColumn.HeaderText = "LOTE";
        materialLotColumn.MinimumWidth = 6;
        materialLotColumn.Name = "materialLotColumn";
        materialLotColumn.ReadOnly = true;
        materialLotColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        materialLotColumn.Width = 180;
        // 
        // materialExpirationColumn
        // 
        dataGridViewCellStyle9.Font = new Font("Segoe UI", 6.75F);
        materialExpirationColumn.DefaultCellStyle = dataGridViewCellStyle9;
        materialExpirationColumn.HeaderText = "VALIDADE";
        materialExpirationColumn.MinimumWidth = 6;
        materialExpirationColumn.Name = "materialExpirationColumn";
        materialExpirationColumn.ReadOnly = true;
        materialExpirationColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        materialExpirationColumn.Width = 210;
        // 
        // materialBalanceColumn
        // 
        dataGridViewCellStyle10.Font = new Font("Segoe UI", 6.75F);
        materialBalanceColumn.DefaultCellStyle = dataGridViewCellStyle10;
        materialBalanceColumn.HeaderText = "SALDO PROD.";
        materialBalanceColumn.MinimumWidth = 6;
        materialBalanceColumn.Name = "materialBalanceColumn";
        materialBalanceColumn.ReadOnly = true;
        materialBalanceColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
        materialBalanceColumn.Width = 210;
        // 
        // productionSearchIconPictureBox
        // 
        productionSearchIconPictureBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionSearchIconPictureBox.BackColor = Color.FromArgb(248, 250, 252);
        productionSearchIconPictureBox.Image = (Image)resources.GetObject("productionSearchIconPictureBox.Image");
        productionSearchIconPictureBox.Location = new Point(870, 13);
        productionSearchIconPictureBox.Name = "productionSearchIconPictureBox";
        productionSearchIconPictureBox.Size = new Size(16, 16);
        productionSearchIconPictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
        productionSearchIconPictureBox.TabIndex = 4;
        productionSearchIconPictureBox.TabStop = false;
        productionSearchIconPictureBox.Visible = false;
        // 
        // tableLayoutPanel7
        // 
        tableLayoutPanel7.ColumnCount = 1;
        tableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel7.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel7.Controls.Add(label1, 0, 1);
        tableLayoutPanel7.Controls.Add(lotSearchIconLabel, 0, 0);
        tableLayoutPanel7.Dock = DockStyle.Fill;
        tableLayoutPanel7.Location = new Point(1115, 3);
        tableLayoutPanel7.Name = "tableLayoutPanel7";
        tableLayoutPanel7.RowCount = 2;
        tableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel7.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel7.Size = new Size(55, 155);
        tableLayoutPanel7.TabIndex = 2;
        // 
        // label1
        // 
        label1.Anchor = AnchorStyles.Top;
        label1.AutoSize = true;
        label1.Font = new Font("Cascadia Code", 9F, FontStyle.Bold);
        label1.ForeColor = Color.FromArgb(75, 85, 99);
        label1.Location = new Point(10, 77);
        label1.Name = "label1";
        label1.Size = new Size(35, 16);
        label1.TabIndex = 0;
        label1.Text = "Lote";
        label1.TextAlign = ContentAlignment.TopCenter;
        // 
        // lotSearchIconLabel
        // 
        lotSearchIconLabel.Anchor = AnchorStyles.None;
        lotSearchIconLabel.BackColor = Color.Transparent;
        lotSearchIconLabel.Image = (Image)resources.GetObject("lotSearchIconLabel.Image");
        lotSearchIconLabel.Location = new Point(6, 19);
        lotSearchIconLabel.Name = "lotSearchIconLabel";
        lotSearchIconLabel.Size = new Size(43, 38);
        lotSearchIconLabel.SizeMode = PictureBoxSizeMode.CenterImage;
        lotSearchIconLabel.TabIndex = 1;
        lotSearchIconLabel.TabStop = false;
        // 
        // statusLabel
        // 
        statusLabel.BackColor = Color.Transparent;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        statusLabel.ForeColor = Color.FromArgb(34, 166, 82);
        statusLabel.Location = new Point(12, 0);
        statusLabel.Margin = new Padding(0);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(478, 38);
        statusLabel.TabIndex = 0;
        statusLabel.Text = "?  Leitura OK !";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // footerBar
        // 
        footerBar.BackColor = Color.FromArgb(248, 250, 253);
        tableLayoutPanel2.SetColumnSpan(footerBar, 2);
        footerBar.Controls.Add(footerBarLayout);
        footerBar.Dock = DockStyle.Fill;
        footerBar.Location = new Point(0, 682);
        footerBar.Margin = new Padding(0);
        footerBar.Name = "footerBar";
        footerBar.Size = new Size(1366, 38);
        footerBar.TabIndex = 2;
        // 
        // footerBarLayout
        // 
        footerBarLayout.BackColor = Color.Transparent;
        footerBarLayout.ColumnCount = 6;
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 8F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        footerBarLayout.Controls.Add(cellUser, 0, 0);
        footerBarLayout.Controls.Add(cellTerminal, 1, 0);
        footerBarLayout.Controls.Add(cellEmpresa, 2, 0);
        footerBarLayout.Controls.Add(cellBanco, 3, 0);
        footerBarLayout.Controls.Add(cellHora, 4, 0);
        footerBarLayout.Controls.Add(cellData, 5, 0);
        footerBarLayout.Dock = DockStyle.Fill;
        footerBarLayout.Location = new Point(0, 0);
        footerBarLayout.Margin = new Padding(0);
        footerBarLayout.Name = "footerBarLayout";
        footerBarLayout.RowCount = 1;
        footerBarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footerBarLayout.Size = new Size(1366, 38);
        footerBarLayout.TabIndex = 0;
        // 
        // cellUser
        // 
        cellUser.BackColor = Color.Transparent;
        cellUser.Controls.Add(cellUserText);
        cellUser.Controls.Add(cellUserIcon);
        cellUser.Controls.Add(cellUserDivider);
        cellUser.Dock = DockStyle.Fill;
        cellUser.Location = new Point(0, 0);
        cellUser.Margin = new Padding(0);
        cellUser.Name = "cellUser";
        cellUser.Size = new Size(218, 38);
        cellUser.TabIndex = 1;
        // 
        // cellUserText
        // 
        cellUserText.Dock = DockStyle.Fill;
        cellUserText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellUserText.ForeColor = Color.FromArgb(98, 108, 124);
        cellUserText.Location = new Point(28, 0);
        cellUserText.Margin = new Padding(0);
        cellUserText.Name = "cellUserText";
        cellUserText.Padding = new Padding(2, 0, 0, 0);
        cellUserText.Size = new Size(189, 38);
        cellUserText.TabIndex = 0;
        cellUserText.Text = "Usuário:  OPERADOR01";
        cellUserText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellUserIcon
        // 
        cellUserIcon.Dock = DockStyle.Left;
        cellUserIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellUserIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellUserIcon.Location = new Point(0, 0);
        cellUserIcon.Margin = new Padding(0);
        cellUserIcon.Name = "cellUserIcon";
        cellUserIcon.Size = new Size(28, 38);
        cellUserIcon.TabIndex = 1;
        cellUserIcon.Text = "";
        cellUserIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellUserDivider
        // 
        cellUserDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellUserDivider.Dock = DockStyle.Right;
        cellUserDivider.Location = new Point(217, 0);
        cellUserDivider.Margin = new Padding(0);
        cellUserDivider.Name = "cellUserDivider";
        cellUserDivider.Size = new Size(1, 38);
        cellUserDivider.TabIndex = 2;
        // 
        // cellTerminal
        // 
        cellTerminal.BackColor = Color.Transparent;
        cellTerminal.Controls.Add(cellTerminalText);
        cellTerminal.Controls.Add(cellTerminalIcon);
        cellTerminal.Controls.Add(cellTerminalDivider);
        cellTerminal.Dock = DockStyle.Fill;
        cellTerminal.Location = new Point(218, 0);
        cellTerminal.Margin = new Padding(0);
        cellTerminal.Name = "cellTerminal";
        cellTerminal.Size = new Size(204, 38);
        cellTerminal.TabIndex = 2;
        // 
        // cellTerminalText
        // 
        cellTerminalText.Dock = DockStyle.Fill;
        cellTerminalText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellTerminalText.ForeColor = Color.FromArgb(98, 108, 124);
        cellTerminalText.Location = new Point(28, 0);
        cellTerminalText.Margin = new Padding(0);
        cellTerminalText.Name = "cellTerminalText";
        cellTerminalText.Padding = new Padding(2, 0, 0, 0);
        cellTerminalText.Size = new Size(175, 38);
        cellTerminalText.TabIndex = 0;
        cellTerminalText.Text = "Terminal:  TERM-03";
        cellTerminalText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellTerminalIcon
        // 
        cellTerminalIcon.Dock = DockStyle.Left;
        cellTerminalIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellTerminalIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellTerminalIcon.Location = new Point(0, 0);
        cellTerminalIcon.Margin = new Padding(0);
        cellTerminalIcon.Name = "cellTerminalIcon";
        cellTerminalIcon.Size = new Size(28, 38);
        cellTerminalIcon.TabIndex = 1;
        cellTerminalIcon.Text = "";
        cellTerminalIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellTerminalDivider
        // 
        cellTerminalDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellTerminalDivider.Dock = DockStyle.Right;
        cellTerminalDivider.Location = new Point(203, 0);
        cellTerminalDivider.Margin = new Padding(0);
        cellTerminalDivider.Name = "cellTerminalDivider";
        cellTerminalDivider.Size = new Size(1, 38);
        cellTerminalDivider.TabIndex = 2;
        // 
        // cellEmpresa
        // 
        cellEmpresa.BackColor = Color.Transparent;
        cellEmpresa.Controls.Add(cellEmpresaText);
        cellEmpresa.Controls.Add(cellEmpresaIcon);
        cellEmpresa.Controls.Add(cellEmpresaDivider);
        cellEmpresa.Dock = DockStyle.Fill;
        cellEmpresa.Location = new Point(422, 0);
        cellEmpresa.Margin = new Padding(0);
        cellEmpresa.Name = "cellEmpresa";
        cellEmpresa.Size = new Size(368, 38);
        cellEmpresa.TabIndex = 3;
        // 
        // cellEmpresaText
        // 
        cellEmpresaText.Dock = DockStyle.Fill;
        cellEmpresaText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellEmpresaText.ForeColor = Color.FromArgb(98, 108, 124);
        cellEmpresaText.Location = new Point(28, 0);
        cellEmpresaText.Margin = new Padding(0);
        cellEmpresaText.Name = "cellEmpresaText";
        cellEmpresaText.Padding = new Padding(2, 0, 0, 0);
        cellEmpresaText.Size = new Size(339, 38);
        cellEmpresaText.TabIndex = 0;
        cellEmpresaText.Text = "Empresa:  FUGA COUROS S.A.";
        cellEmpresaText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellEmpresaIcon
        // 
        cellEmpresaIcon.Dock = DockStyle.Left;
        cellEmpresaIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellEmpresaIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellEmpresaIcon.Location = new Point(0, 0);
        cellEmpresaIcon.Margin = new Padding(0);
        cellEmpresaIcon.Name = "cellEmpresaIcon";
        cellEmpresaIcon.Size = new Size(28, 38);
        cellEmpresaIcon.TabIndex = 1;
        cellEmpresaIcon.Text = "";
        cellEmpresaIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellEmpresaDivider
        // 
        cellEmpresaDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellEmpresaDivider.Dock = DockStyle.Right;
        cellEmpresaDivider.Location = new Point(367, 0);
        cellEmpresaDivider.Margin = new Padding(0);
        cellEmpresaDivider.Name = "cellEmpresaDivider";
        cellEmpresaDivider.Size = new Size(1, 38);
        cellEmpresaDivider.TabIndex = 2;
        // 
        // cellBanco
        // 
        cellBanco.BackColor = Color.Transparent;
        cellBanco.Controls.Add(cellBancoText);
        cellBanco.Controls.Add(cellBancoIcon);
        cellBanco.Controls.Add(cellBancoDivider);
        cellBanco.Dock = DockStyle.Fill;
        cellBanco.Location = new Point(790, 0);
        cellBanco.Margin = new Padding(0);
        cellBanco.Name = "cellBanco";
        cellBanco.Size = new Size(327, 38);
        cellBanco.TabIndex = 4;
        // 
        // cellBancoText
        // 
        cellBancoText.Dock = DockStyle.Fill;
        cellBancoText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellBancoText.ForeColor = Color.FromArgb(98, 108, 124);
        cellBancoText.Location = new Point(28, 0);
        cellBancoText.Margin = new Padding(0);
        cellBancoText.Name = "cellBancoText";
        cellBancoText.Padding = new Padding(2, 0, 0, 0);
        cellBancoText.Size = new Size(298, 38);
        cellBancoText.TabIndex = 0;
        cellBancoText.Text = "Banco de Dados:  PRODCI01";
        cellBancoText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellBancoIcon
        // 
        cellBancoIcon.Dock = DockStyle.Left;
        cellBancoIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellBancoIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellBancoIcon.Location = new Point(0, 0);
        cellBancoIcon.Margin = new Padding(0);
        cellBancoIcon.Name = "cellBancoIcon";
        cellBancoIcon.Size = new Size(28, 38);
        cellBancoIcon.TabIndex = 1;
        cellBancoIcon.Text = "";
        cellBancoIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellBancoDivider
        // 
        cellBancoDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellBancoDivider.Dock = DockStyle.Right;
        cellBancoDivider.Location = new Point(326, 0);
        cellBancoDivider.Margin = new Padding(0);
        cellBancoDivider.Name = "cellBancoDivider";
        cellBancoDivider.Size = new Size(1, 38);
        cellBancoDivider.TabIndex = 2;
        // 
        // cellHora
        // 
        cellHora.BackColor = Color.Transparent;
        cellHora.Controls.Add(cellHoraText);
        cellHora.Controls.Add(cellHoraIcon);
        cellHora.Controls.Add(cellHoraDivider);
        cellHora.Dock = DockStyle.Fill;
        cellHora.Location = new Point(1117, 0);
        cellHora.Margin = new Padding(0);
        cellHora.Name = "cellHora";
        cellHora.Size = new Size(109, 38);
        cellHora.TabIndex = 5;
        // 
        // cellHoraText
        // 
        cellHoraText.Dock = DockStyle.Fill;
        cellHoraText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellHoraText.ForeColor = Color.FromArgb(98, 108, 124);
        cellHoraText.Location = new Point(24, 0);
        cellHoraText.Margin = new Padding(0);
        cellHoraText.Name = "cellHoraText";
        cellHoraText.Padding = new Padding(2, 0, 0, 0);
        cellHoraText.Size = new Size(84, 38);
        cellHoraText.TabIndex = 0;
        cellHoraText.Text = "13:37";
        cellHoraText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellHoraIcon
        // 
        cellHoraIcon.Dock = DockStyle.Left;
        cellHoraIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellHoraIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellHoraIcon.Location = new Point(0, 0);
        cellHoraIcon.Margin = new Padding(0);
        cellHoraIcon.Name = "cellHoraIcon";
        cellHoraIcon.Size = new Size(24, 38);
        cellHoraIcon.TabIndex = 1;
        cellHoraIcon.Text = "";
        cellHoraIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellHoraDivider
        // 
        cellHoraDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellHoraDivider.Dock = DockStyle.Right;
        cellHoraDivider.Location = new Point(108, 0);
        cellHoraDivider.Margin = new Padding(0);
        cellHoraDivider.Name = "cellHoraDivider";
        cellHoraDivider.Size = new Size(1, 38);
        cellHoraDivider.TabIndex = 2;
        // 
        // cellData
        // 
        cellData.BackColor = Color.Transparent;
        cellData.Controls.Add(cellDataText);
        cellData.Controls.Add(cellDataIcon);
        cellData.Dock = DockStyle.Fill;
        cellData.Location = new Point(1226, 0);
        cellData.Margin = new Padding(0);
        cellData.Name = "cellData";
        cellData.Size = new Size(140, 38);
        cellData.TabIndex = 6;
        // 
        // cellDataText
        // 
        cellDataText.Dock = DockStyle.Fill;
        cellDataText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellDataText.ForeColor = Color.FromArgb(98, 108, 124);
        cellDataText.Location = new Point(24, 0);
        cellDataText.Margin = new Padding(0);
        cellDataText.Name = "cellDataText";
        cellDataText.Padding = new Padding(2, 0, 0, 0);
        cellDataText.Size = new Size(116, 38);
        cellDataText.TabIndex = 0;
        cellDataText.Text = "04/05/2026";
        cellDataText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellDataIcon
        // 
        cellDataIcon.Dock = DockStyle.Left;
        cellDataIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellDataIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellDataIcon.Location = new Point(0, 0);
        cellDataIcon.Margin = new Padding(0);
        cellDataIcon.Name = "cellDataIcon";
        cellDataIcon.Size = new Size(24, 38);
        cellDataIcon.TabIndex = 1;
        cellDataIcon.Text = "";
        cellDataIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // statusCell
        // 
        statusCell.BackColor = Color.Transparent;
        statusCell.Controls.Add(statusLabel);
        statusCell.Controls.Add(statusCellDivider);
        statusCell.Dock = DockStyle.Fill;
        statusCell.Location = new Point(0, 0);
        statusCell.Margin = new Padding(0);
        statusCell.Name = "statusCell";
        statusCell.Padding = new Padding(12, 0, 0, 0);
        statusCell.Size = new Size(491, 38);
        statusCell.TabIndex = 0;
        // 
        // statusCellDivider
        // 
        statusCellDivider.BackColor = Color.FromArgb(214, 219, 226);
        statusCellDivider.Dock = DockStyle.Right;
        statusCellDivider.Location = new Point(490, 0);
        statusCellDivider.Margin = new Padding(0);
        statusCellDivider.Name = "statusCellDivider";
        statusCellDivider.Size = new Size(1, 38);
        statusCellDivider.TabIndex = 1;
        // 
        // groupBox1
        // 
        groupBox1.Anchor = AnchorStyles.Left;
        groupBox1.Controls.Add(tableLayoutPanel9);
        groupBox1.Font = new Font("Cascadia Code", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        groupBox1.ForeColor = Color.FromArgb(75, 85, 99);
        groupBox1.Location = new Point(658, 3);
        groupBox1.Name = "groupBox1";
        groupBox1.Size = new Size(157, 81);
        groupBox1.TabIndex = 26;
        groupBox1.TabStop = false;
        groupBox1.Text = "Desembandejamento";
        // 
        // tableLayoutPanel9
        // 
        tableLayoutPanel9.ColumnCount = 1;
        tableLayoutPanel9.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel9.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        tableLayoutPanel9.Dock = DockStyle.Fill;
        tableLayoutPanel9.Location = new Point(3, 17);
        tableLayoutPanel9.Name = "tableLayoutPanel9";
        tableLayoutPanel9.RowCount = 2;
        tableLayoutPanel9.RowStyles.Add(new RowStyle(SizeType.Percent, 29.62963F));
        tableLayoutPanel9.RowStyles.Add(new RowStyle(SizeType.Percent, 70.37037F));
        tableLayoutPanel9.Size = new Size(151, 61);
        tableLayoutPanel9.TabIndex = 0;
        // 
        // customTitleBarPanel
        // 
        customTitleBarPanel.BackColor = Color.FromArgb(24, 31, 43);
        tableLayoutPanel2.SetColumnSpan(customTitleBarPanel, 2);
        customTitleBarPanel.Controls.Add(logoSaLabel);
        customTitleBarPanel.Controls.Add(menuHeaderLabel);
        customTitleBarPanel.Controls.Add(companyLogoPictureBox);
        customTitleBarPanel.Controls.Add(headerDividerLabel);
        customTitleBarPanel.Controls.Add(headerTitleIconPanel);
        customTitleBarPanel.Controls.Add(headerTitleLabel);
        customTitleBarPanel.Controls.Add(sapStatusPanel);
        customTitleBarPanel.Controls.Add(minimizeWindowLabel);
        customTitleBarPanel.Controls.Add(maximizeWindowLabel);
        customTitleBarPanel.Controls.Add(closeWindowLabel);
        customTitleBarPanel.Controls.Add(headerSubtitleLabel);
        customTitleBarPanel.Dock = DockStyle.Fill;
        customTitleBarPanel.Location = new Point(0, 0);
        customTitleBarPanel.Margin = new Padding(0);
        customTitleBarPanel.Name = "customTitleBarPanel";
        customTitleBarPanel.Size = new Size(1366, 52);
        customTitleBarPanel.TabIndex = 2;
        // 
        // logoSaLabel
        // 
        logoSaLabel.BackColor = Color.Transparent;
        logoSaLabel.Font = new Font("Cascadia Code", 3F, FontStyle.Bold, GraphicsUnit.Point, 0);
        logoSaLabel.ForeColor = Color.White;
        logoSaLabel.Location = new Point(171, 12);
        logoSaLabel.Name = "logoSaLabel";
        logoSaLabel.Size = new Size(21, 10);
        logoSaLabel.TabIndex = 13;
        logoSaLabel.Text = "S/A";
        logoSaLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuHeaderLabel
        // 
        menuHeaderLabel.BackColor = Color.Transparent;
        menuHeaderLabel.Cursor = Cursors.Hand;
        menuHeaderLabel.Font = new Font("Segoe MDL2 Assets", 13F, FontStyle.Regular, GraphicsUnit.Point, 0);
        menuHeaderLabel.ForeColor = Color.FromArgb(229, 231, 235);
        menuHeaderLabel.Location = new Point(15, 8);
        menuHeaderLabel.Name = "menuHeaderLabel";
        menuHeaderLabel.Size = new Size(40, 36);
        menuHeaderLabel.TabIndex = 4;
        menuHeaderLabel.Text = "";
        menuHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // companyLogoPictureBox
        // 
        companyLogoPictureBox.BackColor = Color.Transparent;
        companyLogoPictureBox.Image = (Image)resources.GetObject("companyLogoPictureBox.Image");
        companyLogoPictureBox.Location = new Point(60, 5);
        companyLogoPictureBox.Name = "companyLogoPictureBox";
        companyLogoPictureBox.Size = new Size(128, 43);
        companyLogoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        companyLogoPictureBox.TabIndex = 1;
        companyLogoPictureBox.TabStop = false;
        // 
        // headerDividerLabel
        // 
        headerDividerLabel.BackColor = Color.FromArgb(132, 142, 156);
        headerDividerLabel.Location = new Point(208, 12);
        headerDividerLabel.Name = "headerDividerLabel";
        headerDividerLabel.Size = new Size(1, 30);
        headerDividerLabel.TabIndex = 5;
        // 
        // headerTitleIconPanel
        // 
        headerTitleIconPanel.BackColor = Color.Transparent;
        headerTitleIconPanel.Controls.Add(headerTitleIconPictureBox);
        headerTitleIconPanel.FillColor = Color.Transparent;
        headerTitleIconPanel.Location = new Point(239, 9);
        headerTitleIconPanel.Name = "headerTitleIconPanel";
        headerTitleIconPanel.ShadowBlur = 0;
        headerTitleIconPanel.ShadowOffsetY = 0;
        headerTitleIconPanel.Size = new Size(29, 29);
        headerTitleIconPanel.TabIndex = 6;
        // 
        // headerTitleIconPictureBox
        // 
        headerTitleIconPictureBox.BackColor = Color.Transparent;
        headerTitleIconPictureBox.Dock = DockStyle.Fill;
        headerTitleIconPictureBox.Image = (Image)resources.GetObject("headerTitleIconPictureBox.Image");
        headerTitleIconPictureBox.Location = new Point(0, 0);
        headerTitleIconPictureBox.Name = "headerTitleIconPictureBox";
        headerTitleIconPictureBox.Size = new Size(29, 29);
        headerTitleIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerTitleIconPictureBox.TabIndex = 0;
        headerTitleIconPictureBox.TabStop = false;
        // 
        // headerTitleLabel
        // 
        headerTitleLabel.BackColor = Color.Transparent;
        headerTitleLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(278, 6);
        headerTitleLabel.Name = "headerTitleLabel";
        headerTitleLabel.Size = new Size(260, 23);
        headerTitleLabel.TabIndex = 7;
        headerTitleLabel.Text = "Produto Semi-Acabado";
        headerTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // sapStatusPanel
        // 
        sapStatusPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        sapStatusPanel.BackColor = Color.Transparent;
        sapStatusPanel.BorderColor = Color.FromArgb(58, 68, 83);
        sapStatusPanel.BorderRadius = 12;
        sapStatusPanel.Controls.Add(sapStatusDotLabel);
        sapStatusPanel.Controls.Add(sapStatusLabel);
        sapStatusPanel.FillColor = Color.FromArgb(24, 31, 43);
        sapStatusPanel.Location = new Point(748, 10);
        sapStatusPanel.Name = "sapStatusPanel";
        sapStatusPanel.ShadowBlur = 0;
        sapStatusPanel.ShadowOffsetY = 0;
        sapStatusPanel.Size = new Size(452, 27);
        sapStatusPanel.TabIndex = 9;
        // 
        // sapStatusDotLabel
        // 
        sapStatusDotLabel.BackColor = Color.Transparent;
        sapStatusDotLabel.Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusDotLabel.ForeColor = Color.FromArgb(250, 204, 21);
        sapStatusDotLabel.Location = new Point(11, 4);
        sapStatusDotLabel.Name = "sapStatusDotLabel";
        sapStatusDotLabel.Size = new Size(14, 18);
        sapStatusDotLabel.TabIndex = 0;
        sapStatusDotLabel.Text = "●";
        sapStatusDotLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // sapStatusLabel
        // 
        sapStatusLabel.BackColor = Color.Transparent;
        sapStatusLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusLabel.ForeColor = Color.White;
        sapStatusLabel.Location = new Point(27, 5);
        sapStatusLabel.Name = "sapStatusLabel";
        sapStatusLabel.Size = new Size(411, 17);
        sapStatusLabel.TabIndex = 1;
        sapStatusLabel.Text = "SAP: não configurado";
        sapStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // minimizeWindowLabel
        // 
        minimizeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        minimizeWindowLabel.BackColor = Color.Transparent;
        minimizeWindowLabel.Font = new Font("Cascadia Code", 12.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        minimizeWindowLabel.ForeColor = Color.White;
        minimizeWindowLabel.Location = new Point(1218, 0);
        minimizeWindowLabel.Name = "minimizeWindowLabel";
        minimizeWindowLabel.Size = new Size(48, 52);
        minimizeWindowLabel.TabIndex = 10;
        minimizeWindowLabel.Text = "-";
        minimizeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // maximizeWindowLabel
        // 
        maximizeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        maximizeWindowLabel.BackColor = Color.Transparent;
        maximizeWindowLabel.Font = new Font("Cascadia Code", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        maximizeWindowLabel.ForeColor = Color.White;
        maximizeWindowLabel.Location = new Point(1266, 0);
        maximizeWindowLabel.Name = "maximizeWindowLabel";
        maximizeWindowLabel.Size = new Size(48, 52);
        maximizeWindowLabel.TabIndex = 11;
        maximizeWindowLabel.Text = "□";
        maximizeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // closeWindowLabel
        // 
        closeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeWindowLabel.BackColor = Color.Transparent;
        closeWindowLabel.Font = new Font("Cascadia Code ExtraLight", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        closeWindowLabel.ForeColor = Color.White;
        closeWindowLabel.Location = new Point(1314, 0);
        closeWindowLabel.Name = "closeWindowLabel";
        closeWindowLabel.Size = new Size(48, 52);
        closeWindowLabel.TabIndex = 12;
        closeWindowLabel.Text = "×";
        closeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // headerSubtitleLabel
        // 
        headerSubtitleLabel.BackColor = Color.Transparent;
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228);
        headerSubtitleLabel.Location = new Point(279, 29);
        headerSubtitleLabel.Name = "headerSubtitleLabel";
        headerSubtitleLabel.Size = new Size(420, 18);
        headerSubtitleLabel.TabIndex = 8;
        headerSubtitleLabel.Text = "Pesagem e entrada de produto semi-acabado por ordem de produção";
        headerSubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // sidePanel
        // 
        sidePanel.BackColor = Color.Transparent;
        sidePanel.BorderRadius = 7;
        sidePanel.Controls.Add(sideStatusTitleLabel);
        sidePanel.Controls.Add(statusCard);
        sidePanel.Controls.Add(groupBox4);
        sidePanel.Controls.Add(iniciarLeituraButton);
        sidePanel.Controls.Add(lerEtiquetaButton);
        sidePanel.Controls.Add(leituraManualButton);
        sidePanel.Dock = DockStyle.Fill;
        sidePanel.Location = new Point(1142, 66);
        sidePanel.Margin = new Padding(3, 14, 9, 12);
        sidePanel.Name = "sidePanel";
        sidePanel.ShadowBlur = 0;
        sidePanel.ShadowOffsetY = 0;
        sidePanel.Size = new Size(215, 604);
        sidePanel.TabIndex = 1;
        // 
        // sideStatusTitleLabel
        // 
        sideStatusTitleLabel.BackColor = Color.Transparent;
        sideStatusTitleLabel.Font = new Font("Cascadia Code", 7.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sideStatusTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        sideStatusTitleLabel.Location = new Point(16, 16);
        sideStatusTitleLabel.Name = "sideStatusTitleLabel";
        sideStatusTitleLabel.Size = new Size(160, 16);
        sideStatusTitleLabel.TabIndex = 0;
        sideStatusTitleLabel.Text = "STATUS DA LEITURA";
        // 
        // statusCard
        // 
        statusCard.BackColor = Color.Transparent;
        statusCard.BorderColor = Color.FromArgb(248, 190, 190);
        statusCard.BorderRadius = 7;
        statusCard.Controls.Add(statusCardIcon);
        statusCard.Controls.Add(statusValueLabel);
        statusCard.Controls.Add(statusHintLabel);
        statusCard.FillColor = Color.FromArgb(254, 232, 232);
        statusCard.Location = new Point(16, 38);
        statusCard.Name = "statusCard";
        statusCard.ShadowBlur = 0;
        statusCard.ShadowOffsetY = 0;
        statusCard.Size = new Size(183, 80);
        statusCard.TabIndex = 1;
        // 
        // statusCardIcon
        // 
        statusCardIcon.BackColor = Color.Transparent;
        statusCardIcon.Font = new Font("Segoe UI Symbol", 13F, FontStyle.Bold, GraphicsUnit.Point, 0);
        statusCardIcon.ForeColor = Color.FromArgb(220, 53, 69);
        statusCardIcon.Location = new Point(12, 17);
        statusCardIcon.Name = "statusCardIcon";
        statusCardIcon.Size = new Size(20, 22);
        statusCardIcon.TabIndex = 0;
        statusCardIcon.Text = "!";
        statusCardIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // statusValueLabel
        // 
        statusValueLabel.BackColor = Color.Transparent;
        statusValueLabel.Font = new Font("Cascadia Code", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
        statusValueLabel.ForeColor = Color.FromArgb(220, 53, 69);
        statusValueLabel.Location = new Point(38, 17);
        statusValueLabel.Name = "statusValueLabel";
        statusValueLabel.Size = new Size(120, 24);
        statusValueLabel.TabIndex = 1;
        statusValueLabel.Text = "INATIVA";
        statusValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // statusHintLabel
        // 
        statusHintLabel.BackColor = Color.Transparent;
        statusHintLabel.Font = new Font("Cascadia Code", 6.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        statusHintLabel.ForeColor = Color.FromArgb(98, 108, 124);
        statusHintLabel.Location = new Point(13, 52);
        statusHintLabel.Name = "statusHintLabel";
        statusHintLabel.Size = new Size(145, 22);
        statusHintLabel.TabIndex = 2;
        statusHintLabel.Text = "Leitura aguardando início";
        // 
        // groupBox4
        // 
        groupBox4.BackColor = Color.Transparent;
        groupBox4.BorderRadius = 10;
        groupBox4.Controls.Add(tableLayoutPanel12);
        groupBox4.Controls.Add(weightSummaryAccentBar);
        groupBox4.Controls.Add(weightSummaryTitleLabel);
        groupBox4.Controls.Add(weightSummarySubtitleLabel);
        groupBox4.Controls.Add(weightSummaryDividerLabel);
        groupBox4.FillColor = Color.FromArgb(255, 255, 255);
        groupBox4.ForeColor = Color.FromArgb(229, 231, 235);
        groupBox4.Location = new Point(16, 130);
        groupBox4.Name = "groupBox4";
        groupBox4.ShadowBlur = 0;
        groupBox4.ShadowOffsetY = 0;
        groupBox4.Size = new Size(183, 206);
        groupBox4.TabIndex = 14;
        // 
        // tableLayoutPanel12
        // 
        tableLayoutPanel12.ColumnCount = 1;
        tableLayoutPanel12.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel12.Controls.Add(weightSummaryForecastPanel, 0, 0);
        tableLayoutPanel12.Controls.Add(weightSummaryUsedPanel, 0, 1);
        tableLayoutPanel12.Dock = DockStyle.Fill;
        tableLayoutPanel12.Location = new Point(0, 0);
        tableLayoutPanel12.Name = "tableLayoutPanel12";
        tableLayoutPanel12.RowCount = 2;
        tableLayoutPanel12.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel12.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        tableLayoutPanel12.Size = new Size(183, 206);
        tableLayoutPanel12.TabIndex = 9;
        // 
        // weightSummaryForecastPanel
        // 
        weightSummaryForecastPanel.BackColor = Color.Transparent;
        weightSummaryForecastPanel.Controls.Add(boxesCaptionLabel);
        weightSummaryForecastPanel.Controls.Add(boxesCounterLabel);
        weightSummaryForecastPanel.Dock = DockStyle.Fill;
        weightSummaryForecastPanel.Location = new Point(0, 0);
        weightSummaryForecastPanel.Margin = new Padding(0);
        weightSummaryForecastPanel.Name = "weightSummaryForecastPanel";
        weightSummaryForecastPanel.Size = new Size(183, 103);
        weightSummaryForecastPanel.TabIndex = 10;
        // 
        // boxesCaptionLabel
        // 
        boxesCaptionLabel.Font = new Font("Cascadia Code", 7.5F, FontStyle.Bold);
        boxesCaptionLabel.ForeColor = Color.FromArgb(17, 24, 39);
        boxesCaptionLabel.Location = new Point(3, 0);
        boxesCaptionLabel.Name = "boxesCaptionLabel";
        boxesCaptionLabel.Size = new Size(177, 16);
        boxesCaptionLabel.TabIndex = 5;
        boxesCaptionLabel.Text = "Saldo OP";
        boxesCaptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // boxesCounterLabel
        // 
        boxesCounterLabel.BackColor = Color.FromArgb(250, 251, 252);
        boxesCounterLabel.Font = new Font("Segoe UI", 15.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        boxesCounterLabel.ForeColor = Color.FromArgb(184, 18, 32);
        boxesCounterLabel.Location = new Point(3, 16);
        boxesCounterLabel.Name = "boxesCounterLabel";
        boxesCounterLabel.Size = new Size(177, 28);
        boxesCounterLabel.TabIndex = 6;
        boxesCounterLabel.Text = "000";
        boxesCounterLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // weightSummaryUsedPanel
        // 
        weightSummaryUsedPanel.BackColor = Color.Transparent;
        weightSummaryUsedPanel.Controls.Add(packagesCaptionLabel);
        weightSummaryUsedPanel.Controls.Add(packagesCounterLabel);
        weightSummaryUsedPanel.Dock = DockStyle.Fill;
        weightSummaryUsedPanel.Location = new Point(0, 103);
        weightSummaryUsedPanel.Margin = new Padding(0);
        weightSummaryUsedPanel.Name = "weightSummaryUsedPanel";
        weightSummaryUsedPanel.Size = new Size(183, 103);
        weightSummaryUsedPanel.TabIndex = 11;
        // 
        // packagesCaptionLabel
        // 
        packagesCaptionLabel.Font = new Font("Cascadia Code", 7.5F, FontStyle.Bold);
        packagesCaptionLabel.ForeColor = Color.FromArgb(17, 24, 39);
        packagesCaptionLabel.Location = new Point(3, 0);
        packagesCaptionLabel.Name = "packagesCaptionLabel";
        packagesCaptionLabel.Size = new Size(177, 16);
        packagesCaptionLabel.TabIndex = 7;
        packagesCaptionLabel.Text = "Peso Utilizado";
        packagesCaptionLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // packagesCounterLabel
        // 
        packagesCounterLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        packagesCounterLabel.BackColor = Color.FromArgb(250, 251, 252);
        packagesCounterLabel.Font = new Font("Segoe UI", 15.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        packagesCounterLabel.ForeColor = Color.FromArgb(184, 18, 32);
        packagesCounterLabel.Location = new Point(3, 16);
        packagesCounterLabel.Name = "packagesCounterLabel";
        packagesCounterLabel.Size = new Size(177, 28);
        packagesCounterLabel.TabIndex = 8;
        packagesCounterLabel.Text = "000";
        packagesCounterLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // weightSummaryAccentBar
        // 
        weightSummaryAccentBar.BackColor = Color.FromArgb(229, 27, 43);
        weightSummaryAccentBar.Location = new Point(14, 14);
        weightSummaryAccentBar.Name = "weightSummaryAccentBar";
        weightSummaryAccentBar.Size = new Size(4, 28);
        weightSummaryAccentBar.TabIndex = 15;
        // 
        // weightSummaryTitleLabel
        // 
        weightSummaryTitleLabel.BackColor = Color.Transparent;
        weightSummaryTitleLabel.Font = new Font("Cascadia Code", 8.4F, FontStyle.Bold, GraphicsUnit.Point, 0);
        weightSummaryTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        weightSummaryTitleLabel.Location = new Point(28, 11);
        weightSummaryTitleLabel.Name = "weightSummaryTitleLabel";
        weightSummaryTitleLabel.Size = new Size(132, 18);
        weightSummaryTitleLabel.TabIndex = 16;
        weightSummaryTitleLabel.Text = "RESUMO DE PESO";
        // 
        // weightSummarySubtitleLabel
        // 
        weightSummarySubtitleLabel.BackColor = Color.Transparent;
        weightSummarySubtitleLabel.Font = new Font("Cascadia Code", 6.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        weightSummarySubtitleLabel.ForeColor = Color.FromArgb(75, 85, 99);
        weightSummarySubtitleLabel.Location = new Point(28, 28);
        weightSummarySubtitleLabel.Name = "weightSummarySubtitleLabel";
        weightSummarySubtitleLabel.Size = new Size(138, 14);
        weightSummarySubtitleLabel.TabIndex = 17;
        weightSummarySubtitleLabel.Text = "Previsto vs utilizado";
        // 
        // weightSummaryDividerLabel
        // 
        weightSummaryDividerLabel.BackColor = Color.FromArgb(226, 231, 238);
        weightSummaryDividerLabel.Location = new Point(14, 44);
        weightSummaryDividerLabel.Name = "weightSummaryDividerLabel";
        weightSummaryDividerLabel.Size = new Size(155, 1);
        weightSummaryDividerLabel.TabIndex = 18;
        // 
        // iniciarLeituraButton
        // 
        iniciarLeituraButton.BackColor = Color.Transparent;
        iniciarLeituraButton.BaseBackColor = Color.FromArgb(34, 166, 82);
        iniciarLeituraButton.BaseForeColor = Color.White;
        iniciarLeituraButton.Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
        iniciarLeituraButton.IconGlyph = "";
        iniciarLeituraButton.KeyHint = "F5";
        iniciarLeituraButton.Location = new Point(12, 350);
        iniciarLeituraButton.Name = "iniciarLeituraButton";
        iniciarLeituraButton.PrimaryText = "INICIAR LEITURA";
        iniciarLeituraButton.Size = new Size(190, 38);
        iniciarLeituraButton.TabIndex = 10;
        // 
        // lerEtiquetaButton
        // 
        lerEtiquetaButton.BackColor = Color.Transparent;
        lerEtiquetaButton.Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lerEtiquetaButton.IconFontFamily = "Segoe UI Symbol";
        lerEtiquetaButton.IconGlyph = "⚖";
        lerEtiquetaButton.KeyHint = "F12";
        lerEtiquetaButton.Location = new Point(12, 396);
        lerEtiquetaButton.Name = "lerEtiquetaButton";
        lerEtiquetaButton.PrimaryText = "LER PESO";
        lerEtiquetaButton.Size = new Size(190, 38);
        lerEtiquetaButton.TabIndex = 11;
        // 
        // leituraManualButton
        // 
        leituraManualButton.BackColor = Color.Transparent;
        leituraManualButton.Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
        leituraManualButton.IconGlyph = "";
        leituraManualButton.KeyHint = "F9";
        leituraManualButton.Location = new Point(12, 442);
        leituraManualButton.Name = "leituraManualButton";
        leituraManualButton.PrimaryText = "DIGITAR PESO";
        leituraManualButton.Size = new Size(190, 38);
        leituraManualButton.TabIndex = 12;
        // 
        // sidePanelLayout
        // 
        sidePanelLayout.ColumnCount = 1;
        sidePanelLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        sidePanelLayout.Controls.Add(groupBox3, 0, 0);
        sidePanelLayout.Controls.Add(sideActionsGroupBox, 0, 1);
        sidePanelLayout.Controls.Add(deleteLastLegendPanel, 0, 5);
        sidePanelLayout.Controls.Add(deleteByCodeLegendPanel, 0, 6);
        sidePanelLayout.Controls.Add(readWeightLegendPanel, 0, 2);
        sidePanelLayout.Controls.Add(manualLotLegendPanel, 0, 3);
        sidePanelLayout.Dock = DockStyle.Fill;
        sidePanelLayout.Location = new Point(6, 6);
        sidePanelLayout.Name = "sidePanelLayout";
        sidePanelLayout.RowCount = 8;
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 122F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 252F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        sidePanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        sidePanelLayout.Size = new Size(157, 656);
        sidePanelLayout.TabIndex = 0;
        // 
        // groupBox3
        // 
        groupBox3.Controls.Add(sideReadingStatusLabel);
        groupBox3.Dock = DockStyle.Fill;
        groupBox3.Font = new Font("Cascadia Code", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        groupBox3.ForeColor = Color.FromArgb(229, 231, 235);
        groupBox3.Location = new Point(3, 3);
        groupBox3.Name = "groupBox3";
        groupBox3.Size = new Size(151, 64);
        groupBox3.TabIndex = 13;
        groupBox3.TabStop = false;
        groupBox3.Text = "Status da Leitura";
        // 
        // sideReadingStatusLabel
        // 
        sideReadingStatusLabel.Dock = DockStyle.Fill;
        sideReadingStatusLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sideReadingStatusLabel.ForeColor = Color.FromArgb(220, 53, 69);
        sideReadingStatusLabel.Location = new Point(3, 17);
        sideReadingStatusLabel.Name = "sideReadingStatusLabel";
        sideReadingStatusLabel.Size = new Size(145, 44);
        sideReadingStatusLabel.TabIndex = 1;
        sideReadingStatusLabel.Text = "Inativa";
        sideReadingStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // sideActionsGroupBox
        // 
        sideActionsGroupBox.Controls.Add(tableLayoutPanel13);
        sideActionsGroupBox.Dock = DockStyle.Fill;
        sideActionsGroupBox.Font = new Font("Cascadia Code", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sideActionsGroupBox.ForeColor = Color.FromArgb(229, 231, 235);
        sideActionsGroupBox.Location = new Point(3, 73);
        sideActionsGroupBox.Name = "sideActionsGroupBox";
        sideActionsGroupBox.Size = new Size(151, 116);
        sideActionsGroupBox.TabIndex = 15;
        sideActionsGroupBox.TabStop = false;
        sideActionsGroupBox.Text = "Ações";
        // 
        // tableLayoutPanel13
        // 
        tableLayoutPanel13.ColumnCount = 1;
        tableLayoutPanel13.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel13.Controls.Add(startActionPanel, 0, 0);
        tableLayoutPanel13.Controls.Add(stopActionPanel, 0, 1);
        tableLayoutPanel13.Dock = DockStyle.Fill;
        tableLayoutPanel13.Location = new Point(3, 17);
        tableLayoutPanel13.Name = "tableLayoutPanel13";
        tableLayoutPanel13.RowCount = 2;
        tableLayoutPanel13.RowStyles.Add(new RowStyle(SizeType.Percent, 51.0416679F));
        tableLayoutPanel13.RowStyles.Add(new RowStyle(SizeType.Percent, 48.9583321F));
        tableLayoutPanel13.Size = new Size(145, 96);
        tableLayoutPanel13.TabIndex = 4;
        // 
        // startActionPanel
        // 
        startActionPanel.BackColor = Color.FromArgb(34, 166, 82);
        startActionPanel.Controls.Add(startActionIconLabel);
        startActionPanel.Controls.Add(startActionTextLabel);
        startActionPanel.Dock = DockStyle.Fill;
        startActionPanel.Location = new Point(0, 2);
        startActionPanel.Margin = new Padding(0, 2, 0, 2);
        startActionPanel.Name = "startActionPanel";
        startActionPanel.Size = new Size(145, 45);
        startActionPanel.TabIndex = 2;
        // 
        // startActionIconLabel
        // 
        startActionIconLabel.Anchor = AnchorStyles.Right;
        startActionIconLabel.BackColor = Color.Transparent;
        startActionIconLabel.Image = (Image)resources.GetObject("startActionIconLabel.Image");
        startActionIconLabel.Location = new Point(8, 5);
        startActionIconLabel.Name = "startActionIconLabel";
        startActionIconLabel.Size = new Size(32, 34);
        startActionIconLabel.SizeMode = PictureBoxSizeMode.StretchImage;
        startActionIconLabel.TabIndex = 0;
        startActionIconLabel.TabStop = false;
        // 
        // startActionTextLabel
        // 
        startActionTextLabel.Anchor = AnchorStyles.Right;
        startActionTextLabel.AutoEllipsis = true;
        startActionTextLabel.Font = new Font("Cascadia Code", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        startActionTextLabel.ForeColor = Color.FromArgb(209, 213, 219);
        startActionTextLabel.Location = new Point(3, 9);
        startActionTextLabel.Name = "startActionTextLabel";
        startActionTextLabel.Size = new Size(139, 27);
        startActionTextLabel.TabIndex = 1;
        startActionTextLabel.Text = "Iniciar";
        startActionTextLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // stopActionPanel
        // 
        stopActionPanel.BackColor = Color.FromArgb(55, 60, 69);
        stopActionPanel.Controls.Add(stopActionIconLabel);
        stopActionPanel.Controls.Add(stopActionTextLabel);
        stopActionPanel.Dock = DockStyle.Fill;
        stopActionPanel.Location = new Point(0, 51);
        stopActionPanel.Margin = new Padding(0, 2, 0, 2);
        stopActionPanel.Name = "stopActionPanel";
        stopActionPanel.Size = new Size(145, 43);
        stopActionPanel.TabIndex = 3;
        // 
        // stopActionIconLabel
        // 
        stopActionIconLabel.BackColor = Color.Transparent;
        stopActionIconLabel.Image = (Image)resources.GetObject("stopActionIconLabel.Image");
        stopActionIconLabel.Location = new Point(3, 6);
        stopActionIconLabel.Name = "stopActionIconLabel";
        stopActionIconLabel.Size = new Size(32, 33);
        stopActionIconLabel.SizeMode = PictureBoxSizeMode.StretchImage;
        stopActionIconLabel.TabIndex = 0;
        stopActionIconLabel.TabStop = false;
        // 
        // stopActionTextLabel
        // 
        stopActionTextLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        stopActionTextLabel.AutoEllipsis = true;
        stopActionTextLabel.Font = new Font("Cascadia Code", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        stopActionTextLabel.ForeColor = Color.FromArgb(229, 231, 235);
        stopActionTextLabel.Location = new Point(3, 6);
        stopActionTextLabel.Name = "stopActionTextLabel";
        stopActionTextLabel.Size = new Size(142, 33);
        stopActionTextLabel.TabIndex = 1;
        stopActionTextLabel.Text = "Parar";
        stopActionTextLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // deleteLastLegendPanel
        // 
        deleteLastLegendPanel.BackColor = Color.FromArgb(55, 60, 69);
        deleteLastLegendPanel.Controls.Add(deleteLastLegendIconLabel);
        deleteLastLegendPanel.Controls.Add(deleteLastLegendTextLabel);
        deleteLastLegendPanel.Dock = DockStyle.Fill;
        deleteLastLegendPanel.Location = new Point(3, 538);
        deleteLastLegendPanel.Name = "deleteLastLegendPanel";
        deleteLastLegendPanel.Size = new Size(151, 38);
        deleteLastLegendPanel.TabIndex = 11;
        // 
        // deleteLastLegendIconLabel
        // 
        deleteLastLegendIconLabel.BackColor = Color.Transparent;
        deleteLastLegendIconLabel.Image = (Image)resources.GetObject("deleteLastLegendIconLabel.Image");
        deleteLastLegendIconLabel.Location = new Point(2, 9);
        deleteLastLegendIconLabel.Name = "deleteLastLegendIconLabel";
        deleteLastLegendIconLabel.Size = new Size(20, 18);
        deleteLastLegendIconLabel.SizeMode = PictureBoxSizeMode.CenterImage;
        deleteLastLegendIconLabel.TabIndex = 0;
        deleteLastLegendIconLabel.TabStop = false;
        // 
        // deleteLastLegendTextLabel
        // 
        deleteLastLegendTextLabel.Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold);
        deleteLastLegendTextLabel.ForeColor = Color.FromArgb(229, 231, 235);
        deleteLastLegendTextLabel.Location = new Point(28, 2);
        deleteLastLegendTextLabel.Name = "deleteLastLegendTextLabel";
        deleteLastLegendTextLabel.Size = new Size(70, 34);
        deleteLastLegendTextLabel.TabIndex = 1;
        deleteLastLegendTextLabel.Text = "Excluir Ultima\r\nEtiqueta";
        deleteLastLegendTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // deleteByCodeLegendPanel
        // 
        deleteByCodeLegendPanel.BackColor = Color.FromArgb(55, 60, 69);
        deleteByCodeLegendPanel.Controls.Add(deleteByCodeLegendIconLabel);
        deleteByCodeLegendPanel.Controls.Add(deleteByCodeLegendTextLabel);
        deleteByCodeLegendPanel.Dock = DockStyle.Fill;
        deleteByCodeLegendPanel.Location = new Point(3, 582);
        deleteByCodeLegendPanel.Name = "deleteByCodeLegendPanel";
        deleteByCodeLegendPanel.Size = new Size(151, 38);
        deleteByCodeLegendPanel.TabIndex = 12;
        // 
        // deleteByCodeLegendIconLabel
        // 
        deleteByCodeLegendIconLabel.BackColor = Color.Transparent;
        deleteByCodeLegendIconLabel.Image = (Image)resources.GetObject("deleteByCodeLegendIconLabel.Image");
        deleteByCodeLegendIconLabel.Location = new Point(2, 9);
        deleteByCodeLegendIconLabel.Name = "deleteByCodeLegendIconLabel";
        deleteByCodeLegendIconLabel.Size = new Size(20, 18);
        deleteByCodeLegendIconLabel.SizeMode = PictureBoxSizeMode.CenterImage;
        deleteByCodeLegendIconLabel.TabIndex = 0;
        deleteByCodeLegendIconLabel.TabStop = false;
        // 
        // deleteByCodeLegendTextLabel
        // 
        deleteByCodeLegendTextLabel.Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold);
        deleteByCodeLegendTextLabel.ForeColor = Color.FromArgb(229, 231, 235);
        deleteByCodeLegendTextLabel.Location = new Point(28, 2);
        deleteByCodeLegendTextLabel.Name = "deleteByCodeLegendTextLabel";
        deleteByCodeLegendTextLabel.Size = new Size(70, 34);
        deleteByCodeLegendTextLabel.TabIndex = 1;
        deleteByCodeLegendTextLabel.Text = "Excluir Etiqueta\r\npor Codigo";
        deleteByCodeLegendTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // readWeightLegendPanel
        // 
        readWeightLegendPanel.BackColor = Color.FromArgb(55, 60, 69);
        readWeightLegendPanel.BorderStyle = BorderStyle.FixedSingle;
        readWeightLegendPanel.Controls.Add(readWeightLegendIconLabel);
        readWeightLegendPanel.Controls.Add(readWeightLegendTextLabel);
        readWeightLegendPanel.Dock = DockStyle.Fill;
        readWeightLegendPanel.Location = new Point(3, 195);
        readWeightLegendPanel.Name = "readWeightLegendPanel";
        readWeightLegendPanel.Size = new Size(151, 47);
        readWeightLegendPanel.TabIndex = 9;
        // 
        // readWeightLegendIconLabel
        // 
        readWeightLegendIconLabel.BackColor = Color.Transparent;
        readWeightLegendIconLabel.Image = (Image)resources.GetObject("readWeightLegendIconLabel.Image");
        readWeightLegendIconLabel.Location = new Point(5, 15);
        readWeightLegendIconLabel.Name = "readWeightLegendIconLabel";
        readWeightLegendIconLabel.Size = new Size(20, 18);
        readWeightLegendIconLabel.SizeMode = PictureBoxSizeMode.CenterImage;
        readWeightLegendIconLabel.TabIndex = 0;
        readWeightLegendIconLabel.TabStop = false;
        // 
        // readWeightLegendTextLabel
        // 
        readWeightLegendTextLabel.Font = new Font("Cascadia Code", 8.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        readWeightLegendTextLabel.ForeColor = Color.FromArgb(229, 231, 235);
        readWeightLegendTextLabel.Location = new Point(-1, 5);
        readWeightLegendTextLabel.Name = "readWeightLegendTextLabel";
        readWeightLegendTextLabel.Size = new Size(148, 34);
        readWeightLegendTextLabel.TabIndex = 1;
        readWeightLegendTextLabel.Text = "Ler Peso     F12";
        readWeightLegendTextLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // manualLotLegendPanel
        // 
        manualLotLegendPanel.BackColor = Color.FromArgb(55, 60, 69);
        manualLotLegendPanel.Controls.Add(manualLotLegendIconLabel);
        manualLotLegendPanel.Controls.Add(manualLotLegendTextLabel);
        manualLotLegendPanel.Dock = DockStyle.Fill;
        manualLotLegendPanel.Location = new Point(3, 248);
        manualLotLegendPanel.Name = "manualLotLegendPanel";
        manualLotLegendPanel.Size = new Size(151, 32);
        manualLotLegendPanel.TabIndex = 10;
        // 
        // manualLotLegendIconLabel
        // 
        manualLotLegendIconLabel.BackColor = Color.Transparent;
        manualLotLegendIconLabel.Image = (Image)resources.GetObject("manualLotLegendIconLabel.Image");
        manualLotLegendIconLabel.Location = new Point(2, 9);
        manualLotLegendIconLabel.Name = "manualLotLegendIconLabel";
        manualLotLegendIconLabel.Size = new Size(20, 18);
        manualLotLegendIconLabel.SizeMode = PictureBoxSizeMode.CenterImage;
        manualLotLegendIconLabel.TabIndex = 0;
        manualLotLegendIconLabel.TabStop = false;
        // 
        // manualLotLegendTextLabel
        // 
        manualLotLegendTextLabel.Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold);
        manualLotLegendTextLabel.ForeColor = Color.FromArgb(229, 231, 235);
        manualLotLegendTextLabel.Location = new Point(28, 2);
        manualLotLegendTextLabel.Name = "manualLotLegendTextLabel";
        manualLotLegendTextLabel.Size = new Size(70, 34);
        manualLotLegendTextLabel.TabIndex = 1;
        manualLotLegendTextLabel.Text = "[F5] - Lote\r\nManual";
        manualLotLegendTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // tableLayoutPanel2
        // 
        tableLayoutPanel2.ColumnCount = 2;
        tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 227F));
        tableLayoutPanel2.Controls.Add(customTitleBarPanel, 0, 0);
        tableLayoutPanel2.Controls.Add(sidePanel, 1, 1);
        tableLayoutPanel2.Controls.Add(rootTableLayoutPanel, 0, 1);
        tableLayoutPanel2.Controls.Add(footerBar, 0, 2);
        tableLayoutPanel2.Dock = DockStyle.Fill;
        tableLayoutPanel2.Location = new Point(0, 0);
        tableLayoutPanel2.Name = "tableLayoutPanel2";
        tableLayoutPanel2.RowCount = 3;
        tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        tableLayoutPanel2.Size = new Size(1366, 720);
        tableLayoutPanel2.TabIndex = 1;
        // 
        // ProcessoSemiAcabadoForm
        // 
        AutoScaleDimensions = new SizeF(7F, 16F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        ClientSize = new Size(1366, 720);
        Controls.Add(tableLayoutPanel2);
        Font = new Font("Cascadia Code", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1180, 648);
        Name = "ProcessoSemiAcabadoForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Produto Semi-Acabado";
        WindowState = FormWindowState.Maximized;
        rootTableLayoutPanel.ResumeLayout(false);
        tableLayoutPanel1.ResumeLayout(false);
        tableLayoutPanel3.ResumeLayout(false);
        productionOrderShadowPanel.ResumeLayout(false);
        productionOrderIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)productionOrderSearchLabel).EndInit();
        lotCardPanel.ResumeLayout(false);
        lotCardPanel.PerformLayout();
        lotIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)lotIconPictureBox).EndInit();
        stepCardPanel.ResumeLayout(false);
        finishedProductCardPanel.ResumeLayout(false);
        finishedProductCardPanel.PerformLayout();
        finishedProductIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)finishedProductIconPictureBox).EndInit();
        tableLayoutPanel11.ResumeLayout(false);
        productionReadingsPanel.ResumeLayout(false);
        productionReadingsPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)productionReadingsTitleIconPictureBox).EndInit();
        productionSearchPanel.ResumeLayout(false);
        productionSearchPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)productionDataGridView).EndInit();
        apontamentoInfoPanel.ResumeLayout(false);
        apontamentoChipPanel.ResumeLayout(false);
        tableLayoutPanel5.ResumeLayout(false);
        groupBox2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dateTitleIconPictureBox).EndInit();
        tableLayoutPanel6.ResumeLayout(false);
        tableLayoutPanel6.PerformLayout();
        Gpb_PrevisaoLeitura.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)plannedProductionTitleIconPictureBox).EndInit();
        tableLayoutPanel8.ResumeLayout(false);
        tableLayoutPanel8.PerformLayout();
        headerPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)materialTitleIconPictureBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)materialFilterIconPictureBox).EndInit();
        tableLayoutPanel10.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)materialDataGridView).EndInit();
        ((System.ComponentModel.ISupportInitialize)productionSearchIconPictureBox).EndInit();
        tableLayoutPanel7.ResumeLayout(false);
        tableLayoutPanel7.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)lotSearchIconLabel).EndInit();
        footerBar.ResumeLayout(false);
        footerBarLayout.ResumeLayout(false);
        cellUser.ResumeLayout(false);
        cellTerminal.ResumeLayout(false);
        cellEmpresa.ResumeLayout(false);
        cellBanco.ResumeLayout(false);
        cellHora.ResumeLayout(false);
        cellData.ResumeLayout(false);
        statusCell.ResumeLayout(false);
        groupBox1.ResumeLayout(false);
        customTitleBarPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).EndInit();
        headerTitleIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).EndInit();
        sapStatusPanel.ResumeLayout(false);
        sidePanel.ResumeLayout(false);
        statusCard.ResumeLayout(false);
        groupBox4.ResumeLayout(false);
        tableLayoutPanel12.ResumeLayout(false);
        weightSummaryForecastPanel.ResumeLayout(false);
        weightSummaryUsedPanel.ResumeLayout(false);
        sidePanelLayout.ResumeLayout(false);
        groupBox3.ResumeLayout(false);
        sideActionsGroupBox.ResumeLayout(false);
        tableLayoutPanel13.ResumeLayout(false);
        startActionPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)startActionIconLabel).EndInit();
        stopActionPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)stopActionIconLabel).EndInit();
        deleteLastLegendPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)deleteLastLegendIconLabel).EndInit();
        deleteByCodeLegendPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)deleteByCodeLegendIconLabel).EndInit();
        readWeightLegendPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)readWeightLegendIconLabel).EndInit();
        manualLotLegendPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)manualLotLegendIconLabel).EndInit();
        tableLayoutPanel2.ResumeLayout(false);
        ResumeLayout(false);
    }
    private TableLayoutPanel tableLayoutPanel2;
    private FugaPET_Dev.Tela.Controls.RoundedPanel headerPanel;
    private PictureBox materialTitleIconPictureBox;
    private Label materialTitleLabel;
    private Label materialTitleUnderlineLabel;
    private PictureBox materialFilterIconPictureBox;
    private Label materialViewAllLabel;
    private Label materialViewAllChevronLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel productionOrderShadowPanel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel productionOrderIconPanel;
    private Label productionOrderCaptionLabel;
    private ComboBox pedidoComboBox;
    private PictureBox productionOrderSearchLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel stepCardPanel;
    private Label stepCaptionLabel;
    private Label stepDescriptionLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel finishedProductCardPanel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel finishedProductIconPanel;
    private PictureBox finishedProductIconPictureBox;
    private Label menuHeaderLabel;
    private Label headerDividerLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel headerTitleIconPanel;
    private PictureBox headerTitleIconPictureBox;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel sapStatusPanel;
    private Label sapStatusDotLabel;
    private Label sapStatusLabel;
    private Label logoSaLabel;
    private Label minimizeWindowLabel;
    private Label maximizeWindowLabel;
    private Label closeWindowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel lotCardPanel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel lotIconPanel;
    private PictureBox lotIconPictureBox;
    private Label lotCaptionLabel;
    private TextBox lotTextBox;
    private Label stepLabel;
    private Label finishedProductCaptionLabel;
    private TextBox finishedProductCodeTextBox;
    private TextBox finishedProductTextBox;
    private Label ovenExitCaptionLabel;
    private TextBox ovenExitTextBox;
    private Label classificationDateCaptionLabel;
    private TextBox classificationDateTextBox;
    private Label manufacturingDateCaptionLabel;
    private TextBox manufacturingDateTextBox;
    private Label expirationDateCaptionLabel;
    private TextBox expirationDateTextBox;
    private FugaPET_Dev.Tela.Controls.RoundedPanel Gpb_PrevisaoLeitura;
    private PictureBox plannedProductionTitleIconPictureBox;
    private Label plannedProductionTitleLabel;
    private Label plannedProductionDividerLabel1;
    private Label plannedProductionDividerLabel2;
    private Label readForecastPackagesCaptionLabel;
    private TextBox readForecastPackagesTextBox;
    private TextBox readForecastBoxesTextBox;
    private Label readForecastBoxesCaptionLabel;
    private TableLayoutPanel tableLayoutPanel1;
    private TableLayoutPanel tableLayoutPanel3;
    private TableLayoutPanel tableLayoutPanel5;
    private TableLayoutPanel tableLayoutPanel6;
    private TableLayoutPanel tableLayoutPanel8;
    private TableLayoutPanel tableLayoutPanel10;
    private TableLayoutPanel tableLayoutPanel11;
    private FugaPET_Dev.Tela.Controls.RoundedPanel productionReadingsPanel;
    private PictureBox productionReadingsTitleIconPictureBox;
    private Label productionReadingsTitleLabel;
    private Label productionReadingsUnderlineLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel productionSearchPanel;
    private TextBox productionSearchTextBox;
    private Label productionSearchGlyphLabel;
    private PictureBox productionSearchIconPictureBox;
    private Button productionFilterButton;
    private Button productionActionsButton;
    private Label productionFooterLabel;
    private Label productionPageLabel;
    private Button productionPreviousPageButton;
    private TextBox productionPageTextBox;
    private Button productionNextPageButton;
    private FugaPET_Dev.Tela.Controls.RoundedPanel groupBox2;
    private PictureBox dateTitleIconPictureBox;
    private Label dateTitleLabel;
    private Label dateDividerLabel1;
    private Label dateDividerLabel2;
    private Label dateDividerLabel3;
    private TableLayoutPanel tableLayoutPanel7;
    private Label label1;
    private PictureBox lotSearchIconLabel;
    private GroupBox groupBox3;
    private FugaPET_Dev.Tela.Controls.RoundedPanel groupBox4;
    private TableLayoutPanel tableLayoutPanel12;
    private TableLayoutPanel tableLayoutPanel13;
    private GroupBox groupBox1;
    private TableLayoutPanel tableLayoutPanel9;
    private TextBox balanceTextBox;
    private Label balanceCaptionLabel;
    private PictureBox companyLogoPictureBox;
    private Panel footerBar;
    private TableLayoutPanel footerBarLayout;
    private Panel statusCell;
    private Panel cellUser;
    private Label cellUserIcon;
    private Label cellUserText;
    private Panel cellUserDivider;
    private Panel cellTerminal;
    private Label cellTerminalIcon;
    private Label cellTerminalText;
    private Panel cellTerminalDivider;
    private Panel cellEmpresa;
    private Label cellEmpresaIcon;
    private Label cellEmpresaText;
    private Panel cellEmpresaDivider;
    private Panel cellBanco;
    private Label cellBancoIcon;
    private Label cellBancoText;
    private Panel cellBancoDivider;
    private Panel cellHora;
    private Label cellHoraIcon;
    private Label cellHoraText;
    private Panel cellHoraDivider;
    private Panel cellData;
    private Label cellDataIcon;
    private Label cellDataText;
    private Panel statusCellDivider;
    private Label sideStatusTitleLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel statusCard;
    private Label statusCardIcon;
    private Label statusValueLabel;
    private Label statusHintLabel;
    private FugaPET_Dev.Tela.ActionPillButton iniciarLeituraButton;
    private FugaPET_Dev.Tela.ActionPillButton lerEtiquetaButton;
    private FugaPET_Dev.Tela.ActionPillButton leituraManualButton;
}






