using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Processo;

partial class ProcessoResultadoApontamentoForm
{
    private System.ComponentModel.IContainer components = null!;
    private Panel headerPanel;
    private Label headerTitleLabel;
    private Label headerSubtitleLabel;
    private RoundedPanel sapStatusPanel;
    private Label sapStatusLabel;
    private Label minimizeWindowLabel;
    private Label maximizeWindowLabel;
    private Label closeWindowLabel;
    private TableLayoutPanel rootLayout;
    private RoundedPanel contextoCard;
    private Label contextoTitleLabel;
    private Label opCaptionLabel;
    private Label opValueLabel;
    private Label itemCaptionLabel;
    private Label itemValueLabel;
    private Label produtoCaptionLabel;
    private Label produtoValueLabel;
    private Label sequenciaCaptionLabel;
    private Label sequenciaValueLabel;
    private Label operacaoCaptionLabel;
    private Label operacaoValueLabel;
    private Label descricaoOperacaoCaptionLabel;
    private Label descricaoOperacaoValueLabel;
    private Label centroTrabalhoCaptionLabel;
    private Label centroTrabalhoValueLabel;
    private Label usuarioCaptionLabel;
    private Label usuarioValueLabel;
    private Label estacaoCaptionLabel;
    private Label estacaoValueLabel;
    private Label inicioCaptionLabel;
    private Label inicioValueLabel;
    private RoundedPanel gridCard;
    private Label gridTitleLabel;
    private DataGridView resultadosGridView;
    private DataGridViewTextBoxColumn colTipo;
    private DataGridViewTextBoxColumn colMedida;
    private DataGridViewTextBoxColumn colReferencia;
    private DataGridViewTextBoxColumn colResultado;
    private RoundedPanel lateralCard;
    private Label lateralTitleLabel;
    private Label statusCaptionLabel;
    private Label statusValueLabel;
    private Label atalhoCaptionLabel;
    private Label atalhoValueLabel;
    private RoundedPanel instrucaoPanel;
    private Label instrucaoLabel;
    private Panel acoesPanel;
    private TableLayoutPanel acoesTable;
    private Button gravarResultadoButton;
    private Button fecharButton;
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
        minimizeWindowLabel = new Label();
        maximizeWindowLabel = new Label();
        closeWindowLabel = new Label();
        rootLayout = new TableLayoutPanel();
        contextoCard = new RoundedPanel();
        contextoTitleLabel = new Label();
        opCaptionLabel = new Label(); opValueLabel = new Label();
        itemCaptionLabel = new Label(); itemValueLabel = new Label();
        produtoCaptionLabel = new Label(); produtoValueLabel = new Label();
        sequenciaCaptionLabel = new Label(); sequenciaValueLabel = new Label();
        operacaoCaptionLabel = new Label(); operacaoValueLabel = new Label();
        descricaoOperacaoCaptionLabel = new Label(); descricaoOperacaoValueLabel = new Label();
        centroTrabalhoCaptionLabel = new Label(); centroTrabalhoValueLabel = new Label();
        usuarioCaptionLabel = new Label(); usuarioValueLabel = new Label();
        estacaoCaptionLabel = new Label(); estacaoValueLabel = new Label();
        inicioCaptionLabel = new Label(); inicioValueLabel = new Label();
        gridCard = new RoundedPanel();
        gridTitleLabel = new Label();
        resultadosGridView = new DataGridView();
        colTipo = new DataGridViewTextBoxColumn();
        colMedida = new DataGridViewTextBoxColumn();
        colReferencia = new DataGridViewTextBoxColumn();
        colResultado = new DataGridViewTextBoxColumn();
        lateralCard = new RoundedPanel();
        lateralTitleLabel = new Label();
        statusCaptionLabel = new Label(); statusValueLabel = new Label();
        atalhoCaptionLabel = new Label(); atalhoValueLabel = new Label();
        instrucaoPanel = new RoundedPanel();
        instrucaoLabel = new Label();
        acoesPanel = new Panel();
        acoesTable = new TableLayoutPanel();
        gravarResultadoButton = new Button();
        fecharButton = new Button();
        footerPanel = new Panel();
        statusLabel = new Label();
        SuspendLayout();

