namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Uma leitura/pesagem de um item do lancamento (desenvolvimento.entrada_produto_pesagem).
/// Cada leitura e preservada; pesos em quilogramas.
/// </summary>
public sealed record EntradaProdutoPesagem
{
    /// <summary>PK persistida (desenvolvimento.entrada_produto_pesagem); null quando ainda não gravada.</summary>
    public long? CodigoEntradaProdutoPesagem { get; init; }

    public int Sequencia { get; init; }
    public decimal PesoBrutoKg { get; init; }
    public decimal PesoTaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public long? CodigoTara { get; init; }
    public long? CodigoBalanca { get; init; }

    /// <summary>BALANCA ou MANUAL.</summary>
    public string Origem { get; init; } = "BALANCA";

    /// <summary>VALIDA, CANCELADA ou ESTORNADA.</summary>
    public string StatusPesagem { get; init; } = "VALIDA";

    public string? LeituraOriginal { get; init; }
    public string? PayloadBalanca { get; init; }
    public DateTimeOffset PesadoEm { get; init; } = DateTimeOffset.Now;
}
