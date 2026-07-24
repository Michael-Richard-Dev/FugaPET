namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Registro local do resultado de apontamento devolvido pela tela operacional ao controle de apontamentos.
/// </summary>
public sealed class RegistroResultadoApontamento
{
    public long CodigoApontamento { get; init; }
    public string NumeroOrdem { get; init; } = string.Empty;
    public string Sequencia { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string Suboperacao { get; init; } = string.Empty;
    public IReadOnlyList<ResultadoApontamentoItem> Itens { get; init; } = Array.Empty<ResultadoApontamentoItem>();
    public string Usuario { get; init; } = string.Empty;
    public string Estacao { get; init; } = string.Empty;
    public DateTime RegistradoEm { get; init; }
    public string MensagemResumo { get; init; } = string.Empty;
}
