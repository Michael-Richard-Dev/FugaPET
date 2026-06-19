using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

public sealed class ConfiguracaoGeralRepositorio : RepositorioBase
{
    public ConfiguracaoGeralRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    public async Task<bool?> ObterBooleanoAsync(
        string chave,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT valor
              FROM configuracao_geral
             WHERE upper(trim(chave)) = upper(trim(@chave))
               AND situacao_configuracao_geral = true
             ORDER BY codigo_configuracao_geral DESC
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@chave", chave));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);

        if (retorno is not string valor)
        {
            return null;
        }

        return valor.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "sim" or "s" or "yes" or "on" => true,
            "false" or "0" or "nao" or "não" or "n" or "no" or "off" => false,
            _ => null
        };
    }
}
