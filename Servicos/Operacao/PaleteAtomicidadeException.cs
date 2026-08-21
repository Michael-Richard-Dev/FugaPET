namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>
/// GATE 046-E §3: sinaliza, DENTRO da transação de criação do palete, que uma chamada retornou <c>false</c>
/// (criação negada ou vínculo negado). Ao propagar, força o ROLLBACK integral do escopo transacional
/// (<c>ExecutarEmTransacaoAsync</c>). Convertida em resultado fail-closed pelo store — nunca vaza para a UI.
/// </summary>
public sealed class PaleteAtomicidadeException : Exception
{
    public PaleteAtomicidadeException(string mensagem) : base(mensagem)
    {
    }
}
