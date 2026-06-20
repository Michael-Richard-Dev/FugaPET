using System.Text.Json;

namespace FugaPET_Dev.AcessoDados.Banco;

public static class LeitorConfiguracaoBancoPostgreSql
{
    private const string NomeArquivoConfiguracao = "configuracao.banco.json";
    private const string VariavelAmbienteConexao = "FUGAPET_DEV_CONEXAO_POSTGRES";
    private const string VariavelAmbienteSenha = "FUGAPET_DEV_POSTGRES_SENHA";

    public static ConfiguracaoBancoPostgreSql Carregar()
    {
        string? conexaoAmbiente = ObterVariavelAmbiente(VariavelAmbienteConexao);
        if (!string.IsNullOrWhiteSpace(conexaoAmbiente))
        {
            return ParsePorConnectionString(conexaoAmbiente);
        }

        string caminhoArquivo = Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao);
        if (!File.Exists(caminhoArquivo))
        {
            return new ConfiguracaoBancoPostgreSql();
        }

        string json = File.ReadAllText(caminhoArquivo);
        using JsonDocument documento = JsonDocument.Parse(json);

        if (!documento.RootElement.TryGetProperty("banco", out JsonElement banco))
        {
            return new ConfiguracaoBancoPostgreSql();
        }

        return new ConfiguracaoBancoPostgreSql
        {
            Habilitado = LerBooleano(banco, "habilitado", false),
            ModoDemonstracao = LerBooleano(banco, "modo_demonstracao", false),
            AmbienteDemonstrativo = LerBooleano(banco, "ambiente_demonstrativo", false),
            Servidor = LerTexto(banco, "servidor", "127.0.0.1"),
            Porta = LerInteiro(banco, "porta", 5432),
            NomeBanco = LerTexto(banco, "nome_banco", "api_balanca"),
            Schema = ValidarSchema(LerTexto(banco, "schema", "desenvolvimento")),
            Usuario = LerTexto(banco, "usuario", "postgres"),
            Senha = LerSenha(banco),
            TimeoutSegundos = LerInteiro(banco, "timeout_segundos", 15),
            Pooling = LerBooleano(banco, "pooling", true),
            SslMode = LerTexto(banco, "ssl_mode", "Prefer")
        };
    }

    private static ConfiguracaoBancoPostgreSql ParsePorConnectionString(string connectionString)
    {
        Dictionary<string, string> itens = new(StringComparer.OrdinalIgnoreCase);
        string[] pares = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (string par in pares)
        {
            string[] chaveValor = par.Split('=', 2, StringSplitOptions.TrimEntries);
            if (chaveValor.Length == 2 && !string.IsNullOrWhiteSpace(chaveValor[0]))
            {
                itens[chaveValor[0]] = chaveValor[1];
            }
        }

        return new ConfiguracaoBancoPostgreSql
        {
            Habilitado = true,
            ModoDemonstracao = false,
            AmbienteDemonstrativo = false,
            Servidor = LerChave(itens, "Host", "127.0.0.1"),
            Porta = int.TryParse(LerChave(itens, "Port", "5432"), out int porta) ? porta : 5432,
            NomeBanco = LerChave(itens, "Database", "api_balanca"),
            Schema = ValidarSchema(
                LerChave(
                    itens,
                    "Search Path",
                    LerChave(itens, "SearchPath", "desenvolvimento"))),
            Usuario = LerChave(itens, "Username", "postgres"),
            Senha = LerChave(itens, "Password", string.Empty),
            TimeoutSegundos = int.TryParse(LerChave(itens, "Timeout", "15"), out int timeout) ? timeout : 15,
            Pooling = bool.TryParse(LerChave(itens, "Pooling", "true"), out bool pooling) && pooling,
            SslMode = LerChave(itens, "SSL Mode", "Prefer")
        };
    }

    private static string LerChave(IReadOnlyDictionary<string, string> itens, string chave, string valorPadrao)
        => itens.TryGetValue(chave, out string? valor) && !string.IsNullOrWhiteSpace(valor) ? valor : valorPadrao;

    private static string ValidarSchema(string schema)
    {
        string valor = string.IsNullOrWhiteSpace(schema)
            ? "desenvolvimento"
            : schema.Trim();

        if (!char.IsLetter(valor[0]) && valor[0] != '_'
            || valor.Any(caractere => !char.IsLetterOrDigit(caractere) && caractere != '_'))
        {
            throw new InvalidOperationException(
                "O schema PostgreSQL configurado possui formato invalido.");
        }

        return valor;
    }

    private static string LerSenha(JsonElement banco)
    {
        string? senhaAmbiente = ObterVariavelAmbiente(VariavelAmbienteSenha);
        if (!string.IsNullOrWhiteSpace(senhaAmbiente))
        {
            return senhaAmbiente;
        }

        return LerTexto(banco, "senha", string.Empty);
    }

    private static string? ObterVariavelAmbiente(string nome)
        => Environment.GetEnvironmentVariable(nome)
            ?? Environment.GetEnvironmentVariable(nome, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(nome, EnvironmentVariableTarget.Machine);

    private static string LerTexto(JsonElement elemento, string propriedade, string valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? valorPadrao
            : valorPadrao;

    private static int LerInteiro(JsonElement elemento, string propriedade, int valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.TryGetInt32(out int inteiro)
            ? inteiro
            : valorPadrao;

    private static bool LerBooleano(JsonElement elemento, string propriedade, bool valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? valor.GetBoolean()
            : valorPadrao;
}
