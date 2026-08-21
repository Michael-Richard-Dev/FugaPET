using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Composição única do serviço de consulta da norma de embalagem (GET somente leitura). Demo/banco
/// desabilitado → não configurado (sem HTTP); configuração SAP malformada → não configurado; caso
/// contrário → serviço real (o GET é governado por <c>Configurado</c>/URL da norma). Sem fallback fictício.
/// </summary>
public static class FabricaProdutoAcabadoNormaEmbalagemSapServico
{
    public static IProdutoAcabadoNormaEmbalagemSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProdutoAcabadoNormaEmbalagemSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProdutoAcabadoNormaEmbalagemSapServico Criar(
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
            return new ProdutoAcabadoNormaEmbalagemSapNaoConfiguradoServico();
        }

        try
        {
            ConfiguracaoSap configuracaoSap = carregarConfiguracaoSap();
            return new ProdutoAcabadoNormaEmbalagemSapServico(configuracaoSap);
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            ProdutoAcabadoNormaEmbalagemSapServico.RegistrarDiagnostico(
                "Configuração SAP inválida: arquivo existente porém malformado. Consulta da norma bloqueada.");
            return new ProdutoAcabadoNormaEmbalagemSapNaoConfiguradoServico();
        }
    }
}
