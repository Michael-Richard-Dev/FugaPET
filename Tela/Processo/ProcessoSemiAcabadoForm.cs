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

public partial class ProcessoSemiAcabadoForm : Form
{
    internal const string EndpointConsultaOpSemiAcabado = "GET /sap/opu/odata/sap/API_PRODUCTION_ORDER_2_SRV/A_ProductionOrder_2('<OP>')?$format=json&$expand=to_ProductionOrderItem,to_ProductionOrderOperation,to_ProductionOrderStatus&sap-client=110";
    internal const string MensagemBalancaSemiAcabadoNaoConfigurada = "BalanÃ§a de produto semi-acabado nÃ£o configurada para esta operaÃ§Ã£o.";

    private readonly SemiAcabadoController _controller;
    private readonly BalancaLeituraServico _balancaLeituraServico = new();
    private readonly ImpressaoSemiAcabadoServico _impressaoSemiAcabadoServico = new();
    private readonly Dictionary<string, List<PesagemSemiAcabado>> _pesagensPorItemOrdem = [];
    private readonly Dictionary<string, TaraCadastro> _tarasPorItemOrdem = [];
    private IReadOnlyList<SemiAcabadoOrdem> _itensOrdem = [];
    private SemiAcabadoOrdem? _ordemSelecionada;
    private ContextoTerminalLocal? _contextoTerminal;
    private long? _idSetorSelecionado;
    private bool _leituraIniciada;
    private bool _operacaoEmAndamento;

    private readonly Dictionary<string, bool> _confirmadoNaSessaoPorItem = [];
    // CÃ³digo do lanÃ§amento persistido reutilizado no reenvio (nÃ£o insere novo cabeÃ§alho/pesagens) e no histÃ³rico.
    private long? _codigoLancamentoPersistido;
    private bool _modoReenvioLancamentoPersistido;
    private string _statusLancamentoPersistidoAtual = string.Empty;
    // ProteÃ§Ã£o entre reinicializaÃ§Ãµes: lanÃ§amento aberto em ENVIANDO_SAP/DIVERGENCIA_SAP bloqueia nova pesagem/envio.
    private bool _bloqueioLancamentoAberto;
    private int _versaoSelecaoItem;
    private bool _restaurandoSelecaoItem;
    private string? _chaveItemEmLeitura;
    private readonly Dictionary<string, long?> _codigoLancamentoPersistidoPorItem = [];
    private readonly Dictionary<string, bool> _modoReenvioPorItem = [];
    private readonly Dictionary<string, bool> _bloqueioLancamentoPorItem = [];
    private readonly Dictionary<string, SemiAcabadoOrdem> _ordemPersistidaPorItem = [];
    private System.Windows.Forms.Timer? _footerClockTimer;

    private bool ItemAtualConfirmadoNaSessao
        => _ordemSelecionada is not null
            && _confirmadoNaSessaoPorItem.TryGetValue(ChaveItemOrdem(_ordemSelecionada), out bool confirmado)
            && confirmado;

    // Tarefa 20.6 (Ajuste 2): mesmas cores do status de leitura da Entrada.
    private static readonly Color ReadingStatusActiveColor = Color.FromArgb(34, 166, 82);
    private static readonly Color ReadingStatusInactiveColor = Color.FromArgb(220, 53, 69);

    internal sealed class ContextoPesagemSemiAcabadoGrid
    {
        public required PesagemSemiAcabado Pesagem { get; init; }
        public SemiAcabadoOrdem? Ordem { get; init; }
        public string NumeroOrdem { get; init; } = string.Empty;
        public string ItemOrdem { get; init; } = string.Empty;
        public string MaterialProduzido { get; init; } = string.Empty;
        public string Lote { get; init; } = string.Empty;
        public string DescricaoMaterial { get; init; } = string.Empty;
        public long? CodigoLancamento { get; init; }
        public string StatusLancamento { get; init; } = string.Empty;
        public string? MaterialDocument { get; init; }
        public string? MaterialDocumentYear { get; init; }
        public bool Historica { get; init; }
    }

    public ProcessoSemiAcabadoForm()
        : this(new SemiAcabadoController())
    {
    }

