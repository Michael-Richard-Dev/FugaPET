using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// GATE 122C — TRACK A: o startup do Q passa a admitir o modo composto
/// SAFE_PA_PIPELINE_WITH_PALLET. Antes, pallet_write_habilitado=true era gate "sempre proibido" e
/// DERRUBAVA o startup; agora é validado como COMBINAÇÃO, e só o perfil completo o admite.
/// <para>
/// Matriz T01..T22 do gate. Nenhum SAP, nenhum banco, nenhum HTTP: validação pura de combinação +
/// resolução da flag.
/// </para>
/// </summary>
public sealed class PaletizacaoSafeStartup122CTests
{
    // ===================== helpers =====================

    private static ConfiguracaoSap Config(
        bool pipeline = false,
        bool materialDocument = false,
        bool hu = false,
        bool packaging = false,
        bool palete = false,
        bool generica = false)
        => new()
        {
            ProdutoAcabadoPipelineHabilitado = pipeline,
            ProdutoAcabadoMaterialDocumentWriteHabilitado = materialDocument,
            HuWriteHabilitado = hu,
            PackagingHabilitado = packaging,
            PalletWriteHabilitado = palete,
            EscritaHabilitada = generica
        };

    /// <summary>Perfil seguro COMPOSTO (com palete), conforme 122B.</summary>
    private static ConfiguracaoSap PerfilComPalete(bool palete = true)
        => Config(pipeline: true, materialDocument: true, hu: true, packaging: true, palete: palete);

    /// <summary>Executa o startup guard COMPLETO de write gates (absolutos + combinação).</summary>
    private static bool StartupPermitido(ConfiguracaoSap c)
        => ValidadorAmbienteQ.ObterGatesPerigososLigados(c).Length == 0
           && ValidadorAmbienteQ.ValidarModoEscritaAdmitido(c).Valido;

