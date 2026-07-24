using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// Separação de componentes por PROCESSO: a MESMA OP pode abrir nas duas telas (Matéria-Prima e Químicos),
/// cada uma exibindo somente os componentes classificados para ela. A classificação vem do ProductType do
/// Product Master (A_Product) POR COMPONENTE — nunca do produto produzido da OP.
///
/// Os códigos usados aqui são DADOS DE TESTE (fakes), não regra de produção: nenhum código é hardcoded no
/// código produtivo (ver <see cref="CodigoProdutivo_NaoDeveHardcodearMateriaisDeTeste"/>).
/// </summary>
public sealed class ConsumoSeparacaoComponentesTests
{
    // Dados do cenário funcional informado para o teste DEV.
    private const string OpTeste = "1001327";
    private const string ProdutoProduzidoTeste = "2000091";
    private const string ComponenteMateriaPrima = "3500027"; // esperado ROH
    private const string ComponenteQuimico = "1000089";      // esperado HIBE

    // ---------- Cenário principal: mesma OP, componentes separados por tela ----------

    [Fact]
    public async Task MesmaOp_SeparaComponentesPorTela()
    {
        IReadOnlyList<ComponenteConsumoMaterial> componentes = await ClassificarComponentesDaOpAsync();

        IReadOnlyList<ComponenteConsumoMaterial> materiaPrima = FiltrarPorModo(componentes, ModoConsumoMaterial.MateriaPrima);
        IReadOnlyList<ComponenteConsumoMaterial> quimicos = FiltrarPorModo(componentes, ModoConsumoMaterial.Quimico);

        // Tela Matéria-Prima: aceita a OP e mostra SOMENTE o ROH.
        Assert.NotEmpty(materiaPrima);
        Assert.Equal(ComponenteMateriaPrima, Assert.Single(materiaPrima).CodigoMaterial);

        // Tela Químicos: aceita a MESMA OP e mostra SOMENTE o HIBE.
        Assert.NotEmpty(quimicos);
        Assert.Equal(ComponenteQuimico, Assert.Single(quimicos).CodigoMaterial);
    }

    [Fact]
    public async Task MesmaOp_EhAceitaNasDuasTelas()
    {
        IReadOnlyList<ComponenteConsumoMaterial> componentes = await ClassificarComponentesDaOpAsync();

        // "OP aceita no modo" == existe pelo menos um componente daquele modo (é o gate real da tela).
        Assert.NotEmpty(FiltrarPorModo(componentes, ModoConsumoMaterial.MateriaPrima));
        Assert.NotEmpty(FiltrarPorModo(componentes, ModoConsumoMaterial.Quimico));
    }

    [Fact]
    public async Task ProdutoProduzido_NaoBloqueiaNenhumaTela()
    {
        // O produto produzido (FERT) é apenas contexto de cabeçalho: não entra na lista de componentes
        // e não determina a aceitação da OP em nenhuma das telas.
        IReadOnlyList<ComponenteConsumoMaterial> componentes = await ClassificarComponentesDaOpAsync();

        Assert.DoesNotContain(componentes, c => c.CodigoMaterial == ProdutoProduzidoTeste);
        Assert.NotEmpty(FiltrarPorModo(componentes, ModoConsumoMaterial.MateriaPrima));
        Assert.NotEmpty(FiltrarPorModo(componentes, ModoConsumoMaterial.Quimico));
    }

    // ---------- Regra por ProductType ----------

    [Theory]
    [InlineData("ROH", true, false)]    // Matéria-prima → só na tela de Matéria-Prima
    [InlineData("HIBE", false, true)]   // Químico → só na tela de Químicos
    [InlineData("VERP", false, false)]  // Embalagem → em nenhuma das duas
    [InlineData("FERT", false, false)]  // Outro → em nenhuma das duas
    [InlineData("", false, false)]      // Indefinido (sem ProductType) → em nenhuma das duas
    public void ProductType_DefineEmQualTelaOComponenteAparece(
        string productType, bool esperadoMateriaPrima, bool esperadoQuimico)
    {
        ComponenteConsumoMaterial componente = ComponenteClassificado(productType);

        Assert.Equal(
            esperadoMateriaPrima,
            ProcessoConsumoMaterialForm.ComponentePertenceAoModo(componente, ModoConsumoMaterial.MateriaPrima));
        Assert.Equal(
            esperadoQuimico,
            ProcessoConsumoMaterialForm.ComponentePertenceAoModo(componente, ModoConsumoMaterial.Quimico));
    }

