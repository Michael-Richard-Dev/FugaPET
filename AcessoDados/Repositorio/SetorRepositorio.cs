using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class SetorRepositorio : RepositorioBase
{
    public SetorRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<SetorCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_setor, nome_setor, descricao_setor, situacao_setor, setor_criado_em
            FROM setor
            ORDER BY nome_setor;
            """;

        List<SetorCadastro> setores = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            setores.Add(new SetorCadastro
            {
                CodigoSetor = leitor.GetInt64(0),
                NomeSetor = leitor.GetString(1),
                DescricaoSetor = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
                SituacaoSetor = leitor.GetBoolean(3),
                SetorCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
            });
        }

        return setores;
    }

    public async Task<SetorCadastro?> ObterPorIdAsync(long codigoSetor, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_setor, nome_setor, descricao_setor, situacao_setor, setor_criado_em
            FROM setor
            WHERE codigo_setor = @codigo_setor;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new SetorCadastro
        {
            CodigoSetor = leitor.GetInt64(0),
            NomeSetor = leitor.GetString(1),
            DescricaoSetor = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
            SituacaoSetor = leitor.GetBoolean(3),
            SetorCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
        };
    }

    public async Task<long> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO setor (nome_setor, descricao_setor, situacao_setor, setor_criado_por)
            VALUES (@nome_setor, @descricao_setor, @situacao_setor, @setor_criado_por)
            RETURNING codigo_setor;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroTexto("@nome_setor", setor.NomeSetor));
            comando.Parameters.Add(ParametroTexto("@descricao_setor", setor.DescricaoSetor));
            comando.Parameters.Add(ParametroBooleano("@situacao_setor", setor.SituacaoSetor));
            comando.Parameters.Add(ParametroLongoNulo("@setor_criado_por", setor.SetorCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    public async Task<bool> ExisteNomeAsync(string nomeSetor, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM setor
                WHERE upper(trim(nome_setor)) = upper(trim(@nome_setor))
                  AND situacao_setor = true
                  AND (@ignorar_codigo IS NULL OR codigo_setor <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@nome_setor", nomeSetor));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<int> AtualizarAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
    {
        // setor_atualizado_em e atualizado pelo trigger trg_setor_atualizado_em.
        const string sql = """
            UPDATE setor
            SET nome_setor = @nome_setor,
                descricao_setor = @descricao_setor,
                situacao_setor = @situacao_setor,
                setor_atualizado_por = @setor_atualizado_por
            WHERE codigo_setor = @codigo_setor;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_setor", setor.CodigoSetor));
            comando.Parameters.Add(ParametroTexto("@nome_setor", setor.NomeSetor));
            comando.Parameters.Add(ParametroTexto("@descricao_setor", setor.DescricaoSetor));
            comando.Parameters.Add(ParametroBooleano("@situacao_setor", setor.SituacaoSetor));
            comando.Parameters.Add(ParametroLongoNulo("@setor_atualizado_por", setor.SetorAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Soft-delete: marca situacao_setor = false. O trigger detecta como DELETE_LOGICO.
    /// </summary>
    public async Task<int> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE setor
               SET situacao_setor = false,
                   setor_atualizado_por = @setor_atualizado_por
             WHERE codigo_setor = @codigo_setor
               AND situacao_setor = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_setor", id));
            comando.Parameters.Add(ParametroLongoNulo("@setor_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Reativacao: marca situacao_setor = true. Trigger detecta como REATIVACAO.
    /// </summary>
    public async Task<int> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE setor
               SET situacao_setor = true,
                   setor_atualizado_por = @setor_atualizado_por
             WHERE codigo_setor = @codigo_setor
               AND situacao_setor = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_setor", id));
            comando.Parameters.Add(ParametroLongoNulo("@setor_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }
}
