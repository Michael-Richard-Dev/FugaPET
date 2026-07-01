namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Resultado do PREVIEW do movimento de consumo 261 (Tarefa 6). Sem POST/SAP. Quando a validacao
/// falha, NAO gera payload parcial como sucesso (Payload/PayloadJson ficam vazios).
/// </summary>
public sealed class ResultadoPreviewConsumoSap261
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public ConsumoMaterialSap261Request? Payload { get; init; }
    public string PayloadJson { get; init; } = string.Empty;
    public IReadOnlyList<ConsumoMaterialSap261ItemRequest> Itens { get; init; } = [];
    public IReadOnlyList<string> ErrosValidacao { get; init; } = [];

    public static ResultadoPreviewConsumoSap261 Ok(
        ConsumoMaterialSap261Request payload,
        string payloadJson,
        string mensagem)
        => new()
        {
            Sucesso = true,
            Mensagem = mensagem,
            Payload = payload,
            PayloadJson = payloadJson,
            Itens = payload.ToMaterialDocumentItem
        };

    public static ResultadoPreviewConsumoSap261 Falha(string mensagem, IReadOnlyList<string>? erros = null)
        => new()
        {
            Sucesso = false,
            Mensagem = mensagem,
            ErrosValidacao = erros ?? [mensagem]
        };
}
