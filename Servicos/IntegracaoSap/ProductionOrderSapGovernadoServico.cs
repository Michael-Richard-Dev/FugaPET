using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Aplica a governanca central (ambiente + integracao ativa + configuracao + permissao de CONSULTA)
/// antes de delegar a consulta da Ordem de Producao ao servico real.
/// </summary>
internal sealed class ProductionOrderSapGovernadoServico : IProductionOrderSapServico
{
    private readonly IProductionOrderSapServico _servicoInterno;
    private readonly EstadoIntegracaoSapServico _estadoIntegracaoSapServico;

    public ProductionOrderSapGovernadoServico(
        IProductionOrderSapServico servicoInterno,
        EstadoIntegracaoSapServico estadoIntegracaoSapServico)
    {
        _servicoInterno = servicoInterno;
        _estadoIntegracaoSapServico = estadoIntegracaoSapServico;
    }

    public bool EhSimulado => false;
    public bool Configurado => _servicoInterno.Configurado;

    public async Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Consulta,
            cancellationToken);

        return validacao.Sucesso
            ? await _servicoInterno.ConsultarOrdemAsync(numeroOrdem, cancellationToken)
            : ResultadoConsultaOrdemProducaoSap.Indisponivel(validacao.Mensagem);
    }
}
