namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Ordem de Producao retornada pelo SAP (API_PRODUCTION_ORDER_2_SRV, entidade A_ProductionOrder_2),
/// com os componentes, operacoes e itens expandidos. Mapeamento somente-leitura para a Tela de
/// Consumo de Materia-Prima (Tarefa 3 — apenas GET, sem POST/PATCH/persistencia).
/// </summary>
public sealed record OrdemProducaoSap
{
    public string NumeroOrdem { get; init; } = string.Empty;          // ManufacturingOrder
    public string TipoOrdem { get; init; } = string.Empty;            // ManufacturingOrderType
    public string MaterialProduzido { get; init; } = string.Empty;    // Material
    public string Centro { get; init; } = string.Empty;              // ProductionPlant
    public decimal QuantidadePrevista { get; init; }                  // TotalQuantity
    public string Unidade { get; init; } = string.Empty;             // ProductionUnit
    public string Deposito { get; init; } = string.Empty;            // StorageLocation
    public string Lote { get; init; } = string.Empty;               // Batch

    public bool Liberada { get; init; }                               // OrderIsReleased == "X"
    public bool Confirmada { get; init; }                             // OrderIsConfirmed == "X"
    public bool Excluida { get; init; }                               // OrderIsDeleted == "X"

    /// <summary>
    /// Versao de producao da OP (A_ProductionOrder_2.ProductionVersion). Chave AUTORITATIVA para
    /// resolver o roteiro (via API_PRODUCTION_VERSION), nunca pela primeira ocorrencia de
    /// ProductionRoutingMatlAssgmt. Vazia = fail-closed na classificacao PP_FORM (GATE 048-E REV2).
    /// </summary>
    public string VersaoProducao { get; init; } = string.Empty;       // ProductionVersion

    public DateTime? DataOrdem { get; init; }                         // MfgOrderScheduledStartDate / ... (defensivo)
    public string OrigemDataOrdem { get; init; } = string.Empty;      // campo SAP de origem da data (diagnostico)

    public IReadOnlyList<ComponenteOrdemProducaoSap> Componentes { get; init; } = [];
    public IReadOnlyList<OperacaoOrdemProducaoSap> Operacoes { get; init; } = [];
    public IReadOnlyList<ItemOrdemProducaoSap> Itens { get; init; } = [];
}

/// <summary>Componente da OP (to_ProductionOrderComponent / A_ProductionOrderComponent_2).</summary>
public sealed record ComponenteOrdemProducaoSap
{
    public string NumeroOrdem { get; init; } = string.Empty;          // ManufacturingOrder
    public string Reserva { get; init; } = string.Empty;             // Reservation
    public string ItemReserva { get; init; } = string.Empty;         // ReservationItem
    public string Material { get; init; } = string.Empty;            // Material
    public string Centro { get; init; } = string.Empty;             // Plant
    public string Deposito { get; init; } = string.Empty;           // StorageLocation
    public decimal QuantidadeNecessaria { get; init; }               // RequiredQuantity
    public string UnidadeBase { get; init; } = string.Empty;         // BaseUnit
    public decimal QuantidadeRetirada { get; init; }                 // WithdrawnQuantity
    public decimal QuantidadeDisponivelConfirmada { get; init; }     // ConfirmedAvailableQuantity

    // GATE 118B: espelhos TRI-STATE dos mesmos campos SAP. As tres propriedades acima sao legado
    // (decimal nao-nullable) e colapsam ausente/null em 0 — comportamento preservado para a Tela de
    // Consumo. O alocador 261 do Produto Acabado consome EXCLUSIVAMENTE as versoes abaixo, onde
    // null significa AUSENTE/INDETERMINADO e NUNCA zero.
    public decimal? QuantidadeNecessariaSap { get; init; }           // RequiredQuantity      Decimal(13,3)
    public decimal? QuantidadeRetiradaSap { get; init; }             // WithdrawnQuantity     Decimal(13,3)
    public decimal? QuantidadeDisponivelConfirmadaSap { get; init; } // ConfirmedAvailableQuantity Decimal(15,3)

    /// <summary>
    /// QuantityIsFixed (Edm.Boolean, Nullable=true). Semantica provada no gate 118B-A1 pela
    /// documentacao SAP da propria entidade A_ProductionOrderComponent_2:
    /// false = quantidade PROPORCIONAL a quantidade da ordem; true = quantidade CONSTANTE.
    /// null = AUSENTE/INDETERMINADO. Proibido converter null em false.
    /// </summary>
    public bool? QuantidadeFixa { get; init; }                       // QuantityIsFixed
    public string TipoMovimento { get; init; } = string.Empty;       // GoodsMovementType
    public string Lote { get; init; } = string.Empty;               // Batch
    public string ItemBOM { get; init; } = string.Empty;            // BOMItem
    public string CategoriaItemBOM { get; init; } = string.Empty;    // BOMItemCategory
    public bool ReservaFinalizada { get; init; }                     // ReservationIsFinallyIssued
    public bool MarcadoParaEliminacao { get; init; }                 // MatlCompIsMarkedForDeletion
    public bool MaterialGranel { get; init; }                        // IsBulkMaterialComponent
    public bool BackflushSap { get; init; }                          // MatlCompIsMarkedForBackflush
    public string TipoSplitLote { get; init; } = string.Empty;       // BatchSplitType
    public string Operacao { get; init; } = string.Empty;            // ManufacturingOrderOperation
    public string SequenciaOperacao { get; init; } = string.Empty;   // ManufacturingOrderSequence
    public string OrderOperationInternalId { get; init; } = string.Empty; // OrderOperationInternalID
}

