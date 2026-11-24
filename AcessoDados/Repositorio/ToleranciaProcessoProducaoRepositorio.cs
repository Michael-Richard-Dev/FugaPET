using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Cadastro;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Acesso a <c>tolerancia_processo_producao</c> (incremento 071).
/// <para>
/// Mesma forma do repositório do 069, com o PROCESSO na chave. Classe não selada e métodos virtuais
/// pelo mesmo motivo do resto do cadastro: o serviço é testado com um repositório falso, sem banco.
/// </para>
/// </summary>
public class ToleranciaProcessoProducaoRepositorio : RepositorioBase
{
    public ToleranciaProcessoProducaoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    private const string Colunas = """
        codigo_tolerancia, centro, processo, material, percentual_tolerancia,
        descricao_material, tipo_material, situacao,
        criado_em, criado_por, atualizado_em, atualizado_por
        """;

    /// <summary>
    /// Tolerâncias do centro NUM processo, inclusive as inativas — a tela precisa mostrar que existem.
    /// </summary>
    public virtual async Task<IReadOnlyList<ToleranciaProcessoProducao>> ListarPorCentroEProcessoAsync(
        string centro, string processo, CancellationToken cancellationToken = default)
    {
        string sql = $"""
            SELECT {Colunas}
              FROM tolerancia_processo_producao
             WHERE centro = @centro AND processo = @processo
             ORDER BY material;
            """;

        List<ToleranciaProcessoProducao> itens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.AddWithValue("@processo", processo);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(Ler(leitor));
        }

        return itens;
    }

    /// <summary>
    /// A tolerância de UM produto num processo, ou <c>null</c> quando não foi configurado.
    /// <para>
    /// Existe para as TELAS DE PROCESSO, não para o cadastro: o Consumo e o Semi-Acabado precisam do
    /// limite a cada pesagem, e carregar o centro inteiro para achar uma linha seria desperdício num
    /// caminho que roda o tempo todo.
    /// </para>
    /// <para>
    /// Devolve a linha INATIVA também, em vez de filtrar por <c>situacao</c> aqui. Quem decide o que
    /// inativa significa é <see cref="FugaPET_Dev.Servicos.Processo.RegraTolerancia"/>, num lugar só.
    /// </para>
    /// </summary>
    public virtual async Task<ToleranciaProcessoProducao?> ObterAsync(
        string centro, string processo, string material, CancellationToken cancellationToken = default)
    {
        string sql = $"""
            SELECT {Colunas}
              FROM tolerancia_processo_producao
             WHERE centro = @centro AND processo = @processo AND material = @material
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.AddWithValue("@processo", processo);
        comando.Parameters.AddWithValue("@material", material);

        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? Ler(leitor) : null;
    }

    /// <summary>
    /// Grava a decisão. O <c>ON CONFLICT (centro, processo, material)</c> é o que materializa "uma
    /// tolerância por produto em cada processo" sem corrida: dois terminais salvando o mesmo produto
    /// ao mesmo tempo terminam com UMA linha, a última, em vez de um deles estourar violação de
    /// unicidade na cara do operador.
    /// </summary>
    public virtual async Task<long> SalvarAsync(
        ToleranciaProcessoProducao tolerancia, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tolerancia);

        const string sql = """
            INSERT INTO tolerancia_processo_producao
                   (centro, processo, material, percentual_tolerancia, descricao_material,
                    tipo_material, situacao, criado_por)
            VALUES (@centro, @processo, @material, @percentual, @descricao, @tipo, @situacao, @usuario)
            ON CONFLICT (centro, processo, material) DO UPDATE
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
        comando.Parameters.AddWithValue("@processo", tolerancia.Processo);
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
    /// Apaga a tolerância, devolvendo o produto ao estado "não configurado".
    /// <para>
    /// Devolve o CÓDIGO, não um booleano, porque a auditoria precisa dizer QUAL registro saiu — com
    /// booleano o log registra "Registro 0", que não rastreia nada.
    /// </para>
    /// <para>
    /// ATENÇÃO ao efeito operacional: sem linha, a pesagem daquele produto NAQUELE PROCESSO fica
    /// BLOQUEADA (decisão do usuário em 24/11/2026). Apagar não é "voltar ao normal" — é parar a
    /// produção daquele produto até alguém cadastrar de novo.
    /// </para>
    /// </summary>
    public virtual async Task<long?> RemoverAsync(
        string centro, string processo, string material, CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM tolerancia_processo_producao
             WHERE centro = @centro AND processo = @processo AND material = @material
            RETURNING codigo_tolerancia;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("@centro", centro);
        comando.Parameters.AddWithValue("@processo", processo);
        comando.Parameters.AddWithValue("@material", material);

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : null;
    }

    private static ToleranciaProcessoProducao Ler(NpgsqlDataReader leitor) => new()
    {
        CodigoTolerancia = leitor.GetInt64(leitor.GetOrdinal("codigo_tolerancia")),
        Centro = leitor.GetString(leitor.GetOrdinal("centro")),
        Processo = leitor.GetString(leitor.GetOrdinal("processo")),
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
