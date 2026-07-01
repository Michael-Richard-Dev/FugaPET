using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

public sealed class ProdutoAcabadoMaterialDocument101Request
{
    public string GoodsMovementCode { get; init; } = "02";
    public string PostingDate { get; init; } = string.Empty;
    public string DocumentDate { get; init; } = string.Empty;
    public string MaterialDocumentHeaderText { get; init; } = "FugaPET produto acabado";

    [JsonPropertyName("to_MaterialDocumentItem")]
    public ProdutoAcabadoMaterialDocument101ItemResults ToMaterialDocumentItem { get; init; } = new();
}

public sealed class ProdutoAcabadoMaterialDocument101ItemResults
{
    [JsonPropertyName("results")]
    public IReadOnlyList<ProdutoAcabadoMaterialDocument101ItemRequest> Results { get; init; } = [];
}

public sealed class ProdutoAcabadoMaterialDocument101ItemRequest
{
    public string Material { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public string GoodsMovementType { get; init; } = "101";
    public string QuantityInEntryUnit { get; init; } = string.Empty;
    public string EntryUnit { get; init; } = "KG";
    public string ManufacturingOrder { get; init; } = string.Empty;
    public string? ManufacturingOrderItem { get; init; }
    public string Batch { get; init; } = string.Empty;
    public string MaterialDocumentItemText { get; init; } = string.Empty;
}

public sealed class ResultadoPreviewProdutoAcabado101
{
    private ResultadoPreviewProdutoAcabado101(bool sucesso, string mensagem, ProdutoAcabadoMaterialDocument101Request? payload, string payloadJson)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Payload = payload;
        PayloadJson = payloadJson;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public ProdutoAcabadoMaterialDocument101Request? Payload { get; }
    public string PayloadJson { get; }

    public static ResultadoPreviewProdutoAcabado101 Ok(ProdutoAcabadoMaterialDocument101Request payload, string payloadJson)
        => new(true, "Preview Material Document 101 de produto acabado gerado.", payload, payloadJson);

    public static ResultadoPreviewProdutoAcabado101 Falha(string mensagem)
        => new(false, mensagem, null, string.Empty);
}
