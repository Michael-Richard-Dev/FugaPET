namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Evento lido no código de barras da operação da OP. Cada operação impressa possui um código de
/// início e um de término; o sufixo do código define qual dos dois foi lido.
/// </summary>
public enum TipoEventoOperacao
{
    Inicio = 1,
    Termino = 2
}
