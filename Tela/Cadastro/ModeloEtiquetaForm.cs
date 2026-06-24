using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Tela.Controls;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class ModeloEtiquetaForm : Form
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
    private readonly Dictionary<Panel, long> _idModeloPorLinha = new();
    private readonly Dictionary<Panel, ModeloEtiquetaCadastro> _modeloPorLinha = new();
    private readonly List<ModeloEtiquetaCadastro> _modelosCarregados = new();
    private readonly ToolTip _toolTipModelo = new();
    private readonly ModeloEtiquetaController _modeloController;
    private long _idModeloAtual;

    // Campos extras criados em runtime (Designer base so tem nome/descricao/situacao).
    private TextBox? _txtVersao;
    private TextBox? _txtDpi;
    private TextBox? _txtLargura;
    private TextBox? _txtAltura;
    private TextBox? _txtObservacao;
    private readonly List<(Label Rotulo, RoundedPanel Painel, TextBox Campo)> _camposExtras = new();

    private static readonly Color StatusAtivoFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color StatusAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusInativoFundo = Color.FromArgb(255, 237, 213);
    private static readonly Color StatusInativoTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color StatusNeutroFundo = Color.FromArgb(243, 244, 246);
    private static readonly Color StatusNeutroTexto = Color.FromArgb(100, 116, 139);

    public ModeloEtiquetaForm(ModeloEtiquetaController? modeloController = null)
    {
        _modeloController = modeloController ?? FabricaControladoresCadastro.CriarModeloEtiquetaController();
        InitializeComponent();
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";

        // Reaproveita descricaoTextBox como editor de ZPL (multiline).
        descricaoLabel.Text = "Conteudo ZPL *";
        descricaoTextBox.Multiline = true;
        descricaoTextBox.ScrollBars = ScrollBars.Vertical;
        descricaoTextBox.Font = new Font("Consolas", 9F);

        CriarCamposExtrasRuntime();

        ConfigureWindowButtons();
        ConfigureDragOnTitleBar();
        ConfigureNovoModeloAction();
        ConfigureProfilesSearchFilter();
        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        detailsCard.Resize += (_, _) => LayoutDetailsCard();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();
        InitializeProfilesTableSelection();
        LayoutProfilesCard();
        LayoutDetailsCard();
        LayoutSummaryCard();
        tipTextLabel.AutoEllipsis = true;
        tipTextLabel.AutoSize = false;
        tipTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        AtualizarRodapeModelos();
        AtualizarTipCadastro(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        ConectarAcoesCadastro();
        if (_integracaoBancoHabilitada)
        {
            Shown += async (_, _) => await CarregarModelosAsync();
        }
    }

    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) => await SalvarModeloAsync();
        BtnEditar.Click += async (_, _) => await EditarModeloAsync();
        excluirButton.Click += async (_, _) => await ExcluirModeloAsync();
    }

    private ModeloEtiquetaCadastro MontarModeloDoForm()
        => new()
        {
            NomeModeloEtiqueta = nomePerfilTextBox.Text.Trim(),
            ConteudoZpl = descricaoTextBox.Text,
            Versao = ParseIntOuPadrao(_txtVersao?.Text, 1),
            Dpi = ParseIntNullable(_txtDpi?.Text),
            LarguraMm = ParseDecimalNullable(_txtLargura?.Text),
            AlturaMm = ParseDecimalNullable(_txtAltura?.Text),
            Observacao = _txtObservacao?.Text.Trim() ?? string.Empty,
            SituacaoModeloEtiqueta = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase)
        };

    private async Task SalvarModeloAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Modelo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ModeloEtiquetaCadastro modelo = MontarModeloDoForm();
        modelo.ModeloEtiquetaCriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;

        var resultado = await _modeloController.InserirAsync(modelo);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Modelo", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idModeloAtual = resultado.IdGerado ?? 0;
            PrepareNewModelo();
            await CarregarModelosAsync();
        }
    }

    private async Task EditarModeloAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Modelo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idModeloAtual <= 0)
        {
            MessageBox.Show("Selecione um modelo para editar.", "Cadastro de Modelo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        ModeloEtiquetaCadastro modelo = MontarModeloDoForm();
        modelo.CodigoModeloEtiqueta = _idModeloAtual;
        modelo.ModeloEtiquetaAtualizadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;

        var resultado = await _modeloController.AtualizarAsync(modelo);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Modelo", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarModelosAsync();
        }
    }

    private async Task ExcluirModeloAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Modelo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idModeloAtual <= 0)
        {
            MessageBox.Show("Salve ou selecione um modelo para inativar.", "Cadastro de Modelo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            "Confirma a inativacao do modelo atual?\n\nO modelo ficara inativo, mas pode ser reativado depois. Modelos com etiquetas vinculadas nao podem ser inativados.",
            "Cadastro de Modelo",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes) return;

        var resultado = await _modeloController.ExcluirAsync(_idModeloAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Modelo", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idModeloAtual = 0;
            PrepareNewModelo();
            await CarregarModelosAsync();
        }
    }

    private async Task CarregarModelosAsync()
    {
        IReadOnlyList<ModeloEtiquetaCadastro> modelos = await _modeloController.ListarAsync();
        _modelosCarregados.Clear();
        _modelosCarregados.AddRange(modelos);
        RecriarLinhasPerfis();
        PopularLinhasComModelos(_modelosCarregados);
        ApplyProfilesFilter();
    }

    private void ConfigureProfilesSearchFilter()
    {
        searchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void RecriarLinhasPerfis()
    {
        RemoverLinhasPerfisExistentes();

        for (int i = 0; i < _modelosCarregados.Count; i++)
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
        _idModeloPorLinha.Clear();
        _modeloPorLinha.Clear();
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

    private void PopularLinhasComModelos(IReadOnlyList<ModeloEtiquetaCadastro> modelos)
    {
        _idModeloPorLinha.Clear();
        _modeloPorLinha.Clear();

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            ModeloEtiquetaCadastro modelo = modelos[i];
            ProfileSearchRow linha = _profileSearchRows[i];

            linha.NameLabel.Text = modelo.NomeModeloEtiqueta;
            _toolTipModelo.SetToolTip(linha.NameLabel, $"{modelo.NomeModeloEtiqueta} (v{modelo.Versao})");
            linha.UsersLabel.Text = $"v{modelo.Versao}";
            linha.StatusLabel.Text = modelo.SituacaoModeloEtiqueta ? "Ativo" : "Inativo";
            AtualizarBadgeStatus(linha.RowPanel, linha.StatusLabel.Text);
            _idModeloPorLinha[linha.RowPanel] = modelo.CodigoModeloEtiqueta;
            _modeloPorLinha[linha.RowPanel] = modelo;
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
            bool match = string.IsNullOrWhiteSpace(query) || rowName.Contains(query, StringComparison.Ordinal);
            row.RowPanel.Visible = match;
            if (!match) continue;

            int top = baseTop + (rowHeight * visibleIndex);
            row.RowPanel.Location = new Point(baseLeft, top);
            visibleIndex++;
        }

        AtualizarRodapeModelos(visibleIndex);

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

    private void AtualizarRodapeModelos(int? totalVisivel = null)
    {
        int exibindo = totalVisivel ?? _profileSearchRows.Count(x => x.RowPanel.Visible);
        int total = _modelosCarregados.Count;
        profilesFooterLabel.Text = $"Exibindo {exibindo} de {total} modelos";
    }

    private void InitializeProfilesTableSelection()
    {
        profilesTablePanel.AutoScroll = true;
        _profileSearchRows.Clear();
        _rowSelections.Clear();
        _rowSelectionMarkers.Clear();
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

        _idModeloAtual = _idModeloPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        PreencherCamposModeloPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
        AtualizarBotoesAcao(ModoAcaoBotoes.EditarExcluir);
    }

    private void PreencherCamposModeloPorLinha(Panel rowPanel)
    {
        if (!_modeloPorLinha.TryGetValue(rowPanel, out ModeloEtiquetaCadastro? modelo)) return;

        nomePerfilTextBox.Text = modelo.NomeModeloEtiqueta;
        descricaoTextBox.Text = modelo.ConteudoZpl;
        situacaoComboBox.Text = modelo.SituacaoModeloEtiqueta ? "Ativo" : "Inativo";
        if (_txtVersao is not null) _txtVersao.Text = modelo.Versao.ToString(CultureInfo.InvariantCulture);
        if (_txtDpi is not null) _txtDpi.Text = modelo.Dpi?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        if (_txtLargura is not null) _txtLargura.Text = modelo.LarguraMm?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        if (_txtAltura is not null) _txtAltura.Text = modelo.AlturaMm?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
        if (_txtObservacao is not null) _txtObservacao.Text = modelo.Observacao;
        nomePerfilTextBox.ReadOnly = false;
        nomePerfilTextBox.BackColor = Color.White;
        AtualizarTipCadastro(modelo.ModeloEtiquetaCriadoEm);
    }

    private void PrepareNewModelo()
    {
        _idModeloAtual = 0;
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        situacaoComboBox.SelectedIndex = -1;
        situacaoComboBox.Text = string.Empty;
        if (_txtVersao is not null) _txtVersao.Text = "1";
        if (_txtDpi is not null) _txtDpi.Text = "203";
        if (_txtLargura is not null) _txtLargura.Text = string.Empty;
        if (_txtAltura is not null) _txtAltura.Text = string.Empty;
        if (_txtObservacao is not null) _txtObservacao.Text = string.Empty;
        nomePerfilTextBox.ReadOnly = false;
        nomePerfilTextBox.BackColor = Color.White;
        nomePerfilTextBox.Focus();
        AtualizarTipCadastro(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.SomenteSalvar);
    }

    private void AtualizarTipCadastro(DateTime? dataCadastro)
    {
        tipTextLabel.Text = dataCadastro.HasValue
            ? $"Data e Hora do Cadastro: {dataCadastro.Value:dd/MM/yyyy HH:mm}"
            : "Data e Hora do Cadastro: -";
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
        bool podeCriar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.ModeloEtiqueta, PermissoesSistema.Acoes.Criar);
        bool podeEditar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.ModeloEtiqueta, PermissoesSistema.Acoes.Editar);
        bool podeExcluir = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.ModeloEtiqueta, PermissoesSistema.Acoes.Excluir);

        salvarButton.Visible = modo == ModoAcaoBotoes.SomenteSalvar && podeCriar;
        BtnEditar.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeEditar;
        excluirButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeExcluir;
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
            if (category != UnicodeCategory.NonSpacingMark) builder.Append(char.ToUpperInvariant(c));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private void ConfigureNovoModeloAction()
    {
        SetHandCursor(novoPerfilButtonPanel);
        AttachNovoModeloClick(novoPerfilButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;
        foreach (Control child in control.Controls) SetHandCursor(child);
    }

    private void AttachNovoModeloClick(Control control)
    {
        control.Click += (_, _) => PrepareNewModelo();
        foreach (Control child in control.Controls) AttachNovoModeloClick(child);
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
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
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

    private void LayoutSummaryCard()
    {
        const int baseCardWidth = 326;
        const int baseCardHeight = 590;
        if (summaryCard.Width <= 0 || summaryCard.Height <= 0) return;

        float scaleX = summaryCard.Width / (float)baseCardWidth;
        float scaleY = summaryCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control c, int x, int y, int w, int h) => c.Bounds = new Rectangle(x, y, w, h);

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
        BtnEditar.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
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
        SetBounds(tipTextLabel, textLeft, Scale(252, scaleY), Math.Max(180, summaryCard.Width - Scale(98, scaleX)), Math.Max(22, Scale(24, scaleY)));
        SetBounds(salvarButton, buttonX, Scale(402, scaleY), buttonWidth, buttonHeight);
        SetBounds(BtnEditar, buttonX, Scale(434, scaleY), buttonWidth, buttonHeight);
        SetBounds(excluirButton, buttonX, Scale(466, scaleY), buttonWidth, buttonHeight);

        summaryPerfilCaptionLabel.Text = "Modelo selecionado";
        summarySituacaoCaptionLabel.Text = "Situacao";
        summaryUsuariosCaptionLabel.Text = "Versao";
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
        static void SetBounds(Control c, int x, int y, int w, int h) => c.Bounds = new Rectangle(x, y, w, h);

        int left = Scale(24, scaleX);
        int gap = Scale(30, scaleX);
        int cardInnerWidth = Math.Max(260, detailsCard.Width - left - Scale(24, scaleX));
        int fieldWidth = Math.Max(150, (cardInnerWidth - gap) / 2);
        int col1 = left;
        int col2 = left + fieldWidth + gap;

        ApplyScaledFont(detailsTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(nomePerfilTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        situacaoComboBox.IntegralHeight = false;

        SetBounds(detailsTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(detailsTitleLabel, Scale(52, scaleX), Scale(22, scaleY), Math.Max(190, Scale(260, scaleX)), Scale(24, scaleY));

        // Linha 1: Nome (col1) | Situacao (col2)
        SetBounds(nomePerfilLabel, col1, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(nomePerfilInputPanel, col1, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(nomePerfilTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        SetBounds(situacaoLabel, col2, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(situacaoInputPanel, col2, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        int sitW = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int sitH = Math.Max(22, situacaoComboBox.PreferredHeight);
        SetBounds(situacaoComboBox, Scale(12, scaleX), Math.Max(2, (situacaoInputPanel.Height - sitH) / 2), sitW, sitH);

        // col2 empilhado: Versao, DPI, Largura, Altura
        int linhaY = Scale(117, scaleY);
        int rowH = Scale(33, scaleY);
        int lblH = Scale(16, scaleY);
        int passo = Scale(54, scaleY);
        PosicionarCampoExtra(0, col2, linhaY + passo * 0, fieldWidth, lblH, rowH, scaleX, scaleY);
        PosicionarCampoExtra(1, col2, linhaY + passo * 1, fieldWidth, lblH, rowH, scaleX, scaleY);
        PosicionarCampoExtra(2, col2, linhaY + passo * 2, fieldWidth, lblH, rowH, scaleX, scaleY);
        PosicionarCampoExtra(3, col2, linhaY + passo * 3, fieldWidth, lblH, rowH, scaleX, scaleY);

        // Conteudo ZPL (col1, alto)
        SetBounds(descricaoLabel, col1, Scale(117, scaleY), fieldWidth, Scale(16, scaleY));
        int zplH = Math.Max(120, Scale(180, scaleY));
        SetBounds(descricaoInputPanel, col1, Scale(135, scaleY), fieldWidth, zplH);
        SetBounds(descricaoTextBox, Scale(10, scaleX), Scale(8, scaleY), Math.Max(120, fieldWidth - Scale(20, scaleX)), Math.Max(90, zplH - Scale(16, scaleY)));

        // Observacao (col1, abaixo do ZPL)
        int obsY = descricaoInputPanel.Bottom + Scale(10, scaleY);
        PosicionarCampoExtra(4, col1, obsY, fieldWidth, lblH, rowH, scaleX, scaleY);

        SetBounds(detailsTopDividerLabel, col1, Math.Max(descricaoInputPanel.Bottom, linhaY + passo * 4) + Scale(40, scaleY), cardInnerWidth, 1);
    }

    private void PosicionarCampoExtra(int indice, int x, int y, int fieldWidth, int lblH, int rowH, float scaleX, float scaleY)
    {
        if (indice >= _camposExtras.Count) return;
        var (rotulo, painel, campo) = _camposExtras[indice];
        rotulo.Bounds = new Rectangle(x, y, fieldWidth, lblH);
        painel.Bounds = new Rectangle(x, y + lblH + Math.Max(2, (int)Math.Round(2 * scaleY)), fieldWidth, rowH);
        campo.Bounds = new Rectangle(Math.Max(8, (int)Math.Round(12 * scaleX)), Math.Max(6, (int)Math.Round(8 * scaleY)),
            Math.Max(80, fieldWidth - Math.Max(16, (int)Math.Round(24 * scaleX))), Math.Max(16, (int)Math.Round(16 * scaleY)));
        ApplyScaledFont(rotulo, 7.75F, Math.Min(scaleX, scaleY), 8.5F, 11F);
        ApplyScaledFont(campo, 9F, Math.Min(scaleX, scaleY), 9F, 12F);
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
        static void SetBounds(Control c, int x, int y, int w, int h) => c.Bounds = new Rectangle(x, y, w, h);

        int side = Scale(16, scaleX);
        int cardWidth = Math.Max(230, profilesCard.Width - side - side);

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
        SetBounds(profilesTitleLabel, Scale(43, scaleX), Scale(21, scaleY), Scale(190, scaleX), Scale(24, scaleY));

        int quickW = Math.Max(95, Scale(105, scaleX));
        int quickH = Math.Max(27, Scale(27, scaleY));
        SetBounds(novoPerfilButtonPanel, Math.Max(side, profilesCard.Width - side - quickW), Scale(18, scaleY), quickW, quickH);
        SetBounds(novoPerfilIconLabel, Scale(8, scaleX), Scale(3, scaleY), Math.Max(16, Scale(18, scaleX)), Math.Max(18, Scale(21, scaleY)));
        SetBounds(novoPerfilTextLabel, Scale(29, scaleX), Scale(3, scaleY), Math.Max(48, quickW - Scale(32, scaleX)), Math.Max(18, Scale(21, scaleY)));

        int searchY = Scale(60, scaleY);
        int searchH = Math.Max(34, Scale(34, scaleY));
        SetBounds(profilesSearchPanel, side, searchY, cardWidth, searchH);
        int searchTextHeight = Math.Max(16, searchTextBox.PreferredHeight);
        int searchTextY = Math.Max(2, (profilesSearchPanel.Height - searchTextHeight) / 2);
        SetBounds(profilesSearchIconLabel, Scale(8, scaleX), searchTextY, Math.Max(20, Scale(22, scaleX)), Math.Max(20, Scale(24, scaleY)));
        SetBounds(searchTextBox, Scale(36, scaleX), searchTextY, Math.Max(120, cardWidth - Scale(44, scaleX)), searchTextHeight);

        int tableY = Scale(108, scaleY);
        int footerY = Math.Max(tableY + 180, profilesCard.Height - Scale(28, scaleY));
        int tableH = Math.Max(220, footerY - tableY - Scale(10, scaleY));
        SetBounds(profilesTablePanel, side, tableY, cardWidth, tableH);

        int usersX = Math.Max(Scale(150, scaleX), cardWidth - Scale(185, scaleX));
        int statusPanelW = Math.Max(50, Scale(58, scaleX));
        int statusPanelX = Math.Max(Scale(250, scaleX), cardWidth - Scale(3, scaleX) - statusPanelW - Scale(10, scaleX));
        SetBounds(profilesHeaderProfileLabel, Scale(16, scaleX), Scale(9, scaleY), Math.Max(120, usersX - Scale(26, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderUsersLabel, usersX, Scale(9, scaleY), Math.Max(60, statusPanelX - usersX - Scale(8, scaleX)), Scale(20, scaleY));
        SetBounds(profilesHeaderStatusLabel, statusPanelX, Scale(9, scaleY), statusPanelW, Scale(20, scaleY));
        profilesHeaderUsersLabel.Text = "Versao";

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
        int statusPanelW = Math.Max(50, (int)Math.Round(58 * scaleX));
        int rowRightPadding = Math.Max(12, (int)Math.Round(16 * scaleX));
        int statusInnerRightPadding = Math.Max(8, (int)Math.Round(10 * scaleX));
        int rowHeight = Math.Max(40, (int)Math.Round(46 * scaleY));
        int baseRowY = Math.Max(0, (int)Math.Round(34 * scaleY));
        int rowLeft = Math.Max(0, (int)Math.Round(3 * scaleX));
        int nameX = Math.Max(0, (int)Math.Round(12 * scaleX));
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

    private void CriarCamposExtrasRuntime()
    {
        _txtVersao = CriarCampoExtra("Versao *");
        _txtDpi = CriarCampoExtra("DPI");
        _txtLargura = CriarCampoExtra("Largura (mm)");
        _txtAltura = CriarCampoExtra("Altura (mm)");
        _txtObservacao = CriarCampoExtra("Observacao");
        _txtVersao.Text = "1";
        _txtDpi.Text = "203";
    }

    private TextBox CriarCampoExtra(string rotuloTexto)
    {
        Label rotulo = new()
        {
            Name = $"lbl{rotuloTexto}Runtime",
            Text = rotuloTexto,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = nomePerfilLabel.ForeColor,
            Font = nomePerfilLabel.Font,
            BackColor = Color.Transparent
        };

        RoundedPanel painel = new()
        {
            Name = $"pnl{rotuloTexto}Runtime",
            BorderRadius = nomePerfilInputPanel is RoundedPanel rp ? rp.BorderRadius : 6,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(214, 219, 226),
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };

        TextBox campo = new()
        {
            Name = $"txt{rotuloTexto}Runtime",
            BorderStyle = BorderStyle.None,
            Font = nomePerfilTextBox.Font,
            BackColor = Color.White
        };

        painel.Controls.Add(campo);
        detailsCard.Controls.Add(rotulo);
        detailsCard.Controls.Add(painel);
        rotulo.BringToFront();
        painel.BringToFront();
        campo.BringToFront();

        _camposExtras.Add((rotulo, painel, campo));
        return campo;
    }

    private static int ParseIntOuPadrao(string? texto, int padrao)
        => int.TryParse((texto ?? string.Empty).Trim(), out int v) ? v : padrao;

    private static int? ParseIntNullable(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return int.TryParse(texto.Trim(), out int v) ? v : null;
    }

    private static decimal? ParseDecimalNullable(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        string limpo = texto.Trim().Replace(',', '.');
        return decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v) ? v : null;
    }

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
