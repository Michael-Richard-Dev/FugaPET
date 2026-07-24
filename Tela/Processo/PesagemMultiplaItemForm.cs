using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Operacao;
using System.Globalization;

namespace FugaPET_Dev.Tela.Processo;

public sealed class PesagemMultiplaItemForm : Form
{
    private readonly BalancaLeituraServico _balancaLeituraServico;
    private readonly TaraCadastro _tara;
    private readonly long? _codigoBalanca;
    private readonly DataGridView _pesagensGrid = new();
    private readonly TextBox _pesoManualTextBox = new();
    private readonly Label _totalValueLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _lerBalancaButton = new();
    private readonly Button _adicionarManualButton = new();
    private readonly Button _removerButton = new();
    private readonly Button _concluirButton = new();
    private readonly Button _cancelarButton = new();
    private readonly List<EntradaProdutoPesagem> _pesagens = [];
    private readonly CultureInfo _cultura = CultureInfo.GetCultureInfo("pt-BR");

    // Impressão por pesagem individual (regra definitiva). Callbacks injetados pelo formulário pai — a janela
    // não fala com banco/impressora diretamente. Devolvem true se a etiqueta foi impressa.
    private readonly Func<EntradaProdutoPesagem, Task<bool>>? _imprimirPesagemAsync;
    private readonly Func<EntradaProdutoPesagem, Task<bool>>? _reimprimirPesagemAsync;

    // Modo somente consulta/reimpressão (lançamento já persistido): bloqueia incluir/cancelar.
    private readonly bool _somenteConsulta;

    // Índices das pesagens que já dispararam impressão automática nesta sessão (para a mensagem de cancelamento).
    private readonly HashSet<int> _pesagensImpressas = [];

    public IReadOnlyList<EntradaProdutoPesagem> Pesagens =>
        EntradaProdutoPesagemCalculos.ValidarSequencias(_pesagens);
    public decimal PesoTotal => EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(_pesagens);
    public string PesoTotalTexto => FormatarPeso(PesoTotal);

