using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Resultado TIPADO da orquestração do envio após "Confirmar Consumo". Substitui o par
/// (mensagem, MessageBoxIcon) como regra de domínio: o ícone é só apresentação e NUNCA decide se a
/// operação pode ser finalizada.
///
/// Somente <see cref="ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente"/> e
/// <see cref="ResultadoExecucaoProcessoApontamento.ConfirmadoSap"/> liberam
/// EM_ANDAMENTO → AGUARDANDO_FINALIZACAO. ErroSap, DivergenciaSap e NaoConcluido mantêm EM_ANDAMENTO.
/// </summary>
public sealed class ResultadoOrquestracaoConsumoApontamento
{
    public required ResultadoExecucaoProcessoApontamento Resultado { get; init; }
    public long? CodigoLancamento { get; init; }
    public required string Mensagem { get; init; }
    public bool ConfirmadoSap { get; init; }
    public int? StatusHttp { get; init; }

    /// <summary>Falha em que o documento PODE ter sido criado no SAP (não é rejeição comprovada).</summary>
    public bool ResultadoIndeterminado { get; init; }

    /// <summary>Apresentação apenas — jamais usar como regra de domínio.</summary>
    public bool ExibirComoSucesso => Resultado is ResultadoExecucaoProcessoApontamento.ConfirmadoSap
        or ResultadoExecucaoProcessoApontamento.ConcluidoLocalmente;

    // ---- Fábricas por cenário (a regra fica explícita e testável) ----

    /// <summary>261 confirmado com documento/ano.</summary>
    public static ResultadoOrquestracaoConsumoApontamento Confirmado(
        long? codigoLancamento, string mensagem, int? statusHttp)
        => new()
        {
            Resultado = ResultadoExecucaoProcessoApontamento.ConfirmadoSap,
            CodigoLancamento = codigoLancamento,
            Mensagem = mensagem,
            ConfirmadoSap = true,
            StatusHttp = statusHttp
        };

    /// <summary>Rejeição COMPROVADA do SAP: falha segura (não criou documento).</summary>
    public static ResultadoOrquestracaoConsumoApontamento ErroSap(
        long? codigoLancamento, string mensagem, int? statusHttp)
        => new()
        {
            Resultado = ResultadoExecucaoProcessoApontamento.ErroSap,
            CodigoLancamento = codigoLancamento,
            Mensagem = mensagem,
            StatusHttp = statusHttp
        };

    /// <summary>Resultado INDETERMINADO: pode ter criado documento. Não libera término.</summary>
    public static ResultadoOrquestracaoConsumoApontamento Divergencia(
        long? codigoLancamento, string mensagem, int? statusHttp)
        => new()
        {
            Resultado = ResultadoExecucaoProcessoApontamento.DivergenciaSap,
            CodigoLancamento = codigoLancamento,
            Mensagem = mensagem,
            StatusHttp = statusHttp,
            ResultadoIndeterminado = true
        };

    /// <summary>Rota que não envia (Misto/Bloqueado/Backflush): salvo local, mas SEM liberar término.</summary>
    public static ResultadoOrquestracaoConsumoApontamento NaoConcluido(long? codigoLancamento, string mensagem)
        => new()
        {
            Resultado = ResultadoExecucaoProcessoApontamento.NaoConcluido,
            CodigoLancamento = codigoLancamento,
            Mensagem = mensagem
        };

    /// <summary>
    /// Classifica o retorno do 261 sem depender de ícone: sucesso → Confirmado; falha indeterminada →
    /// Divergência; rejeição comprovada → ErroSap. Envio ausente (não executado) → indeterminado.
    /// </summary>
    public static ResultadoOrquestracaoConsumoApontamento DoEnvio261(
        long? codigoLancamento, ResultadoEnvioConsumoSap261? envio, string mensagemPadraoFalha)
    {
        if (envio is null)
        {
            // Sem resultado do envio não há como afirmar que nada foi criado.
            return Divergencia(codigoLancamento, mensagemPadraoFalha, null);
        }

        if (envio.Sucesso)
        {
            return Confirmado(codigoLancamento, envio.Mensagem, envio.StatusHttp);
        }

        string mensagem = string.IsNullOrWhiteSpace(envio.Mensagem) ? mensagemPadraoFalha : envio.Mensagem;
        return envio.ResultadoIndeterminado
            ? Divergencia(codigoLancamento, mensagem, envio.StatusHttp)
            : ErroSap(codigoLancamento, mensagem, envio.StatusHttp);
    }
}
