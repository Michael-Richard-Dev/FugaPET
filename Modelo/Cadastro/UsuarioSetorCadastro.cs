namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class UsuarioSetorCadastro
{
    public long IdUsuarioSetor { get; set; }
    public long IdUsuario { get; set; }
    public long IdSetor { get; set; }
    public bool SetorPadrao { get; set; }
    public bool SituacaoUsuarioSetor { get; set; } = true;

    public long Id
    {
        get => IdUsuarioSetor;
        set => IdUsuarioSetor = value;
    }

    public bool Ativo
    {
        get => SituacaoUsuarioSetor;
        set => SituacaoUsuarioSetor = value;
    }
}
