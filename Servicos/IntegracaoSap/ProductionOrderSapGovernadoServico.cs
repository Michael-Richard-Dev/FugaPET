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
    /// <summary>GATE 118B: a leitura fresca tambem passa pela governanca de CONSULTA.</summary>
    public async Task<LeituraFrescaOrdem261> ConsultarEstadoFresco261Async(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Consulta,
            cancellationToken);

        return validacao.Sucesso
            ? await _servicoInterno.ConsultarEstadoFresco261Async(numeroOrdem, cancellationToken)
            : LeituraFrescaOrdem261.Indisponivel(numeroOrdem ?? string.Empty, validacao.Mensagem);
    }

    public async Task<IReadOnlyList<OrdemProducaoSap>> ListarOrdensRelevantesAsync(
        CancellationToken cancellationToken = default)
    {
        ResultadoOperacao validacao = await _estadoIntegracaoSapServico.ValidarAsync(
            OperacaoIntegracaoSap.Consulta,
            cancellationToken);

        return validacao.Sucesso
            ? await _servicoInterno.ListarOrdensRelevantesAsync(cancellationToken)
            : [];
    }
}

