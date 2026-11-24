using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// Liberacao GERENCIAL de um estouro de tolerancia no Recebimento (incremento 070).
///
/// <para>O operador continua logado: o gerente chega, digita a PROPRIA credencial e autoriza aquela
/// pesagem. A sessao NAO e trocada — ninguem fica operando como gerente depois.</para>
///
/// <para><b>A autenticacao reusa <see cref="AutenticacaoServico"/></b> em vez de comparar senha aqui.
/// E o que garante, de graca, as mesmas regras do login: BCrypt via SenhaServico, usuario inativo
/// barrado, usuario bloqueado barrado. Reimplementar isso seria criar um segundo caminho de
/// autenticacao com regras proprias — e um dia eles divergiriam, justamente no caminho que existe
/// para ser rigoroso.</para>
///
/// <para><b>Permissao, nao perfil.</b> Quem libera e quem tem a acao LIBERAR_TOLERANCIA; o perfil
/// "Gerente" criado pelo 070 e so o veiculo. Checar pelo NOME do perfil amarraria a regra a um texto
/// que qualquer renomeacao quebraria em silencio.</para>
///
/// <para><b>O registro e FAIL-CLOSED na autorizacao.</b> Se a liberacao nao puder ser gravada, ela
/// NAO vale. Autorizar sem registrar produziria exatamente o que o log existe para impedir: um
/// recebimento fora do limite sem ninguem respondendo por ele. Na RECUSA o registro e best-effort —
/// a recusa ja bloqueia por si, e falhar ao registra-la nao deve mudar o desfecho.</para>
/// </summary>
public sealed class AutorizacaoToleranciaServico
{
    private readonly Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> _autenticar;
    private readonly LiberacaoToleranciaRepositorio _repositorio;

    public AutorizacaoToleranciaServico(
        AutenticacaoServico autenticacao,
        LiberacaoToleranciaRepositorio repositorio)
        : this(
            (login, senha, ct) => autenticacao.AutenticarAsync(login, senha, ct),
            repositorio)
    {
    }

    internal AutorizacaoToleranciaServico(
        Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> autenticar,
        LiberacaoToleranciaRepositorio repositorio)
    {
        _autenticar = autenticar ?? throw new ArgumentNullException(nameof(autenticar));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
    }

    public const string MotivoCredencialInvalida = "Credencial inválida.";
    public const string MotivoSemPermissao = "Usuário sem permissão de liberação de tolerância.";
    public const string MotivoLoginVazio = "Informe usuário e senha.";

    /// <summary>
    /// Autoriza (ou recusa) o estouro. Qualquer desfecho que nao seja autorizacao comprovada devolve
    /// recusa — nao ha caminho em que a duvida libere.
    /// </summary>
    public Task<ResultadoAutorizacaoTolerancia> AutorizarAsync(
        string login,
        string senha,
        ContextoEstouroTolerancia contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return AutorizarAsync(login, senha, [contexto], cancellationToken);
    }

    /// <summary>
    /// Autoriza (ou recusa) VARIOS estouros com UMA credencial — o caso da finalizacao, em que mais
    /// de um item do pedido pode estar abaixo do piso.
    /// <para>
    /// Uma digitacao de senha para a decisao inteira, e nao uma por item: a decisao do gerente e
    /// "este recebimento pode ser finalizado assim", nao N decisoes independentes. O log continua
    /// com UMA LINHA POR ITEM, com os numeros daquele item — resumir os itens numa linha so tiraria
    /// do log justamente o que ele existe para responder: qual material saiu do limite, e quanto.
    /// </para>
    /// </summary>
    public async Task<ResultadoAutorizacaoTolerancia> AutorizarAsync(
        string login,
        string senha,
        IReadOnlyList<ContextoEstouroTolerancia> contextos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextos);
        if (contextos.Count == 0)
        {
            throw new ArgumentException("Nada a autorizar.", nameof(contextos));
        }

        ContextoEstouroTolerancia contexto = contextos[0];
        string usuario = (login ?? string.Empty).Trim();

        if (usuario.Length == 0 || string.IsNullOrEmpty(senha))
        {
            // Sem login digitado nao ha o que registrar nem a quem atribuir: a tabela exige
            // login_autorizador nao vazio, e inventar um valor sujaria a auditoria.
            return ResultadoAutorizacaoTolerancia.Nao(MotivoLoginVazio);
        }

