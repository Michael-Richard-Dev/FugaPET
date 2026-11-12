using System.Globalization;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// GATE R2 (FUGAPET-ENTRADA-MP-PO-QUANTITY-ZZ1-NET-WEIGHT-R2): no movimento 101 da Entrada,
/// QuantityInEntryUnit recebe a quantidade INTEGRAL do item do Pedido de Compra e o peso liquido
/// efetivamente pesado vai em ZZ1_PESO_ENTRADA_MMI / ZZ1_PESO_ENTRADA_MMIU.
/// <para>
/// Exemplo aprovado: pedido 1000.000 KG, pesagem 296.500 KG ⇒ o SAP contabiliza 1000, nao 296.5.
/// </para>
/// Nenhum teste faz POST, HTTP ou banco.
/// </summary>
public sealed class EntradaMpQuantidadePedidoZz1R2Tests
{
    private const string Pedido = "4500000010";
    private const string ItemPedido = "10";

    // ======================================================================
    // T1 / T2 — quantidade do pedido no campo padrao e peso liquido no ZZ1
    // ======================================================================

    [Fact] // T1: pedido 1000 KG + pesagem 296.500 KG ⇒ 1000.000 no padrao, 296.500 no ZZ1.
    public async Task T1_PedidoMilKg_Pesagem296500_QuantidadePadraoMilEZz1Peso()
    {
        EntradaProdutoItemEnvioSap lote = Lote(pesoLiquido: 296.500m, pesoBruto: 300.000m, quantidadePedido: 1000.000m);

        MaterialDocumentSapItemRequest item = await PosicaoUnicaAsync(lote, tipoMaterial: "ROH");

        Assert.Equal("1000.000", item.QuantityInEntryUnit);
        Assert.Equal("KG", item.EntryUnit);
        Assert.Equal("296.500", item.Zz1PesoEntradaMmi);
        Assert.Equal("KG", item.Zz1PesoEntradaMmiu);
        // O peso NAO substitui a quantidade, e a quantidade NAO substitui o peso.
        Assert.NotEqual(item.QuantityInEntryUnit, item.Zz1PesoEntradaMmi);
    }

    [Fact] // T2: a tara entra descontada — o ZZ1 recebe o LIQUIDO, nunca o bruto.
    public async Task T2_TaraDescontada_Zz1RecebeLiquidoNaoBruto()
    {
        // bruto 300.000 - tara 3.500 = liquido 296.500
        EntradaProdutoItemEnvioSap lote = Lote(pesoLiquido: 296.500m, pesoBruto: 300.000m, quantidadePedido: 1000.000m);

        MaterialDocumentSapItemRequest item = await PosicaoUnicaAsync(lote, tipoMaterial: "ROH");

        Assert.Equal("296.500", item.Zz1PesoEntradaMmi);
        Assert.DoesNotContain("300", item.Zz1PesoEntradaMmi!, StringComparison.Ordinal);
    }

    // ======================================================================
    // T3 / T4 — serializacao real e cultura invariante
    // ======================================================================

    [Fact] // T3: os dois campos ZZ1 atravessam a allowlist explicita de SerializarPayload.
    public async Task T3_SerializacaoReal_ContemOsDoisCamposZz1()
    {
        EntradaProdutoItemEnvioSap lote = Lote(296.500m, 300.000m, 1000.000m);
        MaterialDocumentSapItemRequest item = await PosicaoUnicaAsync(lote, "ROH");

        string json = MaterialDocumentSapApiClient.SerializarPayload(new MaterialDocumentSapRequest
        {
            PostingDate = new DateTime(2026, 10, 15),
            DocumentDate = new DateTime(2026, 10, 15),
            MaterialDocumentHeaderText = "R2",
            Itens = [item]
        });

        Assert.Contains("\"ZZ1_PESO_ENTRADA_MMI\":\"296.500\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ZZ1_PESO_ENTRADA_MMIU\":\"KG\"", json, StringComparison.Ordinal);
        Assert.Contains("\"QuantityInEntryUnit\":\"1000.000\"", json, StringComparison.Ordinal);
        Assert.Contains("to_MaterialDocumentItem", json, StringComparison.Ordinal);
    }

