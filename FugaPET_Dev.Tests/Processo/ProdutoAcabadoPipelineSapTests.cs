using System.Net;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// FAST TRACK REV3: pipeline 261 â†’ 101 â†’ HU (gates PA isolados, generic WRITE=false), 261 multicomponente
/// reutilizando o CLIENTE homologado, 101 real (02/101/F) sobre o cliente MaterialDocument, e INT012 com o
/// contrato confirmado (Status S/E + Tipo=E + Texto). Sem rede/SAP real: fakes + HttpMessageHandler controlado.
/// </summary>
public sealed class ProdutoAcabadoPipelineSapTests
{
    private const long Usuario = 42;
    private const string Terminal = "TERM-01";

    private static ProdutoAcabadoMovimento261Command Cmd261() => new()
    {
        NumeroOrdem = "1001951",
        CorrelationId = "PA-1-1",
        PostingDate = new DateTime(2026, 8, 11),
        DocumentDate = new DateTime(2026, 8, 11),
        Itens = [new() { Material = "3500027", Plant = "3007", StorageLocation = "PP01", Quantidade = 5m, Unidade = "KG", Reservation = "10", ReservationItem = "1", Batch = "L-COMP" }]
    };
    private static ProdutoAcabadoMovimento101Command Cmd101Completo() => new()
    { NumeroOrdem = "1001951", Material = "4000108", Plant = "3007", StorageLocation = "PP02", ManufacturingOrderItem = "0001", QuantityInEntryUnit = "60", EntryUnit = "KG", Batch = "L1", PostingDate = new DateTime(2026, 8, 11), DocumentDate = new DateTime(2026, 8, 11) };
    // ===================== 261 ADAPTER (gate PA sem generic WRITE) =====================
    private sealed class FakeClient261 : IConsumoMaterialSap261Client
    {
        private readonly ResultadoEnvioConsumoSap261 _r;
        public int Chamadas { get; private set; }
        public ConsumoMaterialSap261Request? Ultima { get; private set; }
        public FakeClient261(ResultadoEnvioConsumoSap261 r) => _r = r;
        public Task<ResultadoEnvioConsumoSap261> EnviarConsumo261Async(ConsumoMaterialSap261Request req, string corr, CancellationToken ct = default)
        { Chamadas++; Ultima = req; return Task.FromResult(_r); }
    }

    // PA gate=true + generic WRITE=false â‡’ o fluxo PA chega ao cliente especÃ­fico (nÃ£o passa pelo gate genÃ©rico).
    [Fact]
    public async Task Adapter261_GateTrue_UsaClienteSem_GenericWrite()
    {
        FakeClient261 cli = new(ResultadoEnvioConsumoSap261.Ok("490", "2026", 201, null));
        ProdutoAcabadoMovimento261Adapter adapter = new(cli, gatePaHabilitado: true);
        ResultadoMovimentoSap r = await adapter.EnviarAsync(Cmd261());
        Assert.Equal(EstadoMovimentoSap.Confirmado, r.Estado);
        Assert.Equal(1, cli.Chamadas); // chegou ao cliente sem depender de FUGAPET_SAP_WRITE_ENABLED
    }

    [Fact]
    public async Task Adapter261_GateFalse_ZeroCliente_NaoIndeterminado()
    {
        FakeClient261 cli = new(ResultadoEnvioConsumoSap261.Ok("490", "2026", 201, null));
        ResultadoMovimentoSap r = await new ProdutoAcabadoMovimento261Adapter(cli, false).EnviarAsync(Cmd261());
        Assert.Equal(EstadoMovimentoSap.Erro, r.Estado); // bloqueio prÃ©-HTTP Ã© ERRO SEGURO, nunca IndeterminadoTimeout
        Assert.NotEqual(EstadoMovimentoSap.IndeterminadoTimeout, r.Estado);
        Assert.Equal(0, cli.Chamadas);
    }

