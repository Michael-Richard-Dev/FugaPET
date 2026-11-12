using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Ambiente;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Le parametros nao secretos de configuracao.sap.json (secao "sap").
/// Usuario e senha sao aceitos exclusivamente pelas variaveis de ambiente FUGAPET_Q_SAP_USERNAME
/// e FUGAPET_Q_SAP_PASSWORD.
/// </summary>
public static class LeitorConfiguracaoSap
{
    private const string NomeArquivoConfiguracao = "configuracao.sap.json";
    private const string VariavelAmbienteBaseUrl = "FUGAPET_Q_SAP_BASE_URL";
    private const string VariavelAmbientePurchaseOrderBaseUrl = "FUGAPET_Q_SAP_PURCHASE_ORDER_BASE_URL";
    private const string VariavelAmbienteMaterialDocumentBaseUrl = "FUGAPET_Q_SAP_MATERIAL_DOCUMENT_BASE_URL";
    private const string VariavelAmbienteProductionOrderBaseUrl = "FUGAPET_Q_SAP_PRODUCTION_ORDER_BASE_URL";
    private const string VariavelAmbienteProductionOrderConfirmationBaseUrl = "FUGAPET_Q_SAP_PRODUCTION_ORDER_CONFIRMATION_BASE_URL";
    private const string VariavelAmbienteProductBaseUrl = "FUGAPET_Q_SAP_PRODUCT_BASE_URL";
    private const string VariavelAmbientePackagingBaseUrl = "FUGAPET_SAP_PACKAGING_BASE_URL";
    private const string VariavelAmbientePackagingUsuario = "FUGAPET_SAP_PACKAGING_USERNAME";
    private const string VariavelAmbientePackagingSenha = "FUGAPET_SAP_PACKAGING_PASSWORD";
    private const string VariavelAmbientePackagingHostsPermitidos = "FUGAPET_SAP_PACKAGING_ALLOWED_HOSTS";
    private const string VariavelAmbientePackagingSapClient = "FUGAPET_SAP_PACKAGING_CLIENT";
    private const string VariavelAmbienteUsuario = "FUGAPET_Q_SAP_USERNAME";
    private const string VariavelAmbienteSenha = "FUGAPET_Q_SAP_PASSWORD";
    private const string VariavelAmbienteSapClient = "FUGAPET_Q_SAP_CLIENT";
    private const string VariavelAmbienteHostsPermitidos = "FUGAPET_Q_SAP_ALLOWED_HOSTS";
    private const string VariavelAmbienteEscritaHabilitada = "FUGAPET_Q_SAP_WRITE_ENABLED";
    private const string VariavelAmbienteHuWriteHabilitado = "FUGAPET_Q_SAP_HU_WRITE_ENABLED";
    private const string VariavelAmbienteHandlingUnitBaseUrl = "FUGAPET_Q_SAP_HANDLING_UNIT_BASE_URL";
    private const string VariavelAmbientePaMaterialDocumentWriteHabilitado = "FUGAPET_Q_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED";
    private const string VariavelAmbientePalletWriteHabilitado = "FUGAPET_Q_SAP_PALLET_WRITE_ENABLED";
    private const string VariavelAmbientePaPipelineHabilitado = "FUGAPET_Q_SAP_PA_PIPELINE_ENABLED";
    private const string VariavelAmbientePalletInt012BaseUrl = "FUGAPET_SAP_PALLET_INT012_BASE_URL";
    private const string VariavelAmbientePalletInt012HostsPermitidos = "FUGAPET_SAP_PALLET_INT012_ALLOWED_HOSTS";
    private const string VariavelAmbientePalletInt012Usuario = "FUGAPET_SAP_PALLET_INT012_USERNAME";
    private const string VariavelAmbientePalletInt012Senha = "FUGAPET_SAP_PALLET_INT012_PASSWORD";
    // GATE Q PACKAGING-05: habilitação da capability de norma de embalagem — Q-namespaced, alvo Process.
    internal const string VariavelAmbientePackagingHabilitado = "FUGAPET_Q_SAP_PACKAGING_ENABLED";

