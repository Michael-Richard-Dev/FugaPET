namespace FugaPET_Dev.Tela;

partial class PainelInicialForm
{
    private System.ComponentModel.IContainer components = null;

    // Root
    private TableLayoutPanel rootLayout;
    private TableLayoutPanel bodyLayout;
    private TableLayoutPanel rightAreaLayout;
    private Panel modulesSectionPanel;
    private Label modulesSectionTitleLabel;
    private Panel welcomeIllustrationPanel;

    // Header bar (white)
    private Panel headerBar;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel sapStatusPanel;
    private Label sapStatusDotLabel;
    private Label sapStatusLabel;
    private Label minimizeWindowLabel;
    private Label maximizeWindowLabel;
    private Label closeWindowLabel;

    // Sidebar (dark)
    private Panel sidebarPanel;
    private PictureBox sidebarLogoPictureBox;
    private Panel menuItemInicio;
    private Label menuInicioIcon;
    private Label menuInicioText;
    private Panel menuItemCadastro;
    private Label menuCadastroIcon;
    private Label menuCadastroText;
    private Panel menuItemLeitura;
    private Label menuLeituraIcon;
    private Label menuLeituraText;
    private Panel menuItemConsulta;
    private Label menuConsultaIcon;
    private Label menuConsultaText;
    private Panel menuItemEtiquetas;
    private Label menuEtiquetasIcon;
    private Label menuEtiquetasText;
    private Panel menuItemHistorico;
    private Label menuHistoricoIcon;
    private Label menuHistoricoText;
    private Panel menuItemRelatorios;
    private Label menuRelatoriosIcon;
    private Label menuRelatoriosText;
    private Panel menuItemSap;
    private Label menuSapIcon;
    private Label menuSapText;
    private Panel menuItemConfig;
    private Label menuConfigIcon;
    private Label menuConfigText;
    private Panel menuItemSeguranca;
    private Label menuSegurancaText;
    private Panel sidebarUserPanel;
    private Label sidebarUserAvatarLabel;
    private Label sidebarUserNameLabel;
    private Label sidebarUserStatusLabel;
    private Label sidebarUserChevronLabel;

    // Content
    private Panel contentScrollPanel;
    private TableLayoutPanel contentLayout;
    private FugaPET_Dev.Tela.Controls.RoundedPanel contentBrandHeaderPanel;
    private PictureBox contentBrandPictureBox;

    // Welcome banner
    private FugaPET_Dev.Tela.Controls.RoundedPanel welcomeBanner;
    private FugaPET_Dev.Tela.Controls.RoundedPanel welcomeIconBg;
    private Label welcomeIconLabel;
    private Label welcomeTitleLabel;
    private Label welcomeUserLabel;
    private Label welcomeSubtitleLabel;

    // Welcome illustration shapes (each editable in Designer)
    private Panel illusMachineBody;
    private Panel illusMachineScreen;
    private Panel illusMachineLed1;
    private Panel illusMachineLed2;
    private Panel illusShield;
    private Label illusShieldCheck;
    private Panel illusBox1;
    private Panel illusBox2;
    private Panel illusBox3;
    private Panel illusMidBox1;
    private Panel illusMidBox2;
    private Panel illusMidBox3;
    private Panel illusFloorLine;
    private Panel illusDot1;
    private Panel illusDot2;
    private Panel illusDot3;
    private Panel illusDot4;
    private Panel illusDot5;
    private Panel illusDot6;

    // Metric cards
    private TableLayoutPanel metricsLayout;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metricCard1;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metric1IconBg;
    private Label metric1IconLabel;
    private Label metric1CaptionLabel;
    private Label metric1ValueLabel;
    private Label metric1UnitLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metricCard2;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metric2IconBg;
    private Label metric2IconLabel;
    private Label metric2CaptionLabel;
    private Label metric2ValueLabel;
    private Label metric2UnitLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metricCard3;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metric3IconBg;
    private Label metric3IconLabel;
    private Label metric3CaptionLabel;
    private Label metric3ValueLabel;
    private Label metric3UnitLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metricCard4;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metric4IconBg;
    private Label metric4IconLabel;
    private Label metric4CaptionLabel;
    private Label metric4ValueLabel;
    private Label metric4UnitLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metricCard5;
    private FugaPET_Dev.Tela.Controls.RoundedPanel metric5IconBg;
    private Label metric5IconLabel;
    private Label metric5CaptionLabel;
    private Label metric5ValueLabel;
    private Label metric5UnitLabel;

    // Modules + Activities
    private TableLayoutPanel modulesActivitiesLayout;
    private FugaPET_Dev.Tela.Controls.RoundedPanel modulesPanel;
    private Label modulesTitleLabel;
    private TableLayoutPanel modulesGridLayout;
    private FugaPET_Dev.Tela.Controls.RoundedPanel moduleCard1;
    private FugaPET_Dev.Tela.Controls.RoundedPanel module1IconBg;
    private Label module1IconLabel;
    private Label module1TitleLabel;
    private Label module1DescLabel;
    private Label module1ArrowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel moduleCard2;
    private FugaPET_Dev.Tela.Controls.RoundedPanel module2IconBg;
    private Label module2IconLabel;
    private Label module2TitleLabel;
    private Label module2DescLabel;
    private Label module2ArrowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel moduleCard3;
    private FugaPET_Dev.Tela.Controls.RoundedPanel module3IconBg;
    private Label module3IconLabel;
    private Label module3TitleLabel;
    private Label module3DescLabel;
    private Label module3ArrowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel moduleCard4;
    private FugaPET_Dev.Tela.Controls.RoundedPanel module4IconBg;
    private Label module4IconLabel;
    private Label module4TitleLabel;
    private Label module4DescLabel;
    private Label module4ArrowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel moduleCard5;
    private FugaPET_Dev.Tela.Controls.RoundedPanel module5IconBg;
    private Label module5IconLabel;
    private Label module5TitleLabel;
    private Label module5DescLabel;
    private Label module5ArrowLabel;

    private FugaPET_Dev.Tela.Controls.RoundedPanel activitiesPanel;
    private Label activitiesTitleLabel;
    private Label activitiesViewAllLabel;
    private Panel activity1Panel;
    private Label activity1IconLabel;
    private Label activity1TitleLabel;
    private Label activity1SubtitleLabel;
    private Label activity1TimeLabel;
    private Panel activity2Panel;
    private Label activity2IconLabel;
    private Label activity2TitleLabel;
    private Label activity2SubtitleLabel;
    private Label activity2TimeLabel;
    private Panel activity3Panel;
    private Label activity3IconLabel;
    private Label activity3TitleLabel;
    private Label activity3SubtitleLabel;
    private Label activity3TimeLabel;
    private Panel activity4Panel;
    private Label activity4IconLabel;
    private Label activity4TitleLabel;
    private Label activity4SubtitleLabel;
    private Label activity4TimeLabel;
    private Panel activity5Panel;
    private Label activity5IconLabel;
    private Label activity5TitleLabel;
    private Label activity5SubtitleLabel;
    private Label activity5TimeLabel;
    private Label activitiesHistoryLabel;

    // Resumo Operacional
    private FugaPET_Dev.Tela.Controls.RoundedPanel resumoPanel;
    private Label resumoTitleLabel;
    private TableLayoutPanel resumoGridLayout;
    private FugaPET_Dev.Tela.Controls.RoundedPanel resumoCard1;
    private Label resumo1IconLabel;
    private Label resumo1TitleLabel;
    private Label resumo1StatusLabel;
    private Panel resumo1ProgressBg;
    private Panel resumo1ProgressFill;
    private FugaPET_Dev.Tela.Controls.RoundedPanel resumoCard2;
    private Label resumo2IconLabel;
    private Label resumo2TitleLabel;
    private Label resumo2StatusLabel;
    private Panel resumo2ProgressBg;
    private Panel resumo2ProgressFill;
    private FugaPET_Dev.Tela.Controls.RoundedPanel resumoCard3;
    private Label resumo3IconLabel;
    private Label resumo3TitleLabel;
    private Label resumo3StatusLabel;
    private Panel resumo3ProgressBg;
    private Panel resumo3ProgressFill;
    private FugaPET_Dev.Tela.Controls.RoundedPanel resumoCard4;
    private Label resumo4IconLabel;
    private Label resumo4TitleLabel;
    private Label resumo4StatusLabel;
    private Panel resumo4ProgressBg;
    private Panel resumo4ProgressFill;
    private FugaPET_Dev.Tela.Controls.RoundedPanel resumoCard5;
    private Label resumo5IconLabel;
    private Label resumo5TitleLabel;
    private Label resumo5StatusLabel;
    private Panel resumo5ProgressBg;
    private Panel resumo5ProgressFill;

    // Ações Rápidas
    private Panel acoesPanel;
    private Label acoesTitleLabel;
    private TableLayoutPanel acoesGridLayout;
    private FugaPET_Dev.Tela.Controls.RoundedPanel acaoCard1;
    private FugaPET_Dev.Tela.Controls.RoundedPanel acao1IconBg;
    private Label acao1IconLabel;
    private Label acao1TitleLabel;
    private Label acao1DescLabel;
    private Label acao1ArrowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel acaoCard2;
    private FugaPET_Dev.Tela.Controls.RoundedPanel acao2IconBg;
    private Label acao2IconLabel;
    private Label acao2TitleLabel;
    private Label acao2DescLabel;
    private Label acao2ArrowLabel;
    private FugaPET_Dev.Tela.Controls.RoundedPanel acaoCard3;
    private FugaPET_Dev.Tela.Controls.RoundedPanel acao3IconBg;
    private Label acao3IconLabel;
    private Label acao3TitleLabel;
    private Label acao3DescLabel;
    private Label acao3ArrowLabel;

