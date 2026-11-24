using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// A POLÍTICA da liberação gerencial de tolerância, num lugar só, sem saber o que está sendo
/// liberado.
/// <para>
/// Existe porque há dois logs: o do Recebimento (070, por pedido e item) e o da produção (071, por
/// ordem e operação). As TABELAS precisam ser duas — pedido e ordem são identidades diferentes, e
/// enfiar as duas numa só exigiria afrouxar colunas que hoje garantem integridade de linhas já
/// auditadas. A DECISÃO, não: ela é idêntica, e duas cópias dela divergiriam no dia em que uma
/// fosse corrigida e a outra não. Foi exatamente esse erro que custou o dia 24/11/2026.
/// </para>
/// <para>
/// O que esta classe garante, para os dois:
/// <list type="bullet">
/// <item>sem credencial digitada não se tenta autenticar nem se registra nada;</item>
/// <item>falha ao autenticar NÃO libera, e nem tenta registrar — sem autenticação não se sabe
/// sequer se o login existe, e o banco provavelmente é a causa da exceção;</item>
/// <item>quem libera é quem tem a AÇÃO, não quem se chama "Gerente";</item>
/// <item><b>fail-closed:</b> se a liberação não pode ser REGISTRADA, ela não vale;</item>
/// <item>na recusa o registro é best-effort — a recusa já bloqueia por si.</item>
/// </list>
/// </para>
/// </summary>
internal static class NucleoAutorizacaoTolerancia
{
    public const string MotivoCredencialInvalida = "Credencial inválida.";
    public const string MotivoSemPermissao = "Usuário sem permissão de liberação de tolerância.";
    public const string MotivoLoginVazio = "Informe usuário e senha.";

    public const string MensagemFalhaAoValidar =
        "Não foi possível validar a credencial agora. A pesagem continua bloqueada.";

    public const string MensagemFalhaAoRegistrar =
        "A liberação não pôde ser registrada e por isso não vale. A pesagem continua bloqueada.";

    /// <summary>
    /// Tem a ação LIBERAR_TOLERANCIA? Lê as permissões do USUÁRIO AUTENTICADO AGORA, nunca as da
    /// sessão em curso — a sessão é do operador, que por definição não pode liberar.
    /// </summary>
    public static bool PossuiPermissaoDeLiberacao(SessaoUsuarioAplicacao sessao)
        => sessao?.Permissoes is not null
           && sessao.Permissoes.Any(p => p.Corresponde(
               PermissoesSistema.Modulos.ProcessoProducao,
               PermissoesSistema.Rotinas.EntradaProduto,
               PermissoesSistema.Acoes.LiberarTolerancia));

    /// <summary>
    /// Decide, e manda registrar. <paramref name="registrarAutorizada"/> pode lançar: lançar é o que
    /// faz a liberação NÃO valer, e é intencional.
    /// </summary>
    public static async Task<ResultadoAutorizacaoTolerancia> DecidirAsync(
        string login,
        string senha,
        Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> autenticar,
        Func<long?, string, CancellationToken, Task> registrarAutorizada,
        Func<long?, string, string, CancellationToken, Task> registrarRecusada,
        CancellationToken cancellationToken)
    {
        string usuario = (login ?? string.Empty).Trim();

        if (usuario.Length == 0 || string.IsNullOrEmpty(senha))
        {
            // Sem login digitado não há o que registrar nem a quem atribuir: a tabela exige
            // login_autorizador não vazio, e inventar um valor sujaria a auditoria.
            return ResultadoAutorizacaoTolerancia.Nao(MotivoLoginVazio);
        }

        ResultadoAutenticacao autenticacao;
        try
        {
            autenticacao = await autenticar(usuario, senha, cancellationToken);
        }
        catch (Exception)
        {
            return ResultadoAutorizacaoTolerancia.Nao(MensagemFalhaAoValidar);
        }

        if (!autenticacao.Sucesso || autenticacao.Sessao is null)
        {
            await RegistrarRecusaBestEffortAsync(
                registrarRecusada, null, usuario, MotivoCredencialInvalida, cancellationToken);
            return ResultadoAutorizacaoTolerancia.Nao(MotivoCredencialInvalida);
        }

        SessaoUsuarioAplicacao gerente = autenticacao.Sessao;

        if (!PossuiPermissaoDeLiberacao(gerente))
        {
            // Mensagem ESPECÍFICA de propósito, ao contrário da credencial inválida: a pessoa está em
            // pé na balança e precisa saber que errou de usuário, não de senha.
            await RegistrarRecusaBestEffortAsync(
                registrarRecusada, gerente.IdUsuario, usuario, MotivoSemPermissao, cancellationToken);
            return ResultadoAutorizacaoTolerancia.Nao(MotivoSemPermissao);
        }

        try
        {
            await registrarAutorizada(gerente.IdUsuario, usuario, cancellationToken);
        }
        catch (Exception)
        {
            // FAIL-CLOSED: sem registro, sem liberação.
            return ResultadoAutorizacaoTolerancia.Nao(MensagemFalhaAoRegistrar);
        }

        return ResultadoAutorizacaoTolerancia.Ok(usuario);
    }

    private static async Task RegistrarRecusaBestEffortAsync(
        Func<long?, string, string, CancellationToken, Task> registrarRecusada,
        long? codigoUsuario,
        string login,
        string motivo,
        CancellationToken cancellationToken)
    {
        try
        {
            await registrarRecusada(codigoUsuario, login, motivo, cancellationToken);
        }
        catch (Exception erro)
        {
            // A recusa já bloqueia; perder o registro dela não deve mudar o desfecho. Mas perder o
            // SINAL importa — é tentativa repetida de credencial de gerente que não aparece no log.
            System.Diagnostics.Trace.TraceWarning(
                $"[Tolerancia] Recusa nao registrada: {erro.GetType().Name}");
        }
    }
}
