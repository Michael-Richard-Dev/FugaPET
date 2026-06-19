namespace FugaPET_Dev.AcessoDados.Banco;

public sealed class ConfiguracaoTerminalLocal
{
    public TerminalLocalItem Padrao { get; init; } = new();
    public IReadOnlyList<TerminalLocalItem> Terminais { get; init; } = [];
}

public sealed class TerminalLocalItem
{
    public string Maquina { get; init; } = string.Empty;
    public string NomeTerminal { get; init; } = string.Empty;
    public string BancoLocalNome { get; init; } = "LOCAL";
    public long? IdSetorPadrao { get; init; }
    public long? IdBalancaPadrao { get; init; }
    public string ImpressoraPadrao { get; init; } = string.Empty;
    public long? IdEtiquetaPadrao { get; init; }
    public long? IdTaraPadrao { get; init; }
}
