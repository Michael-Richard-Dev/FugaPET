namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// GATE 096D — resultado ATÔMICO da reserva/claim do lançamento para envio SAP 101. <see cref="Reservado"/>
/// indica se a transição FINALIZADO_LOCAL/ERRO_SAP → ENVIADO_SAP ocorreu neste claim; <see cref="StatusAnterior"/>
/// é o estado EFETIVAMENTE consumido pela transição atômica (usado para restaurar a reserva num abort pré-POST).
/// StatusAnterior é null quando não reservou (concorrência/idempotência).
/// </summary>
public sealed record ResultadoReservaEnvioSap(bool Reservado, string? StatusAnterior);
