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
}
