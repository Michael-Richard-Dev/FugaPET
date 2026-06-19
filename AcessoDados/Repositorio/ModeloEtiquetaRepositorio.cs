using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class ModeloEtiquetaRepositorio : RepositorioBase
{
    public ModeloEtiquetaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<ModeloEtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_modelo_etiqueta, nome_modelo_etiqueta, versao, largura_mm, altura_mm, dpi,
                   conteudo_zpl, observacao, situacao_modelo_etiqueta, modelo_etiqueta_criado_em
            FROM modelo_etiqueta
            ORDER BY nome_modelo_etiqueta, versao;
            """;

        List<ModeloEtiquetaCadastro> modelos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            modelos.Add(MapearModelo(leitor));
        }

        return modelos;
    }

    public async Task<ModeloEtiquetaCadastro?> ObterPorIdAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_modelo_etiqueta, nome_modelo_etiqueta, versao, largura_mm, altura_mm, dpi,
                   conteudo_zpl, observacao, situacao_modelo_etiqueta, modelo_etiqueta_criado_em
            FROM modelo_etiqueta
            WHERE codigo_modelo_etiqueta = @codigo_modelo_etiqueta;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", codigoModeloEtiqueta));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearModelo(leitor);
    }

    public async Task<bool> ExisteNomeVersaoAsync(string nome, int versao, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        // Espelha uq_modelo_etiqueta_nome_versao (upper(trim(nome)), versao) WHERE situacao = true.
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM modelo_etiqueta
                WHERE upper(trim(nome_modelo_etiqueta)) = upper(trim(@nome_modelo_etiqueta))
                  AND versao = @versao
                  AND situacao_modelo_etiqueta = true
                  AND (@ignorar_codigo IS NULL OR codigo_modelo_etiqueta <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@nome_modelo_etiqueta", nome));
        comando.Parameters.Add(ParametroInteiro("@versao", versao));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public Task<long> InserirAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO modelo_etiqueta
            (nome_modelo_etiqueta, versao, largura_mm, altura_mm, dpi, conteudo_zpl, observacao, situacao_modelo_etiqueta, modelo_etiqueta_criado_por)
            VALUES
            (@nome_modelo_etiqueta, @versao, @largura_mm, @altura_mm, @dpi, @conteudo_zpl, @observacao, @situacao_modelo_etiqueta, @modelo_etiqueta_criado_por)
            RETURNING codigo_modelo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            PreencherParametros(comando, modelo);
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_criado_por", modelo.ModeloEtiquetaCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0L;
        }, cancellationToken);
    }

    public Task<int> AtualizarAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE modelo_etiqueta
               SET nome_modelo_etiqueta = @nome_modelo_etiqueta,
                   versao = @versao,
                   largura_mm = @largura_mm,
                   altura_mm = @altura_mm,
                   dpi = @dpi,
                   conteudo_zpl = @conteudo_zpl,
                   observacao = @observacao,
                   situacao_modelo_etiqueta = @situacao_modelo_etiqueta,
                   modelo_etiqueta_atualizado_por = @modelo_etiqueta_atualizado_por
             WHERE codigo_modelo_etiqueta = @codigo_modelo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", modelo.CodigoModeloEtiqueta));
            PreencherParametros(comando, modelo);
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_atualizado_por", modelo.ModeloEtiquetaAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> ExcluirAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE modelo_etiqueta
               SET situacao_modelo_etiqueta = false,
                   modelo_etiqueta_atualizado_por = @modelo_etiqueta_atualizado_por
             WHERE codigo_modelo_etiqueta = @codigo_modelo_etiqueta
               AND situacao_modelo_etiqueta = true;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", codigoModeloEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> ReativarAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE modelo_etiqueta
               SET situacao_modelo_etiqueta = true,
                   modelo_etiqueta_atualizado_por = @modelo_etiqueta_atualizado_por
             WHERE codigo_modelo_etiqueta = @codigo_modelo_etiqueta
               AND situacao_modelo_etiqueta = false;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", codigoModeloEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void PreencherParametros(NpgsqlCommand comando, ModeloEtiquetaCadastro modelo)
    {
        comando.Parameters.Add(ParametroTexto("@nome_modelo_etiqueta", modelo.NomeModeloEtiqueta));
        comando.Parameters.Add(ParametroInteiro("@versao", modelo.Versao));
        comando.Parameters.Add(ParametroDecimalNulo("@largura_mm", modelo.LarguraMm));
        comando.Parameters.Add(ParametroDecimalNulo("@altura_mm", modelo.AlturaMm));
        comando.Parameters.Add(ParametroInteiroNulo("@dpi", modelo.Dpi));
        comando.Parameters.Add(ParametroTexto("@conteudo_zpl", modelo.ConteudoZpl));
        comando.Parameters.Add(new NpgsqlParameter("@observacao", string.IsNullOrWhiteSpace(modelo.Observacao) ? DBNull.Value : modelo.Observacao));
        comando.Parameters.Add(ParametroBooleano("@situacao_modelo_etiqueta", modelo.SituacaoModeloEtiqueta));
    }

    private static ModeloEtiquetaCadastro MapearModelo(NpgsqlDataReader leitor)
        => new()
        {
            CodigoModeloEtiqueta = leitor.GetInt64(0),
            NomeModeloEtiqueta = leitor.GetString(1),
            Versao = leitor.GetInt32(2),
            LarguraMm = leitor.IsDBNull(3) ? null : leitor.GetDecimal(3),
            AlturaMm = leitor.IsDBNull(4) ? null : leitor.GetDecimal(4),
            Dpi = leitor.IsDBNull(5) ? null : leitor.GetInt32(5),
            ConteudoZpl = leitor.IsDBNull(6) ? string.Empty : leitor.GetString(6),
            Observacao = leitor.IsDBNull(7) ? string.Empty : leitor.GetString(7),
            SituacaoModeloEtiqueta = leitor.GetBoolean(8),
            ModeloEtiquetaCriadoEm = leitor.IsDBNull(9) ? null : leitor.GetDateTime(9).ToLocalTime()
        };
}
