using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Entrada unica do envio CONTROLADO do consumo 261 (API_MATERIAL_DOCUMENT_SRV). Aplica as travas de
/// configuracao/escrita (FUGAPET_SAP_WRITE_ENABLED) e registra log sanitizado. A escolha real/mock
/// pertence a <see cref="FabricaConsumoMaterialSap261Servico"/>.
/// </summary>
public interface IConsumoMaterialSap261Servico
{
    bool EhSimulado { get; }
    bool Configurado { get; }

    /// <summary>Prontidão ESTRUTURAL apenas (config/endpoint). GATE 101E-P2: NÃO bloqueia por EscritaHabilitada e
    /// NÃO consome capability — o gate final de escrita pertence ao writer.</summary>
    ResultadoEnvioConsumoSap261 ValidarProntoParaEnvio();

    /// <summary>
    /// GATE 101E-P2: recebe o <paramref name="codigoLancamento"/> EXPLÍCITO (nunca extraído de chaveNegocio). O
    /// FINAL WRITE GATE (env write OR capability261.TryAdquirir261(codigoLancamento)) é avaliado imediatamente
    /// antes do boundary HTTP; sem autorização ⇒ ZERO HTTP (EnvioAutorizado=false).
    /// </summary>
    Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
        ConsumoMaterialSap261Request requisicao,
        long codigoLancamento,
        string chaveNegocio,
        CancellationToken cancellationToken = default);
}
