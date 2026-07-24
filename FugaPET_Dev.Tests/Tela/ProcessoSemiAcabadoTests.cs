using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoSemiAcabadoTests
{
    [Fact]
    public void ProcessoProducao_DeveExibirModuloSemiAcabadoNaSegundaLinhaComoF5()
    {
        string designer = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");
        string form = LerArquivoProjeto("Tela", "ProcessoProducaoForm.cs");

        Assert.Contains("processoSemiAcabadoCard", designer, StringComparison.Ordinal);
        Assert.Contains("Produto\\r\\nSemi-Acabado", designer, StringComparison.Ordinal);
        Assert.Contains("processoSemiAcabadoCard.Location = new Point(28, 336);", designer, StringComparison.Ordinal);
        Assert.Contains("semiAcabadoShortcutLabel.Text = \"F5\";", designer, StringComparison.Ordinal);
        Assert.Contains("processShortcutLabel.Text = \"F6\";", designer, StringComparison.Ordinal);
        Assert.Contains("ordensShortcutLabel.Text = \"F7\";", designer, StringComparison.Ordinal);
        Assert.Contains("ProcessoSemiAcabadoRequested", form, StringComparison.Ordinal);
        Assert.Contains("OnProcessoSemiAcabadoClick", form, StringComparison.Ordinal);
    }

    [Fact]
    public void PainelInicial_DeveAbrirProcessoSemiAcabadoNoF5()
    {
        string painel = LerArquivoProjeto("Tela", "PainelInicialForm.cs");

        Assert.Contains("view.ProcessoSemiAcabadoRequested += async (_, _) => await OpenProcessoSemiAcabadoAsync();", painel, StringComparison.Ordinal);
        Assert.Contains("private async Task OpenProcessoSemiAcabadoAsync()", painel, StringComparison.Ordinal);
        Assert.Contains("using Processo.ProcessoSemiAcabadoForm form = new();", painel, StringComparison.Ordinal);
        Assert.Contains("if (e.KeyCode == Keys.F5 && _currentContentView == _processoProducaoForm)", painel, StringComparison.Ordinal);
        Assert.Contains("await OpenProcessoSemiAcabadoAsync();", painel, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveManterDesignerDaEntradaMasCodeBehindProprio()
    {
        string semiForm = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string semiDesigner = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.Designer.cs");
        string entradaDesigner = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.Designer.cs");

        Assert.Contains("public partial class ProcessoSemiAcabadoForm : Form", semiForm, StringComparison.Ordinal);
        Assert.Contains("private readonly SemiAcabadoController _controller;", semiForm, StringComparison.Ordinal);
        Assert.Contains("AplicarModoProdutoSemiAcabado();", semiForm, StringComparison.Ordinal);
        Assert.Contains("headerTitleLabel.Text = \"Produto Semi-Acabado\";", semiDesigner, StringComparison.Ordinal);
        Assert.Contains("rootTableLayoutPanel", semiDesigner, StringComparison.Ordinal);
        Assert.Contains("rootTableLayoutPanel", entradaDesigner, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_NaoDeveDependerFuncionalmenteDaEntrada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.DoesNotContain("EntradaProdutoController", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoServico", form, StringComparison.Ordinal);
        Assert.DoesNotContain("PedidoCompraSapItem", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoLancamento", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoItem", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EntradaProdutoPesagem", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultadoConsultaPedido", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultadoFinalizacaoEntrada", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ResultadoEnvioSapEntrada", form, StringComparison.Ordinal);
        Assert.DoesNotContain("CenarioEnvioSapEntrada", form, StringComparison.Ordinal);
        Assert.DoesNotContain("AutorizacaoEntradaProdutoServico", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsultarPedidoAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarPesoEntradaParaSapHomologacaoAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticarEnvioSapEntradaAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("_entradaServico", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveUsarConceitoDeOpPesagemELancamentoProprios()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("ConsultarOrdemProducaoAsync", form, StringComparison.Ordinal);
        Assert.Contains("EndpointConsultaOpSemiAcabado", form, StringComparison.Ordinal);
        Assert.Contains("API_PRODUCTION_ORDER_2_SRV/A_ProductionOrder_2", form, StringComparison.Ordinal);
        Assert.Contains("Selecione uma OP antes de iniciar a leitura.", form, StringComparison.Ordinal);
        Assert.Contains("Consulta de OP", form, StringComparison.Ordinal);
        Assert.Contains("Dictionary<string, List<PesagemSemiAcabado>> _pesagensPorItemOrdem", form, StringComparison.Ordinal);
        Assert.Contains("LancamentoSemiAcabado lancamento", form, StringComparison.Ordinal);
        Assert.Contains("MontarLancamentoLocal(", form, StringComparison.Ordinal);
        Assert.Contains("SalvarEEnviarMaterialDocument101Async", form, StringComparison.Ordinal);
        Assert.Contains("UsuÃ¡rio sem permissÃ£o para executar produto semi-acabado.", form, StringComparison.Ordinal);
        Assert.Contains("TODO PermissÃµes", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveLigarF12ParaBalancaEF9ParaManual()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("lerEtiquetaButton.Click += ReadWeightLegend_Click;", form, StringComparison.Ordinal);
        Assert.Contains("readWeightLegendPanel.Click += ReadWeightLegend_Click;", form, StringComparison.Ordinal);
        Assert.Contains("readWeightLegendIconLabel.Click += ReadWeightLegend_Click;", form, StringComparison.Ordinal);
        Assert.Contains("readWeightLegendTextLabel.Click += ReadWeightLegend_Click;", form, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.Click += LeituraManual_Click;", form, StringComparison.Ordinal);
        Assert.Contains("manualLotLegendPanel.Click += LeituraManual_Click;", form, StringComparison.Ordinal);
        Assert.Contains("manualLotLegendIconLabel.Click += LeituraManual_Click;", form, StringComparison.Ordinal);
        Assert.Contains("manualLotLegendTextLabel.Click += LeituraManual_Click;", form, StringComparison.Ordinal);
        Assert.Contains("readWeightLegendTextLabel.Text = \"F12 - Ler peso balanÃ§a\";", form, StringComparison.Ordinal);
        Assert.Contains("manualLotLegendTextLabel.Text = \"F9 - Digitar peso\";", form, StringComparison.Ordinal);
        Assert.Contains("lerEtiquetaButton.PrimaryText = \"LER PESO\";", form, StringComparison.Ordinal);
        Assert.Contains("lerEtiquetaButton.KeyHint = \"F12\";", form, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.PrimaryText = \"DIGITAR PESO\";", form, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.KeyHint = \"F9\";", form, StringComparison.Ordinal);
        Assert.Contains("e.KeyCode == Keys.F12", form, StringComparison.Ordinal);
        Assert.Contains("await RegistrarPesoBalancaAsync();", form, StringComparison.Ordinal);
        Assert.Contains("e.KeyCode == Keys.F9", form, StringComparison.Ordinal);
        Assert.Contains("await RegistrarPesoManualAsync();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveSelecionarTaraSomenteAoPesarEReaproveitarSelecao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("private readonly Dictionary<string, TaraCadastro> _tarasPorItemOrdem", form, StringComparison.Ordinal);
        Assert.Contains("GarantirTaraSemiAcabadoSelecionadaAsync", form, StringComparison.Ordinal);
        Assert.Contains("_tarasPorItemOrdem.TryGetValue", form, StringComparison.Ordinal);
        Assert.Contains("ListarTarasAtivasPorSetorAsync", form, StringComparison.Ordinal);
        Assert.Contains("using SelecaoTaraPesagemForm form = new(taras, _ordemSelecionada.MaterialProduzido);", form, StringComparison.Ordinal);
        Assert.Contains("_tarasPorItemOrdem[chave] = form.TaraSelecionada;", form, StringComparison.Ordinal);
        Assert.Contains("Tara '{form.TaraSelecionada.NomeTara}' selecionada para o semi-acabado.", form, StringComparison.Ordinal);

        string metodoConsulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");
        string metodoSelecao = ExtrairMetodo(form, "private void CapturarItemSelecionado()");
        Assert.DoesNotContain("GarantirTaraSemiAcabadoSelecionadaAsync", metodoConsulta, StringComparison.Ordinal);
        Assert.DoesNotContain("SelecaoTaraPesagemForm", metodoConsulta, StringComparison.Ordinal);
        Assert.DoesNotContain("GarantirTaraSemiAcabadoSelecionadaAsync", metodoSelecao, StringComparison.Ordinal);
        Assert.DoesNotContain("SelecaoTaraPesagemForm", metodoSelecao, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveCalcularLiquidoComTaraERegistrarOrigem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string pesagem = LerArquivoProjeto("Modelo", "Processo", "PesagemSemiAcabado.cs");

        Assert.Contains("RegistrarPesagemSemiAcabadoAsync(decimal pesoBrutoKg, decimal taraKg, string origem, long? codigoTara)", form, StringComparison.Ordinal);
        Assert.Contains("decimal pesoLiquidoKg = pesoBrutoKg - taraKg;", form, StringComparison.Ordinal);
        Assert.Contains("PesoTaraKg = taraKg", form, StringComparison.Ordinal);
        Assert.Contains("PesoLiquidoKg = pesoLiquidoKg", form, StringComparison.Ordinal);
        Assert.Contains("Origem = origem", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemSemiAcabadoAsync(pesoBrutoKg, taraSelecionada.PesoKg, \"BALANCA\", taraSelecionada.CodigoTara)", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemSemiAcabadoAsync(pesoBrutoKg, taraSelecionada.PesoKg, \"MANUAL\", taraSelecionada.CodigoTara)", form, StringComparison.Ordinal);
        Assert.Contains("pesagem.Origem", form, StringComparison.Ordinal);
        Assert.Contains("public string Origem { get; init; } = \"MANUAL\";", pesagem, StringComparison.Ordinal);
        Assert.DoesNotContain("decimal taraKg = 0m;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveControlarVisibilidadeDoConfirmarELeituras()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("productionActionsButton.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("lerEtiquetaButton.Visible = _leituraIniciada;", form, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.Visible = _leituraIniciada;", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Visible = !_leituraIniciada", form, StringComparison.Ordinal);
        Assert.Contains("&& possuiPesagem", form, StringComparison.Ordinal);
        // Tarefa 20.4: apos confirmar na sessao o botao some.
        Assert.Contains("&& !ItemAtualConfirmadoNaSessao", form, StringComparison.Ordinal);
        Assert.Contains("&& !_bloqueioLancamentoAberto;", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Enabled = productionActionsButton.Visible && livre;", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.PrimaryText = _leituraIniciada ? \"PARAR LEITURA\" : \"INICIAR LEITURA\";", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveBloquearF12SemBalancaENaoBloquearF9PorBalanca()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodoBalanca = ExtrairMetodo(form, "private async Task RegistrarPesoBalancaAsync()");
        string metodoManual = ExtrairMetodo(form, "private async Task RegistrarPesoManualAsync()");

        Assert.Contains("GarantirBalancaSemiAcabadoConfiguradaAsync", metodoBalanca, StringComparison.Ordinal);
        Assert.Contains("MensagemBalancaSemiAcabadoNaoConfigurada", metodoBalanca + form, StringComparison.Ordinal);
        Assert.Contains("BalanÃ§a de produto semi-acabado nÃ£o configurada para esta operaÃ§Ã£o.", form, StringComparison.Ordinal);
        Assert.DoesNotContain("GarantirBalancaSemiAcabadoConfiguradaAsync", metodoManual, StringComparison.Ordinal);
        Assert.Contains("GarantirTaraSemiAcabadoSelecionadaAsync", metodoManual, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_NaoDeveMostrarJsonTecnicoAoOperador()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodoConfirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        // O operador confirma via diÃ¡logo funcional e nunca vÃª JSON tÃ©cnico do payload.
        Assert.Contains("MessageBox.Show(", metodoConfirmar, StringComparison.Ordinal);
        Assert.Contains("MessageBoxButtons.YesNo", metodoConfirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("PayloadJson", metodoConfirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("MensagemSemiAcabadoPendenteSap", metodoConfirmar, StringComparison.Ordinal);
        // A mensagem removida nÃ£o pode reaparecer em lugar nenhum da tela.
        Assert.DoesNotContain("MensagemSemiAcabadoPendenteSap", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveMostrarGridComDadosDeSemiAcabado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("PreencherItensSemiAcabado", form, StringComparison.Ordinal);
        Assert.Contains("SemiAcabadoOrdem item", form, StringComparison.Ordinal);
        Assert.Contains("Material", form, StringComparison.Ordinal);
        Assert.Contains("DescriÃ§Ã£o", form, StringComparison.Ordinal);
        Assert.Contains("Qtd planejada", form, StringComparison.Ordinal);
        // Tarefa 20.6: cabecalhos operacionais â€” coluna "Peso" recebe o liquido pesado, "Origem" a origem.
        Assert.Contains("Saldo pendente", form, StringComparison.Ordinal);
        Assert.Contains("productionPesoLidoColumn.HeaderText = \"Peso\";", form, StringComparison.Ordinal);
        Assert.Contains("productionPesoOrigemColumn.HeaderText = \"Origem\";", form, StringComparison.Ordinal);
        Assert.Contains("DepÃ³sito destino", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ItensPedidoCompra", form, StringComparison.Ordinal);
        Assert.DoesNotContain("TipoPedidoNormal", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Pedido normal", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemiAcabadoController_DeveConsultarOpEMapearSaldoPendente()
    {
        SemiAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));

        ResultadoConsultaSemiAcabado resultado = await controller.ConsultarOrdemProducaoAsync("1000909");

        Assert.True(resultado.Sucesso);
        SemiAcabadoOrdem item = Assert.Single(resultado.Itens);
        Assert.Equal("1000909", item.NumeroOrdem);
        Assert.Equal("3500024", item.MaterialProduzido);
        Assert.Equal("3007", item.Centro);
        Assert.Equal("PA01", item.DepositoDestino);
        Assert.Equal(10m, item.QuantidadePlanejada);
        Assert.Equal(2m, item.QuantidadeEntregue);
        Assert.Equal(8m, item.QuantidadePendente);
        Assert.Equal("KG", item.Unidade);
        Assert.True(item.Liberada);
        Assert.False(item.EncerradaOuDeletada);
    }

    [Fact]
    public async Task SemiAcabadoController_DeveBloquearOpNaoLiberadaOuEncerrada()
    {
        SemiAcabadoController naoLiberada = new(new ProductionOrderSapFakeServico(OrdemSapValida() with { Liberada = false }));
        SemiAcabadoController encerrada = new(new ProductionOrderSapFakeServico(OrdemSapValida() with { Confirmada = true }));

        ResultadoConsultaSemiAcabado resultadoNaoLiberada = await naoLiberada.ConsultarOrdemProducaoAsync("1000909");
        ResultadoConsultaSemiAcabado resultadoEncerrada = await encerrada.ConsultarOrdemProducaoAsync("1000909");

        Assert.False(resultadoNaoLiberada.Sucesso);
        Assert.Contains("nao esta liberada", resultadoNaoLiberada.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.False(resultadoEncerrada.Sucesso);
        Assert.Contains("encerrada", resultadoEncerrada.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controller_DeveMontarLancamentoSemiAcabadoSemPersistirOuEnviarSap()
    {
        SemiAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        LancamentoSemiAcabado lancamento = controller.MontarLancamentoLocal(
            OrdemSemiAcabadoValida(),
            [new PesagemSemiAcabado { Sequencia = 1, PesoBrutoKg = 2m, PesoTaraKg = 0m, PesoLiquidoKg = 2m }],
            "teste");

        Assert.Equal("1000909", lancamento.Ordem.NumeroOrdem);
        Assert.Equal(2m, lancamento.PesoLiquidoTotalKg);
        Assert.Equal("teste", lancamento.Usuario);
    }

    [Fact]
    public void Builder101_DeveGerarPayloadPorOrdemDeProducao()
    {
        LancamentoSemiAcabado lancamento = LancamentoValido(2.5m);
        ResultadoPreviewSemiAcabado101 preview = new SemiAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(lancamento, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(preview.Sucesso);
        Assert.NotNull(preview.Payload);
        Assert.Equal("02", preview.Payload!.GoodsMovementCode);
        SemiAcabadoMaterialDocument101ItemRequest item = Assert.Single(preview.Payload.ToMaterialDocumentItem.Results);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("F", item.GoodsMovementRefDocType);
        Assert.Equal("1000909", item.ManufacturingOrder);
        Assert.Equal("0001", item.ManufacturingOrderItem);
        Assert.Equal("2.5", item.QuantityInEntryUnit);
        Assert.Contains("\"GoodsMovementCode\": \"02\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementType\": \"101\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementRefDocType\": \"F\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"ManufacturingOrder\": \"1000909\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"to_MaterialDocumentItem\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"results\"", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder101_DeveUsarRefDocTypeFESemPedidoDeCompra()
    {
        ResultadoPreviewSemiAcabado101 preview = new SemiAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(LancamentoValido(1m), DateTime.UtcNow);

        Assert.DoesNotContain("PurchaseOrder", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PurchaseOrderItem", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementRefDocType\": \"F\"", preview.PayloadJson, StringComparison.Ordinal);

        ResultadoMaterialDocumentSemiAcabadoRequest request = new SemiAcabadoMaterialDocument101PayloadBuilder()
            .MontarRequisicao101(LancamentoValido(1m), DateTime.UtcNow);
        Assert.True(request.Sucesso);
        MaterialDocumentSapItemRequest item = Assert.Single(request.Requisicao!.Itens);
        Assert.Equal("F", item.GoodsMovementRefDocType);
        Assert.Null(item.PurchaseOrder);
        Assert.Null(item.PurchaseOrderItem);
        string jsonOperacional = MaterialDocumentSapApiClient.SerializarPayload(request.Requisicao);
        Assert.Contains("\"GoodsMovementRefDocType\":\"F\"", jsonOperacional, StringComparison.Ordinal);
        Assert.DoesNotContain("PurchaseOrder", jsonOperacional, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder101_DeveExigirMaterialDocumentEAnoNoRetorno()
    {
        ResultadoEnvioSemiAcabado101 semDocumento = SemiAcabadoMaterialDocument101PayloadBuilder.ValidarRespostaConfirmada(null, "2026");
        ResultadoEnvioSemiAcabado101 semAno = SemiAcabadoMaterialDocument101PayloadBuilder.ValidarRespostaConfirmada("5000001", null);
        ResultadoEnvioSemiAcabado101 ok = SemiAcabadoMaterialDocument101PayloadBuilder.ValidarRespostaConfirmada("5000001", "2026");

        Assert.False(semDocumento.Sucesso);
        Assert.Contains("MaterialDocument", semDocumento.Mensagem, StringComparison.Ordinal);
        Assert.False(semAno.Sucesso);
        Assert.Contains("MaterialDocumentYear", semAno.Mensagem, StringComparison.Ordinal);
        Assert.True(ok.Sucesso);
        Assert.Equal("5000001", ok.MaterialDocument);
        Assert.Equal("2026", ok.MaterialDocumentYear);
    }

    [Fact]
    public void Builder101_DeveBloquearPesoAcimaDoSaldo()
    {
        ResultadoPreviewSemiAcabado101 preview = new SemiAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(LancamentoValido(11m), DateTime.UtcNow);

        Assert.False(preview.Sucesso);
        Assert.Contains("ultrapassa o saldo previsto do semi-acabado", preview.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Ajuste_NaoDeveAlterarEntradaConsumosGoodsMovementRefDocTypeOuSqlPatch()
    {
        string entrada = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string entradaItem = LerArquivoProjeto("Modelo", "IntegracaoSap", "MaterialDocumentSapItemRequest.cs");
        string consumo = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string semiForm = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string semiController = LerArquivoProjeto("Controle", "Processo", "SemiAcabadoController.cs");

        Assert.Contains("GoodsMovementRefDocType = \"B\"", entrada, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType", entradaItem, StringComparison.Ordinal);
        string semiBuilder = LerArquivoProjeto("Servicos", "IntegracaoSap", "SemiAcabadoMaterialDocument101PayloadBuilder.cs");
        Assert.Contains("GoodsMovementRefDocTypeOrdemProducao = \"F\"", semiBuilder, StringComparison.Ordinal);
        Assert.Contains("ProcessoConsumoMaterialForm", consumo, StringComparison.Ordinal);
        Assert.DoesNotContain("API_PROD_ORDER_CONFIRMATION_2_SRV", semiForm, StringComparison.Ordinal);
        Assert.DoesNotContain("API_PROD_ORDER_CONFIRMATION_2_SRV", semiController, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", semiForm + semiController, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", semiForm + semiController, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" PATCH ", semiForm + semiController, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemiAcabado_20_4_ConfirmarMarcaSessaoEAtualizaBotoes()
    {
        // Testes 1/2: ao confirmar, a flag de sessao por item fica true e os botoes sao reavaliados.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        Assert.Contains("private readonly Dictionary<string, bool> _confirmadoNaSessaoPorItem", form, StringComparison.Ordinal);
        Assert.Contains("private bool ItemAtualConfirmadoNaSessao", form, StringComparison.Ordinal);

        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");
        Assert.Contains("_confirmadoNaSessaoPorItem[chaveConfirmada] = true;", confirmar, StringComparison.Ordinal);
        Assert.Contains("AtualizarBotoesOperacao();", confirmar, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_4_BloqueiaNovaPesagemAposConfirmar()
    {
        // Testes 3/4: F9 (manual) e F12 (balanca) passam por ValidarPodePesar, que bloqueia apos confirmar.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string validar = ExtrairMetodo(form, "private bool ValidarPodePesar()");

        Assert.Contains("if (ItemAtualConfirmadoNaSessao)", validar, StringComparison.Ordinal);
        Assert.Contains("Semi-acabado jÃ¡ confirmado nesta sessÃ£o. Recarregue a OP para iniciar novo lanÃ§amento.", validar, StringComparison.Ordinal);
        Assert.Contains("return false;", validar, StringComparison.Ordinal);

        // Ambos os fluxos de pesagem chamam a mesma guarda central.
        string balanca = ExtrairMetodo(form, "private async Task RegistrarPesoBalancaAsync()");
        string manual = ExtrairMetodo(form, "private async Task RegistrarPesoManualAsync()");
        Assert.Contains("ValidarPodePesar()", balanca, StringComparison.Ordinal);
        Assert.Contains("ValidarPodePesar()", manual, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_4_ResetaFlagAoLimparEConsultarOp()
    {
        // Testes 5/6: limpar OP e consultar nova OP com sucesso reiniciam a sessao.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        string limparContextos = ExtrairMetodo(form, "private void LimparContextosOperacionais()");
        Assert.Contains("_confirmadoNaSessaoPorItem.Clear();", limparContextos, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoPersistidoPorItem.Clear();", limparContextos, StringComparison.Ordinal);
        Assert.Contains("_modoReenvioPorItem.Clear();", limparContextos, StringComparison.Ordinal);
        Assert.Contains("_bloqueioLancamentoPorItem.Clear();", limparContextos, StringComparison.Ordinal);

        string consulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");
        Assert.Contains("LimparContextosOperacionais();", consulta, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_ConfirmarTrataTodosOsResultadosDeEnvioSap()
    {
        // O confirmar reage a CADA cenÃ¡rio de ResultadoEnvioSemiAcabadoSap com estado de tela prÃ³prio:
        // sucesso (documento), divergÃªncia (bloqueada), estrutura pendente e erro reenviÃ¡vel.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        Assert.Contains("resultado.Sucesso", confirmar, StringComparison.Ordinal);
        Assert.Contains("resultado.MaterialDocument", confirmar, StringComparison.Ordinal);
        Assert.Contains("CONFIRMADO SAP", confirmar, StringComparison.Ordinal);
        Assert.Contains("resultado.DivergenciaSap", confirmar, StringComparison.Ordinal);
        Assert.Contains("DIVERGÃŠNCIA SAP", confirmar, StringComparison.Ordinal);
        Assert.Contains("resultado.EstruturaPendente", confirmar, StringComparison.Ordinal);
        Assert.Contains("ERRO SAP", confirmar, StringComparison.Ordinal);
        // Nenhum caminho de falha confirma a sessÃ£o (nÃ£o bloqueia novo envio apÃ³s erro reenviÃ¡vel).
        Assert.Contains("Nenhum caminho de falha confirma a sessÃ£o", confirmar, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_4_BloqueiaFechamentoComLeituraAtiva()
    {
        // Testes 9/10: fechar com leitura ativa e cancelado; sem leitura ativa fecha (dispoe o timer).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string closing = ExtrairMetodo(form, "private void ProcessoSemiAcabadoForm_FormClosing");

        Assert.Contains("if (_leituraIniciada)", closing, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", closing, StringComparison.Ordinal);
        Assert.Contains("Finalize a leitura antes de sair da tela.", closing, StringComparison.Ordinal);
        Assert.Contains("_footerClockTimer?.Dispose();", closing, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_ConfirmarEnviaAoSapComProtecaoContraDuploClique()
    {
        // O botÃ£o passou a acionar o envio REAL (SalvarEEnviarMaterialDocument101Async) com guarda
        // de concorrÃªncia (_operacaoEmAndamento em try/finally) contra duplo clique.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        Assert.Contains("if (_operacaoEmAndamento)", confirmar, StringComparison.Ordinal);
        Assert.Contains("_operacaoEmAndamento = true;", confirmar, StringComparison.Ordinal);
        Assert.Contains("await _controller.SalvarEEnviarMaterialDocument101Async(lancamento, CancellationToken.None);", confirmar, StringComparison.Ordinal);
        Assert.Contains("finally", confirmar, StringComparison.Ordinal);
        Assert.Contains("_operacaoEmAndamento = false;", confirmar, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_5_EscondeMarcaRosaEExpandeCampoOp()
    {
        // Testes 1/2: painel do icone (marca rosa) escondido e campo de OP esticado (igual Consumo).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private void ConfigurarCampoOrdemProducaoSemiAcabado()");

        Assert.Contains("productionOrderIconPanel.Visible = false;", metodo, StringComparison.Ordinal);
        Assert.Contains("AjustarLarguraCampoOrdemProducaoSemiAcabado();", metodo, StringComparison.Ordinal);
        Assert.Contains("productionOrderShadowPanel.Resize += (_, _) => AjustarLarguraCampoOrdemProducaoSemiAcabado();", metodo, StringComparison.Ordinal);
        // Chamado na construcao da tela.
        Assert.Contains("ConfigurarCampoOrdemProducaoSemiAcabado();", form, StringComparison.Ordinal);

        string ajuste = ExtrairMetodo(form, "private void AjustarLarguraCampoOrdemProducaoSemiAcabado()");
        Assert.Contains("pedidoComboBox.Width = Math.Max(120, larguraDisponivel);", ajuste, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_5_CampoSemiAcabadoIgualConsumo()
    {
        // Testes 3/4/5: descricao nao aparece no card (Visible false + Text vazio); code recebe so o material.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("finishedProductTextBox.Visible = false;", form, StringComparison.Ordinal);

        string aplicar = ExtrairMetodo(form, "private void AplicarItemSelecionado(SemiAcabadoOrdem ordem)");
        Assert.Contains("finishedProductCodeTextBox.Text = ordem.MaterialProduzido;", aplicar, StringComparison.Ordinal);
        Assert.Contains("finishedProductTextBox.Text = string.Empty;", aplicar, StringComparison.Ordinal);
        Assert.DoesNotContain("finishedProductTextBox.Text = ordem.DescricaoMaterial;", aplicar, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_5_DigitarPesoUsaPermissaoExecutar()
    {
        // Testes 6/7: peso manual nao usa PesoManual; usa temporariamente Executar.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string manual = ExtrairMetodo(form, "private async Task RegistrarPesoManualAsync()");

        Assert.DoesNotContain("PermissoesSistema.Acoes.PesoManual", manual, StringComparison.Ordinal);
        Assert.Contains("BloquearAcaoSemPermissaoAsync(PermissoesSistema.Acoes.Executar", manual, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_5_BotaoIniciarPararComCoresDaEntrada()
    {
        // Testes 8/9/10/11/12: parar=vermelho; iniciar com OP=verde; sem OP=cinza; textos corretos.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarBotoesOperacao()");

        Assert.Contains("iniciarLeituraButton.PrimaryText = _leituraIniciada ? \"PARAR LEITURA\" : \"INICIAR LEITURA\";", metodo, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(212, 37, 49)", metodo, StringComparison.Ordinal);   // vermelho leitura ativa
        Assert.Contains("Color.FromArgb(34, 166, 82)", metodo, StringComparison.Ordinal);   // verde OP valida
        Assert.Contains("Color.FromArgb(156, 163, 175)", metodo, StringComparison.Ordinal); // cinza sem OP
        Assert.Contains("bool podeAlternarLeitura = livre", metodo, StringComparison.Ordinal);
        Assert.Contains("&& !_modoReenvioLancamentoPersistido", metodo, StringComparison.Ordinal);
        Assert.Contains("&& !_bloqueioLancamentoAberto", metodo, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.Enabled = podeAlternarLeitura;", metodo, StringComparison.Ordinal);
        Assert.Contains("startActionPanel.BackColor", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_5_CabecalhoComHoverEToggleWindow()
    {
        // Testes 13/14/15/16: hover configurado nos 3 botoes; maximizar respeita leitura ativa.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("ConfigureTitleButtonHover(minimizeWindowLabel, Color.FromArgb(36, 46, 61));", form, StringComparison.Ordinal);
        Assert.Contains("ConfigureTitleButtonHover(maximizeWindowLabel, Color.FromArgb(36, 46, 61));", form, StringComparison.Ordinal);
        Assert.Contains("ConfigureTitleButtonHover(closeWindowLabel, Color.FromArgb(184, 18, 32));", form, StringComparison.Ordinal);
        Assert.Contains("maximizeWindowLabel.Click += (_, _) => ToggleWindowState();", form, StringComparison.Ordinal);

        string toggle = ExtrairMetodo(form, "private void ToggleWindowState()");
        Assert.Contains("if (_leituraIniciada)", toggle, StringComparison.Ordinal);
        Assert.Contains("return;", toggle, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_6_PesoVaiParaGridPrincipal()
    {
        // Testes grid/peso 1-7: a linha principal recebe o liquido acumulado e a origem consolidada.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        // Linha inicial: "Peso" e "Origem" vazios.
        string preencher = ExtrairMetodo(form, "private void PreencherItensSemiAcabado(IReadOnlyList<SemiAcabadoOrdem> itens)");
        Assert.Contains("string.Empty", preencher, StringComparison.Ordinal);

        // Metodo que atualiza a linha principal.
        string atualizar = ExtrairMetodo(form, "private void AtualizarLinhaPrincipalComPesagem(SemiAcabadoOrdem ordem)");
        // Total considera SOMENTE pesagens vÃ¡lidas (canceladas nÃ£o entram no total nem no payload SAP).
        Assert.Contains("PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(pesagens)", atualizar, StringComparison.Ordinal);
        Assert.Contains("linha.Cells[\"productionPesoLidoColumn\"].Value", atualizar, StringComparison.Ordinal);
        Assert.Contains("pesoLiquidoTotal > 0m ? FormatarKg(pesoLiquidoTotal) : string.Empty", atualizar, StringComparison.Ordinal);
        Assert.Contains("linha.Cells[\"productionPesoOrigemColumn\"].Value = origem;", atualizar, StringComparison.Ordinal);

        // Chamado ao registrar e ao cancelar pesagem.
        string registrar = ExtrairMetodo(form, "private async Task<bool> RegistrarPesagemSemiAcabadoAsync(decimal pesoBrutoKg, decimal taraKg, string origem, long? codigoTara)");
        string cancelar = ExtrairMetodo(form, "private void CancelarUltimaPesagem()");
        Assert.Contains("AtualizarLinhaPrincipalComPesagem(_ordemSelecionada);", registrar, StringComparison.Ordinal);
        Assert.Contains("AtualizarLinhaPrincipalComPesagem(_ordemSelecionada);", cancelar, StringComparison.Ordinal);

        // Origem consolidada MANUAL / BALANCA / MISTO.
        string origem = ExtrairMetodo(form, "private static string DescreverOrigemConsolidada(IReadOnlyList<PesagemSemiAcabado> pesagens)");
        Assert.Contains("\"MISTO\"", origem, StringComparison.Ordinal);
        Assert.Contains("\"BALANCA\"", origem, StringComparison.Ordinal);
        Assert.Contains("\"MANUAL\"", origem, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_6_StatusLeituraIgualEntrada()
    {
        // Testes status 1-9: textos/cores do status de leitura identicos a Entrada + cabecalho bloqueado.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        string estado = ExtrairMetodo(form, "private void AtualizarEstadoLeitura(bool iniciada)");
        Assert.Contains("sideReadingStatusLabel.Text = iniciada ? \"Ativo\" : \"Inativo\";", estado, StringComparison.Ordinal);
        Assert.Contains("AtualizarStatusCardLeitura(iniciada);", estado, StringComparison.Ordinal);
        Assert.Contains("AtualizarBloqueioCabecalho(iniciada);", estado, StringComparison.Ordinal);

        string card = ExtrairMetodo(form, "private void AtualizarStatusCardLeitura(bool iniciada)");
        Assert.Contains("statusValueLabel.Text = iniciada ? \"ATIVA\" : \"INATIVA\";", card, StringComparison.Ordinal);
        Assert.Contains("\"Leitura liberada para registro\"", card, StringComparison.Ordinal);
        Assert.Contains("\"Leitura aguardando inicio\"", card, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(229, 247, 234)", card, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(254, 232, 232)", card, StringComparison.Ordinal);

        string bloqueio = ExtrairMetodo(form, "private void AtualizarBloqueioCabecalho(bool bloqueado)");
        Assert.Contains("menuHeaderLabel.Visible = !bloqueado;", bloqueio, StringComparison.Ordinal);
        Assert.Contains("closeWindowLabel.Visible = !bloqueado;", bloqueio, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_6_AplicaIconePadrao()
    {
        // Testes icone 1/3: a tela aplica o icone padrao via helper que usa fuga.ico.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        Assert.Contains("IconeJanelaHelper.AplicarIconePadrao(this)", form, StringComparison.Ordinal);

        string helper = LerArquivoProjeto("Tela", "Comum", "IconeJanelaHelper.cs");
        Assert.Contains("fuga.ico", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Telas_20_6_FormsPrincipaisTemIcone()
    {
        // Teste icone 2: as telas que estavam sem icone passaram a aplicar o helper.
        foreach (string[] partes in new[]
                 {
                     new[] { "Tela", "Processo", "DiagnosticoConsumoSap261Form.cs" },
                     new[] { "Tela", "Processo", "ProcessoConsumoMaterialHistoricoForm.cs" },
                 })
        {
            string arquivo = LerArquivoProjeto(partes);
            Assert.Contains("IconeJanelaHelper.AplicarIconePadrao(this)", arquivo, StringComparison.Ordinal);
        }
    }

    // ======================================================================
    // Comportamentais â€” cÃ¡lculo vÃ¡lido-apenas, envio SAP e agnosticismo de schema.
    // ======================================================================

    [Fact]
    public void Calculos_TotalEContagemConsideramSomenteValidas()
    {
        // Duas pesagens de 50 kg vÃ¡lidas: total 100, contagem 2.
        List<PesagemSemiAcabado> duasValidas =
        [
            new() { Sequencia = 1, PesoLiquidoKg = 50m, StatusPesagem = "VALIDA" },
            new() { Sequencia = 2, PesoLiquidoKg = 50m, StatusPesagem = "VALIDA" },
        ];
        Assert.Equal(100m, PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(duasValidas));
        Assert.Equal(2, PesagemSemiAcabadoCalculos.ContarValidas(duasValidas));

        // Cancelando a segunda: total volta para 50 e contagem para 1 (cancelada nÃ£o entra).
        List<PesagemSemiAcabado> umaCancelada =
        [
            new() { Sequencia = 1, PesoLiquidoKg = 50m, StatusPesagem = "VALIDA" },
            new() { Sequencia = 2, PesoLiquidoKg = 50m, StatusPesagem = "CANCELADA" },
        ];
        Assert.Equal(50m, PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(umaCancelada));
        Assert.Equal(1, PesagemSemiAcabadoCalculos.ContarValidas(umaCancelada));
    }

    [Fact]
    public async Task Servico_EstruturaAusente_NaoSalvaNaoEnviaNaoTransiciona()
    {
        SemiAcabadoRepositorioFake repo = new() { Estrutura = false };
        MaterialDocumentSapFake sap = new(SucessoSap());
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.EstruturaPendente);
        Assert.Equal(0, repo.SalvouLocal);
        Assert.Equal(0, sap.Chamadas);
        Assert.Empty(repo.Transicoes);
    }

    [Fact]
    public async Task Servico_ReservaRecusada_NaoChamaSapNemTransiciona()
    {
        SemiAcabadoRepositorioFake repo = new() { ReservaConcedida = false };
        MaterialDocumentSapFake sap = new(SucessoSap());
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.EnvioDuplicadoBloqueado);
        Assert.Equal(0, sap.Chamadas);
        Assert.Empty(repo.Transicoes);
    }

    [Fact]
    public async Task Servico_Sucesso_GravaDocumentoEConfirma()
    {
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(SucessoSap());
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.Sucesso);
        Assert.Equal("5000001", resultado.MaterialDocument);
        Assert.Equal("2026", resultado.MaterialDocumentYear);
        Assert.Equal(1, sap.Chamadas);
        Assert.Equal(["CONFIRMADO"], repo.Transicoes);
    }

    [Fact]
    public async Task Servico_Http422Negocio_MarcaErroReenviavel()
    {
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = 422,
            Etapa = MaterialDocumentSapApiClient.EtapaPost,
            MensagemSanitizada = "Etapa POST_DOCUMENTO_MATERIAL: HTTP 422 Unprocessable Entity. SAP code=... msg=..."
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.False(resultado.Sucesso);
        Assert.False(resultado.DivergenciaSap);
        Assert.Equal(["ERRO"], repo.Transicoes);
    }

    [Fact]
    public async Task Servico_Http2xxSemDocumento_MarcaDivergenciaBloqueada()
    {
        // CombinaÃ§Ã£o REAL do MaterialDocumentSapApiClient para 2xx sem documento:
        // Sucesso=false, StatusHttp=201, Etapa=PARSE_RESPOSTA, documentos ausentes.
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = 201,
            Etapa = MaterialDocumentSapApiClient.EtapaParse,
            MaterialDocument = null,
            MaterialDocumentYear = null
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.DivergenciaSap);
        Assert.False(resultado.Sucesso);
        Assert.Contains("NÃ£o reenviar sem suporte", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal(["DIVERGENCIA"], repo.Transicoes);
        Assert.DoesNotContain("ERRO", repo.Transicoes);
        Assert.Equal(42L, resultado.CodigoLancamento);
    }

    [Fact]
    public async Task Servico_TimeoutNoCsrf_MarcaErroReenviavel()
    {
        // Rede caiu ANTES do POST (CSRF_FETCH, sem status HTTP): falha segura â†’ ERRO_SAP (reenvio permitido).
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = null,
            Etapa = MaterialDocumentSapApiClient.EtapaCsrfFetch,
            MensagemSanitizada = "Etapa CSRF_FETCH: falha de TIMEOUT ao contatar o SAP (sem resposta HTTP)."
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.False(resultado.Sucesso);
        Assert.False(resultado.DivergenciaSap);
        Assert.Equal(["ERRO"], repo.Transicoes);
    }

    [Fact]
    public async Task Servico_TimeoutDuranteOPost_MarcaDivergenciaBloqueada()
    {
        // Rede caiu DURANTE o POST (sem status HTTP, etapa POST): indeterminado â†’ DIVERGENCIA_SAP (bloqueado).
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = null,
            Etapa = MaterialDocumentSapApiClient.EtapaPost,
            MensagemSanitizada = "Etapa POST_DOCUMENTO_MATERIAL: falha de TIMEOUT ao contatar o SAP (sem resposta HTTP)."
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.DivergenciaSap);
        Assert.False(resultado.Sucesso);
        Assert.Contains("falha indeterminada durante o POST", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal(["DIVERGENCIA"], repo.Transicoes);
        Assert.DoesNotContain("ERRO", repo.Transicoes);
    }


    [Theory]
    [InlineData(408)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Servico_HttpIndeterminadoDurantePost_MarcaDivergenciaBloqueada(int statusHttp)
    {
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = statusHttp,
            Etapa = MaterialDocumentSapApiClient.EtapaPost,
            MensagemSanitizada = $"Etapa POST_DOCUMENTO_MATERIAL: HTTP {statusHttp}."
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.DivergenciaSap);
        Assert.False(resultado.Sucesso);
        Assert.Contains("falha indeterminada durante o POST", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Equal(["DIVERGENCIA"], repo.Transicoes);
        Assert.DoesNotContain("ERRO", repo.Transicoes);
    }

    [Fact]
    public async Task Servico_Http500DuranteCsrf_MarcaErroReenviavel()
    {
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = 500,
            Etapa = MaterialDocumentSapApiClient.EtapaCsrfFetch,
            MensagemSanitizada = "Etapa CSRF_FETCH: HTTP 500 Internal Server Error."
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.False(resultado.Sucesso);
        Assert.False(resultado.DivergenciaSap);
        Assert.Equal(["ERRO"], repo.Transicoes);
    }

    [Fact]
    public async Task Servico_Http400Negocio_MarcaErroReenviavel()
    {
        // RejeiÃ§Ã£o HTTP explÃ­cita de negÃ³cio (400): falha segura, sem documento â†’ ERRO_SAP (reenviÃ¡vel).
        SemiAcabadoRepositorioFake repo = new();
        MaterialDocumentSapFake sap = new(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = 400,
            Etapa = MaterialDocumentSapApiClient.EtapaPost,
            MensagemSanitizada = "Etapa POST_DOCUMENTO_MATERIAL: HTTP 400 Bad Request. SAP code=... msg=..."
        });
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.False(resultado.Sucesso);
        Assert.False(resultado.DivergenciaSap);
        Assert.Equal(["ERRO"], repo.Transicoes);
    }

    [Fact]
    public async Task Servico_ReenvioReutilizaMesmoLancamento()
    {
        SemiAcabadoRepositorioFake repo = new();

        // 1Âº envio: rejeiÃ§Ã£o de negÃ³cio comprovada â†’ ERRO_SAP; salva uma vez e devolve o cÃ³digo 42.
        SemiAcabadoServico servicoErro = CriarServico(repo, new MaterialDocumentSapFake(new ResultadoMaterialDocumentSap
        {
            Sucesso = false,
            StatusHttp = 400,
            Etapa = MaterialDocumentSapApiClient.EtapaPost,
            MensagemSanitizada = "Etapa POST_DOCUMENTO_MATERIAL: HTTP 400 Bad Request. SAP code=... msg=..."
        }));
        LancamentoSemiAcabado lancamento1 = LancamentoValido(2.5m);
        ResultadoEnvioSemiAcabadoSap r1 = await servicoErro.SalvarEEnviarSap101Async(lancamento1);

        Assert.Equal(1, repo.SalvouLocal);
        Assert.Equal(42L, r1.CodigoLancamento);
        Assert.Equal(["ERRO"], repo.Transicoes);

        // 2Âº envio: a tela reutiliza o cÃ³digo 42 (via _codigoLancamentoPersistido). NÃƒO salva de novo.
        SemiAcabadoServico servicoOk = CriarServico(repo, new MaterialDocumentSapFake(SucessoSap()));
        LancamentoSemiAcabado lancamento2 = LancamentoValido(2.5m);
        lancamento2.CodigoSemiAcabadoLancamento = r1.CodigoLancamento;
        ResultadoEnvioSemiAcabadoSap r2 = await servicoOk.SalvarEEnviarSap101Async(lancamento2);

        Assert.Equal(1, repo.SalvouLocal);            // nÃ£o inseriu novo cabeÃ§alho/pesagens
        Assert.Equal([42L, 42L], repo.Reservas);      // ambas as reservas no MESMO lanÃ§amento
        Assert.True(r2.Sucesso);
        Assert.Equal(["ERRO", "CONFIRMADO"], repo.Transicoes);
    }

    [Fact]
    public async Task Integracao_Cliente2xxSemDocumento_ViraDivergenciaNoServico()
    {
        // IntegraÃ§Ã£o clienteâ†’resultadoâ†’serviÃ§o: garante que a semÃ¢ntica REAL do cliente (2xx sem documento)
        // seja classificada como DIVERGENCIA pelo serviÃ§o, impedindo drift entre as duas camadas.
        ResultadoMaterialDocumentSap doCliente = MaterialDocumentSapApiClient.InterpretarSucesso(201, "{\"d\":{}}");
        Assert.False(doCliente.Sucesso);
        Assert.Equal(201, doCliente.StatusHttp);
        Assert.Equal(MaterialDocumentSapApiClient.EtapaParse, doCliente.Etapa);
        Assert.True(string.IsNullOrWhiteSpace(doCliente.MaterialDocument));

        SemiAcabadoRepositorioFake repo = new();
        SemiAcabadoServico servico = CriarServico(repo, new MaterialDocumentSapFake(doCliente));
        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(LancamentoValido(2.5m));

        Assert.True(resultado.DivergenciaSap);
        Assert.DoesNotContain("ERRO", repo.Transicoes);
        Assert.Equal(["DIVERGENCIA"], repo.Transicoes);
    }

    [Fact]
    public void Etiqueta_CodigoUnicoEntreLancamentosDaMesmaOpItem()
    {
        // Â§6: dois lanÃ§amentos parciais da MESMA OP/item reiniciam a sequÃªncia â€” o cÃ³digo NÃƒO pode colidir.
        SemiAcabadoOrdem ordem = new()
        {
            NumeroOrdem = "1001347",
            ItemOrdem = "0001",
            MaterialProduzido = "3500024"
        };

        string a = ImpressaoSemiAcabadoServico.GerarCodigoEtiqueta(ordem);
        string b = ImpressaoSemiAcabadoServico.GerarCodigoEtiqueta(ordem);

        Assert.NotEqual(a, b);
        Assert.True(a.Length <= 60, $"cÃ³digo com {a.Length} caracteres excede 60");
        Assert.StartsWith("SA-1001347-0001-", a, StringComparison.Ordinal);
        Assert.All(a, c => Assert.True(char.IsLetterOrDigit(c) || c == '-', $"caractere inseguro: '{c}'"));
    }

    [Fact]
    public void Etiqueta_ReimpressaoReutilizaCodigoPersistidoSemRecalcular()
    {
        // A reimpressÃ£o nunca recalcula: reutiliza o CodigoEtiqueta jÃ¡ persistido na pesagem.
        string servico = LerArquivoProjeto("Servicos", "Operacao", "ImpressaoSemiAcabadoServico.cs");
        Assert.Contains("Reimpressão bloqueada: CodigoEtiqueta ausente", servico, StringComparison.Ordinal);
        Assert.Contains("CodigoProducao = pesagem.CodigoEtiqueta", servico, StringComparison.Ordinal);
        // O gerador Ãºnico nÃ£o depende mais de sequÃªncia (que colidia entre lanÃ§amentos parciais).
        Assert.Contains("GerarCodigoEtiqueta(SemiAcabadoOrdem ordem)", servico, StringComparison.Ordinal);
        Assert.Contains("Guid.NewGuid()", servico, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_LancamentoAberto_ExcluiConfirmado()
    {
        // Â§4: aberto = FINALIZADO_LOCAL/ENVIANDO_SAP/ERRO_SAP/DIVERGENCIA_SAP; CONFIRMADO_SAP nunca Ã© aberto.
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "SemiAcabadoRepositorio.cs");
        Assert.Contains(
            "status_lancamento IN ('FINALIZADO_LOCAL', 'ENVIANDO_SAP', 'ERRO_SAP', 'DIVERGENCIA_SAP')",
            repo,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ReutilizaLancamentoPersistidoEBloqueiaAbertos()
    {
        // Â§3/Â§4: reenvio reutiliza o mesmo lanÃ§amento; a avaliaÃ§Ã£o de lanÃ§amento aberto recupera sÃ³ os
        // nÃ£o-confirmados e bloqueia ENVIANDO_SAP/DIVERGENCIA_SAP (histÃ³rico CONFIRMADO nunca compÃµe payload novo).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");
        Assert.Contains("lancamento.CodigoSemiAcabadoLancamento = _codigoLancamentoPersistido;", confirmar, StringComparison.Ordinal);
        Assert.Contains("if (resultado.CodigoLancamento is > 0)", confirmar, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoPersistido = resultado.CodigoLancamento;", confirmar, StringComparison.Ordinal);
        Assert.Contains("if (_bloqueioLancamentoAberto)", confirmar, StringComparison.Ordinal);

        Assert.Contains("ObterLancamentoAbertoPorOpItemAsync", form, StringComparison.Ordinal);
        string avaliar = ExtrairMetodo(form, "private async Task AvaliarLancamentoAbertoDoItemSelecionadoAsync(string? chaveEsperada, int versaoSelecao, bool atualizarTela)");
        Assert.Contains("case \"ENVIANDO_SAP\":", avaliar, StringComparison.Ordinal);
        Assert.Contains("case \"DIVERGENCIA_SAP\":", avaliar, StringComparison.Ordinal);
        Assert.Contains("case \"ERRO_SAP\":", avaliar, StringComparison.Ordinal);
        Assert.Contains("case \"FINALIZADO_LOCAL\":", avaliar, StringComparison.Ordinal);
        Assert.Contains("_bloqueioLancamentoAberto = true;", avaliar, StringComparison.Ordinal);
        Assert.DoesNotContain("CONFIRMADO_SAP", avaliar, StringComparison.Ordinal);
    }

    [Fact]
    public void Pacote037_TemIndiceAberturaEGrantsDeSequence()
    {
        // Â§4/Â§7: Ã­ndice parcial de lanÃ§amento aberto + grants USAGE nas sequences identity.
        string proposta = LerArquivoProjeto("BancoDados", "001_incrementais",
            "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_PROPOSTA_GAIA.sql");
        Assert.Contains("uq_semi_acabado_lancamento_aberto_por_op_item", proposta, StringComparison.Ordinal);
        Assert.Contains("GRANT USAGE, SELECT ON SEQUENCE", proposta, StringComparison.Ordinal);
        Assert.Contains("pg_get_serial_sequence", proposta, StringComparison.Ordinal);

        string validacao = LerArquivoProjeto("BancoDados", "001_incrementais",
            "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_VALIDACAO_GAIA.sql");
        Assert.Contains("has_sequence_privilege", validacao, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_NaoDeveFixarSchemaComoPrefixoNoSql()
    {
        // Tabelas sem prefixo de schema: o schema vem do search_path da conexÃ£o (DEV vs HML).
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "SemiAcabadoRepositorio.cs");
        Assert.DoesNotContain("homologacao.", repo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("desenvolvimento.", repo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("semi_acabado_lancamento", repo, StringComparison.Ordinal);
        Assert.Contains("semi_acabado_pesagem", repo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SemiAcabado_LancamentoAntigoInconsistenteNaoReservaNemChamaSap()
    {
        LancamentoSemiAcabado lancamentoInconsistente = new()
        {
            Ordem = OrdemSemiAcabadoValida(),
            CodigoSemiAcabadoLancamento = 42,
            Pesagens =
            [
                new PesagemSemiAcabado
                {
                    Sequencia = 1,
                    CodigoEtiqueta = string.Empty,
                    PesoBrutoKg = 2.5m,
                    PesoTaraKg = 0m,
                    PesoLiquidoKg = 2.5m,
                    SaldoAposPesagemKg = 7.5m
                }
            ],
            Usuario = "teste"
        };
        SemiAcabadoRepositorioFake repo = new()
        {
            LancamentoPersistido = lancamentoInconsistente
        };
        MaterialDocumentSapFake sap = new(SucessoSap());
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.SalvarEEnviarSap101Async(repo.LancamentoPersistido);

        Assert.True(resultado.InconsistenciaLocal);
        Assert.Contains("lançamento local antigo", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
        Assert.Empty(repo.Transicoes);
    }

    [Fact]
    public async Task SemiAcabado_CancelamentoLocalSoMarcaErroSapSemDocumento()
    {
        SemiAcabadoRepositorioFake repo = new()
        {
            CancelamentoConcedido = true,
            LancamentoPersistido = LancamentoValido(2.5m)
        };
        repo.LancamentoPersistido.CodigoSemiAcabadoLancamento = 77;
        repo.LancamentoPersistido.StatusLancamento = "ERRO_SAP";
        MaterialDocumentSapFake sap = new(SucessoSap());
        SemiAcabadoServico servico = CriarServico(repo, sap);

        ResultadoEnvioSemiAcabadoSap resultado = await servico.CancelarLancamentoLocalAsync(77, "sem documento SAP criado", "teste");

        Assert.Contains("cancelado com sucesso", resultado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Single(repo.Cancelamentos);
        Assert.Equal(77, repo.Cancelamentos[0]);
        Assert.Empty(repo.Reservas);
        Assert.Equal(0, sap.Chamadas);
    }

    [Fact]
    public void SemiAcabado_CancelamentoLocalNaoUsaOpsFixasEmCodigoProdutivo()
    {
        string raiz = RaizProjeto();
        string[] arquivosProdutivos =
        [
            ..Directory.GetFiles(Path.Combine(raiz, "Tela"), "*.cs", SearchOption.AllDirectories),
            ..Directory.GetFiles(Path.Combine(raiz, "Controle"), "*.cs", SearchOption.AllDirectories),
            ..Directory.GetFiles(Path.Combine(raiz, "Servicos"), "*.cs", SearchOption.AllDirectories),
            ..Directory.GetFiles(Path.Combine(raiz, "AcessoDados"), "*.cs", SearchOption.AllDirectories),
            ..Directory.GetFiles(Path.Combine(raiz, "Modelo"), "*.cs", SearchOption.AllDirectories)
        ];

        foreach (string arquivo in arquivosProdutivos)
        {
            string conteudo = File.ReadAllText(arquivo);
            Assert.DoesNotContain("1001327", conteudo, StringComparison.Ordinal);
            Assert.DoesNotContain("1001188", conteudo, StringComparison.Ordinal);
            Assert.DoesNotContain("1001347", conteudo, StringComparison.Ordinal);
        }
    }

    private static ResultadoMaterialDocumentSap SucessoSap()
        => new() { Sucesso = true, StatusHttp = 201, MaterialDocument = "5000001", MaterialDocumentYear = "2026" };

    private static SemiAcabadoServico CriarServico(ISemiAcabadoRepositorio repo, IMaterialDocumentSapServico sap)
        => new(repo, new SemiAcabadoMaterialDocument101PayloadBuilder(), () => sap);

    private static OrdemProducaoSap OrdemSapValida()
        => new()
        {
            NumeroOrdem = "1000909",
            MaterialProduzido = "3500024",
            Centro = "3007",
            QuantidadePrevista = 10m,
            Unidade = "KG",
            Deposito = "PA01",
            Lote = "L001",
            Liberada = true,
            Itens =
            [
                new ItemOrdemProducaoSap
                {
                    ItemOrdem = "0001",
                    Material = "3500024",
                    Centro = "3007",
                    Deposito = "PA01",
                    QuantidadePrevista = 10m,
                    QuantidadeEntregue = 2m,
                    Unidade = "KG",
                    Lote = "L001"
                }
            ]
        };

    private static SemiAcabadoOrdem OrdemSemiAcabadoValida()
        => new()
        {
            NumeroOrdem = "1000909",
            MaterialProduzido = "3500024",
            Centro = "3007",
            DepositoDestino = "PA01",
            QuantidadePlanejada = 10m,
            QuantidadeEntregue = 0m,
            QuantidadePendente = 10m,
            Unidade = "KG",
            Lote = "L001",
            ItemOrdem = "0001",
            Liberada = true
        };

    private static LancamentoSemiAcabado LancamentoValido(decimal liquido)
        => new()
        {
            Ordem = OrdemSemiAcabadoValida(),
            Pesagens =
            [
                new PesagemSemiAcabado
                {
                    Sequencia = 1,
                    CodigoEtiqueta = "SA-TESTE-001",
                    PesoBrutoKg = liquido,
                    PesoTaraKg = 0m,
                    PesoLiquidoKg = liquido,
                    SaldoAposPesagemKg = Math.Max(10m - liquido, 0m)
                }
            ],
            Usuario = "teste"
        };

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairClasse(string conteudo, string assinatura)
    {
        int inicio = conteudo.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Classe nÃ£o encontrada: {assinatura}");
        int abre = conteudo.IndexOf('{', inicio);
        int profundidade = 0;
        for (int i = abre; i < conteudo.Length; i++)
        {
            if (conteudo[i] == '{')
            {
                profundidade++;
            }
            else if (conteudo[i] == '}')
            {
                profundidade--;
                if (profundidade == 0)
                {
                    return conteudo.Substring(inicio, i - inicio + 1);
                }
            }
        }

        throw new InvalidOperationException($"Fim da classe nÃ£o encontrado: {assinatura}");
    }

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"MÃ©todo nÃ£o encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximoMetodo < 0)
        {
            proximoMetodo = fonte.Length;
        }

        return fonte[inicio..proximoMetodo];
    }

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nÃ£o encontrada.");
    }

    private sealed class ProductionOrderSapFakeServico : IProductionOrderSapServico
    {
        private readonly OrdemProducaoSap _ordem;

        public ProductionOrderSapFakeServico(OrdemProducaoSap ordem)
        {
            _ordem = ordem;
        }

        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.Encontrada(_ordem));
    }

    [Fact]
    public void SemiAcabado_Gaia037_DeveMapearSaldoAposPesagemPontaAPonta()
    {
        string modelo = LerArquivoProjeto("Modelo", "Processo", "PesagemSemiAcabado.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string repositorio = LerArquivoProjeto("AcessoDados", "Repositorio", "SemiAcabadoRepositorio.cs");
        string impressao = LerArquivoProjeto("Servicos", "Operacao", "ImpressaoSemiAcabadoServico.cs");

        Assert.Contains("public decimal SaldoAposPesagemKg { get; init; }", modelo, StringComparison.Ordinal);
        Assert.Contains("decimal saldoRestanteAposPesagem = Math.Max(_ordemSelecionada.QuantidadePendente - novoTotal, 0m);", form, StringComparison.Ordinal);
        Assert.Contains("SaldoAposPesagemKg = saldoRestanteAposPesagem", form, StringComparison.Ordinal);
        Assert.Contains("saldo_apos_pesagem_kg", repositorio, StringComparison.Ordinal);
        Assert.Contains("pesagem.SaldoAposPesagemKg", repositorio, StringComparison.Ordinal);
        Assert.Contains("SaldoAposPesagemKg = leitor.GetDecimal", repositorio, StringComparison.Ordinal);
        Assert.Contains("CriarEtiqueta(ordem, pesagem, pesagem.SaldoAposPesagemKg)", impressao, StringComparison.Ordinal);
        Assert.DoesNotContain("ReimprimirPesagemAsync(`n        SemiAcabadoOrdem ordem,`n        IReadOnlyList<PesagemSemiAcabado> pesagens", impressao, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_Gaia037_DeveUsarHistoricoConfirmadoSomenteParaReimpressao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("internal sealed class ContextoPesagemSemiAcabadoGrid", form, StringComparison.Ordinal);
        Assert.Contains("public bool Historica { get; init; }", form, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task CarregarHistoricoConfirmadoNoGridAsync()", form, StringComparison.Ordinal);
        Assert.Contains("ListarLancamentosPorOpItemAsync", form, StringComparison.Ordinal);
        Assert.Contains("ObterLancamentoCompletoAsync", form, StringComparison.Ordinal);
        Assert.Contains("CONFIRMADO_SAP", form, StringComparison.Ordinal);
        Assert.Contains("HISTÃ“RICO CONFIRMADO", form, StringComparison.Ordinal);
        Assert.Contains("ContextoPesagemSemiAcabadoGrid? contexto = materialDataGridView.Rows[e.RowIndex].Tag as ContextoPesagemSemiAcabadoGrid;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_Gaia037_DeveControlarConfirmacaoELeituraPorItem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("private readonly Dictionary<string, bool> _confirmadoNaSessaoPorItem", form, StringComparison.Ordinal);
        Assert.Contains("private bool ItemAtualConfirmadoNaSessao", form, StringComparison.Ordinal);
        Assert.Contains("_confirmadoNaSessaoPorItem[chaveConfirmada] = true;", form, StringComparison.Ordinal);
        Assert.DoesNotContain("_semiAcabadoConfirmadoNaSessao", form, StringComparison.Ordinal);
        Assert.Contains("private string? _chaveItemEmLeitura;", form, StringComparison.Ordinal);
        Assert.Contains("Pare a leitura antes de selecionar outro item.", form, StringComparison.Ordinal);
        Assert.Contains("LocalizarLinhaPrincipalPorChave(_chaveItemEmLeitura)", form, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_Gaia037_PacoteDeveEstarExtraidoESemDefaultNoSaldoPersistido()
    {
        string proposta = LerArquivoProjeto("BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_PROPOSTA_GAIA.sql");
        string validacao = LerArquivoProjeto("BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_VALIDACAO_GAIA.sql");
        string readme = LerArquivoProjeto("BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "README_037_SEMI_ACABADO_GAIA.txt");

        Assert.Contains("saldo_apos_pesagem_kg           numeric(14,3) NOT NULL,", proposta, StringComparison.Ordinal);
        Assert.DoesNotContain("saldo_apos_pesagem_kg           numeric(14,3) NOT NULL DEFAULT 0", proposta, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("saldo_apos_pesagem_kg IN (50.000, 0.000)", validacao, StringComparison.Ordinal);
        Assert.Contains("A aplicacao deve mapear a coluna saldo_apos_pesagem_kg", readme, StringComparison.Ordinal);
        Assert.True(global::System.IO.File.Exists(global::System.IO.Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_PREFLIGHT_GAIA.sql")));
        Assert.True(global::System.IO.File.Exists(global::System.IO.Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_ROLLBACK_GAIA.sql")));
    }

    [Fact]
    public void SemiAcabado_037_Final_ChaveContextoIncluiOpEItem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string chave = ExtrairMetodo(form, "private static string ChaveItemOrdem(SemiAcabadoOrdem ordem)");

        Assert.Contains("ordem.NumeroOrdem.Trim()", chave, StringComparison.Ordinal);
        Assert.Contains("item.Trim()", chave, StringComparison.Ordinal);
        Assert.Contains("|", chave, StringComparison.Ordinal);
        Assert.DoesNotContain("=> string.IsNullOrWhiteSpace(ordem.ItemOrdem)", chave, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final_LimpaTodosContextosAoTrocarOp()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string limpar = ExtrairMetodo(form, "private void LimparContextosOperacionais()");
        string consulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");

        Assert.Contains("_versaoSelecaoItem++;", limpar, StringComparison.Ordinal);
        Assert.Contains("_confirmadoNaSessaoPorItem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoPersistidoPorItem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_modoReenvioPorItem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_bloqueioLancamentoPorItem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_ordemPersistidaPorItem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_pesagensPorItemOrdem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_tarasPorItemOrdem.Clear();", limpar, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoPersistido = null;", limpar, StringComparison.Ordinal);
        Assert.Contains("_modoReenvioLancamentoPersistido = false;", limpar, StringComparison.Ordinal);
        Assert.Contains("_bloqueioLancamentoAberto = false;", limpar, StringComparison.Ordinal);
        Assert.Contains("_chaveItemEmLeitura = null;", limpar, StringComparison.Ordinal);
        Assert.Contains("_leituraIniciada = false;", limpar, StringComparison.Ordinal);
        Assert.Contains("LimparContextosOperacionais();", consulta, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final_SelecaoAsyncDescartaRespostaAtrasadaEPreencheGridUmaVez()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string selecionar = ExtrairMetodo(form, "private async Task SelecionarItemAsync(SemiAcabadoOrdem ordem, bool carregarHistorico, bool selecaoInterna = false)");
        string historico = ExtrairMetodo(form, "private async Task<IReadOnlyList<ContextoPesagemSemiAcabadoGrid>> ObterHistoricoConfirmadoAsync");
        string avaliar = ExtrairMetodo(form, "private async Task AvaliarLancamentoAbertoDoItemSelecionadoAsync(string? chaveEsperada, int versaoSelecao, bool atualizarTela)");

        Assert.Contains("await AvaliarLancamentoAbertoDoItemSelecionadoAsync(chaveNova, versao, false);", selecionar, StringComparison.Ordinal);
        Assert.Contains("if (!SelecaoContinuaAtual(chaveNova, versao))", selecionar, StringComparison.Ordinal);
        Assert.Contains("await ObterHistoricoConfirmadoAsync", selecionar, StringComparison.Ordinal);
        Assert.Contains("PreencherGridPesagens(linhas);", selecionar, StringComparison.Ordinal);
        Assert.Contains("return [];", historico, StringComparison.Ordinal);
        Assert.Contains("if (atualizarTela)", avaliar, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.SelectionChanged += async", form, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = AvaliarLancamentoAbertoDoItemSelecionadoAsync", form, StringComparison.Ordinal);
        Assert.DoesNotContain("private async Task CarregarHistoricoConfirmadoNoGridAsync()", form, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final_ReimpressaoUsaContextoDaLinha()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string contexto = ExtrairClasse(form, "internal sealed class ContextoPesagemSemiAcabadoGrid");
        string reimpressao = ExtrairMetodo(form, "private async void MaterialDataGridView_CellDoubleClick");
        string obterOrdem = ExtrairMetodo(form, "private SemiAcabadoOrdem ObterOrdemParaReimpressao");

        Assert.Contains("public SemiAcabadoOrdem? Ordem { get; init; }", contexto, StringComparison.Ordinal);
        Assert.Contains("public string NumeroOrdem { get; init; }", contexto, StringComparison.Ordinal);
        Assert.Contains("public string ItemOrdem { get; init; }", contexto, StringComparison.Ordinal);
        Assert.Contains("public string MaterialProduzido { get; init; }", contexto, StringComparison.Ordinal);
        Assert.Contains("SemiAcabadoOrdem ordemReimpressao = ObterOrdemParaReimpressao(contexto);", reimpressao, StringComparison.Ordinal);
        Assert.Contains("ChaveItemOrdem(_ordemSelecionada)", obterOrdem, StringComparison.Ordinal);
        Assert.Contains("ChaveItemOrdem(ordemContexto)", obterOrdem, StringComparison.Ordinal);
        Assert.Contains("A pesagem selecionada pertence a outro item", obterOrdem, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final_EnvioDuplicadoEBloqueiosSaoTratados()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");
        string duplicado = ExtrairMetodo(form, "private async Task TratarEnvioDuplicadoBloqueadoAsync");
        string botoes = ExtrairMetodo(form, "private void AtualizarBotoesOperacao()");

        Assert.Contains("if (resultado.EnvioDuplicadoBloqueado)", confirmar, StringComparison.Ordinal);
        Assert.Contains("await TratarEnvioDuplicadoBloqueadoAsync(resultado, ordemConfirmada, chaveConfirmada);", confirmar, StringComparison.Ordinal);
        Assert.Contains("ObterLancamentoCompletoAsync", duplicado, StringComparison.Ordinal);
        Assert.Contains("case \"ENVIANDO_SAP\":", duplicado, StringComparison.Ordinal);
        Assert.Contains("case \"DIVERGENCIA_SAP\":", duplicado, StringComparison.Ordinal);
        Assert.Contains("case \"CONFIRMADO_SAP\":", duplicado, StringComparison.Ordinal);
        Assert.Contains("case \"ERRO_SAP\":", duplicado, StringComparison.Ordinal);
        Assert.Contains("&& !_bloqueioLancamentoAberto;", botoes, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final_CancelamentoAlertaDescarteEtiqueta()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string cancelar = ExtrairMetodo(form, "private void CancelarUltimaPesagem()");

        Assert.Contains("A etiqueta desta pesagem jÃ¡ pode ter sido impressa.", cancelar, StringComparison.Ordinal);
        Assert.Contains("Pesagem: {ultimaValida.Sequencia:00}", cancelar, StringComparison.Ordinal);
        Assert.Contains("Peso lÃ­quido: {FormatarKg(ultimaValida.PesoLiquidoKg)}", cancelar, StringComparison.Ordinal);
        Assert.Contains("Descarte fisicamente a etiqueta cancelada.", cancelar, StringComparison.Ordinal);
        Assert.Contains("StatusCancelada", cancelar, StringComparison.Ordinal);
        Assert.Contains("CanceladoEm = DateTime.Now;", cancelar, StringComparison.Ordinal);
    }


    [Fact]
    public void SemiAcabado_037_Final2_HistoricoUsaCabecalhoPersistido()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string historico = ExtrairMetodo(form, "private async Task<IReadOnlyList<ContextoPesagemSemiAcabadoGrid>> ObterHistoricoConfirmadoAsync");

        Assert.Contains("LancamentoSemiAcabado? completo = await _controller.ObterLancamentoCompletoAsync", historico, StringComparison.Ordinal);
        Assert.Contains("foreach (PesagemSemiAcabado pesagem in completo.Pesagens)", historico, StringComparison.Ordinal);
        Assert.Contains("Ordem = completo.Ordem", historico, StringComparison.Ordinal);
        Assert.Contains("NumeroOrdem = completo.Ordem.NumeroOrdem", historico, StringComparison.Ordinal);
        Assert.Contains("ItemOrdem = completo.Ordem.ItemOrdem", historico, StringComparison.Ordinal);
        Assert.Contains("MaterialProduzido = completo.Ordem.MaterialProduzido", historico, StringComparison.Ordinal);
        Assert.Contains("Lote = completo.Ordem.Lote", historico, StringComparison.Ordinal);
        Assert.Contains("DescricaoMaterial = completo.Ordem.DescricaoMaterial", historico, StringComparison.Ordinal);
        Assert.DoesNotContain("Ordem = _ordemSelecionada", historico, StringComparison.Ordinal);
        Assert.DoesNotContain("_ordemSelecionada?.Lote", historico, StringComparison.Ordinal);
        Assert.DoesNotContain("ListarPesagensPorLancamentoAsync", historico, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final2_BloqueadosCarregamPesagensSomenteLeitura()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string avaliar = ExtrairMetodo(form, "private async Task AvaliarLancamentoAbertoDoItemSelecionadoAsync(string? chaveEsperada, int versaoSelecao, bool atualizarTela)");
        string readOnly = ExtrairMetodo(form, "private async Task CarregarLancamentoPersistidoSomenteLeituraAsync");

        Assert.Contains("case \"ENVIANDO_SAP\":", avaliar, StringComparison.Ordinal);
        Assert.Contains("case \"DIVERGENCIA_SAP\":", avaliar, StringComparison.Ordinal);
        Assert.Contains("await CarregarLancamentoPersistidoSomenteLeituraAsync(aberto.CodigoSemiAcabadoLancamento, chaveAtual, versaoSelecao);", avaliar, StringComparison.Ordinal);
        Assert.Contains("_bloqueioLancamentoAberto = true;", avaliar, StringComparison.Ordinal);
        Assert.Contains("_modoReenvioLancamentoPersistido = false;", avaliar, StringComparison.Ordinal);
        Assert.Contains("ObterLancamentoCompletoAsync", readOnly, StringComparison.Ordinal);
        Assert.Contains("destino.AddRange(completo.Pesagens);", readOnly, StringComparison.Ordinal);
        Assert.Contains("_modoReenvioPorItem[chaveEsperada] = false;", readOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("_modoReenvioPorItem[chaveEsperada] = true;", readOnly, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final2_IniciarLeituraBloqueiaItemConfirmado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string iniciar = ExtrairMetodo(form, "private void IniciarLeitura_Click");
        string botoes = ExtrairMetodo(form, "private void AtualizarBotoesOperacao()");

        Assert.Contains("if (ItemAtualConfirmadoNaSessao)", iniciar, StringComparison.Ordinal);
        Assert.Contains("Este item jÃ¡ foi confirmado no SAP nesta sessÃ£o", iniciar, StringComparison.Ordinal);
        Assert.Contains("&& !ItemAtualConfirmadoNaSessao", botoes, StringComparison.Ordinal);
        Assert.Contains("&& !_modoReenvioLancamentoPersistido", botoes, StringComparison.Ordinal);
        Assert.Contains("&& !_bloqueioLancamentoAberto", botoes, StringComparison.Ordinal);
    }


    [Fact]
    public void SemiAcabado_037_Final3_ConfirmacaoCongelaContextoAposAwait()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");
        string duplicado = ExtrairMetodo(form, "private async Task TratarEnvioDuplicadoBloqueadoAsync");

        Assert.Contains("SemiAcabadoOrdem ordemConfirmada = _ordemSelecionada;", confirmar, StringComparison.Ordinal);
        Assert.Contains("string chaveConfirmada = ChaveItemOrdem(ordemConfirmada);", confirmar, StringComparison.Ordinal);
        Assert.Contains("int versaoConfirmacao = _versaoSelecaoItem;", confirmar, StringComparison.Ordinal);
        Assert.Contains("_controller.MontarLancamentoLocal(", confirmar, StringComparison.Ordinal);
        Assert.Contains("ordemConfirmada,", confirmar, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoPersistidoPorItem[chaveConfirmada]", confirmar, StringComparison.Ordinal);
        Assert.Contains("_confirmadoNaSessaoPorItem[chaveConfirmada]", confirmar, StringComparison.Ordinal);
        Assert.Contains("_bloqueioLancamentoPorItem[chaveConfirmada]", confirmar, StringComparison.Ordinal);
        Assert.Contains("await TratarEnvioDuplicadoBloqueadoAsync(resultado, ordemConfirmada, chaveConfirmada);", confirmar, StringComparison.Ordinal);
        Assert.Contains("SemiAcabadoOrdem ordemConfirmada, string chave", duplicado, StringComparison.Ordinal);
        Assert.DoesNotContain("ChaveItemOrdem(_ordemSelecionada)", duplicado, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final3_BloqueiaSelecaoEPesagemDuranteOperacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string selecionarLinha = ExtrairMetodo(form, "private async Task SelecionarItemDaLinhaAtualAsync");
        string selecionarItem = ExtrairMetodo(form, "private async Task SelecionarItemAsync(SemiAcabadoOrdem ordem, bool carregarHistorico, bool selecaoInterna = false)");
        string duploClique = ExtrairMetodo(form, "private async void ProductionDataGridView_CellDoubleClick");
        string iniciar = ExtrairMetodo(form, "private void IniciarLeitura_Click");
        string validar = ExtrairMetodo(form, "private bool ValidarPodePesar()");
        string cancelar = ExtrairMetodo(form, "private void CancelarUltimaPesagem()");
        string keyDown = ExtrairMetodo(form, "private async void ProcessoSemiAcabadoForm_KeyDown");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        Assert.Contains("if (_operacaoEmAndamento)", selecionarLinha, StringComparison.Ordinal);
        Assert.Contains("(_operacaoEmAndamento && !selecaoInterna)", selecionarItem, StringComparison.Ordinal);
        Assert.Contains("if (_operacaoEmAndamento)", duploClique, StringComparison.Ordinal);
        Assert.Contains("if (BloquearAcaoDuranteOperacao())", iniciar, StringComparison.Ordinal);
        Assert.Contains("if (BloquearAcaoDuranteOperacao())", validar, StringComparison.Ordinal);
        Assert.Contains("if (BloquearAcaoDuranteOperacao())", cancelar, StringComparison.Ordinal);
        Assert.Contains("e.KeyCode == Keys.F9 || e.KeyCode == Keys.F12 || e.KeyCode == Keys.Delete", keyDown, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Enabled = false;", confirmar, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Enabled = true;", confirmar, StringComparison.Ordinal);
        Assert.Contains("Aguarde a conclusÃ£o da operaÃ§Ã£o em andamento.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final3_ReabertosUsamCabecalhoPersistido()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string reenvio = ExtrairMetodo(form, "private async Task CarregarLancamentoPersistidoParaReenvioAsync");
        string somenteLeitura = ExtrairMetodo(form, "private async Task CarregarLancamentoPersistidoSomenteLeituraAsync");
        string contextos = ExtrairMetodo(form, "private List<ContextoPesagemSemiAcabadoGrid> CriarContextosPesagensAtuais");

        Assert.Contains("ObterLancamentoCompletoAsync", reenvio, StringComparison.Ordinal);
        Assert.Contains("destino.AddRange(completo.Pesagens);", reenvio, StringComparison.Ordinal);
        Assert.Contains("_ordemPersistidaPorItem[chaveAtual] = completo.Ordem;", reenvio, StringComparison.Ordinal);
        Assert.Contains("_ordemPersistidaPorItem[chaveEsperada] = completo.Ordem;", somenteLeitura, StringComparison.Ordinal);
        Assert.Contains("_ordemPersistidaPorItem.TryGetValue(chave", contextos, StringComparison.Ordinal);
        Assert.Contains("Lote = ordemContexto.Lote", contextos, StringComparison.Ordinal);
        Assert.Contains("DescricaoMaterial = ordemContexto.DescricaoMaterial", contextos, StringComparison.Ordinal);
        Assert.DoesNotContain("ListarPesagensPorLancamentoAsync", reenvio, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final3_StatusAbertoNaoEhSobrescrito()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string consulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");
        string selecionar = ExtrairMetodo(form, "private async Task SelecionarItemAsync(SemiAcabadoOrdem ordem, bool carregarHistorico, bool selecaoInterna = false)");
        string statusGenerico = ExtrairMetodo(form, "private void AtualizarStatusItemSemLancamentoAberto()");

        Assert.DoesNotContain("statusValueLabel.Text = \"OP CARREGADA\";", consulta, StringComparison.Ordinal);
        Assert.Contains("AtualizarStatusItemSemLancamentoAberto();", selecionar, StringComparison.Ordinal);
        Assert.Contains("_modoReenvioLancamentoPersistido || _bloqueioLancamentoAberto || ItemAtualConfirmadoNaSessao", statusGenerico, StringComparison.Ordinal);
        Assert.Contains("case \"DIVERGENCIA_SAP\":", form, StringComparison.Ordinal);
        Assert.Contains("case \"ERRO_SAP\":", form, StringComparison.Ordinal);
    }


    [Fact]
    public void SemiAcabado_037_Final4_ProtegeDescarteDePesagensLocaisNaoPersistidas()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string possui = ExtrairMetodo(form, "private bool PossuiPesagensLocaisNaoPersistidas()");
        string confirmar = ExtrairMetodo(form, "private bool ConfirmarDescartePesagensLocaisNaoPersistidas");
        string consulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");
        string closing = ExtrairMetodo(form, "private void ProcessoSemiAcabadoForm_FormClosing");

        Assert.Contains("_pesagensPorItemOrdem", possui, StringComparison.Ordinal);
        Assert.Contains("_codigoLancamentoPersistidoPorItem", possui, StringComparison.Ordinal);
        Assert.Contains("codigoLancamento is null or <= 0", possui, StringComparison.Ordinal);
        Assert.Contains("Existem pesagens ainda nÃ£o confirmadas", confirmar, StringComparison.Ordinal);
        Assert.Contains("Descarte fisicamente todas as etiquetas correspondentes.", confirmar, StringComparison.Ordinal);
        Assert.Contains("MessageBoxDefaultButton.Button2", confirmar, StringComparison.Ordinal);
        Assert.Contains("ConfirmarDescartePesagensLocaisNaoPersistidas(\"limpar a OP\")", consulta, StringComparison.Ordinal);
        Assert.Contains("ConfirmarDescartePesagensLocaisNaoPersistidas(\"consultar outra OP\")", consulta, StringComparison.Ordinal);
        Assert.Contains("ConfirmarDescartePesagensLocaisNaoPersistidas(\"sair da tela\")", closing, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final4_BloqueiaFechamentoESelecaoDuranteOperacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string closing = ExtrairMetodo(form, "private void ProcessoSemiAcabadoForm_FormClosing");
        string keyDown = ExtrairMetodo(form, "private async void ProcessoSemiAcabadoForm_KeyDown");
        string consulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");
        string restaurar = ExtrairMetodo(form, "private void RestaurarSelecaoVisualItemAtual()");

        Assert.Contains("if (_operacaoEmAndamento)", closing, StringComparison.Ordinal);
        Assert.Contains("e.Cancel = true;", closing, StringComparison.Ordinal);
        Assert.Contains("Aguarde a conclusÃ£o da consulta ou do envio ao SAP antes de sair.", closing, StringComparison.Ordinal);
        Assert.Contains("else if (e.KeyCode == Keys.Escape)", keyDown, StringComparison.Ordinal);
        Assert.Contains("if (BloquearAcaoDuranteOperacao())", keyDown, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Enabled = false;", consulta, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Enabled = true;", consulta, StringComparison.Ordinal);
        Assert.Contains("RestaurarSelecaoVisualItemAtual();", consulta, StringComparison.Ordinal);
        Assert.Contains("LocalizarLinhaPrincipalPorChave(ChaveItemOrdem(_ordemSelecionada))", restaurar, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final4_ClassificacaoPost5xxEhDivergencia()
    {
        string servico = LerArquivoProjeto("Servicos", "Operacao", "SemiAcabadoServico.cs");
        string classificar = ExtrairMetodo(servico, "private async Task<ResultadoEnvioSemiAcabadoSap> ClassificarResultadoSapAsync");

        Assert.Contains("bool respostaIndeterminadaDurantePost", classificar, StringComparison.Ordinal);
        Assert.Contains("MaterialDocumentSapApiClient.EtapaPost", classificar, StringComparison.Ordinal);
        Assert.Contains("resultadoSap.StatusHttp == 408", classificar, StringComparison.Ordinal);
        Assert.Contains("resultadoSap.StatusHttp is >= 500 and <= 599", classificar, StringComparison.Ordinal);
        Assert.Contains("falha indeterminada durante o POST", classificar, StringComparison.Ordinal);
        Assert.Contains("MarcarDivergenciaSapAsync", classificar, StringComparison.Ordinal);
    }


    [Fact]
    public void SemiAcabado_037_Final4_PreflightValidaTamanhoPrecisaoScaleEIdentity()
    {
        string preflight = LerArquivoProjeto("BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_PREFLIGHT_GAIA.sql");

        Assert.Contains("character_maximum_length", preflight, StringComparison.Ordinal);
        Assert.Contains("numeric_precision", preflight, StringComparison.Ordinal);
        Assert.Contains("numeric_scale", preflight, StringComparison.Ordinal);
        Assert.Contains("is_identity", preflight, StringComparison.Ordinal);
        Assert.Contains("('codigo_etiqueta', 60", preflight, StringComparison.Ordinal);
        Assert.Contains("('material_document_year', 4", preflight, StringComparison.Ordinal);
        Assert.Contains("('peso_bruto_kg', NULL, 14, 3", preflight, StringComparison.Ordinal);
        Assert.Contains("('saldo_apos_pesagem_kg', NULL, 14, 3", preflight, StringComparison.Ordinal);
        Assert.Contains("('codigo_semi_acabado_lancamento', NULL::integer, NULL::integer, NULL::integer, 'YES')", preflight, StringComparison.Ordinal);
        Assert.Contains("('codigo_semi_acabado_pesagem', NULL::integer, NULL::integer, NULL::integer, 'YES')", preflight, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_037_Final4_ScriptsSemBomESetPrimeiroByte()
    {
        string pasta = Path.Combine(RaizProjeto(), "BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA");
        foreach (string arquivo in Directory.GetFiles(pasta, "*.sql"))
        {
            byte[] bytes = File.ReadAllBytes(arquivo);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, Path.GetFileName(arquivo));
            Assert.True(bytes.Length > 0 && bytes[0] == (byte)'\\', Path.GetFileName(arquivo));
        }
    }

    [Fact]
    public void SemiAcabado_037_Final_PacoteCanonicoUnicoEZipSemDefaultZero()
    {
        string raiz = RaizProjeto();
        string pasta = Path.Combine(raiz, "BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA");
        string parent = Path.GetDirectoryName(pasta)!;
        string zip = Path.Combine(parent, "037_semi_acabado_persistencia_sap_etiqueta_GAIA.zip");
        string zipObsoleto = Path.Combine(parent, "037_semi_acabado_persistencia_sap_etiqueta_GAIA_REVISADO_GAIA.zip");

        Assert.True(Directory.Exists(pasta));
        Assert.True(File.Exists(zip));
        Assert.False(File.Exists(zipObsoleto));

        string[] arquivosPasta = Directory.GetFiles(pasta).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;
        Assert.Equal(5, arquivosPasta.Length);

        using System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead(zip);
        string[] arquivosZip = archive.Entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .Select(e => e.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(arquivosPasta, arquivosZip);

        string proposta = LerArquivoProjeto("BancoDados", "001_incrementais", "037_semi_acabado_persistencia_sap_etiqueta_GAIA", "037_semi_acabado_DEV_PROPOSTA_GAIA.sql");
        Assert.Contains("saldo_apos_pesagem_kg           numeric(14,3) NOT NULL,", proposta, StringComparison.Ordinal);
        Assert.DoesNotContain("saldo_apos_pesagem_kg           numeric(14,3) NOT NULL DEFAULT 0", proposta, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class MaterialDocumentSapFake : IMaterialDocumentSapServico
    {
        private readonly ResultadoMaterialDocumentSap _resultado;

        public MaterialDocumentSapFake(ResultadoMaterialDocumentSap resultado) => _resultado = resultado;

        public int Chamadas { get; private set; }
        public bool EhSimulado => true;
        public bool MaterialDocumentConfigurado => true;

        public Task<ResultadoMaterialDocumentSap> CriarDocumentoMaterial101Async(
            MaterialDocumentSapRequest requisicao,
            string chaveNegocio,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(_resultado);
        }
    }

    private sealed class SemiAcabadoRepositorioFake : ISemiAcabadoRepositorio
    {
        public bool Estrutura { get; init; } = true;
        public bool ReservaConcedida { get; init; } = true;
        public long CodigoSalvo { get; init; } = 42;
        public bool CancelamentoConcedido { get; init; }
        public int SalvouLocal { get; private set; }
        public LancamentoSemiAcabado? LancamentoPersistido { get; set; }
        public List<string> Transicoes { get; } = [];
        public List<long> Reservas { get; } = [];
        public List<long> Cancelamentos { get; } = [];

        public Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Estrutura);

        public Task<long> SalvarLancamentoLocalAsync(
            LancamentoSemiAcabado lancamento, string payloadPreviewJson, CancellationToken cancellationToken = default)
        {
            SalvouLocal++;
            lancamento.CodigoSemiAcabadoLancamento = CodigoSalvo;
            LancamentoPersistido = lancamento;
            return Task.FromResult(CodigoSalvo);
        }

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
        {
            Reservas.Add(codigoLancamento);
            return Task.FromResult(ReservaConcedida);
        }

        public Task<LancamentoSemiAcabado?> ObterLancamentoCompletoAsync(
            long codigoLancamento,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LancamentoSemiAcabado?>(LancamentoPersistido ?? LancamentoValido(2.5m));

        public Task MarcarConfirmadoSapAsync(
            long codigoLancamento, string materialDocument, string materialDocumentYear, CancellationToken cancellationToken = default)
        {
            Transicoes.Add("CONFIRMADO");
            return Task.CompletedTask;
        }

        public Task MarcarErroSapAsync(long codigoLancamento, string mensagemErro, CancellationToken cancellationToken = default)
        {
            Transicoes.Add("ERRO");
            return Task.CompletedTask;
        }

        public Task MarcarDivergenciaSapAsync(long codigoLancamento, string mensagem, CancellationToken cancellationToken = default)
        {
            Transicoes.Add("DIVERGENCIA");
            return Task.CompletedTask;
        }

        public Task<bool> CancelarLancamentoLocalAsync(
            long codigoLancamento,
            string motivo,
            string usuario,
            CancellationToken cancellationToken = default)
        {
            if (CancelamentoConcedido)
            {
                Cancelamentos.Add(codigoLancamento);
            }

            return Task.FromResult(CancelamentoConcedido);
        }

        public Task<IReadOnlyList<PesagemSemiAcabado>> ListarPesagensPorLancamentoAsync(
            long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PesagemSemiAcabado>>([]);

        public Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoPorOpItemAsync(
            string numeroOrdem, string itemOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult<LancamentoSemiAcabadoPersistido?>(null);

        public Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoAbertoPorOpItemAsync(
            string numeroOrdem, string itemOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult<LancamentoSemiAcabadoPersistido?>(null);

        public Task<IReadOnlyList<LancamentoSemiAcabadoPersistido>> ListarLancamentosPorOpItemAsync(
            string numeroOrdem, string itemOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LancamentoSemiAcabadoPersistido>>([]);
    }
}












