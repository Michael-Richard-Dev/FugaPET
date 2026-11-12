using System.Text.Json;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Servicos.Ambiente;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.Configuracao;

/// <summary>
/// GATE 120G: duplo clique direto em FugaPET_Dev.exe deve resolver a configuracao operacional Q sem
/// PowerShell, BAT, setx, Registry ou environment de User/Machine.
/// <para>
/// Precedencia provada: (1) override explicito de Process; (2) configuracao local Q; (3) fail-closed.
/// E as duas escritas sempre proibidas (SAP generica e palete INT012) nunca ficam true por default.
/// </para>
/// </summary>
public sealed class DirectExeAutoConfigQ120GTests
{
    // ===================== helpers: ambiente VAZIO (= duplo clique) =====================

    /// <summary>Nenhuma variavel de ambiente em nenhum alvo — exatamente o duplo clique no EXE.</summary>
    private static Func<string, string?> AmbienteVazio() => _ => null;

    private static Func<string, EnvironmentVariableTarget, string?> AlvosVazios() => (_, _) => null;

    private static Func<string, EnvironmentVariableTarget, string?> AlvosCom(
        IReadOnlyDictionary<string, string> processo,
        IReadOnlyDictionary<string, string>? usuario = null,
        IReadOnlyDictionary<string, string>? maquina = null)
        => (nome, alvo) => alvo switch
        {
            EnvironmentVariableTarget.Process => processo.TryGetValue(nome, out string? p) ? p : null,
            EnvironmentVariableTarget.User => usuario is not null && usuario.TryGetValue(nome, out string? u) ? u : null,
            _ => maquina is not null && maquina.TryGetValue(nome, out string? m) ? m : null
        };

    private static Func<string, string?> AmbienteCom(IReadOnlyDictionary<string, string> valores)
        => nome => valores.TryGetValue(nome, out string? v) ? v : null;

    private static PerfilAmbienteLocal PerfilDeJson(string json)
    {
        using JsonDocument documento = JsonDocument.Parse(json);
        return PerfilAmbienteLocal.Interpretar(documento.RootElement);
    }

    private static PerfilAmbienteLocal PerfilQ() => PerfilDeJson("""{ "ambiente": { "perfil": "Q" } }""");

    /// <summary>Carrega a configuracao SEM arquivo no disco (so perfil + ambiente informados).</summary>
    private static ConfiguracaoSap Carregar(
        PerfilAmbienteLocal? perfil,
        Func<string, string?>? ambiente = null,
        Func<string, EnvironmentVariableTarget, string?>? alvos = null)
        => LeitorConfiguracaoSap.Carregar(
            Path.Combine(Path.GetTempPath(), $"120g-inexistente-{Guid.NewGuid():N}.json"),
            ambiente ?? AmbienteVazio(),
            alvos ?? AlvosVazios(),
            perfil);

    // ===================== A. sem nenhuma env var =====================

    [Fact]
    public void A_SemNenhumaEnvVar_PerfilQResolveConfiguracaoOperacionalCompleta()
    {
        ConfiguracaoSap c = Carregar(PerfilQ());

        Assert.True(c.ProdutoAcabadoPipelineHabilitado);                 // pipeline
        Assert.True(c.ProdutoAcabadoMaterialDocumentWriteHabilitado);    // material_document
        Assert.True(c.HuWriteHabilitado);                                // HU
        Assert.True(c.PackagingHabilitado);                              // packaging
        Assert.False(c.EscritaHabilitada);                               // generic_write
        Assert.False(c.PalletWriteHabilitado);                           // pallet_write
    }

    [Fact]
    public void A_SemNenhumaEnvVar_PerfilQSatisfazOStartupGuardNoModo2()
    {
        ConfiguracaoSap c = Carregar(PerfilQ());

        // Nenhum gate sempre-proibido ligado, e a combinacao e o trio do pipeline (Modo 2 / 113E).
        Assert.Empty(ValidadorAmbienteQ.ObterGatesPerigososLigados(c));
        Assert.True(ValidadorAmbienteQ.ValidarModoEscritaAdmitido(c).Valido);
    }

