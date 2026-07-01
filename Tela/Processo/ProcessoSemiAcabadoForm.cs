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
    internal const string MensagemSemiAcabadoPendenteSap = "Semi-acabado salvo localmente. Envio SAP 101 pendente de validação com OP de teste.";
    internal const string MensagemBalancaSemiAcabadoNaoConfigurada = "Balança de produto semi-acabado não configurada para esta operação.";

    private readonly SemiAcabadoController _controller;
    private readonly BalancaLeituraServico _balancaLeituraServico = new();
    private readonly Dictionary<string, List<PesagemSemiAcabado>> _pesagensPorItemOrdem = [];
    private readonly Dictionary<string, TaraCadastro> _tarasPorItemOrdem = [];
    private IReadOnlyList<SemiAcabadoOrdem> _itensOrdem = [];
    private SemiAcabadoOrdem? _ordemSelecionada;
    private ContextoTerminalLocal? _contextoTerminal;
    private long? _idSetorSelecionado;
    private bool _leituraIniciada;
    private bool _operacaoEmAndamento;

    // Tarefa 20.4: uma vez confirmado o semi-acabado nesta sessao, bloqueia nova pesagem/confirmacao
    // ate recarregar/trocar a OP (evita confirmacao/lancamento duplicado).
    private bool _semiAcabadoConfirmadoNaSessao;
    private System.Windows.Forms.Timer? _footerClockTimer;

    // Tarefa 20.6 (Ajuste 2): mesmas cores do status de leitura da Entrada.
    private static readonly Color ReadingStatusActiveColor = Color.FromArgb(34, 166, 82);
    private static readonly Color ReadingStatusInactiveColor = Color.FromArgb(220, 53, 69);

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
        headerSubtitleLabel.Text = "Pesagem e entrada de produto semi-acabado por ordem de produção";
        productionOrderCaptionLabel.Text = "OP";
        pedidoComboBox.AccessibleName = "Ordem de Produção";
        stepCaptionLabel.Text = "Consulta de OP";
        stepDescriptionLabel.Text = "OP selecionada";
        finishedProductCaptionLabel.Text = "Semi-acabado";
        // Tarefa 20.5 (Ajuste 2): igual ao Consumo — só o codigo do material no card, sem descricao duplicada.
        finishedProductTextBox.Visible = false;
        lotCaptionLabel.Text = "Lote";
        ovenExitCaptionLabel.Text = "Depósito destino";
        classificationDateCaptionLabel.Text = "Saldo pendente";
        manufacturingDateCaptionLabel.Text = "Quantidade planejada";
        expirationDateCaptionLabel.Text = "Quantidade entregue";
        materialTitleLabel.Text = "Pesagens do Semi-Acabado";
        productionReadingsTitleLabel.Text = "Itens Produzidos da OP";
        productionActionsButton.Text = "CONFIRMAR SEMI-ACABADO";
        productionActionsButton.Visible = false;
        boxesCaptionLabel.Text = "Saldo OP";
        packagesCaptionLabel.Text = "Pesagens";
        readWeightLegendTextLabel.Text = "F12 - Ler peso balança";
        manualLotLegendTextLabel.Text = "F9 - Digitar peso";
        lerEtiquetaButton.PrimaryText = "LER PESO";
        lerEtiquetaButton.KeyHint = "F12";
        leituraManualButton.PrimaryText = "DIGITAR PESO";
        leituraManualButton.KeyHint = "F9";
        deleteLastLegendTextLabel.Text = "Del - Cancelar última pesagem";
        deleteByCodeLegendTextLabel.Text = "Esc - Fechar";
        statusValueLabel.Text = "AGUARDANDO OP";
        statusHintLabel.Text = "Informe uma OP para iniciar.";
        sapStatusLabel.Text = _controller.SapSimulado ? "SAP OP: DEMONSTRAÇÃO" : "SAP OP: CONSULTA";
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
    /// Tarefa 20.5 (Ajuste 1): mesmo padrao do Consumo — esconde o painel do icone (marca rosa atras do
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
        // Não assinar o Click do productionOrderSearchLabel (evita NRE caso o controle seja removido do Designer).
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
        productionDataGridView.SelectionChanged += (_, _) => CapturarItemSelecionado();
        productionDataGridView.CellClick += (_, _) => CapturarItemSelecionado();
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
        // Tarefa 20.4 (Ajuste 6): nao permite fechar/voltar com a leitura ativa (evita perder pesagem).
        if (_leituraIniciada)
        {
            e.Cancel = true;
            statusLabel.Text = "Finalize a leitura antes de sair da tela.";
            return;
        }

        _footerClockTimer?.Dispose();
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
        // Tarefa 20.6 (Ajuste 1.1): semantica operacional — coluna "Peso" recebe o liquido pesado.
        productionCodeColumn.HeaderText = "Material";
        productionProductColumn.HeaderText = "Descrição";
        productionQuantityColumn.HeaderText = "Qtd planejada";
        productionWeightColumn.HeaderText = "Saldo pendente";
        productionPesoLidoColumn.HeaderText = "Peso";
        productionItemIdColumn.HeaderText = "Item OP";
        productionPesoOrigemColumn.HeaderText = "Origem";
        productionNumeroItemColumn.HeaderText = "Depósito destino";
    }

    private void ConfigurarGridPesagens()
    {
        materialDataGridView.AutoGenerateColumns = false;
        materialDataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        materialDataGridView.MultiSelect = false;
        materialDataGridView.ReadOnly = true;
        materialStatusColumn.HeaderText = "Seq.";
        materialCodeColumn.HeaderText = "Bruto KG";
        materialDescriptionColumn.HeaderText = "Tara KG";
        materialLotColumn.HeaderText = "Líquido KG";
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
            LimparOpCarregada();
            return;
        }

        try
        {
            _operacaoEmAndamento = true;
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

            _semiAcabadoConfirmadoNaSessao = false; // Tarefa 20.4: OP consultada com sucesso reinicia a sessao
            _itensOrdem = resultado.Itens;
            _pesagensPorItemOrdem.Clear();
            _tarasPorItemOrdem.Clear();
            PreencherItensSemiAcabado(_itensOrdem);
            if (productionDataGridView.Rows.Count > 0)
            {
                productionDataGridView.Rows[0].Selected = true;
                productionDataGridView.CurrentCell = productionDataGridView.Rows[0].Cells[0];
                CapturarItemSelecionado();
            }

            statusLabel.Text = $"OP {resultado.NumeroOp} carregada para produto semi-acabado.";
            statusValueLabel.Text = "OP CARREGADA";
            statusHintLabel.Text = "Inicie a leitura e registre as pesagens.";
        }
        catch (Exception ex)
        {
            LimparOpCarregada();
            System.Diagnostics.Trace.TraceWarning($"[ProcessoSemiAcabadoForm] Falha ao consultar OP: {ex.GetType().Name}");
            MessageBox.Show("Não foi possível consultar a OP. Tente novamente.", "Consulta de OP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoesOperacao();
        }
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
        DataGridViewRow? row = productionDataGridView.CurrentRow;
        if (row?.Tag is not SemiAcabadoOrdem ordem)
        {
            _ordemSelecionada = null;
            AtualizarResumoPesagem();
            return;
        }

        _ordemSelecionada = ordem;
        lotTextBox.Text = ordem.Lote;
        stepLabel.Text = ordem.NumeroOrdem;
        finishedProductCodeTextBox.Text = ordem.MaterialProduzido;
        finishedProductTextBox.Text = string.Empty; // Tarefa 20.5: descricao vive no grid, nao no card
        ovenExitTextBox.Text = ordem.DepositoDestino;
        classificationDateTextBox.Text = FormatarKg(ordem.QuantidadePendente);
        manufacturingDateTextBox.Text = FormatarKg(ordem.QuantidadePlanejada);
        expirationDateTextBox.Text = FormatarKg(ordem.QuantidadeEntregue);
        AtualizarGridPesagens();
        AtualizarResumoPesagem();
    }

    private void IniciarLeitura_Click(object? sender, EventArgs e)
    {
        if (_leituraIniciada)
        {
            AtualizarEstadoLeitura(false);
            statusLabel.Text = "Leitura de produto semi-acabado parada.";
            return;
        }

        if (_ordemSelecionada is null)
        {
            MessageBox.Show("Selecione uma OP antes de iniciar a leitura.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

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
                ? "Não foi possível ler o peso da balança."
                : leitura.Mensagem;
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!TryParsePesoKg(leitura.Peso, out decimal pesoBrutoKg))
        {
            MessageBox.Show("Peso retornado pela balança é inválido.", "Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RegistrarPesagemSemiAcabado(pesoBrutoKg, taraSelecionada.PesoKg, "BALANCA");
    }

    private async Task RegistrarPesoManualAsync()
    {
        // TODO Permissões:
        // Quando a matriz de permissões do Produto Semi-Acabado for criada,
        // separar permissão de peso manual se o negócio exigir.
        // Tarefa 20.5 (Ajuste 3): sem matriz específica, usa a mesma permissão operacional do processo (Executar).
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

        RegistrarPesagemSemiAcabado(pesoBrutoKg, taraSelecionada.PesoKg, "MANUAL");
    }

    private bool RegistrarPesagemSemiAcabado(decimal pesoBrutoKg, decimal taraKg, string origem)
    {
        if (_ordemSelecionada is null)
        {
            return false;
        }

        decimal pesoLiquidoKg = pesoBrutoKg - taraKg;
        if (pesoLiquidoKg <= 0m)
        {
            MessageBox.Show("Peso líquido do semi-acabado deve ser maior que zero.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        string chave = ChaveItemOrdem(_ordemSelecionada);
        decimal totalAtual = ObterPesagens(chave).Sum(p => p.PesoLiquidoKg);
        decimal novoTotal = totalAtual + pesoLiquidoKg;
        if (!ValidarSaldoSemiAcabado(novoTotal, _ordemSelecionada.QuantidadePendente, out string mensagemSaldo))
        {
            MessageBox.Show(mensagemSaldo, "Saldo OP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        List<PesagemSemiAcabado> pesagens = ObterPesagens(chave);
        pesagens.Add(new PesagemSemiAcabado
        {
            Sequencia = pesagens.Count + 1,
            PesoBrutoKg = pesoBrutoKg,
            PesoTaraKg = taraKg,
            PesoLiquidoKg = pesoLiquidoKg,
            Origem = origem,
            RegistradoEm = DateTime.Now
        });

        System.Diagnostics.Trace.TraceInformation(
            $"Pesagem semi-acabado: bruto={pesoBrutoKg:0.###}, tara={taraKg:0.###}, liquido={pesoLiquidoKg:0.###}, origem={origem}.");
        AtualizarLinhaPrincipalComPesagem(_ordemSelecionada);
        AtualizarGridPesagens();
        AtualizarResumoPesagem();
        AtualizarContadores();
        statusLabel.Text = $"Peso registrado. Bruto: {FormatarKg(pesoBrutoKg)} | Tara: {FormatarKg(taraKg)} | Líquido: {FormatarKg(pesoLiquidoKg)}.";
        return true;
    }

    private bool ValidarPodePesar()
    {
        // Tarefa 20.4 (Ajuste 2): sessao ja confirmada nao aceita nova pesagem ate recarregar a OP.
        if (_semiAcabadoConfirmadoNaSessao)
        {
            MessageBox.Show(
                "Semi-acabado já confirmado nesta sessão. Recarregue a OP para iniciar novo lançamento.",
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
        if (await BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Finalizar, "confirmar produto semi-acabado"))
        {
            return;
        }

        if (_ordemSelecionada is null)
        {
            MessageBox.Show("Selecione uma OP antes de confirmar o semi-acabado.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        List<PesagemSemiAcabado> pesagens = ObterPesagens(ChaveItemOrdem(_ordemSelecionada));
        if (pesagens.Count == 0)
        {
            MessageBox.Show("Registre ao menos uma pesagem antes de confirmar o semi-acabado.", "Produto Semi-Acabado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        LancamentoSemiAcabado lancamento = _controller.MontarLancamentoLocal(
            _ordemSelecionada,
            pesagens,
            EstadoSessaoUsuarioAtual.SessaoAtual?.Login ?? Environment.UserName);
        ResultadoPreviewSemiAcabado101 preview = _controller.GerarPreviewMaterialDocument101(lancamento, DateTime.UtcNow);
        if (!preview.Sucesso)
        {
            MessageBox.Show(preview.Mensagem, "Preview SAP 101", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Tarefa 20.4 (Ajuste 1): marca a sessao como confirmada e atualiza o estado dos botoes.
        _semiAcabadoConfirmadoNaSessao = true;

        // Ajuste 5: estado final claro ao operador; JSON tecnico permanece SOMENTE em Trace.
        statusLabel.Text = MensagemSemiAcabadoPendenteSap;
        statusValueLabel.Text = "PENDENTE SAP 101";
        statusHintLabel.Text = "Semi-acabado confirmado localmente. Recarregue a OP para novo lançamento.";
        System.Diagnostics.Trace.TraceInformation(
            "[SemiAcabado] Preview Material Document 101: {0}",
            preview.PayloadJson);
        AtualizarBotoesOperacao();
        MessageBox.Show(
            MensagemSemiAcabadoPendenteSap,
            "Produto Semi-Acabado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private LancamentoSemiAcabado MontarLancamentoSemiAcabadoAtual()
    {
        if (_ordemSelecionada is null)
        {
            throw new InvalidOperationException("OP nao selecionada.");
        }

        return _controller.MontarLancamentoLocal(
            _ordemSelecionada,
            ObterPesagens(ChaveItemOrdem(_ordemSelecionada)),
            EstadoSessaoUsuarioAtual.SessaoAtual?.Login ?? Environment.UserName);
    }

    private ResultadoPreviewSemiAcabado101 GerarPreviewMaterialDocument101Atual()
        => _controller.GerarPreviewMaterialDocument101(MontarLancamentoSemiAcabadoAtual(), DateTime.UtcNow);

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
            MessageBox.Show(MensagemBalancaSemiAcabadoNaoConfigurada, "Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            string mensagem = "Usuário sem setor definido: não é possível selecionar a tara do semi-acabado.";
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

        using SelecaoTaraPesagemForm form = new(taras, _ordemSelecionada.MaterialProduzido);
        if (form.ShowDialog(this) != DialogResult.OK || form.TaraSelecionada is null)
        {
            statusLabel.Text = "Seleção de tara cancelada.";
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
            MessageBox.Show("Informe um peso bruto válido maior que zero.", "Peso manual", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void CancelarUltimaPesagem()
    {
        if (_ordemSelecionada is null)
        {
            return;
        }

        List<PesagemSemiAcabado> pesagens = ObterPesagens(ChaveItemOrdem(_ordemSelecionada));
        if (pesagens.Count == 0)
        {
            return;
        }

        pesagens.RemoveAt(pesagens.Count - 1);
        AtualizarLinhaPrincipalComPesagem(_ordemSelecionada);
        AtualizarGridPesagens();
        AtualizarResumoPesagem();
        AtualizarContadores();
        statusLabel.Text = "Última pesagem do semi-acabado cancelada.";
    }

    private void AtualizarGridPesagens()
    {
        materialDataGridView.Rows.Clear();
        if (_ordemSelecionada is null)
        {
            return;
        }

        foreach (PesagemSemiAcabado pesagem in ObterPesagens(ChaveItemOrdem(_ordemSelecionada)))
        {
            materialDataGridView.Rows.Add(
                pesagem.Sequencia.ToString("00", CultureInfo.InvariantCulture),
                FormatarKg(pesagem.PesoBrutoKg),
                FormatarKg(pesagem.PesoTaraKg),
                FormatarKg(pesagem.PesoLiquidoKg),
                pesagem.RegistradoEm.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR")),
                pesagem.Origem);
        }
    }

    /// <summary>
    /// Tarefa 20.6 (Ajuste 1): reflete o peso liquido acumulado (e a origem consolidada) na linha
    /// principal da OP (grid `productionDataGridView`, coluna "Peso"). Vazio se nao houver pesagem.
    /// </summary>
    private void AtualizarLinhaPrincipalComPesagem(SemiAcabadoOrdem ordem)
    {
        string chave = ChaveItemOrdem(ordem);
        List<PesagemSemiAcabado> pesagens = ObterPesagens(chave);

        decimal pesoLiquidoTotal = pesagens.Sum(p => p.PesoLiquidoKg);
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
        if (pesagens.Count == 0)
        {
            return string.Empty;
        }

        bool temManual = pesagens.Any(p => string.Equals(p.Origem, "MANUAL", StringComparison.OrdinalIgnoreCase));
        bool temBalanca = pesagens.Any(p => string.Equals(p.Origem, "BALANCA", StringComparison.OrdinalIgnoreCase));

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
            : ObterPesagens(ChaveItemOrdem(_ordemSelecionada)).Sum(p => p.PesoLiquidoKg);
        weightSummaryTitleLabel.Text = "INFORMAÇÃO DE PESAGEM";
        weightSummarySubtitleLabel.Text = $"Previsto: {FormatarKg(saldo)} | Utilizado: {FormatarKg(utilizado)} | Restante: {FormatarKg(Math.Max(saldo - utilizado, 0m))}";
        apontamentoInfoCaptionLabel.Text = "Peso bruto / tara / líquido";
        apontamentoInfoValueLabel.Text = _ordemSelecionada is null
            ? "Bruto: 0,000 KG | Tara: 0,000 KG | Líquido: 0,000 KG"
            : MontarResumoUltimaPesagem();
        apontamentoChipCaptionLabel.Text = "OP";
        apontamentoChipValueLabel.Text = _ordemSelecionada?.NumeroOrdem ?? "-";
    }

    private string MontarResumoUltimaPesagem()
    {
        if (_ordemSelecionada is null)
        {
            return "Bruto: 0,000 KG | Tara: 0,000 KG | Líquido: 0,000 KG";
        }

        PesagemSemiAcabado? ultima = ObterPesagens(ChaveItemOrdem(_ordemSelecionada)).LastOrDefault();
        return ultima is null
            ? "Bruto: 0,000 KG | Tara: 0,000 KG | Líquido: 0,000 KG"
            : $"Bruto: {FormatarKg(ultima.PesoBrutoKg)} | Tara: {FormatarKg(ultima.PesoTaraKg)} | Líquido: {FormatarKg(ultima.PesoLiquidoKg)}";
    }

    private void AtualizarContadores()
    {
        decimal saldoTotal = _itensOrdem.Sum(i => i.QuantidadePendente);
        int quantidadePesagens = _pesagensPorItemOrdem.Values.Sum(lista => lista.Count);
        boxesCounterLabel.Text = saldoTotal.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
        packagesCounterLabel.Text = quantidadePesagens.ToString("000", CultureInfo.InvariantCulture);
        productionFooterLabel.Text = $"{productionDataGridView.Rows.Count} item(ns) de OP carregado(s).";
    }

    private void AtualizarEstadoLeitura(bool iniciada)
    {
        _leituraIniciada = iniciada;

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

        statusCardIcon.Text = iniciada ? "✓" : "!";
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
        bool possuiPesagem = _ordemSelecionada is not null
            && ObterPesagens(ChaveItemOrdem(_ordemSelecionada)).Count > 0;

        // Tarefa 20.5 (Ajuste 4): iniciar/parar no mesmo padrao visual da Entrada
        // (cinza desabilitado sem OP; verde com OP; vermelho durante a leitura).
        bool podeAlternarLeitura = livre && _ordemSelecionada is not null;
        iniciarLeituraButton.BaseBackColor = _leituraIniciada
            ? Color.FromArgb(212, 37, 49)
            : podeAlternarLeitura ? Color.FromArgb(34, 166, 82) : Color.FromArgb(156, 163, 175);
        iniciarLeituraButton.BaseForeColor = Color.White;
        iniciarLeituraButton.IconFontFamily = "Segoe MDL2 Assets";
        iniciarLeituraButton.IconGlyph = _leituraIniciada ? "" : "";
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
            && !_semiAcabadoConfirmadoNaSessao; // Tarefa 20.4: some apos confirmar na sessao
        productionActionsButton.Enabled = productionActionsButton.Visible && livre;
    }

    private void LimparOpCarregada()
    {
        _semiAcabadoConfirmadoNaSessao = false; // Tarefa 20.4: nova/limpa OP libera novo lancamento
        _ordemSelecionada = null;
        _itensOrdem = [];
        _pesagensPorItemOrdem.Clear();
        _tarasPorItemOrdem.Clear();
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

    private async Task<bool> BloquearAcaoSemPermissaoAsync(string acao, string descricaoAcao)
    {
        // TODO Permissões:
        // Criar permissão específica para Produto Semi-Acabado quando a matriz de permissões for atualizada.
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

        string mensagem = "Usuário sem permissão para executar produto semi-acabado.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private static bool ValidarSaldoSemiAcabado(decimal pesoLiquidoTotalKg, decimal saldoPendenteKg, out string mensagem)
    {
        if (saldoPendenteKg > 0m && pesoLiquidoTotalKg > saldoPendenteKg)
        {
            mensagem = $"Peso líquido informado ({pesoLiquidoTotalKg:0.###} KG) ultrapassa o saldo previsto do semi-acabado ({saldoPendenteKg:0.###} KG).";
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
        => string.IsNullOrWhiteSpace(ordem.ItemOrdem)
            ? ordem.MaterialProduzido
            : ordem.ItemOrdem;

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

    private async void ProcessoSemiAcabadoForm_KeyDown(object? sender, KeyEventArgs e)
    {
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
