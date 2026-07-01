using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class ConsumoMaterialHistoricoTests
{
    [Fact]
    public void FiltroSemParametros_DeveLimitarConsultaA200Registros()
    {
        ConsultaConsumoMaterialFiltro filtro = ConsumoMaterialConsultaServico.NormalizarFiltro(new ConsultaConsumoMaterialFiltro());

        Assert.Equal(200, filtro.Limite);
        Assert.Null(filtro.NumeroOrdem);
        Assert.Null(filtro.StatusLancamento);
    }

    [Fact]
    public void FiltroPorStatus_DeveAceitarSomenteStatusConhecido()
    {
        Assert.Equal("PENDENTE_SAP", ConsumoMaterialConsultaServico.NormalizarFiltro(new ConsultaConsumoMaterialFiltro
        {
            StatusLancamento = "PENDENTE_SAP"
        }).StatusLancamento);

        ArgumentException erro = Assert.Throws<ArgumentException>(() =>
            ConsumoMaterialConsultaServico.NormalizarFiltro(new ConsultaConsumoMaterialFiltro
            {
                StatusLancamento = "DROP TABLE"
            }));
        Assert.Contains(ConsumoMaterialConsultaServico.MensagemStatusInvalido, erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FiltroPorData_DeveNormalizarUtcELimiteMaximo()
    {
        DateTime local = new(2026, 6, 27, 10, 0, 0, DateTimeKind.Local);

        ConsultaConsumoMaterialFiltro filtro = ConsumoMaterialConsultaServico.NormalizarFiltro(new ConsultaConsumoMaterialFiltro
        {
            CriadoDeUtc = local,
            Limite = 5000
        });

        Assert.Equal(DateTimeKind.Utc, filtro.CriadoDeUtc!.Value.Kind);
        Assert.Equal(ConsumoMaterialConsultaServico.LimiteMaximo, filtro.Limite);
    }

    [Fact]
    public void Repositorio_DeveUsarParametrosOrdenarRecentesESomarQuantidade()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ConsumoMaterialRepositorio.cs");

        Assert.Contains("COALESCE(SUM(i.quantidade_consumida_local), 0)", repo, StringComparison.Ordinal);
        Assert.Contains("l.consumo_material_lancamento_criado_em,", repo, StringComparison.Ordinal);
        Assert.Contains("l.consumo_material_lancamento_criado_em >= @criado_de", repo, StringComparison.Ordinal);
        Assert.Contains("l.consumo_material_lancamento_criado_em <= @criado_ate", repo, StringComparison.Ordinal);
        Assert.Contains(
            "ORDER BY l.consumo_material_lancamento_criado_em DESC, l.codigo_consumo_material_lancamento DESC",
            repo,
            StringComparison.Ordinal);
        Assert.DoesNotContain("l.criado_em", repo, StringComparison.Ordinal);
        Assert.Contains("LIMIT @limite", repo, StringComparison.Ordinal);
        Assert.Contains("@numero_ordem", repo, StringComparison.Ordinal);
        Assert.Contains("@status_lancamento", repo, StringComparison.Ordinal);
        Assert.Contains("@criado_de", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"SELECT", repo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("string.Format", repo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Detalhe_DeveCarregarItensEPesagens()
    {
        FakeRepo repo = new()
        {
            Detalhe = LancamentoComPesagem()
        };
        ConsumoMaterialConsultaServico servico = CriarServico(repo);

        DetalheConsumoMaterialLancamento? detalhe = await servico.ObterDetalheCompletoAsync(10);

        Assert.NotNull(detalhe);
        Assert.Single(detalhe!.Itens);
        Assert.Single(detalhe.Pesagens);
    }

    [Fact]
    public void TelaHistorico_NaoDeveEnviarSapBuscarCsrfOuAlterarStatus()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialHistoricoForm.cs");

        Assert.DoesNotContain("EnviarConsumoSap261Async", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarConsumo261Async", form, StringComparison.Ordinal);
        Assert.DoesNotContain("CSRF", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TentarReservarEnvioSapAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarFalhaSapAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("MarcarConsumoConfirmadoSapAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("PATCH", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewTela_DeveSerSomenteParaPendenteENaoEnviarSap()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialHistoricoForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Operacao", "ConsumoMaterialConsultaServico.cs");

        Assert.Contains("ConsumoMaterialConsultaServico.MensagemPreviewSomentePendente", form, StringComparison.Ordinal);
        Assert.Contains("StatusPendenteSap", form, StringComparison.Ordinal);
        Assert.Contains("GerarPreviewSap261Async", form, StringComparison.Ordinal);
        Assert.Contains("string json = preview.PayloadJson;", form, StringComparison.Ordinal);
        Assert.Contains("GerarPreviewSap261Async", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer.Serialize(preview.Payload", form, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Text.Json;", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarConsumoSap261Async", servico, StringComparison.Ordinal);
        Assert.DoesNotContain("TentarReservarEnvioSapAsync", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewPayloadJson_DeveSerODataV2SemNomeDotNet()
    {
        ResultadoPreviewConsumoSap261 preview = new ConsumoMaterialSapPayloadBuilder().MontarPreview261(
            LancamentoComPesagem(),
            new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(preview.Sucesso);
        Assert.Contains("to_MaterialDocumentItem", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"results\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("/Date(", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ToMaterialDocumentItem", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void EntradaProduto_DevePermanecerIntactaSemPatchEGoodsMovementRefDocType()
    {
        string entradaController = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string entradaForm = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("GoodsMovementRefDocType = \"B\"", entradaController, StringComparison.Ordinal);
        Assert.DoesNotContain("PATCH", entradaController, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PATCH", entradaForm, StringComparison.OrdinalIgnoreCase);
    }

    private static ConsumoMaterialConsultaServico CriarServico(FakeRepo repo)
        => new(
            () => repo,
            new ConsumoMaterialServico(new FakeProdOrder(), () => repo));

    private static ConsumoMaterialLancamento LancamentoComPesagem()
        => new()
        {
            Codigo = 10,
            NumeroOrdem = "1000001",
            Centro = "3007",
            MaterialProduzido = "MAT-12345",
            Unidade = "KG",
            StatusLancamento = ConsumoMaterialLancamento.StatusPendenteSap,
            Itens =
            [
                new ConsumoMaterialItem
                {
                    Codigo = 20,
                    NumeroOrdem = "1000001",
                    CodigoMaterial = "MP001",
                    Centro = "3007",
                    DepositoConsumo = "PP01",
                    NumeroReserva = "6676",
                    ItemReserva = "2",
                    Lote = "LOTE-HIST",
                    QuantidadeConsumidaLocal = 1.25m,
                    Unidade = "KG",
                    TipoMovimentoSap = "261",
                    StatusItem = "PENDENTE_SAP",
                    Pesagens =
                    [
                        new ConsumoMaterialPesagem
                        {
                            Codigo = 30,
                            Sequencia = 1,
                            PesoBrutoKg = 2m,
                            PesoTaraKg = 0.75m,
                            PesoLiquidoKg = 1.25m,
                            Origem = "MANUAL",
                            PesadoEm = DateTime.UtcNow
                        }
                    ]
                }
            ]
        };

    private sealed class FakeRepo : IConsumoMaterialRepositorio
    {
        public ConsumoMaterialLancamento? Detalhe { get; init; }

        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(1L);

        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhe);

        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
            ConsultaConsumoMaterialFiltro filtro,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ResumoConsumoMaterialLancamento>>([]);

        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhe);

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, DateTime reservadoEmUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarcarConsumoConfirmadoSapAsync(
            long codigoLancamento,
            string? documentoMaterialSap,
            string? exercicioDocumentoMaterialSap,
            DateTime enviadoSapEmUtc,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProdOrder : IProductionOrderSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoEncontrada());
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
            {
                return diretorio;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
