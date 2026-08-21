namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Posição de um lancamento ja persistido, preparada para o envio CONTROLADO da Entrada ao SAP
/// (criacao de documento de material 101, separado da finalizacao local). Cada instancia representa
/// UM LOTE local (item × lote): o SAP recebe uma posicao por lote, com Batch/ManufactureDate/
/// ShelfLifeExpirationDate proprios — lotes diferentes NUNCA sao agregados numa unica posicao.
/// Pesos consolidados das pesagens VALIDAS do lote, em kg. Material/Centro/Deposito vem do banco
/// local (entrada_produto_item); numero do lote e datas vem de entrada_produto_lote (pacote 043) —
/// nunca valor fixo, fallback ou calculo por atalho. No envio SAP 101 da balanca, EntryUnit e
/// QuantityInEntryUnit sao sempre KG/peso liquido.
/// </summary>
public sealed record EntradaProdutoItemEnvioSap
{
    /// <summary>PK do lote local (entrada_produto_lote); 0 quando o item não tem lote persistido.
    /// Preservado para não se perder qual lote local compõe a posição SAP (inclusive na consolidação
    /// de material NÃO administrado por lote).</summary>
    public long CodigoEntradaProdutoLote { get; init; }

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

    /// <summary>Unidade de medida original do pedido (entrada_produto_item.unidade), apenas informativa no envio 101.</summary>
    public string? Unidade { get; init; }

    /// <summary>Batch — numero do lote local (entrada_produto_lote.numero_lote). Uma posicao SAP por lote.</summary>
    public string? NumeroLote { get; init; }

    /// <summary>ManufactureDate — data civil de fabricacao do lote (entrada_produto_lote.data_fabricacao).</summary>
    public DateTime? DataFabricacao { get; init; }

    /// <summary>ShelfLifeExpirationDate — data civil de validade do lote (entrada_produto_lote.data_vencimento).</summary>
    public DateTime? DataValidade { get; init; }
}
