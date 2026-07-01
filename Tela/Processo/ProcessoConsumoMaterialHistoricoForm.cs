using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tela.Processo;

public partial class ProcessoConsumoMaterialHistoricoForm : Form
{
    private readonly ProcessoConsumoMaterialHistoricoController _controller = new();
    private IReadOnlyList<ResumoConsumoMaterialLancamento> _lancamentos = [];

    public ProcessoConsumoMaterialHistoricoForm()
    {
        InitializeComponent();
        ConfigurarTela();
    }

    private void ConfigurarTela()
    {
        statusComboBox.Items.AddRange(
        [
            "Todos",
            "PENDENTE_SAP",
            "ENVIANDO_SAP",
            "FALHA_SAP",
            "CONFIRMADO_SAP",
            "CANCELADO_LOCAL"
        ]);
        statusComboBox.SelectedIndex = 0;
        dataInicialPicker.Value = DateTime.Today.AddDays(-7);
        dataFinalPicker.Value = DateTime.Today;

        ConfigurarGridLancamentos();
        ConfigurarGridItens();
        ConfigurarGridPesagens();

        pesquisarButton.Click += async (_, _) => await CarregarLancamentosAsync();
        atualizarButton.Click += async (_, _) => await CarregarLancamentosAsync();
        fecharButton.Click += (_, _) => Close();
        visualizarPayloadButton.Click += async (_, _) => await VisualizarPayloadSap261Async();
        lancamentosGridView.SelectionChanged += async (_, _) => await CarregarDetalheSelecionadoAsync();
        Shown += async (_, _) => await CarregarLancamentosAsync();
    }

    private void ConfigurarGridLancamentos()
    {
        lancamentosGridView.Columns.Clear();
        lancamentosGridView.Columns.Add("CodigoLancamento", "Código");
        lancamentosGridView.Columns.Add("NumeroOrdem", "OP");
        lancamentosGridView.Columns.Add("Centro", "Centro");
        lancamentosGridView.Columns.Add("MaterialProduzido", "Material produzido");
        lancamentosGridView.Columns.Add("StatusLancamento", "Status");
        lancamentosGridView.Columns.Add("QuantidadeTotalConsumidaLocal", "Quantidade total");
        lancamentosGridView.Columns.Add("Unidade", "Unidade");
        lancamentosGridView.Columns.Add("DocumentoMaterialSap", "Documento SAP");
        lancamentosGridView.Columns.Add("ExercicioDocumentoMaterialSap", "Ano");
        lancamentosGridView.Columns.Add("CriadoEmUtc", "Criado em");
        lancamentosGridView.Columns.Add("EnviadoSapEmUtc", "Enviado SAP em");
    }

    private void ConfigurarGridItens()
    {
        itensGridView.Columns.Clear();
        itensGridView.Columns.Add("CodigoMaterial", "Material");
        itensGridView.Columns.Add("DescricaoMaterial", "Descrição");
        itensGridView.Columns.Add("DepositoConsumo", "Depósito");
        itensGridView.Columns.Add("NumeroReserva", "Reserva");
        itensGridView.Columns.Add("ItemReserva", "Item reserva");
        itensGridView.Columns.Add("Lote", "Lote");
        itensGridView.Columns.Add("QuantidadeConsumidaLocal", "Quantidade consumida");
        itensGridView.Columns.Add("Unidade", "Unidade");
        itensGridView.Columns.Add("StatusItem", "Status item");
    }

    private void ConfigurarGridPesagens()
    {
        pesagensGridView.Columns.Clear();
        pesagensGridView.Columns.Add("Sequencia", "Sequência");
        pesagensGridView.Columns.Add("PesoBrutoKg", "Peso bruto");
        pesagensGridView.Columns.Add("PesoTaraKg", "Tara");
        pesagensGridView.Columns.Add("PesoLiquidoKg", "Peso líquido");
        pesagensGridView.Columns.Add("Origem", "Origem");
        pesagensGridView.Columns.Add("StatusPesagem", "Status");
        pesagensGridView.Columns.Add("PesadoEm", "Pesado em");
    }

