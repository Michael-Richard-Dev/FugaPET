namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Item do movimento de CONSUMO 261 (preview) para API_MATERIAL_DOCUMENT_SRV. Tarefa 6: apenas
/// montagem/preview — NAO ha POST. Valores ja normalizados pelo builder (quantidade na cultura
/// invariante). Nao contem segredo.
/// </summary>
public sealed record ConsumoMaterialSap261ItemRequest
{
    /// <summary>Material consumido.</summary>
    public string Material { get; init; } = string.Empty;

    /// <summary>Plant — centro do consumo.</summary>
    public string Plant { get; init; } = string.Empty;

    /// <summary>StorageLocation — deposito de consumo.</summary>
    public string StorageLocation { get; init; } = string.Empty;

    /// <summary>GoodsMovementType — fixo "261" (consumo para ordem de producao).</summary>
    public string GoodsMovementType { get; init; } = "261";

    /// <summary>QuantityInEntryUnit — quantidade consumida local (string, cultura invariante, ex. "5.500").</summary>
    public string QuantityInEntryUnit { get; init; } = string.Empty;

    /// <summary>EntryUnit — unidade (apenas KG nesta etapa).</summary>
    public string EntryUnit { get; init; } = "KG";

    /// <summary>ManufacturingOrder — numero da OP.</summary>
    public string ManufacturingOrder { get; init; } = string.Empty;

    /// <summary>Reservation — numero da reserva (isolado p/ ajuste futuro se a API rejeitar).</summary>
    public string Reservation { get; init; } = string.Empty;

    /// <summary>ReservationItem — item da reserva.</summary>
    public string ReservationItem { get; init; } = string.Empty;

    /// <summary>Batch — lote, quando houver.</summary>
    public string Batch { get; init; } = string.Empty;
}
