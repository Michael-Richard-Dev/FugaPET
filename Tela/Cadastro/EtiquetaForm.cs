using System.Globalization;
using System.Runtime.InteropServices;
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

public partial class EtiquetaForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private static readonly Color MarkerColor = Color.FromArgb(239, 68, 68);
    private static readonly string[] TiposEtiqueta = ["CAIXA", "PALETE", "HU", "INTERNA", "OUTRA"];

    private readonly EtiquetaController _etiquetaController;
    private readonly ModeloEtiquetaController _modeloEtiquetaController;
    private readonly AuditoriaServico _auditoriaServico;
    private readonly System.Windows.Forms.Timer _relogioTimer = new() { Interval = 1000 };
    private readonly Panel _etiquetasRowsPanel = new();
    private readonly Label _estadoVazioListaLabel = new();
    private readonly Label _detailsEmptyLabel = new();
    private readonly List<LinhaEtiquetaUi> _linhasEtiquetas = [];
    private IReadOnlyList<EtiquetaCadastro> _etiquetasCarregadas = [];
    private IReadOnlyList<ModeloEtiquetaCadastro> _modelosCarregados = [];
    private Button? camposEtiquetaButton;
    private long _idEtiquetaAtual;
    private bool _situacaoSelecionadaAtiva = true;
    private bool _operacaoEmAndamento;
    private ModoCard _modoCard = ModoCard.Vazio;

    public EtiquetaForm(
        EtiquetaController? etiquetaController = null,
        ModeloEtiquetaController? modeloEtiquetaController = null,
        AuditoriaServico? auditoriaServico = null)
    {
        _etiquetaController = etiquetaController ?? FabricaControladoresCadastro.CriarEtiquetaController();
        _modeloEtiquetaController = modeloEtiquetaController ?? FabricaControladoresCadastro.CriarModeloEtiquetaController();
        _auditoriaServico = auditoriaServico ?? FabricaControladoresCadastro.CriarAuditoriaServico();

        InitializeComponent();
        IconeJanelaHelper.AplicarIconePadrao(this);
        ConfigurarTela();
        ConfigurarListaDinamica();
        CriarControlesAuxiliaresOriginais();
        ConectarEventos();
        LayoutProfilesCard();
        LayoutDetailsCard();
        LayoutSummaryCard();
        AplicarModoCard(ModoCard.Vazio);
        AtualizarBotoes();
        Shown += async (_, _) => await ExecutarOperacaoProtegidaAsync(InicializarTelaAsync, "ETIQUETA_INICIALIZAR_ERRO");
    }

    private enum ModoCard
    {
        Vazio,
        Novo,
        Edicao
    }

    private sealed record ModeloEtiquetaOpcao(long Codigo, string Texto, bool Ativo);

    private sealed record LinhaEtiquetaUi(
        Panel RowPanel,
        Label NameLabel,
        Label TypeLabel,
        RoundedPanel StatusPanel,
        Label StatusLabel,
        Panel MarkerPanel,
        EtiquetaCadastro Etiqueta);

    private void ConfigurarTela()
    {
        sapStatusPanel.Visible = false;
        headerSubtitleLabel.Text = "Cadastro e manutenção de etiqueta";
        cellUserText.Text = UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        AtualizarDataHora();

        tipoEtiquetaComboBox.Items.Clear();
        tipoEtiquetaComboBox.Items.AddRange(TiposEtiqueta);
        tipoEtiquetaComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        modeloEtiquetaComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        situacaoComboBox.Items.Clear();
        situacaoComboBox.Items.Add(SituacaoCadastroHelper.Ativo);
        situacaoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo;
        situacaoComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

        codigoInternoTextBox.MaxLength = EtiquetaCadastro.TamanhoMaximoCodigoInterno;
        nomeEtiquetaTextBox.MaxLength = EtiquetaCadastro.TamanhoMaximoNome;
        descricaoEtiquetaTextBox.MaxLength = EtiquetaCadastro.TamanhoMaximoDescricao;
        searchTextBox.MaxLength = 120;

        profilesHeaderProfileLabel.Text = "Etiqueta";
        profilesHeaderUsersLabel.Text = "Tipo";
        summaryUsuariosCaptionLabel.Text = "Código interno";
        profilesFooterLabel.Text = "Exibindo 0 de 0 etiquetas";

        _relogioTimer.Tick += (_, _) => AtualizarDataHora();
        _relogioTimer.Start();
        FormClosed += (_, _) => _relogioTimer.Dispose();
    }

    private void ConfigurarListaDinamica()
    {
        Panel[] linhasEstaticas =
        [
            profileRow1Panel,
            profileRow2Panel,
            profileRow3Panel,
            profileRow4Panel,
            profileRow5Panel
        ];

        foreach (Panel linha in linhasEstaticas)
        {
            linha.Visible = false;
        }

        _etiquetasRowsPanel.Name = "etiquetasRowsPanel";
        _etiquetasRowsPanel.BackColor = Color.White;
        _etiquetasRowsPanel.AutoScroll = true;
        _etiquetasRowsPanel.TabIndex = 10;
        profilesTablePanel.Controls.Add(_etiquetasRowsPanel);
        _etiquetasRowsPanel.BringToFront();

        _estadoVazioListaLabel.Name = "estadoVazioListaLabel";
        _estadoVazioListaLabel.BackColor = Color.White;
        _estadoVazioListaLabel.Font = new Font("Segoe UI", 9F);
        _estadoVazioListaLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _estadoVazioListaLabel.Text = "Nenhuma etiqueta cadastrada.";
        _estadoVazioListaLabel.TextAlign = ContentAlignment.MiddleCenter;
        _estadoVazioListaLabel.Visible = false;
        _etiquetasRowsPanel.Controls.Add(_estadoVazioListaLabel);
    }

    private void CriarControlesAuxiliaresOriginais()
    {
        camposEtiquetaButton = new Button
        {
            Name = "camposEtiquetaButton",
            Text = "Campos da Etiqueta",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Width = 180,
            Height = 28,
            Cursor = Cursors.Hand
        };
        camposEtiquetaButton.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        detailsCard.Controls.Add(camposEtiquetaButton);
        camposEtiquetaButton.BringToFront();

        _detailsEmptyLabel.Name = "detailsEmptyLabel";
        _detailsEmptyLabel.Font = new Font("Segoe UI", 9F);
        _detailsEmptyLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _detailsEmptyLabel.Text = "Selecione uma etiqueta cadastrada ou clique em Nova Etiqueta para iniciar.";
        _detailsEmptyLabel.TextAlign = ContentAlignment.MiddleCenter;
        detailsCard.Controls.Add(_detailsEmptyLabel);
        _detailsEmptyLabel.BringToFront();
        PosicionarControlesAuxiliares();
    }

    private void ConectarEventos()
    {
        menuHeaderLabel.Click += (_, _) => Close();
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        closeWindowLabel.Click += (_, _) => Close();
        customTitleBarPanel.MouseDown += ArrastarJanela;
        headerTitleLabel.MouseDown += ArrastarJanela;
        headerSubtitleLabel.MouseDown += ArrastarJanela;
        companyLogoPictureBox.MouseDown += ArrastarJanela;

        ConfigurarCliqueNovaEtiqueta(novoPerfilButtonPanel);
        searchTextBox.TextChanged += (_, _) => AplicarFiltro();
        salvarButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(SalvarEtiquetaAsync, "ETIQUETA_SALVAR_ERRO");
        editarButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(EditarEtiquetaAsync, "ETIQUETA_EDITAR_ERRO");
        situacaoButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(AlternarSituacaoEtiquetaAsync, "ETIQUETA_SITUACAO_ERRO");
        camposEtiquetaButton!.Click += (_, _) => AbrirCamposEtiqueta();

        profilesCard.Resize += (_, _) => LayoutProfilesCard();
        detailsCard.Resize += (_, _) => LayoutDetailsCard();
        summaryCard.Resize += (_, _) => LayoutSummaryCard();
    }

    private void ConfigurarCliqueNovaEtiqueta(Control controle)
    {
        controle.Cursor = Cursors.Hand;
        controle.Click += (_, _) => PrepararNovaEtiqueta();
        foreach (Control filho in controle.Controls)
            ConfigurarCliqueNovaEtiqueta(filho);
    }

    private async Task InicializarTelaAsync()
    {
        if (!AutorizacaoServico.PodeVisualizarRotina(
                PermissoesSistema.Modulos.Etiqueta,
                PermissoesSistema.Rotinas.Etiqueta))
        {
            await AuditarAcessoDiretoNegadoSeguroAsync();
            MessageBox.Show(
                "Você não possui permissão para acessar o Cadastro de Etiqueta.",
                "Acesso negado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            Close();
            return;
        }

        if (!EstadoIntegracaoBanco.Habilitado)
        {
            MessageBox.Show(
                "A conexão com o banco está desabilitada neste ambiente.",
                "Cadastro de Etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        await CarregarModelosAtivosAsync();
        await CarregarEtiquetasAsync();
        VoltarAoModoVazio();
    }

    private async Task AuditarAcessoDiretoNegadoSeguroAsync()
    {
        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (!codigoUsuario.HasValue) return;

        try
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(
                codigoUsuario.Value,
                $"Acesso direto negado a {PermissoesSistema.Modulos.Etiqueta}/{PermissoesSistema.Rotinas.Etiqueta}/CONSULTAR ou VISUALIZAR.",
                nameof(EtiquetaForm));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Falha ao auditar acesso direto negado ao EtiquetaForm: {ex}");
        }
    }

    private async Task ExecutarOperacaoProtegidaAsync(Func<Task> operacao, string codigoErro)
    {
        if (_operacaoEmAndamento) return;

        _operacaoEmAndamento = true;
        AtualizarBotoes();
        try
        {
            await operacao();
        }
        catch (Exception ex)
        {
            string mensagem = await ErroUsuarioHelper.TratarAsync(
                codigoErro,
                ex,
                nameof(EtiquetaForm),
                "Não foi possível concluir a operação de etiqueta. Acione o suporte.");
            MessageBox.Show(mensagem, "Cadastro de Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoes();
        }
    }

    private async Task CarregarModelosAtivosAsync()
    {
        try
        {
            _modelosCarregados = await _modeloEtiquetaController.ListarAsync();
            ConfigurarComboModelos();
        }
        catch (Exception ex)
        {
            _modelosCarregados = [];
            ConfigurarComboModelos();
            throw new InvalidOperationException("Falha ao carregar modelos de etiqueta.", ex);
        }
    }

    private async Task CarregarEtiquetasAsync()
    {
        try
        {
            _etiquetasCarregadas = await _etiquetaController.ListarAsync();
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            _etiquetasCarregadas = [];
            LimparLinhasDinamicas();
            AtualizarRodapeLista(0);
            ExibirEstadoVazioLista("Não foi possível carregar as etiquetas.");
            VoltarAoModoVazio();

            string mensagem = await ErroUsuarioHelper.TratarAsync(
                "ETIQUETA_CARREGAR_ERRO",
                ex,
                nameof(EtiquetaForm),
                "Não foi possível carregar as etiquetas. Acione o suporte.");
            MessageBox.Show(mensagem, "Cadastro de Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AplicarFiltro()
    {
        long idSelecionado = _idEtiquetaAtual;
        string filtro = NormalizarPesquisa(searchTextBox.Text);
        IReadOnlyList<EtiquetaCadastro> filtradas = _etiquetasCarregadas
            .Where(etiqueta => string.IsNullOrEmpty(filtro)
                || NormalizarPesquisa(etiqueta.NomeEtiqueta).Contains(filtro, StringComparison.Ordinal)
                || NormalizarPesquisa(etiqueta.CodigoInterno).Contains(filtro, StringComparison.Ordinal)
                || NormalizarPesquisa(etiqueta.TipoEtiqueta).Contains(filtro, StringComparison.Ordinal))
            .ToList();

        _etiquetasRowsPanel.SuspendLayout();
        LimparLinhasDinamicas();
        LinhaEtiquetaUi? linhaSelecionada = null;
        foreach (EtiquetaCadastro etiqueta in filtradas)
        {
            LinhaEtiquetaUi linha = CriarLinhaEtiqueta(etiqueta, _linhasEtiquetas.Count);
            linha.RowPanel.Tag = etiqueta;
            _linhasEtiquetas.Add(linha);
            _etiquetasRowsPanel.Controls.Add(linha.RowPanel);
            if (etiqueta.CodigoEtiqueta == idSelecionado) linhaSelecionada = linha;
        }

        _estadoVazioListaLabel.Text = string.IsNullOrWhiteSpace(searchTextBox.Text)
            ? "Nenhuma etiqueta cadastrada."
            : "Nenhuma etiqueta encontrada para o filtro informado.";
        _estadoVazioListaLabel.Visible = filtradas.Count == 0;
        _estadoVazioListaLabel.BringToFront();
        AtualizarRodapeLista(filtradas.Count);
        ReposicionarLinhasDinamicas();
        _etiquetasRowsPanel.ResumeLayout();

        if (linhaSelecionada is not null)
            SelecionarEtiqueta(linhaSelecionada);
        else if (idSelecionado > 0)
            VoltarAoModoVazio();
    }

    private LinhaEtiquetaUi CriarLinhaEtiqueta(EtiquetaCadastro etiqueta, int indice)
    {
        Panel linha = new()
        {
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Height = 46,
            Name = $"etiquetaRow{etiqueta.CodigoEtiqueta}Panel",
            Tag = etiqueta
        };
        linha.Tag = etiqueta;
        Panel marcador = new()
        {
            BackColor = MarkerColor,
            Dock = DockStyle.Left,
            Name = $"etiquetaRow{etiqueta.CodigoEtiqueta}MarkerPanel",
            Visible = false,
            Width = 3
        };
        Label nome = new()
        {
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = etiqueta.NomeEtiqueta,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label tipo = new()
        {
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.25F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = etiqueta.TipoEtiqueta,
            TextAlign = ContentAlignment.MiddleCenter
        };
        RoundedPanel statusPanel = new()
        {
            BackColor = Color.Transparent,
            BorderColor = Color.Transparent,
            BorderRadius = 11,
            FillColor = etiqueta.SituacaoEtiqueta
                ? Color.FromArgb(220, 252, 231)
                : Color.FromArgb(254, 226, 226),
            ShadowBlur = 0,
            ShadowOffsetY = 0
        };
        Label status = new()
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = etiqueta.SituacaoEtiqueta
                ? Color.FromArgb(22, 163, 74)
                : Color.FromArgb(220, 38, 38),
            Text = etiqueta.SituacaoEtiqueta ? SituacaoCadastroHelper.Ativo : SituacaoCadastroHelper.Inativo,
            TextAlign = ContentAlignment.MiddleCenter
        };
        Panel separador = new()
        {
            BackColor = Color.FromArgb(241, 245, 249),
            Dock = DockStyle.Bottom,
            Height = 1
        };

        statusPanel.Controls.Add(status);
        linha.Controls.AddRange([nome, tipo, statusPanel, separador, marcador]);
        LinhaEtiquetaUi item = new(linha, nome, tipo, statusPanel, status, marcador, etiqueta);
        AnexarCliqueLinha(linha, item);
        return item;
    }

    private void AnexarCliqueLinha(Control controle, LinhaEtiquetaUi linha)
    {
        controle.Cursor = Cursors.Hand;
        controle.Click += (_, _) => SelecionarEtiqueta(linha);
        foreach (Control filho in controle.Controls)
            AnexarCliqueLinha(filho, linha);
    }

    private void SelecionarEtiqueta(LinhaEtiquetaUi linhaSelecionada)
    {
        foreach (LinhaEtiquetaUi linha in _linhasEtiquetas)
        {
            bool selecionada = ReferenceEquals(linha, linhaSelecionada);
            linha.RowPanel.BackColor = selecionada ? Color.FromArgb(254, 242, 242) : Color.White;
            linha.MarkerPanel.Visible = selecionada;
        }

        EtiquetaCadastro etiqueta = linhaSelecionada.Etiqueta;
        _idEtiquetaAtual = etiqueta.CodigoEtiqueta;
        _situacaoSelecionadaAtiva = etiqueta.SituacaoEtiqueta;
        PreencherCampos(etiqueta);
        PreencherResumo(etiqueta);
        AplicarModoCard(ModoCard.Edicao);
        AtualizarBotoes();
    }

    private void PrepararNovaEtiqueta()
    {
        if (_operacaoEmAndamento || !Pode(PermissoesSistema.Acoes.Criar)) return;
        if (!_modelosCarregados.Any(modelo => modelo.SituacaoModeloEtiqueta))
        {
            VoltarAoModoVazio();
            MessageBox.Show(
                "Cadastre ou reative um Modelo de Etiqueta antes de cadastrar uma Etiqueta.",
                "Cadastro de Etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _idEtiquetaAtual = 0;
        _situacaoSelecionadaAtiva = true;
        LimparSelecaoLinhas();
        LimparCampos();
        LimparResumo();
        ConfigurarComboModelos();
        situacaoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo;
        AplicarModoCard(ModoCard.Novo);
        AtualizarBotoes();
        nomeEtiquetaTextBox.Focus();
    }

    private async Task SalvarEtiquetaAsync()
    {
        if (_modoCard != ModoCard.Novo) return;
        EtiquetaCadastro? etiqueta = CriarEtiquetaDaTela();
        if (etiqueta is null) return;

        ResultadoOperacao resultado = await _etiquetaController.InserirAsync(etiqueta);
        ExibirResultado(resultado);
        if (!resultado.Sucesso) return;

        await CarregarEtiquetasAsync();
        VoltarAoModoVazio();
    }

    private async Task EditarEtiquetaAsync()
    {
        if (_modoCard != ModoCard.Edicao || _idEtiquetaAtual <= 0) return;
        EtiquetaCadastro? etiqueta = CriarEtiquetaDaTela();
        if (etiqueta is null) return;

        etiqueta.CodigoEtiqueta = _idEtiquetaAtual;
        etiqueta.SituacaoEtiqueta = _situacaoSelecionadaAtiva;
        ResultadoOperacao resultado = await _etiquetaController.AtualizarAsync(etiqueta);
        ExibirResultado(resultado);
        if (!resultado.Sucesso) return;

        await CarregarEtiquetasAsync();
        VoltarAoModoVazio();
    }

    private async Task AlternarSituacaoEtiquetaAsync()
    {
        if (_modoCard != ModoCard.Edicao || _idEtiquetaAtual <= 0) return;

        string acao = _situacaoSelecionadaAtiva ? "inativar" : "reativar";
        if (MessageBox.Show(
                $"Deseja realmente {acao} esta etiqueta?",
                "Cadastro de Etiqueta",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        ResultadoOperacao resultado = _situacaoSelecionadaAtiva
            ? await _etiquetaController.ExcluirAsync(_idEtiquetaAtual)
            : await _etiquetaController.ReativarAsync(_idEtiquetaAtual);

        ExibirResultado(resultado);
        if (!resultado.Sucesso) return;

        await CarregarEtiquetasAsync();
        VoltarAoModoVazio();
    }

    private EtiquetaCadastro? CriarEtiquetaDaTela()
    {
        bool situacaoAtiva;
        if (_modoCard == ModoCard.Novo)
        {
            situacaoAtiva = true;
        }
        else if (_modoCard == ModoCard.Edicao)
        {
            situacaoAtiva = _situacaoSelecionadaAtiva;
        }
        else if (!SituacaoCadastroHelper.TryInterpretarSituacao(situacaoComboBox.Text, out situacaoAtiva))
        {
            MessageBox.Show(
                "Selecione uma situação válida para a etiqueta.",
                "Cadastro de Etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return null;
        }

        return new EtiquetaCadastro
        {
            CodigoEtiqueta = _idEtiquetaAtual,
            CodigoModeloEtiqueta = ObterCodigoModeloSelecionado(),
            CodigoInterno = codigoInternoTextBox.Text,
            NomeEtiqueta = nomeEtiquetaTextBox.Text,
            TipoEtiqueta = tipoEtiquetaComboBox.Text,
            DescricaoEtiqueta = descricaoEtiquetaTextBox.Text,
            SituacaoEtiqueta = situacaoAtiva
        };
    }

    private void AbrirCamposEtiqueta()
    {
        if (_idEtiquetaAtual <= 0 || _modoCard != ModoCard.Edicao) return;
        if (!_situacaoSelecionadaAtiva)
        {
            MessageBox.Show(
                "Reative a etiqueta antes de configurar seus campos.",
                "Cadastro de Etiqueta",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!AutorizacaoServico.PodeVisualizarRotina(
                PermissoesSistema.Modulos.Etiqueta,
                PermissoesSistema.Rotinas.CampoEtiqueta))
        {
            MessageBox.Show(
                "Você não possui permissão para acessar os Campos da Etiqueta.",
                "Acesso negado",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        using CamposEtiquetaForm form = new(_idEtiquetaAtual);
        form.ShowDialog(this);
    }

    private void PreencherCampos(EtiquetaCadastro etiqueta)
    {
        nomeEtiquetaTextBox.Text = etiqueta.NomeEtiqueta;
        codigoInternoTextBox.Text = etiqueta.CodigoInterno;
        tipoEtiquetaComboBox.SelectedItem = TiposEtiqueta.FirstOrDefault(tipo =>
            string.Equals(tipo, etiqueta.TipoEtiqueta, StringComparison.OrdinalIgnoreCase));
        ConfigurarComboModelos(etiqueta.CodigoModeloEtiqueta);
        situacaoComboBox.SelectedItem = etiqueta.SituacaoEtiqueta
            ? SituacaoCadastroHelper.Ativo
            : SituacaoCadastroHelper.Inativo;
        descricaoEtiquetaTextBox.Text = etiqueta.DescricaoEtiqueta;
    }

    private void ConfigurarComboModelos(long? codigoModeloSelecionado = null)
    {
        List<ModeloEtiquetaOpcao> opcoes = _modelosCarregados
            .Where(modelo => modelo.SituacaoModeloEtiqueta || modelo.CodigoModeloEtiqueta == codigoModeloSelecionado)
            .OrderBy(modelo => modelo.NomeModeloEtiqueta)
            .ThenBy(modelo => modelo.Versao)
            .Select(modelo => new ModeloEtiquetaOpcao(
                modelo.CodigoModeloEtiqueta,
                $"{modelo.NomeModeloEtiqueta} - v{modelo.Versao}{(modelo.SituacaoModeloEtiqueta ? string.Empty : " (Inativo)")}",
                modelo.SituacaoModeloEtiqueta))
            .ToList();

        modeloEtiquetaComboBox.DataSource = opcoes;
        modeloEtiquetaComboBox.DisplayMember = nameof(ModeloEtiquetaOpcao.Texto);
        modeloEtiquetaComboBox.ValueMember = nameof(ModeloEtiquetaOpcao.Codigo);
        modeloEtiquetaComboBox.SelectedIndex = -1;
        if (codigoModeloSelecionado.HasValue)
            modeloEtiquetaComboBox.SelectedValue = codigoModeloSelecionado.Value;
    }

    private long ObterCodigoModeloSelecionado()
        => modeloEtiquetaComboBox.SelectedValue switch
        {
            long codigo => codigo,
            int codigo => codigo,
            _ => 0
        };

    private void AplicarModoCard(ModoCard modo)
    {
        _modoCard = modo;
        bool exibirCampos = modo is ModoCard.Novo or ModoCard.Edicao;
        bool novo = modo == ModoCard.Novo;
        Control[] controlesCampos =
        [
            nomePerfilLabel,
            nomePerfilInputPanel,
            situacaoLabel,
            situacaoInputPanel,
            label1,
            roundedPanel2,
            LblPeso,
            roundedPanel1,
            LblSetorTara,
            RdpSetor,
            descricaoLabel,
            descricaoInputPanel,
            detailsTopDividerLabel
        ];

        foreach (Control controle in controlesCampos)
            controle.Visible = exibirCampos;

        situacaoLabel.Visible = novo;
        situacaoInputPanel.Visible = novo;
        situacaoComboBox.Enabled = novo;
        label2.Visible = false;
        roundedPanel3.Visible = false;
        _detailsEmptyLabel.Visible = !exibirCampos;
        PosicionarControlesAuxiliares();
        AtualizarBotoes();
    }

    private void AtualizarBotoes()
    {
        bool livre = !_operacaoEmAndamento;
        bool podeCriar = Pode(PermissoesSistema.Acoes.Criar);
        novoPerfilButtonPanel.Enabled = livre && podeCriar;

        salvarButton.Visible = _modoCard == ModoCard.Novo;
        salvarButton.Enabled = livre && salvarButton.Visible && podeCriar;

        editarButton.Visible = _modoCard == ModoCard.Edicao;
        editarButton.Enabled = livre && editarButton.Visible && Pode(PermissoesSistema.Acoes.Editar);

        situacaoButton.Visible = _modoCard == ModoCard.Edicao;
        situacaoButton.Text = _situacaoSelecionadaAtiva
            ? "Inativar Etiqueta        F8"
            : "Reativar Etiqueta        F8";
        situacaoButton.ForeColor = _situacaoSelecionadaAtiva
            ? Color.FromArgb(229, 27, 43)
            : Color.FromArgb(22, 163, 74);
        situacaoButton.FlatAppearance.BorderColor = _situacaoSelecionadaAtiva
            ? Color.FromArgb(203, 213, 225)
            : Color.FromArgb(22, 163, 74);
        situacaoButton.Enabled = livre
            && situacaoButton.Visible
            && (_situacaoSelecionadaAtiva
                ? Pode(PermissoesSistema.Acoes.Excluir)
                : Pode(PermissoesSistema.Acoes.Editar));

        if (camposEtiquetaButton is not null)
        {
            camposEtiquetaButton.Enabled = livre
                && _modoCard == ModoCard.Edicao
                && _idEtiquetaAtual > 0
                && _situacaoSelecionadaAtiva
                && AutorizacaoServico.PodeVisualizarRotina(
                    PermissoesSistema.Modulos.Etiqueta,
                    PermissoesSistema.Rotinas.CampoEtiqueta);
        }

        _etiquetasRowsPanel.Enabled = livre;
        searchTextBox.Enabled = livre;
    }

    private bool Pode(string acao)
        => AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.Etiqueta,
            PermissoesSistema.Rotinas.Etiqueta,
            acao);

    private void VoltarAoModoVazio()
    {
        _idEtiquetaAtual = 0;
        _situacaoSelecionadaAtiva = true;
        etiquetasDataGridView.ClearSelection();
        LimparSelecaoLinhas();
        LimparCampos();
        LimparResumo();
        AplicarModoCard(ModoCard.Vazio);
    }

    private void LimparSelecaoLinhas()
    {
        foreach (LinhaEtiquetaUi linha in _linhasEtiquetas)
        {
            linha.RowPanel.BackColor = Color.White;
            linha.MarkerPanel.Visible = false;
        }
    }

    private void LimparLinhasDinamicas()
    {
        foreach (LinhaEtiquetaUi linha in _linhasEtiquetas)
            linha.RowPanel.Dispose();
        _linhasEtiquetas.Clear();
    }

    private void ExibirEstadoVazioLista(string mensagem)
    {
        _estadoVazioListaLabel.Text = mensagem;
        _estadoVazioListaLabel.Visible = true;
        _estadoVazioListaLabel.BringToFront();
    }

    private void LimparCampos()
    {
        nomeEtiquetaTextBox.Clear();
        codigoInternoTextBox.Clear();
        tipoEtiquetaComboBox.SelectedIndex = -1;
        modeloEtiquetaComboBox.SelectedIndex = -1;
        situacaoComboBox.SelectedIndex = -1;
        descricaoEtiquetaTextBox.Clear();
    }

    private void PreencherResumo(EtiquetaCadastro etiqueta)
    {
        summaryPerfilValueLabel.Text = etiqueta.NomeEtiqueta;
        summarySituacaoValueLabel.Text = etiqueta.SituacaoEtiqueta
            ? SituacaoCadastroHelper.Ativo
            : SituacaoCadastroHelper.Inativo;
        summarySituacaoValueLabel.ForeColor = etiqueta.SituacaoEtiqueta
            ? Color.FromArgb(22, 163, 74)
            : Color.FromArgb(220, 38, 38);
        summaryUsuariosValueLabel.Text = etiqueta.CodigoInterno;
        tipTextLabel.Text = $"Tipo: {etiqueta.TipoEtiqueta}\r\nModelo: {etiqueta.ModeloExibicao}\r\nÚltimo cadastro: {etiqueta.EtiquetaCriadoEm?.ToString("dd/MM/yyyy HH:mm") ?? "-"}";
    }

    private void LimparResumo()
    {
        summaryPerfilValueLabel.Text = "-";
        summarySituacaoValueLabel.Text = "-";
        summarySituacaoValueLabel.ForeColor = Color.FromArgb(100, 116, 139);
        summaryUsuariosValueLabel.Text = "-";
        tipTextLabel.Text = "Tipo: -\r\nModelo: -\r\nÚltimo cadastro: -";
    }

    private void AtualizarRodapeLista(int quantidadeVisivel)
        => profilesFooterLabel.Text = $"Exibindo {quantidadeVisivel} de {_etiquetasCarregadas.Count} etiquetas";

    private void ExibirResultado(ResultadoOperacao resultado)
        => MessageBox.Show(
            resultado.Mensagem,
            "Cadastro de Etiqueta",
            MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

    private void AtualizarDataHora()
    {
        DateTime agora = DateTime.Now;
        cellHoraText.Text = agora.ToString("HH:mm");
        cellDataText.Text = agora.ToString("dd/MM/yyyy");
    }

    private static string NormalizarPesquisa(string? texto)
    {
        string valor = (texto ?? string.Empty).Trim().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(valor.Length);
        foreach (char caractere in valor)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToUpperInvariant(caractere));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F5 when salvarButton.Visible && salvarButton.Enabled:
                salvarButton.PerformClick();
                return true;
            case Keys.F6 when editarButton.Visible && editarButton.Enabled:
                editarButton.PerformClick();
                return true;
            case Keys.F8 when situacaoButton.Visible && situacaoButton.Enabled:
                situacaoButton.PerformClick();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
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

        int side = Scale(16, scaleX);
        int cardWidth = Math.Max(230, profilesCard.Width - side * 2);
        profilesTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        profilesFooterLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11F));
        novoPerfilTextLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        searchTextBox.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 9F, 12F));
        profilesSearchIconLabel.Font = new Font("Segoe MDL2 Assets", Math.Clamp(11F * contentScale, 11F, 14F));
        profilesHeaderProfileLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderUsersLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        profilesHeaderStatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10.5F), FontStyle.Bold);
        _estadoVazioListaLabel.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 8.5F, 12F));

        profilesCheckMarkLabel.Bounds = new Rectangle(Scale(16, scaleX), Scale(19, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        profilesTitleLabel.Bounds = new Rectangle(Scale(43, scaleX), Scale(21, scaleY), Scale(190, scaleX), Scale(24, scaleY));
        int quickW = Math.Max(118, Scale(118, scaleX));
        int quickH = Math.Max(27, Scale(27, scaleY));
        novoPerfilButtonPanel.Bounds = new Rectangle(Math.Max(side, profilesCard.Width - side - quickW), Scale(18, scaleY), quickW, quickH);
        novoPerfilIconLabel.Bounds = new Rectangle(Scale(8, scaleX), Scale(3, scaleY), Math.Max(16, Scale(18, scaleX)), Math.Max(18, Scale(21, scaleY)));
        novoPerfilTextLabel.Bounds = new Rectangle(Scale(29, scaleX), Scale(3, scaleY), Math.Max(72, quickW - Scale(32, scaleX)), Math.Max(18, Scale(21, scaleY)));

        int searchY = Scale(60, scaleY);
        int searchH = Math.Max(34, Scale(34, scaleY));
        profilesSearchPanel.Bounds = new Rectangle(side, searchY, cardWidth, searchH);
        int searchIconWidth = Math.Max(20, Scale(22, scaleX));
        int searchIconHeight = Math.Max(20, Scale(24, scaleY));
        int searchTextHeight = Math.Max(16, searchTextBox.PreferredHeight);
        int searchTextY = Math.Max(2, (profilesSearchPanel.Height - searchTextHeight) / 2);
        int searchIconY = Math.Max(2, searchTextY + ((searchTextHeight - searchIconHeight) / 2));
        profilesSearchIconLabel.Bounds = new Rectangle(Scale(8, scaleX), searchIconY, searchIconWidth, searchIconHeight);
        searchTextBox.Bounds = new Rectangle(Scale(36, scaleX), searchTextY, Math.Max(120, cardWidth - Scale(44, scaleX)), searchTextHeight);

        int tableY = Scale(108, scaleY);
        int footerY = Math.Max(tableY + 180, profilesCard.Height - Scale(28, scaleY));
        int tableH = Math.Max(220, footerY - tableY - Scale(10, scaleY));
        profilesTablePanel.Bounds = new Rectangle(side, tableY, cardWidth, tableH);
        profilesFooterLabel.Bounds = new Rectangle(Scale(20, scaleX), footerY, Math.Max(180, Scale(220, scaleX)), Scale(22, scaleY));

        int typeX = Math.Max(Scale(150, scaleX), cardWidth - Scale(185, scaleX));
        int statusW = Math.Max(50, Scale(58, scaleX));
        int statusX = Math.Max(Scale(250, scaleX), cardWidth - statusW - Scale(13, scaleX));
        profilesHeaderProfileLabel.Bounds = new Rectangle(Scale(16, scaleX), Scale(8, scaleY), Math.Max(120, typeX - Scale(26, scaleX)), Math.Max(24, Scale(24, scaleY)));
        profilesHeaderUsersLabel.Bounds = new Rectangle(typeX, Scale(8, scaleY), Math.Max(60, statusX - typeX - Scale(8, scaleX)), Math.Max(24, Scale(24, scaleY)));
        profilesHeaderStatusLabel.Bounds = new Rectangle(statusX, Scale(8, scaleY), statusW, Math.Max(24, Scale(24, scaleY)));
        AtualizarAreaLinhasDinamicas();
    }

    private void AtualizarAreaLinhasDinamicas()
    {
        if (profilesTablePanel.Width <= 0 || profilesTablePanel.Height <= 0) return;
        int linhasTop = 44;
        _etiquetasRowsPanel.Bounds = new Rectangle(3, linhasTop, Math.Max(1, profilesTablePanel.Width - 6), Math.Max(1, profilesTablePanel.Height - linhasTop - 3));
        _estadoVazioListaLabel.Bounds = new Rectangle(0, 0, _etiquetasRowsPanel.ClientSize.Width, _etiquetasRowsPanel.ClientSize.Height);
        ReposicionarLinhasDinamicas();
    }

    private void ReposicionarLinhasDinamicas()
    {
        float scaleX = profilesCard.Width / 411f;
        float scaleY = profilesCard.Height / 596f;
        float contentScale = Math.Min(scaleX, scaleY);
        int rowLeft = Math.Max(0, (int)Math.Round(3 * scaleX));
        int nameX = Math.Max(0, (int)Math.Round(12 * scaleX));
        int statusW = Math.Max(50, (int)Math.Round(58 * scaleX));
        int rowRightPadding = Math.Max(12, (int)Math.Round(16 * scaleX));
        int rowHeight = Math.Max(40, (int)Math.Round(46 * scaleY));
        int statusInnerRightPadding = Math.Max(8, (int)Math.Round(10 * scaleX));
        int largura = Math.Max(200, profilesTablePanel.ClientSize.Width - rowLeft - rowRightPadding - (_etiquetasRowsPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        int typeX = Math.Max((int)Math.Round(150 * scaleX), largura - (int)Math.Round(185 * scaleX));
        int statusXMin = Math.Max((int)Math.Round(250 * scaleX), nameX + 120);
        int statusX = Math.Max(statusXMin, largura - statusW - statusInnerRightPadding);

        profilesHeaderProfileLabel.Left = rowLeft + nameX;
        profilesHeaderProfileLabel.Width = Math.Max(120, typeX - nameX - Math.Max(0, (int)Math.Round(8 * scaleX)));
        profilesHeaderUsersLabel.Left = rowLeft + typeX;
        profilesHeaderUsersLabel.Width = Math.Max(60, statusX - typeX - Math.Max(0, (int)Math.Round(8 * scaleX)));
        profilesHeaderStatusLabel.Left = rowLeft + statusX;
        profilesHeaderStatusLabel.Width = statusW;

        for (int indice = 0; indice < _linhasEtiquetas.Count; indice++)
        {
            LinhaEtiquetaUi linha = _linhasEtiquetas[indice];
            linha.NameLabel.Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            linha.TypeLabel.Font = new Font("Segoe UI", Math.Clamp(8.25F * contentScale, 8.25F, 11.5F), FontStyle.Bold);
            linha.StatusLabel.Font = new Font("Segoe UI", Math.Clamp(7.5F * contentScale, 7.5F, 10F), FontStyle.Bold);
            linha.RowPanel.Bounds = new Rectangle(0, indice * rowHeight, largura, rowHeight);
            int textoY = Math.Max(0, (int)Math.Round(11 * scaleY));
            int textoH = Math.Max(16, (int)Math.Round(24 * scaleY));
            linha.NameLabel.Bounds = new Rectangle(nameX, textoY, Math.Max(90, typeX - nameX - Math.Max(0, (int)Math.Round(8 * scaleX))), textoH);
            linha.TypeLabel.Bounds = new Rectangle(typeX, textoY, Math.Max(55, statusX - typeX - Math.Max(0, (int)Math.Round(8 * scaleX))), textoH);
            linha.StatusPanel.Bounds = new Rectangle(statusX, textoY, statusW, textoH);
        }
        _estadoVazioListaLabel.BringToFront();
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

        int left = Scale(24, scaleX);
        int inner = Math.Max(260, detailsCard.Width - left - Scale(24, scaleX));
        int gap = Scale(30, scaleX);
        int fieldWidth = Math.Max(170, Math.Min(Scale(300, scaleX), (inner - gap) / 2));
        int layoutWidth = fieldWidth * 2 + gap;
        int col1 = left + Math.Max(0, (inner - layoutWidth) / 2);
        int col2 = col1 + fieldWidth + gap;

        ApplyScaledFont(detailsTitleLabel, 8.5F, contentScale, 9F, 12.5F);
        ApplyScaledFont(detailsTitleIconLabel, 14F, contentScale, 14F, 18F);
        ApplyScaledFont(nomePerfilLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(situacaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(label1, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblPeso, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(LblSetorTara, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(descricaoLabel, 7.75F, contentScale, 8.5F, 11F);
        ApplyScaledFont(nomeEtiquetaTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(situacaoComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(codigoInternoTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(tipoEtiquetaComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(modeloEtiquetaComboBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(descricaoEtiquetaTextBox, 9F, contentScale, 9F, 12F);
        ApplyScaledFont(_detailsEmptyLabel, 10F, contentScale, 9.5F, 13F);
        if (camposEtiquetaButton is not null)
            camposEtiquetaButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);

        situacaoComboBox.IntegralHeight = false;
        situacaoComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        tipoEtiquetaComboBox.IntegralHeight = false;
        tipoEtiquetaComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));
        modeloEtiquetaComboBox.IntegralHeight = false;
        modeloEtiquetaComboBox.DropDownHeight = Math.Max(96, Scale(120, scaleY));

        detailsTitleIconLabel.Bounds = new Rectangle(Scale(20, scaleX), Scale(18, scaleY), Scale(26, scaleX), Scale(26, scaleY));
        detailsTitleLabel.Bounds = new Rectangle(Scale(52, scaleX), Scale(22, scaleY), Math.Max(190, Scale(220, scaleX)), Scale(24, scaleY));
        nomePerfilLabel.Bounds = new Rectangle(col1, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        nomePerfilInputPanel.Bounds = new Rectangle(col1, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        int campoTextoWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int campoTextoHeight = Math.Max(16, nomeEtiquetaTextBox.PreferredHeight);
        int campoTextoY = Math.Max(2, (nomePerfilInputPanel.Height - campoTextoHeight) / 2);
        nomeEtiquetaTextBox.Bounds = new Rectangle(Scale(12, scaleX), campoTextoY, campoTextoWidth, campoTextoHeight);
        situacaoLabel.Bounds = new Rectangle(col2, Scale(56, scaleY), fieldWidth, Scale(16, scaleY));
        situacaoInputPanel.Bounds = new Rectangle(col2, Scale(73, scaleY), fieldWidth, Scale(33, scaleY));
        int situacaoComboWidth = Math.Max(80, fieldWidth - Scale(24, scaleX));
        int situacaoComboHeight = Math.Max(22, situacaoComboBox.PreferredHeight);
        int situacaoComboY = Math.Max(2, (situacaoInputPanel.Height - situacaoComboHeight) / 2);
        situacaoComboBox.Bounds = new Rectangle(Scale(12, scaleX), situacaoComboY, situacaoComboWidth, situacaoComboHeight);

        label1.Bounds = new Rectangle(col1, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        roundedPanel2.Bounds = new Rectangle(col1, Scale(140, scaleY), fieldWidth, Scale(33, scaleY));
        int codigoHeight = Math.Max(16, codigoInternoTextBox.PreferredHeight);
        int codigoY = Math.Max(2, (roundedPanel2.Height - codigoHeight) / 2);
        codigoInternoTextBox.Bounds = new Rectangle(Scale(12, scaleX), codigoY, campoTextoWidth, codigoHeight);
        LblPeso.Bounds = new Rectangle(col2, Scale(117, scaleY), fieldWidth, Scale(19, scaleY));
        roundedPanel1.Bounds = new Rectangle(col2, Scale(140, scaleY), fieldWidth, Scale(33, scaleY));
        int tipoComboHeight = Math.Max(22, tipoEtiquetaComboBox.PreferredHeight);
        int tipoComboY = Math.Max(2, (roundedPanel1.Height - tipoComboHeight) / 2);
        tipoEtiquetaComboBox.Bounds = new Rectangle(Scale(12, scaleX), tipoComboY, campoTextoWidth, tipoComboHeight);

        LblSetorTara.Bounds = new Rectangle(col2, Scale(182, scaleY), fieldWidth, Scale(19, scaleY));
        RdpSetor.Bounds = new Rectangle(col2, Scale(204, scaleY), fieldWidth, Scale(33, scaleY));
        int modeloComboHeight = Math.Max(22, modeloEtiquetaComboBox.PreferredHeight);
        int modeloComboY = Math.Max(2, (RdpSetor.Height - modeloComboHeight) / 2);
        modeloEtiquetaComboBox.Bounds = new Rectangle(Scale(12, scaleX), modeloComboY, campoTextoWidth, modeloComboHeight);
        descricaoLabel.Bounds = new Rectangle(col1, Scale(246, scaleY), layoutWidth, Scale(19, scaleY));
        descricaoInputPanel.Bounds = new Rectangle(col1, Scale(269, scaleY), layoutWidth, Math.Max(95, Scale(108, scaleY)));
        descricaoEtiquetaTextBox.Bounds = new Rectangle(Scale(12, scaleX), Scale(10, scaleY), Math.Max(140, layoutWidth - Scale(24, scaleX)), Math.Max(60, descricaoInputPanel.Height - Scale(16, scaleY)));
        detailsTopDividerLabel.Bounds = new Rectangle(col1, descricaoInputPanel.Bottom + Scale(12, scaleY), layoutWidth, 1);
        PosicionarControlesAuxiliares();
    }

    private void PosicionarControlesAuxiliares()
    {
        if (camposEtiquetaButton is not null)
        {
            int largura = Math.Max(170, Math.Min(210, detailsCard.Width / 3));
            int altura = Math.Max(28, (int)Math.Round(28 * Math.Min(detailsCard.Width / 557f, detailsCard.Height / 596f)));
            camposEtiquetaButton.Bounds = new Rectangle(Math.Max(250, detailsCard.Width - largura - 24), 18, largura, altura);
        }
        _detailsEmptyLabel.Bounds = new Rectangle(40, 70, Math.Max(100, detailsCard.Width - 80), Math.Max(100, detailsCard.Height - 150));
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

        int textLeft = Scale(74, scaleX);
        int labelWidth = Math.Max(140, summaryCard.Width - Scale(98, scaleX));
        int dividerWidth = Math.Max(120, summaryCard.Width - Scale(52, scaleX));
        int buttonX = Scale(24, scaleX);
        int buttonWidth = Math.Max(200, summaryCard.Width - Scale(48, scaleX));
        int buttonHeight = Math.Max(28, Scale(28, scaleY));
        int iconSize = Math.Max(32, Scale(32, scaleX));
        int titleHeight = Math.Max(24, Scale(24, scaleY));
        int tipY = Scale(314, scaleY);
        int salvarY = Scale(402, scaleY);
        int tipHeight = Math.Min(
            Math.Max(56, Scale(66, scaleY)),
            Math.Max(32, salvarY - tipY - Scale(16, scaleY)));

        summaryTitleLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 9F, 12.5F), FontStyle.Bold);
        summaryPerfilCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summaryPerfilValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        summarySituacaoCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summarySituacaoValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        summaryUsuariosCaptionLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 11.5F));
        summaryUsuariosValueLabel.Font = new Font("Segoe UI", Math.Clamp(8.5F * contentScale, 8.5F, 12F), FontStyle.Bold);
        tipTextLabel.Font = new Font("Segoe UI", Math.Clamp(9F * contentScale, 8.5F, 12F));
        salvarButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        editarButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);
        situacaoButton.Font = new Font("Segoe UI", Math.Clamp(8F * contentScale, 8F, 11F), FontStyle.Bold);

        summaryTitleIconLabel.Bounds = new Rectangle(Scale(20, scaleX), Scale(18, scaleY), iconSize, iconSize);
        summaryTitleLabel.Bounds = new Rectangle(Scale(52, scaleX), Scale(20, scaleY), Scale(200, scaleX), titleHeight);
        summaryDividerLabel.Bounds = new Rectangle(Scale(26, scaleX), Scale(56, scaleY), dividerWidth, 1);
        summaryDividerLabel2.Bounds = new Rectangle(Scale(26, scaleX), Scale(124, scaleY), dividerWidth, 1);
        summaryDividerLabel3.Bounds = new Rectangle(Scale(26, scaleX), Scale(178, scaleY), dividerWidth, 1);
        summaryDividerLabel4.Bounds = new Rectangle(Scale(26, scaleX), Scale(232, scaleY), dividerWidth, 1);
        summaryDividerLabel5.Bounds = new Rectangle(Scale(26, scaleX), Scale(286, scaleY), dividerWidth, 1);
        summaryPerfilIconLabel.Bounds = new Rectangle(Scale(32, scaleX), Scale(78, scaleY), iconSize, iconSize);
        summaryPerfilCaptionLabel.Bounds = new Rectangle(textLeft, Scale(78, scaleY), labelWidth, Scale(18, scaleY));
        summaryPerfilValueLabel.Bounds = new Rectangle(textLeft, Scale(98, scaleY), labelWidth, Scale(24, scaleY));
        summarySituacaoIconLabel.Bounds = new Rectangle(Scale(32, scaleX), Scale(132, scaleY), iconSize, iconSize);
        summarySituacaoCaptionLabel.Bounds = new Rectangle(textLeft, Scale(132, scaleY), labelWidth, Scale(18, scaleY));
        summarySituacaoValueLabel.Bounds = new Rectangle(textLeft, Scale(152, scaleY), labelWidth, Scale(24, scaleY));
        summaryUsuariosIconLabel.Bounds = new Rectangle(Scale(32, scaleX), Scale(186, scaleY), iconSize, iconSize);
        summaryUsuariosCaptionLabel.Bounds = new Rectangle(textLeft, Scale(186, scaleY), labelWidth, Scale(18, scaleY));
        summaryUsuariosValueLabel.Bounds = new Rectangle(textLeft, Scale(206, scaleY), labelWidth, Scale(24, scaleY));
        tipTextLabel.Bounds = new Rectangle(textLeft, tipY, Math.Max(180, summaryCard.Width - Scale(98, scaleX)), tipHeight);
        salvarButton.Bounds = new Rectangle(buttonX, salvarY, buttonWidth, buttonHeight);
        editarButton.Bounds = new Rectangle(buttonX, Scale(434, scaleY), buttonWidth, buttonHeight);
        situacaoButton.Bounds = new Rectangle(buttonX, Scale(466, scaleY), buttonWidth, buttonHeight);
    }

    private static void ApplyScaledFont(Control control, float baseSize, float scale, float minSize, float maxSize)
    {
        float size = Math.Clamp(baseSize * scale, minSize, maxSize);
        if (Math.Abs(control.Font.Size - size) < 0.01F) return;
        control.Font = new Font(control.Font.FontFamily, size, control.Font.Style);
    }

    private void ArrastarJanela(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
