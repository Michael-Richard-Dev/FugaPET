using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

partial class CamposEtiquetaForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootLayout;
    private Panel headerBar;
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
    private Panel contentPanel;
    private TableLayoutPanel contentLayout;
    private TableLayoutPanel bodyLayout;
    private RoundedPanel camposCard;
    private RoundedPanel dadosCampoCard;
    private RoundedPanel mapeamentoCard;
    private Label etiquetaTituloLabel;
    private Label etiquetaCodigoLabel;
    private Label etiquetaResumoLabel;
    private Button novoCampoButton;
    private RoundedPanel searchPanel;
    private Label searchIconLabel;
    private TextBox searchTextBox;
    private RoundedPanel camposTablePanel;
    private Panel camposHeaderPanel;
    private Label ordemHeaderLabel;
    private Label campoHeaderLabel;
    private Label tipoHeaderLabel;
    private Label situacaoHeaderLabel;
    private FlowLayoutPanel camposRowsPanel;
    private Label listFooterLabel;
    private Label dadosCampoTituloLabel;
    private Label estadoCampoLabel;
    private Panel dadosVazioPanel;
    private Label dadosVazioIconLabel;
    private Label dadosVazioTextoLabel;
    private Panel dadosEditorPanel;
    private Label nomeCampoLabel;
    private RoundedPanel nomeCampoInputPanel;
    private TextBox nomeCampoTextBox;
    private Label tipoDadoLabel;
    private RoundedPanel tipoDadoInputPanel;
    private ComboBox tipoDadoComboBox;
    private Label ordemLabel;
    private RoundedPanel ordemInputPanel;
    private TextBox ordemTextBox;
    private CheckBox obrigatorioCheckBox;
    private Label tamanhoMaximoLabel;
    private RoundedPanel tamanhoMaximoInputPanel;
    private TextBox tamanhoMaximoTextBox;
    private Label formatoSaidaLabel;
    private RoundedPanel formatoSaidaInputPanel;
    private TextBox formatoSaidaTextBox;
    private Label descricaoLabel;
    private RoundedPanel descricaoInputPanel;
    private TextBox descricaoTextBox;
    private Label situacaoCampoLabel;
    private RoundedPanel situacaoCampoInputPanel;
    private ComboBox situacaoCampoComboBox;
    private Button salvarCampoButton;
    private Button editarCampoButton;
    private Button situacaoCampoButton;
    private Label mapeamentoTituloLabel;
    private Label mappingInfoLabel;
    private Label mappingCampoLabel;
    private Label mappingSituacaoCampoLabel;
    private Label mappingSituacaoMapeamentoLabel;
    private Panel mapeamentoVazioPanel;
    private Label mapeamentoVazioIconLabel;
    private Label mapeamentoVazioTextoLabel;
    private Panel mapeamentoEditorPanel;
    private Label origemDadoLabel;
    private RoundedPanel origemDadoInputPanel;
    private ComboBox origemDadoComboBox;
    private Label expressaoOrigemLabel;
    private RoundedPanel expressaoOrigemInputPanel;
    private TextBox expressaoOrigemTextBox;
    private Label valorPadraoLabel;
    private RoundedPanel valorPadraoInputPanel;
    private TextBox valorPadraoTextBox;
    private CheckBox obrigatorioImpressaoCheckBox;
    private Label observacaoMapeamentoLabel;
    private RoundedPanel observacaoMapeamentoInputPanel;
    private TextBox observacaoMapeamentoTextBox;
    private Button salvarMapeamentoButton;
    private Button inativarMapeamentoButton;
    private Panel footerBar;
    private TableLayoutPanel footerBarLayout;
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
    private System.Windows.Forms.Timer clockTimer;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        rootLayout = new TableLayoutPanel(); headerBar = new Panel(); menuHeaderLabel = new Label(); companyLogoPictureBox = new PictureBox(); headerDividerLabel = new Label(); headerTitleIconPanel = new RoundedPanel(); headerTitleIconPictureBox = new PictureBox(); headerTitleLabel = new Label(); headerSubtitleLabel = new Label(); minimizeWindowLabel = new Label(); maximizeWindowLabel = new Label(); closeWindowLabel = new Label();
        contentPanel = new Panel(); contentLayout = new TableLayoutPanel(); bodyLayout = new TableLayoutPanel(); camposCard = new RoundedPanel(); dadosCampoCard = new RoundedPanel(); mapeamentoCard = new RoundedPanel();
        etiquetaTituloLabel = new Label(); etiquetaCodigoLabel = new Label(); etiquetaResumoLabel = new Label(); novoCampoButton = new Button(); searchPanel = new RoundedPanel(); searchIconLabel = new Label(); searchTextBox = new TextBox(); camposTablePanel = new RoundedPanel(); camposHeaderPanel = new Panel(); ordemHeaderLabel = new Label(); campoHeaderLabel = new Label(); tipoHeaderLabel = new Label(); situacaoHeaderLabel = new Label(); camposRowsPanel = new FlowLayoutPanel(); listFooterLabel = new Label();
        dadosCampoTituloLabel = new Label(); estadoCampoLabel = new Label(); dadosVazioPanel = new Panel(); dadosVazioIconLabel = new Label(); dadosVazioTextoLabel = new Label(); dadosEditorPanel = new Panel(); nomeCampoLabel = new Label(); nomeCampoInputPanel = new RoundedPanel(); nomeCampoTextBox = new TextBox(); tipoDadoLabel = new Label(); tipoDadoInputPanel = new RoundedPanel(); tipoDadoComboBox = new ComboBox(); ordemLabel = new Label(); ordemInputPanel = new RoundedPanel(); ordemTextBox = new TextBox(); obrigatorioCheckBox = new CheckBox(); tamanhoMaximoLabel = new Label(); tamanhoMaximoInputPanel = new RoundedPanel(); tamanhoMaximoTextBox = new TextBox(); formatoSaidaLabel = new Label(); formatoSaidaInputPanel = new RoundedPanel(); formatoSaidaTextBox = new TextBox(); descricaoLabel = new Label(); descricaoInputPanel = new RoundedPanel(); descricaoTextBox = new TextBox(); situacaoCampoLabel = new Label(); situacaoCampoInputPanel = new RoundedPanel(); situacaoCampoComboBox = new ComboBox(); salvarCampoButton = new Button(); editarCampoButton = new Button(); situacaoCampoButton = new Button();
        mapeamentoTituloLabel = new Label(); mappingInfoLabel = new Label(); mappingCampoLabel = new Label(); mappingSituacaoCampoLabel = new Label(); mappingSituacaoMapeamentoLabel = new Label(); mapeamentoVazioPanel = new Panel(); mapeamentoVazioIconLabel = new Label(); mapeamentoVazioTextoLabel = new Label(); mapeamentoEditorPanel = new Panel(); origemDadoLabel = new Label(); origemDadoInputPanel = new RoundedPanel(); origemDadoComboBox = new ComboBox(); expressaoOrigemLabel = new Label(); expressaoOrigemInputPanel = new RoundedPanel(); expressaoOrigemTextBox = new TextBox(); valorPadraoLabel = new Label(); valorPadraoInputPanel = new RoundedPanel(); valorPadraoTextBox = new TextBox(); obrigatorioImpressaoCheckBox = new CheckBox(); observacaoMapeamentoLabel = new Label(); observacaoMapeamentoInputPanel = new RoundedPanel(); observacaoMapeamentoTextBox = new TextBox(); salvarMapeamentoButton = new Button(); inativarMapeamentoButton = new Button();
        footerBar = new Panel(); footerBarLayout = new TableLayoutPanel(); cellUser = new Panel(); cellUserIcon = new Label(); cellUserText = new Label(); cellUserDivider = new Panel(); cellTerminal = new Panel(); cellTerminalIcon = new Label(); cellTerminalText = new Label(); cellTerminalDivider = new Panel(); cellEmpresa = new Panel(); cellEmpresaIcon = new Label(); cellEmpresaText = new Label(); cellEmpresaDivider = new Panel(); cellBanco = new Panel(); cellBancoIcon = new Label(); cellBancoText = new Label(); cellBancoDivider = new Panel(); cellHora = new Panel(); cellHoraIcon = new Label(); cellHoraText = new Label(); cellHoraDivider = new Panel(); cellData = new Panel(); cellDataIcon = new Label(); cellDataText = new Label(); clockTimer = new System.Windows.Forms.Timer(components);
        rootLayout.SuspendLayout(); headerBar.SuspendLayout(); ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).BeginInit(); headerTitleIconPanel.SuspendLayout(); ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).BeginInit(); contentPanel.SuspendLayout(); contentLayout.SuspendLayout(); bodyLayout.SuspendLayout(); camposCard.SuspendLayout(); searchPanel.SuspendLayout(); camposTablePanel.SuspendLayout(); camposHeaderPanel.SuspendLayout(); dadosCampoCard.SuspendLayout(); dadosVazioPanel.SuspendLayout(); dadosEditorPanel.SuspendLayout(); nomeCampoInputPanel.SuspendLayout(); tipoDadoInputPanel.SuspendLayout(); ordemInputPanel.SuspendLayout(); tamanhoMaximoInputPanel.SuspendLayout(); formatoSaidaInputPanel.SuspendLayout(); descricaoInputPanel.SuspendLayout(); situacaoCampoInputPanel.SuspendLayout(); mapeamentoCard.SuspendLayout(); mapeamentoVazioPanel.SuspendLayout(); mapeamentoEditorPanel.SuspendLayout(); origemDadoInputPanel.SuspendLayout(); expressaoOrigemInputPanel.SuspendLayout(); valorPadraoInputPanel.SuspendLayout(); observacaoMapeamentoInputPanel.SuspendLayout(); footerBar.SuspendLayout(); footerBarLayout.SuspendLayout(); cellUser.SuspendLayout(); cellTerminal.SuspendLayout(); cellEmpresa.SuspendLayout(); cellBanco.SuspendLayout(); cellHora.SuspendLayout(); cellData.SuspendLayout(); SuspendLayout();
        rootLayout.BackColor = Color.FromArgb(247, 248, 250); rootLayout.ColumnCount = 1; rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); rootLayout.Dock = DockStyle.Fill; rootLayout.RowCount = 3; rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F)); rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F)); rootLayout.Controls.Add(headerBar, 0, 0); rootLayout.Controls.Add(contentPanel, 0, 1); rootLayout.Controls.Add(footerBar, 0, 2);
        headerBar.BackColor = Color.FromArgb(15, 23, 42); headerBar.Dock = DockStyle.Fill; headerBar.Controls.Add(menuHeaderLabel); headerBar.Controls.Add(companyLogoPictureBox); headerBar.Controls.Add(headerDividerLabel); headerBar.Controls.Add(headerTitleIconPanel); headerBar.Controls.Add(headerTitleLabel); headerBar.Controls.Add(headerSubtitleLabel); headerBar.Controls.Add(minimizeWindowLabel); headerBar.Controls.Add(maximizeWindowLabel); headerBar.Controls.Add(closeWindowLabel);
        menuHeaderLabel.Font = new Font("Segoe MDL2 Assets", 15F); menuHeaderLabel.ForeColor = Color.White; menuHeaderLabel.Location = new Point(18, 8); menuHeaderLabel.Size = new Size(36, 36); menuHeaderLabel.Text = ""; menuHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        companyLogoPictureBox.Image = global::FugaPET_Dev.Properties.Resources.fuga_2026_logo; companyLogoPictureBox.Location = new Point(60, 5); companyLogoPictureBox.Size = new Size(128, 43); companyLogoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerDividerLabel.BackColor = Color.FromArgb(132, 142, 156); headerDividerLabel.Location = new Point(208, 12); headerDividerLabel.Size = new Size(1, 30);
        headerTitleIconPanel.BorderColor = Color.Transparent; headerTitleIconPanel.BorderRadius = 0; headerTitleIconPanel.FillColor = Color.Transparent; headerTitleIconPanel.Location = new Point(232, 10); headerTitleIconPanel.Size = new Size(32, 32); headerTitleIconPanel.Controls.Add(headerTitleIconPictureBox);
        headerTitleIconPictureBox.BackColor = Color.Transparent; headerTitleIconPictureBox.Image = global::FugaPET_Dev.Properties.Resources.carga_de_trabalho_24x_white; headerTitleIconPictureBox.Location = new Point(4, 4); headerTitleIconPictureBox.Size = new Size(24, 24); headerTitleIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerTitleLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold); headerTitleLabel.ForeColor = Color.White; headerTitleLabel.Location = new Point(278, 6); headerTitleLabel.Size = new Size(330, 23); headerTitleLabel.Text = "Campos da Etiqueta";
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F); headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228); headerSubtitleLabel.Location = new Point(279, 29); headerSubtitleLabel.Size = new Size(620, 17); headerSubtitleLabel.Text = "Configuração técnica dos campos e mapeamentos da etiqueta";
        ConfigureWindowButton(minimizeWindowLabel, "–", new Point(1218, 0), new Font("Segoe UI", 12F)); ConfigureWindowButton(maximizeWindowLabel, "", new Point(1266, 0), new Font("Segoe MDL2 Assets", 9F)); ConfigureWindowButton(closeWindowLabel, "", new Point(1314, 0), new Font("Segoe MDL2 Assets", 9F));
        contentPanel.BackColor = Color.FromArgb(247, 248, 250); contentPanel.Controls.Add(contentLayout); contentPanel.Dock = DockStyle.Fill;
        contentLayout.ColumnCount = 1; contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F)); contentLayout.Dock = DockStyle.Fill; contentLayout.Padding = new Padding(24, 22, 24, 22); contentLayout.Controls.Add(bodyLayout, 0, 0);
        bodyLayout.ColumnCount = 3; bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F)); bodyLayout.Dock = DockStyle.Fill; bodyLayout.RowCount = 1; bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); bodyLayout.Controls.Add(camposCard, 0, 0); bodyLayout.Controls.Add(dadosCampoCard, 1, 0); bodyLayout.Controls.Add(mapeamentoCard, 2, 0);
        ConfigureCard(camposCard); ConfigureCard(dadosCampoCard); ConfigureCard(mapeamentoCard);
        camposCard.Controls.Add(etiquetaTituloLabel); camposCard.Controls.Add(etiquetaCodigoLabel); camposCard.Controls.Add(etiquetaResumoLabel); camposCard.Controls.Add(novoCampoButton); camposCard.Controls.Add(searchPanel); camposCard.Controls.Add(camposTablePanel); camposCard.Controls.Add(listFooterLabel);
        ConfigureTitleLabel(etiquetaTituloLabel, "Etiqueta"); ConfigureTextLabel(etiquetaCodigoLabel, "Código interno: -"); ConfigureTextLabel(etiquetaResumoLabel, "Campos vinculados à etiqueta selecionada"); ConfigureActionButton(novoCampoButton, "+  Novo Campo");
        searchPanel.BorderRadius = 8; searchPanel.FillColor = Color.White; searchPanel.BorderColor = Color.FromArgb(203, 213, 225); searchPanel.Controls.Add(searchIconLabel); searchPanel.Controls.Add(searchTextBox);
        searchIconLabel.Font = new Font("Segoe MDL2 Assets", 14F); searchIconLabel.ForeColor = Color.FromArgb(71, 85, 105); searchIconLabel.Text = ""; searchIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        searchTextBox.BorderStyle = BorderStyle.None; searchTextBox.Font = new Font("Segoe UI", 9F); searchTextBox.PlaceholderText = "Pesquisar campos...";
        camposTablePanel.BorderRadius = 8; camposTablePanel.FillColor = Color.White; camposTablePanel.BorderColor = Color.FromArgb(226, 232, 240); camposTablePanel.Controls.Add(camposHeaderPanel); camposTablePanel.Controls.Add(camposRowsPanel);
        camposHeaderPanel.BackColor = Color.White; camposHeaderPanel.Controls.Add(ordemHeaderLabel); camposHeaderPanel.Controls.Add(campoHeaderLabel); camposHeaderPanel.Controls.Add(tipoHeaderLabel); camposHeaderPanel.Controls.Add(situacaoHeaderLabel); ConfigureHeaderLabel(ordemHeaderLabel, "Ordem"); ConfigureHeaderLabel(campoHeaderLabel, "Campo"); ConfigureHeaderLabel(tipoHeaderLabel, "Tipo"); ConfigureHeaderLabel(situacaoHeaderLabel, "Situação");
        camposRowsPanel.AutoScroll = true; camposRowsPanel.FlowDirection = FlowDirection.TopDown; camposRowsPanel.WrapContents = false; camposRowsPanel.BackColor = Color.White; ConfigureTextLabel(listFooterLabel, "Exibindo 0 de 0 campos");
        dadosCampoCard.Controls.Add(dadosCampoTituloLabel); dadosCampoCard.Controls.Add(estadoCampoLabel); dadosCampoCard.Controls.Add(dadosVazioPanel); dadosCampoCard.Controls.Add(dadosEditorPanel); ConfigureTitleLabel(dadosCampoTituloLabel, "Dados do Campo"); ConfigureTextLabel(estadoCampoLabel, "Selecione um campo cadastrado ou clique em Novo Campo para iniciar.");
        dadosVazioPanel.BackColor = Color.Transparent; dadosVazioPanel.Controls.Add(dadosVazioIconLabel); dadosVazioPanel.Controls.Add(dadosVazioTextoLabel); dadosVazioIconLabel.Font = new Font("Segoe MDL2 Assets", 26F); dadosVazioIconLabel.ForeColor = Color.FromArgb(148, 163, 184); dadosVazioIconLabel.Text = ""; dadosVazioIconLabel.TextAlign = ContentAlignment.BottomCenter; dadosVazioIconLabel.Dock = DockStyle.Top; dadosVazioIconLabel.Height = 72; dadosVazioTextoLabel.Dock = DockStyle.Top; dadosVazioTextoLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold); dadosVazioTextoLabel.ForeColor = Color.FromArgb(100, 116, 139); dadosVazioTextoLabel.Text = "Nenhum campo selecionado"; dadosVazioTextoLabel.TextAlign = ContentAlignment.TopCenter;
        ConfigurarEditorCampo();
        mapeamentoCard.Controls.Add(mapeamentoTituloLabel); mapeamentoCard.Controls.Add(mappingInfoLabel); mapeamentoCard.Controls.Add(mappingCampoLabel); mapeamentoCard.Controls.Add(mappingSituacaoCampoLabel); mapeamentoCard.Controls.Add(mappingSituacaoMapeamentoLabel); mapeamentoCard.Controls.Add(mapeamentoVazioPanel); mapeamentoCard.Controls.Add(mapeamentoEditorPanel);
        ConfigureTitleLabel(mapeamentoTituloLabel, "Mapeamento do Campo"); ConfigureTextLabel(mappingInfoLabel, "Selecione um campo."); ConfigureBoldTextLabel(mappingCampoLabel, "Campo selecionado: -"); ConfigureTextLabel(mappingSituacaoCampoLabel, "Situação do Campo: -"); ConfigureTextLabel(mappingSituacaoMapeamentoLabel, "Situação do Mapeamento: -");
        mapeamentoVazioPanel.BackColor = Color.Transparent; mapeamentoVazioPanel.Controls.Add(mapeamentoVazioIconLabel); mapeamentoVazioPanel.Controls.Add(mapeamentoVazioTextoLabel); mapeamentoVazioIconLabel.Font = new Font("Segoe MDL2 Assets", 26F); mapeamentoVazioIconLabel.ForeColor = Color.FromArgb(148, 163, 184); mapeamentoVazioIconLabel.Text = ""; mapeamentoVazioIconLabel.TextAlign = ContentAlignment.BottomCenter; mapeamentoVazioIconLabel.Dock = DockStyle.Top; mapeamentoVazioIconLabel.Height = 72; mapeamentoVazioTextoLabel.Dock = DockStyle.Top; mapeamentoVazioTextoLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold); mapeamentoVazioTextoLabel.ForeColor = Color.FromArgb(100, 116, 139); mapeamentoVazioTextoLabel.Text = "Selecione um campo para configurar o mapeamento"; mapeamentoVazioTextoLabel.TextAlign = ContentAlignment.TopCenter;
        ConfigurarEditorMapeamento();
        footerBar.BackColor = Color.White; footerBar.Controls.Add(footerBarLayout); footerBar.Dock = DockStyle.Fill;
        footerBarLayout.ColumnCount = 6; footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6F)); footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.6F)); footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26.8F)); footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F)); footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F)); footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F)); footerBarLayout.Dock = DockStyle.Fill; footerBarLayout.Controls.Add(cellUser, 0, 0); footerBarLayout.Controls.Add(cellTerminal, 1, 0); footerBarLayout.Controls.Add(cellEmpresa, 2, 0); footerBarLayout.Controls.Add(cellBanco, 3, 0); footerBarLayout.Controls.Add(cellHora, 4, 0); footerBarLayout.Controls.Add(cellData, 5, 0);
        ConfigureFooterCell(cellUser, cellUserIcon, cellUserText, cellUserDivider, "", "Usuário: -"); ConfigureFooterCell(cellTerminal, cellTerminalIcon, cellTerminalText, cellTerminalDivider, "", "Terminal: -"); ConfigureFooterCell(cellEmpresa, cellEmpresaIcon, cellEmpresaText, cellEmpresaDivider, "", "Empresa: FUGA COUROS S.A."); ConfigureFooterCell(cellBanco, cellBancoIcon, cellBancoText, cellBancoDivider, "", "Banco de Dados: -"); ConfigureFooterCell(cellHora, cellHoraIcon, cellHoraText, cellHoraDivider, "", "00:00"); ConfigureFooterCell(cellData, cellDataIcon, cellDataText, new Panel(), "", "00/00/0000");
        clockTimer.Interval = 30000;
        AutoScaleDimensions = new SizeF(7F, 16F); AutoScaleMode = AutoScaleMode.Font; BackColor = Color.FromArgb(247, 248, 250); ClientSize = new Size(1366, 720); Controls.Add(rootLayout); Font = new Font("Cascadia Code", 9F); FormBorderStyle = FormBorderStyle.None; MinimumSize = new Size(1180, 648); Name = "CamposEtiquetaForm"; StartPosition = FormStartPosition.CenterScreen; Text = "Campos da Etiqueta"; WindowState = FormWindowState.Maximized;
        rootLayout.ResumeLayout(false); headerBar.ResumeLayout(false); ((System.ComponentModel.ISupportInitialize)companyLogoPictureBox).EndInit(); headerTitleIconPanel.ResumeLayout(false); ((System.ComponentModel.ISupportInitialize)headerTitleIconPictureBox).EndInit(); contentPanel.ResumeLayout(false); contentLayout.ResumeLayout(false); bodyLayout.ResumeLayout(false); camposCard.ResumeLayout(false); searchPanel.ResumeLayout(false); searchPanel.PerformLayout(); camposTablePanel.ResumeLayout(false); camposHeaderPanel.ResumeLayout(false); dadosCampoCard.ResumeLayout(false); dadosVazioPanel.ResumeLayout(false); dadosEditorPanel.ResumeLayout(false); nomeCampoInputPanel.ResumeLayout(false); nomeCampoInputPanel.PerformLayout(); tipoDadoInputPanel.ResumeLayout(false); ordemInputPanel.ResumeLayout(false); ordemInputPanel.PerformLayout(); tamanhoMaximoInputPanel.ResumeLayout(false); tamanhoMaximoInputPanel.PerformLayout(); formatoSaidaInputPanel.ResumeLayout(false); formatoSaidaInputPanel.PerformLayout(); descricaoInputPanel.ResumeLayout(false); descricaoInputPanel.PerformLayout(); situacaoCampoInputPanel.ResumeLayout(false); mapeamentoCard.ResumeLayout(false); mapeamentoVazioPanel.ResumeLayout(false); mapeamentoEditorPanel.ResumeLayout(false); origemDadoInputPanel.ResumeLayout(false); expressaoOrigemInputPanel.ResumeLayout(false); expressaoOrigemInputPanel.PerformLayout(); valorPadraoInputPanel.ResumeLayout(false); valorPadraoInputPanel.PerformLayout(); observacaoMapeamentoInputPanel.ResumeLayout(false); observacaoMapeamentoInputPanel.PerformLayout(); footerBar.ResumeLayout(false); footerBarLayout.ResumeLayout(false); cellUser.ResumeLayout(false); cellTerminal.ResumeLayout(false); cellEmpresa.ResumeLayout(false); cellBanco.ResumeLayout(false); cellHora.ResumeLayout(false); cellData.ResumeLayout(false); ResumeLayout(false);
    }

    private static void ConfigureWindowButton(Label label, string text, Point location, Font font) { label.Anchor = AnchorStyles.Top | AnchorStyles.Right; label.BackColor = Color.Transparent; label.Cursor = Cursors.Hand; label.Font = font; label.ForeColor = Color.White; label.Location = location; label.Size = new Size(48, 52); label.Text = text; label.TextAlign = ContentAlignment.MiddleCenter; }
    private static void ConfigureCard(RoundedPanel card) { card.BorderRadius = 10; card.BorderThickness = 1; card.BorderColor = Color.FromArgb(226, 232, 240); card.FillColor = Color.White; card.Dock = DockStyle.Fill; card.Margin = new Padding(8, 0, 8, 0); card.ShadowEnabled = false; }
    private static void ConfigureTitleLabel(Label label, string text) { label.Font = new Font("Segoe UI", 15F, FontStyle.Bold); label.ForeColor = Color.FromArgb(15, 23, 42); label.Text = text; }
    private static void ConfigureTextLabel(Label label, string text) { label.Font = new Font("Segoe UI", 9F); label.ForeColor = Color.FromArgb(100, 116, 139); label.Text = text; label.AutoEllipsis = true; }
    private static void ConfigureBoldTextLabel(Label label, string text) { ConfigureTextLabel(label, text); label.Font = new Font("Segoe UI", 9F, FontStyle.Bold); label.ForeColor = Color.FromArgb(15, 23, 42); }
    private static void ConfigureHeaderLabel(Label label, string text) { label.Font = new Font("Segoe UI", 8.25F, FontStyle.Bold); label.ForeColor = Color.FromArgb(15, 23, 42); label.Text = text; label.AutoEllipsis = true; }
    private static void ConfigureActionButton(Button button, string text) { button.BackColor = Color.White; button.Cursor = Cursors.Hand; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225); button.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252); button.FlatAppearance.MouseDownBackColor = Color.FromArgb(241, 245, 249); button.Font = new Font("Segoe UI", 9F, FontStyle.Bold); button.ForeColor = Color.FromArgb(15, 23, 42); button.Text = text; button.UseVisualStyleBackColor = false; }

    private void ConfigurarEditorCampo()
    {
        dadosEditorPanel.BackColor = Color.Transparent; dadosEditorPanel.Controls.Add(nomeCampoLabel); dadosEditorPanel.Controls.Add(nomeCampoInputPanel); dadosEditorPanel.Controls.Add(tipoDadoLabel); dadosEditorPanel.Controls.Add(tipoDadoInputPanel); dadosEditorPanel.Controls.Add(ordemLabel); dadosEditorPanel.Controls.Add(ordemInputPanel); dadosEditorPanel.Controls.Add(obrigatorioCheckBox); dadosEditorPanel.Controls.Add(tamanhoMaximoLabel); dadosEditorPanel.Controls.Add(tamanhoMaximoInputPanel); dadosEditorPanel.Controls.Add(formatoSaidaLabel); dadosEditorPanel.Controls.Add(formatoSaidaInputPanel); dadosEditorPanel.Controls.Add(descricaoLabel); dadosEditorPanel.Controls.Add(descricaoInputPanel); dadosEditorPanel.Controls.Add(situacaoCampoLabel); dadosEditorPanel.Controls.Add(situacaoCampoInputPanel); dadosEditorPanel.Controls.Add(salvarCampoButton); dadosEditorPanel.Controls.Add(editarCampoButton); dadosEditorPanel.Controls.Add(situacaoCampoButton);
        ConfigureInputLabel(nomeCampoLabel, "Nome do Campo *"); ConfigureInputLabel(tipoDadoLabel, "Tipo de Dado *"); ConfigureInputLabel(ordemLabel, "Ordem *"); ConfigureInputLabel(tamanhoMaximoLabel, "Tamanho Máximo"); ConfigureInputLabel(formatoSaidaLabel, "Formato de Saída"); ConfigureInputLabel(descricaoLabel, "Descrição"); ConfigureInputLabel(situacaoCampoLabel, "Situação"); ConfigureInputPanel(nomeCampoInputPanel, nomeCampoTextBox); ConfigureInputPanel(tipoDadoInputPanel, tipoDadoComboBox); ConfigureInputPanel(ordemInputPanel, ordemTextBox); ConfigureInputPanel(tamanhoMaximoInputPanel, tamanhoMaximoTextBox); ConfigureInputPanel(formatoSaidaInputPanel, formatoSaidaTextBox); ConfigureInputPanel(descricaoInputPanel, descricaoTextBox); ConfigureInputPanel(situacaoCampoInputPanel, situacaoCampoComboBox);
        descricaoTextBox.Multiline = true; situacaoCampoComboBox.DropDownStyle = ComboBoxStyle.DropDownList; tipoDadoComboBox.DropDownStyle = ComboBoxStyle.DropDownList; obrigatorioCheckBox.Font = new Font("Segoe UI", 9F, FontStyle.Bold); obrigatorioCheckBox.ForeColor = Color.FromArgb(15, 23, 42); obrigatorioCheckBox.Text = "Obrigatório"; ConfigureActionButton(salvarCampoButton, "Salvar Campo             F5"); ConfigureActionButton(editarCampoButton, "Salvar Alterações       F6"); ConfigureActionButton(situacaoCampoButton, "Inativar Campo             F8");
    }

    private void ConfigurarEditorMapeamento()
    {
        mapeamentoEditorPanel.BackColor = Color.Transparent; mapeamentoEditorPanel.Controls.Add(origemDadoLabel); mapeamentoEditorPanel.Controls.Add(origemDadoInputPanel); mapeamentoEditorPanel.Controls.Add(expressaoOrigemLabel); mapeamentoEditorPanel.Controls.Add(expressaoOrigemInputPanel); mapeamentoEditorPanel.Controls.Add(valorPadraoLabel); mapeamentoEditorPanel.Controls.Add(valorPadraoInputPanel); mapeamentoEditorPanel.Controls.Add(obrigatorioImpressaoCheckBox); mapeamentoEditorPanel.Controls.Add(observacaoMapeamentoLabel); mapeamentoEditorPanel.Controls.Add(observacaoMapeamentoInputPanel); mapeamentoEditorPanel.Controls.Add(salvarMapeamentoButton); mapeamentoEditorPanel.Controls.Add(inativarMapeamentoButton);
        ConfigureInputLabel(origemDadoLabel, "Origem do Dado *"); ConfigureInputLabel(expressaoOrigemLabel, "Expressão de Origem"); ConfigureInputLabel(valorPadraoLabel, "Valor Padrão"); ConfigureInputLabel(observacaoMapeamentoLabel, "Observação"); ConfigureInputPanel(origemDadoInputPanel, origemDadoComboBox); ConfigureInputPanel(expressaoOrigemInputPanel, expressaoOrigemTextBox); ConfigureInputPanel(valorPadraoInputPanel, valorPadraoTextBox); ConfigureInputPanel(observacaoMapeamentoInputPanel, observacaoMapeamentoTextBox);
        origemDadoComboBox.DropDownStyle = ComboBoxStyle.DropDownList; observacaoMapeamentoTextBox.Multiline = true; obrigatorioImpressaoCheckBox.Font = new Font("Segoe UI", 9F, FontStyle.Bold); obrigatorioImpressaoCheckBox.ForeColor = Color.FromArgb(15, 23, 42); obrigatorioImpressaoCheckBox.Text = "Obrigatório para Impressão"; ConfigureActionButton(salvarMapeamentoButton, "Salvar Mapeamento"); ConfigureActionButton(inativarMapeamentoButton, "Inativar Mapeamento"); inativarMapeamentoButton.ForeColor = Color.FromArgb(185, 28, 28); inativarMapeamentoButton.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
    }

    private static void ConfigureInputLabel(Label label, string text) { label.Font = new Font("Segoe UI", 9F, FontStyle.Bold); label.ForeColor = Color.FromArgb(15, 23, 42); label.Text = text; }
    private static void ConfigureInputPanel(RoundedPanel panel, Control input) { panel.BorderRadius = 7; panel.BorderThickness = 1; panel.BorderColor = Color.FromArgb(203, 213, 225); panel.FillColor = Color.White; panel.Controls.Add(input); input.Font = new Font("Segoe UI", 9F); input.BackColor = Color.White; }
    private static void ConfigureFooterCell(Panel cell, Label icon, Label text, Panel divider, string iconText, string value)
    {
        cell.BackColor = Color.White; cell.Controls.Add(icon); cell.Controls.Add(text); cell.Controls.Add(divider);
        icon.Font = new Font("Segoe MDL2 Assets", 9F); icon.ForeColor = Color.FromArgb(239, 68, 68); icon.Location = new Point(12, 9); icon.Size = new Size(18, 18); icon.Text = iconText; icon.TextAlign = ContentAlignment.MiddleCenter;
        text.Font = new Font("Segoe UI", 8.25F); text.ForeColor = Color.FromArgb(51, 65, 85); text.Location = new Point(34, 8); text.Size = new Size(280, 20); text.Text = value; text.TextAlign = ContentAlignment.MiddleLeft;
        divider.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right; divider.BackColor = Color.FromArgb(226, 232, 240); divider.Location = new Point(cell.Width - 1, 0); divider.Size = new Size(1, 36);
    }
}

