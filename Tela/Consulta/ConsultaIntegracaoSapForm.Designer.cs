namespace FugaPET_Dev.Tela.Consulta;

partial class ConsultaIntegracaoSapForm
{
    private System.ComponentModel.IContainer components = null;

    // Title bar
    private Panel customTitleBarPanel;
    private PictureBox companyLogoPictureBox;
    private Label logoSaLabel;
    private Label headerDividerLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel headerTitleIconPanel;
    private PictureBox headerTitleIconPictureBox;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel sapStatusPanel;
    private Label sapStatusDotLabel;
    private Label sapStatusLabel;
    private Label minimizeWindowLabel;
    private Label maximizeWindowLabel;
    private Label closeWindowLabel;

    // Root layout
    private TableLayoutPanel rootLayout;
    private TableLayoutPanel contentLayout;

    // Filters card (row 0)
    private FugaPET_Dev.Tela.Controls.RoundedPanel filtersCard;
    private FugaPET_Dev.Tela.Controls.RoundedPanel searchInputPanel;
    private PictureBox searchIconPictureBox;
    private TextBox searchTextBox;
    private Label linhaCaptionLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel linhaInputPanel;
    private ComboBox linhaComboBox;
    private Label turnoCaptionLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel turnoInputPanel;
    private ComboBox turnoComboBox;
    private Button atualizarButton;

    // Orders card (row 1)
    private FugaPET_Dev.Tela.Controls.RoundedPanel ordersCard;
    private PictureBox ordersTitleIconPictureBox;
    private Label ordersTitleLabel;
    private DataGridView ordersDataGridView;
    private DataGridViewTextBoxColumn colDataHora;
    private DataGridViewTextBoxColumn colTipo;
    private DataGridViewTextBoxColumn colOp;
    private DataGridViewTextBoxColumn colProduto;
    private DataGridViewTextBoxColumn colLote;
    private DataGridViewTextBoxColumn colCodigoBarras;
    private DataGridViewTextBoxColumn colQuantidade;
    private DataGridViewTextBoxColumn colUnidade;
    private DataGridViewTextBoxColumn colUsuario;
    private DataGridViewTextBoxColumn colOrigem;
    private DataGridViewTextBoxColumn colSituacao;
    private DataGridViewTextBoxColumn colDetalhes;
    private Label ordersFooterLabel;
    private Button ordersPreviousPageButton;
    private TextBox ordersPageTextBox;
    private Button ordersNextPageButton;

