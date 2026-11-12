using System.Runtime.InteropServices;
using Cadastro = FugaPET_Dev.Tela.Cadastro;
using Consulta = FugaPET_Dev.Tela.Consulta;
using Processo = FugaPET_Dev.Tela.Processo;

using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Status;
using FugaPET_Dev.Servicos.Entrada;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Status;

namespace FugaPET_Dev.Tela;

public partial class PainelInicialForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fugapet.ico";
    private const string RotinaLeituraProducao = PermissoesSistema.Rotinas.LeituraProducao;
    private static readonly Color SidebarActiveColor = Color.FromArgb(250, 105, 26);
    private static readonly Color SidebarInactiveColor = Color.Transparent;
    private static readonly Color SidebarTextColor = Color.FromArgb(229, 231, 235);

    private System.Windows.Forms.Timer? _footerClockTimer;
    private ProcessoProducaoForm? _processoProducaoForm;
    private CadastroForm? _cadastroForm;
    private SegurancaForm? _segurancaForm;
    private Control? _currentContentView;
    private readonly Dictionary<Panel, (Label Icon, Label Text)> _sidebarMenuItems = new();
    private readonly ToolTip _menuRotinaDesenvolvimentoToolTip = new();
    private readonly FugaPET_Dev.Servicos.Auditoria.AuditoriaServico _auditoriaServico =
        FugaPET_Dev.Controle.FabricaControladoresCadastro.CriarAuditoriaServico();

    // Pre-carregamento assincrono dos pedidos da Entrada (aquecimento do cache apos o login).
    private readonly PreCarregamentoPedidosEntradaServico _preCarregamentoPedidos = new();
    private readonly IOrdemProducaoCacheServico _preCarregamentoOrdensProducao = OrdemProducaoCacheServico.Compartilhado;
    private readonly CancellationTokenSource _fechamentoPreCarregamentoCts = new();
    private Label? _preCarregamentoStatusLabel;

    public PainelInicialForm()
    {
        InitializeComponent();
        Text = global::FugaPET_Dev.MarcaProduto.NomeCompleto;
        global::FugaPET_Dev.Tela.Comum.ModoDemonstracaoHelper.AplicarFaixaSeModoDemonstracao(this);
        sidebarUserNameLabel.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterLogin();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        ConfigureSidebarMenu();
        ConfigureFooterDate();
        _currentContentView = contentLayout;
        KeyPreview = true;
        CriarStatusPreCarregamentoPedidos();
        Shown += (_, _) => ApplyRuntimeVisuals();
        // GATE 10 (fidelidade visual): substitui os logos FUGA COUROS embutidos (PainelInicialForm.resx) pelo
        // novo logo FUGA PET recuperado (Resources.fuga_2026_logo → FugaPet sem fundo 1.png). Só imagem; assinado
        // após ApplyRuntimeVisuals para prevalecer. Bytes do asset intocados.
        Shown += (_, _) =>
        {
            // GATE 10-D: sidebar (fundo escuro #8F3405) usa o WORDMARK FUGA PET COMPLETO em versão branca
            // (F laranja original + "FUGA"/"PET" em branco, transparente) — asset derivado determinístico do
            // logo oficial. Centro/Login mantêm o wordmark charcoal sobre branco.
            string marcaPath = Path.Combine(AppContext.BaseDirectory, "Servicos\\image\\FugaPet branco sem fundo.png");
            if (File.Exists(marcaPath))
            {
                using Image marcaTmp = Image.FromFile(marcaPath);
                sidebarLogoPictureBox.Image = new Bitmap(marcaTmp);
            }
            else
            {
                sidebarLogoPictureBox.Image = global::FugaPET_Dev.Properties.Resources.fuga_2026_logo;
            }

            contentBrandPictureBox.Image = global::FugaPET_Dev.Properties.Resources.fuga_2026_logo;
        };
        Shown += (_, _) => AplicarPermissoesPorPerfil();
        Shown += async (_, _) => await AtualizarStatusIndustrialAsync();
        // Apos o MainForm aparecer (usuario ja autenticado), aquece o cache de pedidos da Entrada
        // em segundo plano. Nao bloqueia a UI nem o login.
        Shown += (_, _) => IniciarPreCarregamentoPedidosEntrada();
        Shown += (_, _) => IniciarPreCarregamentoOrdensProducao();
        FormClosing += (_, _) => _fechamentoPreCarregamentoCts.Cancel();
        KeyDown += PainelInicialForm_KeyDown;

        // Legibilidade: duplica (2.0x) a fonte de TODO o corpo (menu lateral + conteúdo), EXCETO cabeçalho e
        // rodapé (que ficam no tamanho padrão). Roda uma única vez, no último Shown — após conteúdo, visuais de
        // runtime e permissões já aplicados — e adapta as alturas ao novo tamanho.
        bool escalaCorpoAplicada = false;
        Shown += (_, _) =>
        {
            if (escalaCorpoAplicada)
            {
                return;
            }
            escalaCorpoAplicada = true;

            SuspendLayout();
            foreach (Control corpo in new Control[] { sidebarPanel, contentScrollPanel })
            {
                global::FugaPET_Dev.Tela.Processo.LegibilidadeRecebimentoMercadoria.AplicarEscalaFonte(
                    corpo, global::FugaPET_Dev.Tela.Processo.LegibilidadeRecebimentoMercadoria.Escala);
                global::FugaPET_Dev.Tela.Processo.LegibilidadeRecebimentoMercadoria.AdaptarAlturasParaFonte(corpo);
            }
            ResumeLayout(true);
            PerformLayout();

            // Altura + espaçamento dos itens (o reempilhamento vertical funciona em runtime): dá altura para o
            // item de 2 linhas ("Processo de Produção") e separa os módulos. Larguras/rotulos já vêm do Designer.
            ReempilharSidebar();
            AjustarRodapeSidebar();
        };

        // Sidebar 2.0x SEM cortar: os rótulos ficam em AutoSize (crescem com a fonte e nunca cortam). A largura
        // da barra e dos itens vem do Designer (coluna 380 / itens 360), então o rótulo grande cabe dentro do
        // item. AutoSize é setado aqui no init (fora de qualquer SuspendLayout) para valer de fato.
        foreach (Label rotulo in new[]
                 {
                     menuInicioText, menuCadastroText, menuLeituraText, menuEtiquetasText, menuHistoricoText,
                     menuRelatoriosText, menuSapText, menuConfigText, menuSegurancaText,
                 })
        {
            rotulo.AutoSize = true;
        }
    }

    // Reempilha os itens do menu com mais altura (cabe o item de 2 linhas) e mais espaço entre eles, centralizando
    // ícone e texto verticalmente. Largura vem do Designer; aqui só mexe em altura/posição (que aplicam em runtime).
    private void ReempilharSidebar()
    {
        const int altura = 62;
        const int espaco = 16;
        const int larguraIcone = 44;

        (Control item, Control icone, Control texto)[] itens =
        [
            (menuItemInicio, menuInicioIcon, menuInicioText),
            (menuItemCadastro, menuCadastroIcon, menuCadastroText),
            (menuItemLeitura, menuLeituraIcon, menuLeituraText),
            (menuItemEtiquetas, menuEtiquetasIcon, menuEtiquetasText),
            (menuItemHistorico, menuHistoricoIcon, menuHistoricoText),
            (menuItemRelatorios, menuRelatoriosIcon, menuRelatoriosText),
            (menuItemSap, menuSapIcon, menuSapText),
            (menuItemConfig, menuConfigIcon, menuConfigText),
            (menuItemSeguranca, menuSegurancaIcon, menuSegurancaText),
        ];

        int larguraTextoMaxima = itens.Max(item => item.texto.GetPreferredSize(Size.Empty).Width);
        int larguraItem = 64 + larguraTextoMaxima + 16;

        // A coluna da sidebar abraça o conteúdo (item + margem) em vez de uma largura fixa folgada — barra justa,
        // sem espaço vazio à direita e sem cortar o maior nome.
        if (bodyLayout.ColumnStyles.Count > 0 && bodyLayout.ColumnStyles[0].SizeType == SizeType.Absolute)
        {
            bodyLayout.ColumnStyles[0].Width = Math.Min(420, larguraItem + 18);
        }
        int y = menuItemInicio.Top + 16;
        foreach ((Control item, Control icone, Control texto) in itens)
        {
            item.Height = altura;
            item.Width = larguraItem;
            item.Top = y;
            y += altura + espaco;

            icone.SetBounds(10, 0, larguraIcone, altura);
            texto.Left = 64;

            // Medição FRESCA (GetPreferredSize) — o PreferredSize em cache pode vir de 1 linha e desalinhar o
            // item de 2 linhas ("Processo de Produção"), fazendo a 2ª linha vazar por baixo.
            int alturaTexto = texto.GetPreferredSize(Size.Empty).Height;
            texto.Top = Math.Max(4, (altura - alturaTexto) / 2);

            ApplyRoundedRegion(item, 6);
        }
    }

    private void AjustarRodapeSidebar()
    {
        const int alturaUsuario = 84;
        int larguraTexto = Math.Max(80, sidebarPanel.ClientSize.Width - 106);

        sidebarUserPanel.Height = alturaUsuario;
        sidebarUserPanel.Top = sidebarPanel.ClientSize.Height - alturaUsuario;
        sidebarUserAvatarLabel.SetBounds(18, 20, 44, 44);
        // Glyph do avatar em tamanho fixo adequado ao círculo (não a fonte 2.0x, que estourava) e círculo
        // reaplicado JÁ no tamanho 44px (a região vinha calculada em 32px e recortava torto).
        sidebarUserAvatarLabel.Font = new Font("Segoe MDL2 Assets", 20F);
        sidebarUserAvatarLabel.Text = "";   // "Contact" (pessoa) — avatar de usuário
        ApplyRoundedRegion(sidebarUserAvatarLabel, sidebarUserAvatarLabel.Width / 2);
        sidebarUserNameLabel.SetBounds(74, 18, larguraTexto, 26);
        sidebarUserStatusLabel.SetBounds(74, 44, larguraTexto, 22);
        sidebarUserChevronLabel.Visible = false;   // seta ">" removida

        if (_preCarregamentoStatusLabel is not null)
        {
            _preCarregamentoStatusLabel.Left = 18;
            _preCarregamentoStatusLabel.Top = sidebarUserPanel.Top - 24;
            _preCarregamentoStatusLabel.Width = Math.Max(40, sidebarPanel.ClientSize.Width - 36);
        }
    }

    /// <summary>Dispara o pre-carregamento dos pedidos da Entrada sem bloquear a UI (fire-and-forget).</summary>
    private void IniciarPreCarregamentoPedidosEntrada()
    {
        AtualizarStatusPreCarregamentoPedidos("Sincronizando pedidos de entrada...");

        // Task.Run: tira do thread da UI ate a montagem da fabrica/IO; nada de .Wait()/.Result.
        _ = Task.Run(async () =>
        {
            try
            {
                ResultadoPreCarregamentoEntrada resultado =
                    await _preCarregamentoPedidos.ExecutarAsync(_fechamentoPreCarregamentoCts.Token);

                AtualizarStatusPreCarregamentoPedidos(resultado.Cenario switch
                {
                    CenarioPreCarregamentoEntrada.Concluido =>
                        $"Pedidos de entrada atualizados às {resultado.ConcluidoEm:HH:mm}",
                    CenarioPreCarregamentoEntrada.JaEmAndamento => "Sincronizando pedidos de entrada...",
                    CenarioPreCarregamentoEntrada.Cancelado => string.Empty,
                    _ => "Pedidos de entrada: usando cache local"
                });
            }
            catch
            {
                // Background nunca derruba o sistema; apenas limpa o status.
                AtualizarStatusPreCarregamentoPedidos(string.Empty);
            }
        });
    }

    /// <summary>Dispara o pré-carregamento das OPs SAP de Consumo sem bloquear login/UI.</summary>
    private void IniciarPreCarregamentoOrdensProducao()
    {
        AtualizarStatusPreCarregamentoPedidos("Sincronizando OPs SAP...");

        _ = Task.Run(async () =>
        {
            try
            {
                ResultadoPreCarregamentoOrdensProducao resultado =
                    await _preCarregamentoOrdensProducao.PreCarregarAsync(_fechamentoPreCarregamentoCts.Token);

                AtualizarStatusPreCarregamentoPedidos(resultado.Cenario switch
                {
                    CenarioPreCarregamentoOrdensProducao.Concluido =>
                        $"OPs SAP carregadas: {resultado.QuantidadeCarregada}",
                    CenarioPreCarregamentoOrdensProducao.JaEmAndamento => "Sincronizando OPs SAP...",
                    CenarioPreCarregamentoOrdensProducao.Cancelado => string.Empty,
                    _ => "Pré-carga de OPs SAP não concluída. Consulta online disponível."
                });
            }
            catch
            {
                AtualizarStatusPreCarregamentoPedidos(string.Empty);
            }
        });
    }
    private void CriarStatusPreCarregamentoPedidos()
    {
        _preCarregamentoStatusLabel = new Label
        {
            Name = "preCarregamentoStatusLabel",
            AutoSize = false,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Size = new Size(Math.Max(40, sidebarPanel.ClientSize.Width - 36), 18),
            TextAlign = ContentAlignment.MiddleLeft,
            Visible = false
        };
        sidebarPanel.Controls.Add(_preCarregamentoStatusLabel);
        _preCarregamentoStatusLabel.Location = new Point(18, sidebarUserPanel.Top - 24);
        _preCarregamentoStatusLabel.BringToFront();
    }

    private void AtualizarStatusPreCarregamentoPedidos(string texto)
    {
        if (IsDisposed || _preCarregamentoStatusLabel is null)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => AtualizarStatusPreCarregamentoPedidos(texto));
            }
            catch (ObjectDisposedException)
            {
                // Form fechando; ignora.
            }

            return;
        }

        _preCarregamentoStatusLabel.Visible = !string.IsNullOrEmpty(texto);
        _preCarregamentoStatusLabel.Text = texto;
    }

    private async Task AtualizarStatusIndustrialAsync()
    {
        cellBancoText.Text = $"{FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados()} | Verificando";

        StatusIndustrial statusIndustrial = await new StatusIndustrialServico().ObterStatusAsync();
        AtualizarStatusBanco(statusIndustrial.Banco);
        AtualizarStatusTerminalLocal(
            statusIndustrial.TerminalLocal,
            statusIndustrial.BalancaConfigurada,
            statusIndustrial.ImpressoraConfigurada,
            statusIndustrial.CacheSapLocal);
    }

    private void AtualizarStatusBanco(ItemStatusIndustrial statusBanco)
    {
        string nomeBanco = string.IsNullOrWhiteSpace(statusBanco.Identificador)
            ? "N/A"
            : statusBanco.Identificador;

        cellBancoText.Text = $"Banco de Dados:  {nomeBanco} | {statusBanco.Ambiente} | {statusBanco.Situacao}";
    }

    private void AtualizarStatusTerminalLocal(
        ItemStatusIndustrial statusTerminalLocal,
        ItemStatusIndustrial statusBalancaConfigurada,
        ItemStatusIndustrial statusImpressoraConfigurada,
        ItemStatusIndustrial statusCacheSapLocal)
    {
        string nomeTerminal = string.IsNullOrWhiteSpace(statusTerminalLocal.Identificador)
            ? Environment.MachineName
            : statusTerminalLocal.Identificador;

        cellTerminalText.Text =
            $"Terminal:  {nomeTerminal} | Bal.: {statusBalancaConfigurada.Situacao} | Imp.: {statusImpressoraConfigurada.Situacao} | SAP: {statusCacheSapLocal.Situacao}";
    }

    private static string? ResolveIconPath(string relativePath)
    {
        string binPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (File.Exists(binPath))
        {
            return binPath;
        }

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string csprojPath = Path.Combine(current.FullName, "FugaPET_Dev.csproj");
            if (File.Exists(csprojPath))
            {
                string projectPath = Path.Combine(current.FullName, relativePath);
                if (File.Exists(projectPath))
                {
                    return projectPath;
                }
            }

            current = current.Parent;
        }

        return null;
    }

    private void AplicarPermissoesPorPerfil()
    {
        ConfigurarDisponibilidadeMenu(menuItemCadastro, PermissoesSistema.Modulos.Cadastro);
        ConfigurarDisponibilidadeMenu(menuItemLeitura, PermissoesSistema.Modulos.ProcessoProducao);
        ConfigurarDisponibilidadeMenu(menuItemEtiquetas, PermissoesSistema.Modulos.Etiqueta);
        ConfigurarDisponibilidadeMenu(menuItemHistorico, PermissoesSistema.Modulos.Historico);
        ConfigurarDisponibilidadeMenu(menuItemRelatorios, PermissoesSistema.Modulos.Relatorio);
        ConfigurarDisponibilidadeMenu(menuItemSap, PermissoesSistema.Modulos.IntegracaoSap);
        ConfigurarDisponibilidadeMenu(menuItemConfig, PermissoesSistema.Modulos.Configuracao);
        ConfigurarDisponibilidadeMenu(menuItemSeguranca, PermissoesSistema.Modulos.Seguranca);
    }

    private void ConfigurarDisponibilidadeMenu(Panel item, string modulo)
    {
        bool permitido = AutorizacaoServico.PodeAcessar(modulo);
        item.Visible = permitido;

        if (!permitido)
        {
            return;
        }

        if (TryObterRotinaEmDesenvolvimento(item, out string nomeRotina))
        {
            AplicarMenuEmDesenvolvimento(item, nomeRotina);
            return;
        }

        item.Enabled = true;
        item.Cursor = Cursors.Hand;
    }

    private void ApplyRuntimeVisuals()
    {
        // Existing rounded controls
        foreach (Panel menuItem in _sidebarMenuItems.Keys)
        {
            ApplyRoundedRegion(menuItem, 6);
        }

        ApplyRoundedRegion(welcomeIconBg, 12);
        ApplyRoundedRegion(sidebarUserAvatarLabel, 16);

        // Welcome illustration shapes (rounded corners applied at runtime)
        ApplyRoundedRegion(illusMachineBody, 6);
        ApplyRoundedRegion(illusMachineScreen, 3);
        ApplyRoundedRegion(illusMachineLed1, 4);
        ApplyRoundedRegion(illusMachineLed2, 4);
        ApplyRoundedRegion(illusShield, 12);
        ApplyRoundedRegion(illusBox1, 4);
        ApplyRoundedRegion(illusBox2, 4);
        ApplyRoundedRegion(illusBox3, 4);
        ApplyRoundedRegion(illusMidBox1, 3);
        ApplyRoundedRegion(illusMidBox2, 3);
        ApplyRoundedRegion(illusMidBox3, 3);
        ApplyRoundedRegion(illusDot1, 2);
        ApplyRoundedRegion(illusDot2, 2);
        ApplyRoundedRegion(illusDot3, 2);
        ApplyRoundedRegion(illusDot4, 2);
        ApplyRoundedRegion(illusDot5, 2);
        ApplyRoundedRegion(illusDot6, 2);
    }

    private void ConfigureSidebarMenu()
    {
        RegisterSidebarMenuItem(menuItemInicio, menuInicioIcon, menuInicioText);
        RegisterSidebarMenuItem(menuItemCadastro, menuCadastroIcon, menuCadastroText);
        RegisterSidebarMenuItem(menuItemLeitura, menuLeituraIcon, menuLeituraText);
        RegisterSidebarMenuItem(menuItemEtiquetas, menuEtiquetasIcon, menuEtiquetasText);
        RegisterSidebarMenuItem(menuItemHistorico, menuHistoricoIcon, menuHistoricoText);
        RegisterSidebarMenuItem(menuItemRelatorios, menuRelatoriosIcon, menuRelatoriosText);
        RegisterSidebarMenuItem(menuItemSap, menuSapIcon, menuSapText);
        RegisterSidebarMenuItem(menuItemConfig, menuConfigIcon, menuConfigText);
        RegisterSidebarMenuItem(menuItemSeguranca, menuSegurancaIcon, menuSegurancaText);

        SetActiveSidebarMenuItem(menuItemInicio);
    }

    private void RegisterSidebarMenuItem(Panel item, Label icon, Label text)
    {
        _sidebarMenuItems[item] = (icon, text);

        item.Click += (_, _) => NavigateFromSidebar(item);
        icon.Click += (_, _) => NavigateFromSidebar(item);
        text.Click += (_, _) => NavigateFromSidebar(item);
    }

    private void AplicarMenuEmDesenvolvimento(Panel item, string nomeRotina)
    {
        if (!_sidebarMenuItems.TryGetValue(item, out var menuItem))
        {
            return;
        }

        string mensagem = $"{nomeRotina}: rotina em desenvolvimento.";

        item.Enabled = true;
        item.Cursor = Cursors.No;
        item.BackColor = SidebarInactiveColor;
        menuItem.Icon.Cursor = Cursors.No;
        menuItem.Text.Cursor = Cursors.No;
        menuItem.Icon.ForeColor = Color.FromArgb(255, 255, 255);
        menuItem.Text.ForeColor = Color.FromArgb(255, 255, 255);
        menuItem.Text.Font = new Font(menuItem.Text.Font.FontFamily, menuItem.Text.Font.Size, FontStyle.Regular);

        _menuRotinaDesenvolvimentoToolTip.SetToolTip(item, mensagem);
        _menuRotinaDesenvolvimentoToolTip.SetToolTip(menuItem.Icon, mensagem);
        _menuRotinaDesenvolvimentoToolTip.SetToolTip(menuItem.Text, mensagem);
    }

    private bool TryObterRotinaEmDesenvolvimento(Panel item, out string nomeRotina)
    {
        if (item == menuItemRelatorios)
        {
            nomeRotina = "Relatórios";
            return true;
        }

        if (item == menuItemConfig)
        {
            nomeRotina = "Configurações";
            return true;
        }

        nomeRotina = string.Empty;
        return false;
    }

    private async void NavigateFromSidebar(Panel item)
    {
        if (TryObterRotinaEmDesenvolvimento(item, out _))
        {
            return;
        }

        SetActiveSidebarMenuItem(item);

        if (item == menuItemInicio)
        {
            ShowPainelInicialContent();
            return;
        }

        if (item == menuItemCadastro)
        {
            await ShowCadastroContentAsync();
            return;
        }

        if (item == menuItemLeitura)
        {
            await ShowProcessoProducaoContentAsync();
            return;
        }

        if (item == menuItemEtiquetas)
        {
            await OpenConsultaEtiquetaAsync();
            return;
        }

        if (item == menuItemHistorico)
        {
            await OpenConsultaHistoricoAsync();
            return;
        }

        if (item == menuItemSap)
        {
            await OpenConsultaIntegracaoSapAsync();
            return;
        }

        if (item == menuItemSeguranca)
        {
            await ShowSegurancaContentAsync();
        }
    }

    public async void NavigateToProcessoProducao()
    {
        SetActiveSidebarMenuItem(menuItemLeitura);
        await ShowProcessoProducaoContentAsync();
    }

    public async void NavigateToCadastro()
    {
        SetActiveSidebarMenuItem(menuItemCadastro);
        await ShowCadastroContentAsync();
    }

    public async void NavigateToSeguranca()
    {
        SetActiveSidebarMenuItem(menuItemSeguranca);
        await ShowSegurancaContentAsync();
    }

    private void SetActiveSidebarMenuItem(Panel activeItem)
    {
        foreach (var menuItem in _sidebarMenuItems)
        {
            if (TryObterRotinaEmDesenvolvimento(menuItem.Key, out string nomeRotina))
            {
                AplicarMenuEmDesenvolvimento(menuItem.Key, nomeRotina);
                continue;
            }

            bool isActive = menuItem.Key == activeItem;
            menuItem.Key.BackColor = isActive ? SidebarActiveColor : SidebarInactiveColor;
            menuItem.Value.Icon.ForeColor = SidebarTextColor;
            menuItem.Value.Text.ForeColor = SidebarTextColor;
            menuItem.Value.Text.Font = new Font(
                menuItem.Value.Text.Font.FontFamily,
                menuItem.Value.Text.Font.Size,
                isActive ? FontStyle.Bold : FontStyle.Regular);
        }

        ApplyRoundedRegion(activeItem, 6);
    }

    private void ShowPainelInicialContent()
    {
        headerTitleLabel.Text = "Painel Inicial";
        headerSubtitleLabel.Text = "Visão geral da operação / Integração SAP";

        contentScrollPanel.Controls.Clear();
        contentScrollPanel.AutoScroll = true;
        contentLayout.Visible = true;
        contentScrollPanel.Controls.Add(contentLayout);
        _currentContentView = contentLayout;
    }

    private async Task ShowProcessoProducaoContentAsync()
    {
        if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção")) return;
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Leitura de Produção")) return;

        if (!PodeAbrirProcesso())
        {
            return;
        }

        headerTitleLabel.Text = "Leitura de Produção";
        headerSubtitleLabel.Text = "Módulos de leitura / Integração SAP";

        bool primeiraExibicao = _processoProducaoForm is null;
        _processoProducaoForm ??= CreateProcessoProducaoForm();

        // Reentrada: na PRIMEIRA exibição o WinForms auto-escala o form (AutoScaleMode.Font) uma única vez,
        // porque sua fonte é ambiente e herda a 2.0x do contentScrollPanel — resultado 2x aprovado. Ao navegar
        // para fora e voltar, o Controls.Add re-parenteia o form no painel ainda 2x e o PerformAutoScale roda de
        // novo (2x sobre 2x → cards gigantes). Congelar a auto-escala após a primeira renderização preserva o
        // visual aprovado e elimina a composição nas reentradas.
        if (!primeiraExibicao)
        {
            _processoProducaoForm.AutoScaleMode = AutoScaleMode.None;
        }

        contentScrollPanel.Controls.Clear();
        contentScrollPanel.AutoScroll = false;
        contentScrollPanel.Controls.Add(_processoProducaoForm);
        _currentContentView = _processoProducaoForm;
    }

    private async Task ShowCadastroContentAsync()
    {
        if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.Cadastro, "Cadastro")) return;

        headerTitleLabel.Text = "Cadastro";
        headerSubtitleLabel.Text = "Módulos de cadastro / Integração SAP";

        _cadastroForm?.Dispose();
        _cadastroForm = new CadastroForm
        {
            Dock = DockStyle.Fill
        };

        // Usuarios/Perfis/Permissoes saem do Cadastro e vivem no menu Seguranca (ShowSegurancaContent).
        _cadastroForm.SetorRequested -= CadastroForm_SetorRequested;
        _cadastroForm.SetorRequested += CadastroForm_SetorRequested;
        _cadastroForm.CargoRequested -= CadastroForm_CargoRequested;
        _cadastroForm.CargoRequested += CadastroForm_CargoRequested;
        _cadastroForm.TaraRequested -= CadastroForm_TaraRequested;
        _cadastroForm.TaraRequested += CadastroForm_TaraRequested;
        _cadastroForm.TipoTaraRequested -= CadastroForm_TipoTaraRequested;
        _cadastroForm.TipoTaraRequested += CadastroForm_TipoTaraRequested;
        _cadastroForm.ModeloEtiquetaRequested -= CadastroForm_ModeloEtiquetaRequested;
        _cadastroForm.ModeloEtiquetaRequested += CadastroForm_ModeloEtiquetaRequested;
        _cadastroForm.BalancaRequested -= CadastroForm_BalancaRequested;
        _cadastroForm.BalancaRequested += CadastroForm_BalancaRequested;
        _cadastroForm.EtiquetaRequested -= CadastroForm_EtiquetaRequested;
        _cadastroForm.EtiquetaRequested += CadastroForm_EtiquetaRequested;

        contentScrollPanel.SuspendLayout();
        contentScrollPanel.Controls.Clear();
        contentScrollPanel.AutoScroll = false;
        contentScrollPanel.AutoScrollPosition = Point.Empty;
        contentScrollPanel.Controls.Add(_cadastroForm);
        _cadastroForm.BringToFront();
        contentScrollPanel.ResumeLayout(true);
        contentScrollPanel.PerformLayout();
        _currentContentView = _cadastroForm;
    }

    private async Task ShowSegurancaContentAsync()
    {
        // Seguranca NAO depende de permissao de Cadastro: valida o proprio modulo SEGURANCA.
        if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.Seguranca, "Segurança")) return;

        headerTitleLabel.Text = "Segurança";
        headerSubtitleLabel.Text = "Usuários, perfis e permissões / Administração do sistema";

        _segurancaForm?.Dispose();
        _segurancaForm = new SegurancaForm
        {
            Dock = DockStyle.Fill
        };

        _segurancaForm.CadastroUsuarioRequested += CadastroForm_CadastroUsuarioRequested;
        _segurancaForm.PerfilAcessoRequested += CadastroForm_PerfilAcessoRequested;
        _segurancaForm.PermissaoRequested += CadastroForm_PermissaoRequested;

        contentScrollPanel.SuspendLayout();
        contentScrollPanel.Controls.Clear();
        contentScrollPanel.AutoScroll = false;
        contentScrollPanel.AutoScrollPosition = Point.Empty;
        contentScrollPanel.Controls.Add(_segurancaForm);
        _segurancaForm.BringToFront();
        contentScrollPanel.ResumeLayout(true);
        contentScrollPanel.PerformLayout();
        _currentContentView = _segurancaForm;
    }

    /// <summary>
    /// Valida permissao macro de modulo para menus principais. Em caso de negacao,
    /// registra auditoria e mostra mensagem amigavel ao usuario.
    /// </summary>
    private async Task<bool> PodeAcessarModuloAsync(string modulo, string nomeMenu)
    {
        if (AutorizacaoServico.PodeAcessar(modulo))
        {
            return true;
        }

        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (codigoUsuario.HasValue)
        {
            await RegistrarAcessoModuloNegadoSeguroAsync(codigoUsuario.Value, modulo, nomeMenu);
        }

        MessageBox.Show(
            $"Você não possui permissão para acessar {nomeMenu}.",
            "Acesso negado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    /// <summary>
    /// Valida permissao fina de CONSULTA antes de abrir qualquer tela. Mesmo a simples
    /// visualizacao e sensivel em ambiente industrial. Em caso de negacao, audita e avisa.
    /// </summary>
    private async Task<bool> PermiteAbrirTelaAsync(string modulo, string rotina, string telaAlvo)
    {
        if (AutorizacaoServico.PodeVisualizarRotina(modulo, rotina))
        {
            return true;
        }

        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (codigoUsuario.HasValue)
        {
            // Auditoria de seguranca: aguardamos (await) o registro do acesso negado.
            await RegistrarAcessoNegadoSeguroAsync(codigoUsuario.Value, modulo, rotina, telaAlvo);
        }

        MessageBox.Show(
            "Você não possui permissão para acessar esta rotina.",
            "Acesso negado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private async Task RegistrarAcessoModuloNegadoSeguroAsync(long codigoUsuario, string modulo, string nomeMenu)
    {
        try
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(
                codigoUsuario,
                $"Acesso negado ao menu {nomeMenu} ({modulo}).",
                nomeMenu);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(
                $"Falha ao registrar acesso negado ao menu ({nomeMenu}): {ex}");
        }
    }

    /// <summary>
    /// Registra o acesso negado aguardando a conclusao (await) e tratando falhas — em vez de
    /// fire-and-forget (_ = ...), que perderia silenciosamente um erro de auditoria de seguranca.
    /// </summary>
    private async Task RegistrarAcessoNegadoSeguroAsync(long codigoUsuario, string modulo, string rotina, string telaAlvo)
    {
        try
        {
            await _auditoriaServico.RegistrarAcessoNegadoAsync(
                codigoUsuario,
                $"Acesso negado a {telaAlvo} ({modulo}/{rotina}/CONSULTAR ou VISUALIZAR).",
                telaAlvo);
        }
        catch (Exception ex)
        {
            // A auditoria nao pode quebrar a navegacao; registra no log de diagnostico.
            // TODO: registrar tambem em log local persistente quando disponivel.
            System.Diagnostics.Trace.TraceError(
                $"Falha ao registrar acesso negado ({telaAlvo}): {ex}");
        }
    }

    private async void CadastroForm_CadastroUsuarioRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, "Cadastro de Usuário")) return;
        using Cadastro.CadastroUsuarioForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToSeguranca();
        }
    }

    private async void CadastroForm_SetorRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Setor, "Cadastro de Setor")) return;
        using Cadastro.SetorForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private async void CadastroForm_CargoRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Cargo, "Cadastro de Cargo")) return;
        using Cadastro.CargoForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private async void CadastroForm_TipoTaraRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.TipoTara, "Cadastro de Tipo de Tara")) return;
        using Cadastro.TipoTaraForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private async void CadastroForm_ModeloEtiquetaRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.ModeloEtiqueta, "Modelo de Etiqueta")) return;
        using Cadastro.ModeloEtiquetaForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private async void CadastroForm_PerfilAcessoRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, "Perfis de Acesso")) return;
        using Cadastro.PerfilAcessoForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToSeguranca();
        }
    }

    private async void CadastroForm_PermissaoRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, "Permissões")) return;
        using Cadastro.PermissaoForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToSeguranca();
        }
    }

    private async void CadastroForm_TaraRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Tara, "Cadastro de Tara")) return;
        using Cadastro.TaraForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private async void CadastroForm_BalancaRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Cadastro, PermissoesSistema.Rotinas.Balanca, "Cadastro de Balança")) return;
        using Cadastro.BalancaForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private async void CadastroForm_EtiquetaRequested(object? sender, EventArgs e)
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.Etiqueta, "Cadastro de Etiqueta")) return;
        using Cadastro.EtiquetaForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            NavigateToCadastro();
        }
    }

    private ProcessoProducaoForm CreateProcessoProducaoForm()
    {
        ProcessoProducaoForm view = new()
        {
            Dock = DockStyle.Fill
        };

        // GATE 073: porta única. O primeiro card (F1) abre o Recebimento de Mercadoria (ROH+HIBE). A antiga
        // rota pública de "Entrada de Químicos" foi removida — o evento do card oculto não é mais assinado.
        view.EntradaMateriaPrimaRequested += async (_, _) => await OpenProcessoEntradaProdutoAsync(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.RecebimentoMercadoria);
        view.ProcessoProdutoAcabadoRequested += async (_, _) => await OpenProcessoProdutoAcabadoAsync();
        view.ProcessoSemiAcabadoRequested += async (_, _) => await OpenProcessoSemiAcabadoAsync();
        view.ProcessoConsumoMaterialRequested += async (_, _) => await OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.MateriaPrima);
        view.ProcessoConsumoQuimicosRequested += async (_, _) => await OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico);
        view.ControleApontamentosRequested += async (_, _) => await OpenControleApontamentosAsync();
        view.HistoricoConsumoMaterialRequested += async (_, _) => await OpenProcessoConsumoMaterialHistoricoAsync();
        view.DiagnosticoConsumoSap261Requested += async (_, _) => await OpenDiagnosticoConsumoSap261Async();
        view.OrdensAndamentoRequested += async (_, _) => await OpenConsultaOrdemProducaoAsync();
        view.PaletizacaoRequested += async (_, _) => await OpenPaletizacaoAsync();

        return view;
    }

    // Tarefa Entrada 24.1 (Ajuste 5): abre a Entrada no modo escolhido (Matéria-Prima × Químicos). Enquanto não
    // houver permissão específica de Entrada de Químicos, ambos os módulos usam a permissão de Entrada atual.
    private async Task OpenProcessoEntradaProdutoAsync(
        global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo =
            global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.RecebimentoMercadoria)
    {
        string nomeTela =
            global::FugaPET_Dev.Modelo.Processo.ConfiguracaoTelaEntradaMaterialFactory.Criar(modo).NomeModulo;

        if (!AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Consultar)
            && !await PermiteAbrirTelaAsync(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.EntradaProduto,
                nomeTela))
        {
            return;
        }

        if (!PodeAbrirProcesso())
        {
            return;
        }

        using Processo.ProcessoEntradaProdutoForm form = new(modo);

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            Activate();
            NavigateToProcessoProducao();
        }
    }

    private async Task OpenProcessoProdutoAcabadoAsync()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Processo Produto Acabado")) return;

        if (!PodeAbrirProcesso())
        {
            return;
        }

        Processo.ProcessoProdutoAcabadoForm form = new();
        form.FormClosed += (_, _) => Show();

        Hide();
        form.Show(this);
    }

    private async Task OpenProcessoSemiAcabadoAsync()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Produto Semi-Acabado")) return;

        if (!PodeAbrirProcesso())
        {
            return;
        }

        using Processo.ProcessoSemiAcabadoForm form = new();
        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            Activate();
            NavigateToProcessoProducao();
        }
    }

    /// <summary>
    /// Controle de Apontamentos (F8). A abertura é governada por
    /// PROCESSO_PRODUCAO / CONTROLE_APONTAMENTOS / VISUALIZAR, através do serviço de autorização
    /// específico do módulo — que decide o fallback pela EXISTÊNCIA da estrutura do 039, não por um OR
    /// permanente com LEITURA_PRODUCAO. Hide/ShowDialog/Show/Activate preservados.
    /// </summary>
    private async Task OpenControleApontamentosAsync()
    {
        if (!await PermiteAbrirControleApontamentosAsync())
        {
            return;
        }

        if (!PodeAbrirProcesso())
        {
            return;
        }

        using Processo.ProcessoControleApontamentosForm form = new();
        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            Activate();
            NavigateToProcessoProducao();
        }
    }

    /// <summary>
    /// Gate de VISUALIZAÇÃO do Controle de Apontamentos. A negativa é auditada com a rotina PRÓPRIA
    /// (CONTROLE_APONTAMENTOS), não com LEITURA_PRODUCAO.
    /// </summary>
    private async Task<bool> PermiteAbrirControleApontamentosAsync()
    {
        Controle.Processo.ProcessoControleApontamentosController controller = new();
        if (await controller.PodeVisualizarAsync())
        {
            return true;
        }

        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (codigoUsuario.HasValue)
        {
            await RegistrarAcessoNegadoSeguroAsync(
                codigoUsuario.Value,
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.ControleApontamentos,
                "Controle de Apontamentos");
        }

        MessageBox.Show(
            "Você não possui permissão para acessar esta rotina.",
            "Acesso negado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private async Task OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial modo = global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.MateriaPrima)
    {
        string nomeTela = modo == global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico ? "Consumo de Químicos" : "Consumo de Matéria-Prima";
        // TODO Permissões:
        // Quando a matriz de permissões for atualizada, separar acesso/execução de Consumo de Químicos.
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, nomeTela)) return;

        if (!PodeAbrirProcesso())
        {
            return;
        }

        Processo.ProcessoConsumoMaterialForm form = new(modo);
        // Ao voltar: reexibe, reativa e renavega o painel; Invalidate(true)+Update() forcam o repaint de
        // TODA a janela (cabecalho + menu lateral + conteudo), evitando o painel "sumindo" ate mover o mouse.
        form.FormClosed += (_, _) =>
        {
            Show();
            Activate();
            NavigateToProcessoProducao();
            Invalidate(true);
            Update();
        };

        Hide();
        form.Show(this);
    }

    private async Task OpenProcessoConsumoMaterialHistoricoAsync()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Histórico de Consumo de Matéria-Prima")) return;

        using Processo.ProcessoConsumoMaterialHistoricoForm form = new();
        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            Activate();
            NavigateToProcessoProducao();
        }
    }

    private async Task OpenDiagnosticoConsumoSap261Async()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Diagnóstico Consumo SAP 261")) return;

        using Processo.DiagnosticoConsumoSap261Form form = new();
        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
            Activate();
            NavigateToProcessoProducao();
        }
    }

    private async Task OpenConsultaOrdemProducaoAsync()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, PermissoesSistema.Rotinas.OrdemAndamento, "Consulta de Ordem de Produção")) return;
        using Consulta.ConsultaOrdemProducaoForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
        }
    }

    private async Task OpenConsultaEtiquetaAsync()
    {
        if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.Etiqueta, "Etiquetas")) return;
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Etiqueta, PermissoesSistema.Rotinas.Etiqueta, "Consulta de Etiqueta")) return;
        using Consulta.ConsultaEtiquetaForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
        }
    }

    private async Task OpenConsultaHistoricoAsync()
    {
        if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.Historico, "Histórico")) return;
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.Historico, PermissoesSistema.Rotinas.Movimentacao, "Consulta de Histórico")) return;
        using Consulta.ConsultaHistoricoForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
        }
    }

    private async Task OpenConsultaIntegracaoSapAsync()
    {
        if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.IntegracaoSap, "SAP")) return;
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.IntegracaoSap, PermissoesSistema.Rotinas.MonitoramentoSap, "Consulta de Integração SAP")) return;
        using Consulta.ConsultaIntegracaoSapForm form = new();

        Hide();

        try
        {
            form.ShowDialog(this);
        }
        finally
        {
            Show();
        }
    }

    private bool PodeAbrirProcesso()
    {
        if (!EstadoIntegracaoBanco.Habilitado)
        {
            return true;
        }

        if (VerificadorConexaoBanco.ConexaoDisponivel(out string mensagem))
        {
            return true;
        }

        MessageBox.Show(
            string.IsNullOrWhiteSpace(mensagem)
                ? "Conexao com banco indisponivel para abrir as telas de processo."
                : mensagem,
            "Integracao indisponivel",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private async void PainelInicialForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_currentContentView == _cadastroForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.Cadastro, "Cadastro"))
            {
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F1)
            {
                CadastroForm_SetorRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F2)
            {
                CadastroForm_CargoRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F3)
            {
                CadastroForm_BalancaRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F4)
            {
                CadastroForm_TipoTaraRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F5)
            {
                CadastroForm_TaraRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            // Reordenação Modelo → Etiqueta: F6 abre Modelo de Etiqueta, F7 abre Etiqueta (alinhado aos cartões).
            if (e.KeyCode == Keys.F6)
            {
                CadastroForm_ModeloEtiquetaRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F7)
            {
                CadastroForm_EtiquetaRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }
        }

        if (_currentContentView == _segurancaForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.Seguranca, "Segurança"))
            {
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F3)
            {
                CadastroForm_PerfilAcessoRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F4)
            {
                CadastroForm_PermissaoRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F5)
            {
                CadastroForm_CadastroUsuarioRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }
        }

        if (e.KeyCode == Keys.F1 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoEntradaProdutoAsync();
            e.Handled = true;
        }

        // GATE 077: a porta removida permanece ausente e os módulos visíveis ocupam F1..F8 sem lacunas.
        if (e.KeyCode == Keys.F2 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoConsumoMaterialAsync();
            e.Handled = true;
        }

        if (e.KeyCode == Keys.F3 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico);
            e.Handled = true;
        }

        if (e.KeyCode == Keys.F4 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoSemiAcabadoAsync();
            e.Handled = true;
        }

        if (e.KeyCode == Keys.F5 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoProdutoAcabadoAsync();
            e.Handled = true;
        }

        if (e.KeyCode == Keys.F6 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenConsultaOrdemProducaoAsync();
            e.Handled = true;
        }

        if (e.KeyCode == Keys.F7 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenControleApontamentosAsync();
            e.Handled = true;
        }
        if (e.KeyCode == Keys.F8 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenPaletizacaoAsync();
            e.Handled = true;
        }
    }


    // INCREMENTAL 047: abre a nova tela de Paletização (por HU de caixas). Mesmo módulo/permissão do processo de produção.
    private async Task OpenPaletizacaoAsync()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Paletização")) return;

        if (!PodeAbrirProcesso())
        {
            return;
        }

        Processo.PaletizacaoForm form = new();
        form.FormClosed += (_, _) => Show();

        Hide();
        form.Show(this);
    }
    private static void ApplyRoundedRegion(Control control, int radius)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return;
        }

        using System.Drawing.Drawing2D.GraphicsPath path = new();
        int diameter = radius * 2;
        Rectangle bounds = new(0, 0, control.Width, control.Height);
        Rectangle arc = new(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        control.Region = new Region(path);
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

    private void ConfigureCustomTitleBar()
    {
        headerBar.MouseDown += CustomTitleBar_MouseDown;
        //menuToggleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;
        sidebarLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        // Hover mais escuro (igual ao Recebimento de Mercadoria) — visível sobre o laranja do cabeçalho.
        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(160, 55, 5));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(160, 55, 5));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(160, 55, 5));

        // Os botões preenchem TODA a altura do cabeçalho (antes 52px em 60px, sobrando uma faixa laranja clara
        // embaixo). Assim o hover escuro vai de cima até encontrar o branco do conteúdo.
        foreach (Control botao in new[] { minimizeWindowLabel, maximizeWindowLabel, closeWindowLabel })
        {
            botao.Top = 0;
            botao.Height = headerBar.Height;
        }
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
        Color normalForeColor = button.ForeColor;
        button.MouseEnter += (_, _) =>
        {
            button.BackColor = hoverColor;
            if (hoverColor == Color.FromArgb(220, 53, 69))
            {
                button.ForeColor = Color.White;
            }
        };
        button.MouseLeave += (_, _) =>
        {
            button.BackColor = normalColor;
            button.ForeColor = normalForeColor;
        };
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
}









