using System.Net;
using System.Net.Security;
using System.Text;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class PedidoCompraSapHttpSegurancaTests
{
    [Fact]
    public void CriarCliente_ComHttpsEHostPermitido_DeveAceitar()
    {
        using HttpClient http = new(new RespostaHandler("{}"));

        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        Assert.NotNull(cliente);
    }

    [Fact]
    public void CriarCliente_ComHttp_DeveRejeitar()
    {
        ConfiguracaoSap configuracao = CriarConfiguracao(
            baseUrl: "http://sap.exemplo.local/odata");
        using HttpClient http = new(new RespostaHandler("{}"));

        InvalidOperationException erro = Assert.Throws<InvalidOperationException>(
            () => new PedidoCompraSapApiClient(configuracao, http));

        Assert.Contains("HTTPS", erro.Message);
    }

    [Fact]
    public void CertificadoInvalido_DeveSerRejeitado()
    {
        Assert.True(FabricaHttpClientSap.CertificadoValido(SslPolicyErrors.None));
        Assert.False(FabricaHttpClientSap.CertificadoValido(SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(FabricaHttpClientSap.CertificadoValido(SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(FabricaHttpClientSap.CertificadoValido(SslPolicyErrors.RemoteCertificateNotAvailable));
    }

    [Fact]
    public async Task NextLinkComHostDiferente_DeveSerRejeitadoAntesDeEnviarCredencial()
    {
        const string json = """
            {
              "value": [],
              "@odata.nextLink": "https://externo.exemplo.local/odata/PurchaseOrder?$skiptoken=1"
            }
            """;
        CapturaHandler handler = new(json);
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cliente.ConsultarPedidosAsync());

        Assert.Equal(1, handler.QuantidadeRequisicoes);
        Assert.All(handler.Destinos, destino => Assert.Equal("sap.exemplo.local", destino.Host));
    }

    [Fact]
    public async Task NextLinkRelativoValido_DeveContinuarPaginacao()
    {
        CapturaHandler handler = new(
            CriarPagina("PurchaseOrder?$skiptoken=RELATIVO"),
            CriarPagina(null));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await cliente.ConsultarPedidosAsync();

        Assert.Equal(2, handler.QuantidadeRequisicoes);
        Assert.All(handler.Destinos, destino =>
            Assert.StartsWith("/odata/", destino.AbsolutePath, StringComparison.Ordinal));
    }

    [Fact]
    public async Task NextLinkAbsolutoMesmaOrigemECaminho_DeveContinuarPaginacao()
    {
        CapturaHandler handler = new(
            CriarPagina("https://sap.exemplo.local/odata/PurchaseOrder?$skiptoken=ABSOLUTO"),
            CriarPagina(null));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await cliente.ConsultarPedidosAsync();

        Assert.Equal(2, handler.QuantidadeRequisicoes);
    }

    [Theory]
    [InlineData("http://sap.exemplo.local/odata/PurchaseOrder?$skiptoken=1")]
    [InlineData("https://sap.exemplo.local:444/odata/PurchaseOrder?$skiptoken=1")]
    [InlineData("https://sap.exemplo.local/outro-servico/PurchaseOrder?$skiptoken=1")]
    [InlineData("../outro-servico/PurchaseOrder?$skiptoken=1")]
    [InlineData("https://usuario:senha@sap.exemplo.local/odata/PurchaseOrder?$skiptoken=1")]
    public async Task NextLinkInseguro_DeveInterromperSemEnviarSegundaCredencial(string nextLink)
    {
        CapturaHandler handler = new(CriarPagina(nextLink));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        InvalidOperationException erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => cliente.ConsultarPedidosAsync());

        Assert.Equal("O nextLink retornado pelo SAP foi rejeitado por seguranca.", erro.Message);
        Assert.DoesNotContain(nextLink, erro.Message, StringComparison.Ordinal);
        Assert.Equal(1, handler.QuantidadeRequisicoes);
    }

    [Fact]
    public async Task ConsultarPedidos_DeveRespeitarTimeoutExterno()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler())
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ConsultarPedidosAsync());
    }

    [Fact]
    public async Task ConsultarPedidos_DeveRespeitarCancelamentoDoChamador()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);
        using CancellationTokenSource cancelamento = new();
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ConsultarPedidosAsync(cancelamento.Token));
    }

    [Fact]
    public async Task ConsultarPedidos_AtingindoLimiteComNextLink_DeveFalharComoIncompleta()
    {
        CapturaHandler handler = new(
            CriarPagina("PurchaseOrder?$skiptoken=2"),
            CriarPagina("PurchaseOrder?$skiptoken=3"));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http, maxPaginas: 2);

        SincronizacaoSapIncompletaException erro =
            await Assert.ThrowsAsync<SincronizacaoSapIncompletaException>(
                () => cliente.ConsultarPedidosAsync());

        Assert.Equal(2, erro.PaginasProcessadas);
        Assert.Equal(0, erro.ItensProcessados);
        Assert.Equal(2, handler.QuantidadeRequisicoes);
    }

    [Fact]
    public async Task ConsultarCargaCompleta_DeveInformarPaginasEPedidos()
    {
        CapturaHandler handler = new(
            """
            {
              "value": [
                {
                  "PurchaseOrder": "4500000001",
                  "_PurchaseOrderItem": [
                    { "PurchaseOrderItem": "10" },
                    { "PurchaseOrderItem": "20" }
                  ]
                }
              ],
              "@odata.nextLink": "PurchaseOrder?$skiptoken=2"
            }
            """,
            """{ "value": [ { "PurchaseOrder": "4500000002" } ] }""");
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http, maxPaginas: 3);

        var resultado = await cliente.ConsultarCargaCompletaAsync();

        Assert.Equal(2, resultado.PaginasProcessadas);
        Assert.Equal(2, resultado.Pedidos.Count);
        Assert.Equal(2, resultado.ItensProcessados);
    }

    [Fact]
    public async Task CargaEmMassa_DeveAplicarFiltroDeEscopoJales()
    {
        // H5/PEND#5: a carga do cache so pode trazer pedidos do escopo Jales — grupo de compras 700,
        // pedido nao totalmente entregue e com pelo menos um item no centro 3007.
        CapturaHandler handler = new("""{ "value": [] }""");
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await cliente.ConsultarPedidosAsync();

        Assert.NotEmpty(handler.Destinos);
        string filtro = Uri.UnescapeDataString(handler.Destinos[0].Query);
        Assert.Contains("$filter=", filtro, StringComparison.Ordinal);
        Assert.Contains("PurchasingGroup eq '700'", filtro, StringComparison.Ordinal);
        Assert.Contains(
            "_PurchaseOrderItem/any(d:d/Plant eq '3007' and d/IsCompletelyDelivered eq false)",
            filtro,
            StringComparison.Ordinal);
        Assert.Contains(
            "$expand=_PurchaseOrderItem($filter=Plant eq '3007' and IsCompletelyDelivered eq false;",
            filtro,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$filter=Plant eq '3007' and IsCompletelyDelivered eq false&$select",
            filtro,
            StringComparison.Ordinal);
        Assert.Contains(
            "$select=PurchaseOrderItem,Material,PurchaseOrderItemText,OrderQuantity,PurchaseOrderQuantityUnit,ItemNetWeight,Plant,StorageLocation,MaterialGroup,IsCompletelyDelivered",
            filtro,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultarPedidoValido_DeveUsarSomenteEndpointEspecifico()
    {
        const string json = """
            {
              "PurchaseOrder": "4500000010",
              "_PurchaseOrderItem": [{ "PurchaseOrderItem": "10" }]
            }
            """;
        CapturaHandler handler = new(json);
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        var pedido = await cliente.ConsultarPedidoAsync("4500000010");

        Assert.NotNull(pedido);
        Assert.Single(handler.Destinos);
        Assert.Contains(
            "PurchaseOrder('4500000010')",
            handler.Destinos[0].AbsoluteUri,
            StringComparison.Ordinal);
        string query = Uri.UnescapeDataString(handler.Destinos[0].Query);
        Assert.Contains("$expand=_PurchaseOrderItem", query);
        Assert.Contains(
            "$expand=_PurchaseOrderItem($filter=Plant eq '3007' and IsCompletelyDelivered eq false;",
            query,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$filter=Plant eq '3007' and IsCompletelyDelivered eq false&$select",
            query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultarPedidoInexistente_DeveRetornarNull()
    {
        using HttpClient http = new(new StatusHandler(HttpStatusCode.NotFound));
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        var pedido = await cliente.ConsultarPedidoAsync("4500000999");

        Assert.Null(pedido);
    }

    [Fact]
    public async Task ConsultarPedido_DeveRespeitarTimeoutExterno()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler())
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ConsultarPedidoAsync("4500000010"));
    }

    [Fact]
    public async Task ConsultarPedido_DeveRespeitarCancelamentoDoChamador()
    {
        using HttpClient http = new(new EsperaCancelamentoHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);
        using CancellationTokenSource cancelamento = new();
        cancelamento.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cliente.ConsultarPedidoAsync("4500000010", cancelamento.Token));
    }

    [Theory]
    [InlineData("https://usuario:senha@sap.exemplo.local/odata")]
    [InlineData("https://sap.exemplo.local/odata?destino=externo")]
    public void CriarCliente_ComBaseUrlInsegura_DeveRejeitar(string baseUrl)
    {
        using HttpClient http = new(new RespostaHandler("{}"));

        Assert.Throws<InvalidOperationException>(
            () => new PedidoCompraSapApiClient(CriarConfiguracao(baseUrl), http));
    }

    private static ConfiguracaoSap CriarConfiguracao(
        string baseUrl = "https://sap.exemplo.local/odata")
        => new()
        {
            BaseUrl = baseUrl,
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            SapClient = "000",
            HostsPermitidos = ["sap.exemplo.local"],
            TimeoutSegundos = 1
        };

    private static string CriarPagina(string? nextLink)
        => nextLink is null
            ? """{ "value": [] }"""
            : $$"""
                {
                  "value": [],
                  "@odata.nextLink": "{{nextLink}}"
                }
                """;

    private sealed class RespostaHandler(string conteudo) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(conteudo, Encoding.UTF8, "application/json")
            });
    }

    private sealed class StatusHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class CapturaHandler(params string[] conteudos) : HttpMessageHandler
    {
        private readonly Queue<string> _conteudos = new(conteudos);

        public int QuantidadeRequisicoes { get; private set; }
        public List<Uri> Destinos { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            QuantidadeRequisicoes++;
            Destinos.Add(request.RequestUri!);
            Assert.NotNull(request.Headers.Authorization);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _conteudos.Count > 0 ? _conteudos.Dequeue() : CriarPagina(null),
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private sealed class EsperaCancelamentoHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Fluxo inatingivel.");
        }
    }
}
