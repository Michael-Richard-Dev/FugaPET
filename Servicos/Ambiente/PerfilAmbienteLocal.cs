using System.Text.Json;

namespace FugaPET_Dev.Servicos.Ambiente;

/// <summary>
/// GATE 120G: perfil de ambiente CANONICO lido da configuracao local, para que o EXE iniciado por
/// DUPLO CLIQUE determine sozinho que esta em Q e carregue os defaults operacionais Q — sem
/// PowerShell, sem BAT, sem setx, sem Registry e sem environment de User/Machine.
/// <para>
/// Secao esperada na raiz de <c>configuracao.sap.json</c>:
/// <code>{ "ambiente": { "perfil": "Q" } }</code>
/// </para>
/// <para>
/// FAIL-CLOSED por construcao: arquivo ausente, secao ausente, perfil vazio, perfil desconhecido ou
/// JSON malformado resultam em <see cref="PerfilOperacional.NaoDefinido"/>, que NAO concede nenhuma
/// capability. O perfil tambem NAO possui qualquer campo capaz de habilitar escrita SAP generica ou
/// escrita de palete — essas duas permanecem estruturalmente fora do seu alcance.
/// </para>
/// </summary>
public sealed record PerfilAmbienteLocal
{
    /// <summary>Nome canonico do perfil Q, identico ao <c>ValidadorAmbienteQ.AmbienteEsperado</c>.</summary>
    public const string NomePerfilQ = "Q";

    /// <summary>Secao e chave canonicas no arquivo de configuracao local.</summary>
    public const string SecaoAmbiente = "ambiente";
    public const string ChavePerfil = "perfil";

    private PerfilAmbienteLocal(PerfilOperacional perfil, string origem, string mensagem)
    {
        Perfil = perfil;
        Origem = origem;
        Mensagem = mensagem;
    }

    public PerfilOperacional Perfil { get; }

    /// <summary>Como o perfil foi determinado (diagnostico; nunca contem caminho nem conteudo do arquivo).</summary>
    public string Origem { get; }

    /// <summary>Motivo objetivo quando o perfil nao pode ser determinado.</summary>
    public string Mensagem { get; }

    /// <summary>true SOMENTE quando o perfil Q foi materialmente reconhecido na configuracao local.</summary>
    public bool EhQ => Perfil == PerfilOperacional.Q;

    /// <summary>Perfil nao determinado: nenhuma capability concedida.</summary>
    public static PerfilAmbienteLocal NaoDefinido(string mensagem)
        => new(PerfilOperacional.NaoDefinido, "AUSENTE", mensagem);

    /// <summary>Perfil Q reconhecido na configuracao local.</summary>
    public static PerfilAmbienteLocal Q(string origem)
        => new(PerfilOperacional.Q, origem, string.Empty);

    /// <summary>
    /// Le o perfil do arquivo de configuracao local. Nunca lanca: qualquer problema (ausencia, JSON
    /// malformado, tipo errado, perfil desconhecido) vira NaoDefinido — configuracao invalida ou
    /// ambigua NUNCA habilita nada.
    /// </summary>
    public static PerfilAmbienteLocal CarregarDoArquivo(string caminhoArquivo)
    {
        if (string.IsNullOrWhiteSpace(caminhoArquivo) || !File.Exists(caminhoArquivo))
        {
            return NaoDefinido(
                "Perfil de ambiente nao definido: configuracao local ausente. Nenhuma capability concedida.");
        }

        try
        {
            using JsonDocument documento = JsonDocument.Parse(File.ReadAllText(caminhoArquivo));
            return Interpretar(documento.RootElement);
        }
        catch (JsonException)
        {
            return NaoDefinido(
                "Perfil de ambiente nao definido: configuracao local malformada. Nenhuma capability concedida.");
        }
        catch (IOException)
        {
            return NaoDefinido(
                "Perfil de ambiente nao definido: configuracao local ilegivel. Nenhuma capability concedida.");
        }
        catch (UnauthorizedAccessException)
        {
            return NaoDefinido(
                "Perfil de ambiente nao definido: configuracao local sem permissao de leitura. Nenhuma capability concedida.");
        }
    }

    /// <summary>Interpreta a raiz do JSON de configuracao. Internal para teste direto, sem arquivo.</summary>
    internal static PerfilAmbienteLocal Interpretar(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object
            || !raiz.TryGetProperty(SecaoAmbiente, out JsonElement ambiente)
            || ambiente.ValueKind != JsonValueKind.Object
            || !ambiente.TryGetProperty(ChavePerfil, out JsonElement perfil)
            || perfil.ValueKind != JsonValueKind.String)
        {
            return NaoDefinido(
                $"Perfil de ambiente nao definido: declare \"{SecaoAmbiente}\": {{ \"{ChavePerfil}\": \"{NomePerfilQ}\" }} "
                + "na configuracao local. Nenhuma capability concedida.");
        }

        string valor = perfil.GetString()?.Trim() ?? string.Empty;
        if (string.Equals(valor, NomePerfilQ, StringComparison.OrdinalIgnoreCase))
        {
            return Q("CONFIG_LOCAL");
        }

        return NaoDefinido(
            valor.Length == 0
                ? "Perfil de ambiente nao definido: perfil vazio na configuracao local. Nenhuma capability concedida."
                : "Perfil de ambiente nao reconhecido na configuracao local. Nenhuma capability concedida.");
    }
}

/// <summary>GATE 120G: perfis operacionais reconhecidos. Qualquer outro valor cai em NaoDefinido.</summary>
public enum PerfilOperacional
{
    /// <summary>Nenhum perfil determinado: fail-closed total.</summary>
    NaoDefinido = 0,

    /// <summary>Homologacao Q.</summary>
    Q = 1
}

/// <summary>
/// GATE 120G: defaults operacionais do perfil Q — a configuracao que hoje so existia como seis
/// variaveis de ambiente de Process, criadas por script externo.
/// <para>
/// As duas escritas SEMPRE proibidas no startup Q (escrita SAP generica e escrita de palete INT012)
/// NAO estao representadas aqui de proposito: nao existe campo, logo nao existe caminho pelo qual o
/// perfil possa ligá-las. Elas continuam valendo false por default e so mudam por override explicito
/// de Process (o mecanismo dos gates tecnicos), nunca por perfil.
/// </para>
/// </summary>
public static class DefaultsPerfilQ
{
    /// <summary>pa_pipeline_habilitado no perfil Q.</summary>
    public static bool PipelineProdutoAcabado(PerfilAmbienteLocal? perfil) => perfil?.EhQ == true;

    /// <summary>pa_material_document_write no perfil Q.</summary>
    public static bool MaterialDocumentProdutoAcabado(PerfilAmbienteLocal? perfil) => perfil?.EhQ == true;

    /// <summary>hu_write_habilitado no perfil Q.</summary>
    public static bool HandlingUnitWrite(PerfilAmbienteLocal? perfil) => perfil?.EhQ == true;

    /// <summary>packaging (norma de embalagem) no perfil Q.</summary>
    public static bool Packaging(PerfilAmbienteLocal? perfil) => perfil?.EhQ == true;
}
