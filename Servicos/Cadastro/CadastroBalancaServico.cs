using FugaPET_Dev.Modelo.Cadastro;

namespace FugaPET_Dev.Servicos.Cadastro;

[Obsolete("Use BalancaServico diretamente. Wrapper legado mantido apenas por compatibilidade.")]
public sealed class CadastroBalancaServico
{
    private readonly BalancaServico _balancaServico;

    public CadastroBalancaServico(BalancaServico balancaServico)
    {
        _balancaServico = balancaServico;
    }

    public Task<IReadOnlyList<BalancaCadastro>> ListarAsync(CancellationToken cancellationToken = default)
        => _balancaServico.ListarAsync(cancellationToken);

    public Task<ResultadoOperacao> InserirAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
        => _balancaServico.InserirAsync(balanca, cancellationToken);

    public Task<ResultadoOperacao> AtualizarAsync(BalancaCadastro balanca, CancellationToken cancellationToken = default)
        => _balancaServico.AtualizarAsync(balanca, cancellationToken);

    public Task<ResultadoOperacao> ExcluirAsync(long codigoBalanca, CancellationToken cancellationToken = default)
        => _balancaServico.ExcluirAsync(codigoBalanca, cancellationToken);

    public Task<ResultadoOperacao> ReativarAsync(long codigoBalanca, CancellationToken cancellationToken = default)
        => _balancaServico.ReativarAsync(codigoBalanca, cancellationToken);
}