    /// <summary>Caminho canonico da configuracao local (ao lado do EXE).</summary>
    public static string CaminhoConfiguracaoLocal
        => Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao);

    public static ConfiguracaoSap Carregar()
        => Carregar(
            CaminhoConfiguracaoLocal,
            ObterVariavelAmbienteSistema,
            LerVariavelAmbientePorAlvoSistema,
            // GATE 120G: perfil de ambiente lido da MESMA configuracao local, para que o duplo clique
            // no EXE resolva os defaults Q sem script externo.
            PerfilAmbienteLocal.CarregarDoArquivo(CaminhoConfiguracaoLocal));

    // GATE Q PACKAGING-05: leitura POR ALVO (Process/User/Machine) sem merge, para o gate da capability.
    internal static string? LerVariavelAmbientePorAlvoSistema(string nome, EnvironmentVariableTarget alvo)
        => Environment.GetEnvironmentVariable(nome, alvo);

    /// <summary>
    /// GATE Q PACKAGING-05: a capability Packaging só habilita quando o valor Process é "true" E os valores
    /// User e Machine estão ausentes/blank. Qualquer outra combinação (User/Machine presentes, Process
    /// false/inválido/ausente) ⇒ false (fail-closed). Config técnica legada de Packaging NUNCA participa.
    /// </summary>
    internal static bool ResolverPackagingHabilitado(Func<string, EnvironmentVariableTarget, string?> lerPorAlvo)
        => ResolverPackagingHabilitado(lerPorAlvo, padraoPerfil: false);

    /// <summary>
    /// GATE 120G: mesma regra de seguranca do PACKAGING-05 (User/Machine presentes ⇒ fail-closed),
    /// agora com a precedencia do 120G: Process explicito vence; na ausencia dele vale o default do
    /// perfil Q local. Config tecnica legada de Packaging continua NAO participando.
    /// </summary>
    internal static bool ResolverPackagingHabilitado(
        Func<string, EnvironmentVariableTarget, string?> lerPorAlvo,
        bool padraoPerfil)
    {
        ArgumentNullException.ThrowIfNull(lerPorAlvo);

        string? usuario = lerPorAlvo(VariavelAmbientePackagingHabilitado, EnvironmentVariableTarget.User)?.Trim();
        string? maquina = lerPorAlvo(VariavelAmbientePackagingHabilitado, EnvironmentVariableTarget.Machine)?.Trim();

        // Persistencia em User/Machine permanece PROIBIDA: presenca em qualquer um ⇒ false.
        if (!string.IsNullOrWhiteSpace(usuario) || !string.IsNullOrWhiteSpace(maquina))
        {
            return false;
        }

        string? processo = lerPorAlvo(VariavelAmbientePackagingHabilitado, EnvironmentVariableTarget.Process)?.Trim();
        return bool.TryParse(processo, out bool explicitoProcesso) ? explicitoProcesso : padraoPerfil;
    }

    /// <summary>
    /// GATE 120G: precedencia UNICA dos gates operacionais booleanos.
    /// <list type="number">
    /// <item>override EXPLICITO de Process environment (true/false parseavel) — mecanismo dos gates tecnicos;</item>
    /// <item>configuracao local: chave propria do arquivo OU default do perfil Q;</item>
    /// <item>false (fail-closed).</item>
    /// </list>
    /// <para>
    /// O override e lido SOMENTE no alvo Process. O alvo User deixou de participar dos gates de
    /// escrita: persistir capability em User/Machine e proibido, e aceitar User aqui permitiria
    /// habilitar escrita por environment persistente — exatamente o que o contrato veda.
    /// </para>
    /// <para>
    /// Na etapa 2, arquivo e perfil se SOMAM: a chave do arquivo pode ADICIONAR, o perfil Q pode
    /// ADICIONAR, e nenhum dos dois faz veto. Para desligar um gate em Q usa-se o override de
    /// Process com "false" — que vence por ser a etapa 1.
    /// </para>
    /// </summary>
    internal static bool ResolverGateOperacional(
        Func<string, EnvironmentVariableTarget, string?>? lerPorAlvo,
        string variavel,
        bool valorArquivo,
        bool padraoPerfil)
    {
        string? processo = lerPorAlvo?.Invoke(variavel, EnvironmentVariableTarget.Process)?.Trim();
        if (bool.TryParse(processo, out bool explicitoProcesso))
        {
            return explicitoProcesso;
        }

        return valorArquivo || padraoPerfil;
    }

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
        Func<string, string?> obterVariavelAmbiente,
        Func<string, EnvironmentVariableTarget, string?>? lerVariavelPorAlvo = null,
        PerfilAmbienteLocal? perfilAmbiente = null)
    {
        // GATE 120G: perfil ausente/nao reconhecido ⇒ nenhum default concedido (fail-closed).
        bool perfilQ = perfilAmbiente?.EhQ == true;
        string baseUrlArquivo = string.Empty;
        string purchaseOrderBaseUrlArquivo = string.Empty;
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
        // GATE 120G: estes dois gates ignoravam a chave do arquivo (o leitor passava o literal false),
        // o que deixava pa_material_document_write_enabled MORTO na configuracao implantada.
        bool paMaterialDocumentWriteHabilitadoArquivo = false;
        bool palletWriteHabilitadoArquivo = false;
        string palletInt012BaseUrlArquivo = string.Empty;
        IReadOnlyList<string> palletInt012HostsPermitidosArquivo = [];
        // GATE 126V: mapeamento da tara fisica da embalagem do palete. Lista VAZIA por padrao ⇒ fail-closed.
        IReadOnlyList<TaraFisicaEmbalagemConfigurada> tarasFisicasEmbalagemPaleteArquivo = [];
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
                    purchaseOrderBaseUrlArquivo = LerTexto(sap, "purchase_order_base_url", string.Empty);
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
                    // Nome EXATO ja presente na configuracao implantada do Q (nao inventado aqui).
                    paMaterialDocumentWriteHabilitadoArquivo =
                        LerBooleano(sap, "pa_material_document_write_enabled", false);
                    palletWriteHabilitadoArquivo = LerBooleano(sap, "pallet_write_habilitado", false);
                    palletInt012BaseUrlArquivo = LerTexto(sap, "pallet_int012_base_url", string.Empty);
                    palletInt012HostsPermitidosArquivo = LerListaTextos(sap, "pallet_int012_hosts_permitidos");
                    // GATE 126V: tara fisica da embalagem do palete por mandante + centro + material.
                    tarasFisicasEmbalagemPaleteArquivo =
                        LerTarasFisicasEmbalagemPalete(sap, "taras_fisicas_embalagem_palete");
                    timeout = LerInteiro(sap, "timeout_segundos", 30);
                }
            }
        }

        return new ConfiguracaoSap
        {
            BaseUrl = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteBaseUrl, baseUrlArquivo),
            PurchaseOrderBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbientePurchaseOrderBaseUrl,
                purchaseOrderBaseUrlArquivo),
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
            // GATE 120G: escrita SAP GENERICA. O perfil NAO participa (padraoPerfil: false sempre) —
            // nenhum perfil pode liga-la. So muda por chave de arquivo ou override explicito de Process.
            EscritaHabilitada = ResolverGateOperacional(
                lerVariavelPorAlvo,
                VariavelAmbienteEscritaHabilitada,
                escritaHabilitada,
                padraoPerfil: false),
            // Autorizacao ESPECIFICA e ISOLADA do POST de HU (independente de WRITE_ENABLED).
            HuWriteHabilitado = ResolverGateOperacional(
                lerVariavelPorAlvo,
                VariavelAmbienteHuWriteHabilitado,
                huWriteHabilitadoArquivo,
                DefaultsPerfilQ.HandlingUnitWrite(perfilAmbiente)),
            HandlingUnitBaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbienteHandlingUnitBaseUrl,
                handlingUnitBaseUrlArquivo),
            // Gates ISOLADOS do Produto Acabado / pipeline / palete INT012 (padrao false; independentes de WRITE_ENABLED e de HU).
            ProdutoAcabadoMaterialDocumentWriteHabilitado = ResolverGateOperacional(
                lerVariavelPorAlvo,
                VariavelAmbientePaMaterialDocumentWriteHabilitado,
                paMaterialDocumentWriteHabilitadoArquivo,
                DefaultsPerfilQ.MaterialDocumentProdutoAcabado(perfilAmbiente)),
            // GATE 120G: escrita de palete INT012. O perfil NAO participa (padraoPerfil: false sempre).
            PalletWriteHabilitado = ResolverGateOperacional(
                lerVariavelPorAlvo,
                VariavelAmbientePalletWriteHabilitado,
                palletWriteHabilitadoArquivo,
                padraoPerfil: false),
            ProdutoAcabadoPipelineHabilitado = ResolverGateOperacional(
                lerVariavelPorAlvo,
                VariavelAmbientePaPipelineHabilitado,
                paPipelineHabilitadoArquivo,
                DefaultsPerfilQ.PipelineProdutoAcabado(perfilAmbiente)),
            PalletInt012BaseUrl = ObterOuAmbiente(
                obterVariavelAmbiente,
                VariavelAmbientePalletInt012BaseUrl,
                palletInt012BaseUrlArquivo),
            PalletInt012HostsPermitidos = ObterHostsPermitidos(
                obterVariavelAmbiente,
                VariavelAmbientePalletInt012HostsPermitidos,
                palletInt012HostsPermitidosArquivo),
            // GATE 126V: SOMENTE arquivo. Nao ha variavel de ambiente para a tara fisica — ela e dado
            // operacional versionavel por instalacao, nao capability, e nao deve persistir em User/Machine.
            TarasFisicasEmbalagemPalete = tarasFisicasEmbalagemPaleteArquivo,
            // GATE 126X: registra, NA PROPRIA configuracao carregada, se o perfil local lido do arquivo foi
            // reconhecido como Q. E a mesma variavel que o 120G ja usava para conceder os defaults Q.
            PerfilLocalQ = perfilQ,
            // GATE 046-K: credenciais CPI/INT012 EXCLUSIVAMENTE por ambiente (nunca arquivo/JSON), sem fallback SAP.
            PalletInt012Usuario = ObterSomenteAmbiente(obterVariavelAmbiente, VariavelAmbientePalletInt012Usuario),
            PalletInt012Senha = ObterSomenteAmbiente(obterVariavelAmbiente, VariavelAmbientePalletInt012Senha),
            // GATE Q PACKAGING-05: capability Packaging só true por Process=true (User/Machine ausentes).
            // Sem leitor por alvo (ex.: testes de carga que não exercitam o gate) ⇒ false fail-closed.
            // GATE 120G: mesma regra, agora com o default do perfil Q quando nao ha Process explicito.
            PackagingHabilitado = lerVariavelPorAlvo is not null
                && ResolverPackagingHabilitado(lerVariavelPorAlvo, DefaultsPerfilQ.Packaging(perfilAmbiente)),
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

    /// <summary>
    /// GATE 126V: le o mapeamento de tara fisica da embalagem do palete. Entradas SEM peso numerico, com
    /// peso em tipo errado ou com chave logica incompleta sao DESCARTADAS na carga — nao viram zero, nao
    /// viram entrada parcial. Uma entrada descartada reaparece a jusante como NaoConfigurada (fail-closed).
    /// </summary>
    private static IReadOnlyList<TaraFisicaEmbalagemConfigurada> LerTarasFisicasEmbalagemPalete(
        JsonElement elemento,
        string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor)
            || valor.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<TaraFisicaEmbalagemConfigurada> mapeamentos = [];
        foreach (JsonElement item in valor.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string sapClient = LerTexto(item, "sap_client", string.Empty).Trim();
            string centro = LerTexto(item, "centro", string.Empty).Trim();
            string material = LerTexto(item, "material_embalagem", string.Empty).Trim();
            string unidade = LerTexto(item, "unidade", string.Empty).Trim();

            // Peso AUSENTE ou nao numerico ⇒ entrada descartada (nunca 0m implicito).
            if (!item.TryGetProperty("peso_kg", out JsonElement peso)
                || peso.ValueKind != JsonValueKind.Number
                || !peso.TryGetDecimal(out decimal pesoKg))
            {
                continue;
            }

            if (sapClient.Length == 0 || centro.Length == 0 || material.Length == 0)
            {
                continue;
            }

            mapeamentos.Add(new TaraFisicaEmbalagemConfigurada
            {
                SapClient = sapClient,
                Centro = centro,
                MaterialEmbalagem = material,
                PesoKg = pesoKg,
                Unidade = unidade
            });
        }

        return mapeamentos;
    }
}




