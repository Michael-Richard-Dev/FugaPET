using System.Globalization;
using System.Runtime.InteropServices;
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

public sealed partial class CamposEtiquetaForm : Form
{
    private enum ModoCampo { Vazio, Novo, Edicao }
    private enum EstadoMapeamento { SemCampo, CampoInativo, CampoAtivoSemMapeamento, CampoAtivoComMapeamento }

    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private static readonly string[] TiposDado = ["TEXTO", "NUMERO", "DATA", "PESO", "QRCODE", "CODIGO_BARRAS", "BOOLEANO"];
    private static readonly string[] OrigensDado = ["SISTEMA", "SAP", "USUARIO", "CALCULADO", "BALANCA", "FIXO"];
    private static readonly Color LinhaSelecionada = Color.FromArgb(255, 241, 242);
    private static readonly Color LinhaHover = Color.FromArgb(248, 250, 252);
    private static readonly Color VermelhoFuga = Color.FromArgb(239, 68, 68);
    private static readonly Color VerdeTexto = Color.FromArgb(22, 163, 74);
    private static readonly Color VerdeFundo = Color.FromArgb(220, 252, 231);
    private static readonly Color LaranjaTexto = Color.FromArgb(234, 88, 12);
    private static readonly Color LaranjaFundo = Color.FromArgb(255, 237, 213);

    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private readonly long _etiquetaPreSelecionada;
    private readonly EtiquetaController _etiquetaController;
    private readonly CampoEtiquetaController _campoController;
    private readonly MapeamentoCampoEtiquetaController _mapeamentoController;
    private readonly AuditoriaServico _auditoriaServico;
    private readonly Dictionary<Panel, CampoEtiquetaCadastro> _campoPorLinha = new();
    private readonly Dictionary<Panel, Panel> _marcadorPorLinha = new();
    private readonly Dictionary<Panel, RoundedPanel> _badgePorLinha = new();

    private IReadOnlyList<CampoEtiquetaCadastro> _campos = [];
    private List<CampoEtiquetaCadastro> _camposFiltrados = [];
    private CancellationTokenSource? _carregarCamposCts;
    private CancellationTokenSource? _selecaoCampoCts;
    private Task _carregarCamposTask = Task.CompletedTask;
    private Task _selecaoCampoTask = Task.CompletedTask;
    private ModoCampo _modoCampo = ModoCampo.Vazio;
    private EstadoMapeamento _estadoMapeamento = EstadoMapeamento.SemCampo;
    private bool _operacaoEmAndamento;
    private bool _atualizandoLista;
    private bool _campoAtualAtivo;
    private bool _podeConsultarCampo;
    private bool _podeCriarCampo;
    private bool _podeEditarCampo;
    private bool _podeExcluirCampo;
    private bool _podeConsultarMapeamento;
    private bool _podeCriarMapeamento;
    private bool _podeEditarMapeamento;
    private bool _podeExcluirMapeamento;
    private long _idCampoAtual;
    private long _idMapeamentoAtual;

    public CamposEtiquetaForm(long etiquetaPreSelecionada = 0, EtiquetaController? etiquetaController = null, CampoEtiquetaController? campoController = null, MapeamentoCampoEtiquetaController? mapeamentoController = null, AuditoriaServico? auditoriaServico = null)
    {
        _etiquetaPreSelecionada = etiquetaPreSelecionada;
        _etiquetaController = etiquetaController ?? FabricaControladoresCadastro.CriarEtiquetaController();
        _campoController = campoController ?? FabricaControladoresCadastro.CriarCampoEtiquetaController();
        _mapeamentoController = mapeamentoController ?? FabricaControladoresCadastro.CriarMapeamentoCampoEtiquetaController();
        _auditoriaServico = auditoriaServico ?? FabricaControladoresCadastro.CriarAuditoriaServico();

        InitializeComponent();
        IconeJanelaHelper.AplicarIconePadrao(this);
        ConfigurarJanelaFugaPet();
        ConfigurarControles();
        KeyPreview = true;
        KeyDown += CamposEtiquetaForm_KeyDown;
        FormClosed += (_, _) => CancelarConsultasPendentes();
        Shown += async (_, _) => await InicializarAsync();
    }

    private void ConfigurarJanelaFugaPet()
    {
        cellUserText.Text = UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        cellBancoText.Text = RodapeBancoHelper.ObterTextoBancoDados();
        AtualizarRelogioRodape();
        clockTimer.Tick += (_, _) => AtualizarRelogioRodape();
        clockTimer.Start();
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        closeWindowLabel.Click += (_, _) => Close();
        headerBar.MouseDown += (_, e) => ArrastarJanela(e);
        headerTitleLabel.MouseDown += (_, e) => ArrastarJanela(e);
        headerSubtitleLabel.MouseDown += (_, e) => ArrastarJanela(e);
    }

