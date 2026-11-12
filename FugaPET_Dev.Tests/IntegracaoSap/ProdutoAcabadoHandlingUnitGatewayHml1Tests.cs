using System.Net;
using System.Text.Json;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class ProdutoAcabadoHandlingUnitGatewayHml1Tests
{
    private const string Host = "sap-hml.exemplo.local";
    private static readonly Uri BaseUri = new($"https://{Host}/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001/");

    [Fact]
    public async Task ReadOnlyAutorizado_ComPostBloqueado_ExecutaSomenteGetCsrf()
    {
        HandlerFake handler = new((request, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("X-CSRF-Token", "TOKEN_REAL_NAO_RETORNAR");
            response.Headers.TryAddWithoutValidation("Set-Cookie", "COOKIE_REAL_NAO_RETORNAR");
            return Task.FromResult(response);
        });
        ProdutoAcabadoHandlingUnitSapGateway gateway = CriarGateway(handler);

        ResultadoDiagnosticoConectividadeHandlingUnit diagnostico = await gateway.DiagnosticarConectividadeAsync();
        ResultadoPostHandlingUnit post = await gateway.CriarHandlingUnitCaixaAsync(
            new HandlingUnitCaixaRequest(), "HandlingUnit");

        Assert.True(gateway.ConectividadeReadOnlyAutorizada);
        Assert.False(gateway.EnvioAutorizado);
        Assert.Equal(200, diagnostico.HttpStatus);
        Assert.True(diagnostico.HttpsValido);
        Assert.True(diagnostico.BasicAuthAceita);
        Assert.True(diagnostico.CsrfObtido);
        Assert.True(diagnostico.CookieSessionObtido);
        Assert.Contains("sap-client=110", diagnostico.EndpointResolvido, StringComparison.Ordinal);
        Assert.Single(handler.Metodos);
        Assert.Equal(HttpMethod.Get, handler.Metodos[0]);
        Assert.Equal("Basic", handler.EsquemasAutenticacao[0]);
        Assert.Equal("Fetch", handler.CsrfSolicitados[0]);
        Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, post.Cenario);

        string resultadoSerializado = JsonSerializer.Serialize(diagnostico);
        Assert.DoesNotContain("SEGREDO_TESTE", resultadoSerializado, StringComparison.Ordinal);
        Assert.DoesNotContain("TOKEN_REAL_NAO_RETORNAR", resultadoSerializado, StringComparison.Ordinal);
        Assert.DoesNotContain("COOKIE_REAL_NAO_RETORNAR", resultadoSerializado, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", resultadoSerializado, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    public async Task FalhaAutorizacao_EhSanitizada(HttpStatusCode status, int esperado)
    {
        HandlerFake handler = new((_, _) => Task.FromResult(new HttpResponseMessage(status)));

        ResultadoDiagnosticoConectividadeHandlingUnit resultado = await CriarGateway(handler).DiagnosticarConectividadeAsync();

        Assert.Equal(esperado, resultado.HttpStatus);
        Assert.False(resultado.BasicAuthAceita);
        Assert.DoesNotContain("SEGREDO_TESTE", resultado.MensagemSanitizada, StringComparison.Ordinal);
        Assert.Single(handler.Metodos);
        Assert.Equal(HttpMethod.Get, handler.Metodos[0]);
    }

    [Fact]
    public async Task TimeoutGet_EhClassificadoSemPost()
    {
        HandlerFake handler = new((_, _) => throw new TaskCanceledException("timeout sintético"));

        ResultadoDiagnosticoConectividadeHandlingUnit resultado = await CriarGateway(handler).DiagnosticarConectividadeAsync();

        Assert.True(resultado.Timeout);
        Assert.Null(resultado.HttpStatus);
        Assert.Single(handler.Metodos);
        Assert.Equal(HttpMethod.Get, handler.Metodos[0]);
    }

    [Fact]
    public async Task HostForaDaAllowlist_BloqueiaAntesDaRede()
    {
        HandlerFake handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        ProdutoAcabadoHandlingUnitSapGateway gateway = new(
            BaseUri, "USUARIO_TESTE", "SEGREDO_TESTE", false, () => handler,
            conectividadeReadOnlyAutorizada: true,
            hostsPermitidos: ["outro-host.exemplo.local"],
            sapClient: "110");

        ResultadoDiagnosticoConectividadeHandlingUnit resultado = await gateway.DiagnosticarConectividadeAsync();

        Assert.Null(resultado.HttpStatus);
        Assert.Empty(handler.Metodos);
        Assert.Contains("host não permitido", resultado.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FabricaHml1_MantemReadOnlyLigadoEPostDesligado()
    {
        Assert.True(FabricaProdutoAcabadoHandlingUnitSapServico.ConectividadeReadOnlyPadrao);
        Assert.False(FabricaProdutoAcabadoHandlingUnitSapServico.EnvioHuAutorizadoPadrao);
    }

    private static ProdutoAcabadoHandlingUnitSapGateway CriarGateway(HandlerFake handler)
        => new(
            BaseUri, "USUARIO_TESTE", "SEGREDO_TESTE", false, () => handler,
            timeout: TimeSpan.FromSeconds(1),
            conectividadeReadOnlyAutorizada: true,
            hostsPermitidos: [Host],
            sapClient: "110");

    private sealed class HandlerFake(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<HttpMethod> Metodos { get; } = [];
        public List<string?> EsquemasAutenticacao { get; } = [];
        public List<string?> CsrfSolicitados { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodos.Add(request.Method);
            EsquemasAutenticacao.Add(request.Headers.Authorization?.Scheme);
            CsrfSolicitados.Add(request.Headers.TryGetValues("X-CSRF-Token", out IEnumerable<string>? valores)
                ? valores.FirstOrDefault()
                : null);
            return responder(request, cancellationToken);
        }
    }
}
