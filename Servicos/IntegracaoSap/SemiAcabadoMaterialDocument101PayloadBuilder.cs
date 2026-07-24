using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class SemiAcabadoMaterialDocument101PayloadBuilder
{
    public const string GoodsMovementCodeSemiAcabado = "02";
    public const string GoodsMovementTypeSemiAcabado = "101";
    public const string GoodsMovementRefDocTypeOrdemProducao = "F";
    public const string EndpointMaterialDocumentHeader = "API_MATERIAL_DOCUMENT_SRV/A_MaterialDocumentHeader";

    public ResultadoPreviewSemiAcabado101 MontarPreview101(
        LancamentoSemiAcabado lancamento,
        DateTime dataLancamentoUtc)
    {
        ArgumentNullException.ThrowIfNull(lancamento);

        SemiAcabadoOrdem ordem = lancamento.Ordem;
        if (string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
        {
            return ResultadoPreviewSemiAcabado101.Falha("OP do semi-acabado não informada.");
        }

        if (string.IsNullOrWhiteSpace(ordem.MaterialProduzido))
        {
            return ResultadoPreviewSemiAcabado101.Falha("Material produzido não informado.");
        }

        if (string.IsNullOrWhiteSpace(ordem.Centro))
        {
            return ResultadoPreviewSemiAcabado101.Falha("Centro do semi-acabado não informado.");
        }

        if (string.IsNullOrWhiteSpace(ordem.DepositoDestino))
        {
            return ResultadoPreviewSemiAcabado101.Falha("Depósito destino do semi-acabado não informado.");
        }

        decimal quantidade = lancamento.PesoLiquidoTotalKg;
        if (quantidade <= 0m)
        {
            return ResultadoPreviewSemiAcabado101.Falha("Peso líquido total do semi-acabado deve ser maior que zero.");
        }

        if (ordem.QuantidadePendente > 0m && quantidade > ordem.QuantidadePendente)
        {
            return ResultadoPreviewSemiAcabado101.Falha(
                $"Peso líquido informado ({quantidade:0.###} KG) ultrapassa o saldo previsto do semi-acabado ({ordem.QuantidadePendente:0.###} KG).");
        }

        string unidade = string.IsNullOrWhiteSpace(ordem.Unidade) ? "KG" : ordem.Unidade.Trim().ToUpperInvariant();
        if (!string.Equals(unidade, "KG", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoPreviewSemiAcabado101.Falha("Unidade do semi-acabado incompatível com o fluxo atual. Esperado KG.");
        }

        SemiAcabadoMaterialDocument101Request payload = new()
        {
            GoodsMovementCode = GoodsMovementCodeSemiAcabado,
            PostingDate = FormatarDataODataV2(dataLancamentoUtc),
            DocumentDate = FormatarDataODataV2(dataLancamentoUtc),
            MaterialDocumentHeaderText = LimitarTexto("FugaPET semi-acabado", 25),
            ToMaterialDocumentItem = new SemiAcabadoMaterialDocument101ItemResults
            {
                Results =
                [
                    new SemiAcabadoMaterialDocument101ItemRequest
                    {
                        Material = ordem.MaterialProduzido.Trim(),
                        Plant = ordem.Centro.Trim(),
                        StorageLocation = ordem.DepositoDestino.Trim(),
                        GoodsMovementType = GoodsMovementTypeSemiAcabado,
                        GoodsMovementRefDocType = GoodsMovementRefDocTypeOrdemProducao,
                        QuantityInEntryUnit = quantidade.ToString("0.###", CultureInfo.InvariantCulture),
                        EntryUnit = unidade,
                        ManufacturingOrder = ordem.NumeroOrdem.Trim(),
                        ManufacturingOrderItem = string.IsNullOrWhiteSpace(ordem.ItemOrdem) ? null : ordem.ItemOrdem.Trim(),
                        Batch = ordem.Lote.Trim(),
                        MaterialDocumentItemText = LimitarTexto("FugaPET semi-acabado", 50)
                    }
                ]
            }
        };

        return ResultadoPreviewSemiAcabado101.Ok(payload, SerializarPreview(payload));
    }

    public ResultadoMaterialDocumentSemiAcabadoRequest MontarRequisicao101(
        LancamentoSemiAcabado lancamento,
        DateTime dataLancamentoUtc)
    {
        ResultadoPreviewSemiAcabado101 preview = MontarPreview101(lancamento, dataLancamentoUtc);
        if (!preview.Sucesso || preview.Payload is null)
        {
            return ResultadoMaterialDocumentSemiAcabadoRequest.Falha(preview.Mensagem);
        }

        SemiAcabadoMaterialDocument101ItemRequest item = preview.Payload.ToMaterialDocumentItem.Results[0];
        MaterialDocumentSapRequest requisicao = new()
        {
            GoodsMovementCode = GoodsMovementCodeSemiAcabado,
            PostingDate = dataLancamentoUtc,
            DocumentDate = dataLancamentoUtc,
            MaterialDocumentHeaderText = preview.Payload.MaterialDocumentHeaderText,
            Itens =
            [
                new MaterialDocumentSapItemRequest
                {
                    Material = item.Material,
                    Plant = item.Plant,
                    StorageLocation = item.StorageLocation,
                    GoodsMovementType = item.GoodsMovementType,
                    GoodsMovementRefDocType = item.GoodsMovementRefDocType,
                    QuantityInEntryUnit = item.QuantityInEntryUnit,
                    EntryUnit = item.EntryUnit,
                    ManufacturingOrder = item.ManufacturingOrder,
                    ManufacturingOrderItem = item.ManufacturingOrderItem,
                    Batch = item.Batch,
                    MaterialDocumentItemText = item.MaterialDocumentItemText
                }
            ]
        };

        return ResultadoMaterialDocumentSemiAcabadoRequest.Ok(requisicao);
    }

    public static ResultadoEnvioSemiAcabado101 ValidarRespostaConfirmada(
        string? materialDocument,
        string? materialDocumentYear)
    {
        if (string.IsNullOrWhiteSpace(materialDocument))
        {
            return ResultadoEnvioSemiAcabado101.Falha("SAP não retornou MaterialDocument. Semi-acabado não será confirmado.");
        }

        if (string.IsNullOrWhiteSpace(materialDocumentYear))
        {
            return ResultadoEnvioSemiAcabado101.Falha("SAP não retornou MaterialDocumentYear. Semi-acabado não será confirmado.");
        }

        return ResultadoEnvioSemiAcabado101.Confirmado(materialDocument.Trim(), materialDocumentYear.Trim());
    }

    internal static string SerializarPreview(SemiAcabadoMaterialDocument101Request payload)
    {
        Dictionary<string, object?> item = new()
        {
            ["Material"] = payload.ToMaterialDocumentItem.Results[0].Material,
            ["Plant"] = payload.ToMaterialDocumentItem.Results[0].Plant,
            ["StorageLocation"] = payload.ToMaterialDocumentItem.Results[0].StorageLocation,
            ["GoodsMovementType"] = payload.ToMaterialDocumentItem.Results[0].GoodsMovementType,
            ["GoodsMovementRefDocType"] = payload.ToMaterialDocumentItem.Results[0].GoodsMovementRefDocType,
            ["QuantityInEntryUnit"] = payload.ToMaterialDocumentItem.Results[0].QuantityInEntryUnit,
            ["EntryUnit"] = payload.ToMaterialDocumentItem.Results[0].EntryUnit,
            ["ManufacturingOrder"] = payload.ToMaterialDocumentItem.Results[0].ManufacturingOrder,
            ["ManufacturingOrderItem"] = payload.ToMaterialDocumentItem.Results[0].ManufacturingOrderItem,
            ["Batch"] = payload.ToMaterialDocumentItem.Results[0].Batch,
            ["MaterialDocumentItemText"] = payload.ToMaterialDocumentItem.Results[0].MaterialDocumentItemText
        };

        Dictionary<string, object?> root = new()
        {
            ["GoodsMovementCode"] = payload.GoodsMovementCode,
            ["PostingDate"] = payload.PostingDate,
            ["DocumentDate"] = payload.DocumentDate,
            ["MaterialDocumentHeaderText"] = payload.MaterialDocumentHeaderText,
            ["to_MaterialDocumentItem"] = new Dictionary<string, object?>
            {
                ["results"] = new[] { item }
            }
        };

        return JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string FormatarDataODataV2(DateTime dataUtc)
    {
        DateTime utc = dataUtc.Kind == DateTimeKind.Utc ? dataUtc : dataUtc.ToUniversalTime();
        long millis = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
        return $"/Date({millis})/";
    }

    private static string LimitarTexto(string texto, int limite)
        => texto.Length <= limite ? texto : texto[..limite];
}

public sealed class ResultadoMaterialDocumentSemiAcabadoRequest
{
    private ResultadoMaterialDocumentSemiAcabadoRequest(bool sucesso, string mensagem, MaterialDocumentSapRequest? requisicao)
    {
        Sucesso = sucesso;
        Mensagem = mensagem;
        Requisicao = requisicao;
    }

    public bool Sucesso { get; }
    public string Mensagem { get; }
    public MaterialDocumentSapRequest? Requisicao { get; }

    public static ResultadoMaterialDocumentSemiAcabadoRequest Ok(MaterialDocumentSapRequest requisicao)
        => new(true, string.Empty, requisicao);

    public static ResultadoMaterialDocumentSemiAcabadoRequest Falha(string mensagem)
        => new(false, mensagem, null);
}