    private async Task CarregarLancamentosAsync()
    {
        try
        {
            mensagemLabel.Text = "Consultando lançamentos...";
            ConsultaConsumoMaterialFiltro filtro = MontarFiltro();
            _lancamentos = await _controller.ConsultarLancamentosAsync(filtro);
            PreencherLancamentos(_lancamentos);
            mensagemLabel.Text = _lancamentos.Count == 0
                ? "Nenhum lançamento encontrado para os filtros informados."
                : $"{_lancamentos.Count} lançamento(s) encontrado(s).";
        }
        catch (Exception ex)
        {
            mensagemLabel.Text = "Não foi possível consultar o histórico de consumo.";
            MessageBox.Show(
                $"Não foi possível consultar o histórico de consumo. Detalhe: {ex.Message}",
                "Histórico de Consumo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private ConsultaConsumoMaterialFiltro MontarFiltro()
    {
        DateTime criadoDeLocal = dataInicialPicker.Value.Date;
        DateTime criadoAteLocal = dataFinalPicker.Value.Date.AddDays(1).AddTicks(-1);
        return new ConsultaConsumoMaterialFiltro
        {
            NumeroOrdem = ordemTextBox.Text,
            StatusLancamento = statusComboBox.SelectedItem?.ToString(),
            CriadoDeUtc = criadoDeLocal.ToUniversalTime(),
            CriadoAteUtc = criadoAteLocal.ToUniversalTime(),
            Limite = ConsumoMaterialConsultaServico.LimitePadrao
        };
    }

    private void PreencherLancamentos(IReadOnlyList<ResumoConsumoMaterialLancamento> lancamentos)
    {
        lancamentosGridView.Rows.Clear();
        itensGridView.Rows.Clear();
        pesagensGridView.Rows.Clear();
        foreach (ResumoConsumoMaterialLancamento lancamento in lancamentos)
        {
            int index = lancamentosGridView.Rows.Add(
                lancamento.CodigoLancamento,
                lancamento.NumeroOrdem,
                lancamento.Centro ?? string.Empty,
                lancamento.MaterialProduzido ?? string.Empty,
                lancamento.StatusLancamento,
                lancamento.QuantidadeTotalConsumidaLocal,
                lancamento.Unidade ?? string.Empty,
                lancamento.DocumentoMaterialSap ?? string.Empty,
                lancamento.ExercicioDocumentoMaterialSap ?? string.Empty,
                FormatarData(lancamento.CriadoEmUtc),
                FormatarData(lancamento.EnviadoSapEmUtc));
            lancamentosGridView.Rows[index].Tag = lancamento;
        }
    }

    private async Task CarregarDetalheSelecionadoAsync()
    {
        if (lancamentosGridView.SelectedRows.Count == 0 ||
            lancamentosGridView.SelectedRows[0].Tag is not ResumoConsumoMaterialLancamento resumo)
        {
            return;
        }

        DetalheConsumoMaterialLancamento? detalhe =
            await _controller.ObterDetalheCompletoAsync(resumo.CodigoLancamento);
        if (detalhe is null)
        {
            itensGridView.Rows.Clear();
            pesagensGridView.Rows.Clear();
            mensagemLabel.Text = "Detalhe do lançamento não encontrado.";
            return;
        }

        PreencherItens(detalhe.Itens);
        PreencherPesagens(detalhe.Pesagens);
        mensagemLabel.Text = $"Lançamento {resumo.CodigoLancamento}: {detalhe.Itens.Count} item(ns), {detalhe.Pesagens.Count} pesagem(ns).";
    }

    private void PreencherItens(IReadOnlyList<ConsumoMaterialItem> itens)
    {
        itensGridView.Rows.Clear();
        foreach (ConsumoMaterialItem item in itens)
        {
            itensGridView.Rows.Add(
                item.CodigoMaterial,
                item.DescricaoMaterial ?? string.Empty,
                item.DepositoConsumo ?? string.Empty,
                item.NumeroReserva ?? string.Empty,
                item.ItemReserva ?? string.Empty,
                item.Lote ?? string.Empty,
                item.QuantidadeConsumidaLocal,
                item.Unidade,
                item.StatusItem);
        }
    }

    private void PreencherPesagens(IReadOnlyList<ConsumoMaterialPesagem> pesagens)
    {
        pesagensGridView.Rows.Clear();
        foreach (ConsumoMaterialPesagem pesagem in pesagens)
        {
            pesagensGridView.Rows.Add(
                pesagem.Sequencia,
                pesagem.PesoBrutoKg,
                pesagem.PesoTaraKg,
                pesagem.PesoLiquidoKg,
                pesagem.Origem,
                pesagem.StatusPesagem,
                FormatarData(pesagem.PesadoEm));
        }
    }

    private async Task VisualizarPayloadSap261Async()
    {
        if (lancamentosGridView.SelectedRows.Count == 0 ||
            lancamentosGridView.SelectedRows[0].Tag is not ResumoConsumoMaterialLancamento resumo)
        {
            MessageBox.Show("Selecione um lançamento pendente para visualizar o preview.", "Preview SAP 261",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!string.Equals(resumo.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal))
        {
            MessageBox.Show(ConsumoMaterialConsultaServico.MensagemPreviewSomentePendente, "Preview SAP 261",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ResultadoPreviewConsumoSap261 preview = await _controller.GerarPreviewSap261Async(resumo.CodigoLancamento);
        if (!preview.Sucesso || string.IsNullOrWhiteSpace(preview.PayloadJson))
        {
            MessageBox.Show(preview.Mensagem, "Preview SAP 261", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string json = preview.PayloadJson;
        using Form dialog = new()
        {
            Text = "Payload SAP 261",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(760, 560),
            MinimizeBox = false,
            MaximizeBox = true
        };
        TextBox textBox = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10F),
            Text = json
        };
        dialog.Controls.Add(textBox);
        dialog.ShowDialog(this);
    }

    private static string FormatarData(DateTime? dataUtc)
        => dataUtc.HasValue
            ? DateTime.SpecifyKind(dataUtc.Value, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : string.Empty;
}
