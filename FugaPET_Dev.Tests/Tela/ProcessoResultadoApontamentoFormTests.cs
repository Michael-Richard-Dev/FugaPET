using System.Drawing;
using System.Windows.Forms;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoResultadoApontamentoFormTests
{
    /// <summary>
    /// Item de teste. O servico do Q carrega as definicoes do banco por perfil e NAO tem mais um
    /// CriarDefinicoesPadrao: a fixture passa a construir o item aqui, sem reintroduzir fallback fixo.
    /// </summary>
    private static ResultadoApontamentoItem ItemFixture()
        => new()
        {
            CodigoDefinicao = 1,
            CodigoItem = "TEMPO",
            Tipo = "TEMPO",
            Medida = "MINUTO",
            Referencia = 5m,
            OrdemExibicao = 1,
            Obrigatorio = true
        };

    private static IReadOnlyList<ResultadoApontamentoItem> DefinicoesFixture()
        => new[] { ItemFixture() };

    [Fact]
    public void ResultadoApontamento_TipoProcesso_DeveDeclararConstante()
        => Assert.Equal("RESULTADO_APONTAMENTO", TipoProcessoOperacao.ResultadoApontamento);

    [Fact]
    public void ResultadoApontamento_Roteamento_DeveAbrirNovaTelaPorTipoProcesso()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string abertura = ExtrairMetodo(form, "private async Task AbrirTelaDestinoAsync");

        Assert.Contains("TipoProcessoOperacao.ResultadoApontamento => AbrirResultadoApontamento(contexto)", abertura, StringComparison.Ordinal);
        Assert.Contains("private ResultadoExecucaoProcesso AbrirResultadoApontamento(ContextoApontamentoProcesso contexto)", form, StringComparison.Ordinal);
        Assert.Contains("using ProcessoResultadoApontamentoForm form = new(contexto);", form, StringComparison.Ordinal);
        Assert.Contains("form.ShowDialog(this);", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_Tela_DeveExigirContextoApontamentoProcesso()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.DoesNotContain("public ProcessoResultadoApontamentoForm()", form, StringComparison.Ordinal);
        Assert.Contains("public ProcessoResultadoApontamentoForm(ContextoApontamentoProcesso contexto)", form, StringComparison.Ordinal);
        Assert.Contains("_contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_Titulo_DeveSerResultadoDoApontamento()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.Designer.cs");

        Assert.Contains("headerTitleLabel.Text = \"Resultado do Apontamento\";", designer, StringComparison.Ordinal);
        Assert.Contains("Text = \"Resultado do Apontamento\";", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_Subtitulo_DeveConterOpOperacaoEDescricao()
    {
        ContextoApontamentoProcesso contexto = ContextoPadrao();

        string subtitulo = ProcessoResultadoApontamentoForm.MontarSubtitulo(contexto);

        Assert.Contains("OP 1001732", subtitulo, StringComparison.Ordinal);
        Assert.Contains("Operação 0070", subtitulo, StringComparison.Ordinal);
        Assert.Contains("MISTURA DE RECEITA", subtitulo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_Grid_DeveTerColunasTipoMedidaReferenciaResultado()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.Designer.cs");

        foreach (string coluna in new[] { "Tipo", "Medida", "Referência", "Resultado" })
        {
            Assert.Contains($"HeaderText = \"{coluna}\";", designer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResultadoApontamento_SomenteResultado_DeveSerEditavel()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.Contains("colTipo.ReadOnly = true;", form, StringComparison.Ordinal);
        Assert.Contains("colMedida.ReadOnly = true;", form, StringComparison.Ordinal);
        Assert.Contains("colReferencia.ReadOnly = true;", form, StringComparison.Ordinal);
        Assert.Contains("colResultado.ReadOnly = false;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_PrimeiraCelulaResultado_DeveReceberFoco()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string foco = ExtrairMetodo(form, "private void FocarPrimeiroResultado()");

        Assert.Contains("resultadosGridView.Rows[0].Cells[colResultado.Index]", foco, StringComparison.Ordinal);
        Assert.Contains("resultadosGridView.BeginEdit(true);", foco, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_ResultadoVazio_DeveBloquearGravacao()
    {
        ResultadoApontamentoItem item = new() { Tipo = "TEMPO", Medida = "MINUTO", Obrigatorio = true };

        string? erro = ResultadoApontamentoServico.ValidarItens(new[] { item });

        Assert.Contains("Informe o resultado", erro, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_ResultadoNaoNumerico_DeveBloquearGravacao()
        => Assert.False(ProcessoResultadoApontamentoForm.TentarConverterResultado("abc", out _));

    [Fact]
    public void ResultadoApontamento_ResultadoDecimalPtBr_DeveSerAceito()
    {
        bool valido = ProcessoResultadoApontamentoForm.TentarConverterResultado("5,75", out decimal valor);

        Assert.True(valido);
        Assert.Equal(5.75m, valor);
    }

    [Fact]
    public async Task ResultadoApontamento_GravacaoConfirmada_DeveRetornarConcluidoLocalmente()
    {
        FakeResultadoApontamentoRepositorio repo = new();
        ResultadoApontamentoServico servico = new(repo);
        ResultadoApontamentoItem item = new()
        {
            CodigoItem = "TEMPO_MINUTO_PADRAO",
            Tipo = "TEMPO",
            Medida = "MINUTO",
            Referencia = 5m,
            Resultado = 5.5m,
            OrdemExibicao = 1,
            Obrigatorio = true
        };

        var resultado = await servico.RegistrarAsync(ContextoPadrao(), new[] { item });
        ResultadoExecucaoProcesso execucao = new(
            ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente,
            resultado.IdGerado,
            resultado.Mensagem,
            indicadorConfirmadoSap: false);

        Assert.True(resultado.Sucesso);
        Assert.Equal(ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente, execucao.Resultado);
        Assert.True(execucao.AtividadeConcluida);
        Assert.Equal(987, execucao.CodigoRegistroProcesso);
    }

    [Fact]
    public void ResultadoApontamento_FecharSemGravar_DeveRetornarNaoConcluido()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.Contains("= ResultadoExecucaoProcesso.NaoConcluido;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_Tela_NaoAlteraApontamentoDiretoParaConcluida()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.DoesNotContain("StatusApontamentoOperacao.Concluida", form, StringComparison.Ordinal);
        Assert.DoesNotContain("CONCLUIDA", form, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente", form, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0070")]
    [InlineData("0090")]
    [InlineData("0100")]
    [InlineData("0105")]
    public void ResultadoApontamento_NaoDeveHardcodarOperacoesNoRoteamentoProdutivo(string operacao)
    {
        string controle = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string novaTela = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.DoesNotContain($"== \"{operacao}\"", controle, StringComparison.Ordinal);
        Assert.DoesNotContain($"== \"{operacao}\"", novaTela, StringComparison.Ordinal);
        Assert.DoesNotContain($"case \"{operacao}\"", controle, StringComparison.Ordinal);
        Assert.DoesNotContain($"case \"{operacao}\"", novaTela, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_NaoDeveChamarApiSap()
    {
        string conjunto = ArquivosResultadoApontamento();

        Assert.DoesNotContain("HttpClient", conjunto, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductionOrderSapApiClient", conjunto, StringComparison.Ordinal);
        Assert.DoesNotContain("MaterialDocument", conjunto, StringComparison.Ordinal);
        Assert.DoesNotContain("CSRF", conjunto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PATCH", conjunto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultadoApontamento_NaoDeveInicializarBalanca()
    {
        string conjunto = ArquivosResultadoApontamento();

        Assert.DoesNotContain("Balanca", conjunto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LeitorBalanca", conjunto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultadoApontamento_NaoDeveCriarCardNoMenu()
    {
        string painel = LerArquivoProjeto("Tela", "PainelInicialForm.cs");
        string processo = LerArquivoProjeto("Tela", "ProcessoProducaoForm.cs");
        string designer = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");

        Assert.DoesNotContain("ProcessoResultadoApontamentoForm", painel, StringComparison.Ordinal);
        Assert.DoesNotContain("Resultado do Apontamento", processo, StringComparison.Ordinal);
        Assert.DoesNotContain("Resultado do Apontamento", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_MPQuimicosESemiAcabado_DevemManterRoteamentoAtual()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");
        string abertura = ExtrairMetodo(form, "private async Task AbrirTelaDestinoAsync");

        Assert.Contains("TipoProcessoOperacao.ConsumoMateriaPrima => AbrirConsumo(contexto, ModoConsumoMaterial.MateriaPrima)", abertura, StringComparison.Ordinal);
        Assert.Contains("TipoProcessoOperacao.ConsumoQuimicos => AbrirConsumo(contexto, ModoConsumoMaterial.Quimico)", abertura, StringComparison.Ordinal);
        // ConsumoSemiAcabado foi removido do DEV ao adotar o gate FUGAPET-Q-TEST-SNAPSHOT-ALIGNMENT-07.
    }

    [Fact]
    public void ResultadoApontamento_OperacaoSeguinte_BloqueiaEnquantoAnteriorNaoConcluida()
    {
        ResultadoExecucaoProcesso execucao = ResultadoExecucaoProcesso.NaoConcluido;

        Assert.False(execucao.AtividadeConcluida);
    }

    [Fact]
    public void ResultadoApontamento_OperacaoSeguinte_LiberaSomenteQuandoAnteriorConcluida()
    {
        ResultadoExecucaoProcesso execucao = new(
            ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente,
            1,
            "Resultado registrado.",
            indicadorConfirmadoSap: false);

        Assert.True(execucao.AtividadeConcluida);
    }

    [Fact]
    public void ResultadoApontamento_Sequencia_DeveSerObtidaDinamicamenteDasOperacoesDaOp()
    {
        string servico = LerArquivoProjeto("Servicos", "Processo", "ProcessoControleApontamentosServico.cs");
        string metodo = ExtrairMetodo(servico, "internal static IReadOnlyList<OperacaoOrdemProducaoSap> OrdenarTecnicamente");

        Assert.Contains("ordem.Operacoes", metodo, StringComparison.Ordinal);
        Assert.Contains("ThenBy(o => o.Operacao", metodo, StringComparison.Ordinal);
        Assert.Contains("ThenBy(o => o.Suboperacao", metodo, StringComparison.Ordinal);
        Assert.Contains("ThenBy(o => o.OrderOperationInternalId", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_OP1001732_DeveResolver0105ComoAnteriorDa0140()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("0050", "0060", "0070", "0090", "0100", "0105", "0140");
        OperacaoOrdemProducaoSap atual = ordem.Operacoes.Single(o => o.Operacao == "0140");

        OperacaoOrdemProducaoSap? anterior = ProcessoControleApontamentosServico.ObterOperacaoAnterior(ordem, atual);

        Assert.NotNull(anterior);
        Assert.Equal("0105", anterior.Operacao);
    }

    [Fact]
    public void ResultadoApontamento_OPCurta_DeveResolver0030ComoAnteriorDa0140()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("0020", "0030", "0140");
        OperacaoOrdemProducaoSap atual = ordem.Operacoes.Single(o => o.Operacao == "0140");

        OperacaoOrdemProducaoSap? anterior = ProcessoControleApontamentosServico.ObterOperacaoAnterior(ordem, atual);

        Assert.NotNull(anterior);
        Assert.Equal("0030", anterior.Operacao);
    }

    // REMOVIDO na promocao da linha Q: ResultadoApontamento_DefinicaoInicial_DeveSerTempoMinutoReferencia5
    // exigia a definicao fixa TEMPO/MINUTO/5 vinda de CriarDefinicoesPadrao. No desenho do Q as definicoes
    // vem do banco por perfil (GAIA 041 / pacote 054) e nao existe mais definicao embutida no codigo.

    [Fact]
    public async Task ResultadoApontamento_SemRepositorio_NaoPodeSimularGravacaoSilenciosa()
    {
        ResultadoApontamentoServico servico = new();
        ResultadoApontamentoItem item = ItemFixture();
        item.Resultado = 5m;

        var resultado = await servico.RegistrarAsync(ContextoComPerfil(), new[] { item });

        Assert.False(resultado.Sucesso);
        // No Q o construtor sem parametros JA monta um repositorio real, logo a falha chega sanitizada
        // pelo GATE 050 (§10): pode ser "nao configurada" ou "falha tecnica". O requisito que importa e
        // nao haver sucesso falso nem vazamento de diagnostico.
        Assert.False(string.IsNullOrWhiteSpace(resultado.Mensagem));
        Assert.DoesNotContain("Npgsql", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResultadoApontamento_View_NaoDeveAcessarPostgreSqlDiretamente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.DoesNotContain("Npgsql", form, StringComparison.Ordinal);
        // A View continua sem tocar no banco. O using de AcessoDados.Repositorio existe apenas para
        // nomear o DTO ResultadoPersistidoApontamento, que mora nesse namespace; o que importa e nao
        // haver campo nem instanciacao de repositorio na tela.
        Assert.DoesNotContain("IResultadoApontamentoRepositorio", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Repositorio(", form, StringComparison.Ordinal);
        Assert.Contains("ResultadoApontamentoController", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_Designer_DeveUsarPadraoVisualFugaPet()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.Designer.cs");

        Assert.Contains("CorCabecalho = Color.FromArgb(17, 24, 39)", designer, StringComparison.Ordinal);
        Assert.Contains("new RoundedPanel()", designer, StringComparison.Ordinal);
        Assert.Contains("Segoe UI", designer, StringComparison.Ordinal);
        // Reskin da linha Q: o acento vermelho (229, 27, 43) passou a laranja (200, 78, 10).
        Assert.Contains("CorAcento = Color.FromArgb(200, 78, 10)", designer, StringComparison.Ordinal);
        Assert.Contains("footerPanel", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_BotoesEAtalhos_DevemSerF10EEsc()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.Designer.cs");

        Assert.Contains("Keys.F10", form, StringComparison.Ordinal);
        Assert.Contains("Keys.Escape", form, StringComparison.Ordinal);
        Assert.Contains("GRAVAR RESULTADO  F10", designer, StringComparison.Ordinal);
        Assert.Contains("FECHAR  Esc", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Gravar Término do Resultado Apontamento", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_DeveBloquearFechamentoDuranteGravacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private void ProcessoResultadoApontamentoForm_FormClosing");

        Assert.Contains("_gravacaoEmAndamento", metodo, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_DeveImpedirDuploCliqueNoBotaoGravar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task GravarResultadoAsync()");

        Assert.Contains("if (_gravacaoEmAndamento)", metodo, StringComparison.Ordinal);
        Assert.Contains("DefinirOperacaoEmAndamento(true);", metodo, StringComparison.Ordinal);
        // Requisito preservado; o Q generalizou a condicao de &&_itens.Count > 0 para && podeGravar.
        Assert.Contains("gravarResultadoButton.Enabled = !emAndamento && podeGravar;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_NaoDeveConsultarComponentesConsumoOuImprimir()
    {
        string conjunto = ArquivosResultadoApontamento();

        Assert.DoesNotContain("Componente", conjunto, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsumoMaterial", conjunto, StringComparison.Ordinal);
        Assert.DoesNotContain("ImpressoraEtiquetaServico", conjunto, StringComparison.Ordinal);
        Assert.DoesNotContain("Impressao", conjunto, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_ContratoRepositorio_DeveExistirComMetodosMinimos()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "IResultadoApontamentoRepositorio.cs");

        Assert.Contains("Task<long> InserirAsync", repo, StringComparison.Ordinal);
        Assert.Contains("RegistroResultadoApontamento registro", repo, StringComparison.Ordinal);
        Assert.Contains("Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync", repo, StringComparison.Ordinal);
        // O Q resolve as definicoes pelo perfil persistido, nao mais pelo contexto da tela.
        Assert.Contains("long codigoPerfilResultado", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_PropostaGaia_DeveSerNecessariaSemSqlNestaTarefa()
    {
        string service = LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");

        Assert.Contains("MensagemPersistenciaNaoConfigurada", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO", service, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void ResultadoApontamento_Rodape_NaoDeveUsarIdentificacaoFixaDev()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");

        Assert.DoesNotContain("conforme ambiente DEV", form, StringComparison.Ordinal);
        Assert.Contains("_controller.ObterIdentificacaoBanco()", form, StringComparison.Ordinal);
    }
    [Fact]
    public void ResultadoApontamento_CarregarDefinicoes_DeveAtualizarEstadoDepoisDePreencherGrid()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task CarregarDefinicoesAsync()");

        int preencher = metodo.IndexOf("PreencherGrid();", StringComparison.Ordinal);
        int atualizar = metodo.IndexOf("AtualizarEstadoDefinicoes();", StringComparison.Ordinal);

        Assert.True(preencher >= 0, "CarregarDefinicoesAsync deve preencher a grade.");
        Assert.True(atualizar > preencher, "CarregarDefinicoesAsync deve atualizar o estado depois de preencher a grade.");
        Assert.DoesNotContain("FocarPrimeiroResultado();", metodo[..atualizar], StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_RepositorioVazio_DeveDesabilitarBotaoEDefinirSemDefinicao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarEstadoDefinicoes()");

        Assert.Contains("bool possuiDefinicoes = _itens.Count > 0;", metodo, StringComparison.Ordinal);
        // O Q admite gravar apenas o cabecalho quando o perfil resolveu e nao ha itens (permiteHeaderOnly).
        Assert.Contains("gravarResultadoButton.Enabled = possuiDefinicoes || permiteHeaderOnly;", metodo, StringComparison.Ordinal);
        Assert.Contains("statusValueLabel.Text = \"SEM DEFINIÇÃO\";", metodo, StringComparison.Ordinal);
        Assert.Contains("Nenhum item de resultado está configurado para esta operação.", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_RepositorioVazio_NaoDeveFocarCelulaInexistente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarEstadoDefinicoes()");

        int semDefinicao = metodo.IndexOf("if (!possuiDefinicoes)", StringComparison.Ordinal);
        int retorno = metodo.IndexOf("return;", semDefinicao, StringComparison.Ordinal);
        int foco = metodo.IndexOf("FocarPrimeiroResultado();", StringComparison.Ordinal);

        Assert.True(semDefinicao >= 0);
        Assert.True(retorno > semDefinicao);
        Assert.True(foco > retorno, "Foco só pode ocorrer depois do retorno do estado sem definição.");
    }

    [Fact]
    public void ResultadoApontamento_FinalOperacao_NaoReabilitaBotaoSemItens()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private void DefinirOperacaoEmAndamento");

        // Requisito preservado; o Q generalizou && _itens.Count > 0 para && podeGravar.
        Assert.Contains("gravarResultadoButton.Enabled = !emAndamento && podeGravar;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_FinalOperacao_ReabilitaBotaoQuandoExistemItens()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private void DefinirOperacaoEmAndamento");

        // Requisito preservado; o Q generalizou _itens.Count > 0 para podeGravar.
        Assert.Contains("!emAndamento && podeGravar", metodo, StringComparison.Ordinal);
        Assert.Contains("fecharButton.Enabled = !emAndamento;", metodo, StringComparison.Ordinal);
        Assert.Contains("resultadosGridView.Enabled = !emAndamento;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_PreencherGrid_DeveUsarFormatarDecimalNaReferenciaEResultado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private void PreencherGrid()");

        Assert.Contains("FormatarDecimal(item.Referencia)", metodo, StringComparison.Ordinal);
        Assert.Contains("item.Resultado is null ? string.Empty : FormatarDecimal(item.Resultado.Value)", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("ToString(\"N3\"", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoApontamento_FalhaCarregamento_DeveLimparGradeEDesabilitarGravacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task CarregarDefinicoesAsync()");

        Assert.Contains("_itens.Clear();", metodo, StringComparison.Ordinal);
        Assert.Contains("resultadosGridView.Rows.Clear();", metodo, StringComparison.Ordinal);
        Assert.Contains("gravarResultadoButton.Enabled = false;", metodo, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoApontamento = ResultadoExecucaoProcesso.NaoConcluido;", metodo, StringComparison.Ordinal);
        Assert.Contains("statusValueLabel.Text = \"ERRO AO CARREGAR\";", metodo, StringComparison.Ordinal);
    }

    // REMOVIDO na promocao da linha Q: ResultadoApontamento_SemRepositorio_DeveRetornarDefinicaoProvisoria
    // exigia que, SEM repositorio, o servico devolvesse uma definicao provisoria TEMPO/MINUTO/5. No Q
    // isso e fail-closed: sem perfil resolvido devolve lista vazia e, com perfil e sem repositorio,
    // lanca InvalidOperationException. Definicao provisoria inventada foi eliminada de proposito.

    [Fact]
    public async Task ResultadoApontamento_RepositorioComDefinicoes_DeveRetornarDefinicoesPersistidas()
    {
        ResultadoApontamentoItem definido = new()
        {
            CodigoItem = "PH",
            Tipo = "QUALIDADE",
            Medida = "PH",
            Referencia = 7.5m,
            OrdemExibicao = 1,
            Obrigatorio = true
        };
        ResultadoApontamentoServico servico = new(new FakeResultadoApontamentoRepositorio(new[] { definido }));

        IReadOnlyList<ResultadoApontamentoItem> itens = await servico.ListarDefinicoesAsync(ContextoComPerfil());

        ResultadoApontamentoItem item = Assert.Single(itens);
        Assert.Equal("QUALIDADE", item.Tipo);
        Assert.Equal("PH", item.Medida);
        Assert.Equal(7.5m, item.Referencia);
    }

    [Fact]
    public async Task ResultadoApontamento_RepositorioExistenteEVazio_NaoDeveRetornarPadrao()
    {
        ResultadoApontamentoServico servico = new(new FakeResultadoApontamentoRepositorio(Array.Empty<ResultadoApontamentoItem>()));

        IReadOnlyList<ResultadoApontamentoItem> itens = await servico.ListarDefinicoesAsync(ContextoPadrao());

        Assert.Empty(itens);
    }

    [Fact]
    public async Task ResultadoApontamento_RepositorioVazio_DeveBloquearGravacao()
    {
        ResultadoApontamentoServico servico = new(new FakeResultadoApontamentoRepositorio(Array.Empty<ResultadoApontamentoItem>()));

        var resultado = await servico.RegistrarAsync(ContextoPadrao(), Array.Empty<ResultadoApontamentoItem>());

        Assert.False(resultado.Sucesso);
        Assert.Contains("Não há itens", resultado.Mensagem, StringComparison.Ordinal);
    }

    // REMOVIDO na promocao da linha Q: ResultadoApontamento_OperacaoSemDefinicao_NaoRecebeTempoMinuto5
    // asseverava que ListarDefinicoesAsync terminava em "return CriarDefinicoesPadrao();", isto e, exigia o
    // fallback para definicao fixa. O Q removeu esse fallback de proposito: sem perfil resolvido ou com erro
    // de banco o servico e fail-closed, em vez de devolver definicao inventada.

    [Fact]
    public void ResultadoApontamento_Form_DeveTratarExcecaoDePersistenciaSemConcluir()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
        string gravar = ExtrairMetodo(form, "private async Task GravarResultadoAsync()");

        Assert.Contains("catch (Exception ex)", gravar, StringComparison.Ordinal);
        Assert.Contains("ResultadoExecucaoApontamento = ResultadoExecucaoProcesso.NaoConcluido;", gravar, StringComparison.Ordinal);
        Assert.Contains("DialogResult = DialogResult.None;", gravar, StringComparison.Ordinal);
        Assert.Contains("O apontamento permanece em andamento e pode ser retomado", gravar, StringComparison.Ordinal);
        Assert.Contains("Trace.TraceError", gravar, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionString", gravar, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StackTrace", gravar, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResultadoApontamento_RepositorioComFalha_DeveVirarFalhaSanitizadaSemPropagar()
    {
        ResultadoApontamentoServico servico = new(new RepositorioResultadoApontamentoComFalha());
        ResultadoApontamentoItem item = ItemFixture();
        item.Resultado = 5m;

        // O GATE 050 (§10) do Q deixou de propagar a excecao para a tela: a causa fica no trace e a
        // tela recebe mensagem sanitizada. Propagar expunha tipo de excecao e diagnostico na interface.
        var resultado = await servico.RegistrarAsync(ContextoComPerfil(), new[] { item });

        Assert.False(resultado.Sucesso);
        Assert.Equal(
            "Falha técnica ao gravar o resultado do apontamento. Tente novamente; se persistir, acione o suporte.",
            resultado.Mensagem);
    }

    [Fact]
    public void ResultadoApontamento_ObrigatorioVazio_DeveBloquear()
    {
        ResultadoApontamentoItem item = new() { Tipo = "TEMPO", Medida = "MINUTO", Obrigatorio = true };

        Assert.False(ProcessoResultadoApontamentoForm.TentarConverterResultadoItem(item, string.Empty, out _));
    }

    [Fact]
    public void ResultadoApontamento_OpcionalVazio_DeveSerAceito()
    {
        ResultadoApontamentoItem item = new() { Tipo = "TEMPO", Medida = "MINUTO", Obrigatorio = false };

        bool ok = ProcessoResultadoApontamentoForm.TentarConverterResultadoItem(item, string.Empty, out decimal? resultado);

        Assert.True(ok);
        Assert.Null(resultado);
    }

    [Fact]
    public void ResultadoApontamento_OpcionalDecimalPtBr_DeveSerAceito()
    {
        ResultadoApontamentoItem item = new() { Tipo = "TEMPO", Medida = "MINUTO", Obrigatorio = false };

        bool ok = ProcessoResultadoApontamentoForm.TentarConverterResultadoItem(item, "5,25", out decimal? resultado);

        Assert.True(ok);
        Assert.Equal(5.25m, resultado);
    }

    [Fact]
    public void ResultadoApontamento_OpcionalTextoInvalido_DeveBloquear()
    {
        ResultadoApontamentoItem item = new() { Tipo = "TEMPO", Medida = "MINUTO", Obrigatorio = false };

        Assert.False(ProcessoResultadoApontamentoForm.TentarConverterResultadoItem(item, "abc", out _));
    }

    [Fact]
    public void ResultadoApontamento_ValorNegativo_DeveContinuarBloqueado()
    {
        ResultadoApontamentoItem item = new() { Tipo = "TEMPO", Medida = "MINUTO", Obrigatorio = false };

        Assert.False(ProcessoResultadoApontamentoForm.TentarConverterResultadoItem(item, "-1", out _));
    }

    [Theory]
    [InlineData("5", "5")]
    [InlineData("5,5", "5,5")]
    [InlineData("5,250", "5,25")]
    public void ResultadoApontamento_Referencia_DeveRemoverZerosDecimaisDesnecessarios(string entrada, string esperado)
    {
        decimal valor = decimal.Parse(entrada, System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

        Assert.Equal(esperado, ProcessoResultadoApontamentoForm.FormatarDecimal(valor));
    }


    [Fact]
    public void ResultadoApontamento_LayoutReal_DeveMostrarBotoesNoPainelDeAcoes()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoResultadoApontamentoForm form = CriarFormNormal();
            ExibirEProcessarLayout(form);

            Control lateral = ObterControle(form, "lateralCard");
            Control acoes = ObterControle(form, "acoesPanel");
            Button gravar = ObterControle<Button>(form, "gravarResultadoButton");
            Button fechar = ObterControle<Button>(form, "fecharButton");
            Control footer = ObterControle(form, "footerPanel");

            Assert.True(gravar.Visible);
            Assert.True(fechar.Visible);
            Assert.Same(acoes.Controls[0], gravar.Parent);
            Assert.Same(acoes.Controls[0], fechar.Parent);
            Assert.True(lateral.ClientRectangle.Contains(lateral.PointToClient(gravar.Parent!.PointToScreen(gravar.Bounds.Location))));
            Assert.True(lateral.ClientRectangle.Contains(lateral.PointToClient(fechar.Parent!.PointToScreen(fechar.Bounds.Location))));
            Assert.False(gravar.Bounds.IntersectsWith(fechar.Bounds));
            Assert.True(acoes.Bottom <= lateral.ClientRectangle.Bottom);
            Assert.True(lateral.Bottom <= footer.Top || footer.Dock == DockStyle.Bottom);
            Assert.True(gravar.Enabled);
            Assert.True(fechar.Enabled);
        });
    }

    [Fact]
    public void ResultadoApontamento_LayoutReal_Em1280x720_DeveManterBotoesVisiveis()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoResultadoApontamentoForm form = CriarFormNormal();
            form.ClientSize = new Size(1280, 720);
            ExibirEProcessarLayout(form);

            Button gravar = ObterControle<Button>(form, "gravarResultadoButton");
            Button fechar = ObterControle<Button>(form, "fecharButton");
            Control lateral = ObterControle(form, "lateralCard");

            Assert.True(gravar.Visible);
            Assert.True(fechar.Visible);
            Assert.True(lateral.ClientRectangle.Contains(lateral.PointToClient(gravar.Parent!.PointToScreen(gravar.Bounds.Location))));
            Assert.True(lateral.ClientRectangle.Contains(lateral.PointToClient(fechar.Parent!.PointToScreen(fechar.Bounds.Location))));
        });
    }

    [Fact]
    public void ResultadoApontamento_SemDefinicao_ComPerfilResolvido_PermiteGravarSomenteCabecalho()
    {
        // MUDANCA DE COMPORTAMENTO DA LINHA Q: antes, nenhuma definicao significava nada gravavel
        // ("SEM DEFINICAO", botao desabilitado). O Q introduziu o caminho permiteHeaderOnly: com o
        // perfil resolvido e zero itens, a operacao PODE ser gravada apenas como cabecalho, com a
        // mensagem "Nenhum resultado a informar para esta operacao". Caminho nomeado e com constante
        // dedicada no codigo do Q, portanto deliberado. O estado "SEM DEFINICAO" segue existindo para
        // quando o perfil NAO resolve.
        ExecutarEmSta(() =>
        {
            using ProcessoResultadoApontamentoForm form = CriarFormSemDefinicao(out _);
            ExibirEProcessarLayout(form);

            Button gravar = ObterControle<Button>(form, "gravarResultadoButton");
            Button fechar = ObterControle<Button>(form, "fecharButton");
            Label atalho = ObterControle<Label>(form, "atalhoValueLabel");
            Label instrucao = ObterControle<Label>(form, "instrucaoLabel");
            Label status = ObterControle<Label>(form, "statusValueLabel");

            Assert.True(gravar.Visible);
            Assert.True(fechar.Visible);
            Assert.True(gravar.Enabled);
            Assert.True(fechar.Enabled);
            Assert.Equal("SEM RESULTADO", status.Text);
            Assert.Contains("F10 gravar", atalho.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Digite o resultado", instrucao.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Nenhum resultado a informar", instrucao.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ResultadoApontamento_SemDefinicao_ComPerfilResolvido_F10GravaSomenteCabecalho()
    {
        // Par do teste acima, mesma mudanca da linha Q: com perfil resolvido e zero itens, o F10 grava
        // um registro SO DE CABECALHO (Itens vazio, resumo "Nenhum resultado a informar para esta
        // operacao") em vez de nao gravar nada. Antes, na linha DEV, F10 sem definicao nao chamava a
        // gravacao. A tecla continua sendo consumida pela tela.
        ExecutarEmSta(() =>
        {
            using ProcessoResultadoApontamentoForm form = CriarFormSemDefinicao(out FakeResultadoApontamentoRepositorio repositorio);
            ExibirEProcessarLayout(form);
            KeyEventArgs args = new(Keys.F10);

            typeof(ProcessoResultadoApontamentoForm)
                .GetMethod("ProcessoResultadoApontamentoForm_KeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(form, new object?[] { form, args });
            ProcessarEventos();

            Assert.True(args.SuppressKeyPress);
            Assert.NotNull(repositorio.Registro);
            Assert.Empty(repositorio.Registro!.Itens);
            Assert.Contains("Nenhum resultado a informar", repositorio.Registro!.MensagemResumo, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Tela com definicoes e perfil resolvido. O construtor publico monta um repositorio real: sem
    /// injetar um fake a tela do Q cai na recuperacao de resultado persistido e exibe RECOVERY
    /// BLOQUEADO, o que nao e o cenario destes testes de layout.
    /// </summary>
    private static ProcessoResultadoApontamentoForm CriarFormNormal()
    {
        ResultadoApontamentoServico servico = new(new FakeResultadoApontamentoRepositorio(DefinicoesFixture()));
        ResultadoApontamentoController controller = new(servico);
        return new ProcessoResultadoApontamentoForm(ContextoComPerfil(), controller);
    }

    private static ProcessoResultadoApontamentoForm CriarFormSemDefinicao(out FakeResultadoApontamentoRepositorio repositorio)
    {
        repositorio = new FakeResultadoApontamentoRepositorio(Array.Empty<ResultadoApontamentoItem>());
        ResultadoApontamentoServico servico = new(repositorio);
        ResultadoApontamentoController controller = new(servico);
        return new ProcessoResultadoApontamentoForm(ContextoComPerfil(), controller);
    }

    private static void ExibirEProcessarLayout(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.ClientSize = new Size(1280, 720);
        form.Show();
        ProcessarEventos(700);
        form.PerformLayout();
        ProcessarEventos();
    }

    private static void ProcessarEventos(int milissegundos = 100)
    {
        DateTime limite = DateTime.UtcNow.AddMilliseconds(milissegundos);
        do
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        while (DateTime.UtcNow < limite);
    }

    private static void ExecutarEmSta(Action acao)
    {
        Exception? erro = null;
        Thread thread = new(() =>
        {
            try
            {
                acao();
            }
            catch (Exception ex)
            {
                erro = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (erro is not null)
        {
            throw erro;
        }
    }

    private static Control ObterControle(Control raiz, string nome)
        => raiz.Controls.Find(nome, true).Single();

    private static T ObterControle<T>(Control raiz, string nome)
        where T : Control
        => Assert.IsType<T>(ObterControle(raiz, nome));
    /// <summary>
    /// Contexto COM perfil de resultado resolvido. No desenho do Q o servico so chega a persistencia
    /// quando contexto.CodigoPerfilResultado tem valor; sem ele e fail-closed. Os testes que exercitam
    /// o repositorio precisam deste contexto, nao do ContextoPadrao.
    /// </summary>
    private static ContextoApontamentoProcesso ContextoComPerfil()
        => new()
        {
            CodigoApontamento = 123,
            NumeroOrdem = "1001732",
            ItemOrdem = "0001",
            Produto = "2000091",
            Sequencia = "000000",
            Operacao = "0070",
            DescricaoOperacao = "MISTURA DE RECEITA",
            CentroTrabalho = "MISTURA",
            TipoProcesso = TipoProcessoOperacao.ResultadoApontamento,
            CodigoPerfilResultado = 1,
            Usuario = "admin",
            Estacao = "TERMINAL_TESTE",
            IniciadoEm = new DateTime(2026, 7, 23, 8, 0, 0)
        };

    private static ContextoApontamentoProcesso ContextoPadrao()
        => new()
        {
            CodigoApontamento = 123,
            NumeroOrdem = "1001732",
            ItemOrdem = "0001",
            Produto = "2000091",
            Sequencia = "000000",
            Operacao = "0070",
            DescricaoOperacao = "MISTURA DE RECEITA",
            CentroTrabalho = "MISTURA",
            TipoProcesso = TipoProcessoOperacao.ResultadoApontamento,
            Usuario = "admin",
            Estacao = "TERMINAL_TESTE",
            IniciadoEm = new DateTime(2026, 7, 23, 8, 0, 0)
        };

    private static OrdemProducaoSap OrdemComOperacoes(params string[] operacoes)
        => new()
        {
            NumeroOrdem = "1001732",
            Operacoes = operacoes
                .Select((operacao, indice) => new OperacaoOrdemProducaoSap
                {
                    Sequencia = "000000",
                    Operacao = operacao,
                    Suboperacao = string.Empty,
                    OrderOperationInternalId = (indice + 1).ToString("0000")
                })
                .ToArray()
        };

    private static string ArquivosResultadoApontamento()
        => string.Join("\n---\n", new[]
        {
            LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs"),
            LerArquivoProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.Designer.cs"),
            LerArquivoProjeto("Controle", "Processo", "ResultadoApontamentoController.cs"),
            LerArquivoProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs"),
            LerArquivoProjeto("Modelo", "Processo", "ResultadoApontamentoItem.cs"),
            LerArquivoProjeto("Modelo", "Processo", "RegistroResultadoApontamento.cs")
        });

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto não encontrada.");
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') profundidade++;
            if (fonte[i] == '}') profundidade--;
            if (profundidade == 0) return fonte[inicio..(i + 1)];
        }

        throw new InvalidOperationException($"Fim do método não encontrado: {assinatura}");
    }

    private sealed class FakeResultadoApontamentoRepositorio : IResultadoApontamentoRepositorio
    {
        private readonly IReadOnlyList<ResultadoApontamentoItem> _definicoes;

        public FakeResultadoApontamentoRepositorio()
            : this(DefinicoesFixture())
        {
        }

        public FakeResultadoApontamentoRepositorio(IReadOnlyList<ResultadoApontamentoItem> definicoes)
        {
            _definicoes = definicoes;
        }

        public RegistroResultadoApontamento? Registro { get; private set; }

        public Task<long> InserirAsync(RegistroResultadoApontamento registro, CancellationToken cancellationToken)
        {
            Registro = registro;
            return Task.FromResult(987L);
        }

        public Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
            long codigoPerfilResultado,
            CancellationToken cancellationToken)
            => Task.FromResult(_definicoes);

        public Task<IReadOnlyList<ResultadoPersistidoApontamento>> ListarResultadosPersistidosDoApontamentoAsync(
            long codigoApontamento,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ResultadoPersistidoApontamento>>(Array.Empty<ResultadoPersistidoApontamento>());
    }

    private sealed class RepositorioResultadoApontamentoComFalha : IResultadoApontamentoRepositorio
    {
        public Task<long> InserirAsync(RegistroResultadoApontamento registro, CancellationToken cancellationToken)
            => throw new InvalidOperationException("falha fake sanitizada");

        public Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
            long codigoPerfilResultado,
            CancellationToken cancellationToken)
            => Task.FromResult(DefinicoesFixture());

        public Task<IReadOnlyList<ResultadoPersistidoApontamento>> ListarResultadosPersistidosDoApontamentoAsync(
            long codigoApontamento,
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("falha fake sanitizada");
    }
}