    internal ProcessoSemiAcabadoForm(SemiAcabadoController controller)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));

        InitializeComponent();
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this); // Tarefa 20.6 (Ajuste 3): icone padrao
        AplicarModoProdutoSemiAcabado();
        ConfigurarCampoOrdemProducaoSemiAcabado();
        ConfigurarRodape();
        ConfigurarEventos();
        ConfigurarGridSemiAcabado();
        ConfigurarGridPesagens();
        AtualizarEstadoLeitura(false);
        AtualizarResumoPesagem();
        KeyPreview = true;
    }

    private async void ProcessoSemiAcabadoForm_Shown(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar, "executar produto semi-acabado"))
        {
            Close();
            return;
        }

        pedidoComboBox.Focus();
    }

    private void AplicarModoProdutoSemiAcabado()
    {
        Text = "Produto Semi-Acabado";
        headerTitleLabel.Text = "Produto Semi-Acabado";
        headerSubtitleLabel.Text = "Pesagem e entrada de produto semi-acabado por ordem de produÃ§Ã£o";
        productionOrderCaptionLabel.Text = "OP";
        pedidoComboBox.AccessibleName = "Ordem de ProduÃ§Ã£o";
        stepCaptionLabel.Text = "Consulta de OP";
        stepDescriptionLabel.Text = "OP selecionada";
        finishedProductCaptionLabel.Text = "Semi-acabado";
        // Tarefa 20.5 (Ajuste 2): igual ao Consumo â€” sÃ³ o codigo do material no card, sem descricao duplicada.
        finishedProductTextBox.Visible = false;
        lotCaptionLabel.Text = "Lote";
        ovenExitCaptionLabel.Text = "DepÃ³sito destino";
        classificationDateCaptionLabel.Text = "Saldo pendente";
        manufacturingDateCaptionLabel.Text = "Quantidade planejada";
        expirationDateCaptionLabel.Text = "Quantidade entregue";
        materialTitleLabel.Text = "Pesagens do Semi-Acabado";
        productionReadingsTitleLabel.Text = "Itens Produzidos da OP";
        productionActionsButton.Text = "CONFIRMAR SEMI-ACABADO";
        productionActionsButton.Visible = false;
        boxesCaptionLabel.Text = "Saldo OP";
        packagesCaptionLabel.Text = "Pesagens";
        readWeightLegendTextLabel.Text = "F12 - Ler peso balanÃ§a";
        manualLotLegendTextLabel.Text = "F9 - Digitar peso";
        lerEtiquetaButton.PrimaryText = "LER PESO";
        lerEtiquetaButton.KeyHint = "F12";
        leituraManualButton.PrimaryText = "DIGITAR PESO";
        leituraManualButton.KeyHint = "F9";
        deleteLastLegendTextLabel.Text = "Del - Cancelar Ãºltima pesagem";
        deleteByCodeLegendTextLabel.Text = "Esc - Fechar";
        statusValueLabel.Text = "AGUARDANDO OP";
        statusHintLabel.Text = "Informe uma OP para iniciar.";
        sapStatusLabel.Text = _controller.SapSimulado ? "SAP OP: DEMONSTRAÃ‡ÃƒO" : "SAP OP: CONSULTA";
        statusLabel.Text = "Informe uma OP para consulta.";
        CarregarContextoTerminal();
        balanceTextBox.Text = MensagemBalancaSemiAcabadoNaoConfigurada;
        cellUserText.Text = UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = RodapeBancoHelper.ObterTextoBancoDados();
        string nomeTerminal = string.IsNullOrWhiteSpace(_contextoTerminal?.NomeTerminal)
            ? Environment.MachineName
            : _contextoTerminal!.NomeTerminal;
        cellTerminalText.Text = $"Terminal:  {nomeTerminal}";
    }

    /// <summary>
    /// Tarefa 20.5 (Ajuste 1): mesmo padrao do Consumo â€” esconde o painel do icone (marca rosa atras do
    /// campo de OP) e estica o ComboBox de OP para ocupar toda a largura do cartao.
    /// </summary>
    private void ConfigurarCampoOrdemProducaoSemiAcabado()
    {
        productionOrderIconPanel.Visible = false;

        pedidoComboBox.DropDownStyle = ComboBoxStyle.DropDown;
        pedidoComboBox.AutoCompleteMode = AutoCompleteMode.None;
        pedidoComboBox.AutoCompleteSource = AutoCompleteSource.None;
        pedidoComboBox.FlatStyle = FlatStyle.Flat;

        productionOrderShadowPanel.Resize += (_, _) => AjustarLarguraCampoOrdemProducaoSemiAcabado();
        AjustarLarguraCampoOrdemProducaoSemiAcabado();
    }

    private void AjustarLarguraCampoOrdemProducaoSemiAcabado()
    {
        const int margemDireita = 16;
        int larguraDisponivel = productionOrderShadowPanel.ClientSize.Width - pedidoComboBox.Left - margemDireita;
        pedidoComboBox.Width = Math.Max(120, larguraDisponivel);
        pedidoComboBox.DropDownWidth = Math.Max(220, pedidoComboBox.Width);
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
        Shown += ProcessoSemiAcabadoForm_Shown;
        FormClosing += ProcessoSemiAcabadoForm_FormClosing;
        KeyDown += ProcessoSemiAcabadoForm_KeyDown;
        pedidoComboBox.KeyDown += PedidoComboBox_KeyDown;
        pedidoComboBox.Validated += async (_, _) => await ConsultarOpSelecionadaAsync();
        // Tarefa 20.5: o icone de busca (marca rosa) fica oculto; a consulta ocorre por Enter/Validated.
        // NÃ£o assinar o Click do productionOrderSearchLabel (evita NRE caso o controle seja removido do Designer).
        iniciarLeituraButton.Click += IniciarLeitura_Click;
        startActionPanel.Click += IniciarLeitura_Click;
        lerEtiquetaButton.Click += ReadWeightLegend_Click;
        readWeightLegendPanel.Click += ReadWeightLegend_Click;
        readWeightLegendIconLabel.Click += ReadWeightLegend_Click;
        readWeightLegendTextLabel.Click += ReadWeightLegend_Click;
        leituraManualButton.Click += LeituraManual_Click;
        manualLotLegendPanel.Click += LeituraManual_Click;
        manualLotLegendIconLabel.Click += LeituraManual_Click;
        manualLotLegendTextLabel.Click += LeituraManual_Click;
        stopActionPanel.Click += (_, _) => AtualizarEstadoLeitura(false);
        deleteLastLegendPanel.Click += (_, _) => CancelarUltimaPesagem();
        productionActionsButton.Click += async (_, _) => await ConfirmarSemiAcabadoAsync();
        productionDataGridView.SelectionChanged += async (_, _) => await SelecionarItemDaLinhaAtualAsync(false);
        productionDataGridView.CellClick += async (_, _) => await SelecionarItemDaLinhaAtualAsync(false);
        productionDataGridView.CellDoubleClick += ProductionDataGridView_CellDoubleClick;
        materialDataGridView.CellDoubleClick += MaterialDataGridView_CellDoubleClick;
        productionSearchTextBox.TextChanged += (_, _) => AplicarFiltroItens();
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        // Tarefa 20.5 (Ajuste 5): mesmo efeito hover do cabecalho da Entrada.
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

    private void ToggleWindowState()
    {
        // Tarefa 20.5 (Ajuste 5): nao maximiza/restaura durante a leitura (mesma guarda da Entrada).
        if (_leituraIniciada)
        {
            return;
        }

        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void ProcessoSemiAcabadoForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_operacaoEmAndamento)
        {
            e.Cancel = true;
            statusLabel.Text = "Aguarde a conclusÃ£o da consulta ou do envio ao SAP antes de sair.";
            return;
        }

        // Tarefa 20.4 (Ajuste 6): nao permite fechar/voltar com a leitura ativa (evita perder pesagem).
        if (_leituraIniciada)
        {
            e.Cancel = true;
            statusLabel.Text = "Finalize a leitura antes de sair da tela.";
            return;
        }

        if (!ConfirmarDescartePesagensLocaisNaoPersistidas("sair da tela"))
        {
            e.Cancel = true;
            return;
        }

        _footerClockTimer?.Dispose();
    }

    private bool PossuiPesagensLocaisNaoPersistidas()
    {
        foreach ((string chave, List<PesagemSemiAcabado> pesagens) in _pesagensPorItemOrdem)
        {
            if (pesagens.Count == 0)
            {
                continue;
            }

            if (!_codigoLancamentoPersistidoPorItem.TryGetValue(chave, out long? codigoLancamento)
                || codigoLancamento is null or <= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool ConfirmarDescartePesagensLocaisNaoPersistidas(string acao)
    {
        if (!PossuiPesagensLocaisNaoPersistidas())
        {
            return true;
        }

        DialogResult resposta = MessageBox.Show(
            "Existem pesagens ainda nÃ£o confirmadas e etiquetas que jÃ¡ podem ter sido impressas.\n\n"
            + "Ao continuar, essas pesagens serÃ£o descartadas e nÃ£o poderÃ£o ser recuperadas.\n\n"
            + "Descarte fisicamente todas as etiquetas correspondentes.\n\n"
            + "Deseja realmente continuar?",
            $"Confirmar {acao}",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (resposta != DialogResult.Yes)
        {
            statusLabel.Text = "OperaÃ§Ã£o cancelada. Pesagens locais e etiquetas preservadas.";
            return false;
        }

        return true;
    }

    private void RestaurarSelecaoVisualItemAtual()
    {
        if (_ordemSelecionada is null)
        {
            return;
        }

        DataGridViewRow? linhaAtual = LocalizarLinhaPrincipalPorChave(ChaveItemOrdem(_ordemSelecionada));
        if (linhaAtual is null)
        {
            return;
        }

        _restaurandoSelecaoItem = true;
        try
        {
            foreach (DataGridViewRow linha in productionDataGridView.Rows)
            {
                if (!linha.IsNewRow)
                {
                    linha.Selected = false;
                }
            }

            linhaAtual.Selected = true;
            productionDataGridView.CurrentCell = linhaAtual.Cells[0];
        }
        finally
        {
            _restaurandoSelecaoItem = false;
        }
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

    private void ConfigurarGridSemiAcabado()
    {
        productionDataGridView.AutoGenerateColumns = false;
        productionDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        productionDataGridView.MultiSelect = false;
        productionDataGridView.ReadOnly = true;
        // Tarefa 20.6 (Ajuste 1.1): semantica operacional â€” coluna "Peso" recebe o liquido pesado.
        productionCodeColumn.HeaderText = "Material";
        productionProductColumn.HeaderText = "DescriÃ§Ã£o";
        productionQuantityColumn.HeaderText = "Qtd planejada";
        productionWeightColumn.HeaderText = "Saldo pendente";
        productionPesoLidoColumn.HeaderText = "Peso";
        productionItemIdColumn.HeaderText = "Item OP";
        productionPesoOrigemColumn.HeaderText = "Origem";
        productionNumeroItemColumn.HeaderText = "DepÃ³sito destino";
    }

    private void ConfigurarGridPesagens()
    {
        materialDataGridView.AutoGenerateColumns = false;
        materialDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        materialDataGridView.MultiSelect = false;
        materialDataGridView.ReadOnly = true;
        materialStatusColumn.HeaderText = "Seq./Status";
        materialCodeColumn.HeaderText = "Bruto KG";
        materialDescriptionColumn.HeaderText = "Tara KG";
        materialLotColumn.HeaderText = "LÃ­quido KG";
        materialExpirationColumn.HeaderText = "Registrado em";
        materialBalanceColumn.HeaderText = "Origem";
    }

    private async Task ConsultarOpSelecionadaAsync()
    {
        if (_operacaoEmAndamento)
        {
            return;
        }

        string numeroOp = pedidoComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(numeroOp))
        {
            if (ConfirmarDescartePesagensLocaisNaoPersistidas("limpar a OP"))
            {
                LimparOpCarregada();
            }

            return;
        }

        if (!ConfirmarDescartePesagensLocaisNaoPersistidas("consultar outra OP"))
        {
            return;
        }

        try
        {
            _operacaoEmAndamento = true;
            productionDataGridView.Enabled = false;
            AtualizarBotoesOperacao();
            statusLabel.Text = $"Consultando OP {numeroOp} no SAP...";
            ResultadoConsultaSemiAcabado resultado = await _controller.ConsultarOrdemProducaoAsync(numeroOp, CancellationToken.None);
            if (!resultado.Sucesso)
            {
                LimparOpCarregada();
                statusLabel.Text = resultado.Mensagem;
                MessageBox.Show(resultado.Mensagem, "Consulta de OP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LimparContextosOperacionais();
            _itensOrdem = resultado.Itens;
            PreencherItensSemiAcabado(_itensOrdem);
            if (productionDataGridView.Rows.Count > 0)
            {
                productionDataGridView.Rows[0].Selected = true;
                productionDataGridView.CurrentCell = productionDataGridView.Rows[0].Cells[0];
                if (productionDataGridView.Rows[0].Tag is SemiAcabadoOrdem primeiraOrdem)
                {
                    await SelecionarItemAsync(primeiraOrdem, false, true);
                }
            }

        }
        catch (Exception ex)
        {
            LimparOpCarregada();
            System.Diagnostics.Trace.TraceWarning($"[ProcessoSemiAcabadoForm] Falha ao consultar OP: {ex.GetType().Name}");
            MessageBox.Show("NÃ£o foi possÃ­vel consultar a OP. Tente novamente.", "Consulta de OP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            productionDataGridView.Enabled = true;
            _operacaoEmAndamento = false;
            RestaurarSelecaoVisualItemAtual();
            AtualizarBotoesOperacao();
        }
    }

    /// <summary>
    /// ProteÃ§Ã£o entre reinicializaÃ§Ãµes (Â§4): consulta o lanÃ§amento ABERTO do item selecionado e reage por status.
    /// FINALIZADO_LOCAL/ERRO_SAP recuperam o mesmo lanÃ§amento e suas pesagens persistidas (reenvio controlado,
    /// sem novo cabeÃ§alho); ENVIANDO_SAP/DIVERGENCIA_SAP bloqueiam nova pesagem e novo envio. HistÃ³rico persistido
    /// aqui compÃµe o MESMO lanÃ§amento reaberto â€” nunca um lanÃ§amento novo (CONFIRMADO_SAP nÃ£o Ã© aberto).
    /// </summary>
    private async Task AvaliarLancamentoAbertoDoItemSelecionadoAsync()
        => await AvaliarLancamentoAbertoDoItemSelecionadoAsync(null, 0);

    private async Task AvaliarLancamentoAbertoDoItemSelecionadoAsync(string? chaveEsperada, int versaoSelecao)
        => await AvaliarLancamentoAbertoDoItemSelecionadoAsync(chaveEsperada, versaoSelecao, true);

    private async Task AvaliarLancamentoAbertoDoItemSelecionadoAsync(string? chaveEsperada, int versaoSelecao, bool atualizarTela)
    {
        if (_ordemSelecionada is null)
        {
            return;
        }

        string chaveAtual = ChaveItemOrdem(_ordemSelecionada);
        if (!string.IsNullOrWhiteSpace(chaveEsperada)
            && !string.Equals(chaveEsperada, chaveAtual, StringComparison.Ordinal))
        {
            return;
        }

        LancamentoSemiAcabadoPersistido? aberto;
        try
        {
            aberto = await _controller.ObterLancamentoAbertoPorOpItemAsync(
                _ordemSelecionada.NumeroOrdem, _ordemSelecionada.ItemOrdem, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Estrutura ainda nÃ£o aplicada ou falha de leitura nÃ£o pode travar o fluxo local de pesagem.
            System.Diagnostics.Trace.TraceWarning(
                $"[ProcessoSemiAcabadoForm] Falha ao avaliar lanÃ§amento aberto: {ex.GetType().Name}");
            return;
        }

        if (versaoSelecao > 0 && versaoSelecao != _versaoSelecaoItem)
        {
            return;
        }

        if (_ordemSelecionada is null || !string.Equals(ChaveItemOrdem(_ordemSelecionada), chaveAtual, StringComparison.Ordinal))
        {
            return;
        }

        _bloqueioLancamentoAberto = false;
        _modoReenvioLancamentoPersistido = false;
        _codigoLancamentoPersistido = null;
        _bloqueioLancamentoPorItem[chaveAtual] = false;
        _modoReenvioPorItem[chaveAtual] = false;
        _codigoLancamentoPersistidoPorItem[chaveAtual] = null;
        _statusLancamentoPersistidoAtual = string.Empty;

        if (aberto is null)
        {
            return;
        }

        switch (aberto.StatusLancamento)
        {
            case "ENVIANDO_SAP":
                await CarregarLancamentoPersistidoSomenteLeituraAsync(aberto.CodigoSemiAcabadoLancamento, chaveAtual, versaoSelecao);
                _bloqueioLancamentoAberto = true;
                _bloqueioLancamentoPorItem[chaveAtual] = true;
                _modoReenvioLancamentoPersistido = false;
                _modoReenvioPorItem[chaveAtual] = false;
                statusValueLabel.Text = "ENVIANDO SAP";
                statusHintLabel.Text = "Envio em andamento. Aguarde ou acione o suporte para verificar no SAP.";
                statusLabel.Text = "JÃ¡ existe um envio em andamento para esta OP/item. Novo envio bloqueado.";
                break;

            case "DIVERGENCIA_SAP":
                await CarregarLancamentoPersistidoSomenteLeituraAsync(aberto.CodigoSemiAcabadoLancamento, chaveAtual, versaoSelecao);
                _bloqueioLancamentoAberto = true;
                _bloqueioLancamentoPorItem[chaveAtual] = true;
                _modoReenvioLancamentoPersistido = false;
                _modoReenvioPorItem[chaveAtual] = false;
                statusValueLabel.Text = "DIVERGÃŠNCIA SAP";
                statusHintLabel.Text = "Confira o documento no SAP. NÃ£o reenviar sem suporte.";
                statusLabel.Text = "LanÃ§amento com divergÃªncia SAP. Novo lanÃ§amento e novo envio bloqueados.";
                break;

            case "ERRO_SAP":
                await CarregarLancamentoPersistidoParaReenvioAsync(aberto.CodigoSemiAcabadoLancamento, chaveAtual, versaoSelecao);
                _modoReenvioLancamentoPersistido = true;
                _modoReenvioPorItem[chaveAtual] = true;
                statusValueLabel.Text = "ERRO SAP";
                statusHintLabel.Text = "Reenvio controlado do mesmo lanÃ§amento (nÃ£o cria novo).";
                statusLabel.Text = "LanÃ§amento anterior com ERRO_SAP recuperado. Confirme para reenviar o mesmo lanÃ§amento.";
                break;

            case "FINALIZADO_LOCAL":
                await CarregarLancamentoPersistidoParaReenvioAsync(aberto.CodigoSemiAcabadoLancamento, chaveAtual, versaoSelecao);
                _modoReenvioLancamentoPersistido = true;
                _modoReenvioPorItem[chaveAtual] = true;
                statusValueLabel.Text = "PENDENTE ENVIO";
                statusHintLabel.Text = "LanÃ§amento local pendente recuperado (nÃ£o duplica).";
                statusLabel.Text = "LanÃ§amento local pendente recuperado. Confirme para enviar ao SAP.";
                break;
        }

        if (atualizarTela)
        {
            AtualizarGridPesagens();
            AtualizarResumoPesagem();
            AtualizarContadores();
            AtualizarBotoesOperacao();
        }
    }

    /// <summary>
    /// Recupera as pesagens persistidas do MESMO lanÃ§amento aberto (ERRO_SAP/FINALIZADO_LOCAL) para o item
    /// selecionado, preservando cada <see cref="PesagemSemiAcabado.CodigoEtiqueta"/> (reimpressÃ£o idÃªntica).
    /// Estas pesagens compÃµem o total/payload por serem o prÃ³prio lanÃ§amento reenviado â€” nÃ£o Ã© histÃ³rico novo.
    /// </summary>
    private async Task CarregarLancamentoPersistidoParaReenvioAsync(long codigoLancamento, string? chaveEsperada = null, int versaoSelecao = 0)
    {
        if (_ordemSelecionada is null)
        {
            return;
        }

        LancamentoSemiAcabado? completo = await _controller.ObterLancamentoCompletoAsync(codigoLancamento, CancellationToken.None);
        if (completo is null)
        {
            return;
        }

        string chavePersistida = ChaveItemOrdem(completo.Ordem);
        string chaveAtual = chaveEsperada ?? ChaveItemOrdem(_ordemSelecionada);
        if (!string.Equals(chavePersistida, chaveAtual, StringComparison.Ordinal)
            || (versaoSelecao > 0 && !SelecaoContinuaAtual(chaveAtual, versaoSelecao)))
        {
            return;
        }

        List<PesagemSemiAcabado> destino = ObterPesagens(chaveAtual);
        destino.Clear();
        destino.AddRange(completo.Pesagens);
        _ordemPersistidaPorItem[chaveAtual] = completo.Ordem;
        _codigoLancamentoPersistido = codigoLancamento;
        _codigoLancamentoPersistidoPorItem[chaveAtual] = codigoLancamento;
        _statusLancamentoPersistidoAtual = completo.StatusLancamento;
        AtualizarLinhaPrincipalComPesagem(completo.Ordem);
    }

    private async Task CarregarLancamentoPersistidoSomenteLeituraAsync(
        long codigoLancamento,
        string chaveEsperada,
        int versaoSelecao)
    {
        LancamentoSemiAcabado? completo = await _controller.ObterLancamentoCompletoAsync(codigoLancamento, CancellationToken.None);
        if (completo is null || !SelecaoContinuaAtual(chaveEsperada, versaoSelecao))
        {
            return;
        }

        string chaveCompleta = ChaveItemOrdem(completo.Ordem);
        if (!string.Equals(chaveCompleta, chaveEsperada, StringComparison.Ordinal))
        {
            return;
        }

        List<PesagemSemiAcabado> destino = ObterPesagens(chaveEsperada);
        destino.Clear();
        destino.AddRange(completo.Pesagens);
        _ordemPersistidaPorItem[chaveEsperada] = completo.Ordem;
        _codigoLancamentoPersistido = codigoLancamento;
        _codigoLancamentoPersistidoPorItem[chaveEsperada] = codigoLancamento;
        _statusLancamentoPersistidoAtual = completo.StatusLancamento;
        _modoReenvioLancamentoPersistido = false;
        _modoReenvioPorItem[chaveEsperada] = false;
        AtualizarLinhaPrincipalComPesagem(completo.Ordem);
    }

    private void PreencherItensSemiAcabado(IReadOnlyList<SemiAcabadoOrdem> itens)
    {
        productionDataGridView.Rows.Clear();
        foreach (SemiAcabadoOrdem item in itens)
        {
            // Tarefa 20.6 (Ajuste 1.2): "Peso" e "Origem" comecam vazios e sao atualizados apos a pesagem.
            int rowIndex = productionDataGridView.Rows.Add(
                item.MaterialProduzido,
                item.DescricaoMaterial,
                FormatarKg(item.QuantidadePlanejada),
                FormatarKg(item.QuantidadePendente),
                string.Empty,
                item.ItemOrdem,
                string.Empty,
                item.DepositoDestino);
            productionDataGridView.Rows[rowIndex].Tag = item;
        }

        AtualizarContadores();
    }

    private void AplicarFiltroItens()
    {
        string filtro = productionSearchTextBox.Text.Trim();
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            bool visivel = string.IsNullOrWhiteSpace(filtro)
                || row.Cells.Cast<DataGridViewCell>().Any(cell => Convert.ToString(cell.Value, CultureInfo.CurrentCulture)?.Contains(filtro, StringComparison.OrdinalIgnoreCase) == true);
            row.Visible = visivel;
        }
    }

    private void CapturarItemSelecionado()
    {
        if (productionDataGridView.CurrentRow?.Tag is SemiAcabadoOrdem ordem)
        {
            AplicarItemSelecionado(ordem);
        }
    }

    private async Task SelecionarItemDaLinhaAtualAsync(bool carregarHistorico)
    {
        if (_operacaoEmAndamento)
        {
            return;
        }

        if (productionDataGridView.CurrentRow?.Tag is SemiAcabadoOrdem ordem)
        {
            await SelecionarItemAsync(ordem, carregarHistorico);
        }
    }

    private async Task SelecionarItemAsync(SemiAcabadoOrdem ordem, bool carregarHistorico, bool selecaoInterna = false)
    {
        if ((_operacaoEmAndamento && !selecaoInterna) || _restaurandoSelecaoItem)
        {
            return;
        }

        string chaveNova = ChaveItemOrdem(ordem);
        if (!ValidarTrocaItemDuranteLeitura(chaveNova))
        {
            return;
        }

        int versao = ++_versaoSelecaoItem;
        AplicarItemSelecionado(ordem);

        await AvaliarLancamentoAbertoDoItemSelecionadoAsync(chaveNova, versao, false);
        if (!SelecaoContinuaAtual(chaveNova, versao))
        {
            return;
        }

        List<ContextoPesagemSemiAcabadoGrid> linhas = CriarContextosPesagensAtuais(ordem);
        int historicas = 0;
        if (carregarHistorico)
        {
            IReadOnlyList<ContextoPesagemSemiAcabadoGrid> historico = await ObterHistoricoConfirmadoAsync(
                chaveNova,
                versao,
                ordem.NumeroOrdem,
                ordem.ItemOrdem,
                ordem.MaterialProduzido);
            if (!SelecaoContinuaAtual(chaveNova, versao))
            {
                return;
            }

            linhas.AddRange(historico);
            historicas = historico.Count;
        }

        PreencherGridPesagens(linhas);
        AtualizarResumoPesagem();
        AtualizarContadores();
        AtualizarBotoesOperacao();

        if (!_modoReenvioLancamentoPersistido && !_bloqueioLancamentoAberto && !ItemAtualConfirmadoNaSessao)
        {
            if (carregarHistorico)
            {
                statusLabel.Text = historicas == 0
                    ? "Item selecionado. Nenhuma pesagem confirmada anterior encontrada."
                    : $"HistÃ³rico confirmado carregado: {historicas} pesagem(ns). Duplo clique na pesagem para reimprimir.";
            }
            else
            {
                AtualizarStatusItemSemLancamentoAberto();
            }
        }
    }

    private bool ValidarTrocaItemDuranteLeitura(string chaveNova)
    {
        if (!_leituraIniciada
            || string.IsNullOrWhiteSpace(_chaveItemEmLeitura)
            || string.Equals(_chaveItemEmLeitura, chaveNova, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            _restaurandoSelecaoItem = true;
            DataGridViewRow? linhaAnterior = LocalizarLinhaPrincipalPorChave(_chaveItemEmLeitura);
            if (linhaAnterior is not null)
            {
                foreach (DataGridViewRow linha in productionDataGridView.Rows)
                {
                    if (!linha.IsNewRow)
                    {
                        linha.Selected = false;
                    }
                }

                linhaAnterior.Selected = true;
                productionDataGridView.CurrentCell = linhaAnterior.Cells[0];
            }
        }
        finally
        {
            _restaurandoSelecaoItem = false;
        }

        statusLabel.Text = "Pare a leitura antes de selecionar outro item.";
        return false;
    }

    private void AplicarItemSelecionado(SemiAcabadoOrdem ordem)
    {
        _ordemSelecionada = ordem;
        string chave = ChaveItemOrdem(ordem);
        _codigoLancamentoPersistido = _codigoLancamentoPersistidoPorItem.TryGetValue(chave, out long? codigo) ? codigo : null;
        _modoReenvioLancamentoPersistido = _modoReenvioPorItem.TryGetValue(chave, out bool modoReenvio) && modoReenvio;
        _bloqueioLancamentoAberto = _bloqueioLancamentoPorItem.TryGetValue(chave, out bool bloqueio) && bloqueio;
        lotTextBox.Text = ordem.Lote;
        stepLabel.Text = ordem.NumeroOrdem;
        finishedProductCodeTextBox.Text = ordem.MaterialProduzido;
        finishedProductTextBox.Text = string.Empty; // Tarefa 20.5: descricao vive no grid, nao no card
        ovenExitTextBox.Text = ordem.DepositoDestino;
        classificationDateTextBox.Text = FormatarKg(ordem.QuantidadePendente);
        manufacturingDateTextBox.Text = FormatarKg(ordem.QuantidadePlanejada);
        expirationDateTextBox.Text = FormatarKg(ordem.QuantidadeEntregue);
    }

    private void AtualizarStatusItemSemLancamentoAberto()
    {
        if (_ordemSelecionada is null || _modoReenvioLancamentoPersistido || _bloqueioLancamentoAberto || ItemAtualConfirmadoNaSessao)
        {
            return;
        }

        statusLabel.Text = $"OP {_ordemSelecionada.NumeroOrdem} carregada para produto semi-acabado.";
        statusValueLabel.Text = "OP CARREGADA";
        statusHintLabel.Text = "Inicie a leitura e registre as pesagens.";
    }

    private bool SelecaoContinuaAtual(string chaveEsperada, int versaoSelecao)
        => versaoSelecao == _versaoSelecaoItem
            && _ordemSelecionada is not null
            && string.Equals(ChaveItemOrdem(_ordemSelecionada), chaveEsperada, StringComparison.Ordinal);

    private void IniciarLeitura_Click(object? sender, EventArgs e)
    {
        if (BloquearAcaoDuranteOperacao())
        {
            return;
        }

        if (ItemAtualConfirmadoNaSessao)
        {
            MessageBox.Show(
                "Este item jÃ¡ foi confirmado no SAP nesta sessÃ£o. Recarregue a OP para iniciar um novo lanÃ§amento.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_modoReenvioLancamentoPersistido || _bloqueioLancamentoAberto)
        {
            MessageBox.Show(
                "LanÃ§amento persistido recuperado. A leitura fica bloqueada; use Confirmar para reenviar ou reimprima as etiquetas.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (_leituraIniciada)
        {
            AtualizarEstadoLeitura(false);
            _chaveItemEmLeitura = null;
            statusLabel.Text = "Leitura de produto semi-acabado parada.";
            return;
        }

        if (_ordemSelecionada is null)
        {
            MessageBox.Show("Selecione uma OP antes de iniciar a leitura.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _chaveItemEmLeitura = ChaveItemOrdem(_ordemSelecionada);
        AtualizarEstadoLeitura(true);
        statusLabel.Text = "Leitura de produto semi-acabado iniciada. Use F9 para digitar peso.";
    }

    private async void LeituraManual_Click(object? sender, EventArgs e)
    {
        await RegistrarPesoManualAsync();
    }

    private async void ReadWeightLegend_Click(object? sender, EventArgs e)
    {
        await RegistrarPesoBalancaAsync();
    }

    private async void ProductionDataGridView_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_operacaoEmAndamento)
        {
            return;
        }

        if (e.RowIndex < 0)
        {
            return;
        }

        if (productionDataGridView.Rows[e.RowIndex].Tag is SemiAcabadoOrdem ordem)
        {
            await SelecionarItemAsync(ordem, true);
        }
    }

    private async void MaterialDataGridView_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _ordemSelecionada is null)
        {
            return;
        }

        ContextoPesagemSemiAcabadoGrid? contexto = materialDataGridView.Rows[e.RowIndex].Tag as ContextoPesagemSemiAcabadoGrid;
        PesagemSemiAcabado? pesagem = contexto?.Pesagem ?? materialDataGridView.Rows[e.RowIndex].Tag as PesagemSemiAcabado;
        if (pesagem is null)
        {
            return;
        }

        if (!pesagem.Valida)
        {
            MessageBox.Show("Pesagem cancelada nÃ£o pode ser reimpressa.", "ReimpressÃ£o", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // ReimpressÃ£o exige permissÃ£o REIMPRIMIR (nÃ£o burlar pelo duplo clique).
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Reimprimir, "reimprimir etiqueta de semi-acabado"))
        {
            return;
        }

        try
        {
            SemiAcabadoOrdem ordemReimpressao = ObterOrdemParaReimpressao(contexto);
            await _impressaoSemiAcabadoServico.ReimprimirPesagemAsync(
                ordemReimpressao,
                pesagem);
            statusLabel.Text = $"ReimpressÃ£o enviada para a pesagem {pesagem.Sequencia:00}.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[ProcessoSemiAcabadoForm] Falha na reimpressÃ£o semi-acabado: {ex.GetType().Name}");
            MessageBox.Show("NÃ£o foi possÃ­vel reimprimir a etiqueta desta pesagem.", "ReimpressÃ£o", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task<IReadOnlyList<ContextoPesagemSemiAcabadoGrid>> ObterHistoricoConfirmadoAsync(
        string chaveEsperada,
        int versaoSelecao,
        string numeroOrdem,
        string itemOrdem,
        string materialProduzido)
    {
        List<ContextoPesagemSemiAcabadoGrid> historico = [];
        IReadOnlyList<LancamentoSemiAcabadoPersistido> lancamentos;
        try
        {
            lancamentos = await _controller.ListarLancamentosPorOpItemAsync(
                numeroOrdem,
                itemOrdem,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (SelecaoContinuaAtual(chaveEsperada, versaoSelecao))
            {
                System.Diagnostics.Trace.TraceWarning($"[ProcessoSemiAcabadoForm] Falha ao carregar histÃ³rico confirmado: {ex.GetType().Name}");
                statusLabel.Text = "NÃ£o foi possÃ­vel carregar o histÃ³rico confirmado deste item.";
            }

            return historico;
        }

        foreach (LancamentoSemiAcabadoPersistido lancamento in lancamentos
                     .Where(l => string.Equals(l.StatusLancamento, "CONFIRMADO_SAP", StringComparison.Ordinal)))
        {
            if (!SelecaoContinuaAtual(chaveEsperada, versaoSelecao))
            {
                return [];
            }

            LancamentoSemiAcabado? completo = await _controller.ObterLancamentoCompletoAsync(
                lancamento.CodigoSemiAcabadoLancamento,
                CancellationToken.None);
            if (completo is null || !SelecaoContinuaAtual(chaveEsperada, versaoSelecao))
            {
                return [];
            }

            string chavePersistida = ChaveItemOrdem(completo.Ordem);
            if (!string.Equals(chavePersistida, chaveEsperada, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (PesagemSemiAcabado pesagem in completo.Pesagens)
            {
                historico.Add(new ContextoPesagemSemiAcabadoGrid
                {
                    Pesagem = pesagem,
                    Ordem = completo.Ordem,
                    NumeroOrdem = completo.Ordem.NumeroOrdem,
                    ItemOrdem = completo.Ordem.ItemOrdem,
                    MaterialProduzido = completo.Ordem.MaterialProduzido,
                    Lote = completo.Ordem.Lote,
                    DescricaoMaterial = completo.Ordem.DescricaoMaterial,
                    CodigoLancamento = lancamento.CodigoSemiAcabadoLancamento,
                    StatusLancamento = lancamento.StatusLancamento,
                    MaterialDocument = lancamento.MaterialDocument,
                    MaterialDocumentYear = lancamento.MaterialDocumentYear,
                    Historica = true
                });
            }
        }

        return historico;
    }

    private async Task RegistrarPesoBalancaAsync()
    {
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar, "ler peso de produto semi-acabado"))
        {
            return;
        }

        if (!ValidarPodePesar())
        {
            return;
        }

        if (!await GarantirBalancaSemiAcabadoConfiguradaAsync())
        {
            return;
        }

        TaraCadastro? taraSelecionada = await GarantirTaraSemiAcabadoSelecionadaAsync();
        if (taraSelecionada is null)
        {
            return;
        }

        ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
        if (!leitura.Sucesso)
        {
            string mensagem = string.IsNullOrWhiteSpace(leitura.Mensagem)
                ? "NÃ£o foi possÃ­vel ler o peso da balanÃ§a."
                : leitura.Mensagem;
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "BalanÃ§a", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryParsePesoKg(leitura.Peso, out decimal pesoBrutoKg))
        {
            MessageBox.Show("Peso retornado pela balanÃ§a Ã© invÃ¡lido.", "BalanÃ§a", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await RegistrarPesagemSemiAcabadoAsync(pesoBrutoKg, taraSelecionada.PesoKg, "BALANCA", taraSelecionada.CodigoTara);
    }

    private async Task RegistrarPesoManualAsync()
    {
        // TODO PermissÃµes:
        // Quando a matriz de permissÃµes do Produto Semi-Acabado for criada,
        // separar permissÃ£o de peso manual se o negÃ³cio exigir.
        // Tarefa 20.5 (Ajuste 3): sem matriz especÃ­fica, usa a mesma permissÃ£o operacional do processo (Executar).
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar, "informar peso manual de produto semi-acabado"))
        {
            return;
        }

        if (!ValidarPodePesar())
        {
            return;
        }

        TaraCadastro? taraSelecionada = await GarantirTaraSemiAcabadoSelecionadaAsync();
        if (taraSelecionada is null)
        {
            return;
        }

        if (!SolicitarPesoManual(taraSelecionada.PesoKg, out decimal pesoBrutoKg))
        {
            return;
        }

        await RegistrarPesagemSemiAcabadoAsync(pesoBrutoKg, taraSelecionada.PesoKg, "MANUAL", taraSelecionada.CodigoTara);
    }

    private async Task<bool> RegistrarPesagemSemiAcabadoAsync(decimal pesoBrutoKg, decimal taraKg, string origem, long? codigoTara)
    {
        if (_ordemSelecionada is null)
        {
            return false;
        }

        decimal pesoLiquidoKg = pesoBrutoKg - taraKg;
        if (pesoLiquidoKg <= 0m)
        {
            MessageBox.Show("Peso lÃ­quido do semi-acabado deve ser maior que zero.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        string chave = ChaveItemOrdem(_ordemSelecionada);
        decimal totalAtual = PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(ObterPesagens(chave));
        decimal novoTotal = totalAtual + pesoLiquidoKg;
        if (!ValidarSaldoSemiAcabado(novoTotal, _ordemSelecionada.QuantidadePendente, out string mensagemSaldo))
        {
            MessageBox.Show(mensagemSaldo, "Saldo OP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        List<PesagemSemiAcabado> pesagens = ObterPesagens(chave);
        decimal saldoRestanteAposPesagem = Math.Max(_ordemSelecionada.QuantidadePendente - novoTotal, 0m);
        PesagemSemiAcabado pesagem = new()
        {
            Sequencia = pesagens.Count + 1,
            CodigoEtiqueta = ImpressaoSemiAcabadoServico.GerarCodigoEtiqueta(_ordemSelecionada),
            PesoBrutoKg = pesoBrutoKg,
            PesoTaraKg = taraKg,
            PesoLiquidoKg = pesoLiquidoKg,
            SaldoAposPesagemKg = saldoRestanteAposPesagem,
            Origem = origem,
            LeituraOriginal = pesoBrutoKg.ToString("0.###", CultureInfo.InvariantCulture),
            CodigoTara = codigoTara,
            RegistradoEm = DateTime.Now
        };
        pesagens.Add(pesagem);

        System.Diagnostics.Trace.TraceInformation(
            $"Pesagem semi-acabado: bruto={pesoBrutoKg:0.###}, tara={taraKg:0.###}, liquido={pesoLiquidoKg:0.###}, origem={origem}.");
        AtualizarLinhaPrincipalComPesagem(_ordemSelecionada);
        AtualizarGridPesagens();
        AtualizarResumoPesagem();
        AtualizarContadores();
        statusLabel.Text = $"Peso registrado. Bruto: {FormatarKg(pesoBrutoKg)} | Tara: {FormatarKg(taraKg)} | LÃ­quido: {FormatarKg(pesoLiquidoKg)}.";
        try
        {
            await _impressaoSemiAcabadoServico.ImprimirNovaPesagemAsync(
                _ordemSelecionada,
                pesagem);
            statusLabel.Text += " Etiqueta enviada para impressÃ£o.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[ProcessoSemiAcabadoForm] Falha ao imprimir etiqueta semi-acabado: {ex.GetType().Name}");
            MessageBox.Show(
                "Pesagem registrada, mas a etiqueta nÃ£o foi impressa. Use duplo clique na pesagem para reimprimir.",
                "Etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return true;
    }

    private bool ValidarPodePesar()
    {
        if (BloquearAcaoDuranteOperacao())
        {
            return false;
        }

        // Tarefa 20.4 (Ajuste 2): sessao ja confirmada nao aceita nova pesagem ate recarregar a OP.
        if (ItemAtualConfirmadoNaSessao)
        {
            MessageBox.Show(
                "Semi-acabado jÃ¡ confirmado nesta sessÃ£o. Recarregue a OP para iniciar novo lanÃ§amento.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return false;
        }

        if (_bloqueioLancamentoAberto)
        {
            MessageBox.Show(
                "LanÃ§amento aberto para esta OP/item (envio em andamento ou divergÃªncia SAP). "
                + "NÃ£o Ã© possÃ­vel registrar nova pesagem atÃ© verificaÃ§Ã£o no SAP/suporte.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_modoReenvioLancamentoPersistido)
        {
            MessageBox.Show(
                "LanÃ§amento persistido recuperado para reenvio. NÃ£o Ã© possÃ­vel alterar pesagens; confirme para reenviar ou consulte o histÃ³rico.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return false;
        }

        if (!_leituraIniciada)
        {
            MessageBox.Show("Inicie a leitura antes de registrar peso.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_ordemSelecionada is null)
        {
            MessageBox.Show("Selecione uma OP antes de registrar peso.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async Task ConfirmarSemiAcabadoAsync()
    {
        // ProteÃ§Ã£o contra duplo clique: um envio por vez (nÃ£o salvar/claim/documento em duplicidade).
        if (_operacaoEmAndamento)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Finalizar, "confirmar produto semi-acabado"))
        {
            return;
        }

        if (_ordemSelecionada is null)
        {
            MessageBox.Show("Selecione uma OP antes de confirmar o semi-acabado.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_bloqueioLancamentoAberto)
        {
            MessageBox.Show(
                "Existe um lanÃ§amento aberto para esta OP/item (envio em andamento ou divergÃªncia SAP). "
                + "Novo envio bloqueado atÃ© verificaÃ§Ã£o no SAP/suporte.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        SemiAcabadoOrdem ordemConfirmada = _ordemSelecionada;
        string chaveConfirmada = ChaveItemOrdem(ordemConfirmada);
        int versaoConfirmacao = _versaoSelecaoItem;
        List<PesagemSemiAcabado> pesagens = ObterPesagens(chaveConfirmada);
        int validas = PesagemSemiAcabadoCalculos.ContarValidas(pesagens);
        if (validas == 0)
        {
            MessageBox.Show("Registre ao menos uma pesagem vÃ¡lida antes de confirmar o semi-acabado.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        LancamentoSemiAcabado lancamento = _controller.MontarLancamentoLocal(
            ordemConfirmada,
            pesagens,
            EstadoSessaoUsuarioAtual.SessaoAtual?.Login ?? Environment.UserName);
        // Reenvio: reutiliza o MESMO lanÃ§amento jÃ¡ persistido (nÃ£o insere novo cabeÃ§alho nem reinsere pesagens).
        lancamento.CodigoSemiAcabadoLancamento = _codigoLancamentoPersistido;
        decimal pesoLiquidoTotal = lancamento.PesoLiquidoTotalKg;

        // ConfirmaÃ§Ã£o ao operador antes do envio real (movimento 101).
        DialogResult confirmacao = MessageBox.Show(
            $"Confirmar produÃ§Ã£o de semi-acabado e enviar ao SAP (movimento 101)?\n\n"
            + $"OP: {ordemConfirmada.NumeroOrdem}\n"
            + $"Material: {ordemConfirmada.MaterialProduzido}\n"
            + $"Pesagens vÃ¡lidas: {validas}\n"
            + $"Peso lÃ­quido total: {FormatarKg(pesoLiquidoTotal)}\n"
            + $"Movimento: 101",
            "Confirmar Produto Semi-Acabado",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (confirmacao != DialogResult.Yes)
        {
            statusLabel.Text = "ConfirmaÃ§Ã£o cancelada.";
            return;
        }

        _operacaoEmAndamento = true;
        productionDataGridView.Enabled = false;
        AtualizarBotoesOperacao();
        statusLabel.Text = "Enviando lanÃ§amento ao SAP (movimento 101)...";
        try
        {
            ResultadoEnvioSemiAcabadoSap resultado =
                await _controller.SalvarEEnviarMaterialDocument101Async(lancamento, CancellationToken.None);

            // Preserva o cÃ³digo persistido ANTES de analisar sucesso/falha: um ERRO_SAP reenviÃ¡vel deve
            // reutilizar exatamente esta linha no prÃ³ximo clique (nÃ£o duplicar cabeÃ§alho nem pesagens).
            if (resultado.CodigoLancamento is > 0)
            {
                _codigoLancamentoPersistido = resultado.CodigoLancamento;
                _codigoLancamentoPersistidoPorItem[chaveConfirmada] = resultado.CodigoLancamento;
            }

            if (resultado.Sucesso
                && !string.IsNullOrWhiteSpace(resultado.MaterialDocument)
                && !string.IsNullOrWhiteSpace(resultado.MaterialDocumentYear))
            {
                _confirmadoNaSessaoPorItem[chaveConfirmada] = true;
                _leituraIniciada = false;
                statusValueLabel.Text = "CONFIRMADO SAP";
                statusHintLabel.Text = $"Documento {resultado.MaterialDocument}/{resultado.MaterialDocumentYear}. HistÃ³rico preservado para reimpressÃ£o.";
                statusLabel.Text = $"Semi-acabado CONFIRMADO no SAP. Documento {resultado.MaterialDocument}/{resultado.MaterialDocumentYear}.";
                AtualizarEstadoLeitura(false);
                MessageBox.Show(
                    $"ProduÃ§Ã£o confirmada no SAP.\n\nDocumento de material: {resultado.MaterialDocument}/{resultado.MaterialDocumentYear}.",
                    "Produto Semi-Acabado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (resultado.EnvioDuplicadoBloqueado)
            {
                await TratarEnvioDuplicadoBloqueadoAsync(resultado, ordemConfirmada, chaveConfirmada);
                return;
            }

            // Nenhum caminho de falha confirma a sessÃ£o nem apaga pesagens.
            if (resultado.DivergenciaSap)
            {
                _bloqueioLancamentoAberto = true;
                _bloqueioLancamentoPorItem[chaveConfirmada] = true;
                statusValueLabel.Text = "DIVERGÃŠNCIA SAP";
                statusHintLabel.Text = resultado.Mensagem;
                statusLabel.Text = resultado.Mensagem;
                MessageBox.Show(resultado.Mensagem, "DivergÃªncia SAP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            statusValueLabel.Text = resultado.EstruturaPendente ? "ESTRUTURA PENDENTE" : "ERRO SAP";
            statusHintLabel.Text = resultado.Mensagem;
            statusLabel.Text = resultado.Mensagem;
            MessageBox.Show(resultado.Mensagem, "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ProcessoSemiAcabadoForm] Falha ao confirmar semi-acabado: {ex.GetType().Name}");
            statusLabel.Text = "NÃ£o foi possÃ­vel concluir o envio ao SAP. Acione o suporte.";
            MessageBox.Show(statusLabel.Text, "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            productionDataGridView.Enabled = true;
            _operacaoEmAndamento = false;
            if (_ordemSelecionada is not null
                && !string.Equals(ChaveItemOrdem(_ordemSelecionada), chaveConfirmada, StringComparison.Ordinal)
                && LocalizarLinhaPrincipalPorChave(chaveConfirmada) is DataGridViewRow linhaConfirmada)
            {
                _restaurandoSelecaoItem = true;
                try
                {
                    foreach (DataGridViewRow linha in productionDataGridView.Rows)
                    {
                        if (!linha.IsNewRow)
                        {
                            linha.Selected = false;
                        }
                    }

                    linhaConfirmada.Selected = true;
                    productionDataGridView.CurrentCell = linhaConfirmada.Cells[0];
                    AplicarItemSelecionado(ordemConfirmada);
                }
                finally
                {
                    _restaurandoSelecaoItem = false;
                }
            }

            AtualizarBotoesOperacao();
        }
    }

    private async Task TratarEnvioDuplicadoBloqueadoAsync(ResultadoEnvioSemiAcabadoSap resultado, SemiAcabadoOrdem ordemConfirmada, string chave)
    {
        LancamentoSemiAcabado? persistido = null;
        if (resultado.CodigoLancamento is long codigoLancamento)
        {
            try
            {
                persistido = await _controller.ObterLancamentoCompletoAsync(codigoLancamento, CancellationToken.None);
                _codigoLancamentoPersistido = codigoLancamento;
                _codigoLancamentoPersistidoPorItem[chave] = codigoLancamento;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning($"[ProcessoSemiAcabadoForm] Falha ao consultar lanÃ§amento bloqueado: {ex.GetType().Name}");
            }
        }

        string status = persistido?.StatusLancamento ?? string.Empty;
        switch (status)
        {
            case "ENVIANDO_SAP":
                _bloqueioLancamentoAberto = true;
                _bloqueioLancamentoPorItem[chave] = true;
                statusValueLabel.Text = "ENVIANDO SAP";
                statusHintLabel.Text = "Envio em andamento. Aguarde ou acione o suporte para verificar no SAP.";
                statusLabel.Text = resultado.Mensagem;
                break;

            case "DIVERGENCIA_SAP":
                _bloqueioLancamentoAberto = true;
                _bloqueioLancamentoPorItem[chave] = true;
                statusValueLabel.Text = "DIVERGÃŠNCIA SAP";
                statusHintLabel.Text = "Confira o documento no SAP. NÃ£o reenviar sem suporte.";
                statusLabel.Text = resultado.Mensagem;
                break;

            case "CONFIRMADO_SAP":
                _confirmadoNaSessaoPorItem[chave] = true;
                _leituraIniciada = false;
                AtualizarEstadoLeitura(false);
                statusValueLabel.Text = "CONFIRMADO SAP";
                statusHintLabel.Text = string.IsNullOrWhiteSpace(persistido?.MaterialDocument)
                    ? "LanÃ§amento jÃ¡ confirmado no SAP."
                    : $"Documento {persistido.MaterialDocument}/{persistido.MaterialDocumentYear}.";
                statusLabel.Text = statusHintLabel.Text;
                break;

            case "ERRO_SAP":
                if (resultado.CodigoLancamento is long codigoErro)
                {
                    await CarregarLancamentoPersistidoParaReenvioAsync(codigoErro, chave);
                }

                _modoReenvioLancamentoPersistido = true;
                _modoReenvioPorItem[chave] = true;
                _bloqueioLancamentoAberto = false;
                _bloqueioLancamentoPorItem[chave] = false;
                statusValueLabel.Text = "ERRO SAP";
                statusHintLabel.Text = "Reenvio controlado do mesmo lanÃ§amento (nÃ£o cria novo).";
                statusLabel.Text = resultado.Mensagem;
                break;

            default:
                _bloqueioLancamentoAberto = true;
                _bloqueioLancamentoPorItem[chave] = true;
                statusValueLabel.Text = "ENVIO BLOQUEADO";
                statusHintLabel.Text = resultado.Mensagem;
                statusLabel.Text = resultado.Mensagem;
                break;
        }

        AtualizarBotoesOperacao();
        MessageBox.Show(resultado.Mensagem, "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private async Task<bool> GarantirBalancaSemiAcabadoConfiguradaAsync()
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
            statusLabel.Text = MensagemBalancaSemiAcabadoNaoConfigurada;
            MessageBox.Show(MensagemBalancaSemiAcabadoNaoConfigurada, "BalanÃ§a", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        await Task.CompletedTask;
        return true;
    }

    private async Task<TaraCadastro?> GarantirTaraSemiAcabadoSelecionadaAsync()
    {
        if (_ordemSelecionada is null)
        {
            return null;
        }

        string chave = ChaveItemOrdem(_ordemSelecionada);
        if (_tarasPorItemOrdem.TryGetValue(chave, out TaraCadastro? taraExistente))
        {
            return taraExistente;
        }

        if (_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0)
        {
            string mensagem = "UsuÃ¡rio sem setor definido: nÃ£o Ã© possÃ­vel selecionar a tara do semi-acabado.";
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "SeleÃ§Ã£o de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        IReadOnlyList<TaraCadastro> taras = await _controller.ListarTarasAtivasPorSetorAsync(codigoSetor);
        if (taras.Count == 0)
        {
            string mensagem = "Nenhuma tara ativa para o setor do usuÃ¡rio. Cadastre uma tara antes de pesar.";
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "SeleÃ§Ã£o de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        using SelecaoTaraPesagemForm form = new(taras, _ordemSelecionada.MaterialProduzido);
        if (form.ShowDialog(this) != DialogResult.OK || form.TaraSelecionada is null)
        {
            statusLabel.Text = "SeleÃ§Ã£o de tara cancelada.";
            return null;
        }

        _tarasPorItemOrdem[chave] = form.TaraSelecionada;
        statusLabel.Text = $"Tara '{form.TaraSelecionada.NomeTara}' selecionada para o semi-acabado.";
        return form.TaraSelecionada;
    }

    private bool SolicitarPesoManual(decimal taraKg, out decimal pesoKg)
    {
        pesoKg = 0m;
        using Form prompt = new()
        {
            Text = "Peso manual - Produto Semi-Acabado",
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
            Text = $"Informe o peso bruto em KG.\r\nTara aplicada: {FormatarKg(taraKg)}.",
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

        string texto = pesoTextBox.Text.Trim().Replace(',', '.');
        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out pesoKg) || pesoKg <= 0m)
        {
            MessageBox.Show("Informe um peso bruto vÃ¡lido maior que zero.", "Peso manual", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }


    private async Task CancelarLancamentoLocalPersistidoAsync()
    {
        if (_codigoLancamentoPersistido is not long codigoLancamento || _ordemSelecionada is null)
        {
            MessageBox.Show("Nenhum lançamento local em ERRO_SAP selecionado para cancelamento.", "Cancelar lançamento local", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!_modoReenvioLancamentoPersistido || !string.Equals(_statusLancamentoPersistidoAtual, "ERRO_SAP", StringComparison.Ordinal))
        {
            MessageBox.Show(
                "O lançamento não pode ser cancelado porque o resultado SAP é indeterminado. Verifique o documento no SAP antes de qualquer ação.",
                "Cancelar lançamento local",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Cancelar, "cancelar lançamento local de semi-acabado"))
        {
            return;
        }

        string motivo = SolicitarMotivoCancelamentoLocal();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            statusLabel.Text = "Cancelamento local não executado: motivo não informado.";
            return;
        }

        decimal total = ObterPesagens(ChaveItemOrdem(_ordemSelecionada)).Where(PesagemSemiAcabadoCalculos.PesagemValida).Sum(p => p.PesoLiquidoKg);
        DialogResult confirmar = MessageBox.Show(
            "Este lançamento possui pesagens e etiquetas já impressas.\n\n"
            + $"OP: {_ordemSelecionada.NumeroOrdem}\n"
            + $"Peso total: {FormatarKg(total)}\n"
            + "Status: ERRO SAP\n\n"
            + "Confirme no SAP que nenhum documento foi criado.\n\n"
            + "Ao cancelar:\n"
            + "- as pesagens serão preservadas no histórico;\n"
            + "- as etiquetas deverão ser descartadas fisicamente;\n"
            + "- um novo lançamento poderá ser iniciado.",
            "Cancelar lançamento local",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmar != DialogResult.Yes)
        {
            statusLabel.Text = "Cancelamento local não executado.";
            return;
        }

        _operacaoEmAndamento = true;
        AtualizarBotoesOperacao();
        try
        {
            ResultadoEnvioSemiAcabadoSap resultado = await _controller.CancelarLancamentoLocalAsync(
                codigoLancamento,
                motivo,
                EstadoSessaoUsuarioAtual.SessaoAtual?.Login ?? Environment.UserName,
                CancellationToken.None);

            statusLabel.Text = resultado.Mensagem;
            if (resultado.CodigoLancamento == codigoLancamento && resultado.Mensagem.Contains("cancelado com sucesso", StringComparison.OrdinalIgnoreCase))
            {
                string chave = ChaveItemOrdem(_ordemSelecionada);
                _pesagensPorItemOrdem.Remove(chave);
                _codigoLancamentoPersistido = null;
                _codigoLancamentoPersistidoPorItem[chave] = null;
                _modoReenvioLancamentoPersistido = false;
                _modoReenvioPorItem[chave] = false;
                _bloqueioLancamentoAberto = false;
                _bloqueioLancamentoPorItem[chave] = false;
                _statusLancamentoPersistidoAtual = "CANCELADO";
                statusValueLabel.Text = "CANCELADO";
                statusHintLabel.Text = "Lançamento local cancelado. Inicie nova leitura para gerar novo lançamento.";
                AtualizarEstadoLeitura(false);
                AtualizarGridPesagens();
                AtualizarResumoPesagem();
                AtualizarContadores();
            }

            MessageBox.Show(resultado.Mensagem, "Cancelar lançamento local", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoesOperacao();
        }
    }

    private string SolicitarMotivoCancelamentoLocal()
    {
        using Form prompt = new()
        {
            Text = "Motivo do cancelamento",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(480, 220),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label label = new()
        {
            Text = "Informe o motivo do cancelamento local:",
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 8, 16, 0),
            Font = new Font("Cascadia Code", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56)
        };
        TextBox motivoTextBox = new()
        {
            Multiline = true,
            MaxLength = 500,
            Location = new Point(16, 56),
            Size = new Size(448, 92),
            Font = new Font("Cascadia Code", 9F)
        };
        Button okButton = CriarBotaoDialogo("Confirmar", Color.FromArgb(34, 166, 82), DialogResult.OK);
        okButton.Location = new Point(232, 164);
        Button cancelarButton = CriarBotaoDialogo("Cancelar", Color.FromArgb(82, 87, 96), DialogResult.Cancel);
        cancelarButton.Location = new Point(352, 164);
        prompt.Controls.Add(label);
        prompt.Controls.Add(motivoTextBox);
        prompt.Controls.Add(okButton);
        prompt.Controls.Add(cancelarButton);
        prompt.AcceptButton = okButton;
        prompt.CancelButton = cancelarButton;

        return prompt.ShowDialog(this) == DialogResult.OK ? motivoTextBox.Text.Trim() : string.Empty;
    }
    private void CancelarUltimaPesagem()
    {
        if (_modoReenvioLancamentoPersistido && string.Equals(_statusLancamentoPersistidoAtual, "ERRO_SAP", StringComparison.Ordinal))
        {
            _ = CancelarLancamentoLocalPersistidoAsync();
            return;
        }

        if (BloquearAcaoDuranteOperacao())
        {
            return;
        }

        if (_ordemSelecionada is null)
        {
            return;
        }

        if (_modoReenvioLancamentoPersistido || _bloqueioLancamentoAberto)
        {
            MessageBox.Show(
                "NÃ£o Ã© possÃ­vel cancelar pesagem de lanÃ§amento persistido ou bloqueado pelo SAP.",
                "Produto Semi-Acabado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        List<PesagemSemiAcabado> pesagens = ObterPesagens(ChaveItemOrdem(_ordemSelecionada));
        PesagemSemiAcabado? ultimaValida = PesagemSemiAcabadoCalculos.UltimaValida(pesagens);
        if (ultimaValida is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"A etiqueta desta pesagem jÃ¡ pode ter sido impressa.\n\n"
                + $"Pesagem: {ultimaValida.Sequencia:00}\n"
                + $"Peso lÃ­quido: {FormatarKg(ultimaValida.PesoLiquidoKg)}\n\n"
                + "Descarte fisicamente a etiqueta cancelada.\n"
                + "Confirma o cancelamento?",
                "Cancelar pesagem",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        ultimaValida.StatusPesagem = PesagemSemiAcabadoCalculos.StatusCancelada;
        ultimaValida.CanceladoEm = DateTime.Now;
        AtualizarLinhaPrincipalComPesagem(_ordemSelecionada);
        AtualizarGridPesagens();
        AtualizarResumoPesagem();
        AtualizarContadores();
        statusLabel.Text = "Ãšltima pesagem do semi-acabado cancelada.";
    }

    private void AtualizarGridPesagens()
    {
        materialDataGridView.Rows.Clear();
        if (_ordemSelecionada is null)
        {
            return;
        }

        PreencherGridPesagens(CriarContextosPesagensAtuais(_ordemSelecionada));
    }

    private List<ContextoPesagemSemiAcabadoGrid> CriarContextosPesagensAtuais(SemiAcabadoOrdem ordem)
    {
        string chave = ChaveItemOrdem(ordem);
        SemiAcabadoOrdem ordemContexto = _ordemPersistidaPorItem.TryGetValue(chave, out SemiAcabadoOrdem? persistida)
            ? persistida
            : ordem;

        return ObterPesagens(chave)
            .Select(pesagem => new ContextoPesagemSemiAcabadoGrid
            {
                Pesagem = pesagem,
                Ordem = ordemContexto,
                NumeroOrdem = ordemContexto.NumeroOrdem,
                ItemOrdem = ordemContexto.ItemOrdem,
                MaterialProduzido = ordemContexto.MaterialProduzido,
                Lote = ordemContexto.Lote,
                DescricaoMaterial = ordemContexto.DescricaoMaterial
            })
            .ToList();
    }

    private void PreencherGridPesagens(IEnumerable<ContextoPesagemSemiAcabadoGrid> linhas)
    {
        materialDataGridView.Rows.Clear();
        foreach (ContextoPesagemSemiAcabadoGrid linha in linhas)
        {
            AdicionarLinhaPesagemGrid(linha);
        }
    }

    private void AdicionarLinhaPesagemGrid(ContextoPesagemSemiAcabadoGrid contexto)
    {
        PesagemSemiAcabado pesagem = contexto.Pesagem;
        string origemStatus = contexto.Historica
            ? $"{pesagem.Origem} Â· HISTÃ“RICO CONFIRMADO"
            : $"{pesagem.Origem} Â· {pesagem.StatusPesagem}";
        string documento = string.IsNullOrWhiteSpace(contexto.MaterialDocument)
            ? string.Empty
            : $" Â· DOC {contexto.MaterialDocument}/{contexto.MaterialDocumentYear}";

        int indice = materialDataGridView.Rows.Add(
            pesagem.Sequencia.ToString("00", CultureInfo.InvariantCulture),
            FormatarKg(pesagem.PesoBrutoKg),
            FormatarKg(pesagem.PesoTaraKg),
            FormatarKg(pesagem.PesoLiquidoKg),
            pesagem.RegistradoEm.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")),
            $"{origemStatus}{documento}");

        DataGridViewRow linha = materialDataGridView.Rows[indice];
        linha.Tag = contexto;
        foreach (DataGridViewCell cell in linha.Cells)
        {
            cell.ToolTipText = contexto.Historica
                ? $"HistÃ³rico confirmado {contexto.MaterialDocument}/{contexto.MaterialDocumentYear}"
                : "Pesagem atual";
        }

        if (contexto.Historica)
        {
            linha.DefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
            linha.DefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        }
        else if (!pesagem.Valida)
        {
            linha.DefaultCellStyle.ForeColor = Color.FromArgb(148, 163, 184);
        }
    }

    private SemiAcabadoOrdem ObterOrdemParaReimpressao(ContextoPesagemSemiAcabadoGrid? contexto)
    {
        if (contexto?.Ordem is SemiAcabadoOrdem ordemContexto)
        {
            if (_ordemSelecionada is not null
                && !string.Equals(ChaveItemOrdem(_ordemSelecionada), ChaveItemOrdem(ordemContexto), StringComparison.Ordinal))
            {
                throw new ErroOperacionalEsperadoException("A pesagem selecionada pertence a outro item. Selecione novamente o item correto antes de reimprimir.");
            }

            return ordemContexto;
        }

        return _ordemSelecionada
            ?? throw new ErroOperacionalEsperadoException("Selecione uma OP/item antes de reimprimir.");
    }

    /// <summary>
    /// Tarefa 20.6 (Ajuste 1): reflete o peso liquido acumulado (e a origem consolidada) na linha
    /// principal da OP (grid `productionDataGridView`, coluna "Peso"). Vazio se nao houver pesagem.
    /// </summary>
    private void AtualizarLinhaPrincipalComPesagem(SemiAcabadoOrdem ordem)
    {
        string chave = ChaveItemOrdem(ordem);
        List<PesagemSemiAcabado> pesagens = ObterPesagens(chave);

        // Total da linha considera SOMENTE pesagens vÃ¡lidas (canceladas nÃ£o entram no total nem no payload SAP).
        decimal pesoLiquidoTotal = PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(pesagens);
        string origem = DescreverOrigemConsolidada(pesagens);

        DataGridViewRow? linha = LocalizarLinhaPrincipalPorChave(chave);
        if (linha is null)
        {
            return;
        }

        linha.Cells["productionPesoLidoColumn"].Value =
            pesoLiquidoTotal > 0m ? FormatarKg(pesoLiquidoTotal) : string.Empty;
        linha.Cells["productionPesoOrigemColumn"].Value = origem;

        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (!row.IsNewRow)
            {
                row.Selected = false;
            }
        }

        linha.Selected = true;
        productionDataGridView.CurrentCell = linha.Cells["productionPesoLidoColumn"];
    }

    private DataGridViewRow? LocalizarLinhaPrincipalPorChave(string chave)
    {
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (row.Tag is SemiAcabadoOrdem ordem
                && string.Equals(ChaveItemOrdem(ordem), chave, StringComparison.Ordinal))
            {
                return row;
            }
        }

        return null;
    }

    private static string DescreverOrigemConsolidada(IReadOnlyList<PesagemSemiAcabado> pesagens)
    {
        // Origem consolidada considera apenas pesagens VÃLIDAS.
        List<PesagemSemiAcabado> validas = pesagens.Where(PesagemSemiAcabadoCalculos.PesagemValida).ToList();
        if (validas.Count == 0)
        {
            return string.Empty;
        }

        bool temManual = validas.Any(p => string.Equals(p.Origem, "MANUAL", StringComparison.OrdinalIgnoreCase));
        bool temBalanca = validas.Any(p => string.Equals(p.Origem, "BALANCA", StringComparison.OrdinalIgnoreCase));

        if (temManual && temBalanca)
        {
            return "MISTO";
        }

        return temBalanca ? "BALANCA" : "MANUAL";
    }

    private void AtualizarResumoPesagem()
    {
        decimal saldo = _ordemSelecionada?.QuantidadePendente ?? 0m;
        decimal utilizado = _ordemSelecionada is null
            ? 0m
            : PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(ObterPesagens(ChaveItemOrdem(_ordemSelecionada)));
        weightSummaryTitleLabel.Text = "INFORMAÃ‡ÃƒO DE PESAGEM";
        weightSummarySubtitleLabel.Text = $"Previsto: {FormatarKg(saldo)} | Utilizado: {FormatarKg(utilizado)} | Restante: {FormatarKg(Math.Max(saldo - utilizado, 0m))}";
        apontamentoInfoCaptionLabel.Text = "Peso bruto / tara / lÃ­quido";
        apontamentoInfoValueLabel.Text = _ordemSelecionada is null
            ? "Bruto: 0,000 KG | Tara: 0,000 KG | LÃ­quido: 0,000 KG"
            : MontarResumoUltimaPesagem();
        apontamentoChipCaptionLabel.Text = "OP";
        apontamentoChipValueLabel.Text = _ordemSelecionada?.NumeroOrdem ?? "-";
    }

    private string MontarResumoUltimaPesagem()
    {
        if (_ordemSelecionada is null)
        {
            return "Bruto: 0,000 KG | Tara: 0,000 KG | LÃ­quido: 0,000 KG";
        }

        PesagemSemiAcabado? ultima = PesagemSemiAcabadoCalculos.UltimaValida(ObterPesagens(ChaveItemOrdem(_ordemSelecionada)));
        return ultima is null
            ? "Bruto: 0,000 KG | Tara: 0,000 KG | LÃ­quido: 0,000 KG"
            : $"Bruto: {FormatarKg(ultima.PesoBrutoKg)} | Tara: {FormatarKg(ultima.PesoTaraKg)} | LÃ­quido: {FormatarKg(ultima.PesoLiquidoKg)}";
    }

    private void AtualizarContadores()
    {
        decimal saldoTotal = _itensOrdem.Sum(i => i.QuantidadePendente);
        // Contador operacional conta SOMENTE pesagens vÃ¡lidas (canceladas ficam sÃ³ no histÃ³rico visual do grid).
        int quantidadePesagens = _pesagensPorItemOrdem.Values.Sum(PesagemSemiAcabadoCalculos.ContarValidas);
        boxesCounterLabel.Text = saldoTotal.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
        packagesCounterLabel.Text = quantidadePesagens.ToString("000", CultureInfo.InvariantCulture);
        productionFooterLabel.Text = $"{productionDataGridView.Rows.Count} item(ns) de OP carregado(s).";
    }

    private void AtualizarEstadoLeitura(bool iniciada)
    {
        _leituraIniciada = iniciada;
        if (!iniciada)
        {
            _chaveItemEmLeitura = null;
        }

        // Tarefa 20.6 (Ajuste 2): status de leitura identico ao da Entrada.
        sideReadingStatusLabel.Text = iniciada ? "Ativo" : "Inativo";
        sideReadingStatusLabel.ForeColor = iniciada
            ? ReadingStatusActiveColor
            : ReadingStatusInactiveColor;

        AtualizarStatusCardLeitura(iniciada);
        AtualizarBloqueioCabecalho(iniciada);
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

        statusCardIcon.Text = iniciada ? "âœ“" : "!";
        statusCardIcon.ForeColor = statusColor;

        statusValueLabel.Text = iniciada ? "ATIVA" : "INATIVA";
        statusValueLabel.ForeColor = statusColor;

        statusHintLabel.Text = iniciada
            ? "Leitura liberada para registro"
            : "Leitura aguardando inicio";
        statusHintLabel.ForeColor = Color.FromArgb(98, 108, 124);

        statusCard.Invalidate(true);
        statusCardIcon.Invalidate();
        statusValueLabel.Invalidate();
        statusHintLabel.Invalidate();
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
        // BotÃ£o Confirmar depende de existir ao menos UMA pesagem VÃLIDA (canceladas nÃ£o contam).
        bool possuiPesagem = _ordemSelecionada is not null
            && PesagemSemiAcabadoCalculos.ContarValidas(ObterPesagens(ChaveItemOrdem(_ordemSelecionada))) > 0;

        // Tarefa 20.5 (Ajuste 4): iniciar/parar no mesmo padrao visual da Entrada
        // (cinza desabilitado sem OP; verde com OP; vermelho durante a leitura).
        bool podeAlternarLeitura = livre
            && _ordemSelecionada is not null
            && !ItemAtualConfirmadoNaSessao
            && !_modoReenvioLancamentoPersistido
            && !_bloqueioLancamentoAberto;
        iniciarLeituraButton.BaseBackColor = _leituraIniciada
            ? Color.FromArgb(212, 37, 49)
            : podeAlternarLeitura ? Color.FromArgb(34, 166, 82) : Color.FromArgb(156, 163, 175);
        iniciarLeituraButton.BaseForeColor = Color.White;
        iniciarLeituraButton.IconFontFamily = "Segoe MDL2 Assets";
        iniciarLeituraButton.IconGlyph = _leituraIniciada ? "îœš" : "î¨";
        iniciarLeituraButton.PrimaryText = _leituraIniciada ? "PARAR LEITURA" : "INICIAR LEITURA";
        iniciarLeituraButton.Enabled = podeAlternarLeitura;
        iniciarLeituraButton.Cursor = podeAlternarLeitura ? Cursors.Hand : Cursors.Default;
        iniciarLeituraButton.Invalidate();
        startActionPanel.BackColor = !_leituraIniciada && podeAlternarLeitura
            ? Color.FromArgb(34, 166, 82)
            : Color.FromArgb(156, 163, 175);
        lerEtiquetaButton.Visible = _leituraIniciada;
        leituraManualButton.Visible = _leituraIniciada;
        lerEtiquetaButton.Enabled = livre && _ordemSelecionada is not null && _leituraIniciada;
        leituraManualButton.Enabled = livre && _ordemSelecionada is not null && _leituraIniciada;
        productionActionsButton.Visible = !_leituraIniciada
            && _ordemSelecionada is not null
            && possuiPesagem
            && !ItemAtualConfirmadoNaSessao
            && !_bloqueioLancamentoAberto;
        productionActionsButton.Enabled = productionActionsButton.Visible && livre;
    }

    private void LimparOpCarregada()
    {
        LimparContextosOperacionais();
        _ordemSelecionada = null;
        _itensOrdem = [];
        productionDataGridView.Rows.Clear();
        materialDataGridView.Rows.Clear();
        lotTextBox.Clear();
        stepLabel.Text = "-";
        finishedProductCodeTextBox.Clear();
        finishedProductTextBox.Clear();
        ovenExitTextBox.Clear();
        classificationDateTextBox.Clear();
        manufacturingDateTextBox.Clear();
        expirationDateTextBox.Clear();
        AtualizarEstadoLeitura(false);
        AtualizarResumoPesagem();
        AtualizarContadores();
    }

    private void LimparContextosOperacionais()
    {
        _versaoSelecaoItem++;
        _confirmadoNaSessaoPorItem.Clear();
        _codigoLancamentoPersistidoPorItem.Clear();
        _modoReenvioPorItem.Clear();
        _bloqueioLancamentoPorItem.Clear();
        _ordemPersistidaPorItem.Clear();
        _pesagensPorItemOrdem.Clear();
        _tarasPorItemOrdem.Clear();
        _codigoLancamentoPersistido = null;
        _modoReenvioLancamentoPersistido = false;
        _bloqueioLancamentoAberto = false;
        _chaveItemEmLeitura = null;
        _leituraIniciada = false;
    }

    private async Task<bool> BloquearAcaoSemPermissaoAsync(string acao, string descricaoAcao)
    {
        // TODO PermissÃµes:
        // Criar permissÃ£o especÃ­fica para Produto Semi-Acabado quando a matriz de permissÃµes for atualizada.
        if (AutorizacaoServico.PossuiPermissao(AutorizacaoServico.ModuloProcesso, PermissoesSistema.Rotinas.LeituraProducao, acao))
        {
            return false;
        }

        await AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            AutorizacaoServico.ModuloProcesso,
            PermissoesSistema.Rotinas.LeituraProducao,
            acao,
            descricaoAcao,
            "ProcessoSemiAcabadoForm");

        string mensagem = "UsuÃ¡rio sem permissÃ£o para executar produto semi-acabado.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private static bool ValidarSaldoSemiAcabado(decimal pesoLiquidoTotalKg, decimal saldoPendenteKg, out string mensagem)
    {
        if (saldoPendenteKg > 0m && pesoLiquidoTotalKg > saldoPendenteKg)
        {
            mensagem = $"Peso lÃ­quido informado ({pesoLiquidoTotalKg:0.###} KG) ultrapassa o saldo previsto do semi-acabado ({saldoPendenteKg:0.###} KG).";
            return false;
        }

        mensagem = string.Empty;
        return true;
    }

    private List<PesagemSemiAcabado> ObterPesagens(string chave)
    {
        if (!_pesagensPorItemOrdem.TryGetValue(chave, out List<PesagemSemiAcabado>? pesagens))
        {
            pesagens = [];
            _pesagensPorItemOrdem[chave] = pesagens;
        }

        return pesagens;
    }

    private static string ChaveItemOrdem(SemiAcabadoOrdem ordem)
    {
        string item = string.IsNullOrWhiteSpace(ordem.ItemOrdem)
            ? ordem.MaterialProduzido
            : ordem.ItemOrdem;

        return $"{ordem.NumeroOrdem.Trim()}|{item.Trim()}";
    }


    private static string SanitizarDiagnostico(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        string sanitizado = texto.Replace("Authorization", "Autorizacao", StringComparison.OrdinalIgnoreCase)
            .Replace("cookie", "sessao", StringComparison.OrdinalIgnoreCase)
            .Replace("token", "marcador", StringComparison.OrdinalIgnoreCase)
            .Replace("senha", "credencial", StringComparison.OrdinalIgnoreCase)
            .Replace("password", "credencial", StringComparison.OrdinalIgnoreCase);
        return sanitizado.Length <= 250 ? sanitizado : sanitizado[..250];
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

    private async void PedidoComboBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            await ConsultarOpSelecionadaAsync();
        }
    }

    private bool BloquearAcaoDuranteOperacao()
    {
        if (!_operacaoEmAndamento)
        {
            return false;
        }

        statusLabel.Text = "Aguarde a conclusÃ£o da operaÃ§Ã£o em andamento.";
        return true;
    }

    private async void ProcessoSemiAcabadoForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_operacaoEmAndamento && (e.KeyCode == Keys.F9 || e.KeyCode == Keys.F12 || e.KeyCode == Keys.Delete))
        {
            e.SuppressKeyPress = true;
            BloquearAcaoDuranteOperacao();
            return;
        }

        if (e.KeyCode == Keys.F9)
        {
            e.SuppressKeyPress = true;
            await RegistrarPesoManualAsync();
        }
        else if (e.KeyCode == Keys.F12)
        {
            e.SuppressKeyPress = true;
            await RegistrarPesoBalancaAsync();
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.SuppressKeyPress = true;
            CancelarUltimaPesagem();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            if (BloquearAcaoDuranteOperacao())
            {
                return;
            }

            Close();
        }
    }

    private void AlignDateCardLayout(object? sender, EventArgs e)
    {
    }

    private void AlignPlannedProductionCardLayout(object? sender, EventArgs e)
    {
    }
}

