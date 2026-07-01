namespace FugaPET_Dev.Modelo.Processo;

/// <summary>Operacao da Ordem de Producao exibida na Tela de Consumo (somente leitura).</summary>
public sealed class OperacaoOrdemConsumo
{
    public string Operacao { get; set; } = string.Empty;
    public string OrderOperationInternalId { get; set; } = string.Empty;
    public string Sequencia { get; set; } = string.Empty;
    public string CentroTrabalho { get; set; } = string.Empty;
    public string Planta { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal QuantidadePrevista { get; set; }
    public string Unidade { get; set; } = string.Empty;
}
