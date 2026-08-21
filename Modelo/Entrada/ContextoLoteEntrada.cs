using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Modelo.Entrada;

public sealed record ContextoLoteEntrada
{
    public string NumeroPedido { get; init; } = string.Empty;
    public string NumeroItemSap { get; init; } = string.Empty;
    public ModoEntradaMaterial Modo { get; init; } = ModoEntradaMaterial.MateriaPrima;
    public EntradaProdutoLoteEmMemoria? LoteAtivo { get; init; }
    public IReadOnlyList<EntradaProdutoLoteEmMemoria> LotesDoItem { get; init; } = [];
}
