using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class ProdutoAcabadoMaterialDocument101PayloadBuilder
{
    public const string GoodsMovementCodeProdutoAcabado = "02";
    public const string GoodsMovementTypeProdutoAcabado = "101";
    public const string EndpointMaterialDocumentHeader = "API_MATERIAL_DOCUMENT_SRV/A_MaterialDocumentHeader";

    public ResultadoPreviewProdutoAcabado101 MontarPreview101(
        ProdutoAcabadoOrdem ordem,
        ProdutoAcabadoCaixa caixa,
        DateTime dataLancamentoUtc)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(caixa);

        if (string.IsNullOrWhiteSpace(ordem.NumeroOrdem))
        {
            return ResultadoPreviewProdutoAcabado101.Falha("OP do produto acabado não informada.");
        }

        if (string.IsNullOrWhiteSpace(ordem.MaterialProduzido))
        {
            return ResultadoPreviewProdutoAcabado101.Falha("Material produzido não informado.");
        }

        if (string.IsNullOrWhiteSpace(ordem.Centro))
        {
            return ResultadoPreviewProdutoAcabado101.Falha("Centro do produto acabado não informado.");
        }

        if (string.IsNullOrWhiteSpace(ordem.DepositoDestino))
        {
            return ResultadoPreviewProdutoAcabado101.Falha("Depósito destino do produto acabado não informado.");
        }

        if (caixa.PesoLiquidoKg <= 0m)
        {
            return ResultadoPreviewProdutoAcabado101.Falha("Peso líquido da caixa deve ser maior que zero.");
        }

        string unidade = string.IsNullOrWhiteSpace(ordem.Unidade) ? "KG" : ordem.Unidade.Trim().ToUpperInvariant();
        if (unidade is not ("KG" or "KGM") && caixa.QuantidadeProdutos <= 0)
        {
            return ResultadoPreviewProdutoAcabado101.Falha("Quantidade de produtos da caixa não informada pela norma de embalagem.");
        }

        decimal quantidadeMovimento = ObterQuantidadeMovimento(ordem, caixa);
        ProdutoAcabadoMaterialDocument101Request payload = new()
        {
            GoodsMovementCode = GoodsMovementCodeProdutoAcabado,
            PostingDate = FormatarDataODataV2(dataLancamentoUtc),
            DocumentDate = FormatarDataODataV2(dataLancamentoUtc),
            MaterialDocumentHeaderText = LimitarTexto("FugaPET produto acabado", 25),
            ToMaterialDocumentItem = new ProdutoAcabadoMaterialDocument101ItemResults
            {
                Results =
                [
                    new ProdutoAcabadoMaterialDocument101ItemRequest
                    {
                        Material = ordem.MaterialProduzido.Trim(),
                        Plant = ordem.Centro.Trim(),
                        StorageLocation = ordem.DepositoDestino.Trim(),
                        GoodsMovementType = GoodsMovementTypeProdutoAcabado,
                        QuantityInEntryUnit = quantidadeMovimento.ToString("0.###", CultureInfo.InvariantCulture),
                        EntryUnit = unidade,
                        ManufacturingOrder = ordem.NumeroOrdem.Trim(),
                        ManufacturingOrderItem = string.IsNullOrWhiteSpace(ordem.ItemOrdem) ? null : ordem.ItemOrdem.Trim(),
                        Batch = ordem.Lote.Trim(),
                        MaterialDocumentItemText = LimitarTexto($"FugaPET produto acabado caixa {caixa.NumeroCaixa}", 50)
                    }
                ]
            }
        };

        return ResultadoPreviewProdutoAcabado101.Ok(payload, SerializarPreview(payload));
    }

    internal static string SerializarPreview(ProdutoAcabadoMaterialDocument101Request payload)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["GoodsMovementCode"] = payload.GoodsMovementCode,
            ["PostingDate"] = payload.PostingDate,
            ["DocumentDate"] = payload.DocumentDate,
            ["MaterialDocumentHeaderText"] = payload.MaterialDocumentHeaderText,
            ["to_MaterialDocumentItem"] = new Dictionary<string, object?>
            {
                ["results"] = payload.ToMaterialDocumentItem.Results
            }
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static string FormatarDataODataV2(DateTime dataUtc)
    {
        DateTime utc = dataUtc.Kind == DateTimeKind.Utc ? dataUtc : dataUtc.ToUniversalTime();
        long millis = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
        return $"/Date({millis})/";
    }

    private static string LimitarTexto(string texto, int limite)
        => texto.Length <= limite ? texto : texto[..limite];

    private static decimal ObterQuantidadeMovimento(ProdutoAcabadoOrdem ordem, ProdutoAcabadoCaixa caixa)
    {
        string unidade = (ordem.Unidade ?? string.Empty).Trim().ToUpperInvariant();
        if (unidade is "KG" or "KGM")
        {
            return caixa.PesoLiquidoKg;
        }

        return caixa.QuantidadeProdutos;
    }
}
