namespace FugaPET_Dev.Servicos.Seguranca;

public sealed class SessaoUsuarioAplicacao
{
    public long? IdUsuario { get; init; }
    public string Login { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
    public long? IdSetorPadrao { get; init; }
    public IReadOnlyList<string> PerfisCodigo { get; init; } = [];
    public IReadOnlyList<PermissaoSessaoAplicacao> Permissoes { get; init; } = [];
    public bool IntegracaoBancoHabilitada { get; init; }
    public bool DeveTrocarSenha { get; init; }
}
