using FugaPET_Dev.AcessoDados.Banco;
using Npgsql;

namespace FugaPET_Dev.Tests.Repositorio;

internal static class BancoTesteIntegracao
{
    public const string VariavelConnectionString = "FUGAPET_DEV_TESTE_CONNECTION_STRING";

    // Opt-in EXPLICITO para testes destrutivos (a limpeza usa DELETE). Sem isto, os testes pulam.
    public const string VariavelPermitirDestrutivo = "FUGAPET_DEV_PERMITIR_TESTE_DESTRUTIVO";

    // Prefixo de seguranca: o database DEVE comecar com isto (nao basta "conter teste"),
    // para nunca rodar DELETE contra um banco compartilhado como "desenvolvimento_teste".
    public const string PrefixoBancoSeguro = "fuga_balanca_teste_";

    /// <summary>True somente se a flag destrutiva estiver explicitamente em "true".</summary>
    public static bool DestrutivoAutorizado()
        => string.Equals(
            Environment.GetEnvironmentVariable(VariavelPermitirDestrutivo)?.Trim(),
            "true",
            StringComparison.OrdinalIgnoreCase);

    public static bool TentarCriar(out FabricaConexaoBancoTeste fabrica, out string motivo)
    {
        fabrica = null!;

        string? connectionString = Environment.GetEnvironmentVariable(VariavelConnectionString);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            motivo = $"Variavel {VariavelConnectionString} nao configurada.";
            return false;
        }

        // (1) Exige autorizacao explicita para operacoes destrutivas (DELETE na limpeza).
        if (!DestrutivoAutorizado())
        {
            motivo = $"Defina {VariavelPermitirDestrutivo}=true para autorizar testes destrutivos (a limpeza usa DELETE).";
            return false;
        }

        NpgsqlConnectionStringBuilder builder = new(connectionString);
        string database = builder.Database ?? string.Empty;

        // (2) Exige prefixo seguro no nome do database (StartsWith, nao apenas "contem").
        if (!database.StartsWith(PrefixoBancoSeguro, StringComparison.OrdinalIgnoreCase))
        {
            motivo = $"Banco de integracao recusado: o database '{database}' deve comecar com '{PrefixoBancoSeguro}'.";
            return false;
        }

        // (3) Registra no output qual banco sera usado (DELETE escopado por prefixo de massa).
        Console.WriteLine(
            $"[Teste Integracao] Operacoes destrutivas AUTORIZADAS. Host={builder.Host}; Porta={builder.Port}; Database={database}.");

        fabrica = new FabricaConexaoBancoTeste(builder.ConnectionString);
        motivo = string.Empty;
        return true;
    }

    public static async Task<bool> SchemaDesenvolvimentoDisponivelAsync(FabricaConexaoBancoTeste fabrica)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'desenvolvimento');";
        await using NpgsqlConnection conexao = await fabrica.CriarConexaoAbertaAsync();
        await using NpgsqlCommand comando = new(sql, conexao);
        object? retorno = await comando.ExecuteScalarAsync();
        return retorno is bool existe && existe;
    }
}

internal sealed class FabricaConexaoBancoTeste : IFabricaConexaoBanco
{
    private readonly string _connectionString;

    public FabricaConexaoBancoTeste(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlConnection CriarConexao()
        => new(_connectionString);

    public async Task<NpgsqlConnection> CriarConexaoAbertaAsync(CancellationToken cancellationToken = default)
    {
        NpgsqlConnection conexao = CriarConexao();
        await conexao.OpenAsync(cancellationToken);
        return conexao;
    }

    public string ObterConnectionString()
        => _connectionString;
}
