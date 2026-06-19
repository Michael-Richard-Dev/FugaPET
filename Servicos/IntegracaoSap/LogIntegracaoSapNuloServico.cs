using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal sealed class LogIntegracaoSapNuloServico : ILogIntegracaoSapServico
{
    public static LogIntegracaoSapNuloServico Instancia { get; } = new();

    private LogIntegracaoSapNuloServico()
    {
    }

    public Task RegistrarAsync(
        RegistroLogIntegracaoSap registro,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
