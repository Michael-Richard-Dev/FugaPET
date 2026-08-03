using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Abstração do gateway SAP de criação de Handling Unit de caixa individual
/// (API_HANDLINGUNIT / OP_HANDLINGUNIT_0001). UMA tentativa controlada por chamada. Injetável no
/// Controller/testes (fake controlado). Nesta fase não há implementação HTTP real — o contrato externo
/// ainda não foi confirmado pelo Ares.
/// </summary>
public interface IProdutoAcabadoHandlingUnitSapServico
{
    /// <summary>True quando o serviço está autorizado a executar o POST real (sempre false nesta fase).</summary>
    bool EnvioAutorizado { get; }

    Task<ResultadoCriacaoHandlingUnitCaixa> CriarHandlingUnitCaixaAsync(
        ProdutoAcabadoCaixaParaEnvio caixa,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado da tentativa de criação da HU. Sanitizado (nunca senha/Authorization/cookie/token).</summary>
public sealed class ResultadoCriacaoHandlingUnitCaixa
{
    public bool Sucesso { get; init; }
    public string? HandlingUnitExternalId { get; init; }
    public int? HttpStatus { get; init; }
    public string ResponsePayloadSanitizado { get; init; } = string.Empty;
    public string MensagemSanitizada { get; init; } = string.Empty;
    public Guid CorrelationId { get; init; }

    /// <summary>Distingue "POST não autorizado" (fase atual) de erro técnico — não libera reprocessamento como ERRO_SAP.</summary>
    public bool NaoAutorizado { get; init; }
}

/// <summary>
/// Implementação BLOQUEADA para o ambiente atual: não executa HTTP e NÃO simula sucesso. Informa que o
/// POST da HU ainda não foi autorizado (contrato externo pendente), preservando o correlation_id da caixa.
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado : IProdutoAcabadoHandlingUnitSapServico
{
    public bool EnvioAutorizado => false;

    public Task<ResultadoCriacaoHandlingUnitCaixa> CriarHandlingUnitCaixaAsync(
        ProdutoAcabadoCaixaParaEnvio caixa,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caixa);
        return Task.FromResult(new ResultadoCriacaoHandlingUnitCaixa
        {
            Sucesso = false,
            NaoAutorizado = true,
            HandlingUnitExternalId = null,
            HttpStatus = null,
            ResponsePayloadSanitizado = string.Empty,
            CorrelationId = caixa.CorrelationId,
            MensagemSanitizada =
                "Envio da Handling Unit ao SAP ainda não autorizado (contrato OP_HANDLINGUNIT_0001 pendente). "
                + "A caixa permanece persistida e o correlation_id é preservado."
        });
    }
}
