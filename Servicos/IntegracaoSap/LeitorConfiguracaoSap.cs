using System.Text.Json;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Le parametros nao secretos de configuracao.sap.json (secao "sap").
/// Usuario e senha sao aceitos exclusivamente pelas variaveis de ambiente FUGAPET_SAP_USERNAME
/// e FUGAPET_SAP_PASSWORD.
/// </summary>
public static class LeitorConfiguracaoSap
{
    private const string NomeArquivoConfiguracao = "configuracao.sap.json";
    private const string VariavelAmbienteBaseUrl = "FUGAPET_SAP_BASE_URL";
    private const string VariavelAmbienteMaterialDocumentBaseUrl = "FUGAPET_SAP_MATERIAL_DOCUMENT_BASE_URL";
    private const string VariavelAmbienteProductionOrderBaseUrl = "FUGAPET_SAP_PRODUCTION_ORDER_BASE_URL";
    private const string VariavelAmbienteProductionOrderConfirmationBaseUrl = "FUGAPET_SAP_PRODUCTION_ORDER_CONFIRMATION_BASE_URL";
    private const string VariavelAmbienteProductBaseUrl = "FUGAPET_SAP_PRODUCT_BASE_URL";
    private const string VariavelAmbientePackagingBaseUrl = "FUGAPET_SAP_PACKAGING_BASE_URL";
    private const string VariavelAmbientePackagingUsuario = "FUGAPET_SAP_PACKAGING_USERNAME";
    private const string VariavelAmbientePackagingSenha = "FUGAPET_SAP_PACKAGING_PASSWORD";
    private const string VariavelAmbientePackagingHostsPermitidos = "FUGAPET_SAP_PACKAGING_ALLOWED_HOSTS";
    private const string VariavelAmbientePackagingSapClient = "FUGAPET_SAP_PACKAGING_CLIENT";
    private const string VariavelAmbienteUsuario = "FUGAPET_SAP_USERNAME";
    private const string VariavelAmbienteSenha = "FUGAPET_SAP_PASSWORD";
    private const string VariavelAmbienteSapClient = "FUGAPET_SAP_CLIENT";
    private const string VariavelAmbienteHostsPermitidos = "FUGAPET_SAP_ALLOWED_HOSTS";
    private const string VariavelAmbienteEscritaHabilitada = "FUGAPET_SAP_WRITE_ENABLED";
    private const string VariavelAmbienteHuWriteHabilitado = "FUGAPET_SAP_HU_WRITE_ENABLED";
    private const string VariavelAmbientePaMaterialDocumentWriteHabilitado = "FUGAPET_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED";
    private const string VariavelAmbientePalletWriteHabilitado = "FUGAPET_SAP_PALLET_WRITE_ENABLED";
    private const string VariavelAmbienteHandlingUnitBaseUrl = "FUGAPET_SAP_HANDLING_UNIT_BASE_URL";
    private const string VariavelAmbientePaPipelineHabilitado = "FUGAPET_SAP_PA_PIPELINE_ENABLED";
    private const string VariavelAmbientePalletInt012BaseUrl = "FUGAPET_SAP_PALLET_INT012_BASE_URL";
    private const string VariavelAmbientePalletInt012HostsPermitidos = "FUGAPET_SAP_PALLET_INT012_ALLOWED_HOSTS";

