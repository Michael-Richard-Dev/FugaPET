namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoPalete
{
    public string CodigoPaleteLocal { get; init; } = string.Empty;
    public int PrimeiraCaixa { get; init; }
    public int UltimaCaixa { get; init; }
    public decimal PesoBrutoKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public decimal TaraKg { get; init; }
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public string PackagingMaterial { get; init; } = string.Empty;
    public IReadOnlyList<ProdutoAcabadoCaixa> Caixas { get; init; } = [];
    public long? CodigoHuPalete { get; set; }
    public string HandlingUnitPalete { get; set; } = string.Empty;
    public string StatusSap { get; set; } = "RASCUNHO";
}

