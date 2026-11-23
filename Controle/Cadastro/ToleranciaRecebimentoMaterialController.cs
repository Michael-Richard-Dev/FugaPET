using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

/// <summary>Fachada da tela para a tolerancia de recebimento por material.</summary>
public sealed class ToleranciaRecebimentoMaterialController
{
    private readonly ToleranciaRecebimentoMaterialServico _servico;

    public ToleranciaRecebimentoMaterialController(ToleranciaRecebimentoMaterialServico servico)
        => _servico = servico ?? throw new ArgumentNullException(nameof(servico));

    /// <summary>Le do catalogo LOCAL. Nao toca no SAP.</summary>
    public Task<ResultadoListagemTolerancia> ListarAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.ListarAsync(centro, cancellationToken);

    /// <summary>Traz do SAP o que o catalogo local ainda nao conhece.</summary>
    public Task<ResumoSincronizacaoCatalogo> SincronizarCatalogoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.SincronizarCatalogoAsync(centro, cancellationToken);

    public Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.ObterUltimaSincronizacaoAsync(centro, cancellationToken);

    public Task<ResultadoOperacao> SalvarAsync(
        ToleranciaRecebimentoMaterial tolerancia, CancellationToken cancellationToken = default)
        => _servico.SalvarAsync(tolerancia, cancellationToken);

    public Task<ResultadoOperacao> RemoverAsync(
        string centro, string material, CancellationToken cancellationToken = default)
        => _servico.RemoverAsync(centro, material, cancellationToken);
}
