using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 048-E REV2: composicao unica do servico de resolucao do roteiro (marcador PP_FORM). Simulado ⇒ mock;
/// configuracao SAP invalida ⇒ fail-closed; caso contrario ⇒ servico real. Sem POST/PATCH/escrita.
/// </summary>
public static class FabricaProductionRoutingSapServico
{
    public static IProductionRoutingSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProductionRoutingSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProductionRoutingSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        if (EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo))
        {
            return new ProductionRoutingSapMockServico();
        }

        try
        {
            return new ProductionRoutingSapServico(carregarConfiguracaoSap());
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            ProductionRoutingSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: roteiro indisponivel. Classificacao PP_FORM bloqueada (fail-closed).");
            return new ProductionRoutingSapFailClosedServico();
        }
    }
}
