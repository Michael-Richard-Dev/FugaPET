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
