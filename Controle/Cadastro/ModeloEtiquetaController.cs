using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class ModeloEtiquetaController
{
    private readonly ModeloEtiquetaServico _servico;

    public ModeloEtiquetaController(ModeloEtiquetaServico servico)
    {
        _servico = servico;
    }

    public Task<IReadOnlyList<ModeloEtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _servico.ListarAsync(cancellationToken);

    public Task<ModeloEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ObterPorIdAsync(id, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
        => _servico.InserirAsync(modelo, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(ModeloEtiquetaCadastro modelo, CancellationToken cancellationToken = default)
        => _servico.AtualizarAsync(modelo, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ReativarAsync(id, cancellationToken);
}
