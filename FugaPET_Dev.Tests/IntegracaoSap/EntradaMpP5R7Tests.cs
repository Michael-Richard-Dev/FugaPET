using System.Net;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R7Tests
{
    [Fact]
    public async Task WriterRohBloqueiaMesmoComWriteTrueSemDispatch()
    {
        int dispatches = 0;
        MaterialDocumentSapServico writer = new(Config(), null, null, dispatchHttp: (_, _) =>
        {
            dispatches++;
            return Task.FromResult(new ResultadoMaterialDocumentSap { Sucesso = true });
        });
        ResultadoMaterialDocumentSap result = await writer.CriarDocumentoMaterial101Async(Roh(), "1/1");
        Assert.False(result.Sucesso);
        Assert.Contains("P5", result.MensagemSanitizada);
        Assert.Equal(0, dispatches);
    }

    [Fact]
    public async Task ClienteDiretoRohBloqueiaAntesDoHttp()
    {
        CountingHandler handler = new();
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient client = new(Config(), http);
        ResultadoMaterialDocumentSap result = await client.CriarDocumentoMaterialAsync(Roh());
        Assert.False(result.Sucesso);
        Assert.Contains("P5", result.MensagemSanitizada);
        Assert.Equal(0, handler.Count);
    }

    internal static ConfiguracaoSap Config() => new()
    {
        BaseUrl = "https://sap.example.test/sap/opu/odata/sap/API_PURCHASEORDER_2/",
        MaterialDocumentBaseUrl = "https://sap.example.test/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/",
        Usuario = "synthetic", Senha = "synthetic", SapClient = "110",
        HostsPermitidos = ["sap.example.test"], EscritaHabilitada = true
    };

    [Fact]
    public async Task WriterSemExtensaoP5PreservaDispatchExistente()
    {
        int dispatches = 0;
        MaterialDocumentSapServico writer = new(Config(), null, null, dispatchHttp: (_, _) =>
        {
            dispatches++;
            return Task.FromResult(new ResultadoMaterialDocumentSap { Sucesso = true });
        });
        MaterialDocumentSapItemRequest item = new() { Material = "SYNTHETIC", PurchaseOrder = "1", PurchaseOrderItem = "00010" };
        FugaPET_Dev.Servicos.Entrada.P5.ClassificacaoMaterialDocumentP5.Registrar(item,
            new ProdutoSapMestre { CodigoProduto = "SYNTHETIC", TipoMaterialSap = "HIBE", Consultado = true });
        MaterialDocumentSapRequest request = new() { Itens = [item] };
        Assert.True((await writer.CriarDocumentoMaterial101Async(request, "1/1")).Sucesso);
        Assert.Equal(1, dispatches);
    }

    [Fact]
    public async Task ParZz1IncompletoTambemBloqueiaSemDispatch()
    {
        CountingHandler handler = new();
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient client = new(Config(), http);
        MaterialDocumentSapRequest request = new() { Itens = [new() { PurchaseOrder = "1", Zz1PesoEntradaMmiu = "KG" }] };
        Assert.Contains("P5", (await client.CriarDocumentoMaterialAsync(request)).MensagemSanitizada);
        Assert.Equal(0, handler.Count);
    }

    private static MaterialDocumentSapRequest Roh() => new()
    {
        Itens = [new() { PurchaseOrder = "1", PurchaseOrderItem = "00010", Zz1PesoEntradaMmi = "1", Zz1PesoEntradaMmiu = "KG" }]
    };

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Count { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
