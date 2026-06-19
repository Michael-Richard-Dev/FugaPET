namespace FugaPET_Dev.Modelo.IntegracaoSap;

public sealed record RegistroLogIntegracaoSap
{
    public required string TipoIntegracao { get; init; }
    public required string Operacao { get; init; }
    public required string Entidade { get; init; }
    public string? ChaveNegocio { get; init; }
    public long? CodigoUsuarioFugaPet { get; init; }
    public int? StatusHttp { get; init; }
    public long DuracaoMs { get; init; }
    public required Guid CorrelationId { get; init; }
    public required string Situacao { get; init; }
    public int Tentativa { get; init; } = 1;
    public string? MensagemTecnicaSanitizada { get; init; }
    public DateTimeOffset RegistradoEmUtc { get; init; } = DateTimeOffset.UtcNow;
}
