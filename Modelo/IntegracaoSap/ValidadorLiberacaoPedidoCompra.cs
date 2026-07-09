namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Tarefa Entrada 23.1: resultado da validação de aprovação/liberação do Pedido de Compra no SAP.
/// </summary>
public sealed record ResultadoValidacaoPedidoCompra
{
    public bool Liberado { get; init; }
    public string NumeroPedido { get; init; } = string.Empty;
    public string CodigoStatus { get; init; } = string.Empty;
    public string DescricaoStatus { get; init; } = string.Empty;
    public bool LiberacaoNaoConcluida { get; init; }

    /// <summary>Motivo do bloqueio (vazio quando <see cref="Liberado"/>).</summary>
    public string MotivoBloqueio { get; init; } = string.Empty;
}

/// <summary>
/// Tarefa Entrada 23.1: validador CENTRAL e PURO da aprovação/liberação do Pedido de Compra
/// (PurchasingProcessingStatus + ReleaseIsNotCompleted). A regra técnica usa o CÓDIGO do status;
/// a descrição amigável (<see cref="DescreverPurchasingProcessingStatus"/>) é só para exibição/diagnóstico.
/// Por segurança, bloqueia sempre que não for possível validar (pedido nulo / status ausente / desconhecido).
/// </summary>
public static class ValidadorLiberacaoPedidoCompra
{
    public const string StatusLiberado = "05";
    public const string StatusEmAprovacao = "03";
    public const string StatusAguardandoLiberacao = "04";
    public const string StatusRejeitado = "08";

    public const string MotivoNaoValidado =
        "Não foi possível validar o status de aprovação/liberação do pedido no SAP.";
    public const string MotivoLiberacaoNaoConcluida =
        "Liberação do pedido ainda não concluída no SAP.";
    public const string MotivoEmAprovacao =
        "Pedido de compra em processo de aprovação/liberação.";
    public const string MotivoAguardandoLiberacao =
        "Pedido de compra ainda não liberado/aprovado.";
    public const string MotivoRejeitado =
        "Pedido de compra rejeitado no SAP.";
    public const string MotivoStatusNaoLiberado =
        "Status de processamento do pedido não liberado para entrada.";

    /// <summary>
    /// Valida a liberação/aprovação do pedido. Ordem: pedido nulo → ReleaseIsNotCompleted → status vazio →
    /// código do status. Um pedido só é liberado com PurchasingProcessingStatus == "05" E ReleaseIsNotCompleted != true.
    /// </summary>
    public static ResultadoValidacaoPedidoCompra Validar(PedidoCompraSap? pedido)
    {
        if (pedido is null)
        {
            return Bloquear(string.Empty, string.Empty, liberacaoNaoConcluida: false, MotivoNaoValidado);
        }

        string status = (pedido.StatusProcessamentoCompraSap ?? string.Empty).Trim();
        bool liberacaoNaoConcluida = pedido.LiberacaoNaoConcluidaSap == true;
        string numero = (pedido.Numero ?? string.Empty).Trim();

        ResultadoValidacaoPedidoCompra resultado = DecidirValidacao(numero, status, liberacaoNaoConcluida, pedido);
        RegistrarDiagnostico(pedido, resultado);
        return resultado;
    }

    private static ResultadoValidacaoPedidoCompra DecidirValidacao(
        string numero,
        string status,
        bool liberacaoNaoConcluida,
        PedidoCompraSap pedido)
    {
        // ReleaseIsNotCompleted == true bloqueia SEMPRE, mesmo com status "05".
        if (liberacaoNaoConcluida)
        {
            return Bloquear(numero, status, liberacaoNaoConcluida: true, MotivoLiberacaoNaoConcluida);
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return Bloquear(numero, status, liberacaoNaoConcluida: false, MotivoNaoValidado);
        }

        return status switch
        {
            StatusLiberado => new ResultadoValidacaoPedidoCompra
            {
                Liberado = true,
                NumeroPedido = numero,
                CodigoStatus = status,
                DescricaoStatus = DescreverPurchasingProcessingStatus(status),
                LiberacaoNaoConcluida = false,
                MotivoBloqueio = string.Empty
            },
            StatusEmAprovacao => Bloquear(numero, status, false, MotivoEmAprovacao),
            StatusAguardandoLiberacao => Bloquear(numero, status, false, MotivoAguardandoLiberacao),
            StatusRejeitado => Bloquear(numero, status, false, MotivoRejeitado),
            _ => Bloquear(numero, status, false, MotivoStatusNaoLiberado)
        };
    }

    private static ResultadoValidacaoPedidoCompra Bloquear(
        string numero,
        string status,
        bool liberacaoNaoConcluida,
        string motivo)
        => new()
        {
            Liberado = false,
            NumeroPedido = numero,
            CodigoStatus = status,
            DescricaoStatus = DescreverPurchasingProcessingStatus(status),
            LiberacaoNaoConcluida = liberacaoNaoConcluida,
            MotivoBloqueio = motivo
        };

    /// <summary>Ajuste 9: descrição amigável do PurchasingProcessingStatus (a regra usa o código, não o texto).</summary>
    public static string DescreverPurchasingProcessingStatus(string? status)
        => (status ?? string.Empty).Trim() switch
        {
            StatusLiberado => "Liberado/Aprovado",
            StatusEmAprovacao => "Em aprovação/liberação",
            StatusAguardandoLiberacao => "Aguardando liberação/aprovação",
            StatusRejeitado => "Rejeitado",
            "" => "Status não informado",
            _ => "Status SAP não mapeado"
        };

    // Ajuste 8: diagnóstico técnico da decisão (não é exibido ao operador).
    private static void RegistrarDiagnostico(PedidoCompraSap pedido, ResultadoValidacaoPedidoCompra resultado)
    {
        string release = pedido.LiberacaoNaoConcluidaSap is bool valor
            ? valor.ToString().ToLowerInvariant()
            : "indisponível";

        System.Diagnostics.Trace.TraceInformation(
            "[Entrada][ValidacaoPedidoCompra] "
            + $"Pedido: {resultado.NumeroPedido}; "
            + $"PurchasingProcessingStatus: {resultado.CodigoStatus}; "
            + $"ReleaseIsNotCompleted: {release}; "
            + $"PurchasingCompletenessStatus: {pedido.StatusCompletudeCompraSap}; "
            + $"Decisao: {(resultado.Liberado ? "PERMITIDO" : "BLOQUEADO")}; "
            + $"Motivo: {(resultado.Liberado ? "-" : resultado.MotivoBloqueio)}");
    }
}
