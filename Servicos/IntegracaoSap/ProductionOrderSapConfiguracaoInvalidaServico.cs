using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Estado de erro de implantacao: arquivo de configuracao SAP existente porem malformado.
/// NAO cai em mock nem em "nao configurado" silencioso; bloqueia a consulta da OP com mensagem
/// operacional, sem expor caminho/conteudo do arquivo.
/// </summary>
internal sealed class ProductionOrderSapConfiguracaoInvalidaServico : IProductionOrderSapServico
{
    public bool EhSimulado => false;
    public bool Configurado => false;

    public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoConfigurado(
            ConfiguracaoSap.MensagemConfiguracaoInvalida));
    public Task<IReadOnlyList<OrdemProducaoSap>> ListarOrdensRelevantesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrdemProducaoSap>>([]);
}
