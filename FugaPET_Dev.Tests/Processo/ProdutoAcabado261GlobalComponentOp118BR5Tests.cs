using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

public sealed class ProdutoAcabado261GlobalComponentOp118BR5Tests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Non261_OpAusente_BloqueiaSemComandos(string? numeroOrdem)
    {
        Resultado261Cumulativo resultado = Calcular(numeroOrdem);

        Assert.Contains("COMPONENT_MANUFACTURING_ORDER_MISSING", resultado.Mensagem);
        AssertSemComandos(resultado);
    }

    [Fact]
    public void Non261_OpDivergente_BloqueiaSemComandos()
    {
        Resultado261Cumulativo resultado = Calcular("1000002");

        Assert.Contains("COMPONENT_MANUFACTURING_ORDER_MISMATCH", resultado.Mensagem);
        AssertSemComandos(resultado);
    }

    [Fact]
    public void Non261_OpCoerente_NaoEmitidoComo261()
    {
        Resultado261Cumulativo resultado = Calcular("1000001");

        Assert.True(resultado.Sucesso, resultado.Mensagem);
        Item261Alocado item = Assert.Single(resultado.Itens);
        Assert.Equal("1", item.ReservationItem);
        Assert.Equal(1.278m, item.Delta261);
        Assert.Single(ProcessoProdutoAcabadoForm.MontarComponentesPipelineRuntime(resultado));
    }

    private static Resultado261Cumulativo Calcular(string? opNon261)
    {
        Componente261Fresco componente261 = new()
        {
            NumeroOrdem = "1000001",
            Reservation = "3092",
            ReservationItem = "1",
            Material = "2000219",
            Plant = "3007",
            StorageLocation = "PA01",
            BaseUnit = "KG",
            TipoMovimento = "261",
            RequiredQuantity = 1440m,
            WithdrawnQuantity = 0m,
            ConfirmedAvailableQuantity = 1440m,
            QuantityIsFixed = false
        };
        Componente261Fresco componenteNon261 = new()
        {
            NumeroOrdem = opNon261!,
            Reservation = "3092",
            ReservationItem = "2",
            TipoMovimento = "101"
        };

        return ProdutoAcabado261AllocatorCumulativo.Calcular(new Entrada261Cumulativa
        {
            NumeroOrdem = "1000001",
            NumeroOrdemItemFresco = "1000001",
            PlannedProductionOp = 9016m,
            PriorProducedConfirmed = 0m,
            CurrentBoxProduction = 8m,
            ProductionUnit = "UN",
            Componentes = [componente261, componenteNon261],
            ComponentesCompletosComprovado = true,
            ConsumoLocalConfirmadoPorComponente = new Dictionary<string, decimal> { ["3092/1"] = 0m }
        });
    }

    private static void AssertSemComandos(Resultado261Cumulativo resultado)
    {
        Assert.Equal(CenarioAllocator261.Bloqueado, resultado.Cenario);
        Assert.Empty(resultado.Itens);
        Assert.Empty(ProcessoProdutoAcabadoForm.MontarComponentesPipelineRuntime(resultado));
        ProdutoAcabadoCaixa caixa = new()
        {
            CodigoProdutoAcabadoCaixa = 1,
            NumeroOrdemProducao = "1000001",
            ItemOrdemProducao = "1",
            Material = "4000174",
            Lote = "169 26",
            Centro = "3007",
            Deposito = "PA01",
            QuantidadeProdutos = 8,
            UnidadeQuantidade = "UN",
            PesoBrutoKg = 14.400m,
            TaraKg = .050m,
            PesoLiquidoKg = 14.350m
        };
        var origem = ProcessoProdutoAcabadoForm.MontarOrigemPipelineRuntime(
            caixa, null, new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc), resultado);
        var comandos = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);
        Assert.False(comandos.Sucesso);
        Assert.Null(comandos.Comando261);
        Assert.Null(comandos.Comando101);
    }
}
