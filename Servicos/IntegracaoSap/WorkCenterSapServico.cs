using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal sealed class WorkCenterSapServico : IWorkCenterSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    public WorkCenterSapServico(ConfiguracaoSap configuracao)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
    }

    public async Task<ResultadoResolucaoWorkCenterSap> ResolverWorkCentersAsync(
        OrdemProducaoSap ordem,
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoes,
        CancellationToken cancellationToken = default)
    {
        if (!_configuracao.WorkCenterConfigurado)
        {
            return Falha("WorkCenter SAP não configurado para resolução Plant + WorkCenter.");
        }

        try
        {
            using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
            WorkCenterSapApiClient cliente = new(_configuracao, httpClient);
            return await cliente.ResolverWorkCentersAsync(ordem, operacoes, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning($"[SAP][WorkCenter] Falha ao resolver master: {ex.GetType().Name}.");
            return Falha("Não foi possível resolver o WorkCenter no SAP. Apontamento bloqueado.");
        }
    }

    private static ResultadoResolucaoWorkCenterSap Falha(string mensagem) => new(false, mensagem, []);
}

internal sealed class WorkCenterSapMockServico : IWorkCenterSapServico
{
    public Task<ResultadoResolucaoWorkCenterSap> ResolverWorkCentersAsync(
        OrdemProducaoSap ordem,
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoes,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ResultadoResolucaoWorkCenterSap(true, string.Empty, operacoes));
}

internal sealed class WorkCenterSapFailClosedServico : IWorkCenterSapServico
{
    public Task<ResultadoResolucaoWorkCenterSap> ResolverWorkCentersAsync(
        OrdemProducaoSap ordem,
        IReadOnlyList<OperacaoOrdemProducaoSap> operacoes,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ResultadoResolucaoWorkCenterSap(false,
            "WorkCenter SAP não resolvido. Apontamento bloqueado.", []));
}
