namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Filtros da consulta de integracao SAP. Todos opcionais (nulo/vazio = sem filtro).
/// </summary>
public sealed record FiltroConsultaIntegracaoSap
{
    /// <summary>Texto livre: ordem de producao, lote ou produto.</summary>
    public string? Termo { get; init; }

    public string? Linha { get; init; }
    public string? Turno { get; init; }
    public DateTime? DataInicial { get; init; }
    public DateTime? DataFinal { get; init; }

    /// <summary>Limite de linhas retornadas (protege a UI). Padrao 200.</summary>
    public int Limite { get; init; } = 200;
}
