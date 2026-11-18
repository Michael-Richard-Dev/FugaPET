using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

/// <summary>Fachada da tela para o vinculo produto × etiqueta × processo.</summary>
public sealed class VinculoProdutoEtiquetaController
{
    private readonly VinculoProdutoEtiquetaServico _servico;

    public VinculoProdutoEtiquetaController(VinculoProdutoEtiquetaServico servico)
        => _servico = servico ?? throw new ArgumentNullException(nameof(servico));

    public Task<ResultadoListagemVinculo> ListarAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.ListarAsync(centro, cancellationToken);

    public Task<IReadOnlyList<EtiquetaCadastro>> ListarEtiquetasDisponiveisAsync(
        CancellationToken cancellationToken = default)
        => _servico.ListarEtiquetasDisponiveisAsync(cancellationToken);

    public Task<ResultadoOperacao> SalvarAsync(
        VinculoProdutoEtiqueta vinculo, CancellationToken cancellationToken = default)
        => _servico.SalvarAsync(vinculo, cancellationToken);

    public Task<ResultadoOperacao> RemoverAsync(
        string centro, string produto, CancellationToken cancellationToken = default)
        => _servico.RemoverAsync(centro, produto, cancellationToken);
}
