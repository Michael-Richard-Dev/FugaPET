using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Acesso a <c>produto_etiqueta_vinculo</c> (incremento 065).
/// <para>
/// Classe NAO selada e metodos virtuais pelo mesmo motivo do resto do cadastro: o servico e testado
/// com um repositorio falso, sem banco.
/// </para>
/// </summary>
public class VinculoProdutoEtiquetaRepositorio : RepositorioBase
{
    public VinculoProdutoEtiquetaRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Vinculos do centro, com o nome da etiqueta ja resolvido.
    /// <para>
    /// O LEFT JOIN e proposital: um vinculo "sem etiqueta" nao tem o que juntar, e perde-lo num INNER
    /// JOIN sumiria com a decisao do usuario da lista.
    /// </para>
    /// </summary>
    public virtual async Task<IReadOnlyList<(VinculoProdutoEtiqueta Vinculo, string NomeEtiqueta)>> ListarPorCentroAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT v.codigo_vinculo, v.centro, v.produto, v.descricao_produto, v.tipo_produto,
                   v.codigo_etiqueta, v.sem_etiqueta, v.processo_pesagem, v.situacao,
                   v.criado_em, v.criado_por, v.atualizado_em, v.atualizado_por,
                   COALESCE(e.nome_etiqueta, '') AS nome_etiqueta
              FROM produto_etiqueta_vinculo v
              LEFT JOIN etiqueta e ON e.codigo_etiqueta = v.codigo_etiqueta
             WHERE v.centro = @centro
             ORDER BY v.produto;
            """;

        List<(VinculoProdutoEtiqueta, string)> itens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add((Ler(leitor), leitor.GetString(leitor.GetOrdinal("nome_etiqueta"))));
        }

        return itens;
    }

    /// <summary>
    /// Grava a decisao do produto: insere, ou atualiza a que ja existia.
    /// <para>
    /// O <c>ON CONFLICT (centro, produto)</c> e o que materializa o "um pra um" sem corrida: dois
    /// terminais salvando o mesmo produto ao mesmo tempo terminam com UMA linha, a ultima, em vez de
    /// uma delas estourar com violacao de unicidade na cara do operador.
    /// </para>
    /// </summary>
    public virtual async Task<long> SalvarAsync(
        VinculoProdutoEtiqueta vinculo, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO produto_etiqueta_vinculo
                   (centro, produto, descricao_produto, tipo_produto,
                    codigo_etiqueta, sem_etiqueta, processo_pesagem, situacao, criado_por)
            VALUES (@centro, @produto, @descricao, @tipo,
                    @codigo_etiqueta, @sem_etiqueta, @processo, @situacao, @usuario)
            ON CONFLICT (centro, produto) DO UPDATE
               SET descricao_produto = EXCLUDED.descricao_produto,
                   tipo_produto      = EXCLUDED.tipo_produto,
                   codigo_etiqueta   = EXCLUDED.codigo_etiqueta,
                   sem_etiqueta      = EXCLUDED.sem_etiqueta,
                   processo_pesagem  = EXCLUDED.processo_pesagem,
                   situacao          = EXCLUDED.situacao,
                   atualizado_em     = now(),
                   atualizado_por    = EXCLUDED.criado_por
            RETURNING codigo_vinculo;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", vinculo.Centro);
        comando.Parameters.AddWithValue("@produto", vinculo.Produto);
        comando.Parameters.AddWithValue("@descricao", (object?)vinculo.DescricaoProduto ?? DBNull.Value);
        comando.Parameters.AddWithValue("@tipo", (object?)vinculo.TipoProduto ?? DBNull.Value);
        comando.Parameters.AddWithValue("@codigo_etiqueta", (object?)vinculo.CodigoEtiqueta ?? DBNull.Value);
        comando.Parameters.AddWithValue("@sem_etiqueta", vinculo.SemEtiqueta);
        comando.Parameters.AddWithValue("@processo", VinculoProdutoEtiqueta.TextoDoProcesso(vinculo.ProcessoPesagem));
        comando.Parameters.AddWithValue("@situacao", vinculo.Situacao);
        comando.Parameters.AddWithValue("@usuario", (object?)vinculo.CriadoPor ?? DBNull.Value);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : 0L;
    }

    /// <summary>
    /// Apaga o vinculo, devolvendo o produto ao estado "nao configurado".
    /// <para>
    /// E apagar mesmo, nao inativar: "sem etiqueta" ja e um estado gravado e significa outra coisa.
    /// Um vinculo inativo seria um terceiro estado sem significado no dominio.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">Cancelamento da operacao.</param>
    /// <returns>
    /// O codigo do vinculo apagado, ou <c>null</c> quando nao havia vinculo.
    /// <para>
    /// Devolve o CODIGO, e nao um booleano, porque a auditoria da exclusao precisa dizer QUAL
    /// registro saiu. Com booleano o log registrava "Registro 0", que nao serve para rastrear nada.
    /// O <c>RETURNING</c> resolve sem uma consulta extra antes do DELETE.
    /// </para>
    /// </returns>
    public virtual async Task<long?> RemoverAsync(
        string centro, string produto, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM produto_etiqueta_vinculo
             WHERE centro = @centro AND produto = @produto
            RETURNING codigo_vinculo;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.AddWithValue("@produto", produto);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : null;
    }

    /// <summary>Quantos produtos usam uma etiqueta — para a tela de etiqueta nao inativar o que esta em uso.</summary>
    public virtual async Task<int> ContarProdutosPorEtiquetaAsync(
        long codigoEtiqueta, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT count(*) FROM produto_etiqueta_vinculo
             WHERE codigo_etiqueta = @codigo_etiqueta AND situacao = true;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@codigo_etiqueta", codigoEtiqueta);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long n ? (int)n : 0;
    }

    private static VinculoProdutoEtiqueta Ler(NpgsqlDataReader leitor) => new()
    {
        CodigoVinculo = leitor.GetInt64(leitor.GetOrdinal("codigo_vinculo")),
        Centro = leitor.GetString(leitor.GetOrdinal("centro")),
        Produto = leitor.GetString(leitor.GetOrdinal("produto")),
        DescricaoProduto = TextoOuNulo(leitor, "descricao_produto"),
        TipoProduto = TextoOuNulo(leitor, "tipo_produto"),
        CodigoEtiqueta = LongOuNulo(leitor, "codigo_etiqueta"),
        SemEtiqueta = leitor.GetBoolean(leitor.GetOrdinal("sem_etiqueta")),
        ProcessoPesagem = VinculoProdutoEtiqueta.ProcessoDoTexto(leitor.GetString(leitor.GetOrdinal("processo_pesagem"))),
        Situacao = leitor.GetBoolean(leitor.GetOrdinal("situacao")),
        CriadoEm = DataOuNulo(leitor, "criado_em"),
        CriadoPor = LongOuNulo(leitor, "criado_por"),
        AtualizadoEm = DataOuNulo(leitor, "atualizado_em"),
        AtualizadoPor = LongOuNulo(leitor, "atualizado_por"),
    };

    private static string? TextoOuNulo(NpgsqlDataReader leitor, string coluna)
    {
        int i = leitor.GetOrdinal(coluna);
        return leitor.IsDBNull(i) ? null : leitor.GetString(i);
    }

    private static long? LongOuNulo(NpgsqlDataReader leitor, string coluna)
    {
        int i = leitor.GetOrdinal(coluna);
        return leitor.IsDBNull(i) ? null : leitor.GetInt64(i);
    }

    private static DateTime? DataOuNulo(NpgsqlDataReader leitor, string coluna)
    {
        int i = leitor.GetOrdinal(coluna);
        return leitor.IsDBNull(i) ? null : leitor.GetDateTime(i);
    }
}
