namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoPesagemCaixa
{
    public decimal PesoBrutoKg { get; init; }
    public decimal TaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public string OrigemPesagem { get; init; } = string.Empty;
    public DateTime RegistradoEm { get; init; } = DateTime.Now;
}
