using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>Cenário do POST de criação da HU (uma tentativa controlada).</summary>
public enum CenarioPostHandlingUnit
{
    /// <summary>HTTP 201: HU criada e confirmada.</summary>
    Confirmado,

    /// <summary>Erro determinístico (não 401/403). Pode ou não liberar reprocessamento.</summary>
    ErroDefinitivo,

    /// <summary>HTTP 401/403: não autorizado. Nunca reprocessável.</summary>
    NaoAutorizado,

    /// <summary>Timeout APÓS possível POST: resultado indeterminado (sem retry, sem 2º POST).</summary>
    Timeout,

    /// <summary>Envio não realizado (gateway fail-closed nesta frente): a caixa permanece intacta.</summary>
    NaoEnviado
}

/// <summary>Cenário do GET de reconciliação da HU.</summary>
public enum CenarioReconciliacaoHandlingUnit
{
    Confirmado,
    NaoEncontrado,
    Indeterminado
}

/// <summary>
/// Abstração do gateway SAP de Handling Unit de caixa (API_HANDLINGUNIT, OData V4/0001). UMA tentativa
/// controlada por chamada. Injetável no Service/testes (fake). Resultados SANITIZADOS: nunca
/// Authorization/cookie/CSRF/token. O default produtivo é fail-closed (não executa POST real).
/// </summary>
public interface IProdutoAcabadoHandlingUnitSapServico
{
    /// <summary>True quando o gateway está autorizado a executar o POST real. Fail-closed por padrão.</summary>
    bool EnvioAutorizado { get; }

    /// <summary>POST de criação da HU (uma tentativa). Nunca lança por rede: classifica em cenário sanitizado.</summary>
    Task<ResultadoPostHandlingUnit> CriarHandlingUnitCaixaAsync(
        HandlingUnitCaixaRequest request, string endpointRelativo, CancellationToken cancellationToken = default);

    /// <summary>GET de reconciliação por HU conhecida (após INDETERMINADO_TIMEOUT). Mocado nesta frente.</summary>
    Task<ResultadoReconciliacaoHandlingUnit> ReconciliarHandlingUnitAsync(
        string handlingUnitExternalId, string warehouse, CancellationToken cancellationToken = default);
}

/// <summary>Resultado do POST HU. Sanitizado. Campos SAP presentes só quando confirmado.</summary>
public sealed class ResultadoPostHandlingUnit
{
    public CenarioPostHandlingUnit Cenario { get; init; }
    public string? HandlingUnitExternalId { get; init; }
    public string? Warehouse { get; init; }
    public int? HttpStatus { get; init; }
    public string? ResponseJsonSanitizado { get; init; }
    public string? SapMessagesJson { get; init; }
    public string? Etag { get; init; }
    public string? CreatedByUserSap { get; init; }
    public DateTimeOffset? CreationDatetimeSap { get; init; }
    public string MensagemSanitizada { get; init; } = string.Empty;

    /// <summary>Só relevante em ErroDefinitivo: libera (ou não) reprocessamento. NaoAutorizado ⇒ sempre false.</summary>
    public bool PodeReprocessar { get; init; }
}

/// <summary>Resultado do GET de reconciliação. Sanitizado.</summary>
public sealed class ResultadoReconciliacaoHandlingUnit
{
    public CenarioReconciliacaoHandlingUnit Cenario { get; init; }
    public string? HandlingUnitExternalId { get; init; }
    public string? Warehouse { get; init; }
    public int? HttpStatus { get; init; }
    public string? ResponseJsonSanitizado { get; init; }
    public string? SapMessagesJson { get; init; }
    public string? Etag { get; init; }
    public string? CreatedByUserSap { get; init; }
    public DateTimeOffset? CreationDatetimeSap { get; init; }
    public string MensagemSanitizada { get; init; } = string.Empty;

    /// <summary>True quando a HU retornada corresponde à esperada (comparação aprovada). Exigido para confirmar.</summary>
    public bool ComparacaoAprovada { get; init; }
}

/// <summary>
/// Gateway BLOQUEADO (fail-closed) padrão: NÃO executa HTTP e NÃO simula sucesso. POST retorna NaoEnviado
/// (a caixa permanece persistida); reconciliação retorna Indeterminado. É o default produtivo/DEV desta
/// frente — o POST real só será exercitado por uma implementação explicitamente autorizada.
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado : IProdutoAcabadoHandlingUnitSapServico
{
    public bool EnvioAutorizado => false;

    public Task<ResultadoPostHandlingUnit> CriarHandlingUnitCaixaAsync(
        HandlingUnitCaixaRequest request, string endpointRelativo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new ResultadoPostHandlingUnit
        {
            Cenario = CenarioPostHandlingUnit.NaoEnviado,
            MensagemSanitizada =
                "Envio da Handling Unit ao SAP não autorizado nesta frente (gateway fail-closed). "
                + "A caixa permanece persistida; nenhum POST foi executado."
        });
    }

    public Task<ResultadoReconciliacaoHandlingUnit> ReconciliarHandlingUnitAsync(
        string handlingUnitExternalId, string warehouse, CancellationToken cancellationToken = default)
        => Task.FromResult(new ResultadoReconciliacaoHandlingUnit
        {
            Cenario = CenarioReconciliacaoHandlingUnit.Indeterminado,
            MensagemSanitizada = "Reconciliação SAP não autorizada nesta frente (gateway fail-closed)."
        });
}
