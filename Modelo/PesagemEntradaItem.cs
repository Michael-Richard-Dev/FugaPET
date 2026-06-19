namespace FugaPET_Dev.Modelo;

/// <summary>
/// Pesagem local (balanca ou manual) de um item de pedido de compra, capturada na
/// tela de Entrada de Produto e gravada em <c>homologacao.pesagem_entrada_item</c>.
/// </summary>
public sealed record PesagemEntradaItem
{
    /// <summary>FK do item do pedido (homologacao.sap_pedido_compra_item).</summary>
    public long CodigoSapPedidoCompraItem { get; init; }

    /// <summary>Peso lido/digitado, em quilogramas.</summary>
    public decimal PesoKg { get; init; }

    /// <summary>Origem do peso: LIDO (balanca), DIGITADO (manual) ou MULTIPLA (soma de leituras).</summary>
    public string OrigemPeso { get; init; } = "LIDO";
}
