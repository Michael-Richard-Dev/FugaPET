using FugaPET_Dev.AcessoDados.Banco;
using Npgsql;

namespace FugaPET_Dev.Tests.Repositorio;

[Xunit.Collection(FugaPET_Dev.Tests.Configuracao.ColecaoConfiguracaoDeAmbiente.Nome)]
public sealed class ConfiguracaoSchemaBancoTests : IDisposable
{
    private readonly string? _conexaoAnterior =
        Environment.GetEnvironmentVariable("FUGAPET_Q_CONEXAO_POSTGRES");

    [Fact]
    public void ConfiguracaoPadrao_Q_DeveFalharFechadoSemSchema()
    {
        ConfiguracaoBancoPostgreSql configuracao = new();

        Assert.Equal(string.Empty, configuracao.Schema);
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
            Schema = "homologacao",
            Usuario = "teste",
            Senha = "teste"
        };

        string connectionString =
            new FabricaConexaoPostgreSql(configuracao).ObterConnectionString();
        NpgsqlConnectionStringBuilder builder = new(connectionString);

        Assert.Equal("homologacao", builder.SearchPath);
    }

    [Fact]
    public void LeitorConnectionString_DeveLerSearchPath()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_Q_CONEXAO_POSTGRES",
            "Host=localhost;Database=teste;Username=teste;Password=teste;Search Path=qualidade");

        ConfiguracaoBancoPostgreSql configuracao =
            LeitorConfiguracaoBancoPostgreSql.Carregar();

        Assert.Equal("qualidade", configuracao.Schema);
        Assert.True(configuracao.Habilitado);
        Assert.False(configuracao.ModoDemonstracao);
        Assert.False(configuracao.AmbienteDemonstrativo);
    }

    [Fact]
    public void LeitorConnectionString_SemSearchPath_Q_DeveFalharFechadoSemSchema()
    {
        // Sem Search Path na connection string, o fallback do leitor deve ser o schema do
        // ambiente Q: ausência de Search Path permanece vazia para bloqueio fail-closed.
        Environment.SetEnvironmentVariable(
            "FUGAPET_Q_CONEXAO_POSTGRES",
            "Host=localhost;Database=teste;Username=teste;Password=teste");

        ConfiguracaoBancoPostgreSql configuracao =
            LeitorConfiguracaoBancoPostgreSql.Carregar();

        Assert.Equal(string.Empty, configuracao.Schema);
    }

    [Fact]
    public void LeitorConnectionString_DeveRejeitarSchemaInvalido()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_Q_CONEXAO_POSTGRES",
            "Host=localhost;Database=teste;Username=teste;Password=teste;Search Path=qualidade,public");

        Assert.Throws<InvalidOperationException>(
            LeitorConfiguracaoBancoPostgreSql.Carregar);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_Q_CONEXAO_POSTGRES",
            _conexaoAnterior);
    }
}




