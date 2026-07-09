using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.10.1: composição única do serviço de descrição do material SAP (A_ProductDescription),
/// espelhando <see cref="FabricaProductionOrderSapServico"/>. Demo/banco desabilitado → mock (sem fake);
/// configuração malformada → estado inválido; caso contrário → serviço real (GET governado por Configurado).
/// </summary>
public static class FabricaProductDescriptionSapServico
{
    public static IProductDescriptionSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProductDescriptionSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProductDescriptionSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        bool podeUsarDadosSimulados =
            EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(
                bancoHabilitado,
                modoDemonstracao,
                ambienteDemonstrativo);
        if (podeUsarDadosSimulados)
        {
            return new ProductDescriptionSapMockServico();
        }

        try
        {
            ConfiguracaoSap configuracaoSap = carregarConfiguracaoSap();
            return new ProductDescriptionSapServico(configuracaoSap);
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            ProductDescriptionSapServico.RegistrarDiagnostico(
                "Configuração SAP inválida: arquivo existente porém malformado. Descrição bloqueada.");
            return new ProductDescriptionSapConfiguracaoInvalidaServico();
        }
    }
}
