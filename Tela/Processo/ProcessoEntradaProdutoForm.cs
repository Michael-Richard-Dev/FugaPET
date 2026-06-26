using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Tela.Teste;
using FugaPET_Dev.Tela;
using FugaPET_Dev.Tela.Comum;

using System.Runtime.InteropServices;

namespace FugaPET_Dev.Tela.Processo;

public partial class ProcessoEntradaProdutoForm : Form
{
    private enum EstadoVisualLocalEntrada
    {
        Pendente,
        Gravado
    }

    private enum EstadoVisualIntegracaoSap
    {
        AguardandoGravacaoLocal,
        BloqueadoSemPermissao,
        BloqueadoAmbiente,
        BloqueadoSapNaoConfigurado,
        BloqueadoIntegracaoInativa,
        BloqueadoEscritaDesabilitada,
        BloqueadoSemItens,
        LiberadoParaEnvio,
        Enviando,
        Enviado,
        Falha,
        Parcial
    }

    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
    private const string TipoPedidoNormal = "NB";
    private const string DescricaoPedidoNormal = "Pedido normal (NB)";
    private const string ColunaReimpressaoEtiqueta = "productionReprintColumn";
    private static readonly Color RowGreen = Color.FromArgb(238, 241, 245);
    private static readonly Color RowLight = Color.FromArgb(250, 251, 252);
    private static readonly Color StartActionHoverBorder = Color.FromArgb(34, 197, 94);
    private static readonly Color ReadWeightHoverBorder = Color.FromArgb(59, 130, 246);
    private static readonly Color DangerActionHoverBorder = Color.FromArgb(229, 27, 43);
    private static readonly Color EnabledLegendTextColor = Color.FromArgb(229, 231, 235);
    private static readonly Color DisabledLegendTextColor = Color.FromArgb(120, 126, 136);
    private static readonly Color SidePanelDefaultColor = Color.FromArgb(45, 49, 56);
    private static readonly Color SidePanelActiveColor = Color.FromArgb(22, 101, 52);
    private static readonly Color ActionEnabledColor = Color.FromArgb(34, 166, 82);
    private static readonly Color ActionDisabledColor = Color.FromArgb(82, 87, 96);
    private static readonly Color ReadingStatusInactiveColor = Color.FromArgb(220, 53, 69);
    private static readonly Color ReadingStatusActiveColor = Color.FromArgb(34, 166, 82);
    private readonly BalancaLeituraServico _balancaLeituraServico;
    private readonly ImpressaoEntradaServico _impressaoEntrada;
    private bool _isStartActionHovering;
    private bool _isReadWeightHovering;
    private Panel? _hoveredDangerActionPanel;
    private bool _isProductionStarted;
    private bool _finalizandoPesagem;
    private bool _isReadingWeight;
    private int _nextProductionCode = 1;
    private Task? _productionDevicesWarmUpTask;
    private readonly SemaphoreSlim _consultaPedidoGate = new(1, 1);
    private CancellationTokenSource? _consultaPedidoCts;
    private Task _consultaPedidoTask = Task.CompletedTask;
    private string _numeroPedidoCarregado = string.Empty;
    private ContextoTerminalLocal? _contextoTerminal;
    private long? _idSetorSelecionado;
    private long? _idBalancaSelecionada;
    private long? _idTaraSelecionada;
    private long? _idEtiquetaSelecionada;

    // H9: os servicos vem do controller (Form nao instancia servicos concretos nem fala direto com
    // o SAP). A escolha mock/real e da fabrica, encapsulada no controller/IntegracaoEntradaSapServico.
    private readonly global::FugaPET_Dev.Controle.Processo.EntradaProdutoController _controller;
    private readonly EntradaProdutoServico _entradaServico;
    private readonly Dictionary<long, PedidoCompraSapItem> _itensCarregadosPorCodigo = [];
    private readonly Dictionary<long, List<EntradaProdutoPesagem>> _leiturasPorItem = [];
    private long? _codigoLancamentoPersistido;
    private readonly CancellationTokenSource _fechamentoTelaCts = new();
    private readonly global::FugaPET_Dev.Controle.Cadastro.TaraController _taraController;
    private Task _envioSapTask = Task.CompletedTask;
    private readonly ToolTip _envioSapToolTip = new();
    private bool _acessoDiretoValidado;

    public ProcessoEntradaProdutoForm()
        : this(new global::FugaPET_Dev.Controle.Processo.EntradaProdutoController())
    {
    }

