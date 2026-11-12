using System.Net;
using System.Security.Authentication;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// GATE 105D: falha TÉCNICA tipada da consulta SAP. Carrega o cenário classificado na borda da integração
/// para que nenhuma camada superior precise inspecionar HttpStatusCode/HttpRequestException — e para que
/// falha técnica nunca vire status de negócio. NUNCA transporta segredo: a mensagem técnica é sanitizada
/// (sem senha, Authorization/Basic, token, cookie ou corpo bruto da resposta).
/// </summary>
public sealed class ConsultaSapException : Exception
{
    public ConsultaSapException(
        CenarioFalhaConsultaSap cenario,
        int? httpStatus = null,
        string? mensagemTecnicaSanitizada = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(MontarMensagemSegura(cenario, httpStatus), innerException)
    {
        Cenario = cenario;
        HttpStatus = httpStatus;
        MensagemTecnicaSanitizada = LogIntegracaoSapServico.SanitizarMensagem(mensagemTecnicaSanitizada);
        CorrelationId = correlationId;
    }

    public CenarioFalhaConsultaSap Cenario { get; }
    public int? HttpStatus { get; }
    public string? MensagemTecnicaSanitizada { get; }
    public string? CorrelationId { get; }

    // Mensagem de exceção propositalmente genérica e sem dado sensível (a mensagem ao operador vem de
    // MensagensFalhaConsultaSap, não daqui).
    private static string MontarMensagemSegura(CenarioFalhaConsultaSap cenario, int? httpStatus)
        => httpStatus is int status
            ? $"Falha na consulta SAP. Cenario={cenario}; HttpStatus={status}."
            : $"Falha na consulta SAP. Cenario={cenario}.";
}

/// <summary>
/// GATE 105D: classificador ÚNICO de falhas de consulta SAP. Concentra a leitura de HTTP/exceções de
/// transporte para que os serviços e a UI trabalhem apenas com <see cref="CenarioFalhaConsultaSap"/>.
/// </summary>
public static class ClassificadorFalhaConsultaSap
{
    /// <summary>Cenário a partir do status HTTP de uma resposta NÃO-sucesso.</summary>
    public static CenarioFalhaConsultaSap ClassificarHttp(int statusHttp)
        => statusHttp switch
        {
            401 => CenarioFalhaConsultaSap.NaoAutenticado,
            403 => CenarioFalhaConsultaSap.SemAutorizacao,
            404 => CenarioFalhaConsultaSap.NaoEncontrado,
            >= 500 and <= 599 => CenarioFalhaConsultaSap.SapIndisponivel,
            _ => CenarioFalhaConsultaSap.HttpNaoClassificado
        };

    public static CenarioFalhaConsultaSap ClassificarHttp(HttpStatusCode statusHttp)
        => ClassificarHttp((int)statusHttp);

    /// <summary>
    /// Cenário a partir de uma exceção de transporte/parse. Cancelamento SOLICITADO pelo chamador não é
    /// falha: o chamador deve propagar <see cref="OperationCanceledException"/> antes de chamar este método.
    /// </summary>
    public static CenarioFalhaConsultaSap ClassificarExcecao(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        if (excecao is ConsultaSapException tipada)
        {
            return tipada.Cenario;
        }

        if (excecao is JsonException)
        {
            return CenarioFalhaConsultaSap.RespostaInvalida;
        }

        // Timeout do HttpClient chega como TaskCanceledException/OperationCanceledException.
        // (O cancelamento do chamador é tratado ANTES, no ponto de chamada.)
        if (excecao is OperationCanceledException)
        {
            return CenarioFalhaConsultaSap.Timeout;
        }

        if (excecao is HttpRequestException requisicao)
        {
            // TLS/handshake tem AuthenticationException na cadeia interna.
            if (PossuiNaCadeia<AuthenticationException>(requisicao))
            {
                return CenarioFalhaConsultaSap.Tls;
            }

            if (PossuiNaCadeia<TimeoutException>(requisicao))
            {
                return CenarioFalhaConsultaSap.Timeout;
            }

            return requisicao.StatusCode is HttpStatusCode status
                ? ClassificarHttp(status)
                : CenarioFalhaConsultaSap.Conectividade;
        }

        // Configuração/endpoint/allowlist rejeitados antes de qualquer I/O.
        if (excecao is UriFormatException or ArgumentException or InvalidOperationException)
        {
            return CenarioFalhaConsultaSap.ConfiguracaoInvalida;
        }

        if (excecao is TimeoutException)
        {
            return CenarioFalhaConsultaSap.Timeout;
        }

        return CenarioFalhaConsultaSap.HttpNaoClassificado;
    }

    /// <summary>Converte qualquer exceção em <see cref="ConsultaSapException"/> preservando o cenário.</summary>
    public static ConsultaSapException Tipar(Exception excecao, string? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        if (excecao is ConsultaSapException tipada)
        {
            return tipada;
        }

        CenarioFalhaConsultaSap cenario = ClassificarExcecao(excecao);
        int? status = excecao is HttpRequestException { StatusCode: HttpStatusCode codigo } ? (int)codigo : null;

        // Só o TIPO da exceção entra no diagnóstico — nunca a mensagem original nem o corpo da resposta.
        return new ConsultaSapException(
            cenario,
            status,
            $"Falha tecnica na consulta SAP ({excecao.GetType().Name}).",
            correlationId,
            excecao);
    }

    private static bool PossuiNaCadeia<TExcecao>(Exception excecao)
        where TExcecao : Exception
    {
        for (Exception? atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is TExcecao)
            {
                return true;
            }
        }

        return false;
    }
}
