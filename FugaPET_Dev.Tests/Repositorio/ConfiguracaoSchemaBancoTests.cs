using FugaPET_Dev.AcessoDados.Banco;
using Npgsql;

namespace FugaPET_Dev.Tests.Repositorio;

public sealed class ConfiguracaoSchemaBancoTests : IDisposable
{
    private readonly string? _conexaoAnterior =
        Environment.GetEnvironmentVariable("FUGAPET_DEV_CONEXAO_POSTGRES");

    [Fact]
    public void ConfiguracaoPadrao_DeveUsarHomologacao()
    {
        ConfiguracaoBancoPostgreSql configuracao = new();

        Assert.Equal("homologacao", configuracao.Schema);
    }

    [Fact]
    public void Fabrica_DeveAplicarSchemaNoSearchPath()
    {
        ConfiguracaoBancoPostgreSql configuracao = new()
        {
            Habilitado = true,
            Servidor = "localhost",
            Porta = 5432,
            NomeBanco = "teste",
            Schema = "desenvolvimento",
            Usuario = "teste",
            Senha = "teste"
        };

        string connectionString =
            new FabricaConexaoPostgreSql(configuracao).ObterConnectionString();
        NpgsqlConnectionStringBuilder builder = new(connectionString);

        Assert.Equal("desenvolvimento", builder.SearchPath);
    }

    [Fact]
    public void LeitorConnectionString_DeveLerSearchPath()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Database=teste;Username=teste;Password=teste;Search Path=desenvolvimento");

        ConfiguracaoBancoPostgreSql configuracao =
            LeitorConfiguracaoBancoPostgreSql.Carregar();

        Assert.Equal("desenvolvimento", configuracao.Schema);
    }

    [Fact]
    public void LeitorConnectionString_DeveRejeitarSchemaInvalido()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Database=teste;Username=teste;Password=teste;Search Path=desenvolvimento,public");

        Assert.Throws<InvalidOperationException>(
            LeitorConfiguracaoBancoPostgreSql.Carregar);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            _conexaoAnterior);
    }
}