    [Fact]
    public void Indefinido_NaoApareceAutomaticamenteEmMateriaPrima()
    {
        // Regressão do bug: sem ProductType, o componente NÃO pode cair por padrão em Matéria-Prima.
        ComponenteConsumoMaterial semTipo = ComponenteClassificado(productType: "");

        Assert.Equal(ClassificacaoConsumoMaterial.Indefinido, semTipo.ClassificacaoConsumo);
        Assert.False(ProcessoConsumoMaterialForm.ComponentePertenceAoModo(semTipo, ModoConsumoMaterial.MateriaPrima));
        Assert.False(ProcessoConsumoMaterialForm.ComponentePertenceAoModo(semTipo, ModoConsumoMaterial.Quimico));
    }

    // ---------- Product Master: técnico + descrição ----------

    [Fact]
    public async Task ProductMaster_FornecemTipoGrupoUnidade_EDescricaoVemDaProductDescription()
    {
        ConsumoMaterialServico servico = CriarServico();

        IReadOnlyDictionary<string, ProdutoSapMestre> mapa =
            await servico.ObterMestresComponentesAsync([ComponenteMateriaPrima, ComponenteQuimico]);

        ProdutoSapMestre roh = mapa[ComponenteMateriaPrima];
        Assert.True(roh.Consultado);                       // A_Product respondeu
        Assert.Equal("ROH", roh.TipoMaterialSap);          // A_Product: ProductType
        Assert.Equal("GRP-MP", roh.GrupoMaterialSap);      // A_Product: ProductGroup
        Assert.Equal("KG", roh.UnidadeBaseSap);            // A_Product: BaseUnit
        Assert.Equal("MATERIA PRIMA TESTE", roh.DescricaoProdutoSap); // A_ProductDescription
        Assert.Equal("PT", roh.IdiomaDescricaoSap);

        ProdutoSapMestre hibe = mapa[ComponenteQuimico];
        Assert.True(hibe.Consultado);
        Assert.Equal("HIBE", hibe.TipoMaterialSap);
        Assert.Equal("QUIMICO TESTE", hibe.DescricaoProdutoSap);
    }

    [Fact]
    public async Task ProductMasterIndisponivel_BloqueiaClassificacaoPorSeguranca()
    {
        // A_Product não retorna nada → Consultado=false → Indefinido → componente fora das duas telas.
        ConsumoMaterialServico servico = CriarServico(productMaster: new ProductMasterFake(devolverNulo: true));

        IReadOnlyDictionary<string, ProdutoSapMestre> mapa =
            await servico.ObterMestresComponentesAsync([ComponenteMateriaPrima, ComponenteQuimico]);

        Assert.False(mapa[ComponenteMateriaPrima].Consultado);
        // A descrição continua chegando mesmo sem o técnico — mas não libera classificação.
        Assert.Equal("MATERIA PRIMA TESTE", mapa[ComponenteMateriaPrima].DescricaoProdutoSap);

        List<ComponenteConsumoMaterial> componentes = ComponentesDaOp();
        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            componentes, codigo => mapa.GetValueOrDefault(codigo.Trim()));

