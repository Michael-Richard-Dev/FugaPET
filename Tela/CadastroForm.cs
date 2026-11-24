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
    public event EventHandler? ToleranciaRecebimentoRequested;
    public event EventHandler? ToleranciaProducaoRequested;

    public CadastroForm()
    {
        InitializeComponent();
        CriarCartaoVinculoProdutoEtiqueta();
        CriarCartaoToleranciaRecebimento();
        CriarCartaoToleranciaProducao();
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
        => _cartaoVinculoProdutoEtiqueta = CriarCartaoClonado(
            nome: "vinculoProdutoEtiquetaCard",
            titulo: "Vínculo de Produto",
            descricao: "Produto acabado, etiqueta e processo de pesagem.",
            atalho: "F8",
            corDoIcone: Color.FromArgb(254, 240, 221),
            aoClicar: OnVinculoProdutoEtiquetaClick);

    /// <summary>
    /// Tolerancia de recebimento por material (incremento 069). Irmao do cartao de Vinculo, com o
    /// mesmo mecanismo de clonagem — ver <see cref="CriarCartaoClonado"/>.
    /// </summary>
    private void CriarCartaoToleranciaRecebimento()
        => _cartaoToleranciaRecebimento = CriarCartaoClonado(
            nome: "toleranciaRecebimentoCard",
            titulo: "Tolerância de Recebimento",
            descricao: "Quanto a pesagem pode divergir do pedido, por material.",
            atalho: "F9",
            // Verde-agua: distingue do laranja-claro do Vinculo, para os dois cartoes clonados nao
            // virarem um par indistinguivel no meio da grade.
            corDoIcone: Color.FromArgb(222, 247, 236),
            aoClicar: OnToleranciaRecebimentoClick);

    /// <summary>
    /// Tolerancia por produto nos processos de producao (incremento 071). Terceiro cartao clonado —
    /// ver <see cref="CriarCartaoClonado"/>.
    /// <para>
    /// Cartao SEPARADO do de Recebimento, e nao uma aba dentro dele, porque sao decisoes de
    /// naturezas diferentes: la a referencia e o pedido de compra, aqui e a ordem de producao. Juntar
    /// os dois numa tela so faria o usuario escolher a referencia errada sem perceber.
    /// </para>
    /// </summary>
    private void CriarCartaoToleranciaProducao()
        => _cartaoToleranciaProducao = CriarCartaoClonado(
            nome: "toleranciaProducaoCard",
            titulo: "Tolerância de Produção",
            descricao: "Quanto a pesagem pode divergir da OP, por produto e processo.",
            atalho: "F10",
            // Azul-claro: terceiro tom, para os cartoes clonados continuarem distinguiveis entre si.
            corDoIcone: Color.FromArgb(219, 234, 254),
            aoClicar: OnToleranciaProducaoClick);

    /// <summary>
    /// Monta um cartao novo COPIANDO a geometria do cartao de Etiqueta que ja existe.
    /// <para>
    /// Por que clonar em vez de desenhar no Designer: toda geometria vem LIDA do cartao vivo —
    /// tamanho, margens, posicao e fonte de cada parte. Assim o cartao novo herda o que o DPI ja fez
    /// com o antigo, em vez de trazer pixels cravados que quebram em outra resolucao; e, se o desenho
    /// dos cartoes mudar, estes acompanham sem ninguem lembrar de atualizar varios lugares.
    /// </para>
    /// </summary>
    private Control CriarCartaoClonado(
        string nome, string titulo, string descricao, string atalho, Color corDoIcone, EventHandler aoClicar)
    {
        Controls.RoundedPanel cartao = new()
        {
            BackColor = etiquetaCard.BackColor,
            BorderColor = etiquetaCard.BorderColor,
            Cursor = Cursors.Hand,
            Margin = etiquetaCard.Margin,
            Name = nome,
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
            FillColor = corDoIcone,
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

        Label rotuloTitulo = ClonarRotuloDoCartao(etiquetaTitleLabel, titulo);
        Label rotuloDescricao = ClonarRotuloDoCartao(etiquetaDescriptionLabel, descricao);
        Label situacao = ClonarRotuloDoCartao(etiquetaStatusLabel, "Disponível");
        Label rotuloAtalho = ClonarRotuloDoCartao(etiquetaShortcutLabel, atalho);
        Label seta = ClonarRotuloDoCartao(etiquetaArrowLabel, etiquetaArrowLabel.Text);

        cartao.Controls.AddRange([icone, rotuloTitulo, rotuloDescricao, situacao, rotuloAtalho, seta]);
        modulesFlowLayoutPanel.Controls.Add(cartao);

        foreach (Control parte in new Control[]
                 { cartao, icone, simboloIcone, rotuloTitulo, rotuloDescricao, situacao, rotuloAtalho, seta })
        {
            parte.Click += aoClicar;
        }

        return cartao;
    }

    private Control? _cartaoVinculoProdutoEtiqueta;
    private Control? _cartaoToleranciaRecebimento;
    private Control? _cartaoToleranciaProducao;

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

    private void OnToleranciaRecebimentoClick(object? sender, EventArgs e)
        => ToleranciaRecebimentoRequested?.Invoke(this, EventArgs.Empty);

    private void OnToleranciaProducaoClick(object? sender, EventArgs e)
        => ToleranciaProducaoRequested?.Invoke(this, EventArgs.Empty);

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

        // Entra pela permissao da propria tela de Recebimento (ENTRADA_PRODUTO), pela mesma razao que
        // levou o Vinculo a entrar por ETIQUETA: rotina nova exigiria semear permissao no banco antes
        // de a tela poder abrir para alguem.
        //
        // RESSALVA HONESTA: isso deixa quem OPERA o recebimento mudar a propria tolerancia, o que
        // enfraquece o controle. Se virar perfil separado, e so trocar a rotina aqui e no painel.
        if (_cartaoToleranciaRecebimento is not null)
        {
            ConfigurarVisibilidadeCadastro(
                _cartaoToleranciaRecebimento,
                Servicos.Seguranca.PermissoesSistema.Modulos.ProcessoProducao,
                Servicos.Seguranca.PermissoesSistema.Rotinas.EntradaProduto);
        }

        // Entra por LEITURA_PRODUCAO, que e a rotina das proprias telas de Consumo e Semi-Acabado —
        // mesma logica do cartao acima, e a mesma ressalva honesta: quem OPERA a producao pode mudar
        // a propria tolerancia. Se virar perfil separado, e so trocar a rotina aqui e no painel.
        if (_cartaoToleranciaProducao is not null)
        {
            ConfigurarVisibilidadeCadastro(
                _cartaoToleranciaProducao,
                Servicos.Seguranca.PermissoesSistema.Modulos.ProcessoProducao,
                Servicos.Seguranca.PermissoesSistema.Rotinas.LeituraProducao);
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
