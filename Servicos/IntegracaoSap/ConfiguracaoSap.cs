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

    /// <summary>URL base explicita do Product Master (API_PRODUCT_SRV).</summary>
    public string ProductBaseUrl { get; init; } = string.Empty;

    public bool ArquivoConfiguracaoSapEncontrado { get; init; } = true;

    public string Usuario { get; init; } = string.Empty;

    public string Senha { get; init; } = string.Empty;

    /// <summary>Mandante SAP (parametro de query sap-client). Opcional.</summary>
    public string SapClient { get; init; } = string.Empty;

    /// <summary>Hosts SAP aceitos, sem protocolo, porta ou caminho.</summary>
    public IReadOnlyList<string> HostsPermitidos { get; init; } = [];

    /// <summary>
    /// Chave de ativacao controlada da escrita SAP GENERICA. Permanece false por padrao.
    /// A operacao ainda exige permissao SAP especifica e CSRF. Se o recurso fornecer
    /// ETag, o valor real e obrigatoriamente enviado; nunca e usado If-Match "*".
    /// </summary>
    public bool EscritaHabilitada { get; init; }

    /// <summary>
    /// Autorizacao ESPECIFICA e ISOLADA do POST de Handling Unit de caixa (modo HML repetitivo controlado).
    /// Independente de <see cref="EscritaHabilitada"/> (que permanece false). Quando false, o POST
    /// /HandlingUnit e bloqueado ANTES da transmissao. Quando true, autoriza SOMENTE o POST /HandlingUnit
    /// via gateway de HU — nunca outros POSTs, PATCH ou DELETE. Configurada por ambiente; padrao false.
    /// </summary>
    public bool HuWriteHabilitado { get; init; }

    /// <summary>
    /// URL base do servico OData de Handling Unit (API_HANDLINGUNIT). Quando vazia, o gateway de HU
    /// permanece fail-closed mesmo com <see cref="HuWriteHabilitado"/>=true (nao ha destino).
    /// </summary>
    public string HandlingUnitBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Autorizacao ESPECIFICA e ISOLADA do POST do MaterialDocument (261/101) do PRODUTO ACABADO. Independente
    /// de <see cref="EscritaHabilitada"/> (que permanece false) e da de HU. Padrao false ⇒ camada PA bloqueia
    /// ANTES de qualquer HTTP. Nunca libera PATCH/DELETE nem outros endpoints. Chave FUGAPET_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED.
    /// </summary>
    public bool ProdutoAcabadoMaterialDocumentWriteHabilitado { get; init; }

    /// <summary>
    /// Autorizacao ESPECIFICA e ISOLADA do POST do palete (INT012/CPI). Independente das demais. Padrao false
    /// ⇒ gateway INT012 fail-closed (zero HTTP). Nunca reutiliza FUGAPET_SAP_WRITE_ENABLED. Chave FUGAPET_SAP_PALLET_WRITE_ENABLED.
    /// </summary>
    public bool PalletWriteHabilitado { get; init; }

    /// <summary>
    /// REV4-§4: gate ESPECIFICO do NOVO pipeline de Produto Acabado (261 → 101 → HU). Padrao false. Independente
    /// de <see cref="EscritaHabilitada"/>, <see cref="HuWriteHabilitado"/> e dos gates de escrita. Quando false,
    /// o runtime mantem EXATAMENTE o caminho HU individual homologado. Quando true, o botao ENVIAR CAIXA SAP passa a
    /// executar SOMENTE 261 → 101 → HU (HU isolada proibida). Chave FUGAPET_SAP_PA_PIPELINE_ENABLED.
    /// </summary>
    public bool ProdutoAcabadoPipelineHabilitado { get; init; }

    /// <summary>
    /// REV4-§14: URL base ESPECIFICA do endpoint CPI do palete (INT012). NUNCA derivada de
    /// <see cref="HandlingUnitBaseUrl"/> (S/4 Handling Unit e CPI sao integracoes distintas). Vazia ⇒ gateway
    /// INT012 fail-closed (zero HTTP). Chave FUGAPET_SAP_PALLET_INT012_BASE_URL.
    /// </summary>
    public string PalletInt012BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// REV4-§14: allowlist PROPRIA de hosts do CPI/INT012 (independente de <see cref="HostsPermitidos"/> do S/4).
    /// Vazia ⇒ o gateway INT012 usa a allowlist geral apenas se explicitamente configurada; sem host valido ⇒
    /// fail-closed. Chave FUGAPET_SAP_PALLET_INT012_ALLOWED_HOSTS.
    /// </summary>
    public IReadOnlyList<string> PalletInt012HostsPermitidos { get; init; } = [];

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

    /// <summary>
    /// Tarefa Consumo 22.10.1: URL base efetiva do servico de Product Master (API_PRODUCT_SRV), usada para
    /// o GET da descricao do componente em A_ProductDescription. Reaproveita o padrao SAP ja configurado
    /// derivando de MaterialDocumentBaseUrl (e, em ultimo caso, de BaseUrl) — troca o segmento API_*_SRV
    /// por API_PRODUCT_SRV, mantendo host/caminho. Sem URL explicita propria (nao ha campo dedicado).
    /// </summary>
    public string ProductMasterBaseUrlEfetiva
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ProductBaseUrl))
            {
                return ProductBaseUrl;
            }

            string derivadoDoMaterialDocument = DerivarUrlServicoSap(MaterialDocumentBaseUrl, "API_PRODUCT_SRV");
            return !string.IsNullOrWhiteSpace(derivadoDoMaterialDocument)
                ? derivadoDoMaterialDocument
                : DerivarUrlServicoSap(BaseUrl, "API_PRODUCT_SRV");
        }
    }

    /// <summary>
    /// True quando, alem das credenciais + allowlist, ha URL (derivada) do servico de Product Master
    /// (API_PRODUCT_SRV) para o GET da descricao do componente na Tela de Consumo.
    /// </summary>
    public bool ProductMasterConfigurado =>
        !string.IsNullOrWhiteSpace(ProductMasterBaseUrlEfetiva)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    /// <summary>
    /// GATE 048-E REV2: URL base efetiva do servico de Versao de Producao (API_PRODUCTION_VERSION), usada
    /// para resolver o roteiro AUTORITATIVO a partir da ProductionVersion da OP. Deriva do padrao SAP ja
    /// configurado (MaterialDocument, e em ultimo caso BaseUrl); sem campo dedicado. Vazia ⇒ fail-closed.
    /// </summary>
    public string ProductionVersionBaseUrlEfetiva
    {
        get
        {
            string derivadoDoMaterialDocument = DerivarUrlServicoSap(MaterialDocumentBaseUrl, "API_PRODUCTION_VERSION");
            return !string.IsNullOrWhiteSpace(derivadoDoMaterialDocument)
                ? derivadoDoMaterialDocument
                : DerivarUrlServicoSap(BaseUrl, "API_PRODUCTION_VERSION");
        }
    }

    /// <summary>
    /// GATE 048-E REV2: URL base efetiva do servico de Roteiro (API_PRODUCTION_ROUTING), usada para ler
    /// ProductionRoutingOperation.OperationStandardTextCode. Deriva do padrao SAP ja configurado. Vazia ⇒ fail-closed.
    /// </summary>
    public string ProductionRoutingBaseUrlEfetiva
    {
        get
        {
            string derivadoDoMaterialDocument = DerivarUrlServicoSap(MaterialDocumentBaseUrl, "API_PRODUCTION_ROUTING");
            return !string.IsNullOrWhiteSpace(derivadoDoMaterialDocument)
                ? derivadoDoMaterialDocument
                : DerivarUrlServicoSap(BaseUrl, "API_PRODUCTION_ROUTING");
        }
    }

    /// <summary>
    /// True quando ha URL (derivada) das DUAS APIs do roteiro (versao + roteiro) + credenciais + allowlist.
    /// Sem isso a resolucao do marcador PP_FORM fica indisponivel (fail-closed no Controle de Apontamentos).
    /// </summary>
    public bool ProductionRoutingConfigurado =>
        !string.IsNullOrWhiteSpace(ProductionVersionBaseUrlEfetiva)
        && !string.IsNullOrWhiteSpace(ProductionRoutingBaseUrlEfetiva)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    /// <summary>
    /// Endpoint consultável (somente leitura) do serviço de norma/estrutura de embalagem do Produto Acabado
    /// (Integration Suite, contrato INT012 — ex.: .../ZAPI_PACKAGING_SRV/GetPackagingSet). Independente das
    /// demais APIs; quando vazio, a consulta da norma fica indisponível (sem fallback fictício).
    /// </summary>
    public string PackagingBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Usuário PRÓPRIO da API de embalagem (Integration Suite). NUNCA reaproveita <see cref="Usuario"/>.
    /// Aceito exclusivamente por variável de ambiente; nunca persistido em JSON.
    /// </summary>
    public string PackagingUsuario { get; init; } = string.Empty;

    /// <summary>
    /// Senha PRÓPRIA da API de embalagem (Integration Suite). NUNCA reaproveita <see cref="Senha"/>.
    /// Aceita exclusivamente por variável de ambiente; nunca persistida em JSON; nunca logada.
    /// </summary>
    public string PackagingSenha { get; init; } = string.Empty;

    /// <summary>Hosts aceitos do Integration Suite da embalagem (allowlist própria, independente da SAP standard).</summary>
    public IReadOnlyList<string> PackagingHostsPermitidos { get; init; } = [];

    /// <summary>
    /// Mandante (sap-client) da API de embalagem. Opcional: só é acrescentado à query quando preenchido
    /// e o contrato do endpoint exigir. Por padrão o Integration Suite NÃO recebe sap-client.
    /// </summary>
    public string PackagingSapClientOpcional { get; init; } = string.Empty;

    /// <summary>
    /// True quando a API de embalagem está configurada. Valida EXCLUSIVAMENTE os parâmetros próprios da
    /// embalagem (URL + usuário + senha + allowlist), sem depender das credenciais das APIs SAP standard.
    /// </summary>
    public bool PackagingConfigurado =>
        !string.IsNullOrWhiteSpace(PackagingBaseUrl)
        && !string.IsNullOrWhiteSpace(PackagingUsuario)
        && !string.IsNullOrWhiteSpace(PackagingSenha)
        && PackagingHostsPermitidos.Count > 0;

    /// <summary>True quando a URL da embalagem existe mas falta usuário/senha própria (diagnóstico específico).</summary>
    public bool PackagingCredencialAusente =>
        !string.IsNullOrWhiteSpace(PackagingBaseUrl)
        && PackagingHostsPermitidos.Count > 0
        && (string.IsNullOrWhiteSpace(PackagingUsuario) || string.IsNullOrWhiteSpace(PackagingSenha));

    public string MensagemConfiguracaoBaseAusente()
    {
        if (!ArquivoConfiguracaoSapEncontrado)
        {
            return MensagemArquivoConfiguracaoSapNaoEncontrado;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            return "base_url n?o configurada no configuracao.sap.json.";
        }

        if (string.IsNullOrWhiteSpace(SapClient))
        {
            return "sap_client n?o configurado no configuracao.sap.json.";
        }

        if (HostsPermitidos.Count == 0)
        {
            return "hosts_permitidos n?o configurado no configuracao.sap.json.";
        }

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Senha))
        {
            return "usuario/senha SAP n?o configurados. Defina no configuracao.sap.json ou nas vari?veis FUGAPET_SAP_USERNAME/FUGAPET_SAP_PASSWORD.";
        }

        if (TimeoutSegundos <= 0)
        {
            return "timeout_segundos n?o configurado corretamente no configuracao.sap.json.";
        }

        return MensagemConfiguracaoAusente;
    }

    public string MensagemMaterialDocumentAusente()
        => string.IsNullOrWhiteSpace(MaterialDocumentBaseUrl)
            ? "material_document_base_url n?o configurada no configuracao.sap.json."
            : MensagemConfiguracaoBaseAusente();

    public string MensagemProductionOrderAusente()
        => string.IsNullOrWhiteSpace(ProductionOrderBaseUrl)
            ? "production_order_base_url n?o configurada no configuracao.sap.json."
            : MensagemConfiguracaoBaseAusente();

    public string MensagemProductMasterAusente()
        => string.IsNullOrWhiteSpace(ProductBaseUrl)
            ? "product_base_url n?o configurada no configuracao.sap.json."
            : MensagemConfiguracaoBaseAusente();

    public static string MensagemArquivoConfiguracaoSapNaoEncontrado =>
        "Arquivo configuracao.sap.json n?o encontrado na pasta da aplica??o."
        + Environment.NewLine
        + Environment.NewLine
        + "Copie o arquivo configuracao.sap.exemplo.json, renomeie para configuracao.sap.json e configure as URLs/credenciais SAP HML.";

    public const string MensagemProductionOrderNaoConfigurado =
        "Integração SAP de Ordem de Produção não configurada.";

    public const string MensagemProductionOrderConfirmationNaoConfigurado =
        "Integração SAP de Confirmação de Produção não configurada.";

    public const string MensagemConfiguracaoAusente =
        "Integracao SAP nao configurada. Defina URL, credenciais e FUGAPET_SAP_ALLOWED_HOSTS.";

    public const string MensagemEscritaBloqueada =
        "Escrita no SAP desativada. Defina FUGAPET_SAP_WRITE_ENABLED=true somente no ambiente autorizado.";

    public const string MensagemHuWriteBloqueada =
        "POST de Handling Unit desativado. Defina FUGAPET_SAP_HU_WRITE_ENABLED=true somente no ambiente autorizado.";

    /// <summary>
    /// True quando o POST de HU pode ser transmitido: flag HU habilitada + URL de HU + credenciais + allowlist.
    /// Nunca depende de <see cref="EscritaHabilitada"/> (autorizacao GENERICA e mantida isolada da de HU).
    /// </summary>
    public bool HandlingUnitConfigurado =>
        HuWriteHabilitado
        && !string.IsNullOrWhiteSpace(HandlingUnitBaseUrl)
        && !string.IsNullOrWhiteSpace(Usuario)
        && !string.IsNullOrWhiteSpace(Senha)
        && HostsPermitidos.Count > 0;

    public const string MensagemMaterialDocumentNaoConfigurado =
        "Integração SAP Material Document não configurada.";

    public const string MensagemProductMasterNaoConfigurado =
        "Integração SAP de Product Master (descrição) não configurada.";

    /// <summary>
    /// Mensagem operacional para arquivo de configuracao SAP existente porem malformado (erro de
    /// implantacao). Nunca expoe caminho do arquivo nem o conteudo.
    /// </summary>
    public const string MensagemConfiguracaoInvalida =
        "Configuração SAP inválida. Acione o suporte técnico.";
}
