using System.Collections.ObjectModel;

namespace FugaPET_Dev.Modelo.Entrada;

public sealed record ResultadoPersistenciaEntradaComLotes
{
    public long CodigoLancamento { get; init; }
    public bool PersistenciaRecuperada { get; init; }
    public IReadOnlyDictionary<string, long> CodigosItensPorNumeroItemSap { get; init; } =
        new ReadOnlyDictionary<string, long>(new Dictionary<string, long>(StringComparer.Ordinal));
    public IReadOnlyDictionary<Guid, long> CodigosLotesPorCodigoLocal { get; init; } =
        new ReadOnlyDictionary<Guid, long>(new Dictionary<Guid, long>());
    public IReadOnlyDictionary<Guid, long> CodigosPesagensPorCodigoLocal { get; init; } =
        new ReadOnlyDictionary<Guid, long>(new Dictionary<Guid, long>());
    public IReadOnlyDictionary<Guid, decimal> PesosConsolidadosPorLoteLocal { get; init; } =
        new ReadOnlyDictionary<Guid, decimal>(new Dictionary<Guid, decimal>());

    public static ResultadoPersistenciaEntradaComLotes Criar(
        long codigoLancamento,
        IDictionary<string, long> codigosItensPorNumeroItemSap,
        IDictionary<Guid, long> codigosLotesPorCodigoLocal,
        IDictionary<Guid, long> codigosPesagensPorCodigoLocal,
        IDictionary<Guid, decimal> pesosConsolidadosPorLoteLocal)
        => new()
        {
            CodigoLancamento = codigoLancamento,
            PersistenciaRecuperada = false,
            CodigosItensPorNumeroItemSap = CriarReadOnlyOrdinal(codigosItensPorNumeroItemSap),
            CodigosLotesPorCodigoLocal = CriarReadOnlyGuid(codigosLotesPorCodigoLocal),
            CodigosPesagensPorCodigoLocal = CriarReadOnlyGuid(codigosPesagensPorCodigoLocal),
            PesosConsolidadosPorLoteLocal = CriarReadOnlyGuidDecimal(pesosConsolidadosPorLoteLocal)
        };

    public static ResultadoPersistenciaEntradaComLotes CriarRecuperado(
        long codigoLancamento,
        IDictionary<string, long> codigosItensPorNumeroItemSap,
        IDictionary<Guid, long> codigosLotesPorCodigoLocal,
        IDictionary<Guid, long> codigosPesagensPorCodigoLocal,
        IDictionary<Guid, decimal> pesosConsolidadosPorLoteLocal)
        => Criar(
                codigoLancamento,
                codigosItensPorNumeroItemSap,
                codigosLotesPorCodigoLocal,
                codigosPesagensPorCodigoLocal,
                pesosConsolidadosPorLoteLocal)
            with
            {
                PersistenciaRecuperada = true
            };

    private static IReadOnlyDictionary<string, long> CriarReadOnlyOrdinal(IDictionary<string, long> origem)
    {
        Dictionary<string, long> destino = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, long> item in origem)
        {
            destino.Add(item.Key, item.Value);
        }

        return new ReadOnlyDictionary<string, long>(destino);
    }

    private static IReadOnlyDictionary<Guid, long> CriarReadOnlyGuid(IDictionary<Guid, long> origem)
        => new ReadOnlyDictionary<Guid, long>(new Dictionary<Guid, long>(origem));

    private static IReadOnlyDictionary<Guid, decimal> CriarReadOnlyGuidDecimal(IDictionary<Guid, decimal> origem)
        => new ReadOnlyDictionary<Guid, decimal>(new Dictionary<Guid, decimal>(origem));
}
