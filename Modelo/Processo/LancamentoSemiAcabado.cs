namespace FugaPET_Dev.Modelo.Processo;

public sealed class LancamentoSemiAcabado
{
    public long? CodigoSemiAcabadoLancamento { get; set; }
    public SemiAcabadoOrdem Ordem { get; init; } = new();
    public IReadOnlyList<PesagemSemiAcabado> Pesagens { get; init; } = [];
    public decimal PesoLiquidoTotalKg => PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(Pesagens);
    public string StatusLancamento { get; set; } = "FINALIZADO_LOCAL";
    public string? MaterialDocument { get; set; }
    public string? MaterialDocumentYear { get; set; }
    public DateTime? EnviadoSapEm { get; set; }
    public string Usuario { get; init; } = string.Empty;
    public DateTime CriadoEm { get; init; } = DateTime.Now;
}

/// <summary>Cabeçalho persistido de um lançamento (para reabrir histórico/reimpressão sem recalcular etiqueta).</summary>
public sealed class LancamentoSemiAcabadoPersistido
{
    public long CodigoSemiAcabadoLancamento { get; init; }
    public string StatusLancamento { get; init; } = string.Empty;
    public string? MaterialDocument { get; init; }
    public string? MaterialDocumentYear { get; init; }
}
