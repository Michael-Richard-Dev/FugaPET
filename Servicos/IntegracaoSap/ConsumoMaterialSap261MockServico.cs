using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Demonstracao: NAO envia consumo 261 ao SAP. Em ambiente demonstrativo / banco desabilitado o
/// envio fica bloqueado (sem CSRF/POST). Mantem a Tela de Consumo sem efeito SAP real.
/// </summary>
internal sealed class ConsumoMaterialSap261MockServico : IConsumoMaterialSap261Servico
{
    private const string MensagemIndisponivel = "Envio de consumo 261 indisponível em ambiente demonstrativo.";

    public bool EhSimulado => true;
    public bool Configurado => false;

    public ResultadoEnvioConsumoSap261 ValidarProntoParaEnvio()
        => ResultadoEnvioConsumoSap261.Falha(MensagemIndisponivel);

    public Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(
        ConsumoMaterialSap261Request requisicao,
        string chaveNegocio,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ValidarProntoParaEnvio());
}
