namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// PREVIEW TECNICO (NAO enviado ao SAP) do caminho de Confirmacao de Producao para componentes
/// Backflush. Estrutura CONCEITUAL — o payload final depende de validacao SAP/Postman e da
/// API_PROD_ORDER_CONFIRMATION_2_SRV (nao implementada nesta etapa). Sem POST/CSRF/PATCH.
/// </summary>
public sealed class ConfirmacaoProducaoPreviewRequest
{
    public string Aviso { get; init; } = "Preview técnico — não enviado ao SAP";
    public string OrdemProducao { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string SequenciaOperacao { get; init; } = string.Empty;

    /// <summary>"COMPONENTE" ou "FALLBACK_PRIMEIRA_OPERACAO_OP" — de onde a operacao foi obtida.</summary>
    public string OrigemOperacao { get; init; } = string.Empty;

    /// <summary>Observacao tecnica (preenchida quando a operacao usa o fallback da primeira operacao da OP).</summary>
    public string ObservacaoOperacao { get; init; } = string.Empty;

    public string Material { get; init; } = string.Empty;
    public decimal QuantidadeConsumida { get; init; }
    public string Unidade { get; init; } = string.Empty;
    public string Reserva { get; init; } = string.Empty;
    public string ItemReserva { get; init; } = string.Empty;
    public string Lote { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;
    public string Deposito { get; init; } = string.Empty;
}

/// <summary>Resultado da montagem do preview tecnico de Confirmacao de Producao. NAO envia SAP.</summary>
public sealed class ResultadoPreviewConfirmacaoProducao
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string Titulo { get; init; } = "Preview técnico — não enviado ao SAP";
    public ConfirmacaoProducaoPreviewRequest? Preview { get; init; }
    public string PreviewJson { get; init; } = string.Empty;

    public static ResultadoPreviewConfirmacaoProducao Falha(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };

    public static ResultadoPreviewConfirmacaoProducao Ok(
        ConfirmacaoProducaoPreviewRequest preview, string previewJson, string mensagem)
        => new()
        {
            Sucesso = true,
            Mensagem = mensagem,
            Preview = preview,
            PreviewJson = previewJson
        };
}
