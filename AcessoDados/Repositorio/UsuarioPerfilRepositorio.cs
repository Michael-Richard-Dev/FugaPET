using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class UsuarioPerfilRepositorio : RepositorioBase
{
    public UsuarioPerfilRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<UsuarioPerfilCadastro>> ListarPorUsuarioAsync(long idUsuario, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COALESCE(NULLIF(to_jsonb(up) ->> 'codigo_usuario_perfil', ''), NULLIF(to_jsonb(up) ->> 'id_usuario_perfil', ''))::bigint AS codigo_usuario_perfil,
                COALESCE(NULLIF(to_jsonb(up) ->> 'codigo_usuario', ''), NULLIF(to_jsonb(up) ->> 'id_usuario', ''))::bigint AS codigo_usuario,
                COALESCE(NULLIF(to_jsonb(up) ->> 'codigo_perfil_acesso', ''), NULLIF(to_jsonb(up) ->> 'id_perfil_acesso', ''))::bigint AS codigo_perfil_acesso,
                situacao_usuario_perfil
            FROM usuario_perfil up
            WHERE COALESCE(NULLIF(to_jsonb(up) ->> 'codigo_usuario', ''), NULLIF(to_jsonb(up) ->> 'id_usuario', ''))::bigint = @codigo_usuario
            ORDER BY 1;
            """;

        List<UsuarioPerfilCadastro> lista = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            lista.Add(new UsuarioPerfilCadastro
            {
                Id = leitor.GetInt64(0),
                IdUsuario = leitor.GetInt64(1),
                IdPerfilAcesso = leitor.GetInt64(2),
                Ativo = leitor.GetBoolean(3)
            });
        }

        return lista;
    }

    public async Task<long> VincularAsync(long idUsuario, long idPerfilAcesso, bool ativo = true, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO usuario_perfil (codigo_usuario, codigo_perfil_acesso, situacao_usuario_perfil, usuario_perfil_criado_por)
            VALUES (@codigo_usuario, @codigo_perfil_acesso, @situacao_usuario_perfil, @usuario_perfil_criado_por)
            RETURNING codigo_usuario_perfil;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", idPerfilAcesso));
            comando.Parameters.Add(ParametroBooleano("@situacao_usuario_perfil", ativo));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_perfil_criado_por", ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    public async Task<long> SincronizarPerfilUnicoAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
    {
        const string sqlInativarAtivos = """
            UPDATE usuario_perfil
               SET situacao_usuario_perfil = false,
                   usuario_perfil_atualizado_por = @usuario_perfil_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND situacao_usuario_perfil = true;
            """;

        const string sqlReativarSelecionado = """
            WITH selecionado AS (
                SELECT codigo_usuario_perfil
                  FROM usuario_perfil
                 WHERE codigo_usuario = @codigo_usuario
                   AND codigo_perfil_acesso = @codigo_perfil_acesso
                   AND situacao_usuario_perfil = false
                 ORDER BY codigo_usuario_perfil DESC
                 LIMIT 1
            )
            UPDATE usuario_perfil up
               SET situacao_usuario_perfil = true,
                   usuario_perfil_atualizado_por = @usuario_perfil_atualizado_por
              FROM selecionado
             WHERE up.codigo_usuario_perfil = selecionado.codigo_usuario_perfil
            RETURNING up.codigo_usuario_perfil;
            """;

        const string sqlInserirSelecionado = """
            INSERT INTO usuario_perfil (codigo_usuario, codigo_perfil_acesso, situacao_usuario_perfil, usuario_perfil_criado_por)
            VALUES (@codigo_usuario, @codigo_perfil_acesso, true, @usuario_perfil_criado_por)
            RETURNING codigo_usuario_perfil;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long? codigoUsuarioSessao = ObterCodigoUsuarioSessao();

            await using (NpgsqlCommand comandoInativar = new(sqlInativarAtivos, conexao, transacao))
            {
                comandoInativar.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
                comandoInativar.Parameters.Add(ParametroLongoNulo("@usuario_perfil_atualizado_por", codigoUsuarioSessao));
                await comandoInativar.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (NpgsqlCommand comandoReativar = new(sqlReativarSelecionado, conexao, transacao))
            {
                comandoReativar.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
                comandoReativar.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", idPerfilAcesso));
                comandoReativar.Parameters.Add(ParametroLongoNulo("@usuario_perfil_atualizado_por", codigoUsuarioSessao));

                object? retornoReativado = await comandoReativar.ExecuteScalarAsync(cancellationToken);
                if (retornoReativado is long idReativado)
                {
                    return idReativado;
                }
            }

            await using (NpgsqlCommand comandoInserir = new(sqlInserirSelecionado, conexao, transacao))
            {
                comandoInserir.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
                comandoInserir.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", idPerfilAcesso));
                comandoInserir.Parameters.Add(ParametroLongoNulo("@usuario_perfil_criado_por", codigoUsuarioSessao));

                object? retornoInserido = await comandoInserir.ExecuteScalarAsync(cancellationToken);
                return retornoInserido is long idInserido ? idInserido : 0;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Soft-delete do vinculo (situacao_usuario_perfil = false). Mantem o historico e
    /// permite reativacao; o indice unico parcial so considera vinculos ativos.
    /// </summary>
    public async Task<int> RemoverAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario_perfil
               SET situacao_usuario_perfil = false,
                   usuario_perfil_atualizado_por = @usuario_perfil_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_usuario_perfil = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", idPerfilAcesso));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_perfil_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Reativa um vinculo previamente inativado (situacao_usuario_perfil = true).
    /// </summary>
    public async Task<int> ReativarAsync(long idUsuario, long idPerfilAcesso, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario_perfil
               SET situacao_usuario_perfil = true,
                   usuario_perfil_atualizado_por = @usuario_perfil_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_usuario_perfil = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", idPerfilAcesso));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_perfil_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }
}






