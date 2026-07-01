using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Composicao unica do envio de consumo 261 (mock em demonstracao, real em ambiente operacional).</summary>
public static class FabricaConsumoMaterialSap261Servico
{
    public static IConsumoMaterialSap261Servico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IConsumoMaterialSap261Servico Criar(
        bool bancoHabilitado, bool modoDemonstracao, bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IConsumoMaterialSap261Servico Criar(
        bool bancoHabilitado, bool modoDemonstracao, bool ambienteDemonstrativo,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        bool podeUsarDadosSimulados = EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(
            bancoHabilitado, modoDemonstracao, ambienteDemonstrativo);
        if (podeUsarDadosSimulados)
        {
            return new ConsumoMaterialSap261MockServico();
        }

        ConfiguracaoSap configuracaoSap;
        try
        {
            configuracaoSap = carregarConfiguracaoSap();
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            SincronizacaoPedidoCompraSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: arquivo existente porem malformado. Envio de consumo 261 bloqueado.");
            return new ConsumoMaterialSap261MockServico();
        }

        FabricaConexaoPostgreSql fabricaConexao = new(LeitorConfiguracaoBancoPostgreSql.Carregar());
        LogIntegracaoSapRepositorio logIntegracaoRepositorio = new(fabricaConexao);
        return new ConsumoMaterialSap261Servico(
            configuracaoSap,
            new LogIntegracaoSapServico(logIntegracaoRepositorio),
            cliente: null);
    }
}
