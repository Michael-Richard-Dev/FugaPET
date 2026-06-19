namespace FugaPET_Dev.Tela;

public partial class CadastroForm : UserControl
{
    public event EventHandler? SetorRequested;
    public event EventHandler? CargoRequested;
    public event EventHandler? TaraRequested;
    public event EventHandler? TipoTaraRequested;
    public event EventHandler? BalancaRequested;
    public event EventHandler? EtiquetaRequested;
    public event EventHandler? ModeloEtiquetaRequested;

    public CadastroForm()
    {
        InitializeComponent();
        WireCardClickEvents();
    }

    private void WireCardClickEvents()
    {
        setorCard.Click += OnSetorClick;
        setorIconPanel.Click += OnSetorClick;
        setorIconLabel.Click += OnSetorClick;
        setorTitleLabel.Click += OnSetorClick;
        setorDescriptionLabel.Click += OnSetorClick;
        setorStatusLabel.Click += OnSetorClick;
        setorShortcutLabel.Click += OnSetorClick;
        setorArrowLabel.Click += OnSetorClick;

        cargoCard.Click += OnCargoClick;
        cargoIconPanel.Click += OnCargoClick;
        cargoIconLabel.Click += OnCargoClick;
        cargoTitleLabel.Click += OnCargoClick;
        cargoDescriptionLabel.Click += OnCargoClick;
        cargoStatusLabel.Click += OnCargoClick;
        cargoShortcutLabel.Click += OnCargoClick;
        cargoArrowLabel.Click += OnCargoClick;

        taraCard.Click += OnTaraClick;
        taraIconPanel.Click += OnTaraClick;
        taraIconLabel.Click += OnTaraClick;
        taraTitleLabel.Click += OnTaraClick;
        taraDescriptionLabel.Click += OnTaraClick;
        taraStatusLabel.Click += OnTaraClick;
        taraShortcutLabel.Click += OnTaraClick;
        taraArrowLabel.Click += OnTaraClick;

        balancaCard.Click += OnBalancaClick;
        balancaIconPanel.Click += OnBalancaClick;
        balancaIconLabel.Click += OnBalancaClick;
        balancaTitleLabel.Click += OnBalancaClick;
        balancaDescriptionLabel.Click += OnBalancaClick;
        balancaStatusLabel.Click += OnBalancaClick;
        balancaShortcutLabel.Click += OnBalancaClick;
        balancaArrowLabel.Click += OnBalancaClick;

        etiquetaCard.Click += OnEtiquetaClick;
        etiquetaIconPanel.Click += OnEtiquetaClick;
        etiquetaIconLabel.Click += OnEtiquetaClick;
        etiquetaTitleLabel.Click += OnEtiquetaClick;
        etiquetaDescriptionLabel.Click += OnEtiquetaClick;
        etiquetaStatusLabel.Click += OnEtiquetaClick;
        etiquetaShortcutLabel.Click += OnEtiquetaClick;
        etiquetaArrowLabel.Click += OnEtiquetaClick;

        tipoTaraCard.Click += OnTipoTaraClick;
        tipoTaraIconPanel.Click += OnTipoTaraClick;
        tipoTaraIconLabel.Click += OnTipoTaraClick;
        tipoTaraTitleLabel.Click += OnTipoTaraClick;
        tipoTaraDescriptionLabel.Click += OnTipoTaraClick;
        tipoTaraStatusLabel.Click += OnTipoTaraClick;
        tipoTaraShortcutLabel.Click += OnTipoTaraClick;
        tipoTaraArrowLabel.Click += OnTipoTaraClick;

        modeloEtiquetaCard.Click += OnModeloEtiquetaClick;
        modeloEtiquetaIconPanel.Click += OnModeloEtiquetaClick;
        modeloEtiquetaIconLabel.Click += OnModeloEtiquetaClick;
        modeloEtiquetaTitleLabel.Click += OnModeloEtiquetaClick;
        modeloEtiquetaDescriptionLabel.Click += OnModeloEtiquetaClick;
        modeloEtiquetaStatusLabel.Click += OnModeloEtiquetaClick;
        modeloEtiquetaShortcutLabel.Click += OnModeloEtiquetaClick;
        modeloEtiquetaArrowLabel.Click += OnModeloEtiquetaClick;
    }

    private void OnSetorClick(object? sender, EventArgs e)
    {
        SetorRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCargoClick(object? sender, EventArgs e)
    {
        CargoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTaraClick(object? sender, EventArgs e)
    {
        TaraRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTipoTaraClick(object? sender, EventArgs e)
    {
        TipoTaraRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnModeloEtiquetaClick(object? sender, EventArgs e)
    {
        ModeloEtiquetaRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnBalancaClick(object? sender, EventArgs e)
    {
        BalancaRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnEtiquetaClick(object? sender, EventArgs e)
    {
        EtiquetaRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F1)
        {
            OnSetorClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F2)
        {
            OnCargoClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F3)
        {
            OnBalancaClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F4)
        {
            OnTipoTaraClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F5)
        {
            OnTaraClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F6)
        {
            OnEtiquetaClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F7)
        {
            OnModeloEtiquetaClick(this, EventArgs.Empty);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}



