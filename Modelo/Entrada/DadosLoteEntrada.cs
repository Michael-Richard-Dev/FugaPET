namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>Dados funcionais informados pelo operador para identificar um lote de Entrada.</summary>
public sealed record DadosLoteEntrada
{
    private string _numeroLote = string.Empty;
    private DateTime _dataFabricacao;
    private DateTime _dataVencimento;

    public DadosLoteEntrada()
    {
    }

    public DadosLoteEntrada(string numeroLote, DateTime dataFabricacao, DateTime dataVencimento)
    {
        NumeroLote = numeroLote;
        DataFabricacao = dataFabricacao;
        DataVencimento = dataVencimento;
    }

    public string NumeroLote
    {
        get => _numeroLote;
        init => _numeroLote = NormalizarNumeroLote(value);
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

    public static string NormalizarNumeroLote(string? numeroLote)
        => (numeroLote ?? string.Empty).Trim().ToUpperInvariant();
}
