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
        ContextoApontamentoProcesso contexto,
        CancellationToken cancellationToken);
}
