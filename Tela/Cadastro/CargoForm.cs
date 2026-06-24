using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Tela.Controls;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class CargoForm : Form
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
    private readonly Dictionary<Panel, long> _idCargoPorLinha = new();
    private readonly Dictionary<Panel, CargoCadastro> _cargoPorLinha = new();
    private readonly List<CargoCadastro> _cargosCarregados = new();
    private readonly ToolTip _toolTipCargo = new();
    private readonly CargoController _cargoController;
    private readonly AuditoriaServico _auditoriaServico;
    private long _idCargoAtual;
    private bool _edicaoCargoExistente;
    private bool _operacaoEmAndamento;
    private ModoAcaoBotoes _modoAcaoBotoesAtual = ModoAcaoBotoes.Nenhum;
    private ModoCard _modoCard = ModoCard.Novo;
    private static readonly Color StatusAtivoFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color StatusAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusInativoFundo = Color.FromArgb(255, 237, 213);
    private static readonly Color StatusInativoTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color StatusNeutroFundo = Color.FromArgb(243, 244, 246);
    private static readonly Color StatusNeutroTexto = Color.FromArgb(100, 116, 139);
    private static readonly Color ResumoSituacaoAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color ResumoSituacaoInativoTexto = Color.FromArgb(220, 38, 38);
    private static readonly Color ResumoSituacaoNeutroTexto = Color.FromArgb(100, 116, 139);

    public CargoForm(
        CargoController? cargoController = null,
        AuditoriaServico? auditoriaServico = null)
    {
        _cargoController = cargoController ?? FabricaControladoresCadastro.CriarCargoController();
        _auditoriaServico = auditoriaServico ?? FabricaControladoresCadastro.CriarAuditoriaServico();
        InitializeComponent();
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        ConfigureWindowButtons();
        ConfigureDragOnTitleBar();
        ConfigureNovoCargoAction();
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
        AtualizarRodapePerfis();
        AtualizarTipCadastroCargo(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        ConfigurarCard(ModoCard.Bloqueado);
        nomePerfilTextBox.MaxLength = CargoCadastro.TamanhoMaximoNome;
        descricaoTextBox.MaxLength = CargoCadastro.TamanhoMaximoDescricao;
        searchTextBox.MaxLength = 120;
        ConectarAcoesCadastro();
        Shown += async (_, _) => await InicializarTelaAsync();
    }

    private async Task InicializarTelaAsync()
    {
        if (!AutorizacaoServico.PodeVisualizarRotina(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Cargo))
        {
            long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
            if (codigoUsuario.HasValue)
            {
                await RegistrarAcessoDiretoNegadoSeguroAsync(codigoUsuario.Value);
            }

            MessageBox.Show(
                "Você não possui permissão para acessar esta rotina.",
                "Acesso negado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Close();
            return;
        }

        if (_integracaoBancoHabilitada)
        {
            await CarregarCargosAsync();
        }
    }

    private async Task RegistrarAcessoDiretoNegadoSeguroAsync(long codigoUsuario)
    {
        try
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(
                codigoUsuario,
                $"Acesso direto negado a Cadastro de Cargo ({PermissoesSistema.Modulos.Cadastro}/{PermissoesSistema.Rotinas.Cargo}/CONSULTAR ou VISUALIZAR).",
                nameof(CargoForm));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"Falha ao registrar acesso direto negado ao CargoForm: {ex}");
        }
    }

    private void ConectarAcoesCadastro()
    {
        salvarButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(SalvarCargoAsync, "CARGO_SALVAR_ERRO");
        novoButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(EditarCargoAsync, "CARGO_ATUALIZAR_ERRO");
        excluirButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(AlternarSituacaoCargoAsync, "CARGO_ALTERAR_SITUACAO_ERRO");
    }

    private async Task ExecutarOperacaoProtegidaAsync(Func<Task> operacao, string acaoErro)
    {
        if (_operacaoEmAndamento)
        {
            return;
        }

        _operacaoEmAndamento = true;
        AtualizarBotoesAcao(_modoAcaoBotoesAtual);

        try
        {
            await operacao();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await global::FugaPET_Dev.Tela.Comum.ErroUsuarioHelper.TratarAsync(
                    acaoErro,
                    ex,
                    nameof(CargoForm),
                    "Não foi possível concluir a operação de cargo. Acione o suporte."),
                "Cadastro de Cargo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoesAcao(_modoAcaoBotoesAtual);
        }
    }

    // Atalhos de teclado: F5 Salvar, F6 Editar, F8 Inativar/Reativar.
    // Aciona o botao correspondente apenas quando ele esta visivel (modo/permissao validos).
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F5 when salvarButton.Visible && salvarButton.Enabled:
                salvarButton.PerformClick();
                return true;
            case Keys.F6 when novoButton.Visible && novoButton.Enabled:
                novoButton.PerformClick();
                return true;
            case Keys.F8 when excluirButton.Visible && excluirButton.Enabled:
                excluirButton.PerformClick();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
    private async Task SalvarCargoAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CargoCadastro cargo = new()
        {
            NomeCargo = nomePerfilTextBox.Text.Trim(),
            DescricaoCargo = descricaoTextBox.Text.Trim(),
            SituacaoCargo = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase),
            CargoCriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario
        };

        var resultado = await _cargoController.InserirAsync(cargo);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Cargo", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idCargoAtual = resultado.IdGerado ?? 0;
            PrepareNewCargo();
            await CarregarCargosAsync();
        }
    }

    private async Task AlternarSituacaoCargoAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idCargoAtual <= 0)
        {
            MessageBox.Show("Selecione um cargo para alterar a situacao.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CargoCadastro? cargo = _cargoPorLinha.Values.FirstOrDefault(x => x.IdCargo == _idCargoAtual);
        if (cargo is null)
        {
            MessageBox.Show("Selecione um cargo valido.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool ativo = cargo.SituacaoCargo;
        string pergunta = ativo
            ? "Confirma a inativacao do cargo atual?\n\nO cargo ficara inativo, mas pode ser reativado depois."
            : "Confirma a reativacao do cargo atual?";

        DialogResult confirmacao = MessageBox.Show(
            pergunta,
            "Cadastro de Cargo",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes)
        {
            return;
        }

        var resultado = ativo
            ? await _cargoController.ExcluirAsync(_idCargoAtual)
            : await _cargoController.ReativarAsync(_idCargoAtual);

        MessageBox.Show(resultado.Mensagem, "Cadastro de Cargo", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idCargoAtual = 0;
            PrepareNewCargo();
            await CarregarCargosAsync();
        }
    }
    private async Task EditarCargoAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idCargoAtual <= 0)
        {
            MessageBox.Show("Selecione um cargo para editar.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CargoCadastro? cargoSelecionado = _cargoPorLinha.Values.FirstOrDefault(x => x.IdCargo == _idCargoAtual);
        if (cargoSelecionado is null)
        {
            MessageBox.Show("Selecione um cargo valido para editar.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string descricaoAtual = descricaoTextBox.Text.Trim();
        bool situacaoAtual = string.Equals(situacaoComboBox.Text, "Ativo", StringComparison.OrdinalIgnoreCase);
        bool descricaoAlterada = !string.Equals(descricaoAtual, cargoSelecionado.DescricaoCargo?.Trim() ?? string.Empty, StringComparison.Ordinal);
        bool situacaoAlterada = situacaoAtual != cargoSelecionado.SituacaoCargo;

        if (!descricaoAlterada && !situacaoAlterada)
        {
            MessageBox.Show("Nenhuma alteracao foi realizada para salvar.", "Cadastro de Cargo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CargoCadastro cargoAtualizado = new()
        {
            IdCargo = _idCargoAtual,
            NomeCargo = nomePerfilTextBox.Text.Trim(),
            DescricaoCargo = descricaoAtual,
            SituacaoCargo = situacaoAtual,
            CargoAtualizadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario
        };

        var resultado = await _cargoController.AtualizarAsync(cargoAtualizado);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Cargo", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarCargosAsync();
        }
    }

    private async Task CarregarCargosAsync()
    {
        try
        {
            IReadOnlyList<CargoCadastro> cargos = await _cargoController.ListarAsync();
            _cargosCarregados.Clear();
            _cargosCarregados.AddRange(cargos);
            RecriarLinhasPerfis();
            PopularLinhasComCargos(_cargosCarregados);
            ClearRowSelection();
            ApplyProfilesFilter();
        }
        catch (Exception ex)
        {
            _cargosCarregados.Clear();
            RemoverLinhasPerfisExistentes();
            ClearRowSelection();
            AtualizarRodapePerfis(0);

            MessageBox.Show(
                await global::FugaPET_Dev.Tela.Comum.ErroUsuarioHelper.TratarAsync(
                    "CARGO_CARREGAR_ERRO",
                    ex,
                    nameof(CargoForm),
                    "Não foi possível carregar os cargos. Acione o suporte."),
                "Cadastro de Cargo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void ConfigureProfilesSearchFilter()
    {
        searchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void RecriarLinhasPerfis()
    {
        RemoverLinhasPerfisExistentes();

        for (int i = 0; i < _cargosCarregados.Count; i++)
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
        _idCargoPorLinha.Clear();
        _cargoPorLinha.Clear();
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
            //ForeColor = profileRow1NameLabel.ForeColor,
            //Font = profileRow1NameLabel.Font
        };

        Label usersLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}UsersLabel",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            //ForeColor = profileRow1UsersLabel.ForeColor,
            //Font = profileRow1UsersLabel.Font,
            Text = "-"
        };

        RoundedPanel statusPanel = new()
        {
            Name = $"profileDynamicRow{indice + 1}StatusPanel",
            //BorderRadius = profileRow1StatusPanel.BorderRadius,
            //FillColor = profileRow1StatusPanel.FillColor,
            //BorderColor = profileRow1StatusPanel.BorderColor,
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };

        Label statusLabel = new()
        {
            Name = $"profileDynamicRow{indice + 1}StatusLabel",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            //ForeColor = profileRow1StatusLabel.ForeColor,
            //Font = profileRow1StatusLabel.Font
        };

        statusPanel.Controls.Add(statusLabel);
        rowPanel.Controls.Add(nameLabel);
        rowPanel.Controls.Add(usersLabel);
        rowPanel.Controls.Add(statusPanel);

        _statusPanelPorLinha[rowPanel] = statusPanel;
        _statusLabelPorLinha[rowPanel] = statusLabel;

        return new ProfileSearchRow(rowPanel, nameLabel, usersLabel, statusLabel);
    }

    private void PopularLinhasComCargos(IReadOnlyList<CargoCadastro> cargos)
    {
        _idCargoPorLinha.Clear();
        _cargoPorLinha.Clear();

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            CargoCadastro cargo = cargos[i];
            ProfileSearchRow linha = _profileSearchRows[i];

            linha.NameLabel.Text = cargo.NomeCargo;
            _toolTipCargo.SetToolTip(linha.NameLabel, cargo.NomeCargo);
            linha.UsersLabel.Text = "-";
            linha.StatusLabel.Text = cargo.SituacaoCargo ? "Ativo" : "Inativo";
            AtualizarBadgeStatus(linha.RowPanel, linha.StatusLabel.Text);
            _idCargoPorLinha[linha.RowPanel] = cargo.IdCargo;
            _cargoPorLinha[linha.RowPanel] = cargo;
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

            if (!match)
            {
                continue;
            }

            int top = baseTop + (rowHeight * visibleIndex);
            row.RowPanel.Location = new Point(baseLeft, top);
            visibleIndex++;
        }

        AtualizarAreaRolagem(visibleIndex);
        AtualizarRodapePerfis(visibleIndex);

        // A busca apenas filtra. A selecao (destaque + marcador + resumo) ocorre somente quando
        // o usuario clica numa linha. Se a linha selecionada saiu do filtro, limpamos a selecao.
        Panel? linhaSelecionada = _idCargoPorLinha
            .FirstOrDefault(item => item.Value == _idCargoAtual)
            .Key;

        if (linhaSelecionada is not null && !linhaSelecionada.Visible)
        {
            ClearRowSelection();
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
        //int baseRowY = profileRow1Panel.Top;
        int linhas = Math.Max(quantidadeLinhasVisiveis, 1);
        //int alturaConteudo = baseRowY + (rowHeight * linhas) + 8;
        //profilesTablePanel.AutoScrollMinSize = new Size(0, alturaConteudo);
    }

    private void AtualizarRodapePerfis(int? totalVisivel = null)
    {
        int exibindo = totalVisivel ?? _profileSearchRows.Count(x => x.RowPanel.Visible);
        int total = _cargosCarregados.Count;
        profilesFooterLabel.Text = $"Exibindo {exibindo} de {total} cargos";
    }

    private void ClearRowSelection()
    {
        _idCargoAtual = 0;

        foreach (RowSelection row in _rowSelections)
        {
            row.RowPanel.BackColor = row.NormalBackColor;
        }

        HideAllMarkers();
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

    private void ConfigureNovoCargoAction()
    {
        SetHandCursor(novoPerfilButtonPanel);
        AttachNovoCargoClick(novoPerfilButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;

        foreach (Control child in control.Controls)
        {
            SetHandCursor(child);
        }
    }

    private void AttachNovoCargoClick(Control control)
    {
        control.Click += (_, _) => PrepareNewCargo();

        foreach (Control child in control.Controls)
        {
            AttachNovoCargoClick(child);
        }
    }

    private void PrepareNewCargo()
    {
        ClearRowSelection();
        ConfigurarCard(ModoCard.Novo);
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        situacaoComboBox.SelectedItem = "Ativo";
        nomePerfilTextBox.ReadOnly = false;
        nomePerfilTextBox.BackColor = Color.White;

        nomePerfilTextBox.Focus();
        nomePerfilTextBox.SelectionStart = 0;
        nomePerfilTextBox.SelectionLength = 0;
        AtualizarTipCadastroCargo(null);
        AtualizarBotoesAcao(ModoAcaoBotoes.SomenteSalvar);
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
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
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
        int divisorSuperiorDataY = Scale(232, scaleY);
        int dataLabelY = Scale(252, scaleY);
        int divisorInferiorDataY = Scale(286, scaleY);
        SetBounds(summaryDividerLabel4, Scale(26, scaleX), divisorSuperiorDataY, dividerWidth, 1);
        SetBounds(summaryDividerLabel5, Scale(26, scaleX), divisorInferiorDataY, dividerWidth, 1);

        SetBounds(summaryPerfilIconLabel, innerLeft, Scale(78, scaleY), iconSize, iconSize);
        SetBounds(summaryPerfilCaptionLabel, textLeft, Scale(78, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryPerfilValueLabel, textLeft, Scale(98, scaleY), labelWidth, titleHeight);

        SetBounds(summarySituacaoIconLabel, innerLeft, Scale(132, scaleY), iconSize, iconSize);
        SetBounds(summarySituacaoCaptionLabel, textLeft, Scale(132, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summarySituacaoValueLabel, textLeft, Scale(152, scaleY), labelWidth, titleHeight);

        SetBounds(summaryUsuariosIconLabel, innerLeft, Scale(186, scaleY), iconSize, iconSize);
        SetBounds(summaryUsuariosCaptionLabel, textLeft, Scale(186, scaleY), labelWidth, Scale(18, scaleY));
        SetBounds(summaryUsuariosValueLabel, textLeft, Scale(206, scaleY), labelWidth, titleHeight);

        SetBounds(tipTextLabel, textLeft, dataLabelY, Math.Max(180, summaryCard.Width - Scale(98, scaleX)), Math.Max(22, Scale(24, scaleY)));

        SetBounds(salvarButton, buttonX, Scale(402, scaleY), buttonWidth, buttonHeight);
        SetBounds(novoButton, buttonX, Scale(434, scaleY), buttonWidth, buttonHeight);
        SetBounds(excluirButton, buttonX, Scale(466, scaleY), buttonWidth, buttonHeight);
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
        static void SetBounds(Control control, int x, int y, int width, int height) =>
            control.Bounds = new Rectangle(x, y, width, height);

        int left = Scale(24, scaleX);
        int right = Scale(24, scaleX);
        int gap = Scale(30, scaleX);
        int cardInnerWidth = Math.Max(260, detailsCard.Width - left - right);
        int fieldWidth = Math.Max(180, (cardInnerWidth - gap) / 2);
        int col1 = left;
        int col2 = left + fieldWidth + gap;

        ApplyScaledFont(detailsTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(detailsTitleIconLabel, 14F, contentScale, 14F, 18F);
        ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(nomePerfilTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(descricaoTextBox, 9F, contentScale, 9F, 12F);
        situacaoComboBox.IntegralHeight = false;
        situacaoComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));

        SetBounds(detailsTitleIconLabel, Scale(20, scaleX), Scale(18, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        SetBounds(detailsTitleLabel, Scale(52, scaleX), Scale(22, scaleY), Math.Max(190, Scale(220, scaleX)), Scale(24, scaleY));

        SetBounds(nomePerfilLabel, col1, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(nomePerfilInputPanel, col1, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(nomePerfilTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        SetBounds(situacaoLabel, col2, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        SetBounds(situacaoInputPanel, col2, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        int situacaoComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int situacaoComboHeight = Math.Max(22, situacaoComboBox.PreferredHeight);
        int situacaoComboY = Math.Max(2, (situacaoInputPanel.Height - situacaoComboHeight) / 2);
        SetBounds(situacaoComboBox, Scale(12, scaleX), situacaoComboY, situacaoComboWidth, situacaoComboHeight);

        // Com Nome/Situacao ocultos (cargo selecionado), a Descricao sobe e ocupa o espaco.
        int descricaoLabelY = _edicaoCargoExistente ? Scale(56, scaleY) : Scale(117, scaleY);
        // Mantem a mesma folga label->campo do layout original (23px) para nao cobrir a borda superior.
        int descricaoInputY = _edicaoCargoExistente ? Scale(79, scaleY) : Scale(140, scaleY);
        int descricaoInputH = _edicaoCargoExistente ? Math.Max(150, Scale(191, scaleY)) : Math.Max(95, Scale(130, scaleY));
        SetBounds(descricaoLabel, col1, descricaoLabelY, cardInnerWidth, Scale(19, scaleY));
        SetBounds(descricaoInputPanel, col1, descricaoInputY, cardInnerWidth, descricaoInputH);
        SetBounds(descricaoTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(120, cardInnerWidth - Scale(24, scaleX)), Math.Max(60, descricaoInputH - Scale(30, scaleY)));

        SetBounds(detailsTopDividerLabel, col1, descricaoInputPanel.Bottom + Scale(12, scaleY), cardInnerWidth, 1);
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

        int quickW = Math.Max(95, Scale(99, scaleX));
        int quickH = Math.Max(27, Scale(27, scaleY));
        SetBounds(novoPerfilButtonPanel, Math.Max(side, profilesCard.Width - side - quickW), Scale(18, scaleY), quickW, quickH);
        SetBounds(novoPerfilIconLabel, Scale(8, scaleX), Scale(3, scaleY), Math.Max(16, Scale(18, scaleX)), Math.Max(18, Scale(21, scaleY)));
        SetBounds(novoPerfilTextLabel, Scale(29, scaleX), Scale(3, scaleY), Math.Max(48, quickW - Scale(32, scaleX)), Math.Max(18, Scale(21, scaleY)));

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
        if (_profileSearchRows.Count == 0)
        {
            return;
        }

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
        int contentHeightEstimado = baseRowY + (rowHeight * _profileSearchRows.Count) + 8;
        bool precisaScrollVertical = contentHeightEstimado > profilesTablePanel.ClientSize.Height;
        int larguraScroll = precisaScrollVertical ? SystemInformation.VerticalScrollBarWidth : 0;
        int larguraUtil = Math.Max(200, profilesTablePanel.ClientSize.Width - rowLeft - rowRightPadding - larguraScroll);
        int rowWidth = larguraUtil;
        int usersX = Math.Max((int)Math.Round(150 * scaleX), rowWidth - (int)Math.Round(185 * scaleX));
        int statusPanelXMin = Math.Max((int)Math.Round(250 * scaleX), nameX + 120);
        int statusPanelXHeader = Math.Max(statusPanelXMin, rowWidth - statusPanelW - statusInnerRightPadding);

        profilesHeaderProfileLabel.Left = rowLeft + Math.Max(0, (int)Math.Round(13 * scaleX));
        profilesHeaderProfileLabel.Width = Math.Max(120, usersX - (profilesHeaderProfileLabel.Left) - Math.Max(0, (int)Math.Round(8 * scaleX)));
        profilesHeaderUsersLabel.Left = rowLeft + usersX;
        profilesHeaderUsersLabel.Width = Math.Max(90, statusPanelXHeader - usersX - Math.Max(0, (int)Math.Round(8 * scaleX)));
        profilesHeaderStatusLabel.Left = rowLeft + statusPanelXHeader;
        profilesHeaderStatusLabel.Width = statusPanelW;

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
        AtualizarAreaRolagem(_profileSearchRows.Count(x => x.RowPanel.Visible));
    }

    private static void ApplyScaledFont(Control control, float baseSize, float scale, float minSize, float maxSize)
    {
        float size = Math.Clamp(baseSize * scale, minSize, maxSize);
        if (Math.Abs(control.Font.Size - size) < 0.01f)
        {
            return;
        }

        control.Font = new Font(control.Font.FontFamily, size, control.Font.Style);
    }

    private void InitializeProfilesTableSelection()
    {
        profilesTablePanel.AutoScroll = true;
        //profilesTablePanel.Controls.Remove(profileRow1Panel);
        //profilesTablePanel.Controls.Remove(profileRow2Panel);
        //profilesTablePanel.Controls.Remove(profileRow3Panel);
        //profilesTablePanel.Controls.Remove(profileRow4Panel);
        //profilesTablePanel.Controls.Remove(profileRow5Panel);
        //profileRow1Panel.Visible = false;
        //profileRow2Panel.Visible = false;
        //profileRow3Panel.Visible = false;
        //profileRow4Panel.Visible = false;
        //profileRow5Panel.Visible = false;
        _profileSearchRows.Clear();
        _rowSelections.Clear();
        _rowSelectionMarkers.Clear();
    }

    private void AttachRowSelectionHandlers(Control control, Panel rowPanel)
    {
        control.Click += (_, _) => SetSelectedRow(rowPanel);

        foreach (Control child in control.Controls)
        {
            AttachRowSelectionHandlers(child, rowPanel);
        }
    }

    private void SetSelectedRow(Panel selectedRowPanel)
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);

        foreach (RowSelection row in _rowSelections)
        {
            bool isSelected = row.RowPanel == selectedRowPanel;
            row.RowPanel.BackColor = isSelected ? selectedBackColor : row.NormalBackColor;
            if (isSelected)
            {
                ShowMarkerForRow(row.RowPanel);
            }
            else
            {
                HideMarkerForRow(row.RowPanel);
            }
        }

        _idCargoAtual = _idCargoPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        ConfigurarCard(ModoCard.Edicao);
        PreencherCamposCargoPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
        AtualizarBotoesAcao(ModoAcaoBotoes.EditarExcluir);
    }

    private void PreencherCamposCargoPorLinha(Panel rowPanel)
    {
        if (!_cargoPorLinha.TryGetValue(rowPanel, out CargoCadastro? cargo))
        {
            return;
        }

        nomePerfilTextBox.Text = cargo.NomeCargo;
        descricaoTextBox.Text = cargo.DescricaoCargo;
        situacaoComboBox.Text = cargo.SituacaoCargo ? "Ativo" : "Inativo";
        nomePerfilTextBox.ReadOnly = true;
        nomePerfilTextBox.BackColor = Color.FromArgb(241, 245, 249);
        AtualizarTipCadastroCargo(cargo.CargoCriadoEm);
    }

    private void AtualizarTipCadastroCargo(DateTime? dataCadastro)
    {
        tipTextLabel.Text = dataCadastro.HasValue
            ? $"Data e Hora do Cadastro: {dataCadastro.Value:dd/MM/yyyy HH:mm}"
            : "Data e Hora do Cadastro: -";
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
        ConfigurarCard(ModoCard.Bloqueado);
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
    }

    // Controla o conteudo do card "Dados do Cargo":
    // - Bloqueado (abertura / nenhuma selecao): nenhum campo, apenas o titulo.
    // - Novo      (Novo Cargo): Nome e Situacao editaveis + Descricao.
    // - Edicao    (cargo selecionado): apenas a Descricao; o status vai pelo toggle Inativar/Reativar.
    private void ConfigurarCard(ModoCard modo)
    {
        if (_modoCard == modo)
        {
            return;
        }

        _modoCard = modo;
        bool novo = modo == ModoCard.Novo;
        bool edicao = modo == ModoCard.Edicao;
        bool mostrarDescricao = novo || edicao;

        nomePerfilLabel.Visible = novo;
        nomePerfilInputPanel.Visible = novo;
        situacaoLabel.Visible = novo;
        situacaoInputPanel.Visible = novo;
        // Situacao so e escolhivel no cadastro; depois fica oculta (status pelo toggle Inativar/Reativar).
        situacaoComboBox.Enabled = novo;

        descricaoLabel.Visible = mostrarDescricao;
        descricaoInputPanel.Visible = mostrarDescricao;
        detailsTopDividerLabel.Visible = mostrarDescricao;

        _edicaoCargoExistente = edicao;
        LayoutDetailsCard();
    }

    private enum ModoCard
    {
        Bloqueado,
        Novo,
        Edicao
    }
    private void AtualizarBotoesAcao(ModoAcaoBotoes modo)
    {
        _modoAcaoBotoesAtual = modo;

        bool podeCriar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Cargo, PermissoesSistema.Acoes.Criar);
        bool podeEditar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Cargo, PermissoesSistema.Acoes.Editar);
        bool podeExcluir = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Cargo, PermissoesSistema.Acoes.Excluir);

        salvarButton.Visible = modo == ModoAcaoBotoes.SomenteSalvar && podeCriar;
        novoButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeEditar;
        // No modo edicao, o botao salva apenas dados cadastrais; a situacao muda por Inativar/Reativar.
        novoButton.Text = "Salvar Alterações          F6";

        // Botao unico de status: alterna entre Inativar (cargo ativo) e Reativar (cargo inativo).
        if (modo == ModoAcaoBotoes.EditarExcluir)
        {
            CargoCadastro? selecionado = _cargoPorLinha.Values.FirstOrDefault(x => x.IdCargo == _idCargoAtual);
            bool ativo = selecionado?.SituacaoCargo ?? true;
            if (ativo)
            {
                excluirButton.Text = "Inativar Cargo            F8";
                excluirButton.ForeColor = Color.FromArgb(229, 27, 43);
                excluirButton.Visible = podeExcluir;
            }
            else
            {
                excluirButton.Text = "Reativar Cargo            F8";
                excluirButton.ForeColor = Color.FromArgb(22, 163, 74);
                excluirButton.Visible = podeEditar;
            }
        }
        else
        {
            excluirButton.Visible = false;
        }

        // Bloqueia duplo-clique / reentrancia enquanto uma operacao esta em andamento.
        bool habilitar = !_operacaoEmAndamento;
        salvarButton.Enabled = habilitar;
        novoButton.Enabled = habilitar;
        excluirButton.Enabled = habilitar;
    }

    private enum ModoAcaoBotoes
    {
        Nenhum = 0,
        SomenteSalvar = 1,
        EditarExcluir = 2
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
        if (!_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker))
        {
            return;
        }

        marker.Parent = rowPanel;
        marker.Bounds = new Rectangle(0, 0, marker.Width, rowPanel.Height);
        marker.Visible = true;
        marker.BringToFront();
    }

    private void AtualizarBadgeStatus(Panel rowPanel, string status)
    {
        RoundedPanel? statusPanel = ObterStatusPanelPorRow(rowPanel);
        Label? statusLabel = ObterStatusLabelPorRow(rowPanel);
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

    private RoundedPanel? ObterStatusPanelPorRow(Panel rowPanel)
    {
        return _statusPanelPorLinha.TryGetValue(rowPanel, out RoundedPanel? panel) ? panel : null;
    }

    private Label? ObterStatusLabelPorRow(Panel rowPanel)
    {
        return _statusLabelPorLinha.TryGetValue(rowPanel, out Label? label) ? label : null;
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










