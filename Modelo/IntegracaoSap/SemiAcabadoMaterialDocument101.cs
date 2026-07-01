using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

public sealed class SemiAcabadoMaterialDocument101Request
{
    public string GoodsMovementCode { get; init; } = "02";
    public string PostingDate { get; init; } = string.Empty;
    public string DocumentDate { get; init; } = string.Empty;
    public string MaterialDocumentHeaderText { get; init; } = "FugaPET semi-acabado";

    [JsonPropertyName("to_MaterialDocumentItem")]
    public SemiAcabadoMaterialDocument101ItemResults ToMaterialDocumentItem { get; init; } = new();
}

public sealed class SemiAcabadoMaterialDocument101ItemResults
{
    [JsonPropertyName("results")]
    public IReadOnlyList<SemiAcabadoMaterialDocument101ItemRequest> Results { get; init; } = [];
}

public sealed class SemiAcabadoMaterialDocument101ItemRequest
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
    public string MaterialDocumentItemText { get; init; } = "FugaPET semi-acabado";
}

public sealed class ResultadoPreviewSemiAcabado101
{
    private ResultadoPreviewSemiAcabado101(bool sucesso, string mensagem, SemiAcabadoMaterialDocument101Request? payload, string payloadJson)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Payload = payload;
        PayloadJson = payloadJson;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public SemiAcabadoMaterialDocument101Request? Payload { get; }
    public string PayloadJson { get; }

    public static ResultadoPreviewSemiAcabado101 Ok(SemiAcabadoMaterialDocument101Request payload, string payloadJson)
        => new(true, "Preview Material Document 101 por OP gerado.", payload, payloadJson);

    public static ResultadoPreviewSemiAcabado101 Falha(string mensagem)
        => new(false, mensagem, null, string.Empty);
}

public sealed class ResultadoEnvioSemiAcabado101
{
    private ResultadoEnvioSemiAcabado101(bool sucesso, string mensagem, string? materialDocument, string? materialDocumentYear)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        MaterialDocument = materialDocument;
        MaterialDocumentYear = materialDocumentYear;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public string? MaterialDocument { get; }
    public string? MaterialDocumentYear { get; }

    public static ResultadoEnvioSemiAcabado101 Confirmado(string materialDocument, string materialDocumentYear)
        => new(true, "Semi-acabado enviado ao SAP com sucesso.", materialDocument, materialDocumentYear);

    public static ResultadoEnvioSemiAcabado101 Falha(string mensagem)
        => new(false, mensagem, null, null);
}
