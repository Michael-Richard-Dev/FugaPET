using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Servico real de consulta (GET) da Ordem de Producao no SAP. Constroi o HttpClient seguro
/// (HTTPS, allowlist, timeout, TLS estrito) e delega ao <see cref="ProductionOrderSapApiClient"/>.
/// Nunca lanca para o chamador: traduz erros em cenarios e registra diagnostico SANITIZADO
/// (apenas etapa, status HTTP e tipo da excecao; sem Authorization/senha/cookie/payload).
/// </summary>
internal sealed class ProductionOrderSapServico : IProductionOrderSapServico
{
    private readonly ConfiguracaoSap _configuracao;

    // Costura de teste: substitui a chamada HTTP real ao cliente (para exercitar o catch sem rede).
    private readonly Func<string, CancellationToken, Task<OrdemProducaoSap?>>? _consultarOverride;

    public ProductionOrderSapServico(ConfiguracaoSap configuracao)
        : this(configuracao, null)
    {
    }

    internal ProductionOrderSapServico(
        ConfiguracaoSap configuracao,
        Func<string, CancellationToken, Task<OrdemProducaoSap?>>? consultarOverride)
    {
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _consultarOverride = consultarOverride;
    }

    public bool EhSimulado => false;
    public bool Configurado => _configuracao.ProductionOrderConfigurado;

    public async Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        if (_consultarOverride is null && !_configuracao.ProductionOrderConfigurado)
        {
            return ResultadoConsultaOrdemProducaoSap.NaoConfigurado(
                _configuracao.MensagemProductionOrderAusente());
        }

        try
        {
            OrdemProducaoSap? ordem = await ObterOrdemAsync(numeroOrdem, cancellationToken);

            return ordem is null
                ? ResultadoConsultaOrdemProducaoSap.NaoEncontrada()
                : ResultadoConsultaOrdemProducaoSap.Encontrada(ordem);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProductionOrderSapConsultaException ex)
        {
            // Falha controlada (cabecalho indisponivel ate no fallback) — diagnostico ja foi registrado.
            // Preserva a mensagem sanitizada da excecao (etapa/status) para a tela; ja sem credenciais.
            RegistrarDiagnostico(
                $"GET A_ProductionOrder_2 etapa {ex.Etapa}: falha controlada (HTTP {(ex.StatusHttp?.ToString() ?? "indefinido")}).");
            return ResultadoConsultaOrdemProducaoSap.Indisponivel(
                ex.Message, ex.StatusHttp);
        }
        catch (HttpRequestException ex)
        {
            int? status = (int?)ex.StatusCode;
            RegistrarDiagnostico(
                $"GET A_ProductionOrder_2 etapa HTTP status {(status?.ToString() ?? "indefinido")}: {ex.GetType().Name}.");
            return ResultadoConsultaOrdemProducaoSap.Indisponivel(
                "Nao foi possivel consultar a Ordem de Producao no SAP.", status);
        }
        catch (Exception ex)
        {
            // Rede/TLS/timeout/validacao de URL/etc. — antes ou durante o HTTP.
            RegistrarDiagnostico($"GET A_ProductionOrder_2 etapa CLIENTE: {ex.GetType().Name}.");
            return ResultadoConsultaOrdemProducaoSap.Indisponivel(
                "Nao foi possivel consultar a Ordem de Producao no SAP.");
        }
    }

    public async Task<IReadOnlyList<OrdemProducaoSap>> ListarOrdensRelevantesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_configuracao.ProductionOrderConfigurado)
        {
            RegistrarDiagnostico("Pre-carga OP ignorada: Production Order não configurado.");
            return [];
        }

        try
        {
            using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
            ProductionOrderSapApiClient cliente = new(_configuracao, httpClient, RegistrarDiagnostico);
            return await cliente.ListarOrdensRelevantesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RegistrarDiagnostico($"Pre-carga OP falhou na listagem ({ex.GetType().Name}).");
            return [];
        }
    }
    private async Task<OrdemProducaoSap?> ObterOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken)
    {
        if (_consultarOverride is not null)
        {
            return await _consultarOverride(numeroOrdem, cancellationToken);
        }

        using HttpClient httpClient = FabricaHttpClientSap.Criar(_configuracao);
        ProductionOrderSapApiClient cliente = new(_configuracao, httpClient, RegistrarDiagnostico);
        return await cliente.ConsultarOrdemAsync(numeroOrdem, cancellationToken);
    }

    internal static void RegistrarDiagnostico(string mensagem)
        => System.Diagnostics.Trace.TraceWarning($"[SAP][ProductionOrder] {mensagem}");
}