    [Fact]
    public void A_SemNenhumaEnvVar_AppEnvEhDeterminadoPeloPerfilLocal()
    {
        // Era o setimo bloqueio do duplo clique: FUGAPET_Q_APP_ENV tambem nao existe.
        Assert.False(ValidadorAmbienteQ.ValidarAppEnv(AmbienteVazio()).Valido);
        Assert.True(ValidadorAmbienteQ.ValidarAppEnv(AmbienteVazio(), PerfilQ()).Valido);
    }

    // ===================== B. override explicito continua funcionando =====================

    [Theory]
    [InlineData("FUGAPET_Q_SAP_PA_PIPELINE_ENABLED")]
    [InlineData("FUGAPET_Q_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED")]
    [InlineData("FUGAPET_Q_SAP_HU_WRITE_ENABLED")]
    [InlineData("FUGAPET_Q_SAP_PACKAGING_ENABLED")]
    public void B_OverrideDeProcessComFalse_DesligaOGateMesmoComPerfilQ(string variavel)
    {
        ConfiguracaoSap c = Carregar(
            PerfilQ(),
            alvos: AlvosCom(new Dictionary<string, string> { [variavel] = "false" }));

        bool valor = variavel switch
        {
            "FUGAPET_Q_SAP_PA_PIPELINE_ENABLED" => c.ProdutoAcabadoPipelineHabilitado,
            "FUGAPET_Q_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED" => c.ProdutoAcabadoMaterialDocumentWriteHabilitado,
            "FUGAPET_Q_SAP_HU_WRITE_ENABLED" => c.HuWriteHabilitado,
            _ => c.PackagingHabilitado
        };

        Assert.False(valor);
    }

    [Fact]
    public void B_OverrideDeProcessComTrue_LigaOGateMesmoSemPerfil()
    {
        ConfiguracaoSap c = Carregar(
            perfil: null,
            alvos: AlvosCom(new Dictionary<string, string>
            {
                ["FUGAPET_Q_SAP_PA_PIPELINE_ENABLED"] = "true",
                ["FUGAPET_Q_SAP_PA_MATERIAL_DOCUMENT_WRITE_ENABLED"] = "true",
                ["FUGAPET_Q_SAP_HU_WRITE_ENABLED"] = "true",
                ["FUGAPET_Q_SAP_PACKAGING_ENABLED"] = "true"
            }));

        Assert.True(c.ProdutoAcabadoPipelineHabilitado);
        Assert.True(c.ProdutoAcabadoMaterialDocumentWriteHabilitado);
        Assert.True(c.HuWriteHabilitado);
        Assert.True(c.PackagingHabilitado);
        Assert.False(c.EscritaHabilitada);
        Assert.False(c.PalletWriteHabilitado);
    }

    [Fact]
    public void B_AppEnvExplicito_TemPrecedenciaSobreOPerfil()
    {
        Func<string, string?> env = AmbienteCom(
            new Dictionary<string, string> { [ValidadorAmbienteQ.VariavelAmbienteAppEnv] = "Q" });

        Assert.True(ValidadorAmbienteQ.ValidarAppEnv(env, perfilAmbiente: null).Valido);
    }

    [Fact]
    public void B_AppEnvExplicitoDiferenteDeQ_BloqueiaMesmoComPerfilQ()
    {
        // Ambiguidade declarada NAO e "corrigida" pelo perfil local.
        Func<string, string?> env = AmbienteCom(
            new Dictionary<string, string> { [ValidadorAmbienteQ.VariavelAmbienteAppEnv] = "PRD" });

        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarAppEnv(env, PerfilQ());

        Assert.False(r.Valido);
        Assert.Contains("valor diferente de Q", r.Mensagem, StringComparison.Ordinal);
    }