        Assert.All(componentes, c => Assert.Equal(ClassificacaoConsumoMaterial.Indefinido, c.ClassificacaoConsumo));
        Assert.Empty(FiltrarPorModo(componentes, ModoConsumoMaterial.MateriaPrima));
        Assert.Empty(FiltrarPorModo(componentes, ModoConsumoMaterial.Quimico));
    }

    [Fact]
    public void Combinar_DescricaoNaoSobrepoeMestreTecnico()
    {
        ProdutoSapMestre tecnico = new()
        {
            CodigoProduto = "0001234",
            TipoMaterialSap = "ROH",
            GrupoMaterialSap = "G",
            UnidadeBaseSap = "KG",
            Consultado = true
        };
        ProdutoSapMestre somenteDescricao = new()
        {
            CodigoProduto = "0001234",
            DescricaoProdutoSap = "DESCR",
            IdiomaDescricaoSap = "PT",
            Consultado = false
        };

        ProdutoSapMestre combinado = ProdutoSapMestre.Combinar("0001234", tecnico, somenteDescricao);

        Assert.True(combinado.Consultado);
        Assert.Equal("ROH", combinado.TipoMaterialSap);
        Assert.Equal("DESCR", combinado.DescricaoProdutoSap);

        // Só descrição (sem técnico) NUNCA marca Consultado — classificação continua bloqueada.
        ProdutoSapMestre semTecnico = ProdutoSapMestre.Combinar("0001234", null, somenteDescricao);
        Assert.False(semTecnico.Consultado);
        Assert.Equal(string.Empty, semTecnico.TipoMaterialSap);
    }

    [Fact]
    public async Task CodigoMaterial_PreservaZerosAEsquerdaSemConverterParaNumero()
    {
        const string codigoComZeros = "0001234";
        ConsumoMaterialServico servico = CriarServico(
            productMaster: new ProductMasterFake(tipoPorCodigo: new() { [codigoComZeros] = "ROH" }));

        IReadOnlyDictionary<string, ProdutoSapMestre> mapa =
            await servico.ObterMestresComponentesAsync([codigoComZeros]);

        Assert.True(mapa.ContainsKey(codigoComZeros));
        Assert.Equal(codigoComZeros, mapa[codigoComZeros].CodigoProduto);
    }

    // ---------- Ordem operacional contém somente os componentes do modo ----------

    [Fact]
    public async Task OrdemOperacional_ContemSomenteComponentesDoModo()
    {
        IReadOnlyList<ComponenteConsumoMaterial> componentes = await ClassificarComponentesDaOpAsync();
        OrdemProducaoConsumo ordemCompleta = new()
        {
            NumeroOrdem = OpTeste,
            MaterialProduzido = ProdutoProduzidoTeste,
            Planta = "3007",
            QuantidadePrevista = 10m,
            Unidade = "KG",
            LoteProdutoProduzido = "L1",
            DataOrdem = new DateTime(2026, 7, 16),
            Liberada = true,
            Componentes = componentes
        };

        IReadOnlyList<ComponenteConsumoMaterial> quimicos = FiltrarPorModo(componentes, ModoConsumoMaterial.Quimico);
        OrdemProducaoConsumo ordemModo = ProcessoConsumoMaterialForm.CopiarOrdemComComponentes(ordemCompleta, quimicos);

        // Cabeçalho preservado.
        Assert.Equal(OpTeste, ordemModo.NumeroOrdem);
        Assert.Equal(ProdutoProduzidoTeste, ordemModo.MaterialProduzido);
        Assert.Equal("3007", ordemModo.Planta);
        Assert.Equal(10m, ordemModo.QuantidadePrevista);
        Assert.Equal("KG", ordemModo.Unidade);
        Assert.Equal("L1", ordemModo.LoteProdutoProduzido);
        Assert.Equal(new DateTime(2026, 7, 16), ordemModo.DataOrdem);
        Assert.True(ordemModo.Liberada);

        // Componentes: SOMENTE os do modo (a tela de Químicos nunca salva/envia o ROH).
        Assert.Equal(ComponenteQuimico, Assert.Single(ordemModo.Componentes).CodigoMaterial);
        Assert.DoesNotContain(ordemModo.Componentes, c => c.CodigoMaterial == ComponenteMateriaPrima);

        // A ordem original (cache SAP) NÃO é alterada de forma destrutiva.
        Assert.Equal(2, ordemCompleta.Componentes.Count);
    }

    [Fact]
    public async Task OrdemOperacionalDeMateriaPrima_NaoContemComponenteQuimico()
    {
        IReadOnlyList<ComponenteConsumoMaterial> componentes = await ClassificarComponentesDaOpAsync();
        OrdemProducaoConsumo ordemCompleta = new() { NumeroOrdem = OpTeste, Componentes = componentes };

        OrdemProducaoConsumo ordemMp = ProcessoConsumoMaterialForm.CopiarOrdemComComponentes(
            ordemCompleta, FiltrarPorModo(componentes, ModoConsumoMaterial.MateriaPrima));

        Assert.Equal(ComponenteMateriaPrima, Assert.Single(ordemMp.Componentes).CodigoMaterial);
        Assert.DoesNotContain(ordemMp.Componentes, c => c.CodigoMaterial == ComponenteQuimico);
    }

    // ---------- Fiação: a tela usa o mestre técnico e registra recente por componentes do modo ----------

    [Fact]
    public void Tela_UsaMestreCompletoEFiltroEstrito()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Product Master COMPLETO (A_Product + A_ProductDescription), não só descrição.
        Assert.Contains("_mestresProdutoPorCodigo = await _controller.ObterMestresComponentesAsync(", form, StringComparison.Ordinal);
        Assert.Contains("BuscarMestreMaterialSap", form, StringComparison.Ordinal);
        Assert.DoesNotContain("_descricoesProdutoPorCodigo", form, StringComparison.Ordinal);

        // Filtro ESTRITO: Indefinido não é mais aceito em Matéria-Prima.
        Assert.Contains(
            "componente.ClassificacaoConsumo == ClassificacaoConsumoMaterial.MateriaPrima",
            form,
            StringComparison.Ordinal);
        Assert.DoesNotContain("or ClassificacaoConsumoMaterial.Indefinido", form, StringComparison.Ordinal);

        // A ordem operacional recebe apenas os componentes do modo.
        Assert.Contains("_ordemConsumoAtual = CopiarOrdemComComponentes(ordem, componentesModo);", form, StringComparison.Ordinal);
    }

    [Fact]
    public void OpRecente_RegistradaPorComponentesDoModo_NaoPeloMaterialProduzido()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string recente = ExtrairMetodo(form, "private void RegistrarOrdemRecenteSePermitida");

        Assert.Contains("componentesModo.Count == 0", recente, StringComparison.Ordinal);
        Assert.DoesNotContain("OrdemPertenceAoModoAtual", recente, StringComparison.Ordinal);
        Assert.DoesNotContain("MaterialProduzido", recente, StringComparison.Ordinal);
    }

    [Fact]
    public void CodigoProdutivo_NaoDeveHardcodearMateriaisDeTeste()
    {
        // Os códigos do cenário são dados de teste; a regra de produção é por ProductType/ProductGroup.
        foreach (string[] arquivo in new[]
                 {
                     new[] { "Tela", "Processo", "ProcessoConsumoMaterialForm.cs" },
                     new[] { "Controle", "Processo", "ProcessoConsumoMaterialController.cs" },
                     new[] { "Servicos", "Operacao", "ConsumoMaterialServico.cs" },
                     new[] { "Modelo", "Processo", "ClassificacaoConsumoMaterial.cs" },
                     new[] { "Modelo", "IntegracaoSap", "ProdutoSapMestre.cs" },
                 })
        {
            string fonte = LerArquivoProjeto(arquivo);
            foreach (string codigo in new[] { ComponenteMateriaPrima, ComponenteQuimico, ProdutoProduzidoTeste, OpTeste })
            {
                Assert.DoesNotContain(codigo, fonte, StringComparison.Ordinal);
            }
        }
    }

    // ---------- Apoio ----------

    // Pipeline REAL: mestre (A_Product + A_ProductDescription) → enriquecimento → classificação.
    private static async Task<IReadOnlyList<ComponenteConsumoMaterial>> ClassificarComponentesDaOpAsync()
    {
        ConsumoMaterialServico servico = CriarServico();
        List<ComponenteConsumoMaterial> componentes = ComponentesDaOp();

        IReadOnlyDictionary<string, ProdutoSapMestre> mapa = await servico.ObterMestresComponentesAsync(
            componentes.Select(c => c.CodigoMaterial));

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial(
            componentes,
            codigo => mapa.GetValueOrDefault(codigo.Trim()),
            modoDaTela: "Teste",
            opNumero: OpTeste,
            produtoProduzido: ProdutoProduzidoTeste);

        return componentes;
    }

    private static List<ComponenteConsumoMaterial> ComponentesDaOp()
        =>
        [
            new ComponenteConsumoMaterial { CodigoMaterial = ComponenteMateriaPrima },
            new ComponenteConsumoMaterial { CodigoMaterial = ComponenteQuimico }
        ];

    private static IReadOnlyList<ComponenteConsumoMaterial> FiltrarPorModo(
        IEnumerable<ComponenteConsumoMaterial> componentes, ModoConsumoMaterial modo)
        => componentes
            .Where(c => ProcessoConsumoMaterialForm.ComponentePertenceAoModo(c, modo))
            .ToList();

    private static ComponenteConsumoMaterial ComponenteClassificado(string productType)
    {
        ComponenteConsumoMaterial componente = new() { CodigoMaterial = "QUALQUER" };
        ProdutoSapMestre mestre = new()
        {
            CodigoProduto = "QUALQUER",
            TipoMaterialSap = productType,
            Consultado = !string.IsNullOrWhiteSpace(productType)
        };

        ConsumoMaterialServico.EnriquecerComponentesComTipoMaterial([componente], _ => mestre);
        return componente;
    }

    private static ConsumoMaterialServico CriarServico(ProductMasterFake? productMaster = null)
        => new(
            new FakeProdOrder(),
            () => throw new InvalidOperationException("repositório não deve ser usado"),
            () => throw new InvalidOperationException("SAP 261 não deve ser usado"),
            () => throw new InvalidOperationException("confirmação não deve ser usada"),
            () => new DescricaoFake(),
            () => productMaster ?? new ProductMasterFake());

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximoMetodo < 0)
        {
            proximoMetodo = fonte.Length;
        }

        return fonte[inicio..proximoMetodo];
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

    private sealed class FakeProdOrder : IProductionOrderSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoEncontrada());
    }

    /// <summary>A_Product simulado: devolve ProductType/ProductGroup/BaseUnit por código (Consultado=true).</summary>
    private sealed class ProductMasterFake : IProductMasterSapServico
    {
        private readonly bool _devolverNulo;
        private readonly Dictionary<string, string> _tipoPorCodigo;

        public ProductMasterFake(bool devolverNulo = false, Dictionary<string, string>? tipoPorCodigo = null)
        {
            _devolverNulo = devolverNulo;
            _tipoPorCodigo = tipoPorCodigo ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ComponenteMateriaPrima] = "ROH",
                [ComponenteQuimico] = "HIBE"
            };
        }

        public bool EhSimulado => true;
        public bool Configurado => !_devolverNulo;

        public Task<ProdutoSapMestre?> ObterProdutoAsync(string codigoProduto, CancellationToken cancellationToken = default)
        {
            if (_devolverNulo)
            {
                return Task.FromResult<ProdutoSapMestre?>(null);
            }

            string codigo = codigoProduto.Trim();
            if (!_tipoPorCodigo.TryGetValue(codigo, out string? tipo))
            {
                return Task.FromResult<ProdutoSapMestre?>(null);
            }

            return Task.FromResult<ProdutoSapMestre?>(new ProdutoSapMestre
            {
                CodigoProduto = codigo,
                TipoMaterialSap = tipo,
                GrupoMaterialSap = tipo == "ROH" ? "GRP-MP" : "GRP-QUI",
                UnidadeBaseSap = "KG",
                Consultado = true
            });
        }
    }

    /// <summary>A_ProductDescription simulado: devolve SOMENTE descrição (Consultado=false).</summary>
    private sealed class DescricaoFake : IProductDescriptionSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;

        public Task<ProdutoSapMestre?> ObterDescricaoAsync(string codigoProduto, CancellationToken cancellationToken = default)
        {
            string codigo = codigoProduto.Trim();
            string? descricao = codigo switch
            {
                ComponenteMateriaPrima => "MATERIA PRIMA TESTE",
                ComponenteQuimico => "QUIMICO TESTE",
                _ => null
            };

            return Task.FromResult<ProdutoSapMestre?>(descricao is null
                ? null
                : new ProdutoSapMestre
                {
                    CodigoProduto = codigo,
                    DescricaoProdutoSap = descricao,
                    IdiomaDescricaoSap = "PT",
                    Consultado = false
                });
        }
    }
}
