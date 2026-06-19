using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class MapeamentoCampoEtiquetaController
{
    private readonly MapeamentoCampoEtiquetaServico _servico;

    public MapeamentoCampoEtiquetaController(MapeamentoCampoEtiquetaServico servico)
    {
        _servico = servico;
    }

    public Task<MapeamentoCampoEtiquetaCadastro?> ObterAtivoPorCampoAsync(long codigoCampoEtiqueta, CancellationToken cancellationToken = default)
        => _servico.ObterAtivoPorCampoAsync(codigoCampoEtiqueta, cancellationToken);

    public Task<MapeamentoCampoEtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ObterPorIdAsync(id, cancellationToken);

    public Task<ResultadoOperacao> SalvarAsync(MapeamentoCampoEtiquetaCadastro mapa, CancellationToken cancellationToken = default)
        => _servico.SalvarAsync(mapa, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ExcluirAsync(id, cancellationToken);
}
