using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// GATE 103V — PROVA COMPORTAMENTAL (não source-scan) da ligação Controle de Apontamentos → Semi-Acabado.
/// Instancia <see cref="ProcessoSemiAcabadoForm"/> REAL em thread STA (sem banco/SAP: não chama Shown, que
/// dispara a consulta da OP), exercitando o CÓDIGO PRODUTIVO do construtor com contexto, do binding da OP
/// autoritativa (AplicarContextoApontamentoSemiAcabado) e do contrato de resultado devolvido ao Controle.
/// </summary>
public sealed class ProcessoSemiAcabadoContextBinding103VTests
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

    private static FieldInfo ObterCampo(Type? tipo, string nome)
    {
        for (Type? t = tipo; t is not null; t = t.BaseType)
        {
            FieldInfo? f = t.GetField(nome, Priv);
            if (f is not null) return f;
        }

        throw new MissingFieldException(tipo?.FullName, nome);
    }

    private static T? GetField<T>(object alvo, string nome)
        => (T?)ObterCampo(alvo.GetType(), nome).GetValue(alvo);

    private static object? Invoke(object alvo, string metodo, params object?[] args)
    {
        for (Type? t = alvo.GetType(); t is not null; t = t.BaseType)
        {
            MethodInfo? m = t.GetMethod(metodo, Priv);
            if (m is not null) return m.Invoke(alvo, args);
        }

        throw new MissingMethodException(alvo.GetType().FullName, metodo);
    }

    private static Control ControlePorNome(object form, string campo)
        => GetField<Control>(form, campo)!;

    private static ContextoApontamentoProcesso Contexto0140()
        => new()
        {
            CodigoApontamento = 555,
            NumeroOrdem = "1000173",
            Operacao = "0140",
            Sequencia = "0",
            DescricaoOperacao = "GERAR ESTOQUE",
            CentroTrabalho = "3007043",
            TipoProcesso = TipoProcessoOperacao.SemiAcabado
        };

    // §14.D + §14.F — construtor com contexto existe e o manual continua existente.
    [Fact]
    public void ConstrutorComContexto_DefineContextoApontamento()
    {
        RunSta(() =>
        {
            using ProcessoSemiAcabadoForm form = new(Contexto0140());
            ContextoApontamentoProcesso? ctx = GetField<ContextoApontamentoProcesso>(form, "_contextoApontamento");
            Assert.NotNull(ctx);
            Assert.Equal("1000173", ctx!.NumeroOrdem);
        });
    }

    [Fact]
    public void ConstrutorManual_ContinuaExistente_SemContexto()
    {
        RunSta(() =>
        {
            using ProcessoSemiAcabadoForm form = new();
            Assert.Null(GetField<ContextoApontamentoProcesso>(form, "_contextoApontamento"));
            // §11: seletor de OP permanece livre no fluxo manual.
            Assert.True(ControlePorNome(form, "pedidoComboBox").Enabled);
        });
    }

    // §14.G — resultado inicia SEMPRE NaoConcluido (em ambos os fluxos).
    [Fact]
    public void ResultadoExecucaoApontamento_IniciaNaoConcluido_NoContexto()
    {
        RunSta(() =>
        {
            using ProcessoSemiAcabadoForm form = new(Contexto0140());
            ResultadoExecucaoProcesso r = form.ResultadoExecucaoApontamento;
            Assert.Same(ResultadoExecucaoProcesso.NaoConcluido, r);
            Assert.False(r.AtividadeConcluida);
        });
    }

    [Fact]
    public void ResultadoExecucaoApontamento_IniciaNaoConcluido_NoManual()
    {
        RunSta(() =>
        {
            using ProcessoSemiAcabadoForm form = new();
            Assert.Same(ResultadoExecucaoProcesso.NaoConcluido, form.ResultadoExecucaoApontamento);
        });
    }

    // §14.E + §7 — a OP vem do contexto e o operador NÃO pode trocar de OP no modo apontamento.
    [Fact]
    public void AplicarContexto_PreencheOpDoContextoEBloqueiaTroca()
    {
        RunSta(() =>
        {
            using ProcessoSemiAcabadoForm form = new(Contexto0140());
            Invoke(form, "AplicarContextoApontamentoSemiAcabado");

            Control combo = ControlePorNome(form, "pedidoComboBox");
            Assert.Equal("1000173", combo.Text);
            Assert.False(combo.Enabled); // OP autoritativa: seletor bloqueado no contexto.
        });
    }
}
