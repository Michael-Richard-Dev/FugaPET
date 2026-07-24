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

    // Cabeçalho
    private Panel headerPanel;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private RoundedPanel sapStatusPanel;
    private Label sapStatusLabel;
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
    private DataGridViewTextBoxColumn colSequencia;
    private DataGridViewTextBoxColumn colOperacao;
    private DataGridViewTextBoxColumn colSuboperacao;
    private DataGridViewTextBoxColumn colDescricao;
    private DataGridViewTextBoxColumn colCentroTrabalho;
    private DataGridViewTextBoxColumn colTipoProcesso;
    private DataGridViewTextBoxColumn colStatus;
    private DataGridViewTextBoxColumn colUsuarioInicio;
    private DataGridViewTextBoxColumn colInicio;
    private DataGridViewTextBoxColumn colTermino;
    private DataGridViewTextBoxColumn colDuracao;
    private DataGridViewTextBoxColumn colTelaDestino;

    // Painel lateral
    private RoundedPanel lateralCard;
    private Label lateralTitleLabel;
    private Label operacaoAtualCaptionLabel;
    private Label operacaoAtualValueLabel;
    private Label proximaOperacaoCaptionLabel;
    private Label proximaOperacaoValueLabel;
    private Label eventoCaptionLabel;
    private Label eventoValueLabel;
    private Label statusApontamentoCaptionLabel;
    private Label statusApontamentoValueLabel;
    private RoundedPanel instrucaoPanel;
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
        headerPanel = new Panel();
        headerTitleLabel = new Label();
        headerSubtitleLabel = new Label();
        sapStatusPanel = new RoundedPanel();
        sapStatusLabel = new Label();
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
        colSequencia = new DataGridViewTextBoxColumn();
        colOperacao = new DataGridViewTextBoxColumn();
        colSuboperacao = new DataGridViewTextBoxColumn();
        colDescricao = new DataGridViewTextBoxColumn();
        colCentroTrabalho = new DataGridViewTextBoxColumn();
        colTipoProcesso = new DataGridViewTextBoxColumn();
        colStatus = new DataGridViewTextBoxColumn();
        colUsuarioInicio = new DataGridViewTextBoxColumn();
        colInicio = new DataGridViewTextBoxColumn();
        colTermino = new DataGridViewTextBoxColumn();
        colDuracao = new DataGridViewTextBoxColumn();
        colTelaDestino = new DataGridViewTextBoxColumn();
        lateralCard = new RoundedPanel();
        lateralTitleLabel = new Label();
        operacaoAtualCaptionLabel = new Label();
        operacaoAtualValueLabel = new Label();
        proximaOperacaoCaptionLabel = new Label();
        proximaOperacaoValueLabel = new Label();
        eventoCaptionLabel = new Label();
        eventoValueLabel = new Label();
        statusApontamentoCaptionLabel = new Label();
        statusApontamentoValueLabel = new Label();
        instrucaoPanel = new RoundedPanel();
        instrucaoLabel = new Label();
        footerPanel = new Panel();
        statusLabel = new Label();

        SuspendLayout();
        //
        // headerPanel
        //
        headerPanel.BackColor = CorCabecalho;
        headerPanel.Controls.Add(headerTitleLabel);
        headerPanel.Controls.Add(headerSubtitleLabel);
        headerPanel.Controls.Add(sapStatusPanel);
        headerPanel.Controls.Add(closeWindowLabel);
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Name = "headerPanel";
        headerPanel.Size = new Size(1280, 84);
        //
        // headerTitleLabel
        //
        headerTitleLabel.AutoSize = true;
        headerTitleLabel.BackColor = Color.Transparent;
        headerTitleLabel.Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point, 0);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(24, 16);
        headerTitleLabel.Name = "headerTitleLabel";
        headerTitleLabel.Text = "Controle de Apontamentos";
        //
        // headerSubtitleLabel
        //
        headerSubtitleLabel.AutoSize = true;
        headerSubtitleLabel.BackColor = Color.Transparent;
        headerSubtitleLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        headerSubtitleLabel.ForeColor = Color.FromArgb(156, 163, 175);
        headerSubtitleLabel.Location = new Point(26, 50);
        headerSubtitleLabel.Name = "headerSubtitleLabel";
        headerSubtitleLabel.Text = "Leitura, início e término das operações da ordem de produção";
        //
        // sapStatusPanel
        //
        sapStatusPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        sapStatusPanel.BackColor = Color.Transparent;
        sapStatusPanel.BorderColor = Color.FromArgb(55, 65, 81);
        sapStatusPanel.BorderRadius = 8;
        sapStatusPanel.Controls.Add(sapStatusLabel);
        sapStatusPanel.FillColor = Color.FromArgb(31, 41, 55);
        sapStatusPanel.Location = new Point(950, 26);
        sapStatusPanel.Name = "sapStatusPanel";
        sapStatusPanel.ShadowBlur = 0;
        sapStatusPanel.ShadowOffsetY = 0;
        sapStatusPanel.Size = new Size(268, 32);
        //
        // sapStatusLabel
        //
        sapStatusLabel.BackColor = Color.Transparent;
        sapStatusLabel.Dock = DockStyle.Fill;
        sapStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusLabel.ForeColor = Color.FromArgb(156, 163, 175);
        sapStatusLabel.Name = "sapStatusLabel";
        sapStatusLabel.Text = "SAP: verificando...";
        sapStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // closeWindowLabel
        //
        closeWindowLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeWindowLabel.BackColor = Color.Transparent;
        closeWindowLabel.Cursor = Cursors.Hand;
        closeWindowLabel.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        closeWindowLabel.ForeColor = Color.FromArgb(156, 163, 175);
        closeWindowLabel.Location = new Point(1234, 22);
        closeWindowLabel.Name = "closeWindowLabel";
        closeWindowLabel.Size = new Size(32, 32);
        closeWindowLabel.Text = "✕";
        closeWindowLabel.TextAlign = ContentAlignment.MiddleCenter;
        //
        // rootLayout
        //
        rootLayout.ColumnCount = 2;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
        rootLayout.Controls.Add(leituraCard, 0, 0);
        rootLayout.Controls.Add(contextoCard, 0, 1);
        rootLayout.Controls.Add(gridCard, 0, 2);
        rootLayout.Controls.Add(lateralCard, 1, 0);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Name = "rootLayout";
        rootLayout.Padding = new Padding(16, 12, 16, 8);
        rootLayout.RowCount = 3;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.SetRowSpan(lateralCard, 3);
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
        leituraCard.Margin = new Padding(0, 0, 12, 10);
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
        codigoLeituraTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        codigoLeituraTextBox.BorderStyle = BorderStyle.FixedSingle;
        codigoLeituraTextBox.CharacterCasing = CharacterCasing.Upper;
        codigoLeituraTextBox.Font = new Font("Segoe UI", 20F, FontStyle.Bold, GraphicsUnit.Point, 0);
        codigoLeituraTextBox.ForeColor = CorTextoForte;
        codigoLeituraTextBox.Location = new Point(18, 36);
        codigoLeituraTextBox.MaxLength = 40;
        codigoLeituraTextBox.Name = "codigoLeituraTextBox";
        codigoLeituraTextBox.Size = new Size(560, 43);
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
        contextoCard.Margin = new Padding(0, 0, 12, 10);
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
        gridCard.Margin = new Padding(0, 0, 12, 0);
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
            colSequencia, colOperacao, colSuboperacao, colDescricao, colCentroTrabalho, colTipoProcesso,
            colStatus, colUsuarioInicio, colInicio, colTermino, colDuracao, colTelaDestino);
        operacoesGridView.Dock = DockStyle.Fill;
        operacoesGridView.EnableHeadersVisualStyles = false;
        operacoesGridView.MultiSelect = false;
        operacoesGridView.Name = "operacoesGridView";
        operacoesGridView.ReadOnly = true;
        operacoesGridView.RowHeadersVisible = false;
        operacoesGridView.RowTemplate.Height = 30;
        operacoesGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        operacoesGridView.TabStop = false;

        colSequencia.HeaderText = "Seq.";
        colSequencia.Name = "colSequencia";
        colSequencia.FillWeight = 45F;
        colOperacao.HeaderText = "Operação";
        colOperacao.Name = "colOperacao";
        colOperacao.FillWeight = 60F;
        colSuboperacao.HeaderText = "Subop.";
        colSuboperacao.Name = "colSuboperacao";
        colSuboperacao.FillWeight = 50F;
        colDescricao.HeaderText = "Descrição";
        colDescricao.Name = "colDescricao";
        colDescricao.FillWeight = 150F;
        colCentroTrabalho.HeaderText = "Centro de trabalho";
        colCentroTrabalho.Name = "colCentroTrabalho";
        colCentroTrabalho.FillWeight = 90F;
        colTipoProcesso.HeaderText = "Tipo de processo";
        colTipoProcesso.Name = "colTipoProcesso";
        colTipoProcesso.FillWeight = 110F;
        colStatus.HeaderText = "Status";
        colStatus.Name = "colStatus";
        colStatus.FillWeight = 95F;
        colUsuarioInicio.HeaderText = "Iniciado por";
        colUsuarioInicio.Name = "colUsuarioInicio";
        colUsuarioInicio.FillWeight = 80F;
        colInicio.HeaderText = "Início";
        colInicio.Name = "colInicio";
        colInicio.FillWeight = 85F;
        colTermino.HeaderText = "Término";
        colTermino.Name = "colTermino";
        colTermino.FillWeight = 85F;
        colDuracao.HeaderText = "Duração";
        colDuracao.Name = "colDuracao";
        colDuracao.FillWeight = 65F;
        colTelaDestino.HeaderText = "Tela de destino";
        colTelaDestino.Name = "colTelaDestino";
        colTelaDestino.FillWeight = 100F;
        //
        // lateralCard
        //
        lateralCard.BackColor = Color.Transparent;
        lateralCard.BorderColor = CorBorda;
        lateralCard.BorderRadius = 10;
        lateralCard.Controls.Add(lateralTitleLabel);
        lateralCard.Controls.Add(operacaoAtualCaptionLabel);
        lateralCard.Controls.Add(operacaoAtualValueLabel);
        lateralCard.Controls.Add(proximaOperacaoCaptionLabel);
        lateralCard.Controls.Add(proximaOperacaoValueLabel);
        lateralCard.Controls.Add(eventoCaptionLabel);
        lateralCard.Controls.Add(eventoValueLabel);
        lateralCard.Controls.Add(statusApontamentoCaptionLabel);
        lateralCard.Controls.Add(statusApontamentoValueLabel);
        lateralCard.Controls.Add(instrucaoPanel);
        lateralCard.Dock = DockStyle.Fill;
        lateralCard.FillColor = Color.White;
        lateralCard.Margin = new Padding(0);
        lateralCard.Name = "lateralCard";
        lateralCard.ShadowBlur = 0;
        lateralCard.ShadowOffsetY = 0;
        //
        // lateralTitleLabel
        //
        lateralTitleLabel.AutoSize = true;
        lateralTitleLabel.BackColor = Color.Transparent;
        lateralTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lateralTitleLabel.ForeColor = CorTextoSuave;
        lateralTitleLabel.Location = new Point(18, 14);
        lateralTitleLabel.Name = "lateralTitleLabel";
        lateralTitleLabel.Text = "SITUAÇÃO DA LEITURA";
        //
        // instrucaoPanel
        //
        instrucaoPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        instrucaoPanel.BackColor = Color.Transparent;
        instrucaoPanel.BorderColor = Color.FromArgb(226, 232, 240);
        instrucaoPanel.BorderRadius = 8;
        instrucaoPanel.Controls.Add(instrucaoLabel);
        instrucaoPanel.FillColor = Color.FromArgb(243, 244, 246);
        instrucaoPanel.Location = new Point(14, 320);
        instrucaoPanel.Name = "instrucaoPanel";
        instrucaoPanel.ShadowBlur = 0;
        instrucaoPanel.ShadowOffsetY = 0;
        instrucaoPanel.Size = new Size(272, 120);
        //
        // instrucaoLabel
        //
        instrucaoLabel.BackColor = Color.Transparent;
        instrucaoLabel.Dock = DockStyle.Fill;
        instrucaoLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        instrucaoLabel.ForeColor = CorTextoSuave;
        instrucaoLabel.Name = "instrucaoLabel";
        instrucaoLabel.Padding = new Padding(10);
        instrucaoLabel.Text = "Leia o código de início da operação para começar.";
        instrucaoLabel.TextAlign = ContentAlignment.TopLeft;
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
        Controls.Add(headerPanel);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        Name = "ProcessoControleApontamentosForm";
        StartPosition = FormStartPosition.CenterScreen;
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
