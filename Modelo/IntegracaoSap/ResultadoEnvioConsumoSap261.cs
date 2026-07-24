namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Resultado do envio CONTROLADO do consumo 261 ao SAP (API_MATERIAL_DOCUMENT_SRV). So e sucesso
/// quando o SAP retorna MaterialDocument E MaterialDocumentYear. Nunca expoe segredo/payload.
/// </summary>
public sealed class ResultadoEnvioConsumoSap261
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;
    public string? DocumentoMaterialSap { get; init; }
    public string? ExercicioDocumentoMaterialSap { get; init; }
    public int? StatusHttp { get; init; }
    public string? CorrelationId { get; init; }
    public string? MetodoHttp { get; init; }
    public string? Endpoint { get; init; }
    public string? ResponseBody { get; init; }
    public string? CodigoErroSap { get; init; }
    public string? MensagemSap { get; init; }
    public string? DetalhesErroSap { get; init; }
    public string? PayloadJson { get; init; }

    /// <summary>
    /// Classifica a falha como INDETERMINADA: o documento PODE ter sido criado no SAP, então o
    /// resultado não é uma rejeição comprovada. Consumidores devem BLOQUEAR (não liberar término,
    /// não reenviar automaticamente) em vez de tratar como erro seguro.
    ///
    /// Indeterminado quando:
    ///  - não há status HTTP (queda de conexão/timeout — possivelmente durante o POST);
    ///  - HTTP 2xx sem MaterialDocument/Year (pode ter criado sem rastreabilidade);
    ///  - HTTP 408 (timeout) ou 5xx (falha do servidor durante o POST).
    ///
    /// Conservador por segurança: uma falha ANTES do POST (validação/CSRF) também chega sem status e
    /// será classificada como indeterminada. O efeito é apenas bloquear o término — nunca liberá-lo
    /// indevidamente. Quando o cliente 261 expuser a ETAPA, esta regra pode ser refinada.
    /// </summary>
    public bool ResultadoIndeterminado
    {
        get
        {
            if (Sucesso)
            {
                return false;
            }

            if (StatusHttp is not int status)
            {
                return true;
            }

            return status is (>= 200 and <= 299) or 408 or (>= 500 and <= 599);
        }
    }

    /// <summary>Rejeição COMPROVADA pelo SAP (4xx de negócio, exceto 408): falha segura e reenviável.</summary>
    public bool RejeicaoComprovada => !Sucesso && !ResultadoIndeterminado;

    public static ResultadoEnvioConsumoSap261 Ok(string documento, string exercicio, int? statusHttp, string? correlationId)
        => new()
        {
            Sucesso = true,
            DocumentoMaterialSap = documento,
            ExercicioDocumentoMaterialSap = exercicio,
            StatusHttp = statusHttp,
            CorrelationId = correlationId,
            Mensagem = $"Consumo enviado ao SAP. Documento {documento}/{exercicio}."
        };

    public static ResultadoEnvioConsumoSap261 Falha(
        string mensagem,
        int? statusHttp = null,
        string? correlationId = null,
        string? metodoHttp = null,
        string? endpoint = null,
        string? responseBody = null,
        string? codigoErroSap = null,
        string? mensagemSap = null,
        string? detalhesErroSap = null,
        string? payloadJson = null)
        => new()
        {
            Sucesso = false,
            Mensagem = mensagem,
            StatusHttp = statusHttp,
            CorrelationId = correlationId,
            MetodoHttp = metodoHttp,
            Endpoint = endpoint,
            ResponseBody = responseBody,
            CodigoErroSap = codigoErroSap,
            MensagemSap = mensagemSap,
            DetalhesErroSap = detalhesErroSap,
            PayloadJson = payloadJson
        };
}
