using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 24.1: separação Entrada de Matéria-Prima × Entrada de Químicos por ProductType do material
/// do ITEM do Pedido de Compra (A_Product). Núcleo de domínio + serviço A_Product + parametrização de tela.
/// </summary>
public sealed class EntradaSeparacaoModo241Tests
{
    // ---- Ajuste 1/2: enum + configuração por modo ----

    [Fact]
    public void Enum_PossuiMateriaPrimaEQuimico()
    {
        Assert.Equal(1, (int)ModoEntradaMaterial.MateriaPrima);
        Assert.Equal(2, (int)ModoEntradaMaterial.Quimico);
    }

    [Fact]
    public void Config_MateriaPrima_TextosEsperados()
    {
        ConfiguracaoTelaEntradaMaterial c = ConfiguracaoTelaEntradaMaterialFactory.Criar(ModoEntradaMaterial.MateriaPrima);
        Assert.Equal("Entrada de Matéria-Prima", c.TituloTela);
        Assert.Equal("Pesagem e entrada de matéria-prima por pedido de compra / SAP", c.SubtituloTela);
        Assert.Equal("ENTRADA_MATERIA_PRIMA", c.TipoBalancaPreferencial);
        Assert.False(c.UsarFiltroQuimicos);
    }

    [Fact]
    public void Config_Quimico_TextosEsperados()
    {
        ConfiguracaoTelaEntradaMaterial c = ConfiguracaoTelaEntradaMaterialFactory.Criar(ModoEntradaMaterial.Quimico);
        Assert.Equal("Entrada de Químicos", c.TituloTela);
        Assert.Equal("Pesagem e entrada de químicos por pedido de compra / SAP", c.SubtituloTela);
        Assert.Equal("ENTRADA_QUIMICOS", c.TipoBalancaPreferencial);
        Assert.True(c.UsarFiltroQuimicos);
    }

    // ---- Ajuste 6: classificador por ProductType ----

    [Theory]
    [InlineData("ROH", ClassificacaoEntradaMaterial.MateriaPrima)]
    [InlineData("roh", ClassificacaoEntradaMaterial.MateriaPrima)]
    [InlineData("HIBE", ClassificacaoEntradaMaterial.Quimico)]
    [InlineData("VERP", ClassificacaoEntradaMaterial.Embalagem)]
    [InlineData("FERT", ClassificacaoEntradaMaterial.ProdutoAcabado)]
    [InlineData("HALB", ClassificacaoEntradaMaterial.Semiacabado)]
    [InlineData("ZZZ", ClassificacaoEntradaMaterial.Outro)]
    [InlineData("", ClassificacaoEntradaMaterial.Indefinido)]
    [InlineData(null, ClassificacaoEntradaMaterial.Indefinido)]
    public void Classificar_PorProductType(string? tipo, ClassificacaoEntradaMaterial esperado)
        => Assert.Equal(esperado, ClassificadorItemEntradaMaterial.ClassificarPorProductType(tipo));

    [Theory]
    [InlineData(ClassificacaoEntradaMaterial.MateriaPrima, ModoEntradaMaterial.MateriaPrima, true)]
    [InlineData(ClassificacaoEntradaMaterial.MateriaPrima, ModoEntradaMaterial.Quimico, false)]
    [InlineData(ClassificacaoEntradaMaterial.Quimico, ModoEntradaMaterial.Quimico, true)]
    [InlineData(ClassificacaoEntradaMaterial.Quimico, ModoEntradaMaterial.MateriaPrima, false)]
    [InlineData(ClassificacaoEntradaMaterial.Indefinido, ModoEntradaMaterial.MateriaPrima, false)]
    public void ItemPertenceAoModo(ClassificacaoEntradaMaterial classificacao, ModoEntradaMaterial modo, bool esperado)
        => Assert.Equal(esperado, ClassificadorItemEntradaMaterial.ItemPertenceAoModo(classificacao, modo));

    // ---- Ajuste 9/10/13: filtro por modo em pedido misto ----

    private static PedidoCompraSapItem Item(string numero, ClassificacaoEntradaMaterial classe)
        => new() { NumeroItem = numero, CodigoMaterial = "M" + numero, ClassificacaoEntrada = classe };

