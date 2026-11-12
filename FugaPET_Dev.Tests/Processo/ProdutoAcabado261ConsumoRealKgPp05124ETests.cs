using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 124E: para componente em KG no depósito PP05, o 261 do Produto Acabado passa a usar o PESO
/// LÍQUIDO REAL das caixas (<c>ProdutoAcabadoCaixa.PesoLiquidoKg</c>) em vez do rateio proporcional
/// da ficha técnica. Todo componente fora desse escopo preserva o comportamento do 118B.
/// <para>
/// Nenhum SAP, nenhum banco, nenhum POST: o alocador é puro.
/// </para>
/// </summary>
public sealed class ProdutoAcabado261ConsumoRealKgPp05124ETests
{
    private const string Op = "1000242";
    private const decimal Planejado = 9016m;

    // ===================== fixtures =====================

    /// <summary>Componente elegível ao consumo real: KG + PP05.</summary>
    private static Componente261Fresco CompKgPp05(
        decimal necessaria = 23.000m, decimal? retirado = 0m, string item = "1", string material = "2000219")
        => Comp(item, material, necessaria, "KG", "PP05", retirado);

    private static Componente261Fresco Comp(
        string item, string material, decimal necessaria, string unidade, string deposito, decimal? retirado)
        => new()
        {
            NumeroOrdem = Op,
            Reservation = "3092",
            ReservationItem = item,
            Material = material,
            Plant = "3007",
            StorageLocation = deposito,
            Batch = string.Empty,
            BaseUnit = unidade,
            TipoMovimento = "261",
            RequiredQuantity = necessaria,
            WithdrawnQuantity = retirado,
            ConfirmedAvailableQuantity = necessaria,
            QuantityIsFixed = false
        };

    private static Entrada261Cumulativa Entrada(
        IReadOnlyList<Componente261Fresco> componentes,
        decimal? pesoCorrente,
        decimal? pesoAnterior,
        decimal? priorProduzido = 0m,
        decimal? caixaCorrenteProducao = 8m,
        string unidadeProducao = "UN",
        IReadOnlyDictionary<string, decimal>? ledger = null)
        => new()
        {
            NumeroOrdem = Op,
            NumeroOrdemItemFresco = Op,
            PlannedProductionOp = Planejado,
            PriorProducedConfirmed = priorProduzido,
            CurrentBoxProduction = caixaCorrenteProducao,
            ProductionUnit = unidadeProducao,
            Componentes = componentes,
            ComponentesCompletosComprovado = true,
            PesoLiquidoCaixaCorrenteKg = pesoCorrente,
            PesoLiquidoConfirmadoAnteriorKg = pesoAnterior,
            ConsumoLocalConfirmadoPorComponente = ledger
                ?? ProdutoAcabado261AllocatorCumulativo.DerivarLedgerLocalEsperado(
                    componentes, priorProduzido, Planejado, pesoAnterior)
        };

    private static Item261Alocado Unico(Resultado261Cumulativo r) => Assert.Single(r.Itens);

    // ===================== T01 / T02 / T03: real menor, igual e maior =====================

