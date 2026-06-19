using FugaPET_Dev.Tela.Controls;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class PermissaoForm : Form
{
private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
    private System.Windows.Forms.Timer? _footerClockTimer;
    private readonly List<ProfileRowSelection> _profileRowSelections = new();
    private readonly List<ProfileSearchRow> _profileSearchRows = new();
    private readonly Dictionary<Panel, Panel> _rowSelectionMarkers = new();
    private readonly Dictionary<Panel, long> _idPermissaoPorLinha = new();
    private readonly Dictionary<Panel, PermissaoCadastro> _permissaoPorLinha = new();
    private readonly List<PermissaoCadastro> _permissoesCarregadas = new();
    private readonly List<PerfilAcessoCadastro> _perfisAcessoCarregados = new();
    private readonly HashSet<long> _codigosPermissaoPerfilSelecionado = new();
    private readonly ToolTip _toolTipPerfil = new();
    private readonly PermissaoController _permissaoController;
    private readonly PerfilAcessoController _perfilAcessoController;
    private long _idPermissaoAtual;
    private static readonly Color StatusAtivoFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color StatusAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusInativoFundo = Color.FromArgb(255, 237, 213);
    private static readonly Color StatusInativoTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color StatusNeutroFundo = Color.FromArgb(243, 244, 246);
    private static readonly Color StatusNeutroTexto = Color.FromArgb(100, 116, 139);
    private static readonly Color ResumoSituacaoAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color ResumoSituacaoInativoTexto = Color.FromArgb(220, 38, 38);
    private static readonly Color ResumoSituacaoNeutroTexto = Color.FromArgb(100, 116, 139);
    private bool _atualizandoWorkspace;
    private bool _atualizandoFiltroPerfil;

    public PermissaoForm(PermissaoController? permissaoController = null, PerfilAcessoController? perfilAcessoController = null)
    {
        _permissaoController = permissaoController ?? FabricaControladoresCadastro.CriarPermissaoController();
        _perfilAcessoController = perfilAcessoController ?? FabricaControladoresCadastro.CriarPerfilAcessoController();
        InitializeComponent();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        ConfigureFooterDate();
        ConfigurePermissionSummaryTracking();
        ConfigureNovoPerfilAction();
        ConfigureProfilesSearchFilter();
        InitializeProfilesTableSelection();
        headerTitleLabel.Text = "Permissão";
        headerSubtitleLabel.Text = "Cadastro e manutenção de permissões / Integração SAP";
        profilesTitleLabel.Text = "Permissões Cadastradas";
        profilesHeaderProfileLabel.Text = "Módulo / Rotina";
        profilesHeaderUsersLabel.Text = "Ação";
        summaryPerfilCaptionLabel.Text = "Módulo";
        summaryNivelCaptionLabel.Text = "Rotina";
        summaryUsuariosCaptionLabel.Text = "Ação";
        novoPerfilTextLabel.Text = "Nova Permissão";
summaryTipTextLabel.AutoEllipsis = true;
        summaryTipTextLabel.AutoSize = false;
        summaryTipTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        ApplyProfilesFilter();
        AtualizarRodapePermissoes();
        AtualizarTipCadastroPermissao(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        ConectarAcoesCadastro();
        ConectarFiltroPerfil();
        if (_integracaoBancoHabilitada)
        {
            Shown += async (_, _) => await InicializarDadosBancoAsync();
        }
    }

    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) => await SalvarPermissaoAsync();
        novoButton.Click += async (_, _) => await EditarPermissaoAsync();
        excluirButton.Click += async (_, _) => await ExcluirPermissaoAsync();
    }

    private void ConectarFiltroPerfil()
    {
        if (_filtroPerfilComboBox is null)
        {
            return;
        }

        _filtroPerfilComboBox.SelectedIndexChanged += FiltroPerfilComboBox_SelectedIndexChanged;
    }

    private async Task InicializarDadosBancoAsync()
    {
        await CarregarPerfisAcessoAsync();
        await CarregarPermissoesAsync();
    }

    private async Task CarregarPerfisAcessoAsync()
    {
        IReadOnlyList<PerfilAcessoCadastro> perfis = await _perfilAcessoController.ListarAsync();
        _perfisAcessoCarregados.Clear();
        _perfisAcessoCarregados.AddRange(perfis.Where(perfil => perfil.SituacaoPerfilAcesso));

        if (_filtroPerfilComboBox is null)
        {
            return;
        }

        _atualizandoFiltroPerfil = true;
        try
        {
            _filtroPerfilComboBox.BeginUpdate();
            _filtroPerfilComboBox.Items.Clear();
            _filtroPerfilComboBox.Items.Add(PerfilFiltroItem.Todos);

            foreach (PerfilAcessoCadastro perfil in _perfisAcessoCarregados.OrderBy(perfil => perfil.NomePerfilAcesso))
            {
                _filtroPerfilComboBox.Items.Add(new PerfilFiltroItem(perfil.IdPerfilAcesso, perfil.NomePerfilAcesso));
            }

            _filtroPerfilComboBox.SelectedIndex = 0;
        }
        finally
        {
            _filtroPerfilComboBox.EndUpdate();
            _atualizandoFiltroPerfil = false;
        }
    }

    private async Task SalvarPermissaoAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Com uma permissão selecionada, Salvar atualiza o registro; sem seleção, insere um novo.
        if (_idPermissaoAtual > 0)
        {
            await AtualizarPermissaoSelecionadaAsync();
            return;
        }

        PermissaoCadastro permissao = MontarPermissaoDosCampos(0);
        var resultado = await _permissaoController.InserirAsync(permissao);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Permissão", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idPermissaoAtual = resultado.IdGerado ?? 0;
            PrepareNewPermissao();
            await CarregarPermissoesAsync();
        }
    }

    private PermissaoCadastro MontarPermissaoDosCampos(long idPermissao) => new()
    {
        IdPermissao = idPermissao,
        ModuloPermissao = nomePerfilTextBox.Text.Trim(),
        RotinaPermissao = nivelAcessoComboBox.Text.Trim(),
        AcaoPermissao = usuariosTextBox.Text.Trim(),
        DescricaoPermissao = descricaoTextBox.Text.Trim(),
        SituacaoPermissao = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase)
    };

    private async Task AtualizarPermissaoSelecionadaAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idPermissaoAtual <= 0)
        {
            MessageBox.Show("Selecione uma permissão na lista para editar.", "Cadastro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PermissaoCadastro permissao = MontarPermissaoDosCampos(_idPermissaoAtual);
        var resultado = await _permissaoController.AtualizarAsync(permissao);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Permissão", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idPermissaoAtual = 0;
            PrepareNewPermissao();
            await CarregarPermissoesAsync();
        }
    }

    private async Task ExcluirPermissaoAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idPermissaoAtual <= 0)
        {
            MessageBox.Show("Salve ou selecione uma permissão para excluir.", "Cadastro de Permissão", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            "Confirma a exclusao da permissão atual?",
            "Cadastro de Permissão",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes)
        {
            return;
        }

        var resultado = await _permissaoController.ExcluirAsync(_idPermissaoAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Permissão", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idPermissaoAtual = 0;
            PrepareNewPermissao();
            await CarregarPermissoesAsync();
        }
    }

    private async Task CarregarPermissoesAsync()
    {
        try
        {
            IReadOnlyList<PermissaoCadastro> permissoes = await _permissaoController.ListarAsync();
            _permissoesCarregadas.Clear();
            _permissoesCarregadas.AddRange(permissoes);
            RecriarLinhasPermissoes();
            PopularLinhasComPermissoes(_permissoesCarregadas);
            AtualizarRodapePermissoes();
            ClearSummarySelectionValues();
            AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
            AtualizarTipCadastroPermissao(null);
            ApplyProfilesFilter();
            AtualizarWorkspacePermissao();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await FugaPET_Dev.Tela.Comum.ErroUsuarioHelper.TratarAsync("PERMISSAO_CARREGAR_ERRO", ex, "PermissaoForm",
                    "Não foi possível carregar as permissões. Acione o suporte."),
                "Cadastro de Permissão",
                MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        }
    }

    private async void FiltroPerfilComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_atualizandoFiltroPerfil)
        {
            return;
        }

        await CarregarPermissoesDoPerfilSelecionadoAsync();
    }

    private async Task CarregarPermissoesDoPerfilSelecionadoAsync()
    {
        _codigosPermissaoPerfilSelecionado.Clear();
        long codigoPerfil = ObterCodigoPerfilSelecionado();

        if (codigoPerfil > 0)
        {
            IReadOnlyList<long> codigos = await _permissaoController.ListarCodigosPermissaoPorPerfilAsync(codigoPerfil);
            foreach (long codigo in codigos)
            {
                _codigosPermissaoPerfilSelecionado.Add(codigo);
            }
        }

        AtualizarWorkspacePermissao();
    }

    private async Task EditarPermissaoAsync()
    {
        // Edita a permissão selecionada com os valores atuais dos campos.
        // O grid e a matriz são recarregados em AtualizarPermissaoSelecionadaAsync -> CarregarPermissoesAsync.
        await AtualizarPermissaoSelecionadaAsync();
    }

    private void RecriarLinhasPermissoes()
    {
        RemoverLinhasPermissoesExistentes();

        for (int i = 0; i < _permissoesCarregadas.Count; i++)
        {
            ProfileSearchRow linha = CriarLinhaPerfil(i);
            _profileSearchRows.Add(linha);
            ProfileRowSelection selecao = new(linha.RowPanel, null);
            _profileRowSelections.Add(selecao);
            SetHandCursor(linha.RowPanel);
            AttachRowSelectionHandlers(linha.RowPanel, selecao);
            CriarMarcadorLinha(linha.RowPanel);
            profilesTablePanel.Controls.Add(linha.RowPanel);
            linha.RowPanel.BringToFront();
        }

        AtualizarLayoutLinhasDinamicas();
        HideAllMarkers();
    }

    private void RemoverLinhasPermissoesExistentes()
    {
        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            profilesTablePanel.Controls.Remove(row.RowPanel);
            row.RowPanel.Dispose();
        }

        _profileSearchRows.Clear();
        _idPermissaoPorLinha.Clear();
        _permissaoPorLinha.Clear();
        _profileRowSelections.Clear();
        _rowSelectionMarkers.Clear();
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
            TextAlign = ContentAlignment.MiddleLeft,
            Font = profilesHeaderProfileLabel.Font,
            ForeColor = profilesHeaderProfileLabel.ForeColor
        };

        Label usersLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}UsersLabel",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = profilesHeaderUsersLabel.Font,
            ForeColor = profilesHeaderUsersLabel.ForeColor,
            Text = "-"
        };

        RoundedPanel statusPanel = new()
        {
            Name = $"profileDynamicRow{indice + 1}StatusPanel",
            BorderRadius = 8,
            FillColor = StatusNeutroFundo,
            BorderColor = StatusNeutroTexto,
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };

        Label statusLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}StatusLabel",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = profilesHeaderStatusLabel.Font,
            ForeColor = StatusNeutroTexto
        };

        statusPanel.Controls.Add(statusLabel);
        rowPanel.Controls.Add(nameLabel);
        rowPanel.Controls.Add(usersLabel);
        rowPanel.Controls.Add(statusPanel);
        return new ProfileSearchRow(rowPanel, nameLabel, usersLabel, statusLabel);
    }

    private void PopularLinhasComPermissoes(IReadOnlyList<PermissaoCadastro> permissoes)
    {
        _idPermissaoPorLinha.Clear();
        _permissaoPorLinha.Clear();

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            PermissaoCadastro permissao = permissoes[i];
            ProfileSearchRow linha = _profileSearchRows[i];
            linha.NameLabel.Text = $"{permissao.ModuloPermissao} / {permissao.RotinaPermissao}";
            _toolTipPerfil.SetToolTip(linha.NameLabel, $"{permissao.ModuloPermissao} / {permissao.RotinaPermissao}");
            linha.UsersLabel.Text = permissao.AcaoPermissao;
            linha.StatusLabel.Text = permissao.SituacaoPermissao ? "Ativo" : "Inativo";
            AtualizarBadgeStatus(linha.RowPanel, linha.StatusLabel.Text);
            _idPermissaoPorLinha[linha.RowPanel] = permissao.IdPermissao;
            _permissaoPorLinha[linha.RowPanel] = permissao;
        }
    }

    private void ConfigureNovoPerfilAction()
    {
        SetHandCursor(novoPerfilButtonPanel);
        AttachNovoPerfilClick(novoPerfilButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;

        foreach (Control child in control.Controls)
        {
            SetHandCursor(child);
        }
    }

    private void AttachNovoPerfilClick(Control control)
    {
        control.Click += (_, _) => PrepareNewPermissao();

        foreach (Control child in control.Controls)
        {
            AttachNovoPerfilClick(child);
        }
    }

    private void PrepareNewPermissao()
    {
        _idPermissaoAtual = 0;
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        situacaoComboBox.SelectedIndex = -1;
        situacaoComboBox.Text = string.Empty;
        nivelAcessoComboBox.SelectedIndex = -1;
        nivelAcessoComboBox.Text = string.Empty;

        foreach (CheckBox permissionCheckBox in GetPermissionCheckBoxes(detailsCard))
        {
            permissionCheckBox.Checked = false;
        }

        nomePerfilTextBox.Focus();
        nomePerfilTextBox.SelectionStart = 0;
        nomePerfilTextBox.SelectionLength = 0;
        AtualizarTipCadastroPermissao(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.SomenteSalvar);
    }

    private void ConfigureProfilesSearchFilter()
    {
        searchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void ApplyProfilesFilter()
    {
        string query = NormalizeForSearch(searchTextBox.Text.Trim());
        int visibleIndex = 0;
        int rowHeight = _profileSearchRows.Count > 0 ? _profileSearchRows[0].RowPanel.Height : 46;
        int rowTop = _profileSearchRows.Count > 0 ? _profileSearchRows[0].RowPanel.Top : 34;
        int rowLeft = _profileSearchRows.Count > 0 ? _profileSearchRows[0].RowPanel.Left : 3;

        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            string rowName = NormalizeForSearch(row.NameLabel.Text);
            bool match = string.IsNullOrWhiteSpace(query)
                || rowName.Contains(query, StringComparison.Ordinal);

            row.RowPanel.Visible = match;

            if (!match)
            {
                continue;
            }

            int top = rowTop + (rowHeight * visibleIndex);
            row.RowPanel.Location = new Point(rowLeft, top);
            visibleIndex++;
        }

        AtualizarAreaRolagem(visibleIndex);
        AtualizarRodapePermissoes(visibleIndex);

        if (string.IsNullOrWhiteSpace(query))
        {
            ClearRowSelection();
            return;
        }

        if (visibleIndex > 0)
        {
            SetFilteredRowsSelected();
            return;
        }

        HideAllMarkers();
    }

    private void ClearRowSelection()
    {
        foreach (ProfileRowSelection row in _profileRowSelections)
        {
            row.RowPanel.BackColor = Color.White;
        }

        HideAllMarkers();
        ClearSummarySelectionValues();
    }

    private void SetFilteredRowsSelected()
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);

        foreach (ProfileRowSelection row in _profileRowSelections)
        {
            bool isVisible = row.RowPanel.Visible;
            row.RowPanel.BackColor = isVisible ? selectedBackColor : Color.White;

            if (isVisible)
            {
                ShowMarkerForRow(row.RowPanel);
            }
            else
            {
                HideMarkerForRow(row.RowPanel);
            }
        }

        // Busca só destaca visualmente; resumo só muda no clique da linha.
        ClearSummarySelectionValues();
    }

    private static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

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

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void LoadWindowIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, WindowIconPath);

        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }
    }

    private void ConfigurePermissionSummaryTracking()
    {
        foreach (CheckBox permissionCheckBox in GetPermissionCheckBoxes(detailsCard))
        {
            permissionCheckBox.CheckedChanged += (_, _) => UpdateSummaryPermissionsCount();
        }

        UpdateSummaryPermissionsCount();
    }

    private static IEnumerable<CheckBox> GetPermissionCheckBoxes(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is CheckBox checkBox && child.Name.StartsWith("permissao", StringComparison.Ordinal))
            {
                yield return checkBox;
            }

            if (child.HasChildren)
            {
                foreach (CheckBox nested in GetPermissionCheckBoxes(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private void UpdateSummaryPermissionsCount()
    {
        int checkedCount = GetPermissionCheckBoxes(detailsCard).Count(checkBox => checkBox.Checked);
        summaryPermissionsValueLabel.Text = $"{checkedCount} habilitadas";
    }

    private void ConfigureCustomTitleBar()
    {
        customTitleBarPanel.MouseDown += CustomTitleBar_MouseDown;
        companyLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;
        menuHeaderLabel.Click += (_, _) => Close();
        customTitleBarPanel.Resize += (_, _) => AlignHeaderRightControls();

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));
        //voltarButton.Click += (_, _) => Close();
        AlignHeaderRightControls();
    }

    private void AlignHeaderRightControls()
    {
        int right = customTitleBarPanel.ClientSize.Width;

        closeWindowLabel.Location = new Point(right - 52, 0);
        maximizeWindowLabel.Location = new Point(right - 100, 0);
        minimizeWindowLabel.Location = new Point(right - 148, 0);

        closeWindowLabel.BringToFront();
        maximizeWindowLabel.BringToFront();
        minimizeWindowLabel.BringToFront();
    }

    private void CustomTitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private static void ConfigureTitleButtonHover(Label button, Color hoverColor)
    {
        Color normalColor = button.BackColor;

        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = normalColor;
    }

    private void ConfigureFooterDate()
    {
        UpdateFooterDateTime();

        _footerClockTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        _footerClockTimer.Tick += (_, _) => UpdateFooterDateTime();
        _footerClockTimer.Start();
    }

    private void UpdateFooterDateTime()
    {
        var ptBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        DateTime now = DateTime.Now;
        cellDataText.Text = now.ToString("dd/MM/yyyy", ptBr);
        cellHoraText.Text = now.ToString("HH:mm", ptBr);
    }

    private void LayoutProfilesCard()
    {
        const int baseCardWidth = 411;
        const int baseCardHeight = 596;

        if (profilesCard.Width <= 0 || profilesCard.Height <= 0)
        {
            return;
        }

        float scaleX = profilesCard.Width / (float)baseCardWidth;
        float scaleY = profilesCard.Height / (float)baseCardHeight;

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height)
        {
            control.Bounds = new Rectangle(x, y, width, height);
        }

        int leftMargin = Scale(16, scaleX);
        int rightMargin = Scale(16, scaleX);

        int headerIconLeft = Scale(16, scaleX);
        int headerIconTop = Scale(19, scaleY);
        int headerIconSize = Math.Max(20, Scale(26, Math.Min(scaleX, scaleY)));
        SetBounds(profilesCheckMarkLabel, headerIconLeft, headerIconTop, headerIconSize, headerIconSize);
        profilesCheckMarkLabel.Font = new Font("Segoe UI Symbol", Math.Max(12F, 14F * Math.Min(scaleX, scaleY)), FontStyle.Bold);
        profilesCheckMarkLabel.TextAlign = ContentAlignment.MiddleCenter;

        LayoutProfilesTitleLabel(profilesTitleLabel, profilesCheckMarkLabel.Right, duplicarButtonPanel.Left, scaleX, scaleY);

        int buttonTop = Scale(18, scaleY);
        int buttonHeight = Scale(27, scaleY);
        int duplicarWidth = Scale(99, scaleX);
        int novoWidth = Scale(99, scaleX);
        int gap = Scale(22, scaleX);

        SetBounds(duplicarButtonPanel, profilesCard.Width - rightMargin - duplicarWidth, buttonTop, duplicarWidth, buttonHeight);
        SetBounds(novoPerfilButtonPanel, duplicarButtonPanel.Left - gap - novoWidth, buttonTop, novoWidth, buttonHeight);
        LayoutToolbarButton(novoPerfilButtonPanel, novoPerfilIconLabel, novoPerfilTextLabel, scaleX, scaleY, "\uE710");
        LayoutToolbarButton(duplicarButtonPanel, duplicarIconLabel, duplicarTextLabel, scaleX, scaleY, "\uE8C8");

        SetBounds(profilesSearchPanel, leftMargin, Scale(60, scaleY), profilesCard.Width - leftMargin - rightMargin, Scale(34, scaleY));
        LayoutProfilesSearchPanel(profilesSearchPanel, profilesSearchIconLabel, searchTextBox, scaleX, scaleY);

        int tableTop = Scale(108, scaleY);
        int footerBottomMargin = Scale(8, scaleY);
        int footerHeight = Scale(22, scaleY);

        SetBounds(profilesFooterLabel, Scale(20, scaleX), profilesCard.Height - footerHeight - footerBottomMargin, Math.Max(180, Scale(180, scaleX)), footerHeight);
        SetBounds(profilesTablePanel, leftMargin, tableTop, profilesCard.Width - leftMargin - rightMargin, profilesFooterLabel.Top - tableTop - Scale(8, scaleY));

        SetBounds(profilesHeaderProfileLabel, Scale(16, scaleX), Scale(9, scaleY), Scale(138, scaleX), Scale(20, scaleY));
        SetBounds(profilesHeaderUsersLabel, Scale(192, scaleX), Scale(10, scaleY), Scale(102, scaleX), Scale(20, scaleY));
        SetBounds(profilesHeaderStatusLabel, Scale(311, scaleX), Scale(9, scaleY), Math.Max(48, Scale(52, scaleX)), Scale(20, scaleY));

        LayoutProfilesTableFonts(scaleX, scaleY);
        AtualizarLayoutLinhasDinamicas();
        UpdateMarkerSizes(Math.Max(3, Scale(3, scaleX)));
        ApplyProfilesFilter();
    }

    private static void LayoutToolbarButton(
        RoundedPanel buttonPanel,
        Label iconLabel,
        Label textLabel,
        float scaleX,
        float scaleY,
        string iconGlyph)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));

        int iconSize = Math.Max(14, Scale(18, Math.Min(scaleX, scaleY)));
        int textLeft = iconSize + Scale(10, scaleX);
        int textWidth = Math.Max(30, buttonPanel.Width - textLeft - Scale(10, scaleX));
        int verticalPadding = Math.Max(0, (buttonPanel.Height - iconSize) / 2);

        iconLabel.Bounds = new Rectangle(Scale(8, scaleX), verticalPadding, iconSize, iconSize);
        iconLabel.Font = new Font("Segoe MDL2 Assets", Math.Max(8F, 9F * Math.Min(scaleX, scaleY)));
        iconLabel.Text = iconGlyph;

        textLabel.Bounds = new Rectangle(textLeft, 0, textWidth, buttonPanel.Height);
        textLabel.Font = new Font("Segoe UI", Math.Max(7F, 7.5F * Math.Min(scaleX, scaleY)), FontStyle.Bold);
        textLabel.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static void LayoutProfilesTitleLabel(Label titleLabel, int iconRight, int duplicateButtonLeft, float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));

        int left = iconRight + Scale(10, scaleX);
        int top = Scale(18, scaleY);
        int width = Math.Max(120, duplicateButtonLeft - left - Scale(18, scaleX));
        int height = Math.Max(24, Scale(24, scaleY));
        float fontSize = Math.Max(8.5F, Math.Min(12F, 8.5F * Math.Min(scaleX, scaleY)));

        titleLabel.Bounds = new Rectangle(left, top, width, height);
        titleLabel.Font = new Font("Segoe UI", fontSize, FontStyle.Bold);
        titleLabel.TextAlign = ContentAlignment.MiddleLeft;
    }

    private static void LayoutProfilesSearchPanel(RoundedPanel searchPanel, Label searchIconLabel, TextBox searchTextBox, float scaleX, float scaleY)
    {
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));

        float contentScale = Math.Min(scaleX, scaleY);
        int iconLeft = Scale(8, scaleX);
        int iconWidth = Math.Max(20, Scale(22, scaleX));
        int iconHeight = Math.Max(20, Scale(24, scaleY));
        int textLeft = Scale(36, scaleX);
        int textHeight = Math.Max(16, searchTextBox.PreferredHeight);
        int textTop = Math.Max(2, (searchPanel.Height - textHeight) / 2);
        int textWidth = Math.Max(40, searchPanel.Width - Scale(52, scaleX));
        int iconTop = Math.Max(2, textTop + ((textHeight - iconHeight) / 2));

        searchIconLabel.Bounds = new Rectangle(iconLeft, iconTop, iconWidth, iconHeight);
        searchIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Max(10F, 11F * contentScale));
        searchIconLabel.TextAlign = ContentAlignment.MiddleCenter;

        searchTextBox.Multiline = false;
        searchTextBox.Bounds = new Rectangle(textLeft, textTop, textWidth, textHeight);
        searchTextBox.Font = new Font("Segoe UI", Math.Max(9F, 9F * contentScale));
    }

    private void LayoutProfilesTableFonts(float scaleX, float scaleY)
    {
        float contentScale = Math.Min(scaleX, scaleY);

        ApplyScaledFont(profilesHeaderProfileLabel, 7.5F, contentScale, 8.25F, 11F);
        ApplyScaledFont(profilesHeaderUsersLabel, 7.5F, contentScale, 8.25F, 11F);
        ApplyScaledFont(profilesHeaderStatusLabel, 7.5F, contentScale, 8.25F, 11F);

        ApplyScaledFont(profilesFooterLabel, 8.5F, contentScale, 9F, 11.5F);
    }

    private void LayoutDetailsCard()
    {
        const int baseCardWidth = 557;
        const int baseCardHeight = 596;

        if (detailsCard.Width <= 0 || detailsCard.Height <= 0)
        {
            return;
        }

        float scaleX = detailsCard.Width / (float)baseCardWidth;
        float scaleY = detailsCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);
        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height)
        {
            control.Bounds = new Rectangle(x, y, width, height);
        }

        int leftMargin = Scale(24, scaleX);
        int rightMargin = Scale(24, scaleX);
        int columnGap = Scale(28, scaleX);
        int cardInnerWidth = Math.Max(200, detailsCard.Width - leftMargin - rightMargin);
        int fieldWidth = Math.Max(Scale(238, scaleX), (detailsCard.Width - leftMargin - rightMargin - columnGap) / 2);
        int leftColumnX = leftMargin;
        int rightColumnX = Math.Min(detailsCard.Width - rightMargin - fieldWidth, leftColumnX + fieldWidth + columnGap);
        int fieldPanelHeight = Math.Max(Scale(33, scaleY), 33);
        int multiLinePanelHeight = Math.Max(Scale(95, scaleY), 95);
        int labelHeight = Math.Max(Scale(14, scaleY), 14);
        float headerScale = Math.Min(scaleX, scaleY);
        int titleHeight = Math.Max(24, Scale(24, headerScale));
        int iconSize = Math.Max(20, Scale(26, headerScale));
        int headerTop = Scale(18, scaleY);
        int titleTop = headerTop + Math.Max(0, (iconSize - titleHeight) / 2);
        int titleLeft = Scale(20, scaleX) + iconSize + Scale(8, headerScale);

        ApplyScaledFont(detailsTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(detailsTitleIconLabel, 14F, contentScale, 14F, 18F);
        ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissionsTitleLabel, 8.5F, contentScale, 9F, 12F);

        ApplyScaledFont(nomePerfilTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(descricaoTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        situacaoComboBox.IntegralHeight = false;
        situacaoComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));

        SetBounds(detailsTitleIconLabel, Scale(20, scaleX), headerTop, iconSize, iconSize);
        SetBounds(detailsTitleLabel, titleLeft, titleTop, Math.Max(180, Scale(180, scaleX)), titleHeight);

        SetBounds(nomePerfilLabel, leftColumnX, Scale(56, scaleY), Scale(180, scaleX), labelHeight);
        SetBounds(nomePerfilInputPanel, leftColumnX, Scale(73, scaleY), fieldWidth, fieldPanelHeight);
        SetBounds(nomePerfilTextBox, Scale(12, scaleX), Math.Max(4, (fieldPanelHeight - Scale(16, scaleY)) / 2), Math.Max(10, fieldWidth - Scale(24, scaleX)), Math.Max(16, Scale(16, scaleY)));

        SetBounds(situacaoLabel, rightColumnX, Scale(56, scaleY), Scale(180, scaleX), labelHeight);
        SetBounds(situacaoInputPanel, rightColumnX, Scale(73, scaleY), fieldWidth, fieldPanelHeight);
        int situacaoComboWidth = Math.Max(10, fieldWidth - Scale(24, scaleX));
        int situacaoComboHeight = Math.Max(22, situacaoComboBox.PreferredHeight);
        int situacaoComboY = Math.Max(2, (fieldPanelHeight - situacaoComboHeight) / 2);
        SetBounds(situacaoComboBox, Scale(12, scaleX), situacaoComboY, situacaoComboWidth, situacaoComboHeight);

        SetBounds(descricaoLabel, leftColumnX, Scale(118, scaleY), Scale(180, scaleX), Math.Max(labelHeight, Scale(19, scaleY)));
        int descricaoWidth = Math.Max(10, cardInnerWidth);
        int descricaoHeight = Math.Max(multiLinePanelHeight, Scale(95, scaleY));
        SetBounds(descricaoInputPanel, leftColumnX, Scale(140, scaleY), descricaoWidth, descricaoHeight);
        SetBounds(descricaoTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(10, descricaoWidth - Scale(24, scaleX)), Math.Max(40, descricaoHeight - Scale(20, scaleY)));

        ApplyScaledFont(permissaoPainelLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoConsultaLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoRelatoriosLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoLeituraLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoEtiquetasLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoSapLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoOrdensLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoHistoricoLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoConfigLabel, 8.5F, contentScale, 9F, 12F);
        ApplyScaledFont(permissaoCadastroLabel, 8.5F, contentScale, 9F, 12F);

        ApplyScaledFont(permissaoPainelIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoConsultaIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoRelatoriosIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoLeituraIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoEtiquetasIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoSapIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoOrdensIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoHistoricoIconLabel, 15F, contentScale, 15F, 20F);
        ApplyScaledFont(permissaoConfigIconLabel, 15F, contentScale, 15F, 20F);

        ApplyScaledFont(permissaoPainelVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoConsultaVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoRelatoriosVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoLeituraVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoLeituraExecutarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoEtiquetasVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoEtiquetasImprimirCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoSapVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoOrdensVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoHistoricoVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoConfigSemAcessoCheckBox, 8F, contentScale, 8.5F, 11F);
        ApplyScaledFont(permissaoCadastroVisualizarCheckBox, 8F, contentScale, 8.5F, 11F);

        SetBounds(detailsTopDividerLabel, Scale(24, scaleX), Scale(242, scaleY), Math.Max(1, Scale(506, scaleX)), Scale(1, scaleY));
        SetBounds(permissionsTitleLabel, Scale(24, scaleX), Scale(250, scaleY), Scale(220, scaleX), titleHeight);
        SetBounds(permissionsDividerLabel, Scale(24, scaleX), Scale(504, scaleY), Math.Max(1, Scale(506, scaleX)), Scale(1, scaleY));
        SetBounds(permissionsVerticalDivider1, Scale(190, scaleX), Scale(279, scaleY), Scale(1, scaleX), Scale(225, scaleY));
        SetBounds(permissionsVerticalDivider2, Scale(361, scaleX), Scale(279, scaleY), Scale(1, scaleX), Scale(225, scaleY));
        SetBounds(permissionsHorizontalDivider1, Scale(24, scaleX), Scale(329, scaleY), Math.Max(1, Scale(506, scaleX)), Scale(1, scaleY));
        SetBounds(permissionsHorizontalDivider2, Scale(24, scaleX), Scale(386, scaleY), Math.Max(1, Scale(506, scaleX)), Scale(1, scaleY));
        SetBounds(permissionsHorizontalDivider3, Scale(24, scaleX), Scale(444, scaleY), Math.Max(1, Scale(506, scaleX)), Scale(1, scaleY));

        SetBounds(permissaoPainelIconLabel, Scale(24, scaleX), Scale(286, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoPainelLabel, Scale(52, scaleX), Scale(286, scaleY), Scale(126, scaleX), titleHeight);
        SetBounds(permissaoPainelVisualizarCheckBox, Scale(52, scaleX), Scale(309, scaleY), Scale(82, scaleX), Scale(17, scaleY));

        SetBounds(permissaoConsultaIconLabel, Scale(205, scaleX), Scale(286, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoConsultaLabel, Scale(233, scaleX), Scale(286, scaleY), Scale(125, scaleX), titleHeight);
        SetBounds(permissaoConsultaVisualizarCheckBox, Scale(233, scaleX), Scale(309, scaleY), Scale(82, scaleX), Scale(20, scaleY));

        SetBounds(permissaoRelatoriosIconLabel, Scale(376, scaleX), Scale(286, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoRelatoriosLabel, Scale(404, scaleX), Scale(286, scaleY), Scale(150, scaleX), titleHeight);
        SetBounds(permissaoRelatoriosVisualizarCheckBox, Scale(404, scaleX), Scale(309, scaleY), Scale(82, scaleX), Scale(20, scaleY));

        SetBounds(permissaoLeituraIconLabel, Scale(24, scaleX), Scale(344, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoLeituraLabel, Scale(52, scaleX), Scale(344, scaleY), Scale(139, scaleX), titleHeight);
        SetBounds(permissaoLeituraVisualizarCheckBox, Scale(48, scaleX), Scale(367, scaleY), Scale(74, scaleX), Scale(17, scaleY));
        SetBounds(permissaoLeituraExecutarCheckBox, Scale(118, scaleX), Scale(367, scaleY), Scale(66, scaleX), Scale(17, scaleY));

        SetBounds(permissaoEtiquetasIconLabel, Scale(205, scaleX), Scale(344, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoEtiquetasLabel, Scale(233, scaleX), Scale(344, scaleY), Scale(126, scaleX), titleHeight);
        SetBounds(permissaoEtiquetasVisualizarCheckBox, Scale(214, scaleX), Scale(367, scaleY), Scale(84, scaleX), Scale(17, scaleY));
        SetBounds(permissaoEtiquetasImprimirCheckBox, Scale(292, scaleX), Scale(367, scaleY), Scale(66, scaleX), Scale(17, scaleY));

        SetBounds(permissaoSapIconLabel, Scale(376, scaleX), Scale(344, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoSapLabel, Scale(404, scaleX), Scale(344, scaleY), Scale(150, scaleX), titleHeight);
        SetBounds(permissaoSapVisualizarCheckBox, Scale(404, scaleX), Scale(367, scaleY), Scale(82, scaleX), Scale(20, scaleY));

        SetBounds(permissaoOrdensIconLabel, Scale(24, scaleX), Scale(402, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoOrdensLabel, Scale(52, scaleX), Scale(402, scaleY), Scale(136, scaleX), titleHeight);
        SetBounds(permissaoOrdensVisualizarCheckBox, Scale(52, scaleX), Scale(425, scaleY), Scale(82, scaleX), Scale(20, scaleY));

        SetBounds(permissaoHistoricoIconLabel, Scale(205, scaleX), Scale(402, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoHistoricoLabel, Scale(233, scaleX), Scale(402, scaleY), Scale(126, scaleX), titleHeight);
        SetBounds(permissaoHistoricoVisualizarCheckBox, Scale(233, scaleX), Scale(425, scaleY), Scale(82, scaleX), Scale(20, scaleY));

        SetBounds(permissaoConfigIconLabel, Scale(376, scaleX), Scale(402, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoConfigLabel, Scale(404, scaleX), Scale(402, scaleY), Scale(150, scaleX), titleHeight);
        SetBounds(permissaoConfigSemAcessoCheckBox, Scale(404, scaleX), Scale(425, scaleY), Scale(82, scaleX), Scale(20, scaleY));

        SetBounds(permissaoCadastroIconLabel, Scale(24, scaleX), Scale(460, scaleY), Scale(22, scaleX), Scale(22, scaleY));
        SetBounds(permissaoCadastroLabel, Scale(52, scaleX), Scale(460, scaleY), Scale(126, scaleX), titleHeight);
        SetBounds(permissaoCadastroVisualizarCheckBox, Scale(52, scaleX), Scale(483, scaleY), Scale(82, scaleX), Scale(17, scaleY));

        detailsTopDividerLabel.SendToBack();
        permissionsDividerLabel.SendToBack();
        permissionsVerticalDivider1.SendToBack();
        permissionsVerticalDivider2.SendToBack();
        permissionsHorizontalDivider1.SendToBack();
        permissionsHorizontalDivider2.SendToBack();
        permissionsHorizontalDivider3.SendToBack();

        permissionsTitleLabel.BringToFront();
        permissaoPainelIconLabel.BringToFront();
        permissaoPainelLabel.BringToFront();
        permissaoPainelVisualizarCheckBox.BringToFront();
        permissaoConsultaIconLabel.BringToFront();
        permissaoConsultaLabel.BringToFront();
        permissaoConsultaVisualizarCheckBox.BringToFront();
        permissaoRelatoriosIconLabel.BringToFront();
        permissaoRelatoriosLabel.BringToFront();
        permissaoRelatoriosVisualizarCheckBox.BringToFront();
        permissaoLeituraIconLabel.BringToFront();
        permissaoLeituraLabel.BringToFront();
        permissaoLeituraVisualizarCheckBox.BringToFront();
        permissaoLeituraExecutarCheckBox.BringToFront();
        permissaoEtiquetasIconLabel.BringToFront();
        permissaoEtiquetasLabel.BringToFront();
        permissaoEtiquetasVisualizarCheckBox.BringToFront();
        permissaoEtiquetasImprimirCheckBox.BringToFront();
        permissaoSapIconLabel.BringToFront();
        permissaoSapLabel.BringToFront();
        permissaoSapVisualizarCheckBox.BringToFront();
        permissaoOrdensIconLabel.BringToFront();
        permissaoOrdensLabel.BringToFront();
        permissaoOrdensVisualizarCheckBox.BringToFront();
        permissaoHistoricoIconLabel.BringToFront();
        permissaoHistoricoLabel.BringToFront();
        permissaoHistoricoVisualizarCheckBox.BringToFront();
        permissaoConfigIconLabel.BringToFront();
        permissaoConfigLabel.BringToFront();
        permissaoConfigSemAcessoCheckBox.BringToFront();
        permissaoCadastroIconLabel.BringToFront();
        permissaoCadastroLabel.BringToFront();
        permissaoCadastroVisualizarCheckBox.BringToFront();
    }

    private void LayoutSummaryCard()
    {
        const int baseCardWidth = 326;
        const int baseCardHeight = 590;

        if (summaryCard.Width <= 0 || summaryCard.Height <= 0)
        {
            return;
        }

        float scaleX = summaryCard.Width / (float)baseCardWidth;
        float scaleY = summaryCard.Height / (float)baseCardHeight;
        float contentScale = Math.Min(scaleX, scaleY);

        static int Scale(int value, float scale) => Math.Max(0, (int)Math.Round(value * scale));
        static void SetBounds(Control control, int x, int y, int width, int height)
        {
            control.Bounds = new Rectangle(x, y, width, height);
        }

        int sideMargin = Scale(24, scaleX);
        int innerLeft = Scale(32, scaleX);
        int textLeft = Scale(74, scaleX);
        int labelWidth = Math.Max(160, summaryCard.Width - Scale(98, scaleX));
        int dividerWidth = Math.Max(120, summaryCard.Width - Scale(52, scaleX));
        int buttonX = Scale(24, scaleX);
        int buttonWidth = Math.Max(220, summaryCard.Width - Scale(48, scaleX));
        int buttonHeight = Math.Max(28, Scale(28, scaleY));
        int titleHeight = Math.Max(24, Scale(24, scaleY));
        int iconSize = Math.Max(32, Scale(32, scaleX));

        ApplyScaledFont(summaryTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(summaryPerfilCaptionLabel, 8.5F, contentScale, 8.5F, 11.5F);
        ApplyScaledFont(summaryPerfilValueLabel, 8.5F, contentScale, 8.5F, 12F);
        ApplyScaledFont(summaryNivelCaptionLabel, 8.5F, contentScale, 8.5F, 11.5F);
        ApplyScaledFont(summaryNivelValueLabel, 8.5F, contentScale, 8.5F, 12F);
        ApplyScaledFont(summarySituacaoCaptionLabel, 8.5F, contentScale, 8.5F, 11.5F);
        ApplyScaledFont(summarySituacaoValueLabel, 8.5F, contentScale, 8.5F, 12F);
        ApplyScaledFont(summaryPermissionsIconLabel, 16F, contentScale, 13F, 18F);
        ApplyScaledFont(summaryPermissionsCaptionLabel, 8.5F, contentScale, 8.5F, 11.5F);
        ApplyScaledFont(summaryPermissionsValueLabel, 8.5F, contentScale, 8.5F, 12F);
        ApplyScaledFont(summaryUsuariosCaptionLabel, 8.5F, contentScale, 8.5F, 11.5F);
        ApplyScaledFont(summaryUsuariosValueLabel, 8.5F, contentScale, 8.5F, 12F);
        ApplyScaledFont(summaryTipTextLabel, 8.5F, contentScale, 8.5F, 11.5F);
        ApplyScaledFont(salvarButton, 8F, contentScale, 8F, 11F);
        ApplyScaledFont(novoButton, 8F, contentScale, 8F, 11F);
        //ApplyScaledFont(duplicarPerfilButton, 8F, contentScale, 8F, 11F);
        ApplyScaledFont(excluirButton, 8F, contentScale, 8F, 11F);
        //ApplyScaledFont(voltarButton, 8F, contentScale, 8F, 11F);

        //SetBounds(summaryTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), iconSize, iconSize);
        SetBounds(summaryTitleLabel, Scale(54, scaleX), Scale(19, scaleY), Scale(180, scaleX), titleHeight);
        SetBounds(summaryDividerLabel, Scale(26, scaleX), Scale(56, scaleY), dividerWidth, Scale(1, scaleY));

        SetBounds(summaryPerfilIconLabel, innerLeft, Scale(78, scaleY), iconSize, iconSize);
        SetBounds(summaryPerfilCaptionLabel, textLeft, Scale(78, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryPerfilValueLabel, textLeft, Scale(98, scaleY), labelWidth, titleHeight);

        SetBounds(summaryNivelIconLabel, innerLeft, Scale(132, scaleY), iconSize, iconSize);
        SetBounds(summaryNivelCaptionLabel, textLeft, Scale(132, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryNivelValueLabel, textLeft, Scale(152, scaleY), labelWidth, titleHeight);

        SetBounds(summarySituacaoIconLabel, innerLeft, Scale(186, scaleY), iconSize, iconSize);
        SetBounds(summarySituacaoCaptionLabel, textLeft, Scale(186, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summarySituacaoValueLabel, textLeft, Scale(206, scaleY), labelWidth, titleHeight);

        SetBounds(summaryPermissionsIconLabel, innerLeft, Scale(240, scaleY), iconSize, iconSize);
        SetBounds(summaryPermissionsCaptionLabel, textLeft, Scale(240, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryPermissionsValueLabel, textLeft, Scale(260, scaleY), labelWidth, titleHeight);

        SetBounds(summaryUsuariosIconLabel, innerLeft, Scale(294, scaleY), iconSize, iconSize);
        SetBounds(summaryUsuariosCaptionLabel, textLeft, Scale(294, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryUsuariosValueLabel, textLeft, Scale(314, scaleY), labelWidth, titleHeight);

        SetBounds(summaryTipIconLabel, innerLeft, Scale(348, scaleY), iconSize, iconSize);
        SetBounds(summaryTipTextLabel, textLeft, Scale(348, scaleY), Math.Max(180, summaryCard.Width - Scale(98, scaleX)), Math.Max(32, Scale(48, scaleY)));

        SetBounds(salvarButton, buttonX, Scale(402, scaleY), buttonWidth, buttonHeight);
        SetBounds(novoButton, buttonX, Scale(434, scaleY), buttonWidth, buttonHeight);
        //SetBounds(duplicarPerfilButton, buttonX, Scale(440, scaleY), buttonWidth, buttonHeight);
        SetBounds(excluirButton, buttonX, Scale(466, scaleY), buttonWidth, buttonHeight);
        //SetBounds(voltarButton, buttonX, Scale(504, scaleY), buttonWidth, buttonHeight);
    }

    private static void LayoutDetailsQuickButton(RoundedPanel buttonPanel, Label iconLabel, Label textLabel, float contentScale)
    {
        int iconLeft = 8;
        int iconWidth = Math.Max(18, (int)Math.Round(18 * contentScale));
        int textLeft = iconLeft + iconWidth + 4;
        int textWidth = Math.Max(30, buttonPanel.Width - textLeft - 8);

        iconLabel.Bounds = new Rectangle(iconLeft, 0, iconWidth, buttonPanel.Height);
        iconLabel.Font = new Font("Segoe MDL2 Assets", Math.Max(11F, 11F * contentScale));
        iconLabel.TextAlign = ContentAlignment.MiddleCenter;

        textLabel.Bounds = new Rectangle(textLeft, 0, textWidth, buttonPanel.Height);
        textLabel.Font = new Font("Segoe UI", Math.Max(7.5F, 7.5F * contentScale), FontStyle.Bold);
        textLabel.TextAlign = ContentAlignment.MiddleLeft;
    }

    private void InitializeProfilesTableSelection()
    {
        profilesTablePanel.AutoScroll = true;
        _profileSearchRows.Clear();
        _profileRowSelections.Clear();
        _rowSelectionMarkers.Clear();
    }

    private void AttachRowSelectionHandlers(Control control, ProfileRowSelection row)
    {
        control.Click += (_, _) => SetSelectedProfileRow(row.RowPanel);

        foreach (Control child in control.Controls)
        {
            AttachRowSelectionHandlers(child, row);
        }
    }

    private void SetSelectedProfileRow(Panel selectedRowPanel)
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);
        Color normalBackColor = Color.White;

        foreach (ProfileRowSelection row in _profileRowSelections)
        {
            bool isSelected = row.RowPanel == selectedRowPanel;
            row.RowPanel.BackColor = isSelected ? selectedBackColor : normalBackColor;
            if (isSelected)
            {
                ShowMarkerForRow(row.RowPanel);
            }
            else
            {
                HideMarkerForRow(row.RowPanel);
            }
        }

        _idPermissaoAtual = _idPermissaoPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        PreencherCamposPerfilPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
        AtualizarBotoesAcao(ModoAcaoBotoes.EditarExcluir);
    }

    private void PreencherCamposPerfilPorLinha(Panel rowPanel)
    {
        if (!_permissaoPorLinha.TryGetValue(rowPanel, out PermissaoCadastro? permissao))
        {
            return;
        }

        nomePerfilTextBox.Text = permissao.ModuloPermissao;
        descricaoTextBox.Text = permissao.DescricaoPermissao;
        situacaoComboBox.Text = permissao.SituacaoPermissao ? "Ativo" : "Inativo";
        nivelAcessoComboBox.Text = permissao.RotinaPermissao;
        usuariosTextBox.Text = permissao.AcaoPermissao;
        AtualizarTipCadastroPermissao(DateTime.Now);
    }

    private void CriarMarcadorLinha(Panel rowPanel)
    {
        Panel marker = new()
        {
            Name = $"{rowPanel.Name}MarkerPanel",
            BackColor = Color.FromArgb(239, 68, 68),
            Size = new Size(3, rowPanel.Height),
            Visible = false
        };
        rowPanel.Controls.Add(marker);
        _rowSelectionMarkers[rowPanel] = marker;
    }

    private void UpdateMarkerSizes(int markerWidth)
    {
        foreach (ProfileRowSelection row in _profileRowSelections)
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
        if (!_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker))
        {
            return;
        }

        marker.Parent = rowPanel;
        marker.Bounds = new Rectangle(0, 0, marker.Width, rowPanel.Height);
        marker.Visible = true;
        marker.BringToFront();
    }

    private void HideMarkerForRow(Panel rowPanel)
    {
        if (_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker))
        {
            marker.Visible = false;
        }
    }

    private void HideAllMarkers()
    {
        foreach (Panel marker in _rowSelectionMarkers.Values)
        {
            marker.Visible = false;
        }
    }

    private void SyncSummaryFromRow(Panel rowPanel)
    {
        ProfileSearchRow? row = _profileSearchRows.FirstOrDefault(x => x.RowPanel == rowPanel);
        if (row is null)
        {
            ClearSummarySelectionValues();
            return;
        }

        summaryPerfilValueLabel.Text = row.NameLabel.Text;
        summarySituacaoValueLabel.Text = row.StatusLabel.Text;
        AtualizarCorSituacaoResumo(row.StatusLabel.Text);
        summaryUsuariosValueLabel.Text = row.UsersLabel.Text;
    }

    private void ClearSummarySelectionValues()
    {
        summaryPerfilValueLabel.Text = "-";
        summarySituacaoValueLabel.Text = "-";
        AtualizarCorSituacaoResumo(summarySituacaoValueLabel.Text);
        summaryUsuariosValueLabel.Text = "-";
        AtualizarTipCadastroPermissao(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
    }

    private void AtualizarLayoutLinhasDinamicas()
    {
        if (_profileSearchRows.Count == 0)
        {
            return;
        }

        float scaleX = profilesCard.Width / 411f;
        float scaleY = profilesCard.Height / 596f;
        float contentScale = Math.Min(scaleX, scaleY);
        int rowHeight = Math.Max(40, (int)Math.Round(46 * scaleY));
        int baseRowY = Math.Max(0, (int)Math.Round(34 * scaleY));
        int rowLeft = Math.Max(0, (int)Math.Round(3 * scaleX));
        int larguraScroll = SystemInformation.VerticalScrollBarWidth;
        int rowWidth = Math.Max(200, profilesTablePanel.ClientSize.Width - rowLeft - 16 - larguraScroll);
        int statusHeaderWidth = Math.Max(72, (int)Math.Round(78 * scaleX));
        int statusBadgeWidth = Math.Max(56, (int)Math.Round(58 * scaleX));
        int usersX = Math.Max((int)Math.Round(150 * scaleX), rowWidth - (int)Math.Round(215 * scaleX));
        int statusX = Math.Max((int)Math.Round(240 * scaleX), rowWidth - statusHeaderWidth - Math.Max(8, (int)Math.Round(10 * scaleX)));

        profilesHeaderProfileLabel.Left = 16;
        profilesHeaderProfileLabel.Width = Math.Max(120, usersX - 24);
        profilesHeaderProfileLabel.Top = Math.Max(8, (int)Math.Round(8 * scaleY));
        profilesHeaderProfileLabel.Height = Math.Max(22, (int)Math.Round(24 * scaleY));
        profilesHeaderUsersLabel.Left = usersX;
        profilesHeaderUsersLabel.Width = Math.Max(90, statusX - usersX - 8);
        profilesHeaderUsersLabel.Top = profilesHeaderProfileLabel.Top;
        profilesHeaderUsersLabel.Height = profilesHeaderProfileLabel.Height;
        profilesHeaderStatusLabel.Left = statusX;
        profilesHeaderStatusLabel.Width = statusHeaderWidth;
        profilesHeaderStatusLabel.Top = profilesHeaderProfileLabel.Top;
        profilesHeaderStatusLabel.Height = profilesHeaderProfileLabel.Height;

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            ProfileSearchRow row = _profileSearchRows[i];
            row.NameLabel.Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            row.UsersLabel.Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            row.StatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10F), FontStyle.Bold);

            row.RowPanel.Bounds = new Rectangle(rowLeft, baseRowY + (rowHeight * i), rowWidth, rowHeight);
            int contentTop = Math.Max(0, (int)Math.Round(11 * scaleY));
            int contentHeight = Math.Max(16, (int)Math.Round(24 * scaleY));
            row.NameLabel.Bounds = new Rectangle(Math.Max(10, (int)Math.Round(12 * scaleX)), contentTop, Math.Max(90, usersX - Math.Max(16, (int)Math.Round(20 * scaleX))), contentHeight);
            row.UsersLabel.Bounds = new Rectangle(usersX, contentTop, Math.Max(60, statusX - usersX - Math.Max(6, (int)Math.Round(8 * scaleX))), contentHeight);
            Control? statusPanel = row.RowPanel.Controls.OfType<RoundedPanel>().FirstOrDefault();
            if (statusPanel is not null)
            {
                statusPanel.Bounds = new Rectangle(statusX + (statusHeaderWidth - statusBadgeWidth), contentTop, statusBadgeWidth, contentHeight);
            }
        }
    }

    private void AtualizarAreaRolagem(int quantidadeLinhasVisiveis)
    {
        if (_profileSearchRows.Count == 0)
        {
            profilesTablePanel.AutoScrollMinSize = Size.Empty;
            return;
        }

        int rowHeight = _profileSearchRows[0].RowPanel.Height;
        int alturaConteudo = 34 + (rowHeight * Math.Max(quantidadeLinhasVisiveis, 1)) + 8;
        profilesTablePanel.AutoScrollMinSize = new Size(0, alturaConteudo);
    }

    private void AtualizarRodapePermissoes(int? totalVisivel = null)
    {
        int exibindo = totalVisivel ?? _profileSearchRows.Count(x => x.RowPanel.Visible);
        int total = _permissoesCarregadas.Count;
        profilesFooterLabel.Text = $"Exibindo {exibindo} de {total} permissões";
    }

    private void AtualizarTipCadastroPermissao(DateTime? dataCadastro)
    {
        summaryTipTextLabel.Text = dataCadastro.HasValue
            ? $"Data e Hora do Cadastro: {dataCadastro.Value:dd/MM/yyyy HH:mm}"
            : "Data e Hora do Cadastro: -";
    }

    private void AtualizarCorSituacaoResumo(string status)
    {
        if (status.Equals("Ativo", StringComparison.OrdinalIgnoreCase))
        {
            summarySituacaoValueLabel.ForeColor = ResumoSituacaoAtivoTexto;
            return;
        }

        if (status.Equals("Inativo", StringComparison.OrdinalIgnoreCase))
        {
            summarySituacaoValueLabel.ForeColor = ResumoSituacaoInativoTexto;
            return;
        }

        summarySituacaoValueLabel.ForeColor = ResumoSituacaoNeutroTexto;
    }

    private void AtualizarBadgeStatus(Panel rowPanel, string status)
    {
        RoundedPanel? statusPanel = rowPanel.Controls.OfType<RoundedPanel>().FirstOrDefault();
        Label? statusLabel = statusPanel?.Controls.OfType<Label>().FirstOrDefault();
        if (statusPanel is null || statusLabel is null)
        {
            return;
        }

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

    private void AtualizarBotoesAcao(ModoAcaoBotoes modo)
    {
        bool podeCriar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Criar);
        bool podeEditar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Editar);
        bool podeExcluir = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Excluir);

        salvarButton.Visible = modo == ModoAcaoBotoes.SomenteSalvar && podeCriar;
        novoButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeEditar;
        excluirButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeExcluir;
    }

    private void AtualizarWorkspacePermissao()
    {
        if (_atualizandoWorkspace)
        {
            return;
        }

        GarantirGridPermissoes();
        if (_workspacePanel is null || _filtroModuloComboBox is null || _filtroRecursoTextBox is null || _filtroStatusComboBox is null || _modulosListBox is null || _permissoesGrid is null)
        {
            return;
        }

        _atualizandoWorkspace = true;
        try
        {
            IEnumerable<PermissaoCadastro> consulta = _permissoesCarregadas;

            string moduloSelecionado = _filtroModuloComboBox.SelectedItem?.ToString() ?? "Todos";
            if (!string.Equals(moduloSelecionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                consulta = consulta.Where(x => string.Equals(x.ModuloPermissao, moduloSelecionado, StringComparison.OrdinalIgnoreCase));
            }

            string statusSelecionado = _filtroStatusComboBox.SelectedItem?.ToString() ?? "Todos";
            if (!string.Equals(statusSelecionado, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                bool ativo = string.Equals(statusSelecionado, "Ativo", StringComparison.OrdinalIgnoreCase);
                consulta = consulta.Where(x => x.SituacaoPermissao == ativo);
            }

            string recurso = _filtroRecursoTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(recurso))
            {
                consulta = consulta.Where(x =>
                    x.RotinaPermissao.Contains(recurso, StringComparison.OrdinalIgnoreCase)
                    || x.AcaoPermissao.Contains(recurso, StringComparison.OrdinalIgnoreCase)
                    || x.DescricaoPermissao.Contains(recurso, StringComparison.OrdinalIgnoreCase));
            }

            List<PermissaoCadastro> itens = consulta.ToList();

            List<string> modulos = _permissoesCarregadas
                .Select(x => x.ModuloPermissao)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            RecarregarComboModulos(modulos);

            _modulosListBox.Items.Clear();
            foreach (string modulo in modulos)
            {
                _modulosListBox.Items.Add(modulo);
            }

            if (_rodapeListaLabel is not null)
            {
                int totalPermissoes = _permissoesCarregadas.Count;
                _rodapeListaLabel.Text = $"Mostrando {itens.Count} de {totalPermissoes} permissões";
            }

            if (_tituloPermissoesPerfilLabel is not null)
            {
                _tituloPermissoesPerfilLabel.Text = $"Permissões do Perfil: {ObterNomePerfilSelecionado()}";
            }

            _permissoesGrid.Rows.Clear();
            foreach (PermissaoCadastro item in itens)
            {
                // OPCAO A: uma linha = UMA permissao real. O checkbox "Permitido" liga/desliga
                // exatamente esta permissao (item.IdPermissao). Sem matriz de acoes ambigua,
                // entao nao ha risco de marcar "Incluir" e gravar "Consultar".
                bool permitido = _codigosPermissaoPerfilSelecionado.Contains(item.IdPermissao);

                int rowIndex = _permissoesGrid.Rows.Add(
                    permitido,
                    item.ModuloPermissao,
                    item.RotinaPermissao,
                    item.AcaoPermissao,
                    item.DescricaoPermissao);

                _permissoesGrid.Rows[rowIndex].Tag = item.IdPermissao;
            }

            AjustarWorkspaceLayout();
        }
        finally
        {
            _atualizandoWorkspace = false;
        }
    }

    private void RecarregarComboModulos(IReadOnlyList<string> modulos)
    {
        if (_filtroModuloComboBox is null)
        {
            return;
        }

        string selecionado = _filtroModuloComboBox.SelectedItem?.ToString() ?? "Todos";
        _filtroModuloComboBox.Items.Clear();
        _filtroModuloComboBox.Items.Add("Todos");
        foreach (string modulo in modulos)
        {
            _filtroModuloComboBox.Items.Add(modulo);
        }

        int idx = _filtroModuloComboBox.Items.IndexOf(selecionado);
        _filtroModuloComboBox.SelectedIndex = idx >= 0 ? idx : 0;

        if (_filtroStatusComboBox is not null && _filtroStatusComboBox.Items.Count == 1)
        {
            _filtroStatusComboBox.Items.Add("Ativo");
            _filtroStatusComboBox.Items.Add("Inativo");
            _filtroStatusComboBox.SelectedIndex = 0;
        }
    }

    private void LimparFiltrosButton_Click(object? sender, EventArgs e)
    {
        if (_filtroPerfilComboBox is not null) _filtroPerfilComboBox.SelectedIndex = 0;
        if (_filtroModuloComboBox is not null) _filtroModuloComboBox.SelectedIndex = 0;
        if (_filtroStatusComboBox is not null) _filtroStatusComboBox.SelectedIndex = 0;
        if (_filtroRecursoTextBox is not null) _filtroRecursoTextBox.Text = string.Empty;
        AtualizarWorkspacePermissao();
    }

    private async void PesquisarFiltrosButton_Click(object? sender, EventArgs e)
    {
        if (!_integracaoBancoHabilitada)
        {
            AtualizarWorkspacePermissao();
            return;
        }

        await CarregarPermissoesAsync();
    }

    private void RestaurarButton_Click(object? sender, EventArgs e)
    {
        AtualizarWorkspacePermissao();
    }

    private async void SalvarPermissoesButton_Click(object? sender, EventArgs e)
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Permissões", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Regra oficial: alterar a matriz perfil x permissao exige SEGURANCA/PERFIL_ACESSO/GERENCIAR
        // (a acao altera o PERFIL_ACESSO, nao o catalogo de permissoes). Mesma regra do PermissaoServico.
        if (!PodeGerenciarPermissoesDoPerfil())
        {
            MessageBox.Show("Voce nao tem permissao para alterar as permissoes do perfil (SEGURANCA / PERFIL_ACESSO / GERENCIAR).",
                "Permissões", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        long codigoPerfil = ObterCodigoPerfilSelecionado();
        if (codigoPerfil <= 0)
        {
            MessageBox.Show("Selecione um perfil de acesso antes de salvar as permissões.", "Permissões", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _permissoesGrid?.EndEdit();
        IReadOnlyCollection<long> permissoesSelecionadas = ObterPermissoesSelecionadasNaMatriz();
        ResultadoOperacao resultado = await _permissaoController.SincronizarPermissoesDoPerfilAsync(codigoPerfil, permissoesSelecionadas);
        MessageBox.Show(resultado.Mensagem, "Permissões", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarPermissoesDoPerfilSelecionadoAsync();
        }
    }

    private long ObterCodigoPerfilSelecionado()
    {
        return _filtroPerfilComboBox?.SelectedItem is PerfilFiltroItem { CodigoPerfil: > 0 } item
            ? item.CodigoPerfil
            : 0L;
    }

    private string ObterNomePerfilSelecionado()
    {
        return _filtroPerfilComboBox?.SelectedItem is PerfilFiltroItem item
            ? item.NomePerfil
            : "Selecione um perfil";
    }

    /// <summary>
    /// Cria o grid de permissoes (uma linha por permissao, checkbox "Permitido") sob demanda,
    /// hospedando-o no card direito do workspace. Idempotente.
    /// </summary>
    private void GarantirGridPermissoes()
    {
        if (_permissoesGrid is not null) return;
        if (_workspacePanel is null) return;

        TableLayoutPanel? conteudo = _workspacePanel.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        RoundedPanel? direita = conteudo?.Controls.OfType<RoundedPanel>().LastOrDefault();
        if (direita is null) return;

        DataGridView grid = new()
        {
            Name = "_permissoesGrid",
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            EditMode = DataGridViewEditMode.EditOnEnter,
            AutoGenerateColumns = false
        };

        bool podeGerenciar = PodeGerenciarPermissoesDoPerfil();
        DataGridViewCheckBoxColumn colPermitido = new() { Name = "colPermitido", HeaderText = "Permitido", Width = 80, Resizable = DataGridViewTriState.False, ReadOnly = !podeGerenciar };
        DataGridViewTextBoxColumn colModulo = new() { Name = "colModulo", HeaderText = "Módulo", Width = 150, ReadOnly = true };
        DataGridViewTextBoxColumn colRotina = new() { Name = "colRotina", HeaderText = "Rotina", Width = 170, ReadOnly = true };
        DataGridViewTextBoxColumn colAcao = new() { Name = "colAcao", HeaderText = "Ação", Width = 130, ReadOnly = true };
        DataGridViewTextBoxColumn colDescricao = new() { Name = "colDescricao", HeaderText = "Descrição", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true };
        grid.Columns.AddRange(colPermitido, colModulo, colRotina, colAcao, colDescricao);

        // Garante que o clique no checkbox seja commitado imediatamente (antes do Salvar ler o valor).
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };

        _permissoesGrid = grid;
        direita.Controls.Add(grid);
        grid.BringToFront();

        // Esconde o botao de salvar a matriz quando o usuario nao pode gerenciar o perfil.
        salvarPermissoesButton.Visible = podeGerenciar;

        AjustarWorkspaceLayout();
    }

    /// <summary>
    /// Regra oficial para alterar a matriz perfil x permissao: SEGURANCA / PERFIL_ACESSO / GERENCIAR.
    /// A acao altera o PERFIL_ACESSO, por isso usa a mesma permissao exigida pelo PermissaoServico,
    /// e nao SEGURANCA/PERMISSAO (que rege apenas o catalogo de permissoes).
    /// </summary>
    private static bool PodeGerenciarPermissoesDoPerfil()
        => AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Gerenciar);

    private IReadOnlyCollection<long> ObterPermissoesSelecionadasNaMatriz()
    {
        if (_permissoesGrid is null)
        {
            return Array.Empty<long>();
        }

        HashSet<long> codigos = new(_codigosPermissaoPerfilSelecionado);
        foreach (DataGridViewRow row in _permissoesGrid.Rows)
        {
            if (row.IsNewRow || row.Tag is not long codigoPermissao)
            {
                continue;
            }

            if (LinhaPossuiPermissaoMarcada(row))
            {
                codigos.Add(codigoPermissao);
            }
            else
            {
                codigos.Remove(codigoPermissao);
            }
        }

        return codigos;
    }

    private static bool LinhaPossuiPermissaoMarcada(DataGridViewRow row)
        => row.Cells["colPermitido"].Value is bool marcado && marcado;

    private void AjustarWorkspaceLayout()
    {
        if (_workspacePanel is null || _permissoesGrid is null)
        {
            return;
        }

        Control? conteudo = _workspacePanel.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        if (conteudo is null)
        {
            return;
        }

        AjustarLayoutFiltros();

        RoundedPanel? direita = conteudo.Controls.OfType<RoundedPanel>().LastOrDefault();
        if (direita is null)
        {
            return;
        }

        int margemDireita = 14;
        int largura = Math.Max(520, direita.ClientSize.Width - (margemDireita * 2));
        int altura = Math.Max(220, direita.ClientSize.Height - 110);
        _permissoesGrid.Bounds = new Rectangle(14, 38, largura, altura);

        RoundedIconButton? salvar = direita.Controls
            .OfType<RoundedIconButton>()
            .FirstOrDefault(btn => btn.Text.Contains("Salvar", StringComparison.OrdinalIgnoreCase));
        RoundedIconButton? restaurar = direita.Controls
            .OfType<RoundedIconButton>()
            .FirstOrDefault(btn => btn.Text.Contains("Restaurar", StringComparison.OrdinalIgnoreCase));

        if (salvar is not null && restaurar is not null)
        {
            int espacoBotoes = 10;
            int botoesY = _permissoesGrid.Bottom + 10;
            salvar.Location = new Point(14 + largura - salvar.Width, botoesY);
            restaurar.Location = new Point(salvar.Left - espacoBotoes - restaurar.Width, botoesY);
        }
    }

    private void AjustarLayoutFiltros()
    {
        if (_filtrosCardPanel is null
            || _perfilFiltroPanel is null
            || _moduloFiltroPanel is null
            || _recursoFiltroPanel is null
            || _statusFiltroPanel is null
            || _perfilFiltroLabelUi is null
            || _moduloFiltroLabelUi is null
            || _recursoFiltroLabelUi is null
            || _statusFiltroLabelUi is null
            || _limparFiltrosButtonPanel is null
            || _pesquisarFiltrosButtonPanel is null)
        {
            return;
        }

        int left = 16;
        int right = 16;
        int spacing = 28;
        int buttonSpacing = 8;
        int available = _filtrosCardPanel.ClientSize.Width - left - right - (spacing * 3);
        int widthEach = Math.Max(178, available / 4);
        int totalFields = (widthEach * 4) + (spacing * 3);
        int extra = Math.Max(0, _filtrosCardPanel.ClientSize.Width - left - right - totalFields);
        widthEach += extra / 4;

        int x1 = left;
        int x2 = x1 + widthEach + spacing;
        int x3 = x2 + widthEach + spacing;
        int x4 = x3 + widthEach + spacing;
        filtrosIconLabel.Location = new Point(left, 14);
        filtrosTitulo.Location = new Point(left + 20, 10);

        int yLabel = 40;
        int yField = 60;
        int fieldHeight = 30;

        _perfilFiltroLabelUi.Location = new Point(x1, yLabel);
        _moduloFiltroLabelUi.Location = new Point(x2, yLabel);
        _recursoFiltroLabelUi.Location = new Point(x3, yLabel);
        _statusFiltroLabelUi.Location = new Point(x4, yLabel);
        _perfilFiltroLabelUi.Width = widthEach;
        _moduloFiltroLabelUi.Width = widthEach;
        _recursoFiltroLabelUi.Width = widthEach;
        _statusFiltroLabelUi.Width = widthEach;

        _perfilFiltroPanel.Location = new Point(x1, yField);
        _moduloFiltroPanel.Location = new Point(x2, yField);
        _recursoFiltroPanel.Location = new Point(x3, yField);
        _statusFiltroPanel.Location = new Point(x4, yField);

        _perfilFiltroPanel.Size = new Size(widthEach, fieldHeight);
        _moduloFiltroPanel.Size = new Size(widthEach, fieldHeight);
        _recursoFiltroPanel.Size = new Size(widthEach, fieldHeight);
        _statusFiltroPanel.Size = new Size(widthEach, fieldHeight);

        _filtroPerfilComboBox!.Width = Math.Max(120, _perfilFiltroPanel.Width - 16);
        _filtroModuloComboBox!.Width = Math.Max(120, _moduloFiltroPanel.Width - 16);
        _filtroStatusComboBox!.Width = Math.Max(120, _statusFiltroPanel.Width - 16);

        if (_filtroRecursoTextBox is not null && recursoIcone is not null)
        {
            recursoIcone.Location = new Point(_recursoFiltroPanel.Width - recursoIcone.Width - 8, 7);
            _filtroRecursoTextBox.Width = Math.Max(80, recursoIcone.Left - 16);
        }
        _limparFiltrosButtonPanel.Size = new Size(104, 30);
        _pesquisarFiltrosButtonPanel.Size = new Size(120, 30);
        _limparFiltrosButton!.Bounds = new Rectangle(0, 0, _limparFiltrosButtonPanel.Width, _limparFiltrosButtonPanel.Height);
        _pesquisarFiltrosButton!.Bounds = new Rectangle(0, 0, _pesquisarFiltrosButtonPanel.Width, _pesquisarFiltrosButtonPanel.Height);

        int buttonsY = 96;
        _pesquisarFiltrosButtonPanel.Location = new Point(_filtrosCardPanel.ClientSize.Width - right - _pesquisarFiltrosButtonPanel.Width, buttonsY);
        _limparFiltrosButtonPanel.Location = new Point(_pesquisarFiltrosButtonPanel.Left - buttonSpacing - _limparFiltrosButtonPanel.Width, buttonsY);
    }

    private void ModulosListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (_modulosListBox is null || e.Index < 0 || e.Index >= _modulosListBox.Items.Count)
        {
            return;
        }

        e.DrawBackground();
        bool selecionado = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        Color back = selecionado ? Color.FromArgb(254, 242, 242) : Color.White;
        Color titulo = Color.FromArgb(15, 23, 42);
        Color subtitulo = Color.FromArgb(100, 116, 139);
        using SolidBrush backBrush = new(back);
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        string modulo = _modulosListBox.Items[e.Index]?.ToString() ?? string.Empty;
        string subtituloTexto = $"Acesso ao módulo {modulo.ToLowerInvariant()}";

        Rectangle indicador = new(e.Bounds.Left + 2, e.Bounds.Top + 7, 3, e.Bounds.Height - 14);
        if (selecionado)
        {
            using SolidBrush indicadorBrush = new(Color.FromArgb(239, 68, 68));
            e.Graphics.FillRectangle(indicadorBrush, indicador);
        }

        using Font tituloFont = new("Segoe UI", 9F, FontStyle.Bold);
        using Font subFont = new("Segoe UI", 7.75F, FontStyle.Regular);
        using SolidBrush tituloBrush = new(titulo);
        using SolidBrush subBrush = new(subtitulo);
        e.Graphics.DrawString(modulo, tituloFont, tituloBrush, e.Bounds.Left + 10, e.Bounds.Top + 6);
        e.Graphics.DrawString(subtituloTexto, subFont, subBrush, e.Bounds.Left + 10, e.Bounds.Top + 23);
        e.DrawFocusRectangle();
    }


    private void ConfigureHeader()
    {
        menuHeaderLabel.Cursor = Cursors.Hand;
        menuHeaderLabel.Font = new Font("Segoe MDL2 Assets", 15F);
        menuHeaderLabel.ForeColor = Color.White;
        menuHeaderLabel.Location = new Point(18, 8);
        menuHeaderLabel.Size = new Size(36, 36);
        menuHeaderLabel.Text = "\uE700";
        menuHeaderLabel.TextAlign = ContentAlignment.MiddleCenter;
        companyLogoPictureBox.Image = global::FugaPET_Dev.Properties.Resources.fuga_2026_logo;
        companyLogoPictureBox.Location = new Point(60, 5);
        companyLogoPictureBox.Size = new Size(128, 43);
        companyLogoPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerDividerLabel.BackColor = Color.FromArgb(132, 142, 156);
        headerDividerLabel.Location = new Point(208, 12);
        headerDividerLabel.Size = new Size(1, 30);
        headerTitleIconPanel.BorderRadius = 0;
        headerTitleIconPanel.BorderColor = Color.Transparent;
        headerTitleIconPanel.Controls.Add(headerTitleIconPictureBox);
        headerTitleIconPanel.FillColor = Color.Transparent;
        headerTitleIconPanel.Location = new Point(232, 10);
        headerTitleIconPanel.Size = new Size(32, 32);
        headerTitleIconPictureBox.BackColor = Color.Transparent;
        headerTitleIconPictureBox.Image = global::FugaPET_Dev.Properties.Resources.perfil_header_24_white;
        headerTitleIconPictureBox.Location = new Point(4, 4);
        headerTitleIconPictureBox.Size = new Size(24, 24);
        headerTitleIconPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        headerTitleLabel.Font = new Font("Cascadia Code", 12F, FontStyle.Bold);
        headerTitleLabel.ForeColor = Color.White;
        headerTitleLabel.Location = new Point(278, 6);
        headerTitleLabel.Size = new Size(310, 23);
        headerTitleLabel.Text = "Perfis de Acesso";
        headerSubtitleLabel.Font = new Font("Cascadia Code", 7.25F);
        headerSubtitleLabel.ForeColor = Color.FromArgb(211, 218, 228);
        headerSubtitleLabel.Location = new Point(279, 29);
        headerSubtitleLabel.Size = new Size(560, 17);
        headerSubtitleLabel.Text = "Permissões e níveis de acesso / Administração do sistema";
        sapStatusPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        sapStatusPanel.BorderColor = Color.FromArgb(58, 68, 83);
        sapStatusPanel.BorderRadius = 12;
        sapStatusPanel.Controls.Add(sapStatusDotLabel);
        sapStatusPanel.Controls.Add(sapStatusLabel);
        sapStatusPanel.FillColor = Color.FromArgb(24, 31, 43);
        sapStatusPanel.Location = new Point(910, 10);
        sapStatusPanel.Size = new Size(190, 27);
        sapStatusDotLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        sapStatusDotLabel.ForeColor = Color.FromArgb(250, 204, 21);
        sapStatusDotLabel.Location = new Point(11, 4);
        sapStatusDotLabel.Size = new Size(14, 18);
        sapStatusDotLabel.Text = "●";
        sapStatusLabel.Font = new Font("Cascadia Code", 7F, FontStyle.Bold);
        sapStatusLabel.ForeColor = Color.White;
        sapStatusLabel.Location = new Point(27, 5);
        sapStatusLabel.Size = new Size(151, 17);
        sapStatusLabel.Text = "SAP: não configurado";
        ConfigureWindowButton(minimizeWindowLabel, "–", new Point(1218, 0), new Font("Segoe UI", 12F));
        ConfigureWindowButton(maximizeWindowLabel, "\uE922", new Point(1266, 0), new Font("Segoe MDL2 Assets", 9F));
        ConfigureWindowButton(closeWindowLabel, "\uE8BB", new Point(1314, 0), new Font("Segoe MDL2 Assets", 9F));
    }

    private static void ConfigureWindowButton(Label label, string text, Point location, Font font)
    {
        label.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        label.Cursor = Cursors.Hand;
        label.Font = font;
        label.ForeColor = Color.White;
        label.Location = location;
        label.Size = new Size(48, 52);
        label.Text = text;
        label.TextAlign = ContentAlignment.MiddleCenter;
    }

    private void WorkspacePanel_Resize(object? sender, EventArgs e)
    {
        AjustarWorkspaceLayout();
    }

    private void FiltrosCardPanel_Resize(object? sender, EventArgs e)
    {
        AjustarLayoutFiltros();
    }

    private void FiltroCampo_Changed(object? sender, EventArgs e)
    {
        AtualizarWorkspacePermissao();
    }

    private void ModulosListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_modulosListBox?.SelectedItem is string modulo && _filtroModuloComboBox is not null)
        {
            int idx = _filtroModuloComboBox.Items.IndexOf(modulo);
            if (idx >= 0)
            {
                _filtroModuloComboBox.SelectedIndex = idx;
            }
        }
    }


    private enum ModoAcaoBotoes
    {
        Nenhum = 0,
        SomenteSalvar = 1,
        EditarExcluir = 2
    }

    private static void ApplyScaledFont(Control control, float baseSize, float scale, float minSize, float maxSize)
    {
        float targetSize = Math.Clamp(baseSize * scale, minSize, maxSize);
        control.Font = new Font(control.Font.FontFamily, targetSize, control.Font.Style);
    }

    private sealed class ProfileRowSelection
    {
        public ProfileRowSelection(Panel rowPanel, Panel? _)
        {
            RowPanel = rowPanel;
        }

        public Panel RowPanel { get; }
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

    private sealed class PerfilFiltroItem
    {
        public static readonly PerfilFiltroItem Todos = new(0, "Todos");

        public PerfilFiltroItem(long codigoPerfil, string nomePerfil)
        {
            CodigoPerfil = codigoPerfil;
            NomePerfil = nomePerfil;
        }

        public long CodigoPerfil { get; }
        public string NomePerfil { get; }

        public override string ToString()
            => NomePerfil;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _footerClockTimer?.Stop();
        _footerClockTimer?.Dispose();
        base.OnFormClosed(e);
    }
}











