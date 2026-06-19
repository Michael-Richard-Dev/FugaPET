using System.Text.Json;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Le parametros nao secretos de configuracao.sap.json (secao "sap").
/// Usuario e senha sao aceitos exclusivamente pelas variaveis de ambiente FUGAPET_SAP_USERNAME
/// e FUGAPET_SAP_PASSWORD.
/// </summary>
public static class LeitorConfiguracaoSap
{
    private const string NomeArquivoConfiguracao = "configuracao.sap.json";
    private const string VariavelAmbienteBaseUrl = "FUGAPET_SAP_BASE_URL";
    private const string VariavelAmbienteUsuario = "FUGAPET_SAP_USERNAME";
    private const string VariavelAmbienteSenha = "FUGAPET_SAP_PASSWORD";
    private const string VariavelAmbienteSapClient = "FUGAPET_SAP_CLIENT";
    private const string VariavelAmbienteHostsPermitidos = "FUGAPET_SAP_ALLOWED_HOSTS";
    private const string VariavelAmbienteEscritaHabilitada = "FUGAPET_SAP_WRITE_ENABLED";

    public static ConfiguracaoSap Carregar()
        => Carregar(
            Path.Combine(AppContext.BaseDirectory, NomeArquivoConfiguracao),
            ObterVariavelAmbienteSistema);

    internal static string? ObterVariavelAmbienteSistema(string nome)
        => ObterVariavelAmbiente(
            nome,
            variavel => Environment.GetEnvironmentVariable(variavel, EnvironmentVariableTarget.Process),
            variavel => Environment.GetEnvironmentVariable(variavel, EnvironmentVariableTarget.User));

    internal static string? ObterVariavelAmbiente(
        string nome,
        Func<string, string?> obterProcesso,
        Func<string, string?> obterUsuario)
    {
        string? processo = obterProcesso(nome);
        return !string.IsNullOrWhiteSpace(processo)
            ? processo
            : obterUsuario(nome);
    }

    internal static ConfiguracaoSap Carregar(
        string caminhoArquivo,
        Func<string, string?> obterVariavelAmbiente)
    {
        string baseUrlArquivo = string.Empty;
        string sapClientArquivo = string.Empty;
        IReadOnlyList<string> hostsPermitidosArquivo = [];
        bool escritaHabilitada = false;
        int timeout = 30;

        if (File.Exists(caminhoArquivo))
        {
            try
            {
                using JsonDocument documento = JsonDocument.Parse(File.ReadAllText(caminhoArquivo));
                if (documento.RootElement.TryGetProperty("sap", out JsonElement sap))
                {
                    baseUrlArquivo = LerTexto(sap, "base_url", string.Empty);
                    sapClientArquivo = LerTexto(sap, "sap_client", string.Empty);
                    hostsPermitidosArquivo = LerListaTextos(sap, "hosts_permitidos");
                    escritaHabilitada = LerBooleano(sap, "escrita_habilitada", false);
                    timeout = LerInteiro(sap, "timeout_segundos", 30);
                }
            }
            catch
            {
                // Arquivo malformado nao deve quebrar a aplicacao: trata como nao configurado.
            }
        }

        return new ConfiguracaoSap
        {
            BaseUrl = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteBaseUrl, baseUrlArquivo),
            Usuario = ObterSomenteAmbiente(obterVariavelAmbiente, VariavelAmbienteUsuario),
            Senha = ObterSomenteAmbiente(obterVariavelAmbiente, VariavelAmbienteSenha),
            SapClient = ObterOuAmbiente(obterVariavelAmbiente, VariavelAmbienteSapClient, sapClientArquivo),
            HostsPermitidos = ObterHostsPermitidos(
                obterVariavelAmbiente,
                VariavelAmbienteHostsPermitidos,
                hostsPermitidosArquivo),
            EscritaHabilitada = ObterBooleanoOuArquivo(
                obterVariavelAmbiente,
                VariavelAmbienteEscritaHabilitada,
                escritaHabilitada),
            TimeoutSegundos = timeout
        };
    }

    private static bool ObterBooleanoOuArquivo(
        Func<string, string?> obterVariavelAmbiente,
        string variavel,
        bool valorArquivo)
    {
        string? ambiente = obterVariavelAmbiente(variavel);
        return bool.TryParse(ambiente, out bool valor) ? valor : valorArquivo;
    }

    private static string ObterOuAmbiente(
        Func<string, string?> obterVariavelAmbiente,
        string variavel,
        string valorArquivo)
    {
        string? ambiente = obterVariavelAmbiente(variavel);
        return !string.IsNullOrWhiteSpace(ambiente) ? ambiente : valorArquivo;
    }

    private static string ObterSomenteAmbiente(
        Func<string, string?> obterVariavelAmbiente,
        string variavel)
        => obterVariavelAmbiente(variavel)?.Trim() ?? string.Empty;

    private static IReadOnlyList<string> ObterHostsPermitidos(
        Func<string, string?> obterVariavelAmbiente,
        string variavel,
        IReadOnlyList<string> valoresArquivo)
    {
        string? ambiente = obterVariavelAmbiente(variavel);
        IEnumerable<string> valores = string.IsNullOrWhiteSpace(ambiente)
            ? valoresArquivo
            : ambiente.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return valores
            .Where(valor => !string.IsNullOrWhiteSpace(valor))
            .Select(valor => valor.Trim().TrimEnd('.').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string LerTexto(JsonElement elemento, string propriedade, string valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString() ?? valorPadrao
            : valorPadrao;

    private static int LerInteiro(JsonElement elemento, string propriedade, int valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor) && valor.TryGetInt32(out int inteiro)
            ? inteiro
            : valorPadrao;

    private static bool LerBooleano(JsonElement elemento, string propriedade, bool valorPadrao)
        => elemento.TryGetProperty(propriedade, out JsonElement valor)
           && valor.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? valor.GetBoolean()
            : valorPadrao;

    private static IReadOnlyList<string> LerListaTextos(JsonElement elemento, string propriedade)
    {
        if (!elemento.TryGetProperty(propriedade, out JsonElement valor)
            || valor.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return valor.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }
}
