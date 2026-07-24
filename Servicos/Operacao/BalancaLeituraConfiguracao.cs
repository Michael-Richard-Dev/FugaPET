using System.IO.Ports;

namespace FugaPET_Dev.Servicos.Operacao;

public sealed class BalancaLeituraConfiguracao
{
    public string PortaSerial { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 4800;
    public int DataBits { get; init; } = 7;
    public Parity Paridade { get; init; } = Parity.Even;
    public StopBits StopBits { get; init; } = StopBits.One;

    // Protocolo do cadastro da balança (ex.: "P03"), usado pelo leitor serial para escolher a escala decimal
    // do valor bruto. Normalizado (Trim + upper) na origem.
    public string Protocolo { get; init; } = string.Empty;
}
