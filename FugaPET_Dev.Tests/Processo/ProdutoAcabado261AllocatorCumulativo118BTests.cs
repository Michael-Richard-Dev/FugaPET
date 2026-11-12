using System.Globalization;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 118B: alocador CUMULATIVO do 261 por caixa. Nenhum SAP real, nenhum banco, nenhuma OP de
/// producao consultada: a fixture 1000242 e OFFLINE e imutavel (OP em quarentena para escrita).
/// Cobre as regressoes do incidente (§13/§14/§15), TODOS os guards negativos (§11/§18) e as provas
/// de source pos-fix (§21).
/// </summary>
public sealed class ProdutoAcabado261AllocatorCumulativo118BTests
{
    private const string Op = "1000242";
    private const decimal PlanejadoFixture = 9016m;

    // ===================== Fixture OFFLINE da OP 1000242 (§13) =====================
    // Cinco componentes reais do incidente, com RequiredQuantity integral da OP.
    private static IReadOnlyList<Componente261Fresco> ComponentesFixture(
        decimal retirado2000219 = 0m,
        decimal retirado3000046 = 0m,
        decimal retirado3000017 = 0m,
        decimal retirado3000018 = 0m,
        decimal retirado3000016 = 0m)
        =>
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado2000219),
            Comp("3092", "2", "3000046", 1003m, "UN", retirado3000046),
            Comp("3092", "3", "3000017", 1003m, "UN", retirado3000017),
            Comp("3092", "4", "3000018", 1003m, "UN", retirado3000018),
            Comp("3092", "5", "3000016", 56.102m, "KG", retirado3000016)
        ];

    /// <summary>
    /// Fixture com o WithdrawnQuantity JA no alvo cumulativo da producao anterior informada — e o
    /// estado real do SAP depois de n caixas corretas, e o que a reconciliacao do §9 espera encontrar.
    /// </summary>
    private static IReadOnlyList<Componente261Fresco> ComponentesNoAlvoDe(decimal produzidoAnterior)
        => ComponentesFixture()
            .Select(c => c with
            {
                WithdrawnQuantity = ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativo(
                    c.RequiredQuantity!.Value, produzidoAnterior, PlanejadoFixture)
            })
            .ToList();

    private static Componente261Fresco Comp(
        string reserva, string item, string material, decimal necessaria, string unidade,
        decimal? retirado, bool? fixa = false)
        => new()
        {
            NumeroOrdem = Op,
            Reservation = reserva,
            ReservationItem = item,
            Material = material,
            Plant = "3007",
            StorageLocation = "PA01",
            Batch = string.Empty,
            BaseUnit = unidade,
            TipoMovimento = "261",
            RequiredQuantity = necessaria,
            WithdrawnQuantity = retirado,
            ConfirmedAvailableQuantity = necessaria,
            QuantityIsFixed = fixa
        };

    private static Entrada261Cumulativa Entrada(
        IReadOnlyList<Componente261Fresco> componentes,
        decimal? priorProduzido,
        decimal? caixaCorrente,
        decimal? planejado = PlanejadoFixture,
        bool completos = true,
        string unidadeProducao = "UN",
        string? ordemItem = null,
        IReadOnlyDictionary<string, decimal>? ledger = null)
        => new()
        {
            NumeroOrdem = Op,
            NumeroOrdemItemFresco = ordemItem ?? Op,
            PlannedProductionOp = planejado,
            PriorProducedConfirmed = priorProduzido,
            CurrentBoxProduction = caixaCorrente,
            ProductionUnit = unidadeProducao,
            Componentes = componentes,
            ComponentesCompletosComprovado = completos,
            ConsumoLocalConfirmadoPorComponente = ledger
                ?? ProdutoAcabado261AllocatorCumulativo.DerivarLedgerLocalEsperado(
                    componentes, priorProduzido, planejado)
        };

    private static decimal Delta(Resultado261Cumulativo r, string material)
        => r.Itens.Single(i => i.Material == material).Delta261;

    // ===================== §13 PRIMEIRA CAIXA: 8 de 9016 =====================

    [Fact]
    public void PrimeiraCaixa_8De9016_ProduzOsCincoDeltasEsperados()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(8m, r.ProducedCumulative);
        Assert.Equal(5, r.Itens.Count);

        Assert.Equal(1.278m, Delta(r, "2000219"));
        Assert.Equal(0.890m, Delta(r, "3000046"));
        Assert.Equal(0.890m, Delta(r, "3000017"));
        Assert.Equal(0.890m, Delta(r, "3000018"));
        Assert.Equal(0.050m, Delta(r, "3000016"));
    }

    [Fact]
    public void PrimeiraCaixa_NaoEnviaNecessidadeIntegralDaOp()
    {
        // A regressao do incidente: 1440 KG integral tinha ido como quantidade de UMA caixa.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 0m, caixaCorrente: 8m));

        Assert.DoesNotContain(r.Itens, i => i.Delta261 == 1440.000m);
        Assert.DoesNotContain(r.Itens, i => i.Delta261 == 1003m);
        Assert.DoesNotContain(r.Itens, i => i.Delta261 == 56.102m);
        Assert.All(r.Itens, i => Assert.True(i.Delta261 > 0m));
    }

    [Fact]
    public void PrimeiraCaixa_PreservaIdentidadeEUnidadeDoComponente()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 0m, caixaCorrente: 8m));

        Item261Alocado kg = r.Itens.Single(i => i.Material == "2000219");
        Assert.Equal("3092", kg.Reservation);
        Assert.Equal("1", kg.ReservationItem);
        Assert.Equal("KG", kg.Unidade);                 // §7: nenhuma conversao KG <-> UN
        Assert.Equal("UN", r.Itens.Single(i => i.Material == "3000046").Unidade);
    }

    // ===================== §14 SEGUNDA CAIXA (cumulativo) =====================

    [Fact]
    public void SegundaCaixa_Cumulativo16_UsaWithdrawnFrescoComoBase()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                ComponentesFixture(1.278m, 0.890m, 0.890m, 0.890m, 0.050m),
                priorProduzido: 8m,
                caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(16m, r.ProducedCumulative);

        Assert.Equal(1.277m, Delta(r, "2000219"));
        Assert.Equal(0.890m, Delta(r, "3000046"));
        Assert.Equal(0.890m, Delta(r, "3000017"));
        Assert.Equal(0.890m, Delta(r, "3000018"));
        Assert.Equal(0.050m, Delta(r, "3000016"));
    }

    [Fact]
    public void SegundaCaixa_AlvoCumulativoEhMaiorQueOPrimeiro()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                ComponentesFixture(1.278m, 0.890m, 0.890m, 0.890m, 0.050m),
                priorProduzido: 8m,
                caixaCorrente: 8m));

        Item261Alocado kg = r.Itens.Single(i => i.Material == "2000219");
        Assert.Equal(2.555m, kg.TargetCumulative);
        Assert.Equal(1.278m, kg.WithdrawnFresco);
        Assert.Equal(kg.TargetCumulative - kg.WithdrawnFresco, kg.Delta261);
    }

    // ===================== §15 FECHAMENTO EXATO =====================

    [Fact]
    public void FechamentoExato_AlvoEhExatamenteRequiredQuantity()
    {
        // Ultima caixa: produzido cumulativo == planejado, com o SAP no alvo das caixas anteriores.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesNoAlvoDe(9008m), priorProduzido: 9008m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(PlanejadoFixture, r.ProducedCumulative);
        Assert.Equal(1440.000m, r.Itens.Single(i => i.Material == "2000219").TargetCumulative);
        Assert.Equal(1003m, r.Itens.Single(i => i.Material == "3000046").TargetCumulative);
        Assert.Equal(56.102m, r.Itens.Single(i => i.Material == "3000016").TargetCumulative);
    }

    [Fact]
    public void FechamentoExato_SomaDosDeltasFechaRequiredQuantitySemResiduo()
    {
        // Caminha caixa a caixa ate fechar a OP e soma os deltas de cada componente.
        Dictionary<string, decimal> acumulado = new()
        {
            ["2000219"] = 0m, ["3000046"] = 0m, ["3000017"] = 0m, ["3000018"] = 0m, ["3000016"] = 0m
        };

        decimal produzido = 0m;
        const decimal porCaixa = 1127m; // 9016 / 1127 = 8 caixas exatas
        while (produzido < PlanejadoFixture)
        {
            Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
                Entrada(
                    ComponentesFixture(
                        acumulado["2000219"], acumulado["3000046"], acumulado["3000017"],
                        acumulado["3000018"], acumulado["3000016"]),
                    priorProduzido: produzido,
                    caixaCorrente: porCaixa));

            Assert.True(r.Sucesso, r.Mensagem);
            foreach (Item261Alocado item in r.Itens)
            {
                acumulado[item.Material] += item.Delta261;
            }

            produzido += porCaixa;
        }

        Assert.Equal(PlanejadoFixture, produzido);
        Assert.Equal(1440.000m, acumulado["2000219"]);
        Assert.Equal(1003m, acumulado["3000046"]);
        Assert.Equal(1003m, acumulado["3000017"]);
        Assert.Equal(1003m, acumulado["3000018"]);
        Assert.Equal(56.102m, acumulado["3000016"]);
    }

    [Fact]
    public void DeltaZero_PulaOComponenteSemBloquearOsOutros()
    {
        // 3000046 ja esta no alvo de 8/9016 (0.890); os demais ainda nao.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                ComponentesFixture(0m, 0.890m, 0m, 0m, 0m),
                priorProduzido: 0m,
                caixaCorrente: 8m,
                ledger: new Dictionary<string, decimal>
                {
                    ["3092/1"] = 0m, ["3092/2"] = 0.890m, ["3092/3"] = 0m, ["3092/4"] = 0m, ["3092/5"] = 0m
                }));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(4, r.Itens.Count);
        Assert.DoesNotContain(r.Itens, i => i.Material == "3000046");
        Assert.Contains("3092/2", r.ComponentesIgnoradosPorDeltaZero);
    }

    [Fact]
    public void TodosNoAlvo_NadaAEnviar()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                ComponentesFixture(1.278m, 0.890m, 0.890m, 0.890m, 0.050m),
                priorProduzido: 8m,
                caixaCorrente: 0.0001m,
                ledger: new Dictionary<string, decimal>
                {
                    ["3092/1"] = 1.278m, ["3092/2"] = 0.890m, ["3092/3"] = 0.890m,
                    ["3092/4"] = 0.890m, ["3092/5"] = 0.050m
                }));

        Assert.Equal(CenarioAllocator261.NadaAEnviar, r.Cenario);
        Assert.Empty(r.Itens);
    }

    // ===================== §11/§18 GUARDS NEGATIVOS =====================

    [Fact]
    public void Guard_GoodsReceiptQtyAusente_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: null, caixaCorrente: 8m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoProduzidoAnteriorInvalido, r.Mensagem);
    }

    [Fact]
    public void Guard_GoodsReceiptQtyNegativo_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: -1m, caixaCorrente: 8m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
    }

    [Fact]
    public void Guard_WithdrawnAusente_Bloqueia()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: null)
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("WithdrawnQuantity ausente", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_WithdrawnDivergenteDoLedgerLocal_BloqueiaReconciliacao()
    {
        // Exatamente o estado do incidente: o SAP tem a necessidade INTEGRAL retirada.
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 1440.000m)
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("BLOCK_RECONCILIATION_REQUIRED", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_LedgerLocalIndisponivel_Bloqueia()
    {
        Entrada261Cumulativa entrada = Entrada(ComponentesFixture(), 0m, 8m) with
        {
            ConsumoLocalConfirmadoPorComponente = null
        };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoLedgerIndisponivel, r.Mensagem);
    }

    [Fact]
    public void Guard_ComponenteAusenteNoLedger_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), 0m, 8m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("ausente no ledger local", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_QuantityIsFixedTrue_Bloqueia()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 0m, fixa: true)
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("QuantityIsFixed=true", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_QuantityIsFixedNull_BloqueiaPorMotivoDiferenteDeTrue()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 0m, fixa: null)
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("AUSENTE/INDETERMINADO", r.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityIsFixed=true", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_CaixaCorrenteIndeterminada_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 0m, caixaCorrente: null));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoCaixaCorrenteInvalida, r.Mensagem);
    }

    [Fact]
    public void Guard_CaixaCorrenteZero_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 0m, caixaCorrente: 0m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoCaixaCorrenteInvalida, r.Mensagem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public void Guard_PlanejadoInvalido_Bloqueia(int? planejado)
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), 0m, 8m,
                planejado: planejado is int p ? p : null,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoPlanejadoInvalido, r.Mensagem);
    }

    [Fact]
    public void Guard_ProduzidoMaiorQuePlanejado_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 9016m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoProduzidoMaiorQuePlanejado, r.Mensagem);
    }

    [Fact]
    public void Guard_DeltaNegativo_Bloqueia()
    {
        // Retirado no SAP (3.000) acima do alvo cumulativo de 16/9016 (2.555), e o ledger local
        // concorda com o SAP — logo nao e divergencia de reconciliacao, e delta negativo mesmo.
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 3.000m)
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 8m, caixaCorrente: 8m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 3.000m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("NEGATIVO", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_UnidadeDeProducaoIndeterminada_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), 0m, 8m, unidadeProducao: "   "));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoUnidadeProducaoIndeterminada, r.Mensagem);
    }

    [Fact]
    public void Guard_BaseUnitDoComponenteIndeterminada_Bloqueia()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 0m) with { BaseUnit = "  " }
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("BaseUnit indeterminada", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_IdentidadeDeComponenteAusente_Bloqueia()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 0m) with { ReservationItem = "" }
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("identidade nao deterministica", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_ListaDeComponentesIncompleta_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), 0m, 8m, completos: false));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoListaComponentesIncompleta, r.Mensagem);
    }

    [Fact]
    public void Guard_SemComponentes_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([], 0m, 8m, ledger: new Dictionary<string, decimal>()));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoSemComponentes, r.Mensagem);
    }

    [Fact]
    public void Guard_ContradicaoEntreLeituras_OpDoItemDivergente_Bloqueia()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), 0m, 8m, ordemItem: "1000210"));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        // GATE 118B-R4: motivo generico substituido por codigo explicito de MISMATCH.
        Assert.Equal(ProdutoAcabado261AllocatorCumulativo.MotivoItemOrdemDivergente, r.Mensagem);
        Assert.Contains("ITEM_MANUFACTURING_ORDER_MISMATCH", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_ContradicaoEntreLeituras_OpDoComponenteDivergente_Bloqueia()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 0m) with { NumeroOrdem = "1000210" }
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("COMPONENT_MANUFACTURING_ORDER_MISMATCH", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains("3092/1", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_RequiredQuantityAusente_Bloqueia()
    {
        IReadOnlyList<Componente261Fresco> componentes =
        [
            Comp("3092", "1", "2000219", 1440.000m, "KG", retirado: 0m) with { RequiredQuantity = null }
        ];

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(componentes, priorProduzido: 0m, caixaCorrente: 8m,
                ledger: new Dictionary<string, decimal> { ["3092/1"] = 0m }));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("RequiredQuantity ausente", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Guard_SemOrdem_Bloqueia()
    {
        Entrada261Cumulativa entrada = Entrada(ComponentesFixture(), 0m, 8m) with
        {
            NumeroOrdem = "   ", NumeroOrdemItemFresco = "   "
        };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);
        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
    }

    // ===================== §7 producao da caixa corrente / unidades =====================

    [Fact]
    public void ProducaoCaixaCorrente_UnidadeDeQuantidade_UsaQuantidadeDeProdutos()
    {
        ProdutoAcabadoCaixa caixa = new()
        {
            QuantidadeProdutos = 8, UnidadeQuantidade = "UN",
            PesoLiquidoKg = 14.350m, UnidadePeso = "KG"
        };

        Assert.Equal(8m, ProdutoAcabado261AllocatorCumulativo.ResolverProducaoCaixaCorrente(caixa, "UN"));
    }

    [Fact]
    public void ProducaoCaixaCorrente_UnidadeDePeso_UsaPesoLiquido()
    {
        ProdutoAcabadoCaixa caixa = new()
        {
            QuantidadeProdutos = 8, UnidadeQuantidade = "UN",
            PesoLiquidoKg = 14.350m, UnidadePeso = "KG"
        };

        Assert.Equal(14.350m, ProdutoAcabado261AllocatorCumulativo.ResolverProducaoCaixaCorrente(caixa, "KG"));
    }

    [Theory]
    [InlineData("TO")]
    [InlineData("G")]
    [InlineData("")]
    [InlineData(null)]
    public void ProducaoCaixaCorrente_UnidadeQueNaoCasa_RetornaNullSemConverter(string? unidadeOp)
    {
        ProdutoAcabadoCaixa caixa = new()
        {
            QuantidadeProdutos = 8, UnidadeQuantidade = "UN",
            PesoLiquidoKg = 14.350m, UnidadePeso = "KG"
        };

        Assert.Null(ProdutoAcabado261AllocatorCumulativo.ResolverProducaoCaixaCorrente(caixa, unidadeOp));
    }

    // ===================== §12 completude a partir da leitura fresca =====================

    [Fact]
    public void LeituraFresca_ContagemDivergente_NaoDeclaraCompletude()
    {
        LeituraFrescaOrdem261 leitura = new()
        {
            NumeroOrdemConsultada = Op,
            Disponivel = true,
            Item = new ItemOrdemProducaoSap(),
            Componentes = [new ComponenteOrdemProducaoSap(), new ComponenteOrdemProducaoSap()],
            ComponentesDeclaradosPeloServico = 5
        };

        Assert.False(leitura.ComponentesCompletos);
    }

    [Fact]
    public void LeituraFresca_SemContagemDeclarada_NaoDeclaraCompletude()
    {
        LeituraFrescaOrdem261 leitura = new()
        {
            NumeroOrdemConsultada = Op,
            Disponivel = true,
            Item = new ItemOrdemProducaoSap(),
            Componentes = [new ComponenteOrdemProducaoSap()],
            ComponentesDeclaradosPeloServico = null
        };

        Assert.False(leitura.ComponentesCompletos);
    }

    [Fact]
    public void LeituraFresca_ContagemBate_DeclaraCompletude()
    {
        LeituraFrescaOrdem261 leitura = new()
        {
            NumeroOrdemConsultada = Op,
            Disponivel = true,
            Item = new ItemOrdemProducaoSap(),
            Componentes = [new ComponenteOrdemProducaoSap(), new ComponenteOrdemProducaoSap()],
            ComponentesDeclaradosPeloServico = 2
        };

        Assert.True(leitura.ComponentesCompletos);
    }

    [Theory]
    [InlineData("""{ "d": { "__count": "5", "results": [] } }""", 5)]
    [InlineData("""{ "d": { "__count": 3, "results": [] } }""", 3)]
    [InlineData("""{ "d": { "results": [] } }""", null)]
    [InlineData("nao-json-nao-xml", null)]
    public void LerContagemDeclarada_ExtraiInlinecountOuNull(string corpo, int? esperado)
        => Assert.Equal(esperado, ProductionOrderSapApiClient.LerContagemDeclarada(corpo));

    [Fact]
    public void LeituraFresca_Indisponivel_NaoTemItemNemCompletude()
    {
        LeituraFrescaOrdem261 leitura = LeituraFrescaOrdem261.Indisponivel(Op, "sem rede");

        Assert.False(leitura.Disponivel);
        Assert.Null(leitura.Item);
        Assert.False(leitura.ComponentesCompletos);
        Assert.Empty(leitura.Componentes);
    }

    // ===================== §21 provas de SOURCE pos-fix =====================

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

    /// <summary>Remove comentarios para que assercoes NEGATIVAS nao batam em texto explicativo.</summary>
    private static string SemComentarios(string fonte)
    {
        string semBloco = System.Text.RegularExpressions.Regex.Replace(
            fonte, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"//[^\r\n]*", string.Empty);
    }

    [Fact]
    public void SourceAudit_NenhumaRotaDePaEnviaRequiredQuantityIntegralPorCaixa()
    {
        string form = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        // O defeito do incidente era exatamente esta atribuicao na origem do 261.
        Assert.DoesNotContain("Quantidade = componente.QuantidadeNecessaria", form, StringComparison.Ordinal);
        Assert.Contains("Quantidade = item.Delta261", form, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_MfgOrderItemActualDeliveryQtyNaoEhMaisLidoNoCliente()
    {
        string cliente = SemComentarios(
            LerFonte("Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs"));

        Assert.DoesNotContain("MfgOrderItemActualDeliveryQty", cliente, StringComparison.Ordinal);
        Assert.Contains("MfgOrderItemGoodsReceiptQty", cliente, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_NenhumNullParaFalseEmQuantityIsFixed()
    {
        string alocador = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        Assert.DoesNotContain("QuantityIsFixed ?? false", alocador, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityIsFixed.GetValueOrDefault", alocador, StringComparison.Ordinal);
        Assert.Contains("QuantityIsFixed is not bool fixa", alocador, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_NenhumGoodsReceiptAusenteViraZero()
    {
        string alocador = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        Assert.DoesNotContain("PriorProducedConfirmed ?? 0", alocador, StringComparison.Ordinal);
        Assert.DoesNotContain("PriorProducedConfirmed.GetValueOrDefault", alocador, StringComparison.Ordinal);
        Assert.DoesNotContain("WithdrawnQuantity ?? 0", alocador, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantidadeRecebidaSap ?? 0", alocador, StringComparison.Ordinal);

        string controller = SemComentarios(LerFonte("Controle", "Processo", "ProdutoAcabadoController.cs"));
        Assert.DoesNotContain("QuantidadeRecebidaSap ?? 0", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_NaoExisteDesvioPorNumeroDeOp()
    {
        // §19: a quarentena da 1000242 e OPERACIONAL; nao pode virar condicional no source.
        foreach (string[] arquivo in new[]
        {
            new[] { "Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs" },
            ["Controle", "Processo", "ProdutoAcabadoController.cs"],
            ["Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"],
            ["Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs"]
        })
        {
            Assert.DoesNotContain("1000242", SemComentarios(LerFonte(arquivo)), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SourceAudit_AlocadorNaoFazHttpNemBanco()
    {
        string alocador = SemComentarios(
            LerFonte("Servicos", "Operacao", "ProdutoAcabado261AllocatorCumulativo.cs"));

        foreach (string proibido in new[] { "HttpClient", "SendAsync", "Npgsql", "DateTime.Now", "DateTime.UtcNow" })
        {
            Assert.DoesNotContain(proibido, alocador, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SourceAudit_LeituraFrescaUsaInlinecountESelectMinimo()
    {
        string cliente = LerFonte("Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs");

        Assert.Contains("$inlinecount=allpages", cliente, StringComparison.Ordinal);
        Assert.Contains("MfgOrderItemGoodsReceiptQty", cliente, StringComparison.Ordinal);
        Assert.Contains("QuantityIsFixed", cliente, StringComparison.Ordinal);
        Assert.Contains("WithdrawnQuantity", cliente, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_FormExigeAlocacaoOkAntesDeMontarComponentes()
    {
        string form = SemComentarios(LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        Assert.Contains("alocacao.Cenario != CenarioAllocator261.Ok", form, StringComparison.Ordinal);
    }

    // ===================== §18 261 falhou => 101/HU nao avancam =====================

    [Fact]
    public void AlocacaoBloqueada_ProduzOrigemSemComponentes_ECommandBuilderBloqueia()
    {
        Resultado261Cumulativo bloqueada = Resultado261Cumulativo.Bloqueado("guard de teste");

        Assert.Empty(ProcessoProdutoAcabadoForm.MontarComponentesPipelineRuntime(bloqueada));

        ProdutoAcabadoCaixa caixa = new()
        {
            NumeroOrdemProducao = Op, ItemOrdemProducao = "1", Material = "4000174", Lote = "169 26",
            Centro = "3007", Deposito = "PA01", QuantidadeProdutos = 8, UnidadeQuantidade = "UN",
            PesoBrutoKg = 14.400m, TaraKg = 0.050m, PesoLiquidoKg = 14.350m
        };
        caixa.CodigoProdutoAcabadoCaixa = 1;

        ProdutoAcabadoPipelineOrigem origem = ProcessoProdutoAcabadoForm.MontarOrigemPipelineRuntime(
            caixa, null, new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc), bloqueada);

        ResultadoComandosPipeline comandos = ProdutoAcabadoPipelineCommandBuilder.Construir(origem);

        // Sem componentes nao ha command 261 — logo nao ha 101 nem HU: o pipeline nao comeca.
        Assert.False(comandos.Sucesso);
        Assert.Null(comandos.Comando261);
        Assert.Null(comandos.Comando101);
    }

    [Fact]
    public void AlocacaoNadaAEnviar_TambemNaoMontaComponentes()
    {
        Resultado261Cumulativo nada = new(
            CenarioAllocator261.NadaAEnviar, "nada", [], 8m, ["3092/1"]);

        Assert.Empty(ProcessoProdutoAcabadoForm.MontarComponentesPipelineRuntime(nada));
    }

    [Fact]
    public void AlocacaoNula_NaoMontaComponentes()
        => Assert.Empty(ProcessoProdutoAcabadoForm.MontarComponentesPipelineRuntime(null));

    // ===================== arredondamento do contrato =====================

    [Fact]
    public void Arredondamento_UsaTresCasasAwayFromZero()
    {
        // 2.001 * 1 / 2 = 1.0005 -> 1.001 com AwayFromZero; ToEven devolveria 1.000.
        Assert.Equal(
            1.001m,
            ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativo(2.001m, 1m, 2m));
    }

    [Fact]
    public void Arredondamento_FechamentoExatoNaoArredonda()
    {
        // Produzido == planejado: devolve a necessidade integral, sem passar pelo Round.
        Assert.Equal(
            56.102m,
            ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativo(56.102m, 9016m, 9016m));
    }

    [Fact]
    public void DerivarLedger_ReproduzOsAlvosDaProducaoAnterior()
    {
        IReadOnlyDictionary<string, decimal>? ledger =
            ProdutoAcabado261AllocatorCumulativo.DerivarLedgerLocalEsperado(
                ComponentesFixture(), produzidoAnteriorConfirmado: 8m, planejadoOp: PlanejadoFixture);

        Assert.NotNull(ledger);
        Assert.Equal(1.278m, ledger!["3092/1"]);
        Assert.Equal(0.890m, ledger["3092/2"]);
        Assert.Equal(0.050m, ledger["3092/5"]);
    }

    [Theory]
    [InlineData(null, 9016)]
    [InlineData(0, 0)]
    [InlineData(-1, 9016)]
    public void DerivarLedger_EntradaInvalida_RetornaNullParaBloquear(int? anterior, int planejado)
        => Assert.Null(ProdutoAcabado261AllocatorCumulativo.DerivarLedgerLocalEsperado(
            ComponentesFixture(),
            anterior is int a ? a : null,
            planejado == 0 ? 0m : planejado));

    [Fact]
    public void FormatacaoDoDelta_CabeNaEscala3DoContrato()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(ComponentesFixture(), priorProduzido: 0m, caixaCorrente: 8m));

        // O adaptador 261 serializa com "0.###": o delta nao pode perder informacao nessa escala.
        foreach (Item261Alocado item in r.Itens)
        {
            string serializado = item.Delta261.ToString("0.###", CultureInfo.InvariantCulture);
            Assert.Equal(item.Delta261, decimal.Parse(serializado, CultureInfo.InvariantCulture));
        }
    }
}
