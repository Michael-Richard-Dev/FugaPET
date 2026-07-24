namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Definição/valor de um resultado operacional coletado em uma operação intermediária.
/// Mantém a tela desacoplada de consumo, pesagem, SAP e estrutura definitiva de banco.
/// </summary>
public sealed class ResultadoApontamentoItem
{
    public string CodigoItem { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public string Medida { get; init; } = string.Empty;
    public decimal Referencia { get; init; }
    public decimal? Resultado { get; set; }
    public int OrdemExibicao { get; init; }
    public bool Obrigatorio { get; init; } = true;
}
