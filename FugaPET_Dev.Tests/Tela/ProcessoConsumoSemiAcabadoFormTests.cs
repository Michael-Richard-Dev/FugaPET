using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoConsumoSemiAcabadoFormTests
{
    [Fact]
    public void TipoProcesso_DeveDeclararConsumoSemiAcabado()
        => Assert.Equal("CONSUMO_SEMI_ACABADO", TipoProcessoOperacao.ConsumoSemiAcabado);

    [Fact]
    public void ControleApontamentos_DeveResolverConsumoSemiAcabadoParaNovaTela()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string abertura = ExtrairMetodo(form, "private async Task AbrirTelaDestinoAsync");

        Assert.Contains("TipoProcessoOperacao.ConsumoSemiAcabado => AbrirConsumoSemiAcabado(contexto)", abertura, StringComparison.Ordinal);
        Assert.Contains("using ProcessoConsumoSemiAcabadoForm form = new(contexto);", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ControleApontamentos_MateriaPrimaEQuimicosContinuamNaTelaExistente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string abertura = ExtrairMetodo(form, "private async Task AbrirTelaDestinoAsync");

        Assert.Contains("TipoProcessoOperacao.ConsumoMateriaPrima => AbrirConsumo(contexto, ModoConsumoMaterial.MateriaPrima)", abertura, StringComparison.Ordinal);
        Assert.Contains("TipoProcessoOperacao.ConsumoQuimicos => AbrirConsumo(contexto, ModoConsumoMaterial.Quimico)", abertura, StringComparison.Ordinal);
    }

    [Fact]
    public void OperacaoNaoConfigurada_ContinuaBloqueadaPorDestinoNaoConectado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string abertura = ExtrairMetodo(form, "private async Task AbrirTelaDestinoAsync");

        Assert.Contains("_ => AvisarDestinoNaoConectado(contexto)", abertura, StringComparison.Ordinal);
    }

    [Fact]
    public void CodigoProdutivo_NaoHardcodaOperacao0140()
    {
        string[] arquivosProdutivos =
        [
            LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs"),
            LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoSemiAcabadoForm.cs"),
            LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs"),
            LerArquivoProjeto("Modelo", "Processo", "ConfiguracaoOperacaoProcesso.cs")
        ];

        foreach (string arquivo in arquivosProdutivos)
        {
            Assert.DoesNotContain("0140", arquivo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Contexto0140_MostraSomenteComponentesDa0140()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("SEMI", "0140", "000000"),
            Componente("MP", "0050", "000000"),
            Componente("QUIM", "0060", "000000")
        ];

        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000");

        Assert.Equal("SEMI", Assert.Single(filtrados).CodigoMaterial);
    }

    [Fact]
    public void Contexto0140_ExcluiComponentes0050E0060()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("MP", "0050", "000000"),
            Componente("QUIM", "0060", "000000")
        ];

        Assert.Empty(ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000"));
    }

    [Fact]
    public void Contexto0140_ExcluiComponenteSemOperacao()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("SEM_OP", "", ""),
            Componente("ESPACO", "   ", "")
        ];

        Assert.Empty(ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000"));
    }

    [Fact]
    public void Contexto0140_ToleraZerosAEsquerda()
    {
        List<ComponenteConsumoMaterial> componentes = [Componente("SEMI", "140", "0")];

        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000");

        Assert.Equal("SEMI", Assert.Single(filtrados).CodigoMaterial);
    }

    [Fact]
    public void Contexto0140_BloqueiaSequenciaDivergente()
    {
        List<ComponenteConsumoMaterial> componentes = [Componente("SEMI", "0140", "000001")];

        Assert.Empty(ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000"));
    }

    [Fact]
    public void Contexto0140_BloqueiaSequenciaParcial()
    {
        List<ComponenteConsumoMaterial> componenteSemSequencia = [Componente("SEMI", "0140", "")];
        List<ComponenteConsumoMaterial> contextoSemSequencia = [Componente("SEMI", "0140", "000000")];

        Assert.Empty(ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componenteSemSequencia, "0140", "000000"));
        Assert.Empty(ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(contextoSemSequencia, "0140", ""));
    }

    [Fact]
    public async Task Contexto0140_ConsultaProductMasterSomenteParaComponentes0140()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("SEMI", "0140", "000000"),
            Componente("MP", "0050", "000000"),
            Componente("QUIM", "0060", "000000")
        ];
        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000");
        List<string> consultados = [];

        await ProcessoConsumoSemiAcabadoForm.ObterMestresComponentesDaOperacaoAsync(
            filtrados,
            (codigos, _) =>
            {
                consultados.AddRange(codigos);
                return Task.FromResult(MapearMestres(codigos));
            });

        Assert.Equal(new[] { "SEMI" }, consultados);
    }

    [Fact]
    public async Task Contexto0140_ErroNoProductMasterDeOutraOperacaoNaoBloqueia()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("SEMI", "0140", "000000"),
            Componente("MATERIAL_COM_ERRO", "0050", "000000")
        ];
        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoSemiAcabadoForm.FiltrarComponentesPorOperacao(componentes, "0140", "000000");
        List<string> consultados = [];

        await ProcessoConsumoSemiAcabadoForm.ObterMestresComponentesDaOperacaoAsync(
            filtrados,
            (codigos, _) =>
            {
                List<string> lista = codigos.ToList();
                if (lista.Contains("MATERIAL_COM_ERRO", StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Componente de outra operação não deveria ser consultado.");
                }

                consultados.AddRange(lista);
                return Task.FromResult(MapearMestres(lista));
            });

        Assert.Equal(new[] { "SEMI" }, consultados);
    }

    [Fact]
    public void ContextoApontamento_ComponenteOutro_NaoBloqueiaPorModo()
    {
        ComponenteConsumoMaterial componente = ComponenteClassificado(ClassificacaoConsumoMaterial.Outro);

        bool bloqueia = ProcessoConsumoSemiAcabadoForm.ClassificacaoPorModoBloqueiaComponente(
            componente,
            ModoConsumoMaterial.MateriaPrima,
            possuiContextoApontamento: true);

        Assert.False(bloqueia);
    }

    [Fact]
    public void ContextoApontamento_HALBClassificadoComoOutro_NaoBloqueiaPorModo()
    {
        ComponenteConsumoMaterial componente = ComponenteClassificado(ClassificacaoConsumoMaterial.Outro);
        componente.TipoMaterialSap = "HALB";

        bool bloqueia = ProcessoConsumoSemiAcabadoForm.ClassificacaoPorModoBloqueiaComponente(
            componente,
            ModoConsumoMaterial.MateriaPrima,
            possuiContextoApontamento: true);

        Assert.False(bloqueia);
    }

    [Theory]
    [InlineData(ClassificacaoConsumoMaterial.Quimico)]
    [InlineData(ClassificacaoConsumoMaterial.Indefinido)]
    public void ContextoApontamento_ClassificacaoNaoBloqueiaComponenteSelecionadoPorOperacao(
        ClassificacaoConsumoMaterial classificacao)
    {
        ComponenteConsumoMaterial componente = ComponenteClassificado(classificacao);

        bool bloqueia = ProcessoConsumoSemiAcabadoForm.ClassificacaoPorModoBloqueiaComponente(
            componente,
            ModoConsumoMaterial.MateriaPrima,
            possuiContextoApontamento: true);

        Assert.False(bloqueia);
    }

    [Theory]
    [InlineData(ClassificacaoConsumoMaterial.Outro)]
    [InlineData(ClassificacaoConsumoMaterial.Quimico)]
    public void FluxoManual_MateriaPrima_BloqueiaComponenteForaDoModo(ClassificacaoConsumoMaterial classificacao)
    {
        ComponenteConsumoMaterial componente = ComponenteClassificado(classificacao);

        bool bloqueia = ProcessoConsumoSemiAcabadoForm.ClassificacaoPorModoBloqueiaComponente(
            componente,
            ModoConsumoMaterial.MateriaPrima,
            possuiContextoApontamento: false);

        Assert.True(bloqueia);
    }

    [Fact]
    public void FluxoManual_MateriaPrima_AceitaComponenteMateriaPrima()
    {
        ComponenteConsumoMaterial componente = ComponenteClassificado(ClassificacaoConsumoMaterial.MateriaPrima);

        bool bloqueia = ProcessoConsumoSemiAcabadoForm.ClassificacaoPorModoBloqueiaComponente(
            componente,
            ModoConsumoMaterial.MateriaPrima,
            possuiContextoApontamento: false);

        Assert.False(bloqueia);
    }

    [Fact]
    public void ComponentePodeOperar_MantemValidacoesOperacionais()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoSemiAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private bool ComponentePodeOperar");

        Assert.Contains("componente is null", metodo, StringComparison.Ordinal);
        Assert.Contains("Tipo SAP bloqueado", metodo, StringComparison.Ordinal);
        Assert.Contains("Sem dep", metodo, StringComparison.Ordinal);
        Assert.Contains("sem lote SAP", metodo, StringComparison.Ordinal);
        Assert.Contains("Backflush", metodo, StringComparison.Ordinal);
        Assert.Contains("AvaliarLiberacaoPesagem", metodo, StringComparison.Ordinal);
        Assert.Contains("CalcularDisponivelConsumoComTolerancia", metodo, StringComparison.Ordinal);
        Assert.Contains("PesagemLiberada", metodo, StringComparison.Ordinal);
        Assert.Contains("ClassificacaoPorModoBloqueiaComponente", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void FechamentoSemConclusao_MantemNaoConcluido()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoSemiAcabadoForm.cs");

        Assert.Contains("ResultadoExecucaoApontamento { get; private set; }", form, StringComparison.Ordinal);
        Assert.Contains("= ResultadoExecucaoProcesso.NaoConcluido;", form, StringComparison.Ordinal);
        Assert.Contains("Sem contexto: não faz absolutamente nada", form, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ResultadoExecucaoProcessoApontamento.ConfirmadoSap, true)]
    [InlineData(ResultadoExecucaoProcessoApontamento.ErroSap, false)]
    [InlineData(ResultadoExecucaoProcessoApontamento.DivergenciaSap, false)]
    [InlineData(ResultadoExecucaoProcessoApontamento.NaoConcluido, false)]
    public void ResultadoOperacional_ControlaLiberacaoDeFinalizacao(
        ResultadoExecucaoProcessoApontamento resultado, bool esperado)
        => Assert.Equal(
            esperado,
            new ResultadoExecucaoProcesso(resultado, 10, "mensagem", resultado == ResultadoExecucaoProcessoApontamento.ConfirmadoSap)
                .AtividadeConcluida);

    [Fact]
    public void Menu_NaoRecebeNovoCardParaConsumoSemiAcabado()
    {
        string processoProducao = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");
        string painelInicial = LerArquivoProjeto("Tela", "PainelInicialForm.cs");

        Assert.DoesNotContain("Consumo de Semi Acabado", processoProducao, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessoConsumoSemiAcabadoForm form = new();", painelInicial, StringComparison.Ordinal);
    }

    [Fact]
    public void NovaTela_DeveTerTituloConsumoDeSemiAcabado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoSemiAcabadoForm.cs");

        Assert.Contains("private const string TituloConsumoSemiAcabado = \"Consumo de Semi Acabado\";", form, StringComparison.Ordinal);
        Assert.Contains("headerTitleLabel.Text = TituloConsumoSemiAcabado;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelasExistentes_DeMateriaPrimaEQuimicosContinuamSemRotaSemiAcabado()
    {
        string formOriginal = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.DoesNotContain("ProcessoConsumoSemiAcabadoForm", formOriginal, StringComparison.Ordinal);
        Assert.Contains("public ProcessoConsumoMaterialForm(ModoConsumoMaterial modo, ContextoApontamentoProcesso contextoApontamento)", formOriginal, StringComparison.Ordinal);
    }

    private static ComponenteConsumoMaterial Componente(string codigo, string operacao, string sequencia)
        => new() { CodigoMaterial = codigo, Operacao = operacao, SequenciaOperacao = sequencia };

    private static ComponenteConsumoMaterial ComponenteClassificado(ClassificacaoConsumoMaterial classificacao)
        => new()
        {
            CodigoMaterial = "SEMI",
            Operacao = "0140",
            SequenciaOperacao = "000000",
            ClassificacaoConsumo = classificacao
        };

    private static IReadOnlyDictionary<string, ProdutoSapMestre> MapearMestres(IEnumerable<string> codigos)
        => codigos.ToDictionary(
            codigo => codigo,
            codigo => new ProdutoSapMestre
            {
                CodigoProduto = codigo,
                TipoMaterialSap = "ROH",
                GrupoMaterialSap = "TESTE",
                Consultado = true
            },
            StringComparer.OrdinalIgnoreCase);

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
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            DirectoryInfo? parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
