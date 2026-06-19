using System.Runtime.InteropServices;

namespace FugaPET_Dev.Tela.Consulta;

public partial class ConsultaOrdemProducaoForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";

    private static readonly Color RowGreen = Color.FromArgb(238, 241, 245);
    private static readonly Color RowLight = Color.FromArgb(250, 251, 252);

    private System.Windows.Forms.Timer? _footerClockTimer;
    private readonly List<OrdemGridItem> _todasOrdens = new();
    private bool _placeholderPesquisaAtivo = true;
    private const string PlaceholderPesquisa = "Pesquisar OP, lote ou produto...";

    // Enquanto a tela nao le dados reais. Ao integrar, troque para false: aviso, faixa e mock somem.
    private const bool UsaDadosSimulados = true;

    public ConsultaOrdemProducaoForm()
    {
        InitializeComponent();
        cellUserText.Text = global::FugaPET_Dev.Tela.Comum.UsuarioLogadoUiHelper.ObterTextoUsuarioRodape();
        cellBancoText.Text = global::FugaPET_Dev.Tela.Comum.RodapeBancoHelper.ObterTextoBancoDados();
        cellTerminalText.Text = $"Terminal:  {Environment.MachineName}";
        if (UsaDadosSimulados)
        {
            if (!global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.PodeUsarDadosSimulados())
            {
                global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.BloquearTelaSimulada(this);
                return;
            }
            global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.Aplicar(headerSubtitleLabel);
            global::FugaPET_Dev.Tela.Comum.AvisoDadosSimuladosHelper.AplicarFaixa(this);
        }
        LoadWindowIcon();
        ConfigureCustomTitleBar();
        ConfigureSearchBox();
        if (UsaDadosSimulados)
        {
            LoadMockData();
        }
        ConfigureFooterDate();
        KeyPreview = true;
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
        customTitleBarPanel.MouseDown += CustomTitleBar_MouseDown;
        companyLogoPictureBox.MouseDown += CustomTitleBar_MouseDown;
        headerTitleLabel.MouseDown += CustomTitleBar_MouseDown;
        headerSubtitleLabel.MouseDown += CustomTitleBar_MouseDown;
        menuHeaderLabel.Click += (_, _) => VoltarParaPainelInicial();

        minimizeWindowLabel.Click += (_, _) => WindowState = FormWindowState.Minimized;
        maximizeWindowLabel.Click += (_, _) => ToggleWindowState();
        closeWindowLabel.Click += (_, _) => Close();

        ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));
        ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));
    }

    private void VoltarParaPainelInicial()
    {
        if (Owner is PainelInicialForm painelInicialForm)
        {
            Close();
            return;
        }

        PainelInicialForm painel = new();
        painel.Show();
        Close();
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

    private void LoadMockData()
    {
        _todasOrdens.Clear();

        AddOrderItem("58422", "04/05/2026 08:12", "119 26", "27771", "TWIST STIX CARNE 24X50PCS", "12 - Depois de LEPESO", "5,568", "3,268", "61%");
        AddOrderItem("58421", "04/05/2026 07:45", "119 25", "27771", "TWIST STIX CARNE 24X50PCS", "12 - Depois de LEPESO", "4,896", "1,584", "32%");
        AddOrderItem("58420", "04/05/2026 06:30", "119 24", "27771", "TWIST STIX CARNE 24X50PCS", "10 - Leitura de Caixas", "5,760", "884", "15%");
        AddOrderItem("58419", "04/05/2026 23:10", "119 23", "27771", "TWIST STIX CARNE 24X50PCS", "08 - Producao", "5,760", "2,755", "48%");
        AddOrderItem("58418", "03/05/2026 20:22", "119 22", "27771", "TWIST STIX CARNE 24X50PCS", "04 - Separacao", "5,760", "4,414", "77%");
        AddOrderItem("58417", "03/05/2026 18:15", "119 21", "27771", "TWIST STIX CARNE 24X50PCS", "12 - Depois de LEPESO", "4,320", "2,592", "60%");
        AddOrderItem("58416", "03/05/2026 16:05", "119 20", "27771", "TWIST STIX CARNE 24X50PCS", "10 - Leitura de Caixas", "5,760", "4,387", "76%");
        AddOrderItem("58415", "03/05/2026 14:45", "119 19", "27771", "TWIST STIX CARNE 24X50PCS", "08 - Producao", "5,760", "5,281", "92%");
        AddOrderItem("58414", "03/05/2026 13:20", "119 18", "27771", "TWIST STIX CARNE 24X50PCS", "04 - Separacao", "5,760", "4,253", "74%");
        AddOrderItem("58413", "03/05/2026 11:30", "119 17", "27771", "TWIST STIX CARNE 24X50PCS", "10 - Leitura de Caixas", "5,760", "4,896", "85%");

        RenderOrders(_todasOrdens);
    }

    private void AddOrderItem(string op, string data, string lote, string produto, string descricao, string passo, string caixasPrev, string caixasLidas, string percent)
    {
        _todasOrdens.Add(new OrdemGridItem(op, data, lote, produto, descricao, passo, caixasPrev, caixasLidas, percent));
    }

    private void ConfigureSearchBox()
    {
        searchTextBox.Text = PlaceholderPesquisa;
        searchTextBox.ForeColor = Color.FromArgb(120, 130, 145);

        searchTextBox.Enter += SearchTextBox_Enter;
        searchTextBox.Leave += SearchTextBox_Leave;
        searchTextBox.TextChanged += SearchTextBox_TextChanged;
    }

    private void SearchTextBox_Enter(object? sender, EventArgs e)
    {
        if (!_placeholderPesquisaAtivo)
        {
            return;
        }

        _placeholderPesquisaAtivo = false;
        searchTextBox.Text = string.Empty;
        searchTextBox.ForeColor = Color.FromArgb(24, 31, 43);
    }

    private void SearchTextBox_Leave(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(searchTextBox.Text))
        {
            return;
        }

        _placeholderPesquisaAtivo = true;
        searchTextBox.Text = PlaceholderPesquisa;
        searchTextBox.ForeColor = Color.FromArgb(120, 130, 145);
    }

    private void SearchTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_placeholderPesquisaAtivo)
        {
            return;
        }

        AplicarFiltroPesquisa(searchTextBox.Text);
    }

    private void AplicarFiltroPesquisa(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            RenderOrders(_todasOrdens);
            return;
        }

        string filtro = termo.Trim();
        string filtroNormalizado = NormalizarParaBusca(filtro);
        List<OrdemGridItem> resultado = _todasOrdens
            .Where(o =>
                ContemIgnoreCase(o.Op, filtro) ||
                ContemIgnoreCase(o.Lote, filtro) ||
                ContemIgnoreCase(o.Produto, filtro) ||
                ContemNormalizado(o.Op, filtroNormalizado) ||
                ContemNormalizado(o.Lote, filtroNormalizado) ||
                ContemNormalizado(o.Produto, filtroNormalizado))
            .ToList();

        RenderOrders(resultado);
    }

    private void RenderOrders(IEnumerable<OrdemGridItem> ordens)
    {
        ordersDataGridView.Rows.Clear();
        foreach (OrdemGridItem ordem in ordens)
        {
            ordersDataGridView.Rows.Add(
                ordem.Op,
                ordem.Data,
                ordem.Lote,
                ordem.Produto,
                ordem.Descricao,
                ordem.Passo,
                ordem.CaixasPrev,
                ordem.CaixasLidas,
                ordem.Percent,
                "Em andamento",
                "\u22EE");
        }

        ApplyGridStyle(ordersDataGridView);
    }

    private static bool ContemIgnoreCase(string valor, string filtro)
    {
        return valor.Contains(filtro, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContemNormalizado(string valor, string filtroNormalizado)
    {
        if (string.IsNullOrWhiteSpace(filtroNormalizado))
        {
            return true;
        }

        string valorNormalizado = NormalizarParaBusca(valor);
        return valorNormalizado.Contains(filtroNormalizado, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizarParaBusca(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        char[] caracteres = valor
            .Where(char.IsLetterOrDigit)
            .ToArray();

        return new string(caracteres);
    }

    private static void ApplyGridStyle(DataGridView grid)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            grid.Rows[i].DefaultCellStyle.BackColor = i % 2 == 0 ? Color.White : RowLight;
        }
        grid.ClearSelection();
    }

    private sealed record OrdemGridItem(
        string Op,
        string Data,
        string Lote,
        string Produto,
        string Descricao,
        string Passo,
        string CaixasPrev,
        string CaixasLidas,
        string Percent);
}






