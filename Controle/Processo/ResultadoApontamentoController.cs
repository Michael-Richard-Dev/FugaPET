using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Controle.Processo;

/// <summary>
/// Controller da tela Resultado do Apontamento. A View não acessa banco nem SAP diretamente.
/// </summary>
public sealed class ResultadoApontamentoController
{
    private readonly ResultadoApontamentoServico _servico;

    public ResultadoApontamentoController()
        : this(new ResultadoApontamentoServico())
    {
    }

    internal ResultadoApontamentoController(ResultadoApontamentoServico servico)
    {
        _servico = servico ?? throw new ArgumentNullException(nameof(servico));
    }

    public string ObterIdentificacaoBanco() => _servico.ObterIdentificacaoBanco();

    public Task<IReadOnlyList<ResultadoApontamentoItem>> ListarDefinicoesAsync(
        ContextoApontamentoProcesso contexto,
        CancellationToken cancellationToken = default)
        => _servico.ListarDefinicoesAsync(contexto, cancellationToken);

    public Task<ResultadoOperacao> RegistrarAsync(
        ContextoApontamentoProcesso contexto,
        IReadOnlyList<ResultadoApontamentoItem> itens,
        CancellationToken cancellationToken = default)
        => _servico.RegistrarAsync(contexto, itens, cancellationToken);
}
