using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Servicos.Seguranca;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class PerfilPermissaoRepositorio : RepositorioBase
{
    public const string MensagemProtecaoPerfilAdministrador = "Operação bloqueada. O sistema precisa manter pelo menos um perfil com permissões administrativas essenciais.";

    private static readonly PermissaoEssencial[] PermissoesAdministrativasEssenciais =
    [
        new(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.PerfilAcesso, PermissoesSistema.Acoes.Gerenciar),
        new(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Permissao, PermissoesSistema.Acoes.Gerenciar),
        new(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Consultar),
        new(PermissoesSistema.Modulos.Seguranca, PermissoesSistema.Rotinas.Usuario, PermissoesSistema.Acoes.Editar),
    ];

    public PerfilPermissaoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<PermissaoSessaoAplicacao>> ListarPermissoesAtivasPorUsuarioAsync(
        long codigoUsuario,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT
                   pe.modulo_permissao,
                   pe.rotina_permissao,
                   pe.acao_permissao
              FROM usuario_perfil up
              JOIN perfil_acesso pa
                ON COALESCE(NULLIF(to_jsonb(pa) ->> 'codigo_perfil_acesso', ''), NULLIF(to_jsonb(pa) ->> 'id_perfil_acesso', ''))::bigint
                 = COALESCE(NULLIF(to_jsonb(up) ->> 'codigo_perfil_acesso', ''), NULLIF(to_jsonb(up) ->> 'id_perfil_acesso', ''))::bigint
              JOIN perfil_permissao pp
                ON COALESCE(NULLIF(to_jsonb(pp) ->> 'codigo_perfil_acesso', ''), NULLIF(to_jsonb(pp) ->> 'id_perfil_acesso', ''))::bigint
                 = COALESCE(NULLIF(to_jsonb(pa) ->> 'codigo_perfil_acesso', ''), NULLIF(to_jsonb(pa) ->> 'id_perfil_acesso', ''))::bigint
              JOIN permissao pe
                ON COALESCE(NULLIF(to_jsonb(pe) ->> 'codigo_permissao', ''), NULLIF(to_jsonb(pe) ->> 'id_permissao', ''))::bigint
                 = COALESCE(NULLIF(to_jsonb(pp) ->> 'codigo_permissao', ''), NULLIF(to_jsonb(pp) ->> 'id_permissao', ''))::bigint
             WHERE COALESCE(NULLIF(to_jsonb(up) ->> 'codigo_usuario', ''), NULLIF(to_jsonb(up) ->> 'id_usuario', ''))::bigint = @codigo_usuario
               AND COALESCE(NULLIF(to_jsonb(up) ->> 'situacao_usuario_perfil', ''), 'true')::boolean = true
               AND COALESCE(NULLIF(to_jsonb(pa) ->> 'situacao_perfil_acesso', ''), 'true')::boolean = true
               AND COALESCE(NULLIF(to_jsonb(pp) ->> 'situacao_perfil_permissao', ''), 'true')::boolean = true
               AND COALESCE(NULLIF(to_jsonb(pe) ->> 'situacao_permissao', ''), 'true')::boolean = true
             ORDER BY pe.modulo_permissao, pe.rotina_permissao, pe.acao_permissao;
            """;

        List<PermissaoSessaoAplicacao> permissoes = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", codigoUsuario));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            permissoes.Add(new PermissaoSessaoAplicacao
            {
                Modulo = leitor.GetString(0),
                Rotina = leitor.GetString(1),
                Acao = leitor.GetString(2)
            });
        }

        return permissoes;
    }

    public async Task<IReadOnlyList<long>> ListarCodigosPermissaoPorPerfilAsync(
        long codigoPerfil,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_permissao
              FROM perfil_permissao
             WHERE codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_perfil_permissao = true
             ORDER BY codigo_permissao;
            """;

        List<long> codigos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfil));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            codigos.Add(leitor.GetInt64(0));
        }

        return codigos;
    }

    public Task<int> InativarPermissoesDoPerfilAsync(long codigoPerfil, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE perfil_permissao
               SET situacao_perfil_permissao = false,
                   perfil_permissao_atualizado_por = @perfil_permissao_atualizado_por
             WHERE codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_perfil_permissao = true;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfil));
            comando.Parameters.Add(ParametroLongoNulo("@perfil_permissao_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<long> VincularPermissaoAsync(
        long codigoPerfil,
        long codigoPermissao,
        CancellationToken cancellationToken = default)
    {
        return ExecutarEmTransacaoAuditavelAsync(
            (conexao, transacao) => VincularPermissaoInternoAsync(conexao, transacao, codigoPerfil, codigoPermissao, cancellationToken),
            cancellationToken);
    }

    public Task<int> ReativarPermissaoAsync(
        long codigoPerfil,
        long codigoPermissao,
        CancellationToken cancellationToken = default)
    {
        return ExecutarEmTransacaoAuditavelAsync(
            (conexao, transacao) => ReativarPermissaoInternoAsync(conexao, transacao, codigoPerfil, codigoPermissao, cancellationToken),
            cancellationToken);
    }

    public Task SincronizarPermissoesAsync(
        long codigoPerfil,
        IReadOnlyCollection<long> permissoesSelecionadas,
        CancellationToken cancellationToken = default)
    {
        long[] codigosSelecionados = permissoesSelecionadas
            .Where(codigo => codigo > 0)
            .Distinct()
            .OrderBy(codigo => codigo)
            .ToArray();

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            bool mantemPerfilAdministrador = await MantemPerfilComPermissoesAdministrativasEssenciaisAsync(
                conexao,
                transacao,
                codigoPerfil,
                codigosSelecionados,
                cancellationToken);

            if (!mantemPerfilAdministrador)
            {
                throw new RemocaoPermissaoAdministrativaEssencialException();
            }

            await InativarPermissoesNaoSelecionadasAsync(conexao, transacao, codigoPerfil, codigosSelecionados, cancellationToken);

            foreach (long codigoPermissao in codigosSelecionados)
            {
                int reativados = await ReativarPermissaoInternoAsync(conexao, transacao, codigoPerfil, codigoPermissao, cancellationToken);
                if (reativados <= 0)
                {
                    await VincularPermissaoInternoAsync(conexao, transacao, codigoPerfil, codigoPermissao, cancellationToken);
                }
            }

            return true;
        }, cancellationToken);
    }

    private readonly record struct PermissaoEssencial(string Modulo, string Rotina, string Acao);

    private static async Task<bool> MantemPerfilComPermissoesAdministrativasEssenciaisAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoPerfilEditado,
        long[] codigosSelecionados,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH permissoes_essenciais_configuradas AS (
                SELECT modulo_permissao,
                       rotina_permissao,
                       acao_permissao
                  FROM unnest(@modulos_permissao, @rotinas_permissao, @acoes_permissao)
                       AS permissao(modulo_permissao, rotina_permissao, acao_permissao)
            ), permissoes_essenciais AS (
                SELECT DISTINCT p.codigo_permissao
                  FROM permissao p
                  JOIN permissoes_essenciais_configuradas pec
                    ON pec.modulo_permissao = p.modulo_permissao
                   AND pec.rotina_permissao = p.rotina_permissao
                   AND pec.acao_permissao = p.acao_permissao
                 WHERE p.situacao_permissao = true
            ), quantidade_essencial AS (
                SELECT count(*)::int AS total
                  FROM permissoes_essenciais
            ), perfil_editado_valido AS (
                SELECT 1
                  FROM perfil_acesso pa
                 WHERE pa.codigo_perfil_acesso = @codigo_perfil_acesso
                   AND pa.situacao_perfil_acesso = true
                   AND (SELECT total FROM quantidade_essencial) = @total_permissoes_essenciais
                   AND NOT EXISTS (
                       SELECT 1
                         FROM permissoes_essenciais pe
                        WHERE NOT (pe.codigo_permissao = ANY(@codigos_permissao))
                   )
            ), outros_perfis_validos AS (
                SELECT pa.codigo_perfil_acesso
                  FROM perfil_acesso pa
                  JOIN perfil_permissao pp
                    ON pp.codigo_perfil_acesso = pa.codigo_perfil_acesso
                   AND pp.situacao_perfil_permissao = true
                  JOIN permissoes_essenciais pe
                    ON pe.codigo_permissao = pp.codigo_permissao
                 WHERE pa.situacao_perfil_acesso = true
                   AND pa.codigo_perfil_acesso <> @codigo_perfil_acesso
                 GROUP BY pa.codigo_perfil_acesso
                HAVING count(DISTINCT pe.codigo_permissao) = (SELECT total FROM quantidade_essencial)
                   AND (SELECT total FROM quantidade_essencial) = @total_permissoes_essenciais
            )
            SELECT EXISTS (SELECT 1 FROM perfil_editado_valido)
                OR EXISTS (SELECT 1 FROM outros_perfis_validos);
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilEditado));
        comando.Parameters.Add(ParametroInteiro("@total_permissoes_essenciais", PermissoesAdministrativasEssenciais.Length));
        comando.Parameters.Add(new NpgsqlParameter<string[]>("@modulos_permissao", PermissoesAdministrativasEssenciais.Select(permissao => permissao.Modulo).ToArray())
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        comando.Parameters.Add(new NpgsqlParameter<string[]>("@rotinas_permissao", PermissoesAdministrativasEssenciais.Select(permissao => permissao.Rotina).ToArray())
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        comando.Parameters.Add(new NpgsqlParameter<string[]>("@acoes_permissao", PermissoesAdministrativasEssenciais.Select(permissao => permissao.Acao).ToArray())
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text
        });
        comando.Parameters.Add(new NpgsqlParameter<long[]>("@codigos_permissao", codigosSelecionados)
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Bigint
        });

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool mantem && mantem;
    }

    private async Task<int> InativarPermissoesNaoSelecionadasAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoPerfil,
        long[] codigosSelecionados,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE perfil_permissao
               SET situacao_perfil_permissao = false,
                   perfil_permissao_atualizado_por = @perfil_permissao_atualizado_por
             WHERE codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_perfil_permissao = true
               AND (@total_selecionado = 0 OR NOT (codigo_permissao = ANY(@codigos_permissao)));
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfil));
        comando.Parameters.Add(ParametroLongoNulo("@perfil_permissao_atualizado_por", ObterCodigoUsuarioSessao()));
        comando.Parameters.Add(ParametroInteiro("@total_selecionado", codigosSelecionados.Length));
        comando.Parameters.Add(new NpgsqlParameter<long[]>("@codigos_permissao", codigosSelecionados)
        {
            NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Bigint
        });

        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> ReativarPermissaoInternoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoPerfil,
        long codigoPermissao,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE perfil_permissao
               SET situacao_perfil_permissao = true,
                   perfil_permissao_atualizado_por = @perfil_permissao_atualizado_por
             WHERE codigo_perfil_permissao = (
                   SELECT codigo_perfil_permissao
                     FROM perfil_permissao
                    WHERE codigo_perfil_acesso = @codigo_perfil_acesso
                      AND codigo_permissao = @codigo_permissao
                      AND situacao_perfil_permissao = false
                    ORDER BY codigo_perfil_permissao DESC
                    LIMIT 1
             );
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfil));
        comando.Parameters.Add(ParametroLongo("@codigo_permissao", codigoPermissao));
        comando.Parameters.Add(ParametroLongoNulo("@perfil_permissao_atualizado_por", ObterCodigoUsuarioSessao()));
        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<long> VincularPermissaoInternoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoPerfil,
        long codigoPermissao,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO perfil_permissao
                   (codigo_perfil_acesso, codigo_permissao, situacao_perfil_permissao, perfil_permissao_criado_por)
            SELECT @codigo_perfil_acesso, @codigo_permissao, true, @perfil_permissao_criado_por
             WHERE NOT EXISTS (
                   SELECT 1
                     FROM perfil_permissao
                    WHERE codigo_perfil_acesso = @codigo_perfil_acesso
                      AND codigo_permissao = @codigo_permissao
                      AND situacao_perfil_permissao = true
             )
            RETURNING codigo_perfil_permissao;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfil));
        comando.Parameters.Add(ParametroLongo("@codigo_permissao", codigoPermissao));
        comando.Parameters.Add(ParametroLongoNulo("@perfil_permissao_criado_por", ObterCodigoUsuarioSessao()));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long id ? id : 0L;
    }
}
