using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

// Nao-sealed e com metodos virtuais para permitir fake em testes de orquestracao (sem interface).
public class UsuarioRepositorio : RepositorioBase
{
    public UsuarioRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<UsuarioCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                   COALESCE(NULLIF(j ->> 'codigo_usuario', ''), NULLIF(j ->> 'id_usuario', ''))::bigint AS codigo_usuario,
                   COALESCE(NULLIF(j ->> 'codigo_cargo', ''), NULLIF(j ->> 'id_cargo', ''))::bigint AS codigo_cargo,
                   COALESCE(NULLIF(j ->> 'codigo_setor_padrao', ''), NULLIF(j ->> 'id_setor_padrao', ''))::bigint AS codigo_setor_padrao,
                   COALESCE(NULLIF(j ->> 'nome_usuario', ''), '') AS nome_usuario,
                   COALESCE(NULLIF(j ->> 'login_usuario', ''), '') AS login_usuario,
                   COALESCE(NULLIF(j ->> 'email_usuario', ''), '') AS email_usuario,
                   COALESCE(NULLIF(j ->> 'senha_hash', ''), '') AS senha_hash,
                   COALESCE(NULLIF(j ->> 'telefone_usuario', ''), '') AS telefone_usuario,
                   COALESCE(NULLIF(j ->> 'deve_trocar_senha', ''), 'false')::boolean AS deve_trocar_senha,
                   COALESCE(NULLIF(j ->> 'bloqueado_usuario', ''), 'false')::boolean AS bloqueado_usuario,
                   NULLIF(j ->> 'ultimo_login_em', '')::timestamptz AS ultimo_login_em,
                   COALESCE(NULLIF(j ->> 'situacao_usuario', ''), 'true')::boolean AS situacao_usuario
            FROM usuario u
            CROSS JOIN LATERAL to_jsonb(u) AS j
            ORDER BY nome_usuario;
            """;

        List<UsuarioCadastro> usuarios = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            usuarios.Add(new UsuarioCadastro
            {
                Id = leitor.GetInt64(0),
                IdCargo = leitor.IsDBNull(1) ? null : leitor.GetInt64(1),
                IdSetorPadrao = leitor.IsDBNull(2) ? null : leitor.GetInt64(2),
                NomeUsuario = leitor.GetString(3),
                LoginUsuario = leitor.GetString(4),
                EmailUsuario = leitor.IsDBNull(5) ? string.Empty : leitor.GetString(5),
                SenhaHash = leitor.GetString(6),
                TelefoneUsuario = leitor.IsDBNull(7) ? string.Empty : leitor.GetString(7),
                DeveTrocarSenha = leitor.GetBoolean(8),
                BloqueadoUsuario = leitor.GetBoolean(9),
                UltimoLoginEm = leitor.IsDBNull(10) ? null : leitor.GetFieldValue<DateTimeOffset>(10),
                SituacaoUsuario = leitor.GetBoolean(11)
            });
        }

        return usuarios;
    }

    public async Task<UsuarioCadastro?> ObterPorLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                   COALESCE(NULLIF(j ->> 'codigo_usuario', ''), NULLIF(j ->> 'id_usuario', ''))::bigint AS codigo_usuario,
                   COALESCE(NULLIF(j ->> 'codigo_cargo', ''), NULLIF(j ->> 'id_cargo', ''))::bigint AS codigo_cargo,
                   COALESCE(NULLIF(j ->> 'codigo_setor_padrao', ''), NULLIF(j ->> 'id_setor_padrao', ''))::bigint AS codigo_setor_padrao,
                   COALESCE(NULLIF(j ->> 'nome_usuario', ''), '') AS nome_usuario,
                   COALESCE(NULLIF(j ->> 'login_usuario', ''), '') AS login_usuario,
                   COALESCE(NULLIF(j ->> 'email_usuario', ''), '') AS email_usuario,
                   COALESCE(NULLIF(j ->> 'senha_hash', ''), '') AS senha_hash,
                   COALESCE(NULLIF(j ->> 'telefone_usuario', ''), '') AS telefone_usuario,
                   COALESCE(NULLIF(j ->> 'deve_trocar_senha', ''), 'false')::boolean AS deve_trocar_senha,
                   COALESCE(NULLIF(j ->> 'bloqueado_usuario', ''), 'false')::boolean AS bloqueado_usuario,
                   NULLIF(j ->> 'ultimo_login_em', '')::timestamptz AS ultimo_login_em,
                   COALESCE(NULLIF(j ->> 'situacao_usuario', ''), 'true')::boolean AS situacao_usuario
            FROM usuario u
            CROSS JOIN LATERAL to_jsonb(u) AS j
            WHERE lower(COALESCE(NULLIF(j ->> 'login_usuario', ''), '')) = lower(@login_usuario)
            LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@login_usuario", login));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UsuarioCadastro
        {
            Id = leitor.GetInt64(0),
            IdCargo = leitor.IsDBNull(1) ? null : leitor.GetInt64(1),
            IdSetorPadrao = leitor.IsDBNull(2) ? null : leitor.GetInt64(2),
            NomeUsuario = leitor.GetString(3),
            LoginUsuario = leitor.GetString(4),
            EmailUsuario = leitor.IsDBNull(5) ? string.Empty : leitor.GetString(5),
            SenhaHash = leitor.GetString(6),
            TelefoneUsuario = leitor.IsDBNull(7) ? string.Empty : leitor.GetString(7),
            DeveTrocarSenha = leitor.GetBoolean(8),
            BloqueadoUsuario = leitor.GetBoolean(9),
            UltimoLoginEm = leitor.IsDBNull(10) ? null : leitor.GetFieldValue<DateTimeOffset>(10),
            SituacaoUsuario = leitor.GetBoolean(11)
        };
    }

    public virtual async Task<UsuarioCadastro?> ObterPorIdAsync(long codigoUsuario, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                   COALESCE(NULLIF(j ->> 'codigo_usuario', ''), NULLIF(j ->> 'id_usuario', ''))::bigint AS codigo_usuario,
                   COALESCE(NULLIF(j ->> 'codigo_cargo', ''), NULLIF(j ->> 'id_cargo', ''))::bigint AS codigo_cargo,
                   COALESCE(NULLIF(j ->> 'codigo_setor_padrao', ''), NULLIF(j ->> 'id_setor_padrao', ''))::bigint AS codigo_setor_padrao,
                   COALESCE(NULLIF(j ->> 'nome_usuario', ''), '') AS nome_usuario,
                   COALESCE(NULLIF(j ->> 'login_usuario', ''), '') AS login_usuario,
                   COALESCE(NULLIF(j ->> 'email_usuario', ''), '') AS email_usuario,
                   COALESCE(NULLIF(j ->> 'senha_hash', ''), '') AS senha_hash,
                   COALESCE(NULLIF(j ->> 'telefone_usuario', ''), '') AS telefone_usuario,
                   COALESCE(NULLIF(j ->> 'deve_trocar_senha', ''), 'false')::boolean AS deve_trocar_senha,
                   COALESCE(NULLIF(j ->> 'bloqueado_usuario', ''), 'false')::boolean AS bloqueado_usuario,
                   NULLIF(j ->> 'ultimo_login_em', '')::timestamptz AS ultimo_login_em,
                   COALESCE(NULLIF(j ->> 'situacao_usuario', ''), 'true')::boolean AS situacao_usuario
            FROM usuario u
            CROSS JOIN LATERAL to_jsonb(u) AS j
            WHERE COALESCE(NULLIF(j ->> 'codigo_usuario', ''), NULLIF(j ->> 'id_usuario', ''))::bigint = @codigo_usuario
            LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearUsuario(leitor);
    }

    public virtual async Task<bool> ExisteLoginAsync(string login, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        // Espelha uq_usuario_login (upper(trim(login)) WHERE situacao_usuario = true).
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM usuario
                WHERE upper(trim(login_usuario)) = upper(trim(@login_usuario))
                  AND situacao_usuario = true
                  AND (@ignorar_codigo IS NULL OR codigo_usuario <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@login_usuario", login));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<long> InserirAsync(UsuarioCadastro usuario, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO usuario
            (codigo_cargo, codigo_setor_padrao, nome_usuario, login_usuario, email_usuario, senha_hash, telefone_usuario, deve_trocar_senha, bloqueado_usuario, situacao_usuario, usuario_criado_por)
            VALUES
            (@codigo_cargo, @codigo_setor_padrao, @nome_usuario, @login_usuario, @email_usuario, @senha_hash, @telefone_usuario, @deve_trocar_senha, @bloqueado_usuario, @situacao_usuario, @usuario_criado_por)
            RETURNING codigo_usuario;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongoNulo("@codigo_cargo", usuario.IdCargo));
            comando.Parameters.Add(ParametroLongoNulo("@codigo_setor_padrao", usuario.IdSetorPadrao));
            comando.Parameters.Add(ParametroTexto("@nome_usuario", usuario.NomeUsuario));
            comando.Parameters.Add(ParametroTexto("@login_usuario", usuario.LoginUsuario));
            comando.Parameters.Add(new NpgsqlParameter("@email_usuario", string.IsNullOrWhiteSpace(usuario.EmailUsuario) ? DBNull.Value : usuario.EmailUsuario));
            comando.Parameters.Add(ParametroTexto("@senha_hash", usuario.SenhaHash));
            comando.Parameters.Add(new NpgsqlParameter("@telefone_usuario", string.IsNullOrWhiteSpace(usuario.TelefoneUsuario) ? DBNull.Value : usuario.TelefoneUsuario));
            comando.Parameters.Add(ParametroBooleano("@deve_trocar_senha", usuario.DeveTrocarSenha));
            comando.Parameters.Add(ParametroBooleano("@bloqueado_usuario", usuario.BloqueadoUsuario));
            comando.Parameters.Add(ParametroBooleano("@situacao_usuario", usuario.SituacaoUsuario));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_criado_por", ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    public async Task<long> InserirComVinculosAsync(
        UsuarioCadastro usuario,
        long codigoPerfilAcesso,
        long codigoSetor,
        CancellationToken cancellationToken = default)
    {
        const string sqlUsuario = """
            INSERT INTO usuario
            (codigo_cargo, codigo_setor_padrao, nome_usuario, login_usuario, email_usuario, senha_hash, telefone_usuario, deve_trocar_senha, bloqueado_usuario, situacao_usuario, usuario_criado_por)
            VALUES
            (@codigo_cargo, @codigo_setor_padrao, @nome_usuario, @login_usuario, @email_usuario, @senha_hash, @telefone_usuario, @deve_trocar_senha, @bloqueado_usuario, @situacao_usuario, @usuario_criado_por)
            RETURNING codigo_usuario;
            """;

        const string sqlPerfil = """
            INSERT INTO usuario_perfil
            (codigo_usuario, codigo_perfil_acesso, situacao_usuario_perfil, usuario_perfil_criado_por)
            VALUES
            (@codigo_usuario, @codigo_perfil_acesso, true, @usuario_perfil_criado_por);
            """;

        const string sqlSetor = """
            INSERT INTO usuario_setor
            (codigo_usuario, codigo_setor, setor_padrao, situacao_usuario_setor, usuario_setor_criado_por)
            VALUES
            (@codigo_usuario, @codigo_setor, true, true, @usuario_setor_criado_por);
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long? codigoUsuarioSessao = ObterCodigoUsuarioSessao();
            long codigoUsuario;

