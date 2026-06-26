using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Entrada;

/// <summary>
/// Pre-carregamento assincrono (em segundo plano) dos pedidos relevantes da Entrada apos o login.
/// Objetivo: aquecer o cache/banco local para que a Tela de Entrada responda rapido consultando
/// primeiro o cache, caindo no GET especifico do SAP apenas quando o pedido nao estiver no cache.
///
/// Garantias:
/// - NAO bloqueia a UI nem o login (e disparado via Task.Run depois que o MainForm abre);
/// - trava simples (<see cref="SemaphoreSlim"/>) impede duas sincronizacoes simultaneas;
/// - timeout por execucao;
/// - cancelavel (token do fechamento do MainForm);
/// - nunca derruba o sistema: qualquer excecao vira diagnostico sanitizado;
/// - log/diagnostico sem segredo (apenas o tipo da excecao / mensagem funcional).
/// A carga em si e FILTRADA pelo escopo (grupo/centro/itens recebiveis) e PAGINADA pelo servico real.
/// </summary>
public sealed class PreCarregamentoPedidosEntradaServico
{
    public static readonly TimeSpan TimeoutPadrao = TimeSpan.FromMinutes(2);

    // Trava de instancia: o MainForm mantem UMA instancia; WaitAsync(0) garante "no maximo uma execucao".
    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly Func<CancellationToken, Task<ResultadoOperacao>> _carregarPedidosRelevantes;
    private readonly Action<string> _registrarDiagnostico;
    private readonly TimeSpan _timeout;

    /// <summary>Construtor de producao: liga a carga ao seam real de integracao da Entrada.</summary>
    public PreCarregamentoPedidosEntradaServico()
        : this(
            cancellationToken => new IntegracaoEntradaSapServico(FabricaPedidoCompraSapServico.Criar())
                .PreCarregarCacheEntradaAsync(cancellationToken),
            mensagem => System.Diagnostics.Trace.TraceInformation(mensagem),
            TimeoutPadrao)
    {
    }

    /// <summary>Construtor para testes: injeta a acao de carga, o diagnostico e o timeout.</summary>
    internal PreCarregamentoPedidosEntradaServico(
        Func<CancellationToken, Task<ResultadoOperacao>> carregarPedidosRelevantes,
        Action<string>? registrarDiagnostico = null,
        TimeSpan? timeout = null)
    {
        _carregarPedidosRelevantes = carregarPedidosRelevantes
            ?? throw new ArgumentNullException(nameof(carregarPedidosRelevantes));
        _registrarDiagnostico = registrarDiagnostico ?? (_ => { });
        _timeout = timeout ?? TimeoutPadrao;
    }

    /// <summary>Momento da ultima conclusao com sucesso (para exibir "atualizados as HH:mm").</summary>
    public DateTimeOffset? UltimaConclusao { get; private set; }

    /// <summary>True enquanto uma sincronizacao esta em andamento.</summary>
    public bool EmAndamento => _trava.CurrentCount == 0;

    public async Task<ResultadoPreCarregamentoEntrada> ExecutarAsync(
        CancellationToken cancellationToken = default)
    {
        // Trava simples: se ja ha uma sincronizacao em andamento, NAO inicia outra (sem bloquear).
        if (!await _trava.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new ResultadoPreCarregamentoEntrada(CenarioPreCarregamentoEntrada.JaEmAndamento, null);
        }

        using CancellationTokenSource timeoutCts = new(_timeout);
        using CancellationTokenSource combinadoCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            ResultadoOperacao resultado =
                await _carregarPedidosRelevantes(combinadoCts.Token).ConfigureAwait(false);

            if (resultado.Sucesso)
            {
                UltimaConclusao = DateTimeOffset.Now;
                _registrarDiagnostico("Pre-carregamento de pedidos de entrada concluido.");
                return new ResultadoPreCarregamentoEntrada(
                    CenarioPreCarregamentoEntrada.Concluido,
                    UltimaConclusao);
            }

            _registrarDiagnostico(
                $"Pre-carregamento de pedidos de entrada indisponivel: {resultado.Mensagem}");
            return new ResultadoPreCarregamentoEntrada(
                CenarioPreCarregamentoEntrada.Indisponivel,
                null);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _registrarDiagnostico("Pre-carregamento de pedidos de entrada excedeu o tempo limite.");
            return new ResultadoPreCarregamentoEntrada(CenarioPreCarregamentoEntrada.Timeout, null);
        }
        catch (OperationCanceledException)
        {
            // Cancelamento externo (ex.: fechamento do MainForm) — encerra silenciosamente.
            return new ResultadoPreCarregamentoEntrada(CenarioPreCarregamentoEntrada.Cancelado, null);
        }
        catch (Exception ex)
        {
            // Falha em background NUNCA pode derrubar o sistema; registra apenas o tipo (sem segredo/payload).
            _registrarDiagnostico(
                $"Pre-carregamento de pedidos de entrada falhou ({ex.GetType().Name}).");
            return new ResultadoPreCarregamentoEntrada(CenarioPreCarregamentoEntrada.Erro, null);
        }
        finally
        {
            _trava.Release();
        }
    }
}

/// <summary>Resultado de uma execucao do pre-carregamento.</summary>
public sealed record ResultadoPreCarregamentoEntrada(
    CenarioPreCarregamentoEntrada Cenario,
    DateTimeOffset? ConcluidoEm);

public enum CenarioPreCarregamentoEntrada
{
    /// <summary>Carga concluida com sucesso; cache aquecido.</summary>
    Concluido,

    /// <summary>Ja havia uma sincronizacao em andamento; esta execucao foi ignorada.</summary>
    JaEmAndamento,

    /// <summary>SAP/integracao indisponivel; sistema segue operando com o cache local.</summary>
    Indisponivel,

    /// <summary>Carga excedeu o tempo limite.</summary>
    Timeout,

    /// <summary>Cancelada (fechamento do MainForm).</summary>
    Cancelado,

    /// <summary>Erro inesperado tratado; sistema continua operando.</summary>
    Erro
}