    [Fact]
    public void B_OverrideDeUser_NaoLigaGateDeEscrita()
    {
        // Persistir capability em User/Machine e proibido: o override e Process-ONLY.
        ConfiguracaoSap c = Carregar(
            perfil: null,
            alvos: AlvosCom(
                processo: new Dictionary<string, string>(),
                usuario: new Dictionary<string, string>
                {
                    ["FUGAPET_Q_SAP_PA_PIPELINE_ENABLED"] = "true",
                    ["FUGAPET_Q_SAP_HU_WRITE_ENABLED"] = "true",
                    ["FUGAPET_Q_SAP_PACKAGING_ENABLED"] = "true"
                }));

        Assert.False(c.ProdutoAcabadoPipelineHabilitado);
        Assert.False(c.HuWriteHabilitado);
        Assert.False(c.PackagingHabilitado);
    }

    [Fact]
    public void B_PackagingComUserOuMachinePresente_ContinuaFailClosedMesmoComPerfilQ()
    {
        ConfiguracaoSap porUser = Carregar(
            PerfilQ(),
            alvos: AlvosCom(
                processo: new Dictionary<string, string>(),
                usuario: new Dictionary<string, string> { ["FUGAPET_Q_SAP_PACKAGING_ENABLED"] = "true" }));

        ConfiguracaoSap porMachine = Carregar(
            PerfilQ(),
            alvos: AlvosCom(
                processo: new Dictionary<string, string>(),
                maquina: new Dictionary<string, string> { ["FUGAPET_Q_SAP_PACKAGING_ENABLED"] = "true" }));

        Assert.False(porUser.PackagingHabilitado);
        Assert.False(porMachine.PackagingHabilitado);
    }

