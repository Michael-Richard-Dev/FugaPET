using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Seguranca;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>Persistência do cancelamento lógico de pesagem (transação SERIALIZABLE, guard SAP, cascata de vazio).</summary>
public interface IExclusaoPesagemRepositorio
{
    Task<ResultadoExclusaoPesagemRepositorio> ExcluirPesagemLocalAsync(
        long codigoPesagem,
        long codigoUsuario,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Regra de domínio de EXCLUIR PESAGEM (cancelamento lógico local): valida a permissão EXATA
/// PROCESSO_PRODUCAO/ENTRADA_PRODUTO/EXCLUIR_PESAGEM (SEM fallback legado), resolve o usuário logado e delega
/// a transação ao repositório. ZERO SAP, ZERO DELETE físico. A decisão final é revalidada dentro da transação.
/// </summary>
public sealed class ExclusaoPesagemServico
{
    private readonly IExclusaoPesagemRepositorio _repositorio;
    private readonly Func<bool> _usuarioAutorizado;
    private readonly Func<long?> _codigoUsuarioLogado;

    public ExclusaoPesagemServico(IExclusaoPesagemRepositorio repositorio)
        : this(repositorio, usuarioAutorizado: null, codigoUsuarioLogado: null)
    {
    }

    internal ExclusaoPesagemServico(
        IExclusaoPesagemRepositorio repositorio,
        Func<bool>? usuarioAutorizado,
        Func<long?>? codigoUsuarioLogado)
    {
        _repositorio = repositorio ?? throw new ArgumentNullException(nameof(repositorio));
        _usuarioAutorizado = usuarioAutorizado ?? PossuiPermissaoExcluirPesagem;
        _codigoUsuarioLogado = codigoUsuarioLogado
            ?? (() => EstadoSessaoUsuarioAtual.SessaoAtual?.IdUsuario);
    }

    public async Task<ResultadoExclusaoPesagem> ExcluirAsync(
        long codigoPesagem,
        CancellationToken cancellationToken = default)
    {
        if (!_usuarioAutorizado())
        {
            return ResultadoExclusaoPesagem.SemPermissao();
        }

        if (_codigoUsuarioLogado() is not long codigoUsuario || codigoUsuario <= 0)
        {
            return ResultadoExclusaoPesagem.SemPermissao();
        }

        if (codigoPesagem <= 0)
        {
            return ResultadoExclusaoPesagem.Inexistente();
        }

        ResultadoExclusaoPesagemRepositorio bruto;
        try
        {
            bruto = await _repositorio.ExcluirPesagemLocalAsync(codigoPesagem, codigoUsuario, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ResultadoExclusaoPesagem.Falha();
        }

        return bruto.Cenario switch
        {
            CenarioExclusaoPesagem.Excluida => ResultadoExclusaoPesagem.Ok(bruto.ArvoreVazia),
            CenarioExclusaoPesagem.PesagemInexistente => ResultadoExclusaoPesagem.Inexistente(),
            CenarioExclusaoPesagem.JaCancelada => ResultadoExclusaoPesagem.JaCancelada(),
            CenarioExclusaoPesagem.EstadoMudou => ResultadoExclusaoPesagem.EstadoMudou(),
            CenarioExclusaoPesagem.BloqueadoSap => ResultadoExclusaoPesagem.BloqueadoSap(),
            _ => ResultadoExclusaoPesagem.Falha()
        };
    }

    // Permissão EXATA, SEM fallback legado (não usa AutorizacaoEntradaProdutoServico, que tem equivalência legada).
    private static bool PossuiPermissaoExcluirPesagem()
        => AutorizacaoServico.PossuiPermissao(
            PermissoesSistema.Modulos.ProcessoProducao,
            PermissoesSistema.Rotinas.EntradaProduto,
            PermissoesSistema.Acoes.ExcluirPesagem);
}
