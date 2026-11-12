namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Definição/valor de um resultado operacional coletado em uma operação intermediária.
/// Mantém a tela desacoplada de consumo, pesagem, SAP e estrutura definitiva de banco.
/// </summary>
public sealed class ResultadoApontamentoItem
{
    /// <summary>
    /// GATE 050 (GAIA 041) — ID REAL da definição (operacao_resultado_definicao.codigo_definicao) que
    /// originou este item. Propagado da definição carregada até a persistência; snapshot histórico no item.
    /// </summary>
    public long CodigoDefinicao { get; init; }

    public string CodigoItem { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public string Medida { get; init; } = string.Empty;
    public decimal Referencia { get; init; }
    public decimal? Resultado { get; set; }
    public int OrdemExibicao { get; init; }
    public bool Obrigatorio { get; init; } = true;
}
