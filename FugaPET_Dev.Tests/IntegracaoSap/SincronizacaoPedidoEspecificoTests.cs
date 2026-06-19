using System.Net;
using System.Text;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class SincronizacaoPedidoEspecificoTests
{
    [Fact]
    public async Task PedidoInexistente_DeveRetornarFalhaAmigavel()
    {
        using HttpClient http = new(new RespostaHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        SincronizacaoPedidoCompraSapServico servico = CriarServico(http);

        var resultado = await servico.SincronizarPedidoAsync("4500000999");

        Assert.False(resultado.Sucesso);
        Assert.Contains("nao encontrado", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PedidoSemItens_DeveRetornarFalhaAmigavel()
    {
        const string json = """
            {
              "PurchaseOrder": "4500000011",
              "IncotermsClassification": "CIF",
              "IncotermsTransferLocation": "COTRISAL",
              "IncotermsLocation1": "COTRISAL",
              "_PurchaseOrderItem": []
            }
            """;
        using HttpClient http = new(new RespostaHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        SincronizacaoPedidoCompraSapServico servico = CriarServico(http);

        var resultado = await servico.SincronizarPedidoAsync("4500000011");

        Assert.False(resultado.Sucesso);
        Assert.Contains("sem itens", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PedidoSemIncoterms_NaoDeveSerBloqueadoPorPadrao()
    {
        // H6: Incoterms nao e mais regra de existencia/consulta. Pedido sem Incoterms deve
        // PASSAR pela checagem de Incoterms e cair na proxima validacao (aqui, "sem itens").
        const string json = """
            {
              "PurchaseOrder": "4500000012",
              "_PurchaseOrderItem": []
            }
            """;
        using HttpClient http = new(new RespostaHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        SincronizacaoPedidoCompraSapServico servico = CriarServico(http);

        var resultado = await servico.SincronizarPedidoAsync("4500000012");

        Assert.False(resultado.Sucesso);
        Assert.Contains("sem itens", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("incoterms", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PedidoSemIncoterms_DeveSerBloqueado_QuandoRegraConfiguradaLigada()
    {
        const string json = """
            {
              "PurchaseOrder": "4500000013",
              "_PurchaseOrderItem": []
            }
            """;
        using HttpClient http = new(new RespostaHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        }));
        SincronizacaoPedidoCompraSapServico servico = CriarServico(http);

        Environment.SetEnvironmentVariable(SincronizacaoPedidoCompraSapServico.VariavelExigirIncoterms, "true");
        try
        {
            var resultado = await servico.SincronizarPedidoAsync("4500000013");

            Assert.False(resultado.Sucesso);
            Assert.Contains("incoterms", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SincronizacaoPedidoCompraSapServico.VariavelExigirIncoterms, null);
        }
    }

    [Fact]
    public async Task Timeout_DeveRetornarFalhaAmigavel()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler())
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        SincronizacaoPedidoCompraSapServico servico = CriarServico(http);

        var resultado = await servico.SincronizarPedidoAsync("4500000010");

        Assert.False(resultado.Sucesso);
        Assert.Contains("tempo limite", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelamentoDoChamador_DeveSerPropagado()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        SincronizacaoPedidoCompraSapServico servico = CriarServico(http);
        using CancellationTokenSource cancelamento = new();
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => servico.SincronizarPedidoAsync("4500000010", cancelamento.Token));
    }

    private static SincronizacaoPedidoCompraSapServico CriarServico(HttpClient http)
    {
        ConfiguracaoSap configuracao = new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            HostsPermitidos = ["sap.exemplo.local"]
        };
        PedidoCompraSapApiClient cliente = new(configuracao, http);
        return new SincronizacaoPedidoCompraSapServico(configuracao, null!, cliente);
    }

    private sealed class RespostaHandler(HttpResponseMessage resposta) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(resposta);
    }

    private sealed class EsperaCancelamentoHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
