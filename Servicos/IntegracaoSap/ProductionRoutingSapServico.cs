using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 048-E REV2: servico real de resolucao do roteiro (GET). Constroi o HttpClient seguro e delega ao
/// <see cref="ProductionRoutingSapApiClient"/>. NUNCA lanca para o chamador: qualquer falha vira null
/// (fail-closed) com diagnostico SANITIZADO. Sem POST/PATCH.
/// </summary>
internal sealed class ProductionRoutingSapServico : IProductionRoutingSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui a chamada HTTP real (exercita o servico sem rede).
    private readonly Func<OrdemProducaoSap, CancellationToken, Task<RoteiroProducaoSap?>>? _resolverOverride;

    public ProductionRoutingSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProductionRoutingSapServico(
        ConfiguracaoSap configuracao,
        Func<OrdemProducaoSap, CancellationToken, Task<RoteiroProducaoSap?>>? resolverOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _resolverOverride = resolverOverride;
    }

    public async Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
        OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
    {
        if (_resolverOverride is null && !_configuracao.ProductionRoutingConfigurado)
        {
            RegistrarDiagnostico("Roteiro nao configurado (versao/roteiro/credenciais/allowlist) — fail-closed.");
            return null;
        }

        try
        {
            if (_resolverOverride is not null)
            {
                return await _resolverOverride(ordem, cancellationToken);
            }

            using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
            ProductionRoutingSapApiClient cliente = new(_configuracao, httpClient, RegistrarDiagnostico);
            return await cliente.ResolverRoteiroDaOrdemAsync(ordem, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Rede/TLS/timeout/validacao de URL/parse — fail-closed, sem vazar credencial nem payload.
            RegistrarDiagnostico($"Falha ao resolver roteiro: {ex.GetType().Name}.");
            return null;
        }
    }

    internal static void RegistrarDiagnostico(string mensagem)
        => System.Diagnostics.Trace.TraceWarning($"[SAP][ProductionRouting] {mensagem}");
}

/// <summary>
/// Modo demonstracao: marca TODAS as operacoes da OP simulada como manuais (PP_FORM), preservando o fluxo
/// demonstrativo do Controle de Apontamentos sem tocar SAP real.
/// </summary>
internal sealed class ProductionRoutingSapMockServico : IProductionRoutingSapServico
{
    public Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
        OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
        => Task.FromResult<RoteiroProducaoSap?>(new RoteiroProducaoSap
        {
            Operacoes = ordem.Operacoes
                .Where(o => !string.IsNullOrWhiteSpace(o.Operacao))
                .Select(o => new OperacaoRoteiroSap
                {
                    Operacao = o.Operacao,
                    CodigoTextoPadrao = MarcadorOperacaoManualSap.CodigoTextoPadraoManual,
                    TextoPadraoObtido = true
                })
                .ToList()
        });
}

/// <summary>Configuracao SAP invalida: roteiro indisponivel — sempre null (fail-closed).</summary>
internal sealed class ProductionRoutingSapFailClosedServico : IProductionRoutingSapServico
{
    public Task<RoteiroProducaoSap?> ResolverRoteiroDaOrdemAsync(
        OrdemProducaoSap ordem, CancellationToken cancellationToken = default)
        => Task.FromResult<RoteiroProducaoSap?>(null);
}
