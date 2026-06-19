using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class CampoEtiquetaController
{
    private readonly CampoEtiquetaServico _servico;

    public CampoEtiquetaController(CampoEtiquetaServico servico)
    {
        _servico = servico;
    }

    public Task<IReadOnlyList<CampoEtiquetaCadastro>> ListarPorEtiquetaAsync(long codigoEtiqueta, CancellationToken cancellationToken = default)
        => _servico.ListarPorEtiquetaAsync(codigoEtiqueta, cancellationToken);

    public Task<CampoEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ObterPorIdAsync(id, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
        => _servico.InserirAsync(campo, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(CampoEtiquetaCadastro campo, CancellationToken cancellationToken = default)
        => _servico.AtualizarAsync(campo, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ReativarAsync(id, cancellationToken);
}
