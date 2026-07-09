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
