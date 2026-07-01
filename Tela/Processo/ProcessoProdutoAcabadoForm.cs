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
        AtualizarEstadoLeitura(false);
        AtualizarResumoOperacional();
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
        stepCaptionLabel.Text = "Consulta de OP";
        stepDescriptionLabel.Text = "OP selecionada";
        finishedProductCaptionLabel.Text = "Produto acabado";
        lotCaptionLabel.Text = "Lote";
        ovenExitCaptionLabel.Text = "Depósito destino";
        classificationDateCaptionLabel.Text = "Saldo pendente";
        manufacturingDateCaptionLabel.Text = "Quantidade planejada";
        expirationDateCaptionLabel.Text = "Quantidade entregue";
        materialTitleLabel.Text = "Norma de Embalagem";
        productionReadingsTitleLabel.Text = "Caixas Pesadas";
        productionActionsButton.Text = "CRIAR PALETE";
        productionActionsButton.Visible = false;
        boxesCaptionLabel.Text = "Caixas";
        packagesCaptionLabel.Text = "Peso líquido";
        readWeightLegendTextLabel.Text = "F12 - Ler peso balança";
        manualLotLegendTextLabel.Text = "F9 - Digitar peso";
        deleteLastLegendTextLabel.Text = "Del - Cancelar última caixa";
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
        balanceTextBox.Text = MensagemBalancaProdutoAcabadoNaoConfigurada;
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
        productionOrderSearchLabel.Click += async (_, _) => await ConsultarOpAsync();
        readForecastBoxesTextBox.KeyPress += ReadForecastBoxesTextBox_KeyPress;
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
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        closeWindowLabel.Click += (_, _) =>
        {
            if (PodeFecharTela())
            {
                Close();
            }
        };
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
        materialStatusColumn.HeaderText = "Tipo";
        materialCodeColumn.HeaderText = "Material";
        materialDescriptionColumn.HeaderText = "Item";
        materialLotColumn.HeaderText = "Qtd";
        materialExpirationColumn.HeaderText = "Un.";
        materialBalanceColumn.HeaderText = "Norma";
    }

    private void ConfigurarGridCaixas()
    {
        productionDataGridView.AutoGenerateColumns = false;
        productionDataGridView.ReadOnly = true;
        productionDataGridView.MultiSelect = false;
        productionCodeColumn.HeaderText = "Caixa";
        productionDateColumn.HeaderText = "Bruto";
        productionProductColumn.HeaderText = "Tara/Líquido";
        productionQuantityColumn.HeaderText = "Qtd";
        productionWeightColumn.HeaderText = "Origem/Status";
        productionPrintColumn.HeaderText = "HU";
    }


    private void ConfigurarPaletizacaoOperacional()
    {
        Label primeiraCaixaLabel = CriarLabelPaletizacao("Primeira caixa");
        primeiraCaixaLabel.Location = new Point(205, 14);
        productionReadingsPanel.Controls.Add(primeiraCaixaLabel);

        primeiraCaixaTextBox = CriarTextBoxPaletizacao("primeiraCaixaTextBox");
        primeiraCaixaTextBox.Location = new Point(205, 31);
        productionReadingsPanel.Controls.Add(primeiraCaixaTextBox);

        Label ultimaCaixaLabel = CriarLabelPaletizacao("Última caixa");
        ultimaCaixaLabel.Location = new Point(300, 14);
        productionReadingsPanel.Controls.Add(ultimaCaixaLabel);

        ultimaCaixaTextBox = CriarTextBoxPaletizacao("ultimaCaixaTextBox");
        ultimaCaixaTextBox.Location = new Point(300, 31);
        productionReadingsPanel.Controls.Add(ultimaCaixaTextBox);

        Label materialEmbalagemLabel = CriarLabelPaletizacao("Material embalagem");
        materialEmbalagemLabel.Location = new Point(395, 14);
        productionReadingsPanel.Controls.Add(materialEmbalagemLabel);

        materialEmbalagemPaleteTextBox = CriarTextBoxPaletizacao("materialEmbalagemPaleteTextBox");
        materialEmbalagemPaleteTextBox.Location = new Point(395, 31);
        materialEmbalagemPaleteTextBox.Size = new Size(150, 20);
        materialEmbalagemPaleteTextBox.Text = "PALLET01";
        productionReadingsPanel.Controls.Add(materialEmbalagemPaleteTextBox);

        productionDataGridView.Location = new Point(17, 60);
        productionDataGridView.Size = new Size(1081, 112);
        productionDataGridView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        paletesDataGridView = new DataGridView
        {
            Name = "paletesDataGridView",
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeight = 20,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(226, 231, 238),
            Location = new Point(17, 178),
            MultiSelect = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Size = new Size(1081, 43),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        paletesDataGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
        paletesDataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        paletesDataGridView.ColumnHeadersDefaultCellStyle.Font = new Font("Cascadia Code", 6.5F, FontStyle.Bold);
        paletesDataGridView.DefaultCellStyle.Font = new Font("Cascadia Code", 6.5F);
        paletesDataGridView.Columns.Add("paleteLocalColumn", "Palete local");
        paletesDataGridView.Columns.Add("paletePrimeiraCaixaColumn", "Primeira caixa");
        paletesDataGridView.Columns.Add("paleteUltimaCaixaColumn", "Última caixa");
        paletesDataGridView.Columns.Add("paleteQtdCaixasColumn", "Qtd caixas");
        paletesDataGridView.Columns.Add("paletePesoBrutoColumn", "Peso bruto");
        paletesDataGridView.Columns.Add("paletePesoLiquidoColumn", "Peso líquido");
        paletesDataGridView.Columns.Add("paleteTaraColumn", "Tara");
        paletesDataGridView.Columns.Add("paleteMaterialColumn", "Material embalagem");
        paletesDataGridView.Columns.Add("paleteStatusColumn", "Status");
        productionReadingsPanel.Controls.Add(paletesDataGridView);
    }

    private static Label CriarLabelPaletizacao(string texto)
        => new()
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = new Font("Cascadia Code", 6.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(75, 85, 99),
            Size = new Size(150, 14),
            Text = texto,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private static TextBox CriarTextBoxPaletizacao(string nome)
        => new()
        {
            Name = nome,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Cascadia Code", 7F, FontStyle.Bold),
            ForeColor = Color.FromArgb(17, 24, 39),
            Size = new Size(82, 20),
            MaxLength = 30
        };

    private async Task ConsultarOpAsync(bool exibirAvisoOpObrigatoria = true)
    {
        if (_operacaoEmAndamento)
        {
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

        stepLabel.Text = _ordemAtual.NumeroOrdem;
        finishedProductCodeTextBox.Text = _ordemAtual.MaterialProduzido;
        finishedProductTextBox.Text = _ordemAtual.DescricaoMaterial;
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
        if (_normaEmbalagem is null)
        {
            AtualizarCampoQuantidadePorCaixa();
            return;
        }

        if (_normaEmbalagem.Itens.Count == 0)
        {
            materialDataGridView.Rows.Add("P", _normaEmbalagem.Material, "0001", _normaEmbalagem.QuantidadeProdutosPorCaixa, _normaEmbalagem.Unidade, _normaEmbalagem.PackagingInstruction);
            AtualizarCampoQuantidadePorCaixa();
            return;
        }

        foreach (ProdutoAcabadoNormaItem item in _normaEmbalagem.Itens)
        {
            materialDataGridView.Rows.Add(item.TipoMaterial, item.Material, item.Item, item.Quantidade, item.Unidade, _normaEmbalagem.PackagingInstruction);
        }

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

        ProdutoAcabadoCaixa caixa;
        try
        {
            caixa = _controller.MontarCaixa(
                _ordemAtual,
                _normaEmbalagem,
                _caixasPesadas.Count + 1,
                pesoBrutoKg,
                taraKg,
                origem);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Produto Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        _caixasPesadas.Add(caixa);
        ResultadoPreviewProdutoAcabado101 preview =
            _controller.GerarPreviewMaterialDocument101(_ordemAtual, caixa, DateTime.UtcNow);
        System.Diagnostics.Trace.TraceInformation("[ProdutoAcabado] Preview Material Document 101 caixa {0}: {1}", caixa.NumeroCaixa, preview.PayloadJson);
        AtualizarGridCaixas();
        AtualizarCamposPaletizacaoPadrao();
        AtualizarResumoOperacional();
        statusLabel.Text = $"Caixa {caixa.NumeroCaixa:0000} registrada. Bruto: {FormatarKg(pesoBrutoKg)} | Tara: {FormatarKg(taraKg)} | Líquido: {FormatarKg(pesoLiquidoKg)}.";
        return true;
    }

    private bool ValidarPodePesar()
    {
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
            productionDataGridView.Rows.Add(
                caixa.NumeroCaixa.ToString("0000", CultureInfo.InvariantCulture),
                FormatarKg(caixa.PesoBrutoKg),
                $"Tara {FormatarKg(caixa.TaraKg)} / Liq {FormatarKg(caixa.PesoLiquidoKg)}",
                caixa.QuantidadeProdutos.ToString(CultureInfo.InvariantCulture),
                ObterStatusCaixa(caixa),
                new Bitmap(1, 1));
        }
    }

    private void CancelarUltimaCaixa()
    {
        if (_caixasPesadas.Count == 0)
        {
            return;
        }

        _caixasPesadas.RemoveAt(_caixasPesadas.Count - 1);
        AtualizarGridCaixas();
        AtualizarCamposPaletizacaoPadrao();
        AtualizarResumoOperacional();
        statusLabel.Text = "Última caixa de produto acabado cancelada.";
    }

    private static string ObterStatusCaixa(ProdutoAcabadoCaixa caixa)
        => string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal)
            ? $"{caixa.OrigemPesagem} / {caixa.StatusSap}"
            : $"{caixa.OrigemPesagem} / PALETE {caixa.CodigoPaleteLocal}";

    private void CriarPaleteLocal()
    {
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
        bool livre = !_operacaoEmAndamento;
        bool podeAlternarLeitura = livre && _ordemAtual is not null;
        iniciarLeituraButton.PrimaryText = _leituraIniciada ? "PARAR LEITURA" : "INICIAR LEITURA";
        iniciarLeituraButton.IconGlyph = _leituraIniciada ? "\uE71A" : "\uE768";
        iniciarLeituraButton.BaseBackColor = _leituraIniciada
            ? Color.FromArgb(212, 37, 49)
            : podeAlternarLeitura ? ReadingStatusActiveColor : ActionDisabledColor;
        iniciarLeituraButton.Enabled = podeAlternarLeitura;
        iniciarLeituraButton.Cursor = podeAlternarLeitura ? Cursors.Hand : Cursors.Default;
        lerEtiquetaButton.Visible = _leituraIniciada;
        leituraManualButton.Visible = _leituraIniciada;
        lerEtiquetaButton.Enabled = livre && _leituraIniciada && _ordemAtual is not null;
        leituraManualButton.Enabled = livre && _leituraIniciada && _ordemAtual is not null;
        productionActionsButton.Visible = !_leituraIniciada && _caixasPesadas.Count > 0;
        productionActionsButton.Enabled = productionActionsButton.Visible && livre;
    }

    private void AtualizarResumoOperacional()
    {
        boxesCounterLabel.Text = _caixasPesadas.Count.ToString("000", CultureInfo.InvariantCulture);
        boxesValueLabel.Text = _caixasPesadas.Count.ToString("000", CultureInfo.InvariantCulture);
        decimal liquido = _caixasPesadas.Sum(caixa => caixa.PesoLiquidoKg);
        packagesCounterLabel.Text = FormatarKg(liquido);
        packagesValueLabel.Text = FormatarKg(liquido);
        productionFooterLabel.Text = $"{_caixasPesadas.Count} caixa(s) registrada(s). POST SAP desativado.";
    }

    private void LimparOp()
    {
        _ordemAtual = null;
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

    private void AlignDateCardLayout(object? sender, EventArgs e)
    {
    }

    private void AlignPlannedProductionCardLayout(object? sender, EventArgs e)
    {
    }
}


