using System.Runtime.InteropServices;

namespace FugaPET_Dev.Tela.Consulta;

public partial class ConsultaIntegracaoSapForm : Form
{
    private const int WmNclButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const string WindowIconPath = "Servicos\\icone\\fuga.ico";
    private const string PlaceholderPesquisa = "Digite OP, lote ou produto...";

    private static readonly Color RowLight = Color.FromArgb(250, 251, 252);

    private System.Windows.Forms.Timer? _footerClockTimer;
    private readonly List<IntegracaoSapGridItem> _todosRegistros = [];
    private bool _placeholderPesquisaAtivo = true;

    // Enquanto a tela nao le dados reais. Ao integrar, troque para false: aviso, faixa e mock somem.
    private const bool UsaDadosSimulados = true;

    public ConsultaIntegracaoSapForm()
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
        Shown += (_, _) => AjustarLayoutResponsivo();
        Resize += (_, _) => AjustarLayoutResponsivo();
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
        if (Owner is PainelInicialForm)
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

    private void LoadMockData()
    {
        _todosRegistros.Clear();

        AddIntegracaoSapItem("04/05/2026 13:36:22", "Envio", "58895", "PE - TURMA DA MONICA CARNE E LEITE", "131 262F", "7891234567890", "16,000", "KG", "OPERADOR01", "Balanca F12", "Sucesso");
        AddIntegracaoSapItem("04/05/2026 13:32:45", "Envio", "58894", "MP PULMAO CONG BOVINO NT CONGELADO", "---", "---", "16,000", "KG", "OPERADOR01", "Manual", "Sucesso");
        AddIntegracaoSapItem("04/05/2026 13:28:11", "Retorno", "58893", "MP C.M.S DE FRANGO NT CONGELADO", "---", "---", "4,000", "KG", "OPERADOR02", "Balanca F8", "Confirmado");
        AddIntegracaoSapItem("04/05/2026 13:21:05", "Envio", "58892", "PEITO DE FRANGO S/OSSO CONGELADO", "---", "---", "8,000", "KG", "SISTEMA", "Automatico", "Enviado");
        AddIntegracaoSapItem("04/05/2026 13:15:30", "Retorno", "58891", "MP FIGADO DE FRANGO CONGELADO", "---", "7899876543210", "4,000", "KG", "OPERADOR01", "Balanca F12", "Confirmado");
        AddIntegracaoSapItem("04/05/2026 13:09:47", "Envio", "58890", "COXA E SOBRECOXA CONGELADA", "---", "---", "12,000", "KG", "OPERADOR02", "Manual", "Sucesso");
        AddIntegracaoSapItem("04/05/2026 12:55:12", "Retorno", "58889", "ASA DE FRANGO CONGELADA", "---", "7891112223334", "10,000", "KG", "OPERADOR01", "Balanca F12", "Confirmado");
        AddIntegracaoSapItem("04/05/2026 12:42:33", "Erro", "58888", "LINGUICA DE FRANGO CONGELADA", "---", "---", "6,000", "KG", "SISTEMA", "Automatico", "Erro");

        RenderHistorico(_todosRegistros);
    }

    private void AddIntegracaoSapItem(string dataHora, string tipo, string op, string produto, string lote, string codigoBarras, string qtd, string unid, string usuario, string origem, string situacao)
    {
        _todosRegistros.Add(new IntegracaoSapGridItem(dataHora, tipo, op, produto, lote, codigoBarras, qtd, unid, usuario, origem, situacao));
    }

    private void AplicarFiltroPesquisa(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            RenderHistorico(_todosRegistros);
            return;
        }

        string filtro = termo.Trim();
        string filtroNormalizado = NormalizarParaBusca(filtro);

        List<IntegracaoSapGridItem> resultado = _todosRegistros
            .Where(r =>
                ContemIgnoreCase(r.Op, filtro) ||
                ContemIgnoreCase(r.Lote, filtro) ||
                ContemIgnoreCase(r.Produto, filtro) ||
                ContemNormalizado(r.Op, filtroNormalizado) ||
                ContemNormalizado(r.Lote, filtroNormalizado) ||
                ContemNormalizado(r.Produto, filtroNormalizado))
            .ToList();

