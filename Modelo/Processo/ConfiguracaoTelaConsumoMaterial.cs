namespace FugaPET_Dev.Modelo.Processo;

public sealed class ConfiguracaoTelaConsumoMaterial
{
    public ModoConsumoMaterial Modo { get; init; }
    public string TituloTela { get; init; } = string.Empty;
    public string SubtituloTela { get; init; } = string.Empty;
    public string NomeModulo { get; init; } = string.Empty;
    public string TipoBalancaPreferencial { get; init; } = string.Empty;
    public string TextoSemBalancaConfigurada { get; init; } = string.Empty;
    public bool UsarFiltroQuimicos { get; init; }
}
