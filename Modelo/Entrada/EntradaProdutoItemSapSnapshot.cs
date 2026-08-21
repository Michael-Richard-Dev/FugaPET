using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Modelo.Entrada;

public sealed record EntradaProdutoItemSapSnapshot
{
    private long _codigoSapPedidoCompraItem;
    private string _numeroItemSap = string.Empty;
    private string _material = string.Empty;
    private string _centro = string.Empty;
    private string _deposito = string.Empty;
    private string _unidade = string.Empty;

    public required long CodigoSapPedidoCompraItem
    {
        get => _codigoSapPedidoCompraItem;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(CodigoSapPedidoCompraItem), "Código local do item SAP é obrigatório.");
            }

            _codigoSapPedidoCompraItem = value;
        }
    }

    public required string NumeroItemSap
    {
        get => _numeroItemSap;
        init => _numeroItemSap = EntradaProdutoItemEmMemoria.NormalizarNumeroItemSap(value);
    }

    public required string Material
    {
        get => _material;
        init => _material = NormalizarObrigatorio(value, "Material do item SAP é obrigatório.");
    }

    public required string Centro
    {
        get => _centro;
        init => _centro = NormalizarObrigatorio(value, "Centro do item SAP é obrigatório.");
    }

    public required string Deposito
    {
        get => _deposito;
        init => _deposito = NormalizarObrigatorio(value, "Depósito do item SAP é obrigatório.");
    }

    public required string Unidade
    {
        get => _unidade;
        init => _unidade = NormalizarObrigatorio(value, "Unidade do item SAP é obrigatória.");
    }

    public decimal? QuantidadePrevista { get; init; }

    public static EntradaProdutoItemSapSnapshot Criar(PedidoCompraSapItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new EntradaProdutoItemSapSnapshot
        {
            CodigoSapPedidoCompraItem = item.CodigoItem,
            NumeroItemSap = item.NumeroItem,
            Material = item.CodigoMaterial ?? string.Empty,
            Centro = item.Centro ?? string.Empty,
            Deposito = item.Deposito ?? string.Empty,
            Unidade = item.UnidadeMedida ?? string.Empty,
            QuantidadePrevista = item.Quantidade
        };
    }

    private static string NormalizarObrigatorio(string? valor, string mensagem)
    {
        string normalizado = (valor ?? string.Empty).Trim();
        if (normalizado.Length == 0)
        {
            throw new ArgumentException(mensagem);
        }

        return normalizado;
    }
}
