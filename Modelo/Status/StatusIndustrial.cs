namespace FugaPET_Dev.Modelo.Status;

public sealed class StatusIndustrial
{
    public ItemStatusIndustrial Banco { get; init; } = new();
    public ItemStatusIndustrial TerminalLocal { get; init; } = new();
    public ItemStatusIndustrial BalancaConfigurada { get; init; } = new();
    public ItemStatusIndustrial ImpressoraConfigurada { get; init; } = new();
    public ItemStatusIndustrial CacheSapLocal { get; init; } = new();
    public DateTimeOffset AtualizadoEm { get; init; } = DateTimeOffset.Now;
}
