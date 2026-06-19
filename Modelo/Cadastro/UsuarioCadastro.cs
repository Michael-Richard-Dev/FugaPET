namespace FugaPET_Dev.Modelo.Cadastro;

public sealed class UsuarioCadastro
{
    public long IdUsuario { get; set; }
    public long? IdCargo { get; set; }
    public long? IdSetorPadrao { get; set; }
    public string NomeUsuario { get; set; } = string.Empty;
    public string LoginUsuario { get; set; } = string.Empty;
    public string EmailUsuario { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string TelefoneUsuario { get; set; } = string.Empty;
    public bool DeveTrocarSenha { get; set; } = true;
    public bool BloqueadoUsuario { get; set; }
    public DateTimeOffset? UltimoLoginEm { get; set; }
    public bool SituacaoUsuario { get; set; } = true;

    // Aliases de compatibilidade para evitar quebra nas telas existentes.
    public long Id
    {
        get => IdUsuario;
        set => IdUsuario = value;
    }

    public string Nome
    {
        get => NomeUsuario;
        set => NomeUsuario = value;
    }

    public string Login
    {
        get => LoginUsuario;
        set => LoginUsuario = value;
    }

    public string Email
    {
        get => EmailUsuario;
        set => EmailUsuario = value;
    }

    public string Telefone
    {
        get => TelefoneUsuario;
        set => TelefoneUsuario = value;
    }

    public bool Bloqueado
    {
        get => BloqueadoUsuario;
        set => BloqueadoUsuario = value;
    }

    public bool Ativo
    {
        get => SituacaoUsuario;
        set => SituacaoUsuario = value;
    }
}
