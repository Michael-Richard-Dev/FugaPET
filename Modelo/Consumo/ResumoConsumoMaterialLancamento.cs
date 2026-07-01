namespace FugaPET_Dev.Modelo.Consumo;

public sealed class ResumoConsumoMaterialLancamento
{
    public long CodigoLancamento { get; init; }
    public string NumeroOrdem { get; init; } = string.Empty;
    public string? Centro { get; init; }
    public string? MaterialProduzido { get; init; }
    public string StatusLancamento { get; init; } = string.Empty;
    public decimal QuantidadeTotalConsumidaLocal { get; init; }
    public string? Unidade { get; init; }
    public string? DocumentoMaterialSap { get; init; }
    public string? ExercicioDocumentoMaterialSap { get; init; }
    public DateTime CriadoEmUtc { get; init; }
    public DateTime? EnviadoSapEmUtc { get; init; }
}