            await using (NpgsqlCommand comandoUsuario = new(sqlUsuario, conexao, transacao))
            {
                PreencherParametrosCadastro(comandoUsuario, usuario);
                comandoUsuario.Parameters.Add(ParametroLongoNulo("@usuario_criado_por", codigoUsuarioSessao));

                object? retorno = await comandoUsuario.ExecuteScalarAsync(cancellationToken);
                codigoUsuario = retorno is long id ? id : 0;
            }

            if (codigoUsuario <= 0)
            {
                return 0;
            }

            await using (NpgsqlCommand comandoPerfil = new(sqlPerfil, conexao, transacao))
            {
                comandoPerfil.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
                comandoPerfil.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilAcesso));
                comandoPerfil.Parameters.Add(ParametroLongoNulo("@usuario_perfil_criado_por", codigoUsuarioSessao));
                await comandoPerfil.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (NpgsqlCommand comandoSetor = new(sqlSetor, conexao, transacao))
            {
                comandoSetor.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
                comandoSetor.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
                comandoSetor.Parameters.Add(ParametroLongoNulo("@usuario_setor_criado_por", codigoUsuarioSessao));
                await comandoSetor.ExecuteNonQueryAsync(cancellationToken);
            }

            return codigoUsuario;
        }, cancellationToken);
    }

    public async Task AtualizarUltimoLoginAsync(long codigoUsuario, CancellationToken cancellationToken = default)
    {
        // Define app.usuario_id = proprio usuario sendo logado, para o trigger
        // de log_alteracao_cadastral identificar quem provocou o UPDATE.
        const string sql = """
            UPDATE usuario
               SET ultimo_login_em = date_trunc('second', now()),
                   usuario_atualizado_por = @codigo_usuario
             WHERE codigo_usuario = @codigo_usuario;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);

        try
        {
            await DefinirUsuarioAppAsync(conexao, transacao, codigoUsuario, cancellationToken);
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
            await comando.ExecuteNonQueryAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> AtualizarAsync(UsuarioCadastro usuario, CancellationToken cancellationToken = default)
    {
        // Nao altera senha_hash aqui (use AtualizarSenhaAsync). atualizado_em fica para o trigger.
        const string sql = """
            UPDATE usuario
               SET codigo_cargo = @codigo_cargo,
                   codigo_setor_padrao = @codigo_setor_padrao,
                   nome_usuario = @nome_usuario,
                   login_usuario = @login_usuario,
                   email_usuario = @email_usuario,
                   telefone_usuario = @telefone_usuario,
                   deve_trocar_senha = @deve_trocar_senha,
                   bloqueado_usuario = @bloqueado_usuario,
                   situacao_usuario = @situacao_usuario,
                   usuario_atualizado_por = @usuario_atualizado_por
             WHERE codigo_usuario = @codigo_usuario;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
            comando.Parameters.Add(ParametroLongoNulo("@codigo_cargo", usuario.IdCargo));
            comando.Parameters.Add(ParametroLongoNulo("@codigo_setor_padrao", usuario.IdSetorPadrao));
            comando.Parameters.Add(ParametroTexto("@nome_usuario", usuario.NomeUsuario));
            comando.Parameters.Add(ParametroTexto("@login_usuario", usuario.LoginUsuario));
            comando.Parameters.Add(new NpgsqlParameter("@email_usuario", string.IsNullOrWhiteSpace(usuario.EmailUsuario) ? DBNull.Value : usuario.EmailUsuario));
            comando.Parameters.Add(new NpgsqlParameter("@telefone_usuario", string.IsNullOrWhiteSpace(usuario.TelefoneUsuario) ? DBNull.Value : usuario.TelefoneUsuario));
            comando.Parameters.Add(ParametroBooleano("@deve_trocar_senha", usuario.DeveTrocarSenha));
            comando.Parameters.Add(ParametroBooleano("@bloqueado_usuario", usuario.BloqueadoUsuario));
            comando.Parameters.Add(ParametroBooleano("@situacao_usuario", usuario.SituacaoUsuario));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public virtual async Task<int> AtualizarComVinculosAsync(
        UsuarioCadastro usuario,
        long codigoPerfilAcesso,
        long codigoSetor,
        CancellationToken cancellationToken = default)
    {
        // Nao altera senha_hash aqui (use AtualizarSenhaAsync). atualizado_em fica para o trigger.
        const string sqlUsuario = """
            UPDATE usuario
               SET codigo_cargo = @codigo_cargo,
                   codigo_setor_padrao = @codigo_setor_padrao,
                   nome_usuario = @nome_usuario,
                   login_usuario = @login_usuario,
                   email_usuario = @email_usuario,
                   telefone_usuario = @telefone_usuario,
                   deve_trocar_senha = @deve_trocar_senha,
                   bloqueado_usuario = @bloqueado_usuario,
                   situacao_usuario = @situacao_usuario,
                   usuario_atualizado_por = @usuario_atualizado_por
             WHERE codigo_usuario = @codigo_usuario;
            """;

        const string sqlInativarPerfis = """
            UPDATE usuario_perfil
               SET situacao_usuario_perfil = false,
                   usuario_perfil_atualizado_por = @usuario_perfil_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND situacao_usuario_perfil = true;
            """;

        const string sqlReativarPerfil = """
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

        const string sqlInserirPerfil = """
            INSERT INTO usuario_perfil (codigo_usuario, codigo_perfil_acesso, situacao_usuario_perfil, usuario_perfil_criado_por)
            VALUES (@codigo_usuario, @codigo_perfil_acesso, true, @usuario_perfil_criado_por);
            """;

        const string sqlLimparSetoresPadrao = """
            UPDATE usuario_setor
               SET setor_padrao = false,
                   usuario_setor_atualizado_por = @usuario_setor_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND setor_padrao = true;
            """;

        const string sqlReativarOuDefinirSetor = """
            WITH selecionado AS (
                SELECT codigo_usuario_setor
                  FROM usuario_setor
                 WHERE codigo_usuario = @codigo_usuario
                   AND codigo_setor = @codigo_setor
                 ORDER BY situacao_usuario_setor DESC, codigo_usuario_setor DESC
                 LIMIT 1
            )
            UPDATE usuario_setor us
               SET situacao_usuario_setor = true,
                   setor_padrao = true,
                   usuario_setor_atualizado_por = @usuario_setor_atualizado_por
              FROM selecionado
             WHERE us.codigo_usuario_setor = selecionado.codigo_usuario_setor
            RETURNING us.codigo_usuario_setor;
            """;

        const string sqlInserirSetor = """
            INSERT INTO usuario_setor (codigo_usuario, codigo_setor, setor_padrao, situacao_usuario_setor, usuario_setor_criado_por)
            VALUES (@codigo_usuario, @codigo_setor, true, true, @usuario_setor_criado_por);
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long? codigoUsuarioSessao = ObterCodigoUsuarioSessao();

            await using (NpgsqlCommand comandoUsuario = new(sqlUsuario, conexao, transacao))
            {
                comandoUsuario.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                comandoUsuario.Parameters.Add(ParametroLongoNulo("@codigo_cargo", usuario.IdCargo));
                comandoUsuario.Parameters.Add(ParametroLongoNulo("@codigo_setor_padrao", codigoSetor));
                comandoUsuario.Parameters.Add(ParametroTexto("@nome_usuario", usuario.NomeUsuario));
                comandoUsuario.Parameters.Add(ParametroTexto("@login_usuario", usuario.LoginUsuario));
                comandoUsuario.Parameters.Add(new NpgsqlParameter("@email_usuario", string.IsNullOrWhiteSpace(usuario.EmailUsuario) ? DBNull.Value : usuario.EmailUsuario));
                comandoUsuario.Parameters.Add(new NpgsqlParameter("@telefone_usuario", string.IsNullOrWhiteSpace(usuario.TelefoneUsuario) ? DBNull.Value : usuario.TelefoneUsuario));
                comandoUsuario.Parameters.Add(ParametroBooleano("@deve_trocar_senha", usuario.DeveTrocarSenha));
                comandoUsuario.Parameters.Add(ParametroBooleano("@bloqueado_usuario", usuario.BloqueadoUsuario));
                comandoUsuario.Parameters.Add(ParametroBooleano("@situacao_usuario", usuario.SituacaoUsuario));
                comandoUsuario.Parameters.Add(ParametroLongoNulo("@usuario_atualizado_por", codigoUsuarioSessao));

                int atualizados = await comandoUsuario.ExecuteNonQueryAsync(cancellationToken);
                if (atualizados <= 0)
                {
                    return 0;
                }
            }

            await using (NpgsqlCommand comandoInativarPerfis = new(sqlInativarPerfis, conexao, transacao))
            {
                comandoInativarPerfis.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                comandoInativarPerfis.Parameters.Add(ParametroLongoNulo("@usuario_perfil_atualizado_por", codigoUsuarioSessao));
                await comandoInativarPerfis.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (NpgsqlCommand comandoReativarPerfil = new(sqlReativarPerfil, conexao, transacao))
            {
                comandoReativarPerfil.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                comandoReativarPerfil.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilAcesso));
                comandoReativarPerfil.Parameters.Add(ParametroLongoNulo("@usuario_perfil_atualizado_por", codigoUsuarioSessao));

                object? perfilReativado = await comandoReativarPerfil.ExecuteScalarAsync(cancellationToken);
                if (perfilReativado is not long)
                {
                    await using NpgsqlCommand comandoInserirPerfil = new(sqlInserirPerfil, conexao, transacao);
                    comandoInserirPerfil.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                    comandoInserirPerfil.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilAcesso));
                    comandoInserirPerfil.Parameters.Add(ParametroLongoNulo("@usuario_perfil_criado_por", codigoUsuarioSessao));
                    await comandoInserirPerfil.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await using (NpgsqlCommand comandoLimparSetores = new(sqlLimparSetoresPadrao, conexao, transacao))
            {
                comandoLimparSetores.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                comandoLimparSetores.Parameters.Add(ParametroLongoNulo("@usuario_setor_atualizado_por", codigoUsuarioSessao));
                await comandoLimparSetores.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (NpgsqlCommand comandoDefinirSetor = new(sqlReativarOuDefinirSetor, conexao, transacao))
            {
                comandoDefinirSetor.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                comandoDefinirSetor.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
                comandoDefinirSetor.Parameters.Add(ParametroLongoNulo("@usuario_setor_atualizado_por", codigoUsuarioSessao));

                object? setorDefinido = await comandoDefinirSetor.ExecuteScalarAsync(cancellationToken);
                if (setorDefinido is not long)
                {
                    await using NpgsqlCommand comandoInserirSetor = new(sqlInserirSetor, conexao, transacao);
                    comandoInserirSetor.Parameters.Add(ParametroLongo("@codigo_usuario", usuario.IdUsuario));
                    comandoInserirSetor.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
                    comandoInserirSetor.Parameters.Add(ParametroLongoNulo("@usuario_setor_criado_por", codigoUsuarioSessao));
                    await comandoInserirSetor.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            return 1;
        }, cancellationToken);
    }

    public async Task<int> AtualizarSenhaAsync(long codigoUsuario, string senhaHash, bool deveTrocarSenha, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario
               SET senha_hash = @senha_hash,
                   deve_trocar_senha = @deve_trocar_senha,
                   usuario_atualizado_por = @usuario_atualizado_por
             WHERE codigo_usuario = @codigo_usuario;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
            comando.Parameters.Add(ParametroTexto("@senha_hash", senhaHash));
            comando.Parameters.Add(ParametroBooleano("@deve_trocar_senha", deveTrocarSenha));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> BloquearAsync(long codigoUsuario, CancellationToken cancellationToken = default)
        => AtualizarBloqueioAsync(codigoUsuario, true, cancellationToken);

    public Task<int> DesbloquearAsync(long codigoUsuario, CancellationToken cancellationToken = default)
        => AtualizarBloqueioAsync(codigoUsuario, false, cancellationToken);

    private async Task<int> AtualizarBloqueioAsync(long codigoUsuario, bool bloqueado, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario
               SET bloqueado_usuario = @bloqueado_usuario,
                   usuario_atualizado_por = @usuario_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND bloqueado_usuario <> @bloqueado_usuario;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
            comando.Parameters.Add(ParametroBooleano("@bloqueado_usuario", bloqueado));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Soft-delete: marca situacao_usuario = false (trigger registra DELETE_LOGICO).
    /// Nao faz DELETE fisico para preservar historico e nao violar FKs (usuario_perfil/usuario_setor).
    /// </summary>
    public async Task<int> ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario
               SET situacao_usuario = false,
                   usuario_atualizado_por = @usuario_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND situacao_usuario = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", id));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReativarAsync(long id, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario
               SET situacao_usuario = true,
                   usuario_atualizado_por = @usuario_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND situacao_usuario = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", id));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void PreencherParametrosCadastro(NpgsqlCommand comando, UsuarioCadastro usuario)
    {
        comando.Parameters.Add(ParametroLongoNulo("@codigo_cargo", usuario.IdCargo));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_setor_padrao", usuario.IdSetorPadrao));
        comando.Parameters.Add(ParametroTexto("@nome_usuario", usuario.NomeUsuario));
        comando.Parameters.Add(ParametroTexto("@login_usuario", usuario.LoginUsuario));
        comando.Parameters.Add(new NpgsqlParameter("@email_usuario", string.IsNullOrWhiteSpace(usuario.EmailUsuario) ? DBNull.Value : usuario.EmailUsuario));
        comando.Parameters.Add(ParametroTexto("@senha_hash", usuario.SenhaHash));
        comando.Parameters.Add(new NpgsqlParameter("@telefone_usuario", string.IsNullOrWhiteSpace(usuario.TelefoneUsuario) ? DBNull.Value : usuario.TelefoneUsuario));
        comando.Parameters.Add(ParametroBooleano("@deve_trocar_senha", usuario.DeveTrocarSenha));
        comando.Parameters.Add(ParametroBooleano("@bloqueado_usuario", usuario.BloqueadoUsuario));
        comando.Parameters.Add(ParametroBooleano("@situacao_usuario", usuario.SituacaoUsuario));
    }

    private static UsuarioCadastro MapearUsuario(NpgsqlDataReader leitor)
        => new()
        {
            Id = leitor.GetInt64(0),
            IdCargo = leitor.IsDBNull(1) ? null : leitor.GetInt64(1),
            IdSetorPadrao = leitor.IsDBNull(2) ? null : leitor.GetInt64(2),
            NomeUsuario = leitor.GetString(3),
            LoginUsuario = leitor.GetString(4),
            EmailUsuario = leitor.IsDBNull(5) ? string.Empty : leitor.GetString(5),
            SenhaHash = leitor.GetString(6),
            TelefoneUsuario = leitor.IsDBNull(7) ? string.Empty : leitor.GetString(7),
            DeveTrocarSenha = leitor.GetBoolean(8),
            BloqueadoUsuario = leitor.GetBoolean(9),
            UltimoLoginEm = leitor.IsDBNull(10) ? null : leitor.GetFieldValue<DateTimeOffset>(10),
            SituacaoUsuario = leitor.GetBoolean(11)
        };
}