    [Fact] // T4: cultura invariante — ponto decimal, sem separador de milhar, 3 casas fixas.
    public void T4_CulturaInvariante_PontoDecimalSemMilhar()
    {
        Assert.Equal("1000.000", EntradaProdutoAlocacao101.FormatarDecimalContratoSap(1000m));
        Assert.Equal("296.500", EntradaProdutoAlocacao101.FormatarDecimalContratoSap(296.5m));
        Assert.Equal("1.500", EntradaProdutoAlocacao101.FormatarDecimalContratoSap(1.5m));
        Assert.Equal("12345.678", EntradaProdutoAlocacao101.FormatarDecimalContratoSap(12345.678m));
        foreach (string formatado in new[]
        {
            EntradaProdutoAlocacao101.FormatarDecimalContratoSap(1000m),
            EntradaProdutoAlocacao101.FormatarDecimalContratoSap(12345.678m)
        })
        {
            Assert.DoesNotContain(",", formatado, StringComparison.Ordinal);
        }
    }

    [Fact] // T4b: escala acima do contrato (15,3) bloqueia — nunca arredonda silenciosamente.
    public void T4b_EscalaAcimaDoContrato_Bloqueia()
    {
        Assert.False(EntradaProdutoAlocacao101.EscalaCompativelContratoSap(1.00049m));
        Assert.True(EntradaProdutoAlocacao101.EscalaCompativelContratoSap(296.500m));

        ResultadoAlocacao101 r = EntradaProdutoAlocacao101.Decidir(
            ClassificacaoEntradaMaterial.MateriaPrima, 1000.0001m, "KG", 296.5m, "10");
        Assert.Equal(CenarioAlocacao101.Bloqueada, r.Cenario);
        Assert.Contains("15,3", r.Motivo, StringComparison.Ordinal);
    }

    // ======================================================================
    // T5 — unidade KG e unidade incompativel
    // ======================================================================

    [Fact] // T5: somente KG neste incremento; qualquer outra unidade bloqueia sem presumir conversao.
    public void T5_UnidadeIncompativel_Bloqueia()
    {
        foreach (string unidade in new[] { "PC", "TO", "M2", "G" })
        {
            ResultadoAlocacao101 r = EntradaProdutoAlocacao101.Decidir(
                ClassificacaoEntradaMaterial.MateriaPrima, 1000m, unidade, 296.5m, "10");
            Assert.Equal(CenarioAlocacao101.Bloqueada, r.Cenario);
            Assert.Contains("nao suportada", r.Motivo, StringComparison.Ordinal);
            Assert.Null(r.PesoLiquidoCustomizado);
        }

        // Unidade ausente tambem bloqueia (nunca assume KG).
        ResultadoAlocacao101 semUnidade = EntradaProdutoAlocacao101.Decidir(
            ClassificacaoEntradaMaterial.MateriaPrima, 1000m, null, 296.5m, "10");
        Assert.Equal(CenarioAlocacao101.Bloqueada, semUnidade.Cenario);

        // KG com espacos/caixa diferente e aceito (normalizado).
        ResultadoAlocacao101 ok = EntradaProdutoAlocacao101.Decidir(
            ClassificacaoEntradaMaterial.MateriaPrima, 1000m, " kg ", 296.5m, "10");
        Assert.Equal(CenarioAlocacao101.MateriaPrimaComQuantidadeDoPedido, ok.Cenario);
    }

    // ======================================================================
    // T6 / T7 — ROH alterado, HIBE preservado
    // ======================================================================

    [Fact] // T6: ROH usa a quantidade do pedido e emite os dois campos ZZ1.
    public async Task T6_ItemRoh_UsaQuantidadeDoPedidoEEmiteZz1()
    {
        MaterialDocumentSapItemRequest item = await PosicaoUnicaAsync(
            Lote(296.500m, 300m, 1000m), tipoMaterial: "ROH");

        Assert.Equal("1000.000", item.QuantityInEntryUnit);
        Assert.NotNull(item.Zz1PesoEntradaMmi);
        Assert.NotNull(item.Zz1PesoEntradaMmiu);
    }

    [Fact] // T7: HIBE (Quimico) PRESERVADO — quantidade = peso liquido e NENHUM campo ZZ1.
    public async Task T7_ItemHibe_PreservaLegadoSemZz1()
    {
        MaterialDocumentSapItemRequest item = await PosicaoUnicaAsync(
            Lote(296.500m, 300m, 1000m), tipoMaterial: "HIBE");

        Assert.Equal("296.5", item.QuantityInEntryUnit); // formato legado "0.###"
        Assert.Equal("KG", item.EntryUnit);
        Assert.Null(item.Zz1PesoEntradaMmi);
        Assert.Null(item.Zz1PesoEntradaMmiu);

        // E o JSON nao carrega os campos customizados.
        string json = MaterialDocumentSapApiClient.SerializarPayload(new MaterialDocumentSapRequest
        {
            PostingDate = new DateTime(2026, 10, 15),
            DocumentDate = new DateTime(2026, 10, 15),
            MaterialDocumentHeaderText = "R2",
            Itens = [item]
        });
        Assert.DoesNotContain("ZZ1_", json, StringComparison.Ordinal);
    }