    private void AtualizarRelogioRodape()
    {
        cellHoraText.Text = DateTime.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        cellDataText.Text = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture);
    }

    private void ConfigurarControles()
    {
        tipoDadoComboBox.Items.AddRange(TiposDado.Cast<object>().ToArray());
        origemDadoComboBox.Items.AddRange(OrigensDado.Cast<object>().ToArray());
        situacaoCampoComboBox.Items.AddRange(new object[] { SituacaoCadastroHelper.Ativo, SituacaoCadastroHelper.Inativo });
        if (tipoDadoComboBox.Items.Count > 0) tipoDadoComboBox.SelectedIndex = 0;
        if (origemDadoComboBox.Items.Count > 0) origemDadoComboBox.SelectedIndex = 0;
        situacaoCampoComboBox.SelectedIndex = 0;
        situacaoCampoComboBox.Enabled = false;
        nomeCampoTextBox.MaxLength = CampoEtiquetaCadastro.TamanhoMaximoNome;
        descricaoTextBox.MaxLength = CampoEtiquetaCadastro.TamanhoMaximoDescricao;
        formatoSaidaTextBox.MaxLength = CampoEtiquetaCadastro.TamanhoMaximoFormatoSaida;
        observacaoMapeamentoTextBox.MaxLength = MapeamentoCampoEtiquetaCadastro.TamanhoMaximoObservacao;
        searchTextBox.MaxLength = 120;
        ordemTextBox.MaxLength = 6;
        tamanhoMaximoTextBox.MaxLength = 8;
        novoCampoButton.Click += (_, _) => PrepararNovoCampo();
        salvarCampoButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(SalvarCampoAsync);
        editarCampoButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(SalvarCampoAsync);
        situacaoCampoButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(AlternarSituacaoCampoAsync);
        salvarMapeamentoButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(SalvarMapeamentoAsync);
        inativarMapeamentoButton.Click += async (_, _) => await ExecutarOperacaoProtegidaAsync(InativarMapeamentoAsync);
        searchTextBox.TextChanged += (_, _) => AplicarFiltroCampos();
        camposCard.Resize += (_, _) => LayoutCamposCard();
        dadosCampoCard.Resize += (_, _) => LayoutDadosCampoCard();
        mapeamentoCard.Resize += (_, _) => LayoutMapeamentoCard();
        LayoutCamposCard(); LayoutDadosCampoCard(); LayoutMapeamentoCard();
    }

    private async Task InicializarAsync()
    {
        if (!_integracaoBancoHabilitada)
        {
            MessageBox.Show("Integração com banco desabilitada no momento.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information);
            BeginInvoke(Close); return;
        }
        if (_etiquetaPreSelecionada <= 0)
        {
            MessageBox.Show("Abra Campos da Etiqueta a partir de uma Etiqueta ativa selecionada.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            BeginInvoke(Close); return;
        }
        if (!await ValidarAcessoDiretoAsync()) { BeginInvoke(Close); return; }
        CarregarPermissoesAcoes();
        await ExecutarOperacaoProtegidaAsync(async () =>
        {
            if (!await CarregarEtiquetaPreSelecionadaAsync()) { BeginInvoke(Close); return; }
            await CarregarCamposAsync();
            AplicarModoCampo(ModoCampo.Vazio);
        });
    }

    private async Task<bool> ValidarAcessoDiretoAsync()
    {
        _podeConsultarCampo = AutorizacaoServico.PodeVisualizarRotina(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.CampoEtiqueta);
        if (_podeConsultarCampo) return true;
        string mensagem = AutorizacaoServico.MensagemSemPermissao(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.CampoEtiqueta, PermissoesSistema.Acoes.Consultar);
        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (codigoUsuario.HasValue) await _auditoriaServico.RegistrarAcessoNegadoAsync(codigoUsuario.Value, mensagem, "CamposEtiquetaForm");
        MessageBox.Show(mensagem, "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private void CarregarPermissoesAcoes()
    {
        _podeCriarCampo = PossuiPermissaoCampo(PermissoesSistema.Acoes.Criar);
        _podeEditarCampo = PossuiPermissaoCampo(PermissoesSistema.Acoes.Editar);
        _podeExcluirCampo = PossuiPermissaoCampo(PermissoesSistema.Acoes.Excluir);
        _podeConsultarMapeamento = AutorizacaoServico.PodeVisualizarRotina(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.MapeamentoCampoEtiqueta);
        _podeCriarMapeamento = PossuiPermissaoMapeamento(PermissoesSistema.Acoes.Criar);
        _podeEditarMapeamento = PossuiPermissaoMapeamento(PermissoesSistema.Acoes.Editar);
        _podeExcluirMapeamento = PossuiPermissaoMapeamento(PermissoesSistema.Acoes.Excluir);
    }

    private static bool PossuiPermissaoCampo(string acao) => AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.CampoEtiqueta, acao);
    private static bool PossuiPermissaoMapeamento(string acao) => AutorizacaoServico.PossuiPermissao(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.MapeamentoCampoEtiqueta, acao);

    private async Task<bool> CarregarEtiquetaPreSelecionadaAsync()
    {
        IReadOnlyList<EtiquetaCadastro> etiquetas = await _etiquetaController.ListarAsync();
        EtiquetaCadastro? etiqueta = etiquetas.FirstOrDefault(e => e.CodigoEtiqueta == _etiquetaPreSelecionada);
        if (etiqueta is null || !etiqueta.SituacaoEtiqueta || !etiquetas.Any(e => e.CodigoEtiqueta == _etiquetaPreSelecionada && e.SituacaoEtiqueta))
        {
            MessageBox.Show("A etiqueta selecionada não está mais ativa. Recarregue o Cadastro de Etiqueta.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        etiquetaTituloLabel.Text = etiqueta.NomeEtiqueta;
        etiquetaCodigoLabel.Text = $"Código interno: {etiqueta.CodigoInterno}";
        etiquetaResumoLabel.Text = etiqueta.NomeEtiqueta;
        return true;
    }

    private async Task CarregarCamposAsync()
    {
        CancellationTokenSource atual = new();
        CancellationTokenSource? anterior = Interlocked.Exchange(ref _carregarCamposCts, atual);
        anterior?.Cancel(); anterior?.Dispose(); CancelarSelecaoCampo();
        try
        {
            IReadOnlyList<CampoEtiquetaCadastro> campos = await _campoController.ListarPorEtiquetaAsync(_etiquetaPreSelecionada, atual.Token);
            if (!EtiquetaSolicitadaAindaEhAtual(atual)) return;
            _campos = campos; AplicarFiltroCampos();
        }
        catch (OperationCanceledException) when (atual.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!EtiquetaSolicitadaAindaEhAtual(atual)) return;
            _campos = []; AplicarFiltroCampos(); AplicarModoCampo(ModoCampo.Vazio); LimparEditorMapeamento();
            MessageBox.Show(await ErroUsuarioHelper.TratarAsync("CAMPOS_ETIQUETA_ERRO", ex, "CamposEtiquetaForm", "Não foi possível carregar os campos. Acione o suporte."), "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AplicarFiltroCampos()
    {
        long idSelecionado = _idCampoAtual;
        string termo = (searchTextBox.Text ?? string.Empty).Trim();
        IEnumerable<CampoEtiquetaCadastro> query = _campos;
        if (!string.IsNullOrWhiteSpace(termo))
            query = query.Where(c => c.NomeCampo.Contains(termo, StringComparison.OrdinalIgnoreCase) || c.TipoDado.Contains(termo, StringComparison.OrdinalIgnoreCase) || c.Ordem.ToString(CultureInfo.InvariantCulture).Contains(termo, StringComparison.OrdinalIgnoreCase));
        _camposFiltrados = query.OrderBy(c => c.Ordem).ThenBy(c => c.NomeCampo).ToList();
        _atualizandoLista = true;
        try
        {
            RenderizarLinhasCampos();
            Panel? linha = _campoPorLinha.FirstOrDefault(x => x.Value.CodigoCampoEtiqueta == idSelecionado).Key;
            if (linha is not null) MarcarLinhaSelecionada(linha);
            else if (idSelecionado > 0) { LimparSelecaoVisualCampos(); AplicarModoCampo(ModoCampo.Vazio); LimparEditorMapeamento(); }
        }
        finally { _atualizandoLista = false; }
        listFooterLabel.Text = $"Exibindo {_camposFiltrados.Count} de {_campos.Count} campos";
    }

    private void RenderizarLinhasCampos()
    {
        camposRowsPanel.SuspendLayout();
        try
        {
            camposRowsPanel.Controls.Clear(); _campoPorLinha.Clear(); _marcadorPorLinha.Clear(); _badgePorLinha.Clear();
            foreach (CampoEtiquetaCadastro campo in _camposFiltrados)
            {
                Panel row = CriarLinhaCampo(campo); camposRowsPanel.Controls.Add(row); _campoPorLinha[row] = campo;
            }
        }
        finally { camposRowsPanel.ResumeLayout(); }
        LayoutCamposCard();
    }

    private Panel CriarLinhaCampo(CampoEtiquetaCadastro campo)
    {
        Panel row = new() { Height = 54, Width = Math.Max(320, camposRowsPanel.ClientSize.Width - 24), Margin = new Padding(0), BackColor = Color.White, Cursor = Cursors.Hand, Tag = campo };
        Panel marker = new() { Width = 3, BackColor = VermelhoFuga, Visible = false };
        Label ordemLabel = CriarLinhaLabel(campo.Ordem.ToString(CultureInfo.InvariantCulture));
        Label nomeLabel = CriarLinhaLabel(campo.NomeCampo);
        Label tipoLabel = CriarLinhaLabel(campo.TipoDado);
        RoundedPanel badge = CriarStatusBadge(campo.SituacaoCampoEtiqueta ? SituacaoCadastroHelper.Ativo : SituacaoCadastroHelper.Inativo);
        row.Controls.Add(marker); row.Controls.Add(ordemLabel); row.Controls.Add(nomeLabel); row.Controls.Add(tipoLabel); row.Controls.Add(badge);
        _marcadorPorLinha[row] = marker; _badgePorLinha[row] = badge;
        ConectarSelecaoLinha(row, row); LayoutLinhaCampo(row); return row;
    }

    private static Label CriarLinhaLabel(string texto) => new() { Text = texto, AutoEllipsis = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };

    private static RoundedPanel CriarStatusBadge(string texto)
    {
        bool ativo = texto.Equals(SituacaoCadastroHelper.Ativo, StringComparison.OrdinalIgnoreCase);
        RoundedPanel panel = new() { BorderRadius = 12, BorderThickness = 0, FillColor = ativo ? VerdeFundo : LaranjaFundo };
        panel.Controls.Add(new Label { Text = texto, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = ativo ? VerdeTexto : LaranjaTexto, BackColor = Color.Transparent });
        return panel;
    }

    private void ConectarSelecaoLinha(Control control, Panel row)
    {
        control.Click += async (_, _) => await SelecionarLinhaCampoAsync(row);
        control.MouseEnter += (_, _) => { if (_marcadorPorLinha.TryGetValue(row, out Panel? marker) && !marker.Visible) row.BackColor = LinhaHover; };
        control.MouseLeave += (_, _) => { if (_marcadorPorLinha.TryGetValue(row, out Panel? marker) && !marker.Visible) row.BackColor = Color.White; };
        foreach (Control filho in control.Controls) ConectarSelecaoLinha(filho, row);
    }

    private async Task SelecionarLinhaCampoAsync(Panel row)
    {
        if (_atualizandoLista || _operacaoEmAndamento) return;
        if (!_campoPorLinha.TryGetValue(row, out CampoEtiquetaCadastro? campo)) return;
        MarcarLinhaSelecionada(row);
        _selecaoCampoTask = AoSelecionarCampoAsync(campo.CodigoCampoEtiqueta);
        await _selecaoCampoTask;
    }

    private void MarcarLinhaSelecionada(Panel row)
    {
        foreach (Panel linha in _campoPorLinha.Keys)
        {
            bool selecionada = linha == row;
            linha.BackColor = selecionada ? LinhaSelecionada : Color.White;
            if (_marcadorPorLinha.TryGetValue(linha, out Panel? marker)) marker.Visible = selecionada;
        }
    }

    private void LimparSelecaoVisualCampos()
    {
        foreach (Panel linha in _campoPorLinha.Keys)
        {
            linha.BackColor = Color.White;
            if (_marcadorPorLinha.TryGetValue(linha, out Panel? marker)) marker.Visible = false;
        }
    }

    private async Task AoSelecionarCampoAsync(long idSolicitado)
    {
        if (idSolicitado <= 0) return;
        CancellationTokenSource atual = new();
        CancellationTokenSource? anterior = Interlocked.Exchange(ref _selecaoCampoCts, atual);
        anterior?.Cancel(); anterior?.Dispose();
        _idCampoAtual = idSolicitado; LimparEditorMapeamento();
        try
        {
            CampoEtiquetaEdicaoAgregado? agregado = await _campoController.ObterEdicaoAgregadaAsync(idSolicitado, atual.Token);
            if (!CampoSolicitadoAindaEhAtual(idSolicitado, atual) || agregado is null) return;
            PreencherEditorCampo(agregado.Campo); PreencherEditorMapeamento(agregado.Campo, agregado.MapeamentoAtivo); AplicarModoCampo(ModoCampo.Edicao);
        }
        catch (OperationCanceledException) when (atual.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!CampoSolicitadoAindaEhAtual(idSolicitado, atual)) return;
            AplicarModoCampo(ModoCampo.Vazio); LimparEditorMapeamento();
            MessageBox.Show(await ErroUsuarioHelper.TratarAsync("MAPEAMENTO_CAMPO_ERRO", ex, "CamposEtiquetaForm", "Não foi possível carregar o campo e seu mapeamento. Acione o suporte."), "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PrepararNovoCampo()
    {
        if (!_podeCriarCampo || _operacaoEmAndamento) return;
        CancelarSelecaoCampo(); LimparSelecaoVisualCampos();
        _idCampoAtual = 0; _campoAtualAtivo = true;
        nomeCampoTextBox.Text = string.Empty; tipoDadoComboBox.SelectedIndex = 0; ordemTextBox.Text = ProximaOrdemSugerida().ToString(CultureInfo.InvariantCulture);
        obrigatorioCheckBox.Checked = false; tamanhoMaximoTextBox.Text = string.Empty; formatoSaidaTextBox.Text = string.Empty; descricaoTextBox.Text = string.Empty;
        situacaoCampoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo;
        LimparEditorMapeamento(); AplicarModoCampo(ModoCampo.Novo); nomeCampoTextBox.Focus();
    }

    private void PreencherEditorCampo(CampoEtiquetaCadastro campo)
    {
        _idCampoAtual = campo.CodigoCampoEtiqueta; _campoAtualAtivo = campo.SituacaoCampoEtiqueta;
        nomeCampoTextBox.Text = campo.NomeCampo; SelecionarCombo(tipoDadoComboBox, campo.TipoDado); ordemTextBox.Text = campo.Ordem.ToString(CultureInfo.InvariantCulture);
        obrigatorioCheckBox.Checked = campo.Obrigatorio; tamanhoMaximoTextBox.Text = campo.TamanhoMaximo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        formatoSaidaTextBox.Text = campo.FormatoSaida; descricaoTextBox.Text = campo.DescricaoCampoEtiqueta;
        situacaoCampoComboBox.SelectedItem = campo.SituacaoCampoEtiqueta ? SituacaoCadastroHelper.Ativo : SituacaoCadastroHelper.Inativo;
    }

    private void PreencherEditorMapeamento(CampoEtiquetaCadastro campo, MapeamentoCampoEtiquetaCadastro? mapa)
    {
        LimparEditorMapeamento();
        mappingCampoLabel.Text = $"Campo selecionado: {campo.NomeCampo}";
        mappingSituacaoCampoLabel.Text = $"Situação do Campo: {(campo.SituacaoCampoEtiqueta ? SituacaoCadastroHelper.Ativo : SituacaoCadastroHelper.Inativo)}";
        if (!_podeConsultarMapeamento) { mappingInfoLabel.Text = "Usuário sem permissão para consultar o mapeamento deste campo."; AplicarEstadoMapeamento(EstadoMapeamento.SemCampo); return; }
        if (!campo.SituacaoCampoEtiqueta)
        {
            mappingInfoLabel.Text = "Reative o campo antes de alterar seu mapeamento.";
            mappingSituacaoMapeamentoLabel.Text = mapa is null ? "Situação do Mapeamento: -" : "Situação do Mapeamento: Ativo";
            if (mapa is not null) PreencherDadosMapeamento(mapa);
            AplicarEstadoMapeamento(EstadoMapeamento.CampoInativo); return;
        }
        if (mapa is null)
        {
            mappingInfoLabel.Text = "Campo sem mapeamento ativo.";
            mappingSituacaoMapeamentoLabel.Text = "Situação do Mapeamento: Sem mapeamento ativo";
            AplicarEstadoMapeamento(EstadoMapeamento.CampoAtivoSemMapeamento); return;
        }
        PreencherDadosMapeamento(mapa);
        mappingInfoLabel.Text = $"Mapeamento ativo (cód. {mapa.CodigoMapeamentoCampoEtiqueta}).";
        mappingSituacaoMapeamentoLabel.Text = mapa.SituacaoMapeamentoCampoEtiqueta ? "Situação do Mapeamento: Ativo" : "Situação do Mapeamento: Inativo";
        AplicarEstadoMapeamento(EstadoMapeamento.CampoAtivoComMapeamento);
    }

    private void PreencherDadosMapeamento(MapeamentoCampoEtiquetaCadastro mapa)
    {
        _idMapeamentoAtual = mapa.CodigoMapeamentoCampoEtiqueta;
        SelecionarCombo(origemDadoComboBox, mapa.OrigemDado);
        expressaoOrigemTextBox.Text = mapa.ExpressaoOrigem;
        valorPadraoTextBox.Text = mapa.ValorPadrao;
        obrigatorioImpressaoCheckBox.Checked = mapa.ObrigatorioParaImpressao;
        observacaoMapeamentoTextBox.Text = mapa.Observacao;
    }

    private async Task SalvarCampoAsync()
    {
        if (!GarantirBanco()) return;
        if (!ValidarFormularioCampo(out CampoEtiquetaCadastro campo, out string mensagem))
        {
            MessageBox.Show(mensagem, "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
        }
        ResultadoOperacao resultado = _modoCampo == ModoCampo.Novo ? await _campoController.InserirAsync(campo) : await _campoController.AtualizarAsync(campo);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (resultado.Sucesso) { await CarregarCamposAsync(); AplicarModoCampo(ModoCampo.Vazio); LimparEditorMapeamento(); }
    }

    private bool ValidarFormularioCampo(out CampoEtiquetaCadastro campo, out string mensagem)
    {
        campo = new CampoEtiquetaCadastro(); mensagem = string.Empty;
        if (string.IsNullOrWhiteSpace(nomeCampoTextBox.Text)) { mensagem = "Informe o nome do campo."; return false; }
        if (!int.TryParse(ordemTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ordem) || ordem <= 0) { mensagem = "Informe uma ordem válida maior que zero."; return false; }
        int? tamanhoMaximo = null;
        if (!string.IsNullOrWhiteSpace(tamanhoMaximoTextBox.Text))
        {
            if (!int.TryParse(tamanhoMaximoTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tamanho) || tamanho <= 0)
            { mensagem = "Informe um tamanho máximo válido maior que zero ou deixe o campo vazio."; return false; }
            tamanhoMaximo = tamanho;
        }
        campo = new CampoEtiquetaCadastro { CodigoCampoEtiqueta = _idCampoAtual, CodigoEtiqueta = _etiquetaPreSelecionada, NomeCampo = nomeCampoTextBox.Text.Trim(), TipoDado = tipoDadoComboBox.Text, Ordem = ordem, Obrigatorio = obrigatorioCheckBox.Checked, TamanhoMaximo = tamanhoMaximo, FormatoSaida = formatoSaidaTextBox.Text.Trim(), DescricaoCampoEtiqueta = descricaoTextBox.Text.Trim(), SituacaoCampoEtiqueta = _modoCampo == ModoCampo.Novo || _campoAtualAtivo };
        return true;
    }

    private async Task AlternarSituacaoCampoAsync()
    {
        if (!GarantirBanco() || _idCampoAtual <= 0) return;
        ResultadoOperacao resultado = _campoAtualAtivo ? await _campoController.ExcluirAsync(_idCampoAtual) : await _campoController.ReativarAsync(_idCampoAtual);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (resultado.Sucesso) { await CarregarCamposAsync(); AplicarModoCampo(ModoCampo.Vazio); LimparEditorMapeamento(); }
    }

    private async Task SalvarMapeamentoAsync()
    {
        if (!GarantirBanco()) return;
        if (_idCampoAtual <= 0 || !_campoAtualAtivo) { MessageBox.Show("Reative o campo antes de alterar seu mapeamento.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        MapeamentoCampoEtiquetaCadastro mapa = new() { CodigoCampoEtiqueta = _idCampoAtual, OrigemDado = origemDadoComboBox.Text, ExpressaoOrigem = expressaoOrigemTextBox.Text.Trim(), ValorPadrao = valorPadraoTextBox.Text, ObrigatorioParaImpressao = obrigatorioImpressaoCheckBox.Checked, Observacao = observacaoMapeamentoTextBox.Text.Trim(), SituacaoMapeamentoCampoEtiqueta = true };
        ResultadoOperacao resultado = await _mapeamentoController.SalvarAsync(mapa);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (resultado.Sucesso) await RecarregarCampoAtualAsync();
    }

    private async Task InativarMapeamentoAsync()
    {
        if (!GarantirBanco()) return;
        if (_idMapeamentoAtual <= 0) { MessageBox.Show("Não há mapeamento ativo para inativar neste campo.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        ResultadoOperacao resultado = await _mapeamentoController.ExcluirAsync(_idMapeamentoAtual);
        MessageBox.Show(resultado.Mensagem, "Campos da Etiqueta", MessageBoxButtons.OK, resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (resultado.Sucesso) { LimparEditorMapeamento(); await RecarregarCampoAtualAsync(); }
    }

    private async Task ExecutarOperacaoProtegidaAsync(Func<Task> operacao)
    {
        if (_operacaoEmAndamento) return;
        _operacaoEmAndamento = true; AtualizarBotoes();
        try { await operacao(); }
        finally { _operacaoEmAndamento = false; AtualizarBotoes(); }
    }

    private void AplicarModoCampo(ModoCampo modo)
    {
        _modoCampo = modo;
        bool editavel = modo is ModoCampo.Novo or ModoCampo.Edicao;
        dadosEditorPanel.Visible = editavel; dadosVazioPanel.Visible = modo == ModoCampo.Vazio;
        nomeCampoTextBox.Enabled = editavel; tipoDadoComboBox.Enabled = editavel; ordemTextBox.Enabled = editavel; obrigatorioCheckBox.Enabled = editavel; tamanhoMaximoTextBox.Enabled = editavel; formatoSaidaTextBox.Enabled = editavel; descricaoTextBox.Enabled = editavel; situacaoCampoComboBox.Enabled = false;
        if (modo == ModoCampo.Vazio)
        {
            _idCampoAtual = 0; _campoAtualAtivo = false; nomeCampoTextBox.Text = string.Empty; tipoDadoComboBox.SelectedIndex = 0; ordemTextBox.Text = string.Empty; obrigatorioCheckBox.Checked = false; tamanhoMaximoTextBox.Text = string.Empty; formatoSaidaTextBox.Text = string.Empty; descricaoTextBox.Text = string.Empty; situacaoCampoComboBox.SelectedItem = SituacaoCadastroHelper.Ativo; estadoCampoLabel.Text = "Selecione um campo cadastrado ou clique em Novo Campo para iniciar.";
        }
        else if (modo == ModoCampo.Novo) estadoCampoLabel.Text = "Novo campo será cadastrado como Ativo.";
        else estadoCampoLabel.Text = _campoAtualAtivo ? "Campo ativo selecionado." : "Campo inativo selecionado.";
        AtualizarBotoes(); LayoutDadosCampoCard();
    }

    private void AplicarEstadoMapeamento(EstadoMapeamento estado)
    {
        _estadoMapeamento = estado;
        mapeamentoEditorPanel.Visible = estado is EstadoMapeamento.CampoAtivoSemMapeamento or EstadoMapeamento.CampoAtivoComMapeamento or EstadoMapeamento.CampoInativo;
        mapeamentoVazioPanel.Visible = estado == EstadoMapeamento.SemCampo;
        AtualizarBotoes(); LayoutMapeamentoCard();
    }

    private void AtualizarBotoes()
    {
        novoCampoButton.Enabled = !_operacaoEmAndamento && _podeCriarCampo;
        salvarCampoButton.Visible = _modoCampo == ModoCampo.Novo; editarCampoButton.Visible = _modoCampo == ModoCampo.Edicao; situacaoCampoButton.Visible = _modoCampo == ModoCampo.Edicao;
        salvarCampoButton.Enabled = !_operacaoEmAndamento && _podeCriarCampo && _modoCampo == ModoCampo.Novo;
        editarCampoButton.Enabled = !_operacaoEmAndamento && _podeEditarCampo && _modoCampo == ModoCampo.Edicao;
        situacaoCampoButton.Enabled = !_operacaoEmAndamento && ((_campoAtualAtivo && _podeExcluirCampo) || (!_campoAtualAtivo && _podeEditarCampo));
        situacaoCampoButton.Text = _campoAtualAtivo ? "Inativar Campo             F8" : "Reativar Campo             F8";
        situacaoCampoButton.ForeColor = _campoAtualAtivo ? Color.FromArgb(185, 28, 28) : VerdeTexto;
        situacaoCampoButton.BackColor = Color.White;
        situacaoCampoButton.FlatAppearance.BorderColor = _campoAtualAtivo ? Color.FromArgb(254, 202, 202) : Color.FromArgb(187, 247, 208);
        bool mapeamentoEditavel = _estadoMapeamento is EstadoMapeamento.CampoAtivoSemMapeamento or EstadoMapeamento.CampoAtivoComMapeamento;
        bool podeSalvarMapa = _estadoMapeamento == EstadoMapeamento.CampoAtivoSemMapeamento ? _podeCriarMapeamento : _podeEditarMapeamento;
        origemDadoComboBox.Enabled = !_operacaoEmAndamento && mapeamentoEditavel && podeSalvarMapa;
        expressaoOrigemTextBox.Enabled = origemDadoComboBox.Enabled; valorPadraoTextBox.Enabled = origemDadoComboBox.Enabled; obrigatorioImpressaoCheckBox.Enabled = origemDadoComboBox.Enabled; observacaoMapeamentoTextBox.Enabled = origemDadoComboBox.Enabled;
        salvarMapeamentoButton.Enabled = !_operacaoEmAndamento && mapeamentoEditavel && podeSalvarMapa;
        inativarMapeamentoButton.Enabled = !_operacaoEmAndamento && _estadoMapeamento == EstadoMapeamento.CampoAtivoComMapeamento && _podeExcluirMapeamento;
        mapeamentoCard.Visible = _podeConsultarMapeamento;
    }

    private void LimparEditorMapeamento()
    {
        _idMapeamentoAtual = 0; mappingInfoLabel.Text = "Selecione um campo."; mappingCampoLabel.Text = "Campo selecionado: -"; mappingSituacaoCampoLabel.Text = "Situação do Campo: -"; mappingSituacaoMapeamentoLabel.Text = "Situação do Mapeamento: -";
        if (origemDadoComboBox.Items.Count > 0) origemDadoComboBox.SelectedIndex = 0;
        expressaoOrigemTextBox.Text = string.Empty; valorPadraoTextBox.Text = string.Empty; obrigatorioImpressaoCheckBox.Checked = false; observacaoMapeamentoTextBox.Text = string.Empty;
        AplicarEstadoMapeamento(EstadoMapeamento.SemCampo);
    }

    private async Task RecarregarCampoAtualAsync()
    {
        if (_idCampoAtual <= 0) return;
        _selecaoCampoTask = AoSelecionarCampoAsync(_idCampoAtual);
        await _selecaoCampoTask;
    }

    private void CamposEtiquetaForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5 && salvarCampoButton.Visible && salvarCampoButton.Enabled) { e.Handled = true; _ = ExecutarOperacaoProtegidaAsync(SalvarCampoAsync); }
        else if (e.KeyCode == Keys.F6 && editarCampoButton.Visible && editarCampoButton.Enabled) { e.Handled = true; _ = ExecutarOperacaoProtegidaAsync(SalvarCampoAsync); }
        else if (e.KeyCode == Keys.F8 && situacaoCampoButton.Visible && situacaoCampoButton.Enabled) { e.Handled = true; _ = ExecutarOperacaoProtegidaAsync(AlternarSituacaoCampoAsync); }
    }

    private bool EtiquetaSolicitadaAindaEhAtual(CancellationTokenSource origem) => !origem.IsCancellationRequested && ReferenceEquals(_carregarCamposCts, origem) && _etiquetaPreSelecionada > 0;
    private bool CampoSolicitadoAindaEhAtual(long idCampo, CancellationTokenSource origem) => !origem.IsCancellationRequested && ReferenceEquals(_selecaoCampoCts, origem) && _idCampoAtual == idCampo && _camposFiltrados.Any(c => c.CodigoCampoEtiqueta == idCampo);
    private void CancelarSelecaoCampo() { CancellationTokenSource? anterior = Interlocked.Exchange(ref _selecaoCampoCts, null); anterior?.Cancel(); anterior?.Dispose(); }
    private void CancelarConsultasPendentes() { CancellationTokenSource? campos = Interlocked.Exchange(ref _carregarCamposCts, null); campos?.Cancel(); campos?.Dispose(); CancelarSelecaoCampo(); }
    private int ProximaOrdemSugerida() => _campos.Count == 0 ? 1 : _campos.Max(c => c.Ordem) + 1;
    private bool GarantirBanco() { if (_integracaoBancoHabilitada) return true; MessageBox.Show("Integração com banco desabilitada no momento.", "Campos da Etiqueta", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
    private static void SelecionarCombo(ComboBox combo, string valor) { int idx = combo.FindStringExact(valor); combo.SelectedIndex = idx >= 0 ? idx : (combo.Items.Count > 0 ? 0 : -1); }

    private void LayoutCamposCard()
    {
        int p = 20, w = Math.Max(260, camposCard.ClientSize.Width - p * 2);
        etiquetaTituloLabel.SetBounds(p, 18, w, 28); etiquetaCodigoLabel.SetBounds(p, 48, w, 20); etiquetaResumoLabel.SetBounds(p, 68, w, 20);
        novoCampoButton.SetBounds(p, 98, Math.Min(190, w), 42); searchPanel.SetBounds(p, 154, w, 42);
        searchIconLabel.SetBounds(12, 0, 28, 42); searchTextBox.SetBounds(44, 9, Math.Max(80, w - 56), 23);
        camposTablePanel.SetBounds(p, 212, w, Math.Max(230, camposCard.ClientSize.Height - 260)); listFooterLabel.SetBounds(p, camposCard.ClientSize.Height - 36, w, 22);
        int tableW = Math.Max(260, camposTablePanel.ClientSize.Width - 22); camposHeaderPanel.SetBounds(0, 0, tableW, 34); camposRowsPanel.SetBounds(0, 34, tableW + 14, Math.Max(120, camposTablePanel.ClientSize.Height - 34));
        int ordemW = 52, statusW = 78, tipoW = Math.Min(105, Math.Max(78, tableW / 4)), nomeW = Math.Max(90, tableW - ordemW - tipoW - statusW - 24);
        ordemHeaderLabel.SetBounds(12, 7, ordemW, 20); campoHeaderLabel.SetBounds(12 + ordemW, 7, nomeW, 20); tipoHeaderLabel.SetBounds(12 + ordemW + nomeW, 7, tipoW, 20); situacaoHeaderLabel.SetBounds(tableW - statusW - 10, 7, statusW, 20);
        foreach (Panel row in _campoPorLinha.Keys) { row.Width = Math.Max(260, camposRowsPanel.ClientSize.Width - 22); LayoutLinhaCampo(row); }
    }

    private void LayoutLinhaCampo(Panel row)
    {
        int w = Math.Max(260, row.Width), ordemW = 52, statusW = 78, tipoW = Math.Min(105, Math.Max(78, w / 4)), nomeW = Math.Max(90, w - ordemW - tipoW - statusW - 24);
        Label[] labels = row.Controls.OfType<Label>().ToArray();
        if (labels.Length >= 3)
        {
            labels[0].SetBounds(12, 14, ordemW, 24); labels[1].SetBounds(12 + ordemW, 14, nomeW, 24); labels[2].SetBounds(12 + ordemW + nomeW, 14, tipoW, 24);
        }
        if (_badgePorLinha.TryGetValue(row, out RoundedPanel? badge)) badge.SetBounds(w - statusW - 10, 13, statusW, 28);
        if (_marcadorPorLinha.TryGetValue(row, out Panel? marker)) marker.SetBounds(0, 0, 3, row.Height);
    }

    private void LayoutDadosCampoCard()
    {
        int p = 20, w = Math.Max(260, dadosCampoCard.ClientSize.Width - p * 2);
        dadosCampoTituloLabel.SetBounds(p, 18, w, 24); estadoCampoLabel.SetBounds(p, 48, w, 42); dadosVazioPanel.SetBounds(p, 112, w, Math.Max(160, dadosCampoCard.ClientSize.Height - 190)); dadosEditorPanel.SetBounds(p, 100, w, Math.Max(420, dadosCampoCard.ClientSize.Height - 168));
        int col = Math.Max(120, (w - 14) / 2);
        LayoutCampoInput(nomeCampoInputPanel, nomeCampoLabel, nomeCampoTextBox, 0, 0, w, "text");
        LayoutCampoInput(tipoDadoInputPanel, tipoDadoLabel, tipoDadoComboBox, 0, 62, col, "combo"); LayoutCampoInput(ordemInputPanel, ordemLabel, ordemTextBox, col + 14, 62, col, "text");
        obrigatorioCheckBox.SetBounds(0, 126, w, 24); LayoutCampoInput(tamanhoMaximoInputPanel, tamanhoMaximoLabel, tamanhoMaximoTextBox, 0, 162, col, "text"); LayoutCampoInput(formatoSaidaInputPanel, formatoSaidaLabel, formatoSaidaTextBox, col + 14, 162, col, "text");
        LayoutCampoInput(descricaoInputPanel, descricaoLabel, descricaoTextBox, 0, 224, w, "multiline"); LayoutCampoInput(situacaoCampoInputPanel, situacaoCampoLabel, situacaoCampoComboBox, 0, 334, w, "combo");
        salvarCampoButton.SetBounds(0, dadosEditorPanel.Height - 46, Math.Min(168, w), 38); editarCampoButton.SetBounds(0, dadosEditorPanel.Height - 46, Math.Min(188, w), 38); situacaoCampoButton.SetBounds(Math.Min(204, w / 2), dadosEditorPanel.Height - 46, Math.Min(196, w - Math.Min(204, w / 2)), 38);
    }

    private void LayoutMapeamentoCard()
    {
        int p = 20, w = Math.Max(260, mapeamentoCard.ClientSize.Width - p * 2);
        mapeamentoTituloLabel.SetBounds(p, 18, w, 24); mappingInfoLabel.SetBounds(p, 48, w, 42); mappingCampoLabel.SetBounds(p, 102, w, 20); mappingSituacaoCampoLabel.SetBounds(p, 128, w, 20); mappingSituacaoMapeamentoLabel.SetBounds(p, 154, w, 20);
        mapeamentoVazioPanel.SetBounds(p, 194, w, Math.Max(150, mapeamentoCard.ClientSize.Height - 270)); mapeamentoEditorPanel.SetBounds(p, 194, w, Math.Max(370, mapeamentoCard.ClientSize.Height - 240));
        LayoutCampoInput(origemDadoInputPanel, origemDadoLabel, origemDadoComboBox, 0, 0, w, "combo"); LayoutCampoInput(expressaoOrigemInputPanel, expressaoOrigemLabel, expressaoOrigemTextBox, 0, 62, w, "text"); LayoutCampoInput(valorPadraoInputPanel, valorPadraoLabel, valorPadraoTextBox, 0, 124, w, "text");
        obrigatorioImpressaoCheckBox.SetBounds(0, 188, w, 24); LayoutCampoInput(observacaoMapeamentoInputPanel, observacaoMapeamentoLabel, observacaoMapeamentoTextBox, 0, 224, w, "multiline");
        salvarMapeamentoButton.SetBounds(0, mapeamentoEditorPanel.Height - 46, Math.Min(180, w), 38); inativarMapeamentoButton.SetBounds(Math.Min(196, w / 2), mapeamentoEditorPanel.Height - 46, Math.Min(190, w - Math.Min(196, w / 2)), 38);
    }

    private static void LayoutCampoInput(Control panel, Label label, Control input, int x, int y, int width, string tipo)
    {
        int inputHeight = tipo == "multiline" ? 72 : 34; label.SetBounds(x, y, width, 20); panel.SetBounds(x, y + 22, width, inputHeight);
        if (input is TextBox textBox) { textBox.BorderStyle = BorderStyle.None; textBox.SetBounds(12, tipo == "multiline" ? 8 : 7, Math.Max(40, width - 24), Math.Max(20, inputHeight - 14)); }
        else input.SetBounds(8, 4, Math.Max(40, width - 16), Math.Max(24, inputHeight - 8));
    }

    private void ArrastarJanela(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture(); SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}

