using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FugaPET_Dev.Tela;

namespace FugaPET_Dev.Tests.Tela;

public sealed class ProcessoProducaoCompactFkeys077Tests
{
    public static TheoryData<string, string, Point> Modulos => new()
    {
        { "entradaProdutoCard", "F1", new Point(28, 70) },
        { "processoConsumoMaterialCard", "F2", new Point(284, 70) },
        { "processoConsumoQuimicosCard", "F3", new Point(540, 70) },
        { "processoSemiAcabadoCard", "F4", new Point(796, 70) },
        { "processoProdutoAcabadoCard", "F5", new Point(28, 336) },
        { "ordensAndamentoCard", "F6", new Point(284, 336) },
        { "controleApontamentosCard", "F7", new Point(540, 336) },
        { "paletizacaoCard", "F8", new Point(796, 336) }
    };

    [Theory]
    [MemberData(nameof(Modulos))]
    public void CardsVisiveis_OcupamGradeCompactaEExibemAtalhoCorreto(
        string campoCard,
        string atalhoEsperado,
        Point posicaoEsperada)
    {
        using ProcessoProducaoForm form = new();
        Control card = ObterCampo<Control>(form, campoCard);
        Label atalho = ObterCampo<Label>(form, CampoAtalho(campoCard));

        Assert.True(card.Visible);
        Assert.Equal(posicaoEsperada, card.Location);
        Assert.Equal(atalhoEsperado, atalho.Text);
    }

    [Fact]
    public void EntradaQuimicosPublicaPermaneceOcultaESemLacunaNaGrade()
    {
        using ProcessoProducaoForm form = new();
        Control entradaQuimicos = ObterCampo<Control>(form, "entradaQuimicosCard");
        Point[] posicoes = Modulos.Select(dado => dado[2]).Cast<Point>().ToArray();
        Point[] atuais = Modulos
            .Select(dado => ObterCampo<Control>(form, (string)dado[0]).Location)
            .ToArray();

        Assert.False(entradaQuimicos.Visible);
        Assert.Equal(posicoes, atuais);
    }

    [Theory]
    [InlineData("F1", "OpenProcessoEntradaProdutoAsync()")]
    [InlineData("F2", "OpenProcessoConsumoMaterialAsync()")]
    [InlineData("F3", "OpenProcessoConsumoMaterialAsync(global::FugaPET_Dev.Modelo.Processo.ModoConsumoMaterial.Quimico)")]
    [InlineData("F4", "OpenProcessoSemiAcabadoAsync()")]
    [InlineData("F5", "OpenProcessoProdutoAcabadoAsync()")]
    [InlineData("F6", "OpenConsultaOrdemProducaoAsync()")]
    [InlineData("F7", "OpenControleApontamentosAsync()")]
    [InlineData("F8", "OpenPaletizacaoAsync()")]
    public void AtalhoReal_AbreModuloCorrespondente(string tecla, string chamada)
    {
        string painel = LerArquivo("Tela", "PainelInicialForm.cs");
        string bloco = ExtrairBlocoAtalho(painel, tecla);

        Assert.Contains(chamada, bloco, StringComparison.Ordinal);
    }

    [Fact]
    public void F9_NaoPossuiBindingNoConjuntoProcessoProducao()
    {
        string painel = LerArquivo("Tela", "PainelInicialForm.cs");
        string blocoProcesso = painel[painel.IndexOf("if (e.KeyCode == Keys.F1 && _currentContentView == _processoProducaoForm", StringComparison.Ordinal)..];
        blocoProcesso = blocoProcesso[..blocoProcesso.IndexOf("private async Task OpenPaletizacaoAsync", StringComparison.Ordinal)];

        Assert.DoesNotContain("Keys.F9", blocoProcesso, StringComparison.Ordinal);
    }

    [Fact]
    public void F9_NaoApareceNosCardsVisiveis()
    {
        using ProcessoProducaoForm form = new();
        string[] atalhos = Modulos
            .Select(dado => ObterCampo<Label>(form, CampoAtalho((string)dado[0])).Text)
            .ToArray();

        Assert.DoesNotContain("F9", atalhos);
    }

    [Fact]
    public void AtalhosVisuaisEBindingsReais_SaoEquivalentesDeF1AteF8()
    {
        using ProcessoProducaoForm form = new();
        string painel = LerArquivo("Tela", "PainelInicialForm.cs");

        foreach (object[] modulo in Modulos)
        {
            string tecla = ObterCampo<Label>(form, CampoAtalho((string)modulo[0])).Text;
            Assert.Contains($"Keys.{tecla}", ExtrairBlocoAtalho(painel, tecla), StringComparison.Ordinal);
        }
    }

    private static string CampoAtalho(string campoCard)
        => campoCard switch
        {
            "entradaProdutoCard" => "entradaShortcutLabel",
            "processoConsumoMaterialCard" => "pesagemShortcutLabel",
            "processoConsumoQuimicosCard" => "quimicosShortcutLabel",
            "processoSemiAcabadoCard" => "semiAcabadoShortcutLabel",
            "processoProdutoAcabadoCard" => "processShortcutLabel",
            "ordensAndamentoCard" => "ordensShortcutLabel",
            "controleApontamentosCard" => "apontamentosShortcutLabel",
            "paletizacaoCard" => "paletizacaoShortcutLabel",
            _ => throw new ArgumentOutOfRangeException(nameof(campoCard))
        };

    private static T ObterCampo<T>(ProcessoProducaoForm form, string nome)
        where T : class
        => (T)typeof(ProcessoProducaoForm)
            .GetField(nome, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(form)!;

    private static string ExtrairBlocoAtalho(string fonte, string tecla)
    {
        string inicio = $"if (e.KeyCode == Keys.{tecla} && _currentContentView == _processoProducaoForm)";
        int indiceInicio = fonte.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(indiceInicio >= 0, $"Binding {tecla} não encontrado.");
        int indiceProximo = fonte.IndexOf("if (e.KeyCode == Keys.F", indiceInicio + inicio.Length, StringComparison.Ordinal);
        return indiceProximo < 0 ? fonte[indiceInicio..] : fonte[indiceInicio..indiceProximo];
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine(RaizProjeto(), Path.Combine(partes)));

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

        throw new DirectoryNotFoundException("Raiz do projeto não encontrada.");
    }
}
