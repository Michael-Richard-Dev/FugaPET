namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Cabecalho de um lancamento de Entrada de Produto (desenvolvimento.entrada_produto_lancamento).
/// Cada lancamento e independente; nunca sobrescreve um anterior.
/// </summary>
public sealed record EntradaProdutoLancamento
{
    public string NumeroPedido { get; init; } = string.Empty;
    public string? Fornecedor { get; init; }
    public long? CodigoSetor { get; init; }
    public string? Terminal { get; init; }

    public IReadOnlyList<EntradaProdutoItem> Itens { get; init; } = [];
}
