namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoOrdem
{
    public string NumeroOrdem { get; init; } = string.Empty;
    public string MaterialProduzido { get; init; } = string.Empty;
    public string DescricaoMaterial { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;
    public string DepositoDestino { get; init; } = string.Empty;
    public decimal QuantidadePlanejada { get; init; }
    public decimal QuantidadeEntregue { get; init; }
    public decimal QuantidadePendente { get; init; }
    public string Unidade { get; init; } = "KG";
    public string Lote { get; init; } = string.Empty;
    public string ItemOrdem { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string StatusOrdem { get; init; } = string.Empty;
    public bool Liberada { get; init; }
    public bool EncerradaOuDeletada { get; init; }
}
