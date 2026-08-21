using System.Text.RegularExpressions;
using FugaPET_Dev.AcessoDados.Banco;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Executor Npgsql REAL das funções do Incremental 045. Consome SOMENTE funções <c>fn_pa_045_*</c> via
/// invocação de função (<c>... * FROM &lt;funcao&gt;($1,...)</c>). NÃO faz DML direto e NÃO lê tabelas (portanto
/// nunca busca capability/token por consulta). Usa a conexão/role configurada da aplicação (§7: sem hardcode; SearchPath do
/// schema oficial; nunca superuser). O locking/atomicidade é do banco (SECURITY DEFINER) — não recriado no C#.
/// </summary>
public sealed class ProdutoAcabadoPipeline045NpgsqlExecutor : IProdutoAcabadoPipeline045Executor
{
    private static readonly Regex NomeFuncaoValido = new("^fn_pa_045_[a-z0-9_]+$", RegexOptions.Compiled);
    private static readonly Regex NomeViewValido = new("^vw_pa_045_[a-z0-9_]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> ColunasFiltroPermitidas = new(StringComparer.Ordinal)
        { "codigo_hu_caixa", "codigo_hu_palete" };

    private readonly FabricaConexaoPostgreSql _fabricaConexao;

    public ProdutoAcabadoPipeline045NpgsqlExecutor(FabricaConexaoPostgreSql fabricaConexao)
        => _fabricaConexao = fabricaConexao ?? throw new ArgumentNullException(nameof(fabricaConexao));

    public bool Disponivel => true; // criado apenas quando a fábrica confirmou banco habilitado/configurado

    public async Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(
        string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        if (string.IsNullOrWhiteSpace(funcao) || !NomeFuncaoValido.IsMatch(funcao))
        {
            // Defesa: só funções 045 nomeadas por constantes internas chegam aqui; nunca entrada de usuário.
            throw new ArgumentException("Nome de função 045 inválido.", nameof(funcao));
        }

        // SELECT * FROM fn(...): chamada de FUNÇÃO (não é DML, não é SELECT de tabela). SearchPath resolve o schema.
        string posicoes = parametros.Count == 0
            ? string.Empty
            : string.Join(",", Enumerable.Range(1, parametros.Count).Select(i => $"${i}"));
        string sql = $"SELECT * FROM {funcao}({posicoes})";

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        foreach (Parametro045 p in parametros)
        {
            cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = MapearTipo(p.TipoSql), Value = p.Valor ?? DBNull.Value });
        }

