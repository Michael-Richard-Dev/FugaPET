using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class PermissaoController
{
    private readonly PermissaoServico _permissaoServico;

    public PermissaoController(PermissaoServico permissaoServico)
    {
        _permissaoServico = permissaoServico;
    }

    public Task<IReadOnlyList<PermissaoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _permissaoServico.ListarAsync(cancellationToken);

    public Task<PermissaoCadastro?> ObterPorIdAsync(long idPermissao, CancellationToken cancellationToken = default)
        => _permissaoServico.ObterPorIdAsync(idPermissao, cancellationToken);

    public Task<IReadOnlyList<long>> ListarCodigosPermissaoPorPerfilAsync(
        long codigoPerfil,
        CancellationToken cancellationToken = default)
        => _permissaoServico.ListarCodigosPermissaoPorPerfilAsync(codigoPerfil, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(PermissaoCadastro permissao, CancellationToken cancellationToken = default)
        => _permissaoServico.InserirAsync(permissao, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(PermissaoCadastro permissao, CancellationToken cancellationToken = default)
        => _permissaoServico.AtualizarAsync(permissao, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long idPermissao, CancellationToken cancellationToken = default)
        => _permissaoServico.ExcluirAsync(idPermissao, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long idPermissao, CancellationToken cancellationToken = default)
        => _permissaoServico.ReativarAsync(idPermissao, cancellationToken);

    public Task<ResultadoOperacao> SincronizarPermissoesDoPerfilAsync(
        long codigoPerfil,
        IReadOnlyCollection<long> permissoesSelecionadas,
        CancellationToken cancellationToken = default)
        => _permissaoServico.SincronizarPermissoesDoPerfilAsync(codigoPerfil, permissoesSelecionadas, cancellationToken);
}
