using System.IO.Ports;
using System.Reflection;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Terminal;

namespace FugaPET_Dev.Tests.Operacao;

/// <summary>
/// Integração: BalancaLeituraServico deve transportar BalancaCadastro.Protocolo (normalizado) para
/// BalancaLeituraConfiguracao.Protocolo, sem alterar porta/baud/data bits/paridade/stop bits nem a busca da
/// balança padrão do terminal.
/// </summary>
public sealed class BalancaLeituraServicoProtocoloTests : IDisposable
{
    private const long CodigoBalancaPadrao = 42;

    public BalancaLeituraServicoProtocoloTests()
        => DefinirContextoTerminal(CodigoBalancaPadrao);

    public void Dispose() => LimparContextoTerminal();

    [Fact]
    public async Task ObterConfiguracao_TransportaProtocoloNormalizado_EPreservaSerial()
    {
        BalancaCadastro balanca = new()
        {
            CodigoBalanca = CodigoBalancaPadrao,
            TipoConexao = "SERIAL",
            SituacaoBalanca = true,
            PortaSerial = "COM7",
            BaudRate = 9600,
            DataBits = 8,
            Paridade = "PAR",     // -> Parity.Even
            StopBits = 2m,        // -> StopBits.Two
            Protocolo = "  p03  " // -> "P03"
        };

        BalancaLeituraConfiguracao config = await ObterConfiguracaoAsync(balanca);

        // Protocolo transportado e normalizado.
        Assert.Equal("P03", config.Protocolo);
        // Parâmetros seriais preservados.
        Assert.Equal("COM7", config.PortaSerial);
        Assert.Equal(9600, config.BaudRate);
        Assert.Equal(8, config.DataBits);
        Assert.Equal(Parity.Even, config.Paridade);
        Assert.Equal(StopBits.Two, config.StopBits);
    }

    [Fact]
    public async Task ObterConfiguracao_ProtocoloVazio_ViraStringVazia()
    {
        BalancaCadastro balanca = new()
        {
            CodigoBalanca = CodigoBalancaPadrao,
            TipoConexao = "USB",
            SituacaoBalanca = true,
            PortaSerial = "COM3",
            BaudRate = 4800,
            DataBits = 7,
            Paridade = "EVEN",
            StopBits = 1m,
            Protocolo = "   "
        };

        BalancaLeituraConfiguracao config = await ObterConfiguracaoAsync(balanca);

        Assert.Equal(string.Empty, config.Protocolo);
        Assert.Equal("COM3", config.PortaSerial);
        Assert.Equal(4800, config.BaudRate);
    }

    private static async Task<BalancaLeituraConfiguracao> ObterConfiguracaoAsync(BalancaCadastro balanca)
    {
        BalancaLeituraServico servico = new(new RepositorioFake(balanca), new LeitorBalancaSerialServico());
        MethodInfo metodo = typeof(BalancaLeituraServico).GetMethod(
            "ObterConfiguracaoBalancaPadraoAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task<BalancaLeituraConfiguracao> task = (Task<BalancaLeituraConfiguracao>)metodo.Invoke(
            servico, new object[] { CancellationToken.None })!;
        return await task;
    }

    private static void DefinirContextoTerminal(long idBalancaPadrao)
        => CampoContexto().SetValue(null, new ContextoTerminalLocal { IdBalancaPadrao = idBalancaPadrao });

    private static void LimparContextoTerminal() => CampoContexto().SetValue(null, null);

    private static FieldInfo CampoContexto()
        => typeof(EstadoTerminalLocalAtual).GetField("_contextoAtual", BindingFlags.NonPublic | BindingFlags.Static)!;

    private sealed class RepositorioFake : BalancaRepositorio
    {
        private readonly BalancaCadastro _balanca;

        public RepositorioFake(BalancaCadastro balanca) : base(null!) => _balanca = balanca;

        public override Task<BalancaCadastro?> ObterPorIdAsync(long codigoBalanca, CancellationToken cancellationToken = default)
            => Task.FromResult<BalancaCadastro?>(codigoBalanca == _balanca.CodigoBalanca ? _balanca : null);
    }
}
