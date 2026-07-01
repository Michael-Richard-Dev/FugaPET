using System.Text.Encodings.Web;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Montagem PURA do preview tecnico de Confirmacao de Producao (Backflush). NAO envia SAP, NAO faz
/// POST, NAO busca CSRF, NAO usa PATCH e NAO cria client. Apenas serializa um objeto conceitual a
/// partir dos dados ja disponiveis (OP/operacao/componente/quantidade local). O payload final depende
/// de validacao SAP/Postman e da API_PROD_ORDER_CONFIRMATION_2_SRV (fora desta etapa).
/// </summary>
internal static class ConfirmacaoProducaoPreviewBuilder
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static ResultadoPreviewConfirmacaoProducao Montar(
        OrdemProducaoConsumo ordem,
        ComponenteConsumoMaterial componente,
        decimal quantidadeConsumidaLocal)
    {
        ArgumentNullException.ThrowIfNull(ordem);
        ArgumentNullException.ThrowIfNull(componente);

        if (!componente.BackflushSap)
        {
            return ResultadoPreviewConfirmacaoProducao.Falha(
                "Preview de Confirmação de Produção disponível apenas para componente Backflush.");
        }

        // Operacao: PREFERE a do componente (ManufacturingOrderOperation). So usa a 1a operacao da OP
        // como FALLBACK controlado quando o componente nao trouxe operacao — e sinaliza a origem.
        bool temOperacaoComponente = !string.IsNullOrWhiteSpace(componente.Operacao);
        string operacao = temOperacaoComponente
            ? componente.Operacao
            : ordem.Operacoes.Count > 0 ? ordem.Operacoes[0].Operacao : string.Empty;
        string origemOperacao = temOperacaoComponente ? "COMPONENTE" : "FALLBACK_PRIMEIRA_OPERACAO_OP";
        string observacaoOperacao = temOperacaoComponente
            ? string.Empty
            : "Operação do componente não retornada pelo SAP; usando primeira operação da OP apenas como fallback técnico.";

        ConfirmacaoProducaoPreviewRequest preview = new()
        {
            OrdemProducao = ordem.NumeroOrdem,
            Operacao = operacao,
            SequenciaOperacao = componente.SequenciaOperacao,
            OrigemOperacao = origemOperacao,
            ObservacaoOperacao = observacaoOperacao,
            Material = componente.CodigoMaterial,
            QuantidadeConsumida = quantidadeConsumidaLocal,
            Unidade = componente.UnidadeMedida,
            Reserva = componente.NumeroReserva,
            ItemReserva = componente.ItemReserva,
            Lote = componente.Lote,
            Centro = componente.Centro,
            Deposito = componente.DepositoConsumo
        };

        string json = JsonSerializer.Serialize(preview, OpcoesJson);
        return ResultadoPreviewConfirmacaoProducao.Ok(
            preview,
            json,
            "Preview técnico — não enviado ao SAP");
    }

    /// <summary>
    /// Tarefa 16: preview tecnico a partir do LANCAMENTO SALVO (PENDENTE_SAP), usando os dados realmente
    /// gravados (OP/centro/material/reserva/item/lote/deposito/quantidade/unidade/status). Sem POST/CSRF/PATCH.
    /// </summary>
    public static ResultadoPreviewConfirmacaoProducao MontarDoLancamento(
        FugaPET_Dev.Modelo.Consumo.ConsumoMaterialLancamento lancamento)
    {
        ArgumentNullException.ThrowIfNull(lancamento);
        if (lancamento.Itens.Count == 0)
        {
            return ResultadoPreviewConfirmacaoProducao.Falha("Lançamento salvo sem itens para preview de Confirmação.");
        }

        var itens = lancamento.Itens.Select(item => new ConfirmacaoProducaoSapRequest
        {
            OrdemProducao = lancamento.NumeroOrdem,
            MaterialProduzido = lancamento.MaterialProduzido ?? string.Empty,
            Centro = item.Centro ?? string.Empty,
            Reserva = item.NumeroReserva ?? string.Empty,
            ItemReserva = item.ItemReserva ?? string.Empty,
            Material = item.CodigoMaterial,
            Lote = item.Lote ?? string.Empty,
            Deposito = item.DepositoConsumo ?? string.Empty,
            QuantidadeConsumida = item.QuantidadeConsumidaLocal,
            Unidade = item.Unidade,
            StatusLocal = lancamento.StatusLancamento
        }).ToList();

        var preview = new
        {
            Aviso = "Preview técnico — não enviado ao SAP",
            Lancamento = lancamento.Codigo,
            OrdemProducao = lancamento.NumeroOrdem,
            StatusLocal = lancamento.StatusLancamento,
            Itens = itens
        };

        string json = JsonSerializer.Serialize(preview, OpcoesJson);
        return new ResultadoPreviewConfirmacaoProducao
        {
            Sucesso = true,
            Mensagem = "Preview técnico — não enviado ao SAP",
            Titulo = "Preview técnico — não enviado ao SAP",
            PreviewJson = json
        };
    }
}
