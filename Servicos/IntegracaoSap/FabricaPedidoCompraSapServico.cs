using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Composicao unica do servico de pedidos de compra SAP.</summary>
public static class FabricaPedidoCompraSapServico
{
    public static IPedidoCompraSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IPedidoCompraSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IPedidoCompraSapServico Criar(
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
            return new PedidoCompraSapMockServico(podeUsarDadosSimulados);
        }

        ConfiguracaoSap configuracaoSap;
        try
        {
            configuracaoSap = carregarConfiguracaoSap();
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            // Erro de implantacao: NAO cair em mock nem em "nao configurado". Registra evento tecnico
            // sanitizado (sem caminho/conteudo do arquivo) e devolve um servico em estado de erro.
            SincronizacaoPedidoCompraSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: arquivo existente porem malformado. Integracao SAP bloqueada.");
            return new PedidoCompraSapConfiguracaoInvalidaServico();
        }

        FabricaConexaoPostgreSql fabricaConexao =
            new(LeitorConfiguracaoBancoPostgreSql.Carregar());
        SapPedidoCompraRepositorio pedidoRepositorio = new(fabricaConexao);
        LogIntegracaoSapRepositorio logIntegracaoRepositorio = new(fabricaConexao);
        ConfiguracaoGeralRepositorio configuracaoRepositorio = new(fabricaConexao);
        AuditoriaAcaoUsuarioRepositorio auditoriaRepositorio = new(fabricaConexao);
        AuditoriaServico auditoriaServico = new(
            new AuditoriaAcaoUsuarioServico(auditoriaRepositorio));
        EstadoIntegracaoSapServico estadoServico = new(
            configuracaoSap,
            configuracaoRepositorio,
            auditoriaServico);
        SincronizacaoPedidoCompraSapServico servicoReal =
            new(
                configuracaoSap,
                pedidoRepositorio,
                new LogIntegracaoSapServico(logIntegracaoRepositorio));

        return new PedidoCompraSapGovernadoServico(servicoReal, estadoServico);
    }
}
