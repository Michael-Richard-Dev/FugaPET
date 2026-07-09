using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

// Tarefa Tipo de Tara: classe NÃO selada + métodos virtuais para permitir teste do serviço com repositório fake
// (mesmo padrão do CargoRepositorio). Nenhuma alteração de SQL/estrutura da tabela.
public class TipoTaraRepositorio : RepositorioBase
{
    public TipoTaraRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public virtual async Task<IReadOnlyList<TipoTaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tipo_tara, nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara, tipo_tara_criado_em
            FROM tipo_tara
            ORDER BY nome_tipo_tara;
            """;

        List<TipoTaraCadastro> tipos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            tipos.Add(new TipoTaraCadastro
            {
                CodigoTipoTara = leitor.GetInt64(0),
                NomeTipoTara = leitor.GetString(1),
                DescricaoTipoTara = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
                SituacaoTipoTara = leitor.GetBoolean(3),
                TipoTaraCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
            });
        }

        return tipos;
    }

    public virtual async Task<TipoTaraCadastro?> ObterPorIdAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tipo_tara, nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara, tipo_tara_criado_em
            FROM tipo_tara
            WHERE codigo_tipo_tara = @codigo_tipo_tara;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        if (!await leitor.ReadAsync(cancellationToken)) return null;

        return new TipoTaraCadastro
        {
            CodigoTipoTara = leitor.GetInt64(0),
            NomeTipoTara = leitor.GetString(1),
            DescricaoTipoTara = leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2),
            SituacaoTipoTara = leitor.GetBoolean(3),
            TipoTaraCriadoEm = leitor.IsDBNull(4) ? null : leitor.GetDateTime(4).ToLocalTime()
        };
    }

    // Tarefa Tipo de Tara (Ajuste 1): duplicidade GLOBAL — nome único por upper(trim(nome_tipo_tara))
    // INDEPENDENTE da situação (ativo OU inativo). NÃO filtra por situacao_tipo_tara.
    public virtual async Task<bool> ExisteNomeAsync(string nome, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM tipo_tara
                WHERE upper(trim(nome_tipo_tara)) = upper(trim(@nome_tipo_tara))
                  AND (@ignorar_codigo IS NULL OR codigo_tipo_tara <> @ignorar_codigo)
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@nome_tipo_tara", nome));
        comando.Parameters.Add(ParametroLongoNulo("@ignorar_codigo", ignorarCodigo));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    public virtual async Task<long> InserirAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO tipo_tara (nome_tipo_tara, descricao_tipo_tara, situacao_tipo_tara, tipo_tara_criado_por)
            VALUES (@nome_tipo_tara, @descricao_tipo_tara, @situacao_tipo_tara, @tipo_tara_criado_por)
            RETURNING codigo_tipo_tara;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroTexto("@nome_tipo_tara", tipo.NomeTipoTara));
            comando.Parameters.Add(ParametroTexto("@descricao_tipo_tara", tipo.DescricaoTipoTara));
            comando.Parameters.Add(ParametroBooleano("@situacao_tipo_tara", tipo.SituacaoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_criado_por", tipo.TipoTaraCriadoPor ?? ObterCodigoUsuarioSessao()));

            object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
            return retorno is long id ? id : 0;
        }, cancellationToken);
    }

    // Tarefa Tipo de Tara (Ajuste 8): AtualizarAsync edita SOMENTE nome/descrição. NÃO altera situacao_tipo_tara
    // (situação muda apenas por ExcluirAsync/ReativarAsync).
    public virtual async Task<int> AtualizarAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tipo_tara
            SET nome_tipo_tara = @nome_tipo_tara,
                descricao_tipo_tara = @descricao_tipo_tara,
                tipo_tara_atualizado_por = @tipo_tara_atualizado_por
            WHERE codigo_tipo_tara = @codigo_tipo_tara;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", tipo.CodigoTipoTara));
            comando.Parameters.Add(ParametroTexto("@nome_tipo_tara", tipo.NomeTipoTara));
            comando.Parameters.Add(ParametroTexto("@descricao_tipo_tara", tipo.DescricaoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_atualizado_por", tipo.TipoTaraAtualizadoPor ?? ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    // Tarefa Tipo de Tara (Ajuste 9): inativação com proteção ATÔMICA no próprio UPDATE — não inativa se houver
    // Tara ativa vinculada (evita corrida entre a verificação prévia do serviço e o UPDATE).
    public virtual async Task<int> ExcluirAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tipo_tara
               SET situacao_tipo_tara = false,
                   tipo_tara_atualizado_por = @tipo_tara_atualizado_por
             WHERE codigo_tipo_tara = @codigo_tipo_tara
               AND situacao_tipo_tara = true
               AND NOT EXISTS (
                   SELECT 1
                     FROM tara
                    WHERE codigo_tipo_tara = @codigo_tipo_tara
                      AND situacao_tara = true
               );
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    public virtual async Task<int> ReativarAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE tipo_tara
               SET situacao_tipo_tara = true,
                   tipo_tara_atualizado_por = @tipo_tara_atualizado_por
             WHERE codigo_tipo_tara = @codigo_tipo_tara
               AND situacao_tipo_tara = false;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));
            comando.Parameters.Add(ParametroLongoNulo("@tipo_tara_atualizado_por", ObterCodigoUsuarioSessao()));
            return await comando.ExecuteNonQueryAsync(cancellationToken);
        }, cancellationToken);
    }

    /// <summary>
    /// Tarefa Tipo de Tara (Ajuste 6): diagnóstico de nomes duplicados (global, por upper(trim(nome_tipo_tara))).
    /// Somente leitura — NÃO corrige dados. Serve para identificar registros a tratar antes de uma constraint global.
    /// </summary>
    public virtual async Task<IReadOnlyList<DuplicadoTipoTara>> ListarNomesDuplicadosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT upper(trim(nome_tipo_tara)) AS nome_normalizado,
                   count(*) AS quantidade,
                   string_agg(codigo_tipo_tara::text || ':' || nome_tipo_tara || ':' || situacao_tipo_tara::text, ', ' ORDER BY codigo_tipo_tara) AS registros
              FROM tipo_tara
             GROUP BY upper(trim(nome_tipo_tara))
            HAVING count(*) > 1;
            """;

