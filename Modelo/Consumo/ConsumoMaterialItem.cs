namespace FugaPET_Dev.Modelo.Consumo;

/// <summary>Componente consumido dentro da OP (tabela consumo_material_item).</summary>
public sealed class ConsumoMaterialItem
{
    public const string StatusPendenteSap = "PENDENTE_SAP";
    public const string TipoMovimentoConsumo = "261";

    public long Codigo { get; set; }
    public string NumeroOrdem { get; set; } = string.Empty;
    public string CodigoMaterial { get; set; } = string.Empty;
    public string? DescricaoMaterial { get; set; }
    public string? Centro { get; set; }
    public string? DepositoConsumo { get; set; }
    public string? NumeroReserva { get; set; }
    public string? ItemReserva { get; set; }
    public string? Lote { get; set; }
    public decimal? QuantidadePrevista { get; set; }
    public decimal? QuantidadeRetiradaSap { get; set; }
    public decimal? QuantidadePendenteSap { get; set; }
    public decimal QuantidadeConsumidaLocal { get; set; }
    public string Unidade { get; set; } = string.Empty;
    public string TipoMovimentoSap { get; set; } = TipoMovimentoConsumo;
    public string StatusItem { get; set; } = StatusPendenteSap;

    public IReadOnlyList<ConsumoMaterialPesagem> Pesagens { get; set; } = [];
}
