using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Cadastro;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Tela.Comum;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela.Cadastro;

public partial class TaraForm : Form
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
    private readonly Dictionary<Panel, long> _idTaraPorLinha = new();
    private readonly Dictionary<Panel, TaraCadastro> _taraPorLinha = new();
    private readonly List<TaraCadastro> _tarasCarregadas = new();
    private readonly ToolTip _toolTipTara = new();

    private readonly TaraController _taraController;
    private readonly TipoTaraController _tipoTaraController;
    private readonly SetorController _setorController;
    private readonly AuditoriaServico _auditoriaServico;

    private long _idTaraAtual;
    // Alinhamento Setor/Cargo/TipoTara: operação protegida + modo de card + modo de botões + situação selecionada.
    private bool _operacaoEmAndamento;
    private ModoCard _modoCard = ModoCard.Novo;
    private ModoAcaoBotoes _modoAcaoBotoesAtual = ModoAcaoBotoes.Nenhum;
    private bool _situacaoSelecionadaAtiva = true;

    // Campo Tamanho criado em runtime (sem mexer no Designer).
    private Label? _lblTamanho;
    private RoundedPanel? _tamanhoInputPanel;
    private TextBox? _txtTamanho;

    private static readonly Color StatusAtivoFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color StatusAtivoTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color StatusInativoFundo = Color.FromArgb(255, 237, 213);
    private static readonly Color StatusInativoTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color StatusNeutroFundo = Color.FromArgb(243, 244, 246);
    private static readonly Color StatusNeutroTexto = Color.FromArgb(100, 116, 139);

    public TaraForm(
        TaraController? taraController = null,
        TipoTaraController? tipoTaraController = null,
        SetorController? setorController = null,
        AuditoriaServico? auditoriaServico = null)
    {
        _taraController = taraController ?? FabricaControladoresCadastro.CriarTaraController();
        _tipoTaraController = tipoTaraController ?? FabricaControladoresCadastro.CriarTipoTaraController();
        _setorController = setorController ?? FabricaControladoresCadastro.CriarSetorController();
        _auditoriaServico = auditoriaServico ?? FabricaControladoresCadastro.CriarAuditoriaServico();

        InitializeComponent();
        global::FugaPET_Dev.Tela.Comum.IconeJanelaHelper.AplicarIconePadrao(this);
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";

        ConfigureWindowButtons();
        ConfigureDragOnTitleBar();
        ConfigureNovoTaraAction();
        ConfigureProfilesSearchFilter();

        CriarCampoTamanhoRuntime();

        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        detailsCard.Resize += (_, _) => LayoutDetailsCard();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();

        InitializeProfilesTableSelection();

        LayoutProfilesCard();
        LayoutDetailsCard();
        LayoutSummaryCard();

        AtualizarRodapePerfis();
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
        ConfigurarCard(ModoCard.Vazio);
        // Ajuste 5/10: combos críticos como DropDownList + MaxLength dos campos de texto.
        situacaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        CmbTipoTara.DropDownStyle = ComboBoxStyle.DropDownList;
        CmbSetor.DropDownStyle = ComboBoxStyle.DropDownList;
        nomePerfilTextBox.MaxLength = TaraCadastro.TamanhoMaximoNome;
        descricaoTextBox.MaxLength = TaraCadastro.TamanhoMaximoObservacao;
        TxtPeso.MaxLength = 18;
        ConectarAcoesCadastro();
        Shown += async (_, _) => await InicializarTelaAsync();
    }

    // Alinhamento Setor/Cargo/TipoTara (Ajuste 1): valida permissão de visualização antes de carregar; em acesso
    // direto sem permissão, audita, avisa e fecha a tela.
    private async Task InicializarTelaAsync()
    {
        if (!AutorizacaoServico.PodeVisualizarRotina(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Tara))
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
            await InicializarDadosAsync();
        }
    }

    private async Task RegistrarAcessoDiretoNegadoSeguroAsync(long codigoUsuario)
    {
        try
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(
                codigoUsuario,
                $"Acesso direto negado a Cadastro de Tara ({PermissoesSistema.Modulos.Cadastro}/{PermissoesSistema.Rotinas.Tara}/CONSULTAR ou VISUALIZAR).",
                nameof(TaraForm));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Falha ao registrar acesso direto negado ao TaraForm: {ex}");
        }
    }

    // Ajuste 2: bloqueia reentrância; desabilita botões durante a operação e reabilita no finally.
    private async Task ExecutarOperacaoProtegidaAsync(Func<Task> operacao, string codigoErro)
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
                await ErroUsuarioHelper.TratarAsync(codigoErro, ex, nameof(TaraForm), "Não foi possível concluir a operação de tara. Acione o suporte."),
                "Cadastro de Tara",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoesAcao(_modoAcaoBotoesAtual);
        }
    }

    // Ajuste 3: atalhos F5 Salvar, F6 Editar, F8 Inativar/Reativar — só se Visible && Enabled (não burla permissão).
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

    // Ajuste 4: modo do card (Vazio/Novo/Edição) controla a visibilidade dos campos editáveis.
    private void ConfigurarCard(ModoCard modo)
    {
        if (_modoCard == modo)
        {
            return;
        }

        _modoCard = modo;
        // Situação só é escolhível na CRIAÇÃO; na edição fica bloqueada (status muda por Inativar/Reativar).
        situacaoComboBox.Enabled = modo == ModoCard.Novo;
    }

    private enum ModoCard
    {
        Vazio,
        Novo,
        Edicao
    }

    private async Task InicializarDadosAsync()
    {
        await PopularCombosAsync();
        await CarregarTarasAsync();
    }

    private async Task PopularCombosAsync()
    {
        try
        {
            IReadOnlyList<TipoTaraCadastro> tipos = await _tipoTaraController.ListarAsync();
            CmbTipoTara.DisplayMember = "Nome";
            CmbTipoTara.ValueMember = "Id";
            CmbTipoTara.DataSource = tipos.Where(t => t.SituacaoTipoTara).ToList();
            CmbTipoTara.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync("TARA_CARREGAR_TIPOS_ERRO", ex, "TaraForm",
                    "Não foi possível carregar os tipos de tara. Acione o suporte."),
                "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

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
                await ErroUsuarioHelper.TratarAsync("TARA_CARREGAR_SETORES_ERRO", ex, "TaraForm",
                    "Não foi possível carregar os setores. Acione o suporte."),
                "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task CarregarTarasAsync()
    {
        try
        {
            IReadOnlyList<TaraCadastro> taras = await _taraController.ListarAsync();
            _tarasCarregadas.Clear();
            _tarasCarregadas.AddRange(taras);
            RecriarLinhasPerfis();
            PopularLinhasComTaras(_tarasCarregadas);
            ApplyProfilesFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync("TARA_CARREGAR_ERRO", ex, "TaraForm",
                    "Não foi possível carregar as taras. Acione o suporte."),
                "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ============================================================
    // Acoes CRUD
    // ============================================================

    private void ConectarAcoesCadastro()
    {
        // Ajuste 2: toda ação passa pela proteção contra operação duplicada.
        salvarButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(SalvarTaraAsync, "TARA_SALVAR_ERRO");
        novoButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(EditarTaraAsync, "TARA_EDITAR_ERRO");
        // Ajuste 6: botão de status alterna Inativar/Reativar conforme a situação selecionada.
        excluirButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(AlternarSituacaoTaraAsync, "TARA_ALTERAR_SITUACAO_ERRO");
    }

    // Ajuste 6: tara ativa → Inativar (Excluir); tara inativa → Reativar.
    private Task AlternarSituacaoTaraAsync()
        => _situacaoSelecionadaAtiva ? ExcluirTaraAsync() : ReativarTaraAsync();

    private async Task ReativarTaraAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idTaraAtual <= 0)
        {
            MessageBox.Show("Selecione uma tara inativa para reativar.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            "Confirma a reativação da tara atual?",
            "Cadastro de Tara",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes)
        {
            return;
        }

        ResultadoOperacao resultado = await _taraController.ReativarAsync(_idTaraAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Tara", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idTaraAtual = 0;
            PrepareNewTara();
            await CarregarTarasAsync();
        }
    }

    private async Task SalvarTaraAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Ajuste 9: peso válido em KG (> 0), sem parse silencioso para zero.
        if (!TryParsePesoKg(TxtPeso.Text, out decimal pesoKg))
        {
            MessageBox.Show("Informe um peso de tara válido em KG, maior que zero.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Ajuste 5: situação explícita "Ativo"/"Inativo" (nunca vazia/inválida convertida a Inativo).
        if (!SituacaoCadastroHelper.TryInterpretarSituacao(situacaoComboBox.Text, out bool situacaoAtiva))
        {
            MessageBox.Show("Selecione a situação (Ativo ou Inativo).", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        TaraCadastro tara = new()
        {
            CodigoTipoTara = TryParseLong(CmbTipoTara.SelectedValue),
            CodigoSetor = TryParseLong(CmbSetor.SelectedValue),
            NomeTara = nomePerfilTextBox.Text.Trim(),
            Tamanho = _txtTamanho?.Text.Trim() ?? string.Empty,
            PesoKg = pesoKg,
            Observacao = descricaoTextBox.Text.Trim(),
            SituacaoTara = situacaoAtiva,
            TaraCriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario
        };

        ResultadoOperacao resultado = await _taraController.InserirAsync(tara);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Tara", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idTaraAtual = resultado.IdGerado ?? 0;
            PrepareNewTara();
            await CarregarTarasAsync();
        }
    }

    private async Task EditarTaraAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idTaraAtual <= 0)
        {
            MessageBox.Show("Selecione uma tara para editar.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        TaraCadastro? taraSelecionada = _taraPorLinha.Values.FirstOrDefault(x => x.CodigoTara == _idTaraAtual);
        if (taraSelecionada is null)
        {
            MessageBox.Show("Selecione uma tara valida para editar.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Ajuste 9: peso válido em KG (> 0).
        if (!TryParsePesoKg(TxtPeso.Text, out decimal pesoKg))
        {
            MessageBox.Show("Informe um peso de tara válido em KG, maior que zero.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        TaraCadastro taraAtualizada = new()
        {
            CodigoTara = _idTaraAtual,
            CodigoTipoTara = TryParseLong(CmbTipoTara.SelectedValue),
            CodigoSetor = TryParseLong(CmbSetor.SelectedValue),
            NomeTara = nomePerfilTextBox.Text.Trim(),
            Tamanho = _txtTamanho?.Text.Trim() ?? string.Empty,
            PesoKg = pesoKg,
            Observacao = descricaoTextBox.Text.Trim(),
            // Ajuste 5: situação NÃO muda por edição — envia a situação atual (o serviço bloqueia mudanças).
            SituacaoTara = taraSelecionada.SituacaoTara,
            TaraAtualizadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario
        };

        ResultadoOperacao resultado = await _taraController.AtualizarAsync(taraAtualizada);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Tara", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            await CarregarTarasAsync();
        }
    }

    private async Task ExcluirTaraAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco está desabilitada temporariamente.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_idTaraAtual <= 0)
        {
            MessageBox.Show("Salve ou selecione uma tara para inativar.", "Cadastro de Tara", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirmacao = MessageBox.Show(
            "Confirma a inativação da tara atual?\n\nA tara ficará inativa, mas pode ser reativada depois.",
            "Cadastro de Tara",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmacao != DialogResult.Yes)
        {
            return;
        }

        ResultadoOperacao resultado = await _taraController.ExcluirAsync(_idTaraAtual);
        MessageBox.Show(resultado.Mensagem, "Cadastro de Tara", MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (resultado.Sucesso)
        {
            _idTaraAtual = 0;
            PrepareNewTara();
            await CarregarTarasAsync();
        }
    }

    private void PrepareNewTara()
    {
        _idTaraAtual = 0;
        _situacaoSelecionadaAtiva = true;
        // Ajuste 4: modo Novo — campos habilitados; situação editável só na CRIAÇÃO, iniciando "Ativo".
        ConfigurarCard(ModoCard.Novo);
        nomePerfilTextBox.Text = string.Empty;
        descricaoTextBox.Text = string.Empty;
        if (_txtTamanho is not null) _txtTamanho.Text = string.Empty;
        TxtPeso.Text = string.Empty;
        situacaoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo;
        CmbTipoTara.SelectedIndex = -1;
        CmbSetor.SelectedIndex = -1;
        nomePerfilTextBox.ReadOnly = false;
        nomePerfilTextBox.BackColor = Color.White;

        nomePerfilTextBox.Focus();
        nomePerfilTextBox.SelectionStart = 0;
        nomePerfilTextBox.SelectionLength = 0;
        AtualizarBotoesAcao(ModoAcaoBotoes.SomenteSalvar);
    }

    private static long TryParseLong(object? value)
    {
        if (value is long l) return l;
        if (value is int i) return i;
        if (value is string s && long.TryParse(s, out long p)) return p;
        return 0;
    }

    // Ajuste 9: peso da tara em KG. Aceita vírgula OU ponto. Retorna false para vazio/inválido/<= 0 (nunca
    // converte silenciosamente para zero). internal static para teste direto (InternalsVisibleTo).
    internal static bool TryParsePesoKg(string? texto, out decimal pesoKg)
    {
        pesoKg = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string limpo = texto.Trim().Replace(" ", string.Empty).Replace(',', '.');
        if (!decimal.TryParse(limpo, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal valor) || valor <= 0m)
        {
            return false;
        }

        pesoKg = valor;
        return true;
    }

    // ============================================================
    // Renderizacao dinamica das linhas (padrao SetorForm)
    // ============================================================

    private void ConfigureProfilesSearchFilter()
    {
        searchTextBox.TextChanged += (_, _) => ApplyProfilesFilter();
    }

    private void RecriarLinhasPerfis()
    {
        RemoverLinhasPerfisExistentes();

        for (int i = 0; i < _tarasCarregadas.Count; i++)
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
        _idTaraPorLinha.Clear();
        _taraPorLinha.Clear();
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

    private void PopularLinhasComTaras(IReadOnlyList<TaraCadastro> taras)
    {
        _idTaraPorLinha.Clear();
        _taraPorLinha.Clear();

        for (int i = 0; i < _profileSearchRows.Count; i++)
        {
            TaraCadastro tara = taras[i];
            ProfileSearchRow linha = _profileSearchRows[i];

            linha.NameLabel.Text = tara.NomeTara;
            string pesoFormatado = FormatarPeso(tara.PesoKg);
            _toolTipTara.SetToolTip(linha.NameLabel, $"{tara.NomeTara} ({pesoFormatado} kg)");
            linha.UsersLabel.Text = pesoFormatado;
            linha.StatusLabel.Text = tara.SituacaoTara ? "Ativo" : "Inativo";
            AtualizarBadgeStatus(linha.RowPanel, linha.StatusLabel.Text);
            _idTaraPorLinha[linha.RowPanel] = tara.CodigoTara;
            _taraPorLinha[linha.RowPanel] = tara;
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

        // Ajuste 13: o filtro apenas oculta/exibe. Mantém a seleção real se a linha continuar visível; senão limpa.
        RestaurarSelecaoAposFiltro();
    }

    private void RestaurarSelecaoAposFiltro()
    {
        Panel? selecionada = null;
        if (_idTaraAtual > 0)
        {
            foreach (KeyValuePair<Panel, long> par in _idTaraPorLinha)
            {
                if (par.Value == _idTaraAtual)
                {
                    selecionada = par.Key;
                    break;
                }
            }
        }

        if (selecionada is not null && selecionada.Visible)
        {
            DestacarLinhaSelecionada(selecionada);
            return;
        }

        _idTaraAtual = 0;
        ClearRowSelection();
        ConfigurarCard(ModoCard.Vazio);
    }

    private void DestacarLinhaSelecionada(Panel selectedRowPanel)
    {
        Color selectedBackColor = Color.FromArgb(254, 242, 242);
        foreach (RowSelection row in _rowSelections)
        {
            bool isSelected = row.RowPanel == selectedRowPanel;
            row.RowPanel.BackColor = isSelected ? selectedBackColor : row.NormalBackColor;
            if (isSelected) ShowMarkerForRow(row.RowPanel);
            else HideMarkerForRow(row.RowPanel);
        }
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
        int total = _tarasCarregadas.Count;
        profilesFooterLabel.Text = $"Exibindo {exibindo} de {total} taras";
    }

    private void InitializeProfilesTableSelection()
    {
        profilesTablePanel.AutoScroll = true;
        // Esconde as 5 linhas fixas do Designer; usaremos linhas dinamicas.
        EsconderLinhasFixasDoDesigner();
        _profileSearchRows.Clear();
        _rowSelections.Clear();
        _rowSelectionMarkers.Clear();
    }

    private void EsconderLinhasFixasDoDesigner()
    {
        Panel[] linhasFixas = [profileRow1Panel, profileRow2Panel, profileRow3Panel, profileRow4Panel, profileRow5Panel];
        foreach (Panel linha in linhasFixas)
        {
            linha.Visible = false;
        }
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
        DestacarLinhaSelecionada(selectedRowPanel);

        _idTaraAtual = _idTaraPorLinha.TryGetValue(selectedRowPanel, out long id) ? id : 0;
        PreencherCamposTaraPorLinha(selectedRowPanel);
        SyncSummaryFromRow(selectedRowPanel);
        AtualizarBotoesAcao(ModoAcaoBotoes.EditarExcluir);
    }

    private void PreencherCamposTaraPorLinha(Panel rowPanel)
    {
        if (!_taraPorLinha.TryGetValue(rowPanel, out TaraCadastro? tara))
        {
            return;
        }

        // Ajuste 4: modo Edição — campos editáveis; situação bloqueada (muda só por Inativar/Reativar).
        ConfigurarCard(ModoCard.Edicao);
        nomePerfilTextBox.Text = tara.NomeTara;
        descricaoTextBox.Text = tara.Observacao;
        if (_txtTamanho is not null) _txtTamanho.Text = tara.Tamanho;
        TxtPeso.Text = FormatarPeso(tara.PesoKg);
        situacaoComboBox.SelectedItem = tara.SituacaoTara ? SituacaoCadastroHelper.Ativo : SituacaoCadastroHelper.Inativo;
        _situacaoSelecionadaAtiva = tara.SituacaoTara;
        CmbTipoTara.SelectedValue = tara.CodigoTipoTara;
        CmbSetor.SelectedValue = tara.CodigoSetor;
        // Ajuste 4: nome editável (renomear é seguro; vínculos com a tara são por código).
        nomePerfilTextBox.ReadOnly = false;
        nomePerfilTextBox.BackColor = Color.White;
    }

    private void SyncSummaryFromRow(Panel rowPanel)
    {
        ProfileSearchRow? row = _profileSearchRows.FirstOrDefault(x => x.RowPanel == rowPanel);
        if (row is null || !_taraPorLinha.TryGetValue(rowPanel, out TaraCadastro? tara))
        {
            ClearSummarySelectionValues();
            return;
        }

        summaryPerfilValueLabel.Text = row.NameLabel.Text;
        summarySituacaoValueLabel.Text = row.StatusLabel.Text;
        summaryUsuariosValueLabel.Text = row.UsersLabel.Text;
        tipTextLabel.Text = FormatarUltimoCadastro(tara.TaraCriadoEm);
    }

    private void ClearSummarySelectionValues()
    {
        summaryPerfilValueLabel.Text = "-";
        summarySituacaoValueLabel.Text = "-";
        summaryUsuariosValueLabel.Text = "-";
        tipTextLabel.Text = "Último cadastro: -";
        AtualizarBotoesAcao(ModoAcaoBotoes.Nenhum);
    }

    private static string FormatarUltimoCadastro(DateTime? dataCadastro)
    {
        if (!dataCadastro.HasValue)
        {
            return "Último cadastro: -";
        }

        return $"Último cadastro: {dataCadastro.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"))}";
    }

    private void AtualizarBotoesAcao(ModoAcaoBotoes modo)
    {
        _modoAcaoBotoesAtual = modo;

        bool podeCriar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Tara, PermissoesSistema.Acoes.Criar);
        bool podeEditar = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Tara, PermissoesSistema.Acoes.Editar);
        bool podeExcluir = AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Tara, PermissoesSistema.Acoes.Excluir);

        salvarButton.Visible = modo == ModoAcaoBotoes.SomenteSalvar && podeCriar;
        novoButton.Visible = modo == ModoAcaoBotoes.EditarExcluir && podeEditar;

        // Ajuste 6: botão de status (texto/cor/visibilidade/permissão) centralizado a partir do registro selecionado.
        if (modo == ModoAcaoBotoes.EditarExcluir)
        {
            TaraCadastro? selecionada = _taraPorLinha.Values.FirstOrDefault(x => x.CodigoTara == _idTaraAtual);
            bool ativo = selecionada?.SituacaoTara ?? true;
            _situacaoSelecionadaAtiva = ativo;
            if (ativo)
            {
                excluirButton.Text = "Inativar Tara             F8";
                excluirButton.ForeColor = Color.FromArgb(229, 27, 43);
                excluirButton.Visible = podeExcluir;
            }
            else
            {
                excluirButton.Text = "Reativar Tara             F8";
                excluirButton.ForeColor = Color.FromArgb(22, 163, 74);
                excluirButton.Visible = podeEditar;
            }
        }
        else
        {
            excluirButton.Visible = false;
        }

        // Ajuste 2: bloqueio de duplo-clique / reentrância enquanto uma operação está em andamento.
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

    private static string FormatarPeso(decimal pesoKg)
        => pesoKg.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));

    // ============================================================
    // Configuracao de janela e drag
    // ============================================================

    private void ConfigureNovoTaraAction()
    {
        SetHandCursor(novoPerfilButtonPanel);
        AttachNovoTaraClick(novoPerfilButtonPanel);
    }

    private static void SetHandCursor(Control control)
    {
        control.Cursor = Cursors.Hand;
        foreach (Control child in control.Controls) SetHandCursor(child);
    }

    private void AttachNovoTaraClick(Control control)
    {
        control.Click += (_, _) => PrepareNewTara();
        foreach (Control child in control.Controls) AttachNovoTaraClick(child);
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
    // Layouts (mantidos do Designer original, adaptados)
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
        ApplyScaledFont(LblTipoTara, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblSetorTara, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(nomePerfilTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(descricaoTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(TxtPeso, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(CmbTipoTara, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(CmbSetor, 9F, contentScale, 9F, 12F);

        if (_lblTamanho is not null) ApplyScaledFont(_lblTamanho, 7.75F, contentScale, 8.5F, 11F);
        if (_txtTamanho is not null) ApplyScaledFont(_txtTamanho, 9F, contentScale, 9F, 12F);

        situacaoComboBox.IntegralHeight = false;
        situacaoComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        CmbTipoTara.IntegralHeight = false;
        CmbTipoTara.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        CmbSetor.IntegralHeight = false;
        CmbSetor.DropDownHeight = Math.Max(96, Scale(120, scaleY));

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

        // Linha 2: Descricao (col1) | Peso (col2)
        SetBounds(descricaoLabel, col1, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(descricaoInputPanel, col1, Scale(140, scaleY), fieldWidth, Math.Max(95, Scale(166, scaleY)));
        SetBounds(descricaoTextBox, Scale(12, scaleX), Scale(10, scaleY), Math.Max(120, fieldWidth - Scale(24, scaleX)), Math.Max(60, descricaoInputPanel.Height - Scale(16, scaleY)));

        SetBounds(LblPeso, col2, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(roundedPanel1, col2, Scale(140, scaleY), fieldWidth, Scale(33, scaleY));
        SetBounds(TxtPeso, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));

        // Linha 3 (col2): Tipo de Tara
        SetBounds(LblTipoTara, col2, Scale(187, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(RdpTipoTara, col2, Scale(209, scaleY), fieldWidth, Scale(33, scaleY));
        int tipoComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int tipoComboHeight = Math.Max(22, CmbTipoTara.PreferredHeight);
        int tipoComboY = Math.Max(2, (RdpTipoTara.Height - tipoComboHeight) / 2);
        SetBounds(CmbTipoTara, Scale(12, scaleX), tipoComboY, tipoComboWidth, tipoComboHeight);

        // Linha 4 (col2): Setor
        SetBounds(LblSetorTara, col2, Scale(251, scaleY), fieldWidth, Scale(19, scaleY));
        SetBounds(RdpSetor, col2, Scale(273, scaleY), fieldWidth, Scale(33, scaleY));
        int setorComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int setorComboHeight = Math.Max(22, CmbSetor.PreferredHeight);
        int setorComboY = Math.Max(2, (RdpSetor.Height - setorComboHeight) / 2);
        SetBounds(CmbSetor, Scale(12, scaleX), setorComboY, setorComboWidth, setorComboHeight);

        // Linha 5 (col1, abaixo da descricao): campo Tamanho (criado em runtime)
        if (_lblTamanho is not null && _tamanhoInputPanel is not null && _txtTamanho is not null)
        {
            int tamanhoY = descricaoInputPanel.Bottom + Scale(12, scaleY);
            SetBounds(_lblTamanho, col1, tamanhoY, fieldWidth, Scale(19, scaleY));
            SetBounds(_tamanhoInputPanel, col1, tamanhoY + Scale(22, scaleY), fieldWidth, Scale(33, scaleY));
            SetBounds(_txtTamanho, Scale(12, scaleX), Scale(8, scaleY), Math.Max(80, fieldWidth - Scale(24, scaleX)), Scale(16, scaleY));
        }

        SetBounds(detailsTopDividerLabel, col1,
            (_tamanhoInputPanel?.Bottom ?? descricaoInputPanel.Bottom) + Scale(12, scaleY),
            layoutWidth, 1);
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
    // Markers e badges de status
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
        if (_rowSelectionMarkers.TryGetValue(rowPanel, out Panel? marker))
        {
            marker.Visible = false;
        }
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
    // Campo Tamanho criado em runtime
    // ============================================================

    private void CriarCampoTamanhoRuntime()
    {
        _lblTamanho = new Label
        {
            Name = "lblTamanhoRuntime",
            Text = "Tamanho",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = nomePerfilLabel.ForeColor,
            Font = nomePerfilLabel.Font,
            BackColor = Color.Transparent
        };

        _tamanhoInputPanel = new RoundedPanel
        {
            Name = "tamanhoInputPanelRuntime",
            BorderRadius = nomePerfilInputPanel is RoundedPanel rpNome ? rpNome.BorderRadius : 6,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(214, 219, 226),
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };

        _txtTamanho = new TextBox
        {
            Name = "txtTamanhoRuntime",
            BorderStyle = BorderStyle.None,
            Font = nomePerfilTextBox.Font,
            BackColor = Color.White,
            MaxLength = TaraCadastro.TamanhoMaximoTamanho // Ajuste 10
        };

        _tamanhoInputPanel.Controls.Add(_txtTamanho);
        detailsCard.Controls.Add(_lblTamanho);
        detailsCard.Controls.Add(_tamanhoInputPanel);

        _lblTamanho.BringToFront();
        _tamanhoInputPanel.BringToFront();
        _txtTamanho.BringToFront();
    }

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
