using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public class CampoEtiquetaRepositorio : RepositorioBase
{
    public CampoEtiquetaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public virtual async Task<IReadOnlyList<CampoEtiquetaCadastro>> ListarPorEtiquetaAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_campo_etiqueta, codigo_etiqueta, nome_campo, descricao_campo_etiqueta,
                   tipo_dado, obrigatorio, tamanho_maximo, formato_saida, ordem,
                   situacao_campo_etiqueta, campo_etiqueta_criado_em
            FROM campo_etiqueta
            WHERE codigo_etiqueta = @codigo_etiqueta
            ORDER BY ordem, nome_campo;
            """;

        List<CampoEtiquetaCadastro> campos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", codigoEtiqueta));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            campos.Add(MapearCampo(leitor));
        }

        return campos;
    }

    public virtual async Task<CampoEtiquetaCadastro?> ObterPorIdAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_campo_etiqueta, codigo_etiqueta, nome_campo, descricao_campo_etiqueta,
                   tipo_dado, obrigatorio, tamanho_maximo, formato_saida, ordem,
                   situacao_campo_etiqueta, campo_etiqueta_criado_em
            FROM campo_etiqueta
            WHERE codigo_campo_etiqueta = @codigo_campo_etiqueta;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", codigoCampoEtiqueta));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;
        return MapearCampo(leitor);
    }

    public virtual async Task<CampoEtiquetaEdicaoAgregado?> ObterEdicaoAgregadaAsync(
        long codigoCampoEtiqueta,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT c.codigo_campo_etiqueta, c.codigo_etiqueta, c.nome_campo, c.descricao_campo_etiqueta,
                   c.tipo_dado, c.obrigatorio, c.tamanho_maximo, c.formato_saida, c.ordem,
                   c.situacao_campo_etiqueta, c.campo_etiqueta_criado_em,
                   m.codigo_mapeamento_campo_etiqueta, m.codigo_campo_etiqueta, m.origem_dado,
                   m.expressao_origem, m.valor_padrao, m.obrigatorio_para_impressao, m.observacao,
                   m.situacao_mapeamento_campo_etiqueta, m.mapeamento_campo_etiqueta_criado_em
              FROM campo_etiqueta c
              LEFT JOIN LATERAL (
                  SELECT codigo_mapeamento_campo_etiqueta, codigo_campo_etiqueta, origem_dado,
                         expressao_origem, valor_padrao, obrigatorio_para_impressao, observacao,
                         situacao_mapeamento_campo_etiqueta, mapeamento_campo_etiqueta_criado_em
                    FROM mapeamento_campo_etiqueta
                   WHERE codigo_campo_etiqueta = c.codigo_campo_etiqueta
                     AND situacao_mapeamento_campo_etiqueta = true
                   ORDER BY codigo_mapeamento_campo_etiqueta DESC
                   LIMIT 1
              ) m ON true
             WHERE c.codigo_campo_etiqueta = @codigo_campo_etiqueta
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", codigoCampoEtiqueta));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;

        return new CampoEtiquetaEdicaoAgregado
        {
            Campo = MapearCampo(leitor),
            MapeamentoAtivo = leitor.IsDBNull(11)
                ? null
                : new MapeamentoCampoEtiquetaCadastro
                {
                    CodigoMapeamentoCampoEtiqueta = leitor.GetInt64(11),
                    CodigoCampoEtiqueta = leitor.GetInt64(12),
                    OrigemDado = leitor.GetString(13),
                    ExpressaoOrigem = leitor.IsDBNull(14) ? string.Empty : leitor.GetString(14),
                    ValorPadrao = leitor.IsDBNull(15) ? string.Empty : leitor.GetString(15),
                    ObrigatorioParaImpressao = leitor.GetBoolean(16),
                    Observacao = leitor.IsDBNull(17) ? string.Empty : leitor.GetString(17),
                    SituacaoMapeamentoCampoEtiqueta = leitor.GetBoolean(18),
                    MapeamentoCampoEtiquetaCriadoEm = leitor.IsDBNull(19)
                        ? null
                        : leitor.GetDateTime(19).ToLocalTime()
                }
        };
    }

    public virtual async Task<bool> ExisteNomeNaEtiquetaAsync(long codigoEtiqueta, string nomeCampo, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        // Regra global: nome unico por etiqueta independentemente da situacao.
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM campo_etiqueta
                WHERE codigo_etiqueta = @codigo_etiqueta
                  AND upper(trim(nome_campo)) = upper(trim(@nome_campo))
                  AND (@ignorar_codigo IS NULL OR codigo_campo_etiqueta <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", codigoEtiqueta));
        comando.Parameters.Add(ParametroTexto("@nome_campo", nomeCampo));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public virtual async Task<bool> EtiquetaEstaAtivaAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                  FROM etiqueta
                 WHERE codigo_etiqueta = @codigo_etiqueta
                   AND situacao_etiqueta = true
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", codigoEtiqueta));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public virtual async Task<bool> CampoEstaAtivoAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                  FROM campo_etiqueta
                 WHERE codigo_campo_etiqueta = @codigo_campo_etiqueta
                   AND situacao_campo_etiqueta = true
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", codigoCampoEtiqueta));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public virtual Task<long> InserirAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO campo_etiqueta
            (codigo_etiqueta, nome_campo, descricao_campo_etiqueta, tipo_dado, obrigatorio, tamanho_maximo, formato_saida, ordem, situacao_campo_etiqueta, campo_etiqueta_criado_por)
            VALUES
            (@codigo_etiqueta, @nome_campo, @descricao_campo_etiqueta, @tipo_dado, @obrigatorio, @tamanho_maximo, @formato_saida, @ordem, @situacao_campo_etiqueta, @campo_etiqueta_criado_por)
            RETURNING codigo_campo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            PreencherParametros(comando, campo);
            comando.Parameters.Add(ParametroLongoNulo("@campo_etiqueta_criado_por", campo.CampoEtiquetaCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0L;
        }, cancellationToken);
    }

    public virtual Task<int> AtualizarAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE campo_etiqueta
               SET nome_campo = @nome_campo,
                   descricao_campo_etiqueta = @descricao_campo_etiqueta,
                   tipo_dado = @tipo_dado,
                   obrigatorio = @obrigatorio,
                   tamanho_maximo = @tamanho_maximo,
                   formato_saida = @formato_saida,
                   ordem = @ordem,
                   campo_etiqueta_atualizado_por = @campo_etiqueta_atualizado_por
             WHERE codigo_campo_etiqueta = @codigo_campo_etiqueta;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", campo.CodigoCampoEtiqueta));
            PreencherParametrosEdicao(comando, campo);
            comando.Parameters.Add(ParametroLongoNulo("@campo_etiqueta_atualizado_por", campo.CampoEtiquetaAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public virtual Task<int> ExcluirAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE campo_etiqueta
               SET situacao_campo_etiqueta = false,
                   campo_etiqueta_atualizado_por = @campo_etiqueta_atualizado_por
             WHERE codigo_campo_etiqueta = @codigo_campo_etiqueta
               AND situacao_campo_etiqueta = true;
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", codigoCampoEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@campo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public virtual Task<int> ReativarAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE campo_etiqueta
               SET situacao_campo_etiqueta = true,
                   campo_etiqueta_atualizado_por = @campo_etiqueta_atualizado_por
             WHERE codigo_campo_etiqueta = @codigo_campo_etiqueta
               AND situacao_campo_etiqueta = false
               AND EXISTS (
                   SELECT 1
                     FROM etiqueta e
                    WHERE e.codigo_etiqueta = campo_etiqueta.codigo_etiqueta
                      AND e.situacao_etiqueta = true
               )
               AND NOT EXISTS (
                   SELECT 1
                     FROM campo_etiqueta outro
                    WHERE outro.codigo_etiqueta = campo_etiqueta.codigo_etiqueta
                      AND outro.codigo_campo_etiqueta <> campo_etiqueta.codigo_campo_etiqueta
                      AND upper(trim(outro.nome_campo)) = upper(trim(campo_etiqueta.nome_campo))
               );
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_campo_etiqueta", codigoCampoEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@campo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    private static void PreencherParametros(NpgsqlCommand comando, CampoEtiquetaCadastro campo)
    {
        comando.Parameters.Add(ParametroLongo("@codigo_etiqueta", campo.CodigoEtiqueta));
        comando.Parameters.Add(ParametroTexto("@nome_campo", campo.NomeCampo));
        comando.Parameters.Add(new NpgsqlParameter("@descricao_campo_etiqueta", string.IsNullOrWhiteSpace(campo.DescricaoCampoEtiqueta) ? DBNull.Value : campo.DescricaoCampoEtiqueta));
        comando.Parameters.Add(ParametroTexto("@tipo_dado", string.IsNullOrWhiteSpace(campo.TipoDado) ? "TEXTO" : campo.TipoDado));
        comando.Parameters.Add(ParametroBooleano("@obrigatorio", campo.Obrigatorio));
        comando.Parameters.Add(ParametroInteiroNulo("@tamanho_maximo", campo.TamanhoMaximo));
        comando.Parameters.Add(new NpgsqlParameter("@formato_saida", string.IsNullOrWhiteSpace(campo.FormatoSaida) ? DBNull.Value : campo.FormatoSaida));
        comando.Parameters.Add(ParametroInteiro("@ordem", campo.Ordem));
        comando.Parameters.Add(ParametroBooleano("@situacao_campo_etiqueta", campo.SituacaoCampoEtiqueta));
    }

    private static void PreencherParametrosEdicao(NpgsqlCommand comando, CampoEtiquetaCadastro campo)
    {
        comando.Parameters.Add(ParametroTexto("@nome_campo", campo.NomeCampo));
        comando.Parameters.Add(new NpgsqlParameter("@descricao_campo_etiqueta", string.IsNullOrWhiteSpace(campo.DescricaoCampoEtiqueta) ? DBNull.Value : campo.DescricaoCampoEtiqueta));
        comando.Parameters.Add(ParametroTexto("@tipo_dado", string.IsNullOrWhiteSpace(campo.TipoDado) ? "TEXTO" : campo.TipoDado));
        comando.Parameters.Add(ParametroBooleano("@obrigatorio", campo.Obrigatorio));
        comando.Parameters.Add(ParametroInteiroNulo("@tamanho_maximo", campo.TamanhoMaximo));
        comando.Parameters.Add(new NpgsqlParameter("@formato_saida", string.IsNullOrWhiteSpace(campo.FormatoSaida) ? DBNull.Value : campo.FormatoSaida));
        comando.Parameters.Add(ParametroInteiro("@ordem", campo.Ordem));
    }

    private static CampoEtiquetaCadastro MapearCampo(NpgsqlDataReader leitor)
        => new()
        {
            CodigoCampoEtiqueta = leitor.GetInt64(0),
            CodigoEtiqueta = leitor.GetInt64(1),
            NomeCampo = leitor.GetString(2),
            DescricaoCampoEtiqueta = leitor.IsDBNull(3) ? string.Empty : leitor.GetString(3),
            TipoDado = leitor.IsDBNull(4) ? "TEXTO" : leitor.GetString(4),
            Obrigatorio = leitor.GetBoolean(5),
            TamanhoMaximo = leitor.IsDBNull(6) ? null : leitor.GetInt32(6),
            FormatoSaida = leitor.IsDBNull(7) ? string.Empty : leitor.GetString(7),
            Ordem = leitor.GetInt32(8),
            SituacaoCampoEtiqueta = leitor.GetBoolean(9),
            CampoEtiquetaCriadoEm = leitor.IsDBNull(10) ? null : leitor.GetDateTime(10).ToLocalTime()
        };
}