    [Fact] // T7b: classificacao INDETERMINADA bloqueia — o tipo do material nunca e presumido.
    public async Task T7b_ClassificacaoIndeterminada_Bloqueia()
    {
        ResultadoPreparacaoPayloadEntrada preparo = await PrepararAsync(
            [Lote(296.5m, 300m, 1000m)], tipoMaterial: string.Empty, administradoPorLote: true);

        Assert.Null(preparo.Posicoes);
        Assert.NotNull(preparo.Bloqueio);
        Assert.Contains("classificacao do material nao determinada", preparo.Bloqueio!.Mensagem!, StringComparison.Ordinal);
    }

    // ======================================================================
    // T8 / T9 — multiplos lotes e multiplos depositos sem duplicacao
    // ======================================================================

    [Fact] // T8: 2 lotes do mesmo item (material administrado) ⇒ BLOQUEIA, sem repetir a quantidade.
    public async Task T8_DoisLotesMesmoItem_BloqueiaSemDuplicarQuantidadeDoPedido()
    {
        EntradaProdutoItemEnvioSap loteA = Lote(100m, 101m, 1000m, numeroLote: "L-A") with { CodigoEntradaProdutoLote = 1 };
        EntradaProdutoItemEnvioSap loteB = Lote(196.5m, 198m, 1000m, numeroLote: "L-B") with { CodigoEntradaProdutoLote = 2 };

        ResultadoPreparacaoPayloadEntrada preparo = await PrepararAsync(
            [loteA, loteB], tipoMaterial: "ROH", administradoPorLote: true);

        Assert.Null(preparo.Posicoes);
        Assert.NotNull(preparo.Bloqueio);
        Assert.Contains("não pode ser repetida", preparo.Bloqueio!.Mensagem!, StringComparison.Ordinal);
        Assert.Contains("2 posições", preparo.Bloqueio.Mensagem!, StringComparison.Ordinal);
    }

    [Fact] // T9: 2 depositos do mesmo item (material NAO administrado) ⇒ BLOQUEIA igualmente.
    public async Task T9_DoisDepositosMesmoItem_BloqueiaSemDuplicarQuantidadeDoPedido()
    {
        EntradaProdutoItemEnvioSap d1 = Lote(100m, 101m, 1000m) with { Deposito = "PA01", CodigoEntradaProdutoLote = 1 };
        EntradaProdutoItemEnvioSap d2 = Lote(196.5m, 198m, 1000m) with { Deposito = "PA02", CodigoEntradaProdutoLote = 2 };

        ResultadoPreparacaoPayloadEntrada preparo = await PrepararAsync(
            [d1, d2], tipoMaterial: "ROH", administradoPorLote: false);

        Assert.Null(preparo.Posicoes);
        Assert.NotNull(preparo.Bloqueio);
        Assert.Contains("não pode ser repetida", preparo.Bloqueio!.Mensagem!, StringComparison.Ordinal);
    }

    [Fact] // T9b: HIBE com 2 lotes continua gerando 2 posicoes (comportamento legado NAO bloqueado).
    public async Task T9b_HibeComDoisLotes_NaoBloqueia()
    {
        EntradaProdutoItemEnvioSap loteA = Lote(100m, 101m, 1000m, numeroLote: "L-A") with { CodigoEntradaProdutoLote = 1 };
        EntradaProdutoItemEnvioSap loteB = Lote(196.5m, 198m, 1000m, numeroLote: "L-B") with { CodigoEntradaProdutoLote = 2 };

        ResultadoPreparacaoPayloadEntrada preparo = await PrepararAsync(
            [loteA, loteB], tipoMaterial: "HIBE", administradoPorLote: true);

        Assert.Null(preparo.Bloqueio);
        Assert.Equal(2, preparo.Posicoes!.Count);
        Assert.All(preparo.Posicoes!, p => Assert.Null(p.Item.Zz1PesoEntradaMmi));
        // Cada posicao leva o SEU peso, nunca um total.
        Assert.Equal(
            new[] { "100", "196.5" },
            preparo.Posicoes!.Select(p => p.Item.QuantityInEntryUnit).OrderBy(q => q, StringComparer.Ordinal));
    }

