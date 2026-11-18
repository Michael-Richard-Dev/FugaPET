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
    public event EventHandler? VinculoProdutoEtiquetaRequested;

    public CadastroForm()
    {
        InitializeComponent();
        CriarCartaoVinculoProdutoEtiqueta();
        if (System.ComponentModel.LicenseManager.UsageMode != System.ComponentModel.LicenseUsageMode.Designtime)
        {
            AplicarFiltroPermissoes();
        }
        WireCardClickEvents();
    }

    /// <summary>
    /// Cartao do Vinculo de Produto e Etiqueta, montado EM TEMPO DE EXECUCAO a partir do cartao de
    /// Etiqueta que ja existe.
    /// <para>
    /// Por que clonar em vez de desenhar no Designer: toda geometria vem LIDA do cartao vivo —
    /// tamanho, margens, posicao e fonte de cada parte. Assim o cartao novo herda o que o DPI ja fez
    /// com o antigo, em vez de trazer pixels cravados que quebram em outra resolucao; e, se o
    /// desenho dos cartoes mudar, este acompanha sem ninguem lembrar de atualizar dois lugares.
    /// </para>
    /// </summary>
    private void CriarCartaoVinculoProdutoEtiqueta()
    {
        Controls.RoundedPanel cartao = new()
        {
            BackColor = etiquetaCard.BackColor,
            BorderColor = etiquetaCard.BorderColor,
            Cursor = Cursors.Hand,
            Margin = etiquetaCard.Margin,
            Name = "vinculoProdutoEtiquetaCard",
            ShadowBlur = etiquetaCard.ShadowBlur,
            ShadowOffsetY = etiquetaCard.ShadowOffsetY,
            Size = etiquetaCard.Size,
        };

        Controls.RoundedPanel icone = new()
        {
            BackColor = etiquetaIconPanel.BackColor,
            BorderRadius = etiquetaIconPanel.BorderRadius,
            Cursor = Cursors.Hand,
            // Cor propria para o cartao nao virar copia visual do de Etiqueta: sao coisas diferentes.
            FillColor = Color.FromArgb(254, 240, 221),
            Location = etiquetaIconPanel.Location,
            Size = etiquetaIconPanel.Size,
        };

        Label simboloIcone = new()
        {
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Image = etiquetaIconLabel.Image,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        icone.Controls.Add(simboloIcone);

        Label titulo = ClonarRotuloDoCartao(etiquetaTitleLabel, "Vínculo de Produto");
        Label descricao = ClonarRotuloDoCartao(
            etiquetaDescriptionLabel, "Produto acabado, etiqueta e processo de pesagem.");
        Label situacao = ClonarRotuloDoCartao(etiquetaStatusLabel, "Disponível");
        Label atalho = ClonarRotuloDoCartao(etiquetaShortcutLabel, "F8");
        Label seta = ClonarRotuloDoCartao(etiquetaArrowLabel, etiquetaArrowLabel.Text);

        cartao.Controls.AddRange([icone, titulo, descricao, situacao, atalho, seta]);
        modulesFlowLayoutPanel.Controls.Add(cartao);

        foreach (Control parte in new Control[] { cartao, icone, simboloIcone, titulo, descricao, situacao, atalho, seta })
        {
            parte.Click += OnVinculoProdutoEtiquetaClick;
        }

        _cartaoVinculoProdutoEtiqueta = cartao;
    }

    private Control? _cartaoVinculoProdutoEtiqueta;

    /// <summary>Copia aparencia e geometria de um rotulo do cartao, trocando so o texto.</summary>
    private static Label ClonarRotuloDoCartao(Label modelo, string texto) => new()
    {
        BackColor = modelo.BackColor,
        Cursor = Cursors.Hand,
        Font = modelo.Font,
        ForeColor = modelo.ForeColor,
        Location = modelo.Location,
        Size = modelo.Size,
        Text = texto,
        TextAlign = modelo.TextAlign,
    };

    private void OnVinculoProdutoEtiquetaClick(object? sender, EventArgs e)
        => VinculoProdutoEtiquetaRequested?.Invoke(this, EventArgs.Empty);

    private void AplicarFiltroPermissoes()
    {
        ConfigurarVisibilidadeCadastro(
            setorCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Cadastro,
            Servicos.Seguranca.PermissoesSistema.Rotinas.Setor);

        ConfigurarVisibilidadeCadastro(
            cargoCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Cadastro,
            Servicos.Seguranca.PermissoesSistema.Rotinas.Cargo);

        ConfigurarVisibilidadeCadastro(
            balancaCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Cadastro,
            Servicos.Seguranca.PermissoesSistema.Rotinas.Balanca);

        ConfigurarVisibilidadeCadastro(
            tipoTaraCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Cadastro,
            Servicos.Seguranca.PermissoesSistema.Rotinas.TipoTara);

        ConfigurarVisibilidadeCadastro(
            taraCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Cadastro,
            Servicos.Seguranca.PermissoesSistema.Rotinas.Tara);

        ConfigurarVisibilidadeCadastro(
            etiquetaCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Etiqueta,
            Servicos.Seguranca.PermissoesSistema.Rotinas.Etiqueta);

        ConfigurarVisibilidadeCadastro(
            modeloEtiquetaCard,
            Servicos.Seguranca.PermissoesSistema.Modulos.Etiqueta,
            Servicos.Seguranca.PermissoesSistema.Rotinas.ModeloEtiqueta);

        // Mesma permissao do cadastro de etiqueta: quem administra etiqueta decide este vinculo.
        if (_cartaoVinculoProdutoEtiqueta is not null)
        {
            ConfigurarVisibilidadeCadastro(
                _cartaoVinculoProdutoEtiqueta,
                Servicos.Seguranca.PermissoesSistema.Modulos.Etiqueta,
                Servicos.Seguranca.PermissoesSistema.Rotinas.Etiqueta);
        }
    }

    private static void ConfigurarVisibilidadeCadastro(Control opcao, string modulo, string rotina)
    {
        opcao.Visible = UsuarioPodeVerCadastro(modulo, rotina);
    }

    private static bool UsuarioPodeVerCadastro(string modulo, string rotina)
    {
        return Servicos.Seguranca.AutorizacaoServico.PodeVisualizarRotina(modulo, rotina);
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
        if (keyData == Keys.F1 && setorCard.Visible)
        {
            OnSetorClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F2 && cargoCard.Visible)
        {
            OnCargoClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F3 && balancaCard.Visible)
        {
            OnBalancaClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F4 && tipoTaraCard.Visible)
        {
            OnTipoTaraClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F5 && taraCard.Visible)
        {
            OnTaraClick(this, EventArgs.Empty);
            return true;
        }

        // Reordenação Modelo → Etiqueta: F6 abre Modelo de Etiqueta, F7 abre Etiqueta.
        if (keyData == Keys.F6 && modeloEtiquetaCard.Visible)
        {
            OnModeloEtiquetaClick(this, EventArgs.Empty);
            return true;
        }

        if (keyData == Keys.F7 && etiquetaCard.Visible)
        {
            OnEtiquetaClick(this, EventArgs.Empty);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
