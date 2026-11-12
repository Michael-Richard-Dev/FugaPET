using System.Text.Json;

namespace FugaPET_Dev.AcessoDados.Banco;

public static class LeitorConfiguracaoBancoPostgreSql
{
    private const string NomeArquivoConfiguracao = "configuracao.banco.json";
    private const string VariavelAmbienteConexao = "FUGAPET_Q_CONEXAO_POSTGRES";
    private const string VariavelAmbienteSenha = "FUGAPET_Q_POSTGRES_SENHA";

    public static ConfiguracaoBancoPostgreSql Carregar()
        => Carregar(ObterVariavelAmbiente, Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao));

    internal static ConfiguracaoBancoPostgreSql Carregar(
        Func<string, string?> obterVariavelAmbiente,
        string? caminhoArquivo = null)
    {
        string? conexaoAmbiente = obterVariavelAmbiente(VariavelAmbienteConexao);
        if (!string.IsNullOrWhiteSpace(conexaoAmbiente))
        {
            // GATE 20D: o caminho por connection string preserva Host/Port/Database/Username/SearchPath da
            // própria string; a SENHA, quando não materializada de forma válida na connection string
            // (Password ausente/blank), é complementada EXCLUSIVAMENTE por FUGAPET_Q_POSTGRES_SENHA. Sem
            // fallback HML, sem senha default, sem hardcode — ausência de secret permanece fail-closed.
            return ParsePorConnectionString(conexaoAmbiente, obterVariavelAmbiente(VariavelAmbienteSenha));
        }

        caminhoArquivo ??= Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao);
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
            Servidor = LerTexto(banco, "servidor", string.Empty),
            Porta = LerInteiro(banco, "porta", 5432),
            NomeBanco = LerTexto(banco, "nome_banco", string.Empty),
            Schema = ValidarSchema(LerTexto(banco, "schema", string.Empty)),
            Usuario = LerTexto(banco, "usuario", string.Empty),
            Senha = LerSenha(banco),
            TimeoutSegundos = LerInteiro(banco, "timeout_segundos", 15),
            Pooling = LerBooleano(banco, "pooling", true),
            SslMode = LerTexto(banco, "ssl_mode", "Prefer")
        };
    }

    private static ConfiguracaoBancoPostgreSql ParsePorConnectionString(
        string connectionString,
        string? senhaComplementarAmbiente)
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

        // GATE 20D: precedência da connection string para Password; se ausente/blank, complementa por
        // FUGAPET_Q_POSTGRES_SENHA. Ausência de ambos ⇒ senha vazia (fail-closed). Sem fallback/hardcode.
        string senhaConexao = LerChave(itens, "Password", string.Empty);
        string senhaFinal = !string.IsNullOrWhiteSpace(senhaConexao)
            ? senhaConexao
            : (senhaComplementarAmbiente?.Trim() ?? string.Empty);

        return new ConfiguracaoBancoPostgreSql
        {
            Habilitado = true,
            ModoDemonstracao = false,
            AmbienteDemonstrativo = false,
            Servidor = LerChave(itens, "Host", string.Empty),
            Porta = int.TryParse(LerChave(itens, "Port", "5432"), out int porta) ? porta : 5432,
            NomeBanco = LerChave(itens, "Database", string.Empty),
            Schema = ValidarSchema(
                LerChave(
                    itens,
                    "Search Path",
                    LerChave(itens, "SearchPath", string.Empty))),
            Usuario = LerChave(itens, "Username", string.Empty),
            Senha = senhaFinal,
            TimeoutSegundos = int.TryParse(LerChave(itens, "Timeout", "15"), out int timeout) ? timeout : 15,
            Pooling = bool.TryParse(LerChave(itens, "Pooling", "true"), out bool pooling) && pooling,
            SslMode = LerChave(itens, "SSL Mode", "Prefer")
        };
    }

    private static string LerChave(IReadOnlyDictionary<string, string> itens, string chave, string valorPadrao)
        => itens.TryGetValue(chave, out string? valor) && !string.IsNullOrWhiteSpace(valor) ? valor : valorPadrao;

    private static string ValidarSchema(string schema)
    {
        string valor = schema.Trim();
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

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
