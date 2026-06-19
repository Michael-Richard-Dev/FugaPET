using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Composicao unica do servico de pedidos de compra SAP.</summary>
public static class FabricaPedidoCompraSapServico
{
    public static IPedidoCompraSapServico Criar()
        => Criar(EstadoIntegracaoBanco.ModoDemonstracao);

    internal static IPedidoCompraSapServico Criar(bool modoDemonstracao)
    {
        if (modoDemonstracao)
        {
            return new PedidoCompraSapMockServico();
        }

        ConfiguracaoSap configuracaoSap = LeitorConfiguracaoSap.Carregar();
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
