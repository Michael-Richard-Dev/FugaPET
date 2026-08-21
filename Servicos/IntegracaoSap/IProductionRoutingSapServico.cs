using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 048-E REV2: resolucao READ-ONLY do roteiro AUTORITATIVO da OP para obter os
/// OperationStandardTextCode (marcador PP_FORM). A selecao parte de A_ProductionOrder_2.ProductionVersion
/// (via API_PRODUCTION_VERSION), nunca da primeira ocorrencia de ProductionRoutingMatlAssgmt. Qualquer
/// ausencia/ambiguidade/falha de contrato resulta em <c>null</c> (fail-closed) — nunca em excecao ao chamador.
/// </summary>
public interface IProductionRoutingSapServico
{
    /// <summary>
    /// Resolve o roteiro da OP. Retorna <c>null</c> quando: ProductionVersion ausente/nao resolvida;
    /// API_PRODUCTION_VERSION nao correlaciona grupo/variante/tipo; nenhum ou mais de um routing possivel;
    /// ou a integracao nao esta configurada/disponivel. NUNCA escolhe candidato por heuristica.
    /// </summary>
    Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
        OrdemProducaoSap ordem,
        CancellationToken cancellationToken = default);
}
