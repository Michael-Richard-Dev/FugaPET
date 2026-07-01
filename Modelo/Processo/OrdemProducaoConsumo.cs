namespace FugaPET_Dev.Modelo.Processo;

public sealed class OrdemProducaoConsumo
{
    public string NumeroOrdem { get; set; } = string.Empty;
    public string TipoOrdem { get; set; } = string.Empty;
    public string MaterialProduzido { get; set; } = string.Empty;
    public string Planta { get; set; } = string.Empty;
    public decimal QuantidadePrevista { get; set; }
    public string Unidade { get; set; } = string.Empty;
    public string ItemOrdem { get; set; } = string.Empty;
    public string DepositoConsumo { get; set; } = string.Empty;
    public string LoteProdutoProduzido { get; set; } = string.Empty;
    public string Lote { get; set; } = string.Empty;

    /// <summary>Data da OP (do SAP). Null quando o SAP nao retornou data — exibe "--/--/----".</summary>
    public DateTime? DataOrdem { get; set; }

    /// <summary>Campo SAP de origem da <see cref="DataOrdem"/> (diagnostico). Vazio quando ausente.</summary>
    public string OrigemDataOrdem { get; set; } = string.Empty;

    /// <summary>True quando a OP esta liberada e nao excluida (apta a consumo).</summary>
    public bool Liberada { get; set; }

    public IReadOnlyList<ComponenteConsumoMaterial> Componentes { get; set; } = [];
    public IReadOnlyList<OperacaoOrdemConsumo> Operacoes { get; set; } = [];
}