        List<Linha045> linhas = [];
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            Dictionary<string, object?> colunas = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < leitor.FieldCount; i++)
            {
                object? valor = await leitor.IsDBNullAsync(i, cancellationToken) ? null : leitor.GetValue(i);
                colunas[leitor.GetName(i)] = valor;
            }
            linhas.Add(new Linha045(colunas));
        }
        return linhas;
    }

    public async Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(
        string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(valorFiltro);
        if (string.IsNullOrWhiteSpace(view) || !NomeViewValido.IsMatch(view) || !ColunasFiltroPermitidas.Contains(colunaFiltro))
        {
            throw new ArgumentException("View/coluna de filtro 045 inválida.", nameof(view));
        }

        // Leitura de VIEW SEGURA (sem tokens). Filtro por chave via parâmetro ($1); identificadores whitelisted.
        string sql = $"SELECT * FROM {view} WHERE {colunaFiltro}=$1";

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = MapearTipo(valorFiltro.TipoSql), Value = valorFiltro.Valor ?? DBNull.Value });

        List<Linha045> linhas = [];
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            Dictionary<string, object?> colunas = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < leitor.FieldCount; i++)
            {
                colunas[leitor.GetName(i)] = await leitor.IsDBNullAsync(i, cancellationToken) ? null : leitor.GetValue(i);
            }
            linhas.Add(new Linha045(colunas));
        }
        return linhas;
    }

    public async Task<T> ExecutarEmTransacaoAsync<T>(
        Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao = await con.BeginTransactionAsync(cancellationToken);
        ExecutorFuncoes045Transacional executorTx = new(con, transacao);
        try
        {
            T resultado = await operacao(executorTx, cancellationToken);
            // REV2 Blocker 1: reavalia o token ENTRE o fim da última operação e o início do COMMIT. Cancelamento
            // solicitado antes do commit ⇒ exceção ⇒ ROLLBACK integral (nada persiste).
            cancellationToken.ThrowIfCancellationRequested();
            // §5/§G: COMMIT com token NEUTRO — uma vez iniciado, um cancelamento posterior não reverte o persistido.
            await transacao.CommitAsync(CancellationToken.None);
            return resultado;
        }
        catch
        {
            // §3/§14: qualquer false→exceção, erro PostgreSQL, exception ou cancelamento antes do COMMIT ⇒ ROLLBACK integral.
            await transacao.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(
        string numeroOrdemProducao, string? terminal, CancellationToken cancellationToken = default)
    {
        // §7/§8: projeção OPERACIONAL enxuta, colunas EXPLÍCITAS (cabeçalho + item + caixa). Somente campos de
        // uso operacional — nada de dados técnicos de request/resposta, tokens ou recovery. Filtro por OP ($1) e
        // terminal opcional ($2). Ordenação determinística (não depende da ordem natural do PostgreSQL). Schema
        // resolvido pelo SearchPath (sem hardcode de schema).
        const string sql = """
            SELECT
                h.codigo_hu_palete, h.material_embalagem, h.centro, h.deposito,
                h.peso_bruto, h.peso_liquido, h.peso_tara, h.unidade_peso, h.status_hu_palete, h.terminal,
                COALESCE(to_jsonb(h)->>'handling_unit_external_id', to_jsonb(h)->>'hu_palete') AS handling_unit_palete,
                i.codigo_hu_palete_item, i.codigo_hu_caixa, i.ordem_item, i.status_vinculo,
                c.numero_ordem_producao, c.item_ordem_producao, c.hu_caixa, c.handling_unit_external_id,
                c.numero_caixa, c.material, c.lote, c.status_hu_caixa
            FROM vw_pa_045_palete_estado_runtime h
            JOIN hu_palete_item i ON i.codigo_hu_palete = h.codigo_hu_palete
            JOIN hu_caixa c ON c.codigo_hu_caixa = i.codigo_hu_caixa
            WHERE c.numero_ordem_producao = $1
              AND ($2::text IS NULL OR h.terminal = $2)
            ORDER BY h.codigo_hu_palete, i.ordem_item
            """;

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = numeroOrdemProducao ?? (object)DBNull.Value });
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)terminal ?? DBNull.Value });
        return await LerLinhasAsync(cmd, cancellationToken);
    }

    // GATE 047-AB: reload por TERMINAL (sem OP). Mesma projeção enxuta e colunas explícitas; filtra por h.terminal ($1).
    public async Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorTerminalAsync(
        string terminal, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                h.codigo_hu_palete, h.material_embalagem, h.centro, h.deposito,
                h.peso_bruto, h.peso_liquido, h.peso_tara, h.unidade_peso, h.status_hu_palete, h.terminal,
                COALESCE(to_jsonb(h)->>'handling_unit_external_id', to_jsonb(h)->>'hu_palete') AS handling_unit_palete,
                i.codigo_hu_palete_item, i.codigo_hu_caixa, i.ordem_item, i.status_vinculo,
                c.numero_ordem_producao, c.item_ordem_producao, c.hu_caixa, c.handling_unit_external_id,
                c.numero_caixa, c.material, c.lote, c.status_hu_caixa
            FROM vw_pa_045_palete_estado_runtime h
            JOIN hu_palete_item i ON i.codigo_hu_palete = h.codigo_hu_palete
            JOIN hu_caixa c ON c.codigo_hu_caixa = i.codigo_hu_caixa
            WHERE h.terminal = $1
            ORDER BY h.codigo_hu_palete, i.ordem_item
            """;

        await using NpgsqlConnection con = await _fabricaConexao.CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand cmd = new(sql, con);
        cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = (object?)terminal ?? DBNull.Value });
        return await LerLinhasAsync(cmd, cancellationToken);
    }

    private static async Task<IReadOnlyList<Linha045>> LerLinhasAsync(NpgsqlCommand cmd, CancellationToken cancellationToken)
    {
        List<Linha045> linhas = [];
        await using NpgsqlDataReader leitor = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            Dictionary<string, object?> colunas = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < leitor.FieldCount; i++)
            {
                colunas[leitor.GetName(i)] = await leitor.IsDBNullAsync(i, cancellationToken) ? null : leitor.GetValue(i);
            }
            linhas.Add(new Linha045(colunas));
        }
        return linhas;
    }

    /// <summary>Executor de funções 045 preso a uma conexão/transação já abertas (sem COMMIT/ROLLBACK próprios).</summary>
    private sealed class ExecutorFuncoes045Transacional : IExecutorFuncoes045Transacional
    {
        private readonly NpgsqlConnection _con;
        private readonly NpgsqlTransaction _transacao;

        public ExecutorFuncoes045Transacional(NpgsqlConnection con, NpgsqlTransaction transacao)
        {
            _con = con;
            _transacao = transacao;
        }

        public async Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(
            string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(parametros);
            if (string.IsNullOrWhiteSpace(funcao) || !NomeFuncaoValido.IsMatch(funcao))
            {
                throw new ArgumentException("Nome de função 045 inválido.", nameof(funcao));
            }

            string posicoes = parametros.Count == 0
                ? string.Empty
                : string.Join(",", Enumerable.Range(1, parametros.Count).Select(i => $"${i}"));
            await using NpgsqlCommand cmd = new($"SELECT * FROM {funcao}({posicoes})", _con, _transacao);
            foreach (Parametro045 p in parametros)
            {
                cmd.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = MapearTipo(p.TipoSql), Value = p.Valor ?? DBNull.Value });
            }
            return await LerLinhasAsync(cmd, cancellationToken);
        }
    }

    private static NpgsqlDbType MapearTipo(string tipoSql) => tipoSql switch
    {
        "bigint" => NpgsqlDbType.Bigint,
        "integer" => NpgsqlDbType.Integer,
        "numeric" => NpgsqlDbType.Numeric,
        "varchar" => NpgsqlDbType.Varchar,
        "text" => NpgsqlDbType.Text,
        "uuid" => NpgsqlDbType.Uuid,
        "jsonb" => NpgsqlDbType.Jsonb,
        "boolean" => NpgsqlDbType.Boolean,
        "timestamptz" => NpgsqlDbType.TimestampTz,
        _ => throw new ArgumentOutOfRangeException(nameof(tipoSql), tipoSql, "Tipo SQL não suportado para 045.")
    };
}

/// <summary>Executor 045 indisponível (fail-closed): banco desabilitado/demonstrativo/config inválida ⇒ nenhuma chamada.</summary>
public sealed class ProdutoAcabadoPipeline045ExecutorIndisponivel : IProdutoAcabadoPipeline045Executor
{
    public bool Disponivel => false;

    public Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(
        string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Store 045 indisponível (banco não configurado/habilitado). Fail-closed: nenhuma função 045 executada.");

    public Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(
        string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Store 045 indisponível: nenhuma view runtime consultada (fail-closed).");

    public Task<T> ExecutarEmTransacaoAsync<T>(
        Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Store 045 indisponível: nenhuma transação 045 iniciada (fail-closed).");

    public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(
        string numeroOrdemProducao, string? terminal, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Store 045 indisponível: nenhuma composição de palete consultada (fail-closed).");

    public Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorTerminalAsync(
        string terminal, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "Store 045 indisponível: nenhuma composição de palete consultada (fail-closed).");
}

/// <summary>
/// Composição do executor 045. Banco habilitado e não demonstrativo ⇒ executor Npgsql real sobre a conexão
/// configurada (role da aplicação, nunca superuser); caso contrário ⇒ fail-closed (indisponível).
/// </summary>
public static class FabricaProdutoAcabadoPipeline045Executor
{
    public static IProdutoAcabadoPipeline045Executor Criar()
    {
        if (EstadoIntegracaoBanco.PodeUsarDadosSimulados || !EstadoIntegracaoBanco.Habilitado)
        {
            return new ProdutoAcabadoPipeline045ExecutorIndisponivel();
        }

        ConfiguracaoBancoPostgreSql configuracao = LeitorConfiguracaoBancoPostgreSql.Carregar();
        DiagnosticoRuntimeBanco.AvisarSeRuntimeNaoAprovado(configuracao);
        return new ProdutoAcabadoPipeline045NpgsqlExecutor(new FabricaConexaoPostgreSql(configuracao));
    }
}
