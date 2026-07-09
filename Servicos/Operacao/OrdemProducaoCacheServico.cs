using System.Collections.Concurrent;
using System.Diagnostics;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Operacao;

public interface IOrdemProducaoCacheServico
{
    bool TentarObter(string numeroOrdem, out OrdemProducaoSap ordem);

    void Armazenar(OrdemProducaoSap ordem);

    Task<ResultadoPreCarregamentoOrdensProducao> PreCarregarAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache em memória por sessão das OPs SAP relevantes para a Tela de Consumo.
/// Pré-carga roda em background após login; consulta da tela segue cache-first e fallback online.
/// </summary>
public sealed class OrdemProducaoCacheServico : IOrdemProducaoCacheServico
{
    public static readonly TimeSpan ValidadePadrao = TimeSpan.FromMinutes(20);
    public static readonly TimeSpan TimeoutPadrao = TimeSpan.FromMinutes(2);
    public const int ParalelismoDetalhePadrao = 4;

    public static OrdemProducaoCacheServico Compartilhado { get; } = new();

    private readonly ConcurrentDictionary<string, EntradaCache> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _travaPreCarga = new(1, 1);

    private readonly Func<IProductionOrderSapServico> _criarServicoSap;
    private readonly Action<string> _registrarDiagnostico;
    private readonly TimeSpan _validade;
    private readonly TimeSpan _timeout;

    public OrdemProducaoCacheServico()
        : this(FabricaProductionOrderSapServico.Criar, mensagem => Trace.TraceInformation(mensagem))
    {
    }

    internal OrdemProducaoCacheServico(
        Func<IProductionOrderSapServico> criarServicoSap,
        Action<string>? registrarDiagnostico = null,
        TimeSpan? validade = null,
        TimeSpan? timeout = null)
    {
        _criarServicoSap = criarServicoSap ?? throw new ArgumentNullException(nameof(criarServicoSap));
        _registrarDiagnostico = registrarDiagnostico ?? (_ => { });
        _validade = validade ?? ValidadePadrao;
        _timeout = timeout ?? TimeoutPadrao;
    }

    public bool TentarObter(string numeroOrdem, out OrdemProducaoSap ordem)
    {
        ordem = default!;
        string chave = ConsumoMaterialServico.NormalizarNumeroOrdem(numeroOrdem);
        if (string.IsNullOrWhiteSpace(chave))
        {
            return false;
        }

        if (!_cache.TryGetValue(chave, out EntradaCache? entrada) || entrada is null)
        {
            _registrarDiagnostico($"[Consumo][PreCargaOP] cache miss OP {chave}.");
            return false;
        }

        if (DateTimeOffset.Now - entrada.CarregadoEm > _validade)
        {
            _cache.TryRemove(chave, out _);
            _registrarDiagnostico($"[Consumo][PreCargaOP] cache expirado OP {chave}.");
            return false;
        }

        ordem = entrada.Ordem;
        _registrarDiagnostico($"[Consumo][PreCargaOP] cache hit OP {chave}.");
        return true;
    }

    public void Armazenar(OrdemProducaoSap ordem)
    {
        if (ordem is null || string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
        {
            return;
        }

        _cache[ConsumoMaterialServico.NormalizarNumeroOrdem(ordem.NumeroOrdem)] =
            new EntradaCache(ordem, DateTimeOffset.Now);
    }

    public async Task<ResultadoPreCarregamentoOrdensProducao> PreCarregarAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await _travaPreCarga.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new ResultadoPreCarregamentoOrdensProducao(
                CenarioPreCarregamentoOrdensProducao.JaEmAndamento,
                _cache.Count,
                null);
        }

        using CancellationTokenSource timeoutCts = new(_timeout);
        using CancellationTokenSource combinadoCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            _registrarDiagnostico("[Consumo][PreCargaOP] início da pré-carga de OPs SAP: planta=3007, liberadas, abertas, janela=30 dias.");
            IProductionOrderSapServico sapServico = _criarServicoSap();
            IReadOnlyList<OrdemProducaoSap> cabecalhos =
                await sapServico.ListarOrdensRelevantesAsync(combinadoCts.Token).ConfigureAwait(false);

            int carregadas = 0;
            using SemaphoreSlim paralelismo = new(ParalelismoDetalhePadrao, ParalelismoDetalhePadrao);
            IEnumerable<Task> tarefas = cabecalhos
                .Where(ordem => !string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
                .Select(async ordemResumo =>
                {
                    await paralelismo.WaitAsync(combinadoCts.Token).ConfigureAwait(false);
                    try
                    {
                        ResultadoConsultaOrdemProducaoSap detalhe =
                            await sapServico.ConsultarOrdemAsync(ordemResumo.NumeroOrdem, combinadoCts.Token).ConfigureAwait(false);
                        if (detalhe.Cenario == CenarioConsultaOrdemProducaoSap.Encontrada && detalhe.Ordem is not null)
                        {
                            Armazenar(detalhe.Ordem);
                            Interlocked.Increment(ref carregadas);
                        }
                    }
                    finally
                    {
                        paralelismo.Release();
                    }
                });

            await Task.WhenAll(tarefas).ConfigureAwait(false);
            stopwatch.Stop();
            _registrarDiagnostico($"[Consumo][PreCargaOP] fim da pré-carga: cabecalhos={cabecalhos.Count}, carregadas={carregadas}, tempo_ms={stopwatch.ElapsedMilliseconds}.");
            return new ResultadoPreCarregamentoOrdensProducao(
                CenarioPreCarregamentoOrdensProducao.Concluido,
                carregadas,
                DateTimeOffset.Now);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _registrarDiagnostico("[Consumo][PreCargaOP] pré-carga excedeu o tempo limite; fallback online continua disponível.");
            return new ResultadoPreCarregamentoOrdensProducao(
                CenarioPreCarregamentoOrdensProducao.Timeout,
                _cache.Count,
                null);
        }
        catch (OperationCanceledException)
        {
            return new ResultadoPreCarregamentoOrdensProducao(
                CenarioPreCarregamentoOrdensProducao.Cancelado,
                _cache.Count,
                null);
        }
        catch (Exception ex)
        {
            _registrarDiagnostico($"[Consumo][PreCargaOP] falha tratada ({ex.GetType().Name}); fallback online continua disponível.");
            return new ResultadoPreCarregamentoOrdensProducao(
                CenarioPreCarregamentoOrdensProducao.Erro,
                _cache.Count,
                null);
        }
        finally
        {
            _travaPreCarga.Release();
        }
    }

    private sealed record EntradaCache(OrdemProducaoSap Ordem, DateTimeOffset CarregadoEm);
}

public sealed record ResultadoPreCarregamentoOrdensProducao(
    CenarioPreCarregamentoOrdensProducao Cenario,
    int QuantidadeCarregada,
    DateTimeOffset? ConcluidoEm);

public enum CenarioPreCarregamentoOrdensProducao
{
    Concluido,
    JaEmAndamento,
    Timeout,
    Cancelado,
    Erro
}


