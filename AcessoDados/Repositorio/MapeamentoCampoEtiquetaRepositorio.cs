using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

// Nao-sealed e com metodos virtuais para permitir fake em testes (sem extrair interface).
public class MapeamentoCampoEtiquetaRepositorio : RepositorioBase
{
    public MapeamentoCampoEtiquetaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<MapeamentoCampoEtiquetaCadastro?> ObterPorIdAsync(long codigo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_mapeamento_campo_etiqueta, codigo_campo_etiqueta, origem_dado, expressao_origem,
                   valor_padrao, obrigatorio_para_impressao, observacao,
                   situacao_mapeamento_campo_etiqueta, mapeamento_campo_etiqueta_criado_em
            FROM mapeamento_campo_etiqueta
            WHERE codigo_mapeamento_campo_etiqueta = @codigo;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigo));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearMapeamento(leitor);
    }

    /// <summary>
    /// Retorna o mapeamento ATIVO do campo (1:1 garantido pelo indice uq_mapeamento_campo_ativo), ou null.
    /// </summary>
    public virtual async Task<MapeamentoCampoEtiquetaCadastro?> ObterAtivoPorCampoAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_mapeamento_campo_etiqueta, codigo_campo_etiqueta, origem_dado, expressao_origem,
                   valor_padrao, obrigatorio_para_impressao, observacao,
                   situacao_mapeamento_campo_etiqueta, mapeamento_campo_etiqueta_criado_em
            FROM mapeamento_campo_etiqueta
            WHERE codigo_campo_etiqueta = @codigo_campo_etiqueta
              AND situacao_mapeamento_campo_etiqueta = true
            LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", codigoCampoEtiqueta));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearMapeamento(leitor);
    }

    public virtual Task<long> InserirAsync(MapeamentoCampoEtiquetaCadastro mapa, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO mapeamento_campo_etiqueta
            (codigo_campo_etiqueta, origem_dado, expressao_origem, valor_padrao, obrigatorio_para_impressao, observacao, situacao_mapeamento_campo_etiqueta, mapeamento_campo_etiqueta_criado_por)
            VALUES
            (@codigo_campo_etiqueta, @origem_dado, @expressao_origem, @valor_padrao, @obrigatorio_para_impressao, @observacao, @situacao_mapeamento_campo_etiqueta, @mapeamento_campo_etiqueta_criado_por)
            RETURNING codigo_mapeamento_campo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            PreencherParametros(comando, mapa);
            comando.Parameters.Add(ParametroLongoNulo("@mapeamento_campo_etiqueta_criado_por", mapa.MapeamentoCampoEtiquetaCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0L;
        }, cancellationToken);
    }

    public virtual Task<int> AtualizarAsync(MapeamentoCampoEtiquetaCadastro mapa, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE mapeamento_campo_etiqueta
               SET origem_dado = @origem_dado,
                   expressao_origem = @expressao_origem,
                   valor_padrao = @valor_padrao,
                   obrigatorio_para_impressao = @obrigatorio_para_impressao,
                   observacao = @observacao,
                   situacao_mapeamento_campo_etiqueta = @situacao_mapeamento_campo_etiqueta,
                   mapeamento_campo_etiqueta_atualizado_por = @mapeamento_campo_etiqueta_atualizado_por
             WHERE codigo_mapeamento_campo_etiqueta = @codigo_mapeamento_campo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_mapeamento_campo_etiqueta", mapa.CodigoMapeamentoCampoEtiqueta));
            PreencherParametros(comando, mapa);
            comando.Parameters.Add(ParametroLongoNulo("@mapeamento_campo_etiqueta_atualizado_por", mapa.MapeamentoCampoEtiquetaAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public Task<int> ExcluirAsync(long codigo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE mapeamento_campo_etiqueta
               SET situacao_mapeamento_campo_etiqueta = false,
                   mapeamento_campo_etiqueta_atualizado_por = @mapeamento_campo_etiqueta_atualizado_por
             WHERE codigo_mapeamento_campo_etiqueta = @codigo_mapeamento_campo_etiqueta
               AND situacao_mapeamento_campo_etiqueta = true;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_mapeamento_campo_etiqueta", codigo));
            comando.Parameters.Add(ParametroLongoNulo("@mapeamento_campo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void PreencherParametros(NpgsqlCommand comando, MapeamentoCampoEtiquetaCadastro mapa)
    {
        comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", mapa.CodigoCampoEtiqueta));
        comando.Parameters.Add(ParametroTexto("@origem_dado", string.IsNullOrWhiteSpace(mapa.OrigemDado) ? "USUARIO" : mapa.OrigemDado));
        comando.Parameters.Add(new NpgsqlParameter("@expressao_origem", string.IsNullOrWhiteSpace(mapa.ExpressaoOrigem) ? DBNull.Value : mapa.ExpressaoOrigem));
        comando.Parameters.Add(new NpgsqlParameter("@valor_padrao", string.IsNullOrWhiteSpace(mapa.ValorPadrao) ? DBNull.Value : mapa.ValorPadrao));
        comando.Parameters.Add(ParametroBooleano("@obrigatorio_para_impressao", mapa.ObrigatorioParaImpressao));
        comando.Parameters.Add(new NpgsqlParameter("@observacao", string.IsNullOrWhiteSpace(mapa.Observacao) ? DBNull.Value : mapa.Observacao));
        comando.Parameters.Add(ParametroBooleano("@situacao_mapeamento_campo_etiqueta", mapa.SituacaoMapeamentoCampoEtiqueta));
    }

    private static MapeamentoCampoEtiquetaCadastro MapearMapeamento(NpgsqlDataReader leitor)
        => new()
        {
            CodigoMapeamentoCampoEtiqueta = leitor.GetInt64(0),
            CodigoCampoEtiqueta = leitor.GetInt64(1),
            OrigemDado = leitor.GetString(2),
            ExpressaoOrigem = leitor.IsDBNull(3) ? string.Empty : leitor.GetString(3),
            ValorPadrao = leitor.IsDBNull(4) ? string.Empty : leitor.GetString(4),
            ObrigatorioParaImpressao = leitor.GetBoolean(5),
            Observacao = leitor.IsDBNull(6) ? string.Empty : leitor.GetString(6),
            SituacaoMapeamentoCampoEtiqueta = leitor.GetBoolean(7),
            MapeamentoCampoEtiquetaCriadoEm = leitor.IsDBNull(8) ? null : leitor.GetDateTime(8).ToLocalTime()
        };
}
