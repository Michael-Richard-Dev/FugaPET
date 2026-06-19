using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

partial class PermissaoForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootLayout;
    private Panel customTitleBarPanel;
    private Label menuHeaderLabel;
    private PictureBox companyLogoPictureBox;
    private Label headerDividerLabel;
    private RoundedPanel headerTitleIconPanel;
    private PictureBox headerTitleIconPictureBox;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private RoundedPanel sapStatusPanel;
    private Label sapStatusDotLabel;
    private Label sapStatusLabel;
    private Label minimizeWindowLabel;
    private Label maximizeWindowLabel;
    private Label closeWindowLabel;
    private Panel contentPanel;
    private TableLayoutPanel contentLayout;
    private RoundedPanel heroPanel;
    private PictureBox heroLogoPictureBox;
    private Label heroTitleLabel;
    private Label heroSubtitleLabel;
    private Panel heroIllustrationPanel;
    private RoundedPanel heroShieldPanel;
    private Label heroShieldLabel;
    private RoundedPanel heroLockPanel;
    private Label heroLockLabel;
    private TableLayoutPanel bodyLayout;
    private RoundedPanel profilesCard;
    private RoundedPanel detailsCard;
    private RoundedPanel summaryCard;
    private Label profilesTitleLabel;
    private RoundedPanel profilesSearchPanel;
    private Label profilesSearchIconLabel;
    private RoundedPanel profilesTablePanel;
    private Label profilesHeaderProfileLabel;
    private Label profilesHeaderUsersLabel;
    private Label profilesHeaderStatusLabel;
    private DataGridView profilesDataGridView;
    private TextBox searchTextBox;
    private Button novoPerfilButton;
    private Button duplicarButton;
    private RoundedPanel novoPerfilButtonPanel;
    private Label novoPerfilIconLabel;
    private Label novoPerfilTextLabel;
    private RoundedPanel duplicarButtonPanel;
    private Label duplicarIconLabel;
    private Label duplicarTextLabel;
    private Label profilesFooterLabel;
    private Label detailsTitleIconLabel;
    private Label detailsTitleLabel;
    private Label nomePerfilLabel;
    private TextBox nomePerfilTextBox;
    private RoundedPanel nomePerfilInputPanel;
    private Label nivelAcessoLabel;
    private ComboBox nivelAcessoComboBox;
    private RoundedPanel nivelAcessoInputPanel;
    private Label descricaoLabel;
    private TextBox descricaoTextBox;
    private RoundedPanel descricaoInputPanel;
    private Label situacaoLabel;
    private ComboBox situacaoComboBox;
    private RoundedPanel situacaoInputPanel;
    private Label usuariosLabel;
    private TextBox usuariosTextBox;
    private RoundedPanel usuariosInputPanel;
    private Label detailsTopDividerLabel;
    private Label permissionsTitleLabel;
    private Label permissionsDividerLabel;
    private Label permissaoPainelIconLabel;
    private Label permissaoPainelLabel;
    private CheckBox permissaoPainelVisualizarCheckBox;
    private Label permissaoConsultaIconLabel;
    private Label permissaoConsultaLabel;
    private CheckBox permissaoConsultaVisualizarCheckBox;
    private Label permissaoRelatoriosIconLabel;
    private Label permissaoRelatoriosLabel;
    private CheckBox permissaoRelatoriosVisualizarCheckBox;
    private Label permissaoLeituraIconLabel;
    private Label permissaoLeituraLabel;
    private CheckBox permissaoLeituraVisualizarCheckBox;
    private CheckBox permissaoLeituraExecutarCheckBox;
    private Label permissaoEtiquetasIconLabel;
    private Label permissaoEtiquetasLabel;
    private CheckBox permissaoEtiquetasVisualizarCheckBox;
    private CheckBox permissaoEtiquetasImprimirCheckBox;
    private Label permissaoSapIconLabel;
    private Label permissaoSapLabel;
    private CheckBox permissaoSapVisualizarCheckBox;
    private Label permissaoOrdensIconLabel;
    private Label permissaoOrdensLabel;
    private CheckBox permissaoOrdensVisualizarCheckBox;
    private Label permissaoHistoricoIconLabel;
    private Label permissaoHistoricoLabel;
    private CheckBox permissaoHistoricoVisualizarCheckBox;
    private Label permissaoConfigIconLabel;
    private Label permissaoConfigLabel;
    private CheckBox permissaoConfigSemAcessoCheckBox;
    private Label permissaoCadastroIconLabel;
    private Label permissaoCadastroLabel;
    private CheckBox permissaoCadastroVisualizarCheckBox;
    private Panel permissionsVerticalDivider1;
    private Panel permissionsVerticalDivider2;
    private Panel permissionsHorizontalDivider1;
    private Panel permissionsHorizontalDivider2;
    private Label permissionsHorizontalDivider3;
    private Label summaryTitleLabel;
    private Panel summaryDividerLabel;
    private Label summaryPerfilIconLabel;
    private Label summaryPerfilCaptionLabel;
    private Label summaryPerfilValueLabel;
    private Label summaryNivelIconLabel;
    private Label summaryNivelCaptionLabel;
    private Label summaryNivelValueLabel;
    private Label summarySituacaoIconLabel;
    private Label summarySituacaoCaptionLabel;
    private Label summarySituacaoValueLabel;
    private Label summaryPermissionsIconLabel;
    private Label summaryPermissionsCaptionLabel;
    private Label summaryPermissionsValueLabel;
    private Label summaryUsuariosIconLabel;
    private Label summaryUsuariosCaptionLabel;
    private Label summaryUsuariosValueLabel;
    private Label summaryTipIconLabel;
    private Label summaryTipTextLabel;
    private Button salvarButton;
    private Button novoButton;
    private Button excluirButton;
    private Panel footerBar;
    private Panel cellUser;
    private Label cellUserIcon;
    private Label cellUserText;
    private Panel cellUserDivider;
    private TableLayoutPanel footerBarLayout;
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
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;

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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PermissaoForm));
        DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
        DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
        rootLayout = new TableLayoutPanel();
        contentPanel = new Panel();
        contentLayout = new TableLayoutPanel();
        bodyLayout = new TableLayoutPanel();
        profilesCard = new RoundedPanel();
        profilesCheckMarkLabel = new Label();
        duplicarButtonPanel = new RoundedPanel();
        duplicarIconLabel = new Label();
        duplicarTextLabel = new Label();
        novoPerfilButtonPanel = new RoundedPanel();
        novoPerfilIconLabel = new Label();
        novoPerfilTextLabel = new Label();
        profilesSearchPanel = new RoundedPanel();
        profilesSearchIconLabel = new Label();
        searchTextBox = new TextBox();
        profilesTablePanel = new RoundedPanel();
        profilesHeaderProfileLabel = new Label();
        profilesHeaderUsersLabel = new Label();
        profilesHeaderStatusLabel = new Label();
        profilesFooterLabel = new Label();
        profilesTitleLabel = new Label();
        detailsCard = new RoundedPanel();
        permissionsHorizontalDivider3 = new Label();
        permissionsHorizontalDivider2 = new Panel();
        permissaoEtiquetasImprimirCheckBox = new CheckBox();
        permissaoLeituraExecutarCheckBox = new CheckBox();
        detailsTitleIconLabel = new Label();
        detailsTitleLabel = new Label();
        nomePerfilLabel = new Label();
        nomePerfilInputPanel = new RoundedPanel();
        nomePerfilTextBox = new TextBox();
        descricaoLabel = new Label();
        descricaoInputPanel = new RoundedPanel();
        descricaoTextBox = new TextBox();
        situacaoInputPanel = new RoundedPanel();
        situacaoComboBox = new ComboBox();
        detailsTopDividerLabel = new Label();
        permissionsTitleLabel = new Label();
        permissionsDividerLabel = new Label();
        permissionsVerticalDivider1 = new Panel();
        permissionsVerticalDivider2 = new Panel();
        permissionsHorizontalDivider1 = new Panel();
        permissaoPainelIconLabel = new Label();
        permissaoPainelLabel = new Label();
        permissaoPainelVisualizarCheckBox = new CheckBox();
        permissaoConsultaIconLabel = new Label();
        permissaoConsultaLabel = new Label();
        permissaoConsultaVisualizarCheckBox = new CheckBox();
        permissaoRelatoriosIconLabel = new Label();
        permissaoRelatoriosLabel = new Label();
        permissaoRelatoriosVisualizarCheckBox = new CheckBox();
        permissaoLeituraIconLabel = new Label();
        permissaoLeituraLabel = new Label();
        permissaoLeituraVisualizarCheckBox = new CheckBox();
        permissaoEtiquetasIconLabel = new Label();
        permissaoEtiquetasLabel = new Label();
        permissaoEtiquetasVisualizarCheckBox = new CheckBox();
        permissaoSapIconLabel = new Label();
        permissaoSapLabel = new Label();
        permissaoOrdensIconLabel = new Label();
        permissaoOrdensLabel = new Label();
        permissaoOrdensVisualizarCheckBox = new CheckBox();
        permissaoHistoricoIconLabel = new Label();
        permissaoHistoricoLabel = new Label();
        permissaoHistoricoVisualizarCheckBox = new CheckBox();
        permissaoConfigIconLabel = new Label();
        permissaoConfigLabel = new Label();
        permissaoConfigSemAcessoCheckBox = new CheckBox();
        permissaoCadastroIconLabel = new Label();
        permissaoCadastroLabel = new Label();
        permissaoCadastroVisualizarCheckBox = new CheckBox();
        permissaoSapVisualizarCheckBox = new CheckBox();
        situacaoLabel = new Label();
        summaryCard = new RoundedPanel();
        label1 = new Label();
        summaryTitleLabel = new Label();
        summaryDividerLabel = new Panel();
        summaryPerfilIconLabel = new Label();
        summaryPerfilCaptionLabel = new Label();
        summaryPerfilValueLabel = new Label();
        summaryNivelIconLabel = new Label();
        summaryNivelCaptionLabel = new Label();
        summaryNivelValueLabel = new Label();
        summarySituacaoIconLabel = new Label();
        summarySituacaoCaptionLabel = new Label();
        summarySituacaoValueLabel = new Label();
        summaryPermissionsIconLabel = new Label();
        summaryPermissionsCaptionLabel = new Label();
        summaryPermissionsValueLabel = new Label();
        summaryUsuariosIconLabel = new Label();
        summaryUsuariosCaptionLabel = new Label();
        summaryUsuariosValueLabel = new Label();
        summaryTipIconLabel = new Label();
        summaryTipTextLabel = new Label();
        salvarButton = new Button();
        novoButton = new Button();
        excluirButton = new Button();
        _workspacePanel = new Panel();
        workspaceConteudo = new TableLayoutPanel();
        esquerdaCard = new RoundedPanel();
        esquerdaTitulo = new Label();
        _modulosListBox = new ListBox();
        _rodapeListaLabel = new Label();
        direitaCard = new RoundedPanel();
        _tituloPermissoesPerfilLabel = new Label();
        restaurarButton = new RoundedIconButton();
        salvarPermissoesButton = new RoundedIconButton();
        _filtrosCardPanel = new RoundedPanel();
        _limparFiltrosButtonPanel = new RoundedPanel();
        _limparFiltrosButton = new RoundedIconButton();
        _pesquisarFiltrosButtonPanel = new RoundedPanel();
        _pesquisarFiltrosButton = new RoundedIconButton();
        filtrosIconLabel = new Label();
        filtrosTitulo = new Label();
        _perfilFiltroLabelUi = new Label();
        _moduloFiltroLabelUi = new Label();
        _recursoFiltroLabelUi = new Label();
        _statusFiltroLabelUi = new Label();
        _perfilFiltroPanel = new RoundedPanel();
        _filtroPerfilComboBox = new ComboBox();
        _moduloFiltroPanel = new RoundedPanel();
        _filtroModuloComboBox = new ComboBox();
        _recursoFiltroPanel = new RoundedPanel();
        recursoIcone = new Label();
        _filtroRecursoTextBox = new TextBox();
        _statusFiltroPanel = new RoundedPanel();
        _filtroStatusComboBox = new ComboBox();
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
        customTitleBarPanel = new Panel();
        menuHeaderLabel = new Label();
        companyLogoPictureBox = new PictureBox();
        headerDividerLabel = new Label();
        headerTitleIconPanel = new RoundedPanel();
        headerTitleIconPictureBox = new PictureBox();
        headerTitleLabel = new Label();
        headerSubtitleLabel = new Label();
        sapStatusPanel = new RoundedPanel();
        sapStatusDotLabel = new Label();
        sapStatusLabel = new Label();
        minimizeWindowLabel = new Label();
        maximizeWindowLabel = new Label();
        closeWindowLabel = new Label();
        nivelAcessoLabel = new Label();
        nivelAcessoInputPanel = new RoundedPanel();
        nivelAcessoComboBox = new ComboBox();
        usuariosLabel = new Label();
        usuariosInputPanel = new RoundedPanel();
        usuariosTextBox = new TextBox();
        novoPerfilButton = new Button();
        duplicarButton = new Button();
        profilesDataGridView = new DataGridView();
        dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
        dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
        heroPanel = new RoundedPanel();
        heroLogoPictureBox = new PictureBox();
        heroTitleLabel = new Label();
        heroSubtitleLabel = new Label();
        heroIllustrationPanel = new Panel();
        heroShieldPanel = new RoundedPanel();
        heroShieldLabel = new Label();
        heroLockPanel = new RoundedPanel();
        heroLockLabel = new Label();
        rootLayout.SuspendLayout();
        contentPanel.SuspendLayout();
        contentLayout.SuspendLayout();
        bodyLayout.SuspendLayout();
        profilesCard.SuspendLayout();
        duplicarButtonPanel.SuspendLayout();
        novoPerfilButtonPanel.SuspendLayout();
        profilesSearchPanel.SuspendLayout();
        profilesTablePanel.SuspendLayout();
        detailsCard.SuspendLayout();
        nomePerfilInputPanel.SuspendLayout();
        descricaoInputPanel.SuspendLayout();
        situacaoInputPanel.SuspendLayout();
        summaryCard.SuspendLayout();
        _workspacePanel.SuspendLayout();
        workspaceConteudo.SuspendLayout();
        esquerdaCard.SuspendLayout();
        direitaCard.SuspendLayout();
        _filtrosCardPanel.SuspendLayout();
        _limparFiltrosButtonPanel.SuspendLayout();
        _pesquisarFiltrosButtonPanel.SuspendLayout();
        _perfilFiltroPanel.SuspendLayout();
        _moduloFiltroPanel.SuspendLayout();
        _recursoFiltroPanel.SuspendLayout();
        _statusFiltroPanel.SuspendLayout();
        footerBar.SuspendLayout();
        footerBarLayout.SuspendLayout();
        cellUser.SuspendLayout();
        cellTerminal.SuspendLayout();
        cellEmpresa.SuspendLayout();
        cellBanco.SuspendLayout();
        cellHora.SuspendLayout();
        cellData.SuspendLayout();
        customTitleBarPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).BeginInit();
        headerTitleIconPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).BeginInit();
        sapStatusPanel.SuspendLayout();
        nivelAcessoInputPanel.SuspendLayout();
        usuariosInputPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)profilesDataGridView).BeginInit();
        heroPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)heroLogoPictureBox).BeginInit();
        heroIllustrationPanel.SuspendLayout();
        heroShieldPanel.SuspendLayout();
        heroLockPanel.SuspendLayout();
        SuspendLayout();
        // 
        // rootLayout
        // 
        rootLayout.BackColor = Color.FromArgb(247, 248, 250);
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(contentPanel, 0, 1);
        rootLayout.Controls.Add(footerBar, 0, 2);
        rootLayout.Controls.Add(customTitleBarPanel, 0, 0);
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
        // contentPanel
        // 
        contentPanel.AutoScroll = true;
        contentPanel.BackColor = Color.FromArgb(247, 248, 250);
        contentPanel.Controls.Add(contentLayout);
        contentPanel.Dock = DockStyle.Fill;
        contentPanel.Location = new Point(0, 52);
        contentPanel.Margin = new Padding(0);
        contentPanel.Name = "contentPanel";
        contentPanel.Padding = new Padding(18, 14, 18, 14);
        contentPanel.Size = new Size(1366, 630);
        contentPanel.TabIndex = 1;
        // 
        // contentLayout
        // 
        contentLayout.ColumnCount = 1;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contentLayout.Controls.Add(bodyLayout, 0, 0);
        contentLayout.Controls.Add(_workspacePanel, 0, 0);
        contentLayout.Dock = DockStyle.Fill;
        contentLayout.Location = new Point(18, 14);
        contentLayout.Name = "contentLayout";
        contentLayout.RowCount = 1;
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        contentLayout.Size = new Size(1330, 602);
        contentLayout.TabIndex = 0;
        // 
        // bodyLayout
        // 
        bodyLayout.ColumnCount = 3;
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43F));
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        bodyLayout.Controls.Add(profilesCard, 0, 0);
        bodyLayout.Controls.Add(detailsCard, 1, 0);
        bodyLayout.Controls.Add(summaryCard, 2, 0);
        bodyLayout.Dock = DockStyle.Fill;
        bodyLayout.Location = new Point(3, 585);
        bodyLayout.Name = "bodyLayout";
        bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        bodyLayout.Size = new Size(1324, 14);
        bodyLayout.TabIndex = 1;
        bodyLayout.Visible = false;
        // 
        // profilesCard
        // 
        profilesCard.BackColor = Color.Transparent;
        profilesCard.BorderColor = Color.FromArgb(226, 232, 240);
        profilesCard.Controls.Add(profilesCheckMarkLabel);
        profilesCard.Controls.Add(duplicarButtonPanel);
        profilesCard.Controls.Add(novoPerfilButtonPanel);
        profilesCard.Controls.Add(profilesSearchPanel);
        profilesCard.Controls.Add(profilesTablePanel);
        profilesCard.Controls.Add(profilesFooterLabel);
        profilesCard.Controls.Add(profilesTitleLabel);
        profilesCard.Dock = DockStyle.Fill;
        profilesCard.Location = new Point(0, 0);
        profilesCard.Margin = new Padding(0, 0, 12, 0);
        profilesCard.Name = "profilesCard";
        profilesCard.ShadowBlur = 0;
        profilesCard.ShadowOffsetY = 0;
        profilesCard.Size = new Size(411, 14);
        profilesCard.TabIndex = 0;
        // 
        // profilesCheckMarkLabel
        // 
        profilesCheckMarkLabel.Font = new Font("Segoe UI Symbol", 14F, FontStyle.Bold);
        profilesCheckMarkLabel.ForeColor = Color.FromArgb(15, 23, 42);
        profilesCheckMarkLabel.Location = new Point(16, 19);
        profilesCheckMarkLabel.Name = "profilesCheckMarkLabel";
        profilesCheckMarkLabel.Size = new Size(26, 26);
        profilesCheckMarkLabel.TabIndex = 7;
        profilesCheckMarkLabel.Text = "✓";
        profilesCheckMarkLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // duplicarButtonPanel
        // 
        duplicarButtonPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        duplicarButtonPanel.BackColor = Color.Transparent;
        duplicarButtonPanel.BorderColor = Color.FromArgb(203, 213, 225);
        duplicarButtonPanel.BorderRadius = 4;
        duplicarButtonPanel.Controls.Add(duplicarIconLabel);
        duplicarButtonPanel.Controls.Add(duplicarTextLabel);
        duplicarButtonPanel.Location = new Point(296, 18);
        duplicarButtonPanel.Name = "duplicarButtonPanel";
        duplicarButtonPanel.ShadowBlur = 0;
        duplicarButtonPanel.ShadowOffsetY = 0;
        duplicarButtonPanel.Size = new Size(99, 27);
        duplicarButtonPanel.TabIndex = 4;
        // 
        // duplicarIconLabel
        // 
        duplicarIconLabel.BackColor = Color.Transparent;
        duplicarIconLabel.Font = new Font("Segoe MDL2 Assets", 9F);
        duplicarIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        duplicarIconLabel.Location = new Point(8, 4);
        duplicarIconLabel.Name = "duplicarIconLabel";
        duplicarIconLabel.Size = new Size(18, 21);
        duplicarIconLabel.TabIndex = 0;
        duplicarIconLabel.Text = "";
        duplicarIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // duplicarTextLabel
        // 
        duplicarTextLabel.BackColor = Color.Transparent;
        duplicarTextLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        duplicarTextLabel.ForeColor = Color.FromArgb(15, 23, 42);
        duplicarTextLabel.Location = new Point(29, 3);
        duplicarTextLabel.Name = "duplicarTextLabel";
        duplicarTextLabel.Size = new Size(51, 21);
        duplicarTextLabel.TabIndex = 1;
        duplicarTextLabel.Text = "Duplicar";
        duplicarTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // novoPerfilButtonPanel
        // 
        novoPerfilButtonPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        novoPerfilButtonPanel.BackColor = Color.Transparent;
        novoPerfilButtonPanel.BorderColor = Color.FromArgb(203, 213, 225);
        novoPerfilButtonPanel.BorderRadius = 4;
        novoPerfilButtonPanel.Controls.Add(novoPerfilIconLabel);
        novoPerfilButtonPanel.Controls.Add(novoPerfilTextLabel);
        novoPerfilButtonPanel.Location = new Point(191, 18);
        novoPerfilButtonPanel.Name = "novoPerfilButtonPanel";
        novoPerfilButtonPanel.ShadowBlur = 0;
        novoPerfilButtonPanel.ShadowOffsetY = 0;
        novoPerfilButtonPanel.Size = new Size(99, 27);
        novoPerfilButtonPanel.TabIndex = 3;
        // 
        // novoPerfilIconLabel
        // 
        novoPerfilIconLabel.BackColor = Color.Transparent;
        novoPerfilIconLabel.Font = new Font("Segoe MDL2 Assets", 9F);
        novoPerfilIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        novoPerfilIconLabel.Location = new Point(8, 3);
        novoPerfilIconLabel.Name = "novoPerfilIconLabel";
        novoPerfilIconLabel.Size = new Size(18, 21);
        novoPerfilIconLabel.TabIndex = 0;
        novoPerfilIconLabel.Text = "";
        novoPerfilIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // novoPerfilTextLabel
        // 
        novoPerfilTextLabel.BackColor = Color.Transparent;
        novoPerfilTextLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        novoPerfilTextLabel.ForeColor = Color.FromArgb(15, 23, 42);
        novoPerfilTextLabel.Location = new Point(29, 3);
        novoPerfilTextLabel.Name = "novoPerfilTextLabel";
        novoPerfilTextLabel.Size = new Size(67, 21);
        novoPerfilTextLabel.TabIndex = 1;
        novoPerfilTextLabel.Text = "Novo Perfil";
        novoPerfilTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // profilesSearchPanel
        // 
        profilesSearchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        profilesSearchPanel.BackColor = Color.Transparent;
        profilesSearchPanel.BorderColor = Color.FromArgb(214, 219, 226);
        profilesSearchPanel.BorderRadius = 7;
        profilesSearchPanel.Controls.Add(profilesSearchIconLabel);
        profilesSearchPanel.Controls.Add(searchTextBox);
        profilesSearchPanel.Location = new Point(16, 60);
        profilesSearchPanel.Name = "profilesSearchPanel";
        profilesSearchPanel.ShadowBlur = 0;
        profilesSearchPanel.ShadowOffsetY = 0;
        profilesSearchPanel.Size = new Size(379, 34);
        profilesSearchPanel.TabIndex = 2;
        // 
        // profilesSearchIconLabel
        // 
        profilesSearchIconLabel.BackColor = Color.Transparent;
        profilesSearchIconLabel.Font = new Font("Segoe MDL2 Assets", 11F);
        profilesSearchIconLabel.ForeColor = Color.FromArgb(107, 114, 128);
        profilesSearchIconLabel.Location = new Point(8, 8);
        profilesSearchIconLabel.Name = "profilesSearchIconLabel";
        profilesSearchIconLabel.Size = new Size(22, 24);
        profilesSearchIconLabel.TabIndex = 0;
        profilesSearchIconLabel.Text = "";
        profilesSearchIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // searchTextBox
        // 
        searchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchTextBox.BorderStyle = BorderStyle.None;
        searchTextBox.Font = new Font("Segoe UI", 9F);
        searchTextBox.Location = new Point(36, 8);
        searchTextBox.Name = "searchTextBox";
        searchTextBox.PlaceholderText = "Buscar perfil...";
        searchTextBox.Size = new Size(340, 16);
        searchTextBox.TabIndex = 4;
        // 
        // profilesTablePanel
        // 
        profilesTablePanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        profilesTablePanel.BackColor = Color.Transparent;
        profilesTablePanel.BorderColor = Color.FromArgb(226, 232, 240);
        profilesTablePanel.BorderRadius = 5;
        profilesTablePanel.Controls.Add(profilesHeaderProfileLabel);
        profilesTablePanel.Controls.Add(profilesHeaderUsersLabel);
        profilesTablePanel.Controls.Add(profilesHeaderStatusLabel);
        profilesTablePanel.Location = new Point(16, 108);
        profilesTablePanel.Name = "profilesTablePanel";
        profilesTablePanel.ShadowBlur = 0;
        profilesTablePanel.ShadowOffsetY = 0;
        profilesTablePanel.Size = new Size(379, 0);
        profilesTablePanel.TabIndex = 5;
        // 
        // profilesHeaderProfileLabel
        // 
        profilesHeaderProfileLabel.BackColor = Color.Transparent;
        profilesHeaderProfileLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        profilesHeaderProfileLabel.ForeColor = Color.FromArgb(51, 65, 85);
        profilesHeaderProfileLabel.Location = new Point(16, 9);
        profilesHeaderProfileLabel.Name = "profilesHeaderProfileLabel";
        profilesHeaderProfileLabel.Size = new Size(138, 20);
        profilesHeaderProfileLabel.TabIndex = 0;
        profilesHeaderProfileLabel.Text = "Perfil";
        profilesHeaderProfileLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // profilesHeaderUsersLabel
        // 
        profilesHeaderUsersLabel.BackColor = Color.Transparent;
        profilesHeaderUsersLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        profilesHeaderUsersLabel.ForeColor = Color.FromArgb(51, 65, 85);
        profilesHeaderUsersLabel.Location = new Point(183, 9);
        profilesHeaderUsersLabel.Name = "profilesHeaderUsersLabel";
        profilesHeaderUsersLabel.Size = new Size(122, 20);
        profilesHeaderUsersLabel.TabIndex = 1;
        profilesHeaderUsersLabel.Text = "Usuários vinculados";
        profilesHeaderUsersLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // profilesHeaderStatusLabel
        // 
        profilesHeaderStatusLabel.BackColor = Color.Transparent;
        profilesHeaderStatusLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        profilesHeaderStatusLabel.ForeColor = Color.FromArgb(51, 65, 85);
        profilesHeaderStatusLabel.Location = new Point(311, 9);
        profilesHeaderStatusLabel.Name = "profilesHeaderStatusLabel";
        profilesHeaderStatusLabel.Size = new Size(52, 20);
        profilesHeaderStatusLabel.TabIndex = 2;
        profilesHeaderStatusLabel.Text = "Situação";
        profilesHeaderStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // profilesFooterLabel
        // 
        profilesFooterLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        profilesFooterLabel.Font = new Font("Segoe UI", 8.5F);
        profilesFooterLabel.ForeColor = Color.FromArgb(71, 85, 105);
        profilesFooterLabel.Location = new Point(20, -14);
        profilesFooterLabel.Name = "profilesFooterLabel";
        profilesFooterLabel.Size = new Size(180, 22);
        profilesFooterLabel.TabIndex = 6;
        profilesFooterLabel.Text = "Exibindo 0 de 0 permissões";
        // 
        // profilesTitleLabel
        // 
        profilesTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        profilesTitleLabel.ForeColor = Color.FromArgb(15, 23, 42);
        profilesTitleLabel.Location = new Point(43, 21);
        profilesTitleLabel.Name = "profilesTitleLabel";
        profilesTitleLabel.Size = new Size(150, 24);
        profilesTitleLabel.TabIndex = 1;
        profilesTitleLabel.Text = "Perfis Cadastrados";
        profilesTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // detailsCard
        // 
        detailsCard.BackColor = Color.Transparent;
        detailsCard.BorderColor = Color.FromArgb(226, 232, 240);
        detailsCard.BorderRadius = 9;
        detailsCard.Controls.Add(permissionsHorizontalDivider3);
        detailsCard.Controls.Add(permissionsHorizontalDivider2);
        detailsCard.Controls.Add(permissaoEtiquetasImprimirCheckBox);
        detailsCard.Controls.Add(permissaoLeituraExecutarCheckBox);
        detailsCard.Controls.Add(detailsTitleIconLabel);
        detailsCard.Controls.Add(detailsTitleLabel);
        detailsCard.Controls.Add(nomePerfilLabel);
        detailsCard.Controls.Add(nomePerfilInputPanel);
        detailsCard.Controls.Add(descricaoLabel);
        detailsCard.Controls.Add(descricaoInputPanel);
        detailsCard.Controls.Add(situacaoInputPanel);
        detailsCard.Controls.Add(detailsTopDividerLabel);
        detailsCard.Controls.Add(permissionsTitleLabel);
        detailsCard.Controls.Add(permissionsDividerLabel);
        detailsCard.Controls.Add(permissionsVerticalDivider1);
        detailsCard.Controls.Add(permissionsVerticalDivider2);
        detailsCard.Controls.Add(permissionsHorizontalDivider1);
        detailsCard.Controls.Add(permissaoPainelIconLabel);
        detailsCard.Controls.Add(permissaoPainelLabel);
        detailsCard.Controls.Add(permissaoPainelVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoConsultaIconLabel);
        detailsCard.Controls.Add(permissaoConsultaLabel);
        detailsCard.Controls.Add(permissaoConsultaVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoRelatoriosIconLabel);
        detailsCard.Controls.Add(permissaoRelatoriosLabel);
        detailsCard.Controls.Add(permissaoRelatoriosVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoLeituraIconLabel);
        detailsCard.Controls.Add(permissaoLeituraLabel);
        detailsCard.Controls.Add(permissaoLeituraVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoEtiquetasIconLabel);
        detailsCard.Controls.Add(permissaoEtiquetasLabel);
        detailsCard.Controls.Add(permissaoEtiquetasVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoSapIconLabel);
        detailsCard.Controls.Add(permissaoSapLabel);
        detailsCard.Controls.Add(permissaoOrdensIconLabel);
        detailsCard.Controls.Add(permissaoOrdensLabel);
        detailsCard.Controls.Add(permissaoOrdensVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoHistoricoIconLabel);
        detailsCard.Controls.Add(permissaoHistoricoLabel);
        detailsCard.Controls.Add(permissaoHistoricoVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoConfigIconLabel);
        detailsCard.Controls.Add(permissaoConfigLabel);
        detailsCard.Controls.Add(permissaoConfigSemAcessoCheckBox);
        detailsCard.Controls.Add(permissaoCadastroIconLabel);
        detailsCard.Controls.Add(permissaoCadastroLabel);
        detailsCard.Controls.Add(permissaoCadastroVisualizarCheckBox);
        detailsCard.Controls.Add(permissaoSapVisualizarCheckBox);
        detailsCard.Controls.Add(situacaoLabel);
        detailsCard.Dock = DockStyle.Fill;
        detailsCard.Location = new Point(423, 0);
        detailsCard.Margin = new Padding(0, 0, 12, 0);
        detailsCard.Name = "detailsCard";
        detailsCard.ShadowBlur = 0;
        detailsCard.ShadowOffsetY = 0;
        detailsCard.Size = new Size(557, 14);
        detailsCard.TabIndex = 1;
        // 
        // permissionsHorizontalDivider3
        // 
        permissionsHorizontalDivider3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        permissionsHorizontalDivider3.BackColor = Color.FromArgb(226, 232, 240);
        permissionsHorizontalDivider3.Location = new Point(24, 507);
        permissionsHorizontalDivider3.Name = "permissionsHorizontalDivider3";
        permissionsHorizontalDivider3.Size = new Size(171, 1);
        permissionsHorizontalDivider3.TabIndex = 51;
        // 
        // permissionsHorizontalDivider2
        // 
        permissionsHorizontalDivider2.BackColor = Color.FromArgb(226, 232, 240);
        permissionsHorizontalDivider2.Location = new Point(24, 386);
        permissionsHorizontalDivider2.Name = "permissionsHorizontalDivider2";
        permissionsHorizontalDivider2.Size = new Size(506, 1);
        permissionsHorizontalDivider2.TabIndex = 38;
        // 
        // permissaoEtiquetasImprimirCheckBox
        // 
        permissaoEtiquetasImprimirCheckBox.BackColor = Color.Transparent;
        permissaoEtiquetasImprimirCheckBox.Checked = true;
        permissaoEtiquetasImprimirCheckBox.CheckState = CheckState.Checked;
        permissaoEtiquetasImprimirCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoEtiquetasImprimirCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoEtiquetasImprimirCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoEtiquetasImprimirCheckBox.Location = new Point(292, 367);
        permissaoEtiquetasImprimirCheckBox.Name = "permissaoEtiquetasImprimirCheckBox";
        permissaoEtiquetasImprimirCheckBox.Size = new Size(66, 17);
        permissaoEtiquetasImprimirCheckBox.TabIndex = 24;
        permissaoEtiquetasImprimirCheckBox.Text = "Imprimir";
        permissaoEtiquetasImprimirCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoLeituraExecutarCheckBox
        // 
        permissaoLeituraExecutarCheckBox.BackColor = Color.Transparent;
        permissaoLeituraExecutarCheckBox.Checked = true;
        permissaoLeituraExecutarCheckBox.CheckState = CheckState.Checked;
        permissaoLeituraExecutarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoLeituraExecutarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoLeituraExecutarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoLeituraExecutarCheckBox.Location = new Point(118, 367);
        permissaoLeituraExecutarCheckBox.Name = "permissaoLeituraExecutarCheckBox";
        permissaoLeituraExecutarCheckBox.Size = new Size(66, 17);
        permissaoLeituraExecutarCheckBox.TabIndex = 21;
        permissaoLeituraExecutarCheckBox.Text = "Executar";
        permissaoLeituraExecutarCheckBox.UseVisualStyleBackColor = false;
        // 
        // detailsTitleIconLabel
        // 
        detailsTitleIconLabel.Font = new Font("Segoe MDL2 Assets", 14F);
        detailsTitleIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        detailsTitleIconLabel.Location = new Point(20, 18);
        detailsTitleIconLabel.Name = "detailsTitleIconLabel";
        detailsTitleIconLabel.Size = new Size(26, 26);
        detailsTitleIconLabel.TabIndex = 0;
        detailsTitleIconLabel.Text = "";
        detailsTitleIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // detailsTitleLabel
        // 
        detailsTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        detailsTitleLabel.ForeColor = Color.FromArgb(15, 23, 42);
        detailsTitleLabel.Location = new Point(52, 22);
        detailsTitleLabel.Name = "detailsTitleLabel";
        detailsTitleLabel.Size = new Size(180, 24);
        detailsTitleLabel.TabIndex = 1;
        detailsTitleLabel.Text = "Dados do Perfil";
        detailsTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // nomePerfilLabel
        // 
        nomePerfilLabel.Font = new Font("Segoe UI", 7.75F, FontStyle.Bold);
        nomePerfilLabel.ForeColor = Color.FromArgb(15, 23, 42);
        nomePerfilLabel.Location = new Point(24, 56);
        nomePerfilLabel.Name = "nomePerfilLabel";
        nomePerfilLabel.Size = new Size(180, 14);
        nomePerfilLabel.TabIndex = 2;
        nomePerfilLabel.Text = "Nome do Perfil *";
        // 
        // nomePerfilInputPanel
        // 
        nomePerfilInputPanel.BackColor = Color.Transparent;
        nomePerfilInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        nomePerfilInputPanel.BorderRadius = 5;
        nomePerfilInputPanel.Controls.Add(nomePerfilTextBox);
        nomePerfilInputPanel.Location = new Point(24, 73);
        nomePerfilInputPanel.Name = "nomePerfilInputPanel";
        nomePerfilInputPanel.ShadowBlur = 0;
        nomePerfilInputPanel.ShadowOffsetY = 0;
        nomePerfilInputPanel.Size = new Size(238, 33);
        nomePerfilInputPanel.TabIndex = 3;
        // 
        // nomePerfilTextBox
        // 
        nomePerfilTextBox.BackColor = Color.White;
        nomePerfilTextBox.BorderStyle = BorderStyle.None;
        nomePerfilTextBox.Font = new Font("Segoe UI", 9F);
        nomePerfilTextBox.Location = new Point(12, 10);
        nomePerfilTextBox.Name = "nomePerfilTextBox";
        nomePerfilTextBox.Size = new Size(214, 16);
        nomePerfilTextBox.TabIndex = 3;
        // 
        // descricaoLabel
        // 
        descricaoLabel.Font = new Font("Segoe UI", 7.75F, FontStyle.Bold);
        descricaoLabel.ForeColor = Color.FromArgb(15, 23, 42);
        descricaoLabel.Location = new Point(24, 117);
        descricaoLabel.Name = "descricaoLabel";
        descricaoLabel.Size = new Size(180, 19);
        descricaoLabel.TabIndex = 6;
        descricaoLabel.Text = "Descrição";
        // 
        // descricaoInputPanel
        // 
        descricaoInputPanel.BackColor = Color.Transparent;
        descricaoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        descricaoInputPanel.BorderRadius = 5;
        descricaoInputPanel.Controls.Add(descricaoTextBox);
        descricaoInputPanel.Location = new Point(24, 140);
        descricaoInputPanel.Name = "descricaoInputPanel";
        descricaoInputPanel.ShadowBlur = 0;
        descricaoInputPanel.ShadowOffsetY = 0;
        descricaoInputPanel.Size = new Size(506, 95);
        descricaoInputPanel.TabIndex = 7;
        // 
        // descricaoTextBox
        // 
        descricaoTextBox.BorderStyle = BorderStyle.None;
        descricaoTextBox.Font = new Font("Segoe UI", 9F);
        descricaoTextBox.Location = new Point(3, 3);
        descricaoTextBox.Multiline = true;
        descricaoTextBox.Name = "descricaoTextBox";
        descricaoTextBox.Size = new Size(500, 89);
        descricaoTextBox.TabIndex = 7;
        // 
        // situacaoInputPanel
        // 
        situacaoInputPanel.BackColor = Color.Transparent;
        situacaoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        situacaoInputPanel.BorderRadius = 5;
        situacaoInputPanel.Controls.Add(situacaoComboBox);
        situacaoInputPanel.Location = new Point(292, 73);
        situacaoInputPanel.Name = "situacaoInputPanel";
        situacaoInputPanel.ShadowBlur = 0;
        situacaoInputPanel.ShadowOffsetY = 0;
        situacaoInputPanel.Size = new Size(238, 33);
        situacaoInputPanel.TabIndex = 9;
        // 
        // situacaoComboBox
        // 
        situacaoComboBox.FlatStyle = FlatStyle.Flat;
        situacaoComboBox.Font = new Font("Segoe UI", 9F);
        situacaoComboBox.Items.AddRange(new object[] { "Ativo", "Inativo" });
        situacaoComboBox.Location = new Point(12, 4);
        situacaoComboBox.Name = "situacaoComboBox";
        situacaoComboBox.Size = new Size(214, 23);
        situacaoComboBox.TabIndex = 9;
        // 
        // detailsTopDividerLabel
        // 
        detailsTopDividerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        detailsTopDividerLabel.BackColor = Color.FromArgb(226, 232, 240);
        detailsTopDividerLabel.Location = new Point(24, 242);
        detailsTopDividerLabel.Name = "detailsTopDividerLabel";
        detailsTopDividerLabel.Size = new Size(506, 1);
        detailsTopDividerLabel.TabIndex = 33;
        // 
        // permissionsTitleLabel
        // 
        permissionsTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissionsTitleLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissionsTitleLabel.Location = new Point(24, 250);
        permissionsTitleLabel.Name = "permissionsTitleLabel";
        permissionsTitleLabel.Size = new Size(220, 24);
        permissionsTitleLabel.TabIndex = 12;
        permissionsTitleLabel.Text = "Permissões por Módulo";
        // 
        // permissionsDividerLabel
        // 
        permissionsDividerLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        permissionsDividerLabel.BackColor = Color.FromArgb(226, 232, 240);
        permissionsDividerLabel.Location = new Point(24, 446);
        permissionsDividerLabel.Name = "permissionsDividerLabel";
        permissionsDividerLabel.Size = new Size(506, 1);
        permissionsDividerLabel.TabIndex = 34;
        // 
        // permissionsVerticalDivider1
        // 
        permissionsVerticalDivider1.BackColor = Color.FromArgb(226, 232, 240);
        permissionsVerticalDivider1.Location = new Point(194, 279);
        permissionsVerticalDivider1.Name = "permissionsVerticalDivider1";
        permissionsVerticalDivider1.Size = new Size(1, 229);
        permissionsVerticalDivider1.TabIndex = 35;
        // 
        // permissionsVerticalDivider2
        // 
        permissionsVerticalDivider2.BackColor = Color.FromArgb(226, 232, 240);
        permissionsVerticalDivider2.Location = new Point(364, 280);
        permissionsVerticalDivider2.Name = "permissionsVerticalDivider2";
        permissionsVerticalDivider2.Size = new Size(1, 167);
        permissionsVerticalDivider2.TabIndex = 36;
        // 
        // permissionsHorizontalDivider1
        // 
        permissionsHorizontalDivider1.BackColor = Color.FromArgb(226, 232, 240);
        permissionsHorizontalDivider1.Location = new Point(24, 329);
        permissionsHorizontalDivider1.Name = "permissionsHorizontalDivider1";
        permissionsHorizontalDivider1.Size = new Size(506, 1);
        permissionsHorizontalDivider1.TabIndex = 37;
        // 
        // permissaoPainelIconLabel
        // 
        permissaoPainelIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoPainelIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoPainelIconLabel.Location = new Point(24, 286);
        permissaoPainelIconLabel.Name = "permissaoPainelIconLabel";
        permissaoPainelIconLabel.Size = new Size(22, 22);
        permissaoPainelIconLabel.TabIndex = 39;
        permissaoPainelIconLabel.Text = "";
        permissaoPainelIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoPainelLabel
        // 
        permissaoPainelLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoPainelLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoPainelLabel.Location = new Point(52, 286);
        permissaoPainelLabel.Name = "permissaoPainelLabel";
        permissaoPainelLabel.Size = new Size(126, 20);
        permissaoPainelLabel.TabIndex = 13;
        permissaoPainelLabel.Text = "Painel Inicial";
        // 
        // permissaoPainelVisualizarCheckBox
        // 
        permissaoPainelVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoPainelVisualizarCheckBox.Checked = true;
        permissaoPainelVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoPainelVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoPainelVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoPainelVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoPainelVisualizarCheckBox.Location = new Point(52, 309);
        permissaoPainelVisualizarCheckBox.Name = "permissaoPainelVisualizarCheckBox";
        permissaoPainelVisualizarCheckBox.Size = new Size(82, 17);
        permissaoPainelVisualizarCheckBox.TabIndex = 14;
        permissaoPainelVisualizarCheckBox.Text = "Visualizar";
        permissaoPainelVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoConsultaIconLabel
        // 
        permissaoConsultaIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoConsultaIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoConsultaIconLabel.Location = new Point(205, 286);
        permissaoConsultaIconLabel.Name = "permissaoConsultaIconLabel";
        permissaoConsultaIconLabel.Size = new Size(22, 22);
        permissaoConsultaIconLabel.TabIndex = 40;
        permissaoConsultaIconLabel.Text = "";
        permissaoConsultaIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoConsultaLabel
        // 
        permissaoConsultaLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoConsultaLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoConsultaLabel.Location = new Point(233, 286);
        permissaoConsultaLabel.Name = "permissaoConsultaLabel";
        permissaoConsultaLabel.Size = new Size(125, 20);
        permissaoConsultaLabel.TabIndex = 15;
        permissaoConsultaLabel.Text = "Consulta de OP";
        // 
        // permissaoConsultaVisualizarCheckBox
        // 
        permissaoConsultaVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoConsultaVisualizarCheckBox.Checked = true;
        permissaoConsultaVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoConsultaVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoConsultaVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoConsultaVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoConsultaVisualizarCheckBox.Location = new Point(233, 309);
        permissaoConsultaVisualizarCheckBox.Name = "permissaoConsultaVisualizarCheckBox";
        permissaoConsultaVisualizarCheckBox.Size = new Size(82, 20);
        permissaoConsultaVisualizarCheckBox.TabIndex = 16;
        permissaoConsultaVisualizarCheckBox.Text = "Visualizar";
        permissaoConsultaVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoRelatoriosIconLabel
        // 
        permissaoRelatoriosIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoRelatoriosIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoRelatoriosIconLabel.Location = new Point(376, 286);
        permissaoRelatoriosIconLabel.Name = "permissaoRelatoriosIconLabel";
        permissaoRelatoriosIconLabel.Size = new Size(22, 22);
        permissaoRelatoriosIconLabel.TabIndex = 41;
        permissaoRelatoriosIconLabel.Text = "";
        permissaoRelatoriosIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoRelatoriosLabel
        // 
        permissaoRelatoriosLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoRelatoriosLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoRelatoriosLabel.Location = new Point(404, 286);
        permissaoRelatoriosLabel.Name = "permissaoRelatoriosLabel";
        permissaoRelatoriosLabel.Size = new Size(150, 20);
        permissaoRelatoriosLabel.TabIndex = 17;
        permissaoRelatoriosLabel.Text = "Relatórios";
        // 
        // permissaoRelatoriosVisualizarCheckBox
        // 
        permissaoRelatoriosVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoRelatoriosVisualizarCheckBox.Checked = true;
        permissaoRelatoriosVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoRelatoriosVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoRelatoriosVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoRelatoriosVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoRelatoriosVisualizarCheckBox.Location = new Point(404, 309);
        permissaoRelatoriosVisualizarCheckBox.Name = "permissaoRelatoriosVisualizarCheckBox";
        permissaoRelatoriosVisualizarCheckBox.Size = new Size(82, 20);
        permissaoRelatoriosVisualizarCheckBox.TabIndex = 18;
        permissaoRelatoriosVisualizarCheckBox.Text = "Visualizar";
        permissaoRelatoriosVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoLeituraIconLabel
        // 
        permissaoLeituraIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoLeituraIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoLeituraIconLabel.Location = new Point(24, 344);
        permissaoLeituraIconLabel.Name = "permissaoLeituraIconLabel";
        permissaoLeituraIconLabel.Size = new Size(22, 22);
        permissaoLeituraIconLabel.TabIndex = 42;
        permissaoLeituraIconLabel.Text = "";
        permissaoLeituraIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoLeituraLabel
        // 
        permissaoLeituraLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoLeituraLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoLeituraLabel.Location = new Point(52, 344);
        permissaoLeituraLabel.Name = "permissaoLeituraLabel";
        permissaoLeituraLabel.Size = new Size(139, 20);
        permissaoLeituraLabel.TabIndex = 19;
        permissaoLeituraLabel.Text = "Leitura de Produção";
        // 
        // permissaoLeituraVisualizarCheckBox
        // 
        permissaoLeituraVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoLeituraVisualizarCheckBox.Checked = true;
        permissaoLeituraVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoLeituraVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoLeituraVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoLeituraVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoLeituraVisualizarCheckBox.Location = new Point(48, 367);
        permissaoLeituraVisualizarCheckBox.Name = "permissaoLeituraVisualizarCheckBox";
        permissaoLeituraVisualizarCheckBox.Size = new Size(74, 17);
        permissaoLeituraVisualizarCheckBox.TabIndex = 20;
        permissaoLeituraVisualizarCheckBox.Text = "Visualizar";
        permissaoLeituraVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoEtiquetasIconLabel
        // 
        permissaoEtiquetasIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoEtiquetasIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoEtiquetasIconLabel.Location = new Point(205, 344);
        permissaoEtiquetasIconLabel.Name = "permissaoEtiquetasIconLabel";
        permissaoEtiquetasIconLabel.Size = new Size(22, 22);
        permissaoEtiquetasIconLabel.TabIndex = 43;
        permissaoEtiquetasIconLabel.Text = "";
        permissaoEtiquetasIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoEtiquetasLabel
        // 
        permissaoEtiquetasLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoEtiquetasLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoEtiquetasLabel.Location = new Point(233, 344);
        permissaoEtiquetasLabel.Name = "permissaoEtiquetasLabel";
        permissaoEtiquetasLabel.Size = new Size(126, 20);
        permissaoEtiquetasLabel.TabIndex = 22;
        permissaoEtiquetasLabel.Text = "Etiquetas";
        // 
        // permissaoEtiquetasVisualizarCheckBox
        // 
        permissaoEtiquetasVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoEtiquetasVisualizarCheckBox.Checked = true;
        permissaoEtiquetasVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoEtiquetasVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoEtiquetasVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoEtiquetasVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoEtiquetasVisualizarCheckBox.Location = new Point(214, 367);
        permissaoEtiquetasVisualizarCheckBox.Name = "permissaoEtiquetasVisualizarCheckBox";
        permissaoEtiquetasVisualizarCheckBox.Size = new Size(84, 17);
        permissaoEtiquetasVisualizarCheckBox.TabIndex = 23;
        permissaoEtiquetasVisualizarCheckBox.Text = "Visualizar";
        permissaoEtiquetasVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoSapIconLabel
        // 
        permissaoSapIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoSapIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoSapIconLabel.Location = new Point(376, 344);
        permissaoSapIconLabel.Name = "permissaoSapIconLabel";
        permissaoSapIconLabel.Size = new Size(22, 22);
        permissaoSapIconLabel.TabIndex = 44;
        permissaoSapIconLabel.Text = "";
        permissaoSapIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoSapLabel
        // 
        permissaoSapLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoSapLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoSapLabel.Location = new Point(404, 344);
        permissaoSapLabel.Name = "permissaoSapLabel";
        permissaoSapLabel.Size = new Size(150, 20);
        permissaoSapLabel.TabIndex = 25;
        permissaoSapLabel.Text = "Integração SAP";
        // 
        // permissaoOrdensIconLabel
        // 
        permissaoOrdensIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoOrdensIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoOrdensIconLabel.Location = new Point(24, 402);
        permissaoOrdensIconLabel.Name = "permissaoOrdensIconLabel";
        permissaoOrdensIconLabel.Size = new Size(22, 22);
        permissaoOrdensIconLabel.TabIndex = 45;
        permissaoOrdensIconLabel.Text = "";
        permissaoOrdensIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoOrdensLabel
        // 
        permissaoOrdensLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoOrdensLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoOrdensLabel.Location = new Point(52, 402);
        permissaoOrdensLabel.Name = "permissaoOrdensLabel";
        permissaoOrdensLabel.Size = new Size(136, 20);
        permissaoOrdensLabel.TabIndex = 27;
        permissaoOrdensLabel.Text = "Ordens em Andamento";
        // 
        // permissaoOrdensVisualizarCheckBox
        // 
        permissaoOrdensVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoOrdensVisualizarCheckBox.Checked = true;
        permissaoOrdensVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoOrdensVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoOrdensVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoOrdensVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoOrdensVisualizarCheckBox.Location = new Point(52, 425);
        permissaoOrdensVisualizarCheckBox.Name = "permissaoOrdensVisualizarCheckBox";
        permissaoOrdensVisualizarCheckBox.Size = new Size(82, 20);
        permissaoOrdensVisualizarCheckBox.TabIndex = 28;
        permissaoOrdensVisualizarCheckBox.Text = "Visualizar";
        permissaoOrdensVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoHistoricoIconLabel
        // 
        permissaoHistoricoIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoHistoricoIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoHistoricoIconLabel.Location = new Point(205, 402);
        permissaoHistoricoIconLabel.Name = "permissaoHistoricoIconLabel";
        permissaoHistoricoIconLabel.Size = new Size(22, 22);
        permissaoHistoricoIconLabel.TabIndex = 46;
        permissaoHistoricoIconLabel.Text = "";
        permissaoHistoricoIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoHistoricoLabel
        // 
        permissaoHistoricoLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoHistoricoLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoHistoricoLabel.Location = new Point(233, 402);
        permissaoHistoricoLabel.Name = "permissaoHistoricoLabel";
        permissaoHistoricoLabel.Size = new Size(126, 20);
        permissaoHistoricoLabel.TabIndex = 29;
        permissaoHistoricoLabel.Text = "Histórico";
        // 
        // permissaoHistoricoVisualizarCheckBox
        // 
        permissaoHistoricoVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoHistoricoVisualizarCheckBox.Checked = true;
        permissaoHistoricoVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoHistoricoVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoHistoricoVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoHistoricoVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoHistoricoVisualizarCheckBox.Location = new Point(233, 425);
        permissaoHistoricoVisualizarCheckBox.Name = "permissaoHistoricoVisualizarCheckBox";
        permissaoHistoricoVisualizarCheckBox.Size = new Size(82, 20);
        permissaoHistoricoVisualizarCheckBox.TabIndex = 30;
        permissaoHistoricoVisualizarCheckBox.Text = "Visualizar";
        permissaoHistoricoVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoConfigIconLabel
        // 
        permissaoConfigIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoConfigIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoConfigIconLabel.Location = new Point(376, 402);
        permissaoConfigIconLabel.Name = "permissaoConfigIconLabel";
        permissaoConfigIconLabel.Size = new Size(22, 22);
        permissaoConfigIconLabel.TabIndex = 47;
        permissaoConfigIconLabel.Text = "";
        permissaoConfigIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoConfigLabel
        // 
        permissaoConfigLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoConfigLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoConfigLabel.Location = new Point(404, 402);
        permissaoConfigLabel.Name = "permissaoConfigLabel";
        permissaoConfigLabel.Size = new Size(150, 20);
        permissaoConfigLabel.TabIndex = 31;
        permissaoConfigLabel.Text = "Configurações";
        // 
        // permissaoConfigSemAcessoCheckBox
        // 
        permissaoConfigSemAcessoCheckBox.BackColor = Color.Transparent;
        permissaoConfigSemAcessoCheckBox.Enabled = false;
        permissaoConfigSemAcessoCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoConfigSemAcessoCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoConfigSemAcessoCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoConfigSemAcessoCheckBox.Location = new Point(404, 425);
        permissaoConfigSemAcessoCheckBox.Name = "permissaoConfigSemAcessoCheckBox";
        permissaoConfigSemAcessoCheckBox.Size = new Size(82, 20);
        permissaoConfigSemAcessoCheckBox.TabIndex = 32;
        permissaoConfigSemAcessoCheckBox.Text = "Sem acesso";
        permissaoConfigSemAcessoCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoCadastroIconLabel
        // 
        permissaoCadastroIconLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        permissaoCadastroIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoCadastroIconLabel.Location = new Point(24, 460);
        permissaoCadastroIconLabel.Name = "permissaoCadastroIconLabel";
        permissaoCadastroIconLabel.Size = new Size(22, 22);
        permissaoCadastroIconLabel.TabIndex = 48;
        permissaoCadastroIconLabel.Text = "";
        permissaoCadastroIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // permissaoCadastroLabel
        // 
        permissaoCadastroLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        permissaoCadastroLabel.ForeColor = Color.FromArgb(15, 23, 42);
        permissaoCadastroLabel.Location = new Point(52, 460);
        permissaoCadastroLabel.Name = "permissaoCadastroLabel";
        permissaoCadastroLabel.Size = new Size(126, 20);
        permissaoCadastroLabel.TabIndex = 49;
        permissaoCadastroLabel.Text = "Cadastro";
        // 
        // permissaoCadastroVisualizarCheckBox
        // 
        permissaoCadastroVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoCadastroVisualizarCheckBox.Checked = true;
        permissaoCadastroVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoCadastroVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoCadastroVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoCadastroVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoCadastroVisualizarCheckBox.Location = new Point(52, 483);
        permissaoCadastroVisualizarCheckBox.Name = "permissaoCadastroVisualizarCheckBox";
        permissaoCadastroVisualizarCheckBox.Size = new Size(82, 17);
        permissaoCadastroVisualizarCheckBox.TabIndex = 50;
        permissaoCadastroVisualizarCheckBox.Text = "Visualizar";
        permissaoCadastroVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // permissaoSapVisualizarCheckBox
        // 
        permissaoSapVisualizarCheckBox.BackColor = Color.Transparent;
        permissaoSapVisualizarCheckBox.Checked = true;
        permissaoSapVisualizarCheckBox.CheckState = CheckState.Checked;
        permissaoSapVisualizarCheckBox.FlatStyle = FlatStyle.Flat;
        permissaoSapVisualizarCheckBox.Font = new Font("Segoe UI", 8F);
        permissaoSapVisualizarCheckBox.ForeColor = Color.FromArgb(71, 85, 105);
        permissaoSapVisualizarCheckBox.Location = new Point(404, 367);
        permissaoSapVisualizarCheckBox.Name = "permissaoSapVisualizarCheckBox";
        permissaoSapVisualizarCheckBox.Size = new Size(82, 17);
        permissaoSapVisualizarCheckBox.TabIndex = 26;
        permissaoSapVisualizarCheckBox.Text = "Visualizar";
        permissaoSapVisualizarCheckBox.UseVisualStyleBackColor = false;
        // 
        // situacaoLabel
        // 
        situacaoLabel.Font = new Font("Segoe UI", 7.75F, FontStyle.Bold);
        situacaoLabel.ForeColor = Color.FromArgb(15, 23, 42);
        situacaoLabel.Location = new Point(292, 56);
        situacaoLabel.Name = "situacaoLabel";
        situacaoLabel.Size = new Size(180, 19);
        situacaoLabel.TabIndex = 8;
        situacaoLabel.Text = "Situação";
        // 
        // summaryCard
        // 
        summaryCard.BackColor = Color.Transparent;
        summaryCard.BorderColor = Color.FromArgb(226, 232, 240);
        summaryCard.BorderRadius = 9;
        summaryCard.Controls.Add(label1);
        summaryCard.Controls.Add(summaryTitleLabel);
        summaryCard.Controls.Add(summaryDividerLabel);
        summaryCard.Controls.Add(summaryPerfilIconLabel);
        summaryCard.Controls.Add(summaryPerfilCaptionLabel);
        summaryCard.Controls.Add(summaryPerfilValueLabel);
        summaryCard.Controls.Add(summaryNivelIconLabel);
        summaryCard.Controls.Add(summaryNivelCaptionLabel);
        summaryCard.Controls.Add(summaryNivelValueLabel);
        summaryCard.Controls.Add(summarySituacaoIconLabel);
        summaryCard.Controls.Add(summarySituacaoCaptionLabel);
        summaryCard.Controls.Add(summarySituacaoValueLabel);
        summaryCard.Controls.Add(summaryPermissionsIconLabel);
        summaryCard.Controls.Add(summaryPermissionsCaptionLabel);
        summaryCard.Controls.Add(summaryPermissionsValueLabel);
        summaryCard.Controls.Add(summaryUsuariosIconLabel);
        summaryCard.Controls.Add(summaryUsuariosCaptionLabel);
        summaryCard.Controls.Add(summaryUsuariosValueLabel);
        summaryCard.Controls.Add(summaryTipIconLabel);
        summaryCard.Controls.Add(summaryTipTextLabel);
        summaryCard.Controls.Add(salvarButton);
        summaryCard.Controls.Add(novoButton);
        summaryCard.Controls.Add(excluirButton);
        summaryCard.Dock = DockStyle.Fill;
        summaryCard.Location = new Point(995, 3);
        summaryCard.Name = "summaryCard";
        summaryCard.ShadowBlur = 0;
        summaryCard.ShadowOffsetY = 0;
        summaryCard.Size = new Size(326, 8);
        summaryCard.TabIndex = 2;
        // 
        // label1
        // 
        label1.Font = new Font("Segoe MDL2 Assets", 16F);
        label1.ForeColor = Color.FromArgb(15, 23, 42);
        label1.Location = new Point(24, 20);
        label1.Name = "label1";
        label1.Size = new Size(28, 28);
        label1.TabIndex = 24;
        label1.Text = "";
        label1.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summaryTitleLabel
        // 
        summaryTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        summaryTitleLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryTitleLabel.Location = new Point(52, 20);
        summaryTitleLabel.Name = "summaryTitleLabel";
        summaryTitleLabel.Size = new Size(180, 24);
        summaryTitleLabel.TabIndex = 1;
        summaryTitleLabel.Text = "Resumo do Perfil";
        summaryTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // summaryDividerLabel
        // 
        summaryDividerLabel.BackColor = Color.FromArgb(226, 232, 240);
        summaryDividerLabel.Location = new Point(26, 56);
        summaryDividerLabel.Name = "summaryDividerLabel";
        summaryDividerLabel.Size = new Size(280, 1);
        summaryDividerLabel.TabIndex = 2;
        // 
        // summaryPerfilIconLabel
        // 
        summaryPerfilIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        summaryPerfilIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        summaryPerfilIconLabel.Location = new Point(32, 78);
        summaryPerfilIconLabel.Name = "summaryPerfilIconLabel";
        summaryPerfilIconLabel.Size = new Size(32, 32);
        summaryPerfilIconLabel.TabIndex = 3;
        summaryPerfilIconLabel.Text = "";
        summaryPerfilIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summaryPerfilCaptionLabel
        // 
        summaryPerfilCaptionLabel.Font = new Font("Segoe UI", 8.5F);
        summaryPerfilCaptionLabel.ForeColor = Color.FromArgb(100, 116, 139);
        summaryPerfilCaptionLabel.Location = new Point(74, 78);
        summaryPerfilCaptionLabel.Name = "summaryPerfilCaptionLabel";
        summaryPerfilCaptionLabel.Size = new Size(180, 18);
        summaryPerfilCaptionLabel.TabIndex = 4;
        summaryPerfilCaptionLabel.Text = "Perfil selecionado";
        // 
        // summaryPerfilValueLabel
        // 
        summaryPerfilValueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        summaryPerfilValueLabel.ForeColor = Color.FromArgb(229, 27, 43);
        summaryPerfilValueLabel.Location = new Point(74, 98);
        summaryPerfilValueLabel.Name = "summaryPerfilValueLabel";
        summaryPerfilValueLabel.Size = new Size(180, 24);
        summaryPerfilValueLabel.TabIndex = 5;
        summaryPerfilValueLabel.Text = "Operador";
        // 
        // summaryNivelIconLabel
        // 
        summaryNivelIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        summaryNivelIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryNivelIconLabel.Location = new Point(32, 132);
        summaryNivelIconLabel.Name = "summaryNivelIconLabel";
        summaryNivelIconLabel.Size = new Size(32, 32);
        summaryNivelIconLabel.TabIndex = 6;
        summaryNivelIconLabel.Text = "";
        summaryNivelIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summaryNivelCaptionLabel
        // 
        summaryNivelCaptionLabel.Font = new Font("Segoe UI", 8.5F);
        summaryNivelCaptionLabel.ForeColor = Color.FromArgb(100, 116, 139);
        summaryNivelCaptionLabel.Location = new Point(74, 132);
        summaryNivelCaptionLabel.Name = "summaryNivelCaptionLabel";
        summaryNivelCaptionLabel.Size = new Size(180, 18);
        summaryNivelCaptionLabel.TabIndex = 7;
        summaryNivelCaptionLabel.Text = "Nível";
        // 
        // summaryNivelValueLabel
        // 
        summaryNivelValueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        summaryNivelValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryNivelValueLabel.Location = new Point(74, 152);
        summaryNivelValueLabel.Name = "summaryNivelValueLabel";
        summaryNivelValueLabel.Size = new Size(180, 24);
        summaryNivelValueLabel.TabIndex = 8;
        summaryNivelValueLabel.Text = "Operacional";
        // 
        // summarySituacaoIconLabel
        // 
        summarySituacaoIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        summarySituacaoIconLabel.ForeColor = Color.FromArgb(22, 163, 74);
        summarySituacaoIconLabel.Location = new Point(32, 186);
        summarySituacaoIconLabel.Name = "summarySituacaoIconLabel";
        summarySituacaoIconLabel.Size = new Size(32, 32);
        summarySituacaoIconLabel.TabIndex = 9;
        summarySituacaoIconLabel.Text = "";
        summarySituacaoIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summarySituacaoCaptionLabel
        // 
        summarySituacaoCaptionLabel.Font = new Font("Segoe UI", 8.5F);
        summarySituacaoCaptionLabel.ForeColor = Color.FromArgb(100, 116, 139);
        summarySituacaoCaptionLabel.Location = new Point(74, 186);
        summarySituacaoCaptionLabel.Name = "summarySituacaoCaptionLabel";
        summarySituacaoCaptionLabel.Size = new Size(180, 18);
        summarySituacaoCaptionLabel.TabIndex = 10;
        summarySituacaoCaptionLabel.Text = "Situação";
        // 
        // summarySituacaoValueLabel
        // 
        summarySituacaoValueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        summarySituacaoValueLabel.ForeColor = Color.FromArgb(22, 163, 74);
        summarySituacaoValueLabel.Location = new Point(74, 206);
        summarySituacaoValueLabel.Name = "summarySituacaoValueLabel";
        summarySituacaoValueLabel.Size = new Size(180, 24);
        summarySituacaoValueLabel.TabIndex = 11;
        summarySituacaoValueLabel.Text = "Ativo";
        // 
        // summaryPermissionsIconLabel
        // 
        summaryPermissionsIconLabel.Font = new Font("Segoe UI Symbol", 16F, FontStyle.Bold);
        summaryPermissionsIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryPermissionsIconLabel.Location = new Point(32, 240);
        summaryPermissionsIconLabel.Name = "summaryPermissionsIconLabel";
        summaryPermissionsIconLabel.Size = new Size(32, 32);
        summaryPermissionsIconLabel.TabIndex = 12;
        summaryPermissionsIconLabel.Text = "✓";
        summaryPermissionsIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summaryPermissionsCaptionLabel
        // 
        summaryPermissionsCaptionLabel.Font = new Font("Segoe UI", 8.5F);
        summaryPermissionsCaptionLabel.ForeColor = Color.FromArgb(100, 116, 139);
        summaryPermissionsCaptionLabel.Location = new Point(74, 240);
        summaryPermissionsCaptionLabel.Name = "summaryPermissionsCaptionLabel";
        summaryPermissionsCaptionLabel.Size = new Size(180, 18);
        summaryPermissionsCaptionLabel.TabIndex = 13;
        summaryPermissionsCaptionLabel.Text = "Total de permissões";
        // 
        // summaryPermissionsValueLabel
        // 
        summaryPermissionsValueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        summaryPermissionsValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryPermissionsValueLabel.Location = new Point(74, 260);
        summaryPermissionsValueLabel.Name = "summaryPermissionsValueLabel";
        summaryPermissionsValueLabel.Size = new Size(180, 24);
        summaryPermissionsValueLabel.TabIndex = 14;
        summaryPermissionsValueLabel.Text = "9 habilitadas";
        // 
        // summaryUsuariosIconLabel
        // 
        summaryUsuariosIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        summaryUsuariosIconLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryUsuariosIconLabel.Location = new Point(32, 294);
        summaryUsuariosIconLabel.Name = "summaryUsuariosIconLabel";
        summaryUsuariosIconLabel.Size = new Size(32, 32);
        summaryUsuariosIconLabel.TabIndex = 15;
        summaryUsuariosIconLabel.Text = "";
        summaryUsuariosIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summaryUsuariosCaptionLabel
        // 
        summaryUsuariosCaptionLabel.Font = new Font("Segoe UI", 8.5F);
        summaryUsuariosCaptionLabel.ForeColor = Color.FromArgb(100, 116, 139);
        summaryUsuariosCaptionLabel.Location = new Point(74, 294);
        summaryUsuariosCaptionLabel.Name = "summaryUsuariosCaptionLabel";
        summaryUsuariosCaptionLabel.Size = new Size(180, 18);
        summaryUsuariosCaptionLabel.TabIndex = 16;
        summaryUsuariosCaptionLabel.Text = "Usuários vinculados";
        // 
        // summaryUsuariosValueLabel
        // 
        summaryUsuariosValueLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        summaryUsuariosValueLabel.ForeColor = Color.FromArgb(15, 23, 42);
        summaryUsuariosValueLabel.Location = new Point(74, 314);
        summaryUsuariosValueLabel.Name = "summaryUsuariosValueLabel";
        summaryUsuariosValueLabel.Size = new Size(180, 24);
        summaryUsuariosValueLabel.TabIndex = 17;
        summaryUsuariosValueLabel.Text = "18";
        // 
        // summaryTipIconLabel
        // 
        summaryTipIconLabel.Font = new Font("Segoe MDL2 Assets", 18F);
        summaryTipIconLabel.ForeColor = Color.FromArgb(229, 27, 43);
        summaryTipIconLabel.Location = new Point(32, 348);
        summaryTipIconLabel.Name = "summaryTipIconLabel";
        summaryTipIconLabel.Size = new Size(32, 32);
        summaryTipIconLabel.TabIndex = 18;
        summaryTipIconLabel.Text = "";
        summaryTipIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // summaryTipTextLabel
        // 
        summaryTipTextLabel.Font = new Font("Segoe UI", 8.5F);
        summaryTipTextLabel.ForeColor = Color.FromArgb(71, 85, 105);
        summaryTipTextLabel.Location = new Point(74, 348);
        summaryTipTextLabel.Name = "summaryTipTextLabel";
        summaryTipTextLabel.Size = new Size(210, 48);
        summaryTipTextLabel.TabIndex = 19;
        summaryTipTextLabel.Text = "Mantenha permissões mínimas\r\npara cada função.";
        // 
        // salvarButton
        // 
        salvarButton.BackColor = Color.FromArgb(229, 27, 43);
        salvarButton.FlatAppearance.BorderColor = Color.FromArgb(229, 27, 43);
        salvarButton.FlatStyle = FlatStyle.Flat;
        salvarButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        salvarButton.ForeColor = Color.White;
        salvarButton.Location = new Point(24, 402);
        salvarButton.Name = "salvarButton";
        salvarButton.Size = new Size(280, 28);
        salvarButton.TabIndex = 20;
        salvarButton.Text = "Salvar Perfil                 F5";
        salvarButton.UseVisualStyleBackColor = false;
        // 
        // novoButton
        // 
        novoButton.BackColor = Color.White;
        novoButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        novoButton.FlatStyle = FlatStyle.Flat;
        novoButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        novoButton.ForeColor = Color.FromArgb(15, 23, 42);
        novoButton.Location = new Point(24, 434);
        novoButton.Name = "novoButton";
        novoButton.Size = new Size(280, 28);
        novoButton.TabIndex = 21;
        novoButton.Text = "Editar Perfil                F6";
        novoButton.UseVisualStyleBackColor = false;
        // 
        // excluirButton
        // 
        excluirButton.BackColor = Color.White;
        excluirButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        excluirButton.FlatStyle = FlatStyle.Flat;
        excluirButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        excluirButton.ForeColor = Color.FromArgb(229, 27, 43);
        excluirButton.Location = new Point(24, 466);
        excluirButton.Name = "excluirButton";
        excluirButton.Size = new Size(280, 28);
        excluirButton.TabIndex = 22;
        excluirButton.Text = "Excluir Perfil                F8";
        excluirButton.UseVisualStyleBackColor = false;
        // 
        // _workspacePanel
        // 
        _workspacePanel.BackColor = Color.FromArgb(247, 248, 250);
        _workspacePanel.Controls.Add(workspaceConteudo);
        _workspacePanel.Controls.Add(_filtrosCardPanel);
        _workspacePanel.Dock = DockStyle.Fill;
        _workspacePanel.Location = new Point(3, 3);
        _workspacePanel.Name = "_workspacePanel";
        _workspacePanel.Padding = new Padding(6);
        _workspacePanel.Size = new Size(1324, 576);
        _workspacePanel.TabIndex = 2;
        _workspacePanel.Resize += WorkspacePanel_Resize;
        // 
        // workspaceConteudo
        // 
        workspaceConteudo.ColumnCount = 2;
        workspaceConteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        workspaceConteudo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
        workspaceConteudo.Controls.Add(esquerdaCard, 0, 0);
        workspaceConteudo.Controls.Add(direitaCard, 1, 0);
        workspaceConteudo.Dock = DockStyle.Fill;
        workspaceConteudo.Location = new Point(6, 138);
        workspaceConteudo.Name = "workspaceConteudo";
        workspaceConteudo.Padding = new Padding(0, 12, 0, 0);
        workspaceConteudo.RowCount = 1;
        workspaceConteudo.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        workspaceConteudo.Size = new Size(1312, 432);
        workspaceConteudo.TabIndex = 0;
        // 
        // esquerdaCard
        // 
        esquerdaCard.BackColor = Color.Transparent;
        esquerdaCard.BorderColor = Color.FromArgb(226, 232, 240);
        esquerdaCard.Controls.Add(esquerdaTitulo);
        esquerdaCard.Controls.Add(_modulosListBox);
        esquerdaCard.Controls.Add(_rodapeListaLabel);
        esquerdaCard.Dock = DockStyle.Fill;
        esquerdaCard.Location = new Point(0, 12);
        esquerdaCard.Margin = new Padding(0, 0, 10, 0);
        esquerdaCard.Name = "esquerdaCard";
        esquerdaCard.ShadowBlur = 0;
        esquerdaCard.ShadowOffsetY = 0;
        esquerdaCard.Size = new Size(357, 420);
        esquerdaCard.TabIndex = 0;
        // 
        // esquerdaTitulo
        // 
        esquerdaTitulo.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        esquerdaTitulo.ForeColor = Color.FromArgb(30, 41, 59);
        esquerdaTitulo.Location = new Point(12, 12);
        esquerdaTitulo.Name = "esquerdaTitulo";
        esquerdaTitulo.Size = new Size(240, 20);
        esquerdaTitulo.TabIndex = 0;
        esquerdaTitulo.Text = "Perfis de Acesso";
        // 
        // _modulosListBox
        // 
        _modulosListBox.BorderStyle = BorderStyle.None;
        _modulosListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _modulosListBox.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _modulosListBox.ItemHeight = 44;
        _modulosListBox.Location = new Point(12, 38);
        _modulosListBox.Name = "_modulosListBox";
        _modulosListBox.Size = new Size(250, 352);
        _modulosListBox.TabIndex = 1;
        _modulosListBox.DrawItem += ModulosListBox_DrawItem;
        _modulosListBox.SelectedIndexChanged += ModulosListBox_SelectedIndexChanged;
        // 
        // _rodapeListaLabel
        // 
        _rodapeListaLabel.Font = new Font("Segoe UI", 8F);
        _rodapeListaLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _rodapeListaLabel.Location = new Point(12, 404);
        _rodapeListaLabel.Name = "_rodapeListaLabel";
        _rodapeListaLabel.Size = new Size(250, 20);
        _rodapeListaLabel.TabIndex = 2;
        _rodapeListaLabel.Text = "Mostrando 0 de 0 permissões";
        // 
        // direitaCard
        // 
        direitaCard.BackColor = Color.Transparent;
        direitaCard.BorderColor = Color.FromArgb(226, 232, 240);
        direitaCard.Controls.Add(_tituloPermissoesPerfilLabel);
        direitaCard.Controls.Add(restaurarButton);
        direitaCard.Controls.Add(salvarPermissoesButton);
        direitaCard.Dock = DockStyle.Fill;
        direitaCard.Location = new Point(370, 15);
        direitaCard.Name = "direitaCard";
        direitaCard.ShadowBlur = 0;
        direitaCard.ShadowOffsetY = 0;
        direitaCard.Size = new Size(939, 414);
        direitaCard.TabIndex = 1;
        // 
        // _tituloPermissoesPerfilLabel
        // 
        _tituloPermissoesPerfilLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _tituloPermissoesPerfilLabel.ForeColor = Color.FromArgb(30, 41, 59);
        _tituloPermissoesPerfilLabel.Location = new Point(14, 12);
        _tituloPermissoesPerfilLabel.Name = "_tituloPermissoesPerfilLabel";
        _tituloPermissoesPerfilLabel.Size = new Size(380, 20);
        _tituloPermissoesPerfilLabel.TabIndex = 0;
        _tituloPermissoesPerfilLabel.Text = "Permissões do Perfil: Todos";
        // 
        // restaurarButton
        // 
        restaurarButton.BackColor = Color.White;
        restaurarButton.BorderColor = Color.FromArgb(203, 213, 225);
        restaurarButton.BorderRadius = 6;
        restaurarButton.BorderThickness = 1;
        restaurarButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        restaurarButton.ForeColor = Color.FromArgb(30, 41, 59);
        restaurarButton.Location = new Point(514, 374);
        restaurarButton.Name = "restaurarButton";
        restaurarButton.Size = new Size(140, 28);
        restaurarButton.TabIndex = 1;
        restaurarButton.Text = "Restaurar Padrões";
        restaurarButton.Click += RestaurarButton_Click;
        // 
        // salvarPermissoesButton
        // 
        salvarPermissoesButton.BackColor = Color.FromArgb(220, 38, 38);
        salvarPermissoesButton.BorderColor = Color.FromArgb(220, 38, 38);
        salvarPermissoesButton.BorderRadius = 6;
        salvarPermissoesButton.BorderThickness = 0;
        salvarPermissoesButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        salvarPermissoesButton.ForeColor = Color.White;
        salvarPermissoesButton.Location = new Point(662, 374);
        salvarPermissoesButton.Name = "salvarPermissoesButton";
        salvarPermissoesButton.Size = new Size(124, 28);
        salvarPermissoesButton.TabIndex = 2;
        salvarPermissoesButton.Text = "Salvar Permissões";
        salvarPermissoesButton.Click += SalvarPermissoesButton_Click;
        // 
        // _filtrosCardPanel
        // 
        _filtrosCardPanel.BackColor = Color.Transparent;
        _filtrosCardPanel.BorderColor = Color.FromArgb(203, 213, 225);
        _filtrosCardPanel.Controls.Add(_limparFiltrosButtonPanel);
        _filtrosCardPanel.Controls.Add(_pesquisarFiltrosButtonPanel);
        _filtrosCardPanel.Controls.Add(filtrosIconLabel);
        _filtrosCardPanel.Controls.Add(filtrosTitulo);
        _filtrosCardPanel.Controls.Add(_perfilFiltroLabelUi);
        _filtrosCardPanel.Controls.Add(_moduloFiltroLabelUi);
        _filtrosCardPanel.Controls.Add(_recursoFiltroLabelUi);
        _filtrosCardPanel.Controls.Add(_statusFiltroLabelUi);
        _filtrosCardPanel.Controls.Add(_perfilFiltroPanel);
        _filtrosCardPanel.Controls.Add(_moduloFiltroPanel);
        _filtrosCardPanel.Controls.Add(_recursoFiltroPanel);
        _filtrosCardPanel.Controls.Add(_statusFiltroPanel);
        _filtrosCardPanel.Dock = DockStyle.Top;
        _filtrosCardPanel.Location = new Point(6, 6);
        _filtrosCardPanel.Name = "_filtrosCardPanel";
        _filtrosCardPanel.ShadowBlur = 0;
        _filtrosCardPanel.ShadowOffsetY = 0;
        _filtrosCardPanel.Size = new Size(1312, 132);
        _filtrosCardPanel.TabIndex = 1;
        _filtrosCardPanel.Resize += FiltrosCardPanel_Resize;
        // 
        // _limparFiltrosButtonPanel
        // 
        _limparFiltrosButtonPanel.BackColor = Color.Transparent;
        _limparFiltrosButtonPanel.BorderColor = Color.FromArgb(203, 213, 225);
        _limparFiltrosButtonPanel.BorderRadius = 6;
        _limparFiltrosButtonPanel.Controls.Add(_limparFiltrosButton);
        _limparFiltrosButtonPanel.Location = new Point(1021, 96);
        _limparFiltrosButtonPanel.Name = "_limparFiltrosButtonPanel";
        _limparFiltrosButtonPanel.ShadowBlur = 0;
        _limparFiltrosButtonPanel.ShadowOffsetY = 0;
        _limparFiltrosButtonPanel.Size = new Size(104, 30);
        _limparFiltrosButtonPanel.TabIndex = 5;
        // 
        // _limparFiltrosButton
        // 
        _limparFiltrosButton.BackColor = Color.White;
        _limparFiltrosButton.BorderColor = Color.FromArgb(203, 213, 225);
        _limparFiltrosButton.BorderRadius = 6;
        _limparFiltrosButton.BorderThickness = 1;
        _limparFiltrosButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _limparFiltrosButton.ForeColor = Color.FromArgb(30, 41, 59);
        _limparFiltrosButton.Image = (Image)resources.GetObject("_limparFiltrosButton.Image");
        _limparFiltrosButton.Location = new Point(0, 0);
        _limparFiltrosButton.Name = "_limparFiltrosButton";
        _limparFiltrosButton.Size = new Size(104, 30);
        _limparFiltrosButton.TabIndex = 0;
        _limparFiltrosButton.Text = "Limpar";
        _limparFiltrosButton.Click += LimparFiltrosButton_Click;
        // 
        // _pesquisarFiltrosButtonPanel
        // 
        _pesquisarFiltrosButtonPanel.BackColor = Color.Transparent;
        _pesquisarFiltrosButtonPanel.BorderColor = Color.White;
        _pesquisarFiltrosButtonPanel.BorderRadius = 6;
        _pesquisarFiltrosButtonPanel.BorderThickness = 0;
        _pesquisarFiltrosButtonPanel.Controls.Add(_pesquisarFiltrosButton);
        _pesquisarFiltrosButtonPanel.FillColor = Color.White;
        _pesquisarFiltrosButtonPanel.Location = new Point(1124, 96);
        _pesquisarFiltrosButtonPanel.Name = "_pesquisarFiltrosButtonPanel";
        _pesquisarFiltrosButtonPanel.ShadowBlur = 0;
        _pesquisarFiltrosButtonPanel.ShadowOffsetY = 0;
        _pesquisarFiltrosButtonPanel.Size = new Size(120, 30);
        _pesquisarFiltrosButtonPanel.TabIndex = 6;
        // 
        // _pesquisarFiltrosButton
        // 
        _pesquisarFiltrosButton.BackColor = Color.FromArgb(220, 38, 38);
        _pesquisarFiltrosButton.BorderColor = Color.FromArgb(220, 38, 38);
        _pesquisarFiltrosButton.BorderRadius = 6;
        _pesquisarFiltrosButton.BorderThickness = 0;
        _pesquisarFiltrosButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        _pesquisarFiltrosButton.ForeColor = Color.White;
        _pesquisarFiltrosButton.Image = (Image)resources.GetObject("_pesquisarFiltrosButton.Image");
        _pesquisarFiltrosButton.Location = new Point(0, 0);
        _pesquisarFiltrosButton.Name = "_pesquisarFiltrosButton";
        _pesquisarFiltrosButton.Size = new Size(120, 30);
        _pesquisarFiltrosButton.TabIndex = 0;
        _pesquisarFiltrosButton.Text = "Pesquisar";
        _pesquisarFiltrosButton.Click += PesquisarFiltrosButton_Click;
        // 
        // filtrosIconLabel
        // 
        filtrosIconLabel.Font = new Font("Segoe MDL2 Assets", 10F);
        filtrosIconLabel.ForeColor = Color.FromArgb(220, 38, 38);
        filtrosIconLabel.Location = new Point(16, 14);
        filtrosIconLabel.Name = "filtrosIconLabel";
        filtrosIconLabel.Size = new Size(16, 16);
        filtrosIconLabel.TabIndex = 7;
        filtrosIconLabel.Text = "";
        filtrosIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // filtrosTitulo
        // 
        filtrosTitulo.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        filtrosTitulo.ForeColor = Color.FromArgb(30, 41, 59);
        filtrosTitulo.Location = new Point(36, 10);
        filtrosTitulo.Name = "filtrosTitulo";
        filtrosTitulo.Size = new Size(220, 22);
        filtrosTitulo.TabIndex = 0;
        filtrosTitulo.Text = "Filtros de pesquisa";
        // 
        // _perfilFiltroLabelUi
        // 
        _perfilFiltroLabelUi.Font = new Font("Segoe UI", 8.25F);
        _perfilFiltroLabelUi.ForeColor = Color.FromArgb(71, 85, 105);
        _perfilFiltroLabelUi.Location = new Point(16, 40);
        _perfilFiltroLabelUi.Name = "_perfilFiltroLabelUi";
        _perfilFiltroLabelUi.Size = new Size(170, 16);
        _perfilFiltroLabelUi.TabIndex = 1;
        _perfilFiltroLabelUi.Text = "Perfil de Acesso";
        // 
        // _moduloFiltroLabelUi
        // 
        _moduloFiltroLabelUi.Font = new Font("Segoe UI", 8.25F);
        _moduloFiltroLabelUi.ForeColor = Color.FromArgb(71, 85, 105);
        _moduloFiltroLabelUi.Location = new Point(330, 41);
        _moduloFiltroLabelUi.Name = "_moduloFiltroLabelUi";
        _moduloFiltroLabelUi.Size = new Size(170, 16);
        _moduloFiltroLabelUi.TabIndex = 2;
        _moduloFiltroLabelUi.Text = "Módulo";
        // 
        // _recursoFiltroLabelUi
        // 
        _recursoFiltroLabelUi.Font = new Font("Segoe UI", 8.25F);
        _recursoFiltroLabelUi.ForeColor = Color.FromArgb(71, 85, 105);
        _recursoFiltroLabelUi.Location = new Point(644, 40);
        _recursoFiltroLabelUi.Name = "_recursoFiltroLabelUi";
        _recursoFiltroLabelUi.Size = new Size(170, 16);
        _recursoFiltroLabelUi.TabIndex = 3;
        _recursoFiltroLabelUi.Text = "Recurso";
        // 
        // _statusFiltroLabelUi
        // 
        _statusFiltroLabelUi.Font = new Font("Segoe UI", 8.25F);
        _statusFiltroLabelUi.ForeColor = Color.FromArgb(71, 85, 105);
        _statusFiltroLabelUi.Location = new Point(955, 40);
        _statusFiltroLabelUi.Name = "_statusFiltroLabelUi";
        _statusFiltroLabelUi.Size = new Size(170, 16);
        _statusFiltroLabelUi.TabIndex = 4;
        _statusFiltroLabelUi.Text = "Status";
        // 
        // _perfilFiltroPanel
        // 
        _perfilFiltroPanel.BackColor = Color.Transparent;
        _perfilFiltroPanel.BorderColor = Color.FromArgb(203, 213, 225);
        _perfilFiltroPanel.BorderRadius = 5;
        _perfilFiltroPanel.Controls.Add(_filtroPerfilComboBox);
        _perfilFiltroPanel.Location = new Point(16, 60);
        _perfilFiltroPanel.Name = "_perfilFiltroPanel";
        _perfilFiltroPanel.ShadowBlur = 0;
        _perfilFiltroPanel.ShadowOffsetY = 0;
        _perfilFiltroPanel.Size = new Size(286, 30);
        _perfilFiltroPanel.TabIndex = 8;
        // 
        // _filtroPerfilComboBox
        // 
        _filtroPerfilComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _filtroPerfilComboBox.FlatStyle = FlatStyle.Flat;
        _filtroPerfilComboBox.Font = new Font("Segoe UI", 9F);
        _filtroPerfilComboBox.FormattingEnabled = true;
        _filtroPerfilComboBox.Items.AddRange(new object[] { "Todos" });
        _filtroPerfilComboBox.Location = new Point(8, 4);
        _filtroPerfilComboBox.Name = "_filtroPerfilComboBox";
        _filtroPerfilComboBox.Size = new Size(270, 23);
        _filtroPerfilComboBox.TabIndex = 0;
        _filtroPerfilComboBox.SelectedIndexChanged += FiltroCampo_Changed;
        // 
        // _moduloFiltroPanel
        // 
        _moduloFiltroPanel.BackColor = Color.Transparent;
        _moduloFiltroPanel.BorderColor = Color.FromArgb(203, 213, 225);
        _moduloFiltroPanel.BorderRadius = 5;
        _moduloFiltroPanel.Controls.Add(_filtroModuloComboBox);
        _moduloFiltroPanel.Location = new Point(330, 60);
        _moduloFiltroPanel.Name = "_moduloFiltroPanel";
        _moduloFiltroPanel.ShadowBlur = 0;
        _moduloFiltroPanel.ShadowOffsetY = 0;
        _moduloFiltroPanel.Size = new Size(286, 30);
        _moduloFiltroPanel.TabIndex = 9;
        // 
        // _filtroModuloComboBox
        // 
        _filtroModuloComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _filtroModuloComboBox.FlatStyle = FlatStyle.Flat;
        _filtroModuloComboBox.Font = new Font("Segoe UI", 9F);
        _filtroModuloComboBox.FormattingEnabled = true;
        _filtroModuloComboBox.Items.AddRange(new object[] { "Todos" });
        _filtroModuloComboBox.Location = new Point(8, 4);
        _filtroModuloComboBox.Name = "_filtroModuloComboBox";
        _filtroModuloComboBox.Size = new Size(270, 23);
        _filtroModuloComboBox.TabIndex = 0;
        _filtroModuloComboBox.SelectedIndexChanged += FiltroCampo_Changed;
        // 
        // _recursoFiltroPanel
        // 
        _recursoFiltroPanel.BackColor = Color.Transparent;
        _recursoFiltroPanel.BorderColor = Color.FromArgb(203, 213, 225);
        _recursoFiltroPanel.BorderRadius = 5;
        _recursoFiltroPanel.Controls.Add(recursoIcone);
        _recursoFiltroPanel.Controls.Add(_filtroRecursoTextBox);
        _recursoFiltroPanel.Location = new Point(644, 60);
        _recursoFiltroPanel.Name = "_recursoFiltroPanel";
        _recursoFiltroPanel.ShadowBlur = 0;
        _recursoFiltroPanel.ShadowOffsetY = 0;
        _recursoFiltroPanel.Size = new Size(286, 30);
        _recursoFiltroPanel.TabIndex = 10;
        // 
        // recursoIcone
        // 
        recursoIcone.Font = new Font("Segoe MDL2 Assets", 11F);
        recursoIcone.ForeColor = Color.FromArgb(71, 85, 105);
        recursoIcone.Location = new Point(156, 6);
        recursoIcone.Name = "recursoIcone";
        recursoIcone.Size = new Size(16, 16);
        recursoIcone.TabIndex = 0;
        recursoIcone.Text = "";
        recursoIcone.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // _filtroRecursoTextBox
        // 
        _filtroRecursoTextBox.BorderStyle = BorderStyle.None;
        _filtroRecursoTextBox.Font = new Font("Segoe UI", 9F);
        _filtroRecursoTextBox.Location = new Point(8, 6);
        _filtroRecursoTextBox.Name = "_filtroRecursoTextBox";
        _filtroRecursoTextBox.PlaceholderText = "Digite o recurso";
        _filtroRecursoTextBox.Size = new Size(148, 16);
        _filtroRecursoTextBox.TabIndex = 0;
        _filtroRecursoTextBox.TextChanged += FiltroCampo_Changed;
        // 
        // _statusFiltroPanel
        // 
        _statusFiltroPanel.BackColor = Color.Transparent;
        _statusFiltroPanel.BorderColor = Color.FromArgb(203, 213, 225);
        _statusFiltroPanel.BorderRadius = 5;
        _statusFiltroPanel.Controls.Add(_filtroStatusComboBox);
        _statusFiltroPanel.Location = new Point(958, 60);
        _statusFiltroPanel.Name = "_statusFiltroPanel";
        _statusFiltroPanel.ShadowBlur = 0;
        _statusFiltroPanel.ShadowOffsetY = 0;
        _statusFiltroPanel.Size = new Size(286, 30);
        _statusFiltroPanel.TabIndex = 11;
        // 
        // _filtroStatusComboBox
        // 
        _filtroStatusComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _filtroStatusComboBox.FlatStyle = FlatStyle.Flat;
        _filtroStatusComboBox.Font = new Font("Segoe UI", 9F);
        _filtroStatusComboBox.FormattingEnabled = true;
        _filtroStatusComboBox.Items.AddRange(new object[] { "Todos", "Ativo", "Inativo" });
        _filtroStatusComboBox.Location = new Point(8, 4);
        _filtroStatusComboBox.Name = "_filtroStatusComboBox";
        _filtroStatusComboBox.Size = new Size(270, 23);
        _filtroStatusComboBox.TabIndex = 0;
        _filtroStatusComboBox.SelectedIndexChanged += FiltroCampo_Changed;
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
        cellUserText.AutoEllipsis = true;
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
        cellTerminalText.AutoEllipsis = true;
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
        cellEmpresaText.AutoEllipsis = true;
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
        cellBancoText.AutoEllipsis = true;
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
        cellHoraText.AutoEllipsis = true;
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
        cellDataText.AutoEllipsis = true;
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
        // customTitleBarPanel
        // 
        customTitleBarPanel.BackColor = Color.FromArgb(17, 24, 39);
        customTitleBarPanel.Controls.Add(menuHeaderLabel);
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
        headerTitleLabel.Text = "Permissão";
        // 
        // headerSubtitleLabel
        // 
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F);
        headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228);
        headerSubtitleLabel.Location = new Point(279, 29);
        headerSubtitleLabel.Name = "headerSubtitleLabel";
        headerSubtitleLabel.Size = new Size(343, 17);
        headerSubtitleLabel.TabIndex = 5;
        headerSubtitleLabel.Text = "Gerencie as permissões de acesso dos perfils do sistema.";
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
        sapStatusPanel.TabIndex = 6;
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
        sapStatusLabel.Size = new Size(151, 17);
        sapStatusLabel.TabIndex = 0;
        sapStatusLabel.Text = "SAP: não configurado";
        sapStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
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
        minimizeWindowLabel.TabIndex = 7;
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
        maximizeWindowLabel.TabIndex = 8;
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
        closeWindowLabel.TabIndex = 9;
        closeWindowLabel.Text = "×";
        closeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // nivelAcessoLabel
        // 
        nivelAcessoLabel.Font = new Font("Segoe UI", 7.75F, FontStyle.Bold);
        nivelAcessoLabel.ForeColor = Color.FromArgb(15, 23, 42);
        nivelAcessoLabel.Location = new Point(292, 56);
        nivelAcessoLabel.Name = "nivelAcessoLabel";
        nivelAcessoLabel.Size = new Size(180, 14);
        nivelAcessoLabel.TabIndex = 4;
        nivelAcessoLabel.Text = "Nível de Acesso *";
        // 
        // nivelAcessoInputPanel
        // 
        nivelAcessoInputPanel.BackColor = Color.Transparent;
        nivelAcessoInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        nivelAcessoInputPanel.BorderRadius = 5;
        nivelAcessoInputPanel.Controls.Add(nivelAcessoComboBox);
        nivelAcessoInputPanel.Location = new Point(292, 73);
        nivelAcessoInputPanel.Name = "nivelAcessoInputPanel";
        nivelAcessoInputPanel.ShadowBlur = 0;
        nivelAcessoInputPanel.ShadowOffsetY = 0;
        nivelAcessoInputPanel.Size = new Size(238, 33);
        nivelAcessoInputPanel.TabIndex = 5;
        // 
        // nivelAcessoComboBox
        // 
        nivelAcessoComboBox.FlatStyle = FlatStyle.Flat;
        nivelAcessoComboBox.Font = new Font("Segoe UI", 9F);
        nivelAcessoComboBox.Items.AddRange(new object[] { "Ativo", "Inativo", "Operacional", "Administrativo" });
        nivelAcessoComboBox.Location = new Point(12, 5);
        nivelAcessoComboBox.Name = "nivelAcessoComboBox";
        nivelAcessoComboBox.Size = new Size(214, 23);
        nivelAcessoComboBox.TabIndex = 5;
        nivelAcessoComboBox.Text = "Operacional";
        // 
        // usuariosLabel
        // 
        usuariosLabel.Font = new Font("Segoe UI", 7.75F, FontStyle.Bold);
        usuariosLabel.ForeColor = Color.FromArgb(15, 23, 42);
        usuariosLabel.Location = new Point(292, 180);
        usuariosLabel.Name = "usuariosLabel";
        usuariosLabel.Size = new Size(180, 20);
        usuariosLabel.TabIndex = 10;
        usuariosLabel.Text = "Usuários Vinculados";
        // 
        // usuariosInputPanel
        // 
        usuariosInputPanel.BackColor = Color.Transparent;
        usuariosInputPanel.BorderColor = Color.FromArgb(203, 213, 225);
        usuariosInputPanel.BorderRadius = 5;
        usuariosInputPanel.Controls.Add(usuariosTextBox);
        usuariosInputPanel.Location = new Point(292, 202);
        usuariosInputPanel.Name = "usuariosInputPanel";
        usuariosInputPanel.ShadowBlur = 0;
        usuariosInputPanel.ShadowOffsetY = 0;
        usuariosInputPanel.Size = new Size(238, 33);
        usuariosInputPanel.TabIndex = 11;
        // 
        // usuariosTextBox
        // 
        usuariosTextBox.BorderStyle = BorderStyle.None;
        usuariosTextBox.Font = new Font("Segoe UI", 9F);
        usuariosTextBox.Location = new Point(12, 6);
        usuariosTextBox.Name = "usuariosTextBox";
        usuariosTextBox.Size = new Size(214, 16);
        usuariosTextBox.TabIndex = 11;
        usuariosTextBox.Text = "18";
        // 
        // novoPerfilButton
        // 
        novoPerfilButton.BackColor = Color.White;
        novoPerfilButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        novoPerfilButton.FlatStyle = FlatStyle.Flat;
        novoPerfilButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        novoPerfilButton.ForeColor = Color.FromArgb(15, 23, 42);
        novoPerfilButton.Location = new Point(224, 18);
        novoPerfilButton.Name = "novoPerfilButton";
        novoPerfilButton.Size = new Size(92, 28);
        novoPerfilButton.TabIndex = 2;
        novoPerfilButton.Text = "＋  Novo Perfil";
        novoPerfilButton.UseVisualStyleBackColor = false;
        // 
        // duplicarButton
        // 
        duplicarButton.BackColor = Color.White;
        duplicarButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        duplicarButton.FlatStyle = FlatStyle.Flat;
        duplicarButton.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        duplicarButton.ForeColor = Color.FromArgb(15, 23, 42);
        duplicarButton.Location = new Point(323, 18);
        duplicarButton.Name = "duplicarButton";
        duplicarButton.Size = new Size(92, 28);
        duplicarButton.TabIndex = 3;
        duplicarButton.Text = "⧉  Duplicar";
        duplicarButton.UseVisualStyleBackColor = false;
        // 
        // profilesDataGridView
        // 
        profilesDataGridView.AllowUserToAddRows = false;
        profilesDataGridView.AllowUserToDeleteRows = false;
        profilesDataGridView.AllowUserToResizeColumns = false;
        profilesDataGridView.AllowUserToResizeRows = false;
        profilesDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        profilesDataGridView.BackgroundColor = Color.White;
        profilesDataGridView.BorderStyle = BorderStyle.None;
        profilesDataGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        profilesDataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle1.BackColor = Color.White;
        dataGridViewCellStyle1.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        dataGridViewCellStyle1.ForeColor = Color.FromArgb(51, 65, 85);
        dataGridViewCellStyle1.SelectionBackColor = Color.White;
        dataGridViewCellStyle1.SelectionForeColor = Color.FromArgb(51, 65, 85);
        dataGridViewCellStyle1.WrapMode = DataGridViewTriState.False;
        profilesDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
        profilesDataGridView.ColumnHeadersHeight = 31;
        profilesDataGridView.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3, dataGridViewTextBoxColumn4 });
        dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleLeft;
        dataGridViewCellStyle4.BackColor = Color.White;
        dataGridViewCellStyle4.Font = new Font("Segoe UI", 8F);
        dataGridViewCellStyle4.ForeColor = Color.FromArgb(51, 65, 85);
        dataGridViewCellStyle4.SelectionBackColor = Color.FromArgb(254, 242, 242);
        dataGridViewCellStyle4.SelectionForeColor = Color.FromArgb(51, 65, 85);
        dataGridViewCellStyle4.WrapMode = DataGridViewTriState.False;
        profilesDataGridView.DefaultCellStyle = dataGridViewCellStyle4;
        profilesDataGridView.EnableHeadersVisualStyles = false;
        profilesDataGridView.GridColor = Color.FromArgb(229, 231, 235);
        profilesDataGridView.Location = new Point(20, 107);
        profilesDataGridView.Name = "profilesDataGridView";
        profilesDataGridView.ReadOnly = true;
        profilesDataGridView.RowHeadersVisible = false;
        profilesDataGridView.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        profilesDataGridView.RowTemplate.Height = 42;
        profilesDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        profilesDataGridView.Size = new Size(372, 448);
        profilesDataGridView.TabIndex = 5;
        // 
        // dataGridViewTextBoxColumn1
        // 
        dataGridViewTextBoxColumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dataGridViewCellStyle2.Font = new Font("Segoe MDL2 Assets", 11F);
        dataGridViewTextBoxColumn1.DefaultCellStyle = dataGridViewCellStyle2;
        dataGridViewTextBoxColumn1.HeaderText = "";
        dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
        dataGridViewTextBoxColumn1.ReadOnly = true;
        dataGridViewTextBoxColumn1.Width = 42;
        // 
        // dataGridViewTextBoxColumn2
        // 
        dataGridViewTextBoxColumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        dataGridViewTextBoxColumn2.HeaderText = "Perfil";
        dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
        dataGridViewTextBoxColumn2.ReadOnly = true;
        dataGridViewTextBoxColumn2.Width = 162;
        // 
        // dataGridViewTextBoxColumn3
        // 
        dataGridViewTextBoxColumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dataGridViewTextBoxColumn3.DefaultCellStyle = dataGridViewCellStyle3;
        dataGridViewTextBoxColumn3.HeaderText = "Usuários vinculados";
        dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
        dataGridViewTextBoxColumn3.ReadOnly = true;
        // 
        // dataGridViewTextBoxColumn4
        // 
        dataGridViewTextBoxColumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        dataGridViewTextBoxColumn4.FillWeight = 80F;
        dataGridViewTextBoxColumn4.HeaderText = "Situação";
        dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
        dataGridViewTextBoxColumn4.ReadOnly = true;
        // 
        // heroPanel
        // 
        heroPanel.BackColor = Color.Transparent;
        heroPanel.BorderColor = Color.FromArgb(226, 232, 240);
        heroPanel.BorderRadius = 9;
        heroPanel.Controls.Add(heroLogoPictureBox);
        heroPanel.Controls.Add(heroTitleLabel);
        heroPanel.Controls.Add(heroSubtitleLabel);
        heroPanel.Controls.Add(heroIllustrationPanel);
        heroPanel.Dock = DockStyle.Fill;
        heroPanel.Location = new Point(0, 0);
        heroPanel.Margin = new Padding(0, 0, 0, 12);
        heroPanel.Name = "heroPanel";
        heroPanel.ShadowBlur = 0;
        heroPanel.ShadowOffsetY = 0;
        heroPanel.Size = new Size(1330, 120);
        heroPanel.TabIndex = 0;
        // 
        // heroLogoPictureBox
        // 
        heroLogoPictureBox.BackColor = Color.FromArgb(254, 226, 226);
        heroLogoPictureBox.Image = (Image)resources.GetObject("heroLogoPictureBox.Image");
        heroLogoPictureBox.Location = new Point(28, 20);
        heroLogoPictureBox.Name = "heroLogoPictureBox";
        heroLogoPictureBox.Size = new Size(118, 80);
        heroLogoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        heroLogoPictureBox.TabIndex = 0;
        heroLogoPictureBox.TabStop = false;
        // 
        // heroTitleLabel
        // 
        heroTitleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        heroTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        heroTitleLabel.Location = new Point(178, 28);
        heroTitleLabel.Name = "heroTitleLabel";
        heroTitleLabel.Size = new Size(430, 34);
        heroTitleLabel.TabIndex = 1;
        heroTitleLabel.Text = "Gestão de Perfis de Acesso";
        // 
        // heroSubtitleLabel
        // 
        heroSubtitleLabel.Font = new Font("Segoe UI", 10F);
        heroSubtitleLabel.ForeColor = Color.FromArgb(71, 85, 105);
        heroSubtitleLabel.Location = new Point(180, 65);
        heroSubtitleLabel.Name = "heroSubtitleLabel";
        heroSubtitleLabel.Size = new Size(420, 42);
        heroSubtitleLabel.TabIndex = 2;
        heroSubtitleLabel.Text = "Configure níveis de permissão para cada\r\ntipo de usuário do sistema.";
        // 
        // heroIllustrationPanel
        // 
        heroIllustrationPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        heroIllustrationPanel.Controls.Add(heroShieldPanel);
        heroIllustrationPanel.Controls.Add(heroLockPanel);
        heroIllustrationPanel.Location = new Point(1804, 8);
        heroIllustrationPanel.Name = "heroIllustrationPanel";
        heroIllustrationPanel.Size = new Size(620, 104);
        heroIllustrationPanel.TabIndex = 3;
        // 
        // heroShieldPanel
        // 
        heroShieldPanel.BackColor = Color.Transparent;
        heroShieldPanel.BorderColor = Color.FromArgb(203, 213, 225);
        heroShieldPanel.BorderRadius = 14;
        heroShieldPanel.Controls.Add(heroShieldLabel);
        heroShieldPanel.FillColor = Color.FromArgb(30, 41, 59);
        heroShieldPanel.Location = new Point(250, 12);
        heroShieldPanel.Name = "heroShieldPanel";
        heroShieldPanel.Size = new Size(132, 86);
        heroShieldPanel.TabIndex = 0;
        // 
        // heroShieldLabel
        // 
        heroShieldLabel.Dock = DockStyle.Fill;
        heroShieldLabel.Font = new Font("Segoe MDL2 Assets", 38F);
        heroShieldLabel.ForeColor = Color.FromArgb(239, 68, 68);
        heroShieldLabel.Location = new Point(0, 0);
        heroShieldLabel.Name = "heroShieldLabel";
        heroShieldLabel.Size = new Size(132, 86);
        heroShieldLabel.TabIndex = 0;
        heroShieldLabel.Text = "";
        heroShieldLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // heroLockPanel
        // 
        heroLockPanel.BackColor = Color.Transparent;
        heroLockPanel.Controls.Add(heroLockLabel);
        heroLockPanel.FillColor = Color.FromArgb(229, 27, 43);
        heroLockPanel.Location = new Point(386, 58);
        heroLockPanel.Name = "heroLockPanel";
        heroLockPanel.Size = new Size(42, 38);
        heroLockPanel.TabIndex = 1;
        // 
        // heroLockLabel
        // 
        heroLockLabel.Dock = DockStyle.Fill;
        heroLockLabel.Font = new Font("Segoe MDL2 Assets", 17F);
        heroLockLabel.ForeColor = Color.White;
        heroLockLabel.Location = new Point(0, 0);
        heroLockLabel.Name = "heroLockLabel";
        heroLockLabel.Size = new Size(42, 38);
        heroLockLabel.TabIndex = 0;
        heroLockLabel.Text = "";
        heroLockLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // PermissaoForm
        // 
        AutoScaleDimensions = new SizeF(7F, 16F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        ClientSize = new Size(1366, 720);
        Controls.Add(rootLayout);
        Font = new Font("Cascadia Code", 9F);
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1180, 648);
        Name = "PermissaoForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Perfis de Acesso";
        WindowState = FormWindowState.Maximized;
        rootLayout.ResumeLayout(false);
        contentPanel.ResumeLayout(false);
        contentLayout.ResumeLayout(false);
        bodyLayout.ResumeLayout(false);
        profilesCard.ResumeLayout(false);
        duplicarButtonPanel.ResumeLayout(false);
        novoPerfilButtonPanel.ResumeLayout(false);
        profilesSearchPanel.ResumeLayout(false);
        profilesSearchPanel.PerformLayout();
        profilesTablePanel.ResumeLayout(false);
        detailsCard.ResumeLayout(false);
        nomePerfilInputPanel.ResumeLayout(false);
        nomePerfilInputPanel.PerformLayout();
        descricaoInputPanel.ResumeLayout(false);
        descricaoInputPanel.PerformLayout();
        situacaoInputPanel.ResumeLayout(false);
        summaryCard.ResumeLayout(false);
        _workspacePanel.ResumeLayout(false);
        workspaceConteudo.ResumeLayout(false);
        esquerdaCard.ResumeLayout(false);
        direitaCard.ResumeLayout(false);
        _filtrosCardPanel.ResumeLayout(false);
        _limparFiltrosButtonPanel.ResumeLayout(false);
        _pesquisarFiltrosButtonPanel.ResumeLayout(false);
        _perfilFiltroPanel.ResumeLayout(false);
        _moduloFiltroPanel.ResumeLayout(false);
        _recursoFiltroPanel.ResumeLayout(false);
        _recursoFiltroPanel.PerformLayout();
        _statusFiltroPanel.ResumeLayout(false);
        footerBar.ResumeLayout(false);
        footerBarLayout.ResumeLayout(false);
        cellUser.ResumeLayout(false);
        cellTerminal.ResumeLayout(false);
        cellEmpresa.ResumeLayout(false);
        cellBanco.ResumeLayout(false);
        cellHora.ResumeLayout(false);
        cellData.ResumeLayout(false);
        customTitleBarPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).EndInit();
        headerTitleIconPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).EndInit();
        sapStatusPanel.ResumeLayout(false);
        nivelAcessoInputPanel.ResumeLayout(false);
        usuariosInputPanel.ResumeLayout(false);
        usuariosInputPanel.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)profilesDataGridView).EndInit();
        heroPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)heroLogoPictureBox).EndInit();
        heroIllustrationPanel.ResumeLayout(false);
        heroShieldPanel.ResumeLayout(false);
        heroLockPanel.ResumeLayout(false);
        ResumeLayout(false);
    }


    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
    private Label profilesCheckMarkLabel;
    private Label label1;
    private Panel _workspacePanel;
    private ComboBox _filtroPerfilComboBox;
    private ComboBox _filtroModuloComboBox;
    private TextBox _filtroRecursoTextBox;
    private ComboBox _filtroStatusComboBox;
    private ListBox _modulosListBox;
    private DataGridView _permissoesGrid = null;
    private Label _rodapeListaLabel;
    private Label _tituloPermissoesPerfilLabel;
    private RoundedPanel _filtrosCardPanel;
    private RoundedPanel _perfilFiltroPanel;
    private RoundedPanel _moduloFiltroPanel;
    private RoundedPanel _recursoFiltroPanel;
    private RoundedPanel _statusFiltroPanel;
    private Label _perfilFiltroLabelUi;
    private Label _moduloFiltroLabelUi;
    private Label _recursoFiltroLabelUi;
    private Label _statusFiltroLabelUi;
    private RoundedPanel _limparFiltrosButtonPanel;
    private RoundedPanel _pesquisarFiltrosButtonPanel;
    private RoundedIconButton _limparFiltrosButton;
    private RoundedIconButton _pesquisarFiltrosButton;
    private TableLayoutPanel workspaceConteudo;
    private RoundedPanel esquerdaCard;
    private Label esquerdaTitulo;
    private RoundedPanel direitaCard;
    private RoundedIconButton restaurarButton;
    private RoundedIconButton salvarPermissoesButton;
    private Label filtrosIconLabel;
    private Label filtrosTitulo;
    private Label recursoIcone;
}








