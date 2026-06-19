namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Dados de produto/ordem de producao como vistos pelo SAP (resultado da consulta por OP).
/// </summary>
public sealed record ProdutoSap
{
    public string OrdemProducao { get; init; } = string.Empty;
    public string CodigoProduto { get; init; } = string.Empty;
    public string DescricaoProduto { get; init; } = string.Empty;
    public string Unidade { get; init; } = "KG";
    public string? Lote { get; init; }
    public decimal? QuantidadePlanejada { get; init; }
    public bool Ativo { get; init; } = true;
}
