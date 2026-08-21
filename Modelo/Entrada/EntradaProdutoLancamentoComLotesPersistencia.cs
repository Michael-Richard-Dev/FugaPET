namespace FugaPET_Dev.Modelo.Entrada;

public sealed record EntradaProdutoLancamentoComLotesPersistencia
{
    public EntradaProdutoLancamento Lancamento { get; init; } = new();
    public IReadOnlyList<EntradaProdutoItemComLotesPersistencia> Itens { get; init; } = [];
}

public sealed record EntradaProdutoItemComLotesPersistencia
{
    public EntradaProdutoItem Item { get; init; } = new();
    public string NumeroItemSap { get; init; } = string.Empty;
    public IReadOnlyList<EntradaProdutoLoteComPesagensPersistencia> Lotes { get; init; } = [];
}

public sealed record EntradaProdutoLoteComPesagensPersistencia
{
    private Guid _codigoLocal;
    private Guid _correlationId;

    public required Guid CodigoLocal
    {
        get => _codigoLocal;
        init => _codigoLocal = ValidarGuid(value, nameof(CodigoLocal));
    }

    public DadosLoteEntrada Dados { get; init; } = new();

    public required Guid CorrelationId
    {
        get => _correlationId;
        init => _correlationId = ValidarGuid(value, nameof(CorrelationId));
    }

    public EstadoOperacionalLoteEntrada EstadoOperacional { get; init; }

    public IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia> Pesagens { get; init; } = [];

    public static EntradaProdutoLoteComPesagensPersistencia CriarDeLoteFinalizado(EntradaProdutoLoteEmMemoria lote)
    {
        ArgumentNullException.ThrowIfNull(lote);

        IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia> pesagens = lote.PesagensComCodigoLocal
            .Select(pesagem => new EntradaProdutoPesagemComCodigoLocalPersistencia
            {
                CodigoLocalPesagem = pesagem.CodigoLocalPesagem,
                Pesagem = pesagem.Pesagem with { }
            })
            .ToList();

        return CriarDeLoteFinalizado(lote, pesagens);
    }

    public static EntradaProdutoLoteComPesagensPersistencia CriarDeLoteFinalizado(
        EntradaProdutoLoteEmMemoria lote,
        IReadOnlyList<EntradaProdutoPesagemComCodigoLocalPersistencia> pesagens)
    {
        ArgumentNullException.ThrowIfNull(lote);
        ArgumentNullException.ThrowIfNull(pesagens);

        if (lote.Estado != EstadoOperacionalLoteEntrada.FinalizadoEmMemoria)
        {
            throw new InvalidOperationException("Lote deve estar finalizado em memória antes de criar o DTO de persistência.");
        }

        return new EntradaProdutoLoteComPesagensPersistencia
        {
            CodigoLocal = lote.CodigoLocal,
            Dados = new DadosLoteEntrada(lote.Dados.NumeroLote, lote.Dados.DataFabricacao, lote.Dados.DataVencimento),
            CorrelationId = lote.CorrelationId,
            EstadoOperacional = lote.Estado,
            Pesagens = pesagens.Select(pesagem => pesagem with { Pesagem = pesagem.Pesagem with { } }).ToList()
        };
    }

    private static Guid ValidarGuid(Guid valor, string nomeParametro)
    {
        if (valor == Guid.Empty)
        {
            throw new ArgumentException("Identificador local não pode ser vazio.", nomeParametro);
        }

        return valor;
    }
}

public sealed record EntradaProdutoPesagemComCodigoLocalPersistencia
{
    private Guid _codigoLocalPesagem;

    public required Guid CodigoLocalPesagem
    {
        get => _codigoLocalPesagem;
        init => _codigoLocalPesagem = ValidarGuid(value, nameof(CodigoLocalPesagem));
    }

    public EntradaProdutoPesagem Pesagem { get; init; } = new();

    private static Guid ValidarGuid(Guid valor, string nomeParametro)
    {
        if (valor == Guid.Empty)
        {
            throw new ArgumentException("Identificador local da pesagem não pode ser vazio.", nomeParametro);
        }

        return valor;
    }
}
