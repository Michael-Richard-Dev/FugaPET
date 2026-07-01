using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoProdutoAcabadoTests
{
    [Fact]
    public void DeveUsarTelaExistenteSemCriarDuplicada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.Designer.cs");

        Assert.Contains("public partial class ProcessoProdutoAcabadoForm : Form", form, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCampoOrdemProducaoProdutoAcabado();", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.ReadOnly = false;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.Text = string.Empty;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.Multiline = false;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.MaxLength = 20;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.TextAlign = HorizontalAlignment.Left;", form, StringComparison.Ordinal);
        Assert.Contains("partial class ProcessoProdutoAcabadoForm", designer, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.ReadOnly = false;", designer, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.Multiline = false;", designer, StringComparison.Ordinal);
        Assert.Contains("productionOrderTextBox.MaxLength = 20;", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("productionOrderTextBox.Text = \"58422\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("58422", form + designer, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoProdutoProntoForm.cs")));
        Assert.False(File.Exists(Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoProdutoAcabadoNovoForm.cs")));
        Assert.False(File.Exists(Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoProdutoAcabadoV2Form.cs")));
        Assert.Contains("IconeJanelaHelper.AplicarIconePadrao(this)", form, StringComparison.Ordinal);
        Assert.DoesNotContain("private void LoadWindowIcon()", form, StringComparison.Ordinal);
        Assert.Contains("sideReadingStatusLabel.Text = iniciada ? \"Ativo\" : \"Inativo\";", form, StringComparison.Ordinal);
        Assert.Contains("statusValueLabel.Text = iniciada ? \"ATIVA\" : \"INATIVA\";", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Controller_DeveConsultarOpEMapearProdutoAcabado()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));

        ResultadoConsultaProdutoAcabado resultado = await controller.ConsultarOrdemProducaoAsync("1000909");

        Assert.True(resultado.Sucesso);
        Assert.NotNull(resultado.Ordem);
        Assert.Equal("1000909", resultado.Ordem!.NumeroOrdem);
        Assert.Equal("3500024", resultado.Ordem.MaterialProduzido);
        Assert.Equal("3007", resultado.Ordem.Centro);
        Assert.Equal("PA01", resultado.Ordem.DepositoDestino);
        Assert.Equal("L001", resultado.Ordem.Lote);
        Assert.Equal("0001", resultado.Ordem.ItemOrdem);
        Assert.Equal(10m, resultado.Ordem.QuantidadePlanejada);
        Assert.Equal(8m, resultado.Ordem.QuantidadePendente);
        Assert.NotNull(resultado.NormaEmbalagem);
        Assert.Equal(1, resultado.NormaEmbalagem!.QuantidadeProdutosPorCaixa);
    }

    [Fact]
    public async Task Controller_DeveBloquearOpNaoLiberadaOuSemSaldo()
    {
        ProdutoAcabadoController naoLiberada = new(new ProductionOrderSapFakeServico(OrdemSapValida() with { Liberada = false }));
        ProdutoAcabadoController semSaldo = new(new ProductionOrderSapFakeServico(OrdemSapValida() with
        {
            Itens =
            [
                new ItemOrdemProducaoSap
                {
                    ItemOrdem = "0001",
                    Material = "3500024",
                    Centro = "3007",
                    Deposito = "PA01",
                    QuantidadePrevista = 10m,
                    QuantidadeEntregue = 10m,
                    Unidade = "KG",
                    Lote = "L001"
                }
            ]
        }));

        ResultadoConsultaProdutoAcabado resultadoNaoLiberada = await naoLiberada.ConsultarOrdemProducaoAsync("1000909");
        ResultadoConsultaProdutoAcabado resultadoSemSaldo = await semSaldo.ConsultarOrdemProducaoAsync("1000909");

        Assert.False(resultadoNaoLiberada.Sucesso);
        Assert.Contains("liberada", resultadoNaoLiberada.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.False(resultadoSemSaldo.Sucesso);
        Assert.Contains("sem saldo pendente", resultadoSemSaldo.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormaEmbalagem_DeveTerModeloClientEFallbackControlado()
    {
        string modelo = LerArquivoProjeto("Modelo", "Processo", "ProdutoAcabadoNormaEmbalagem.cs");
        string client = LerArquivoProjeto("Servicos", "IntegracaoSap", "ProdutoAcabadoPackagingApiClient.cs");

        Assert.Contains("public sealed class ProdutoAcabadoNormaEmbalagem", modelo, StringComparison.Ordinal);
        Assert.Contains("public sealed class ProdutoAcabadoNormaItem", modelo, StringComparison.Ordinal);
        Assert.Contains("ZAPI_PACKAGING_SRV/GetPackagingSet", client, StringComparison.Ordinal);
        Assert.Contains("Norma de embalagem", client, StringComparison.Ordinal);
        Assert.Contains("produto informado", client, StringComparison.Ordinal);
        Assert.Contains("CriarFallbackControlado", client, StringComparison.Ordinal);
        string url = ProdutoAcabadoPackagingApiClient.MontarUrlConsulta("3500024", "N001");
        Assert.Contains("ZAPI_PACKAGING_SRV/GetPackagingSet", url, StringComparison.Ordinal);
        Assert.Contains("sap-client=110", url, StringComparison.Ordinal);
    }

    [Fact]
    public void PesagemCaixa_DeveCalcularLiquidoEOrigem()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa = controller.MontarCaixa(
            OrdemProdutoAcabadoValida(),
            NormaValida(),
            1,
            2.5m,
            0.2m,
            "MANUAL");

        Assert.Equal(1, caixa.NumeroCaixa);
        Assert.Equal("CX-1000909-0001", caixa.CodigoCaixaLocal);
        Assert.Equal(2.5m, caixa.PesoBrutoKg);
        Assert.Equal(0.2m, caixa.TaraKg);
        Assert.Equal(2.3m, caixa.PesoLiquidoKg);
        Assert.Equal(12, caixa.QuantidadeProdutos);
        Assert.Equal("MANUAL", caixa.OrigemPesagem);
        Assert.Equal("PENDENTE_SAP", caixa.StatusSap);
    }

    [Fact]
    public void Tela_DeveTerF12F9TaraGridCaixasEPostDesativado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("private readonly List<ProdutoAcabadoCaixa> _caixasPesadas = [];", form, StringComparison.Ordinal);
        Assert.Contains("ReadWeightLegend_Click", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesoBalancaAsync", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarPesoManualAsync", form, StringComparison.Ordinal);
        Assert.Contains("GarantirTaraCaixaSelecionadaAsync", form, StringComparison.Ordinal);
        Assert.Contains("decimal pesoLiquidoKg = pesoBrutoKg - taraKg;", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarCaixaProdutoAcabado(pesoBrutoKg, tara.PesoKg, \"BALANCA\")", form, StringComparison.Ordinal);
        Assert.Contains("RegistrarCaixaProdutoAcabado(pesoBrutoKg, tara.PesoKg, \"MANUAL\")", form, StringComparison.Ordinal);
        Assert.Contains("Peso total das caixas ultrapassa o saldo pendente da OP.", form, StringComparison.Ordinal);
        Assert.Contains("productionCodeColumn.HeaderText = \"Caixa\";", form, StringComparison.Ordinal);
        Assert.Contains("productionDateColumn.HeaderText = \"Bruto\";", form, StringComparison.Ordinal);
        Assert.Contains("productionProductColumn.HeaderText = \"Tara/L", form, StringComparison.Ordinal);
        Assert.Contains("productionQuantityColumn.HeaderText = \"Qtd\";", form, StringComparison.Ordinal);
        Assert.Contains("productionWeightColumn.HeaderText = \"Origem/Status\";", form, StringComparison.Ordinal);
        Assert.Contains("CancelarUltimaCaixa", form, StringComparison.Ordinal);
        Assert.Contains("POST SAP", form, StringComparison.Ordinal);
        Assert.Contains("desativado", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".PostAsync(", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tela_DeveControlarBotaoStatusCabecalhoEFechamento()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("bool podeAlternarLeitura = livre && _ordemAtual is not null;", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.PrimaryText = _leituraIniciada ? \"PARAR LEITURA\" : \"INICIAR LEITURA\";", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.IconGlyph = _leituraIniciada ? \"\\uE71A\" : \"\\uE768\";", form, StringComparison.Ordinal);
        Assert.Contains("ActionDisabledColor", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.Enabled = podeAlternarLeitura;", form, StringComparison.Ordinal);
        Assert.Contains("sideReadingStatusLabel.Text = iniciada ? \"Ativo\" : \"Inativo\";", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarStatusCardLeitura(iniciada);", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarBloqueioCabecalho(iniciada);", form, StringComparison.Ordinal);
        Assert.Contains("statusValueLabel.Text = iniciada ? \"ATIVA\" : \"INATIVA\";", form, StringComparison.Ordinal);
        Assert.Contains("menuHeaderLabel.Visible = !bloqueado;", form, StringComparison.Ordinal);
        Assert.Contains("customTitleBarPanel.Cursor = bloqueado ? Cursors.Default : Cursors.SizeAll;", form, StringComparison.Ordinal);
        Assert.Contains("private bool PodeFecharTela()", form, StringComparison.Ordinal);
        Assert.Contains("Finalize a leitura antes de sair da tela.", form, StringComparison.Ordinal);
        Assert.Contains("FormClosing += ProcessoProdutoAcabadoForm_FormClosing;", form, StringComparison.Ordinal);
        Assert.Contains("if (PodeFecharTela())", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveUsarPermissaoExecutarNoF9EIconeHelper()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodoManual = ExtrairMetodo(form, "private async Task RegistrarPesoManualAsync()");

        Assert.Contains("IconeJanelaHelper.AplicarIconePadrao(this)", form, StringComparison.Ordinal);
        Assert.DoesNotContain("private void LoadWindowIcon()", form, StringComparison.Ordinal);
        Assert.Contains("PermissoesSistema.Acoes.Executar", metodoManual, StringComparison.Ordinal);
        Assert.DoesNotContain("PermissoesSistema.Acoes.PesoManual", metodoManual, StringComparison.Ordinal);
        Assert.Contains("TODO Permiss", metodoManual, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveAtualizarFallbackQuantidadePorCaixaAntesDaLeitura()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.Designer.cs");

        Assert.Contains("AtualizarNormaFallbackAntesDaLeitura()", form, StringComparison.Ordinal);
        Assert.Contains("PackagingInstruction, \"FALLBACK_MEMORIA\"", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesCaptionLabel.Text = \"QTD. POR CAIXA\";", form, StringComparison.Ordinal);
        Assert.Contains("readForecastPackagesCaptionLabel.Text = \"NORMA EMBALAGEM\";", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesCaptionLabel.Text = \"QTD. POR CAIXA\";", designer, StringComparison.Ordinal);
        Assert.Contains("readForecastPackagesCaptionLabel.Text = \"NORMA EMBALAGEM\";", designer, StringComparison.Ordinal);
        Assert.Contains("private void AtualizarCampoQuantidadePorCaixa()", form, StringComparison.Ordinal);
        Assert.Contains("bool fallback = NormaEmFallbackMemoria();", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.ReadOnly = !fallback;", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.Enabled = _ordemAtual is not null;", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.Multiline = false;", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.TextAlign = HorizontalAlignment.Left;", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.Cursor = fallback ? Cursors.IBeam : Cursors.Default;", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.TabStop = fallback;", form, StringComparison.Ordinal);
        Assert.Contains("Norma SAP indisponível. Informe a QTD. POR CAIXA antes de iniciar a leitura.", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.Focus();", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.SelectAll();", form, StringComparison.Ordinal);
        Assert.Contains("Informe a QTD. POR CAIXA para continuar", form, StringComparison.Ordinal);
        Assert.Contains("quantidade será usada em cada caixa pesada", form, StringComparison.Ordinal);
        Assert.Contains("Quantidade por caixa definida: {quantidadePorCaixa} produto(s) por caixa.", form, StringComparison.Ordinal);
        Assert.Contains("_controller.ConsultarOuPrepararNormaEmbalagem(", form, StringComparison.Ordinal);
        Assert.Contains("PreencherNormaEmbalagem();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveRestringirQuantidadePorCaixaParaInteiros()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private void ReadForecastBoxesTextBox_KeyPress(object? sender, KeyPressEventArgs e)");

        Assert.Contains("readForecastBoxesTextBox.KeyPress += ReadForecastBoxesTextBox_KeyPress;", form, StringComparison.Ordinal);
        Assert.Contains("char.IsControl(e.KeyChar)", metodo, StringComparison.Ordinal);
        Assert.Contains("!char.IsDigit(e.KeyChar)", metodo, StringComparison.Ordinal);
        Assert.Contains("e.Handled = true;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder101_DeveUsarOrdemProducaoENaoPedidoCompra()
    {
        ProdutoAcabadoCaixa caixa = new()
        {
            NumeroCaixa = 1,
            CodigoCaixaLocal = "CX-1000909-0001",
            PesoBrutoKg = 2m,
            TaraKg = 0.2m,
            PesoLiquidoKg = 1.8m,
            QuantidadeProdutos = 12,
            OrigemPesagem = "MANUAL"
        };

        ResultadoPreviewProdutoAcabado101 preview = new ProdutoAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(OrdemProdutoAcabadoValida(), caixa, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(preview.Sucesso);
        Assert.NotNull(preview.Payload);
        Assert.Equal("02", preview.Payload!.GoodsMovementCode);
        ProdutoAcabadoMaterialDocument101ItemRequest item = Assert.Single(preview.Payload.ToMaterialDocumentItem.Results);
        Assert.Equal("101", item.GoodsMovementType);
        Assert.Equal("1000909", item.ManufacturingOrder);
        Assert.Equal("0001", item.ManufacturingOrderItem);
        Assert.Equal("1.8", item.QuantityInEntryUnit);
        Assert.Contains("\"ManufacturingOrder\": \"1000909\"", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PurchaseOrder", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PurchaseOrderItem", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("KG", "1.8")]
    [InlineData("KGM", "1.8")]
    [InlineData("UN", "12")]
    [InlineData("PC", "12")]
    [InlineData("ST", "12")]
    public void Builder101_DeveDefinirQuantidadeConformeUnidadeDaOp(string unidade, string quantidadeEsperada)
    {
        ProdutoAcabadoOrdem ordem = OrdemProdutoAcabadoValida(unidade);
        ProdutoAcabadoCaixa caixa = new()
        {
            NumeroCaixa = 1,
            CodigoCaixaLocal = "CX-1000909-0001",
            PesoBrutoKg = 2m,
            TaraKg = 0.2m,
            PesoLiquidoKg = 1.8m,
            QuantidadeProdutos = 12,
            OrigemPesagem = "MANUAL"
        };

        ResultadoPreviewProdutoAcabado101 preview = new ProdutoAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(ordem, caixa, DateTime.UtcNow);

        ProdutoAcabadoMaterialDocument101ItemRequest item = Assert.Single(preview.Payload!.ToMaterialDocumentItem.Results);
        Assert.Equal(quantidadeEsperada, item.QuantityInEntryUnit);
        Assert.Equal(unidade, item.EntryUnit);
    }

    [Fact]
    public void Builder101_DeveBloquearQuantidadeNaoKgSemQuantidadeProdutos()
    {
        ProdutoAcabadoOrdem ordem = OrdemProdutoAcabadoValida("UN");
        ProdutoAcabadoCaixa caixa = new()
        {
            NumeroCaixa = 1,
            CodigoCaixaLocal = "CX-1000909-0001",
            PesoBrutoKg = 2m,
            TaraKg = 0.2m,
            PesoLiquidoKg = 1.8m,
            QuantidadeProdutos = 0,
            OrigemPesagem = "MANUAL"
        };

        ResultadoPreviewProdutoAcabado101 preview = new ProdutoAcabadoMaterialDocument101PayloadBuilder()
            .MontarPreview101(ordem, caixa, DateTime.UtcNow);

        Assert.False(preview.Sucesso);
        Assert.Contains("Quantidade de produtos da caixa", preview.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Palete_DeveGerarPreviewComHandlingUnitsESemPost()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa1 = Caixa(1, 10m, 1m, 9m);
        ProdutoAcabadoCaixa caixa2 = Caixa(2, 11m, 1m, 10m);
        ProdutoAcabadoPalete palete = controller.MontarPalete(
            OrdemProdutoAcabadoValida(),
            [caixa1, caixa2],
            1,
            2,
            "PALLET01");

        ResultadoPreviewProdutoAcabadoPalete preview = controller.GerarPreviewPalete(palete);

        Assert.True(preview.Sucesso);
        Assert.NotNull(preview.Payload);
        Assert.Equal("PLT-1000909-0001-0002", preview.Payload!.HandlingUnitExternalID);
        Assert.Equal(21m, preview.Payload.GrossWeight);
        Assert.Equal(19m, preview.Payload.NetWeight);
        Assert.Equal(2m, preview.Payload.TareWeight);
        Assert.Equal("KG", preview.Payload.WeightUnit);
        Assert.Equal("3007", preview.Payload.Plant);
        Assert.Equal("PA01", preview.Payload.StorageLocation);
        Assert.Equal("PALLET01", preview.Payload.PackagingMaterial);
        Assert.Equal("PLT-1000909-0001-0002", caixa1.CodigoPaleteLocal);
        Assert.Equal("PLT-1000909-0001-0002", caixa2.CodigoPaleteLocal);
        Assert.Equal(2, palete.Caixas.Count);
        Assert.Contains("HandlingUnitExternalID", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("GrossWeight", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("NetWeight", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("TareWeight", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("WeightUnit", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("Plant", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("StorageLocation", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("PackagingMaterial", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("_HandlingUnitItem", preview.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("CX-1000909-0001", preview.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveCriarPaleteOperacionalSemPost()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("productionActionsButton.Text = \"CRIAR PALETE\";", form, StringComparison.Ordinal);
        Assert.DoesNotContain("PREVIEW PALETE", form, StringComparison.Ordinal);
        Assert.Contains("private TextBox primeiraCaixaTextBox", form, StringComparison.Ordinal);
        Assert.Contains("private TextBox ultimaCaixaTextBox", form, StringComparison.Ordinal);
        Assert.Contains("private TextBox materialEmbalagemPaleteTextBox", form, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView", form, StringComparison.Ordinal);
        Assert.Contains("Palete local", form, StringComparison.Ordinal);
        Assert.Contains("Primeira caixa", form, StringComparison.Ordinal);
        Assert.Contains("Última caixa", form, StringComparison.Ordinal);
        Assert.Contains("Material embalagem", form, StringComparison.Ordinal);
        Assert.Contains("PENDENTE SAP", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Click += (_, _) => CriarPaleteLocal();", form, StringComparison.Ordinal);
        Assert.Contains("TryLerDadosPaletizacao(out int primeiraCaixa, out int ultimaCaixa, out string materialEmbalagem", form, StringComparison.Ordinal);
        Assert.Contains("MontarPaletePorIntervalo(primeiraCaixa, ultimaCaixa, materialEmbalagem)", form, StringComparison.Ordinal);
        Assert.Contains("private ProdutoAcabadoPalete MontarPaletePorIntervalo(int primeiraCaixa, int ultimaCaixa, string materialEmbalagem)", form, StringComparison.Ordinal);
        Assert.Contains("Informe a primeira caixa do palete.", form, StringComparison.Ordinal);
        Assert.Contains("Informe a última caixa do palete.", form, StringComparison.Ordinal);
        Assert.Contains("A primeira caixa não pode ser maior que a última.", form, StringComparison.Ordinal);
        Assert.Contains("Nenhuma caixa encontrada no intervalo informado.", form, StringComparison.Ordinal);
        Assert.Contains("caixasPaletizadas", form, StringComparison.Ordinal);
        Assert.Contains("Não é possível criar o palete. Caixa(s) já vinculada(s): {lista}.", form, StringComparison.Ordinal);
        Assert.Contains("{caixa.NumeroCaixa:0000} ({caixa.CodigoPaleteLocal})", form, StringComparison.Ordinal);
        Assert.Contains("Informe o material de embalagem do palete.", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarGridCaixas();", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarGridPaletes();", form, StringComparison.Ordinal);
        Assert.Contains("PALETE {caixa.CodigoPaleteLocal}", form, StringComparison.Ordinal);
        Assert.Contains("ProdutoAcabadoCaixa[] caixasLivres", form, StringComparison.Ordinal);
        Assert.Contains("primeiraCaixaTextBox.Text = string.Empty;", form, StringComparison.Ordinal);
        Assert.Contains("ultimaCaixaTextBox.Text = string.Empty;", form, StringComparison.Ordinal);
        Assert.Contains("Todas as caixas pesadas já foram vinculadas a paletes.", form, StringComparison.Ordinal);
        Assert.Contains("System.Diagnostics.Trace.TraceInformation(\"[ProdutoAcabado] Payload Palete local", form, StringComparison.Ordinal);
        Assert.Contains("Palete criado localmente. Envio SAP da HU/palete pendente de liberação da API.", form, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox.Show(preview.PayloadJson", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".PostAsync(", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Palete_DeveBloquearIntervaloInvalidoECaixaRepetida()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa1 = Caixa(1, 10m, 1m, 9m);
        ProdutoAcabadoCaixa caixa2 = Caixa(2, 11m, 1m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            controller.MontarPalete(OrdemProdutoAcabadoValida(), [caixa1, caixa2], 2, 1, "PALLET01"));

        ProdutoAcabadoPalete palete = controller.MontarPalete(OrdemProdutoAcabadoValida(), [caixa1, caixa2], 1, 2, "PALLET01");
        Assert.NotEmpty(palete.CodigoPaleteLocal);

        Assert.Throws<InvalidOperationException>(() =>
            controller.MontarPalete(OrdemProdutoAcabadoValida(), [caixa1, caixa2], 1, 2, "PALLET01"));
    }

    [Fact]
    public void Palete_DeveBloquearMaterialEmbalagemVazioNoPreview()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoPalete palete = controller.MontarPalete(
            OrdemProdutoAcabadoValida(),
            [Caixa(1, 10m, 1m, 9m)],
            1,
            1,
            string.Empty);

        ResultadoPreviewProdutoAcabadoPalete preview = controller.GerarPreviewPalete(palete);

        Assert.False(preview.Sucesso);
        Assert.Contains("Material de embalagem", preview.Mensagem, StringComparison.Ordinal);
    }


    [Fact]
    public void Garantias_NaoDeveAlterarFluxosProibidos()
    {
        string entrada = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string entradaItem = LerArquivoProjeto("Modelo", "IntegracaoSap", "MaterialDocumentSapItemRequest.cs");
        string consumo = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string semi = LerArquivoProjeto("Tela", "Processo", "ProcessoSemiAcabadoForm.cs");
        string produto = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string controller = LerArquivoProjeto("Controle", "Processo", "ProdutoAcabadoController.cs");

        Assert.Contains("GoodsMovementRefDocType = \"B\"", entrada, StringComparison.Ordinal);
        Assert.Contains("GoodsMovementRefDocType", entradaItem, StringComparison.Ordinal);
        Assert.Contains("ProcessoConsumoMaterialForm", consumo, StringComparison.Ordinal);
        Assert.Contains("ProcessoSemiAcabadoForm", semi, StringComparison.Ordinal);
        Assert.DoesNotContain("API_PROD_ORDER_CONFIRMATION_2_SRV", produto + controller, StringComparison.Ordinal);
        Assert.DoesNotContain(".PatchAsync(", produto + controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".PostAsync(", produto + controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE", produto + controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", produto + controller, StringComparison.OrdinalIgnoreCase);
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

    private static ProdutoAcabadoOrdem OrdemProdutoAcabadoValida(string unidade = "KG")
        => new()
        {
            NumeroOrdem = "1000909",
            MaterialProduzido = "3500024",
            Centro = "3007",
            DepositoDestino = "PA01",
            QuantidadePlanejada = 10m,
            QuantidadeEntregue = 2m,
            QuantidadePendente = 8m,
            Unidade = unidade,
            Lote = "L001",
            ItemOrdem = "0001",
            Liberada = true
        };

    private static ProdutoAcabadoNormaEmbalagem NormaValida()
        => new()
        {
            Material = "3500024",
            PackagingInstruction = "N001",
            QuantidadeProdutosPorCaixa = 12,
            Unidade = "UN"
        };

    private static ProdutoAcabadoCaixa Caixa(int numero, decimal bruto, decimal tara, decimal liquido)
        => new()
        {
            NumeroCaixa = numero,
            CodigoCaixaLocal = $"CX-1000909-{numero:0000}",
            PesoBrutoKg = bruto,
            TaraKg = tara,
            PesoLiquidoKg = liquido,
            QuantidadeProdutos = 12,
            OrigemPesagem = "MANUAL"
        };

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

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
}







