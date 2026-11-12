using System.Reflection;
using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Consumo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 101W — PROVA COMPORTAMENTAL (não source-scan) do recovery pós-retomada na Form REAL.
/// Instancia <see cref="ProcessoConsumoMaterialForm"/> em thread STA, injeta um resolvedor READ-ONLY fake
/// (sem banco/SAP), executa o CÓDIGO PRODUTIVO de resolução/materialização e depois o entrypoint REAL do
/// refresh de seleção (<c>AtualizarComponenteSelecionadoDoGrid</c> — o mesmo chamado pelos eventos do grid),
/// e afere o estado REAL dos controles. Reproduz o incidente OP1000170/0010/PK8.
/// </summary>
public sealed class ConsumoRecuperacaoFormPosRetomadaBehavioralTests
{
    // ---------------- infraestrutura STA + reflection ----------------

    private static void RunSta(Action corpo)
    {
        Exception? capturada = null;
        var t = new Thread(() =>
        {
            try { corpo(); }
            catch (Exception e) { capturada = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        t.Join();
        if (capturada is not null)
        {
            throw new Xunit.Sdk.XunitException("Falha no corpo STA: " + capturada);
        }
    }

    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static void SetField(object alvo, string nome, object? valor)
        => ObterCampo(alvo.GetType(), nome).SetValue(alvo, valor);

    private static T? GetField<T>(object alvo, string nome)
        => (T?)ObterCampo(alvo.GetType(), nome).GetValue(alvo);

    private static FieldInfo ObterCampo(Type? tipo, string nome)
    {
        for (Type? t = tipo; t is not null; t = t.BaseType)
        {
            FieldInfo? f = t.GetField(nome, Priv);
            if (f is not null) return f;
        }

        throw new MissingFieldException(tipo?.FullName, nome);
    }

    private static object? Invoke(object alvo, string metodo, params object?[] args)
    {
        for (Type? t = alvo.GetType(); t is not null; t = t.BaseType)
        {
            MethodInfo? m = t.GetMethod(metodo, Priv);
            if (m is not null)
            {
                object? r = m.Invoke(alvo, args);
                if (r is Task tarefa) tarefa.GetAwaiter().GetResult();
                return r;
            }
        }

        throw new MissingMethodException(alvo.GetType().FullName, metodo);
    }

    private static bool ControleEnabled(object form, string campo)
        => GetField<System.Windows.Forms.Control>(form, campo)?.Enabled ?? false;

    // Control.Visible (getter) percorre a cadeia de pais e é sempre false enquanto o Form não é exibido.
    // Para provar a intenção REAL do código produtivo (Visible = true/false que ELE setou), lemos o bit
    // intrínseco de estado via Control.GetState(States.Visible), sem depender da exibição da janela.
    private static readonly Type StatesType =
        typeof(System.Windows.Forms.Control).GetNestedType("States", BindingFlags.NonPublic)!;

    private static readonly MethodInfo GetStateMethod =
        typeof(System.Windows.Forms.Control).GetMethod("GetState", Priv, [StatesType])!;

    private static bool ControleVisible(object form, string campo)
    {
        var c = GetField<System.Windows.Forms.Control>(form, campo);
        if (c is null) return false;
        object visible = Enum.ToObject(StatesType, 0x00000002); // States.Visible
        return (bool)GetStateMethod.Invoke(c, [visible])!;
    }

    private static string StatusText(object form)
        => GetField<System.Windows.Forms.Control>(form, "statusLabel")?.Text ?? string.Empty;

    // ---------------- cenário/fixture ----------------

    private static ContextoApontamentoProcesso ContextoOp1000170()
        => new() { NumeroOrdem = "1000170", Operacao = "0010", Sequencia = "0", TipoProcesso = "CONSUMO_MATERIA_PRIMA" };

    private static List<ComponenteConsumoMaterial> ComponentesOcorrencia()
        => [new()
        {
            CodigoMaterial = "1000186", NumeroReserva = "185", ItemReserva = "1",
            DepositoConsumo = "PP01", Lote = "0000000222", TipoMovimento = "261", PesagemLiberada = true,
            QuantidadePendente = 2271.698m, UnidadeMedida = "KG"
        }];

    private static ConsumoMaterialItem ItemPk8()
        => new()
        {
            CodigoMaterial = "1000186", NumeroReserva = "185", ItemReserva = "1",
            DepositoConsumo = "PP01", Lote = "0000000222", TipoMovimentoSap = "261"
        };

    /// <summary>Injeta no controller da Form um ConsumoMaterialConsultaServico com repositório FAKE (sem banco).</summary>
    private static FakeRepo InjetarResolvedorFake(object form, FakeRepo repo)
    {
        object controller = GetField<object>(form, "_controller")!;
        var consulta = new ConsumoMaterialConsultaServico(() => repo, new ConsumoMaterialServico());
        SetField(controller, "_consultaServico", consulta);
        return repo;
    }

    private static object CriarFormComContexto()
        => new ProcessoConsumoMaterialForm(ModoConsumoMaterial.MateriaPrima, ContextoOp1000170());

    private static void ResolverEReceber(object form, FakeRepo repo)
    {
        InjetarResolvedorFake(form, repo);
        // Executa o CÓDIGO PRODUTIVO real de resolução + aplicação da modalidade (switch/materialização).
        Invoke(form, "ResolverRecuperacaoPersistidaDoContextoAsync", ComponentesOcorrencia());
    }

    // ==================================================================
    // PROVA PRINCIPAL — UmPendente / PK8 sobrevive ao refresh tardio (P01–P08 comportamental)
    // ==================================================================

    [Fact]
    public void UmPendentePk8_RefreshTardioNaoReabilitaFreshFlow()
    {
        RunSta(() =>
        {
            object form = CriarFormComContexto();

            // Save de sessão propositalmente DIVERGENTE, para provar que o envio de recovery NÃO o usa.
            SetField(form, "_ultimoCodigoLancamentoSalvo", 999L);

            var repo = new FakeRepo
            {
                Candidatos = [new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" }],
                Detalhes = { [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] } }
            };

            // (C/D) resolve + materializa via código produtivo real.
            ResolverEReceber(form, repo);

            // Estado imediatamente após materialização.
            Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));
            Assert.Equal(8L, GetField<long?>(form, "_codigoLancamentoRecuperado"));

            // (E) REFRESH TARDIO real: o MESMO método que os eventos SelectionChanged/CurrentCellChanged/RowEnter chamam.
            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");

            // ---- asserts APÓS o refresh (estado REAL dos controles) ----
            Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));
            Assert.Equal(8L, GetField<long?>(form, "_codigoLancamentoRecuperado"));

            Assert.True((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            Assert.False((bool)Invoke(form, "PodeIniciarLeituraConsumo")!);      // fresh-flow bloqueado

            Assert.False(ControleEnabled(form, "iniciarLeituraButton"));          // INICIAR LEITURA
            Assert.False(ControleEnabled(form, "lerEtiquetaButton"));             // LER PESO
            Assert.False(ControleEnabled(form, "leituraManualButton"));          // DIGITAR PESO
            Assert.False(ControleEnabled(form, "_confirmarConsumoButton"));      // CONFIRMAR CONSUMO

            Assert.True(ControleVisible(form, "_enviarSap261Button"));           // Enviar SAP 261 disponível

            // Status permanece semanticamente recovery/PENDENTE — NÃO fresh-flow.
            string status = StatusText(form);
            Assert.Contains("PENDENTE_SAP", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Inicie a leitura", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Pronto para leitura", status, StringComparison.OrdinalIgnoreCase);

            // (§8) PK do envio de recovery = 8 (recuperado), nunca o save de sessão (999).
            long? pkEnvio = (long?)Invoke(form, "PkEnvioRecuperadoAtual");
            Assert.Equal(8L, pkEnvio);
            Assert.NotEqual(GetField<long?>(form, "_ultimoCodigoLancamentoSalvo"), pkEnvio);

            // (§9) materialização/refresh READ-ONLY: zero efeitos colaterais no repositório.
            Assert.Equal(0, repo.Saves);
            Assert.Equal(0, repo.Reservas);
            Assert.Equal(0, repo.Confirmacoes);
            Assert.Equal(0, repo.Falhas);
        });
    }

    // ==================================================================
    // CONTRAPONTO — Nenhum preserva fresh-flow (guard não bloqueia globalmente)
    // ==================================================================

    [Fact]
    public void Nenhum_RefreshNaoEhBloqueadoPeloRecovery()
    {
        RunSta(() =>
        {
            object form = CriarFormComContexto();
            var repo = new FakeRepo(); // zero candidatos → Nenhum
            ResolverEReceber(form, repo);

            Assert.Equal(ModalidadeRecuperacaoConsumo.Nenhum, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));

            // marca sentinela para detectar o caminho tomado pelo refresh.
            GetField<System.Windows.Forms.Control>(form, "statusLabel")!.Text = "SENTINELA_101W";
            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");

            // O guard de recovery NÃO engata: fresh-flow preservado.
            Assert.False((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            // Não materializa bloqueio de reconciliação (status não vira mensagem de recovery/bloqueio).
            string status = StatusText(form);
            Assert.DoesNotContain("reconciliação", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PENDENTE_SAP", status, StringComparison.OrdinalIgnoreCase);
            // Envio de recovery não acionável em Nenhum.
            Assert.Null((long?)Invoke(form, "PkEnvioRecuperadoAtual"));
        });
    }

    // ==================================================================
    // OUTRAS MODALIDADES — bloqueio sobrevive ao refresh (executado pela Form)
    // ==================================================================

    [Fact]
    public void AmbiguoPendente_RefreshMantemBloqueio()
    {
        RunSta(() =>
        {
            object form = CriarFormComContexto();
            var repo = new FakeRepo
            {
                Candidatos =
                [
                    new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" },
                    new() { CodigoLancamento = 9, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" }
                ],
                Detalhes =
                {
                    [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] },
                    [9] = new() { Codigo = 9, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] }
                }
            };
            ResolverEReceber(form, repo);
            Assert.Equal(ModalidadeRecuperacaoConsumo.AmbiguoPendente, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));

            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");

            Assert.True((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            Assert.False(ControleEnabled(form, "iniciarLeituraButton"));
            Assert.False(ControleVisible(form, "_enviarSap261Button"));  // ambíguo NÃO materializa envio
            Assert.Null((long?)Invoke(form, "PkEnvioRecuperadoAtual"));
        });
    }

    [Fact]
    public void EnviandoReconciliacao_RefreshMantemBloqueio()
    {
        RunSta(() =>
        {
            object form = CriarFormComContexto();
            var repo = new FakeRepo
            {
                Candidatos = [new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "ENVIANDO_SAP" }],
                Detalhes = { [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "ENVIANDO_SAP", Itens = [ItemPk8()] } }
            };
            ResolverEReceber(form, repo);
            Assert.Equal(ModalidadeRecuperacaoConsumo.EnviandoReconciliacao, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));

            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");

            Assert.True((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            Assert.False(ControleEnabled(form, "iniciarLeituraButton"));
            Assert.False(ControleVisible(form, "_enviarSap261Button"));
            Assert.Null((long?)Invoke(form, "PkEnvioRecuperadoAtual"));
        });
    }

    [Fact]
    public void FalhaResolucaoPersistida_RefreshMantemBloqueio()
    {
        RunSta(() =>
        {
            object form = CriarFormComContexto();
            var repo = new FakeRepo { Lancar = true }; // exceção → catch → FalhaResolucaoPersistida
            ResolverEReceber(form, repo);
            Assert.Equal(ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));

            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");

            Assert.True((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            Assert.False(ControleEnabled(form, "iniciarLeituraButton"));
            Assert.False(ControleVisible(form, "_enviarSap261Button"));
            Assert.Null((long?)Invoke(form, "PkEnvioRecuperadoAtual"));
        });
    }

    [Fact]
    public void ModalidadeDesconhecida_RefreshMantemBloqueio()
    {
        RunSta(() =>
        {
            object form = CriarFormComContexto();
            // valor de enum fora do domínio, injetado diretamente no estado da Form.
            SetField(form, "_modalidadeRecuperacao", (ModalidadeRecuperacaoConsumo)999);
            SetField(form, "_codigoLancamentoRecuperado", null);

            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");

            Assert.True((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            Assert.False(ControleEnabled(form, "iniciarLeituraButton"));
            Assert.Null((long?)Invoke(form, "PkEnvioRecuperadoAtual"));
        });
    }

    // ==================================================================
    // LIFECYCLE REAL — entrypoint runtime (ConsultarOrdemProducaoAsync), componente SEM lote (SAP Batch
    // pré-consumo). GATE 102B: reproduz OP1000170/0010/PK8 e prova que o recovery agora MATERIALIZA.
    // ==================================================================

    /// <summary>OP1000170 com o componente da operação 0010 SEM lote (batch vazio na reserva) — como o SAP
    /// entrega antes da pesagem. É este objeto que o código produtivo entrega ao resolver no caminho real.</summary>
    private static OrdemProducaoSap OrdemOp1000170SemLote()
        => new()
        {
            NumeroOrdem = "1000170",
            TipoOrdem = "PP01",
            MaterialProduzido = "PRODUTO-OP",
            Centro = "3007",
            QuantidadePrevista = 100m,
            Unidade = "KG",
            Deposito = "PP01",
            Liberada = true,
            Itens = [new ItemOrdemProducaoSap { ItemOrdem = "0001", Material = "PRODUTO-OP" }],
            Operacoes = [new OperacaoOrdemProducaoSap { Operacao = "0010", Sequencia = "0", CentroTrabalho = "MONT01" }],
            Componentes =
            [
                new ComponenteOrdemProducaoSap
                {
                    NumeroOrdem = "1000170",
                    Reserva = "185",
                    ItemReserva = "1",
                    Material = "1000186",
                    Centro = "3007",
                    Deposito = "PP01",
                    QuantidadeNecessaria = 2271.698m,
                    QuantidadeRetirada = 0m,
                    UnidadeBase = "KG",
                    TipoMovimento = "261",
                    Lote = string.Empty,           // ← incidente: batch VAZIO na reserva pré-consumo
                    Operacao = "0010",
                    SequenciaOperacao = "0"
                }
            ]
        };

    [Fact]
    public void LifecycleReal_ConsultarOrdem_ComponenteSemLote_MaterializaRecoveryPk8()
    {
        RunSta(() =>
        {
            var orderServico = new FakeOrderServico
            {
                Resultado = ResultadoConsultaOrdemProducaoSap.Encontrada(OrdemOp1000170SemLote())
            };
            var repo = new FakeRepo
            {
                Candidatos = [new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" }],
                Detalhes = { [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] } }
            };

            object form = CriarFormComContexto();

            // Injeta o serviço com OP fake + Product Master/Description FAKE (evita chamada de rede) e o
            // resolvedor sobre repo fake — sem banco/SAP real.
            var controller = new ProcessoConsumoMaterialController(CriarServicoFake(orderServico, repo));
            SetField(controller, "_consultaServico", new ConsumoMaterialConsultaServico(() => repo, CriarServicoFake(orderServico, repo)));
            SetField(form, "_controller", controller);

            // Save de sessão divergente (prova que o envio de recovery não o usa).
            SetField(form, "_ultimoCodigoLancamentoSalvo", 999L);

            // ENTRYPOINT REAL do runtime (o mesmo que o handler Shown chama após Retomar operação).
            Invoke(form, "ConsultarOrdemProducaoAsync", false);

            // O resolver foi alcançado pelo lifecycle e materializou UmPendente/PK8 (com componente SEM lote).
            Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));
            Assert.Equal(8L, GetField<long?>(form, "_codigoLancamentoRecuperado"));

            Assert.True((bool)Invoke(form, "RecuperacaoBloqueiaNovoConsumo")!);
            Assert.False(ControleEnabled(form, "iniciarLeituraButton"));
            Assert.False(ControleEnabled(form, "lerEtiquetaButton"));
            Assert.False(ControleEnabled(form, "leituraManualButton"));
            Assert.False(ControleEnabled(form, "_confirmarConsumoButton"));
            Assert.True(ControleVisible(form, "_enviarSap261Button"));

            string status = StatusText(form);
            Assert.Contains("PENDENTE_SAP", status, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Inicie a leitura", status, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(8L, (long?)Invoke(form, "PkEnvioRecuperadoAtual"));
            Assert.Equal(0, repo.Saves);
            Assert.Equal(0, repo.Reservas);
            Assert.Equal(0, repo.Confirmacoes);
        });
    }

    [Fact] // PROVA NEGATIVA: o matcher com lote OBRIGATÓRIO no componente (contrato pré-102B) rejeitaria o
           // componente sem lote — demonstra que era exatamente esse o defeito (recovery não materializava).
    public void ProvaNegativa_ComponenteSemLote_SeriaRejeitadoComLoteObrigatorio()
    {
        var itens = new List<ConsumoMaterialItem> { ItemPk8() };            // item persistido tem lote 0000000222
        var comp = ComponentesOcorrencia();
        comp[0].Lote = string.Empty;                                         // componente da reserva SEM lote (real)

        // Contrato ATUAL (102B): casa (lote é atributo do consumo, não da reserva).
        Assert.True(ConsumoMaterialConsultaServico.ItensCasamComComponentes(itens, comp));

        // Simula o contrato PRÉ-102B (lote obrigatório no componente): teria rejeitado → Nenhum → sem recovery.
        bool casaComLoteObrigatorio =
            !string.IsNullOrWhiteSpace(comp[0].Lote)
            && string.Equals(comp[0].Lote.Trim(), itens[0].Lote?.Trim(), StringComparison.Ordinal);
        Assert.False(casaComLoteObrigatorio);
    }

    // ==================================================================
    // GATE 102K-A — SUPERFÍCIE VISUAL do recovery (status card + apontamento) deve representar PENDENTE SAP.
    // ==================================================================

    private static string TextoControle(object form, string campo)
        => GetField<System.Windows.Forms.Control>(form, campo)?.Text ?? string.Empty;

    private static object PrepararFormComRecoveryUmPendente()
    {
        object form = CriarFormComContexto();
        var repo = new FakeRepo
        {
            Candidatos = [new() { CodigoLancamento = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP" }],
            Detalhes = { [8] = new() { Codigo = 8, NumeroOrdem = "1000170", StatusLancamento = "PENDENTE_SAP", Itens = [ItemPk8()] } }
        };
        ResolverEReceber(form, repo); // materializa UmPendente/8 via código produtivo real
        Assert.Equal(ModalidadeRecuperacaoConsumo.UmPendente, GetField<ModalidadeRecuperacaoConsumo>(form, "_modalidadeRecuperacao"));
        Assert.Equal(8L, GetField<long?>(form, "_codigoLancamentoRecuperado"));
        return form;
    }

    private static void AferirSuperficieRecovery(object form)
    {
        // Status card NÃO pode dominar como fresh-flow (INATIVA / "Leitura aguardando").
        string statusValue = TextoControle(form, "statusValueLabel");
        string statusHint = TextoControle(form, "statusHintLabel");
        Assert.DoesNotContain("INATIVA", statusValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PENDENTE", statusValue, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Leitura aguardando", statusHint, StringComparison.OrdinalIgnoreCase);

        // Orientação NÃO pode ser fresh-flow ("Selecione um componente" / "Pronto para leitura").
        string orientacao = TextoControle(form, "apontamentoInfoValueLabel");
        Assert.DoesNotContain("Selecione um componente", orientacao, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pronto para leitura", orientacao, StringComparison.OrdinalIgnoreCase);

        // statusLabel mantém a mensagem completa de recovery.
        Assert.Contains("PENDENTE_SAP", StatusText(form), StringComparison.OrdinalIgnoreCase);

        // Controles bloqueados; envio 261 visível.
        Assert.False(ControleEnabled(form, "iniciarLeituraButton"));
        Assert.False(ControleEnabled(form, "lerEtiquetaButton"));
        Assert.False(ControleEnabled(form, "leituraManualButton"));
        Assert.False(ControleEnabled(form, "_confirmarConsumoButton"));
        Assert.True(ControleVisible(form, "_enviarSap261Button"));
    }

    [Fact] // RED/GREEN: a superfície visual (status card + orientação) representa PENDENTE SAP, não fresh-flow.
    public void UmPendente_SuperficieVisual_RepresentaPendenteSap()
    {
        RunSta(() =>
        {
            object form = PrepararFormComRecoveryUmPendente();
            AferirSuperficieRecovery(form);
        });
    }

    [Fact] // GATE 102K-J: o statusHint contém a mensagem COMPLETA e o Label comporta visualmente as 2 linhas.
    public void UmPendente_StatusHint_ExibeMensagemCompleta()
    {
        RunSta(() =>
        {
            object form = PrepararFormComRecoveryUmPendente();

            var hint = GetField<System.Windows.Forms.Label>(form, "statusHintLabel")!;

            // STATUS_HINT_TEXT_HAS_FULL_MESSAGE
            Assert.Contains("Consumo já pesado e salvo.", hint.Text, StringComparison.Ordinal);
            Assert.Contains("Aguardando envio ao SAP.", hint.Text, StringComparison.Ordinal);

            // STATUS_HINT_VISUAL_CAPACITY_FOR_FULL_MESSAGE: a altura do label comporta o texto renderizado na sua largura.
            Assert.False(hint.AutoSize);
            Assert.False(hint.AutoEllipsis);
            System.Drawing.Size medido = System.Windows.Forms.TextRenderer.MeasureText(
                hint.Text, hint.Font, new System.Drawing.Size(hint.Width, int.MaxValue),
                System.Windows.Forms.TextFormatFlags.WordBreak | System.Windows.Forms.TextFormatFlags.TextBoxControl);
            Assert.True(hint.Height >= medido.Height, $"altura do hint {hint.Height} < necessário {medido.Height}");

            // Dentro dos limites do card (não extrapola/clipa).
            var card = GetField<System.Windows.Forms.Control>(form, "statusCard")!;
            Assert.True(hint.Right <= card.Width, $"hint.Right {hint.Right} > card.Width {card.Width}");
            Assert.True(hint.Bottom <= card.Height, $"hint.Bottom {hint.Bottom} > card.Height {card.Height}");

            // Demais elementos do modo recovery preservados.
            AferirSuperficieRecovery(form);
        });
    }

    [Fact] // §7/§11: a superfície visual de recovery é preservada após refreshes genéricos (idempotência).
    public void UmPendente_SuperficieVisual_PreservadaAposRefresh()
    {
        RunSta(() =>
        {
            object form = PrepararFormComRecoveryUmPendente();

            // refreshes genéricos que os eventos disparam no runtime real
            Invoke(form, "AtualizarComponenteSelecionadoDoGrid");
            Invoke(form, "AtualizarLiberacaoInicioLeitura");
            Invoke(form, "ReaplicarEstadoRecuperacao");

            AferirSuperficieRecovery(form);
        });
    }

    // ---------------- fake repo READ-ONLY (sem banco/SAP) ----------------

    private sealed class FakeOrderServico : IProductionOrderSapServico
    {
        public ResultadoConsultaOrdemProducaoSap Resultado { get; init; } = ResultadoConsultaOrdemProducaoSap.NaoEncontrada();
        public bool EhSimulado => false;
        public bool Configurado => true;
        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(Resultado);
    }

    // Product Master/Description FAKE: retornam null (sem enriquecimento, sem rede). O match do resolver usa
    // os campos de identidade vindos do componente da ordem, não do enriquecimento.
    private sealed class FakeDescricaoServico : IProductDescriptionSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;
        public Task<ProdutoSapMestre?> ObterDescricaoAsync(string codigoProduto, CancellationToken cancellationToken = default)
            => Task.FromResult<ProdutoSapMestre?>(null);
    }

    private sealed class FakeMasterServico : IProductMasterSapServico
    {
        public bool EhSimulado => true;
        public bool Configurado => true;
        public Task<ProdutoSapMestre?> ObterProdutoAsync(string codigoProduto, CancellationToken cancellationToken = default)
            => Task.FromResult<ProdutoSapMestre?>(null);
    }

    private static ConsumoMaterialServico CriarServicoFake(IProductionOrderSapServico orderServico, IConsumoMaterialRepositorio repo)
        => new(
            orderServico,
            () => repo,
            FugaPET_Dev.Servicos.IntegracaoSap.FabricaConsumoMaterialSap261Servico.Criar,
            () => new FugaPET_Dev.Servicos.IntegracaoSap.ConfirmacaoProducaoSapServico(),
            () => new FakeDescricaoServico(),
            () => new FakeMasterServico());


    private sealed class FakeRepo : IConsumoMaterialRepositorio
    {
        public IReadOnlyList<ResumoConsumoMaterialLancamento> Candidatos { get; init; } = [];
        public Dictionary<long, ConsumoMaterialLancamento> Detalhes { get; init; } = new();
        public bool Lancar { get; init; }
        public int Saves { get; private set; }
        public int Reservas { get; private set; }
        public int Confirmacoes { get; private set; }
        public int Falhas { get; private set; }

        public Task<long> SalvarConsumoLocalAsync(ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken = default)
        { Saves++; return Task.FromResult(1L); }

        public Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhes.TryGetValue(codigoLancamento, out ConsumoMaterialLancamento? l) ? l : null);

        public Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(ConsultaConsumoMaterialFiltro filtro, CancellationToken cancellationToken = default)
        {
            if (Lancar) throw new InvalidOperationException("falha simulada de consulta");
            return Task.FromResult(Candidatos);
        }

        public Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(long codigoLancamento, CancellationToken cancellationToken = default)
            => Task.FromResult(Detalhes.TryGetValue(codigoLancamento, out ConsumoMaterialLancamento? l) ? l : null);

        public Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, DateTime reservadoEmUtc, CancellationToken cancellationToken = default)
        { Reservas++; return Task.FromResult(false); }

        public Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
        { Falhas++; return Task.CompletedTask; }

        public Task MarcarConsumoConfirmadoSapAsync(long codigoLancamento, string? documentoMaterialSap, string? exercicioDocumentoMaterialSap, DateTime enviadoSapEmUtc, CancellationToken cancellationToken = default)
        { Confirmacoes++; return Task.CompletedTask; }
    }
}
