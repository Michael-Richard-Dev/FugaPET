namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Resultado da criacao de um documento de material (movimento 101) no SAP. Mensagem sempre
/// sanitizada (sem Authorization/senha/cookie/payload). Em sucesso traz o numero do documento e o
/// exercicio retornados pelo SAP.
/// </summary>
public sealed record ResultadoMaterialDocumentSap
{
    public bool Sucesso { get; init; }

    /// <summary>Status HTTP da resposta SAP (null quando nao houve resposta HTTP: rede/TLS/timeout).</summary>
    public int? StatusHttp { get; init; }

    /// <summary>Etapa onde o resultado foi produzido: CSRF_FETCH, POST_DOCUMENTO_MATERIAL, PARSE_RESPOSTA.</summary>
    public string? Etapa { get; init; }

    /// <summary>d.MaterialDocument — numero do documento de material criado.</summary>
    public string? MaterialDocument { get; init; }

    /// <summary>d.MaterialDocumentYear — exercicio do documento de material criado.</summary>
    public string? MaterialDocumentYear { get; init; }

    /// <summary>Itens do documento (d.to_MaterialDocumentItem.results[].MaterialDocumentItem).</summary>
    public IReadOnlyList<string> ItensDocumento { get; init; } = [];

    public string MensagemSanitizada { get; init; } = string.Empty;

    public static ResultadoMaterialDocumentSap Falha(int? statusHttp, string mensagem)
        => new() { Sucesso = false, StatusHttp = statusHttp, MensagemSanitizada = mensagem };
}
