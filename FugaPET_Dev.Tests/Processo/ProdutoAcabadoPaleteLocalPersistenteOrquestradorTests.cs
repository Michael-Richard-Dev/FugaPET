using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ProdutoAcabadoPaleteLocalPersistenteOrquestradorTests
{
    private const long Usuario = 42;
    private const string Terminal = "TERM-PALETE";

    [Fact]
    public async Task CriarPaleteLocal_ComCaixasElegiveis_PersisteEVinculaEmOrdem()
    {
        FakeOps ops = new() { CodigoCriado = 1234 };
        ProdutoAcabadoPalete palete = Palete([Caixa(1, 101), Caixa(2, 102)], "PACK_REAL_001");

        ResultadoPaletePersistenciaLocal resultado = await new ProdutoAcabadoPaleteLocalPersistenteOrquestrador(ops)
            .CriarAsync(palete, Usuario, Terminal);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1234, resultado.CodigoHuPalete);
        Assert.Equal(1, ops.Criacoes); // UMA chamada atômica (criar + vincular na mesma transação)
        Assert.Equal("PACK_REAL_001", ops.MaterialEmbalagemCriacao);
        Assert.Equal(["vincular:1234:101:1", "vincular:1234:102:2"], ops.Vinculos);
        Assert.Empty(ops.Claims);
        Assert.Empty(ops.PostsVirtuais);
    }

    [Fact]
    public async Task MaterialEmbalagemAusente_BloqueiaAntesDeCriar()
    {
        FakeOps ops = new();
        ProdutoAcabadoPalete palete = Palete([Caixa(1, 101)], string.Empty);

        ResultadoPaletePersistenciaLocal resultado = await new ProdutoAcabadoPaleteLocalPersistenteOrquestrador(ops)
            .CriarAsync(palete, Usuario, Terminal);

        Assert.False(resultado.Sucesso);
        Assert.Equal(0, ops.Criacoes);
        Assert.Empty(ops.Vinculos);
    }

    [Fact]
    public async Task FalhaAoCriar_NaoVinculaNenhumaCaixa()
    {
        FakeOps ops = new() { CodigoCriado = null };

        ResultadoPaletePersistenciaLocal resultado = await new ProdutoAcabadoPaleteLocalPersistenteOrquestrador(ops)
            .CriarAsync(Palete([Caixa(1, 101), Caixa(2, 102)], "PACK_REAL_001"), Usuario, Terminal);

        Assert.False(resultado.Sucesso);
        Assert.Equal(1, ops.Criacoes);
        Assert.Empty(ops.Vinculos);
        Assert.Empty(ops.Claims);
    }

    [Fact]
    public async Task FalhaEmVinculo_NaoInformaSucessoFinalENaoChamaClaimOuPost()
    {
        FakeOps ops = new() { CodigoCriado = 1234, VinculoOk = codigoCaixa => codigoCaixa != 102 };

        ResultadoPaletePersistenciaLocal resultado = await new ProdutoAcabadoPaleteLocalPersistenteOrquestrador(ops)
            .CriarAsync(Palete([Caixa(1, 101), Caixa(2, 102)], "PACK_REAL_001"), Usuario, Terminal);

        Assert.False(resultado.Sucesso);
        Assert.Equal(["vincular:1234:101:1", "vincular:1234:102:2"], ops.Vinculos);
        Assert.Empty(ops.Claims);
        Assert.Empty(ops.PostsVirtuais);
    }

    [Fact]
    public void Validador_RejeitaCaixaNaoConfirmadaSemHuDuplicadaEJaPaletizada()
    {
        Assert.False(ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar("1001", [Caixa(1, 101, status: StatusIntegracaoCaixa.ErroSap)], []).Sucesso);
        Assert.False(ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar("1001", [Caixa(1, 101, hu: string.Empty)], []).Sucesso);
        Assert.False(ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar("1001", [Caixa(1, 101), Caixa(1, 101)], []).Sucesso);
        Assert.False(ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar("1001", [Caixa(1, 101, paleteLocal: "PLT-ANTIGO")], []).Sucesso);
    }

    [Fact]
    public void Validador_RejeitaCaixaJaEmOutroPaleteAtivo()
    {
        ProdutoAcabadoCaixa caixaExistente = Caixa(1, 101);
        ProdutoAcabadoPalete existente = Palete([caixaExistente], "PACK_REAL_001");
        ProdutoAcabadoCaixa novaSelecaoMesmaCaixa = Caixa(1, 101);

        ResultadoPaleteLocal resultado = ProdutoAcabadoPaleteLocalValidador.ValidarEAgrupar(
            "1001",
            [novaSelecaoMesmaCaixa],
            [existente]);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public async Task Controller_MarcaRascunhoEUsaCodigoRetornadoSemInt012()
    {
        FakeStore store = new() { CodigoCriado = 555 };
        ProdutoAcabadoController controller = new(new ProductionOrderSapFake(), configuracaoSap: new ConfiguracaoSap { ProdutoAcabadoPipelineHabilitado = true }, pipelineStore: store);
        ProdutoAcabadoPalete palete = Palete([Caixa(1, 101)], "PACK_REAL_001");

        ResultadoPaletePersistenciaLocal resultado = await controller.CriarPaleteLocalPersistenteAsync(palete, Usuario, Terminal);

        Assert.True(resultado.Sucesso);
        Assert.Equal(555, palete.CodigoHuPalete);
        Assert.Equal("RASCUNHO", palete.StatusSap);
        Assert.Equal(palete.CodigoPaleteLocal, palete.Caixas[0].CodigoPaleteLocal);
        Assert.Empty(store.Claims);
    }

    [Fact]
    public void OrquestradorLocal_NaoContemClaimInt012OuPost()
    {
        string src = File.ReadAllText(CaminhoFonte("Servicos", "Operacao", "ProdutoAcabadoPaleteLocalPersistenteOrquestrador.cs"));
        Assert.DoesNotContain("ClaimEnvioPaleteAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarPaleteAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("POST_FORMACAO", src, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrarSucessoPaleteAsync", src, StringComparison.Ordinal);
    }

    private static ProdutoAcabadoPalete Palete(IReadOnlyList<ProdutoAcabadoCaixa> caixas, string packaging)
        => new()
        {
            CodigoPaleteLocal = "PLT-1001-0001-0002",
            PrimeiraCaixa = caixas.Min(c => c.NumeroCaixa),
            UltimaCaixa = caixas.Max(c => c.NumeroCaixa),
            PesoBrutoKg = caixas.Sum(c => c.PesoBrutoKg),
            PesoLiquidoKg = caixas.Sum(c => c.PesoLiquidoKg),
            TaraKg = caixas.Sum(c => c.TaraKg),
            Plant = "3007",
            StorageLocation = "PP01",
            PackagingMaterial = packaging,
            Caixas = caixas
        };

    private static ProdutoAcabadoCaixa Caixa(
        int numero,
        long codigo,
        StatusIntegracaoCaixa status = StatusIntegracaoCaixa.ConfirmadaSap,
        string hu = "HU-REAL",
        string paleteLocal = "")
        => new()
        {
            CodigoProdutoAcabadoCaixa = codigo,
            NumeroCaixa = numero,
            CodigoCaixaLocal = $"CX-1001-{numero:0000}",
            NumeroOrdemProducao = "1001",
            Material = "2000091",
            Lote = "LOTE1",
            Centro = "3007",
            Deposito = "PP01",
            PesoBrutoKg = 10m,
            PesoLiquidoKg = 9m,
            TaraKg = 1m,
            StatusIntegracao = status,
            HandlingUnitExternalId = hu,
            CodigoPaleteLocal = paleteLocal
        };

    private sealed class ProductionOrderSapFake : IProductionOrderSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoConfigurado("Fake sem consulta SAP."));
    }

    private sealed class FakeStore : FakeOps, IProdutoAcabadoPipelineStore
    {
        private readonly Dictionary<long, ProdutoAcabadoPipelineSnapshot> _snapshots = [];

        public ProdutoAcabadoPipelineSnapshot? Obter(long codigoCaixa)
            => _snapshots.TryGetValue(codigoCaixa, out ProdutoAcabadoPipelineSnapshot? snapshot) ? snapshot : null;

        public void Salvar(ProdutoAcabadoPipelineSnapshot snapshot)
        {
            if (snapshot.CodigoProdutoAcabadoCaixa is long codigo)
            {
                _snapshots[codigo] = snapshot;
            }
        }
    }

    private class FakeOps : IProdutoAcabadoPipeline045Operacoes
    {
        public bool SuportaPersistenciaDefinitiva => true;
        public long? CodigoCriado { get; set; } = 1234;
        public Func<long, bool> VinculoOk { get; set; } = _ => true;
        public int Criacoes { get; private set; }
        public string? MaterialEmbalagemCriacao { get; private set; }
        public List<string> Vinculos { get; } = [];
        public List<string> Claims { get; } = [];
        public List<string> PostsVirtuais { get; } = [];

        // GATE 046-E: o orquestrador agora chama a criação ATÔMICA (uma transação). A simulação registra a
        // criação e os vínculos que a transação teria feito; qualquer vínculo negado ⇒ Bloqueado (rollback).
        public Task<ResultadoCriacaoPaleteLocalAtomica> CriarPaleteComCaixasAsync(
            string materialEmbalagem, string plant, string storageLocation,
            decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg,
            IReadOnlyList<long> codigosCaixas, long usuario, string terminal, CancellationToken ct = default)
        {
            Criacoes++;
            MaterialEmbalagemCriacao = materialEmbalagem;
            if (CodigoCriado is not long codigo || codigo <= 0)
            {
                return Task.FromResult(ResultadoCriacaoPaleteLocalAtomica.Bloqueado("Criação negada (fake)."));
            }

            bool todosOk = true;
            for (int i = 0; i < codigosCaixas.Count; i++)
            {
                Vinculos.Add($"vincular:{codigo}:{codigosCaixas[i]}:{i + 1}");
                if (!VinculoOk(codigosCaixas[i])) { todosOk = false; }
            }

            return Task.FromResult(todosOk
                ? ResultadoCriacaoPaleteLocalAtomica.Ok(codigo)
                : ResultadoCriacaoPaleteLocalAtomica.Bloqueado("Vínculo negado (fake) ⇒ rollback."));
        }

        public Task<long?> CriarPaleteAsync(string codigoPaleteLocal, string plant, string storageLocation, decimal pesoBrutoKg, decimal pesoLiquidoKg, decimal taraKg, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(CodigoCriado);

        public Task<bool> VincularCaixaPaleteAsync(long codigoPalete, long codigoCaixa, int sequencia, long usuario, string terminal, CancellationToken ct = default)
            => Task.FromResult(VinculoOk(codigoCaixa));

        public Task<ResultadoClaim045> ClaimEnvioPaleteAsync(long codigoPalete, string requestJson, string endpoint, long usuario, string terminal, CancellationToken ct = default)
        { Claims.Add($"claim:{codigoPalete}"); return Task.FromResult(ResultadoClaim045.NaoObtido); }

        public Task<bool> IniciarFluxoAsync(long codigo, long usuario, string terminal, string? origem = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> PrepararEtapaAsync(long codigo, string etapa, string payloadJson, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoEtapaAsync(long codigo, string etapa, int http, string materialDocument, string materialDocumentYear, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarErroEtapaAsync(long codigo, string etapa, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> RegistrarTimeoutEtapaAsync(long codigo, string etapa, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimEtapaAsync(long codigo, string etapa, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarReconciliacaoEtapaAsync(long codigo, string etapa, string resultado, int? http, string? materialDocument, string? materialDocumentYear, string responseJson, string? erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> LiberarReprocessamentoEtapaAsync(long codigo, string etapa, string tipoLiberacao, string motivo, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResultadoClaim045> AdquirirRecoveryPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<ResultadoClaim045> ReassumirClaimPaleteAsync(long codigoPalete, long usuario, string terminal, CancellationToken ct = default) => Task.FromResult(ResultadoClaim045.NaoObtido);
        public Task<bool> RegistrarSucessoPaleteAsync(long codigoPalete, int http, string ucGerada, string responseJson, string endpoint, long usuario, string terminal, CancellationToken ct = default) { PostsVirtuais.Add("sucesso"); return Task.FromResult(true); }
        public Task<bool> RegistrarErroPaleteAsync(long codigoPalete, int http, string responseJson, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) { PostsVirtuais.Add("erro"); return Task.FromResult(true); }
        public Task<bool> RegistrarTimeoutPaleteAsync(long codigoPalete, string erro, string endpoint, long usuario, string terminal, CancellationToken ct = default) { PostsVirtuais.Add("timeout"); return Task.FromResult(true); }
        public Task<IReadOnlyList<Linha045>> LerEstadoEtapasAsync(long codigo, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
        public Task<IReadOnlyList<Linha045>> LerEstadoPaleteAsync(long codigoPalete, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<Linha045>)[]);
    }

    private static string CaminhoFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        return Path.Combine(dir, Path.Combine(partes));
    }
}


