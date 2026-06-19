namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Uma linha do historico de integracao SAP (envio/retorno/erro).
/// Substitui, de forma tipada, o mock inline que hoje vive na tela de consulta.
/// </summary>
public sealed record RegistroIntegracaoSap
{
    public DateTimeOffset DataHora { get; init; }
    public TipoMovimentoSap Tipo { get; init; }
    public string OrdemProducao { get; init; } = string.Empty;
    public string Produto { get; init; } = string.Empty;
    public string? Lote { get; init; }
    public string? CodigoBarras { get; init; }
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = "KG";
    public string Usuario { get; init; } = string.Empty;
    public string Origem { get; init; } = string.Empty;
    public SituacaoIntegracaoSap Situacao { get; init; }

    /// <summary>Detalhe amigavel opcional (ex.: motivo do erro de integracao).</summary>
    public string? Mensagem { get; init; }
}
