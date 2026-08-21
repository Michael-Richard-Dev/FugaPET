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
    private readonly List<EntradaProdutoPesagemEmMemoria> _pesagensCanonicas = [];
    private readonly CultureInfo _cultura = CultureInfo.GetCultureInfo("pt-BR");
    private readonly bool _modoCanonico;
    private readonly Func<decimal, string, string, Task<EntradaProdutoPesagemEmMemoria>>? _registrarPesagemCanonicaAsync;
    private readonly Func<Guid, Task<EntradaProdutoPesagemEmMemoria>>? _cancelarPesagemCanonicaAsync;

    // Impressão por pesagem individual (regra definitiva). Callbacks injetados pelo formulário pai — a janela
    // não fala com banco/impressora diretamente. Devolvem true se a etiqueta foi impressa.
    private readonly Func<EntradaProdutoPesagem, Task<bool>>? _imprimirPesagemAsync;
    private readonly Func<EntradaProdutoPesagem, Task<bool>>? _reimprimirPesagemAsync;

    // Modo somente consulta/reimpressão (lançamento já persistido): bloqueia incluir/cancelar.
    private readonly bool _somenteConsulta;

    // Índices das pesagens que já dispararam impressão automática nesta sessão (para a mensagem de cancelamento).
    private readonly HashSet<int> _pesagensImpressas = [];

    public IReadOnlyList<EntradaProdutoPesagem> Pesagens => _modoCanonico
        ? _pesagens.Select(p => p with { }).ToList()
        : EntradaProdutoPesagemCalculos.ValidarSequencias(_pesagens);
    public IReadOnlyList<EntradaProdutoPesagemEmMemoria> PesagensComCodigoLocal => _pesagensCanonicas.AsReadOnly();
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

    public PesagemMultiplaItemForm(
        BalancaLeituraServico balancaLeituraServico,
        string itemPedido,
        TaraCadastro tara,
        long? codigoBalanca,
        IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagensAtuais,
        Func<decimal, string, string, Task<EntradaProdutoPesagemEmMemoria>> registrarPesagemAsync,
        Func<Guid, Task<EntradaProdutoPesagemEmMemoria>> cancelarPesagemAsync,
        Func<EntradaProdutoPesagem, Task<bool>>? imprimirPesagemAsync = null,
        Func<EntradaProdutoPesagem, Task<bool>>? reimprimirPesagemAsync = null)
        : this(
            balancaLeituraServico,
            itemPedido,
            tara,
            codigoBalanca,
            (pesagensAtuais ?? throw new ArgumentNullException(nameof(pesagensAtuais)))
                .OrderBy(p => p.Pesagem.Sequencia)
                .Select(p => p.Pesagem)
                .ToList(),
            imprimirPesagemAsync,
            reimprimirPesagemAsync)
    {
        _modoCanonico = true;
        _registrarPesagemCanonicaAsync = registrarPesagemAsync ?? throw new ArgumentNullException(nameof(registrarPesagemAsync));
        _cancelarPesagemCanonicaAsync = cancelarPesagemAsync ?? throw new ArgumentNullException(nameof(cancelarPesagemAsync));
        _pesagensCanonicas.AddRange(pesagensAtuais
            .OrderBy(p => p.Pesagem.Sequencia)
            .Select(ValidarPesagemCanonica));
        SincronizarPesagensLegadasComCanonicas();
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
        _removerButton.Click += async (_, _) => await CancelarPesoSelecionadoAsync();
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

    private async Task<bool> AdicionarPesoManualAsync()
    {
        string leituraOriginal = _pesoManualTextBox.Text;
        if (!TryParsePeso(leituraOriginal, out decimal peso))
        {
            _statusLabel.Text = "Informe um peso manual válido.";
            ManterPesoManualParaCorrecao(leituraOriginal);
            return false;
        }

        bool adicionado = await AdicionarPesoAsync(peso, "MANUAL", leituraOriginal);
        if (adicionado)
        {
            _pesoManualTextBox.Clear();
            _pesoManualTextBox.Focus();
            return true;
        }

        ManterPesoManualParaCorrecao(leituraOriginal);
        return false;
    }

    private async Task<bool> AdicionarPesoAsync(decimal peso, string origem, string leituraOriginal)
    {
        if (_somenteConsulta)
        {
            _statusLabel.Text = "Lançamento já finalizado: pesagens em modo somente consulta/reimpressão.";
            return false;
        }

        if (_modoCanonico)
        {
            return await AdicionarPesoCanonicoAsync(peso, origem, leituraOriginal);
        }

        decimal pesoLiquido = peso - _tara.PesoKg;
        if (peso <= 0m || pesoLiquido <= 0m)
        {
            _statusLabel.Text = "O peso bruto deve ser maior que a tara.";
            return false;
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
            return true;
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

        return true;
    }

    private async Task<bool> AdicionarPesoCanonicoAsync(decimal peso, string origem, string leituraOriginal)
    {
        if (peso <= 0m)
        {
            _statusLabel.Text = "Informe um peso bruto maior que zero.";
            return false;
        }

        if (_registrarPesagemCanonicaAsync is null)
        {
            _statusLabel.Text = "Registro canônico de pesagem não configurado.";
            return false;
        }

        EntradaProdutoPesagemEmMemoria pesagemCanonica;
        try
        {
            pesagemCanonica = ValidarPesagemCanonica(await _registrarPesagemCanonicaAsync(peso, origem, leituraOriginal));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Não foi possível registrar a pesagem: {ex.Message}";
            return false;
        }

        if (!string.Equals(pesagemCanonica.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusValida, StringComparison.OrdinalIgnoreCase))
        {
            _statusLabel.Text = "O registro retornou uma pesagem que não está válida.";
            return false;
        }

        if (_pesagensCanonicas.Any(p => p.CodigoLocalPesagem == pesagemCanonica.CodigoLocalPesagem))
        {
            _statusLabel.Text = "O registro retornou um identificador de pesagem já existente.";
            return false;
        }

        if (_pesagensCanonicas.Any(p => p.Pesagem.Sequencia == pesagemCanonica.Pesagem.Sequencia))
        {
            _statusLabel.Text = "O registro retornou uma sequência de pesagem já existente.";
            return false;
        }

        _pesagensCanonicas.Add(pesagemCanonica);
        OrdenarPesagensCanonicas();
        SincronizarPesagensLegadasComCanonicas();
        RecarregarGrid();
        AtualizarResumo();

        if (_imprimirPesagemAsync is null)
        {
            _statusLabel.Text = $"Pesagem líquida {FormatarPeso(pesagemCanonica.Pesagem.PesoLiquidoKg)} kg adicionada.";
            return true;
        }

        bool impressa;
        try
        {
            impressa = await _imprimirPesagemAsync(pesagemCanonica.Pesagem);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Pesagem registrada, mas ocorreu uma falha ao imprimir a etiqueta: {ex.Message}";
            return true;
        }

        int indice = _pesagensCanonicas.FindIndex(p => p.CodigoLocalPesagem == pesagemCanonica.CodigoLocalPesagem);
        if (impressa && indice >= 0)
        {
            _pesagensImpressas.Add(indice);
            _statusLabel.Text = $"Pesagem {pesagemCanonica.Pesagem.Sequencia} — {FormatarPeso(pesagemCanonica.Pesagem.PesoLiquidoKg)} kg: etiqueta impressa.";
        }
        else if (!impressa)
        {
            _statusLabel.Text =
                "Pesagem registrada, mas a etiqueta não foi impressa. Dê dois cliques na pesagem para reimprimir após corrigir a impressora.";
        }

        return true;
    }

    private void ManterPesoManualParaCorrecao(string leituraOriginal)
    {
        _pesoManualTextBox.Text = leituraOriginal;
        _pesoManualTextBox.SelectAll();
        _pesoManualTextBox.Focus();
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

    private async Task CancelarPesoSelecionadoAsync()
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

        if (_modoCanonico)
        {
            await CancelarPesoCanonicoAsync(row.Index);
            return;
        }

        // Marca como CANCELADA (retira do total, preserva o histórico) — não tenta "desimprimir" a etiqueta física.
        _pesagens[row.Index] = _pesagens[row.Index] with { StatusPesagem = "CANCELADA" };
        RecarregarGrid();
        AtualizarResumo();
        _statusLabel.Text = "Pesagem marcada como cancelada. Descarte fisicamente a etiqueta, se impressa.";
    }

    private async Task CancelarPesoCanonicoAsync(int indice)
    {
        if (_cancelarPesagemCanonicaAsync is null || indice < 0 || indice >= _pesagensCanonicas.Count)
        {
            _statusLabel.Text = "Cancelamento canônico de pesagem não configurado.";
            return;
        }

        EntradaProdutoPesagemEmMemoria original = _pesagensCanonicas[indice];
        Guid codigoSolicitado = original.CodigoLocalPesagem;
        EntradaProdutoPesagemEmMemoria cancelada;
        try
        {
            cancelada = ValidarPesagemCanonica(await _cancelarPesagemCanonicaAsync(codigoSolicitado));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Não foi possível cancelar a pesagem: {ex.Message}";
            return;
        }

        if (cancelada.CodigoLocalPesagem != codigoSolicitado)
        {
            _statusLabel.Text = "O cancelamento retornou uma pesagem diferente da solicitada.";
            return;
        }

        if (!ValidarRetornoCancelamentoCanonico(original, cancelada))
        {
            _statusLabel.Text = "O cancelamento retornou uma pesagem com dados divergentes do contrato canônico.";
            return;
        }

        int indiceAtual = _pesagensCanonicas.FindIndex(p => p.CodigoLocalPesagem == codigoSolicitado);
        if (indiceAtual < 0)
        {
            _statusLabel.Text = "Pesagem cancelada não pertence à lista atual.";
            return;
        }

        _pesagensCanonicas[indiceAtual] = cancelada;
        OrdenarPesagensCanonicas();
        SincronizarPesagensLegadasComCanonicas();
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
            int sequenciaExibida = _modoCanonico
                ? pesagem.Sequencia
                : index + 1;
            _pesagensGrid.Rows.Add(
                sequenciaExibida,
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

    private void SincronizarPesagensLegadasComCanonicas()
    {
        _pesagens.Clear();
        _pesagens.AddRange(_pesagensCanonicas
            .OrderBy(p => p.Pesagem.Sequencia)
            .Select(p => p.Pesagem));
    }

    private void OrdenarPesagensCanonicas()
        => _pesagensCanonicas.Sort((a, b) => a.Pesagem.Sequencia.CompareTo(b.Pesagem.Sequencia));

    private static EntradaProdutoPesagemEmMemoria ValidarPesagemCanonica(EntradaProdutoPesagemEmMemoria pesagem)
    {
        ArgumentNullException.ThrowIfNull(pesagem);
        if (pesagem.CodigoLocalPesagem == Guid.Empty)
        {
            throw new InvalidOperationException("Pesagem canônica retornou identificador local vazio.");
        }

        return pesagem;
    }

    private static bool ValidarRetornoCancelamentoCanonico(
        EntradaProdutoPesagemEmMemoria original,
        EntradaProdutoPesagemEmMemoria retornada)
        => retornada.CodigoLocalPesagem == original.CodigoLocalPesagem
            && retornada.Pesagem.Sequencia == original.Pesagem.Sequencia
            && retornada.Pesagem.PesoBrutoKg == original.Pesagem.PesoBrutoKg
            && retornada.Pesagem.PesoTaraKg == original.Pesagem.PesoTaraKg
            && retornada.Pesagem.PesoLiquidoKg == original.Pesagem.PesoLiquidoKg
            && retornada.Pesagem.CodigoTara == original.Pesagem.CodigoTara
            && retornada.Pesagem.CodigoBalanca == original.Pesagem.CodigoBalanca
            && string.Equals(retornada.Pesagem.Origem, original.Pesagem.Origem, StringComparison.Ordinal)
            && string.Equals(retornada.Pesagem.LeituraOriginal, original.Pesagem.LeituraOriginal, StringComparison.Ordinal)
            && retornada.Pesagem.PesadoEm == original.Pesagem.PesadoEm
            && string.Equals(retornada.Pesagem.StatusPesagem, EntradaProdutoPesagemCalculos.StatusCancelada, StringComparison.OrdinalIgnoreCase);

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
            bool adicionada = await AdicionarPesoManualAsync();
            if (!adicionada)
            {
                return;
            }
        }

        // Concluir NÃO imprime etiqueta consolidada ? cada pesagem já imprimiu individualmente.
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
