using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Fluxo do Controle de Apontamentos: erros operacionais são RESULTADOS funcionais, nunca exceções.
/// Os códigos/OP usados são dados de teste do processo analisado.
/// </summary>
public sealed class ProcessoControleApontamentosServicoTests
{
    private const string CodigoInicio = "000001001710005001";
    private const string CodigoTermino = "000001001710005002";
    private const string CodigoInicio0060 = "000001001710006001";
    private const string Usuario = "michael";
    private const string Estacao = "EST-01";

    // ---------- Código / sessão ----------

    [Fact]
    public async Task CodigoInvalido_NaoConsultaSapNemBanco()
    {
        RepositorioFake repo = new();
        SapFake sap = new(OrdemComOperacoes("0050"));
        ProcessoControleApontamentosServico servico = Criar(sap, repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync("ABC", Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.CodigoInvalido, r.Cenario);
        Assert.Equal(0, sap.Consultas);
        Assert.Contains("CODIGO_INVALIDO", repo.Eventos); // auditado
    }

    [Fact]
    public async Task SemSessaoUsuario_NaoRegistraApontamento()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, "", Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SemSessaoUsuario, r.Cenario);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("SEM_SESSAO", repo.Eventos);
    }

    // ---------- OP / operação ----------

    [Fact]
    public async Task OpInexistente_DevolveOrdemNaoEncontradaEAudita()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(null), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OrdemNaoEncontrada, r.Cenario);
        Assert.Contains("OP_INEXISTENTE", repo.Eventos);
    }

    [Fact]
    public async Task OpNaoLiberada_BloqueiaEAudita()
    {
        RepositorioFake repo = new();
        OrdemProducaoSap ordem = OrdemComOperacoes("0050") with { Liberada = false };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(ordem), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OrdemNaoLiberada, r.Cenario);
        Assert.Contains("OP_NAO_LIBERADA", repo.Eventos);
    }

    [Fact]
    public async Task FalhaSap_NoInicio_DevolveResultadoFuncionalEAudita()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(null, lancar: true), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.FalhaConsultaSap, r.Cenario);
        Assert.Contains("FALHA_SAP", repo.Eventos);
    }

    [Fact]
    public async Task OperacaoInexistente_DevolveOperacaoNaoEncontrada()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0010")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoNaoEncontrada, r.Cenario);
        Assert.Contains("OPERACAO_INEXISTENTE", repo.Eventos);
    }

    [Fact]
    public async Task OperacaoAmbigua_NaoEscolheArbitrariamente_EBloqueia()
    {
        OrdemProducaoSap ordem = OrdemBase() with
        {
            Operacoes =
            [
                new OperacaoOrdemProducaoSap { Operacao = "0050", Sequencia = "000000", Descricao = "A" },
                new OperacaoOrdemProducaoSap { Operacao = "0050", Sequencia = "000001", Descricao = "B" }
            ]
        };
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(ordem), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoAmbigua, r.Cenario);
        Assert.Contains("mais de uma vez", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("OPERACAO_AMBIGUA", repo.Eventos);
    }

    // ---------- Estrutura / mapeamento / configuração ----------

    [Fact]
    public async Task EstruturaNaoAplicada_ConsultaOpEExibeOperacoesMasBloqueiaInicio()
    {
        RepositorioFake repo = new() { Estrutura = false };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.EstruturaNaoAplicada, r.Cenario);
        Assert.Equal(ProcessoControleApontamentosServico.MensagemEstruturaNaoAplicada, r.Mensagem);
        Assert.NotNull(r.Ordem);
        Assert.NotNull(r.Operacao);
        Assert.Empty(repo.Iniciados);
    }

    [Fact]
    public async Task SemConfiguracaoDeDestino_BloqueiaInicio()
    {
        RepositorioFake repo = new() { Configuracao = new ResultadoConfiguracaoOperacao(null, false, 0) };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.MapeamentoNaoConfigurado, r.Cenario);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("MAPEAMENTO_AUSENTE", repo.Eventos);
    }

    [Fact]
    public async Task ConfiguracaoAmbigua_BloqueiaEmVezDeEscolher()
    {
        RepositorioFake repo = new() { Configuracao = new ResultadoConfiguracaoOperacao(null, true, 2) };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.ConfiguracaoAmbigua, r.Cenario);
        Assert.Contains("mesma especificidade", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("CONFIGURACAO_AMBIGUA", repo.Eventos);
    }

    [Fact]
    public async Task Operacao0050_RoteiaParaConsumoMateriaPrima()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfiguracaoDestino("0050", TipoProcessoOperacao.ConsumoMateriaPrima)
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
        Assert.True(r.DeveAbrirDestino);
        Assert.Equal(TipoProcessoOperacao.ConsumoMateriaPrima, r.Contexto!.TipoProcesso);
        Assert.Equal(TipoProcessoOperacao.ConsumoMateriaPrima, Assert.Single(repo.Iniciados).TipoProcesso);
    }

    [Fact]
    public async Task Operacao0060_RoteiaParaConsumoQuimicos()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfiguracaoDestino("0060", TipoProcessoOperacao.ConsumoQuimicos)
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0060")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio0060, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
        Assert.True(r.DeveAbrirDestino);
        Assert.Equal(TipoProcessoOperacao.ConsumoQuimicos, r.Contexto!.TipoProcesso);
        Assert.Equal(TipoProcessoOperacao.ConsumoQuimicos, Assert.Single(repo.Iniciados).TipoProcesso);
    }

    [Fact]
    public void ResolverOperacao_Operacao50EquivaleA0050()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("0050");

        ResultadoResolucaoOperacao resolucao = ProcessoControleApontamentosServico.ResolverOperacao(ordem, "50");

        Assert.False(resolucao.Ambigua);
        Assert.Equal("0050", resolucao.Operacao!.Operacao);
    }

    [Fact]
    public void ResolverOperacao_Operacao60EquivaleA0060()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("0060");

        ResultadoResolucaoOperacao resolucao = ProcessoControleApontamentosServico.ResolverOperacao(ordem, "60");

        Assert.False(resolucao.Ambigua);
        Assert.Equal("0060", resolucao.Operacao!.Operacao);
    }
    [Fact]
    public async Task ConfiguracaoResolvida_DeveConsiderarCentroDeTrabalho()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        // O centro de trabalho da operação é levado à resolução da configuração.
        Assert.Equal("CT01", repo.CentroTrabalhoConsultado);
    }

    // ---------- Sequência ----------

    [Fact]
    public async Task OperacaoAnteriorNaoConcluida_BloqueiaInicio()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfigComExigencia(true),
            // A anterior (0040) existe, mas não há apontamento CONCLUIDA para ela.
            ApontamentosOrdem = []
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0040", "0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoAnteriorNaoConcluida, r.Cenario);
        Assert.Contains("0040", r.Mensagem, StringComparison.Ordinal); // informa QUAL operação falta
        Assert.Empty(repo.Iniciados);
        Assert.Contains("SEQUENCIA_BLOQUEADA", repo.Eventos);
    }

    [Fact]
    public async Task OperacaoAnteriorConcluida_LiberaInicio()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfigComExigencia(true),
            ApontamentosOrdem = [Concluido("000000", "0040")]
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0040", "0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
        Assert.Single(repo.Iniciados);
    }

    [Fact]
    public async Task SemExigenciaDeAnterior_NaoBloqueiaMesmoComAnteriorPendente()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfigComExigencia(false),
            ApontamentosOrdem = []
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0040", "0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
    }

    [Fact]
    public async Task Operacao0050_NaoExige0040QuandoConfiguracaoNaoExigeAnterior()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfiguracaoDestino(
                "0050",
                TipoProcessoOperacao.ConsumoMateriaPrima,
                exigeOperacaoAnterior: false),
            ApontamentosOrdem = []
        };
        ProcessoControleApontamentosServico servico = Criar(
            new SapFake(OrdemComOperacoes("0040", "0050", "0060")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
        Assert.Equal("0050", Assert.Single(repo.Iniciados).Operacao);
    }

    [Fact]
    public async Task Operacao0060_ExigindoAnteriorSem0050Concluida_Bloqueia()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfiguracaoDestino(
                "0060",
                TipoProcessoOperacao.ConsumoQuimicos,
                exigeOperacaoAnterior: true),
            ApontamentosOrdem = []
        };
        ProcessoControleApontamentosServico servico = Criar(
            new SapFake(OrdemComOperacoes("0050", "0060")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio0060, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoAnteriorNaoConcluida, r.Cenario);
        Assert.Contains("0050", r.Mensagem, StringComparison.Ordinal);
        Assert.Empty(repo.Iniciados);
    }

    [Fact]
    public async Task Operacao0060_ExigindoAnteriorCom0050Concluida_Inicia()
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfiguracaoDestino(
                "0060",
                TipoProcessoOperacao.ConsumoQuimicos,
                exigeOperacaoAnterior: true),
            ApontamentosOrdem = [Concluido("000000", "0050")]
        };
        ProcessoControleApontamentosServico servico = Criar(
            new SapFake(OrdemComOperacoes("0050", "0060")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio0060, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
        Assert.Equal("0060", Assert.Single(repo.Iniciados).Operacao);
    }

    [Theory]
    [InlineData(StatusApontamentoOperacao.EmAndamento)]
    [InlineData(StatusApontamentoOperacao.AguardandoFinalizacao)]
    public async Task Operacao0060_ExigindoAnteriorCom0050NaoConcluida_Bloqueia(string statusAnterior)
    {
        RepositorioFake repo = new()
        {
            Configuracao = ConfiguracaoDestino(
                "0060",
                TipoProcessoOperacao.ConsumoQuimicos,
                exigeOperacaoAnterior: true),
            ApontamentosOrdem = [Ativo(statusAnterior, Usuario)]
        };
        ProcessoControleApontamentosServico servico = Criar(
            new SapFake(OrdemComOperacoes("0050", "0060")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio0060, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoAnteriorNaoConcluida, r.Cenario);
        Assert.Contains("0050", r.Mensagem, StringComparison.Ordinal);
        Assert.Empty(repo.Iniciados);
    }

    [Fact]
    public void OrdenacaoTecnica_NaoUsaOrdemDeTextoPura()
    {
        // "0100" > "0050" numericamente; em texto puro "0100" viria antes.
        OrdemProducaoSap ordem = OrdemBase() with
        {
            Operacoes =
            [
                new OperacaoOrdemProducaoSap { Operacao = "0100", Sequencia = "000000" },
                new OperacaoOrdemProducaoSap { Operacao = "0050", Sequencia = "000000" }
            ]
        };

        IReadOnlyList<OperacaoOrdemProducaoSap> ordenadas =
            ProcessoControleApontamentosServico.OrdenarTecnicamente(ordem);

        Assert.Equal("0050", ordenadas[0].Operacao);
        Assert.Equal("0100", ordenadas[1].Operacao);

        OperacaoOrdemProducaoSap? anterior =
            ProcessoControleApontamentosServico.ObterOperacaoAnterior(ordem, ordenadas[1]);
        Assert.Equal("0050", anterior!.Operacao);
    }

    [Fact]
    public void OrdenacaoTecnica_UsaSequenciaAntesDaOperacao()
    {
        OrdemProducaoSap ordem = OrdemBase() with
        {
            Operacoes =
            [
                new OperacaoOrdemProducaoSap { Operacao = "0010", Sequencia = "000001" },
                new OperacaoOrdemProducaoSap { Operacao = "0090", Sequencia = "000000" }
            ]
        };

        IReadOnlyList<OperacaoOrdemProducaoSap> ordenadas =
            ProcessoControleApontamentosServico.OrdenarTecnicamente(ordem);

        // Sequência 000000 vem antes, mesmo com operação maior.
        Assert.Equal("000000", ordenadas[0].Sequencia);
        Assert.Equal("0090", ordenadas[0].Operacao);
    }

    // ---------- Início ----------

    [Fact]
    public async Task InicioAutorizado_RegistraEmAndamentoEDevolveContextoComHorarioDoBanco()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoInicio, r.Cenario);
        Assert.Single(repo.Iniciados);
        Assert.NotNull(r.Contexto);
        Assert.Equal("1001710", r.Contexto!.NumeroOrdem);
        Assert.Equal("0050", r.Contexto.Operacao);
        Assert.Equal(TipoProcessoOperacao.ConsumoMateriaPrima, r.Contexto.TipoProcesso);
        Assert.Equal(CodigoInicio, r.Contexto.CodigoBarrasInicio);
        // O horário vem do "banco" (RETURNING), não de DateTime.Now do cliente.
        Assert.Equal(RepositorioFake.HorarioBanco.LocalDateTime, r.Contexto.IniciadoEm);
    }

    [Fact]
    public async Task InicioRecusadoPeloUsuario_NaoRegistraEAudita()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio, Usuario, Estacao, NuncaConfirma);

        Assert.Equal(CenarioLeituraApontamento.ConfirmacaoPendente, r.Cenario);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("CONFIRMACAO_RECUSADA", repo.Eventos);
    }

    [Fact]
    public async Task InicioComCodigoJaUtilizado_Bloqueia()
    {
        RepositorioFake repo = new() { CodigoUtilizado = true };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.InicioDuplicado, r.Cenario);
        Assert.Contains("já foi utilizado", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INICIO_DUPLICADO", repo.Eventos);
    }

    [Fact]
    public async Task ClaimPerdido_UniqueViolation_ViraResultadoFuncionalNaoErroDeSuporte()
    {
        // O repository converte 23505 em null; o serviço traduz em cenário funcional.
        RepositorioFake repo = new() { ClaimFalha = true };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.InicioDuplicado, r.Cenario);
        Assert.Contains("Outra estação", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FALHA_CONCORRENTE", repo.Eventos);
    }

    // ---------- Retomada ----------

    [Fact]
    public async Task RetomadaDisponivel_MesmoUsuario_NaoCriaNovoApontamentoEPreservaInicio()
    {
        OperacaoProducaoApontamento ativo = Ativo(StatusApontamentoOperacao.EmAndamento, Usuario);
        RepositorioFake repo = new() { AtivoPorOperacao = ativo };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.RetomadaDisponivel, r.Cenario);
        Assert.True(r.DeveAbrirDestino);
        Assert.Empty(repo.Iniciados);                                  // NÃO cria novo apontamento
        Assert.Equal(ativo.CodigoApontamento, r.Contexto!.CodigoApontamento);
        Assert.Equal(ativo.IniciadoEm!.Value.LocalDateTime, r.Contexto.IniciadoEm); // início ORIGINAL preservado
        Assert.Equal(TipoProcessoOperacao.ConsumoMateriaPrima, r.Contexto.TipoProcesso);
        Assert.Contains("RETOMADA", repo.Eventos);                     // evento de retomada registrado
    }

    [Fact]
    public async Task RetomadaReconstroiContextoDoPersistido_MesmoAposReiniciarOServico()
    {
        // "Reinício": um serviço NOVO, sem memória, só com o que está no banco.
        OperacaoProducaoApontamento ativo = Ativo(StatusApontamentoOperacao.EmAndamento, Usuario);
        RepositorioFake repo = new() { AtivoPorOperacao = ativo };
        ProcessoControleApontamentosServico servicoNovo = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servicoNovo.ProcessarLeituraAsync(
            CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.RetomadaDisponivel, r.Cenario);
        Assert.NotNull(r.Contexto);
        Assert.Equal("1001710", r.Contexto!.NumeroOrdem);
        Assert.Equal(ativo.CodigoBarrasInicio, r.Contexto.CodigoBarrasInicio);
        Assert.Equal(ativo.UsuarioInicio, r.Contexto.Usuario);
        Assert.Equal(ativo.EstacaoInicio, r.Contexto.Estacao);
    }

    [Fact]
    public async Task RetomadaRecusada_NaoAbreDestino()
    {
        RepositorioFake repo = new() { AtivoPorOperacao = Ativo(StatusApontamentoOperacao.EmAndamento, Usuario) };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, NuncaConfirma);

        Assert.Equal(CenarioLeituraApontamento.ConfirmacaoPendente, r.Cenario);
        Assert.False(r.DeveAbrirDestino);
    }

    [Fact]
    public async Task OperacaoEmAndamentoPorOutroUsuario_Bloqueia()
    {
        RepositorioFake repo = new() { AtivoPorOperacao = Ativo(StatusApontamentoOperacao.EmAndamento, "outro") };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoEmAndamentoPorOutroUsuario, r.Cenario);
        Assert.False(r.DeveAbrirDestino);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("outro", r.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeituraDeInicioComAtividadeConcluida_OrientaLerTermino()
    {
        RepositorioFake repo = new()
        {
            AtivoPorOperacao = Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario)
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoJaAguardandoTermino, r.Cenario);
        Assert.False(r.DeveAbrirDestino); // não reabre a atividade
        Assert.Contains("TÉRMINO", r.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JA_AGUARDANDO_TERMINO", repo.Eventos);
    }

    // ---------- Término ----------

    [Fact]
    public async Task TerminoSemInicio_NaoCriaApontamento()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.TerminoSemInicio, r.Cenario);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("TERMINO_SEM_INICIO", repo.Eventos);
    }

    [Fact]
    public async Task TerminoComSapIndisponivel_ConcluiPeloApontamentoLocal()
    {
        // SAP lançando exceção: o término NÃO depende dele.
        RepositorioFake repo = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario)]
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(null, lancar: true), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoTermino, r.Cenario);
        Assert.True(repo.Concluiu);
    }

    [Fact]
    public async Task TerminoComOpAlteradaNoSapAposInicio_NaoPrendeOApontamento()
    {
        // OP voltou do SAP como NÃO liberada depois do início: o término local continua possível.
        RepositorioFake repo = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario)]
        };
        OrdemProducaoSap encerrada = OrdemComOperacoes("0050") with { Liberada = false, Confirmada = true };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(encerrada), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoTermino, r.Cenario);
        Assert.True(repo.Concluiu);
    }

    [Fact]
    public async Task TerminoComMaisDeUmApontamentoAtivo_BloqueiaPorAmbiguidade()
    {
        RepositorioFake repo = new()
        {
            AtivosPorOperacao =
            [
                Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario, codigo: 1),
                Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario, codigo: 2)
            ]
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.TerminoAmbiguo, r.Cenario);
        Assert.False(repo.Concluiu);
        Assert.Contains("TERMINO_AMBIGUO", repo.Eventos);
    }

    [Fact]
    public async Task TerminoComAtividadeNaoConcluida_NaoLibera()
    {
        RepositorioFake repo = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.EmAndamento, Usuario)]
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.TerminoNaoLiberado, r.Cenario);
        Assert.False(repo.Concluiu);
        Assert.Contains("TERMINO_NAO_LIBERADO", repo.Eventos);
    }

    [Fact]
    public async Task TerminoCorreto_ViraConcluidaComHorarioDoBanco()
    {
        RepositorioFake repo = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario)]
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.SucessoTermino, r.Cenario);
        Assert.True(repo.Concluiu);
        Assert.Equal(StatusApontamentoOperacao.Concluida, r.Apontamento!.Status);
        Assert.Equal(CodigoTermino, r.Apontamento.CodigoBarrasTermino);
        Assert.Equal(RepositorioFake.HorarioTermino, r.Apontamento.TerminadoEm);
        // Duração calculada a partir dos horários do BANCO.
        Assert.Equal(RepositorioFake.HorarioTermino - RepositorioFake.HorarioBanco, r.Apontamento.Duracao);
    }

    [Fact]
    public async Task TerminoDuplicado_QuandoOutraEstacaoJaConcluiu_Bloqueia()
    {
        RepositorioFake repo = new()
        {
            AtivosPorOperacao = [Ativo(StatusApontamentoOperacao.AguardandoFinalizacao, Usuario)],
            ConclusaoFalha = true
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.TerminoDuplicado, r.Cenario);
        Assert.Contains("FALHA_CONCORRENTE", repo.Eventos);
    }

    [Fact]
    public async Task TerminoComCodigoJaUtilizado_Bloqueia()
    {
        RepositorioFake repo = new() { CodigoUtilizado = true };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(CodigoTermino, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.TerminoDuplicado, r.Cenario);
        Assert.Contains("TERMINO_DUPLICADO", repo.Eventos);
    }

    // ---------- Conclusão operacional ----------

    [Theory]
    [InlineData(ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente, true)]
    [InlineData(ResultadoExecucaoProcessoApontamento.ConfirmadoSap, true)]
    [InlineData(ResultadoExecucaoProcessoApontamento.NaoConcluido, false)]   // fechar não conclui
    [InlineData(ResultadoExecucaoProcessoApontamento.ErroSap, false)]        // erro não libera término
    [InlineData(ResultadoExecucaoProcessoApontamento.DivergenciaSap, false)] // divergência não libera
    [InlineData(ResultadoExecucaoProcessoApontamento.Cancelado, false)]
    public async Task ConclusaoOperacional_SoAvancaQuandoAtividadeConcluida(
        ResultadoExecucaoProcessoApontamento execucao, bool esperadoAvancar)
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        bool avancou = await servico.RegistrarConclusaoOperacionalAsync(
            42, new ResultadoExecucaoProcesso(execucao, 777, "msg", false), Usuario, Estacao, CodigoValido());

        Assert.Equal(esperadoAvancar, avancou);
        Assert.Equal(esperadoAvancar, repo.MarcouAguardando);
    }

    [Fact]
    public async Task ConclusaoOperacional_PersisteVinculoComCodigoLancamentoDoConsumo()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        await servico.RegistrarConclusaoOperacionalAsync(
            42,
            new ResultadoExecucaoProcesso(
                ResultadoExecucaoProcessoApontamento.ConfirmadoSap, 12345, "Consumo enviado.", true),
            Usuario, Estacao, CodigoValido());

        Assert.NotNull(repo.ResultadoRegistrado);
        Assert.Equal(12345, repo.ResultadoRegistrado!.CodigoRegistroProcesso);
        Assert.True(repo.ResultadoRegistrado.IndicadorConfirmadoSap);
        Assert.Equal("Consumo enviado.", repo.ResultadoRegistrado.Mensagem);
    }

    [Fact]
    public async Task ConclusaoOperacional_ZeroLinhasAtualizadas_NaoRetornaTrue()
    {
        // Apontamento inexistente / estado incompatível / update concorrente → 0 linhas.
        RepositorioFake repo = new() { MarcarAguardandoFalha = true };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        bool avancou = await servico.RegistrarConclusaoOperacionalAsync(
            42,
            new ResultadoExecucaoProcesso(ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente, 1, "", false),
            Usuario, Estacao, CodigoValido());

        Assert.False(avancou);
    }

    [Fact]
    public async Task ConclusaoOperacional_BancoIndisponivel_NaoQuebraENaoAvanca()
    {
        RepositorioFake repo = new() { LancarNoMarcar = true };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0050")), repo);

        bool avancou = await servico.RegistrarConclusaoOperacionalAsync(
            42,
            new ResultadoExecucaoProcesso(ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente, 1, "", false),
            Usuario, Estacao, CodigoValido());

        Assert.False(avancou);
    }

    // ---------- Resolução de operação ----------

    [Fact]
    public void ResolverOperacao_ToleraZerosSemConverterParaNumero()
    {
        OrdemProducaoSap ordem = OrdemBase() with
        {
            Operacoes = [new OperacaoOrdemProducaoSap { Operacao = "50", Sequencia = "000000" }]
        };

        ResultadoResolucaoOperacao r = ProcessoControleApontamentosServico.ResolverOperacao(ordem, "0050");

        Assert.False(r.Ambigua);
        Assert.NotNull(r.Operacao);
    }

    [Fact]
    public void ResolverOperacao_Encontra0050QuandoSapRetorna0050()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("0050");

        ResultadoResolucaoOperacao r = ProcessoControleApontamentosServico.ResolverOperacao(ordem, "0050");

        Assert.NotNull(r.Operacao);
        Assert.Equal("0050", r.Operacao!.Operacao);
    }

    [Fact]
    public void ResolverOperacao_Encontra0050QuandoCodigoLidoVem50()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("0050");

        ResultadoResolucaoOperacao r = ProcessoControleApontamentosServico.ResolverOperacao(ordem, "50");

        Assert.NotNull(r.Operacao);
        Assert.Equal("0050", r.Operacao!.Operacao);
    }

    [Fact]
    public void ResolverOperacao_Encontra50QuandoCodigoLidoVem0050()
    {
        OrdemProducaoSap ordem = OrdemComOperacoes("50");

        ResultadoResolucaoOperacao r = ProcessoControleApontamentosServico.ResolverOperacao(ordem, "0050");

        Assert.NotNull(r.Operacao);
        Assert.Equal("50", r.Operacao!.Operacao);
    }

    [Fact]
    public async Task OperacoesSemCodigoMapeado_DevolveFalhaMapeamentoENaoCriaApontamento()
    {
        RepositorioFake repo = new();
        OrdemProducaoSap ordem = OrdemBase() with
        {
            Operacoes = Enumerable.Range(1, 10)
                .Select(_ => new OperacaoOrdemProducaoSap { Operacao = "", Sequencia = "", Descricao = "" })
                .ToList()
        };
        ProcessoControleApontamentosServico servico = Criar(new SapFake(ordem), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.FalhaMapeamentoOperacoesSap, r.Cenario);
        Assert.NotEqual(CenarioLeituraApontamento.OperacaoNaoEncontrada, r.Cenario);
        Assert.Contains("ManufacturingOrderOperation", r.Mensagem, StringComparison.Ordinal);
        Assert.Empty(repo.Iniciados);
        Assert.Contains("FALHA_MAPEAMENTO_OPERACOES_SAP", repo.Eventos);
    }

    [Fact]
    public async Task OperacoesValidasSemCodigoLido_DevolveOperacaoNaoEncontrada()
    {
        RepositorioFake repo = new();
        ProcessoControleApontamentosServico servico = Criar(new SapFake(OrdemComOperacoes("0010", "0060")), repo);

        ResultadoLeituraApontamento r = await servico.ProcessarLeituraAsync(
            CodigoInicio, Usuario, Estacao, SempreConfirma);

        Assert.Equal(CenarioLeituraApontamento.OperacaoNaoEncontrada, r.Cenario);
        Assert.Contains("Operação 0050 não existe", r.Mensagem, StringComparison.Ordinal);
        Assert.Empty(repo.Iniciados);
    }

    // ---------- Apoio ----------

    // Autorização permissiva: estes testes exercitam o FLUXO, não o gate de permissão
    // (coberto em ControleApontamentosCorrecoesFinaisTests).
    private static ProcessoControleApontamentosServico Criar(SapFake sap, RepositorioFake repo)
        => new(sap, () => repo, null, new AutorizacaoPermissiva(), new RoteiroManualFake());

    // GATE 048-E: roteiro que marca TODAS as operações da OP como manuais (PP_FORM). Estes testes exercitam
    // o FLUXO/sequência, não a regra PP_FORM (coberta em ControleApontamentosMarcadorPpFormTests).
    private sealed class RoteiroManualFake : IProductionRoutingSapServico
    {
        public Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
            OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
            => Task.FromResult<RoteiroProducaoSap?>(new RoteiroProducaoSap
            {
                Operacoes = ordem.Operacoes
                    .Where(o => !string.IsNullOrWhiteSpace(o.Operacao))
                    .Select(o => new OperacaoRoteiroSap
                    {
                        Operacao = o.Operacao,
                        CodigoTextoPadrao = "PP_FORM",
                        TextoPadraoObtido = true
                    })
                    .ToList()
            });
    }

    private sealed class AutorizacaoPermissiva : IControleApontamentosAutorizacaoServico
    {
        public Task<bool> EstruturaControleApontamentosDisponivelAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> PodeVisualizarAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public bool PodeIniciar() => true;
        public bool PodeFinalizar() => true;
    }

    private static CodigoBarrasOperacao CodigoValido()
        => new CodigoBarrasOperacaoServico().Interpretar(CodigoInicio);

    private static bool SempreConfirma(ConfirmacaoApontamento c) => true;

    private static bool NuncaConfirma(ConfirmacaoApontamento c) => false;

    private static ResultadoConfiguracaoOperacao ConfiguracaoDestino(
        string operacao,
        string tipoProcesso,
        bool exigeOperacaoAnterior = false)
        => new(
            new ConfiguracaoOperacaoProcesso
            {
                CodigoConfiguracao = 1,
                OperacaoSap = operacao,
                TipoProcesso = tipoProcesso,
                TelaDestino = "ProcessoConsumoMaterialForm",
                ExigeOperacaoAnterior = exigeOperacaoAnterior,
                Ativo = true
            },
            false,
            1);
    private static ResultadoConfiguracaoOperacao ConfigComExigencia(bool exige)
        => new(
            new ConfiguracaoOperacaoProcesso
            {
                CodigoConfiguracao = 1,
                OperacaoSap = "0050",
                TipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima,
                TelaDestino = "ProcessoConsumoMaterialForm",
                ExigeOperacaoAnterior = exige,
                Ativo = true
            },
            false,
            1);

    private static OrdemProducaoSap OrdemBase()
        => new()
        {
            NumeroOrdem = "1001710",
            TipoOrdem = "ZP01",
            MaterialProduzido = "2000091",
            Centro = "3007",
            Liberada = true,
            Itens = [new ItemOrdemProducaoSap { ItemOrdem = "0001", Material = "2000091", Lote = "L1" }]
        };

    private static OrdemProducaoSap OrdemComOperacoes(params string[] operacoes)
        => OrdemBase() with
        {
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

    private static OperacaoProducaoApontamento Ativo(string status, string usuario, long codigo = 42)
        => new()
        {
            CodigoApontamento = codigo,
            NumeroOrdem = "1001710",
            Sequencia = "000000",
            Operacao = "0050",
            Suboperacao = string.Empty,
            UsuarioInicio = usuario,
            EstacaoInicio = "EST-ORIGINAL",
            IniciadoEm = RepositorioFake.HorarioBanco,
            Status = status,
            CodigoBarrasInicio = CodigoInicio,
            TipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima,
            TelaDestino = "ProcessoConsumoMaterialForm",
            CorrelationId = "corr"
        };

    private static OperacaoProducaoApontamento Concluido(string sequencia, string operacao)
        => new()
        {
            CodigoApontamento = 1,
            NumeroOrdem = "1001710",
            Sequencia = sequencia,
            Operacao = operacao,
            Suboperacao = string.Empty,
            Status = StatusApontamentoOperacao.Concluida
        };

    private sealed class SapFake : IProductionOrderSapServico
    {
        private readonly OrdemProducaoSap? _ordem;
        private readonly bool _lancar;

        public SapFake(OrdemProducaoSap? ordem, bool lancar = false)
        {
            _ordem = ordem;
            _lancar = lancar;
        }

        public int Consultas { get; private set; }
        public bool EhSimulado => true;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
        {
            Consultas++;
            if (_lancar)
            {
                throw new InvalidOperationException("SAP indisponível (teste).");
            }

            return Task.FromResult(_ordem is null
                ? ResultadoConsultaOrdemProducaoSap.NaoEncontrada()
                : ResultadoConsultaOrdemProducaoSap.Encontrada(_ordem));
        }
    }

    private sealed class RepositorioFake : IControleApontamentosRepositorio
    {
        public static readonly DateTimeOffset HorarioBanco = new(2026, 7, 16, 8, 0, 0, TimeSpan.FromHours(-3));
        public static readonly DateTimeOffset HorarioTermino = new(2026, 7, 16, 10, 30, 0, TimeSpan.FromHours(-3));

        public bool Estrutura { get; init; } = true;
        public bool CodigoUtilizado { get; init; }
        public bool ClaimFalha { get; init; }
        public bool ConclusaoFalha { get; init; }
        public bool MarcarAguardandoFalha { get; init; }
        public bool LancarNoMarcar { get; init; }

        /// <summary>Ativo por sequência+operação (caminho do INÍCIO).</summary>
        public OperacaoProducaoApontamento? AtivoPorOperacao { get; init; }

        /// <summary>Ativos por OP+operação (caminho do TÉRMINO).</summary>
        public IReadOnlyList<OperacaoProducaoApontamento> AtivosPorOperacao { get; init; } = [];

        public IReadOnlyList<OperacaoProducaoApontamento> ApontamentosOrdem { get; init; } = [];

        public ResultadoConfiguracaoOperacao Configuracao { get; init; } = new(
            new ConfiguracaoOperacaoProcesso
            {
                CodigoConfiguracao = 1,
                OperacaoSap = "0050",
                TipoProcesso = TipoProcessoOperacao.ConsumoMateriaPrima,
                TelaDestino = "ProcessoConsumoMaterialForm",
                Ativo = true
            },
            false,
            1);

        public List<OperacaoProducaoApontamento> Iniciados { get; } = [];
        public List<string> Eventos { get; } = [];
        public bool Concluiu { get; private set; }
        public bool MarcouAguardando { get; private set; }
        public ResultadoExecucaoProcesso? ResultadoRegistrado { get; private set; }
        public string CentroTrabalhoConsultado { get; private set; } = string.Empty;

        public Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Estrutura);

        public Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoOperacaoAsync(
            string centro, string tipoOrdem, string sequencia, string operacao, string suboperacao,
            string centroTrabalho, CancellationToken cancellationToken = default)
        {
            CentroTrabalhoConsultado = centroTrabalho;
            return Task.FromResult(Configuracao);
        }

        public Task<IReadOnlyList<ConfiguracaoOperacaoProcesso>> ListarConfiguracoesAtivasAsync(
            string centro, string tipoOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConfiguracaoOperacaoProcesso>>(
                Configuracao.Configuracao is null ? [] : [Configuracao.Configuracao]);

        public Task<OperacaoProducaoApontamento?> ObterApontamentoAtivoAsync(
            string numeroOrdem, string sequencia, string operacao, string suboperacao,
            CancellationToken cancellationToken = default)
            => Task.FromResult(AtivoPorOperacao);

        public Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosAtivosPorOperacaoAsync(
            string numeroOrdem, string operacao, CancellationToken cancellationToken = default)
            => Task.FromResult(AtivosPorOperacao);

        public Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosDaOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ApontamentosOrdem);

        public Task<bool> CodigoJaUtilizadoAsync(string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.FromResult(CodigoUtilizado);

        public Task<OperacaoProducaoApontamento?> TentarIniciarApontamentoAsync(
            OperacaoProducaoApontamento apontamento, CodigoBarrasOperacao codigo,
            CancellationToken cancellationToken = default)
        {
            if (ClaimFalha)
            {
                return Task.FromResult<OperacaoProducaoApontamento?>(null);
            }

            Iniciados.Add(apontamento);
            apontamento.CodigoApontamento = 99;
            apontamento.IniciadoEm = HorarioBanco; // horário vindo do "banco"
            return Task.FromResult<OperacaoProducaoApontamento?>(apontamento);
        }

        public Task<bool> TentarMarcarAguardandoFinalizacaoAsync(
            long codigoApontamento, ResultadoExecucaoProcesso resultado, string usuario, string estacao,
            CodigoBarrasOperacao codigo, CancellationToken cancellationToken = default)
        {
            if (LancarNoMarcar)
            {
                throw new InvalidOperationException("Banco indisponível (teste).");
            }

            if (MarcarAguardandoFalha)
            {
                return Task.FromResult(false);
            }

            MarcouAguardando = true;
            ResultadoRegistrado = resultado;
            return Task.FromResult(true);
        }

        public Task<DateTimeOffset?> TentarConcluirApontamentoAsync(
            long codigoApontamento, string usuarioTermino, string estacaoTermino,
            CodigoBarrasOperacao codigoTermino, string idempotencyKeyTermino,
            CancellationToken cancellationToken = default)
        {
            if (ConclusaoFalha)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }

            Concluiu = true;
            return Task.FromResult<DateTimeOffset?>(HorarioTermino);
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
