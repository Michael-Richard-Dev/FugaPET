namespace FugaPET_Dev.Servicos.Terminal;

public static class EstadoTerminalLocalAtual
{
    private static readonly object Sincronizacao = new();
    private static ContextoTerminalLocal? _contextoAtual;

    public static ContextoTerminalLocal Contexto
    {
        get
        {
            lock (Sincronizacao)
            {
                return _contextoAtual ??= ResolverContexto();
            }
        }
    }

    public static ContextoTerminalLocal Recarregar()
    {
        lock (Sincronizacao)
        {
            _contextoAtual = ResolverContexto();
            return _contextoAtual;
        }
    }

    public static ContextoTerminalLocal ObterContextoAtualizado()
        => Recarregar();

    private static ContextoTerminalLocal ResolverContexto()
        => new ResolvedorContextoTerminalLocal().ResolverPorMaquinaAtual();
}
