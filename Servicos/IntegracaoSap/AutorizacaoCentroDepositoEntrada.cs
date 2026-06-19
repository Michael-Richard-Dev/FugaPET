namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Regra CONFIGURAVEL de centros (Plant) e depositos (StorageLocation) autorizados para a
/// Entrada de Produto (ex.: Jales). Nao deve ser hardcoded no Form: as listas vem de
/// configuracao (variaveis de ambiente FUGAPET_ENTRADA_CENTROS_AUTORIZADOS e
/// FUGAPET_ENTRADA_DEPOSITOS_AUTORIZADOS, separadas por ';').
///
/// Politica quando uma lista esta vazia: NAO restringe aquele eixo (mantem o comportamento
/// atual ate a configuracao ser definida) — decisao sinalizada para validacao funcional.
/// Esta classe NAO inventa regra de status/saldo; trata apenas centro/deposito.
/// </summary>
public sealed class AutorizacaoCentroDepositoEntrada
{
    public const string VariavelCentros = "FUGAPET_ENTRADA_CENTROS_AUTORIZADOS";
    public const string VariavelDepositos = "FUGAPET_ENTRADA_DEPOSITOS_AUTORIZADOS";

    private readonly IReadOnlySet<string> _centros;
    private readonly IReadOnlySet<string> _depositos;

    public AutorizacaoCentroDepositoEntrada(IEnumerable<string> centrosAutorizados, IEnumerable<string> depositosAutorizados)
    {
        _centros = Normalizar(centrosAutorizados);
        _depositos = Normalizar(depositosAutorizados);
    }

    /// <summary>True quando ha lista de centros configurada (logo, ha restricao de centro).</summary>
    public bool RestringeCentro => _centros.Count > 0;

    /// <summary>True quando ha lista de depositos configurada (logo, ha restricao de deposito).</summary>
    public bool RestringeDeposito => _depositos.Count > 0;

    public bool CentroAutorizado(string? plant)
        => !RestringeCentro
           || (!string.IsNullOrWhiteSpace(plant) && _centros.Contains(plant.Trim().ToUpperInvariant()));

    public bool DepositoAutorizado(string? storageLocation)
        => !RestringeDeposito
           || (!string.IsNullOrWhiteSpace(storageLocation) && _depositos.Contains(storageLocation.Trim().ToUpperInvariant()));

    /// <summary>Item autorizado quando centro E deposito estao dentro do escopo configurado.</summary>
    public bool ItemAutorizado(string? plant, string? storageLocation)
        => CentroAutorizado(plant) && DepositoAutorizado(storageLocation);

    public static AutorizacaoCentroDepositoEntrada CarregarDoAmbiente()
        => new(
            LerLista(Environment.GetEnvironmentVariable(VariavelCentros)),
            LerLista(Environment.GetEnvironmentVariable(VariavelDepositos)));

    private static IReadOnlySet<string> Normalizar(IEnumerable<string> valores)
        => valores
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Select(valor => valor.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> LerLista(string? bruto)
        => string.IsNullOrWhiteSpace(bruto)
            ? []
            : bruto.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
