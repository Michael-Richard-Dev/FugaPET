using FugaPET_Dev.Servicos.Operacao;
using System.Globalization;

namespace FugaPET_Dev.Tela.Processo;

public sealed class PesagemMultiplaItemForm : Form
{
    private readonly BalancaLeituraServico _balancaLeituraServico;
    private readonly DataGridView _pesagensGrid = new();
    private readonly TextBox _pesoManualTextBox = new();
    private readonly Label _totalValueLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _lerBalancaButton = new();
    private readonly Button _adicionarManualButton = new();
    private readonly Button _removerButton = new();
    private readonly Button _concluirButton = new();
    private readonly Button _cancelarButton = new();
    private readonly List<decimal> _pesos = new();
    private readonly CultureInfo _cultura = CultureInfo.GetCultureInfo("pt-BR");

    public decimal PesoTotal => _pesos.Sum();
    public string PesoTotalTexto => FormatarPeso(PesoTotal);

    public PesagemMultiplaItemForm(BalancaLeituraServico balancaLeituraServico, string itemPedido, string taraResumo, string pesoAtual)
    {
        _balancaLeituraServico = balancaLeituraServico;

        Text = "Pesagens do Item";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(760, 520);
        BackColor = Color.FromArgb(247, 248, 250);

        Label tituloLabel = new()
        {
            Text = "Pesagens do item selecionado",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 18),
            Size = new Size(520, 30)
        };

        Label itemLabel = new()
        {
            Text = $"Item: {itemPedido}",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(26, 50),
            Size = new Size(680, 22)
        };

        Label taraLabel = new()
        {
            Text = taraResumo,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(34, 166, 82),
            Location = new Point(26, 74),
            Size = new Size(680, 22)
        };

        ConfigurarGrid();
        ConfigurarEntradaManual();
        ConfigurarBotoes();

        _statusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _statusLabel.ForeColor = Color.FromArgb(184, 18, 32);
        _statusLabel.Location = new Point(24, 404);
        _statusLabel.Size = new Size(470, 24);

        _totalValueLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        _totalValueLabel.ForeColor = Color.FromArgb(184, 18, 32);
        _totalValueLabel.TextAlign = ContentAlignment.MiddleRight;
        _totalValueLabel.Location = new Point(520, 390);
        _totalValueLabel.Size = new Size(210, 44);

        Controls.Add(tituloLabel);
        Controls.Add(itemLabel);
        Controls.Add(taraLabel);
        Controls.Add(_pesagensGrid);
        Controls.Add(_pesoManualTextBox);
        Controls.Add(_lerBalancaButton);
        Controls.Add(_adicionarManualButton);
        Controls.Add(_removerButton);
        Controls.Add(_statusLabel);
        Controls.Add(_totalValueLabel);
        Controls.Add(_concluirButton);
        Controls.Add(_cancelarButton);

