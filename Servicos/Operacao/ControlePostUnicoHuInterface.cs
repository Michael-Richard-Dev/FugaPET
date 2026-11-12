namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Trava de processo contra duplo clique. Não possui reset deliberadamente.</summary>
public sealed class ControlePostUnicoHuInterface
{
    private int _iniciado;

    public bool Iniciado => Volatile.Read(ref _iniciado) == 1;

    public bool TentarIniciar()
        => Interlocked.CompareExchange(ref _iniciado, 1, 0) == 0;
}
