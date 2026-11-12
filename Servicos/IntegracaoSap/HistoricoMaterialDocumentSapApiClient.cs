using System.Text.Json;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE R6 / contrato R5A (ARES): cliente SOMENTE LEITURA do historico de documentos de material
/// (API_MATERIAL_DOCUMENT_SRV / A_MaterialDocumentItem, OData V2). Monta a URL e interpreta o envelope
/// <c>{ "d": { "results": [...], "__next": "..." } }</c>. NAO faz HTTP: a camada de servico cuida da
/// composicao, paginacao e validacao de destino. Sem POST, sem PATCH, sem CSRF.
/// <para>
/// NOMES DE CAMPO: concentrados em <see cref="Campos"/> para que uma correcao de contrato seja pontual.
/// Campo necessario AUSENTE no envelope NAO e tratado como "ausencia de movimento": o documento e
/// classificado como INCERTO, o que bloqueia a jusante. Nunca se presume correspondencia de estorno.
/// </para>
/// </summary>
public static class HistoricoMaterialDocumentSapApiClient
{
    public const string CaminhoEntidade = "A_MaterialDocumentItem";

    /// <summary>Tipos de movimento relevantes ao recebimento por pedido de compra.</summary>
    public const string MovimentoEntrada101 = "101";
    public const string MovimentoEstorno102 = "102";

    /// <summary>GATE R6: nomes dos campos do contrato R5A, isolados para correcao pontual.</summary>
    public static class Campos
    {
        public const string PurchaseOrder = "PurchaseOrder";
        public const string PurchaseOrderItem = "PurchaseOrderItem";
        public const string Material = "Material";
        public const string Plant = "Plant";
        public const string GoodsMovementType = "GoodsMovementType";
        public const string MaterialDocument = "MaterialDocument";
        public const string MaterialDocumentItem = "MaterialDocumentItem";
        public const string MaterialDocumentYear = "MaterialDocumentYear";
        /// <summary>Vinculo DOCUMENTAL do estorno. Sem ele, o estorno nao e correlacionado por suposicao.</summary>
        public const string ReversedMaterialDocument = "ReversedMaterialDocument";
        public const string ReversedMaterialDocumentItem = "ReversedMaterialDocumentItem";
        public const string IsReversalMovement = "IsReversalMovement";
    }

    private static readonly string[] _selecionados =
    [
        Campos.PurchaseOrder, Campos.PurchaseOrderItem, Campos.Material, Campos.Plant,
        Campos.GoodsMovementType, Campos.MaterialDocument, Campos.MaterialDocumentItem,
        Campos.MaterialDocumentYear, Campos.ReversedMaterialDocument,
        Campos.ReversedMaterialDocumentItem, Campos.IsReversalMovement
    ];

    /// <summary>
    /// Normaliza o item do pedido para COMPARACAO: "10", "00010" e " 010 " sao o MESMO item. Preserva
    /// texto (nunca converte para numero); apenas remove espacos e zeros a esquerda.
    /// </summary>
    public static string NormalizarItemPedido(string? numeroItem)
    {
        string bruto = (numeroItem ?? string.Empty).Trim();
        if (bruto.Length == 0)
        {
            return string.Empty;
        }

        string semZeros = bruto.TrimStart('0');
        return semZeros.Length == 0 ? "0" : semZeros;
    }

    public static bool MesmoItemPedido(string? esquerda, string? direita)
        => NormalizarItemPedido(esquerda).Length > 0
           && string.Equals(NormalizarItemPedido(esquerda), NormalizarItemPedido(direita), StringComparison.Ordinal);

    /// <summary>
    /// GET: A_MaterialDocumentItem?$filter=PurchaseOrder eq '...'&amp;$select=...&amp;$format=json&amp;sap-client=n
    /// <para>
    /// O filtro usa SOMENTE PurchaseOrder: o item do pedido e filtrado em memoria pela normalizacao
    /// (00010 == 10), porque a representacao do item no SAP nao e garantida. Isso AMPLIA o conjunto
    /// lido — nunca o reduz — preservando a deteccao de movimentos do item.
    /// </para>
    /// </summary>
    public static Uri MontarUrlHistoricoPorPedido(Uri baseUri, string numeroPedido, string? sapClient = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        string pedido = (numeroPedido ?? string.Empty).Trim();
        if (pedido.Length == 0)
        {
            throw new ArgumentException("Numero do pedido obrigatorio para consultar o historico.", nameof(numeroPedido));
        }

        string baseSemBarra = baseUri.ToString().TrimEnd('/');
        string cliente = string.IsNullOrWhiteSpace(sapClient) ? string.Empty : $"&sap-client={sapClient.Trim()}";
        string filtro = Uri.EscapeDataString($"{Campos.PurchaseOrder} eq '{pedido}'");
        string select = Uri.EscapeDataString(string.Join(',', _selecionados));
        return new Uri($"{baseSemBarra}/{CaminhoEntidade}?$filter={filtro}&$select={select}&$format=json{cliente}");
    }

