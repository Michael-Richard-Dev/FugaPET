using System.Reflection;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 103D — PROVA COMPORTAMENTAL (não source-scan) do gate de MODO em
/// <see cref="ProcessoConsumoMaterialForm"/> (método privado <c>ComponentePodeOperar</c>).
///
/// Contradição reproduzida (OP1000173 / operação 0060 / CONSUMO_QUIMICOS):
/// os componentes da operação 0060 aparecem no grid como "261 Direto" (coluna vem de
/// <c>ClassificacaoEnvio</c>), mas INICIAR LEITURA fica bloqueado porque
/// <c>ComponentePodeOperar</c> re-aplicava <c>ComponentePertenceAoModoAtual</c>
/// (exige <c>ClassificacaoConsumo == Quimico</c>) mesmo no fluxo de APONTAMENTO,
/// onde a operação configurada já é autoridade para a tela.
///
/// Regra congelada:
///  - APONTAMENTO (_contextoApontamento != null): NÃO re-bloquear por ClassificacaoConsumo/ProductType.
///  - MANUAL (_contextoApontamento == null): filtro de modo PERMANECE obrigatório.
///  - Todos os demais gates operacionais permanecem em AMBOS os fluxos.
/// </summary>
public sealed class ConsumoApontamentoModeGate103DTests
{
    private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

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

    private static ContextoApontamentoProcesso ContextoOp1000173_0060()
        => new()
        {
            NumeroOrdem = "1000173",
            Operacao = "0060",
            Sequencia = "0",
            TipoProcesso = "CONSUMO_QUIMICOS"
        };

    /// <summary>
    /// Componente da operação 0060 que satisfaz TODOS os gates operacionais (261 Direto elegível,
    /// depósito/lote presentes, saldo positivo, unidade KG, pesagem liberada). Apenas a
    /// <paramref name="classe"/> (ProductType/ClassificacaoConsumo) varia — é o eixo do gate de modo.
    /// </summary>
    private static ComponenteConsumoMaterial ComponenteValido0060(ClassificacaoConsumoMaterial classe)
        => new()
        {
            CodigoMaterial = "1000186",
            Operacao = "0060",
            NumeroReserva = "185",
            ItemReserva = "1",
            DepositoConsumo = "PP01",
            Lote = "0000000222",
            TipoMovimento = "261",
            UnidadeMedida = "KG",
            QuantidadePendente = 2271.698m,
            PesagemLiberada = true,
            BackflushSap = false,
            ElegivelMaterialDocument261Direto = true,
            ClassificacaoEnvio = ClassificacaoEnvioConsumo261.MaterialDocument261Direto,
            ClassificacaoConsumo = classe
        };

    private static (bool operavel, string motivo) AvaliarOperabilidade(object form, ComponenteConsumoMaterial componente)
    {
        MethodInfo m = form.GetType().GetMethod("ComponentePodeOperar", Priv)
            ?? throw new MissingMethodException(form.GetType().FullName, "ComponentePodeOperar");
        object?[] args = [componente, null];
        bool ok = (bool)m.Invoke(form, args)!;
        return (ok, (string)(args[1] ?? string.Empty));
    }

    private static string TipoSapGrid(ComponenteConsumoMaterial componente)
    {
        MethodInfo m = typeof(ProcessoConsumoMaterialForm)
            .GetMethod("ObterTipoSapGrid", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string)m.Invoke(null, [componente])!;
    }

    private static object CriarFormApontamentoQuimicos()
        => new ProcessoConsumoMaterialForm(ModoConsumoMaterial.Quimico, ContextoOp1000173_0060());

    private static object CriarFormManualQuimicos()
        => new ProcessoConsumoMaterialForm(ModoConsumoMaterial.Quimico);

    // ==================================================================
    // §2 — Premissa: os 5 componentes aparecem como "261 Direto" no grid,
    //      INDEPENDENTE da ClassificacaoConsumo (a coluna vem de ClassificacaoEnvio).
    // ==================================================================

