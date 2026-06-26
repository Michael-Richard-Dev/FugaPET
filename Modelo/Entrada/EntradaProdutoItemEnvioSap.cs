namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Item de um lancamento ja persistido, preparado para o envio CONTROLADO da Entrada ao SAP
/// (criacao de documento de material 101, separado da finalizacao local). Pesos consolidados das
/// pesagens VALIDAS, em kg. Material/Centro/Deposito/Unidade vem do banco local (entrada_produto_item),
/// gravados na finalizacao a partir do item do pedido/cache — nunca valor fixo nem fallback.
/// </summary>
public sealed record EntradaProdutoItemEnvioSap
{
    public string NumeroPedido { get; init; } = string.Empty;
    public string NumeroItem { get; init; } = string.Empty;
    public decimal PesoLiquidoKg { get; init; }
    public decimal PesoBrutoKg { get; init; }

    /// <summary>Material do item (entrada_produto_item.material).</summary>
    public string? Material { get; init; }

    /// <summary>Centro / Plant do item (entrada_produto_item.centro).</summary>
    public string? Centro { get; init; }

    /// <summary>Deposito / StorageLocation do item (entrada_produto_item.deposito).</summary>
    public string? Deposito { get; init; }

    /// <summary>Unidade de medida do item / EntryUnit (entrada_produto_item.unidade).</summary>
    public string? Unidade { get; init; }
}