    [Fact]
    public void Filtro_PedidoMisto_MateriaPrima_MostraSoRoh()
    {
        List<PedidoCompraSapItem> itens = [Item("10", ClassificacaoEntradaMaterial.MateriaPrima), Item("20", ClassificacaoEntradaMaterial.Quimico)];
        IReadOnlyList<PedidoCompraSapItem> filtrados = FiltroItensEntradaMaterial.FiltrarItensPorModo(itens, ModoEntradaMaterial.MateriaPrima);
        Assert.Single(filtrados);
        Assert.Equal("10", filtrados[0].NumeroItem);
    }

    [Fact]
    public void Filtro_PedidoMisto_Quimico_MostraSoHibe()
    {
        List<PedidoCompraSapItem> itens = [Item("10", ClassificacaoEntradaMaterial.MateriaPrima), Item("20", ClassificacaoEntradaMaterial.Quimico)];
        IReadOnlyList<PedidoCompraSapItem> filtrados = FiltroItensEntradaMaterial.FiltrarItensPorModo(itens, ModoEntradaMaterial.Quimico);
        Assert.Single(filtrados);
        Assert.Equal("20", filtrados[0].NumeroItem);
    }

    [Fact]
    public void Contar_Totais_PedidoMisto()
    {
        List<PedidoCompraSapItem> itens =
        [
            Item("10", ClassificacaoEntradaMaterial.MateriaPrima),
            Item("20", ClassificacaoEntradaMaterial.Quimico),
            Item("30", ClassificacaoEntradaMaterial.Indefinido)
        ];
        TotaisClassificacaoEntrada t = FiltroItensEntradaMaterial.ContarClassificacoes(itens, ModoEntradaMaterial.Quimico);
        Assert.Equal(3, t.Total);
        Assert.Equal(1, t.MateriaPrima);
        Assert.Equal(1, t.Quimico);
        Assert.Equal(1, t.OutroOuIndefinido);
        Assert.Equal(1, t.DoModo);
    }

    // ---- Ajuste 15: todos indefinidos ----

    [Fact]
    public void TodosIndefinidos_True_QuandoNenhumClassificado()
        => Assert.True(FiltroItensEntradaMaterial.TodosIndefinidos([Item("10", ClassificacaoEntradaMaterial.Indefinido)]));

    [Fact]
    public void TodosIndefinidos_False_QuandoHaClassificado()
        => Assert.False(FiltroItensEntradaMaterial.TodosIndefinidos([Item("10", ClassificacaoEntradaMaterial.MateriaPrima)]));

    // ---- Ajuste 9: mensagens ----

    [Fact]
    public void Mensagem_Quimico_SemItens()
    {
        string m = FiltroItensEntradaMaterial.MontarMensagemSemItensDoModo(ModoEntradaMaterial.Quimico, "4500000001", 3);
        Assert.Contains("Entrada de Químicos", m, StringComparison.Ordinal);
        Assert.Contains("Pedido: 4500000001", m, StringComparison.Ordinal);
        Assert.Contains("Itens encontrados: 3", m, StringComparison.Ordinal);
        Assert.Contains("Itens de químicos encontrados: 0", m, StringComparison.Ordinal);
        Assert.Contains("Use a tela de Entrada de Matéria-Prima", m, StringComparison.Ordinal);
    }

    [Fact]
    public void Mensagem_MateriaPrima_SemItens()
    {
        string m = FiltroItensEntradaMaterial.MontarMensagemSemItensDoModo(ModoEntradaMaterial.MateriaPrima, "4500000002", 2);
        Assert.Contains("Entrada de Matéria-Prima", m, StringComparison.Ordinal);
        Assert.Contains("Itens de matéria-prima encontrados: 0", m, StringComparison.Ordinal);
        Assert.Contains("Use a tela de Entrada de Químicos", m, StringComparison.Ordinal);
    }

    // ---- Ajuste 7/15: serviço A_Product real (com costura) ----

