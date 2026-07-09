using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Servico de demonstracao: NAO retorna dados simulados da Ordem de Producao. Em ambiente
/// demonstrativo / banco desabilitado, a consulta da OP fica indisponivel (sem fake), mantendo
/// a Tela de Consumo sem dados simulados (Tarefa 2).
/// </summary>
internal sealed class ProductionOrderSapMockServico : IProductionOrderSapServico
{
    public bool EhSimulado => true;
    public bool Configurado => false;

    public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoConsultaOrdemProducaoSap.Indisponivel(
            "Consulta de Ordem de Producao indisponivel em ambiente demonstrativo."));
    public Task<IReadOnlyList<OrdemProducaoSap>> ListarOrdensRelevantesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrdemProducaoSap>>([]);
}
