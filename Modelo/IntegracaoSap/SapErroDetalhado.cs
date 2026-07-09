namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>Detalhe tecnico de erro OData SAP, sem credenciais.</summary>
public sealed record SapErroDetalhado
{
    public string? Codigo { get; init; }
    public string? Mensagem { get; init; }
    public string? Detalhes { get; init; }
    public string RetornoBruto { get; init; } = string.Empty;
}
