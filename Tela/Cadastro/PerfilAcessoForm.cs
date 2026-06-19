using FugaPET_Dev.Tela.Controls;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class PerfilAcessoForm : Form
{
    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
    private System.Windows.Forms.Timer? _footerClockTimer;
    private readonly List<ProfileRowSelection> _profileRowSelections = new();
    private readonly List<ProfileSearchRow> _profileSearchRows = new();
    private readonly Dictionary<Panel, Panel> _rowSelectionMarkers = new();
    private readonly Dictionary<Panel, long> _idPerfilPorLinha = new();
    private readonly Dictionary<Panel, PerfilAcessoCadastro> _perfilPorLinha = new();
    private readonly List<PerfilAcessoCadastro> _perfisCarregados = new();
    private readonly ToolTip _toolTipPerfil = new();
    private readonly PerfilAcessoController _perfilAcessoController;
    private long _idPerfilAtual;
    private static readonly Color StatusAtivoFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color StatusAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusInativoFundo = Color.FromArgb(255, 237, 213);
    private static readonly Color StatusInativoTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color StatusNeutroFundo = Color.FromArgb(243, 244, 246);
    private static readonly Color StatusNeutroTexto = Color.FromArgb(100, 116, 139);
    private static readonly Color ResumoSituacaoAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color ResumoSituacaoInativoTexto = Color.FromArgb(220, 38, 38);
    private static readonly Color ResumoSituacaoNeutroTexto = Color.FromArgb(100, 116, 139);

    public PerfilAcessoForm(PerfilAcessoController? perfilAcessoController = null)
    {
        _perfilAcessoController = perfilAcessoController ?? FabricaControladoresCadastro.CriarPerfilAcessoController();
        InitializeComponent();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        ConfigureFooterDate();
        OcultarBlocoPermissoesNoDetailsCard();
        ConfigureNovoPerfilAction();
        ConfigureProfilesSearchFilter();
        InitializeProfilesTableSelection();
        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        detailsCard.Resize += (_, _) => LayoutDetailsCard();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();
        LayoutProfilesCard();
        LayoutDetailsCard();
        LayoutSummaryCard();
        summaryTipTextLabel.AutoEllipsis = true;
        summaryTipTextLabel.AutoSize = false;
        summaryTipTextLabel.TextAlign = ContentAlignment.MiddleLeft;
        summaryPermissionsValueLabel.Text = "Controlado na tela Permissao";
        ApplyProfilesFilter();
        AtualizarRodapePerfis();
        AtualizarTipCadastroPerfil(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        ConectarAcoesCadastro();
        if (_integracaoBancoHabilitada)
        {
            Shown += async (_, _) => await CarregarPerfisAsync();
        }
    }

    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) => await SalvarPerfilAsync();
        novoButton.Click += async (_, _) => await EditarPerfilAsync();
        excluirButton.Click += async (_, _) => await ExcluirPerfilAsync();
    }

    private async Task SalvarPerfilAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        PerfilAcessoCadastro perfil = new()
        {
            NomePerfilAcesso = nomePerfilTextBox.Text.Trim(),
            DescricaoPerfilAcesso = descricaoTextBox.Text.Trim(),
            PerfilSistema = string.Equals(nivelAcessoComboBox.Text, "Administrativo", StringComparison.OrdinalIgnoreCase),
            SituacaoPerfilAcesso = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase),
            PerfilAcessoCriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario
        };

        var resultado = await _perfilAcessoController.InserirAsync(perfil);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Perfil", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idPerfilAtual = resultado.IdGerado ?? 0;
            PrepareNewProfile();
            await CarregarPerfisAsync();
        }
    }

    private async Task ExcluirPerfilAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idPerfilAtual <= 0)
        {
            MessageBox.Show("Salve ou selecione um perfil para inativar.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            "Confirma a inativacao do perfil atual?\n\nO perfil ficara inativo, mas pode ser reativado depois alterando a situacao na edicao.\nPerfis de sistema nao podem ser inativados.",
            "Cadastro de Perfil",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes)
        {
            return;
        }

        var resultado = await _perfilAcessoController.ExcluirAsync(_idPerfilAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Perfil", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idPerfilAtual = 0;
            PrepareNewProfile();
            await CarregarPerfisAsync();
        }
    }

    private async Task CarregarPerfisAsync()
    {
        IReadOnlyList<PerfilAcessoCadastro> perfis = await _perfilAcessoController.ListarAsync();
        _perfisCarregados.Clear();
        _perfisCarregados.AddRange(perfis);
        RecriarLinhasPerfis();
        PopularLinhasComPerfis(_perfisCarregados);
        AtualizarRodapePerfis();
        ClearSummarySelectionValues();
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        AtualizarTipCadastroPerfil(null);
        ApplyProfilesFilter();
    }

    private async Task EditarPerfilAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idPerfilAtual <= 0)
        {
            MessageBox.Show("Selecione um perfil para editar.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        PerfilAcessoCadastro? perfilSelecionado = _perfilPorLinha.Values.FirstOrDefault(x => x.IdPerfilAcesso == _idPerfilAtual);
        if (perfilSelecionado is null)
        {
            MessageBox.Show("Selecione um perfil valido para editar.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string descricaoAtual = descricaoTextBox.Text.Trim();
        bool situacaoAtual = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase);
        bool nivelSistemaAtual = string.Equals(nivelAcessoComboBox.Text, "Administrativo", StringComparison.OrdinalIgnoreCase);

        bool descricaoAlterada = !string.Equals(descricaoAtual, perfilSelecionado.DescricaoPerfilAcesso?.Trim() ?? string.Empty, StringComparison.Ordinal);
        bool situacaoAlterada = situacaoAtual != perfilSelecionado.SituacaoPerfilAcesso;
        bool nivelAlterado = nivelSistemaAtual != perfilSelecionado.PerfilSistema;

        if (!descricaoAlterada && !situacaoAlterada && !nivelAlterado)
        {
            MessageBox.Show("Nenhuma alteracao foi realizada para salvar.", "Cadastro de Perfil", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        PerfilAcessoCadastro perfilAtualizado = new()
        {
            IdPerfilAcesso = _idPerfilAtual,
            NomePerfilAcesso = nomePerfilTextBox.Text.Trim(),
            DescricaoPerfilAcesso = descricaoAtual,
            PerfilSistema = nivelSistemaAtual,
            SituacaoPerfilAcesso = situacaoAtual,
            PerfilAcessoAtualizadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario
        };

        var resultado = await _perfilAcessoController.AtualizarAsync(perfilAtualizado);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Perfil", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarPerfisAsync();
        }
    }

    private void RecriarLinhasPerfis()
    {
        RemoverLinhasPerfisExistentes();

        for (int i = 0; i < _perfisCarregados.Count; i++)
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

    private void RemoverLinhasPerfisExistentes()
    {
        foreach (ProfileSearchRow row in _profileSearchRows)
        {
            profilesTablePanel.Controls.Remove(row.RowPanel);
            row.RowPanel.Dispose();
        }

        _profileSearchRows.Clear();
        _idPerfilPorLinha.Clear();
        _perfilPorLinha.Clear();
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

    private void PopularLinhasComPerfis(IReadOnlyList<PerfilAcessoCadastro> perfis)
    {
        _idPerfilPorLinha.Clear();
        _perfilPorLinha.Clear();

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            PerfilAcessoCadastro perfil = perfis[i];
            ProfileSearchRow linha = _profileSearchRows[i];
            linha.NameLabel.Text = perfil.NomePerfilAcesso;
            _toolTipPerfil.SetToolTip(linha.NameLabel, perfil.NomePerfilAcesso);
            linha.UsersLabel.Text = "-";
            linha.StatusLabel.Text = perfil.SituacaoPerfilAcesso ? "Ativo" : "Inativo";
            AtualizarBadgeStatus(linha.RowPanel, linha.StatusLabel.Text);
            _idPerfilPorLinha[linha.RowPanel] = perfil.IdPerfilAcesso;
            _perfilPorLinha[linha.RowPanel] = perfil;
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
        control.Click += (_, _) => PrepareNewProfile();

        foreach (Control child in control.Controls)
        {
            AttachNovoPerfilClick(child);
        }
    }

    private void PrepareNewProfile()
    {
        _idPerfilAtual = 0;
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        situacaoComboBox.SelectedIndex = -1;
        situacaoComboBox.Text = string.Empty;
        nivelAcessoComboBox.SelectedIndex = -1;
        nivelAcessoComboBox.Text = string.Empty;

        nomePerfilTextBox.Focus();
        nomePerfilTextBox.SelectionStart = 0;
        nomePerfilTextBox.SelectionLength = 0;
        AtualizarTipCadastroPerfil(null);
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
        AtualizarRodapePerfis(visibleIndex);

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

    private void OcultarBlocoPermissoesNoDetailsCard()
    {
        foreach (Control control in detailsCard.Controls)
        {
            string nomeControle = control.Name ?? string.Empty;
            if (nomeControle.StartsWith("permissao", StringComparison.OrdinalIgnoreCase)
                || nomeControle.StartsWith("permissions", StringComparison.OrdinalIgnoreCase))
            {
                control.Visible = false;
            }
        }
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

        SetBounds(detailsTopDividerLabel, Scale(24, scaleX), Scale(242, scaleY), Math.Max(1, Scale(506, scaleX)), Scale(1, scaleY));
        detailsTopDividerLabel.SendToBack();
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

        _idPerfilAtual = _idPerfilPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        PreencherCamposPerfilPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
        AtualizarBotoesAcao(ModoAcaoBotoes.EditarExcluir);
    }

    private void PreencherCamposPerfilPorLinha(Panel rowPanel)
    {
        if (!_perfilPorLinha.TryGetValue(rowPanel, out PerfilAcessoCadastro? perfil))
        {
            return;
        }

        nomePerfilTextBox.Text = perfil.NomePerfilAcesso;
        descricaoTextBox.Text = perfil.DescricaoPerfilAcesso;
        situacaoComboBox.Text = perfil.SituacaoPerfilAcesso ? "Ativo" : "Inativo";
        nivelAcessoComboBox.Text = perfil.PerfilSistema ? "Administrativo" : "Operacional";
        usuariosTextBox.Text = "0";
        AtualizarTipCadastroPerfil(perfil.PerfilAcessoCriadoEm);
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
        AtualizarTipCadastroPerfil(null);
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

    private void AtualizarRodapePerfis(int? totalVisivel = null)
    {
        int exibindo = totalVisivel ?? _profileSearchRows.Count(x => x.RowPanel.Visible);
        int total = _perfisCarregados.Count;
        profilesFooterLabel.Text = $"Exibindo {exibindo} de {total} perfis";
    }

    private void AtualizarTipCadastroPerfil(DateTime? dataCadastro)
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
        bool podeCriar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Criar);
        bool podeEditar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Editar);
        bool podeExcluir = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Excluir);

        salvarButton.Visible = modo == ModoAcaoBotoes.SomenteSalvar && podeCriar;
        novoButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeEditar;
        excluirButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeExcluir;
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _footerClockTimer?.Stop();
        _footerClockTimer?.Dispose();
        base.OnFormClosed(e);
    }
}







