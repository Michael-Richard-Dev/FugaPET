using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tela;

public partial class ProcessoProducaoForm : UserControl
{
    private const string ProducaoIconPath = "Servicos\\icone\\producao_24x_red.png";

    // Tarefa Entrada 24.1b: cards separados de Entrada por modo (Matéria-Prima × Químicos).
    public event EventHandler? EntradaMateriaPrimaRequested;
    public event EventHandler? EntradaQuimicosRequested;
    public event EventHandler? ProcessoProdutoAcabadoRequested;
    public event EventHandler? ProcessoSemiAcabadoRequested;
    public event EventHandler? ProcessoConsumoMaterialRequested;
    public event EventHandler? ProcessoConsumoQuimicosRequested;
    public event EventHandler? HistoricoConsumoMaterialRequested;
    public event EventHandler? DiagnosticoConsumoSap261Requested;
    public event EventHandler? OrdensAndamentoRequested;

    public ProcessoProducaoForm()
    {
        InitializeComponent();
        ApplyProductionIcons();
        AddHistoricoConsumoButton();
        AddDiagnosticoConsumoButton();
        WireCardClickEvents();
    }

    private void AddHistoricoConsumoButton()
    {
        Button historicoConsumoButton = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(Math.Max(16, Width - 214), 26),
            Name = "historicoConsumoButton",
            Size = new Size(186, 30),
            Text = "Histórico de Consumo",
            UseVisualStyleBackColor = false
        };
        historicoConsumoButton.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        historicoConsumoButton.Click += OnHistoricoConsumoMaterialClick;
        contentPanel.Controls.Add(historicoConsumoButton);
        historicoConsumoButton.BringToFront();
    }

    private void AddDiagnosticoConsumoButton()
    {
        Button diagnosticoConsumoButton = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(31, 41, 55),
            Location = new Point(Math.Max(16, Width - 214), 60),
            Name = "diagnosticoConsumoButton",
            Size = new Size(186, 30),
            Text = "Diagnóstico Consumo 261",
            UseVisualStyleBackColor = false
        };
        diagnosticoConsumoButton.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
        diagnosticoConsumoButton.Click += OnDiagnosticoConsumoSap261Click;
        contentPanel.Controls.Add(diagnosticoConsumoButton);
        diagnosticoConsumoButton.BringToFront();
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
        entradaQuimicosIconLabel.Image = new Bitmap(source);
        processIconLabel.Image = new Bitmap(source);
        pesagemIconLabel.Image = new Bitmap(source);
        quimicosIconLabel.Image = new Bitmap(source);
        semiAcabadoIconLabel.Image = new Bitmap(source);
        ordensIconLabel.Image = new Bitmap(source);
        entradaIconLabel.Text = string.Empty;
        entradaQuimicosIconLabel.Text = string.Empty;
        processIconLabel.Text = string.Empty;
        pesagemIconLabel.Text = string.Empty;
        quimicosIconLabel.Text = string.Empty;
        semiAcabadoIconLabel.Text = string.Empty;
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
        entradaProdutoCard.Click += OnEntradaMateriaPrimaClick;
        entradaIconPanel.Click += OnEntradaMateriaPrimaClick;
        entradaIconLabel.Click += OnEntradaMateriaPrimaClick;
        entradaTitleLabel.Click += OnEntradaMateriaPrimaClick;
        entradaDescriptionLabel.Click += OnEntradaMateriaPrimaClick;
        entradaStatusLabel.Click += OnEntradaMateriaPrimaClick;
        entradaShortcutLabel.Click += OnEntradaMateriaPrimaClick;
        entradaArrowLabel.Click += OnEntradaMateriaPrimaClick;

        entradaQuimicosCard.Click += OnEntradaQuimicosClick;
        entradaQuimicosIconPanel.Click += OnEntradaQuimicosClick;
        entradaQuimicosIconLabel.Click += OnEntradaQuimicosClick;
        entradaQuimicosTitleLabel.Click += OnEntradaQuimicosClick;
        entradaQuimicosDescriptionLabel.Click += OnEntradaQuimicosClick;
        entradaQuimicosStatusLabel.Click += OnEntradaQuimicosClick;
        entradaQuimicosShortcutLabel.Click += OnEntradaQuimicosClick;
        entradaQuimicosArrowLabel.Click += OnEntradaQuimicosClick;

        processoSemiAcabadoCard.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoIconPanel.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoIconLabel.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoTitleLabel.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoDescriptionLabel.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoStatusLabel.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoShortcutLabel.Click += OnProcessoSemiAcabadoClick;
        semiAcabadoArrowLabel.Click += OnProcessoSemiAcabadoClick;

        processoProdutoAcabadoCard.Click += OnProcessoProdutoAcabadoClick;
        processIconPanel.Click += OnProcessoProdutoAcabadoClick;
        processIconLabel.Click += OnProcessoProdutoAcabadoClick;
        processTitleLabel.Click += OnProcessoProdutoAcabadoClick;
        processDescriptionLabel.Click += OnProcessoProdutoAcabadoClick;
        processStatusLabel.Click += OnProcessoProdutoAcabadoClick;
        processShortcutLabel.Click += OnProcessoProdutoAcabadoClick;
        processArrowLabel.Click += OnProcessoProdutoAcabadoClick;

        processoConsumoMaterialCard.Click += OnProcessoConsumoMaterialClick;
        pesagemIconPanel.Click += OnProcessoConsumoMaterialClick;
        pesagemIconLabel.Click += OnProcessoConsumoMaterialClick;
        pesagemTitleLabel.Click += OnProcessoConsumoMaterialClick;
        pesagemDescriptionLabel.Click += OnProcessoConsumoMaterialClick;
        pesagemStatusLabel.Click += OnProcessoConsumoMaterialClick;
        pesagemShortcutLabel.Click += OnProcessoConsumoMaterialClick;
        pesagemArrowLabel.Click += OnProcessoConsumoMaterialClick;

        processoConsumoQuimicosCard.Click += OnProcessoConsumoQuimicosClick;
        quimicosIconPanel.Click += OnProcessoConsumoQuimicosClick;
        quimicosIconLabel.Click += OnProcessoConsumoQuimicosClick;
        quimicosTitleLabel.Click += OnProcessoConsumoQuimicosClick;
        quimicosDescriptionLabel.Click += OnProcessoConsumoQuimicosClick;
        quimicosStatusLabel.Click += OnProcessoConsumoQuimicosClick;
        quimicosShortcutLabel.Click += OnProcessoConsumoQuimicosClick;
        quimicosArrowLabel.Click += OnProcessoConsumoQuimicosClick;

        ordensAndamentoCard.Click += OnOrdensAndamentoClick;
        ordensIconPanel.Click += OnOrdensAndamentoClick;
        ordensIconLabel.Click += OnOrdensAndamentoClick;
        ordensTitleLabel.Click += OnOrdensAndamentoClick;
        ordensDescriptionLabel.Click += OnOrdensAndamentoClick;
        ordensStatusLabel.Click += OnOrdensAndamentoClick;
        ordensShortcutLabel.Click += OnOrdensAndamentoClick;
        ordensArrowLabel.Click += OnOrdensAndamentoClick;
    }

    private void OnEntradaMateriaPrimaClick(object? sender, EventArgs e)
    {
        _ = ModoEntradaMaterial.MateriaPrima;
        EntradaMateriaPrimaRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnEntradaQuimicosClick(object? sender, EventArgs e)
    {
        _ = ModoEntradaMaterial.Quimico;
        EntradaQuimicosRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessoProdutoAcabadoClick(object? sender, EventArgs e)
    {
        ProcessoProdutoAcabadoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessoSemiAcabadoClick(object? sender, EventArgs e)
    {
        ProcessoSemiAcabadoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessoConsumoMaterialClick(object? sender, EventArgs e)
    {
        ProcessoConsumoMaterialRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnProcessoConsumoQuimicosClick(object? sender, EventArgs e)
    {
        _ = ModoConsumoMaterial.Quimico;
        ProcessoConsumoQuimicosRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnHistoricoConsumoMaterialClick(object? sender, EventArgs e)
    {
        HistoricoConsumoMaterialRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDiagnosticoConsumoSap261Click(object? sender, EventArgs e)
    {
        DiagnosticoConsumoSap261Requested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOrdensAndamentoClick(object? sender, EventArgs e)
    {
        OrdensAndamentoRequested?.Invoke(this, EventArgs.Empty);
    }
}

