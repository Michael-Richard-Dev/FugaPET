namespace FugaPET_Dev.Servicos.Terminal;

public sealed class ContextoTerminalLocal
{
    public string MaquinaWindows { get; init; } = string.Empty;
    public string NomeTerminal { get; init; } = string.Empty;
    public string BancoLocalNome { get; init; } = "LOCAL";
    public long? IdSetorPadrao { get; init; }
    public long? IdBalancaPadrao { get; init; }
    public string ImpressoraPadrao { get; init; } = string.Empty;
    public long? IdEtiquetaPadrao { get; init; }
    public long? IdTaraPadrao { get; init; }
}
