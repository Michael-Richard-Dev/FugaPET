namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>Campos de rastreabilidade lidos da resposta OData V2 do SAP no envio do consumo 261.</summary>
public sealed record ConsumoMaterialSap261Response
{
    public string? MaterialDocument { get; init; }
    public string? MaterialDocumentYear { get; init; }
}
