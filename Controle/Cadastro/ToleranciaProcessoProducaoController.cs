using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Controle.Cadastro;

/// <summary>Fachada da tela para a tolerância por produto nos processos de produção (071).</summary>
public sealed class ToleranciaProcessoProducaoController
{
    private readonly ToleranciaProcessoProducaoServico _servico;

    public ToleranciaProcessoProducaoController(ToleranciaProcessoProducaoServico servico)
        => _servico = servico ?? throw new ArgumentNullException(nameof(servico));

    /// <summary>Lê do catálogo LOCAL. Não toca no SAP.</summary>
    public Task<ResultadoListagemToleranciaProcesso> ListarAsync(
        string centro, string processo, CancellationToken cancellationToken = default)
        => _servico.ListarAsync(centro, processo, cancellationToken);

    /// <summary>Traz do SAP o que o catálogo local ainda não conhece.</summary>
    public Task<ResumoSincronizacaoCatalogo> SincronizarCatalogoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.SincronizarCatalogoAsync(centro, cancellationToken);

    public Task<DateTime?> ObterUltimaSincronizacaoAsync(
        string centro, CancellationToken cancellationToken = default)
        => _servico.ObterUltimaSincronizacaoAsync(centro, cancellationToken);

    public Task<ResultadoOperacao> SalvarAsync(
        ToleranciaProcessoProducao tolerancia, CancellationToken cancellationToken = default)
        => _servico.SalvarAsync(tolerancia, cancellationToken);

    public Task<ResultadoOperacao> RemoverAsync(
        string centro, string processo, string material, CancellationToken cancellationToken = default)
        => _servico.RemoverAsync(centro, processo, material, cancellationToken);
}
