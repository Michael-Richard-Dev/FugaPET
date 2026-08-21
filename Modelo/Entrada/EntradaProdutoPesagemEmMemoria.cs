namespace FugaPET_Dev.Modelo.Entrada;

public sealed record EntradaProdutoPesagemEmMemoria
{
    private Guid _codigoLocalPesagem;
    private EntradaProdutoPesagem _pesagem = new();

    public required Guid CodigoLocalPesagem
    {
        get => _codigoLocalPesagem;
        init
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("Identificador local da pesagem não pode ser vazio.", nameof(CodigoLocalPesagem));
            }

            _codigoLocalPesagem = value;
        }
    }

    public required EntradaProdutoPesagem Pesagem
    {
        get => _pesagem;
        init => _pesagem = value ?? throw new ArgumentNullException(nameof(Pesagem));
    }

    public static EntradaProdutoPesagemEmMemoria Criar(EntradaProdutoPesagem pesagem)
    {
        ArgumentNullException.ThrowIfNull(pesagem);

        return new EntradaProdutoPesagemEmMemoria
        {
            CodigoLocalPesagem = Guid.NewGuid(),
            Pesagem = pesagem with { }
        };
    }

    public EntradaProdutoPesagemEmMemoria ComStatus(string status)
        => this with { Pesagem = Pesagem with { StatusPesagem = status } };
}
