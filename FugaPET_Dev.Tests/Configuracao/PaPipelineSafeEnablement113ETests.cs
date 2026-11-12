using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// GATE 113E — habilitação SEGURA do pipeline 261→101→HU (fecha B1 e B3 do 113B) e paridade da etiqueta.
///
/// B1: o startup do Q bloqueava pa_pipeline e pa_material_document incondicionalmente. Agora existe o modo
/// SAFE_PA_PIPELINE_STARTUP, admitido SOMENTE com o TRIO completo
/// (pa_pipeline + pa_material_document + hu_write) e com a escrita genérica e o palete desligados.
/// Combinação parcial BLOQUEIA — habilitar meia cadeia é pior que não habilitar.
///
/// B3: SolicitarEnvioCaixaPipelineAsync não validava o ESTADO da caixa; uma caixa já CONFIRMADA_SAP
/// (CX-1000210-0006/0007) postaria 261 e 101 NOVOS antes de a idempotência da etapa HU ser consultada.
///
/// Testes puros: sem WinForms instanciado, sem banco, sem SAP.
/// </summary>
public sealed class PaPipelineSafeEnablement113ETests
{
    private const string HostQ = ValidadorAmbienteQ.SapHostEsperado;

    private static string Url(string servico)
        => $"https://{HostQ}:{ValidadorAmbienteQ.SapPortaEsperada}/sap/opu/odata/sap/{servico}/";

    private static ConfiguracaoSap ConfigQ(
        bool hu = false,
        bool generica = false,
        bool pipeline = false,
        bool paMaterialDocument = false,
        bool pallet = false)
        => new()
        {
            BaseUrl = Url("API_MATERIAL_DOCUMENT_SRV"),
            MaterialDocumentBaseUrl = Url("API_MATERIAL_DOCUMENT_SRV"),
            ProductionOrderBaseUrl = Url("API_PRODUCTION_ORDER_2_SRV"),
            ProductionOrderConfirmationBaseUrl = Url("API_PROD_ORDER_CONFIRMATION_2_SRV"),
            ProductBaseUrl = Url("API_PRODUCT_SRV"),
            HandlingUnitBaseUrl = Url("API_HANDLINGUNIT"),
            SapClient = "123",
            HostsPermitidos = [HostQ],
            EscritaHabilitada = generica,
            HuWriteHabilitado = hu,
            ProdutoAcabadoMaterialDocumentWriteHabilitado = paMaterialDocument,
            PalletWriteHabilitado = pallet,
            ProdutoAcabadoPipelineHabilitado = pipeline
        };

    private static ResultadoValidacaoAmbienteQ Validar(ConfiguracaoSap c) => ValidadorAmbienteQ.ValidarSap(c);

    private static ProdutoAcabadoCaixa Caixa(StatusIntegracaoCaixa status, long? codigo = 4004)
        => new()
        {
            CodigoProdutoAcabadoCaixa = codigo,
            CodigoCaixaLocal = "CX-1000210-0006",
            NumeroOrdemProducao = "1000210",
            Material = "4000174",
            Centro = "3007",
            StatusIntegracao = status
        };

    // ==================================================================
    // §2 / §6 A..F — STARTUP GUARD
    // ==================================================================

    [Fact] // A) HU-only seguro (112D) continua iniciando — NÃO houve regressão.
    public void A_HuOnlySeguro_ContinuaIniciando()
    {
        Assert.True(Validar(ConfigQ(hu: true)).Valido);
        Assert.True(Validar(ConfigQ()).Valido);
    }

