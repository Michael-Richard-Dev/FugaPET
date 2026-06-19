namespace FugaPET_Dev.Servicos.Seguranca;

public sealed class ResultadoAutenticacao
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public SessaoUsuarioAplicacao? Sessao { get; init; }

    public static ResultadoAutenticacao Ok(SessaoUsuarioAplicacao sessao)
        => new() { Sucesso = true, Sessao = sessao };

    public static ResultadoAutenticacao Falha(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };
}
