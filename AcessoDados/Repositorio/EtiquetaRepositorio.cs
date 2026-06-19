using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class EtiquetaRepositorio : RepositorioBase
{
    public EtiquetaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<EtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_etiqueta, codigo_modelo_etiqueta, codigo_interno, nome_etiqueta,
                   tipo_etiqueta, descricao_etiqueta, situacao_etiqueta, etiqueta_criado_em
            FROM etiqueta
            ORDER BY nome_etiqueta;
            """;

        List<EtiquetaCadastro> etiquetas = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            etiquetas.Add(MapearEtiqueta(leitor));
        }

        return etiquetas;
    }

    public async Task<EtiquetaCadastro?> ObterPorIdAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_etiqueta, codigo_modelo_etiqueta, codigo_interno, nome_etiqueta,
                   tipo_etiqueta, descricao_etiqueta, situacao_etiqueta, etiqueta_criado_em
            FROM etiqueta
            WHERE codigo_etiqueta = @codigo_etiqueta;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", codigoEtiqueta));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearEtiqueta(leitor);
    }

    public async Task<bool> ExisteCodigoInternoAsync(string codigoInterno, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        // Espelha uq_etiqueta_codigo (upper(trim(codigo_interno))) WHERE situacao = true.
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM etiqueta
                WHERE upper(trim(codigo_interno)) = upper(trim(@codigo_interno))
                  AND situacao_etiqueta = true
                  AND (@ignorar_codigo IS NULL OR codigo_etiqueta <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@codigo_interno", codigoInterno));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<long> InserirAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO etiqueta
            (
                codigo_modelo_etiqueta, codigo_interno, nome_etiqueta, tipo_etiqueta,
                descricao_etiqueta, situacao_etiqueta, etiqueta_criado_por
            )
            VALUES
            (
                @codigo_modelo_etiqueta, @codigo_interno, @nome_etiqueta, @tipo_etiqueta,
                @descricao_etiqueta, @situacao_etiqueta, @etiqueta_criado_por
            )
            RETURNING codigo_etiqueta;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            PreencherParametros(comando, etiqueta);
            comando.Parameters.Add(ParametroLongoNulo("@etiqueta_criado_por", etiqueta.EtiquetaCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0;
        }, cancellationToken);
    }

    public async Task<int> ExcluirAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE etiqueta
               SET situacao_etiqueta = false,
                   etiqueta_atualizado_por = @etiqueta_atualizado_por
             WHERE codigo_etiqueta = @codigo_etiqueta
               AND situacao_etiqueta = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", codigoEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> AtualizarAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE etiqueta
               SET codigo_modelo_etiqueta = @codigo_modelo_etiqueta,
                   codigo_interno = @codigo_interno,
                   nome_etiqueta = @nome_etiqueta,
                   tipo_etiqueta = @tipo_etiqueta,
                   descricao_etiqueta = @descricao_etiqueta,
                   situacao_etiqueta = @situacao_etiqueta,
                   etiqueta_atualizado_por = @etiqueta_atualizado_por
             WHERE codigo_etiqueta = @codigo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", etiqueta.CodigoEtiqueta));
            PreencherParametros(comando, etiqueta);
            comando.Parameters.Add(ParametroLongoNulo("@etiqueta_atualizado_por", etiqueta.EtiquetaAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> ReativarAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE etiqueta
               SET situacao_etiqueta = true,
                   etiqueta_atualizado_por = @etiqueta_atualizado_por
             WHERE codigo_etiqueta = @codigo_etiqueta
               AND situacao_etiqueta = false;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", codigoEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void PreencherParametros(NpgsqlCommand comando, EtiquetaCadastro etiqueta)
    {
        comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", etiqueta.CodigoModeloEtiqueta));
        comando.Parameters.Add(ParametroTexto("@codigo_interno", etiqueta.CodigoInterno));
        comando.Parameters.Add(ParametroTexto("@nome_etiqueta", etiqueta.NomeEtiqueta));
        comando.Parameters.Add(ParametroTexto("@tipo_etiqueta", etiqueta.TipoEtiqueta));
        comando.Parameters.Add(ParametroTexto("@descricao_etiqueta", etiqueta.DescricaoEtiqueta));
        comando.Parameters.Add(ParametroBooleano("@situacao_etiqueta", etiqueta.SituacaoEtiqueta));
    }

    private static EtiquetaCadastro MapearEtiqueta(NpgsqlDataReader leitor)
        => new()
        {
            CodigoEtiqueta = leitor.GetInt64(0),
            CodigoModeloEtiqueta = leitor.GetInt64(1),
            CodigoInterno = leitor.GetString(2),
            NomeEtiqueta = leitor.GetString(3),
            TipoEtiqueta = leitor.GetString(4),
            DescricaoEtiqueta = leitor.IsDBNull(5) ? string.Empty : leitor.GetString(5),
            SituacaoEtiqueta = leitor.GetBoolean(6),
            EtiquetaCriadoEm = leitor.IsDBNull(7) ? null : leitor.GetDateTime(7).ToLocalTime()
        };
}
