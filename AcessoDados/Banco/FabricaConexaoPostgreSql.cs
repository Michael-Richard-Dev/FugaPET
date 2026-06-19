using Npgsql;

namespace FugaPET_Dev.AcessoDados.Banco;

public sealed class FabricaConexaoPostgreSql : IFabricaConexaoBanco
{
    private readonly ConfiguracaoBancoPostgreSql _configuracao;

    public FabricaConexaoPostgreSql()
        : this(LeitorConfiguracaoBancoPostgreSql.Carregar())
    {
    }

    public FabricaConexaoPostgreSql(ConfiguracaoBancoPostgreSql configuracao)
    {
        _configuracao = configuracao;
    }

    public string ObterConnectionString()
    {
        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = _configuracao.Servidor,
            Port = _configuracao.Porta,
            Database = _configuracao.NomeBanco,
            SearchPath = _configuracao.Schema,
            Username = _configuracao.Usuario,
            Password = _configuracao.Senha,
            Timeout = _configuracao.TimeoutSegundos,
            Pooling = _configuracao.Pooling
        };

        if (Enum.TryParse(_configuracao.SslMode, true, out SslMode sslMode))
        {
            builder.SslMode = sslMode;
        }

        return builder.ConnectionString;
    }

    public NpgsqlConnection CriarConexao() => new(ObterConnectionString());

    public async Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
    {
        NpgsqlConnection conexao = CriarConexao();
        await conexao.OpenAsync(cancellationToken);
        return conexao;
    }
}
