using System.Text.RegularExpressions;

namespace FugaPET_Dev.Tests.Responsividade;

/// <summary>
/// Rodada 1 de diagnóstico de responsividade/DPI. Testes de ANÁLISE: leem as propriedades reais dos
/// *.Designer.cs (não strings arbitrárias), travam invariantes hoje verdadeiros e documentam os riscos —
/// SEM alterar comportamento ou layout. Ver Documentacao/Diagnostico_Responsividade_WinForms_FugaPET.md.
/// </summary>
public sealed class ResponsividadeDiagnosticoTests
{
    // ---- Configuração global de DPI ----

    [Fact]
    public void Global_NaoDefineHighDpiModeNemManifest_UsaDefaultSystemAware()
    {
        // csproj sem ApplicationHighDpiMode / ApplicationManifest.
        string csproj = File.ReadAllText(Path.Combine(RaizProjeto(), "FugaPET_Dev.csproj"));
        Assert.DoesNotContain("ApplicationHighDpiMode", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplicationManifest", csproj, StringComparison.Ordinal);

        // Nenhum SetHighDpiMode manual e nenhum app.manifest com dpiAware no projeto.
        foreach (string cs in ArquivosCs())
        {
            string txt = File.ReadAllText(cs);
            Assert.DoesNotContain("SetHighDpiMode", txt, StringComparison.Ordinal);
        }
        Assert.Empty(Directory.GetFiles(RaizProjeto(), "*.manifest", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !p.Contains("pacotes_limpos", StringComparison.Ordinal)));
    }

    [Fact]
    public void Global_NenhumHandlerDpiChanged_GapParaPerMonitorV2()
    {
        // Documenta o gap: ninguém trata DpiChanged (relevante para uma futura virada PerMonitorV2).
        foreach (string cs in ArquivosCs())
        {
            string txt = File.ReadAllText(cs);
            Assert.DoesNotContain("DpiChanged", txt, StringComparison.Ordinal);
            Assert.DoesNotContain("OnDpiChanged", txt, StringComparison.Ordinal);
        }
    }

    // ---- AutoScaleMode / AutoScaleDimensions ----

    [Fact]
    public void TodosOsForms_UsamAutoScaleModeFont()
    {
        List<string> foraDoPadrao = new();
        foreach (string designer in ArquivosDesignerDeForm())
        {
            string txt = File.ReadAllText(designer);
            if (!txt.Contains("AutoScaleMode = AutoScaleMode.Font", StringComparison.Ordinal))
            {
                foraDoPadrao.Add(Path.GetFileName(designer));
            }
        }
        Assert.True(foraDoPadrao.Count == 0, "Forms sem AutoScaleMode.Font: " + string.Join(", ", foraDoPadrao));
    }

    [Fact]
    public void AutoScaleDimensions_TodasNoConjuntoConhecido_EOsDoisBaselinesCoexistem()
    {
        HashSet<string> baselines = new(StringComparer.Ordinal);
        foreach (string designer in ArquivosDesignerDeForm())
        {
            string txt = File.ReadAllText(designer);
            Match m = Regex.Match(txt, @"AutoScaleDimensions = new SizeF\((?<x>[0-9.]+)F, (?<y>[0-9.]+)F\)");
            if (m.Success)
            {
                baselines.Add($"{m.Groups["x"].Value}x{m.Groups["y"].Value}");
            }
        }
        // Todos pertencem ao conjunto conhecido.
        Assert.All(baselines, b => Assert.Contains(b, new[] { "7x15", "7x16" }));
        // Documenta a INCONSISTÊNCIA: os dois baselines coexistem (risco sistêmico nº 1 do relatório).
        Assert.Contains("7x15", baselines);
        Assert.Contains("7x16", baselines);
    }

    // ---- Telas maduras: MinimumSize declarado ----

    [Theory]
    [InlineData("CamposEtiquetaForm")]
    [InlineData("CargoForm")]
    [InlineData("SetorForm")]
    [InlineData("TipoTaraForm")]
    [InlineData("TaraForm")]
    [InlineData("EtiquetaForm")]
    [InlineData("ModeloEtiquetaForm")]
    [InlineData("ProcessoEntradaProdutoForm")]
    [InlineData("ProcessoConsumoMaterialForm")]
    public void TelasMaduras_DeclaramMinimumSize(string form)
    {
        string txt = LerDesigner(form);
        Assert.Matches(@"MinimumSize = new Size\(\d+, \d+\)", txt);
    }

    // ---- Padrão de ouro: percentuais somam 100 ----

    [Fact]
    public void CamposEtiqueta_BodyLayout_ColunasPercentuaisSomam100()
    {
        string txt = LerDesigner("CamposEtiquetaForm");
        // bodyLayout: 30% + 35% + 35% = 100.
        List<decimal> pct = ColunasPercentuais(txt, "bodyLayout");
        Assert.Equal(3, pct.Count);
        Assert.Equal(100m, pct.Sum());
    }

    [Fact]
    public void CamposEtiqueta_FooterLayout_ColunasPercentuaisSomam100()
    {
        string txt = LerDesigner("CamposEtiquetaForm");
        // footerBarLayout: 16.6 + 16.6 + 26.8 + 20 + 10 + 10 = 100.0.
        List<decimal> pct = ColunasPercentuais(txt, "footerBarLayout");
        Assert.Equal(6, pct.Count);
        Assert.Equal(100.0m, pct.Sum());
    }

    [Fact]
    public void CamposEtiqueta_RootLayout_ContainerBased_SemReescalaManualDeFonte()
    {
        string txt = LerDesigner("CamposEtiquetaForm");
        Assert.Contains("rootLayout = new TableLayoutPanel()", txt, StringComparison.Ordinal);
        Assert.Contains("rootLayout.Dock = DockStyle.Fill", txt, StringComparison.Ordinal);
        // Diferencial do padrão de ouro: NÃO há re-escala manual de FONTE sobre o AutoScaleMode.Font
        // (sem ApplyScaledFont / contentScale) — é o que evita a dupla escala de fonte das telas manuais.
        string form = File.ReadAllText(CaminhoForm("CamposEtiquetaForm"));
        Assert.DoesNotContain("ApplyScaledFont", form, StringComparison.Ordinal);
        Assert.DoesNotContain("contentScale", form, StringComparison.Ordinal);
    }

    [Fact]
    public void GrupoDeRiscoDuplaEscala_ReescalaFonteManualSobreAutoScaleFont()
    {
        // Cohort com re-escala manual de FONTE (ApplyScaledFont OU new Font(...* contentScale/scale)) sobre
        // AutoScaleMode.Font — NÃO virar para PerMonitorV2 sem migração (relatório §6). São 9 telas.
        // (BalancaForm faz SetBounds manual, mas NÃO re-escala fonte — fica fora deste cohort.)
        string[] grupo =
        {
            "CadastroUsuarioForm", "CargoForm", "EtiquetaForm", "ModeloEtiquetaForm",
            "PerfilAcessoForm", "PermissaoForm", "SetorForm", "TaraForm", "TipoTaraForm"
        };
        foreach (string f in grupo)
        {
            string form = File.ReadAllText(CaminhoForm(f));
            bool reescalaFonte = form.Contains("ApplyScaledFont", StringComparison.Ordinal)
                || Regex.IsMatch(form, @"new Font\([^)]*\* (contentScale|scale)")
                || Regex.IsMatch(form, @"Math\.Clamp\([0-9.]+F \* (contentScale|scale)");
            Assert.True(reescalaFonte, $"{f} deveria re-escalar fonte manualmente (cohort de dupla escala).");
        }
    }

    // Extrai os percentuais das ColumnStyles atribuídas logo após "<nome>.ColumnCount".
    private static List<decimal> ColunasPercentuais(string designerTexto, string nomeLayout)
    {
        int inicio = designerTexto.IndexOf($"{nomeLayout}.ColumnCount", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Layout {nomeLayout} não encontrado.");
        // Considera a janela de texto até o Dock/RowCount desse layout (onde terminam as ColumnStyles).
        int fim = designerTexto.IndexOf($"{nomeLayout}.Dock", inicio, StringComparison.Ordinal);
        if (fim < 0) fim = Math.Min(designerTexto.Length, inicio + 2000);
        string janela = designerTexto[inicio..fim];

        List<decimal> pct = new();
        foreach (Match m in Regex.Matches(janela, @"new ColumnStyle\(SizeType\.Percent, (?<v>[0-9.]+)F\)"))
        {
            pct.Add(decimal.Parse(m.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
        return pct;
    }

    // ---- helpers ----

    private static string LerDesigner(string form) => File.ReadAllText(CaminhoDesigner(form));

    private static string CaminhoDesigner(string form)
    {
        string? p = Directory.GetFiles(Path.Combine(RaizProjeto(), "Tela"), $"{form}.Designer.cs", SearchOption.AllDirectories)
            .FirstOrDefault(x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        Assert.True(p is not null, $"Designer não encontrado: {form}");
        return p!;
    }

    private static string CaminhoForm(string form)
        => Directory.GetFiles(Path.Combine(RaizProjeto(), "Tela"), $"{form}.cs", SearchOption.AllDirectories)
            .First(x => !x.EndsWith(".Designer.cs", StringComparison.Ordinal)
                     && !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IEnumerable<string> ArquivosDesignerDeForm()
        => ArquivosDesigner().Where(p =>
        {
            string txt = File.ReadAllText(p);
            return Regex.IsMatch(txt, @"AutoScaleMode = AutoScaleMode\.");
        });

    private static IEnumerable<string> ArquivosDesigner()
        => Directory.GetFiles(Path.Combine(RaizProjeto(), "Tela"), "*.Designer.cs", SearchOption.AllDirectories)
            .Where(NaoEhBuild);

    private static IEnumerable<string> ArquivosCs()
        => Directory.GetFiles(Path.Combine(RaizProjeto(), "Tela"), "*.cs", SearchOption.AllDirectories)
            .Where(NaoEhBuild)
            .Concat(new[] { Path.Combine(RaizProjeto(), "Program.cs") });

    private static bool NaoEhBuild(string caminho)
        => !caminho.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !caminho.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !caminho.Contains("pacotes_limpos", StringComparison.Ordinal);

    private static string RaizProjeto()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir))
        {
            if (File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
