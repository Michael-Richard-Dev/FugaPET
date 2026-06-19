namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class CargoCadastro
{
    public long IdCargo { get; set; }
    public string NomeCargo { get; set; } = string.Empty;
    public string DescricaoCargo { get; set; } = string.Empty;
    public bool SituacaoCargo { get; set; } = true;
    public DateTime? CargoCriadoEm { get; set; }
    public long? CargoCriadoPor { get; set; }
    public long? CargoAtualizadoPor { get; set; }

    // Compatibilidade transitória com chamadas legadas da aplicação.
    public long Id
    {
        get => IdCargo;
        set => IdCargo = value;
    }

    public string Nome
    {
        get => NomeCargo;
        set => NomeCargo = value;
    }

    public string Descricao
    {
        get => DescricaoCargo;
        set => DescricaoCargo = value;
    }

    public bool Ativo
    {
        get => SituacaoCargo;
        set => SituacaoCargo = value;
    }
}
