using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

public sealed class BalancaController
{
    private readonly BalancaServico _balancaServico;

    public BalancaController(BalancaServico balancaServico)
    {
        _balancaServico = balancaServico;
    }

    public Task<IReadOnlyList<BalancaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _balancaServico.ListarAsync(cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
        => _balancaServico.InserirAsync(balanca, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
        => _balancaServico.AtualizarAsync(balanca, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long id, CancellationToken cancellationToken = default)
        => _balancaServico.ExcluirAsync(id, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long id, CancellationToken cancellationToken = default)
        => _balancaServico.ReativarAsync(id, cancellationToken);
}
