using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class TaraRepositorio : RepositorioBase
{
    public TaraRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    private const string ColunasSelect =
        "codigo_tara, codigo_tipo_tara, codigo_setor, nome_tara, tamanho, peso_kg, observacao, situacao_tara, tara_criado_em";

    public async Task<IReadOnlyList<TaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {ColunasSelect} FROM tara ORDER BY nome_tara;";

        List<TaraCadastro> taras = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            taras.Add(MapearTara(leitor));
        }

        return taras;
    }

    /// <summary>
    /// Taras ATIVAS de um setor especifico, para a selecao de tara da Entrada de Produto.
    /// Impede o uso de tara pertencente a outro setor.
    /// </summary>
    public async Task<IReadOnlyList<TaraCadastro>> ListarAtivasPorSetorAsync(long codigoSetor, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {ColunasSelect}
              FROM tara
             WHERE codigo_setor = @codigo_setor
               AND situacao_tara = true
             ORDER BY nome_tara;
            """;

        List<TaraCadastro> taras = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            taras.Add(MapearTara(leitor));
        }

        return taras;
    }

    public async Task<TaraCadastro?> ObterPorIdAsync(long codigoTara, CancellationToken cancellationToken = default)
    {
        const string sql = $"SELECT {ColunasSelect} FROM tara WHERE codigo_tara = @codigo_tara;";

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_tara", codigoTara));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearTara(leitor);
    }

    public async Task<bool> ExisteNomeNoSetorTipoAsync(string nome, long codigoSetor, long codigoTipoTara, long? ignorarCodigo, CancellationToken cancellationToken = default)
    {
        // Index unique: uq_tara_setor_tipo_nome (codigo_setor, codigo_tipo_tara, upper(trim(nome_tara))) WHERE situacao_tara
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM tara
                WHERE codigo_setor = @codigo_setor
                  AND codigo_tipo_tara = @codigo_tipo_tara
                  AND upper(trim(nome_tara)) = upper(trim(@nome_tara))
                  AND situacao_tara = true
                  AND (@ignorar_codigo IS NULL OR codigo_tara <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_setor", codigoSetor));
        comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
        comando.Parameters.Add(ParametroTexto("@nome_tara", nome));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public async Task<long> InserirAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO tara
            (codigo_tipo_tara, codigo_setor, nome_tara, tamanho, peso_kg, observacao, situacao_tara, tara_criado_por)
            VALUES
            (@codigo_tipo_tara, @codigo_setor, @nome_tara, @tamanho, @peso_kg, @observacao, @situacao_tara, @tara_criado_por)
            RETURNING codigo_tara;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", tara.CodigoTipoTara));
            comando.Parameters.Add(ParametroLongo("@codigo_setor", tara.CodigoSetor));
            comando.Parameters.Add(ParametroTexto("@nome_tara", tara.NomeTara));
            comando.Parameters.Add(ParametroTexto("@tamanho", tara.Tamanho));
            comando.Parameters.Add(new NpgsqlParameter("@peso_kg", NpgsqlDbType.Numeric) { Value = tara.PesoKg });
            comando.Parameters.Add(ParametroTexto("@observacao", tara.Observacao));
            comando.Parameters.Add(ParametroBooleano("@situacao_tara", tara.SituacaoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tara_criado_por", tara.TaraCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0;
        }, cancellationToken);
    }

    public async Task<int> AtualizarAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tara
            SET codigo_tipo_tara = @codigo_tipo_tara,
                codigo_setor = @codigo_setor,
                nome_tara = @nome_tara,
                tamanho = @tamanho,
                peso_kg = @peso_kg,
                observacao = @observacao,
                situacao_tara = @situacao_tara,
                tara_atualizado_por = @tara_atualizado_por
            WHERE codigo_tara = @codigo_tara;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tara", tara.CodigoTara));
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", tara.CodigoTipoTara));
            comando.Parameters.Add(ParametroLongo("@codigo_setor", tara.CodigoSetor));
            comando.Parameters.Add(ParametroTexto("@nome_tara", tara.NomeTara));
            comando.Parameters.Add(ParametroTexto("@tamanho", tara.Tamanho));
            comando.Parameters.Add(new NpgsqlParameter("@peso_kg", NpgsqlDbType.Numeric) { Value = tara.PesoKg });
            comando.Parameters.Add(ParametroTexto("@observacao", tara.Observacao));
            comando.Parameters.Add(ParametroBooleano("@situacao_tara", tara.SituacaoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tara_atualizado_por", tara.TaraAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ExcluirAsync(long codigoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tara
               SET situacao_tara = false,
                   tara_atualizado_por = @tara_atualizado_por
             WHERE codigo_tara = @codigo_tara
               AND situacao_tara = true;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tara", codigoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tara_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public async Task<int> ReativarAsync(long codigoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tara
               SET situacao_tara = true,
                   tara_atualizado_por = @tara_atualizado_por
             WHERE codigo_tara = @codigo_tara
               AND situacao_tara = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tara", codigoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tara_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static TaraCadastro MapearTara(NpgsqlDataReader leitor)
        => new()
        {
            CodigoTara = leitor.GetInt64(0),
            CodigoTipoTara = leitor.GetInt64(1),
            CodigoSetor = leitor.GetInt64(2),
            NomeTara = leitor.GetString(3),
            Tamanho = leitor.IsDBNull(4) ? string.Empty : leitor.GetString(4),
            PesoKg = leitor.IsDBNull(5) ? 0m : leitor.GetDecimal(5),
            Observacao = leitor.IsDBNull(6) ? string.Empty : leitor.GetString(6),
            SituacaoTara = leitor.GetBoolean(7),
            TaraCriadoEm = leitor.IsDBNull(8) ? null : leitor.GetDateTime(8).ToLocalTime()
        };
}
