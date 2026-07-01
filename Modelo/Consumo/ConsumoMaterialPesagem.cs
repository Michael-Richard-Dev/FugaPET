namespace FugaPET_Dev.Modelo.Consumo;

/// <summary>Detalhe de uma pesagem do componente (tabela consumo_material_pesagem).</summary>
public sealed class ConsumoMaterialPesagem
{
    public const string StatusRegistradaLocalmente = "REGISTRADA_LOCALMENTE";

    public long Codigo { get; set; }
    public int Sequencia { get; set; }
    public decimal PesoBrutoKg { get; set; }
    public decimal PesoTaraKg { get; set; }
    public decimal PesoLiquidoKg { get; set; }
    public string Unidade { get; set; } = "KG";

    /// <summary>BALANCA ou MANUAL.</summary>
    public string Origem { get; set; } = string.Empty;
    public string StatusPesagem { get; set; } = StatusRegistradaLocalmente;
    public DateTime PesadoEm { get; set; }
    public string? UsuarioCriacao { get; set; }
}