        List<DuplicadoTipoTara> duplicados = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            duplicados.Add(new DuplicadoTipoTara(
                leitor.GetString(0),
                (int)leitor.GetInt64(1),
                leitor.IsDBNull(2) ? string.Empty : leitor.GetString(2)));
        }

        return duplicados;
    }

    /// <summary>
    /// Tarefa Tipo de Tara (Ajuste 3): existe alguma Tara ATIVA vinculada a este tipo de tara?
    /// Usado para bloquear a inativação. SQL parametrizado, somente leitura (sem alterar a tabela tara).
    /// </summary>
    public virtual async Task<bool> ExisteTaraAtivaVinculadaAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM tara
                WHERE codigo_tipo_tara = @codigo_tipo_tara
                  AND situacao_tara = true
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is bool existe && existe;
    }

    /// <summary>
    /// Tarefa Tipo de Tara (Ajuste 10): resumo de dependências ativas (nº de taras ativas vinculadas), no
    /// padrão de <c>CargoRepositorio.ObterResumoDependenciasAtivasAsync</c>. SQL parametrizado, somente leitura.
    /// </summary>
    public virtual async Task<ResumoDependenciasTipoTara> ObterResumoDependenciasAtivasAsync(long codigoTipoTara, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT count(*)
            FROM tara
            WHERE codigo_tipo_tara = @codigo_tipo_tara
              AND situacao_tara = true;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_tipo_tara", codigoTipoTara));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        int tarasAtivas = retorno is long total ? (int)total : 0;
        return new ResumoDependenciasTipoTara { TarasAtivas = tarasAtivas };
    }

    /// <summary>
    /// Tarefa Tipo de Tara (Ajuste 4): contagem de taras ATIVAS por tipo (referência exibida na tela).
    /// Uma única consulta agregada (evita N chamadas). Mapa codigo_tipo_tara → quantidade.
    /// </summary>
    public virtual async Task<IReadOnlyDictionary<long, int>> ContarTarasAtivasPorTipoAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tipo_tara, COUNT(*) AS total
            FROM tara
            WHERE situacao_tara = true
            GROUP BY codigo_tipo_tara;
            """;

        Dictionary<long, int> contagens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);

        while (await leitor.ReadAsync(cancellationToken))
        {
            contagens[leitor.GetInt64(0)] = (int)leitor.GetInt64(1);
        }

        return contagens;
    }
}
