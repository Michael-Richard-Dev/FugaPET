namespace FugaPET_Dev.Modelo.Processo;

public sealed class ProdutoAcabadoLancamento
{
    public ProdutoAcabadoOrdem Ordem { get; init; } = new();
    public ProdutoAcabadoNormaEmbalagem NormaEmbalagem { get; init; } = new();
    public IReadOnlyList<ProdutoAcabadoCaixa> Caixas { get; init; } = [];
    public IReadOnlyList<ProdutoAcabadoPalete> Paletes { get; init; } = [];
    public string Usuario { get; init; } = string.Empty;
    public DateTime CriadoEm { get; init; } = DateTime.Now;
    public decimal PesoLiquidoTotalKg => Caixas.Sum(caixa => caixa.PesoLiquidoKg);
}
