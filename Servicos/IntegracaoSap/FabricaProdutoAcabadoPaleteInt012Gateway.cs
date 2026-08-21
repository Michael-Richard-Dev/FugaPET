namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Composição REAL do gateway INT012 a partir da <see cref="ConfiguracaoSap"/>. Fail-closed: sem
/// <c>PalletWriteHabilitado</c> (FUGAPET_SAP_PALLET_WRITE_ENABLED=false, default) ⇒ gateway NÃO autorizado
/// (zero HTTP). Endpoint path INT012 confirmado é anexado à base configurável (host nunca hardcoded).
/// Nunca depende de FUGAPET_SAP_WRITE_ENABLED. Nenhum POST real durante esta REV (gate false).
/// </summary>
public static class FabricaProdutoAcabadoPaleteInt012Gateway
{
    /// <summary>Path confirmado do INT012 (CPI) — anexado à base configurada.</summary>
    public const string EndpointPath = "/http/pesagem/handling_unit/CreateHUInput/1111/SAP__self.processHandlingUnitPayload";

    public static IProdutoAcabadoPaleteInt012Gateway Criar(
        ConfiguracaoSap configuracao,
        Func<HttpMessageHandler>? fabricaHandler = null)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        // REV5-§9: gate específico do palete (default false). Base do CPI/INT012 vem EXCLUSIVAMENTE de
        // PalletInt012BaseUrl (nunca de HandlingUnitBaseUrl) e a allowlist vem EXCLUSIVAMENTE de
        // PalletInt012HostsPermitidos — SEM fallback para a allowlist SAP genérica (HostsPermitidos). Qualquer
        // um vazio ⇒ gateway fail-closed (zero HTTP), independentemente do gate. Separação preservada.
        bool autorizado = configuracao.PalletWriteHabilitado
            && !string.IsNullOrWhiteSpace(configuracao.PalletInt012BaseUrl)
            && configuracao.PalletInt012HostsPermitidos.Count > 0;
        string endpoint = MontarEndpoint(configuracao.PalletInt012BaseUrl);

        return new ProdutoAcabadoPaleteInt012Gateway(
            baseUrl: endpoint,
            hostsPermitidos: configuracao.PalletInt012HostsPermitidos,
            usuario: configuracao.Usuario,
            senha: configuracao.Senha,
            envioAutorizado: autorizado,
            fabricaHandler: fabricaHandler ?? FabricaHttpClientSap.CriarHandler,
            timeout: TimeSpan.FromSeconds(configuracao.TimeoutSegundos));
    }

    internal static string MontarEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
        {
            return string.Empty;
        }

        // Usa somente esquema://host (path da base é ignorado) + o path INT012 confirmado.
        return $"{uri.Scheme}://{uri.Authority}{EndpointPath}";
    }
}
