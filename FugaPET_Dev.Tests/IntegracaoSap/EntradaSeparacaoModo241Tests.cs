using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Seguranca;

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

    // ================= GATE 097A — Recebimento de Mercadoria (ROH/HIBE/VERP/HALB) =================

    // T01-T06/T08: aceita somente os quatro tipos homologados; demais permanecem fail-closed.
    [Theory]
    [InlineData(ClassificacaoEntradaMaterial.MateriaPrima, true)]   // ROH
    [InlineData(ClassificacaoEntradaMaterial.Quimico, true)]        // HIBE
    [InlineData(ClassificacaoEntradaMaterial.Embalagem, true)]      // VERP
    [InlineData(ClassificacaoEntradaMaterial.ProdutoAcabado, false)] // FERT
    [InlineData(ClassificacaoEntradaMaterial.Semiacabado, true)]    // HALB
    [InlineData(ClassificacaoEntradaMaterial.Outro, false)]         // Outro
    [InlineData(ClassificacaoEntradaMaterial.Indefinido, false)]    // fail-closed
    public void Recebimento_AceitaTiposHomologados_ExcluiOResto(ClassificacaoEntradaMaterial classe, bool esperado)
        => Assert.Equal(esperado,
            ClassificadorItemEntradaMaterial.ItemPertenceAoModo(classe, ModoEntradaMaterial.RecebimentoMercadoria));

    // T09: cenário de quatro itens elegíveis, incluindo VERP/HALB, não resulta em zero mercadorias.
    [Fact]
    public void Recebimento_PedidoComQuatroTiposHomologados_RetornaTodos()
    {
        List<PedidoCompraSapItem> itens =
        [
            Item("10", ClassificacaoEntradaMaterial.MateriaPrima),
            Item("20", ClassificacaoEntradaMaterial.Quimico),
            Item("30", ClassificacaoEntradaMaterial.Embalagem),
            Item("40", ClassificacaoEntradaMaterial.Semiacabado)
        ];
        IReadOnlyList<PedidoCompraSapItem> filtrados =
            FiltroItensEntradaMaterial.FiltrarItensPorModo(itens, ModoEntradaMaterial.RecebimentoMercadoria);
        Assert.Equal(4, filtrados.Count);
        Assert.Contains(filtrados, i => i.NumeroItem == "10");
        Assert.Contains(filtrados, i => i.NumeroItem == "20");
        Assert.Contains(filtrados, i => i.NumeroItem == "30");
        Assert.Contains(filtrados, i => i.NumeroItem == "40");
    }

    // TEST_08 (cascata): pedido só com Indefinidos permanece fail-closed também no modo unificado.
    [Fact]
    public void Recebimento_TodosIndefinidos_ContinuaFailClosed()
        => Assert.True(FiltroItensEntradaMaterial.TodosIndefinidos([Item("10", ClassificacaoEntradaMaterial.Indefinido)]));

    // TEST_09/10: nomenclatura visível da porta única (título, subtítulo, módulo, placeholder e lote).
    [Fact]
    public void Config_RecebimentoMercadoria_TextosEsperados()
    {
        ConfiguracaoTelaEntradaMaterial c =
            ConfiguracaoTelaEntradaMaterialFactory.Criar(ModoEntradaMaterial.RecebimentoMercadoria);
        Assert.Equal("Recebimento de Mercadoria", c.TituloTela);
        Assert.Equal("Recebimento de Mercadoria", c.NomeModulo);
        Assert.Equal("Pesagem e recebimento de mercadorias por pedido de compra / SAP", c.SubtituloTela);
        Assert.Equal("Pesquisar itens do pedido...", c.PlaceholderPesquisa);
        Assert.Equal("Informe o lote da mercadoria antes da pesagem.", c.MensagemLote);
        // §10: reutiliza infraestrutura de balança existente (não cria tipo físico novo).
        Assert.Equal("ENTRADA_MATERIA_PRIMA", c.TipoBalancaPreferencial);
    }

    // TEST_10 (mensagem sem itens): não referencia as telas separadas ("Use a tela de ...").
    [Fact]
    public void Recebimento_MensagemSemItens_SemReferenciaTelasSeparadas()
    {
        string m = FiltroItensEntradaMaterial.MontarMensagemSemItensDoModo(
            ModoEntradaMaterial.RecebimentoMercadoria, "4500000005", 3);
        Assert.Contains("mercadoria", m, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROH, HIBE, VERP ou HALB", m, StringComparison.Ordinal);
        Assert.DoesNotContain("Use a tela de", m, StringComparison.Ordinal);
    }

    // TEST_16/§9: nenhuma permissão nova (RECEBIMENTO_MERCADORIA / ENTRADA_QUIMICOS); rota pública usa ENTRADA_PRODUTO;
    // permissões críticas preservadas.
    [Fact]
    public void Permissao_ContinuaEntradaProduto_SemNovasAcoes()
    {
        string permissoes = File.ReadAllText(Path.Combine(RaizProjeto(), "Servicos", "Seguranca", "PermissoesSistema.cs"));
        Assert.DoesNotContain("RECEBIMENTO_MERCADORIA", permissoes, StringComparison.Ordinal);
        Assert.DoesNotContain("ENTRADA_QUIMICOS", permissoes, StringComparison.Ordinal);
        Assert.Equal("EXCLUIR_PESAGEM", PermissoesSistema.Acoes.ExcluirPesagem);

        string painel = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "PainelInicialForm.cs"));
        Assert.Contains("PermissoesSistema.Rotinas.EntradaProduto", painel, StringComparison.Ordinal);
    }

    // TEST_17/18/19: SAP 101 e persistência inalterados (modo é só classificação); Excluir Pesagem intacto.
    [Fact]
    public void ContratoSap101_Persistencia_ExcluirPesagem_Inalterados()
    {
        string controller = File.ReadAllText(Path.Combine(RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));
        // TEST_17: payload 101 preservado. GATE R2: EntryUnit passou a vir da alocação; o literal "KG"
        // migrou para EntradaProdutoAlocacao101.UnidadeSuportada, única unidade admitida no incremento.
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
        Assert.Contains("EntryUnit = alocacao.UnidadePadrao", controller, StringComparison.Ordinal);
        string alocacao = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Modelo", "Entrada", "EntradaProdutoAlocacao101.cs"));
        Assert.Contains("UnidadeSuportada = \"KG\"", alocacao, StringComparison.Ordinal);
        // TEST_18: consulta compartilhada parametrizada por modo (mesma Form/controller/persistência).
        Assert.Contains("EnriquecerEClassificarItensAsync(", controller, StringComparison.Ordinal);
        // TEST_19: safe-delete de pesagem preservado (arquivo congelado do gate 067).
        string repoExclusao = File.ReadAllText(Path.Combine(RaizProjeto(), "AcessoDados", "Repositorio", "ExclusaoPesagemRepositorio.cs"));
        Assert.Contains("fn_entrada_produto_sap_guard_counts", repoExclusao, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", repoExclusao, StringComparison.Ordinal);
        Assert.Contains("status_pesagem = 'CANCELADA'", repoExclusao, StringComparison.Ordinal);
    }

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

    // GATE 073/077: porta única em F1; card de entrada de Químicos oculto; módulos restantes compactados.
    [Fact]
    public void ProcessoProducao_PrimeiroCardEhRecebimentoMercadoria_QuimicosOculto()
    {
        string designer = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "ProcessoProducaoForm.Designer.cs"));
        string form = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "ProcessoProducaoForm.cs"));

        // TEST_11: F1 representa Recebimento de Mercadoria (título + descrição genérica).
        Assert.Contains("entradaProdutoCard.Location = new Point(28, 70)", designer, StringComparison.Ordinal);
        Assert.Contains("Recebimento de\\r\\nMercadoria", designer, StringComparison.Ordinal);
        Assert.Contains("Pesagem e recebimento de mercadorias", designer, StringComparison.Ordinal);
        Assert.Contains("entradaShortcutLabel.Text = \"F1\"", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Entrada de\\r\\nMat", designer, StringComparison.Ordinal);

        // TEST_13: card de Químicos oculto (não navegável).
        Assert.Contains("entradaQuimicosCard.Visible = false", designer, StringComparison.Ordinal);

        Assert.Contains("pesagemShortcutLabel.Text = \"F2\"", designer, StringComparison.Ordinal);
        Assert.Contains("quimicosShortcutLabel.Text = \"F3\"", designer, StringComparison.Ordinal);
        Assert.Contains("semiAcabadoShortcutLabel.Text = \"F4\"", designer, StringComparison.Ordinal);
        Assert.Contains("processShortcutLabel.Text = \"F5\"", designer, StringComparison.Ordinal);
        Assert.Contains("ordensShortcutLabel.Text = \"F6\"", designer, StringComparison.Ordinal);
        Assert.Contains("apontamentosShortcutLabel.Text = \"F7\"", designer, StringComparison.Ordinal);

        Assert.Contains("EntradaMateriaPrimaRequested", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Painel_PortaUnica_AbreRecebimentoMercadoria_SemRotaQuimicos()
    {
        string painel = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", "PainelInicialForm.cs"));
        Assert.Contains("ProcessoEntradaProdutoForm form = new(modo)", painel, StringComparison.Ordinal);

        // TEST_12: a entrada pública (card F1) abre RecebimentoMercadoria.
        Assert.Contains("EntradaMateriaPrimaRequested += async (_, _) => await OpenProcessoEntradaProdutoAsync(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.RecebimentoMercadoria)", painel, StringComparison.Ordinal);

        // TEST_13/14: nenhuma rota pública para Entrada de Químicos.
        Assert.DoesNotContain("EntradaQuimicosRequested += async", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenProcessoEntradaProdutoAsync(global::FugaPET_Dev.Modelo.Processo.ModoEntradaMaterial.Quimico)", painel, StringComparison.Ordinal);

        // TEST_12: F1 abre a porta única (default = RecebimentoMercadoria).
        AssertAtalhoProcesso(painel, "F1", "OpenProcessoEntradaProdutoAsync()");
        AssertAtalhoProcesso(painel, "F2", "OpenProcessoConsumoMaterialAsync()");
        AssertAtalhoProcesso(painel, "F3", "OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico)");
        AssertAtalhoProcesso(painel, "F4", "OpenProcessoSemiAcabadoAsync()");
        AssertAtalhoProcesso(painel, "F5", "OpenProcessoProdutoAcabadoAsync()");
        AssertAtalhoProcesso(painel, "F6", "OpenConsultaOrdemProducaoAsync()");
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
        // GATE R2: o campo padrão passou a vir da alocação (quantidade do Pedido de Compra no caminho ROH);
        // EntryUnit também vem da alocação, validada contra KG. GoodsMovementRefDocType segue literal.
        Assert.Contains("QuantityInEntryUnit = alocacao.QuantidadePadrao", controller, StringComparison.Ordinal);
        Assert.Contains("EntryUnit = alocacao.UnidadePadrao", controller, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
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
