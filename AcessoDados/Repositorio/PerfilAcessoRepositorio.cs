using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class PerfilAcessoRepositorio : RepositorioBase
{
    public PerfilAcessoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<PerfilAcessoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_perfil_acesso, nome_perfil_acesso, descricao_perfil_acesso,
                   perfil_sistema, situacao_perfil_acesso, perfil_acesso_criado_em
            FROM perfil_acesso
            ORDER BY nome_perfil_acesso;
            """;

        List<PerfilAcessoCadastro> perfis = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            perfis.Add(MapearPerfil(leitor));
        }

        return perfis;
    }

    public async Task<PerfilAcessoCadastro?> ObterPorIdAsync(long codigoPerfilAcesso, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_perfil_acesso, nome_perfil_acesso, descricao_perfil_acesso,
                   perfil_sistema, situacao_perfil_acesso, perfil_acesso_criado_em
            FROM perfil_acesso
            WHERE codigo_perfil_acesso = @codigo_perfil_acesso;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilAcesso));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearPerfil(leitor);
    }

    public async Task<bool> ExisteNomeAsync(string nome, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM perfil_acesso
                WHERE upper(trim(nome_perfil_acesso)) = upper(trim(@nome_perfil_acesso))
                  AND situacao_perfil_acesso = true
                  AND (@ignorar_codigo IS NULL OR codigo_perfil_acesso <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@nome_perfil_acesso", nome));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<long> InserirAsync(PerfilAcessoCadastro perfil, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO perfil_acesso
            (nome_perfil_acesso, descricao_perfil_acesso, perfil_sistema, situacao_perfil_acesso, perfil_acesso_criado_por)
            VALUES (@nome_perfil_acesso, @descricao_perfil_acesso, @perfil_sistema, @situacao_perfil_acesso, @perfil_acesso_criado_por)
            RETURNING codigo_perfil_acesso;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroTexto("@nome_perfil_acesso", perfil.NomePerfilAcesso));
            comando.Parameters.Add(ParametroTexto("@descricao_perfil_acesso", perfil.DescricaoPerfilAcesso));
            comando.Parameters.Add(ParametroBooleano("@perfil_sistema", perfil.PerfilSistema));
            comando.Parameters.Add(ParametroBooleano("@situacao_perfil_acesso", perfil.SituacaoPerfilAcesso));
            comando.Parameters.Add(ParametroLongoNulo("@perfil_acesso_criado_por", perfil.PerfilAcessoCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    public async Task<int> AtualizarAsync(PerfilAcessoCadastro perfil, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE perfil_acesso
            SET nome_perfil_acesso = @nome_perfil_acesso,
                descricao_perfil_acesso = @descricao_perfil_acesso,
                perfil_sistema = @perfil_sistema,
                situacao_perfil_acesso = @situacao_perfil_acesso,
                perfil_acesso_atualizado_por = @perfil_acesso_atualizado_por
            WHERE codigo_perfil_acesso = @codigo_perfil_acesso;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", perfil.IdPerfilAcesso));
            comando.Parameters.Add(ParametroTexto("@nome_perfil_acesso", perfil.NomePerfilAcesso));
            comando.Parameters.Add(ParametroTexto("@descricao_perfil_acesso", perfil.DescricaoPerfilAcesso));
            comando.Parameters.Add(ParametroBooleano("@perfil_sistema", perfil.PerfilSistema));
            comando.Parameters.Add(ParametroBooleano("@situacao_perfil_acesso", perfil.SituacaoPerfilAcesso));
            comando.Parameters.Add(ParametroLongoNulo("@perfil_acesso_atualizado_por", perfil.PerfilAcessoAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ExcluirAsync(long codigoPerfilAcesso, CancellationToken cancellationToken = default)
    {
        // Soft-delete: nao desativa perfis de sistema.
        const string sql = """
            UPDATE perfil_acesso
               SET situacao_perfil_acesso = false,
                   perfil_acesso_atualizado_por = @perfil_acesso_atualizado_por
             WHERE codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_perfil_acesso = true
               AND perfil_sistema = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilAcesso));
            comando.Parameters.Add(ParametroLongoNulo("@perfil_acesso_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReativarAsync(long codigoPerfilAcesso, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE perfil_acesso
               SET situacao_perfil_acesso = true,
                   perfil_acesso_atualizado_por = @perfil_acesso_atualizado_por
             WHERE codigo_perfil_acesso = @codigo_perfil_acesso
               AND situacao_perfil_acesso = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_perfil_acesso", codigoPerfilAcesso));
            comando.Parameters.Add(ParametroLongoNulo("@perfil_acesso_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static PerfilAcessoCadastro MapearPerfil(NpgsqlDataReader leitor)
        => new()
        {
            IdPerfilAcesso = leitor.GetInt64(0),
            NomePerfilAcesso = leitor.GetString(1),
            DescricaoPerfilAcesso = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
            PerfilSistema = leitor.GetBoolean(3),
            SituacaoPerfilAcesso = leitor.GetBoolean(4),
            PerfilAcessoCriadoEm = leitor.IsDBNull(5) ? null : leitor.GetDateTime(5).ToLocalTime()
        };
}
