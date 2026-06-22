using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Tela.Comum;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class BalancaForm : Form
{
    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private static readonly Color MarkerColor = Color.FromArgb(239, 68, 68);

    private readonly List<RowSelection> _rowSelections = new();
    private readonly List<ProfileSearchRow> _profileSearchRows = new();
    private readonly Dictionary<Panel, Panel> _rowSelectionMarkers = new();
    private readonly Dictionary<Panel, RoundedPanel> _statusPanelPorLinha = new();
    private readonly Dictionary<Panel, Label> _statusLabelPorLinha = new();
    private readonly Dictionary<Panel, long> _idBalancaPorLinha = new();
    private readonly Dictionary<Panel, BalancaCadastro> _balancaPorLinha = new();
    private readonly List<BalancaCadastro> _balancasCarregadas = new();
    private readonly ToolTip _toolTipBalanca = new();

    private readonly BalancaController _balancaController;
    private readonly SetorController _setorController;

    private long _idBalancaAtual;

    // Campos extras criados em runtime (sem mexer no Designer).
    private Label? _lblTipoConexao;
    private RoundedPanel? _tipoConexaoInputPanel;
    private ComboBox? _cmbTipoConexao;

    private Label? _lblPortaTcp;
    private RoundedPanel? _portaTcpInputPanel;
    private TextBox? _txtPortaTcp;

    private Label? _lblIdentificacaoLocal;
    private RoundedPanel? _identificacaoLocalInputPanel;
    private TextBox? _txtIdentificacaoLocal;

    private Label? _lblPortaSerial;
    private RoundedPanel? _portaSerialInputPanel;
    private TextBox? _txtPortaSerial;

    private Label? _lblBaudRate;
    private RoundedPanel? _baudRateInputPanel;
    private TextBox? _txtBaudRate;

    private Label? _lblDataBits;
    private RoundedPanel? _dataBitsInputPanel;
    private TextBox? _txtDataBits;

    private Label? _lblParidade;
    private RoundedPanel? _paridadeInputPanel;
    private ComboBox? _cmbParidade;

    private Label? _lblStopBits;
    private RoundedPanel? _stopBitsInputPanel;
    private ComboBox? _cmbStopBits;

    private static readonly string[] TiposConexao = ["SERIAL", "TCP_IP", "USB", "MANUAL"];
    private static readonly string[] Paridades = ["NONE", "EVEN", "ODD", "MARK", "SPACE"];
    private static readonly string[] StopBits = ["1", "1.5", "2"];

    private static readonly Color StatusAtivoFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color StatusAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusInativoFundo = Color.FromArgb(255, 237, 213);
    private static readonly Color StatusInativoTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color StatusNeutroFundo = Color.FromArgb(243, 244, 246);
    private static readonly Color StatusNeutroTexto = Color.FromArgb(100, 116, 139);

    public BalancaForm(BalancaController? balancaController = null, SetorController? setorController = null)
    {
        _balancaController = balancaController ?? FabricaControladoresCadastro.CriarBalancaController();
        _setorController = setorController ?? FabricaControladoresCadastro.CriarSetorController();

        InitializeComponent();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";

        ConfigureWindowButtons();
        ConfigureDragOnTitleBar();
        ConfigureNovoBalancaAction();
        ConfigureProfilesSearchFilter();

        // Reaproveita TxtPeso como "Endereço IP" e descricaoTextBox como "Observação".
        RotularControlesReaproveitados();
        CriarCamposExtrasRuntime();
        ConfigurarLimitesVisuais();
        situacaoComboBox.Enabled = false;

        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        detailsCard.Resize += (_, _) => LayoutDetailsCard();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();

        InitializeProfilesTableSelection();
        LayoutProfilesCard();
        LayoutDetailsCard();
        LayoutSummaryCard();

        AtualizarRodapePerfis();
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        ConectarAcoesCadastro();

        if (_integracaoBancoHabilitada)
        {
            Shown += async (_, _) => await InicializarDadosAsync();
        }
    }

    private void RotularControlesReaproveitados()
    {
        // TxtPeso passa a ser "Endereço IP" — banco da balança nao tem peso, mas precisa de IP.
        LblPeso.Text = "Endereço IP";
        descricaoLabel.Text = "Observação";
    }

    private void ConfigurarLimitesVisuais()
    {
        nomePerfilTextBox.MaxLength = BalancaCadastro.TamanhoMaximoNome;
        descricaoTextBox.MaxLength = BalancaCadastro.TamanhoMaximoObservacao;
        TxtPeso.MaxLength = BalancaCadastro.TamanhoMaximoEnderecoIp;
        searchTextBox.MaxLength = 120;

        if (_txtIdentificacaoLocal is not null)
            _txtIdentificacaoLocal.MaxLength = BalancaCadastro.TamanhoMaximoIdentificacaoLocal;
        if (_txtPortaSerial is not null)
            _txtPortaSerial.MaxLength = BalancaCadastro.TamanhoMaximoPortaSerial;
        if (_txtPortaTcp is not null)
            _txtPortaTcp.MaxLength = 5;
        if (_txtBaudRate is not null)
            _txtBaudRate.MaxLength = 7;
        if (_txtDataBits is not null)
            _txtDataBits.MaxLength = 2;
    }

    private async Task InicializarDadosAsync()
    {
        await PopularComboSetorAsync();
        await CarregarBalancasAsync();
    }

    private async Task PopularComboSetorAsync()
    {
        try
        {
            IReadOnlyList<SetorCadastro> setores = await _setorController.ListarAsync();
            CmbSetor.DisplayMember = "Nome";
            CmbSetor.ValueMember = "Codigo";
            CmbSetor.DataSource = setores.Where(s => s.SituacaoSetor).ToList();
            CmbSetor.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync("BALANCA_CARREGAR_SETORES_ERRO", ex, "BalancaForm",
                    "Não foi possível carregar os setores. Acione o suporte."),
                "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CarregarBalancasAsync()
    {
        try
        {
            IReadOnlyList<BalancaCadastro> balancas = await _balancaController.ListarAsync();
            _balancasCarregadas.Clear();
            _balancasCarregadas.AddRange(balancas);
            RecriarLinhasPerfis();
            PopularLinhasComBalancas(_balancasCarregadas);
            ApplyProfilesFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync("BALANCA_CARREGAR_ERRO", ex, "BalancaForm",
                    "Não foi possível carregar as balanças. Acione o suporte."),
                "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ============================================================
    // CRUD
    // ============================================================

    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) => await SalvarBalancaAsync();
        novoButton.Click += async (_, _) => await EditarBalancaAsync();
        excluirButton.Click += async (_, _) => await AlterarSituacaoBalancaAsync();
    }

    private async Task SalvarBalancaAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        BalancaCadastro balanca = MontarBalancaDoForm();
        balanca.BalancaCriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;

        ResultadoOperacao resultado = await _balancaController.InserirAsync(balanca);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Balanca", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idBalancaAtual = resultado.IdGerado ?? 0;
            PrepareNewBalanca();
            await CarregarBalancasAsync();
        }
    }

    private async Task EditarBalancaAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idBalancaAtual <= 0)
        {
            MessageBox.Show("Selecione uma balanca para editar.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        BalancaCadastro? balancaSelecionada = _balancaPorLinha.Values.FirstOrDefault(x => x.CodigoBalanca == _idBalancaAtual);
        if (balancaSelecionada is null)
        {
            MessageBox.Show("Selecione uma balanca valida para editar.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        BalancaCadastro balanca = MontarBalancaDoForm();
        balanca.CodigoBalanca = _idBalancaAtual;
        balanca.FlowControl = balancaSelecionada.FlowControl;
        balanca.Protocolo = balancaSelecionada.Protocolo;
        balanca.ParametrosTecnicos = balancaSelecionada.ParametrosTecnicos;
        balanca.BalancaAtualizadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;

        ResultadoOperacao resultado = await _balancaController.AtualizarAsync(balanca);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Balanca", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarBalancasAsync();
        }
    }

    private async Task AlterarSituacaoBalancaAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idBalancaAtual <= 0)
        {
            MessageBox.Show("Salve ou selecione uma balanca para inativar.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        BalancaCadastro? balancaSelecionada = _balancaPorLinha.Values
            .FirstOrDefault(item => item.CodigoBalanca == _idBalancaAtual);
        if (balancaSelecionada is null)
        {
            MessageBox.Show("Selecione uma balanca valida.", "Cadastro de Balanca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool inativar = balancaSelecionada.SituacaoBalanca;
        string acao = inativar ? "inativacao" : "reativacao";
        DialogResult confirmacao = MessageBox.Show(
            $"Confirma a {acao} da balanca atual?",
            "Cadastro de Balanca",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes) return;

        ResultadoOperacao resultado = inativar
            ? await _balancaController.ExcluirAsync(_idBalancaAtual)
            : await _balancaController.ReativarAsync(_idBalancaAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Balanca", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idBalancaAtual = 0;
            PrepareNewBalanca();
            await CarregarBalancasAsync();
        }
    }

    private BalancaCadastro MontarBalancaDoForm()
    {
        string tipoConexao = _cmbTipoConexao?.SelectedItem?.ToString() ?? "SERIAL";
        return new BalancaCadastro
        {
            CodigoSetor = TryParseLong(CmbSetor.SelectedValue),
            NomeBalanca = nomePerfilTextBox.Text.Trim(),
            IdentificacaoLocal = _txtIdentificacaoLocal?.Text.Trim() ?? string.Empty,
            EnderecoIp = TxtPeso.Text.Trim(),
            PortaTcp = ParseIntNullable(_txtPortaTcp?.Text),
            PortaSerial = _txtPortaSerial?.Text.Trim() ?? string.Empty,
            TipoConexao = tipoConexao,
            BaudRate = ParseIntNullable(_txtBaudRate?.Text),
            DataBits = ParseIntNullable(_txtDataBits?.Text),
            Paridade = _cmbParidade?.SelectedItem?.ToString() ?? string.Empty,
            StopBits = ParseDecimalNullable(_cmbStopBits?.SelectedItem?.ToString()),
            Observacao = descricaoTextBox.Text.Trim(),
            SituacaoBalanca = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase)
        };
    }

    private void PrepareNewBalanca()
    {
        _idBalancaAtual = 0;
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        TxtPeso.Text = string.Empty;
        situacaoComboBox.Text = "Ativo";
        CmbSetor.SelectedIndex = -1;
        if (_cmbTipoConexao is not null) _cmbTipoConexao.SelectedIndex = 0; // SERIAL por padrao
        if (_txtPortaTcp is not null) _txtPortaTcp.Text = string.Empty;
        if (_txtIdentificacaoLocal is not null) _txtIdentificacaoLocal.Text = string.Empty;
        if (_txtPortaSerial is not null) _txtPortaSerial.Text = string.Empty;
        if (_txtBaudRate is not null) _txtBaudRate.Text = "9600";
        if (_txtDataBits is not null) _txtDataBits.Text = "8";
        if (_cmbParidade is not null) _cmbParidade.SelectedItem = "NONE";
        if (_cmbStopBits is not null) _cmbStopBits.SelectedItem = "1";
        nomePerfilTextBox.ReadOnly = false;
        nomePerfilTextBox.BackColor = Color.White;

        nomePerfilTextBox.Focus();
        AtualizarBotoesAcao(ModoAcaoBotoes.SomenteSalvar);
    }

    private static long TryParseLong(object? value)
    {
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is string s && long.TryParse(s, out long p)) return p;
        return 0;
    }

    private static int? ParseIntNullable(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return int.TryParse(texto.Trim(), out int v) ? v : null;
    }

    private static decimal? ParseDecimalNullable(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        string normalizado = texto.Trim().Replace(',', '.');
        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor)
            ? valor
            : null;
    }

    // ============================================================
    // Linhas dinamicas
    // ============================================================

    private void ConfigureProfilesSearchFilter()
    {
        searchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void RecriarLinhasPerfis()
    {
        RemoverLinhasPerfisExistentes();

        for (int i = 0; i < _balancasCarregadas.Count; i++)
        {
            ProfileSearchRow linha = CriarLinhaPerfil(i);
            _profileSearchRows.Add(linha);
            _rowSelections.Add(new RowSelection(linha.RowPanel, Color.White));

            SetHandCursor(linha.RowPanel);
            AttachRowSelectionHandlers(linha.RowPanel, linha.RowPanel);
            CriarMarcadorLinha(linha.RowPanel);

            profilesTablePanel.Controls.Add(linha.RowPanel);
            linha.RowPanel.BringToFront();
        }

        AtualizarLayoutLinhas();
        HideAllMarkers();
    }

    private void RemoverLinhasPerfisExistentes()
    {
        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            profilesTablePanel.Controls.Remove(row.RowPanel);
            row.RowPanel.Dispose();
        }

        _profileSearchRows.Clear();
        _rowSelections.Clear();
        _rowSelectionMarkers.Clear();
        _statusPanelPorLinha.Clear();
        _statusLabelPorLinha.Clear();
        _idBalancaPorLinha.Clear();
        _balancaPorLinha.Clear();
    }

    private ProfileSearchRow CriarLinhaPerfil(int indice)
    {
        Panel rowPanel = new()
        {
            Name = $"profileDynamicRow{indice + 1}Panel",
            BackColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Left,
            Margin = Padding.Empty,
            Cursor = Cursors.Hand
        };

        Label nameLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}NameLabel",
            AutoSize = false,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Label usersLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}UsersLabel",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "-"
        };

        RoundedPanel statusPanel = new()
        {
            Name = $"profileDynamicRow{indice + 1}StatusPanel",
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };

        Label statusLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}StatusLabel",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        statusPanel.Controls.Add(statusLabel);
        rowPanel.Controls.Add(nameLabel);
        rowPanel.Controls.Add(usersLabel);
        rowPanel.Controls.Add(statusPanel);

        _statusPanelPorLinha[rowPanel] = statusPanel;
        _statusLabelPorLinha[rowPanel] = statusLabel;

        return new ProfileSearchRow(rowPanel, nameLabel, usersLabel, statusLabel);
    }

    private void PopularLinhasComBalancas(IReadOnlyList<BalancaCadastro> balancas)
    {
        _idBalancaPorLinha.Clear();
        _balancaPorLinha.Clear();

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            BalancaCadastro balanca = balancas[i];
            ProfileSearchRow linha = _profileSearchRows[i];

            linha.NameLabel.Text = balanca.NomeBalanca;
            _toolTipBalanca.SetToolTip(linha.NameLabel, $"{balanca.NomeBalanca} ({balanca.TipoConexao})");
            linha.UsersLabel.Text = balanca.TipoConexao;
            linha.StatusLabel.Text = balanca.SituacaoBalanca ? "Ativo" : "Inativo";
            AtualizarBadgeStatus(linha.RowPanel, linha.StatusLabel.Text);
            _idBalancaPorLinha[linha.RowPanel] = balanca.CodigoBalanca;
            _balancaPorLinha[linha.RowPanel] = balanca;
        }
    }

    private void ApplyProfilesFilter()
    {
        string query = NormalizeForSearch(searchTextBox.Text.Trim());
        int visibleIndex = 0;
        float scaleX = profilesCard.Width / 411f;
        float scaleY = profilesCard.Height / 596f;
        int baseTop = Math.Max(0, (int)Math.Round(34 * scaleY));
        int baseLeft = Math.Max(0, (int)Math.Round(3 * scaleX));
        int rowHeight = _profileSearchRows.Count > 0 ? _profileSearchRows[0].RowPanel.Height : Math.Max(40, (int)Math.Round(46 * scaleY));

        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            string rowName = NormalizeForSearch(row.NameLabel.Text);

            bool match = string.IsNullOrWhiteSpace(query)
                || rowName.Contains(query, StringComparison.Ordinal);

            row.RowPanel.Visible = match;

            if (!match) continue;

            int top = baseTop + (rowHeight * visibleIndex);
            row.RowPanel.Location = new Point(baseLeft, top);
            visibleIndex++;
        }

        AtualizarRodapePerfis(visibleIndex);

        if (string.IsNullOrWhiteSpace(query))
        {
            ClearRowSelection();
            return;
        }

        if (visibleIndex == 0) HideAllMarkers();
    }

    private void ClearRowSelection()
    {
        foreach (RowSelection row in _rowSelections)
        {
            row.RowPanel.BackColor = row.NormalBackColor;
        }

        HideAllMarkers();
        ClearSummarySelectionValues();
    }

    private void AtualizarRodapePerfis(int? totalVisivel = null)
    {
        int exibindo = totalVisivel ?? _profileSearchRows.Count(x => x.RowPanel.Visible);
        int total = _balancasCarregadas.Count;
        profilesFooterLabel.Text = $"Exibindo {exibindo} de {total} balancas";
    }

    private void InitializeProfilesTableSelection()
    {
        profilesTablePanel.AutoScroll = true;
        EsconderLinhasFixasDoDesigner();
        _profileSearchRows.Clear();
        _rowSelections.Clear();
        _rowSelectionMarkers.Clear();
    }

    private void EsconderLinhasFixasDoDesigner()
    {
        Panel[] linhasFixas = [profileRow1Panel, profileRow2Panel, profileRow3Panel, profileRow4Panel, profileRow5Panel];
        foreach (Panel linha in linhasFixas) linha.Visible = false;
    }

    private void AttachRowSelectionHandlers(Control control, Panel rowPanel)
    {
        control.Click += (_, _) => SetSelectedRow(rowPanel);
        foreach (Control child in control.Controls) AttachRowSelectionHandlers(child, rowPanel);
    }

    private void SetSelectedRow(Panel selectedRowPanel)
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);

        foreach (RowSelection row in _rowSelections)
        {
            bool isSelected = row.RowPanel == selectedRowPanel;
            row.RowPanel.BackColor = isSelected ? selectedBackColor : row.NormalBackColor;
            if (isSelected) ShowMarkerForRow(row.RowPanel);
            else HideMarkerForRow(row.RowPanel);
        }

        _idBalancaAtual = _idBalancaPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        PreencherCamposBalancaPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
        AtualizarBotoesAcao(ModoAcaoBotoes.EditarExcluir);
    }

    private void PreencherCamposBalancaPorLinha(Panel rowPanel)
    {
        if (!_balancaPorLinha.TryGetValue(rowPanel, out BalancaCadastro? balanca)) return;

        nomePerfilTextBox.Text = balanca.NomeBalanca;
        descricaoTextBox.Text = balanca.Observacao;
        TxtPeso.Text = balanca.EnderecoIp;
        situacaoComboBox.Text = balanca.SituacaoBalanca ? "Ativo" : "Inativo";
        CmbSetor.SelectedValue = balanca.CodigoSetor;
        if (_txtIdentificacaoLocal is not null) _txtIdentificacaoLocal.Text = balanca.IdentificacaoLocal;
        if (_txtPortaTcp is not null) _txtPortaTcp.Text = balanca.PortaTcp?.ToString() ?? string.Empty;
        if (_txtPortaSerial is not null) _txtPortaSerial.Text = balanca.PortaSerial;
        if (_txtBaudRate is not null) _txtBaudRate.Text = balanca.BaudRate?.ToString() ?? string.Empty;
        if (_txtDataBits is not null) _txtDataBits.Text = balanca.DataBits?.ToString() ?? string.Empty;
        if (_cmbParidade is not null)
        {
            string paridade = string.IsNullOrWhiteSpace(balanca.Paridade) ? "NONE" : balanca.Paridade;
            _cmbParidade.SelectedItem = Paridades.Contains(paridade) ? paridade : "NONE";
        }
        if (_cmbStopBits is not null)
        {
            string stopBits = balanca.StopBits?.ToString(CultureInfo.InvariantCulture) ?? "1";
            _cmbStopBits.SelectedItem = StopBits.Contains(stopBits) ? stopBits : "1";
        }
        if (_cmbTipoConexao is not null)
        {
            string tipo = string.IsNullOrWhiteSpace(balanca.TipoConexao) ? "SERIAL" : balanca.TipoConexao;
            int idx = Array.IndexOf(TiposConexao, tipo);
            _cmbTipoConexao.SelectedIndex = idx >= 0 ? idx : 0;
        }

        nomePerfilTextBox.ReadOnly = true;
        nomePerfilTextBox.BackColor = Color.FromArgb(241, 245, 249);
    }

    private void SyncSummaryFromRow(Panel rowPanel)
    {
        ProfileSearchRow? row = _profileSearchRows.FirstOrDefault(x => x.RowPanel == rowPanel);
        if (row is null) { ClearSummarySelectionValues(); return; }

        summaryPerfilValueLabel.Text = row.NameLabel.Text;
        summarySituacaoValueLabel.Text = row.StatusLabel.Text;
        summaryUsuariosValueLabel.Text = row.UsersLabel.Text;
    }

    private void ClearSummarySelectionValues()
    {
        summaryPerfilValueLabel.Text = "-";
        summarySituacaoValueLabel.Text = "-";
        summaryUsuariosValueLabel.Text = "-";
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
    }

    private void AtualizarBotoesAcao(ModoAcaoBotoes modo)
    {
        bool podeCriar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Balanca, PermissoesSistema.Acoes.Criar);
        bool podeEditar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Balanca, PermissoesSistema.Acoes.Editar);
        bool podeExcluir = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Balanca, PermissoesSistema.Acoes.Excluir);

        salvarButton.Visible = modo == ModoAcaoBotoes.SomenteSalvar && podeCriar;
        novoButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeEditar;

        BalancaCadastro? selecionada = _balancaPorLinha.Values
            .FirstOrDefault(item => item.CodigoBalanca == _idBalancaAtual);
        bool balancaAtiva = selecionada?.SituacaoBalanca ?? true;
        excluirButton.Text = balancaAtiva
            ? "Inativar Balança          F8"
            : "Reativar Balança          F8";
        excluirButton.Visible = modo == ModoAcaoBotoes.EditarExcluir
            && (balancaAtiva ? podeExcluir : podeEditar);
    }

    private enum ModoAcaoBotoes { Nenhum = 0, SomenteSalvar = 1, EditarExcluir = 2 }

    private static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalized.Length);

        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    // ============================================================
    // Window/drag/novo
    // ============================================================

    private void ConfigureNovoBalancaAction()
    {
        SetHandCursor(novoPerfilButtonPanel);
        AttachNovoBalancaClick(novoPerfilButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;
        foreach (Control child in control.Controls) SetHandCursor(child);
    }

    private void AttachNovoBalancaClick(Control control)
    {
        control.Click += (_, _) => PrepareNewBalanca();
        foreach (Control child in control.Controls) AttachNovoBalancaClick(child);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void ConfigureWindowButtons()
    {
        menuHeaderLabel.Click += (_, _) => Close();
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) =>
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
        };
        closeWindowLabel.Click += (_, _) => Close();
    }

    private void ConfigureDragOnTitleBar()
    {
        customTitleBarPanel.MouseDown += HandleTitleBarMouseDown;
        headerTitleLabel.MouseDown += HandleTitleBarMouseDown;
        headerSubtitleLabel.MouseDown += HandleTitleBarMouseDown;
        companyLogoPictureBox.MouseDown += HandleTitleBarMouseDown;
    }

    private void HandleTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    // ============================================================
    // Layouts
    // ============================================================

    private void LayoutSummaryCard()
    {
        const int baseCardWidth = 326;
        const int baseCardHeight = 590;

        if (summaryCard.Width <= 0 || summaryCard.Height <= 0) return;

        float scaleX = summaryCard.Width / (float)baseCardWidth;
        float scaleY = summaryCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int innerLeft = Scale(32, scaleX);
        int textLeft = Scale(74, scaleX);
        int labelWidth = Math.Max(160, summaryCard.Width - Scale(98, scaleX));
        int dividerWidth = Math.Max(120, summaryCard.Width - Scale(52, scaleX));
        int buttonX = Scale(24, scaleX);
        int buttonWidth = Math.Max(220, summaryCard.Width - Scale(48, scaleX));
        int buttonHeight = Math.Max(28, Scale(28, scaleY));
        int iconSize = Math.Max(32, Scale(32, scaleX));
        int titleHeight = Math.Max(24, Scale(24, scaleY));

        summaryTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 9F, 12.5F), FontStyle.Bold);
        summaryPerfilCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summaryPerfilValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        summarySituacaoCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summarySituacaoValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        summaryUsuariosCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summaryUsuariosValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        tipTextLabel.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 8.5F, 12F));
        salvarButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        novoButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        excluirButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);

        SetBounds(summaryTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), iconSize, iconSize);
        SetBounds(summaryTitleLabel, Scale(52, scaleX), Scale(20, scaleY), Scale(200, scaleX), titleHeight);

        SetBounds(summaryDividerLabel, Scale(26, scaleX), Scale(56, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel2, Scale(26, scaleX), Scale(124, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel3, Scale(26, scaleX), Scale(178, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel4, Scale(26, scaleX), Scale(232, scaleY), dividerWidth, 1);
        SetBounds(summaryDividerLabel5, Scale(26, scaleX), Scale(286, scaleY), dividerWidth, 1);

        SetBounds(summaryPerfilIconLabel, innerLeft, Scale(78, scaleY), iconSize, iconSize);
        SetBounds(summaryPerfilCaptionLabel, textLeft, Scale(78, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryPerfilValueLabel, textLeft, Scale(98, scaleY), labelWidth, titleHeight);

        SetBounds(summarySituacaoIconLabel, innerLeft, Scale(132, scaleY), iconSize, iconSize);
        SetBounds(summarySituacaoCaptionLabel, textLeft, Scale(132, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summarySituacaoValueLabel, textLeft, Scale(152, scaleY), labelWidth, titleHeight);

        SetBounds(summaryUsuariosIconLabel, innerLeft, Scale(186, scaleY), iconSize, iconSize);
        SetBounds(summaryUsuariosCaptionLabel, textLeft, Scale(186, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryUsuariosValueLabel, textLeft, Scale(206, scaleY), labelWidth, titleHeight);

        SetBounds(tipTextLabel, textLeft, Scale(314, scaleY), Math.Max(180, summaryCard.Width - Scale(98, scaleX)), Math.Max(56, Scale(66, scaleY)));

        SetBounds(salvarButton, buttonX, Scale(402, scaleY), buttonWidth, buttonHeight);
        SetBounds(novoButton, buttonX, Scale(434, scaleY), buttonWidth, buttonHeight);
        SetBounds(excluirButton, buttonX, Scale(466, scaleY), buttonWidth, buttonHeight);
    }

    private void LayoutDetailsCard()
    {
        const int baseCardWidth = 557;
        const int baseCardHeight = 596;

        if (detailsCard.Width <= 0 || detailsCard.Height <= 0) return;

        float scaleX = detailsCard.Width / (float)baseCardWidth;
        float scaleY = detailsCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int leftPadding = Scale(24, scaleX);
        int rightPadding = Scale(24, scaleX);
        int cardInnerWidth = Math.Max(260, detailsCard.Width - leftPadding - rightPadding);

        int desiredGap = Scale(30, scaleX);
        int baseFieldWidth = Scale(238, scaleX);
        int maxFieldWidth = Scale(320, scaleX);
        int fieldWidth = Math.Min(maxFieldWidth, Math.Max(baseFieldWidth, (cardInnerWidth - desiredGap) / 2));
        int gap = Math.Max(Scale(22, scaleX), cardInnerWidth - (fieldWidth * 2));

        int layoutWidth = fieldWidth * 2 + gap;
        int col1 = leftPadding + Math.Max(0, (cardInnerWidth - layoutWidth) / 2);
        int col2 = col1 + fieldWidth + gap;

        ApplyScaledFont(detailsTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(detailsTitleIconLabel, 14F, contentScale, 14F, 18F);
        ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblPeso, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblSetorTara, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(nomePerfilTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(descricaoTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(TxtPeso, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(CmbSetor, 9F, contentScale, 9F, 12F);

        if (_lblTipoConexao is not null) ApplyScaledFont(_lblTipoConexao, 7.75F, contentScale, 8.5F, 11F);
        if (_cmbTipoConexao is not null) ApplyScaledFont(_cmbTipoConexao, 9F, contentScale, 9F, 12F);
        if (_lblPortaTcp is not null) ApplyScaledFont(_lblPortaTcp, 7.75F, contentScale, 8.5F, 11F);
        if (_txtPortaTcp is not null) ApplyScaledFont(_txtPortaTcp, 9F, contentScale, 9F, 12F);
        if (_lblIdentificacaoLocal is not null) ApplyScaledFont(_lblIdentificacaoLocal, 7.75F, contentScale, 8.5F, 11F);
        if (_txtIdentificacaoLocal is not null) ApplyScaledFont(_txtIdentificacaoLocal, 9F, contentScale, 9F, 12F);
        if (_lblPortaSerial is not null) ApplyScaledFont(_lblPortaSerial, 7.75F, contentScale, 8.5F, 11F);
        if (_txtPortaSerial is not null) ApplyScaledFont(_txtPortaSerial, 9F, contentScale, 9F, 12F);
        if (_lblBaudRate is not null) ApplyScaledFont(_lblBaudRate, 7.75F, contentScale, 8.5F, 11F);
        if (_txtBaudRate is not null) ApplyScaledFont(_txtBaudRate, 9F, contentScale, 9F, 12F);
        if (_lblDataBits is not null) ApplyScaledFont(_lblDataBits, 7.75F, contentScale, 8.5F, 11F);
        if (_txtDataBits is not null) ApplyScaledFont(_txtDataBits, 9F, contentScale, 9F, 12F);
        if (_lblParidade is not null) ApplyScaledFont(_lblParidade, 7.75F, contentScale, 8.5F, 11F);
        if (_cmbParidade is not null) ApplyScaledFont(_cmbParidade, 9F, contentScale, 9F, 12F);
        if (_lblStopBits is not null) ApplyScaledFont(_lblStopBits, 7.75F, contentScale, 8.5F, 11F);
        if (_cmbStopBits is not null) ApplyScaledFont(_cmbStopBits, 9F, contentScale, 9F, 12F);

        situacaoComboBox.IntegralHeight = false;
        situacaoComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        CmbSetor.IntegralHeight = false;
        CmbSetor.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        if (_cmbTipoConexao is not null)
        {
            _cmbTipoConexao.IntegralHeight = false;
            _cmbTipoConexao.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        }
        if (_cmbParidade is not null) _cmbParidade.IntegralHeight = false;
        if (_cmbStopBits is not null) _cmbStopBits.IntegralHeight = false;

        SetBounds(detailsTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(detailsTitleLabel, Scale(52, scaleX), Scale(22, scaleY), Math.Max(190, Scale(220, scaleX)), Scale(24, scaleY));

        // Linha 1: Nome (col1) | Situacao (col2)
        SetBounds(nomePerfilLabel, col1, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(nomePerfilInputPanel, col1, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(nomePerfilTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        SetBounds(situacaoLabel, col2, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(situacaoInputPanel, col2, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        int situacaoComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int situacaoComboHeight = Math.Max(22, situacaoComboBox.PreferredHeight);
        int situacaoComboY = Math.Max(2, (situacaoInputPanel.Height - situacaoComboHeight) / 2);
        SetBounds(situacaoComboBox, Scale(12, scaleX), situacaoComboY, situacaoComboWidth, situacaoComboHeight);

        // Linha 2: Observacao (col1, alta) | Endereco IP (col2)
        SetBounds(descricaoLabel, col1, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(descricaoInputPanel, col1, Scale(140, scaleY), fieldWidth, Math.Max(95, Scale(166, scaleY)));
        SetBounds(descricaoTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(120, fieldWidth - Scale(24, scaleX)), Math.Max(60, descricaoInputPanel.Height - Scale(16, scaleY)));

        SetBounds(LblPeso, col2, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(roundedPanel1, col2, Scale(140, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(TxtPeso, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        // Linha 3 (col2): Tipo Conexao
        if (_lblTipoConexao is not null && _tipoConexaoInputPanel is not null && _cmbTipoConexao is not null)
        {
            int tipoY = Scale(187, scaleY);
            SetBounds(_lblTipoConexao, col2, tipoY, fieldWidth, Scale(19, scaleY));
            SetBounds(_tipoConexaoInputPanel, col2, tipoY + Scale(22, scaleY), fieldWidth, Scale(33, scaleY));
            int tipoComboW = Math.Max(80, fieldWidth - Scale(24, scaleX));
            int tipoComboH = Math.Max(22, _cmbTipoConexao.PreferredHeight);
            int tipoComboY = Math.Max(2, (_tipoConexaoInputPanel.Height - tipoComboH) / 2);
            SetBounds(_cmbTipoConexao, Scale(12, scaleX), tipoComboY, tipoComboW, tipoComboH);
        }

        // Linha 4 (col2): Porta TCP
        if (_lblPortaTcp is not null && _portaTcpInputPanel is not null && _txtPortaTcp is not null)
        {
            int portaY = Scale(251, scaleY);
            SetBounds(_lblPortaTcp, col2, portaY, fieldWidth, Scale(19, scaleY));
            SetBounds(_portaTcpInputPanel, col2, portaY + Scale(22, scaleY), fieldWidth, Scale(33, scaleY));
            SetBounds(_txtPortaTcp, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));
        }

        // Linha 5 (col2): Setor
        SetBounds(LblSetorTara, col2, Scale(315, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(RdpSetor, col2, Scale(337, scaleY), fieldWidth, Scale(33, scaleY));
        int setorComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int setorComboHeight = Math.Max(22, CmbSetor.PreferredHeight);
        int setorComboY = Math.Max(2, (RdpSetor.Height - setorComboHeight) / 2);
        SetBounds(CmbSetor, Scale(12, scaleX), setorComboY, setorComboWidth, setorComboHeight);

        // Linha 6 (col1, abaixo da observacao): Identificacao Local
        if (_lblIdentificacaoLocal is not null && _identificacaoLocalInputPanel is not null && _txtIdentificacaoLocal is not null)
        {
            int idLocalY = descricaoInputPanel.Bottom + Scale(12, scaleY);
            SetBounds(_lblIdentificacaoLocal, col1, idLocalY, fieldWidth, Scale(19, scaleY));
            SetBounds(_identificacaoLocalInputPanel, col1, idLocalY + Scale(22, scaleY), fieldWidth, Scale(33, scaleY));
            SetBounds(_txtIdentificacaoLocal, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));
        }

        int divisorY = Math.Max(_identificacaoLocalInputPanel?.Bottom ?? 0, RdpSetor.Bottom) + Scale(12, scaleY);
        SetBounds(detailsTopDividerLabel, col1, divisorY, layoutWidth, 1);

        int tecnicoY1 = divisorY + Scale(10, scaleY);
        PosicionarCampoRuntime(_lblPortaSerial, _portaSerialInputPanel, _txtPortaSerial, col1, tecnicoY1, fieldWidth, scaleX, scaleY);
        PosicionarCampoRuntime(_lblBaudRate, _baudRateInputPanel, _txtBaudRate, col2, tecnicoY1, fieldWidth, scaleX, scaleY);

        int tecnicoY2 = tecnicoY1 + Scale(58, scaleY);
        PosicionarCampoRuntime(_lblDataBits, _dataBitsInputPanel, _txtDataBits, col1, tecnicoY2, fieldWidth, scaleX, scaleY);
        PosicionarComboRuntime(_lblParidade, _paridadeInputPanel, _cmbParidade, col2, tecnicoY2, fieldWidth, scaleX, scaleY);

        int tecnicoY3 = tecnicoY2 + Scale(58, scaleY);
        PosicionarComboRuntime(_lblStopBits, _stopBitsInputPanel, _cmbStopBits, col1, tecnicoY3, fieldWidth, scaleX, scaleY);
    }

    private static void PosicionarCampoRuntime(
        Label? label,
        RoundedPanel? panel,
        TextBox? campo,
        int x,
        int y,
        int width,
        float scaleX,
        float scaleY)
    {
        if (label is null || panel is null || campo is null) return;
        label.Bounds = new Rectangle(x, y, width, Math.Max(16, (int)Math.Round(19 * scaleY)));
        panel.Bounds = new Rectangle(x, y + Math.Max(18, (int)Math.Round(20 * scaleY)), width, Math.Max(28, (int)Math.Round(33 * scaleY)));
        campo.Bounds = new Rectangle(
            Math.Max(8, (int)Math.Round(12 * scaleX)),
            Math.Max(6, (int)Math.Round(8 * scaleY)),
            Math.Max(80, width - Math.Max(16, (int)Math.Round(24 * scaleX))),
            Math.Max(16, (int)Math.Round(16 * scaleY)));
    }

    private static void PosicionarComboRuntime(
        Label? label,
        RoundedPanel? panel,
        ComboBox? combo,
        int x,
        int y,
        int width,
        float scaleX,
        float scaleY)
    {
        if (label is null || panel is null || combo is null) return;
        label.Bounds = new Rectangle(x, y, width, Math.Max(16, (int)Math.Round(19 * scaleY)));
        panel.Bounds = new Rectangle(x, y + Math.Max(18, (int)Math.Round(20 * scaleY)), width, Math.Max(28, (int)Math.Round(33 * scaleY)));
        int comboHeight = Math.Max(22, combo.PreferredHeight);
        combo.Bounds = new Rectangle(
            Math.Max(8, (int)Math.Round(12 * scaleX)),
            Math.Max(2, (panel.Height - comboHeight) / 2),
            Math.Max(80, width - Math.Max(16, (int)Math.Round(24 * scaleX))),
            comboHeight);
    }

    private void LayoutProfilesCard()
    {
        const int baseCardWidth = 411;
        const int baseCardHeight = 596;

        if (profilesCard.Width <= 0 || profilesCard.Height <= 0) return;

        float scaleX = profilesCard.Width / (float)baseCardWidth;
        float scaleY = profilesCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int side = Scale(16, scaleX);
        int cardWidth = Math.Max(230, profilesCard.Width - side - side);
        int titleY = Scale(21, scaleY);

        profilesTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        profilesFooterLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11F));
        novoPerfilTextLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        searchTextBox.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 9F, 12F));
        profilesSearchIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(11F * contentScale, 11F, 14F));
        profilesHeaderProfileLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderUsersLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderStatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        searchTextBox.Multiline = false;

        SetBounds(profilesCheckMarkLabel, Scale(16, scaleX), Scale(19, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(profilesTitleLabel, Scale(43, scaleX), titleY, Scale(190, scaleX), Scale(24, scaleY));

        int quickH = Math.Max(31, Scale(31, scaleY));
        int iconW = Math.Max(20, Scale(20, scaleX));
        int iconLeft = Math.Max(10, Scale(10, scaleX));
        int textLeft = iconLeft + iconW + Math.Max(4, Scale(4, scaleX));
        int horizontalPadding = Math.Max(8, Scale(8, scaleX));
        int minQuickW = Math.Max(142, Scale(142, scaleX));
        int textWidth = TextRenderer.MeasureText(novoPerfilTextLabel.Text, novoPerfilTextLabel.Font).Width;
        int quickW = Math.Max(minQuickW, textLeft + textWidth + horizontalPadding);

        SetBounds(novoPerfilButtonPanel, Math.Max(side, profilesCard.Width - side - quickW), Scale(16, scaleY), quickW, quickH);
        SetBounds(novoPerfilIconLabel, iconLeft, Math.Max(3, (quickH - Math.Max(18, Scale(21, scaleY))) / 2), iconW, Math.Max(18, Scale(21, scaleY)));
        SetBounds(novoPerfilTextLabel, textLeft, Math.Max(3, (quickH - Math.Max(18, Scale(21, scaleY))) / 2), Math.Max(72, quickW - textLeft - horizontalPadding), Math.Max(18, Scale(21, scaleY)));

        int searchY = Scale(60, scaleY);
        int searchH = Math.Max(34, Scale(34, scaleY));
        SetBounds(profilesSearchPanel, side, searchY, cardWidth, searchH);
        int searchIconWidth = Math.Max(20, Scale(22, scaleX));
        int searchIconHeight = Math.Max(20, Scale(24, scaleY));
        int searchTextHeight = Math.Max(16, searchTextBox.PreferredHeight);
        int searchTextY = Math.Max(2, (profilesSearchPanel.Height - searchTextHeight) / 2);
        int searchIconY = Math.Max(2, searchTextY + ((searchTextHeight - searchIconHeight) / 2));
        SetBounds(profilesSearchIconLabel, Scale(8, scaleX), searchIconY, searchIconWidth, searchIconHeight);
        SetBounds(searchTextBox, Scale(36, scaleX), searchTextY, Math.Max(120, cardWidth - Scale(44, scaleX)), searchTextHeight);

        int tableY = Scale(108, scaleY);
        int footerY = Math.Max(tableY + 180, profilesCard.Height - Scale(28, scaleY));
        int tableH = Math.Max(220, footerY - tableY - Scale(10, scaleY));
        SetBounds(profilesTablePanel, side, tableY, cardWidth, tableH);

        int nameX = Scale(12, scaleX);
        int usersX = Math.Max(Scale(150, scaleX), cardWidth - Scale(185, scaleX));
        int statusPanelW = Math.Max(50, Scale(58, scaleX));
        int statusPanelX = Math.Max(Scale(250, scaleX), cardWidth - Scale(3, scaleX) - statusPanelW - Scale(10, scaleX));

        SetBounds(profilesHeaderProfileLabel, Scale(16, scaleX), Scale(9, scaleY), Math.Max(120, usersX - Scale(26, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderUsersLabel, usersX, Scale(9, scaleY), Math.Max(90, statusPanelX - usersX - Scale(8, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderStatusLabel, statusPanelX, Scale(9, scaleY), statusPanelW, Scale(20, scaleY));

        SetBounds(profilesFooterLabel, Scale(20, scaleX), footerY, Math.Max(180, Scale(220, scaleX)), Scale(22, scaleY));

        AtualizarLayoutLinhas();
        ApplyProfilesFilter();
    }

    private void AtualizarLayoutLinhas()
    {
        if (_profileSearchRows.Count == 0) return;

        float scaleX = profilesCard.Width / 411f;
        float scaleY = profilesCard.Height / 596f;
        float contentScale = Math.Min(scaleX, scaleY);
        int side = Math.Max(0, (int)Math.Round(16 * scaleX));
        int cardWidth = Math.Max(230, profilesCard.Width - side - side);
        int nameX = Math.Max(0, (int)Math.Round(12 * scaleX));
        int statusPanelW = Math.Max(50, (int)Math.Round(58 * scaleX));
        int rowRightPadding = Math.Max(12, (int)Math.Round(16 * scaleX));
        int statusInnerRightPadding = Math.Max(8, (int)Math.Round(10 * scaleX));
        int rowHeight = Math.Max(40, (int)Math.Round(46 * scaleY));
        int baseRowY = Math.Max(0, (int)Math.Round(34 * scaleY));
        int rowLeft = Math.Max(0, (int)Math.Round(3 * scaleX));
        int larguraUtil = Math.Max(200, profilesTablePanel.ClientSize.Width - rowLeft - rowRightPadding);
        int rowWidth = larguraUtil;
        int usersX = Math.Max((int)Math.Round(150 * scaleX), rowWidth - (int)Math.Round(185 * scaleX));
        int statusPanelXMin = Math.Max((int)Math.Round(250 * scaleX), nameX + 120);

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            ProfileSearchRow row = _profileSearchRows[i];
            row.NameLabel.Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            row.UsersLabel.Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            row.StatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10F), FontStyle.Bold);

            row.RowPanel.Bounds = new Rectangle(rowLeft, baseRowY + rowHeight * i, rowWidth, rowHeight);
            int statusPanelX = Math.Max(statusPanelXMin, rowWidth - statusPanelW - statusInnerRightPadding);
            row.NameLabel.Bounds = new Rectangle(nameX, Math.Max(0, (int)Math.Round(11 * scaleY)), Math.Max(90, usersX - nameX - Math.Max(0, (int)Math.Round(8 * scaleX))), Math.Max(16, (int)Math.Round(24 * scaleY)));
            row.UsersLabel.Bounds = new Rectangle(usersX, Math.Max(0, (int)Math.Round(11 * scaleY)), Math.Max(60, statusPanelX - usersX - Math.Max(0, (int)Math.Round(8 * scaleX))), Math.Max(16, (int)Math.Round(24 * scaleY)));

            if (_statusPanelPorLinha.TryGetValue(row.RowPanel, out RoundedPanel? statusPanel))
            {
                statusPanel.Bounds = new Rectangle(statusPanelX, Math.Max(0, (int)Math.Round(11 * scaleY)), statusPanelW, Math.Max(16, (int)Math.Round(24 * scaleY)));
            }
        }

        UpdateMarkerSizes(Math.Max(3, Math.Max(0, (int)Math.Round(3 * scaleX))));
    }

    private static void ApplyScaledFont(Control control, float baseSize, float scale, float minSize, float maxSize)
    {
        float size = Math.Clamp(baseSize * scale, minSize, maxSize);
        if (Math.Abs(control.Font.Size - size) < 0.01f) return;
        control.Font = new Font(control.Font.FontFamily, size, control.Font.Style);
    }

    // ============================================================
    // Markers e badges
    // ============================================================

    private void CriarMarcadorLinha(Panel rowPanel)
    {
        Panel marker = new()
        {
            Name = $"{rowPanel.Name}MarkerPanel",
            BackColor = MarkerColor,
            Size = new Size(3, rowPanel.Height),
            Visible = false
        };
        rowPanel.Controls.Add(marker);
        _rowSelectionMarkers[rowPanel] = marker;
    }

    private void UpdateMarkerSizes(int markerWidth)
    {
        foreach (RowSelection row in _rowSelections)
        {
            if (_rowSelectionMarkers.TryGetValue(row.RowPanel, out Panel? marker))
            {
                marker.Size = new Size(markerWidth, row.RowPanel.Height);
                if (marker.Visible)
                {
                    marker.Bounds = new Rectangle(0, 0, markerWidth, row.RowPanel.Height);
                    marker.BringToFront();
                }
            }
        }
    }

    private void ShowMarkerForRow(Panel rowPanel)
    {
        if (!_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker)) return;
        marker.Parent = rowPanel;
        marker.Bounds = new Rectangle(0, 0, marker.Width, rowPanel.Height);
        marker.Visible = true;
        marker.BringToFront();
    }

    private void HideMarkerForRow(Panel rowPanel)
    {
        if (_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker)) marker.Visible = false;
    }

    private void HideAllMarkers()
    {
        foreach (Panel marker in _rowSelectionMarkers.Values) marker.Visible = false;
    }

    private void AtualizarBadgeStatus(Panel rowPanel, string status)
    {
        if (!_statusPanelPorLinha.TryGetValue(rowPanel, out RoundedPanel? statusPanel)) return;
        if (!_statusLabelPorLinha.TryGetValue(rowPanel, out Label? statusLabel)) return;

        if (status.Equals("Ativo", StringComparison.OrdinalIgnoreCase))
        {
            statusPanel.FillColor = StatusAtivoFundo;
            statusPanel.BorderColor = StatusAtivoTexto;
            statusLabel.ForeColor = StatusAtivoTexto;
            return;
        }

        if (status.Equals("Inativo", StringComparison.OrdinalIgnoreCase))
        {
            statusPanel.FillColor = StatusInativoFundo;
            statusPanel.BorderColor = StatusInativoTexto;
            statusLabel.ForeColor = StatusInativoTexto;
            return;
        }

        statusPanel.FillColor = StatusNeutroFundo;
        statusPanel.BorderColor = StatusNeutroTexto;
        statusLabel.ForeColor = StatusNeutroTexto;
    }

    // ============================================================
    // Campos extras runtime
    // ============================================================

    private void CriarCamposExtrasRuntime()
    {
        // Tipo de Conexao
        _lblTipoConexao = CriarLabelTopico("lblTipoConexaoRuntime", "Tipo de Conexão");
        _tipoConexaoInputPanel = CriarPanelArredondado("tipoConexaoInputPanelRuntime");
        _cmbTipoConexao = new ComboBox
        {
            Name = "cmbTipoConexaoRuntime",
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = situacaoComboBox.Font,
            BackColor = Color.White
        };
        _cmbTipoConexao.Items.AddRange(TiposConexao);
        _cmbTipoConexao.SelectedIndex = 0;
        _tipoConexaoInputPanel.Controls.Add(_cmbTipoConexao);

        // Porta TCP
        _lblPortaTcp = CriarLabelTopico("lblPortaTcpRuntime", "Porta TCP");
        _portaTcpInputPanel = CriarPanelArredondado("portaTcpInputPanelRuntime");
        _txtPortaTcp = new TextBox
        {
            Name = "txtPortaTcpRuntime",
            BorderStyle = BorderStyle.None,
            Font = nomePerfilTextBox.Font,
            BackColor = Color.White
        };
        _portaTcpInputPanel.Controls.Add(_txtPortaTcp);

        // Identificacao Local
        _lblIdentificacaoLocal = CriarLabelTopico("lblIdentificacaoLocalRuntime", "Identificação Local");
        _identificacaoLocalInputPanel = CriarPanelArredondado("identificacaoLocalInputPanelRuntime");
        _txtIdentificacaoLocal = new TextBox
        {
            Name = "txtIdentificacaoLocalRuntime",
            BorderStyle = BorderStyle.None,
            Font = nomePerfilTextBox.Font,
            BackColor = Color.White
        };
        _identificacaoLocalInputPanel.Controls.Add(_txtIdentificacaoLocal);

        _lblPortaSerial = CriarLabelTopico("lblPortaSerialRuntime", "Porta Serial");
        _portaSerialInputPanel = CriarPanelArredondado("portaSerialInputPanelRuntime");
        _txtPortaSerial = CriarTextBoxRuntime("txtPortaSerialRuntime");
        _portaSerialInputPanel.Controls.Add(_txtPortaSerial);

        _lblBaudRate = CriarLabelTopico("lblBaudRateRuntime", "Baud Rate");
        _baudRateInputPanel = CriarPanelArredondado("baudRateInputPanelRuntime");
        _txtBaudRate = CriarTextBoxRuntime("txtBaudRateRuntime");
        _baudRateInputPanel.Controls.Add(_txtBaudRate);

        _lblDataBits = CriarLabelTopico("lblDataBitsRuntime", "Data Bits");
        _dataBitsInputPanel = CriarPanelArredondado("dataBitsInputPanelRuntime");
        _txtDataBits = CriarTextBoxRuntime("txtDataBitsRuntime");
        _dataBitsInputPanel.Controls.Add(_txtDataBits);

        _lblParidade = CriarLabelTopico("lblParidadeRuntime", "Paridade");
        _paridadeInputPanel = CriarPanelArredondado("paridadeInputPanelRuntime");
        _cmbParidade = CriarComboRuntime("cmbParidadeRuntime", Paridades);
        _paridadeInputPanel.Controls.Add(_cmbParidade);

        _lblStopBits = CriarLabelTopico("lblStopBitsRuntime", "Stop Bits");
        _stopBitsInputPanel = CriarPanelArredondado("stopBitsInputPanelRuntime");
        _cmbStopBits = CriarComboRuntime("cmbStopBitsRuntime", StopBits);
        _stopBitsInputPanel.Controls.Add(_cmbStopBits);

        detailsCard.Controls.Add(_lblTipoConexao);
        detailsCard.Controls.Add(_tipoConexaoInputPanel);
        detailsCard.Controls.Add(_lblPortaTcp);
        detailsCard.Controls.Add(_portaTcpInputPanel);
        detailsCard.Controls.Add(_lblIdentificacaoLocal);
        detailsCard.Controls.Add(_identificacaoLocalInputPanel);
        detailsCard.Controls.Add(_lblPortaSerial);
        detailsCard.Controls.Add(_portaSerialInputPanel);
        detailsCard.Controls.Add(_lblBaudRate);
        detailsCard.Controls.Add(_baudRateInputPanel);
        detailsCard.Controls.Add(_lblDataBits);
        detailsCard.Controls.Add(_dataBitsInputPanel);
        detailsCard.Controls.Add(_lblParidade);
        detailsCard.Controls.Add(_paridadeInputPanel);
        detailsCard.Controls.Add(_lblStopBits);
        detailsCard.Controls.Add(_stopBitsInputPanel);

        _lblTipoConexao.BringToFront(); _tipoConexaoInputPanel.BringToFront(); _cmbTipoConexao.BringToFront();
        _lblPortaTcp.BringToFront(); _portaTcpInputPanel.BringToFront(); _txtPortaTcp.BringToFront();
        _lblIdentificacaoLocal.BringToFront(); _identificacaoLocalInputPanel.BringToFront(); _txtIdentificacaoLocal.BringToFront();
        _lblPortaSerial.BringToFront(); _portaSerialInputPanel.BringToFront(); _txtPortaSerial.BringToFront();
        _lblBaudRate.BringToFront(); _baudRateInputPanel.BringToFront(); _txtBaudRate.BringToFront();
        _lblDataBits.BringToFront(); _dataBitsInputPanel.BringToFront(); _txtDataBits.BringToFront();
        _lblParidade.BringToFront(); _paridadeInputPanel.BringToFront(); _cmbParidade.BringToFront();
        _lblStopBits.BringToFront(); _stopBitsInputPanel.BringToFront(); _cmbStopBits.BringToFront();

        _cmbTipoConexao.SelectedIndexChanged += (_, _) => AtualizarVisibilidadeCamposTecnicos();
        AtualizarVisibilidadeCamposTecnicos();
    }

    private TextBox CriarTextBoxRuntime(string name)
        => new()
        {
            Name = name,
            BorderStyle = BorderStyle.None,
            Font = nomePerfilTextBox.Font,
            BackColor = Color.White
        };

    private ComboBox CriarComboRuntime(string name, string[] itens)
    {
        ComboBox combo = new()
        {
            Name = name,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = situacaoComboBox.Font,
            BackColor = Color.White
        };
        combo.Items.AddRange(itens);
        combo.SelectedIndex = 0;
        return combo;
    }

    private void AtualizarVisibilidadeCamposTecnicos()
    {
        string tipo = _cmbTipoConexao?.SelectedItem?.ToString() ?? "SERIAL";
        bool serial = tipo == "SERIAL";
        bool tcp = tipo == "TCP_IP";
        bool usb = tipo == "USB";

        DefinirVisibilidade(tcp, LblPeso, roundedPanel1);
        DefinirVisibilidade(tcp, _lblPortaTcp, _portaTcpInputPanel);
        DefinirVisibilidade(usb, _lblIdentificacaoLocal, _identificacaoLocalInputPanel);
        DefinirVisibilidade(serial, _lblPortaSerial, _portaSerialInputPanel);
        DefinirVisibilidade(serial, _lblBaudRate, _baudRateInputPanel);
        DefinirVisibilidade(serial, _lblDataBits, _dataBitsInputPanel);
        DefinirVisibilidade(serial, _lblParidade, _paridadeInputPanel);
        DefinirVisibilidade(serial, _lblStopBits, _stopBitsInputPanel);
    }

    private static void DefinirVisibilidade(bool visivel, params Control?[] controles)
    {
        foreach (Control? controle in controles)
        {
            if (controle is not null) controle.Visible = visivel;
        }
    }

    private Label CriarLabelTopico(string name, string text)
        => new()
        {
            Name = name,
            Text = text,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = nomePerfilLabel.ForeColor,
            Font = nomePerfilLabel.Font,
            BackColor = Color.Transparent
        };

    private RoundedPanel CriarPanelArredondado(string name)
        => new()
        {
            Name = name,
            BorderRadius = nomePerfilInputPanel is RoundedPanel rp ? rp.BorderRadius : 6,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(214, 219, 226),
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };

    // ============================================================
    // Helpers internos
    // ============================================================

    private sealed class RowSelection
    {
        public RowSelection(Panel rowPanel, Color normalBackColor)
        {
            RowPanel = rowPanel;
            NormalBackColor = normalBackColor;
        }

        public Panel RowPanel { get; }
        public Color NormalBackColor { get; }
    }

    private sealed class ProfileSearchRow
    {
        public ProfileSearchRow(Panel rowPanel, Label nameLabel, Label usersLabel, Label statusLabel)
        {
            RowPanel = rowPanel;
            NameLabel = nameLabel;
            UsersLabel = usersLabel;
            StatusLabel = statusLabel;
        }

        public Panel RowPanel { get; }
        public Label NameLabel { get; }
        public Label UsersLabel { get; }
        public Label StatusLabel { get; }
    }
}
