using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Item (componente) do movimento 261 do Produto Acabado. Um request 261 pode ter N itens.
/// REV4-§7: nenhuma inferência — <see cref="Unidade"/> inicia VAZIA (jamais assume "KG"). Todos os campos
/// abaixo são OBRIGATÓRIOS por item; ausência ⇒ fail-closed no adaptador (zero HTTP). Origem real de
/// Reservation/ReservationItem/Batch = DEPENDENCIA_FUNCIONAL/ARES; não inventar.
/// </summary>
public sealed record ProdutoAcabadoMovimento261ItemCommand
{
    public string Material { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = string.Empty;          // REV4-§7: SEM fallback "KG"; vazia ⇒ fail-closed
    public string Reservation { get; init; } = string.Empty;       // DEPENDENCIA_FUNCIONAL enquanto origem real não comprovada
    public string ReservationItem { get; init; } = string.Empty;   // idem
    public string Batch { get; init; } = string.Empty;
}

/// <summary>
/// Command interno do 261 do Produto Acabado — CABEÇALHO + N COMPONENTES. Mapeado 1:1 para o
/// <c>ConsumoMaterialSap261Request</c> HOMOLOGADO (todos os itens em ToMaterialDocumentItem). Não puxa regras
/// funcionais do Consumo; não altera o request/serialização homologados.
/// REV4-§8: <see cref="PostingDate"/>/<see cref="DocumentDate"/> são <c>DateTime?</c> — ausentes ⇒ fail-closed
/// (NUNCA serializa DateTime.MinValue). REV4-§9: <see cref="CorrelationId"/> obrigatório e POR CAIXA/tentativa.
/// </summary>
public sealed record ProdutoAcabadoMovimento261Command
{
    public string NumeroOrdem { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;    // REV4-§9: por caixa/tentativa; vazio ⇒ fail-closed
    public DateTime? PostingDate { get; init; }                    // REV4-§8: ausente ⇒ fail-closed
    public DateTime? DocumentDate { get; init; }                   // REV4-§8: ausente ⇒ fail-closed
    public string TextoCabecalho { get; init; } = string.Empty;
    public IReadOnlyList<ProdutoAcabadoMovimento261ItemCommand> Itens { get; init; } = [];
}

/// <summary>
/// Command interno do 101 do Produto Acabado. Contrato CONFIRMADO: GoodsMovementCode "02", GoodsMovementType
/// "101", GoodsMovementRefDocType "F", Material/Plant/StorageLocation, ManufacturingOrder. Ainda pendentes
/// (obrigatórios do command; ausência ⇒ DEPENDENCIA_ARES, zero HTTP): ManufacturingOrderItem, QuantityInEntryUnit,
/// EntryUnit, Batch, PostingDate/DocumentDate. NÃO usa PurchaseOrder.
/// </summary>
public sealed record ProdutoAcabadoMovimento101Command
{
    public string NumeroOrdem { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;
    public string Plant { get; init; } = string.Empty;
    public string StorageLocation { get; init; } = string.Empty;
    public string ManufacturingOrderItem { get; init; } = string.Empty; // DEPENDENCIA_ARES (massa/contrato)
    public string QuantityInEntryUnit { get; init; } = string.Empty;    // DEPENDENCIA_ARES
    public string EntryUnit { get; init; } = string.Empty;              // DEPENDENCIA_ARES
    public string Batch { get; init; } = string.Empty;                  // DEPENDENCIA_ARES
    public DateTime? PostingDate { get; init; }                         // DEPENDENCIA_ARES
    public DateTime? DocumentDate { get; init; }                        // DEPENDENCIA_ARES
    public string TextoCabecalho { get; init; } = string.Empty;
}

/// <summary>Gateway fino do 261 de Produto Acabado. Fail-closed pelo gate PA (não pelo gate genérico do Consumo).</summary>
public interface IProdutoAcabadoMovimento261Gateway
{
    Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento261Command comando, CancellationToken cancellationToken = default);
}

/// <summary>Gateway do 101 de Produto Acabado (reusa a stack MaterialDocument). Fail-closed por gate/DEPENDENCIA_ARES.</summary>
public interface IProdutoAcabadoMovimento101Gateway
{
    Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento101Command comando, CancellationToken cancellationToken = default);
}

/// <summary>Adaptador FINO sobre o ProdutoAcabadoHuService HOMOLOGADO (claim/timeout/estado/centro 3007 preservados).</summary>
public interface IProdutoAcabadoHuEnvio
{
    bool EnvioAutorizado { get; }
    Task<StatusIntegracaoCaixa> EnviarHuAsync(long codigoCaixa, long usuario, string terminal, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface ESTRUTURAL fina sobre o cliente 261 HOMOLOGADO (<c>ConsumoMaterialSap261ApiClient</c>): mesmo
/// método/assinatura, zero mudança de payload/HTTP/CSRF/cookies. Permite a camada PA reutilizar o cliente
/// SEM passar pelo gate genérico do serviço de Consumo. Testes injetam fake.
/// </summary>
public interface IConsumoMaterialSap261Client
{
    Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
        ConsumoMaterialSap261Request requisicao, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>Interface ESTRUTURAL fina sobre <c>MaterialDocumentSapApiClient</c> (mesma assinatura). Testes injetam fake.</summary>
public interface IMaterialDocumentSapClient
{
    Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterialAsync(
        MaterialDocumentSapRequest requisicao, CancellationToken cancellationToken = default);
}
