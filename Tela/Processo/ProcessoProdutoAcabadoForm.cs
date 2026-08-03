using System.Globalization;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tela.Processo;

public partial class ProcessoProdutoAcabadoForm : Form
{
    private static readonly Color ReadingStatusInactiveColor = Color.FromArgb(220, 53, 69);
    private static readonly Color ReadingStatusActiveColor = Color.FromArgb(34, 166, 82);
    private static readonly Color ActionDisabledColor = Color.FromArgb(156, 163, 175);

    // Tarefa 21.6.5 (Ajuste 6): fontes maiores/legíveis (Segoe UI), padrão dos cadastros Setor/Cargo.
    private static readonly Font FonteTituloSecao = new("Segoe UI", 9.5F, FontStyle.Bold);
    private static readonly Font FonteLabelCampo = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FonteValorCampo = new("Segoe UI", 11F, FontStyle.Bold);
    private static readonly Font FonteGridHeader = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FonteGridCell = new("Segoe UI", 9.5F, FontStyle.Regular);
    private static readonly Font FonteBotao = new("Segoe UI", 9F, FontStyle.Bold);
    private static readonly Font FonteAuxiliar = new("Segoe UI", 8F, FontStyle.Italic);

    internal const string MensagemBalancaProdutoAcabadoNaoConfigurada =
        "Balança de produto acabado não configurada para esta operação.";
    internal const string MensagemPostSapDesativado =
        "Produto acabado salvo localmente em memória. POST SAP automático está desativado nesta etapa.";

    private readonly ProdutoAcabadoController _controller;
    private readonly BalancaLeituraServico _balancaLeituraServico = new();
    private readonly List<ProdutoAcabadoCaixa> _caixasPesadas = [];
    private readonly List<ProdutoAcabadoPalete> _paletesMontados = [];
    private ProdutoAcabadoOrdem? _ordemAtual;
    private ProdutoAcabadoNormaEmbalagem? _normaEmbalagem;
    private TaraCadastro? _taraCaixaSelecionada;
    private ContextoTerminalLocal? _contextoTerminal;
    private long? _idSetorSelecionado;
    private bool _leituraIniciada;
    private bool _operacaoEmAndamento;
    private System.Windows.Forms.Timer? _footerClockTimer;
    private TextBox primeiraCaixaTextBox = null!;
    private TextBox ultimaCaixaTextBox = null!;
    private TextBox materialEmbalagemPaleteTextBox = null!;
    private DataGridView paletesDataGridView = null!;
    private string _ultimaOpConsultada = string.Empty; // Tarefa 21.6.3 (Ajuste 1): evita reconsultar a mesma OP no Tab/Leave
    private FugaPET_Dev.Tela.Controls.RoundedPanel? _criarPaleteCard; // Tarefa 21.6.3 (Ajuste 8)
    private Label? _paleteMensagemLabel;
    private TableLayoutPanel? _areaInferior; // Tarefa 21.6.5 (Ajuste 5): composição vertical da área inferior
    private Label? _plannedDivider3;          // Tarefa 21.6.4 (Ajuste 4): 3º divisor (4 colunas)
    private Label? _materialCaixaCaptionLabel; // Tarefa 21.6.4 (Ajuste 4): coluna própria MATERIAL CAIXA
    private Label? _materialCaixaValorLabel;
    private Label? _normaVaziaLabel;              // Tarefa 21.6 (Ajuste 4): estado vazio do grid de norma
    private Label? _pendenciaBalancaTituloLabel;  // Tarefa 21.6 (Ajuste 3): pendencia operacional (lateral)
    private Label? _pendenciaBalancaTextoLabel;
    private Label? _statusNormaValorLabel;        // Tarefa 21.6 (Ajuste 2): STATUS NORMA / MATERIAL CAIXA
    private Label? _avisoNormaFallbackLabel;

    // Correção HU (1ª entrega): Produto Acabado por CAIXA INDIVIDUAL via Handling Unit.
    // Enquanto true: uma caixa por vez, área de palete fora do fluxo ativo e botão de envio manual apenas
    // preparado (desabilitado — schema e contrato OP_HANDLINGUNIT_0001 pendentes; nenhum POST).
    private const bool PrimeiraEntregaHu = true;
    // §6: NÃO existe fallback de material de embalagem inventado. A única origem confirmada é a norma SAP
    // (MaterialCaixa). Sem origem real ⇒ vazio e o preview bloqueia a finalização (mensagem clara).
    private Button? _enviarCaixaSapButton; // §10: "ENVIAR CAIXA SAP HML" (preparado/desabilitado)

    public ProcessoProdutoAcabadoForm()
        : this(new ProdutoAcabadoController())
    {
    }

    internal ProcessoProdutoAcabadoForm(ProdutoAcabadoController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        InitializeComponent();
        ConfigurarCampoOrdemProducaoProdutoAcabado();
        AplicarModoProdutoAcabadoReal();
        ConfigurarRodape();
        ConfigurarEventos();
        ConfigurarGridNormaEmbalagem();
        ConfigurarGridCaixas();
        ConfigurarPaletizacaoOperacional();
        ConfigurarCardNormaExtra();      // Tarefa 21.6 (Ajuste 2)
        ConfigurarCampoQtdArredondado(); // Tarefa 21.6.4 (Ajuste 5)
        CriarBlocoPendenciaBalanca();    // Tarefa 21.6 (Ajuste 3)
        AtualizarEstadoLeitura(false);
        AtualizarResumoOperacional();
        AtualizarPendenciaBalanca();
        KeyPreview = true;
    }

