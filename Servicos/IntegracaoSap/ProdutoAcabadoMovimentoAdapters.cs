using System.Globalization;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Wrappers ESTRUTURAIS (zero mudanÃ§a de comportamento) sobre os clientes SAP homologados.</summary>
public sealed class ConsumoMaterialSap261ClientAdapter(ConsumoMaterialSap261ApiClient cliente) : IConsumoMaterialSap261Client
{
    public Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(ConsumoMaterialSap261Request requisicao, string correlationId, CancellationToken cancellationToken = default)
        => cliente.EnviarConsumo261Async(requisicao, correlationId, cancellationToken);
}

public sealed class MaterialDocumentSapClientAdapter(MaterialDocumentSapApiClient cliente) : IMaterialDocumentSapClient
{
    public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterialAsync(MaterialDocumentSapRequest requisicao, CancellationToken cancellationToken = default)
        => cliente.CriarDocumentoMaterialAsync(requisicao, cancellationToken);
}

/// <summary>
/// Adaptador FINO Produto Acabado â†’ 261 HOMOLOGADO usando o CLIENTE 261 diretamente (nÃ£o o serviÃ§o de
/// Consumo). Assim o gate PA <c>ProdutoAcabadoMaterialDocumentWriteHabilitado</c> libera o 261 SEM depender do
/// gate genÃ©rico FUGAPET_SAP_WRITE_ENABLED (que continua false). CSRF/cookies/HTTPS/allowlist/serializaÃ§Ã£o sÃ£o
/// os do cliente existente. Bloqueio PRÃ‰-HTTP (gate/dados) â‡’ ERRO SEGURO (nunca timeout indeterminado);
/// timeout/rede apÃ³s possÃ­vel POST â‡’ indeterminado. Zero retry. Suporta N componentes (um request, N itens).
/// </summary>
public sealed class ProdutoAcabadoMovimento261Adapter : IProdutoAcabadoMovimento261Gateway
{
    private readonly IConsumoMaterialSap261Client? _cliente261;
    private readonly bool _gatePaHabilitado;

    /// <summary>Runtime: cliente 261 pode ser null quando a config SAP nÃ£o permite construÃ­-lo â‡’ fail-closed.</summary>
    public ProdutoAcabadoMovimento261Adapter(IConsumoMaterialSap261Client? cliente261, bool gatePaHabilitado)
    {
        _cliente261 = cliente261;
        _gatePaHabilitado = gatePaHabilitado;
    }

    public async Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento261Command comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (!_gatePaHabilitado || _cliente261 is null)
        {
            return ResultadoMovimentoSap.Bloqueado(
                "Movimento 261 do Produto Acabado nÃ£o autorizado (FUGAPET_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED=false ou cliente ausente).");
        }

        if (comando.Itens.Count == 0 || string.IsNullOrWhiteSpace(comando.NumeroOrdem))
        {
            return ResultadoMovimentoSap.Bloqueado("261 sem OP ou sem componentes: fail-closed (nenhum POST).");
        }

        // REV4-Â§9: correlaÃ§Ã£o POR CAIXA/tentativa obrigatÃ³ria (distingue caixas da mesma OP). Vazia â‡’ fail-closed.
        if (string.IsNullOrWhiteSpace(comando.CorrelationId))
        {
            return ResultadoMovimentoSap.Bloqueado(
                "261 sem CorrelationId por caixa/tentativa: DEPENDENCIA_GAIA (nenhum POST).");
        }

        // REV4-Â§8: datas obrigatÃ³rias e reais â€” NUNCA serializar DateTime.MinValue. Ausentes â‡’ fail-closed, zero HTTP.
        if (comando.PostingDate is not DateTime posting || posting == DateTime.MinValue
            || comando.DocumentDate is not DateTime document || document == DateTime.MinValue)
        {
            return ResultadoMovimentoSap.Bloqueado(
                "261 sem PostingDate/DocumentDate reais: DEPENDENCIA_ARES (nenhum POST).");
        }

