using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal interface ILogIntegracaoSapServico
{
    Task RegistrarAsync(
        RegistroLogIntegracaoSap registro,
        CancellationToken cancellationToken = default);
}
