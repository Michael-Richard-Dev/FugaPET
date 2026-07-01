using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoSemiAcabadoTests
{
    [Fact]
    public void ProcessoProducao_DeveExibirModuloSemiAcabadoComoQuartoProcesso()
    {
        string designer = LerArquivoProjeto("Tela", "ProcessoProducaoForm.Designer.cs");
        string form = LerArquivoProjeto("Tela", "ProcessoProducaoForm.cs");

        Assert.Contains("processoSemiAcabadoCard", designer, StringComparison.Ordinal);
        Assert.Contains("Produto\\r\\nSemi-Acabado", designer, StringComparison.Ordinal);
        Assert.Contains("semiAcabadoShortcutLabel.Text = \"F4\";", designer, StringComparison.Ordinal);
        Assert.Contains("processShortcutLabel.Text = \"F5\";", designer, StringComparison.Ordinal);
        Assert.Contains("ordensShortcutLabel.Text = \"F6\";", designer, StringComparison.Ordinal);
        Assert.Contains("ProcessoSemiAcabadoRequested", form, StringComparison.Ordinal);
        Assert.Contains("OnProcessoSemiAcabadoClick", form, StringComparison.Ordinal);
    }

    [Fact]
    public void PainelInicial_DeveAbrirProcessoSemiAcabadoNoF4()
    {
        string painel = LerArquivoProjeto("Tela", "PainelInicialForm.cs");

        Assert.Contains("view.ProcessoSemiAcabadoRequested += async (_, _) => await OpenProcessoSemiAcabadoAsync();", painel, StringComparison.Ordinal);
        Assert.Contains("private async Task OpenProcessoSemiAcabadoAsync()", painel, StringComparison.Ordinal);
        Assert.Contains("using Processo.ProcessoSemiAcabadoForm form = new();", painel, StringComparison.Ordinal);
        Assert.Contains("if (e.KeyCode == Keys.F4 && _currentContentView == _processoProducaoForm)", painel, StringComparison.Ordinal);
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
        Assert.Contains("MontarLancamentoSemiAcabadoAtual", form, StringComparison.Ordinal);
        Assert.Contains("GerarPreviewMaterialDocument101Atual", form, StringComparison.Ordinal);
        Assert.Contains("Usuário sem permissão para executar produto semi-acabado.", form, StringComparison.Ordinal);
        Assert.Contains("TODO Permissões", form, StringComparison.Ordinal);
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
        Assert.Contains("readWeightLegendTextLabel.Text = \"F12 - Ler peso balança\";", form, StringComparison.Ordinal);
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

        Assert.Contains("RegistrarPesagemSemiAcabado(decimal pesoBrutoKg, decimal taraKg, string origem)", form, StringComparison.Ordinal);
        Assert.Contains("decimal pesoLiquidoKg = pesoBrutoKg - taraKg;", form, StringComparison.Ordinal);
        Assert.Contains("PesoTaraKg = taraKg", form, StringComparison.Ordinal);
        Assert.Contains("PesoLiquidoKg = pesoLiquidoKg", form, StringComparison.Ordinal);
        Assert.Contains("Origem = origem", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemSemiAcabado(pesoBrutoKg, taraSelecionada.PesoKg, \"BALANCA\")", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesagemSemiAcabado(pesoBrutoKg, taraSelecionada.PesoKg, \"MANUAL\")", form, StringComparison.Ordinal);
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
        Assert.Contains("&& !_semiAcabadoConfirmadoNaSessao;", form, StringComparison.Ordinal);
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
        Assert.Contains("Balança de produto semi-acabado não configurada para esta operação.", form, StringComparison.Ordinal);
        Assert.DoesNotContain("GarantirBalancaSemiAcabadoConfiguradaAsync", metodoManual, StringComparison.Ordinal);
        Assert.Contains("GarantirTaraSemiAcabadoSelecionadaAsync", metodoManual, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_NaoDeveMostrarJsonTecnicoAoOperador()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string metodoConfirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        Assert.Contains("Trace.TraceInformation", metodoConfirmar, StringComparison.Ordinal);
        Assert.Contains("preview.PayloadJson", metodoConfirmar, StringComparison.Ordinal);
        Assert.Contains("MessageBox.Show(", metodoConfirmar, StringComparison.Ordinal);
        Assert.Contains("MensagemSemiAcabadoPendenteSap,", metodoConfirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("MensagemSemiAcabadoPendenteSap + Environment.NewLine", metodoConfirmar, StringComparison.Ordinal);
    }

    [Fact]
    public void TelaSemiAcabado_DeveMostrarGridComDadosDeSemiAcabado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");

        Assert.Contains("PreencherItensSemiAcabado", form, StringComparison.Ordinal);
        Assert.Contains("SemiAcabadoOrdem item", form, StringComparison.Ordinal);
        Assert.Contains("Material", form, StringComparison.Ordinal);
        Assert.Contains("Descrição", form, StringComparison.Ordinal);
        Assert.Contains("Qtd planejada", form, StringComparison.Ordinal);
        // Tarefa 20.6: cabecalhos operacionais — coluna "Peso" recebe o liquido pesado, "Origem" a origem.
        Assert.Contains("Saldo pendente", form, StringComparison.Ordinal);
        Assert.Contains("productionPesoLidoColumn.HeaderText = \"Peso\";", form, StringComparison.Ordinal);
        Assert.Contains("productionPesoOrigemColumn.HeaderText = \"Origem\";", form, StringComparison.Ordinal);
        Assert.Contains("Depósito destino", form, StringComparison.Ordinal);
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
        Assert.Equal("1000909", item.ManufacturingOrder);
        Assert.Equal("0001", item.ManufacturingOrderItem);
        Assert.Equal("2.5", item.QuantityInEntryUnit);
        Assert.Contains("\"GoodsMovementCode\": \"02\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"GoodsMovementType\": \"101\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"ManufacturingOrder\": \"1000909\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"to_MaterialDocumentItem\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("\"results\"", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder101_NaoDeveUsarPedidoDeCompraOuRefDocType()
    {
        ResultadoPreviewSemiAcabado101 preview = new SemiAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(LancamentoValido(1m), DateTime.UtcNow);

        Assert.DoesNotContain("PurchaseOrder", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PurchaseOrderItem", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GoodsMovementRefDocType", preview.PayloadJson, StringComparison.Ordinal);
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
        // Testes 1/2: ao confirmar, a flag de sessao fica true e os botoes sao reavaliados.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        Assert.Contains("private bool _semiAcabadoConfirmadoNaSessao;", form, StringComparison.Ordinal);

        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");
        Assert.Contains("_semiAcabadoConfirmadoNaSessao = true;", confirmar, StringComparison.Ordinal);
        Assert.Contains("AtualizarBotoesOperacao();", confirmar, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_4_BloqueiaNovaPesagemAposConfirmar()
    {
        // Testes 3/4: F9 (manual) e F12 (balanca) passam por ValidarPodePesar, que bloqueia apos confirmar.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string validar = ExtrairMetodo(form, "private bool ValidarPodePesar()");

        Assert.Contains("if (_semiAcabadoConfirmadoNaSessao)", validar, StringComparison.Ordinal);
        Assert.Contains("Semi-acabado já confirmado nesta sessão. Recarregue a OP para iniciar novo lançamento.", validar, StringComparison.Ordinal);
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

        string limpar = ExtrairMetodo(form, "private void LimparOpCarregada()");
        Assert.Contains("_semiAcabadoConfirmadoNaSessao = false;", limpar, StringComparison.Ordinal);

        string consulta = ExtrairMetodo(form, "private async Task ConsultarOpSelecionadaAsync()");
        Assert.Contains("_semiAcabadoConfirmadoNaSessao = false;", consulta, StringComparison.Ordinal);
    }

    [Fact]
    public void SemiAcabado_20_4_ConfirmarNaoMostraJsonMasMantemEmTrace()
    {
        // Testes 7/8: JSON tecnico apenas em Trace; MessageBox usa a mensagem funcional, sem payload.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        Assert.Contains("Trace.TraceInformation", confirmar, StringComparison.Ordinal);
        Assert.Contains("preview.PayloadJson", confirmar, StringComparison.Ordinal);
        Assert.Contains("MensagemSemiAcabadoPendenteSap,", confirmar, StringComparison.Ordinal);
        // A MessageBox nao expoe o JSON diretamente ao operador.
        Assert.DoesNotContain("MessageBox.Show(preview.PayloadJson", confirmar, StringComparison.Ordinal);
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
    public void SemiAcabado_20_4_NaoAtivaPostSapAutomatico()
    {
        // Teste 11: nenhum POST automatico ao confirmar (rota permanece preview 101 local).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string confirmar = ExtrairMetodo(form, "private async Task ConfirmarSemiAcabadoAsync()");

        Assert.Contains("GerarPreviewMaterialDocument101", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpMethod.Post", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarMaterialDocument101", confirmar, StringComparison.Ordinal);
        Assert.DoesNotContain("PostAsync", confirmar, StringComparison.Ordinal);
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

        string captura = ExtrairMetodo(form, "private void CapturarItemSelecionado()");
        Assert.Contains("finishedProductCodeTextBox.Text = ordem.MaterialProduzido;", captura, StringComparison.Ordinal);
        Assert.Contains("finishedProductTextBox.Text = string.Empty;", captura, StringComparison.Ordinal);
        Assert.DoesNotContain("finishedProductTextBox.Text = ordem.DescricaoMaterial;", captura, StringComparison.Ordinal);
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
        Assert.Contains("bool podeAlternarLeitura = livre && _ordemSelecionada is not null;", metodo, StringComparison.Ordinal);
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
        Assert.Contains("pesagens.Sum(p => p.PesoLiquidoKg)", atualizar, StringComparison.Ordinal);
        Assert.Contains("linha.Cells[\"productionPesoLidoColumn\"].Value", atualizar, StringComparison.Ordinal);
        Assert.Contains("pesoLiquidoTotal > 0m ? FormatarKg(pesoLiquidoTotal) : string.Empty", atualizar, StringComparison.Ordinal);
        Assert.Contains("linha.Cells[\"productionPesoOrigemColumn\"].Value = origem;", atualizar, StringComparison.Ordinal);

        // Chamado ao registrar e ao cancelar pesagem.
        string registrar = ExtrairMetodo(form, "private bool RegistrarPesagemSemiAcabado(decimal pesoBrutoKg, decimal taraKg, string origem)");
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
                new PesagemSemiAcabado { Sequencia = 1, PesoBrutoKg = liquido, PesoTaraKg = 0m, PesoLiquidoKg = liquido }
            ],
            Usuario = "teste"
        };

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
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
}