    [Fact]
    public async Task Adapter261_MultiplosComponentes_UmRequest_NItens()
    {
        FakeClient261 cli = new(ResultadoEnvioConsumoSap261.Ok("490", "2026", 201, null));
        ProdutoAcabadoMovimento261Command cmd = new()
        {
            NumeroOrdem = "1001951",
            CorrelationId = "PA-1-1",
            PostingDate = new DateTime(2026, 8, 11),
            DocumentDate = new DateTime(2026, 8, 11),
            Itens =
            [
                new() { Material = "A", Plant = "3007", StorageLocation = "PP01", Quantidade = 5m, Unidade = "KG", Reservation = "10", ReservationItem = "1", Batch = "L-COMP" },
                new() { Material = "B", Plant = "3007", StorageLocation = "PP01", Quantidade = 3m, Unidade = "KG", Reservation = "10", ReservationItem = "2", Batch = "L-COMP-2" },
            ]
        };
        await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(cmd);
        Assert.Equal(1, cli.Chamadas); // exatamente UM request MaterialDocument
        Assert.Equal(2, cli.Ultima!.ToMaterialDocumentItem.Count); // N itens preservados
        Assert.Equal("A", cli.Ultima.ToMaterialDocumentItem[0].Material);
        Assert.Equal("B", cli.Ultima.ToMaterialDocumentItem[1].Material);
    }

    [Fact]
    public void Adapter261_Payload_Homologado_Intacto()
    {
        ConsumoMaterialSap261Request req = ProdutoAcabadoMovimento261Adapter.MapearRequisicao(Cmd261());
        Assert.Equal("03", req.GoodsMovementCode);
        Assert.Equal("261", req.ToMaterialDocumentItem[0].GoodsMovementType);
        Assert.Equal("KG", req.ToMaterialDocumentItem[0].EntryUnit);
        Assert.Equal("1001951", req.ToMaterialDocumentItem[0].ManufacturingOrder);
    }

