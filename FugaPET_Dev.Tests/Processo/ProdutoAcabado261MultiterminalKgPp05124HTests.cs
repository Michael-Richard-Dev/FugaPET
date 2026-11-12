using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// GATE 124H: o acumulado real do 261 (componente KG/PP05) não pode ser particionado por TERMINAL.
/// <para>
/// Defeito comprovado pelo Hermes no candidato 2baab29c: o ledger local vinha de
/// <c>ListarPorContextoAsync(..., terminal)</c>, enquanto o <c>WithdrawnQuantity</c> do SAP é
/// cumulativo por componente (Reservation + ReservationItem) e não conhece terminal. Escopos
/// diferentes ⇒ subconsumo silencioso quando a mesma OP é produzida em mais de um terminal.
/// </para>
/// <para>
/// Nenhum SAP, nenhum banco, nenhum POST: o alocador e o agregador são puros.
/// </para>
/// </summary>
public sealed class ProdutoAcabado261MultiterminalKgPp05124HTests
{
    private const string Op = "1000210";
    private const decimal Planejado = 9016m;
    private const string Material = "2000219";
    private const string Lote = "169 26";
    private const string T1 = "TERM-01";
    private const string T2 = "TERM-02";

    // ===================== fixtures =====================

    /// <summary>Componente real da OP 1000210 comprovado pelo ARES 124F: KG + PP05, reserva 795/1.</summary>
    private static Componente261Fresco CompKgPp05(decimal necessaria = 1440.000m, decimal? retirado = 0m, string item = "1")
        => new()
        {
            NumeroOrdem = Op,
            Reservation = "795",
            ReservationItem = item,
            Material = Material,
            Plant = "3007",
            StorageLocation = "PP05",
            Batch = Lote,
            BaseUnit = "KG",
            TipoMovimento = "261",
            RequiredQuantity = necessaria,
            WithdrawnQuantity = retirado,
            ConfirmedAvailableQuantity = necessaria,
            QuantityIsFixed = false
        };

    private static Componente261Fresco Comp(string item, string material, decimal necessaria, string unidade, string deposito)
        => CompKgPp05(necessaria, 0m, item) with
        {
            Material = material, BaseUnit = unidade, StorageLocation = deposito
        };

    private static ProdutoAcabadoCaixa Caixa(
        long? codigo, decimal pesoLiquido, StatusIntegracaoCaixa status, string terminal,
        string op = Op, string lote = Lote, string item = "1", string material = "4000174")
    {
        ProdutoAcabadoCaixa c = new()
        {
            NumeroOrdemProducao = op,
            ItemOrdemProducao = item,
            Material = material,
            Lote = lote,
            Centro = "3007",
            Deposito = "PA01",
            PesoLiquidoKg = pesoLiquido,
            Terminal = terminal
        };
        c.CodigoProdutoAcabadoCaixa = codigo;
        c.StatusIntegracao = status;
        return c;
    }

    private static Entrada261Cumulativa Entrada(
        IReadOnlyList<Componente261Fresco> componentes,
        decimal? pesoCorrente,
        decimal? pesoAnterior,
        decimal? priorProduzido = 8m)
        => new()
        {
            NumeroOrdem = Op,
            NumeroOrdemItemFresco = Op,
            PlannedProductionOp = Planejado,
            PriorProducedConfirmed = priorProduzido,
            CurrentBoxProduction = 8m,
            ProductionUnit = "UN",
            Componentes = componentes,
            ComponentesCompletosComprovado = true,
            PesoLiquidoCaixaCorrenteKg = pesoCorrente,
            PesoLiquidoConfirmadoAnteriorKg = pesoAnterior,
            ConsumoLocalConfirmadoPorComponente =
                ProdutoAcabado261AllocatorCumulativo.DerivarLedgerLocalEsperado(
                    componentes, priorProduzido, Planejado, pesoAnterior)
        };