    public static ConfiguracaoSap Carregar()
        => Carregar(
            Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao),
            ObterVariavelAmbienteSistema);

    internal static string? ObterVariavelAmbienteSistema(string nome)
        => ObterVariavelAmbiente(
            nome,
            variavel => Environment.GetEnvironmentVariable(variavel, EnvironmentVariableTarget.Process),
            variavel => Environment.GetEnvironmentVariable(variavel, EnvironmentVariableTarget.User));

    internal static string? ObterVariavelAmbiente(
        string nome,
        Func<string, string?> obterProcesso,
        Func<string, string?> obterUsuario)
    {
        string? processo = obterProcesso(nome);
        return !string.IsNullOrWhiteSpace(processo)
            ? processo
            : obterUsuario(nome);
    }

    internal static ConfiguracaoSap Carregar(
        string caminhoArquivo,
        Func<string, string?> obterVariavelAmbiente)
    {
        string baseUrlArquivo = string.Empty;
        string materialDocumentBaseUrlArquivo = string.Empty;
        string productionOrderBaseUrlArquivo = string.Empty;
        string productionOrderConfirmationBaseUrlArquivo = string.Empty;
        string productBaseUrlArquivo = string.Empty;
        string packagingBaseUrlArquivo = string.Empty;
        string packagingSapClientArquivo = string.Empty;
        IReadOnlyList<string> packagingHostsPermitidosArquivo = [];
        string usuarioArquivo = string.Empty;
        string senhaArquivo = string.Empty;
        string sapClientArquivo = string.Empty;
        IReadOnlyList<string> hostsPermitidosArquivo = [];
        bool escritaHabilitada = false;
        bool huWriteHabilitadoArquivo = false;
        string handlingUnitBaseUrlArquivo = string.Empty;
        bool paPipelineHabilitadoArquivo = false;
        string palletInt012BaseUrlArquivo = string.Empty;
        IReadOnlyList<string> palletInt012HostsPermitidosArquivo = [];
        int timeout = 30;

        // Arquivo ausente: e valido carregar a configuracao apenas por variaveis de ambiente.
        // Arquivo presente porem malformado e ERRO DE IMPLANTACAO: erro controlado, nunca tratado
        // silenciosamente como "nao configurado" (e nunca expondo caminho ou conteudo do arquivo).
        if (File.Exists(caminhoArquivo))
        {
            JsonDocument documento;
            try
            {
                documento = JsonDocument.Parse(File.ReadAllText(caminhoArquivo));
            }
            catch (JsonException ex)
            {
                throw new ConfiguracaoSapInvalidaException(ex);
            }

            using (documento)
            {
                if (documento.RootElement.TryGetProperty("sap", out JsonElement sap))
                {
                    baseUrlArquivo = LerTexto(sap, "base_url", string.Empty);
                    materialDocumentBaseUrlArquivo = LerTexto(sap, "material_document_base_url", string.Empty);
                    productionOrderBaseUrlArquivo = LerTexto(sap, "production_order_base_url", string.Empty);
                    productionOrderConfirmationBaseUrlArquivo = LerTexto(sap, "production_order_confirmation_base_url", string.Empty);
                    productBaseUrlArquivo = LerTexto(sap, "product_base_url", string.Empty);
                    packagingBaseUrlArquivo = LerTexto(sap, "packaging_base_url", string.Empty);
                    packagingSapClientArquivo = LerTexto(sap, "packaging_sap_client", string.Empty);
                    packagingHostsPermitidosArquivo = LerListaTextos(sap, "packaging_hosts_permitidos");
                    usuarioArquivo = LerTexto(sap, "usuario", string.Empty);
                    senhaArquivo = LerTexto(sap, "senha", string.Empty);
                    sapClientArquivo = LerTexto(sap, "sap_client", string.Empty);
                    hostsPermitidosArquivo = LerListaTextos(sap, "hosts_permitidos");
                    escritaHabilitada = LerBooleano(sap, "escrita_habilitada", false);
                    huWriteHabilitadoArquivo = LerBooleano(sap, "hu_write_habilitado", false);
                    handlingUnitBaseUrlArquivo = LerTexto(sap, "handling_unit_base_url", string.Empty);
                    paPipelineHabilitadoArquivo = LerBooleano(sap, "pa_pipeline_habilitado", false);
                    palletInt012BaseUrlArquivo = LerTexto(sap, "pallet_int012_base_url", string.Empty);
                    palletInt012HostsPermitidosArquivo = LerListaTextos(sap, "pallet_int012_hosts_permitidos");
                    timeout = LerInteiro(sap, "timeout_segundos", 30);
                }
            }
        }

        return new ConfiguracaoSap
        {
            BaseUrl = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteBaseUrl, baseUrlArquivo),
            MaterialDocumentBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbienteMaterialDocumentBaseUrl,
                materialDocumentBaseUrlArquivo),
            ProductionOrderBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbienteProductionOrderBaseUrl,
                productionOrderBaseUrlArquivo),
            ProductionOrderConfirmationBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbienteProductionOrderConfirmationBaseUrl,
                productionOrderConfirmationBaseUrlArquivo),
            ProductBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbienteProductBaseUrl,
                productBaseUrlArquivo),
            PackagingBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbientePackagingBaseUrl,
                packagingBaseUrlArquivo),
            // Credenciais da embalagem: EXCLUSIVAMENTE por variável de ambiente (nunca de arquivo/JSON).
            PackagingUsuario = ObterSomenteAmbiente(obterVariavelAmbiente, VariavelAmbientePackagingUsuario),
            PackagingSenha = ObterSomenteAmbiente(obterVariavelAmbiente, VariavelAmbientePackagingSenha),
            PackagingHostsPermitidos = ObterHostsPermitidos(
                obterVariavelAmbiente,
                VariavelAmbientePackagingHostsPermitidos,
                packagingHostsPermitidosArquivo),
            PackagingSapClientOpcional = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbientePackagingSapClient,
                packagingSapClientArquivo),
            ArquivoConfiguracaoSapEncontrado = File.Exists(caminhoArquivo),
            Usuario = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteUsuario, usuarioArquivo),
            Senha = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteSenha, senhaArquivo),
            SapClient = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteSapClient, sapClientArquivo),
            HostsPermitidos = ObterHostsPermitidos(
                obterVariavelAmbiente,
                VariavelAmbienteHostsPermitidos,
                hostsPermitidosArquivo),
            EscritaHabilitada = ObterBooleanoOuArquivo(
                obterVariavelAmbiente,
                VariavelAmbienteEscritaHabilitada,
                escritaHabilitada),
            // Autorizacao ESPECIFICA e ISOLADA do POST de HU (padrao false; independente de WRITE_ENABLED).
            HuWriteHabilitado = ObterBooleanoOuArquivo(
                obterVariavelAmbiente,
                VariavelAmbienteHuWriteHabilitado,
                huWriteHabilitadoArquivo),
            HandlingUnitBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbienteHandlingUnitBaseUrl,
                handlingUnitBaseUrlArquivo),
            // Gates ESPECIFICOS e ISOLADOS do Produto Acabado (padrao false; independentes de WRITE_ENABLED e de HU).
            ProdutoAcabadoMaterialDocumentWriteHabilitado = ObterBooleanoOuArquivo(
                obterVariavelAmbiente,
                VariavelAmbientePaMaterialDocumentWriteHabilitado,
                false),
            PalletWriteHabilitado = ObterBooleanoOuArquivo(
                obterVariavelAmbiente,
                VariavelAmbientePalletWriteHabilitado,
                false),
            // REV4-§4: gate do NOVO pipeline PA (261→101→HU). Padrao false; independente dos demais gates.
            ProdutoAcabadoPipelineHabilitado = ObterBooleanoOuArquivo(
                obterVariavelAmbiente,
                VariavelAmbientePaPipelineHabilitado,
                paPipelineHabilitadoArquivo),
            // REV4-§14: URL/allowlist ESPECIFICAS do CPI/INT012 (nunca derivadas de HandlingUnitBaseUrl).
            PalletInt012BaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbientePalletInt012BaseUrl,
                palletInt012BaseUrlArquivo),
            PalletInt012HostsPermitidos = ObterHostsPermitidos(
                obterVariavelAmbiente,
                VariavelAmbientePalletInt012HostsPermitidos,
                palletInt012HostsPermitidosArquivo),
            TimeoutSegundos = timeout
        };
    }

    private static bool ObterBooleanoOuArquivo(
        Func<string, string?> obterVariavelAmbiente,
        string variavel,
        bool valorArquivo)
    {
        string? ambiente = obterVariavelAmbiente(variavel);
        return bool.TryParse(ambiente, out bool valor) ? valor : valorArquivo;
    }

    private static string ObterOuAmbiente(
        Func<string, string?> obterVariavelAmbiente,
        string variavel,
        string valorArquivo)
    {
        string? ambiente = obterVariavelAmbiente(variavel);
        return !string.IsNullOrWhiteSpace(ambiente) ? ambiente : valorArquivo;
    }

    private static string ObterSomenteAmbiente(
        Func<string, string?> obterVariavelAmbiente,
        string variavel)
        => obterVariavelAmbiente(variavel)?.Trim() ?? string.Empty;

    private static IReadOnlyList<string> ObterHostsPermitidos(
        Func<string, string?> obterVariavelAmbiente,
        string variavel,
        IReadOnlyList<string> valoresArquivo)
    {
        string? ambiente = obterVariavelAmbiente(variavel);
        IEnumerable<string> valores = string.IsNullOrWhiteSpace(ambiente)
            ? valoresArquivo
            : ambiente.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return valores
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Select(valor => valor.Trim().TrimEnd('.').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string LerTexto(JsonElement elemento, string propriedade, string valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? valorPadrao
            : valorPadrao;

    private static int LerInteiro(JsonElement elemento, string propriedade, int valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.TryGetInt32(out int inteiro)
            ? inteiro
            : valorPadrao;

    private static bool LerBooleano(JsonElement elemento, string propriedade, bool valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor)
           && valor.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? valor.GetBoolean()
            : valorPadrao;

    private static IReadOnlyList<string> LerListaTextos(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor)
            || valor.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return valor.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }
}