    // Footer
    private Panel footerBar;
    private TableLayoutPanel footerBarLayout;
    private Panel statusCell;
    private Label statusLabel;
    private Panel statusCellDivider;
    private Panel cellUser;
    private Label cellUserText;
    private Label cellUserIcon;
    private Panel cellUserDivider;
    private Panel cellTerminal;
    private Label cellTerminalText;
    private Label cellTerminalIcon;
    private Panel cellTerminalDivider;
    private Panel cellEmpresa;
    private Label cellEmpresaText;
    private Label cellEmpresaIcon;
    private Panel cellEmpresaDivider;
    private Panel cellBanco;
    private Label cellBancoText;
    private Label cellBancoIcon;
    private Panel cellBancoDivider;
    private Panel cellHora;
    private Label cellHoraText;
    private Label cellHoraIcon;
    private Panel cellHoraDivider;
    private Panel cellData;
    private Label cellDataText;
    private Label cellDataIcon;

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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ConsultaIntegracaoSapForm));
        DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
        customTitleBarPanel = new Panel();
        menuHeaderLabel = new Label();
        logoSaLabel = new Label();
        companyLogoPictureBox = new PictureBox();
        headerDividerLabel = new Label();
        headerTitleIconPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        headerTitleIconPictureBox = new PictureBox();
        headerTitleLabel = new Label();
        headerSubtitleLabel = new Label();
        sapStatusPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        sapStatusDotLabel = new Label();
        sapStatusLabel = new Label();
        minimizeWindowLabel = new Label();
        maximizeWindowLabel = new Label();
        closeWindowLabel = new Label();
        rootLayout = new TableLayoutPanel();
        contentLayout = new TableLayoutPanel();
        filtersCard = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        searchIconPictureBox = new PictureBox();
        searchInputPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        searchTextBox = new TextBox();
        linhaInputPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        linhaComboBox = new ComboBox();
        turnoInputPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        turnoComboBox = new ComboBox();
        atualizarButton = new Button();
        linhaCaptionLabel = new Label();
        turnoCaptionLabel = new Label();
        ordersCard = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        ordersTitleIconPictureBox = new PictureBox();
        ordersTitleLabel = new Label();
        ordersDataGridView = new DataGridView();
        colDataHora = new DataGridViewTextBoxColumn();
        colTipo = new DataGridViewTextBoxColumn();
        colOp = new DataGridViewTextBoxColumn();
        colProduto = new DataGridViewTextBoxColumn();
        colLote = new DataGridViewTextBoxColumn();
        colCodigoBarras = new DataGridViewTextBoxColumn();
        colQuantidade = new DataGridViewTextBoxColumn();
        colUnidade = new DataGridViewTextBoxColumn();
        colUsuario = new DataGridViewTextBoxColumn();
        colOrigem = new DataGridViewTextBoxColumn();
        colSituacao = new DataGridViewTextBoxColumn();
        colDetalhes = new DataGridViewTextBoxColumn();
        ordersFooterLabel = new Label();
        ordersPreviousPageButton = new Button();
        ordersPageTextBox = new TextBox();
        ordersNextPageButton = new Button();
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
        statusLabel = new Label();
        statusCellDivider = new Panel();
        customTitleBarPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).BeginInit();
        headerTitleIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).BeginInit();
        sapStatusPanel.SuspendLayout();
        rootLayout.SuspendLayout();
        contentLayout.SuspendLayout();
        filtersCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)searchIconPictureBox).BeginInit();
        searchInputPanel.SuspendLayout();
        linhaInputPanel.SuspendLayout();
        turnoInputPanel.SuspendLayout();
        ordersCard.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)ordersTitleIconPictureBox).BeginInit();
        ((System.ComponentModel.ISupportInitialize)ordersDataGridView).BeginInit();
        footerBar.SuspendLayout();
        footerBarLayout.SuspendLayout();
        cellUser.SuspendLayout();
        cellTerminal.SuspendLayout();
        cellEmpresa.SuspendLayout();
        cellBanco.SuspendLayout();
        cellHora.SuspendLayout();
        cellData.SuspendLayout();
        statusCell.SuspendLayout();
        SuspendLayout();
        // 
        // customTitleBarPanel
        // 
        customTitleBarPanel.BackColor = Color.FromArgb(24, 31, 43);
        customTitleBarPanel.Controls.Add(menuHeaderLabel);
        customTitleBarPanel.Controls.Add(logoSaLabel);
        customTitleBarPanel.Controls.Add(companyLogoPictureBox);
        customTitleBarPanel.Controls.Add(headerDividerLabel);
        customTitleBarPanel.Controls.Add(headerTitleIconPanel);
        customTitleBarPanel.Controls.Add(headerTitleLabel);
        customTitleBarPanel.Controls.Add(headerSubtitleLabel);
        customTitleBarPanel.Controls.Add(sapStatusPanel);
        customTitleBarPanel.Controls.Add(minimizeWindowLabel);
        customTitleBarPanel.Controls.Add(maximizeWindowLabel);
        customTitleBarPanel.Controls.Add(closeWindowLabel);
        customTitleBarPanel.Dock = DockStyle.Fill;
        customTitleBarPanel.Location = new Point(0, 0);
        customTitleBarPanel.Margin = new Padding(0);
        customTitleBarPanel.Name = "customTitleBarPanel";
        customTitleBarPanel.Size = new Size(1366, 52);
        customTitleBarPanel.TabIndex = 1;
        // 
        // menuHeaderLabel
        // 
        menuHeaderLabel.Cursor = Cursors.Hand;
        menuHeaderLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        menuHeaderLabel.ForeColor = Color.White;
        menuHeaderLabel.Location = new Point(18, 5);
        menuHeaderLabel.Name = "menuHeaderLabel";
        menuHeaderLabel.Size = new Size(36, 40);
        menuHeaderLabel.TabIndex = 7;
        menuHeaderLabel.Text = "";
        menuHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
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
        headerTitleIconPanel.BorderThickness = 0;
        headerTitleIconPanel.BorderRadius = 6;
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
        headerTitleIconPictureBox.Image = global::FugaPET_Dev.Properties.Resources.sap_menu_24x_white;
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
        headerTitleLabel.Text = "Integracao SAP";
        headerTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
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
        headerSubtitleLabel.Text = "Monitoramento dos envios e retornos da integracao";
        headerSubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
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
        sapStatusPanel.Location = new Point(910, 10);
        sapStatusPanel.Name = "sapStatusPanel";
        sapStatusPanel.ShadowBlur = 0;
        sapStatusPanel.ShadowOffsetY = 0;
        sapStatusPanel.Size = new Size(190, 27);
        sapStatusPanel.TabIndex = 9;
        // 
        // sapStatusDotLabel
        // 
        sapStatusDotLabel.BackColor = Color.Transparent;
        sapStatusDotLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
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
        sapStatusLabel.Size = new Size(151, 17);
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
        minimizeWindowLabel.Text = "−";
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
        // rootLayout
        // 
        rootLayout.BackColor = Color.FromArgb(247, 248, 250);
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(customTitleBarPanel, 0, 0);
        rootLayout.Controls.Add(contentLayout, 0, 1);
        rootLayout.Controls.Add(footerBar, 0, 2);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.RowCount = 3;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        rootLayout.Size = new Size(1366, 720);
        rootLayout.TabIndex = 0;
        // 
        // contentLayout
        // 
        contentLayout.BackColor = Color.Transparent;
        contentLayout.ColumnCount = 1;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contentLayout.Controls.Add(filtersCard, 0, 0);
        contentLayout.Controls.Add(ordersCard, 0, 1);
        contentLayout.Dock = DockStyle.Fill;
        contentLayout.Location = new Point(0, 52);
        contentLayout.Margin = new Padding(0);
        contentLayout.Name = "contentLayout";
        contentLayout.Padding = new Padding(12, 12, 12, 6);
        contentLayout.RowCount = 2;
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        contentLayout.Size = new Size(1366, 630);
        contentLayout.TabIndex = 0;
        // 
        // filtersCard
        // 
        filtersCard.BackColor = Color.Transparent;
        filtersCard.BorderRadius = 7;
        filtersCard.Controls.Add(searchIconPictureBox);
        filtersCard.Controls.Add(searchInputPanel);
        filtersCard.Controls.Add(linhaInputPanel);
        filtersCard.Controls.Add(turnoInputPanel);
        filtersCard.Controls.Add(atualizarButton);
        filtersCard.Controls.Add(linhaCaptionLabel);
        filtersCard.Controls.Add(turnoCaptionLabel);
        filtersCard.Dock = DockStyle.Fill;
        filtersCard.Location = new Point(12, 12);
        filtersCard.Margin = new Padding(0, 0, 0, 9);
        filtersCard.Name = "filtersCard";
        filtersCard.ShadowBlur = 0;
        filtersCard.ShadowOffsetY = 0;
        filtersCard.Size = new Size(1342, 69);
        filtersCard.TabIndex = 0;
        // 
        // searchIconPictureBox
        // 
        searchIconPictureBox.BackColor = Color.Transparent;
        searchIconPictureBox.Image = (Image)resources.GetObject("searchIconPictureBox.Image");
        searchIconPictureBox.Location = new Point(14, 14);
        searchIconPictureBox.Name = "searchIconPictureBox";
        searchIconPictureBox.Size = new Size(26, 33);
        searchIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        searchIconPictureBox.TabIndex = 0;
        searchIconPictureBox.TabStop = false;
        // 
        // searchInputPanel
        // 
        searchInputPanel.BackColor = Color.Transparent;
        searchInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        searchInputPanel.BorderRadius = 5;
        searchInputPanel.Controls.Add(searchTextBox);
        searchInputPanel.Location = new Point(46, 14);
        searchInputPanel.Name = "searchInputPanel";
        searchInputPanel.ShadowBlur = 0;
        searchInputPanel.ShadowOffsetY = 0;
        searchInputPanel.Size = new Size(420, 33);
        searchInputPanel.TabIndex = 1;
        // 
        // searchTextBox
        // 
        searchTextBox.BackColor = Color.White;
        searchTextBox.BorderStyle = BorderStyle.None;
        searchTextBox.Font = new Font("Segoe UI", 9F);
        searchTextBox.ForeColor = Color.FromArgb(120, 130, 145);
        searchTextBox.Location = new Point(12, 10);
        searchTextBox.Name = "searchTextBox";
        searchTextBox.Size = new Size(396, 16);
        searchTextBox.TabIndex = 1;
        searchTextBox.Text = "Digite OP, lote ou produto...";
        // 
        // linhaInputPanel
        // 
        linhaInputPanel.BackColor = Color.Transparent;
        linhaInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        linhaInputPanel.BorderRadius = 5;
        linhaInputPanel.Controls.Add(linhaComboBox);
        linhaInputPanel.Location = new Point(510, 17);
        linhaInputPanel.Name = "linhaInputPanel";
        linhaInputPanel.ShadowBlur = 0;
        linhaInputPanel.ShadowOffsetY = 0;
        linhaInputPanel.Size = new Size(190, 33);
        linhaInputPanel.TabIndex = 3;
        // 
        // linhaComboBox
        // 
        linhaComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        linhaComboBox.FlatStyle = FlatStyle.Flat;
        linhaComboBox.Font = new Font("Segoe UI", 9F);
        linhaComboBox.ForeColor = Color.FromArgb(17, 24, 39);
        linhaComboBox.FormattingEnabled = true;
        linhaComboBox.Items.AddRange(new object[] { "Todos", "Envio", "Retorno", "Erro" });
        linhaComboBox.Location = new Point(12, 4);
        linhaComboBox.Name = "linhaComboBox";
        linhaComboBox.Size = new Size(166, 23);
        linhaComboBox.TabIndex = 3;
        // 
        // turnoInputPanel
        // 
        turnoInputPanel.BackColor = Color.Transparent;
        turnoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        turnoInputPanel.BorderRadius = 5;
        turnoInputPanel.Controls.Add(turnoComboBox);
        turnoInputPanel.Location = new Point(720, 17);
        turnoInputPanel.Name = "turnoInputPanel";
        turnoInputPanel.ShadowBlur = 0;
        turnoInputPanel.ShadowOffsetY = 0;
        turnoInputPanel.Size = new Size(190, 33);
        turnoInputPanel.TabIndex = 5;
        // 
        // turnoComboBox
        // 
        turnoComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        turnoComboBox.FlatStyle = FlatStyle.Flat;
        turnoComboBox.Font = new Font("Segoe UI", 9F);
        turnoComboBox.ForeColor = Color.FromArgb(17, 24, 39);
        turnoComboBox.FormattingEnabled = true;
        turnoComboBox.Items.AddRange(new object[] { "Todas", "Sucesso", "Confirmado", "Enviado", "Erro" });
        turnoComboBox.Location = new Point(12, 4);
        turnoComboBox.Name = "turnoComboBox";
        turnoComboBox.Size = new Size(166, 23);
        turnoComboBox.TabIndex = 5;
        // 
        // atualizarButton
        // 
        atualizarButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        atualizarButton.BackColor = Color.FromArgb(220, 53, 69);
        atualizarButton.Cursor = Cursors.Hand;
        atualizarButton.FlatAppearance.BorderColor = Color.FromArgb(220, 53, 69);
        atualizarButton.FlatAppearance.BorderSize = 0;
        atualizarButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 35, 51);
        atualizarButton.FlatStyle = FlatStyle.Flat;
        atualizarButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        atualizarButton.ForeColor = Color.White;
        atualizarButton.Location = new Point(2317, 18);
        atualizarButton.Name = "atualizarButton";
        atualizarButton.Size = new Size(150, 32);
        atualizarButton.TabIndex = 6;
        atualizarButton.Text = "🔎  Pesquisar";
        atualizarButton.UseVisualStyleBackColor = false;
        // 
        // linhaCaptionLabel
        // 
        linhaCaptionLabel.BackColor = Color.Transparent;
        linhaCaptionLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        linhaCaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        linhaCaptionLabel.Location = new Point(510, 5);
        linhaCaptionLabel.Name = "linhaCaptionLabel";
        linhaCaptionLabel.Size = new Size(120, 14);
        linhaCaptionLabel.TabIndex = 2;
        linhaCaptionLabel.Text = "Tipo de Movimentacao";
        linhaCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // turnoCaptionLabel
        // 
        turnoCaptionLabel.BackColor = Color.Transparent;
        turnoCaptionLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        turnoCaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        turnoCaptionLabel.Location = new Point(720, 5);
        turnoCaptionLabel.Name = "turnoCaptionLabel";
        turnoCaptionLabel.Size = new Size(120, 14);
        turnoCaptionLabel.TabIndex = 4;
        turnoCaptionLabel.Text = "Situacao";
        turnoCaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // ordersCard
        // 
        ordersCard.BackColor = Color.Transparent;
        ordersCard.BorderRadius = 7;
        ordersCard.Controls.Add(ordersTitleIconPictureBox);
        ordersCard.Controls.Add(ordersTitleLabel);
        ordersCard.Controls.Add(ordersDataGridView);
        ordersCard.Controls.Add(ordersFooterLabel);
        ordersCard.Controls.Add(ordersPreviousPageButton);
        ordersCard.Controls.Add(ordersPageTextBox);
        ordersCard.Controls.Add(ordersNextPageButton);
        ordersCard.Dock = DockStyle.Fill;
        ordersCard.Location = new Point(12, 90);
        ordersCard.Margin = new Padding(0);
        ordersCard.Name = "ordersCard";
        ordersCard.ShadowBlur = 0;
        ordersCard.ShadowOffsetY = 0;
        ordersCard.Size = new Size(1342, 534);
        ordersCard.TabIndex = 1;
        // 
        // ordersTitleIconPictureBox
        // 
        ordersTitleIconPictureBox.BackColor = Color.Transparent;
        ordersTitleIconPictureBox.Image = (Image)resources.GetObject("ordersTitleIconPictureBox.Image");
        ordersTitleIconPictureBox.Location = new Point(18, 14);
        ordersTitleIconPictureBox.Name = "ordersTitleIconPictureBox";
        ordersTitleIconPictureBox.Size = new Size(18, 18);
        ordersTitleIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        ordersTitleIconPictureBox.TabIndex = 0;
        ordersTitleIconPictureBox.TabStop = false;
        // 
        // ordersTitleLabel
        // 
        ordersTitleLabel.BackColor = Color.Transparent;
        ordersTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        ordersTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        ordersTitleLabel.Location = new Point(40, 14);
        ordersTitleLabel.Name = "ordersTitleLabel";
        ordersTitleLabel.Size = new Size(360, 18);
        ordersTitleLabel.TabIndex = 1;
        ordersTitleLabel.Text = "HISTORICO DA INTEGRACAO SAP";
        ordersTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // ordersDataGridView
        // 
        ordersDataGridView.AllowUserToAddRows = false;
        ordersDataGridView.AllowUserToDeleteRows = false;
        ordersDataGridView.AllowUserToResizeRows = false;
        ordersDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        ordersDataGridView.BackgroundColor = Color.White;
        ordersDataGridView.BorderStyle = BorderStyle.None;
        ordersDataGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        ordersDataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        dataGridViewCellStyle1.BackColor = Color.FromArgb(31, 41, 55);
        dataGridViewCellStyle1.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        dataGridViewCellStyle1.ForeColor = Color.White;
        dataGridViewCellStyle1.Padding = new Padding(8, 0, 4, 0);
        dataGridViewCellStyle1.SelectionBackColor = Color.FromArgb(31, 41, 55);
        dataGridViewCellStyle1.SelectionForeColor = Color.White;
        ordersDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
        ordersDataGridView.ColumnHeadersHeight = 32;
        ordersDataGridView.Columns.AddRange(new DataGridViewColumn[] { colDataHora, colTipo, colOp, colProduto, colLote, colCodigoBarras, colQuantidade, colUnidade, colUsuario, colOrigem, colSituacao, colDetalhes });
        dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle3.BackColor = SystemColors.Window;
        dataGridViewCellStyle3.Font = new Font("Segoe UI", 8.5F);
        dataGridViewCellStyle3.ForeColor = Color.FromArgb(17, 24, 39);
        dataGridViewCellStyle3.Padding = new Padding(8, 0, 4, 0);
        dataGridViewCellStyle3.SelectionBackColor = Color.FromArgb(243, 244, 246);
        dataGridViewCellStyle3.SelectionForeColor = Color.FromArgb(17, 24, 39);
        dataGridViewCellStyle3.WrapMode = DataGridViewTriState.False;
        ordersDataGridView.DefaultCellStyle = dataGridViewCellStyle3;
        ordersDataGridView.EnableHeadersVisualStyles = false;
        ordersDataGridView.GridColor = Color.FromArgb(229, 232, 238);
        ordersDataGridView.Location = new Point(14, 42);
        ordersDataGridView.Name = "ordersDataGridView";
        ordersDataGridView.RowHeadersVisible = false;
        ordersDataGridView.RowTemplate.Height = 30;
        ordersDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        ordersDataGridView.Size = new Size(2458, 844);
        ordersDataGridView.TabIndex = 2;
        // 
        // colDataHora
        // 
        colDataHora.HeaderText = "Data/Hora";
        colDataHora.Name = "colDataHora";
        colDataHora.Width = 145;
        // 
        // colTipo
        // 
        colTipo.HeaderText = "Tipo";
        colTipo.Name = "colTipo";
        colTipo.Width = 90;
        // 
        // colOp
        // 
        colOp.HeaderText = "OP";
        colOp.Name = "colOp";
        colOp.Width = 62;
        // 
        // colProduto
        // 
        colProduto.HeaderText = "Produto";
        colProduto.Name = "colProduto";
        colProduto.Width = 220;
        // 
        // colLote
        // 
        colLote.HeaderText = "Lote";
        colLote.Name = "colLote";
        colLote.Width = 75;
        // 
        // colCodigoBarras
        // 
        colCodigoBarras.HeaderText = "Codigo de Barras";
        colCodigoBarras.Name = "colCodigoBarras";
        colCodigoBarras.Width = 140;
        // 
        // colQuantidade
        // 
        colQuantidade.HeaderText = "Qtd.";
        colQuantidade.Name = "colQuantidade";
        colQuantidade.Width = 60;
        // 
        // colUnidade
        // 
        colUnidade.HeaderText = "Unid.";
        colUnidade.Name = "colUnidade";
        colUnidade.Width = 55;
        // 
        // colUsuario
        // 
        colUsuario.HeaderText = "Usuario";
        colUsuario.Name = "colUsuario";
        colUsuario.Width = 90;
        // 
        // colOrigem
        // 
        colOrigem.HeaderText = "Origem";
        colOrigem.Name = "colOrigem";
        colOrigem.Width = 90;
        // 
        // colSituacao
        // 
        colSituacao.HeaderText = "Situacao";
        colSituacao.Name = "colSituacao";
        colSituacao.Width = 80;
        // 
        // colDetalhes
        // 
        dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dataGridViewCellStyle2.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        dataGridViewCellStyle2.ForeColor = Color.FromArgb(98, 108, 124);
        colDetalhes.DefaultCellStyle = dataGridViewCellStyle2;
        colDetalhes.HeaderText = "Detalhes";
        colDetalhes.Name = "colDetalhes";
        colDetalhes.Width = 75;
        // 
        // ordersFooterLabel
        // 
        ordersFooterLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        ordersFooterLabel.BackColor = Color.Transparent;
        ordersFooterLabel.Font = new Font("Segoe UI", 8F);
        ordersFooterLabel.ForeColor = Color.FromArgb(98, 108, 124);
        ordersFooterLabel.Location = new Point(18, 894);
        ordersFooterLabel.Name = "ordersFooterLabel";
        ordersFooterLabel.Size = new Size(280, 22);
        ordersFooterLabel.TabIndex = 3;
        ordersFooterLabel.Text = "Mostrando 1 a 8 de 184 registros";
        ordersFooterLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // ordersPreviousPageButton
        // 
        ordersPreviousPageButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        ordersPreviousPageButton.BackColor = Color.White;
        ordersPreviousPageButton.Cursor = Cursors.Hand;
        ordersPreviousPageButton.FlatAppearance.BorderColor = Color.FromArgb(214, 219, 226);
        ordersPreviousPageButton.FlatStyle = FlatStyle.Flat;
        ordersPreviousPageButton.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        ordersPreviousPageButton.ForeColor = Color.FromArgb(98, 108, 124);
        ordersPreviousPageButton.Location = new Point(2338, 892);
        ordersPreviousPageButton.Name = "ordersPreviousPageButton";
        ordersPreviousPageButton.Size = new Size(28, 26);
        ordersPreviousPageButton.TabIndex = 4;
        ordersPreviousPageButton.Text = "‹";
        ordersPreviousPageButton.UseVisualStyleBackColor = false;
        // 
        // ordersPageTextBox
        // 
        ordersPageTextBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        ordersPageTextBox.BackColor = Color.White;
        ordersPageTextBox.BorderStyle = BorderStyle.FixedSingle;
        ordersPageTextBox.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        ordersPageTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        ordersPageTextBox.Location = new Point(2372, 896);
        ordersPageTextBox.Name = "ordersPageTextBox";
        ordersPageTextBox.Size = new Size(40, 23);
        ordersPageTextBox.TabIndex = 5;
        ordersPageTextBox.Text = "1";
        ordersPageTextBox.TextAlign = HorizontalAlignment.Center;
        // 
        // ordersNextPageButton
        // 
        ordersNextPageButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        ordersNextPageButton.BackColor = Color.White;
        ordersNextPageButton.Cursor = Cursors.Hand;
        ordersNextPageButton.FlatAppearance.BorderColor = Color.FromArgb(214, 219, 226);
        ordersNextPageButton.FlatStyle = FlatStyle.Flat;
        ordersNextPageButton.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        ordersNextPageButton.ForeColor = Color.FromArgb(98, 108, 124);
        ordersNextPageButton.Location = new Point(2418, 892);
        ordersNextPageButton.Name = "ordersNextPageButton";
        ordersNextPageButton.Size = new Size(28, 26);
        ordersNextPageButton.TabIndex = 6;
        ordersNextPageButton.Text = "›";
        ordersNextPageButton.UseVisualStyleBackColor = false;
        // 
        // footerBar
        // 
        footerBar.BackColor = Color.FromArgb(248, 250, 253);
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
        statusCell.Size = new Size(200, 100);
        statusCell.TabIndex = 0;
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
        statusLabel.Size = new Size(187, 100);
        statusLabel.TabIndex = 0;
        statusLabel.Text = "●  Consulta pronta";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // statusCellDivider
        // 
        statusCellDivider.BackColor = Color.FromArgb(214, 219, 226);
        statusCellDivider.Dock = DockStyle.Right;
        statusCellDivider.Location = new Point(199, 0);
        statusCellDivider.Margin = new Padding(0);
        statusCellDivider.Name = "statusCellDivider";
        statusCellDivider.Size = new Size(1, 100);
        statusCellDivider.TabIndex = 1;
        // 
        // ConsultaIntegracaoSapForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        ClientSize = new Size(1366, 720);
        Controls.Add(rootLayout);
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1180, 680);
        Name = "ConsultaIntegracaoSapForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Integracao SAP";
        WindowState = FormWindowState.Maximized;
        customTitleBarPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).EndInit();
        headerTitleIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).EndInit();
        sapStatusPanel.ResumeLayout(false);
        rootLayout.ResumeLayout(false);
        contentLayout.ResumeLayout(false);
        filtersCard.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)searchIconPictureBox).EndInit();
        searchInputPanel.ResumeLayout(false);
        searchInputPanel.PerformLayout();
        linhaInputPanel.ResumeLayout(false);
        turnoInputPanel.ResumeLayout(false);
        ordersCard.ResumeLayout(false);
        ordersCard.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)ordersTitleIconPictureBox).EndInit();
        ((System.ComponentModel.ISupportInitialize)ordersDataGridView).EndInit();
        footerBar.ResumeLayout(false);
        footerBarLayout.ResumeLayout(false);
        cellUser.ResumeLayout(false);
        cellTerminal.ResumeLayout(false);
        cellEmpresa.ResumeLayout(false);
        cellBanco.ResumeLayout(false);
        cellHora.ResumeLayout(false);
        cellData.ResumeLayout(false);
        statusCell.ResumeLayout(false);
        ResumeLayout(false);
    }

    private Label menuHeaderLabel;
}





