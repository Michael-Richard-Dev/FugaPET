namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class ResultadoOperacao
{
    public bool Sucesso { get; private init; }
    public string Mensagem { get; private init; } = string.Empty;
    public long? IdGerado { get; private init; }

    public static ResultadoOperacao Ok(string mensagem = "Operacao realizada com sucesso.", long? idGerado = null)
        => new() { Sucesso = true, Mensagem = mensagem, IdGerado = idGerado };

    public static ResultadoOperacao Falha(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };
}
