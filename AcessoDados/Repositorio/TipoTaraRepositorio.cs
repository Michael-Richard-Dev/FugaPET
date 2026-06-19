using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class TipoTaraRepositorio : RepositorioBase
{
    public TipoTaraRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<TipoTaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tipo_tara, nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara, tipo_tara_criado_em
            FROM tipo_tara
            ORDER BY nome_tipo_tara;
            """;

        List<TipoTaraCadastro> tipos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            tipos.Add(new TipoTaraCadastro
            {
                CodigoTipoTara = leitor.GetInt64(0),
                NomeTipoTara = leitor.GetString(1),
                DescricaoTipoTara = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
                SituacaoTipoTara = leitor.GetBoolean(3),
                TipoTaraCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
            });
        }

        return tipos;
    }

    public async Task<TipoTaraCadastro?> ObterPorIdAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tipo_tara, nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara, tipo_tara_criado_em
            FROM tipo_tara
            WHERE codigo_tipo_tara = @codigo_tipo_tara;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;

        return new TipoTaraCadastro
        {
            CodigoTipoTara = leitor.GetInt64(0),
            NomeTipoTara = leitor.GetString(1),
            DescricaoTipoTara = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
            SituacaoTipoTara = leitor.GetBoolean(3),
            TipoTaraCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
        };
    }

    public async Task<bool> ExisteNomeAsync(string nome, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM tipo_tara
                WHERE upper(trim(nome_tipo_tara)) = upper(trim(@nome_tipo_tara))
                  AND situacao_tipo_tara = true
                  AND (@ignorar_codigo IS NULL OR codigo_tipo_tara <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@nome_tipo_tara", nome));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<long> InserirAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO tipo_tara (nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara, tipo_tara_criado_por)
            VALUES (@nome_tipo_tara, @descricao_tipo_tara, @situacao_tipo_tara, @tipo_tara_criado_por)
            RETURNING codigo_tipo_tara;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroTexto("@nome_tipo_tara", tipo.NomeTipoTara));
            comando.Parameters.Add(ParametroTexto("@descricao_tipo_tara", tipo.DescricaoTipoTara));
            comando.Parameters.Add(ParametroBooleano("@situacao_tipo_tara", tipo.SituacaoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_criado_por", tipo.TipoTaraCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    public async Task<int> AtualizarAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tipo_tara
            SET nome_tipo_tara = @nome_tipo_tara,
                descricao_tipo_tara = @descricao_tipo_tara,
                situacao_tipo_tara = @situacao_tipo_tara,
                tipo_tara_atualizado_por = @tipo_tara_atualizado_por
            WHERE codigo_tipo_tara = @codigo_tipo_tara;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", tipo.CodigoTipoTara));
            comando.Parameters.Add(ParametroTexto("@nome_tipo_tara", tipo.NomeTipoTara));
            comando.Parameters.Add(ParametroTexto("@descricao_tipo_tara", tipo.DescricaoTipoTara));
            comando.Parameters.Add(ParametroBooleano("@situacao_tipo_tara", tipo.SituacaoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_atualizado_por", tipo.TipoTaraAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ExcluirAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tipo_tara
               SET situacao_tipo_tara = false,
                   tipo_tara_atualizado_por = @tipo_tara_atualizado_por
             WHERE codigo_tipo_tara = @codigo_tipo_tara
               AND situacao_tipo_tara = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReativarAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tipo_tara
               SET situacao_tipo_tara = true,
                   tipo_tara_atualizado_por = @tipo_tara_atualizado_por
             WHERE codigo_tipo_tara = @codigo_tipo_tara
               AND situacao_tipo_tara = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }
}
