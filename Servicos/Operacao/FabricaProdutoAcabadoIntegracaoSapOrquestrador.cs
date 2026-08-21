using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// REV4-§3/§5/§15: composição PRODUTIVA do pipeline PA (261 → 101 → HU) a partir da <see cref="ConfiguracaoSap"/>.
/// Reutiliza os CLIENTES homologados (<c>ConsumoMaterialSap261ApiClient</c>, <c>MaterialDocumentSapApiClient</c>)
/// e o <c>ProdutoAcabadoHuService</c> homologado — sem duplicar HttpClient/CSRF e sem alterar contratos.
///
/// FAIL-CLOSED em cascata:
/// <list type="bullet">
///   <item>gate <c>FUGAPET_SAP_PA_PIPELINE_ENABLED</c>=false ⇒ <see cref="MotivoDesabilitado"/> (HU-only permanece);</item>
///   <item>gate true porém store SEM persistência definitiva (ex.: memória) ⇒ <see cref="MotivoDependenciaGaia"/>
///   (sem 261/101/HU) — Gaia ainda não entregou o repository do pipeline;</item>
/// </list>
/// Só quando o gate está true E há store persistente definitivo o orquestrador é composto. Mesmo então, os
/// gateways 261/101 continuam gated por <c>FUGAPET_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED</c> e o builder bloqueia
/// campos DEPENDENCIA_ARES. Nunca depende de FUGAPET_SAP_WRITE_ENABLED.
/// </summary>
public static class FabricaProdutoAcabadoIntegracaoSapOrquestrador
{
    public const string MotivoDesabilitado = "PIPELINE_DESABILITADO";
    public const string MotivoDependenciaGaia = "DEPENDENCIA_GAIA_PERSISTENCIA_PIPELINE";
    public const string MotivoComposto = "PIPELINE_COMPOSTO";

    /// <summary>Resultado da composição: quando indisponível, o <see cref="Orquestrador"/> é null e o runtime segue HU-only.</summary>
    public sealed record ResultadoComposicaoPipeline(
        bool Disponivel,
        string Motivo,
        ProdutoAcabadoPipeline045Orquestrador? Orquestrador);

    public static ResultadoComposicaoPipeline Compor(
        ConfiguracaoSap configuracao,
        ProdutoAcabadoHuService huService,
        IProdutoAcabadoPipelineStore? store)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(huService);

        // §4: gate específico do NOVO pipeline. False ⇒ HU-only homologado permanece EXATAMENTE como está.
        if (!configuracao.ProdutoAcabadoPipelineHabilitado)
        {
            return new ResultadoComposicaoPipeline(false, MotivoDesabilitado, null);
        }

        // §5/§10: pipeline SAP produtivo EXIGE persistência definitiva via contrato 045 (IProdutoAcabadoPipeline045Operacoes).
        // Store em memória (não implementa as operações 045) ou ausente/indisponível ⇒ fail-closed. NUNCA MemoryStore produtivo.
        if (store is not IProdutoAcabadoPipeline045Operacoes ops || !ops.SuportaPersistenciaDefinitiva)
        {
            return new ResultadoComposicaoPipeline(false, MotivoDependenciaGaia, null);
        }

        // Gate PA do MaterialDocument (261/101). NUNCA usa a escrita genérica; incompatível com ela ⇒ fica false.
        bool gatePa = configuracao.ProdutoAcabadoMaterialDocumentWriteHabilitado && !configuracao.EscritaHabilitada;

        // §9: config SAP faltando/ inválida ⇒ clientes NULOS (gateways fail-closed), NUNCA exceção na composição.
        // A persistência (store) segue disponível; o POST externo permanece bloqueado por gate/config.
        IConsumoMaterialSap261Client? cliente261 = null;
        IMaterialDocumentSapClient? clienteMatDoc = null;
        if (configuracao.Configurado)
        {
            try
            {
                cliente261 = new ConsumoMaterialSap261ClientAdapter(new ConsumoMaterialSap261ApiClient(configuracao, FabricaHttpClientSap.Criar(configuracao)));
                clienteMatDoc = new MaterialDocumentSapClientAdapter(new MaterialDocumentSapApiClient(configuracao, FabricaHttpClientSap.Criar(configuracao)));
            }
            catch (InvalidOperationException)
            {
                cliente261 = null; // URL/allowlist SAP inválida ⇒ fail-closed (sem cliente)
                clienteMatDoc = null;
            }
        }

        ProdutoAcabadoMovimento261Adapter gateway261 = new(cliente261, gatePa);
        ProdutoAcabadoMovimento101Gateway gateway101 = new(clienteMatDoc, gatePa);
        ProdutoAcabadoHuEnvioAdapter huEnvio = new(huService);

        // Orquestrador PRODUTIVO: persiste via contrato 045 (claim → POST → registrar), não por snapshot.
        ProdutoAcabadoPipeline045Orquestrador orquestrador = new(ops, gateway261, gateway101, huEnvio);
        return new ResultadoComposicaoPipeline(true, MotivoComposto, orquestrador);
    }
}