    [Theory]
    [InlineData(ClassificacaoConsumoMaterial.Quimico)]
    [InlineData(ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData(ClassificacaoConsumoMaterial.Outro)]
    [InlineData(ClassificacaoConsumoMaterial.Indefinido)]
    public void Grid_0060_MostraSempre261Direto_IndependenteDaClassificacaoDeModo(ClassificacaoConsumoMaterial classe)
    {
        Assert.Equal("261 Direto", TipoSapGrid(ComponenteValido0060(classe)));
    }

    // ==================================================================
    // §6 RED→GREEN — APONTAMENTO: todos os componentes 0060 são OPERÁVEIS
    //      quando os demais gates são válidos, qualquer que seja a classificação de modo.
    //      (RED antes do fix: MateriaPrima/Outro/Indefinido falhavam SÓ pelo gate de modo.)
    // ==================================================================

    [Theory]
    [InlineData(ClassificacaoConsumoMaterial.Quimico)]
    [InlineData(ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData(ClassificacaoConsumoMaterial.Outro)]
    [InlineData(ClassificacaoConsumoMaterial.Indefinido)]
    public void Apontamento_Quimicos_0060_NaoBloqueiaPorClassificacaoDeModo(ClassificacaoConsumoMaterial classe)
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            (bool operavel, string motivo) = AvaliarOperabilidade(form, ComponenteValido0060(classe));

            Assert.True(operavel, $"Esperado OPERÁVEL no apontamento para {classe}; motivo do bloqueio: '{motivo}'.");
            Assert.DoesNotContain("não pertence ao modo", motivo, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ==================================================================
    // §7 — MANUAL: o filtro de modo PERMANECE obrigatório (regressão obrigatória).
    // ==================================================================

    [Fact]
    public void Manual_Quimicos_Quimico_Operavel()
    {
        RunSta(() =>
        {
            object form = CriarFormManualQuimicos();
            (bool operavel, _) = AvaliarOperabilidade(form, ComponenteValido0060(ClassificacaoConsumoMaterial.Quimico));
            Assert.True(operavel);
        });
    }

    [Theory]
    [InlineData(ClassificacaoConsumoMaterial.MateriaPrima)]
    [InlineData(ClassificacaoConsumoMaterial.Outro)]
    [InlineData(ClassificacaoConsumoMaterial.Indefinido)]
    public void Manual_Quimicos_NaoQuimico_BloqueadoPeloModo(ClassificacaoConsumoMaterial classe)
    {
        RunSta(() =>
        {
            object form = CriarFormManualQuimicos();
            (bool operavel, string motivo) = AvaliarOperabilidade(form, ComponenteValido0060(classe));

            Assert.False(operavel);
            Assert.Contains("não pertence ao modo", motivo, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ==================================================================
    // §8 — NEGATIVE TESTS: mesmo no APONTAMENTO, os demais gates continuam bloqueando.
    //      (Provam que o fix NÃO libera cegamente o componente.)
    // ==================================================================

    [Fact]
    public void Apontamento_SemDeposito_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.DepositoConsumo = string.Empty;
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }

    [Fact]
    public void Apontamento_SemLote_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.Lote = string.Empty;
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }

    [Fact]
    public void Apontamento_Backflush_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.BackflushSap = true;
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }

    [Fact]
    public void Apontamento_Nao261Direto_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.ClassificacaoEnvio = ClassificacaoEnvioConsumo261.RequerConfirmacaoProducao;
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }

    [Fact]
    public void Apontamento_SaldoZero_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.QuantidadePendente = 0m;
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }

    [Fact]
    public void Apontamento_UnidadeNaoKg_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.UnidadeMedida = "L";
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }

    [Fact]
    public void Apontamento_PesagemNaoLiberada_ContinuaBloqueado()
    {
        RunSta(() =>
        {
            object form = CriarFormApontamentoQuimicos();
            ComponenteConsumoMaterial c = ComponenteValido0060(ClassificacaoConsumoMaterial.MateriaPrima);
            c.PesagemLiberada = false;
            Assert.False(AvaliarOperabilidade(form, c).operavel);
        });
    }
}
