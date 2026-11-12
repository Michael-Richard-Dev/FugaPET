using System.Threading;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Estados da capability de escrita SAP em runtime (12E-E-B). SOMENTE em memória; nunca persistido.
/// </summary>
public enum EstadoCapabilitySap
{
    Desabilitada = 0,
    Armada101 = 1,
    Consumida = 2,
    ReconciliacaoRequerida = 3
}

/// <summary>Snapshot imutável do estado corrente (sem segredo).</summary>
public readonly record struct SnapshotCapabilitySap(
    EstadoCapabilitySap Estado,
    DateTimeOffset? ExpiraEm,
    TimeSpan? TempoRestante);

/// <summary>
/// Códigos de ação de auditoria da capability (sink auditoria_acao_usuario). ENABLE_REQUESTED/ENABLED/DENIED
/// jamais podem ser fail-silent; CONSUMED/EXPIRED/RECONCILIATION são best-effort.
/// </summary>
public static class AcoesCapabilitySap
{
    public const string EnableRequested = "CAPABILITY_ENABLE_REQUESTED";
    public const string Enabled = "CAPABILITY_ENABLED";
    public const string Denied = "CAPABILITY_DENIED";
    public const string Consumed = "CAPABILITY_CONSUMED";
    public const string Expired = "CAPABILITY_EXPIRED";
    public const string ReconciliationRequired = "RECONCILIATION_REQUIRED";
}

/// <summary>
/// Capability runtime de escrita SAP, escopo ÚNICO Material Document 101 (12E-D-A/12E-D-B congelados).
/// One-shot, consumo atômico, TTL, reset no restart (in-memory), sem auto-rearm/auto-retry/refund.
/// O gate de bootstrap FUGAPET_Q_SAP_WRITE_ENABLED permanece intocado (ValidadorAmbienteQ): esta capability
/// é ORTOGONAL e não lê/escreve env/DB/config.
/// </summary>
public interface IRuntimeSapWriteCapabilityService
{
    SnapshotCapabilitySap ObterEstado();

    /// <summary>PEEK — não consome, não autoriza POST. True somente se ARMADA_101 e dentro do TTL.
    /// Uso exclusivo para diagnóstico/UI (C7) e precheck do controller.</summary>
    bool EstaArmado101 { get; }

    /// <summary>
    /// Cerimônia de habilitação. Só arma se <paramref name="autorizado"/> E a auditoria durável FAIL-CLOSED
    /// concluir sem exceção. Falha de persistência de auditoria ⇒ NÃO arma (retorna Falha).
    /// </summary>
    Task<ResultadoOperacao> SolicitarHabilitacao101Async(
        bool autorizado,
        Func<CancellationToken, Task> auditarHabilitacaoDuravelAsync,
        CancellationToken cancellationToken = default);

    /// <summary>Aquisição ATÔMICA one-shot: ARMADA_101 → CONSUMIDA. Exatamente um caller vence.</summary>
    bool TryAdquirir101();

    /// <summary>Marca reconciliação requerida (CONSUMIDA → RECONCILIACAO). Sem refund/rearm.</summary>
    void MarcarReconciliacao();

    /// <summary>Desarma explicitamente para DESABILITADA.</summary>
    void Desabilitar();
}

/// <summary>
/// Implementação lock-free (Interlocked.CompareExchange) da capability 101. Estado inteiro + expiração em ticks,
/// ambos manipulados por operações atômicas. Relógio injetável para teste de TTL.
/// </summary>
public sealed class RuntimeSapWriteCapabilityService : IRuntimeSapWriteCapabilityService
{
    /// <summary>TTL padrão do MVP (12E-E-B). Não persistido; ajustável apenas em source.</summary>
    public static readonly TimeSpan TtlPadrao = TimeSpan.FromSeconds(120);

    private readonly TimeSpan _ttl;
    private readonly Func<DateTimeOffset> _relogio;

    private int _estado = (int)EstadoCapabilitySap.Desabilitada;
    private long _expiraEmTicks; // DateTimeOffset.UtcTicks do vencimento; 0 quando não armada.

    public RuntimeSapWriteCapabilityService()
        : this(TtlPadrao, () => DateTimeOffset.UtcNow)
    {
    }

    internal RuntimeSapWriteCapabilityService(TimeSpan ttl, Func<DateTimeOffset> relogio)
    {
        _ttl = ttl > TimeSpan.Zero ? ttl : TtlPadrao;
        _relogio = relogio ?? (() => DateTimeOffset.UtcNow);
    }

