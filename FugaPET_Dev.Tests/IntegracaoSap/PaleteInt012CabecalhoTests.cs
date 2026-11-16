using System.Net;
using System.Net.Http.Headers;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE 126M: o que vai nos CABECALHOS do POST do palete.
/// <para>
/// Em 16/11/2026 o MESMO corpo, na MESMA URL, voltou 500 pelo aplicativo e 200 pelo Postman. O corpo
/// foi conferido contra o documento INT012 e contra o JSON persistido — identicos. A diferenca
/// restante era o aplicativo mandar "application/json; charset=utf-8" e o Postman "application/json".
/// </para>
/// <para>
/// Estes testes inspecionam a requisicao REAL montada pelo gateway, interceptada por um handler, em
/// vez de auditar o fonte: e o cabecalho que sai no fio que importa.
/// </para>
/// </summary>
public sealed class PaleteInt012CabecalhoTests
{
    private sealed class HandlerEspiao : HttpMessageHandler
    {
        internal HttpRequestMessage? Requisicao;
        internal string? CorpoEnviado;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requisicao = request;
            CorpoEnviado = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"UC_gerada":"300099999","Status":"S","_Mensagens":[]}""")
            };
        }
    }

    private static ProdutoAcabadoPaleteRequest Payload() => new()
    {
        HandlingUnitExternalID = "$1",
        PackagingMaterial = "PALLET01",
        Plant = "3007",
        StorageLocation = "PP02",
        GrossWeight = 4.800m,
        NetWeight = 2.800m,
        TareWeight = 2.000m,
        WeightUnit = "KG",
        HandlingUnitItems = [new() { HandlingUnit = "300015998" }, new() { HandlingUnit = "300015999" }]
    };

    private static async Task<HandlerEspiao> EnviarAsync()
    {
        HandlerEspiao espiao = new();
        ProdutoAcabadoPaleteInt012Gateway gateway = new(
            baseUrl: "https://host.exemplo/http/pesagem/handling_unit/CreateHUInput/1111/SAP__self.processHandlingUnitPayload",
            hostsPermitidos: ["host.exemplo"],
            usuario: "u",
            senha: "p",
            envioAutorizado: true,
            fabricaHandler: () => espiao,
            timeout: TimeSpan.FromSeconds(30));

        await gateway.EnviarPaleteAsync(Payload(), CancellationToken.None);
        return espiao;
    }

    /// <summary>
    /// O defeito: "application/json; charset=utf-8". O rotulo tem de sair LIMPO — os bytes continuam
    /// UTF-8, que e o padrao do JSON.
    /// </summary>
    [Fact]
    public async Task ContentType_NaoLevaCharset()
    {
        HandlerEspiao espiao = await EnviarAsync();

        MediaTypeHeaderValue? tipo = espiao.Requisicao!.Content!.Headers.ContentType;
        Assert.NotNull(tipo);
        Assert.Equal("application/json", tipo!.MediaType);
        Assert.Null(tipo.CharSet);
        Assert.DoesNotContain("charset", tipo.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>O corpo continua em UTF-8 de fato: so o rotulo mudou.</summary>
    [Fact]
    public async Task CorpoContinuaIntegro()
    {
        HandlerEspiao espiao = await EnviarAsync();

        Assert.Contains("\"PackagingMaterial\":\"PALLET01\"", espiao.CorpoEnviado, StringComparison.Ordinal);
        Assert.Contains("\"HandlingUnitExternalID\":\"$1\"", espiao.CorpoEnviado, StringComparison.Ordinal);
        Assert.Contains("\"_HandlingUnitItem\"", espiao.CorpoEnviado, StringComparison.Ordinal);
        Assert.Contains("300015998", espiao.CorpoEnviado, StringComparison.Ordinal);
    }

    /// <summary>POST e Basic Auth seguem como o documento INT012 exige.</summary>
    [Fact]
    public async Task MetodoEAutenticacaoSeguemOContrato()
    {
        HandlerEspiao espiao = await EnviarAsync();

        Assert.Equal(HttpMethod.Post, espiao.Requisicao!.Method);
        Assert.Equal("Basic", espiao.Requisicao.Headers.Authorization!.Scheme);
    }
}
