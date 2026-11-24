using Xunit;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// A GUARDA PRECISA TER PORTA DE SAÍDA, E PRECISA ESTAR EM TODOS OS CAMINHOS.
/// <para>
/// Em 24/11/2026 o usuário pesou um material sem tolerância cadastrada e ficou preso na tela: a
/// finalização recusava, a tela só se fecha com a leitura encerrada, e pesagem em memória não tem
/// como ser excluída (a exclusão só alcança pesagem persistida). Saiu matando o processo pelo
/// Gerenciador de Tarefas.
/// </para>
/// <para>
/// Dois defeitos distintos vieram desse teste, e cada um tem seu caso aqui:
/// <list type="number">
/// <item>a janela de pesagens registrava peso SEM passar pela tolerância — um terceiro caminho de
/// pesagem, o único sem a trava, e foi por ele que o peso entrou;</item>
/// <item>bloqueado, não havia saída nenhuma: nem descartar, nem fechar a tela.</item>
/// </list>
/// </para>
/// </summary>
public sealed class ToleranciaSaidaDoBloqueioTests
{
    private static string Form() => File.ReadAllText(
        Path.Combine(RaizProjeto(), "Tela", "Processo", "ProcessoEntradaProdutoForm.cs"));

    /// <summary>
    /// O callback de registro da janela de pesagens passa pela tolerância. A âncora é a chamada ao
    /// controlador SEM verificação, que era a forma antiga e não pode voltar.
    /// </summary>
    [Fact]
    public void JanelaDePesagens_NaoRegistraDireto_SemPassarPelaTolerancia()
    {
        string form = Form();

        Assert.DoesNotContain(
            "(peso, origem, leitura) => Task.FromResult(_controller.RegistrarPesagemOperacaoComLotes(",
            form,
            StringComparison.Ordinal);

        Assert.Contains("PesagemBloqueadaPorToleranciaException", form, StringComparison.Ordinal);
    }

    /// <summary>
    /// TODA chamada ao registro de pesagem do controlador vem depois de uma verificação de
    /// tolerância. Este é o teste que teria pego o defeito: a trava existia, mas só num caminho.
    /// </summary>
    [Fact]
    public void TodoCaminhoDePesagem_VerificaAToleranciaAntes()
    {
        string form = Form();

        const string registro = "_controller.RegistrarPesagemOperacaoComLotes(";
        const string trava = "ToleranciaLiberaPesagemAsync(";

        int registros = ContarOcorrencias(form, registro);
        Assert.True(registros > 0, "nenhuma chamada de registro encontrada - ancora errada");

        // Cada registro tem uma trava ANTES dele, no mesmo trecho de codigo.
        int posicao = 0;
        for (int i = 0; i < registros; i++)
        {
            int ondeRegistra = form.IndexOf(registro, posicao, StringComparison.Ordinal);
            int ondeTrava = form.LastIndexOf(trava, ondeRegistra, StringComparison.Ordinal);

            Assert.True(
                ondeTrava >= 0 && ondeRegistra - ondeTrava < 2000,
                $"registro de pesagem na posicao {ondeRegistra} sem trava de tolerancia imediatamente antes");

            posicao = ondeRegistra + registro.Length;
        }
    }

    /// <summary>Bloqueado, o operador pode descartar as pesagens em memória e encerrar a leitura.</summary>
    [Fact]
    public void BloqueioDeTolerancia_OfereceDescarteEEncerramento()
    {
        string form = Form();

        Assert.Contains("OferecerDescarteDaLeituraBloqueada", form, StringComparison.Ordinal);
        // A caixa de aviso seca, sem saida, era a forma antiga.
        Assert.DoesNotContain(
            "MessageBox.Show(this, mensagem, \"Tolerância de recebimento\",",
            form,
            StringComparison.Ordinal);
        // O descarte usa a primitiva que NAO toca em nada persistido.
        Assert.Contains("_controller.LimparOperacaoComLotes();", form, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tela pode ser fechada quando nada está gravado nem gravando. Os dois casos com estado real
    /// em risco continuam barrados — esses não são escolha do operador.
    /// </summary>
    [Fact]
    public void Fechamento_NaoExigeFinalizarQuandoNadaFoiGravado()
    {
        string form = Form();

        Assert.Contains(
            "if (_isProductionStarted && !_finalizandoPesagem && !_lotesFinalizadosAguardandoPersistencia)",
            form,
            StringComparison.Ordinal);
        Assert.Contains("Sair do recebimento", form, StringComparison.Ordinal);
        Assert.Contains("Gerenciador de Tarefas", form, StringComparison.Ordinal);
    }

    private static int ContarOcorrencias(string texto, string agulha)
    {
        int total = 0, i = 0;
        while ((i = texto.IndexOf(agulha, i, StringComparison.Ordinal)) >= 0) { total++; i += agulha.Length; }
        return total;
    }

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do projeto nao encontrada.");
    }
}