    private bool Expirou()
    {
        long ticks = Interlocked.Read(ref _expiraEmTicks);
        return ticks != 0 && _relogio().UtcTicks > ticks;
    }

    // Se está ARMADA_101 e o TTL venceu, transiciona para DESABILITADA (não consome).
    private EstadoCapabilitySap NormalizarExpiracao()
    {
        var atual = (EstadoCapabilitySap)Volatile.Read(ref _estado);
        if (atual == EstadoCapabilitySap.Armada101 && Expirou()
            && Interlocked.CompareExchange(
                ref _estado,
                (int)EstadoCapabilitySap.Desabilitada,
                (int)EstadoCapabilitySap.Armada101) == (int)EstadoCapabilitySap.Armada101)
        {
            Interlocked.Exchange(ref _expiraEmTicks, 0);
            return EstadoCapabilitySap.Desabilitada;
        }

        return (EstadoCapabilitySap)Volatile.Read(ref _estado);
    }

    public SnapshotCapabilitySap ObterEstado()
    {
        EstadoCapabilitySap estado = NormalizarExpiracao();
        long ticks = Interlocked.Read(ref _expiraEmTicks);
        if (estado != EstadoCapabilitySap.Armada101 || ticks == 0)
        {
            return new SnapshotCapabilitySap(estado, null, null);
        }

        var expiraEm = new DateTimeOffset(ticks, TimeSpan.Zero);
        TimeSpan restante = expiraEm - _relogio();
        return new SnapshotCapabilitySap(estado, expiraEm, restante > TimeSpan.Zero ? restante : TimeSpan.Zero);
    }

    public bool EstaArmado101 => NormalizarExpiracao() == EstadoCapabilitySap.Armada101;

    public async Task<ResultadoOperacao> SolicitarHabilitacao101Async(
        bool autorizado,
        Func<CancellationToken, Task> auditarHabilitacaoDuravelAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditarHabilitacaoDuravelAsync);

        // Só arma a partir de DESABILITADA (expira antes de avaliar). Evita rearm silencioso de CONSUMIDA/RECONCILIACAO.
        if (NormalizarExpiracao() != EstadoCapabilitySap.Desabilitada)
        {
            return ResultadoOperacao.Falha(
                "Escrita SAP 101 já habilitada ou em uso. Aguarde a conclusão ou o reset.");
        }

        if (!autorizado)
        {
            return ResultadoOperacao.Falha("Usuário sem permissão para habilitar a escrita SAP 101.");
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
                "Não foi possível registrar a autorização de escrita SAP com durabilidade. Escrita NÃO habilitada.");
        }

        Interlocked.Exchange(ref _expiraEmTicks, _relogio().Add(_ttl).UtcTicks);
        if (Interlocked.CompareExchange(
                ref _estado,
                (int)EstadoCapabilitySap.Armada101,
                (int)EstadoCapabilitySap.Desabilitada) != (int)EstadoCapabilitySap.Desabilitada)
        {
            Interlocked.Exchange(ref _expiraEmTicks, 0);
            return ResultadoOperacao.Falha("Escrita SAP 101 já habilitada por outra ação. Tente novamente.");
        }

        return ResultadoOperacao.Ok("Escrita SAP 101 habilitada (Material Document, ambiente Q).");
    }

    public bool TryAdquirir101()
    {
        if (Expirou())
        {
            NormalizarExpiracao();
            return false;
        }

        return Interlocked.CompareExchange(
            ref _estado,
            (int)EstadoCapabilitySap.Consumida,
            (int)EstadoCapabilitySap.Armada101) == (int)EstadoCapabilitySap.Armada101;
    }

    public void MarcarReconciliacao()
        => Interlocked.CompareExchange(
            ref _estado,
            (int)EstadoCapabilitySap.ReconciliacaoRequerida,
            (int)EstadoCapabilitySap.Consumida);

    public void Desabilitar()
    {
        Interlocked.Exchange(ref _estado, (int)EstadoCapabilitySap.Desabilitada);
        Interlocked.Exchange(ref _expiraEmTicks, 0);
    }
}

/// <summary>
/// Portador singleton em memória da capability (ORTOGONAL ao ValidadorAmbienteQ). Processo novo ⇒ DESABILITADA
/// (RESET_ON_RESTART). Nunca vai a DB/config/env.
/// </summary>
public static class RuntimeSapWriteCapability
{
    public static IRuntimeSapWriteCapabilityService Instancia { get; } = new RuntimeSapWriteCapabilityService();
}
