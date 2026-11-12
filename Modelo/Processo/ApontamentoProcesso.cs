namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// GATE 048-E REV4: vínculo 1:N APONTAMENTO → PROCESSO (tabela operacao_producao_apontamento_processo).
/// Identidade lógica: (<see cref="CodigoApontamento"/>, <see cref="TipoProcesso"/>, <see cref="CodigoRegistroProcesso"/>).
/// O estado terminal é lido do lançamento (status + documento/exercício persistidos), NUNCA de
/// quantidade_retirada/pendente locais (comprovadamente stale no processo 63).
/// </summary>
public sealed record ApontamentoProcesso
{
    public long CodigoApontamentoProcesso { get; init; }
    public long CodigoApontamento { get; init; }
    public string TipoProcesso { get; init; } = string.Empty;
    public long CodigoRegistroProcesso { get; init; }

    /// <summary>Status do lançamento do processo (ex.: CONFIRMADO_SAP). Lido do registro, não decidido aqui.</summary>
    public string StatusLancamento { get; init; } = string.Empty;

    /// <summary>Documento de material SAP persistido do processo (parte da prova de terminalidade).</summary>
    public string DocumentoMaterialSap { get; init; } = string.Empty;

    /// <summary>Exercício do documento de material SAP persistido do processo.</summary>
    public string ExercicioMaterialSap { get; init; } = string.Empty;

    /// <summary>Identidade do requisito coberto (Reservation), quando disponível.</summary>
    public string Reservation { get; init; } = string.Empty;

    /// <summary>Identidade do requisito coberto (ReservationItem), quando disponível.</summary>
    public string ReservationItem { get; init; } = string.Empty;
}

/// <summary>Estado AGREGADO da conclusão da operação (todos os requisitos SAP × processos locais).</summary>
public enum EstadoConclusaoAgregada
{
    /// <summary>Todos os requisitos concluídos (por FinalIssue, saldo SAP coerente, ou processo local terminal).</summary>
    Concluida,

    /// <summary>Ao menos um requisito ainda pendente. O apontamento permanece EM_ANDAMENTO.</summary>
    Pendente,

    /// <summary>Universo SAP ausente/ambíguo/incompatível. Fail-closed: não transiciona.</summary>
    Indeterminada
}
