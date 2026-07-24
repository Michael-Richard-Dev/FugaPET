using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

// Não selado + métodos virtuais para permitir RepositorioFake nos testes de serviço (padrão Tara/TipoTara).
public class ModeloEtiquetaRepositorio : RepositorioBase
{
    public ModeloEtiquetaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public virtual async Task<IReadOnlyList<ModeloEtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
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

    public virtual async Task<ModeloEtiquetaCadastro?> ObterPorIdAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
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

    public virtual async Task<bool> ExisteNomeVersaoAsync(string nome, int versao, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        // Duplicidade GLOBAL: nome normalizado + versão são únicos independentemente da situação (ativo OU inativo).
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM modelo_etiqueta
                WHERE upper(trim(nome_modelo_etiqueta)) = upper(trim(@nome_modelo_etiqueta))
                  AND versao = @versao
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

    // Dependência explícita: existe alguma etiqueta ATIVA vinculada a este modelo? (histórico/impressões não bloqueiam)
    public virtual async Task<bool> ExisteEtiquetaAtivaVinculadaAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM etiqueta
                WHERE codigo_modelo_etiqueta = @codigo_modelo_etiqueta
                  AND situacao_etiqueta = true
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", codigoModeloEtiqueta));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public virtual Task<long> InserirAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
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
            comando.Parameters.Add(ParametroBooleano("@situacao_modelo_etiqueta", modelo.SituacaoModeloEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_criado_por", modelo.ModeloEtiquetaCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? id = await comando.ExecuteScalarAsync(cancellationToken);
            return id is long valor ? valor : 0L;
        }, cancellationToken);
    }

    // Atualização CADASTRAL apenas — NÃO altera situacao_modelo_etiqueta (situação muda só por Inativar/Reativar).
    public virtual Task<int> AtualizarAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
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

    // Inativação atômica: só inativa se NÃO houver etiqueta ativa vinculada (evita corrida com cadastro de etiqueta).
    public virtual Task<int> ExcluirAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE modelo_etiqueta m
               SET situacao_modelo_etiqueta = false,
                   modelo_etiqueta_atualizado_por = @modelo_etiqueta_atualizado_por
             WHERE m.codigo_modelo_etiqueta = @codigo_modelo_etiqueta
               AND m.situacao_modelo_etiqueta = true
               AND NOT EXISTS (
                     SELECT 1 FROM etiqueta e
                      WHERE e.codigo_modelo_etiqueta = m.codigo_modelo_etiqueta
                        AND e.situacao_etiqueta = true
                   );
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", codigoModeloEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    // Reativação atômica: só reativa se NÃO existir outro modelo com o mesmo nome normalizado + versão (duplicidade global).
    public virtual Task<int> ReativarAsync(long codigoModeloEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE modelo_etiqueta m
               SET situacao_modelo_etiqueta = true,
                   modelo_etiqueta_atualizado_por = @modelo_etiqueta_atualizado_por
             WHERE m.codigo_modelo_etiqueta = @codigo_modelo_etiqueta
               AND m.situacao_modelo_etiqueta = false
               AND NOT EXISTS (
                     SELECT 1 FROM modelo_etiqueta outro
                      WHERE upper(trim(outro.nome_modelo_etiqueta)) = upper(trim(m.nome_modelo_etiqueta))
                        AND outro.versao = m.versao
                        AND outro.codigo_modelo_etiqueta <> m.codigo_modelo_etiqueta
                   );
            """;

        return ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_modelo_etiqueta", codigoModeloEtiqueta));
            comando.Parameters.Add(ParametroLongoNulo("@modelo_etiqueta_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    // Parâmetros CADASTRAIS compartilhados por Inserir/Atualizar (a situação é tratada à parte, só no Inserir).
    private static void PreencherParametros(NpgsqlCommand comando, ModeloEtiquetaCadastro modelo)
    {
        comando.Parameters.Add(ParametroTexto("@nome_modelo_etiqueta", modelo.NomeModeloEtiqueta));
        comando.Parameters.Add(ParametroInteiro("@versao", modelo.Versao));
        comando.Parameters.Add(ParametroDecimalNulo("@largura_mm", modelo.LarguraMm));
        comando.Parameters.Add(ParametroDecimalNulo("@altura_mm", modelo.AlturaMm));
        comando.Parameters.Add(ParametroInteiroNulo("@dpi", modelo.Dpi));
        comando.Parameters.Add(ParametroTexto("@conteudo_zpl", modelo.ConteudoZpl));
        comando.Parameters.Add(new NpgsqlParameter("@observacao", string.IsNullOrWhiteSpace(modelo.Observacao) ? DBNull.Value : modelo.Observacao));
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
