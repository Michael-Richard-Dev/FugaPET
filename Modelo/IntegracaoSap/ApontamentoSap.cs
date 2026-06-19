namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Apontamento de pesagem a ser enviado ao SAP. Contrato de ENTRADA do envio.
/// </summary>
public sealed record ApontamentoSap
{
    public string OrdemProducao { get; init; } = string.Empty;
    public string CodigoProduto { get; init; } = string.Empty;
    public decimal Quantidade { get; init; }
    public string Unidade { get; init; } = "KG";
    public string? Lote { get; init; }
    public string? CodigoBarras { get; init; }

    /// <summary>Origem da leitura (ex.: nome da balanca, "Manual", "Automatico").</summary>
    public string Origem { get; init; } = string.Empty;

    /// <summary>Login/identificacao do operador responsavel.</summary>
    public string Usuario { get; init; } = string.Empty;

    public DateTimeOffset MomentoLeitura { get; init; } = DateTimeOffset.Now;
}
