namespace FugaPET_Dev.Modelo.Auditoria;

public sealed class RegistroAuditoriaLogin
{
    public long? CodigoUsuario { get; init; }
    public string LoginTentado { get; init; } = string.Empty;
    public bool Sucesso { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public DateTime RegistradoEm { get; init; } = DateTime.Now;
}
