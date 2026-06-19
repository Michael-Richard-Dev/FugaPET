using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class PerfilAcessoController
{
    private readonly PerfilAcessoServico _perfilAcessoServico;

    public PerfilAcessoController(PerfilAcessoServico perfilAcessoServico)
    {
        _perfilAcessoServico = perfilAcessoServico;
    }

    public Task<IReadOnlyList<PerfilAcessoCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _perfilAcessoServico.ListarAsync(cancellationToken);

    public Task<PerfilAcessoCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _perfilAcessoServico.ObterPorIdAsync(id, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(PerfilAcessoCadastro perfil, CancellationToken cancellationToken = default)
        => _perfilAcessoServico.InserirAsync(perfil, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(PerfilAcessoCadastro perfil, CancellationToken cancellationToken = default)
        => _perfilAcessoServico.AtualizarAsync(perfil, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long idPerfilAcesso, CancellationToken cancellationToken = default)
        => _perfilAcessoServico.ExcluirAsync(idPerfilAcesso, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long idPerfilAcesso, CancellationToken cancellationToken = default)
        => _perfilAcessoServico.ReativarAsync(idPerfilAcesso, cancellationToken);
}
