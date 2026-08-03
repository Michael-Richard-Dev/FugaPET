using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Processo;

/// <summary>
/// Layout do Controle de Apontamentos no padrão visual ATUAL do FugaPET: cabeçalho escuro com título/
/// subtítulo + status SAP, cards claros arredondados, Segoe UI, grid organizado e painel lateral.
/// Nenhum componente de outra tela é alterado; nada do visual antigo do SISCOMP é reproduzido.
/// </summary>
partial class ProcessoControleApontamentosForm
{
    private System.ComponentModel.IContainer components = null!;

    // Cabeçalho padrão (barra de título custom, idêntica às demais telas de Processo)
    private Panel customTitleBarPanel;
    private Label menuHeaderLabel;
    private Label logoSaLabel;
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

    // Corpo
    private TableLayoutPanel rootLayout;

    // Card de leitura
    private RoundedPanel leituraCard;
    private Label leituraCaptionLabel;
    private TextBox codigoLeituraTextBox;
    private Label leituraHintLabel;

    // Card de contexto da OP
    private RoundedPanel contextoCard;
    private Label opCaptionLabel;
    private Label opValueLabel;
    private Label produtoCaptionLabel;
    private Label produtoValueLabel;
    private Label itemCaptionLabel;
    private Label itemValueLabel;
    private Label loteCaptionLabel;
    private Label loteValueLabel;
    private Label usuarioCaptionLabel;
    private Label usuarioValueLabel;
    private Label estacaoCaptionLabel;
    private Label estacaoValueLabel;

    // Grid de operações
    private RoundedPanel gridCard;
    private Label gridTitleLabel;
    private DataGridView operacoesGridView;
    private DataGridViewCheckBoxColumn colSelecao;
    private DataGridViewTextBoxColumn colApontamento;
    private DataGridViewTextBoxColumn colDataInicio;
    private DataGridViewTextBoxColumn colHoraInicio;

    // Indicadores de status (mantidos como campos não-visuais: a caixa "Situação da leitura" foi removida,
    // mas o fluxo da View ainda atualiza estes labels — evita alterar a lógica de negócio).
    private Label operacaoAtualCaptionLabel;
    private Label operacaoAtualValueLabel;
    private Label proximaOperacaoCaptionLabel;
    private Label proximaOperacaoValueLabel;
    private Label eventoCaptionLabel;
    private Label eventoValueLabel;
    private Label statusApontamentoCaptionLabel;
    private Label statusApontamentoValueLabel;

    // Faixa de destaque (E): operação/processo atual + linha de instrução do operador
    private RoundedPanel destaqueCard;
    private Label destaqueCaptionLabel;
    private Label destaqueValueLabel;
    private Label instrucaoLabel;

    // Rodapé
    private Panel footerPanel;
    private Label statusLabel;

    private static readonly Color CorFundoJanela = Color.FromArgb(243, 244, 246);
    private static readonly Color CorCabecalho = Color.FromArgb(17, 24, 39);
    private static readonly Color CorBorda = Color.FromArgb(226, 232, 240);
    private static readonly Color CorTextoForte = Color.FromArgb(17, 24, 39);
    private static readonly Color CorTextoSuave = Color.FromArgb(75, 85, 99);
    private static readonly Color CorAcento = Color.FromArgb(229, 27, 43);

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
        System.ComponentModel.ComponentResourceManager resources = new(typeof(ProcessoControleApontamentosForm));
        customTitleBarPanel = new Panel();
        menuHeaderLabel = new Label();
        logoSaLabel = new Label();
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
        rootLayout = new TableLayoutPanel();
        leituraCard = new RoundedPanel();
        leituraCaptionLabel = new Label();
        codigoLeituraTextBox = new TextBox();
        leituraHintLabel = new Label();
        contextoCard = new RoundedPanel();
        opCaptionLabel = new Label();
        opValueLabel = new Label();
        produtoCaptionLabel = new Label();
        produtoValueLabel = new Label();
        itemCaptionLabel = new Label();
        itemValueLabel = new Label();
        loteCaptionLabel = new Label();
        loteValueLabel = new Label();
        usuarioCaptionLabel = new Label();
        usuarioValueLabel = new Label();
        estacaoCaptionLabel = new Label();
        estacaoValueLabel = new Label();
        gridCard = new RoundedPanel();
        gridTitleLabel = new Label();
        operacoesGridView = new DataGridView();
        colSelecao = new DataGridViewCheckBoxColumn();
        colApontamento = new DataGridViewTextBoxColumn();
        colDataInicio = new DataGridViewTextBoxColumn();
        colHoraInicio = new DataGridViewTextBoxColumn();
        operacaoAtualCaptionLabel = new Label();
        operacaoAtualValueLabel = new Label();
        proximaOperacaoCaptionLabel = new Label();
        proximaOperacaoValueLabel = new Label();
        eventoCaptionLabel = new Label();
        eventoValueLabel = new Label();
        statusApontamentoCaptionLabel = new Label();
        statusApontamentoValueLabel = new Label();
        destaqueCard = new RoundedPanel();
        destaqueCaptionLabel = new Label();
        destaqueValueLabel = new Label();
        instrucaoLabel = new Label();
        footerPanel = new Panel();
        statusLabel = new Label();

