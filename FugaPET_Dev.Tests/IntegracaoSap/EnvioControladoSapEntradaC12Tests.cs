using FugaPET_Dev.Controle;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Servicos.Seguranca;
using System.Globalization;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Envio CONTROLADO da Entrada ao SAP via Material Document (movimento 101), separado da finalizacao
/// local. Garante que o Finalizar nao chama SAP, que o envio NAO usa PATCH no Pedido de Compra, que
/// cria um unico documento de material por lancamento com os itens elegiveis, e que as travas
/// (HOMOLOGACAO + permissao + escrita + Material Document configurado + dados obrigatorios) sao
/// reaplicadas antes do POST.
/// </summary>
public sealed class EnvioControladoSapEntradaC12Tests : IDisposable
{
    public EnvioControladoSapEntradaC12Tests()
    {
        Environment.SetEnvironmentVariable(
            "FUGAPET_DEV_CONEXAO_POSTGRES",
            "Host=localhost;Port=5432;Database=teste;Username=teste;Password=teste");
        EstadoSessaoUsuarioAtual.Limpar();
    }

    public void Dispose() => EstadoSessaoUsuarioAtual.Limpar();

    [Fact]
    public async Task FinalizarLeitura_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico pedido = new();
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller =
            CriarController(pedido, materialDoc, ehHomologacao: true, itens: [Item()]);

