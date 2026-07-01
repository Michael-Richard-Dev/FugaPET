namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoNormaEmbalagem
{
    public string Material { get; init; } = string.Empty;
    public string PackagingInstruction { get; init; } = string.Empty;
    public IReadOnlyList<ProdutoAcabadoNormaItem> Itens { get; init; } = [];
    public int QuantidadeProdutosPorCaixa { get; init; }
    public string MaterialCaixa { get; init; } = string.Empty;
    public string Unidade { get; init; } = "UN";
}

public sealed class ProdutoAcabadoNormaItem
{
    public string Material { get; init; } = string.Empty;
    public string TipoMaterial { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public string Item { get; init; } = string.Empty;
}