    [Fact]
    public void T01_KgPp05_RealMenorQueTeorico_EnviaOReal()
    {
        // Previsto 23,000 KG · Real 22,000 KG  ⇒  261 = 22,000 KG
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(necessaria: 23.000m)], pesoCorrente: 22.000m, pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Item261Alocado item = Unico(r);
        Assert.Equal(22.000m, item.Delta261);
        Assert.Equal(22.000m, item.TargetCumulative);
        Assert.Equal("KG", item.Unidade);
        // E NÃO o rateio proporcional (23 * 8 / 9016 = 0,020).
        Assert.NotEqual(0.020m, item.Delta261);
    }

    [Fact]
    public void T02_KgPp05_RealIgualAoTeorico_EnviaOReal()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(necessaria: 23.000m)], pesoCorrente: 23.000m, pesoAnterior: 0m));

        Assert.Equal(23.000m, Unico(r).Delta261);
    }

    [Fact]
    public void T03_KgPp05_RealMaiorQueTeorico_EnviaOReal_SemContornarGuards()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(necessaria: 23.000m)], pesoCorrente: 23.500m, pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(23.500m, Unico(r).Delta261);
    }

    [Fact]
    public void T03a_RealMaiorQueOJaRetirado_ContinuaSujeitoAoGuardDeDeltaNegativo()
    {
        // SAP já retirou 30,000 e o alvo real é 23,500 ⇒ delta negativo ⇒ BLOCK (guard do 118B intacto).
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(necessaria: 23.000m, retirado: 30.000m)],
                pesoCorrente: 23.500m,
                pesoAnterior: 0m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 30.000m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("NEGATIVO", r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== T04 / T05 / T06: escopo preservado =====================

    [Fact]
    public void T04_KgEmPp01_PreservaRateioProporcional()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [Comp("1", "2000219", 1440.000m, "KG", "PP01", 0m)],
                pesoCorrente: 22.000m, pesoAnterior: 0m));

        // 1440 * 8 / 9016 = 1,2777... -> 1,278 (fórmula 118B, intacta)
        Assert.Equal(1.278m, Unico(r).Delta261);
        Assert.NotEqual(22.000m, Unico(r).Delta261);
    }

    [Fact]
    public void T05_UnidadeUN_PreservaRateioProporcional()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [Comp("2", "3000046", 1003m, "UN", "PP05", 0m)],
                pesoCorrente: 22.000m, pesoAnterior: 0m));

        // UN em PP05 NÃO é elegível: só KG+PP05.
        Assert.Equal(0.890m, Unico(r).Delta261);
    }

    [Fact]
    public void T06_OpEmUN_ComComponenteKgPp05_AplicaOReal()
    {
        // A elegibilidade é do COMPONENTE, não da OP. OP em UN e componente KG/PP05 ⇒ real.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(necessaria: 23.000m)],
                pesoCorrente: 22.000m, pesoAnterior: 0m, unidadeProducao: "UN"));

        Assert.Equal(22.000m, Unico(r).Delta261);
    }

    [Fact]
    public void T06a_OpEmKG_NaoTornaComponentePp01Elegivel()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [Comp("1", "2000219", 1440.000m, "KG", "PP01", 0m)],
                pesoCorrente: 22.000m, pesoAnterior: 0m,
                caixaCorrenteProducao: 14.350m, unidadeProducao: "KG"));

        Assert.NotEqual(22.000m, Unico(r).Delta261);
    }

    [Theory]
    [InlineData("KG", "PP05", true)]
    [InlineData("kg", "pp05", true)]
    [InlineData(" KG ", " PP05 ", true)]
    [InlineData("KG", "PP01", false)]
    [InlineData("UN", "PP05", false)]
    [InlineData("KGM", "PP05", false)]
    [InlineData("", "PP05", false)]
    [InlineData("KG", "", false)]
    public void Elegibilidade_ExigeExatamenteKgEPp05(string unidade, string deposito, bool elegivel)
        => Assert.Equal(
            elegivel,
            ProdutoAcabado261AllocatorCumulativo.EhComponenteConsumoRealPorPeso(
                Comp("1", "2000219", 23m, unidade, deposito, 0m)));

    // ===================== T07: peso ausente/inválido =====================

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void T07_PesoCaixaCorrenteAusenteOuInvalido_Bloqueia_SemCairParaTeorico(int? peso)
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05()],
                pesoCorrente: peso is int p ? p : null,
                pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("CONSUMO_REAL_PESO_CAIXA_AUSENTE", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void T07a_AcumuladoRealIndisponivel_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05()],
                pesoCorrente: 22.000m,
                pesoAnterior: null,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("CONSUMO_REAL_ACUMULADO_NAO_RECONSTRUIVEL", r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== T08 / T09: duas caixas e acumulado parcial =====================

    [Fact]
    public void T08_DuasCaixasComPesosDiferentes_SegundaEnviaApenasOSeuDelta()
    {
        // 1ª caixa já confirmada com 22,000 e já consumida no SAP; 2ª caixa pesa 21,500.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(necessaria: 23.000m, retirado: 22.000m)],
                pesoCorrente: 21.500m,
                pesoAnterior: 22.000m,
                priorProduzido: 8m));

        Item261Alocado item = Unico(r);
        Assert.Equal(43.500m, item.TargetCumulative);   // 22,000 + 21,500
        Assert.Equal(22.000m, item.WithdrawnFresco);
        Assert.Equal(21.500m, item.Delta261);           // só o peso da caixa corrente
    }

    [Fact]
    public void T09_ConsumoParcialAcumulado_TresCaixasSomamOsPesosReais()
    {
        decimal acumulado = 0m;
        foreach (decimal peso in new[] { 22.000m, 21.500m, 23.250m })
        {
            Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
                Entrada(
                    [CompKgPp05(necessaria: 100.000m, retirado: acumulado)],
                    pesoCorrente: peso,
                    pesoAnterior: acumulado,
                    priorProduzido: 8m));

            Assert.Equal(peso, Unico(r).Delta261);
            acumulado += Unico(r).Delta261;
        }

        Assert.Equal(66.750m, acumulado);   // 22,000 + 21,500 + 23,250
    }

    // ===================== T10 / T11: caixa já contabilizada e reexecução =====================

    [Fact]
    public void T10_CaixaJaContabilizada_NaoEhSomadaDuasVezes()
    {
        ProdutoAcabadoCaixa corrente = Caixa(100, 21.500m, StatusIntegracaoCaixa.FinalizadaLocal);
        ProdutoAcabadoCaixa anterior = Caixa(99, 22.000m, StatusIntegracaoCaixa.ConfirmadaSap);

        decimal soma = ProdutoAcabado261AllocatorCumulativo.SomarPesoLiquidoConfirmadoAnterior(
            [anterior, corrente], corrente.CodigoProdutoAcabadoCaixa);

        // A corrente NÃO entra no acumulado anterior (nem está CONFIRMADA, nem é "anterior").
        Assert.Equal(22.000m, soma);
    }

    [Fact]
    public void T10a_AcumuladoIgnoraCanceladaEstadosNaoConfirmadosESemCodigo()
    {
        List<ProdutoAcabadoCaixa> caixas =
        [
            Caixa(1, 10.000m, StatusIntegracaoCaixa.ConfirmadaSap),
            Caixa(2, 99.000m, StatusIntegracaoCaixa.Cancelada),
            Caixa(3, 99.000m, StatusIntegracaoCaixa.EmPesagem),
            Caixa(4, 99.000m, StatusIntegracaoCaixa.EnviandoSap),
            Caixa(5, 99.000m, StatusIntegracaoCaixa.ErroSap),
            Caixa(6, 99.000m, StatusIntegracaoCaixa.IndeterminadoTimeout),
            Caixa(7, 99.000m, StatusIntegracaoCaixa.Bloqueada),
            Caixa(null, 99.000m, StatusIntegracaoCaixa.ConfirmadaSap),   // sem código persistido
            Caixa(8, 5.000m, StatusIntegracaoCaixa.ConfirmadaSap)
        ];

        Assert.Equal(
            15.000m,
            ProdutoAcabado261AllocatorCumulativo.SomarPesoLiquidoConfirmadoAnterior(caixas, 100));
    }

    [Fact]
    public void T11_ReexecucaoDoMesmoEvento_ProduzDeltaZero_NaoDuplicaConsumo()
    {
        // Após o 1º envio, o SAP já tem 22,000 retirado e a caixa ainda é a corrente.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(necessaria: 23.000m, retirado: 22.000m)],
                pesoCorrente: 22.000m,
                pesoAnterior: 0m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 22.000m }));

        Assert.Equal(CenarioAllocator261.NadaAEnviar, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("3092/1", r.ComponentesIgnoradosPorDeltaZero);
    }

    [Fact]
    public void T11a_DivergenciaEntreSapELedgerReal_BloqueiaReconciliacao()
    {
        // SAP retirou 50,000 mas o acumulado real local é 22,000 ⇒ divergência material.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(necessaria: 100.000m, retirado: 50.000m)],
                pesoCorrente: 21.500m,
                pesoAnterior: 22.000m,
                priorProduzido: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("BLOCK_RECONCILIATION_REQUIRED", r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== T14 / T15: componentes mistos e ambiguidade =====================

    [Fact]
    public void T14_ComponentesMistos_CadaUmUsaASuaRegra()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [
                    CompKgPp05(necessaria: 23.000m),                        // real
                    Comp("2", "3000046", 1003m, "UN", "PA01", 0m),          // proporcional
                    Comp("3", "2000219", 1440.000m, "KG", "PP01", 0m)       // proporcional
                ],
                pesoCorrente: 22.000m, pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(3, r.Itens.Count);
        Assert.Equal(22.000m, r.Itens.Single(i => i.ReservationItem == "1").Delta261);
        Assert.Equal(0.890m, r.Itens.Single(i => i.ReservationItem == "2").Delta261);
        Assert.Equal(1.278m, r.Itens.Single(i => i.ReservationItem == "3").Delta261);
    }

    [Fact]
    public void T15_MaisDeUmComponenteKgPp05_BloqueiaSomenteEsseCenario()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [
                    CompKgPp05(necessaria: 23.000m, item: "1", material: "2000219"),
                    CompKgPp05(necessaria: 56.102m, item: "5", material: "3000016")
                ],
                pesoCorrente: 22.000m, pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Equal(
            ProdutoAcabado261AllocatorCumulativo.MotivoMultiplosComponentesConsumoReal, r.Mensagem);
        Assert.Contains("ATRIBUICAO_AMBIGUA", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void T15a_UmKgPp05MaisOutrosNaoElegiveis_NaoEhAmbiguo()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [
                    CompKgPp05(necessaria: 23.000m, item: "1"),
                    Comp("5", "3000016", 56.102m, "KG", "PP01", 0m)
                ],
                pesoCorrente: 22.000m, pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(2, r.Itens.Count);
    }

    // ===================== precisão decimal =====================

    [Fact]
    public void PrecisaoDecimal_PreservaTresCasasESerializaSemPerda()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(necessaria: 100.000m)],
                pesoCorrente: 22.1235m, pesoAnterior: 0.0005m));

        // 0,0005 + 22,1235 = 22,1240 -> 22,124 (AwayFromZero, 3 casas)
        Assert.Equal(22.124m, Unico(r).Delta261);
    }

    [Fact]
    public void PrecisaoDecimal_AlvoRealUsaAwayFromZero()
        => Assert.Equal(
            1.001m,
            ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativoReal(0.0005m, 1.0005m));

    // ===================== regressão 118B / 119B =====================

    [Fact]
    public void Regressao118B_SemComponenteElegivel_FormulaProporcionalIntacta()
    {
        // Fixture original do 118B: 8 de 9016, cinco componentes, nenhum em PP05.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [
                    Comp("1", "2000219", 1440.000m, "KG", "PA01", 0m),
                    Comp("2", "3000046", 1003m, "UN", "PA01", 0m),
                    Comp("5", "3000016", 56.102m, "KG", "PA01", 0m)
                ],
                pesoCorrente: 14.350m, pesoAnterior: 0m));

        Assert.Equal(1.278m, r.Itens.Single(i => i.Material == "2000219").Delta261);
        Assert.Equal(0.890m, r.Itens.Single(i => i.Material == "3000046").Delta261);
        Assert.Equal(0.050m, r.Itens.Single(i => i.Material == "3000016").Delta261);
    }

    [Fact]
    public void Regressao118B_FechamentoExatoProporcionalPreservado()
        => Assert.Equal(
            56.102m,
            ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativo(56.102m, 9016m, 9016m));

    [Fact]
    public void Regressao_GuardsDoAllocatorContinuamValendoParaOComponenteElegivel()
    {
        // QuantityIsFixed=null continua bloqueando, mesmo em KG/PP05 com peso real disponível.
        Componente261Fresco fixaIndeterminada = CompKgPp05() with { QuantityIsFixed = null };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([fixaIndeterminada], pesoCorrente: 22.000m, pesoAnterior: 0m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("AUSENTE/INDETERMINADO", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Regressao_ContradicaoDeOpContinuaBloqueandoAntesDoConsumoReal()
    {
        Entrada261Cumulativa entrada = Entrada([CompKgPp05()], 22.000m, 0m) with
        {
            NumeroOrdemItemFresco = "1000210"
        };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("ITEM_MANUFACTURING_ORDER_MISMATCH", r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== T12 / T13: timeout e 101 preservados (source) =====================

    [Fact]
    public void T12_T13_AlocadorNaoTocaTimeoutNem101()
    {
        string alocador = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        // O alocador segue puro: nenhum POST, nenhum retry, nenhuma referência a 101/HU/gateway.
        foreach (string proibido in new[]
        {
            "HttpClient", "SendAsync", "PostAsync", "Movimento101", "Gateway", "Retry", "Npgsql"
        })
        {
            Assert.DoesNotContain(proibido, alocador, StringComparison.Ordinal);
        }

        // E o 101 continua recebendo a quantidade de produtos da caixa, não o peso do componente.
        string form = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));
        Assert.Contains(
            "QuantityInEntryUnit = caixa.QuantidadeProdutos.ToString(CultureInfo.InvariantCulture)",
            form, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_ElegibilidadeNaoUsaUnidadeDaOpNemHardcodeDeMaterial()
    {
        string alocador = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        int i = alocador.IndexOf("internal static bool EhComponenteConsumoRealPorPeso", StringComparison.Ordinal);
        Assert.True(i >= 0);
        int fim = alocador.IndexOf("\n    internal", i + 40, StringComparison.Ordinal);
        string corpo = fim > i ? alocador[i..fim] : alocador[i..];

        Assert.Contains("BaseUnit", corpo, StringComparison.Ordinal);
        Assert.Contains("StorageLocation", corpo, StringComparison.Ordinal);
        // A elegibilidade é do COMPONENTE: não olha ProductionUnit nem material específico.
        Assert.DoesNotContain("ProductionUnit", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("1000242", corpo, StringComparison.Ordinal);
    }

    // ===================== infra =====================

    private static ProdutoAcabadoCaixa Caixa(long? codigo, decimal pesoLiquido, StatusIntegracaoCaixa status)
    {
        ProdutoAcabadoCaixa c = new()
        {
            NumeroOrdemProducao = Op,
            ItemOrdemProducao = "1",
            Material = "4000174",
            Lote = "169 26",
            Centro = "3007",
            Deposito = "PA01",
            PesoLiquidoKg = pesoLiquido,
            Terminal = "TERM-01"
        };
        c.CodigoProdutoAcabadoCaixa = codigo;
        c.StatusIntegracao = status;
        return c;
    }

    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        Assert.False(string.IsNullOrWhiteSpace(dir), "Raiz do projeto nao localizada.");
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static string SemComentarios(string fonte)
    {
        string semBloco = System.Text.RegularExpressions.Regex.Replace(
            fonte, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"///?[^\r\n]*", string.Empty);
    }
}
