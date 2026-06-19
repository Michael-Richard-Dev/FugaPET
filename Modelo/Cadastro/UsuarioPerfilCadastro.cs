namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class UsuarioPerfilCadastro
{
    public long IdUsuarioPerfil { get; set; }
    public long IdUsuario { get; set; }
    public long IdPerfilAcesso { get; set; }
    public bool SituacaoUsuarioPerfil { get; set; } = true;

    public long Id
    {
        get => IdUsuarioPerfil;
        set => IdUsuarioPerfil = value;
    }

    public bool Ativo
    {
        get => SituacaoUsuarioPerfil;
        set => SituacaoUsuarioPerfil = value;
    }
}
