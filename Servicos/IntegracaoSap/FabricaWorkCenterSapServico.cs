using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public static class FabricaWorkCenterSapServico
{
    public static IWorkCenterSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IWorkCenterSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IWorkCenterSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        if (EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo))
        {
            return new WorkCenterSapMockServico();
        }

        try
        {
            return new WorkCenterSapServico(carregarConfiguracaoSap());
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            return new WorkCenterSapFailClosedServico();
        }
    }
}
