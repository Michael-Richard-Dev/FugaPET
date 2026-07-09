using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

partial class BalancaForm
{
    private System.ComponentModel.IContainer components;
    private TableLayoutPanel rootLayout;
    private Panel customTitleBarPanel;
    private Label menuHeaderLabel;
    private PictureBox companyLogoPictureBox;
    private Label headerDividerLabel;
    private RoundedPanel headerTitleIconPanel;
    private PictureBox headerTitleIconPictureBox;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private Label minimizeWindowLabel;
    private Label maximizeWindowLabel;
    private Label closeWindowLabel;
    private TableLayoutPanel contentLayout;
    private RoundedPanel balancasCard;
    private Label balancasTitleLabel;
    private RoundedPanel novoBalancaButtonPanel;
    private Label novoBalancaIconLabel;
    private Label novoBalancaTextLabel;
    private RoundedPanel searchPanel;
    private Label searchIconLabel;
    private TextBox searchTextBox;
    private Panel listaHeaderPanel;
    private Label listaNomeHeaderLabel;
    private Label listaDetalhesHeaderLabel;
    private Label listaSituacaoHeaderLabel;
    private FlowLayoutPanel balancasFlowPanel;
    private Label quantidadeBalancasLabel;
    private RoundedPanel dadosCard;
    private Panel dadosPanel;
    private Label dadosTitleLabel;
    private Label dadosTitleIconLabel;
    private Label identificacaoSectionLabel;
    private TableLayoutPanel identificacaoTable;
    private Panel nomeCampoPanel;
    private Label nomeBalancaLabel;
    private RoundedPanel nomeBalancaInputPanel;
    private TextBox nomeBalancaTextBox;
    private Panel setorCampoPanel;
    private Label setorLabel;
    private RoundedPanel setorInputPanel;
    private ComboBox setorComboBox;
    private Panel identificacaoLocalCampoPanel;
    private Label identificacaoLocalLabel;
    private RoundedPanel identificacaoLocalInputPanel;
    private TextBox identificacaoLocalTextBox;
    private Panel situacaoCampoPanel;
    private Label situacaoLabel;
    private RoundedPanel situacaoInputPanel;
    private TextBox situacaoTextBox;
    private Label conexaoSectionLabel;
    private TableLayoutPanel conexaoTable;
    private Panel tipoConexaoCampoPanel;
    private Label tipoConexaoLabel;
    private RoundedPanel tipoConexaoInputPanel;
    private ComboBox tipoConexaoComboBox;
    private Panel tcpPanel;
    private TableLayoutPanel tcpTable;
    private Panel enderecoIpCampoPanel;
    private Label enderecoIpLabel;
    private RoundedPanel enderecoIpInputPanel;
    private TextBox enderecoIpTextBox;
    private Panel portaTcpCampoPanel;
    private Label portaTcpLabel;
    private RoundedPanel portaTcpInputPanel;
    private TextBox portaTcpTextBox;
    private Panel portaSerialPanel;
    private Label portaSerialLabel;
    private RoundedPanel portaSerialInputPanel;
    private TextBox portaSerialTextBox;
    private Label parametrosSeriaisSectionLabel;
    private Panel serialPanel;
    private TableLayoutPanel serialTable;
    private Panel baudRateCampoPanel;
    private Label baudRateLabel;
    private RoundedPanel baudRateInputPanel;
    private TextBox baudRateTextBox;
    private Panel dataBitsCampoPanel;
    private Label dataBitsLabel;
    private RoundedPanel dataBitsInputPanel;
    private TextBox dataBitsTextBox;
    private Panel paridadeCampoPanel;
    private Label paridadeLabel;
    private RoundedPanel paridadeInputPanel;
    private ComboBox paridadeComboBox;
    private Panel stopBitsCampoPanel;
    private Label stopBitsLabel;
    private RoundedPanel stopBitsInputPanel;
    private ComboBox stopBitsComboBox;
    private Panel flowControlCampoPanel;
    private Label flowControlLabel;
    private RoundedPanel flowControlInputPanel;
    private ComboBox flowControlComboBox;
    private Panel protocoloCampoPanel;
    private Label protocoloLabel;
    private RoundedPanel protocoloInputPanel;
    private TextBox protocoloTextBox;
    private Label manualWarningLabel;
    private Label observacaoSectionLabel;
    private RoundedPanel observacaoInputPanel;
    private TextBox observacaoTextBox;
    private RoundedPanel resumoCard;
    private Label resumoTitleLabel;
    private Label resumoNomeLabel;
    private Label resumoNomeValueLabel;
    private Label resumoSetorLabel;
    private Label resumoSetorValueLabel;
    private Label resumoConexaoLabel;
    private Label resumoConexaoValueLabel;
    private Label resumoSituacaoLabel;
    private Label resumoSituacaoValueLabel;
    private Label resumoTitleIconLabel;
    private Label resumoNomeIconLabel;
    private Label resumoSetorIconLabel;
    private Label resumoConexaoIconLabel;
    private Label resumoSituacaoIconLabel;
    private Panel resumoDivisorTitulo;
    private Panel resumoDivisorNome;
    private Panel resumoDivisorSetor;
    private Panel resumoDivisorConexao;
    private Panel resumoDivisorRodape;
    private Label resumoDataLabel;
    private Button salvarButton;
    private Button salvarAlteracoesButton;
    private Button alterarSituacaoButton;
    private Panel footerBar;
    private TableLayoutPanel footerBarLayout;
    private Panel cellUser;
    private Label cellUserText;
    private Panel cellUserDivider;
    private Panel cellTerminal;
    private Label cellTerminalText;
    private Panel cellTerminalDivider;
    private Panel cellEmpresa;
    private Label cellEmpresaText;
    private Panel cellEmpresaDivider;
    private Panel cellBanco;
    private Label cellBancoText;
    private Panel cellBancoDivider;
    private Panel cellHora;
    private Label cellHoraText;
    private Panel cellHoraDivider;
    private Panel cellData;
    private Label cellDataText;
    private Label cellUserIcon;
    private Label cellTerminalIcon;
    private Label cellEmpresaIcon;
    private Label cellBancoIcon;
    private Label cellHoraIcon;
    private Label cellDataIcon;

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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BalancaForm));
        rootLayout = new TableLayoutPanel();
        customTitleBarPanel = new Panel();
        menuHeaderLabel = new Label();
        companyLogoPictureBox = new PictureBox();
        headerDividerLabel = new Label();
        headerTitleIconPanel = new RoundedPanel();
        headerTitleIconPictureBox = new PictureBox();
        headerTitleLabel = new Label();
        headerSubtitleLabel = new Label();
        minimizeWindowLabel = new Label();
        maximizeWindowLabel = new Label();
        closeWindowLabel = new Label();
        contentLayout = new TableLayoutPanel();
        balancasCard = new RoundedPanel();
        balancasTitleLabel = new Label();
        novoBalancaButtonPanel = new RoundedPanel();
        novoBalancaIconLabel = new Label();
        novoBalancaTextLabel = new Label();
        searchPanel = new RoundedPanel();
        searchIconLabel = new Label();
        searchTextBox = new TextBox();
        listaHeaderPanel = new Panel();
        listaNomeHeaderLabel = new Label();
        listaDetalhesHeaderLabel = new Label();
        listaSituacaoHeaderLabel = new Label();
        balancasFlowPanel = new FlowLayoutPanel();
        quantidadeBalancasLabel = new Label();
        dadosCard = new RoundedPanel();
        dadosPanel = new Panel();
        dadosTitleIconLabel = new Label();
        dadosTitleLabel = new Label();
        identificacaoSectionLabel = new Label();
        identificacaoTable = new TableLayoutPanel();
        nomeCampoPanel = new Panel();
        nomeBalancaInputPanel = new RoundedPanel();
        nomeBalancaTextBox = new TextBox();
        nomeBalancaLabel = new Label();
        setorCampoPanel = new Panel();
        setorInputPanel = new RoundedPanel();
        setorComboBox = new ComboBox();
        setorLabel = new Label();
        identificacaoLocalCampoPanel = new Panel();
        identificacaoLocalInputPanel = new RoundedPanel();
        identificacaoLocalTextBox = new TextBox();
        identificacaoLocalLabel = new Label();
        situacaoCampoPanel = new Panel();
        situacaoInputPanel = new RoundedPanel();
        situacaoTextBox = new TextBox();
        situacaoLabel = new Label();
        conexaoSectionLabel = new Label();
        conexaoTable = new TableLayoutPanel();
        tipoConexaoCampoPanel = new Panel();
        tipoConexaoInputPanel = new RoundedPanel();
        tipoConexaoComboBox = new ComboBox();
        tipoConexaoLabel = new Label();
        tcpPanel = new Panel();
        tcpTable = new TableLayoutPanel();
        enderecoIpCampoPanel = new Panel();
        enderecoIpInputPanel = new RoundedPanel();
        enderecoIpTextBox = new TextBox();
        enderecoIpLabel = new Label();
        portaTcpCampoPanel = new Panel();
        portaTcpInputPanel = new RoundedPanel();
        portaTcpTextBox = new TextBox();
        portaTcpLabel = new Label();
        portaSerialPanel = new Panel();
        portaSerialInputPanel = new RoundedPanel();
        portaSerialTextBox = new TextBox();
        portaSerialLabel = new Label();
        parametrosSeriaisSectionLabel = new Label();
        serialPanel = new Panel();
        serialTable = new TableLayoutPanel();
        baudRateCampoPanel = new Panel();
        baudRateInputPanel = new RoundedPanel();
        baudRateTextBox = new TextBox();
        baudRateLabel = new Label();
        dataBitsCampoPanel = new Panel();
        dataBitsInputPanel = new RoundedPanel();
        dataBitsTextBox = new TextBox();
        dataBitsLabel = new Label();
        paridadeCampoPanel = new Panel();
        paridadeInputPanel = new RoundedPanel();
        paridadeComboBox = new ComboBox();
        paridadeLabel = new Label();
        stopBitsCampoPanel = new Panel();
        stopBitsInputPanel = new RoundedPanel();
        stopBitsComboBox = new ComboBox();
        stopBitsLabel = new Label();
        flowControlCampoPanel = new Panel();
        flowControlInputPanel = new RoundedPanel();
        flowControlComboBox = new ComboBox();
        flowControlLabel = new Label();
        protocoloCampoPanel = new Panel();
        protocoloInputPanel = new RoundedPanel();
        protocoloTextBox = new TextBox();
        protocoloLabel = new Label();
        manualWarningLabel = new Label();
        observacaoSectionLabel = new Label();
        observacaoInputPanel = new RoundedPanel();
        observacaoTextBox = new TextBox();
        resumoCard = new RoundedPanel();
        resumoTitleLabel = new Label();
        resumoTitleIconLabel = new Label();
        resumoDivisorTitulo = new Panel();
        resumoDivisorNome = new Panel();
        resumoDivisorSetor = new Panel();
        resumoDivisorConexao = new Panel();
        resumoNomeIconLabel = new Label();
        resumoSetorIconLabel = new Label();
        resumoConexaoIconLabel = new Label();
        resumoSituacaoIconLabel = new Label();
        resumoDivisorRodape = new Panel();
        resumoDataLabel = new Label();
        resumoNomeLabel = new Label();
        resumoNomeValueLabel = new Label();
        resumoSetorLabel = new Label();
        resumoSetorValueLabel = new Label();
        resumoConexaoLabel = new Label();
        resumoConexaoValueLabel = new Label();
        resumoSituacaoLabel = new Label();
        resumoSituacaoValueLabel = new Label();
        salvarButton = new Button();
        salvarAlteracoesButton = new Button();
        alterarSituacaoButton = new Button();
        footerBar = new Panel();
        footerBarLayout = new TableLayoutPanel();
        cellUser = new Panel();
        cellUserText = new Label();
        cellUserDivider = new Panel();
        cellUserIcon = new Label();
        cellTerminal = new Panel();
        cellTerminalText = new Label();
        cellTerminalDivider = new Panel();
        cellTerminalIcon = new Label();
        cellEmpresa = new Panel();
        cellEmpresaText = new Label();
        cellEmpresaDivider = new Panel();
        cellEmpresaIcon = new Label();
        cellBanco = new Panel();
        cellBancoText = new Label();
        cellBancoDivider = new Panel();
        cellBancoIcon = new Label();
        cellHora = new Panel();
        cellHoraText = new Label();
        cellHoraDivider = new Panel();
        cellHoraIcon = new Label();
        cellData = new Panel();
        cellDataText = new Label();
        cellDataIcon = new Label();
        rootLayout.SuspendLayout();
        customTitleBarPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).BeginInit();
        headerTitleIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).BeginInit();
        contentLayout.SuspendLayout();
        balancasCard.SuspendLayout();
        novoBalancaButtonPanel.SuspendLayout();
        searchPanel.SuspendLayout();
        listaHeaderPanel.SuspendLayout();
        dadosCard.SuspendLayout();
        dadosPanel.SuspendLayout();
        identificacaoTable.SuspendLayout();
        nomeCampoPanel.SuspendLayout();
        nomeBalancaInputPanel.SuspendLayout();
        setorCampoPanel.SuspendLayout();
        setorInputPanel.SuspendLayout();
        identificacaoLocalCampoPanel.SuspendLayout();
        identificacaoLocalInputPanel.SuspendLayout();
        situacaoCampoPanel.SuspendLayout();
        situacaoInputPanel.SuspendLayout();
        conexaoTable.SuspendLayout();
        tipoConexaoCampoPanel.SuspendLayout();
        tipoConexaoInputPanel.SuspendLayout();
        tcpPanel.SuspendLayout();
        tcpTable.SuspendLayout();
        enderecoIpCampoPanel.SuspendLayout();
        enderecoIpInputPanel.SuspendLayout();
        portaTcpCampoPanel.SuspendLayout();
        portaTcpInputPanel.SuspendLayout();
        portaSerialPanel.SuspendLayout();
        portaSerialInputPanel.SuspendLayout();
        serialPanel.SuspendLayout();
        serialTable.SuspendLayout();
        baudRateCampoPanel.SuspendLayout();
        baudRateInputPanel.SuspendLayout();
        dataBitsCampoPanel.SuspendLayout();
        dataBitsInputPanel.SuspendLayout();
        paridadeCampoPanel.SuspendLayout();
        paridadeInputPanel.SuspendLayout();
        stopBitsCampoPanel.SuspendLayout();
        stopBitsInputPanel.SuspendLayout();
        flowControlCampoPanel.SuspendLayout();
        flowControlInputPanel.SuspendLayout();
        protocoloCampoPanel.SuspendLayout();
        protocoloInputPanel.SuspendLayout();
        observacaoInputPanel.SuspendLayout();
        resumoCard.SuspendLayout();
        footerBar.SuspendLayout();
        footerBarLayout.SuspendLayout();
        cellUser.SuspendLayout();
        cellTerminal.SuspendLayout();
        cellEmpresa.SuspendLayout();
        cellBanco.SuspendLayout();
        cellHora.SuspendLayout();
        cellData.SuspendLayout();
        SuspendLayout();
        // 
        // rootLayout
        // 
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
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        rootLayout.Size = new Size(1366, 720);
        rootLayout.TabIndex = 0;
        // 
        // customTitleBarPanel
        // 
        customTitleBarPanel.BackColor = Color.FromArgb(17, 24, 39);
        customTitleBarPanel.Controls.Add(menuHeaderLabel);
        customTitleBarPanel.Controls.Add(companyLogoPictureBox);
        customTitleBarPanel.Controls.Add(headerDividerLabel);
        customTitleBarPanel.Controls.Add(headerTitleIconPanel);
        customTitleBarPanel.Controls.Add(headerTitleLabel);
        customTitleBarPanel.Controls.Add(headerSubtitleLabel);
        customTitleBarPanel.Controls.Add(minimizeWindowLabel);
        customTitleBarPanel.Controls.Add(maximizeWindowLabel);
        customTitleBarPanel.Controls.Add(closeWindowLabel);
        customTitleBarPanel.Dock = DockStyle.Fill;
        customTitleBarPanel.Location = new Point(0, 0);
        customTitleBarPanel.Margin = new Padding(0);
        customTitleBarPanel.Name = "customTitleBarPanel";
        customTitleBarPanel.Size = new Size(1366, 52);
        customTitleBarPanel.TabIndex = 0;
        // 
        // menuHeaderLabel
        // 
        menuHeaderLabel.Cursor = Cursors.Hand;
        menuHeaderLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        menuHeaderLabel.ForeColor = Color.White;
        menuHeaderLabel.Location = new Point(18, 8);
        menuHeaderLabel.Name = "menuHeaderLabel";
        menuHeaderLabel.Size = new Size(36, 36);
        menuHeaderLabel.TabIndex = 0;
        menuHeaderLabel.Text = "";
        menuHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // companyLogoPictureBox
        // 
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
        headerDividerLabel.TabIndex = 2;
        // 
        // headerTitleIconPanel
        // 
        headerTitleIconPanel.BackColor = Color.Transparent;
        headerTitleIconPanel.BorderColor = Color.Transparent;
        headerTitleIconPanel.BorderRadius = 0;
        headerTitleIconPanel.Controls.Add(headerTitleIconPictureBox);
        headerTitleIconPanel.FillColor = Color.Transparent;
        headerTitleIconPanel.Location = new Point(232, 10);
        headerTitleIconPanel.Name = "headerTitleIconPanel";
        headerTitleIconPanel.ShadowBlur = 0;
        headerTitleIconPanel.ShadowOffsetY = 0;
        headerTitleIconPanel.Size = new Size(32, 32);
        headerTitleIconPanel.TabIndex = 3;
        // 
        // headerTitleIconPictureBox
        // 
        headerTitleIconPictureBox.BackColor = Color.Transparent;
        headerTitleIconPictureBox.Image = (Image)resources.GetObject("headerTitleIconPictureBox.Image");
        headerTitleIconPictureBox.Location = new Point(4, 4);
        headerTitleIconPictureBox.Name = "headerTitleIconPictureBox";
        headerTitleIconPictureBox.Size = new Size(24, 24);
        headerTitleIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerTitleIconPictureBox.TabIndex = 0;
        headerTitleIconPictureBox.TabStop = false;
        // 
        // headerTitleLabel
        // 
        headerTitleLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(278, 6);
        headerTitleLabel.Name = "headerTitleLabel";
        headerTitleLabel.Size = new Size(310, 23);
        headerTitleLabel.TabIndex = 4;
        headerTitleLabel.Text = "Balanças";
        // 
        // headerSubtitleLabel
        // 
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F);
        headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228);
        headerSubtitleLabel.Location = new Point(279, 29);
        headerSubtitleLabel.Name = "headerSubtitleLabel";
        headerSubtitleLabel.Size = new Size(560, 17);
        headerSubtitleLabel.TabIndex = 5;
        headerSubtitleLabel.Text = "Cadastro e manutenção técnica de balanças";
        // 
        // minimizeWindowLabel
        // 
        minimizeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        minimizeWindowLabel.BackColor = Color.Transparent;
        minimizeWindowLabel.Cursor = Cursors.Hand;
        minimizeWindowLabel.Font = new Font("Cascadia Code", 12.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        minimizeWindowLabel.ForeColor = Color.White;
        minimizeWindowLabel.Location = new Point(1218, 0);
        minimizeWindowLabel.Name = "minimizeWindowLabel";
        minimizeWindowLabel.Size = new Size(48, 52);
        minimizeWindowLabel.TabIndex = 6;
        minimizeWindowLabel.Text = "-";
        minimizeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // maximizeWindowLabel
        // 
        maximizeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        maximizeWindowLabel.BackColor = Color.Transparent;
        maximizeWindowLabel.Cursor = Cursors.Hand;
        maximizeWindowLabel.Font = new Font("Cascadia Code", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        maximizeWindowLabel.ForeColor = Color.White;
        maximizeWindowLabel.Location = new Point(1266, 0);
        maximizeWindowLabel.Name = "maximizeWindowLabel";
        maximizeWindowLabel.Size = new Size(48, 52);
        maximizeWindowLabel.TabIndex = 7;
        maximizeWindowLabel.Text = "□";
        maximizeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // closeWindowLabel
        // 
        closeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeWindowLabel.BackColor = Color.Transparent;
        closeWindowLabel.Cursor = Cursors.Hand;
        closeWindowLabel.Font = new Font("Cascadia Code ExtraLight", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        closeWindowLabel.ForeColor = Color.White;
        closeWindowLabel.Location = new Point(1314, 0);
        closeWindowLabel.Name = "closeWindowLabel";
        closeWindowLabel.Size = new Size(48, 52);
        closeWindowLabel.TabIndex = 8;
        closeWindowLabel.Text = "×";
        closeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // contentLayout
        // 
        contentLayout.BackColor = Color.FromArgb(247, 248, 250);
        contentLayout.ColumnCount = 3;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
        contentLayout.Controls.Add(balancasCard, 0, 0);
        contentLayout.Controls.Add(dadosCard, 1, 0);
        contentLayout.Controls.Add(resumoCard, 2, 0);
        contentLayout.Dock = DockStyle.Fill;
        contentLayout.Location = new Point(3, 55);
        contentLayout.Name = "contentLayout";
        contentLayout.Padding = new Padding(20, 16, 20, 16);
        contentLayout.RowCount = 1;
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        contentLayout.Size = new Size(1360, 622);
        contentLayout.TabIndex = 1;
        // 
        // balancasCard
        // 
        balancasCard.BackColor = Color.Transparent;
        balancasCard.BorderColor = Color.FromArgb(226, 232, 240);
        balancasCard.BorderRadius = 9;
        balancasCard.Controls.Add(balancasTitleLabel);
        balancasCard.Controls.Add(novoBalancaButtonPanel);
        balancasCard.Controls.Add(searchPanel);
        balancasCard.Controls.Add(listaHeaderPanel);
        balancasCard.Controls.Add(balancasFlowPanel);
        balancasCard.Controls.Add(quantidadeBalancasLabel);
        balancasCard.Dock = DockStyle.Fill;
        balancasCard.Location = new Point(20, 16);
        balancasCard.Margin = new Padding(0, 0, 12, 0);
        balancasCard.Name = "balancasCard";
        balancasCard.ShadowBlur = 0;
        balancasCard.ShadowOffsetY = 0;
        balancasCard.Size = new Size(397, 590);
        balancasCard.TabIndex = 0;
        // 
        // balancasTitleLabel
        // 
        balancasTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        balancasTitleLabel.ForeColor = Color.FromArgb(15, 23, 42);
        balancasTitleLabel.Location = new Point(16, 18);
        balancasTitleLabel.Name = "balancasTitleLabel";
        balancasTitleLabel.Size = new Size(207, 28);
        balancasTitleLabel.TabIndex = 0;
        balancasTitleLabel.Text = "✓  Balanças Cadastradas";
        balancasTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // novoBalancaButtonPanel
        // 
        novoBalancaButtonPanel.BackColor = Color.Transparent;
        novoBalancaButtonPanel.BorderColor = Color.FromArgb(203, 213, 225);
        novoBalancaButtonPanel.BorderRadius = 4;
        novoBalancaButtonPanel.Controls.Add(novoBalancaIconLabel);
        novoBalancaButtonPanel.Controls.Add(novoBalancaTextLabel);
        novoBalancaButtonPanel.Location = new Point(234, 16);
        novoBalancaButtonPanel.Name = "novoBalancaButtonPanel";
        novoBalancaButtonPanel.ShadowBlur = 0;
        novoBalancaButtonPanel.ShadowOffsetY = 0;
        novoBalancaButtonPanel.Size = new Size(160, 32);
        novoBalancaButtonPanel.TabIndex = 1;
        // 
        // novoBalancaIconLabel
        // 
        novoBalancaIconLabel.BackColor = Color.Transparent;
        novoBalancaIconLabel.Font = new Font("Segoe MDL2 Assets", 10F);
        novoBalancaIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        novoBalancaIconLabel.Location = new Point(12, 5);
        novoBalancaIconLabel.Name = "novoBalancaIconLabel";
        novoBalancaIconLabel.Size = new Size(20, 22);
        novoBalancaIconLabel.TabIndex = 0;
        novoBalancaIconLabel.Text = "";
        novoBalancaIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // novoBalancaTextLabel
        // 
        novoBalancaTextLabel.BackColor = Color.Transparent;
        novoBalancaTextLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        novoBalancaTextLabel.ForeColor = Color.FromArgb(15, 23, 42);
        novoBalancaTextLabel.Location = new Point(36, 5);
        novoBalancaTextLabel.Name = "novoBalancaTextLabel";
        novoBalancaTextLabel.Size = new Size(118, 22);
        novoBalancaTextLabel.TabIndex = 1;
        novoBalancaTextLabel.Text = "Nova Balança";
        novoBalancaTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // searchPanel
        // 
        searchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchPanel.BackColor = Color.Transparent;
        searchPanel.BorderColor = Color.FromArgb(214, 219, 226);
        searchPanel.BorderRadius = 7;
        searchPanel.Controls.Add(searchIconLabel);
        searchPanel.Controls.Add(searchTextBox);
        searchPanel.Location = new Point(16, 60);
        searchPanel.Name = "searchPanel";
        searchPanel.ShadowBlur = 0;
        searchPanel.ShadowOffsetY = 0;
        searchPanel.Size = new Size(576, 34);
        searchPanel.TabIndex = 2;
        // 
        // searchIconLabel
        // 
        searchIconLabel.Font = new Font("Segoe MDL2 Assets", 11F);
        searchIconLabel.ForeColor = Color.FromArgb(107, 114, 128);
        searchIconLabel.Location = new Point(8, 8);
        searchIconLabel.Name = "searchIconLabel";
        searchIconLabel.Size = new Size(22, 24);
        searchIconLabel.TabIndex = 0;
        searchIconLabel.Text = "";
        searchIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // searchTextBox
        // 
        searchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchTextBox.BorderStyle = BorderStyle.None;
        searchTextBox.Font = new Font("Segoe UI", 9F);
        searchTextBox.Location = new Point(36, 8);
        searchTextBox.MaxLength = 120;
        searchTextBox.Name = "searchTextBox";
        searchTextBox.PlaceholderText = "Buscar balança...";
        searchTextBox.Size = new Size(537, 16);
        searchTextBox.TabIndex = 1;
        // 
        // listaHeaderPanel
        // 
        listaHeaderPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        listaHeaderPanel.Controls.Add(listaNomeHeaderLabel);
        listaHeaderPanel.Controls.Add(listaDetalhesHeaderLabel);
        listaHeaderPanel.Controls.Add(listaSituacaoHeaderLabel);
        listaHeaderPanel.Location = new Point(16, 110);
        listaHeaderPanel.Name = "listaHeaderPanel";
        listaHeaderPanel.Size = new Size(575, 28);
        listaHeaderPanel.TabIndex = 3;
        // 
        // listaNomeHeaderLabel
        // 
        listaNomeHeaderLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        listaNomeHeaderLabel.Location = new Point(14, 4);
        listaNomeHeaderLabel.Name = "listaNomeHeaderLabel";
        listaNomeHeaderLabel.Size = new Size(110, 20);
        listaNomeHeaderLabel.TabIndex = 0;
        listaNomeHeaderLabel.Text = "Nome";
        // 
        // listaDetalhesHeaderLabel
        // 
        listaDetalhesHeaderLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        listaDetalhesHeaderLabel.Location = new Point(135, 4);
        listaDetalhesHeaderLabel.Name = "listaDetalhesHeaderLabel";
        listaDetalhesHeaderLabel.Size = new Size(130, 20);
        listaDetalhesHeaderLabel.TabIndex = 1;
        listaDetalhesHeaderLabel.Text = "Setor / Conexão";
        // 
        // listaSituacaoHeaderLabel
        // 
        listaSituacaoHeaderLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        listaSituacaoHeaderLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        listaSituacaoHeaderLabel.Location = new Point(497, 4);
        listaSituacaoHeaderLabel.Name = "listaSituacaoHeaderLabel";
        listaSituacaoHeaderLabel.Size = new Size(64, 20);
        listaSituacaoHeaderLabel.TabIndex = 2;
        listaSituacaoHeaderLabel.Text = "Situação";
        listaSituacaoHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // balancasFlowPanel
        // 
        balancasFlowPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        balancasFlowPanel.AutoScroll = true;
        balancasFlowPanel.FlowDirection = FlowDirection.TopDown;
        balancasFlowPanel.Location = new Point(16, 140);
        balancasFlowPanel.Name = "balancasFlowPanel";
        balancasFlowPanel.Padding = new Padding(4);
        balancasFlowPanel.Size = new Size(575, 930);
        balancasFlowPanel.TabIndex = 4;
        balancasFlowPanel.WrapContents = false;
        // 
        // quantidadeBalancasLabel
        // 
        quantidadeBalancasLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        quantidadeBalancasLabel.Font = new Font("Segoe UI", 8F);
        quantidadeBalancasLabel.ForeColor = Color.FromArgb(71, 85, 105);
        quantidadeBalancasLabel.Location = new Point(20, 1076);
        quantidadeBalancasLabel.Name = "quantidadeBalancasLabel";
        quantidadeBalancasLabel.Size = new Size(567, 22);
        quantidadeBalancasLabel.TabIndex = 5;
        quantidadeBalancasLabel.Text = "Exibindo 0 de 0 balanças";
        // 
        // dadosCard
        // 
        dadosCard.BackColor = Color.Transparent;
        dadosCard.BorderColor = Color.FromArgb(226, 232, 240);
        dadosCard.BorderRadius = 9;
        dadosCard.Controls.Add(dadosPanel);
        dadosCard.Dock = DockStyle.Fill;
        dadosCard.Location = new Point(429, 16);
        dadosCard.Margin = new Padding(0, 0, 12, 0);
        dadosCard.Name = "dadosCard";
        dadosCard.ShadowBlur = 0;
        dadosCard.ShadowOffsetY = 0;
        dadosCard.Size = new Size(582, 590);
        dadosCard.TabIndex = 1;
        // 
        // dadosPanel
        // 
        dadosPanel.AutoScroll = true;
        dadosPanel.Controls.Add(dadosTitleIconLabel);
        dadosPanel.Controls.Add(dadosTitleLabel);
        dadosPanel.Controls.Add(identificacaoSectionLabel);
        dadosPanel.Controls.Add(identificacaoTable);
        dadosPanel.Controls.Add(conexaoSectionLabel);
        dadosPanel.Controls.Add(conexaoTable);
        dadosPanel.Controls.Add(parametrosSeriaisSectionLabel);
        dadosPanel.Controls.Add(serialPanel);
        dadosPanel.Controls.Add(manualWarningLabel);
        dadosPanel.Controls.Add(observacaoSectionLabel);
        dadosPanel.Controls.Add(observacaoInputPanel);
        dadosPanel.Dock = DockStyle.Fill;
        dadosPanel.Location = new Point(0, 0);
        dadosPanel.Name = "dadosPanel";
        dadosPanel.Padding = new Padding(22, 14, 22, 18);
        dadosPanel.Size = new Size(582, 590);
        dadosPanel.TabIndex = 0;
        // 
        // dadosTitleIconLabel
        // 
        dadosTitleIconLabel.Font = new Font("Segoe MDL2 Assets", 14F);
        dadosTitleIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        dadosTitleIconLabel.Location = new Point(20, 16);
        dadosTitleIconLabel.Name = "dadosTitleIconLabel";
        dadosTitleIconLabel.Size = new Size(28, 28);
        dadosTitleIconLabel.TabIndex = 0;
        dadosTitleIconLabel.Text = "";
        dadosTitleIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // dadosTitleLabel
        // 
        dadosTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dadosTitleLabel.ForeColor = Color.FromArgb(15, 23, 42);
        dadosTitleLabel.Location = new Point(52, 18);
        dadosTitleLabel.Name = "dadosTitleLabel";
        dadosTitleLabel.Size = new Size(480, 26);
        dadosTitleLabel.TabIndex = 0;
        dadosTitleLabel.Text = "Dados da Balança";
        dadosTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // identificacaoSectionLabel
        // 
        identificacaoSectionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        identificacaoSectionLabel.ForeColor = Color.FromArgb(220, 38, 38);
        identificacaoSectionLabel.Location = new Point(24, 64);
        identificacaoSectionLabel.Name = "identificacaoSectionLabel";
        identificacaoSectionLabel.Size = new Size(520, 22);
        identificacaoSectionLabel.TabIndex = 1;
        identificacaoSectionLabel.Text = "IDENTIFICAÇÃO";
        // 
        // identificacaoTable
        // 
        identificacaoTable.ColumnCount = 2;
        identificacaoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        identificacaoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        identificacaoTable.Controls.Add(nomeCampoPanel, 0, 0);
        identificacaoTable.Controls.Add(setorCampoPanel, 1, 0);
        identificacaoTable.Controls.Add(identificacaoLocalCampoPanel, 0, 1);
        identificacaoTable.Controls.Add(situacaoCampoPanel, 1, 1);
        identificacaoTable.Location = new Point(20, 88);
        identificacaoTable.Name = "identificacaoTable";
        identificacaoTable.RowCount = 2;
        identificacaoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        identificacaoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        identificacaoTable.Size = new Size(540, 124);
        identificacaoTable.TabIndex = 2;
        // 
        // nomeCampoPanel
        // 
        nomeCampoPanel.Controls.Add(nomeBalancaInputPanel);
        nomeCampoPanel.Controls.Add(nomeBalancaLabel);
        nomeCampoPanel.Dock = DockStyle.Fill;
        nomeCampoPanel.Location = new Point(0, 0);
        nomeCampoPanel.Margin = new Padding(0);
        nomeCampoPanel.Name = "nomeCampoPanel";
        nomeCampoPanel.Padding = new Padding(5, 2, 5, 4);
        nomeCampoPanel.Size = new Size(270, 62);
        nomeCampoPanel.TabIndex = 0;
        // 
        // nomeBalancaInputPanel
        // 
        nomeBalancaInputPanel.BackColor = Color.Transparent;
        nomeBalancaInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        nomeBalancaInputPanel.BorderRadius = 5;
        nomeBalancaInputPanel.Controls.Add(nomeBalancaTextBox);
        nomeBalancaInputPanel.Dock = DockStyle.Fill;
        nomeBalancaInputPanel.Location = new Point(5, 22);
        nomeBalancaInputPanel.Name = "nomeBalancaInputPanel";
        nomeBalancaInputPanel.Padding = new Padding(10, 7, 8, 7);
        nomeBalancaInputPanel.ShadowBlur = 0;
        nomeBalancaInputPanel.ShadowOffsetY = 0;
        nomeBalancaInputPanel.Size = new Size(260, 36);
        nomeBalancaInputPanel.TabIndex = 0;
        // 
        // nomeBalancaTextBox
        // 
        nomeBalancaTextBox.BackColor = Color.White;
        nomeBalancaTextBox.BorderStyle = BorderStyle.None;
        nomeBalancaTextBox.Dock = DockStyle.Fill;
        nomeBalancaTextBox.Font = new Font("Segoe UI", 9F);
        nomeBalancaTextBox.Location = new Point(10, 7);
        nomeBalancaTextBox.MaxLength = 80;
        nomeBalancaTextBox.Name = "nomeBalancaTextBox";
        nomeBalancaTextBox.Size = new Size(242, 16);
        nomeBalancaTextBox.TabIndex = 0;
        // 
        // nomeBalancaLabel
        // 
        nomeBalancaLabel.Dock = DockStyle.Top;
        nomeBalancaLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        nomeBalancaLabel.Location = new Point(5, 2);
        nomeBalancaLabel.Name = "nomeBalancaLabel";
        nomeBalancaLabel.Size = new Size(260, 20);
        nomeBalancaLabel.TabIndex = 1;
        nomeBalancaLabel.Text = "Nome da Balança *";
        // 
        // setorCampoPanel
        // 
        setorCampoPanel.Controls.Add(setorInputPanel);
        setorCampoPanel.Controls.Add(setorLabel);
        setorCampoPanel.Dock = DockStyle.Fill;
        setorCampoPanel.Location = new Point(270, 0);
        setorCampoPanel.Margin = new Padding(0);
        setorCampoPanel.Name = "setorCampoPanel";
        setorCampoPanel.Padding = new Padding(5, 2, 5, 4);
        setorCampoPanel.Size = new Size(270, 62);
        setorCampoPanel.TabIndex = 1;
        // 
        // setorInputPanel
        // 
        setorInputPanel.BackColor = Color.Transparent;
        setorInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        setorInputPanel.BorderRadius = 5;
        setorInputPanel.Controls.Add(setorComboBox);
        setorInputPanel.Dock = DockStyle.Fill;
        setorInputPanel.Location = new Point(5, 22);
        setorInputPanel.Name = "setorInputPanel";
        setorInputPanel.Padding = new Padding(10, 5, 8, 5);
        setorInputPanel.ShadowBlur = 0;
        setorInputPanel.ShadowOffsetY = 0;
        setorInputPanel.Size = new Size(260, 36);
        setorInputPanel.TabIndex = 0;
        // 
        // setorComboBox
        // 
        setorComboBox.BackColor = Color.White;
        setorComboBox.Dock = DockStyle.Top;
        setorComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        setorComboBox.FlatStyle = FlatStyle.Flat;
        setorComboBox.Font = new Font("Segoe UI", 9F);
        setorComboBox.IntegralHeight = false;
        setorComboBox.Location = new Point(10, 7);
        setorComboBox.Name = "setorComboBox";
        setorComboBox.Size = new Size(242, 23);
        setorComboBox.TabIndex = 0;
        // 
        // setorLabel
        // 
        setorLabel.Dock = DockStyle.Top;
        setorLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        setorLabel.Location = new Point(5, 2);
        setorLabel.Name = "setorLabel";
        setorLabel.Size = new Size(260, 20);
        setorLabel.TabIndex = 1;
        setorLabel.Text = "Setor *";
        // 
        // identificacaoLocalCampoPanel
        // 
        identificacaoLocalCampoPanel.Controls.Add(identificacaoLocalInputPanel);
        identificacaoLocalCampoPanel.Controls.Add(identificacaoLocalLabel);
        identificacaoLocalCampoPanel.Dock = DockStyle.Fill;
        identificacaoLocalCampoPanel.Location = new Point(0, 62);
        identificacaoLocalCampoPanel.Margin = new Padding(0);
        identificacaoLocalCampoPanel.Name = "identificacaoLocalCampoPanel";
        identificacaoLocalCampoPanel.Padding = new Padding(5, 2, 5, 4);
        identificacaoLocalCampoPanel.Size = new Size(270, 62);
        identificacaoLocalCampoPanel.TabIndex = 2;
        // 
        // identificacaoLocalInputPanel
        // 
        identificacaoLocalInputPanel.BackColor = Color.Transparent;
        identificacaoLocalInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        identificacaoLocalInputPanel.BorderRadius = 5;
        identificacaoLocalInputPanel.Controls.Add(identificacaoLocalTextBox);
        identificacaoLocalInputPanel.Dock = DockStyle.Fill;
        identificacaoLocalInputPanel.Location = new Point(5, 22);
        identificacaoLocalInputPanel.Name = "identificacaoLocalInputPanel";
        identificacaoLocalInputPanel.Padding = new Padding(10, 7, 8, 7);
        identificacaoLocalInputPanel.ShadowBlur = 0;
        identificacaoLocalInputPanel.ShadowOffsetY = 0;
        identificacaoLocalInputPanel.Size = new Size(260, 36);
        identificacaoLocalInputPanel.TabIndex = 0;
        // 
        // identificacaoLocalTextBox
        // 
        identificacaoLocalTextBox.BackColor = Color.White;
        identificacaoLocalTextBox.BorderStyle = BorderStyle.None;
        identificacaoLocalTextBox.Dock = DockStyle.Fill;
        identificacaoLocalTextBox.Font = new Font("Segoe UI", 9F);
        identificacaoLocalTextBox.Location = new Point(10, 7);
        identificacaoLocalTextBox.MaxLength = 120;
        identificacaoLocalTextBox.Name = "identificacaoLocalTextBox";
        identificacaoLocalTextBox.Size = new Size(242, 16);
        identificacaoLocalTextBox.TabIndex = 0;
        // 
        // identificacaoLocalLabel
        // 
        identificacaoLocalLabel.Dock = DockStyle.Top;
        identificacaoLocalLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        identificacaoLocalLabel.Location = new Point(5, 2);
        identificacaoLocalLabel.Name = "identificacaoLocalLabel";
        identificacaoLocalLabel.Size = new Size(260, 20);
        identificacaoLocalLabel.TabIndex = 1;
        identificacaoLocalLabel.Text = "Identificação Local";
        // 
        // situacaoCampoPanel
        // 
        situacaoCampoPanel.Controls.Add(situacaoInputPanel);
        situacaoCampoPanel.Controls.Add(situacaoLabel);
        situacaoCampoPanel.Dock = DockStyle.Fill;
        situacaoCampoPanel.Location = new Point(270, 62);
        situacaoCampoPanel.Margin = new Padding(0);
        situacaoCampoPanel.Name = "situacaoCampoPanel";
        situacaoCampoPanel.Padding = new Padding(5, 2, 5, 4);
        situacaoCampoPanel.Size = new Size(270, 62);
        situacaoCampoPanel.TabIndex = 3;
        // 
        // situacaoInputPanel
        // 
        situacaoInputPanel.BackColor = Color.Transparent;
        situacaoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        situacaoInputPanel.BorderRadius = 5;
        situacaoInputPanel.Controls.Add(situacaoTextBox);
        situacaoInputPanel.Dock = DockStyle.Fill;
        situacaoInputPanel.Location = new Point(5, 22);
        situacaoInputPanel.Name = "situacaoInputPanel";
        situacaoInputPanel.Padding = new Padding(10, 7, 8, 7);
        situacaoInputPanel.ShadowBlur = 0;
        situacaoInputPanel.ShadowOffsetY = 0;
        situacaoInputPanel.Size = new Size(260, 36);
        situacaoInputPanel.TabIndex = 0;
        // 
        // situacaoTextBox
        // 
        situacaoTextBox.BackColor = Color.FromArgb(241, 245, 249);
        situacaoTextBox.BorderStyle = BorderStyle.None;
        situacaoTextBox.Dock = DockStyle.Fill;
        situacaoTextBox.Font = new Font("Segoe UI", 9F);
        situacaoTextBox.Location = new Point(10, 7);
        situacaoTextBox.MaxLength = 10;
        situacaoTextBox.Name = "situacaoTextBox";
        situacaoTextBox.ReadOnly = true;
        situacaoTextBox.Size = new Size(242, 16);
        situacaoTextBox.TabIndex = 0;
        situacaoTextBox.TabStop = false;
        situacaoTextBox.Text = "Ativo";
        // 
        // situacaoLabel
        // 
        situacaoLabel.Dock = DockStyle.Top;
        situacaoLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        situacaoLabel.Location = new Point(5, 2);
        situacaoLabel.Name = "situacaoLabel";
        situacaoLabel.Size = new Size(260, 20);
        situacaoLabel.TabIndex = 1;
        situacaoLabel.Text = "Situação";
        // 
        // conexaoSectionLabel
        // 
        conexaoSectionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        conexaoSectionLabel.ForeColor = Color.FromArgb(220, 38, 38);
        conexaoSectionLabel.Location = new Point(24, 222);
        conexaoSectionLabel.Name = "conexaoSectionLabel";
        conexaoSectionLabel.Size = new Size(520, 22);
        conexaoSectionLabel.TabIndex = 3;
        conexaoSectionLabel.Text = "CONEXÃO";
        // 
        // conexaoTable
        // 
        conexaoTable.ColumnCount = 1;
        conexaoTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        conexaoTable.Controls.Add(tipoConexaoCampoPanel, 0, 0);
        conexaoTable.Controls.Add(tcpPanel, 0, 1);
        conexaoTable.Controls.Add(portaSerialPanel, 0, 2);
        conexaoTable.Location = new Point(20, 246);
        conexaoTable.Name = "conexaoTable";
        conexaoTable.RowCount = 3;
        conexaoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        conexaoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        conexaoTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        conexaoTable.Size = new Size(540, 186);
        conexaoTable.TabIndex = 4;
        // 
        // tipoConexaoCampoPanel
        // 
        tipoConexaoCampoPanel.Controls.Add(tipoConexaoInputPanel);
        tipoConexaoCampoPanel.Controls.Add(tipoConexaoLabel);
        tipoConexaoCampoPanel.Dock = DockStyle.Fill;
        tipoConexaoCampoPanel.Location = new Point(0, 0);
        tipoConexaoCampoPanel.Margin = new Padding(0);
        tipoConexaoCampoPanel.Name = "tipoConexaoCampoPanel";
        tipoConexaoCampoPanel.Padding = new Padding(5, 2, 5, 4);
        tipoConexaoCampoPanel.Size = new Size(540, 62);
        tipoConexaoCampoPanel.TabIndex = 0;
        // 
        // tipoConexaoInputPanel
        // 
        tipoConexaoInputPanel.BackColor = Color.Transparent;
        tipoConexaoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        tipoConexaoInputPanel.BorderRadius = 5;
        tipoConexaoInputPanel.Controls.Add(tipoConexaoComboBox);
        tipoConexaoInputPanel.Dock = DockStyle.Fill;
        tipoConexaoInputPanel.Location = new Point(5, 22);
        tipoConexaoInputPanel.Name = "tipoConexaoInputPanel";
        tipoConexaoInputPanel.Padding = new Padding(10, 5, 8, 5);
        tipoConexaoInputPanel.ShadowBlur = 0;
        tipoConexaoInputPanel.ShadowOffsetY = 0;
        tipoConexaoInputPanel.Size = new Size(530, 36);
        tipoConexaoInputPanel.TabIndex = 0;
        // 
        // tipoConexaoComboBox
        // 
        tipoConexaoComboBox.BackColor = Color.White;
        tipoConexaoComboBox.Dock = DockStyle.Top;
        tipoConexaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        tipoConexaoComboBox.FlatStyle = FlatStyle.Flat;
        tipoConexaoComboBox.Font = new Font("Segoe UI", 9F);
        tipoConexaoComboBox.IntegralHeight = false;
        tipoConexaoComboBox.Location = new Point(10, 7);
        tipoConexaoComboBox.Name = "tipoConexaoComboBox";
        tipoConexaoComboBox.Size = new Size(512, 23);
        tipoConexaoComboBox.TabIndex = 0;
        // 
        // tipoConexaoLabel
        // 
        tipoConexaoLabel.Dock = DockStyle.Top;
        tipoConexaoLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        tipoConexaoLabel.Location = new Point(5, 2);
        tipoConexaoLabel.Name = "tipoConexaoLabel";
        tipoConexaoLabel.Size = new Size(530, 20);
        tipoConexaoLabel.TabIndex = 1;
        tipoConexaoLabel.Text = "Tipo de Conexão *";
        // 
        // tcpPanel
        // 
        tcpPanel.Controls.Add(tcpTable);
        tcpPanel.Dock = DockStyle.Fill;
        tcpPanel.Location = new Point(0, 62);
        tcpPanel.Margin = new Padding(0);
        tcpPanel.Name = "tcpPanel";
        tcpPanel.Size = new Size(540, 62);
        tcpPanel.TabIndex = 1;
        // 
        // tcpTable
        // 
        tcpTable.ColumnCount = 2;
        tcpTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        tcpTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        tcpTable.Controls.Add(enderecoIpCampoPanel, 0, 0);
        tcpTable.Controls.Add(portaTcpCampoPanel, 1, 0);
        tcpTable.Dock = DockStyle.Fill;
        tcpTable.Location = new Point(0, 0);
        tcpTable.Name = "tcpTable";
        tcpTable.RowCount = 1;
        tcpTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tcpTable.Size = new Size(540, 62);
        tcpTable.TabIndex = 0;
        // 
        // enderecoIpCampoPanel
        // 
        enderecoIpCampoPanel.Controls.Add(enderecoIpInputPanel);
        enderecoIpCampoPanel.Controls.Add(enderecoIpLabel);
        enderecoIpCampoPanel.Dock = DockStyle.Fill;
        enderecoIpCampoPanel.Location = new Point(0, 0);
        enderecoIpCampoPanel.Margin = new Padding(0);
        enderecoIpCampoPanel.Name = "enderecoIpCampoPanel";
        enderecoIpCampoPanel.Padding = new Padding(5, 2, 5, 4);
        enderecoIpCampoPanel.Size = new Size(351, 62);
        enderecoIpCampoPanel.TabIndex = 0;
        // 
        // enderecoIpInputPanel
        // 
        enderecoIpInputPanel.BackColor = Color.Transparent;
        enderecoIpInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        enderecoIpInputPanel.BorderRadius = 5;
        enderecoIpInputPanel.Controls.Add(enderecoIpTextBox);
        enderecoIpInputPanel.Dock = DockStyle.Fill;
        enderecoIpInputPanel.Location = new Point(5, 22);
        enderecoIpInputPanel.Name = "enderecoIpInputPanel";
        enderecoIpInputPanel.Padding = new Padding(10, 7, 8, 7);
        enderecoIpInputPanel.ShadowBlur = 0;
        enderecoIpInputPanel.ShadowOffsetY = 0;
        enderecoIpInputPanel.Size = new Size(341, 36);
        enderecoIpInputPanel.TabIndex = 0;
        // 
        // enderecoIpTextBox
        // 
        enderecoIpTextBox.BackColor = Color.White;
        enderecoIpTextBox.BorderStyle = BorderStyle.None;
        enderecoIpTextBox.Dock = DockStyle.Fill;
        enderecoIpTextBox.Font = new Font("Segoe UI", 9F);
        enderecoIpTextBox.Location = new Point(10, 7);
        enderecoIpTextBox.MaxLength = 45;
        enderecoIpTextBox.Name = "enderecoIpTextBox";
        enderecoIpTextBox.Size = new Size(323, 16);
        enderecoIpTextBox.TabIndex = 0;
        // 
        // enderecoIpLabel
        // 
        enderecoIpLabel.Dock = DockStyle.Top;
        enderecoIpLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        enderecoIpLabel.Location = new Point(5, 2);
        enderecoIpLabel.Name = "enderecoIpLabel";
        enderecoIpLabel.Size = new Size(341, 20);
        enderecoIpLabel.TabIndex = 1;
        enderecoIpLabel.Text = "Endereço IP *";
        // 
        // portaTcpCampoPanel
        // 
        portaTcpCampoPanel.Controls.Add(portaTcpInputPanel);
        portaTcpCampoPanel.Controls.Add(portaTcpLabel);
        portaTcpCampoPanel.Dock = DockStyle.Fill;
        portaTcpCampoPanel.Location = new Point(351, 0);
        portaTcpCampoPanel.Margin = new Padding(0);
        portaTcpCampoPanel.Name = "portaTcpCampoPanel";
        portaTcpCampoPanel.Padding = new Padding(5, 2, 5, 4);
        portaTcpCampoPanel.Size = new Size(189, 62);
        portaTcpCampoPanel.TabIndex = 1;
        // 
        // portaTcpInputPanel
        // 
        portaTcpInputPanel.BackColor = Color.Transparent;
        portaTcpInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        portaTcpInputPanel.BorderRadius = 5;
        portaTcpInputPanel.Controls.Add(portaTcpTextBox);
        portaTcpInputPanel.Dock = DockStyle.Fill;
        portaTcpInputPanel.Location = new Point(5, 22);
        portaTcpInputPanel.Name = "portaTcpInputPanel";
        portaTcpInputPanel.Padding = new Padding(10, 7, 8, 7);
        portaTcpInputPanel.ShadowBlur = 0;
        portaTcpInputPanel.ShadowOffsetY = 0;
        portaTcpInputPanel.Size = new Size(179, 36);
        portaTcpInputPanel.TabIndex = 0;
        // 
        // portaTcpTextBox
        // 
        portaTcpTextBox.BackColor = Color.White;
        portaTcpTextBox.BorderStyle = BorderStyle.None;
        portaTcpTextBox.Dock = DockStyle.Fill;
        portaTcpTextBox.Font = new Font("Segoe UI", 9F);
        portaTcpTextBox.Location = new Point(10, 7);
        portaTcpTextBox.MaxLength = 5;
        portaTcpTextBox.Name = "portaTcpTextBox";
        portaTcpTextBox.Size = new Size(161, 16);
        portaTcpTextBox.TabIndex = 0;
        // 
        // portaTcpLabel
        // 
        portaTcpLabel.Dock = DockStyle.Top;
        portaTcpLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        portaTcpLabel.Location = new Point(5, 2);
        portaTcpLabel.Name = "portaTcpLabel";
        portaTcpLabel.Size = new Size(179, 20);
        portaTcpLabel.TabIndex = 1;
        portaTcpLabel.Text = "Porta TCP *";
        // 
        // portaSerialPanel
        // 
        portaSerialPanel.Controls.Add(portaSerialInputPanel);
        portaSerialPanel.Controls.Add(portaSerialLabel);
        portaSerialPanel.Dock = DockStyle.Fill;
        portaSerialPanel.Location = new Point(0, 124);
        portaSerialPanel.Margin = new Padding(0);
        portaSerialPanel.Name = "portaSerialPanel";
        portaSerialPanel.Padding = new Padding(5, 2, 5, 4);
        portaSerialPanel.Size = new Size(540, 62);
        portaSerialPanel.TabIndex = 2;
        // 
        // portaSerialInputPanel
        // 
        portaSerialInputPanel.BackColor = Color.Transparent;
        portaSerialInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        portaSerialInputPanel.BorderRadius = 5;
        portaSerialInputPanel.Controls.Add(portaSerialTextBox);
        portaSerialInputPanel.Dock = DockStyle.Fill;
        portaSerialInputPanel.Location = new Point(5, 22);
        portaSerialInputPanel.Name = "portaSerialInputPanel";
        portaSerialInputPanel.Padding = new Padding(10, 7, 8, 7);
        portaSerialInputPanel.ShadowBlur = 0;
        portaSerialInputPanel.ShadowOffsetY = 0;
        portaSerialInputPanel.Size = new Size(530, 36);
        portaSerialInputPanel.TabIndex = 0;
        // 
        // portaSerialTextBox
        // 
        portaSerialTextBox.BackColor = Color.White;
        portaSerialTextBox.BorderStyle = BorderStyle.None;
        portaSerialTextBox.Dock = DockStyle.Fill;
        portaSerialTextBox.Font = new Font("Segoe UI", 9F);
        portaSerialTextBox.Location = new Point(10, 7);
        portaSerialTextBox.MaxLength = 50;
        portaSerialTextBox.Name = "portaSerialTextBox";
        portaSerialTextBox.Size = new Size(512, 16);
        portaSerialTextBox.TabIndex = 0;
        // 
        // portaSerialLabel
        // 
        portaSerialLabel.Dock = DockStyle.Top;
        portaSerialLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        portaSerialLabel.Location = new Point(5, 2);
        portaSerialLabel.Name = "portaSerialLabel";
        portaSerialLabel.Size = new Size(530, 20);
        portaSerialLabel.TabIndex = 1;
        portaSerialLabel.Text = "Porta Serial";
        // 
        // parametrosSeriaisSectionLabel
        // 
        parametrosSeriaisSectionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        parametrosSeriaisSectionLabel.ForeColor = Color.FromArgb(220, 38, 38);
        parametrosSeriaisSectionLabel.Location = new Point(24, 442);
        parametrosSeriaisSectionLabel.Name = "parametrosSeriaisSectionLabel";
        parametrosSeriaisSectionLabel.Size = new Size(520, 22);
        parametrosSeriaisSectionLabel.TabIndex = 5;
        parametrosSeriaisSectionLabel.Text = "PARÂMETROS SERIAIS";
        // 
        // serialPanel
        // 
        serialPanel.Controls.Add(serialTable);
        serialPanel.Location = new Point(20, 466);
        serialPanel.Name = "serialPanel";
        serialPanel.Size = new Size(540, 186);
        serialPanel.TabIndex = 6;
        // 
        // serialTable
        // 
        serialTable.ColumnCount = 3;
        serialTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        serialTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        serialTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        serialTable.Controls.Add(baudRateCampoPanel, 0, 0);
        serialTable.Controls.Add(dataBitsCampoPanel, 1, 0);
        serialTable.Controls.Add(paridadeCampoPanel, 2, 0);
        serialTable.Controls.Add(stopBitsCampoPanel, 0, 1);
        serialTable.Controls.Add(flowControlCampoPanel, 1, 1);
        serialTable.Controls.Add(protocoloCampoPanel, 2, 1);
        serialTable.Dock = DockStyle.Fill;
        serialTable.Location = new Point(0, 0);
        serialTable.Name = "serialTable";
        serialTable.RowCount = 2;
        serialTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        serialTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        serialTable.Size = new Size(540, 186);
        serialTable.TabIndex = 0;
        // 
        // baudRateCampoPanel
        // 
        baudRateCampoPanel.Controls.Add(baudRateInputPanel);
        baudRateCampoPanel.Controls.Add(baudRateLabel);
        baudRateCampoPanel.Dock = DockStyle.Fill;
        baudRateCampoPanel.Location = new Point(0, 0);
        baudRateCampoPanel.Margin = new Padding(0);
        baudRateCampoPanel.Name = "baudRateCampoPanel";
        baudRateCampoPanel.Padding = new Padding(5, 2, 5, 4);
        baudRateCampoPanel.Size = new Size(179, 62);
        baudRateCampoPanel.TabIndex = 0;
        // 
        // baudRateInputPanel
        // 
        baudRateInputPanel.BackColor = Color.Transparent;
        baudRateInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        baudRateInputPanel.BorderRadius = 5;
        baudRateInputPanel.Controls.Add(baudRateTextBox);
        baudRateInputPanel.Dock = DockStyle.Fill;
        baudRateInputPanel.Location = new Point(5, 22);
        baudRateInputPanel.Name = "baudRateInputPanel";
        baudRateInputPanel.Padding = new Padding(10, 7, 8, 7);
        baudRateInputPanel.ShadowBlur = 0;
        baudRateInputPanel.ShadowOffsetY = 0;
        baudRateInputPanel.Size = new Size(169, 36);
        baudRateInputPanel.TabIndex = 0;
        // 
        // baudRateTextBox
        // 
        baudRateTextBox.BackColor = Color.White;
        baudRateTextBox.BorderStyle = BorderStyle.None;
        baudRateTextBox.Dock = DockStyle.Fill;
        baudRateTextBox.Font = new Font("Segoe UI", 9F);
        baudRateTextBox.Location = new Point(10, 7);
        baudRateTextBox.MaxLength = 7;
        baudRateTextBox.Name = "baudRateTextBox";
        baudRateTextBox.Size = new Size(151, 16);
        baudRateTextBox.TabIndex = 0;
        // 
        // baudRateLabel
        // 
        baudRateLabel.Dock = DockStyle.Top;
        baudRateLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        baudRateLabel.Location = new Point(5, 2);
        baudRateLabel.Name = "baudRateLabel";
        baudRateLabel.Size = new Size(169, 20);
        baudRateLabel.TabIndex = 1;
        baudRateLabel.Text = "Baud Rate *";
        // 
        // dataBitsCampoPanel
        // 
        dataBitsCampoPanel.Controls.Add(dataBitsInputPanel);
        dataBitsCampoPanel.Controls.Add(dataBitsLabel);
        dataBitsCampoPanel.Dock = DockStyle.Fill;
        dataBitsCampoPanel.Location = new Point(179, 0);
        dataBitsCampoPanel.Margin = new Padding(0);
        dataBitsCampoPanel.Name = "dataBitsCampoPanel";
        dataBitsCampoPanel.Padding = new Padding(5, 2, 5, 4);
        dataBitsCampoPanel.Size = new Size(179, 62);
        dataBitsCampoPanel.TabIndex = 1;
        // 
        // dataBitsInputPanel
        // 
        dataBitsInputPanel.BackColor = Color.Transparent;
        dataBitsInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        dataBitsInputPanel.BorderRadius = 5;
        dataBitsInputPanel.Controls.Add(dataBitsTextBox);
        dataBitsInputPanel.Dock = DockStyle.Fill;
        dataBitsInputPanel.Location = new Point(5, 22);
        dataBitsInputPanel.Name = "dataBitsInputPanel";
        dataBitsInputPanel.Padding = new Padding(10, 7, 8, 7);
        dataBitsInputPanel.ShadowBlur = 0;
        dataBitsInputPanel.ShadowOffsetY = 0;
        dataBitsInputPanel.Size = new Size(169, 36);
        dataBitsInputPanel.TabIndex = 0;
        // 
        // dataBitsTextBox
        // 
        dataBitsTextBox.BackColor = Color.White;
        dataBitsTextBox.BorderStyle = BorderStyle.None;
        dataBitsTextBox.Dock = DockStyle.Fill;
        dataBitsTextBox.Font = new Font("Segoe UI", 9F);
        dataBitsTextBox.Location = new Point(10, 7);
        dataBitsTextBox.MaxLength = 2;
        dataBitsTextBox.Name = "dataBitsTextBox";
        dataBitsTextBox.Size = new Size(151, 16);
        dataBitsTextBox.TabIndex = 0;
        // 
        // dataBitsLabel
        // 
        dataBitsLabel.Dock = DockStyle.Top;
        dataBitsLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        dataBitsLabel.Location = new Point(5, 2);
        dataBitsLabel.Name = "dataBitsLabel";
        dataBitsLabel.Size = new Size(169, 20);
        dataBitsLabel.TabIndex = 1;
        dataBitsLabel.Text = "Data Bits *";
        // 
        // paridadeCampoPanel
        // 
        paridadeCampoPanel.Controls.Add(paridadeInputPanel);
        paridadeCampoPanel.Controls.Add(paridadeLabel);
        paridadeCampoPanel.Dock = DockStyle.Fill;
        paridadeCampoPanel.Location = new Point(358, 0);
        paridadeCampoPanel.Margin = new Padding(0);
        paridadeCampoPanel.Name = "paridadeCampoPanel";
        paridadeCampoPanel.Padding = new Padding(5, 2, 5, 4);
        paridadeCampoPanel.Size = new Size(182, 62);
        paridadeCampoPanel.TabIndex = 2;
        // 
        // paridadeInputPanel
        // 
        paridadeInputPanel.BackColor = Color.Transparent;
        paridadeInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        paridadeInputPanel.BorderRadius = 5;
        paridadeInputPanel.Controls.Add(paridadeComboBox);
        paridadeInputPanel.Dock = DockStyle.Fill;
        paridadeInputPanel.Location = new Point(5, 22);
        paridadeInputPanel.Name = "paridadeInputPanel";
        paridadeInputPanel.Padding = new Padding(10, 5, 8, 5);
        paridadeInputPanel.ShadowBlur = 0;
        paridadeInputPanel.ShadowOffsetY = 0;
        paridadeInputPanel.Size = new Size(172, 36);
        paridadeInputPanel.TabIndex = 0;
        // 
        // paridadeComboBox
        // 
        paridadeComboBox.BackColor = Color.White;
        paridadeComboBox.Dock = DockStyle.Top;
        paridadeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        paridadeComboBox.FlatStyle = FlatStyle.Flat;
        paridadeComboBox.Font = new Font("Segoe UI", 9F);
        paridadeComboBox.IntegralHeight = false;
        paridadeComboBox.Location = new Point(10, 7);
        paridadeComboBox.Name = "paridadeComboBox";
        paridadeComboBox.Size = new Size(154, 23);
        paridadeComboBox.TabIndex = 0;
        // 
        // paridadeLabel
        // 
        paridadeLabel.Dock = DockStyle.Top;
        paridadeLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        paridadeLabel.Location = new Point(5, 2);
        paridadeLabel.Name = "paridadeLabel";
        paridadeLabel.Size = new Size(172, 20);
        paridadeLabel.TabIndex = 1;
        paridadeLabel.Text = "Paridade *";
        // 
        // stopBitsCampoPanel
        // 
        stopBitsCampoPanel.Controls.Add(stopBitsInputPanel);
        stopBitsCampoPanel.Controls.Add(stopBitsLabel);
        stopBitsCampoPanel.Dock = DockStyle.Fill;
        stopBitsCampoPanel.Location = new Point(0, 62);
        stopBitsCampoPanel.Margin = new Padding(0);
        stopBitsCampoPanel.Name = "stopBitsCampoPanel";
        stopBitsCampoPanel.Padding = new Padding(5, 2, 5, 4);
        stopBitsCampoPanel.Size = new Size(179, 124);
        stopBitsCampoPanel.TabIndex = 3;
        // 
        // stopBitsInputPanel
        // 
        stopBitsInputPanel.BackColor = Color.Transparent;
        stopBitsInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        stopBitsInputPanel.BorderRadius = 5;
        stopBitsInputPanel.Controls.Add(stopBitsComboBox);
        stopBitsInputPanel.Dock = DockStyle.Fill;
        stopBitsInputPanel.Location = new Point(5, 22);
        stopBitsInputPanel.Name = "stopBitsInputPanel";
        stopBitsInputPanel.Padding = new Padding(10, 7, 8, 7);
        stopBitsInputPanel.ShadowBlur = 0;
        stopBitsInputPanel.ShadowOffsetY = 0;
        stopBitsInputPanel.Size = new Size(169, 98);
        stopBitsInputPanel.TabIndex = 0;
        // 
        // stopBitsComboBox
        // 
        stopBitsComboBox.BackColor = Color.White;
        stopBitsComboBox.Dock = DockStyle.Top;
        stopBitsComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        stopBitsComboBox.FlatStyle = FlatStyle.Flat;
        stopBitsComboBox.Font = new Font("Segoe UI", 9F);
        stopBitsComboBox.IntegralHeight = false;
        stopBitsComboBox.Location = new Point(10, 7);
        stopBitsComboBox.Name = "stopBitsComboBox";
        stopBitsComboBox.Size = new Size(151, 23);
        stopBitsComboBox.TabIndex = 0;
        // 
        // stopBitsLabel
        // 
        stopBitsLabel.Dock = DockStyle.Top;
        stopBitsLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        stopBitsLabel.Location = new Point(5, 2);
        stopBitsLabel.Name = "stopBitsLabel";
        stopBitsLabel.Size = new Size(169, 20);
        stopBitsLabel.TabIndex = 1;
        stopBitsLabel.Text = "Stop Bits *";
        // 
        // flowControlCampoPanel
        // 
        flowControlCampoPanel.Controls.Add(flowControlInputPanel);
        flowControlCampoPanel.Controls.Add(flowControlLabel);
        flowControlCampoPanel.Dock = DockStyle.Fill;
        flowControlCampoPanel.Location = new Point(179, 62);
        flowControlCampoPanel.Margin = new Padding(0);
        flowControlCampoPanel.Name = "flowControlCampoPanel";
        flowControlCampoPanel.Padding = new Padding(5, 2, 5, 4);
        flowControlCampoPanel.Size = new Size(179, 124);
        flowControlCampoPanel.TabIndex = 4;
        // 
        // flowControlInputPanel
        // 
        flowControlInputPanel.BackColor = Color.Transparent;
        flowControlInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        flowControlInputPanel.BorderRadius = 5;
        flowControlInputPanel.Controls.Add(flowControlComboBox);
        flowControlInputPanel.Dock = DockStyle.Fill;
        flowControlInputPanel.Location = new Point(5, 22);
        flowControlInputPanel.Name = "flowControlInputPanel";
        flowControlInputPanel.Padding = new Padding(10, 7, 8, 7);
        flowControlInputPanel.ShadowBlur = 0;
        flowControlInputPanel.ShadowOffsetY = 0;
        flowControlInputPanel.Size = new Size(169, 98);
        flowControlInputPanel.TabIndex = 0;
        // 
        // flowControlComboBox
        // 
        flowControlComboBox.BackColor = Color.White;
        flowControlComboBox.Dock = DockStyle.Top;
        flowControlComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        flowControlComboBox.FlatStyle = FlatStyle.Flat;
        flowControlComboBox.Font = new Font("Segoe UI", 9F);
        flowControlComboBox.IntegralHeight = false;
        flowControlComboBox.Location = new Point(10, 7);
        flowControlComboBox.Name = "flowControlComboBox";
        flowControlComboBox.Size = new Size(151, 23);
        flowControlComboBox.TabIndex = 0;
        // 
        // flowControlLabel
        // 
        flowControlLabel.Dock = DockStyle.Top;
        flowControlLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        flowControlLabel.Location = new Point(5, 2);
        flowControlLabel.Name = "flowControlLabel";
        flowControlLabel.Size = new Size(169, 20);
        flowControlLabel.TabIndex = 1;
        flowControlLabel.Text = "Flow Control";
        // 
        // protocoloCampoPanel
        // 
        protocoloCampoPanel.Controls.Add(protocoloInputPanel);
        protocoloCampoPanel.Controls.Add(protocoloLabel);
        protocoloCampoPanel.Dock = DockStyle.Fill;
        protocoloCampoPanel.Location = new Point(358, 62);
        protocoloCampoPanel.Margin = new Padding(0);
        protocoloCampoPanel.Name = "protocoloCampoPanel";
        protocoloCampoPanel.Padding = new Padding(5, 2, 5, 4);
        protocoloCampoPanel.Size = new Size(182, 124);
        protocoloCampoPanel.TabIndex = 5;
        // 
        // protocoloInputPanel
        // 
        protocoloInputPanel.BackColor = Color.Transparent;
        protocoloInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        protocoloInputPanel.BorderRadius = 5;
        protocoloInputPanel.Controls.Add(protocoloTextBox);
        protocoloInputPanel.Dock = DockStyle.Fill;
        protocoloInputPanel.Location = new Point(5, 22);
        protocoloInputPanel.Name = "protocoloInputPanel";
        protocoloInputPanel.Padding = new Padding(10, 7, 8, 7);
        protocoloInputPanel.ShadowBlur = 0;
        protocoloInputPanel.ShadowOffsetY = 0;
        protocoloInputPanel.Size = new Size(172, 98);
        protocoloInputPanel.TabIndex = 0;
        // 
        // protocoloTextBox
        // 
        protocoloTextBox.BackColor = Color.White;
        protocoloTextBox.BorderStyle = BorderStyle.None;
        protocoloTextBox.Dock = DockStyle.Fill;
        protocoloTextBox.Font = new Font("Segoe UI", 9F);
        protocoloTextBox.Location = new Point(10, 7);
        protocoloTextBox.MaxLength = 50;
        protocoloTextBox.Name = "protocoloTextBox";
        protocoloTextBox.Size = new Size(154, 16);
        protocoloTextBox.TabIndex = 0;
        // 
        // protocoloLabel
        // 
        protocoloLabel.Dock = DockStyle.Top;
        protocoloLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        protocoloLabel.Location = new Point(5, 2);
        protocoloLabel.Name = "protocoloLabel";
        protocoloLabel.Size = new Size(172, 20);
        protocoloLabel.TabIndex = 1;
        protocoloLabel.Text = "Protocolo";
        // 
        // manualWarningLabel
        // 
        manualWarningLabel.BackColor = Color.FromArgb(255, 251, 235);
        manualWarningLabel.BorderStyle = BorderStyle.FixedSingle;
        manualWarningLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        manualWarningLabel.ForeColor = Color.FromArgb(146, 64, 14);
        manualWarningLabel.Location = new Point(20, 466);
        manualWarningLabel.Name = "manualWarningLabel";
        manualWarningLabel.Size = new Size(540, 46);
        manualWarningLabel.TabIndex = 7;
        manualWarningLabel.Text = "⚠  Balança manual não realiza leitura física automática.";
        manualWarningLabel.TextAlign = ContentAlignment.MiddleCenter;
        manualWarningLabel.Visible = false;
        // 
        // observacaoSectionLabel
        // 
        observacaoSectionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        observacaoSectionLabel.ForeColor = Color.FromArgb(220, 38, 38);
        observacaoSectionLabel.Location = new Point(24, 662);
        observacaoSectionLabel.Name = "observacaoSectionLabel";
        observacaoSectionLabel.Size = new Size(520, 22);
        observacaoSectionLabel.TabIndex = 8;
        observacaoSectionLabel.Text = "OBSERVAÇÕES";
        // 
        // observacaoInputPanel
        // 
        observacaoInputPanel.BackColor = Color.Transparent;
        observacaoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        observacaoInputPanel.BorderRadius = 5;
        observacaoInputPanel.Controls.Add(observacaoTextBox);
        observacaoInputPanel.Location = new Point(20, 688);
        observacaoInputPanel.Name = "observacaoInputPanel";
        observacaoInputPanel.Padding = new Padding(10, 8, 8, 8);
        observacaoInputPanel.ShadowBlur = 0;
        observacaoInputPanel.ShadowOffsetY = 0;
        observacaoInputPanel.Size = new Size(540, 86);
        observacaoInputPanel.TabIndex = 9;
        // 
        // observacaoTextBox
        // 
        observacaoTextBox.BackColor = Color.White;
        observacaoTextBox.BorderStyle = BorderStyle.None;
        observacaoTextBox.Dock = DockStyle.Fill;
        observacaoTextBox.Font = new Font("Segoe UI", 9F);
        observacaoTextBox.Location = new Point(10, 8);
        observacaoTextBox.MaxLength = 255;
        observacaoTextBox.Multiline = true;
        observacaoTextBox.Name = "observacaoTextBox";
        observacaoTextBox.ScrollBars = ScrollBars.Vertical;
        observacaoTextBox.Size = new Size(522, 70);
        observacaoTextBox.TabIndex = 0;
        // 
        // resumoCard
        // 
        resumoCard.BackColor = Color.Transparent;
        resumoCard.BorderColor = Color.FromArgb(226, 232, 240);
        resumoCard.BorderRadius = 9;
        resumoCard.Controls.Add(resumoTitleLabel);
        resumoCard.Controls.Add(resumoTitleIconLabel);
        resumoCard.Controls.Add(resumoDivisorTitulo);
        resumoCard.Controls.Add(resumoDivisorNome);
        resumoCard.Controls.Add(resumoDivisorSetor);
        resumoCard.Controls.Add(resumoDivisorConexao);
        resumoCard.Controls.Add(resumoNomeIconLabel);
        resumoCard.Controls.Add(resumoSetorIconLabel);
        resumoCard.Controls.Add(resumoConexaoIconLabel);
        resumoCard.Controls.Add(resumoSituacaoIconLabel);
        resumoCard.Controls.Add(resumoDivisorRodape);
        resumoCard.Controls.Add(resumoDataLabel);
        resumoCard.Controls.Add(resumoNomeLabel);
        resumoCard.Controls.Add(resumoNomeValueLabel);
        resumoCard.Controls.Add(resumoSetorLabel);
        resumoCard.Controls.Add(resumoSetorValueLabel);
        resumoCard.Controls.Add(resumoConexaoLabel);
        resumoCard.Controls.Add(resumoConexaoValueLabel);
        resumoCard.Controls.Add(resumoSituacaoLabel);
        resumoCard.Controls.Add(resumoSituacaoValueLabel);
        resumoCard.Controls.Add(salvarButton);
        resumoCard.Controls.Add(salvarAlteracoesButton);
        resumoCard.Controls.Add(alterarSituacaoButton);
        resumoCard.Dock = DockStyle.Fill;
        resumoCard.Location = new Point(1023, 16);
        resumoCard.Margin = new Padding(0);
        resumoCard.Name = "resumoCard";
        resumoCard.ShadowBlur = 0;
        resumoCard.ShadowOffsetY = 0;
        resumoCard.Size = new Size(317, 590);
        resumoCard.TabIndex = 2;
        // 
        // resumoTitleLabel
        // 
        resumoTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        resumoTitleLabel.Location = new Point(52, 18);
        resumoTitleLabel.Name = "resumoTitleLabel";
        resumoTitleLabel.Size = new Size(240, 28);
        resumoTitleLabel.TabIndex = 0;
        resumoTitleLabel.Text = "Resumo da Balança";
        // 
        // resumoTitleIconLabel
        // 
        resumoTitleIconLabel.Font = new Font("Segoe MDL2 Assets", 16F);
        resumoTitleIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        resumoTitleIconLabel.Location = new Point(20, 16);
        resumoTitleIconLabel.Name = "resumoTitleIconLabel";
        resumoTitleIconLabel.Size = new Size(30, 30);
        resumoTitleIconLabel.TabIndex = 1;
        resumoTitleIconLabel.Text = "";
        resumoTitleIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumoDivisorTitulo
        // 
        resumoDivisorTitulo.BackColor = Color.FromArgb(226, 232, 240);
        resumoDivisorTitulo.Location = new Point(24, 56);
        resumoDivisorTitulo.Name = "resumoDivisorTitulo";
        resumoDivisorTitulo.Size = new Size(278, 1);
        resumoDivisorTitulo.TabIndex = 2;
        // 
        // resumoDivisorNome
        // 
        resumoDivisorNome.BackColor = Color.FromArgb(226, 232, 240);
        resumoDivisorNome.Location = new Point(24, 130);
        resumoDivisorNome.Name = "resumoDivisorNome";
        resumoDivisorNome.Size = new Size(278, 1);
        resumoDivisorNome.TabIndex = 3;
        // 
        // resumoDivisorSetor
        // 
        resumoDivisorSetor.BackColor = Color.FromArgb(226, 232, 240);
        resumoDivisorSetor.Location = new Point(24, 196);
        resumoDivisorSetor.Name = "resumoDivisorSetor";
        resumoDivisorSetor.Size = new Size(278, 1);
        resumoDivisorSetor.TabIndex = 4;
        // 
        // resumoDivisorConexao
        // 
        resumoDivisorConexao.BackColor = Color.FromArgb(226, 232, 240);
        resumoDivisorConexao.Location = new Point(24, 262);
        resumoDivisorConexao.Name = "resumoDivisorConexao";
        resumoDivisorConexao.Size = new Size(278, 1);
        resumoDivisorConexao.TabIndex = 5;
        // 
        // resumoNomeIconLabel
        // 
        resumoNomeIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        resumoNomeIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        resumoNomeIconLabel.Location = new Point(26, 76);
        resumoNomeIconLabel.Name = "resumoNomeIconLabel";
        resumoNomeIconLabel.Size = new Size(32, 32);
        resumoNomeIconLabel.TabIndex = 6;
        resumoNomeIconLabel.Text = "";
        resumoNomeIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumoSetorIconLabel
        // 
        resumoSetorIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        resumoSetorIconLabel.ForeColor = Color.FromArgb(22, 163, 74);
        resumoSetorIconLabel.Location = new Point(26, 142);
        resumoSetorIconLabel.Name = "resumoSetorIconLabel";
        resumoSetorIconLabel.Size = new Size(32, 32);
        resumoSetorIconLabel.TabIndex = 7;
        resumoSetorIconLabel.Text = "";
        resumoSetorIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumoConexaoIconLabel
        // 
        resumoConexaoIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        resumoConexaoIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        resumoConexaoIconLabel.Location = new Point(26, 208);
        resumoConexaoIconLabel.Name = "resumoConexaoIconLabel";
        resumoConexaoIconLabel.Size = new Size(32, 32);
        resumoConexaoIconLabel.TabIndex = 8;
        resumoConexaoIconLabel.Text = "";
        resumoConexaoIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumoSituacaoIconLabel
        // 
        resumoSituacaoIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        resumoSituacaoIconLabel.ForeColor = Color.FromArgb(22, 163, 74);
        resumoSituacaoIconLabel.Location = new Point(26, 274);
        resumoSituacaoIconLabel.Name = "resumoSituacaoIconLabel";
        resumoSituacaoIconLabel.Size = new Size(32, 32);
        resumoSituacaoIconLabel.TabIndex = 9;
        resumoSituacaoIconLabel.Text = "";
        resumoSituacaoIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumoDivisorRodape
        // 
        resumoDivisorRodape.BackColor = Color.FromArgb(226, 232, 240);
        resumoDivisorRodape.Location = new Point(24, 332);
        resumoDivisorRodape.Name = "resumoDivisorRodape";
        resumoDivisorRodape.Size = new Size(278, 1);
        resumoDivisorRodape.TabIndex = 10;
        // 
        // resumoDataLabel
        // 
        resumoDataLabel.Font = new Font("Segoe UI", 9F);
        resumoDataLabel.ForeColor = Color.FromArgb(71, 85, 105);
        resumoDataLabel.Location = new Point(24, 348);
        resumoDataLabel.Name = "resumoDataLabel";
        resumoDataLabel.Size = new Size(278, 22);
        resumoDataLabel.TabIndex = 11;
        resumoDataLabel.Text = "Data e Hora do Cadastro: -";
        resumoDataLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumoNomeLabel
        // 
        resumoNomeLabel.Font = new Font("Segoe UI", 8F);
        resumoNomeLabel.ForeColor = Color.FromArgb(100, 116, 139);
        resumoNomeLabel.Location = new Point(66, 76);
        resumoNomeLabel.Name = "resumoNomeLabel";
        resumoNomeLabel.Size = new Size(236, 18);
        resumoNomeLabel.TabIndex = 1;
        resumoNomeLabel.Text = "Balança selecionada";
        // 
        // resumoNomeValueLabel
        // 
        resumoNomeValueLabel.AutoEllipsis = true;
        resumoNomeValueLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        resumoNomeValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        resumoNomeValueLabel.Location = new Point(66, 96);
        resumoNomeValueLabel.Name = "resumoNomeValueLabel";
        resumoNomeValueLabel.Size = new Size(236, 24);
        resumoNomeValueLabel.TabIndex = 2;
        resumoNomeValueLabel.Text = "-";
        // 
        // resumoSetorLabel
        // 
        resumoSetorLabel.Font = new Font("Segoe UI", 8F);
        resumoSetorLabel.ForeColor = Color.FromArgb(100, 116, 139);
        resumoSetorLabel.Location = new Point(66, 142);
        resumoSetorLabel.Name = "resumoSetorLabel";
        resumoSetorLabel.Size = new Size(236, 18);
        resumoSetorLabel.TabIndex = 3;
        resumoSetorLabel.Text = "Setor";
        // 
        // resumoSetorValueLabel
        // 
        resumoSetorValueLabel.AutoEllipsis = true;
        resumoSetorValueLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        resumoSetorValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        resumoSetorValueLabel.Location = new Point(66, 162);
        resumoSetorValueLabel.Name = "resumoSetorValueLabel";
        resumoSetorValueLabel.Size = new Size(236, 24);
        resumoSetorValueLabel.TabIndex = 4;
        resumoSetorValueLabel.Text = "-";
        // 
        // resumoConexaoLabel
        // 
        resumoConexaoLabel.Font = new Font("Segoe UI", 8F);
        resumoConexaoLabel.ForeColor = Color.FromArgb(100, 116, 139);
        resumoConexaoLabel.Location = new Point(66, 208);
        resumoConexaoLabel.Name = "resumoConexaoLabel";
        resumoConexaoLabel.Size = new Size(236, 18);
        resumoConexaoLabel.TabIndex = 5;
        resumoConexaoLabel.Text = "Tipo de conexão";
        // 
        // resumoConexaoValueLabel
        // 
        resumoConexaoValueLabel.AutoEllipsis = true;
        resumoConexaoValueLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        resumoConexaoValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        resumoConexaoValueLabel.Location = new Point(66, 228);
        resumoConexaoValueLabel.Name = "resumoConexaoValueLabel";
        resumoConexaoValueLabel.Size = new Size(236, 24);
        resumoConexaoValueLabel.TabIndex = 6;
        resumoConexaoValueLabel.Text = "-";
        // 
        // resumoSituacaoLabel
        // 
        resumoSituacaoLabel.Font = new Font("Segoe UI", 8F);
        resumoSituacaoLabel.ForeColor = Color.FromArgb(100, 116, 139);
        resumoSituacaoLabel.Location = new Point(66, 274);
        resumoSituacaoLabel.Name = "resumoSituacaoLabel";
        resumoSituacaoLabel.Size = new Size(236, 18);
        resumoSituacaoLabel.TabIndex = 7;
        resumoSituacaoLabel.Text = "Situação";
        // 
        // resumoSituacaoValueLabel
        // 
        resumoSituacaoValueLabel.AutoEllipsis = true;
        resumoSituacaoValueLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        resumoSituacaoValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        resumoSituacaoValueLabel.Location = new Point(66, 294);
        resumoSituacaoValueLabel.Name = "resumoSituacaoValueLabel";
        resumoSituacaoValueLabel.Size = new Size(236, 24);
        resumoSituacaoValueLabel.TabIndex = 8;
        resumoSituacaoValueLabel.Text = "-";
        // 
        // salvarButton
        // 
        salvarButton.BackColor = Color.FromArgb(229, 27, 43);
        salvarButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        salvarButton.FlatStyle = FlatStyle.Flat;
        salvarButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        salvarButton.ForeColor = Color.White;
        salvarButton.Location = new Point(22, 460);
        salvarButton.Name = "salvarButton";
        salvarButton.Size = new Size(278, 34);
        salvarButton.TabIndex = 9;
        salvarButton.Text = "Salvar Balança          F5";
        salvarButton.UseVisualStyleBackColor = false;
        // 
        // salvarAlteracoesButton
        // 
        salvarAlteracoesButton.BackColor = Color.White;
        salvarAlteracoesButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        salvarAlteracoesButton.FlatStyle = FlatStyle.Flat;
        salvarAlteracoesButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        salvarAlteracoesButton.ForeColor = Color.FromArgb(15, 23, 42);
        salvarAlteracoesButton.Location = new Point(22, 502);
        salvarAlteracoesButton.Name = "salvarAlteracoesButton";
        salvarAlteracoesButton.Size = new Size(278, 34);
        salvarAlteracoesButton.TabIndex = 10;
        salvarAlteracoesButton.Text = "Salvar Alterações          F6";
        salvarAlteracoesButton.UseVisualStyleBackColor = false;
        // 
        // alterarSituacaoButton
        // 
        alterarSituacaoButton.BackColor = Color.White;
        alterarSituacaoButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        alterarSituacaoButton.FlatStyle = FlatStyle.Flat;
        alterarSituacaoButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        alterarSituacaoButton.ForeColor = Color.FromArgb(220, 38, 38);
        alterarSituacaoButton.Location = new Point(22, 544);
        alterarSituacaoButton.Name = "alterarSituacaoButton";
        alterarSituacaoButton.Size = new Size(278, 34);
        alterarSituacaoButton.TabIndex = 11;
        alterarSituacaoButton.Text = "Inativar Balança          F8";
        alterarSituacaoButton.UseVisualStyleBackColor = false;
        // 
        // footerBar
        // 
        footerBar.BackColor = Color.FromArgb(248, 250, 253);
        footerBar.Controls.Add(footerBarLayout);
        footerBar.Dock = DockStyle.Fill;
        footerBar.Location = new Point(0, 680);
        footerBar.Margin = new Padding(0);
        footerBar.Name = "footerBar";
        footerBar.Size = new Size(1366, 40);
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
        footerBarLayout.Size = new Size(1366, 40);
        footerBarLayout.TabIndex = 0;
        // 
        // cellUser
        // 
        cellUser.BackColor = Color.Transparent;
        cellUser.Controls.Add(cellUserText);
        cellUser.Controls.Add(cellUserDivider);
        cellUser.Controls.Add(cellUserIcon);
        cellUser.Dock = DockStyle.Fill;
        cellUser.Location = new Point(0, 0);
        cellUser.Margin = new Padding(0);
        cellUser.Name = "cellUser";
        cellUser.Size = new Size(218, 40);
        cellUser.TabIndex = 0;
        // 
        // cellUserText
        // 
        cellUserText.AutoEllipsis = true;
        cellUserText.Dock = DockStyle.Fill;
        cellUserText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellUserText.ForeColor = Color.FromArgb(98, 108, 124);
        cellUserText.Location = new Point(28, 0);
        cellUserText.Name = "cellUserText";
        cellUserText.Padding = new Padding(2, 0, 0, 0);
        cellUserText.Size = new Size(189, 40);
        cellUserText.TabIndex = 0;
        cellUserText.Text = "Usuário:  -";
        cellUserText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellUserDivider
        // 
        cellUserDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellUserDivider.Dock = DockStyle.Right;
        cellUserDivider.Location = new Point(217, 0);
        cellUserDivider.Margin = new Padding(0);
        cellUserDivider.Name = "cellUserDivider";
        cellUserDivider.Size = new Size(1, 40);
        cellUserDivider.TabIndex = 1;
        // 
        // cellUserIcon
        // 
        cellUserIcon.Dock = DockStyle.Left;
        cellUserIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellUserIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellUserIcon.Location = new Point(0, 0);
        cellUserIcon.Name = "cellUserIcon";
        cellUserIcon.Size = new Size(28, 40);
        cellUserIcon.TabIndex = 2;
        cellUserIcon.Text = "";
        cellUserIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellTerminal
        // 
        cellTerminal.BackColor = Color.Transparent;
        cellTerminal.Controls.Add(cellTerminalText);
        cellTerminal.Controls.Add(cellTerminalDivider);
        cellTerminal.Controls.Add(cellTerminalIcon);
        cellTerminal.Dock = DockStyle.Fill;
        cellTerminal.Location = new Point(218, 0);
        cellTerminal.Margin = new Padding(0);
        cellTerminal.Name = "cellTerminal";
        cellTerminal.Size = new Size(204, 40);
        cellTerminal.TabIndex = 1;
        // 
        // cellTerminalText
        // 
        cellTerminalText.AutoEllipsis = true;
        cellTerminalText.Dock = DockStyle.Fill;
        cellTerminalText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellTerminalText.ForeColor = Color.FromArgb(98, 108, 124);
        cellTerminalText.Location = new Point(28, 0);
        cellTerminalText.Name = "cellTerminalText";
        cellTerminalText.Padding = new Padding(2, 0, 0, 0);
        cellTerminalText.Size = new Size(175, 40);
        cellTerminalText.TabIndex = 0;
        cellTerminalText.Text = "Terminal:  -";
        cellTerminalText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellTerminalDivider
        // 
        cellTerminalDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellTerminalDivider.Dock = DockStyle.Right;
        cellTerminalDivider.Location = new Point(203, 0);
        cellTerminalDivider.Margin = new Padding(0);
        cellTerminalDivider.Name = "cellTerminalDivider";
        cellTerminalDivider.Size = new Size(1, 40);
        cellTerminalDivider.TabIndex = 1;
        // 
        // cellTerminalIcon
        // 
        cellTerminalIcon.Dock = DockStyle.Left;
        cellTerminalIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellTerminalIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellTerminalIcon.Location = new Point(0, 0);
        cellTerminalIcon.Name = "cellTerminalIcon";
        cellTerminalIcon.Size = new Size(28, 40);
        cellTerminalIcon.TabIndex = 2;
        cellTerminalIcon.Text = "";
        cellTerminalIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellEmpresa
        // 
        cellEmpresa.BackColor = Color.Transparent;
        cellEmpresa.Controls.Add(cellEmpresaText);
        cellEmpresa.Controls.Add(cellEmpresaDivider);
        cellEmpresa.Controls.Add(cellEmpresaIcon);
        cellEmpresa.Dock = DockStyle.Fill;
        cellEmpresa.Location = new Point(422, 0);
        cellEmpresa.Margin = new Padding(0);
        cellEmpresa.Name = "cellEmpresa";
        cellEmpresa.Size = new Size(368, 40);
        cellEmpresa.TabIndex = 2;
        // 
        // cellEmpresaText
        // 
        cellEmpresaText.AutoEllipsis = true;
        cellEmpresaText.Dock = DockStyle.Fill;
        cellEmpresaText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellEmpresaText.ForeColor = Color.FromArgb(98, 108, 124);
        cellEmpresaText.Location = new Point(28, 0);
        cellEmpresaText.Name = "cellEmpresaText";
        cellEmpresaText.Padding = new Padding(2, 0, 0, 0);
        cellEmpresaText.Size = new Size(339, 40);
        cellEmpresaText.TabIndex = 0;
        cellEmpresaText.Text = "Empresa:  FUGA COUROS S.A.";
        cellEmpresaText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellEmpresaDivider
        // 
        cellEmpresaDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellEmpresaDivider.Dock = DockStyle.Right;
        cellEmpresaDivider.Location = new Point(367, 0);
        cellEmpresaDivider.Margin = new Padding(0);
        cellEmpresaDivider.Name = "cellEmpresaDivider";
        cellEmpresaDivider.Size = new Size(1, 40);
        cellEmpresaDivider.TabIndex = 1;
        // 
        // cellEmpresaIcon
        // 
        cellEmpresaIcon.Dock = DockStyle.Left;
        cellEmpresaIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellEmpresaIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellEmpresaIcon.Location = new Point(0, 0);
        cellEmpresaIcon.Name = "cellEmpresaIcon";
        cellEmpresaIcon.Size = new Size(28, 40);
        cellEmpresaIcon.TabIndex = 2;
        cellEmpresaIcon.Text = "";
        cellEmpresaIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellBanco
        // 
        cellBanco.BackColor = Color.Transparent;
        cellBanco.Controls.Add(cellBancoText);
        cellBanco.Controls.Add(cellBancoDivider);
        cellBanco.Controls.Add(cellBancoIcon);
        cellBanco.Dock = DockStyle.Fill;
        cellBanco.Location = new Point(790, 0);
        cellBanco.Margin = new Padding(0);
        cellBanco.Name = "cellBanco";
        cellBanco.Size = new Size(327, 40);
        cellBanco.TabIndex = 3;
        // 
        // cellBancoText
        // 
        cellBancoText.AutoEllipsis = true;
        cellBancoText.Dock = DockStyle.Fill;
        cellBancoText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellBancoText.ForeColor = Color.FromArgb(98, 108, 124);
        cellBancoText.Location = new Point(28, 0);
        cellBancoText.Name = "cellBancoText";
        cellBancoText.Padding = new Padding(2, 0, 0, 0);
        cellBancoText.Size = new Size(298, 40);
        cellBancoText.TabIndex = 0;
        cellBancoText.Text = "Banco de Dados:  -";
        cellBancoText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellBancoDivider
        // 
        cellBancoDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellBancoDivider.Dock = DockStyle.Right;
        cellBancoDivider.Location = new Point(326, 0);
        cellBancoDivider.Margin = new Padding(0);
        cellBancoDivider.Name = "cellBancoDivider";
        cellBancoDivider.Size = new Size(1, 40);
        cellBancoDivider.TabIndex = 1;
        // 
        // cellBancoIcon
        // 
        cellBancoIcon.Dock = DockStyle.Left;
        cellBancoIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellBancoIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellBancoIcon.Location = new Point(0, 0);
        cellBancoIcon.Name = "cellBancoIcon";
        cellBancoIcon.Size = new Size(28, 40);
        cellBancoIcon.TabIndex = 2;
        cellBancoIcon.Text = "";
        cellBancoIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellHora
        // 
        cellHora.BackColor = Color.Transparent;
        cellHora.Controls.Add(cellHoraText);
        cellHora.Controls.Add(cellHoraDivider);
        cellHora.Controls.Add(cellHoraIcon);
        cellHora.Dock = DockStyle.Fill;
        cellHora.Location = new Point(1117, 0);
        cellHora.Margin = new Padding(0);
        cellHora.Name = "cellHora";
        cellHora.Size = new Size(109, 40);
        cellHora.TabIndex = 4;
        // 
        // cellHoraText
        // 
        cellHoraText.AutoEllipsis = true;
        cellHoraText.Dock = DockStyle.Fill;
        cellHoraText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellHoraText.ForeColor = Color.FromArgb(98, 108, 124);
        cellHoraText.Location = new Point(28, 0);
        cellHoraText.Name = "cellHoraText";
        cellHoraText.Padding = new Padding(2, 0, 0, 0);
        cellHoraText.Size = new Size(80, 40);
        cellHoraText.TabIndex = 0;
        cellHoraText.Text = "--:--";
        cellHoraText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellHoraDivider
        // 
        cellHoraDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellHoraDivider.Dock = DockStyle.Right;
        cellHoraDivider.Location = new Point(108, 0);
        cellHoraDivider.Margin = new Padding(0);
        cellHoraDivider.Name = "cellHoraDivider";
        cellHoraDivider.Size = new Size(1, 40);
        cellHoraDivider.TabIndex = 1;
        // 
        // cellHoraIcon
        // 
        cellHoraIcon.Dock = DockStyle.Left;
        cellHoraIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellHoraIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellHoraIcon.Location = new Point(0, 0);
        cellHoraIcon.Name = "cellHoraIcon";
        cellHoraIcon.Size = new Size(28, 40);
        cellHoraIcon.TabIndex = 2;
        cellHoraIcon.Text = "";
        cellHoraIcon.TextAlign = ContentAlignment.MiddleCenter;
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
        cellData.Size = new Size(140, 40);
        cellData.TabIndex = 5;
        // 
        // cellDataText
        // 
        cellDataText.AutoEllipsis = true;
        cellDataText.Dock = DockStyle.Fill;
        cellDataText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellDataText.ForeColor = Color.FromArgb(98, 108, 124);
        cellDataText.Location = new Point(28, 0);
        cellDataText.Name = "cellDataText";
        cellDataText.Padding = new Padding(2, 0, 0, 0);
        cellDataText.Size = new Size(112, 40);
        cellDataText.TabIndex = 0;
        cellDataText.Text = "--/--/----";
        cellDataText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellDataIcon
        // 
        cellDataIcon.Dock = DockStyle.Left;
        cellDataIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellDataIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellDataIcon.Location = new Point(0, 0);
        cellDataIcon.Name = "cellDataIcon";
        cellDataIcon.Size = new Size(28, 40);
        cellDataIcon.TabIndex = 1;
        cellDataIcon.Text = "";
        cellDataIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // BalancaForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        ClientSize = new Size(1366, 720);
        Controls.Add(rootLayout);
        Font = new Font("Segoe UI", 9F);
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1180, 680);
        Name = "BalancaForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Cadastro de Balança";
        WindowState = FormWindowState.Maximized;
        rootLayout.ResumeLayout(false);
        customTitleBarPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).EndInit();
        headerTitleIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).EndInit();
        contentLayout.ResumeLayout(false);
        balancasCard.ResumeLayout(false);
        novoBalancaButtonPanel.ResumeLayout(false);
        searchPanel.ResumeLayout(false);
        searchPanel.PerformLayout();
        listaHeaderPanel.ResumeLayout(false);
        dadosCard.ResumeLayout(false);
        dadosPanel.ResumeLayout(false);
        identificacaoTable.ResumeLayout(false);
        nomeCampoPanel.ResumeLayout(false);
        nomeBalancaInputPanel.ResumeLayout(false);
        nomeBalancaInputPanel.PerformLayout();
        setorCampoPanel.ResumeLayout(false);
        setorInputPanel.ResumeLayout(false);
        identificacaoLocalCampoPanel.ResumeLayout(false);
        identificacaoLocalInputPanel.ResumeLayout(false);
        identificacaoLocalInputPanel.PerformLayout();
        situacaoCampoPanel.ResumeLayout(false);
        situacaoInputPanel.ResumeLayout(false);
        situacaoInputPanel.PerformLayout();
        conexaoTable.ResumeLayout(false);
        tipoConexaoCampoPanel.ResumeLayout(false);
        tipoConexaoInputPanel.ResumeLayout(false);
        tcpPanel.ResumeLayout(false);
        tcpTable.ResumeLayout(false);
        enderecoIpCampoPanel.ResumeLayout(false);
        enderecoIpInputPanel.ResumeLayout(false);
        enderecoIpInputPanel.PerformLayout();
        portaTcpCampoPanel.ResumeLayout(false);
        portaTcpInputPanel.ResumeLayout(false);
        portaTcpInputPanel.PerformLayout();
        portaSerialPanel.ResumeLayout(false);
        portaSerialInputPanel.ResumeLayout(false);
        portaSerialInputPanel.PerformLayout();
        serialPanel.ResumeLayout(false);
        serialTable.ResumeLayout(false);
        baudRateCampoPanel.ResumeLayout(false);
        baudRateInputPanel.ResumeLayout(false);
        baudRateInputPanel.PerformLayout();
        dataBitsCampoPanel.ResumeLayout(false);
        dataBitsInputPanel.ResumeLayout(false);
        dataBitsInputPanel.PerformLayout();
        paridadeCampoPanel.ResumeLayout(false);
        paridadeInputPanel.ResumeLayout(false);
        stopBitsCampoPanel.ResumeLayout(false);
        stopBitsInputPanel.ResumeLayout(false);
        flowControlCampoPanel.ResumeLayout(false);
        flowControlInputPanel.ResumeLayout(false);
        protocoloCampoPanel.ResumeLayout(false);
        protocoloInputPanel.ResumeLayout(false);
        protocoloInputPanel.PerformLayout();
        observacaoInputPanel.ResumeLayout(false);
        observacaoInputPanel.PerformLayout();
        resumoCard.ResumeLayout(false);
        footerBar.ResumeLayout(false);
        footerBarLayout.ResumeLayout(false);
        cellUser.ResumeLayout(false);
        cellTerminal.ResumeLayout(false);
        cellEmpresa.ResumeLayout(false);
        cellBanco.ResumeLayout(false);
        cellHora.ResumeLayout(false);
        cellData.ResumeLayout(false);
        ResumeLayout(false);
    }
}
