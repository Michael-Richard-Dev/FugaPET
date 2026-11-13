using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Identifica o ambiente da aplicacao a partir do nome do banco configurado, para gatear a
/// ESCRITA controlada no SAP. O envio de peso so e permitido em HOMOLOGACAO.
/// </summary>
public static class AmbienteIntegracaoSap
{
    /// <summary>True quando o banco configurado for de homologacao (nome contem homolog/hml).</summary>
    public static bool EhHomologacao()
        => EhHomologacao(LeitorConfiguracaoBancoPostgreSql.Carregar().NomeBanco);

    public static bool EhHomologacao(string? nomeBanco)
        => !string.IsNullOrWhiteSpace(nomeBanco)
           && (nomeBanco.Contains("homolog", StringComparison.OrdinalIgnoreCase)
               || nomeBanco.Contains("_hml", StringComparison.OrdinalIgnoreCase)
               || nomeBanco.EndsWith("hml", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Liberacao EXPLICITA do envio de peso em um ambiente que nao e homologacao.
    /// <para>
    /// Existe porque o DEV110 aponta para um SAP de DESENVOLVIMENTO (DS) proprio, e a regra
    /// original — "so homologacao escreve" — foi escrita quando o DEV nao tinha destino SAP.
    /// Em vez de afrouxar <see cref="EhHomologacao(string?)"/>, que mudaria o comportamento de
    /// QUALQUER ambiente cujo banco tenha certo nome, a abertura e pontual e declarada.
    /// </para>
    /// <para>
    /// FAIL-CLOSED: ausente, vazia ou nao-booleana significa NAO LIBERADO. So o literal "true"
    /// libera. Lida no escopo Process, como os demais gates operacionais.
    /// </para>
    /// </summary>
    public const string VariavelLiberacaoEnvioPeso = "FUGAPET_SAP_ENVIO_PESO_LIBERADO";

    /// <summary>True quando a liberacao explicita esta ligada neste processo.</summary>
    public static bool EnvioPesoLiberadoExplicitamente()
        => EnvioPesoLiberadoExplicitamente(
            variavel => Environment.GetEnvironmentVariable(
                variavel, EnvironmentVariableTarget.Process));

    internal static bool EnvioPesoLiberadoExplicitamente(Func<string, string?> obterVariavel)
        => bool.TryParse(obterVariavel(VariavelLiberacaoEnvioPeso)?.Trim(), out bool liberado)
           && liberado;

    /// <summary>
    /// Regra efetiva do envio de peso: homologacao OU liberacao explicita deste ambiente.
    /// E esta que o fluxo de Entrada consulta — <see cref="EhHomologacao()"/> continua
    /// respondendo apenas "este banco e de homologacao?", sem opiniao sobre escrita.
    /// </summary>
    public static bool EnvioPesoPermitido()
        => EhHomologacao() || EnvioPesoLiberadoExplicitamente();
}
