using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

/// <summary>Fachada da tela para o vinculo produto × etiqueta × processo.</summary>
public sealed class VinculoProdutoEtiquetaController
{
    private readonly VinculoProdutoEtiquetaServico _servico;

    public VinculoProdutoEtiquetaController(VinculoProdutoEtiquetaServico servico)
        => _servico = servico ?? throw new ArgumentNullException(nameof(servico));

    /// <summary>Le do catalogo LOCAL. Nao toca no SAP.</summary>
    public Task<ResultadoListagemVinculo> ListarAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.ListarAsync(centro, cancellationToken);

    /// <summary>Traz do SAP o que o catalogo local ainda nao conhece.</summary>
    public Task<Modelo.Cadastro.ResumoSincronizacaoCatalogo> SincronizarCatalogoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.SincronizarCatalogoAsync(centro, cancellationToken);

    public Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.ObterUltimaSincronizacaoAsync(centro, cancellationToken);

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
