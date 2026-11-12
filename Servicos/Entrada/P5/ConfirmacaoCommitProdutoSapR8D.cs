namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed class ConfirmacaoCommitProdutoSapR8D<T>(T recibo) where T : class
{
    internal T Recibo { get; } = recibo ?? throw new ArgumentNullException(nameof(recibo));
    private int _estado;
    internal EstadoCommitProdutoSapR8D Estado => (EstadoCommitProdutoSapR8D)Volatile.Read(ref _estado);

    internal async Task<EstadoCommitProdutoSapR8D> ConfirmarUmaVezAsync(Func<CancellationToken, Task> commit,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _estado, (int)EstadoCommitProdutoSapR8D.CommitIndeterminado,
            (int)EstadoCommitProdutoSapR8D.ResultadoDaFuncao) != (int)EstadoCommitProdutoSapR8D.ResultadoDaFuncao) return Estado;
        try
        {
            await commit(cancellationToken);
            Interlocked.Exchange(ref _estado, (int)EstadoCommitProdutoSapR8D.CommitConfirmado);
        }
        catch (Exception) { }
        return Estado;
    }

    public override string ToString() => "P5_R8D_ESTADO_COMMIT_SEM_AUTORIZACAO_DE_POST";
}
