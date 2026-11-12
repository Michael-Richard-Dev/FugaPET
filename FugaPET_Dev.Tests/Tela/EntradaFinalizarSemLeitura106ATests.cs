using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Operacao;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 106A — Parar/Finalizar SEM nenhuma leitura registrada é CANCELAMENTO da tentativa de pesagem
/// em memória, não finalização. Antes, o fluxo bloqueava exigindo pesagem válida
/// (FinalizarLotesAtivosEmMemoria / "não possui pesagem válida para finalizar").
///
/// Provas COMPORTAMENTAIS em STA sobre o Form REAL, com o seam de persistência instrumentado para
/// comprovar ZERO gravação. Sem banco, sem SAP.
/// </summary>
public sealed class EntradaFinalizarSemLeitura106ATests
{
    // ---------------- infraestrutura (espelha o harness de lotes existente) ----------------

    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static void ExecutarEmSta(Action acao)
    {
        Exception? erro = null;
        Thread thread = new(() =>
        {
            try { acao(); }
            catch (Exception ex) { erro = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (erro is not null) throw erro;
    }

    private static FieldInfo ObterCampo(Type? tipo, string nome)
    {
        for (Type? t = tipo; t is not null; t = t.BaseType)
        {
            FieldInfo? f = t.GetField(nome, Priv);
            if (f is not null) return f;
        }

        throw new MissingFieldException(tipo?.FullName, nome);
    }

    private static T Campo<T>(object alvo, string nome) => (T)ObterCampo(alvo.GetType(), nome).GetValue(alvo)!;
    private static void DefinirCampo(object alvo, string nome, object? valor)
        => ObterCampo(alvo.GetType(), nome).SetValue(alvo, valor);

    private static T Controle<T>(object alvo, string nome) where T : Control => Campo<T>(alvo, nome);

    private static object? Invocar(object alvo, string nome, params object?[] args)
    {
        for (Type? t = alvo.GetType(); t is not null; t = t.BaseType)
        {
            MethodInfo? m = t.GetMethod(nome, Priv);
            if (m is not null) return m.Invoke(alvo, args);
        }

        throw new MissingMethodException(alvo.GetType().FullName, nome);
    }

    private static T Invocar<T>(object alvo, string nome, params object?[] args) => (T)Invocar(alvo, nome, args)!;

    private static void AguardarTask(object? possivelTask)
    {
        if (possivelTask is Task tarefa) tarefa.GetAwaiter().GetResult();
    }

    /// <summary>Executa o fluxo REAL do botão Parar (caminho testável, sem async void).</summary>
    private static void ExecutarParar(ProcessoEntradaProdutoForm form)
        => AguardarTask(Invocar(form, "ExecutarPersistenciaLotesAsync"));

    private static PedidoCompraSapItem CriarItem(long codigoItem, string numeroItem, string material)
        => new()
        {
            CodigoItem = codigoItem,
            NumeroItem = numeroItem,
            CodigoMaterial = material,
            Descricao = $"Material {material}",
            Quantidade = 10m,
            UnidadeMedida = "KG",
            PesoItem = 10m,
            Centro = "3007",
            Deposito = "PP01",
            TipoMaterialSap = "ROH",
            ClassificacaoEntrada = ClassificacaoEntradaMaterial.MateriaPrima
        };

    private static ProcessoEntradaProdutoForm CriarFormComPedido(params PedidoCompraSapItem[] itens)
    {
        EntradaProdutoController controller = new();
        ProcessoEntradaProdutoForm form = new(
            controller,
            ModoEntradaMaterial.MateriaPrima,
            (_, _) => new DadosLoteEntrada("LOTE-TESTE", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31)));

        DefinirCampo(form, "_idSetorSelecionado", 1L);
        DefinirCampo(form, "_numeroPedidoCarregado", "4500000001");
        DefinirCampo(form, "_itensPedidoCarregados", itens.ToList());

        Dictionary<long, PedidoCompraSapItem> porCodigo =
            Campo<Dictionary<long, PedidoCompraSapItem>>(form, "_itensCarregadosPorCodigo");
        porCodigo.Clear();
        foreach (PedidoCompraSapItem item in itens) porCodigo[item.CodigoItem] = item;

        Controle<ComboBox>(form, "pedidoComboBox").Text = "4500000001";
        Controle<TextBox>(form, "lotTextBox").Text = "FORN-A";

        DataGridView grid = Controle<DataGridView>(form, "productionDataGridView");
        grid.Rows.Clear();
        foreach (PedidoCompraSapItem item in itens)
        {
            DataGridViewRow row = grid.Rows[grid.Rows.Add()];
            row.Cells["productionCodeColumn"].Value = item.CodigoMaterial ?? string.Empty;
            row.Cells["productionProductColumn"].Value = item.Descricao ?? string.Empty;
            row.Cells["productionWeightColumn"].Value = item.UnidadeMedida ?? string.Empty;
            row.Cells["productionPesoLidoColumn"].Value = string.Empty;
            row.Cells["productionItemIdColumn"].Value = item.CodigoItem.ToString();
            row.Cells["productionPesoOrigemColumn"].Value = string.Empty;
            row.Tag = new TaraCadastro { CodigoTara = 1, NomeTara = "Sem tara", PesoKg = 0m, SituacaoTara = true };
        }

        return form;
    }

    /// <summary>Instrumenta os seams de persistência e de SAP para provar que NÃO são chamados.</summary>
    private sealed class Sondas
    {
        public int Persistencias { get; set; }
        public int DiagnosticosSap { get; set; }
    }

    private static Sondas InstrumentarSeams(ProcessoEntradaProdutoForm form)
    {
        Sondas sondas = new();

        DefinirCampo(
            form,
            "_registrarOuRecuperarLancamentoComLotes",
            (Func<EntradaProdutoLancamentoComLotesPersistencia, CancellationToken,
                Task<ResultadoPersistenciaEntradaComLotes>>)((_, _) =>
            {
                sondas.Persistencias++;
                return Task.FromResult(ResultadoPersistenciaEntradaComLotes.Criar(
                    999,
                    new Dictionary<string, long> { ["00010"] = 1 },
                    new Dictionary<Guid, long>(),
                    new Dictionary<Guid, long>(),
                    new Dictionary<Guid, decimal>()));
            }));

        DefinirCampo(
            form,
            "_diagnosticarEnvioSapEntrada",
            (Func<long?, CancellationToken, Task<DiagnosticoEnvioSapEntrada>>)((codigo, _) =>
            {
                sondas.DiagnosticosSap++;
                return Task.FromResult(new DiagnosticoEnvioSapEntrada { CodigoLancamento = codigo });
            }));

        return sondas;
    }

    /// <summary>Inicia a operação por lotes com a leitura ativa (equivalente a "Iniciar leitura").</summary>
    private static EntradaProdutoController IniciarLeitura(ProcessoEntradaProdutoForm form)
    {
        EntradaProdutoController controller = Campo<EntradaProdutoController>(form, "_controller");
        Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
        DefinirCampo(form, "_isProductionStarted", true);
        return controller;
    }

    private static int TotalPesagensValidas(EntradaProdutoController controller)
        => controller.ObterEstadoOperacaoComLotes().Itens
            .SelectMany(item => item.Lotes)
            .Sum(lote => lote.QuantidadePesagensValidas);

    // ==================================================================
    // A — operação iniciada, ZERO lote, ZERO pesagem, Parar ⇒ cancela em memória
    // ==================================================================

    [Fact]
    public void A_ZeroLoteZeroPesagem_Parar_CancelaEmMemoriaSemPersistir()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);

            ExecutarParar(form);

            Assert.False(Campo<bool>(form, "_isProductionStarted"));           // sessão encerrada
            Assert.False(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada); // operação descartada
            Assert.Equal(0, sondas.Persistencias);                              // sem persistência
            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));     // sem lançamento
            Assert.Equal(
                ProcessoEntradaProdutoForm.MensagemPesagemEncerradaSemLeituras,
                Controle<Label>(form, "statusLabel").Text);
        });
    }

    // ==================================================================
    // B — lote confirmado SEM pesagem ⇒ lote temporário descartado, sem persistência
    // ==================================================================

    [Fact]
    public void B_LoteConfirmadoSemPesagem_Parar_DescartaLoteTemporario()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);
            controller.ConfirmarLoteOperacaoComLotes(
                101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));

            // Pré-condição: existe lote em memória, mas nenhuma pesagem válida.
            Assert.NotEmpty(controller.ObterEstadoOperacaoComLotes().Itens.SelectMany(i => i.Lotes));
            Assert.Equal(0, TotalPesagensValidas(controller));

            ExecutarParar(form);

            Assert.False(Campo<bool>(form, "_isProductionStarted"));
            Assert.False(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.Empty(controller.ObterEstadoOperacaoComLotes().Itens.SelectMany(i => i.Lotes));
            Assert.Equal(0, sondas.Persistencias);
        });
    }

    // ==================================================================
    // C / D / E / F — invariantes do cancelamento vazio
    // ==================================================================

    [Fact]
    public void C_ZeroPesagem_CodigoLancamentoContinuaNulo()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            InstrumentarSeams(form);
            IniciarLeitura(form);

            ExecutarParar(form);

            Assert.Null(Campo<long?>(form, "_codigoLancamentoPersistido"));
        });
    }

    [Fact]
    public void D_ZeroPesagem_NaoEntraEmAguardandoPersistencia()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            InstrumentarSeams(form);
            IniciarLeitura(form);

            ExecutarParar(form);

            Assert.False(Campo<bool>(form, "_lotesFinalizadosAguardandoPersistencia"));
            Assert.False(Campo<bool>(form, "_finalizandoPesagem"));
        });
    }

    [Fact]
    public void E_ZeroPesagem_NaoChamaSeamDePersistencia()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            IniciarLeitura(form);

            ExecutarParar(form);

            Assert.Equal(0, sondas.Persistencias); // DB_WRITE = 0
        });
    }

    [Fact]
    public void F_ZeroPesagem_NaoChamaDiagnosticoSap()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            IniciarLeitura(form);

            // Caminho completo do botão Parar (persistência + reavaliação de prontidão).
            AguardarTask(Invocar(form, "FinalizarPersistenciaEAtualizarProntidaoSapAsync"));

            Assert.Equal(0, sondas.Persistencias);
            Assert.Equal(0, sondas.DiagnosticosSap); // ZERO SAP
        });
    }

    // ==================================================================
    // G — pedido permanece carregado e a leitura pode ser reiniciada
    // ==================================================================

    [Fact]
    public void G_AposCancelar_PedidoSegueCarregadoEPodeIniciarNovamente()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            InstrumentarSeams(form);
            IniciarLeitura(form);

            ExecutarParar(form);

            // Pedido/itens preservados.
            Assert.Equal("4500000001", Campo<string>(form, "_numeroPedidoCarregado"));
            Assert.NotEmpty(Campo<List<PedidoCompraSapItem>>(form, "_itensPedidoCarregados"));
            Assert.NotEmpty(Controle<DataGridView>(form, "productionDataGridView").Rows);

            // Nova tentativa é possível.
            Assert.True(Invocar<bool>(form, "GarantirOperacaoLotesIniciada"));
        });
    }

    // ==================================================================
    // H — com 1 pesagem válida o fluxo normal permanece (não cancela)
    // ==================================================================

    [Fact]
    public void H_UmaPesagemValida_NaoCancela_SeguiFluxoNormalDePersistencia()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);
            controller.ConfirmarLoteOperacaoComLotes(
                101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(
                101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);

            Assert.False(Invocar<bool>(form, "OperacaoSemQualquerPesagemValida"));

            ExecutarParar(form);

            Assert.Equal(1, sondas.Persistencias);                          // persistiu normalmente
            Assert.Equal(999L, Campo<long?>(form, "_codigoLancamentoPersistido"));
            Assert.NotEqual(
                ProcessoEntradaProdutoForm.MensagemPesagemEncerradaSemLeituras,
                Controle<Label>(form, "statusLabel").Text);
        });
    }

    // ==================================================================
    // I — CENÁRIO MISTO: leitura válida em outro item + lote ativo vazio ⇒ não limpa nada
    // ==================================================================

    [Fact]
    public void I_CenarioMisto_NaoCancelaOperacaoNemPerdePesagem()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(
                CriarItem(101, "10", "MAT-001"),
                CriarItem(202, "20", "MAT-002"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);

            // Item 101: lote COM pesagem válida.
            controller.ConfirmarLoteOperacaoComLotes(
                101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(
                101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);

            // Item 202: lote ativo SEM pesagem.
            controller.ConfirmarLoteOperacaoComLotes(
                202, "LOTE-B", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));

            Assert.False(Invocar<bool>(form, "OperacaoSemQualquerPesagemValida")); // zero NÃO é global

            ExecutarParar(form);

            // Não cancelou a sessão inteira e não descartou a leitura do item 101.
            Assert.Equal(0, sondas.Persistencias); // bloqueio do lote incompleto (proteção existente)
            Assert.True(Campo<bool>(form, "_isProductionStarted"));
            Assert.Equal(1, TotalPesagensValidas(controller));
            Assert.NotEqual(
                ProcessoEntradaProdutoForm.MensagemPesagemEncerradaSemLeituras,
                Controle<Label>(form, "statusLabel").Text);
        });
    }

    // ==================================================================
    // J — leitura de balança em andamento continua bloqueando
    // ==================================================================

    [Fact]
    public void J_LeituraEmAndamento_NaoCancelaNemLimpaOperacao()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);
            DefinirCampo(form, "_isReadingWeight", true);

            ExecutarParar(form);

            // Proteção existente preservada: nada foi encerrado nem limpo.
            Assert.True(Campo<bool>(form, "_isProductionStarted"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.Equal(0, sondas.Persistencias);
            Assert.NotEqual(
                ProcessoEntradaProdutoForm.MensagemPesagemEncerradaSemLeituras,
                Controle<Label>(form, "statusLabel").Text);
        });
    }

    // ==================================================================
    // K — retry de persistência nunca é tratado como cancelamento vazio
    // ==================================================================

    [Fact]
    public void K_RetryPendente_NaoEhTratadoComoCancelamentoVazio()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);
            controller.ConfirmarLoteOperacaoComLotes(
                101, "LOTE-A", new DateTime(2026, 7, 1), new DateTime(2026, 12, 31));
            controller.RegistrarPesagemOperacaoComLotes(
                101, 2m, 0m, 1, EntradaProdutoPesagemCalculos.OrigemManual, null, "2", DateTimeOffset.Now);

            // Simula retry pendente após falha de gravação.
            DefinirCampo(form, "_lotesFinalizadosAguardandoPersistencia", true);

            ExecutarParar(form);

            // Retry executou a persistência (não virou cancelamento silencioso).
            Assert.Equal(1, sondas.Persistencias);
            Assert.NotEqual(
                ProcessoEntradaProdutoForm.MensagemPesagemEncerradaSemLeituras,
                Controle<Label>(form, "statusLabel").Text);
        });
    }

    // ==================================================================
    // L — peso visual positivo SEM leitura rastreável continua protegido
    // ==================================================================

    [Fact]
    public void L_PesoVisualSemLeituraRastreavel_NaoCancelaSilenciosamente()
    {
        ExecutarEmSta(() =>
        {
            using ProcessoEntradaProdutoForm form = CriarFormComPedido(CriarItem(101, "10", "MAT-001"));
            Sondas sondas = InstrumentarSeams(form);
            EntradaProdutoController controller = IniciarLeitura(form);

            // Peso visível na grade sem qualquer leitura rastreável vinculada.
            DataGridView grid = Controle<DataGridView>(form, "productionDataGridView");
            grid.Rows[0].Cells["productionPesoLidoColumn"].Value = "5,000";
            Assert.True(Invocar<bool>(form, "ExistePesoVisualSemLeituraRastreavel"));
            Assert.True(Invocar<bool>(form, "OperacaoSemQualquerPesagemValida"));

            ExecutarParar(form);

            // NÃO cancelou silenciosamente: operação preservada e sem persistência.
            Assert.True(Campo<bool>(form, "_isProductionStarted"));
            Assert.True(controller.ObterEstadoOperacaoComLotes().OperacaoIniciada);
            Assert.Equal(0, sondas.Persistencias);
            Assert.NotEqual(
                ProcessoEntradaProdutoForm.MensagemPesagemEncerradaSemLeituras,
                Controle<Label>(form, "statusLabel").Text);
        });
    }
}
