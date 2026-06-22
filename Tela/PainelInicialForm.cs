using System.Runtime.InteropServices;
using Cadastro = FugaPET_Dev.Tela.Cadastro;
using Consulta = FugaPET_Dev.Tela.Consulta;
using Processo = FugaPET_Dev.Tela.Processo;

using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Status;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Servicos.Status;

namespace FugaPET_Dev.Tela;

public partial class PainelInicialForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
    private const string RotinaLeituraProducao = PermissoesSistema.Rotinas.LeituraProducao;
    private static readonly Color SidebarActiveColor = Color.FromArgb(229, 27, 43);
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
        Shown += (_, _) => ApplyRuntimeVisuals();
        Shown += (_, _) => AplicarPermissoesPorPerfil();
        Shown += async (_, _) => await AtualizarStatusIndustrialAsync();
        KeyDown += PainelInicialForm_KeyDown;
    }

    private async Task AtualizarStatusIndustrialAsync()
    {
        sapStatusLabel.Text = "Verificando banco";
        sapStatusDotLabel.ForeColor = Color.FromArgb(250, 204, 21);
        sapStatusPanel.BorderColor = Color.FromArgb(250, 204, 21);
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

        Color corStatus = ObterCorStatusBanco(statusBanco);

        cellBancoText.Text = $"Banco de Dados:  {nomeBanco} | {statusBanco.Ambiente} | {statusBanco.Situacao}";
        sapStatusLabel.Text = $"Banco {statusBanco.Ambiente} {statusBanco.Situacao}";
        sapStatusDotLabel.ForeColor = corStatus;
        sapStatusPanel.BorderColor = corStatus;
        sapStatusPanel.Invalidate();
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

    private static Color ObterCorStatusBanco(ItemStatusIndustrial status)
    {
        if (!status.Habilitado)
        {
            return Color.FromArgb(250, 204, 21);
        }

        return status.Online
            ? Color.FromArgb(34, 197, 94)
            : Color.FromArgb(239, 68, 68);
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
        menuItem.Icon.ForeColor = Color.FromArgb(107, 114, 128);
        menuItem.Text.ForeColor = Color.FromArgb(107, 114, 128);
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

        _processoProducaoForm ??= CreateProcessoProducaoForm();

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

        view.EntradaProdutoRequested += async (_, _) => await OpenProcessoEntradaProdutoAsync();
        view.ProcessoProdutoAcabadoRequested += async (_, _) => await OpenProcessoProdutoAcabadoAsync();
        view.ProcessoPesagemApontamentoRequested += async (_, _) => await OpenProcessoPesagemApontamentoAsync();
        view.OrdensAndamentoRequested += async (_, _) => await OpenConsultaOrdemProducaoAsync();

        return view;
    }

    private async Task OpenProcessoEntradaProdutoAsync()
    {
        if (!AutorizacaoEntradaProdutoServico.PossuiPermissao(PermissoesSistema.Acoes.Consultar)
            && !await PermiteAbrirTelaAsync(
                PermissoesSistema.Modulos.ProcessoProducao,
                PermissoesSistema.Rotinas.EntradaProduto,
                "Entrada de Produto"))
        {
            return;
        }

        if (!PodeAbrirProcesso())
        {
            return;
        }

        using Processo.ProcessoEntradaProdutoForm form = new();

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

    private async Task OpenProcessoPesagemApontamentoAsync()
    {
        if (!await PermiteAbrirTelaAsync(PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, "Processo Pesagem Apontamento")) return;

        if (!PodeAbrirProcesso())
        {
            return;
        }

        Processo.ProcessoPesagemApontamentoForm form = new();
        form.FormClosed += (_, _) => Show();

        Hide();
        form.Show(this);
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

            if (e.KeyCode == Keys.F6)
            {
                CadastroForm_EtiquetaRequested(this, EventArgs.Empty);
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.F7)
            {
                CadastroForm_ModeloEtiquetaRequested(this, EventArgs.Empty);
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

        if (e.KeyCode == Keys.F2 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoProdutoAcabadoAsync();
            e.Handled = true;
        }

        if (e.KeyCode == Keys.F4 && _currentContentView == _processoProducaoForm)
        {
            if (!await PodeAcessarModuloAsync(PermissoesSistema.Modulos.ProcessoProducao, "Leitura de Produção"))
            {
                e.Handled = true;
                return;
            }

            await OpenProcessoPesagemApontamentoAsync();
            e.Handled = true;
        }
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

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));
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




