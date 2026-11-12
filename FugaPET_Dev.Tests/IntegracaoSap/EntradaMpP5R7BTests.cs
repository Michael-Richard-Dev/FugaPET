using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R7BTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PedidoSemClassificacaoNaoDespachaMesmoSemZz1(bool viaWriter)
    {
        int dispatches = 0;
        using HttpClient http = new(new NeverHttp());
        MaterialDocumentSapApiClient client = new(EntradaMpP5R7Tests.Config(), http);
        MaterialDocumentSapRequest request = new()
        {
            Itens = [new() { Material = "SYNTHETIC", PurchaseOrder = "123", PurchaseOrderItem = "00010" }]
        };
        ResultadoMaterialDocumentSap result = viaWriter
            ? await new MaterialDocumentSapServico(EntradaMpP5R7Tests.Config(), null, client,
                dispatchHttp: (_, _) => { dispatches++; return Task.FromResult(new ResultadoMaterialDocumentSap { Sucesso = true }); })
                .CriarDocumentoMaterial101Async(request, "123/900")
            : await client.CriarDocumentoMaterialAsync(request);
        Assert.False(result.Sucesso);
        Assert.Contains("P5", result.MensagemSanitizada);
        Assert.Equal(0, dispatches);
    }

    [Theory]
    [InlineData("ROH", false, false)]
    [InlineData("HIBE", true, false)]
    [InlineData("HIBE", false, true)]
    [InlineData("DESCONHECIDO", false, false)]
    public async Task TipoOuIdentidadeAusenteBloqueiaMesmoSemCustomizados(
        string tipo, bool clonar, bool produtoDivergente)
    {
        MaterialDocumentSapItemRequest item = new()
        {
            Material = "SYNTHETIC", PurchaseOrder = "123", PurchaseOrderItem = "00010"
        };
        FugaPET_Dev.Servicos.Entrada.P5.ClassificacaoMaterialDocumentP5.Registrar(item,
            new ProdutoSapMestre
            {
                CodigoProduto = produtoDivergente ? "OTHER" : "SYNTHETIC",
                TipoMaterialSap = tipo, Consultado = true
            });
        if (clonar) item = item with { Material = "OTHER" };
        int dispatches = 0;
        MaterialDocumentSapServico writer = new(EntradaMpP5R7Tests.Config(), null, null,
            dispatchHttp: (_, _) =>
            {
                dispatches++;
                return Task.FromResult(new ResultadoMaterialDocumentSap { Sucesso = true });
            });
        Assert.False((await writer.CriarDocumentoMaterial101Async(
            new() { Itens = [item] }, "123/900")).Sucesso);
        Assert.Equal(0, dispatches);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultiItemRohOuMistoSemZz1NaoDespacha(bool misto)
    {
        MaterialDocumentSapItemRequest Criar(string numeroItem, string tipo)
        {
            MaterialDocumentSapItemRequest item = new()
            {
                Material = "SYNTHETIC", PurchaseOrder = "123", PurchaseOrderItem = numeroItem
            };
            return FugaPET_Dev.Servicos.Entrada.P5.ClassificacaoMaterialDocumentP5.Registrar(item,
                new ProdutoSapMestre { CodigoProduto = "SYNTHETIC", TipoMaterialSap = tipo, Consultado = true });
        }
        using HttpClient http = new(new NeverHttp());
        MaterialDocumentSapApiClient client = new(EntradaMpP5R7Tests.Config(), http);
        ResultadoMaterialDocumentSap result = await client.CriarDocumentoMaterialAsync(
            new() { Itens = [Criar("00010", "ROH"), Criar("00020", misto ? "HIBE" : "ROH")] });
        Assert.False(result.Sucesso);
        Assert.Contains("P5", result.MensagemSanitizada);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManufacturingOrderPreservaRotaDosOutrosProcessos(bool viaWriter)
    {
        ManufacturingHandler handler = new();
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient client = new(EntradaMpP5R7Tests.Config(), http);
        MaterialDocumentSapRequest request = new()
        {
            Itens = [new() { Material = "SYNTHETIC", ManufacturingOrder = "1000001" }]
        };
        ResultadoMaterialDocumentSap result = viaWriter
            ? await new MaterialDocumentSapServico(EntradaMpP5R7Tests.Config(), null, client)
                .CriarDocumentoMaterial101Async(request, "PA/900")
            : await client.CriarDocumentoMaterialAsync(request);
        Assert.True(result.Sucesso);
        Assert.True(result.PostTentado);
        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Post }, handler.Metodos);
    }

    private sealed class ManufacturingHandler : HttpMessageHandler
    {
        internal readonly List<HttpMethod> Metodos = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodos.Add(request.Method);
            HttpResponseMessage response = new(request.Method == HttpMethod.Get
                ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.Created);
            if (request.Method == HttpMethod.Get) response.Headers.TryAddWithoutValidation("X-CSRF-Token", "OFFLINE");
            else response.Content = new StringContent(
                """{"d":{"MaterialDocument":"5000000001","MaterialDocumentYear":"2026"}}""");
            return Task.FromResult(response);
        }
    }

    private sealed class NeverHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("HTTP_FORBIDDEN");
    }
}
