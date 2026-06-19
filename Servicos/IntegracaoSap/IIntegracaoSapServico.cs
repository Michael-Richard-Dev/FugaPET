using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Contrato interno de integracao com o SAP.
/// CONCEITUAL: hoje so existe a implementacao mock (<see cref="IntegracaoSapMockServico"/>),
/// que NAO conversa com SAP real. Quando a integracao real for desenvolvida, basta criar uma
/// nova implementacao desta interface e troca-la em <see cref="FabricaIntegracaoSap"/> — as
/// telas e servicos consumidores permanecem inalterados.
/// </summary>
public interface IIntegracaoSapServico
{
    /// <summary>
    /// True quando os dados retornados sao simulados (sem SAP real).
    /// As telas usam para exibir o aviso de "dados simulados".
    /// </summary>
    bool EhSimulado { get; }

    /// <summary>
    /// Historico de movimentos de integracao (envios/retornos/erros), conforme o filtro.
    /// </summary>
    Task<IReadOnlyList<RegistroIntegracaoSap>> ConsultarHistoricoAsync(
        FiltroConsultaIntegracaoSap filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta dados de produto/ordem de producao no SAP. Retorna null quando a OP nao existe.
    /// </summary>
    Task<ProdutoSap?> ConsultarProdutoPorOrdemAsync(
        string ordemProducao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um apontamento de pesagem ao SAP e devolve o resultado (sucesso + protocolo ou falha).
    /// </summary>
    Task<ResultadoEnvioSap> EnviarApontamentoAsync(
        ApontamentoSap apontamento,
        CancellationToken cancellationToken = default);
}
