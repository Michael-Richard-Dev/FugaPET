using System.Globalization;
using System.Text.Json;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Builder PURO (testavel, sem SAP/HTTP) do PREVIEW do movimento de consumo 261 para
/// API_MATERIAL_DOCUMENT_SRV. Tarefa 6: somente MONTAGEM â€” NAO faz POST, NAO busca CSRF,
/// NAO cria documento. Gera o objeto de request e o JSON OData V2 (to_MaterialDocumentItem.results,
/// datas em /Date(ms)/). Sem credenciais/URL/segredo no payload.
/// </summary>
public sealed class ConsumoMaterialSapPayloadBuilder
{
    public const string GoodsMovementCodeConsumo = "03";
    public const string GoodsMovementTypeConsumo = "261";
    public const string UnidadePesavel = "KG";
    private const int LimiteHeaderText = 25;

    public ResultadoPreviewConsumoSap261 MontarPreview261(
        ConsumoMaterialLancamento lancamento,
        DateTime dataLancamentoUtc)
    {
        if (lancamento is null)
        {
            return ResultadoPreviewConsumoSap261.Falha("Lançamento de consumo não informado.");
        }

        if (!string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal))
        {
            return ResultadoPreviewConsumoSap261.Falha(
                $"Preview disponível apenas para lançamento {ConsumoMaterialLancamento.StatusPendenteSap}.");
        }

        if (string.IsNullOrWhiteSpace(lancamento.NumeroOrdem))
        {
            return ResultadoPreviewConsumoSap261.Falha("Lançamento sem número da OP.");
        }

        if (lancamento.Itens is null || lancamento.Itens.Count == 0)
        {
            return ResultadoPreviewConsumoSap261.Falha("Lançamento sem itens de consumo.");
        }

        List<string> erros = [];
        List<ConsumoMaterialSap261ItemRequest> itens = [];

        foreach (ConsumoMaterialItem item in lancamento.Itens)
        {
            // Ignora item sem quantidade consumida local.
            if (item.QuantidadeConsumidaLocal <= 0m)
            {
                continue;
            }

            if (!string.Equals(item.Unidade, UnidadePesavel, StringComparison.OrdinalIgnoreCase))
            {
                erros.Add($"Componente {item.CodigoMaterial}: unidade {item.Unidade} não suportada no consumo 261 (apenas KG).");
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.CodigoMaterial)
                || string.IsNullOrWhiteSpace(item.Centro)
                || string.IsNullOrWhiteSpace(item.DepositoConsumo))
            {
                erros.Add($"Componente {item.CodigoMaterial}: material/centro/depósito incompletos.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Lote))
            {
                erros.Add(
                    $"Informe o lote do componente antes de enviar o consumo ao SAP. Material: {item.CodigoMaterial}, reserva {item.NumeroReserva}/{item.ItemReserva}.");
                continue;
            }

            itens.Add(new ConsumoMaterialSap261ItemRequest
            {
                Material = item.CodigoMaterial.Trim(),
                Plant = item.Centro!.Trim(),
                StorageLocation = item.DepositoConsumo!.Trim(),
                GoodsMovementType = GoodsMovementTypeConsumo,
                QuantityInEntryUnit = item.QuantidadeConsumidaLocal.ToString("0.000", CultureInfo.InvariantCulture),
                EntryUnit = UnidadePesavel,
                ManufacturingOrder = lancamento.NumeroOrdem.Trim(),
                Reservation = (item.NumeroReserva ?? string.Empty).Trim(),
                ReservationItem = (item.ItemReserva ?? string.Empty).Trim(),
                Batch = item.Lote.Trim()
            });
        }

        if (erros.Count > 0)
        {
            // Validacao falhou: NAO gera payload parcial como sucesso.
            return ResultadoPreviewConsumoSap261.Falha("Há itens inválidos para o consumo 261.", erros);
        }

        if (itens.Count == 0)
        {
            return ResultadoPreviewConsumoSap261.Falha("Nenhum item com quantidade consumida para gerar o preview.");
        }

        ConsumoMaterialSap261Request payload = new()
        {
            GoodsMovementCode = GoodsMovementCodeConsumo,
            PostingDate = dataLancamentoUtc,
            DocumentDate = dataLancamentoUtc,
            MaterialDocumentHeaderText = MontarTextoCabecalho(lancamento.NumeroOrdem),
            ToMaterialDocumentItem = itens
        };

        string json = SerializarPreview(payload);
        return ResultadoPreviewConsumoSap261.Ok(payload, json, $"Preview do consumo 261 gerado para a OP {lancamento.NumeroOrdem}.");
    }

    private static string MontarTextoCabecalho(string numeroOrdem)
    {
        string texto = $"FP CONS {numeroOrdem.Trim()}";
        return texto.Length <= LimiteHeaderText ? texto : texto[..LimiteHeaderText];
    }

    /// <summary>Serializa o preview OData V2: datas em /Date(ms)/, itens em to_MaterialDocumentItem.results.</summary>
    internal static string SerializarPreview(ConsumoMaterialSap261Request requisicao)
    {
        List<Dictionary<string, object?>> itens = [];
        foreach (ConsumoMaterialSap261ItemRequest item in requisicao.ToMaterialDocumentItem)
        {
            itens.Add(new Dictionary<string, object?>
            {
                ["Material"] = item.Material,
                ["Plant"] = item.Plant,
                ["StorageLocation"] = item.StorageLocation,
                ["GoodsMovementType"] = item.GoodsMovementType,
                ["QuantityInEntryUnit"] = item.QuantityInEntryUnit,
                ["EntryUnit"] = item.EntryUnit,
                ["ManufacturingOrder"] = item.ManufacturingOrder,
                ["Reservation"] = item.Reservation,
                ["ReservationItem"] = item.ReservationItem,
                ["Batch"] = item.Batch
            });
        }

        Dictionary<string, object?> payload = new()
        {
            ["GoodsMovementCode"] = requisicao.GoodsMovementCode,
            ["PostingDate"] = FormatarDataODataV2(requisicao.PostingDate),
            ["DocumentDate"] = FormatarDataODataV2(requisicao.DocumentDate),
            ["MaterialDocumentHeaderText"] = requisicao.MaterialDocumentHeaderText,
            ["to_MaterialDocumentItem"] = new Dictionary<string, object?> { ["results"] = itens }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>OData V2 /Date(ms)/ a partir da parte de data, rotulada como UTC (qualquer DateTimeKind).</summary>
    internal static string FormatarDataODataV2(DateTime data)
    {
        DateTime diaUtc = DateTime.SpecifyKind(data.Date, DateTimeKind.Utc);
        long ms = new DateTimeOffset(diaUtc).ToUnixTimeMilliseconds();
        return $"/Date({ms.ToString(CultureInfo.InvariantCulture)})/";
    }
}