    /// <summary>Agregação como a consulta FOCAL do 124H entrega: contexto sem terminal, só CONFIRMADA_SAP.</summary>
    private static decimal AcumuladoSemTerminal(IReadOnlyList<ProdutoAcabadoCaixa> todas, long? correnteCodigo)
        => ProdutoAcabado261AllocatorCumulativo.SomarPesoLiquidoConfirmadoAnterior(
            [.. todas.Where(c => c.StatusIntegracao == StatusIntegracaoCaixa.ConfirmadaSap)],
            correnteCodigo);

    /// <summary>Agregação como o candidato DEFEITUOSO fazia: particionada por terminal.</summary>
    private static decimal AcumuladoParticionadoPorTerminal(
        IReadOnlyList<ProdutoAcabadoCaixa> todas, long? correnteCodigo, string terminal)
        => ProdutoAcabado261AllocatorCumulativo.SomarPesoLiquidoConfirmadoAnterior(
            [.. todas.Where(c => c.StatusIntegracao == StatusIntegracaoCaixa.ConfirmadaSap
                                 && string.Equals(c.Terminal, terminal, StringComparison.OrdinalIgnoreCase))],
            correnteCodigo);

    // ===================== TEST_MULTITERMINAL_HERMES_REPRO =====================

    [Fact]
    public void TEST_MULTITERMINAL_HERMES_REPRO()
    {
        // T1 confirmadas = 50 KG · T2 confirmadas = 20 KG · corrente T1 = 10 KG · Withdrawn = 50 KG
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2),
            Caixa(3, 10.000m, StatusIntegracaoCaixa.FinalizadaLocal, T1)   // corrente
        ];

        // (1) O escopo CORRIGIDO vê os dois terminais.
        decimal real = AcumuladoSemTerminal(todas, 3);
        Assert.Equal(70.000m, real);

        // (2) O escopo DEFEITUOSO via só o terminal corrente — é o defeito que o Hermes reproduziu.
        decimal defeituoso = AcumuladoParticionadoPorTerminal(todas, 3, T1);
        Assert.Equal(50.000m, defeituoso);
        Assert.NotEqual(real, defeituoso);

        // (3) Com o escopo correto, a divergência contra o Withdrawn (50) é DETECTADA e BLOQUEIA.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 50.000m)], pesoCorrente: 10.000m, pesoAnterior: real));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("BLOCK_RECONCILIATION_REQUIRED", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void HERMES_REPRO_OCandidatoAntigoTeriaSubconsumido20Kg()
    {
        // Prova aritmética do subconsumo silencioso do candidato antigo, sem reintroduzi-lo.
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2),
            Caixa(3, 10.000m, StatusIntegracaoCaixa.FinalizadaLocal, T1)
        ];
        const decimal withdrawn = 50.000m;

        // Antigo: ledger 50 == Withdrawn 50 ⇒ reconciliação PASSAVA, target 60, delta 10, SAP ficaria 60.
        decimal antigo = AcumuladoParticionadoPorTerminal(todas, 3, T1);
        decimal targetAntigo = ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativoReal(antigo, 10.000m);
        Assert.Equal(50.000m, antigo);
        Assert.Equal(60.000m, targetAntigo);
        Assert.Equal(10.000m, targetAntigo - withdrawn);

        // Real cumulativo seria 80 ⇒ 20 KG de subconsumo silencioso.
        decimal realCumulativo = ProdutoAcabado261AllocatorCumulativo.CalcularAlvoCumulativoReal(
            AcumuladoSemTerminal(todas, 3), 10.000m);
        Assert.Equal(80.000m, realCumulativo);
        Assert.Equal(20.000m, realCumulativo - targetAntigo);
    }

    // ===================== A. MULTITERMINAL_PURE_POST_CUTOVER =====================

    [Fact]
    public void A_MultiterminalPosCutover_TargetOitentaDeltaDez()
    {
        // T1=50 · T2=20 · Withdrawn=70 (SAP já reflete os dois terminais) · corrente=10
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2),
            Caixa(3, 10.000m, StatusIntegracaoCaixa.FinalizadaLocal, T1)
        ];

        decimal anterior = AcumuladoSemTerminal(todas, 3);
        Assert.Equal(70.000m, anterior);

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 70.000m)], pesoCorrente: 10.000m, pesoAnterior: anterior));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Item261Alocado item = Assert.Single(r.Itens);
        Assert.Equal(80.000m, item.TargetCumulative);
        Assert.Equal(70.000m, item.WithdrawnFresco);
        Assert.Equal(10.000m, item.Delta261);
    }

    [Fact]
    public void A_TresTerminais_TodosEntramNoAcumulado()
    {
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 11.500m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 12.250m, StatusIntegracaoCaixa.ConfirmadaSap, T2),
            Caixa(3, 13.125m, StatusIntegracaoCaixa.ConfirmadaSap, "TERM-03"),
            Caixa(4, 10.000m, StatusIntegracaoCaixa.FinalizadaLocal, T2)
        ];

        Assert.Equal(36.875m, AcumuladoSemTerminal(todas, 4));
    }

    // ===================== B. SAME_TERMINAL_REGRESSION =====================

    [Fact]
    public void B_MesmoTerminal_ComportamentoPreservado()
    {
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 22.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 21.500m, StatusIntegracaoCaixa.FinalizadaLocal, T1)
        ];

        // Sem segundo terminal, o escopo novo e o antigo coincidem.
        Assert.Equal(22.000m, AcumuladoSemTerminal(todas, 2));
        Assert.Equal(22.000m, AcumuladoParticionadoPorTerminal(todas, 2, T1));

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 22.000m)], pesoCorrente: 21.500m, pesoAnterior: 22.000m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(43.500m, Assert.Single(r.Itens).TargetCumulative);
        Assert.Equal(21.500m, Assert.Single(r.Itens).Delta261);
    }

    // ===================== C / D. outra OP e outro lote excluídos =====================

    [Fact]
    public void C_OutroTerminalOutraOp_Excluido()
    {
        // A consulta focal filtra OP por igualdade; outra OP não entra nem de outro terminal.
        List<ProdutoAcabadoCaixa> contexto =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2)
        ];
        ProdutoAcabadoCaixa outraOp = Caixa(9, 99.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2, op: "1000242");

        // Simula o que a consulta devolve: SOMENTE o contexto pedido (outraOp não é retornada).
        Assert.Equal(70.000m, AcumuladoSemTerminal(contexto, 3));
        Assert.DoesNotContain(outraOp.NumeroOrdemProducao, contexto.Select(c => c.NumeroOrdemProducao));
    }

    [Fact]
    public void D_OutroTerminalOutroLote_Excluido()
    {
        List<ProdutoAcabadoCaixa> contexto =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2)
        ];
        ProdutoAcabadoCaixa outroLote = Caixa(9, 99.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2, lote: "170 26");

        Assert.Equal(70.000m, AcumuladoSemTerminal(contexto, 3));
        Assert.DoesNotContain(outroLote.Lote, contexto.Select(c => c.Lote));
    }

    [Fact]
    public void CD_ConsultaFocalExigeContextoCompletoEFiltraStatus()
    {
        // Prova no SOURCE da consulta focal: igualdade de OP/item/material/lote, status CONFIRMADA_SAP,
        // e NENHUM filtro por terminal.
        string repo = LerFonte("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");
        int i = repo.IndexOf("ListarConfirmadasPorContextoSemTerminalAsync", StringComparison.Ordinal);
        Assert.True(i >= 0);
        int fim = repo.IndexOf("INC-047: leitura por HU externo", i, StringComparison.Ordinal);
        string corpo = fim > i ? repo[i..fim] : repo[i..];

        Assert.Contains("numero_ordem_producao=@op", corpo, StringComparison.Ordinal);
        Assert.Contains("btrim(item_ordem_producao)=btrim(@item)", corpo, StringComparison.Ordinal);
        Assert.Contains("btrim(material)=btrim(@material)", corpo, StringComparison.Ordinal);
        Assert.Contains("btrim(lote)=btrim(@lote)", corpo, StringComparison.Ordinal);
        Assert.Contains("status_hu_caixa=@status", corpo, StringComparison.Ordinal);
        Assert.Contains("StatusIntegracaoCaixa.ConfirmadaSap", corpo, StringComparison.Ordinal);

        // O defeito corrigido: não existe FILTRO por terminal nesta consulta. A asserção é sobre o
        // predicado/parâmetro, não sobre a palavra — o próprio nome do método contém "SemTerminal".
        Assert.DoesNotContain("terminal)=", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("terminal=@", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("btrim(terminal", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AddWithValue(\"t\"", corpo, StringComparison.Ordinal);
    }

    // ===================== E / F. corrente e cancelada excluídas =====================

    [Fact]
    public void E_CaixaCorrenteExcluidaDoAcumuladoAnterior()
    {
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            // corrente JÁ confirmada (cenário de reexecução): mesmo assim não entra como "anterior"
            Caixa(2, 10.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2)
        ];

        Assert.Equal(50.000m, AcumuladoSemTerminal(todas, 2));
    }

    [Fact]
    public void F_CanceladaDeOutroTerminalExcluida()
    {
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 99.000m, StatusIntegracaoCaixa.Cancelada, T2),
            Caixa(3, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2),
            Caixa(4, 10.000m, StatusIntegracaoCaixa.FinalizadaLocal, T1)
        ];

        Assert.Equal(70.000m, AcumuladoSemTerminal(todas, 4));
    }

    [Fact]
    public void F_EstadosNaoConfirmadosDeOutroTerminalExcluidos()
    {
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 10.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 99.000m, StatusIntegracaoCaixa.EmPesagem, T2),
            Caixa(3, 99.000m, StatusIntegracaoCaixa.EnviandoSap, T2),
            Caixa(4, 99.000m, StatusIntegracaoCaixa.ErroSap, T2),
            Caixa(5, 99.000m, StatusIntegracaoCaixa.IndeterminadoTimeout, T2),
            Caixa(6, 99.000m, StatusIntegracaoCaixa.Bloqueada, T2),
            Caixa(null, 99.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2),   // sem identidade persistida
            Caixa(7, 5.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2)
        ];

        Assert.Equal(15.000m, AcumuladoSemTerminal(todas, 100));
    }

    // ===================== G / H / I. escopo preservado =====================

    [Fact]
    public void G_DoisComponentesKgPp05_ContinuamBloqueados()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada(
                [CompKgPp05(1440.000m, 0m, "1"), CompKgPp05(56.102m, 0m, "5")],
                pesoCorrente: 10.000m, pesoAnterior: 70.000m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Equal(
            ProdutoAcabado261AllocatorCumulativo.MotivoMultiplosComponentesConsumoReal, r.Mensagem);
    }

    [Fact]
    public void H_KgPp01_Inalterado()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([Comp("1", Material, 1440.000m, "KG", "PP01")], pesoCorrente: 10.000m, pesoAnterior: 70.000m, priorProduzido: 0m));

        Assert.Equal(1.278m, Assert.Single(r.Itens).Delta261);   // proporcional 118B
    }

    [Fact]
    public void I_Un_Inalterado()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([Comp("2", "3000046", 1003m, "UN", "PP05")], pesoCorrente: 10.000m, pesoAnterior: 70.000m, priorProduzido: 0m));

        Assert.Equal(0.890m, Assert.Single(r.Itens).Delta261);
    }

    // ===================== §7 cutover: divergência histórica continua bloqueando =====================

    [Fact]
    public void Cutover_DivergenciaHistoricaNaoEhCompensadaAutomaticamente()
    {
        // Ledger real 70 contra Withdrawn 50: bloqueia, NÃO compensa os 20 que faltam.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 50.000m)], pesoCorrente: 10.000m, pesoAnterior: 70.000m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("BLOCK_RECONCILIATION_REQUIRED", r.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// GATE 124H — propriedade ESTRUTURAL que DECLARO: no caminho REAL (KG/PP05), com ledger
    /// consistente (<c>withdrawn == anterior</c>), o delta é sempre <c>+corrente</c>. Logo nem
    /// delta ZERO nem delta NEGATIVO são alcançáveis ali: qualquer <c>withdrawn != anterior</c> é
    /// capturado ANTES, pela reconciliação. Os dois guards seguem vivos e são exercitados no
    /// caminho PROPORCIONAL (suíte 124E).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(70)]
    [InlineData(1000)]
    public void Cutover_CaminhoReal_LedgerConsistenteNuncaProduzDeltaZeroNemNegativo(int anterior)
    {
        decimal ant = anterior;
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: ant)], pesoCorrente: 10.000m, pesoAnterior: ant));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(10.000m, Assert.Single(r.Itens).Delta261);
        Assert.True(Assert.Single(r.Itens).Delta261 > 0m);
    }

    [Fact]
    public void Cutover_WithdrawnAcimaDoAnterior_EhCapturadoPelaReconciliacaoAntesDoDelta()
    {
        // withdrawn 90 > anterior 70: a divergência é detectada primeiro — não vira delta negativo
        // nem compensação automática.
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 90.000m)], pesoCorrente: 10.000m, pesoAnterior: 70.000m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);
        Assert.Contains("BLOCK_RECONCILIATION_REQUIRED", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Cutover_GuardDeDeltaNegativoContinuaVivoNoCaminhoProporcional()
    {
        // KG/PP01 (proporcional): retirado 2000 contra alvo ~1,278 ⇒ delta negativo ⇒ BLOCK.
        Componente261Fresco proporcional = Comp("1", Material, 1440.000m, "KG", "PP01") with
        {
            WithdrawnQuantity = 2000.000m
        };

        Entrada261Cumulativa entrada = new()
        {
            NumeroOrdem = Op,
            NumeroOrdemItemFresco = Op,
            PlannedProductionOp = Planejado,
            PriorProducedConfirmed = 0m,
            CurrentBoxProduction = 8m,
            ProductionUnit = "UN",
            Componentes = [proporcional],
            ComponentesCompletosComprovado = true,
            PesoLiquidoCaixaCorrenteKg = 10.000m,
            PesoLiquidoConfirmadoAnteriorKg = 0m,
            ConsumoLocalConfirmadoPorComponente = new Dictionary<string, decimal> { ["795/1"] = 2000.000m }
        };

        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(entrada);

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Contains("NEGATIVO", r.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>
    /// GATE 124H — comportamento que DECLARO explicitamente: no caminho REAL (KG/PP05), um estado
    /// consistente SEMPRE produz delta &gt; 0, igual ao peso da caixa corrente. Isso porque
    /// <c>target = anterior + corrente</c> e, consistente, <c>withdrawn == anterior</c>; logo
    /// <c>delta == corrente</c>, e corrente &lt;= 0 já é bloqueado por guard próprio.
    /// <para>
    /// Consequência: o SKIP por delta zero NÃO é alcançável neste caminho em estado consistente — e
    /// não deveria ser, porque cada caixa nova consome o seu próprio peso. O SKIP por delta zero
    /// continua exercitado no caminho PROPORCIONAL (suíte 124E).
    /// </para>
    /// </summary>
    [Fact]
    public void Cutover_CaminhoReal_EstadoConsistenteSempreTemDeltaIgualAoPesoDaCaixa()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 70.000m)], pesoCorrente: 10.000m, pesoAnterior: 70.000m));

        Assert.Equal(CenarioAllocator261.Ok, r.Cenario);
        Assert.Equal(10.000m, Assert.Single(r.Itens).Delta261);
    }

    /// <summary>
    /// GATE 124H — reexecução ANTES de a caixa corrente virar CONFIRMADA_SAP: o SAP já tem 80 (o
    /// POST passou) mas o acumulado local das ANTERIORES ainda é 70, porque a corrente só entra no
    /// acumulado depois de fechar 101/HU. O resultado é BLOCK_RECONCILIATION_REQUIRED, não SKIP.
    /// <para>
    /// Isso é SEGURO — impede o segundo POST do mesmo peso — mas o relato ao operador é de
    /// divergência, não de "nada a enviar". Registro o comportamento em vez de mascará-lo.
    /// </para>
    /// </summary>
    [Fact]
    public void Cutover_ReexecucaoAntesDaConfirmacao_BloqueiaPorReconciliacao_NaoDuplicaConsumo()
    {
        Resultado261Cumulativo r = ProdutoAcabado261AllocatorCumulativo.Calcular(
            Entrada([CompKgPp05(retirado: 80.000m)], pesoCorrente: 10.000m, pesoAnterior: 70.000m));

        Assert.Equal(CenarioAllocator261.Bloqueado, r.Cenario);
        Assert.Empty(r.Itens);   // nenhum item ⇒ nenhum POST ⇒ sem consumo duplicado
        Assert.Contains("BLOCK_RECONCILIATION_REQUIRED", r.Mensagem, StringComparison.Ordinal);
    }

    /// <summary>Depois de a caixa virar CONFIRMADA_SAP, o acumulado a inclui e o estado fica coerente.</summary>
    [Fact]
    public void Cutover_AposConfirmacaoDaCorrente_AcumuladoPassaAIncluiLa()
    {
        List<ProdutoAcabadoCaixa> todas =
        [
            Caixa(1, 50.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1),
            Caixa(2, 20.000m, StatusIntegracaoCaixa.ConfirmadaSap, T2),
            Caixa(3, 10.000m, StatusIntegracaoCaixa.ConfirmadaSap, T1)   // antes era a corrente
        ];

        // Para a PRÓXIMA caixa (código 4), o acumulado anterior já é 80.
        Assert.Equal(80.000m, AcumuladoSemTerminal(todas, 4));
    }

    // ===================== wiring: o controller usa a consulta FOCAL =====================

    [Fact]
    public void Wiring_ControllerUsaAConsultaSemTerminalParaOAcumulado261()
    {
        string controller = SemComentarios(LerFonte("Controle", "Processo", "ProdutoAcabadoController.cs"));
        int i = controller.IndexOf("CalcularAlocacao261FrescaAsync", StringComparison.Ordinal);
        Assert.True(i >= 0);
        int fim = controller.IndexOf("public bool SapSimulado", i, StringComparison.Ordinal);
        string corpo = fim > i ? controller[i..fim] : controller[i..];

        Assert.Contains("ListarConfirmadasPorContextoSemTerminalAsync", corpo, StringComparison.Ordinal);
        // E NÃO usa mais a leitura por terminal nesse cálculo.
        Assert.DoesNotContain("ListarCaixasPersistidasPorContextoAsync", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("caixa.Terminal", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void Wiring_ListarPorContextoAsyncComTerminalPermaneceParaOsOutrosFluxos()
    {
        // O fix é focal: a consulta COM terminal continua existindo e sendo usada pelo reload da grid.
        string repo = LerFonte("AcessoDados", "Repositorio", "ProdutoAcabadoRepositorio.cs");
        Assert.Contains("public async Task<IReadOnlyList<ProdutoAcabadoCaixa>> ListarPorContextoAsync(", repo, StringComparison.Ordinal);
        Assert.Contains("upper(btrim(terminal))=upper(btrim(@t))", repo, StringComparison.Ordinal);

        string form = LerFonte("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        Assert.Contains("ListarCaixasPersistidasPorContextoAsync", form, StringComparison.Ordinal);
    }

    // ===================== infra =====================

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
