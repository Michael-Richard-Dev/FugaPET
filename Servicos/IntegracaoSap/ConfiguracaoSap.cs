using System.Text.RegularExpressions;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Configuracao de conexao com a API SAP (OData V4, Basic Authentication).
/// Credenciais NUNCA sao lidas de arquivo: devem vir exclusivamente de variaveis de ambiente.
/// </summary>
public sealed class ConfiguracaoSap
{
    /// <summary>URL base do servico OData do pedido de compra (ate .../purchaseorder/0001).</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// URL base do servico OData de Movimentos de Material (API_MATERIAL_DOCUMENT_SRV), usada para
    /// criar o documento de material (movimento 101) da Entrada de Produto. Independente da BaseUrl
    /// do pedido de compra; quando vazia, o envio de entrada e bloqueado.
    /// </summary>
    public string MaterialDocumentBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// URL base do servico OData de Ordens de Producao (API_PRODUCTION_ORDER_2_SRV), usada para o GET
    /// da Ordem de Producao na Tela de Consumo de Materia-Prima. Independente da BaseUrl do pedido de
    /// compra; quando vazia, a consulta da OP fica indisponivel.
    /// </summary>
    public string ProductionOrderBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// URL base do servico OData de Confirmacao de Producao (API_PROD_ORDER_CONFIRMATION_2_SRV).
    /// Usada para POST em ProdnOrdConf2 no fluxo Backflush.
    /// </summary>
    public string ProductionOrderConfirmationBaseUrl { get; init; } = string.Empty;

    public string Usuario { get; init; } = string.Empty;

    public string Senha { get; init; } = string.Empty;

    /// <summary>Mandante SAP (parametro de query sap-client). Opcional.</summary>
    public string SapClient { get; init; } = string.Empty;

    /// <summary>Hosts SAP aceitos, sem protocolo, porta ou caminho.</summary>
    public IReadOnlyList<string> HostsPermitidos { get; init; } = [];

    /// <summary>
    /// Chave de ativacao controlada da escrita SAP. Permanece false por padrao.
    /// A operacao ainda exige permissao SAP especifica e CSRF. Se o recurso fornecer
    /// ETag, o valor real e obrigatoriamente enviado; nunca e usado If-Match "*".
    /// </summary>
    public bool EscritaHabilitada { get; init; }

    public int TimeoutSegundos { get; init; } = 30;

    /// <summary>True quando ha URL + credenciais suficientes para chamar o SAP real.</summary>
    public bool Configurado =>
        !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    /// <summary>
    /// True quando, alem da configuracao base (credenciais + allowlist), ha URL do servico de
    /// Material Document para criar o movimento 101 da Entrada de Produto.
    /// </summary>
    public bool MaterialDocumentConfigurado =>
        !string.IsNullOrWhiteSpace(MaterialDocumentBaseUrl)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    /// <summary>
    /// URL base efetiva do servico de Ordens de Producao. Usa <see cref="ProductionOrderBaseUrl"/>
    /// quando definida; caso contrario, REAPROVEITA o padrao SAP ja configurado derivando do
    /// <see cref="MaterialDocumentBaseUrl"/> (e, em ultimo caso, do <see cref="BaseUrl"/>) — troca o
    /// segmento de servico OData (API_*_SRV) por API_PRODUCTION_ORDER_2_SRV, mantendo host/caminho.
    /// </summary>
    public string ProductionOrderBaseUrlEfetiva
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ProductionOrderBaseUrl))
            {
                return ProductionOrderBaseUrl;
            }

            string derivadoDoMaterialDocument = DerivarUrlProductionOrder(MaterialDocumentBaseUrl);
            return !string.IsNullOrWhiteSpace(derivadoDoMaterialDocument)
                ? derivadoDoMaterialDocument
                : DerivarUrlProductionOrder(BaseUrl);
        }
    }

    public string ProductionOrderConfirmationBaseUrlEfetiva
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ProductionOrderConfirmationBaseUrl))
            {
                return ProductionOrderConfirmationBaseUrl;
            }

            string derivadoDoMaterialDocument = DerivarUrlServicoSap(
                MaterialDocumentBaseUrl,
                "API_PROD_ORDER_CONFIRMATION_2_SRV");
            return !string.IsNullOrWhiteSpace(derivadoDoMaterialDocument)
                ? derivadoDoMaterialDocument
                : DerivarUrlServicoSap(BaseUrl, "API_PROD_ORDER_CONFIRMATION_2_SRV");
        }
    }

    private static string DerivarUrlProductionOrder(string urlServicoSap)
    {
        if (string.IsNullOrWhiteSpace(urlServicoSap))
        {
            return string.Empty;
        }

        // Caminho OData padrao SAP: .../sap/opu/odata/sap/API_<SERVICO>_SRV[/...]. Troca so o servico.
        Match correspondencia = Regex.Match(
            urlServicoSap,
            @"^(?<prefixo>.*/)API_[A-Za-z0-9_]+_SRV(?<sufixo>(/.*)?)$");
        return correspondencia.Success
            ? $"{correspondencia.Groups["prefixo"].Value}API_PRODUCTION_ORDER_2_SRV{correspondencia.Groups["sufixo"].Value}"
            : string.Empty;
    }

    private static string DerivarUrlServicoSap(string urlServicoSap, string nomeServico)
    {
        if (string.IsNullOrWhiteSpace(urlServicoSap))
        {
            return string.Empty;
        }

        Match correspondencia = Regex.Match(
            urlServicoSap,
            @"^(?<prefixo>.*/)API_[A-Za-z0-9_]+_SRV(?<sufixo>(/.*)?)$");
        return correspondencia.Success
            ? $"{correspondencia.Groups["prefixo"].Value}{nomeServico}{correspondencia.Groups["sufixo"].Value}"
            : string.Empty;
    }

    /// <summary>
    /// True quando, alem das credenciais + allowlist, ha URL (explicita OU derivada) do servico de
    /// Ordens de Producao (API_PRODUCTION_ORDER_2_SRV) para o GET da OP na Tela de Consumo.
    /// </summary>
    public bool ProductionOrderConfigurado =>
        !string.IsNullOrWhiteSpace(ProductionOrderBaseUrlEfetiva)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    public bool ProductionOrderConfirmationConfigurado =>
        !string.IsNullOrWhiteSpace(ProductionOrderConfirmationBaseUrlEfetiva)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    public const string MensagemProductionOrderNaoConfigurado =
        "Integração SAP de Ordem de Produção não configurada.";

    public const string MensagemProductionOrderConfirmationNaoConfigurado =
        "Integração SAP de Confirmação de Produção não configurada.";

    public const string MensagemConfiguracaoAusente =
        "Integracao SAP nao configurada. Defina URL, credenciais e FUGAPET_SAP_ALLOWED_HOSTS.";

    public const string MensagemEscritaBloqueada =
        "Escrita no SAP desativada. Defina FUGAPET_SAP_WRITE_ENABLED=true somente no ambiente autorizado.";

    public const string MensagemMaterialDocumentNaoConfigurado =
        "Integração SAP Material Document não configurada.";

    /// <summary>
    /// Mensagem operacional para arquivo de configuracao SAP existente porem malformado (erro de
    /// implantacao). Nunca expoe caminho do arquivo nem o conteudo.
    /// </summary>
    public const string MensagemConfiguracaoInvalida =
        "Configuração SAP inválida. Acione o suporte técnico.";
}