    [Fact] // B) pipeline seguro COMPLETO inicia.
    public void B_PipelineSeguroCompleto_Inicia()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(pipeline: true, paMaterialDocument: true, hu: true));

        Assert.True(r.Valido, r.Mensagem);
    }

    [Fact] // C) pipeline=true SEM pa_material_document ⇒ BLOCK, nomeando o que falta.
    public void C_PipelineSemPaMaterialWrite_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(pipeline: true, hu: true));

        Assert.False(r.Valido);
        Assert.Contains("pa_material_document_write_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // D) pa_material_document=true SEM pipeline ⇒ BLOCK (não liberar 261/101 fora da orquestração).
    public void D_PaMaterialWriteSemPipeline_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(paMaterialDocument: true, hu: true));

        Assert.False(r.Valido);
        Assert.Contains("pa_pipeline_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // Trio incompleto na 3ª ponta: pipeline + pa_material_document SEM hu_write ⇒ BLOCK.
    public void PipelineSemHuWrite_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(pipeline: true, paMaterialDocument: true));

        Assert.False(r.Valido);
        Assert.Contains("hu_write_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // E) pipeline + escrita genérica ⇒ BLOCK (genérica nunca é admitida).
    public void E_PipelineComEscritaGenerica_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(
            ConfigQ(pipeline: true, paMaterialDocument: true, hu: true, generica: true));

        Assert.False(r.Valido);
        Assert.Contains("escrita_habilitada", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // F) pipeline + pallet ⇒ BLOCK (INT012 nunca é admitido).
    public void F_PipelineComPallet_Block()
    {
        ResultadoValidacaoAmbienteQ r = Validar(
            ConfigQ(pipeline: true, paMaterialDocument: true, hu: true, pallet: true));

        Assert.False(r.Valido);
        Assert.Contains("pallet_write_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Theory] // Gates sempre proibidos permanecem proibidos, isolados.
    [InlineData(true, false, "escrita_habilitada")]
    [InlineData(false, true, "pallet_write_habilitado")]
    public void GatesSempreProibidos_Block(bool generica, bool pallet, string esperado)
    {
        ResultadoValidacaoAmbienteQ r = Validar(ConfigQ(generica: generica, pallet: pallet));

        Assert.False(r.Valido);
        Assert.Contains(esperado, r.Mensagem, StringComparison.Ordinal);
    }

    [Fact] // O guard não foi afrouxado em nenhuma outra dimensão, mesmo no modo pipeline.
    public void ModoPipeline_NaoRelaxaOutrasValidacoesDoGuard()
    {
        ConfiguracaoSap baseOk = ConfigQ(pipeline: true, paMaterialDocument: true, hu: true);
        Assert.True(Validar(baseOk).Valido);

        ConfiguracaoSap sapClienteInvalido = ConfigQ(pipeline: true, paMaterialDocument: true, hu: true);
        // sap-client inválido continua bloqueando (validação anterior aos gates).
        Assert.False(Validar(new ConfiguracaoSap
        {
            BaseUrl = sapClienteInvalido.BaseUrl,
            MaterialDocumentBaseUrl = sapClienteInvalido.MaterialDocumentBaseUrl,
            ProductionOrderBaseUrl = sapClienteInvalido.ProductionOrderBaseUrl,
            ProductionOrderConfirmationBaseUrl = sapClienteInvalido.ProductionOrderConfirmationBaseUrl,
            ProductBaseUrl = sapClienteInvalido.ProductBaseUrl,
            HandlingUnitBaseUrl = sapClienteInvalido.HandlingUnitBaseUrl,
            SapClient = "12",
            HostsPermitidos = [HostQ],
            ProdutoAcabadoPipelineHabilitado = true,
            ProdutoAcabadoMaterialDocumentWriteHabilitado = true,
            HuWriteHabilitado = true
        }).Valido);

        // allowlist com host DS continua bloqueando.
        Assert.False(Validar(new ConfiguracaoSap
        {
            BaseUrl = baseOk.BaseUrl,
            MaterialDocumentBaseUrl = baseOk.MaterialDocumentBaseUrl,
            ProductionOrderBaseUrl = baseOk.ProductionOrderBaseUrl,
            ProductionOrderConfirmationBaseUrl = baseOk.ProductionOrderConfirmationBaseUrl,
            ProductBaseUrl = baseOk.ProductBaseUrl,
            HandlingUnitBaseUrl = baseOk.HandlingUnitBaseUrl,
            SapClient = "123",
            HostsPermitidos = [HostQ, "vhfufds4ci.sap.fugacouros.com.br"],
            ProdutoAcabadoPipelineHabilitado = true,
            ProdutoAcabadoMaterialDocumentWriteHabilitado = true,
            HuWriteHabilitado = true
        }).Valido);
    }

    // ==================================================================
    // §3 / §4 / §6 G..J — STATUS GUARD DO PIPELINE
    // ==================================================================

    [Fact] // G) CONFIRMADA_SAP (0006/0007) ⇒ INELEGÍVEL ⇒ zero 261, zero 101, zero HU.
    public void G_CaixaConfirmadaSap_Inelegivel()
    {
        ProdutoAcabadoCaixa caixa = Caixa(StatusIntegracaoCaixa.ConfirmadaSap);

        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(caixa));
        string msg = ProcessoProdutoAcabadoForm.MensagemCaixaInelegivelParaPipeline(caixa);
        Assert.Contains("CONFIRMADA_SAP", msg, StringComparison.Ordinal);
        Assert.Contains("Nenhum POST foi executado", msg, StringComparison.Ordinal);
    }

    [Fact] // H) CANCELADA ⇒ INELEGÍVEL.
    public void H_CaixaCancelada_Inelegivel()
        => Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(
            Caixa(StatusIntegracaoCaixa.Cancelada)));

    [Fact] // I) AGUARDANDO_AUTORIZACAO_SAP ⇒ ELEGÍVEL.
    public void I_CaixaAguardando_Elegivel()
        => Assert.True(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(
            Caixa(StatusIntegracaoCaixa.AguardandoAutorizacaoSap)));

    [Fact] // J) PRONTA_PARA_ENVIO ⇒ ELEGÍVEL.
    public void J_CaixaPronta_Elegivel()
        => Assert.True(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(
            Caixa(StatusIntegracaoCaixa.ProntaParaEnvio)));

    [Theory] // Allowlist: qualquer outro estado é INELEGÍVEL (não é denylist).
    [InlineData(StatusIntegracaoCaixa.EmPesagem)]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal)]
    [InlineData(StatusIntegracaoCaixa.PreviewHuGerado)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada)]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap)]
    [InlineData(StatusIntegracaoCaixa.Cancelada)]
    public void DemaisEstados_Inelegiveis(StatusIntegracaoCaixa status)
        => Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(Caixa(status)));

    [Fact] // Caixa sem código persistido ou nula ⇒ INELEGÍVEL (fail-closed).
    public void CaixaSemCodigoOuNula_Inelegivel()
    {
        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(null));
        Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(
            Caixa(StatusIntegracaoCaixa.ProntaParaEnvio, codigo: null)));
        Assert.Equal(
            "Nenhuma caixa elegível para o pipeline SAP.",
            ProcessoProdutoAcabadoForm.MensagemCaixaInelegivelParaPipeline(null));
    }

    [Fact] // §4: as duas caixas do incidente, explicitamente.
    public void Caixas0006E0007_ConfirmadaSap_NuncaEntramNoPipeline()
    {
        foreach (string local in new[] { "CX-1000210-0006", "CX-1000210-0007" })
        {
            ProdutoAcabadoCaixa caixa = new()
            {
                CodigoProdutoAcabadoCaixa = 6006,
                CodigoCaixaLocal = local,
                Centro = "3007",
                StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
                HandlingUnitExternalId = "300000100"
            };

            Assert.False(ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(caixa));
        }
    }

    // ==================================================================
    // §5 / §6 K — ETIQUETA SÓ APÓS PIPELINE INTEGRALMENTE CONFIRMADO
    // ==================================================================

    private static ResultadoPipelineProdutoAcabado Resultado(
        bool claim, EtapaPipelineProdutoAcabado etapa)
        => new(claim, etapa, "teste", null);

    [Fact] // K) só Concluido + claim obtido dispara a etiqueta.
    public void K_EtiquetaSomenteComPipelineConcluido()
    {
        Assert.True(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(
            Resultado(true, EtapaPipelineProdutoAcabado.Concluido)));
    }

    [Theory] // Etapa parcial ou bloqueio ⇒ NÃO imprime.
    [InlineData(EtapaPipelineProdutoAcabado.Movimento261)]
    [InlineData(EtapaPipelineProdutoAcabado.Movimento101)]
    [InlineData(EtapaPipelineProdutoAcabado.HandlingUnit)]
    [InlineData(EtapaPipelineProdutoAcabado.Bloqueada)]
    public void EtapaParcial_NaoImprimeEtiqueta(EtapaPipelineProdutoAcabado etapa)
        => Assert.False(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(Resultado(true, etapa)));

    [Fact] // Sem claim ⇒ NÃO imprime, mesmo se a etapa vier Concluido.
    public void SemClaim_NaoImprimeEtiqueta()
    {
        Assert.False(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(
            Resultado(false, EtapaPipelineProdutoAcabado.Concluido)));
        Assert.False(ProcessoProdutoAcabadoForm.PipelineConcluidoIntegralmente(null));
    }
}
