namespace FugaPET_Dev.Tela.Comum;

public static class UsuarioLogadoUiHelper
{
    public static string ObterLogin()
    {
        string? login = global::FugaPET_Dev.Servicos.Seguranca.EstadoSessaoUsuarioAtual.SessaoAtual?.Login;
        return string.IsNullOrWhiteSpace(login) ? "N/A" : login.Trim();
    }

    public static string ObterTextoUsuarioRodape()
    {
        return $"Usuário:  {ObterLogin()}";
    }
}
