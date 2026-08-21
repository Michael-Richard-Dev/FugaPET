using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Entrada;

public sealed class EntradaProdutoLotesIdempotenciaContratosTests
{
    private static readonly Guid C1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid C2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid C3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // ---------------------------------------------------------------------------------------------------
    // §3 — Classificador PURO das correlações (sem banco).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void ClassificarCorrelacoes_Nenhuma_QuandoNadaEncontrado()
    {
        EntradaProdutoRepositorio.ResultadoClassificacaoCorrelacoesEntradaLotes resultado =
            EntradaProdutoRepositorio.ClassificarCorrelacoes([C1, C2], []);

        Assert.Equal(EntradaProdutoRepositorio.ClassificacaoCorrelacoesEntradaLotes.NenhumaExistente, resultado.Classificacao);
        Assert.Equal(2, resultado.QuantidadeSolicitada);
        Assert.Equal(0, resultado.QuantidadeEncontrada);
        Assert.Null(resultado.CodigoLancamentoUnico);
    }

    [Fact]
    public void ClassificarCorrelacoes_TodasEmUm_ResolveLancamentoUnico()
    {
        EntradaProdutoRepositorio.ResultadoClassificacaoCorrelacoesEntradaLotes resultado =
            EntradaProdutoRepositorio.ClassificarCorrelacoes([C1, C2], [(C1, 700), (C2, 700)]);

        Assert.Equal(EntradaProdutoRepositorio.ClassificacaoCorrelacoesEntradaLotes.TodasExistentes, resultado.Classificacao);
        Assert.Equal(2, resultado.QuantidadeEncontrada);
        Assert.Equal(700, resultado.CodigoLancamentoUnico);
        Assert.Equal([700], resultado.CodigosLancamentoDistintos);
    }

    [Fact]
    public void ClassificarCorrelacoes_Parcial_QuandoParteEncontrada()
    {
        EntradaProdutoRepositorio.ResultadoClassificacaoCorrelacoesEntradaLotes resultado =
            EntradaProdutoRepositorio.ClassificarCorrelacoes([C1, C2, C3], [(C1, 700)]);

        Assert.Equal(EntradaProdutoRepositorio.ClassificacaoCorrelacoesEntradaLotes.ParcialmenteExistente, resultado.Classificacao);
        Assert.Equal(3, resultado.QuantidadeSolicitada);
        Assert.Equal(1, resultado.QuantidadeEncontrada);
        Assert.Null(resultado.CodigoLancamentoUnico);
    }

    [Fact]
    public void ClassificarCorrelacoes_TodasEmDois_MarcaLancamentosDivergentes()
    {
        EntradaProdutoRepositorio.ResultadoClassificacaoCorrelacoesEntradaLotes resultado =
            EntradaProdutoRepositorio.ClassificarCorrelacoes([C1, C2], [(C1, 700), (C2, 800)]);

        Assert.Equal(EntradaProdutoRepositorio.ClassificacaoCorrelacoesEntradaLotes.LancamentosDivergentes, resultado.Classificacao);
        Assert.Null(resultado.CodigoLancamentoUnico);
        Assert.Equal([700, 800], resultado.CodigosLancamentoDistintos);
    }

    [Fact]
    public void ClassificarCorrelacoes_ResultadoDuplicado_NaoAlteraContagem()
    {
        EntradaProdutoRepositorio.ResultadoClassificacaoCorrelacoesEntradaLotes resultado =
            EntradaProdutoRepositorio.ClassificarCorrelacoes([C1, C2], [(C1, 700), (C1, 700), (C2, 700), (C2, 700)]);

        Assert.Equal(EntradaProdutoRepositorio.ClassificacaoCorrelacoesEntradaLotes.TodasExistentes, resultado.Classificacao);
        Assert.Equal(2, resultado.QuantidadeEncontrada);
        Assert.Equal(700, resultado.CodigoLancamentoUnico);
    }

