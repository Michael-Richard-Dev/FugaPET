using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class TaraController
{
    private readonly TaraServico _taraServico;

    public TaraController(TaraServico taraServico)
    {
        _taraServico = taraServico;
    }

    public Task<IReadOnlyList<TaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _taraServico.ListarAsync(cancellationToken);

    public Task<IReadOnlyList<TaraCadastro>> ListarAtivasPorSetorAsync(long codigoSetor, CancellationToken cancellationToken = default)
        => _taraServico.ListarAtivasPorSetorAsync(codigoSetor, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
        => _taraServico.InserirAsync(tara, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(TaraCadastro tara, CancellationToken cancellationToken = default)
        => _taraServico.AtualizarAsync(tara, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _taraServico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _taraServico.ReativarAsync(id, cancellationToken);
}
