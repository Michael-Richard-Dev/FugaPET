namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Item de um lancamento ja persistido, preparado para o envio CONTROLADO de peso ao SAP
/// (PATCH separado da finalizacao local). Pesos consolidados das pesagens VALIDAS, em kg.
/// </summary>
public sealed record EntradaProdutoItemEnvioSap
{
    public string NumeroPedido { get; init; } = string.Empty;
    public string NumeroItem { get; init; } = string.Empty;
    public decimal PesoLiquidoKg { get; init; }
    public decimal PesoBrutoKg { get; init; }
}
