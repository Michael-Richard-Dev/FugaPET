using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

// Nao-sealed e com metodos virtuais para permitir fakes nos testes de regra de negocio.
public class SetorRepositorio : RepositorioBase
{
    public SetorRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public virtual async Task<IReadOnlyList<SetorCadastro>> ListarAsync(CancellationToken cancellationToken = default)
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

    public virtual async Task<SetorCadastro?> ObterPorIdAsync(long codigoSetor, CancellationToken cancellationToken = default)
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

    public virtual async Task<long> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
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

    public virtual async Task<bool> ExisteNomeAsync(string nomeSetor, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
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

    public virtual async Task<int> AtualizarAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
    {
        // setor_atualizado_em e atualizado pelo trigger trg_setor_atualizado_em.
        const string sql = """
            UPDATE setor
            SET nome_setor = @nome_setor,
                descricao_setor = @descricao_setor,
                setor_atualizado_por = @setor_atualizado_por
            WHERE codigo_setor = @codigo_setor;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_setor", setor.CodigoSetor));
            comando.Parameters.Add(ParametroTexto("@nome_setor", setor.NomeSetor));
            comando.Parameters.Add(ParametroTexto("@descricao_setor", setor.DescricaoSetor));
            comando.Parameters.Add(ParametroLongoNulo("@setor_atualizado_por", setor.SetorAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Soft-delete: marca situacao_setor = false. O trigger detecta como DELETE_LOGICO.
    /// </summary>
    public virtual async Task<ResumoDependenciasSetor> ObterResumoDependenciasAtivasAsync(
        long codigoSetor,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                (SELECT count(*)::integer
                   FROM usuario
                  WHERE codigo_setor_padrao = @codigo_setor
                    AND situacao_usuario = true),
                (SELECT count(*)::integer
                   FROM usuario_setor
                  WHERE codigo_setor = @codigo_setor
                    AND situacao_usuario_setor = true),
                (SELECT count(*)::integer
                   FROM balanca
                  WHERE codigo_setor = @codigo_setor
                    AND situacao_balanca = true),
                (SELECT count(*)::integer
                   FROM tara
                  WHERE codigo_setor = @codigo_setor
                    AND situacao_tara = true),
                (SELECT count(*)::integer
                   FROM parametro_operacao
                  WHERE codigo_setor = @codigo_setor
                    AND situacao_parametro_operacao = true),
                (SELECT count(*)::integer
                   FROM entrada_produto_lancamento
                  WHERE codigo_setor = @codigo_setor
                    AND situacao_entrada_produto_lancamento = true
                    AND status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO'));
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken))
        {
            return new ResumoDependenciasSetor();
        }

        return new ResumoDependenciasSetor
        {
            UsuariosPadraoAtivos = leitor.GetInt32(0),
            VinculosUsuarioAtivos = leitor.GetInt32(1),
            BalancasAtivas = leitor.GetInt32(2),
            TarasAtivas = leitor.GetInt32(3),
            ParametrosOperacaoAtivos = leitor.GetInt32(4),
            LancamentosOperacionaisAtivos = leitor.GetInt32(5)
        };
    }

    public virtual async Task<int> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE setor
               SET situacao_setor = false,
                   setor_atualizado_por = @setor_atualizado_por
             WHERE codigo_setor = @codigo_setor
               AND situacao_setor = true
               AND NOT EXISTS (
                   SELECT 1 FROM usuario
                    WHERE codigo_setor_padrao = @codigo_setor
                      AND situacao_usuario = true
               )
               AND NOT EXISTS (
                   SELECT 1 FROM usuario_setor
                    WHERE codigo_setor = @codigo_setor
                      AND situacao_usuario_setor = true
               )
               AND NOT EXISTS (
                   SELECT 1 FROM balanca
                    WHERE codigo_setor = @codigo_setor
                      AND situacao_balanca = true
               )
               AND NOT EXISTS (
                   SELECT 1 FROM tara
                    WHERE codigo_setor = @codigo_setor
                      AND situacao_tara = true
               )
               AND NOT EXISTS (
                   SELECT 1 FROM parametro_operacao
                    WHERE codigo_setor = @codigo_setor
                      AND situacao_parametro_operacao = true
               )
               AND NOT EXISTS (
                   SELECT 1 FROM entrada_produto_lancamento
                    WHERE codigo_setor = @codigo_setor
                      AND situacao_entrada_produto_lancamento = true
                      AND status_lancamento NOT IN ('CONFIRMADO_SAP', 'CANCELADO')
               );
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
    public virtual async Task<int> ReativarAsync(long id, CancellationToken cancellationToken = default)
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
