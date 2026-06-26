using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Composicao unica do servico de Material Document SAP (movimento 101). Espelha
/// <see cref="FabricaPedidoCompraSapServico"/>: mock em demonstracao, governado em ambiente real.</summary>
public static class FabricaMaterialDocumentSapServico
{
    public static IMaterialDocumentSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IMaterialDocumentSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IMaterialDocumentSapServico Criar(
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
            return new MaterialDocumentSapMockServico(podeUsarDadosSimulados);
        }

        ConfiguracaoSap configuracaoSap;
        try
        {
            configuracaoSap = carregarConfiguracaoSap();
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            SincronizacaoPedidoCompraSapServico.RegistrarDiagnostico(
                "Configuracao SAP invalida: arquivo existente porem malformado. Material Document bloqueado.");
            return new MaterialDocumentSapConfiguracaoInvalidaServico();
        }

        FabricaConexaoPostgreSql fabricaConexao =
            new(LeitorConfiguracaoBancoPostgreSql.Carregar());
        LogIntegracaoSapRepositorio logIntegracaoRepositorio = new(fabricaConexao);
        ConfiguracaoGeralRepositorio configuracaoRepositorio = new(fabricaConexao);
        AuditoriaAcaoUsuarioRepositorio auditoriaRepositorio = new(fabricaConexao);
        AuditoriaServico auditoriaServico = new(
            new AuditoriaAcaoUsuarioServico(auditoriaRepositorio));
        EstadoIntegracaoSapServico estadoServico = new(
            configuracaoSap,
            configuracaoRepositorio,
            auditoriaServico);
        MaterialDocumentSapServico servicoReal = new(
            configuracaoSap,
            new LogIntegracaoSapServico(logIntegracaoRepositorio),
            cliente: null);

        return new MaterialDocumentSapGovernadoServico(servicoReal, estadoServico);
    }
}