        ResultadoAutenticacao autenticacao;
        try
        {
            autenticacao = await _autenticar(usuario, senha, cancellationToken);
        }
        catch (Exception)
        {
            // Falha ao autenticar NAO libera. Nem tenta registrar: sem autenticacao nao se sabe
            // sequer se o login existe, e o banco provavelmente e a propria causa da excecao.
            return ResultadoAutorizacaoTolerancia.Nao(
                "Não foi possível validar a credencial agora. A pesagem continua bloqueada.");
        }

        if (!autenticacao.Sucesso || autenticacao.Sessao is null)
        {
            await RegistrarRecusaBestEffortAsync(contextos, usuario, null, MotivoCredencialInvalida, cancellationToken);
            return ResultadoAutorizacaoTolerancia.Nao(MotivoCredencialInvalida);
        }

        SessaoUsuarioAplicacao gerente = autenticacao.Sessao;

        if (!PossuiPermissaoDeLiberacao(gerente))
        {
            // Aqui a mensagem é ESPECÍFICA de propósito, ao contrário da credencial inválida: a
            // pessoa está em pé na balança e precisa saber que errou de usuário, não de senha.
            await RegistrarRecusaBestEffortAsync(
                contextos, usuario, gerente.IdUsuario, MotivoSemPermissao, cancellationToken);
            return ResultadoAutorizacaoTolerancia.Nao(MotivoSemPermissao);
        }

        try
        {
            await _repositorio.RegistrarLoteAsync(
                [.. contextos.Select(c => new LiberacaoToleranciaRecebimento
                {
                    Contexto = c,
                    Resultado = LiberacaoToleranciaRecebimento.Autorizada,
                    CodigoUsuarioAutorizador = gerente.IdUsuario,
                    LoginAutorizador = usuario,
                })],
                cancellationToken);
        }
        catch (Exception)
        {
            // FAIL-CLOSED: sem registro, sem liberacao.
            return ResultadoAutorizacaoTolerancia.Nao(
                "A liberação não pôde ser registrada e por isso não vale. A pesagem continua bloqueada.");
        }

        return ResultadoAutorizacaoTolerancia.Ok(usuario);
    }

    /// <summary>
    /// Tem a acao LIBERAR_TOLERANCIA? Le as permissoes do USUARIO AUTENTICADO AGORA, nunca as da
    /// sessao em curso — a sessao e do operador, que por definicao nao pode liberar.
    /// </summary>
    internal static bool PossuiPermissaoDeLiberacao(SessaoUsuarioAplicacao sessao)
        => sessao?.Permissoes is not null
           && sessao.Permissoes.Any(p => p.Corresponde(
               PermissoesSistema.Modulos.ProcessoProducao,
               PermissoesSistema.Rotinas.EntradaProduto,
               PermissoesSistema.Acoes.LiberarTolerancia));

    private async Task RegistrarRecusaBestEffortAsync(
        IReadOnlyList<ContextoEstouroTolerancia> contextos,
        string login,
        long? codigoUsuario,
        string motivo,
        CancellationToken cancellationToken)
    {
        try
        {
            // Lote tambem na recusa, para o log ler igual nos dois desfechos. Aqui a transacao e
            // conveniencia, nao regra: a recusa ja bloqueia por si.
            await _repositorio.RegistrarLoteAsync(
                [.. contextos.Select(c => new LiberacaoToleranciaRecebimento
                {
                    Contexto = c,
                    Resultado = LiberacaoToleranciaRecebimento.Recusada,
                    MotivoRecusa = motivo,
                    CodigoUsuarioAutorizador = codigoUsuario,
                    LoginAutorizador = login,
                })],
                cancellationToken);
        }
        catch (Exception erro)
        {
            // A recusa ja bloqueia; perder o registro dela nao deve mudar o desfecho. Mas perder o
            // SINAL importa — e tentativa repetida de credencial de gerente que nao aparece no log.
            System.Diagnostics.Trace.TraceWarning(
                $"[Tolerancia] Recusa nao registrada: {erro.GetType().Name}");
        }
    }
}
