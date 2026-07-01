using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Composicao unica do servico de Ordens de Producao SAP (Tela de Consumo de Materia-Prima).</summary>
public static class FabricaProductionOrderSapServico
{
    public static IProductionOrderSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProductionOrderSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProductionOrderSapServico Criar(
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
            return new ProductionOrderSapMockServico();
        }

        ConfiguracaoSap configuracaoSap;
        try
        {
            configuracaoSap = carregarConfiguracaoSap();
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            ProductionOrderSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: arquivo existente porem malformado. Consulta de OP bloqueada.");
            return new ProductionOrderSapConfiguracaoInvalidaServico();
        }

        FabricaConexaoPostgreSql fabricaConexao =
            new(LeitorConfiguracaoBancoPostgreSql.Carregar());
        ConfiguracaoGeralRepositorio configuracaoRepositorio = new(fabricaConexao);
        AuditoriaAcaoUsuarioRepositorio auditoriaRepositorio = new(fabricaConexao);
        AuditoriaServico auditoriaServico = new(
            new AuditoriaAcaoUsuarioServico(auditoriaRepositorio));
        EstadoIntegracaoSapServico estadoServico = new(
            configuracaoSap,
            configuracaoRepositorio,
            auditoriaServico);

        return new ProductionOrderSapGovernadoServico(
            new ProductionOrderSapServico(configuracaoSap),
            estadoServico);
    }
}
