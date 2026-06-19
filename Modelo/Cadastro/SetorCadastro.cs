namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class SetorCadastro
{
    public long CodigoSetor { get; set; }
    public string NomeSetor { get; set; } = string.Empty;
    public string DescricaoSetor { get; set; } = string.Empty;
    public bool SituacaoSetor { get; set; } = true;
    public DateTime? SetorCriadoEm { get; set; }
    public long? SetorCriadoPor { get; set; }
    public long? SetorAtualizadoPor { get; set; }

    // Compatibilidade transitória com chamadas legadas da aplicação.
    public long Codigo
    {
        get => CodigoSetor;
        set => CodigoSetor = value;
    }

    public string Nome
    {
        get => NomeSetor;
        set => NomeSetor = value;
    }

    public string Descricao
    {
        get => DescricaoSetor;
        set => DescricaoSetor = value;
    }

    public bool Ativo
    {
        get => SituacaoSetor;
        set => SituacaoSetor = value;
    }
}
