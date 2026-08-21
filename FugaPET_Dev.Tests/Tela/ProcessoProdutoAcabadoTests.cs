using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Tela.Processo;
using System.Windows.Forms;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoProdutoAcabadoTests
{
    [Fact]
    public void PaletizacaoAntiga_DoProdutoAcabado_FicaOcultaNoRuntime()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("paletização saiu do Produto Acabado", form, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("_criarPaleteCard.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("AdicionarColunaCaixa(\"caixaPaleteLocalColumn\", \"Palete local\", 110);", form, StringComparison.Ordinal);
    }

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
        // Norma vem da CONSULTA REAL (injetada aqui como fake): material caixa + qtd por caixa reais.
        ProdutoAcabadoController controller = new(
            new ProductionOrderSapFakeServico(OrdemSapValida()),
            normaEmbalagemSapServico: NormaSapValidaFake("3500024", "EMB01", "12"));

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
        Assert.Equal("EMB01", resultado.NormaEmbalagem!.MaterialCaixa);
        Assert.Equal(12, resultado.NormaEmbalagem.QuantidadeProdutosPorCaixa);
        Assert.Equal("CONSULTADA SAP", resultado.NormaEmbalagem.Status);
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
    public void NormaEmbalagem_DeveUsarDtoCruEModeloInterpretadoSemFallback()
    {
        // DTO cru separado do modelo interpretado; sem cliente/fallback fictício (FALLBACK_MEMORIA removido).
        string dto = LerArquivoProjeto("Modelo", "IntegracaoSap", "ConsultaNormaEmbalagemSapResponse.cs");
        string modeloInterno = LerArquivoProjeto("Modelo", "Processo", "NormaEmbalagemProdutoAcabado.cs");
        string interpretador = LerArquivoProjeto("Servicos", "IntegracaoSap", "InterpretadorNormaEmbalagemProdutoAcabado.cs");

        Assert.Contains("JsonPropertyName(\"Material Type\")", dto, StringComparison.Ordinal);
        Assert.Contains("public sealed class NormaEmbalagemProdutoAcabado", modeloInterno, StringComparison.Ordinal);
        Assert.Contains("MaterialCaixa", modeloInterno, StringComparison.Ordinal);

        // Fallback fictício não existe mais no projeto de produção.
        Assert.False(File.Exists(Path.Combine(RaizProjeto(), "Servicos", "IntegracaoSap", "ProdutoAcabadoPackagingApiClient.cs")));
        Assert.DoesNotContain("FALLBACK_MEMORIA", interpretador, StringComparison.Ordinal);
        string controller = LerArquivoProjeto("Controle", "Processo", "ProdutoAcabadoController.cs");
        Assert.DoesNotContain("FALLBACK_MEMORIA", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("CriarFallbackControlado", controller, StringComparison.Ordinal);
    }

    private static IProdutoAcabadoNormaEmbalagemSapServico NormaSapValidaFake(
        string materialProduto, string materialCaixa, string quantidadePorCaixa)
        => new NormaSapFakeTela(ResultadoConsultaNormaEmbalagemSap.DeEncontrada(
            new ConsultaNormaEmbalagemSapResponse
            {
                Material = materialProduto,
                PackagingInstruction = string.Empty,
                PkgInstructionItems =
                [
                    new ConsultaNormaEmbalagemSapItemResponse { Material = materialCaixa, MaterialType = "P", Quantity = "1", QtyUOM = "ST", Item = "10" },
                    new ConsultaNormaEmbalagemSapItemResponse { Material = materialProduto, MaterialType = "I", Quantity = quantidadePorCaixa, QtyUOM = "UN", Item = "20" }
                ]
            },
            200, "https://host/GetPackagingSet", "cid"));

    private sealed class NormaSapFakeTela : IProdutoAcabadoNormaEmbalagemSapServico
    {
        private readonly ResultadoConsultaNormaEmbalagemSap _resultado;
        public NormaSapFakeTela(ResultadoConsultaNormaEmbalagemSap resultado) => _resultado = resultado;
        public bool Configurado => true;
        public Task<ResultadoConsultaNormaEmbalagemSap> ObterNormaAsync(string material, CancellationToken cancellationToken = default)
            => Task.FromResult(_resultado);
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
        // Produto Acabado deixou o movimento 101: a caixa nasce FINALIZADA_LOCAL (status de integração HU),
        // com correlation_id gerado uma única vez.
        Assert.Equal(StatusIntegracaoCaixa.FinalizadaLocal, caixa.StatusIntegracao);
        Assert.NotEqual(Guid.Empty, caixa.CorrelationId);
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
        Assert.Contains("await RegistrarCaixaProdutoAcabadoAsync(pesoBrutoKg, tara.PesoKg, \"BALANCA\")", form, StringComparison.Ordinal);
        Assert.Contains("await RegistrarCaixaProdutoAcabadoAsync(pesoBrutoKg, tara.PesoKg, \"MANUAL\")", form, StringComparison.Ordinal);
        Assert.Contains("Peso total das caixas ultrapassa o saldo pendente da OP.", form, StringComparison.Ordinal);
        // Tarefa 21.6 (Ajuste 6): colunas separadas (sem cabecalho combinado Tara/Líquido ou Origem/Status).
        Assert.Contains("AdicionarColunaCaixa(\"caixaTaraColumn\", \"Tara\"", form, StringComparison.Ordinal);
        Assert.Contains("AdicionarColunaCaixa(\"caixaPesoLiquidoColumn\", \"Peso líquido\"", form, StringComparison.Ordinal);
        Assert.Contains("AdicionarColunaCaixa(\"caixaOrigemColumn\", \"Origem\"", form, StringComparison.Ordinal);
        Assert.Contains("AdicionarColunaCaixa(\"caixaStatusColumn\", \"Status\"", form, StringComparison.Ordinal);
        Assert.Contains("AdicionarColunaCaixa(\"caixaPaleteLocalColumn\", \"Palete local\"", form, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Tara/Líquido\"", form, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Origem/Status\"", form, StringComparison.Ordinal);
        Assert.Contains("SolicitarCancelamentoUltimaCaixaAsync", form, StringComparison.Ordinal);
        // REV4-§11: texto NEUTRO (nunca hardcoda DEV/HML na regra de envio).
        Assert.Contains("Envio de HU SAP não habilitado nesta execução", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Envio SAP não autorizado no ambiente DEV", form, StringComparison.Ordinal);
        Assert.Contains("await _controller.FinalizarCaixaLocalAsync(", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".PostAsync(", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tela_DeveControlarBotaoStatusCabecalhoEFechamento()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // Tarefa 21.6.2: iniciar exige OP + QTD. por caixa válida (verde só quando pode pesar).
        Assert.Contains("bool podeAlternarLeitura = livre && _ordemAtual is not null && QuantidadePorCaixaValida() && !caixaAtiva;", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.PrimaryText = _leituraIniciada ? \"PARAR LEITURA\" : \"INICIAR LEITURA\";", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.IconGlyph = _leituraIniciada ? \"\\uE71A\" : \"\\uE768\";", form, StringComparison.Ordinal);
        Assert.Contains("ActionDisabledColor", form, StringComparison.Ordinal);
        // Tarefa 21.6.2: durante a leitura o botão (PARAR) segue habilitado para poder parar.
        Assert.Contains("iniciarLeituraButton.Enabled = _leituraIniciada || podeAlternarLeitura;", form, StringComparison.Ordinal);
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
    public void Tela_DeveUsarNormaRealSemFallbackMemoria()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // FALLBACK_MEMORIA removido; QTD. POR CAIXA sempre somente leitura (vem da consulta real).
        Assert.DoesNotContain("FALLBACK_MEMORIA", form, StringComparison.Ordinal);
        Assert.DoesNotContain("NormaEmFallbackMemoria", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsultarOuPrepararNormaEmbalagem", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesTextBox.ReadOnly = true;", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesCaptionLabel.Text = \"QTD. POR CAIXA\";", form, StringComparison.Ordinal);

        // Validação antes da leitura bloqueia quando não há norma válida (SEM NORMA CADASTRADA).
        Assert.Contains("private bool ValidarNormaEmbalagemAntesDaLeitura()", form, StringComparison.Ordinal);
        string validar = ExtrairMetodo(form, "private bool ValidarNormaEmbalagemAntesDaLeitura()");
        Assert.Contains("_normaEmbalagem.MaterialCaixa", validar, StringComparison.Ordinal);
        Assert.Contains("_normaEmbalagem.QuantidadeProdutosPorCaixa <= 0", validar, StringComparison.Ordinal);

        // STATUS NORMA usa o status real / SEM NORMA CADASTRADA; NORMA vazia mostra "NÃO INFORMADA".
        Assert.Contains("SEM NORMA CADASTRADA", form, StringComparison.Ordinal);
        Assert.Contains("NÃO INFORMADA", form, StringComparison.Ordinal);
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

    // Os antigos testes Builder101_* foram removidos: Produto Acabado deixou o movimento 101 /
    // Material Document. A cobertura da caixa individual (Handling Unit) está em
    // ProdutoAcabadoHandlingUnitCaixaTests (preview, idempotência, envio manual, escopo).

    [Fact]
    public void Palete_DeveGerarPreviewComHandlingUnitsESemPost()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa1 = Caixa(1, 10m, 1m, 9m);
        ProdutoAcabadoCaixa caixa2 = Caixa(2, 11m, 1m, 10m);
        ProdutoAcabadoPalete palete = controller.MontarPalete(
            OrdemProdutoAcabadoValida(),
            [caixa1, caixa2],
            [], // REV4-§13: nenhum palete existente ainda
            1,
            2,
            "PACK_TEST_001");

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
        Assert.Equal("PACK_TEST_001", preview.Payload.PackagingMaterial);
        Assert.True(string.IsNullOrWhiteSpace(caixa1.CodigoPaleteLocal));
        Assert.True(string.IsNullOrWhiteSpace(caixa2.CodigoPaleteLocal));
        controller.ConfirmarVinculoPaleteLocal(palete);
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
        // §18/§27: o payload contém as HUs SAP das caixas (não mais o CodigoCaixaLocal — fallback removido).
        Assert.Contains("300010001", preview.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("CX-1000909-0001", preview.PayloadJson, StringComparison.Ordinal);
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
        Assert.Contains("palete.StatusSap", form, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Click += (_, _) => CriarPaleteLocal();", form, StringComparison.Ordinal);
        Assert.Contains("TryLerDadosPaletizacao(out int primeiraCaixa, out int ultimaCaixa, out string materialEmbalagem", form, StringComparison.Ordinal);
        Assert.Contains("MontarPaletePorIntervalo(primeiraCaixa, ultimaCaixa, materialEmbalagem)", form, StringComparison.Ordinal);
        Assert.Contains("private ProdutoAcabadoPalete MontarPaletePorIntervalo(int primeiraCaixa, int ultimaCaixa, string materialEmbalagem)", form, StringComparison.Ordinal);
        Assert.Contains("_controller.CriarPaleteLocalPersistenteAsync(palete, usuario, terminal)", form, StringComparison.Ordinal);
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
        // Tarefa 21.6 (Ajuste 6): o vínculo da caixa ao palete agora vai na coluna dedicada "Palete local".
        Assert.Contains("string.IsNullOrWhiteSpace(caixa.CodigoPaleteLocal) ? \"-\" : caixa.CodigoPaleteLocal", form, StringComparison.Ordinal);
        Assert.Contains("ProdutoAcabadoCaixa[] caixasLivres", form, StringComparison.Ordinal);
        Assert.Contains("primeiraCaixaTextBox.Text = string.Empty;", form, StringComparison.Ordinal);
        Assert.Contains("ultimaCaixaTextBox.Text = string.Empty;", form, StringComparison.Ordinal);
        Assert.Contains("Todas as caixas pesadas já foram vinculadas a paletes.", form, StringComparison.Ordinal);
        Assert.Contains("System.Diagnostics.Trace.TraceInformation(\"[ProdutoAcabado] Palete local persistido", form, StringComparison.Ordinal);
        Assert.Contains("PALETE CRIADO LOCALMENTE em RASCUNHO. Nenhum envio SAP foi executado.", form, StringComparison.Ordinal);
        Assert.DoesNotContain("MessageBox.Show(preview.PayloadJson", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".PostAsync(", form, StringComparison.OrdinalIgnoreCase);
        // GATE 046-E §12: BANCO autoritativo — recarrega paletes persistidos ao carregar OP e após criar.
        Assert.Contains("private async Task<bool> RecarregarPaletesPersistidosAsync()", form, StringComparison.Ordinal);
        Assert.Contains("await _controller.RecarregarPaletesLocaisAsync(_ordemAtual.NumeroOrdem, terminal)", form, StringComparison.Ordinal);
        Assert.Contains("await RecarregarPaletesPersistidosAsync();", form, StringComparison.Ordinal);
        // GATE 046-E REV2 (Blocker 2): sem fallback de memória — persistir+reload-falhou NÃO injeta o objeto
        // recém-criado na coleção nem afirma reconstrução persistente. BANCO continua fonte autoritativa.
        Assert.DoesNotContain("_paletesMontados.Add(palete)", form, StringComparison.Ordinal);
        Assert.Contains("Palete PERSISTIDO em RASCUNHO, mas a atualização da tela falhou", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Palete_DeveBloquearIntervaloInvalidoECaixaRepetida()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoCaixa caixa1 = Caixa(1, 10m, 1m, 9m);
        ProdutoAcabadoCaixa caixa2 = Caixa(2, 11m, 1m, 10m);

        Assert.Throws<InvalidOperationException>(() =>
            controller.MontarPalete(OrdemProdutoAcabadoValida(), [caixa1, caixa2], [], 2, 1, "PACK_TEST_001"));

        ProdutoAcabadoPalete palete = controller.MontarPalete(OrdemProdutoAcabadoValida(), [caixa1, caixa2], [], 1, 2, "PACK_TEST_001");
        Assert.NotEmpty(palete.CodigoPaleteLocal);
        Assert.True(string.IsNullOrWhiteSpace(caixa1.CodigoPaleteLocal));
        Assert.True(string.IsNullOrWhiteSpace(caixa2.CodigoPaleteLocal));
        controller.ConfirmarVinculoPaleteLocal(palete);

        Assert.Throws<InvalidOperationException>(() =>
            controller.MontarPalete(OrdemProdutoAcabadoValida(), [caixa1, caixa2], [], 1, 2, "PACK_TEST_001"));
    }

    [Fact]
    public void Palete_DeveBloquearMaterialEmbalagemVazioNoPreview()
    {
        ProdutoAcabadoController controller = new(new ProductionOrderSapFakeServico(OrdemSapValida()));
        ProdutoAcabadoPalete palete = controller.MontarPalete(
            OrdemProdutoAcabadoValida(),
            [Caixa(1, 10m, 1m, 9m)],
            [],
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

    // ===================== Correção HU 1ª entrega (Form §6-§10) =====================

    [Fact]
    public void FormHu_UmaCaixaPorVez_BloqueiaSegundaPesagem()
    {
        // §7/§11.C: guard explícito "uma caixa por vez" (não usa apenas Count+1 como autorização).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("private static readonly bool PrimeiraEntregaHu = true;", form, StringComparison.Ordinal);
        Assert.Contains("if (PrimeiraEntregaHu && ExisteCaixaAtiva())", form, StringComparison.Ordinal);
        Assert.Contains("private bool ExisteCaixaAtiva()", form, StringComparison.Ordinal);
        Assert.Contains("Confirme o envio ou cancele a caixa atual antes de pesar outra.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_PaleteForaDoFluxoAtivo()
    {
        // REV8/§6: palete FORA do fluxo por PADRÃO (HU-only), porém NÃO permanentemente. Fica oculto/inativo
        // apenas quando PrimeiraEntregaHu E o gate do pipeline 045 está DESLIGADO; com o gate ligado, o palete
        // volta a ser montável (local, zero POST) e integrável (INT012 por ação explícita).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string criarPalete = ExtrairMetodo(form, "private async void CriarPaleteLocal()");

        // O acionamento retorna cedo SOMENTE em HU-only (gate off) — não mais permanentemente.
        Assert.Contains("if (PrimeiraEntregaHu && !_controller.PipelinePaGateHabilitado)", criarPalete, StringComparison.Ordinal);
        Assert.Contains("return;", criarPalete, StringComparison.Ordinal);
        Assert.DoesNotContain("MontarPaletePorIntervalo", criarPalete.Split("if (PrimeiraEntregaHu")[0], StringComparison.Ordinal);
        // Card/título/grid ocultados apenas no modo HU-only (mesma condição do gate).
        Assert.Contains("PrimeiraEntregaHu && !_controller.PipelinePaGateHabilitado", form, StringComparison.Ordinal);
        Assert.Contains("_criarPaleteCard.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.Visible = false;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_MaterialEmbalagemExplicitoBloqueiaPreviewInvalido()
    {
        // §9/§11.E: material de embalagem por origem controlada (nunca PALLET01); preview inválido bloqueia add.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string registrar = ExtrairMetodo(form, "private async Task<bool> RegistrarCaixaProdutoAcabadoAsync(decimal pesoBrutoKg, decimal taraKg, string origem)");

        Assert.Contains("ObterMaterialEmbalagemCaixaControlada()", registrar, StringComparison.Ordinal);
        Assert.Contains("await _controller.FinalizarCaixaLocalAsync(", registrar, StringComparison.Ordinal);
        Assert.Contains("if (!resultado.Sucesso || resultado.Caixa is null)", registrar, StringComparison.Ordinal);
        // A caixa (snapshot persistido) só é adicionada DEPOIS da checagem de sucesso da persistência.
        int idxCheck = registrar.IndexOf("if (!resultado.Sucesso || resultado.Caixa is null)", StringComparison.Ordinal);
        int idxAdd = registrar.IndexOf("_caixasPesadas.Add(resultado.Caixa);", StringComparison.Ordinal);
        Assert.True(idxCheck >= 0 && idxAdd > idxCheck, "O Add do snapshot deve ocorrer após a checagem de sucesso.");
        // Origem controlada nunca PALLET01.
        Assert.Contains("private static bool EhMaterialPalete(string? material)", form, StringComparison.Ordinal);
        Assert.Contains("OrigemMaterialEmbalagemCaixa.NaoInformada", form, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_BotaoEnvioDinamico_HabilitaPorEstadoEAutorizacao()
    {
        // REV2 §5/§6: botão "ENVIAR CAIXA SAP" DINÂMICO — habilitado só com gateway autorizado + caixa
        // elegível + sem envio em andamento; o clique delega ao Controller (a Form nunca faz HTTP).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // REV4-§11: rótulo NEUTRO (sem sufixo de ambiente HML).
        Assert.Contains("Text = \"ENVIAR CAIXA SAP\",", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ENVIAR CAIXA SAP HML", form, StringComparison.Ordinal);
        Assert.Contains("private void ConfigurarBotaoEnvioCaixaSap()", form, StringComparison.Ordinal);
        // O clique delega ao handler async (nenhum POST/HTTP direto na Form).
        Assert.Contains("await SolicitarEnvioCaixaSapAsync()", form, StringComparison.Ordinal);
        Assert.Contains("await _controller.EnviarCaixaHandlingUnitAsync(codigo, usuario, terminal)", form, StringComparison.Ordinal);

        // Estado do botão é DINÂMICO (não mais fixo em false).
        string atualizar = ExtrairMetodo(form, "private void AtualizarEstadoEnvioCaixaSap()");
        Assert.Contains("_controller.EnvioHuAutorizado && elegivelEnvio", atualizar, StringComparison.Ordinal);
        // REV3-B5: bloqueio local por estado indeterminado (nunca reabilita por objeto stale).
        Assert.Contains("!_envioCaixaSapEmAndamento && !bloqueadaPorEstadoIndeterminado", atualizar, StringComparison.Ordinal);
        Assert.Contains("StatusIntegracaoCaixa.AguardandoAutorizacaoSap", atualizar, StringComparison.Ordinal);
        Assert.Contains("StatusIntegracaoCaixa.ProntaParaEnvio", atualizar, StringComparison.Ordinal);
        Assert.DoesNotContain("_enviarCaixaSapButton.Enabled = false;", atualizar, StringComparison.Ordinal); // não é fixo
        // Proteção de duplo clique + fail-closed textual NEUTRO + nenhum POST/PatchAsync na Form.
        Assert.Contains("if (_envioCaixaSapEmAndamento", form, StringComparison.Ordinal);
        Assert.Contains("Envio de HU SAP não habilitado nesta execução", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".PostAsync(", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".PatchAsync(", form, StringComparison.OrdinalIgnoreCase);
        // Estados visuais preparados.
        Assert.Contains("\"AGUARDANDO AUTORIZAÇÃO SAP\"", form, StringComparison.Ordinal);
        Assert.Contains("\"CONFIRMADA SAP\"", form, StringComparison.Ordinal);
        Assert.Contains("\"ERRO SAP\"", form, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_DevePreservarCaixaAtivaEOcultarPaleteEmTodaAtualizacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string atualizar = ExtrairMetodo(form, "private void AtualizarBotoesOperacao()");
        string limpar = ExtrairMetodo(form, "private bool LimparOp()");
        string cancelar = ExtrairMetodo(form, "private async Task SolicitarCancelamentoUltimaCaixaAsync()");
        string teclas = ExtrairMetodo(form, "private async void ProcessoProdutoAcabadoForm_KeyDown(object? sender, KeyEventArgs e)");

        // §3: cancelamento persiste via Controller (nunca só memória).
        Assert.Contains("CancelarCaixaHandlingUnitAsync(codigo", cancelar, StringComparison.Ordinal);
        // REV8/§6: palete oculto em toda atualização SÓ em HU-only (gate off); gate on ⇒ palete acessível.
        Assert.DoesNotContain("if (PrimeiraEntregaHu && !_controller.PipelinePaGateHabilitado)", atualizar, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Visible = false;", atualizar, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Enabled = false;", atualizar, StringComparison.Ordinal);
        Assert.Contains("_criarPaleteCard.Visible = false;", atualizar, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.Visible = false;", atualizar, StringComparison.Ordinal);

        Assert.Contains("bool caixaAtiva = ExisteCaixaAtiva();", atualizar, StringComparison.Ordinal);
        Assert.Contains("!caixaAtiva", atualizar, StringComparison.Ordinal);
        Assert.Contains("Keys.F5 or Keys.F9 or Keys.F12", teclas, StringComparison.Ordinal);

        int guardaLimpeza = limpar.IndexOf("if (!PodeTrocarOuLimparOp())", StringComparison.Ordinal);
        int descarte = limpar.IndexOf("_caixasPesadas.Clear();", StringComparison.Ordinal);
        Assert.True(guardaLimpeza >= 0 && descarte > guardaLimpeza);
        int transicaoCancelamento = form.IndexOf("caixa.TransicionarPara(StatusIntegracaoCaixa.Cancelada);", StringComparison.Ordinal);
        int remocao = form.IndexOf("caixas.RemoveAt(caixas.Count - 1);", StringComparison.Ordinal);
        Assert.True(transicaoCancelamento >= 0 && remocao > transicaoCancelamento);
        Assert.Contains("MessageBoxButtons.YesNo", cancelar, StringComparison.Ordinal);
        Assert.Contains("MessageBoxDefaultButton.Button2", cancelar, StringComparison.Ordinal);
        Assert.Contains("PodeFecharTela()", teclas, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_RegistroReal_DeveAtualizarEstadoVisualDoF6()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string registrar = ExtrairMetodo(
            form,
            "private async Task<bool> RegistrarCaixaProdutoAcabadoAsync(decimal pesoBrutoKg, decimal taraKg, string origem)");
        // §1: delega ao Controller/Service persistente (nunca transição manual de estado persistente).
        int delegacao = registrar.IndexOf("await _controller.FinalizarCaixaLocalAsync(", StringComparison.Ordinal);
        int atualizacaoVisual = registrar.IndexOf("AtualizarBotoesOperacao();", StringComparison.Ordinal);
        Assert.True(delegacao >= 0 && atualizacaoVisual > delegacao);
        Assert.DoesNotContain("caixa.TransicionarPara(StatusIntegracaoCaixa.AguardandoAutorizacaoSap);", registrar, StringComparison.Ordinal);
        Assert.DoesNotContain("caixa.TransicionarPara(StatusIntegracaoCaixa.PreviewHuGerado);", registrar, StringComparison.Ordinal);
        Assert.Contains("_caixasPesadas.Add(resultado.Caixa);", registrar, StringComparison.Ordinal);

        global::FugaPET_Dev.Tela.ActionPillButton botao = new();
        List<ProdutoAcabadoCaixa> caixas = [];
        ProcessoProdutoAcabadoForm.AtualizarEstadoBotaoExcluirUltima(botao, caixas);
        Assert.False(botao.Enabled);

        ProdutoAcabadoCaixa caixa = CaixaCancelavel();
        caixa.StatusIntegracao = StatusIntegracaoCaixa.FinalizadaLocal;
        caixas.Add(caixa);
        caixa.TransicionarPara(StatusIntegracaoCaixa.PreviewHuGerado);
        caixa.TransicionarPara(StatusIntegracaoCaixa.AguardandoAutorizacaoSap);
        ProcessoProdutoAcabadoForm.AtualizarEstadoBotaoExcluirUltima(botao, caixas);
        Assert.True(botao.Enabled);
        Assert.Equal(Cursors.Hand, botao.Cursor);

        Assert.False(ProcessoProdutoAcabadoForm.CancelarUltimaCaixaEmMemoria(caixas, DialogResult.No));
        ProcessoProdutoAcabadoForm.AtualizarEstadoBotaoExcluirUltima(botao, caixas);
        Assert.True(botao.Enabled);
        Assert.Single(caixas);
        Assert.Equal(60, caixas.Sum(item => item.QuantidadeProdutos));

        Assert.True(ProcessoProdutoAcabadoForm.CancelarUltimaCaixaEmMemoria(caixas, DialogResult.Yes));
        ProcessoProdutoAcabadoForm.AtualizarEstadoBotaoExcluirUltima(botao, caixas);
        Assert.False(botao.Enabled);
        Assert.Empty(caixas);
        Assert.Equal(0, caixas.Sum(item => item.QuantidadeProdutos));
    }

    [Fact]
    public void FormHu_CancelamentoNo_DevePreservarCaixaProdutosEStatus()
    {
        ProdutoAcabadoCaixa caixa = CaixaCancelavel();
        List<ProdutoAcabadoCaixa> caixas = [caixa];

        bool cancelada = ProcessoProdutoAcabadoForm.CancelarUltimaCaixaEmMemoria(caixas, DialogResult.No);

        Assert.False(cancelada);
        Assert.Single(caixas);
        Assert.Equal(60, caixas.Sum(item => item.QuantidadeProdutos));
        Assert.Equal(StatusIntegracaoCaixa.AguardandoAutorizacaoSap, caixa.StatusIntegracao);
    }

    [Fact]
    public void FormHu_CancelamentoYes_DeveTransicionarAntesDeRemoverELiberarNovaPesagem()
    {
        ProdutoAcabadoCaixa caixa = CaixaCancelavel();
        List<ProdutoAcabadoCaixa> caixas = [caixa];

        bool cancelada = ProcessoProdutoAcabadoForm.CancelarUltimaCaixaEmMemoria(caixas, DialogResult.Yes);

        Assert.True(cancelada);
        Assert.Equal(StatusIntegracaoCaixa.Cancelada, caixa.StatusIntegracao);
        Assert.Empty(caixas);
        Assert.Equal(0, caixas.Sum(item => item.QuantidadeProdutos));
        Assert.False(ProcessoProdutoAcabadoForm.PodeCancelarUltimaCaixa(caixas));
    }

    [Theory]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    public void FormHu_EstadoNaoCancelavel_DeveBloquearCancelamento(StatusIntegracaoCaixa status)
    {
        ProdutoAcabadoCaixa caixa = CaixaCancelavel();
        caixa.StatusIntegracao = status;
        List<ProdutoAcabadoCaixa> caixas = [caixa];

        Assert.False(ProcessoProdutoAcabadoForm.PodeCancelarUltimaCaixa(caixas));
        Assert.False(ProcessoProdutoAcabadoForm.CancelarUltimaCaixaEmMemoria(caixas, DialogResult.Yes));
        Assert.Single(caixas);
        Assert.Equal(status, caixa.StatusIntegracao);
    }

    [Fact]
    public void FormHu_BotaoEF6_DevemUsarMesmaRotinaEF7FicarForaDoFluxo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string eventos = ExtrairMetodo(form, "private void ConfigurarEventos()");
        string teclas = ExtrairMetodo(form, "private async void ProcessoProdutoAcabadoForm_KeyDown(object? sender, KeyEventArgs e)");
        string atualizar = ExtrairMetodo(form, "private void AtualizarBotoesOperacao()");

        Assert.Contains("excluirUltimaButton.Click += ExcluirUltimaButton_Click;", eventos, StringComparison.Ordinal);
        Assert.DoesNotContain("deleteLastLegendPanel.Click", eventos, StringComparison.Ordinal);
        Assert.Contains("=> await SolicitarCancelamentoUltimaCaixaAsync();", form, StringComparison.Ordinal);
        Assert.Contains("e.KeyCode is Keys.F6 or Keys.Delete", teclas, StringComparison.Ordinal);
        Assert.Contains("e.SuppressKeyPress = true;", teclas, StringComparison.Ordinal);
        Assert.Contains("excluirUltimaButton.Visible && excluirUltimaButton.Enabled", teclas, StringComparison.Ordinal);
        Assert.Contains("await SolicitarCancelamentoUltimaCaixaAsync();", teclas, StringComparison.Ordinal);
        Assert.DoesNotContain("RegistrarPeso", teclas[teclas.IndexOf("Keys.F6 or Keys.Delete", StringComparison.Ordinal)..], StringComparison.Ordinal);

        Assert.Contains("AtualizarEstadoBotaoExcluirUltima(excluirUltimaButton, _caixasPesadas);", atualizar, StringComparison.Ordinal);
        string atualizarExcluir = ExtrairMetodo(
            form,
            "internal static void AtualizarEstadoBotaoExcluirUltima(");
        Assert.Contains("botao.Enabled = podeCancelarUltimaCaixa;", atualizarExcluir, StringComparison.Ordinal);
        Assert.Contains("PodeCancelarUltimaCaixa(caixas)", atualizarExcluir, StringComparison.Ordinal);
        Assert.Contains("excluirCodigoButton.Visible = false;", eventos, StringComparison.Ordinal);
        Assert.Contains("excluirCodigoButton.KeyHint = string.Empty;", eventos, StringComparison.Ordinal);
        Assert.DoesNotContain("Keys.F7", teclas, StringComparison.Ordinal);
        Assert.DoesNotContain("ProdutoAcabadoRepositorioIndisponivel", form, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_NaoDeveInventarMaterialNemNumeroDefinitivo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string controller = LerArquivoProjeto("Controle", "Processo", "ProdutoAcabadoController.cs");
        string contrato = LerArquivoProjeto("AcessoDados", "Repositorio", "IProdutoAcabadoRepositorio.cs");

        Assert.DoesNotContain("EMB_CX_PADRAO", form + controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_caixasPesadas.Count + 1", form, StringComparison.Ordinal);
        // §1: a Form NÃO monta identidade/numeração local — delega a finalização persistente ao Controller.
        Assert.Contains("await _controller.FinalizarCaixaLocalAsync(", form, StringComparison.Ordinal);
        Assert.Contains("OrigemMaterialEmbalagemCaixa.NaoInformada", form, StringComparison.Ordinal);
        Assert.Contains("Material de embalagem da caixa não informado.",
            LerArquivoProjeto("Servicos", "IntegracaoSap", "ProdutoAcabadoHandlingUnitCaixaRequestBuilder.cs"),
            StringComparison.Ordinal);
        // A identidade (numero_caixa/codigo_caixa_local/hu_caixa) é gerada ATOMICAMENTE pelo banco — o app nunca a fornece.
        Assert.Contains("gerada ATOMICAMENTE pelo banco", contrato, StringComparison.Ordinal);
        Assert.Contains("numero_caixa", contrato, StringComparison.Ordinal);
        Assert.Contains("correlation_id", contrato, StringComparison.Ordinal);
        Assert.Contains("Uma caixa por vez por terminal", contrato, StringComparison.Ordinal);
    }

    [Fact]
    public void FormHu_BotaoEnvioManualDevePermanecerDentroDoPainelSemSobreposicao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.Designer.cs");
        string configurar = ExtrairMetodo(form, "private void ConfigurarBotaoEnvioCaixaSap()");
        string atualizar = ExtrairMetodo(form, "private void AtualizarEstadoEnvioCaixaSap()");

        // REGRA 2: ENVIAR CAIXA SAP tem POSIÇÃO PRÓPRIA (faixa livre y=534), não a do botão de PESAGEM MANUAL.
        Assert.Contains("Location = new Point(leituraManualButton.Location.X, 534),", configurar, StringComparison.Ordinal);
        Assert.Contains("Size = new Size(leituraManualButton.Size.Width, leituraManualButton.Size.Height),", configurar, StringComparison.Ordinal);
        Assert.Contains("Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,", configurar, StringComparison.Ordinal);
        Assert.DoesNotContain("Location = leituraManualButton.Location,", configurar, StringComparison.Ordinal);
        // REGRA 2: AtualizarEstadoEnvioCaixaSap NÃO esconde mais a PESAGEM MANUAL (visibilidade é de AtualizarBotoesOperacao).
        Assert.DoesNotContain("leituraManualButton.Visible", atualizar, StringComparison.Ordinal);

        // Não-sobreposição: o botão SAP (y=534..572) fica ABAIXO do botão de pesagem manual (y=442..480) e
        // dentro do painel (215x604).
        const int parentWidth = 215;
        const int parentHeight = 604;
        const int left = 12;
        const int manualTop = 442;
        const int manualHeight = 38;
        const int sapTop = 534;
        const int width = 190;
        const int height = 38;
        Assert.True(left + width <= parentWidth);
        Assert.True(sapTop >= manualTop + manualHeight, "SAP não sobrepõe PESAGEM MANUAL");
        Assert.True(sapTop + height <= parentHeight, "SAP dentro do painel");
        Assert.Contains("sidePanel.Size = new Size(215, 604);", designer, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.Location = new Point(12, 442);", designer, StringComparison.Ordinal);
        Assert.Contains("leituraManualButton.Size = new Size(190, 38);", designer, StringComparison.Ordinal);
        // A faixa y=534 é a de excluirCodigoButton, que é permanentemente oculto (sem conflito visual).
        Assert.Contains("excluirCodigoButton.Location = new Point(12, 534);", designer, StringComparison.Ordinal);
    }

    private static ProdutoAcabadoCaixa CaixaCancelavel()
        => new()
        {
            NumeroOrdemProducao = "1001951",
            Material = "4000108",
            PesoBrutoKg = 2.500m,
            TaraKg = 0.500m,
            PesoLiquidoKg = 2.000m,
            QuantidadeProdutos = 60,
            CorrelationId = Guid.NewGuid(),
            StatusIntegracao = StatusIntegracaoCaixa.AguardandoAutorizacaoSap
        };

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
            CodigoProdutoAcabadoCaixa = numero,
            NumeroCaixa = numero,
            CodigoCaixaLocal = $"CX-1000909-{numero:0000}",
            NumeroOrdemProducao = "1000909",
            PesoBrutoKg = bruto,
            TaraKg = tara,
            PesoLiquidoKg = liquido,
            QuantidadeProdutos = 12,
            OrigemPesagem = "MANUAL",
            // Palete só aceita CONFIRMADA_SAP com HU SAP individual (regras endurecidas §10/§11).
            StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap,
            HandlingUnitExternalId = $"30001{numero:0000}"
        };

    [Fact]
    public void Tela_21_6_CardDadosSemDataEStatusNorma()
    {
        // Testes 1-4: card de dados produtivos sem titulo "DATAS"; card de norma com QTD/NORMA/STATUS fallback.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("dateTitleLabel.Text = \"DADOS DA OP\";", form, StringComparison.Ordinal);
        Assert.Contains("readForecastBoxesCaptionLabel.Text = \"QTD. POR CAIXA\";", form, StringComparison.Ordinal);
        Assert.Contains("readForecastPackagesCaptionLabel.Text = \"NORMA EMBALAGEM\";", form, StringComparison.Ordinal);
        Assert.Contains("balanceCaptionLabel.Text = \"STATUS NORMA\";", form, StringComparison.Ordinal);
        // STATUS NORMA agora reflete a consulta real (CONSULTADA SAP) ou SEM NORMA CADASTRADA (sem fallback).
        Assert.Contains("CONSULTADA SAP", form, StringComparison.Ordinal);
        Assert.Contains("SEM NORMA CADASTRADA", form, StringComparison.Ordinal);
        Assert.Contains("MATERIAL CAIXA", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_AlertaBalancaNaLateralNaoNoCard()
    {
        // Teste 5: a mensagem de balança virou pendência operacional na lateral direita (fora do card).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("CriarBlocoPendenciaBalanca", form, StringComparison.Ordinal);
        Assert.Contains("PENDÊNCIA OPERACIONAL", form, StringComparison.Ordinal);
        Assert.Contains("Balança de produto acabado não configurada.", form, StringComparison.Ordinal);
        // A mensagem nao fica mais dentro do balanceTextBox do card de produção.
        Assert.Contains("balanceTextBox.Text = string.Empty;", form, StringComparison.Ordinal);
        Assert.DoesNotContain("balanceTextBox.Text = MensagemBalancaProdutoAcabadoNaoConfigurada;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_NormaVaziaMensagemAmigavel()
    {
        // Testes 6/7: norma vazia mostra mensagem amigável; cabeçalhos completos (sem truncar).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("Norma de embalagem não retornou itens. Usando fallback de quantidade por caixa.", form, StringComparison.Ordinal);
        Assert.Contains("materialLotColumn.HeaderText = \"Quantidade\";", form, StringComparison.Ordinal);
        Assert.Contains("materialExpirationColumn.HeaderText = \"Unidade\";", form, StringComparison.Ordinal);
        Assert.Contains("materialStatusColumn.HeaderText = \"Tipo\";", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_CardCriarPaleteEGridPaletes()
    {
        // Testes 8-10/14/15: card CRIAR PALETE com labels completos + grid "Paletes Criados".
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("Text = \"CRIAR PALETE\"", form, StringComparison.Ordinal);
        Assert.Contains("CriarLabelPaletizacao(\"Primeira caixa\")", form, StringComparison.Ordinal);
        Assert.Contains("CriarLabelPaletizacao(\"Última caixa\")", form, StringComparison.Ordinal);
        Assert.Contains("CriarLabelPaletizacao(\"Material embalagem\")", form, StringComparison.Ordinal);
        Assert.Contains("Text = \"Paletes Criados\"", form, StringComparison.Ordinal);
        Assert.Contains("CriarPaleteLocal()", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_LateralDireitaEBotoesExcluir()
    {
        // Testes 16-19: termos operacionais na lateral + estado dos botões de excluir.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("boxesTitleLabel.Text = \"CAIXAS PESADAS\";", form, StringComparison.Ordinal);
        Assert.Contains("packagesTitleLabel.Text = \"PESO REGISTRADO\";", form, StringComparison.Ordinal);
        Assert.DoesNotContain("LEITURA - PACOTES", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarEstadoBotaoExcluirUltima(excluirUltimaButton, _caixasPesadas);", form, StringComparison.Ordinal);
        Assert.Contains("TransicaoStatusIntegracaoCaixa.PodeTransitar", form, StringComparison.Ordinal);
        Assert.Contains("deleteLastLegendPanel.Enabled = false;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_1_DesignerSemDadosFixosDeExemplo()
    {
        // Testes 1-6: a tela não pode abrir parecendo já ter OP carregada (sem valores fixos no Designer).
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.Designer.cs");

        Assert.DoesNotContain("\"119 26\"", designer, StringComparison.Ordinal);       // lote
        Assert.DoesNotContain("\"27771\"", designer, StringComparison.Ordinal);        // produto
        Assert.DoesNotContain("TWIST STIX CARNE", designer, StringComparison.Ordinal); // descrição
        Assert.DoesNotContain("readForecastBoxesTextBox.Text = \"35\";", designer, StringComparison.Ordinal);   // qtd/caixa
        Assert.DoesNotContain("readForecastPackagesTextBox.Text = \"840\";", designer, StringComparison.Ordinal); // norma
        Assert.DoesNotContain("ovenExitTextBox.Text = \"04/05/2026\";", designer, StringComparison.Ordinal);      // data em depósito
        Assert.DoesNotContain("classificationDateTextBox.Text = \"04/05/2026\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("manufacturingDateTextBox.Text = \"29/04/2026\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("expirationDateTextBox.Text = \"28/04/2029\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("balanceTextBox.Text = \"5,568\";", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_1_TituloDadosDaOpEPendenciaNaLateral()
    {
        // Testes 7/21: título "DADOS DA OP" com largura suficiente; pendência de balança no sidePanel (lateral).
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.Designer.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // largura do título aumentada (não trunca "DADOS DA OP").
        Assert.DoesNotContain("dateTitleLabel.Size = new Size(70, 16);", designer, StringComparison.Ordinal);
        Assert.Contains("dateTitleLabel.Size = new Size(160, 16);", designer, StringComparison.Ordinal);

        // pendência operacional é adicionada ao painel lateral (sidePanel), não a groupBox3/statusCard.
        string pend = ExtrairMetodo(form, "private void CriarBlocoPendenciaBalanca()");
        Assert.Contains("sidePanel.Controls.Add(_pendenciaBalancaTituloLabel);", pend, StringComparison.Ordinal);
        Assert.Contains("sidePanel.Controls.Add(_pendenciaBalancaTextoLabel);", pend, StringComparison.Ordinal);
        Assert.Contains("PENDÊNCIA OPERACIONAL", pend, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_1_BlocoPaleteSeparadoComBotao()
    {
        // Testes 10-13: card CRIAR PALETE separado do grid de caixas, botão dentro do bloco, labels completos.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private void ConfigurarPaletizacaoOperacional()");

        // Tarefa 21.6.3 (Ajuste 8): CRIAR PALETE é um card RoundedPanel; botão dentro do card à direita.
        Assert.Contains("_criarPaleteCard = new FugaPET_Dev.Tela.Controls.RoundedPanel", metodo, StringComparison.Ordinal);
        Assert.Contains("_criarPaleteCard.Controls.Add(productionActionsButton);", metodo, StringComparison.Ordinal);
        // Tarefa 21.6.5 (Ajuste 5): área inferior em TableLayoutPanel vertical (sem coordenadas fixas).
        Assert.Contains("_areaInferior = new TableLayoutPanel", metodo, StringComparison.Ordinal);
        Assert.Contains("_areaInferior.Controls.Add(productionDataGridView, 0, 0);", metodo, StringComparison.Ordinal);
        Assert.Contains("_areaInferior.Controls.Add(_criarPaleteCard, 0, 1);", metodo, StringComparison.Ordinal);
        Assert.Contains("_areaInferior.Controls.Add(paletesDataGridView, 0, 3);", metodo, StringComparison.Ordinal);
        // grid de paletes usa Dock=Fill na célula (sem Anchor Bottom próprio).
        Assert.Contains("Dock = DockStyle.Fill, // Tarefa 21.6.5 (Ajustes 1/4): nada de Anchor Bottom", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_3_CamposArredondados()
    {
        // Testes 19-23: campos de palete em caixa arredondada (RoundedPanel), padrão Setor/Cargo.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("private static FugaPET_Dev.Tela.Controls.RoundedPanel EnvolverCampoArredondado(", form, StringComparison.Ordinal);
        Assert.Contains("EnvolverCampoArredondado(primeiraCaixaTextBox", form, StringComparison.Ordinal);
        Assert.Contains("EnvolverCampoArredondado(ultimaCaixaTextBox", form, StringComparison.Ordinal);
        Assert.Contains("EnvolverCampoArredondado(materialEmbalagemPaleteTextBox", form, StringComparison.Ordinal);
        // card com mensagens de estado
        Assert.Contains("paletização saiu do Produto Acabado", form, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("paletesDataGridView.Visible = false;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_2_IniciarVerdeComOpERepaint()
    {
        // Testes 1-3: iniciar cinza sem OP, verde com OP+QTD válida, vermelho na leitura; ActionPillButton repinta.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarBotoesOperacao()");

        Assert.Contains("&& QuantidadePorCaixaValida()", metodo, StringComparison.Ordinal);
        Assert.Contains("Color.FromArgb(212, 37, 49)", metodo, StringComparison.Ordinal);   // vermelho parar
        Assert.Contains("Color.FromArgb(34, 166, 82)", metodo, StringComparison.Ordinal);   // verde iniciar
        Assert.Contains("Color.FromArgb(156, 163, 175)", metodo, StringComparison.Ordinal); // cinza
        Assert.Contains("iniciarLeituraButton.Invalidate();", metodo, StringComparison.Ordinal);
        Assert.Contains("private bool QuantidadePorCaixaValida()", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_2_LateralSemIconeECardNorma()
    {
        // Testes 4-7: lateral sem ícone sobreposto; MATERIAL CAIXA + aviso voltam ao card de norma (não lateral).
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("boxesTitleLabel.Image = null;", form, StringComparison.Ordinal);
        Assert.Contains("packagesTitleLabel.Image = null;", form, StringComparison.Ordinal);

        // Tarefa 21.6.4 (Ajuste 4): MATERIAL CAIXA vira coluna própria (col 3) no tableLayoutPanel8 (4 colunas).
        string cardExtra = ExtrairMetodo(form, "private void ConfigurarCardNormaExtra()");
        Assert.Contains("tableLayoutPanel8.ColumnCount = 4;", cardExtra, StringComparison.Ordinal);
        Assert.Contains("tableLayoutPanel8.Controls.Add(_materialCaixaValorLabel, 3, 1);", cardExtra, StringComparison.Ordinal);
        Assert.Contains("_statusNormaValorLabel = _materialCaixaValorLabel;", cardExtra, StringComparison.Ordinal);
        Assert.Contains("cardNorma.Controls.Add(_avisoNormaFallbackLabel);", cardExtra, StringComparison.Ordinal);
        Assert.DoesNotContain("sidePanel.Controls.Add(_statusNormaValorLabel);", cardExtra, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_2_GridCaixasFillENormaVaziaAlterna()
    {
        // Testes 9/10: grid de caixas preenche a largura; norma vazia alterna grid/mensagem central.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;", form, StringComparison.Ordinal);
        Assert.Contains("FillWeight = peso,", form, StringComparison.Ordinal);
        Assert.Contains("materialDataGridView.Visible = !semItens;", form, StringComparison.Ordinal);
        Assert.Contains("Norma de embalagem não retornou itens.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_3_ConsultaOpPorTabLeave()
    {
        // Testes 1-3: sair do campo OP (Tab/foco) consulta OP nova; não reconsulta a mesma.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("productionOrderTextBox.Leave += async", form, StringComparison.Ordinal);
        Assert.Contains("private async Task ConsultarOpAoSairDoCampoAsync()", form, StringComparison.Ordinal);
        string metodo = ExtrairMetodo(form, "private async Task ConsultarOpAoSairDoCampoAsync()");
        Assert.Contains("string.Equals(op, _ultimaOpConsultada, StringComparison.OrdinalIgnoreCase)", metodo, StringComparison.Ordinal);
        Assert.Contains("_ultimaOpConsultada = _ordemAtual.NumeroOrdem;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_3_CardItemOperacaoEProdutoSemDuplicidade()
    {
        // Testes 4-6: card "ITEM / OPERAÇÃO" (sem "Consulta de"); produto sem duplicar código como descrição.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.DoesNotContain("stepCaptionLabel.Text = \"Consulta de OP\";", form, StringComparison.Ordinal);
        Assert.DoesNotContain("stepDescriptionLabel.Text = \"OP selecionada\";", form, StringComparison.Ordinal);

        // Tarefa 21.6.4 (Ajuste 1): "ITEM OP" (só item) ou "ITEM / OPERAÇÃO" (com operação); sem "/ -".
        string preencher = ExtrairMetodo(form, "private void PreencherDadosOrdem()");
        Assert.Contains("stepCaptionLabel.Text = temOperacao ? \"ITEM / OPERAÇÃO\" : \"ITEM OP\";", preencher, StringComparison.Ordinal);
        Assert.Contains("stepLabel.Text = temOperacao ? $\"{item} / {_ordemAtual.Operacao.Trim()}\" : item;", preencher, StringComparison.Ordinal);
        Assert.Contains("bool descricaoUtil =", preencher, StringComparison.Ordinal);
        Assert.Contains("finishedProductTextBox.Text = descricaoUtil ? _ordemAtual.DescricaoMaterial : string.Empty;", preencher, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_3_GridsFillPesquisaAcimaEContadorUnidade()
    {
        // Testes 12-14/27-31: grids Fill; pesquisa acima do grid; contador por unidade; fallback âmbar.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // norma e paletes com Fill
        string norma = ExtrairMetodo(form, "private void ConfigurarGridNormaEmbalagem()");
        Assert.Contains("materialDataGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;", norma, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.DefaultCellStyle.Font = FonteGridCell;", form, StringComparison.Ordinal);

        // pesquisa/filtros acima do grid (não sobre o DataGridView)
        Assert.Contains("productionSearchPanel.Location = new Point(675, 6);", form, StringComparison.Ordinal);
        Assert.Contains("productionFilterButton.Location = new Point(902, 6);", form, StringComparison.Ordinal);

        // contador por unidade (peso x produtos), sem total fixo
        string resumo = ExtrairMetodo(form, "private void AtualizarResumoOperacional()");
        Assert.Contains("packagesTitleLabel.Text = \"PESO REGISTRADO\";", resumo, StringComparison.Ordinal);
        Assert.Contains("packagesTitleLabel.Text = \"PRODUTOS REGISTRADOS\";", resumo, StringComparison.Ordinal);
        Assert.DoesNotContain("de 840", form, StringComparison.Ordinal);

        // fallback âmbar (não vermelho crítico)
        Assert.Contains("Color.FromArgb(180, 83, 9)", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_4_ControllerNaoUsaCodigoComoDescricao()
    {
        // Testes 7/8: o controller não preenche DescricaoMaterial com o código do material.
        string controller = LerArquivoProjeto("Controle", "Processo", "ProdutoAcabadoController.cs");
        Assert.Contains("DescricaoMaterial = string.Empty,", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("DescricaoMaterial = PrimeiroTexto(item?.Material, ordemSap.MaterialProduzido)", controller, StringComparison.Ordinal);
        // reforço normalizado no form
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        Assert.Contains("private static bool TextosEquivalentesComoCodigo(string? a, string? b)", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_4_AlignDivisoresImplementados()
    {
        // Testes 9/10/13: métodos de alinhamento dos divisores não ficam vazios.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        string alignDate = ExtrairMetodo(form, "private void AlignDateCardLayout(object? sender, EventArgs e)");
        Assert.Contains("tableLayoutPanel6.Width / 4", alignDate, StringComparison.Ordinal);
        Assert.Contains("PosicionarDivisor(dateDividerLabel1", alignDate, StringComparison.Ordinal);

        string alignPlanned = ExtrairMetodo(form, "private void AlignPlannedProductionCardLayout(object? sender, EventArgs e)");
        Assert.Contains("tableLayoutPanel8.Width / 4", alignPlanned, StringComparison.Ordinal);
        Assert.Contains("PosicionarDivisor(_plannedDivider3", alignPlanned, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_4_FontesMaioresECampoQtdArredondado()
    {
        // Testes 17/24/25: fontes maiores nos grids; QTD. POR CAIXA em caixa arredondada.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        Assert.Contains("private static readonly Font FonteGridCell", form, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.DefaultCellStyle.Font = FonteGridCell;", form, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.DefaultCellStyle.Font = FonteGridCell;", form, StringComparison.Ordinal);
        Assert.Contains("materialDataGridView.DefaultCellStyle.Font = FonteGridCell;", form, StringComparison.Ordinal);

        // QTD. POR CAIXA arredondada (reparent na célula da tabela)
        string qtd = ExtrairMetodo(form, "private void ConfigurarCampoQtdArredondado()");
        Assert.Contains("FugaPET_Dev.Tela.Controls.RoundedPanel caixa = new()", qtd, StringComparison.Ordinal);
        Assert.Contains("caixa.Controls.Add(readForecastBoxesTextBox);", qtd, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_21_6_5_AreaInferiorEmTableLayoutSemAnchorBottom()
    {
        // Testes 1/2/9: área inferior em TableLayoutPanel; paletes sem Anchor Bottom; empilhamento coerente.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");
        string metodo = ExtrairMetodo(form, "private void ConfigurarPaletizacaoOperacional()");

        Assert.Contains("_areaInferior = new TableLayoutPanel", metodo, StringComparison.Ordinal);
        Assert.Contains("RowStyles.Add(new RowStyle(SizeType.Percent, 56F))", metodo, StringComparison.Ordinal); // caixas
        Assert.Contains("RowStyles.Add(new RowStyle(SizeType.Absolute, 92F))", metodo, StringComparison.Ordinal); // card
        Assert.Contains("RowStyles.Add(new RowStyle(SizeType.Percent, 44F))", metodo, StringComparison.Ordinal); // paletes
        Assert.Contains("paletesDataGridView", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("Anchor = AnchorStyles.Bottom", metodo, StringComparison.Ordinal);
        Assert.Contains("MinimumSize = new Size(0, 170)", metodo, StringComparison.Ordinal); // caixas min height
        Assert.Contains("MinimumSize = new Size(0, 120)", metodo, StringComparison.Ordinal); // paletes min height
    }

    [Fact]
    public void Tela_21_6_5_FontesGrandesELinhasAltas()
    {
        // Testes 10-15: fontes Segoe UI maiores; grids com linhas/headers >= 26.
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoProdutoAcabadoForm.cs");

        // Constantes de fonte em Segoe UI (sem 6.75/7/7.5/7.75 operacional).
        Assert.Contains("FonteGridCell = new(\"Segoe UI\", 9.5F, FontStyle.Regular)", form, StringComparison.Ordinal);
        Assert.Contains("FonteGridHeader = new(\"Segoe UI\", 9F, FontStyle.Bold)", form, StringComparison.Ordinal);
        Assert.DoesNotContain("new Font(\"Cascadia Code\", 6.75F", form, StringComparison.Ordinal);
        Assert.DoesNotContain("new Font(\"Cascadia Code\", 7F", form, StringComparison.Ordinal);

        // Alturas de linha/cabeçalho dos grids >= 26.
        Assert.Contains("productionDataGridView.RowTemplate.Height = 28;", form, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.ColumnHeadersHeight = 28;", form, StringComparison.Ordinal);
        Assert.Contains("paletesDataGridView.RowTemplate.Height = 28;", form, StringComparison.Ordinal);
        Assert.Contains("materialDataGridView.RowTemplate.Height = 28;", form, StringComparison.Ordinal);
        // Campo de palete maior (Ajuste 8).
        Assert.Contains("Size = new Size(largura, 28)", form, StringComparison.Ordinal);
    }

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












