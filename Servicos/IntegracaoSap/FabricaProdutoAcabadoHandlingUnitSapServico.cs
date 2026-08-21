using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Composição única do gateway de HU de caixa (POST /HandlingUnit). FAIL-CLOSED por padrão:
/// banco demonstrativo/desabilitado, configuração SAP malformada, OU autorização HU desabilitada
/// (<c>FUGAPET_SAP_HU_WRITE_ENABLED</c>=false / sem URL de HU) ⇒ gateway NÃO AUTORIZADO (sem rede).
/// Só quando <see cref="ConfiguracaoSap.HandlingUnitConfigurado"/> é true o gateway REAL é criado,
/// autorizado EXCLUSIVAMENTE para POST /HandlingUnit. Nunca depende de FUGAPET_SAP_WRITE_ENABLED.
/// </summary>
public static class FabricaProdutoAcabadoHandlingUnitSapServico
{
    public static IProdutoAcabadoHandlingUnitSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IProdutoAcabadoHandlingUnitSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
        => Criar(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo, LeitorConfiguracaoSap.Carregar);

    internal static IProdutoAcabadoHandlingUnitSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo,
        Func<ConfiguracaoSap> carregarConfiguracaoSap)
    {
        bool podeUsarDadosSimulados =
            EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(bancoHabilitado, modoDemonstracao, ambienteDemonstrativo);
        if (podeUsarDadosSimulados)
        {
            return new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado();
        }

        ConfiguracaoSap configuracao;
        try
        {
            configuracao = carregarConfiguracaoSap();
        }
        catch (ConfiguracaoSapInvalidaException)
        {
            return new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado();
        }

        // Fail-closed: sem autorização HU específica (ou sem URL/credenciais/allowlist) NÃO cria gateway real.
        if (!configuracao.HandlingUnitConfigurado)
        {
            return new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado();
        }

        // §13: o modo HU controlado EXIGE a escrita SAP genérica DESLIGADA. HU=true + WRITE_ENABLED=true é
        // configuração incompatível ⇒ fail-closed (a flag HU nunca liga/desliga a genérica).
        if (configuracao.EscritaHabilitada)
        {
            return new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado();
        }

        // §9: valida efetivamente HTTPS + host na allowlist + sem credenciais na URL. Inválida ⇒ fail-closed.
        try
        {
            ValidadorUrlSap.ValidarBaseUrl(configuracao.HandlingUnitBaseUrl, configuracao.HostsPermitidos);
        }
        catch (InvalidOperationException)
        {
            return new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado();
        }

        return new ProdutoAcabadoHandlingUnitSapGateway(
            configuracao.HandlingUnitBaseUrl,
            configuracao.HostsPermitidos,
            configuracao.Usuario,
            configuracao.Senha,
            envioAutorizado: true, // já validado por HuWriteHabilitado + URL/allowlist
            fabricaHandler: FabricaHttpClientSap.CriarHandler,
            timeout: TimeSpan.FromSeconds(Math.Max(1, configuracao.TimeoutSegundos)));
    }
}
