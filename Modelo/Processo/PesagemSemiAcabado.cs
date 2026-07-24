namespace FugaPET_Dev.Modelo.Processo;

public sealed class PesagemSemiAcabado
{
    public long? CodigoSemiAcabadoPesagem { get; set; }
    public int Sequencia { get; init; }
    public string CodigoEtiqueta { get; init; } = string.Empty;
    public string StatusPesagem { get; set; } = PesagemSemiAcabadoCalculos.StatusValida;
    public decimal PesoBrutoKg { get; init; }
    public decimal PesoTaraKg { get; init; }
    public decimal PesoLiquidoKg { get; init; }
    public decimal SaldoAposPesagemKg { get; init; }
    public string Origem { get; init; } = "MANUAL";
    public string LeituraOriginal { get; init; } = string.Empty;
    public long? CodigoTara { get; init; }
    public DateTime RegistradoEm { get; init; } = DateTime.Now;
    public DateTime? CanceladoEm { get; set; }

    public bool Valida => PesagemSemiAcabadoCalculos.PesagemValida(this);
}
