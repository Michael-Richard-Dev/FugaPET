namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Uma linha de A_ProductPlant lida na LISTAGEM por centro. Diferente de
/// <see cref="SapProductPlantDto"/>, que modela a consulta por chave composta de UM material, esta
/// nasce do percurso "todos os produtos do centro X".
/// </summary>
public sealed record ProdutoDoCentroSap
{
    public string Produto { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;

    /// <summary>Edm.Boolean; null quando ausente ou indeterminado na resposta.</summary>
    public bool? AdministradoPorLote { get; init; }
}

/// <summary>
/// Dados mestres do produto (A_Product), que o A_ProductPlant NAO traz: a entidade de centro nao
/// tem navegacao para o cadastro do material nesta instalacao (verificado na $metadata), entao a
/// descricao, o tipo e a unidade exigem uma segunda consulta.
/// </summary>
public sealed record ProdutoMestreSapCatalogo
{
    public string Produto { get; init; } = string.Empty;

    /// <summary>ProductType: ROH, HIBE, FERT, VERP... Classifica materia-prima x quimico x acabado.</summary>
    public string? TipoMaterial { get; init; }

    /// <summary>BaseUnit: unidade de medida base do material.</summary>
    public string? UnidadeBase { get; init; }

    /// <summary>ProductGroup: grupo de mercadorias.</summary>
    public string? GrupoMercadoria { get; init; }

    /// <summary>CrossPlantStatus: status do material entre centros (vazio = sem restricao).</summary>
    public string? StatusMaterial { get; init; }

    /// <summary>IsMarkedForDeletion: material marcado para eliminacao no SAP.</summary>
    public bool? MarcadoParaEliminacao { get; init; }
}

/// <summary>
/// Uma PAGINA da listagem: os itens lidos e o <c>__next</c> cru que o SAP devolveu, quando houver.
/// A paginacao nunca e presumida completa — quem consome decide se seguiu ate o fim.
/// </summary>
public sealed record PaginaCatalogoProdutoSap
{
    public IReadOnlyList<ProdutoDoCentroSap> Itens { get; init; } = [];

    /// <summary>Link cru da proxima pagina (d.__next). Null/vazio significa ultima pagina.</summary>
    public string? ProximaPagina { get; init; }

    /// <summary>False quando o envelope nao e interpretavel; nesse caso Itens vem vazio.</summary>
    public bool Valida { get; init; }

    public string? Motivo { get; init; }

    public static PaginaCatalogoProdutoSap Invalida(string motivo)
        => new() { Valida = false, Motivo = motivo };
}