    // ---------------------------------------------------------------------------------------------------
    // §5 — Classificador PURO do conflito 23505 (SqlState + constraint), executável sem banco.
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void EhConflitoCorrelationId_23505ComConstraintCorreta_EhVerdadeiro()
        => Assert.True(EntradaProdutoRepositorio.EhConflitoCorrelationId("23505", "uq_entrada_lote_correlation_id"));

    [Fact]
    public void EhConflitoCorrelationId_23505ComOutraConstraint_EhFalso()
        => Assert.False(EntradaProdutoRepositorio.EhConflitoCorrelationId("23505", "outra_constraint"));

    [Fact]
    public void EhConflitoCorrelationId_OutroSqlStateComConstraintCorreta_EhFalso()
        => Assert.False(EntradaProdutoRepositorio.EhConflitoCorrelationId("23503", "uq_entrada_lote_correlation_id"));

    [Fact]
    public void EhConflitoCorrelationId_Nulos_EhFalso()
        => Assert.False(EntradaProdutoRepositorio.EhConflitoCorrelationId(null, null));

    [Fact]
    public void Resultado_CriarPreservaInsertOnlyENovoCriarRecuperadoMarcaOrigem()
    {
        Dictionary<string, long> itens = new(StringComparer.Ordinal) { ["00010"] = 10 };
        Dictionary<Guid, long> lotes = new() { [Guid.NewGuid()] = 20 };
        Dictionary<Guid, long> pesagens = new() { [Guid.NewGuid()] = 30 };
        Dictionary<Guid, decimal> pesos = new() { [lotes.Keys.Single()] = 5m };

        ResultadoPersistenciaEntradaComLotes inserido =
            ResultadoPersistenciaEntradaComLotes.Criar(1, itens, lotes, pesagens, pesos);
        ResultadoPersistenciaEntradaComLotes recuperado =
            ResultadoPersistenciaEntradaComLotes.CriarRecuperado(1, itens, lotes, pesagens, pesos);

        Assert.False(inserido.PersistenciaRecuperada);
        Assert.True(recuperado.PersistenciaRecuperada);
        Assert.Equal(inserido.CodigoLancamento, recuperado.CodigoLancamento);
        Assert.Equal(inserido.CodigosItensPorNumeroItemSap, recuperado.CodigosItensPorNumeroItemSap);
        Assert.Equal(inserido.CodigosLotesPorCodigoLocal, recuperado.CodigosLotesPorCodigoLocal);
        Assert.Equal(inserido.CodigosPesagensPorCodigoLocal, recuperado.CodigosPesagensPorCodigoLocal);
    }

