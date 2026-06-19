using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.Auditoria;

namespace FugaPET_Dev.Servicos.Cadastro;

internal static class TratamentoErroCadastroServico
{
    public static async Task<ResultadoOperacao> TratarFalhaAsync(
        AuditoriaServico auditoriaServico,
        string acao,
        Exception ex,
        string tela,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await auditoriaServico.RegistrarErroAsync(acao, ex.ToString(), tela, cancellationToken);
        }
        catch
        {
            // Nao mascarar a falha original caso a auditoria tambem falhe.
        }

        return ResultadoOperacao.Falha(ErroBancoTratado.ObterMensagemAmigavel(ex));
    }
}
