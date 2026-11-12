using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Processo;
using Npgsql;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 050 (GAIA 041) — persistência do Resultado do Apontamento no contrato físico canônico:
/// cabeçalho sem numero_ordem/sequencia/operacao/suboperacao/atualizado_em; item com codigo_definicao +
/// obrigatorio (snapshot) e sem atualizado_em; header-only preservado; erro Postgres não vira "não configurada".
/// </summary>
public sealed class ResultadoApontamentoPersistencia041CodeFixTests
{
    // ---------- Contrato físico do INSERT (source, para pegar drift de shape) ----------

    private static string RepositorioFonte()
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && raiz is not null; i++)
        {
            foreach (string candidato in new[]
            {
                Path.Combine(raiz, "AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs"),
                Path.Combine(raiz, "FugaPet_HML", "AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs")
            })
            {
                if (File.Exists(candidato))
                {
                    return File.ReadAllText(candidato);
                }
            }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException("ResultadoApontamentoRepositorio.cs não encontrado.");
    }

    [Fact] // R1: cabeçalho usa o contrato 041 (colunas exatas).
    public void R1_Cabecalho_Contrato041()
    {
        string repo = RepositorioFonte();
        Assert.Contains("INSERT INTO operacao_resultado_registro", repo, StringComparison.Ordinal);
        Assert.Contains("codigo_apontamento, usuario, estacao, registrado_em, mensagem_resumo, criado_em", repo, StringComparison.Ordinal);
    }

    [Fact] // R2: cabeçalho NÃO referencia colunas redundantes nem atualizado_em.
    public void R2_Cabecalho_SemColunasRedundantes()
    {
        string repo = RepositorioFonte();
        Assert.DoesNotContain("numero_ordem", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("@numero_ordem", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("@sequencia", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("@operacao", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("@suboperacao", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("atualizado_em", repo, StringComparison.Ordinal); // R5 também: removido de ambos INSERTs
    }

    [Fact] // R3/R4/R5: item persiste codigo_definicao + obrigatorio; sem atualizado_em.
    public void R3_R4_R5_Item_Contrato041()
    {
        string repo = RepositorioFonte();
        Assert.Contains("INSERT INTO operacao_resultado_registro_item", repo, StringComparison.Ordinal);
        Assert.Contains("codigo_resultado, codigo_definicao, codigo_item, tipo, medida, referencia,", repo, StringComparison.Ordinal);
        Assert.Contains("resultado, ordem_exibicao, obrigatorio, criado_em", repo, StringComparison.Ordinal);
        Assert.Contains("@codigo_definicao", repo, StringComparison.Ordinal);
        Assert.Contains("ParametroBooleano(\"@obrigatorio\"", repo, StringComparison.Ordinal);
    }

    [Fact] // R7 (source): codigo_definicao vem do item (definição carregada), não hardcodado.
    public void R7_CodigoDefinicao_NaoHardcode()
    {
        string repo = RepositorioFonte();
        Assert.Contains("ParametroLongo(\"@codigo_definicao\", item.CodigoDefinicao)", repo, StringComparison.Ordinal);
        Assert.Contains("CodigoDefinicao = leitor.GetInt64(0)", repo, StringComparison.Ordinal); // carregado do SELECT
    }

    [Fact] // R16 (source): cabeçalho + itens na MESMA transação (rollback atômico).
    public void R16_TransacaoAtomica()
    {
        string repo = RepositorioFonte();
        Assert.Contains("ExecutarEmTransacaoAuditavelAsync", repo, StringComparison.Ordinal);
        Assert.Contains("new(sqlRegistro, conexao, transacao)", repo, StringComparison.Ordinal);
        Assert.Contains("new(sqlItem, conexao, transacao)", repo, StringComparison.Ordinal);
    }

    // ---------- Comportamento do serviço (fake repo) ----------

    [Fact] // R6/R7: 0070 — 1 definição → 1 header + 1 item; codigo_definicao propagado.
    public async Task R6_R7_Op0070_UmHeaderUmItem()
    {
        var repo = new FakeRepo();
        var servico = new ResultadoApontamentoServico(repo);
        var itens = new[]
        {
            new ResultadoApontamentoItem
            {
                CodigoDefinicao = 1, CodigoItem = "TEMPO_OPERACAO", Tipo = "TEMPO", Medida = "MINUTO",
                Referencia = 5.000m, OrdemExibicao = 1, Obrigatorio = true, Resultado = 5m
            }
        };

        ResultadoOperacao r = await servico.RegistrarAsync(Contexto(perfil: 10L), itens);

        Assert.True(r.Sucesso);
        Assert.NotNull(repo.UltimoRegistro);
        Assert.Single(repo.UltimoRegistro!.Itens);
        Assert.Equal(1, repo.UltimoRegistro.Itens[0].CodigoDefinicao); // veio da definição, não hardcode
        Assert.True(repo.UltimoRegistro.Itens[0].Obrigatorio);        // R4/R9 snapshot
        Assert.Equal(5.000m, repo.UltimoRegistro.Itens[0].Referencia); // R8 snapshot
        Assert.Equal(12, repo.UltimoRegistro.CodigoApontamento);
    }

    [Theory] // R10/R11/R12/R13: perfil ativo + zero definições → 1 header + 0 itens + sucesso (0090/0100/0105).
    [InlineData(90L)]
    [InlineData(100L)]
    [InlineData(105L)]
    public async Task R10_HeaderOnly_PerfilAtivoSemDefinicoes(long perfil)
    {
        var repo = new FakeRepo();
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoOperacao r = await servico.RegistrarAsync(Contexto(perfil: perfil), Array.Empty<ResultadoApontamentoItem>());

        Assert.True(r.Sucesso);
        Assert.NotNull(repo.UltimoRegistro);
        Assert.Empty(repo.UltimoRegistro!.Itens);           // 0 itens, sem placeholder
        Assert.Equal(1, repo.Insercoes);                    // 1 cabeçalho criado
    }

    [Fact] // R14: perfil AUSENTE + zero itens → fail-closed (não grava header-only).
    public async Task R14_PerfilAusente_FailClosed()
    {
        var repo = new FakeRepo();
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoOperacao r = await servico.RegistrarAsync(Contexto(perfil: null), Array.Empty<ResultadoApontamentoItem>());

        Assert.False(r.Sucesso);
        Assert.Equal(0, repo.Insercoes);
    }

    [Fact] // R17: erro Postgres REAL (não schema-absent) NÃO vira "não configurada".
    public async Task R17_ErroPostgresReal_NaoViraNaoConfigurada()
    {
        var repo = new FakeRepo { ExcecaoAoInserir = new PostgresException("unique", "ERROR", "ERROR", "23505") };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoOperacao r = await servico.RegistrarAsync(
            Contexto(perfil: 10L),
            [new ResultadoApontamentoItem { CodigoDefinicao = 1, CodigoItem = "X", Tipo = "T", Medida = "M", Referencia = 1m, OrdemExibicao = 1, Obrigatorio = true, Resultado = 1m }]);

        Assert.False(r.Sucesso);
        Assert.Equal(ResultadoApontamentoServico.MensagemFalhaTecnicaPersistencia, r.Mensagem);
        Assert.NotEqual(ResultadoApontamentoServico.MensagemPersistenciaNaoConfigurada, r.Mensagem);
    }

    [Fact] // R17 (complemento): estrutura REALMENTE ausente (42P01) → "não configurada".
    public async Task R17b_EstruturaAusente_ViraNaoConfigurada()
    {
        var repo = new FakeRepo { ExcecaoAoInserir = new PostgresException("undefined", "ERROR", "ERROR", "42P01") };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoOperacao r = await servico.RegistrarAsync(
            Contexto(perfil: 10L),
            [new ResultadoApontamentoItem { CodigoDefinicao = 1, CodigoItem = "X", Tipo = "T", Medida = "M", Referencia = 1m, OrdemExibicao = 1, Obrigatorio = true, Resultado = 1m }]);

        Assert.False(r.Sucesso);
        Assert.Equal(ResultadoApontamentoServico.MensagemPersistenciaNaoConfigurada, r.Mensagem);
    }

    [Fact]
    public async Task R19_RecoverySemResultado_MantemEntradaNormal()
    {
        var repo = new FakeRepo { ResultadosPersistidos = [] };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: 10L, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            DefinicoesPadrao());

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Ausente, r.Estado);
        Assert.Equal(0, repo.Insercoes);
    }

    [Fact]
    public async Task R20_RecoveryUmResultadoValido_NaoInsereNovoHeaderItem()
    {
        var repo = new FakeRepo
        {
            ResultadosPersistidos =
            [
                new ResultadoPersistidoApontamento
                {
                    CodigoResultado = 701,
                    CodigoApontamento = 12,
                    MensagemResumo = "Resultado registrado",
                    Itens = [ComResultado(DefinicoesPadrao()[0], 5m)]
                }
            ]
        };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: 10L, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            DefinicoesPadrao());

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Recuperado, r.Estado);
        Assert.Equal(701, r.Resultado!.CodigoResultado);
        Assert.Equal(0, repo.Insercoes);
    }

    [Fact]
    public async Task R21_RecoveryMaisDeUmResultado_FailClosed()
    {
        var repo = new FakeRepo
        {
            ResultadosPersistidos =
            [
                new ResultadoPersistidoApontamento { CodigoResultado = 701, CodigoApontamento = 12 },
                new ResultadoPersistidoApontamento { CodigoResultado = 702, CodigoApontamento = 12 }
            ]
        };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: 10L, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            DefinicoesPadrao());

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Falha, r.Estado);
    }

    [Fact]
    public async Task R22_RecoveryHeaderOnly_Preservado()
    {
        var repo = new FakeRepo
        {
            ResultadosPersistidos =
            [
                new ResultadoPersistidoApontamento
                {
                    CodigoResultado = 703,
                    CodigoApontamento = 12,
                    MensagemResumo = ResultadoApontamentoServico.MensagemNenhumResultadoAInformar,
                    Itens = []
                }
            ]
        };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: 90L, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            []);

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Recuperado, r.Estado);
        Assert.Empty(r.Resultado!.Itens);
    }

    [Fact]
    public async Task R23_RecoveryPerfilAusente_FailClosed()
    {
        var repo = new FakeRepo
        {
            ResultadosPersistidos =
            [
                new ResultadoPersistidoApontamento { CodigoResultado = 704, CodigoApontamento = 12, Itens = [] }
            ]
        };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: null, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            []);

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Falha, r.Estado);
    }

    [Fact]
    public async Task R24_RecoveryDefinicaoEstranha_FailClosed()
    {
        var estranho = new ResultadoApontamentoItem
        {
            CodigoDefinicao = 99, CodigoItem = "OUTRO", Tipo = "OUTRO", Medida = "UN",
            Referencia = 1m, OrdemExibicao = 1, Obrigatorio = true, Resultado = 1m
        };
        var repo = new FakeRepo
        {
            ResultadosPersistidos =
            [
                new ResultadoPersistidoApontamento { CodigoResultado = 705, CodigoApontamento = 12, Itens = [estranho] }
            ]
        };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: 10L, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            DefinicoesPadrao());

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Falha, r.Estado);
    }

    [Fact]
    public async Task R25_RecoveryObrigatorioSemResultado_FailClosed()
    {
        var repo = new FakeRepo
        {
            ResultadosPersistidos =
            [
                new ResultadoPersistidoApontamento
                {
                    CodigoResultado = 706,
                    CodigoApontamento = 12,
                    Itens = [ComResultado(DefinicoesPadrao()[0], null)]
                }
            ]
        };
        var servico = new ResultadoApontamentoServico(repo);

        ResultadoApontamentoPersistidoRecovery r = await servico.ObterResultadoPersistidoDoApontamentoAsync(
            Contexto(perfil: 10L, tipoProcesso: TipoProcessoOperacao.ResultadoApontamento),
            DefinicoesPadrao());

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Falha, r.Estado);
    }

    [Fact]
    public void R26_Form_VerificaResultadoPersistidoAntesDePermitirGravacao()
    {
        string form = FonteForm();

        Assert.Contains("ObterResultadoPersistidoDoApontamentoAsync", form, StringComparison.Ordinal);
        Assert.Contains("AplicarResultadoPersistido", form, StringComparison.Ordinal);
        Assert.Contains("RESULTADO JÁ REGISTRADO", form, StringComparison.Ordinal);
        Assert.Contains("Use FINALIZAR para reconciliar o resultado persistido sem nova gravação.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void R27_Form_FinalizarRecovery_DevolveResultadoExistenteSemRegistrarAsync()
    {
        string form = FonteForm();
        string trechoFinalizar = form[
            form.IndexOf("private void FinalizarResultadoPersistido", StringComparison.Ordinal)..];

        Assert.Contains("ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente", trechoFinalizar, StringComparison.Ordinal);
        Assert.Contains("_resultadoPersistido.CodigoResultado", trechoFinalizar, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrarAsync", trechoFinalizar, StringComparison.Ordinal);
        Assert.Contains("if (ResultadoPersistidoAtivo)", form, StringComparison.Ordinal);
    }

    [Fact] // classificação direta (sanitização): SQLSTATE preservado, sem Message.
    public void Classificador_EhEstruturaAusente_E_Sanitiza()
    {
        Assert.True(ResultadoApontamentoServico.EhEstruturaAusente(new PostgresException("x", "ERROR", "ERROR", "42P01")));
        Assert.True(ResultadoApontamentoServico.EhEstruturaAusente(new PostgresException("x", "ERROR", "ERROR", "42703")));
        Assert.False(ResultadoApontamentoServico.EhEstruturaAusente(new PostgresException("x", "ERROR", "ERROR", "23505")));
        Assert.False(ResultadoApontamentoServico.EhEstruturaAusente(new InvalidOperationException("boom")));

        string diag = ResultadoApontamentoServico.DiagnosticoSanitizado(new PostgresException("segredo Host=x;Password=z", "ERROR", "ERROR", "23505"));
        Assert.Equal("PostgresException SQLSTATE=23505", diag);
        Assert.DoesNotContain("Password", diag, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // R15/R18 (source): erro ao carregar definições não vira header-only; nenhuma dependência SAP.
    public void R15_R18_Form_CargaFalha_NaoHeaderOnly_SemSap()
    {
        string form = FonteForm();
        // Header-only só é permitido quando as definições foram carregadas COM perfil resolvido.
        Assert.Contains("_definicoesCarregadasComPerfilResolvido = false;", form, StringComparison.Ordinal);
        string servico = FonteServico();
        Assert.DoesNotContain("EnviarConsumoSap261Async", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", servico, StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private static ContextoApontamentoProcesso Contexto(long? perfil, string tipoProcesso = TipoProcessoOperacao.ResultadoApontamento)
        => new()
        {
            CodigoApontamento = 12,
            NumeroOrdem = "1002101",
            Operacao = "0070",
            Sequencia = "0",
            TipoProcesso = tipoProcesso,
            Usuario = "op",
            Estacao = "EST",
            CodigoPerfilResultado = perfil
        };

    private static IReadOnlyList<ResultadoApontamentoItem> DefinicoesPadrao()
        =>
        [
            new ResultadoApontamentoItem
            {
                CodigoDefinicao = 1,
                CodigoItem = "TEMPO_OPERACAO",
                Tipo = "TEMPO",
                Medida = "MINUTO",
                Referencia = 5.000m,
                OrdemExibicao = 1,
                Obrigatorio = true
            }
        ];

    private static ResultadoApontamentoItem ComResultado(ResultadoApontamentoItem item, decimal? resultado)
        => new()
        {
            CodigoDefinicao = item.CodigoDefinicao,
            CodigoItem = item.CodigoItem,
            Tipo = item.Tipo,
            Medida = item.Medida,
            Referencia = item.Referencia,
            Resultado = resultado,
            OrdemExibicao = item.OrdemExibicao,
            Obrigatorio = item.Obrigatorio
        };

    private static string FonteForm() => LerProjeto("Tela", "Processo", "ProcessoResultadoApontamentoForm.cs");
    private static string FonteServico() => LerProjeto("Servicos", "Processo", "ResultadoApontamentoServico.cs");

    private static string LerProjeto(params string[] partes)
    {
        string raiz = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && raiz is not null; i++)
        {
            foreach (string candidato in new[] { Path.Combine(raiz, Path.Combine(partes)), Path.Combine(raiz, "FugaPet_HML", Path.Combine(partes)) })
            {
                if (File.Exists(candidato))
                {
                    return File.ReadAllText(candidato);
                }
            }

            raiz = Directory.GetParent(raiz)?.FullName!;
        }

        throw new FileNotFoundException($"Fonte não encontrada: {string.Join('/', partes)}");
    }

    private sealed class FakeRepo : IResultadoApontamentoRepositorio
    {
        public RegistroResultadoApontamento? UltimoRegistro { get; private set; }
        public int Insercoes { get; private set; }
        public Exception? ExcecaoAoInserir { get; init; }
        public IReadOnlyList<ResultadoApontamentoItem> Definicoes { get; init; } = [];
        public IReadOnlyList<ResultadoPersistidoApontamento> ResultadosPersistidos { get; init; } = [];

        public Task<long> InserirAsync(RegistroResultadoApontamento registro, CancellationToken cancellationToken)
        {
            if (ExcecaoAoInserir is not null)
            {
                throw ExcecaoAoInserir;
            }

            UltimoRegistro = registro;
            Insercoes++;
            return Task.FromResult(500L);
        }

        public Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(long codigoPerfilResultado, CancellationToken cancellationToken)
            => Task.FromResult(Definicoes);

        public Task<IReadOnlyList<ResultadoPersistidoApontamento>> ListarResultadosPersistidosDoApontamentoAsync(
            long codigoApontamento,
            CancellationToken cancellationToken)
            => Task.FromResult(ResultadosPersistidos);
    }
}
