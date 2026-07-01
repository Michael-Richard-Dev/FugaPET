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

    ResultadoEnvioConsumoSap261 ValidarProntoParaEnvio();

    Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
        ConsumoMaterialSap261Request requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default);
}
