namespace FugaPET_Dev.Modelo.Processo;

public sealed class PesagemSemiAcabado
{
    public int Sequencia { get; init; }
    public decimal PesoBrutoKg { get; init; }
    public decimal PesoTaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public string Origem { get; init; } = "MANUAL";
    public DateTime RegistradoEm { get; init; } = DateTime.Now;
}