        // REV4-Â§7: cada componente precisa de TODOS os obrigatÃ³rios, INCLUSIVE Unidade (sem fallback "KG").
        // Ausente â‡’ bloqueio (DEPENDENCIA_FUNCIONAL/ARES), sem HTTP.
        if (comando.Itens.Any(i => string.IsNullOrWhiteSpace(i.Material) || string.IsNullOrWhiteSpace(i.Plant)
            || string.IsNullOrWhiteSpace(i.StorageLocation) || i.Quantidade <= 0m || string.IsNullOrWhiteSpace(i.Unidade)
            || string.IsNullOrWhiteSpace(i.Reservation) || string.IsNullOrWhiteSpace(i.ReservationItem)))
        {
            return ResultadoMovimentoSap.Bloqueado(
                "Componente do 261 incompleto (Material/Plant/StorageLocation/Quantidade/Unidade/Reservation/ReservationItem): DEPENDENCIA_FUNCIONAL.");
        }

        ConsumoMaterialSap261Request requisicao = MapearRequisicao(comando);
        ResultadoEnvioConsumoSap261 r = await _cliente261.EnviarConsumo261Async(
            requisicao, correlationId: comando.CorrelationId, cancellationToken);

        if (r.Sucesso)
        {
            return ResultadoMovimentoSap.Confirmado(
                r.DocumentoMaterialSap ?? string.Empty, r.ExercicioDocumentoMaterialSap ?? string.Empty, r.StatusHttp);
        }

        return r.ResultadoIndeterminado
            ? ResultadoMovimentoSap.Indeterminado(r.Mensagem, r.StatusHttp)
            : ResultadoMovimentoSap.Erro(r.Mensagem, r.StatusHttp);
    }

    /// <summary>Mapeia o command PA (N componentes) para o request 261 EXISTENTE â€” valores/serializaÃ§Ã£o homologados.</summary>
    internal static ConsumoMaterialSap261Request MapearRequisicao(ProdutoAcabadoMovimento261Command c)
        => new()
        {
            // GoodsMovementCode "03" = default do record homologado (nÃ£o sobrescrever).
            // REV4-Â§8: datas jÃ¡ validadas nÃ£o-nulas/reais pelo adaptador antes de mapear.
            PostingDate = c.PostingDate!.Value,
            DocumentDate = c.DocumentDate!.Value,
            MaterialDocumentHeaderText = c.TextoCabecalho,
            ToMaterialDocumentItem = c.Itens
                .Select(item => new ConsumoMaterialSap261ItemRequest
                {
                    // GoodsMovementType "261" default do record homologado.
                    Material = item.Material,
                    Plant = item.Plant,
                    StorageLocation = item.StorageLocation,
                    QuantityInEntryUnit = item.Quantidade.ToString("0.###", CultureInfo.InvariantCulture),
                    EntryUnit = item.Unidade, // REV4-Â§7: sem fallback "KG" (adaptador jÃ¡ bloqueou Unidade vazia)
                    ManufacturingOrder = c.NumeroOrdem,
                    Reservation = item.Reservation,
                    ReservationItem = item.ReservationItem,
                    Batch = item.Batch
                })
                .ToArray()
        };
}

/// <summary>
/// Gateway do 101 do Produto Acabado usando a stack MaterialDocument HOMOLOGADA (cliente/serializaÃ§Ã£o/CSRF),
/// gated pelo flag PA (nÃ£o pelo genÃ©rico). Contrato CONFIRMADO: GoodsMovementCode "02", GoodsMovementType "101",
/// GoodsMovementRefDocType "F" (sem PurchaseOrder). Campos ainda pendentes (ManufacturingOrderItem/
/// QuantityInEntryUnit/EntryUnit/Batch/datas) sÃ£o obrigatÃ³rios do command; ausentes â‡’ DEPENDENCIA_ARES, ZERO HTTP.
/// Sucesso exige MaterialDocument + MaterialDocumentYear. Zero retry.
/// </summary>
public sealed class ProdutoAcabadoMovimento101Gateway : IProdutoAcabadoMovimento101Gateway
{
    public const string GoodsMovementCodePa = "02";
    public const string GoodsMovementTypePa = "101";
    public const string GoodsMovementRefDocTypePa = "F";

