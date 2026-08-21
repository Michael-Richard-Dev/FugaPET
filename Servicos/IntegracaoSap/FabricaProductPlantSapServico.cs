using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Composicao unica do servico de administracao de lote SAP (A_ProductPlant), espelhando
/// <see cref="FabricaProductMasterSapServico"/> (mesma API_PRODUCT_SRV). Demo/banco desabilitado → mock;
/// configuracao malformada → estado invalido; caso contrario → servico real (GET governado por Configurado).
/// </summary>
public static class FabricaProductPlantSapServico
{
    public static IProductPlantSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProductPlantSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProductPlantSapServico Criar(
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
            return new ProductPlantSapMockServico();
        }

        try
        {
            ConfiguracaoSap configuracaoSap = carregarConfiguracaoSap();
            return new ProductPlantSapServico(configuracaoSap);
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            ProductPlantSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: arquivo existente porem malformado. Administracao de lote bloqueada.");
            return new ProductPlantSapConfiguracaoInvalidaServico();
        }
    }
}