    [Fact]
    public void Repository_DevePreservarInsertOnlyECriarFluxoSeparadoIdempotente()
    {
        string repo = LerProjeto("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");

        Assert.Contains("public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarLancamentoComLotesAsync", repo, StringComparison.Ordinal);
        Assert.Contains("public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarLancamentoComLotesAsync", repo, StringComparison.Ordinal);
        Assert.Contains("ClassificacaoCorrelacoesEntradaLotes.NenhumaExistente", repo, StringComparison.Ordinal);
        Assert.Contains("ClassificacaoCorrelacoesEntradaLotes.TodasExistentes", repo, StringComparison.Ordinal);
        Assert.Contains("ClassificacaoCorrelacoesEntradaLotes.ParcialmenteExistente", repo, StringComparison.Ordinal);
        Assert.Contains("A persistência por lotes está inconsistente", repo, StringComparison.Ordinal);
        Assert.Contains("A correlation_id informada já pertence a uma árvore de entrada diferente", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_EtapaALocalizaLancamentoPelasCorrelacoes()
    {
        string repo = LerProjeto("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");
        string etapaA = ExtrairMetodo(repo, "private async Task<ResultadoClassificacaoCorrelacoesEntradaLotes> ClassificarCorrelacoesComLancamentoAsync");

        Assert.Contains("NpgsqlDbType.Array | NpgsqlDbType.Uuid", etapaA, StringComparison.Ordinal);
        Assert.Contains("lote.correlation_id = ANY(@correlations)", etapaA, StringComparison.Ordinal);
        // Localiza o lançamento via join lote → item (não infere pelo primeiro registro).
        Assert.Contains("item.codigo_entrada_produto_lancamento", etapaA, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", etapaA, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repository_EtapaBCarregaArvoreCompletaPeloCodigoLancamento()
    {
        string repo = LerProjeto("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");
        string etapaB = ExtrairMetodo(repo, "private async Task<EntradaProdutoLancamentoPersistidoComLotes> ConsultarArvoreCompletaPorLancamentoAsync");

        // Carrega pelo codigo_entrada_produto_lancamento, não filtra externamente pelos itens das correlações.
        Assert.Contains("codigo_entrada_produto_lancamento = @codigo_lancamento", etapaB, StringComparison.Ordinal);
        Assert.DoesNotContain("correlation_id = ANY", etapaB, StringComparison.Ordinal);
        // JOIN de todos os itens; LEFT JOIN de lotes e pesagens (item sem lote continua na árvore).
        Assert.Contains("JOIN entrada_produto_item item", etapaB, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN entrada_produto_lote lote", etapaB, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN entrada_produto_pesagem pesagem", etapaB, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT *", etapaB, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repository_DeveTratarCorridaSomenteNaConstraintCorrelationId()
    {
        string repo = LerProjeto("AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs");
        // A checagem 23505 vive no overload PURO (string?, string?); o wrapper apenas delega.
        string classificador = ExtrairMetodo(repo, "internal static bool EhConflitoCorrelationId(string? sqlState");
        string corrida = ExtrairMetodo(repo, "private async Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarAposCorridaAsync");

        Assert.Contains("PostgresErrorCodes.UniqueViolation", classificador, StringComparison.Ordinal);
        Assert.Contains("uq_entrada_lote_correlation_id", repo, StringComparison.Ordinal);
        Assert.Contains("catch (PostgresException ex) when (EhConflitoCorrelationId(ex))", corrida, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (PostgresException", repo.Replace("catch (PostgresException ex) when (EhConflitoCorrelationId(ex))", string.Empty), StringComparison.Ordinal);
    }

    [Fact]
    public void Service_DeveReusarPreparacaoEDelegarAoMetodoNovo()
    {
        string service = LerProjeto("Servicos", "Operacao", "EntradaProdutoServico.cs");
        string metodoNovo = ExtrairMetodo(service, "public async Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarLancamentoComLotesAsync");
        string preparacao = ExtrairMetodo(service, "private async Task<(EntradaProdutoLancamentoComLotesPersistencia Snapshot, ContextoAuditoriaEntradaLotes Contexto)>");

        Assert.Contains("PrepararLancamentoComLotesAsync", metodoNovo, StringComparison.Ordinal);
        Assert.Contains("_repositorio.RegistrarOuRecuperarLancamentoComLotesAsync", metodoNovo, StringComparison.Ordinal);
        Assert.Contains("ValidadorEntradaProdutoArvoreLotes.Validar", preparacao, StringComparison.Ordinal);
        Assert.Contains("AutorizacaoEntradaProdutoServico.PossuiPermissao", preparacao, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.PesoManual", preparacao, StringComparison.Ordinal);
        Assert.Contains("ValidarItemComLotesAsync", preparacao, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_DeveDelegarAoServiceENaoAcessarRepositorio()
    {
        string controller = LerProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string metodo = ExtrairMetodo(controller, "public Task<ResultadoPersistenciaEntradaComLotes> RegistrarOuRecuperarLancamentoComLotesAsync");

        Assert.Contains("EntradaProduto.RegistrarOuRecuperarLancamentoComLotesAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoRepositorio", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void FormPrincipal_DeveConectarAoFluxoIdempotenteViaSeamDoController()
    {
        // Fase 4G: o botão Parar passa a gravar localmente pelo fluxo idempotente. A Form conecta via SEAM
        // que delega ao Controller (fronteira de persistência) e NUNCA acessa o Repository diretamente.
        string form = LerProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("_registrarOuRecuperarLancamentoComLotes", form, StringComparison.Ordinal);
        Assert.Contains("_controller.RegistrarOuRecuperarLancamentoComLotesAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoRepositorio", form, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // §16 — auditoria sanitizada do conflito + mapeamento para a exceção operacional (sem banco).
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Service_ConflitoIdempotencia_AuditaSanitizadoELancaExcecaoGenerica()
    {
        AuditoriaSpy spy = new();
        EntradaProdutoServico servico = CriarServicoSomenteAuditoria(spy);

        Guid correlation = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
        ConflitoPersistenciaEntradaLotesException conflito = new(
            "A correlation_id informada já pertence a uma árvore de entrada diferente.",
            "Correlações pertencem a 2 lançamentos distintos: 700, 800.",
            correlation,
            "00020");

        ErroOperacionalEsperadoException mapeada =
            await servico.MapearConflitoIdempotenciaAsync(conflito, default);

        // Auditoria chamada, ação identifica a idempotência da Entrada.
        (string acao, string mensagem, string? tela) = Assert.Single(spy.Erros);
        Assert.Equal("ENTRADA_LOTES_IDEMPOTENCIA_DIVERGENTE", acao);
        Assert.Contains("lançamentos distintos", mensagem, StringComparison.Ordinal);
        Assert.Contains(correlation.ToString(), mensagem, StringComparison.Ordinal);
        Assert.Contains("00020", mensagem, StringComparison.Ordinal);
        Assert.Equal("Entrada de Produto", tela);

        // Mensagem pública genérica, conflito preservado como InnerException.
        Assert.Equal("A correlation_id informada já pertence a uma árvore de entrada diferente.", mapeada.Message);
        Assert.Same(conflito, mapeada.InnerException);
    }

    [Fact]
    public async Task Service_FalhaDeAuditoria_NaoLiberaOperacaoEAindaLancaExcecao()
    {
        AuditoriaSpy spy = new() { Falhar = true };
        EntradaProdutoServico servico = CriarServicoSomenteAuditoria(spy);

        ConflitoPersistenciaEntradaLotesException conflito = new(
            "A correlation_id informada já pertence a uma árvore de entrada diferente.",
            "Árvore persistida difere da árvore informada.");

        ErroOperacionalEsperadoException mapeada =
            await servico.MapearConflitoIdempotenciaAsync(conflito, default);

        Assert.True(spy.Tentou);
        Assert.Same(conflito, mapeada.InnerException);
        Assert.Equal(conflito.MensagemUsuario, mapeada.Message);
    }

    private static EntradaProdutoServico CriarServicoSomenteAuditoria(AuditoriaServico auditoria)
        => new(
            repositorio: null!,
            itemRepositorio: null!,
            taraRepositorio: null!,
            balancaRepositorio: null,
            autorizacaoCentroDeposito: null!,
            auditoria: auditoria);

    private sealed class AuditoriaSpy : AuditoriaServico
    {
        public List<(string Acao, string Mensagem, string? Tela)> Erros { get; } = [];
        public bool Falhar { get; init; }
        public bool Tentou { get; private set; }

        public AuditoriaSpy() : base(null!) { }

        public override Task RegistrarErroAsync(string acao, string mensagem, string? tela = null, CancellationToken cancellationToken = default)
        {
            Tentou = true;
            if (Falhar)
            {
                throw new InvalidOperationException("Falha simulada de auditoria.");
            }

            Erros.Add((acao, mensagem, tela));
            return Task.CompletedTask;
        }
    }

    private static string LerProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");
        int proximo = fonte.IndexOf("\n    public ", inicio + assinatura.Length, StringComparison.Ordinal);
        int privado = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        int interno = fonte.IndexOf("\n    internal ", inicio + assinatura.Length, StringComparison.Ordinal);
        int fim = new[] { proximo, privado, interno }.Where(i => i > inicio).DefaultIfEmpty(fonte.Length).Min();
        return fonte[inicio..fim];
    }

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
