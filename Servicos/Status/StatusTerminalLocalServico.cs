using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Status;

namespace FugaPET_Dev.Servicos.Status;

public sealed class StatusTerminalLocalServico
{
    private readonly ConfiguracaoTerminalLocal _configuracaoTerminal;

    public StatusTerminalLocalServico()
        : this(LeitorConfiguracaoTerminalLocal.Carregar())
    {
    }

    public StatusTerminalLocalServico(ConfiguracaoTerminalLocal configuracaoTerminal)
    {
        _configuracaoTerminal = configuracaoTerminal;
    }

    public ItemStatusIndustrial ObterStatus()
    {
        string maquina = Environment.MachineName.Trim().ToUpperInvariant();
        TerminalLocalItem? terminalConfigurado = _configuracaoTerminal.Terminais.FirstOrDefault(x =>
            string.Equals(x.Maquina.Trim(), maquina, StringComparison.OrdinalIgnoreCase));

        bool localizado = terminalConfigurado is not null;
        TerminalLocalItem terminal = terminalConfigurado ?? _configuracaoTerminal.Padrao;
        string nomeTerminal = string.IsNullOrWhiteSpace(terminal.NomeTerminal)
            ? maquina
            : terminal.NomeTerminal;
        string bancoLocal = string.IsNullOrWhiteSpace(terminal.BancoLocalNome)
            ? "LOCAL"
            : terminal.BancoLocalNome;

        return new ItemStatusIndustrial
        {
            Nome = "Terminal Local",
            Identificador = nomeTerminal,
            Ambiente = bancoLocal,
            Habilitado = true,
            Online = localizado,
            Situacao = localizado ? "Configurado" : "Padrão",
            Mensagem = localizado
                ? $"Terminal {nomeTerminal} configurado para a máquina {maquina}."
                : $"Máquina {maquina} não encontrada na configuração. Usando terminal padrão {nomeTerminal}.",
            AtualizadoEm = DateTimeOffset.Now
        };
    }
}
