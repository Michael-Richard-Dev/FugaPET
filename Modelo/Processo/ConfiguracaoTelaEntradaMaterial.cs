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

    /// <summary>GATE 073: placeholder do campo de pesquisa de itens do pedido, por modo.</summary>
    public string PlaceholderPesquisa { get; init; } = "Pesquisar itens...";

    /// <summary>GATE 073: instrução de lote exibida antes da pesagem, por modo.</summary>
    public string MensagemLote { get; init; } = "Informe o lote antes da pesagem.";
}
