using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class PermissaoRepositorio : RepositorioBase
{
    public PermissaoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<PermissaoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_permissao, modulo_permissao, rotina_permissao, acao_permissao,
                   descricao_permissao, situacao_permissao, permissao_criado_em
            FROM permissao
            ORDER BY modulo_permissao, rotina_permissao, acao_permissao;
            """;

        List<PermissaoCadastro> permissoes = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            permissoes.Add(MapearPermissao(leitor));
        }

        return permissoes;
    }

    public async Task<PermissaoCadastro?> ObterPorIdAsync(long codigoPermissao, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_permissao, modulo_permissao, rotina_permissao, acao_permissao,
                   descricao_permissao, situacao_permissao, permissao_criado_em
            FROM permissao
            WHERE codigo_permissao = @codigo_permissao;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_permissao", codigoPermissao));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearPermissao(leitor);
    }

    /// <summary>
    /// Verifica se ja existe permissao ativa com a mesma combinacao modulo+rotina+acao.
    /// Espelha o indice unique uq_permissao_modulo_rotina_acao (filtra ativos, case-insensitive, trim).
    /// </summary>
    public async Task<bool> ExisteCombinacaoAsync(
        string modulo, string rotina, string acao, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM permissao
                WHERE upper(trim(modulo_permissao)) = upper(trim(@modulo_permissao))
                  AND upper(trim(rotina_permissao)) = upper(trim(@rotina_permissao))
                  AND upper(trim(acao_permissao)) = upper(trim(@acao_permissao))
                  AND situacao_permissao = true
                  AND (@ignorar_codigo IS NULL OR codigo_permissao <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@modulo_permissao", modulo));
        comando.Parameters.Add(ParametroTexto("@rotina_permissao", rotina));
        comando.Parameters.Add(ParametroTexto("@acao_permissao", acao));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public Task<long> InserirAsync(PermissaoCadastro permissao, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO permissao
            (modulo_permissao, rotina_permissao, acao_permissao, descricao_permissao, situacao_permissao, permissao_criado_por)
            VALUES (@modulo_permissao, @rotina_permissao, @acao_permissao, @descricao_permissao, @situacao_permissao, @permissao_criado_por)
            RETURNING codigo_permissao;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroTexto("@modulo_permissao", permissao.ModuloPermissao));
            comando.Parameters.Add(ParametroTexto("@rotina_permissao", permissao.RotinaPermissao));
            comando.Parameters.Add(ParametroTexto("@acao_permissao", permissao.AcaoPermissao));
            comando.Parameters.Add(ParametroTexto("@descricao_permissao", permissao.DescricaoPermissao));
            comando.Parameters.Add(ParametroBooleano("@situacao_permissao", permissao.SituacaoPermissao));
            comando.Parameters.Add(ParametroLongoNulo("@permissao_criado_por", permissao.PermissaoCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0L;
        }, cancellationToken);
    }

    public Task<int> AtualizarAsync(PermissaoCadastro permissao, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE permissao
            SET modulo_permissao = @modulo_permissao,
                rotina_permissao = @rotina_permissao,
                acao_permissao = @acao_permissao,
                descricao_permissao = @descricao_permissao,
                situacao_permissao = @situacao_permissao,
                permissao_atualizado_por = @permissao_atualizado_por
            WHERE codigo_permissao = @codigo_permissao;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_permissao", permissao.IdPermissao));
            comando.Parameters.Add(ParametroTexto("@modulo_permissao", permissao.ModuloPermissao));
            comando.Parameters.Add(ParametroTexto("@rotina_permissao", permissao.RotinaPermissao));
            comando.Parameters.Add(ParametroTexto("@acao_permissao", permissao.AcaoPermissao));
            comando.Parameters.Add(ParametroTexto("@descricao_permissao", permissao.DescricaoPermissao));
            comando.Parameters.Add(ParametroBooleano("@situacao_permissao", permissao.SituacaoPermissao));
            comando.Parameters.Add(ParametroLongoNulo("@permissao_atualizado_por", permissao.PermissaoAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> ExcluirAsync(long codigoPermissao, CancellationToken cancellationToken = default)
    {
        // Soft-delete: trigger detecta como DELETE_LOGICO.
        const string sql = """
            UPDATE permissao
               SET situacao_permissao = false,
                   permissao_atualizado_por = @permissao_atualizado_por
             WHERE codigo_permissao = @codigo_permissao
               AND situacao_permissao = true;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_permissao", codigoPermissao));
            comando.Parameters.Add(ParametroLongoNulo("@permissao_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> ReativarAsync(long codigoPermissao, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE permissao
               SET situacao_permissao = true,
                   permissao_atualizado_por = @permissao_atualizado_por
             WHERE codigo_permissao = @codigo_permissao
               AND situacao_permissao = false;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_permissao", codigoPermissao));
            comando.Parameters.Add(ParametroLongoNulo("@permissao_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static PermissaoCadastro MapearPermissao(NpgsqlDataReader leitor)
        => new()
        {
            IdPermissao = leitor.GetInt64(0),
            ModuloPermissao = leitor.GetString(1),
            RotinaPermissao = leitor.GetString(2),
            AcaoPermissao = leitor.GetString(3),
            DescricaoPermissao = leitor.IsDBNull(4) ? string.Empty : leitor.GetString(4),
            SituacaoPermissao = leitor.GetBoolean(5),
            PermissaoCriadoEm = leitor.IsDBNull(6) ? null : leitor.GetDateTime(6).ToLocalTime()
        };
}