        RenderHistorico(resultado);
    }

    private void RenderHistorico(IEnumerable<IntegracaoSapGridItem> registros)
    {
        ordersDataGridView.Rows.Clear();
        foreach (IntegracaoSapGridItem item in registros)
        {
            ordersDataGridView.Rows.Add(
                item.DataHora,
                item.Tipo,
                item.Op,
                item.Produto,
                item.Lote,
                item.CodigoBarras,
                item.Quantidade,
                item.Unidade,
                item.Usuario,
                item.Origem,
                item.Situacao,
                "Ver");
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

        char[] caracteres = valor.Where(char.IsLetterOrDigit).ToArray();
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

    private void AjustarLayoutResponsivo()
    {
        if (filtersCard.Width <= 0)
        {
            return;
        }

        const int margemLateral = 16;
        const int espaco = 12;
        const int larguraCombo = 220;
        const int larguraBotao = 150;

        int topo = 17;
        int alturaCampo = 33;
        int larguraDisponivel = filtersCard.ClientSize.Width - (margemLateral * 2);

        atualizarButton.Size = new Size(larguraBotao, 32);
        atualizarButton.Location = new Point(
            Math.Max(margemLateral, filtersCard.ClientSize.Width - margemLateral - atualizarButton.Width),
            topo + 1);

        turnoInputPanel.Size = new Size(larguraCombo, alturaCampo);
        turnoInputPanel.Location = new Point(
            atualizarButton.Left - espaco - turnoInputPanel.Width,
            topo);

        turnoCaptionLabel.Location = new Point(turnoInputPanel.Left, 2);
        turnoCaptionLabel.Width = turnoInputPanel.Width;

        linhaInputPanel.Size = new Size(larguraCombo, alturaCampo);
        linhaInputPanel.Location = new Point(
            turnoInputPanel.Left - espaco - linhaInputPanel.Width,
            topo);

        linhaCaptionLabel.Location = new Point(linhaInputPanel.Left, 2);
        linhaCaptionLabel.Width = linhaInputPanel.Width;

        int searchLeft = Math.Max(margemLateral + 30, searchInputPanel.Left);
        int larguraPesquisa = Math.Max(
            320,
            linhaInputPanel.Left - espaco - searchLeft);

        searchInputPanel.Location = new Point(searchLeft, 14);
        searchInputPanel.Size = new Size(larguraPesquisa, alturaCampo);

        searchIconPictureBox.Location = new Point(margemLateral, 18);
        searchIconPictureBox.Size = new Size(20, 24);

        int larguraGrid = Math.Max(300, ordersCard.ClientSize.Width - 28);
        int alturaGrid = Math.Max(220, ordersCard.ClientSize.Height - 92);
        ordersDataGridView.Location = new Point(14, 42);
        ordersDataGridView.Size = new Size(larguraGrid, alturaGrid);

        AjustarColunasGridResponsivo();
    }

    private void AjustarColunasGridResponsivo()
    {
        if (ordersDataGridView.Columns.Count == 0)
        {
            return;
        }

        ordersDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

        colDataHora.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colTipo.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colOp.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colLote.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colQuantidade.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colUnidade.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colSituacao.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        colDetalhes.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

        colProduto.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        colProduto.FillWeight = 28;
        colCodigoBarras.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        colCodigoBarras.FillWeight = 18;
        colUsuario.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        colUsuario.FillWeight = 12;
        colOrigem.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        colOrigem.FillWeight = 12;
    }

    private sealed record IntegracaoSapGridItem(
        string DataHora,
        string Tipo,
        string Op,
        string Produto,
        string Lote,
        string CodigoBarras,
        string Quantidade,
        string Unidade,
        string Usuario,
        string Origem,
        string Situacao);
}


