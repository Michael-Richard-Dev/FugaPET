using System.Text.Json.Serialization;

namespace FugaPET_Dev.Modelo.IntegracaoSap;

public sealed class ProdutoAcabadoPaleteRequest
{
    public string HandlingUnitExternalID { get; init; } = string.Empty;
    public decimal GrossWeight { get; init; }
    public decimal NetWeight { get; init; }
    public decimal TareWeight { get; init; }
    public string WeightUnit { get; init; } = "KG";
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public string PackagingMaterial { get; init; } = string.Empty;

    [JsonPropertyName("_HandlingUnitItem")]
    public IReadOnlyList<ProdutoAcabadoPaleteItemRequest> HandlingUnitItems { get; init; } = [];
}

public sealed class ProdutoAcabadoPaleteItemRequest
{
    public string HandlingUnit { get; init; } = string.Empty;
}

public sealed class ResultadoPreviewProdutoAcabadoPalete
{
    private ResultadoPreviewProdutoAcabadoPalete(bool sucesso, string mensagem, ProdutoAcabadoPaleteRequest? payload, string payloadJson)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Payload = payload;
        PayloadJson = payloadJson;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public ProdutoAcabadoPaleteRequest? Payload { get; }
    public string PayloadJson { get; }

    public static ResultadoPreviewProdutoAcabadoPalete Ok(ProdutoAcabadoPaleteRequest payload, string payloadJson)
        => new(true, "Preview de formação de palete gerado.", payload, payloadJson);

    public static ResultadoPreviewProdutoAcabadoPalete Falha(string mensagem)
        => new(false, mensagem, null, string.Empty);
}
