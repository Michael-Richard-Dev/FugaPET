using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// GATE 112D — SAFE_HU_ONLY_STARTUP no ValidadorAmbienteQ.
///
/// O guard de bootstrap exigia TODOS os write gates false, o que impedia o Q de iniciar com
/// hu_write_habilitado=true e bloqueava a operação HU-only do Produto Acabado (única escrita do V6:
/// GET CSRF + 1 POST /HandlingUnit; sem 261, sem 101).
///
/// O guard NÃO foi afrouxado: hu_write_habilitado passou a ser o ÚNICO gate de escrita que pode iniciar
/// true, e apenas ISOLADO. Qualquer outro gate perigoso em true continua BLOQUEANDO o startup.
/// Testes puros, sem banco, sem SAP.
/// </summary>
public sealed class StartupGuardHuOnly112DTests
{
    private const string HostQ = ValidadorAmbienteQ.SapHostEsperado;

    private static string Url(string servico)
        => $"https://{HostQ}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/opu/odata/sap/{servico}/";

    /// <summary>Config Q válida em tudo, variando SOMENTE os gates de escrita.</summary>
    private static ConfiguracaoSap ConfigQ(
        bool hu = false,
        bool generica = false,
        bool pipeline = false,
        bool paMaterialDocument = false,
        bool pallet = false,
        string? handlingUnitBaseUrl = null,
        string sapClient = "123",
        IReadOnlyList<string>? hostsPermitidos = null)
        => new()
        {
            BaseUrl = Url("API_MATERIAL_DOCUMENT_SRV"),
            MaterialDocumentBaseUrl = Url("API_MATERIAL_DOCUMENT_SRV"),
            ProductionOrderBaseUrl = Url("API_PRODUCTION_ORDER_2_SRV"),
            ProductionOrderConfirmationBaseUrl = Url("API_PROD_ORDER_CONFIRMATION_2_SRV"),
            ProductBaseUrl = Url("API_PRODUCT_SRV"),
            HandlingUnitBaseUrl = handlingUnitBaseUrl ?? Url("API_HANDLINGUNIT"),
            SapClient = sapClient,
            HostsPermitidos = hostsPermitidos ?? [HostQ],
            EscritaHabilitada = generica,
            HuWriteHabilitado = hu,
            ProdutoAcabadoMaterialDocumentWriteHabilitado = paMaterialDocument,
            PalletWriteHabilitado = pallet,
            ProdutoAcabadoPipelineHabilitado = pipeline
        };

    private static ResultadoValidacaoAmbienteQ Validar(ConfiguracaoSap configuracao)
        => ValidadorAmbienteQ.ValidarSap(configuracao);

    // ==================================================================
    // §7 — MATRIZ OBRIGATÓRIA A..F
    // ==================================================================

    [Fact] // A) nenhum gate ligado ⇒ PASS (comportamento histórico preservado)
    public void A_TodosFalse_Pass()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ());

        Assert.True(r.Valido, r.Mensagem);
        Assert.Equal(string.Empty, r.Mensagem);
    }

    [Fact] // B) HU isolado ⇒ PASS (o delta deste gate)
    public void B_HuIsolado_Pass()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(hu: true));

        Assert.True(r.Valido, r.Mensagem);
    }

    [Fact] // C) HU + escrita genérica ⇒ BLOCK
    public void C_HuComEscritaGenerica_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(hu: true, generica: true));

        Assert.False(r.Valido);
        Assert.Contains("escrita_habilitada", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // D) HU + pipeline PA ⇒ BLOCK
    public void D_HuComPipeline_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(hu: true, pipeline: true));

        Assert.False(r.Valido);
        Assert.Contains("pa_pipeline_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // E) escrita genérica isolada ⇒ BLOCK (segue bloqueada, sem HU)
    public void E_EscritaGenericaIsolada_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(generica: true));

        Assert.False(r.Valido);
        Assert.Contains("escrita_habilitada", r.Mensagem, StringComparison.Ordinal);
    }

    [Theory] // F) qualquer outro gate perigoso ⇒ BLOCK, isolado ou junto com HU
    [InlineData(false, true, false, "pa_material_document_write_habilitado")]
    [InlineData(true, true, false, "pa_material_document_write_habilitado")]
    [InlineData(false, false, true, "pallet_write_habilitado")]
    [InlineData(true, false, true, "pallet_write_habilitado")]
    public void F_OutrosGatesPerigosos_Block(
        bool hu, bool paMaterialDocument, bool pallet, string gateEsperado)
    {
        ResultadoValidacaoAmbienteQ r = Validar(
            ConfigQ(hu: hu, paMaterialDocument: paMaterialDocument, pallet: pallet));

        Assert.False(r.Valido);
        Assert.Contains(gateEsperado, r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // Vários gates ligados ⇒ o diagnóstico nomeia TODOS (não só o primeiro).
    public void DiagnosticoNomeiaTodosOsGatesLigados()
    {
        ResultadoValidacaoAmbienteQ r = Validar(
            ConfigQ(hu: true, generica: true, pipeline: true, paMaterialDocument: true, pallet: true));

        Assert.False(r.Valido);
        Assert.Contains("escrita_habilitada", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("pa_material_document_write_habilitado", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("pallet_write_habilitado", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("pa_pipeline_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // O guard NÃO foi afrouxado: a mensagem-contrato histórica permanece.
    public void MensagemContratoHistoricaPreservada()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(generica: true));

        Assert.Contains("write gates devem iniciar false", r.Mensagem, StringComparison.Ordinal);
        // E orienta explicitamente qual é a única exceção admitida.
        Assert.Contains("hu_write_habilitado", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("HU-only", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // hu_write_habilitado NÃO integra a lista de gates perigosos (é a exceção, por desenho).
    public void HuNaoEhListadoComoGatePerigoso()
    {
        Assert.Empty(ValidadorAmbienteQ.ObterGatesPerigososLigados(ConfigQ(hu: true)));
        Assert.Empty(ValidadorAmbienteQ.ObterGatesPerigososLigados(ConfigQ()));
        Assert.NotEmpty(ValidadorAmbienteQ.ObterGatesPerigososLigados(ConfigQ(hu: true, generica: true)));
    }

    // ==================================================================
    // §5 — o guard continua íntegro nas demais dimensões
    // ==================================================================

    [Fact] // HU=true não dispensa host/porta/allowlist/sap-client do contrato Q.
    public void HuTrue_NaoRelaxaOutrasValidacoesDoGuard()
    {
        // host fora do Q
        Assert.False(Validar(ConfigQ(
            hu: true,
            handlingUnitBaseUrl: "https://vhfufds4ci.sap.fugacouros.com.br:44300/sap/opu/odata/sap/API_HANDLINGUNIT/")).Valido);

        // allowlist com host DS
        Assert.False(Validar(ConfigQ(
            hu: true,
            hostsPermitidos: [HostQ, "vhfufds4ci.sap.fugacouros.com.br"])).Valido);

        // sap-client inválido
        Assert.False(Validar(ConfigQ(hu: true, sapClient: "12")).Valido);

        // URL http (não https)
        Assert.False(Validar(ConfigQ(
            hu: true,
            handlingUnitBaseUrl:
                $"http://{HostQ}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/opu/odata/sap/API_HANDLINGUNIT/")).Valido);
    }

    [Fact] // O cenário operacional exato do 112A/112C inicia: HU=true, genérica=false, pipeline=false.
    public void CenarioOperacional112A_Inicia()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(hu: true, generica: false, pipeline: false));

        Assert.True(r.Valido, r.Mensagem);
    }
}
