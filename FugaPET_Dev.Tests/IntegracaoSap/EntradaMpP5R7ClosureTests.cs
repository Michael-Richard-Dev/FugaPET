using System.Net;
using System.Text;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaMpP5R7ClosureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HibeRealMapperPreservaWriterEClienteComHttpFalso(bool viaWriter)
    {
        MaterialDocumentSapItemRequest item = Hibe();
        Assert.Null(item.Zz1PesoEntradaMmi); Assert.Null(item.Zz1PesoEntradaMmiu);
        Assert.Equal("5.5", item.QuantityInEntryUnit);
        OfflineHandler handler = new();
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(EntradaMpP5R7Tests.Config(), http);
        MaterialDocumentSapRequest request = new() { Itens = [item] };
        ResultadoMaterialDocumentSap result = viaWriter
            ? await new MaterialDocumentSapServico(EntradaMpP5R7Tests.Config(), null, cliente)
                .CriarDocumentoMaterial101Async(request, "123/900")
            : await cliente.CriarDocumentoMaterialAsync(request);
        Assert.True(result.Sucesso);
        Assert.Equal(new[] { "GET", "POST" }, handler.Metodos);
        Assert.DoesNotContain("ZZ1_PESO_ENTRADA", handler.Payload);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DocumentoMultiItemRohOuMistoBloqueiaInteiroSemSplitOuHttp(bool viaWriter, bool misto)
    {
        OfflineHandler handler = new();
        using HttpClient http = new(handler);
        MaterialDocumentSapApiClient cliente = new(EntradaMpP5R7Tests.Config(), http);
        MaterialDocumentSapItemRequest roh = Hibe() with { Zz1PesoEntradaMmi = "5.500", Zz1PesoEntradaMmiu = "KG" };
        MaterialDocumentSapRequest request = new()
        {
            Itens = [roh, misto ? Hibe() with { PurchaseOrderItem = "00020" } : roh with { PurchaseOrderItem = "00020" }]
        };
        ResultadoMaterialDocumentSap result = viaWriter
            ? await new MaterialDocumentSapServico(EntradaMpP5R7Tests.Config(), null, cliente)
                .CriarDocumentoMaterial101Async(request, "123/900")
            : await cliente.CriarDocumentoMaterialAsync(request);
        Assert.False(result.Sucesso); Assert.Contains("P5", result.MensagemSanitizada);
        Assert.Empty(handler.Metodos);
    }

    private static MaterialDocumentSapItemRequest Hibe()
    {
        ClassificacaoEntradaMaterial classificacao = ClassificadorItemEntradaMaterial.ClassificarPorProductType("HIBE");
        ResultadoAlocacao101 alocacao = EntradaProdutoAlocacao101.Decidir(classificacao, null, "KG", 5.5m, "SYNTHETIC");
        MaterialDocumentSapItemRequest item = EntradaProdutoController.MontarItemMaterialDocument("123", new EntradaProdutoItemEnvioSap
        {
            Material = "SYNTHETIC", Centro = "3007", Deposito = "PP01", Unidade = "KG", NumeroItem = "10"
        }, false, alocacao);
        return FugaPET_Dev.Servicos.Entrada.P5.ClassificacaoMaterialDocumentP5.Registrar(item,
            new ProdutoSapMestre { CodigoProduto = "SYNTHETIC", TipoMaterialSap = "HIBE", Consultado = true });
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        internal readonly List<string> Metodos = [];
        internal string Payload = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodos.Add(request.Method.Method);
            if (request.Method == HttpMethod.Get)
            {
                HttpResponseMessage fetch = new(HttpStatusCode.OK);
                fetch.Headers.TryAddWithoutValidation("X-CSRF-Token", "OFFLINE_CSRF");
                return fetch;
            }
            Assert.Equal(HttpMethod.Post, request.Method);
            Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("""{"d":{"MaterialDocument":"5000000001","MaterialDocumentYear":"2026"}}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