    [Fact]
    public async Task ProductMaster_ComProductType_RetornaMestre()
    {
        ProductMasterSapServico servico = new(
            new ConfiguracaoSap(),
            (_, _) => Task.FromResult<string?>("""{"d":{"Product":"1000046","ProductType":"HIBE","ProductGroup":"Q1","BaseUnit":"KG"}}"""));

        ProdutoSapMestre? mestre = await servico.ObterProdutoAsync("1000046");

        Assert.NotNull(mestre);
        Assert.Equal("HIBE", mestre!.TipoMaterialSap);
        Assert.Equal(ClassificacaoEntradaMaterial.Quimico, ClassificadorItemEntradaMaterial.ClassificarPorProductType(mestre.TipoMaterialSap));
    }

    [Fact]
    public async Task ProductMaster_SemProductType_RetornaNull_ItemFicaIndefinido()
    {
        ProductMasterSapServico servico = new(
            new ConfiguracaoSap(),
            (_, _) => Task.FromResult<string?>("""{"d":{"Product":"1000046"}}"""));

        Assert.Null(await servico.ObterProdutoAsync("1000046"));
    }

    // ---- Ajuste 3/16: parametrização de tela e painel (source-scan) ----

    [Fact]
    public void Tela_AceitaModoNoConstrutorEAplicaHeader()
    {
        string tela = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));
        Assert.Contains("public ProcessoEntradaProdutoForm(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial modo)", tela, StringComparison.Ordinal);
        Assert.Contains("ConfiguracaoTelaEntradaMaterialFactory.Criar(modo)", tela, StringComparison.Ordinal);
        Assert.Contains("headerTitleLabel.Text = _configuracaoTela.TituloTela", tela, StringComparison.Ordinal);
        Assert.Contains("headerSubtitleLabel.Text = _configuracaoTela.SubtituloTela", tela, StringComparison.Ordinal);
        // consulta passa o modo ao controller.
        Assert.Contains("_controller.ConsultarPedidoAsync(numeroPedido, _modoEntrada", tela, StringComparison.Ordinal);
        // bloqueio quando não há itens do modo.
        Assert.Contains("if (!resultado.PedidoTemItensDoModo)", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessoProducao_DeveExibirCardsEntradaMateriaPrimaEQuimicos()
    {
        string designer = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "ProcessoProducaoForm.Designer.cs"));
        string form = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "ProcessoProducaoForm.cs"));

        Assert.Contains("entradaProdutoCard.Location = new Point(28, 70)", designer, StringComparison.Ordinal);
        Assert.Contains("Entrada de\\r\\nMat", designer, StringComparison.Ordinal);
        Assert.Contains("Entrada 101 de mat", designer, StringComparison.Ordinal);
        Assert.Contains("entradaShortcutLabel.Text = \"F1\"", designer, StringComparison.Ordinal);
        Assert.Contains("entradaQuimicosCard.Location = new Point(284, 70)", designer, StringComparison.Ordinal);
        Assert.Contains("Entrada de\\r\\nQu", designer, StringComparison.Ordinal);
        Assert.Contains("Entrada 101 de qu", designer, StringComparison.Ordinal);
        Assert.Contains("entradaQuimicosShortcutLabel.Text = \"F2\"", designer, StringComparison.Ordinal);
        Assert.Contains("processoConsumoMaterialCard.Location = new Point(540, 70)", designer, StringComparison.Ordinal);
        Assert.Contains("pesagemShortcutLabel.Text = \"F3\"", designer, StringComparison.Ordinal);
        Assert.Contains("processoConsumoQuimicosCard.Location = new Point(796, 70)", designer, StringComparison.Ordinal);
        Assert.Contains("quimicosShortcutLabel.Text = \"F4\"", designer, StringComparison.Ordinal);
        Assert.Contains("processoSemiAcabadoCard.Location = new Point(28, 336)", designer, StringComparison.Ordinal);
        Assert.Contains("semiAcabadoShortcutLabel.Text = \"F5\"", designer, StringComparison.Ordinal);
        Assert.Contains("processoProdutoAcabadoCard.Location = new Point(284, 336)", designer, StringComparison.Ordinal);
        Assert.Contains("processShortcutLabel.Text = \"F6\"", designer, StringComparison.Ordinal);
        Assert.Contains("ordensAndamentoCard.Location = new Point(540, 336)", designer, StringComparison.Ordinal);
        Assert.Contains("ordensShortcutLabel.Text = \"F7\"", designer, StringComparison.Ordinal);
        Assert.Contains("EntradaMateriaPrimaRequested", form, StringComparison.Ordinal);
        Assert.Contains("EntradaQuimicosRequested", form, StringComparison.Ordinal);
        Assert.Contains("OnEntradaMateriaPrimaClick", form, StringComparison.Ordinal);
        Assert.Contains("OnEntradaQuimicosClick", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Painel_AbreEntradaPorModo()
    {
        string painel = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "PainelInicialForm.cs"));
        Assert.Contains("OpenProcessoEntradaProdutoAsync(", painel, StringComparison.Ordinal);
        Assert.Contains("ProcessoEntradaProdutoForm form = new(modo)", painel, StringComparison.Ordinal);
        Assert.Contains("ModoEntradaMaterial modo", painel, StringComparison.Ordinal);
        Assert.Contains("EntradaMateriaPrimaRequested += async (_, _) => await OpenProcessoEntradaProdutoAsync(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.MateriaPrima)", painel, StringComparison.Ordinal);
        Assert.Contains("EntradaQuimicosRequested += async (_, _) => await OpenProcessoEntradaProdutoAsync(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.Quimico)", painel, StringComparison.Ordinal);
        AssertAtalhoProcesso(painel, "F1", "OpenProcessoEntradaProdutoAsync()");
        AssertAtalhoProcesso(painel, "F2", "OpenProcessoEntradaProdutoAsync(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.Quimico)");
        AssertAtalhoProcesso(painel, "F3", "OpenProcessoConsumoMaterialAsync()");
        AssertAtalhoProcesso(painel, "F4", "OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico)");
        AssertAtalhoProcesso(painel, "F5", "OpenProcessoSemiAcabadoAsync()");
        AssertAtalhoProcesso(painel, "F6", "OpenProcessoProdutoAcabadoAsync()");
        AssertAtalhoProcesso(painel, "F7", "OpenConsultaOrdemProducaoAsync()");
    }

    // ---- Ajuste 6/9/12/14: controller classifica/filtra/bloqueia sem quebrar 23.1/payload ----

    [Fact]
    public void Controller_ClassificaFiltraEBloqueiaPorModo()
    {
        string controller = File.ReadAllText(Path.Combine(RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));
        Assert.Contains("EnriquecerEClassificarItensAsync(", controller, StringComparison.Ordinal);
        Assert.Contains("FiltroItensEntradaMaterial.FiltrarItensPorModo(", controller, StringComparison.Ordinal);
        Assert.Contains("ProductMasterServico.ObterProdutoAsync(", controller, StringComparison.Ordinal);
        Assert.Contains("ClassificadorItemEntradaMaterial.ClassificarPorProductType(", controller, StringComparison.Ordinal);
        Assert.Contains("PedidoTemItensDoModo = false", controller, StringComparison.Ordinal);
        // 23.1 e payload preservados.
        Assert.Contains("ValidadorLiberacaoPedidoCompra.Validar(cabecalhoSap)", controller, StringComparison.Ordinal);
        Assert.Contains("QuantityInEntryUnit = FormatarQuantidade(item.PesoLiquidoKg)", controller, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
        Assert.Contains("EntryUnit = \"KG\"", controller, StringComparison.Ordinal);
    }

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }

    private static void AssertAtalhoProcesso(string painel, string tecla, string chamadaEsperada)
    {
        string marcador = $"if (e.KeyCode == Keys.{tecla} && _currentContentView == _processoProducaoForm)";
        int inicio = painel.IndexOf(marcador, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Atalho {tecla} não encontrado no painel de processo.");

        int proximo = painel.IndexOf("if (e.KeyCode == Keys.", inicio + marcador.Length, StringComparison.Ordinal);
        string bloco = proximo >= 0
            ? painel[inicio..proximo]
            : painel[inicio..];

        Assert.Contains(chamadaEsperada, bloco, StringComparison.Ordinal);
    }
}
