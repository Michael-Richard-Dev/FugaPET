namespace FugaPET_Dev.Modelo.Consumo;

public sealed class ConsultaConsumoMaterialFiltro
{
    public string? NumeroOrdem { get; init; }
    public string? StatusLancamento { get; init; }
    public DateTime? CriadoDeUtc { get; init; }
    public DateTime? CriadoAteUtc { get; init; }
    public int Limite { get; init; } = 200;
}