    // Footer
    private Panel footerBar;
    private TableLayoutPanel footerBarLayout;
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
    private Panel cellDataDivider;

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
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PainelInicialForm));
        rootLayout = new TableLayoutPanel();
        bodyLayout = new TableLayoutPanel();
        sidebarPanel = new Panel();
        sidebarLogoPictureBox = new PictureBox();
        menuItemInicio = new Panel();
        menuInicioIcon = new Label();
        menuInicioText = new Label();
        menuItemCadastro = new Panel();
        menuCadastroIcon = new Label();
        menuCadastroText = new Label();
        menuItemLeitura = new Panel();
        menuLeituraIcon = new Label();
        menuLeituraText = new Label();
        menuItemEtiquetas = new Panel();
        menuEtiquetasIcon = new Label();
        menuEtiquetasText = new Label();
        menuItemHistorico = new Panel();
        menuHistoricoIcon = new Label();
        menuHistoricoText = new Label();
        menuItemRelatorios = new Panel();
        menuRelatoriosIcon = new Label();
        menuRelatoriosText = new Label();
        menuItemSap = new Panel();
        menuSapIcon = new Label();
        menuSapText = new Label();
        menuItemConfig = new Panel();
        menuConfigIcon = new Label();
        menuConfigText = new Label();
        menuItemSeguranca = new Panel();
        menuSegurancaIcon = new Label();
        menuSegurancaText = new Label();
        sidebarUserPanel = new Panel();
        sidebarUserAvatarLabel = new Label();
        sidebarUserNameLabel = new Label();
        sidebarUserStatusLabel = new Label();
        sidebarUserChevronLabel = new Label();
        rightAreaLayout = new TableLayoutPanel();
        headerBar = new Panel();
        headerSubtitleLabel = new Label();
        sapStatusPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        sapStatusDotLabel = new Label();
        sapStatusLabel = new Label();
        minimizeWindowLabel = new Label();
        maximizeWindowLabel = new Label();
        closeWindowLabel = new Label();
        headerTitleLabel = new Label();
        contentScrollPanel = new Panel();
        contentLayout = new TableLayoutPanel();
        contentBrandHeaderPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        logoSaLabel = new Label();
        contentBrandPictureBox = new PictureBox();
        footerBar = new Panel();
        footerBarLayout = new TableLayoutPanel();
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
        cellDataDivider = new Panel();
        menuItemConsulta = new Panel();
        menuConsultaIcon = new Label();
        menuConsultaText = new Label();
        welcomeBanner = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        welcomeIconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        welcomeIconLabel = new Label();
        welcomeTitleLabel = new Label();
        welcomeUserLabel = new Label();
        welcomeSubtitleLabel = new Label();
        welcomeIllustrationPanel = new Panel();
        illusMachineBody = new Panel();
        illusMachineScreen = new Panel();
        illusMachineLed1 = new Panel();
        illusMachineLed2 = new Panel();
        illusShield = new Panel();
        illusShieldCheck = new Label();
        illusBox1 = new Panel();
        illusBox2 = new Panel();
        illusBox3 = new Panel();
        illusMidBox1 = new Panel();
        illusMidBox2 = new Panel();
        illusMidBox3 = new Panel();
        illusFloorLine = new Panel();
        illusDot1 = new Panel();
        illusDot2 = new Panel();
        illusDot3 = new Panel();
        illusDot4 = new Panel();
        illusDot5 = new Panel();
        illusDot6 = new Panel();
        metricsLayout = new TableLayoutPanel();
        metricCard1 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric1IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric1IconLabel = new Label();
        metric1CaptionLabel = new Label();
        metric1ValueLabel = new Label();
        metric1UnitLabel = new Label();
        metricCard2 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric2IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric2IconLabel = new Label();
        metric2CaptionLabel = new Label();
        metric2ValueLabel = new Label();
        metric2UnitLabel = new Label();
        metricCard3 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric3IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric3IconLabel = new Label();
        metric3CaptionLabel = new Label();
        metric3ValueLabel = new Label();
        metric3UnitLabel = new Label();
        metricCard4 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric4IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric4IconLabel = new Label();
        metric4CaptionLabel = new Label();
        metric4ValueLabel = new Label();
        metric4UnitLabel = new Label();
        metricCard5 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric5IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        metric5IconLabel = new Label();
        metric5CaptionLabel = new Label();
        metric5ValueLabel = new Label();
        metric5UnitLabel = new Label();
        modulesActivitiesLayout = new TableLayoutPanel();
        modulesSectionPanel = new Panel();
        modulesSectionTitleLabel = new Label();
        modulesGridLayout = new TableLayoutPanel();
        moduleCard1 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module1IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module1IconLabel = new Label();
        module1TitleLabel = new Label();
        module1DescLabel = new Label();
        module1ArrowLabel = new Label();
        moduleCard2 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module2IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module2IconLabel = new Label();
        module2TitleLabel = new Label();
        module2DescLabel = new Label();
        module2ArrowLabel = new Label();
        moduleCard3 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module3IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module3IconLabel = new Label();
        module3TitleLabel = new Label();
        module3DescLabel = new Label();
        module3ArrowLabel = new Label();
        moduleCard4 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module4IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module4IconLabel = new Label();
        module4TitleLabel = new Label();
        module4DescLabel = new Label();
        module4ArrowLabel = new Label();
        moduleCard5 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module5IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        module5IconLabel = new Label();
        module5TitleLabel = new Label();
        module5DescLabel = new Label();
        module5ArrowLabel = new Label();
        activitiesPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        activitiesTitleLabel = new Label();
        activitiesViewAllLabel = new Label();
        activity1Panel = new Panel();
        activity1IconLabel = new Label();
        activity1TitleLabel = new Label();
        activity1SubtitleLabel = new Label();
        activity1TimeLabel = new Label();
        activity2Panel = new Panel();
        activity2IconLabel = new Label();
        activity2TitleLabel = new Label();
        activity2SubtitleLabel = new Label();
        activity2TimeLabel = new Label();
        activity3Panel = new Panel();
        activity3IconLabel = new Label();
        activity3TitleLabel = new Label();
        activity3SubtitleLabel = new Label();
        activity3TimeLabel = new Label();
        activity4Panel = new Panel();
        activity4IconLabel = new Label();
        activity4TitleLabel = new Label();
        activity4SubtitleLabel = new Label();
        activity4TimeLabel = new Label();
        activity5Panel = new Panel();
        activity5IconLabel = new Label();
        activity5TitleLabel = new Label();
        activity5SubtitleLabel = new Label();
        activity5TimeLabel = new Label();
        activitiesHistoryLabel = new Label();
        resumoPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        resumoTitleLabel = new Label();
        resumoGridLayout = new TableLayoutPanel();
        resumoCard1 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        resumo1IconLabel = new Label();
        resumo1TitleLabel = new Label();
        resumo1StatusLabel = new Label();
        resumo1ProgressBg = new Panel();
        resumo1ProgressFill = new Panel();
        resumoCard2 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        resumo2IconLabel = new Label();
        resumo2TitleLabel = new Label();
        resumo2StatusLabel = new Label();
        resumo2ProgressBg = new Panel();
        resumo2ProgressFill = new Panel();
        resumoCard3 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        resumo3IconLabel = new Label();
        resumo3TitleLabel = new Label();
        resumo3StatusLabel = new Label();
        resumo3ProgressBg = new Panel();
        resumo3ProgressFill = new Panel();
        resumoCard4 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        resumo4IconLabel = new Label();
        resumo4TitleLabel = new Label();
        resumo4StatusLabel = new Label();
        resumo4ProgressBg = new Panel();
        resumo4ProgressFill = new Panel();
        resumoCard5 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        resumo5IconLabel = new Label();
        resumo5TitleLabel = new Label();
        resumo5StatusLabel = new Label();
        resumo5ProgressBg = new Panel();
        resumo5ProgressFill = new Panel();
        acoesPanel = new Panel();
        acoesTitleLabel = new Label();
        acoesGridLayout = new TableLayoutPanel();
        acaoCard1 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        acao1IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        acao1IconLabel = new Label();
        acao1TitleLabel = new Label();
        acao1DescLabel = new Label();
        acao1ArrowLabel = new Label();
        acaoCard2 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        acao2IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        acao2IconLabel = new Label();
        acao2TitleLabel = new Label();
        acao2DescLabel = new Label();
        acao2ArrowLabel = new Label();
        acaoCard3 = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        acao3IconBg = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        acao3IconLabel = new Label();
        acao3TitleLabel = new Label();
        acao3DescLabel = new Label();
        acao3ArrowLabel = new Label();
        modulesPanel = new FugaPET_Dev.Tela.Controls.RoundedPanel();
        modulesTitleLabel = new Label();
        cellUser = new Panel();
        cellUserText = new Label();
        cellUserIcon = new Label();
        cellUserDivider = new Panel();
        rootLayout.SuspendLayout();
        bodyLayout.SuspendLayout();
        sidebarPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)sidebarLogoPictureBox).BeginInit();
        menuItemInicio.SuspendLayout();
        menuItemCadastro.SuspendLayout();
        menuItemLeitura.SuspendLayout();
        menuItemEtiquetas.SuspendLayout();
        menuItemHistorico.SuspendLayout();
        menuItemRelatorios.SuspendLayout();
        menuItemSap.SuspendLayout();
        menuItemConfig.SuspendLayout();
        menuItemSeguranca.SuspendLayout();
        sidebarUserPanel.SuspendLayout();
        rightAreaLayout.SuspendLayout();
        headerBar.SuspendLayout();
        sapStatusPanel.SuspendLayout();
        contentScrollPanel.SuspendLayout();
        contentLayout.SuspendLayout();
        contentBrandHeaderPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)contentBrandPictureBox).BeginInit();
        footerBar.SuspendLayout();
        footerBarLayout.SuspendLayout();
        cellTerminal.SuspendLayout();
        cellEmpresa.SuspendLayout();
        cellBanco.SuspendLayout();
        cellHora.SuspendLayout();
        cellData.SuspendLayout();
        menuItemConsulta.SuspendLayout();
        welcomeBanner.SuspendLayout();
        welcomeIconBg.SuspendLayout();
        welcomeIllustrationPanel.SuspendLayout();
        metricsLayout.SuspendLayout();
        metricCard1.SuspendLayout();
        metric1IconBg.SuspendLayout();
        metricCard2.SuspendLayout();
        metric2IconBg.SuspendLayout();
        metricCard3.SuspendLayout();
        metric3IconBg.SuspendLayout();
        metricCard4.SuspendLayout();
        metric4IconBg.SuspendLayout();
        metricCard5.SuspendLayout();
        metric5IconBg.SuspendLayout();
        modulesActivitiesLayout.SuspendLayout();
        modulesSectionPanel.SuspendLayout();
        modulesGridLayout.SuspendLayout();
        moduleCard1.SuspendLayout();
        module1IconBg.SuspendLayout();
        moduleCard2.SuspendLayout();
        module2IconBg.SuspendLayout();
        moduleCard3.SuspendLayout();
        module3IconBg.SuspendLayout();
        moduleCard4.SuspendLayout();
        module4IconBg.SuspendLayout();
        moduleCard5.SuspendLayout();
        module5IconBg.SuspendLayout();
        activitiesPanel.SuspendLayout();
        activity1Panel.SuspendLayout();
        activity2Panel.SuspendLayout();
        activity3Panel.SuspendLayout();
        activity4Panel.SuspendLayout();
        activity5Panel.SuspendLayout();
        resumoPanel.SuspendLayout();
        resumoGridLayout.SuspendLayout();
        resumoCard1.SuspendLayout();
        resumo1ProgressBg.SuspendLayout();
        resumoCard2.SuspendLayout();
        resumo2ProgressBg.SuspendLayout();
        resumoCard3.SuspendLayout();
        resumo3ProgressBg.SuspendLayout();
        resumoCard4.SuspendLayout();
        resumo4ProgressBg.SuspendLayout();
        resumoCard5.SuspendLayout();
        resumo5ProgressBg.SuspendLayout();
        acoesPanel.SuspendLayout();
        acoesGridLayout.SuspendLayout();
        acaoCard1.SuspendLayout();
        acao1IconBg.SuspendLayout();
        acaoCard2.SuspendLayout();
        acao2IconBg.SuspendLayout();
        acaoCard3.SuspendLayout();
        acao3IconBg.SuspendLayout();
        cellUser.SuspendLayout();
        SuspendLayout();
        // 
        // rootLayout
        // 
        rootLayout.BackColor = Color.FromArgb(247, 248, 250);
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(bodyLayout, 0, 0);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Location = new Point(0, 0);
        rootLayout.Name = "rootLayout";
        rootLayout.RowCount = 1;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Size = new Size(1366, 720);
        rootLayout.TabIndex = 0;
        // 
        // bodyLayout
        // 
        bodyLayout.BackColor = Color.FromArgb(247, 248, 250);
        bodyLayout.ColumnCount = 2;
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
        bodyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bodyLayout.Controls.Add(sidebarPanel, 0, 0);
        bodyLayout.Controls.Add(rightAreaLayout, 1, 0);
        bodyLayout.Dock = DockStyle.Fill;
        bodyLayout.Location = new Point(0, 0);
        bodyLayout.Margin = new Padding(0);
        bodyLayout.Name = "bodyLayout";
        bodyLayout.RowCount = 1;
        bodyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        bodyLayout.Size = new Size(1366, 720);
        bodyLayout.TabIndex = 0;
        // 
        // sidebarPanel
        // 
        sidebarPanel.BackColor = Color.FromArgb(9, 22, 36);
        sidebarPanel.Controls.Add(sidebarLogoPictureBox);
        sidebarPanel.Controls.Add(menuItemInicio);
        sidebarPanel.Controls.Add(menuItemCadastro);
        sidebarPanel.Controls.Add(menuItemLeitura);
        sidebarPanel.Controls.Add(menuItemEtiquetas);
        sidebarPanel.Controls.Add(menuItemHistorico);
        sidebarPanel.Controls.Add(menuItemRelatorios);
        sidebarPanel.Controls.Add(menuItemSap);
        sidebarPanel.Controls.Add(menuItemConfig);
        sidebarPanel.Controls.Add(menuItemSeguranca);
        sidebarPanel.Controls.Add(sidebarUserPanel);
        sidebarPanel.Dock = DockStyle.Fill;
        sidebarPanel.Location = new Point(0, 0);
        sidebarPanel.Margin = new Padding(0);
        sidebarPanel.Name = "sidebarPanel";
        sidebarPanel.Size = new Size(180, 720);
        sidebarPanel.TabIndex = 0;
        // 
        // sidebarLogoPictureBox
        // 
        sidebarLogoPictureBox.BackColor = Color.Transparent;
        sidebarLogoPictureBox.Image = (Image)resources.GetObject("sidebarLogoPictureBox.Image");
        sidebarLogoPictureBox.Location = new Point(27, 18);
        sidebarLogoPictureBox.Name = "sidebarLogoPictureBox";
        sidebarLogoPictureBox.Size = new Size(122, 48);
        sidebarLogoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        sidebarLogoPictureBox.TabIndex = 0;
        sidebarLogoPictureBox.TabStop = false;
        // 
        // menuItemInicio
        // 
        menuItemInicio.BackColor = Color.FromArgb(229, 27, 43);
        menuItemInicio.Controls.Add(menuInicioIcon);
        menuItemInicio.Controls.Add(menuInicioText);
        menuItemInicio.Cursor = Cursors.Hand;
        menuItemInicio.Location = new Point(9, 82);
        menuItemInicio.Name = "menuItemInicio";
        menuItemInicio.Size = new Size(162, 40);
        menuItemInicio.TabIndex = 1;
        // 
        // menuInicioIcon
        // 
        menuInicioIcon.BackColor = Color.Transparent;
        menuInicioIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuInicioIcon.ForeColor = Color.White;
        menuInicioIcon.Location = new Point(12, 0);
        menuInicioIcon.Name = "menuInicioIcon";
        menuInicioIcon.Size = new Size(28, 40);
        menuInicioIcon.TabIndex = 0;
        menuInicioIcon.Text = "";
        menuInicioIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuInicioText
        // 
        menuInicioText.BackColor = Color.Transparent;
        menuInicioText.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        menuInicioText.ForeColor = Color.White;
        menuInicioText.Location = new Point(48, 0);
        menuInicioText.Name = "menuInicioText";
        menuInicioText.Size = new Size(108, 40);
        menuInicioText.TabIndex = 1;
        menuInicioText.Text = "Início";
        menuInicioText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemCadastro
        // 
        menuItemCadastro.BackColor = Color.Transparent;
        menuItemCadastro.Controls.Add(menuCadastroIcon);
        menuItemCadastro.Controls.Add(menuCadastroText);
        menuItemCadastro.Cursor = Cursors.Hand;
        menuItemCadastro.Location = new Point(9, 130);
        menuItemCadastro.Name = "menuItemCadastro";
        menuItemCadastro.Size = new Size(162, 40);
        menuItemCadastro.TabIndex = 2;
        // 
        // menuCadastroIcon
        // 
        menuCadastroIcon.BackColor = Color.Transparent;
        menuCadastroIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuCadastroIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuCadastroIcon.Location = new Point(12, 0);
        menuCadastroIcon.Name = "menuCadastroIcon";
        menuCadastroIcon.Size = new Size(28, 40);
        menuCadastroIcon.TabIndex = 0;
        menuCadastroIcon.Text = "";
        menuCadastroIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuCadastroText
        // 
        menuCadastroText.BackColor = Color.Transparent;
        menuCadastroText.Font = new Font("Segoe UI", 8.5F);
        menuCadastroText.ForeColor = Color.FromArgb(189, 196, 209);
        menuCadastroText.Location = new Point(48, 0);
        menuCadastroText.Name = "menuCadastroText";
        menuCadastroText.Size = new Size(108, 40);
        menuCadastroText.TabIndex = 1;
        menuCadastroText.Text = "Cadastro";
        menuCadastroText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemLeitura
        // 
        menuItemLeitura.BackColor = Color.Transparent;
        menuItemLeitura.Controls.Add(menuLeituraIcon);
        menuItemLeitura.Controls.Add(menuLeituraText);
        menuItemLeitura.Cursor = Cursors.Hand;
        menuItemLeitura.Location = new Point(9, 178);
        menuItemLeitura.Name = "menuItemLeitura";
        menuItemLeitura.Size = new Size(162, 40);
        menuItemLeitura.TabIndex = 3;
        // 
        // menuLeituraIcon
        // 
        menuLeituraIcon.BackColor = Color.Transparent;
        menuLeituraIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuLeituraIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuLeituraIcon.Location = new Point(12, 0);
        menuLeituraIcon.Name = "menuLeituraIcon";
        menuLeituraIcon.Size = new Size(28, 40);
        menuLeituraIcon.TabIndex = 0;
        menuLeituraIcon.Text = "";
        menuLeituraIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuLeituraText
        // 
        menuLeituraText.BackColor = Color.Transparent;
        menuLeituraText.Font = new Font("Segoe UI", 8.5F);
        menuLeituraText.ForeColor = Color.FromArgb(189, 196, 209);
        menuLeituraText.Location = new Point(44, 0);
        menuLeituraText.Name = "menuLeituraText";
        menuLeituraText.Size = new Size(112, 40);
        menuLeituraText.TabIndex = 1;
        menuLeituraText.Text = "Processo de \r\nProdução";
        menuLeituraText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemEtiquetas
        // 
        menuItemEtiquetas.BackColor = Color.Transparent;
        menuItemEtiquetas.Controls.Add(menuEtiquetasIcon);
        menuItemEtiquetas.Controls.Add(menuEtiquetasText);
        menuItemEtiquetas.Cursor = Cursors.Hand;
        menuItemEtiquetas.Location = new Point(9, 226);
        menuItemEtiquetas.Name = "menuItemEtiquetas";
        menuItemEtiquetas.Size = new Size(162, 40);
        menuItemEtiquetas.TabIndex = 6;
        // 
        // menuEtiquetasIcon
        // 
        menuEtiquetasIcon.BackColor = Color.Transparent;
        menuEtiquetasIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuEtiquetasIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuEtiquetasIcon.Image = (Image)resources.GetObject("menuEtiquetasIcon.Image");
        menuEtiquetasIcon.Location = new Point(12, 0);
        menuEtiquetasIcon.Name = "menuEtiquetasIcon";
        menuEtiquetasIcon.Size = new Size(28, 40);
        menuEtiquetasIcon.TabIndex = 0;
        menuEtiquetasIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuEtiquetasText
        // 
        menuEtiquetasText.BackColor = Color.Transparent;
        menuEtiquetasText.Font = new Font("Segoe UI", 8.5F);
        menuEtiquetasText.ForeColor = Color.FromArgb(189, 196, 209);
        menuEtiquetasText.Location = new Point(48, 0);
        menuEtiquetasText.Name = "menuEtiquetasText";
        menuEtiquetasText.Size = new Size(108, 40);
        menuEtiquetasText.TabIndex = 1;
        menuEtiquetasText.Text = "Etiquetas";
        menuEtiquetasText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemHistorico
        // 
        menuItemHistorico.BackColor = Color.Transparent;
        menuItemHistorico.Controls.Add(menuHistoricoIcon);
        menuItemHistorico.Controls.Add(menuHistoricoText);
        menuItemHistorico.Cursor = Cursors.Hand;
        menuItemHistorico.Location = new Point(9, 274);
        menuItemHistorico.Name = "menuItemHistorico";
        menuItemHistorico.Size = new Size(162, 40);
        menuItemHistorico.TabIndex = 7;
        // 
        // menuHistoricoIcon
        // 
        menuHistoricoIcon.BackColor = Color.Transparent;
        menuHistoricoIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuHistoricoIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuHistoricoIcon.Location = new Point(12, 0);
        menuHistoricoIcon.Name = "menuHistoricoIcon";
        menuHistoricoIcon.Size = new Size(28, 40);
        menuHistoricoIcon.TabIndex = 0;
        menuHistoricoIcon.Text = "";
        menuHistoricoIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuHistoricoText
        // 
        menuHistoricoText.BackColor = Color.Transparent;
        menuHistoricoText.Font = new Font("Segoe UI", 8.5F);
        menuHistoricoText.ForeColor = Color.FromArgb(189, 196, 209);
        menuHistoricoText.Location = new Point(48, 0);
        menuHistoricoText.Name = "menuHistoricoText";
        menuHistoricoText.Size = new Size(108, 40);
        menuHistoricoText.TabIndex = 1;
        menuHistoricoText.Text = "Histórico";
        menuHistoricoText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemRelatorios
        // 
        menuItemRelatorios.BackColor = Color.Transparent;
        menuItemRelatorios.Controls.Add(menuRelatoriosIcon);
        menuItemRelatorios.Controls.Add(menuRelatoriosText);
        menuItemRelatorios.Cursor = Cursors.Hand;
        menuItemRelatorios.Location = new Point(9, 322);
        menuItemRelatorios.Name = "menuItemRelatorios";
        menuItemRelatorios.Size = new Size(162, 40);
        menuItemRelatorios.TabIndex = 8;
        // 
        // menuRelatoriosIcon
        // 
        menuRelatoriosIcon.BackColor = Color.Transparent;
        menuRelatoriosIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuRelatoriosIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuRelatoriosIcon.Location = new Point(12, 0);
        menuRelatoriosIcon.Name = "menuRelatoriosIcon";
        menuRelatoriosIcon.Size = new Size(28, 40);
        menuRelatoriosIcon.TabIndex = 0;
        menuRelatoriosIcon.Text = "";
        menuRelatoriosIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuRelatoriosText
        // 
        menuRelatoriosText.BackColor = Color.Transparent;
        menuRelatoriosText.Font = new Font("Segoe UI", 8.5F);
        menuRelatoriosText.ForeColor = Color.FromArgb(189, 196, 209);
        menuRelatoriosText.Location = new Point(48, 0);
        menuRelatoriosText.Name = "menuRelatoriosText";
        menuRelatoriosText.Size = new Size(108, 40);
        menuRelatoriosText.TabIndex = 1;
        menuRelatoriosText.Text = "Relatórios";
        menuRelatoriosText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemSap
        // 
        menuItemSap.BackColor = Color.Transparent;
        menuItemSap.Controls.Add(menuSapIcon);
        menuItemSap.Controls.Add(menuSapText);
        menuItemSap.Cursor = Cursors.Hand;
        menuItemSap.Location = new Point(9, 370);
        menuItemSap.Name = "menuItemSap";
        menuItemSap.Size = new Size(162, 40);
        menuItemSap.TabIndex = 9;
        // 
        // menuSapIcon
        // 
        menuSapIcon.BackColor = Color.Transparent;
        menuSapIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuSapIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuSapIcon.Location = new Point(12, 0);
        menuSapIcon.Name = "menuSapIcon";
        menuSapIcon.Size = new Size(28, 40);
        menuSapIcon.TabIndex = 0;
        menuSapIcon.Text = "";
        menuSapIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuSapText
        // 
        menuSapText.BackColor = Color.Transparent;
        menuSapText.Font = new Font("Segoe UI", 8.5F);
        menuSapText.ForeColor = Color.FromArgb(189, 196, 209);
        menuSapText.Location = new Point(48, 0);
        menuSapText.Name = "menuSapText";
        menuSapText.Size = new Size(108, 40);
        menuSapText.TabIndex = 1;
        menuSapText.Text = "Integração SAP";
        menuSapText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemConfig
        // 
        menuItemConfig.BackColor = Color.Transparent;
        menuItemConfig.Controls.Add(menuConfigIcon);
        menuItemConfig.Controls.Add(menuConfigText);
        menuItemConfig.Cursor = Cursors.Hand;
        menuItemConfig.Location = new Point(9, 418);
        menuItemConfig.Name = "menuItemConfig";
        menuItemConfig.Size = new Size(162, 40);
        menuItemConfig.TabIndex = 10;
        // 
        // menuConfigIcon
        // 
        menuConfigIcon.BackColor = Color.Transparent;
        menuConfigIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuConfigIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuConfigIcon.Location = new Point(12, 0);
        menuConfigIcon.Name = "menuConfigIcon";
        menuConfigIcon.Size = new Size(28, 40);
        menuConfigIcon.TabIndex = 0;
        menuConfigIcon.Text = "";
        menuConfigIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuConfigText
        // 
        menuConfigText.BackColor = Color.Transparent;
        menuConfigText.Font = new Font("Segoe UI", 8.5F);
        menuConfigText.ForeColor = Color.FromArgb(189, 196, 209);
        menuConfigText.Location = new Point(48, 0);
        menuConfigText.Name = "menuConfigText";
        menuConfigText.Size = new Size(108, 40);
        menuConfigText.TabIndex = 1;
        menuConfigText.Text = "Configurações";
        menuConfigText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // menuItemSeguranca
        // 
        menuItemSeguranca.BackColor = Color.Transparent;
        menuItemSeguranca.Controls.Add(menuSegurancaIcon);
        menuItemSeguranca.Controls.Add(menuSegurancaText);
        menuItemSeguranca.Cursor = Cursors.Hand;
        menuItemSeguranca.Location = new Point(9, 466);
        menuItemSeguranca.Name = "menuItemSeguranca";
        menuItemSeguranca.Size = new Size(162, 40);
        menuItemSeguranca.TabIndex = 11;
        // 
        // menuSegurancaIcon
        // 
        menuSegurancaIcon.BackColor = Color.Transparent;
        menuSegurancaIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuSegurancaIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuSegurancaIcon.Image = global::FugaPET_Dev.Properties.Resources.seguranca_24x_white;
        menuSegurancaIcon.ImageAlign = ContentAlignment.MiddleCenter;
        menuSegurancaIcon.Location = new Point(12, 0);
        menuSegurancaIcon.Name = "menuSegurancaIcon";
        menuSegurancaIcon.Size = new Size(28, 40);
        menuSegurancaIcon.TabIndex = 0;
        menuSegurancaIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuSegurancaText
        // 
        menuSegurancaText.BackColor = Color.Transparent;
        menuSegurancaText.Font = new Font("Segoe UI", 8.5F);
        menuSegurancaText.ForeColor = Color.FromArgb(189, 196, 209);
        menuSegurancaText.Location = new Point(48, 0);
        menuSegurancaText.Name = "menuSegurancaText";
        menuSegurancaText.Size = new Size(108, 40);
        menuSegurancaText.TabIndex = 1;
        menuSegurancaText.Text = "Segurança";
        menuSegurancaText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // sidebarUserPanel
        // 
        sidebarUserPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        sidebarUserPanel.BackColor = Color.Transparent;
        sidebarUserPanel.Controls.Add(sidebarUserAvatarLabel);
        sidebarUserPanel.Controls.Add(sidebarUserNameLabel);
        sidebarUserPanel.Controls.Add(sidebarUserStatusLabel);
        sidebarUserPanel.Controls.Add(sidebarUserChevronLabel);
        sidebarUserPanel.Location = new Point(0, 656);
        sidebarUserPanel.Name = "sidebarUserPanel";
        sidebarUserPanel.Size = new Size(180, 64);
        sidebarUserPanel.TabIndex = 11;
        // 
        // sidebarUserAvatarLabel
        // 
        sidebarUserAvatarLabel.BackColor = Color.FromArgb(229, 231, 235);
        sidebarUserAvatarLabel.Font = new Font("Segoe MDL2 Assets", 14F);
        sidebarUserAvatarLabel.ForeColor = Color.FromArgb(17, 24, 39);
        sidebarUserAvatarLabel.Location = new Point(18, 15);
        sidebarUserAvatarLabel.Name = "sidebarUserAvatarLabel";
        sidebarUserAvatarLabel.Size = new Size(32, 32);
        sidebarUserAvatarLabel.TabIndex = 0;
        sidebarUserAvatarLabel.Text = "";
        sidebarUserAvatarLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // sidebarUserNameLabel
        // 
        sidebarUserNameLabel.BackColor = Color.Transparent;
        sidebarUserNameLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        sidebarUserNameLabel.ForeColor = Color.White;
        sidebarUserNameLabel.Location = new Point(58, 13);
        sidebarUserNameLabel.Name = "sidebarUserNameLabel";
        sidebarUserNameLabel.Size = new Size(110, 18);
        sidebarUserNameLabel.TabIndex = 1;
        sidebarUserNameLabel.Text = "OPERADOR01";
        sidebarUserNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // sidebarUserStatusLabel
        // 
        sidebarUserStatusLabel.BackColor = Color.Transparent;
        sidebarUserStatusLabel.Font = new Font("Segoe UI", 7.5F);
        sidebarUserStatusLabel.ForeColor = Color.FromArgb(34, 197, 94);
        sidebarUserStatusLabel.Location = new Point(58, 31);
        sidebarUserStatusLabel.Name = "sidebarUserStatusLabel";
        sidebarUserStatusLabel.Size = new Size(110, 16);
        sidebarUserStatusLabel.TabIndex = 2;
        sidebarUserStatusLabel.Text = "● Online";
        sidebarUserStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // sidebarUserChevronLabel
        // 
        sidebarUserChevronLabel.BackColor = Color.Transparent;
        sidebarUserChevronLabel.Font = new Font("Segoe MDL2 Assets", 8F);
        sidebarUserChevronLabel.ForeColor = Color.White;
        sidebarUserChevronLabel.Location = new Point(154, 24);
        sidebarUserChevronLabel.Name = "sidebarUserChevronLabel";
        sidebarUserChevronLabel.Size = new Size(16, 16);
        sidebarUserChevronLabel.TabIndex = 3;
        sidebarUserChevronLabel.Text = "";
        sidebarUserChevronLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // rightAreaLayout
        // 
        rightAreaLayout.BackColor = Color.FromArgb(247, 248, 250);
        rightAreaLayout.ColumnCount = 1;
        rightAreaLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rightAreaLayout.Controls.Add(headerBar, 0, 0);
        rightAreaLayout.Controls.Add(contentScrollPanel, 0, 1);
        rightAreaLayout.Controls.Add(footerBar, 0, 2);
        rightAreaLayout.Dock = DockStyle.Fill;
        rightAreaLayout.Location = new Point(180, 0);
        rightAreaLayout.Margin = new Padding(0);
        rightAreaLayout.Name = "rightAreaLayout";
        rightAreaLayout.RowCount = 3;
        rightAreaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        rightAreaLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rightAreaLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        rightAreaLayout.Size = new Size(1186, 720);
        rightAreaLayout.TabIndex = 1;
        // 
        // headerBar
        // 
        headerBar.BackColor = Color.FromArgb(17, 24, 39);
        headerBar.Controls.Add(headerSubtitleLabel);
        headerBar.Controls.Add(sapStatusPanel);
        headerBar.Controls.Add(minimizeWindowLabel);
        headerBar.Controls.Add(maximizeWindowLabel);
        headerBar.Controls.Add(closeWindowLabel);
        headerBar.Controls.Add(headerTitleLabel);
        headerBar.Dock = DockStyle.Fill;
        headerBar.Location = new Point(0, 0);
        headerBar.Margin = new Padding(0);
        headerBar.Name = "headerBar";
        headerBar.Size = new Size(1186, 60);
        headerBar.TabIndex = 0;
        // 
        // headerSubtitleLabel
        // 
        headerSubtitleLabel.BackColor = Color.Transparent;
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228);
        headerSubtitleLabel.Location = new Point(63, 29);
        headerSubtitleLabel.Name = "headerSubtitleLabel";
        headerSubtitleLabel.Size = new Size(380, 18);
        headerSubtitleLabel.TabIndex = 3;
        headerSubtitleLabel.Text = "Visão geral da operação / Integração SAP";
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
        sapStatusPanel.Location = new Point(685, 12);
        sapStatusPanel.Name = "sapStatusPanel";
        sapStatusPanel.ShadowBlur = 0;
        sapStatusPanel.ShadowOffsetY = 0;
        sapStatusPanel.Size = new Size(190, 27);
        sapStatusPanel.TabIndex = 4;
        // 
        // sapStatusDotLabel
        // 
        sapStatusDotLabel.BackColor = Color.Transparent;
        sapStatusDotLabel.Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusDotLabel.ForeColor = Color.FromArgb(34, 197, 94);
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
        sapStatusLabel.Text = "Verificando banco";
        sapStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // minimizeWindowLabel
        // 
        minimizeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        minimizeWindowLabel.BackColor = Color.Transparent;
        minimizeWindowLabel.Cursor = Cursors.Hand;
        minimizeWindowLabel.Font = new Font("Cascadia Code", 12.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        minimizeWindowLabel.ForeColor = Color.White;
        minimizeWindowLabel.Location = new Point(1038, 0);
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
        maximizeWindowLabel.Location = new Point(1086, 0);
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
        closeWindowLabel.Location = new Point(1134, 0);
        closeWindowLabel.Name = "closeWindowLabel";
        closeWindowLabel.Size = new Size(48, 52);
        closeWindowLabel.TabIndex = 9;
        closeWindowLabel.Text = "×";
        closeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // headerTitleLabel
        // 
        headerTitleLabel.BackColor = Color.Transparent;
        headerTitleLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(62, 7);
        headerTitleLabel.Name = "headerTitleLabel";
        headerTitleLabel.Size = new Size(190, 23);
        headerTitleLabel.TabIndex = 1;
        headerTitleLabel.Text = "Painel Inicial";
        headerTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // contentScrollPanel
        // 
        contentScrollPanel.AutoScroll = true;
        contentScrollPanel.BackColor = Color.FromArgb(247, 248, 250);
        contentScrollPanel.Controls.Add(contentLayout);
        contentScrollPanel.Dock = DockStyle.Fill;
        contentScrollPanel.Location = new Point(0, 60);
        contentScrollPanel.Margin = new Padding(0);
        contentScrollPanel.Name = "contentScrollPanel";
        contentScrollPanel.Size = new Size(1186, 622);
        contentScrollPanel.TabIndex = 1;
        // 
        // contentLayout
        // 
        contentLayout.BackColor = Color.Transparent;
        contentLayout.ColumnCount = 1;
        contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        contentLayout.Controls.Add(contentBrandHeaderPanel, 0, 0);
        contentLayout.Dock = DockStyle.Fill;
        contentLayout.Location = new Point(0, 0);
        contentLayout.Margin = new Padding(0);
        contentLayout.Name = "contentLayout";
        contentLayout.Padding = new Padding(20, 16, 20, 16);
        contentLayout.RowCount = 2;
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
        contentLayout.Size = new Size(1186, 622);
        contentLayout.TabIndex = 0;
        // 
        // contentBrandHeaderPanel
        // 
        contentBrandHeaderPanel.Anchor = AnchorStyles.None;
        contentBrandHeaderPanel.BackColor = Color.Transparent;
        contentBrandHeaderPanel.BorderColor = Color.FromArgb(17, 24, 39);
        contentBrandHeaderPanel.BorderRadius = 10;
        contentBrandHeaderPanel.Controls.Add(logoSaLabel);
        contentBrandHeaderPanel.Controls.Add(contentBrandPictureBox);
        contentBrandHeaderPanel.FillColor = Color.FromArgb(17, 24, 39);
        contentBrandHeaderPanel.Location = new Point(399, 245);
        contentBrandHeaderPanel.Name = "contentBrandHeaderPanel";
        contentBrandHeaderPanel.ShadowBlur = 0;
        contentBrandHeaderPanel.ShadowOffsetY = 0;
        contentBrandHeaderPanel.Size = new Size(388, 112);
        contentBrandHeaderPanel.TabIndex = 0;
        // 
        // logoSaLabel
        // 
        logoSaLabel.BackColor = Color.Transparent;
        logoSaLabel.Font = new Font("Cascadia Code", 6.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        logoSaLabel.ForeColor = Color.White;
        logoSaLabel.Location = new Point(318, 19);
        logoSaLabel.Name = "logoSaLabel";
        logoSaLabel.Size = new Size(38, 20);
        logoSaLabel.TabIndex = 2;
        logoSaLabel.Text = "S/A";
        logoSaLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // contentBrandPictureBox
        // 
        contentBrandPictureBox.BackColor = Color.Transparent;
        contentBrandPictureBox.Image = (Image)resources.GetObject("contentBrandPictureBox.Image");
        contentBrandPictureBox.Location = new Point(0, 0);
        contentBrandPictureBox.Name = "contentBrandPictureBox";
        contentBrandPictureBox.Padding = new Padding(62, 18, 62, 18);
        contentBrandPictureBox.Size = new Size(388, 112);
        contentBrandPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        contentBrandPictureBox.TabIndex = 0;
        contentBrandPictureBox.TabStop = false;
        // 
        // footerBar
        // 
        footerBar.BackColor = Color.FromArgb(248, 250, 253);
        footerBar.Controls.Add(footerBarLayout);
        footerBar.Dock = DockStyle.Fill;
        footerBar.Location = new Point(0, 682);
        footerBar.Margin = new Padding(0);
        footerBar.Name = "footerBar";
        footerBar.Size = new Size(1186, 38);
        footerBar.TabIndex = 2;
        // 
        // footerBarLayout
        // 
        footerBarLayout.BackColor = Color.Transparent;
        footerBarLayout.ColumnCount = 5;
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12F));
        footerBarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        footerBarLayout.Controls.Add(cellTerminal, 0, 0);
        footerBarLayout.Controls.Add(cellEmpresa, 1, 0);
        footerBarLayout.Controls.Add(cellBanco, 2, 0);
        footerBarLayout.Controls.Add(cellHora, 3, 0);
        footerBarLayout.Controls.Add(cellData, 4, 0);
        footerBarLayout.Dock = DockStyle.Fill;
        footerBarLayout.Location = new Point(0, 0);
        footerBarLayout.Margin = new Padding(0);
        footerBarLayout.Name = "footerBarLayout";
        footerBarLayout.RowCount = 1;
        footerBarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footerBarLayout.Size = new Size(1186, 38);
        footerBarLayout.TabIndex = 0;
        // 
        // cellTerminal
        // 
        cellTerminal.BackColor = Color.Transparent;
        cellTerminal.Controls.Add(cellTerminalText);
        cellTerminal.Controls.Add(cellTerminalIcon);
        cellTerminal.Controls.Add(cellTerminalDivider);
        cellTerminal.Dock = DockStyle.Fill;
        cellTerminal.Location = new Point(0, 0);
        cellTerminal.Margin = new Padding(0);
        cellTerminal.Name = "cellTerminal";
        cellTerminal.Size = new Size(213, 38);
        cellTerminal.TabIndex = 0;
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
        cellTerminalText.Size = new Size(184, 38);
        cellTerminalText.TabIndex = 0;
        cellTerminalText.Text = "Terminal: ";
        cellTerminalText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellTerminalIcon
        // 
        cellTerminalIcon.Dock = DockStyle.Left;
        cellTerminalIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellTerminalIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellTerminalIcon.Location = new Point(0, 0);
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
        cellTerminalDivider.Location = new Point(212, 0);
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
        cellEmpresa.Location = new Point(213, 0);
        cellEmpresa.Margin = new Padding(0);
        cellEmpresa.Name = "cellEmpresa";
        cellEmpresa.Size = new Size(332, 38);
        cellEmpresa.TabIndex = 1;
        // 
        // cellEmpresaText
        // 
        cellEmpresaText.Dock = DockStyle.Fill;
        cellEmpresaText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellEmpresaText.ForeColor = Color.FromArgb(98, 108, 124);
        cellEmpresaText.Location = new Point(28, 0);
        cellEmpresaText.Name = "cellEmpresaText";
        cellEmpresaText.Padding = new Padding(2, 0, 0, 0);
        cellEmpresaText.Size = new Size(303, 38);
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
        cellEmpresaDivider.Location = new Point(331, 0);
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
        cellBanco.Location = new Point(545, 0);
        cellBanco.Margin = new Padding(0);
        cellBanco.Name = "cellBanco";
        cellBanco.Size = new Size(308, 38);
        cellBanco.TabIndex = 2;
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
        cellBancoText.Size = new Size(279, 38);
        cellBancoText.TabIndex = 0;
        cellBancoText.Text = "Banco de Dados:  verificando...";
        cellBancoText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // cellBancoIcon
        // 
        cellBancoIcon.Dock = DockStyle.Left;
        cellBancoIcon.Font = new Font("Segoe MDL2 Assets", 10F);
        cellBancoIcon.ForeColor = Color.FromArgb(212, 37, 49);
        cellBancoIcon.Location = new Point(0, 0);
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
        cellBancoDivider.Location = new Point(307, 0);
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
        cellHora.Location = new Point(853, 0);
        cellHora.Margin = new Padding(0);
        cellHora.Name = "cellHora";
        cellHora.Size = new Size(142, 38);
        cellHora.TabIndex = 3;
        // 
        // cellHoraText
        // 
        cellHoraText.Dock = DockStyle.Fill;
        cellHoraText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellHoraText.ForeColor = Color.FromArgb(98, 108, 124);
        cellHoraText.Location = new Point(28, 0);
        cellHoraText.Name = "cellHoraText";
        cellHoraText.Padding = new Padding(2, 0, 0, 0);
        cellHoraText.Size = new Size(113, 38);
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
        cellHoraIcon.Name = "cellHoraIcon";
        cellHoraIcon.Size = new Size(28, 38);
        cellHoraIcon.TabIndex = 1;
        cellHoraIcon.Text = "";
        cellHoraIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellHoraDivider
        // 
        cellHoraDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellHoraDivider.Dock = DockStyle.Right;
        cellHoraDivider.Location = new Point(141, 0);
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
        cellData.Controls.Add(cellDataDivider);
        cellData.Dock = DockStyle.Fill;
        cellData.Location = new Point(995, 0);
        cellData.Margin = new Padding(0);
        cellData.Name = "cellData";
        cellData.Size = new Size(191, 38);
        cellData.TabIndex = 4;
        // 
        // cellDataText
        // 
        cellDataText.Dock = DockStyle.Fill;
        cellDataText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellDataText.ForeColor = Color.FromArgb(98, 108, 124);
        cellDataText.Location = new Point(28, 0);
        cellDataText.Name = "cellDataText";
        cellDataText.Padding = new Padding(2, 0, 0, 0);
        cellDataText.Size = new Size(162, 38);
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
        cellDataIcon.Name = "cellDataIcon";
        cellDataIcon.Size = new Size(28, 38);
        cellDataIcon.TabIndex = 1;
        cellDataIcon.Text = "";
        cellDataIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellDataDivider
        // 
        cellDataDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellDataDivider.Dock = DockStyle.Right;
        cellDataDivider.Location = new Point(190, 0);
        cellDataDivider.Margin = new Padding(0);
        cellDataDivider.Name = "cellDataDivider";
        cellDataDivider.Size = new Size(1, 38);
        cellDataDivider.TabIndex = 2;
        cellDataDivider.Visible = false;
        // 
        // menuItemConsulta
        // 
        menuItemConsulta.BackColor = Color.Transparent;
        menuItemConsulta.Controls.Add(menuConsultaIcon);
        menuItemConsulta.Controls.Add(menuConsultaText);
        menuItemConsulta.Cursor = Cursors.Hand;
        menuItemConsulta.Location = new Point(9, 274);
        menuItemConsulta.Name = "menuItemConsulta";
        menuItemConsulta.Size = new Size(162, 40);
        menuItemConsulta.TabIndex = 5;
        // 
        // menuConsultaIcon
        // 
        menuConsultaIcon.BackColor = Color.Transparent;
        menuConsultaIcon.Font = new Font("Segoe MDL2 Assets", 13F);
        menuConsultaIcon.ForeColor = Color.FromArgb(189, 196, 209);
        menuConsultaIcon.Location = new Point(12, 0);
        menuConsultaIcon.Name = "menuConsultaIcon";
        menuConsultaIcon.Size = new Size(28, 40);
        menuConsultaIcon.TabIndex = 0;
        menuConsultaIcon.Text = "";
        menuConsultaIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // menuConsultaText
        // 
        menuConsultaText.BackColor = Color.Transparent;
        menuConsultaText.Font = new Font("Segoe UI", 8.5F);
        menuConsultaText.ForeColor = Color.FromArgb(189, 196, 209);
        menuConsultaText.Location = new Point(48, 0);
        menuConsultaText.Name = "menuConsultaText";
        menuConsultaText.Size = new Size(108, 40);
        menuConsultaText.TabIndex = 1;
        menuConsultaText.Text = "Consulta de OP";
        menuConsultaText.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // welcomeBanner
        // 
        welcomeBanner.BackColor = Color.Transparent;
        welcomeBanner.BorderRadius = 10;
        welcomeBanner.Controls.Add(welcomeIconBg);
        welcomeBanner.Controls.Add(welcomeTitleLabel);
        welcomeBanner.Controls.Add(welcomeUserLabel);
        welcomeBanner.Controls.Add(welcomeSubtitleLabel);
        welcomeBanner.Controls.Add(welcomeIllustrationPanel);
        welcomeBanner.Dock = DockStyle.Fill;
        welcomeBanner.Location = new Point(20, 16);
        welcomeBanner.Margin = new Padding(0, 0, 0, 12);
        welcomeBanner.Name = "welcomeBanner";
        welcomeBanner.ShadowBlur = 0;
        welcomeBanner.ShadowOffsetY = 0;
        welcomeBanner.Size = new Size(1129, 118);
        welcomeBanner.TabIndex = 0;
        // 
        // welcomeIconBg
        // 
        welcomeIconBg.BackColor = Color.Transparent;
        welcomeIconBg.BorderColor = Color.FromArgb(254, 226, 230);
        welcomeIconBg.BorderRadius = 12;
        welcomeIconBg.Controls.Add(welcomeIconLabel);
        welcomeIconBg.FillColor = Color.FromArgb(254, 226, 230);
        welcomeIconBg.Location = new Point(20, 22);
        welcomeIconBg.Name = "welcomeIconBg";
        welcomeIconBg.ShadowBlur = 0;
        welcomeIconBg.ShadowOffsetY = 0;
        welcomeIconBg.Size = new Size(72, 72);
        welcomeIconBg.TabIndex = 0;
        // 
        // welcomeIconLabel
        // 
        welcomeIconLabel.BackColor = Color.Transparent;
        welcomeIconLabel.Dock = DockStyle.Fill;
        welcomeIconLabel.Font = new Font("Segoe UI", 28F, FontStyle.Bold);
        welcomeIconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        welcomeIconLabel.Location = new Point(0, 0);
        welcomeIconLabel.Name = "welcomeIconLabel";
        welcomeIconLabel.Size = new Size(72, 72);
        welcomeIconLabel.TabIndex = 0;
        welcomeIconLabel.Text = "F";
        welcomeIconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // welcomeTitleLabel
        // 
        welcomeTitleLabel.BackColor = Color.Transparent;
        welcomeTitleLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        welcomeTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        welcomeTitleLabel.Location = new Point(110, 28);
        welcomeTitleLabel.Name = "welcomeTitleLabel";
        welcomeTitleLabel.Size = new Size(130, 30);
        welcomeTitleLabel.TabIndex = 1;
        welcomeTitleLabel.Text = "Bem-vindo,";
        welcomeTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // welcomeUserLabel
        // 
        welcomeUserLabel.BackColor = Color.Transparent;
        welcomeUserLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        welcomeUserLabel.ForeColor = Color.FromArgb(212, 37, 49);
        welcomeUserLabel.Location = new Point(238, 28);
        welcomeUserLabel.Name = "welcomeUserLabel";
        welcomeUserLabel.Size = new Size(280, 30);
        welcomeUserLabel.TabIndex = 2;
        welcomeUserLabel.Text = "OPERADOR01";
        welcomeUserLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // welcomeSubtitleLabel
        // 
        welcomeSubtitleLabel.BackColor = Color.Transparent;
        welcomeSubtitleLabel.Font = new Font("Segoe UI", 9F);
        welcomeSubtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        welcomeSubtitleLabel.Location = new Point(110, 60);
        welcomeSubtitleLabel.Name = "welcomeSubtitleLabel";
        welcomeSubtitleLabel.Size = new Size(540, 40);
        welcomeSubtitleLabel.TabIndex = 3;
        welcomeSubtitleLabel.Text = "Acompanhe a produção, consulte ordens e acesse rapidamente os módulos do sistema.";
        // 
        // welcomeIllustrationPanel
        // 
        welcomeIllustrationPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
        welcomeIllustrationPanel.BackColor = Color.Transparent;
        welcomeIllustrationPanel.Controls.Add(illusMachineBody);
        welcomeIllustrationPanel.Controls.Add(illusMachineScreen);
        welcomeIllustrationPanel.Controls.Add(illusMachineLed1);
        welcomeIllustrationPanel.Controls.Add(illusMachineLed2);
        welcomeIllustrationPanel.Controls.Add(illusShield);
        welcomeIllustrationPanel.Controls.Add(illusShieldCheck);
        welcomeIllustrationPanel.Controls.Add(illusBox1);
        welcomeIllustrationPanel.Controls.Add(illusBox2);
        welcomeIllustrationPanel.Controls.Add(illusBox3);
        welcomeIllustrationPanel.Controls.Add(illusMidBox1);
        welcomeIllustrationPanel.Controls.Add(illusMidBox2);
        welcomeIllustrationPanel.Controls.Add(illusMidBox3);
        welcomeIllustrationPanel.Controls.Add(illusFloorLine);
        welcomeIllustrationPanel.Controls.Add(illusDot1);
        welcomeIllustrationPanel.Controls.Add(illusDot2);
        welcomeIllustrationPanel.Controls.Add(illusDot3);
        welcomeIllustrationPanel.Controls.Add(illusDot4);
        welcomeIllustrationPanel.Controls.Add(illusDot5);
        welcomeIllustrationPanel.Controls.Add(illusDot6);
        welcomeIllustrationPanel.Location = new Point(1609, 0);
        welcomeIllustrationPanel.Margin = new Padding(0);
        welcomeIllustrationPanel.Name = "welcomeIllustrationPanel";
        welcomeIllustrationPanel.Size = new Size(450, 148);
        welcomeIllustrationPanel.TabIndex = 4;
        // 
        // illusMachineBody
        // 
        illusMachineBody.BackColor = Color.FromArgb(31, 41, 55);
        illusMachineBody.Location = new Point(230, 27);
        illusMachineBody.Name = "illusMachineBody";
        illusMachineBody.Size = new Size(90, 76);
        illusMachineBody.TabIndex = 0;
        // 
        // illusMachineScreen
        // 
        illusMachineScreen.BackColor = Color.FromArgb(55, 65, 81);
        illusMachineScreen.Location = new Point(240, 37);
        illusMachineScreen.Name = "illusMachineScreen";
        illusMachineScreen.Size = new Size(70, 28);
        illusMachineScreen.TabIndex = 1;
        // 
        // illusMachineLed1
        // 
        illusMachineLed1.BackColor = Color.FromArgb(212, 37, 49);
        illusMachineLed1.Location = new Point(250, 79);
        illusMachineLed1.Name = "illusMachineLed1";
        illusMachineLed1.Size = new Size(8, 8);
        illusMachineLed1.TabIndex = 2;
        // 
        // illusMachineLed2
        // 
        illusMachineLed2.BackColor = Color.FromArgb(34, 166, 82);
        illusMachineLed2.Location = new Point(264, 79);
        illusMachineLed2.Name = "illusMachineLed2";
        illusMachineLed2.Size = new Size(8, 8);
        illusMachineLed2.TabIndex = 3;
        // 
        // illusShield
        // 
        illusShield.BackColor = Color.FromArgb(212, 37, 49);
        illusShield.Location = new Point(330, 35);
        illusShield.Name = "illusShield";
        illusShield.Size = new Size(36, 60);
        illusShield.TabIndex = 4;
        // 
        // illusShieldCheck
        // 
        illusShieldCheck.BackColor = Color.Transparent;
        illusShieldCheck.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
        illusShieldCheck.ForeColor = Color.White;
        illusShieldCheck.Location = new Point(330, 50);
        illusShieldCheck.Name = "illusShieldCheck";
        illusShieldCheck.Size = new Size(36, 30);
        illusShieldCheck.TabIndex = 5;
        illusShieldCheck.Text = "?";
        illusShieldCheck.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // illusBox1
        // 
        illusBox1.BackColor = Color.FromArgb(212, 37, 49);
        illusBox1.Location = new Point(40, 69);
        illusBox1.Name = "illusBox1";
        illusBox1.Size = new Size(28, 28);
        illusBox1.TabIndex = 6;
        // 
        // illusBox2
        // 
        illusBox2.BackColor = Color.FromArgb(229, 87, 95);
        illusBox2.Location = new Point(72, 69);
        illusBox2.Name = "illusBox2";
        illusBox2.Size = new Size(28, 28);
        illusBox2.TabIndex = 7;
        // 
        // illusBox3
        // 
        illusBox3.BackColor = Color.FromArgb(212, 37, 49);
        illusBox3.Location = new Point(56, 39);
        illusBox3.Name = "illusBox3";
        illusBox3.Size = new Size(28, 28);
        illusBox3.TabIndex = 8;
        // 
        // illusMidBox1
        // 
        illusMidBox1.BackColor = Color.FromArgb(229, 87, 95);
        illusMidBox1.Location = new Point(150, 59);
        illusMidBox1.Name = "illusMidBox1";
        illusMidBox1.Size = new Size(22, 22);
        illusMidBox1.TabIndex = 9;
        // 
        // illusMidBox2
        // 
        illusMidBox2.BackColor = Color.FromArgb(212, 37, 49);
        illusMidBox2.Location = new Point(176, 59);
        illusMidBox2.Name = "illusMidBox2";
        illusMidBox2.Size = new Size(22, 22);
        illusMidBox2.TabIndex = 10;
        // 
        // illusMidBox3
        // 
        illusMidBox3.BackColor = Color.FromArgb(212, 37, 49);
        illusMidBox3.Location = new Point(163, 33);
        illusMidBox3.Name = "illusMidBox3";
        illusMidBox3.Size = new Size(22, 22);
        illusMidBox3.TabIndex = 11;
        // 
        // illusFloorLine
        // 
        illusFloorLine.BackColor = Color.FromArgb(229, 232, 238);
        illusFloorLine.Location = new Point(30, 112);
        illusFloorLine.Name = "illusFloorLine";
        illusFloorLine.Size = new Size(390, 2);
        illusFloorLine.TabIndex = 12;
        // 
        // illusDot1
        // 
        illusDot1.BackColor = Color.FromArgb(248, 213, 217);
        illusDot1.Location = new Point(370, 14);
        illusDot1.Name = "illusDot1";
        illusDot1.Size = new Size(5, 5);
        illusDot1.TabIndex = 13;
        // 
        // illusDot2
        // 
        illusDot2.BackColor = Color.FromArgb(248, 213, 217);
        illusDot2.Location = new Point(378, 14);
        illusDot2.Name = "illusDot2";
        illusDot2.Size = new Size(5, 5);
        illusDot2.TabIndex = 14;
        // 
        // illusDot3
        // 
        illusDot3.BackColor = Color.FromArgb(248, 213, 217);
        illusDot3.Location = new Point(386, 14);
        illusDot3.Name = "illusDot3";
        illusDot3.Size = new Size(5, 5);
        illusDot3.TabIndex = 15;
        // 
        // illusDot4
        // 
        illusDot4.BackColor = Color.FromArgb(248, 213, 217);
        illusDot4.Location = new Point(370, 22);
        illusDot4.Name = "illusDot4";
        illusDot4.Size = new Size(5, 5);
        illusDot4.TabIndex = 16;
        // 
        // illusDot5
        // 
        illusDot5.BackColor = Color.FromArgb(248, 213, 217);
        illusDot5.Location = new Point(378, 22);
        illusDot5.Name = "illusDot5";
        illusDot5.Size = new Size(5, 5);
        illusDot5.TabIndex = 17;
        // 
        // illusDot6
        // 
        illusDot6.BackColor = Color.FromArgb(248, 213, 217);
        illusDot6.Location = new Point(386, 22);
        illusDot6.Name = "illusDot6";
        illusDot6.Size = new Size(5, 5);
        illusDot6.TabIndex = 18;
        // 
        // metricsLayout
        // 
        metricsLayout.BackColor = Color.Transparent;
        metricsLayout.ColumnCount = 5;
        metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        metricsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        metricsLayout.Controls.Add(metricCard1, 0, 0);
        metricsLayout.Controls.Add(metricCard2, 1, 0);
        metricsLayout.Controls.Add(metricCard3, 2, 0);
        metricsLayout.Controls.Add(metricCard4, 3, 0);
        metricsLayout.Controls.Add(metricCard5, 4, 0);
        metricsLayout.Dock = DockStyle.Fill;
        metricsLayout.Location = new Point(20, 146);
        metricsLayout.Margin = new Padding(0, 0, 0, 12);
        metricsLayout.Name = "metricsLayout";
        metricsLayout.RowCount = 1;
        metricsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        metricsLayout.Size = new Size(1129, 98);
        metricsLayout.TabIndex = 1;
        // 
        // metricCard1
        // 
        metricCard1.BackColor = Color.Transparent;
        metricCard1.BorderRadius = 10;
        metricCard1.Controls.Add(metric1IconBg);
        metricCard1.Controls.Add(metric1CaptionLabel);
        metricCard1.Controls.Add(metric1ValueLabel);
        metricCard1.Controls.Add(metric1UnitLabel);
        metricCard1.Dock = DockStyle.Fill;
        metricCard1.Location = new Point(0, 0);
        metricCard1.Margin = new Padding(0, 0, 6, 0);
        metricCard1.Name = "metricCard1";
        metricCard1.ShadowBlur = 0;
        metricCard1.ShadowOffsetY = 0;
        metricCard1.Size = new Size(219, 98);
        metricCard1.TabIndex = 0;
        // 
        // metric1IconBg
        // 
        metric1IconBg.BackColor = Color.Transparent;
        metric1IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        metric1IconBg.Controls.Add(metric1IconLabel);
        metric1IconBg.FillColor = Color.FromArgb(254, 226, 230);
        metric1IconBg.Location = new Point(14, 24);
        metric1IconBg.Name = "metric1IconBg";
        metric1IconBg.ShadowBlur = 0;
        metric1IconBg.ShadowOffsetY = 0;
        metric1IconBg.Size = new Size(46, 46);
        metric1IconBg.TabIndex = 0;
        // 
        // metric1IconLabel
        // 
        metric1IconLabel.BackColor = Color.Transparent;
        metric1IconLabel.Dock = DockStyle.Fill;
        metric1IconLabel.Font = new Font("Segoe Fluent Icons", 14F);
        metric1IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric1IconLabel.Location = new Point(0, 0);
        metric1IconLabel.Name = "metric1IconLabel";
        metric1IconLabel.Size = new Size(46, 46);
        metric1IconLabel.TabIndex = 0;
        metric1IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // metric1CaptionLabel
        // 
        metric1CaptionLabel.BackColor = Color.Transparent;
        metric1CaptionLabel.Font = new Font("Segoe UI", 8.5F);
        metric1CaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric1CaptionLabel.Location = new Point(72, 18);
        metric1CaptionLabel.Name = "metric1CaptionLabel";
        metric1CaptionLabel.Size = new Size(150, 18);
        metric1CaptionLabel.TabIndex = 1;
        metric1CaptionLabel.Text = "Ordens em andamento";
        metric1CaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric1ValueLabel
        // 
        metric1ValueLabel.BackColor = Color.Transparent;
        metric1ValueLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        metric1ValueLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric1ValueLabel.Location = new Point(72, 38);
        metric1ValueLabel.Name = "metric1ValueLabel";
        metric1ValueLabel.Size = new Size(80, 30);
        metric1ValueLabel.TabIndex = 2;
        metric1ValueLabel.Text = "10";
        metric1ValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric1UnitLabel
        // 
        metric1UnitLabel.BackColor = Color.Transparent;
        metric1UnitLabel.Font = new Font("Segoe UI", 8.5F);
        metric1UnitLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric1UnitLabel.Location = new Point(146, 46);
        metric1UnitLabel.Name = "metric1UnitLabel";
        metric1UnitLabel.Size = new Size(80, 18);
        metric1UnitLabel.TabIndex = 3;
        metric1UnitLabel.Text = "ordens";
        metric1UnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metricCard2
        // 
        metricCard2.BackColor = Color.Transparent;
        metricCard2.BorderRadius = 10;
        metricCard2.Controls.Add(metric2IconBg);
        metricCard2.Controls.Add(metric2CaptionLabel);
        metricCard2.Controls.Add(metric2ValueLabel);
        metricCard2.Controls.Add(metric2UnitLabel);
        metricCard2.Dock = DockStyle.Fill;
        metricCard2.Location = new Point(231, 0);
        metricCard2.Margin = new Padding(6, 0, 6, 0);
        metricCard2.Name = "metricCard2";
        metricCard2.ShadowBlur = 0;
        metricCard2.ShadowOffsetY = 0;
        metricCard2.Size = new Size(213, 98);
        metricCard2.TabIndex = 1;
        // 
        // metric2IconBg
        // 
        metric2IconBg.BackColor = Color.Transparent;
        metric2IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        metric2IconBg.Controls.Add(metric2IconLabel);
        metric2IconBg.FillColor = Color.FromArgb(254, 226, 230);
        metric2IconBg.Location = new Point(14, 24);
        metric2IconBg.Name = "metric2IconBg";
        metric2IconBg.ShadowBlur = 0;
        metric2IconBg.ShadowOffsetY = 0;
        metric2IconBg.Size = new Size(46, 46);
        metric2IconBg.TabIndex = 0;
        // 
        // metric2IconLabel
        // 
        metric2IconLabel.BackColor = Color.Transparent;
        metric2IconLabel.Dock = DockStyle.Fill;
        metric2IconLabel.Font = new Font("Segoe Fluent Icons", 14F);
        metric2IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric2IconLabel.Location = new Point(0, 0);
        metric2IconLabel.Name = "metric2IconLabel";
        metric2IconLabel.Size = new Size(46, 46);
        metric2IconLabel.TabIndex = 0;
        metric2IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // metric2CaptionLabel
        // 
        metric2CaptionLabel.BackColor = Color.Transparent;
        metric2CaptionLabel.Font = new Font("Segoe UI", 8.5F);
        metric2CaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric2CaptionLabel.Location = new Point(72, 18);
        metric2CaptionLabel.Name = "metric2CaptionLabel";
        metric2CaptionLabel.Size = new Size(150, 18);
        metric2CaptionLabel.TabIndex = 1;
        metric2CaptionLabel.Text = "Caixas previstas";
        metric2CaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric2ValueLabel
        // 
        metric2ValueLabel.BackColor = Color.Transparent;
        metric2ValueLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        metric2ValueLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric2ValueLabel.Location = new Point(72, 38);
        metric2ValueLabel.Name = "metric2ValueLabel";
        metric2ValueLabel.Size = new Size(80, 30);
        metric2ValueLabel.TabIndex = 2;
        metric2ValueLabel.Text = "5,568";
        metric2ValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric2UnitLabel
        // 
        metric2UnitLabel.BackColor = Color.Transparent;
        metric2UnitLabel.Font = new Font("Segoe UI", 8.5F);
        metric2UnitLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric2UnitLabel.Location = new Point(146, 46);
        metric2UnitLabel.Name = "metric2UnitLabel";
        metric2UnitLabel.Size = new Size(80, 18);
        metric2UnitLabel.TabIndex = 3;
        metric2UnitLabel.Text = "caixas";
        metric2UnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metricCard3
        // 
        metricCard3.BackColor = Color.Transparent;
        metricCard3.BorderRadius = 10;
        metricCard3.Controls.Add(metric3IconBg);
        metricCard3.Controls.Add(metric3CaptionLabel);
        metricCard3.Controls.Add(metric3ValueLabel);
        metricCard3.Controls.Add(metric3UnitLabel);
        metricCard3.Dock = DockStyle.Fill;
        metricCard3.Location = new Point(456, 0);
        metricCard3.Margin = new Padding(6, 0, 6, 0);
        metricCard3.Name = "metricCard3";
        metricCard3.ShadowBlur = 0;
        metricCard3.ShadowOffsetY = 0;
        metricCard3.Size = new Size(213, 98);
        metricCard3.TabIndex = 2;
        // 
        // metric3IconBg
        // 
        metric3IconBg.BackColor = Color.Transparent;
        metric3IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        metric3IconBg.Controls.Add(metric3IconLabel);
        metric3IconBg.FillColor = Color.FromArgb(254, 226, 230);
        metric3IconBg.Location = new Point(14, 24);
        metric3IconBg.Name = "metric3IconBg";
        metric3IconBg.ShadowBlur = 0;
        metric3IconBg.ShadowOffsetY = 0;
        metric3IconBg.Size = new Size(46, 46);
        metric3IconBg.TabIndex = 0;
        // 
        // metric3IconLabel
        // 
        metric3IconLabel.BackColor = Color.Transparent;
        metric3IconLabel.Dock = DockStyle.Fill;
        metric3IconLabel.Font = new Font("Segoe Fluent Icons", 14F);
        metric3IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric3IconLabel.Location = new Point(0, 0);
        metric3IconLabel.Name = "metric3IconLabel";
        metric3IconLabel.Size = new Size(46, 46);
        metric3IconLabel.TabIndex = 0;
        metric3IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // metric3CaptionLabel
        // 
        metric3CaptionLabel.BackColor = Color.Transparent;
        metric3CaptionLabel.Font = new Font("Segoe UI", 8.5F);
        metric3CaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric3CaptionLabel.Location = new Point(72, 18);
        metric3CaptionLabel.Name = "metric3CaptionLabel";
        metric3CaptionLabel.Size = new Size(150, 18);
        metric3CaptionLabel.TabIndex = 1;
        metric3CaptionLabel.Text = "Caixas lidas hoje";
        metric3CaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric3ValueLabel
        // 
        metric3ValueLabel.BackColor = Color.Transparent;
        metric3ValueLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        metric3ValueLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric3ValueLabel.Location = new Point(72, 38);
        metric3ValueLabel.Name = "metric3ValueLabel";
        metric3ValueLabel.Size = new Size(80, 30);
        metric3ValueLabel.TabIndex = 2;
        metric3ValueLabel.Text = "3,268";
        metric3ValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric3UnitLabel
        // 
        metric3UnitLabel.BackColor = Color.Transparent;
        metric3UnitLabel.Font = new Font("Segoe UI", 8.5F);
        metric3UnitLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric3UnitLabel.Location = new Point(146, 46);
        metric3UnitLabel.Name = "metric3UnitLabel";
        metric3UnitLabel.Size = new Size(80, 18);
        metric3UnitLabel.TabIndex = 3;
        metric3UnitLabel.Text = "caixas";
        metric3UnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metricCard4
        // 
        metricCard4.BackColor = Color.Transparent;
        metricCard4.BorderRadius = 10;
        metricCard4.Controls.Add(metric4IconBg);
        metricCard4.Controls.Add(metric4CaptionLabel);
        metricCard4.Controls.Add(metric4ValueLabel);
        metricCard4.Controls.Add(metric4UnitLabel);
        metricCard4.Dock = DockStyle.Fill;
        metricCard4.Location = new Point(681, 0);
        metricCard4.Margin = new Padding(6, 0, 6, 0);
        metricCard4.Name = "metricCard4";
        metricCard4.ShadowBlur = 0;
        metricCard4.ShadowOffsetY = 0;
        metricCard4.Size = new Size(213, 98);
        metricCard4.TabIndex = 3;
        // 
        // metric4IconBg
        // 
        metric4IconBg.BackColor = Color.Transparent;
        metric4IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        metric4IconBg.Controls.Add(metric4IconLabel);
        metric4IconBg.FillColor = Color.FromArgb(254, 226, 230);
        metric4IconBg.Location = new Point(14, 24);
        metric4IconBg.Name = "metric4IconBg";
        metric4IconBg.ShadowBlur = 0;
        metric4IconBg.ShadowOffsetY = 0;
        metric4IconBg.Size = new Size(46, 46);
        metric4IconBg.TabIndex = 0;
        // 
        // metric4IconLabel
        // 
        metric4IconLabel.BackColor = Color.Transparent;
        metric4IconLabel.Dock = DockStyle.Fill;
        metric4IconLabel.Font = new Font("Segoe Fluent Icons", 14F);
        metric4IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric4IconLabel.Location = new Point(0, 0);
        metric4IconLabel.Name = "metric4IconLabel";
        metric4IconLabel.Size = new Size(46, 46);
        metric4IconLabel.TabIndex = 0;
        metric4IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // metric4CaptionLabel
        // 
        metric4CaptionLabel.BackColor = Color.Transparent;
        metric4CaptionLabel.Font = new Font("Segoe UI", 8.5F);
        metric4CaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric4CaptionLabel.Location = new Point(72, 18);
        metric4CaptionLabel.Name = "metric4CaptionLabel";
        metric4CaptionLabel.Size = new Size(150, 18);
        metric4CaptionLabel.TabIndex = 1;
        metric4CaptionLabel.Text = "Pacotes lidos";
        metric4CaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric4ValueLabel
        // 
        metric4ValueLabel.BackColor = Color.Transparent;
        metric4ValueLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        metric4ValueLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric4ValueLabel.Location = new Point(72, 38);
        metric4ValueLabel.Name = "metric4ValueLabel";
        metric4ValueLabel.Size = new Size(80, 30);
        metric4ValueLabel.TabIndex = 2;
        metric4ValueLabel.Text = "840";
        metric4ValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric4UnitLabel
        // 
        metric4UnitLabel.BackColor = Color.Transparent;
        metric4UnitLabel.Font = new Font("Segoe UI", 8.5F);
        metric4UnitLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric4UnitLabel.Location = new Point(146, 46);
        metric4UnitLabel.Name = "metric4UnitLabel";
        metric4UnitLabel.Size = new Size(80, 18);
        metric4UnitLabel.TabIndex = 3;
        metric4UnitLabel.Text = "pacotes";
        metric4UnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metricCard5
        // 
        metricCard5.BackColor = Color.Transparent;
        metricCard5.BorderRadius = 10;
        metricCard5.Controls.Add(metric5IconBg);
        metricCard5.Controls.Add(metric5CaptionLabel);
        metricCard5.Controls.Add(metric5ValueLabel);
        metricCard5.Controls.Add(metric5UnitLabel);
        metricCard5.Dock = DockStyle.Fill;
        metricCard5.Location = new Point(906, 0);
        metricCard5.Margin = new Padding(6, 0, 0, 0);
        metricCard5.Name = "metricCard5";
        metricCard5.ShadowBlur = 0;
        metricCard5.ShadowOffsetY = 0;
        metricCard5.Size = new Size(223, 98);
        metricCard5.TabIndex = 4;
        // 
        // metric5IconBg
        // 
        metric5IconBg.BackColor = Color.Transparent;
        metric5IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        metric5IconBg.Controls.Add(metric5IconLabel);
        metric5IconBg.FillColor = Color.FromArgb(254, 226, 230);
        metric5IconBg.Location = new Point(14, 24);
        metric5IconBg.Name = "metric5IconBg";
        metric5IconBg.ShadowBlur = 0;
        metric5IconBg.ShadowOffsetY = 0;
        metric5IconBg.Size = new Size(46, 46);
        metric5IconBg.TabIndex = 0;
        // 
        // metric5IconLabel
        // 
        metric5IconLabel.BackColor = Color.Transparent;
        metric5IconLabel.Dock = DockStyle.Fill;
        metric5IconLabel.Font = new Font("Segoe Fluent Icons", 14F);
        metric5IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric5IconLabel.Location = new Point(0, 0);
        metric5IconLabel.Name = "metric5IconLabel";
        metric5IconLabel.Size = new Size(46, 46);
        metric5IconLabel.TabIndex = 0;
        metric5IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // metric5CaptionLabel
        // 
        metric5CaptionLabel.BackColor = Color.Transparent;
        metric5CaptionLabel.Font = new Font("Segoe UI", 8.5F);
        metric5CaptionLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric5CaptionLabel.Location = new Point(72, 18);
        metric5CaptionLabel.Name = "metric5CaptionLabel";
        metric5CaptionLabel.Size = new Size(150, 18);
        metric5CaptionLabel.TabIndex = 1;
        metric5CaptionLabel.Text = "Alertas";
        metric5CaptionLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric5ValueLabel
        // 
        metric5ValueLabel.BackColor = Color.Transparent;
        metric5ValueLabel.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        metric5ValueLabel.ForeColor = Color.FromArgb(212, 37, 49);
        metric5ValueLabel.Location = new Point(72, 38);
        metric5ValueLabel.Name = "metric5ValueLabel";
        metric5ValueLabel.Size = new Size(80, 30);
        metric5ValueLabel.TabIndex = 2;
        metric5ValueLabel.Text = "3";
        metric5ValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // metric5UnitLabel
        // 
        metric5UnitLabel.BackColor = Color.Transparent;
        metric5UnitLabel.Font = new Font("Segoe UI", 8.5F);
        metric5UnitLabel.ForeColor = Color.FromArgb(98, 108, 124);
        metric5UnitLabel.Location = new Point(146, 46);
        metric5UnitLabel.Name = "metric5UnitLabel";
        metric5UnitLabel.Size = new Size(80, 18);
        metric5UnitLabel.TabIndex = 3;
        metric5UnitLabel.Text = "pendentes";
        metric5UnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // modulesActivitiesLayout
        // 
        modulesActivitiesLayout.BackColor = Color.Transparent;
        modulesActivitiesLayout.ColumnCount = 2;
        modulesActivitiesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
        modulesActivitiesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
        modulesActivitiesLayout.Controls.Add(modulesSectionPanel, 0, 0);
        modulesActivitiesLayout.Controls.Add(activitiesPanel, 1, 0);
        modulesActivitiesLayout.Dock = DockStyle.Fill;
        modulesActivitiesLayout.Location = new Point(20, 256);
        modulesActivitiesLayout.Margin = new Padding(0, 0, 0, 12);
        modulesActivitiesLayout.Name = "modulesActivitiesLayout";
        modulesActivitiesLayout.RowCount = 1;
        modulesActivitiesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        modulesActivitiesLayout.Size = new Size(1129, 258);
        modulesActivitiesLayout.TabIndex = 2;
        // 
        // modulesSectionPanel
        // 
        modulesSectionPanel.BackColor = Color.Transparent;
        modulesSectionPanel.Controls.Add(modulesSectionTitleLabel);
        modulesSectionPanel.Controls.Add(modulesGridLayout);
        modulesSectionPanel.Dock = DockStyle.Fill;
        modulesSectionPanel.Location = new Point(0, 0);
        modulesSectionPanel.Margin = new Padding(0, 0, 12, 0);
        modulesSectionPanel.Name = "modulesSectionPanel";
        modulesSectionPanel.Size = new Size(721, 258);
        modulesSectionPanel.TabIndex = 0;
        // 
        // modulesSectionTitleLabel
        // 
        modulesSectionTitleLabel.BackColor = Color.Transparent;
        modulesSectionTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        modulesSectionTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        modulesSectionTitleLabel.Location = new Point(0, 0);
        modulesSectionTitleLabel.Name = "modulesSectionTitleLabel";
        modulesSectionTitleLabel.Size = new Size(280, 22);
        modulesSectionTitleLabel.TabIndex = 0;
        modulesSectionTitleLabel.Text = "Acesso rápido aos módulos";
        modulesSectionTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // modulesGridLayout
        // 
        modulesGridLayout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        modulesGridLayout.BackColor = Color.Transparent;
        modulesGridLayout.ColumnCount = 5;
        modulesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        modulesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        modulesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        modulesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        modulesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        modulesGridLayout.Controls.Add(moduleCard1, 0, 0);
        modulesGridLayout.Controls.Add(moduleCard2, 1, 0);
        modulesGridLayout.Controls.Add(moduleCard3, 2, 0);
        modulesGridLayout.Controls.Add(moduleCard4, 3, 0);
        modulesGridLayout.Controls.Add(moduleCard5, 4, 0);
        modulesGridLayout.Location = new Point(0, 28);
        modulesGridLayout.Name = "modulesGridLayout";
        modulesGridLayout.RowCount = 1;
        modulesGridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        modulesGridLayout.Size = new Size(1261, 382);
        modulesGridLayout.TabIndex = 1;
        // 
        // moduleCard1
        // 
        moduleCard1.BackColor = Color.Transparent;
        moduleCard1.BorderRadius = 10;
        moduleCard1.Controls.Add(module1IconBg);
        moduleCard1.Controls.Add(module1TitleLabel);
        moduleCard1.Controls.Add(module1DescLabel);
        moduleCard1.Controls.Add(module1ArrowLabel);
        moduleCard1.Cursor = Cursors.Hand;
        moduleCard1.Dock = DockStyle.Fill;
        moduleCard1.Location = new Point(0, 0);
        moduleCard1.Margin = new Padding(0, 0, 6, 0);
        moduleCard1.Name = "moduleCard1";
        moduleCard1.ShadowBlur = 0;
        moduleCard1.ShadowOffsetY = 0;
        moduleCard1.Size = new Size(246, 382);
        moduleCard1.TabIndex = 0;
        // 
        // module1IconBg
        // 
        module1IconBg.BackColor = Color.Transparent;
        module1IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        module1IconBg.Controls.Add(module1IconLabel);
        module1IconBg.FillColor = Color.FromArgb(254, 226, 230);
        module1IconBg.Location = new Point(14, 14);
        module1IconBg.Name = "module1IconBg";
        module1IconBg.ShadowBlur = 0;
        module1IconBg.ShadowOffsetY = 0;
        module1IconBg.Size = new Size(40, 40);
        module1IconBg.TabIndex = 0;
        // 
        // module1IconLabel
        // 
        module1IconLabel.BackColor = Color.Transparent;
        module1IconLabel.Dock = DockStyle.Fill;
        module1IconLabel.Font = new Font("Segoe Fluent Icons", 13F);
        module1IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module1IconLabel.Location = new Point(0, 0);
        module1IconLabel.Name = "module1IconLabel";
        module1IconLabel.Size = new Size(40, 40);
        module1IconLabel.TabIndex = 0;
        module1IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // module1TitleLabel
        // 
        module1TitleLabel.BackColor = Color.Transparent;
        module1TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        module1TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        module1TitleLabel.Location = new Point(14, 64);
        module1TitleLabel.Name = "module1TitleLabel";
        module1TitleLabel.Size = new Size(130, 22);
        module1TitleLabel.TabIndex = 1;
        module1TitleLabel.Text = "Iniciar Leitura";
        module1TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // module1DescLabel
        // 
        module1DescLabel.BackColor = Color.Transparent;
        module1DescLabel.Font = new Font("Segoe UI", 7.5F);
        module1DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        module1DescLabel.Location = new Point(14, 88);
        module1DescLabel.Name = "module1DescLabel";
        module1DescLabel.Size = new Size(130, 70);
        module1DescLabel.TabIndex = 2;
        module1DescLabel.Text = "Realize a leitura de caixas e pacotes.";
        // 
        // module1ArrowLabel
        // 
        module1ArrowLabel.BackColor = Color.Transparent;
        module1ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        module1ArrowLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module1ArrowLabel.Location = new Point(14, 168);
        module1ArrowLabel.Name = "module1ArrowLabel";
        module1ArrowLabel.Size = new Size(28, 22);
        module1ArrowLabel.TabIndex = 3;
        module1ArrowLabel.Text = "→";
        module1ArrowLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // moduleCard2
        // 
        moduleCard2.BackColor = Color.Transparent;
        moduleCard2.BorderRadius = 10;
        moduleCard2.Controls.Add(module2IconBg);
        moduleCard2.Controls.Add(module2TitleLabel);
        moduleCard2.Controls.Add(module2DescLabel);
        moduleCard2.Controls.Add(module2ArrowLabel);
        moduleCard2.Cursor = Cursors.Hand;
        moduleCard2.Dock = DockStyle.Fill;
        moduleCard2.Location = new Point(258, 0);
        moduleCard2.Margin = new Padding(6, 0, 6, 0);
        moduleCard2.Name = "moduleCard2";
        moduleCard2.ShadowBlur = 0;
        moduleCard2.ShadowOffsetY = 0;
        moduleCard2.Size = new Size(240, 382);
        moduleCard2.TabIndex = 1;
        // 
        // module2IconBg
        // 
        module2IconBg.BackColor = Color.Transparent;
        module2IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        module2IconBg.Controls.Add(module2IconLabel);
        module2IconBg.FillColor = Color.FromArgb(254, 226, 230);
        module2IconBg.Location = new Point(14, 14);
        module2IconBg.Name = "module2IconBg";
        module2IconBg.ShadowBlur = 0;
        module2IconBg.ShadowOffsetY = 0;
        module2IconBg.Size = new Size(40, 40);
        module2IconBg.TabIndex = 0;
        // 
        // module2IconLabel
        // 
        module2IconLabel.BackColor = Color.Transparent;
        module2IconLabel.Dock = DockStyle.Fill;
        module2IconLabel.Font = new Font("Segoe Fluent Icons", 13F);
        module2IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module2IconLabel.Location = new Point(0, 0);
        module2IconLabel.Name = "module2IconLabel";
        module2IconLabel.Size = new Size(40, 40);
        module2IconLabel.TabIndex = 0;
        module2IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // module2TitleLabel
        // 
        module2TitleLabel.BackColor = Color.Transparent;
        module2TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        module2TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        module2TitleLabel.Location = new Point(14, 64);
        module2TitleLabel.Name = "module2TitleLabel";
        module2TitleLabel.Size = new Size(130, 22);
        module2TitleLabel.TabIndex = 1;
        module2TitleLabel.Text = "Consultar Ordens";
        module2TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // module2DescLabel
        // 
        module2DescLabel.BackColor = Color.Transparent;
        module2DescLabel.Font = new Font("Segoe UI", 7.5F);
        module2DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        module2DescLabel.Location = new Point(14, 88);
        module2DescLabel.Name = "module2DescLabel";
        module2DescLabel.Size = new Size(130, 70);
        module2DescLabel.TabIndex = 2;
        module2DescLabel.Text = "Consulte ordens de produção (OP).";
        // 
        // module2ArrowLabel
        // 
        module2ArrowLabel.BackColor = Color.Transparent;
        module2ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        module2ArrowLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module2ArrowLabel.Location = new Point(14, 168);
        module2ArrowLabel.Name = "module2ArrowLabel";
        module2ArrowLabel.Size = new Size(28, 22);
        module2ArrowLabel.TabIndex = 3;
        module2ArrowLabel.Text = "→";
        module2ArrowLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // moduleCard3
        // 
        moduleCard3.BackColor = Color.Transparent;
        moduleCard3.BorderRadius = 10;
        moduleCard3.Controls.Add(module3IconBg);
        moduleCard3.Controls.Add(module3TitleLabel);
        moduleCard3.Controls.Add(module3DescLabel);
        moduleCard3.Controls.Add(module3ArrowLabel);
        moduleCard3.Cursor = Cursors.Hand;
        moduleCard3.Dock = DockStyle.Fill;
        moduleCard3.Location = new Point(510, 0);
        moduleCard3.Margin = new Padding(6, 0, 6, 0);
        moduleCard3.Name = "moduleCard3";
        moduleCard3.ShadowBlur = 0;
        moduleCard3.ShadowOffsetY = 0;
        moduleCard3.Size = new Size(240, 382);
        moduleCard3.TabIndex = 2;
        // 
        // module3IconBg
        // 
        module3IconBg.BackColor = Color.Transparent;
        module3IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        module3IconBg.Controls.Add(module3IconLabel);
        module3IconBg.FillColor = Color.FromArgb(254, 226, 230);
        module3IconBg.Location = new Point(14, 14);
        module3IconBg.Name = "module3IconBg";
        module3IconBg.ShadowBlur = 0;
        module3IconBg.ShadowOffsetY = 0;
        module3IconBg.Size = new Size(40, 40);
        module3IconBg.TabIndex = 0;
        // 
        // module3IconLabel
        // 
        module3IconLabel.BackColor = Color.Transparent;
        module3IconLabel.Dock = DockStyle.Fill;
        module3IconLabel.Font = new Font("Segoe Fluent Icons", 13F);
        module3IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module3IconLabel.Location = new Point(0, 0);
        module3IconLabel.Name = "module3IconLabel";
        module3IconLabel.Size = new Size(40, 40);
        module3IconLabel.TabIndex = 0;
        module3IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // module3TitleLabel
        // 
        module3TitleLabel.BackColor = Color.Transparent;
        module3TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        module3TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        module3TitleLabel.Location = new Point(14, 64);
        module3TitleLabel.Name = "module3TitleLabel";
        module3TitleLabel.Size = new Size(130, 22);
        module3TitleLabel.TabIndex = 1;
        module3TitleLabel.Text = "Ordens em Andamento";
        module3TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // module3DescLabel
        // 
        module3DescLabel.BackColor = Color.Transparent;
        module3DescLabel.Font = new Font("Segoe UI", 7.5F);
        module3DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        module3DescLabel.Location = new Point(14, 88);
        module3DescLabel.Name = "module3DescLabel";
        module3DescLabel.Size = new Size(130, 70);
        module3DescLabel.TabIndex = 2;
        module3DescLabel.Text = "Acompanhe todas as ordens ativas.";
        // 
        // module3ArrowLabel
        // 
        module3ArrowLabel.BackColor = Color.Transparent;
        module3ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        module3ArrowLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module3ArrowLabel.Location = new Point(14, 168);
        module3ArrowLabel.Name = "module3ArrowLabel";
        module3ArrowLabel.Size = new Size(28, 22);
        module3ArrowLabel.TabIndex = 3;
        module3ArrowLabel.Text = "→";
        module3ArrowLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // moduleCard4
        // 
        moduleCard4.BackColor = Color.Transparent;
        moduleCard4.BorderRadius = 10;
        moduleCard4.Controls.Add(module4IconBg);
        moduleCard4.Controls.Add(module4TitleLabel);
        moduleCard4.Controls.Add(module4DescLabel);
        moduleCard4.Controls.Add(module4ArrowLabel);
        moduleCard4.Cursor = Cursors.Hand;
        moduleCard4.Dock = DockStyle.Fill;
        moduleCard4.Location = new Point(762, 0);
        moduleCard4.Margin = new Padding(6, 0, 6, 0);
        moduleCard4.Name = "moduleCard4";
        moduleCard4.ShadowBlur = 0;
        moduleCard4.ShadowOffsetY = 0;
        moduleCard4.Size = new Size(240, 382);
        moduleCard4.TabIndex = 3;
        // 
        // module4IconBg
        // 
        module4IconBg.BackColor = Color.Transparent;
        module4IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        module4IconBg.Controls.Add(module4IconLabel);
        module4IconBg.FillColor = Color.FromArgb(254, 226, 230);
        module4IconBg.Location = new Point(14, 14);
        module4IconBg.Name = "module4IconBg";
        module4IconBg.ShadowBlur = 0;
        module4IconBg.ShadowOffsetY = 0;
        module4IconBg.Size = new Size(40, 40);
        module4IconBg.TabIndex = 0;
        // 
        // module4IconLabel
        // 
        module4IconLabel.BackColor = Color.Transparent;
        module4IconLabel.Dock = DockStyle.Fill;
        module4IconLabel.Font = new Font("Segoe Fluent Icons", 13F);
        module4IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module4IconLabel.Location = new Point(0, 0);
        module4IconLabel.Name = "module4IconLabel";
        module4IconLabel.Size = new Size(40, 40);
        module4IconLabel.TabIndex = 0;
        module4IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // module4TitleLabel
        // 
        module4TitleLabel.BackColor = Color.Transparent;
        module4TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        module4TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        module4TitleLabel.Location = new Point(14, 64);
        module4TitleLabel.Name = "module4TitleLabel";
        module4TitleLabel.Size = new Size(130, 22);
        module4TitleLabel.TabIndex = 1;
        module4TitleLabel.Text = "Histórico de Leituras";
        module4TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // module4DescLabel
        // 
        module4DescLabel.BackColor = Color.Transparent;
        module4DescLabel.Font = new Font("Segoe UI", 7.5F);
        module4DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        module4DescLabel.Location = new Point(14, 88);
        module4DescLabel.Name = "module4DescLabel";
        module4DescLabel.Size = new Size(130, 70);
        module4DescLabel.TabIndex = 2;
        module4DescLabel.Text = "Visualize o histórico de leituras realizadas.";
        // 
        // module4ArrowLabel
        // 
        module4ArrowLabel.BackColor = Color.Transparent;
        module4ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        module4ArrowLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module4ArrowLabel.Location = new Point(14, 168);
        module4ArrowLabel.Name = "module4ArrowLabel";
        module4ArrowLabel.Size = new Size(28, 22);
        module4ArrowLabel.TabIndex = 3;
        module4ArrowLabel.Text = "→";
        module4ArrowLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // moduleCard5
        // 
        moduleCard5.BackColor = Color.Transparent;
        moduleCard5.BorderRadius = 10;
        moduleCard5.Controls.Add(module5IconBg);
        moduleCard5.Controls.Add(module5TitleLabel);
        moduleCard5.Controls.Add(module5DescLabel);
        moduleCard5.Controls.Add(module5ArrowLabel);
        moduleCard5.Cursor = Cursors.Hand;
        moduleCard5.Dock = DockStyle.Fill;
        moduleCard5.Location = new Point(1014, 0);
        moduleCard5.Margin = new Padding(6, 0, 0, 0);
        moduleCard5.Name = "moduleCard5";
        moduleCard5.ShadowBlur = 0;
        moduleCard5.ShadowOffsetY = 0;
        moduleCard5.Size = new Size(247, 382);
        moduleCard5.TabIndex = 4;
        // 
        // module5IconBg
        // 
        module5IconBg.BackColor = Color.Transparent;
        module5IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        module5IconBg.Controls.Add(module5IconLabel);
        module5IconBg.FillColor = Color.FromArgb(254, 226, 230);
        module5IconBg.Location = new Point(14, 14);
        module5IconBg.Name = "module5IconBg";
        module5IconBg.ShadowBlur = 0;
        module5IconBg.ShadowOffsetY = 0;
        module5IconBg.Size = new Size(40, 40);
        module5IconBg.TabIndex = 0;
        // 
        // module5IconLabel
        // 
        module5IconLabel.BackColor = Color.Transparent;
        module5IconLabel.Dock = DockStyle.Fill;
        module5IconLabel.Font = new Font("Segoe Fluent Icons", 13F);
        module5IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module5IconLabel.Location = new Point(0, 0);
        module5IconLabel.Name = "module5IconLabel";
        module5IconLabel.Size = new Size(40, 40);
        module5IconLabel.TabIndex = 0;
        module5IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // module5TitleLabel
        // 
        module5TitleLabel.BackColor = Color.Transparent;
        module5TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        module5TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        module5TitleLabel.Location = new Point(14, 64);
        module5TitleLabel.Name = "module5TitleLabel";
        module5TitleLabel.Size = new Size(130, 22);
        module5TitleLabel.TabIndex = 1;
        module5TitleLabel.Text = "Relatórios";
        module5TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // module5DescLabel
        // 
        module5DescLabel.BackColor = Color.Transparent;
        module5DescLabel.Font = new Font("Segoe UI", 7.5F);
        module5DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        module5DescLabel.Location = new Point(14, 88);
        module5DescLabel.Name = "module5DescLabel";
        module5DescLabel.Size = new Size(130, 70);
        module5DescLabel.TabIndex = 2;
        module5DescLabel.Text = "Acesse relatórios e indicadores.";
        // 
        // module5ArrowLabel
        // 
        module5ArrowLabel.BackColor = Color.Transparent;
        module5ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        module5ArrowLabel.ForeColor = Color.FromArgb(212, 37, 49);
        module5ArrowLabel.Location = new Point(14, 168);
        module5ArrowLabel.Name = "module5ArrowLabel";
        module5ArrowLabel.Size = new Size(28, 22);
        module5ArrowLabel.TabIndex = 3;
        module5ArrowLabel.Text = "→";
        module5ArrowLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activitiesPanel
        // 
        activitiesPanel.BackColor = Color.Transparent;
        activitiesPanel.BorderRadius = 10;
        activitiesPanel.Controls.Add(activitiesTitleLabel);
        activitiesPanel.Controls.Add(activitiesViewAllLabel);
        activitiesPanel.Controls.Add(activity1Panel);
        activitiesPanel.Controls.Add(activity2Panel);
        activitiesPanel.Controls.Add(activity3Panel);
        activitiesPanel.Controls.Add(activity4Panel);
        activitiesPanel.Controls.Add(activity5Panel);
        activitiesPanel.Controls.Add(activitiesHistoryLabel);
        activitiesPanel.Dock = DockStyle.Fill;
        activitiesPanel.Location = new Point(739, 0);
        activitiesPanel.Margin = new Padding(6, 0, 0, 0);
        activitiesPanel.Name = "activitiesPanel";
        activitiesPanel.ShadowBlur = 0;
        activitiesPanel.ShadowOffsetY = 0;
        activitiesPanel.Size = new Size(390, 258);
        activitiesPanel.TabIndex = 1;
        // 
        // activitiesTitleLabel
        // 
        activitiesTitleLabel.BackColor = Color.Transparent;
        activitiesTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        activitiesTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        activitiesTitleLabel.Location = new Point(18, 14);
        activitiesTitleLabel.Name = "activitiesTitleLabel";
        activitiesTitleLabel.Size = new Size(180, 22);
        activitiesTitleLabel.TabIndex = 0;
        activitiesTitleLabel.Text = "Últimas Atividades";
        activitiesTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activitiesViewAllLabel
        // 
        activitiesViewAllLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        activitiesViewAllLabel.BackColor = Color.Transparent;
        activitiesViewAllLabel.Cursor = Cursors.Hand;
        activitiesViewAllLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        activitiesViewAllLabel.ForeColor = Color.FromArgb(212, 37, 49);
        activitiesViewAllLabel.Location = new Point(520, 16);
        activitiesViewAllLabel.Name = "activitiesViewAllLabel";
        activitiesViewAllLabel.Size = new Size(60, 18);
        activitiesViewAllLabel.TabIndex = 1;
        activitiesViewAllLabel.Text = "Ver todas";
        activitiesViewAllLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // activity1Panel
        // 
        activity1Panel.BackColor = Color.Transparent;
        activity1Panel.Controls.Add(activity1IconLabel);
        activity1Panel.Controls.Add(activity1TitleLabel);
        activity1Panel.Controls.Add(activity1SubtitleLabel);
        activity1Panel.Controls.Add(activity1TimeLabel);
        activity1Panel.Location = new Point(14, 42);
        activity1Panel.Name = "activity1Panel";
        activity1Panel.Size = new Size(380, 36);
        activity1Panel.TabIndex = 2;
        // 
        // activity1IconLabel
        // 
        activity1IconLabel.BackColor = Color.Transparent;
        activity1IconLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        activity1IconLabel.ForeColor = Color.FromArgb(34, 166, 82);
        activity1IconLabel.Location = new Point(0, 4);
        activity1IconLabel.Name = "activity1IconLabel";
        activity1IconLabel.Size = new Size(28, 28);
        activity1IconLabel.TabIndex = 0;
        activity1IconLabel.Text = "●\u008f";
        activity1IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // activity1TitleLabel
        // 
        activity1TitleLabel.BackColor = Color.Transparent;
        activity1TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        activity1TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        activity1TitleLabel.Location = new Point(34, 0);
        activity1TitleLabel.Name = "activity1TitleLabel";
        activity1TitleLabel.Size = new Size(280, 18);
        activity1TitleLabel.TabIndex = 1;
        activity1TitleLabel.Text = "Leitura concluída";
        activity1TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity1SubtitleLabel
        // 
        activity1SubtitleLabel.BackColor = Color.Transparent;
        activity1SubtitleLabel.Font = new Font("Segoe UI", 7.5F);
        activity1SubtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity1SubtitleLabel.Location = new Point(34, 18);
        activity1SubtitleLabel.Name = "activity1SubtitleLabel";
        activity1SubtitleLabel.Size = new Size(280, 16);
        activity1SubtitleLabel.TabIndex = 2;
        activity1SubtitleLabel.Text = "OP 58421 - 119 25 - 27771";
        activity1SubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity1TimeLabel
        // 
        activity1TimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        activity1TimeLabel.BackColor = Color.Transparent;
        activity1TimeLabel.Font = new Font("Segoe UI", 8F);
        activity1TimeLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity1TimeLabel.Location = new Point(515, 8);
        activity1TimeLabel.Name = "activity1TimeLabel";
        activity1TimeLabel.Size = new Size(45, 18);
        activity1TimeLabel.TabIndex = 3;
        activity1TimeLabel.Text = "13:32";
        activity1TimeLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // activity2Panel
        // 
        activity2Panel.BackColor = Color.Transparent;
        activity2Panel.Controls.Add(activity2IconLabel);
        activity2Panel.Controls.Add(activity2TitleLabel);
        activity2Panel.Controls.Add(activity2SubtitleLabel);
        activity2Panel.Controls.Add(activity2TimeLabel);
        activity2Panel.Location = new Point(14, 80);
        activity2Panel.Name = "activity2Panel";
        activity2Panel.Size = new Size(380, 36);
        activity2Panel.TabIndex = 3;
        // 
        // activity2IconLabel
        // 
        activity2IconLabel.BackColor = Color.Transparent;
        activity2IconLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        activity2IconLabel.ForeColor = Color.FromArgb(59, 130, 246);
        activity2IconLabel.Location = new Point(0, 4);
        activity2IconLabel.Name = "activity2IconLabel";
        activity2IconLabel.Size = new Size(28, 28);
        activity2IconLabel.TabIndex = 0;
        activity2IconLabel.Text = "●\u008f";
        activity2IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // activity2TitleLabel
        // 
        activity2TitleLabel.BackColor = Color.Transparent;
        activity2TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        activity2TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        activity2TitleLabel.Location = new Point(34, 0);
        activity2TitleLabel.Name = "activity2TitleLabel";
        activity2TitleLabel.Size = new Size(280, 18);
        activity2TitleLabel.TabIndex = 1;
        activity2TitleLabel.Text = "OP aberta";
        activity2TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity2SubtitleLabel
        // 
        activity2SubtitleLabel.BackColor = Color.Transparent;
        activity2SubtitleLabel.Font = new Font("Segoe UI", 7.5F);
        activity2SubtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity2SubtitleLabel.Location = new Point(34, 18);
        activity2SubtitleLabel.Name = "activity2SubtitleLabel";
        activity2SubtitleLabel.Size = new Size(280, 16);
        activity2SubtitleLabel.TabIndex = 2;
        activity2SubtitleLabel.Text = "OP 58422 - 119 26 - 27771";
        activity2SubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity2TimeLabel
        // 
        activity2TimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        activity2TimeLabel.BackColor = Color.Transparent;
        activity2TimeLabel.Font = new Font("Segoe UI", 8F);
        activity2TimeLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity2TimeLabel.Location = new Point(515, 8);
        activity2TimeLabel.Name = "activity2TimeLabel";
        activity2TimeLabel.Size = new Size(45, 18);
        activity2TimeLabel.TabIndex = 3;
        activity2TimeLabel.Text = "13:28";
        activity2TimeLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // activity3Panel
        // 
        activity3Panel.BackColor = Color.Transparent;
        activity3Panel.Controls.Add(activity3IconLabel);
        activity3Panel.Controls.Add(activity3TitleLabel);
        activity3Panel.Controls.Add(activity3SubtitleLabel);
        activity3Panel.Controls.Add(activity3TimeLabel);
        activity3Panel.Location = new Point(14, 118);
        activity3Panel.Name = "activity3Panel";
        activity3Panel.Size = new Size(380, 36);
        activity3Panel.TabIndex = 4;
        // 
        // activity3IconLabel
        // 
        activity3IconLabel.BackColor = Color.Transparent;
        activity3IconLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        activity3IconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity3IconLabel.Location = new Point(0, 4);
        activity3IconLabel.Name = "activity3IconLabel";
        activity3IconLabel.Size = new Size(28, 28);
        activity3IconLabel.TabIndex = 0;
        activity3IconLabel.Text = "●\u008f";
        activity3IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // activity3TitleLabel
        // 
        activity3TitleLabel.BackColor = Color.Transparent;
        activity3TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        activity3TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        activity3TitleLabel.Location = new Point(34, 0);
        activity3TitleLabel.Name = "activity3TitleLabel";
        activity3TitleLabel.Size = new Size(280, 18);
        activity3TitleLabel.TabIndex = 1;
        activity3TitleLabel.Text = "Integração enviada ao SAP";
        activity3TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity3SubtitleLabel
        // 
        activity3SubtitleLabel.BackColor = Color.Transparent;
        activity3SubtitleLabel.Font = new Font("Segoe UI", 7.5F);
        activity3SubtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity3SubtitleLabel.Location = new Point(34, 18);
        activity3SubtitleLabel.Name = "activity3SubtitleLabel";
        activity3SubtitleLabel.Size = new Size(280, 16);
        activity3SubtitleLabel.TabIndex = 2;
        activity3SubtitleLabel.Text = "Leituras e produção (OP 58420)";
        activity3SubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity3TimeLabel
        // 
        activity3TimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        activity3TimeLabel.BackColor = Color.Transparent;
        activity3TimeLabel.Font = new Font("Segoe UI", 8F);
        activity3TimeLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity3TimeLabel.Location = new Point(515, 8);
        activity3TimeLabel.Name = "activity3TimeLabel";
        activity3TimeLabel.Size = new Size(45, 18);
        activity3TimeLabel.TabIndex = 3;
        activity3TimeLabel.Text = "13:21";
        activity3TimeLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // activity4Panel
        // 
        activity4Panel.BackColor = Color.Transparent;
        activity4Panel.Controls.Add(activity4IconLabel);
        activity4Panel.Controls.Add(activity4TitleLabel);
        activity4Panel.Controls.Add(activity4SubtitleLabel);
        activity4Panel.Controls.Add(activity4TimeLabel);
        activity4Panel.Location = new Point(14, 156);
        activity4Panel.Name = "activity4Panel";
        activity4Panel.Size = new Size(380, 36);
        activity4Panel.TabIndex = 5;
        // 
        // activity4IconLabel
        // 
        activity4IconLabel.BackColor = Color.Transparent;
        activity4IconLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        activity4IconLabel.ForeColor = Color.FromArgb(212, 122, 28);
        activity4IconLabel.Location = new Point(0, 4);
        activity4IconLabel.Name = "activity4IconLabel";
        activity4IconLabel.Size = new Size(28, 28);
        activity4IconLabel.TabIndex = 0;
        activity4IconLabel.Text = "●\u008f";
        activity4IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // activity4TitleLabel
        // 
        activity4TitleLabel.BackColor = Color.Transparent;
        activity4TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        activity4TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        activity4TitleLabel.Location = new Point(34, 0);
        activity4TitleLabel.Name = "activity4TitleLabel";
        activity4TitleLabel.Size = new Size(280, 18);
        activity4TitleLabel.TabIndex = 1;
        activity4TitleLabel.Text = "Atenção de saldo";
        activity4TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity4SubtitleLabel
        // 
        activity4SubtitleLabel.BackColor = Color.Transparent;
        activity4SubtitleLabel.Font = new Font("Segoe UI", 7.5F);
        activity4SubtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity4SubtitleLabel.Location = new Point(34, 18);
        activity4SubtitleLabel.Name = "activity4SubtitleLabel";
        activity4SubtitleLabel.Size = new Size(280, 16);
        activity4SubtitleLabel.TabIndex = 2;
        activity4SubtitleLabel.Text = "OP 58419 - Saldo abaixo do previsto";
        activity4SubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity4TimeLabel
        // 
        activity4TimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        activity4TimeLabel.BackColor = Color.Transparent;
        activity4TimeLabel.Font = new Font("Segoe UI", 8F);
        activity4TimeLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity4TimeLabel.Location = new Point(515, 8);
        activity4TimeLabel.Name = "activity4TimeLabel";
        activity4TimeLabel.Size = new Size(45, 18);
        activity4TimeLabel.TabIndex = 3;
        activity4TimeLabel.Text = "13:15";
        activity4TimeLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // activity5Panel
        // 
        activity5Panel.BackColor = Color.Transparent;
        activity5Panel.Controls.Add(activity5IconLabel);
        activity5Panel.Controls.Add(activity5TitleLabel);
        activity5Panel.Controls.Add(activity5SubtitleLabel);
        activity5Panel.Controls.Add(activity5TimeLabel);
        activity5Panel.Location = new Point(14, 194);
        activity5Panel.Name = "activity5Panel";
        activity5Panel.Size = new Size(380, 36);
        activity5Panel.TabIndex = 6;
        // 
        // activity5IconLabel
        // 
        activity5IconLabel.BackColor = Color.Transparent;
        activity5IconLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
        activity5IconLabel.ForeColor = Color.FromArgb(34, 166, 82);
        activity5IconLabel.Location = new Point(0, 4);
        activity5IconLabel.Name = "activity5IconLabel";
        activity5IconLabel.Size = new Size(28, 28);
        activity5IconLabel.TabIndex = 0;
        activity5IconLabel.Text = "●\u008f";
        activity5IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // activity5TitleLabel
        // 
        activity5TitleLabel.BackColor = Color.Transparent;
        activity5TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        activity5TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        activity5TitleLabel.Location = new Point(34, 0);
        activity5TitleLabel.Name = "activity5TitleLabel";
        activity5TitleLabel.Size = new Size(280, 18);
        activity5TitleLabel.TabIndex = 1;
        activity5TitleLabel.Text = "Leitura concluída";
        activity5TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity5SubtitleLabel
        // 
        activity5SubtitleLabel.BackColor = Color.Transparent;
        activity5SubtitleLabel.Font = new Font("Segoe UI", 7.5F);
        activity5SubtitleLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity5SubtitleLabel.Location = new Point(34, 18);
        activity5SubtitleLabel.Name = "activity5SubtitleLabel";
        activity5SubtitleLabel.Size = new Size(280, 16);
        activity5SubtitleLabel.TabIndex = 2;
        activity5SubtitleLabel.Text = "OP 58418 - 119 22 - 27771";
        activity5SubtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // activity5TimeLabel
        // 
        activity5TimeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        activity5TimeLabel.BackColor = Color.Transparent;
        activity5TimeLabel.Font = new Font("Segoe UI", 8F);
        activity5TimeLabel.ForeColor = Color.FromArgb(98, 108, 124);
        activity5TimeLabel.Location = new Point(515, 8);
        activity5TimeLabel.Name = "activity5TimeLabel";
        activity5TimeLabel.Size = new Size(45, 18);
        activity5TimeLabel.TabIndex = 3;
        activity5TimeLabel.Text = "13:09";
        activity5TimeLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // activitiesHistoryLabel
        // 
        activitiesHistoryLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        activitiesHistoryLabel.BackColor = Color.Transparent;
        activitiesHistoryLabel.Cursor = Cursors.Hand;
        activitiesHistoryLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        activitiesHistoryLabel.ForeColor = Color.FromArgb(212, 37, 49);
        activitiesHistoryLabel.Location = new Point(410, 398);
        activitiesHistoryLabel.Name = "activitiesHistoryLabel";
        activitiesHistoryLabel.Size = new Size(170, 18);
        activitiesHistoryLabel.TabIndex = 7;
        activitiesHistoryLabel.Text = "Ver histórico completo  ->";
        activitiesHistoryLabel.TextAlign = ContentAlignment.MiddleRight;
        // 
        // resumoPanel
        // 
        resumoPanel.BackColor = Color.Transparent;
        resumoPanel.BorderRadius = 10;
        resumoPanel.Controls.Add(resumoTitleLabel);
        resumoPanel.Controls.Add(resumoGridLayout);
        resumoPanel.Dock = DockStyle.Fill;
        resumoPanel.Location = new Point(20, 526);
        resumoPanel.Margin = new Padding(0, 0, 0, 12);
        resumoPanel.Name = "resumoPanel";
        resumoPanel.ShadowBlur = 0;
        resumoPanel.ShadowOffsetY = 0;
        resumoPanel.Size = new Size(1129, 118);
        resumoPanel.TabIndex = 3;
        // 
        // resumoTitleLabel
        // 
        resumoTitleLabel.BackColor = Color.Transparent;
        resumoTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        resumoTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        resumoTitleLabel.Location = new Point(18, 12);
        resumoTitleLabel.Name = "resumoTitleLabel";
        resumoTitleLabel.Size = new Size(220, 22);
        resumoTitleLabel.TabIndex = 0;
        resumoTitleLabel.Text = "•  Resumo Operacional";
        resumoTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumoGridLayout
        // 
        resumoGridLayout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resumoGridLayout.BackColor = Color.Transparent;
        resumoGridLayout.ColumnCount = 5;
        resumoGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        resumoGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        resumoGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        resumoGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        resumoGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        resumoGridLayout.Controls.Add(resumoCard1, 0, 0);
        resumoGridLayout.Controls.Add(resumoCard2, 1, 0);
        resumoGridLayout.Controls.Add(resumoCard3, 2, 0);
        resumoGridLayout.Controls.Add(resumoCard4, 3, 0);
        resumoGridLayout.Controls.Add(resumoCard5, 4, 0);
        resumoGridLayout.Location = new Point(14, 40);
        resumoGridLayout.Name = "resumoGridLayout";
        resumoGridLayout.RowCount = 1;
        resumoGridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        resumoGridLayout.Size = new Size(2067, 94);
        resumoGridLayout.TabIndex = 1;
        // 
        // resumoCard1
        // 
        resumoCard1.BackColor = Color.Transparent;
        resumoCard1.Controls.Add(resumo1IconLabel);
        resumoCard1.Controls.Add(resumo1TitleLabel);
        resumoCard1.Controls.Add(resumo1StatusLabel);
        resumoCard1.Controls.Add(resumo1ProgressBg);
        resumoCard1.Dock = DockStyle.Fill;
        resumoCard1.FillColor = Color.FromArgb(248, 250, 253);
        resumoCard1.Location = new Point(0, 0);
        resumoCard1.Margin = new Padding(0, 0, 6, 0);
        resumoCard1.Name = "resumoCard1";
        resumoCard1.ShadowBlur = 0;
        resumoCard1.ShadowOffsetY = 0;
        resumoCard1.Size = new Size(407, 94);
        resumoCard1.TabIndex = 0;
        // 
        // resumo1IconLabel
        // 
        resumo1IconLabel.BackColor = Color.Transparent;
        resumo1IconLabel.Font = new Font("Segoe Fluent Icons", 11F);
        resumo1IconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        resumo1IconLabel.Location = new Point(12, 8);
        resumo1IconLabel.Name = "resumo1IconLabel";
        resumo1IconLabel.Size = new Size(22, 22);
        resumo1IconLabel.TabIndex = 0;
        resumo1IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumo1TitleLabel
        // 
        resumo1TitleLabel.BackColor = Color.Transparent;
        resumo1TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        resumo1TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        resumo1TitleLabel.Location = new Point(38, 8);
        resumo1TitleLabel.Name = "resumo1TitleLabel";
        resumo1TitleLabel.Size = new Size(140, 22);
        resumo1TitleLabel.TabIndex = 1;
        resumo1TitleLabel.Text = "Linha 01";
        resumo1TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo1StatusLabel
        // 
        resumo1StatusLabel.BackColor = Color.Transparent;
        resumo1StatusLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        resumo1StatusLabel.ForeColor = Color.FromArgb(34, 166, 82);
        resumo1StatusLabel.Location = new Point(38, 30);
        resumo1StatusLabel.Name = "resumo1StatusLabel";
        resumo1StatusLabel.Size = new Size(160, 16);
        resumo1StatusLabel.TabIndex = 2;
        resumo1StatusLabel.Text = "• Normal";
        resumo1StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo1ProgressBg
        // 
        resumo1ProgressBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resumo1ProgressBg.BackColor = Color.FromArgb(229, 232, 238);
        resumo1ProgressBg.Controls.Add(resumo1ProgressFill);
        resumo1ProgressBg.Location = new Point(12, 50);
        resumo1ProgressBg.Name = "resumo1ProgressBg";
        resumo1ProgressBg.Size = new Size(397, 4);
        resumo1ProgressBg.TabIndex = 3;
        // 
        // resumo1ProgressFill
        // 
        resumo1ProgressFill.BackColor = Color.FromArgb(34, 166, 82);
        resumo1ProgressFill.Location = new Point(0, 0);
        resumo1ProgressFill.Name = "resumo1ProgressFill";
        resumo1ProgressFill.Size = new Size(190, 4);
        resumo1ProgressFill.TabIndex = 0;
        // 
        // resumoCard2
        // 
        resumoCard2.BackColor = Color.Transparent;
        resumoCard2.Controls.Add(resumo2IconLabel);
        resumoCard2.Controls.Add(resumo2TitleLabel);
        resumoCard2.Controls.Add(resumo2StatusLabel);
        resumoCard2.Controls.Add(resumo2ProgressBg);
        resumoCard2.Dock = DockStyle.Fill;
        resumoCard2.FillColor = Color.FromArgb(248, 250, 253);
        resumoCard2.Location = new Point(419, 0);
        resumoCard2.Margin = new Padding(6, 0, 6, 0);
        resumoCard2.Name = "resumoCard2";
        resumoCard2.ShadowBlur = 0;
        resumoCard2.ShadowOffsetY = 0;
        resumoCard2.Size = new Size(401, 94);
        resumoCard2.TabIndex = 1;
        // 
        // resumo2IconLabel
        // 
        resumo2IconLabel.BackColor = Color.Transparent;
        resumo2IconLabel.Font = new Font("Segoe Fluent Icons", 11F);
        resumo2IconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        resumo2IconLabel.Location = new Point(12, 8);
        resumo2IconLabel.Name = "resumo2IconLabel";
        resumo2IconLabel.Size = new Size(22, 22);
        resumo2IconLabel.TabIndex = 0;
        resumo2IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumo2TitleLabel
        // 
        resumo2TitleLabel.BackColor = Color.Transparent;
        resumo2TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        resumo2TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        resumo2TitleLabel.Location = new Point(38, 8);
        resumo2TitleLabel.Name = "resumo2TitleLabel";
        resumo2TitleLabel.Size = new Size(140, 22);
        resumo2TitleLabel.TabIndex = 1;
        resumo2TitleLabel.Text = "Linha 02";
        resumo2TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo2StatusLabel
        // 
        resumo2StatusLabel.BackColor = Color.Transparent;
        resumo2StatusLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        resumo2StatusLabel.ForeColor = Color.FromArgb(34, 166, 82);
        resumo2StatusLabel.Location = new Point(38, 30);
        resumo2StatusLabel.Name = "resumo2StatusLabel";
        resumo2StatusLabel.Size = new Size(160, 16);
        resumo2StatusLabel.TabIndex = 2;
        resumo2StatusLabel.Text = "• Normal";
        resumo2StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo2ProgressBg
        // 
        resumo2ProgressBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resumo2ProgressBg.BackColor = Color.FromArgb(229, 232, 238);
        resumo2ProgressBg.Controls.Add(resumo2ProgressFill);
        resumo2ProgressBg.Location = new Point(12, 50);
        resumo2ProgressBg.Name = "resumo2ProgressBg";
        resumo2ProgressBg.Size = new Size(391, 4);
        resumo2ProgressBg.TabIndex = 3;
        // 
        // resumo2ProgressFill
        // 
        resumo2ProgressFill.BackColor = Color.FromArgb(34, 166, 82);
        resumo2ProgressFill.Location = new Point(0, 0);
        resumo2ProgressFill.Name = "resumo2ProgressFill";
        resumo2ProgressFill.Size = new Size(171, 4);
        resumo2ProgressFill.TabIndex = 0;
        // 
        // resumoCard3
        // 
        resumoCard3.BackColor = Color.Transparent;
        resumoCard3.Controls.Add(resumo3IconLabel);
        resumoCard3.Controls.Add(resumo3TitleLabel);
        resumoCard3.Controls.Add(resumo3StatusLabel);
        resumoCard3.Controls.Add(resumo3ProgressBg);
        resumoCard3.Dock = DockStyle.Fill;
        resumoCard3.FillColor = Color.FromArgb(248, 250, 253);
        resumoCard3.Location = new Point(832, 0);
        resumoCard3.Margin = new Padding(6, 0, 6, 0);
        resumoCard3.Name = "resumoCard3";
        resumoCard3.ShadowBlur = 0;
        resumoCard3.ShadowOffsetY = 0;
        resumoCard3.Size = new Size(401, 94);
        resumoCard3.TabIndex = 2;
        // 
        // resumo3IconLabel
        // 
        resumo3IconLabel.BackColor = Color.Transparent;
        resumo3IconLabel.Font = new Font("Segoe Fluent Icons", 11F);
        resumo3IconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        resumo3IconLabel.Location = new Point(12, 8);
        resumo3IconLabel.Name = "resumo3IconLabel";
        resumo3IconLabel.Size = new Size(22, 22);
        resumo3IconLabel.TabIndex = 0;
        resumo3IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumo3TitleLabel
        // 
        resumo3TitleLabel.BackColor = Color.Transparent;
        resumo3TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        resumo3TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        resumo3TitleLabel.Location = new Point(38, 8);
        resumo3TitleLabel.Name = "resumo3TitleLabel";
        resumo3TitleLabel.Size = new Size(140, 22);
        resumo3TitleLabel.TabIndex = 1;
        resumo3TitleLabel.Text = "Linha 03";
        resumo3TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo3StatusLabel
        // 
        resumo3StatusLabel.BackColor = Color.Transparent;
        resumo3StatusLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        resumo3StatusLabel.ForeColor = Color.FromArgb(212, 122, 28);
        resumo3StatusLabel.Location = new Point(38, 30);
        resumo3StatusLabel.Name = "resumo3StatusLabel";
        resumo3StatusLabel.Size = new Size(160, 16);
        resumo3StatusLabel.TabIndex = 2;
        resumo3StatusLabel.Text = "• Atenção";
        resumo3StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo3ProgressBg
        // 
        resumo3ProgressBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resumo3ProgressBg.BackColor = Color.FromArgb(229, 232, 238);
        resumo3ProgressBg.Controls.Add(resumo3ProgressFill);
        resumo3ProgressBg.Location = new Point(12, 50);
        resumo3ProgressBg.Name = "resumo3ProgressBg";
        resumo3ProgressBg.Size = new Size(391, 4);
        resumo3ProgressBg.TabIndex = 3;
        // 
        // resumo3ProgressFill
        // 
        resumo3ProgressFill.BackColor = Color.FromArgb(212, 122, 28);
        resumo3ProgressFill.Location = new Point(0, 0);
        resumo3ProgressFill.Name = "resumo3ProgressFill";
        resumo3ProgressFill.Size = new Size(105, 4);
        resumo3ProgressFill.TabIndex = 0;
        // 
        // resumoCard4
        // 
        resumoCard4.BackColor = Color.Transparent;
        resumoCard4.Controls.Add(resumo4IconLabel);
        resumoCard4.Controls.Add(resumo4TitleLabel);
        resumoCard4.Controls.Add(resumo4StatusLabel);
        resumoCard4.Controls.Add(resumo4ProgressBg);
        resumoCard4.Dock = DockStyle.Fill;
        resumoCard4.FillColor = Color.FromArgb(248, 250, 253);
        resumoCard4.Location = new Point(1245, 0);
        resumoCard4.Margin = new Padding(6, 0, 6, 0);
        resumoCard4.Name = "resumoCard4";
        resumoCard4.ShadowBlur = 0;
        resumoCard4.ShadowOffsetY = 0;
        resumoCard4.Size = new Size(401, 94);
        resumoCard4.TabIndex = 3;
        // 
        // resumo4IconLabel
        // 
        resumo4IconLabel.BackColor = Color.Transparent;
        resumo4IconLabel.Font = new Font("Segoe Fluent Icons", 11F);
        resumo4IconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        resumo4IconLabel.Location = new Point(12, 8);
        resumo4IconLabel.Name = "resumo4IconLabel";
        resumo4IconLabel.Size = new Size(22, 22);
        resumo4IconLabel.TabIndex = 0;
        resumo4IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumo4TitleLabel
        // 
        resumo4TitleLabel.BackColor = Color.Transparent;
        resumo4TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        resumo4TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        resumo4TitleLabel.Location = new Point(38, 8);
        resumo4TitleLabel.Name = "resumo4TitleLabel";
        resumo4TitleLabel.Size = new Size(140, 22);
        resumo4TitleLabel.TabIndex = 1;
        resumo4TitleLabel.Text = "Embalagem";
        resumo4TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo4StatusLabel
        // 
        resumo4StatusLabel.BackColor = Color.Transparent;
        resumo4StatusLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        resumo4StatusLabel.ForeColor = Color.FromArgb(59, 130, 246);
        resumo4StatusLabel.Location = new Point(38, 30);
        resumo4StatusLabel.Name = "resumo4StatusLabel";
        resumo4StatusLabel.Size = new Size(160, 16);
        resumo4StatusLabel.TabIndex = 2;
        resumo4StatusLabel.Text = "• Normal";
        resumo4StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo4ProgressBg
        // 
        resumo4ProgressBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resumo4ProgressBg.BackColor = Color.FromArgb(229, 232, 238);
        resumo4ProgressBg.Controls.Add(resumo4ProgressFill);
        resumo4ProgressBg.Location = new Point(12, 50);
        resumo4ProgressBg.Name = "resumo4ProgressBg";
        resumo4ProgressBg.Size = new Size(391, 4);
        resumo4ProgressBg.TabIndex = 3;
        // 
        // resumo4ProgressFill
        // 
        resumo4ProgressFill.BackColor = Color.FromArgb(59, 130, 246);
        resumo4ProgressFill.Location = new Point(0, 0);
        resumo4ProgressFill.Name = "resumo4ProgressFill";
        resumo4ProgressFill.Size = new Size(162, 4);
        resumo4ProgressFill.TabIndex = 0;
        // 
        // resumoCard5
        // 
        resumoCard5.BackColor = Color.Transparent;
        resumoCard5.Controls.Add(resumo5IconLabel);
        resumoCard5.Controls.Add(resumo5TitleLabel);
        resumoCard5.Controls.Add(resumo5StatusLabel);
        resumoCard5.Controls.Add(resumo5ProgressBg);
        resumoCard5.Dock = DockStyle.Fill;
        resumoCard5.FillColor = Color.FromArgb(248, 250, 253);
        resumoCard5.Location = new Point(1658, 0);
        resumoCard5.Margin = new Padding(6, 0, 0, 0);
        resumoCard5.Name = "resumoCard5";
        resumoCard5.ShadowBlur = 0;
        resumoCard5.ShadowOffsetY = 0;
        resumoCard5.Size = new Size(409, 94);
        resumoCard5.TabIndex = 4;
        // 
        // resumo5IconLabel
        // 
        resumo5IconLabel.BackColor = Color.Transparent;
        resumo5IconLabel.Font = new Font("Segoe Fluent Icons", 11F);
        resumo5IconLabel.ForeColor = Color.FromArgb(98, 108, 124);
        resumo5IconLabel.Location = new Point(12, 8);
        resumo5IconLabel.Name = "resumo5IconLabel";
        resumo5IconLabel.Size = new Size(22, 22);
        resumo5IconLabel.TabIndex = 0;
        resumo5IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // resumo5TitleLabel
        // 
        resumo5TitleLabel.BackColor = Color.Transparent;
        resumo5TitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        resumo5TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        resumo5TitleLabel.Location = new Point(38, 8);
        resumo5TitleLabel.Name = "resumo5TitleLabel";
        resumo5TitleLabel.Size = new Size(140, 22);
        resumo5TitleLabel.TabIndex = 1;
        resumo5TitleLabel.Text = "Expedição";
        resumo5TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo5StatusLabel
        // 
        resumo5StatusLabel.BackColor = Color.Transparent;
        resumo5StatusLabel.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        resumo5StatusLabel.ForeColor = Color.FromArgb(98, 108, 124);
        resumo5StatusLabel.Location = new Point(38, 30);
        resumo5StatusLabel.Name = "resumo5StatusLabel";
        resumo5StatusLabel.Size = new Size(160, 16);
        resumo5StatusLabel.TabIndex = 2;
        resumo5StatusLabel.Text = "• Parada programada";
        resumo5StatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // resumo5ProgressBg
        // 
        resumo5ProgressBg.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        resumo5ProgressBg.BackColor = Color.FromArgb(229, 232, 238);
        resumo5ProgressBg.Controls.Add(resumo5ProgressFill);
        resumo5ProgressBg.Location = new Point(12, 50);
        resumo5ProgressBg.Name = "resumo5ProgressBg";
        resumo5ProgressBg.Size = new Size(399, 4);
        resumo5ProgressBg.TabIndex = 3;
        // 
        // resumo5ProgressFill
        // 
        resumo5ProgressFill.BackColor = Color.FromArgb(98, 108, 124);
        resumo5ProgressFill.Location = new Point(0, 0);
        resumo5ProgressFill.Name = "resumo5ProgressFill";
        resumo5ProgressFill.Size = new Size(0, 4);
        resumo5ProgressFill.TabIndex = 0;
        // 
        // acoesPanel
        // 
        acoesPanel.BackColor = Color.Transparent;
        acoesPanel.Controls.Add(acoesTitleLabel);
        acoesPanel.Controls.Add(acoesGridLayout);
        acoesPanel.Dock = DockStyle.Fill;
        acoesPanel.Location = new Point(20, 656);
        acoesPanel.Margin = new Padding(0);
        acoesPanel.Name = "acoesPanel";
        acoesPanel.Size = new Size(1129, 110);
        acoesPanel.TabIndex = 4;
        // 
        // acoesTitleLabel
        // 
        acoesTitleLabel.BackColor = Color.Transparent;
        acoesTitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        acoesTitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        acoesTitleLabel.Location = new Point(0, 0);
        acoesTitleLabel.Name = "acoesTitleLabel";
        acoesTitleLabel.Size = new Size(180, 22);
        acoesTitleLabel.TabIndex = 0;
        acoesTitleLabel.Text = "Ações Rápidas";
        acoesTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acoesGridLayout
        // 
        acoesGridLayout.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        acoesGridLayout.BackColor = Color.Transparent;
        acoesGridLayout.ColumnCount = 3;
        acoesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        acoesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        acoesGridLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        acoesGridLayout.Controls.Add(acaoCard1, 0, 0);
        acoesGridLayout.Controls.Add(acaoCard2, 1, 0);
        acoesGridLayout.Controls.Add(acaoCard3, 2, 0);
        acoesGridLayout.Location = new Point(0, 28);
        acoesGridLayout.Name = "acoesGridLayout";
        acoesGridLayout.RowCount = 1;
        acoesGridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        acoesGridLayout.Size = new Size(2067, 70);
        acoesGridLayout.TabIndex = 1;
        // 
        // acaoCard1
        // 
        acaoCard1.BackColor = Color.Transparent;
        acaoCard1.BorderRadius = 10;
        acaoCard1.Controls.Add(acao1IconBg);
        acaoCard1.Controls.Add(acao1TitleLabel);
        acaoCard1.Controls.Add(acao1DescLabel);
        acaoCard1.Controls.Add(acao1ArrowLabel);
        acaoCard1.Cursor = Cursors.Hand;
        acaoCard1.Dock = DockStyle.Fill;
        acaoCard1.Location = new Point(0, 0);
        acaoCard1.Margin = new Padding(0, 0, 6, 0);
        acaoCard1.Name = "acaoCard1";
        acaoCard1.ShadowBlur = 0;
        acaoCard1.ShadowOffsetY = 0;
        acaoCard1.Size = new Size(683, 70);
        acaoCard1.TabIndex = 0;
        // 
        // acao1IconBg
        // 
        acao1IconBg.BackColor = Color.Transparent;
        acao1IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        acao1IconBg.Controls.Add(acao1IconLabel);
        acao1IconBg.FillColor = Color.FromArgb(254, 226, 230);
        acao1IconBg.Location = new Point(14, 12);
        acao1IconBg.Name = "acao1IconBg";
        acao1IconBg.ShadowBlur = 0;
        acao1IconBg.ShadowOffsetY = 0;
        acao1IconBg.Size = new Size(36, 36);
        acao1IconBg.TabIndex = 0;
        // 
        // acao1IconLabel
        // 
        acao1IconLabel.BackColor = Color.Transparent;
        acao1IconLabel.Dock = DockStyle.Fill;
        acao1IconLabel.Font = new Font("Segoe Fluent Icons", 12F);
        acao1IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        acao1IconLabel.Location = new Point(0, 0);
        acao1IconLabel.Name = "acao1IconLabel";
        acao1IconLabel.Size = new Size(36, 36);
        acao1IconLabel.TabIndex = 0;
        acao1IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // acao1TitleLabel
        // 
        acao1TitleLabel.BackColor = Color.Transparent;
        acao1TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        acao1TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        acao1TitleLabel.Location = new Point(60, 10);
        acao1TitleLabel.Name = "acao1TitleLabel";
        acao1TitleLabel.Size = new Size(220, 20);
        acao1TitleLabel.TabIndex = 1;
        acao1TitleLabel.Text = "Abrir Leitura";
        acao1TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acao1DescLabel
        // 
        acao1DescLabel.BackColor = Color.Transparent;
        acao1DescLabel.Font = new Font("Segoe UI", 7.5F);
        acao1DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        acao1DescLabel.Location = new Point(60, 30);
        acao1DescLabel.Name = "acao1DescLabel";
        acao1DescLabel.Size = new Size(280, 18);
        acao1DescLabel.TabIndex = 2;
        acao1DescLabel.Text = "Iniciar leitura de caixas e pacotes";
        acao1DescLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acao1ArrowLabel
        // 
        acao1ArrowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        acao1ArrowLabel.BackColor = Color.Transparent;
        acao1ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        acao1ArrowLabel.ForeColor = Color.FromArgb(98, 108, 124);
        acao1ArrowLabel.Location = new Point(813, 18);
        acao1ArrowLabel.Name = "acao1ArrowLabel";
        acao1ArrowLabel.Size = new Size(28, 22);
        acao1ArrowLabel.TabIndex = 3;
        acao1ArrowLabel.Text = "›";
        acao1ArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // acaoCard2
        // 
        acaoCard2.BackColor = Color.Transparent;
        acaoCard2.BorderRadius = 10;
        acaoCard2.Controls.Add(acao2IconBg);
        acaoCard2.Controls.Add(acao2TitleLabel);
        acaoCard2.Controls.Add(acao2DescLabel);
        acaoCard2.Controls.Add(acao2ArrowLabel);
        acaoCard2.Cursor = Cursors.Hand;
        acaoCard2.Dock = DockStyle.Fill;
        acaoCard2.Location = new Point(695, 0);
        acaoCard2.Margin = new Padding(6, 0, 6, 0);
        acaoCard2.Name = "acaoCard2";
        acaoCard2.ShadowBlur = 0;
        acaoCard2.ShadowOffsetY = 0;
        acaoCard2.Size = new Size(677, 70);
        acaoCard2.TabIndex = 1;
        // 
        // acao2IconBg
        // 
        acao2IconBg.BackColor = Color.Transparent;
        acao2IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        acao2IconBg.Controls.Add(acao2IconLabel);
        acao2IconBg.FillColor = Color.FromArgb(254, 226, 230);
        acao2IconBg.Location = new Point(14, 12);
        acao2IconBg.Name = "acao2IconBg";
        acao2IconBg.ShadowBlur = 0;
        acao2IconBg.ShadowOffsetY = 0;
        acao2IconBg.Size = new Size(36, 36);
        acao2IconBg.TabIndex = 0;
        // 
        // acao2IconLabel
        // 
        acao2IconLabel.BackColor = Color.Transparent;
        acao2IconLabel.Dock = DockStyle.Fill;
        acao2IconLabel.Font = new Font("Segoe Fluent Icons", 12F);
        acao2IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        acao2IconLabel.Location = new Point(0, 0);
        acao2IconLabel.Name = "acao2IconLabel";
        acao2IconLabel.Size = new Size(36, 36);
        acao2IconLabel.TabIndex = 0;
        acao2IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // acao2TitleLabel
        // 
        acao2TitleLabel.BackColor = Color.Transparent;
        acao2TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        acao2TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        acao2TitleLabel.Location = new Point(60, 10);
        acao2TitleLabel.Name = "acao2TitleLabel";
        acao2TitleLabel.Size = new Size(220, 20);
        acao2TitleLabel.TabIndex = 1;
        acao2TitleLabel.Text = "Nova Consulta";
        acao2TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acao2DescLabel
        // 
        acao2DescLabel.BackColor = Color.Transparent;
        acao2DescLabel.Font = new Font("Segoe UI", 7.5F);
        acao2DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        acao2DescLabel.Location = new Point(60, 30);
        acao2DescLabel.Name = "acao2DescLabel";
        acao2DescLabel.Size = new Size(280, 18);
        acao2DescLabel.TabIndex = 2;
        acao2DescLabel.Text = "Consultar ordens de produção";
        acao2DescLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acao2ArrowLabel
        // 
        acao2ArrowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        acao2ArrowLabel.BackColor = Color.Transparent;
        acao2ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        acao2ArrowLabel.ForeColor = Color.FromArgb(98, 108, 124);
        acao2ArrowLabel.Location = new Point(807, 18);
        acao2ArrowLabel.Name = "acao2ArrowLabel";
        acao2ArrowLabel.Size = new Size(28, 22);
        acao2ArrowLabel.TabIndex = 3;
        acao2ArrowLabel.Text = "›";
        acao2ArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // acaoCard3
        // 
        acaoCard3.BackColor = Color.Transparent;
        acaoCard3.BorderRadius = 10;
        acaoCard3.Controls.Add(acao3IconBg);
        acaoCard3.Controls.Add(acao3TitleLabel);
        acaoCard3.Controls.Add(acao3DescLabel);
        acaoCard3.Controls.Add(acao3ArrowLabel);
        acaoCard3.Cursor = Cursors.Hand;
        acaoCard3.Dock = DockStyle.Fill;
        acaoCard3.Location = new Point(1384, 0);
        acaoCard3.Margin = new Padding(6, 0, 0, 0);
        acaoCard3.Name = "acaoCard3";
        acaoCard3.ShadowBlur = 0;
        acaoCard3.ShadowOffsetY = 0;
        acaoCard3.Size = new Size(683, 70);
        acaoCard3.TabIndex = 2;
        // 
        // acao3IconBg
        // 
        acao3IconBg.BackColor = Color.Transparent;
        acao3IconBg.BorderColor = Color.FromArgb(254, 226, 230);
        acao3IconBg.Controls.Add(acao3IconLabel);
        acao3IconBg.FillColor = Color.FromArgb(254, 226, 230);
        acao3IconBg.Location = new Point(14, 12);
        acao3IconBg.Name = "acao3IconBg";
        acao3IconBg.ShadowBlur = 0;
        acao3IconBg.ShadowOffsetY = 0;
        acao3IconBg.Size = new Size(36, 36);
        acao3IconBg.TabIndex = 0;
        // 
        // acao3IconLabel
        // 
        acao3IconLabel.BackColor = Color.Transparent;
        acao3IconLabel.Dock = DockStyle.Fill;
        acao3IconLabel.Font = new Font("Segoe Fluent Icons", 12F);
        acao3IconLabel.ForeColor = Color.FromArgb(212, 37, 49);
        acao3IconLabel.Location = new Point(0, 0);
        acao3IconLabel.Name = "acao3IconLabel";
        acao3IconLabel.Size = new Size(36, 36);
        acao3IconLabel.TabIndex = 0;
        acao3IconLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // acao3TitleLabel
        // 
        acao3TitleLabel.BackColor = Color.Transparent;
        acao3TitleLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        acao3TitleLabel.ForeColor = Color.FromArgb(17, 24, 39);
        acao3TitleLabel.Location = new Point(60, 10);
        acao3TitleLabel.Name = "acao3TitleLabel";
        acao3TitleLabel.Size = new Size(220, 20);
        acao3TitleLabel.TabIndex = 1;
        acao3TitleLabel.Text = "Exportar Dados";
        acao3TitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acao3DescLabel
        // 
        acao3DescLabel.BackColor = Color.Transparent;
        acao3DescLabel.Font = new Font("Segoe UI", 7.5F);
        acao3DescLabel.ForeColor = Color.FromArgb(98, 108, 124);
        acao3DescLabel.Location = new Point(60, 30);
        acao3DescLabel.Name = "acao3DescLabel";
        acao3DescLabel.Size = new Size(280, 18);
        acao3DescLabel.TabIndex = 2;
        acao3DescLabel.Text = "Exportar dados para Excel / CSV";
        acao3DescLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // acao3ArrowLabel
        // 
        acao3ArrowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        acao3ArrowLabel.BackColor = Color.Transparent;
        acao3ArrowLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        acao3ArrowLabel.ForeColor = Color.FromArgb(98, 108, 124);
        acao3ArrowLabel.Location = new Point(813, 18);
        acao3ArrowLabel.Name = "acao3ArrowLabel";
        acao3ArrowLabel.Size = new Size(28, 22);
        acao3ArrowLabel.TabIndex = 3;
        acao3ArrowLabel.Text = "›";
        acao3ArrowLabel.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // modulesPanel
        // 
        modulesPanel.BackColor = Color.Transparent;
        modulesPanel.Location = new Point(0, 0);
        modulesPanel.Name = "modulesPanel";
        modulesPanel.Size = new Size(200, 100);
        modulesPanel.TabIndex = 0;
        modulesPanel.Visible = false;
        // 
        // modulesTitleLabel
        // 
        modulesTitleLabel.Location = new Point(0, 0);
        modulesTitleLabel.Name = "modulesTitleLabel";
        modulesTitleLabel.Size = new Size(100, 23);
        modulesTitleLabel.TabIndex = 0;
        modulesTitleLabel.Visible = false;
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
        cellUser.Size = new Size(200, 100);
        cellUser.TabIndex = 0;
        // 
        // cellUserText
        // 
        cellUserText.Dock = DockStyle.Fill;
        cellUserText.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        cellUserText.ForeColor = Color.FromArgb(98, 108, 124);
        cellUserText.Location = new Point(28, 0);
        cellUserText.Name = "cellUserText";
        cellUserText.Padding = new Padding(2, 0, 0, 0);
        cellUserText.Size = new Size(171, 100);
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
        cellUserIcon.Name = "cellUserIcon";
        cellUserIcon.Size = new Size(28, 100);
        cellUserIcon.TabIndex = 1;
        cellUserIcon.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // cellUserDivider
        // 
        cellUserDivider.BackColor = Color.FromArgb(214, 219, 226);
        cellUserDivider.Dock = DockStyle.Right;
        cellUserDivider.Location = new Point(199, 0);
        cellUserDivider.Margin = new Padding(0);
        cellUserDivider.Name = "cellUserDivider";
        cellUserDivider.Size = new Size(1, 100);
        cellUserDivider.TabIndex = 2;
        // 
        // PainelInicialForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(247, 248, 250);
        ClientSize = new Size(1366, 720);
        Controls.Add(rootLayout);
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(1180, 680);
        Name = "PainelInicialForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Painel Inicial";
        WindowState = FormWindowState.Maximized;
        rootLayout.ResumeLayout(false);
        bodyLayout.ResumeLayout(false);
        sidebarPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)sidebarLogoPictureBox).EndInit();
        menuItemInicio.ResumeLayout(false);
        menuItemCadastro.ResumeLayout(false);
        menuItemLeitura.ResumeLayout(false);
        menuItemEtiquetas.ResumeLayout(false);
        menuItemHistorico.ResumeLayout(false);
        menuItemRelatorios.ResumeLayout(false);
        menuItemSap.ResumeLayout(false);
        menuItemConfig.ResumeLayout(false);
        menuItemSeguranca.ResumeLayout(false);
        sidebarUserPanel.ResumeLayout(false);
        rightAreaLayout.ResumeLayout(false);
        headerBar.ResumeLayout(false);
        sapStatusPanel.ResumeLayout(false);
        contentScrollPanel.ResumeLayout(false);
        contentLayout.ResumeLayout(false);
        contentBrandHeaderPanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)contentBrandPictureBox).EndInit();
        footerBar.ResumeLayout(false);
        footerBarLayout.ResumeLayout(false);
        cellTerminal.ResumeLayout(false);
        cellEmpresa.ResumeLayout(false);
        cellBanco.ResumeLayout(false);
        cellHora.ResumeLayout(false);
        cellData.ResumeLayout(false);
        menuItemConsulta.ResumeLayout(false);
        welcomeBanner.ResumeLayout(false);
        welcomeIconBg.ResumeLayout(false);
        welcomeIllustrationPanel.ResumeLayout(false);
        metricsLayout.ResumeLayout(false);
        metricCard1.ResumeLayout(false);
        metric1IconBg.ResumeLayout(false);
        metricCard2.ResumeLayout(false);
        metric2IconBg.ResumeLayout(false);
        metricCard3.ResumeLayout(false);
        metric3IconBg.ResumeLayout(false);
        metricCard4.ResumeLayout(false);
        metric4IconBg.ResumeLayout(false);
        metricCard5.ResumeLayout(false);
        metric5IconBg.ResumeLayout(false);
        modulesActivitiesLayout.ResumeLayout(false);
        modulesSectionPanel.ResumeLayout(false);
        modulesGridLayout.ResumeLayout(false);
        moduleCard1.ResumeLayout(false);
        module1IconBg.ResumeLayout(false);
        moduleCard2.ResumeLayout(false);
        module2IconBg.ResumeLayout(false);
        moduleCard3.ResumeLayout(false);
        module3IconBg.ResumeLayout(false);
        moduleCard4.ResumeLayout(false);
        module4IconBg.ResumeLayout(false);
        moduleCard5.ResumeLayout(false);
        module5IconBg.ResumeLayout(false);
        activitiesPanel.ResumeLayout(false);
        activity1Panel.ResumeLayout(false);
        activity2Panel.ResumeLayout(false);
        activity3Panel.ResumeLayout(false);
        activity4Panel.ResumeLayout(false);
        activity5Panel.ResumeLayout(false);
        resumoPanel.ResumeLayout(false);
        resumoGridLayout.ResumeLayout(false);
        resumoCard1.ResumeLayout(false);
        resumo1ProgressBg.ResumeLayout(false);
        resumoCard2.ResumeLayout(false);
        resumo2ProgressBg.ResumeLayout(false);
        resumoCard3.ResumeLayout(false);
        resumo3ProgressBg.ResumeLayout(false);
        resumoCard4.ResumeLayout(false);
        resumo4ProgressBg.ResumeLayout(false);
        resumoCard5.ResumeLayout(false);
        resumo5ProgressBg.ResumeLayout(false);
        acoesPanel.ResumeLayout(false);
        acoesGridLayout.ResumeLayout(false);
        acaoCard1.ResumeLayout(false);
        acao1IconBg.ResumeLayout(false);
        acaoCard2.ResumeLayout(false);
        acao2IconBg.ResumeLayout(false);
        acaoCard3.ResumeLayout(false);
        acao3IconBg.ResumeLayout(false);
        cellUser.ResumeLayout(false);
        ResumeLayout(false);
    }

    private Label logoSaLabel;
    private Label menuSegurancaIcon;
}


