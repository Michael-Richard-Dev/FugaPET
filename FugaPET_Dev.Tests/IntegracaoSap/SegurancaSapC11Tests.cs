using System.Net;
using System.Text;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class SegurancaSapC11Tests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ConsultaPedido_HttpErro_DevePreservarStatusSemExporResposta(
        HttpStatusCode status)
    {
        const string respostaSensivel =
            """{"error":{"message":"Authorization: Basic credencial-nao-pode-vazar"}}""";
        using HttpClient http = new(new RespostaHandler(
            new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    respostaSensivel,
                    Encoding.UTF8,
                    "application/json")
            }));
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        HttpRequestException erro = await Assert.ThrowsAsync<HttpRequestException>(
            () => cliente.ConsultarPedidoAsync("4500000010"));

        Assert.Equal(status, erro.StatusCode);
        Assert.DoesNotContain(
            "credencial-nao-pode-vazar",
            erro.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConsultaPedido_Http404_DeveRetornarAusente()
    {
        using HttpClient http = new(new RespostaHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        PedidoCompraSap? pedido = await cliente.ConsultarPedidoAsync("4500000999");

        Assert.Null(pedido);
    }

    [Fact]
    public async Task ConsultaPedido_JsonInvalido_DeveFalharSemRequisicaoReal()
    {
        using HttpClient http = new(new RespostaHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{ json-invalido",
                    Encoding.UTF8,
                    "application/json")
            }));
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        await Assert.ThrowsAnyAsync<JsonException>(
            () => cliente.ConsultarPedidoAsync("4500000010"));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Patch_HttpErro_DeveRetornarStatusEMensagemSanitizada(
        HttpStatusCode status)
    {
        HttpResponseMessage leitura = new(HttpStatusCode.OK);
        leitura.Headers.TryAddWithoutValidation("X-CSRF-Token", "csrf-teste");
        leitura.Headers.TryAddWithoutValidation("ETag", "\"v1\"");

        using HttpClient http = new(new RespostaHandler(
            leitura,
            new HttpResponseMessage(status)
            {
                Content = new StringContent(
                    """{"error":{"message":"Authorization: Basic segredo"}}""",
                    Encoding.UTF8,
                    "application/json")
            }));
        PedidoCompraSapApiClient cliente = new(CriarConfiguracao(), http);

        ResultadoPatchSap resultado = await cliente.AtualizarPesoItemAsync(
            "4500000010",
            "10",
            29.9m,
            30m);

        Assert.False(resultado.Sucesso);
        Assert.Equal((int)status, resultado.HttpStatus);
        Assert.DoesNotContain("segredo", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ContratoLog_NaoDevePossuirCamposDeCredencialOuConteudoHttp()
    {
        string[] propriedades = typeof(RegistroLogIntegracaoSap)
            .GetProperties()
            .Select(propriedade => propriedade.Name)
            .ToArray();

        Assert.DoesNotContain(propriedades, NomeSensivel);
        Assert.Contains(nameof(RegistroLogIntegracaoSap.CorrelationId), propriedades);
        Assert.Contains(nameof(RegistroLogIntegracaoSap.StatusHttp), propriedades);
        Assert.Contains(nameof(RegistroLogIntegracaoSap.RegistradoEmUtc), propriedades);
    }

    private static bool NomeSensivel(string nome)
        => nome.Contains("Senha", StringComparison.OrdinalIgnoreCase)
           || nome.Contains("Password", StringComparison.OrdinalIgnoreCase)
           || nome.Contains("Authorization", StringComparison.OrdinalIgnoreCase)
           || nome.Contains("Cookie", StringComparison.OrdinalIgnoreCase)
           || nome.Contains("Payload", StringComparison.OrdinalIgnoreCase)
           || nome.Contains("Response", StringComparison.OrdinalIgnoreCase)
           || nome.Contains("Resposta", StringComparison.OrdinalIgnoreCase);

    private static ConfiguracaoSap CriarConfiguracao()
        => new()
        {
            BaseUrl = "https://sap.exemplo.local/odata",
            Usuario = "usuario-teste",
            Senha = "senha-teste",
            SapClient = "000",
            HostsPermitidos = ["sap.exemplo.local"]
        };

    private sealed class RespostaHandler(params HttpResponseMessage[] respostas) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respostas = new(respostas);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_respostas.Dequeue());
    }
}
