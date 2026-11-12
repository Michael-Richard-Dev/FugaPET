using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Estados da capability runtime de escrita SAP 261 (Consumo de Matéria-Prima) — GATE 101E. SOMENTE em memória,
/// nunca persistido. Diferente da 101, é BOUND a um <c>codigo_lancamento</c> específico.
/// </summary>
public enum EstadoCapabilitySap261
{
    Desabilitada = 0,
    Armada261 = 1,
    Consumida261 = 2,
    ReconciliacaoRequerida = 3
}

/// <summary>Snapshot imutável do estado corrente da capability 261 (sem segredo).</summary>
public readonly record struct SnapshotCapabilitySap261(
    EstadoCapabilitySap261 Estado,
    long? CodigoLancamento,
    DateTimeOffset? ExpiraEm,
    TimeSpan? TempoRestante);

/// <summary>Escopo de auditoria específico da capability 261.</summary>
public static class EscopoCapabilitySap261
{
    public const string Scope = "CONSUMO_MATERIAL_261";
}

/// <summary>
/// Capability runtime de escrita SAP, escopo ÚNICO Consumo 261, BOUND a <c>codigo_lancamento</c> (GATE 101E).
/// One-shot, consumo atômico do PAR (estado + codigo_lancamento), TTL 120s, reset no restart/logout/session change
/// (in-memory), sem auto-rearm/auto-retry/refund. ORTOGONAL ao gate de bootstrap FUGAPET_Q_SAP_WRITE_ENABLED e à
/// capability 101 — NÃO reutiliza <see cref="IRuntimeSapWriteCapabilityService"/> como autoridade 261.
/// </summary>
public interface IRuntimeSapWriteCapability261Service
{
    SnapshotCapabilitySap261 ObterEstado();

    /// <summary>PEEK — não consome, não autoriza POST. True só se ARMADA_261 para ESTE codigo_lancamento e dentro do TTL.</summary>
    bool EstaArmado261(long codigoLancamento);

    /// <summary>
    /// Cerimônia de habilitação bound ao <paramref name="codigoLancamento"/>. Só arma se <paramref name="autorizado"/>
    /// E a auditoria durável FAIL-CLOSED concluir sem exceção. Falha de auditoria ⇒ NÃO arma (Falha).
    /// </summary>
    Task<ResultadoOperacao> SolicitarHabilitacao261Async(
        long codigoLancamento,
        bool autorizado,
        Func<CancellationToken, Task> auditarHabilitacaoDuravelAsync,
        CancellationToken cancellationToken = default);

    /// <summary>Aquisição ATÔMICA one-shot: ARMADA_261(pk) → CONSUMIDA_261(pk). Exatamente um caller vence, e só
    /// para o <paramref name="codigoLancamento"/> exato armado.</summary>
    bool TryAdquirir261(long codigoLancamento);

    /// <summary>Marca reconciliação requerida (CONSUMIDA_261(pk) → RECONCILIACAO(pk)). Sem refund/rearm.</summary>
    void MarcarReconciliacao(long codigoLancamento);

    /// <summary>Desarma explicitamente para DESABILITADA (restart/logout/session change).</summary>
    void Desabilitar();
}

/// <summary>
/// Implementação com lock interno sobre o conjunto atômico (estado + codigo_lancamento + expiração). O lock garante
/// que "exatamente um caller vence" o TryAdquirir261. Relógio injetável para teste de TTL.
/// </summary>
public sealed class RuntimeSapWriteCapability261Service : IRuntimeSapWriteCapability261Service
{
    /// <summary>TTL padrão (GATE 101E). Não persistido; ajustável apenas em source.</summary>
    public static readonly TimeSpan TtlPadrao = TimeSpan.FromSeconds(120);

    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _relogio;
    private readonly object _sync = new();

    private EstadoCapabilitySap261 _estado = EstadoCapabilitySap261.Desabilitada;
    private long _codigoLancamento;   // PK bound; 0 quando DESABILITADA
    private long _expiraEmTicks;      // UtcTicks do vencimento; 0 quando não ARMADA

    public RuntimeSapWriteCapability261Service()
        : this(TtlPadrao, () => DateTimeOffset.UtcNow)
    {
    }

    internal RuntimeSapWriteCapability261Service(TimeSpan ttl, Func<DateTimeOffset> relogio)
    {
        _ttl = ttl > TimeSpan.Zero ? ttl : TtlPadrao;
        _relogio = relogio ?? (() => DateTimeOffset.UtcNow);
    }

    // Requer _sync. Se ARMADA_261 e TTL venceu, transiciona para DESABILITADA (não consome, não reconcilia).
    private void NormalizarExpiracaoSemLock()
    {
        if (_estado == EstadoCapabilitySap261.Armada261
            && _expiraEmTicks != 0
            && _relogio().UtcTicks > _expiraEmTicks)
        {
            _estado = EstadoCapabilitySap261.Desabilitada;
            _codigoLancamento = 0;
            _expiraEmTicks = 0;
        }
    }

