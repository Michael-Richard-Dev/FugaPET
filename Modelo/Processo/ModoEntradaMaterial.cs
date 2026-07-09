namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tarefa Entrada 24.1: modo operacional da tela de Entrada (separação Matéria-Prima × Químicos).
/// Distinto de <see cref="ModoConsumoMaterial"/> para não misturar os conceitos de entrada e consumo.
/// </summary>
public enum ModoEntradaMaterial
{
    MateriaPrima = 1,
    Quimico = 2
}
