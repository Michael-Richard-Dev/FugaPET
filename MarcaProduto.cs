namespace FugaPET_Dev;

/// <summary>
/// Identidade OFICIAL do produto — fonte unica para titulos, mensagens e documentacao.
/// Use estas constantes em vez de repetir o nome do produto em texto solto.
/// Observacao: NAO renomeia solution/projeto/namespaces; o nome tecnico continua "FugaPET_Dev".
/// </summary>
public static class MarcaProduto
{
    /// <summary>Nome curto do produto: "FugaPET".</summary>
    public const string Nome = "FugaPET";

    /// <summary>Descritivo oficial do sistema.</summary>
    public const string Descricao = "Sistema de Produção e Rastreabilidade PET";

    /// <summary>Nome completo oficial: "FugaPET — Sistema de Produção e Rastreabilidade PET".</summary>
    public const string NomeCompleto = Nome + " — " + Descricao;

    /// <summary>Empresa responsavel (mantida separada do nome do produto).</summary>
    public const string Empresa = "FUGA COUROS S.A.";
}
