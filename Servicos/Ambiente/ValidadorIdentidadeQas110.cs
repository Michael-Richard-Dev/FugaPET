using FugaPET_Dev.Modelo.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Ambiente;

/// <summary>
/// GATE 126X: emite a identidade QAS110 SOMENTE a partir da configuracao EFETIVAMENTE CARREGADA pelo
/// runtime, reutilizando os contratos existentes — nao duplica o validador de inicializacao.
/// <para>
/// Condicoes CUMULATIVAS:
/// <list type="number">
/// <item><see cref="ConfiguracaoSap.PerfilLocalQ"/> true — bandeira gravada por
///   <see cref="LeitorConfiguracaoSap"/> a partir do perfil REALMENTE lido do arquivo local
///   (<see cref="PerfilAmbienteLocal"/>, GATE 120G). Nao e um texto livre do chamador;</item>
/// <item>destino SAP Q aprovado por <see cref="ValidadorAmbienteQ.ValidarDestinoSapQ"/>: toda URL SAP
///   efetiva em https com host Q e porta 44300, allowlist com o host Q e SEM o host DS, sap-client de
///   3 digitos;</item>
/// <item>quando a rota CPI/INT012 estiver configurada, ela precisa ser absoluta, https e com host dentro
///   da PROPRIA allowlist do INT012 (contrato 046-K).</item>
/// </list>
/// </para>
/// <para>
/// O PERFIL, ISOLADO, NAO BASTA e o MANDANTE, ISOLADO, NAO BASTA: o mandante 110 tambem e usado no DS.
/// O discriminante material de ambiente e o DESTINO validado por IGUALDADE EXATA de host e porta — nunca
/// Contains, StartsWith ou comparacao parcial de nome.
/// </para>
/// </summary>
public static class ValidadorIdentidadeQas110
{
    public static ResultadoIdentidadeQas110 Resolver(ConfiguracaoSap? configuracaoSap)
    {
        if (configuracaoSap is null)
        {
            return ResultadoIdentidadeQas110.Bloqueada(
                CenarioIdentidadeQas110.ConfiguracaoAusente,
                "configuracao SAP ausente: identidade QAS110 nao pode ser comprovada.");
        }

        if (!configuracaoSap.PerfilLocalQ)
        {
            return ResultadoIdentidadeQas110.Bloqueada(
                CenarioIdentidadeQas110.PerfilNaoQ,
                "a configuracao carregada nao foi reconhecida com perfil local Q.");
        }

        // GATE 126G: o destino passou a ser validado como DECLARADO pela propria instalacao, e nao
        // mais cravado no host do Q. O que se exige continua sendo um destino INEQUIVOCO — todas as
        // URLs efetivas no mesmo host e porta, allowlist com exatamente esse host, mandante de 3
        // digitos. Ambiente misturado segue bloqueado; o que deixou de ser bloqueado e a instalacao
        // que aponta, sem ambiguidade, para um SAP que nao e o do Q.
        //
        // O caminho de ARRANQUE do Q (ValidadorAmbienteQ.ValidarDestinoSapQ) NAO foi tocado: la o
        // host do Q continua obrigatorio.
        ResultadoValidacaoAmbienteQ destino = ValidadorAmbienteQ.ValidarDestinoSapDeclarado(configuracaoSap);
        if (!destino.Valido)
        {
            return ResultadoIdentidadeQas110.Bloqueada(
                CenarioIdentidadeQas110.DestinoNaoQ,
                "destino SAP nao e inequivoco: " + destino.Mensagem);
        }

        if (!RotaCpiConsistente(configuracaoSap, out string motivoCpi))
        {
            return ResultadoIdentidadeQas110.Bloqueada(
                CenarioIdentidadeQas110.RotaCpiInconsistente,
                "rota CPI/INT012 inconsistente com a propria allowlist: " + motivoCpi);
        }

        // A identidade carrega o destino COMPROVADO, nao constantes do Q: e ele que distingue uma
        // instalacao da outra quando o mandante coincide.
        (string host, int porta) = ValidadorAmbienteQ.ObterDestinoEfetivo(configuracaoSap);

        return ResultadoIdentidadeQas110.Ok(IdentidadeAmbienteQas110.Emitir(
            configuracaoSap.SapClient.Trim(), host, porta));
    }

    /// <summary>
    /// Rota CPI/INT012: validada por INTEGRIDADE quando presente (absoluta, https, host na allowlist
    /// PROPRIA do INT012 — contrato 046-K). Ausencia NAO bloqueia, porque a tara e necessaria ao preview,
    /// que existe independentemente de a escrita de palete estar configurada.
    /// <para>
    /// LIMITE DECLARADO: o tenant CPI e compartilhado entre ambientes, portanto o host do CPI NAO
    /// discrimina Q de DS. Esta verificacao e de CONSISTENCIA, nunca de isolamento; o isolamento vem do
    /// destino SAP validado.
    /// </para>
    /// </summary>
    private static bool RotaCpiConsistente(ConfiguracaoSap configuracao, out string motivo)
    {
        motivo = string.Empty;
        string rota = (configuracao.PalletInt012BaseUrl ?? string.Empty).Trim();
        if (rota.Length == 0)
        {
            return true;
        }

        if (!Uri.TryCreate(rota, UriKind.Absolute, out Uri? uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            motivo = "rota configurada nao e uma URL https absoluta.";
            return false;
        }

        bool hostPermitido = configuracao.PalletInt012HostsPermitidos.Any(host =>
            string.Equals((host ?? string.Empty).Trim(), uri.Host, StringComparison.OrdinalIgnoreCase));
        if (!hostPermitido)
        {
            motivo = "host da rota nao consta na allowlist dedicada do INT012.";
            return false;
        }

        return true;
    }
}
