namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Uma leitura/pesagem de um item do lancamento (homologacao.entrada_produto_pesagem).
/// Cada leitura e preservada; pesos em quilogramas.
/// </summary>
public sealed record EntradaProdutoPesagem
{
    public int Sequencia { get; init; }
    public decimal PesoBrutoKg { get; init; }
    public decimal PesoTaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public long? CodigoTara { get; init; }
    public long? CodigoBalanca { get; init; }

    /// <summary>BALANCA ou MANUAL.</summary>
    public string Origem { get; init; } = "BALANCA";
}
