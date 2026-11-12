using System.Net;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class ProdutoAcabadoHuPostUnicoHml2Tests
{
    private const string Host = "sap-hml2.exemplo.local";
    private static readonly Uri BaseUri = new($"https://{Host}/sap/opu/odata4/sap/api_handlingunit/srvd_a2x/sap/handlingunit/0001/");

    [Fact]
    public void Hml2_SemChaveDedicada_PermaneceBloqueado()
    {
        AvaliacaoGateHuHml2 resultado = FabricaProdutoAcabadoHandlingUnitSapServico.AvaliarGate(null, false, "GATE-1");
        Assert.False(resultado.Autorizado);
    }

    [Fact]
    public void Hml2_ChaveDedicadaTrue_EscritaGlobalFalse_Autoriza()
    {
        AvaliacaoGateHuHml2 resultado = FabricaProdutoAcabadoHandlingUnitSapServico.AvaliarGate("true", false, "GATE-1");
        Assert.True(resultado.Autorizado);
        Assert.Equal("GATE-1", resultado.GateId);
    }

    [Fact]
    public void Hml2_EscritaGlobalTrue_BloqueiaMesmoComChaveDedicada()
    {
        AvaliacaoGateHuHml2 resultado = FabricaProdutoAcabadoHandlingUnitSapServico.AvaliarGate("true", true, "GATE-1");
        Assert.False(resultado.Autorizado);
        Assert.Contains("incompatível", resultado.MensagemSanitizada, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SinglePost_GateArquivoPersisteAposNovoProcessoLogico()
    {
        string raiz = CriarDiretorioTemporario();
        try
        {
            PostUnicoHuGateArquivo primeiro = new("HML2-PERSISTENCIA", raiz);
            Assert.True(primeiro.TentarConsumir().ConsumidoAgora);

            PostUnicoHuGateArquivo aposRestart = new("HML2-PERSISTENCIA", raiz);
            Assert.True(aposRestart.Consumido);
            Assert.False(aposRestart.Disponivel);
            Assert.False(aposRestart.TentarConsumir().ConsumidoAgora);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task SinglePost_ConsumoConcorrente_TemExatamenteUmVencedor()
    {
        string raiz = CriarDiretorioTemporario();
        try
        {
            PostUnicoHuGateArquivo gate = new("HML2-CONCORRENCIA", raiz);
            ResultadoConsumoPostUnicoHu[] resultados = await Task.WhenAll(
                Enumerable.Range(0, 12).Select(_ => Task.Run(gate.TentarConsumir)));
            Assert.Single(resultados, resultado => resultado.ConsumidoAgora);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Fact]
    public async Task SinglePost_PrimeiroAlcancaHandler_SegundoNaoEmiteHttpAdicional()
    {
        string raiz = CriarDiretorioTemporario();
        try
        {
            HandlerHml2 handler = HandlerComStatus(HttpStatusCode.Created);
            PostUnicoHuGateArquivo gate = new("HML2-POST-UNICO", raiz);
            ProdutoAcabadoHandlingUnitSapGateway primeiro = CriarGateway(handler, gate);

            ResultadoPostHandlingUnit resultado1 = await primeiro.CriarHandlingUnitCaixaAsync(new HandlingUnitCaixaRequest(), "HandlingUnit");
            int requisicoesDepoisPrimeiro = handler.Metodos.Count;

            ProdutoAcabadoHandlingUnitSapGateway aposRestart = CriarGateway(handler, new PostUnicoHuGateArquivo("HML2-POST-UNICO", raiz));
            ResultadoPostHandlingUnit resultado2 = await aposRestart.CriarHandlingUnitCaixaAsync(new HandlingUnitCaixaRequest(), "HandlingUnit");

            Assert.Equal(CenarioPostHandlingUnit.Confirmado, resultado1.Cenario);
            Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, resultado2.Cenario);
            Assert.Equal(2, requisicoesDepoisPrimeiro);
            Assert.Equal(requisicoesDepoisPrimeiro, handler.Metodos.Count);
            Assert.Single(handler.Metodos, metodo => metodo == HttpMethod.Post);
        }
        finally
        {
            Directory.Delete(raiz, recursive: true);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, CenarioPostHandlingUnit.NaoAutorizado)]
    [InlineData(HttpStatusCode.Forbidden, CenarioPostHandlingUnit.NaoAutorizado)]
    [InlineData(HttpStatusCode.BadRequest, CenarioPostHandlingUnit.ErroDefinitivo)]
    [InlineData(HttpStatusCode.InternalServerError, CenarioPostHandlingUnit.ErroDefinitivo)]
    public async Task Hml2_QualquerHttpDeFalha_NaoEhReprocessavel(
        HttpStatusCode status,
        CenarioPostHandlingUnit cenario)
    {
        HandlerHml2 handler = HandlerComStatus(status);
        ResultadoPostHandlingUnit resultado = await CriarGateway(handler, new GateMemoria()).CriarHandlingUnitCaixaAsync(
            new HandlingUnitCaixaRequest(), "HandlingUnit");

        Assert.Equal(cenario, resultado.Cenario);
        Assert.False(resultado.PodeReprocessar);
        Assert.Single(handler.Metodos, metodo => metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task Hml2_OperationCanceledAposInicioPost_ViraIndeterminado()
    {
        HandlerHml2 handler = new((request, _) => request.Method == HttpMethod.Get
            ? Task.FromResult(RespostaCsrf())
            : throw new TaskCanceledException("timeout sintético após início do POST"));

        ResultadoPostHandlingUnit resultado = await CriarGateway(handler, new GateMemoria()).CriarHandlingUnitCaixaAsync(
            new HandlingUnitCaixaRequest(), "HandlingUnit");

        Assert.Equal(CenarioPostHandlingUnit.Timeout, resultado.Cenario);
        Assert.False(resultado.PodeReprocessar);
        Assert.Single(handler.Metodos, metodo => metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task Hml2_HttpRequestExceptionAposConsumo_ViraIndeterminado()
    {
        HandlerHml2 handler = new((request, _) => request.Method == HttpMethod.Get
            ? Task.FromResult(RespostaCsrf())
            : throw new HttpRequestException("falha sintética"));

        ResultadoPostHandlingUnit resultado = await CriarGateway(handler, new GateMemoria()).CriarHandlingUnitCaixaAsync(
            new HandlingUnitCaixaRequest(), "HandlingUnit");

        Assert.Equal(CenarioPostHandlingUnit.Timeout, resultado.Cenario);
        Assert.False(resultado.PodeReprocessar);
        Assert.Single(handler.Metodos, metodo => metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task Hml2_SemCsrfValido_NaoConsomeGateENaoEmitePost()
    {
        GateMemoria gate = new();
        HandlerHml2 handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        ResultadoPostHandlingUnit resultado = await CriarGateway(handler, gate).CriarHandlingUnitCaixaAsync(
            new HandlingUnitCaixaRequest(), "HandlingUnit");

        Assert.Equal(CenarioPostHandlingUnit.NaoEnviado, resultado.Cenario);
        Assert.False(gate.Consumido);
        Assert.Single(handler.Metodos);
        Assert.Equal(HttpMethod.Get, handler.Metodos[0]);
    }

    [Fact]
    public void Hml2_TravaInterface_BloqueiaDuploCliqueSemReset()
    {
        ControlePostUnicoHuInterface controle = new();
        Assert.True(controle.TentarIniciar());
        Assert.False(controle.TentarIniciar());
        Assert.True(controle.Iniciado);
    }

    [Fact]
    public void Hml2_BotaoSoHabilitaParaCaixaPersistidaElegivelEAutorizada()
    {
        ProdutoAcabadoCaixa caixa = new()
        {
            CodigoProdutoAcabadoCaixa = 10,
            StatusIntegracao = StatusIntegracaoCaixa.AguardandoAutorizacaoSap
        };

        Assert.True(ProcessoProdutoAcabadoForm.PodeHabilitarEnvioHuHml2(caixa, true, false, false, false));
        Assert.False(ProcessoProdutoAcabadoForm.PodeHabilitarEnvioHuHml2(caixa, false, false, false, false));
        Assert.False(ProcessoProdutoAcabadoForm.PodeHabilitarEnvioHuHml2(caixa, true, true, false, false));
        Assert.False(ProcessoProdutoAcabadoForm.PodeHabilitarEnvioHuHml2(caixa, true, false, true, false));
        caixa.CodigoProdutoAcabadoCaixa = null;
        Assert.False(ProcessoProdutoAcabadoForm.PodeHabilitarEnvioHuHml2(caixa, true, false, false, false));
    }

    [Fact]
    public void Hml2_FormTemConfirmacaoEUmaUnicaDelegacaoDeEnvio()
    {
        string form = File.ReadAllText(CaminhoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));
        Assert.Contains("TESTE HML — POST ÚNICO DE HU", form, StringComparison.Ordinal);
        Assert.Contains("AutorizarEnvioCaixaAsync", form, StringComparison.Ordinal);
        Assert.Contains("DiagnosticarProntidaoEnvioCaixaAsync", form, StringComparison.Ordinal);
        Assert.Equal(1, Contar(form, "_controller.EnviarCaixaHandlingUnitAsync("));
        Assert.DoesNotContain("ReprocessarCaixaHandlingUnitAsync", ExtrairMetodo(form, "private async Task EnviarCaixaSapHml2Async()"), StringComparison.Ordinal);
    }

    private static ProdutoAcabadoHandlingUnitSapGateway CriarGateway(HandlerHml2 handler, IPostUnicoHuGate gate)
        => new(
            BaseUri,
            "USUARIO_TESTE",
            "SEGREDO_TESTE",
            envioAutorizado: true,
            fabricaHandler: () => handler,
            timeout: TimeSpan.FromSeconds(1),
            conectividadeReadOnlyAutorizada: true,
            hostsPermitidos: [Host],
            sapClient: "110",
            postUnicoGate: gate);

    private static HandlerHml2 HandlerComStatus(HttpStatusCode status)
        => new((request, _) => Task.FromResult(request.Method == HttpMethod.Get
            ? RespostaCsrf()
            : new HttpResponseMessage(status)
            {
                Content = new StringContent(status == HttpStatusCode.Created
                    ? "{\"HandlingUnitExternalID\":\"HU-HML2-TESTE\",\"Warehouse\":\"\"}"
                    : "{}")
            }));

    private static HttpResponseMessage RespostaCsrf()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("X-CSRF-Token", "TOKEN_SINTETICO");
        response.Headers.TryAddWithoutValidation("Set-Cookie", "COOKIE_SINTETICO");
        return response;
    }

    private static string CriarDiretorioTemporario()
    {
        string caminho = Path.Combine(Path.GetTempPath(), "FugaPET-HML2-Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(caminho);
        return caminho;
    }

    private static int Contar(string texto, string trecho)
        => (texto.Length - texto.Replace(trecho, string.Empty, StringComparison.Ordinal).Length) / trecho.Length;

    private static string ExtrairMetodo(string arquivo, string assinatura)
    {
        int inicio = arquivo.IndexOf(assinatura, StringComparison.Ordinal);
        return inicio < 0 ? string.Empty : arquivo[inicio..Math.Min(arquivo.Length, inicio + 9000)];
    }

    private static string CaminhoProjeto(params string[] partes)
    {
        string diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio) && !File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
        {
            diretorio = Directory.GetParent(diretorio)?.FullName ?? string.Empty;
        }

        return Path.Combine(diretorio, Path.Combine(partes));
    }

    private sealed class GateMemoria : IPostUnicoHuGate
    {
        private int _consumido;
        public string GateId => "HML2-MEMORIA";
        public bool Consumido => Volatile.Read(ref _consumido) == 1;
        public bool Disponivel => !Consumido;

        public ResultadoConsumoPostUnicoHu TentarConsumir()
            => Interlocked.CompareExchange(ref _consumido, 1, 0) == 0
                ? new ResultadoConsumoPostUnicoHu(true, "Consumido.")
                : new ResultadoConsumoPostUnicoHu(false, "Já consumido.");
    }

    private sealed class HandlerHml2(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<HttpMethod> Metodos { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodos.Add(request.Method);
            return responder(request, cancellationToken);
        }
    }
}
