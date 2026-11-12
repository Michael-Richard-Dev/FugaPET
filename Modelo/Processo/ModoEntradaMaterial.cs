namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tarefa Entrada 24.1: modo operacional da tela de Entrada (separação Matéria-Prima × Químicos).
/// Distinto de <see cref="ModoConsumoMaterial"/> para não misturar os conceitos de entrada e consumo.
/// </summary>
public enum ModoEntradaMaterial
{
    MateriaPrima = 1,
    Quimico = 2,

    /// <summary>
    /// GATE 073 — porta única "Recebimento de Mercadoria": aceita simultaneamente ROH (MatériaPrima) e
    /// HIBE (Químico) no mesmo pedido. MateriaPrima/Quimico permanecem para compatibilidade/testes.
    /// </summary>
    RecebimentoMercadoria = 3
}
