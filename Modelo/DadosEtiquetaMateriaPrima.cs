namespace FugaPET_Dev.Modelo;

public sealed class DadosEtiquetaMateriaPrima
{
    public string CodigoProduto { get; init; } = string.Empty;
    public string DescricaoProduto { get; init; } = string.Empty;
    public string LoteOrigem { get; init; } = string.Empty;
    public string LoteInterno { get; init; } = string.Empty;
    public string DataFabricacao { get; init; } = string.Empty;
    public string DataVencimento { get; init; } = string.Empty;
    public string CertificadoSanitario { get; init; } = string.Empty;
    public string Sif { get; init; } = string.Empty;
    public string Fornecedor { get; init; } = string.Empty;
    public string NumeroNotaFiscal { get; init; } = string.Empty;
    public string Peso { get; init; } = string.Empty;
    public string NumeroPedido { get; init; } = string.Empty;
    public string NumeroItem { get; init; } = string.Empty;

    public string ConteudoQrCode => string.Join("|", new[]
    {
        $"PEDIDO:{NumeroPedido}",
        $"ITEM:{NumeroItem}",
        $"MATERIAL:{CodigoProduto}",
        $"LOTE_ORIGEM:{LoteOrigem}",
        $"LOTE_INTERNO:{LoteInterno}",
        $"FORNECEDOR:{Fornecedor}",
        $"PESO:{Peso}"
    });
}
