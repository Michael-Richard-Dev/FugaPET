using FugaPET_Dev.Modelo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Terminal;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Tela.Teste;
using FugaPET_Dev.Tela;
using FugaPET_Dev.Tela.Comum;

using System.Runtime.InteropServices;

namespace FugaPET_Dev.Tela.Processo;

public partial class ProcessoEntradaProdutoForm : Form
{
    private enum EstadoVisualLocalEntrada
    {
        Pendente,
        Gravado
    }

    private enum EstadoVisualIntegracaoSap
    {
        AguardandoGravacaoLocal,
        BloqueadoSemPermissao,
        BloqueadoAmbiente,
        BloqueadoSapNaoConfigurado,
        BloqueadoIntegracaoInativa,
        BloqueadoEscritaDesabilitada,
        BloqueadoSemItens,
        LiberadoParaEnvio,
        Enviando,
        Enviado,
        Falha,
        Parcial
    }

    private enum FiltroItensEntrada
    {
        Todos,
        PendentesPesagem,
        PesadosLocalmente,
        PendentesSap,
        EnviadosSap,
        ErroSap,
        ComSaldo,
        SemSaldo
    }

    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fugapet.ico";
    private const string TipoPedidoNormal = "NB";
    private const string DescricaoPedidoNormal = "Pedido normal (NB)";
    private const string ColunaReimpressaoEtiqueta = "productionReprintColumn";
    private static readonly Color RowGreen = Color.FromArgb(238, 241, 245);
    private static readonly Color RowLight = Color.FromArgb(250, 251, 252);
    private static readonly Color StartActionHoverBorder = Color.FromArgb(34, 197, 94);
    private static readonly Color ReadWeightHoverBorder = Color.FromArgb(59, 130, 246);
    private static readonly Color DangerActionHoverBorder = Color.FromArgb(200, 78, 10);
    private static readonly Color EnabledLegendTextColor = Color.FromArgb(229, 231, 235);
    private static readonly Color DisabledLegendTextColor = Color.FromArgb(120, 126, 136);
    private static readonly Color SidePanelDefaultColor = Color.FromArgb(45, 49, 56);
    private static readonly Color SidePanelActiveColor = Color.FromArgb(22, 101, 52);
    private static readonly Color ActionEnabledColor = Color.FromArgb(34, 166, 82);
    private static readonly Color ActionDisabledColor = Color.FromArgb(82, 87, 96);
    private static readonly Color ReadingStatusInactiveColor = Color.FromArgb(220, 53, 69);
    private static readonly Color ReadingStatusActiveColor = Color.FromArgb(34, 166, 82);
    private readonly BalancaLeituraServico _balancaLeituraServico;
    private readonly ImpressaoEntradaServico _impressaoEntrada;
    private bool _isStartActionHovering;
    private bool _isReadWeightHovering;
    private Panel? _hoveredDangerActionPanel;
    private bool _isProductionStarted;
    private bool _finalizandoPesagem;
    private bool _isReadingWeight;
    private int _nextProductionCode = 1;
    private Task? _productionDevicesWarmUpTask;
    private readonly SemaphoreSlim _consultaPedidoGate = new(1, 1);
    private CancellationTokenSource? _consultaPedidoCts;
    private Task _consultaPedidoTask = Task.CompletedTask;
    private string _numeroPedidoCarregado = string.Empty;
    private ContextoTerminalLocal? _contextoTerminal;
    private long? _idSetorSelecionado;
    private long? _idBalancaSelecionada;
    private long? _idTaraSelecionada;
    private long? _idEtiquetaSelecionada;

    // H9: os servicos vem do controller (Form nao instancia servicos concretos nem fala direto com
    // o SAP). A escolha mock/real e da fabrica, encapsulada no controller/IntegracaoEntradaSapServico.
    private readonly global::FugaPET_Dev.Controle.Processo.EntradaProdutoController _controller;
    private readonly EntradaProdutoServico _entradaServico;
    private readonly Dictionary<long, PedidoCompraSapItem> _itensCarregadosPorCodigo = [];

    // Tolerancia de recebimento por MATERIAL (incremento 069), carregada uma vez por pedido.
    // Chave = material; ausente = nao cadastrada, o que BLOQUEIA (decisao do usuario em 23/11/2026).
    // Consultar a cada pesagem iria ao banco no meio do ritmo da balanca, e uma falha de rede no
    // meio do recebimento viraria bloqueio de material que ESTA cadastrado.
    private readonly Dictionary<string, Modelo.Cadastro.ToleranciaRecebimentoMaterial> _toleranciasPorMaterial =
        new(StringComparer.OrdinalIgnoreCase);

    // Falso enquanto as tolerancias nao puderam ser lidas (banco fora, cadastro indisponivel). Nesse
    // caso a tela NAO bloqueia por "sem cadastro": nao saber se existe tolerancia e diferente de saber
    // que nao existe, e parar a producao por indisponibilidade do cadastro seria a guarda sem valvula
    // de saida que ja prendeu a operacao antes.
    private bool _toleranciasDisponiveis;

    // Composto PREGUICOSAMENTE: a tela de Entrada abre em ambiente sem banco de cadastro, e falhar
    // na composicao aqui impediria a tela de abrir por causa de um caminho que talvez nem seja usado.
    private Servicos.Seguranca.AutorizacaoToleranciaServico? _autorizacaoTolerancia;

    private Servicos.Seguranca.AutorizacaoToleranciaServico ObterAutorizacaoToleranciaServico()
        => _autorizacaoTolerancia ??= Controle.FabricaControladoresCadastro.CriarAutorizacaoToleranciaServico();
    private readonly Dictionary<long, List<EntradaProdutoPesagem>> _leiturasPorItem = [];
    private readonly Dictionary<long, global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro> _tarasPorItem = [];
    private IReadOnlyList<PedidoCompraSapItem> _itensPedidoCarregados = [];
    private long? _codigoLancamentoPersistido;

    // Fase 4G: lotes finalizados em memória, mas a gravação local ainda não teve sucesso (falha/cancelamento).
    // Enquanto true, o botão Parar continua habilitado para RETRY e nenhuma nova pesagem/lote é permitida.
    private bool _lotesFinalizadosAguardandoPersistencia;

    // Seam de persistência local por lotes (§2): produção usa o Controller; testes injetam delegate controlado.
    // Retorna o resultado para distinguir gravação inédita × recuperação idempotente. A Form NÃO acessa o
    // Repository diretamente e NÃO duplica Service/Controller.
    private readonly Func<EntradaProdutoLancamentoComLotesPersistencia, CancellationToken, Task<ResultadoPersistenciaEntradaComLotes>> _registrarOuRecuperarLancamentoComLotes;

    // Seam de diagnóstico de prontidão SAP (§5): produção delega ao Controller; testes injetam delegate
    // controlado. A Form NUNCA acessa Repository/SAP diretamente — apenas pede o diagnóstico e reflete o
    // resultado (botão/chip/tooltip) na UI. Nenhum POST ao SAP acontece por aqui.
    private readonly Func<long?, CancellationToken, Task<DiagnosticoEnvioSapEntrada>> _diagnosticarEnvioSapEntrada;

    private readonly CancellationTokenSource _fechamentoTelaCts = new();
    private readonly global::FugaPET_Dev.Controle.Cadastro.TaraController _taraController;
    private Task _envioSapTask = Task.CompletedTask;
    private readonly ToolTip _envioSapToolTip = new();
    // Cerimônia de habilitação da escrita SAP 101 (12E-E-B). Construção sem efeito colateral (auditoria lazy).
    // 12G-B: acionada como detalhe interno do botão de envio original (sem botão separado).
    private readonly global::FugaPET_Dev.Servicos.IntegracaoSap.HabilitacaoEscritaSapServico _habilitacaoEscritaSap = new();
    private readonly ContextMenuStrip _filtroItensPedidoMenu = new();
    private Label? _estadoVazioItensLabel;
    private FiltroItensEntrada _filtroItensAtual = FiltroItensEntrada.Todos;
    private EstadoVisualIntegracaoSap _estadoIntegracaoSapAtual = EstadoVisualIntegracaoSap.AguardandoGravacaoLocal;
    private bool _acessoDiretoValidado;
    private bool _restaurandoPedidoOperacaoLotes;
    private readonly Func<IWin32Window, ModoEntradaMaterial, DadosLoteEntrada?> _solicitarDadosLote;

    // Tarefa Entrada 24.1 (Ajuste 3): modo operacional (Matéria-Prima × Químicos) + configuração da tela.
    private readonly global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial _modoEntrada;
    private readonly global::FugaPET_Dev.Modelo.Processo.ConfiguracaoTelaEntradaMaterial _configuracaoTela;

    public ProcessoEntradaProdutoForm()
        : this(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.MateriaPrima)
    {
    }

    public ProcessoEntradaProdutoForm(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo)
        : this(new global::FugaPET_Dev.Controle.Processo.EntradaProdutoController(), modo)
    {
    }

    internal ProcessoEntradaProdutoForm(
        global::FugaPET_Dev.Controle.Processo.EntradaProdutoController controller,
        global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo =
            global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.MateriaPrima,
        Func<IWin32Window, ModoEntradaMaterial, DadosLoteEntrada?>? solicitarDadosLote = null,
        Func<EntradaProdutoLancamentoComLotesPersistencia, CancellationToken, Task<ResultadoPersistenciaEntradaComLotes>>? registrarOuRecuperarLancamentoComLotes = null,
        Func<long?, CancellationToken, Task<DiagnosticoEnvioSapEntrada>>? diagnosticarEnvioSapEntrada = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _modoEntrada = modo;
        _solicitarDadosLote = solicitarDadosLote ?? SolicitarDadosLotePadrao;
        // Produção: delega ao Controller (fronteira de persistência). Testes: delegate controlado sem banco.
        _registrarOuRecuperarLancamentoComLotes =
            registrarOuRecuperarLancamentoComLotes ?? _controller.RegistrarOuRecuperarLancamentoComLotesAsync;
        // Produção: delega ao Controller (diagnóstico de prontidão SAP). Testes: delegate controlado sem SAP.
        _diagnosticarEnvioSapEntrada =
            diagnosticarEnvioSapEntrada ?? _controller.DiagnosticarEnvioSapEntradaAsync;
        _configuracaoTela = global::FugaPET_Dev.Modelo.Processo.ConfiguracaoTelaEntradaMaterialFactory.Criar(modo);
        _entradaServico = _controller.EntradaProduto;
        _balancaLeituraServico = _controller.BalancaLeitura;
        _impressaoEntrada = _controller.Impressao;
        _taraController = _controller.Tara;
        InitializeComponent();
        AplicarConfiguracaoModoEntrada();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        if (_controller.Sap.EhSimulado)
        {
            if (!global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.PodeUsarDadosSimulados())
            {
                global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.BloquearTelaSimulada(this);
                return;
            }

            global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.Aplicar(headerSubtitleLabel);
            global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.AplicarFaixa(this);
        }
        AplicarContextoTerminalAutomatico();
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "aguardando gravação local");
        ConfigureResponsiveSummaryCards();
        ConfigureProductionSearchBox();
        ConfigurarBuscaFiltroEstadoVazioEntrada();
        ConfigurarComboPedidos();
        ConfigureSideActionButtonIcons();
        if (_controller.Sap.EhSimulado)
        {
            LoadMockData();
        }
        ApplyGridStyle(materialDataGridView);
        ApplyGridStyle(productionDataGridView);
        ConfigurarColunaReimpressaoEtiqueta();
        ConfigureProductionGridFooter();
        productionDataGridView.CellMouseDown += ProductionDataGridView_CellMouseDown;
        productionDataGridView.CellClick += ProductionDataGridView_CellClick;
        productionDataGridView.SelectionChanged += ProductionDataGridView_SelectionChanged;
        productionDataGridView.CellDoubleClick += ProductionDataGridView_CellDoubleClick;
        ConfigureStartActionHoverEffect();
        ConfigureProductionActions();
        ConfigureFooterDate();
        UpdateProductionCounters();
        KeyPreview = true;
        Shown += ProcessoProdutoAcabadoForm_Shown;
        FormClosing += ProcessoProdutoAcabadoForm_FormClosing;
        FormClosed += (_, _) =>
        {
            _consultaPedidoCts?.Cancel();
            _fechamentoTelaCts.Cancel();
            _controller.LimparOperacaoComLotes();
        };

