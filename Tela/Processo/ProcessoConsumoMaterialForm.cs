using FugaPET_Dev.Modelo;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Tela;
using FugaPET_Dev.Tela.Comum;

using System.Runtime.InteropServices;

namespace FugaPET_Dev.Tela.Processo;

public partial class ProcessoConsumoMaterialForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
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
    private const string MensagemConfirmacaoSalvarConsumoPadrao = "Deseja salvar localmente este consumo como pendente de envio ao SAP?";
    private const string PermissaoConsumoMateriaPrima = "PROCESSO_CONSUMO_MATERIA_PRIMA";
    private const string PermissaoConsumoQuimicos = "PROCESSO_CONSUMO_QUIMICOS";
    private readonly BalancaLeituraServico _balancaLeituraServico = new();
    private bool _isStartActionHovering;
    private bool _isReadWeightHovering;
    private Panel? _hoveredDangerActionPanel;
    private bool _isProductionStarted;
    private bool _isReadingWeight;
    private Task? _productionDevicesWarmUpTask;
    private ContextoTerminalLocal? _contextoTerminal;
    private long? _idSetorSelecionado;
    private long? _idBalancaSelecionada;
    private long? _idTaraSelecionada;
    private readonly ProcessoConsumoMaterialController _controller = new();
    private readonly ModoConsumoMaterial _modoConsumo;
    private readonly ConfiguracaoTelaConsumoMaterial _configuracaoConsumo;
    private readonly ClassificadorOrdemConsumoMaterial _classificadorOrdemConsumo;

    // Estado da OP de consumo carregada e do componente selecionado no grid de componentes.
    private OrdemProducaoConsumo? _ordemConsumoAtual;
    private ComponenteConsumoMaterial? _componenteConsumoSelecionado;
    private bool _atualizandoComponentes;
    private bool _restaurandoSelecaoLinhaComponentes;

    // Pesagens LOCAIS de consumo por componente (chave composta). Apenas memoria ate o salvar.
    private readonly Dictionary<string, List<PesagemConsumoMaterial>> _pesagensPorComponente = new();
    private int _proximaSequenciaPesagem = 1;

    // Persistencia local (Tarefa 5): idempotencia contra clique duplo / regravacao na mesma sessao.
    private bool _salvandoConsumo;
    private bool _consumoSalvoNaSessao;
    private Button? _confirmarConsumoButton;

    // Preview SAP 261 (Tarefa 6): so montagem/visualizacao do payload — NAO envia SAP.
    private long? _ultimoCodigoLancamentoSalvo;
    private Button? _previewSap261Button;

    // Envio controlado SAP 261 (Tarefa 7): governado por WRITE_ENABLED; trava clique duplo.
    private bool _enviandoSap;
    private Button? _enviarSap261Button;
    private ToolTip? _envioSap261ToolTip;
    private ToolTip? _apontamentoInfoToolTip;
    private ToolTip? _sapStatusToolTip;
    private bool _enviandoConfirmacao;
    private Button? _enviarConfirmacaoButton;

    // Preview Confirmacao de Producao (Tarefa 12): so para componente Backflush — diagnostico, NAO envia SAP.
    private Button? _previewConfirmacaoButton;

    // Saldo Restante no card lateral (layout): label menor abaixo de "Peso Utilizado". So exibicao.
    private Label? saldoRestanteCounterLabel;

    // Ajuste 5: suprime o "limpar OP" durante o preenchimento programatico do campo de Ordem.
    private bool _suprimirEventoOrdem;

    // Tarefa 15: evita reconsulta concorrente da OP (protege _pesagensPorComponente de limpeza acidental).
    private bool _consultandoOrdem;

    // Correcao 2 (Tarefa 15): tara selecionada POR COMPONENTE (chave composta), igual ao padrao da Entrada.
    private readonly Dictionary<string, global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro> _tarasPorComponente = new();
    private bool _selecionandoTara;

    // Tarefa Consumo 22.10.1: descrições reais dos componentes (A_ProductDescription), buscadas por consulta de OP.
    // Preenchido em ConsultarOrdemProducaoAsync e lido pelo seam BuscarTipoMaterialSap no join lógico.
    private IReadOnlyDictionary<string, ProdutoSapMestre> _descricoesProdutoPorCodigo =
        new Dictionary<string, ProdutoSapMestre>(StringComparer.OrdinalIgnoreCase);

    // Tarefa 15.1: enquanto true, o Validated do campo OP NAO reconsulta (acao operacional em andamento).
    private bool _acaoOperacionalEmAndamento;

    // Tarefa 18.3: enquanto true, fechar/voltar a tela NAO dispara validacao/consulta de OP (evita alerta indevido).
    private bool _fechandoTela;

    // Tarefa 16: rota do consumo SALVO (261 direto / Backflush-confirmacao / Misto / Bloqueado) — so habilita botoes.
    private RotaEnvioConsumo _rotaEnvioSalva = RotaEnvioConsumo.Bloqueado;

    // Tarefa 17.6: true apos uma falha SAP (HTTP) no lancamento salvo — bloqueia reenvio automatico (FALHA_SAP).
    private bool _lancamentoComFalhaSap;

    // Ajuste 3 (Tarefa 14): mensagem de peso decimal invalido (KG).
    private const string MensagemPesoConsumoInvalido = "Informe o peso em KG. Exemplo: 0,400 ou 1,5.";

    public ProcessoConsumoMaterialForm()
        : this(ModoConsumoMaterial.MateriaPrima)
    {
    }

    public ProcessoConsumoMaterialForm(ModoConsumoMaterial modo)
        : this(modo, ClassificadorOrdemConsumoMaterial.Padrao)
    {
    }

    internal ProcessoConsumoMaterialForm(
        ModoConsumoMaterial modo,
        ClassificadorOrdemConsumoMaterial classificadorOrdemConsumo)
    {
        _modoConsumo = modo;
        _configuracaoConsumo = ConfiguracaoTelaConsumoMaterialFactory.Criar(modo);
        _classificadorOrdemConsumo = classificadorOrdemConsumo ?? throw new ArgumentNullException(nameof(classificadorOrdemConsumo));
        InitializeComponent();
        AplicarConfiguracaoModoConsumo();
        CriarBotaoConfirmarConsumo();
        CriarBotaoPreviewSap261();
        CriarBotaoEnviarSap261();
        CriarBotaoPreviewConfirmacao();
        CriarBotaoEnviarConfirmacao();
        CriarLabelSaldoRestante();
        AtualizarIndicadorSapConsumo();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        AplicarContextoTerminalAutomatico();
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        ConfigureResponsiveSummaryCards();
        ConfigureProductionSearchBox();
        ConfigureSideActionButtonIcons();
        ApplyGridStyle(materialDataGridView);
        ApplyGridStyle(productionDataGridView);
        ConfigurarSelecaoLinhaInteiraGridComponentes();
        ConfigureProductionGridFooter();
        productionDataGridView.CellMouseDown += ProductionDataGridView_CellMouseDown;
        productionDataGridView.CellClick += ProductionDataGridView_CellClick;
        productionDataGridView.CellMouseClick += ProductionDataGridView_CellMouseClick;
        productionDataGridView.CurrentCellChanged += ProductionDataGridView_CurrentCellChanged;
        productionDataGridView.RowEnter += ProductionDataGridView_RowEnter;
        productionDataGridView.SelectionChanged += ProductionDataGridView_SelectionChanged;
        ConfigureStartActionHoverEffect();
        ConfigureProductionActions();
        ConfigureFooterDate();
        ConfigurarCausesValidacaoOperacional();
        UpdateProductionCounters();
        LimparDadosOrdem();
        KeyPreview = true;
        Shown += ProcessoProdutoAcabadoForm_Shown;
        FormClosing += ProcessoProdutoAcabadoForm_FormClosing;
    }

    private void AplicarConfiguracaoModoConsumo()
    {
        Text = _configuracaoConsumo.TituloTela;
        headerTitleLabel.Text = _configuracaoConsumo.TituloTela;
        headerSubtitleLabel.Text = _configuracaoConsumo.SubtituloTela;
    }

    private string TituloMensagemConsumo => _configuracaoConsumo.TituloTela;

    private string NomeOperacionalConsumo => _configuracaoConsumo.NomeModulo;


    /// <summary>
    /// Correcao 1 (Tarefa 15.1): controles OPERACIONAIS nao causam validacao do campo OP. Assim, clicar
    /// em Confirmar/Enviar/Preview/Iniciar Leitura/F9/F12/acoes laterais NAO dispara o Validated do ComboBox
    /// (que reconsultava a OP e limpava as pesagens). O Validated segue valendo p/ navegacao normal (Tab).
    /// </summary>
    private void ConfigurarCausesValidacaoOperacional()
    {
        Control?[] controles =
        {
            _confirmarConsumoButton, _previewSap261Button, _previewConfirmacaoButton, _enviarConfirmacaoButton, productionActionsButton,
            iniciarLeituraButton, leituraManualButton, lerEtiquetaButton,
            startActionPanel, startActionIconLabel, startActionTextLabel,
            readWeightLegendPanel, readWeightLegendIconLabel, readWeightLegendTextLabel,
            deleteLastLegendPanel, deleteByCodeLegendPanel,
            // Tarefa 18.3: controles de navegacao/titulo NAO devem forcar o Validated do ComboBox de OP.
            closeWindowLabel, minimizeWindowLabel, maximizeWindowLabel, menuHeaderLabel,
            customTitleBarPanel, companyLogoPictureBox, headerTitleLabel, headerSubtitleLabel
        };

        foreach (Control? controle in controles)
        {
            if (controle is not null)
            {
                controle.CausesValidation = false;
            }
        }
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
        // Consumo nao imprime etiqueta: o resumo nao exibe "Etiqueta: ...".
        string tara = _idTaraSelecionada.HasValue
            ? $"{_idTaraSelecionada.Value} (padrão do terminal; será confirmada na pesagem)"
            : "Sem tara";

        return
            $"Selecao automatica -> Setor: {FormatarId(_idSetorSelecionado)}, " +
            $"Balanca: {FormatarId(_idBalancaSelecionada)}, " +
            $"Tara: {tara}.";
    }

    private static string FormatarId(long? valor)
    {
        return valor.HasValue ? valor.Value.ToString() : "Nao definido";
    }

    private static bool PossuiPermissaoLeituraProducao(string acao)
        => AutorizacaoServico.PossuiPermissao(AutorizacaoServico.ModuloProcesso, PermissoesSistema.Rotinas.LeituraProducao, acao);

    // 1) verifica permissao; 2) se negado, AUDITA (await, seguro); 3) mostra mensagem amigavel;
    // 4) retorna true para o chamador abortar a acao.
    private async Task<bool> BloquearAcaoSemPermissaoAsync(string acao, string descricaoAcao)
    {
        if (PossuiPermissaoLeituraProducao(acao))
        {
            return false;
        }

        await global::FugaPET_Dev.Tela.Comum.AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            AutorizacaoServico.ModuloProcesso, PermissoesSistema.Rotinas.LeituraProducao, acao, descricaoAcao, "ProcessoConsumoMaterialForm");

        string mensagem = $"Usuario sem permissao para {descricaoAcao.ToLowerInvariant()} na leitura de consumo.";
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
        // Correcao 4: o RELOGIO so atualiza o rodape; o card "DATA OP" recebe a data DA OP (PreencherOrdemCarregada).
        var ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        DateTime now = DateTime.Now;
        cellDataText.Text = now.ToString("dd/MM/yyyy", ptBr);
        cellHoraText.Text = now.ToString("HH:mm", ptBr);
    }

    private void AtualizarCardDataOrdem(DateTime? dataOrdem)
    {
        var ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        stepLabel.Text = dataOrdem.HasValue
            ? dataOrdem.Value.ToString("dd/MM/yyyy", ptBr)
            : "--/--/----";
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
        closeWindowLabel.Click += (_, _) => FecharTelaSemValidarOrdem();

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));
    }

    private void ReturnToLeituraProducao()
    {
        if (_isProductionStarted)
        {
            statusLabel.Text = "Finalize a leitura antes de sair da tela.";
            return;
        }

        // Tarefa 18.3: marca o fechamento para nao disparar a validacao passiva de OP ao sair.
        _fechandoTela = true;
        _acaoOperacionalEmAndamento = true;
        AutoValidate = AutoValidate.Disable;

        // Padrao da Entrada (evita quebrar o menu): com Owner vivo, apenas fecha — o painel ja esta atras.
        // NAO reativar a navegacao do owner aqui: re-navegar o painel enquanto a tela fecha quebra o menu.
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

    /// <summary>
    /// Tarefa 18.3: fecha a tela sem disparar a validacao passiva do campo de OP. Usado por fechar (X)
    /// e por voltar (menu), evitando o alerta indevido de "informe uma ordem de producao" ao sair sem OP.
    /// </summary>
    private void FecharTelaSemValidarOrdem()
    {
        _fechandoTela = true;
        _acaoOperacionalEmAndamento = true;
        AutoValidate = AutoValidate.Disable;

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
        // Tarefa 18.3: leitura inativa -> fecha normalmente, marcando fechamento (sem validar OP).
        if (!_isProductionStarted)
        {
            _fechandoTela = true;
            return;
        }

        // Leitura ativa: bloqueia o fechamento e restaura as flags (a tela permanece aberta).
        e.Cancel = true;
        _fechandoTela = false;
        _acaoOperacionalEmAndamento = false;
        AutoValidate = AutoValidate.EnableAllowFocusChange;

        statusLabel.Text = "Finalize a leitura antes de sair da tela.";
    }

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

    private void UpdateProductionGridFooter()
    {
        int totalRows = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Count(row => !row.IsNewRow);

        if (totalRows == 0)
        {
            productionFooterLabel.Text = "Exibindo 0 de 0 leituras";
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

        productionFooterLabel.Text = $"Exibindo {firstItem} a {lastItem} de {totalRows} leituras";
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
        productionOrderSearchLabel.Cursor = Cursors.Hand;
        productionOrderSearchLabel.Click += ConsultarOrdemProducao_Click;
        ConfigurarComboOrdem();

        // Selecao de componente no grid secundario mantida por compatibilidade; o grid principal visivel
        // tambem seleciona componentes e e a fonte preferencial da tela.
        materialDataGridView.SelectionChanged += MaterialDataGridView_SelectionChanged;
        materialDataGridView.CellClick += MaterialDataGridView_CellClick;

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

        UpdateProductionState(false);
        SetReadWeightEnabled(false);
        SetDeleteActionsEnabled(false);
        UpdateProductionCounters();
    }

    private async void ConsultarOrdemProducao_Click(object? sender, EventArgs e)
    {
        await ConsultarOrdemProducaoAsync(exibirAvisoOrdemObrigatoria: true);
    }

    /// <summary>
    /// Tarefa 13.1: campo de OP no mesmo padrao do pedidoComboBox da Entrada — ComboBox editavel,
    /// sem autocomplete nativo, com lista de OPs recentes (consultadas com sucesso). Eventos: TextUpdate
    /// (digitacao) limpa a OP anterior; SelectedIndexChanged (escolha na lista) consulta; Enter consulta.
    /// </summary>
    private void ConfigurarComboOrdem()
    {
        // Igual a Entrada (ConfigurarComboPedidos): esconde o icone e deixa o ComboBox ocupar toda a
        // largura do cartao (AjustarLarguraComboOrdem no Resize).
        productionOrderIconPanel.Visible = false;
        productionOrderComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        productionOrderComboBox.AutoCompleteMode = AutoCompleteMode.None;
        productionOrderComboBox.AutoCompleteSource = AutoCompleteSource.None;
        productionOrderComboBox.KeyDown += ProductionOrderComboBox_KeyDown;
        productionOrderComboBox.TextUpdate += ProductionOrderComboBox_TextUpdate;
        productionOrderComboBox.SelectedIndexChanged += ProductionOrderComboBox_SelectedIndexChanged;
        productionOrderComboBox.Validated += ProductionOrderComboBox_Validated;
        productionOrderShadowPanel.Resize += (_, _) => AjustarLarguraComboOrdem();
        AjustarLarguraComboOrdem();
    }

    /// <summary>Estica o ComboBox de OP para ocupar a largura do cartao (mesmo padrao da Entrada).</summary>
    private void AjustarLarguraComboOrdem()
    {
        const int margemDireita = 16;
        int larguraDisponivel = productionOrderShadowPanel.ClientSize.Width - productionOrderComboBox.Left - margemDireita;
        productionOrderComboBox.Width = Math.Max(120, larguraDisponivel);
        productionOrderComboBox.DropDownWidth = Math.Max(220, productionOrderComboBox.Width);
    }

    private async void ProductionOrderComboBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        await ConsultarOrdemProducaoAsync(exibirAvisoOrdemObrigatoria: true);
    }

    /// <summary>
    /// Padrao da Entrada (PedidoComboBox_TextUpdate): ao DIGITAR, limpa os dados da OP carregada
    /// anteriormente (o valor digitado permanece visivel). A consulta continua por Enter/selecao.
    /// </summary>
    private bool PossuiPesagensLocaisNaoSalvas()
        => !_consumoSalvoNaSessao
           && _pesagensPorComponente.Values.Any(lista => lista.Count > 0);

    private void ProductionOrderComboBox_TextUpdate(object? sender, EventArgs e)
    {
        if (_suprimirEventoOrdem)
        {
            return;
        }

        // Correcao 1.3: alterar a OP com pesagens NAO salvas exige confirmacao antes de descartar.
        if (PossuiPesagensLocaisNaoSalvas())
        {
            DialogResult resposta = MessageBox.Show(
                "Existem pesagens locais não salvas. Alterar a OP irá descartar essas pesagens. Deseja continuar?",
                "Alterar Ordem de Produção",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (resposta != DialogResult.Yes)
            {
                DefinirTextoCampoOrdem(_ordemConsumoAtual?.NumeroOrdem ?? string.Empty);
                return;
            }
        }

        if (_ordemConsumoAtual is not null || productionDataGridView.Rows.Count > 0)
        {
            LimparDadosOrdem(limparNumeroOrdem: false);
        }
    }

    /// <summary>Padrao da Entrada (PedidoComboBox_SelectedIndexChanged): selecionar OP da lista consulta.</summary>
    private async void ProductionOrderComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suprimirEventoOrdem)
        {
            return;
        }

        // Selecao real na lista nunca esta vazia -> mantem o aviso de OP obrigatoria.
        await ConsultarOrdemProducaoAsync(exibirAvisoOrdemObrigatoria: true);
    }

    /// <summary>Padrao da Entrada (PedidoComboBox_Validated): ao sair do campo (foco), consulta a OP.</summary>
    /// <summary>
    /// Tarefa 15.1: condicoes em que o campo OP NAO deve reconsultar por perda de foco (acao operacional,
    /// leitura iniciada ou pesagens locais nao salvas). Troca de OP com pesagens passa pelo TextUpdate.
    /// </summary>
    private bool DeveIgnorarValidacaoOrdem()
        => _fechandoTela
           || IsDisposed
           || Disposing
           || _suprimirEventoOrdem
           || _consultandoOrdem
           || _salvandoConsumo
           || _enviandoSap
           || _isProductionStarted
           || _acaoOperacionalEmAndamento
           || PossuiPesagensLocaisNaoSalvas();

    private async void ProductionOrderComboBox_Validated(object? sender, EventArgs e)
    {
        if (DeveIgnorarValidacaoOrdem())
        {
            return;
        }

        // Validacao passiva (perda de foco): OP vazia NAO deve exibir aviso — o usuario pode estar so saindo da tela.
        await ConsultarOrdemProducaoAsync(exibirAvisoOrdemObrigatoria: false);
    }

    private void DefinirTextoCampoOrdem(string texto)
    {
        _suprimirEventoOrdem = true;
        productionOrderComboBox.Text = texto;
        _suprimirEventoOrdem = false;
    }

    /// <summary>
    /// Parte 3: lista de OPs RECENTES (consultadas com sucesso). Sem consulta SAP em massa — apenas
    /// memoriza as OPs ja consultadas. Mantem a digitacao manual (ComboBox editavel).
    /// </summary>
    private void RegistrarOrdemRecente(string numeroOrdem)
    {
        if (string.IsNullOrWhiteSpace(numeroOrdem))
        {
            return;
        }

        string valor = numeroOrdem.Trim();
        if (productionOrderComboBox.Items.Cast<object>()
            .Any(item => string.Equals(item?.ToString(), valor, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _suprimirEventoOrdem = true;
        productionOrderComboBox.Items.Insert(0, valor);
        _suprimirEventoOrdem = false;
    }

    private void RegistrarOrdemRecenteSePermitida(OrdemProducaoConsumo ordem)
    {
        if (!OrdemPertenceAoModoAtual(ordem, out _))
        {
            return;
        }

        RegistrarOrdemRecente(ordem.NumeroOrdem);
    }

    // Tarefa Consumo 22.9.1 (Ajuste 1): classificação do produto produzido da OP mantida APENAS como contexto
    // de diagnóstico (não bloqueia). A separação por modo é feita pelos COMPONENTES (FiltrarComponentesPorModo).
    private bool OrdemPertenceAoModoAtual(
        OrdemProducaoConsumo ordem,
        out ResultadoClassificacaoOrdemConsumo classificacao)
        => _classificadorOrdemConsumo.OrdemPertenceAoModo(ordem, _modoConsumo, out classificacao);

    private void RegistrarDiagnosticoClassificacaoOrdem(
        OrdemProducaoConsumo ordem,
        ResultadoClassificacaoOrdemConsumo classificacao,
        bool aceita)
    {
        System.Diagnostics.Trace.TraceInformation(
            "[Consumo][ClassificacaoOP] "
            + $"Modo da tela: {ClassificadorOrdemConsumoMaterial.NomeModo(_modoConsumo)}; "
            + $"OP: {ordem.NumeroOrdem}; "
            + $"Produto da OP: {ordem.MaterialProduzido}; "
            + $"Campo usado para classificação: {classificacao.CampoUsado}; "
            + $"Classificação encontrada: {ClassificadorOrdemConsumoMaterial.NomeClassificacao(classificacao.Classificacao)}; "
            + $"Resultado: {(aceita ? "Aceita" : "Bloqueada")}");
    }
    private static string NormalizarNumeroOrdem(string? valor)
        => (valor ?? string.Empty).Trim();

    private async Task ConsultarOrdemProducaoAsync(bool exibirAvisoOrdemObrigatoria = true)
    {
        // Correcao 1 (Tarefa 15): nao reentrar (ex.: Validated disparando durante uma consulta em curso).
        if (_consultandoOrdem)
        {
            return;
        }

        string numeroOrdem = NormalizarNumeroOrdem(productionOrderComboBox.Text);

        // Tarefa 18.3: OP vazia — consulta explicita avisa; validacao passiva/fechamento NAO exibe MessageBox.
        if (string.IsNullOrWhiteSpace(numeroOrdem))
        {
            LimparDadosOrdem(limparNumeroOrdem: false);
            statusLabel.Text = "Informe uma ordem de produção para consultar.";

            if (exibirAvisoOrdemObrigatoria && !_fechandoTela && !Disposing && !IsDisposed)
            {
                MessageBox.Show(
                    "Informe uma ordem de produção para consultar.",
                    "Ordem de Produção",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        // Mesma OP ja carregada: NAO reconsulta nem limpa pesagens (protege _pesagensPorComponente).
        if (_ordemConsumoAtual is not null
            && string.Equals(
                NormalizarNumeroOrdem(_ordemConsumoAtual.NumeroOrdem),
                numeroOrdem,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _consultandoOrdem = true;
        try
        {
            ResultadoConsultaOrdemConsumo resultado = await _controller.ConsultarOrdemProducaoAsync(numeroOrdem);

            // OP obrigatoria / nao encontrada / SAP indisponivel: limpa dados e avisa.
            if (resultado.Cenario is CenarioConsultaOrdemConsumo.OrdemObrigatoria
                or CenarioConsultaOrdemConsumo.NaoEncontrada
                or CenarioConsultaOrdemConsumo.Indisponivel
                || resultado.Ordem is null)
            {
                if (resultado.Cenario != CenarioConsultaOrdemConsumo.OrdemObrigatoria)
                {
                    LimparDadosOrdem(limparNumeroOrdem: false);
                }

                statusLabel.Text = resultado.Mensagem;
                MessageBox.Show(resultado.Mensagem, "Ordem de Produção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Tarefa Consumo 22.9.1 (Ajuste 1): o PRODUTO PRODUZIDO da OP é apenas CONTEXTO no topo da tela e
            // NÃO bloqueia a OP sozinho. A separação Matéria-Prima × Químico passa a ser feita pelos COMPONENTES
            // da OP (ProductType do Product Master), no filtro por modo (FiltrarComponentesPorModo). Aqui só
            // registramos diagnóstico da classificação do produto produzido — sem bloquear.
            OrdemPertenceAoModoAtual(resultado.Ordem, out ResultadoClassificacaoOrdemConsumo classificacaoOrdem);
            RegistrarDiagnosticoClassificacaoOrdem(resultado.Ordem, classificacaoOrdem, aceita: true);

            // Tarefa Consumo 22.10.1 (Ajuste 6): busca as descrições reais (A_ProductDescription) dos componentes
            // ANTES do enriquecimento — o join lógico é feito por código de produto em BuscarTipoMaterialSap.
            _descricoesProdutoPorCodigo = await _controller.ObterDescricoesComponentesAsync(
                resultado.Ordem.Componentes.Select(componente => componente.CodigoMaterial));

            // Tarefa Consumo 22.9.2 (Ajustes 1/2/5): classifica/filtra os componentes pelo MODO da tela ANTES de
            // carregar a operação. Se não sobrar nenhum componente compatível, a OP NÃO abre operacional (grid
            // vazia parecendo sucesso): bloqueia com alerta e volta ao estado inicial (mantendo só o número da OP).
            IReadOnlyList<ComponenteConsumoMaterial> componentesModo =
                EnriquecerEClassificarComponentesDoModo(resultado.Ordem);

            if (componentesModo.Count == 0)
            {
                BloquearOrdemIncompativelComModo(resultado.Ordem, resultado.NumeroOrdem);
                return;
            }

            // OP carregada (liberada) ou nao liberada: cabecalho exibido; pesagem so libera com componente
            // pesavel selecionado.
            PreencherOrdemCarregada(resultado.Ordem, componentesModo);
            DefinirTextoCampoOrdem(resultado.NumeroOrdem);
            RegistrarOrdemRecenteSePermitida(resultado.Ordem); // OP aceita no modo atual entra na lista recente
            statusLabel.Text = resultado.Mensagem;

            if (resultado.Cenario is CenarioConsultaOrdemConsumo.NaoLiberada)
            {
                MessageBox.Show(resultado.Mensagem, "Ordem de Produção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _consultandoOrdem = false;
        }
    }

    // Tarefa Consumo 22.9.2: recebe a lista JÁ classificada/filtrada pelo modo (não vazia — a garantia de
    // "pelo menos um componente compatível" é feita em ConsultarOrdemProducaoAsync antes de chamar este método).
    private void PreencherOrdemCarregada(
        OrdemProducaoConsumo ordem,
        IReadOnlyList<ComponenteConsumoMaterial> componentesModo)
    {
        // Reaproveita a limpeza padrao e, em seguida, preenche cabecalho + grid de componentes.
        LimparDadosOrdem(limparNumeroOrdem: false);
        _ordemConsumoAtual = ordem;
        _componenteConsumoSelecionado = null;

        // Cabecalho da OP (Correcao 5): preenche campos existentes, sem redesenhar layout.
        DefinirTextoCampoOrdem(ordem.NumeroOrdem);
        plantaValueLabel.Text = ordem.Planta;
        AtualizarCardDataOrdem(ordem.DataOrdem); // Correcao 4: card "DATA OP" recebe a data da OP (nao o relogio).
        // Produto semiacabado exibido UMA vez (textbox unico ajustado ao card); o segundo textbox fica oculto.
        finishedProductCodeTextBox.Text = ordem.MaterialProduzido;
        finishedProductTextBox.Text = string.Empty;
        lotTextBox.Text = ordem.LoteProdutoProduzido;
        readForecastPackagesTextBox.Text = ordem.QuantidadePrevista > 0
            ? $"{ordem.QuantidadePrevista:0.###} {ordem.Unidade}".Trim()
            : string.Empty;

        // Grid principal visivel: lista os componentes da OP. Guarda contra eventos de selecao durante
        // o preenchimento para nao habilitar leitura antes de clique do usuario.
        _atualizandoComponentes = true;
        materialDataGridView.Rows.Clear();
        productionDataGridView.Rows.Clear();
        foreach (ComponenteConsumoMaterial componente in componentesModo)
        {
            string unidade = string.IsNullOrWhiteSpace(componente.UnidadeMedida) ? "KG" : componente.UnidadeMedida;
            decimal previstoInicial = ObterQuantidadePrevistaInicial(componente);
            decimal utilizadoInicial = Math.Max(0m, componente.QuantidadeConsumida);
            decimal saldoInicial = Math.Max(0m, previstoInicial - utilizadoInicial);
            string previsto = FormatarPesoGrid(previstoInicial, unidade);
            int indicePrincipal = productionDataGridView.Rows.Add(
                componente.CodigoMaterial,
                ObterDescricaoProdutoGrid(componente),
                ObterOperacaoGrid(componente),  // Tarefa Consumo 22.1: Operação SAP ao lado da descrição
                componente.NumeroReserva,
                componente.ItemReserva,
                componente.DepositoConsumo,
                ObterLoteComponenteGrid(componente),
                ObterTipoSapGrid(componente),
                previsto,                       // Peso Previsto (FIXO — quantidade original da OP)
                FormatarPesoGrid(utilizadoInicial, unidade), // Peso Utilizado (SAP + local)
                FormatarPesoGrid(saldoInicial, unidade));    // Saldo Restante
            DataGridViewRow linhaPrincipal = productionDataGridView.Rows[indicePrincipal];
            linhaPrincipal.Tag = componente;
            AplicarStatusVisualComponente(linhaPrincipal, componente);
            DefinirTooltipLinha(linhaPrincipal, ObterTooltipComponente(componente));

            int indiceSecundario = materialDataGridView.Rows.Add(
                componente.Status,
                componente.CodigoMaterial,
                componente.DescricaoMaterial,
                componente.Lote,
                string.Empty,
                FormatarPesoGrid(saldoInicial, componente.UnidadeMedida));
            materialDataGridView.Rows[indiceSecundario].Tag = componente;
        }

        productionDataGridView.ClearSelection();
        productionDataGridView.CurrentCell = null;
        materialDataGridView.ClearSelection();
        materialDataGridView.CurrentCell = null;
        _atualizandoComponentes = false;

        // OP carregada: inicio de leitura permanece BLOQUEADO ate selecionar um componente pesavel.
        AtualizarLiberacaoInicioLeitura();
        AtualizarApontamentoVisual(null);
    }

    // Tarefa Consumo 22.9.2 (Ajuste 1): enriquece os componentes com o tipo mestre (Product Master) e devolve
    // APENAS os compatíveis com o modo atual da tela. O enriquecimento roda UMA vez por consulta (evita Trace
    // duplicado); a classificação usa ProductType/ProductGroup quando disponível (rollout: Indefinido no filtro).
    private IReadOnlyList<ComponenteConsumoMaterial> EnriquecerEClassificarComponentesDoModo(OrdemProducaoConsumo ordem)
    {
        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            ordem.Componentes,
            BuscarTipoMaterialSap,
            modoDaTela: ClassificadorOrdemConsumoMaterial.NomeModo(_modoConsumo),
            opNumero: ordem.NumeroOrdem,
            produtoProduzido: ordem.MaterialProduzido);

        return FiltrarComponentesPorModo(ordem.Componentes);
    }

    // Tarefa Consumo 22.9.2 (Ajustes 2/3/4/5/6/7): OP sem NENHUM componente compatível com o modo NÃO abre
    // operacional. Volta ao estado inicial (cards/grid/painel limpos, ações desabilitadas), mantém só o número
    // digitado, exibe alerta por modo e devolve o foco ao campo OP. O produto produzido NÃO bloqueia sozinho.
    private void BloquearOrdemIncompativelComModo(OrdemProducaoConsumo ordem, string numeroOrdem)
    {
        RegistrarDiagnosticoOpIncompativel(ordem);

        LimparDadosOrdem(limparNumeroOrdem: false); // limpa dados operacionais; Iniciar/F9/F12/Confirmar desabilitados
        DefinirTextoCampoOrdem(numeroOrdem);        // mantém o número consultado para o usuário saber o que tentou

        AtualizarApontamentoOpIncompativel();       // apontamentoInfoPanel + sapStatusPanel de bloqueio
        statusLabel.Text = "OP incompatível com esta tela. Informe uma OP compatível.";

        (string titulo, string mensagem) = MontarMensagemOpIncompativel(
            _modoConsumo, ordem.NumeroOrdem, ordem.MaterialProduzido, ordem.Componentes.Count);
        MessageBox.Show(mensagem, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        DevolverFocoParaCampoOrdem(); // foco de volta no campo OP (Ajuste 6/14)
    }

    // Ajustes 3/4: título/mensagem por modo, com contagem de componentes quando a OP possuir componentes.
    // internal static para permitir teste direto (InternalsVisibleTo) sem instanciar o Form.
    internal static (string titulo, string mensagem) MontarMensagemOpIncompativel(
        ModoConsumoMaterial modo, string? numeroOrdem, string? produtoProduzido, int totalComponentes)
    {
        string op = string.IsNullOrWhiteSpace(numeroOrdem) ? "(não informada)" : numeroOrdem.Trim();
        string produto = string.IsNullOrWhiteSpace(produtoProduzido) ? "(não informado)" : produtoProduzido.Trim();

        bool quimico = modo == ModoConsumoMaterial.Quimico;
        string titulo = quimico
            ? "OP não pertence ao Consumo Químico"
            : "OP não pertence ao Consumo de Matéria-Prima";
        string tipoCompativel = quimico ? "Consumo Químico" : "Consumo de Matéria-Prima";
        string telaAlternativa = quimico ? "Consumo de Matéria-Prima" : "Consumo Químico";
        string rotuloContagem = quimico ? "Componentes químicos encontrados" : "Componentes de matéria-prima encontrados";

        string mensagem =
            $"Esta OP não possui componentes classificados para {tipoCompativel}.\r\n\r\n"
            + $"OP: {op}\r\n"
            + $"Produto da OP: {produto}\r\n\r\n";

        if (totalComponentes > 0)
        {
            mensagem += $"Componentes encontrados: {totalComponentes}\r\n"
                + $"{rotuloContagem}: 0\r\n\r\n";
        }

        mensagem += $"Use a tela de {telaAlternativa} ou verifique a classificação dos componentes no SAP.";

        return (titulo, mensagem);
    }

    // Ajustes 2/6: painel de orientação + status SAP explícitos de OP incompatível (não parece consulta concluída).
    private void AtualizarApontamentoOpIncompativel()
    {
        apontamentoChipCaptionLabel.Text = "ROTA SAP";
        apontamentoInfoCaptionLabel.Text = "ORIENTAÇÃO";
        apontamentoChipValueLabel.Text = "Bloqueado";
        AtualizarApontamentoInfo(
            "OP incompatível\ncom esta tela.",
            "OP incompatível com esta tela. Informe uma OP compatível.");
        AtualizarEstadoVisualIntegracaoSapConsumo(
            EstadoVisualIntegracaoSapConsumo.BloqueadoOpIncompativel,
            "OP incompatível com esta tela.");
    }

    private void DevolverFocoParaCampoOrdem()
    {
        if (productionOrderComboBox.CanFocus)
        {
            productionOrderComboBox.Focus();
        }
    }

    // Ajuste 7: diagnóstico da OP bloqueada por modo (contagens por classificação de componente).
    private void RegistrarDiagnosticoOpIncompativel(OrdemProducaoConsumo ordem)
    {
        int materiaPrima = 0, quimico = 0, embalagem = 0, outro = 0, indefinido = 0;
        foreach (ComponenteConsumoMaterial componente in ordem.Componentes)
        {
            switch (componente.ClassificacaoConsumo)
            {
                case ClassificacaoConsumoMaterial.MateriaPrima: materiaPrima++; break;
                case ClassificacaoConsumoMaterial.Quimico: quimico++; break;
                case ClassificacaoConsumoMaterial.Embalagem: embalagem++; break;
                case ClassificacaoConsumoMaterial.Outro: outro++; break;
                default: indefinido++; break;
            }
        }

        System.Diagnostics.Trace.TraceInformation(
            "[Consumo][OpIncompativel] "
            + $"Modo da tela: {ClassificadorOrdemConsumoMaterial.NomeModo(_modoConsumo)}; "
            + $"OP consultada: {ordem.NumeroOrdem}; "
            + $"Produto da OP: {ordem.MaterialProduzido}; "
            + $"Total de componentes da OP: {ordem.Componentes.Count}; "
            + "Total de componentes compativeis com o modo: 0; "
            + $"MateriaPrima(ROH): {materiaPrima}; Quimico(HIBE): {quimico}; Embalagem(VERP): {embalagem}; "
            + $"Outro: {outro}; Indefinido: {indefinido}; "
            + "Resultado: OP bloqueada para o modo atual");
    }

    // Tarefa Consumo 22.9.1/22.9.2 (Ajustes 5/6): separação por COMPONENTE via ProductType (Product Master),
    // e não mais pelo produto produzido da OP. A OP é aceita quando tem >=1 componente do modo; se a lista
    // filtrada ficar vazia, ConsultarOrdemProducaoAsync bloqueia a abertura operacional (22.9.2).
    private IReadOnlyList<ComponenteConsumoMaterial> FiltrarComponentesPorModo(
        IReadOnlyList<ComponenteConsumoMaterial> componentes)
        => componentes
            .Where(ComponentePertenceAoModoAtual)
            .ToList();

    // Tarefa Consumo 22.10.1: seam do Product Master. A DESCRIÇÃO (A_ProductDescription) já é real — o mapa
    // _descricoesProdutoPorCodigo é preenchido em ConsultarOrdemProducaoAsync (GET governado). O mestre retornado
    // traz só a descrição (Consultado=false), então a CLASSIFICAÇÃO por ProductType (A_Product) segue no rollout
    // atual (Indefinido) — separação Matéria-Prima/Químico inalterada. A_Product real fica para tarefa futura.
    private ProdutoSapMestre? BuscarTipoMaterialSap(string codigoMaterial)
        => _descricoesProdutoPorCodigo.GetValueOrDefault((codigoMaterial ?? string.Empty).Trim());

    private bool ComponentePertenceAoModoAtual(ComponenteConsumoMaterial componente)
        => _modoConsumo == ModoConsumoMaterial.MateriaPrima
            ? ComponenteEhMateriaPrima(componente)
            : ComponenteEhQuimico(componente);

    // Ajuste 6 (rollout): enquanto o Product Master não estiver ligado à consulta real, os componentes
    // ficam como Indefinido. Para NÃO reproduzir o bug (OP sumindo do modo Matéria-Prima), o modo
    // Matéria-Prima aceita MatériaPrima E Indefinido; o modo Químico exige explicitamente Químico.
    // Quando a consulta ao Product Master estiver homologada, Indefinido passará a bloquear (Ajuste 10).
    private static bool ComponenteEhMateriaPrima(ComponenteConsumoMaterial componente)
        => componente.ClassificacaoConsumo is ClassificacaoConsumoMaterial.MateriaPrima
            or ClassificacaoConsumoMaterial.Indefinido;

    private static bool ComponenteEhQuimico(ComponenteConsumoMaterial componente)
        => componente.ClassificacaoConsumo == ClassificacaoConsumoMaterial.Quimico;

    // Tarefa Consumo 22.10 (Ajuste 5/6/12): a coluna "Descrição Produto" exibe a descrição REAL
    // (A_ProductDescription.ProductDescription, aplicada no componente pelo enriquecimento) ou a descrição da OP.
    // Sem descrição do SAP, usa o fallback controlado — NÃO mais "Material <codigo>".
    internal const string DescricaoProdutoNaoRetornada = "Descrição não retornada pelo SAP";

    private static string ObterDescricaoProdutoGrid(ComponenteConsumoMaterial componente)
        => string.IsNullOrWhiteSpace(componente.DescricaoMaterial)
            ? DescricaoProdutoNaoRetornada
            : componente.DescricaoMaterial.Trim();

    // Tarefa Consumo 22.1: operação SAP do componente (ManufacturingOrderOperation), "-" se vazia.
    private static string ObterOperacaoGrid(ComponenteConsumoMaterial componente)
        => string.IsNullOrWhiteSpace(componente.Operacao)
            ? "-"
            : componente.Operacao.Trim();

    // Coluna "Tipo SAP" curta (classificacao); o motivo completo vai no tooltip/status.
    private static string ObterTipoSapGrid(ComponenteConsumoMaterial componente)
    {
        if (string.IsNullOrWhiteSpace(componente.DepositoConsumo))
        {
            return "Sem depósito";
        }

        return componente.ClassificacaoEnvio switch
        {
            ClassificacaoEnvioConsumo261.MaterialDocument261Direto => "261 Direto",
            ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao => "Backflush",
            _ => "Bloqueado"
        };
    }

    // Tarefa Consumo 22.2: lote sempre do SAP; sem lote é bloqueante → mostra "Sem lote" (não "Não informado").
    private static string ObterLoteComponenteGrid(ComponenteConsumoMaterial componente)
        => string.IsNullOrWhiteSpace(componente.Lote) ? "Sem lote" : componente.Lote.Trim();

    // Texto completo (motivo) para tooltip/status — fora da coluna para nao poluir a grid.
    private static string ObterTooltipComponente(ComponenteConsumoMaterial componente)
    {
        if (string.IsNullOrWhiteSpace(componente.DepositoConsumo))
        {
            return "Componente sem depósito de consumo. Pesagem bloqueada.";
        }

        if (componente.QuantidadePendenteSapOriginal < 0m)
        {
            return $"Saldo SAP negativo: {componente.QuantidadePendenteSapOriginal:0.000} kg. Pesagem bloqueada.";
        }

        if (componente.BackflushSap)
        {
            return "Backflush SAP — não enviar por 261 direto; pode exigir confirmação de produção.";
        }

        if (!componente.ElegivelMaterialDocument261Direto)
        {
            return $"Envio 261 direto bloqueado: {componente.MotivoInelegibilidadeMaterialDocument261}";
        }

        if (!componente.PesagemLiberada)
        {
            return ObterMotivoComponenteBloqueado(componente);
        }

        return $"Reserva {componente.NumeroReserva}/{componente.ItemReserva} — elegível para 261 direto.";
    }

    private static void DefinirTooltipLinha(DataGridViewRow linha, string texto)
    {
        foreach (DataGridViewCell celula in linha.Cells)
        {
            celula.ToolTipText = texto;
        }
    }

    private static string ObterMotivoComponenteBloqueado(ComponenteConsumoMaterial componente)
        => string.Equals(componente.Status, ComponenteConsumoMaterial.StatusConsumido, StringComparison.OrdinalIgnoreCase)
            ? "componente já consumido"
            : "componente não liberado para pesagem";

    private bool ComponentePodeOperar(ComponenteConsumoMaterial? componente, out string motivoBloqueio)
    {
        motivoBloqueio = string.Empty;

        if (componente is null)
        {
            motivoBloqueio = "Selecione um componente da ordem.";
            return false;
        }

        string tipoSap = ObterTipoSapGrid(componente);

        if (tipoSap.Contains("Bloqueado", StringComparison.OrdinalIgnoreCase))
        {
            motivoBloqueio = string.IsNullOrWhiteSpace(componente.MotivoInelegibilidadeMaterialDocument261)
                ? "Tipo SAP bloqueado para consumo operacional nesta tela."
                : $"Tipo SAP bloqueado: {componente.MotivoInelegibilidadeMaterialDocument261}";
            return false;
        }

        if (tipoSap.Contains("Sem dep?sito", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(componente.DepositoConsumo))
        {
            motivoBloqueio = "Componente sem dep?sito de consumo informado pelo SAP.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(componente.Lote))
        {
            motivoBloqueio = "Componente sem lote SAP informado.";
            return false;
        }

        if (tipoSap.Contains("Backflush", StringComparison.OrdinalIgnoreCase)
            || componente.BackflushSap
            || componente.ClassificacaoEnvio == ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao)
        {
            motivoBloqueio = "Componente Backflush n?o pode ser operado por 261 direto nesta tela.";
            return false;
        }

        if (!string.Equals(tipoSap, "261 Direto", StringComparison.OrdinalIgnoreCase)
            || componente.ClassificacaoEnvio != ClassificacaoEnvioConsumo261.MaterialDocument261Direto
            || !componente.ElegivelMaterialDocument261Direto)
        {
            motivoBloqueio = string.IsNullOrWhiteSpace(componente.MotivoInelegibilidadeMaterialDocument261)
                ? "Componente n?o classificado como 261 Direto."
                : componente.MotivoInelegibilidadeMaterialDocument261;
            return false;
        }

        if (!ComponentePertenceAoModoAtual(componente))
        {
            motivoBloqueio = $"Componente n?o pertence ao modo {NomeOperacionalConsumo}.";
            return false;
        }

        if (!ConsumoMaterialServico.AvaliarLiberacaoPesagem(componente, out string motivoLiberacao))
        {
            motivoBloqueio = motivoLiberacao;
            return false;
        }

        if (ConsumoMaterialServico.CalcularDisponivelConsumoComTolerancia(componente) <= 0m)
        {
            motivoBloqueio = "Componente sem saldo dispon?vel para nova pesagem.";
            return false;
        }

        if (!componente.PesagemLiberada)
        {
            motivoBloqueio = string.IsNullOrWhiteSpace(componente.MotivoBloqueioPesagem)
                ? ObterMotivoComponenteBloqueado(componente)
                : componente.MotivoBloqueioPesagem;
            return false;
        }

        return true;
    }

    private void BloquearComponenteOperacional(ComponenteConsumoMaterial? componente, string motivoBloqueio)
    {
        string mensagem = string.IsNullOrWhiteSpace(motivoBloqueio)
            ? "Opera??o bloqueada para este componente."
            : $"Opera??o bloqueada para este componente. Motivo: {motivoBloqueio}";

        statusLabel.Text = mensagem;
        AtualizarApontamentoVisual(componente, mensagem);
        AtualizarEstadoVisualIntegracaoSapConsumo(
            componente is not null && string.IsNullOrWhiteSpace(componente.DepositoConsumo)
                ? EstadoVisualIntegracaoSapConsumo.BloqueadoSemDeposito
                : EstadoVisualIntegracaoSapConsumo.Bloqueado,
            motivoBloqueio);
        SetReadWeightEnabled(false);

        if (!_isProductionStarted)
        {
            AtualizarLiberacaoInicioLeitura();
        }

        if (_confirmarConsumoButton is not null)
        {
            _confirmarConsumoButton.Enabled = false;
        }

        if (_previewSap261Button is not null)
        {
            _previewSap261Button.Enabled = false;
        }

        if (_enviarSap261Button is not null)
        {
            _enviarSap261Button.Enabled = false;
        }
    }

    private string MontarMensagemOperacaoBloqueada(string motivoBloqueio)
        => string.IsNullOrWhiteSpace(motivoBloqueio)
            ? "Opera??o bloqueada para este componente."
            : $"Opera??o bloqueada para este componente.\r\nMotivo: {motivoBloqueio}";

    private bool ValidarComponentesComPesagemPendentesOperaveis(out string mensagem)
    {
        mensagem = string.Empty;

        if (_ordemConsumoAtual is null)
        {
            return true;
        }

        foreach (ComponenteConsumoMaterial componente in _ordemConsumoAtual.Componentes)
        {
            string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
            if (!_pesagensPorComponente.TryGetValue(chave, out List<PesagemConsumoMaterial>? pesagens)
                || pesagens.Count == 0)
            {
                continue;
            }

            if (ComponentePodeOperar(componente, out string motivoBloqueio))
            {
                continue;
            }

            _componenteConsumoSelecionado = componente;
            BloquearComponenteOperacional(componente, motivoBloqueio);
            mensagem = MontarMensagemOperacaoBloqueada(motivoBloqueio);
            return false;
        }

        return true;
    }

    private bool ValidarComponenteSelecionadoParaSap261(out string mensagem)
    {
        mensagem = string.Empty;

        if (_componenteConsumoSelecionado is null)
        {
            return true;
        }

        if (ComponentePodeOperar(_componenteConsumoSelecionado, out string motivoBloqueio))
        {
            return true;
        }

        BloquearComponenteOperacional(_componenteConsumoSelecionado, motivoBloqueio);
        mensagem = MontarMensagemOperacaoBloqueada(motivoBloqueio);
        return false;
    }

    private static void AplicarStatusVisualComponente(DataGridViewRow linha, ComponenteConsumoMaterial componente)
    {
        if (componente.PesagemLiberada)
        {
            return;
        }

        linha.DefaultCellStyle.ForeColor = Color.FromArgb(107, 114, 128);
        linha.DefaultCellStyle.SelectionForeColor = Color.White;
    }

    private void MaterialDataGridView_SelectionChanged(object? sender, EventArgs e)
    {
        // O grid secundario e somente informativo neste fluxo. A selecao operacional deve vir
        // exclusivamente do productionDataGridView para evitar componente fantasma.
    }

    private void MaterialDataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        // O grid secundario nao altera _componenteConsumoSelecionado.
    }

    private void AtualizarComponenteSelecionado()
        => AtualizarComponenteSelecionadoDoGrid();

    private void AtualizarComponenteSelecionadoDoGrid()
    {
        // Correcao 2: so ignora durante uma LEITURA DE PESO em andamento; permite trocar de componente
        // com a leitura ativa (sem precisar parar/iniciar de novo).
        if (_isReadingWeight || _restaurandoSelecaoLinhaComponentes)
        {
            return;
        }

        CapturarComponenteSelecionadoDoGridPrincipal();
    }

    private bool CapturarComponenteSelecionadoDoGridPrincipal()
    {
        if (_atualizandoComponentes)
        {
            return false;
        }

        DataGridViewRow? linhaSelecionada = ObterLinhaSelecionadaNoGridPrincipal();
        _componenteConsumoSelecionado = linhaSelecionada?.Tag as ComponenteConsumoMaterial;

        if (_componenteConsumoSelecionado is null)
        {
            UpdateProductionCounters();
            AtualizarApontamentoVisual(null, "Selecione um componente");
            AtualizarLiberacaoInicioLeitura();
            return false;
        }

        RegistrarDiagnosticoSelecaoConsumo(_componenteConsumoSelecionado, linhaSelecionada?.Index ?? -1);

        AtualizarTotaisConsumo(
            _componenteConsumoSelecionado,
            ProcessoConsumoMaterialController.ChaveComponente(_componenteConsumoSelecionado));

        if (!ComponentePodeOperar(_componenteConsumoSelecionado, out string motivoBloqueio))
        {
            BloquearComponenteOperacional(_componenteConsumoSelecionado, motivoBloqueio);
            return false;
        }

        statusLabel.Text = $"Componente {_componenteConsumoSelecionado.CodigoMaterial} selecionado. Inicie a leitura de consumo.";
        AtualizarLiberacaoInicioLeitura();
        return true;
    }

    private ComponenteConsumoMaterial? ObterComponenteSelecionadoNoGridPrincipal()
        => ObterLinhaSelecionadaNoGridPrincipal()?.Tag as ComponenteConsumoMaterial;

    /// <summary>
    /// Correcao 1 (Tarefa 14.1): valida o componente DA LINHA ATUAL para pesagem ANTES de abrir peso
    /// manual (F9) ou ler a balanca (F12). Recaptura a linha, aplica a regra central
    /// (<see cref="ConsumoMaterialServico.AvaliarLiberacaoPesagem"/>) e bloqueia sem deposito/saldo/unidade.
    /// </summary>
    private bool ValidarComponenteAtualParaPesagem(out ComponenteConsumoMaterial? componente, out string mensagem)
    {
        componente = null;
        mensagem = string.Empty;

        CapturarComponenteSelecionadoDoGridPrincipal();

        if (_ordemConsumoAtual is null)
        {
            mensagem = "Carregue uma ordem de produ??o v?lida para pesar.";
            return false;
        }

        if (_componenteConsumoSelecionado is null)
        {
            mensagem = "Selecione um componente da ordem antes de pesar.";
            return false;
        }

        componente = _componenteConsumoSelecionado;

        if (!ComponentePodeOperar(componente, out string motivoBloqueio))
        {
            BloquearComponenteOperacional(componente, motivoBloqueio);
            mensagem = MontarMensagemOperacaoBloqueada(motivoBloqueio);
            return false;
        }

        return true;
    }

    private DataGridViewRow? ObterLinhaSelecionadaNoGridPrincipal()
    {
        DataGridViewRow? row = null;

        if (productionDataGridView.CurrentCell is not null
            && productionDataGridView.CurrentCell.RowIndex >= 0
            && productionDataGridView.CurrentCell.RowIndex < productionDataGridView.Rows.Count)
        {
            row = productionDataGridView.Rows[productionDataGridView.CurrentCell.RowIndex];
        }

        if (row is null || row.IsNewRow)
        {
            row = productionDataGridView.CurrentRow;
        }

        return row is not null && !row.IsNewRow
            ? row
            : null;
    }

    private void RegistrarDiagnosticoSelecaoConsumo(ComponenteConsumoMaterial componente, int rowIndex)
        => System.Diagnostics.Trace.WriteLine(
            $"Seleção consumo: material={componente.CodigoMaterial}, reserva={componente.NumeroReserva}/{componente.ItemReserva}, pendente={componente.QuantidadePendente:0.###}, rowIndex={rowIndex}");

    /// <summary>
    /// Libera o inicio da leitura de consumo apenas quando ha OP carregada + componente selecionado
    /// com pesagem liberada (e permissao). Nunca habilita durante a leitura em andamento.
    /// </summary>
    private void AtualizarLiberacaoInicioLeitura()
    {
        if (_isProductionStarted)
        {
            return;
        }

        // Tarefa 18.2 (padrao Entrada): habilita INICIAR com OP valida carregada (sem exigir clique em linha).
        bool habilitar = PodeIniciarLeituraConsumo();

        startActionPanel.Enabled = habilitar;
        startActionPanel.BackColor = habilitar ? ActionEnabledColor : ActionDisabledColor;
        startActionTextLabel.ForeColor = habilitar ? EnabledLegendTextColor : DisabledLegendTextColor;
        startActionIconLabel.Enabled = habilitar;
        startActionTextLabel.Enabled = habilitar;
        startActionPanel.Cursor = habilitar ? Cursors.Hand : Cursors.Default;
        startActionIconLabel.Cursor = startActionPanel.Cursor;
        startActionTextLabel.Cursor = startActionPanel.Cursor;
        iniciarLeituraButton.BaseBackColor = habilitar ? ReadingStatusActiveColor : ActionDisabledColor;
        iniciarLeituraButton.Enabled = habilitar;
        iniciarLeituraButton.Cursor = habilitar ? Cursors.Hand : Cursors.Default;
        iniciarLeituraButton.Invalidate();
    }

    /// <summary>Tarefa 18.2 (padrao Entrada): pode iniciar leitura = permissao EXECUTAR + OP valida carregada.</summary>
    private bool PodeIniciarLeituraConsumo()
        => PossuiPermissaoLeituraProducao(AutorizacaoServico.AcaoExecutar)
           && OrdemConsumoSelecionadaValida();

    private bool OrdemConsumoSelecionadaValida()
    {
        string numeroOrdem = NormalizarNumeroOrdem(productionOrderComboBox.Text);
        return _ordemConsumoAtual is not null
            && !string.IsNullOrWhiteSpace(numeroOrdem)
            && string.Equals(
                NormalizarNumeroOrdem(_ordemConsumoAtual.NumeroOrdem),
                numeroOrdem,
                StringComparison.OrdinalIgnoreCase)
            && _componenteConsumoSelecionado is not null
            && ComponentePodeOperar(_componenteConsumoSelecionado, out _);
    }

    private void LimparDadosOrdem(bool limparNumeroOrdem = true)
    {
        if (limparNumeroOrdem)
        {
            _suprimirEventoOrdem = true;
            productionOrderComboBox.Text = string.Empty;
            productionOrderComboBox.SelectedIndex = -1;
            _suprimirEventoOrdem = false;
        }

        lotTextBox.Clear();
        plantaValueLabel.Text = string.Empty;
        AtualizarCardDataOrdem(null); // Correcao 4: sem OP, volta para "--/--/----".
        finishedProductCodeTextBox.Clear();
        finishedProductTextBox.Clear();
        ovenExitTextBox.Clear();
        classificationDateTextBox.Clear();
        manufacturingDateTextBox.Clear();
        expirationDateTextBox.Clear();
        readForecastPackagesTextBox.Clear();
        readForecastBoxesTextBox.Clear();
        productionSearchTextBox.Clear();
        materialDataGridView.Rows.Clear();
        productionDataGridView.Rows.Clear();
        _isProductionStarted = false;
        _isReadingWeight = false;
        _ordemConsumoAtual = null;
        _componenteConsumoSelecionado = null;
        _pesagensPorComponente.Clear();
        _tarasPorComponente.Clear();
        _rotaEnvioSalva = RotaEnvioConsumo.Bloqueado;
        _lancamentoComFalhaSap = false;
        _proximaSequenciaPesagem = 1;
        _consumoSalvoNaSessao = false;
        _ultimoCodigoLancamentoSalvo = null;
        UpdateProductionState(false);
        BloquearAcoesSemOrdemCarregada();
        AtualizarBotaoConfirmar();
        UpdateProductionCounters();
        ClearGridSelections();
        AtualizarApontamentoVisual(null);
        statusLabel.Text = ConsumoMaterialServico.MensagemEstadoInicial;
    }

    private void BloquearAcoesSemOrdemCarregada()
    {
        startActionPanel.Enabled = false;
        startActionIconLabel.Enabled = false;
        startActionTextLabel.Enabled = false;
        startActionPanel.Cursor = Cursors.Default;
        startActionIconLabel.Cursor = Cursors.Default;
        startActionTextLabel.Cursor = Cursors.Default;
        iniciarLeituraButton.Enabled = false;
        iniciarLeituraButton.Cursor = Cursors.Default;
        SetReadWeightEnabled(false);
        SetDeleteActionsEnabled(false);
    }

    private bool PossuiOrdemEComponenteValido()
        => _ordemConsumoAtual is not null
           && _componenteConsumoSelecionado is not null
           && _componenteConsumoSelecionado.PesagemLiberada;

    /// <summary>
    /// OP carregada com PELO MENOS UM componente liberado para pesagem. Habilita "Iniciar Leitura"
    /// assim que a OP valida e carregada (sem exigir clique previo numa linha do grid).
    /// </summary>
    private bool PossuiOrdemComComponentePesavel()
        => _ordemConsumoAtual is not null
           && _ordemConsumoAtual.Componentes.Any(componente => componente.PesagemLiberada);

    /// <summary>
    /// Auto-seleção SEGURA do primeiro componente pesável: se já houver uma linha selecionada VÁLIDA
    /// (pesável), mantém-na (não sobrescreve a escolha manual). Caso contrário, percorre as LINHAS REAIS
    /// do grid, seleciona/destaca a primeira pesável, sincroniza <see cref="_componenteConsumoSelecionado"/>,
    /// painel lateral, botões e status. Retorna false quando não há componente pesável. Sem efeito SAP.
    /// </summary>
    private bool SelecionarPrimeiroComponentePesavelSeNecessario()
    {
        // Correcao 4: preserva selecao manual valida (nao troca para o primeiro componente).
        ComponenteConsumoMaterial? atual = ObterComponenteSelecionadoNoGridPrincipal();
        if (atual is not null && ComponentePodeOperar(atual, out _))
        {
            _componenteConsumoSelecionado = atual;
            AtualizarTotaisConsumo(atual, ProcessoConsumoMaterialController.ChaveComponente(atual));
            return true;
        }

        // Auto-seleciona a primeira LINHA REAL pesavel do grid (destaca + sincroniza).
        foreach (DataGridViewRow linha in productionDataGridView.Rows)
        {
            if (linha.IsNewRow || !linha.Visible)
            {
                continue;
            }

            if (linha.Tag is not ComponenteConsumoMaterial componente
                || !ComponentePodeOperar(componente, out _))
            {
                continue;
            }

            _atualizandoComponentes = true;
            productionDataGridView.ClearSelection();
            linha.Selected = true;
            if (linha.Cells.Count > 0)
            {
                productionDataGridView.CurrentCell = linha.Cells[0];
            }

            try
            {
                productionDataGridView.FirstDisplayedScrollingRowIndex = linha.Index;
            }
            catch (InvalidOperationException)
            {
                // Scroll opcional; ignora se a linha ainda nao for rolavel.
            }

            _atualizandoComponentes = false;

            _componenteConsumoSelecionado = componente;
            AtualizarTotaisConsumo(componente, ProcessoConsumoMaterialController.ChaveComponente(componente));
            statusLabel.Text =
                $"Componente selecionado automaticamente: {componente.CodigoMaterial} - Reserva {componente.NumeroReserva}/{componente.ItemReserva}.";
            RegistrarDiagnosticoSelecaoConsumo(componente, linha.Index);
            return true;
        }

        _componenteConsumoSelecionado = null;
        AtualizarLiberacaoInicioLeitura();
        return false;
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
            // Consumo: F9 = peso manual (Designer mostra "F9" em leituraManualButton). Sem Zebra/teste.
            LeituraManual_Click(leituraManualButton, EventArgs.Empty);
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

        if (keyData == (Keys.Control | Keys.S))
        {
            _ = ConfirmarConsumoAsync(); // salvar consumo local (PENDENTE_SAP); sem SAP
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

        if (_ordemConsumoAtual is null)
        {
            statusLabel.Text = "Carregue uma ordem de produ??o v?lida para iniciar a leitura de consumo.";
            return;
        }

        // Auto-selecao SEGURA: mantem selecao manual valida; senao destaca a 1a linha operavel real do grid.
        if (!SelecionarPrimeiroComponentePesavelSeNecessario())
        {
            statusLabel.Text = "Nenhum componente operacional liberado para leitura de consumo.";
            return;
        }

        // Ponto critico: recaptura a linha DESTACADA para garantir que a regra use exatamente esse componente.
        CapturarComponenteSelecionadoDoGridPrincipal();

        if (_componenteConsumoSelecionado is null)
        {
            statusLabel.Text = "Selecione um componente pendente para iniciar a leitura de consumo.";
            return;
        }

        if (!ComponentePodeOperar(_componenteConsumoSelecionado, out string motivoBloqueio))
        {
            BloquearComponenteOperacional(_componenteConsumoSelecionado, motivoBloqueio);
            MessageBox.Show(
                MontarMensagemOperacaoBloqueada(motivoBloqueio),
                "Iniciar leitura de consumo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(AutorizacaoServico.AcaoExecutar, "executar leitura"))
        {
            return;
        }

        _isProductionStarted = true;
        UpdateProductionState(true);
        statusLabel.Text = $"Leitura de consumo iniciada para o componente {_componenteConsumoSelecionado?.CodigoMaterial}.";
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
        // Tarefa 4: Consumo aquece SOMENTE a balanca — nao toca a Zebra/impressora.
        await WarmUpSaldoSafelyAsync();
    }

    private async Task WarmUpSaldoSafelyAsync()
    {
        try
        {
            await AquecerBalancaOperacionalAsync();
        }
        catch (Exception)
        {
            // A validacao com mensagem amigavel continua acontecendo ao clicar em Ler Peso.
        }
    }

    private async Task AquecerBalancaOperacionalAsync()
    {
        await ResolverBalancaOperacionalAsync();
        await _balancaLeituraServico.AquecerAsync();
    }

    private async Task<ResultadoLeituraPeso> LerPesoBalancaOperacionalAsync()
    {
        await ResolverBalancaOperacionalAsync();
        ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
        if (!leitura.Sucesso && _modoConsumo == ModoConsumoMaterial.Quimico)
        {
            return ResultadoLeituraPeso.Falha(AjustarMensagemBalancaPorModo(leitura.Mensagem));
        }

        return leitura;
    }

    private Task<global::FugaPET_Dev.Modelo.Cadastro.BalancaCadastro?> ResolverBalancaOperacionalAsync()
    {
        if (_modoConsumo == ModoConsumoMaterial.Quimico)
        {
            return ResolverBalancaSaidaQuimicosAsync();
        }

        return ResolverBalancaConsumoMateriaPrimaAsync();
    }

    private Task<global::FugaPET_Dev.Modelo.Cadastro.BalancaCadastro?> ResolverBalancaConsumoMateriaPrimaAsync()
        => Task.FromResult<global::FugaPET_Dev.Modelo.Cadastro.BalancaCadastro?>(null);

    private Task<global::FugaPET_Dev.Modelo.Cadastro.BalancaCadastro?> ResolverBalancaSaidaQuimicosAsync()
    {
        // TODO Cadastro/Balança:
        // Quando o tipo SAIDA_QUIMICOS existir no cadastro, resolver a balança por TipoBalancaPreferencial.
        // Até lá, mantém fallback controlado para a balança padrão do terminal, sem permitir escolha manual.
        System.Diagnostics.Trace.TraceInformation(
            $"[Consumo][Balanca] Modo={_modoConsumo}; tipoPreferencial={_configuracaoConsumo.TipoBalancaPreferencial}; fallback=terminal.");
        return Task.FromResult<global::FugaPET_Dev.Modelo.Cadastro.BalancaCadastro?>(null);
    }

    private string AjustarMensagemBalancaPorModo(string mensagem)
    {
        if (_modoConsumo == ModoConsumoMaterial.Quimico
            && !string.IsNullOrWhiteSpace(_configuracaoConsumo.TextoSemBalancaConfigurada)
            && mensagem.Contains("Balanca padrao", StringComparison.OrdinalIgnoreCase))
        {
            return _configuracaoConsumo.TextoSemBalancaConfigurada;
        }

        return mensagem;
    }

    private async void StopProduction_Click(object? sender, EventArgs e)
    {
        if (!_isProductionStarted)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(AutorizacaoServico.AcaoFinalizar, "finalizar leitura"))
        {
            return;
        }

        _isProductionStarted = false;
        UpdateProductionState(false);
        statusLabel.Text = "Leitura de consumo parada.";
    }

    private async void ReadWeightLegend_Click(object? sender, EventArgs e)
    {
        if (!_isProductionStarted || _isReadingWeight)
        {
            if (!_isProductionStarted)
            {
                statusLabel.Text = "Inicie a leitura de consumo antes de ler a balança.";
            }

            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(AutorizacaoServico.AcaoExecutar, "executar leitura"))
        {
            return;
        }

        // Correcao 3: captura a linha atual + valida o componente ANTES de ler a balanca.
        if (!ValidarComponenteAtualParaPesagem(out ComponenteConsumoMaterial? componenteF12, out string mensagemValidacao))
        {
            statusLabel.Text = mensagemValidacao;
            MessageBox.Show(mensagemValidacao, "Pesagem de consumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Tarefa Consumo 22.2: lote vem SEMPRE do SAP; sem lote o componente já é bloqueado em
        // AvaliarLiberacaoPesagem (via ValidarComponenteAtualParaPesagem). Não há mais lote manual.

        // Correcao 2: garante a tara selecionada do componente antes de ler a balanca (F12 usa essa tara).
        await SelecionarTaraParaComponenteAsync(componenteF12!);
        if (!ExisteTaraSelecionada(componenteF12!))
        {
            statusLabel.Text = "Peso não registrado: é necessário selecionar a tara do componente.";
            return;
        }

        _isReadingWeight = true;
        SetReadWeightEnabled(false);
        statusLabel.Text = "Lendo peso da balanca...";

        try
        {
            // Tarefa 4: pesagem LOCAL de consumo — sem impressao de etiqueta.
            ResultadoLeituraPeso leitura = await LerPesoBalancaOperacionalAsync();
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

            if (!ConsumoMaterialServico.TryParsePesoConsumoKg(leitura.Peso, out decimal pesoBruto))
            {
                statusLabel.Text = MensagemPesoConsumoInvalido;
                return;
            }

            await RegistrarPesagemConsumoAsync(pesoBruto, PesagemConsumoMaterial.OrigemBalanca);
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

    /// <summary>
    /// Valida (via controller→service) e registra LOCALMENTE a pesagem de consumo do componente
    /// selecionado, aplicando a tara selecionada. Sem banco, sem SAP, sem impressao.
    /// </summary>
    private async Task RegistrarPesagemConsumoAsync(decimal pesoBruto, string origem)
    {
        CapturarComponenteSelecionadoDoGridPrincipal();

        if (_ordemConsumoAtual is null || _componenteConsumoSelecionado is null)
        {
            statusLabel.Text = "Selecione um componente da ordem antes de iniciar a leitura.";
            MessageBox.Show(
                statusLabel.Text,
                "Pesagem de consumo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ComponenteConsumoMaterial componente = _componenteConsumoSelecionado;
        if (!ComponentePodeOperar(componente, out string motivoBloqueio))
        {
            BloquearComponenteOperacional(componente, motivoBloqueio);
            MessageBox.Show(
                MontarMensagemOperacaoBloqueada(motivoBloqueio),
                "Pesagem de consumo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        RegistrarDiagnosticoPesagemComponente(componente);
        // Correcao 2: a tara vem da SELECAO POR COMPONENTE (escolhida antes do F9/F12).
        TaraConsumoAplicada taraAplicada = ObterTaraConsumoAplicada(componente);
        decimal tara = taraAplicada.PesoKg;

        string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
        decimal totalLocal = SomarPesagensLocais(chave);

        RegistrarDiagnosticoPesagemConsumo(pesoBruto, tara, pesoBruto - tara, taraAplicada.Origem);

        ResultadoPesagemConsumo resultado = _controller.RegistrarPesagemConsumo(
            componente,
            _ordemConsumoAtual.NumeroOrdem,
            pesoBruto,
            tara,
            origem,
            totalLocal,
            _proximaSequenciaPesagem);

        if (!resultado.Sucesso || resultado.Pesagem is null)
        {
            statusLabel.Text = resultado.Mensagem;
            MessageBox.Show(resultado.Mensagem, "Pesagem de consumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_pesagensPorComponente.TryGetValue(chave, out List<PesagemConsumoMaterial>? lista))
        {
            lista = [];
            _pesagensPorComponente[chave] = lista;
        }

        lista.Add(resultado.Pesagem);
        _proximaSequenciaPesagem++;

        AtualizarTotaisConsumo(componente, chave);
        AtualizarApontamentoVisual(componente, $"?ltima pesagem: {resultado.Pesagem.PesoLiquidoKg:0.000} kg.");
        statusLabel.Text = $"{resultado.Mensagem} {FormatarResumoPesagem(resultado.Pesagem)}";
    }

    // Correcao 2: tara aplicada vem da SELECAO POR COMPONENTE (dicionario), nao mais da tara invisivel do terminal.
    private TaraConsumoAplicada ObterTaraConsumoAplicada(ComponenteConsumoMaterial componente)
    {
        string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
        if (_tarasPorComponente.TryGetValue(chave, out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            return new TaraConsumoAplicada(tara.PesoKg, $"tara selecionada: {tara.NomeTara}");
        }

        return new TaraConsumoAplicada(0m, "sem tara selecionada");
    }

    private bool ExisteTaraSelecionada(ComponenteConsumoMaterial componente)
        => _tarasPorComponente.ContainsKey(ProcessoConsumoMaterialController.ChaveComponente(componente));

    /// <summary>
    /// Correcao 2: abre a tela de selecao de tara (mesma da Entrada) para o componente PESAVEL, lista taras
    /// ativas do setor e armazena por chave de componente. Nao abre para componente sem deposito/saldo/nao pesavel,
    /// nem reabre se ja houver tara selecionada (clique simples).
    /// </summary>
    private async Task SelecionarTaraParaComponenteAsync(ComponenteConsumoMaterial componente)
    {
        if (_selecionandoTara)
        {
            return;
        }

        if (!ComponentePodeOperar(componente, out _))
        {
            return; // sem deposito/saldo/unidade/consumido/backflush/bloqueado -> nao abre selecao de tara.
        }

        if (ExisteTaraSelecionada(componente))
        {
            return; // ja selecionada -> nao reabrir a cada clique simples.
        }

        if (_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0)
        {
            statusLabel.Text = "Usuário sem setor definido: não é possível selecionar a tara.";
            MessageBox.Show(statusLabel.Text, "Seleção de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _selecionandoTara = true;
        try
        {
            IReadOnlyList<global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro> taras =
                await _controller.ListarTarasAtivasPorSetorAsync(codigoSetor);
            if (taras.Count == 0)
            {
                statusLabel.Text = "Nenhuma tara ativa para o seu setor. Cadastre uma tara antes de pesar.";
                MessageBox.Show(statusLabel.Text, "Seleção de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using SelecaoTaraPesagemForm form = new(taras, componente.CodigoMaterial);
            if (form.ShowDialog(this) != DialogResult.OK || form.TaraSelecionada is null)
            {
                statusLabel.Text = "Seleção de tara cancelada.";
                return;
            }

            string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
            _tarasPorComponente[chave] = form.TaraSelecionada;

            string nome = form.TaraSelecionada.NomeTara;
            decimal peso = form.TaraSelecionada.PesoKg;
            string texto = $"Tara selecionada: {nome} ({peso.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} kg)";
            statusLabel.Text = texto;
            AplicarTooltipTaraNaLinha(componente, texto);
        }
        finally
        {
            _selecionandoTara = false;
        }
    }

    private void AplicarTooltipTaraNaLinha(ComponenteConsumoMaterial componente, string tooltip)
    {
        foreach (DataGridViewRow linha in productionDataGridView.Rows)
        {
            if (ReferenceEquals(linha.Tag, componente))
            {
                foreach (DataGridViewCell celula in linha.Cells)
                {
                    celula.ToolTipText = tooltip;
                }

                break;
            }
        }
    }

    private DialogResult ConfirmarTaraAplicada(decimal pesoBruto, TaraConsumoAplicada taraAplicada)
    {
        decimal liquido = pesoBruto - taraAplicada.PesoKg;
        string mensagem =
            "Existe uma tara padrão configurada para este terminal." + Environment.NewLine + Environment.NewLine +
            $"Peso bruto: {FormatarKg(pesoBruto)}" + Environment.NewLine +
            $"Tara aplicada: {FormatarKg(taraAplicada.PesoKg)}" + Environment.NewLine +
            $"Peso líquido: {FormatarKg(liquido)}" + Environment.NewLine + Environment.NewLine +
            "Confirmar pesagem com esta tara?" + Environment.NewLine +
            "Sim = confirmar com tara | Não = registrar sem tara | Cancelar = cancelar.";

        return MessageBox.Show(
            mensagem,
            "Confirmar tara da pesagem",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button3);
    }

    private static string FormatarResumoPesagem(PesagemConsumoMaterial pesagem)
        => $"Bruto: {FormatarKg(pesagem.PesoBrutoKg)} | Tara: {FormatarKg(pesagem.PesoTaraKg)} | Líquido: {FormatarKg(pesagem.PesoLiquidoKg)}";

    private static string FormatarKg(decimal valor)
        => $"{valor:0.000} kg";

    private static void RegistrarDiagnosticoPesagemConsumo(decimal bruto, decimal tara, decimal liquido, string origemTara)
        => System.Diagnostics.Trace.WriteLine(
            $"Pesagem consumo: bruto={bruto:0.###}, tara={tara:0.###}, liquido={liquido:0.###}, origemTara={origemTara}");

    private static void RegistrarDiagnosticoPesagemComponente(ComponenteConsumoMaterial componente)
        => System.Diagnostics.Trace.WriteLine(
            $"Pesagem consumo usando: material={componente.CodigoMaterial}, reserva={componente.NumeroReserva}/{componente.ItemReserva}, pendente={componente.QuantidadePendente:0.###}");

    private readonly record struct TaraConsumoAplicada(decimal PesoKg, string Origem);

    private decimal SomarPesagensLocais(string chave)
        => _pesagensPorComponente.TryGetValue(chave, out List<PesagemConsumoMaterial>? lista)
            ? lista.Sum(pesagem => pesagem.PesoLiquidoKg)
            : 0m;

    private static decimal ObterQuantidadePrevistaInicial(ComponenteConsumoMaterial componente)
        => componente.QuantidadePrevista > 0m
            ? componente.QuantidadePrevista
            : componente.QuantidadePendente + Math.Max(0m, componente.QuantidadeConsumida);

    private decimal ObterQuantidadeUtilizadaComponente(ComponenteConsumoMaterial componente, string chave)
        => Math.Max(0m, componente.QuantidadeConsumida) + SomarPesagensLocais(chave);

    private static string FormatarPesoPainel(decimal valor)
        => $"{valor.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} kg";

    private static string FormatarPesoGrid(decimal valor, string unidade)
    {
        string texto = valor.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        string unidadeNormalizada = string.IsNullOrWhiteSpace(unidade) ? "KG" : unidade.Trim();
        return $"{texto} {unidadeNormalizada}".Trim();
    }
    /// <summary>
    /// Atualiza, por DECIMAL e a partir da MEMORIA (_pesagensPorComponente), os totais de consumo:
    /// Peso Previsto (pendente local) e Peso Utilizado (total pesado local); e recalcula o status do
    /// componente (consumido/pendente), reabrindo a pesagem quando ficar abaixo do pendente.
    /// </summary>
    private void AtualizarTotaisConsumo(ComponenteConsumoMaterial componente, string chave)
    {
        decimal totalLocal = SomarPesagensLocais(chave);
        decimal previstoInicial = ObterQuantidadePrevistaInicial(componente);
        decimal totalUtilizado = ObterQuantidadeUtilizadaComponente(componente, chave);
        decimal saldoRestante = Math.Max(0m, previstoInicial - totalUtilizado);

        // Tarefa 22.12: card lateral mostra o mesmo componente selecionado da grid.
        boxesCounterLabel.Text = FormatarPesoPainel(previstoInicial);
        packagesCounterLabel.Text = FormatarPesoPainel(totalUtilizado);
        if (saldoRestanteCounterLabel is not null)
        {
            saldoRestanteCounterLabel.Text = $"Saldo: {FormatarPesoPainel(saldoRestante)}";
        }

        ConsumoMaterialServico.AtualizarStatusComponentePorTotalLocal(componente, totalLocal);
        AtualizarLinhaComponenteSelecionado(componente);
        AtualizarLiberacaoInicioLeitura();
        AtualizarBotaoConfirmar();
        AtualizarApontamentoVisual(componente);
    }
    private void AtualizarLinhaComponenteSelecionado(ComponenteConsumoMaterial componente)
    {
        foreach (DataGridViewRow linha in materialDataGridView.Rows)
        {
            if (ReferenceEquals(linha.Tag, componente))
            {
                linha.Cells["materialStatusColumn"].Value = componente.Status;
                break;
            }
        }

        foreach (DataGridViewRow linha in productionDataGridView.Rows)
        {
            if (ReferenceEquals(linha.Tag, componente))
            {
                string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
                decimal previstoInicial = ObterQuantidadePrevistaInicial(componente);
                decimal totalUtilizado = ObterQuantidadeUtilizadaComponente(componente, chave);
                decimal saldo = Math.Max(0m, previstoInicial - totalUtilizado);
                string unidade = string.IsNullOrWhiteSpace(componente.UnidadeMedida) ? "KG" : componente.UnidadeMedida;
                // Ajuste 7: a coluna de Peso Previsto NAO e reescrita durante a pesagem local (fica fixa).
                linha.Cells["productionWeightColumn"].Value = FormatarPesoGrid(totalUtilizado, unidade);
                linha.Cells["productionSaldoColumn"].Value = FormatarPesoGrid(saldo, unidade);
                AplicarStatusVisualComponente(linha, componente);
                RestaurarSelecaoComponente(componente);
                break;
            }
        }
    }

    private void CriarBotaoConfirmarConsumo()
    {
        // Botao minimo (codigo, sem redesenhar o Designer), abaixo de "INICIAR LEITURA" no sidePanel.
        _confirmarConsumoButton = new Button
        {
            Name = "confirmarConsumoButton",
            Text = "Confirmar Consumo",
            Size = new Size(190, 36),
            Location = new Point(12, 488), // Tarefa 18.1: abaixo de LER/DIGITAR PESO (evita sobreposicao)
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(59, 130, 246),
            ForeColor = Color.White,
            Font = new Font("Cascadia Code", 6.8F, FontStyle.Bold),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _confirmarConsumoButton.FlatAppearance.BorderSize = 0;
        _confirmarConsumoButton.Click += async (_, _) => await ConfirmarConsumoAsync();
        sidePanel.Controls.Add(_confirmarConsumoButton);
        _confirmarConsumoButton.BringToFront();
    }

    private bool PossuiPesagemPendenteParaNovoApontamento()
        => _pesagensPorComponente.Values.Any(lista => lista.Count > 0);

    private string FinalizarApontamentoEnviadoSapELiberarNovaPesagem(string mensagemSucesso)
    {
        if (_ordemConsumoAtual is null)
        {
            return mensagemSucesso;
        }

        Dictionary<string, decimal> quantidadesEnviadas = _pesagensPorComponente
            .Where(par => par.Value.Count > 0)
            .ToDictionary(
                par => par.Key,
                par => par.Value.Sum(pesagem => pesagem.PesoLiquidoKg),
                StringComparer.Ordinal);

        foreach (ComponenteConsumoMaterial componente in _ordemConsumoAtual.Componentes)
        {
            string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
            if (!quantidadesEnviadas.TryGetValue(chave, out decimal enviadoKg) || enviadoKg <= 0m)
            {
                continue;
            }

            componente.QuantidadeConsumida += enviadoKg;
            componente.QuantidadePendente = Math.Max(0m, componente.QuantidadePrevista - componente.QuantidadeConsumida);
            componente.QuantidadePendenteSapOriginal = componente.QuantidadePendente;
            ConsumoMaterialServico.AtualizarStatusComponentePorTotalLocal(componente, 0m);
            AtualizarLinhaComponenteSelecionado(componente);
        }

        _pesagensPorComponente.Clear();
        _ultimoCodigoLancamentoSalvo = null;
        _consumoSalvoNaSessao = false;
        _lancamentoComFalhaSap = false;
        _rotaEnvioSalva = RotaEnvioConsumo.Bloqueado;

        ComponenteConsumoMaterial? selecionado = _componenteConsumoSelecionado;
        if (selecionado is not null)
        {
            AtualizarTotaisConsumo(selecionado, ProcessoConsumoMaterialController.ChaveComponente(selecionado));
            decimal saldoRestante = Math.Max(0m, selecionado.QuantidadePendente);
            decimal disponivelComTolerancia = ConsumoMaterialServico.CalcularDisponivelConsumoComTolerancia(selecionado);
            if (disponivelComTolerancia > 0m && selecionado.PesagemLiberada)
            {
                AtualizarApontamentoVisual(selecionado, "Consumo enviado ao SAP. Nova pesagem liberada para o saldo restante.");
                AtualizarBotaoConfirmar();
                return $"Consumo enviado ao SAP.\r\nSaldo restante: {saldoRestante:0.000} KG.\r\nVocê pode iniciar uma nova leitura para este componente.";
            }

            AtualizarApontamentoVisual(selecionado, "Consumo enviado ao SAP. Componente sem saldo disponível para nova pesagem.");
            AtualizarBotaoConfirmar();
            return "Consumo enviado ao SAP.\r\nLimite de consumo atingido para este componente.";
        }

        AtualizarBotaoConfirmar();
        return mensagemSucesso;
    }

    private void AtualizarBotaoConfirmar()
    {
        if (_confirmarConsumoButton is not null)
        {
            // Ajuste 5 (Tarefa 18.2): CONFIRMAR CONSUMO visivel so FORA da leitura, com pesagem local e
            // consumo ainda nao salvo (oculto ao abrir/durante leitura; some/desabilita apos salvar).
            bool possuiPesagem = PossuiPesagemPendenteParaNovoApontamento();
            _confirmarConsumoButton.Visible = !_isProductionStarted
                && !_lancamentoComFalhaSap
                && _ordemConsumoAtual is not null
                && possuiPesagem
                && !_consumoSalvoNaSessao;
            _confirmarConsumoButton.Enabled = _confirmarConsumoButton.Visible && !_salvandoConsumo;
        }

        // Ajuste 2 (Tarefa 18.2): botoes tecnicos NUNCA visiveis ao operador (fluxo unico = Confirmar Consumo).
        // As instancias/metodos permanecem para testes/diagnostico interno.
        if (_previewSap261Button is not null) { _previewSap261Button.Visible = false; }
        if (_enviarSap261Button is not null) { _enviarSap261Button.Visible = false; }
        if (_previewConfirmacaoButton is not null) { _previewConfirmacaoButton.Visible = false; }
        if (_enviarConfirmacaoButton is not null) { _enviarConfirmacaoButton.Visible = false; }
    }

    /// <summary>
    /// Ajuste 3/6 (Tarefa 18.2): após salvar o consumo, orquestra o envio pela ROTA. 261 direto reaproveita
    /// o envio existente; Backflush não envia (yield-zero da Tarefa 17.11 intacta); Misto/Bloqueado não enviam.
    /// Não mostra preview JSON ao operador. Retorna (mensagem, ícone) para o MessageBox final.
    /// </summary>
    private async Task<(string mensagem, MessageBoxIcon icone)> OrquestrarEnvioAposConfirmarAsync(long codigoLancamento, string usuario)
    {
        if (_modoConsumo == ModoConsumoMaterial.Quimico)
        {
            ResultadoEnvioConsumoSap261? envio = await ExecutarEnvioSap261AposConfirmarAsync(codigoLancamento, usuario);
            return envio is { Sucesso: true }
                ? ("Consumo de químicos salvo localmente e enviado ao SAP por 261 direto com sucesso.", MessageBoxIcon.Information)
                : (envio?.Mensagem
                    ?? "Consumo de químicos salvo localmente, mas o envio SAP 261 direto não foi concluído. Verifique o histórico/diagnóstico antes de reenviar.",
                    MessageBoxIcon.Warning);
        }

        if (_rotaEnvioSalva == RotaEnvioConsumo.Direto261)
        {
            ResultadoEnvioConsumoSap261? envio = await ExecutarEnvioSap261AposConfirmarAsync(codigoLancamento, usuario);
            return envio is { Sucesso: true }
                ? ("Consumo salvo localmente e enviado ao SAP com sucesso.", MessageBoxIcon.Information)
                : (envio?.Mensagem
                    ?? "Consumo salvo localmente, mas o envio ao SAP falhou. Verifique o histórico/diagnóstico antes de reenviar.",
                    MessageBoxIcon.Warning);
        }

        if (_rotaEnvioSalva == RotaEnvioConsumo.BackflushConfirmacao)
        {
            return ("Consumo salvo localmente. Envio SAP não executado: componente Backflush exige apontamento real de produção ou validação SAP para consumo manual via 261.",
                MessageBoxIcon.Warning);
        }

        // Misto / Bloqueado: salva local, mas NAO tenta envio automatico.
        return ("Consumo salvo localmente, mas o envio automático foi bloqueado (rota mista ou inconsistente). Verifique depósito, lote e componentes antes de enviar ao SAP.",
            MessageBoxIcon.Warning);
    }

    /// <summary>Refatoração: envio 261 direto SEM depender de botão visual (reaproveita o método existente).</summary>
    private async Task<ResultadoEnvioConsumoSap261?> ExecutarEnvioSap261AposConfirmarAsync(long codigoLancamento, string usuario)
    {
        ResultadoEnvioConsumoSap261 resultado = await _controller.EnviarConsumoSap261Async(codigoLancamento, usuario);
        if (!resultado.Sucesso && resultado.StatusHttp.HasValue)
        {
            _lancamentoComFalhaSap = true;
        }

        return resultado;
    }

    private string ObterTooltipEnvio261(bool salvo)
    {
        if (!salvo)
        {
            return "Salve o consumo local (Confirmar Consumo) antes de enviar ao SAP.";
        }

        return _rotaEnvioSalva switch
        {
            RotaEnvioConsumo.Direto261 => "Enviar consumo 261 ao SAP.",
            RotaEnvioConsumo.BackflushConfirmacao => "Backflush não usa 261 direto. Use Confirmação de Produção.",
            RotaEnvioConsumo.Misto => "Lançamento misto (261 + Backflush): envio automático bloqueado.",
            _ => "Lançamento bloqueado: verifique depósito, saldo e lote dos componentes."
        };
    }

    private void CriarBotaoEnviarSap261()
    {
        // Correcao 4 (Tarefa 15): reaproveita o productionActionsButton (mesmo ponto da Entrada — topo da
        // grid), SEM criar botao duplicado. Mesmo fluxo EnviarSap261Async e mesma habilitacao.
        // Correcao 6 (Tarefa 15.1): mesmo estilo visual do productionActionsButton da Entrada
        // (fundo branco, borda cinza clara, texto escuro, fonte Cascadia 6.75). Mesmo fluxo de envio.
        productionActionsButton.Text = "Enviar SAP 261";
        productionActionsButton.FlatStyle = FlatStyle.Flat;
        productionActionsButton.BackColor = Color.White;
        productionActionsButton.ForeColor = Color.FromArgb(31, 41, 55);
        productionActionsButton.Font = new Font("Cascadia Code", 6.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
        productionActionsButton.FlatAppearance.BorderColor = Color.FromArgb(226, 231, 238);
        productionActionsButton.Enabled = false;
        productionActionsButton.Cursor = Cursors.Hand;
        productionActionsButton.Click += async (_, _) => await EnviarSap261Async();

        _enviarSap261Button = productionActionsButton;
        _envioSap261ToolTip = new ToolTip();
    }

    /// <summary>
    /// Envia o consumo salvo ao SAP (movimento 261). NUNCA automatico apos salvar: so por acao do usuario.
    /// Trava clique duplo (<see cref="_enviandoSap"/>); bloqueio/sucesso/erro vem do service (WRITE_ENABLED,
    /// CSRF, parse rigoroso). Mensagens sanitizadas.
    /// </summary>
    private async Task EnviarSap261Async()
    {
        if (_enviandoSap)
        {
            return;
        }

        if (_ultimoCodigoLancamentoSalvo is not long codigoLancamento)
        {
            statusLabel.Text = "Salve o consumo local antes de enviar ao SAP.";
            MessageBox.Show(statusLabel.Text, "Enviar SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidarComponenteSelecionadoParaSap261(out string mensagemBloqueioOperacionalEnviar))
        {
            statusLabel.Text = mensagemBloqueioOperacionalEnviar;
            MessageBox.Show(statusLabel.Text, "Enviar SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Tarefa 16: defesa extra (alem do botao desabilitado) — Backflush NAO vai por 261 direto.
        if (_rotaEnvioSalva == RotaEnvioConsumo.BackflushConfirmacao)
        {
            statusLabel.Text =
                "Este lançamento é Backflush e não deve ser enviado por movimento 261 direto.\n"
                + "Ele deve ser enviado pelo fluxo de Confirmação de Produção.\n"
                + "O envio por confirmação ainda não está habilitado nesta versão.";
            MessageBox.Show(statusLabel.Text, "Enviar SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(
                "Deseja enviar este consumo ao SAP agora?",
                "Enviar SAP 261",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _enviandoSap = true;
        AtualizarBotaoConfirmar();
        try
        {
            string usuario = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterLogin();
            ResultadoEnvioConsumoSap261 resultado = await _controller.EnviarConsumoSap261Async(codigoLancamento, usuario);

            // Correcao 4: falha de nivel SAP (HTTP) marca FALHA_SAP local — bloqueia reenvio automatico.
            if (!resultado.Sucesso && resultado.StatusHttp.HasValue)
            {
                _lancamentoComFalhaSap = true;
            }

            statusLabel.Text = resultado.Mensagem;
            MessageBox.Show(
                resultado.Mensagem,
                "Enviar SAP 261",
                MessageBoxButtons.OK,
                resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _enviandoSap = false;
            AtualizarBotaoConfirmar();
        }
    }

    private async Task EnviarConfirmacaoProducaoAsync()
    {
        if (_enviandoConfirmacao)
        {
            return;
        }

        if (_ultimoCodigoLancamentoSalvo is not long codigoLancamento)
        {
            statusLabel.Text = "Salve o consumo local antes de enviar a Confirmação de Produção.";
            MessageBox.Show(statusLabel.Text, "Enviar Confirmação", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_rotaEnvioSalva != RotaEnvioConsumo.BackflushConfirmacao)
        {
            statusLabel.Text = _rotaEnvioSalva == RotaEnvioConsumo.Direto261
                ? "Este lançamento não é Backflush. Use Enviar SAP 261."
                : "Lançamento misto/bloqueado não pode ser enviado automaticamente por Confirmação.";
            MessageBox.Show(statusLabel.Text, "Enviar Confirmação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (MessageBox.Show(
                MontarMensagemConfirmacaoProducao(),
                "Enviar Confirmação",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _enviandoConfirmacao = true;
        AtualizarBotaoConfirmar();
        try
        {
            statusLabel.Text = "Consultando operação de confirmação SAP...";
            string usuario = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterLogin();
            statusLabel.Text = "Enviando Confirmação de Produção ao SAP...";
            ResultadoEnvioConfirmacaoProducao resultado =
                await _controller.EnviarConfirmacaoProducaoAsync(codigoLancamento, usuario);

            // Correcao 4: falha de nivel SAP (HTTP) marca FALHA_SAP local — bloqueia reenvio automatico.
            if (!resultado.Sucesso && resultado.StatusHttp.HasValue)
            {
                _lancamentoComFalhaSap = true;
            }

            statusLabel.Text = resultado.Mensagem;
            MessageBox.Show(
                resultado.Mensagem,
                "Enviar Confirmação",
                MessageBoxButtons.OK,
                resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            _enviandoConfirmacao = false;
            AtualizarBotaoConfirmar();
        }
    }

    private string MontarMensagemConfirmacaoProducao()
    {
        decimal quantidade = _pesagensPorComponente.Values
            .SelectMany(lista => lista)
            .Sum(pesagem => pesagem.PesoLiquidoKg);
        string componentes = _ordemConsumoAtual is null
            ? string.Empty
            : string.Join(", ", _ordemConsumoAtual.Componentes
                .Where(componente => _pesagensPorComponente.ContainsKey(
                    ProcessoConsumoMaterialController.ChaveComponente(componente)))
                .Select(componente => $"{componente.CodigoMaterial} ({componente.NumeroReserva}/{componente.ItemReserva})")
                .Take(5));

        return "Enviar confirmação de produção para SAP?" + Environment.NewLine
            + $"OP: {_ordemConsumoAtual?.NumeroOrdem ?? productionOrderComboBox.Text}" + Environment.NewLine
            + $"Quantidade: {quantidade:0.###} KG" + Environment.NewLine
            + $"Componente(s): {componentes}";
    }

    private void CriarBotaoPreviewSap261()
    {
        _previewSap261Button = new Button
        {
            Name = "previewSap261Button",
            Text = "Preview SAP 261",
            Size = new Size(190, 32),
            Location = new Point(12, 534), // Tarefa 18.1: reposicionado abaixo de Confirmar Consumo
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(71, 85, 105),
            ForeColor = Color.White,
            Font = new Font("Cascadia Code", 6.5F, FontStyle.Bold),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _previewSap261Button.FlatAppearance.BorderSize = 0;
        _previewSap261Button.Click += async (_, _) => await VisualizarPayloadSap261Async();
        sidePanel.Controls.Add(_previewSap261Button);
        _previewSap261Button.BringToFront();
    }

    /// <summary>
    /// Gera e EXIBE o preview do payload de consumo 261 (somente montagem). NAO envia SAP, NAO faz POST,
    /// NAO busca CSRF. O JSON nao contem credenciais/URL/token.
    /// </summary>
    private async Task VisualizarPayloadSap261Async()
    {
        if (_ultimoCodigoLancamentoSalvo is not long codigoLancamento)
        {
            statusLabel.Text = "Salve o consumo local antes de gerar o preview SAP 261.";
            MessageBox.Show(statusLabel.Text, "Preview SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!ValidarComponenteSelecionadoParaSap261(out string mensagemBloqueioOperacionalPreview))
        {
            statusLabel.Text = mensagemBloqueioOperacionalPreview;
            MessageBox.Show(statusLabel.Text, "Preview SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ResultadoPreviewConsumoSap261 preview = await _controller.GerarPreviewSap261Async(codigoLancamento);
        if (!preview.Sucesso)
        {
            string detalhe = preview.ErrosValidacao.Count > 0
                ? preview.Mensagem + Environment.NewLine + string.Join(Environment.NewLine, preview.ErrosValidacao)
                : preview.Mensagem;
            statusLabel.Text = preview.Mensagem;
            MessageBox.Show(detalhe, "Preview SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        statusLabel.Text = preview.Mensagem;
        MessageBox.Show(preview.PayloadJson, "Preview SAP 261 (somente montagem)", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CriarBotaoPreviewConfirmacao()
    {
        _previewConfirmacaoButton = new Button
        {
            Name = "previewConfirmacaoButton",
            Text = "Preview Confirmação",
            Size = new Size(190, 32),
            Location = new Point(12, 580), // Tarefa 18.1: reposicionado abaixo de Preview SAP 261
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(124, 58, 237),
            ForeColor = Color.White,
            Font = new Font("Cascadia Code", 6.5F, FontStyle.Bold),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _previewConfirmacaoButton.FlatAppearance.BorderSize = 0;
        _previewConfirmacaoButton.Click += async (_, _) => await VisualizarPreviewConfirmacao();
        sidePanel.Controls.Add(_previewConfirmacaoButton);
        _previewConfirmacaoButton.BringToFront();
    }

    private void CriarBotaoEnviarConfirmacao()
    {
        _enviarConfirmacaoButton = new Button
        {
            Name = "enviarConfirmacaoButton",
            Text = "Enviar Confirmação",
            Size = new Size(190, 32),
            Location = new Point(12, 626), // Tarefa 18.1: reposicionado abaixo de Preview Confirmação
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(22, 163, 74),
            ForeColor = Color.White,
            Font = new Font("Cascadia Code", 6.3F, FontStyle.Bold),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _enviarConfirmacaoButton.FlatAppearance.BorderSize = 0;
        _enviarConfirmacaoButton.Click += async (_, _) => await EnviarConfirmacaoProducaoAsync();
        sidePanel.Controls.Add(_enviarConfirmacaoButton);
        _enviarConfirmacaoButton.BringToFront();
    }

    /// <summary>
    /// Cria, em CODIGO (sem redesenhar o Designer), um label menor de "Saldo Restante" no card de
    /// Peso Utilizado. Apenas exibicao — nao altera regra/calculo. Ajuste 8 (sem terceiro cartao).
    /// </summary>
    private void CriarLabelSaldoRestante()
    {
        saldoRestanteCounterLabel = new Label
        {
            Name = "saldoRestanteCounterLabel",
            AutoSize = false,
            Font = new Font("Cascadia Code", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(107, 114, 128),
            Location = new Point(3, 46),
            Size = new Size(177, 14),
            Text = "Saldo: 0 kg",
            TextAlign = ContentAlignment.MiddleLeft
        };
        weightSummaryUsedPanel.Controls.Add(saldoRestanteCounterLabel);
        saldoRestanteCounterLabel.BringToFront();
    }

    /// <summary>
    /// Ajuste 9: texto do indicador SAP no topo (somente layout/exibicao — NAO altera integracao).
    /// Le a configuracao em modo defensivo e evita a mensagem enganosa fixa "SAP: não configurado".
    /// </summary>
    // Ajuste 2: estados visuais do indicador SAP (mesmo padrao da Entrada). Apenas EXIBICAO — nao integra.
    private enum EstadoVisualIntegracaoSapConsumo
    {
        Bloqueado,
        AguardandoGravacaoLocal,
        LiberadoParaEnvio,
        Enviando,
        Enviado,
        Falha,
        Parcial,
        BackflushConfirmacaoPendente,
        BloqueadoSemDeposito,
        BloqueadoSemSaldo,
        BloqueadoEscritaDesabilitada,
        BloqueadoSapNaoConfigurado,
        BloqueadoOpIncompativel
    }

    private static readonly Color SapVerde = Color.FromArgb(34, 197, 94);
    private static readonly Color SapAzul = Color.FromArgb(59, 130, 246);
    private static readonly Color SapAmarelo = Color.FromArgb(250, 204, 21);
    private static readonly Color SapLaranja = Color.FromArgb(249, 115, 22);
    private static readonly Color SapVermelho = Color.FromArgb(239, 68, 68);

    /// <summary>
    /// Atualiza o status SAP do header com texto curto no painel e detalhe completo no tooltip.
    /// Não altera integração, envio 261 ou regras de consumo.
    /// </summary>
    private void AtualizarEstadoVisualIntegracaoSapConsumo(
        EstadoVisualIntegracaoSapConsumo estado,
        string? detalhe = null)
    {
        (string textoCurto, Color cor, string detalhePadrao) = estado switch
        {
            EstadoVisualIntegracaoSapConsumo.LiberadoParaEnvio => ("SAP HML: PENDENTE", SapAmarelo, "Consumo salvo localmente e pendente de envio SAP."),
            EstadoVisualIntegracaoSapConsumo.AguardandoGravacaoLocal => ("SAP HML: AGUARDANDO", SapAmarelo, "Aguardando gravação local do consumo."),
            EstadoVisualIntegracaoSapConsumo.Enviando => ("SAP HML: ENVIANDO", SapAzul, "Envio SAP 261 em andamento."),
            EstadoVisualIntegracaoSapConsumo.Enviado => ("SAP HML: ENVIADO", SapVerde, "Consumo enviado ao SAP."),
            EstadoVisualIntegracaoSapConsumo.Falha => ("SAP HML: FALHA", SapVermelho, "Falha no envio SAP. Verifique o diagnóstico."),
            EstadoVisualIntegracaoSapConsumo.Parcial => ("SAP HML: FALHA", SapLaranja, "Envio SAP parcial. Verifique o diagnóstico."),
            EstadoVisualIntegracaoSapConsumo.BackflushConfirmacaoPendente => ("SAP HML: BLOQUEADO", SapLaranja, "Backflush bloqueado para consumo manual via 261."),
            EstadoVisualIntegracaoSapConsumo.BloqueadoSemDeposito => ("SAP HML: BLOQUEADO", SapVermelho, "Componente sem depósito SAP informado."),
            EstadoVisualIntegracaoSapConsumo.BloqueadoSemSaldo => ("SAP HML: SEM SALDO", SapVermelho, "Componente sem saldo SAP disponível para consumo."),
            EstadoVisualIntegracaoSapConsumo.BloqueadoEscritaDesabilitada => ("SAP HML: BLOQUEADO", SapVermelho, "Escrita SAP desabilitada no ambiente."),
            EstadoVisualIntegracaoSapConsumo.BloqueadoSapNaoConfigurado => ("SAP HML: OFFLINE", SapVermelho, "Integração SAP não configurada ou indisponível."),
            EstadoVisualIntegracaoSapConsumo.BloqueadoOpIncompativel => ("SAP HML: BLOQUEADO", SapVermelho, "OP incompatível com esta tela."),
            _ => ("SAP HML: BLOQUEADO", SapVermelho, "Consumo bloqueado para envio SAP.")
        };

        string tooltip = string.IsNullOrWhiteSpace(detalhe)
            ? detalhePadrao
            : detalhe.Trim();

        AtualizarSapStatus(textoCurto, tooltip, cor);
    }

    private void AtualizarSapStatus(string statusCurto, string? detalheCompleto, Color cor)
    {
        sapStatusDotLabel.ForeColor = cor;
        sapStatusLabel.AutoSize = false;
        sapStatusLabel.AutoEllipsis = false;
        sapStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        sapStatusLabel.Text = ResumirStatusSapHeader(statusCurto);

        _sapStatusToolTip ??= new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 350,
            ReshowDelay = 150,
            ShowAlways = true
        };

        string tooltip = string.IsNullOrWhiteSpace(detalheCompleto)
            ? statusCurto
            : detalheCompleto.Trim();

        _sapStatusToolTip.SetToolTip(sapStatusPanel, tooltip);
        _sapStatusToolTip.SetToolTip(sapStatusLabel, tooltip);
        _sapStatusToolTip.SetToolTip(sapStatusDotLabel, tooltip);
    }

    private static string ResumirStatusSapHeader(string? status)
    {
        string texto = string.IsNullOrWhiteSpace(status) ? "SAP HML: AGUARDANDO" : status.Trim();

        if (texto.Contains("M7/021", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("Deficit of", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("saldo", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP HML: SEM SALDO";
        }

        if (texto.Contains("HTTP", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("<", StringComparison.Ordinal)
            || texto.Contains("{", StringComparison.Ordinal)
            || texto.Contains("Payload", StringComparison.OrdinalIgnoreCase))
        {
            return "SAP HML: FALHA";
        }

        if (texto.Length <= 22)
        {
            return texto;
        }

        return "SAP HML: FALHA";
    }

    private EstadoVisualIntegracaoSapConsumo DeterminarEstadoSapConsumo(ComponenteConsumoMaterial? componente)
    {
        ConfiguracaoSap? configuracao = null;
        try
        {
            configuracao = LeitorConfiguracaoSap.Carregar();
        }
        catch
        {
            // Sem acoplar a integracao.
        }

        if (configuracao is null
            || !(configuracao.ProductionOrderConfigurado || configuracao.MaterialDocumentConfigurado))
        {
            return EstadoVisualIntegracaoSapConsumo.BloqueadoSapNaoConfigurado;
        }

        if (componente is not null)
        {
            if (string.IsNullOrWhiteSpace(componente.DepositoConsumo))
            {
                return EstadoVisualIntegracaoSapConsumo.BloqueadoSemDeposito;
            }

            if (componente.QuantidadePendente <= 0m)
            {
                return EstadoVisualIntegracaoSapConsumo.BloqueadoSemSaldo;
            }

            if (componente.BackflushSap)
            {
                return EstadoVisualIntegracaoSapConsumo.BackflushConfirmacaoPendente;
            }
        }

        if (!configuracao.EscritaHabilitada)
        {
            return EstadoVisualIntegracaoSapConsumo.BloqueadoEscritaDesabilitada;
        }

        return _ultimoCodigoLancamentoSalvo is not null
            ? EstadoVisualIntegracaoSapConsumo.LiberadoParaEnvio
            : EstadoVisualIntegracaoSapConsumo.AguardandoGravacaoLocal;
    }

    private void AtualizarIndicadorSapConsumo()
        => AtualizarEstadoVisualIntegracaoSapConsumo(DeterminarEstadoSapConsumo(_componenteConsumoSelecionado));

    /// <summary>
    /// Ajuste 1: painéis com responsabilidades distintas — <c>apontamentoChipPanel</c> (ROTA SAP, curta)
    /// e <c>apontamentoInfoPanel</c> (ORIENTAÇÃO, detalhada). Também sincroniza o indicador SAP do topo.
    /// </summary>
    private void AtualizarApontamentoVisual(ComponenteConsumoMaterial? componente = null, string? orientacao = null)
    {
        apontamentoChipCaptionLabel.Text = "ROTA SAP";
        apontamentoInfoCaptionLabel.Text = "ORIENTAÇÃO";

        string chip;
        string info;
        string? mensagemCompleta = orientacao;

        if (_ordemConsumoAtual is null)
        {
            chip = "Sem OP";
            info = "Carregue uma OP\npara iniciar.";
        }
        else if (componente is null)
        {
            chip = "—";
            info = MensagemApontamentoInicial;
        }
        else if (string.IsNullOrWhiteSpace(componente.DepositoConsumo))
        {
            chip = "Sem depósito";
            info = "Sem depósito SAP.\nAjuste necessário.";
            mensagemCompleta ??= "Componente sem depósito de consumo. Pesagem bloqueada.";
        }
        else if (componente.QuantidadePendente <= 0m)
        {
            chip = "Sem saldo";
            mensagemCompleta ??= componente.QuantidadePendenteSapOriginal < 0m
                ? $"Saldo SAP negativo: {componente.QuantidadePendenteSapOriginal:0.000} kg. Pesagem bloqueada."
                : "Componente sem saldo pendente no SAP.";
            info = "Sem saldo SAP.\nPesagem bloqueada.";
        }
        else if (componente.BackflushSap)
        {
            chip = "Backflush";
            info = "Backflush bloqueado\npara consumo manual.";
            mensagemCompleta ??= "Backflush: usar Preview Confirmação. Não enviar 261 direto.";
        }
        else if (componente.PesagemLiberada)
        {
            chip = "261 Direto";
            info = _isProductionStarted
                ? "Leitura ativa.\nAguardando peso."
                : "Componente selecionado.\nPronto para leitura.";
        }
        else
        {
            chip = "Bloqueado";
            mensagemCompleta ??= string.IsNullOrWhiteSpace(componente.MotivoBloqueioPesagem)
                ? "Componente não liberado para pesagem."
                : $"Bloqueado: {componente.MotivoBloqueioPesagem}";
            info = ObterMensagemCurtaBloqueioApontamento(mensagemCompleta);
        }

        if (!string.IsNullOrWhiteSpace(orientacao))
        {
            info = ObterMensagemCurtaOrientacaoApontamento(orientacao);
        }

        apontamentoChipValueLabel.Text = chip;
        AtualizarApontamentoInfo(info, mensagemCompleta ?? info);
        AtualizarEstadoVisualIntegracaoSapConsumo(DeterminarEstadoSapConsumo(componente));
    }

    private const string MensagemApontamentoInicial = "Selecione um componente\nou inicie a leitura.";

    private static string ObterMensagemCurtaOrientacaoApontamento(string orientacao)
    {
        string texto = orientacao.Trim();

        if (texto.Contains("Última pesagem", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("Consumo salvo", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("PENDENTE_SAP", StringComparison.OrdinalIgnoreCase))
        {
            return "Consumo salvo.\nPendente de envio SAP.";
        }

        if (texto.Contains("enviado", StringComparison.OrdinalIgnoreCase)
            && texto.Contains("SAP", StringComparison.OrdinalIgnoreCase))
        {
            return "Consumo enviado\nao SAP.";
        }

        if (texto.Contains("Falha", StringComparison.OrdinalIgnoreCase)
            && texto.Contains("SAP", StringComparison.OrdinalIgnoreCase))
        {
            return "Falha no envio SAP.\nVerifique o diagnóstico.";
        }

        return ObterMensagemCurtaBloqueioApontamento(texto);
    }

    private static string ObterMensagemCurtaBloqueioApontamento(string mensagem)
    {
        if (mensagem.Contains("Backflush", StringComparison.OrdinalIgnoreCase))
        {
            return "Backflush bloqueado\npara consumo manual.";
        }

        if (mensagem.Contains("depósito", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("deposito", StringComparison.OrdinalIgnoreCase))
        {
            return "Sem depósito SAP.\nAjuste necessário.";
        }

        if (mensagem.Contains("lote", StringComparison.OrdinalIgnoreCase))
        {
            return "Sem lote SAP.\nAjuste necessário.";
        }

        if (mensagem.Contains("tolerância", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("tolerancia", StringComparison.OrdinalIgnoreCase)
            || mensagem.Contains("exced", StringComparison.OrdinalIgnoreCase))
        {
            return "Tolerância excedida.\nPesagem bloqueada.";
        }

        return ResumirMensagemApontamento(mensagem);
    }

    private void AtualizarApontamentoInfo(string mensagemCurta, string? mensagemCompleta = null)
    {
        string mensagemTela = ResumirMensagemApontamento(mensagemCurta);
        string? tooltip = string.IsNullOrWhiteSpace(mensagemCompleta)
            ? mensagemCurta
            : mensagemCompleta;

        apontamentoInfoValueLabel.AutoSize = false;
        apontamentoInfoValueLabel.AutoEllipsis = false;
        apontamentoInfoValueLabel.TextAlign = ContentAlignment.MiddleLeft;
        apontamentoInfoValueLabel.Text = mensagemTela;

        _apontamentoInfoToolTip ??= new ToolTip
        {
            AutoPopDelay = 12000,
            InitialDelay = 350,
            ReshowDelay = 150,
            ShowAlways = true
        };

        _apontamentoInfoToolTip.SetToolTip(apontamentoInfoPanel, tooltip);
        _apontamentoInfoToolTip.SetToolTip(apontamentoInfoValueLabel, tooltip);
    }

    private static string ResumirMensagemApontamento(string? mensagem)
    {
        const int limitePainel = 84;
        string texto = string.IsNullOrWhiteSpace(mensagem) ? "--" : mensagem.Trim();

        if (texto.Contains('\n') || texto.Contains('\r'))
        {
            string[] linhas = texto.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return linhas.Length == 0
                ? "--"
                : string.Join(Environment.NewLine, linhas.Take(2));
        }

        while (texto.Contains("  ", StringComparison.Ordinal))
        {
            texto = texto.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (texto.Length <= limitePainel)
        {
            return texto;
        }

        int corte = texto.LastIndexOf(' ', Math.Min(limitePainel, texto.Length - 1));
        if (corte < 42)
        {
            corte = limitePainel;
        }

        return texto[..corte].TrimEnd() + "…";
    }

    /// <summary>
    /// Gera e EXIBE o preview tecnico do caminho de Confirmacao de Producao (componente Backflush).
    /// NAO envia SAP, NAO faz POST, NAO busca CSRF, NAO usa PATCH. O lancamento permanece PENDENTE_SAP.
    /// </summary>
    private async Task VisualizarPreviewConfirmacao()
    {
        // Tarefa 17.6: com lancamento SALVO, o preview mostra o PAYLOAD REAL do POST (mesmo builder do envio,
        // operacao resolvida no SAP) — sem GoodsMovementIsFinallyPosted, sem ManufacturingOrder no item.
        if (_ultimoCodigoLancamentoSalvo is long codigoSalvo)
        {
            ResultadoPreviewConfirmacaoProducaoSap previewReal =
                await _controller.GerarPreviewConfirmacaoProducaoRealAsync(codigoSalvo);
            statusLabel.Text = previewReal.Sucesso ? "Preview real da Confirmação (payload do POST)." : previewReal.Mensagem;
            string corpo = previewReal.Sucesso
                ? previewReal.PayloadJson
                : previewReal.ErrosValidacao.Count > 0
                    ? previewReal.Mensagem + Environment.NewLine + string.Join(Environment.NewLine, previewReal.ErrosValidacao)
                    : previewReal.Mensagem;
            MessageBox.Show(
                corpo,
                "Preview Confirmação",
                MessageBoxButtons.OK,
                previewReal.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return;
        }

        if (_ordemConsumoAtual is null || _componenteConsumoSelecionado is null)
        {
            statusLabel.Text = "Selecione um componente Backflush para o preview de Confirmação de Produção.";
            MessageBox.Show(statusLabel.Text, "Preview Confirmação", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_componenteConsumoSelecionado.BackflushSap)
        {
            statusLabel.Text = "Preview de Confirmação de Produção disponível apenas para componente Backflush.";
            MessageBox.Show(statusLabel.Text, "Preview Confirmação", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        decimal totalLocal = SomarPesagensLocais(
            ProcessoConsumoMaterialController.ChaveComponente(_componenteConsumoSelecionado));

        ResultadoPreviewConfirmacaoProducao preview = _controller.GerarPreviewConfirmacaoProducao(
            _ordemConsumoAtual, _componenteConsumoSelecionado, totalLocal);

        if (!preview.Sucesso)
        {
            statusLabel.Text = preview.Mensagem;
            MessageBox.Show(preview.Mensagem, "Preview Confirmação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        statusLabel.Text = preview.Titulo;
        MessageBox.Show(preview.PreviewJson, preview.Titulo, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>
    /// Salva LOCALMENTE o consumo (PENDENTE_SAP) via controller. Idempotente: trava clique duplo
    /// (<see cref="_salvandoConsumo"/>) e bloqueia regravacao na sessao (<see cref="_consumoSalvoNaSessao"/>).
    /// NAO envia SAP.
    /// </summary>
    private async Task ConfirmarConsumoAsync()
    {
        if (_salvandoConsumo)
        {
            return;
        }

        // Correcao 3 (Tarefa 15.1): trava a validacao do campo OP DESDE O INICIO (antes das validacoes),
        // para que o Validated do ComboBox nao reconsulte/limpe pesagens ao perder foco para o botao.
        _acaoOperacionalEmAndamento = true;
        _salvandoConsumo = true;
        AtualizarBotaoConfirmar();
        try
        {
            if (_consumoSalvoNaSessao)
            {
                statusLabel.Text = "Nenhuma nova pesagem pendente para envio SAP.";
                return;
            }

            int totalPesagens = _pesagensPorComponente.Values.Sum(lista => lista.Count);

            // Correcao 4: diagnostico sanitizado antes da mensagem de "nenhuma pesagem".
            System.Diagnostics.Trace.WriteLine(
                $"Confirmar consumo: ordemAtual={_ordemConsumoAtual?.NumeroOrdem ?? "<null>"}, " +
                $"totalPesagens={totalPesagens}, gridPesoUtilizado={GridMostraPesoUtilizado()}, " +
                $"comboTexto={productionOrderComboBox.Text}, isStarted={_isProductionStarted}, " +
                $"salvando={_salvandoConsumo}, consultando={_consultandoOrdem}");

            // Correcao 1.5: a fonte oficial e _pesagensPorComponente. Se o GRID mostra peso utilizado mas a
            // memoria esta vazia, e dessincronizacao — NAO salvar com base no grid.
            if (totalPesagens == 0 && GridMostraPesoUtilizado())
            {
                statusLabel.Text =
                    "Inconsistência interna: o grid mostra peso utilizado, mas a memória de pesagens está vazia. Recarregue a OP e refaça a pesagem.";
                MessageBox.Show(statusLabel.Text, TituloMensagemConsumo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_ordemConsumoAtual is null || totalPesagens == 0)
            {
                statusLabel.Text = "Nenhuma pesagem de consumo registrada para salvar.";
                MessageBox.Show(statusLabel.Text, TituloMensagemConsumo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Tarefa Consumo 22.2: sem lote manual — só componentes com lote SAP puderam ser pesados,
            // então as pesagens já nascem com lote correto (bloqueio ocorre em AvaliarLiberacaoPesagem).

            // Tarefa Consumo 22.12.1: defesa final antes de persistir ? pesagem pendente s? pode salvar
            // se o componente ainda estiver operacional no modo atual (261 Direto, lote/deposito/saldo ok).
            if (!ValidarComponentesComPesagemPendentesOperaveis(out string mensagemBloqueioOperacional))
            {
                statusLabel.Text = mensagemBloqueioOperacional;
                MessageBox.Show(statusLabel.Text, TituloMensagemConsumo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(
                    _modoConsumo == ModoConsumoMaterial.MateriaPrima
                        ? MensagemConfirmacaoSalvarConsumoPadrao
                        : $"Deseja salvar localmente este {NomeOperacionalConsumo.ToLowerInvariant()} como pendente de envio ao SAP?",
                    "Confirmar Consumo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            // Correcao 5: diagnostico SANITIZADO das chaves componente x pesagens antes de salvar.
            foreach (ComponenteConsumoMaterial diag in _ordemConsumoAtual.Componentes)
            {
                string chaveDiag = ProcessoConsumoMaterialController.ChaveComponente(diag);
                bool possuiPesagem = _pesagensPorComponente.TryGetValue(chaveDiag, out List<PesagemConsumoMaterial>? listaDiag)
                    && listaDiag.Count > 0;
                System.Diagnostics.Trace.WriteLine(
                    $"Salvar consumo - componente: material={diag.CodigoMaterial}, reserva={diag.NumeroReserva}/{diag.ItemReserva}, " +
                    $"lote={diag.Lote}, deposito={diag.DepositoConsumo}, chave={chaveDiag}, possuiPesagem={possuiPesagem}");
            }

            string usuario = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterLogin();
            ResultadoPersistenciaConsumoMaterial resultado = await _controller.SalvarConsumoLocalAsync(
                _ordemConsumoAtual,
                _ordemConsumoAtual.Componentes,
                _pesagensPorComponente,
                usuario);

            // Correcao 6: SemPesagem com pesagens na memoria = falha de vinculacao (lote/reserva/item/deposito).
            if (resultado.Cenario == CenarioPersistenciaConsumo.SemPesagem && totalPesagens > 0)
            {
                statusLabel.Text =
                    "Pesagens registradas não foram vinculadas aos componentes da OP. Verifique lote/reserva/item/depósito e refaça a confirmação.";
                MessageBox.Show(statusLabel.Text, TituloMensagemConsumo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (resultado.Sucesso)
            {
                _consumoSalvoNaSessao = true;
                _ultimoCodigoLancamentoSalvo = resultado.CodigoLancamento;

                // Classifica a rota do consumo salvo (a partir dos componentes consumidos).
                List<ComponenteConsumoMaterial> consumidos = _ordemConsumoAtual.Componentes
                    .Where(c => _pesagensPorComponente.TryGetValue(
                        ProcessoConsumoMaterialController.ChaveComponente(c), out List<PesagemConsumoMaterial>? l) && l.Count > 0)
                    .ToList();
                _rotaEnvioSalva = ProcessoConsumoMaterialController.ClassificarRotaEnvio(consumidos);

                // Bloqueia regravacao: encerra a leitura ate uma nova OP / limpeza.
                if (_isProductionStarted)
                {
                    _isProductionStarted = false;
                    UpdateProductionState(false);
                }

                // Ajuste 3/6 (Tarefa 18.2): CONFIRMAR CONSUMO e o fluxo UNICO — apos salvar, orquestra o envio
                // pela rota (261 direto reaproveita o envio existente; Backflush/Misto/Bloqueado NAO enviam).
                (string mensagemFinal, MessageBoxIcon icone) = await OrquestrarEnvioAposConfirmarAsync(resultado.CodigoLancamento.GetValueOrDefault(), usuario);
                bool envioSapConfirmado = icone == MessageBoxIcon.Information
                    && (_modoConsumo == ModoConsumoMaterial.Quimico || _rotaEnvioSalva == RotaEnvioConsumo.Direto261);
                if (envioSapConfirmado)
                {
                    mensagemFinal = FinalizarApontamentoEnviadoSapELiberarNovaPesagem(mensagemFinal);
                }

                statusLabel.Text = mensagemFinal;
                MessageBox.Show(mensagemFinal, TituloMensagemConsumo, MessageBoxButtons.OK, icone);
            }
            else
            {
                statusLabel.Text = resultado.Mensagem;
                MessageBox.Show(resultado.Mensagem, TituloMensagemConsumo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _salvandoConsumo = false;
            _acaoOperacionalEmAndamento = false;
            AtualizarBotaoConfirmar();
        }
    }

    // Tarefa Consumo 22.2: removidos os métodos de LOTE MANUAL (prompt/aplicação/reindex). O lote vem só do SAP;
    // componente sem lote é bloqueado em AvaliarLiberacaoPesagem, igual ao componente sem depósito.

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

    private async void DeleteLastProductionRow_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(AutorizacaoServico.AcaoCancelar, "cancelar leitura"))
        {
            return;
        }

        if (!_isProductionStarted)
        {
            return;
        }

        PesagemConsumoMaterial? pesagem = ObterUltimaPesagemLocal();
        if (pesagem is null)
        {
            statusLabel.Text = "Não há pesagens para excluir.";
            return;
        }

        if (!ConfirmDeleteLastProductionRow(pesagem.Sequencia.ToString()))
        {
            statusLabel.Text = "Exclusão cancelada.";
            return;
        }

        RemoverPesagemLocal(pesagem);
        statusLabel.Text = "Última pesagem de consumo excluída.";
    }

    private async void DeleteProductionRowByCode_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(AutorizacaoServico.AcaoCancelar, "cancelar leitura"))
        {
            return;
        }

        if (!_isProductionStarted)
        {
            return;
        }

        string? sequenciaTexto = PromptProductionCodeToDelete();
        if (string.IsNullOrWhiteSpace(sequenciaTexto))
        {
            statusLabel.Text = "Exclusão cancelada.";
            return;
        }

        string alvo = sequenciaTexto.Trim();
        PesagemConsumoMaterial? pesagem = ObterPesagemLocalPorSequencia(alvo);
        if (pesagem is null)
        {
            statusLabel.Text = "Pesagem não encontrada.";
            MessageBox.Show(
                $"Nenhuma pesagem com sequência {alvo} foi encontrada.",
                "Pesagem não encontrada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!ConfirmProductionRowDelete(alvo, "Deseja realmente excluir a pesagem informada?"))
        {
            statusLabel.Text = "Exclusão cancelada.";
            return;
        }

        RemoverPesagemLocal(pesagem);
        statusLabel.Text = $"Pesagem {alvo} excluída.";
    }

    /// <summary>
    /// Exclusao LOCAL de pesagem: remove a pesagem da memoria, recalcula totais no componente e
    /// reabre o componente quando o total local cair abaixo do pendente. Sem banco/SAP.
    /// </summary>
    private void RemoverPesagemLocal(PesagemConsumoMaterial pesagem)
    {
        string chave = ConsumoMaterialServico.ChavePesagem(pesagem);
        if (_pesagensPorComponente.TryGetValue(chave, out List<PesagemConsumoMaterial>? lista))
        {
            lista.Remove(pesagem);
            if (lista.Count == 0)
            {
                _pesagensPorComponente.Remove(chave);
            }
        }

        ClearGridSelection(productionDataGridView);

        ComponenteConsumoMaterial? componente = ObterComponentePorChave(chave);
        if (componente is not null)
        {
            AtualizarTotaisConsumo(componente, chave);
        }
    }

    private PesagemConsumoMaterial? ObterUltimaPesagemLocal()
        => _pesagensPorComponente.Values
            .SelectMany(lista => lista)
            .OrderByDescending(pesagem => pesagem.Sequencia)
            .FirstOrDefault();

    private PesagemConsumoMaterial? ObterPesagemLocalPorSequencia(string sequencia)
        => _pesagensPorComponente.Values
            .SelectMany(lista => lista)
            .FirstOrDefault(pesagem => string.Equals(
                pesagem.Sequencia.ToString(),
                sequencia,
                StringComparison.OrdinalIgnoreCase));

    private ComponenteConsumoMaterial? ObterComponentePorChave(string chave)
        => _ordemConsumoAtual?.Componentes.FirstOrDefault(componente => string.Equals(
            ConsumoMaterialServico.ChaveComponente(componente),
            chave,
            StringComparison.Ordinal));

    private string? PromptProductionCodeToDelete()
    {
        using Form promptForm = new()
        {
            Text = "Excluir pesagem por sequência",
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
            Text = "Informe a sequência da pesagem que deseja excluir.",
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
        return ConfirmProductionRowDelete(productionCode, "Deseja realmente excluir a última pesagem de consumo?");
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
        // Estado SEM componente selecionado: zera os cartoes. Os totais por componente sao definidos
        // por AtualizarTotaisConsumo (Peso Previsto fixo / Peso Utilizado / Saldo Restante).
        boxesCounterLabel.Text = "0 kg";
        packagesCounterLabel.Text = "0 kg";
        if (saldoRestanteCounterLabel is not null)
        {
            saldoRestanteCounterLabel.Text = "Saldo: 0 kg";
        }

        UpdateProductionGridFooter();
    }

    private static string NormalizeCounterTotal(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int total) ? total.ToString() : "0";
    }

    private void SetReadWeightEnabled(bool enabled)
    {
        enabled = enabled && PossuiPermissaoLeituraProducao(AutorizacaoServico.AcaoExecutar);

        if (!enabled)
        {
            _isReadWeightHovering = false;
            readWeightLegendPanel.Invalidate();
        }

        readWeightLegendPanel.Enabled = enabled;
        readWeightLegendIconLabel.Enabled = enabled;
        readWeightLegendTextLabel.Enabled = enabled;
        readWeightLegendPanel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        readWeightLegendIconLabel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        readWeightLegendTextLabel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        readWeightLegendTextLabel.ForeColor = enabled ? EnabledLegendTextColor : DisabledLegendTextColor;
        readWeightLegendIconLabel.Visible = enabled;
        lerEtiquetaButton.Enabled = enabled;
        leituraManualButton.Enabled = enabled;
    }

    private void SetDeleteActionsEnabled(bool enabled)
    {
        enabled = enabled && PossuiPermissaoLeituraProducao(AutorizacaoServico.AcaoCancelar);

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
        if (await BloquearAcaoSemPermissaoAsync(AutorizacaoServico.AcaoExecutar, "executar leitura"))
        {
            return;
        }

        if (!_isProductionStarted)
        {
            statusLabel.Text = "Inicie a leitura de consumo antes de informar o peso manual.";
            return;
        }

        // Correcao 3: captura a linha atual + valida o componente ANTES de abrir o campo de peso.
        if (!ValidarComponenteAtualParaPesagem(out ComponenteConsumoMaterial? componenteF9, out string mensagemValidacao))
        {
            statusLabel.Text = mensagemValidacao;
            MessageBox.Show(mensagemValidacao, "Pesagem de consumo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Tarefa Consumo 22.2: lote vem SEMPRE do SAP; sem lote o componente já é bloqueado em
        // AvaliarLiberacaoPesagem (via ValidarComponenteAtualParaPesagem). Não há mais lote manual.

        // Correcao 2: garante a tara selecionada do componente antes de pedir o peso (F9 usa essa tara).
        await SelecionarTaraParaComponenteAsync(componenteF9!);
        if (!ExisteTaraSelecionada(componenteF9!))
        {
            statusLabel.Text = "Peso não registrado: é necessário selecionar a tara do componente.";
            return;
        }

        string? manualWeight = PromptManualProductionWeight(string.Empty);
        if (string.IsNullOrWhiteSpace(manualWeight))
        {
            statusLabel.Text = "Peso manual cancelado.";
            return;
        }

        if (!ConsumoMaterialServico.TryParsePesoConsumoKg(manualWeight, out decimal pesoBruto))
        {
            MessageBox.Show(
                MensagemPesoConsumoInvalido,
                "Peso manual",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        // Peso manual = peso BRUTO; a tara selecionada e aplicada no service (mesmo fluxo da balanca).
        await RegistrarPesagemConsumoAsync(pesoBruto, PesagemConsumoMaterial.OrigemManual);
    }

    private void UpdateProductionState(bool started)
    {
        ClearDangerActionHover();
        sidePanel.BackColor = Color.White;
        sideReadingStatusLabel.Text = started ? "Ativo" : "Inativo";
        sideReadingStatusLabel.ForeColor = started ? ReadingStatusActiveColor : ReadingStatusInactiveColor;
        // Tarefa 18.2 (padrao Entrada): INICIAR so libera com permissao + OP valida carregada (sem exigir
        // clique previo em linha). Cinza/desabilitado ao abrir; verde quando a OP valida esta carregada.
        bool podeAlternarLeitura = started
            ? PossuiPermissaoLeituraProducao(AutorizacaoServico.AcaoFinalizar)
            : PodeIniciarLeituraConsumo();
        startActionPanel.BackColor = !started && podeAlternarLeitura ? ActionEnabledColor : ActionDisabledColor;
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
        // Tarefa 18.2 (padrao Entrada): LER PESO / DIGITAR PESO so aparecem durante a leitura ativa.
        lerEtiquetaButton.Visible = started;
        leituraManualButton.Visible = started;

        productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productionDataGridView.MultiSelect = false;

        stopActionPanel.Visible = started;
        stopActionPanel.Enabled = started && podeAlternarLeitura;
        stopActionPanel.Cursor = stopActionPanel.Enabled ? Cursors.Hand : Cursors.Default;
        stopActionIconLabel.Cursor = stopActionPanel.Cursor;
        stopActionTextLabel.Cursor = stopActionPanel.Cursor;

        if (!started && _componenteConsumoSelecionado is not null)
        {
            RestaurarSelecaoComponente(_componenteConsumoSelecionado);
        }

        SetReadWeightEnabled(started);
        SetDeleteActionsEnabled(started);

        // Fora da leitura, o inicio so e liberado com OP carregada + componente pesavel selecionado.
        if (!started)
        {
            AtualizarLiberacaoInicioLeitura();
        }
    }

    private void ConfigurarSelecaoLinhaInteiraGridComponentes()
    {
        productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productionDataGridView.MultiSelect = false;
        productionDataGridView.ReadOnly = true;
        productionDataGridView.RowHeadersVisible = false;
        productionDataGridView.AllowUserToAddRows = false;
        productionDataGridView.AllowUserToDeleteRows = false;
    }

    private void SelecionarLinhaInteiraGridComponentes(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= productionDataGridView.Rows.Count)
        {
            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[rowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productionDataGridView.MultiSelect = false;
        productionDataGridView.ClearSelection();

        int safeColumnIndex = columnIndex >= 0 && columnIndex < row.Cells.Count ? columnIndex : 0;
        productionDataGridView.CurrentCell = row.Cells[safeColumnIndex];
        row.Selected = true;
        if (!_restaurandoSelecaoLinhaComponentes)
        {
            AtualizarComponenteSelecionadoDoGrid();
        }
    }

    private bool RestaurarSelecaoComponente(ComponenteConsumoMaterial componente)
    {
        string chave = ProcessoConsumoMaterialController.ChaveComponente(componente);
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow || row.Tag is not ComponenteConsumoMaterial candidato)
            {
                continue;
            }

            if (!string.Equals(ProcessoConsumoMaterialController.ChaveComponente(candidato), chave, StringComparison.Ordinal))
            {
                continue;
            }

            _restaurandoSelecaoLinhaComponentes = true;
            try
            {
                SelecionarLinhaInteiraGridComponentes(row.Index, productionDataGridView.CurrentCell?.ColumnIndex ?? 0);
            }
            finally
            {
                _restaurandoSelecaoLinhaComponentes = false;
            }

            return true;
        }

        productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productionDataGridView.MultiSelect = false;
        ClearGridSelection(productionDataGridView);
        return false;
    }
    private void ProductionDataGridView_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        SelecionarLinhaInteiraGridComponentes(e.RowIndex, e.ColumnIndex);
    }

    private void ProductionDataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        // Ajuste 1 (Tarefa 18): clicar no grid APENAS seleciona a linha — NAO abre a tela de tara. A tara
        // e escolhida somente ao pesar (F12/LER PESO em ReadWeightLegend_Click e F9/DIGITAR PESO em LeituraManual_Click).
        SelecionarLinhaInteiraGridComponentes(e.RowIndex, e.ColumnIndex);

        if (_isProductionStarted && _componenteConsumoSelecionado is { PesagemLiberada: true })
        {
            statusLabel.Text = "Componente selecionado. Use LER PESO ou DIGITAR PESO para registrar a pesagem.";
        }
        else if (!_isProductionStarted && _componenteConsumoSelecionado is { PesagemLiberada: true })
        {
            statusLabel.Text = "Componente selecionado. Inicie a leitura para pesar.";
        }
    }

    private void ProductionDataGridView_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        SelecionarLinhaInteiraGridComponentes(e.RowIndex, e.ColumnIndex);
    }

    private void ProductionDataGridView_CurrentCellChanged(object? sender, EventArgs e)
        => AtualizarComponenteSelecionadoDoGrid();

    private void ProductionDataGridView_RowEnter(object? sender, DataGridViewCellEventArgs e)
        => AtualizarComponenteSelecionadoDoGrid();

    private void ProductionDataGridView_SelectionChanged(object? sender, EventArgs e)
    {
        // Correcao 2: nao bloquear selecao so porque a leitura esta ativa (apenas durante leitura de peso).
        if (_isReadingWeight)
        {
            return;
        }

        if (productionDataGridView.SelectedCells.Count == 0 && productionDataGridView.SelectedRows.Count == 0)
        {
            return;
        }

        AtualizarComponenteSelecionadoDoGrid();
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

    // Tarefa 4.1: a Tela de Consumo NAO imprime etiqueta — o duplo clique de impressao foi removido.

    private DataGridViewRow? GetSelectedProductionRow()
        => ObterLinhaSelecionadaNoGridPrincipal();

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

    private static string GetCellValue(DataGridViewRow row, string columnName)
    {
        return Convert.ToString(row.Cells[columnName].Value) ?? string.Empty;
    }

    /// <summary>Correcao 1.5: true se ALGUMA linha do grid exibe Peso Utilizado &gt; 0 (deteccao de dessincronizacao).</summary>
    private bool GridMostraPesoUtilizado()
        => productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Any(row =>
                ConsumoMaterialServico.TryParsePesoConsumoKg(GetCellValue(row, "productionWeightColumn"), out decimal peso)
                && peso > 0m);

    private void ProcessoProdutoAcabadoForm_Shown(object? sender, EventArgs e)
    {
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
















