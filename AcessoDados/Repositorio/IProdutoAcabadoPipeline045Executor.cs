namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Parâmetro posicional de uma chamada de função 045. <see cref="TipoSql"/> é o nome lógico do tipo PostgreSQL
/// (bigint, integer, varchar, text, uuid, jsonb, boolean, timestamptz) — o executor Npgsql mapeia para
/// <c>NpgsqlDbType</c>; o executor fake dos testes ignora o tipo e apenas registra o valor.
/// </summary>
public sealed record Parametro045(string TipoSql, object? Valor)
{
    public static Parametro045 Bigint(long? v) => new("bigint", v);
    public static Parametro045 Integer(int? v) => new("integer", v);
    public static Parametro045 Numeric(decimal? v) => new("numeric", v);
    public static Parametro045 Varchar(string? v) => new("varchar", (object?)v ?? DBNull.Value);
    public static Parametro045 Texto(string? v) => new("text", (object?)v ?? DBNull.Value);
    public static Parametro045 Uuid(Guid? v) => new("uuid", v);
    public static Parametro045 Jsonb(string? json) => new("jsonb", (object?)json ?? DBNull.Value);
    public static Parametro045 Booleano(bool v) => new("boolean", v);
    public static Parametro045 TimestampTz(DateTimeOffset? v) => new("timestamptz", v);
}

/// <summary>Uma linha retornada por uma função 045 (colunas por nome). Só leitura.</summary>
public sealed class Linha045
{
    private readonly IReadOnlyDictionary<string, object?> _colunas;
    public Linha045(IReadOnlyDictionary<string, object?> colunas) => _colunas = colunas;

    public bool Tem(string coluna) => _colunas.ContainsKey(coluna);
    public object? Bruto(string coluna) => _colunas.TryGetValue(coluna, out object? v) ? v : null;

    public Guid? ObterUuid(string coluna)
        => Bruto(coluna) is Guid g ? g : Bruto(coluna) is string s && Guid.TryParse(s, out Guid p) ? p : null;

    public string? ObterTexto(string coluna) => Bruto(coluna)?.ToString();

    public int? ObterInt(string coluna)
        => Bruto(coluna) is int i ? i : Bruto(coluna) is long l ? (int)l : null;

    public long? ObterLong(string coluna)
        => Bruto(coluna) is long l ? l : Bruto(coluna) is int i ? i : null;

    public decimal? ObterDecimal(string coluna) => Bruto(coluna) switch
    {
        decimal d => d,
        double db => (decimal)db,
        float f => (decimal)f,
        long l => l,
        int i => i,
        string s when decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal p) => p,
        _ => null
    };
}

/// <summary>
/// Executor de funções 045 vinculado a UMA transação aberta (mesma conexão/mesma transação Npgsql). Usado
/// EXCLUSIVAMENTE dentro de <see cref="IProdutoAcabadoPipeline045Executor.ExecutarEmTransacaoAsync{T}"/> para
/// compor operações atômicas (ex.: criar palete + vincular todas as caixas). Sem COMMIT/ROLLBACK aqui — isso é
/// responsabilidade do escopo transacional que o produziu (COMMIT só após TODAS as chamadas terem sucesso).
/// </summary>
public interface IExecutorFuncoes045Transacional
{
    Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(
        string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default);
}

/// <summary>
/// SEAM testável de execução das FUNÇÕES do Incremental 045 (SECURITY DEFINER). O contrato C# consome
/// EXCLUSIVAMENTE funções <c>fn_pa_045_*</c> — NUNCA DML direto (INSERT/UPDATE/DELETE) e NUNCA SELECT de
/// tabela (em particular NUNCA <c>SELECT claim_token</c>/<c>recovery_claim_token</c>). Os testes injetam um
/// executor fake; produção usa o executor Npgsql real sobre a conexão/role configurada da aplicação.
/// </summary>
public interface IProdutoAcabadoPipeline045Executor
{
    /// <summary>True somente quando há configuração PostgreSQL válida da aplicação (banco habilitado, não demonstrativo).</summary>
    bool Disponivel { get; }

    /// <summary>
    /// Executa <c>SELECT * FROM &lt;funcao&gt;($1,$2,...)</c> com os parâmetros posicionais e devolve as linhas.
    /// <paramref name="funcao"/> DEVE casar <c>^fn_pa_045_[a-z0-9_]+$</c> (nunca entrada de usuário).
    /// </summary>
    Task<IReadOnlyList<Linha045>> ExecutarFuncaoAsync(
        string funcao, IReadOnlyList<Parametro045> parametros, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê uma VIEW runtime SEGURA de estado/snapshot (sem tokens): <c>SELECT * FROM &lt;view&gt; WHERE
    /// &lt;colunaFiltro&gt;=$1</c>. <paramref name="view"/> DEVE casar <c>^vw_pa_045_[a-z0-9_]+$</c> e
    /// <paramref name="colunaFiltro"/> ser um identificador de chave permitido (codigo_hu_caixa/codigo_hu_palete).
    /// As views oficiais NÃO expõem claim_token/recovery_claim_token (§4) — reconstrução de estado sem capability.
    /// </summary>
    Task<IReadOnlyList<Linha045>> LerViewRuntimeAsync(
        string view, string colunaFiltro, Parametro045 valorFiltro, CancellationToken cancellationToken = default);

    /// <summary>
    /// GATE 046-E §3/§4: executa <paramref name="operacao"/> dentro de UMA ÚNICA conexão + transação Npgsql.
    /// COMMIT somente se a operação retornar normalmente; qualquer exceção (inclusive falha/cancelamento antes do
    /// COMMIT) provoca ROLLBACK integral e é repropagada. Após o COMMIT concluir, um cancelamento POSTERIOR do
    /// token NÃO reverte a operação já persistida. Sem DELETE/UPDATE compensatório.
    /// </summary>
    Task<T> ExecutarEmTransacaoAsync<T>(
        Func<IExecutorFuncoes045Transacional, CancellationToken, Task<T>> operacao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GATE 046-E §7 (leitura autorizada GAIA V3): projeção OPERACIONAL da composição de paletes locais por OP
    /// (e terminal, quando informado), unindo <c>vw_pa_045_palete_estado_runtime</c> + <c>hu_palete_item</c> +
    /// <c>hu_caixa</c>. Colunas EXPLÍCITAS (sem SELECT *), SEM payload/claim/lease/recovery. Ordem determinística
    /// por codigo_hu_palete, ordem_item. Schema resolvido pelo SearchPath (sem hardcode).
    /// </summary>
    Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorOrdemAsync(
        string numeroOrdemProducao, string? terminal, CancellationToken cancellationToken = default);

    /// <summary>
    /// GATE 047-AB: mesma projeção OPERACIONAL da composição, porém filtrada SOMENTE por <c>h.terminal</c> (sem OP) —
    /// reload de PALETES FORMADOS ao ABRIR a Paletização (o operador não seleciona OP na abertura). Colunas
    /// EXPLÍCITAS, sem payload/claim/lease/recovery, ordem determinística. Default: lista vazia (fakes herdam).
    /// </summary>
    Task<IReadOnlyList<Linha045>> LerComposicaoPaletesLocaisPorTerminalAsync(
        string terminal, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Linha045>>([]);
}
