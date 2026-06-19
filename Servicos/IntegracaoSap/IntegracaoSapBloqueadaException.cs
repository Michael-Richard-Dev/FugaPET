namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class IntegracaoSapBloqueadaException : InvalidOperationException
{
    public IntegracaoSapBloqueadaException(string mensagem)
        : base(mensagem)
    {
    }
}
