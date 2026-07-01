namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Pesagem LOCAL (na tela) do consumo de um componente da OP. Tarefa 4: somente memoria — sem banco,
/// sem envio SAP, sem impressao. Status local <see cref="StatusRegistradaLocalmente"/>.
/// </summary>
public sealed class PesagemConsumoMaterial
{
    public const string OrigemBalanca = "BALANCA";
    public const string OrigemManual = "MANUAL";
    public const string StatusRegistradaLocalmente = "REGISTRADA_LOCALMENTE";

    public int Sequencia { get; set; }
    public string NumeroOrdem { get; set; } = string.Empty;
    public string CodigoMaterial { get; set; } = string.Empty;
    public string DescricaoMaterial { get; set; } = string.Empty;
    public string DepositoConsumo { get; set; } = string.Empty;
    public string NumeroReserva { get; set; } = string.Empty;
    public string ItemReserva { get; set; } = string.Empty;
    public string Lote { get; set; } = string.Empty;

    public decimal PesoBrutoKg { get; set; }
    public decimal PesoTaraKg { get; set; }
    public decimal PesoLiquidoKg { get; set; }
    public string UnidadeMedida { get; set; } = string.Empty;

    public DateTime PesadoEm { get; set; }

    /// <summary><see cref="OrigemBalanca"/> ou <see cref="OrigemManual"/>.</summary>
    public string Origem { get; set; } = OrigemBalanca;

    /// <summary>Status LOCAL (sempre <see cref="StatusRegistradaLocalmente"/> nesta tarefa).</summary>
    public string StatusLocal { get; set; } = StatusRegistradaLocalmente;
}
