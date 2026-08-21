using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Modelo.Entrada;

public sealed record ContextoOperacaoEntradaProdutoLotes
{
    private string _numeroPedido = string.Empty;
    private string _terminal = string.Empty;
    private long _codigoSetor;
    private ModoEntradaMaterial _modoEntradaMaterial = ModoEntradaMaterial.MateriaPrima;

    public required string NumeroPedido
    {
        get => _numeroPedido;
        init => _numeroPedido = NormalizarObrigatorio(value, "Número do pedido é obrigatório para iniciar a operação por lotes.");
    }

    public string? Fornecedor { get; init; }

    public required long CodigoSetor
    {
        get => _codigoSetor;
        init
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(CodigoSetor), "Setor é obrigatório para iniciar a operação por lotes.");
            }

            _codigoSetor = value;
        }
    }

    public required string Terminal
    {
        get => _terminal;
        init => _terminal = NormalizarObrigatorio(value, "Terminal é obrigatório para iniciar a operação por lotes.");
    }

    public required ModoEntradaMaterial ModoEntradaMaterial
    {
        get => _modoEntradaMaterial;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(ModoEntradaMaterial), "Modo de entrada inválido.");
            }

            _modoEntradaMaterial = value;
        }
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
