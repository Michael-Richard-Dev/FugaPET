namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>Cenarios da consulta (GET) da Ordem de Producao no SAP.</summary>
public enum CenarioConsultaOrdemProducaoSap
{
    /// <summary>OP encontrada (HTTP 200) e mapeada.</summary>
    Encontrada,

    /// <summary>OP nao existe no SAP (HTTP 404).</summary>
    NaoEncontrada,

    /// <summary>Integracao indisponivel (erro tecnico/rede/HTTP nao tratado, timeout).</summary>
    Indisponivel,

    /// <summary>Integracao de Ordem de Producao nao configurada (sem URL/credenciais/allowlist).</summary>
    NaoConfigurado
}

/// <summary>
/// Resultado do GET da Ordem de Producao no SAP. Nunca expoe segredo/payload; carrega o cenario,
/// a OP mapeada (quando houver), uma mensagem sanitizada e o status HTTP (quando aplicavel).
/// </summary>
public sealed record ResultadoConsultaOrdemProducaoSap
{
    public CenarioConsultaOrdemProducaoSap Cenario { get; init; }
    public OrdemProducaoSap? Ordem { get; init; }
    public string MensagemSanitizada { get; init; } = string.Empty;
    public int? StatusHttp { get; init; }

    public static ResultadoConsultaOrdemProducaoSap Encontrada(OrdemProducaoSap ordem)
        => new()
        {
            Cenario = CenarioConsultaOrdemProducaoSap.Encontrada,
            Ordem = ordem,
            StatusHttp = 200,
            MensagemSanitizada = $"Ordem de producao {ordem.NumeroOrdem} consultada no SAP."
        };

    public static ResultadoConsultaOrdemProducaoSap NaoEncontrada(int? statusHttp = 404)
        => new()
        {
            Cenario = CenarioConsultaOrdemProducaoSap.NaoEncontrada,
            StatusHttp = statusHttp,
            MensagemSanitizada = "Ordem de producao nao encontrada no SAP."
        };

    public static ResultadoConsultaOrdemProducaoSap Indisponivel(string mensagem, int? statusHttp = null)
        => new()
        {
            Cenario = CenarioConsultaOrdemProducaoSap.Indisponivel,
            StatusHttp = statusHttp,
            MensagemSanitizada = mensagem
        };

    public static ResultadoConsultaOrdemProducaoSap NaoConfigurado(string mensagem)
        => new()
        {
            Cenario = CenarioConsultaOrdemProducaoSap.NaoConfigurado,
            MensagemSanitizada = mensagem
        };
}
