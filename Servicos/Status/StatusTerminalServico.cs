using FugaPET_Dev.Modelo.Status;

namespace FugaPET_Dev.Servicos.Status;

public sealed class StatusTerminalServico
{
    private readonly StatusTerminalLocalServico _statusTerminalLocalServico;

    public StatusTerminalServico()
        : this(new StatusTerminalLocalServico())
    {
    }

    public StatusTerminalServico(StatusTerminalLocalServico statusTerminalLocalServico)
    {
        _statusTerminalLocalServico = statusTerminalLocalServico;
    }

    public ItemStatusIndustrial ObterStatus()
        => _statusTerminalLocalServico.ObterStatus();
}
