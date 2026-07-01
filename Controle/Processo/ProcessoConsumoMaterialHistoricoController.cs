using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Controle.Processo;

public sealed class ProcessoConsumoMaterialHistoricoController
{
    private readonly ConsumoMaterialConsultaServico _consultaServico;

    public ProcessoConsumoMaterialHistoricoController()
        : this(new ConsumoMaterialConsultaServico())
    {
    }

    internal ProcessoConsumoMaterialHistoricoController(ConsumoMaterialConsultaServico consultaServico)
    {
        _consultaServico = consultaServico;
    }

    public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
        ConsultaConsumoMaterialFiltro filtro,
        CancellationToken cancellationToken = default)
        => _consultaServico.ConsultarLancamentosAsync(filtro, cancellationToken);

    public Task<DetalheConsumoMaterialLancamento?> ObterDetalheCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _consultaServico.ObterDetalheCompletoAsync(codigoLancamento, cancellationToken);

    public Task<ResultadoPreviewConsumoSap261> GerarPreviewSap261Async(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
        => _consultaServico.GerarPreviewSap261Async(codigoLancamento, cancellationToken);
}
