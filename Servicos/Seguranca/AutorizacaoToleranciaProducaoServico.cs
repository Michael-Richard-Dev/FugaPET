using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Servicos.Seguranca;

/// <summary>
/// Liberação GERENCIAL de um estouro de tolerância no CONSUMO e no SEMI-ACABADO (incremento 071).
///
/// <para>Gêmeo do <see cref="AutorizacaoToleranciaServico"/> do Recebimento, e de propósito: a
/// POLÍTICA é a mesma e vive num lugar só, em <see cref="NucleoAutorizacaoTolerancia"/>. O que muda
/// aqui é apenas ONDE se grava — a tabela da produção, que identifica o caso por ordem e operação
/// em vez de pedido e item.</para>
///
/// <para><b>A permissão é a MESMA</b> (LIBERAR_TOLERANCIA, do 070), por decisão do usuário em
/// 24/11/2026: quem pode liberar é o mesmo gerente, e uma segunda permissão só criaria mais um lugar
/// para esquecer de conceder.</para>
/// </summary>
public sealed class AutorizacaoToleranciaProducaoServico
{
    private readonly Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> _autenticar;
    private readonly LiberacaoToleranciaProcessoRepositorio _repositorio;

    public AutorizacaoToleranciaProducaoServico(
        AutenticacaoServico autenticacao,
        LiberacaoToleranciaProcessoRepositorio repositorio)
        : this((login, senha, ct) => autenticacao.AutenticarAsync(login, senha, ct), repositorio)
    {
    }

    internal AutorizacaoToleranciaProducaoServico(
        Func<string, string, CancellationToken, Task<ResultadoAutenticacao>> autenticar,
        LiberacaoToleranciaProcessoRepositorio repositorio)
    {
        _autenticar = autenticar ?? throw new ArgumentNullException(nameof(autenticar));
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
    }

    public const string MotivoCredencialInvalida = NucleoAutorizacaoTolerancia.MotivoCredencialInvalida;
    public const string MotivoSemPermissao = NucleoAutorizacaoTolerancia.MotivoSemPermissao;
    public const string MotivoLoginVazio = NucleoAutorizacaoTolerancia.MotivoLoginVazio;

    public Task<ResultadoAutorizacaoTolerancia> AutorizarAsync(
        string login,
        string senha,
        ContextoEstouroProducao contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return AutorizarAsync(login, senha, [contexto], cancellationToken);
    }

    /// <summary>
    /// Autoriza (ou recusa) vários estouros com UMA credencial — o caso da conclusão do Consumo, em
    /// que mais de um componente da ordem pode estar fora do limite. O log continua com UMA LINHA
    /// POR COMPONENTE, com os números daquele componente.
    /// </summary>
    public Task<ResultadoAutorizacaoTolerancia> AutorizarAsync(
        string login,
        string senha,
        IReadOnlyList<ContextoEstouroProducao> contextos,
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
                [.. contextos.Select(c => new LiberacaoToleranciaProcesso
                {
                    Contexto = c,
                    Resultado = LiberacaoToleranciaProcesso.Autorizada,
                    CodigoUsuarioAutorizador = codigoUsuario,
                    LoginAutorizador = usuario,
                })],
                ct),
            (codigoUsuario, usuario, motivo, ct) => _repositorio.RegistrarLoteAsync(
                [.. contextos.Select(c => new LiberacaoToleranciaProcesso
                {
                    Contexto = c,
                    Resultado = LiberacaoToleranciaProcesso.Recusada,
                    MotivoRecusa = motivo,
                    CodigoUsuarioAutorizador = codigoUsuario,
                    LoginAutorizador = usuario,
                })],
                ct),
            cancellationToken);
    }
}
