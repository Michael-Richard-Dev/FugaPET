using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class TipoTaraController
{
    private readonly TipoTaraServico _servico;

    public TipoTaraController(TipoTaraServico servico)
    {
        _servico = servico;
    }

    public Task<IReadOnlyList<TipoTaraCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _servico.ListarAsync(cancellationToken);

    // Tarefa Tipo de Tara (Ajuste 4): referência = contagem de taras ativas por tipo.
    public Task<IReadOnlyDictionary<long, int>> ContarTarasAtivasPorTipoAsync(CancellationToken cancellationToken = default)
        => _servico.ContarTarasAtivasPorTipoAsync(cancellationToken);

    // Tarefa Tipo de Tara (Ajuste 6): diagnóstico de nomes duplicados (somente leitura).
    public Task<IReadOnlyList<DuplicadoTipoTara>> ListarNomesDuplicadosAsync(CancellationToken cancellationToken = default)
        => _servico.ListarNomesDuplicadosAsync(cancellationToken);

    public Task<TipoTaraCadastro?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ObterPorIdAsync(id, cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
        => _servico.InserirAsync(tipo, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(TipoTaraCadastro tipo, CancellationToken cancellationToken = default)
        => _servico.AtualizarAsync(tipo, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _servico.ReativarAsync(id, cancellationToken);
}