    private async void ProcessoProdutoAcabadoForm_Shown(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar, "executar produto acabado"))
        {
            Close();
            return;
        }

        productionOrderTextBox.Focus();
    }

    private void ConfigurarCampoOrdemProducaoProdutoAcabado()
    {
        productionOrderTextBox.ReadOnly = false;
        productionOrderTextBox.Text = string.Empty;
        productionOrderTextBox.Multiline = false;
        productionOrderTextBox.MaxLength = 20;
        productionOrderTextBox.TextAlign = HorizontalAlignment.Left;
        productionOrderTextBox.BorderStyle = BorderStyle.None;
        productionOrderTextBox.BackColor = Color.White;
        productionOrderTextBox.ForeColor = Color.FromArgb(229, 27, 43);
        productionOrderTextBox.TabStop = true;
        productionOrderSearchLabel.Enabled = true;
        productionOrderSearchLabel.Cursor = Cursors.Hand;
    }

    private void AplicarModoProdutoAcabadoReal()
    {
        Text = "Produto Acabado";
        headerTitleLabel.Text = "Produto Acabado";
        headerSubtitleLabel.Text = "Pesagem de caixas e formação de palete por ordem de produção";
        productionOrderCaptionLabel.Text = "OP";
        // Tarefa 21.6.4 (Ajuste 1): card ITEM OP útil (sem OP → "-"/"Aguardando OP"; nada de "Consulta de").
        stepCaptionLabel.Text = "ITEM OP";
        stepDescriptionLabel.Text = "Aguardando OP";
        stepLabel.Text = "-";
        finishedProductCaptionLabel.Text = "Produto acabado";
        lotCaptionLabel.Text = "Lote";
        // Tarefa 21.6 (Ajuste 1): card de dados produtivos — NAO usar titulo "DATAS" nem rotulos de data.
        dateTitleLabel.Text = "DADOS DA OP";
        ovenExitCaptionLabel.Text = "Depósito destino";
        classificationDateCaptionLabel.Text = "Saldo pendente";
        manufacturingDateCaptionLabel.Text = "Quantidade planejada";
        expirationDateCaptionLabel.Text = "Quantidade entregue";
        // Tarefa 21.6 (Ajuste 2): status/material da norma no card de producao planejada.
        balanceCaptionLabel.Text = "STATUS NORMA";
        materialTitleLabel.Text = "Norma de Embalagem";
        productionReadingsTitleLabel.Text = "Caixas Pesadas";
        productionActionsButton.Text = "CRIAR PALETE";
        productionActionsButton.Visible = false;
        // Tarefa 21.6 (Ajuste 8): termos operacionais na lateral direita (sem "pacotes").
        // Tarefa 21.6.2 (Ajuste 2): remover o ícone que sobrepunha o texto na lateral direita.
        boxesTitleLabel.Text = "CAIXAS PESADAS";
        boxesTitleLabel.Image = null;
        boxesTitleLabel.Padding = new Padding(0);
        packagesTitleLabel.Text = "PESO REGISTRADO";
        packagesTitleLabel.Image = null;
        packagesTitleLabel.Padding = new Padding(0);
        boxesCaptionLabel.Text = "Caixas";
        packagesCaptionLabel.Text = "Peso líquido";
        readWeightLegendTextLabel.Text = "F12 - Ler peso balança";
        manualLotLegendTextLabel.Text = "F9 - Digitar peso";
        deleteLastLegendTextLabel.Text = "Del - Excluir última caixa";
        deleteByCodeLegendTextLabel.Text = "Esc - Fechar";
        lerEtiquetaButton.PrimaryText = "LER PESO";
        lerEtiquetaButton.KeyHint = "F12";
        leituraManualButton.PrimaryText = "DIGITAR PESO";
        leituraManualButton.KeyHint = "F9";
        statusValueLabel.Text = "INATIVA";
        statusHintLabel.Text = "Informe uma OP para iniciar.";
        sapStatusLabel.Text = _controller.SapSimulado ? "SAP OP: DEMONSTRAÇÃO" : "SAP OP: CONSULTA";
        statusLabel.Text = "Informe uma OP para consulta.";
        CarregarContextoTerminal();
        // Tarefa 21.6 (Ajuste 3): a mensagem de balança saiu do card — vira pendencia na lateral direita.
        balanceTextBox.Text = string.Empty;
        cellUserText.Text = UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = RodapeBancoHelper.ObterTextoBancoDados();
        string nomeTerminal = string.IsNullOrWhiteSpace(_contextoTerminal?.NomeTerminal)
            ? Environment.MachineName
            : _contextoTerminal!.NomeTerminal;
        cellTerminalText.Text = $"Terminal:  {nomeTerminal}";
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
    }

    private void CarregarContextoTerminal()
    {
        try
        {
            _contextoTerminal = EstadoTerminalLocalAtual.ObterContextoAtualizado();
        }
        catch
        {
            _contextoTerminal = null;
        }

        _idSetorSelecionado = EstadoSessaoUsuarioAtual.SessaoAtual?.IdSetorPadrao
            ?? _contextoTerminal?.IdSetorPadrao;
    }

    private void ConfigurarEventos()
    {
        Shown += ProcessoProdutoAcabadoForm_Shown;
        FormClosing += ProcessoProdutoAcabadoForm_FormClosing;
        KeyDown += ProcessoProdutoAcabadoForm_KeyDown;
        productionOrderTextBox.KeyDown += ProductionOrderTextBox_KeyDown;
        // Tarefa 21.6.3 (Ajuste 1): sair do campo OP (Tab/perda de foco) consulta a OP nova, como o Enter.
        productionOrderTextBox.Leave += async (_, _) => await ConsultarOpAoSairDoCampoAsync();
        productionOrderSearchLabel.Click += async (_, _) => await ConsultarOpAsync();
        readForecastBoxesTextBox.KeyPress += ReadForecastBoxesTextBox_KeyPress;
        // Tarefa 21.6.2 (Ajuste 1): digitar a QTD. por caixa no fallback reavalia o botão iniciar (fica verde).
        readForecastBoxesTextBox.TextChanged += (_, _) => AtualizarBotoesOperacao();
        iniciarLeituraButton.Click += ToggleProductionFromSideButton_Click;
        startActionPanel.Click += ToggleProductionFromSideButton_Click;
        stopActionPanel.Click += (_, _) => AtualizarEstadoLeitura(false);
        lerEtiquetaButton.Click += ReadWeightLegend_Click;
        readWeightLegendPanel.Click += ReadWeightLegend_Click;
        readWeightLegendIconLabel.Click += ReadWeightLegend_Click;
        readWeightLegendTextLabel.Click += ReadWeightLegend_Click;
        leituraManualButton.Click += LeituraManual_Click;
        manualLotLegendPanel.Click += LeituraManual_Click;
        manualLotLegendIconLabel.Click += LeituraManual_Click;
        manualLotLegendTextLabel.Click += LeituraManual_Click;
        deleteLastLegendPanel.Click += (_, _) => CancelarUltimaCaixa();
        productionActionsButton.Click += (_, _) => CriarPaleteLocal();
        // "Os três pontinhos" (menu) retorna à tela de Processos: fecha o diálogo (com a mesma proteção
        // de fechamento), devolvendo o controle ao painel.
        menuHeaderLabel.Click += (_, _) =>
        {
            if (PodeFecharTela())
            {
                Close();
            }
        };
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        closeWindowLabel.Click += (_, _) =>
        {
            if (PodeFecharTela())
            {
                Close();
            }
        };

        // Mesmo efeito hover do cabeçalho das demais telas de Processo.
        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));
    }

    private static void ConfigureTitleButtonHover(Label button, Color hoverColor)
    {
        Color normalColor = button.BackColor;

        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = normalColor;
    }

    private void ConfigurarRodape()
    {
        AtualizarDataHoraRodape();
        _footerClockTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _footerClockTimer.Tick += (_, _) => AtualizarDataHoraRodape();
        _footerClockTimer.Start();
    }

    private void AtualizarDataHoraRodape()
    {
        DateTime agora = DateTime.Now;
        cellHoraText.Text = agora.ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        cellDataText.Text = agora.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private void ConfigurarGridNormaEmbalagem()
    {
        materialDataGridView.AutoGenerateColumns = false;
        materialDataGridView.ReadOnly = true;
        materialDataGridView.MultiSelect = false;
        // Tarefa 21.6.3 (Ajuste 6): grid da norma ocupa a largura útil (Fill), como o grid de caixas.
        materialDataGridView.Dock = DockStyle.Fill;
        materialDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        materialDataGridView.ColumnHeadersDefaultCellStyle.Font = FonteGridHeader; // Tarefa 21.6.5 (Ajustes 6/7)
        materialDataGridView.DefaultCellStyle.Font = FonteGridCell;
        materialDataGridView.ColumnHeadersHeight = 28;
        materialDataGridView.RowTemplate.Height = 28;
        // Tarefa 21.6 (Ajuste 4): cabecalhos completos (sem truncar "T..." / "Un.").
        materialStatusColumn.HeaderText = "Tipo";
        materialStatusColumn.FillWeight = 70;
        materialCodeColumn.HeaderText = "Material";
        materialCodeColumn.FillWeight = 110;
        materialDescriptionColumn.HeaderText = "Item";
        materialDescriptionColumn.FillWeight = 70;
        materialLotColumn.HeaderText = "Quantidade";
        materialLotColumn.FillWeight = 90;
        materialExpirationColumn.HeaderText = "Unidade";
        materialExpirationColumn.FillWeight = 80;
        materialBalanceColumn.HeaderText = "Norma";
        materialBalanceColumn.FillWeight = 120;
        CriarMensagemNormaVazia();
    }

    /// <summary>Tarefa 21.6 (Ajuste 4): estado vazio amigavel sobre o grid de norma de embalagem.</summary>
    private void CriarMensagemNormaVazia()
    {
        _normaVaziaLabel = new Label
        {
            Name = "normaVaziaLabel",
            AutoSize = false,
            BackColor = Color.FromArgb(248, 250, 252),
            Dock = DockStyle.Fill,
            Font = new Font("Cascadia Code", 8F, FontStyle.Italic),
            ForeColor = Color.FromArgb(107, 114, 128),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "Norma de embalagem não retornou itens. Usando fallback de quantidade por caixa.",
            Visible = false
        };
        // Tarefa 21.6.2 (Ajuste 5): o pai da grid é um TableLayoutPanel — para a mensagem central aparecer,
        // colocamos o label na MESMA célula da grid (Dock=Fill) e alternamos a visibilidade grid/label.
        if (materialDataGridView.Parent is TableLayoutPanel tabelaNorma)
        {
            TableLayoutPanelCellPosition celula = tabelaNorma.GetCellPosition(materialDataGridView);
            tabelaNorma.Controls.Add(_normaVaziaLabel);
            tabelaNorma.SetCellPosition(_normaVaziaLabel, celula);
        }
        else if (materialDataGridView.Parent is Control paiNorma)
        {
            paiNorma.Controls.Add(_normaVaziaLabel);
            _normaVaziaLabel.Location = materialDataGridView.Location;
            _normaVaziaLabel.Size = materialDataGridView.Size;
            _normaVaziaLabel.Anchor = materialDataGridView.Anchor;
            _normaVaziaLabel.BringToFront();
        }

        AtualizarMensagemNormaVazia(); // estado inicial (sem OP): mostra a mensagem central
    }

    private void AtualizarMensagemNormaVazia()
    {
        if (_normaVaziaLabel is null)
        {
            return;
        }

        bool semItens = _normaEmbalagem is null || _normaEmbalagem.Itens.Count == 0;
        _normaVaziaLabel.Text = _normaEmbalagem is null
            ? "Norma de embalagem não retornou itens.\r\nUsando fallback de quantidade por caixa."
            : "Norma de embalagem carregada sem componentes.";
        // Alterna: mensagem central quando vazio; grid quando há itens.
        _normaVaziaLabel.Visible = semItens;
        materialDataGridView.Visible = !semItens;
        if (semItens)
        {
            _normaVaziaLabel.BringToFront();
        }
    }

    private void ConfigurarGridCaixas()
    {
        // Tarefa 21.6 (Ajuste 6): uma coluna por conceito — tara, liquido, origem e status separados.
        productionDataGridView.AutoGenerateColumns = false;
        productionDataGridView.ReadOnly = true;
        productionDataGridView.MultiSelect = false;
        productionDataGridView.Columns.Clear();
        productionDataGridView.ScrollBars = ScrollBars.Vertical;
        // Tarefa 21.6.2 (Ajuste 7): colunas preenchem a largura útil (Fill + FillWeight por coluna).
        productionDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Tarefa 21.6.5 (Ajustes 6/7): fonte e linhas de grid maiores/legíveis.
        productionDataGridView.ColumnHeadersDefaultCellStyle.Font = FonteGridHeader;
        productionDataGridView.DefaultCellStyle.Font = FonteGridCell;
        productionDataGridView.ColumnHeadersHeight = 28;
        productionDataGridView.RowTemplate.Height = 28;
        AdicionarColunaCaixa("caixaNumeroColumn", "Caixa", 70);
        AdicionarColunaCaixa("caixaPesoBrutoColumn", "Peso bruto", 90);
        AdicionarColunaCaixa("caixaTaraColumn", "Tara", 70);
        AdicionarColunaCaixa("caixaPesoLiquidoColumn", "Peso líquido", 90);
        AdicionarColunaCaixa("caixaQtdProdutosColumn", "Qtd produtos", 90);
        AdicionarColunaCaixa("caixaOrigemColumn", "Origem", 80);
        AdicionarColunaCaixa("caixaStatusColumn", "Status", 120);
        AdicionarColunaCaixa("caixaPaleteLocalColumn", "Palete local", 110);
        AdicionarColunaCaixa("caixaHuColumn", "HU caixa", 110);
    }

    private void AdicionarColunaCaixa(string nome, string titulo, int peso)
        => productionDataGridView.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nome,
            HeaderText = titulo,
            FillWeight = peso,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });


    private void ConfigurarPaletizacaoOperacional()
    {
        // Cabeçalho (Caixas Pesadas + pesquisa/filtros) permanece no topo do painel (Designer).
        productionSearchPanel.Location = new Point(675, 6);
        productionSearchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        productionFilterButton.Location = new Point(902, 6);
        productionFilterButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Tarefa 21.6.5 (Ajuste 8): card CRIAR PALETE maior (Dock=Fill numa linha de 92px da tabela).
        _criarPaleteCard = new FugaPET_Dev.Tela.Controls.RoundedPanel
        {
            Name = "criarPaleteCard",
            BorderRadius = 10,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(226, 231, 238),
            FillColor = Color.White,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 4)
        };

        Label criarPaleteTituloLabel = new()
        {
            Name = "criarPaleteTituloLabel",
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = FonteTituloSecao,
            ForeColor = Color.FromArgb(17, 24, 39),
            Location = new Point(14, 8),
            Size = new Size(320, 20),
            Text = "CRIAR PALETE",
            TextAlign = ContentAlignment.MiddleLeft
        };
        _criarPaleteCard.Controls.Add(criarPaleteTituloLabel);

        Label primeiraCaixaLabel = CriarLabelPaletizacao("Primeira caixa");
        primeiraCaixaLabel.Location = new Point(14, 32);
        primeiraCaixaLabel.Size = new Size(126, 16);
        _criarPaleteCard.Controls.Add(primeiraCaixaLabel);
        primeiraCaixaTextBox = CriarTextBoxPaletizacao("primeiraCaixaTextBox");
        _criarPaleteCard.Controls.Add(EnvolverCampoArredondado(primeiraCaixaTextBox, new Point(14, 52), 126));

        Label ultimaCaixaLabel = CriarLabelPaletizacao("Última caixa");
        ultimaCaixaLabel.Location = new Point(150, 32);
        ultimaCaixaLabel.Size = new Size(126, 16);
        _criarPaleteCard.Controls.Add(ultimaCaixaLabel);
        ultimaCaixaTextBox = CriarTextBoxPaletizacao("ultimaCaixaTextBox");
        _criarPaleteCard.Controls.Add(EnvolverCampoArredondado(ultimaCaixaTextBox, new Point(150, 52), 126));

        Label materialEmbalagemLabel = CriarLabelPaletizacao("Material embalagem");
        materialEmbalagemLabel.Location = new Point(286, 32);
        materialEmbalagemLabel.Size = new Size(200, 16);
        _criarPaleteCard.Controls.Add(materialEmbalagemLabel);
        materialEmbalagemPaleteTextBox = CriarTextBoxPaletizacao("materialEmbalagemPaleteTextBox");
        materialEmbalagemPaleteTextBox.Text = "PALLET01";
        _criarPaleteCard.Controls.Add(EnvolverCampoArredondado(materialEmbalagemPaleteTextBox, new Point(286, 52), 200));

        // Mensagem de estado do card (fonte legível).
        _paleteMensagemLabel = new Label
        {
            Name = "paleteMensagemLabel",
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = FonteAuxiliar,
            ForeColor = Color.FromArgb(107, 114, 128),
            Location = new Point(510, 56),
            Size = new Size(430, 20),
            Text = "Registre caixas para criar um palete.",
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _criarPaleteCard.Controls.Add(_paleteMensagemLabel);

        // Botão CRIAR PALETE à direita do card, reposicionado no Resize (card é Dock=Fill).
        productionActionsButton.Parent?.Controls.Remove(productionActionsButton);
        productionActionsButton.Size = new Size(176, 34);
        productionActionsButton.Font = FonteBotao;
        _criarPaleteCard.Controls.Add(productionActionsButton);
        _criarPaleteCard.Resize += (_, _) =>
            productionActionsButton.Location = new Point(_criarPaleteCard.Width - productionActionsButton.Width - 16, 30);
        productionActionsButton.BringToFront();

        paletesDataGridView = new DataGridView
        {
            Name = "paletesDataGridView",
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 28, // Tarefa 21.6.5 (Ajuste 7)
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(226, 231, 238),
            Dock = DockStyle.Fill, // Tarefa 21.6.5 (Ajustes 1/4): nada de Anchor Bottom
            MinimumSize = new Size(0, 120),
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        paletesDataGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
        paletesDataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        paletesDataGridView.ColumnHeadersDefaultCellStyle.Font = FonteGridHeader;
        paletesDataGridView.DefaultCellStyle.Font = FonteGridCell;
        paletesDataGridView.RowTemplate.Height = 28; // Tarefa 21.6.5 (Ajuste 7)
        paletesDataGridView.Columns.Add("paleteLocalColumn", "Palete local");
        paletesDataGridView.Columns.Add("paletePrimeiraCaixaColumn", "Primeira caixa");
        paletesDataGridView.Columns.Add("paleteUltimaCaixaColumn", "Última caixa");
        paletesDataGridView.Columns.Add("paleteQtdCaixasColumn", "Qtd caixas");
        paletesDataGridView.Columns.Add("paletePesoBrutoColumn", "Peso bruto");
        paletesDataGridView.Columns.Add("paletePesoLiquidoColumn", "Peso líquido");
        paletesDataGridView.Columns.Add("paleteTaraColumn", "Tara");
        paletesDataGridView.Columns.Add("paleteMaterialColumn", "Material");
        paletesDataGridView.Columns.Add("paleteStatusColumn", "Status");

        Label paletesCriadosTituloLabel = new()
        {
            Name = "paletesCriadosTituloLabel",
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = FonteTituloSecao,
            ForeColor = Color.FromArgb(17, 24, 39),
            Dock = DockStyle.Fill,
            Padding = new Padding(2, 4, 0, 2),
            Text = "Paletes Criados",
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Tarefa 21.6.5 (Ajuste 5): área inferior estruturada em TableLayoutPanel vertical (sem coords fixas).
        // [caixas grid %] [card CRIAR PALETE 92px] [título Paletes Criados 24px] [paletes grid %].
        _areaInferior = new TableLayoutPanel
        {
            Name = "areaInferiorProdutoAcabado",
            ColumnCount = 1,
            RowCount = 4,
            Location = new Point(0, 34),
            Size = new Size(productionReadingsPanel.Width, productionReadingsPanel.Height - 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Padding = new Padding(14, 0, 14, 6),
            BackColor = Color.Transparent
        };
        _areaInferior.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _areaInferior.RowStyles.Add(new RowStyle(SizeType.Percent, 56F));   // caixas
        _areaInferior.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));  // card CRIAR PALETE
        _areaInferior.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));  // título Paletes Criados
        _areaInferior.RowStyles.Add(new RowStyle(SizeType.Percent, 44F));   // paletes

        productionDataGridView.Parent?.Controls.Remove(productionDataGridView);
        productionDataGridView.Dock = DockStyle.Fill;
        productionDataGridView.Margin = new Padding(0, 0, 0, 6);
        productionDataGridView.MinimumSize = new Size(0, 170); // Tarefa 21.6.5 (Ajuste 2)
        _areaInferior.Controls.Add(productionDataGridView, 0, 0);
        _areaInferior.Controls.Add(_criarPaleteCard, 0, 1);
        _areaInferior.Controls.Add(paletesCriadosTituloLabel, 0, 2);
        _areaInferior.Controls.Add(paletesDataGridView, 0, 3);
        productionReadingsPanel.Controls.Add(_areaInferior);
        _areaInferior.BringToFront();

        // §8: 1ª entrega por caixa individual (HU) — palete FORA do fluxo ativo. Oculta integralmente o card
        // CRIAR PALETE, o título e o grid de paletes, e colapsa as linhas correspondentes da área inferior.
        if (PrimeiraEntregaHu)
        {
            _criarPaleteCard.Visible = false;
            paletesCriadosTituloLabel.Visible = false;
            paletesDataGridView.Visible = false;
            productionActionsButton.Visible = false;
            _areaInferior.RowStyles[0] = new RowStyle(SizeType.Percent, 100F); // caixas ocupam a área
            _areaInferior.RowStyles[1] = new RowStyle(SizeType.Absolute, 0F);
            _areaInferior.RowStyles[2] = new RowStyle(SizeType.Absolute, 0F);
            _areaInferior.RowStyles[3] = new RowStyle(SizeType.Absolute, 0F);
            ConfigurarBotaoEnvioCaixaSap();
        }
    }

    /// <summary>
    /// §10: prepara o botão "ENVIAR CAIXA SAP HML". Estrutura visual apenas — enquanto o schema do banco e o
    /// contrato OP_HANDLINGUNIT_0001 estiverem pendentes o botão permanece DESABILITADO e nenhum POST ocorre.
    /// Fica visível somente após uma caixa finalizada.
    /// </summary>
    private void ConfigurarBotaoEnvioCaixaSap()
    {
        _enviarCaixaSapButton = new Button
        {
            Name = "enviarCaixaSapButton",
            Text = "ENVIAR CAIXA SAP HML",
            Font = FonteBotao,
            Location = leituraManualButton.Location,
            Size = leituraManualButton.Size,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            TabIndex = leituraManualButton.TabIndex,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(156, 163, 175), // cinza = desabilitado
            Enabled = false,
            Visible = false,
            Cursor = Cursors.Default
        };
        _enviarCaixaSapButton.FlatAppearance.BorderSize = 0;

        ToolTip tooltip = new();
        tooltip.SetToolTip(_enviarCaixaSapButton,
            "Envio manual indisponível: schema do banco pendente e contrato OP_HANDLINGUNIT_0001 pendente. "
            + "POST ao SAP não autorizado nesta fase.");

        // Nenhum handler de POST: o clique não executa HTTP. Enquanto desabilitado nem dispara.
        _enviarCaixaSapButton.Click += (_, _) =>
            statusLabel.Text = "Envio ao SAP não autorizado nesta fase (schema/contrato pendentes). Nenhum POST executado.";

        sidePanel.Controls.Add(_enviarCaixaSapButton);
        _enviarCaixaSapButton.BringToFront();
        AtualizarEstadoEnvioCaixaSap();
    }

    /// <summary>
    /// §10: reflete o estado da caixa no botão de envio e no status. O botão só aparece após uma caixa
    /// finalizada e permanece DESABILITADO enquanto schema/gateway estiverem pendentes (nenhum POST).
    /// </summary>
    private void AtualizarEstadoEnvioCaixaSap()
    {
        if (_enviarCaixaSapButton is null)
        {
            return;
        }

        ProdutoAcabadoCaixa? caixa = _caixasPesadas.LastOrDefault();
        bool temCaixaFinalizada = caixa is not null
            && caixa.StatusIntegracao is StatusIntegracaoCaixa.FinalizadaLocal
                or StatusIntegracaoCaixa.PreviewHuGerado
                or StatusIntegracaoCaixa.AguardandoAutorizacaoSap
                or StatusIntegracaoCaixa.ProntaParaEnvio
                or StatusIntegracaoCaixa.ErroSap;

        _enviarCaixaSapButton.Visible = temCaixaFinalizada;
        // Sempre desabilitado nesta fase: schema pendente + contrato pendente + POST não autorizado.
        _enviarCaixaSapButton.Enabled = false;
        leituraManualButton.Visible = _leituraIniciada && !temCaixaFinalizada;

        if (caixa is not null)
        {
            _enviarCaixaSapButton.Text = "ENVIAR CAIXA SAP HML";
        }
    }

    /// <summary>§10: rótulo amigável dos estados de integração da caixa.</summary>
    private static string DescreverStatusIntegracao(StatusIntegracaoCaixa status)
        => status switch
        {
            StatusIntegracaoCaixa.FinalizadaLocal => "FINALIZADA LOCAL",
            StatusIntegracaoCaixa.PreviewHuGerado => "PREVIEW GERADO",
            StatusIntegracaoCaixa.AguardandoAutorizacaoSap => "AGUARDANDO AUTORIZAÇÃO SAP",
            StatusIntegracaoCaixa.ProntaParaEnvio => "PRONTA PARA ENVIO",
            StatusIntegracaoCaixa.EnviandoSap => "ENVIANDO SAP",
            StatusIntegracaoCaixa.ConfirmadaSap => "CONFIRMADA SAP",
            StatusIntegracaoCaixa.ErroSap => "ERRO SAP",
            StatusIntegracaoCaixa.Cancelada => "CANCELADA",
            StatusIntegracaoCaixa.Bloqueada => "BLOQUEADA",
            _ => "EM PESAGEM"
        };

    private static Label CriarLabelPaletizacao(string texto)
        => new()
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = FonteLabelCampo, // Tarefa 21.6.4 (Ajuste 7)
            ForeColor = Color.FromArgb(75, 85, 99),
            Size = new Size(150, 14),
            Text = texto,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static TextBox CriarTextBoxPaletizacao(string nome)
        => new()
        {
            Name = nome,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = FonteLabelCampo, // Tarefa 21.6.5 (Ajuste 6)
            ForeColor = Color.FromArgb(17, 24, 39),
            MaxLength = 30
        };

    /// <summary>
    /// Tarefa 21.6.3 (Ajuste 9): envolve um TextBox numa caixa arredondada (RoundedPanel) — padrão dos
    /// cadastros de Setor/Cargo. O TextBox fica Dock=Fill dentro do painel, preservando eventos/validação.
    /// </summary>
    private static FugaPET_Dev.Tela.Controls.RoundedPanel EnvolverCampoArredondado(TextBox tb, Point local, int largura)
    {
        tb.BorderStyle = BorderStyle.None;
        tb.BackColor = Color.White;
        tb.Dock = DockStyle.Fill;
        tb.Font = FonteLabelCampo; // Tarefa 21.6.5 (Ajuste 6): fonte legível dentro do campo
        FugaPET_Dev.Tela.Controls.RoundedPanel caixa = new()
        {
            BorderRadius = 8,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(209, 213, 219),
            FillColor = Color.White,
            Location = local,
            Size = new Size(largura, 28), // Tarefa 21.6.5 (Ajuste 8): campo maior p/ fonte maior
            Padding = new Padding(8, 5, 8, 5)
        };
        caixa.Controls.Add(tb);
        return caixa;
    }

    /// <summary>Tarefa 21.6.3 (Ajuste 1): consulta a OP ao sair do campo (Tab/foco), se for uma OP nova.</summary>
    private async Task ConsultarOpAoSairDoCampoAsync()
    {
        string op = productionOrderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(op) || _operacaoEmAndamento || _leituraIniciada)
        {
            return;
        }

        if (string.Equals(op, _ultimaOpConsultada, StringComparison.OrdinalIgnoreCase))
        {
            return; // mesma OP já carregada — não reconsulta
        }

        await ConsultarOpAsync(exibirAvisoOpObrigatoria: false);
    }

    private async Task ConsultarOpAsync(bool exibirAvisoOpObrigatoria = true)
    {
        if (_operacaoEmAndamento)
        {
            return;
        }

        // §5: protege a caixa ativa (só em memória nesta fase) de descarte silencioso ao trocar/limpar OP.
        if (!PodeTrocarOuLimparOp())
        {
            productionOrderTextBox.Text = _ultimaOpConsultada; // restaura a OP corrente; nada é descartado
            return;
        }

        string numeroOp = productionOrderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(numeroOp))
        {
            LimparOp();
            statusLabel.Text = "Informe uma OP para consulta.";
            if (exibirAvisoOpObrigatoria)
            {
                MessageBox.Show(
                    "Informe uma OP para consulta.",
                    "Produto Acabado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        try
        {
            _operacaoEmAndamento = true;
            AtualizarBotoesOperacao();
            statusLabel.Text = $"Consultando OP {numeroOp} no SAP...";
            ResultadoConsultaProdutoAcabado resultado =
                await _controller.ConsultarOrdemProducaoAsync(numeroOp, CancellationToken.None);
            if (!resultado.Sucesso || resultado.Ordem is null || resultado.NormaEmbalagem is null)
            {
                LimparOp();
                statusLabel.Text = resultado.Mensagem;
                MessageBox.Show(resultado.Mensagem, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _ordemAtual = resultado.Ordem;
            _ultimaOpConsultada = _ordemAtual.NumeroOrdem; // Tarefa 21.6.3 (Ajuste 1): não reconsultar a mesma OP no Leave
            _normaEmbalagem = resultado.NormaEmbalagem;
            _caixasPesadas.Clear();
            _paletesMontados.Clear();
            AtualizarGridPaletes();
            _taraCaixaSelecionada = null;
            PreencherDadosOrdem();
            PreencherNormaEmbalagem();
            AtualizarCampoQuantidadePorCaixa();
            AtualizarGridCaixas();
            AtualizarResumoOperacional();
            statusValueLabel.Text = "INATIVA";
            statusHintLabel.Text = "OP carregada. Inicie a leitura para pesar caixas.";
            if (NormaEmFallbackMemoria())
            {
                statusLabel.Text = "Norma SAP indisponível. Informe a QTD. POR CAIXA antes de iniciar a leitura.";
                readForecastBoxesTextBox.Focus();
                readForecastBoxesTextBox.SelectAll();
            }
            else
            {
                statusLabel.Text = $"OP {_ordemAtual.NumeroOrdem} carregada para produto acabado.";
            }
        }
        catch (Exception ex)
        {
            LimparOp();
            System.Diagnostics.Trace.TraceWarning($"[ProcessoProdutoAcabadoForm] Falha ao consultar OP: {ex.GetType().Name}");
            MessageBox.Show("Não foi possível consultar a OP. Tente novamente.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoesOperacao();
        }
    }

    private void PreencherDadosOrdem()
    {
        if (_ordemAtual is null)
        {
            return;
        }

        // Tarefa 21.6.4 (Ajuste 1): card "ITEM OP" (só item) ou "ITEM / OPERAÇÃO" (item + operação, sem "/-").
        string item = string.IsNullOrWhiteSpace(_ordemAtual.ItemOrdem) ? "-" : _ordemAtual.ItemOrdem.Trim();
        bool temOperacao = !string.IsNullOrWhiteSpace(_ordemAtual.Operacao);
        stepCaptionLabel.Text = temOperacao ? "ITEM / OPERAÇÃO" : "ITEM OP";
        stepLabel.Text = temOperacao ? $"{item} / {_ordemAtual.Operacao.Trim()}" : item;
        stepDescriptionLabel.Text = string.IsNullOrWhiteSpace(_ordemAtual.StatusOrdem)
            ? "Aguardando OP"
            : $"Status: {FormatarStatusOrdem(_ordemAtual.StatusOrdem)}";

        // Tarefa 21.6.4 (Ajuste 2): não duplicar o código como descrição (comparação normalizada).
        finishedProductCodeTextBox.Text = _ordemAtual.MaterialProduzido;
        bool descricaoUtil = !string.IsNullOrWhiteSpace(_ordemAtual.DescricaoMaterial)
            && !TextosEquivalentesComoCodigo(_ordemAtual.DescricaoMaterial, _ordemAtual.MaterialProduzido);
        finishedProductTextBox.Text = descricaoUtil ? _ordemAtual.DescricaoMaterial : string.Empty;
        finishedProductTextBox.Visible = descricaoUtil;
        lotTextBox.Text = _ordemAtual.Lote;
        ovenExitTextBox.Text = _ordemAtual.DepositoDestino;
        classificationDateTextBox.Text = FormatarKg(_ordemAtual.QuantidadePendente);
        manufacturingDateTextBox.Text = FormatarKg(_ordemAtual.QuantidadePlanejada);
        expirationDateTextBox.Text = FormatarKg(_ordemAtual.QuantidadeEntregue);
        readForecastBoxesTextBox.Text = _normaEmbalagem?.QuantidadeProdutosPorCaixa.ToString(CultureInfo.InvariantCulture) ?? "0";
        readForecastPackagesTextBox.Text = _normaEmbalagem?.PackagingInstruction ?? string.Empty;
    }

    private void PreencherNormaEmbalagem()
    {
        materialDataGridView.Rows.Clear();
        if (_normaEmbalagem is not null)
        {
            foreach (ProdutoAcabadoNormaItem item in _normaEmbalagem.Itens)
            {
                materialDataGridView.Rows.Add(item.TipoMaterial, item.Material, item.Item, item.Quantidade, item.Unidade, _normaEmbalagem.PackagingInstruction);
            }
        }

        // Tarefa 21.6 (Ajuste 4): grid vazio mostra mensagem amigavel em vez de tabela em branco.
        AtualizarMensagemNormaVazia();
        AtualizarCampoQuantidadePorCaixa();
    }

    private void AtualizarCampoQuantidadePorCaixa()
    {
        bool fallback = NormaEmFallbackMemoria();

        readForecastBoxesCaptionLabel.Text = "QTD. POR CAIXA";
        readForecastPackagesCaptionLabel.Text = "NORMA EMBALAGEM";
        readForecastBoxesTextBox.ReadOnly = !fallback;
        readForecastBoxesTextBox.Enabled = _ordemAtual is not null;
        readForecastBoxesTextBox.Multiline = false;
        readForecastBoxesTextBox.TextAlign = HorizontalAlignment.Left;
        readForecastBoxesTextBox.BackColor = fallback ? Color.White : Color.FromArgb(248, 250, 252);
        readForecastBoxesTextBox.ForeColor = Color.FromArgb(17, 24, 39);
        readForecastBoxesTextBox.Cursor = fallback ? Cursors.IBeam : Cursors.Default;
        readForecastBoxesTextBox.TabStop = fallback;

        // Tarefa 21.6 (Ajuste 2): STATUS NORMA + MATERIAL CAIXA + aviso discreto de fallback.
        // Tarefa 21.6.3 (Ajuste 14): fallback em ÂMBAR (não vermelho crítico); SAP OK em verde discreto.
        balanceTextBox.Text = _ordemAtual is null
            ? string.Empty
            : fallback ? "FALLBACK MEMÓRIA" : "SAP OK";
        balanceTextBox.ForeColor = _ordemAtual is null
            ? Color.FromArgb(17, 24, 39)
            : fallback ? Color.FromArgb(180, 83, 9)     // âmbar/laranja discreto
                       : Color.FromArgb(22, 101, 52);   // verde discreto
        if (_statusNormaValorLabel is not null)
        {
            // A coluna já tem a legenda "MATERIAL CAIXA" — aqui vai só o valor (ou "-").
            _statusNormaValorLabel.Text = _normaEmbalagem is null || string.IsNullOrWhiteSpace(_normaEmbalagem.MaterialCaixa)
                ? "-"
                : _normaEmbalagem.MaterialCaixa;
        }

        if (_avisoNormaFallbackLabel is not null)
        {
            _avisoNormaFallbackLabel.Visible = fallback;
            _avisoNormaFallbackLabel.Text = "Norma SAP indisponível. Quantidade por caixa informada manualmente.";
        }
    }

    /// <summary>
    /// Tarefa 21.6.2 (Ajuste 3): MATERIAL CAIXA + aviso de fallback DENTRO do card PRODUÇÃO PLANEJADA
    /// (Gpb_PrevisaoLeitura) — não na lateral. MATERIAL CAIXA na linha do título (espaço livre à direita);
    /// aviso na faixa inferior do card. STATUS NORMA continua no card via balanceTextBox.
    /// </summary>
    /// <summary>
    /// Tarefa 21.6.4 (Ajuste 5): envolve o campo QTD. POR CAIXA numa caixa arredondada, na própria célula
    /// da tabela (o campo OP e a pesquisa já são RoundedPanel). Preserva eventos/validação (só reparenta).
    /// </summary>
    private void ConfigurarCampoQtdArredondado()
    {
        if (readForecastBoxesTextBox.Parent is not TableLayoutPanel tabela)
        {
            return;
        }

        TableLayoutPanelCellPosition celula = tabela.GetCellPosition(readForecastBoxesTextBox);
        tabela.Controls.Remove(readForecastBoxesTextBox);
        readForecastBoxesTextBox.BorderStyle = BorderStyle.None;
        readForecastBoxesTextBox.Dock = DockStyle.Fill;
        FugaPET_Dev.Tela.Controls.RoundedPanel caixa = new()
        {
            Name = "qtdPorCaixaCaixaArredondada",
            BorderRadius = 8,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(209, 213, 219),
            FillColor = Color.White,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 1, 8, 1),
            Padding = new Padding(8, 2, 8, 2)
        };
        caixa.Controls.Add(readForecastBoxesTextBox);
        tabela.Controls.Add(caixa, celula.Column, celula.Row);
    }

    private void ConfigurarCardNormaExtra()
    {
        // Tarefa 21.6.4 (Ajuste 4): PRODUÇÃO PLANEJADA vira 4 COLUNAS reais
        // (QTD. POR CAIXA | NORMA EMBALAGEM | STATUS NORMA | MATERIAL CAIXA).
        tableLayoutPanel8.ColumnStyles.Clear();
        tableLayoutPanel8.ColumnCount = 4;
        for (int i = 0; i < 4; i++)
        {
            tableLayoutPanel8.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        _materialCaixaCaptionLabel = new Label
        {
            Name = "materialCaixaCaptionLabel",
            AutoSize = false,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Font = FonteLabelCampo,
            ForeColor = readForecastBoxesCaptionLabel.ForeColor,
            Margin = readForecastBoxesCaptionLabel.Margin,
            Text = "MATERIAL CAIXA",
            TextAlign = ContentAlignment.MiddleLeft
        };
        tableLayoutPanel8.Controls.Add(_materialCaixaCaptionLabel, 3, 0);

        _materialCaixaValorLabel = new Label
        {
            Name = "materialCaixaValorLabel",
            AutoSize = false,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Font = FonteValorCampo,
            ForeColor = Color.FromArgb(17, 24, 39),
            Margin = balanceTextBox.Margin,
            Text = "-",
            TextAlign = ContentAlignment.MiddleLeft
        };
        tableLayoutPanel8.Controls.Add(_materialCaixaValorLabel, 3, 1);
        _statusNormaValorLabel = _materialCaixaValorLabel; // AtualizarCampoQuantidadePorCaixa preenche o valor

        Control cardNorma = plannedProductionTitleLabel.Parent ?? Gpb_PrevisaoLeitura;

        // 3º divisor (entre STATUS NORMA e MATERIAL CAIXA), posicionado por AlignPlannedProductionCardLayout.
        _plannedDivider3 = new Label
        {
            Name = "plannedDivider3",
            AutoSize = false,
            BackColor = Color.FromArgb(226, 231, 238),
            Size = new Size(1, 22)
        };
        cardNorma.Controls.Add(_plannedDivider3);

        // Aviso de fallback numa linha própria discreta, abaixo das 4 colunas.
        _avisoNormaFallbackLabel = new Label
        {
            Name = "avisoNormaFallbackLabel",
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = FonteAuxiliar, // Tarefa 21.6.5 (Ajuste 6)
            ForeColor = Color.FromArgb(180, 83, 9),
            Location = new Point(17, 78),
            Size = new Size(630, 18),
            Text = "Norma SAP indisponível. Quantidade por caixa informada manualmente.",
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        cardNorma.Controls.Add(_avisoNormaFallbackLabel);
        _avisoNormaFallbackLabel.BringToFront();
        AlignPlannedProductionCardLayout(this, EventArgs.Empty);
    }

    /// <summary>Tarefa 21.6.1 (Ajuste 8): pendência operacional da balança na lateral direita (sidePanel).</summary>
    private void CriarBlocoPendenciaBalanca()
    {
        _pendenciaBalancaTituloLabel = new Label
        {
            Name = "pendenciaBalancaTituloLabel",
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = FonteLabelCampo, // Tarefa 21.6.5 (Ajuste 6)
            ForeColor = Color.FromArgb(180, 83, 9),
            Location = new Point(12, 392),
            Size = new Size(195, 18),
            Text = "PENDÊNCIA OPERACIONAL",
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        sidePanel.Controls.Add(_pendenciaBalancaTituloLabel);

        _pendenciaBalancaTextoLabel = new Label
        {
            Name = "pendenciaBalancaTextoLabel",
            AutoSize = false,
            BackColor = Color.FromArgb(254, 243, 199),
            Font = FonteAuxiliar, // Tarefa 21.6.5 (Ajuste 6)
            ForeColor = Color.FromArgb(120, 53, 15),
            Location = new Point(12, 410),
            Size = new Size(195, 62),
            Text = "Balança de produto acabado não configurada.\r\nUse F9 para peso manual ou configure a balança.",
            TextAlign = ContentAlignment.TopLeft,
            Visible = false
        };
        sidePanel.Controls.Add(_pendenciaBalancaTextoLabel);
        _pendenciaBalancaTituloLabel.BringToFront();
        _pendenciaBalancaTextoLabel.BringToFront();
    }

    private void AtualizarPendenciaBalanca()
    {
        bool balancaConfigurada = _contextoTerminal?.IdBalancaPadrao is long id && id > 0;
        bool exibir = !balancaConfigurada; // sem balança = pendencia operacional (F9 continua liberado)
        if (_pendenciaBalancaTituloLabel is not null)
        {
            _pendenciaBalancaTituloLabel.Visible = exibir;
        }

        if (_pendenciaBalancaTextoLabel is not null)
        {
            _pendenciaBalancaTextoLabel.Visible = exibir;
        }
    }

    private bool NormaEmFallbackMemoria()
        => _normaEmbalagem is not null
            && string.Equals(_normaEmbalagem.PackagingInstruction, "FALLBACK_MEMORIA", StringComparison.OrdinalIgnoreCase);

    private void ToggleProductionFromSideButton_Click(object? sender, EventArgs e)
    {
        if (_leituraIniciada)
        {
            AtualizarEstadoLeitura(false);
            statusLabel.Text = "Leitura de produto acabado parada.";
            return;
        }

        // §4: com caixa ATIVA, não reiniciar a leitura — exige confirmar/cancelar a caixa atual antes.
        if (PrimeiraEntregaHu && ExisteCaixaAtiva())
        {
            AvisarCaixaAtivaPendente();
            return;
        }

        if (_ordemAtual is null)
        {
            MessageBox.Show("Selecione uma OP antes de iniciar a leitura.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!AtualizarNormaFallbackAntesDaLeitura())
        {
            return;
        }

        AtualizarEstadoLeitura(true);
        statusLabel.Text = "Leitura de caixas iniciada. Use F12 ou F9 para pesar.";
    }

    private async void ReadWeightLegend_Click(object? sender, EventArgs e)
    {
        await RegistrarPesoBalancaAsync();
    }

    private async void LeituraManual_Click(object? sender, EventArgs e)
    {
        await RegistrarPesoManualAsync();
    }

    private async Task RegistrarPesoBalancaAsync()
    {
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar, "ler peso de produto acabado"))
        {
            return;
        }

        if (!ValidarPodePesar())
        {
            return;
        }

        if (!GarantirBalancaProdutoAcabadoConfigurada())
        {
            return;
        }

        TaraCadastro? tara = await GarantirTaraCaixaSelecionadaAsync();
        if (tara is null)
        {
            return;
        }

        ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
        if (!leitura.Sucesso)
        {
            string mensagem = string.IsNullOrWhiteSpace(leitura.Mensagem) ? "Não foi possível ler o peso da balança." : leitura.Mensagem;
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryParsePesoKg(leitura.Peso, out decimal pesoBrutoKg))
        {
            MessageBox.Show("Peso retornado pela balança é inválido.", "Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RegistrarCaixaProdutoAcabado(pesoBrutoKg, tara.PesoKg, "BALANCA");
    }

    private async Task RegistrarPesoManualAsync()
    {
        // TODO Permissões:
        // Quando a matriz de permissões do Produto Acabado for criada,
        // separar permissão de peso manual se o negócio exigir.
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar, "informar peso manual de produto acabado"))
        {
            return;
        }

        if (!ValidarPodePesar())
        {
            return;
        }

        TaraCadastro? tara = await GarantirTaraCaixaSelecionadaAsync();
        if (tara is null)
        {
            return;
        }

        if (!SolicitarPesoManual(tara.PesoKg, out decimal pesoBrutoKg))
        {
            return;
        }

        RegistrarCaixaProdutoAcabado(pesoBrutoKg, tara.PesoKg, "MANUAL");
    }

    private bool RegistrarCaixaProdutoAcabado(decimal pesoBrutoKg, decimal taraKg, string origem)
    {
        if (_ordemAtual is null || _normaEmbalagem is null)
        {
            return false;
        }

        // §7: UMA CAIXA POR VEZ. Enquanto houver uma caixa ativa, não registra outra — sem usar apenas
        // Count+1 como autorização. Exige confirmar/cancelar a caixa atual antes da próxima.
        if (PrimeiraEntregaHu && ExisteCaixaAtiva())
        {
            MessageBox.Show(
                "Já existe uma caixa em andamento. Confirme o envio ou cancele a caixa atual antes de pesar outra.",
                "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        decimal pesoLiquidoKg = pesoBrutoKg - taraKg;
        if (pesoLiquidoKg <= 0m)
        {
            MessageBox.Show("Peso líquido da caixa deve ser maior que zero.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        decimal totalNovo = _caixasPesadas.Sum(caixa => caixa.PesoLiquidoKg) + pesoLiquidoKg;
        if (totalNovo > _ordemAtual.QuantidadePendente)
        {
            MessageBox.Show("Peso total das caixas ultrapassa o saldo pendente da OP.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // §9: material de embalagem da CAIXA obtido explicitamente (origem controlada, nunca PALLET01).
        (string materialEmbalagem, OrigemMaterialEmbalagemCaixa origemEmbalagem) = ObterMaterialEmbalagemCaixaControlada();

        ProdutoAcabadoCaixa caixa;
        try
        {
            caixa = _controller.MontarCaixaSemIdentidadeSequencial(
                _ordemAtual,
                _normaEmbalagem,
                pesoBrutoKg,
                taraKg,
                origem,
                materialEmbalagem: string.IsNullOrWhiteSpace(materialEmbalagem) ? null : materialEmbalagem,
                origemMaterialEmbalagem: origemEmbalagem);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // §9: valida o preview da HU ANTES de adicionar a caixa. Preview inválido (ex.: material de embalagem
        // ausente) BLOQUEIA a finalização — a pesagem não é perdida em silêncio nem a caixa é adicionada.
        FugaPET_Dev.Servicos.IntegracaoSap.ResultadoPreviewHandlingUnitCaixa preview =
            _controller.GerarPreviewHandlingUnitCaixa(_ordemAtual, caixa);
        if (!preview.Sucesso)
        {
            MessageBox.Show(
                $"Não foi possível preparar a caixa para o SAP: {preview.Mensagem}",
                "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        _caixasPesadas.Add(caixa);
        // Produto Acabado deixou o movimento 101: preview é da Handling Unit da caixa (contrato SAP ainda não
        // confirmado; nenhum POST). Nome interno de propriedades, não o JSON externo definitivo.
        System.Diagnostics.Trace.TraceInformation(
            "[ProdutoAcabado] Preview Handling Unit caixa {0} (contrato SAP não confirmado): {1}",
            caixa.NumeroCaixa, preview.PayloadJsonSanitizado);
        caixa.TransicionarPara(StatusIntegracaoCaixa.PreviewHuGerado);
        caixa.TransicionarPara(StatusIntegracaoCaixa.AguardandoAutorizacaoSap);
        AtualizarGridCaixas();
        AtualizarCamposPaletizacaoPadrao();
        AtualizarResumoOperacional();
        AtualizarEstadoEnvioCaixaSap();
        statusLabel.Text = $"Caixa registrada em memória, aguardando numeração persistente (FINALIZADA LOCAL). Bruto: {FormatarKg(pesoBrutoKg)} | Tara: {FormatarKg(taraKg)} | Líquido: {FormatarKg(pesoLiquidoKg)}.";
        return true;
    }

    /// <summary>§7: existe caixa ativa (não confirmada/cancelada) em andamento na tela.</summary>
    private bool ExisteCaixaAtiva()
        => _caixasPesadas.Any(c =>
            c.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap
            && c.StatusIntegracao != StatusIntegracaoCaixa.Cancelada);

    /// <summary>§3: oculta um controle de palete localizado por Name (o título é criado localmente).</summary>
    private void OcultarControlePaletePorNome(string nome)
    {
        foreach (Control controle in Controls.Find(nome, true))
        {
            controle.Visible = false;
        }
    }

    /// <summary>
    /// §6/§9: material de embalagem da caixa por origem REAL e explícita (nunca PALLET01, nunca código
    /// fictício). Única origem confirmada nesta fase: <c>_normaEmbalagem.MaterialCaixa</c> (quando preenchido
    /// e diferente de PALLET01). Não existindo origem real ⇒ retorna vazio; o preview então BLOQUEIA a
    /// finalização com "Material de embalagem da caixa não informado." Nenhum fallback inventado.
    /// </summary>
    private (string material, OrigemMaterialEmbalagemCaixa origem) ObterMaterialEmbalagemCaixaControlada()
    {
        string? daNorma = _normaEmbalagem?.MaterialCaixa;
        if (!string.IsNullOrWhiteSpace(daNorma) && !EhMaterialPalete(daNorma))
        {
            return (daNorma.Trim(), OrigemMaterialEmbalagemCaixa.Sap);
        }

        // Sem origem real (norma SAP não trouxe MaterialCaixa e não há campo de operador aprovado): vazio.
        return (string.Empty, OrigemMaterialEmbalagemCaixa.NaoInformada);
    }

    private static bool EhMaterialPalete(string? material)
        => !string.IsNullOrWhiteSpace(material)
            && material.Trim().Equals("PALLET01", StringComparison.OrdinalIgnoreCase);

    private bool ValidarPodePesar()
    {
        // §4: bloqueio preventivo — com caixa ativa pendente não abre pesagem (F9/F12/manual/balança).
        if (PrimeiraEntregaHu && ExisteCaixaAtiva())
        {
            AvisarCaixaAtivaPendente();
            return false;
        }

        if (!_leituraIniciada)
        {
            MessageBox.Show("Inicie a leitura antes de registrar caixa.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_ordemAtual is null)
        {
            MessageBox.Show("Selecione uma OP antes de registrar caixa.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_normaEmbalagem is null || _normaEmbalagem.QuantidadeProdutosPorCaixa <= 0)
        {
            MessageBox.Show("Norma de embalagem não localizada para o produto informado.", "Norma de Embalagem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private bool AtualizarNormaFallbackAntesDaLeitura()
    {
        if (_ordemAtual is null || _normaEmbalagem is null)
        {
            return false;
        }

        if (!string.Equals(_normaEmbalagem.PackagingInstruction, "FALLBACK_MEMORIA", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!int.TryParse(readForecastBoxesTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantidadePorCaixa)
            || quantidadePorCaixa <= 0)
        {
            string mensagem = "Informe a QTD. POR CAIXA para continuar. Enquanto a norma de embalagem SAP não estiver disponível, essa quantidade será usada em cada caixa pesada.";
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Norma de Embalagem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            readForecastBoxesTextBox.Focus();
            readForecastBoxesTextBox.SelectAll();
            return false;
        }

        _normaEmbalagem = _controller.ConsultarOuPrepararNormaEmbalagem(
            _ordemAtual.MaterialProduzido,
            quantidadePorCaixa,
            _normaEmbalagem.PackagingInstruction);
        PreencherNormaEmbalagem();
        statusLabel.Text = $"Quantidade por caixa definida: {quantidadePorCaixa} produto(s) por caixa.";
        return true;
    }

    private bool GarantirBalancaProdutoAcabadoConfigurada()
    {
        try
        {
            _contextoTerminal = EstadoTerminalLocalAtual.ObterContextoAtualizado();
        }
        catch
        {
            _contextoTerminal = null;
        }

        if (_contextoTerminal?.IdBalancaPadrao is not long idBalanca || idBalanca <= 0)
        {
            statusLabel.Text = MensagemBalancaProdutoAcabadoNaoConfigurada;
            MessageBox.Show(MensagemBalancaProdutoAcabadoNaoConfigurada, "Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async Task<TaraCadastro?> GarantirTaraCaixaSelecionadaAsync()
    {
        if (_taraCaixaSelecionada is not null)
        {
            return _taraCaixaSelecionada;
        }

        if (_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0)
        {
            string mensagem = "Usuário sem setor definido: não é possível selecionar a tara da caixa.";
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Seleção de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        IReadOnlyList<TaraCadastro> taras = await _controller.ListarTarasAtivasPorSetorAsync(codigoSetor);
        if (taras.Count == 0)
        {
            string mensagem = "Nenhuma tara ativa para o setor do usuário. Cadastre uma tara antes de pesar.";
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Seleção de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        using SelecaoTaraPesagemForm form = new(taras, "CAIXA PRODUTO ACABADO");
        if (form.ShowDialog(this) != DialogResult.OK || form.TaraSelecionada is null)
        {
            statusLabel.Text = "Seleção de tara cancelada.";
            return null;
        }

        _taraCaixaSelecionada = form.TaraSelecionada;
        statusLabel.Text = $"Tara '{form.TaraSelecionada.NomeTara}' selecionada para caixas de produto acabado.";
        return _taraCaixaSelecionada;
    }

    private void AtualizarGridCaixas()
    {
        productionDataGridView.Rows.Clear();
        foreach (ProdutoAcabadoCaixa caixa in _caixasPesadas)
        {
            // Tarefa 21.6 (Ajuste 6): uma coluna por conceito (bruto, tara, liquido, origem, status, palete, HU).
            productionDataGridView.Rows.Add(
                caixa.NumeroCaixa > 0
                    ? caixa.NumeroCaixa.ToString("0000", CultureInfo.InvariantCulture)
                    : "PEND.",
                FormatarKg(caixa.PesoBrutoKg),
                FormatarKg(caixa.TaraKg),
                FormatarKg(caixa.PesoLiquidoKg),
                caixa.QuantidadeProdutos.ToString(CultureInfo.InvariantCulture),
                caixa.OrigemPesagem,
                caixa.StatusIntegracao.ToString(),
                string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal) ? "-" : caixa.CodigoPaleteLocal,
                string.IsNullOrWhiteSpace(caixa.HandlingUnitExternalId) ? "-" : caixa.HandlingUnitExternalId);
        }
    }

    private void CancelarUltimaCaixa()
    {
        if (_caixasPesadas.Count == 0)
        {
            return;
        }

        ProdutoAcabadoCaixa caixa = _caixasPesadas[^1];
        if (!TransicaoStatusIntegracaoCaixa.PodeTransitar(caixa.StatusIntegracao, StatusIntegracaoCaixa.Cancelada))
        {
            MessageBox.Show(
                "A caixa atual não pode ser cancelada no estado de integração em que se encontra.",
                "Produto Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        caixa.TransicionarPara(StatusIntegracaoCaixa.Cancelada);
        _caixasPesadas.RemoveAt(_caixasPesadas.Count - 1);
        AtualizarGridCaixas();
        AtualizarCamposPaletizacaoPadrao();
        AtualizarResumoOperacional();
        AtualizarEstadoEnvioCaixaSap();
        AtualizarBotoesOperacao();
        statusLabel.Text = "Última caixa de produto acabado cancelada.";
    }

    private void CriarPaleteLocal()
    {
        // §8: na 1ª entrega (caixa individual via HU) a formação de palete está FORA do fluxo ativo.
        // O acionamento é neutralizado aqui — a Form não monta palete nem gera preview de palete.
        // O código permanece para fase futura, mas não é acionável enquanto PrimeiraEntregaHu.
        if (PrimeiraEntregaHu)
        {
            return;
        }

        if (_ordemAtual is null || _caixasPesadas.Count == 0)
        {
            MessageBox.Show("Registre caixas antes de criar o palete.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryLerDadosPaletizacao(out int primeiraCaixa, out int ultimaCaixa, out string materialEmbalagem, out string mensagem))
        {
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            ProdutoAcabadoPalete palete = MontarPaletePorIntervalo(primeiraCaixa, ultimaCaixa, materialEmbalagem);
            ResultadoPreviewProdutoAcabadoPalete preview = _controller.GerarPreviewPalete(palete);
            if (!preview.Sucesso)
            {
                statusLabel.Text = preview.Mensagem;
                MessageBox.Show(preview.Mensagem, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _paletesMontados.Add(palete);
            AtualizarGridCaixas();
            AtualizarGridPaletes();
            AtualizarCamposPaletizacaoPadrao();
            AtualizarResumoOperacional();
            System.Diagnostics.Trace.TraceInformation("[ProdutoAcabado] Payload Palete local {0}: {1}", palete.CodigoPaleteLocal, preview.PayloadJson);
            statusLabel.Text = "Palete criado localmente. Envio SAP da HU/palete pendente de liberação da API.";
            MessageBox.Show("Palete criado localmente. Envio SAP da HU/palete pendente de liberação da API.", "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            statusLabel.Text = ex.Message;
            MessageBox.Show(ex.Message, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private bool TryLerDadosPaletizacao(out int primeiraCaixa, out int ultimaCaixa, out string materialEmbalagem, out string mensagem)
    {
        primeiraCaixa = 0;
        ultimaCaixa = 0;
        materialEmbalagem = materialEmbalagemPaleteTextBox.Text.Trim();
        mensagem = string.Empty;

        if (string.IsNullOrWhiteSpace(primeiraCaixaTextBox.Text))
        {
            mensagem = "Informe a primeira caixa do palete.";
            return false;
        }

        try
        {
            primeiraCaixa = LerPrimeiraCaixaInformada();
        }
        catch (InvalidOperationException ex)
        {
            mensagem = ex.Message;
            return false;
        }

        if (string.IsNullOrWhiteSpace(ultimaCaixaTextBox.Text))
        {
            mensagem = "Informe a última caixa do palete.";
            return false;
        }

        try
        {
            ultimaCaixa = LerUltimaCaixaInformada();
        }
        catch (InvalidOperationException ex)
        {
            mensagem = ex.Message;
            return false;
        }

        if (primeiraCaixa > ultimaCaixa)
        {
            mensagem = "A primeira caixa não pode ser maior que a última.";
            return false;
        }

        int primeiraCaixaFiltro = primeiraCaixa;
        int ultimaCaixaFiltro = ultimaCaixa;
        ProdutoAcabadoCaixa[] caixasIntervalo = _caixasPesadas
            .Where(caixa => caixa.NumeroCaixa >= primeiraCaixaFiltro && caixa.NumeroCaixa <= ultimaCaixaFiltro)
            .OrderBy(caixa => caixa.NumeroCaixa)
            .ToArray();
        if (caixasIntervalo.Length == 0 || caixasIntervalo.Length != ultimaCaixa - primeiraCaixa + 1)
        {
            mensagem = "Nenhuma caixa encontrada no intervalo informado.";
            return false;
        }

        ProdutoAcabadoCaixa[] caixasPaletizadas = caixasIntervalo
            .Where(caixa => !string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal))
            .ToArray();
        if (caixasPaletizadas.Length > 0)
        {
            string lista = string.Join(
                ", ",
                caixasPaletizadas.Select(caixa => $"{caixa.NumeroCaixa:0000} ({caixa.CodigoPaleteLocal})"));
            mensagem = $"Não é possível criar o palete. Caixa(s) já vinculada(s): {lista}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(materialEmbalagem))
        {
            mensagem = "Informe o material de embalagem do palete.";
            return false;
        }

        return true;
    }

    private ProdutoAcabadoPalete MontarPaletePorIntervalo(int primeiraCaixa, int ultimaCaixa, string materialEmbalagem)
    {
        if (_ordemAtual is null)
        {
            throw new InvalidOperationException("OP não carregada para montar palete.");
        }

        return _controller.MontarPalete(
            _ordemAtual,
            _caixasPesadas,
            primeiraCaixa,
            ultimaCaixa,
            materialEmbalagem);
    }

    private int LerPrimeiraCaixaInformada()
    {
        if (!int.TryParse(primeiraCaixaTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int primeiraCaixa))
        {
            throw new InvalidOperationException("Informe a primeira caixa do palete.");
        }

        return primeiraCaixa;
    }

    private int LerUltimaCaixaInformada()
    {
        if (!int.TryParse(ultimaCaixaTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ultimaCaixa))
        {
            throw new InvalidOperationException("Informe a última caixa do palete.");
        }

        return ultimaCaixa;
    }

    private void AtualizarCamposPaletizacaoPadrao()
    {
        if (primeiraCaixaTextBox is null || ultimaCaixaTextBox is null || materialEmbalagemPaleteTextBox is null)
        {
            return;
        }

        ProdutoAcabadoCaixa[] caixasLivres = _caixasPesadas
            .Where(caixa => string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal))
            .OrderBy(caixa => caixa.NumeroCaixa)
            .ToArray();

        if (_caixasPesadas.Count > 0 && caixasLivres.Length == 0)
        {
            primeiraCaixaTextBox.Text = string.Empty;
            ultimaCaixaTextBox.Text = string.Empty;
            statusLabel.Text = "Todas as caixas pesadas já foram vinculadas a paletes.";
        }
        else
        {
            primeiraCaixaTextBox.Text = caixasLivres.FirstOrDefault()?.NumeroCaixa.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            ultimaCaixaTextBox.Text = caixasLivres.LastOrDefault()?.NumeroCaixa.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(materialEmbalagemPaleteTextBox.Text))
        {
            materialEmbalagemPaleteTextBox.Text = "PALLET01";
        }
    }

    private void AtualizarGridPaletes()
    {
        if (paletesDataGridView is null)
        {
            return;
        }

        paletesDataGridView.Rows.Clear();
        foreach (ProdutoAcabadoPalete palete in _paletesMontados)
        {
            paletesDataGridView.Rows.Add(
                palete.CodigoPaleteLocal,
                palete.PrimeiraCaixa.ToString("0000", CultureInfo.InvariantCulture),
                palete.UltimaCaixa.ToString("0000", CultureInfo.InvariantCulture),
                palete.Caixas.Count.ToString(CultureInfo.InvariantCulture),
                FormatarKg(palete.PesoBrutoKg),
                FormatarKg(palete.PesoLiquidoKg),
                FormatarKg(palete.TaraKg),
                palete.PackagingMaterial,
                "PENDENTE SAP");
        }
    }

    private async Task ImprimirEtiquetaCaixaAsync(ProdutoAcabadoCaixa caixa)
    {
        await Task.CompletedTask;
        System.Diagnostics.Trace.TraceInformation("[ProdutoAcabado] Ponto de extensão etiqueta caixa: {0}", caixa.CodigoCaixaLocal);
    }

    private async Task ImprimirEtiquetaPaleteAsync(ProdutoAcabadoPalete palete)
    {
        await Task.CompletedTask;
        System.Diagnostics.Trace.TraceInformation("[ProdutoAcabado] Ponto de extensão etiqueta palete: {0}", palete.CodigoPaleteLocal);
    }

    private void AtualizarEstadoLeitura(bool iniciada)
    {
        _leituraIniciada = iniciada;
        sideReadingStatusLabel.Text = iniciada ? "Ativo" : "Inativo";
        sideReadingStatusLabel.ForeColor = iniciada ? ReadingStatusActiveColor : ReadingStatusInactiveColor;
        AtualizarStatusCardLeitura(iniciada);
        AtualizarBloqueioCabecalho(iniciada);
        productionOrderTextBox.Enabled = !iniciada;
        productionOrderSearchLabel.Enabled = !iniciada;
        AtualizarBotoesOperacao();
    }

    private void AtualizarStatusCardLeitura(bool iniciada)
    {
        Color statusColor = iniciada ? ReadingStatusActiveColor : ReadingStatusInactiveColor;
        statusCard.BackColor = Color.Transparent;
        statusCard.FillColor = iniciada
            ? Color.FromArgb(229, 247, 234)
            : Color.FromArgb(254, 232, 232);
        statusCard.BorderColor = iniciada
            ? Color.FromArgb(187, 229, 199)
            : Color.FromArgb(248, 190, 190);
        statusCardIcon.Text = iniciada ? "✓" : "!";
        statusCardIcon.ForeColor = statusColor;
        statusValueLabel.Text = iniciada ? "ATIVA" : "INATIVA";
        statusValueLabel.ForeColor = statusColor;
        statusHintLabel.Text = iniciada
            ? "Leitura liberada para registro"
            : "Leitura aguardando inicio";
        statusHintLabel.ForeColor = Color.FromArgb(98, 108, 124);
    }

    private void AtualizarBloqueioCabecalho(bool bloqueado)
    {
        menuHeaderLabel.Visible = !bloqueado;
        minimizeWindowLabel.Visible = !bloqueado;
        maximizeWindowLabel.Visible = !bloqueado;
        closeWindowLabel.Visible = !bloqueado;
        menuHeaderLabel.Enabled = !bloqueado;
        minimizeWindowLabel.Enabled = !bloqueado;
        maximizeWindowLabel.Enabled = !bloqueado;
        closeWindowLabel.Enabled = !bloqueado;
        customTitleBarPanel.Cursor = bloqueado ? Cursors.Default : Cursors.SizeAll;
        companyLogoPictureBox.Cursor = customTitleBarPanel.Cursor;
        headerTitleLabel.Cursor = customTitleBarPanel.Cursor;
        headerSubtitleLabel.Cursor = customTitleBarPanel.Cursor;
    }

    private void AtualizarBotoesOperacao()
    {
        // Tarefa 21.6.2 (Ajuste 1): iniciar verde com OP + QTD. por caixa válida (falta de balança não impede).
        bool livre = !_operacaoEmAndamento;
        // §4: com caixa ATIVA (finalizada aguardando integração) a leitura NÃO pode reiniciar — apenas parar
        // uma leitura já em andamento. Bloqueio preventivo, não só dentro de RegistrarCaixaProdutoAcabado.
        bool caixaAtiva = ExisteCaixaAtiva();
        bool podeAlternarLeitura = livre && _ordemAtual is not null && QuantidadePorCaixaValida() && !caixaAtiva;
        iniciarLeituraButton.PrimaryText = _leituraIniciada ? "PARAR LEITURA" : "INICIAR LEITURA";
        iniciarLeituraButton.IconGlyph = _leituraIniciada ? "\uE71A" : "\uE768";
        iniciarLeituraButton.BaseBackColor = _leituraIniciada
            ? Color.FromArgb(212, 37, 49)                        // vermelho parar
            : podeAlternarLeitura ? Color.FromArgb(34, 166, 82)  // verde iniciar
                                  : Color.FromArgb(156, 163, 175); // cinza desabilitado
        iniciarLeituraButton.BaseForeColor = Color.White;
        iniciarLeituraButton.Enabled = _leituraIniciada || podeAlternarLeitura;
        iniciarLeituraButton.Cursor = (_leituraIniciada || podeAlternarLeitura) ? Cursors.Hand : Cursors.Default;
        iniciarLeituraButton.Invalidate(); // ActionPillButton é custom-painted: precisa repintar a cor
        lerEtiquetaButton.Visible = _leituraIniciada;
        leituraManualButton.Visible = _leituraIniciada;
        // §4: F9 (etiqueta) e leitura manual só com leitura ativa E sem caixa ativa pendente.
        lerEtiquetaButton.Enabled = livre && _leituraIniciada && _ordemAtual is not null && !caixaAtiva;
        leituraManualButton.Enabled = livre && _leituraIniciada && _ordemAtual is not null && !caixaAtiva;

        // §3: na 1ª entrega (caixa individual via HU) o fluxo de palete NÃO pode reaparecer em NENHUMA
        // atualização de estado. Ramo explícito mantém botão/card/título/grid ocultos e desabilitados.
        if (PrimeiraEntregaHu)
        {
            productionActionsButton.Visible = false;
            productionActionsButton.Enabled = false;
            if (_criarPaleteCard is not null)
            {
                _criarPaleteCard.Visible = false;
            }

            OcultarControlePaletePorNome("paletesCriadosTituloLabel");
            if (paletesDataGridView is not null)
            {
                paletesDataGridView.Visible = false;
            }

            if (_paleteMensagemLabel is not null)
            {
                _paleteMensagemLabel.Visible = false;
            }
        }
        else
        {
            // Comportamento futuro do palete existente (fora do escopo desta 1ª entrega).
            productionActionsButton.Visible = !_leituraIniciada;
            productionActionsButton.Enabled = !_leituraIniciada && livre && ExisteCaixaLivreParaPalete();
            if (_paleteMensagemLabel is not null)
            {
                _paleteMensagemLabel.Text = _caixasPesadas.Count == 0
                    ? "Registre caixas para criar um palete."
                    : ExisteCaixaLivreParaPalete() ? string.Empty
                    : "Todas as caixas já foram vinculadas a paletes.";
                _paleteMensagemLabel.Visible = _paleteMensagemLabel.Text.Length > 0;
            }
        }

        AtualizarEstadoEnvioCaixaSap();

        // Tarefa 21.6.2 (Ajuste 10): "Excluir última caixa" fica VISÍVEL porém cinza/desabilitado sem caixa.
        // (deleteByCodeLegendPanel aqui é "Esc - Fechar" — NÃO desabilitar, senão trava o fechamento.)
        bool possuiCaixa = _caixasPesadas.Count > 0;
        deleteLastLegendPanel.Enabled = possuiCaixa;
        deleteLastLegendTextLabel.ForeColor = possuiCaixa ? Color.FromArgb(229, 231, 235) : Color.FromArgb(120, 126, 136);
    }

    /// <summary>Tarefa 21.6.2: QTD. por caixa válida (norma real ou fallback digitado &gt; 0).</summary>
    private bool QuantidadePorCaixaValida()
    {
        if (_normaEmbalagem is null)
        {
            return false;
        }

        if (_normaEmbalagem.QuantidadeProdutosPorCaixa > 0)
        {
            return true;
        }

        return NormaEmFallbackMemoria()
            && int.TryParse(readForecastBoxesTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int q)
            && q > 0;
    }

    private bool ExisteCaixaLivreParaPalete()
        => _caixasPesadas.Any(c => string.IsNullOrWhiteSpace(c.CodigoPaleteLocal));

    private void AtualizarResumoOperacional()
    {
        // Tarefa 21.6.3 (Ajuste 13): contadores sem total fixo; unidade da OP decide peso x produtos.
        int qtdCaixas = _caixasPesadas.Count;
        boxesCounterLabel.Text = qtdCaixas.ToString("000", CultureInfo.InvariantCulture);
        boxesValueLabel.Text = qtdCaixas.ToString("000", CultureInfo.InvariantCulture);
        boxesTotalLabel.Text = "de 0";

        bool temOp = _ordemAtual is not null;
        string unidade = (_ordemAtual?.Unidade ?? "KG").Trim().ToUpperInvariant();
        bool ehPeso = unidade is "KG" or "KGM" or "G" or "TO" or "";
        if (ehPeso)
        {
            packagesTitleLabel.Text = "PESO REGISTRADO";
            decimal liquido = _caixasPesadas.Sum(caixa => caixa.PesoLiquidoKg);
            packagesCounterLabel.Text = FormatarKg(liquido);
            packagesValueLabel.Text = FormatarKg(liquido);
            packagesTotalLabel.Text = temOp ? $"de {FormatarKg(_ordemAtual!.QuantidadePendente)} KG" : "de 0";
        }
        else
        {
            packagesTitleLabel.Text = "PRODUTOS REGISTRADOS";
            int produtos = _caixasPesadas.Sum(caixa => caixa.QuantidadeProdutos);
            packagesCounterLabel.Text = produtos.ToString("000", CultureInfo.InvariantCulture);
            packagesValueLabel.Text = produtos.ToString("000", CultureInfo.InvariantCulture);
            packagesTotalLabel.Text = temOp
                ? $"de {_ordemAtual!.QuantidadePendente.ToString("0", CultureInfo.InvariantCulture)}"
                : "de 0";
        }

        productionFooterLabel.Text = $"{qtdCaixas} caixa(s) registrada(s). POST SAP desativado.";
    }

    private bool LimparOp()
    {
        if (!PodeTrocarOuLimparOp())
        {
            return false;
        }

        _ordemAtual = null;
        _ultimaOpConsultada = string.Empty; // Tarefa 21.6.3 (Ajuste 1): limpar libera nova consulta no Leave
        _normaEmbalagem = null;
        _taraCaixaSelecionada = null;
        _caixasPesadas.Clear();
        _paletesMontados.Clear();
        materialDataGridView.Rows.Clear();
        productionDataGridView.Rows.Clear();
        AtualizarGridPaletes();
        stepLabel.Text = "-";
        finishedProductCodeTextBox.Clear();
        finishedProductTextBox.Clear();
        lotTextBox.Clear();
        ovenExitTextBox.Clear();
        classificationDateTextBox.Clear();
        manufacturingDateTextBox.Clear();
        expirationDateTextBox.Clear();
        readForecastBoxesTextBox.Clear();
        readForecastPackagesTextBox.Clear();
        AtualizarCampoQuantidadePorCaixa();
        AtualizarCamposPaletizacaoPadrao();
        AtualizarEstadoLeitura(false);
        AtualizarResumoOperacional();
        return true;
    }

    private async Task<bool> BloquearAcaoSemPermissaoAsync(string acao, string descricaoAcao)
    {
        if (AutorizacaoServico.PossuiPermissao(AutorizacaoServico.ModuloProcesso, PermissoesSistema.Rotinas.LeituraProducao, acao))
        {
            return false;
        }

        await AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            AutorizacaoServico.ModuloProcesso,
            PermissoesSistema.Rotinas.LeituraProducao,
            acao,
            descricaoAcao,
            "ProcessoProdutoAcabadoForm");

        string mensagem = "Usuário sem permissão para executar produto acabado.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private void ProcessoProdutoAcabadoForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!PodeFecharTela())
        {
            e.Cancel = true;
            return;
        }

        _footerClockTimer?.Dispose();
    }

    private bool PodeFecharTela()
    {
        // §5: caixa ativa (só em memória nesta fase) NÃO pode ser descartada ao fechar/Escape.
        if (PrimeiraEntregaHu && ExisteCaixaAtiva())
        {
            AvisarCaixaAtivaPendente();
            return false;
        }

        if (!_leituraIniciada)
        {
            return true;
        }

        statusLabel.Text = "Finalize a leitura antes de sair da tela.";
        MessageBox.Show(
            "Finalize a leitura antes de sair da tela.",
            "Produto Acabado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
    }

    /// <summary>
    /// §5: pode trocar ou limpar a OP? Enquanto houver caixa ativa (não persistida, só em memória) a troca
    /// é bloqueada para não descartar a caixa silenciosamente. Somente confirmar/cancelar libera.
    /// </summary>
    private bool PodeTrocarOuLimparOp()
    {
        if (PrimeiraEntregaHu && ExisteCaixaAtiva())
        {
            AvisarCaixaAtivaPendente();
            return false;
        }

        return true;
    }

    /// <summary>§5: aviso central de caixa ativa pendente (usado em pesagem, troca de OP e fechamento).</summary>
    private void AvisarCaixaAtivaPendente()
    {
        const string mensagem =
            "Existe uma caixa finalizada aguardando integração. Confirme ou cancele a caixa antes de trocar a OP ou fechar a tela.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private bool SolicitarPesoManual(decimal taraKg, out decimal pesoKg)
    {
        pesoKg = 0m;
        using Form prompt = new()
        {
            Text = "Peso manual - Produto Acabado",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 160),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label label = new()
        {
            Text = $"Informe o peso bruto da caixa em KG.\r\nTara aplicada: {FormatarKg(taraKg)}.",
            Dock = DockStyle.Top,
            Height = 72,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56)
        };

        TextBox pesoTextBox = new()
        {
            Location = new Point(80, 78),
            Size = new Size(200, 31),
            TextAlign = HorizontalAlignment.Center,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold)
        };

        Button confirmarButton = CriarBotaoDialogo("Confirmar", Color.FromArgb(34, 166, 82), DialogResult.OK);
        Button cancelarButton = CriarBotaoDialogo("Cancelar", Color.FromArgb(82, 87, 96), DialogResult.Cancel);
        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 6, 28, 6)
        };
        buttons.Controls.Add(confirmarButton);
        buttons.Controls.Add(cancelarButton);
        prompt.Controls.Add(label);
        prompt.Controls.Add(pesoTextBox);
        prompt.Controls.Add(buttons);
        prompt.AcceptButton = confirmarButton;
        prompt.CancelButton = cancelarButton;
        prompt.ActiveControl = pesoTextBox;

        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return false;
        }

        return TryParsePesoKg(pesoTextBox.Text, out pesoKg) && pesoKg > 0m;
    }

    private static Button CriarBotaoDialogo(string texto, Color cor, DialogResult dialogResult)
    {
        Button button = new()
        {
            Text = texto,
            DialogResult = dialogResult,
            BackColor = cor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Cascadia Code", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Size = new Size(108, 32),
            Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static string FormatarKg(decimal valor)
        => $"{valor.ToString("0.000", CultureInfo.GetCultureInfo("pt-BR"))} KG";

    private static bool TryParsePesoKg(string texto, out decimal peso)
    {
        string normalizado = (texto ?? string.Empty)
            .Trim()
            .Replace("KG", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("kg", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(',', '.');
        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out peso);
    }

    private void ReadForecastBoxesTextBox_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            return;
        }

        if (!char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    private async void ProductionOrderTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.SuppressKeyPress = true;
        await ConsultarOpAsync();
    }

    private async void ProcessoProdutoAcabadoForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // §4: bloqueio preventivo de F5/F9/F12 com caixa ativa pendente (Delete=cancelar e Escape seguem).
        if (PrimeiraEntregaHu && ExisteCaixaAtiva()
            && e.KeyCode is Keys.F5 or Keys.F9 or Keys.F12 && !_leituraIniciada)
        {
            e.SuppressKeyPress = true;
            AvisarCaixaAtivaPendente();
            return;
        }

        if (e.KeyCode == Keys.F5)
        {
            e.SuppressKeyPress = true;
            ToggleProductionFromSideButton_Click(iniciarLeituraButton, EventArgs.Empty);
        }
        else if (e.KeyCode == Keys.F12)
        {
            e.SuppressKeyPress = true;
            await RegistrarPesoBalancaAsync();
        }
        else if (e.KeyCode == Keys.F9)
        {
            e.SuppressKeyPress = true;
            await RegistrarPesoManualAsync();
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.SuppressKeyPress = true;
            CancelarUltimaCaixa();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            if (PodeFecharTela())
            {
                Close();
            }
        }
    }

    /// <summary>Tarefa 21.6.4 (Ajuste 3): divisores do card DADOS DA OP alinhados às bordas das 4 colunas.</summary>
    private void AlignDateCardLayout(object? sender, EventArgs e)
    {
        if (tableLayoutPanel6 is null)
        {
            return;
        }

        int left = tableLayoutPanel6.Left;
        int top = tableLayoutPanel6.Top + 6;
        int height = Math.Max(20, tableLayoutPanel6.Height - 12);
        int colWidth = tableLayoutPanel6.Width / 4;
        PosicionarDivisor(dateDividerLabel1, left + colWidth, top, height);
        PosicionarDivisor(dateDividerLabel2, left + colWidth * 2, top, height);
        PosicionarDivisor(dateDividerLabel3, left + colWidth * 3, top, height);
    }

    /// <summary>Tarefa 21.6.4 (Ajuste 4): divisores do card PRODUÇÃO PLANEJADA alinhados às 4 colunas.</summary>
    private void AlignPlannedProductionCardLayout(object? sender, EventArgs e)
    {
        if (tableLayoutPanel8 is null)
        {
            return;
        }

        int left = tableLayoutPanel8.Left;
        int top = tableLayoutPanel8.Top + 6;
        int height = Math.Max(20, tableLayoutPanel8.Height - 12);
        int colWidth = tableLayoutPanel8.Width / 4;
        PosicionarDivisor(plannedProductionDividerLabel1, left + colWidth, top, height);
        PosicionarDivisor(plannedProductionDividerLabel2, left + colWidth * 2, top, height);
        PosicionarDivisor(_plannedDivider3, left + colWidth * 3, top, height);
    }

    private static void PosicionarDivisor(Label? divisor, int x, int y, int height)
    {
        if (divisor is null)
        {
            return;
        }

        divisor.Location = new Point(x, y);
        divisor.Size = new Size(1, height);
        divisor.BringToFront();
    }

    /// <summary>
    /// Tarefa 21.6.4 (Ajuste 2): dois textos são "o mesmo código" se, normalizados (trim/upper/sem espaço
    /// e sem zeros à esquerda), forem iguais — usado para não exibir a descrição igual ao código.
    /// </summary>
    private static bool TextosEquivalentesComoCodigo(string? a, string? b)
    {
        static string Normalizar(string? valor)
        {
            string texto = (valor ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            return texto.TrimStart('0');
        }

        return !string.IsNullOrWhiteSpace(a)
            && !string.IsNullOrWhiteSpace(b)
            && string.Equals(Normalizar(a), Normalizar(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatarStatusOrdem(string status)
    {
        string s = status.Trim();
        return s switch
        {
            "LIBERADA" => "Liberada",
            "NAO_LIBERADA" => "Não liberada",
            _ => s.Length > 1 ? char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant() : s
        };
    }
}


