namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tarefa Entrada 24.1 (Ajuste 2): configuração visual/operacional da tela de Entrada por modo
/// (Matéria-Prima × Químicos), espelhando <see cref="ConfiguracaoTelaConsumoMaterial"/>.
/// </summary>
public sealed class ConfiguracaoTelaEntradaMaterial
{
    public ModoEntradaMaterial Modo { get; init; }
    public string TituloTela { get; init; } = string.Empty;
    public string SubtituloTela { get; init; } = string.Empty;
    public string NomeModulo { get; init; } = string.Empty;
    public string TipoBalancaPreferencial { get; init; } = string.Empty;
    public bool UsarFiltroQuimicos { get; init; }
}
