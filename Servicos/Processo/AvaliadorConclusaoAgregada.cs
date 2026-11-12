using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>
/// GATE 048-E REV4: avaliação AGREGADA da conclusão de uma operação. Cruza o universo SAP autoritativo
/// (componentes/requisitos da operação, por Reservation+ReservationItem) com os processos locais vinculados.
/// Um único processo terminal NÃO conclui a operação sozinho: só CONCLUIDA quando TODOS os requisitos estão
/// satisfeitos. Não consulta SAP, não persiste, não decide término formal — é lógica pura e testável.
/// </summary>
public static class AvaliadorConclusaoAgregada
{
    public const string StatusLancamentoConfirmadoSap = "CONFIRMADO_SAP";
    /// <summary>
    /// §5: processo LOCAL TERMINAL para consumo 261 autorizado = status CONFIRMADO_SAP + documento/exercício
    /// de material persistidos. NÃO usa quantidade_retirada/pendente (stale). Processo terminal não reenvia.
    /// </summary>
    public static bool EhProcessoTerminal(ApontamentoProcesso processo)
        => processo is not null
           && TipoProcessoPossuiTerminalidadeConsumo(processo.TipoProcesso)
           && string.Equals((processo.StatusLancamento ?? string.Empty).Trim(), StatusLancamentoConfirmadoSap, StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(processo.DocumentoMaterialSap)
           && !string.IsNullOrWhiteSpace(processo.ExercicioMaterialSap);

    private static bool TipoProcessoPossuiTerminalidadeConsumo(string? tipoProcesso)
        => string.Equals(tipoProcesso, TipoProcessoOperacao.ConsumoMateriaPrima, StringComparison.Ordinal)
           || string.Equals(tipoProcesso, TipoProcessoOperacao.ConsumoQuimicos, StringComparison.Ordinal);

    public static bool EhResultadoApontamentoTerminal(ApontamentoProcesso processo)
        => processo is not null
           && string.Equals(processo.TipoProcesso, TipoProcessoOperacao.ResultadoApontamento, StringComparison.Ordinal)
           && processo.CodigoRegistroProcesso > 0;

    public static EstadoConclusaoAgregada AvaliarResultadoApontamento(IReadOnlyList<ApontamentoProcesso> vinculos)
    {
        ArgumentNullException.ThrowIfNull(vinculos);
        return vinculos.Any(EhResultadoApontamentoTerminal)
            ? EstadoConclusaoAgregada.Concluida
            : EstadoConclusaoAgregada.Pendente;
    }

    private enum EstadoRequisito
    {
        Concluido,
        Pendente,
        Indeterminado
    }

    private static EstadoRequisito ClassificarRequisito(
        ComponenteOrdemProducaoSap requisito,
        IReadOnlyList<ApontamentoProcesso> vinculos,
        IReadOnlyList<ComponenteConsumoDecisaoOperacional> decisoesZero)
    {
        // §4.3: identidade obrigatória Reservation + ReservationItem. Sem ela, INDETERMINADO (não presumir por material).
        if (string.IsNullOrWhiteSpace(requisito.Reserva)
            || string.IsNullOrWhiteSpace(requisito.ItemReserva))
        {
            return EstadoRequisito.Indeterminado;
        }

        // §4.6/§5: processo local terminal cobrindo o requisito PRESERVA a conclusão, mesmo com saldo SAP stale.
        bool cobertoPorProcessoTerminal = vinculos.Any(v =>
            EhProcessoTerminal(v)
            && string.Equals(v.Reservation, requisito.Reserva, StringComparison.Ordinal)
            && string.Equals(v.ReservationItem, requisito.ItemReserva, StringComparison.Ordinal));
        bool cobertoPorZeroIntencional = decisoesZero.Any(d =>
            string.Equals(d.DecisaoOperacional, ComponenteConsumoDecisaoOperacional.DecisaoZeroIntencional, StringComparison.Ordinal)
            && d.Quantidade == 0m
            && !string.IsNullOrWhiteSpace(requisito.Material)
            && string.Equals(d.NumeroReserva, requisito.Reserva, StringComparison.Ordinal)
            && string.Equals(d.ItemReserva, requisito.ItemReserva, StringComparison.Ordinal)
            && string.Equals(d.CodigoMaterial, requisito.Material, StringComparison.Ordinal));
        if (cobertoPorProcessoTerminal && cobertoPorZeroIntencional)
        {
            return EstadoRequisito.Indeterminado;
        }

        if (cobertoPorProcessoTerminal || cobertoPorZeroIntencional)
        {
            return EstadoRequisito.Concluido;
        }

        // FinalIssue=true → concluído.
        if (requisito.ReservaFinalizada)
        {
            return EstadoRequisito.Concluido;
        }

        // FinalIssue=false && Required > Withdrawn → pendente.
        if (requisito.QuantidadeNecessaria > requisito.QuantidadeRetirada)
        {
            return EstadoRequisito.Pendente;
        }

        // FinalIssue=false && Required <= Withdrawn → concluído SÓ com valores SAP autoritativos coerentes.
        return requisito.QuantidadeNecessaria >= 0m && requisito.QuantidadeRetirada >= 0m
            ? EstadoRequisito.Concluido
            : EstadoRequisito.Indeterminado;
    }

    /// <summary>
    /// Avalia o agregado. Universo SAP ausente/incompleto ⇒ INDETERMINADA (fail-closed). Qualquer requisito
    /// INDETERMINADO ⇒ INDETERMINADA. Caso contrário, PENDENTE se houver requisito pendente; senão CONCLUIDA.
    /// </summary>
    public static EstadoConclusaoAgregada Avaliar(
        IReadOnlyList<ComponenteOrdemProducaoSap>? requisitosSap,
        IReadOnlyList<ApontamentoProcesso> vinculos)
        => Avaliar(requisitosSap, vinculos, []);

    public static EstadoConclusaoAgregada Avaliar(
        IReadOnlyList<ComponenteOrdemProducaoSap>? requisitosSap,
        IReadOnlyList<ApontamentoProcesso> vinculos,
        IReadOnlyList<ComponenteConsumoDecisaoOperacional> decisoesZero)
    {
        ArgumentNullException.ThrowIfNull(vinculos);
        ArgumentNullException.ThrowIfNull(decisoesZero);

        if (requisitosSap is null || requisitosSap.Count == 0)
        {
            return EstadoConclusaoAgregada.Indeterminada;
        }

        bool algumPendente = false;
        foreach (ComponenteOrdemProducaoSap requisito in requisitosSap)
        {
            switch (ClassificarRequisito(requisito, vinculos, decisoesZero))
            {
                case EstadoRequisito.Indeterminado:
                    return EstadoConclusaoAgregada.Indeterminada;
                case EstadoRequisito.Pendente:
                    algumPendente = true;
                    break;
            }
        }

        return algumPendente ? EstadoConclusaoAgregada.Pendente : EstadoConclusaoAgregada.Concluida;
    }
}
