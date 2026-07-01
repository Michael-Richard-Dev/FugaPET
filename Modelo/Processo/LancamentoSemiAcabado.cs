namespace FugaPET_Dev.Modelo.Processo;

public sealed class LancamentoSemiAcabado
{
    public SemiAcabadoOrdem Ordem { get; init; } = new();
    public IReadOnlyList<PesagemSemiAcabado> Pesagens { get; init; } = [];
    public decimal PesoLiquidoTotalKg => Pesagens.Sum(p => p.PesoLiquidoKg);
    public string Usuario { get; init; } = string.Empty;
    public DateTime CriadoEm { get; init; } = DateTime.Now;
}
