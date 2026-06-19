using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Comum;

public abstract class RepositorioBase
{
    private readonly IFabricaConexaoBanco _fabricaConexaoBanco;

    protected RepositorioBase(IFabricaConexaoBanco fabricaConexaoBanco)
    {
        _fabricaConexaoBanco = fabricaConexaoBanco;
    }

    protected Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
        => _fabricaConexaoBanco.CriarConexaoAbertaAsync(cancellationToken);

    /// <summary>
    /// Executa uma operacao de gravacao auditavel no fluxo padrao:
    /// abrir conexao, iniciar transacao, definir app.usuario_id, executar DML e commit.
    /// Em falha, faz rollback e relanca a excecao para a camada de servico tratar
    /// com mensagem amigavel e log tecnico quando aplicavel.
    /// </summary>
    protected async Task<T> ExecutarEmTransacaoAuditavelAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> operacao,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);

        try
        {
            await DefinirUsuarioAppAsync(conexao, transacao, cancellationToken);
            T resultado = await operacao(conexao, transacao);
            await transacao.CommitAsync(cancellationToken);
            return resultado;
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Auditoria SEMPRE via fluxo transacional: use ExecutarEmTransacaoAuditavelAsync, que abre a
    // transacao e chama DefinirUsuarioAppAsync com a transacao. O set_config('app.usuario_id', ..., true)
    // e LOCAL a transacao; definir o usuario fora de uma transacao explicita nao garante o valor
    // para os comandos seguintes. Por isso NAO existe helper de "conexao com usuario" sem transacao.

    /// <summary>
    /// Define app.usuario_id na transacao informada (zera se nao houver sessao), para os triggers
    /// de log_alteracao_cadastral identificarem o usuario corrente. Exige transacao explicita —
    /// o set_config(..., true) e local a ela.
    /// </summary>
    protected static async Task DefinirUsuarioAppAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT set_config('app.usuario_id', @app_usuario_id, true);";
        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroUsuarioApp());
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    protected static async Task DefinirUsuarioAppAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoUsuario,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT set_config('app.usuario_id', @app_usuario_id, true);";
        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroTexto("@app_usuario_id", codigoUsuario.ToString()));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Parametro @app_usuario_id pronto para ser concatenado em SQL multi-statement
    /// que comece com "SELECT set_config('app.usuario_id', @app_usuario_id, true); ...".
    /// Vazio quando nao ha sessao ativa (trigger trata via NULLIF).
    /// </summary>
    protected static NpgsqlParameter ParametroUsuarioApp()
    {
        string usuarioId = ObterCodigoUsuarioSessaoTexto();
        return new NpgsqlParameter("@app_usuario_id", usuarioId);
    }

    /// <summary>
    /// Codigo do usuario corrente ou null quando nao ha sessao.
    /// Use para preencher colunas *_criado_por / *_atualizado_por automaticamente.
    /// </summary>
    protected static long? ObterCodigoUsuarioSessao()
        => EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;

    private static string ObterCodigoUsuarioSessaoTexto()
        => EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario?.ToString() ?? string.Empty;

    protected static NpgsqlParameter ParametroTexto(string nome, string? valor)
        => new(nome, valor ?? string.Empty);

    protected static NpgsqlParameter ParametroInteiro(string nome, int valor)
        => new(nome, valor);

    protected static NpgsqlParameter ParametroInteiroNulo(string nome, int? valor)
        => new(nome, valor is null ? DBNull.Value : valor);

    protected static NpgsqlParameter ParametroLongo(string nome, long valor)
        => new(nome, valor);

    protected static NpgsqlParameter ParametroLongoNulo(string nome, long? valor)
        => new(nome, NpgsqlTypes.NpgsqlDbType.Bigint) { Value = valor is null ? DBNull.Value : valor };

    protected static NpgsqlParameter ParametroDecimalNulo(string nome, decimal? valor)
        => new(nome, valor is null ? DBNull.Value : valor);

    protected static NpgsqlParameter ParametroBooleano(string nome, bool valor)
        => new(nome, valor);
}