    public PesagemMultiplaItemForm(
        BalancaLeituraServico balancaLeituraServico,
        string itemPedido,
        TaraCadastro tara,
        long? codigoBalanca,
        IReadOnlyList<EntradaProdutoPesagem> pesagensAtuais,
        Func<EntradaProdutoPesagem, Task<bool>>? imprimirPesagemAsync = null,
        Func<EntradaProdutoPesagem, Task<bool>>? reimprimirPesagemAsync = null,
        bool somenteConsulta = false)
    {
        _balancaLeituraServico = balancaLeituraServico;
        _tara = tara;
        _codigoBalanca = codigoBalanca;
        _imprimirPesagemAsync = imprimirPesagemAsync;
        _reimprimirPesagemAsync = reimprimirPesagemAsync;
        _somenteConsulta = somenteConsulta;
        _pesagens.AddRange(pesagensAtuais);

        Text = "Pesagens do Item";
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
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
            Text = $"Tara por leitura: {_tara.NomeTara} ({FormatarPeso(_tara.PesoKg)} kg)",
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
        _totalValueLabel.AutoSize = true;
        _totalValueLabel.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        _totalValueLabel.ForeColor = Color.FromArgb(184, 18, 32);
        _totalValueLabel.TextAlign = ContentAlignment.MiddleRight;
        // Posicao recalculada a cada atualizacao do total (AlinharTotalADireita),
        // para que valores grandes sempre apareçam inteiros, alinhados à direita.
        _totalValueLabel.Location = new Point(520, 392);

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

        RecarregarGrid();
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
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "sequenciaColumn", HeaderText = "#", FillWeight = 8 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "pesoColumn", HeaderText = "Bruto", FillWeight = 16 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "taraColumn", HeaderText = "Tara", FillWeight = 14 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "liquidoColumn", HeaderText = "Líquido", FillWeight = 16 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "origemColumn", HeaderText = "Origem", FillWeight = 16 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "horaColumn", HeaderText = "Hora", FillWeight = 15 });
        _pesagensGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "statusColumn", HeaderText = "Status", FillWeight = 15 });

        // Duplo clique reimprime SOMENTE aquela pesagem (regra definitiva). Permissão é validada no callback do pai.
        _pesagensGrid.CellDoubleClick += async (_, e) => await ReimprimirPesagemAsync(e.RowIndex);
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
        ConfigurarBotao(_removerButton, "Cancelar leitura", Color.White, Color.FromArgb(45, 49, 56), new Point(410, 370), new Size(110, 32));
        ConfigurarBotao(_concluirButton, "Concluir", Color.FromArgb(184, 18, 32), Color.White, new Point(500, 454), new Size(110, 36));
        ConfigurarBotao(_cancelarButton, "Fechar", Color.White, Color.FromArgb(45, 49, 56), new Point(620, 454), new Size(110, 36));

        _lerBalancaButton.Click += async (_, _) => await LerBalancaAsync();
        _adicionarManualButton.Click += async (_, _) => await AdicionarPesoManualAsync();
        _removerButton.Click += (_, _) => CancelarPesoSelecionado();
        _concluirButton.Click += async (_, _) => await ConcluirAsync();
        // "Fechar" preserva as pesagens adicionadas (que já podem ter impresso etiqueta) — retorna OK ao pai,
        // NUNCA descarta silenciosamente. Cancelar uma leitura específica é feito por "Cancelar leitura".
        _cancelarButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        AcceptButton = _concluirButton;

        // Modo somente consulta/reimpressão: bloqueia incluir/cancelar; mantém duplo clique para reimpressão.
        if (_somenteConsulta)
        {
            _lerBalancaButton.Enabled = false;
            _adicionarManualButton.Enabled = false;
            _removerButton.Enabled = false;
            _pesoManualTextBox.Enabled = false;
            _concluirButton.Text = "Fechar";
        }
    }

    private static void ConfigurarBotao(
        Button button,
        string text,
        Color backColor,
        Color foreColor,
        Point location,
        Size size)
    {
        button.Text = text;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatStyle = FlatStyle.Flat;
        button.Location = location;
        button.Size = size;
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
            if (!TryParsePeso(leitura.Peso, out decimal peso))
            {
                _statusLabel.Text = "Peso lido inválido.";
                return;
            }

            await AdicionarPesoAsync(peso, "BALANCA", leitura.Peso);
        }
        finally
        {
            _lerBalancaButton.Enabled = true;
        }
    }

    private async Task AdicionarPesoManualAsync()
    {
        string leituraOriginal = _pesoManualTextBox.Text;
        if (!TryParsePeso(leituraOriginal, out decimal peso))
        {
            _statusLabel.Text = "Informe um peso manual válido.";
            return;
        }

        await AdicionarPesoAsync(peso, "MANUAL", leituraOriginal);
        _pesoManualTextBox.Clear();
        _pesoManualTextBox.Focus();
    }

    private async Task AdicionarPesoAsync(decimal peso, string origem, string leituraOriginal)
    {
        if (_somenteConsulta)
        {
            _statusLabel.Text = "Lançamento já finalizado: pesagens em modo somente consulta/reimpressão.";
            return;
        }

        decimal pesoLiquido = peso - _tara.PesoKg;
        if (peso <= 0m || pesoLiquido <= 0m)
        {
            _statusLabel.Text = "O peso bruto deve ser maior que a tara.";
            return;
        }

        EntradaProdutoPesagem nova = new()
        {
            Sequencia = _pesagens.Count + 1,
            PesoBrutoKg = peso,
            PesoTaraKg = _tara.PesoKg,
            PesoLiquidoKg = pesoLiquido,
            CodigoTara = _tara.CodigoTara,
            CodigoBalanca = _codigoBalanca,
            Origem = origem,
            StatusPesagem = "VALIDA",
            LeituraOriginal = leituraOriginal,
            PesadoEm = DateTimeOffset.Now
        };
        int indice = _pesagens.Count;
        _pesagens.Add(nova);
        RecarregarGrid();
        AtualizarResumo();

        // Regra definitiva: imprime imediatamente SOMENTE esta nova pesagem (peso líquido dela). Falha de
        // impressão NÃO remove a pesagem — ela fica disponível para reimpressão por duplo clique.
        if (_imprimirPesagemAsync is null)
        {
            _statusLabel.Text = $"Pesagem líquida {FormatarPeso(pesoLiquido)} kg adicionada.";
            return;
        }

        bool impressa = await _imprimirPesagemAsync(nova);
        if (impressa)
        {
            _pesagensImpressas.Add(indice);
            _statusLabel.Text = $"Pesagem {nova.Sequencia} — {FormatarPeso(pesoLiquido)} kg: etiqueta impressa.";
        }
        else
        {
            _statusLabel.Text =
                "Pesagem registrada, mas a etiqueta não foi impressa. Dê dois cliques na pesagem para reimprimir após corrigir a impressora.";
        }
    }

    private async Task ReimprimirPesagemAsync(int rowIndex)
    {
        if (_reimprimirPesagemAsync is null || rowIndex < 0 || rowIndex >= _pesagens.Count)
        {
            return;
        }

        EntradaProdutoPesagem pesagem = _pesagens[rowIndex];
        if (!string.Equals(pesagem.StatusPesagem, "VALIDA", StringComparison.OrdinalIgnoreCase))
        {
            _statusLabel.Text = "Só é possível reimprimir pesagens com status VÁLIDA.";
            return;
        }

        bool impressa = await _reimprimirPesagemAsync(pesagem);
        _statusLabel.Text = impressa
            ? $"Etiqueta da pesagem {pesagem.Sequencia} reimpressa com sucesso — {FormatarPeso(pesagem.PesoLiquidoKg)} kg."
            : "Não foi possível reimprimir a etiqueta desta pesagem.";
    }

    private void CancelarPesoSelecionado()
    {
        if (_somenteConsulta)
        {
            _statusLabel.Text = "Lançamento já finalizado: não é possível cancelar pesagens.";
            return;
        }

        DataGridViewRow? row = _pesagensGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .FirstOrDefault();
        if (row is null || row.Index < 0 || row.Index >= _pesagens.Count)
        {
            _statusLabel.Text = "Selecione uma pesagem para cancelar.";
            return;
        }

        // A etiqueta desta pesagem já pode ter sido impressa: confirmar e orientar o descarte físico.
        string aviso = _pesagensImpressas.Contains(row.Index)
            ? "A etiqueta desta pesagem já pode ter sido impressa. Descarte fisicamente a etiqueta cancelada.\n\nConfirma o cancelamento desta leitura?"
            : "Confirma o cancelamento desta leitura?";
        if (MessageBox.Show(aviso, "Cancelar leitura", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        // Marca como CANCELADA (retira do total, preserva o histórico) — não tenta "desimprimir" a etiqueta física.
        _pesagens[row.Index] = _pesagens[row.Index] with { StatusPesagem = "CANCELADA" };
        RecarregarGrid();
        AtualizarResumo();
        _statusLabel.Text = "Pesagem marcada como cancelada. Descarte fisicamente a etiqueta, se impressa.";
    }

    private void RecarregarGrid()
    {
        _pesagensGrid.Rows.Clear();
        for (int index = 0; index < _pesagens.Count; index++)
        {
            EntradaProdutoPesagem pesagem = _pesagens[index];
            _pesagensGrid.Rows.Add(
                index + 1,
                FormatarPeso(pesagem.PesoBrutoKg),
                FormatarPeso(pesagem.PesoTaraKg),
                FormatarPeso(pesagem.PesoLiquidoKg),
                pesagem.Origem,
                pesagem.PesadoEm.ToLocalTime().ToString("HH:mm:ss", _cultura),
                pesagem.StatusPesagem);
        }
    }

    private void AtualizarResumo()
    {
        _totalValueLabel.Text = $"Total bruto: {PesoTotalTexto}";
        AlinharTotalADireita();
        _concluirButton.Enabled =
            EntradaProdutoPesagemCalculos.PossuiLeituraValida(_pesagens);
    }

    // Mantem o total colado na margem direita do dialogo; como o label e AutoSize,
    // a largura acompanha o texto e numeros grandes nao sao mais cortados.
    private void AlinharTotalADireita()
    {
        const int margemDireita = 24;
        _totalValueLabel.Left = Math.Max(
            200,
            ClientSize.Width - margemDireita - _totalValueLabel.PreferredWidth);
    }

    private async Task ConcluirAsync()
    {
        // Em consulta, "Concluir" apenas fecha (preservando).
        if (!_somenteConsulta && !string.IsNullOrWhiteSpace(_pesoManualTextBox.Text))
        {
            await AdicionarPesoManualAsync();
        }

        // Concluir NÃO imprime etiqueta consolidada — cada pesagem já imprimiu individualmente.
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

        string limpo = new string(
            texto.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
        limpo = limpo.Replace(',', '.');
        return decimal.TryParse(
                limpo,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out peso)
            && peso > 0m;
    }

    private string FormatarPeso(decimal peso)
        => peso.ToString("0.###", _cultura);
}