    [Fact]
    public async Task Adapter261_ComponenteIncompleto_FailClosed_SemCliente()
    {
        FakeClient261 cli = new(ResultadoEnvioConsumoSap261.Ok("490", "2026", 201, null));
        ProdutoAcabadoMovimento261Command cmd = Cmd261() with { Itens = [Cmd261().Itens[0] with { Reservation = string.Empty }] };
        ResultadoMovimentoSap r = await new ProdutoAcabadoMovimento261Adapter(cli, true).EnviarAsync(cmd);
        Assert.Equal(EstadoMovimentoSap.Erro, r.Estado);
        Assert.Equal(0, cli.Chamadas);
        Assert.Contains("DEPENDENCIA", r.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
    }

    // ===================== 101 GATEWAY REAL (02/101/F) =====================
    private sealed class FakeMatDocClient : IMaterialDocumentSapClient
    {
        private readonly ResultadoMaterialDocumentSap _r; public int Chamadas { get; private set; }
        public MaterialDocumentSapRequest? Ultima { get; private set; }
        public FakeMatDocClient(ResultadoMaterialDocumentSap r) => _r = r;
        public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterialAsync(MaterialDocumentSapRequest req, CancellationToken ct = default)
        { Chamadas++; Ultima = req; return Task.FromResult(_r); }
    }
    private static ResultadoMaterialDocumentSap MatDoc(bool ok, string? doc, string? ano, int? http)
        => new() { Sucesso = ok, MaterialDocument = doc, MaterialDocumentYear = ano, StatusHttp = http, MensagemSanitizada = "x" };

    [Fact]
    public void Gateway101_Payload_02_101_F_SemPurchaseOrder()
    {
        MaterialDocumentSapRequest req = ProdutoAcabadoMovimento101Gateway.MontarRequisicao(Cmd101Completo());
        Assert.Equal("02", req.GoodsMovementCode);
        MaterialDocumentSapItemRequest item = Assert.Single(req.Itens);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("F", item.GoodsMovementRefDocType);
        Assert.Equal("4000108", item.Material);
        Assert.Equal("3007", item.Plant);
        Assert.Equal("PP02", item.StorageLocation);
        Assert.Null(item.PurchaseOrder);
        Assert.Null(item.PurchaseOrderItem);
    }

    [Fact]
    public async Task Gateway101_GateFalse_Bloqueado()
        => Assert.Equal(EstadoMovimentoSap.Erro,
            (await new ProdutoAcabadoMovimento101Gateway(new FakeMatDocClient(MatDoc(true, "1", "2026", 201)), false).EnviarAsync(Cmd101Completo())).Estado);

    [Fact]
    public async Task Gateway101_CamposPendentes_DependenciaAres_ZeroHttp()
    {
        FakeMatDocClient cli = new(MatDoc(true, "1", "2026", 201));
        ProdutoAcabadoMovimento101Command parcial = Cmd101Completo() with { ManufacturingOrderItem = string.Empty };
        ResultadoMovimentoSap r = await new ProdutoAcabadoMovimento101Gateway(cli, true).EnviarAsync(parcial);
        Assert.Equal(EstadoMovimentoSap.Erro, r.Estado);
        Assert.Equal(0, cli.Chamadas);
        Assert.Contains("DEPENDENCIA_ARES", r.MensagemSanitizada, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gateway101_201_ComDocumento_Confirma()
    {
        FakeMatDocClient cli = new(MatDoc(true, "490000999", "2026", 201));
        Assert.Equal(EstadoMovimentoSap.Confirmado, (await new ProdutoAcabadoMovimento101Gateway(cli, true).EnviarAsync(Cmd101Completo())).Estado);
        Assert.Equal(1, cli.Chamadas);
    }

    [Fact]
    public async Task Gateway101_201_SemDocumento_NaoConfirma()
    {
        FakeMatDocClient cli = new(MatDoc(true, null, null, 201));
        Assert.Equal(EstadoMovimentoSap.Erro, (await new ProdutoAcabadoMovimento101Gateway(cli, true).EnviarAsync(Cmd101Completo())).Estado);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(402)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task Gateway101_HttpErro4xx_Erro(int status)
        => Assert.Equal(EstadoMovimentoSap.Erro,
            (await new ProdutoAcabadoMovimento101Gateway(new FakeMatDocClient(MatDoc(false, null, null, status)), true).EnviarAsync(Cmd101Completo())).Estado);

    [Fact]
    public async Task Gateway101_5xx_Ou_SemStatus_Indeterminado()
    {
        Assert.Equal(EstadoMovimentoSap.IndeterminadoTimeout, (await new ProdutoAcabadoMovimento101Gateway(new FakeMatDocClient(MatDoc(false, null, null, 500)), true).EnviarAsync(Cmd101Completo())).Estado);
        Assert.Equal(EstadoMovimentoSap.IndeterminadoTimeout, (await new ProdutoAcabadoMovimento101Gateway(new FakeMatDocClient(MatDoc(false, null, null, null)), true).EnviarAsync(Cmd101Completo())).Estado);
    }

    // ===================== HU ADAPTER (Â§5) â€” estrutural =====================
    [Fact]
    public void HuAdapter_Real_Delega_Ao_HuService_UmaVez()
    {
        string src = File.ReadAllText(CaminhoProjeto("Servicos", "Operacao", "ProdutoAcabadoHuEnvioAdapter.cs"));
        Assert.Contains("_huService.EnviarAsync(codigoCaixa, usuario, terminal, cancellationToken)", src, StringComparison.Ordinal);
        Assert.Contains("resultado.Caixa?.StatusIntegracao", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ClaimEnvioAsync", src, StringComparison.Ordinal); // nÃ£o duplica claim
    }

    // ===================== INT012 (contrato confirmado Â§7) =====================
    private sealed class HandlerInt012 : HttpMessageHandler
    {
        private readonly HttpStatusCode _s; private readonly string _c; private readonly bool _t;
        public int Posts { get; private set; }
        public HandlerInt012(HttpStatusCode s, string c, bool t = false) { _s = s; _c = c; _t = t; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Posts++; if (_t) { throw new TaskCanceledException(); } return Task.FromResult(new HttpResponseMessage(_s) { Content = new StringContent(_c) }); }
    }
    private static ProdutoAcabadoPaleteRequest ReqInt012(string pack = "PACK_TEST_001") => new()
    { HandlingUnitExternalID = "PLT-1", GrossWeight = 20m, NetWeight = 18m, TareWeight = 2m, WeightUnit = "KG", Plant = "3007", StorageLocation = "PA01", PackagingMaterial = pack, HandlingUnitItems = [new() { HandlingUnit = "300010001" }] };
    private const string EndpointInt012 = "https://cpi.exemplo.local/http/pesagem/handling_unit/CreateHUInput/1111/SAP__self.processHandlingUnitPayload";
    private static readonly IReadOnlyList<string> HostsInt012 = ["cpi.exemplo.local"];

    [Fact]
    public async Task Int012_StatusS_UC_SemTipoE_Confirmado()
    {
        HandlerInt012 h = new(HttpStatusCode.OK, """{ "UC_gerada": "300099999", "Status": "S", "_Mensagens": [{ "Id": "1", "Id_Msg": "000", "Tipo": "I", "Texto": "criado" }] }""");
        ResultadoPaleteInt012 r = await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", true, () => h).EnviarPaleteAsync(ReqInt012());
        Assert.Equal(EstadoPaleteInt012.Confirmado, r.Estado);
        Assert.Equal("300099999", r.UcGerada);
        Assert.Equal(1, h.Posts);
        Assert.Equal("criado", r.Mensagens[0].Texto);
    }

    [Fact]
    public async Task Int012_StatusE_Erro()
    {
        HandlerInt012 h = new(HttpStatusCode.OK, """{ "UC_gerada": "", "Status": "E", "_Mensagens": [] }""");
        Assert.Equal(EstadoPaleteInt012.ErroStatus, (await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", true, () => h).EnviarPaleteAsync(ReqInt012())).Estado);
    }

    [Fact]
    public async Task Int012_MensagemTipoE_Erro_MesmoStatusS()
    {
        HandlerInt012 h = new(HttpStatusCode.OK, """{ "UC_gerada": "1", "Status": "S", "_Mensagens": [{ "Tipo": "E", "Texto": "reprovado" }] }""");
        ResultadoPaleteInt012 r = await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", true, () => h).EnviarPaleteAsync(ReqInt012());
        Assert.Equal(EstadoPaleteInt012.ErroStatus, r.Estado);
        Assert.Equal("reprovado", r.Mensagens[0].Texto); // parser lÃª Texto explicitamente
    }

    [Fact]
    public async Task Int012_GateFalse_ZeroHttp()
    {
        HandlerInt012 h = new(HttpStatusCode.OK, "{}");
        ResultadoPaleteInt012 r = await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", false, () => h).EnviarPaleteAsync(ReqInt012());
        Assert.Equal(EstadoPaleteInt012.NaoEnviado, r.Estado); Assert.Equal(0, h.Posts);
    }

    [Fact]
    public async Task Int012_PackagingVazio_ZeroHttp_Pallet01_AceitoSemSubstituir()
    {
        HandlerInt012 hVazio = new(HttpStatusCode.OK, "{}");
        Assert.Equal(EstadoPaleteInt012.NaoEnviado, (await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", true, () => hVazio).EnviarPaleteAsync(ReqInt012(pack: ""))).Estado);
        Assert.Equal(0, hVazio.Posts);

        HandlerInt012 hPallet01 = new(HttpStatusCode.OK, "{}");
        Assert.Equal(EstadoPaleteInt012.ContratoStatusPendente, (await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", true, () => hPallet01).EnviarPaleteAsync(ReqInt012(pack: "PALLET01"))).Estado);
        Assert.Equal(1, hPallet01.Posts);
    }
    [Fact]
    public async Task Int012_Timeout_Indeterminado_UmPost()
    {
        HandlerInt012 h = new(HttpStatusCode.OK, "{}", t: true);
        ResultadoPaleteInt012 r = await new ProdutoAcabadoPaleteInt012Gateway(EndpointInt012, HostsInt012, "u", "p", true, () => h).EnviarPaleteAsync(ReqInt012());
        Assert.Equal(EstadoPaleteInt012.IndeterminadoTimeout, r.Estado); Assert.Equal(1, h.Posts);
    }

    [Fact]
    public void Int012_Factory_FailClosed_PorPadrao()
    {
        ConfiguracaoSap cfg = new() { HandlingUnitBaseUrl = "https://cpi.exemplo.local/base", HostsPermitidos = HostsInt012, Usuario = "u", Senha = "p" };
        IProdutoAcabadoPaleteInt012Gateway gw = FabricaProdutoAcabadoPaleteInt012Gateway.Criar(cfg, () => new HandlerInt012(HttpStatusCode.OK, "{}"));
        Assert.False(gw.EnvioAutorizado); // PalletWriteHabilitado default false â‡’ fail-closed
    }

    [Fact]
    public void Int012_Factory_EndpointPathConfirmado()
        => Assert.EndsWith("/http/pesagem/handling_unit/CreateHUInput/1111/SAP__self.processHandlingUnitPayload",
            FabricaProdutoAcabadoPaleteInt012Gateway.MontarEndpoint("https://cpi.exemplo.local/qualquer"), StringComparison.Ordinal);

    private static string CaminhoProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return Path.Combine(dir, Path.Combine(partes));
    }
}



