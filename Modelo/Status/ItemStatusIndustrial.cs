namespace FugaPET_Dev.Modelo.Status;

public sealed class ItemStatusIndustrial
{
    public string Nome { get; init; } = string.Empty;
    public string Identificador { get; init; } = string.Empty;
    public string Ambiente { get; init; } = string.Empty;
    public bool Habilitado { get; init; }
    public bool Online { get; init; }
    public string Situacao { get; init; } = string.Empty;
    public string Mensagem { get; init; } = string.Empty;
    public DateTimeOffset AtualizadoEm { get; init; } = DateTimeOffset.Now;
}
