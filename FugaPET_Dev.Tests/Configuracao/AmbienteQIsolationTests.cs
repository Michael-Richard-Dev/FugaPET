using System.Text.Json;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Configuracao;

public sealed class AmbienteQIsolationTests : IDisposable
{
    private const string HostQ = "vhfufqs4ci.sap.fugacouros.com.br";
    private const string HostDs = "vhfufds4ci.sap.fugacouros.com.br";
    private const string DatabaseQ = "fuga_jales_local_homologacao_q_v1_2";
    private const string DatabaseHml = "fuga_jales_local_homologacao_v1_2";
    private const string SchemaQ = "homologacao";
    private const string RoleQ = "fugapet_q_app";
    private readonly string _sapArquivo = Path.GetTempFileName();
    private readonly string _bancoArquivo = Path.GetTempFileName();

    [Fact]
    public void AppEnv_Q_Valido_Aceita()
    {
        ResultadoValidacaoAmbienteQ resultado = ValidadorAmbienteQ.ValidarAppEnv(nome => nome == "FUGAPET_Q_APP_ENV" ? "Q" : null);

        Assert.True(resultado.Valido);
    }

    [Fact]
    public void AppEnv_Ausente_Bloqueia()
    {
        ResultadoValidacaoAmbienteQ resultado = ValidadorAmbienteQ.ValidarAppEnv(_ => null);

        Assert.False(resultado.Valido);
        Assert.Contains("FUGAPET_Q_APP_ENV", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void AppEnv_DiferenteDeQ_Bloqueia()
    {
        ResultadoValidacaoAmbienteQ resultado = ValidadorAmbienteQ.ValidarAppEnv(nome => nome == "FUGAPET_Q_APP_ENV" ? "HML" : null);

        Assert.False(resultado.Valido);
    }

    [Fact]
    public void Sap_Q_IgnoraVariaveisLegadasEUsaNamespaceQ()
    {
        GravarSap(Url(HostDs, "API_MATERIAL_DOCUMENT_SRV"), HostDs, sapClient: "110");
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_BASE_URL"] = $"https://{HostDs}:44300/legacy",
            ["FUGAPET_Q_SAP_BASE_URL"] = $"https://{HostQ}:44300/q",
            ["FUGAPET_Q_SAP_ALLOWED_HOSTS"] = HostQ,
            ["FUGAPET_Q_SAP_CLIENT"] = "123"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_sapArquivo, nome => ambiente.GetValueOrDefault(nome));

        Assert.Equal($"https://{HostQ}:44300/q", configuracao.BaseUrl);
        Assert.Equal("123", configuracao.SapClient);
        Assert.Contains(HostQ, configuracao.HostsPermitidos);
        Assert.DoesNotContain(HostDs, configuracao.BaseUrl, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void Sap_QPurchaseOrderBaseUrl_IgnoraLegacyEUsaNamespaceQ()
    {
        GravarSap(Url(HostQ, "API_MATERIAL_DOCUMENT_SRV"), HostQ, sapClient: "110");
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_BASE_URL"] = Url(HostDs, "API_PURCHASEORDER_2"),
            ["FUGAPET_Q_SAP_PURCHASE_ORDER_BASE_URL"] = PurchaseOrderUrl(HostQ),
            ["FUGAPET_Q_SAP_ALLOWED_HOSTS"] = HostQ,
            ["FUGAPET_Q_SAP_CLIENT"] = "110"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_sapArquivo, nome => ambiente.GetValueOrDefault(nome));

        Assert.Equal(PurchaseOrderUrl(HostQ), configuracao.PurchaseOrderBaseUrl);
        Assert.Equal(PurchaseOrderUrl(HostQ), configuracao.PurchaseOrderBaseUrlEfetiva);
        Assert.DoesNotContain(HostDs, configuracao.PurchaseOrderBaseUrlEfetiva, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sap_LegacyAllowedHostsDs_NaoContaminaAllowlistQ()
    {
        GravarSap(Url(HostQ, "API_MATERIAL_DOCUMENT_SRV"), HostQ, sapClient: "123");
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_ALLOWED_HOSTS"] = HostDs,
            ["FUGAPET_Q_SAP_ALLOWED_HOSTS"] = HostQ
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_sapArquivo, nome => ambiente.GetValueOrDefault(nome));

        Assert.Contains(HostQ, configuracao.HostsPermitidos);
        Assert.DoesNotContain(HostDs, configuracao.HostsPermitidos);
        Assert.True(ValidadorAmbienteQ.ValidarSap(configuracao).Valido);
    }

    [Fact]
    public void Sap_LegacyWriteGateTrue_NaoHabilitaWritesQ()
    {
        GravarSap(Url(HostQ, "API_MATERIAL_DOCUMENT_SRV"), HostQ, sapClient: "123");
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_SAP_WRITE_ENABLED"] = "true",
            ["FUGAPET_SAP_HU_WRITE_ENABLED"] = "true",
            ["FUGAPET_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED"] = "true",
            ["FUGAPET_SAP_PA_PIPELINE_ENABLED"] = "true",
            ["FUGAPET_SAP_PALLET_WRITE_ENABLED"] = "true"
        };

        ConfiguracaoSap configuracao = LeitorConfiguracaoSap.Carregar(_sapArquivo, nome => ambiente.GetValueOrDefault(nome));

        Assert.False(configuracao.EscritaHabilitada);
        Assert.False(configuracao.HuWriteHabilitado);
        Assert.False(configuracao.ProdutoAcabadoMaterialDocumentWriteHabilitado);
        Assert.False(configuracao.PalletWriteHabilitado);
        Assert.False(configuracao.ProdutoAcabadoPipelineHabilitado);
        Assert.True(ValidadorAmbienteQ.ValidarSap(configuracao).Valido);
    }

    [Fact]
    public void Sap_QWriteGateTrue_BloqueiaStartup()
    {
        ConfiguracaoSap configuracao = SapQValido(produtoAcabadoPipelineHabilitado: true);

        Assert.False(ValidadorAmbienteQ.ValidarSap(configuracao).Valido);
    }

    [Fact]
    public void Sap_HostDs_BloqueiaEHostQ44300_Aceita()
    {
        ConfiguracaoSap ds = SapQValido(host: HostDs, hostsPermitidos: [HostDs]);
        ConfiguracaoSap q = SapQValido();

        Assert.False(ValidadorAmbienteQ.ValidarSap(ds).Valido);
        Assert.True(ValidadorAmbienteQ.ValidarSap(q).Valido);
    }

    [Fact]
    public void SapClient_AusenteOuInvalido_BloqueiaSemHardcode110()
    {
        Assert.False(ValidadorAmbienteQ.ValidarSap(SapQValido(sapClient: string.Empty)).Valido);
        Assert.False(ValidadorAmbienteQ.ValidarSap(SapQValido(sapClient: "Q")).Valido);
        Assert.True(ValidadorAmbienteQ.ValidarSap(SapQValido(sapClient: "123")).Valido);
    }

    [Fact]
    public void Banco_Q_IgnoraVariavelHmlEUsaNamespaceQ()
    {
        Dictionary<string, string> ambiente = new()
        {
            ["FUGAPET_HML_CONEXAO_POSTGRES"] = $"Host=192.168.3.226;Database={DatabaseHml};Username=fugapet_hml_app;Password=hml;Search Path={SchemaQ}",
            ["FUGAPET_Q_CONEXAO_POSTGRES"] = $"Host=192.168.3.226;Database={DatabaseQ};Username={RoleQ};Password=q;Search Path={SchemaQ}"
        };

        ConfiguracaoBancoPostgreSql configuracao = LeitorConfiguracaoBancoPostgreSql.Carregar(nome => ambiente.GetValueOrDefault(nome), _bancoArquivo);

        Assert.Equal("192.168.3.226", configuracao.Servidor);
        Assert.Equal(DatabaseQ, configuracao.NomeBanco);
        Assert.Equal(SchemaQ, configuracao.Schema);
        Assert.Equal(RoleQ, configuracao.Usuario);
    }

    [Fact]
    public void Banco_DatabaseQComSchemaHomologacao_Aceita()
    {
        ConfiguracaoBancoPostgreSql configuracao = BancoQValido();

        Assert.True(ValidadorAmbienteQ.ValidarBanco(configuracao).Valido);
        Assert.True(ValidadorAmbienteQ.ValidarIdentidadeBanco(configuracao, DatabaseQ, SchemaQ).Valido);
    }

    [Fact]
    public void Banco_DatabaseHmlComMesmoSchemaHomologacao_Bloqueia()
    {
        ConfiguracaoBancoPostgreSql configuracao = BancoQValido(nomeBanco: DatabaseHml);

        Assert.False(ValidadorAmbienteQ.ValidarBanco(configuracao).Valido);
    }

    [Fact]
    public void Banco_RoleHmlEmConfiguracaoQ_Bloqueia()
    {
        ConfiguracaoBancoPostgreSql configuracao = BancoQValido(usuario: "fugapet_hml_app");

        Assert.False(ValidadorAmbienteQ.ValidarBanco(configuracao).Valido);
    }

    [Fact]
    public void Banco_ConfigAusenteOuLocalhostOuIdentidadeAusente_Bloqueia()
    {
        ConfiguracaoBancoPostgreSql ausente = new();
        ConfiguracaoBancoPostgreSql localhost = BancoQValido(servidor: "localhost");
        ConfiguracaoBancoPostgreSql semDatabase = BancoQValido(nomeBanco: string.Empty);
        ConfiguracaoBancoPostgreSql semSchema = BancoQValido(schema: string.Empty);

        Assert.False(ValidadorAmbienteQ.ValidarBanco(ausente).Valido);
        Assert.False(ValidadorAmbienteQ.ValidarBanco(localhost).Valido);
        Assert.False(ValidadorAmbienteQ.ValidarBanco(semDatabase).Valido);
        Assert.False(ValidadorAmbienteQ.ValidarBanco(semSchema).Valido);
    }

    [Fact]
    public void Banco_DatabaseSchemaDivergentes_BloqueiaECorrespondentes_Aceita()
    {
        ConfiguracaoBancoPostgreSql configuracao = BancoQValido();

        Assert.False(ValidadorAmbienteQ.ValidarIdentidadeBanco(configuracao, "outro", SchemaQ).Valido);
        Assert.False(ValidadorAmbienteQ.ValidarIdentidadeBanco(configuracao, DatabaseQ, "outro").Valido);
        Assert.True(ValidadorAmbienteQ.ValidarIdentidadeBanco(configuracao, DatabaseQ, SchemaQ).Valido);
    }

    [Fact]
    public void Mensagens_DeErro_NaoExpoemSegredos()
    {
        ResultadoValidacaoAmbienteQ resultado = ValidadorAmbienteQ.ValidarBanco(new()
        {
            Servidor = "localhost",
            NomeBanco = DatabaseQ,
            Schema = SchemaQ,
            Usuario = "usuario-sensivel",
            Senha = "senha-super-secreta"
        });

        Assert.DoesNotContain("senha-super-secreta", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usuario-sensivel", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        File.Delete(_sapArquivo);
        File.Delete(_bancoArquivo);
    }

    private static ConfiguracaoSap SapQValido(
        string host = HostQ,
        string sapClient = "123",
        IReadOnlyList<string>? hostsPermitidos = null,
        bool produtoAcabadoPipelineHabilitado = false)
        => new()
        {
            BaseUrl = Url(host, "API_MATERIAL_DOCUMENT_SRV"),
            MaterialDocumentBaseUrl = Url(host, "API_MATERIAL_DOCUMENT_SRV"),
            ProductionOrderBaseUrl = Url(host, "API_PRODUCTION_ORDER_2_SRV"),
            ProductionOrderConfirmationBaseUrl = Url(host, "API_PROD_ORDER_CONFIRMATION_2_SRV"),
            ProductBaseUrl = Url(host, "API_PRODUCT_SRV"),
            HandlingUnitBaseUrl = Url(host, "API_HANDLINGUNIT"),
            SapClient = sapClient,
            HostsPermitidos = hostsPermitidos ?? [host],
            EscritaHabilitada = false,
            HuWriteHabilitado = false,
            ProdutoAcabadoMaterialDocumentWriteHabilitado = false,
            PalletWriteHabilitado = false,
            ProdutoAcabadoPipelineHabilitado = produtoAcabadoPipelineHabilitado
        };

    private static ConfiguracaoBancoPostgreSql BancoQValido(
        string servidor = "192.168.3.226",
        string nomeBanco = DatabaseQ,
        string schema = SchemaQ,
        string usuario = RoleQ)
        => new()
        {
            Servidor = servidor,
            Porta = 5432,
            NomeBanco = nomeBanco,
            Schema = schema,
            Usuario = usuario
        };

    private void GravarSap(string materialDocumentUrl, string host, string sapClient)
    {
        string json = JsonSerializer.Serialize(new
        {
            sap = new
            {
                base_url = materialDocumentUrl,
                material_document_base_url = materialDocumentUrl,
                production_order_base_url = Url(host, "API_PRODUCTION_ORDER_2_SRV"),
                production_order_confirmation_base_url = Url(host, "API_PROD_ORDER_CONFIRMATION_2_SRV"),
                product_base_url = Url(host, "API_PRODUCT_SRV"),
                handling_unit_base_url = Url(host, "API_HANDLINGUNIT"),
                sap_client = sapClient,
                hosts_permitidos = new[] { host },
                escrita_habilitada = false,
                hu_write_habilitado = false,
                pa_material_document_write_enabled = false,
                pa_pipeline_habilitado = false
            }
        });
        File.WriteAllText(_sapArquivo, json);
    }

    private static string Url(string host, string service)
        => $"https://{host}:44300/sap/opu/odata/sap/{service}/";
    private static string PurchaseOrderUrl(string host)
        => $"https://{host}:44300/sap/opu/odata4/sap/api_purchaseorder_2/srvd_a2x/sap/purchaseorder/0001/";
}




