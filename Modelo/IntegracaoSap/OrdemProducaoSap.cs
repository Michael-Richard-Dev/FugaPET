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
    public string Operacao { get; init; } = string.Empty;            // ProductionOrderOperation
    public string OrderOperationInternalId { get; init; } = string.Empty; // OrderOperationInternalID
    public string Sequencia { get; init; } = string.Empty;           // ProductionOrderSequence
    public string CentroTrabalho { get; init; } = string.Empty;      // WorkCenter
    public string Centro { get; init; } = string.Empty;             // Plant
    public string Descricao { get; init; } = string.Empty;          // OperationText
    public decimal QuantidadePrevista { get; init; }                 // OpPlannedTotalQuantity
    public decimal QuantidadeConfirmada { get; init; }               // OpTotalConfirmedYieldQty (pode nao vir)
    public string Unidade { get; init; } = string.Empty;            // OperationUnit
}

/// <summary>Item da OP (to_ProductionOrderItem / A_ProductionOrderItem_2).</summary>
public sealed record ItemOrdemProducaoSap
{
    public string ItemOrdem { get; init; } = string.Empty;           // ManufacturingOrderItem
    public string Material { get; init; } = string.Empty;            // Material
    public string Centro { get; init; } = string.Empty;             // ProductionPlant (pode nao vir)
    public string Deposito { get; init; } = string.Empty;           // StorageLocation
    public decimal QuantidadePrevista { get; init; }                 // MfgOrderItemPlannedTotalQty
    public decimal QuantidadeEntregue { get; init; }                 // MfgOrderItemActualDeliveryQty
    public string Unidade { get; init; } = string.Empty;            // ProductionUnit (pode nao vir)
    public string Lote { get; init; } = string.Empty;               // Batch
}
