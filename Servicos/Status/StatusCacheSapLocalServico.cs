using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Status;
using Npgsql;

namespace FugaPET_Dev.Servicos.Status;

public sealed class StatusCacheSapLocalServico
{
    private static readonly string[] ViewsCacheSap =
    [
        "vw_sap_cache_status",
        "producao.vw_sap_cache_status"
    ];

    private readonly ConfiguracaoBancoPostgreSql _configuracao;
    private readonly IFabricaConexaoBanco _fabricaConexaoBanco;

    public StatusCacheSapLocalServico()
        : this(LeitorConfiguracaoBancoPostgreSql.Carregar())
    {
    }

    public StatusCacheSapLocalServico(ConfiguracaoBancoPostgreSql configuracao)
        : this(configuracao, new FabricaConexaoPostgreSql(configuracao))
    {
    }

    public StatusCacheSapLocalServico(
        ConfiguracaoBancoPostgreSql configuracao,
        IFabricaConexaoBanco fabricaConexaoBanco)
    {
        _configuracao = configuracao;
        _fabricaConexaoBanco = fabricaConexaoBanco;
    }

    public async Task<ItemStatusIndustrial> ObterStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuracao.Habilitado)
        {
            return CriarStatus(
                "Desabilitado",
                false,
                "Banco local desabilitado. Cache SAP local não pode ser consultado.");
        }

        try
        {
            await using NpgsqlConnection conexao =
                await _fabricaConexaoBanco.CriarConexaoAbertaAsync(cancellationToken);

            string? viewCache = await ResolverViewCacheSapAsync(conexao, cancellationToken);
            if (viewCache is not null)
            {
                return await ObterStatusPorViewAsync(conexao, viewCache, cancellationToken);
            }

            int tabelasCache = await ContarTabelasCacheSapAsync(conexao, cancellationToken);
            if (tabelasCache > 0)
            {
                return CriarStatus(
                    "Estrutura local",
                    true,
                    $"Estrutura local de cache SAP encontrada ({tabelasCache} tabela(s)), mas a view vw_sap_cache_status não foi localizada.");
            }

            return CriarStatus(
                "Não configurado",
                false,
                "Nenhuma estrutura local de cache SAP foi encontrada no banco configurado.");
        }
        catch (Exception ex)
        {
            // Detalhe tecnico vai para o log de diagnostico; o operador ve mensagem generica.
            System.Diagnostics.Trace.TraceError($"StatusCacheSapLocalServico: falha ao consultar cache SAP. {ex}");
            return CriarStatus(
                "Indisponível",
                false,
                "Não foi possível consultar o cache SAP local. Acione o suporte.");
        }
    }

    private static async Task<string?> ResolverViewCacheSapAsync(
        NpgsqlConnection conexao,
        CancellationToken cancellationToken)
    {
        foreach (string viewCache in ViewsCacheSap)
        {
            await using NpgsqlCommand comando = new("SELECT to_regclass(@view_cache) IS NOT NULL;", conexao);
            comando.Parameters.Add(new NpgsqlParameter("@view_cache", viewCache));
            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            if (retorno is bool existe && existe)
            {
                return viewCache;
            }
        }

        return null;
    }

    private static async Task<ItemStatusIndustrial> ObterStatusPorViewAsync(
        NpgsqlConnection conexao,
        string viewCache,
        CancellationToken cancellationToken)
    {
        string sql = $"""
            SELECT COUNT(*)::bigint AS total,
                   COUNT(*) FILTER (
                       WHERE status_cache = 'VALIDO'
                         AND cache_vencido = false
                         AND ativo_sap = true
                   )::bigint AS validos,
                   COUNT(*) FILTER (
                       WHERE status_cache <> 'VALIDO'
                          OR cache_vencido = true
                          OR ativo_sap = false
                   )::bigint AS alertas,
                   MAX(sincronizado_em) AS ultima_sincronizacao
            FROM {viewCache};
            """;

        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return CriarStatus("Sem dados", true, "Cache SAP local consultado, sem retorno de resumo.");
        }

        long total = leitor.GetInt64(0);
        long validos = leitor.GetInt64(1);
        long alertas = leitor.GetInt64(2);
        DateTime? ultimaSincronizacao = leitor.IsDBNull(3) ? null : leitor.GetDateTime(3).ToLocalTime();

        if (total == 0)
        {
            return CriarStatus(
                "Sem dados",
                true,
                $"View {viewCache} encontrada, mas ainda não há registros no cache SAP local.");
        }

        string situacao = alertas > 0 ? "Atenção" : "Leitura local";
        string ultima = ultimaSincronizacao.HasValue
            ? ultimaSincronizacao.Value.ToString("dd/MM/yyyy HH:mm")
            : "sem data";

        return new ItemStatusIndustrial
        {
            Nome = "Cache SAP Local",
            Identificador = viewCache,
            Ambiente = "Somente leitura local",
            Habilitado = true,
            Online = alertas == 0,
            Situacao = situacao,
            Mensagem = $"Cache SAP local consultado: {validos} válido(s), {alertas} alerta(s), {total} total. Última sincronização: {ultima}.",
            AtualizadoEm = DateTimeOffset.Now
        };
    }

    private static async Task<int> ContarTabelasCacheSapAsync(
        NpgsqlConnection conexao,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::integer
            FROM information_schema.tables
            WHERE table_schema IN ('desenvolvimento', 'producao')
              AND table_type = 'BASE TABLE'
              AND table_name LIKE 'sap_%';
            """;

        await using NpgsqlCommand comando = new(sql, conexao);
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is int total ? total : 0;
    }

    private static ItemStatusIndustrial CriarStatus(
        string situacao,
        bool habilitado,
        string mensagem)
        => new()
        {
            Nome = "Cache SAP Local",
            Identificador = "SAP",
            Ambiente = "Somente leitura local",
            Habilitado = habilitado,
            Online = habilitado,
            Situacao = situacao,
            Mensagem = mensagem,
            AtualizadoEm = DateTimeOffset.Now
        };
}
