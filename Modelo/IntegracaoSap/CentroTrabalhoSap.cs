namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>Master data reduzido de A_WorkCenters usado para resolver a rota Plant + WorkCenter.</summary>
public sealed record CentroTrabalhoSap
{
    public string WorkCenterInternalId { get; init; } = string.Empty;
    public string WorkCenterTypeCode { get; init; } = string.Empty;
    public string WorkCenter { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public bool WorkCenterIsToBeDeleted { get; init; }
}
