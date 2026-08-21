using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Controls;

namespace FugaPET_Dev.Tela;

public partial class ProcessoProducaoForm : UserControl
{
    private const string ProducaoIconPath = "Servicos\\icone\\producao_24x_red.png";
    private RoundedPanel? paletizacaoCard;
    private Label? paletizacaoIconLabel;
    private Label? paletizacaoTitleLabel;
    private Label? paletizacaoDescriptionLabel;
    private Label? paletizacaoStatusLabel;
    private Label? paletizacaoShortcutLabel;
    private Label? paletizacaoArrowLabel;

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
    public event EventHandler? PaletizacaoRequested;

    /// <summary>Controle de Apontamentos (F8): leitura/início/término das operações da OP.</summary>
    public event EventHandler? ControleApontamentosRequested;

    public ProcessoProducaoForm()
    {
        InitializeComponent();
        AddPaletizacaoCard();
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

    private void AddPaletizacaoCard()
    {
        contentPanel.AutoScroll = true;
        paletizacaoCard = CriarModuloCard(
            "paletizacaoCard",
            new Point(28, 602),
            "Paletização por\r\nHU",
            "Formação de paletes\r\npor HU de caixas",
            "F9",
            out paletizacaoIconLabel,
            out paletizacaoTitleLabel,
            out paletizacaoDescriptionLabel,
            out paletizacaoStatusLabel,
            out paletizacaoShortcutLabel,
            out paletizacaoArrowLabel);

        contentPanel.Controls.Add(paletizacaoCard);
        paletizacaoCard.BringToFront();
    }

    private static RoundedPanel CriarModuloCard(
        string name,
        Point location,
        string title,
        string description,
        string shortcut,
        out Label iconLabel,
        out Label titleLabel,
        out Label descriptionLabel,
        out Label statusLabel,
        out Label shortcutLabel,
        out Label arrowLabel)
    {
        RoundedPanel card = new()
        {
            BackColor = Color.Transparent,
            BorderColor = Color.FromArgb(226, 232, 240),
            Cursor = Cursors.Hand,
            Location = location,
            Name = name,
            ShadowBlur = 0,
            ShadowOffsetY = 0,
            Size = new Size(240, 250)
        };

        RoundedPanel iconPanel = new()
        {
            BackColor = Color.Transparent,
            BorderRadius = 9,
            Cursor = Cursors.Hand,
            FillColor = Color.FromArgb(254, 226, 226),
            Location = new Point(92, 20),
            Name = name + "IconPanel",
            ShadowBlur = 0,
            ShadowOffsetY = 0,
            Size = new Size(56, 56)
        };
        iconLabel = new Label
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe MDL2 Assets", 22F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(229, 27, 43),
            ImageAlign = ContentAlignment.MiddleCenter,
            Text = string.Empty,
            TextAlign = ContentAlignment.MiddleCenter
        };
        iconPanel.Controls.Add(iconLabel);

        titleLabel = new Label
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(17, 24, 39),
            Location = new Point(20, 92),
            Size = new Size(202, 62),
            Text = title,
            TextAlign = ContentAlignment.MiddleCenter
        };
        descriptionLabel = new Label
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 156),
            Size = new Size(175, 46),
            Text = description,
            TextAlign = ContentAlignment.TopCenter
        };
        statusLabel = new Label
        {
            BackColor = Color.FromArgb(220, 252, 231),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(22, 163, 74),
            Location = new Point(20, 214),
            Size = new Size(82, 28),
            Text = "Disponível",
            TextAlign = ContentAlignment.MiddleCenter
        };
        shortcutLabel = new Label
        {
            BackColor = Color.FromArgb(241, 245, 249),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(110, 214),
            Size = new Size(38, 28),
            Text = shortcut,
            TextAlign = ContentAlignment.MiddleCenter
        };
        arrowLabel = new Label
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 16F, FontStyle.Regular, GraphicsUnit.Point, 0),
            ForeColor = Color.FromArgb(239, 68, 68),
            Location = new Point(190, 207),
            Size = new Size(32, 36),
            Text = "→",
            TextAlign = ContentAlignment.MiddleCenter
        };

        card.Controls.Add(iconPanel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(descriptionLabel);
        card.Controls.Add(statusLabel);
        card.Controls.Add(shortcutLabel);
        card.Controls.Add(arrowLabel);
        return card;
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
        apontamentosIconLabel.Image = new Bitmap(source);
        if (paletizacaoIconLabel is not null) { paletizacaoIconLabel.Image = new Bitmap(source); }
        entradaIconLabel.Text = string.Empty;
        entradaQuimicosIconLabel.Text = string.Empty;
        processIconLabel.Text = string.Empty;
        pesagemIconLabel.Text = string.Empty;
        quimicosIconLabel.Text = string.Empty;
        semiAcabadoIconLabel.Text = string.Empty;
        ordensIconLabel.Text = string.Empty;
        apontamentosIconLabel.Text = string.Empty;
        if (paletizacaoIconLabel is not null) { paletizacaoIconLabel.Text = string.Empty; }
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

        // Controle de Apontamentos (F8): todos os controles do card acionam o mesmo evento.
        controleApontamentosCard.Click += OnControleApontamentosClick;
        apontamentosIconPanel.Click += OnControleApontamentosClick;
        apontamentosIconLabel.Click += OnControleApontamentosClick;
        apontamentosTitleLabel.Click += OnControleApontamentosClick;
        apontamentosDescriptionLabel.Click += OnControleApontamentosClick;
        apontamentosStatusLabel.Click += OnControleApontamentosClick;
        apontamentosShortcutLabel.Click += OnControleApontamentosClick;
        apontamentosArrowLabel.Click += OnControleApontamentosClick;

        ConectarPaletizacaoCard();
    }

    private void ConectarPaletizacaoCard()
    {
        if (paletizacaoCard is null) { return; }
        paletizacaoCard.Click += OnPaletizacaoClick;
        paletizacaoIconLabel!.Click += OnPaletizacaoClick;
        paletizacaoTitleLabel!.Click += OnPaletizacaoClick;
        paletizacaoDescriptionLabel!.Click += OnPaletizacaoClick;
        paletizacaoStatusLabel!.Click += OnPaletizacaoClick;
        paletizacaoShortcutLabel!.Click += OnPaletizacaoClick;
        paletizacaoArrowLabel!.Click += OnPaletizacaoClick;
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

    private void OnControleApontamentosClick(object? sender, EventArgs e)
    {
        ControleApontamentosRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnPaletizacaoClick(object? sender, EventArgs e)
    {
        PaletizacaoRequested?.Invoke(this, EventArgs.Empty);
    }
}







