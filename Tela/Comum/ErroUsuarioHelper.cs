using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Controle;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// Centraliza o tratamento de excecao na camada de UI:
///   1) registra o detalhe TECNICO (ex.ToString()) em auditoria_acao_usuario;
///   2) devolve uma mensagem AMIGAVEL (sem detalhe tecnico) para exibir ao operador.
///
/// Nunca exiba ex.Message diretamente na tela — pode vazar servidor, porta,
/// caminho interno, constraint do banco e outros detalhes que o operador nao deve ver.
/// </summary>
public static class ErroUsuarioHelper
{
    private const string MensagemPadrao = "Não foi possível concluir a operação. Acione o suporte.";

    private static readonly Lazy<AuditoriaServico> Auditoria =
        new(FabricaControladoresCadastro.CriarAuditoriaServico);

    /// <summary>
    /// Versao SINCRONA (fire-and-forget): dispara a auditoria sem aguardar e retorna a
    /// mensagem amigavel. A auditoria roda em RegistrarErroSeguroAsync, que TRATA a falha
    /// (vai para Trace) — entao o erro nunca morre silenciosamente, mesmo sem await.
    /// Use em handlers sincronos / erros comuns. Para fluxos assincronos ou telas criticas
    /// (seguranca, acesso negado), prefira <see cref="TratarAsync"/> com await.
    /// </summary>
    /// <param name="acao">Codigo da acao para auditoria (ex.: "IMPRESSAO_ERRO").</param>
    /// <param name="ex">Excecao capturada (o ToString completo vai para auditoria).</param>
    /// <param name="tela">Tela de origem.</param>
    /// <param name="mensagemAmigavel">Mensagem amigavel opcional; se nula, usa o tratamento padrao.</param>
    public static string Tratar(string acao, Exception ex, string tela, string? mensagemAmigavel = null)
    {
        _ = RegistrarErroSeguroAsync(acao, ex, tela);
        return ResolverMensagemAmigavel(ex, mensagemAmigavel);
    }

    /// <summary>
    /// Versao ASSINCRONA: AGUARDA (await) o registro da auditoria antes de retornar a
    /// mensagem amigavel. Use em fluxos assincronos e telas criticas, onde a confirmacao
    /// do registro importa. Nunca lanca por falha de auditoria (tratada internamente).
    /// </summary>
    public static async Task<string> TratarAsync(string acao, Exception ex, string tela, string? mensagemAmigavel = null)
    {
        await RegistrarErroSeguroAsync(acao, ex, tela);
        return ResolverMensagemAmigavel(ex, mensagemAmigavel);
    }

    private static async Task RegistrarErroSeguroAsync(string acao, Exception ex, string tela)
    {
        try
        {
            await Auditoria.Value.RegistrarErroAsync(acao, ex.ToString(), tela);
        }
        catch (Exception erroAuditoria)
        {
            // Auditoria nunca pode quebrar o fluxo da UI; o detalhe vai para o log de diagnostico.
            System.Diagnostics.Trace.TraceError(
                $"Falha ao auditar erro '{acao}' ({tela}): {erroAuditoria}");
        }
    }

    private static string ResolverMensagemAmigavel(Exception ex, string? mensagemAmigavel)
    {
        if (!string.IsNullOrWhiteSpace(mensagemAmigavel))
        {
            return mensagemAmigavel;
        }

        string amigavel = ErroBancoTratado.ObterMensagemAmigavel(ex);
        return string.IsNullOrWhiteSpace(amigavel) ? MensagemPadrao : amigavel;
    }
}
