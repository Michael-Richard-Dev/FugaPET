using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Servicos.Seguranca;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Correções finais do Controle de Apontamentos: permissão por ação, filtro de componentes pela
/// operação do apontamento, resultado tipado do Consumo, transição estrita de término e proteção
/// de fechamento durante a leitura.
/// </summary>
public sealed class ControleApontamentosCorrecoesFinaisTests
{
    private const string CodigoInicio = "000001001710005001";
    private const string CodigoTermino = "000001001710005002";
    private const string Usuario = "michael";
    private const string Estacao = "EST-01";

    // ================= §1 Permissões por ação =================

    [Fact]
    public async Task ComVisualizarMasSemIniciar_NaoCriaApontamento()
    {
        RepositorioFake repo = new();
        SapFake sap = new(OrdemComOperacoes("0050"));
        ProcessoControleApontamentosServico servico = Criar(sap, repo, AutorizacaoFake.SomenteVisualizar);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SemPermissaoAcao, r.Cenario);
        Assert.Contains("INICIAR", r.Mensagem, StringComparison.Ordinal);
        Assert.Empty(repo.Iniciados);
        // Bloqueia ANTES de consultar o SAP e antes de qualquer alteração de banco.
        Assert.Equal(0, sap.Consultas);
        // A negativa é auditada informando a ação negada.
        Assert.Contains("SEM_PERMISSAO_INICIAR", repo.Eventos);
    }

    [Fact]
    public async Task SemFinalizar_NaoConclui_EEstadoPermaneceAguardandoFinalizacao()
    {
        RepositorioFake repo = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.AguardandoFinalizacao)]
        };
        ProcessoControleApontamentosServico servico = Criar(
            new SapFake(OrdemComOperacoes("0050")), repo, AutorizacaoFake.SomenteVisualizar);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SemPermissaoAcao, r.Cenario);
        Assert.Contains("FINALIZAR", r.Mensagem, StringComparison.Ordinal);
        Assert.False(repo.Concluiu); // continua AGUARDANDO_FINALIZACAO
        Assert.Contains("SEM_PERMISSAO_FINALIZAR", repo.Eventos);
    }

    [Fact]
    public async Task ComIniciarEFinalizar_FluxoPermitido()
    {
        RepositorioFake repoInicio = new();
        ProcessoControleApontamentosServico inicio = Criar(
            new SapFake(OrdemComOperacoes("0050")), repoInicio, AutorizacaoFake.Completa);

        Assert.Equal(
            CenarioLeituraApontamento.SucessoInicio,
            (await inicio.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma)).Cenario);

        RepositorioFake repoTermino = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.AguardandoFinalizacao)]
        };
        ProcessoControleApontamentosServico termino = Criar(
            new SapFake(OrdemComOperacoes("0050")), repoTermino, AutorizacaoFake.Completa);

        Assert.Equal(
            CenarioLeituraApontamento.SucessoTermino,
            (await termino.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma)).Cenario);
        Assert.True(repoTermino.Concluiu);
    }

    [Fact]
    public async Task Autorizacao_SemEstrutura039_FallbackDeVisualizacaoValeMasNaoLiberaAcoes()
    {
        // 039 NÃO aplicado + matriz só com LEITURA_PRODUCAO/VISUALIZAR.
        ControleApontamentosAutorizacaoServico autorizacao = new(
            (modulo, rotina, acao) =>
                rotina == PermissoesSistema.Rotinas.LeituraProducao && acao == PermissoesSistema.Acoes.Visualizar,
            _ => Task.FromResult(false));

        Assert.True(await autorizacao.PodeVisualizarAsync()); // fallback transitório abre a tela
        // O fallback NUNCA concede ações críticas.
        Assert.False(autorizacao.PodeIniciar());
        Assert.False(autorizacao.PodeFinalizar());
    }

    [Fact]
    public async Task Autorizacao_ComEstrutura039_LeituraProducaoNaoAbreMaisOModulo()
    {
        // 039 aplicado: acabou o fallback. Só a rotina própria abre.
        ControleApontamentosAutorizacaoServico autorizacao = new(
            (modulo, rotina, acao) =>
                rotina == PermissoesSistema.Rotinas.LeituraProducao && acao == PermissoesSistema.Acoes.Visualizar,
            _ => Task.FromResult(true));

        Assert.False(await autorizacao.PodeVisualizarAsync());
    }

    [Fact]
    public async Task Autorizacao_ComPermissaoPropria_AbreMesmoSemAPermissaoAntiga()
    {
        // Usuário só com CONTROLE_APONTAMENTOS/VISUALIZAR (sem LEITURA_PRODUCAO), 039 aplicado.
        ControleApontamentosAutorizacaoServico autorizacao = new(
            (modulo, rotina, acao) =>
                modulo == PermissoesSistema.Modulos.ProcessoProducao
                && rotina == PermissoesSistema.Rotinas.ControleApontamentos
                && acao == PermissoesSistema.Acoes.Visualizar,
            _ => Task.FromResult(true));

        Assert.True(await autorizacao.PodeVisualizarAsync());
    }

    [Fact]
    public async Task Autorizacao_ComRotinaPropria_UsaAsAcoesProprias()
    {
        ControleApontamentosAutorizacaoServico autorizacao = new(
            (modulo, rotina, acao) =>
                modulo == PermissoesSistema.Modulos.ProcessoProducao
                && rotina == PermissoesSistema.Rotinas.ControleApontamentos
                && acao is PermissoesSistema.Acoes.Visualizar or PermissoesSistema.Acoes.Iniciar,
            _ => Task.FromResult(true));

        Assert.True(await autorizacao.PodeVisualizarAsync());
        Assert.True(autorizacao.PodeIniciar());
        Assert.False(autorizacao.PodeFinalizar()); // não concedida
    }

    [Fact]
    public async Task Autorizacao_SemNenhumaPermissao_NaoAbreEmNenhumCenario()
    {
        foreach (bool estruturaAplicada in new[] { false, true })
        {
            ControleApontamentosAutorizacaoServico autorizacao = new(
                (_, _, _) => false, _ => Task.FromResult(estruturaAplicada));

            Assert.False(await autorizacao.PodeVisualizarAsync());
        }
    }

    [Fact]
    public void PainelInicial_DeveUsarOServicoEAuditarComARotinaPropria()
    {
        string painel = LerArquivoProjeto("Tela", "PainelInicialForm.cs");
        string gate = ExtrairMetodo(painel, "private async Task<bool> PermiteAbrirControleApontamentosAsync()");

        // Usa o serviço específico e NÃO valida por LEITURA_PRODUCAO.
        Assert.Contains("controller.PodeVisualizarAsync()", gate, StringComparison.Ordinal);
        Assert.DoesNotContain("RotinaLeituraProducao", gate, StringComparison.Ordinal);
        // Auditoria com a rotina PRÓPRIA.
        Assert.Contains("PermissoesSistema.Rotinas.ControleApontamentos", gate, StringComparison.Ordinal);
        Assert.Contains("RegistrarAcessoNegadoSeguroAsync", gate, StringComparison.Ordinal);

        // A abertura preserva Hide/ShowDialog/Show/Activate e não usa mais o TODO antigo.
        string abertura = ExtrairMetodo(painel, "private async Task OpenControleApontamentosAsync()");
        Assert.Contains("await PermiteAbrirControleApontamentosAsync()", abertura, StringComparison.Ordinal);
        Assert.Contains("Hide();", abertura, StringComparison.Ordinal);
        Assert.Contains("form.ShowDialog(this);", abertura, StringComparison.Ordinal);
        Assert.Contains("Show();", abertura, StringComparison.Ordinal);
        Assert.Contains("Activate();", abertura, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO Permissões", abertura, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PermiteAbrirTelaAsync(\n            PermissoesSistema.Modulos.ProcessoProducao, RotinaLeituraProducao, \"Controle de Apontamentos\")",
            painel.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    // ================= §2 Filtro por operação no Consumo =================

    [Fact]
    public void ContextoDe0050_MostraSomenteComponentesDa0050()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("3500027", "0050", "000000"),
            Componente("1000089", "0060", "000000"),
            Componente("9999999", "0100", "000000")
        ];

        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000");

        Assert.Equal("3500027", Assert.Single(filtrados).CodigoMaterial);
    }

    [Fact]
    public void ComponenteDeOutraOperacao_NaoAparece()
    {
        List<ComponenteConsumoMaterial> componentes = [Componente("1000089", "0060", "000000")];

        Assert.Empty(ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000"));
    }

    [Fact]
    public void ComponenteSemOperacao_NaoEhUsadoComoFallback()
    {
        // Sem vínculo comprovado, não se adivinha: fica de fora.
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("3500027", operacao: "", sequencia: ""),
            Componente("1000089", operacao: "   ", sequencia: "")
        ];

        Assert.Empty(ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000"));
    }

    [Fact]
    public void FiltroPorOperacao_ToleraZerosAEsquerda()
    {
        List<ComponenteConsumoMaterial> componentes = [Componente("3500027", "50", "0")];

        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000");

        Assert.Single(filtrados);
    }

    [Fact]
    public void FiltroPorOperacao_SequenciaSoEhExigidaQuandoAmbosPossuem()
    {
        // Componente sem sequência com contexto sequenciado: não há vínculo comprovado.
        List<ComponenteConsumoMaterial> semSequencia = [Componente("3500027", "0050", "")];
        Assert.Empty(ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(semSequencia, "0050", "000000"));

        // Ambos com sequência e divergentes: não entra.
        List<ComponenteConsumoMaterial> sequenciaDiferente = [Componente("3500027", "0050", "000001")];
        Assert.Empty(ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(sequenciaDiferente, "0050", "000000"));
    }

    [Theory]
    [InlineData("0050", ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData("0050", ClassificacaoConsumoMaterial.Quimico)]
    [InlineData("0050", ClassificacaoConsumoMaterial.Indefinido)]
    [InlineData("0060", ClassificacaoConsumoMaterial.Quimico)]
    [InlineData("0060", ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData("0060", ClassificacaoConsumoMaterial.Indefinido)]
    public void ContextoComOperacaoAutoritativa_IncluiComponenteMesmoComClassificacaoInesperada(
        string operacao, ClassificacaoConsumoMaterial classificacao)
    {
        ComponenteConsumoMaterial componente = Componente("COMP", operacao, "000000");
        componente.ClassificacaoConsumo = classificacao;
        componente.TipoMaterialSap = classificacao == ClassificacaoConsumoMaterial.Quimico ? "HIBE" : "ROH";
        componente.GrupoMaterialSap = "GRUPO-TESTE";

        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao([componente], operacao, "000000");

        Assert.Equal("COMP", Assert.Single(filtrados).CodigoMaterial);
    }

    [Fact]
    public void FluxoComApontamento_UsaOperacaoAntesDeQualquerBloqueioPorModo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string consultar = ExtrairMetodo(form, "private async Task ConsultarOrdemProducaoAsync");
        int contexto = consultar.IndexOf("if (_contextoApontamento is not null)", StringComparison.Ordinal);
        int filtroOperacao = consultar.IndexOf("FiltrarComponentesPorOperacaoDoApontamento(resultado.Ordem.Componentes)", StringComparison.Ordinal);
        int productMasterOperacao = consultar.IndexOf("ObterMestresComponentesDaOperacaoAsync(", StringComparison.Ordinal);
        int enriquecimentoOperacao = consultar.IndexOf("EnriquecerComponentesParaDiagnosticoApontamento(componentesOperacao, resultado.Ordem)", StringComparison.Ordinal);
        int filtroModo = consultar.IndexOf("EnriquecerEClassificarComponentesDoModo(resultado.Ordem)", StringComparison.Ordinal);

        Assert.True(contexto >= 0, "Fluxo com contexto não encontrado.");
        Assert.True(filtroOperacao > contexto, "Fluxo com apontamento deve filtrar por operação.");
        Assert.True(productMasterOperacao > filtroOperacao, "Product Master deve consultar somente componentes já filtrados pela operação.");
        Assert.True(enriquecimentoOperacao > productMasterOperacao, "Diagnóstico deve enriquecer somente componentes da operação atual.");
        Assert.True(filtroModo > filtroOperacao, "Filtro por modo deve ficar somente no else manual.");
        Assert.Contains("ProductType e ProductGroup entram só depois", consultar, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticoComApontamento_InformaClassificacaoSemBloquear()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("ResultadoVinculo", form, StringComparison.Ordinal);
        Assert.Contains("INCLUIDO_POR_OPERACAO", form, StringComparison.Ordinal);
        Assert.Contains("ClassificacaoMaterial", form, StringComparison.Ordinal);
        Assert.Contains("ClassificacaoBloqueante: false", form, StringComparison.Ordinal);
    }
    [Fact]
    public void ContextoDe0060_MostraSomenteComponentesDa0060()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("3500027", "0050", "000000"),
            Componente("1000089", "0060", "000000"),
            Componente("9999999", "0100", "000000")
        ];

        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0060", "000000");

        Assert.Equal("1000089", Assert.Single(filtrados).CodigoMaterial);
    }

    [Fact]
    public void MesmaOpComComponentes0050E0060_CadaContextoRecebeSomenteSuaOperacao()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("MP", "0050", "000000"),
            Componente("QUIM", "0060", "000000")
        ];

        Assert.Equal("MP", Assert.Single(
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000")).CodigoMaterial);
        Assert.Equal("QUIM", Assert.Single(
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0060", "000000")).CodigoMaterial);
    }

    [Fact]
    public async Task Contexto0050_ConsultaProductMasterSomenteDosComponentesDaOperacao0050()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("MP", "0050", "000000"),
            Componente("QUIM", "0060", "000000")
        ];
        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000");
        List<string> consultados = [];

        await ProcessoConsumoMaterialForm.ObterMestresComponentesDaOperacaoAsync(
            filtrados,
            (codigos, _) =>
            {
                consultados.AddRange(codigos);
                return Task.FromResult(MapearMestres(codigos));
            });

        Assert.Equal(new[] { "MP" }, consultados);
    }

    [Fact]
    public async Task Contexto0060_ConsultaProductMasterSomenteDosComponentesDaOperacao0060()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("MP", "0050", "000000"),
            Componente("QUIM", "0060", "000000")
        ];
        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0060", "000000");
        List<string> consultados = [];

        await ProcessoConsumoMaterialForm.ObterMestresComponentesDaOperacaoAsync(
            filtrados,
            (codigos, _) =>
            {
                consultados.AddRange(codigos);
                return Task.FromResult(MapearMestres(codigos));
            });

        Assert.Equal(new[] { "QUIM" }, consultados);
    }

    [Fact]
    public async Task Contexto0050_NaoConsultaComponenteIrrelevanteQueFalhariaNoProductMaster()
    {
        List<ComponenteConsumoMaterial> componentes =
        [
            Componente("MP", "0050", "000000"),
            Componente("MATERIAL_COM_ERRO", "0060", "000000")
        ];
        IReadOnlyList<ComponenteConsumoMaterial> filtrados =
            ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(componentes, "0050", "000000");
        List<string> consultados = [];

        await ProcessoConsumoMaterialForm.ObterMestresComponentesDaOperacaoAsync(
            filtrados,
            (codigos, _) =>
            {
                List<string> lista = codigos.ToList();
                if (lista.Contains("MATERIAL_COM_ERRO", StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Product Master irrelevante não deveria ser consultado.");
                }

                consultados.AddRange(lista);
                return Task.FromResult(MapearMestres(lista));
            });

        Assert.Equal(new[] { "MP" }, consultados);
    }

    [Fact]
    public void FiltroPorOperacao_BloqueiaQuandoSomenteUmLadoTemSequencia()
    {
        List<ComponenteConsumoMaterial> componenteSemSequencia = [Componente("3500027", "0050", "")];
        List<ComponenteConsumoMaterial> contextoSemSequencia = [Componente("3500027", "0050", "000000")];

        Assert.Empty(ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(
            componenteSemSequencia, "0050", "000000"));
        Assert.Empty(ProcessoConsumoMaterialForm.FiltrarComponentesPorOperacao(
            contextoSemSequencia, "0050", ""));
    }

    [Fact]
    public void RepositorioConfiguracao_ComparaOperacaoIgnorandoZerosAEsquerda()
    {
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        Assert.Contains("ltrim(operacao_sap, '0')", repositorio, StringComparison.Ordinal);
        Assert.Contains("ltrim(@operacao_sap, '0')", repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("AND operacao_sap = @operacao_sap", repositorio, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticoComponenteSemOperacao_UsaMarcadorSemVinculoOperacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("SEM_VINCULO_OPERACAO", form, StringComparison.Ordinal);
        Assert.DoesNotContain("SEM_VINCULO\"", form, StringComparison.Ordinal);
    }
    [Fact]
    public void MensagemSemComponenteDaOperacao_OrientaVerificarOsCamposDeVinculo()
    {
        string mensagem = ProcessoConsumoMaterialForm.MontarMensagemSemComponenteDaOperacao("0050");

        Assert.Contains("Nenhum componente da OP foi vinculado à operação 0050.", mensagem, StringComparison.Ordinal);
        Assert.Contains("ManufacturingOrderOperation", mensagem, StringComparison.Ordinal);
        Assert.Contains("ManufacturingOrderSequence", mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void AberturaManualSemContexto_NaoAplicaFiltroPorOperacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string consultar = ExtrairMetodo(form, "private async Task ConsultarOrdemProducaoAsync");

        // O filtro por operação só roda quando há contexto de apontamento.
        Assert.Contains("if (_contextoApontamento is not null)", consultar, StringComparison.Ordinal);
        Assert.Contains("FiltrarComponentesPorOperacaoDoApontamento(resultado.Ordem.Componentes)", consultar, StringComparison.Ordinal);
        Assert.Contains("BloquearOrdemSemComponenteDaOperacao(resultado.Ordem, resultado.NumeroOrdem)", consultar, StringComparison.Ordinal);

        // Sem contexto o método de filtro devolve a lista original (fluxo manual intacto).
        string filtro = ExtrairMetodo(form, "private IReadOnlyList<ComponenteConsumoMaterial> FiltrarComponentesPorOperacaoDoApontamento");
        Assert.Contains("if (_contextoApontamento is null)", filtro, StringComparison.Ordinal);
        Assert.Contains("return componentes;", filtro, StringComparison.Ordinal);
    }

    // ================= §3/§4 Resultado tipado do Consumo =================

    [Fact]
    public void FalhaSapSemStatusHttp_NaoViraConcluidoLocalmente()
    {
        // Sem status HTTP o resultado é INDETERMINADO → DivergenciaSap (nunca conclusão local).
        ResultadoEnvioConsumoSap261 envio = ResultadoEnvioConsumoSap261.Falha("Timeout ao enviar.");

        ResultadoOrquestracaoConsumoApontamento r =
            ResultadoOrquestracaoConsumoApontamento.DoEnvio261(10, envio, "padrão");

        Assert.True(envio.ResultadoIndeterminado);
        Assert.Equal(ResultadoExecucaoProcessoApontamento.DivergenciaSap, r.Resultado);
        Assert.NotEqual(ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente, r.Resultado);
        Assert.False(r.ConfirmadoSap);
    }

    [Theory]
    [InlineData(408)] // timeout
    [InlineData(500)] // 5xx durante POST
    [InlineData(503)]
    [InlineData(201)] // 2xx sem documento
    public void FalhasIndeterminadas_ViramDivergenciaSap(int statusHttp)
    {
        ResultadoEnvioConsumoSap261 envio = ResultadoEnvioConsumoSap261.Falha("Falha.", statusHttp);

        ResultadoOrquestracaoConsumoApontamento r =
            ResultadoOrquestracaoConsumoApontamento.DoEnvio261(10, envio, "padrão");

        Assert.True(envio.ResultadoIndeterminado);
        Assert.Equal(ResultadoExecucaoProcessoApontamento.DivergenciaSap, r.Resultado);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    public void RejeicaoComprovadaDeNegocio_ViraErroSap(int statusHttp)
    {
        ResultadoEnvioConsumoSap261 envio = ResultadoEnvioConsumoSap261.Falha("Rejeitado pelo SAP.", statusHttp);

        ResultadoOrquestracaoConsumoApontamento r =
            ResultadoOrquestracaoConsumoApontamento.DoEnvio261(10, envio, "padrão");

        Assert.True(envio.RejeicaoComprovada);
        Assert.Equal(ResultadoExecucaoProcessoApontamento.ErroSap, r.Resultado);
    }

    [Fact]
    public void SucessoSap_ViraConfirmadoSap()
    {
        ResultadoEnvioConsumoSap261 envio = ResultadoEnvioConsumoSap261.Ok("5000001", "2026", 201, "corr");

        ResultadoOrquestracaoConsumoApontamento r =
            ResultadoOrquestracaoConsumoApontamento.DoEnvio261(10, envio, "padrão");

        Assert.Equal(ResultadoExecucaoProcessoApontamento.ConfirmadoSap, r.Resultado);
        Assert.True(r.ConfirmadoSap);
        Assert.Equal(10, r.CodigoLancamento);
    }

    [Fact]
    public void RotasQueNaoEnviam_NaoLiberamTermino()
    {
        // Misto / Bloqueado / Backflush → NaoConcluido.
        ResultadoOrquestracaoConsumoApontamento r =
            ResultadoOrquestracaoConsumoApontamento.NaoConcluido(10, "rota não envia");

        Assert.Equal(ResultadoExecucaoProcessoApontamento.NaoConcluido, r.Resultado);
        Assert.False(new ResultadoExecucaoProcesso(r.Resultado, r.CodigoLancamento, r.Mensagem, false)
            .AtividadeConcluida);
    }

    [Theory]
    [InlineData(ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente, true)]
    [InlineData(ResultadoExecucaoProcessoApontamento.ConfirmadoSap, true)]
    [InlineData(ResultadoExecucaoProcessoApontamento.DivergenciaSap, false)]
    [InlineData(ResultadoExecucaoProcessoApontamento.ErroSap, false)]
    [InlineData(ResultadoExecucaoProcessoApontamento.NaoConcluido, false)]
    public void SomenteConclusaoRealMarcaAguardandoFinalizacao(
        ResultadoExecucaoProcessoApontamento resultado, bool esperado)
        => Assert.Equal(
            esperado,
            new ResultadoExecucaoProcesso(resultado, 1, "m", false).AtividadeConcluida);

    [Fact]
    public void Orquestracao_NaoUsaMessageBoxIconComoRegraDeDominio()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string orquestrar = ExtrairMetodo(
            form, "private async Task<ResultadoOrquestracaoConsumoApontamento> OrquestrarEnvioAposConfirmarAsync");

        // A orquestração devolve tipo próprio e não decide nada por ícone.
        Assert.DoesNotContain("MessageBoxIcon", orquestrar, StringComparison.Ordinal);
        Assert.Contains("ResultadoOrquestracaoConsumoApontamento.DoEnvio261", orquestrar, StringComparison.Ordinal);
        Assert.Contains("ResultadoOrquestracaoConsumoApontamento.NaoConcluido", orquestrar, StringComparison.Ordinal);

        // O antigo acoplamento (icone == Information) não existe mais.
        Assert.DoesNotContain("icone == MessageBoxIcon.Information", form, StringComparison.Ordinal);
        Assert.DoesNotContain("_lancamentoComFalhaSap\n                            ?", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Backflush_MantemNaoConcluido_ComDecisaoDocumentada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string orquestrar = ExtrairMetodo(
            form, "private async Task<ResultadoOrquestracaoConsumoApontamento> OrquestrarEnvioAposConfirmarAsync");

        Assert.Contains("DECISÃO DOCUMENTADA", orquestrar, StringComparison.Ordinal);
        Assert.Contains("RotaEnvioConsumo.BackflushConfirmacao", orquestrar, StringComparison.Ordinal);
    }

    // ================= §5 Transição estrita =================

    [Fact]
    public void Repository_UpdateDeConclusao_NaoAceitaEmAndamento()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");

        // CONCLUIDA só pode vir de AGUARDANDO_FINALIZACAO.
        Assert.Contains("AND status = 'AGUARDANDO_FINALIZACAO'", repo, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AND status IN ('EM_ANDAMENTO', 'AGUARDANDO_FINALIZACAO')\n                    RETURNING terminado_em",
            repo.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Pacote039_ExigeResultadoValidoTambemEmConcluida()
    {
        string proposta = LerArquivoProjeto(
            "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA",
            "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql");

        Assert.Contains("status NOT IN ('AGUARDANDO_FINALIZACAO', 'CONCLUIDA')", proposta, StringComparison.Ordinal);
        Assert.Contains(
            "resultado_operacional IN ('ConcluidoLocalmente', 'ConfirmadoSap')", proposta, StringComparison.Ordinal);
        Assert.Contains("concluido_operacional_em IS NOT NULL", proposta, StringComparison.Ordinal);
    }

    // ================= §7 Pacote 039 MVP de permissões =================

    [Fact]
    public void Pacote039_DeveCriarSomenteAsTresAcoesComEfeitoFuncional()
    {
        string proposta = LerArquivoProjeto(
            "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA",
            "039_controle_apontamentos_DEV_PROPOSTA_GAIA.sql");
        string validacao = LerArquivoProjeto(
            "BancoDados", "001_incrementais", "039_controle_apontamentos_producao_GAIA",
            "039_controle_apontamentos_DEV_VALIDACAO_GAIA.sql");

        Assert.Contains("('VISUALIZAR'", proposta, StringComparison.Ordinal);
        Assert.Contains("('INICIAR'", proposta, StringComparison.Ordinal);
        Assert.Contains("('FINALIZAR'", proposta, StringComparison.Ordinal);

        // Ações sem efeito funcional NÃO são criadas nesta versão.
        foreach (string acao in new[] { "CONSULTAR_HISTORICO", "CANCELAR", "REABRIR", "IGNORAR_SEQUENCIA" })
        {
            Assert.DoesNotContain($"('{acao}'", proposta, StringComparison.Ordinal);
        }

        // E a validação recusa se alguém criar ações fora do MVP.
        Assert.Contains(
            "acao_permissao NOT IN ('VISUALIZAR','INICIAR','FINALIZAR')", validacao, StringComparison.Ordinal);
    }

    // ================= §6 Fechamento durante leitura =================

    [Fact]
    public void Fechamento_DeveSerBloqueadoDuranteLeitura()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoControleApontamentosForm.cs");

        // FormClosing cobre X, Alt+F4 e fechamento pelo Windows.
        string closing = ExtrairMetodo(form, "private void ProcessoControleApontamentosForm_FormClosing");
        Assert.Contains("if (_processandoLeitura)", closing, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", closing, StringComparison.Ordinal);
        Assert.Contains("MensagemAguardeLeitura", closing, StringComparison.Ordinal);

        // O X da tela também não fecha durante a leitura.
        string botaoFechar = ExtrairMetodo(form, "private void CloseWindowLabel_Click");
        Assert.Contains("if (_processandoLeitura)", botaoFechar, StringComparison.Ordinal);
        Assert.Contains("statusLabel.Text = MensagemAguardeLeitura;", botaoFechar, StringComparison.Ordinal);
        Assert.Contains("return;", botaoFechar, StringComparison.Ordinal);

        Assert.Contains("FormClosing += ProcessoControleApontamentosForm_FormClosing;", form, StringComparison.Ordinal);
        Assert.Contains(
            "MensagemAguardeLeitura = \"Aguarde a conclusão da leitura antes de sair.\"",
            form,
            StringComparison.Ordinal);
    }

    // ================= Apoio =================

    private static ProcessoControleApontamentosServico Criar(
        SapFake sap, RepositorioFake repo, IControleApontamentosAutorizacaoServico autorizacao)
        => new(sap, () => repo, null, autorizacao, new RoteiroManualFake());

    private static bool SempreConfirma(ConfirmacaoApontamento c) => true;

    private static ComponenteConsumoMaterial Componente(string codigo, string operacao, string sequencia)
        => new() { CodigoMaterial = codigo, Operacao = operacao, SequenciaOperacao = sequencia };

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

    private static OrdemProducaoSap OrdemComOperacoes(params string[] operacoes)
        => new()
        {
            NumeroOrdem = "1001710",
            TipoOrdem = "ZP01",
            MaterialProduzido = "2000091",
            Centro = "3007",
            Liberada = true,
            Itens = [new ItemOrdemProducaoSap { ItemOrdem = "0001", Material = "2000091" }],
            Operacoes = operacoes
                .Select(op => new OperacaoOrdemProducaoSap
                {
                    Operacao = op,
                    Sequencia = "000000",
                    Descricao = $"Operacao {op}",
                    CentroTrabalho = "CT01"
                })
                .ToList()
        };

    private static OperacaoProducaoApontamento Ativo(string status)
        => new()
        {
            CodigoApontamento = 42,
            NumeroOrdem = "1001710",
            Sequencia = "000000",
            Operacao = "0050",
            Suboperacao = string.Empty,
            UsuarioInicio = Usuario,
            EstacaoInicio = Estacao,
            IniciadoEm = DateTimeOffset.Now.AddHours(-1),
            Status = status,
            CodigoBarrasInicio = CodigoInicio,
            TipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima
        };

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

    private sealed class AutorizacaoFake : IControleApontamentosAutorizacaoServico
    {
        public static AutorizacaoFake Completa { get; } = new(true, true, true);
        public static AutorizacaoFake SomenteVisualizar { get; } = new(true, false, false);

        private readonly bool _visualizar;
        private readonly bool _iniciar;
        private readonly bool _finalizar;

        private AutorizacaoFake(bool visualizar, bool iniciar, bool finalizar)
        {
            _visualizar = visualizar;
            _iniciar = iniciar;
            _finalizar = finalizar;
        }

        public Task<bool> EstruturaControleApontamentosDisponivelAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> PodeVisualizarAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_visualizar);

        public bool PodeIniciar() => _iniciar;
        public bool PodeFinalizar() => _finalizar;
    }

    private sealed class RoteiroManualFake : IProductionRoutingSapServico
    {
        public Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
            OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
            => Task.FromResult<RoteiroProducaoSap?>(new RoteiroProducaoSap
            {
                BillOfOperationsGroup = "TESTE",
                BillOfOperationsVariant = "1",
                Operacoes = ordem.Operacoes
                    .Select(o => new OperacaoRoteiroSap
                    {
                        Operacao = o.Operacao,
                        CodigoTextoPadrao = "PP_FORM",
                        TextoPadraoObtido = true
                    })
                    .ToList()
            });
    }
    private sealed class SapFake : IProductionOrderSapServico
    {
        private readonly OrdemProducaoSap? _ordem;

        public SapFake(OrdemProducaoSap? ordem) => _ordem = ordem;

        public int Consultas { get; private set; }
        public bool EhSimulado => true;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
        {
            Consultas++;
            return Task.FromResult(_ordem is null
                ? ResultadoConsultaOrdemProducaoSap.NaoEncontrada()
                : ResultadoConsultaOrdemProducaoSap.Encontrada(_ordem));
        }
    }

    private sealed class RepositorioFake : IControleApontamentosRepositorio
    {
        public IReadOnlyList<OperacaoProducaoApontamento> AtivosPorOperacao { get; init; } = [];
        public List<OperacaoProducaoApontamento> Iniciados { get; } = [];
        public List<string> Eventos { get; } = [];
        public bool Concluiu { get; private set; }

        public Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoOperacaoAsync(
            string centro, string tipoOrdem, string sequencia, string operacao, string suboperacao,
            string centroTrabalho, CancellationToken cancellationToken = default)
            => Task.FromResult(new ResultadoConfiguracaoOperacao(
                new ConfiguracaoOperacaoProcesso
                {
                    CodigoConfiguracao = 1,
                    OperacaoSap = "0050",
                    TipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima,
                    TelaDestino = "ProcessoConsumoMaterialForm",
                    Ativo = true
                },
                false,
                1));

        public Task<IReadOnlyList<ConfiguracaoOperacaoProcesso>> ListarConfiguracoesAtivasAsync(
            string centro, string tipoOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConfiguracaoOperacaoProcesso>>([]);

        public Task<OperacaoProducaoApontamento?> ObterApontamentoAtivoAsync(
            string numeroOrdem, string sequencia, string operacao, string suboperacao,
            CancellationToken cancellationToken = default)
            => Task.FromResult<OperacaoProducaoApontamento?>(null);

        public Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosAtivosPorOperacaoAsync(
            string numeroOrdem, string operacao, CancellationToken cancellationToken = default)
            => Task.FromResult(AtivosPorOperacao);

        public Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosDaOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OperacaoProducaoApontamento>>([]);

        public Task<bool> CodigoJaUtilizadoAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<OperacaoProducaoApontamento?> TentarIniciarApontamentoAsync(
            OperacaoProducaoApontamento apontamento, CodigoBarrasOperacao codigo,
            CancellationToken cancellationToken = default)
        {
            Iniciados.Add(apontamento);
            apontamento.CodigoApontamento = 99;
            apontamento.IniciadoEm = DateTimeOffset.Now;
            return Task.FromResult<OperacaoProducaoApontamento?>(apontamento);
        }

        public Task<bool> TentarMarcarAguardandoFinalizacaoAsync(
            long codigoApontamento, ResultadoExecucaoProcesso resultado, string usuario, string estacao,
            CodigoBarrasOperacao codigo, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<DateTimeOffset?> TentarConcluirApontamentoAsync(
            long codigoApontamento, string usuarioTermino, string estacaoTermino,
            CodigoBarrasOperacao codigoTermino, string idempotencyKeyTermino,
            CancellationToken cancellationToken = default)
        {
            Concluiu = true;
            return Task.FromResult<DateTimeOffset?>(DateTimeOffset.Now);
        }

        public Task<bool> TentarCancelarApontamentoAsync(
            long codigoApontamento, string usuario, string estacao, CodigoBarrasOperacao codigo,
            string motivo, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task RegistrarEventoAsync(
            long? codigoApontamento, CodigoBarrasOperacao codigo, string usuario, string estacao,
            string statusAnterior, string statusNovo, string resultado, string mensagem,
            string correlationId, CancellationToken cancellationToken = default)
        {
            Eventos.Add(resultado);
            return Task.CompletedTask;
        }
    }
}

