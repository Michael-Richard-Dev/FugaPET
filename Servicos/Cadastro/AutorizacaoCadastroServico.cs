using FugaPET_Dev.Servicos.Auditoria;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Cadastro;

internal static class AutorizacaoCadastroServico
{
    public static Task<ResultadoOperacao?> BloquearSeNaoPodeGerenciarAsync(
        string rotina,
        string acao,
        AuditoriaServico auditoriaServico,
        string? tela = null,
        CancellationToken cancellationToken = default)
    {
        return BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Cadastro,
            rotina,
            acao,
            auditoriaServico,
            tela,
            cancellationToken);
    }

    public static Task<ResultadoOperacao?> BloquearSeNaoPodeGerenciarEtiquetaAsync(
        string rotina,
        string acao,
        AuditoriaServico auditoriaServico,
        string? tela = null,
        CancellationToken cancellationToken = default)
    {
        return BloquearSeNaoPossuiPermissaoAsync(
            PermissoesSistema.Modulos.Etiqueta,
            rotina,
            acao,
            auditoriaServico,
            tela,
            cancellationToken);
    }

    public static async Task<ResultadoOperacao?> BloquearSeNaoPossuiPermissaoAsync(
        string modulo,
        string rotina,
        string acao,
        AuditoriaServico auditoriaServico,
        string? tela = null,
        CancellationToken cancellationToken = default)
    {
        if (AutorizacaoServico.PossuiPermissao(modulo, rotina, acao))
        {
            return null;
        }

        string mensagem = AutorizacaoServico.MensagemSemPermissao(modulo, rotina, acao);
        await RegistrarAcessoNegadoAsync(auditoriaServico, mensagem, tela, cancellationToken);
        return ResultadoOperacao.Falha(mensagem);
    }

    private static async Task RegistrarAcessoNegadoAsync(
        AuditoriaServico auditoriaServico,
        string mensagem,
        string? tela,
        CancellationToken cancellationToken)
    {
        long? codigoUsuario = EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario;
        if (!codigoUsuario.HasValue)
        {
            return;
        }

        await auditoriaServico.RegistrarAcessoNegadoAsync(codigoUsuario.Value, mensagem, tela, cancellationToken);
    }
}
