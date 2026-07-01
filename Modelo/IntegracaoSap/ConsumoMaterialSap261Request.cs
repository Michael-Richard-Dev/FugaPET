namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Cabecalho do movimento de CONSUMO 261 (preview) para API_MATERIAL_DOCUMENT_SRV. Tarefa 6: apenas
/// montagem/preview do payload — NAO ha POST, CSRF nem criacao de documento. Datas serializadas em
/// OData V2 (/Date(ms)/) pelo builder; itens em to_MaterialDocumentItem.results.
/// </summary>
public sealed record ConsumoMaterialSap261Request
{
    /// <summary>GoodsMovementCode — fixo "03" (consumo/baixa para ordem).</summary>
    public string GoodsMovementCode { get; init; } = "03";

    /// <summary>Data de lancamento (PostingDate).</summary>
    public DateTime PostingDate { get; init; }

    /// <summary>Data do documento (DocumentDate).</summary>
    public DateTime DocumentDate { get; init; }

    /// <summary>Texto de cabecalho curto (<= 25 caracteres), ex. "FP CONS 1000009".</summary>
    public string MaterialDocumentHeaderText { get; init; } = string.Empty;

    /// <summary>Itens do consumo (serializados em to_MaterialDocumentItem.results).</summary>
    public IReadOnlyList<ConsumoMaterialSap261ItemRequest> ToMaterialDocumentItem { get; init; } = [];
}
