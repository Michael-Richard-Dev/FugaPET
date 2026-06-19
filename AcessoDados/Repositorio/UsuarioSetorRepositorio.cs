using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class UsuarioSetorRepositorio : RepositorioBase
{
    public UsuarioSetorRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<UsuarioSetorCadastro>> ListarPorUsuarioAsync(long idUsuario, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                COALESCE(NULLIF(to_jsonb(us) ->> 'codigo_usuario_setor', ''), NULLIF(to_jsonb(us) ->> 'id_usuario_setor', ''))::bigint AS codigo_usuario_setor,
                COALESCE(NULLIF(to_jsonb(us) ->> 'codigo_usuario', ''), NULLIF(to_jsonb(us) ->> 'id_usuario', ''))::bigint AS codigo_usuario,
                COALESCE(NULLIF(to_jsonb(us) ->> 'codigo_setor', ''), NULLIF(to_jsonb(us) ->> 'id_setor', ''))::bigint AS codigo_setor,
                setor_padrao,
                situacao_usuario_setor
            FROM usuario_setor us
            WHERE COALESCE(NULLIF(to_jsonb(us) ->> 'codigo_usuario', ''), NULLIF(to_jsonb(us) ->> 'id_usuario', ''))::bigint = @codigo_usuario
            ORDER BY setor_padrao DESC, 1;
            """;

        List<UsuarioSetorCadastro> lista = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            lista.Add(new UsuarioSetorCadastro
            {
                Id = leitor.GetInt64(0),
                IdUsuario = leitor.GetInt64(1),
                IdSetor = leitor.GetInt64(2),
                SetorPadrao = leitor.GetBoolean(3),
                Ativo = leitor.GetBoolean(4)
            });
        }

        return lista;
    }

    /// <summary>
    /// Vincula um setor ao usuario de forma ATOMICA (transacao unica): verifica o vinculo
    /// existente, aplica a exclusividade do setor padrao (limpa o anterior) e entao reativa o
    /// vinculo inativo OU insere um novo — tudo na MESMA transacao. Em falha, rollback total
    /// (nunca deixa "setor padrao limpo" sem o novo vinculo gravado).
    /// </summary>
    public async Task<ResultadoVinculoSetor> VincularComExclusividadeAsync(
        long idUsuario,
        long idSetor,
        bool setorPadrao = false,
        bool ativo = true,
        CancellationToken cancellationToken = default)
    {
        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            bool? situacaoExistente = await ObterSituacaoVinculoAsync(conexao, transacao, idUsuario, idSetor, cancellationToken);

            if (situacaoExistente == true)
            {
                return new ResultadoVinculoSetor(TipoAplicacaoVinculoSetor.JaVinculadoAtivo, 0);
            }

            // Exclusividade: so um setor padrao por usuario.
            if (setorPadrao)
            {
                await LimparSetoresPadraoInternoAsync(conexao, transacao, idUsuario, cancellationToken);
            }

            // Vinculo inativo -> reativa (respeita indice unico parcial).
            if (situacaoExistente == false)
            {
                await ReativarInternoAsync(conexao, transacao, idUsuario, idSetor, cancellationToken);
                if (setorPadrao)
                {
                    await DefinirSetorPadraoInternoAsync(conexao, transacao, idUsuario, idSetor, true, cancellationToken);
                }

                return new ResultadoVinculoSetor(TipoAplicacaoVinculoSetor.Reativado, 0);
            }

            long id = await VincularInternoAsync(conexao, transacao, idUsuario, idSetor, setorPadrao, ativo, cancellationToken);
            return new ResultadoVinculoSetor(TipoAplicacaoVinculoSetor.Inserido, id);
        }, cancellationToken);
    }

    /// <summary>
    /// Define o setor padrao do usuario de forma ATOMICA: valida que o vinculo existe e esta
    /// ativo, limpa o setor padrao anterior e marca o novo, na MESMA transacao.
    /// </summary>
    public async Task<ResultadoDefinirSetorPadrao> DefinirSetorPadraoExclusivoAsync(
        long idUsuario,
        long idSetor,
        CancellationToken cancellationToken = default)
    {
        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            bool? situacao = await ObterSituacaoVinculoAsync(conexao, transacao, idUsuario, idSetor, cancellationToken);
            if (situacao != true)
            {
                // Vinculo inexistente ou inativo: nada e alterado.
                return new ResultadoDefinirSetorPadrao(false, 0);
            }

            await LimparSetoresPadraoInternoAsync(conexao, transacao, idUsuario, cancellationToken);
            int atualizados = await DefinirSetorPadraoInternoAsync(conexao, transacao, idUsuario, idSetor, true, cancellationToken);
            return new ResultadoDefinirSetorPadrao(true, atualizados);
        }, cancellationToken);
    }

    /// <summary>
    /// Soft-delete do vinculo (situacao_usuario_setor = false). Tambem zera setor_padrao
    /// para nao deixar um setor padrao apontando para vinculo inativo.
    /// </summary>
    public async Task<int> RemoverAsync(long idUsuario, long idSetor, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE usuario_setor
               SET situacao_usuario_setor = false,
                   setor_padrao = false,
                   usuario_setor_atualizado_por = @usuario_setor_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND codigo_setor = @codigo_setor
               AND situacao_usuario_setor = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
            comando.Parameters.Add(ParametroLongo("@codigo_setor", idSetor));
            comando.Parameters.Add(ParametroLongoNulo("@usuario_setor_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Helpers internos: executam UM statement na conexao/transacao recebida.
    // Compoem os metodos transacionais acima sem abrir novas transacoes.
    // ---------------------------------------------------------------------

    private async Task<bool?> ObterSituacaoVinculoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long idUsuario,
        long idSetor,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT situacao_usuario_setor
              FROM usuario_setor
             WHERE codigo_usuario = @codigo_usuario
               AND codigo_setor = @codigo_setor
             LIMIT 1;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        comando.Parameters.Add(ParametroLongo("@codigo_setor", idSetor));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool situacao ? situacao : null;
    }

    private async Task<long> VincularInternoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long idUsuario,
        long idSetor,
        bool setorPadrao,
        bool ativo,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO usuario_setor (codigo_usuario, codigo_setor, setor_padrao, situacao_usuario_setor, usuario_setor_criado_por)
            VALUES (@codigo_usuario, @codigo_setor, @setor_padrao, @situacao_usuario_setor, @usuario_setor_criado_por)
            RETURNING codigo_usuario_setor;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        comando.Parameters.Add(ParametroLongo("@codigo_setor", idSetor));
        comando.Parameters.Add(ParametroBooleano("@setor_padrao", setorPadrao));
        comando.Parameters.Add(ParametroBooleano("@situacao_usuario_setor", ativo));
        comando.Parameters.Add(ParametroLongoNulo("@usuario_setor_criado_por", ObterCodigoUsuarioSessao()));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long id ? id : 0;
    }

    private async Task<int> ReativarInternoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long idUsuario,
        long idSetor,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE usuario_setor
               SET situacao_usuario_setor = true,
                   usuario_setor_atualizado_por = @usuario_setor_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND codigo_setor = @codigo_setor
               AND situacao_usuario_setor = false;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        comando.Parameters.Add(ParametroLongo("@codigo_setor", idSetor));
        comando.Parameters.Add(ParametroLongoNulo("@usuario_setor_atualizado_por", ObterCodigoUsuarioSessao()));
        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> DefinirSetorPadraoInternoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long idUsuario,
        long idSetor,
        bool setorPadrao,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE usuario_setor
               SET setor_padrao = @setor_padrao,
                   usuario_setor_atualizado_por = @usuario_setor_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND codigo_setor = @codigo_setor;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroBooleano("@setor_padrao", setorPadrao));
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        comando.Parameters.Add(ParametroLongo("@codigo_setor", idSetor));
        comando.Parameters.Add(ParametroLongoNulo("@usuario_setor_atualizado_por", ObterCodigoUsuarioSessao()));
        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<int> LimparSetoresPadraoInternoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long idUsuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE usuario_setor
               SET setor_padrao = false,
                   usuario_setor_atualizado_por = @usuario_setor_atualizado_por
             WHERE codigo_usuario = @codigo_usuario
               AND setor_padrao = true;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_usuario", idUsuario));
        comando.Parameters.Add(ParametroLongoNulo("@usuario_setor_atualizado_por", ObterCodigoUsuarioSessao()));
        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}

/// <summary>Tipo de operacao aplicada por <see cref="UsuarioSetorRepositorio.VincularComExclusividadeAsync"/>.</summary>
public enum TipoAplicacaoVinculoSetor
{
    JaVinculadoAtivo,
    Inserido,
    Reativado
}

/// <summary>Resultado atomico do vinculo de setor (tipo aplicado + id quando inserido).</summary>
public readonly record struct ResultadoVinculoSetor(TipoAplicacaoVinculoSetor Tipo, long IdVinculo);

/// <summary>Resultado atomico de definir setor padrao (se achou vinculo ativo + linhas marcadas).</summary>
public readonly record struct ResultadoDefinirSetorPadrao(bool VinculoAtivoEncontrado, int Atualizados);
