namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Resultado de um envio de apontamento ao SAP. Carrega apenas mensagem amigavel
/// (nunca detalhe tecnico) e, em sucesso, o protocolo retornado pelo SAP.
/// </summary>
public sealed record ResultadoEnvioSap
{
    public bool Sucesso { get; init; }
    public SituacaoIntegracaoSap Situacao { get; init; }

    /// <summary>Protocolo/numero de documento retornado pelo SAP (quando sucesso).</summary>
    public string? Protocolo { get; init; }

    public string Mensagem { get; init; } = string.Empty;

    public static ResultadoEnvioSap Ok(string protocolo, string? mensagem = null) => new()
    {
        Sucesso = true,
        Situacao = SituacaoIntegracaoSap.Enviado,
        Protocolo = protocolo,
        Mensagem = mensagem ?? "Apontamento enviado ao SAP com sucesso."
    };

    public static ResultadoEnvioSap Falha(string mensagem) => new()
    {
        Sucesso = false,
        Situacao = SituacaoIntegracaoSap.Erro,
        Mensagem = mensagem
    };
}