    // ======================================================================
    // T11 — quantidade documental ausente
    // ======================================================================

    [Fact] // T11: quantidade do pedido desconhecida ⇒ BLOQUEIA antes de qualquer payload/POST.
    public async Task T11_QuantidadeDocumentalAusente_Bloqueia()
    {
        ResultadoPreparacaoPayloadEntrada preparo = await PrepararAsync(
            [Lote(296.5m, 300m, quantidadePedido: null)], tipoMaterial: "ROH", administradoPorLote: true);

        Assert.Null(preparo.Posicoes);
        Assert.NotNull(preparo.Bloqueio);
        Assert.Contains("quantidade do item do Pedido de Compra desconhecida",
            preparo.Bloqueio!.Mensagem!, StringComparison.Ordinal);

        // Zero tambem bloqueia (ausencia nunca vira zero, e zero nao e quantidade valida).
        ResultadoAlocacao101 zero = EntradaProdutoAlocacao101.Decidir(
            ClassificacaoEntradaMaterial.MateriaPrima, 0m, "KG", 296.5m, "10");
        Assert.Equal(CenarioAlocacao101.Bloqueada, zero.Cenario);
    }

    [Fact] // R2: par ZZ1 e TUDO-OU-NADA em todo cenario bloqueado/legado.
    public void R2_ParCustomizado_EhTudoOuNada()
    {
        ResultadoAlocacao101 bloqueada = EntradaProdutoAlocacao101.Decidir(
            ClassificacaoEntradaMaterial.Indefinido, 1000m, "KG", 296.5m, "10");
        Assert.Null(bloqueada.PesoLiquidoCustomizado);
        Assert.Null(bloqueada.UnidadePesoCustomizado);
        Assert.False(bloqueada.UsaQuantidadeDoPedido);

        ResultadoAlocacao101 legado = EntradaProdutoAlocacao101.Decidir(
            ClassificacaoEntradaMaterial.Quimico, 1000m, "KG", 296.5m, "10");
        Assert.Null(legado.PesoLiquidoCustomizado);
        Assert.Null(legado.UnidadePesoCustomizado);
        Assert.False(legado.UsaQuantidadeDoPedido);
    }

    // ======================================================================
    // Helpers — sem HTTP, sem banco, sem POST
    // ======================================================================

    private static EntradaProdutoItemEnvioSap Lote(
        decimal pesoLiquido,
        decimal pesoBruto,
        decimal? quantidadePedido,
        string? numeroLote = null)
        => new()
        {
            NumeroPedido = Pedido,
            NumeroItem = ItemPedido,
            PesoLiquidoKg = pesoLiquido,
            PesoBrutoKg = pesoBruto,
            Material = "2000205",
            Centro = "3007",
            Deposito = "PA01",
            Unidade = "KG",
            QuantidadePedidoItem = quantidadePedido,
            NumeroLote = numeroLote ?? "L-1",
            DataFabricacao = new DateTime(2026, 1, 10),
            DataValidade = new DateTime(2027, 1, 10),
            CodigoEntradaProdutoLote = 1
        };

    private static Task<ResultadoPreparacaoPayloadEntrada> PrepararAsync(
        IReadOnlyList<EntradaProdutoItemEnvioSap> lotes,
        string tipoMaterial,
        bool administradoPorLote)
        => PreparadorPayloadMaterialDocumentEntrada.PrepararAsync(
            99,
            Pedido,
            lotes,
            (material, centro, _) => Task.FromResult<ProdutoCentroSapMestre?>(
                new ProdutoCentroSapMestre
                {
                    Material = material,
                    Centro = centro,
                    IsBatchManagementRequired = administradoPorLote,
                    Consultado = true
                }),
            (material, _) => Task.FromResult<ProdutoSapMestre?>(
                string.IsNullOrWhiteSpace(tipoMaterial)
                    ? null
                    : new ProdutoSapMestre
                    {
                        CodigoProduto = material,
                        TipoMaterialSap = tipoMaterial,
                        Consultado = true
                    }),
            CancellationToken.None);

    private static async Task<MaterialDocumentSapItemRequest> PosicaoUnicaAsync(
        EntradaProdutoItemEnvioSap lote, string tipoMaterial)
    {
        ResultadoPreparacaoPayloadEntrada preparo = await PrepararAsync([lote], tipoMaterial, administradoPorLote: true);
        Assert.Null(preparo.Bloqueio?.Mensagem);
        return Assert.Single(preparo.Posicoes!).Item;
    }
}
