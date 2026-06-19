using System.IO.Ports;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class BalancaLeituraConfiguracao
{
    public string PortaSerial { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 4800;
    public int DataBits { get; init; } = 7;
    public Parity Paridade { get; init; } = Parity.Even;
    public StopBits StopBits { get; init; } = StopBits.One;
}
