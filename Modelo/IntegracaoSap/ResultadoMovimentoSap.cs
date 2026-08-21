using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Resultado tipado de um movimento SAP (261 ou 101) do Produto Acabado. Confirmado SOMENTE quando:
/// HTTP de sucesso esperado E MaterialDocument não vazio E MaterialDocumentYear não vazio. HTTP 201 sem
/// documento ⇒ Erro (não confirmado). Timeout após possível transmissão ⇒ IndeterminadoTimeout (bloqueia
/// a etapa seguinte; zero retry cego). Nunca expõe segredo/payload — mensagem sempre sanitizada.
/// </summary>
public sealed record ResultadoMovimentoSap
{
    public required EstadoMovimentoSap Estado { get; init; }
    public string? MaterialDocument { get; init; }
    public string? MaterialDocumentYear { get; init; }
    public int? HttpStatus { get; init; }
    public string MensagemSanitizada { get; init; } = string.Empty;

    /// <summary>Só o estado Confirmado autoriza a etapa seguinte do pipeline.</summary>
    public bool PodeAvancar => Estado == EstadoMovimentoSap.Confirmado;

    public static ResultadoMovimentoSap Confirmado(string materialDocument, string materialDocumentYear, int? httpStatus)
        => new()
        {
            Estado = EstadoMovimentoSap.Confirmado,
            MaterialDocument = materialDocument,
            MaterialDocumentYear = materialDocumentYear,
            HttpStatus = httpStatus,
            MensagemSanitizada = $"Documento {materialDocument}/{materialDocumentYear} confirmado."
        };

    public static ResultadoMovimentoSap Erro(string mensagemSanitizada, int? httpStatus = null)
        => new() { Estado = EstadoMovimentoSap.Erro, HttpStatus = httpStatus, MensagemSanitizada = mensagemSanitizada };

    public static ResultadoMovimentoSap Indeterminado(string mensagemSanitizada, int? httpStatus = null)
        => new() { Estado = EstadoMovimentoSap.IndeterminadoTimeout, HttpStatus = httpStatus, MensagemSanitizada = mensagemSanitizada };

    /// <summary>Bloqueio fail-closed (gate/dependência) ANTES de qualquer HTTP: erro sem status, sem POST.</summary>
    public static ResultadoMovimentoSap Bloqueado(string mensagemSanitizada)
        => new() { Estado = EstadoMovimentoSap.Erro, HttpStatus = null, MensagemSanitizada = mensagemSanitizada };

    /// <summary>
    /// Classifica um resultado 201/2xx: confirmado só com documento+ano; senão Erro (não confirmado).
    /// </summary>
    public static ResultadoMovimentoSap ClassificarSucesso(string? materialDocument, string? materialDocumentYear, int? httpStatus)
        => !string.IsNullOrWhiteSpace(materialDocument) && !string.IsNullOrWhiteSpace(materialDocumentYear)
            ? Confirmado(materialDocument!, materialDocumentYear!, httpStatus)
            : Erro("HTTP de sucesso sem MaterialDocument/MaterialDocumentYear: não confirmado.", httpStatus);
}
