using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela.Comum;
using Xunit;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// O REALCE DOS BOTOES DE JANELA EM TODAS AS TELAS (pedido do usuario em 25/11/2026).
/// <para>
/// "Tem esse efeito que esta funcionando na tela inicial, com os botoes de minimizar, maximizar e
/// sair, quero que coloque esse efeito em todas as telas possiveis; a tela de cadastro de setor e
/// uma das que nao funciona."
/// </para>
/// <para>
/// CAUSA: o metodo do realce vivia como COPIA PRIVADA em cada Form — e nove telas nunca receberam
/// a copia. Cada copia ainda recebia a cor como literal, entao mesmo as que tinham podiam divergir.
/// </para>
/// <para>
/// Os testes DISPARAM o MouseEnter de verdade e olham a cor que o botao assume. Auditar o fonte
/// diria que a chamada existe, nao que o botao muda de cor.
/// </para>
/// </summary>
public sealed class BarraDeTituloTests
{
    private static readonly Color CabecalhoLaranja = Color.FromArgb(200, 78, 10);
    private static readonly Color CabecalhoEscuro = Color.FromArgb(15, 23, 42);

    // ------------------------------------------------- a regra da cor

    /// <summary>
    /// O cabecalho LARANJA escurece, e com exatamente o tom da tela inicial — a que o usuario
    /// apontou como referencia. "Mesmo aspecto" nao e "parecido".
    /// </summary>
    [Fact]
    public void CabecalhoLaranja_UsaORealceDaTelaInicial()
        => Assert.Equal(
            Color.FromArgb(160, 55, 5).ToArgb(),
            BarraDeTituloHelper.RealceParaCabecalho(CabecalhoLaranja).ToArgb());

    /// <summary>
    /// O cabecalho ESCURO CLAREIA, com o tom que as telas de consulta ja usam. Escurecer um
    /// cabecalho que ja e quase preto nao produziria realce visivel nenhum.
    /// </summary>
    [Fact]
    public void CabecalhoEscuro_Clareia_ComOTomDasConsultas()
    {
        Color realce = BarraDeTituloHelper.RealceParaCabecalho(CabecalhoEscuro);

        Assert.Equal(Color.FromArgb(36, 46, 61).ToArgb(), realce.ToArgb());
        Assert.True(
            realce.GetBrightness() > CabecalhoEscuro.GetBrightness(),
            "num cabecalho escuro o realce tem de ser MAIS CLARO que o fundo");
    }

    /// <summary>
    /// Cabecalho de cor desconhecida nao fica sem realce: cai na regra geral e ainda se AFASTA do
    /// fundo. Sem isso, uma tela nova repetiria o defeito que estamos corrigindo.
    /// </summary>
    [Fact]
    public void CabecalhoDesconhecido_AindaGeraRealceVisivel()
    {
        Color qualquer = Color.FromArgb(120, 180, 90);
        Color realce = BarraDeTituloHelper.RealceParaCabecalho(qualquer);

        Assert.NotEqual(qualquer.ToArgb(), realce.ToArgb());
        Assert.True(Math.Abs(realce.GetBrightness() - qualquer.GetBrightness()) > 0.03f);
    }

    // ------------------------------------------------- o efeito, disparado de verdade

    /// <summary>
    /// Entrar com o mouse PINTA o botao e sair DEVOLVE a cor original. A volta importa tanto quanto
    /// a ida: sem ela o botao ficaria realcado para sempre depois do primeiro passe.
    /// </summary>
    [Fact]
    public void OMouse_PintaEDevolveACorDoBotao()
    {
        using Panel cabecalho = new() { BackColor = CabecalhoLaranja };
        using Label botao = new() { BackColor = CabecalhoLaranja };
        cabecalho.Controls.Add(botao);

        BarraDeTituloHelper.AplicarRealceDosBotoes(cabecalho, botao);

        Disparar(botao, "OnMouseEnter");
        Assert.Equal(Color.FromArgb(160, 55, 5).ToArgb(), botao.BackColor.ToArgb());

        Disparar(botao, "OnMouseLeave");
        Assert.Equal(CabecalhoLaranja.ToArgb(), botao.BackColor.ToArgb());
    }

    // ------------------------------------------------- as telas que faltavam

    /// <summary>
    /// AS NOVE TELAS QUE NAO TINHAM O EFEITO passam a liga-lo. Setor esta nomeada porque foi a que
    /// o usuario abriu e relatou.
    /// </summary>
    [Theory]
    [InlineData("Cadastro", "SetorForm.cs")]
    [InlineData("Cadastro", "CargoForm.cs")]
    [InlineData("Cadastro", "TaraForm.cs")]
    [InlineData("Cadastro", "BalancaForm.cs")]
    [InlineData("Cadastro", "EtiquetaForm.cs")]
    [InlineData("Cadastro", "ModeloEtiquetaForm.cs")]
    [InlineData("Cadastro", "TipoTaraForm.cs")]
    [InlineData("Cadastro", "CamposEtiquetaForm.cs")]
    [InlineData("Processo", "ProcessoResultadoApontamentoForm.cs")]
    public void AsTelasQueFaltavam_LigamORealce(string pasta, string arquivo)
    {
        string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "Tela", pasta, arquivo));

        Assert.Contains("BarraDeTituloHelper.AplicarRealceDosBotoes", fonte, StringComparison.Ordinal);
    }

    /// <summary>
    /// A COR NAO E PARAMETRO no helper: era exatamente assim que cada tela podia errar a sua. Quem
    /// chama passa o CABECALHO, e a regra decide.
    /// </summary>
    [Fact]
    public void OHelper_NaoAceitaCorComoParametro()
    {
        MethodInfo metodo = typeof(BarraDeTituloHelper).GetMethod(
            "AplicarRealceDosBotoes", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.DoesNotContain(metodo.GetParameters(), p => p.ParameterType == typeof(Color));
    }

    /// <summary>
    /// A tela de Resultado de Apontamento ganha tambem o ARRASTE pelo cabecalho: era a unica das
    /// nove sem ele, entao a janela nao se movia de jeito nenhum.
    /// </summary>
    [Fact]
    public void ResultadoApontamento_GanhaOArrastePeloCabecalho()
    {
        string fonte = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Tela", "Processo", "ProcessoResultadoApontamentoForm.cs"));

        Assert.Contains("BarraDeTituloHelper.PermitirArrastarPeloCabecalho", fonte, StringComparison.Ordinal);
    }

    // ------------------------------------------------- apoio

    private static void Disparar(Control alvo, string evento)
        => typeof(Control)
            .GetMethod(evento, BindingFlags.NonPublic | BindingFlags.Instance, [typeof(EventArgs)])!
            .Invoke(alvo, [EventArgs.Empty]);

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
