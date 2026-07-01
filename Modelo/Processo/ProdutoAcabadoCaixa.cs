namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoCaixa
{
    public int NumeroCaixa { get; init; }
    public string CodigoCaixaLocal { get; init; } = string.Empty;
    public decimal PesoBrutoKg { get; init; }
    public decimal TaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public int QuantidadeProdutos { get; init; }
    public string OrigemPesagem { get; init; } = string.Empty;
    public string StatusSap { get; set; } = "PENDENTE_SAP";
    public string HandlingUnitCaixa { get; set; } = string.Empty;
    public string MaterialDocument { get; set; } = string.Empty;
    public string MaterialDocumentYear { get; set; } = string.Empty;
    public string CodigoPaleteLocal { get; set; } = string.Empty;
}
