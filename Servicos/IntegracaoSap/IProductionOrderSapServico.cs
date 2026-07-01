using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Entrada unica da integracao de Ordens de Producao SAP (API_PRODUCTION_ORDER_2_SRV) usada pela
/// Tela de Consumo de Materia-Prima. A escolha entre real, demonstracao e configuracao invalida
/// pertence a <see cref="FabricaProductionOrderSapServico"/>.
/// </summary>
public interface IProductionOrderSapServico
{
    bool EhSimulado { get; }
    bool Configurado { get; }

    Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default);
}
