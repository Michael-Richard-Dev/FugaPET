using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Servicos.Cadastro;

public sealed class CadastroSetorServico
{
    private readonly SetorServico _setorServico;

    public CadastroSetorServico(SetorServico setorServico)
    {
        _setorServico = setorServico;
    }

    public Task<IReadOnlyList<SetorCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _setorServico.ListarAsync(cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
        => _setorServico.InserirAsync(setor, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(SetorCadastro setor, CancellationToken cancellationToken = default)
        => _setorServico.AtualizarAsync(setor, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long codigoSetor, CancellationToken cancellationToken = default)
        => _setorServico.ExcluirAsync(codigoSetor, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long codigoSetor, CancellationToken cancellationToken = default)
        => _setorServico.ReativarAsync(codigoSetor, cancellationToken);
}
