namespace FugaPET_Dev.Modelo;

/// <summary>
/// LEGADO — NAO USAR PARA NOVA ENTRADA DE PRODUTO. Modelo do fluxo antigo (pesagem_entrada_item,
/// uma pesagem por item, sobrescreve). Usar Modelo.Entrada.EntradaProdutoPesagem com
/// EntradaProdutoServico + EntradaProdutoRepositorio.
/// </summary>
[Obsolete("Modelo do fluxo legado de pesagem. Nao usar para nova Entrada de Produto. " +
    "Usar EntradaProdutoServico + EntradaProdutoRepositorio.")]
public sealed record PesagemEntradaItem
{
    /// <summary>FK do item do pedido (desenvolvimento.sap_pedido_compra_item).</summary>
    public long CodigoSapPedidoCompraItem { get; init; }

    /// <summary>Peso lido/digitado, em quilogramas.</summary>
    public decimal PesoKg { get; init; }

    /// <summary>Origem do peso: LIDO (balanca), DIGITADO (manual) ou MULTIPLA (soma de leituras).</summary>
    public string OrigemPeso { get; init; } = "LIDO";
}
