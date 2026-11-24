using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Acesso a <c>tolerancia_recebimento_material</c> (incremento 069).
/// <para>
/// Classe NAO selada e metodos virtuais pelo mesmo motivo do resto do cadastro: o servico e testado
/// com um repositorio falso, sem banco.
/// </para>
/// </summary>
public class ToleranciaRecebimentoMaterialRepositorio : RepositorioBase
{
    public ToleranciaRecebimentoMaterialRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    /// <summary>Tolerancias do centro, inclusive as inativas — a tela precisa mostrar que existem.</summary>
    public virtual async Task<IReadOnlyList<ToleranciaRecebimentoMaterial>> ListarPorCentroAsync(
        string centro, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tolerancia, centro, material, percentual_tolerancia,
                   descricao_material, tipo_material, situacao,
                   criado_em, criado_por, atualizado_em, atualizado_por
              FROM tolerancia_recebimento_material
             WHERE centro = @centro
             ORDER BY material;
            """;

        List<ToleranciaRecebimentoMaterial> itens = [];
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
    /// A tolerancia de UM material do centro, ou <c>null</c> quando nao foi configurado.
    /// <para>
    /// Existe para o RECEBIMENTO, nao para o cadastro: a Entrada precisa do limite do material do item
    /// a cada pesagem, e carregar o centro inteiro para achar uma linha seria desperdicio num caminho
    /// que roda o tempo todo.
    /// </para>
    /// <para>
    /// Devolve a linha INATIVA tambem, em vez de filtrar por <c>situacao</c> aqui. Quem decide o que
    /// inativa significa e <see cref="FugaPET_Dev.Servicos.Processo.RegraTolerancia"/>, num
    /// lugar so — um filtro aqui faria "inativa" virar "inexistente" antes de a regra opinar, e as duas
    /// coisas poderiam divergir no dia em que o significado mudasse.
    /// </para>
    /// </summary>
    public virtual async Task<ToleranciaRecebimentoMaterial?> ObterPorMaterialAsync(
        string centro, string material, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_tolerancia, centro, material, percentual_tolerancia,
                   descricao_material, tipo_material, situacao,
                   criado_em, criado_por, atualizado_em, atualizado_por
              FROM tolerancia_recebimento_material
             WHERE centro = @centro AND material = @material;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.AddWithValue("@material", material);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? Ler(leitor) : null;
    }

    /// <summary>
    /// Grava a tolerancia do material: insere, ou atualiza a que ja existia.
    /// <para>
    /// O <c>ON CONFLICT (centro, material)</c> e o que materializa "uma tolerancia por material" sem
    /// corrida: dois terminais salvando o mesmo material ao mesmo tempo terminam com UMA linha, a
    /// ultima, em vez de um deles estourar violacao de unicidade na cara do operador.
    /// </para>
    /// </summary>
    public virtual async Task<long> SalvarAsync(
        ToleranciaRecebimentoMaterial tolerancia, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO tolerancia_recebimento_material
                   (centro, material, percentual_tolerancia, descricao_material, tipo_material,
                    situacao, criado_por)
            VALUES (@centro, @material, @percentual, @descricao, @tipo, @situacao, @usuario)
            ON CONFLICT (centro, material) DO UPDATE
               SET percentual_tolerancia = EXCLUDED.percentual_tolerancia,
                   descricao_material    = EXCLUDED.descricao_material,
                   tipo_material         = EXCLUDED.tipo_material,
                   situacao              = EXCLUDED.situacao,
                   atualizado_em         = now(),
                   atualizado_por        = EXCLUDED.criado_por
            RETURNING codigo_tolerancia;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", tolerancia.Centro);
        comando.Parameters.AddWithValue("@material", tolerancia.Material);
        comando.Parameters.AddWithValue("@percentual", tolerancia.PercentualTolerancia);
        comando.Parameters.AddWithValue("@descricao", (object?)tolerancia.DescricaoMaterial ?? DBNull.Value);
        comando.Parameters.AddWithValue("@tipo", (object?)tolerancia.TipoMaterial ?? DBNull.Value);
        comando.Parameters.AddWithValue("@situacao", tolerancia.Situacao);
        comando.Parameters.AddWithValue("@usuario", (object?)tolerancia.CriadoPor ?? DBNull.Value);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : 0L;
    }

    /// <summary>
    /// Apaga a tolerancia, devolvendo o material ao estado "nao configurado".
    /// <para>
    /// Devolve o CODIGO, nao um booleano, porque a auditoria precisa dizer QUAL registro saiu — com
    /// booleano o log registra "Registro 0", que nao rastreia nada.
    /// </para>
    /// <para>
    /// ATENCAO ao efeito operacional: sem linha, o Recebimento do material fica BLOQUEADO (decisao do
    /// usuario em 23/11/2026). Apagar nao e "voltar ao normal" — e parar de receber aquele material ate
    /// alguem cadastrar de novo.
    /// </para>
    /// </summary>
    public virtual async Task<long?> RemoverAsync(
        string centro, string material, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM tolerancia_recebimento_material
             WHERE centro = @centro AND material = @material
            RETURNING codigo_tolerancia;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.AddWithValue("@material", material);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : null;
    }

    private static ToleranciaRecebimentoMaterial Ler(NpgsqlDataReader leitor) => new()
    {
        CodigoTolerancia = leitor.GetInt64(leitor.GetOrdinal("codigo_tolerancia")),
        Centro = leitor.GetString(leitor.GetOrdinal("centro")),
        Material = leitor.GetString(leitor.GetOrdinal("material")),
        PercentualTolerancia = leitor.GetDecimal(leitor.GetOrdinal("percentual_tolerancia")),
        DescricaoMaterial = TextoOuNulo(leitor, "descricao_material"),
        TipoMaterial = TextoOuNulo(leitor, "tipo_material"),
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
