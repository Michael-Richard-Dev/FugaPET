namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Item de um lancamento de Entrada (desenvolvimento.entrada_produto_item) com suas pesagens.
/// </summary>
public sealed record EntradaProdutoItem
{
    /// <summary>FK opcional ao item do pedido SAP (desenvolvimento.sap_pedido_compra_item).</summary>
    public long? CodigoSapPedidoCompraItem { get; init; }

    public string NumeroItem { get; init; } = string.Empty;
    public string? Material { get; init; }
    public string? Centro { get; init; }
    public string? Deposito { get; init; }
    public string? Unidade { get; init; }
    public decimal? QuantidadePrevista { get; init; }
    public decimal? QuantidadeRecebida { get; init; }

    public IReadOnlyList<EntradaProdutoPesagem> Pesagens { get; init; } = [];
}