        // GATE 081: melhoria visual/legibilidade — SOMENTE no modo Recebimento de Mercadoria e por ÚLTIMO,
        // após toda a configuração de fontes/estilos, para não ser sobrescrita.
        AplicarLegibilidadeRecebimentoMercadoria();
    }

    // GATE 081: identidade FugaPET + ícone laranja/traços brancos + status SAP destacado + tipografia 2.0x.
    // Escopo estrito: só quando a tela é a porta única "Recebimento de Mercadoria". Só apresentação — nenhuma
    // regra de negócio, SAP, persistência, filtro ROH/HIBE, lote/tara ou Excluir Pesagem é alterada aqui.
    private void AplicarLegibilidadeRecebimentoMercadoria()
    {
        if (_modoEntrada != global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.RecebimentoMercadoria)
        {
            return;
        }

        SuspendLayout();

        // §4 Identidade FugaPET (substitui o branding local Fuga Couros pelo logo FugaPET aprovado). O logo é
        // renderizado em BRANCO sobre o laranja (não cinza), preservando a transparência; a barrinha divisória
        // do cabeçalho também vai a branco.
        // GATE 127A: o mesmo helper usado pelas demais telas. O trecho era privado daqui, e por isso
        // nenhuma outra tela tinha a identidade FugaPET.
        global::FugaPET_Dev.Tela.Comum.IdentidadeVisualFugaPet.AplicarNoCabecalho(
            companyLogoPictureBox, headerDividerLabel, logoSaLabel);

        // §5 Ícone do título: fundo LARANJA FugaPET + desenho de TRAÇOS BRANCOS (asset aprovado). Sem vermelho/rosa.
        headerTitleIconPanel.BackColor = Color.Transparent;
        headerTitleIconPanel.FillColor = Color.Transparent;
        headerTitleIconPictureBox.Image = ExtrairTracosBrancos(global::FugaPET_Dev.Properties.Resources.production_title_icon);
        headerTitleIconPictureBox.Tag = "FUGAPET_ICON_BRANCO";

        // §6 Status SAP com destaque ALTO: badge preenchido (laranja) + texto branco. O cálculo/estado SAP
        // (AtualizarEstadoVisualIntegracaoSap) só troca a cor do "dot" e o texto — este realce não é sobrescrito.
        sapStatusPanel.FillColor = LegibilidadeRecebimentoMercadoria.LaranjaFugaPetEscuro;
        sapStatusPanel.BorderColor = LegibilidadeRecebimentoMercadoria.LaranjaFugaPet;
        sapStatusLabel.ForeColor = Color.White;
        sapStatusLabel.Tag = "SAP_STATUS_DESTAQUE";

        // FIX4: a ampliação 2.0x é aplicada SOMENTE à ÁREA INTERNA (cards/grid/painel direito). O CABEÇALHO
        // (customTitleBarPanel) e o RODAPÉ (footerBar) permanecem no tamanho PADRÃO das demais telas — não são
        // escalados nem têm suas linhas de altura crescidas. Só a identidade FugaPET/ícone/status SAP acima é
        // aplicada ao cabeçalho (não altera tamanho).
        LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(rootTableLayoutPanel, LegibilidadeRecebimentoMercadoria.Escala);
        LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(sidePanel, LegibilidadeRecebimentoMercadoria.Escala);
        LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(rootTableLayoutPanel);
        LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(sidePanel);

        // Orçamento de altura: só a faixa interna dos cards (linha 0 ABSOLUTA do rootTableLayoutPanel) cresce
        // até caber o conteúdo 2.0x; a grade (linha Percent) cede o espaço. Cabeçalho/rodapé ficam no padrão.
        AjustarOrcamentoAlturaRecebimento();

        // Painel lateral (Status/Qtde/Peso/Iniciar Leitura): alarga a coluna, alarga o conteúdo (empurrando pra
        // dentro), aumenta o botão e reempilha para a fonte 2.0x não quebrar/cortar.
        AjustarPainelLateralRecebimento();

        // Grade: alarga as colunas fixas para caber os cabeçalhos 2.0x (Material/Quantidade/Unidade/Peso/Pesagens)
        // e reduz o peso da coluna "Descrição do Material" (Fill) para ela não dominar — assim todas aparecem.
        AjustarColunasGridRecebimento();

        // Toolbar acima da grade: aumenta busca + botões (harmonia com a fonte 2.0x) e os traz para a esquerda,
        // logo após o título, alinhados verticalmente.
        AjustarToolbarGridRecebimento();

        // §8/§9/§13 Sem AutoScroll: a janela permanece Maximizada, cabendo integralmente na área de trabalho
        // (1920x1080 / 100%). A grade é a região elástica; nenhuma barra de rolagem de janela é necessária.
        AutoScroll = false;
        WindowState = FormWindowState.Maximized;

        ResumeLayout(true);
        PerformLayout();
    }

    // GATE 127A: CarregarLogoFugaPetBranco foi promovido a IdentidadeVisualFugaPet.CarregarLogoBranco,
    // em Tela/Comum. A copia privada daqui foi removida para nao haver duas verdades sobre o asset.

    private static Bitmap ExtrairTracosBrancos(Bitmap origem)
    {
        Bitmap resultado = new(origem.Width, origem.Height);

        for (int x = 0; x < origem.Width; x++)
        {
            for (int y = 0; y < origem.Height; y++)
            {
                Color pixel = origem.GetPixel(x, y);
                resultado.SetPixel(
                    x,
                    y,
                    pixel.A > 0 && pixel.G >= 160 && pixel.B >= 160
                        ? Color.FromArgb(pixel.A, Color.White)
                        : Color.Transparent);
            }
        }

        return resultado;
    }

    // GATE 082-FIX3: redistribui a altura reservando o necessário para as faixas de conteúdo e deixando a grade
    // absorver o restante. Os tetos FIXOS reais são linhas ABSOLUTAS: tableLayoutPanel2 linha 0 (cabeçalho, 52),
    // tableLayoutPanel2 linha 2 (rodapé, 38) e rootTableLayoutPanel linha 0 (cards, 86). Medimos pela BASE dos
    // controles-filho (já crescidos com a fonte 2.0x), não pelo PreferredSize do painel (que devolve ~1x quando
    // AutoSize=false). Só CRESCE. A linha Percent (grade + painel direito) cede o espaço.
    private void AjustarOrcamentoAlturaRecebimento()
    {
        // CARDS — linha 0 ABSOLUTA de rootTableLayoutPanel (faixa interna). A grade (linha Percent) cede o espaço;
        // o cabeçalho/rodapé ficam no padrão.

        // O container dos 4 cards (tableLayoutPanel3) tinha Anchor Top|Left|Right com ALTURA FIXA (69px), então
        // não crescia quando a faixa aumentava — as caixas ficavam curtas e o texto transbordava por baixo.
        // Anexa Bottom para o container esticar na vertical e preencher toda a faixa dos cards.
        tableLayoutPanel3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        // A linha dos cards ocupa 100% da altura (a 2ª linha do tableLayoutPanel3 está vazia → vai a 0), assim as
        // caixas (RoundedPanel Dock=Fill) crescem e contornam legenda + valor por inteiro.
        if (tableLayoutPanel3.RowStyles.Count >= 2)
        {
            tableLayoutPanel3.RowStyles[0].SizeType = SizeType.Percent;
            tableLayoutPanel3.RowStyles[0].Height = 100f;
            tableLayoutPanel3.RowStyles[1].SizeType = SizeType.Percent;
            tableLayoutPanel3.RowStyles[1].Height = 0f;
        }

        int alturaCards = 0;
        foreach (Control card in new Control[]
                 { productionOrderShadowPanel, lotCardPanel, stepCardPanel, finishedProductCardPanel })
        {
            int baseCard = 0;
            foreach (Control filho in card.Controls)
            {
                baseCard = Math.Max(baseCard, filho.Bottom);
            }
            alturaCards = Math.Max(alturaCards, baseCard + card.Padding.Bottom + 12);
        }

        // Piso confortável: a faixa dos cards ~dobra para acomodar a fonte 2.0x (o grid diminui).
        alturaCards = Math.Max(alturaCards, 160);
        LegibilidadeRecebimentoMercadoria.CrescerTetoDeConteudo(rootTableLayoutPanel, 0, alturaCards);

        // Harmoniza o conteúdo interno de cada card: ícone no topo-direito; legenda + valor(es) alargados (não
        // cortam horizontal), com altura garantida (não cortam embaixo) e o grupo centralizado verticalmente na
        // caixa — reaplicado no resize (vale também ao maximizar).
        foreach (Control card in new Control[]
                 { productionOrderShadowPanel, lotCardPanel, stepCardPanel, finishedProductCardPanel })
        {
            HarmonizarConteudoCard(card);
        }
    }

    private void AjustarToolbarGridRecebimento()
    {
        const int y = 6;
        const int altura = 34;
        const int gap = 12;
        const int margemDir = 16;

        // Remove o traço laranja de sublinhado abaixo de "ITENS DO PEDIDO".
        productionReadingsUnderlineLabel.Visible = false;

        // Título "ITENS DO PEDIDO" à esquerda, sem cortar, centralizado na linha.
        productionReadingsTitleLabel.Left = 34;
        int largTitulo = productionReadingsTitleLabel.PreferredSize.Width;
        if (largTitulo > productionReadingsTitleLabel.Width)
        {
            productionReadingsTitleLabel.Width = largTitulo;
        }
        productionReadingsTitleLabel.Top = y + Math.Max(0, (altura - productionReadingsTitleLabel.Height) / 2);

        // Dimensiona busca + botões (mesmos tamanhos), mas ENCOSTA o grupo na direita da caixa.
        productionSearchPanel.Size = new Size(340, altura);
        productionSearchTextBox.Height = Math.Min(productionSearchTextBox.PreferredSize.Height, altura - 8);
        productionSearchTextBox.Top = Math.Max(2, (altura - productionSearchTextBox.Height) / 2);

        productionFilterButton.Height = altura;
        productionFilterButton.Width = Math.Max(productionFilterButton.Width, TextRenderer.MeasureText(productionFilterButton.Text, productionFilterButton.Font).Width + 44);
        productionActionsButton.Height = altura;
        productionActionsButton.Width = Math.Max(productionActionsButton.Width, TextRenderer.MeasureText(productionActionsButton.Text, productionActionsButton.Font).Width + 44);

        // Alinha à direita: [busca] [Filtros] [Enviar] — todos ancorados à direita (acompanham o resize).
        int direita = productionReadingsPanel.ClientSize.Width - margemDir;
        PosicionarADireita(productionActionsButton, ref direita, y, gap);
        PosicionarADireita(productionFilterButton, ref direita, y, gap);
        PosicionarADireita(productionSearchPanel, ref direita, y, gap);
    }

    private static void PosicionarADireita(Control controle, ref int direita, int y, int gap)
    {
        controle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        controle.Location = new Point(direita - controle.Width, y);
        direita = controle.Left - gap;
    }

    private void AjustarColunasGridRecebimento()
    {
        Font fonteHeader = productionDataGridView.ColumnHeadersDefaultCellStyle.Font ?? productionDataGridView.Font;

        foreach (DataGridViewColumn coluna in productionDataGridView.Columns)
        {
            if (!coluna.Visible || coluna.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
            {
                continue; // a coluna "Descrição" (Fill) permanece elástica e cede a largura.
            }

            // Largura mínima para o cabeçalho 2.0x caber (texto medido + folga p/ borda/ordenação).
            int larguraHeader = TextRenderer.MeasureText(coluna.HeaderText, fonteHeader).Width + 32;
            if (larguraHeader > coluna.Width)
            {
                coluna.Width = larguraHeader;
            }
        }

        // "Descrição do Material" deixa de dominar: menor peso de preenchimento (encolhe, mas continua a maior).
        if (productionProductColumn.AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
        {
            productionProductColumn.FillWeight = 120F;
        }
    }

    private static void HarmonizarConteudoCard(Control card)
    {
        void Reflow()
        {
            const int margem = 16;
            const int gap = 4;
            int largura = card.ClientSize.Width;
            int altura = card.ClientSize.Height;
            if (largura <= 0 || altura <= 0)
            {
                return;
            }

            Control? icone = null;
            List<Control> conteudo = [];
            foreach (Control filho in card.Controls)
            {
                if (filho.Name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    icone = filho;
                }
                else
                {
                    conteudo.Add(filho);
                }
            }
            if (conteudo.Count == 0)
            {
                return;
            }

            conteudo.Sort((a, b) => a.Top.CompareTo(b.Top));

            // Chip de ícone (RoundedPanel vermelho-claro) vira "resquício" visual nos cards — oculta.
            if (icone is not null)
            {
                icone.Visible = false;
            }

            int larguraUtil = largura - (margem * 2);

            // Empilha legenda + valor(es): largura útil (sem cortar horizontal) e altura preferida (sem cortar embaixo).
            int total = 0;
            int[] alturas = new int[conteudo.Count];
            for (int i = 0; i < conteudo.Count; i++)
            {
                Control c = conteudo[i];
                c.Left = margem;
                if (c.Width < larguraUtil)
                {
                    c.Width = larguraUtil;
                }
                int preferida = c.PreferredSize.Height;
                if (preferida > c.Height)
                {
                    c.Height = preferida;
                }
                alturas[i] = c.Height;
                total += c.Height + gap;
            }
            total -= gap;

            // Centraliza o grupo verticalmente na caixa.
            int y = Math.Max(margem, (altura - total) / 2);
            for (int i = 0; i < conteudo.Count; i++)
            {
                conteudo[i].Top = y;
                y += alturas[i] + gap;
            }
        }

        card.Resize += (_, _) => Reflow();
        Reflow();
    }

    // Painel lateral com fonte 2.0x: alarga a coluna e o conteúdo, aumenta a altura do botão Iniciar Leitura e
    // reempilha os itens (todos são absolutos, sem Anchor). Só CRESCE larguras/alturas — empurra o texto pra dentro.
    private const int LarguraPainelLateralRecebimento = 320;
    private const int MargemPainelLateralRecebimento = 18;
    private const int AlturaBotaoLateralRecebimento = 64;

    private void AjustarPainelLateralRecebimento()
    {
        // (1) Alarga a coluna do painel lateral (coluna 1 ABSOLUTA de tableLayoutPanel2).
        if (tableLayoutPanel2.ColumnStyles.Count > 1 &&
            tableLayoutPanel2.ColumnStyles[1].SizeType == SizeType.Absolute &&
            tableLayoutPanel2.ColumnStyles[1].Width < LarguraPainelLateralRecebimento)
        {
            tableLayoutPanel2.ColumnStyles[1].Width = LarguraPainelLateralRecebimento;
        }

        int larguraUtil = LarguraPainelLateralRecebimento - (MargemPainelLateralRecebimento * 2);

        void Alargar(Control c)
        {
            c.Left = MargemPainelLateralRecebimento;
            if (c.Width < larguraUtil)
            {
                c.Width = larguraUtil;
            }
        }

        // (2) Alarga títulos/cards e empurra pra dentro; alarga os rótulos internos (valores) dos cards.
        Alargar(sideStatusTitleLabel);
        Alargar(statusCard);
        Alargar(groupBox4);
        AlargarFilhos(statusCard);
        AlargarFilhos(groupBox4);

        // (3a) Card de STATUS (vermelho): valor + ícone na linha de cima, dica logo abaixo — sem sobrepor/cortar.
        statusCardIcon.Top = 12;
        statusCardIcon.Height = Math.Max(statusCardIcon.Height, statusCardIcon.PreferredSize.Height);
        statusValueLabel.Top = 12;
        statusValueLabel.Left = statusCardIcon.Right + 8;
        statusValueLabel.Height = Math.Max(statusValueLabel.Height, statusValueLabel.PreferredSize.Height);
        statusHintLabel.Left = 13;
        statusHintLabel.Width = statusCard.ClientSize.Width - statusHintLabel.Left - 8;
        statusHintLabel.Height = Math.Max(statusHintLabel.Height, statusHintLabel.PreferredSize.Height);
        statusHintLabel.Top = Math.Max(statusCardIcon.Bottom, statusValueLabel.Bottom) + 8;

        // (3b) Alinha o bloco "Qtde Total" IGUAL ao "Peso" (centralizado): espelha Left/Width/Anchor dos rótulos.
        boxesCaptionLabel.Anchor = packagesCaptionLabel.Anchor;
        boxesCaptionLabel.Left = packagesCaptionLabel.Left;
        boxesCaptionLabel.Width = packagesCaptionLabel.Width;
        boxesCounterLabel.Anchor = packagesCounterLabel.Anchor;
        boxesCounterLabel.Left = packagesCounterLabel.Left;
        boxesCounterLabel.Width = packagesCounterLabel.Width;

        // (3c) Centraliza VERTICALMENTE o par legenda+valor em cada metade (Qtde e Peso), inclusive ao redimensionar.
        CentralizarVerticalmente(weightSummaryForecastPanel, boxesCaptionLabel, boxesCounterLabel);
        CentralizarVerticalmente(weightSummaryUsedPanel, packagesCaptionLabel, packagesCounterLabel);

        // (3d) Cresce a altura dos cards para o conteúdo 2.0x (pela base dos filhos já reempilhados/ampliados).
        statusCard.Height = Math.Max(statusCard.Height, FundoDosFilhos(statusCard) + 14);
        groupBox4.Height = Math.Max(groupBox4.Height, FundoDosFilhos(groupBox4) + 14);

        // (4) Botões maiores (cabem "INICIAR LEITURA" 2.0x) e na largura do painel.
        foreach (Control botao in new Control[] { iniciarLeituraButton, lerEtiquetaButton, leituraManualButton })
        {
            botao.Left = MargemPainelLateralRecebimento;
            botao.Width = larguraUtil;
            botao.Height = AlturaBotaoLateralRecebimento;
        }

        // (5) Reempilha verticalmente. Os 3 botões são pré-empilhados SEMPRE (mesmo ocultos): "Iniciar/Parar
        // Leitura" começa visível, mas "Ler Etiqueta"/"Digitar Peso" só aparecem após iniciar — se não fossem
        // posicionados aqui, surgiriam no lugar antigo e sobrepunham. A troca de estado só altera Visible.
        int y = 16;
        sideStatusTitleLabel.Top = y; y = sideStatusTitleLabel.Bottom + 8;
        statusCard.Top = y;           y = statusCard.Bottom + 12;
        groupBox4.Top = y;            y = groupBox4.Bottom + 16;
        iniciarLeituraButton.Top = y; y = iniciarLeituraButton.Bottom + 10;
        lerEtiquetaButton.Top = y;    y = lerEtiquetaButton.Bottom + 10;
        leituraManualButton.Top = y;
    }

    private static void AlargarFilhos(Control pai)
    {
        int largura = pai.ClientSize.Width;
        foreach (Control filho in pai.Controls)
        {
            int disponivel = largura - filho.Left - 8;
            if (disponivel > filho.Width)
            {
                filho.Width = disponivel;
            }
        }
    }

    private static int FundoDosFilhos(Control pai)
    {
        int fundo = 0;
        foreach (Control filho in pai.Controls)
        {
            fundo = Math.Max(fundo, filho.Bottom);
        }
        return fundo;
    }

    // Centraliza verticalmente o par (legenda em cima, valor embaixo) dentro do painel — reaplica no resize.
    private static void CentralizarVerticalmente(Control painel, Control legenda, Control valor)
    {
        void Reposicionar()
        {
            const int espaco = 2;
            int total = legenda.Height + espaco + valor.Height;
            int topo = Math.Max(0, (painel.ClientSize.Height - total) / 2);
            legenda.Top = topo;
            valor.Top = legenda.Bottom + espaco;
        }

        painel.Resize += (_, _) => Reposicionar();
        Reposicionar();
    }

    // Tinge a imagem inteiramente de BRANCO preservando o canal alfa (silhueta branca) — usado para o logo
    // FugaPET aparecer em branco sobre o cabeçalho laranja, em vez de cinza.
    private static Image TingirDeBranco(Image origem)
    {
        Bitmap destino = new(origem.Width, origem.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using Graphics grafico = Graphics.FromImage(destino);
        System.Drawing.Imaging.ColorMatrix matriz = new(new[]
        {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 1, 1, 1, 0, 1 }
        });
        using System.Drawing.Imaging.ImageAttributes atributos = new();
        atributos.SetColorMatrix(matriz);
        grafico.DrawImage(
            origem,
            new Rectangle(0, 0, destino.Width, destino.Height),
            0, 0, origem.Width, origem.Height,
            GraphicsUnit.Pixel,
            atributos);
        return destino;
    }

    private static DadosLoteEntrada? SolicitarDadosLotePadrao(IWin32Window owner, ModoEntradaMaterial modoEntrada)
    {
        using EntradaProdutoDadosLoteForm form = new(modoEntrada);
        return form.ShowDialog(owner) == DialogResult.OK
            ? form.DadosConfirmados
            : null;
    }

    private void AplicarContextoTerminalAutomatico()
    {
        try
        {
            _contextoTerminal = EstadoTerminalLocalAtual.Contexto;
        }
        catch
        {
            _contextoTerminal = null;
        }

        long? idSetorUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdSetorPadrao;
        _idSetorSelecionado = idSetorUsuario ?? _contextoTerminal?.IdSetorPadrao;
        _idBalancaSelecionada = _contextoTerminal?.IdBalancaPadrao;
        _idTaraSelecionada = _contextoTerminal?.IdTaraPadrao;
        _idEtiquetaSelecionada = _contextoTerminal?.IdEtiquetaPadrao;

        string nomeTerminal = string.IsNullOrWhiteSpace(_contextoTerminal?.NomeTerminal)
            ? Environment.MachineName
            : _contextoTerminal!.NomeTerminal;
        cellTerminalText.Text = $"Terminal:  {nomeTerminal}";

        if (_idBalancaSelecionada.HasValue)
        {
            balanceTextBox.Text = _idBalancaSelecionada.Value.ToString();
        }

        statusLabel.Text = MontarResumoSelecaoAutomatica();
    }

    private string MontarResumoSelecaoAutomatica()
    {
        return
            $"Selecao automatica -> Setor: {FormatarId(_idSetorSelecionado)}, " +
            $"Balanca: {FormatarId(_idBalancaSelecionada)}, " +
            $"Tara: {FormatarId(_idTaraSelecionada)}, " +
            $"Etiqueta: {FormatarId(_idEtiquetaSelecionada)}.";
    }

    private static string FormatarId(long? valor)
    {
        return valor.HasValue ? valor.Value.ToString() : "Nao definido";
    }

    private static bool PossuiPermissaoEntrada(string acao)
        => AutorizacaoEntradaProdutoServico.PossuiPermissao(acao);

    // 1) verifica permissao; 2) se negado, AUDITA (await, seguro); 3) mostra mensagem amigavel;
    // 4) retorna true para o chamador abortar a acao.
    private async Task<bool> BloquearAcaoSemPermissaoAsync(string acao, string descricaoAcao)
    {
        if (PossuiPermissaoEntrada(acao))
        {
            return false;
        }

        await global::FugaPET_Dev.Tela.Comum.AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            AutorizacaoServico.ModuloProcesso, PermissoesSistema.Rotinas.EntradaProduto, acao, descricaoAcao, "ProcessoEntradaProdutoForm");

        string mensagem = $"Usuario sem permissao para {descricaoAcao.ToLowerInvariant()} na entrada de produto.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private async Task<bool> BloquearImpressaoSemPermissaoAsync(
        string acao,
        string descricaoAcao)
    {
        if (AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(acao))
        {
            return false;
        }

        await global::FugaPET_Dev.Tela.Comum.AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
            PermissoesSistema.Modulos.Etiqueta,
            PermissoesSistema.Rotinas.ImpressaoEtiqueta,
            acao,
            descricaoAcao,
            "ProcessoEntradaProdutoForm");

        string mensagem = $"Usuario sem permissao para {descricaoAcao.ToLowerInvariant()}.";
        statusLabel.Text = mensagem;
        MessageBox.Show(mensagem, "Acesso negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private System.Windows.Forms.Timer? _footerClockTimer;

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

    private void UpdateStepCardDate(DateTime now, System.Globalization.CultureInfo culture)
    {
        stepLabel.Text = now.ToString("dd/MM/yyyy", culture);
    }


    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void ConfigureCustomTitleBar()
    {
        customTitleBarPanel.MouseDown += CustomTitleBar_MouseDown;
        companyLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;
        menuHeaderLabel.Click += (_, _) => ReturnToLeituraProducao();

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(160, 55, 5));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(160, 55, 5));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(160, 55, 5));
    }

    private void AtualizarEstadoVisualLocal(EstadoVisualLocalEntrada estado, string detalhe)
    {
        statusLabel.ForeColor = estado == EstadoVisualLocalEntrada.Gravado
            ? Color.FromArgb(34, 166, 82)
            : Color.FromArgb(180, 83, 9);
        statusLabel.Text = estado == EstadoVisualLocalEntrada.Gravado
            ? $"LOCAL GRAVADO — {detalhe}"
            : $"LOCAL PENDENTE — {detalhe}";
    }

    private void AtualizarEstadoVisualIntegracaoSap(
        EstadoVisualIntegracaoSap estado,
        string? detalhe = null)
    {
        _estadoIntegracaoSapAtual = estado;
        (string texto, Color cor) = estado switch
        {
            EstadoVisualIntegracaoSap.LiberadoParaEnvio =>
                ("SAP HML: LIBERADO PARA ENVIO", Color.FromArgb(34, 197, 94)),
            EstadoVisualIntegracaoSap.Enviando =>
                ("SAP HML: ENVIANDO", Color.FromArgb(59, 130, 246)),
            EstadoVisualIntegracaoSap.Enviado =>
                ("SAP HML: ENVIADO", Color.FromArgb(34, 197, 94)),
            EstadoVisualIntegracaoSap.Falha =>
                ("SAP HML: FALHA", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.Parcial =>
                ("SAP HML: PARCIAL", Color.FromArgb(249, 115, 22)),
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal =>
                ("SAP HML: AGUARDANDO GRAVAÇÃO LOCAL", Color.FromArgb(250, 204, 21)),
            // O diagnóstico já distingue SEIS motivos de bloqueio; antes todos caíam no
            // genérico abaixo e o operador não tinha como saber qual era. O motivo
            // detalhado continua no tooltip do chip — mas o tooltip do BOTÃO nunca
            // aparece, porque o botão está desabilitado e no WinForms controle
            // desabilitado não dispara evento de mouse. Daí nomear cada estado aqui.
            EstadoVisualIntegracaoSap.BloqueadoAmbiente =>
                ("SAP HML: AMBIENTE NÃO LIBERADO", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.BloqueadoSemPermissao =>
                ("SAP HML: SEM PERMISSÃO", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.BloqueadoIntegracaoInativa =>
                ("SAP HML: INTEGRAÇÃO INATIVA", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.BloqueadoSapNaoConfigurado =>
                ("SAP HML: SAP NÃO CONFIGURADO", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.BloqueadoEscritaDesabilitada =>
                ("SAP HML: ESCRITA DESABILITADA", Color.FromArgb(239, 68, 68)),
            EstadoVisualIntegracaoSap.BloqueadoSemItens =>
                ("SAP HML: SEM ITENS ELEGÍVEIS", Color.FromArgb(249, 115, 22)),
            _ =>
                ("SAP HML: BLOQUEADO", Color.FromArgb(239, 68, 68))
        };

        sapStatusDotLabel.ForeColor = cor;
        string tooltip = string.IsNullOrWhiteSpace(detalhe)
            ? texto
            : $"{texto} — {detalhe}";
        sapStatusLabel.Text = texto;
        sapStatusLabel.AutoSize = false;
        _envioSapToolTip.SetToolTip(sapStatusPanel, tooltip);
        _envioSapToolTip.SetToolTip(sapStatusLabel, tooltip);
        _envioSapToolTip.SetToolTip(sapStatusDotLabel, tooltip);
    }

    private void ReturnToLeituraProducao()
    {
        if (_isProductionStarted)
        {
            statusLabel.Text = "Finalize a leitura antes de sair da tela.";
            return;
        }

        if (Owner is PainelInicialForm)
        {
            Close();
            return;
        }

        PainelInicialForm painel = new();
        painel.NavigateToProcessoProducao();
        painel.Show();
        Close();
    }

    private void CustomTitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNclButtonDown, HtCaption, 0);
    }

    private void ToggleWindowState()
    {
        if (_isProductionStarted)
        {
            return;
        }

        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void ProcessoProdutoAcabadoForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // §13: bloqueia durante leitura ativa, persistência em andamento ou lotes aguardando retry. Depois do
        // sucesso (_isProductionStarted=false e não aguardando) o fechamento é liberado.
        if (!_isProductionStarted && !_lotesFinalizadosAguardandoPersistencia && !_finalizandoPesagem)
        {
            return;
        }

        e.Cancel = true;
        statusLabel.Text = _lotesFinalizadosAguardandoPersistencia
            ? "Existe uma entrada por lotes finalizada em memória e ainda não gravada. Tente novamente a gravação antes de sair."
            : "Finalize a leitura antes de sair da tela.";
    }

    private bool PodeAtualizarTela()
        => !IsDisposed && !Disposing && !_fechamentoTelaCts.IsCancellationRequested;

    private static void ConfigureTitleButtonHover(Label button, Color hoverColor)
    {
        Color normalColor = button.BackColor;

        button.MouseEnter += (_, _) => button.BackColor = hoverColor;
        button.MouseLeave += (_, _) => button.BackColor = normalColor;
    }

    private void ConfigureResponsiveSummaryCards()
    {
        tableLayoutPanel6.Resize += (_, _) => AlignDateCardLayout(null, EventArgs.Empty);
        tableLayoutPanel8.Resize += (_, _) => AlignPlannedProductionCardLayout(null, EventArgs.Empty);
        Shown += (_, _) => RefreshSummaryCardDividers();
        Layout += (_, _) => RefreshSummaryCardDividers();

        RefreshSummaryCardDividers();
    }

    private void RefreshSummaryCardDividers()
    {
        AlignDateCardLayout(null, EventArgs.Empty);
        AlignPlannedProductionCardLayout(null, EventArgs.Empty);
    }

    private void AlignDateCardLayout(object? sender, EventArgs e)
    {
        int dividerTop = tableLayoutPanel6.Top + 11;
        int dividerHeight = Math.Max(18, tableLayoutPanel6.Height - 14);

        AlignDateDividerBetweenColumns(dateDividerLabel1, 1, ovenExitCaptionLabel, ovenExitTextBox, classificationDateCaptionLabel, classificationDateTextBox, dividerTop, dividerHeight);
        AlignDateDividerBetweenColumns(dateDividerLabel2, 2, classificationDateCaptionLabel, classificationDateTextBox, manufacturingDateCaptionLabel, manufacturingDateTextBox, dividerTop, dividerHeight);
        AlignDateDividerBetweenColumns(dateDividerLabel3, 3, manufacturingDateCaptionLabel, manufacturingDateTextBox, expirationDateCaptionLabel, expirationDateTextBox, dividerTop, dividerHeight);
    }

    private void AlignPlannedProductionCardLayout(object? sender, EventArgs e)
    {
        int dividerTop = tableLayoutPanel8.Top + 13;
        int dividerHeight = Math.Max(18, tableLayoutPanel8.Height - 17);

        AlignDividerBetweenColumns(plannedProductionDividerLabel1, readForecastBoxesCaptionLabel, readForecastBoxesTextBox, readForecastPackagesCaptionLabel, readForecastPackagesTextBox, dividerTop, dividerHeight);
        AlignDividerBetweenColumns(plannedProductionDividerLabel2, readForecastPackagesCaptionLabel, readForecastPackagesTextBox, balanceCaptionLabel, balanceTextBox, dividerTop, dividerHeight);
    }

    private void AlignDateDividerBetweenColumns(Label divider, int columnBoundaryIndex, Control previousCaption, Control previousValue, Control nextCaption, Control nextValue, int top, int height)
    {
        int tableLeft = tableLayoutPanel6.Left;
        int previousRight = tableLeft + Math.Max(GetVisibleTextRight(previousCaption), GetVisibleTextRight(previousValue));
        int nextLeft = tableLeft + Math.Min(GetVisibleTextLeft(nextCaption), GetVisibleTextLeft(nextValue));
        int columnBoundary = tableLeft + (tableLayoutPanel6.Width * columnBoundaryIndex / 4);

        int dividerX = nextLeft - previousRight >= 24
            ? previousRight + ((nextLeft - previousRight) / 2)
            : columnBoundary - 10;

        divider.Location = new Point(dividerX, top);
        divider.Size = new Size(1, height);
    }

    private void AlignDividerBetweenColumns(Label divider, Control previousCaption, Control previousValue, Control nextCaption, Control nextValue, int top, int height)
    {
        int previousRight = Math.Max(GetVisibleTextRight(previousCaption), GetVisibleTextRight(previousValue));
        int nextLeft = Math.Min(GetVisibleTextLeft(nextCaption), GetVisibleTextLeft(nextValue));

        int dividerX = previousRight < nextLeft
            ? previousRight + ((nextLeft - previousRight) / 2)
            : nextLeft - 12;

        divider.Location = new Point(dividerX, top);
        divider.Size = new Size(1, height);
    }

    private static int GetVisibleTextLeft(Control control)
    {
        return control.Left + control.Margin.Left;
    }

    private static int GetVisibleTextRight(Control control)
    {
        Size textSize = TextRenderer.MeasureText(control.Text, control.Font, control.Size, TextFormatFlags.NoPadding);
        return control.Left + control.Margin.Left + textSize.Width;
    }

    private void ConfigureProductionSearchBox()
    {
        Color searchBackColor = Color.FromArgb(248, 250, 252);
        Color searchIconColor = Color.FromArgb(148, 163, 184);

        productionSearchPanel.FillColor = searchBackColor;
        productionSearchTextBox.BackColor = searchBackColor;
        productionSearchGlyphLabel.ForeColor = searchIconColor;
        productionSearchGlyphLabel.Cursor = Cursors.IBeam;
        productionSearchGlyphLabel.Click += (_, _) => productionSearchTextBox.Focus();
        productionSearchPanel.Click += (_, _) => productionSearchTextBox.Focus();
        productionSearchIconPictureBox.BackColor = searchBackColor;
        productionSearchIconPictureBox.Image = CreateTintedIcon(global::FugaPET_Dev.Properties.Resources.search_red, searchIconColor);
        productionSearchIconPictureBox.Cursor = Cursors.IBeam;
        productionSearchIconPictureBox.Click += (_, _) => productionSearchTextBox.Focus();
    }

    private void ConfigurarBuscaFiltroEstadoVazioEntrada()
    {
        productionSearchTextBox.PlaceholderText = _configuracaoTela.PlaceholderPesquisa;
        productionSearchTextBox.MaxLength = 120;
        productionSearchTextBox.TextChanged += (_, _) => AplicarFiltroItensPedido();

        ConfigurarMenuFiltrosItensPedido();
        productionFilterButton.Click += (_, _) =>
            _filtroItensPedidoMenu.Show(productionFilterButton, new Point(0, productionFilterButton.Height));

        ConfigurarEstadoVazioItensPedido();
        ExibirEstadoVazioItensPedido(
            "Informe ou selecione um Pedido de Compra para carregar os itens.",
            $"A tela exibirá apenas itens compatíveis com {_configuracaoTela.TituloTela}.");
    }

    private void ConfigurarMenuFiltrosItensPedido()
    {
        _filtroItensPedidoMenu.Items.Clear();
        AdicionarFiltroItensPedido("Todos", FiltroItensEntrada.Todos);
        AdicionarFiltroItensPedido("Pendentes de pesagem", FiltroItensEntrada.PendentesPesagem);
        AdicionarFiltroItensPedido("Pesados localmente", FiltroItensEntrada.PesadosLocalmente);
        AdicionarFiltroItensPedido("Pendentes SAP", FiltroItensEntrada.PendentesSap);
        AdicionarFiltroItensPedido("Enviados SAP", FiltroItensEntrada.EnviadosSap);
        AdicionarFiltroItensPedido("Erro SAP", FiltroItensEntrada.ErroSap);
        AdicionarFiltroItensPedido("Com saldo", FiltroItensEntrada.ComSaldo);
        AdicionarFiltroItensPedido("Sem saldo", FiltroItensEntrada.SemSaldo);
        AtualizarTextoFiltroItensPedido();
    }

    private void AdicionarFiltroItensPedido(string texto, FiltroItensEntrada filtro)
    {
        ToolStripMenuItem item = new(texto)
        {
            Tag = filtro,
            Checked = filtro == _filtroItensAtual
        };
        item.Click += (_, _) =>
        {
            _filtroItensAtual = filtro;
            foreach (ToolStripMenuItem menuItem in _filtroItensPedidoMenu.Items.OfType<ToolStripMenuItem>())
            {
                menuItem.Checked = menuItem.Tag is FiltroItensEntrada valor && valor == _filtroItensAtual;
            }

            AtualizarTextoFiltroItensPedido();
            AplicarFiltroItensPedido();
        };
        _filtroItensPedidoMenu.Items.Add(item);
    }

    private void AtualizarTextoFiltroItensPedido()
    {
        string texto = _filtroItensAtual switch
        {
            FiltroItensEntrada.PendentesPesagem => "Pendentes",
            FiltroItensEntrada.PesadosLocalmente => "Pesados",
            FiltroItensEntrada.PendentesSap => "Pend. SAP",
            FiltroItensEntrada.EnviadosSap => "Enviados",
            FiltroItensEntrada.ErroSap => "Erro SAP",
            FiltroItensEntrada.ComSaldo => "Com saldo",
            FiltroItensEntrada.SemSaldo => "Sem saldo",
            _ => "Filtros"
        };

        productionFilterButton.Text = $"=  {texto}";
    }

    private void ConfigurarEstadoVazioItensPedido()
    {
        _estadoVazioItensLabel = new Label
        {
            AutoSize = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(75, 85, 99),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        productionReadingsPanel.Controls.Add(_estadoVazioItensLabel);
        PosicionarEstadoVazioItensPedido();
        _estadoVazioItensLabel.BringToFront();
        productionReadingsPanel.Resize += (_, _) => PosicionarEstadoVazioItensPedido();
        productionDataGridView.Resize += (_, _) => PosicionarEstadoVazioItensPedido();
    }

    private void PosicionarEstadoVazioItensPedido()
    {
        if (_estadoVazioItensLabel is null)
        {
            return;
        }

        _estadoVazioItensLabel.Location = productionDataGridView.Location;
        _estadoVazioItensLabel.Size = productionDataGridView.Size;
    }

    private void ExibirEstadoVazioItensPedido(string mensagem, string? submensagem = null)
    {
        if (_estadoVazioItensLabel is null)
        {
            return;
        }

        _estadoVazioItensLabel.Text = string.IsNullOrWhiteSpace(submensagem)
            ? mensagem
            : $"{mensagem}{Environment.NewLine}{Environment.NewLine}{submensagem}";
        _estadoVazioItensLabel.Visible = true;
        _estadoVazioItensLabel.BringToFront();
    }

    private void OcultarEstadoVazioItensPedido()
    {
        if (_estadoVazioItensLabel is not null)
        {
            _estadoVazioItensLabel.Visible = false;
        }
    }

    private void ConfigureSideActionButtonIcons()
    {
        lerEtiquetaButton.IconGlyph = string.Empty;
        lerEtiquetaButton.IconImage = CreateTintedIcon(
            global::FugaPET_Dev.Properties.Resources.read_weight,
            Color.FromArgb(17, 24, 39));
    }

    private void ConfigureProductionGridFooter()
    {
        productionDataGridView.RowsAdded += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.RowsRemoved += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.Scroll += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.SizeChanged += (_, _) => UpdateProductionGridFooter();
        productionDataGridView.DataBindingComplete += (_, _) => UpdateProductionGridFooter();
        Shown += (_, _) => BeginInvoke(UpdateProductionGridFooter);

        UpdateProductionGridFooter();
    }

    private void ConfigurarColunaReimpressaoEtiqueta()
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(
                PermissoesSistema.Acoes.Reimprimir))
        {
            return;
        }

        if (productionDataGridView.Columns.Contains(ColunaReimpressaoEtiqueta))
        {
            return;
        }

        DataGridViewButtonColumn colunaReimpressao = new()
        {
            Name = ColunaReimpressaoEtiqueta,
            HeaderText = "Pesagens",
            Text = "🖨",
            ToolTipText = "Ver pesagens e reimprimir etiquetas",
            UseColumnTextForButtonValue = true,
            ReadOnly = true,
            Width = 72,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };

        colunaReimpressao.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        colunaReimpressao.DefaultCellStyle.Font = new Font("Segoe UI Emoji", 10F, FontStyle.Regular);
        productionDataGridView.Columns.Insert(5, colunaReimpressao);
    }

    private void UpdateProductionGridFooter()
    {
        int totalRows = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Count(row => !row.IsNewRow);

        if (totalRows == 0)
        {
            productionFooterLabel.Text = "Exibindo 0 de 0 itens";
            productionPageLabel.Text = "Pagina 1 de 1";
            productionPageTextBox.Text = "1";
            productionPreviousPageButton.Enabled = false;
            productionNextPageButton.Enabled = false;
            return;
        }

        int firstVisibleRow = GetFirstVisibleProductionRowIndex();
        int visibleRows = Math.Max(1, productionDataGridView.DisplayedRowCount(false));
        int firstItem = Math.Min(totalRows, firstVisibleRow + 1);
        int lastItem = Math.Min(totalRows, firstVisibleRow + visibleRows);
        int totalPages = Math.Max(1, (int)Math.Ceiling(totalRows / (double)visibleRows));
        int currentPage = Math.Min(totalPages, (firstVisibleRow / visibleRows) + 1);

        productionFooterLabel.Text = $"Exibindo {firstItem} a {lastItem} de {totalRows} itens";
        productionPageLabel.Text = $"Pagina {currentPage} de {totalPages}";
        productionPageTextBox.Text = currentPage.ToString();
        productionPreviousPageButton.Enabled = currentPage > 1;
        productionNextPageButton.Enabled = currentPage < totalPages;
    }

    private int GetFirstVisibleProductionRowIndex()
    {
        try
        {
            return productionDataGridView.FirstDisplayedScrollingRowIndex >= 0
                ? productionDataGridView.FirstDisplayedScrollingRowIndex
                : 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static Bitmap CreateTintedIcon(Bitmap source, Color tintColor)
    {
        Bitmap tintedIcon = new(source.Width, source.Height);

        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                tintedIcon.SetPixel(x, y, Color.FromArgb(pixel.A, tintColor));
            }
        }

        return tintedIcon;
    }

    private void ConfigureProductionActions()
    {
        startActionPanel.Click += StartProduction_Click;
        startActionIconLabel.Click += StartProduction_Click;
        startActionTextLabel.Click += StartProduction_Click;

        readWeightLegendPanel.Click += ReadWeightLegend_Click;
        readWeightLegendIconLabel.Click += ReadWeightLegend_Click;
        readWeightLegendTextLabel.Click += ReadWeightLegend_Click;
        ConfigureReadWeightHoverEffect();

        stopActionPanel.Click += StopProduction_Click;
        stopActionIconLabel.Click += StopProduction_Click;
        stopActionTextLabel.Click += StopProduction_Click;
        ConfigureDangerActionHoverEffect(stopActionPanel, stopActionIconLabel, stopActionTextLabel);
        ConfigureDangerActionHoverEffect(deleteLastLegendPanel, deleteLastLegendIconLabel, deleteLastLegendTextLabel);
        ConfigureDangerActionHoverEffect(deleteByCodeLegendPanel, deleteByCodeLegendIconLabel, deleteByCodeLegendTextLabel);

        deleteLastLegendPanel.Click += DeleteLastProductionRow_Click;
        deleteLastLegendIconLabel.Click += DeleteLastProductionRow_Click;
        deleteLastLegendTextLabel.Click += DeleteLastProductionRow_Click;
        deleteByCodeLegendPanel.Click += DeleteProductionRowByCode_Click;
        deleteByCodeLegendIconLabel.Click += DeleteProductionRowByCode_Click;
        deleteByCodeLegendTextLabel.Click += DeleteProductionRowByCode_Click;

        // Wire new sidePanel buttons to the same handlers as the legacy ones
        iniciarLeituraButton.Click += ToggleProductionFromSideButton_Click;
        lerEtiquetaButton.Click += ReadWeightLegend_Click;
        leituraManualButton.Click += LeituraManual_Click;
        ConfigurarAcaoEnvioSapHomologacao();

        UpdateProductionState(false);
        SetReadWeightEnabled(false);
        SetDeleteActionsEnabled(false);
        UpdateProductionCounters();
    }

    private void ConfigurarAcaoEnvioSapHomologacao()
    {
        bool podeEnviarSap = PossuiPermissaoEntrada(PermissoesSistema.Acoes.EnviarSap);
        productionActionsButton.Visible = podeEnviarSap;
        productionActionsButton.Enabled = false;
        productionActionsButton.Text = "Enviar SAP HML";
        productionActionsButton.AccessibleName = "Criar movimento 101 no SAP de homologação";
        _envioSapToolTip.SetToolTip(
            productionActionsButton,
            podeEnviarSap
                ? "Finalize e grave o lançamento para validar a prontidão do envio SAP."
                : "Usuário sem permissão ENVIAR_SAP.");
        productionActionsButton.Click += EnviarSapHomologacao_Click;
    }

    private async Task AtualizarProntidaoEnvioSapAsync()
    {
        DiagnosticoEnvioSapEntrada diagnostico;
        try
        {
            diagnostico = await _diagnosticarEnvioSapEntrada(
                _codigoLancamentoPersistido,
                _fechamentoTelaCts.Token);
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
            // Fechamento da tela: não há UI a atualizar e nada foi enviado ao SAP; encerra silenciosamente.
            return;
        }
        catch (Exception ex)
        {
            // §4/C: falha inesperada no diagnóstico NÃO envia SAP, NÃO apaga o lançamento local e NÃO congela
            // a tela. Botão desabilitado, chip FALHA e motivo amigável/sanitizado no tooltip e no status.
            // Registro de diagnóstico sanitizado (só o TIPO da exceção, nunca mensagem/credencial).
            System.Diagnostics.Trace.TraceError(
                $"PRONTIDAO_SAP_HML_ERRO em ProcessoEntradaProdutoForm: {ex.GetType().Name}");
            string motivoFalha = GetFriendlyErrorMessage(ex);
            productionActionsButton.Enabled = false;
            _envioSapToolTip.SetToolTip(productionActionsButton, motivoFalha);
            AtualizarEstadoVisualIntegracaoSap(EstadoVisualIntegracaoSap.Falha, motivoFalha);
            statusLabel.Text =
                $"Lançamento local {_codigoLancamentoPersistido} preservado. {motivoFalha}";
            return;
        }

        productionActionsButton.Visible = diagnostico.UsuarioTemPermissao;
        productionActionsButton.Enabled =
            diagnostico.PodeEnviar
            && _envioSapTask.IsCompleted
            && !_isProductionStarted;

        string motivo = diagnostico.MotivoBloqueio ?? "Lançamento apto para envio controlado.";
        _envioSapToolTip.SetToolTip(productionActionsButton, motivo);

        if (diagnostico.PodeEnviar)
        {
            AtualizarEstadoVisualIntegracaoSap(
                EstadoVisualIntegracaoSap.LiberadoParaEnvio,
                $"{diagnostico.TotalItensPersistidos} item(ns) apto(s)");
            return;
        }

        EstadoVisualIntegracaoSap estado = diagnostico.CodigoLancamento is null
            ? EstadoVisualIntegracaoSap.AguardandoGravacaoLocal
            : !diagnostico.AmbienteHomologacao
                ? EstadoVisualIntegracaoSap.BloqueadoAmbiente
                : !diagnostico.UsuarioTemPermissao
                ? EstadoVisualIntegracaoSap.BloqueadoSemPermissao
                : !diagnostico.IntegracaoSapAtiva
                    ? EstadoVisualIntegracaoSap.BloqueadoIntegracaoInativa
                    : !diagnostico.SapConfigurado
                        ? EstadoVisualIntegracaoSap.BloqueadoSapNaoConfigurado
                        : !diagnostico.EscritaSapHabilitada
                            ? EstadoVisualIntegracaoSap.BloqueadoEscritaDesabilitada
                            : EstadoVisualIntegracaoSap.BloqueadoSemItens;

        AtualizarEstadoVisualIntegracaoSap(estado, motivo);
    }

    private async void EnviarSapHomologacao_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_envioSapTask is { IsCompleted: false })
            {
                return;
            }

            _envioSapTask = EnviarSapHomologacaoAsync();
            await _envioSapTask;
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            string mensagem = await ErroUsuarioHelper.TratarAsync(
                "ENVIO_SAP_HML_ERRO",
                ex,
                "ProcessoEntradaProdutoForm",
                "Não foi possível concluir o envio controlado ao SAP de homologação.");
            AtualizarEstadoVisualIntegracaoSap(EstadoVisualIntegracaoSap.Falha);
            MessageBox.Show(
                mensagem,
                "Falha no envio SAP HML",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task EnviarSapHomologacaoAsync()
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.EnviarSap,
                "enviar lançamento ao SAP de homologação"))
        {
            return;
        }

        if (_isProductionStarted)
        {
            AtualizarEstadoVisualLocal(
                EstadoVisualLocalEntrada.Pendente,
                "finalize e grave o lançamento antes do envio SAP");
            return;
        }

        if (_codigoLancamentoPersistido is not long codigoLancamento || codigoLancamento <= 0)
        {
            AtualizarEstadoVisualLocal(
                EstadoVisualLocalEntrada.Pendente,
                "nenhum lançamento local gravado para envio");
            MessageBox.Show(
                "Finalize e grave o lançamento local antes de solicitar o envio ao SAP de homologação.",
                "Envio SAP HML indisponível",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        // Tarefa Entrada 23.2 (Ajuste 7): a confirmação mostra o PESO LÍQUIDO (KG) que será enviado ao SAP 101.
        decimal pesoLiquidoTotalKg = _leiturasPorItem.Values
            .Sum(EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido);

        DialogResult confirmacao = MessageBox.Show(
            MontarConfirmacaoEnvio101(codigoLancamento, pesoLiquidoTotalKg),
            "Criar movimento 101 no SAP HML",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmacao != DialogResult.Yes)
        {
            AtualizarEstadoVisualIntegracaoSap(
                EstadoVisualIntegracaoSap.LiberadoParaEnvio,
                "envio não confirmado");
            return;
        }

        // 12G-B: a habilitação da escrita SAP 101 é DETALHE INTERNO do envio — a confirmação acima é a única
        // ação do operador. Valida HABILITAR_ESCRITA_SAP, executa auditoria durável fail-closed e arma a
        // capability (one-shot). Falha de permissão/auditoria => ZERO POST. O writer consome antes do POST.
        ResultadoOperacao habilitacaoEscrita =
            await _habilitacaoEscritaSap.HabilitarParaEnvioAsync(_fechamentoTelaCts.Token);
        if (!habilitacaoEscrita.Sucesso)
        {
            statusLabel.Text = habilitacaoEscrita.Mensagem;
            AtualizarEstadoVisualIntegracaoSap(
                EstadoVisualIntegracaoSap.Falha,
                habilitacaoEscrita.Mensagem);
            MessageBox.Show(
                habilitacaoEscrita.Mensagem,
                "Envio SAP HML indisponível",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        productionActionsButton.Enabled = false;
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.Enviando,
            "envio autorizado em processamento");

        try
        {
            ResultadoEnvioSapEntrada resultado =
                await _controller.EnviarPesoEntradaParaSapHomologacaoAsync(
                    codigoLancamento,
                    _fechamentoTelaCts.Token);
            await ApresentarResultadoEnvioSapHomologacaoAsync(resultado);
        }
        finally
        {
            if (PodeAtualizarTela())
            {
                productionActionsButton.Enabled = false;
            }
        }
    }

    private async Task ApresentarResultadoEnvioSapHomologacaoAsync(
        ResultadoEnvioSapEntrada resultado)
    {
        switch (resultado.Cenario)
        {
            case CenarioEnvioSapEntrada.Enviado:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    $"lançamento {_codigoLancamentoPersistido} confirmado no SAP");
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.Enviado,
                    $"{resultado.Enviados} de {resultado.Total} item(ns)");
                MessageBox.Show(
                    resultado.Mensagem
                    ?? $"Documento de material criado no SAP de homologação para {resultado.Enviados} item(ns).",
                    "Documento de material criado no SAP",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                break;

            case CenarioEnvioSapEntrada.Parcial:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    "itens atualizados; lançamento mantido FINALIZADO_LOCAL");
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.Parcial,
                    $"{resultado.Enviados} de {resultado.Total} item(ns)");
                MessageBox.Show(
                    $"O SAP de homologação confirmou {resultado.Enviados} de {resultado.Total} item(ns). "
                    + "Verifique a rotina autorizada de integração antes de tentar novamente.",
                    "Envio SAP HML parcial",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioEnvioSapEntrada.Falha:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    "falha SAP registrada localmente como ERRO_SAP");
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "O SAP não criou o documento de material. "
                    + "A falha foi registrada no lançamento local.");
                break;

            case CenarioEnvioSapEntrada.SemPermissao:
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.BloqueadoSemPermissao,
                    "usuário sem permissão ENVIAR_SAP");
                break;

            case CenarioEnvioSapEntrada.AmbienteNaoHomologacao:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque o ambiente atual não é homologação.");
                break;

            case CenarioEnvioSapEntrada.EscritaDesabilitada:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque a escrita SAP está desabilitada.");
                break;

            case CenarioEnvioSapEntrada.IntegracaoInativa:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque a integração SAP está inativa.");
                break;

            case CenarioEnvioSapEntrada.SapNaoConfigurado:
                ApresentarFalhaEnvioSap(
                    "O envio foi bloqueado porque a configuração SAP está indisponível.");
                break;

            case CenarioEnvioSapEntrada.LancamentoSemItens:
                ApresentarFalhaEnvioSap(
                    "O lançamento local não possui itens persistidos elegíveis para envio.");
                break;

            case CenarioEnvioSapEntrada.MaterialDocumentNaoConfigurado:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Integração SAP Material Document não configurada.");
                break;

            case CenarioEnvioSapEntrada.UnidadeNaoSuportada:
            case CenarioEnvioSapEntrada.DadosIncompletos:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Item não elegível para criar o movimento 101 no SAP.");
                break;

            case CenarioEnvioSapEntrada.LancamentoJaConfirmadoSap:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    "lançamento já confirmado no SAP");
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Lançamento já confirmado no SAP. Reenvio bloqueado.");
                break;

            case CenarioEnvioSapEntrada.LancamentoCancelado:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Lançamento cancelado. Envio bloqueado.");
                break;

            case CenarioEnvioSapEntrada.EnvioEmProcessamento:
                ApresentarFalhaEnvioSap(
                    resultado.Mensagem
                    ?? "Envio já bloqueado ou em processamento. Aguarde a conclusão.");
                break;

            case CenarioEnvioSapEntrada.FalhaPersistenciaLocal:
                await ApresentarFalhaCriticaPersistenciaLocalAsync(resultado);
                break;

            default:
                ApresentarFalhaEnvioSap(
                    "O envio controlado ao SAP de homologação não foi concluído.");
                await AtualizarProntidaoEnvioSapAsync();
                break;
        }
    }

    private async Task ApresentarFalhaCriticaPersistenciaLocalAsync(
        ResultadoEnvioSapEntrada resultado)
    {
        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "divergência crítica entre resposta SAP e status local");
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.Falha,
            "SAP respondeu, mas o status local não foi atualizado");

        string mensagem = await ErroUsuarioHelper.TratarAsync(
            "ENVIO_SAP_STATUS_LOCAL_CRITICO",
            new InvalidOperationException(
                resultado.MensagemCritica
                ?? "Falha ao persistir o status local após resposta do SAP."),
            "ProcessoEntradaProdutoForm",
            "O SAP respondeu ao envio, mas o status local não foi atualizado. "
            + "Não repita a operação antes da análise do suporte.");

        MessageBox.Show(
            mensagem,
            "Divergência crítica SAP x FugaPET",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void ApresentarFalhaEnvioSap(string mensagem)
    {
        AtualizarEstadoVisualIntegracaoSap(EstadoVisualIntegracaoSap.Falha);
        MessageBox.Show(
            mensagem,
            "Falha no envio SAP HML",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void ConfigureStartActionHoverEffect()
    {
        startActionPanel.Padding = new Padding(2);
        startActionPanel.Paint += StartActionPanel_Paint;

        startActionPanel.MouseEnter += StartActionHover_MouseEnter;
        startActionIconLabel.MouseEnter += StartActionHover_MouseEnter;
        startActionTextLabel.MouseEnter += StartActionHover_MouseEnter;

        startActionPanel.MouseLeave += StartActionHover_MouseLeave;
        startActionIconLabel.MouseLeave += StartActionHover_MouseLeave;
        startActionTextLabel.MouseLeave += StartActionHover_MouseLeave;

        startActionPanel.Resize += (_, _) => AlignStartActionChildren();
        AlignStartActionChildren();
    }

    private void AlignStartActionChildren()
    {
        const int borderInset = 2;
        const int iconSize = 24;
        const int iconLeft = 4;

        int contentHeight = Math.Max(0, startActionPanel.ClientSize.Height - (borderInset * 2));
        int iconTop = borderInset + Math.Max(0, (contentHeight - iconSize) / 2);

        startActionIconLabel.Size = new Size(iconSize, iconSize);
        startActionIconLabel.Location = new Point(iconLeft, iconTop);

        int textLeft = iconLeft + iconSize + 2;
        startActionTextLabel.Location = new Point(textLeft, borderInset);
        startActionTextLabel.Size = new Size(
            Math.Max(0, startActionPanel.ClientSize.Width - textLeft - borderInset),
            Math.Max(0, startActionPanel.ClientSize.Height - (borderInset * 2)));
        startActionTextLabel.BackColor = Color.Transparent;
        startActionTextLabel.BorderStyle = BorderStyle.None;
        startActionIconLabel.BorderStyle = BorderStyle.None;
    }

    private void StartActionHover_MouseEnter(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        _isStartActionHovering = true;
        startActionPanel.Invalidate();
    }

    private void StartActionHover_MouseLeave(object? sender, EventArgs e)
    {
        Point cursorPosition = startActionPanel.PointToClient(Cursor.Position);
        if (startActionPanel.ClientRectangle.Contains(cursorPosition))
        {
            return;
        }

        _isStartActionHovering = false;
        startActionPanel.Invalidate();
    }

    private void StartActionPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (!_isStartActionHovering || _isProductionStarted)
        {
            return;
        }

        using Pen pen = new(StartActionHoverBorder, 3);
        Rectangle border = new(1, 1, startActionPanel.ClientSize.Width - 3, startActionPanel.ClientSize.Height - 3);
        e.Graphics.DrawRectangle(pen, border);
    }

    private void ConfigureReadWeightHoverEffect()
    {
        readWeightLegendPanel.Padding = new Padding(2);
        readWeightLegendPanel.Paint += ReadWeightLegendPanel_Paint;

        readWeightLegendPanel.MouseEnter += ReadWeightHover_MouseEnter;
        readWeightLegendIconLabel.MouseEnter += ReadWeightHover_MouseEnter;
        readWeightLegendTextLabel.MouseEnter += ReadWeightHover_MouseEnter;

        readWeightLegendPanel.MouseLeave += ReadWeightHover_MouseLeave;
        readWeightLegendIconLabel.MouseLeave += ReadWeightHover_MouseLeave;
        readWeightLegendTextLabel.MouseLeave += ReadWeightHover_MouseLeave;
    }

    private void ReadWeightHover_MouseEnter(object? sender, EventArgs e)
    {
        if (!readWeightLegendPanel.Enabled || !readWeightLegendPanel.Visible)
        {
            return;
        }

        _isReadWeightHovering = true;
        readWeightLegendPanel.Invalidate();
    }

    private void ReadWeightHover_MouseLeave(object? sender, EventArgs e)
    {
        Point cursorPosition = readWeightLegendPanel.PointToClient(Cursor.Position);
        if (readWeightLegendPanel.ClientRectangle.Contains(cursorPosition))
        {
            return;
        }

        _isReadWeightHovering = false;
        readWeightLegendPanel.Invalidate();
    }

    private void ReadWeightLegendPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (!_isReadWeightHovering || !readWeightLegendPanel.Enabled || !readWeightLegendPanel.Visible)
        {
            return;
        }

        using Pen pen = new(ReadWeightHoverBorder, 3);
        Rectangle border = new(1, 1, readWeightLegendPanel.ClientSize.Width - 3, readWeightLegendPanel.ClientSize.Height - 3);
        e.Graphics.DrawRectangle(pen, border);
    }

    private void ConfigureDangerActionHoverEffect(Panel panel, Control icon, Control text)
    {
        panel.Padding = new Padding(2);
        panel.Paint += DangerActionPanel_Paint;

        panel.MouseEnter += DangerActionHover_MouseEnter;
        icon.MouseEnter += DangerActionHover_MouseEnter;
        text.MouseEnter += DangerActionHover_MouseEnter;

        panel.MouseLeave += DangerActionHover_MouseLeave;
        icon.MouseLeave += DangerActionHover_MouseLeave;
        text.MouseLeave += DangerActionHover_MouseLeave;
    }

    private void DangerActionHover_MouseEnter(object? sender, EventArgs e)
    {
        Panel? panel = GetDangerActionPanel(sender);
        if (panel is null || !panel.Enabled || !panel.Visible)
        {
            return;
        }

        if (_hoveredDangerActionPanel != panel)
        {
            _hoveredDangerActionPanel?.Invalidate();
            _hoveredDangerActionPanel = panel;
        }

        panel.Invalidate();
    }

    private void DangerActionHover_MouseLeave(object? sender, EventArgs e)
    {
        Panel? panel = GetDangerActionPanel(sender);
        if (panel is null)
        {
            return;
        }

        Point cursorPosition = panel.PointToClient(Cursor.Position);
        if (panel.ClientRectangle.Contains(cursorPosition))
        {
            return;
        }

        if (_hoveredDangerActionPanel == panel)
        {
            _hoveredDangerActionPanel = null;
            panel.Invalidate();
        }
    }

    private void DangerActionPanel_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || _hoveredDangerActionPanel != panel || !panel.Enabled || !panel.Visible)
        {
            return;
        }

        using Pen pen = new(DangerActionHoverBorder, 3);
        Rectangle border = new(1, 1, panel.ClientSize.Width - 3, panel.ClientSize.Height - 3);
        e.Graphics.DrawRectangle(pen, border);
    }

    private Panel? GetDangerActionPanel(object? sender)
    {
        return sender switch
        {
            Panel panel => panel,
            Control { Parent: Panel panel } => panel,
            _ => null
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F9)
        {
            using TesteZebraForm printTestForm = new();
            printTestForm.ShowDialog(this);
            return true;
        }

        if (keyData == Keys.F12)
        {
            ReadWeightLegend_Click(readWeightLegendTextLabel, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F5)
        {
            // F5 = botão lateral Iniciar/Parar. Só dispara quando o botão está habilitado; durante
            // persistência/retry/após persistido ele fica desabilitado, então o F5 é consumido sem efeito
            // (não chama o seam, não muda _finalizandoPesagem/_lotesFinalizadosAguardandoPersistencia nem
            // correlation_id). Apenas o painel Parar inicia a nova tentativa no retry.
            if (iniciarLeituraButton.Enabled)
            {
                ToggleProductionFromSideButton_Click(iniciarLeituraButton, EventArgs.Empty);
            }

            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ToggleProductionFromSideButton_Click(object? sender, EventArgs e)
    {
        // Guarda defensiva: nunca alternar leitura pelo botão lateral quando ele está desabilitado
        // (persistência em andamento, retry aguardando ou lançamento já persistido).
        if (!iniciarLeituraButton.Enabled)
        {
            return;
        }

        if (_isProductionStarted)
        {
            StopProduction_Click(sender, e);
            return;
        }

        StartProduction_Click(sender, e);
    }

    private async void StartProduction_Click(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Executar,
                "executar entrada de produto"))
        {
            return;
        }

        try
        {
            if (!GarantirOperacaoLotesIniciada())
            {
                return;
            }

            _isProductionStarted = true;
            _codigoLancamentoPersistido = null;
            UpdateProductionState(true);
            AtualizarEstadoVisualIntegracaoSap(
                EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
                "operação por lotes em memória; SAP bloqueado nesta fase");
            statusLabel.Text = "Leitura por lotes iniciada em memória. Selecione o item, confirme o lote e registre a pesagem.";
        }
        catch (Exception ex)
        {
            _isProductionStarted = false;
            UpdateProductionState(false);
            string mensagem = GetFriendlyErrorMessage(ex);
            statusLabel.Text = mensagem;
            MessageBox.Show(mensagem, "Entrada por lotes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StartProductionDevicesWarmUp()
    {
        if (_productionDevicesWarmUpTask is { IsCompleted: false })
        {
            return;
        }

        _productionDevicesWarmUpTask = WarmUpProductionDevicesAsync();
    }

    private async Task WarmUpProductionDevicesAsync()
    {
        // Ambos os metodos ja sao assincronos e tratam excecao internamente;
        // chamamos direto (sem Task.Run desnecessario empurrando para o thread pool).
        await Task.WhenAll(
            AquecerImpressoraComSegurancaAsync(),
            WarmUpSaldoSafelyAsync());
    }

    private async Task AquecerImpressoraComSegurancaAsync()
    {
        try
        {
            await _impressaoEntrada.AquecerSeImpressoraDisponivelAsync();
        }
        catch (Exception)
        {
            // A validacao com mensagem amigavel continua acontecendo ao clicar em Ler Peso.
        }
    }

    private async Task WarmUpSaldoSafelyAsync()
    {
        try
        {
            await _balancaLeituraServico.AquecerAsync();
        }
        catch (Exception)
        {
            // A validacao com mensagem amigavel continua acontecendo ao clicar em Ler Peso.
        }
    }

    private const string MensagemRetryPersistenciaLotes =
        "Lotes finalizados em memória. A gravação local falhou. Clique em Parar para tentar novamente.";

    private const string MensagemAguardarLeituraBalanca =
        "Aguarde a conclusão da leitura da balança antes de parar a operação.";

    // GATE 106A: Parar sem NENHUMA leitura válida na operação = cancelamento da tentativa em memória.
    internal const string MensagemPesagemEncerradaSemLeituras =
        "Pesagem encerrada sem leituras. Nenhum lançamento foi gerado.";

    // §6/§12: nenhuma nova pesagem/lote enquanto persistindo ou aguardando o retry da gravação local.
    private bool PesagemBloqueadaNoFluxoLotes => _finalizandoPesagem || _lotesFinalizadosAguardandoPersistencia;

    private async void StopProduction_Click(object? sender, EventArgs e)
    {
        // §4: permite retry mesmo com _isProductionStarted já false, desde que aguardando persistência.
        if ((!_isProductionStarted && !_lotesFinalizadosAguardandoPersistencia) || _finalizandoPesagem)
        {
            return;
        }

        // §5: não parar/persistir enquanto uma leitura serial da balança estiver pendente.
        if (_isReadingWeight)
        {
            statusLabel.Text = MensagemAguardarLeituraBalanca;
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Finalizar,
                "finalizar entrada de produto"))
        {
            return;
        }

        // §5: async void = wrapper mínimo. Toda a orquestração (persistência local + reavaliação de prontidão
        // SAP) vive no método Task testável abaixo. Anteparo final: nenhuma exceção pode escapar do async void —
        // o lançamento local (se houve) permanece preservado e a tela continua operacional; nada é enviado ao SAP.
        try
        {
            await FinalizarPersistenciaEAtualizarProntidaoSapAsync();
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
            // Fechamento da tela em andamento: nada a fazer.
        }
        catch (Exception ex)
        {
            // Anteparo final do async void: registro sanitizado (só o TIPO) + mensagem amigável; nada de SAP.
            System.Diagnostics.Trace.TraceError(
                $"STOP_PRODUCTION_PRONTIDAO_ERRO em ProcessoEntradaProdutoForm: {ex.GetType().Name}");
            statusLabel.Text = GetFriendlyErrorMessage(ex);
        }
    }

    // §5: fluxo TESTÁVEL do botão Parar. (1) persiste localmente; (2) confirma sucesso local REAL;
    // (3) reavalia a prontidão do envio SAP manual; (4) atualiza botão/chip/tooltip. NUNCA envia SAP
    // automaticamente. Uma falha no diagnóstico NÃO perde o lançamento persistido nem congela a tela.
    internal async Task FinalizarPersistenciaEAtualizarProntidaoSapAsync()
    {
        await ExecutarPersistenciaLotesAsync();

        // Só reavalia prontidão quando a persistência local terminou com SUCESSO REAL:
        // código atribuído, produção encerrada e sem retry pendente. Em falha/retry, não diagnostica.
        if (_codigoLancamentoPersistido is not null
            && !_isProductionStarted
            && !_lotesFinalizadosAguardandoPersistencia)
        {
            await AtualizarProntidaoEnvioSapAsync();
        }
    }

    // §4/§5/§7/§11/§12: fluxo local idempotente do botão Parar. Extraído em Task para ser testável (STA) sem
    // depender de async void. NÃO usa o fluxo legado (GravarPesagensAsync/FinalizarLeituraAsync/SalvarLancamento).
    internal async Task ExecutarPersistenciaLotesAsync()
    {
        // §5: leitura serial pendente ⇒ não finaliza lote, não monta árvore, não chama o seam, não muda
        // _finalizandoPesagem nem correlation_id. Apenas orienta o operador a aguardar.
        if (_isReadingWeight)
        {
            statusLabel.Text = MensagemAguardarLeituraBalanca;
            return;
        }

        if (_finalizandoPesagem)
        {
            return; // §12: apenas uma persistência por vez (protege clique duplo).
        }

        _finalizandoPesagem = true;
        AtualizarControlesFluxoLotes(); // §3: reflete "persistência em andamento" imediatamente.
        try
        {
            // GATE 106A: ZERO pesagens válidas em TODA a operação (não apenas no lote selecionado) e nenhum
            // peso visual inconsistente ⇒ o Parar é CANCELAMENTO da tentativa em memória, não finalização.
            // Encerra ANTES de qualquer montagem de árvore, persistência local, lançamento ou SAP.
            // Retry pendente nunca entra aqui (a árvore já finalizada possui pesagens válidas).
            if (!_lotesFinalizadosAguardandoPersistencia
                && OperacaoSemQualquerPesagemValida()
                && !ExistePesoVisualSemLeituraRastreavel())
            {
                CancelarOperacaoSemLeiturasEmMemoria();
                return;
            }

            // §5: reentrante — na primeira passada valida e finaliza; no retry reconhece a árvore já finalizada.
            if (!GarantirLotesProntosParaPersistencia())
            {
                return; // mensagem objetiva já publicada; mantém a leitura ativa.
            }

            // Incremento 069: o PISO da tolerancia so pode ser cobrado aqui. Todo item comeca em zero,
            // que esta abaixo de qualquer piso — cobra-lo durante a pesagem barraria a primeira pesagem
            // de todo recebimento. Falta de quantidade so vira fato quando o operador declara que
            // terminou, e e este o momento.
            // 070: FALTA tambem e liberavel pelo gerente — o outro lado de "maior ou menor". A
            // mensagem de bloqueio e publicada dentro, junto com o dialogo quando ele cabe.
            if (!await ToleranciaLiberaFinalizacaoAsync())
            {
                return;
            }

            // §3: o orquestrador é a única fonte da árvore por lotes (mesmas correlation_id no retry).
            EntradaProdutoLancamentoComLotesPersistencia arvore =
                _controller.MontarLancamentoComLotesParaPersistencia();

            ResultadoPersistenciaEntradaComLotes resultado =
                await _registrarOuRecuperarLancamentoComLotes(arvore, _fechamentoTelaCts.Token);

            AplicarSucessoPersistenciaLotes(resultado);
        }
        catch (ErroOperacionalEsperadoException ex)
        {
            // §11.A: mensagem amigável; mantém operação; permite retry; não perde correlation_id.
            MarcarAguardandoPersistencia(ex.Message);
        }
        catch (OperationCanceledException)
        {
            // §11.B: cancelamento (não durante o fechamento) preserva a árvore e permite retry.
            if (!_fechamentoTelaCts.IsCancellationRequested)
            {
                MarcarAguardandoPersistencia(MensagemRetryPersistenciaLotes);
            }
        }
        catch (Exception ex)
        {
            // §11.C: exceção inesperada — mensagem amigável; não marca como gravado; não perde a árvore.
            MarcarAguardandoPersistencia(GetFriendlyErrorMessage(ex));
        }
        finally
        {
            _finalizandoPesagem = false;
            // §3: reconstrói a interface a partir do estado final REAL (sucesso, retry ou leitura ativa).
            AtualizarControlesFluxoLotes();
        }
    }

    // §2/§3: ponto ÚNICO de atualização dos controles do fluxo por lotes. Recalcula tudo a partir dos campos
    // de estado atuais (persistência, retry, leitura, persistido), sem depender de uma chamada anterior.
    private void AtualizarControlesFluxoLotes() => UpdateProductionState(_isProductionStarted);

    // §7: sucesso — só aqui atribui o código, encerra a leitura, limpa a operação e atualiza a interface.
    private void AplicarSucessoPersistenciaLotes(ResultadoPersistenciaEntradaComLotes resultado)
    {
        _codigoLancamentoPersistido = resultado.CodigoLancamento;
        _isProductionStarted = false;
        _lotesFinalizadosAguardandoPersistencia = false;
        UpdateProductionState(false);

        string detalhe = resultado.PersistenciaRecuperada
            ? $"lançamento {resultado.CodigoLancamento} recuperado com segurança"
            : $"lançamento {resultado.CodigoLancamento} gravado por lotes";
        AtualizarEstadoVisualLocal(EstadoVisualLocalEntrada.Gravado, detalhe);

        // §9: SAP continua separado — apenas indica AGUARDANDO; nenhuma chamada de integração aqui.
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "envio ao SAP é uma ação separada e explícita");

        statusLabel.Text = resultado.PersistenciaRecuperada
            ? $"Lançamento local {resultado.CodigoLancamento} já havia sido gravado e foi recuperado com segurança."
            : $"Lançamento local {resultado.CodigoLancamento} gravado com sucesso por lotes.";

        // §7/§8: depois de salvar o código e atualizar o estado visual, limpa a operação em memória. A
        // projeção visual do grid permanece para consulta; _codigoLancamentoPersistido é preservado.
        _controller.LimparOperacaoComLotes();
    }

    // §6/§11: falha/cancelamento após finalização em memória. Preserva a árvore, mantém _isProductionStarted
    // e _codigoLancamentoPersistido=null, habilita RETRY e bloqueia novas pesagens. A mensagem final SEMPRE
    // orienta o operador a clicar novamente em Parar, sem duplicar quando já for a mensagem padrão.
    private void MarcarAguardandoPersistencia(string mensagem)
    {
        _lotesFinalizadosAguardandoPersistencia = true;
        AtualizarControlesFluxoLotes(); // §3: reflete "aguardando retry" (Parar habilitado, pesagem bloqueada).

        string motivoSeguro = string.IsNullOrWhiteSpace(mensagem) ? string.Empty : mensagem.Trim();
        string detalhe = motivoSeguro.Length == 0 || motivoSeguro.Contains(MensagemRetryPersistenciaLotes, StringComparison.Ordinal)
            ? (motivoSeguro.Length == 0 ? MensagemRetryPersistenciaLotes : motivoSeguro)
            : $"{motivoSeguro} {MensagemRetryPersistenciaLotes}";

        AtualizarEstadoVisualLocal(EstadoVisualLocalEntrada.Pendente, detalhe);
    }

    /// <summary>
    /// GATE 106A: true quando a operação corrente NÃO possui NENHUMA pesagem válida — somando TODOS os itens
    /// e TODOS os lotes (ativos e já finalizados em memória). O escopo é GLOBAL de propósito: uma leitura
    /// válida em qualquer outro item/lote impede o cancelamento e preserva o fluxo normal de validação.
    /// internal para teste direto (InternalsVisibleTo).
    /// </summary>
    internal bool OperacaoSemQualquerPesagemValida()
    {
        EstadoOperacaoEntradaProdutoLotes estado = _controller.ObterEstadoOperacaoComLotes();

        return estado.Itens
            .SelectMany(item => item.Lotes)
            .Sum(lote => lote.QuantidadePesagensValidas) <= 0;
    }

    /// <summary>
    /// GATE 106A: cancela a tentativa de pesagem SOMENTE em memória. Descarta a operação/lotes temporários
    /// (não há pesagem válida nem lançamento persistido, logo nada de persistido é cancelado), encerra a
    /// leitura e devolve a tela ao estado "leitura não iniciada" — mantendo pedido e itens carregados para
    /// uma nova tentativa. NÃO cria peso 0, NÃO monta árvore, NÃO persiste, NÃO gera código de lançamento e
    /// NÃO toca SAP. <c>_codigoLancamentoPersistido</c> não é alterado: nada foi gravado aqui e um lançamento
    /// legítimo de uma sessão anterior não pode ser descartado.
    /// </summary>
    private void CancelarOperacaoSemLeiturasEmMemoria()
    {
        _controller.LimparOperacaoComLotes();
        _isProductionStarted = false;
        _lotesFinalizadosAguardandoPersistencia = false;
        UpdateProductionState(false);

        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "pesagem encerrada sem leituras");
        statusLabel.Text = MensagemPesagemEncerradaSemLeituras;
    }

    // §5: garante que a árvore esteja pronta para persistir, de forma REENTRANTE.
    //  A. lote ativo não finalizado: valida todos e só então finaliza (sem estado parcial).
    //  B. lote já FinalizadoEmMemoria: considerado pronto (retry) — não finaliza de novo.
    //  C. item sem lote: não impede a persistência quando outro item possui lote (excluído pelo orquestrador).
    //  D. nenhuma árvore com lote/pesagem válida: não persiste, mantém leitura ativa e mostra mensagem.
    private bool GarantirLotesProntosParaPersistencia()
    {
        EstadoOperacaoEntradaProdutoLotes estado = _controller.ObterEstadoOperacaoComLotes();

        bool existeLoteFinalizadoValido = estado.Itens
            .SelectMany(item => item.Lotes)
            .Any(lote => lote.Estado == EstadoOperacionalLoteEntrada.FinalizadoEmMemoria
                         && lote.QuantidadePesagensValidas > 0);

        bool existeLoteAtivoPendente = estado.Itens.Any(item =>
            item.CodigoLoteAtivoLocal is Guid ativo
            && item.Lotes.Any(lote => lote.CodigoLocal == ativo
                                      && lote.Estado != EstadoOperacionalLoteEntrada.FinalizadoEmMemoria));

        // B: retry — árvore já integralmente finalizada e sem lote ativo pendente.
        if (existeLoteFinalizadoValido && !existeLoteAtivoPendente)
        {
            return true;
        }

        // A: primeira passada — valida e finaliza os lotes ativos (separando validação de mutação).
        return FinalizarLotesAtivosEmMemoria();
    }

    private bool GarantirOperacaoLotesIniciada()
    {
        if (!PedidoSelecionadoValido())
        {
            statusLabel.Text = "Selecione um pedido antes de iniciar a leitura.";
            MessageBox.Show("Selecione um pedido antes de iniciar a leitura.", "Entrada de Produto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            AtualizarDisponibilidadeInicioLeitura();
            return false;
        }

        if (_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0)
        {
            statusLabel.Text = "Usuário sem setor definido: não é possível iniciar a operação por lotes.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ObterNomeTerminalAtual()))
        {
            statusLabel.Text = "Terminal local não identificado para iniciar a operação por lotes.";
            return false;
        }

        if (_itensPedidoCarregados.Count == 0)
        {
            statusLabel.Text = "Pedido sem itens autorizados carregados para iniciar a operação por lotes.";
            return false;
        }

        string numeroPedido = _numeroPedidoCarregado.Trim();
        EstadoOperacaoEntradaProdutoLotes estadoAtual = _controller.ObterEstadoOperacaoComLotes();
        if (estadoAtual.OperacaoIniciada)
        {
            if (string.Equals(estadoAtual.NumeroPedido, numeroPedido, StringComparison.OrdinalIgnoreCase)
                && estadoAtual.ModoEntradaMaterial == _modoEntrada)
            {
                statusLabel.Text = "Operação por lotes retomada em memória para o mesmo pedido.";
                return true;
            }

            statusLabel.Text = "Existe uma operação em memória de outro pedido ou modo. Finalize ou feche a tela antes de trocar.";
            return false;
        }

        ContextoOperacaoEntradaProdutoLotes contexto = new()
        {
            NumeroPedido = numeroPedido,
            Fornecedor = lotTextBox.Text.Trim(),
            CodigoSetor = codigoSetor,
            Terminal = ObterNomeTerminalAtual(),
            ModoEntradaMaterial = _modoEntrada
        };

        _controller.IniciarOperacaoComLotes(contexto, _itensPedidoCarregados);
        return true;
    }

    private async Task<bool> GarantirLoteAtivoParaLinhaAsync(DataGridViewRow linhaItem)
    {
        // §6: nenhum novo lote pode ser aberto pela Form durante persistência/retry.
        if (PesagemBloqueadaNoFluxoLotes)
        {
            statusLabel.Text = MensagemRetryPersistenciaLotes;
            return false;
        }

        if (!long.TryParse(GetCellValue(linhaItem, "productionItemIdColumn"), out long codigoItem) || codigoItem <= 0)
        {
            statusLabel.Text = "Item inválido para confirmar lote.";
            return false;
        }

        _controller.SelecionarItemOperacaoComLotes(codigoItem);
        EstadoItemEntradaProdutoLotes? item = ObterEstadoItemOperacaoLotes(codigoItem);
        if (item?.PodePesar == true)
        {
            AtualizarStatusLoteAtivo(item, "Pesagem liberada.");
            return true;
        }

        if (item is null || !item.PodeConfirmarNovoLote)
        {
            statusLabel.Text = "O lote atual precisa ser finalizado ou corrigido antes de nova pesagem.";
            return false;
        }

        DadosLoteEntrada? dados = _solicitarDadosLote(this, _modoEntrada);
        if (dados is null)
        {
            statusLabel.Text = "Confirmação de lote cancelada. Nenhuma pesagem foi registrada.";
            return false;
        }

        _controller.ConfirmarLoteOperacaoComLotes(codigoItem, dados.NumeroLote, dados.DataFabricacao, dados.DataVencimento);
        item = ObterEstadoItemOperacaoLotes(codigoItem);
        if (item?.PodePesar != true)
        {
            statusLabel.Text = "Lote confirmado, mas a pesagem não foi liberada pelo orquestrador.";
            return false;
        }

        AtualizarStatusLoteAtivo(item, "Pesagem liberada.");
        await Task.CompletedTask;
        return true;
    }

    private EstadoItemEntradaProdutoLotes? ObterEstadoItemOperacaoLotes(long codigoItem)
        => _controller.ObterEstadoOperacaoComLotes().Itens
            .FirstOrDefault(item => item.CodigoSapPedidoCompraItem == codigoItem);

    private void AtualizarStatusLoteAtivo(EstadoItemEntradaProdutoLotes item, string detalhe)
    {
        EstadoLoteEntradaProdutoLotes? lote = item.Lotes.FirstOrDefault(l => l.CodigoLocal == item.CodigoLoteAtivoLocal);
        if (lote is null)
        {
            statusLabel.Text = $"Item {item.NumeroItemSap} selecionado. {detalhe}";
            return;
        }

        List<string> partes = [$"Item {item.NumeroItemSap} — lote {lote.Dados.NumeroLote} confirmado."];
        if (lote.Dados.DataFabricacao != default)
        {
            partes.Add($"Fabricação {lote.Dados.DataFabricacao:dd/MM/yyyy}.");
        }

        if (lote.Dados.DataVencimento != default)
        {
            partes.Add($"Vencimento {lote.Dados.DataVencimento:dd/MM/yyyy}.");
        }

        partes.Add($"Estado {lote.Estado}.");
        partes.Add(detalhe);
        statusLabel.Text = string.Join(" ", partes);
    }

    private async Task<EntradaProdutoPesagemEmMemoria?> RegistrarPesoLidoOperacaoComLotesAsync(
        DataGridViewRow linhaItem,
        string pesoTexto,
        string origem,
        string leituraOriginal)
    {
        if (!TryParsePesoKg(pesoTexto, out decimal pesoBruto))
        {
            statusLabel.Text = "Peso lido inválido.";
            return null;
        }

        if (!await GarantirLoteAtivoParaLinhaAsync(linhaItem))
        {
            return null;
        }

        if (!long.TryParse(GetCellValue(linhaItem, "productionItemIdColumn"), out long codigoItem) || codigoItem <= 0)
        {
            statusLabel.Text = "Item inválido para registrar a leitura.";
            return null;
        }

        if (!LinhaPossuiTaraSelecionada(linhaItem, out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            statusLabel.Text = "Selecione a tara antes de registrar a leitura.";
            return null;
        }

        decimal taraKg = EntradaProdutoQuantidadeSap.ConverterTaraParaKg(tara.PesoKg, "KG");

        // TETO DA TOLERANCIA — ESTE E O CAMINHO VIVO DA PESAGEM.
        //
        // Ate 24/11/2026 esta verificacao existia so em AdicionarLeituraNaLinha, que e chamado
        // unicamente por RegistrarPesoLido — metodo que NAO E MAIS CHAMADO por ninguem desde que o
        // fluxo por lotes o substituiu (ha ate teste afirmando a remocao). Resultado: o teto nunca
        // bloqueou nada, e ninguem percebeu porque a tolerancia ainda nao tinha sido exercida num
        // pedido real. O piso, na finalizacao, sempre esteve no caminho vivo.
        //
        // Aqui e antes de RegistrarPesagemOperacaoComLotes de proposito: depois dele o peso ja faz
        // parte do total do item, e desfazer exigiria excluir a pesagem.
        if (!await ToleranciaLiberaPesagemAsync(
                codigoItem,
                EntradaProdutoPesagemCalculos.CalcularPesoLiquido(pesoBruto, taraKg)))
        {
            return null;
        }

        EntradaProdutoPesagemEmMemoria pesagem = _controller.RegistrarPesagemOperacaoComLotes(
            codigoItem,
            pesoBruto,
            taraKg,
            tara.CodigoTara,
            origem,
            string.Equals(origem, EntradaProdutoPesagemCalculos.OrigemBalanca, StringComparison.OrdinalIgnoreCase)
                ? _idBalancaSelecionada
                : null,
            leituraOriginal,
            DateTimeOffset.Now);

        if (pesagem.CodigoLocalPesagem == Guid.Empty)
        {
            statusLabel.Text = "Registro de pesagem retornou identificador local inválido.";
            return null;
        }

        SincronizarLeiturasItemComOperacaoLotes(linhaItem, codigoItem);
        ApplyProductionRowStyle(linhaItem, linhaItem.Index);
        ClearGridSelection(productionDataGridView);
        linhaItem.Selected = true;
        SetCurrentProductionCell(linhaItem, "productionPesoLidoColumn");
        UpdateProductionCounters();
        return pesagem;
    }

    private bool SincronizarLeiturasItemComOperacaoLotes(
        DataGridViewRow linhaItem,
        long codigoSapPedidoCompraItem)
    {
        IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagens =
            _controller.ObterPesagensItemOperacaoComLotes(codigoSapPedidoCompraItem);
        _leiturasPorItem[codigoSapPedidoCompraItem] = pesagens
            .OrderBy(p => p.Pesagem.Sequencia)
            .Select(p => p.Pesagem)
            .ToList();
        bool atualizado = AtualizarTotaisDaLinha(linhaItem, _leiturasPorItem[codigoSapPedidoCompraItem]);
        UpdateProductionCounters();
        return atualizado;
    }


    private bool FinalizarLotesAtivosEmMemoria()
    {
        EstadoOperacaoEntradaProdutoLotes estado = _controller.ObterEstadoOperacaoComLotes();
        List<EstadoItemEntradaProdutoLotes> itensValidados = [];

        foreach (EstadoItemEntradaProdutoLotes item in estado.Itens)
        {
            if (item.CodigoLoteAtivoLocal is not Guid codigoLote)
            {
                continue;
            }

            EstadoLoteEntradaProdutoLotes? lote = item.Lotes.FirstOrDefault(l => l.CodigoLocal == codigoLote);
            if (lote is null || lote.Estado == EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
            {
                continue;
            }

            if (lote.QuantidadePesagensValidas <= 0)
            {
                statusLabel.Text = $"Lote {lote.Dados.NumeroLote} do item {item.NumeroItemSap} não possui pesagem válida para finalizar.";
                return false;
            }

            if (!item.PodeFinalizarLote)
            {
                statusLabel.Text = $"Lote {lote.Dados.NumeroLote} do item {item.NumeroItemSap} ainda não pode ser finalizado.";
                return false;
            }

            itensValidados.Add(item);
        }

        if (itensValidados.Count == 0)
        {
            statusLabel.Text = "Não existe lote ativo com leitura válida para finalizar em memória.";
            return false;
        }

        foreach (EstadoItemEntradaProdutoLotes itemValidado in itensValidados)
        {
            _controller.FinalizarLoteOperacaoComLotes(itemValidado.CodigoSapPedidoCompraItem);
        }

        return true;
    }



    private bool BloquearTrocaPedidoComOperacaoEmMemoria(
        string numeroPedidoSolicitado,
        bool restaurarTexto)
    {
        EstadoOperacaoEntradaProdutoLotes estado = _controller.ObterEstadoOperacaoComLotes();
        if (!estado.OperacaoIniciada)
        {
            return false;
        }

        if (string.Equals(estado.NumeroPedido, numeroPedidoSolicitado, StringComparison.OrdinalIgnoreCase)
            && estado.ModoEntradaMaterial == _modoEntrada)
        {
            return false;
        }

        if (!OperacaoComLotesPossuiLotes())
        {
            _controller.LimparOperacaoComLotes();
            return false;
        }

        if (restaurarTexto)
        {
            string pedidoOriginal = estado.NumeroPedido ?? _numeroPedidoCarregado;
            if (!string.IsNullOrWhiteSpace(pedidoOriginal)
                && !string.Equals(pedidoComboBox.Text.Trim(), pedidoOriginal, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _restaurandoPedidoOperacaoLotes = true;
                    pedidoComboBox.Text = pedidoOriginal;
                }
                finally
                {
                    _restaurandoPedidoOperacaoLotes = false;
                }
            }
        }

        statusLabel.Text = "Existe uma operação em memória ainda não persistida. Feche a tela para descartar ou continue o mesmo pedido.";
        AtualizarDisponibilidadeInicioLeitura();
        return true;
    }

    private bool OperacaoComLotesPossuiLotes()
    {
        EstadoOperacaoEntradaProdutoLotes estado = _controller.ObterEstadoOperacaoComLotes();
        return estado.Itens.Any(item => item.Lotes.Count > 0);
    }

    // Captura as leituras do grid e delega somente a persistencia LOCAL ao controller.
    // O envio SAP HML permanece uma acao separada, explicita e confirmada.
    private async Task GravarPesagensAsync()
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            statusLabel.Text = "Producao parada.";
            return;
        }

        EntradaProdutoLancamento lancamento = MontarLancamentoDoGrid();
        if (lancamento.Itens.Count == 0 && ExistePesoVisualSemLeituraRastreavel())
        {
            AtualizarEstadoVisualLocal(
                EstadoVisualLocalEntrada.Pendente,
                "peso visual sem leitura rastreável");
            MessageBox.Show(
                "Há peso visual na grade, mas não há leitura rastreável vinculada. Refaça a leitura.",
                "Finalização da pesagem",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ResultadoFinalizacaoEntrada resultado =
            await _controller.FinalizarLeituraAsync(lancamento, _fechamentoTelaCts.Token);
        await ApresentarResultadoFinalizacaoAsync(resultado);
    }

    // Le o grid de producao e monta o lancamento (captura de selecao da tela).
    private EntradaProdutoLancamento MontarLancamentoDoGrid()
    {
        List<EntradaProdutoItem> itensLancamento = [];
        string numeroPedido = pedidoComboBox.Text.Trim();
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (!long.TryParse(GetCellValue(row, "productionItemIdColumn"), out long codigoItem) || codigoItem <= 0)
            {
                continue;
            }

            IReadOnlyList<EntradaProdutoPesagem> leituras = ObterLeiturasItem(codigoItem);
            if (leituras.Count == 0)
            {
                continue;
            }

            if (!LinhaPossuiTaraSelecionada(row, out _))
            {
                continue;
            }

            decimal pesoLiquidoTotal = EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(leituras);
            _itensCarregadosPorCodigo.TryGetValue(codigoItem, out PedidoCompraSapItem? itemCarregado);
            itensLancamento.Add(new EntradaProdutoItem
            {
                CodigoSapPedidoCompraItem = codigoItem,
                NumeroItem = GetCellValue(row, "productionNumeroItemColumn"),
                Material = GetCellValue(row, "productionCodeColumn"),
                Centro = itemCarregado?.Centro,
                Deposito = itemCarregado?.Deposito,
                Unidade = GetCellValue(row, "productionWeightColumn"),
                QuantidadePrevista = itemCarregado?.Quantidade,
                QuantidadeRecebida = pesoLiquidoTotal,
                Pesagens = leituras
            });
        }

        return new EntradaProdutoLancamento
        {
            NumeroPedido = numeroPedido,
            Fornecedor = lotTextBox.Text.Trim(),
            CodigoSetor = _idSetorSelecionado,
            Terminal = ObterNomeTerminalAtual(),
            Itens = itensLancamento
        };
    }

    private bool ExistePesoVisualSemLeituraRastreavel()
    {
        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (!TryParsePesoKg(GetCellValue(row, "productionPesoLidoColumn"), out decimal pesoVisual)
                || pesoVisual <= 0m)
            {
                continue;
            }

            if (!long.TryParse(GetCellValue(row, "productionItemIdColumn"), out long codigoItem)
                || codigoItem <= 0
                || ObterLeiturasItem(codigoItem).Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    // Apresenta o resultado da finalizacao (somente UI: status + dialogo).
    private async Task ApresentarResultadoFinalizacaoAsync(ResultadoFinalizacaoEntrada resultado)
    {
        switch (resultado.Cenario)
        {
            case CenarioFinalizacaoEntrada.NenhumaLeitura:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Pendente,
                    "nenhuma leitura para gravar");
                MessageBox.Show(
                    "Nenhuma leitura foi registrada. Clique em Iniciar Leitura e use Ler Peso, Leitura Manual ou Pesagem Múltipla antes de finalizar.",
                    "Finalização da pesagem",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioFinalizacaoEntrada.LancamentoNaoGravado:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Pendente,
                    "lançamento não foi gravado");
                MessageBox.Show(
                    resultado.MensagemFalhaLancamento ?? string.Empty,
                    "Lancamento nao gravado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;

            case CenarioFinalizacaoEntrada.GravadoLocal:
                // C12: a finalizacao grava SOMENTE local; o envio ao SAP e um passo separado.
                _codigoLancamentoPersistido = resultado.CodigoLancamento;
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Gravado,
                    $"lançamento {resultado.CodigoLancamento} com {resultado.Gravados} item(ns)");
                AtualizarEstadoVisualIntegracaoSap(
                    EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
                    "validando prontidão do envio");
                await AtualizarProntidaoEnvioSapAsync();
                MessageBox.Show(
                    $"Lançamento local {resultado.CodigoLancamento} gravado com sucesso "
                    + $"com {resultado.Gravados} item(ns).\n\n"
                    + "O envio ao SAP deve ser executado pela rotina autorizada de integração em homologação.",
                    "Lançamento local gravado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                break;

            case CenarioFinalizacaoEntrada.ErroAoGravar:
                AtualizarEstadoVisualLocal(
                    EstadoVisualLocalEntrada.Pendente,
                    "não foi possível gravar as pesagens");
                break;
        }
    }

    private static bool TryParsePesoKg(string texto, out decimal peso)
    {
        peso = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string limpo = new string(texto.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray()).Replace(',', '.');
        return decimal.TryParse(limpo, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out peso);
    }

    private async void ReadWeightLegend_Click(object? sender, EventArgs e)
    {
        if (!_isProductionStarted || _isReadingWeight || PesagemBloqueadaNoFluxoLotes)
        {
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Executar,
                "ler peso da entrada"))
        {
            return;
        }

        DataGridViewRow? linhaItem = GetSelectedProductionRow();
        if (linhaItem is null)
        {
            statusLabel.Text = "Selecione o item do pedido antes de ler o peso.";
            MessageBox.Show(
                "Selecione o item do pedido na lista antes de ler o peso.",
                "Leitura de peso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!LinhaPossuiTaraSelecionada(linhaItem, out _))
        {
            statusLabel.Text = "Selecione a tara do item antes de ler o peso.";
            await SelecionarTaraParaLinhaAsync(linhaItem);
            if (!LinhaPossuiTaraSelecionada(linhaItem, out _))
            {
                statusLabel.Text = "Peso nao registrado: e necessario selecionar a tara do item.";
                return;
            }
        }

        _isReadingWeight = true;
        SetReadWeightEnabled(false);
        statusLabel.Text = "Lendo peso da balanca...";

        try
        {
            ResultadoLeituraPeso leitura = await _balancaLeituraServico.LerPesoAsync();
            if (!leitura.Sucesso)
            {
                statusLabel.Text = leitura.Mensagem;
                MessageBox.Show(
                    leitura.Mensagem,
                    "Erro ao ler peso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            string weight = leitura.Peso;
            EntradaProdutoPesagemEmMemoria? pesagemCriada = await RegistrarPesoLidoOperacaoComLotesAsync(
                linhaItem,
                weight,
                EntradaProdutoPesagemCalculos.OrigemBalanca,
                weight);
            if (pesagemCriada is null)
            {
                return;
            }

            statusLabel.Text = "Pesagem registrada no lote em memória. Impressão do novo fluxo de lotes ainda não habilitada.";
        }
        catch (Exception ex)
        {
            string message = GetFriendlyErrorMessage(ex);
            statusLabel.Text = message;
            MessageBox.Show(
                message,
                "Erro ao ler peso",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _isReadingWeight = false;
            // §4: uma leitura assíncrona não pode reabilitar controles durante persistência/retry.
            SetReadWeightEnabled(_isProductionStarted && !PesagemBloqueadaNoFluxoLotes);
        }
    }

    private static string GetFriendlyErrorMessage(Exception ex)
    {
        if (ex.InnerException is not null &&
            ex.Message.Contains("One or more errors occurred", StringComparison.OrdinalIgnoreCase))
        {
            return GetFriendlyErrorMessage(ex.InnerException);
        }

        return ex is ErroOperacionalEsperadoException or ErroImpressaoZebraException
            ? ex.Message
            : "Nao foi possivel concluir a operacao. Acione o suporte.";
    }

    private Task<bool> TentarImprimirEtiquetaAposLeituraAsync(DadosEtiquetaMateriaPrima label)
        => TentarImprimirEtiquetaAutomaticaAsync(label, "leitura de peso");

    private async Task<bool> TentarImprimirEtiquetaAutomaticaAsync(
        DadosEtiquetaMateriaPrima label,
        string operacao)
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(PermissoesSistema.Acoes.Imprimir))
        {
            statusLabel.Text =
                $"Peso registrado, mas etiqueta não impressa via {operacao}: usuário sem permissão de impressão.";
            return false;
        }

        string impressora = "não identificada";
        try
        {
            impressora = await _impressaoEntrada.DescreverImpressoraAtualAsync();
            await _impressaoEntrada.GarantirImpressoraDisponivelAsync();
            await _impressaoEntrada.ImprimirEtiquetaMateriaPrimaAsync(label);
            return true;
        }
        catch (Exception ex)
        {
            await ErroUsuarioHelper.TratarAsync(
                "IMPRESSAO_ETIQUETA_AUTOMATICA_ERRO",
                ex,
                "ProcessoEntradaProdutoForm",
                "Peso registrado, mas a etiqueta não foi impressa.");
            string mensagemAmigavel = GetFriendlyErrorMessage(ex);
            statusLabel.Text =
                $"Peso registrado via {operacao}, mas a etiqueta não foi impressa na Zebra '{impressora}'. "
                + $"{mensagemAmigavel} Use a reimpressão após corrigir a impressora.";
            return false;
        }
    }

    // Grava o peso lido da balanca na coluna Peso da linha do item selecionado e devolve a pesagem criada
    // (para imprimir SOMENTE aquela pesagem, não o total consolidado).
    private bool RegistrarPesoLido(DataGridViewRow linhaItem, string weight, out EntradaProdutoPesagem? pesagemCriada)
    {
        pesagemCriada = null;

        if (!TryParsePesoKg(weight, out decimal pesoBruto))
        {
            statusLabel.Text = "Peso lido invalido.";
            return false;
        }

        if (!AdicionarLeituraNaLinha(
                linhaItem,
                pesoBruto,
                "BALANCA",
                weight,
                out pesagemCriada))
        {
            return false;
        }

        ApplyProductionRowStyle(linhaItem, linhaItem.Index);
        ClearGridSelection(productionDataGridView);
        linhaItem.Selected = true;
        SetCurrentProductionCell(linhaItem, "productionPesoLidoColumn");
        UpdateProductionCounters();
        return true;
    }

    private async void DeleteLastProductionRow_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Cancelar,
                "cancelar entrada de produto"))
        {
            return;
        }

        if (!_isProductionStarted || PesagemBloqueadaNoFluxoLotes)
        {
            return;
        }

        DataGridViewRow? lastRow = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .LastOrDefault();

        if (lastRow is null)
        {
            statusLabel.Text = "Nao ha linhas para excluir.";
            return;
        }

        string productionCode = GetCellValue(lastRow, "productionCodeColumn");
        if (!ConfirmDeleteLastProductionRow(productionCode))
        {
            statusLabel.Text = "Exclusao cancelada.";
            return;
        }

        if (CancelarLeiturasDaLinha(lastRow))
        {
            statusLabel.Text = $"Leituras do item {productionCode} marcadas como canceladas.";
            return;
        }

        productionDataGridView.Rows.Remove(lastRow);
        _nextProductionCode = Math.Max(1, _nextProductionCode - 1);
        RestyleProductionRows();
        UpdateProductionCounters();
        ClearGridSelection(productionDataGridView);
        statusLabel.Text = $"Ultima etiqueta {productionCode} excluida.";
    }

    private async void DeleteProductionRowByCode_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.Cancelar,
                "cancelar entrada de produto"))
        {
            return;
        }

        if (!_isProductionStarted || PesagemBloqueadaNoFluxoLotes)
        {
            return;
        }

        string? productionCode = PromptProductionCodeToDelete();
        if (string.IsNullOrWhiteSpace(productionCode))
        {
            statusLabel.Text = "Exclusao cancelada.";
            return;
        }

        DataGridViewRow? row = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(item => !item.IsNewRow)
            .FirstOrDefault(item => string.Equals(
                GetCellValue(item, "productionCodeColumn"),
                productionCode,
                StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            statusLabel.Text = $"Etiqueta {productionCode} nao encontrada.";
            MessageBox.Show(
                $"Nenhuma etiqueta com codigo {productionCode} foi encontrada.",
                "Etiqueta nao encontrada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!ConfirmProductionRowDelete(productionCode, "Deseja realmente excluir a etiqueta informada?"))
        {
            statusLabel.Text = "Exclusao cancelada.";
            return;
        }

        if (CancelarLeiturasDaLinha(row))
        {
            statusLabel.Text = $"Leituras do item {productionCode} marcadas como canceladas.";
            return;
        }

        productionDataGridView.Rows.Remove(row);
        RestyleProductionRows();
        UpdateProductionCounters();
        ClearGridSelection(productionDataGridView);
        statusLabel.Text = $"Etiqueta {productionCode} excluida.";
    }

    private string? PromptProductionCodeToDelete()
    {
        using Form promptForm = new()
        {
            Text = "Excluir etiqueta por codigo",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 185),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label messageLabel = new()
        {
            Text = "Informe o codigo serial da etiqueta que deseja excluir.",
            Dock = DockStyle.Top,
            Height = 62,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(18, 10, 18, 4)
        };

        TextBox codeTextBox = new()
        {
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Location = new Point(80, 76),
            Size = new Size(220, 31),
            TextAlign = HorizontalAlignment.Center
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(74, 8, 0, 14),
            Height = 64
        };

        Button cancelButton = CreateDialogButton("Cancelar", Color.FromArgb(55, 60, 69), DialogResult.Cancel);
        Button deleteButton = CreateDialogButton("Excluir", Color.FromArgb(200, 78, 10), DialogResult.OK);
        cancelButton.Margin = new Padding(0, 0, 10, 0);

        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(deleteButton);
        promptForm.Controls.Add(messageLabel);
        promptForm.Controls.Add(codeTextBox);
        promptForm.Controls.Add(buttonsPanel);
        promptForm.AcceptButton = deleteButton;
        promptForm.CancelButton = cancelButton;
        promptForm.ActiveControl = codeTextBox;

        return promptForm.ShowDialog(this) == DialogResult.OK
            ? codeTextBox.Text.Trim()
            : null;
    }

    private bool ConfirmDeleteLastProductionRow(string productionCode)
    {
        return ConfirmProductionRowDelete(productionCode, "Deseja realmente excluir a ultima etiqueta?");
    }

    private bool ConfirmProductionRowDelete(string productionCode, string message)
    {
        using Form confirmationForm = new()
        {
            Text = "Confirmar exclusao",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(360, 150),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label messageLabel = new()
        {
            Text = $"{message}\r\nCodigo: {productionCode}",
            Dock = DockStyle.Top,
            Height = 86,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(18, 10, 18, 4)
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(74, 8, 0, 14),
            Height = 64
        };

        Button noButton = CreateDialogButton("Nao", Color.FromArgb(55, 60, 69), DialogResult.No);
        Button yesButton = CreateDialogButton("Sim", Color.FromArgb(200, 78, 10), DialogResult.Yes);
        noButton.Margin = new Padding(0, 0, 10, 0);

        buttonsPanel.Controls.Add(noButton);
        buttonsPanel.Controls.Add(yesButton);
        confirmationForm.Controls.Add(messageLabel);
        confirmationForm.Controls.Add(buttonsPanel);
        confirmationForm.AcceptButton = noButton;
        confirmationForm.CancelButton = noButton;
        confirmationForm.ActiveControl = noButton;

        return confirmationForm.ShowDialog(this) == DialogResult.Yes;
    }

    private static Button CreateDialogButton(string text, Color backColor, DialogResult dialogResult)
    {
        Button button = new()
        {
            Text = text,
            DialogResult = dialogResult,
            BackColor = backColor,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Cascadia Code", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Size = new Size(96, 34),
            Margin = new Padding(0)
        };

        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void RestyleProductionRows()
    {
        for (int rowIndex = 0; rowIndex < productionDataGridView.Rows.Count; rowIndex++)
        {
            DataGridViewRow row = productionDataGridView.Rows[rowIndex];
            if (!row.IsNewRow)
            {
                ApplyProductionRowStyle(row, rowIndex);
            }
        }
    }

    private void UpdateProductionCounters()
    {
        int quantidadeTotalPedido = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Sum(row => int.TryParse(GetCellValue(row, "productionQuantityColumn"), out int quantity) ? quantity : 0);

        decimal pesoUtilizado = productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Sum(row => TryParsePesoKg(GetCellValue(row, "productionPesoLidoColumn"), out decimal weight) ? weight : 0m);

        boxesCaptionLabel.Text = "Qtde Total Pedido";
        packagesCaptionLabel.Text = "Peso Utilizado";
        boxesCounterLabel.Text = quantidadeTotalPedido.ToString("000");
        packagesCounterLabel.Text = $"{pesoUtilizado.ToString("000.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} KG";
        UpdateProductionGridFooter();
    }

    private static string NormalizeCounterTotal(string value)
    {
        string digits = new(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int total) ? total.ToString() : "0";
    }

    private void SetReadWeightEnabled(bool enabled)
    {
        bool leituraBalancaHabilitada =
            enabled && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Executar);
        bool leituraManualHabilitada =
            enabled && PossuiPermissaoEntrada(PermissoesSistema.Acoes.PesoManual);

        if (!leituraBalancaHabilitada)
        {
            _isReadWeightHovering = false;
            readWeightLegendPanel.Invalidate();
        }

        readWeightLegendPanel.Enabled = leituraBalancaHabilitada;
        readWeightLegendIconLabel.Enabled = leituraBalancaHabilitada;
        readWeightLegendTextLabel.Enabled = leituraBalancaHabilitada;
        readWeightLegendPanel.Cursor = leituraBalancaHabilitada ? Cursors.Hand : Cursors.Default;
        readWeightLegendIconLabel.Cursor = leituraBalancaHabilitada ? Cursors.Hand : Cursors.Default;
        readWeightLegendTextLabel.Cursor = leituraBalancaHabilitada ? Cursors.Hand : Cursors.Default;
        readWeightLegendTextLabel.ForeColor = leituraBalancaHabilitada ? EnabledLegendTextColor : DisabledLegendTextColor;
        readWeightLegendIconLabel.Visible = leituraBalancaHabilitada;
        lerEtiquetaButton.Enabled = leituraBalancaHabilitada;
        leituraManualButton.Enabled = leituraManualHabilitada;
    }

    private void SetDeleteActionsEnabled(bool enabled)
    {
        enabled = enabled && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Cancelar);

        deleteLastLegendPanel.Enabled = enabled;
        deleteLastLegendIconLabel.Enabled = enabled;
        deleteLastLegendTextLabel.Enabled = enabled;
        deleteByCodeLegendPanel.Enabled = enabled;
        deleteByCodeLegendIconLabel.Enabled = enabled;
        deleteByCodeLegendTextLabel.Enabled = enabled;

        deleteLastLegendPanel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        deleteLastLegendIconLabel.Cursor = deleteLastLegendPanel.Cursor;
        deleteLastLegendTextLabel.Cursor = deleteLastLegendPanel.Cursor;
        deleteByCodeLegendPanel.Cursor = enabled ? Cursors.Hand : Cursors.Default;
        deleteByCodeLegendIconLabel.Cursor = deleteByCodeLegendPanel.Cursor;
        deleteByCodeLegendTextLabel.Cursor = deleteByCodeLegendPanel.Cursor;

        deleteLastLegendTextLabel.ForeColor = enabled ? EnabledLegendTextColor : DisabledLegendTextColor;
        deleteByCodeLegendTextLabel.ForeColor = enabled ? EnabledLegendTextColor : DisabledLegendTextColor;
        deleteLastLegendIconLabel.Visible = enabled;
        deleteByCodeLegendIconLabel.Visible = enabled;
    }

    private async void LeituraManual_Click(object? sender, EventArgs e)
    {
        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.PesoManual,
                "informar peso manual"))
        {
            return;
        }

        if (!_isProductionStarted || PesagemBloqueadaNoFluxoLotes)
        {
            statusLabel.Text = "Inicie a leitura antes de informar o peso manual.";
            return;
        }

        DataGridViewRow? selectedRow = GetSelectedProductionRow();
        if (selectedRow is null)
        {
            statusLabel.Text = "Selecione uma linha para informar o peso manual.";
            MessageBox.Show(
                "Selecione o item do pedido na lista antes de informar o peso manual.",
                "Peso manual",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!LinhaPossuiTaraSelecionada(selectedRow, out _))
        {
            statusLabel.Text = "Selecione a tara do item antes de informar o peso manual.";
            await SelecionarTaraParaLinhaAsync(selectedRow);
            if (!LinhaPossuiTaraSelecionada(selectedRow, out _))
            {
                statusLabel.Text = "Peso nao registrado: e necessario selecionar a tara do item.";
                return;
            }
        }

        string? manualWeight = PromptManualProductionWeight(string.Empty);
        if (string.IsNullOrWhiteSpace(manualWeight))
        {
            statusLabel.Text = "Peso manual cancelado.";
            return;
        }

        if (!TryNormalizeWeight(manualWeight, out string normalizedWeight, out string? errorMessage))
        {
            MessageBox.Show(
                errorMessage ?? "Informe um peso valido.",
                "Peso manual",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!TryParsePesoKg(normalizedWeight, out decimal pesoManual))
        {
            return;
        }

        EntradaProdutoPesagemEmMemoria? pesagemManual = await RegistrarPesoLidoOperacaoComLotesAsync(
            selectedRow,
            normalizedWeight,
            EntradaProdutoPesagemCalculos.OrigemManual,
            manualWeight);
        if (pesagemManual is null)
        {
            return;
        }

        statusLabel.Text = "Pesagem registrada no lote em memória. Impressão do novo fluxo de lotes ainda não habilitada.";
    }

    private void UpdateProductionState(bool started)
    {
        ClearDangerActionHover();
        sidePanel.BackColor = Color.White;
        sideReadingStatusLabel.Text = started ? "Ativo" : "Inativo";
        sideReadingStatusLabel.ForeColor = started ? ReadingStatusActiveColor : ReadingStatusInactiveColor;
        // §2: os quatro estados do fluxo por lotes governam Enabled/Visible de forma coerente.
        bool persistindo = _finalizandoPesagem;
        bool aguardandoRetry = _lotesFinalizadosAguardandoPersistencia;
        bool bloqueiaPesagem = persistindo || aguardandoRetry; // == PesagemBloqueadaNoFluxoLotes
        bool podePararOuTentarNovamente = (started || aguardandoRetry)
            && !persistindo
            && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Finalizar);
        bool podeAlternarLeitura = started
            ? PossuiPermissaoEntrada(PermissoesSistema.Acoes.Finalizar)
            : PodeIniciarLeitura();
        Color corInicio = !started && podeAlternarLeitura ? ActionEnabledColor : ActionDisabledColor;
        startActionPanel.BackColor = corInicio;
        startActionTextLabel.ForeColor = !started && podeAlternarLeitura ? EnabledLegendTextColor : DisabledLegendTextColor;
        startActionPanel.Enabled = !started && podeAlternarLeitura;
        startActionIconLabel.Enabled = startActionPanel.Enabled;
        startActionTextLabel.Enabled = startActionPanel.Enabled;
        startActionPanel.Cursor = startActionPanel.Enabled ? Cursors.Hand : Cursors.Default;
        startActionIconLabel.Cursor = startActionPanel.Cursor;
        startActionTextLabel.Cursor = startActionPanel.Cursor;
        _isStartActionHovering = false;
        startActionPanel.Invalidate();

        UpdateStatusCardState(started);
        UpdateTitleBarLockState(started);
        iniciarLeituraButton.BaseBackColor = started
            ? Color.FromArgb(250, 105, 26)
            : podeAlternarLeitura ? ReadingStatusActiveColor : ActionDisabledColor;
        iniciarLeituraButton.BaseForeColor = Color.White;
        iniciarLeituraButton.IconFontFamily = "Segoe MDL2 Assets";
        iniciarLeituraButton.IconGlyph = started ? "\uE71A" : "\uE768";
        iniciarLeituraButton.PrimaryText = started ? "PARAR LEITURA" : "INICIAR LEITURA";
        // Durante persistência E durante retry o alternador lateral (Iniciar/Parar leitura) fica desabilitado.
        // No retry, somente o painel Parar (stopActionPanel) pode iniciar a nova tentativa.
        iniciarLeituraButton.Enabled = podeAlternarLeitura && !bloqueiaPesagem;
        iniciarLeituraButton.Cursor = iniciarLeituraButton.Enabled ? Cursors.Hand : Cursors.Default;
        iniciarLeituraButton.Invalidate();
        lerEtiquetaButton.Visible = started;
        leituraManualButton.Visible =
            started && PossuiPermissaoEntrada(PermissoesSistema.Acoes.PesoManual);

        productionDataGridView.SelectionMode = started
            ? DataGridViewSelectionMode.FullRowSelect
            : DataGridViewSelectionMode.CellSelect;

        // Pedido desabilitado durante leitura ativa, persistência e retry.
        pedidoComboBox.Enabled = !started
            && !bloqueiaPesagem
            && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Consultar)
            && PossuiPermissaoEntrada(PermissoesSistema.Acoes.SincronizarCache);

        // Parar: visível durante leitura e durante retry; habilitado apenas quando pode parar/tentar de novo.
        stopActionPanel.Visible = started || aguardandoRetry;
        stopActionPanel.Enabled = podePararOuTentarNovamente;
        stopActionPanel.Cursor = stopActionPanel.Enabled ? Cursors.Hand : Cursors.Default;
        stopActionIconLabel.Cursor = stopActionPanel.Cursor;
        stopActionTextLabel.Cursor = stopActionPanel.Cursor;

        if (!started)
        {
            ClearGridSelection(productionDataGridView);
        }

        // §4: leitura/manual/cancelamentos desabilitados durante persistência e retry, não só pelos handlers.
        SetReadWeightEnabled(started && !bloqueiaPesagem);
        SetDeleteActionsEnabled(started && !bloqueiaPesagem);
    }

    private bool PodeIniciarLeitura()
        // §8: após persistir, não se inicia nova leitura sobre a mesma projeção; recarregar o pedido limpa o
        // código e a projeção. Também não se inicia enquanto aguarda o retry da gravação.
        => !_codigoLancamentoPersistido.HasValue
            && !_lotesFinalizadosAguardandoPersistencia
            && PossuiPermissaoEntrada(PermissoesSistema.Acoes.Executar)
            && PedidoSelecionadoValido();

    private bool PedidoSelecionadoValido()
    {
        string numeroPedido = pedidoComboBox.Text.Trim();
        return !string.IsNullOrWhiteSpace(numeroPedido)
            && string.Equals(numeroPedido, _numeroPedidoCarregado, StringComparison.OrdinalIgnoreCase)
            && productionDataGridView.Rows
                .Cast<DataGridViewRow>()
                .Any(row => !row.IsNewRow);
    }

    private void AtualizarDisponibilidadeInicioLeitura()
    {
        if (!_isProductionStarted)
        {
            UpdateProductionState(false);
        }
    }

    private void ProductionDataGridView_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            if (!_isProductionStarted)
            {
                ClearGridSelection(productionDataGridView);
            }

            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        productionDataGridView.CurrentCell = productionDataGridView[e.ColumnIndex, e.RowIndex];
        row.Selected = true;
    }

    private async void ProductionDataGridView_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        // Coluna "Pesagens": abre a relação de pesagens (ver/reimprimir), nunca imprime o total do item.
        if (e.ColumnIndex >= 0 && productionDataGridView.Columns[e.ColumnIndex].Name == ColunaReimpressaoEtiqueta)
        {
            await AbrirDetalhePesagensAsync(row);
            return;
        }

        if (!_isProductionStarted)
        {
            if (!LinhaPossuiTaraSelecionada(row, out _))
            {
                await SelecionarTaraParaLinhaAsync(row);
            }

            ClearGridSelection(productionDataGridView);
            row.Selected = true;
            SetCurrentProductionCell(row, "productionCodeColumn");
            statusLabel.Text = LinhaPossuiTaraSelecionada(row, out _)
                ? "Tara selecionada. Inicie a leitura para registrar o peso do item."
                : "Selecione a tara do item ou inicie a leitura para registrar peso.";
            return;
        }

        await AbrirDetalhePesagensAsync(row);
    }

    private DateTime _ultimaAberturaDetalhePesagens = DateTime.MinValue;

    // Ponto único de abertura da relação de pesagens. Anti-dupla-abertura (CellClick + CellDoubleClick).
    // Se o lançamento já foi persistido, abre em modo consulta/reimpressão com as pesagens do banco.
    private async Task AbrirDetalhePesagensAsync(DataGridViewRow row)
    {
        if ((DateTime.Now - _ultimaAberturaDetalhePesagens).TotalMilliseconds < 800)
        {
            return;
        }
        _ultimaAberturaDetalhePesagens = DateTime.Now;

        try
        {
            if (_codigoLancamentoPersistido is long codigoLancamento && codigoLancamento > 0)
            {
                await AbrirPesagensPersistidasParaLinhaAsync(row, codigoLancamento);
            }
            else
            {
                await AbrirPesagemMultiplaParaLinhaAsync(row);
            }
        }
        finally
        {
            _ultimaAberturaDetalhePesagens = DateTime.Now;
        }
    }

    // Modo consulta/reimpressão: carrega cada pesagem persistida (não SUM) e reimprime por pesagem individual.
    private async Task AbrirPesagensPersistidasParaLinhaAsync(DataGridViewRow row, long codigoLancamento)
    {
        if (!long.TryParse(GetCellValue(row, "productionItemIdColumn"), out long codigoSapItem) || codigoSapItem <= 0)
        {
            statusLabel.Text = "Item invalido para consultar as pesagens.";
            return;
        }

        EntradaProdutoItemPersistido? itemPersistido =
            await _entradaServico.ObterItemPersistidoAsync(codigoLancamento, codigoSapItem, _fechamentoTelaCts.Token);
        IReadOnlyList<EntradaProdutoPesagem> pesagens =
            await _entradaServico.ListarPesagensPersistidasAsync(codigoLancamento, codigoSapItem, _fechamentoTelaCts.Token);
        if (itemPersistido is null || pesagens.Count == 0)
        {
            statusLabel.Text = "Nao foram encontradas pesagens persistidas para este item.";
            MessageBox.Show(
                "Nao ha pesagens persistidas para este item.",
                "Pesagens",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro tara =
            row.Tag as global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro
            ?? new global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro { NomeTara = "-", PesoKg = 0m };

        // Reimpressão persistida: monta a etiqueta com dados do item + peso LÍQUIDO da pesagem individual.
        Func<EntradaProdutoPesagem, Task<bool>> reimprimirPesagem = pesagem =>
            TentarReimprimirEtiquetaPesagemAsync(
                ImpressaoEntradaServico.MontarEtiquetaPorPesagem(itemPersistido, pesagem, expirationDateTextBox.Text));

        string itemPedido = GetCellValue(row, "productionCodeColumn");

        // 054: EXCLUIR PESAGEM — só oferece o callback (e o menu) com a permissão EXATA EXCLUIR_PESAGEM.
        bool excluiuAlguma = false;
        Func<EntradaProdutoPesagem, Task<bool>>? excluirPesagem = null;
        if (PossuiPermissaoEntrada(PermissoesSistema.Acoes.ExcluirPesagem))
        {
            excluirPesagem = pesagem => ConfirmarEExcluirPesagemPersistidaAsync(
                pesagem, itemPedido, sucesso => excluiuAlguma = excluiuAlguma || sucesso);
        }

        using (PesagemMultiplaItemForm form = new(
            _balancaLeituraServico,
            itemPedido,
            tara,
            _idBalancaSelecionada,
            pesagens,
            imprimirPesagemAsync: null,
            reimprimirPesagemAsync: reimprimirPesagem,
            somenteConsulta: true,
            excluirPesagemAsync: excluirPesagem))
        {
            form.ShowDialog(this);
        }

        if (excluiuAlguma)
        {
            await ReidratarAposExclusaoPesagemAsync();
            return;
        }

        statusLabel.Text = $"Pesagens do item {itemPedido}. Total: {itemPersistido.PesoLiquidoTotalKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} kg.";
    }

    private async Task AbrirPesagemMultiplaParaLinhaAsync(DataGridViewRow linhaItem)
    {
        // §6: pesagem múltipla desabilitada durante persistência/retry da gravação local.
        if (PesagemBloqueadaNoFluxoLotes)
        {
            statusLabel.Text = MensagemRetryPersistenciaLotes;
            return;
        }

        if (!LinhaPertenceAoGridProducao(linhaItem))
        {
            statusLabel.Text = "Selecione um item válido do pedido para pesar.";
            return;
        }

        if (!LinhaPossuiTaraSelecionada(linhaItem, out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            await SelecionarTaraParaLinhaAsync(linhaItem);
            if (!LinhaPossuiTaraSelecionada(linhaItem, out tara))
            {
                return;
            }
        }

        string itemPedido = GetCellValue(linhaItem, "productionCodeColumn");
        string itemId = GetCellValue(linhaItem, "productionItemIdColumn");
        if (!long.TryParse(itemId, out long codigoItem) || codigoItem <= 0)
        {
            statusLabel.Text = "Item inválido para pesagem.";
            return;
        }

        if (!await GarantirLoteAtivoParaLinhaAsync(linhaItem))
        {
            return;
        }

        IReadOnlyList<EntradaProdutoPesagemEmMemoria> pesagensAtuais =
            _controller.ObterPesagensLoteAtivoOperacaoComLotes(codigoItem);

        // 058: EXCLUIR PESAGEM também no fluxo do lote ativo em memória. As pesagens recuperadas já estão
        // persistidas (carregam PK); o callback só é criado com a permissão EXATA EXCLUIR_PESAGEM. A janela
        // ignora automaticamente pesagens sem PK (em memória pura). Sucesso => reidrata sem restart.
        bool excluiuAlguma = false;
        Func<EntradaProdutoPesagem, Task<bool>>? excluirPesagem = null;
        if (PossuiPermissaoEntrada(PermissoesSistema.Acoes.ExcluirPesagem))
        {
            excluirPesagem = pesagem => ConfirmarEExcluirPesagemPersistidaAsync(
                pesagem, itemPedido, sucesso => excluiuAlguma = excluiuAlguma || sucesso);
        }

        using (PesagemMultiplaItemForm form = new(
            _balancaLeituraServico,
            itemPedido,
            tara,
            _idBalancaSelecionada,
            pesagensAtuais,
            (peso, origem, leitura) => Task.FromResult(_controller.RegistrarPesagemOperacaoComLotes(
                codigoItem,
                peso,
                EntradaProdutoQuantidadeSap.ConverterTaraParaKg(tara.PesoKg, "KG"),
                tara.CodigoTara,
                origem,
                string.Equals(origem, EntradaProdutoPesagemCalculos.OrigemBalanca, StringComparison.OrdinalIgnoreCase)
                    ? _idBalancaSelecionada
                    : null,
                leitura,
                DateTimeOffset.Now)),
            codigoLocalPesagem => Task.FromResult(_controller.CancelarPesagemOperacaoComLotes(codigoItem, codigoLocalPesagem)),
            imprimirPesagemAsync: null,
            reimprimirPesagemAsync: null,
            excluirPesagemAsync: excluirPesagem))
        {
            form.ShowDialog(this);
        }

        if (excluiuAlguma)
        {
            await ReidratarAposExclusaoPesagemAsync();
            return;
        }

        DataGridViewRow linhaAlvo = LocalizarLinhaProducaoPorItemId(itemId) ?? linhaItem;
        linhaAlvo.Tag = tara;
        if (!SincronizarLeiturasItemComOperacaoLotes(linhaAlvo, codigoItem))
        {
            statusLabel.Text = "Não foi possível sincronizar as pesagens do lote ativo.";
            return;
        }

        ApplyProductionRowStyle(linhaAlvo, linhaAlvo.Index);
        ClearGridSelection(productionDataGridView);
        linhaAlvo.Selected = true;
        SetCurrentProductionCell(linhaAlvo, "productionPesoLidoColumn");
        UpdateProductionCounters();
        statusLabel.Text = $"Pesagens atualizadas no lote em memória. Total do item: {EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(ObterLeiturasItem(codigoItem)).ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} kg.";
    }

    // Reimpressão por pesagem individual (permissão Reimprimir). Usada pela janela de pesagens (duplo clique).
    private async Task<bool> TentarReimprimirEtiquetaPesagemAsync(DadosEtiquetaMateriaPrima label)
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissaoImpressao(PermissoesSistema.Acoes.Reimprimir))
        {
            statusLabel.Text = "Reimpressão não realizada: usuário sem permissão de reimpressão.";
            return false;
        }

        try
        {
            await _impressaoEntrada.GarantirImpressoraDisponivelAsync();
            await _impressaoEntrada.ReimprimirEtiquetaMateriaPrimaAsync(label);
            return true;
        }
        catch (Exception ex)
        {
            await ErroUsuarioHelper.TratarAsync(
                "REIMPRESSAO_ETIQUETA_ERRO",
                ex,
                "ProcessoEntradaProdutoForm",
                "Não foi possível reimprimir a etiqueta da pesagem.");
            return false;
        }
    }

    private async Task SelecionarTaraParaLinhaAsync(DataGridViewRow linhaItem)
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            statusLabel.Text = "Banco desabilitado: não foi possível carregar taras.";
            return;
        }

        if (_idSetorSelecionado is not long codigoSetor || codigoSetor <= 0)
        {
            statusLabel.Text = "Usuário sem setor definido: não é possível selecionar tara.";
            MessageBox.Show(
                "Seu usuário não possui setor definido.\n\nDefina o setor padrão do usuário para selecionar a tara do item.",
                "Seleção de Tara",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // H4: apenas taras ativas do setor autorizado (nao confiar so no ComboBox/Form).
            IReadOnlyList<global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro> taras =
                await _taraController.ListarAtivasPorSetorAsync(codigoSetor, _fechamentoTelaCts.Token);
            if (!PodeAtualizarTela())
            {
                return;
            }

            if (taras.Count == 0)
            {
                statusLabel.Text = "Nenhuma tara ativa para o seu setor. Cadastre uma tara no setor antes de pesar.";
                MessageBox.Show(
                    "Nenhuma tara ativa para o seu setor.\n\nCadastre/ative uma tara do setor em Cadastro > Tara antes de registrar o peso do item.",
                    "Seleção de Tara",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string itemPedido = GetCellValue(linhaItem, "productionCodeColumn");
            using SelecaoTaraPesagemForm form = new(taras, itemPedido);
            if (form.ShowDialog(this) != DialogResult.OK || form.TaraSelecionada is null)
            {
                statusLabel.Text = "Seleção de tara cancelada.";
                return;
            }

            linhaItem.Tag = form.TaraSelecionada;
            string tooltipTara =
                $"Tara: {form.TaraSelecionada.NomeTara} " +
                $"({form.TaraSelecionada.PesoKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} kg)";
            foreach (DataGridViewCell cell in linhaItem.Cells)
            {
                cell.ToolTipText = tooltipTara;
            }

            statusLabel.Text = $"Tara '{form.TaraSelecionada.NomeTara}' selecionada para o item {itemPedido}.";
        }
        catch (OperationCanceledException)
        {
            // Tela fechada durante a carga das taras.
        }
        catch (Exception ex)
        {
            _controller.Sap.RegistrarDiagnostico($"ERRO ao carregar taras para pesagem.{Environment.NewLine}{ex}");
            statusLabel.Text = "Não foi possível carregar as taras cadastradas.";
            MessageBox.Show(
                "Não foi possível carregar as taras cadastradas. Acione o suporte.",
                "Seleção de Tara",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ProductionDataGridView_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isProductionStarted)
        {
            return;
        }

        if (productionDataGridView.SelectedCells.Count == 0 && productionDataGridView.SelectedRows.Count == 0)
        {
            return;
        }

        ClearGridSelection(productionDataGridView);
    }

    private void UpdateTitleBarLockState(bool locked)
    {
        menuHeaderLabel.Visible = !locked;
        minimizeWindowLabel.Visible = !locked;
        maximizeWindowLabel.Visible = !locked;
        closeWindowLabel.Visible = !locked;

        menuHeaderLabel.Enabled = !locked;
        minimizeWindowLabel.Enabled = !locked;
        maximizeWindowLabel.Enabled = !locked;
        closeWindowLabel.Enabled = !locked;

        customTitleBarPanel.Cursor = locked ? Cursors.Default : Cursors.SizeAll;
        companyLogoPictureBox.Cursor = customTitleBarPanel.Cursor;
        headerTitleLabel.Cursor = customTitleBarPanel.Cursor;
        headerSubtitleLabel.Cursor = customTitleBarPanel.Cursor;
    }

    private void UpdateStatusCardState(bool started)
    {
        Color statusColor = started ? ReadingStatusActiveColor : ReadingStatusInactiveColor;

        statusCard.BackColor = Color.Transparent;
        statusCard.FillColor = started ? Color.FromArgb(229, 247, 234) : Color.FromArgb(254, 232, 232);
        statusCard.BorderColor = started ? Color.FromArgb(187, 229, 199) : Color.FromArgb(248, 190, 190);

        statusCardIcon.Text = started ? "✓" : "!";
        statusCardIcon.ForeColor = statusColor;
        statusValueLabel.Text = started ? "ATIVA" : "INATIVA";
        statusValueLabel.ForeColor = statusColor;
        statusHintLabel.Text = started ? "Leitura liberada para registro" : "Leitura aguardando inicio";
        statusHintLabel.ForeColor = Color.FromArgb(98, 108, 124);

        statusCard.Invalidate(true);
        statusCardIcon.Invalidate();
        statusValueLabel.Invalidate();
        statusHintLabel.Invalidate();
    }

    private void ClearDangerActionHover()
    {
        Panel? hoveredPanel = _hoveredDangerActionPanel;
        _hoveredDangerActionPanel = null;
        hoveredPanel?.Invalidate();
    }

    private async void ProductionDataGridView_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        DataGridViewRow row = productionDataGridView.Rows[e.RowIndex];
        if (row.IsNewRow)
        {
            return;
        }

        // Duplo clique na LINHA principal abre a relação de pesagens (não imprime mais o total consolidado).
        // A reimpressão é feita por duplo clique em UMA pesagem, dentro da janela de detalhe.
        await AbrirDetalhePesagensAsync(row);
    }

    // Etiqueta de UMA pesagem individual (regra definitiva): peso = pesagem.PesoLiquidoKg; demais campos da linha.
    private DadosEtiquetaMateriaPrima ConstruirEtiquetaPorPesagem(DataGridViewRow row, EntradaProdutoPesagem pesagem)
        => ConstruirEtiquetaComPeso(
            row,
            pesagem.PesoLiquidoKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")));

    private DadosEtiquetaMateriaPrima ConstruirEtiquetaComPeso(DataGridViewRow row, string pesoFormatado)
    {
        string numeroPedido = pedidoComboBox.Text.Trim();
        string numeroItem = GetCellValue(row, "productionNumeroItemColumn");
        string codigoMaterial = GetCellValue(row, "productionCodeColumn");

        return new DadosEtiquetaMateriaPrima
        {
            CodigoProduto = codigoMaterial,
            DescricaoProduto = GetCellValue(row, "productionProductColumn"),
            LoteOrigem = numeroPedido,
            LoteInterno = numeroItem,
            DataFabricacao = stepLabel.Text,
            DataVencimento = expirationDateTextBox.Text,
            CertificadoSanitario = string.Empty,
            Sif = string.Empty,
            Fornecedor = lotTextBox.Text,
            NumeroNotaFiscal = string.Empty,
            Peso = pesoFormatado,
            NumeroPedido = numeroPedido,
            NumeroItem = numeroItem
        };
    }

    private static string FormatarPesoEtiquetaMateriaPrima(string peso)
    {
        if (TryParsePesoKg(peso, out decimal pesoKg))
        {
            return pesoKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        }

        return peso.Trim();
    }

    private DataGridViewRow? GetSelectedProductionRow()
    {
        DataGridViewRow? selectedRow = productionDataGridView.SelectedRows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => !row.IsNewRow);

        if (selectedRow is not null)
        {
            return selectedRow;
        }

        if (productionDataGridView.CurrentRow is not null && !productionDataGridView.CurrentRow.IsNewRow)
        {
            return productionDataGridView.CurrentRow;
        }

        return productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => row.Selected && !row.IsNewRow);
    }

    private static bool LinhaPossuiTaraSelecionada(
        DataGridViewRow linhaItem,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara)
    {
        tara = linhaItem.Tag as global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro;
        return tara is not null;
    }

    private IReadOnlyList<EntradaProdutoPesagem> ObterLeiturasItem(long codigoItem)
        => _leiturasPorItem.TryGetValue(codigoItem, out List<EntradaProdutoPesagem>? leituras)
            ? leituras
            : [];

    private bool AdicionarLeituraNaLinha(
        DataGridViewRow linhaItem,
        decimal pesoBruto,
        string origem,
        string leituraOriginal,
        out EntradaProdutoPesagem? pesagemCriada)
    {
        pesagemCriada = null;

        if (!long.TryParse(
                GetCellValue(linhaItem, "productionItemIdColumn"),
                out long codigoItem)
            || codigoItem <= 0)
        {
            statusLabel.Text = "Item invalido para registrar a leitura.";
            return false;
        }

        if (!LinhaPossuiTaraSelecionada(
                linhaItem,
                out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            statusLabel.Text = "Selecione a tara antes de registrar a leitura.";
            return false;
        }

        // Tarefa Entrada 23.2 (Ajuste 2): tara SEMPRE convertida para KG por um ponto central. A tara de
        // cadastro (TaraCadastro.PesoKg) já está em KG; passamos "KG" para manter a regra única e diagnosticável.
        decimal taraKg = EntradaProdutoQuantidadeSap.ConverterTaraParaKg(tara.PesoKg, "KG");
        decimal pesoLiquido = EntradaProdutoPesagemCalculos.CalcularPesoLiquido(pesoBruto, taraKg);
        if (!EntradaProdutoPesagemCalculos.LeituraTemPesoValido(pesoBruto, pesoLiquido))
        {
            statusLabel.Text = "O peso bruto deve ser maior que a tara.";
            return false;
        }

        List<EntradaProdutoPesagem> leituras =
            _leiturasPorItem.GetValueOrDefault(codigoItem) ?? [];

        // ATENCAO — ESTE METODO ESTA MORTO. AdicionarLeituraNaLinha e chamado unicamente por
        // RegistrarPesoLido, que NAO E MAIS CHAMADO por ninguem (um teste de 054 ate exige a sua
        // ausencia do fluxo de leitura). Em 24/11/2026 descobriu-se que, por isso, a trava de teto
        // escrita aqui pelo 069 NUNCA BLOQUEOU NADA desde que foi publicada.
        //
        // A trava viva esta em RegistrarPesoLidoOperacaoComLotesAsync, agora com a liberacao
        // gerencial do 070. Nao ponha regra nova aqui acreditando que ela protege a pesagem: nao
        // protege. A verificacao fica para o dia em que este caminho voltar a ser chamado — nunca
        // chamado e melhor do que chamado sem trava.
        if (!ToleranciaPermitePesagem(codigoItem, leituras, pesoLiquido, out string motivoTolerancia))
        {
            statusLabel.Text = motivoTolerancia;
            // Caixa de dialogo, nao so a barra de status: a pesagem foi RECUSADA, e quem esta na
            // balanca precisa parar e ler em vez de seguir achando que registrou.
            MessageBox.Show(this, motivoTolerancia, "Tolerância de recebimento",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Regra definitiva: cada leitura é UMA pesagem = UMA etiqueta. Guardamos a pesagem criada para
        // imprimir SOMENTE ela (pesagem.PesoLiquidoKg), nunca o total consolidado da linha.
        EntradaProdutoPesagem nova = EntradaProdutoPesagemCalculos.MontarLeitura(
            leituras,
            pesoBruto,
            taraKg,
            tara.CodigoTara,
            origem,
            _idBalancaSelecionada,
            leituraOriginal,
            DateTimeOffset.Now);
        leituras.Add(nova);
        _leiturasPorItem[codigoItem] = leituras;
        pesagemCriada = nova;
        return AtualizarTotaisDaLinha(linhaItem, leituras);
    }

    /// <summary>
    /// Carrega as tolerancias do centro uma vez por pedido (incremento 069).
    /// <para>
    /// Falha aqui NAO bloqueia: <see cref="_toleranciasDisponiveis"/> fica falso e a tela volta a se
    /// comportar como antes do 069. Nao saber se existe tolerancia e diferente de saber que nao existe,
    /// e parar a producao por indisponibilidade do CADASTRO seria a guarda sem valvula de saida que ja
    /// prendeu a operacao antes.
    /// </para>
    /// </summary>
    private async Task CarregarToleranciasDoPedidoAsync(IReadOnlyList<PedidoCompraSapItem> itens)
    {
        _toleranciasPorMaterial.Clear();
        _toleranciasDisponiveis = false;

        string centro = itens
            .Select(i => (i.Centro ?? string.Empty).Trim())
            .FirstOrDefault(c => c.Length > 0)
            ?? Modelo.Cadastro.ToleranciaRecebimentoMaterial.CentroPadrao;

        try
        {
            IReadOnlyList<Modelo.Cadastro.ToleranciaRecebimentoMaterial> tolerancias =
                await _controller.ListarToleranciasRecebimentoAsync(centro, CancellationToken.None);

            foreach (Modelo.Cadastro.ToleranciaRecebimentoMaterial tolerancia in tolerancias)
            {
                _toleranciasPorMaterial[tolerancia.Material] = tolerancia;
            }

            _toleranciasDisponiveis = true;
        }
        catch (Exception erro)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[Entrada] Tolerancias de recebimento indisponiveis: {erro.GetType().Name}. Pesagem segue sem o limite.");
        }
    }

    /// <summary>
    /// Todos os itens PESADOS estao dentro da tolerancia? Cobra os dois lados, porque aqui o
    /// recebimento esta sendo encerrado.
    /// <para>
    /// Item SEM nenhuma pesagem e ignorado de proposito: nao receber um item nao e "receber a menos",
    /// e um pedido parcial e situacao normal. Cobrar o piso de quem nao foi pesado impediria finalizar
    /// qualquer recebimento parcial.
    /// </para>
    /// <para>
    /// Relata TODOS os itens fora, nao so o primeiro: quem esta finalizando precisa saber o tamanho do
    /// problema de uma vez, em vez de descobrir um item por tentativa.
    /// </para>
    /// </summary>
    /// <summary>Um item que a finalização barrou, com os números que justificam o bloqueio.</summary>
    private sealed record ItemBloqueadoNaFinalizacao(
        string Material, string NumeroItem, VereditoTolerancia Veredito, string Mensagem);

    /// <summary>
    /// Decide se o recebimento pode ser finalizado e, quando o que barra é SÓ FALTA (abaixo do piso),
    /// oferece a liberação gerencial — o outro lado do pedido de 24/11/2026, "maior ou menor".
    /// <para>
    /// UMA autorização cobre a finalização inteira, e não uma por item: a decisão do gerente é "este
    /// recebimento pode ser fechado assim". O log, porém, continua com uma linha por item.
    /// </para>
    /// <para>
    /// Se entre os bloqueios houver qualquer coisa que NÃO seja falta — teto estourado, ou material
    /// sem cadastro de tolerância —, nada é oferecido e a finalização para como antes. Teto não se
    /// libera aqui porque já foi liberado (ou barrado) pesagem por pesagem, e falta de cadastro não é
    /// liberável por decisão sua.
    /// </para>
    /// </summary>
    private async Task<bool> ToleranciaLiberaFinalizacaoAsync()
    {
        if (ToleranciaPermiteFinalizar(out string mensagem, out List<ItemBloqueadoNaFinalizacao> bloqueados))
        {
            return true;
        }

        bool somenteFalta = bloqueados.Count > 0
            && bloqueados.All(b => b.Veredito.Resultado == ResultadoTolerancia.AbaixoDoPiso);

        if (!somenteFalta)
        {
            statusLabel.Text = mensagem;
            MessageBox.Show(this, mensagem, "Tolerância de recebimento",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        string detalhe = bloqueados.Count == 1
            ? DescreverEstouroParaDialogo(bloqueados[0].Material, bloqueados[0].Veredito)
            : string.Join(
                Environment.NewLine,
                bloqueados.Select(b => "• " + DescreverEstouroParaDialogo(b.Material, b.Veredito)));

        return await PedirLiberacaoGerencialAsync(
            [.. bloqueados.Select(b => MontarContextoEstouro(b.Material, b.NumeroItem, b.Veredito))],
            detalhe,
            mensagem);
    }

    private bool ToleranciaPermiteFinalizar(
        out string mensagem, out List<ItemBloqueadoNaFinalizacao> bloqueados)
    {
        mensagem = string.Empty;
        bloqueados = [];
        if (!_toleranciasDisponiveis)
        {
            return true;
        }

        List<string> problemas = [];

        foreach (DataGridViewRow row in productionDataGridView.Rows)
        {
            if (row.IsNewRow
                || !long.TryParse(GetCellValue(row, "productionItemIdColumn"), out long codigoItem)
                || codigoItem <= 0)
            {
                continue;
            }

            IReadOnlyList<EntradaProdutoPesagem> leituras = ObterLeiturasItem(codigoItem);
            if (leituras.Count == 0)
            {
                continue;
            }

            if (!_itensCarregadosPorCodigo.TryGetValue(codigoItem, out PedidoCompraSapItem? item))
            {
                continue;
            }

            string material = (item.CodigoMaterial ?? string.Empty).Trim();
            _toleranciasPorMaterial.TryGetValue(material, out Modelo.Cadastro.ToleranciaRecebimentoMaterial? tolerancia);

            VereditoTolerancia veredito = RegraToleranciaRecebimento.AvaliarNaFinalizacao(
                item.Quantidade,
                EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(leituras),
                tolerancia);

            if (veredito.Bloqueia)
            {
                string problema = RegraToleranciaRecebimento.Mensagem(material, veredito);
                problemas.Add(problema);
                bloqueados.Add(new ItemBloqueadoNaFinalizacao(
                    material, item.NumeroItem, veredito, problema));
            }
        }

        if (problemas.Count == 0)
        {
            return true;
        }

        mensagem = problemas.Count == 1
            ? problemas[0]
            : "Não é possível finalizar o recebimento:" + Environment.NewLine + Environment.NewLine
              + string.Join(Environment.NewLine + Environment.NewLine, problemas);
        return false;
    }

    /// <summary>
    /// Decide se a pesagem segue: avalia o teto e, havendo ESTOURO, oferece a liberacao gerencial
    /// (incremento 070). Devolve true quando pode registrar.
    /// <para>
    /// FALTA DE CADASTRO NAO ABRE O DIALOGO — decisao do usuario em 24/11/2026. Cadastrar leva
    /// segundos na tela de Tolerancia; deixar o gerente furar esse bloqueio faria o cadastro nunca
    /// ser feito, esvaziando a regra que ele mesmo escolheu.
    /// </para>
    /// <para>
    /// A liberacao vale para UMA pesagem: ela e pedida aqui, a cada estouro, e nao fica guardada.
    /// Nao ha flag de "liberado" sobrevivendo a esta chamada — e o que impede a autorizacao de virar
    /// porta aberta para o resto do turno.
    /// </para>
    /// </summary>
    private async Task<bool> ToleranciaLiberaPesagemAsync(long codigoItem, decimal pesoLiquidoNovo)
    {
        AvaliacaoToleranciaPesagem avaliacao = AvaliarToleranciaDaPesagem(
            codigoItem, ObterLeiturasItem(codigoItem), pesoLiquidoNovo);

        if (avaliacao.Permite)
        {
            return true;
        }

        VereditoTolerancia? veredito = avaliacao.Veredito;

        if (veredito?.Resultado != ResultadoTolerancia.AcimaDoTeto)
        {
            statusLabel.Text = avaliacao.Mensagem;
            MessageBox.Show(this, avaliacao.Mensagem, "Tolerância de recebimento",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return await PedirLiberacaoGerencialAsync(
            [MontarContextoEstouro(avaliacao.Material, avaliacao.NumeroItem, veredito)],
            DescreverEstouroParaDialogo(avaliacao.Material, veredito),
            avaliacao.Mensagem);
    }

    /// <summary>
    /// Congela os números do estouro no contexto que vai para o log. Congelados de propósito: a
    /// tolerância cadastrada pode ser alterada amanhã, e o log tem de continuar dizendo qual limite
    /// valia no momento em que o gerente autorizou.
    /// </summary>
    private ContextoEstouroTolerancia MontarContextoEstouro(
        string material, string numeroItem, VereditoTolerancia veredito)
        => new()
        {
            NumeroPedido = _numeroPedidoCarregado ?? string.Empty,
            NumeroItem = numeroItem,
            Material = material,
            QuantidadePedida = veredito.QuantidadePedida,
            QuantidadeApurada = veredito.QuantidadeRecebida,
            // O limite que o caso ESTOUROU: teto quando passou, piso quando faltou. Gravar sempre o
            // teto faria a linha de falta mostrar um numero que nada tem a ver com o bloqueio.
            LimiteAplicado = veredito.Resultado == ResultadoTolerancia.AbaixoDoPiso
                ? veredito.LimiteInferior
                : veredito.LimiteSuperior,
            PercentualTolerancia = veredito.Percentual,
            TipoEstouro = ContextoEstouroTolerancia.TipoDoResultado(veredito.Resultado),
            LoginOperador = EstadoSessaoUsuarioAtual.SessaoAtual?.Login,
            CodigoUsuarioOperador = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario,
            Terminal = ObterNomeTerminalAtual(),
        };

    private static string FormatarQuantidade(decimal? valor) => valor is null
        ? "-"
        : valor.Value.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private static string DescreverEstouroParaDialogo(string material, VereditoTolerancia veredito)
        => LiberacaoToleranciaDialog.DescreverEstouro(
            material,
            new VereditoToleranciaTexto(
                FormatarQuantidade(veredito.QuantidadePedida),
                FormatarQuantidade(veredito.QuantidadeRecebida),
                FormatarQuantidade(veredito.Resultado == ResultadoTolerancia.AbaixoDoPiso
                    ? veredito.LimiteInferior
                    : veredito.LimiteSuperior),
                $"{FormatarQuantidade(veredito.Percentual)} %"));

    /// <summary>
    /// Abre o diálogo de credencial e autoriza, ou não. Mantém o diálogo aberto quando a credencial
    /// é recusada — quem está em pé na balança merece tentar de novo sem recomeçar a pesagem.
    /// </summary>
    private async Task<bool> PedirLiberacaoGerencialAsync(
        IReadOnlyList<ContextoEstouroTolerancia> contextos,
        string detalhe,
        string mensagemDoBloqueio)
    {
        using LiberacaoToleranciaDialog dialogo = new(detalhe);

        while (dialogo.ShowDialog(this) == DialogResult.OK)
        {
            ResultadoAutorizacaoTolerancia resultado;
            try
            {
                resultado = await ObterAutorizacaoToleranciaServico()
                    .AutorizarAsync(dialogo.Usuario, dialogo.Senha, contextos, CancellationToken.None);
            }
            catch (Exception erro)
            {
                // Qualquer falha inesperada NAO libera: a duvida mantem o bloqueio.
                System.Diagnostics.Trace.TraceWarning(
                    $"[Tolerancia] Falha ao autorizar: {erro.GetType().Name}");
                resultado = ResultadoAutorizacaoTolerancia.Nao(
                    "Não foi possível validar a credencial agora. A pesagem continua bloqueada.");
            }

            if (resultado.Autorizada)
            {
                statusLabel.Text = resultado.Mensagem;
                return true;
            }

            dialogo.MostrarErro(resultado.Mensagem);
        }

        statusLabel.Text = mensagemDoBloqueio + " Liberação não autorizada.";
        return false;
    }

    /// <summary>O que a avaliacao do teto apurou, para a tela decidir o que fazer.</summary>
    private sealed record AvaliacaoToleranciaPesagem(
        bool Permite, string Mensagem, VereditoTolerancia? Veredito, string Material, string NumeroItem)
    {
        public static AvaliacaoToleranciaPesagem Liberada()
            => new(true, string.Empty, null, string.Empty, string.Empty);
    }

    /// <summary>
    /// O item pode receber mais esta pesagem? Cobra SO o teto — o piso e da finalizacao
    /// (ver <see cref="RegraToleranciaRecebimento"/>).
    /// <para>
    /// Devolve o VEREDITO, e nao so uma mensagem, porque quem chama precisa distinguir ESTOURO (que o
    /// gerente pode liberar, incremento 070) de FALTA DE CADASTRO (que so se resolve cadastrando) — e
    /// precisa dos numeros para mostrar ao gerente o que ele esta autorizando.
    /// </para>
    /// </summary>
    private AvaliacaoToleranciaPesagem AvaliarToleranciaDaPesagem(
        long codigoItem,
        IReadOnlyList<EntradaProdutoPesagem> leiturasAtuais,
        decimal pesoLiquidoNovo)
    {
        // Cadastro indisponivel nesta execucao: nao da para afirmar que falta tolerancia.
        if (!_toleranciasDisponiveis
            || !_itensCarregadosPorCodigo.TryGetValue(codigoItem, out PedidoCompraSapItem? item))
        {
            return AvaliacaoToleranciaPesagem.Liberada();
        }

        string material = (item.CodigoMaterial ?? string.Empty).Trim();
        _toleranciasPorMaterial.TryGetValue(material, out Modelo.Cadastro.ToleranciaRecebimentoMaterial? tolerancia);

        // TOTAL do item depois desta pesagem — nao a pesagem sozinha. Um item de 100 recebido em 5
        // pesagens de 20 seria barrado nas cinco se a conta fosse por pesagem.
        decimal totalProjetado =
            EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(leiturasAtuais) + pesoLiquidoNovo;

        VereditoTolerancia veredito = RegraToleranciaRecebimento.AvaliarDurantePesagem(
            item.Quantidade, totalProjetado, tolerancia);

        return veredito.Bloqueia
            ? new AvaliacaoToleranciaPesagem(
                false, RegraToleranciaRecebimento.Mensagem(material, veredito), veredito, material, item.NumeroItem)
            : AvaliacaoToleranciaPesagem.Liberada();
    }

    private bool ToleranciaPermitePesagem(
        long codigoItem,
        IReadOnlyList<EntradaProdutoPesagem> leiturasAtuais,
        decimal pesoLiquidoNovo,
        out string mensagem)
    {
        AvaliacaoToleranciaPesagem avaliacao =
            AvaliarToleranciaDaPesagem(codigoItem, leiturasAtuais, pesoLiquidoNovo);
        mensagem = avaliacao.Mensagem;
        return avaliacao.Permite;
    }

    private static bool AtualizarTotaisDaLinha(
        DataGridViewRow linhaItem,
        IReadOnlyList<EntradaProdutoPesagem> leituras)
    {
        decimal pesoBrutoTotal =
            EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(leituras);
        string origem = EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada(leituras);

        return SetCellValue(
                linhaItem,
                "productionPesoLidoColumn",
                pesoBrutoTotal > 0m
                    ? pesoBrutoTotal.ToString(
                        "0.###",
                        System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))
                    : string.Empty)
            && SetCellValue(linhaItem, "productionPesoOrigemColumn", origem);
    }

    private bool CancelarLeiturasDaLinha(DataGridViewRow linhaItem)
    {
        if (!long.TryParse(
                GetCellValue(linhaItem, "productionItemIdColumn"),
                out long codigoItem)
            || codigoItem <= 0)
        {
            return false;
        }

        try
        {
            int canceladas = _controller.CancelarPesagensOperacaoComLotes(codigoItem);
            if (canceladas <= 0)
            {
                return false;
            }

            bool sincronizado = SincronizarLeiturasItemComOperacaoLotes(linhaItem, codigoItem);
            UpdateProductionCounters();
            return sincronizado;
        }
        catch (Exception ex)
        {
            statusLabel.Text = GetFriendlyErrorMessage(ex);
            return false;
        }
    }

    private string ObterNomeTerminalAtual()
        => string.IsNullOrWhiteSpace(_contextoTerminal?.NomeTerminal)
            ? Environment.MachineName
            : _contextoTerminal.NomeTerminal;

    private string? PromptManualProductionWeight(string currentWeight)
    {
        using Form promptForm = new()
        {
            Text = "Peso manual",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 185),
            BackColor = Color.FromArgb(247, 248, 250)
        };

        Label messageLabel = new()
        {
            Text = "Informe o peso utilizado para a linha selecionada.",
            Dock = DockStyle.Top,
            Height = 62,
            Font = new Font("Cascadia Code", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 49, 56),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(18, 10, 18, 4)
        };

        TextBox weightTextBox = new()
        {
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Location = new Point(80, 76),
            Size = new Size(220, 31),
            TextAlign = HorizontalAlignment.Center,
            Text = currentWeight
        };

        FlowLayoutPanel buttonsPanel = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(74, 8, 0, 14),
            Height = 64
        };

        Button cancelButton = CreateDialogButton("Cancelar", Color.FromArgb(55, 60, 69), DialogResult.Cancel);
        Button okButton = CreateDialogButton("OK", Color.FromArgb(200, 78, 10), DialogResult.OK);
        cancelButton.Margin = new Padding(0, 0, 10, 0);

        buttonsPanel.Controls.Add(cancelButton);
        buttonsPanel.Controls.Add(okButton);
        promptForm.Controls.Add(messageLabel);
        promptForm.Controls.Add(weightTextBox);
        promptForm.Controls.Add(buttonsPanel);
        promptForm.AcceptButton = okButton;
        promptForm.CancelButton = cancelButton;
        promptForm.ActiveControl = weightTextBox;

        return promptForm.ShowDialog(this) == DialogResult.OK
            ? weightTextBox.Text.Trim()
            : null;
    }

    private static bool TryNormalizeWeight(string input, out string normalizedWeight, out string? errorMessage)
    {
        normalizedWeight = string.Empty;
        errorMessage = null;

        string cleaned = input.Trim().Replace(",", string.Empty).Replace(".", string.Empty);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            errorMessage = "Informe um peso valido.";
            return false;
        }

        if (!int.TryParse(cleaned, out int weight) || weight < 0)
        {
            errorMessage = "O peso precisa ser um numero inteiro maior ou igual a zero.";
            return false;
        }

        normalizedWeight = weight.ToString();
        return true;
    }

    private static string GetCellValue(DataGridViewRow row, string columnName)
    {
        DataGridViewCell? cell = TryGetCell(row, columnName);
        return Convert.ToString(cell?.Value) ?? string.Empty;
    }

    // Re-localiza a linha do item pelo id (productionItemIdColumn) no grid atual.
    private DataGridViewRow? LocalizarLinhaProducaoPorItemId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        return productionDataGridView.Rows
            .Cast<DataGridViewRow>()
            .FirstOrDefault(row => !row.IsNewRow
                && string.Equals(GetCellValue(row, "productionItemIdColumn"), itemId, StringComparison.Ordinal));
    }

    private static bool SetCellValue(DataGridViewRow row, string columnName, object? value)
    {
        DataGridViewCell? cell = TryGetCell(row, columnName);
        if (cell is null)
        {
            return false;
        }

        cell.Value = value;
        return true;
    }

    private static DataGridViewCell? TryGetCell(DataGridViewRow row, string columnName)
    {
        DataGridView? grid = row.DataGridView;
        if (grid is null)
        {
            return null;
        }

        DataGridViewColumn? column = grid.Columns.Contains(columnName)
            ? grid.Columns[columnName]
            : grid.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(item => string.Equals(item.Name, columnName, StringComparison.OrdinalIgnoreCase));

        return column is null || column.Index < 0 || column.Index >= row.Cells.Count
            ? null
            : row.Cells[column.Index];
    }

    private bool LinhaPertenceAoGridProducao(DataGridViewRow row)
        => ReferenceEquals(row.DataGridView, productionDataGridView);

    private void SetCurrentProductionCell(DataGridViewRow row, string columnName)
    {
        DataGridViewCell? cell = TryGetCell(row, columnName);
        if (cell is not null && LinhaPertenceAoGridProducao(row))
        {
            productionDataGridView.CurrentCell = cell;
        }
    }

    private async void ProcessoProdutoAcabadoForm_Shown(object? sender, EventArgs e)
    {
        if (!_acessoDiretoValidado)
        {
            _acessoDiretoValidado = true;
            if (!PossuiPermissaoEntrada(PermissoesSistema.Acoes.Consultar))
            {
                await AcaoNegadaHelper.RegistrarAcaoNegadaSeguroAsync(
                    PermissoesSistema.Modulos.ProcessoProducao,
                    PermissoesSistema.Rotinas.EntradaProduto,
                    PermissoesSistema.Acoes.Consultar,
                    "abrir diretamente a Entrada de Produto",
                    "ProcessoEntradaProdutoForm");
                MessageBox.Show(
                    "Você não possui permissão para acessar a Entrada de Produto.",
                    "Acesso negado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                Close();
                return;
            }
        }

        BeginInvoke(ClearGridSelections);
        StartProductionDevicesWarmUp();
    }

    private void LoadWindowIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, WindowIconPath);

        if (File.Exists(iconPath))
        {
            Icon = new Icon(iconPath);
        }
    }

    private void ConfigurarComboPedidos()
    {
        productionOrderIconPanel.Visible = false;
        pedidoComboBox.Enabled =
            PossuiPermissaoEntrada(PermissoesSistema.Acoes.Consultar)
            && PossuiPermissaoEntrada(PermissoesSistema.Acoes.SincronizarCache);
        pedidoComboBox.AutoCompleteMode = AutoCompleteMode.None;
        pedidoComboBox.AutoCompleteSource = AutoCompleteSource.None;
        AjustarLarguraComboPedido();
        pedidoComboBox.Items.Clear();
        pedidoComboBox.Text = string.Empty;
        lotTextBox.Text = string.Empty;
        stepLabel.Text = "--/--/----";
        finishedProductCodeTextBox.Text = string.Empty;
        finishedProductTextBox.Text = string.Empty;
        LimparItensPedidoCompra();

        pedidoComboBox.TextUpdate += PedidoComboBox_TextUpdate;
        pedidoComboBox.SelectedIndexChanged += PedidoComboBox_SelectedIndexChanged;
        // GATE 07: contrato de disparo determinístico — Enter (KeyDown) + saída do campo (Leave). Substitui
        // Validated (comprovadamente não confiável em runtime para Tab). Idempotência garantida por
        // AtualizarDadosPedidoSelecionadoAsync (PedidoJaCarregado + troca de CancellationTokenSource + gate).
        pedidoComboBox.KeyDown += PedidoComboBox_KeyDown;
        pedidoComboBox.Leave += PedidoComboBox_Leave;
        productionOrderShadowPanel.Resize += (_, _) => AjustarLarguraComboPedido();
        AtualizarDisponibilidadeInicioLeitura();
    }

    private void AjustarLarguraComboPedido()
    {
        const int margemDireita = 16;
        int larguraDisponivel = productionOrderShadowPanel.ClientSize.Width - pedidoComboBox.Left - margemDireita;
        pedidoComboBox.Width = Math.Max(120, larguraDisponivel);
        pedidoComboBox.DropDownWidth = Math.Max(220, pedidoComboBox.Width);
    }


    private void PedidoComboBox_TextUpdate(object? sender, EventArgs e)
    {
        if (_restaurandoPedidoOperacaoLotes)
        {
            return;
        }

        string numeroPedidoSolicitado = pedidoComboBox.Text.Trim();
        if (BloquearTrocaPedidoComOperacaoEmMemoria(numeroPedidoSolicitado, restaurarTexto: true))
        {
            return;
        }

        _consultaPedidoCts?.Cancel();
        if (!string.Equals(
                numeroPedidoSolicitado,
                _numeroPedidoCarregado,
                StringComparison.OrdinalIgnoreCase))
        {
            _numeroPedidoCarregado = string.Empty;
            LimparDadosPedidoSelecionado();
        }

        AtualizarDisponibilidadeInicioLeitura();
    }


    private async void PedidoComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _consultaPedidoTask = AtualizarDadosPedidoSelecionadoAsync();
        await _consultaPedidoTask;
        AtualizarDisponibilidadeInicioLeitura();
    }

    // GATE 07: Enter dispara a carga do pedido digitado. Handled/SuppressKeyPress evitam beep e ação dupla.
    private async void PedidoComboBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;

        _consultaPedidoTask = AtualizarDadosPedidoSelecionadoAsync();
        await _consultaPedidoTask;
        AtualizarDisponibilidadeInicioLeitura();
    }

    // GATE 07: saída do campo (Tab/clique fora) dispara a carga. Idempotente com Enter/seleção via
    // PedidoJaCarregado + troca de CancellationTokenSource em AtualizarDadosPedidoSelecionadoAsync.
    private async void PedidoComboBox_Leave(object? sender, EventArgs e)
    {
        _consultaPedidoTask = AtualizarDadosPedidoSelecionadoAsync();
        await _consultaPedidoTask;
        AtualizarDisponibilidadeInicioLeitura();
    }

    private async Task AtualizarDadosPedidoSelecionadoAsync()
    {
        string numeroPedido = pedidoComboBox.Text.Trim();
        if (!PodeAtualizarTela())
        {
            return;
        }

        if (BloquearTrocaPedidoComOperacaoEmMemoria(numeroPedido, restaurarTexto: true))
        {
            return;
        }

        if (PedidoJaCarregado(numeroPedido))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(numeroPedido) || !EstadoIntegracaoBanco.Habilitado)
        {
            _numeroPedidoCarregado = string.Empty;
            LimparDadosPedidoSelecionado();
            AtualizarDisponibilidadeInicioLeitura();
            return;
        }

        if (await BloquearAcaoSemPermissaoAsync(
                PermissoesSistema.Acoes.SincronizarCache,
                "sincronizar o pedido com o cache SAP"))
        {
            return;
        }

        CancellationTokenSource novaConsulta = CancellationTokenSource.CreateLinkedTokenSource(
            _fechamentoTelaCts.Token);
        CancellationTokenSource? consultaAnterior = Interlocked.Exchange(
            ref _consultaPedidoCts,
            novaConsulta);
        consultaAnterior?.Cancel();
        consultaAnterior?.Dispose();
        CancellationToken cancellationToken = novaConsulta.Token;
        bool gateAdquirido = false;

        try
        {
            await _consultaPedidoGate.WaitAsync(cancellationToken);
            gateAdquirido = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (!PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                return;
            }
            statusLabel.Text = $"Consultando pedido {numeroPedido} no SAP...";

            ResultadoConsultaPedido resultado =
                await _controller.ConsultarPedidoAsync(numeroPedido, _modoEntrada, cancellationToken);
            if (!PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                return;
            }
            if (!resultado.Sucesso)
            {
                _numeroPedidoCarregado = string.Empty;
                LimparDadosPedidoSelecionado();
                statusLabel.Text = resultado.Mensagem;
                if (PodeAtualizarTela())
                {
                    // GATE 105D: falha de CONSULTA (técnica ou não encontrado) usa o título de APLICAÇÃO do
                    // cenário — nunca "Pedido de Compra não liberado". A tela não conhece HTTP/exceções.
                    MessageBox.Show(
                        resultado.Mensagem,
                        string.IsNullOrWhiteSpace(resultado.TituloFalha)
                            ? "Consulta de pedido"
                            : resultado.TituloFalha,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!PodeAtualizarTela()
                || !PedidoSolicitadoAindaEhAtual(numeroPedido)
                || !string.Equals(
                    resultado.NumeroPedido,
                    numeroPedido,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Tarefa Entrada 23.1: pedido NAO aprovado/liberado no SAP nao abre operacao. Mantem o numero
            // digitado no combo, limpa os dados operacionais e nao habilita leitura/confirmacao/envio.
            if (!resultado.PedidoLiberado)
            {
                _numeroPedidoCarregado = string.Empty;
                LimparDadosPedidoSelecionado();
                // GATE 105D: status INDETERMINADO tem título/mensagem próprios (não afirma "não liberado").
                // Só um status funcional REAL (03/04/08/liberação não concluída) usa o texto de não liberado.
                bool statusIndeterminado =
                    resultado.CenarioFalhaSap == CenarioFalhaConsultaSap.StatusNegocioDesconhecido;
                string mensagemBloqueio = statusIndeterminado
                    ? resultado.Mensagem
                    : MontarMensagemPedidoNaoLiberado(resultado);
                string tituloBloqueio = statusIndeterminado && !string.IsNullOrWhiteSpace(resultado.TituloFalha)
                    ? resultado.TituloFalha
                    : "Pedido de Compra não liberado";
                statusLabel.Text = resultado.MotivoBloqueioLiberacao;
                AtualizarDisponibilidadeInicioLeitura();
                if (PodeAtualizarTela())
                {
                    MessageBox.Show(
                        mensagemBloqueio,
                        tituloBloqueio,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return;
            }

            // Tarefa Entrada 24.1 (Ajuste 9/10): pedido liberado, mas SEM item compatível com o modo desta tela
            // (ou todos Indefinidos) — não carrega grid vazia como sucesso; alerta e mantém o número no combo.
            if (!resultado.PedidoTemItensDoModo)
            {
                _numeroPedidoCarregado = string.Empty;
                LimparDadosPedidoSelecionado();
                statusLabel.Text = resultado.MotivoBloqueioModo;
                AtualizarDisponibilidadeInicioLeitura();
                if (PodeAtualizarTela())
                {
                    MessageBox.Show(
                        resultado.MotivoBloqueioModo,
                        _configuracaoTela.NomeModulo,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return;
            }

            lotTextBox.Text = resultado.Fornecedor;
            stepLabel.Text = resultado.DataPedido.HasValue
                ? resultado.DataPedido.Value.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))
                : "--/--/----";
            finishedProductCodeTextBox.Text = resultado.TipoPedido;
            finishedProductTextBox.Text = string.Equals(resultado.TipoPedido, TipoPedidoNormal, StringComparison.OrdinalIgnoreCase)
                ? DescricaoPedidoNormal
                : string.Empty;

            _itensCarregadosPorCodigo.Clear();
            foreach (PedidoCompraSapItem itemAutorizado in resultado.ItensAutorizados)
            {
                _itensCarregadosPorCodigo[itemAutorizado.CodigoItem] = itemAutorizado;
            }

            await CarregarToleranciasDoPedidoAsync(resultado.ItensAutorizados);

            PreencherItensPedidoCompra(resultado.ItensAutorizados);
            await RehidratarLancamentoLocalPersistidoAsync(numeroPedido);
            _numeroPedidoCarregado = numeroPedido;
            statusLabel.Text = resultado.ItensOcultados > 0
                ? $"{resultado.Mensagem} {resultado.ItensOcultados} item(ns) fora do centro/deposito autorizado nao exibido(s)."
                : resultado.Mensagem;
            AtualizarDisponibilidadeInicioLeitura();
        }
        catch (OperationCanceledException)
        {
            // Nova consulta ou fechamento da tela cancelou esta operacao.
        }
        catch (IntegracaoSapBloqueadaException ex)
        {
            if (PodeAtualizarTela() && PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                statusLabel.Text = ex.Message;
            }
        }
        catch (Exception ex)
        {
            _controller.Sap.RegistrarDiagnostico(
                $"ERRO ao carregar dados do pedido {numeroPedido}.{Environment.NewLine}{ex}");
            if (!PodeAtualizarTela() || !PedidoSolicitadoAindaEhAtual(numeroPedido))
            {
                return;
            }

            LimparDadosPedidoSelecionado();
            _numeroPedidoCarregado = string.Empty;
            statusLabel.Text = "Nao foi possivel carregar os dados do pedido selecionado.";
            AtualizarDisponibilidadeInicioLeitura();
        }
        finally
        {
            if (gateAdquirido)
            {
                _consultaPedidoGate.Release();
            }

            if (ReferenceEquals(
                    Interlocked.CompareExchange(
                        ref _consultaPedidoCts,
                        null,
                        novaConsulta),
                    novaConsulta))
            {
                novaConsulta.Dispose();
            }
        }
    }

    private bool PedidoSolicitadoAindaEhAtual(string numeroPedido)
        => PodeAtualizarTela()
            && string.Equals(
                pedidoComboBox.Text.Trim(),
                numeroPedido,
                StringComparison.OrdinalIgnoreCase);

    // Tarefa Entrada 24.1 (Ajuste 3/16): título/subtítulo do header conforme o modo (Matéria-Prima × Químicos).
    private void AplicarConfiguracaoModoEntrada()
    {
        Text = _configuracaoTela.TituloTela;
        headerTitleLabel.Text = _configuracaoTela.TituloTela;
        headerSubtitleLabel.Text = _configuracaoTela.SubtituloTela;
    }

    // Tarefa Entrada 23.2 (Ajuste 7): confirmação do envio 101 mostrando o peso líquido (KG) em pt-BR.
    // Formatação de EXIBIÇÃO (vírgula) — o payload usa serialização invariante ("1.5"), nunca este texto.
    // internal static para teste direto (InternalsVisibleTo).
    internal static string MontarConfirmacaoEnvio101(long codigoLancamento, decimal pesoLiquidoTotalKg)
    {
        string liquido = pesoLiquidoTotalKg.ToString(
            "0.000", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        return $"Entrada SAP 101 será enviada com peso líquido de {liquido} KG.\r\n\r\n"
            + $"Confirma criar o movimento 101 no SAP DE HOMOLOGAÇÃO para o lançamento {codigoLancamento}?\r\n\r\n"
            + "Será criado um documento de material (entrada) vinculado ao pedido. Esta é uma etapa "
            + "separada da gravação local e só ocorre após esta confirmação.";
    }

    // Tarefa Entrada 23.1 (Ajuste 5): mensagem amigável de pedido não liberado por status/liberação.
    // internal static para teste direto (InternalsVisibleTo) sem instanciar o Form.
    internal static string MontarMensagemPedidoNaoLiberado(ResultadoConsultaPedido resultado)
    {
        string pedido = string.IsNullOrWhiteSpace(resultado.NumeroPedido) ? "(não informado)" : resultado.NumeroPedido.Trim();
        string status = string.IsNullOrWhiteSpace(resultado.StatusProcessamento) ? "(não informado)" : resultado.StatusProcessamento.Trim();
        string descricao = string.IsNullOrWhiteSpace(resultado.DescricaoStatusProcessamento)
            ? "Status SAP não mapeado"
            : resultado.DescricaoStatusProcessamento.Trim();

        if (resultado.LiberacaoNaoConcluida)
        {
            return "Pedido de compra com liberação não concluída no SAP.\r\n\r\n"
                + $"Pedido: {pedido}\r\n"
                + "A entrada não pode ser realizada até a conclusão da liberação.";
        }

        if (string.Equals(resultado.StatusProcessamento?.Trim(), ValidadorLiberacaoPedidoCompra.StatusRejeitado, StringComparison.Ordinal))
        {
            return "Pedido de compra rejeitado no SAP.\r\n\r\n"
                + $"Pedido: {pedido}\r\n"
                + "Status atual: 08 - Rejeitado.\r\n\r\n"
                + "A entrada não pode ser realizada para pedido rejeitado.";
        }

        return "Pedido de compra ainda não liberado/aprovado no SAP.\r\n\r\n"
            + $"Pedido: {pedido}\r\n"
            + $"Status atual: {status} - {descricao}\r\n\r\n"
            + "A entrada não pode ser realizada enquanto o pedido não estiver aprovado/liberado.";
    }

    private void LimparDadosPedidoSelecionado()
    {
        lotTextBox.Text = string.Empty;
        stepLabel.Text = "--/--/----";
        finishedProductCodeTextBox.Text = string.Empty;
        finishedProductTextBox.Text = string.Empty;
        LimparItensPedidoCompra();
    }

    private bool PedidoJaCarregado(string numeroPedido)
        => !string.IsNullOrWhiteSpace(numeroPedido)
            && string.Equals(numeroPedido, _numeroPedidoCarregado, StringComparison.OrdinalIgnoreCase)
            && productionDataGridView.Rows
                .Cast<DataGridViewRow>()
                .Any(row => !row.IsNewRow);

    private void LimparItensPedidoCompra()
    {
        _leiturasPorItem.Clear();
        _tarasPorItem.Clear();
        _itensPedidoCarregados = [];
        _codigoLancamentoPersistido = null;
        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "aguardando finalização do lançamento");
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "aguardando gravação local");
        productionActionsButton.Enabled = false;
        productionDataGridView.Rows.Clear();
        ExibirEstadoVazioItensPedido(
            "Informe ou selecione um Pedido de Compra para carregar os itens.",
            $"A tela exibirá apenas itens compatíveis com {_configuracaoTela.TituloTela}.");
        UpdateProductionCounters();
        UpdateProductionGridFooter();
    }

    private void PreencherItensPedidoCompra(IReadOnlyList<PedidoCompraSapItem> itens)
    {
        _leiturasPorItem.Clear();
        _tarasPorItem.Clear();
        _itensPedidoCarregados = itens.ToList();
        _codigoLancamentoPersistido = null;
        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Pendente,
            "pedido carregado; lançamento ainda não gravado");
        AtualizarEstadoVisualIntegracaoSap(
            EstadoVisualIntegracaoSap.AguardandoGravacaoLocal,
            "aguardando gravação local");
        productionActionsButton.Enabled = false;
        AplicarFiltroItensPedido();
    }

    // 054: §7 confirmação + chamada de exclusão da pesagem PERSISTIDA (backend revalida tudo na transação).
    private async Task<bool> ConfirmarEExcluirPesagemPersistidaAsync(
        EntradaProdutoPesagem pesagem, string itemPedido, Action<bool> registrarResultado)
    {
        if (pesagem.CodigoEntradaProdutoPesagem is not long codigo || codigo <= 0)
        {
            return false;
        }

        string lote = pesagem.CodigoEntradaProdutoLote?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
        string pesoLiquido = pesagem.PesoLiquidoKg.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        DialogResult confirmacao = MessageBox.Show(
            "Excluir esta pesagem local?" + Environment.NewLine + Environment.NewLine
            + $"Pedido: {_numeroPedidoCarregado}" + Environment.NewLine
            + $"Item: {itemPedido}" + Environment.NewLine
            + $"Lote: {lote}" + Environment.NewLine
            + $"Pesagem: {codigo}" + Environment.NewLine
            + $"Peso líquido: {pesoLiquido} kg" + Environment.NewLine + Environment.NewLine
            + "Nenhuma operação será executada no SAP.",
            "Excluir pesagem",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmacao != DialogResult.Yes)
        {
            return false;
        }

        ResultadoExclusaoPesagem resultado;
        try
        {
            resultado = await _controller.ExcluirPesagemLocalAsync(codigo, _fechamentoTelaCts.Token);
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
            return false;
        }

        statusLabel.Text = resultado.Mensagem;
        if (!resultado.Sucesso)
        {
            MessageBox.Show(resultado.Mensagem, "Excluir pesagem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        registrarResultado(true);
        return true;
    }

    // 054/§18: pós-sucesso sem restart — descarta estado em memória e força recarregar a MESMA PO (pesagens
    // persistidas ativas), recalcula peso/elegibilidade e reidrata. Árvore vazia/cancelada => sem lançamento elegível.
    private async Task ReidratarAposExclusaoPesagemAsync()
    {
        _leiturasPorItem.Clear();
        _codigoLancamentoPersistido = null;
        _numeroPedidoCarregado = null;
        await AtualizarDadosPedidoSelecionadoAsync();
    }

    // 12G-D: após carregar um PO, reconhece um lançamento local FINALIZADO_LOCAL/ERRO_SAP já persistido
    // (restart) — reidrata _codigoLancamentoPersistido, projeta o peso por item na grade e reavalia a
    // prontidão para habilitar o botão ORIGINAL de envio. Read-only: não cria pesagem/lançamento nem altera dados.
    private async Task RehidratarLancamentoLocalPersistidoAsync(string numeroPedido)
    {
        if (_codigoLancamentoPersistido is not null
            || string.IsNullOrWhiteSpace(numeroPedido)
            || _itensPedidoCarregados.Count == 0)
        {
            return;
        }

        long? codigo;
        try
        {
            codigo = await _controller.RecuperarCodigoLancamentoLocalPorPedidoAsync(
                numeroPedido, _fechamentoTelaCts.Token);
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            // Reidratação é best-effort: falha de leitura não bloqueia a tela nem cria dados.
            return;
        }

        if (codigo is not long codigoLancamento || codigoLancamento <= 0)
        {
            return;
        }

        _codigoLancamentoPersistido = codigoLancamento;

        try
        {
            var itensPersistidos = await _controller.ListarItensPersistidosParaEnvioAsync(
                codigoLancamento, _fechamentoTelaCts.Token);
            Dictionary<string, decimal> pesoPorItem = itensPersistidos
                .GroupBy(item => NormalizarNumeroItem(item.NumeroItem))
                .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(item => item.PesoLiquidoKg));

            foreach (DataGridViewRow row in productionDataGridView.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }

                string numeroItem = NormalizarNumeroItem(GetCellValue(row, "productionNumeroItemColumn"));
                if (pesoPorItem.TryGetValue(numeroItem, out decimal peso) && peso > 0m)
                {
                    row.Cells["productionPesoLidoColumn"].Value =
                        peso.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
                }
            }

            UpdateProductionCounters();
        }
        catch (OperationCanceledException) when (_fechamentoTelaCts.IsCancellationRequested)
        {
            return;
        }
        catch
        {
            // O peso exibido é cosmético; o envio usa o lançamento persistido no banco.
        }

        AtualizarEstadoVisualLocal(
            EstadoVisualLocalEntrada.Gravado,
            $"lançamento {codigoLancamento} recuperado do banco");
        await AtualizarProntidaoEnvioSapAsync();
    }

    private static string NormalizarNumeroItem(string? valor)
    {
        string texto = valor?.Trim() ?? string.Empty;
        return int.TryParse(texto, out int numero)
            ? numero.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : texto;
    }

    private void AplicarFiltroItensPedido()
    {
        productionDataGridView.Rows.Clear();

        if (_itensPedidoCarregados.Count == 0)
        {
            ExibirEstadoVazioItensPedido(
                "Informe ou selecione um Pedido de Compra para carregar os itens.",
                $"A tela exibirá apenas itens compatíveis com {_configuracaoTela.TituloTela}.");
            AtualizarDisponibilidadeInicioLeitura();
            UpdateProductionCounters();
            UpdateProductionGridFooter();
            return;
        }

        string pesquisa = productionSearchTextBox.Text.Trim();
        List<PedidoCompraSapItem> itensFiltrados = _itensPedidoCarregados
            .Where(item => ItemAtendeFiltroOperacional(item)
                && ItemAtendePesquisa(item, pesquisa))
            .ToList();

        foreach (PedidoCompraSapItem item in itensFiltrados)
        {
            AdicionarLinhaItemPedido(item);
        }

        ClearGridSelection(productionDataGridView);

        if (itensFiltrados.Count == 0)
        {
            string mensagem = string.IsNullOrWhiteSpace(pesquisa)
                ? "Nenhum item encontrado para o filtro selecionado."
                : "Nenhum item encontrado para o filtro informado.";
            ExibirEstadoVazioItensPedido(mensagem);
        }
        else
        {
            OcultarEstadoVazioItensPedido();
        }

        AtualizarDisponibilidadeInicioLeitura();
        UpdateProductionCounters();
        UpdateProductionGridFooter();
    }

    private bool ItemAtendeFiltroOperacional(PedidoCompraSapItem item)
    {
        bool possuiPeso = ItemPossuiPesoRegistrado(item.CodigoItem);
        bool possuiSaldo = ItemPossuiSaldo(item);

        return _filtroItensAtual switch
        {
            FiltroItensEntrada.PendentesPesagem => !possuiPeso,
            FiltroItensEntrada.PesadosLocalmente => possuiPeso,
            FiltroItensEntrada.PendentesSap => possuiPeso
                && _codigoLancamentoPersistido.HasValue
                && _estadoIntegracaoSapAtual is not EstadoVisualIntegracaoSap.Enviado
                && _estadoIntegracaoSapAtual is not EstadoVisualIntegracaoSap.Falha,
            FiltroItensEntrada.EnviadosSap => possuiPeso
                && _estadoIntegracaoSapAtual == EstadoVisualIntegracaoSap.Enviado,
            FiltroItensEntrada.ErroSap => possuiPeso
                && _estadoIntegracaoSapAtual == EstadoVisualIntegracaoSap.Falha,
            FiltroItensEntrada.ComSaldo => possuiSaldo,
            FiltroItensEntrada.SemSaldo => !possuiSaldo,
            _ => true
        };
    }

    private bool ItemAtendePesquisa(PedidoCompraSapItem item, string pesquisa)
    {
        if (string.IsNullOrWhiteSpace(pesquisa))
        {
            return true;
        }

        string texto = string.Join(
            " ",
            item.CodigoMaterial,
            item.Descricao,
            item.UnidadeMedida,
            item.Quantidade?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            item.NumeroItem,
            item.CodigoItem.ToString(),
            ItemPossuiPesoRegistrado(item.CodigoItem) ? "pesado localmente" : "pendente pesagem",
            _codigoLancamentoPersistido.HasValue ? "pendente sap" : "aguardando local");

        return texto.Contains(pesquisa, StringComparison.OrdinalIgnoreCase);
    }

    private void AdicionarLinhaItemPedido(PedidoCompraSapItem item)
    {
        var cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        string quantidade = item.Quantidade.HasValue
            ? item.Quantidade.Value.ToString("0.###", cultura)
            : string.Empty;
        int rowIndex = productionDataGridView.Rows.Add();
        DataGridViewRow row = productionDataGridView.Rows[rowIndex];
        row.Cells["productionCodeColumn"].Value = item.CodigoMaterial ?? string.Empty;
        row.Cells["productionProductColumn"].Value = item.Descricao ?? string.Empty;
        row.Cells["productionQuantityColumn"].Value = quantidade;
        row.Cells["productionWeightColumn"].Value = item.UnidadeMedida ?? string.Empty;
        row.Cells["productionPesoLidoColumn"].Value = string.Empty;
        row.Cells["productionItemIdColumn"].Value = item.CodigoItem.ToString();
        row.Cells["productionPesoOrigemColumn"].Value = string.Empty;
        row.Cells["productionNumeroItemColumn"].Value = item.NumeroItem;
        if (_tarasPorItem.TryGetValue(item.CodigoItem, out global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro? tara))
        {
            row.Tag = tara;
        }

        if (_leiturasPorItem.TryGetValue(item.CodigoItem, out List<EntradaProdutoPesagem>? leituras))
        {
            AtualizarTotaisDaLinha(row, leituras);
        }

        ApplyProductionRowStyle(row, rowIndex);
    }

    private bool ItemPossuiPesoRegistrado(long codigoItem)
        => _leiturasPorItem.TryGetValue(codigoItem, out List<EntradaProdutoPesagem>? leituras)
            && EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(leituras) > 0m;

    private bool ItemPossuiSaldo(PedidoCompraSapItem item)
    {
        if (!item.Quantidade.HasValue)
        {
            return true;
        }

        decimal utilizado = _leiturasPorItem.TryGetValue(item.CodigoItem, out List<EntradaProdutoPesagem>? leituras)
            ? EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(leituras)
            : 0m;
        return item.Quantidade.Value - utilizado > 0m;
    }

    private void LoadMockData()
    {
        materialDataGridView.Rows.Clear();
        materialDataGridView.Rows.Add("●", "22273", "POLUCHINHA SMALL TWIST STIX BEEF 50PK", "07032302", "28/05/2026", "0");
        materialDataGridView.Rows.Add("●", "27215", "CX 01 MTHM - 450X300X65", "23012615", "22/01/2028", "808");
        materialDataGridView.Rows.Add("●", "27275", "ETIQ COUCHE ZEBRA 100X43 - PET", "09012607", "06/07/2026", "72");
        materialDataGridView.Rows.Add("●", "27277", "ETIQ COUCHE ZEBRA 80X50 - PET", "12032601", "09/09/2026", "6987");

        productionDataGridView.Rows.Clear();
    }

    private static void ApplyGridStyle(DataGridView grid)
    {
        for (int rowIndex = 0; rowIndex < grid.Rows.Count; rowIndex++)
        {
            ApplyProductionRowStyle(grid.Rows[rowIndex], rowIndex);
        }

        DataGridViewColumn? printColumn = grid.Columns["productionPrintColumn"];
        if (printColumn is not null)
        {
            printColumn.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        ClearGridSelection(grid);
    }

    private static void ApplyProductionRowStyle(DataGridViewRow row, int rowIndex)
    {
        row.DefaultCellStyle.BackColor = rowIndex % 2 == 0 ? RowLight : RowGreen;
        row.DefaultCellStyle.ForeColor = Color.FromArgb(45, 49, 56);
    }

    private void ClearGridSelections()
    {
        ClearGridSelection(materialDataGridView);
        ClearGridSelection(productionDataGridView);
    }

    private static void ClearGridSelection(DataGridView grid)
    {
        grid.ClearSelection();
        grid.CurrentCell = null;
    }
}


