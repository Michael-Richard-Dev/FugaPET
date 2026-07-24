using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class EtiquetaController
{
    private readonly EtiquetaServico _etiquetaServico;

    public EtiquetaController(EtiquetaServico etiquetaServico)
    {
        _etiquetaServico = etiquetaServico;
    }

    public Task<IReadOnlyList<EtiquetaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _etiquetaServico.ListarAsync(cancellationToken);

    public Task<EtiquetaCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _etiquetaServico.ObterPorIdAsync(id, cancellationToken);

    public Task<bool> ExisteCodigoInternoAsync(string codigoInterno, long? ignorarCodigo = null, CancellationToken cancellationToken = default)
        => _etiquetaServico.ExisteCodigoInternoAsync(codigoInterno, ignorarCodigo, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
        => _etiquetaServico.InserirAsync(etiqueta, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(EtiquetaCadastro etiqueta, CancellationToken cancellationToken = default)
        => _etiquetaServico.AtualizarAsync(etiqueta, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _etiquetaServico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _etiquetaServico.ReativarAsync(id, cancellationToken);

    public Task<ResumoDependenciasEtiqueta> ObterResumoDependenciasAtivasAsync(long id, CancellationToken cancellationToken = default)
        => _etiquetaServico.ObterResumoDependenciasAtivasAsync(id, cancellationToken);
}
