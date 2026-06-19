namespace FugaPET_Dev.Servicos.IntegracaoSap;

internal sealed class SincronizacaoSapIncompletaException : InvalidOperationException
{
    public SincronizacaoSapIncompletaException(
        int paginasProcessadas,
        int registrosProcessados,
        int itensProcessados)
        : base("A sincronizacao SAP foi interrompida antes de receber todas as paginas.")
    {
        PaginasProcessadas = paginasProcessadas;
        RegistrosProcessados = registrosProcessados;
        ItensProcessados = itensProcessados;
    }

    public int PaginasProcessadas { get; }
    public int RegistrosProcessados { get; }
    public int ItensProcessados { get; }
}
