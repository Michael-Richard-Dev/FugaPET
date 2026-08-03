using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Testes COMPORTAMENTAIS (não string-search) da caixa individual de Produto Acabado via Handling Unit
/// (API_HANDLINGUNIT / OP_HANDLINGUNIT_0001). Cobrem: finalização local + correlation_id, preview HU
/// sanitizado sem Material Document/movimento 101, uma caixa por vez, gateway não autorizado (sem POST),
/// sucesso/erro controlados via fake, claim atômico/duplo clique e regressão de escopo. Nenhum POST real,
/// nenhum SQL, nenhuma persistência produtiva (o fake em memória é exclusivo do teste).
/// </summary>
public sealed class ProdutoAcabadoHandlingUnitCaixaTests
{
    // ---------- A) Finalização local gera correlation_id, persiste e prepara preview ----------
    [Fact]
    public async Task FinalizarCaixaLocal_DeveGerarCorrelationIdPersistirEPreparPreview()
    {
        RepositorioFake repo = new();
        ProdutoAcabadoController controller = NovoController(repo);

        ResultadoFinalizacaoCaixa resultado = await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX");

        Assert.Equal(CenarioFinalizacaoCaixa.Ok, resultado.Cenario);
        Assert.NotNull(resultado.Caixa);
        Assert.NotEqual(Guid.Empty, resultado.Caixa!.CorrelationId);
        Assert.Equal(StatusIntegracaoCaixa.AguardandoAutorizacaoSap, resultado.Caixa.StatusIntegracao);
        Assert.NotNull(resultado.Caixa.CodigoProdutoAcabadoCaixa);
        Assert.Single(repo.Todas);
        // Preview foi gerado e o request_payload sanitizado ficou persistido.
        Assert.NotNull(resultado.Preview);
        Assert.True(resultado.Preview!.Sucesso);
        Assert.False(resultado.Preview.ContratoSapConfirmado);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Caixa.RequestPayload));
    }

    // ---------- B) Preview NÃO é Material Document / movimento 101 e é sanitizado ----------
    [Fact]
    public void PreviewHu_NaoDeveConterMaterialDocumentOuMovimento101()
    {
        ProdutoAcabadoController controller = NovoController(new RepositorioFake());
        ProdutoAcabadoCaixa caixa = controller.MontarCaixa(
            OrdemValida(), NormaValida(), 1, 2.5m, 0.2m, "MANUAL", materialEmbalagem: "EMB-CX");

        ResultadoPreviewHandlingUnitCaixa preview = controller.GerarPreviewHandlingUnitCaixa(OrdemValida(), caixa);

        Assert.True(preview.Sucesso);
        Assert.Contains("ContratoSapConfirmado", preview.PayloadJsonSanitizado, StringComparison.Ordinal);
        Assert.Contains("false", preview.PayloadJsonSanitizado, StringComparison.Ordinal);
        Assert.Contains(caixa.CorrelationId.ToString(), preview.PayloadJsonSanitizado, StringComparison.Ordinal);
        // Nenhum vestígio do fluxo 101 / Material Document / credenciais.
        Assert.DoesNotContain("MaterialDocument", preview.PayloadJsonSanitizado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GoodsMovement", preview.PayloadJsonSanitizado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", preview.PayloadJsonSanitizado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", preview.PayloadJsonSanitizado, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(preview.PendenciasContrato);
    }

    // ---------- C) §16: material de embalagem da caixa nunca é PALLET01; preview bloqueia se vazio ----------
    [Fact]
    public void MaterialEmbalagemVazio_DeveBloquearPreview()
    {
        ProdutoAcabadoController controller = NovoController(new RepositorioFake());
        // Norma sem MaterialCaixa e sem override ⇒ embalagem vazia.
        ProdutoAcabadoCaixa caixa = controller.MontarCaixa(OrdemValida(), NormaValida(), 1, 2.5m, 0.2m, "MANUAL");
        Assert.NotEqual("PALLET01", caixa.MaterialEmbalagem);

        ResultadoPreviewHandlingUnitCaixa preview = controller.GerarPreviewHandlingUnitCaixa(OrdemValida(), caixa);

        Assert.False(preview.Sucesso);
        Assert.Contains("embalagem", preview.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- D) Uma caixa por vez: segunda finalização bloqueia enquanto a anterior está ativa ----------
    [Fact]
    public async Task UmaCaixaPorVez_DeveBloquearSegundaCaixaComAnteriorAtiva()
    {
        RepositorioFake repo = new();
        ProdutoAcabadoController controller = NovoController(repo);

        ResultadoFinalizacaoCaixa primeira = await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX");
        ResultadoFinalizacaoCaixa segunda = await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 3.0m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX");

        Assert.Equal(CenarioFinalizacaoCaixa.Ok, primeira.Cenario);
        Assert.Equal(CenarioFinalizacaoCaixa.Bloqueada, segunda.Cenario);
        Assert.Single(repo.Todas);
    }

    // ---------- E) Gateway NÃO autorizado (produção): nenhum POST, caixa preservada ----------
    [Fact]
    public async Task GatewayNaoAutorizado_DeveBloquearSemPostEPreservarCaixa()
    {
        RepositorioFake repo = new();
        // Gateway default = ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado (EnvioAutorizado=false).
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapFake(), repositorio: repo,
            huSapServico: new ProdutoAcabadoHandlingUnitSapServicoNaoAutorizado());

        ResultadoFinalizacaoCaixa finalizada = await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX",
            origemMaterialEmbalagem: OrigemMaterialEmbalagemCaixa.Operador);
        long codigo = finalizada.Caixa!.CodigoProdutoAcabadoCaixa!.Value;

        ResultadoEnvioCaixaHu envio = await controller.EnviarCaixaHandlingUnitAsync(codigo);

        Assert.Equal(CenarioEnvioCaixaHu.NaoAutorizado, envio.Cenario);
        Assert.False(finalizada.PodeEnviar);
        Assert.Equal(StatusIntegracaoCaixa.AguardandoAutorizacaoSap, repo.Todas.Single().StatusIntegracao);
        Assert.Equal(0, repo.Reservas); // nenhum claim / nenhum POST
    }

    // ---------- F) Gateway autorizado com SUCESSO: confirma e persiste o HandlingUnitExternalId ----------
    [Fact]
    public async Task GatewayAutorizadoSucesso_DeveConfirmarEPersistirHandlingUnit()
    {
        RepositorioFake repo = new();
        GatewayFake gateway = GatewayFake.Sucesso("HU-99001");
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapFake(), repositorio: repo, huSapServico: gateway);

        ResultadoFinalizacaoCaixa finalizada = await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX");
        long codigo = finalizada.Caixa!.CodigoProdutoAcabadoCaixa!.Value;
        Guid correlationAntes = finalizada.Caixa.CorrelationId;

        ResultadoEnvioCaixaHu envio = await controller.EnviarCaixaHandlingUnitAsync(codigo);

        Assert.Equal(CenarioEnvioCaixaHu.Confirmado, envio.Cenario);
        Assert.Equal("HU-99001", envio.HandlingUnitExternalId);
        ProdutoAcabadoCaixa persistida = repo.Todas.Single();
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, persistida.StatusIntegracao);
        Assert.Equal("HU-99001", persistida.HandlingUnitExternalId);
        Assert.Equal(correlationAntes, persistida.CorrelationId); // identidade preservada
        Assert.Equal(1, gateway.Chamadas); // uma única tentativa
    }

    // ---------- G) Gateway autorizado com ERRO: ERRO_SAP, correlation_id preservado, reprocessável ----------
    [Fact]
    public async Task GatewayAutorizadoErro_DevePreservarIdentidadeEPermitirReprocesso()
    {
        RepositorioFake repo = new();
        GatewayFake gateway = GatewayFake.Erro("Falha simulada 500");
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapFake(), repositorio: repo, huSapServico: gateway);

        ResultadoFinalizacaoCaixa finalizada = await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX");
        long codigo = finalizada.Caixa!.CodigoProdutoAcabadoCaixa!.Value;
        Guid correlationAntes = finalizada.Caixa.CorrelationId;

        ResultadoEnvioCaixaHu envio = await controller.EnviarCaixaHandlingUnitAsync(codigo);

        Assert.Equal(CenarioEnvioCaixaHu.Falha, envio.Cenario);
        ProdutoAcabadoCaixa caixa = repo.Todas.Single();
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, caixa.StatusIntegracao);
        Assert.Equal(correlationAntes, caixa.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(caixa.ErroSanitizado));

        // Reprocessar: cenário PRÓPRIO (não confirmado), mesmo correlation_id, volta para PRONTA_PARA_ENVIO.
        ResultadoEnvioCaixaHu reproc = await controller.ReprocessarCaixaHandlingUnitAsync(codigo);
        Assert.Equal(CenarioEnvioCaixaHu.ProntaParaReenvio, reproc.Cenario);
        Assert.True(reproc.Reprocessavel);
        Assert.False(reproc.Sucesso);            // reprocessamento não é envio confirmado
        Assert.Null(reproc.HandlingUnitExternalId); // HU não confirmada
        Assert.Equal(StatusIntegracaoCaixa.ProntaParaEnvio, caixa.StatusIntegracao);
        Assert.Equal(correlationAntes, caixa.CorrelationId);
    }

    // ---------- H) Caixa CONFIRMADA_SAP não pode ser reenviada (idempotência) ----------
    [Fact]
    public async Task CaixaConfirmada_NaoPodeReenviar()
    {
        RepositorioFake repo = new();
        GatewayFake gateway = GatewayFake.Sucesso("HU-1");
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapFake(), repositorio: repo, huSapServico: gateway);

        long codigo = (await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX"))
            .Caixa!.CodigoProdutoAcabadoCaixa!.Value;

        await controller.EnviarCaixaHandlingUnitAsync(codigo);
        ResultadoEnvioCaixaHu segundo = await controller.EnviarCaixaHandlingUnitAsync(codigo);

        Assert.Equal(CenarioEnvioCaixaHu.Bloqueado, segundo.Cenario);
        Assert.Equal(1, gateway.Chamadas); // não chamou o gateway de novo
    }

    // ---------- I) Claim ATÔMICO: só uma reserva PRONTA_PARA_ENVIO → ENVIANDO_SAP ----------
    [Fact]
    public async Task ClaimAtomico_SoDevePermitirUmaReserva()
    {
        RepositorioFake repo = new();
        ProdutoAcabadoCaixa caixa = new()
        {
            NumeroCaixa = 1,
            CodigoCaixaLocal = "CX-1000909-0001",
            NumeroOrdemProducao = "1000909",
            CorrelationId = Guid.NewGuid(),
            StatusIntegracao = StatusIntegracaoCaixa.ProntaParaEnvio
        };
        ProdutoAcabadoCaixa persistida = await repo.RegistrarCaixaAsync(caixa);
        long codigo = persistida.CodigoProdutoAcabadoCaixa!.Value;

        ProdutoAcabadoCaixa? primeira = await repo.TentarReservarEnvioSapAsync(codigo);
        ProdutoAcabadoCaixa? segunda = await repo.TentarReservarEnvioSapAsync(codigo);

        Assert.NotNull(primeira);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, primeira!.StatusIntegracao); // snapshot reservado
        Assert.Null(segunda);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, persistida.StatusIntegracao);
    }

    // ---------- J) Transições inválidas de status são rejeitadas ----------
    [Fact]
    public void TransicaoInvalida_DeveLancar()
    {
        ProdutoAcabadoCaixa caixa = new()
        {
            NumeroCaixa = 1,
            CorrelationId = Guid.NewGuid(),
            StatusIntegracao = StatusIntegracaoCaixa.EmPesagem
        };

        // EmPesagem → ConfirmadaSap é inválido (pula todo o fluxo).
        Assert.Throws<TransicaoStatusInvalidaException>(() =>
            caixa.TransicionarPara(StatusIntegracaoCaixa.ConfirmadaSap));

        // Caminho válido não lança.
        caixa.TransicionarPara(StatusIntegracaoCaixa.FinalizadaLocal);
        caixa.TransicionarPara(StatusIntegracaoCaixa.PreviewHuGerado);
        Assert.Equal(StatusIntegracaoCaixa.PreviewHuGerado, caixa.StatusIntegracao);
    }

    // ---------- K) Repository DESCONECTADO: sucesso sem TransicaoStatusInvalidaException ----------
    [Fact]
    public async Task RepositorioDesconectado_Sucesso_DeveConfirmarSemTransicaoInvalida()
    {
        RepositorioDesconectadoFake repo = new();
        GatewayFake gateway = GatewayFake.Sucesso("HU-DESC-1");
        ProdutoAcabadoController controller = new(new ProductionOrderSapFake(), repositorio: repo, huSapServico: gateway);

        long codigo = (await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX"))
            .Caixa!.CodigoProdutoAcabadoCaixa!.Value;

        // Nenhuma exceção de transição deve escapar mesmo com objetos desconectados a cada consulta.
        ResultadoEnvioCaixaHu envio = await controller.EnviarCaixaHandlingUnitAsync(codigo);

        Assert.Equal(CenarioEnvioCaixaHu.Confirmado, envio.Cenario);
        Assert.Equal("HU-DESC-1", envio.HandlingUnitExternalId);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, envio.Caixa!.StatusIntegracao); // snapshot reservado confirmado
        // Estado PERSISTIDO final (cópia no repositório) é o correto.
        ProdutoAcabadoCaixa? persistida = await repo.ObterCaixaPorCodigoAsync(codigo);
        Assert.NotNull(persistida);
        Assert.Equal(StatusIntegracaoCaixa.ConfirmadaSap, persistida!.StatusIntegracao);
        Assert.Equal("HU-DESC-1", persistida.HandlingUnitExternalId);
    }

    // ---------- L) Repository DESCONECTADO: erro e duplo clique ----------
    [Fact]
    public async Task RepositorioDesconectado_Erro_DevePersistirErroSapEBloquearDuploClique()
    {
        RepositorioDesconectadoFake repo = new();
        GatewayFake gateway = GatewayFake.Erro("Falha simulada 500");
        ProdutoAcabadoController controller = new(new ProductionOrderSapFake(), repositorio: repo, huSapServico: gateway);

        long codigo = (await controller.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-01", materialEmbalagem: "EMB-CX"))
            .Caixa!.CodigoProdutoAcabadoCaixa!.Value;

        ResultadoEnvioCaixaHu envio = await controller.EnviarCaixaHandlingUnitAsync(codigo);

        Assert.Equal(CenarioEnvioCaixaHu.Falha, envio.Cenario);
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, envio.Caixa!.StatusIntegracao); // EnviandoSap → ErroSap, sem exceção
        ProdutoAcabadoCaixa? persistida = await repo.ObterCaixaPorCodigoAsync(codigo);
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, persistida!.StatusIntegracao);

        // Duplo clique: em ErroSap um novo envio ainda deve funcionar (reprocessa e reenvia); mas duas
        // reservas concorrentes sobre PRONTA_PARA_ENVIO só uma vence.
        await repo.AtualizarPreviewHuAsync(codigo, persistida.RequestPayload ?? "{}", StatusIntegracaoCaixa.ProntaParaEnvio);
        ProdutoAcabadoCaixa? r1 = await repo.TentarReservarEnvioSapAsync(codigo);
        ProdutoAcabadoCaixa? r2 = await repo.TentarReservarEnvioSapAsync(codigo);
        Assert.NotNull(r1);
        Assert.Equal(StatusIntegracaoCaixa.EnviandoSap, r1!.StatusIntegracao);
        Assert.Null(r2);
    }

    [Fact]
    public async Task NumeracaoPersistentePorOp_DeveContinuarAposNovoController()
    {
        RepositorioFake repo = new();
        ProdutoAcabadoController primeiroController = NovoController(repo);

        ResultadoFinalizacaoCaixa primeira = await primeiroController.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.5m, 0.2m, "MANUAL", "TERM-SEQ", materialEmbalagem: "EMB-CX");
        Assert.Equal(1, primeira.Caixa!.NumeroCaixa);
        Assert.Equal("CX-1000909-0001", primeira.Caixa.CodigoCaixaLocal);
        repo.LiberarTerminal(primeira.Caixa.CodigoProdutoAcabadoCaixa!.Value);

        ResultadoFinalizacaoCaixa segunda = await primeiroController.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.6m, 0.2m, "MANUAL", "TERM-SEQ", materialEmbalagem: "EMB-CX");
        Assert.Equal(2, segunda.Caixa!.NumeroCaixa);
        repo.LiberarTerminal(segunda.Caixa.CodigoProdutoAcabadoCaixa!.Value);

        ProdutoAcabadoController novoController = NovoController(repo);
        ResultadoFinalizacaoCaixa terceira = await novoController.FinalizarCaixaLocalAsync(
            OrdemValida(), NormaValida(), 2.7m, 0.2m, "MANUAL", "TERM-SEQ", materialEmbalagem: "EMB-CX");

        Assert.Equal(3, terceira.Caixa!.NumeroCaixa);
        Assert.Equal("CX-1000909-0003", terceira.Caixa.CodigoCaixaLocal);
        Assert.Equal(3, repo.Todas.Select(c => c.CodigoCaixaLocal).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task NumeracaoPersistente_DeveSerIndependentePorOpEAtomicaEmConcorrencia()
    {
        RepositorioFake repo = new();
        ProdutoAcabadoController controller = NovoController(repo);

        ProdutoAcabadoCaixa opA1 = controller.MontarCaixaSemIdentidadeSequencial(
            OrdemValida("1001800"), NormaValida(), 2.5m, 0.2m, "MANUAL", "EMB-CX");
        ProdutoAcabadoCaixa opA2 = controller.MontarCaixaSemIdentidadeSequencial(
            OrdemValida("1001800"), NormaValida(), 2.6m, 0.2m, "MANUAL", "EMB-CX");
        ProdutoAcabadoCaixa opB1 = controller.MontarCaixaSemIdentidadeSequencial(
            OrdemValida("1001900"), NormaValida(), 2.7m, 0.2m, "MANUAL", "EMB-CX");
        Guid correlationA1 = opA1.CorrelationId;

        ProdutoAcabadoCaixa[] persistidas = await Task.WhenAll(
            repo.RegistrarCaixaAsync(opA1),
            repo.RegistrarCaixaAsync(opA2),
            repo.RegistrarCaixaAsync(opB1));

        int[] numerosOpA = persistidas
            .Where(c => c.NumeroOrdemProducao == "1001800")
            .Select(c => c.NumeroCaixa)
            .Order()
            .ToArray();
        Assert.Equal([1, 2], numerosOpA);
        Assert.Equal(1, persistidas.Single(c => c.NumeroOrdemProducao == "1001900").NumeroCaixa);
        Assert.Equal(3, persistidas.Select(c => c.CodigoCaixaLocal).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(correlationA1, persistidas.Single(c => c.CorrelationId == correlationA1).CorrelationId);
    }

    // ================================ Fábricas de dados ================================
    private static ProdutoAcabadoController NovoController(IProdutoAcabadoRepositorio repo)
        => new(new ProductionOrderSapFake(), repositorio: repo);

    private static ProdutoAcabadoOrdem OrdemValida(string numeroOrdem = "1000909")
        => new()
        {
            NumeroOrdem = numeroOrdem,
            MaterialProduzido = "3500024",
            Centro = "3007",
            DepositoDestino = "PA01",
            QuantidadePlanejada = 10m,
            QuantidadePendente = 8m,
            Unidade = "KG",
            Lote = "L001",
            ItemOrdem = "0001",
            Liberada = true
        };

    private static ProdutoAcabadoNormaEmbalagem NormaValida()
        => new()
        {
            Material = "3500024",
            PackagingInstruction = "N001",
            QuantidadeProdutosPorCaixa = 12,
            Unidade = "UN"
        };

    // ================================ Fakes de teste ================================
    private sealed class ProductionOrderSapFake : IProductionOrderSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.Encontrada(new OrdemProducaoSap
            {
                NumeroOrdem = "1000909",
                MaterialProduzido = "3500024",
                Centro = "3007",
                Deposito = "PA01",
                QuantidadePrevista = 10m,
                Unidade = "KG",
                Lote = "L001",
                Liberada = true
            }));
    }

    /// <summary>Repositório em memória EXCLUSIVO de teste (não é persistência produtiva). Claim atômico real.</summary>
    private sealed class RepositorioFake : IProdutoAcabadoRepositorio
    {
        private readonly object _lock = new();
        private readonly Dictionary<long, ProdutoAcabadoCaixa> _porCodigo = [];
        private readonly Dictionary<string, int> _sequenciaPorOp = new(StringComparer.OrdinalIgnoreCase);
        private long _sequencia;

        public IReadOnlyList<ProdutoAcabadoCaixa> Todas => [.. _porCodigo.Values];
        public int Reservas { get; private set; }

        public void LiberarTerminal(long codigo)
        {
            lock (_lock)
            {
                _porCodigo[codigo].StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap;
            }
        }

        public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                string op = caixa.NumeroOrdemProducao.Trim();
                int numeroCaixa = _sequenciaPorOp.TryGetValue(op, out int atual) ? atual + 1 : 1;
                _sequenciaPorOp[op] = numeroCaixa;
                caixa.NumeroCaixa = numeroCaixa;
                caixa.CodigoCaixaLocal = $"CX-{op}-{numeroCaixa:0000}";
                caixa.CodigoProdutoAcabadoCaixa = ++_sequencia;
                _porCodigo[caixa.CodigoProdutoAcabadoCaixa.Value] = caixa;
                return Task.FromResult(caixa);
            }
        }

        public Task<ProdutoAcabadoCaixa?> ObterCaixaAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa? ativa = _porCodigo.Values.FirstOrDefault(c =>
                    string.Equals(c.Terminal, terminal, StringComparison.OrdinalIgnoreCase)
                    && c.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap
                    && c.StatusIntegracao != StatusIntegracaoCaixa.Cancelada);
                return Task.FromResult(ativa);
            }
        }

        public Task<ProdutoAcabadoCaixa?> ObterCaixaPorCodigoAsync(long codigo, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _porCodigo.TryGetValue(codigo, out ProdutoAcabadoCaixa? caixa);
                return Task.FromResult(caixa);
            }
        }

        public Task AtualizarPreviewHuAsync(long codigo, string requestPayloadSanitizado, StatusIntegracaoCaixa status, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa caixa = _porCodigo[codigo];
                caixa.RequestPayload = requestPayloadSanitizado;
                caixa.StatusIntegracao = status;
                caixa.AtualizadoEm = DateTimeOffset.Now;
                return Task.CompletedTask;
            }
        }

        public Task<ProdutoAcabadoCaixa?> TentarReservarEnvioSapAsync(long codigo, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa caixa = _porCodigo[codigo];
                if (caixa.StatusIntegracao != StatusIntegracaoCaixa.ProntaParaEnvio)
                {
                    return Task.FromResult<ProdutoAcabadoCaixa?>(null);
                }

                caixa.StatusIntegracao = StatusIntegracaoCaixa.EnviandoSap;
                Reservas++;
                return Task.FromResult<ProdutoAcabadoCaixa?>(caixa);
            }
        }

        public Task RegistrarSucessoHuAsync(long codigo, string handlingUnitExternalId, string responsePayloadSanitizado, int? httpStatus, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa caixa = _porCodigo[codigo];
                caixa.HandlingUnitExternalId = handlingUnitExternalId;
                caixa.ResponsePayload = responsePayloadSanitizado;
                caixa.HttpStatus = httpStatus;
                caixa.ConfirmadoSapEm = DateTimeOffset.Now;
                caixa.StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap;
                return Task.CompletedTask;
            }
        }

        public Task RegistrarErroHuAsync(long codigo, string erroSanitizado, string responsePayloadSanitizado, int? httpStatus, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa caixa = _porCodigo[codigo];
                caixa.ErroSanitizado = erroSanitizado;
                caixa.ResponsePayload = responsePayloadSanitizado;
                caixa.HttpStatus = httpStatus;
                caixa.StatusIntegracao = StatusIntegracaoCaixa.ErroSap;
                return Task.CompletedTask;
            }
        }

        public Task CancelarCaixaAsync(long codigo, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _porCodigo[codigo].StatusIntegracao = StatusIntegracaoCaixa.Cancelada;
                return Task.CompletedTask;
            }
        }
    }

    /// <summary>Gateway HU controlado de teste (AUTORIZADO). Uma tentativa por chamada; sucesso/erro sob controle.</summary>
    private sealed class GatewayFake : IProdutoAcabadoHandlingUnitSapServico
    {
        private readonly bool _sucesso;
        private readonly string _handlingUnitExternalId;
        private readonly string _mensagem;

        private GatewayFake(bool sucesso, string handlingUnitExternalId, string mensagem)
        {
            _sucesso = sucesso;
            _handlingUnitExternalId = handlingUnitExternalId;
            _mensagem = mensagem;
        }

        public static GatewayFake Sucesso(string handlingUnitExternalId) => new(true, handlingUnitExternalId, string.Empty);
        public static GatewayFake Erro(string mensagem) => new(false, string.Empty, mensagem);

        public bool EnvioAutorizado => true;
        public int Chamadas { get; private set; }

        public Task<ResultadoCriacaoHandlingUnitCaixa> CriarHandlingUnitCaixaAsync(
            ProdutoAcabadoCaixaParaEnvio caixa, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(new ResultadoCriacaoHandlingUnitCaixa
            {
                Sucesso = _sucesso,
                HandlingUnitExternalId = _sucesso ? _handlingUnitExternalId : null,
                HttpStatus = _sucesso ? 201 : 500,
                ResponsePayloadSanitizado = "{ \"_teste\": true }",
                MensagemSanitizada = _mensagem,
                CorrelationId = caixa.CorrelationId
            });
        }
    }

    /// <summary>
    /// Repositório de teste que simula um banco REAL desconectado: guarda CÓPIAS e devolve NOVAS cópias a
    /// cada consulta/claim — nunca compartilha referência com o Controller. Assim, se o Controller confiasse
    /// no objeto carregado antes do claim, uma TransicaoStatusInvalidaException apareceria. O claim faz o
    /// UPDATE condicional PRONTA_PARA_ENVIO → ENVIANDO_SAP e retorna o snapshot atualizado (ou null).
    /// </summary>
    private sealed class RepositorioDesconectadoFake : IProdutoAcabadoRepositorio
    {
        private readonly object _lock = new();
        private readonly Dictionary<long, ProdutoAcabadoCaixa> _porCodigo = [];
        private readonly Dictionary<string, int> _sequenciaPorOp = new(StringComparer.OrdinalIgnoreCase);
        private long _sequencia;

        public Task<ProdutoAcabadoCaixa> RegistrarCaixaAsync(ProdutoAcabadoCaixa caixa, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                string op = caixa.NumeroOrdemProducao.Trim();
                int numeroCaixa = _sequenciaPorOp.TryGetValue(op, out int atual) ? atual + 1 : 1;
                _sequenciaPorOp[op] = numeroCaixa;
                caixa.NumeroCaixa = numeroCaixa;
                caixa.CodigoCaixaLocal = $"CX-{op}-{numeroCaixa:0000}";
                caixa.CodigoProdutoAcabadoCaixa = ++_sequencia;
                _porCodigo[caixa.CodigoProdutoAcabadoCaixa.Value] = Clonar(caixa);
                return Task.FromResult(Clonar(_porCodigo[caixa.CodigoProdutoAcabadoCaixa.Value]));
            }
        }

        public Task<ProdutoAcabadoCaixa?> ObterCaixaAtivaPorTerminalAsync(string terminal, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa? ativa = _porCodigo.Values.FirstOrDefault(c =>
                    string.Equals(c.Terminal, terminal, StringComparison.OrdinalIgnoreCase)
                    && c.StatusIntegracao != StatusIntegracaoCaixa.ConfirmadaSap
                    && c.StatusIntegracao != StatusIntegracaoCaixa.Cancelada);
                return Task.FromResult(ativa is null ? null : Clonar(ativa));
            }
        }

        public Task<ProdutoAcabadoCaixa?> ObterCaixaPorCodigoAsync(long codigo, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_porCodigo.TryGetValue(codigo, out ProdutoAcabadoCaixa? c) ? Clonar(c) : null);
            }
        }

        public Task AtualizarPreviewHuAsync(long codigo, string requestPayloadSanitizado, StatusIntegracaoCaixa status, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa c = _porCodigo[codigo];
                c.RequestPayload = requestPayloadSanitizado;
                c.StatusIntegracao = status;
                c.AtualizadoEm = DateTimeOffset.Now;
                return Task.CompletedTask;
            }
        }

        public Task<ProdutoAcabadoCaixa?> TentarReservarEnvioSapAsync(long codigo, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa c = _porCodigo[codigo];
                if (c.StatusIntegracao != StatusIntegracaoCaixa.ProntaParaEnvio)
                {
                    return Task.FromResult<ProdutoAcabadoCaixa?>(null);
                }

                c.StatusIntegracao = StatusIntegracaoCaixa.EnviandoSap;
                return Task.FromResult<ProdutoAcabadoCaixa?>(Clonar(c)); // snapshot desconectado
            }
        }

        public Task RegistrarSucessoHuAsync(long codigo, string handlingUnitExternalId, string responsePayloadSanitizado, int? httpStatus, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa c = _porCodigo[codigo];
                c.HandlingUnitExternalId = handlingUnitExternalId;
                c.ResponsePayload = responsePayloadSanitizado;
                c.HttpStatus = httpStatus;
                c.ConfirmadoSapEm = DateTimeOffset.Now;
                c.StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap;
                return Task.CompletedTask;
            }
        }

        public Task RegistrarErroHuAsync(long codigo, string erroSanitizado, string responsePayloadSanitizado, int? httpStatus, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                ProdutoAcabadoCaixa c = _porCodigo[codigo];
                c.ErroSanitizado = erroSanitizado;
                c.ResponsePayload = responsePayloadSanitizado;
                c.HttpStatus = httpStatus;
                c.StatusIntegracao = StatusIntegracaoCaixa.ErroSap;
                return Task.CompletedTask;
            }
        }

        public Task CancelarCaixaAsync(long codigo, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _porCodigo[codigo].StatusIntegracao = StatusIntegracaoCaixa.Cancelada;
                return Task.CompletedTask;
            }
        }

        private static ProdutoAcabadoCaixa Clonar(ProdutoAcabadoCaixa o)
            => new()
            {
                NumeroCaixa = o.NumeroCaixa,
                CodigoCaixaLocal = o.CodigoCaixaLocal,
                NumeroOrdemProducao = o.NumeroOrdemProducao,
                ItemOrdemProducao = o.ItemOrdemProducao,
                Material = o.Material,
                Lote = o.Lote,
                Centro = o.Centro,
                Deposito = o.Deposito,
                PesoBrutoKg = o.PesoBrutoKg,
                TaraKg = o.TaraKg,
                PesoLiquidoKg = o.PesoLiquidoKg,
                UnidadePeso = o.UnidadePeso,
                QuantidadeProdutos = o.QuantidadeProdutos,
                UnidadeQuantidade = o.UnidadeQuantidade,
                OrigemPesagem = o.OrigemPesagem,
                CorrelationId = o.CorrelationId,
                CriadoEm = o.CriadoEm,
                CodigoUsuario = o.CodigoUsuario,
                Terminal = o.Terminal,
                CodigoProdutoAcabadoCaixa = o.CodigoProdutoAcabadoCaixa,
                MaterialEmbalagem = o.MaterialEmbalagem,
                OrigemMaterialEmbalagem = o.OrigemMaterialEmbalagem,
                StatusIntegracao = o.StatusIntegracao,
                HandlingUnitExternalId = o.HandlingUnitExternalId,
                RequestPayload = o.RequestPayload,
                ResponsePayload = o.ResponsePayload,
                ErroSanitizado = o.ErroSanitizado,
                HttpStatus = o.HttpStatus,
                AtualizadoEm = o.AtualizadoEm,
                EnviadoSapEm = o.EnviadoSapEm,
                ConfirmadoSapEm = o.ConfirmadoSapEm,
                CodigoPaleteLocal = o.CodigoPaleteLocal,
                HandlingUnitCaixa = o.HandlingUnitCaixa
            };
    }
}