/// <summary>Operacao da OP (to_ProductionOrderOperation / A_ProductionOrderOperation_2).</summary>
public sealed record OperacaoOrdemProducaoSap
{
    public string Operacao { get; init; } = string.Empty;            // ManufacturingOrderOperation
    public string OrderOperationInternalId { get; init; } = string.Empty; // OrderIntBillOfOperationsItem (aliases defensivos: OrderOperationInternalID/Id, ManufacturingOrderOperationInternalID)
    public string Sequencia { get; init; } = string.Empty;           // ManufacturingOrderSequence

    /// <summary>
    /// Suboperação (ManufacturingOrderSubOperation / ProductionOrderSubOperation). LEITURA DEFENSIVA:
    /// fica VAZIA quando o SAP não retorna o campo — o metadata da Fuga ainda não confirmou sua existência.
    /// Nenhum consumo existente depende dela; compõe a chave técnica da operação quando disponível.
    /// </summary>
    public string Suboperacao { get; init; } = string.Empty;

    public string CentroTrabalho { get; init; } = string.Empty;      // WorkCenter
    public string WorkCenterInternalId { get; init; } = string.Empty; // WorkCenterInternalID (A_ProductionOrderOperation_2)
    public string WorkCenterTypeCode { get; init; } = string.Empty;   // WorkCenterTypeCode
    public string Centro { get; init; } = string.Empty;             // ProductionPlant
    public string Descricao { get; init; } = string.Empty;          // MfgOrderOperationText
    public decimal QuantidadePrevista { get; init; }                 // OpPlannedTotalQuantity
    public decimal QuantidadeConfirmada { get; init; }               // OpTotalConfirmedYieldQty (pode nao vir)
    public string Unidade { get; init; } = string.Empty;            // OperationUnit

    /// <summary>
    /// Marcador SAP Standard Text Code (ProductionRoutingOperation.OperationStandardTextCode) do roteiro
    /// AUTORITATIVO da OP. NAO vem de A_ProductionOrderOperation_2 (essa API nao expoe isoladamente): e
    /// preenchido pelo SERVICO do Controle de Apontamentos apos cruzar a operacao com o roteiro. Valor
    /// "PP_FORM" = operacao MANUAL FugaPET. NAO confundir com OperationControlProfile (YBP1/QM01...).
    /// </summary>
    public string CodigoTextoPadrao { get; init; } = string.Empty;   // OperationStandardTextCode (marcador PP_FORM)
}

/// <summary>Item da OP (to_ProductionOrderItem / A_ProductionOrderItem_2).</summary>
public sealed record ItemOrdemProducaoSap
{
    /// <summary>
    /// GATE 118B-R4: OP REALMENTE RETORNADA pelo SAP no item (ManufacturingOrder), parte da KEY de
    /// A_ProductionOrderItem_2. Antes era descartada no mapeamento, o que tornava a checagem
    /// cruzada "OP solicitada x OP do item" TAUTOLOGICA (comparava a request consigo mesma).
    /// Vazia = AUSENTE; nunca e preenchida por fallback da OP solicitada.
    /// </summary>
    public string NumeroOrdem { get; init; } = string.Empty;         // ManufacturingOrder

    public string ItemOrdem { get; init; } = string.Empty;           // ManufacturingOrderItem
    public string Material { get; init; } = string.Empty;            // Material
    public string Centro { get; init; } = string.Empty;             // ProductionPlant (pode nao vir)
    public string Deposito { get; init; } = string.Empty;           // StorageLocation
    public decimal QuantidadePrevista { get; init; }                 // MfgOrderItemPlannedTotalQty

    // GATE 118B / 118B-A1: MfgOrderItemActualDeliveryQty NAO EXISTE em A_ProductionOrderItem_2
    // (zero ocorrencias no $metadata real de API_PRODUCTION_ORDER_2_SRV em QAS110). A propriedade
    // antiga QuantidadeEntregue era alimentada por esse campo inexistente e, por isso, valia SEMPRE 0.
    // Foi REMOVIDA em vez de reapontada: o contrato proibe tratar a ausencia como 0, como default ou
    // como equivalente funcional de MfgOrderItemGoodsReceiptQty.
    // O campo autoritativo de producao ja recebida e o abaixo, TRI-STATE.

    /// <summary>
    /// MfgOrderItemGoodsReceiptQty (Edm.Decimal, Precision=13, Scale=3, Nullable=true).
    /// Quantidade JA RECEBIDA do item da ordem. null = AUSENTE/INDETERMINADO, nunca zero.
    /// </summary>
    public decimal? QuantidadeRecebidaSap { get; init; }             // MfgOrderItemGoodsReceiptQty

    /// <summary>MfgOrderItemPlannedTotalQty em TRI-STATE (null = ausente/indeterminado).</summary>
    public decimal? QuantidadePrevistaSap { get; init; }             // MfgOrderItemPlannedTotalQty

    public string Unidade { get; init; } = string.Empty;            // ProductionUnit (pode nao vir)
    public string Lote { get; init; } = string.Empty;               // Batch
}

