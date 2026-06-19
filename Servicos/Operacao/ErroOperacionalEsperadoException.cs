namespace FugaPET_Dev.Servicos.Operacao;

public sealed class ErroOperacionalEsperadoException : Exception
{
    public ErroOperacionalEsperadoException(string mensagem)
        : base(mensagem)
    {
    }
}
