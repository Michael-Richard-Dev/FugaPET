namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// GATE 126F: datas de um LOTE lidas do SAP (API_BATCH_SRV, colecao <c>Batch</c>), para a etiqueta do
/// palete.
/// <para>
/// As duas datas sao ANULAVEIS de proposito. O SAP devolve null em lote sem data cadastrada, e a
/// etiqueta precisa distinguir "nao informada" de uma data qualquer: data de validade errada numa
/// etiqueta de expedicao e pior que etiqueta sem data.
/// </para>
/// </summary>
public sealed record LoteSapDatas(
    string Material,
    string Lote,
    DateTime? DataFabricacao,
    DateTime? DataVencimento)
{
    /// <summary>Chave de comparacao do lote, sem espacos e sem diferenciar caixa.</summary>
    public static string NormalizarLote(string? lote)
        => (lote ?? string.Empty).Trim().ToUpperInvariant();
}
