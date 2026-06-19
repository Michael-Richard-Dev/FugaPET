namespace FugaPET_Dev.Modelo.Auditoria;

public sealed class LogAlteracaoCadastralRegistro
{
    public long CodigoLogAlteracaoCadastral { get; init; }
    public string Tabela { get; init; } = string.Empty;
    public long CodigoRegistro { get; init; }
    public string Operacao { get; init; } = string.Empty;
    public long? CodigoUsuario { get; init; }
    public DateTime CriadoEm { get; init; }
    public string? DadosAnterioresJson { get; init; }
    public string? DadosNovosJson { get; init; }
}