        await controller.FinalizarLeituraAsync(Lancamento());

        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Equal(0, pedido.PatchChamadas);
    }

    [Fact]
    public async Task Enviar_ComTodasAsTravas_DeveCriarUmUnicoDocumentoMaterialSemPatch()
    {
        DefinirSessao(comEnviarSap: true);
        FakePedidoCompraSapServico pedido = new() { EscritaHabilitada = true };
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller =
            CriarController(pedido, materialDoc, ehHomologacao: true, itens: [Item("10"), Item("20")]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas); // um unico documento por lancamento
        Assert.Equal(0, pedido.PatchChamadas); // Entrada nao usa PATCH no Pedido de Compra
        Assert.Equal(2, resultado.Total);
        Assert.True(resultado.StatusLocalAtualizado);
        Assert.NotNull(materialDoc.UltimaRequisicao);
        Assert.Equal(2, materialDoc.UltimaRequisicao!.Itens.Count);
    }

    [Fact]
    public async Task Enviar_DeveMontarPayloadComMovimento101EItemNormalizado()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10")]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        MaterialDocumentSapRequest req = materialDoc.UltimaRequisicao!;
        Assert.Equal("01", req.GoodsMovementCode);
        MaterialDocumentSapItemRequest item = Assert.Single(req.Itens);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("B", item.GoodsMovementRefDocType);
        Assert.Equal("4500000010", item.PurchaseOrder);
        Assert.Equal("00010", item.PurchaseOrderItem); // 5 digitos
        Assert.Equal("KG", item.EntryUnit);
        Assert.Equal("3500027", item.Material);
        Assert.Equal("3007", item.Plant);
        Assert.Equal("PP01", item.StorageLocation);
        // QuantityInEntryUnit = peso liquido em kg
        Assert.Equal(
            8m,
            decimal.Parse(item.QuantityInEntryUnit, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Enviar_DeveMontarTextoCabecalhoCurtoMax25EAscii()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10")]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        string texto = materialDoc.UltimaRequisicao!.MaterialDocumentHeaderText;
        Assert.True(texto.Length <= 25, $"texto cabecalho excede 25: '{texto}' ({texto.Length})");
        Assert.Contains("4500000010", texto);                 // numero do pedido
        Assert.Contains("99", texto);                          // codigo do lancamento
        Assert.Matches("^[A-Za-z0-9 ]+$", texto);              // ASCII, sem acentos/caracteres perigosos
        Assert.DoesNotContain("Entrada", texto);               // nao envia mais o texto longo antigo
    }

    [Fact]
    public async Task Enviar_TextoCabecalho_DeveRemoverAcentosECaracteresEspeciais()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap itemEstranho = Item("10") with { NumeroPedido = "45000-ção/01" };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [itemEstranho]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(77);

        string texto = materialDoc.UltimaRequisicao!.MaterialDocumentHeaderText;
        Assert.Matches("^[A-Za-z0-9 ]+$", texto);   // sem 'ç'/'ã' nem '-' '/'
        Assert.True(texto.Length <= 25);
        Assert.Contains("77", texto);
    }

    [Theory]
    [InlineData("KG")]
    [InlineData("KGM")]
    [InlineData("UN")]
    [InlineData("PC")]
    [InlineData("ST")]
    [InlineData("PAL")]
    [InlineData("CX")]
    public async Task Enviar_UnidadeComercialDoPedido_DeveEnviarPesoLiquidoSempreEmKg(string unidadePedido)
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10", unidade: unidadePedido)]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas);
        MaterialDocumentSapItemRequest item = Assert.Single(materialDoc.UltimaRequisicao!.Itens);
        Assert.Equal("KG", item.EntryUnit);
        Assert.Equal(
            8m,
            decimal.Parse(item.QuantityInEntryUnit, CultureInfo.InvariantCulture));
        Assert.Equal("B", item.GoodsMovementRefDocType);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("4500000010", item.PurchaseOrder);
        Assert.Equal("00010", item.PurchaseOrderItem);
    }

    [Fact]
    public async Task Enviar_ItemComPesoLiquidoZero_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap semPeso = Item("10", unidade: "UN") with { PesoLiquidoKg = 0m };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [semPeso]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Contains("peso", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enviar_FalhaSap_DeveExibirMensagemSanitizadaDoMaterialDocument()
    {
        DefinirSessao(comEnviarSap: true);
        string mensagemSap =
            "Etapa POST_DOCUMENTO_MATERIAL: HTTP 400 Bad Request. "
            + "O SAP rejeitou a entrada em KG para este item do pedido.";
        FakeMaterialDocumentSapServico materialDoc = new()
        {
            Sucesso = false,
            MensagemFalha = mensagemSap
        };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item("10", unidade: "UN")]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Falha, resultado.Cenario);
        Assert.Equal(mensagemSap, resultado.Mensagem);
        Assert.Equal(mensagemSap, resultado.MensagemCritica);
        Assert.Contains(mensagemSap, resultado.Itens.Single().Mensagem);
        Assert.Equal(1, materialDoc.Chamadas);
    }


    [Fact]
    public async Task Enviar_ItemSemDadosObrigatorios_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap semCentro = Item("10") with { Centro = null };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [semCentro]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    // ===================================================================================================
    // SAP 12/019 (shelf life) — payload por LOTE com Batch/ManufactureDate/ShelfLifeExpirationDate. A..G.
    // ===================================================================================================

    // A. Um lote com fabricacao e validade ⇒ payload traz Batch, ManufactureDate e ShelfLifeExpirationDate.
    [Fact]
    public async Task Enviar_UmLoteComDatas_DevePreencherBatchManufactureEShelfLife()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        DateTime fab = new(2026, 1, 10);
        DateTime val = new(2027, 1, 10);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true,
            itens: [Item("10", numeroLote: "L-777", dataFabricacao: fab, dataValidade: val)]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        MaterialDocumentSapItemRequest item = Assert.Single(materialDoc.UltimaRequisicao!.Itens);
        Assert.Equal("L-777", item.Batch);
        Assert.Equal(fab, item.ManufactureDate);
        Assert.Equal(val, item.ShelfLifeExpirationDate);
    }

    // B. Dois lotes do mesmo item ⇒ duas posicoes SAP distintas, quantidades NAO agregadas, datas proprias.
    [Fact]
    public async Task Enviar_DoisLotesDoMesmoItem_GeraDuasPosicoesSemAgregarComDatasProprias()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap loteA = Item("10", numeroLote: "L-A", dataValidade: new DateTime(2027, 3, 1), pesoLiquido: 3m);
        EntradaProdutoItemEnvioSap loteB = Item("10", numeroLote: "L-B", dataValidade: new DateTime(2027, 9, 1), pesoLiquido: 5m);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [loteA, loteB]);

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        IReadOnlyList<MaterialDocumentSapItemRequest> itens = materialDoc.UltimaRequisicao!.Itens;
        Assert.Equal(2, itens.Count);
        Assert.All(itens, i => Assert.Equal("00010", i.PurchaseOrderItem)); // mesmo item do pedido
        Assert.Contains(itens, i => i.Batch == "L-A"
            && decimal.Parse(i.QuantityInEntryUnit, CultureInfo.InvariantCulture) == 3m);
        Assert.Contains(itens, i => i.Batch == "L-B"
            && decimal.Parse(i.QuantityInEntryUnit, CultureInfo.InvariantCulture) == 5m);
        // Quantidades NAO agregadas: nao existe uma posicao unica de 8 KG.
        Assert.DoesNotContain(itens, i => decimal.Parse(i.QuantityInEntryUnit, CultureInfo.InvariantCulture) == 8m);
        Assert.Equal(new DateTime(2027, 3, 1), itens.Single(i => i.Batch == "L-A").ShelfLifeExpirationDate);
        Assert.Equal(new DateTime(2027, 9, 1), itens.Single(i => i.Batch == "L-B").ShelfLifeExpirationDate);
    }

    // C. Validade ausente e obrigatoria ⇒ bloqueia ANTES do HTTP; POST nao executado.
    [Fact]
    public async Task Enviar_ValidadeAusente_DeveBloquearAntesDoPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap semValidade = Item("10") with { DataValidade = null };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [semValidade]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Contains("validade", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // D. Fabricacao presente e validade ausente ⇒ NAO calcula validade; bloqueia (sem POST).
    [Fact]
    public async Task Enviar_FabricacaoPresenteEValidadeAusente_NaoCalculaEBloqueia()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap item = Item("10", dataFabricacao: new DateTime(2026, 2, 1)) with { DataValidade = null };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [item]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Contains("validade", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // E. Validade anterior a fabricacao ⇒ bloqueia ANTES do POST.
    [Fact]
    public async Task Enviar_ValidadeAnteriorAFabricacao_DeveBloquearAntesDoPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap item = Item("10",
            dataFabricacao: DateTime.Today.AddDays(-1),
            dataValidade: DateTime.Today.AddDays(-2)); // validade antes da fabricacao
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [item]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Contains("fabrica", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // F. Serializacao ⇒ Batch como string e datas em OData V2 "/Date(ms)/" (mesmo contrato de PostingDate).
    [Fact]
    public void Serializar_ComLoteEDatas_ProduzBatchEDatasODataV2()
    {
        MaterialDocumentSapItemRequest item = new()
        {
            Material = "3500027", Plant = "3007", StorageLocation = "PP01",
            GoodsMovementType = "101", GoodsMovementRefDocType = "B",
            QuantityInEntryUnit = "1.000", EntryUnit = "KG",
            PurchaseOrder = "4500000010", PurchaseOrderItem = "00010",
            Batch = "L-1",
            ManufactureDate = new DateTime(2026, 1, 10),
            ShelfLifeExpirationDate = new DateTime(2027, 1, 10)
        };
        MaterialDocumentSapRequest req = new()
        {
            GoodsMovementCode = "01",
            PostingDate = new DateTime(2026, 7, 29),
            DocumentDate = new DateTime(2026, 7, 29),
            MaterialDocumentHeaderText = "FP 4500000010 L99",
            Itens = [item]
        };

        string json = MaterialDocumentSapApiClient.SerializarPayload(req);

        long fabMs = new DateTimeOffset(DateTime.SpecifyKind(new DateTime(2026, 1, 10), DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        long valMs = new DateTimeOffset(DateTime.SpecifyKind(new DateTime(2027, 1, 10), DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        Assert.Contains("\"Batch\":\"L-1\"", json);
        Assert.Contains($"\"ManufactureDate\":\"/Date({fabMs})/\"", json);
        Assert.Contains($"\"ShelfLifeExpirationDate\":\"/Date({valMs})/\"", json);
    }

    // G. Reenvio apos HTTP 400 ⇒ nao perde lancamento/lotes, nao cria nova persistencia/documento e
    //    reabre para reenvio (status local -> ERRO_SAP), com um unico POST por acao.
    [Fact]
    public async Task Enviar_Falha400_PreservaLancamentoEReabreParaReenvioComoErroSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new()
        {
            Sucesso = false,
            MensagemFalha = "Etapa POST_DOCUMENTO_MATERIAL: HTTP 400 Bad Request."
        };
        CenarioEnvioSapEntrada? cenarioStatusLocal = null;
        RastreabilidadeDocumentoMaterialSap? rastreabilidade = null;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [Item("10")],
            atualizarStatus: (_, _, cenario, rastro, _) =>
            {
                cenarioStatusLocal = cenario;
                rastreabilidade = rastro;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Falha, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas);                             // um unico POST por acao
        Assert.Equal(CenarioEnvioSapEntrada.Falha, cenarioStatusLocal);    // status local -> ERRO_SAP (reenvio liberado)
        Assert.Null(rastreabilidade);                                      // nenhum documento/rastreabilidade gravado
        Assert.True(resultado.StatusLocalAtualizado);                      // persistencia local preservada
        Assert.NotEqual(CenarioEnvioSapEntrada.FalhaPersistenciaLocal, resultado.Cenario);
    }

    // ===================================================================================================
    // MM_IM_ODATA_API_MDOC/014 — payload CONDICIONAL por administracao de lote SAP (A_ProductPlant). C..J.
    // (A/B/H = material administrado por lote: ver testes acima com Batch/datas via stub padrao batch.)
    // ===================================================================================================

    // C. Material NAO administrado por lote ⇒ Batch/datas ausentes, POST permitido, peso enviado.
    [Fact]
    public async Task Enviar_MaterialNaoAdministradoPorLote_NaoEnviaBatchNemDatasEPermitePost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true,
            itens: [Item("10", numeroLote: "L-X", pesoLiquido: 7m)],
            consultarProdutoCentro: (mat, ce, ct) => Task.FromResult<ProdutoCentroSapMestre?>(Mestre(mat, ce, false)));

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas);
        MaterialDocumentSapItemRequest item = Assert.Single(materialDoc.UltimaRequisicao!.Itens);
        Assert.Null(item.Batch);
        Assert.Null(item.ManufactureDate);
        Assert.Null(item.ShelfLifeExpirationDate);
        Assert.Equal(7m, decimal.Parse(item.QuantityInEntryUnit, CultureInfo.InvariantCulture));
    }

    // D. Dois lotes locais de material NAO administrado ⇒ uma unica posicao, soma dos pesos, sem Batch/datas,
    //    preservando os dois codigos de lote local na posicao preparada.
    [Fact]
    public async Task Preparar_DoisLotesNaoAdministrado_ConsolidaEmUmaPosicaoPreservandoCodigosDeLote()
    {
        EntradaProdutoItemEnvioSap loteA = Item("10", numeroLote: "L-A", pesoLiquido: 3m) with { CodigoEntradaProdutoLote = 101 };
        EntradaProdutoItemEnvioSap loteB = Item("10", numeroLote: "L-B", pesoLiquido: 5m) with { CodigoEntradaProdutoLote = 202 };

        ResultadoPreparacaoPayloadEntrada preparo = await PreparadorPayloadMaterialDocumentEntrada.PrepararAsync(
            99, "4500000010", [loteA, loteB],
            (mat, ce, ct) => Task.FromResult<ProdutoCentroSapMestre?>(Mestre(mat, ce, false)),
            CancellationToken.None);

        Assert.Null(preparo.Bloqueio);
        EntradaProdutoPosicaoMaterialDocument posicao = Assert.Single(preparo.Posicoes!);
        Assert.Null(posicao.Item.Batch);
        Assert.Null(posicao.Item.ShelfLifeExpirationDate);
        Assert.Equal(8m, decimal.Parse(posicao.Item.QuantityInEntryUnit, CultureInfo.InvariantCulture));
        Assert.Equal(new long[] { 101, 202 }, posicao.CodigosLotesLocais.OrderBy(codigo => codigo));
    }

    // E. Documento MISTO: um material administrado por lote + um nao administrado ⇒ cada item usa sua regra.
    [Fact]
    public async Task Enviar_DocumentoMisto_CadaMaterialUsaSuaRegra()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap comLote = Item("10", numeroLote: "L-BM") with { Material = "MAT-BATCH" };
        EntradaProdutoItemEnvioSap semLote = Item("20", numeroLote: "L-NB", pesoLiquido: 4m) with { Material = "MAT-NOBATCH", DataValidade = null };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [comLote, semLote],
            consultarProdutoCentro: (mat, ce, ct) => Task.FromResult<ProdutoCentroSapMestre?>(Mestre(mat, ce, mat == "MAT-BATCH")));

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        IReadOnlyList<MaterialDocumentSapItemRequest> itens = materialDoc.UltimaRequisicao!.Itens;
        MaterialDocumentSapItemRequest posBatch = itens.Single(i => i.Material == "MAT-BATCH");
        MaterialDocumentSapItemRequest posNaoBatch = itens.Single(i => i.Material == "MAT-NOBATCH");
        Assert.Equal("L-BM", posBatch.Batch);
        Assert.NotNull(posBatch.ShelfLifeExpirationDate);
        Assert.Null(posNaoBatch.Batch);          // nao administrado: sem Batch mesmo com lote local
        Assert.Null(posNaoBatch.ShelfLifeExpirationDate); // e sem validade (nem exigida)
    }

    // F. Consulta A_ProductPlant indeterminada (null) ⇒ POST NAO executado, mensagem com material e centro.
    [Fact]
    public async Task Enviar_ConsultaProductPlantIndeterminada_BloqueiaSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [Item("10")],
            consultarProdutoCentro: (_, _, _) => Task.FromResult<ProdutoCentroSapMestre?>(null));

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.DadosIncompletos, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Contains("3500027", resultado.Mensagem); // material
        Assert.Contains("3007", resultado.Mensagem);    // centro
    }

    // G. Material 1000395 / centro 3007 simulado como NON-batch ⇒ payload sem as tres propriedades proibidas.
    [Fact]
    public async Task Enviar_Material1000395NonBatch_PayloadSemAsTresPropriedadesProibidas()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoItemEnvioSap item = Item("10", numeroLote: "L-1") with { Material = "1000395", Centro = "3007" };
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true, itens: [item],
            consultarProdutoCentro: (mat, ce, ct) => Task.FromResult<ProdutoCentroSapMestre?>(Mestre(mat, ce, false)));

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        MaterialDocumentSapItemRequest posicao = Assert.Single(materialDoc.UltimaRequisicao!.Itens);
        Assert.Equal("1000395", posicao.Material);
        Assert.Null(posicao.Batch);
        Assert.Null(posicao.ManufactureDate);
        Assert.Null(posicao.ShelfLifeExpirationDate);
        string json = MaterialDocumentSapApiClient.SerializarPayload(materialDoc.UltimaRequisicao!);
        Assert.DoesNotContain("Batch", json);
        Assert.DoesNotContain("ManufactureDate", json);
        Assert.DoesNotContain("ShelfLifeExpirationDate", json);
    }

    // I. Cache por envio: varios lotes do mesmo material+centro ⇒ um unico GET A_ProductPlant.
    [Fact]
    public async Task Enviar_MultiplosLotesMesmoMaterialCentro_ConsultaProductPlantUmaVez()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        int consultas = 0;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc, ehHomologacao: true,
            itens: [Item("10", numeroLote: "L-A"), Item("10", numeroLote: "L-B"), Item("10", numeroLote: "L-C")],
            consultarProdutoCentro: (mat, ce, ct) => { consultas++; return Task.FromResult<ProdutoCentroSapMestre?>(Mestre(mat, ce, true)); });

        await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(1, consultas); // um unico GET por material+centro
        Assert.Equal(3, materialDoc.UltimaRequisicao!.Itens.Count); // administrado: 3 posicoes (uma por lote)
    }

    // J. Nenhuma chamada SAP automatica (ProductPlant nem Material Document) na finalizacao local.
    [Fact]
    public async Task FinalizarLeitura_NaoDeveChamarProductPlantNemMaterialDocument()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        int consultas = 0;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico(), materialDoc, ehHomologacao: true, itens: [Item()],
            consultarProdutoCentro: (mat, ce, ct) => { consultas++; return Task.FromResult<ProdutoCentroSapMestre?>(Mestre(mat, ce, true)); });

        await controller.FinalizarLeituraAsync(Lancamento());

        Assert.Equal(0, materialDoc.Chamadas);
        Assert.Equal(0, consultas);
    }

    [Fact]
    public async Task Enviar_MaterialDocumentNaoConfigurado_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            materialDocumentConfigurado: false);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.MaterialDocumentNaoConfigurado, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_LancamentoJaConfirmadoSap_DeveBloquearSemChamarCriarDocumento()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            statusLancamento: "CONFIRMADO_SAP");

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoJaConfirmadoSap, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas); // nao chamou CriarDocumentoMaterialEntradaAsync
        Assert.Contains("confirmado", resultado.Mensagem!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enviar_LancamentoCancelado_DeveBloquearSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            statusLancamento: "CANCELADO");

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoCancelado, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_ReservaNaoObtida_DeveAbortarSemPost()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            reservaObtida: false);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.EnvioEmProcessamento, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas); // nao chamou o POST sem a reserva
        Assert.Contains("processamento", resultado.Mensagem!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enviar_ReservaObtida_DeveCriarDocumento()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            reservaObtida: true);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(1, materialDoc.Chamadas);
    }

    [Fact]
    public void TentarReservar_DeveSerUpdateAtomicoCondicionalAoStatus()
    {
        // Claim atomico: UPDATE ... -> ENVIADO_SAP WHERE status IN ('FINALIZADO_LOCAL','ERRO_SAP').
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "AcessoDados",
            "Repositorio",
            "EntradaProdutoRepositorio.cs"));

        Assert.Contains("status_lancamento = 'ENVIADO_SAP'", repositorio, StringComparison.Ordinal);
        Assert.Contains(
            "AND status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')",
            repositorio,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ListarItensParaEnvioSap_DevePossuirFiltroDeStatusContraReenvio()
    {
        // Itens CONFIRMADO_SAP/CANCELADO nao podem entrar no payload (defesa SQL de duplicacao).
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "AcessoDados",
            "Repositorio",
            "EntradaProdutoRepositorio.cs"));

        Assert.Contains(
            "lancamento.status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')",
            repositorio,
            StringComparison.Ordinal);
        Assert.Contains(
            "item.status_item IN ('FINALIZADO_LOCAL', 'ERRO_SAP')",
            repositorio,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AtualizarStatusAposEnvioSap_DevePersistirRastreabilidadeDoDocumentoMaterial()
    {
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto(), "AcessoDados", "Repositorio", "EntradaProdutoRepositorio.cs"));

        Assert.Contains("documento_material_sap = @documento_material_sap", repositorio, StringComparison.Ordinal);
        Assert.Contains("exercicio_documento_material_sap = @exercicio_documento_material_sap", repositorio, StringComparison.Ordinal);
        Assert.Contains("enviado_sap_em = now()", repositorio, StringComparison.Ordinal);
        Assert.Contains("documento_material_item = @documento_material_item", repositorio, StringComparison.Ordinal);
    }

    [Fact]
    public void LogDePayload_DeveUsarSituacaoParcialNaoPayload()
    {
        string servico = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Servicos", "IntegracaoSap", "MaterialDocumentSapServico.cs"));

        Assert.Contains("\"PARCIAL\"", servico, StringComparison.Ordinal);
        Assert.Contains("Etapa PAYLOAD: ", servico, StringComparison.Ordinal);
        // A situacao "PAYLOAD" nao pode ser usada (constraint do log_integracao_sap pode recusar).
        Assert.DoesNotContain("\"PAYLOAD\"", servico, StringComparison.Ordinal);
    }

    // Regex que cobre as formas de habilitar a escrita SAP em .cmd/.bat/.ps1 (com/sem aspas, $env:,
    // SetEnvironmentVariable), preservando a varredura de scripts locais perigosos.
    private const string PadraoHabilitaEscritaSap =
        @"(FUGAPET_SAP_WRITE_ENABLED\s*=\s*[""']?\s*true)"
        + @"|(SetEnvironmentVariable\s*\(\s*[""']FUGAPET_SAP_WRITE_ENABLED[""']\s*,\s*[""']?\s*true)";

    // Detecta somente comandos efetivos. Literais de regex, comentarios e mensagens do gerador nao contam.
    private const string PadraoExecucaoEfetivaEscritaSap =
        @"^\s*(?:set\s+|\$env:)?FUGAPET_SAP_WRITE_ENABLED\s*=\s*[""']?\s*true"
        + @"|^\s*\[Environment\]::SetEnvironmentVariable\s*\(\s*[""']FUGAPET_SAP_WRITE_ENABLED[""']\s*,\s*[""']?\s*true";

    [Fact]
    public void Projeto_NaoDeveConterScriptQueHabilitaEscritaSap()
    {
        string raiz = RaizProjeto();
        List<string> scriptsPerigosos = ObterScriptsPerigosos(raiz);

        Assert.True(
            scriptsPerigosos.Count == 0,
            "Script(s) (.cmd/.bat/.ps1) habilitando escrita SAP: "
                + string.Join("; ", scriptsPerigosos.Select(Path.GetFileName)));
    }

    [Fact]
    public void GerarPacoteLimpo_DeveSerPreservadoESomenteSeuCaminhoCanonicoPodeSerIgnorado()
    {
        string raiz = RaizProjeto();
        string caminhoGerador = Path.Combine(raiz, "Scripts", "GerarPacoteLimpo.ps1");

        Assert.True(File.Exists(caminhoGerador), "Scripts/GerarPacoteLimpo.ps1 deve existir localmente.");

        string conteudoGerador = File.ReadAllText(caminhoGerador);
        Assert.False(System.Text.RegularExpressions.Regex.IsMatch(
            conteudoGerador,
            PadraoExecucaoEfetivaEscritaSap,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.Multiline));
        Assert.Contains("function Testar-ScriptHabilitaEscritaSap", conteudoGerador, StringComparison.Ordinal);
        Assert.Contains("FUGAPET_SAP_WRITE_ENABLED", conteudoGerador, StringComparison.Ordinal);
        Assert.Contains("SetEnvironmentVariable", conteudoGerador, StringComparison.Ordinal);
        Assert.Contains("'.cmd', '.bat', '.ps1'", conteudoGerador, StringComparison.Ordinal);

        string raizTemporaria = Path.Combine(
            Path.GetTempPath(),
            $"fugapet-governanca-{Guid.NewGuid():N}");

        try
        {
            string diretorioCanonico = Path.Combine(raizTemporaria, "Scripts");
            string diretorioMalicioso = Path.Combine(raizTemporaria, "Outro");
            Directory.CreateDirectory(diretorioCanonico);
            Directory.CreateDirectory(diretorioMalicioso);

            File.WriteAllText(
                Path.Combine(diretorioCanonico, "GerarPacoteLimpo.ps1"),
                "$env:FUGAPET_SAP_WRITE_ENABLED = 'true'");
            string scriptMalicioso = Path.Combine(diretorioMalicioso, "GerarPacoteLimpo.ps1");
            File.WriteAllText(scriptMalicioso, "$env:FUGAPET_SAP_WRITE_ENABLED = 'true'");

            List<string> scriptsPerigosos = ObterScriptsPerigosos(raizTemporaria);

            Assert.Single(scriptsPerigosos);
            Assert.Equal(Path.GetFullPath(scriptMalicioso), Path.GetFullPath(scriptsPerigosos[0]));
        }
        finally
        {
            if (Directory.Exists(raizTemporaria))
            {
                Directory.Delete(raizTemporaria, recursive: true);
            }
        }
    }

    private static List<string> ObterScriptsPerigosos(string raiz)
    {
        string[] dirsIgnoradas = ["bin", "obj", "pacotes_limpos", ".git", ".vs", "_backup"];
        string[] extensoes = [".cmd", ".bat", ".ps1"];

        return Directory
            .EnumerateFiles(raiz, "*.*", SearchOption.AllDirectories)
            .Where(arquivo => extensoes.Any(ext =>
                arquivo.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Where(arquivo => !EhGeradorPacoteLimpoCanonico(raiz, arquivo))
            .Where(arquivo => dirsIgnoradas.All(dir =>
                !arquivo.Contains($"{Path.DirectorySeparatorChar}{dir}{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)))
            .Where(arquivo => System.Text.RegularExpressions.Regex.IsMatch(
                File.ReadAllText(arquivo),
                PadraoHabilitaEscritaSap,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            .ToList();
    }

    private static bool EhGeradorPacoteLimpoCanonico(string raiz, string arquivo)
    {
        string caminhoRelativo = Path
            .GetRelativePath(raiz, arquivo)
            .Replace(Path.DirectorySeparatorChar, '/');

        return string.Equals(
            caminhoRelativo,
            "Scripts/GerarPacoteLimpo.ps1",
            StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Controller_DeveDiagnosticarUnidadeOriginalEPesoSapKgSemBloqueioAntigo()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        Assert.Contains("Unidade original do pedido", controller, StringComparison.Ordinal);
        Assert.Contains("Peso l", controller, StringComparison.Ordinal);
        Assert.Contains("Quantidade SAP", controller, StringComparison.Ordinal);
        Assert.Contains("EntryUnit SAP: KG", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("Unidade do item n", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EntryUnit = item.Unidade", controller, StringComparison.Ordinal);
    }


    [Fact]
    public void Controller_DeveSetarGoodsMovementRefDocTypeBExplicitoESemPatch()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        // Padronizacao DEV/HML: nao depender apenas do default do modelo.
        Assert.Contains("GoodsMovementRefDocType = \"B\"", controller, StringComparison.Ordinal);
        // Entrada nao usa PATCH no Pedido de Compra.
        Assert.DoesNotContain("AtualizarPesoItemSapAsync", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Controller_NaoDeveConterMensagemAntigaDePendenciaGaia()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Controle", "Processo", "EntradaProdutoController.cs"));

        // A rastreabilidade agora e persistida; a mensagem de pendencia esta desatualizada.
        Assert.DoesNotContain("PENDENCIA GAIA", controller, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Envio SAP confirmado: documento material", controller, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enviar_Sap2xxSemDocumento_DeveSerCriticoSemConfirmarSemReenvio()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new() { RespostaSemDocumento = true };
        bool atualizouStatus = false;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, _, _, _) =>
            {
                atualizouStatus = true;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        // Nao confirma; trata como divergencia critica; nao atualiza status (nao marca ERRO_SAP nem
        // CONFIRMADO_SAP) -> lancamento fica ENVIADO_SAP, bloqueando reenvio automatico.
        Assert.Equal(CenarioEnvioSapEntrada.FalhaPersistenciaLocal, resultado.Cenario);
        Assert.False(resultado.StatusLocalAtualizado);
        Assert.False(atualizouStatus);
        Assert.NotNull(resultado.MensagemCritica);
    }

    [Fact]
    public async Task Enviar_SucessoSap_DeveConfirmarStatusLocalEGravarRastreabilidade()
    {
        DefinirSessao(comEnviarSap: true);
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        RastreabilidadeDocumentoMaterialSap? rastreabilidadePersistida = null;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico { Sucesso = true },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, cenario, rastreio, _) =>
            {
                cenarioPersistido = cenario;
                rastreabilidadePersistida = rastreio;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Enviado, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Enviado, cenarioPersistido);
        Assert.True(resultado.StatusLocalAtualizado);
        Assert.Equal("5000000124", resultado.MaterialDocument);
        // Persiste documento/exercicio na mesma transacao do CONFIRMADO_SAP.
        Assert.NotNull(rastreabilidadePersistida);
        Assert.Equal("5000000124", rastreabilidadePersistida!.Documento);
        Assert.Equal("2026", rastreabilidadePersistida.Exercicio);
    }

    [Fact]
    public async Task Enviar_FalhaSap_DeveRegistrarErroLocal()
    {
        DefinirSessao(comEnviarSap: true);
        CenarioEnvioSapEntrada? cenarioPersistido = null;
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico { Sucesso = false },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, cenario, _, _) =>
            {
                cenarioPersistido = cenario;
                return Task.FromResult(ResultadoOperacao.Ok());
            });

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.Falha, resultado.Cenario);
        Assert.Equal(CenarioEnvioSapEntrada.Falha, cenarioPersistido);
    }

    [Fact]
    public async Task Enviar_FalhaPersistenciaAposSucessoSap_DeveSinalizarCritico()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico { Sucesso = true },
            ehHomologacao: true,
            itens: [Item()],
            atualizarStatus: (_, _, _, _, _) => Task.FromResult(
                ResultadoOperacao.Falha("Falha local.")));

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.FalhaPersistenciaLocal, resultado.Cenario);
        Assert.False(resultado.StatusLocalAtualizado);
        Assert.NotNull(resultado.MensagemCritica);
    }

    [Fact]
    public async Task Enviar_SemPermissaoEnviarSap_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: false);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.SemPermissao, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_AmbienteDiferenteDeHomologacao_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: false,
            itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.AmbienteNaoHomologacao, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_EscritaDesabilitada_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = false },
            materialDoc,
            ehHomologacao: true,
            itens: [Item()]);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.EscritaDesabilitada, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Enviar_LancamentoSemItens_NaoDeveChamarSap()
    {
        DefinirSessao(comEnviarSap: true);
        FakeMaterialDocumentSapServico materialDoc = new();
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            materialDoc,
            ehHomologacao: true,
            itens: []);

        ResultadoEnvioSapEntrada resultado = await controller.EnviarPesoEntradaParaSapHomologacaoAsync(99);

        Assert.Equal(CenarioEnvioSapEntrada.LancamentoSemItens, resultado.Cenario);
        Assert.Equal(0, materialDoc.Chamadas);
    }

    [Fact]
    public async Task Diagnosticar_ComTodasAsTravas_DeveLiberarEnvio()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico(),
            ehHomologacao: true,
            itens: [Item()]);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.True(diagnostico.PodeEnviar);
        Assert.Null(diagnostico.MotivoBloqueio);
        Assert.True(diagnostico.MaterialDocumentConfigurado);
    }

    [Fact]
    public async Task Diagnosticar_MaterialDocumentNaoConfigurado_DeveBloquear()
    {
        DefinirSessao(comEnviarSap: true);
        EntradaProdutoController controller = CriarController(
            new FakePedidoCompraSapServico { EscritaHabilitada = true },
            new FakeMaterialDocumentSapServico(),
            ehHomologacao: true,
            itens: [Item()],
            materialDocumentConfigurado: false);

        DiagnosticoEnvioSapEntrada diagnostico =
            await controller.DiagnosticarEnvioSapEntradaAsync(99);

        Assert.False(diagnostico.PodeEnviar);
        Assert.False(diagnostico.MaterialDocumentConfigurado);
        Assert.Contains("Material Document", diagnostico.MotivoBloqueio);
    }

    private static EntradaProdutoController CriarController(
        FakePedidoCompraSapServico pedido,
        FakeMaterialDocumentSapServico materialDoc,
        bool ehHomologacao,
        IReadOnlyList<EntradaProdutoItemEnvioSap> itens,
        bool integracaoAtiva = true,
        bool materialDocumentConfigurado = true,
        string statusLancamento = "FINALIZADO_LOCAL",
        bool reservaObtida = true,
        Func<
            long,
            IReadOnlyList<ResultadoItemEnvioSap>,
            CenarioEnvioSapEntrada,
            RastreabilidadeDocumentoMaterialSap?,
            CancellationToken,
            Task<ResultadoOperacao>>? atualizarStatus = null,
        Func<string, string, CancellationToken, Task<ProdutoCentroSapMestre?>>? consultarProdutoCentro = null)
        => new(
            new IntegracaoEntradaSapServico(pedido, materialDoc),
            new EntradaProdutoServico(null!, null!, null!, null, new AutorizacaoCentroDepositoEntrada([], []), null),
            new BalancaLeituraServico(),
            new ImpressoraEtiquetaServico(),
            new AutorizacaoCentroDepositoEntrada([], []),
            FabricaControladoresCadastro.CriarTaraController(),
            ehAmbienteHomologacao: () => ehHomologacao,
            carregarItensParaEnvio: (_, _) => Task.FromResult(itens),
            // Por padrão, material ADMINISTRADO por lote (mantém o envio de Batch/datas dos testes existentes).
            consultarProdutoCentroSap: consultarProdutoCentro ?? ProdutoCentroBatchManaged,
            obterStatusLancamento: (_, _) => Task.FromResult<string?>(statusLancamento),
            reservarLancamentoParaEnvio: (_, _) => Task.FromResult(reservaObtida),
            diagnosticarIntegracaoSap: _ => Task.FromResult(
                new DiagnosticoProntidaoIntegracaoSap(
                    AmbienteOperacional: true,
                    IntegracaoAtiva: integracaoAtiva,
                    SapConfigurado: pedido.SapConfigurado,
                    EscritaSapHabilitada: pedido.EscritaSapHabilitada,
                    MaterialDocumentConfigurado: materialDocumentConfigurado,
                    MotivoBloqueio: integracaoAtiva
                        ? null
                        : "Integração SAP inativa.")),
            atualizarStatusAposEnvioSap: atualizarStatus
                ?? ((_, _, _, _, _) => Task.FromResult(ResultadoOperacao.Ok())));

    private static void DefinirSessao(bool comEnviarSap)
    {
        IReadOnlyList<PermissaoSessaoAplicacao> permissoes = comEnviarSap
            ? [new PermissaoSessaoAplicacao
            {
                Modulo = PermissoesSistema.Modulos.ProcessoProducao,
                Rotina = PermissoesSistema.Rotinas.EntradaProduto,
                Acao = PermissoesSistema.Acoes.EnviarSap
            }]
            : [];

        EstadoSessaoUsuarioAtual.Definir(new SessaoUsuarioAplicacao
        {
            IdUsuario = 7,
            Login = "operador_teste",
            Nome = "Operador Teste",
            PerfisCodigo = ["OPERADOR"],
            Permissoes = permissoes,
            IntegracaoBancoHabilitada = true
        });
    }

    private static EntradaProdutoItemEnvioSap Item(
        string numeroItem = "10",
        string unidade = "KG",
        string? numeroLote = "LOTE-A",
        DateTime? dataFabricacao = null,
        DateTime? dataValidade = null,
        decimal pesoLiquido = 8m,
        decimal pesoBruto = 10m)
        => new()
        {
            NumeroPedido = "4500000010",
            NumeroItem = numeroItem,
            PesoLiquidoKg = pesoLiquido,
            PesoBrutoKg = pesoBruto,
            Material = "3500027",
            Centro = "3007",
            Deposito = "PP01",
            Unidade = unidade,
            NumeroLote = numeroLote,
            CodigoEntradaProdutoLote = 0L,
            // Datas civis validas por padrao (fabricacao no passado, validade no futuro), como um lote real.
            DataFabricacao = dataFabricacao ?? DateTime.Today.AddMonths(-1),
            DataValidade = dataValidade ?? DateTime.Today.AddYears(1)
        };

    // Stub A_ProductPlant: material ADMINISTRADO por lote (envia Batch/datas).
    private static Task<ProdutoCentroSapMestre?> ProdutoCentroBatchManaged(string material, string centro, CancellationToken ct)
        => Task.FromResult<ProdutoCentroSapMestre?>(
            new ProdutoCentroSapMestre { Material = material, Centro = centro, IsBatchManagementRequired = true, Consultado = true });

    private static ProdutoCentroSapMestre Mestre(string material, string centro, bool? batch, bool consultado = true)
        => new() { Material = material, Centro = centro, IsBatchManagementRequired = batch, Consultado = consultado };

    private static EntradaProdutoLancamento Lancamento()
        => new()
        {
            NumeroPedido = "4500000010",
            Itens = [new EntradaProdutoItem
            {
                NumeroItem = "10",
                Pesagens = [new EntradaProdutoPesagem
                {
                    PesoBrutoKg = 10m, PesoTaraKg = 2m, PesoLiquidoKg = 8m, Origem = "BALANCA", StatusPesagem = "VALIDA"
                }]
            }]
        };

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

    private sealed class FakeMaterialDocumentSapServico : IMaterialDocumentSapServico
    {
        public bool Sucesso { get; init; } = true;
        public string MensagemFalha { get; init; } = "O SAP recusou a criacao do documento de material.";

        /// <summary>Simula HTTP 2xx porem sem MaterialDocument/MaterialDocumentYear (etapa PARSE_RESPOSTA).</summary>
        public bool RespostaSemDocumento { get; init; }

        public bool MaterialDocumentConfigurado => true;
        public bool EhSimulado => false;
        public int Chamadas { get; private set; }
        public MaterialDocumentSapRequest? UltimaRequisicao { get; private set; }
        public string? UltimaChave { get; private set; }

        public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
            MaterialDocumentSapRequest requisicao,
            string chaveNegocio,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            UltimaRequisicao = requisicao;
            UltimaChave = chaveNegocio;

            if (RespostaSemDocumento)
            {
                return Task.FromResult(new ResultadoMaterialDocumentSap
                {
                    Sucesso = false,
                    StatusHttp = 201,
                    Etapa = "PARSE_RESPOSTA",
                    MensagemSanitizada =
                        "Etapa PARSE_RESPOSTA: SAP retornou sucesso HTTP 201, mas sem MaterialDocument/MaterialDocumentYear na resposta."
                });
            }

            return Task.FromResult(Sucesso
                ? new ResultadoMaterialDocumentSap
                {
                    Sucesso = true,
                    StatusHttp = 201,
                    MaterialDocument = "5000000124",
                    MaterialDocumentYear = "2026",
                    MensagemSanitizada = "Documento de material 5000000124/2026 criado no SAP."
                }
                : ResultadoMaterialDocumentSap.Falha(400, MensagemFalha));
        }
    }

    private sealed class FakePedidoCompraSapServico : IPedidoCompraSapServico
    {
        public bool Configurado { get; init; } = true;
        public bool EscritaHabilitada { get; init; }
        public int PatchChamadas { get; private set; }

        public bool EhSimulado => false;
        public bool SapConfigurado => Configurado;
        public bool EscritaSapHabilitada => EscritaHabilitada;

        public Task<ResultadoOperacao> AtualizarPesoItemSapAsync(
            string numeroPedido, string numeroItem, decimal pesoLiquido, decimal pesoBruto, CancellationToken cancellationToken = default)
        {
            PatchChamadas++;
            return Task.FromResult(ResultadoOperacao.Ok("Peso atualizado no SAP."));
        }

        public Task<ResultadoOperacao> SincronizarPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoOperacao.Ok());
        public Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSapAgregado?>(null);
        public Task<PedidoCompraSap?> ObterCabecalhoSapParaValidacaoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<PedidoCompraSap?>(new PedidoCompraSap { Numero = numeroPedido, StatusProcessamentoCompraSap = "05" });
        public Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string> ObterFornecedorPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<DateOnly?> ObterDataPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<DateOnly?>(null);
        public Task<string> ObterTipoPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PedidoCompraSapItem>>([]);
    }
}


