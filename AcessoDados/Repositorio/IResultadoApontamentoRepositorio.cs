using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Contrato futuro de persistência dos resultados de apontamento. A View não conhece PostgreSQL.
/// </summary>
public interface IResultadoApontamentoRepositorio
{
    Task<long> InserirAsync(
        RegistroResultadoApontamento registro,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
        long codigoPerfilResultado,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ResultadoPersistidoApontamento>> ListarResultadosPersistidosDoApontamentoAsync(
        long codigoApontamento,
        CancellationToken cancellationToken);
}

public sealed class ResultadoPersistidoApontamento
{
    public long CodigoResultado { get; init; }
    public long CodigoApontamento { get; init; }
    public string MensagemResumo { get; init; } = string.Empty;
    public IReadOnlyList<ResultadoApontamentoItem> Itens { get; init; } = Array.Empty<ResultadoApontamentoItem>();
}

