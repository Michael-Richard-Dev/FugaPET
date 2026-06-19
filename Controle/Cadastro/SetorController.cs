using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class SetorController
{
    private readonly SetorServico _setorServico;

    public SetorController(SetorServico setorServico)
    {
        _setorServico = setorServico;
    }

    public Task<IReadOnlyList<SetorCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _setorServico.ListarAsync(cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
        => _setorServico.InserirAsync(setor, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
        => _setorServico.AtualizarAsync(setor, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long idSetor, CancellationToken cancellationToken = default)
        => _setorServico.ExcluirAsync(idSetor, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long idSetor, CancellationToken cancellationToken = default)
        => _setorServico.ReativarAsync(idSetor, cancellationToken);
}
