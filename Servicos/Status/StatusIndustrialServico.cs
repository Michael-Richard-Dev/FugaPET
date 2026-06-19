using FugaPET_Dev.Modelo.Banco;
using FugaPET_Dev.Modelo.Status;
using FugaPET_Dev.Servicos.Banco;

namespace FugaPET_Dev.Servicos.Status;

public sealed class StatusIndustrialServico
{
    private readonly StatusBancoServico _statusBancoServico;
    private readonly StatusTerminalLocalServico _statusTerminalLocalServico;
    private readonly StatusBalancaConfiguradaServico _statusBalancaConfiguradaServico;
    private readonly StatusImpressoraConfiguradaServico _statusImpressoraConfiguradaServico;
    private readonly StatusCacheSapLocalServico _statusCacheSapLocalServico;

    public StatusIndustrialServico()
        : this(
            new StatusBancoServico(),
            new StatusTerminalLocalServico(),
            new StatusBalancaConfiguradaServico(),
            new StatusImpressoraConfiguradaServico(),
            new StatusCacheSapLocalServico())
    {
    }

    public StatusIndustrialServico(
        StatusBancoServico statusBancoServico,
        StatusTerminalLocalServico statusTerminalLocalServico,
        StatusBalancaConfiguradaServico statusBalancaConfiguradaServico,
        StatusImpressoraConfiguradaServico statusImpressoraConfiguradaServico,
        StatusCacheSapLocalServico statusCacheSapLocalServico)
    {
        _statusBancoServico = statusBancoServico;
        _statusTerminalLocalServico = statusTerminalLocalServico;
        _statusBalancaConfiguradaServico = statusBalancaConfiguradaServico;
        _statusImpressoraConfiguradaServico = statusImpressoraConfiguradaServico;
        _statusCacheSapLocalServico = statusCacheSapLocalServico;
    }

    public async Task<StatusIndustrial> ObterStatusAsync(CancellationToken cancellationToken = default)
    {
        StatusBanco statusBanco = await _statusBancoServico.ObterStatusAsync(cancellationToken);
        ItemStatusIndustrial statusTerminalLocal = _statusTerminalLocalServico.ObterStatus();
        ItemStatusIndustrial statusBalancaConfigurada =
            await _statusBalancaConfiguradaServico.ObterStatusAsync(cancellationToken);
        ItemStatusIndustrial statusImpressoraConfigurada = _statusImpressoraConfiguradaServico.ObterStatus();
        ItemStatusIndustrial statusCacheSapLocal =
            await _statusCacheSapLocalServico.ObterStatusAsync(cancellationToken);
        DateTimeOffset atualizadoEm = DateTimeOffset.Now;

        return new StatusIndustrial
        {
            Banco = CriarStatusBanco(statusBanco, atualizadoEm),
            TerminalLocal = statusTerminalLocal,
            BalancaConfigurada = statusBalancaConfigurada,
            ImpressoraConfigurada = statusImpressoraConfigurada,
            CacheSapLocal = statusCacheSapLocal,
            AtualizadoEm = atualizadoEm
        };
    }

    private static ItemStatusIndustrial CriarStatusBanco(StatusBanco statusBanco, DateTimeOffset atualizadoEm)
    {
        return new ItemStatusIndustrial
        {
            Nome = "Banco de Dados",
            Identificador = statusBanco.NomeBanco,
            Ambiente = ObterAmbienteBanco(statusBanco.NomeBanco),
            Habilitado = statusBanco.IntegracaoHabilitada,
            Online = statusBanco.Conectado,
            Situacao = ObterSituacaoBanco(statusBanco),
            Mensagem = statusBanco.Mensagem,
            AtualizadoEm = atualizadoEm
        };
    }

    private static string ObterSituacaoBanco(StatusBanco statusBanco)
    {
        if (!statusBanco.IntegracaoHabilitada)
        {
            return "Desabilitado";
        }

        return statusBanco.Conectado ? "Conectado" : "Offline";
    }

    private static string ObterAmbienteBanco(string nomeBanco)
    {
        if (nomeBanco.Contains("homolog", StringComparison.OrdinalIgnoreCase)
            || nomeBanco.Contains("_hml", StringComparison.OrdinalIgnoreCase)
            || nomeBanco.EndsWith("hml", StringComparison.OrdinalIgnoreCase))
        {
            return "HML";
        }

        if (nomeBanco.Contains("prod", StringComparison.OrdinalIgnoreCase)
            || nomeBanco.Contains("prd", StringComparison.OrdinalIgnoreCase))
        {
            return "PRD";
        }

        if (nomeBanco.Contains("dev", StringComparison.OrdinalIgnoreCase))
        {
            return "DEV";
        }

        return "Banco";
    }
}
