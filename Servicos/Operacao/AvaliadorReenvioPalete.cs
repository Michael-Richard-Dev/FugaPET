using System.Text.RegularExpressions;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>O que o operador pode fazer com um palete que nao foi confirmado no SAP.</summary>
public enum SituacaoReenvioPalete
{
    /// <summary>Nem enviar nem liberar: exige reconciliacao com evidencia externa, ou nao ha o que fazer.</summary>
    Bloqueado = 0,

    /// <summary>Pode enviar agora (nunca enviado, ou reprocessamento ja liberado).</summary>
    PodeEnviar = 1,

    /// <summary>Recusa do SAP COMPROVADA: pode liberar o reprocessamento e enviar de novo.</summary>
    PodeLiberar = 2
}

/// <summary>Veredito sobre o reenvio, com o motivo a ser mostrado ao operador.</summary>
public sealed record AvaliacaoReenvioPalete(SituacaoReenvioPalete Situacao, string Motivo);

/// <summary>
/// GATE 126L: decide, a partir do estado PERSISTIDO do palete, se um novo envio pode acontecer.
/// <para>
/// POR QUE EXISTE: depois de um erro o palete ficava sem saida na tela -- "nao elegivel para novo
/// envio normal" --, embora o contrato 122C ja descrevesse o estado ERRO_SAP como "exige liberacao
/// explicita antes de novo envio" e o banco ja tivesse a funcao de liberacao, o claim que aceita
/// ERRO_SAP com pode_reprocessar e ate os valores de auditoria reservados. Faltava apenas quem
/// decidisse e chamasse.
/// </para>
/// <para>
/// A REGRA DE SEGURANCA, em uma frase: so se libera quando esta COMPROVADO que o SAP nao criou a HU.
/// Prova de ausencia, nunca ausencia de prova -- um 500 de corpo vazio ou um timeout NAO provam nada
/// e continuam bloqueados, porque reenviar neles duplicaria o palete no SAP.
/// </para>
/// <para>Funcao PURA: nao le banco, nao chama SAP e nao decide sozinha nada que o usuario nao confirme.</para>
/// </summary>
public static class AvaliadorReenvioPalete
{
    internal const string MotivoJaCriadoNoSap =
        "O SAP JA criou a HU deste palete. Reenviar duplicaria o palete: novo envio proibido.";

    internal const string MotivoLiberado =
        "Reprocessamento ja liberado: o palete pode ser enviado novamente.";

    internal const string MotivoNuncaEnviado =
        "Palete ainda nao enviado.";

    internal const string MotivoRecusaComprovada =
        "O SAP recusou o palete e NAO criou HU alguma (resposta com Status \"E\" e sem UC gerada). "
        + "Corrigida a causa, o reenvio e seguro.";

    internal const string MotivoSemProvaDeAusencia =
        "Nao ha prova de que o SAP deixou de criar a HU (resposta vazia, timeout ou corpo nao "
        + "conclusivo). Reenviar poderia duplicar o palete: e necessaria reconciliacao com evidencia "
        + "obtida no SAP.";

    internal const string MotivoEnvioEmAberto =
        "Ha um envio em aberto sem resultado fechado: e necessaria reconciliacao antes de qualquer "
        + "novo envio.";

    internal const string MotivoConfirmado =
        "Palete ja CONFIRMADO no SAP: nao existe reenvio.";

    internal const string MotivoEstadoNaoComprovado =
        "Estado do palete nao comprovado: novo envio bloqueado (fail-closed).";

    public static AvaliacaoReenvioPalete Avaliar(
        string? statusPalete,
        bool podeReprocessar,
        string? handlingUnitExterna,
        string? huPalete,
        int? statusHttp,
        string? respostaSanitizada)
    {
        // PRIMEIRO de tudo, antes de olhar status: se ha HU gravada, o SAP criou o palete. Esta
        // verificacao vem na frente de proposito -- qualquer outro raciocinio depois dela seria
        // sobre um palete que ja existe la.
        if (TemTexto(handlingUnitExterna) || TemTexto(huPalete))
        {
            return new(SituacaoReenvioPalete.Bloqueado, MotivoJaCriadoNoSap);
        }

        string status = (statusPalete ?? string.Empty).Trim().ToUpperInvariant();

        return status switch
        {
            "RASCUNHO" => new(SituacaoReenvioPalete.PodeEnviar, MotivoNuncaEnviado),

            // O claim do banco aceita ERRO_SAP e BLOQUEADO quando o reprocessamento foi liberado.
            "ERRO_SAP" or "BLOQUEADO" when podeReprocessar
                => new(SituacaoReenvioPalete.PodeEnviar, MotivoLiberado),

            "ERRO_SAP" or "BLOQUEADO" => AusenciaComprovada(statusHttp, respostaSanitizada)
                ? new(SituacaoReenvioPalete.PodeLiberar, MotivoRecusaComprovada)
                : new(SituacaoReenvioPalete.Bloqueado, MotivoSemProvaDeAusencia),

            // Claim em aberto / resultado nao fechado: so reconciliacao.
            "ENVIADO_SAP" or "INDETERMINADO" or "INDETERMINADO_TIMEOUT"
                => new(SituacaoReenvioPalete.Bloqueado, MotivoEnvioEmAberto),

            "CONFIRMADO_SAP" => new(SituacaoReenvioPalete.Bloqueado, MotivoConfirmado),

            // Status vazio, desconhecido ou CANCELADO: fail-closed.
            _ => new(SituacaoReenvioPalete.Bloqueado, MotivoEstadoNaoComprovado)
        };
    }

    /// <summary>
    /// Prova de que o SAP processou e RECUSOU sem criar nada. Exige, cumulativamente, uma resposta
    /// recebida (houve status HTTP), o Status de aplicacao "E" e a UC VAZIA.
    /// <para>
    /// Os tres juntos sao o ponto: "Status E" sozinho nao basta se vier UC preenchida (seria recusa
    /// parcial com objeto criado), e a ausencia de UC sozinha nao basta porque um corpo vazio
    /// tambem nao tem UC -- e corpo vazio nao prova nada.
    /// </para>
    /// </summary>
    internal static bool AusenciaComprovada(int? statusHttp, string? resposta)
    {
        if (statusHttp is null || !TemTexto(resposta))
        {
            return false;
        }

        string texto = resposta!;

        bool statusErro = Regex.IsMatch(texto, "\"Status\"\\s*:\\s*\"E\"", RegexOptions.IgnoreCase);
        if (!statusErro)
        {
            return false;
        }

        // UC presente e nao vazia derruba a prova: alguma coisa foi criada.
        Match uc = Regex.Match(texto, "\"UC_gerada\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (!uc.Success)
        {
            return false;   // sem o campo, nao se pode afirmar que nao houve UC
        }

        return uc.Groups[1].Value.Trim().Length == 0;
    }

    private static bool TemTexto(string? valor) => !string.IsNullOrWhiteSpace(valor);
}
