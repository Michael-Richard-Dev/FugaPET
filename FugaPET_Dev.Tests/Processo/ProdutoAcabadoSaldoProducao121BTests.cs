using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ProdutoAcabadoSaldoProducao121BTests
{
    [Fact]
    public void Mapper_ProductionUnitAusente_NaoInventaKgParaAbatimento()
    {
        var metodo = typeof(FugaPET_Dev.Controle.Processo.ProdutoAcabadoController).GetMethod(
            "MapearOrdem", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var origem = new FugaPET_Dev.Modelo.IntegracaoSap.OrdemProducaoSap();
        var ordem = (ProdutoAcabadoOrdem)metodo.Invoke(null, [origem])!;
        Assert.Equal(string.Empty, ordem.Unidade);
        Assert.Equal(60m, Saldo(60m, ordem.Unidade, Caixa(unidade: "KG")));
    }

    [Fact]
    public void ConfirmadaSap_NaoAbateRecebimentoJaConsideradoNaBase()
    {
        var caixa = new ProdutoAcabadoCaixa
        {
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
            QuantidadeProdutos = 10,
            UnidadeQuantidade = "UN",
            PesoLiquidoKg = 14.350m
        };

        Assert.Equal(60m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(100m - 40m, [caixa], "UN"));
    }

    [Fact]
    public void FinalizadaLocal_AbateQuantidadeProdutosENaoPesoFisico()
    {
        var caixa = new ProdutoAcabadoCaixa
        {
            StatusIntegracao = StatusIntegracaoCaixa.FinalizadaLocal,
            QuantidadeProdutos = 8,
            UnidadeQuantidade = "UN",
            PesoLiquidoKg = 14.350m
        };

        Assert.Equal(9008m, CalculoSaldoProdutoAcabado.SaldoPendenteExibido(9016m, [caixa], "UN"));
        Assert.Equal(14.350m, caixa.PesoLiquidoKg);
    }

    [Theory]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal)]
    [InlineData(StatusIntegracaoCaixa.PreviewHuGerado)]
    [InlineData(StatusIntegracaoCaixa.AguardandoAutorizacaoSap)]
    [InlineData(StatusIntegracaoCaixa.ProntaParaEnvio)]
    public void EstadoElegivel_AbateOitoUnidades(StatusIntegracaoCaixa status)
        => Assert.Equal(9008m, Saldo(9016m, "UN", Caixa(status)));

    [Theory]
    [InlineData(StatusIntegracaoCaixa.EmPesagem)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout)]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap)]
    [InlineData(StatusIntegracaoCaixa.Cancelada)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada)]
    [InlineData((StatusIntegracaoCaixa)999)]
    public void EstadoExcluido_NaoAbate(StatusIntegracaoCaixa status)
        => Assert.Equal(9016m, Saldo(9016m, "UN", Caixa(status)));

    [Theory]
    [InlineData("UN", "UN", 52)]
    [InlineData("UN", " un ", 52)]
    [InlineData("kg", "KG", 52)]
    [InlineData(" to ", "TO", 52)]
    [InlineData("KGM", "KGM", 52)]
    [InlineData("UN", "KG", 60)]
    [InlineData("KG", "KGM", 60)]
    [InlineData("UN", "PCS", 60)]
    [InlineData(null, "UN", 60)]
    [InlineData("", "UN", 60)]
    [InlineData(" ", "UN", 60)]
    [InlineData("UN", null, 60)]
    [InlineData("UN", "", 60)]
    [InlineData("UN", " ", 60)]
    [InlineData(null, null, 60)]
    public void Unidade_NormalizaSomenteEspacosECaixaSemConversao(string? op, string? unidadeCaixa, int esperado)
        => Assert.Equal((decimal)esperado, Saldo(60m, op, Caixa(unidade: unidadeCaixa)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuantidadeAusenteOuInvalida_NaoUsaPesoComoFallback(int quantidade)
        => Assert.Equal(60m, Saldo(60m, "UN", Caixa(quantidade: quantidade)));

    [Fact]
    public void DuasCaixasLocais_AbatemDezesseisUnidades()
        => Assert.Equal(9000m, Saldo(9016m, "UN", Caixa(), Caixa()));

    [Fact]
    public void ComposicaoLocal_AbateSomenteQuinzeDaBaseSapSessenta()
        => Assert.Equal(45m, Saldo(Math.Max(100m - 40m, 0m), "UN",
            Caixa(StatusIntegracaoCaixa.FinalizadaLocal, 10),
            Caixa(StatusIntegracaoCaixa.PreviewHuGerado, 5),
            Caixa(StatusIntegracaoCaixa.ConfirmadaSap, 8),
            Caixa(StatusIntegracaoCaixa.EmPesagem, 7)));

    [Theory]
    [InlineData(5, 0)]
    [InlineData(-1, 0)]
    public void ClampZeroPermanece(int pendente, int esperado)
        => Assert.Equal((decimal)esperado, Saldo(pendente, "UN", Caixa()));

    [Fact]
    public void UnidadeIncompativel_NaoImpedeAbatimentoDasOutrasCaixasValidas()
        => Assert.Equal(52m, Saldo(60m, "UN", Caixa(), Caixa(unidade: "KG")));

    [Fact]
    public void CampoERodape_UsamMesmaRegraCentral()
    {
        string diretorio = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj")))
        {
            diretorio = Directory.GetParent(diretorio)?.FullName
                ?? throw new InvalidOperationException("Projeto nao encontrado.");
        }

        string fonte = File.ReadAllText(Path.Combine(diretorio, "Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("CalculoSaldoProdutoAcabado.SaldoPendenteExibido(pendente, _caixasPesadas, UnidadeProducaoAtual)", fonte);
        Assert.Contains("classificationDateTextBox.Text =\n            FormatarQuantidadeProducao(CalcularSaldoPendenteExibido(), UnidadeProducaoAtual)", fonte);
        Assert.Contains("packagesTotalLabel.Text = _ordemAtual?.QuantidadePendente is not null\n            ? $\"de {FormatarQuantidadeProducao(CalcularSaldoPendenteExibido(), UnidadeProducaoAtual)}\"", fonte);
        Assert.DoesNotContain("FormatarQuantidadeProducao(pendentePeso", fonte);
        Assert.DoesNotContain("pendenteQtd.ToString", fonte);
    }

    private static ProdutoAcabadoCaixa Caixa(StatusIntegracaoCaixa status = StatusIntegracaoCaixa.FinalizadaLocal,
        int quantidade = 8, string? unidade = "UN") => new()
    {
        StatusIntegracao = status,
        QuantidadeProdutos = quantidade,
        UnidadeQuantidade = unidade!,
        PesoBrutoKg = 14.400m,
        TaraKg = 0.050m,
        PesoLiquidoKg = 14.350m
    };

    private static decimal Saldo(decimal pendente, string? unidade, params ProdutoAcabadoCaixa[] caixas)
        => CalculoSaldoProdutoAcabado.SaldoPendenteExibido(pendente, caixas, unidade);
}