    // ===================== C. ambiente/config invalido => fail-closed =====================

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "ambiente": { } }""")]
    [InlineData("""{ "ambiente": { "perfil": "" } }""")]
    [InlineData("""{ "ambiente": { "perfil": "   " } }""")]
    [InlineData("""{ "ambiente": { "perfil": "PRD" } }""")]
    [InlineData("""{ "ambiente": { "perfil": "DEV" } }""")]
    [InlineData("""{ "ambiente": { "perfil": 1 } }""")]
    [InlineData("""{ "ambiente": "Q" }""")]
    [InlineData("""{ "ambiente": { "perfil": null } }""")]
    public void C_PerfilInvalidoOuAmbiguo_NaoConcedeNenhumaCapability(string json)
    {
        PerfilAmbienteLocal perfil = PerfilDeJson(json);
        Assert.False(perfil.EhQ);
        Assert.Equal(PerfilOperacional.NaoDefinido, perfil.Perfil);

        ConfiguracaoSap c = Carregar(perfil);
        Assert.False(c.ProdutoAcabadoPipelineHabilitado);
        Assert.False(c.ProdutoAcabadoMaterialDocumentWriteHabilitado);
        Assert.False(c.HuWriteHabilitado);
        Assert.False(c.PackagingHabilitado);
        Assert.False(c.EscritaHabilitada);
        Assert.False(c.PalletWriteHabilitado);

        // E o startup nao passa pelo AppEnv.
        Assert.False(ValidadorAmbienteQ.ValidarAppEnv(AmbienteVazio(), perfil).Valido);
    }

    [Fact]
    public void C_ArquivoAusente_FailClosedSemLancar()
    {
        PerfilAmbienteLocal perfil = PerfilAmbienteLocal.CarregarDoArquivo(
            Path.Combine(Path.GetTempPath(), $"120g-nao-existe-{Guid.NewGuid():N}.json"));

        Assert.False(perfil.EhQ);
        Assert.Contains("ausente", perfil.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void C_ArquivoMalformado_FailClosedSemLancar()
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"120g-malformado-{Guid.NewGuid():N}.json");
        File.WriteAllText(caminho, "{ isso nao e json");
        try
        {
            PerfilAmbienteLocal perfil = PerfilAmbienteLocal.CarregarDoArquivo(caminho);

            Assert.False(perfil.EhQ);
            Assert.Equal(PerfilOperacional.NaoDefinido, perfil.Perfil);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void C_PerfilNulo_EquivaleAFailClosed()
    {
        ConfiguracaoSap c = Carregar(perfil: null);

        Assert.False(c.ProdutoAcabadoPipelineHabilitado);
        Assert.False(c.ProdutoAcabadoMaterialDocumentWriteHabilitado);
        Assert.False(c.HuWriteHabilitado);
        Assert.False(c.PackagingHabilitado);
    }

    [Fact]
    public void C_MensagemDeBloqueioNaoVazaCaminhoNemConteudoDoArquivo()
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"120g-segredo-{Guid.NewGuid():N}.json");
        File.WriteAllText(caminho, """{ "ambiente": { "perfil": "PRD" }, "sap": { "senha": "NAO-VAZAR" } }""");
        try
        {
            PerfilAmbienteLocal perfil = PerfilAmbienteLocal.CarregarDoArquivo(caminho);

            Assert.DoesNotContain(caminho, perfil.Mensagem, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("NAO-VAZAR", perfil.Mensagem, StringComparison.Ordinal);
            Assert.DoesNotContain("senha", perfil.Mensagem, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    // ===================== D / E. generic_write e pallet_write nunca true por default =====================

    [Fact]
    public void D_GenericWrite_NuncaFicaTruePorDefaultDoPerfil()
    {
        Assert.False(Carregar(PerfilQ()).EscritaHabilitada);
        Assert.False(Carregar(perfil: null).EscritaHabilitada);
    }

    [Fact]
    public void E_PalletWrite_NuncaFicaTruePorDefaultDoPerfil()
    {
        Assert.False(Carregar(PerfilQ()).PalletWriteHabilitado);
        Assert.False(Carregar(perfil: null).PalletWriteHabilitado);
    }

    [Fact]
    public void DE_NenhumPerfilConhecidoLigaAsDuasEscritasProibidas()
    {
        // Varre TODOS os perfis do enum, inclusive futuros, pelo caminho real de carga.
        foreach (PerfilOperacional valor in Enum.GetValues<PerfilOperacional>())
        {
            PerfilAmbienteLocal perfil = valor == PerfilOperacional.Q
                ? PerfilQ()
                : PerfilAmbienteLocal.NaoDefinido("teste");

            ConfiguracaoSap c = Carregar(perfil);
            Assert.False(c.EscritaHabilitada);
            Assert.False(c.PalletWriteHabilitado);
            Assert.Empty(ValidadorAmbienteQ.ObterGatesPerigososLigados(c));
        }
    }

    // ===================== chave de arquivo: pode ADICIONAR, nao pode vetar =====================

    [Fact]
    public void ChaveDeArquivo_PaMaterialDocumentDeixouDeSerIgnorada()
    {
        // Antes do 120G o leitor passava o literal false aqui: a chave estava MORTA na config implantada.
        string caminho = Path.Combine(Path.GetTempPath(), $"120g-arquivo-{Guid.NewGuid():N}.json");
        File.WriteAllText(caminho, """
        { "sap": { "pa_material_document_write_enabled": true, "pallet_write_habilitado": false } }
        """);
        try
        {
            ConfiguracaoSap c = LeitorConfiguracaoSap.Carregar(
                caminho, AmbienteVazio(), AlvosVazios(), perfilAmbiente: null);

            Assert.True(c.ProdutoAcabadoMaterialDocumentWriteHabilitado);
            Assert.False(c.PalletWriteHabilitado);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void ChaveDeArquivoFalse_NaoVetaOPerfilQ_MasOverrideDeProcessVeta()
    {
        string caminho = Path.Combine(Path.GetTempPath(), $"120g-veto-{Guid.NewGuid():N}.json");
        File.WriteAllText(caminho, """
        { "ambiente": { "perfil": "Q" }, "sap": { "pa_pipeline_habilitado": false } }
        """);
        try
        {
            PerfilAmbienteLocal perfil = PerfilAmbienteLocal.CarregarDoArquivo(caminho);
            Assert.True(perfil.EhQ);

            // Arquivo false + perfil Q => Q vence na etapa 2 (arquivo e perfil se somam).
            ConfiguracaoSap semOverride = LeitorConfiguracaoSap.Carregar(
                caminho, AmbienteVazio(), AlvosVazios(), perfil);
            Assert.True(semOverride.ProdutoAcabadoPipelineHabilitado);

            // Para desligar em Q usa-se o override de Process, que e a etapa 1.
            ConfiguracaoSap comOverride = LeitorConfiguracaoSap.Carregar(
                caminho,
                AmbienteVazio(),
                AlvosCom(new Dictionary<string, string> { ["FUGAPET_Q_SAP_PA_PIPELINE_ENABLED"] = "false" }),
                perfil);
            Assert.False(comOverride.ProdutoAcabadoPipelineHabilitado);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Theory]
    [InlineData("sim")]
    [InlineData("1")]
    [InlineData("TRUE_")]
    [InlineData("")]
    [InlineData("  ")]
    public void OverrideDeProcessNaoParseavel_CaiParaEtapa2EnaoQuebra(string valor)
    {
        ConfiguracaoSap c = Carregar(
            PerfilQ(),
            alvos: AlvosCom(new Dictionary<string, string> { ["FUGAPET_Q_SAP_PA_PIPELINE_ENABLED"] = valor }));

        // Valor nao booleano nao e tratado como false silencioso nem como true: cai no perfil Q.
        Assert.True(c.ProdutoAcabadoPipelineHabilitado);
    }

    // ===================== fluxo real de startup =====================

    [Fact]
    public void Startup_SemEnvVars_ComPerfilQ_ChegaAteAValidacaoDeSapSemBloquearNoAppEnv()
    {
        // ConfiguracaoSap e classe (nao record): a configuracao Q vem de um arquivo local completo,
        // igual ao que o EXE le ao lado de si.
        string caminho = Path.Combine(Path.GetTempPath(), $"120g-startup-{Guid.NewGuid():N}.json");
        string url = $"https://{ValidadorAmbienteQ.SapHostEsperado}:{ValidadorAmbienteQ.SapPortaEsperada}"
            + "/sap/opu/odata/sap/API_MATERIAL_DOCUMENT_SRV/";
        File.WriteAllText(caminho, $$"""
        {
          "ambiente": { "perfil": "Q" },
          "sap": {
            "base_url": "{{url}}",
            "material_document_base_url": "{{url}}",
            "sap_client": "110",
            "hosts_permitidos": ["{{ValidadorAmbienteQ.SapHostEsperado}}"]
          }
        }
        """);

        try
        {
            PerfilAmbienteLocal perfil = PerfilAmbienteLocal.CarregarDoArquivo(caminho);
            ConfiguracaoSap sap = LeitorConfiguracaoSap.Carregar(
                caminho, AmbienteVazio(), AlvosVazios(), perfil);

            ConfiguracaoBancoPostgreSql banco = new()
            {
                Servidor = "NT-TI-256",
                NomeBanco = ValidadorAmbienteQ.DatabaseEsperado,
                Schema = ValidadorAmbienteQ.SchemaEsperado,
                Usuario = ValidadorAmbienteQ.RoleAplicacaoEsperada
            };

            ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarStartup(
                sap, banco, AmbienteVazio(), perfil);

            Assert.True(r.Valido, r.Mensagem);

            // E o Modo 2 inteiro resolvido sem UMA variavel de ambiente.
            Assert.True(sap.ProdutoAcabadoPipelineHabilitado);
            Assert.True(sap.ProdutoAcabadoMaterialDocumentWriteHabilitado);
            Assert.True(sap.HuWriteHabilitado);
            Assert.True(sap.PackagingHabilitado);
            Assert.False(sap.EscritaHabilitada);
            Assert.False(sap.PalletWriteHabilitado);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public void Startup_SemEnvVars_SemPerfil_ContinuaBloqueadoNoAppEnv()
    {
        ConfiguracaoSap sap = Carregar(perfil: null);
        ConfiguracaoBancoPostgreSql banco = new();

        ResultadoValidacaoAmbienteQ r = ValidadorAmbienteQ.ValidarStartup(
            sap, banco, AmbienteVazio(), perfilAmbiente: null);

        Assert.False(r.Valido);
        Assert.Contains(ValidadorAmbienteQ.VariavelAmbienteAppEnv, r.Mensagem, StringComparison.Ordinal);
    }

    // ===================== source audit =====================

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

    [Fact]
    public void SourceAudit_PerfilNaoTemCampoCapazDeLigarEscritaGenericaOuPalete()
    {
        string perfil = SemComentarios(LerFonte("Servicos", "Ambiente", "PerfilAmbienteLocal.cs"));

        // DefaultsPerfilQ expoe EXATAMENTE as quatro capabilities permitidas.
        Assert.Contains("PipelineProdutoAcabado", perfil, StringComparison.Ordinal);
        Assert.Contains("MaterialDocumentProdutoAcabado", perfil, StringComparison.Ordinal);
        Assert.Contains("HandlingUnitWrite", perfil, StringComparison.Ordinal);
        Assert.Contains("Packaging", perfil, StringComparison.Ordinal);

        // E NAO expoe nada relacionado as duas proibidas.
        Assert.DoesNotContain("EscritaHabilitada", perfil, StringComparison.Ordinal);
        Assert.DoesNotContain("EscritaGenerica", perfil, StringComparison.Ordinal);
        Assert.DoesNotContain("Pallet", perfil, StringComparison.Ordinal);
        Assert.DoesNotContain("Palete", perfil, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_LeitorPassaFalseFixoNosDoisGatesProibidos()
    {
        string leitor = LerFonte("Servicos", "IntegracaoSap", "LeitorConfiguracaoSap.cs");

        Assert.Contains("VariavelAmbienteEscritaHabilitada,\r\n                escritaHabilitada,\r\n                padraoPerfil: false)", leitor, StringComparison.Ordinal);
        Assert.Contains("VariavelAmbientePalletWriteHabilitado,\r\n                palletWriteHabilitadoArquivo,\r\n                padraoPerfil: false)", leitor, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_OverrideDeGateEhLidoSomenteNoAlvoProcess()
    {
        string leitor = SemComentarios(LerFonte("Servicos", "IntegracaoSap", "LeitorConfiguracaoSap.cs"));

        // O resolver de gate le Process e nada mais.
        int inicio = leitor.IndexOf("internal static bool ResolverGateOperacional", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "ResolverGateOperacional nao encontrado.");
        int fim = leitor.IndexOf("\n    internal", inicio + 40, StringComparison.Ordinal);
        string corpo = fim > inicio ? leitor[inicio..fim] : leitor[inicio..];

        Assert.Contains("EnvironmentVariableTarget.Process", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentVariableTarget.User", corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnvironmentVariableTarget.Machine", corpo, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_ProgramCarregaOPerfilLocalEoPassaAoStartup()
    {
        string program = SemComentarios(LerFonte("Program.cs"));

        Assert.Contains("PerfilAmbienteLocal.CarregarDoArquivo(", program, StringComparison.Ordinal);
        Assert.Contains("perfilAmbiente)", program, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAudit_NaoIntroduzSetxRegistryNemPersistenciaDeAmbiente()
    {
        foreach (string[] arquivo in new[]
        {
            new[] { "Servicos", "Ambiente", "PerfilAmbienteLocal.cs" },
            ["Servicos", "IntegracaoSap", "LeitorConfiguracaoSap.cs"],
            ["Servicos", "Ambiente", "ValidadorAmbienteQ.cs"],
            ["Program.cs"]
        })
        {
            string fonte = SemComentarios(LerFonte(arquivo));
            foreach (string proibido in new[] { "setx", "SetEnvironmentVariable", "Registry", "Microsoft.Win32" })
            {
                Assert.DoesNotContain(proibido, fonte, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void SourceAudit_TemplateDeDeployDeclaraOPerfilQ()
    {
        using JsonDocument documento = JsonDocument.Parse(LerFonte("configuracao.sap.exemplo.json"));
        PerfilAmbienteLocal perfil = PerfilAmbienteLocal.Interpretar(documento.RootElement);

        Assert.True(perfil.EhQ);
        Assert.Equal("CONFIG_LOCAL", perfil.Origem);
    }
}
