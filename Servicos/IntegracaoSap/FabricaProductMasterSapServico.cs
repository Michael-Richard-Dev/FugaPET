using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 24.1: composição única do serviço de tipo mestre do material SAP (A_Product), espelhando
/// <see cref="FabricaProductDescriptionSapServico"/>. Demo/banco desabilitado → mock; configuração malformada →
/// estado inválido; caso contrário → serviço real (GET governado por Configurado).
/// </summary>
public static class FabricaProductMasterSapServico
{
    public static IProductMasterSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProductMasterSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProductMasterSapServico Criar(
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
            return new ProductMasterSapMockServico();
        }

        try
        {
            ConfiguracaoSap configuracaoSap = carregarConfiguracaoSap();
            return new ProductMasterSapServico(configuracaoSap);
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            ProductMasterSapServico.RegistrarDiagnostico(
                "Configuração SAP inválida: arquivo existente porém malformado. Tipo de material bloqueado.");
            return new ProductMasterSapConfiguracaoInvalidaServico();
        }
    }
}