        SuspendLayout();
        //
        // customTitleBarPanel (barra de título padrão FugaPET)
        //
        customTitleBarPanel.BackColor = Color.FromArgb(24, 31, 43);
        customTitleBarPanel.Controls.Add(logoSaLabel);
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
        customTitleBarPanel.Dock = DockStyle.Top;
        customTitleBarPanel.Name = "customTitleBarPanel";
        customTitleBarPanel.Size = new Size(1280, 52);
        //
        // logoSaLabel
        //
        logoSaLabel.BackColor = Color.Transparent;
        logoSaLabel.Font = new Font("Cascadia Code", 3F, FontStyle.Bold, GraphicsUnit.Point, 0);
        logoSaLabel.ForeColor = Color.White;
        logoSaLabel.Location = new Point(171, 12);
        logoSaLabel.Name = "logoSaLabel";
        logoSaLabel.Size = new Size(21, 10);
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
        companyLogoPictureBox.TabStop = false;
        //
        // headerDividerLabel
        //
        headerDividerLabel.BackColor = Color.FromArgb(132, 142, 156);
        headerDividerLabel.Location = new Point(208, 12);
        headerDividerLabel.Name = "headerDividerLabel";
        headerDividerLabel.Size = new Size(1, 30);
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
        //
        // headerTitleIconPictureBox
        //
        headerTitleIconPictureBox.BackColor = Color.Transparent;
        headerTitleIconPictureBox.Dock = DockStyle.Fill;
        headerTitleIconPictureBox.Image = (Image)resources.GetObject("headerTitleIconPictureBox.Image");
        headerTitleIconPictureBox.Name = "headerTitleIconPictureBox";
        headerTitleIconPictureBox.Size = new Size(29, 29);
        headerTitleIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerTitleIconPictureBox.TabStop = false;
        //
        // headerTitleLabel
        //
        headerTitleLabel.BackColor = Color.Transparent;
        headerTitleLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(278, 6);
        headerTitleLabel.Name = "headerTitleLabel";
        headerTitleLabel.Size = new Size(320, 23);
        headerTitleLabel.Text = "Controle de Apontamentos";
        headerTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // headerSubtitleLabel
        //
        headerSubtitleLabel.BackColor = Color.Transparent;
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
        headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228);
        headerSubtitleLabel.Location = new Point(279, 29);
        headerSubtitleLabel.Name = "headerSubtitleLabel";
        headerSubtitleLabel.Size = new Size(520, 18);
        headerSubtitleLabel.Text = "Leitura, início e término das operações da ordem de produção";
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
        sapStatusPanel.Location = new Point(786, 12);
        sapStatusPanel.Name = "sapStatusPanel";
        sapStatusPanel.ShadowBlur = 0;
        sapStatusPanel.ShadowOffsetY = 0;
        sapStatusPanel.Size = new Size(350, 27);
        //
        // sapStatusDotLabel
        //
        sapStatusDotLabel.BackColor = Color.Transparent;
        sapStatusDotLabel.Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusDotLabel.ForeColor = Color.FromArgb(250, 204, 21);
        sapStatusDotLabel.Location = new Point(10, 4);
        sapStatusDotLabel.Name = "sapStatusDotLabel";
        sapStatusDotLabel.Size = new Size(13, 18);
        sapStatusDotLabel.Text = "●";
        sapStatusDotLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // sapStatusLabel
        //
        sapStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        sapStatusLabel.AutoEllipsis = true;
        sapStatusLabel.AutoSize = false;
        sapStatusLabel.BackColor = Color.Transparent;
        sapStatusLabel.Font = new Font("Cascadia Code", 7.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusLabel.ForeColor = Color.White;
        sapStatusLabel.Location = new Point(27, 4);
        sapStatusLabel.Name = "sapStatusLabel";
        sapStatusLabel.Padding = new Padding(2, 0, 4, 0);
        sapStatusLabel.Size = new Size(316, 19);
        sapStatusLabel.Text = "SAP: verificando...";
        sapStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // minimizeWindowLabel
        //
        minimizeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        minimizeWindowLabel.BackColor = Color.Transparent;
        minimizeWindowLabel.Cursor = Cursors.Hand;
        minimizeWindowLabel.Font = new Font("Cascadia Code", 12.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
        minimizeWindowLabel.ForeColor = Color.White;
        minimizeWindowLabel.Location = new Point(1136, 0);
        minimizeWindowLabel.Name = "minimizeWindowLabel";
        minimizeWindowLabel.Size = new Size(48, 52);
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
        maximizeWindowLabel.Location = new Point(1184, 0);
        maximizeWindowLabel.Name = "maximizeWindowLabel";
        maximizeWindowLabel.Size = new Size(48, 52);
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
        closeWindowLabel.Location = new Point(1232, 0);
        closeWindowLabel.Name = "closeWindowLabel";
        closeWindowLabel.Size = new Size(48, 52);
        closeWindowLabel.Text = "×";
        closeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // rootLayout
        //
        // Coluna única: sem o painel lateral pesado. A grade principal é o centro operacional da tela.
        rootLayout.ColumnCount = 1;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(leituraCard, 0, 0);       // B: leitura do código
        rootLayout.Controls.Add(contextoCard, 0, 1);      // C: resumo OP/Produto/Item/Lote/Usuário/Estação
        rootLayout.Controls.Add(destaqueCard, 0, 2);      // E: faixa de destaque da operação/processo atual
        rootLayout.Controls.Add(instrucaoLabel, 0, 3);    // instrução do operador (linha fina)
        rootLayout.Controls.Add(gridCard, 0, 4);          // F: grade das operações da OP (centro da tela)
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(16, 12, 16, 8);
        rootLayout.RowCount = 5;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F)); // B leitura
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));  // C resumo
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));  // E faixa de destaque
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // instrução
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // F grade central (maior)
        //
        // leituraCard
        //
        leituraCard.BackColor = Color.Transparent;
        leituraCard.BorderColor = CorBorda;
        leituraCard.BorderRadius = 10;
        leituraCard.Controls.Add(leituraCaptionLabel);
        leituraCard.Controls.Add(codigoLeituraTextBox);
        leituraCard.Controls.Add(leituraHintLabel);
        leituraCard.Dock = DockStyle.Fill;
        leituraCard.FillColor = Color.White;
        leituraCard.Margin = new Padding(0, 0, 0, 10);
        leituraCard.Name = "leituraCard";
        leituraCard.ShadowBlur = 0;
        leituraCard.ShadowOffsetY = 0;
        //
        // leituraCaptionLabel
        //
        leituraCaptionLabel.AutoSize = true;
        leituraCaptionLabel.BackColor = Color.Transparent;
        leituraCaptionLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        leituraCaptionLabel.ForeColor = CorTextoSuave;
        leituraCaptionLabel.Location = new Point(18, 12);
        leituraCaptionLabel.Name = "leituraCaptionLabel";
        leituraCaptionLabel.Text = "CÓDIGO DA OPERAÇÃO";
        //
        // codigoLeituraTextBox
        //
        // Largura FIXA (sem ancorar à direita): o campo não se estica com o card em tela cheia.
        codigoLeituraTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        codigoLeituraTextBox.BorderStyle = BorderStyle.FixedSingle;
        codigoLeituraTextBox.CharacterCasing = CharacterCasing.Upper;
        codigoLeituraTextBox.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point, 0);
        codigoLeituraTextBox.ForeColor = CorTextoForte;
        codigoLeituraTextBox.Location = new Point(18, 36);
        codigoLeituraTextBox.MaxLength = 40;
        codigoLeituraTextBox.Name = "codigoLeituraTextBox";
        codigoLeituraTextBox.Size = new Size(520, 40);
        codigoLeituraTextBox.TabIndex = 0;
        //
        // leituraHintLabel
        //
        leituraHintLabel.AutoSize = true;
        leituraHintLabel.BackColor = Color.Transparent;
        leituraHintLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        leituraHintLabel.ForeColor = CorTextoSuave;
        leituraHintLabel.Location = new Point(596, 48);
        leituraHintLabel.Name = "leituraHintLabel";
        leituraHintLabel.Text = "Leia o código de início ou término impresso na Ordem de Produção.";
        //
        // contextoCard
        //
        contextoCard.BackColor = Color.Transparent;
        contextoCard.BorderColor = CorBorda;
        contextoCard.BorderRadius = 10;
        contextoCard.Controls.Add(opCaptionLabel);
        contextoCard.Controls.Add(opValueLabel);
        contextoCard.Controls.Add(produtoCaptionLabel);
        contextoCard.Controls.Add(produtoValueLabel);
        contextoCard.Controls.Add(itemCaptionLabel);
        contextoCard.Controls.Add(itemValueLabel);
        contextoCard.Controls.Add(loteCaptionLabel);
        contextoCard.Controls.Add(loteValueLabel);
        contextoCard.Controls.Add(usuarioCaptionLabel);
        contextoCard.Controls.Add(usuarioValueLabel);
        contextoCard.Controls.Add(estacaoCaptionLabel);
        contextoCard.Controls.Add(estacaoValueLabel);
        contextoCard.Dock = DockStyle.Fill;
        contextoCard.FillColor = Color.White;
        contextoCard.Margin = new Padding(0, 0, 0, 10);
        contextoCard.Name = "contextoCard";
        contextoCard.ShadowBlur = 0;
        contextoCard.ShadowOffsetY = 0;
        //
        // gridCard
        //
        gridCard.BackColor = Color.Transparent;
        gridCard.BorderColor = CorBorda;
        gridCard.BorderRadius = 10;
        gridCard.Controls.Add(operacoesGridView);
        gridCard.Controls.Add(gridTitleLabel);
        gridCard.Dock = DockStyle.Fill;
        gridCard.FillColor = Color.White;
        gridCard.Margin = new Padding(0, 0, 0, 0);
        gridCard.Name = "gridCard";
        gridCard.Padding = new Padding(12, 40, 12, 12);
        gridCard.ShadowBlur = 0;
        gridCard.ShadowOffsetY = 0;
        //
        // gridTitleLabel
        //
        gridTitleLabel.AutoSize = true;
        gridTitleLabel.BackColor = Color.Transparent;
        gridTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        gridTitleLabel.ForeColor = CorTextoSuave;
        gridTitleLabel.Location = new Point(18, 14);
        gridTitleLabel.Name = "gridTitleLabel";
        gridTitleLabel.Text = "OPERAÇÕES DA ORDEM DE PRODUÇÃO";
        //
        // operacoesGridView
        //
        operacoesGridView.AllowUserToAddRows = false;
        operacoesGridView.AllowUserToDeleteRows = false;
        operacoesGridView.AllowUserToResizeRows = false;
        operacoesGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        operacoesGridView.BackgroundColor = Color.White;
        operacoesGridView.BorderStyle = BorderStyle.None;
        operacoesGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        operacoesGridView.ColumnHeadersHeight = 34;
        operacoesGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        operacoesGridView.Columns.AddRange(
            colSelecao, colApontamento, colDataInicio, colHoraInicio);
        operacoesGridView.Dock = DockStyle.Fill;
        operacoesGridView.EnableHeadersVisualStyles = false;
        operacoesGridView.MultiSelect = false;
        operacoesGridView.Name = "operacoesGridView";
        operacoesGridView.ReadOnly = true;
        operacoesGridView.RowHeadersVisible = false;
        operacoesGridView.RowTemplate.Height = 30;
        operacoesGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        operacoesGridView.TabStop = false;

        colSelecao.HeaderText = "Seleção";
        colSelecao.Name = "colSelecao";
        colSelecao.FillWeight = 60F;
        colSelecao.FlatStyle = FlatStyle.Flat;
        colApontamento.HeaderText = "Apontamento / Batida";
        colApontamento.Name = "colApontamento";
        colApontamento.FillWeight = 260F;
        colDataInicio.HeaderText = "Data início";
        colDataInicio.Name = "colDataInicio";
        colDataInicio.FillWeight = 90F;
        colHoraInicio.HeaderText = "Hora início";
        colHoraInicio.Name = "colHoraInicio";
        colHoraInicio.FillWeight = 90F;
        //
        // destaqueCard (E): faixa de destaque da operação/processo atual (acento institucional FugaPET)
        //
        destaqueCard.BackColor = Color.Transparent;
        destaqueCard.BorderColor = Color.FromArgb(248, 210, 213);
        destaqueCard.BorderRadius = 10;
        destaqueCard.Controls.Add(destaqueCaptionLabel);
        destaqueCard.Controls.Add(destaqueValueLabel);
        destaqueCard.Dock = DockStyle.Fill;
        destaqueCard.FillColor = Color.FromArgb(254, 242, 242);
        destaqueCard.Margin = new Padding(0, 0, 0, 8);
        destaqueCard.Name = "destaqueCard";
        destaqueCard.ShadowBlur = 0;
        destaqueCard.ShadowOffsetY = 0;
        //
        // destaqueCaptionLabel
        //
        destaqueCaptionLabel.AutoSize = true;
        destaqueCaptionLabel.BackColor = Color.Transparent;
        destaqueCaptionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
        destaqueCaptionLabel.ForeColor = CorAcento;
        destaqueCaptionLabel.Location = new Point(18, 8);
        destaqueCaptionLabel.Name = "destaqueCaptionLabel";
        destaqueCaptionLabel.Text = "OPERAÇÃO / PROCESSO ATUAL";
        //
        // destaqueValueLabel
        //
        destaqueValueLabel.AutoEllipsis = true;
        destaqueValueLabel.BackColor = Color.Transparent;
        destaqueValueLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0);
        destaqueValueLabel.ForeColor = CorTextoForte;
        destaqueValueLabel.Location = new Point(18, 26);
        destaqueValueLabel.Name = "destaqueValueLabel";
        destaqueValueLabel.Size = new Size(900, 26);
        destaqueValueLabel.Text = "-";
        //
        // instrucaoLabel (linha fina de instrução do operador, largura total)
        //
        instrucaoLabel.BackColor = Color.Transparent;
        instrucaoLabel.Dock = DockStyle.Fill;
        instrucaoLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        instrucaoLabel.ForeColor = CorTextoSuave;
        instrucaoLabel.Margin = new Padding(2, 0, 0, 4);
        instrucaoLabel.Name = "instrucaoLabel";
        instrucaoLabel.Text = "Leia o código de início da operação para começar.";
        instrucaoLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // footerPanel
        //
        footerPanel.BackColor = Color.White;
        footerPanel.Controls.Add(statusLabel);
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Name = "footerPanel";
        footerPanel.Size = new Size(1280, 34);
        //
        // statusLabel
        //
        statusLabel.BackColor = Color.Transparent;
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        statusLabel.ForeColor = CorTextoSuave;
        statusLabel.Name = "statusLabel";
        statusLabel.Padding = new Padding(20, 0, 0, 0);
        statusLabel.Text = "Aguardando leitura do código da operação.";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        //
        // ProcessoControleApontamentosForm
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = CorFundoJanela;
        ClientSize = new Size(1280, 720);
        Controls.Add(rootLayout);
        Controls.Add(footerPanel);
        Controls.Add(customTitleBarPanel);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Name = "ProcessoControleApontamentosForm";
        StartPosition = FormStartPosition.CenterScreen;
        // Abre maximizada (tela cheia), como pedido.
        WindowState = FormWindowState.Maximized;
        Text = "Controle de Apontamentos";
        ResumeLayout(false);
    }

    /// <summary>Rótulo/valor do card de contexto, criados com o mesmo estilo (caption cinza + valor forte).</summary>
    private static void ConfigurarParCampo(
        Label caption, Label valor, string titulo, int x, int y, int largura)
    {
        caption.AutoSize = true;
        caption.BackColor = Color.Transparent;
        caption.Font = new Font("Segoe UI", 8F, FontStyle.Bold, GraphicsUnit.Point, 0);
        caption.ForeColor = CorTextoSuave;
        caption.Location = new Point(x, y);
        caption.Text = titulo;

        valor.AutoEllipsis = true;
        valor.BackColor = Color.Transparent;
        valor.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point, 0);
        valor.ForeColor = CorTextoForte;
        valor.Location = new Point(x, y + 20);
        valor.Size = new Size(largura, 24);
        valor.Text = "-";
    }
}
