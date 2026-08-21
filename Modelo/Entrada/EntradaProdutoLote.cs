namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Dados persistentes de desenvolvimento.entrada_produto_lote, conforme incremental 043.
/// Não contém regra de banco; apenas normaliza dados civis do lote para uso seguro na aplicação.
/// </summary>
public sealed class EntradaProdutoLote
{
    private string _numeroLote = string.Empty;
    private DateTime _dataFabricacao;
    private DateTime _dataVencimento;

    public long? CodigoEntradaProdutoLote { get; init; }
    public long CodigoEntradaProdutoItem { get; init; }

    public string NumeroLote
    {
        get => _numeroLote;
        init => _numeroLote = DadosLoteEntrada.NormalizarNumeroLote(value);
    }

    public DateTime DataFabricacao
    {
        get => _dataFabricacao;
        init => _dataFabricacao = value.Date;
    }

    public DateTime DataVencimento
    {
        get => _dataVencimento;
        init => _dataVencimento = value.Date;
    }

    public decimal PesoLiquidoTotalKg { get; init; }
    public string StatusLote { get; init; } = StatusLoteEntrada.FinalizadoLocal;
    public Guid CorrelationId { get; init; }
    public string? DocumentoMaterialSap { get; init; }
    public string? ExercicioDocumentoMaterialSap { get; init; }
    public string? DocumentoMaterialItem { get; init; }
    public string? BatchRetornadoSap { get; init; }
    public decimal? QuantidadeRetornoSap { get; init; }
    public string? PayloadEnviadoJson { get; init; }
    public string? RespostaSapJson { get; init; }
    public string? ErroSanitizado { get; init; }
    public DateTimeOffset CriadoEm { get; init; }
    public string CriadoPor { get; init; } = string.Empty;
    public DateTimeOffset? ConfirmadoEm { get; init; }
    public DateTimeOffset? EnviadoSapEm { get; init; }
    public DateTimeOffset? ConfirmadoSapEm { get; init; }
    public DateTimeOffset? AlteradoEm { get; init; }
    public string? AlteradoPor { get; init; }
}
