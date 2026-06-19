using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.Terminal;

public sealed class ResolvedorContextoTerminalLocal
{
    private readonly ConfiguracaoTerminalLocal _configuracaoTerminal;

    public ResolvedorContextoTerminalLocal()
        : this(LeitorConfiguracaoTerminalLocal.Carregar())
    {
    }

    public ResolvedorContextoTerminalLocal(ConfiguracaoTerminalLocal configuracaoTerminal)
    {
        _configuracaoTerminal = configuracaoTerminal;
    }

    public ContextoTerminalLocal ResolverPorMaquinaAtual()
    {
        string maquina = Environment.MachineName.Trim().ToUpperInvariant();
        TerminalLocalItem? item = _configuracaoTerminal.Terminais.FirstOrDefault(x =>
            string.Equals(x.Maquina.Trim(), maquina, StringComparison.OrdinalIgnoreCase));

        item ??= _configuracaoTerminal.Padrao;

        return new ContextoTerminalLocal
        {
            MaquinaWindows = maquina,
            NomeTerminal = string.IsNullOrWhiteSpace(item.NomeTerminal) ? maquina : item.NomeTerminal,
            BancoLocalNome = string.IsNullOrWhiteSpace(item.BancoLocalNome) ? "LOCAL" : item.BancoLocalNome,
            IdSetorPadrao = item.IdSetorPadrao,
            IdBalancaPadrao = item.IdBalancaPadrao,
            ImpressoraPadrao = item.ImpressoraPadrao,
            IdEtiquetaPadrao = item.IdEtiquetaPadrao,
            IdTaraPadrao = item.IdTaraPadrao
        };
    }
}