    internal ProcessoEntradaProdutoForm(global::FugaPET_Dev.Controle.Processo.EntradaProdutoController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _entradaServico = _controller.EntradaProduto;
        _balancaLeituraServico = _controller.BalancaLeitura;
        _impressaoEntrada = _controller.Impressao;
        _taraController = _controller.Tara;
        InitializeComponent();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        if (_controller.Sap.EhSimulado)
        {
            if (!global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.PodeUsarDadosSimulados())
            {
                global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.BloquearTelaSimulada(this);
                return;
            }

            global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.Aplicar(headerSubtitleLabel);
            global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.AplicarFaixa(this);
        }
        AplicarContextoTerminalAutomatico();
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "aguardando gravação local");
        ConfigureResponsiveSummaryCards();
        ConfigureProductionSearchBox();
        ConfigurarComboPedidos();
        ConfigureSideActionButtonIcons();
        if (_controller.Sap.EhSimulado)
        {
            LoadMockData();
        }
        ApplyGridStyle(materialDataGridView);
        ApplyGridStyle(productionDataGridView);
        ConfigurarColunaReimpressaoEtiqueta();
        ConfigureProductionGridFooter();
        productionDataGridView.CellMouseDown += ProductionDataGridView_CellMouseDown;
        productionDataGridView.CellClick += ProductionDataGridView_CellClick;
        productionDataGridView.SelectionChanged += ProductionDataGridView_SelectionChanged;
        productionDataGridView.CellDoubleClick += ProductionDataGridView_CellDoubleClick;
        ConfigureStartActionHoverEffect();
        ConfigureProductionActions();
        ConfigureFooterDate();
        UpdateProductionCounters();
        KeyPreview = true;
        Shown += ProcessoProdutoAcabadoForm_Shown;
        FormClosing += ProcessoProdutoAcabadoForm_FormClosing;
        FormClosed += (_, _) =>
        {
            _consultaPedidoCts?.Cancel();
            _fechamentoTelaCts.Cancel();
        };
    }

    private void AplicarContextoTerminalAutomatico()
    {
        try
        {
            _contextoTerminal = EstadoTerminalLocalAtual.Contexto;
        }
        catch
        {
            _contextoTerminal = null;
        }

        long? idSetorUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdSetorPadrao;
        _idSetorSelecionado = idSetorUsuario ?? _contextoTerminal?.IdSetorPadrao;
        _idBalancaSelecionada = _contextoTerminal?.IdBalancaPadrao;
        _idTaraSelecionada = _contextoTerminal?.IdTaraPadrao;
        _idEtiquetaSelecionada = _contextoTerminal?.IdEtiquetaPadrao;

        string nomeTerminal = string.IsNullOrWhiteSpace(_contextoTerminal?.NomeTerminal)
            ? Environment.MachineName
            : _contextoTerminal!.NomeTerminal;
        cellTerminalText.Text = $"Terminal:  {nomeTerminal}";

        if (_idBalancaSelecionada.HasValue)
        {
            balanceTextBox.Text = _idBalancaSelecionada.Value.ToString();
        }

        statusLabel.Text = MontarResumoSelecaoAutomatica();
    }

    private string MontarResumoSelecaoAutomatica()
    {
        return
            $"Selecao automatica -> Setor: {FormatarId(_idSetorSelecionado)}, " +
            $"Balanca: {FormatarId(_idBalancaSelecionada)}, " +
            $"Tara: {FormatarId(_idTaraSelecionada)}, " +
            $"Etiqueta: {FormatarId(_idEtiquetaSelecionada)}.";
    }

    private static string FormatarId(long? valor)
    {
        return valor.HasValue ? valor.Value.ToString() : "Nao definido";
    }

    private static bool PossuiPermissaoEntrada(string acao)
        => AutorizacaoEntradaProdutoServico.PossuiPermissao(acao);

    // 1) verifica permissao; 2) se negado, AUDITA (await, seguro); 3) mostra mensagem amigavel;
    // 4) retorna true para o chamador abortar a acao.
    private async Task<bool> BloquearAcaoSemPermissaoAsync(string acao, string descricaoAcao)
    {
        if (PossuiPermissaoEntrada(acao))
        {
            return false;
        }

        await global::FugaPET_Dev.Tela.Comum.AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            AutorizacaoServico.ModuloProcesso, PermissoesSistema.Rotinas.EntradaProduto, acao, descricaoAcao, "ProcessoEntradaProdutoForm");

        string mensagem = $"Usuario sem permissao para {descricaoAcao.ToLowerInvariant()} na entrada de produto.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private async Task<bool> BloquearImpressaoSemPermissaoAsync(
        string acao,
        string descricaoAcao)
    {
        if (AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(acao))
        {
            return false;
        }

        await global::FugaPET_Dev.Tela.Comum.AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            PermissoesSistema.Modulos.Etiqueta,
            PermissoesSistema.Rotinas.ImpressaoEtiqueta,
            acao,
            descricaoAcao,
            "ProcessoEntradaProdutoForm");

        string mensagem = $"Usuario sem permissao para {descricaoAcao.ToLowerInvariant()}.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private System.Windows.Forms.Timer? _footerClockTimer;

    private void ConfigureFooterDate()
    {
        UpdateFooterDateTime();

        _footerClockTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _footerClockTimer.Tick += (_, _) => UpdateFooterDateTime();
        _footerClockTimer.Start();
    }

    private void UpdateFooterDateTime()
    {
        var ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        DateTime now = DateTime.Now;
        cellDataText.Text = now.ToString("dd/MM/yyyy", ptBr);
        cellHoraText.Text = now.ToString("HH:mm", ptBr);
    }

    private void UpdateStepCardDate(DateTime now, System.Globalization.CultureInfo culture)
    {
        stepLabel.Text = now.ToString("dd/MM/yyyy", culture);
    }


    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void ConfigureCustomTitleBar()
    {
        customTitleBarPanel.MouseDown += CustomTitleBar_MouseDown;
        companyLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;
        menuHeaderLabel.Click += (_, _) => ReturnToLeituraProducao();

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));
    }

    private void AtualizarEstadoVisualLocal(EstadoVisualLocalEntrada estado, string detalhe)
    {
        statusLabel.ForeColor = estado == EstadoVisualLocalEntrada.Gravado
            ? Color.FromArgb(34, 166, 82)
            : Color.FromArgb(180, 83, 9);
        statusLabel.Text = estado == EstadoVisualLocalEntrada.Gravado
            ? $"LOCAL GRAVADO — {detalhe}"
            : $"LOCAL PENDENTE — {detalhe}";
    }

    private void AtualizarEstadoVisualIntegracaoSap(
        EstadoVisualIntegracaoSap estado,
        string? detalhe = null)
    {
        (string texto, Color cor) = estado switch
        {
            EstadoVisualIntegracaoSap.LiberadoParaEnvio =>
                ("SAP HML: LIBERADO PARA ENVIO", Color.FromArgb(34, 197, 94)),
            EstadoVisualIntegracaoSap.Enviando =>
                ("SAP HML: ENVIANDO", Color.FromArgb(59, 130, 246)),
            EstadoVisualIntegracaoSap.Enviado =>
                ("SAP HML: ENVIADO", Color.FromArgb(34, 197, 94)),
            EstadoVisualIntegracaoSap.Falha =>
                ("SAP HML: FALHA", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.Parcial =>
                ("SAP HML: PARCIAL", Color.FromArgb(249, 115, 22)),
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal =>
                ("SAP HML: AGUARDANDO GRAVAÇÃO LOCAL", Color.FromArgb(250, 204, 21)),
            _ =>
                ("SAP HML: BLOQUEADO", Color.FromArgb(239, 68, 68))
        };

        sapStatusDotLabel.ForeColor = cor;
        sapStatusLabel.Text = string.IsNullOrWhiteSpace(detalhe)
            ? texto
            : $"{texto} — {detalhe}";
        sapStatusLabel.AutoSize = false;
    }

    private void ReturnToLeituraProducao()
    {
        if (_isProductionStarted)
        {
            statusLabel.Text = "Finalize a leitura antes de sair da tela.";
            return;
        }

        if (Owner is PainelInicialForm)
        {
            Close();
            return;
        }

        PainelInicialForm painel = new();
        painel.NavigateToProcessoProducao();
        painel.Show();
        Close();
    }

    private void CustomTitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private void ToggleWindowState()
    {
        if (_isProductionStarted)
        {
            return;
        }

        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void ProcessoProdutoAcabadoForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isProductionStarted)
        {
            return;
        }

        e.Cancel = true;
        statusLabel.Text = "Finalize a leitura antes de sair da tela.";
    }

    private bool PodeAtualizarTela()
        => !IsDisposed && !Disposing && !_fechamentoTelaCts.IsCancellationRequested;

    private static void ConfigureTitleButtonHover(Label button, Color hoverColor)
    {
        Color normalColor = button.BackColor;

        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = normalColor;
    }

    private void ConfigureResponsiveSummaryCards()
    {
        tableLayoutPanel6.Resize += (_, _) => AlignDateCardLayout(null, EventArgs.Empty);
        tableLayoutPanel8.Resize += (_, _) => AlignPlannedProductionCardLayout(null, EventArgs.Empty);
        Shown += (_, _) => RefreshSummaryCardDividers();
        Layout += (_, _) => RefreshSummaryCardDividers();

        RefreshSummaryCardDividers();
    }

    private void RefreshSummaryCardDividers()
    {
        AlignDateCardLayout(null, EventArgs.Empty);
        AlignPlannedProductionCardLayout(null, EventArgs.Empty);
    }

    private void AlignDateCardLayout(object? sender, EventArgs e)
    {
        int dividerTop = tableLayoutPanel6.Top + 11;
        int dividerHeight = Math.Max(18, tableLayoutPanel6.Height - 14);

        AlignDateDividerBetweenColumns(dateDividerLabel1, 1, ovenExitCaptionLabel, ovenExitTextBox, classificationDateCaptionLabel, classificationDateTextBox, dividerTop, dividerHeight);
        AlignDateDividerBetweenColumns(dateDividerLabel2, 2, classificationDateCaptionLabel, classificationDateTextBox, manufacturingDateCaptionLabel, manufacturingDateTextBox, dividerTop, dividerHeight);
        AlignDateDividerBetweenColumns(dateDividerLabel3, 3, manufacturingDateCaptionLabel, manufacturingDateTextBox, expirationDateCaptionLabel, expirationDateTextBox, dividerTop, dividerHeight);
    }

    private void AlignPlannedProductionCardLayout(object? sender, EventArgs e)
    {
        int dividerTop = tableLayoutPanel8.Top + 13;
        int dividerHeight = Math.Max(18, tableLayoutPanel8.Height - 17);

        AlignDividerBetweenColumns(plannedProductionDividerLabel1, readForecastBoxesCaptionLabel, readForecastBoxesTextBox, readForecastPackagesCaptionLabel, readForecastPackagesTextBox, dividerTop, dividerHeight);
        AlignDividerBetweenColumns(plannedProductionDividerLabel2, readForecastPackagesCaptionLabel, readForecastPackagesTextBox, balanceCaptionLabel, balanceTextBox, dividerTop, dividerHeight);
    }

    private void AlignDateDividerBetweenColumns(Label divider, int columnBoundaryIndex, Control previousCaption, Control previousValue, Control nextCaption, Control nextValue, int top, int height)
    {
        int tableLeft = tableLayoutPanel6.Left;
        int previousRight = tableLeft + Math.Max(GetVisibleTextRight(previousCaption), GetVisibleTextRight(previousValue));
        int nextLeft = tableLeft + Math.Min(GetVisibleTextLeft(nextCaption), GetVisibleTextLeft(nextValue));
        int columnBoundary = tableLeft + (tableLayoutPanel6.Width * columnBoundaryIndex / 4);

        int dividerX = nextLeft - previousRight >= 24
            ? previousRight + ((nextLeft - previousRight) / 2)
            : columnBoundary - 10;

        divider.Location = new Point(dividerX, top);
        divider.Size = new Size(1, height);
    }

    private void AlignDividerBetweenColumns(Label divider, Control previousCaption, Control previousValue, Control nextCaption, Control nextValue, int top, int height)
    {
        int previousRight = Math.Max(GetVisibleTextRight(previousCaption), GetVisibleTextRight(previousValue));
        int nextLeft = Math.Min(GetVisibleTextLeft(nextCaption), GetVisibleTextLeft(nextValue));

        int dividerX = previousRight < nextLeft
            ? previousRight + ((nextLeft - previousRight) / 2)
            : nextLeft - 12;

        divider.Location = new Point(dividerX, top);
        divider.Size = new Size(1, height);
    }

    private static int GetVisibleTextLeft(Control control)
    {
        return control.Left + control.Margin.Left;
    }

    private static int GetVisibleTextRight(Control control)
    {
        Size textSize = TextRenderer.MeasureText(control.Text, control.Font, control.Size, TextFormatFlags.NoPadding);
        return control.Left + control.Margin.Left + textSize.Width;
    }

    private void ConfigureProductionSearchBox()
    {
        Color searchBackColor = Color.FromArgb(248, 250, 252);
        Color searchIconColor = Color.FromArgb(148, 163, 184);

        productionSearchPanel.FillColor = searchBackColor;
        productionSearchTextBox.BackColor = searchBackColor;
        productionSearchGlyphLabel.ForeColor = searchIconColor;
        productionSearchGlyphLabel.Cursor = Cursors.IBeam;
        productionSearchGlyphLabel.Click += (_, _) => productionSearchTextBox.Focus();
        productionSearchPanel.Click += (_, _) => productionSearchTextBox.Focus();
        productionSearchIconPictureBox.BackColor = searchBackColor;
        productionSearchIconPictureBox.Image = CreateTintedIcon(global::FugaPET_Dev.Properties.Resources.search_red, searchIconColor);
        productionSearchIconPictureBox.Cursor = Cursors.IBeam;
        productionSearchIconPictureBox.Click += (_, _) => productionSearchTextBox.Focus();
    }

    private void ConfigureSideActionButtonIcons()
    {
        lerEtiquetaButton.IconGlyph = string.Empty;
        lerEtiquetaButton.IconImage = CreateTintedIcon(
            global::FugaPET_Dev.Properties.Resources.read_weight,
            Color.FromArgb(17, 24, 39));
    }

    private void ConfigureProductionGridFooter()
    {
        productionDataGridView.RowsAdded += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.RowsRemoved += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.Scroll += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.SizeChanged += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.DataBindingComplete += (_, _) => UpdateProductionGridFooter();
        Shown += (_, _) => BeginInvoke(UpdateProductionGridFooter);

        UpdateProductionGridFooter();
    }

    private void ConfigurarColunaReimpressaoEtiqueta()
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(
                PermissoesSistema.Acoes.Reimprimir))
        {
            return;
        }

        if (productionDataGridView.Columns.Contains(ColunaReimpressaoEtiqueta))
        {
            return;
        }

        DataGridViewButtonColumn colunaReimpressao = new()
        {
            Name = ColunaReimpressaoEtiqueta,
            HeaderText = "Imprimir",
            Text = "🖨",
            ToolTipText = "Reimprimir etiqueta",
            UseColumnTextForButtonValue = true,
            ReadOnly = true,
            Width = 72,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        colunaReimpressao.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colunaReimpressao.DefaultCellStyle.Font = new Font("Segoe UI Emoji", 10F, FontStyle.Regular);
        productionDataGridView.Columns.Insert(5, colunaReimpressao);
    }

    private void UpdateProductionGridFooter()
    {
        int totalRows = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Count(row => !row.IsNewRow);

        if (totalRows == 0)
        {
            productionFooterLabel.Text = "Exibindo 0 de 0 itens";
            productionPageLabel.Text = "Pagina 1 de 1";
            productionPageTextBox.Text = "1";
            productionPreviousPageButton.Enabled = false;
            productionNextPageButton.Enabled = false;
            return;
        }

        int firstVisibleRow = GetFirstVisibleProductionRowIndex();
        int visibleRows = Math.Max(1, productionDataGridView.DisplayedRowCount(false));
        int firstItem = Math.Min(totalRows, firstVisibleRow + 1);
        int lastItem = Math.Min(totalRows, firstVisibleRow + visibleRows);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)visibleRows));
        int currentPage = Math.Min(totalPages, (firstVisibleRow / visibleRows) + 1);

        productionFooterLabel.Text = $"Exibindo {firstItem} a {lastItem} de {totalRows} itens";
        productionPageLabel.Text = $"Pagina {currentPage} de {totalPages}";
        productionPageTextBox.Text = currentPage.ToString();
        productionPreviousPageButton.Enabled = currentPage > 1;
        productionNextPageButton.Enabled = currentPage < totalPages;
    }

    private int GetFirstVisibleProductionRowIndex()
    {
        try
        {
            return productionDataGridView.FirstDisplayedScrollingRowIndex >= 0
                ? productionDataGridView.FirstDisplayedScrollingRowIndex
                : 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static Bitmap CreateTintedIcon(Bitmap source, Color tintColor)
    {
        Bitmap tintedIcon = new(source.Width, source.Height);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                tintedIcon.SetPixel(x, y, Color.FromArgb(pixel.A, tintColor));
            }
        }

        return tintedIcon;
    }

    private void ConfigureProductionActions()
    {
        startActionPanel.Click += StartProduction_Click;
        startActionIconLabel.Click += StartProduction_Click;
        startActionTextLabel.Click += StartProduction_Click;

        readWeightLegendPanel.Click += ReadWeightLegend_Click;
        readWeightLegendIconLabel.Click += ReadWeightLegend_Click;
        readWeightLegendTextLabel.Click += ReadWeightLegend_Click;
        ConfigureReadWeightHoverEffect();

        stopActionPanel.Click += StopProduction_Click;
        stopActionIconLabel.Click += StopProduction_Click;
        stopActionTextLabel.Click += StopProduction_Click;
        ConfigureDangerActionHoverEffect(stopActionPanel, stopActionIconLabel, stopActionTextLabel);
        ConfigureDangerActionHoverEffect(deleteLastLegendPanel, deleteLastLegendIconLabel, deleteLastLegendTextLabel);
        ConfigureDangerActionHoverEffect(deleteByCodeLegendPanel, deleteByCodeLegendIconLabel, deleteByCodeLegendTextLabel);

        deleteLastLegendPanel.Click += DeleteLastProductionRow_Click;
        deleteLastLegendIconLabel.Click += DeleteLastProductionRow_Click;
        deleteLastLegendTextLabel.Click += DeleteLastProductionRow_Click;
        deleteByCodeLegendPanel.Click += DeleteProductionRowByCode_Click;
        deleteByCodeLegendIconLabel.Click += DeleteProductionRowByCode_Click;
        deleteByCodeLegendTextLabel.Click += DeleteProductionRowByCode_Click;

        // Wire new sidePanel buttons to the same handlers as the legacy ones
        iniciarLeituraButton.Click += ToggleProductionFromSideButton_Click;
        lerEtiquetaButton.Click += ReadWeightLegend_Click;
        leituraManualButton.Click += LeituraManual_Click;
        ConfigurarAcaoEnvioSapHomologacao();

        UpdateProductionState(false);
        SetReadWeightEnabled(false);
        SetDeleteActionsEnabled(false);
        UpdateProductionCounters();
    }

    private void ConfigurarAcaoEnvioSapHomologacao()
    {
        bool podeEnviarSap = PossuiPermissaoEntrada(PermissoesSistema.Acoes.EnviarSap);
        productionActionsButton.Visible = podeEnviarSap;
        productionActionsButton.Enabled = false;
        productionActionsButton.Text = "Enviar SAP HML";
        productionActionsButton.AccessibleName = "Criar movimento 101 no SAP de homologação";
        _envioSapToolTip.SetToolTip(
            productionActionsButton,
            podeEnviarSap
                ? "Finalize e grave o lançamento para validar a prontidão do envio SAP."
                : "Usuário sem permissão ENVIAR_SAP.");
        productionActionsButton.Click += EnviarSapHomologacao_Click;
    }

    private async Task AtualizarProntidaoEnvioSapAsync()
    {
        DiagnosticoEnvioSapEntrada diagnostico =
            await _controller.DiagnosticarEnvioSapEntradaAsync(
                _codigoLancamentoPersistido,
                _fechamentoTelaCts.Token);

        productionActionsButton.Visible = diagnostico.UsuarioTemPermissao;
        productionActionsButton.Enabled =
            diagnostico.PodeEnviar
            && _envioSapTask.IsCompleted
            && !_isProductionStarted;

        string motivo = diagnostico.MotivoBloqueio ?? "Lançamento apto para envio controlado.";
        _envioSapToolTip.SetToolTip(productionActionsButton, motivo);

        if (diagnostico.PodeEnviar)
        {
            AtualizarEstadoVisualIntegracaoSap(
                EstadoVisualIntegracaoSap.LiberadoParaEnvio,
                $"{diagnostico.TotalItensPersistidos} item(ns) apto(s)");
            return;
        }

        EstadoVisualIntegracaoSap estado = diagnostico.CodigoLancamento is null
            ? EstadoVisualIntegracaoSap.AguardandoGravacaoLocal
            : !diagnostico.AmbienteHomologacao
                ? EstadoVisualIntegracaoSap.BloqueadoAmbiente
                : !diagnostico.UsuarioTemPermissao
                ? EstadoVisualIntegracaoSap.BloqueadoSemPermissao
                : !diagnostico.IntegracaoSapAtiva
                    ? EstadoVisualIntegracaoSap.BloqueadoIntegracaoInativa
                    : !diagnostico.SapConfigurado
                        ? EstadoVisualIntegracaoSap.BloqueadoSapNaoConfigurado
                        : !diagnostico.EscritaSapHabilitada
                            ? EstadoVisualIntegracaoSap.BloqueadoEscritaDesabilitada
                            : EstadoVisualIntegracaoSap.BloqueadoSemItens;

        AtualizarEstadoVisualIntegracaoSap(estado, motivo);
    }

    private async void EnviarSapHomologacao_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_envioSapTask is { IsCompleted: false })
            {
                return;
            }

            _envioSapTask = EnviarSapHomologacaoAsync();
            await _envioSapTask;
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            string mensagem = await ErroUsuarioHelper.TratarAsync(
                "ENVIO_SAP_HML_ERRO",
                ex,
                "ProcessoEntradaProdutoForm",
                "Não foi possível concluir o envio controlado ao SAP de homologação.");
            AtualizarEstadoVisualIntegracaoSap(EstadoVisualIntegracaoSap.Falha);
            MessageBox.Show(
                mensagem,
                "Falha no envio SAP HML",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task EnviarSapHomologacaoAsync()
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.EnviarSap,
                "enviar lançamento ao SAP de homologação"))
        {
            return;
        }

        if (_isProductionStarted)
        {
            AtualizarEstadoVisualLocal(
                EstadoVisualLocalEntrada.Pendente,
                "finalize e grave o lançamento antes do envio SAP");
            return;
        }

        if (_codigoLancamentoPersistido is not long codigoLancamento || codigoLancamento <= 0)
        {
            AtualizarEstadoVisualLocal(
                EstadoVisualLocalEntrada.Pendente,
                "nenhum lançamento local gravado para envio");
            MessageBox.Show(
                "Finalize e grave o lançamento local antes de solicitar o envio ao SAP de homologação.",
                "Envio SAP HML indisponível",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            $"Confirma criar o movimento 101 no SAP DE HOMOLOGAÇÃO para o lançamento {codigoLancamento}?\n\n"
            + "Será criado um documento de material (entrada) vinculado ao pedido. Esta é uma etapa "
            + "separada da gravação local e só ocorre após esta confirmação.",
            "Criar movimento 101 no SAP HML",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmacao != DialogResult.Yes)
        {
            AtualizarEstadoVisualIntegracaoSap(
                EstadoVisualIntegracaoSap.LiberadoParaEnvio,
                "envio não confirmado");
            return;
        }

        productionActionsButton.Enabled = false;
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.Enviando,
            "envio autorizado em processamento");

        try
        {
            ResultadoEnvioSapEntrada resultado =
                await _controller.EnviarPesoEntradaParaSapHomologacaoAsync(
                    codigoLancamento,
                    _fechamentoTelaCts.Token);
            await ApresentarResultadoEnvioSapHomologacaoAsync(resultado);
        }
        finally
        {
            if (PodeAtualizarTela())
            {
                productionActionsButton.Enabled = false;
            }
        }
    }

    private async Task ApresentarResultadoEnvioSapHomologacaoAsync(
        ResultadoEnvioSapEntrada resultado)
    {
        switch (resultado.Cenario)
        {
            case CenarioEnvioSapEntrada.Enviado:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    $"lançamento {_codigoLancamentoPersistido} confirmado no SAP");
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.Enviado,
                    $"{resultado.Enviados} de {resultado.Total} item(ns)");
                MessageBox.Show(
                    resultado.Mensagem
                    ?? $"Documento de material criado no SAP de homologação para {resultado.Enviados} item(ns).",
                    "Documento de material criado no SAP",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                break;

            case CenarioEnvioSapEntrada.Parcial:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    "itens atualizados; lançamento mantido FINALIZADO_LOCAL");
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.Parcial,
                    $"{resultado.Enviados} de {resultado.Total} item(ns)");
                MessageBox.Show(
                    $"O SAP de homologação confirmou {resultado.Enviados} de {resultado.Total} item(ns). "
                    + "Verifique a rotina autorizada de integração antes de tentar novamente.",
                    "Envio SAP HML parcial",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioEnvioSapEntrada.Falha:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    "falha SAP registrada localmente como ERRO_SAP");
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "O SAP não criou o documento de material. "
                    + "A falha foi registrada no lançamento local.");
                break;

            case CenarioEnvioSapEntrada.SemPermissao:
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.BloqueadoSemPermissao,
                    "usuário sem permissão ENVIAR_SAP");
                break;

            case CenarioEnvioSapEntrada.AmbienteNaoHomologacao:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque o ambiente atual não é homologação.");
                break;

            case CenarioEnvioSapEntrada.EscritaDesabilitada:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque a escrita SAP está desabilitada.");
                break;

            case CenarioEnvioSapEntrada.IntegracaoInativa:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque a integração SAP está inativa.");
                break;

            case CenarioEnvioSapEntrada.SapNaoConfigurado:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque a configuração SAP está indisponível.");
                break;

            case CenarioEnvioSapEntrada.LancamentoSemItens:
                ApresentarFalhaEnvioSap(
                    "O lançamento local não possui itens persistidos elegíveis para envio.");
                break;

            case CenarioEnvioSapEntrada.MaterialDocumentNaoConfigurado:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Integração SAP Material Document não configurada.");
                break;

            case CenarioEnvioSapEntrada.UnidadeNaoSuportada:
            case CenarioEnvioSapEntrada.DadosIncompletos:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Item não elegível para criar o movimento 101 no SAP.");
                break;

            case CenarioEnvioSapEntrada.LancamentoJaConfirmadoSap:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    "lançamento já confirmado no SAP");
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Lançamento já confirmado no SAP. Reenvio bloqueado.");
                break;

            case CenarioEnvioSapEntrada.LancamentoCancelado:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Lançamento cancelado. Envio bloqueado.");
                break;

            case CenarioEnvioSapEntrada.EnvioEmProcessamento:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Envio já bloqueado ou em processamento. Aguarde a conclusão.");
                break;

            case CenarioEnvioSapEntrada.FalhaPersistenciaLocal:
                await ApresentarFalhaCriticaPersistenciaLocalAsync(resultado);
                break;

            default:
                ApresentarFalhaEnvioSap(
                    "O envio controlado ao SAP de homologação não foi concluído.");
                await AtualizarProntidaoEnvioSapAsync();
                break;
        }
    }

    private async Task ApresentarFalhaCriticaPersistenciaLocalAsync(
        ResultadoEnvioSapEntrada resultado)
    {
        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "divergência crítica entre resposta SAP e status local");
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.Falha,
            "SAP respondeu, mas o status local não foi atualizado");

        string mensagem = await ErroUsuarioHelper.TratarAsync(
            "ENVIO_SAP_STATUS_LOCAL_CRITICO",
            new InvalidOperationException(
                resultado.MensagemCritica
                ?? "Falha ao persistir o status local após resposta do SAP."),
            "ProcessoEntradaProdutoForm",
            "O SAP respondeu ao envio, mas o status local não foi atualizado. "
            + "Não repita a operação antes da análise do suporte.");

        MessageBox.Show(
            mensagem,
            "Divergência crítica SAP x FugaPET",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ApresentarFalhaEnvioSap(string mensagem)
    {
        AtualizarEstadoVisualIntegracaoSap(EstadoVisualIntegracaoSap.Falha);
        MessageBox.Show(
            mensagem,
            "Falha no envio SAP HML",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void ConfigureStartActionHoverEffect()
    {
        startActionPanel.Padding = new Padding(2);
        startActionPanel.Paint += StartActionPanel_Paint;

        startActionPanel.MouseEnter += StartActionHover_MouseEnter;
        startActionIconLabel.MouseEnter += StartActionHover_MouseEnter;
        startActionTextLabel.MouseEnter += StartActionHover_MouseEnter;

        startActionPanel.MouseLeave += StartActionHover_MouseLeave;
        startActionIconLabel.MouseLeave += StartActionHover_MouseLeave;
        startActionTextLabel.MouseLeave += StartActionHover_MouseLeave;

        startActionPanel.Resize += (_, _) => AlignStartActionChildren();
        AlignStartActionChildren();
    }

    private void AlignStartActionChildren()
    {
        const int borderInset = 2;
        const int iconSize = 24;
        const int iconLeft = 4;

        int contentHeight = Math.Max(0, startActionPanel.ClientSize.Height - (borderInset * 2));
        int iconTop = borderInset + Math.Max(0, (contentHeight - iconSize) / 2);

        startActionIconLabel.Size = new Size(iconSize, iconSize);
        startActionIconLabel.Location = new Point(iconLeft, iconTop);

        int textLeft = iconLeft + iconSize + 2;
        startActionTextLabel.Location = new Point(textLeft, borderInset);
        startActionTextLabel.Size = new Size(
            Math.Max(0, startActionPanel.ClientSize.Width - textLeft - borderInset),
            Math.Max(0, startActionPanel.ClientSize.Height - (borderInset * 2)));
        startActionTextLabel.BackColor = Color.Transparent;
        startActionTextLabel.BorderStyle = BorderStyle.None;
        startActionIconLabel.BorderStyle = BorderStyle.None;
    }

    private void StartActionHover_MouseEnter(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        _isStartActionHovering = true;
        startActionPanel.Invalidate();
    }

    private void StartActionHover_MouseLeave(object? sender, EventArgs e)
    {
        Point cursorPosition = startActionPanel.PointToClient(Cursor.Position);
        if (startActionPanel.ClientRectangle.Contains(cursorPosition))
        {
            return;
        }

        _isStartActionHovering = false;
        startActionPanel.Invalidate();
    }

    private void StartActionPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (!_isStartActionHovering || _isProductionStarted)
        {
            return;
        }

        using Pen pen = new(StartActionHoverBorder, 3);
        Rectangle border = new(1, 1, startActionPanel.ClientSize.Width - 3, startActionPanel.ClientSize.Height - 3);
        e.Graphics.DrawRectangle(pen, border);
    }

    private void ConfigureReadWeightHoverEffect()
    {
        readWeightLegendPanel.Padding = new Padding(2);
        readWeightLegendPanel.Paint += ReadWeightLegendPanel_Paint;

        readWeightLegendPanel.MouseEnter += ReadWeightHover_MouseEnter;
        readWeightLegendIconLabel.MouseEnter += ReadWeightHover_MouseEnter;
        readWeightLegendTextLabel.MouseEnter += ReadWeightHover_MouseEnter;

        readWeightLegendPanel.MouseLeave += ReadWeightHover_MouseLeave;
        readWeightLegendIconLabel.MouseLeave += ReadWeightHover_MouseLeave;
        readWeightLegendTextLabel.MouseLeave += ReadWeightHover_MouseLeave;
    }

    private void ReadWeightHover_MouseEnter(object? sender, EventArgs e)
    {
        if (!readWeightLegendPanel.Enabled || !readWeightLegendPanel.Visible)
        {
            return;
        }

        _isReadWeightHovering = true;
        readWeightLegendPanel.Invalidate();
    }

    private void ReadWeightHover_MouseLeave(object? sender, EventArgs e)
    {
        Point cursorPosition = readWeightLegendPanel.PointToClient(Cursor.Position);
        if (readWeightLegendPanel.ClientRectangle.Contains(cursorPosition))
        {
            return;
        }

        _isReadWeightHovering = false;
        readWeightLegendPanel.Invalidate();
    }

    private void ReadWeightLegendPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (!_isReadWeightHovering || !readWeightLegendPanel.Enabled || !readWeightLegendPanel.Visible)
        {
            return;
        }

        using Pen pen = new(ReadWeightHoverBorder, 3);
        Rectangle border = new(1, 1, readWeightLegendPanel.ClientSize.Width - 3, readWeightLegendPanel.ClientSize.Height - 3);
        e.Graphics.DrawRectangle(pen, border);
    }

    private void ConfigureDangerActionHoverEffect(Panel panel, Control icon, Control text)
    {
        panel.Padding = new Padding(2);
        panel.Paint += DangerActionPanel_Paint;

        panel.MouseEnter += DangerActionHover_MouseEnter;
        icon.MouseEnter += DangerActionHover_MouseEnter;
        text.MouseEnter += DangerActionHover_MouseEnter;

        panel.MouseLeave += DangerActionHover_MouseLeave;
        icon.MouseLeave += DangerActionHover_MouseLeave;
        text.MouseLeave += DangerActionHover_MouseLeave;
    }

    private void DangerActionHover_MouseEnter(object? sender, EventArgs e)
    {
        Panel? panel = GetDangerActionPanel(sender);
        if (panel is null || !panel.Enabled || !panel.Visible)
        {
            return;
        }

        if (_hoveredDangerActionPanel != panel)
        {
            _hoveredDangerActionPanel?.Invalidate();
            _hoveredDangerActionPanel = panel;
        }

        panel.Invalidate();
    }

    private void DangerActionHover_MouseLeave(object? sender, EventArgs e)
    {
        Panel? panel = GetDangerActionPanel(sender);
        if (panel is null)
        {
            return;
        }

        Point cursorPosition = panel.PointToClient(Cursor.Position);
        if (panel.ClientRectangle.Contains(cursorPosition))
        {
            return;
        }

        if (_hoveredDangerActionPanel == panel)
        {
            _hoveredDangerActionPanel = null;
            panel.Invalidate();
        }
    }

    private void DangerActionPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || _hoveredDangerActionPanel != panel || !panel.Enabled || !panel.Visible)
        {
            return;
        }

        using Pen pen = new(DangerActionHoverBorder, 3);
        Rectangle border = new(1, 1, panel.ClientSize.Width - 3, panel.ClientSize.Height - 3);
        e.Graphics.DrawRectangle(pen, border);
    }

    private Panel? GetDangerActionPanel(object? sender)
    {
        return sender switch
        {
            Panel panel => panel,
            Control { Parent: Panel panel } => panel,
            _ => null
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F9)
        {
            using TesteZebraForm printTestForm = new();
            printTestForm.ShowDialog(this);
            return true;
        }

        if (keyData == Keys.F12)
        {
            ReadWeightLegend_Click(readWeightLegendTextLabel, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F5)
        {
            ToggleProductionFromSideButton_Click(iniciarLeituraButton, EventArgs.Empty);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ToggleProductionFromSideButton_Click(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            StopProduction_Click(sender, e);
            return;
        }

        StartProduction_Click(sender, e);
    }

    private async void StartProduction_Click(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Executar,
                "executar entrada de produto"))
        {
            return;
        }

        if (!PedidoSelecionadoValido())
        {
            statusLabel.Text = "Selecione um pedido antes de iniciar a leitura.";
            MessageBox.Show(
                "Selecione um pedido antes de iniciar a leitura.",
                "Entrada de Produto",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            AtualizarDisponibilidadeInicioLeitura();
            return;
        }

        _isProductionStarted = true;
        UpdateProductionState(true);
        statusLabel.Text = "Leitura iniciada. Selecione o item e registre uma leitura.";
        StartProductionDevicesWarmUp();
    }

    private void StartProductionDevicesWarmUp()
    {
        if (_productionDevicesWarmUpTask is { IsCompleted: false })
        {
            return;
        }

        _productionDevicesWarmUpTask = WarmUpProductionDevicesAsync();
    }

    private async Task WarmUpProductionDevicesAsync()
    {
        // Ambos os metodos ja sao assincronos e tratam excecao internamente;
        // chamamos direto (sem Task.Run desnecessario empurrando para o thread pool).
        await Task.WhenAll(
            AquecerImpressoraComSegurancaAsync(),
            WarmUpSaldoSafelyAsync());
    }

    private async Task AquecerImpressoraComSegurancaAsync()
    {
        try
        {
            await _impressaoEntrada.AquecerAsync();
        }
        catch (Exception)
        {
            // A validacao com mensagem amigavel continua acontecendo ao clicar em Ler Peso.
        }
    }

    private async Task WarmUpSaldoSafelyAsync()
    {
        try
        {
            await _balancaLeituraServico.AquecerAsync();
        }
        catch (Exception)
        {
            // A validacao com mensagem amigavel continua acontecendo ao clicar em Ler Peso.
        }
    }

    private async void StopProduction_Click(object? sender, EventArgs e)
    {
        if (!_isProductionStarted || _finalizandoPesagem)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Finalizar,
                "finalizar entrada de produto"))
        {
            return;
        }

        _finalizandoPesagem = true;
        try
        {
            // Consolida enquanto o grid ainda preserva selecao, tara e metadados do item.
            await GravarPesagensAsync();
        }
        finally
        {
            _isProductionStarted = false;
            _finalizandoPesagem = false;
            UpdateProductionState(false);
        }

        // Producao ja parada: revalida a prontidao de envio para liberar o botao
        // "Enviar SAP HML". A 1a validacao acontece dentro de GravarPesagensAsync, quando
        // _isProductionStarted ainda era true, o que mantinha o botao desabilitado mesmo
        // com o estado visual "LIBERADO PARA ENVIO".
        if (_codigoLancamentoPersistido is not null)
        {
            await AtualizarProntidaoEnvioSapAsync();
        }
    }

    // Captura as leituras do grid e delega somente a persistencia LOCAL ao controller.
    // O envio SAP HML permanece uma acao separada, explicita e confirmada.
    private async Task GravarPesagensAsync()
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            statusLabel.Text = "Producao parada.";
            return;
        }

        EntradaProdutoLancamento lancamento = MontarLancamentoDoGrid();
        if (lancamento.Itens.Count == 0 && ExistePesoVisualSemLeituraRastreavel())
        {
            AtualizarEstadoVisualLocal(
                EstadoVisualLocalEntrada.Pendente,
                "peso visual sem leitura rastreável");
            MessageBox.Show(
                "Há peso visual na grade, mas não há leitura rastreável vinculada. Refaça a leitura.",
                "Finalização da pesagem",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ResultadoFinalizacaoEntrada resultado =
            await _controller.FinalizarLeituraAsync(lancamento, _fechamentoTelaCts.Token);
        await ApresentarResultadoFinalizacaoAsync(resultado);
    }

    // Le o grid de producao e monta o lancamento (captura de selecao da tela).
    private EntradaProdutoLancamento MontarLancamentoDoGrid()
    {
        List<EntradaProdutoItem> itensLancamento = [];
        string numeroPedido = pedidoComboBox.Text.Trim();
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (!long.TryParse(GetCellValue(row, "productionItemIdColumn"), out long codigoItem) || codigoItem <= 0)
            {
                continue;
            }

            IReadOnlyList<EntradaProdutoPesagem> leituras = ObterLeiturasItem(codigoItem);
            if (leituras.Count == 0)
            {
                continue;
            }

            if (!LinhaPossuiTaraSelecionada(row, out _))
            {
                continue;
            }

            decimal pesoLiquidoTotal = EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(leituras);
            _itensCarregadosPorCodigo.TryGetValue(codigoItem, out PedidoCompraSapItem? itemCarregado);
            itensLancamento.Add(new EntradaProdutoItem
            {
                CodigoSapPedidoCompraItem = codigoItem,
                NumeroItem = GetCellValue(row, "productionNumeroItemColumn"),
                Material = GetCellValue(row, "productionCodeColumn"),
                Centro = itemCarregado?.Centro,
                Deposito = itemCarregado?.Deposito,
                Unidade = GetCellValue(row, "productionWeightColumn"),
                QuantidadePrevista = itemCarregado?.Quantidade,
                QuantidadeRecebida = pesoLiquidoTotal,
                Pesagens = leituras
            });
        }

        return new EntradaProdutoLancamento
        {
            NumeroPedido = numeroPedido,
            Fornecedor = lotTextBox.Text.Trim(),
            CodigoSetor = _idSetorSelecionado,
            Terminal = ObterNomeTerminalAtual(),
            Itens = itensLancamento
        };
    }

    private bool ExistePesoVisualSemLeituraRastreavel()
    {
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (!TryParsePesoKg(GetCellValue(row, "productionPesoLidoColumn"), out decimal pesoVisual)
                || pesoVisual <= 0m)
            {
                continue;
            }

            if (!long.TryParse(GetCellValue(row, "productionItemIdColumn"), out long codigoItem)
                || codigoItem <= 0
                || ObterLeiturasItem(codigoItem).Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    // Apresenta o resultado da finalizacao (somente UI: status + dialogo).
    private async Task ApresentarResultadoFinalizacaoAsync(ResultadoFinalizacaoEntrada resultado)
    {
        switch (resultado.Cenario)
        {
            case CenarioFinalizacaoEntrada.NenhumaLeitura:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Pendente,
                    "nenhuma leitura para gravar");
                MessageBox.Show(
                    "Nenhuma leitura foi registrada. Clique em Iniciar Leitura e use Ler Peso, Leitura Manual ou Pesagem Múltipla antes de finalizar.",
                    "Finalização da pesagem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioFinalizacaoEntrada.LancamentoNaoGravado:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Pendente,
                    "lançamento não foi gravado");
                MessageBox.Show(
                    resultado.MensagemFalhaLancamento ?? string.Empty,
                    "Lancamento nao gravado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioFinalizacaoEntrada.GravadoLocal:
                // C12: a finalizacao grava SOMENTE local; o envio ao SAP e um passo separado.
                _codigoLancamentoPersistido = resultado.CodigoLancamento;
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    $"lançamento {resultado.CodigoLancamento} com {resultado.Gravados} item(ns)");
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
                    "validando prontidão do envio");
                await AtualizarProntidaoEnvioSapAsync();
                MessageBox.Show(
                    $"Lançamento local {resultado.CodigoLancamento} gravado com sucesso "
                    + $"com {resultado.Gravados} item(ns).\n\n"
                    + "O envio ao SAP deve ser executado pela rotina autorizada de integração em homologação.",
                    "Lançamento local gravado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                break;

            case CenarioFinalizacaoEntrada.ErroAoGravar:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Pendente,
                    "não foi possível gravar as pesagens");
                break;
        }
    }

    private static bool TryParsePesoKg(string texto, out decimal peso)
    {
        peso = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string limpo = new string(texto.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray()).Replace(',', '.');
        return decimal.TryParse(limpo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out peso);
    }

    private async void ReadWeightLegend_Click(object? sender, EventArgs e)
    {
        if (!_isProductionStarted || _isReadingWeight)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Executar,
                "ler peso da entrada"))
        {
            return;
        }

        DataGridViewRow? linhaItem = GetSelectedProductionRow();
        if (linhaItem is null)
        {
            statusLabel.Text = "Selecione o item do pedido antes de ler o peso.";
            MessageBox.Show(
                "Selecione o item do pedido na lista antes de ler o peso.",
                "Leitura de peso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!LinhaPossuiTaraSelecionada(linhaItem, out _))
        {
            statusLabel.Text = "Selecione a tara do item antes de ler o peso.";
            await SelecionarTaraParaLinhaAsync(linhaItem);
            if (!LinhaPossuiTaraSelecionada(linhaItem, out _))
            {
                statusLabel.Text = "Peso nao registrado: e necessario selecionar a tara do item.";
                return;
            }
        }

        _isReadingWeight = true;
        SetReadWeightEnabled(false);
        statusLabel.Text = "Lendo peso da balanca...";

        try
        {
            ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
            if (!leitura.Sucesso)
            {
                statusLabel.Text = leitura.Mensagem;
                MessageBox.Show(
                    leitura.Mensagem,
                    "Erro ao ler peso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string weight = leitura.Peso;
            if (!RegistrarPesoLido(linhaItem, weight))
            {
                return;
            }

            statusLabel.Text = "Peso registrado localmente. Finalize a leitura para gravar o lançamento.";
            DadosEtiquetaMateriaPrima label = ConstruirDadosEtiquetaMateriaPrima(linhaItem);
            if (!await TentarImprimirEtiquetaAposLeituraAsync(label))
            {
                MessageBox.Show(
                    "Peso registrado, mas a etiqueta não foi impressa.",
                    "Etiqueta não impressa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            statusLabel.Text = $"Peso {weight} registrado no item {GetCellValue(linhaItem, "productionCodeColumn")} e etiqueta {label.CodigoProduto} enviada para impressao.";
        }
        catch (Exception ex)
        {
            string message = GetFriendlyErrorMessage(ex);
            statusLabel.Text = message;
            MessageBox.Show(
                message,
                "Erro ao ler peso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _isReadingWeight = false;
            SetReadWeightEnabled(_isProductionStarted);
        }
    }

    private static string GetFriendlyErrorMessage(Exception ex)
    {
        if (ex.InnerException is not null &&
            ex.Message.Contains("One or more errors occurred", StringComparison.OrdinalIgnoreCase))
        {
            return GetFriendlyErrorMessage(ex.InnerException);
        }

        return ex is ErroOperacionalEsperadoException
            ? ex.Message
            : "Nao foi possivel concluir a operacao. Acione o suporte.";
    }

    private async Task<bool> TentarImprimirEtiquetaAposLeituraAsync(DadosEtiquetaMateriaPrima label)
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(PermissoesSistema.Acoes.Imprimir))
        {
            statusLabel.Text = "Peso registrado, mas a etiqueta não foi impressa.";
            return false;
        }

        try
        {
            await _impressaoEntrada.GarantirImpressoraDisponivelAsync();
            await _impressaoEntrada.ImprimirEtiquetaMateriaPrimaAsync(label);
            return true;
        }
        catch (Exception ex)
        {
            await ErroUsuarioHelper.TratarAsync(
                "IMPRESSAO_ETIQUETA_APOS_LEITURA_ERRO",
                ex,
                "ProcessoEntradaProdutoForm",
                "Peso registrado, mas a etiqueta não foi impressa.");
            statusLabel.Text = "Peso registrado, mas a etiqueta não foi impressa.";
            return false;
        }
    }

    // Grava o peso lido da balanca na coluna Peso da linha do item selecionado.
    private bool RegistrarPesoLido(DataGridViewRow linhaItem, string weight)
    {
        if (!TryParsePesoKg(weight, out decimal pesoBruto))
        {
            statusLabel.Text = "Peso lido invalido.";
            return false;
        }

        if (!AdicionarLeituraNaLinha(
                linhaItem,
                pesoBruto,
                "BALANCA",
                weight))
        {
            return false;
        }

        ApplyProductionRowStyle(linhaItem, linhaItem.Index);
        ClearGridSelection(productionDataGridView);
        linhaItem.Selected = true;
        SetCurrentProductionCell(linhaItem, "productionPesoLidoColumn");
        UpdateProductionCounters();
        return true;
    }

    private async void DeleteLastProductionRow_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Cancelar,
                "cancelar entrada de produto"))
        {
            return;
        }

        if (!_isProductionStarted)
        {
            return;
        }

        DataGridViewRow? lastRow = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .LastOrDefault();

        if (lastRow is null)
        {
            statusLabel.Text = "Nao ha linhas para excluir.";
            return;
        }

        string productionCode = GetCellValue(lastRow, "productionCodeColumn");
        if (!ConfirmDeleteLastProductionRow(productionCode))
        {
            statusLabel.Text = "Exclusao cancelada.";
            return;
        }

        if (CancelarLeiturasDaLinha(lastRow))
        {
            statusLabel.Text = $"Leituras do item {productionCode} marcadas como canceladas.";
            return;
        }

        productionDataGridView.Rows.Remove(lastRow);
        _nextProductionCode = Math.Max(1, _nextProductionCode - 1);
        RestyleProductionRows();
        UpdateProductionCounters();
        ClearGridSelection(productionDataGridView);
        statusLabel.Text = $"Ultima etiqueta {productionCode} excluida.";
    }

    private async void DeleteProductionRowByCode_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Cancelar,
                "cancelar entrada de produto"))
        {
            return;
        }

        if (!_isProductionStarted)
        {
            return;
        }

        string? productionCode = PromptProductionCodeToDelete();
        if (string.IsNullOrWhiteSpace(productionCode))
        {
            statusLabel.Text = "Exclusao cancelada.";
            return;
        }

        DataGridViewRow? row = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(item => !item.IsNewRow)
            .FirstOrDefault(item => string.Equals(
                GetCellValue(item, "productionCodeColumn"),
                productionCode,
                StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            statusLabel.Text = $"Etiqueta {productionCode} nao encontrada.";
            MessageBox.Show(
                $"Nenhuma etiqueta com codigo {productionCode} foi encontrada.",
                "Etiqueta nao encontrada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!ConfirmProductionRowDelete(productionCode, "Deseja realmente excluir a etiqueta informada?"))
        {
            statusLabel.Text = "Exclusao cancelada.";
            return;
        }

        if (CancelarLeiturasDaLinha(row))
        {
            statusLabel.Text = $"Leituras do item {productionCode} marcadas como canceladas.";
            return;
        }

        productionDataGridView.Rows.Remove(row);
        RestyleProductionRows();
        UpdateProductionCounters();
        ClearGridSelection(productionDataGridView);
        statusLabel.Text = $"Etiqueta {productionCode} excluida.";
    }

    private string? PromptProductionCodeToDelete()
    {
        using Form promptForm = new()
        {
            Text = "Excluir etiqueta por codigo",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 185),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label messageLabel = new()
        {
            Text = "Informe o codigo serial da etiqueta que deseja excluir.",
            Dock = DockStyle.Top,
            Height = 62,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(18, 10, 18, 4)
        };

        TextBox codeTextBox = new()
        {
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Location = new Point(80, 76),
            Size = new Size(220, 31),
            TextAlign = HorizontalAlignment.Center
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(74, 8, 0, 14),
            Height = 64
        };

        Button cancelButton = CreateDialogButton("Cancelar", Color.FromArgb(55, 60, 69), DialogResult.Cancel);
        Button deleteButton = CreateDialogButton("Excluir", Color.FromArgb(184, 18, 32), DialogResult.OK);
        cancelButton.Margin = new Padding(0, 0, 10, 0);

        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(deleteButton);
        promptForm.Controls.Add(messageLabel);
        promptForm.Controls.Add(codeTextBox);
        promptForm.Controls.Add(buttonsPanel);
        promptForm.AcceptButton = deleteButton;
        promptForm.CancelButton = cancelButton;
        promptForm.ActiveControl = codeTextBox;

        return promptForm.ShowDialog(this) == DialogResult.OK
            ? codeTextBox.Text.Trim()
            : null;
    }

    private bool ConfirmDeleteLastProductionRow(string productionCode)
    {
        return ConfirmProductionRowDelete(productionCode, "Deseja realmente excluir a ultima etiqueta?");
    }

    private bool ConfirmProductionRowDelete(string productionCode, string message)
    {
        using Form confirmationForm = new()
        {
            Text = "Confirmar exclusao",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 150),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label messageLabel = new()
        {
            Text = $"{message}\r\nCodigo: {productionCode}",
            Dock = DockStyle.Top,
            Height = 86,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(18, 10, 18, 4)
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(74, 8, 0, 14),
            Height = 64
        };

        Button noButton = CreateDialogButton("Nao", Color.FromArgb(55, 60, 69), DialogResult.No);
        Button yesButton = CreateDialogButton("Sim", Color.FromArgb(184, 18, 32), DialogResult.Yes);
        noButton.Margin = new Padding(0, 0, 10, 0);

        buttonsPanel.Controls.Add(noButton);
        buttonsPanel.Controls.Add(yesButton);
        confirmationForm.Controls.Add(messageLabel);
        confirmationForm.Controls.Add(buttonsPanel);
        confirmationForm.AcceptButton = noButton;
        confirmationForm.CancelButton = noButton;
        confirmationForm.ActiveControl = noButton;

        return confirmationForm.ShowDialog(this) == DialogResult.Yes;
    }

    private static Button CreateDialogButton(string text, Color backColor, DialogResult dialogResult)
    {
        Button button = new()
        {
            Text = text,
            DialogResult = dialogResult,
            BackColor = backColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Cascadia Code", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Size = new Size(96, 34),
            Margin = new Padding(0)
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void RestyleProductionRows()
    {
        for (int rowIndex = 0; rowIndex < productionDataGridView.Rows.Count; rowIndex++)
        {
            DataGridViewRow row = productionDataGridView.Rows[rowIndex];
            if (!row.IsNewRow)
            {
                ApplyProductionRowStyle(row, rowIndex);
            }
        }
    }

    private void UpdateProductionCounters()
    {
        int quantidadeTotalPedido = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Sum(row => int.TryParse(GetCellValue(row, "productionQuantityColumn"), out int quantity) ? quantity : 0);

        decimal pesoUtilizado = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Sum(row => TryParsePesoKg(GetCellValue(row, "productionPesoLidoColumn"), out decimal weight) ? weight : 0m);

        boxesCaptionLabel.Text = "Qtde Total Pedido";
        packagesCaptionLabel.Text = "Peso Utilizado";
        boxesCounterLabel.Text = quantidadeTotalPedido.ToString("000");
        packagesCounterLabel.Text = $"{pesoUtilizado.ToString("000.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} KG";
        UpdateProductionGridFooter();
    }

    private static string NormalizeCounterTotal(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int total) ? total.ToString() : "0";
    }

    private void SetReadWeightEnabled(bool enabled)
    {
        bool leituraBalancaHabilitada =
            enabled && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Executar);
        bool leituraManualHabilitada =
            enabled && PossuiPermissaoEntrada(PermissoesSistema.Acoes.PesoManual);

        if (!leituraBalancaHabilitada)
        {
            _isReadWeightHovering = false;
            readWeightLegendPanel.Invalidate();
        }

        readWeightLegendPanel.Enabled = leituraBalancaHabilitada;
        readWeightLegendIconLabel.Enabled = leituraBalancaHabilitada;
        readWeightLegendTextLabel.Enabled = leituraBalancaHabilitada;
        readWeightLegendPanel.Cursor = leituraBalancaHabilitada ? Cursors.Hand : Cursors.Default;
        readWeightLegendIconLabel.Cursor = leituraBalancaHabilitada ? Cursors.Hand : Cursors.Default;
        readWeightLegendTextLabel.Cursor = leituraBalancaHabilitada ? Cursors.Hand : Cursors.Default;
        readWeightLegendTextLabel.ForeColor = leituraBalancaHabilitada ? EnabledLegendTextColor : DisabledLegendTextColor;
        readWeightLegendIconLabel.Visible = leituraBalancaHabilitada;
        lerEtiquetaButton.Enabled = leituraBalancaHabilitada;
        leituraManualButton.Enabled = leituraManualHabilitada;
    }

    private void SetDeleteActionsEnabled(bool enabled)
    {
        enabled = enabled && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Cancelar);

        deleteLastLegendPanel.Enabled = enabled;
        deleteLastLegendIconLabel.Enabled = enabled;
        deleteLastLegendTextLabel.Enabled = enabled;
        deleteByCodeLegendPanel.Enabled = enabled;
        deleteByCodeLegendIconLabel.Enabled = enabled;
        deleteByCodeLegendTextLabel.Enabled = enabled;

        deleteLastLegendPanel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        deleteLastLegendIconLabel.Cursor = deleteLastLegendPanel.Cursor;
        deleteLastLegendTextLabel.Cursor = deleteLastLegendPanel.Cursor;
        deleteByCodeLegendPanel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        deleteByCodeLegendIconLabel.Cursor = deleteByCodeLegendPanel.Cursor;
        deleteByCodeLegendTextLabel.Cursor = deleteByCodeLegendPanel.Cursor;

        deleteLastLegendTextLabel.ForeColor = enabled ? EnabledLegendTextColor : DisabledLegendTextColor;
        deleteByCodeLegendTextLabel.ForeColor = enabled ? EnabledLegendTextColor : DisabledLegendTextColor;
        deleteLastLegendIconLabel.Visible = enabled;
        deleteByCodeLegendIconLabel.Visible = enabled;
    }

    private async void LeituraManual_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.PesoManual,
                "informar peso manual"))
        {
            return;
        }

        if (!_isProductionStarted)
        {
            statusLabel.Text = "Inicie a leitura antes de informar o peso manual.";
            return;
        }

        DataGridViewRow? selectedRow = GetSelectedProductionRow();
        if (selectedRow is null)
        {
            statusLabel.Text = "Selecione uma linha para informar o peso manual.";
            MessageBox.Show(
                "Selecione o item do pedido na lista antes de informar o peso manual.",
                "Peso manual",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!LinhaPossuiTaraSelecionada(selectedRow, out _))
        {
            statusLabel.Text = "Selecione a tara do item antes de informar o peso manual.";
            await SelecionarTaraParaLinhaAsync(selectedRow);
            if (!LinhaPossuiTaraSelecionada(selectedRow, out _))
            {
                statusLabel.Text = "Peso nao registrado: e necessario selecionar a tara do item.";
                return;
            }
        }

        string? manualWeight = PromptManualProductionWeight(string.Empty);
        if (string.IsNullOrWhiteSpace(manualWeight))
        {
            statusLabel.Text = "Peso manual cancelado.";
            return;
        }

        if (!TryNormalizeWeight(manualWeight, out string normalizedWeight, out string? errorMessage))
        {
            MessageBox.Show(
                errorMessage ?? "Informe um peso valido.",
                "Peso manual",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!TryParsePesoKg(normalizedWeight, out decimal pesoManual)
            || !AdicionarLeituraNaLinha(
                selectedRow,
                pesoManual,
                "MANUAL",
                manualWeight))
        {
            return;
        }

        ApplyProductionRowStyle(selectedRow, selectedRow.Index);
        ClearGridSelection(productionDataGridView);
        selectedRow.Selected = true;
        SetCurrentProductionCell(selectedRow, "productionPesoLidoColumn");
        UpdateProductionCounters();
        statusLabel.Text = "Peso registrado localmente. Finalize a leitura para gravar o lançamento.";
    }

    private void UpdateProductionState(bool started)
    {
        ClearDangerActionHover();
        sidePanel.BackColor = Color.White;
        sideReadingStatusLabel.Text = started ? "Ativo" : "Inativo";
        sideReadingStatusLabel.ForeColor = started ? ReadingStatusActiveColor : ReadingStatusInactiveColor;
        bool podeAlternarLeitura = started
            ? PossuiPermissaoEntrada(PermissoesSistema.Acoes.Finalizar)
            : PodeIniciarLeitura();
        Color corInicio = !started && podeAlternarLeitura ? ActionEnabledColor : ActionDisabledColor;
        startActionPanel.BackColor = corInicio;
        startActionTextLabel.ForeColor = !started && podeAlternarLeitura ? EnabledLegendTextColor : DisabledLegendTextColor;
        startActionPanel.Enabled = !started && podeAlternarLeitura;
        startActionIconLabel.Enabled = startActionPanel.Enabled;
        startActionTextLabel.Enabled = startActionPanel.Enabled;
        startActionPanel.Cursor = startActionPanel.Enabled ? Cursors.Hand : Cursors.Default;
        startActionIconLabel.Cursor = startActionPanel.Cursor;
        startActionTextLabel.Cursor = startActionPanel.Cursor;
        _isStartActionHovering = false;
        startActionPanel.Invalidate();

        UpdateStatusCardState(started);
        UpdateTitleBarLockState(started);
        iniciarLeituraButton.BaseBackColor = started
            ? Color.FromArgb(212, 37, 49)
            : podeAlternarLeitura ? ReadingStatusActiveColor : ActionDisabledColor;
        iniciarLeituraButton.BaseForeColor = Color.White;
        iniciarLeituraButton.IconFontFamily = "Segoe MDL2 Assets";
        iniciarLeituraButton.IconGlyph = started ? "\uE71A" : "\uE768";
        iniciarLeituraButton.PrimaryText = started ? "PARAR LEITURA" : "INICIAR LEITURA";
        iniciarLeituraButton.Enabled = podeAlternarLeitura;
        iniciarLeituraButton.Cursor = podeAlternarLeitura ? Cursors.Hand : Cursors.Default;
        iniciarLeituraButton.Invalidate();
        lerEtiquetaButton.Visible = started;
        leituraManualButton.Visible =
            started && PossuiPermissaoEntrada(PermissoesSistema.Acoes.PesoManual);

        productionDataGridView.SelectionMode = started
            ? DataGridViewSelectionMode.FullRowSelect
            : DataGridViewSelectionMode.CellSelect;

        stopActionPanel.Visible = started;
        stopActionPanel.Enabled = started && podeAlternarLeitura;
        stopActionPanel.Cursor = stopActionPanel.Enabled ? Cursors.Hand : Cursors.Default;
        stopActionIconLabel.Cursor = stopActionPanel.Cursor;
        stopActionTextLabel.Cursor = stopActionPanel.Cursor;

        if (!started)
        {
            ClearGridSelection(productionDataGridView);
        }

        SetReadWeightEnabled(started);
        SetDeleteActionsEnabled(started);
    }

    private bool PodeIniciarLeitura()
        => PossuiPermissaoEntrada(PermissoesSistema.Acoes.Executar)
            && PedidoSelecionadoValido();

    private bool PedidoSelecionadoValido()
    {
        string numeroPedido = pedidoComboBox.Text.Trim();
        return !string.IsNullOrWhiteSpace(numeroPedido)
            && string.Equals(numeroPedido, _numeroPedidoCarregado, StringComparison.OrdinalIgnoreCase)
            && productionDataGridView.Rows
                .Cast<DataGridViewRow>()
                .Any(row => !row.IsNewRow);
    }

    private void AtualizarDisponibilidadeInicioLeitura()
    {
        if (!_isProductionStarted)
        {
            UpdateProductionState(false);
        }
    }

    private void ProductionDataGridView_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            if (!_isProductionStarted)
            {
                ClearGridSelection(productionDataGridView);
            }

            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        productionDataGridView.CurrentCell = productionDataGridView[e.ColumnIndex, e.RowIndex];
        row.Selected = true;
    }

    private async void ProductionDataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        if (e.ColumnIndex >= 0 && productionDataGridView.Columns[e.ColumnIndex].Name == ColunaReimpressaoEtiqueta)
        {
            await ReimprimirEtiquetaComConfirmacaoAsync(row);
            return;
        }

        if (!_isProductionStarted)
        {
            if (!LinhaPossuiTaraSelecionada(row, out _))
            {
                await SelecionarTaraParaLinhaAsync(row);
            }

            ClearGridSelection(productionDataGridView);
            row.Selected = true;
            SetCurrentProductionCell(row, "productionCodeColumn");
            statusLabel.Text = LinhaPossuiTaraSelecionada(row, out _)
                ? "Tara selecionada. Inicie a leitura para registrar o peso do item."
                : "Selecione a tara do item ou inicie a leitura para registrar peso.";
            return;
        }

        await AbrirPesagemMultiplaParaLinhaAsync(row);
    }

    private async Task ReimprimirEtiquetaComConfirmacaoAsync(DataGridViewRow row)
    {
        if (await BloquearImpressaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Reimprimir,
                "reimprimir etiqueta"))
        {
            return;
        }

        if (_codigoLancamentoPersistido is not long codigoLancamento
            || codigoLancamento <= 0)
        {
            statusLabel.Text = "Finalize e persista o lancamento antes de reimprimir.";
            MessageBox.Show(
                "A reimpressao usa os dados persistidos.\n\nFinalize o lancamento antes de reimprimir a etiqueta.",
                "Reimpressao indisponivel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!long.TryParse(
                GetCellValue(row, "productionItemIdColumn"),
                out long codigoSapItem)
            || codigoSapItem <= 0)
        {
            statusLabel.Text = "Item invalido para reimpressao.";
            return;
        }

        string itemPedido = GetCellValue(row, "productionCodeColumn");
        using ConfirmarReimpressaoEtiquetaForm confirmacao = new(itemPedido);
        if (confirmacao.ShowDialog(this) != DialogResult.Yes)
        {
            statusLabel.Text = "Reimpressão cancelada.";
            return;
        }

        try
        {
            EntradaProdutoItemPersistido? itemPersistido =
                await _entradaServico.ObterItemPersistidoAsync(
                    codigoLancamento,
                    codigoSapItem,
                    _fechamentoTelaCts.Token);
            if (itemPersistido is null || itemPersistido.PesoLiquidoTotalKg <= 0m)
            {
                statusLabel.Text = "Dados persistidos nao encontrados para reimpressao.";
                MessageBox.Show(
                    "Nao foi encontrada pesagem valida persistida para este item.",
                    "Reimpressao indisponivel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DadosEtiquetaMateriaPrima etiqueta =
                ImpressaoEntradaServico.MontarEtiqueta(itemPersistido, expirationDateTextBox.Text);
            await _impressaoEntrada.ReimprimirEtiquetaMateriaPrimaAsync(etiqueta);
            statusLabel.Text =
                $"Etiqueta reimpressa com dados do lancamento {codigoLancamento}.";
        }
        catch (Exception ex)
        {
            string mensagem = await ErroUsuarioHelper.TratarAsync(
                "REIMPRESSAO_ETIQUETA_ERRO",
                ex,
                "ProcessoEntradaProdutoForm",
                "Nao foi possivel reimprimir a etiqueta. Acione o suporte.");
            statusLabel.Text = mensagem;
            MessageBox.Show(
                mensagem,
                "Erro ao reimprimir etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task AbrirPesagemMultiplaParaLinhaAsync(DataGridViewRow linhaItem)
    {
        if (!LinhaPertenceAoGridProducao(linhaItem))
        {
            statusLabel.Text = "Selecione um item valido do pedido para pesar.";
            return;
        }

        if (!LinhaPossuiTaraSelecionada(linhaItem, out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            await SelecionarTaraParaLinhaAsync(linhaItem);
            if (!LinhaPossuiTaraSelecionada(linhaItem, out tara))
            {
                return;
            }
        }

        string itemPedido = GetCellValue(linhaItem, "productionCodeColumn");
        string itemId = GetCellValue(linhaItem, "productionItemIdColumn");
        if (!long.TryParse(itemId, out long codigoItem) || codigoItem <= 0)
        {
            statusLabel.Text = "Item invalido para pesagem.";
            return;
        }

        IReadOnlyList<EntradaProdutoPesagem> leiturasAtuais =
            ObterLeiturasItem(codigoItem);
        using PesagemMultiplaItemForm form = new(
            _balancaLeituraServico,
            itemPedido,
            tara,
            _idBalancaSelecionada,
            leiturasAtuais);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            statusLabel.Text = "Pesagem múltipla cancelada.";
            return;
        }

        // Re-localiza a linha pelo id do item: apos o dialogo, a referencia original pode estar
        // desatualizada se o grid foi recarregado. Garante que o peso somado seja gravado na linha viva.
        DataGridViewRow linhaAlvo = LocalizarLinhaProducaoPorItemId(itemId) ?? linhaItem;
        linhaAlvo.Tag = tara;
        _leiturasPorItem[codigoItem] = form.Pesagens.ToList();

        if (!AtualizarTotaisDaLinha(linhaAlvo, _leiturasPorItem[codigoItem]))
        {
            statusLabel.Text = "Nao foi possivel consolidar o peso na linha do item.";
            MessageBox.Show(
                $"Nao foi possivel gravar o peso somado ({form.PesoTotalTexto}) na linha do item {itemPedido}.\n\nSelecione o item novamente e repita a pesagem.",
                "Pesagem múltipla",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ApplyProductionRowStyle(linhaAlvo, linhaAlvo.Index);
        ClearGridSelection(productionDataGridView);
        linhaAlvo.Selected = true;
        SetCurrentProductionCell(linhaAlvo, "productionPesoLidoColumn");
        UpdateProductionCounters();
        statusLabel.Text = $"Peso bruto total {form.PesoTotalTexto} registrado no item {itemPedido}.";
        MessageBox.Show(
            $"Peso bruto total {form.PesoTotalTexto} registrado no item {itemPedido}.",
            "Pesagem múltipla",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task SelecionarTaraParaLinhaAsync(DataGridViewRow linhaItem)
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            statusLabel.Text = "Banco desabilitado: não foi possível carregar taras.";
            return;
        }

        if (_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0)
        {
            statusLabel.Text = "Usuário sem setor definido: não é possível selecionar tara.";
            MessageBox.Show(
                "Seu usuário não possui setor definido.\n\nDefina o setor padrão do usuário para selecionar a tara do item.",
                "Seleção de Tara",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // H4: apenas taras ativas do setor autorizado (nao confiar so no ComboBox/Form).
            IReadOnlyList<global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro> taras =
                await _taraController.ListarAtivasPorSetorAsync(codigoSetor, _fechamentoTelaCts.Token);
            if (!PodeAtualizarTela())
            {
                return;
            }

            if (taras.Count == 0)
            {
                statusLabel.Text = "Nenhuma tara ativa para o seu setor. Cadastre uma tara no setor antes de pesar.";
                MessageBox.Show(
                    "Nenhuma tara ativa para o seu setor.\n\nCadastre/ative uma tara do setor em Cadastro > Tara antes de registrar o peso do item.",
                    "Seleção de Tara",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string itemPedido = GetCellValue(linhaItem, "productionCodeColumn");
            using SelecaoTaraPesagemForm form = new(taras, itemPedido);
            if (form.ShowDialog(this) != DialogResult.OK || form.TaraSelecionada is null)
            {
                statusLabel.Text = "Seleção de tara cancelada.";
                return;
            }

            linhaItem.Tag = form.TaraSelecionada;
            string tooltipTara =
                $"Tara: {form.TaraSelecionada.NomeTara} " +
                $"({form.TaraSelecionada.PesoKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} kg)";
            foreach (DataGridViewCell cell in linhaItem.Cells)
            {
                cell.ToolTipText = tooltipTara;
            }

            statusLabel.Text = $"Tara '{form.TaraSelecionada.NomeTara}' selecionada para o item {itemPedido}.";
        }
        catch (OperationCanceledException)
        {
            // Tela fechada durante a carga das taras.
        }
        catch (Exception ex)
        {
            _controller.Sap.RegistrarDiagnostico($"ERRO ao carregar taras para pesagem.{Environment.NewLine}{ex}");
            statusLabel.Text = "Não foi possível carregar as taras cadastradas.";
            MessageBox.Show(
                "Não foi possível carregar as taras cadastradas. Acione o suporte.",
                "Seleção de Tara",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ProductionDataGridView_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        if (productionDataGridView.SelectedCells.Count == 0 && productionDataGridView.SelectedRows.Count == 0)
        {
            return;
        }

        ClearGridSelection(productionDataGridView);
    }

    private void UpdateTitleBarLockState(bool locked)
    {
        menuHeaderLabel.Visible = !locked;
        minimizeWindowLabel.Visible = !locked;
        maximizeWindowLabel.Visible = !locked;
        closeWindowLabel.Visible = !locked;

        menuHeaderLabel.Enabled = !locked;
        minimizeWindowLabel.Enabled = !locked;
        maximizeWindowLabel.Enabled = !locked;
        closeWindowLabel.Enabled = !locked;

        customTitleBarPanel.Cursor = locked ? Cursors.Default : Cursors.SizeAll;
        companyLogoPictureBox.Cursor = customTitleBarPanel.Cursor;
        headerTitleLabel.Cursor = customTitleBarPanel.Cursor;
        headerSubtitleLabel.Cursor = customTitleBarPanel.Cursor;
    }

    private void UpdateStatusCardState(bool started)
    {
        Color statusColor = started ? ReadingStatusActiveColor : ReadingStatusInactiveColor;

        statusCard.BackColor = Color.Transparent;
        statusCard.FillColor = started ? Color.FromArgb(229, 247, 234) : Color.FromArgb(254, 232, 232);
        statusCard.BorderColor = started ? Color.FromArgb(187, 229, 199) : Color.FromArgb(248, 190, 190);

        statusCardIcon.Text = started ? "✓" : "!";
        statusCardIcon.ForeColor = statusColor;
        statusValueLabel.Text = started ? "ATIVA" : "INATIVA";
        statusValueLabel.ForeColor = statusColor;
        statusHintLabel.Text = started ? "Leitura liberada para registro" : "Leitura aguardando inicio";
        statusHintLabel.ForeColor = Color.FromArgb(98, 108, 124);

        statusCard.Invalidate(true);
        statusCardIcon.Invalidate();
        statusValueLabel.Invalidate();
        statusHintLabel.Invalidate();
    }

    private void ClearDangerActionHover()
    {
        Panel? hoveredPanel = _hoveredDangerActionPanel;
        _hoveredDangerActionPanel = null;
        hoveredPanel?.Invalidate();
    }

    private async void ProductionDataGridView_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        if (await BloquearImpressaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Imprimir,
                "imprimir etiqueta"))
        {
            return;
        }

        await ImprimirEtiquetaDaLinhaAsync(
            row,
            "Etiqueta enviada para impressao.",
            reimpressao: false);
    }

    private async Task ImprimirEtiquetaDaLinhaAsync(
        DataGridViewRow row,
        string mensagemSucesso,
        bool reimpressao)
    {
        try
        {
            DadosEtiquetaMateriaPrima label = ConstruirDadosEtiquetaMateriaPrima(row);
            if (reimpressao)
            {
                await _impressaoEntrada.ReimprimirEtiquetaMateriaPrimaAsync(label);
            }
            else
            {
                await _impressaoEntrada.ImprimirEtiquetaMateriaPrimaAsync(label);
            }
            statusLabel.Text = $"{mensagemSucesso} Item {label.CodigoProduto}.";
        }
        catch (Exception ex)
        {
            string msg = await ErroUsuarioHelper.TratarAsync("IMPRESSAO_ETIQUETA_ERRO", ex, "ProcessoEntradaProdutoForm",
                "Não foi possível imprimir a etiqueta. Acione o suporte.");
            statusLabel.Text = msg;
            MessageBox.Show(
                msg,
                "Erro ao imprimir etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private DadosEtiquetaMateriaPrima ConstruirDadosEtiquetaMateriaPrima(DataGridViewRow row)
    {
        string numeroPedido = pedidoComboBox.Text.Trim();
        string numeroItem = GetCellValue(row, "productionNumeroItemColumn");
        string codigoMaterial = GetCellValue(row, "productionCodeColumn");

        return new DadosEtiquetaMateriaPrima
        {
            CodigoProduto = codigoMaterial,
            DescricaoProduto = GetCellValue(row, "productionProductColumn"),
            LoteOrigem = numeroPedido,
            LoteInterno = numeroItem,
            DataFabricacao = stepLabel.Text,
            DataVencimento = expirationDateTextBox.Text,
            CertificadoSanitario = string.Empty,
            Sif = string.Empty,
            Fornecedor = lotTextBox.Text,
            NumeroNotaFiscal = string.Empty,
            Peso = FormatarPesoEtiquetaMateriaPrima(GetCellValue(row, "productionPesoLidoColumn")),
            NumeroPedido = numeroPedido,
            NumeroItem = numeroItem
        };
    }

    private static string FormatarPesoEtiquetaMateriaPrima(string peso)
    {
        if (TryParsePesoKg(peso, out decimal pesoKg))
        {
            return pesoKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        }

        return peso.Trim();
    }

    private DataGridViewRow? GetSelectedProductionRow()
    {
        DataGridViewRow? selectedRow = productionDataGridView.SelectedRows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => !row.IsNewRow);

        if (selectedRow is not null)
        {
            return selectedRow;
        }

        if (productionDataGridView.CurrentRow is not null && !productionDataGridView.CurrentRow.IsNewRow)
        {
            return productionDataGridView.CurrentRow;
        }

        return productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => row.Selected && !row.IsNewRow);
    }

    private static bool LinhaPossuiTaraSelecionada(
        DataGridViewRow linhaItem,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara)
    {
        tara = linhaItem.Tag as global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro;
        return tara is not null;
    }

    private IReadOnlyList<EntradaProdutoPesagem> ObterLeiturasItem(long codigoItem)
        => _leiturasPorItem.TryGetValue(codigoItem, out List<EntradaProdutoPesagem>? leituras)
            ? leituras
            : [];

    private bool AdicionarLeituraNaLinha(
        DataGridViewRow linhaItem,
        decimal pesoBruto,
        string origem,
        string leituraOriginal)
    {
        if (!long.TryParse(
                GetCellValue(linhaItem, "productionItemIdColumn"),
                out long codigoItem)
            || codigoItem <= 0)
        {
            statusLabel.Text = "Item invalido para registrar a leitura.";
            return false;
        }

        if (!LinhaPossuiTaraSelecionada(
                linhaItem,
                out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            statusLabel.Text = "Selecione a tara antes de registrar a leitura.";
            return false;
        }

        decimal pesoLiquido = EntradaProdutoPesagemCalculos.CalcularPesoLiquido(pesoBruto, tara.PesoKg);
        if (!EntradaProdutoPesagemCalculos.LeituraTemPesoValido(pesoBruto, pesoLiquido))
        {
            statusLabel.Text = "O peso bruto deve ser maior que a tara.";
            return false;
        }

        List<EntradaProdutoPesagem> leituras =
            _leiturasPorItem.GetValueOrDefault(codigoItem) ?? [];
        leituras.Add(EntradaProdutoPesagemCalculos.MontarLeitura(
            leituras,
            pesoBruto,
            tara.PesoKg,
            tara.CodigoTara,
            origem,
            _idBalancaSelecionada,
            leituraOriginal,
            DateTimeOffset.Now));
        _leiturasPorItem[codigoItem] = leituras;
        return AtualizarTotaisDaLinha(linhaItem, leituras);
    }

    private static bool AtualizarTotaisDaLinha(
        DataGridViewRow linhaItem,
        IReadOnlyList<EntradaProdutoPesagem> leituras)
    {
        decimal pesoBrutoTotal =
            EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(leituras);
        string origem = EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada(leituras);

        return SetCellValue(
                linhaItem,
                "productionPesoLidoColumn",
                pesoBrutoTotal > 0m
                    ? pesoBrutoTotal.ToString(
                        "0.###",
                        System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))
                    : string.Empty)
            && SetCellValue(linhaItem, "productionPesoOrigemColumn", origem);
    }

    private bool CancelarLeiturasDaLinha(DataGridViewRow linhaItem)
    {
        if (!long.TryParse(
                GetCellValue(linhaItem, "productionItemIdColumn"),
                out long codigoItem)
            || !_leiturasPorItem.TryGetValue(
                codigoItem,
                out List<EntradaProdutoPesagem>? leituras)
            || leituras.Count == 0)
        {
            return false;
        }

        _leiturasPorItem[codigoItem] =
            [.. EntradaProdutoPesagemCalculos.Cancelar(leituras)];
        AtualizarTotaisDaLinha(linhaItem, _leiturasPorItem[codigoItem]);
        UpdateProductionCounters();
        return true;
    }

    private string ObterNomeTerminalAtual()
        => string.IsNullOrWhiteSpace(_contextoTerminal?.NomeTerminal)
            ? Environment.MachineName
            : _contextoTerminal.NomeTerminal;

    private string? PromptManualProductionWeight(string currentWeight)
    {
        using Form promptForm = new()
        {
            Text = "Peso manual",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 185),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label messageLabel = new()
        {
            Text = "Informe o peso utilizado para a linha selecionada.",
            Dock = DockStyle.Top,
            Height = 62,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(18, 10, 18, 4)
        };

        TextBox weightTextBox = new()
        {
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Location = new Point(80, 76),
            Size = new Size(220, 31),
            TextAlign = HorizontalAlignment.Center,
            Text = currentWeight
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(74, 8, 0, 14),
            Height = 64
        };

        Button cancelButton = CreateDialogButton("Cancelar", Color.FromArgb(55, 60, 69), DialogResult.Cancel);
        Button okButton = CreateDialogButton("OK", Color.FromArgb(184, 18, 32), DialogResult.OK);
        cancelButton.Margin = new Padding(0, 0, 10, 0);

        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        promptForm.Controls.Add(messageLabel);
        promptForm.Controls.Add(weightTextBox);
        promptForm.Controls.Add(buttonsPanel);
        promptForm.AcceptButton = okButton;
        promptForm.CancelButton = cancelButton;
        promptForm.ActiveControl = weightTextBox;

        return promptForm.ShowDialog(this) == DialogResult.OK
            ? weightTextBox.Text.Trim()
            : null;
    }

    private static bool TryNormalizeWeight(string input, out string normalizedWeight, out string? errorMessage)
    {
        normalizedWeight = string.Empty;
        errorMessage = null;

        string cleaned = input.Trim().Replace(",", string.Empty).Replace(".", string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            errorMessage = "Informe um peso valido.";
            return false;
        }

        if (!int.TryParse(cleaned, out int weight) || weight < 0)
        {
            errorMessage = "O peso precisa ser um numero inteiro maior ou igual a zero.";
            return false;
        }

        normalizedWeight = weight.ToString();
        return true;
    }

    private static string GetCellValue(DataGridViewRow row, string columnName)
    {
        DataGridViewCell? cell = TryGetCell(row, columnName);
        return Convert.ToString(cell?.Value) ?? string.Empty;
    }

    // Re-localiza a linha do item pelo id (productionItemIdColumn) no grid atual.
    private DataGridViewRow? LocalizarLinhaProducaoPorItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        return productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => !row.IsNewRow
                && string.Equals(GetCellValue(row, "productionItemIdColumn"), itemId, StringComparison.Ordinal));
    }

    private static bool SetCellValue(DataGridViewRow row, string columnName, object? value)
    {
        DataGridViewCell? cell = TryGetCell(row, columnName);
        if (cell is null)
        {
            return false;
        }

        cell.Value = value;
        return true;
    }

    private static DataGridViewCell? TryGetCell(DataGridViewRow row, string columnName)
    {
        DataGridView? grid = row.DataGridView;
        if (grid is null)
        {
            return null;
        }

        DataGridViewColumn? column = grid.Columns.Contains(columnName)
            ? grid.Columns[columnName]
            : grid.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(item => string.Equals(item.Name, columnName, StringComparison.OrdinalIgnoreCase));

        return column is null || column.Index < 0 || column.Index >= row.Cells.Count
            ? null
            : row.Cells[column.Index];
    }

    private bool LinhaPertenceAoGridProducao(DataGridViewRow row)
        => ReferenceEquals(row.DataGridView, productionDataGridView);

    private void SetCurrentProductionCell(DataGridViewRow row, string columnName)
    {
        DataGridViewCell? cell = TryGetCell(row, columnName);
        if (cell is not null && LinhaPertenceAoGridProducao(row))
        {
            productionDataGridView.CurrentCell = cell;
        }
    }

    private async void ProcessoProdutoAcabadoForm_Shown(object? sender, EventArgs e)
    {
        if (!_acessoDiretoValidado)
        {
            _acessoDiretoValidado = true;
            if (!PossuiPermissaoEntrada(PermissoesSistema.Acoes.Consultar))
            {
                await AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
                    PermissoesSistema.Modulos.ProcessoProducao,
                    PermissoesSistema.Rotinas.EntradaProduto,
                    PermissoesSistema.Acoes.Consultar,
                    "abrir diretamente a Entrada de Produto",
                    "ProcessoEntradaProdutoForm");
                MessageBox.Show(
                    "Você não possui permissão para acessar a Entrada de Produto.",
                    "Acesso negado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                Close();
                return;
            }
        }

        BeginInvoke(ClearGridSelections);
        StartProductionDevicesWarmUp();
    }

    private void LoadWindowIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, WindowIconPath);

        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }
    }

    private void ConfigurarComboPedidos()
    {
        productionOrderIconPanel.Visible = false;
        pedidoComboBox.Enabled =
            PossuiPermissaoEntrada(PermissoesSistema.Acoes.Consultar)
            && PossuiPermissaoEntrada(PermissoesSistema.Acoes.SincronizarCache);
        pedidoComboBox.AutoCompleteMode = AutoCompleteMode.None;
        pedidoComboBox.AutoCompleteSource = AutoCompleteSource.None;
        AjustarLarguraComboPedido();
        pedidoComboBox.Items.Clear();
        pedidoComboBox.Text = string.Empty;
        lotTextBox.Text = string.Empty;
        stepLabel.Text = "--/--/----";
        finishedProductCodeTextBox.Text = string.Empty;
        finishedProductTextBox.Text = string.Empty;
        LimparItensPedidoCompra();

        pedidoComboBox.TextUpdate += PedidoComboBox_TextUpdate;
        pedidoComboBox.SelectedIndexChanged += PedidoComboBox_SelectedIndexChanged;
        pedidoComboBox.Validated += PedidoComboBox_Validated;
        productionOrderShadowPanel.Resize += (_, _) => AjustarLarguraComboPedido();
        AtualizarDisponibilidadeInicioLeitura();
    }

    private void AjustarLarguraComboPedido()
    {
        const int margemDireita = 16;
        int larguraDisponivel = productionOrderShadowPanel.ClientSize.Width - pedidoComboBox.Left - margemDireita;
        pedidoComboBox.Width = Math.Max(120, larguraDisponivel);
        pedidoComboBox.DropDownWidth = Math.Max(220, pedidoComboBox.Width);
    }

    private void PedidoComboBox_TextUpdate(object? sender, EventArgs e)
    {
        _consultaPedidoCts?.Cancel();
        if (!string.Equals(
                pedidoComboBox.Text.Trim(),
                _numeroPedidoCarregado,
                StringComparison.OrdinalIgnoreCase))
        {
            _numeroPedidoCarregado = string.Empty;
            LimparDadosPedidoSelecionado();
        }

        AtualizarDisponibilidadeInicioLeitura();
    }

    private async void PedidoComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _consultaPedidoTask = AtualizarDadosPedidoSelecionadoAsync();
        await _consultaPedidoTask;
        AtualizarDisponibilidadeInicioLeitura();
    }

    private async void PedidoComboBox_Validated(object? sender, EventArgs e)
    {
        _consultaPedidoTask = AtualizarDadosPedidoSelecionadoAsync();
        await _consultaPedidoTask;
        AtualizarDisponibilidadeInicioLeitura();
    }

    private async Task AtualizarDadosPedidoSelecionadoAsync()
    {
        string numeroPedido = pedidoComboBox.Text.Trim();
        if (!PodeAtualizarTela())
        {
            return;
        }

        if (PedidoJaCarregado(numeroPedido))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(numeroPedido) || !EstadoIntegracaoBanco.Habilitado)
        {
            _numeroPedidoCarregado = string.Empty;
            LimparDadosPedidoSelecionado();
            AtualizarDisponibilidadeInicioLeitura();
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.SincronizarCache,
                "sincronizar o pedido com o cache SAP"))
        {
            return;
        }

        CancellationTokenSource novaConsulta = CancellationTokenSource.CreateLinkedTokenSource(
            _fechamentoTelaCts.Token);
        CancellationTokenSource? consultaAnterior = Interlocked.Exchange(
            ref _consultaPedidoCts,
            novaConsulta);
        consultaAnterior?.Cancel();
        consultaAnterior?.Dispose();
        CancellationToken cancellationToken = novaConsulta.Token;
        bool gateAdquirido = false;

        try
        {
            await _consultaPedidoGate.WaitAsync(cancellationToken);
            gateAdquirido = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (!PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                return;
            }
            statusLabel.Text = $"Consultando pedido {numeroPedido} no SAP...";

            ResultadoConsultaPedido resultado =
                await _controller.ConsultarPedidoAsync(numeroPedido, cancellationToken);
            if (!PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                return;
            }
            if (!resultado.Sucesso)
            {
                _numeroPedidoCarregado = string.Empty;
                LimparDadosPedidoSelecionado();
                statusLabel.Text = resultado.Mensagem;
                if (PodeAtualizarTela())
                {
                    MessageBox.Show(
                        resultado.Mensagem,
                        "Consulta de pedido",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!PodeAtualizarTela()
                || !PedidoSolicitadoAindaEhAtual(numeroPedido)
                || !string.Equals(
                    resultado.NumeroPedido,
                    numeroPedido,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lotTextBox.Text = resultado.Fornecedor;
            stepLabel.Text = resultado.DataPedido.HasValue
                ? resultado.DataPedido.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))
                : "--/--/----";
            finishedProductCodeTextBox.Text = resultado.TipoPedido;
            finishedProductTextBox.Text = string.Equals(resultado.TipoPedido, TipoPedidoNormal, StringComparison.OrdinalIgnoreCase)
                ? DescricaoPedidoNormal
                : string.Empty;

            _itensCarregadosPorCodigo.Clear();
            foreach (PedidoCompraSapItem itemAutorizado in resultado.ItensAutorizados)
            {
                _itensCarregadosPorCodigo[itemAutorizado.CodigoItem] = itemAutorizado;
            }

            PreencherItensPedidoCompra(resultado.ItensAutorizados);
            _numeroPedidoCarregado = numeroPedido;
            statusLabel.Text = resultado.ItensOcultados > 0
                ? $"{resultado.Mensagem} {resultado.ItensOcultados} item(ns) fora do centro/deposito autorizado nao exibido(s)."
                : resultado.Mensagem;
            AtualizarDisponibilidadeInicioLeitura();
        }
        catch (OperationCanceledException)
        {
            // Nova consulta ou fechamento da tela cancelou esta operacao.
        }
        catch (IntegracaoSapBloqueadaException ex)
        {
            if (PodeAtualizarTela() && PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                statusLabel.Text = ex.Message;
            }
        }
        catch (Exception ex)
        {
            _controller.Sap.RegistrarDiagnostico(
                $"ERRO ao carregar dados do pedido {numeroPedido}.{Environment.NewLine}{ex}");
            if (!PodeAtualizarTela() || !PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                return;
            }

            LimparDadosPedidoSelecionado();
            _numeroPedidoCarregado = string.Empty;
            statusLabel.Text = "Nao foi possivel carregar os dados do pedido selecionado.";
            AtualizarDisponibilidadeInicioLeitura();
        }
        finally
        {
            if (gateAdquirido)
            {
                _consultaPedidoGate.Release();
            }

            if (ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref _consultaPedidoCts,
                        null,
                        novaConsulta),
                    novaConsulta))
            {
                novaConsulta.Dispose();
            }
        }
    }

    private bool PedidoSolicitadoAindaEhAtual(string numeroPedido)
        => PodeAtualizarTela()
            && string.Equals(
                pedidoComboBox.Text.Trim(),
                numeroPedido,
                StringComparison.OrdinalIgnoreCase);

    private void LimparDadosPedidoSelecionado()
    {
        lotTextBox.Text = string.Empty;
        stepLabel.Text = "--/--/----";
        finishedProductCodeTextBox.Text = string.Empty;
        finishedProductTextBox.Text = string.Empty;
        LimparItensPedidoCompra();
    }

    private bool PedidoJaCarregado(string numeroPedido)
        => !string.IsNullOrWhiteSpace(numeroPedido)
            && string.Equals(numeroPedido, _numeroPedidoCarregado, StringComparison.OrdinalIgnoreCase)
            && productionDataGridView.Rows
                .Cast<DataGridViewRow>()
                .Any(row => !row.IsNewRow);

    private void LimparItensPedidoCompra()
    {
        _leiturasPorItem.Clear();
        _codigoLancamentoPersistido = null;
        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "aguardando finalização do lançamento");
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "aguardando gravação local");
        productionActionsButton.Enabled = false;
        productionDataGridView.Rows.Clear();
        UpdateProductionCounters();
        UpdateProductionGridFooter();
    }

    private void PreencherItensPedidoCompra(IReadOnlyList<PedidoCompraSapItem> itens)
    {
        _leiturasPorItem.Clear();
        _codigoLancamentoPersistido = null;
        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "pedido carregado; lançamento ainda não gravado");
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "aguardando gravação local");
        productionActionsButton.Enabled = false;
        productionDataGridView.Rows.Clear();
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        foreach (PedidoCompraSapItem item in itens)
        {
            string quantidade = item.Quantidade.HasValue
                ? item.Quantidade.Value.ToString("0.###", cultura)
                : string.Empty;
            int rowIndex = productionDataGridView.Rows.Add();
            DataGridViewRow row = productionDataGridView.Rows[rowIndex];
            row.Cells["productionCodeColumn"].Value = item.CodigoMaterial ?? string.Empty;
            row.Cells["productionProductColumn"].Value = item.Descricao ?? string.Empty;
            row.Cells["productionQuantityColumn"].Value = quantidade;
            row.Cells["productionWeightColumn"].Value = item.UnidadeMedida ?? string.Empty;
            row.Cells["productionPesoLidoColumn"].Value = string.Empty;
            row.Cells["productionItemIdColumn"].Value = item.CodigoItem.ToString();
            row.Cells["productionPesoOrigemColumn"].Value = string.Empty;
            row.Cells["productionNumeroItemColumn"].Value = item.NumeroItem;
            ApplyProductionRowStyle(row, rowIndex);
        }

        ClearGridSelection(productionDataGridView);
        UpdateProductionCounters();
        UpdateProductionGridFooter();
    }

    private void LoadMockData()
    {
        materialDataGridView.Rows.Clear();
        materialDataGridView.Rows.Add("●", "22273", "POLUCHINHA SMALL TWIST STIX BEEF 50PK", "07032302", "28/05/2026", "0");
        materialDataGridView.Rows.Add("●", "27215", "CX 01 MTHM - 450X300X65", "23012615", "22/01/2028", "808");
        materialDataGridView.Rows.Add("●", "27275", "ETIQ COUCHE ZEBRA 100X43 - PET", "09012607", "06/07/2026", "72");
        materialDataGridView.Rows.Add("●", "27277", "ETIQ COUCHE ZEBRA 80X50 - PET", "12032601", "09/09/2026", "6987");

        productionDataGridView.Rows.Clear();
    }

    private static void ApplyGridStyle(DataGridView grid)
    {
        for (int rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
        {
            ApplyProductionRowStyle(grid.Rows[rowIndex], rowIndex);
        }

        DataGridViewColumn? printColumn = grid.Columns["productionPrintColumn"];
        if (printColumn is not null)
        {
            printColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        ClearGridSelection(grid);
    }

    private static void ApplyProductionRowStyle(DataGridViewRow row, int rowIndex)
    {
        row.DefaultCellStyle.BackColor = rowIndex % 2 == 0 ? RowLight : RowGreen;
        row.DefaultCellStyle.ForeColor = Color.FromArgb(45, 49, 56);
    }

    private void ClearGridSelections()
    {
        ClearGridSelection(materialDataGridView);
        ClearGridSelection(productionDataGridView);
    }

    private static void ClearGridSelection(DataGridView grid)
    {
        grid.ClearSelection();
        grid.CurrentCell = null;
    }
}
