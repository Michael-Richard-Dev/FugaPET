using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 089A — correção do drift <c>operacao_producao_configuracao.codigo_perfil_resultado</c> (coluna inexistente
/// no Q/HML, provado em 087A/087C; causaria SQLSTATE 42703 em ListarConfiguracoesAtivasAsync). Prova materialmente:
///  A. os SELECTs de operacao_producao_configuracao NÃO leem mais codigo_perfil_resultado;
///  B. a coluna REAL do contrato 041 (operacao_resultado_definicao.codigo_perfil_resultado) é preservada;
///  C. o contrato/modelo CodigoPerfilResultado continua existindo (não é removido);
///  D. RESULTADO_APONTAMENTO permanece FAIL-CLOSED quando o perfil é null — sem fallback/default/último perfil.
/// </summary>
public sealed class ControleApontamentosPerfilDriftFix089Tests
{
    // ---------- A. SELECTs de configuração sem a coluna drifted ----------

    [Fact]
    public void A_SelectsDeConfiguracao_NaoLeemMaisCodigoPerfilResultado()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");
        string obter = ExtrairMetodo(repo, "public async Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoOperacaoAsync");
        string listar = ExtrairMetodo(repo, "public async Task<IReadOnlyList<ConfiguracaoOperacaoProcesso>> ListarConfiguracoesAtivasAsync");

        // Continuam consultando a tabela de configuração...
        Assert.Contains("FROM operacao_producao_configuracao", obter, StringComparison.Ordinal);
        Assert.Contains("FROM operacao_producao_configuracao", listar, StringComparison.Ordinal);

        // ...mas SEM a coluna inexistente.
        Assert.DoesNotContain("codigo_perfil_resultado", obter, StringComparison.Ordinal);
        Assert.DoesNotContain("codigo_perfil_resultado", listar, StringComparison.Ordinal);
    }

    [Fact]
    public void A_LerConfiguracao_MaterializaPerfilComoNull()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ControleApontamentosRepositorio.cs");
        string ler = ExtrairMetodo(repo, "private static ConfiguracaoOperacaoProcesso LerConfiguracao");

        // Materializa explicitamente null (fail-closed, resolvido depois) e NÃO lê nenhum ordinal do reader para
        // o perfil. Verifica o padrão de LEITURA (não a string do nome da coluna, que aparece no comentário).
        Assert.Contains("CodigoPerfilResultado = null", ler, StringComparison.Ordinal);
        Assert.DoesNotContain("CodigoPerfilResultado = leitor", ler, StringComparison.Ordinal);
    }

    // ---------- B. Coluna real do 041 preservada ----------

    [Fact]
    public void B_ResultadoApontamentoRepositorio_PreservaColunaDoContrato041()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ResultadoApontamentoRepositorio.cs");
        string listarDefinicoes = ExtrairMetodo(
            repo, "public async Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync");

        // Esta é OUTRA coluna, real, na tabela de DEFINIÇÕES (041) — deve continuar sendo usada.
        Assert.Contains("FROM operacao_resultado_definicao", listarDefinicoes, StringComparison.Ordinal);
        Assert.Contains("codigo_perfil_resultado = @codigo_perfil_resultado", listarDefinicoes, StringComparison.Ordinal);
    }

    // ---------- C. Contrato/modelo preservado ----------

    [Fact]
    public void C_ModeloEContrato_PreservamCodigoPerfilResultado()
    {
        Assert.NotNull(typeof(ConfiguracaoOperacaoProcesso).GetProperty(nameof(ConfiguracaoOperacaoProcesso.CodigoPerfilResultado)));
        Assert.NotNull(typeof(ContextoApontamentoProcesso).GetProperty(nameof(ContextoApontamentoProcesso.CodigoPerfilResultado)));
    }

    // ---------- D. Fail-closed comportamental quando perfil = null ----------

    [Fact]
    public async Task D_ListarDefinicoes_ComPerfilNull_RetornaVazioSemTocarRepositorio()
    {
        RepositorioSentinela repositorio = new();
        ResultadoApontamentoServico servico = new(repositorio);
        ContextoApontamentoProcesso contexto = ContextoComPerfil(null);

        IReadOnlyList<ResultadoApontamentoItem> definicoes =
            await servico.ListarDefinicoesAsync(contexto, CancellationToken.None);

        Assert.Empty(definicoes);                                   // nenhuma definição carregada
        Assert.False(repositorio.ListarDefinicoesChamado);          // fail-closed: nem consultou o banco
    }

    [Fact]
    public async Task D_ListarDefinicoes_ComPerfilResolvido_ConsultaRepositorio()
    {
        // Controle negativo: com perfil resolvido, o serviço PRECISA consultar o repositório — provando que o
        // short-circuit do teste anterior é causado exclusivamente pelo perfil null, não por outro motivo.
        RepositorioSentinela repositorio = new();
        ResultadoApontamentoServico servico = new(repositorio);
        ContextoApontamentoProcesso contexto = ContextoComPerfil(42);

        IReadOnlyList<ResultadoApontamentoItem> definicoes =
            await servico.ListarDefinicoesAsync(contexto, CancellationToken.None);

        Assert.Empty(definicoes);                                   // o sentinela devolve lista vazia
        Assert.True(repositorio.ListarDefinicoesChamado);           // mas AGORA consultou o banco
        Assert.Equal(42, repositorio.UltimoPerfilConsultado);
    }

    [Fact]
    public async Task D_Recovery_ComPerfilNull_FalhaSemTocarRepositorio()
    {
        RepositorioSentinela repositorio = new();
        ResultadoApontamentoServico servico = new(repositorio);
        ContextoApontamentoProcesso contexto = ContextoComPerfil(null);

        ResultadoApontamentoPersistidoRecovery recovery =
            await servico.ObterResultadoPersistidoDoApontamentoAsync(
                contexto, Array.Empty<ResultadoApontamentoItem>(), CancellationToken.None);

        Assert.Equal(ResultadoApontamentoPersistidoRecoveryEstado.Falha, recovery.Estado);
        Assert.False(repositorio.ListarResultadosChamado);          // fail-closed: recovery não busca nada
    }

    private static ContextoApontamentoProcesso ContextoComPerfil(long? perfil)
        => new()
        {
            CodigoApontamento = 1,
            NumeroOrdem = "1000164",
            TipoProcesso = TipoProcessoOperacao.ResultadoApontamento,
            CodigoPerfilResultado = perfil
        };

    private sealed class RepositorioSentinela : IResultadoApontamentoRepositorio
    {
        public bool ListarDefinicoesChamado { get; private set; }
        public bool ListarResultadosChamado { get; private set; }
        public long? UltimoPerfilConsultado { get; private set; }

        public Task<long> InserirAsync(RegistroResultadoApontamento registro, CancellationToken cancellationToken)
            => Task.FromResult(1L);

        public Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
            long codigoPerfilResultado, CancellationToken cancellationToken)
        {
            ListarDefinicoesChamado = true;
            UltimoPerfilConsultado = codigoPerfilResultado;
            return Task.FromResult<IReadOnlyList<ResultadoApontamentoItem>>(Array.Empty<ResultadoApontamentoItem>());
        }

        public Task<IReadOnlyList<ResultadoPersistidoApontamento>> ListarResultadosPersistidosDoApontamentoAsync(
            long codigoApontamento, CancellationToken cancellationToken)
        {
            ListarResultadosChamado = true;
            return Task.FromResult<IReadOnlyList<ResultadoPersistidoApontamento>>(Array.Empty<ResultadoPersistidoApontamento>());
        }
    }

    // ---------- helpers de source-scan ----------

    private static string LerArquivoProjeto(params string[] partes)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return File.ReadAllText(Path.Combine(new[] { dir }.Concat(partes).ToArray()));
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Projeto FugaPET_Dev não localizado para source-scan.");
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");

        int abre = fonte.IndexOf('{', inicio);
        Assert.True(abre >= 0, $"Corpo não encontrado: {assinatura}");

        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{')
            {
                profundidade++;
            }
            else if (fonte[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return fonte[inicio..(i + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Fim do método não encontrado: {assinatura}");
    }
}
