using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoConsumoMaterialFormTests
{
    [Fact]
    public void Tela_DeveAbrirSemDadosSimuladosNoFluxoNormal()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        Assert.DoesNotContain("UsaDadosSimulados", form, StringComparison.Ordinal);
        Assert.DoesNotContain("AvisoDadosSimuladosHelper", form, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadMockData", form, StringComparison.Ordinal);
        Assert.DoesNotContain("58422", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("TWIST STIX", designer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LimparDadosOrdem();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void EstadoInicial_DeveManterGridsVaziasEAcoesBloqueadas()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        Assert.Contains("materialDataGridView.Rows.Clear();", form, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Rows.Clear();", form, StringComparison.Ordinal);
        Assert.Contains("startActionPanel.Enabled = false;", form, StringComparison.Ordinal);
        Assert.Contains("iniciarLeituraButton.Enabled = false;", form, StringComparison.Ordinal);
        Assert.Contains("SetReadWeightEnabled(false);", form, StringComparison.Ordinal);
        Assert.Contains(ConsumoMaterialServico.MensagemEstadoInicial, designer, StringComparison.Ordinal);
        Assert.Contains("ConsumoMaterialServico.MensagemEstadoInicial", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_Vazia_NaoChamaSapEMostraValidacao()
    {
        FakeProductionOrderSapServico sap = new();
        ConsumoMaterialServico servico = new(sap);

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("   ");

        Assert.False(resultado.Sucesso);
        Assert.Equal(ConsumoMaterialServico.MensagemOrdemObrigatoria, resultado.Mensagem);
        Assert.Equal(0, sap.Chamadas); // OP vazia nao chama o SAP
    }

    [Fact]
    public void NormalizarNumeroOrdem_RemoveControleEEspacosPreservandoZeros()
    {
        Assert.Equal("1000001", ConsumoMaterialServico.NormalizarNumeroOrdem(" 1000001 "));
        Assert.Equal("0001234", ConsumoMaterialServico.NormalizarNumeroOrdem("\t0001234\r\n"));
    }

    [Fact]
    public async Task ConsultaOrdem_Encontrada_MapeiaCabecalhoComponentesEPendente()
    {
        FakeProductionOrderSapServico sap = new()
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemSapExemplo(liberada: true))
        };
        ConsumoMaterialServico servico = new(sap);

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000001234");

        Assert.True(resultado.Sucesso);
        Assert.Equal(CenarioConsultaOrdemConsumo.Carregada, resultado.Cenario);
        Assert.Equal(1, sap.Chamadas);

        OrdemProducaoConsumo ordem = resultado.Ordem!;
        Assert.Equal("1000001234", ordem.NumeroOrdem);
        Assert.Equal("MAT-12345", ordem.MaterialProduzido);
        Assert.Equal("1000", ordem.Planta);
        Assert.Equal(100m, ordem.QuantidadePrevista);
        Assert.Equal("PC", ordem.Unidade);
        Assert.Equal(2, ordem.Componentes.Count);

        ComponenteConsumoMaterial parcial = ordem.Componentes[1];
        Assert.Equal("0000123456", parcial.NumeroReserva);
        Assert.Equal("0002", parcial.ItemReserva);
        Assert.Equal("KG", parcial.UnidadeMedida);
        Assert.Equal(25m, parcial.QuantidadePrevista);
        Assert.Equal(10m, parcial.QuantidadeConsumida);
        Assert.Equal(15m, parcial.QuantidadePendente); // 25 - 10
        Assert.Equal(ComponenteConsumoMaterial.StatusPendente, parcial.Status);
        Assert.True(parcial.PesagemLiberada);
        Assert.Equal("261", parcial.TipoMovimento); // GoodsMovementType vazio -> 261
    }

    [Fact]
    public async Task ConsultaOrdem_LoteProdutoNaoDeveSerCopiadoParaComponente()
    {
        OrdemProducaoSap sapOrdem = OrdemSapExemplo(liberada: true) with
        {
            Itens = [new ItemOrdemProducaoSap { ItemOrdem = "0001", Material = "MAT-12345", Lote = "61058562W" }],
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    Reserva = "11237",
                    ItemReserva = "1",
                    Material = "1000037",
                    Centro = "3007",
                    Deposito = "PP01",
                    QuantidadeNecessaria = 49.928m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "KG",
                    TipoMovimento = "261",
                    Lote = string.Empty
                }
            ]
        };
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(sapOrdem)
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000909");

        Assert.Equal("61058562W", resultado.Ordem!.LoteProdutoProduzido);
        Assert.Equal("61058562W", resultado.Ordem.Lote);
        Assert.Equal(string.Empty, resultado.Ordem.Componentes[0].Lote);
        Assert.Contains("lote do produto produzido", resultado.Mensagem, StringComparison.Ordinal);
        Assert.Contains("lote dos componentes não foi retornado", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_BatchDoComponenteDeveSerLoteDoComponente()
    {
        OrdemProducaoSap sapOrdem = OrdemSapExemplo(liberada: true) with
        {
            Itens = [new ItemOrdemProducaoSap { ItemOrdem = "0001", Material = "MAT-12345", Lote = "LOTE-PROD" }],
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    Reserva = "11237",
                    ItemReserva = "2",
                    Material = "1000055",
                    Centro = "3007",
                    Deposito = "PP01",
                    QuantidadeNecessaria = 0.046m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "KG",
                    TipoMovimento = "261",
                    Lote = "LOTE-MP"
                }
            ]
        };
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(sapOrdem)
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000909");

        Assert.Equal("LOTE-PROD", resultado.Ordem!.LoteProdutoProduzido);
        Assert.Equal("LOTE-MP", resultado.Ordem.Componentes[0].Lote);
        Assert.DoesNotContain("lote dos componentes não foi retornado", resultado.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_ComponenteTotalmenteConsumido_NaoLiberaPesagem()
    {
        // Componente com retirada >= necessaria => consumido, pendente 0.
        OrdemProducaoSap sapOrdem = OrdemSapExemplo(liberada: true) with
        {
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    Material = "COMP-9012",
                    QuantidadeNecessaria = 25m,
                    QuantidadeRetirada = 25m,
                    UnidadeBase = "KG"
                }
            ]
        };
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(sapOrdem)
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000001234");

        ComponenteConsumoMaterial consumido = resultado.Ordem!.Componentes[0];
        Assert.Equal(0m, consumido.QuantidadePendente);
        Assert.Equal(ComponenteConsumoMaterial.StatusConsumido, consumido.Status);
        Assert.False(consumido.PesagemLiberada);
    }

    [Fact]
    public async Task ConsultaOrdem_NaoEncontrada_LimpaComCenarioNaoEncontrada()
    {
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.NaoEncontrada()
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("9999999999");

        Assert.False(resultado.Sucesso);
        Assert.Equal(CenarioConsultaOrdemConsumo.NaoEncontrada, resultado.Cenario);
        Assert.Equal(ConsumoMaterialServico.MensagemNaoEncontrada, resultado.Mensagem);
    }

    [Fact]
    public async Task ConsultaOrdem_SapIndisponivel_DeveSurfacarDiagnosticoSanitizado()
    {
        const string diagnostico = "Consulta OP falhou na etapa GET_FALLBACK, HTTP 400. Verifique a OP ou o serviço SAP.";
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Indisponivel(diagnostico, 400)
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000001234");

        Assert.False(resultado.Sucesso);
        Assert.Equal(CenarioConsultaOrdemConsumo.Indisponivel, resultado.Cenario);
        // Ajuste 3: a mensagem sanitizada do SAP (etapa/status) chega ate a tela.
        Assert.Equal(diagnostico, resultado.Mensagem);
    }

    [Fact]
    public async Task ConsultaOrdem_SemComponentes_DeveBloquearComMensagemEspecifica()
    {
        OrdemProducaoSap semComponentes = OrdemSapExemplo(liberada: true) with { Componentes = [] };
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(semComponentes)
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000009");

        Assert.False(resultado.Sucesso); // nao libera leitura
        Assert.Equal(CenarioConsultaOrdemConsumo.SemComponentes, resultado.Cenario);
        Assert.Equal(ConsumoMaterialServico.MensagemSemComponentes, resultado.Mensagem);
        Assert.NotNull(resultado.Ordem);                 // cabecalho disponivel
        Assert.Empty(resultado.Ordem!.Componentes);      // grid vazio
    }

    [Fact]
    public async Task ConsultaOrdem_NaoLiberada_ExibeDadosEBloqueiaPesagem()
    {
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemSapExemplo(liberada: false))
        });

        ResultadoConsultaOrdemConsumo resultado = await servico.ConsultarOrdemAsync("1000001234");

        Assert.True(resultado.Sucesso);
        Assert.Equal(CenarioConsultaOrdemConsumo.NaoLiberada, resultado.Cenario);
        Assert.Equal(ConsumoMaterialServico.MensagemNaoLiberada, resultado.Mensagem);
        Assert.All(resultado.Ordem!.Componentes, c => Assert.False(c.PesagemLiberada));
    }

    [Fact]
    public async Task Controller_DelegaConsultaAoServico()
    {
        ConsumoMaterialServico servico = new(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemSapExemplo(liberada: true))
        });
        ProcessoConsumoMaterialController controller = new(servico);

        ResultadoConsultaOrdemConsumo resultado = await controller.ConsultarOrdemProducaoAsync("1000001234");

        Assert.True(resultado.Sucesso);
        Assert.Equal("1000001234", resultado.NumeroOrdem);
    }

    // ---------- Pesagem local de consumo (Tarefa 4) ----------

    private static ConsumoMaterialServico ServicoPesagem() => new(new FakeProductionOrderSapServico());

    private static ComponenteConsumoMaterial Componente(
        string unidade = "KG", decimal pendente = 50m, string reserva = "0000123456", string itemReserva = "0001")
        => new()
        {
            CodigoMaterial = "COMP-5678",
            DescricaoMaterial = "Insumo",
            UnidadeMedida = unidade,
            QuantidadePendente = pendente,
            NumeroReserva = reserva,
            ItemReserva = itemReserva,
            Lote = "L1",
            DepositoConsumo = "0001",
            PesagemLiberada = true
        };

    [Fact]
    public void PesagemLocal_ComponenteKgValido_RegistraComLiquidoCorreto()
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(), "1000009", pesoBrutoKg: 10m, pesoTaraKg: 2m,
            PesagemConsumoMaterial.OrigemManual, totalJaPesadoLocalKg: 0m, sequencia: 1);

        Assert.True(r.Sucesso);
        Assert.Equal(8m, r.Pesagem!.PesoLiquidoKg);         // 10 - 2
        Assert.Equal("MANUAL", r.Pesagem.Origem);
        Assert.Equal("REGISTRADA_LOCALMENTE", r.Pesagem.StatusLocal);
        Assert.Equal("0000123456", r.Pesagem.NumeroReserva);
    }

    [Fact]
    public void PesagemLocal_UnidadeDiferenteKg_Bloqueia()
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(unidade: "L"), "1000009", 10m, 0m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        // Tarefa 14.1: a regra central (Camada A) bloqueia unidade != KG antes da validacao de peso.
        Assert.Equal(CenarioPesagemConsumo.ComponenteNaoLiberado, r.Cenario);
        Assert.Contains("unidade L não suportada", r.Mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void PesagemLocal_BrutoZeroOuNegativo_Bloqueia(int bruto)
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(), "1000009", bruto, 0m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        Assert.Equal(CenarioPesagemConsumo.PesoBrutoInvalido, r.Cenario);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(5, 6)]
    public void PesagemLocal_TaraMaiorOuIgualBruto_Bloqueia(int bruto, int tara)
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(), "1000009", bruto, tara, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        Assert.Equal(CenarioPesagemConsumo.PesoLiquidoInvalido, r.Cenario);
    }

    [Fact]
    public void PesagemLocal_ComponenteSelecionadoCom6911Kg_AceitaUmKg()
    {
        ComponenteConsumoMaterial componente = Componente(
            pendente: 69.11m,
            reserva: "11237",
            itemReserva: "6");
        componente.CodigoMaterial = "3500024";

        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            componente, "1000909", 1m, 0m, PesagemConsumoMaterial.OrigemManual, 0m, 1);

        Assert.True(r.Sucesso);
        Assert.Equal(1m, r.Pesagem!.PesoLiquidoKg);
        Assert.Equal("3500024", r.Pesagem.CodigoMaterial);
        Assert.Equal("11237", r.Pesagem.NumeroReserva);
        Assert.Equal("6", r.Pesagem.ItemReserva);
    }

    [Fact]
    public void PesagemLocal_ComponenteSelecionadoCom0046Kg_BloqueiaUmKg()
    {
        ComponenteConsumoMaterial componente = Componente(
            pendente: 0.046m,
            reserva: "11237",
            itemReserva: "4");
        componente.CodigoMaterial = "1000055";

        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            componente, "1000909", 1m, 0m, PesagemConsumoMaterial.OrigemManual, 0m, 1);

        Assert.Equal(CenarioPesagemConsumo.ExcedePendente, r.Cenario);
        Assert.Equal(ConsumoMaterialServico.MensagemExcedePendente, r.Mensagem);
    }

    [Fact]
    public void PesagemLocal_ExcedeQuantidadePendente_BloqueiaENaoRegistra()
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(pendente: 50m), "1000009", pesoBrutoKg: 10m, pesoTaraKg: 0m,
            PesagemConsumoMaterial.OrigemBalanca, totalJaPesadoLocalKg: 45m, sequencia: 2);

        Assert.Equal(CenarioPesagemConsumo.ExcedePendente, r.Cenario); // 45 + 10 = 55 > 50
        Assert.Null(r.Pesagem);
        Assert.Equal(ConsumoMaterialServico.MensagemExcedePendente, r.Mensagem);
    }

    [Fact]
    public void PesagemLocal_DentroDoPendente_AcumulaAteOLimite()
    {
        ConsumoMaterialServico servico = ServicoPesagem();
        ComponenteConsumoMaterial componente = Componente(pendente: 20m);

        ResultadoPesagemConsumo primeira = servico.RegistrarPesagemLocal(
            componente, "1000009", 12m, 0m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);
        ResultadoPesagemConsumo segunda = servico.RegistrarPesagemLocal(
            componente, "1000009", 8m, 0m, PesagemConsumoMaterial.OrigemBalanca, primeira.Pesagem!.PesoLiquidoKg, 2);
        ResultadoPesagemConsumo terceira = servico.RegistrarPesagemLocal(
            componente, "1000009", 1m, 0m, PesagemConsumoMaterial.OrigemBalanca, 20m, 3);

        Assert.True(primeira.Sucesso);
        Assert.True(segunda.Sucesso);                 // 12 + 8 = 20 == pendente: ok
        Assert.Equal(CenarioPesagemConsumo.ExcedePendente, terceira.Cenario); // ja no limite
    }

    [Fact]
    public void ChaveComponente_DeveDiferenciarPorReservaItemNaoSoMaterial()
    {
        string chaveA = ConsumoMaterialServico.ChaveComponente(Componente(reserva: "R1", itemReserva: "0001"));
        string chaveB = ConsumoMaterialServico.ChaveComponente(Componente(reserva: "R1", itemReserva: "0002"));
        string chaveAigual = ConsumoMaterialServico.ChaveComponente(Componente(reserva: "R1", itemReserva: "0001"));

        Assert.NotEqual(chaveA, chaveB);   // mesmo material, item de reserva diferente -> chave diferente
        Assert.Equal(chaveA, chaveAigual);
    }

    [Fact]
    public void Tela_PesagemConsumoNaoImprimeEtiqueta()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        int inicio = form.IndexOf("private async Task RegistrarPesagemConsumoAsync", StringComparison.Ordinal);
        int fim = form.IndexOf("private decimal SomarPesagensLocais", StringComparison.Ordinal);
        string metodo = form[inicio..fim];

        Assert.DoesNotContain("ImprimirEtiqueta", metodo, StringComparison.Ordinal);
        Assert.Contains("_pesagensPorComponente", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PesagemManualNaoDeveAplicarTaraInvisivel()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task RegistrarPesagemConsumoAsync");

        // Tarefa 15: a tara vem da SELECAO POR COMPONENTE (dicionario), nao da tara invisivel do terminal.
        Assert.Contains("ObterTaraConsumoAplicada(componente)", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("ObterTaraConsumoAplicadaAsync()", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmarTaraAplicada(", metodo, StringComparison.Ordinal);
        Assert.Contains("pesoBruto - tara", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveExibirBrutoTaraLiquidoAntesEDepoisDaPesagem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("Peso bruto:", form, StringComparison.Ordinal);
        Assert.Contains("Tara aplicada:", form, StringComparison.Ordinal);
        Assert.Contains("Peso líquido:", form, StringComparison.Ordinal);
        Assert.Contains("Confirmar pesagem com esta tara?", form, StringComparison.Ordinal);
        Assert.Contains("Bruto: {FormatarKg(pesagem.PesoBrutoKg)}", form, StringComparison.Ordinal);
        Assert.Contains("Tara: {FormatarKg(pesagem.PesoTaraKg)}", form, StringComparison.Ordinal);
        Assert.Contains("Líquido: {FormatarKg(pesagem.PesoLiquidoKg)}", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveRegistrarDiagnosticoSanitizadoDaTaraAplicada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private static void RegistrarDiagnosticoPesagemConsumo");

        Assert.Contains("Pesagem consumo: bruto=", metodo, StringComparison.Ordinal);
        Assert.Contains("origemTara=", metodo, StringComparison.Ordinal);
        Assert.Contains("tara selecionada:", form, StringComparison.Ordinal); // Tarefa 15: tara por componente
        Assert.DoesNotContain("senha", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PesagemLocal_SemTara_UsaLiquidoIgualAoBruto()
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(), "1000009", pesoBrutoKg: 2m, pesoTaraKg: 0m,
            PesagemConsumoMaterial.OrigemManual, totalJaPesadoLocalKg: 0m, sequencia: 1);

        Assert.True(r.Sucesso);
        Assert.Equal(2m, r.Pesagem!.PesoBrutoKg);
        Assert.Equal(0m, r.Pesagem.PesoTaraKg);
        Assert.Equal(2m, r.Pesagem.PesoLiquidoKg);
    }

    [Fact]
    public void PesagemLocal_ComTara_GravaBrutoTaraELiquido()
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(), "1000009", pesoBrutoKg: 2m, pesoTaraKg: 0.5m,
            PesagemConsumoMaterial.OrigemManual, totalJaPesadoLocalKg: 0m, sequencia: 1);

        Assert.True(r.Sucesso);
        Assert.Equal(2m, r.Pesagem!.PesoBrutoKg);
        Assert.Equal(0.5m, r.Pesagem.PesoTaraKg);
        Assert.Equal(1.5m, r.Pesagem.PesoLiquidoKg);
    }


    [Fact]
    public void Tela_NaoDeveConterTextosResiduaisDeProducaoOuEtiqueta()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.DoesNotContain("ultima etiqueta", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leitura de producao", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Etiqueta: {FormatarId", form, StringComparison.Ordinal);

        Assert.Contains("Deseja realmente excluir a última pesagem de consumo?", form, StringComparison.Ordinal);
        Assert.Contains("na leitura de consumo.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_NaoDeveAbrirTesteZebraNemAtalhoZebra()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Nenhum acesso a Zebra/TesteZebraForm.
        Assert.DoesNotContain("TesteZebraForm", form, StringComparison.Ordinal);

        // F9 deve acionar peso MANUAL (Designer indica F9 em leituraManualButton), nunca Zebra.
        int inicio = form.IndexOf("if (keyData == Keys.F9)", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Bloco F9 não encontrado no ProcessCmdKey.");
        int fim = form.IndexOf("if (keyData == Keys.F12)", inicio, StringComparison.Ordinal);
        string blocoF9 = form[inicio..fim];

        Assert.Contains("LeituraManual_Click", blocoF9, StringComparison.Ordinal);
        Assert.DoesNotContain("TesteZebra", blocoF9, StringComparison.Ordinal);
        Assert.DoesNotContain("ShowDialog", blocoF9, StringComparison.Ordinal);

        // F12 continua lendo a balança.
        Assert.Contains("ReadWeightLegend_Click(readWeightLegendTextLabel, EventArgs.Empty)", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_ConsumoNaoImprimeNemTocaZebra()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.DoesNotContain("ImprimirEtiqueta", form, StringComparison.Ordinal);
        Assert.DoesNotContain("DadosEtiquetaProducao", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ConstruirDadosEtiquetaProducao", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ImpressoraEtiquetaServico", form, StringComparison.Ordinal);
        Assert.DoesNotContain("CellDoubleClick", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_WarmUpDeveAquecerSomenteBalanca()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        int inicio = form.IndexOf("private async Task WarmUpProductionDevicesAsync", StringComparison.Ordinal);
        int fim = form.IndexOf("private", inicio + 20, StringComparison.Ordinal);
        string metodo = form[inicio..fim];

        Assert.Contains("WarmUpSaldoSafelyAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("Impressora", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("AquecerImpressoraComSegurancaAsync", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PesagemDefineRowTagEMensagensDeConsumo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("lista.Add(resultado.Pesagem);", form, StringComparison.Ordinal);
        Assert.Contains("productionWeightColumn", form, StringComparison.Ordinal);
        Assert.DoesNotContain(".Tag = resultado.Pesagem;", form, StringComparison.Ordinal);
        Assert.Contains("PesagemConsumoMaterial? pesagem", form, StringComparison.Ordinal);
        Assert.Contains("Última pesagem de consumo excluída.", form, StringComparison.Ordinal);
        Assert.Contains("Pesagem não encontrada.", form, StringComparison.Ordinal);
        Assert.Contains("Leitura de consumo parada.", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Ultima etiqueta", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Producao parada.", form, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(50, 50, "CONSUMIDO", false)]   // total atingiu o pendente
    [InlineData(50, 30, "PENDENTE", true)]     // abaixo do pendente: reabre
    [InlineData(0, 0, "CONSUMIDO", false)]     // ja consumido no SAP (pendente 0)
    public void AtualizarStatusComponente_ReabreOuBloqueiaPorTotalLocal(
        int pendente, int total, string statusEsperado, bool liberadaEsperada)
    {
        ComponenteConsumoMaterial componente = Componente(pendente: pendente);
        ConsumoMaterialServico.AtualizarStatusComponentePorTotalLocal(componente, total);

        Assert.Equal(statusEsperado, componente.Status);
        Assert.Equal(liberadaEsperada, componente.PesagemLiberada);
    }

    [Fact]
    public void ChavePesagem_DeveCoincidirComChaveDoComponente()
    {
        ComponenteConsumoMaterial componente = Componente();
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            componente, "1000009", 10m, 0m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        Assert.Equal(
            ConsumoMaterialServico.ChaveComponente(componente),
            ConsumoMaterialServico.ChavePesagem(r.Pesagem!));
    }

    // ---------- Persistencia local do consumo (Tarefa 5) ----------

    private static PesagemConsumoMaterial PesagemDe(ComponenteConsumoMaterial c, int seq, decimal liquido, string origem = "MANUAL")
        => new()
        {
            Sequencia = seq,
            CodigoMaterial = c.CodigoMaterial,
            NumeroReserva = c.NumeroReserva,
            ItemReserva = c.ItemReserva,
            Lote = c.Lote,
            DepositoConsumo = c.DepositoConsumo,
            PesoBrutoKg = liquido,
            PesoTaraKg = 0m,
            PesoLiquidoKg = liquido,
            UnidadeMedida = "KG",
            Origem = origem,
            PesadoEm = DateTime.Now
        };

    private static Dictionary<string, List<PesagemConsumoMaterial>> Pesagens(params (ComponenteConsumoMaterial comp, PesagemConsumoMaterial[] pes)[] grupos)
    {
        Dictionary<string, List<PesagemConsumoMaterial>> dict = [];
        foreach ((ComponenteConsumoMaterial comp, PesagemConsumoMaterial[] pes) in grupos)
        {
            dict[ConsumoMaterialServico.ChaveComponente(comp)] = [.. pes];
        }

        return dict;
    }

    private static OrdemProducaoConsumo OrdemConsumo(params ComponenteConsumoMaterial[] componentes)
        => new()
        {
            NumeroOrdem = "1000009",
            Planta = "1000",
            MaterialProduzido = "MAT-12345",
            Unidade = "PC",
            QuantidadePrevista = 100m,
            Lote = "LP1",
            Componentes = componentes
        };

    private static ConsumoMaterialServico ServicoPersistencia(FakeConsumoMaterialRepositorio repo)
        => new(new FakeProductionOrderSapServico(), () => repo);

    [Fact]
    public async Task SalvarConsumoLocal_SemPesagem_RetornaValidacaoENaoChamaRepositorio()
    {
        ComponenteConsumoMaterial comp = Componente();
        FakeConsumoMaterialRepositorio repo = new();

        ResultadoPersistenciaConsumoMaterial r = await ServicoPersistencia(repo).SalvarConsumoLocalAsync(
            OrdemConsumo(comp), [comp], new Dictionary<string, List<PesagemConsumoMaterial>>(), "operador", default);

        Assert.Equal(CenarioPersistenciaConsumo.SemPesagem, r.Cenario);
        Assert.Equal(0, repo.Chamadas);
    }

    [Fact]
    public async Task SalvarConsumoLocal_ComPesagemValida_CriaLancamentoPendenteSap()
    {
        ComponenteConsumoMaterial comp = Componente(pendente: 50m);
        FakeConsumoMaterialRepositorio repo = new();

        ResultadoPersistenciaConsumoMaterial r = await ServicoPersistencia(repo).SalvarConsumoLocalAsync(
            OrdemConsumo(comp), [comp], Pesagens((comp, [PesagemDe(comp, 1, 8m)])), "operador", default);

        Assert.True(r.Sucesso);
        Assert.Equal(7777, r.CodigoLancamento);
        Assert.Contains("Lançamento 7777 pendente de envio ao SAP", r.Mensagem, StringComparison.Ordinal);

        ConsumoMaterialLancamento lanc = repo.UltimoLancamento!;
        Assert.Equal("PENDENTE_SAP", lanc.StatusLancamento);
        Assert.Equal("operador", lanc.UsuarioCriacao);
        Assert.Single(lanc.Itens);
        Assert.Equal("PENDENTE_SAP", lanc.Itens[0].StatusItem);
        Assert.Equal(8m, lanc.Itens[0].QuantidadeConsumidaLocal);
        Assert.Single(lanc.Itens[0].Pesagens);
        Assert.Equal("REGISTRADA_LOCALMENTE", lanc.Itens[0].Pesagens[0].StatusPesagem);
    }

    [Fact]
    public async Task SalvarConsumoLocal_NaoCriaItemParaComponenteSemPesagem()
    {
        ComponenteConsumoMaterial comPesagem = Componente(reserva: "R1", itemReserva: "0001");
        ComponenteConsumoMaterial semPesagem = Componente(reserva: "R2", itemReserva: "0002");
        FakeConsumoMaterialRepositorio repo = new();

        ResultadoPersistenciaConsumoMaterial r = await ServicoPersistencia(repo).SalvarConsumoLocalAsync(
            OrdemConsumo(comPesagem, semPesagem),
            [comPesagem, semPesagem],
            Pesagens((comPesagem, [PesagemDe(comPesagem, 1, 5m)])),
            "operador", default);

        Assert.True(r.Sucesso);
        Assert.Single(repo.UltimoLancamento!.Itens); // somente o componente com pesagem
    }

    [Fact]
    public async Task SalvarConsumoLocal_SomaQuantidadeConsumidaLocal()
    {
        ComponenteConsumoMaterial comp = Componente(pendente: 50m);
        FakeConsumoMaterialRepositorio repo = new();

        await ServicoPersistencia(repo).SalvarConsumoLocalAsync(
            OrdemConsumo(comp), [comp],
            Pesagens((comp, [PesagemDe(comp, 1, 10m), PesagemDe(comp, 2, 5m)])),
            "operador", default);

        Assert.Equal(15m, repo.UltimoLancamento!.Itens[0].QuantidadeConsumidaLocal);
        Assert.Equal(2, repo.UltimoLancamento.Itens[0].Pesagens.Count);
    }

    [Fact]
    public async Task SalvarConsumoLocal_ErroNoRepositorio_RetornaErroComRollback()
    {
        ComponenteConsumoMaterial comp = Componente(pendente: 50m);
        FakeConsumoMaterialRepositorio repo = new() { LancarErro = true };

        ResultadoPersistenciaConsumoMaterial r = await ServicoPersistencia(repo).SalvarConsumoLocalAsync(
            OrdemConsumo(comp), [comp], Pesagens((comp, [PesagemDe(comp, 1, 8m)])), "operador", default);

        Assert.Equal(CenarioPersistenciaConsumo.Erro, r.Cenario);
        Assert.False(r.Sucesso);
    }

    [Fact]
    public async Task SalvarConsumoLocal_NaoChamaSap()
    {
        ComponenteConsumoMaterial comp = Componente(pendente: 50m);
        FakeProductionOrderSapServico sap = new();
        FakeConsumoMaterialRepositorio repo = new();
        ConsumoMaterialServico servico = new(sap, () => repo);

        await servico.SalvarConsumoLocalAsync(
            OrdemConsumo(comp), [comp], Pesagens((comp, [PesagemDe(comp, 1, 8m)])), "operador", default);

        Assert.Equal(0, sap.Chamadas); // persistencia local nao consulta nem envia SAP
    }

    [Fact]
    public void Tela_DeveSalvarConsumoLocalComIdempotenciaSemSap()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("private async Task ConfirmarConsumoAsync()", form, StringComparison.Ordinal);
        Assert.Contains("_controller.SalvarConsumoLocalAsync(", form, StringComparison.Ordinal);
        Assert.Contains("_salvandoConsumo", form, StringComparison.Ordinal);
        Assert.Contains("_consumoSalvoNaSessao", form, StringComparison.Ordinal);
        Assert.Contains("Deseja salvar localmente este consumo como pendente de envio ao SAP?", form, StringComparison.Ordinal);
        // salvar local nao envia SAP nem cria documento/movimento
        int inicio = form.IndexOf("private async Task ConfirmarConsumoAsync()", StringComparison.Ordinal);
        int fim = form.IndexOf("private static string GetFriendlyErrorMessage", StringComparison.Ordinal);
        string metodo = form[inicio..fim];
        Assert.DoesNotContain("MaterialDocument", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("261", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql030_DeveCriarTresTabelasNoSchemaHomologacao()
    {
        string proposta = File.ReadAllText(Path.Combine(
            RaizProjeto(), "BancoDados", "001_incrementais",
            "030_consumo_material_persistencia_local_GAIA",
            "030_consumo_material_persistencia_local_PROPOSTA_GAIA.sql"));

        Assert.Contains("SET search_path TO homologacao;", proposta, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS homologacao.consumo_material_lancamento", proposta, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS homologacao.consumo_material_item", proposta, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS homologacao.consumo_material_pesagem", proposta, StringComparison.Ordinal);
        Assert.Contains("status_lancamento", proposta, StringComparison.Ordinal);
        Assert.Contains("'PENDENTE_SAP'", proposta, StringComparison.Ordinal);
        Assert.Contains("'ENVIANDO_SAP'", proposta, StringComparison.Ordinal); // claim atomico
        Assert.Contains("REFERENCES homologacao.consumo_material_lancamento", proposta, StringComparison.Ordinal);
        Assert.Contains("REFERENCES homologacao.consumo_material_item", proposta, StringComparison.Ordinal);

        string validacao = File.ReadAllText(Path.Combine(
            RaizProjeto(), "BancoDados", "001_incrementais",
            "030_consumo_material_persistencia_local_GAIA",
            "030_consumo_material_persistencia_local_VALIDACAO_GAIA.sql"));
        Assert.Contains("VALIDACAO 030", validacao, StringComparison.Ordinal);
        Assert.Contains("ENVIANDO_SAP", validacao, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_ClaimEConfirmacao_DevemUsarUpdateCondicionalDeStatus()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ConsumoMaterialRepositorio.cs");

        // Reserva: PENDENTE_SAP -> ENVIANDO_SAP condicional, com documento NULL (claim atomico).
        Assert.Contains("SET status_lancamento = 'ENVIANDO_SAP'", repo, StringComparison.Ordinal);
        Assert.Contains("AND status_lancamento = 'PENDENTE_SAP'", repo, StringComparison.Ordinal);
        Assert.Contains("linhas != 1", repo, StringComparison.Ordinal);

        // Confirmacao exige ENVIANDO_SAP e documento NULL (evita confirmar sem claim valido).
        Assert.Contains("AND status_lancamento = 'ENVIANDO_SAP'", repo, StringComparison.Ordinal);

        // Falha: ENVIANDO_SAP -> FALHA_SAP (nao volta automatico para PENDENTE_SAP).
        Assert.Contains("SET status_lancamento = 'FALHA_SAP'", repo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PreviewSap261_DeveSerSomenteVisualizacaoSemEnvio()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("VisualizarPayloadSap261Async", form, StringComparison.Ordinal);
        Assert.Contains("_controller.GerarPreviewSap261Async(", form, StringComparison.Ordinal);
        Assert.Contains("Preview SAP 261", form, StringComparison.Ordinal);
        Assert.Contains("Salve o consumo local antes de gerar o preview SAP 261.", form, StringComparison.Ordinal);

        // Ajuste 7 (nomes explicitos): "EnviarConsumoSap261/EnviarSap261Async" sao validos (Tarefa 7);
        // proibidos os nomes vagos/duplicados de montagem de documento.
        Assert.DoesNotContain("CriarDocumentoMaterial", form, StringComparison.Ordinal);
        Assert.DoesNotContain("PostarConsumo", form, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmarSap", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_EnviarSap261_NaoEnviaAutomaticamenteEBloqueiaCliqueDuplo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("private async Task EnviarSap261Async()", form, StringComparison.Ordinal);
        Assert.Contains("_controller.EnviarConsumoSap261Async(", form, StringComparison.Ordinal);
        Assert.Contains("_enviandoSap", form, StringComparison.Ordinal);
        Assert.Contains("Enviar SAP 261", form, StringComparison.Ordinal);
        Assert.Contains("Deseja enviar este consumo ao SAP agora?", form, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpMethod.Patch", form, StringComparison.Ordinal);

        // NAO envia automaticamente apos salvar: ConfirmarConsumoAsync nao chama o envio SAP.
        int inicio = form.IndexOf("private async Task ConfirmarConsumoAsync()", StringComparison.Ordinal);
        int fim = form.IndexOf("private void CriarBotaoPreviewSap261", StringComparison.Ordinal);
        if (fim < 0 || fim < inicio)
        {
            fim = form.IndexOf("private static string GetFriendlyErrorMessage", StringComparison.Ordinal);
        }

        string confirmar = form[inicio..fim];
        Assert.DoesNotContain("EnviarConsumoSap261Async", confirmar, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrarPesagemLocal_DeveCriarPesadoEmUtc()
    {
        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            Componente(), "1000009", 10m, 2m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        Assert.True(r.Sucesso);
        Assert.Equal(DateTimeKind.Utc, r.Pesagem!.PesadoEm.Kind);
    }

    [Fact]
    public void RepositorioNormalizarUtc_DeveConverterLocalEUnspecifiedSemQuebrar()
    {
        // Local -> convertido para UTC (instante preservado).
        DateTime local = new(2026, 6, 27, 15, 0, 0, DateTimeKind.Local);
        DateTime deLocal = ConsumoMaterialRepositorio.NormalizarUtc(local);
        Assert.Equal(DateTimeKind.Utc, deLocal.Kind);
        Assert.Equal(local.ToUniversalTime(), deLocal);

        // Unspecified -> tratado como UTC, SEM deslocar o relogio.
        DateTime indef = new(2026, 6, 27, 15, 0, 0, DateTimeKind.Unspecified);
        DateTime deIndef = ConsumoMaterialRepositorio.NormalizarUtc(indef);
        Assert.Equal(DateTimeKind.Utc, deIndef.Kind);
        Assert.Equal(15, deIndef.Hour);

        // Utc -> inalterado.
        DateTime utc = new(2026, 6, 27, 15, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc, ConsumoMaterialRepositorio.NormalizarUtc(utc));
    }

    [Fact]
    public void Repositorio_InserirPesagem_DeveNormalizarPesadoEmParaUtc()
    {
        string repo = LerArquivoProjeto("AcessoDados", "Repositorio", "ConsumoMaterialRepositorio.cs");

        // O parametro @pesado_em (timestamptz) usa NormalizarUtc, nunca o valor Local direto.
        Assert.Contains("NormalizarUtc(pesagem.PesadoEm)", repo, StringComparison.Ordinal);
        Assert.DoesNotContain("{ Value = pesagem.PesadoEm }", repo, StringComparison.Ordinal);
    }

    private sealed class FakeConsumoMaterialRepositorio : IConsumoMaterialRepositorio
    {
        public int Chamadas { get; private set; }
        public bool LancarErro { get; init; }
        public long CodigoRetorno { get; init; } = 7777;
        public ConsumoMaterialLancamento? UltimoLancamento { get; private set; }

        // Para os testes de envio: lancamento devolvido por ObterPorCodigoAsync e contagem de confirmacoes.
        public ConsumoMaterialLancamento? LancamentoParaObter { get; set; }
        public int Confirmacoes { get; private set; }
        public string? DocumentoConfirmado { get; private set; }

        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            if (LancarErro)
            {
                throw new InvalidOperationException("Falha simulada no insert (rollback esperado).");
            }

            UltimoLancamento = lancamento;
            return Task.FromResult(CodigoRetorno);
        }

        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(LancamentoParaObter ?? UltimoLancamento);

        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
            ConsultaConsumoMaterialFiltro filtro,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ResumoConsumoMaterialLancamento>>([]);

        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(
            long codigoLancamento,
            CancellationToken cancellationToken = default)
            => Task.FromResult(LancamentoParaObter ?? UltimoLancamento);

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, DateTime reservadoEmUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarcarConsumoConfirmadoSapAsync(
            long codigoLancamento, string? documentoMaterialSap, string? exercicioDocumentoMaterialSap,
            DateTime enviadoSapEmUtc, CancellationToken cancellationToken = default)
        {
            Confirmacoes++;
            DocumentoConfirmado = documentoMaterialSap;
            return Task.CompletedTask;
        }
    }

    // ---------- Carga/filtro/diagnostico de componentes (Tarefa 7.2) ----------

    private static ComponenteOrdemProducaoSap CompSap(
        string material = "QM002", string unidade = "KG", decimal necessaria = 10m, decimal retirada = 0m,
        string deposito = "PP01", string operacao = "")
        => new()
        {
            Material = material, UnidadeBase = unidade, QuantidadeNecessaria = necessaria, QuantidadeRetirada = retirada,
            Deposito = deposito, Centro = "3007", Reserva = "6676", ItemReserva = "2", Operacao = operacao
        };

    [Fact]
    public async Task ConsultaOrdem_MapeiaOperacaoDoComponente()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(
            OrdemSapCustom(liberada: true, CompSap(operacao: "0060")));

        Assert.NotNull(r.Ordem);
        Assert.Equal("0060", r.Ordem!.Componentes[0].Operacao);
    }

    private static OrdemProducaoSap OrdemSapCustom(bool liberada, params ComponenteOrdemProducaoSap[] comps)
        => new() { NumeroOrdem = "1000001", MaterialProduzido = "FG126", Centro = "3007", Liberada = liberada, Componentes = comps };

    private static async Task<ResultadoConsultaOrdemConsumo> ConsultarComSap(OrdemProducaoSap sap)
        => await new ConsumoMaterialServico(new FakeProductionOrderSapServico
        {
            Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(sap)
        }).ConsultarOrdemAsync("1000001");

    [Fact]
    public async Task ConsultaOrdem_ComponenteKgPendenteComDeposito_CarregaEElegivel()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(OrdemSapCustom(liberada: true, CompSap()));

        Assert.Equal(CenarioConsultaOrdemConsumo.Carregada, r.Cenario);
        Assert.True(r.Ordem!.Componentes[0].PesagemLiberada);
        Assert.Contains("elegiveis para pesagem=1", r.Diagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_SemComponentesNoSap_MensagemClaraNaoSilenciosa()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(OrdemSapCustom(liberada: true));

        Assert.Equal(CenarioConsultaOrdemConsumo.SemComponentes, r.Cenario);
        Assert.Equal(ConsumoMaterialServico.MensagemSemComponentes, r.Mensagem);
        Assert.NotNull(r.Ordem); // ordem presente -> grid populado (vazio), com mensagem
        Assert.Contains("componentes SAP (expand+fallback)=0", r.Diagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_ComponenteNaoKg_BloqueiaComMotivoUnidadeEExibeNoGrid()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(OrdemSapCustom(liberada: true, CompSap(unidade: "L")));

        Assert.Equal(CenarioConsultaOrdemConsumo.SemComponentes, r.Cenario);
        Assert.Contains("unidade diferente de KG", r.Mensagem, StringComparison.Ordinal);
        Assert.Single(r.Ordem!.Componentes); // componente exibido no grid, porem bloqueado
        Assert.False(r.Ordem.Componentes[0].PesagemLiberada);
    }

    [Fact]
    public async Task ConsultaOrdem_TodosConsumidos_BloqueiaComMotivoPendenteZerada()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(
            OrdemSapCustom(liberada: true, CompSap(necessaria: 10m, retirada: 10m)));

        Assert.Equal(CenarioConsultaOrdemConsumo.SemComponentes, r.Cenario);
        Assert.Contains("já consumidos", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_SemDeposito_BloqueiaComMotivoDeposito()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(
            OrdemSapCustom(liberada: true, CompSap(deposito: string.Empty)));

        Assert.Equal(CenarioConsultaOrdemConsumo.SemComponentes, r.Cenario);
        Assert.Contains("depósito de consumo ausente", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_NaoLiberada_MensagemEspecificaNaoGenerica()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(OrdemSapCustom(liberada: false, CompSap()));

        Assert.Equal(CenarioConsultaOrdemConsumo.NaoLiberada, r.Cenario);
        Assert.Contains("nenhum componente consumível foi liberado", r.Mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("não está liberada para consumo.", r.Mensagem, StringComparison.Ordinal); // sem msg generica
        Assert.NotNull(r.Ordem); // grid populado com o componente (bloqueado)
    }

    private static OrdemProducaoSap OrdemSapExemplo(bool liberada)
        => new()
        {
            NumeroOrdem = "1000001234",
            TipoOrdem = "PP01",
            MaterialProduzido = "MAT-12345",
            Centro = "1000",
            QuantidadePrevista = 100m,
            Unidade = "PC",
            Deposito = "0001",
            Liberada = liberada,
            Itens = [new ItemOrdemProducaoSap { ItemOrdem = "0001", Material = "MAT-12345" }],
            Operacoes = [new OperacaoOrdemProducaoSap { Operacao = "0010", CentroTrabalho = "MONT01" }],
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    Reserva = "0000123456",
                    ItemReserva = "0001",
                    Material = "COMP-5678",
                    Centro = "1000",
                    Deposito = "0001",
                    QuantidadeNecessaria = 50m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "PC",
                    TipoMovimento = "261"
                },
                new ComponenteOrdemProducaoSap
                {
                    Reserva = "0000123456",
                    ItemReserva = "0002",
                    Material = "COMP-9012",
                    Centro = "1000",
                    Deposito = "0001",
                    QuantidadeNecessaria = 25m,
                    QuantidadeRetirada = 10m,
                    UnidadeBase = "KG",
                    TipoMovimento = string.Empty
                }
            ]
        };

    private sealed class FakeProductionOrderSapServico : IProductionOrderSapServico
    {
        public int Chamadas { get; private set; }
        public ResultadoConsultaOrdemProducaoSap Resultado { get; init; } =
            ResultadoConsultaOrdemProducaoSap.NaoEncontrada();

        public bool EhSimulado => false;
        public bool Configurado => true;

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(Resultado);
        }
    }

    [Fact]
    public void Tela_NaoDeveAcessarBancoOuSapDiretamente()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.DoesNotContain("FabricaConexao", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Repositorio", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API_PRODUCTION_ORDER_2_SRV", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("API_MATERIAL_DOCUMENT_SRV", form, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tela_GridDeveExibirMotivoBackflushSemEnviar261Direto()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("Backflush SAP", form, StringComparison.Ordinal);
        Assert.Contains("não enviar por 261 direto", form, StringComparison.Ordinal);
        Assert.Contains("MotivoInelegibilidadeMaterialDocument261", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_SelecaoOperacionalDeveVirSomenteDoGridPrincipal()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("productionDataGridView.CellClick += ProductionDataGridView_CellClick", form, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.CellMouseClick += ProductionDataGridView_CellMouseClick", form, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.CurrentCellChanged += ProductionDataGridView_CurrentCellChanged", form, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.RowEnter += ProductionDataGridView_RowEnter", form, StringComparison.Ordinal);
        Assert.Contains("private ComponenteConsumoMaterial? ObterComponenteSelecionadoNoGridPrincipal()", form, StringComparison.Ordinal);
        Assert.Contains("private bool CapturarComponenteSelecionadoDoGridPrincipal()", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarComponenteSelecionadoDoGrid();", form, StringComparison.Ordinal);
        Assert.Contains("Seleção consumo: material=", form, StringComparison.Ordinal);
        Assert.Contains("Pesagem consumo usando: material=", form, StringComparison.Ordinal);
        Assert.DoesNotContain("materialDataGridView.CurrentRow?.Tag as ComponenteConsumoMaterial", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PesagemDeveRecapturarComponenteAntesDeValidar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        int inicio = form.IndexOf("private async Task RegistrarPesagemConsumoAsync", StringComparison.Ordinal);
        int fim = form.IndexOf("private TaraConsumoAplicada ObterTaraConsumoAplicada", StringComparison.Ordinal);
        string metodo = form[inicio..fim];

        Assert.Contains("CapturarComponenteSelecionadoDoGridPrincipal();", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("AtualizarComponenteSelecionadoDoGrid();", metodo, StringComparison.Ordinal);
        Assert.Contains("Selecione um componente da ordem antes de iniciar a leitura.", metodo, StringComparison.Ordinal);
        Assert.Contains("ComponenteConsumoMaterial componente = _componenteConsumoSelecionado;", metodo, StringComparison.Ordinal);
        Assert.Contains("RegistrarDiagnosticoPesagemComponente(componente);", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_StartProductionDeveCapturarLinhaAtualAntesDeIniciar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async void StartProduction_Click");

        Assert.Contains("CapturarComponenteSelecionadoDoGridPrincipal();", metodo, StringComparison.Ordinal);
        // Ponto critico: auto-select SEGURO acontece e, em seguida, recaptura a linha DESTACADA.
        Assert.True(
            metodo.IndexOf("SelecionarPrimeiroComponentePesavelSeNecessario()", StringComparison.Ordinal)
            < metodo.IndexOf("CapturarComponenteSelecionadoDoGridPrincipal();", StringComparison.Ordinal));
    }

    [Fact]
    public void IniciarLeitura_HabilitaComOrdemValidaSemExigirCliqueEmLinha()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarLiberacaoInicioLeitura");

        // Botao habilita com OP valida + componente pesavel (NAO exige selecao previa no grid).
        Assert.Contains("PossuiOrdemComComponentePesavel()", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("PossuiOrdemEComponenteValido()", metodo, StringComparison.Ordinal);

        // Ao iniciar sem selecao, auto-seleciona (de forma segura) o primeiro componente pesavel.
        string start = ExtrairMetodo(form, "private async void StartProduction_Click");
        Assert.Contains("SelecionarPrimeiroComponentePesavelSeNecessario()", start, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoSelecao_Segura_DestacaLinhaSincronizaComponenteEPreservaSelecaoManual()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private bool SelecionarPrimeiroComponentePesavelSeNecessario");

        // Correcao 4: preserva selecao manual valida (nao troca para o primeiro).
        Assert.Contains("ObterComponenteSelecionadoNoGridPrincipal()", metodo, StringComparison.Ordinal);
        Assert.Contains("atual is not null && atual.PesagemLiberada", metodo, StringComparison.Ordinal);
        // Destaca a linha REAL do grid e sincroniza o componente selecionado.
        Assert.Contains("linha.Tag is not ComponenteConsumoMaterial componente", metodo, StringComparison.Ordinal);
        Assert.Contains("linha.Selected = true;", metodo, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.CurrentCell = linha.Cells[0];", metodo, StringComparison.Ordinal);
        Assert.Contains("_componenteConsumoSelecionado = componente;", metodo, StringComparison.Ordinal);
        // Painel lateral/botões via AtualizarTotaisConsumo + status claro + diagnostico.
        Assert.Contains("AtualizarTotaisConsumo(componente,", metodo, StringComparison.Ordinal);
        Assert.Contains("Componente selecionado automaticamente:", metodo, StringComparison.Ordinal);
        Assert.Contains("RegistrarDiagnosticoSelecaoConsumo(componente, linha.Index);", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Pesagem_F9F12_RecapturamLinhaDestacadaAntesDeRegistrar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string registrar = ExtrairMetodo(form, "private async Task RegistrarPesagemConsumoAsync");

        // F9 (manual) e F12 (balança) entram por RegistrarPesagemConsumoAsync, que recaptura a linha atual.
        Assert.StartsWith("private async Task RegistrarPesagemConsumoAsync", registrar.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("CapturarComponenteSelecionadoDoGridPrincipal();", registrar, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_CapturaForcadaNaoDeveBloquearQuandoLeituraEstaAtiva()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string captura = ExtrairMetodo(form, "private bool CapturarComponenteSelecionadoDoGridPrincipal");

        Assert.Contains("ObterLinhaSelecionadaNoGridPrincipal()", captura, StringComparison.Ordinal);
        Assert.Contains("_componenteConsumoSelecionado = linhaSelecionada?.Tag as ComponenteConsumoMaterial;", captura, StringComparison.Ordinal);
        Assert.DoesNotContain("_isProductionStarted", captura, StringComparison.Ordinal);
    }

    [Fact]
    public void ControllerEServico_DevePrepararArquiteturaSemIntegracaoSap()
    {
        string controller = LerArquivoProjeto("Controle", "Processo", "ProcessoConsumoMaterialController.cs");
        string servico = LerArquivoProjeto("Servicos", "Operacao", "ConsumoMaterialServico.cs");

        Assert.Contains("ProcessoConsumoMaterialController", controller, StringComparison.Ordinal);
        Assert.Contains("ConsumoMaterialServico", servico, StringComparison.Ordinal);
        Assert.Contains("ConsultarOrdemProducaoAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SapApi", controller, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", servico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SapApi", servico, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EntradaProduto_DevePermanecerIntactaSemNovoPatch()
    {
        string entradaController = LerArquivoProjeto("Controle", "Processo", "EntradaProdutoController.cs");
        string entradaForm = LerArquivoProjeto("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");

        Assert.Contains("GoodsMovementRefDocType = \"B\"", entradaController, StringComparison.Ordinal);
        Assert.DoesNotContain("Patch", entradaController, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PATCH", entradaForm, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Projeto_NaoDeveConterReferenciaAtivaProcessoPesagemApontamento()
    {
        string raiz = RaizProjeto();
        string[] dirsIgnoradas = ["bin", "obj", "pacotes_limpos", ".git", ".vs", "_backup", "FugaPET_Dev.Tests"];

        List<string> ocorrencias = Directory
            .EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(arquivo => dirsIgnoradas.All(dir =>
                !arquivo.Contains($"{Path.DirectorySeparatorChar}{dir}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            .Where(arquivo => File.ReadAllText(arquivo).Contains("ProcessoPesagemApontamento", StringComparison.Ordinal))
            .Select(arquivo => Path.GetFileName(arquivo)!)
            .ToList();

        Assert.True(ocorrencias.Count == 0, "Referencia ativa a ProcessoPesagemApontamento: " + string.Join(", ", ocorrencias!));
    }

    [Fact]
    public void PossuiOrdemEComponenteValido_DeveUsarComponenteSelecionadoNaoGridDeLeituras()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        int inicio = form.IndexOf("private bool PossuiOrdemEComponenteValido()", StringComparison.Ordinal);
        int fim = form.IndexOf("private", inicio + 20, StringComparison.Ordinal);
        string metodo = form[inicio..fim];

        Assert.Contains("_componenteConsumoSelecionado", metodo, StringComparison.Ordinal);
        Assert.Contains("PesagemLiberada", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("productionDataGridView", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveTerEventoDeSelecaoDeComponenteEEstado()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("materialDataGridView.SelectionChanged += MaterialDataGridView_SelectionChanged;", form, StringComparison.Ordinal);
        Assert.Contains("materialDataGridView.CellClick += MaterialDataGridView_CellClick;", form, StringComparison.Ordinal);
        Assert.Contains("private OrdemProducaoConsumo? _ordemConsumoAtual;", form, StringComparison.Ordinal);
        Assert.Contains("private ComponenteConsumoMaterial? _componenteConsumoSelecionado;", form, StringComparison.Ordinal);
        // Mensagem de componente nao liberado.
        Assert.Contains("Componente já consumido ou não liberado para pesagem.", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveExibirMensagemDoResultadoNoStatusEMessageBox()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("statusLabel.Text = resultado.Mensagem;", form, StringComparison.Ordinal);
        Assert.Contains("MessageBox.Show(resultado.Mensagem", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DevePreencherCabecalhoDaOrdemCarregada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("DefinirTextoCampoOrdem(ordem.NumeroOrdem);", form, StringComparison.Ordinal);
        Assert.Contains("finishedProductCodeTextBox.Text = ordem.MaterialProduzido;", form, StringComparison.Ordinal);
        Assert.Contains("lotTextBox.Text = ordem.LoteProdutoProduzido;", form, StringComparison.Ordinal);
        // Novo card PLANTA recebe a planta da OP.
        Assert.Contains("plantaValueLabel.Text = ordem.Planta;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Cabecalho_PossuiCardPlantaAntesDoProdutoSemiAcabado()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        Assert.Contains("plantaCaptionLabel.Text = \"PLANTA\";", designer, StringComparison.Ordinal);
        Assert.Contains("tableLayoutPanel3.Controls.Add(plantaCardPanel, 3, 0);", designer, StringComparison.Ordinal);
        // Produto semiacabado foi deslocado para a coluna seguinte (apos a PLANTA).
        Assert.Contains("tableLayoutPanel3.Controls.Add(finishedProductCardPanel, 4, 0);", designer, StringComparison.Ordinal);
        Assert.Contains("tableLayoutPanel3.ColumnCount = 5;", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DevePreencherGridPrincipalVisivelComComponentesDaOrdem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void PreencherOrdemCarregada");

        Assert.Contains("productionDataGridView.Rows.Clear();", metodo, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.Rows.Add(", metodo, StringComparison.Ordinal);
        Assert.Contains("linhaPrincipal.Tag = componente;", metodo, StringComparison.Ordinal);
        Assert.Contains("ObterDescricaoProdutoGrid(componente)", metodo, StringComparison.Ordinal);
        // Colunas separadas (reserva/item/deposito/lote/tipo SAP), sem concatenar na descricao.
        Assert.Contains("componente.NumeroReserva", metodo, StringComparison.Ordinal);
        Assert.Contains("componente.DepositoConsumo", metodo, StringComparison.Ordinal);
        Assert.Contains("ObterLoteComponenteGrid(componente)", metodo, StringComparison.Ordinal);
        Assert.Contains("ObterTipoSapGrid(componente)", metodo, StringComparison.Ordinal);
        Assert.Contains("A ordem de produção não possui componentes para consumo.", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveSelecionarComponentePeloGridPrincipalVisivel()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarComponenteSelecionado");

        Assert.Contains("AtualizarComponenteSelecionadoDoGrid();", metodo, StringComparison.Ordinal);
        string central = ExtrairMetodo(form, "private void AtualizarComponenteSelecionadoDoGrid");
        string captura = ExtrairMetodo(form, "private bool CapturarComponenteSelecionadoDoGridPrincipal");
        Assert.Contains("CapturarComponenteSelecionadoDoGridPrincipal();", central, StringComparison.Ordinal);
        Assert.Contains("ObterLinhaSelecionadaNoGridPrincipal()", captura, StringComparison.Ordinal);
        Assert.Contains("linhaSelecionada?.Tag as ComponenteConsumoMaterial", captura, StringComparison.Ordinal);
        Assert.Contains("_componenteConsumoSelecionado.PesagemLiberada", captura, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveRegistrarPesagemAtualizandoPesoUtilizadoSemNovaLinhaNoGridPrincipal()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string registrar = ExtrairMetodo(form, "private async Task RegistrarPesagemConsumoAsync");
        string atualizarLinha = ExtrairMetodo(form, "private void AtualizarLinhaComponenteSelecionado");

        Assert.DoesNotContain("productionDataGridView.Rows.Add", registrar, StringComparison.Ordinal);
        Assert.Contains("AtualizarTotaisConsumo(componente, chave);", registrar, StringComparison.Ordinal);
        Assert.Contains("productionWeightColumn", atualizarLinha, StringComparison.Ordinal);
        // Ajuste 7: Peso Previsto NAO e reescrito; agora atualiza Saldo Restante (nao a coluna de previsto).
        Assert.DoesNotContain("productionQuantityColumn", atualizarLinha, StringComparison.Ordinal);
        Assert.Contains("productionSaldoColumn", atualizarLinha, StringComparison.Ordinal);
        Assert.Contains("SomarPesagensLocais(chave)", atualizarLinha, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveManterComponentesBloqueadosNoGridPrincipalComMotivo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("ObterMotivoComponenteBloqueado", form, StringComparison.Ordinal);
        Assert.Contains("componente já consumido", form, StringComparison.Ordinal);
        Assert.Contains("componente não liberado para pesagem", form, StringComparison.Ordinal);
        Assert.Contains("AplicarStatusVisualComponente(linhaPrincipal, componente);", form, StringComparison.Ordinal);
        // Motivo completo agora no tooltip da linha (fora da coluna de descricao).
        Assert.Contains("DefinirTooltipLinha(linhaPrincipal, ObterTooltipComponente(componente));", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveMostrarESolicitarLoteDoComponenteAntesDeSalvar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("Não informado", form, StringComparison.Ordinal); // coluna Lote Componente quando vazio
        Assert.Contains("GarantirLoteComponentesPesados()", form, StringComparison.Ordinal);
        Assert.Contains("SolicitarLoteComponente(componente)", form, StringComparison.Ordinal);
        Assert.Contains("Informe o lote do componente", form, StringComparison.Ordinal);
        Assert.Contains("este lote é da matéria-prima consumida, não do produto produzido", form, StringComparison.Ordinal);
        Assert.Contains("AplicarLoteComponente(componente, loteInformado.Trim())", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_LoteInformadoDeveIrParaComponenteGridEPesagens()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AplicarLoteComponente");

        Assert.Contains("componente.Lote = lote;", metodo, StringComparison.Ordinal);
        // Lote do componente agora tem coluna propria (productionLoteColumn), nao mais na descricao.
        Assert.Contains("productionLoteColumn", metodo, StringComparison.Ordinal);
        Assert.Contains("pesagem.Lote = lote;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_PainelLateralDeveUsarLinhaAtualDoGridPrincipal()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string selecionar = ExtrairMetodo(form, "private bool CapturarComponenteSelecionadoDoGridPrincipal");
        string obterLinha = ExtrairMetodo(form, "private DataGridViewRow? ObterLinhaSelecionadaNoGridPrincipal");

        Assert.Contains("ObterLinhaSelecionadaNoGridPrincipal()", selecionar, StringComparison.Ordinal);
        Assert.DoesNotContain("materialDataGridView.CurrentRow?.Tag as ComponenteConsumoMaterial", selecionar, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.CurrentCell", obterLinha, StringComparison.Ordinal);
        Assert.Contains("productionDataGridView.CurrentRow", obterLinha, StringComparison.Ordinal);
    }

    [Fact]
    public void Tela_DeveTerBotaoPreviewConfirmacaoSomenteParaBackflush()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("Preview Confirmação", form, StringComparison.Ordinal);
        Assert.Contains("CriarBotaoPreviewConfirmacao();", form, StringComparison.Ordinal);
        // Tarefa 16: habilita p/ componente Backflush selecionado OU lancamento salvo de rota Backflush.
        Assert.Contains("_previewConfirmacaoButton.Enabled = _componenteConsumoSelecionado?.BackflushSap == true", form, StringComparison.Ordinal);
        Assert.Contains("_rotaEnvioSalva == RotaEnvioConsumo.BackflushConfirmacao", form, StringComparison.Ordinal);
        // Preview puro: nada de envio/POST/CSRF/PATCH no fluxo de confirmacao.
        Assert.Contains("GerarPreviewConfirmacaoProducao(", form, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpMethod.Patch", form, StringComparison.Ordinal);
    }

    // ---------- Layout/UX (Tarefa 13): colunas separadas, Tipo SAP, Peso Previsto fixo ----------

    [Fact]
    public void Grid_DeveTerColunasSeparadasDeDepositoLoteReservaItemTipoSap()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        Assert.Contains("productionReservaColumn.HeaderText = \"Reserva\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionItemColumn.HeaderText = \"Item\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionDepositoColumn.HeaderText = \"Depósito\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionLoteColumn.HeaderText = \"Lote Componente\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionTipoSapColumn.HeaderText = \"Tipo SAP\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionSaldoColumn.HeaderText = \"Saldo Restante\";", designer, StringComparison.Ordinal);
        // Peso Previsto e Peso Utilizado preservados.
        Assert.Contains("productionQuantityColumn.HeaderText = \"Peso Previsto\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionWeightColumn.HeaderText = \"Peso Utilizado\";", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Grid_DescricaoNaoConcatenaDepositoReservaLoteNemMotivo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // A descricao do produto NAO deve mais concatenar deposito/reserva/lote/motivo.
        Assert.DoesNotContain("Depósito {componente.DepositoConsumo}", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote componente: {componente.Lote", form, StringComparison.Ordinal);
        Assert.DoesNotContain("Reserva {componente.NumeroReserva}/{componente.ItemReserva} Depósito", form, StringComparison.Ordinal);
    }

    [Fact]
    public void TipoSap_BackflushExibeRotuloCurto()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private static string ObterTipoSapGrid");

        Assert.Contains("\"261 Direto\"", metodo, StringComparison.Ordinal);
        Assert.Contains("\"Backflush\"", metodo, StringComparison.Ordinal);
        Assert.Contains("\"Bloqueado\"", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Grid_PesoPrevistoNaoEhReduzidoAoPesar_AtualizaUtilizadoESaldo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarLinhaComponenteSelecionado");

        // Peso Previsto (productionQuantityColumn) NAO e reescrito durante a pesagem.
        Assert.DoesNotContain("productionQuantityColumn", metodo, StringComparison.Ordinal);
        // Peso Utilizado e Saldo Restante SAO atualizados.
        Assert.Contains("productionWeightColumn", metodo, StringComparison.Ordinal);
        Assert.Contains("productionSaldoColumn", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Campo_LoteSuperiorRenomeadoParaLoteProduto()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        Assert.Contains("lotCaptionLabel.Text = \"LOTE PRODUTO\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("lotCaptionLabel.Text = \"LOTE\";", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void CampoOrdem_AlterarTextoLimpaDadosDaOrdemCarregada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Padrao da Entrada: TextUpdate limpa a OP anterior ao digitar.
        Assert.Contains("productionOrderComboBox.TextUpdate += ProductionOrderComboBox_TextUpdate;", form, StringComparison.Ordinal);
        string metodo = ExtrairMetodo(form, "private void ProductionOrderComboBox_TextUpdate");
        Assert.Contains("LimparDadosOrdem(limparNumeroOrdem: false);", metodo, StringComparison.Ordinal);
        // O valor digitado permanece visivel (preenchimento programatico suprime o evento).
        Assert.Contains("DefinirTextoCampoOrdem(", form, StringComparison.Ordinal);
    }

    [Fact]
    public void CampoOrdem_UsaComboBoxNoMesmoPadraoDoPedidoDaEntrada()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Mesmo controle visual da Entrada (ComboBox editavel, FlatStyle.Flat, vermelho).
        Assert.Contains("productionOrderComboBox = new ComboBox();", designer, StringComparison.Ordinal);
        Assert.Contains("productionOrderComboBox.FlatStyle = FlatStyle.Flat;", designer, StringComparison.Ordinal);
        Assert.Contains("private ComboBox productionOrderComboBox;", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("productionOrderTextBox", designer, StringComparison.Ordinal);
        // Comportamento da Entrada: editavel, sem autocomplete nativo, eventos de selecao/validacao.
        Assert.Contains("productionOrderComboBox.DropDownStyle = ComboBoxStyle.DropDown;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderComboBox.AutoCompleteMode = AutoCompleteMode.None;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderComboBox.SelectedIndexChanged += ProductionOrderComboBox_SelectedIndexChanged;", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderComboBox.KeyDown += ProductionOrderComboBox_KeyDown;", form, StringComparison.Ordinal);
        // Padrao da Entrada: Validated consulta ao sair do campo (guardado por _suprimirEventoOrdem).
        Assert.Contains("productionOrderComboBox.Validated += ProductionOrderComboBox_Validated;", form, StringComparison.Ordinal);
        string validated = ExtrairMetodo(form, "private async void ProductionOrderComboBox_Validated");
        Assert.Contains("DeveIgnorarValidacaoOrdem()", validated, StringComparison.Ordinal);
        Assert.Contains("ConsultarOrdemProducaoAsync()", validated, StringComparison.Ordinal);
        // O guard de supressao agora vive no helper central.
        string guard = ExtrairMetodo(form, "private bool DeveIgnorarValidacaoOrdem");
        Assert.Contains("_suprimirEventoOrdem", guard, StringComparison.Ordinal);
        // Igual a Entrada: combo ocupa toda a largura do cartao (icone escondido + ajuste no Resize).
        Assert.Contains("productionOrderIconPanel.Visible = false;", form, StringComparison.Ordinal);
        Assert.Contains("AjustarLarguraComboOrdem();", form, StringComparison.Ordinal);
        Assert.Contains("productionOrderShadowPanel.Resize +=", form, StringComparison.Ordinal);
    }

    [Fact]
    public void CampoOrdem_EnterESelecaoConsultam_ListaRecentePreservaDigitacao()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Enter consulta.
        string keyDown = ExtrairMetodo(form, "private async void ProductionOrderComboBox_KeyDown");
        Assert.Contains("Keys.Enter", keyDown, StringComparison.Ordinal);
        Assert.Contains("ConsultarOrdemProducaoAsync()", keyDown, StringComparison.Ordinal);
        // Selecao na lista consulta.
        string selChanged = ExtrairMetodo(form, "private async void ProductionOrderComboBox_SelectedIndexChanged");
        Assert.Contains("ConsultarOrdemProducaoAsync()", selChanged, StringComparison.Ordinal);
        // Parte 3: OP consultada com sucesso entra na lista recente, sem bloquear digitacao manual.
        Assert.Contains("RegistrarOrdemRecente(resultado.NumeroOrdem)", form, StringComparison.Ordinal);
        string recente = ExtrairMetodo(form, "private void RegistrarOrdemRecente");
        Assert.Contains("productionOrderComboBox.Items.Insert(0, valor);", recente, StringComparison.Ordinal);
    }

    // ---------- Tarefa 14: parser decimal, liberação central, painéis, sapStatus ----------

    [Theory]
    [InlineData("0,4", 0.4)]
    [InlineData("0.4", 0.4)]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    [InlineData("10", 10)]
    [InlineData("10 kg", 10)]
    [InlineData("0,400kg", 0.4)]
    [InlineData("1.234,5", 1234.5)]
    [InlineData("1,234.5", 1234.5)]
    public void Peso_TryParsePesoConsumoKg_AceitaDecimalKgSemNormalizarInteiro(string entrada, double esperado)
    {
        Assert.True(ConsumoMaterialServico.TryParsePesoConsumoKg(entrada, out decimal peso));
        Assert.Equal((decimal)esperado, peso);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("0,0")]
    [InlineData("-1")]
    [InlineData("-0,4")]
    [InlineData("abc")]
    public void Peso_TryParsePesoConsumoKg_RejeitaVazioZeroNegativo(string entrada)
        => Assert.False(ConsumoMaterialServico.TryParsePesoConsumoKg(entrada, out _));

    [Fact]
    public void Liberacao_SemDeposito_PendenteZero_UnidadeNaoKg_Bloqueia()
    {
        Assert.False(ConsumoMaterialServico.AvaliarLiberacaoPesagem(
            new ComponenteConsumoMaterial { DepositoConsumo = string.Empty, QuantidadePendente = 5m, UnidadeMedida = "KG" },
            out string m1));
        Assert.Contains("depósito", m1, StringComparison.Ordinal);

        Assert.False(ConsumoMaterialServico.AvaliarLiberacaoPesagem(
            new ComponenteConsumoMaterial { DepositoConsumo = "PP01", QuantidadePendente = 0m, UnidadeMedida = "KG" },
            out string m2));
        Assert.Contains("pendente", m2, StringComparison.Ordinal);

        Assert.False(ConsumoMaterialServico.AvaliarLiberacaoPesagem(
            new ComponenteConsumoMaterial { DepositoConsumo = "PP01", QuantidadePendente = 5m, UnidadeMedida = "L" },
            out string m3));
        Assert.Contains("KG", m3, StringComparison.Ordinal);

        Assert.True(ConsumoMaterialServico.AvaliarLiberacaoPesagem(
            new ComponenteConsumoMaterial { DepositoConsumo = "PP01", QuantidadePendente = 5m, UnidadeMedida = "KG" },
            out _));
    }

    [Fact]
    public void AtualizarStatus_SemDeposito_NaoReabilitaPesagem()
    {
        ComponenteConsumoMaterial componente = new()
        {
            DepositoConsumo = string.Empty, QuantidadePendente = 10m, UnidadeMedida = "KG", PesagemLiberada = true
        };

        ConsumoMaterialServico.AtualizarStatusComponentePorTotalLocal(componente, 0m);

        Assert.False(componente.PesagemLiberada);
        Assert.Contains("depósito", componente.MotivoBloqueioPesagem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultaOrdem_ComponenteSemDeposito_NaoPesavelComMotivo()
    {
        ResultadoConsultaOrdemConsumo r = await ConsultarComSap(
            OrdemSapCustom(liberada: true, CompSap(deposito: string.Empty)));

        Assert.False(r.Ordem!.Componentes[0].PesagemLiberada);
        Assert.Contains("depósito", r.Ordem.Componentes[0].MotivoBloqueioPesagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Apontamento_ChipEInfoComResponsabilidadesDistintas()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarApontamentoVisual");

        Assert.Contains("apontamentoChipCaptionLabel.Text = \"ROTA SAP\";", metodo, StringComparison.Ordinal);
        Assert.Contains("apontamentoInfoCaptionLabel.Text = \"ORIENTAÇÃO\";", metodo, StringComparison.Ordinal);
        Assert.Contains("apontamentoChipValueLabel.Text = chip;", metodo, StringComparison.Ordinal);
        Assert.Contains("apontamentoInfoValueLabel.Text = info;", metodo, StringComparison.Ordinal);
        // Rotas curtas no chip + orientação detalhada distinta no info.
        Assert.Contains("\"Sem depósito\"", metodo, StringComparison.Ordinal);
        Assert.Contains("\"Backflush\"", metodo, StringComparison.Ordinal);
        Assert.Contains("Componente sem depósito. Pesagem bloqueada.", metodo, StringComparison.Ordinal);
        Assert.Contains("usar Preview Confirmação", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void SapStatus_PadraoEntrada_TextosEstadoECorDoPonto()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("enum EstadoVisualIntegracaoSapConsumo", form, StringComparison.Ordinal);
        string metodo = ExtrairMetodo(form, "private void AtualizarEstadoVisualIntegracaoSapConsumo");
        Assert.Contains("SAP HML: LIBERADO PARA ENVIO", metodo, StringComparison.Ordinal);
        Assert.Contains("SAP HML: BLOQUEADO — SEM DEPÓSITO", metodo, StringComparison.Ordinal);
        Assert.Contains("SAP HML: BACKFLUSH — CONFIRMAÇÃO EM PREPARAÇÃO", metodo, StringComparison.Ordinal);
        Assert.Contains("sapStatusDotLabel.ForeColor = cor;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Pesagem_UsaParserDecimalSeguroEMensagemKg()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("ConsumoMaterialServico.TryParsePesoConsumoKg(", form, StringComparison.Ordinal);
        Assert.DoesNotContain("TryNormalizeWeight", form, StringComparison.Ordinal);
        Assert.Contains("Informe o peso em KG. Exemplo: 0,400 ou 1,5.", form, StringComparison.Ordinal);
    }

    // ---------- Tarefa 14.1: bloqueio sem depósito, troca de componente, DATA da OP ----------

    [Fact]
    public void RegistrarPesagemLocal_SemDeposito_Bloqueia()
    {
        ComponenteConsumoMaterial c = Componente();
        c.DepositoConsumo = string.Empty;

        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            c, "1000009", 10m, 0m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        Assert.Equal(CenarioPesagemConsumo.ComponenteNaoLiberado, r.Cenario);
        Assert.Contains("depósito", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrarPesagemLocal_PendenteNegativo_Bloqueia()
    {
        ComponenteConsumoMaterial c = Componente();
        c.QuantidadePendente = 0m;
        c.QuantidadePendenteSapOriginal = -0.4m;

        ResultadoPesagemConsumo r = ServicoPesagem().RegistrarPesagemLocal(
            c, "1000009", 1m, 0m, PesagemConsumoMaterial.OrigemBalanca, 0m, 1);

        Assert.Equal(CenarioPesagemConsumo.ComponenteNaoLiberado, r.Cenario);
        Assert.Contains("pendente", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void Pesagem_F9F12_CapturamEValidamComponenteAntesDePedirPeso()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        string f9 = ExtrairMetodo(form, "private async void LeituraManual_Click");
        Assert.Contains("ValidarComponenteAtualParaPesagem(", f9, StringComparison.Ordinal);
        Assert.DoesNotContain("PossuiOrdemEComponenteValido()", f9, StringComparison.Ordinal);

        string f12 = ExtrairMetodo(form, "private async void ReadWeightLegend_Click");
        Assert.Contains("ValidarComponenteAtualParaPesagem(", f12, StringComparison.Ordinal);
        Assert.DoesNotContain("PossuiOrdemEComponenteValido()", f12, StringComparison.Ordinal);

        // Helper captura a linha ANTES de validar.
        string helper = ExtrairMetodo(form, "private bool ValidarComponenteAtualParaPesagem");
        Assert.True(
            helper.IndexOf("CapturarComponenteSelecionadoDoGridPrincipal();", StringComparison.Ordinal)
            < helper.IndexOf("AvaliarLiberacaoPesagem(", StringComparison.Ordinal));
    }

    [Fact]
    public void Selecao_PermiteTrocarComponenteComLeituraAtiva()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarComponenteSelecionadoDoGrid");

        Assert.Contains("if (_isReadingWeight)", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("if (_isProductionStarted)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void CardData_UsaDataDaOpNaoRelogioDoSistema()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        string timer = ExtrairMetodo(form, "private void UpdateFooterDateTime");
        Assert.DoesNotContain("UpdateStepCardDate", timer, StringComparison.Ordinal);
        Assert.DoesNotContain("stepLabel", timer, StringComparison.Ordinal);

        Assert.Contains("AtualizarCardDataOrdem(ordem.DataOrdem);", form, StringComparison.Ordinal);
        Assert.Contains("AtualizarCardDataOrdem(null);", form, StringComparison.Ordinal);
        Assert.Contains("stepCaptionLabel.Text = \"DATA OP\";", designer, StringComparison.Ordinal);
    }

    // ---------- Tarefa 15: não perder pesagens, tara por componente, textos, botão Enviar ----------

    [Fact]
    public void Consulta_MesmaOpNaoReconsultaNemLimpaPesagens()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConsultarOrdemProducaoAsync");

        Assert.Contains("if (_consultandoOrdem)", metodo, StringComparison.Ordinal);
        // Guarda de "mesma OP ja carregada" (nao reconsulta nem limpa pesagens).
        Assert.Contains("NormalizarNumeroOrdem(_ordemConsumoAtual.NumeroOrdem)", metodo, StringComparison.Ordinal);
        Assert.Contains("_consultandoOrdem = true;", metodo, StringComparison.Ordinal);
        Assert.Contains("_consultandoOrdem = false;", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmar_NaoSalvaBaseadoNoGrid_DetectaInconsistencia()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConfirmarConsumoAsync");

        Assert.Contains("GridMostraPesoUtilizado()", metodo, StringComparison.Ordinal);
        Assert.Contains("Inconsistência interna", metodo, StringComparison.Ordinal);
        // Fonte oficial continua sendo _pesagensPorComponente.
        Assert.Contains("_pesagensPorComponente.Values.Sum(lista => lista.Count)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void TextUpdate_PesagensNaoSalvas_PedeConfirmacaoAntesDeDescartar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void ProductionOrderComboBox_TextUpdate");

        Assert.Contains("PossuiPesagensLocaisNaoSalvas()", metodo, StringComparison.Ordinal);
        Assert.Contains("descartar essas pesagens", metodo, StringComparison.Ordinal);
        Assert.Contains("DefinirTextoCampoOrdem(_ordemConsumoAtual?.NumeroOrdem", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Tara_PorComponente_DialogoEUsoNoF9F12()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        // Dicionario por componente + abertura da mesma tela da Entrada.
        Assert.Contains("Dictionary<string, global::FugaPET_Dev.Modelo.Cadastro.TaraCadastro> _tarasPorComponente", form, StringComparison.Ordinal);
        Assert.Contains("using SelecaoTaraPesagemForm form = new(taras, componente.CodigoMaterial);", form, StringComparison.Ordinal);
        Assert.Contains("ListarTarasAtivasPorSetorAsync(", form, StringComparison.Ordinal);

        // F9 e F12 garantem a tara do componente e bloqueiam se nao houver.
        string f9 = ExtrairMetodo(form, "private async void LeituraManual_Click");
        Assert.Contains("SelecionarTaraParaComponenteAsync(componenteF9!)", f9, StringComparison.Ordinal);
        Assert.Contains("ExisteTaraSelecionada(componenteF9!)", f9, StringComparison.Ordinal);
        string f12 = ExtrairMetodo(form, "private async void ReadWeightLegend_Click");
        Assert.Contains("SelecionarTaraParaComponenteAsync(componenteF12!)", f12, StringComparison.Ordinal);
        Assert.Contains("ExisteTaraSelecionada(componenteF12!)", f12, StringComparison.Ordinal);

        // Componente sem deposito/nao pesavel nao abre selecao de tara.
        string sel = ExtrairMetodo(form, "private async Task SelecionarTaraParaComponenteAsync");
        Assert.Contains("AvaliarLiberacaoPesagem(componente, out _) || !componente.PesagemLiberada", sel, StringComparison.Ordinal);
        Assert.Contains("ExisteTaraSelecionada(componente)", sel, StringComparison.Ordinal);
    }

    [Fact]
    public void EnviarSap261_ReaproveitaProductionActionsButton_SemDuplicar()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void CriarBotaoEnviarSap261");

        Assert.Contains("productionActionsButton.Text = \"Enviar SAP 261\";", metodo, StringComparison.Ordinal);
        Assert.Contains("_enviarSap261Button = productionActionsButton;", metodo, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.Click += async (_, _) => await EnviarSap261Async();", metodo, StringComparison.Ordinal);
        // Sem botao duplicado no sidePanel.
        Assert.DoesNotContain("sidePanel.Controls.Add(_enviarSap261Button)", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Textos_ProdutoEComponentesDaOrdem()
    {
        string designer = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.Designer.cs");

        Assert.Contains("finishedProductCaptionLabel.Text = \"PRODUTO\";", designer, StringComparison.Ordinal);
        Assert.Contains("productionReadingsTitleLabel.Text = \"COMPONENTES DA ORDEM\";", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("PRODUTO SEMI ACABADO", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("PRODUTOS DA ORDEM", designer, StringComparison.Ordinal);
    }

    // ---------- Tarefa 15.1: CausesValidation, flags no Confirmar, tara após iniciar, estilo do botão ----------

    [Fact]
    public void Operacionais_NaoCausamValidacaoDoCampoOrdem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void ConfigurarCausesValidacaoOperacional");

        foreach (string controle in new[]
                 {
                     "_confirmarConsumoButton", "_previewSap261Button", "_previewConfirmacaoButton",
                     "productionActionsButton", "iniciarLeituraButton", "leituraManualButton", "lerEtiquetaButton",
                     "startActionPanel", "readWeightLegendPanel", "deleteLastLegendPanel", "deleteByCodeLegendPanel"
                 })
        {
            Assert.Contains(controle, metodo, StringComparison.Ordinal);
        }

        Assert.Contains("controle.CausesValidation = false;", metodo, StringComparison.Ordinal);
        Assert.Contains("ConfigurarCausesValidacaoOperacional();", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmar_TravaValidacaoDesdeOInicioERestauraNoFinally()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConfirmarConsumoAsync");

        // Flags ligadas ANTES das validacoes (antes do calculo de totalPesagens).
        Assert.True(
            metodo.IndexOf("_acaoOperacionalEmAndamento = true;", StringComparison.Ordinal)
            < metodo.IndexOf("_pesagensPorComponente.Values.Sum", StringComparison.Ordinal));
        Assert.True(
            metodo.IndexOf("_salvandoConsumo = true;", StringComparison.Ordinal)
            < metodo.IndexOf("_pesagensPorComponente.Values.Sum", StringComparison.Ordinal));
        // finally restaura ambas.
        Assert.Contains("_salvandoConsumo = false;", metodo, StringComparison.Ordinal);
        Assert.Contains("_acaoOperacionalEmAndamento = false;", metodo, StringComparison.Ordinal);
        // Diagnostico sanitizado antes da mensagem de "nenhuma pesagem".
        Assert.Contains("Confirmar consumo: ordemAtual=", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Validacao_IgnoradaComLeituraOuPesagensNaoSalvas()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string guard = ExtrairMetodo(form, "private bool DeveIgnorarValidacaoOrdem");

        foreach (string condicao in new[]
                 {
                     "_isProductionStarted", "_acaoOperacionalEmAndamento",
                     "PossuiPesagensLocaisNaoSalvas()", "_salvandoConsumo", "_enviandoSap"
                 })
        {
            Assert.Contains(condicao, guard, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Tara_SoAbreDepoisDeIniciarLeitura()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string cellClick = ExtrairMetodo(form, "private async void ProductionDataGridView_CellClick");

        Assert.Contains("_isProductionStarted && !_isReadingWeight && _componenteConsumoSelecionado is not null", cellClick, StringComparison.Ordinal);
        Assert.Contains("SelecionarTaraParaComponenteAsync(_componenteConsumoSelecionado)", cellClick, StringComparison.Ordinal);
        Assert.Contains("Inicie a leitura para selecionar tara e pesar.", cellClick, StringComparison.Ordinal);
    }

    [Fact]
    public void GridMostraPesoUtilizado_SoVerdadeiroParaPesoMaiorQueZero()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private bool GridMostraPesoUtilizado");

        Assert.Contains("out decimal peso", metodo, StringComparison.Ordinal);
        Assert.Contains("peso > 0m", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void EnviarSap261_TemEstiloVisualDaEntrada()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void CriarBotaoEnviarSap261");

        Assert.Contains("productionActionsButton.BackColor = Color.White;", metodo, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.ForeColor = Color.FromArgb(31, 41, 55);", metodo, StringComparison.Ordinal);
        Assert.Contains("productionActionsButton.FlatAppearance.BorderColor = Color.FromArgb(226, 231, 238);", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb(202, 138, 4)", metodo, StringComparison.Ordinal); // sem amarelo
    }

    // ---------- Tarefa 15.2: lote antes da pesagem + reindexação da chave ----------

    [Fact]
    public void Confirmar_DesabilitadoDuranteLeituraAtiva()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AtualizarBotaoConfirmar");

        Assert.Contains("!_isProductionStarted", metodo, StringComparison.Ordinal);
        Assert.Contains("_pesagensPorComponente.Values.Any(lista => lista.Count > 0)", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Pesagem_F9F12_GarantemLoteAntesDaTaraEPeso()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        string f9 = ExtrairMetodo(form, "private async void LeituraManual_Click");
        Assert.True(
            f9.IndexOf("GarantirLoteComponenteAntesDaPesagem(componenteF9!)", StringComparison.Ordinal)
            < f9.IndexOf("SelecionarTaraParaComponenteAsync(componenteF9!)", StringComparison.Ordinal));

        string f12 = ExtrairMetodo(form, "private async void ReadWeightLegend_Click");
        Assert.True(
            f12.IndexOf("GarantirLoteComponenteAntesDaPesagem(componenteF12!)", StringComparison.Ordinal)
            < f12.IndexOf("SelecionarTaraParaComponenteAsync(componenteF12!)", StringComparison.Ordinal));

        // Sem lote -> nao registra peso.
        string helper = ExtrairMetodo(form, "private bool GarantirLoteComponenteAntesDaPesagem");
        Assert.Contains("Peso não registrado: informe o lote do componente", helper, StringComparison.Ordinal);
        Assert.Contains("AplicarLoteComponente(componente, loteInformado.Trim());", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void AplicarLote_ReindexaPesagensDaChaveAntigaParaNova()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private void AplicarLoteComponente");

        Assert.Contains("string chaveAntiga = ProcessoConsumoMaterialController.ChaveComponente(componente);", metodo, StringComparison.Ordinal);
        Assert.Contains("string chaveNova = ProcessoConsumoMaterialController.ChaveComponente(componente);", metodo, StringComparison.Ordinal);
        Assert.Contains("_pesagensPorComponente.Remove(chaveAntiga);", metodo, StringComparison.Ordinal);
        Assert.Contains("existentes.AddRange(pesagens);", metodo, StringComparison.Ordinal);
        // NAO usa mais o padrao perigoso material-only.
        Assert.DoesNotContain("p.CodigoMaterial,", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ChaveComponente_IncluiLoteReservaItemDeposito()
    {
        ComponenteConsumoMaterial sem = new()
        {
            CodigoMaterial = "1000037", NumeroReserva = "11237", ItemReserva = "4",
            DepositoConsumo = "PP01", Lote = string.Empty
        };
        ComponenteConsumoMaterial com = new()
        {
            CodigoMaterial = "1000037", NumeroReserva = "11237", ItemReserva = "4",
            DepositoConsumo = "PP01", Lote = "000256"
        };

        string chaveSem = ProcessoConsumoMaterialController.ChaveComponente(sem);
        string chaveCom = ProcessoConsumoMaterialController.ChaveComponente(com);

        Assert.NotEqual(chaveSem, chaveCom);               // o lote muda a chave (causa do bug)
        Assert.Contains("000256", chaveCom, StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmar_DiagnosticoDeChavesEMensagemSemPesagem()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task ConfirmarConsumoAsync");

        Assert.Contains("Salvar consumo - componente: material=", metodo, StringComparison.Ordinal);
        Assert.Contains("possuiPesagem=", metodo, StringComparison.Ordinal);
        Assert.Contains("CenarioPersistenciaConsumo.SemPesagem && totalPesagens > 0", metodo, StringComparison.Ordinal);
        Assert.Contains("não foram vinculadas aos componentes da OP", metodo, StringComparison.Ordinal);
    }

    // ---------- Tarefa 16: rota do envio (261 direto x Backflush) ----------

    [Fact]
    public void Envio261_HabilitaSoParaRotaDireto_BloqueiaBackflush()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        string atualizar = ExtrairMetodo(form, "private void AtualizarBotaoConfirmar");
        Assert.Contains("_rotaEnvioSalva == RotaEnvioConsumo.Direto261", atualizar, StringComparison.Ordinal);

        // Tooltip por rota.
        string tooltip = ExtrairMetodo(form, "private string ObterTooltipEnvio261");
        Assert.Contains("Backflush não usa 261 direto. Use Confirmação de Produção.", tooltip, StringComparison.Ordinal);

        // Defesa extra no envio: Backflush nao vai por 261 direto + classificacao ao salvar.
        string enviar = ExtrairMetodo(form, "private async Task EnviarSap261Async");
        Assert.Contains("_rotaEnvioSalva == RotaEnvioConsumo.BackflushConfirmacao", enviar, StringComparison.Ordinal);
        Assert.Contains("não deve ser enviado por movimento 261 direto", enviar, StringComparison.Ordinal);
        Assert.Contains("ClassificarRotaEnvio(consumidos)", form, StringComparison.Ordinal);
        Assert.Contains("Aguardando envio por Confirmação de Produção.", form, StringComparison.Ordinal);
        // Preview a partir do lancamento salvo.
        Assert.Contains("GerarPreviewConfirmacaoProducaoRealAsync(", form, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvioConfirmacao_BackflushTemBotaoDedicadoESem261Direto()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");

        Assert.Contains("CriarBotaoEnviarConfirmacao();", form, StringComparison.Ordinal);
        Assert.Contains("Name = \"enviarConfirmacaoButton\"", form, StringComparison.Ordinal);
        Assert.Contains("Text = \"Enviar Confirmação\"", form, StringComparison.Ordinal);
        Assert.Contains("_enviarConfirmacaoButton.Click += async (_, _) => await EnviarConfirmacaoProducaoAsync();", form, StringComparison.Ordinal);

        string atualizar = ExtrairMetodo(form, "private void AtualizarBotaoConfirmar");
        Assert.Contains("_rotaEnvioSalva == RotaEnvioConsumo.BackflushConfirmacao", atualizar, StringComparison.Ordinal);
        Assert.Contains("_enviandoConfirmacao", atualizar, StringComparison.Ordinal);

        string enviarConfirmacao = ExtrairMetodo(form, "private async Task EnviarConfirmacaoProducaoAsync");
        Assert.Contains("_controller.EnviarConfirmacaoProducaoAsync", enviarConfirmacao, StringComparison.Ordinal);
        Assert.Contains("_rotaEnvioSalva != RotaEnvioConsumo.BackflushConfirmacao", enviarConfirmacao, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarConsumoSap261Async", enviarConfirmacao, StringComparison.Ordinal);
    }

    // ---------- Tarefa 17.6: preview real + bloqueio FALHA_SAP ----------

    [Fact]
    public void PreviewConfirmacao_UsaPayloadRealDoEnvio()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string metodo = ExtrairMetodo(form, "private async Task VisualizarPreviewConfirmacao");

        // Preview do lancamento salvo usa o builder REAL (payload do POST), nao o conceitual antigo.
        Assert.Contains("GerarPreviewConfirmacaoProducaoRealAsync(codigoSalvo)", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("GerarPreviewConfirmacaoProducaoDoLancamentoAsync", metodo, StringComparison.Ordinal);
        Assert.Contains("PayloadJson", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void FalhaSap_BloqueiaReenvioAutomatico()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string atualizar = ExtrairMetodo(form, "private void AtualizarBotaoConfirmar");

        // Botoes de envio bloqueiam quando o lancamento esta FALHA_SAP.
        Assert.Contains("!_lancamentoComFalhaSap", atualizar, StringComparison.Ordinal);
        // Falha de nivel SAP (HTTP) marca a flag nos dois fluxos de envio.
        Assert.Contains("if (!resultado.Sucesso && resultado.StatusHttp.HasValue)", form, StringComparison.Ordinal);
        Assert.Contains("_lancamentoComFalhaSap = true;", form, StringComparison.Ordinal);
    }

    [Fact]
    public void Consumo_NaoDeveConterPostNemPatchNovo()
    {
        string form = LerArquivoProjeto("Tela", "Processo", "ProcessoConsumoMaterialForm.cs");
        string servico = LerArquivoProjeto("Servicos", "Operacao", "ConsumoMaterialServico.cs");
        string client = LerArquivoProjeto("Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs");

        foreach (string fonte in new[] { form, servico, client })
        {
            Assert.DoesNotContain("HttpMethod.Post", fonte, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpMethod.Patch", fonte, StringComparison.Ordinal);
        }
    }

    private static string LerArquivoProjeto(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

    private static string ExtrairMetodo(string fonte, string assinatura)
    {
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Metodo nao encontrado: {assinatura}");

        int proximoMetodo = fonte.IndexOf("\n    private ", inicio + assinatura.Length, StringComparison.Ordinal);
        Assert.True(proximoMetodo > inicio, $"Fim do metodo nao encontrado: {assinatura}");

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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
