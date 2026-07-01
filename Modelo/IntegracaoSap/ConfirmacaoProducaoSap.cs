namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Request real controlado para POST em API_PROD_ORDER_CONFIRMATION_2_SRV/ProdnOrdConf2.
/// Serializacao final OData V2 fica no builder/client para preservar /Date(ms)/ e results.
/// </summary>
public sealed class ConfirmacaoProducaoSapRequest
{
    public string Aviso { get; init; } = string.Empty;
    public string OrdemProducao { get; init; } = string.Empty;
    public string MaterialProduzido { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string SequenciaOperacao { get; init; } = string.Empty;
    public string Reserva { get; init; } = string.Empty;
    public string ItemReserva { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string Deposito { get; init; } = string.Empty;
    public decimal QuantidadeConsumida { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public string StatusLocal { get; init; } = string.Empty;

    public string OrderID { get; init; } = string.Empty;
    public string ManufacturingOrder { get; init; } = string.Empty;
    public string Sequence { get; init; } = string.Empty;
    public string OrderOperation { get; init; } = string.Empty;
    public string OrderOperationInternalID { get; init; } = string.Empty;
    public string ConfirmationYieldQuantity { get; init; } = string.Empty;
    public string ConfirmationUnit { get; init; } = string.Empty;
    public string ConfirmationText { get; init; } = string.Empty;
    public DateTime PostingDate { get; init; }
    public bool IsFinalConfirmation { get; init; }
    public string Plant { get; init; } = string.Empty;
    public bool GoodsMovementIsFinallyPosted { get; init; } = true;
    public IReadOnlyList<ConfirmacaoProducaoSapItemRequest> ToProdnOrdConfMatlDocItm { get; init; } = [];
}

public sealed class ConfirmacaoProducaoSapItemRequest
{
    public string Material { get; init; } = string.Empty;
    public string ManufacturingOrder { get; init; } = string.Empty;
    public string Reservation { get; init; } = string.Empty;
    public string ReservationItem { get; init; } = string.Empty;
    public string GoodsMovementType { get; init; } = "261";
    public string QuantityInEntryUnit { get; init; } = string.Empty;
    public string EntryUnit { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public string Batch { get; init; } = string.Empty;
    // Tarefa 17.8: tipo de documento de referencia do movimento na CONFIRMACAO (exemplo real SAP = "F").
    // NAO confundir com o "B" da Entrada (Material Document 101) — fluxo distinto.
    public string GoodsMovementRefDocType { get; init; } = "F";
    public string GoodsMovementReasonCode { get; init; } = string.Empty;
    public string InventoryValuationType { get; init; } = string.Empty;
}

public sealed class OperacaoConfirmacaoSap
{
    public string OrderId { get; init; } = string.Empty;
    public string Sequence { get; init; } = string.Empty;
    public string OrderOperation { get; init; } = string.Empty;
    public string OrderOperationInternalId { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public string WorkCenter { get; init; } = string.Empty;
    public string ConfirmationUnit { get; init; } = string.Empty;
}

public sealed class ConfirmacaoProducaoSapResponse
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string? ConfirmationGroup { get; init; }
    public string? ConfirmationCount { get; init; }
    public string? ManufacturingOrder { get; init; }
    public string? MaterialDocument { get; init; }
    public string? MaterialDocumentYear { get; init; }
    public bool GoodsMovementIsFinallyPosted { get; init; }
    public int? StatusHttp { get; init; }
    public string? CorrelationId { get; init; }

    public static ConfirmacaoProducaoSapResponse Ok(
        string confirmationGroup,
        string confirmationCount,
        string manufacturingOrder,
        string? materialDocument,
        string? materialDocumentYear,
        bool goodsMovementIsFinallyPosted,
        int? statusHttp,
        string? correlationId)
        => new()
        {
            Sucesso = true,
            ConfirmationGroup = confirmationGroup,
            ConfirmationCount = confirmationCount,
            ManufacturingOrder = manufacturingOrder,
            MaterialDocument = materialDocument,
            MaterialDocumentYear = materialDocumentYear,
            GoodsMovementIsFinallyPosted = goodsMovementIsFinallyPosted,
            StatusHttp = statusHttp,
            CorrelationId = correlationId,
            Mensagem = $"Confirmação de Produção enviada ao SAP. Grupo {confirmationGroup}/{confirmationCount}."
        };

    public static ConfirmacaoProducaoSapResponse Falha(string mensagem, int? statusHttp = null, string? correlationId = null)
        => new() { Sucesso = false, Mensagem = mensagem, StatusHttp = statusHttp, CorrelationId = correlationId };
}

public sealed class ResultadoEnvioConfirmacaoProducao
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string? ConfirmationGroup { get; init; }
    public string? ConfirmationCount { get; init; }
    public string? ManufacturingOrder { get; init; }
    public string? DocumentoMaterialSap { get; init; }
    public string? ExercicioDocumentoMaterialSap { get; init; }
    public int? StatusHttp { get; init; }
    public string? CorrelationId { get; init; }

    public static ResultadoEnvioConfirmacaoProducao Ok(ConfirmacaoProducaoSapResponse resposta)
        => new()
        {
            Sucesso = true,
            Mensagem = resposta.Mensagem,
            ConfirmationGroup = resposta.ConfirmationGroup,
            ConfirmationCount = resposta.ConfirmationCount,
            ManufacturingOrder = resposta.ManufacturingOrder,
            DocumentoMaterialSap = resposta.MaterialDocument,
            ExercicioDocumentoMaterialSap = resposta.MaterialDocumentYear,
            StatusHttp = resposta.StatusHttp,
            CorrelationId = resposta.CorrelationId
        };

    public static ResultadoEnvioConfirmacaoProducao Falha(string mensagem, int? statusHttp = null, string? correlationId = null)
        => new() { Sucesso = false, Mensagem = mensagem, StatusHttp = statusHttp, CorrelationId = correlationId };
}

public sealed class ResultadoPreviewConfirmacaoProducaoSap
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public ConfirmacaoProducaoSapRequest? Payload { get; init; }
    public string PayloadJson { get; init; } = string.Empty;
    public IReadOnlyList<string> ErrosValidacao { get; init; } = [];

    public static ResultadoPreviewConfirmacaoProducaoSap Falha(string mensagem, IReadOnlyList<string>? erros = null)
        => new() { Sucesso = false, Mensagem = mensagem, ErrosValidacao = erros ?? [] };

    public static ResultadoPreviewConfirmacaoProducaoSap Ok(ConfirmacaoProducaoSapRequest payload, string payloadJson, string mensagem)
        => new() { Sucesso = true, Payload = payload, PayloadJson = payloadJson, Mensagem = mensagem };
}