    private static ConfiguracaoSap CarregarComAlvos(
        IReadOnlyDictionary<string, string> processo,
        IReadOnlyDictionary<string, string>? usuario = null,
        IReadOnlyDictionary<string, string>? maquina = null,
        bool? arquivoPallet = null)
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"122c-{Guid.NewGuid():N}.json");
        string conteudo = arquivoPallet is bool v
            ? $$"""{ "sap": { "pallet_write_habilitado": {{(v ? "true" : "false")}} } }"""
            : """{ "sap": { } }""";
        File.WriteAllText(caminho, conteudo);
        try
        {
            return LeitorConfiguracaoSap.Carregar(
                caminho,
                nome => processo.TryGetValue(nome, out string? p) ? p : null,
                (nome, alvo) => alvo switch
                {
                    EnvironmentVariableTarget.Process => processo.TryGetValue(nome, out string? p) ? p : null,
                    EnvironmentVariableTarget.User => usuario is not null && usuario.TryGetValue(nome, out string? u) ? u : null,
                    _ => maquina is not null && maquina.TryGetValue(nome, out string? m) ? m : null
                },
                perfilAmbiente: null);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    private const string Flag = "FUGAPET_Q_SAP_PALLET_WRITE_ENABLED";
    private static Dictionary<string, string> Vazio() => [];

    // ===================== T01 / T02: os dois modos admitidos =====================

    [Fact]
    public void T01_PalletFalse_ComPerfilSeguroAtual_Passa()
    {
        ConfiguracaoSap c = PerfilComPalete(palete: false);

        Assert.True(StartupPermitido(c));
        Assert.Equal(ValidadorAmbienteQ.ModoSeguroPipeline, ValidadorAmbienteQ.ObterModoEscritaAdmitido(c));
    }

    [Fact]
    public void T02_PalletTrue_ComPerfilSeguroComposto_Passa()
    {
        ConfiguracaoSap c = PerfilComPalete();

        Assert.True(StartupPermitido(c));
        Assert.Empty(ValidadorAmbienteQ.ObterGatesPerigososLigados(c));
        Assert.Equal(
            ValidadorAmbienteQ.ModoSeguroPipelineComPalete,
            ValidadorAmbienteQ.ObterModoEscritaAdmitido(c));
    }

    // ===================== T03 / T04: escrita genérica SEMPRE nega =====================

    [Fact]
    public void T03_GenericTrue_Bloqueia()
    {
        ConfiguracaoSap c = Config(generica: true);

        Assert.False(StartupPermitido(c));
        Assert.Contains(
            "escrita_habilitada",
            string.Join(";", ValidadorAmbienteQ.ObterGatesPerigososLigados(c)),
            StringComparison.Ordinal);
        Assert.Null(ValidadorAmbienteQ.ObterModoEscritaAdmitido(c));
    }

    [Fact]
    public void T04_PalletTrue_MaisGenericTrue_Bloqueia()
    {
        ConfiguracaoSap c = Config(
            pipeline: true, materialDocument: true, hu: true, packaging: true, palete: true, generica: true);

        Assert.False(StartupPermitido(c));
        // A escrita genérica continua sendo gate ABSOLUTO, mesmo com o perfil de palete completo.
        Assert.NotEmpty(ValidadorAmbienteQ.ObterGatesPerigososLigados(c));
        Assert.Null(ValidadorAmbienteQ.ObterModoEscritaAdmitido(c));
    }

    // ===================== T05..T08: pallet=true fora do perfil exato =====================

    [Fact]
    public void T05_PalletTrue_SemPipeline_Bloqueia()
    {
        ConfiguracaoSap c = Config(pipeline: false, materialDocument: true, hu: true, packaging: true, palete: true);

        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarModoEscritaAdmitido(c);
        Assert.False(r.Valido);
        Assert.Contains("pa_pipeline_habilitado", r.Mensagem, StringComparison.Ordinal);
        Assert.Contains(ValidadorAmbienteQ.ModoSeguroPipelineComPalete, r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void T06_PalletTrue_SemMaterialDocument_Bloqueia()
    {
        ConfiguracaoSap c = Config(pipeline: true, materialDocument: false, hu: true, packaging: true, palete: true);

        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarModoEscritaAdmitido(c);
        Assert.False(r.Valido);
        Assert.Contains("pa_material_document_write_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void T07_PalletTrue_SemHu_Bloqueia()
    {
        ConfiguracaoSap c = Config(pipeline: true, materialDocument: true, hu: false, packaging: true, palete: true);

        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarModoEscritaAdmitido(c);
        Assert.False(r.Valido);
        Assert.Contains("hu_write_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void T08_PalletTrue_SemPackaging_Bloqueia()
    {
        ConfiguracaoSap c = Config(pipeline: true, materialDocument: true, hu: true, packaging: false, palete: true);

        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarModoEscritaAdmitido(c);
        Assert.False(r.Valido);
        Assert.Contains("packaging_habilitado", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void T05a_PalletTrue_Isolado_Bloqueia()
    {
        ConfiguracaoSap c = Config(palete: true);

        Assert.False(StartupPermitido(c));
        Assert.Null(ValidadorAmbienteQ.ObterModoEscritaAdmitido(c));
    }

    /// <summary>Varredura exaustiva: pallet=true só passa na combinação EXATA dos quatro.</summary>
    [Fact]
    public void T09a_PalletTrue_SomenteUmaDas16CombinacoesPassa()
    {
        int passaram = 0;
        foreach (bool pipeline in new[] { false, true })
        foreach (bool md in new[] { false, true })
        foreach (bool hu in new[] { false, true })
        foreach (bool pack in new[] { false, true })
        {
            ConfiguracaoSap c = Config(pipeline, md, hu, pack, palete: true);
            if (StartupPermitido(c))
            {
                passaram++;
                Assert.True(pipeline && md && hu && pack);
            }
        }

        Assert.Equal(1, passaram);
    }

    // ===================== T09: perfil desconhecido =====================

    [Fact]
    public void T09_PalletTrue_ComPerfilDesconhecido_NaoHabilitaAFlag()
    {
        // Perfil local não reconhecido ⇒ nenhuma capability concedida ⇒ as outras ficam false ⇒
        // pallet=true (por Process) cai em combinação parcial ⇒ BLOCK.
        ConfiguracaoSap c = CarregarComAlvos(new Dictionary<string, string> { [Flag] = "true" });

        Assert.True(c.PalletWriteHabilitado);
        Assert.False(c.ProdutoAcabadoPipelineHabilitado);
        Assert.False(StartupPermitido(c));
    }

    // ===================== T10..T14 / T17: precedência da flag (PRESERVADA) =====================

    [Fact]
    public void T10_ProcessTrue_ArquivoFalse_EfetivoTrue()
        => Assert.True(CarregarComAlvos(
            new Dictionary<string, string> { [Flag] = "true" }, arquivoPallet: false).PalletWriteHabilitado);

    [Fact]
    public void T11_ProcessFalse_ArquivoTrue_EfetivoFalse()
        => Assert.False(CarregarComAlvos(
            new Dictionary<string, string> { [Flag] = "false" }, arquivoPallet: true).PalletWriteHabilitado);

    [Fact]
    public void T12_ProcessAusente_ArquivoTrue_EfetivoTrue()
        => Assert.True(CarregarComAlvos(Vazio(), arquivoPallet: true).PalletWriteHabilitado);

    [Fact]
    public void T13_ProcessAusente_ArquivoFalse_EfetivoFalse()
        => Assert.False(CarregarComAlvos(Vazio(), arquivoPallet: false).PalletWriteHabilitado);

    [Fact]
    public void T14_ChaveAusente_EfetivoFalse()
        => Assert.False(CarregarComAlvos(Vazio()).PalletWriteHabilitado);

    [Fact]
    public void T15_UserTrueIsolado_NaoHabilita()
        => Assert.False(CarregarComAlvos(
            Vazio(), usuario: new Dictionary<string, string> { [Flag] = "true" }).PalletWriteHabilitado);

    [Fact]
    public void T16_MachineTrueIsolado_NaoHabilita()
        => Assert.False(CarregarComAlvos(
            Vazio(), maquina: new Dictionary<string, string> { [Flag] = "true" }).PalletWriteHabilitado);

    [Theory]
    [InlineData("sim")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("TRUE_")]
    [InlineData("  ")]
    public void T17_ValorInvalidoNoProcess_CaiParaArquivoFalse_FailClosed(string valor)
        => Assert.False(CarregarComAlvos(
            new Dictionary<string, string> { [Flag] = valor }, arquivoPallet: false).PalletWriteHabilitado);

    [Fact]
    public void T18_DirectExeSemConfigExplicita_PalletFalse()
    {
        // Perfil Q (120G) concede pipeline/md/hu/packaging — e NUNCA palete.
        ConfiguracaoSap c = CarregarComAlvos(Vazio());

        Assert.False(c.PalletWriteHabilitado);
        Assert.False(c.EscritaHabilitada);
    }

    // ===================== T19..T21: habilitar palete não libera mais nada =====================

    [Fact]
    public void T19_StartupComPalletTrue_NaoExecutaNenhumHttp()
    {
        // O guard é PURO: só lê a configuração. Nenhum HttpClient/SendAsync no caminho.
        string fonte = LerFonte("Servicos", "Ambiente", "ValidadorAmbienteQ.cs");

        foreach (string proibido in new[] { "HttpClient", "SendAsync", "Npgsql", "PostAsync", "GetAsync" })
        {
            Assert.DoesNotContain(proibido, fonte, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void T20_PalletTrue_GenericContinuaFalse()
    {
        ConfiguracaoSap c = PerfilComPalete();

        Assert.True(c.PalletWriteHabilitado);
        Assert.False(c.EscritaHabilitada);
        Assert.Empty(ValidadorAmbienteQ.ObterGatesPerigososLigados(c));
    }

    [Fact]
    public void T21_PalletTrue_NaoHabilitaCapabilityAdicional()
    {
        ConfiguracaoSap semPalete = PerfilComPalete(palete: false);
        ConfiguracaoSap comPalete = PerfilComPalete();

        // A ÚNICA diferença entre os dois é a própria flag de palete.
        Assert.Equal(semPalete.ProdutoAcabadoPipelineHabilitado, comPalete.ProdutoAcabadoPipelineHabilitado);
        Assert.Equal(semPalete.ProdutoAcabadoMaterialDocumentWriteHabilitado, comPalete.ProdutoAcabadoMaterialDocumentWriteHabilitado);
        Assert.Equal(semPalete.HuWriteHabilitado, comPalete.HuWriteHabilitado);
        Assert.Equal(semPalete.PackagingHabilitado, comPalete.PackagingHabilitado);
        Assert.Equal(semPalete.EscritaHabilitada, comPalete.EscritaHabilitada);
        Assert.NotEqual(semPalete.PalletWriteHabilitado, comPalete.PalletWriteHabilitado);
    }

    // ===================== T22: regressão com pallet=false =====================

    [Fact]
    public void T22_Regressao_PalletFalse_PreservaComportamentoAtual()
    {
        // Modo 1: nada ligado.
        Assert.True(StartupPermitido(Config()));
        // Modo 1: hu_write isolado.
        Assert.True(StartupPermitido(Config(hu: true)));
        // Modo 2: trio do pipeline — packaging continua NÃO exigido quando não há palete.
        Assert.True(StartupPermitido(Config(pipeline: true, materialDocument: true, hu: true)));
        // Combinação parcial do pipeline continua bloqueada.
        Assert.False(StartupPermitido(Config(pipeline: true, materialDocument: true)));
        Assert.False(StartupPermitido(Config(pipeline: true)));
        Assert.False(StartupPermitido(Config(materialDocument: true)));
    }

    [Fact]
    public void T22a_PalletSaiuDaListaDeGatesAbsolutos()
    {
        string fonte = SemComentarios(LerFonte("Servicos", "Ambiente", "ValidadorAmbienteQ.cs"));
        int i = fonte.IndexOf("internal static string[] ObterGatesPerigososLigados", StringComparison.Ordinal);
        Assert.True(i >= 0);
        int fim = fonte.IndexOf("\n    internal", i + 40, StringComparison.Ordinal);
        string corpo = fim > i ? fonte[i..fim] : fonte[i..];

        Assert.Contains("EscritaHabilitada", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("PalletWriteHabilitado", corpo, StringComparison.Ordinal);
    }

    // ===================== infra =====================

    private static string LerFonte(params string[] partes)
    {
        string dir = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(dir) && !File.Exists(Path.Combine(dir, "FugaPET_Dev.csproj")))
        {
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }

        Assert.False(string.IsNullOrWhiteSpace(dir), "Raiz do projeto nao localizada.");
        return File.ReadAllText(Path.Combine(dir, Path.Combine(partes)));
    }

    private static string SemComentarios(string fonte)
    {
        string semBloco = System.Text.RegularExpressions.Regex.Replace(
            fonte, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(semBloco, @"///?[^\r\n]*", string.Empty);
    }
}
