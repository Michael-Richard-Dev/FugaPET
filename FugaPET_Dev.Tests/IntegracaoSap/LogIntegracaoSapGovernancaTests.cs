using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class LogIntegracaoSapGovernancaTests
{
    [Theory]
    [InlineData("Authorization: Basic segredo")]
    [InlineData("Password=segredo")]
    [InlineData("Cookie: sessao")]
    public void SanitizarMensagem_ComMarcadorSensivel_DeveRemoverConteudo(string mensagem)
    {
        string? resultado = LogIntegracaoSapServico.SanitizarMensagem(mensagem);

        Assert.Equal(
            "Mensagem tecnica removida por conter dado potencialmente sensivel.",
            resultado);
        Assert.DoesNotContain("segredo", resultado, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CredencialCompleta_NaoDeveAparecerNoLogSanitizado()
    {
        string segredo = Guid.NewGuid().ToString("N");

        string? resultado = LogIntegracaoSapServico.SanitizarMensagem(
            $"Authorization: Basic {segredo}");

        Assert.DoesNotContain(segredo, resultado, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptBanco_NaoDeveConterCamposDeSegredoOuPayload()
    {
        string caminhoIncremental = LocalizarArquivo(
            "BancoDados", "001_incrementais", "019_criar_log_integracao_sap_v1_0.sql");
        string caminho = File.Exists(caminhoIncremental)
            ? caminhoIncremental
            : LocalizarArquivo(
                "BancoDados",
                "000_baseline",
                "Banco_Desenvolvimento_V1_1_Geral",
                "000_execucao_completa_desenvolvimento_v1_1.sql");
        string conteudoCompleto = File.ReadAllText(caminho);
        string conteudo = ExtrairSecao(
            conteudoCompleto,
            "019 - Log",
            "020 - Perm");

        Assert.Contains("correlation_id", conteudo, StringComparison.Ordinal);
        Assert.Contains("registrado_em_utc", conteudo, StringComparison.Ordinal);
        Assert.Contains("limpar_log_integracao_sap", conteudo, StringComparison.Ordinal);
        Assert.DoesNotContain("authorization ", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password ", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload_original", conteudo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FluxoProdutivo_NaoDeveEscreverSapSyncLog()
    {
        string raiz = LocalizarRaizProjeto();
        string[] arquivos = Directory.GetFiles(
            Path.Combine(raiz, "Servicos"),
            "*.cs",
            SearchOption.AllDirectories);

        foreach (string arquivo in arquivos)
        {
            string conteudo = File.ReadAllText(arquivo);
            Assert.DoesNotContain("sap_sync.log", conteudo, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PacoteLimpo_DeveBloquearArquivosLog()
    {
        string caminho = LocalizarArquivo("Scripts", "GerarPacoteLimpo.ps1");
        string conteudo = File.ReadAllText(caminho);

        Assert.Contains("'.log'", conteudo, StringComparison.Ordinal);
        Assert.Contains("'pacotes_limpos'", conteudo, StringComparison.Ordinal);
    }

    private static string LocalizarArquivo(params string[] partes)
        => Path.Combine([LocalizarRaizProjeto(), .. partes]);

    private static string ExtrairSecao(string conteudo, string inicio, string fim)
    {
        int indiceInicio = conteudo.IndexOf(inicio, StringComparison.Ordinal);
        if (indiceInicio < 0)
        {
            return conteudo;
        }

        int indiceFim = conteudo.IndexOf(fim, indiceInicio, StringComparison.Ordinal);
        return indiceFim > indiceInicio
            ? conteudo[indiceInicio..indiceFim]
            : conteudo[indiceInicio..];
    }

    private static string LocalizarRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null)
        {
            if (File.Exists(Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj")))
            {
                return diretorio.FullName;
            }

            diretorio = diretorio.Parent;
        }

        throw new DirectoryNotFoundException("Raiz do projeto nao encontrada.");
    }
}