        CarregarPesoAtual(pesoAtual);
        AtualizarResumo();
    }

    private void ConfigurarGrid()
    {
        _pesagensGrid.Location = new Point(24, 110);
        _pesagensGrid.Size = new Size(706, 250);
        _pesagensGrid.AllowUserToAddRows = false;
        _pesagensGrid.AllowUserToDeleteRows = false;
        _pesagensGrid.AllowUserToResizeRows = false;
        _pesagensGrid.BackgroundColor = Color.White;
        _pesagensGrid.BorderStyle = BorderStyle.FixedSingle;
        _pesagensGrid.ColumnHeadersHeight = 30;
        _pesagensGrid.EnableHeadersVisualStyles = false;
        _pesagensGrid.MultiSelect = false;
        _pesagensGrid.ReadOnly = true;
        _pesagensGrid.RowHeadersVisible = false;
        _pesagensGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _pesagensGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "sequenciaColumn", HeaderText = "#", FillWeight = 12 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pesoColumn", HeaderText = "Peso", FillWeight = 28 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "origemColumn", HeaderText = "Origem", FillWeight = 30 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "horaColumn", HeaderText = "Hora", FillWeight = 30 });
    }

    private void ConfigurarEntradaManual()
    {
        _pesoManualTextBox.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _pesoManualTextBox.Location = new Point(24, 372);
        _pesoManualTextBox.Size = new Size(130, 27);
        _pesoManualTextBox.TextAlign = HorizontalAlignment.Center;
        _pesoManualTextBox.PlaceholderText = "Peso";
    }

    private void ConfigurarBotoes()
    {
        ConfigurarBotao(_lerBalancaButton, "Ler balança", Color.FromArgb(34, 166, 82), Color.White, new Point(170, 370), new Size(112, 32));
        ConfigurarBotao(_adicionarManualButton, "Adicionar", Color.FromArgb(45, 49, 56), Color.White, new Point(294, 370), new Size(104, 32));
        ConfigurarBotao(_removerButton, "Remover", Color.White, Color.FromArgb(45, 49, 56), new Point(410, 370), new Size(94, 32));
        ConfigurarBotao(_concluirButton, "Concluir", Color.FromArgb(184, 18, 32), Color.White, new Point(500, 454), new Size(110, 36));
        ConfigurarBotao(_cancelarButton, "Cancelar", Color.White, Color.FromArgb(45, 49, 56), new Point(620, 454), new Size(110, 36));

        _lerBalancaButton.Click += async (_, _) => await LerBalancaAsync();
        _adicionarManualButton.Click += (_, _) => AdicionarPesoManual();
        _removerButton.Click += (_, _) => RemoverPesoSelecionado();
        _concluirButton.Click += (_, _) => Concluir();
        _cancelarButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _concluirButton;
        CancelButton = _cancelarButton;
    }

    private static void ConfigurarBotao(Button button, string text, Color backColor, Color foreColor, Point location, Size size)
    {
        button.Text = text;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatStyle = FlatStyle.Flat;
        button.Location = location;
        button.Size = size;
    }

    private void CarregarPesoAtual(string pesoAtual)
    {
        if (TryParsePeso(pesoAtual, out decimal peso) && peso > 0)
        {
            AdicionarPeso(peso, "ANTERIOR");
        }
    }

    private async Task LerBalancaAsync()
    {
        _lerBalancaButton.Enabled = false;
        _statusLabel.Text = "Lendo peso da balança...";

        try
        {
            ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
            if (!leitura.Sucesso)
            {
                _statusLabel.Text = leitura.Mensagem;
                return;
            }

            if (!TryParsePeso(leitura.Peso, out decimal peso) || peso <= 0)
            {
                _statusLabel.Text = "Peso lido inválido.";
                return;
            }

            AdicionarPeso(peso, "BALANÇA");
            _statusLabel.Text = $"Peso {FormatarPeso(peso)} adicionado.";
        }
        finally
        {
            _lerBalancaButton.Enabled = true;
        }
    }

    private void AdicionarPesoManual()
    {
        if (!TryParsePeso(_pesoManualTextBox.Text, out decimal peso) || peso <= 0)
        {
            _statusLabel.Text = "Informe um peso manual válido.";
            return;
        }

        AdicionarPeso(peso, "MANUAL");
        _pesoManualTextBox.Clear();
        _pesoManualTextBox.Focus();
        _statusLabel.Text = $"Peso {FormatarPeso(peso)} adicionado.";
    }

    private void AdicionarPeso(decimal peso, string origem)
    {
        _pesos.Add(peso);
        _pesagensGrid.Rows.Add(_pesos.Count, FormatarPeso(peso), origem, DateTime.Now.ToString("HH:mm:ss", _cultura));
        AtualizarResumo();
    }

    private void RemoverPesoSelecionado()
    {
        DataGridViewRow? row = _pesagensGrid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault();
        if (row is null)
        {
            _statusLabel.Text = "Selecione uma pesagem para remover.";
            return;
        }

        int index = row.Index;
        if (index < 0 || index >= _pesos.Count)
        {
            return;
        }

        _pesos.RemoveAt(index);
        RecarregarGrid();
        AtualizarResumo();
        _statusLabel.Text = "Pesagem removida.";
    }

    private void RecarregarGrid()
    {
        _pesagensGrid.Rows.Clear();
        for (int index = 0; index < _pesos.Count; index++)
        {
            _pesagensGrid.Rows.Add(index + 1, FormatarPeso(_pesos[index]), "AJUSTADO", DateTime.Now.ToString("HH:mm:ss", _cultura));
        }
    }

    private void AtualizarResumo()
    {
        _totalValueLabel.Text = $"Total: {PesoTotalTexto}";
        _concluirButton.Enabled = _pesos.Count > 0;
    }

    private void Concluir()
    {
        if (!string.IsNullOrWhiteSpace(_pesoManualTextBox.Text))
        {
            if (!TryParsePeso(_pesoManualTextBox.Text, out decimal pesoPendente) || pesoPendente <= 0)
            {
                _statusLabel.Text = "Informe um peso manual válido antes de concluir.";
                return;
            }

            AdicionarPeso(pesoPendente, "MANUAL");
            _pesoManualTextBox.Clear();
        }

        if (_pesos.Count == 0)
        {
            _statusLabel.Text = "Adicione ao menos uma pesagem.";
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryParsePeso(string texto, out decimal peso)
    {
        peso = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string limpo = new string(texto.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
        if (string.IsNullOrWhiteSpace(limpo))
        {
            return false;
        }

        limpo = limpo.Replace(',', '.');
        return decimal.TryParse(limpo, NumberStyles.Any, CultureInfo.InvariantCulture, out peso);
    }

    private string FormatarPeso(decimal peso)
        => peso.ToString("0.###", _cultura);
}
