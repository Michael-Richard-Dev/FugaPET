namespace FugaPET_Dev.Servicos.Terminal;

public static class EstadoTerminalLocalAtual
{
    private static readonly Lazy<ContextoTerminalLocal> ContextoAtual = new(() =>
        new ResolvedorContextoTerminalLocal().ResolverPorMaquinaAtual());

    public static ContextoTerminalLocal Contexto => ContextoAtual.Value;
}
