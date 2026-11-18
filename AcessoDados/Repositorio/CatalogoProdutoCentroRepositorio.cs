using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.IntegracaoSap;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Acesso a <c>produto_catalogo_centro</c> (incremento 066): o espelho local dos materiais do centro.
/// <para>
/// Classe NAO selada e metodos virtuais pelo mesmo motivo do resto do cadastro: o servico e testado
/// com um repositorio falso, sem banco.
/// </para>
/// </summary>
public class CatalogoProdutoCentroRepositorio : RepositorioBase
{
    public CatalogoProdutoCentroRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    /// <summary>Materiais do centro que o SAP ainda lista, com o cadastro ja lido.</summary>
    public virtual async Task<IReadOnlyList<ProdutoCatalogoLocal>> ListarPorCentroAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_catalogo, centro, produto, descricao, tipo_material, unidade_base,
                   marcado_eliminacao, visto_em, mestre_lido_em, presente_no_sap
              FROM produto_catalogo_centro
             WHERE centro = @centro
             ORDER BY produto;
            """;

        List<ProdutoCatalogoLocal> itens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(Ler(leitor));
        }

        return itens;
    }

    /// <summary>
    /// Registra que estes materiais existem no centro AGORA.
    /// <para>
    /// Material novo entra com <c>mestre_lido_em</c> NULO — e assim que a proxima etapa sabe o que
    /// ainda precisa perguntar ao SAP. Material ja conhecido so tem <c>visto_em</c> renovado e volta
    /// a <c>presente_no_sap</c>; o cadastro dele NAO e invalidado, senao toda sincronizacao mandaria
    /// buscar tudo de novo e o incremental deixaria de existir.
    /// </para>
    /// </summary>
    /// <returns>Quantos materiais eram NOVOS.</returns>
    public virtual async Task<int> RegistrarVistosAsync(
        string centro, IReadOnlyList<string> produtos, CancellationToken cancellationToken = default)
    {
        if (produtos.Count == 0) return 0;

        const string sql = """
            WITH entrada AS (
                SELECT unnest(@produtos::text[]) AS produto
            ),
            gravados AS (
                INSERT INTO produto_catalogo_centro (centro, produto)
                SELECT @centro, produto FROM entrada
                ON CONFLICT (centro, produto) DO UPDATE
                   SET visto_em        = now(),
                       presente_no_sap = true,
                       atualizado_em   = now()
                RETURNING (xmax = 0) AS inserido
            )
            SELECT count(*) FILTER (WHERE inserido) FROM gravados;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.Add(new NpgsqlParameter("@produtos", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = produtos.ToArray(),
        });

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long novos ? (int)novos : 0;
    }

    /// <summary>
    /// Marca como ausentes os materiais do centro que NAO vieram na lista do SAP.
    /// <para>
    /// Marca, nao apaga: pode haver vinculo decidido sobre o material, e a tela precisa poder mostrar
    /// que ele saiu em vez de simplesmente deixar de exibi-lo.
    /// </para>
    /// </summary>
    public virtual async Task<int> MarcarAusentesAsync(
        string centro, IReadOnlyList<string> presentes, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE produto_catalogo_centro
               SET presente_no_sap = false,
                   atualizado_em   = now()
             WHERE centro = @centro
               AND presente_no_sap = true
               AND NOT (produto = ANY(@presentes::text[]));
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.Add(new NpgsqlParameter("@presentes", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = presentes.ToArray(),
        });

        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Materiais do centro cujo cadastro ainda nao foi lido. E a pergunta que torna a sincronizacao
    /// barata; ha indice parcial exatamente sobre ela.
    /// </summary>
    public virtual async Task<IReadOnlyList<string>> ListarPendentesDeMestreAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT produto
              FROM produto_catalogo_centro
             WHERE centro = @centro
               AND mestre_lido_em IS NULL
             ORDER BY produto;
            """;

        List<string> pendentes = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            pendentes.Add(leitor.GetString(0));
        }

        return pendentes;
    }

    /// <summary>
    /// Grava o cadastro lido do SAP e marca o material como resolvido.
    /// <para>
    /// A descricao vem separada porque ela nasce de outra entidade (A_ProductDescription) e pode
    /// faltar; material sem descricao continua resolvido — o codigo basta para vincular.
    /// </para>
    /// </summary>
    public virtual async Task<int> AtualizarMestresAsync(
        string centro,
        IReadOnlyList<ProdutoAcabadoDoCentro> mestres,
        CancellationToken cancellationToken = default)
    {
        if (mestres.Count == 0) return 0;

        const string sql = """
            UPDATE produto_catalogo_centro AS c
               SET descricao          = dados.descricao,
                   tipo_material      = dados.tipo,
                   unidade_base       = dados.unidade,
                   marcado_eliminacao = dados.eliminado,
                   mestre_lido_em     = now(),
                   atualizado_em      = now()
              FROM (
                SELECT unnest(@produtos::text[])    AS produto,
                       unnest(@descricoes::text[])  AS descricao,
                       unnest(@tipos::text[])       AS tipo,
                       unnest(@unidades::text[])    AS unidade,
                       unnest(@eliminados::bool[])  AS eliminado
              ) AS dados
             WHERE c.centro = @centro AND c.produto = dados.produto;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.Add(Texto("@produtos", [.. mestres.Select(m => m.Produto)]));
        comando.Parameters.Add(Texto("@descricoes", [.. mestres.Select(m => m.Descricao)]));
        comando.Parameters.Add(Texto("@tipos", [.. mestres.Select(m => m.TipoMaterial)]));
        comando.Parameters.Add(Texto("@unidades", [.. mestres.Select(m => m.UnidadeBase)]));
        comando.Parameters.Add(new NpgsqlParameter("@eliminados", NpgsqlDbType.Array | NpgsqlDbType.Boolean)
        {
            Value = mestres.Select(m => m.MarcadoParaEliminacao).ToArray(),
        });

        return await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Quando o centro foi sincronizado pela ultima vez; nulo quando nunca foi.</summary>
    public virtual async Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT max(visto_em) FROM produto_catalogo_centro WHERE centro = @centro;";

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is DateTime quando ? quando : null;
    }

    private static NpgsqlParameter Texto(string nome, string?[] valores)
        => new(nome, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = valores };

    private static ProdutoCatalogoLocal Ler(NpgsqlDataReader leitor) => new()
    {
        CodigoCatalogo = leitor.GetInt64(0),
        Centro = leitor.GetString(1),
        Produto = leitor.GetString(2),
        Descricao = leitor.IsDBNull(3) ? null : leitor.GetString(3),
        TipoMaterial = leitor.IsDBNull(4) ? null : leitor.GetString(4),
        UnidadeBase = leitor.IsDBNull(5) ? null : leitor.GetString(5),
        MarcadoEliminacao = leitor.GetBoolean(6),
        VistoEm = leitor.GetDateTime(7),
        MestreLidoEm = leitor.IsDBNull(8) ? null : leitor.GetDateTime(8),
        PresenteNoSap = leitor.GetBoolean(9),
    };
}