    private readonly IMaterialDocumentSapClient? _cliente;
    private readonly bool _gatePaHabilitado;

    /// <summary>Runtime: injeta o cliente MaterialDocument (via wrapper). Gate false â‡’ fail-closed.</summary>
    public ProdutoAcabadoMovimento101Gateway(IMaterialDocumentSapClient? cliente, bool gatePaHabilitado)
    {
        _cliente = cliente;
        _gatePaHabilitado = gatePaHabilitado;
    }

    public async Task<ResultadoMovimentoSap> EnviarAsync(ProdutoAcabadoMovimento101Command comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        if (!_gatePaHabilitado || _cliente is null)
        {
            return ResultadoMovimentoSap.Bloqueado(
                "Movimento 101 do Produto Acabado nÃ£o autorizado (FUGAPET_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED=false ou cliente ausente).");
        }

        // DEPENDENCIA_ARES: campos de massa/contrato ainda nÃ£o fechados. Ausentes â‡’ zero HTTP.
        if (string.IsNullOrWhiteSpace(comando.NumeroOrdem) || string.IsNullOrWhiteSpace(comando.Material)
            || string.IsNullOrWhiteSpace(comando.Plant) || string.IsNullOrWhiteSpace(comando.StorageLocation)
            || string.IsNullOrWhiteSpace(comando.ManufacturingOrderItem) || string.IsNullOrWhiteSpace(comando.QuantityInEntryUnit)
            || string.IsNullOrWhiteSpace(comando.EntryUnit) || string.IsNullOrWhiteSpace(comando.Batch)
            || comando.PostingDate is null || comando.DocumentDate is null)
        {
            return ResultadoMovimentoSap.Bloqueado(
                "Campos obrigatÃ³rios do 101 de Produto Acabado ausentes (ManufacturingOrderItem/Quantity/EntryUnit/Batch/datas): DEPENDENCIA_ARES. Nenhum POST.");
        }

        MaterialDocumentSapRequest requisicao = MontarRequisicao(comando);
        ResultadoMaterialDocumentSap r = await _cliente.CriarDocumentoMaterialAsync(requisicao, cancellationToken);

        if (r.Sucesso)
        {
            return ResultadoMovimentoSap.ClassificarSucesso(r.MaterialDocument, r.MaterialDocumentYear, r.StatusHttp);
        }

        // Sem status HTTP OU 5xx/408 â‡’ indeterminado (pode ter transmitido); 4xx de negÃ³cio â‡’ erro comprovado.
        return r.StatusHttp is not int st || st is (>= 500 and <= 599) or 408
            ? ResultadoMovimentoSap.Indeterminado(r.MensagemSanitizada, r.StatusHttp)
            : ResultadoMovimentoSap.Erro(r.MensagemSanitizada, r.StatusHttp);
    }

    internal static MaterialDocumentSapRequest MontarRequisicao(ProdutoAcabadoMovimento101Command c)
        => new()
        {
            GoodsMovementCode = GoodsMovementCodePa, // "02"
            PostingDate = c.PostingDate!.Value,
            DocumentDate = c.DocumentDate!.Value,
            MaterialDocumentHeaderText = c.TextoCabecalho,
            Itens =
            [
                new MaterialDocumentSapItemRequest
                {
                    Material = c.Material,
                    Plant = c.Plant,
                    StorageLocation = c.StorageLocation,
                    GoodsMovementType = GoodsMovementTypePa,           // "101"
                    GoodsMovementRefDocType = GoodsMovementRefDocTypePa, // "F"
                    ManufacturingOrder = c.NumeroOrdem,
                    ManufacturingOrderItem = c.ManufacturingOrderItem,
                    QuantityInEntryUnit = c.QuantityInEntryUnit,
                    EntryUnit = c.EntryUnit,
                    Batch = c.Batch
                    // NÃƒO usa PurchaseOrder/PurchaseOrderItem (fluxo de Entrada).
                }
            ]
        };
}


