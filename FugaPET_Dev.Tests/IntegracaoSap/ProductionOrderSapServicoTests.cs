using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Servico de Ordem de Producao: a mensagem SANITIZADA de uma falha controlada
/// (<see cref="ProductionOrderSapConsultaException"/>) deve chegar ao resultado, sem credenciais.
/// </summary>
public sealed class ProductionOrderSapServicoTests
{
    [Fact]
    public async Task FalhaControlada_DevePreservarMensagemSanitizadaSemCredenciais()
    {
        const string sanitizada =
            "Consulta OP falhou na etapa GET_FALLBACK, HTTP 400. Verifique a OP ou o serviço SAP.";

        ProductionOrderSapServico servico = new(
            new ConfiguracaoSap(),
            (_, _) => throw new ProductionOrderSapConsultaException("GET_FALLBACK", 400, sanitizada));

        ResultadoConsultaOrdemProducaoSap resultado = await servico.ConsultarOrdemAsync("1000009");

        // Teste 1: a mensagem sanitizada da excecao chega ao MensagemSanitizada do resultado.
        Assert.Equal(CenarioConsultaOrdemProducaoSap.Indisponivel, resultado.Cenario);
        Assert.Equal(sanitizada, resultado.MensagemSanitizada);
        Assert.Equal(400, resultado.StatusHttp);

        // Teste 4: a mensagem nunca expoe segredo.
        foreach (string segredo in new[] { "Authorization", "senha", "password", "cookie", "token", "Basic " })
        {
            Assert.DoesNotContain(segredo, resultado.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
        }
    }
}
