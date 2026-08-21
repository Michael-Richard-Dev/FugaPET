namespace FugaPET_Dev.Servicos.Operacao;

public sealed class ErroOperacionalEsperadoException : Exception
{
    public ErroOperacionalEsperadoException(string mensagem)
        : base(mensagem)
    {
    }

    /// <summary>
    /// Preserva a exceção de origem (ex.: ConflitoPersistenciaEntradaLotesException) como InnerException,
    /// sem expô-la ao operador — a mensagem pública continua sendo a segura.
    /// </summary>
    public ErroOperacionalEsperadoException(string mensagem, Exception innerException)
        : base(mensagem, innerException)
    {
    }
}