    public SnapshotCapabilitySap261 ObterEstado()
    {
        lock (_sync)
        {
            NormalizarExpiracaoSemLock();
            if (_estado == EstadoCapabilitySap261.Armada261 && _expiraEmTicks != 0)
            {
                var expira = new DateTimeOffset(_expiraEmTicks, TimeSpan.Zero);
                TimeSpan restante = expira - _relogio();
                return new SnapshotCapabilitySap261(
                    _estado, _codigoLancamento, expira, restante > TimeSpan.Zero ? restante : TimeSpan.Zero);
            }

            long? pk = _estado is EstadoCapabilitySap261.Consumida261 or EstadoCapabilitySap261.ReconciliacaoRequerida
                ? _codigoLancamento
                : null;
            return new SnapshotCapabilitySap261(_estado, pk, null, null);
        }
    }

    public bool EstaArmado261(long codigoLancamento)
    {
        lock (_sync)
        {
            NormalizarExpiracaoSemLock();
            return _estado == EstadoCapabilitySap261.Armada261 && _codigoLancamento == codigoLancamento;
        }
    }

    public async Task<ResultadoOperacao> SolicitarHabilitacao261Async(
        long codigoLancamento,
        bool autorizado,
        Func<CancellationToken, Task> auditarHabilitacaoDuravelAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditarHabilitacaoDuravelAsync);

        if (codigoLancamento <= 0)
        {
            return ResultadoOperacao.Falha("Lançamento inválido para habilitar a escrita SAP 261.");
        }

        // Só arma a partir de DESABILITADA (expira antes de avaliar). Evita rearm silencioso de CONSUMIDA/RECONCILIACAO.
        lock (_sync)
        {
            NormalizarExpiracaoSemLock();
            if (_estado != EstadoCapabilitySap261.Desabilitada)
            {
                return ResultadoOperacao.Falha(
                    "Escrita SAP 261 já habilitada ou em uso. Aguarde a conclusão ou o reset.");
            }
        }

        if (!autorizado)
        {
            return ResultadoOperacao.Falha("Usuário sem permissão para habilitar a escrita SAP 261.");
        }

        // FAIL-CLOSED: a auditoria durável precede o armamento. Exceção ⇒ NÃO arma.
        try
        {
            await auditarHabilitacaoDuravelAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ResultadoOperacao.Falha(
                "Não foi possível registrar a autorização de escrita SAP 261 com durabilidade. Escrita NÃO habilitada.");
        }

        lock (_sync)
        {
            NormalizarExpiracaoSemLock();
            if (_estado != EstadoCapabilitySap261.Desabilitada)
            {
                return ResultadoOperacao.Falha("Escrita SAP 261 já habilitada por outra ação. Tente novamente.");
            }

            _estado = EstadoCapabilitySap261.Armada261;
            _codigoLancamento = codigoLancamento;
            _expiraEmTicks = _relogio().Add(_ttl).UtcTicks;
        }

        return ResultadoOperacao.Ok("Escrita SAP 261 habilitada (Consumo de Matéria-Prima, ambiente Q).");
    }

    public bool TryAdquirir261(long codigoLancamento)
    {
        lock (_sync)
        {
            NormalizarExpiracaoSemLock();
            if (_estado == EstadoCapabilitySap261.Armada261 && _codigoLancamento == codigoLancamento)
            {
                _estado = EstadoCapabilitySap261.Consumida261; // mantém _codigoLancamento para reconciliação
                _expiraEmTicks = 0;
                return true;
            }

            return false;
        }
    }

    public void MarcarReconciliacao(long codigoLancamento)
    {
        lock (_sync)
        {
            if (_estado == EstadoCapabilitySap261.Consumida261 && _codigoLancamento == codigoLancamento)
            {
                _estado = EstadoCapabilitySap261.ReconciliacaoRequerida;
            }
        }
    }

    public void Desabilitar()
    {
        lock (_sync)
        {
            _estado = EstadoCapabilitySap261.Desabilitada;
            _codigoLancamento = 0;
            _expiraEmTicks = 0;
        }
    }
}

/// <summary>
/// Portador singleton em memória da capability 261 (ORTOGONAL ao ValidadorAmbienteQ e à capability 101). Processo
/// novo ⇒ DESABILITADA (RESET_ON_RESTART). Nunca vai a DB/config/env.
/// </summary>
public static class RuntimeSapWriteCapability261
{
    public static IRuntimeSapWriteCapability261Service Instancia { get; } = new RuntimeSapWriteCapability261Service();
}
