namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Composição REAL do gateway INT012 a partir da <see cref="ConfiguracaoSap"/>. Fail-closed: sem
/// <c>PalletWriteHabilitado</c> (FUGAPET_SAP_PALLET_WRITE_ENABLED=false, default) ⇒ gateway NÃO autorizado
/// (zero HTTP). Endpoint path INT012 confirmado é anexado à base configurável (host nunca hardcoded).
/// Nunca depende de FUGAPET_SAP_WRITE_ENABLED. Nenhum POST real durante esta REV (gate false).
/// </summary>
public static class FabricaProdutoAcabadoPaleteInt012Gateway
{
    /// <summary>
    /// GATE 126N: path do INT012 usado SOMENTE como referência de documentação.
    /// <para>
    /// ELE NÃO É MAIS ANEXADO À BASE. Até 16/11/2026 a fábrica descartava o caminho configurado e
    /// colava este literal, que carrega o segmento <c>QS4_110</c> — a rota do QAS110. Consequência:
    /// o DEV110 postava palete na rota do QAS, e o SAP respondia
    /// "ZPP_CREATEHU/003 Dados embalagem não encontrado!", porque material e HUs não são os mesmos lá.
    /// A URL configurada para o DEV110, sem o segmento de sistema, nunca era usada: só o host dela.
    /// </para>
    /// <para>
    /// Comprovado por eliminação: o MESMO corpo, com os MESMOS cabeçalhos e as MESMAS credenciais,
    /// retornou 200 e criou a UC quando enviado à rota configurada, e 500 quando enviado pelo
    /// aplicativo. A rota era a única variável restante.
    /// </para>
    /// </summary>
    public const string EndpointPathReferencia =
        "/http/QS4_110/pesagem/handling_unit/CreateHUInput/1111/SAP__self.processHandlingUnitPayload";

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
            && configuracao.PalletInt012HostsPermitidos.Count > 0
            // GATE 046-K: credencial CPI DEDICADA obrigatoria (sem fallback SAP). Ausente => nao autorizado.
            && !string.IsNullOrWhiteSpace(configuracao.PalletInt012Usuario)
            && !string.IsNullOrWhiteSpace(configuracao.PalletInt012Senha);
        string endpoint = MontarEndpoint(configuracao.PalletInt012BaseUrl);

        return new ProdutoAcabadoPaleteInt012Gateway(
            baseUrl: endpoint,
            hostsPermitidos: configuracao.PalletInt012HostsPermitidos,
            usuario: configuracao.PalletInt012Usuario,
            senha: configuracao.PalletInt012Senha,
            envioAutorizado: autorizado,
            fabricaHandler: fabricaHandler ?? FabricaHttpClientSap.CriarHandler,
            timeout: TimeSpan.FromSeconds(configuracao.TimeoutSegundos));
    }

    /// <summary>
    /// GATE 126N: o endpoint é o que está CONFIGURADO, com o caminho preservado. Cada instalação
    /// aponta para a sua rota no CPI — o DEV110 para a sem segmento de sistema, as linhas Q para a
    /// com <c>QS4_110</c>.
    /// <para>
    /// Base sem caminho próprio devolve VAZIO, e o gateway nasce fail-closed. Antes a fábrica
    /// adivinhava o caminho; adivinhar rota de escrita foi exatamente o que fez o DEV110 postar no
    /// QAS. Preferimos não enviar a enviar para o sistema errado.
    /// </para>
    /// </summary>
    internal static string MontarEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri))
        {
            return string.Empty;
        }

        // Caminho ausente ("/" ou vazio) nao identifica a rota INT012: nao se completa por conta propria.
        string caminho = uri.AbsolutePath;
        if (caminho.Length <= 1)
        {
            return string.Empty;
        }

        return uri.AbsoluteUri;
    }
}
