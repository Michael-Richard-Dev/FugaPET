namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class PerfilAcessoCadastro
{
    public long IdPerfilAcesso { get; set; }
    public string NomePerfilAcesso { get; set; } = string.Empty;
    public string DescricaoPerfilAcesso { get; set; } = string.Empty;
    public bool PerfilSistema { get; set; }
    public bool SituacaoPerfilAcesso { get; set; } = true;
    public DateTime? PerfilAcessoCriadoEm { get; set; }
    public long? PerfilAcessoCriadoPor { get; set; }
    public long? PerfilAcessoAtualizadoPor { get; set; }

    // Compatibilidade transitória com chamadas legadas da aplicação.
    public long Id
    {
        get => IdPerfilAcesso;
        set => IdPerfilAcesso = value;
    }

    public string Nome
    {
        get => NomePerfilAcesso;
        set => NomePerfilAcesso = value;
    }

    public string Descricao
    {
        get => DescricaoPerfilAcesso;
        set => DescricaoPerfilAcesso = value;
    }

    public bool Ativo
    {
        get => SituacaoPerfilAcesso;
        set => SituacaoPerfilAcesso = value;
    }

    // Como o novo schema não possui coluna de código, mantemos um código derivado do nome.
    public string Codigo
    {
        get => string.IsNullOrWhiteSpace(NomePerfilAcesso)
            ? string.Empty
            : NomePerfilAcesso.Trim().ToUpperInvariant().Replace(" ", "_");
        set { }
    }
}