    /// <summary>
    /// Interpreta UMA pagina do envelope OData V2. Envelope invalido (sem <c>d</c> ou sem
    /// <c>results</c> em array) ⇒ <see cref="PaginaHistoricoMaterial.Invalida"/>.
    /// </summary>
    public static PaginaHistoricoMaterial InterpretarPagina(string corpoJson, string numeroPedido, string numeroItemPedido)
    {
        if (string.IsNullOrWhiteSpace(corpoJson))
        {
            return PaginaHistoricoMaterial.Invalida("corpo vazio");
        }

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(corpoJson);
        }
        catch (JsonException)
        {
            return PaginaHistoricoMaterial.Invalida("JSON malformado");
        }

        using (documento)
        {
            if (!documento.RootElement.TryGetProperty("d", out JsonElement d)
                || d.ValueKind != JsonValueKind.Object
                || !d.TryGetProperty("results", out JsonElement results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return PaginaHistoricoMaterial.Invalida("envelope OData V2 sem d.results em array");
            }

            List<MovimentoHistoricoMaterial> movimentos = [];
            foreach (JsonElement item in results.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    movimentos.Add(MovimentoHistoricoMaterial.Incerto("entrada de results nao e objeto"));
                    continue;
                }

                // Item do pedido: fora do item consultado ⇒ simplesmente nao pertence a esta avaliacao.
                string? itemPedido = LerTexto(item, Campos.PurchaseOrderItem);
                if (itemPedido is null)
                {
                    movimentos.Add(MovimentoHistoricoMaterial.Incerto(
                        $"documento sem {Campos.PurchaseOrderItem}"));
                    continue;
                }

                if (!MesmoItemPedido(itemPedido, numeroItemPedido))
                {
                    continue;
                }

                string? tipo = LerTexto(item, Campos.GoodsMovementType);
                string? documentoSap = LerTexto(item, Campos.MaterialDocument);
                string? material = LerTexto(item, Campos.Material);
                string? centro = LerTexto(item, Campos.Plant);

                if (tipo is null || documentoSap is null)
                {
                    movimentos.Add(MovimentoHistoricoMaterial.Incerto(
                        "documento sem tipo de movimento ou sem identidade documental"));
                    continue;
                }

                string? estornaDocumento = LerTexto(item, Campos.ReversedMaterialDocument);
                movimentos.Add(new MovimentoHistoricoMaterial(
                    TipoMovimento: tipo,
                    MaterialDocument: documentoSap,
                    MaterialDocumentItem: LerTexto(item, Campos.MaterialDocumentItem),
                    MaterialDocumentYear: LerTexto(item, Campos.MaterialDocumentYear),
                    Material: material,
                    Centro: centro,
                    EstornaMaterialDocument: estornaDocumento,
                    EhIncerto: false,
                    MotivoIncerteza: string.Empty));
            }

            string? proxima = d.TryGetProperty("__next", out JsonElement next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;

            return PaginaHistoricoMaterial.Lida(movimentos, proxima);
        }
    }

    private static string? LerTexto(JsonElement objeto, string propriedade)
    {
        if (!objeto.TryGetProperty(propriedade, out JsonElement valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString()?.Trim(),
            JsonValueKind.Number => valor.ToString(),
            _ => null
        };
    }
}

/// <summary>GATE R6: UM movimento do historico, ja restrito ao item consultado.</summary>
public sealed record MovimentoHistoricoMaterial(
    string TipoMovimento,
    string MaterialDocument,
    string? MaterialDocumentItem,
    string? MaterialDocumentYear,
    string? Material,
    string? Centro,
    string? EstornaMaterialDocument,
    bool EhIncerto,
    string MotivoIncerteza)
{
    public static MovimentoHistoricoMaterial Incerto(string motivo)
        => new(string.Empty, string.Empty, null, null, null, null, null, true, motivo);

    public bool EhEntrada101 => string.Equals(
        TipoMovimento, HistoricoMaterialDocumentSapApiClient.MovimentoEntrada101, StringComparison.Ordinal);

    public bool EhEstorno102 => string.Equals(
        TipoMovimento, HistoricoMaterialDocumentSapApiClient.MovimentoEstorno102, StringComparison.Ordinal);

    /// <summary>Estorno COM vinculo documental explicito. Sem vinculo, nao se presume correspondencia.</summary>
    public bool EstornoComVinculoDocumental =>
        EhEstorno102 && !string.IsNullOrWhiteSpace(EstornaMaterialDocument);
}

/// <summary>GATE R6: resultado da interpretacao de UMA pagina.</summary>
public sealed record PaginaHistoricoMaterial(
    bool Valida,
    IReadOnlyList<MovimentoHistoricoMaterial> Movimentos,
    string? ProximaPagina,
    string Motivo)
{
    public static PaginaHistoricoMaterial Lida(IReadOnlyList<MovimentoHistoricoMaterial> movimentos, string? proxima)
        => new(true, movimentos, proxima, string.Empty);

    public static PaginaHistoricoMaterial Invalida(string motivo)
        => new(false, [], null, motivo);
}
