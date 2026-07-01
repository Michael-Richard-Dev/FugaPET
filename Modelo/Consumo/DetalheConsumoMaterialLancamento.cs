namespace FugaPET_Dev.Modelo.Consumo;

public sealed class DetalheConsumoMaterialLancamento
{
    public ConsumoMaterialLancamento Lancamento { get; init; } = new();
    public IReadOnlyList<ConsumoMaterialItem> Itens { get; init; } = [];
    public IReadOnlyList<ConsumoMaterialPesagem> Pesagens { get; init; } = [];
}
