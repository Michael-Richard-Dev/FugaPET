using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// Liberacao GERENCIAL de um estouro de tolerancia no RECEBIMENTO (incremento 070).
///
/// <para>O operador continua logado: o gerente chega, digita a PROPRIA credencial e autoriza aquela
/// pesagem. A sessao NAO e trocada — ninguem fica operando como gerente depois.</para>
///
/// <para><b>A autenticacao reusa <see cref="AutenticacaoServico"/></b> em vez de comparar senha aqui.
/// E o que garante, de graca, as mesmas regras do login: BCrypt via SenhaServico, usuario inativo
/// barrado, usuario bloqueado barrado.</para>
///
/// <para><b>A POLITICA mora em <see cref="NucleoAutorizacaoTolerancia"/></b>, compartilhada com a
/// liberacao da producao (071): permissao e nao perfil, fail-closed no registro, best-effort na
/// recusa. Esta classe so sabe ONDE gravar — a tabela do Recebimento.</para>
/// </summary>
public sealed class AutorizacaoToleranciaServico
{
    private readonly Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> _autenticar;
    private readonly LiberacaoToleranciaRepositorio _repositorio;

    public AutorizacaoToleranciaServico(
        AutenticacaoServico autenticacao,
        LiberacaoToleranciaRepositorio repositorio)
        : this((login, senha, ct) => autenticacao.AutenticarAsync(login, senha, ct), repositorio)
    {
    }

    internal AutorizacaoToleranciaServico(
        Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> autenticar,
        LiberacaoToleranciaRepositorio repositorio)
    {
        _autenticar = autenticar ?? throw new ArgumentNullException(nameof(autenticar));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
    }

    public const string MotivoCredencialInvalida = NucleoAutorizacaoTolerancia.MotivoCredencialInvalida;
    public const string MotivoSemPermissao = NucleoAutorizacaoTolerancia.MotivoSemPermissao;
    public const string MotivoLoginVazio = NucleoAutorizacaoTolerancia.MotivoLoginVazio;

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
    /// "este recebimento pode ser fechado assim", nao N decisoes independentes. O log continua
    /// com UMA LINHA POR ITEM, com os numeros daquele item — resumir os itens numa linha so tiraria
    /// do log justamente o que ele existe para responder: qual material saiu do limite, e quanto.
    /// </para>
    /// </summary>
    public Task<ResultadoAutorizacaoTolerancia> AutorizarAsync(
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

        return NucleoAutorizacaoTolerancia.DecidirAsync(
            login,
            senha,
            _autenticar,
            (codigoUsuario, usuario, ct) => _repositorio.RegistrarLoteAsync(
                [.. contextos.Select(c => new LiberacaoToleranciaRecebimento
                {
                    Contexto = c,
                    Resultado = LiberacaoToleranciaRecebimento.Autorizada,
                    CodigoUsuarioAutorizador = codigoUsuario,
                    LoginAutorizador = usuario,
                })],
                ct),
            (codigoUsuario, usuario, motivo, ct) => _repositorio.RegistrarLoteAsync(
                [.. contextos.Select(c => new LiberacaoToleranciaRecebimento
                {
                    Contexto = c,
                    Resultado = LiberacaoToleranciaRecebimento.Recusada,
                    MotivoRecusa = motivo,
                    CodigoUsuarioAutorizador = codigoUsuario,
                    LoginAutorizador = usuario,
                })],
                ct),
            cancellationToken);
    }

    internal static bool PossuiPermissaoDeLiberacao(SessaoUsuarioAplicacao sessao)
        => NucleoAutorizacaoTolerancia.PossuiPermissaoDeLiberacao(sessao);
}
