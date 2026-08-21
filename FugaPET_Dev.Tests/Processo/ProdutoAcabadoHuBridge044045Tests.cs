using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Dédalo V6 (blockers 2 e 5): ponte HU 045→044 no adapter (autoriza+comprova PRONTA / não re-autoriza / outros
/// estados fail-closed zero POST) e recarga autoritativa do cache na Form após o pipeline.
/// </summary>
public sealed class ProdutoAcabadoHuBridge044045Tests
{
    private const long Codigo = 1, Usuario = 42;
    private const string Terminal = "T1";

    private sealed class FakeHuBridge : IProdutoAcabadoHuBridgeServico
    {
        public bool EnvioAutorizado => true;
        public Queue<StatusIntegracaoCaixa?> Estados { get; } = new();
        public bool AutorizarResultado { get; set; } = true;
        public StatusIntegracaoCaixa EstadoPosEnvio { get; set; } = StatusIntegracaoCaixa.ConfirmadaSap;
        public int Autorizacoes { get; private set; }
        public int Posts { get; private set; }

        private static ProdutoAcabadoCaixa? Caixa(StatusIntegracaoCaixa? e)
            => e is null ? null : new ProdutoAcabadoCaixa { CodigoProdutoAcabadoCaixa = Codigo, StatusIntegracao = e.Value };

        public Task<ProdutoAcabadoCaixa?> ObterPorCodigoAsync(long c, CancellationToken ct = default)
            => Task.FromResult(Caixa(Estados.Count > 0 ? Estados.Dequeue() : null));
        public Task<bool> AutorizarEnvioAsync(long c, long u, string t, CancellationToken ct = default)
        { Autorizacoes++; return Task.FromResult(AutorizarResultado); }
        public Task<ResultadoEnvioHu> EnviarAsync(long c, long u, string t, CancellationToken ct = default)
        { Posts++; return Task.FromResult(ResultadoEnvioHu.Confirmado("HU-TEST", new ProdutoAcabadoCaixa { CodigoProdutoAcabadoCaixa = Codigo, StatusIntegracao = EstadoPosEnvio })); }
    }

    private static Task<StatusIntegracaoCaixa> Enviar(FakeHuBridge b)
        => new ProdutoAcabadoHuEnvioAdapter(b).EnviarHuAsync(Codigo, Usuario, Terminal);

    // §9.1 — início AGUARDANDO: autoriza UMA vez, comprova PRONTA e então envia (claim HU).
    [Fact]
    public async Task Aguardando_AutorizaUmaVez_ComprovaPronta_Envia()
    {
        FakeHuBridge b = new();
        b.Estados.Enqueue(StatusIntegracaoCaixa.AguardandoAutorizacaoSap); // 1ª leitura
        b.Estados.Enqueue(StatusIntegracaoCaixa.ProntaParaEnvio);          // releitura pós-autorização
        StatusIntegracaoCaixa r = await Enviar(b);
        Assert.Equal(1, b.Autorizacoes);
        Assert.Equal(1, b.Posts);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, r);
    }

    // §9.2 — início PRONTA: NÃO autoriza de novo; envia direto.
    [Fact]
    public async Task Pronta_NaoAutorizaNovamente_Envia()
    {
        FakeHuBridge b = new();
        b.Estados.Enqueue(StatusIntegracaoCaixa.ProntaParaEnvio);
        StatusIntegracaoCaixa r = await Enviar(b);
        Assert.Equal(0, b.Autorizacoes);
        Assert.Equal(1, b.Posts);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, r);
    }

    // §9.3 — estado != AGUARDANDO/PRONTA ⇒ zero POST HU.
    [Theory]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada)]
    public async Task OutroEstado_ZeroPostHu(StatusIntegracaoCaixa estado)
    {
        FakeHuBridge b = new();
        b.Estados.Enqueue(estado);
        StatusIntegracaoCaixa r = await Enviar(b);
        Assert.Equal(0, b.Autorizacoes);
        Assert.Equal(0, b.Posts);
        Assert.Equal(estado, r);
    }

    // AGUARDANDO mas autorização não comprova PRONTA ⇒ fail-closed, zero POST.
    [Fact]
    public async Task Aguardando_AutorizacaoNaoComprovaPronta_ZeroPost()
    {
        FakeHuBridge b = new();
        b.Estados.Enqueue(StatusIntegracaoCaixa.AguardandoAutorizacaoSap);
        b.Estados.Enqueue(StatusIntegracaoCaixa.EnviandoSap); // não avançou para PRONTA
        StatusIntegracaoCaixa r = await Enviar(b);
        Assert.Equal(1, b.Autorizacoes);
        Assert.Equal(0, b.Posts);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, r);
    }

    [Fact]
    public async Task CaixaAusente_FailClosed_ZeroPost()
    {
        FakeHuBridge b = new(); // fila vazia ⇒ Obter retorna null
        StatusIntegracaoCaixa r = await Enviar(b);
        Assert.Equal(0, b.Posts);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, r);
    }

    // §9.10/§9.11 — Form recarrega snapshot após pipeline; recarga falha ⇒ não reabilita por cache stale.
    [Fact]
    public void Form_RecarregaSnapshot_AposPipeline_EBloqueiaSeRecargaFalhar()
    {
        string metodo = ExtrairMetodo(LerForm(), "private async Task SolicitarEnvioCaixaPipelineAsync()");
        Assert.Contains("await _controller.ObterCaixaPorCodigoAsync(codigo)", metodo, StringComparison.Ordinal);
        Assert.Contains("SubstituirCaixaNoCache(snapshot)", metodo, StringComparison.Ordinal);
        // recarga null OU exceção ⇒ bloqueia reenvio local (nunca reabilita por objeto stale).
        Assert.Contains("_codigoCaixaEnvioIndeterminado = codigo", metodo, StringComparison.Ordinal);
        Assert.Contains("catch (Exception recarga)", metodo, StringComparison.Ordinal);
    }

    private static string LerForm() => LerProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Assinatura não encontrada: {assinatura}");
        int abre = fonte.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < fonte.Length; i++)
        {
            if (fonte[i] == '{') { profundidade++; }
            else if (fonte[i] == '}') { profundidade--; if (profundidade == 0) { return fonte[inicio..(i + 1)]; } }
        }
        return fonte[inicio..];
    }

    private static string LerProjeto(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        { dir = Directory.GetParent(dir)?.FullName ?? string.Empty; }
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }
}
