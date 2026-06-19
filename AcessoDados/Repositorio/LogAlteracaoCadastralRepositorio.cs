using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Auditoria;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class LogAlteracaoCadastralRepositorio : RepositorioBase
{
    public LogAlteracaoCadastralRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<LogAlteracaoCadastralRegistro>> ListarRecentesAsync(
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_log_alteracao_cadastral,
                   tabela,
                   codigo_registro,
                   operacao,
                   codigo_usuario,
                   log_alteracao_cadastral_criado_em,
                   dados_anteriores::text,
                   dados_novos::text
              FROM log_alteracao_cadastral
             ORDER BY codigo_log_alteracao_cadastral DESC
             LIMIT @limite;
            """;

        List<LogAlteracaoCadastralRegistro> registros = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroInteiro("@limite", Math.Clamp(limite, 1, 500)));

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            registros.Add(Mapear(leitor));
        }

        return registros;
    }

    public async Task<IReadOnlyList<LogAlteracaoCadastralRegistro>> ListarPorRegistroAsync(
        string tabela,
        long codigoRegistro,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_log_alteracao_cadastral,
                   tabela,
                   codigo_registro,
                   operacao,
                   codigo_usuario,
                   log_alteracao_cadastral_criado_em,
                   dados_anteriores::text,
                   dados_novos::text
              FROM log_alteracao_cadastral
             WHERE tabela = @tabela
               AND codigo_registro = @codigo_registro
             ORDER BY codigo_log_alteracao_cadastral DESC;
            """;

        List<LogAlteracaoCadastralRegistro> registros = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@tabela", tabela.Trim()));
        comando.Parameters.Add(ParametroLongo("@codigo_registro", codigoRegistro));

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            registros.Add(Mapear(leitor));
        }

        return registros;
    }

    private static LogAlteracaoCadastralRegistro Mapear(NpgsqlDataReader leitor)
        => new()
        {
            CodigoLogAlteracaoCadastral = leitor.GetInt64(0),
            Tabela = leitor.GetString(1),
            CodigoRegistro = leitor.GetInt64(2),
            Operacao = leitor.GetString(3),
            CodigoUsuario = leitor.IsDBNull(4) ? null : leitor.GetInt64(4),
            CriadoEm = leitor.GetDateTime(5),
            DadosAnterioresJson = leitor.IsDBNull(6) ? null : leitor.GetString(6),
            DadosNovosJson = leitor.IsDBNull(7) ? null : leitor.GetString(7)
        };
}
