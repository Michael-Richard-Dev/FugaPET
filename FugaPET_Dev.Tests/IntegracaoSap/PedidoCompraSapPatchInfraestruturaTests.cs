using System.Net;
using System.Net.Http.Headers;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class PedidoCompraSapPatchInfraestruturaTests
{
    [Fact]
    public async Task CsrfAusente_DeveAbortarSemEnviarPatch()
    {
        PatchHandler handler = new(CriarRespostaLeitura(etag: "\"v1\""));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.False(resultado.Sucesso);
        Assert.Contains("CSRF", resultado.Mensagem);
        Assert.Equal(1, handler.QuantidadeRequisicoes);
        Assert.DoesNotContain(handler.Metodos, metodo => metodo == HttpMethod.Patch);
    }

    [Fact]
    public async Task CsrfValidoEtagValido_DeveEnviarValoresReaisNoPatch()
    {
        PatchHandler handler = new(
            CriarRespostaLeitura(token: "csrf-valido", etag: "W/\"v2\""),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, handler.QuantidadeRequisicoes);
        Assert.Equal("csrf-valido", handler.TokenCsrfPatch);
        Assert.Equal("W/\"v2\"", handler.IfMatchPatch);
        Assert.NotEqual("*", handler.IfMatchPatch);
    }

    [Fact]
    public async Task EtagAusente_DeveEnviarPatchSemIfMatch()
    {
        PatchHandler handler = new(
            CriarRespostaLeitura(token: "csrf-valido"),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, handler.QuantidadeRequisicoes);
        Assert.Contains(handler.Metodos, metodo => metodo == HttpMethod.Patch);
        Assert.Null(handler.IfMatchPatch);
    }

    [Fact]
    public async Task EtagNoCorpoOData_DeveEnviarValorRealNoPatch()
    {
        HttpResponseMessage leitura = CriarRespostaLeitura(token: "csrf-valido");
        leitura.Content = new StringContent("""{"@odata.etag":"W/\"v-corpo\""}""");
        PatchHandler handler = new(
            leitura,
            new HttpResponseMessage(HttpStatusCode.NoContent));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.True(resultado.Sucesso);
        Assert.Equal("W/\"v-corpo\"", handler.IfMatchPatch);
        Assert.NotEqual("*", handler.IfMatchPatch);
    }

    [Fact]
    public async Task Http412_DeveRetornarConflitoControlado()
    {
        PatchHandler handler = new(
            CriarRespostaLeitura(token: "csrf-valido", etag: "\"v3\""),
            new HttpResponseMessage(HttpStatusCode.PreconditionFailed));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.False(resultado.Sucesso);
        Assert.Equal(412, resultado.HttpStatus);
        Assert.Contains("alterado no SAP", resultado.Mensagem);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task SessaoInvalidaNaLeitura_DeveAbortarSemEnviarPatch(HttpStatusCode status)
    {
        PatchHandler handler = new(new HttpResponseMessage(status));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.False(resultado.Sucesso);
        Assert.Equal((int)status, resultado.HttpStatus);
        Assert.Contains("Sessao SAP invalida", resultado.Mensagem);
        Assert.Equal(1, handler.QuantidadeRequisicoes);
    }

    [Fact]
    public async Task SessaoExpiradaNoPatch_DeveRetornarErroControlado()
    {
        PatchHandler handler = new(
            CriarRespostaLeitura(token: "csrf-valido", etag: "\"v4\""),
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using HttpClient http = new(handler);
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync("4500000010", "10", 29.9m, 30m);

        Assert.False(resultado.Sucesso);
        Assert.Equal(401, resultado.HttpStatus);
        Assert.Contains("Sessao SAP invalida", resultado.Mensagem);
        Assert.Equal(2, handler.QuantidadeRequisicoes);
    }

    [Fact]
    public void FabricaHttpClientSap_DevePreservarCookiesDaSessao()
    {
        using HttpClientHandler handler = FabricaHttpClientSap.CriarHandler();

        Assert.True(handler.UseCookies);
        Assert.NotNull(handler.CookieContainer);
        Assert.False(handler.AllowAutoRedirect);
    }

    private static ConfiguracaoSap CriarConfiguracao()
        => new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            SapClient = "000",
            HostsPermitidos = ["sap.exemplo.local"]
        };

    private static HttpResponseMessage CriarRespostaLeitura(
        string? token = null,
        string? etag = null)
    {
        HttpResponseMessage resposta = new(HttpStatusCode.OK);
        if (!string.IsNullOrWhiteSpace(token))
        {
            resposta.Headers.TryAddWithoutValidation("X-CSRF-Token", token);
        }

        if (!string.IsNullOrWhiteSpace(etag))
        {
            resposta.Headers.ETag = EntityTagHeaderValue.Parse(etag);
        }

        return resposta;
    }

    private sealed class PatchHandler(params HttpResponseMessage[] respostas) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respostas = new(respostas);

        public int QuantidadeRequisicoes { get; private set; }
        public List<HttpMethod> Metodos { get; } = [];
        public string? TokenCsrfPatch { get; private set; }
        public string? IfMatchPatch { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            QuantidadeRequisicoes++;
            Metodos.Add(request.Method);

            if (request.Method == HttpMethod.Patch)
            {
                TokenCsrfPatch = request.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? tokens)
                    ? tokens.Single()
                    : null;
                IfMatchPatch = request.Headers.IfMatch.SingleOrDefault()?.ToString();
            }

            return Task.FromResult(_respostas.Dequeue());
        }
    }
}
