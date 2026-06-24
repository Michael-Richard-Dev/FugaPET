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

public partial class BalancaForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    // Coluna "Setor / Conexao" das linhas da lista; alinhada ao cabecalho listaDetalhesHeaderLabel.
    private const int ColunaDetalhesLinha = 135;
    private static readonly Color CorSelecao = Color.FromArgb(254, 242, 242);
    private static readonly Color CorMarcador = Color.FromArgb(239, 68, 68);
    private static readonly Color CorAtivo = Color.FromArgb(22, 163, 74);
    private static readonly Color CorInativo = Color.FromArgb(234, 88, 12);

    private readonly bool _integracaoBancoHabilitada = EstadoIntegracaoBanco.Habilitado;
    private readonly BalancaController _balancaController;
    private readonly SetorController _setorController;
    private readonly AuditoriaServico _auditoriaServico;
    private readonly List<BalancaCadastro> _balancasCarregadas = [];
    private readonly Dictionary<Panel, BalancaCadastro> _balancaPorLinha = [];

    private long _idBalancaAtual;
    private bool _operacaoEmAndamento;
    private ModoTela _modoTela = ModoTela.Vazio;
    private float _escalaLista = 1f;

    public BalancaForm(
        BalancaController? balancaController = null,
        SetorController? setorController = null,
        AuditoriaServico? auditoriaServico = null)
    {
        _balancaController = balancaController ?? FabricaControladoresCadastro.CriarBalancaController();
        _setorController = setorController ?? FabricaControladoresCadastro.CriarSetorController();
        _auditoriaServico = auditoriaServico ?? FabricaControladoresCadastro.CriarAuditoriaServico();

        InitializeComponent();
        IconeJanelaHelper.AplicarIconePadrao(this);
        ConfigurarRodape();
        ConfigurarEventos();
        ConfigurarCombos();
        balancasCard.Resize += (_, _) => AjustarLayout();
        resumoCard.Resize += (_, _) => AjustarLayout();
        dadosCard.Resize += (_, _) => AjustarLayout();
        AjustarLayout();
        ConfigurarModo(ModoTela.Vazio);
        Shown += async (_, _) => await InicializarTelaAsync();
    }

    private async Task InicializarTelaAsync()
    {
        if (!AutorizacaoServico.PodeVisualizarRotina(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Balanca))
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
            await PopularSetoresAsync();
            await CarregarBalancasAsync();
        }
    }

    private async Task RegistrarAcessoDiretoNegadoSeguroAsync(long codigoUsuario)
    {
        try
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(
                codigoUsuario,
                $"Acesso direto negado a Cadastro de Balança ({PermissoesSistema.Modulos.Cadastro}/{PermissoesSistema.Rotinas.Balanca}/CONSULTAR ou VISUALIZAR).",
                nameof(BalancaForm));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"Falha ao registrar acesso direto negado ao BalancaForm: {ex}");
        }
    }

    private void ConfigurarRodape()
    {
        cellUserText.Text = UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";

        AtualizarRelogioRodape();
        System.Windows.Forms.Timer relogio = new() { Interval = 1000 };
        relogio.Tick += (_, _) => AtualizarRelogioRodape();
        relogio.Start();
    }

    private void AtualizarRelogioRodape()
    {
        DateTime agora = DateTime.Now;
        cellHoraText.Text = agora.ToString("HH:mm");
        cellDataText.Text = agora.ToString("dd/MM/yyyy");
    }

    private void ConfigurarEventos()
    {
        menuHeaderLabel.Click += (_, _) => Close();
        closeWindowLabel.Click += (_, _) => Close();
        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) =>
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;

        customTitleBarPanel.MouseDown += ArrastarJanela;
        headerTitleLabel.MouseDown += ArrastarJanela;
        headerSubtitleLabel.MouseDown += ArrastarJanela;
        companyLogoPictureBox.MouseDown += ArrastarJanela;
        novoBalancaButtonPanel.Cursor = Cursors.Hand;
        novoBalancaIconLabel.Cursor = Cursors.Hand;
        novoBalancaTextLabel.Cursor = Cursors.Hand;
        novoBalancaButtonPanel.Click += (_, _) => PrepararNovaBalanca();
        novoBalancaIconLabel.Click += (_, _) => PrepararNovaBalanca();
        novoBalancaTextLabel.Click += (_, _) => PrepararNovaBalanca();
        searchTextBox.TextChanged += (_, _) => AplicarFiltro();
        tipoConexaoComboBox.SelectedIndexChanged += (_, _) => AtualizarCamposPorTipoConexao();

        salvarButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(SalvarBalancaAsync, "BALANCA_SALVAR_ERRO");
        salvarAlteracoesButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(AtualizarBalancaAsync, "BALANCA_ATUALIZAR_ERRO");
        alterarSituacaoButton.Click += async (_, _) =>
            await ExecutarOperacaoProtegidaAsync(AlternarSituacaoAsync, "BALANCA_ALTERAR_SITUACAO_ERRO");
    }

    private void ConfigurarCombos()
    {
        tipoConexaoComboBox.Items.AddRange(["SERIAL", "TCP_IP", "USB", "MANUAL"]);
        paridadeComboBox.Items.AddRange(["NONE", "EVEN", "ODD", "MARK", "SPACE"]);
        stopBitsComboBox.Items.AddRange(["1", "1.5", "2"]);
        flowControlComboBox.Items.AddRange(["NONE", "XON_XOFF", "RTS_CTS", "DTR_DSR"]);

        tipoConexaoComboBox.SelectedItem = "SERIAL";
        paridadeComboBox.SelectedItem = "NONE";
        stopBitsComboBox.SelectedItem = "1";
        flowControlComboBox.SelectedItem = "NONE";
        situacaoTextBox.Text = "Ativo";
        AtualizarCamposPorTipoConexao();
    }

    private async Task PopularSetoresAsync()
    {
        try
        {
            IReadOnlyList<SetorCadastro> setores = await _setorController.ListarAsync();
            setorComboBox.DisplayMember = nameof(SetorCadastro.Nome);
            setorComboBox.ValueMember = nameof(SetorCadastro.Codigo);
            setorComboBox.DataSource = setores.Where(item => item.SituacaoSetor).ToList();
            setorComboBox.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync(
                    "BALANCA_CARREGAR_SETORES_ERRO",
                    ex,
                    nameof(BalancaForm),
                    "Não foi possível carregar os setores. Acione o suporte."),
                "Cadastro de Balança",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task CarregarBalancasAsync()
    {
        try
        {
            IReadOnlyList<BalancaCadastro> balancas = await _balancaController.ListarAsync();
            _balancasCarregadas.Clear();
            _balancasCarregadas.AddRange(balancas);
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync(
                    "BALANCA_CARREGAR_ERRO",
                    ex,
                    nameof(BalancaForm),
                    "Não foi possível carregar as balanças. Acione o suporte."),
                "Cadastro de Balança",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void AplicarFiltro()
    {
        string consulta = NormalizarBusca(searchTextBox.Text);
        IEnumerable<BalancaCadastro> filtradas = _balancasCarregadas.Where(item =>
            string.IsNullOrEmpty(consulta)
            || NormalizarBusca(item.NomeBalanca).Contains(consulta)
            || NormalizarBusca(ObterNomeSetor(item.CodigoSetor)).Contains(consulta)
            || NormalizarBusca(item.TipoConexao).Contains(consulta)
            || NormalizarBusca(item.IdentificacaoLocal).Contains(consulta)
            || NormalizarBusca(item.EnderecoIp).Contains(consulta)
            || NormalizarBusca(item.PortaSerial).Contains(consulta)
            || NormalizarBusca(item.SituacaoBalanca ? "Ativo" : "Inativo").Contains(consulta));

        RecriarLinhas(filtradas.ToList());
    }

    private void RecriarLinhas(IReadOnlyList<BalancaCadastro> balancas)
    {
        balancasFlowPanel.SuspendLayout();
        balancasFlowPanel.Controls.Clear();
        _balancaPorLinha.Clear();

        foreach (BalancaCadastro balanca in balancas)
        {
            Panel linha = CriarLinhaBalanca(balanca);
            _balancaPorLinha[linha] = balanca;
            balancasFlowPanel.Controls.Add(linha);
        }

        balancasFlowPanel.ResumeLayout();
        quantidadeBalancasLabel.Text = $"Exibindo {balancas.Count} de {_balancasCarregadas.Count} balanças";

        if (_idBalancaAtual > 0
            && !balancas.Any(item => item.CodigoBalanca == _idBalancaAtual))
        {
            LimparSelecao();
        }
    }

    private Panel CriarLinhaBalanca(BalancaCadastro balanca)
    {
        // Linha em coluna unica (igual ao Cargo): Nome | Setor/Conexao | badge de Situacao,
        // alinhados as mesmas colunas do cabecalho (Nome=14, Setor/Conexao=ColunaDetalhes).
        // Altura da linha escala com a lista (no Cargo a linha cresce quando a janela e maximizada).
        int rowH = Math.Max(46, (int)Math.Round(46 * _escalaLista));
        const int colDetalhes = ColunaDetalhesLinha;
        Panel linha = new()
        {
            Width = balancasFlowPanel.ClientSize.Width,
            Height = rowH,
            Margin = new Padding(0, 0, 0, 1),
            BackColor = Color.White,
            Cursor = Cursors.Hand,
            Tag = balanca.CodigoBalanca
        };

        Panel marcador = new()
        {
            Name = "marcadorSelecao",
            Width = 3,
            Dock = DockStyle.Left,
            BackColor = CorMarcador,
            Visible = balanca.CodigoBalanca == _idBalancaAtual
        };
        Label nome = new()
        {
            Text = balanca.NomeBalanca,
            Font = new Font("Segoe UI", Math.Clamp(8.25f * _escalaLista, 8.25f, 11.5f), FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(14, (rowH - 22) / 2),
            Size = new Size(colDetalhes - 14 - 8, 22),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label detalhes = new()
        {
            Text = $"{ObterNomeSetor(balanca.CodigoSetor)} • {balanca.TipoConexao}",
            Font = new Font("Segoe UI", Math.Clamp(8f * _escalaLista, 8f, 10.5f)),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(colDetalhes, (rowH - 22) / 2),
            Size = new Size(Math.Max(40, linha.Width - colDetalhes - 86), 22),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        bool ativo = balanca.SituacaoBalanca;
        int badgeW = LarguraBadgeSituacao();
        int badgeH = Math.Max(24, (int)Math.Round(24 * _escalaLista));
        RoundedPanel situacaoBadge = new()
        {
            Name = "situacaoBadge",
            BorderRadius = 9,
            FillColor = ativo ? Color.FromArgb(220, 252, 231) : Color.FromArgb(255, 237, 213),
            BorderColor = ativo ? Color.FromArgb(22, 163, 74) : Color.FromArgb(234, 88, 12),
            ShadowBlur = 0,
            ShadowOffsetY = 0,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(badgeW, badgeH),
            Location = new Point(linha.Width - badgeW - 14, (rowH - badgeH) / 2)
        };
        Label situacao = new()
        {
            Dock = DockStyle.Fill,
            Text = ativo ? "Ativo" : "Inativo",
            Font = new Font("Segoe UI", Math.Clamp(7.5f * _escalaLista, 7.5f, 10f), FontStyle.Bold),
            ForeColor = ativo ? CorAtivo : CorInativo,
            TextAlign = ContentAlignment.MiddleCenter
        };
        situacaoBadge.Controls.Add(situacao);

        linha.Controls.Add(situacaoBadge);
        linha.Controls.Add(detalhes);
        linha.Controls.Add(nome);
        linha.Controls.Add(marcador);

        void selecionar(object? _, EventArgs __) => SelecionarBalanca(linha, balanca);
        linha.Click += selecionar;
        nome.Click += selecionar;
        detalhes.Click += selecionar;
        situacao.Click += selecionar;
        situacaoBadge.Click += selecionar;
        linha.Resize += (_, _) =>
        {
            situacaoBadge.Left = linha.Width - situacaoBadge.Width - 14;
            detalhes.Width = Math.Max(40, linha.Width - colDetalhes - situacaoBadge.Width - 22);
        };

        if (balanca.CodigoBalanca == _idBalancaAtual)
        {
            linha.BackColor = CorSelecao;
        }

        return linha;
    }

    private void SelecionarBalanca(Panel linhaSelecionada, BalancaCadastro balanca)
    {
        foreach (Control controle in balancasFlowPanel.Controls)
        {
            if (controle is not Panel linha) continue;
            bool selecionada = ReferenceEquals(linha, linhaSelecionada);
            linha.BackColor = selecionada ? CorSelecao : Color.White;
            Control? marcador = linha.Controls["marcadorSelecao"];
            if (marcador is not null) marcador.Visible = selecionada;
        }

        _idBalancaAtual = balanca.CodigoBalanca;
        PreencherCampos(balanca);
        PreencherResumo(balanca);
        ConfigurarModo(ModoTela.Edicao);
    }

    private void PreencherCampos(BalancaCadastro balanca)
    {
        nomeBalancaTextBox.Text = balanca.NomeBalanca;
        setorComboBox.SelectedValue = balanca.CodigoSetor;
        identificacaoLocalTextBox.Text = balanca.IdentificacaoLocal;
        situacaoTextBox.Text = balanca.SituacaoBalanca ? "Ativo" : "Inativo";
        tipoConexaoComboBox.SelectedItem = string.IsNullOrWhiteSpace(balanca.TipoConexao)
            ? "SERIAL"
            : balanca.TipoConexao;
        enderecoIpTextBox.Text = balanca.EnderecoIp;
        portaTcpTextBox.Text = balanca.PortaTcp?.ToString() ?? string.Empty;
        portaSerialTextBox.Text = balanca.PortaSerial;
        baudRateTextBox.Text = balanca.BaudRate?.ToString() ?? string.Empty;
        dataBitsTextBox.Text = balanca.DataBits?.ToString() ?? string.Empty;
        paridadeComboBox.SelectedItem = string.IsNullOrWhiteSpace(balanca.Paridade) ? "NONE" : balanca.Paridade;
        stopBitsComboBox.SelectedItem = balanca.StopBits?.ToString(CultureInfo.InvariantCulture) ?? "1";
        flowControlComboBox.SelectedItem = string.IsNullOrWhiteSpace(balanca.FlowControl) ? "NONE" : balanca.FlowControl;
        protocoloTextBox.Text = balanca.Protocolo;
        observacaoTextBox.Text = balanca.Observacao;
        AtualizarCamposPorTipoConexao();
    }

    private void PreencherResumo(BalancaCadastro balanca)
    {
        resumoNomeValueLabel.Text = balanca.NomeBalanca;
        resumoSetorValueLabel.Text = ObterNomeSetor(balanca.CodigoSetor);
        resumoConexaoValueLabel.Text = balanca.TipoConexao;
        resumoSituacaoValueLabel.Text = balanca.SituacaoBalanca ? "Ativo" : "Inativo";
        resumoSituacaoValueLabel.ForeColor = balanca.SituacaoBalanca ? CorAtivo : CorInativo;
        resumoDataLabel.Text = balanca.BalancaCriadoEm.HasValue
            ? $"Data e Hora do Cadastro: {balanca.BalancaCriadoEm.Value:dd/MM/yyyy HH:mm}"
            : "Data e Hora do Cadastro: -";
    }

    private void PrepararNovaBalanca()
    {
        _idBalancaAtual = 0;
        LimparSelecaoVisual();
        LimparCampos();
        LimparResumo();
        ConfigurarModo(ModoTela.Novo);
        nomeBalancaTextBox.Focus();
    }

    private void LimparSelecao()
    {
        _idBalancaAtual = 0;
        LimparSelecaoVisual();
        LimparCampos();
        LimparResumo();
        ConfigurarModo(ModoTela.Vazio);
    }

    private void LimparSelecaoVisual()
    {
        foreach (Control controle in balancasFlowPanel.Controls)
        {
            if (controle is not Panel linha) continue;
            linha.BackColor = Color.White;
            Control? marcador = linha.Controls["marcadorSelecao"];
            if (marcador is not null) marcador.Visible = false;
        }
    }

    private void LimparCampos()
    {
        nomeBalancaTextBox.Clear();
        setorComboBox.SelectedIndex = -1;
        identificacaoLocalTextBox.Clear();
        situacaoTextBox.Text = "Ativo";
        tipoConexaoComboBox.SelectedItem = "SERIAL";
        enderecoIpTextBox.Clear();
        portaTcpTextBox.Clear();
        portaSerialTextBox.Clear();
        baudRateTextBox.Text = "9600";
        dataBitsTextBox.Text = "8";
        paridadeComboBox.SelectedItem = "NONE";
        stopBitsComboBox.SelectedItem = "1";
        flowControlComboBox.SelectedItem = "NONE";
        protocoloTextBox.Clear();
        observacaoTextBox.Clear();
        AtualizarCamposPorTipoConexao();
    }

    private void LimparResumo()
    {
        resumoNomeValueLabel.Text = "-";
        resumoSetorValueLabel.Text = "-";
        resumoConexaoValueLabel.Text = "-";
        resumoSituacaoValueLabel.Text = "-";
        resumoSituacaoValueLabel.ForeColor = Color.FromArgb(100, 116, 139);
        resumoDataLabel.Text = "Data e Hora do Cadastro: -";
    }

    private async Task SalvarBalancaAsync()
    {
        if (!ConfirmarIntegracaoBanco()) return;

        BalancaCadastro balanca = MontarBalancaDoForm();
        balanca.BalancaCriadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        ResultadoOperacao resultado = await _balancaController.InserirAsync(balanca);
        ExibirResultado(resultado);

        if (resultado.Sucesso)
        {
            PrepararNovaBalanca();
            await CarregarBalancasAsync();
        }
    }

    private async Task AtualizarBalancaAsync()
    {
        if (!ConfirmarIntegracaoBanco() || _idBalancaAtual <= 0) return;

        BalancaCadastro? anterior = _balancasCarregadas
            .FirstOrDefault(item => item.CodigoBalanca == _idBalancaAtual);
        if (anterior is null)
        {
            MessageBox.Show("Selecione uma balança válida.", "Cadastro de Balança", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        BalancaCadastro balanca = MontarBalancaDoForm();
        balanca.CodigoBalanca = _idBalancaAtual;
        balanca.SituacaoBalanca = anterior.SituacaoBalanca;
        balanca.ParametrosTecnicos = anterior.ParametrosTecnicos;
        balanca.BalancaAtualizadoPor = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;

        ResultadoOperacao resultado = await _balancaController.AtualizarAsync(balanca);
        ExibirResultado(resultado);
        if (resultado.Sucesso) await CarregarBalancasAsync();
    }

    private async Task AlternarSituacaoAsync()
    {
        if (!ConfirmarIntegracaoBanco() || _idBalancaAtual <= 0) return;

        BalancaCadastro? balanca = _balancasCarregadas
            .FirstOrDefault(item => item.CodigoBalanca == _idBalancaAtual);
        if (balanca is null) return;

        string acao = balanca.SituacaoBalanca ? "inativação" : "reativação";
        if (MessageBox.Show(
                $"Confirma a {acao} da balança \"{balanca.NomeBalanca}\"?",
                "Cadastro de Balança",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        ResultadoOperacao resultado = balanca.SituacaoBalanca
            ? await _balancaController.ExcluirAsync(balanca.CodigoBalanca)
            : await _balancaController.ReativarAsync(balanca.CodigoBalanca);

        ExibirResultado(resultado);
        if (resultado.Sucesso)
        {
            LimparSelecao();
            await CarregarBalancasAsync();
        }
    }

    private BalancaCadastro MontarBalancaDoForm()
        => new()
        {
            CodigoSetor = ObterCodigoSetorSelecionado(),
            NomeBalanca = nomeBalancaTextBox.Text,
            IdentificacaoLocal = identificacaoLocalTextBox.Text,
            EnderecoIp = enderecoIpTextBox.Text,
            PortaTcp = ParseInteiro(portaTcpTextBox.Text),
            PortaSerial = portaSerialTextBox.Text,
            TipoConexao = tipoConexaoComboBox.SelectedItem?.ToString() ?? string.Empty,
            BaudRate = ParseInteiro(baudRateTextBox.Text),
            DataBits = ParseInteiro(dataBitsTextBox.Text),
            Paridade = paridadeComboBox.SelectedItem?.ToString() ?? string.Empty,
            StopBits = ParseDecimal(stopBitsComboBox.SelectedItem?.ToString()),
            FlowControl = flowControlComboBox.SelectedItem?.ToString() ?? string.Empty,
            Protocolo = protocoloTextBox.Text,
            Observacao = observacaoTextBox.Text,
            SituacaoBalanca = string.Equals(
                situacaoTextBox.Text,
                "Ativo",
                StringComparison.OrdinalIgnoreCase)
        };

    private async Task ExecutarOperacaoProtegidaAsync(Func<Task> operacao, string acaoErro)
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
            MessageBox.Show(
                await ErroUsuarioHelper.TratarAsync(
                    acaoErro,
                    ex,
                    nameof(BalancaForm),
                    "Não foi possível concluir a operação de balança. Acione o suporte."),
                "Cadastro de Balança",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        finally
        {
            _operacaoEmAndamento = false;
            AtualizarBotoes();
        }
    }

    private void ConfigurarModo(ModoTela modo)
    {
        _modoTela = modo;
        bool possuiRegistro = modo == ModoTela.Edicao;

        AtualizarVisibilidadeCampos();
        salvarButton.Visible = modo == ModoTela.Novo
            && AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Balanca,
                PermissoesSistema.Acoes.Criar);
        salvarAlteracoesButton.Visible = possuiRegistro
            && AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Balanca,
                PermissoesSistema.Acoes.Editar);
        alterarSituacaoButton.Visible = possuiRegistro;
        AtualizarBotoes();
    }

    // Card "Dados da Balança": no modo Vazio mostra apenas o titulo (como Cargo/Setor);
    // os campos so aparecem ao criar (Novo) ou selecionar uma balanca (Edicao).
    private void AtualizarVisibilidadeCampos()
    {
        bool mostrarCampos = _modoTela is ModoTela.Novo or ModoTela.Edicao;

        identificacaoSectionLabel.Visible = mostrarCampos;
        identificacaoTable.Visible = mostrarCampos;
        conexaoSectionLabel.Visible = mostrarCampos;
        conexaoTable.Visible = mostrarCampos;
        observacaoSectionLabel.Visible = mostrarCampos;
        observacaoInputPanel.Visible = mostrarCampos;

        // Desabilita apenas os CAMPOS durante uma operacao (nao o painel inteiro), para o titulo
        // "Dados da Balanca" e o icone nao ficarem acinzentados no modo Vazio (igual ao Cargo).
        bool habilitarCampos = mostrarCampos && !_operacaoEmAndamento;
        identificacaoTable.Enabled = habilitarCampos;
        conexaoTable.Enabled = habilitarCampos;
        serialPanel.Enabled = habilitarCampos;
        observacaoInputPanel.Enabled = habilitarCampos;

        if (!mostrarCampos)
        {
            parametrosSeriaisSectionLabel.Visible = false;
            serialPanel.Visible = false;
            tcpPanel.Visible = false;
            portaSerialPanel.Visible = false;
            manualWarningLabel.Visible = false;
            return;
        }

        AplicarRegrasPorTipoConexao();
    }

    private void AtualizarBotoes()
    {
        bool habilitar = !_operacaoEmAndamento;
        salvarButton.Enabled = habilitar;
        salvarAlteracoesButton.Enabled = habilitar;
        alterarSituacaoButton.Enabled = habilitar;
        novoBalancaButtonPanel.Enabled = habilitar;

        BalancaCadastro? balanca = _balancasCarregadas
            .FirstOrDefault(item => item.CodigoBalanca == _idBalancaAtual);
        bool ativa = balanca?.SituacaoBalanca ?? true;
        alterarSituacaoButton.Text = ativa
            ? "Inativar Balança          F8"
            : "Reativar Balança          F8";
        alterarSituacaoButton.ForeColor = ativa ? Color.FromArgb(220, 38, 38) : CorAtivo;

        bool podeAlterar = ativa
            ? AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Balanca,
                PermissoesSistema.Acoes.Excluir)
            : AutorizacaoServico.PossuiPermissao(
                PermissoesSistema.Modulos.Cadastro,
                PermissoesSistema.Rotinas.Balanca,
                PermissoesSistema.Acoes.Editar);
        alterarSituacaoButton.Visible = _modoTela == ModoTela.Edicao && podeAlterar;
    }

    private void AtualizarCamposPorTipoConexao()
    {
        // So aplica as regras quando os campos estao visiveis (Novo/Edicao);
        // no modo Vazio o card fica em branco.
        if (_modoTela is not (ModoTela.Novo or ModoTela.Edicao)) return;
        AplicarRegrasPorTipoConexao();
    }

    private void AplicarRegrasPorTipoConexao()
    {
        string tipo = tipoConexaoComboBox.SelectedItem?.ToString() ?? "SERIAL";
        bool serial = tipo == "SERIAL";
        bool tcp = tipo == "TCP_IP";
        bool usb = tipo == "USB";
        bool manual = tipo == "MANUAL";

        DefinirGrupoAtivo(tcpPanel, tcp);
        DefinirGrupoAtivo(portaSerialPanel, serial);
        DefinirGrupoAtivo(serialPanel, serial);
        parametrosSeriaisSectionLabel.Visible = serial || manual;
        parametrosSeriaisSectionLabel.Text = manual ? "OPERAÇÃO MANUAL" : "PARÂMETROS SERIAIS";
        identificacaoLocalTextBox.Enabled = usb || serial || tcp;
        manualWarningLabel.Visible = manual;
    }

    private void AjustarLayout()
    {
        AjustarEscalaFontes();
        AjustarBotoesLayout();
    }

    // Mesma logica de escala de fonte do SetorForm/CargoForm (Math.Clamp por tipo de controle),
    // para que os tamanhos de fonte fiquem iguais aos dessas telas em qualquer tamanho de janela.
    private void AjustarEscalaFontes()
    {
        float csLista = CalcularEscala(balancasCard, 405f, 596f);
        float csDados = CalcularEscala(dadosCard, 590f, 596f);
        float csResumo = CalcularEscala(resumoCard, 318f, 596f);
        _escalaLista = csLista;

        // Card da lista (esquerda)
        DefinirFonte(balancasTitleLabel, 8.5f, csLista, 8.5f, 12f, FontStyle.Bold);
        DefinirFonte(novoBalancaTextLabel, 7.5f, csLista, 7.5f, 10.5f, FontStyle.Bold);
        DefinirFonte(searchTextBox, 9f, csLista, 9f, 12f, FontStyle.Regular);
        float tamanhoIconeBusca = Math.Clamp(11f * csLista, 11f, 14f);
        if (Math.Abs(searchIconLabel.Font.Size - tamanhoIconeBusca) > 0.05f)
        {
            searchIconLabel.Font = new Font("Segoe MDL2 Assets", tamanhoIconeBusca);
        }
        DefinirFonte(listaNomeHeaderLabel, 7.5f, csLista, 7.5f, 10.5f, FontStyle.Bold);
        DefinirFonte(listaDetalhesHeaderLabel, 7.5f, csLista, 7.5f, 10.5f, FontStyle.Bold);
        DefinirFonte(listaSituacaoHeaderLabel, 7.5f, csLista, 7.5f, 10.5f, FontStyle.Bold);
        DefinirFonte(quantidadeBalancasLabel, 8.5f, csLista, 8.5f, 11f, FontStyle.Regular);

        // Card de dados (centro)
        DefinirFonte(dadosTitleLabel, 8.5f, csDados, 9f, 12.5f, FontStyle.Bold);
        foreach (Label secao in new[] { identificacaoSectionLabel, conexaoSectionLabel, parametrosSeriaisSectionLabel, observacaoSectionLabel })
            DefinirFonte(secao, 8f, csDados, 8.5f, 11f, FontStyle.Bold);
        foreach (Label rotulo in new[]
        {
            nomeBalancaLabel, setorLabel, identificacaoLocalLabel, situacaoLabel, tipoConexaoLabel,
            enderecoIpLabel, portaTcpLabel, portaSerialLabel, baudRateLabel, dataBitsLabel,
            paridadeLabel, stopBitsLabel, flowControlLabel, protocoloLabel
        })
            DefinirFonte(rotulo, 7.75f, csDados, 8.5f, 11f, FontStyle.Bold);
        foreach (Control campo in new Control[]
        {
            nomeBalancaTextBox, setorComboBox, identificacaoLocalTextBox, situacaoTextBox, tipoConexaoComboBox,
            enderecoIpTextBox, portaTcpTextBox, portaSerialTextBox, baudRateTextBox, dataBitsTextBox,
            paridadeComboBox, stopBitsComboBox, flowControlComboBox, protocoloTextBox, observacaoTextBox
        })
            DefinirFonte(campo, 9f, csDados, 9f, 12f, FontStyle.Regular);
        DefinirFonte(manualWarningLabel, 8.5f, csDados, 8.5f, 11f, FontStyle.Bold);

        // Card de resumo (direita)
        DefinirFonte(resumoTitleLabel, 8.5f, csResumo, 9f, 12.5f, FontStyle.Bold);
        foreach (Label legenda in new[] { resumoNomeLabel, resumoSetorLabel, resumoConexaoLabel, resumoSituacaoLabel })
            DefinirFonte(legenda, 8.5f, csResumo, 8.5f, 11.5f, FontStyle.Regular);
        foreach (Label valor in new[] { resumoNomeValueLabel, resumoSetorValueLabel, resumoConexaoValueLabel, resumoSituacaoValueLabel })
            DefinirFonte(valor, 8.5f, csResumo, 8.5f, 12f, FontStyle.Bold);
        foreach (Button botao in new[] { salvarButton, salvarAlteracoesButton, alterarSituacaoButton })
            DefinirFonte(botao, 8f, csResumo, 8f, 11f, FontStyle.Bold);
    }

    private static float CalcularEscala(Control card, float baseLargura, float baseAltura)
    {
        if (card.Width <= 0 || card.Height <= 0) return 1f;
        return Math.Min(card.Width / baseLargura, card.Height / baseAltura);
    }

    // Largura do badge de Situacao das linhas, escalada com a lista (usada na linha e no cabecalho).
    private int LarguraBadgeSituacao() => Math.Max(64, (int)Math.Round(64 * _escalaLista));

    private static void DefinirFonte(Control controle, float tamanhoBase, float escala, float minimo, float maximo, FontStyle estilo)
    {
        float tamanho = Math.Clamp(tamanhoBase * escala, minimo, maximo);
        if (Math.Abs(controle.Font.Size - tamanho) < 0.05f && controle.Font.Style == estilo)
        {
            return;
        }

        controle.Font = new Font("Segoe UI", tamanho, estilo);
    }

    private void AjustarBotoesLayout()
    {
        // Escala baseada no tamanho do card (mesma base do CargoForm: altura 596, largura 411).
        float escalaY = balancasCard.ClientSize.Height <= 0 ? 1f : balancasCard.ClientSize.Height / 596f;
        float escalaX = balancasCard.ClientSize.Width <= 0 ? 1f : balancasCard.ClientSize.Width / 411f;

        // ===================================================================
        // TAMANHO DO BOTAO "Nova Balanca"  ->  CONTROLE UNICO AQUI.
        // ALTURA: acompanha a do "Novo Cargo" do CargoForm: base 27 * escalaY.
        //   >> Para mudar a ALTURA, troque o numero 27.
        // LARGURA: adaptada ao conteudo (mede o texto) para o botao nao ficar
        //   largo demais. As "folgas" sao a margem esquerda do texto (onde fica o
        //   icone) e a margem direita.
        //   >> Para dar mais respiro, aumente o folgaDireita (16).
        // OBS: o tamanho do Form Designer e ignorado em runtime (AutoScaleMode.Font);
        //      quem define o tamanho final e ESTE metodo.
        // ===================================================================
        novoBalancaButtonPanel.Height = Math.Max(27, (int)Math.Round(27 * escalaY));

        int folgaDireita = Math.Max(14, (int)Math.Round(16 * escalaX));
        int larguraTextoNovo = TextRenderer.MeasureText(novoBalancaTextLabel.Text, novoBalancaTextLabel.Font).Width;
        novoBalancaTextLabel.Width = larguraTextoNovo + 2;
        novoBalancaButtonPanel.Width = novoBalancaTextLabel.Left + larguraTextoNovo + folgaDireita;

        novoBalancaIconLabel.Top = Math.Max(2, (novoBalancaButtonPanel.Height - novoBalancaIconLabel.Height) / 2);
        novoBalancaTextLabel.Top = Math.Max(2, (novoBalancaButtonPanel.Height - novoBalancaTextLabel.Height) / 2);

        // Margem 16 para a borda direita do botao alinhar com a caixa de busca e a lista (igual ao Cargo).
        novoBalancaButtonPanel.Left = Math.Max(16, balancasCard.ClientSize.Width - novoBalancaButtonPanel.Width - 16);

        // Topo do titulo e do botao escala junto com a busca/lista (cabecalho desce proporcionalmente).
        balancasTitleLabel.Top = (int)Math.Round(18 * escalaY);
        novoBalancaButtonPanel.Top = (int)Math.Round(16 * escalaY);

        // Titulo "Dados da Balanca": mesma posicao/escala do "Dados do Cargo". O LayoutDetailsCard do
        // CargoForm usa icone em (20,18) e titulo em (52,22), escalados por escalaX/escalaY.
        dadosTitleIconLabel.Left = (int)Math.Round(20 * escalaX);
        dadosTitleIconLabel.Top = (int)Math.Round(18 * escalaY);
        dadosTitleLabel.Left = (int)Math.Round(52 * escalaX);
        dadosTitleLabel.Top = (int)Math.Round(22 * escalaY);

        // Largura definida explicitamente (como o Cargo) para a caixa terminar com margem
        // dos dois lados, em vez de depender do Anchor (que pode esticar alem do card).
        const int lado = 16;
        int larguraConteudo = Math.Max(200, balancasCard.ClientSize.Width - (lado * 2));
        searchPanel.Left = lado;
        searchPanel.Width = larguraConteudo;
        listaHeaderPanel.Left = lado;
        listaHeaderPanel.Width = larguraConteudo;
        balancasFlowPanel.Left = lado;
        balancasFlowPanel.Width = larguraConteudo;
        // Sem padding interno: as linhas comecam no mesmo X do cabecalho (colunas alinhadas, como o Cargo).
        balancasFlowPanel.Padding = new Padding(0);

        // Cabecalho "Situacao" centralizado sobre a coluna do badge (mesma largura do badge,
        // com folga para nao cortar o texto quando a fonte escala).
        int badgeWHeader = LarguraBadgeSituacao();
        int larguraSituacaoHeader = Math.Max(90, badgeWHeader + 8);
        int centroBadge = listaHeaderPanel.ClientSize.Width - 14 - (badgeWHeader / 2);
        listaSituacaoHeaderLabel.Width = larguraSituacaoHeader;
        listaSituacaoHeaderLabel.Left = Math.Max(0, centroBadge - (larguraSituacaoHeader / 2));

        // Mantem as linhas com a largura do flow (re-estica no resize; o badge segue ancorado a direita).
        foreach (Control controle in balancasFlowPanel.Controls)
        {
            if (controle is Panel linha)
            {
                linha.Width = balancasFlowPanel.ClientSize.Width;
            }
        }

        searchPanel.Top = (int)Math.Round(60 * escalaY);
        searchPanel.Height = Math.Max(34, (int)Math.Round(34 * escalaY));
        listaHeaderPanel.Top = searchPanel.Bottom + (int)Math.Round(16 * escalaY);
        balancasFlowPanel.Top = listaHeaderPanel.Bottom + (int)Math.Round(2 * escalaY);
        int limiteInferiorLista = quantidadeBalancasLabel.Top - (int)Math.Round(6 * escalaY);
        balancasFlowPanel.Height = Math.Max(120, limiteInferiorLista - balancasFlowPanel.Top);

        // Centraliza verticalmente icone e texto da busca dentro do painel (igual ao Cargo).
        int alturaTexto = Math.Max(16, searchTextBox.PreferredHeight);
        searchTextBox.Top = Math.Max(2, (searchPanel.Height - alturaTexto) / 2);
        searchIconLabel.Top = Math.Max(2, (searchPanel.Height - searchIconLabel.Height) / 2);

        int largura = Math.Max(180, resumoCard.ClientSize.Width - 44);
        int baseY = Math.Max(330, resumoCard.ClientSize.Height - 130);
        salvarButton.SetBounds(22, baseY, largura, 34);
        salvarAlteracoesButton.SetBounds(22, baseY + 42, largura, 34);
        alterarSituacaoButton.SetBounds(22, baseY + 84, largura, 34);
    }

    private static void DefinirGrupoAtivo(Control grupo, bool ativo)
    {
        grupo.Visible = ativo;
        grupo.Enabled = ativo;
    }

    private bool ConfirmarIntegracaoBanco()
    {
        if (_integracaoBancoHabilitada) return true;

        MessageBox.Show(
            "Integração com banco está desabilitada temporariamente.",
            "Cadastro de Balança",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        return false;
    }

    private void ExibirResultado(ResultadoOperacao resultado)
    {
        MessageBox.Show(
            resultado.Mensagem,
            "Cadastro de Balança",
            MessageBoxButtons.OK,
            resultado.Sucesso ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private string ObterNomeSetor(long codigoSetor)
    {
        if (setorComboBox.DataSource is IEnumerable<SetorCadastro> setores)
        {
            return setores.FirstOrDefault(item => item.CodigoSetor == codigoSetor)?.NomeSetor ?? $"Setor {codigoSetor}";
        }

        return $"Setor {codigoSetor}";
    }

    private long ObterCodigoSetorSelecionado()
        => setorComboBox.SelectedValue switch
        {
            long valor => valor,
            int valor => valor,
            string valor when long.TryParse(valor, out long codigo) => codigo,
            _ => 0
        };

    private static int? ParseInteiro(string texto)
        => int.TryParse(texto.Trim(), out int valor) ? valor : null;

    private static decimal? ParseDecimal(string? texto)
        => decimal.TryParse(
            texto?.Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out decimal valor)
            ? valor
            : null;

    private static string NormalizarBusca(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        string normalizado = texto.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(normalizado.Length);
        foreach (char caractere in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(caractere));
            }
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
            case Keys.F6 when salvarAlteracoesButton.Visible && salvarAlteracoesButton.Enabled:
                salvarAlteracoesButton.PerformClick();
                return true;
            case Keys.F8 when alterarSituacaoButton.Visible && alterarSituacaoButton.Enabled:
                alterarSituacaoButton.PerformClick();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
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

    private enum ModoTela
    {
        Vazio,
        Novo,
        Edicao
    }
}
