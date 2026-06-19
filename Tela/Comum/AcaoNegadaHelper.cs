using FugaPET_Dev.Controle;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Tela.Comum;

/// <summary>
/// Auditoria de ACOES operacionais negadas por falta de permissao (ex.: executar/finalizar/
/// cancelar leitura). Em ambiente industrial, tentativa negada e evento de seguranca e deve
/// ser registrada em auditoria_acao_usuario — nao basta statusLabel + MessageBox.
/// O registro e aguardado (await) e tratado: falha de auditoria nao quebra a UI, mas vai
/// para o log de diagnostico (nunca fire-and-forget silencioso).
/// </summary>
internal static class AcaoNegadaHelper
{
    public static async Task RegistrarAcaoNegadaSeguroAsync(
        string modulo,
        string rotina,
        string acao,
        string descricaoAcao,
        string tela,
        FugaPET_Dev.Servicos.Auditoria.AuditoriaServico? auditoria = null)
    {
        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (!codigoUsuario.HasValue)
        {
            return;
        }

        try
        {
            // 'auditoria' permite injetar um spy em testes; em producao usa a fabrica padrao.
            FugaPET_Dev.Servicos.Auditoria.AuditoriaServico servicoAuditoria =
                auditoria ?? FabricaControladoresCadastro.CriarAuditoriaServico();

            await servicoAuditoria
                .RegistrarAcessoNegadoAsync(
                    codigoUsuario.Value,
                    $"Acao negada: {descricaoAcao} ({modulo}/{rotina}/{acao}).",
                    tela);
        }
        catch (Exception ex)
        {
            // Auditoria nao pode quebrar o fluxo da tela; detalhe tecnico vai para o log.
            System.Diagnostics.Trace.TraceError(
                $"Falha ao auditar acao negada ({descricaoAcao}): {ex}");
        }
    }
}
