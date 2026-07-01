using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

public sealed class ConfirmacaoProducaoSapPayloadBuilder
{
    public const string GoodsMovementTypeConsumo = "261";
    public const string UnidadePesavel = "KG";
    public const string MensagemOrderOperationInternalIdAusente =
        "Não foi possível determinar o OrderOperationInternalID da operação. Valide a operação no SAP/Postman antes de enviar.";

    public ResultadoPreviewConfirmacaoProducaoSap MontarPreview(
        ConsumoMaterialLancamento lancamento,
        OperacaoConfirmacaoSap operacaoResolvida,
        DateTime dataLancamentoUtc)
    {
        if (lancamento is null)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha("Lançamento de consumo não informado.");
        }

        // Tarefa 17.6: preview/payload disponivel para PENDENTE_SAP ou FALHA_SAP (diagnostico). O ENVIO real
        // continua bloqueando FALHA_SAP no servico (sem reprocessamento automatico).
        bool pendenteOuFalha =
            string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusPendenteSap, StringComparison.Ordinal)
            || string.Equals(lancamento.StatusLancamento, ConsumoMaterialLancamento.StatusFalhaSap, StringComparison.Ordinal);
        if (!pendenteOuFalha)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha(
                $"Confirmação disponível apenas para lançamento {ConsumoMaterialLancamento.StatusPendenteSap} ou {ConsumoMaterialLancamento.StatusFalhaSap}.");
        }

        if (operacaoResolvida is null)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha(MensagemOrderOperationInternalIdAusente);
        }

        if (string.IsNullOrWhiteSpace(operacaoResolvida.OrderOperation))
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha("Operação SAP de confirmação não informada. Envio não será executado.");
        }

        if (string.IsNullOrWhiteSpace(operacaoResolvida.Sequence))
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha("Sequência SAP da operação de confirmação não informada. Envio não será executado.");
        }

        if (lancamento.Itens.Count == 0)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha("Lançamento sem itens de consumo.");
        }

        List<string> erros = [];
        List<ConfirmacaoProducaoSapItemRequest> itens = [];
        foreach (ConsumoMaterialItem item in lancamento.Itens)
        {
            if (item.QuantidadeConsumidaLocal <= 0m)
            {
                continue;
            }

            string numeroOrdem = (lancamento.NumeroOrdem ?? string.Empty).Trim();
            string reserva = NormalizarReservaSap(item.NumeroReserva);
            string itemReserva = NormalizarItemReservaSap(item.ItemReserva);

            if (string.IsNullOrWhiteSpace(numeroOrdem))
            {
                erros.Add($"Componente {item.CodigoMaterial}: ordem de produção obrigatória para confirmação.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(reserva) || string.IsNullOrWhiteSpace(itemReserva))
            {
                erros.Add($"Componente {item.CodigoMaterial}: reserva/item obrigatórios para confirmação de produção com movimento 261.");
                continue;
            }

            if (!string.Equals(item.Unidade, UnidadePesavel, StringComparison.OrdinalIgnoreCase))
            {
                erros.Add($"Componente {item.CodigoMaterial}: unidade {item.Unidade} não suportada na confirmação (apenas KG).");
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
                erros.Add($"Componente {item.CodigoMaterial}: lote obrigatório para confirmação.");
                continue;
            }

            Debug.WriteLine(
                $"Confirmação SAP item: material={item.CodigoMaterial.Trim()}, op={numeroOrdem}, operacao={operacaoResolvida.OrderOperation.Trim()}, reserva={reserva}, itemReserva={itemReserva}, deposito={item.DepositoConsumo!.Trim()}, lote={item.Lote.Trim()}, quantidade={FormatarQuantidade(item.QuantidadeConsumidaLocal)}, unidade={UnidadePesavel}");

            itens.Add(new ConfirmacaoProducaoSapItemRequest
            {
                Material = item.CodigoMaterial.Trim(),
                ManufacturingOrder = numeroOrdem,
                Reservation = reserva,
                ReservationItem = itemReserva,
                GoodsMovementType = GoodsMovementTypeConsumo,
                QuantityInEntryUnit = FormatarQuantidade(item.QuantidadeConsumidaLocal),
                EntryUnit = UnidadePesavel,
                Plant = item.Centro!.Trim(),
                StorageLocation = item.DepositoConsumo!.Trim(),
                Batch = item.Lote.Trim(),
                // Tarefa 17.8: campos obrigatorios do movimento (exemplo real SAP): RefDocType "F" e ReasonCode "0".
                GoodsMovementRefDocType = "F",
                GoodsMovementReasonCode = "0"
            });
        }

        if (erros.Count > 0)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha(
                "Há itens inválidos para a Confirmação de Produção.", erros);
        }

        if (itens.Count == 0)
        {
            return ResultadoPreviewConfirmacaoProducaoSap.Falha("Nenhum item com quantidade consumida para confirmar.");
        }

        decimal quantidadeTotal = lancamento.Itens
            .Where(item => item.QuantidadeConsumidaLocal > 0m)
            .Sum(item => item.QuantidadeConsumidaLocal);
        string numeroOrdemPayload = (lancamento.NumeroOrdem ?? string.Empty).Trim();

        ConfirmacaoProducaoSapRequest payload = new()
        {
            OrderID = numeroOrdemPayload,
            ManufacturingOrder = numeroOrdemPayload,
            Sequence = operacaoResolvida.Sequence.Trim(),
            OrderOperation = operacaoResolvida.OrderOperation.Trim(),
            OrderOperationInternalID = operacaoResolvida.OrderOperationInternalId.Trim(),
            // Tarefa 17.11: ConfirmationYieldQuantity = quantidade APONTADA/produzida da operacao (NAO a
            // quantidade consumida do componente). O FugaPET nao aponta producao real -> yield ZERO.
            ConfirmationYieldQuantity = FormatarQuantidade(0m),
            ConfirmationUnit = UnidadePesavel,
            ConfirmationText = "FugaPET teste consumo",
            PostingDate = dataLancamentoUtc,
            IsFinalConfirmation = false,
            Plant = (lancamento.Centro ?? itens[0].Plant).Trim(),
            ToProdnOrdConfMatlDocItm = itens
        };

        return ResultadoPreviewConfirmacaoProducaoSap.Ok(
            payload,
            SerializarPreview(payload),
            $"Preview da Confirmação de Produção gerado para a OP {lancamento.NumeroOrdem}.");
    }

    internal static string SerializarPreview(ConfirmacaoProducaoSapRequest requisicao)
    {
        List<Dictionary<string, object?>> itens = [];
        foreach (ConfirmacaoProducaoSapItemRequest item in requisicao.ToProdnOrdConfMatlDocItm)
        {
            itens.Add(new Dictionary<string, object?>
            {
                ["Material"] = item.Material,
                // Tarefa 17.6: ManufacturingOrder NAO e aceito no item de to_ProdnOrdConfMatlDocItm
                // (SAP: "Property 'ManufacturingOrder' is invalid"). A OP fica no cabecalho como OrderID.
                ["Reservation"] = item.Reservation,
                ["ReservationItem"] = item.ReservationItem,
                ["GoodsMovementType"] = item.GoodsMovementType,
                // Tarefa 17.11: GoodsMovementRefDocType NAO e enviado no POST (mantido fora ate validacao $metadata).
                ["QuantityInEntryUnit"] = item.QuantityInEntryUnit,
                ["EntryUnit"] = item.EntryUnit,
                ["Plant"] = item.Plant,
                ["StorageLocation"] = item.StorageLocation,
                ["Batch"] = item.Batch,
                ["GoodsMovementReasonCode"] = item.GoodsMovementReasonCode,
                ["InventoryValuationType"] = item.InventoryValuationType
            });
        }

        Dictionary<string, object?> payload = new()
        {
            ["OrderID"] = string.IsNullOrWhiteSpace(requisicao.OrderID) ? requisicao.ManufacturingOrder : requisicao.OrderID,
            ["Sequence"] = requisicao.Sequence,
            ["OrderOperation"] = requisicao.OrderOperation,
            ["ConfirmationYieldQuantity"] = requisicao.ConfirmationYieldQuantity,
            ["ConfirmationUnit"] = requisicao.ConfirmationUnit,
            ["PostingDate"] = ConsumoMaterialSapPayloadBuilder.FormatarDataODataV2(requisicao.PostingDate),
            ["IsFinalConfirmation"] = requisicao.IsFinalConfirmation,
            ["ConfirmationText"] = requisicao.ConfirmationText,
            ["Plant"] = requisicao.Plant,
            // APIConfHasNoGoodsMovements apareceu no retorno da API, mas não será enviado até confirmação no metadata de que é gravável.
            ["to_ProdnOrdConfMatlDocItm"] = new Dictionary<string, object?> { ["results"] = itens }
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string FormatarQuantidade(decimal quantidade)
        => quantidade.ToString("0.000", CultureInfo.InvariantCulture);

    private static string NormalizarReservaSap(string? reserva)
    {
        string valor = ApenasDigitos(reserva);
        return string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.PadLeft(10, '0');
    }

    private static string NormalizarItemReservaSap(string? itemReserva)
    {
        string valor = ApenasDigitos(itemReserva);
        return string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.PadLeft(4, '0');
    }

    private static string ApenasDigitos(string? valor)
        => new((valor ?? string.Empty).Where(char.IsDigit).ToArray());
}
