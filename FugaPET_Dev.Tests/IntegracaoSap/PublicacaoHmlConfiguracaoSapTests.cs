using System.Text.Json;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class PublicacaoHmlConfiguracaoSapTests
{
    [Fact]
    public void Csproj_DevePublicarSomenteModelosEProtegerConfiguracaoRealDaMaquina()
    {
        string csproj = LerArquivoProjeto("FugaPET_Dev.csproj");

        // Modelos (.exemplo.json) vao para o publish como referencia da 1a configuracao.
        Assert.Contains("<None Include=\"configuracao.sap.exemplo.json\">", csproj, StringComparison.Ordinal);
        // O DEV NAO publica ambiente.q.json: aquele marcador declara o ambiente Q e nao existe nesta linha.
        Assert.DoesNotContain("ambiente.q.json", csproj, StringComparison.Ordinal);
        Assert.Contains("<Content Include=\"configuracao.banco.exemplo.json\">", csproj, StringComparison.Ordinal);
        Assert.Contains("<Content Include=\"configuracao.terminal.exemplo.json\">", csproj, StringComparison.Ordinal);

        // Configuracoes REAIS existem no bin (execucao local) mas NUNCA no publish,
        // para que o pacote de atualizacao nao sobrescreva os configuracao.*.json da maquina.
        Assert.Contains("<None Update=\"configuracao.sap.json\" Condition=\"Exists('configuracao.sap.json')\">", csproj, StringComparison.Ordinal);
        Assert.Contains("<Content Include=\"configuracao.banco.json\" Condition=\"Exists('configuracao.banco.json')\">", csproj, StringComparison.Ordinal);
        Assert.Contains("<Content Include=\"configuracao.terminal.json\" Condition=\"Exists('configuracao.terminal.json')\">", csproj, StringComparison.Ordinal);

        // Modelos vao para publish; configuracoes reais permanecem protegidas.
        Assert.Equal(0, Contar(csproj, "ambiente.q.json"));
        Assert.Equal(3, Contar(csproj, "<CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>"));
        Assert.Equal(3, Contar(csproj, "<CopyToPublishDirectory>Never</CopyToPublishDirectory>"));
    }

    [Fact]
    public void ConfiguracaoSapExemplo_DeveConterChavesHmlSemSenhaReal()
    {
        string json = LerArquivoProjeto("configuracao.sap.exemplo.json");
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement sap = doc.RootElement.GetProperty("sap");

        Assert.Equal("110", sap.GetProperty("sap_client").GetString());
        Assert.Contains("API_MATERIAL_DOCUMENT_SRV", sap.GetProperty("material_document_base_url").GetString(), StringComparison.Ordinal);
        Assert.Contains("API_PRODUCTION_ORDER_2_SRV", sap.GetProperty("production_order_base_url").GetString(), StringComparison.Ordinal);
        Assert.Contains("API_PRODUCT_SRV", sap.GetProperty("product_base_url").GetString(), StringComparison.Ordinal);
        Assert.Equal("DEFINIR_USUARIO_OU_USAR_VARIAVEL_AMBIENTE", sap.GetProperty("usuario").GetString());
        Assert.Equal("DEFINIR_SENHA_OU_USAR_VARIAVEL_AMBIENTE", sap.GetProperty("senha").GetString());

        // API de embalagem (Integration Suite): endpoint + allowlist própria no exemplo; NUNCA credencial.
        Assert.Contains("GetPackagingSet", sap.GetProperty("packaging_base_url").GetString(), StringComparison.Ordinal);
        Assert.True(sap.TryGetProperty("packaging_hosts_permitidos", out _));
        Assert.False(sap.TryGetProperty("packaging_usuario", out _));
        Assert.False(sap.TryGetProperty("packaging_senha", out _));

        Assert.DoesNotContain("Authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Basic ", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gitignore_DeveImpedirVersionarConfiguracaoSapReal()
    {
        string gitignore = LerArquivoProjeto(".gitignore");

        Assert.Contains("configuracao.sap.json", gitignore, StringComparison.Ordinal);
        Assert.Contains("!configuracao.sap.exemplo.json", gitignore, StringComparison.Ordinal);
    }

    [Fact]
    public void ChecklistPublicacaoHml_DeveExigirConfiguracaoSap()
    {
        string checklist = LerArquivoProjeto("README_PUBLICACAO_HML.md");

        Assert.Contains("configuracao.sap.json", checklist, StringComparison.Ordinal);
        Assert.Contains("production_order_base_url", checklist, StringComparison.Ordinal);
        Assert.Contains("material_document_base_url", checklist, StringComparison.Ordinal);
        Assert.Contains("product_base_url", checklist, StringComparison.Ordinal);
        Assert.Contains("Test-NetConnection", checklist, StringComparison.Ordinal);
        Assert.Contains("$metadata", checklist, StringComparison.Ordinal);

        // API de embalagem (Integration Suite): URL/host próprios + variáveis específicas + GET de validação 200.
        Assert.Contains("packaging_base_url", checklist, StringComparison.Ordinal);
        Assert.Contains("FUGAPET_SAP_PACKAGING_USERNAME", checklist, StringComparison.Ordinal);
        Assert.Contains("FUGAPET_SAP_PACKAGING_PASSWORD", checklist, StringComparison.Ordinal);
        Assert.Contains("GetPackagingSet", checklist, StringComparison.Ordinal);
    }

    private static int Contar(string texto, string trecho)
        => texto.Split(trecho, StringSplitOptions.None).Length - 1;

    private static string LerArquivoProjeto(params string[] partes)
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

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}


