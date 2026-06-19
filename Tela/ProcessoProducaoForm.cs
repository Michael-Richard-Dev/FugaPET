namespace FugaPET_Dev.Tela;

public partial class ProcessoProducaoForm : UserControl
{
    private const string ProducaoIconPath = "Servicos\\icone\\producao_24x_red.png";

    public event EventHandler? EntradaProdutoRequested;
    public event EventHandler? ProcessoProdutoAcabadoRequested;
    public event EventHandler? ProcessoPesagemApontamentoRequested;
    public event EventHandler? OrdensAndamentoRequested;

    public ProcessoProducaoForm()
    {
        InitializeComponent();
        ApplyProductionIcons();
        WireCardClickEvents();
    }

    private void ApplyProductionIcons()
    {
        string? iconPath = ResolveProductionIconPath();
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return;
        }

        using Bitmap source = new(iconPath);
        entradaIconLabel.Image = new Bitmap(source);
        processIconLabel.Image = new Bitmap(source);
        pesagemIconLabel.Image = new Bitmap(source);
        ordensIconLabel.Image = new Bitmap(source);
        entradaIconLabel.Text = string.Empty;
        processIconLabel.Text = string.Empty;
        pesagemIconLabel.Text = string.Empty;
        ordensIconLabel.Text = string.Empty;
    }

    private static string? ResolveProductionIconPath()
    {
        string runtimePath = Path.Combine(AppContext.BaseDirectory, ProducaoIconPath);
        if (File.Exists(runtimePath))
        {
            return runtimePath;
        }

        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int i = 0; i < 10 && current is not null; i++)
        {
            string projectMarker = Path.Combine(current.FullName, "FugaPET_Dev.csproj");
            if (File.Exists(projectMarker))
            {
                string designerPath = Path.Combine(current.FullName, ProducaoIconPath);
                if (File.Exists(designerPath))
                {
                    return designerPath;
                }
            }

            current = current.Parent;
        }

        return null;
    }

    private void WireCardClickEvents()
    {
        entradaProdutoCard.Click += OnEntradaProdutoClick;
        entradaIconPanel.Click += OnEntradaProdutoClick;
        entradaIconLabel.Click += OnEntradaProdutoClick;
        entradaTitleLabel.Click += OnEntradaProdutoClick;
        entradaDescriptionLabel.Click += OnEntradaProdutoClick;
        entradaStatusLabel.Click += OnEntradaProdutoClick;
        entradaShortcutLabel.Click += OnEntradaProdutoClick;
        entradaArrowLabel.Click += OnEntradaProdutoClick;

        processoProdutoAcabadoCard.Click += OnProcessoProdutoAcabadoClick;
        processIconPanel.Click += OnProcessoProdutoAcabadoClick;
        processIconLabel.Click += OnProcessoProdutoAcabadoClick;
        processTitleLabel.Click += OnProcessoProdutoAcabadoClick;
        processDescriptionLabel.Click += OnProcessoProdutoAcabadoClick;
        processStatusLabel.Click += OnProcessoProdutoAcabadoClick;
        processShortcutLabel.Click += OnProcessoProdutoAcabadoClick;
        processArrowLabel.Click += OnProcessoProdutoAcabadoClick;

        processoPesagemApontamentoCard.Click += OnProcessoPesagemApontamentoClick;
        pesagemIconPanel.Click += OnProcessoPesagemApontamentoClick;
        pesagemIconLabel.Click += OnProcessoPesagemApontamentoClick;
        pesagemTitleLabel.Click += OnProcessoPesagemApontamentoClick;
        pesagemDescriptionLabel.Click += OnProcessoPesagemApontamentoClick;
        pesagemStatusLabel.Click += OnProcessoPesagemApontamentoClick;
        pesagemShortcutLabel.Click += OnProcessoPesagemApontamentoClick;
        pesagemArrowLabel.Click += OnProcessoPesagemApontamentoClick;

        ordensAndamentoCard.Click += OnOrdensAndamentoClick;
        ordensIconPanel.Click += OnOrdensAndamentoClick;
        ordensIconLabel.Click += OnOrdensAndamentoClick;
        ordensTitleLabel.Click += OnOrdensAndamentoClick;
        ordensDescriptionLabel.Click += OnOrdensAndamentoClick;
        ordensStatusLabel.Click += OnOrdensAndamentoClick;
        ordensShortcutLabel.Click += OnOrdensAndamentoClick;
        ordensArrowLabel.Click += OnOrdensAndamentoClick;
    }

    private void OnEntradaProdutoClick(object? sender, EventArgs e)
    {
        EntradaProdutoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessoProdutoAcabadoClick(object? sender, EventArgs e)
    {
        ProcessoProdutoAcabadoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessoPesagemApontamentoClick(object? sender, EventArgs e)
    {
        ProcessoPesagemApontamentoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOrdensAndamentoClick(object? sender, EventArgs e)
    {
        OrdensAndamentoRequested?.Invoke(this, EventArgs.Empty);
    }
}