        headerPanel.BackColor = CorCabecalho;
        headerPanel.Controls.AddRange(new Control[] { headerTitleLabel, headerSubtitleLabel, sapStatusPanel, minimizeWindowLabel, maximizeWindowLabel, closeWindowLabel });
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Size = new Size(1280, 84);
        headerPanel.MouseDown += HeaderPanel_MouseDown;

        headerTitleLabel.AutoSize = true;
        headerTitleLabel.Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point, 0);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(24, 16);
        headerTitleLabel.Text = "Resultado do Apontamento";

        headerSubtitleLabel.AutoSize = true;
        headerSubtitleLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        headerSubtitleLabel.ForeColor = Color.FromArgb(156, 163, 175);
        headerSubtitleLabel.Location = new Point(26, 50);
        headerSubtitleLabel.Text = "OP - | Operação -";

        sapStatusPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        sapStatusPanel.BorderColor = Color.FromArgb(55, 65, 81);
        sapStatusPanel.BorderRadius = 8;
        sapStatusPanel.Controls.Add(sapStatusLabel);
        sapStatusPanel.FillColor = Color.FromArgb(31, 41, 55);
        sapStatusPanel.Location = new Point(900, 26);
        sapStatusPanel.Size = new Size(260, 32);
        sapStatusLabel.Dock = DockStyle.Fill;
        sapStatusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        sapStatusLabel.ForeColor = Color.FromArgb(156, 163, 175);
        sapStatusLabel.Text = "RESULTADO LOCAL";
        sapStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        ConfigurarBotaoJanela(minimizeWindowLabel, "—", 1170);
        ConfigurarBotaoJanela(maximizeWindowLabel, "□", 1204);
        ConfigurarBotaoJanela(closeWindowLabel, "✕", 1238);
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

        rootLayout.ColumnCount = 2;
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300F));
        rootLayout.Controls.Add(contextoCard, 0, 0);
        rootLayout.Controls.Add(gridCard, 0, 1);
        rootLayout.Controls.Add(lateralCard, 1, 0);
        rootLayout.Dock = DockStyle.Fill;
        rootLayout.Padding = new Padding(16, 12, 16, 8);
        rootLayout.RowCount = 2;
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 174F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.SetRowSpan(lateralCard, 2);

        ConfigurarCard(contextoCard);
        contextoCard.Margin = new Padding(0, 0, 12, 10);
        contextoCard.Controls.AddRange(new Control[] { contextoTitleLabel, opCaptionLabel, opValueLabel, itemCaptionLabel, itemValueLabel, produtoCaptionLabel, produtoValueLabel, sequenciaCaptionLabel, sequenciaValueLabel, operacaoCaptionLabel, operacaoValueLabel, descricaoOperacaoCaptionLabel, descricaoOperacaoValueLabel, centroTrabalhoCaptionLabel, centroTrabalhoValueLabel, usuarioCaptionLabel, usuarioValueLabel, estacaoCaptionLabel, estacaoValueLabel, inicioCaptionLabel, inicioValueLabel });
        contextoTitleLabel.AutoSize = true;
        contextoTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        contextoTitleLabel.ForeColor = CorTextoSuave;
        contextoTitleLabel.Location = new Point(18, 14);
        contextoTitleLabel.Text = "CONTEXTO DA OPERAÇÃO";
        ConfigurarParCampo(opCaptionLabel, opValueLabel, "OP", 18, 44, 130);
        ConfigurarParCampo(itemCaptionLabel, itemValueLabel, "ITEM", 168, 44, 90);
        ConfigurarParCampo(produtoCaptionLabel, produtoValueLabel, "PRODUTO", 278, 44, 150);
        ConfigurarParCampo(sequenciaCaptionLabel, sequenciaValueLabel, "SEQUÊNCIA", 448, 44, 110);
        ConfigurarParCampo(operacaoCaptionLabel, operacaoValueLabel, "OPERAÇÃO", 578, 44, 110);
        ConfigurarParCampo(centroTrabalhoCaptionLabel, centroTrabalhoValueLabel, "CENTRO TRAB.", 708, 44, 140);
        ConfigurarParCampo(descricaoOperacaoCaptionLabel, descricaoOperacaoValueLabel, "DESCRIÇÃO DA OPERAÇÃO", 18, 106, 420);
        ConfigurarParCampo(usuarioCaptionLabel, usuarioValueLabel, "USUÁRIO", 458, 106, 150);
        ConfigurarParCampo(estacaoCaptionLabel, estacaoValueLabel, "ESTAÇÃO", 628, 106, 150);
        ConfigurarParCampo(inicioCaptionLabel, inicioValueLabel, "INÍCIO", 798, 106, 140);

        ConfigurarCard(gridCard);
        gridCard.Margin = new Padding(0, 0, 12, 0);
        gridCard.Padding = new Padding(12, 42, 12, 12);
        gridCard.Controls.Add(resultadosGridView);
        gridCard.Controls.Add(gridTitleLabel);
        gridTitleLabel.AutoSize = true;
        gridTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        gridTitleLabel.ForeColor = CorTextoSuave;
        gridTitleLabel.Location = new Point(18, 15);
        gridTitleLabel.Text = "RESULTADOS DA OPERAÇÃO";
        resultadosGridView.AllowUserToAddRows = false;
        resultadosGridView.AllowUserToDeleteRows = false;
        resultadosGridView.AllowUserToResizeRows = false;
        resultadosGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        resultadosGridView.BackgroundColor = Color.White;
        resultadosGridView.BorderStyle = BorderStyle.None;
        resultadosGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        resultadosGridView.ColumnHeadersHeight = 34;
        resultadosGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        resultadosGridView.Columns.AddRange(colTipo, colMedida, colReferencia, colResultado);
        resultadosGridView.Dock = DockStyle.Fill;
        resultadosGridView.EditMode = DataGridViewEditMode.EditOnEnter;
        resultadosGridView.EnableHeadersVisualStyles = false;
        resultadosGridView.MultiSelect = false;
        resultadosGridView.RowHeadersVisible = false;
        resultadosGridView.RowTemplate.Height = 32;
        resultadosGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        resultadosGridView.TabIndex = 0;
        colTipo.HeaderText = "Tipo";
        colTipo.Name = "colTipo";
        colTipo.SortMode = DataGridViewColumnSortMode.NotSortable;
        colMedida.HeaderText = "Medida";
        colMedida.Name = "colMedida";
        colMedida.SortMode = DataGridViewColumnSortMode.NotSortable;
        colReferencia.HeaderText = "Referência";
        colReferencia.Name = "colReferencia";
        colReferencia.SortMode = DataGridViewColumnSortMode.NotSortable;
        colResultado.HeaderText = "Resultado";
        colResultado.Name = "colResultado";
        colResultado.SortMode = DataGridViewColumnSortMode.NotSortable;

        lateralCard.Name = "lateralCard";
        ConfigurarCard(lateralCard);
        lateralCard.Controls.AddRange(new Control[] { lateralTitleLabel, statusCaptionLabel, statusValueLabel, atalhoCaptionLabel, atalhoValueLabel, instrucaoPanel, acoesPanel });
        lateralCard.Margin = new Padding(0);
        lateralTitleLabel.AutoSize = true;
        lateralTitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lateralTitleLabel.ForeColor = CorTextoSuave;
        lateralTitleLabel.Location = new Point(18, 14);
        lateralTitleLabel.Text = "STATUS DO RESULTADO";
        ConfigurarParCampo(statusCaptionLabel, statusValueLabel, "SITUAÇÃO", 18, 54, 250);
        statusValueLabel.Name = "statusValueLabel";
        statusValueLabel.Text = "AGUARDANDO RESULTADO";
        statusValueLabel.ForeColor = Color.FromArgb(217, 119, 6);
        ConfigurarParCampo(atalhoCaptionLabel, atalhoValueLabel, "ATALHOS", 18, 118, 250);
        atalhoValueLabel.Name = "atalhoValueLabel";
        atalhoValueLabel.Text = "F10 gravar · Esc fechar";
        instrucaoPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        instrucaoPanel.BorderColor = Color.FromArgb(226, 232, 240);
        instrucaoPanel.BorderRadius = 8;
        instrucaoPanel.Controls.Add(instrucaoLabel);
        instrucaoPanel.FillColor = Color.FromArgb(243, 244, 246);
        instrucaoPanel.Location = new Point(14, 190);
        instrucaoPanel.Size = new Size(272, 130);
        instrucaoLabel.Name = "instrucaoLabel";
        instrucaoLabel.Dock = DockStyle.Fill;
        instrucaoLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        instrucaoLabel.ForeColor = CorTextoSuave;
        instrucaoLabel.Padding = new Padding(10);
        instrucaoLabel.Text = "Digite o resultado da operação e pressione F10 para gravar localmente.";
        instrucaoLabel.TextAlign = ContentAlignment.TopLeft;
        acoesPanel.Name = "acoesPanel";
        acoesPanel.BackColor = Color.Transparent;
        acoesPanel.Controls.Add(acoesTable);
        acoesPanel.Dock = DockStyle.Bottom;
        acoesPanel.Height = 116;
        acoesPanel.Padding = new Padding(14, 10, 14, 14);
        acoesTable.Name = "acoesTable";
        acoesTable.ColumnCount = 1;
        acoesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        acoesTable.Controls.Add(gravarResultadoButton, 0, 0);
        acoesTable.Controls.Add(fecharButton, 0, 1);
        acoesTable.Dock = DockStyle.Fill;
        acoesTable.RowCount = 2;
        acoesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
        acoesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
        gravarResultadoButton.Name = "gravarResultadoButton";
        gravarResultadoButton.BackColor = CorAcento;
        gravarResultadoButton.Dock = DockStyle.Fill;
        gravarResultadoButton.FlatAppearance.BorderSize = 0;
        gravarResultadoButton.FlatStyle = FlatStyle.Flat;
        gravarResultadoButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        gravarResultadoButton.ForeColor = Color.White;
        gravarResultadoButton.Margin = new Padding(0, 0, 0, 8);
        gravarResultadoButton.MinimumSize = new Size(0, 40);
        gravarResultadoButton.TabIndex = 1;
        gravarResultadoButton.Text = "GRAVAR RESULTADO  F10";
        gravarResultadoButton.UseVisualStyleBackColor = false;
        fecharButton.Name = "fecharButton";
        fecharButton.BackColor = Color.White;
        fecharButton.Dock = DockStyle.Fill;
        fecharButton.FlatAppearance.BorderColor = CorBorda;
        fecharButton.FlatStyle = FlatStyle.Flat;
        fecharButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
        fecharButton.ForeColor = CorTextoForte;
        fecharButton.Margin = new Padding(0);
        fecharButton.MinimumSize = new Size(0, 38);
        fecharButton.TabIndex = 2;
        fecharButton.Text = "FECHAR  Esc";
        fecharButton.UseVisualStyleBackColor = false;

        footerPanel.Name = "footerPanel";
        footerPanel.BackColor = Color.White;
        footerPanel.Controls.Add(statusLabel);
        footerPanel.Dock = DockStyle.Bottom;
        footerPanel.Size = new Size(1280, 34);
        statusLabel.Name = "statusLabel";
        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        statusLabel.ForeColor = CorTextoSuave;
        statusLabel.Padding = new Padding(20, 0, 0, 0);
        statusLabel.Text = "Aguardando preenchimento do resultado.";
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;

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
        Name = "ProcessoResultadoApontamentoForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Resultado do Apontamento";
        ResumeLayout(false);
    }

    private static void ConfigurarCard(RoundedPanel card)
    {
        card.BackColor = Color.Transparent;
        card.BorderColor = CorBorda;
        card.BorderRadius = 10;
        card.Dock = DockStyle.Fill;
        card.FillColor = Color.White;
        card.ShadowBlur = 0;
        card.ShadowOffsetY = 0;
    }

    private static void ConfigurarParCampo(Label caption, Label valor, string titulo, int x, int y, int largura)
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

    private static void ConfigurarBotaoJanela(Label label, string texto, int x)
    {
        label.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        label.BackColor = Color.Transparent;
        label.Cursor = Cursors.Hand;
        label.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        label.ForeColor = Color.FromArgb(156, 163, 175);
        label.Location = new Point(x, 22);
        label.Size = new Size(32, 32);
        label.Text = texto;
        label.TextAlign = ContentAlignment.MiddleCenter;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void HeaderPanel_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }
}







