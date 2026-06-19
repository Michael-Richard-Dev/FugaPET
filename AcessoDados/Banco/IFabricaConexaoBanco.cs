using Npgsql;

namespace FugaPET_Dev.AcessoDados.Banco;

public interface IFabricaConexaoBanco
{
    NpgsqlConnection CriarConexao();
    Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default);
    string ObterConnectionString();
}
