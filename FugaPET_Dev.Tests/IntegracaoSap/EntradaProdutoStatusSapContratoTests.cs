namespace FugaPET_Dev.Tests.IntegracaoSap;

public sealed class EntradaProdutoStatusSapContratoTests
{
    private static readonly string RaizProjeto = ObterRaizProjeto();

    [Fact]
    public void Repositorio_DevePersistirStatusValidosSemInventarParcialSap()
    {
        string repositorio = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "AcessoDados",
            "Repositorio",
            "EntradaProdutoRepositorio.cs"));

        Assert.Contains("\"CONFIRMADO_SAP\"", repositorio, StringComparison.Ordinal);
        Assert.Contains("\"ERRO_SAP\"", repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PARCIAL_SAP\"", repositorio, StringComparison.Ordinal);
        Assert.Contains(
            "cenario is CenarioEnvioSapEntrada.Enviado or CenarioEnvioSapEntrada.Falha",
            repositorio,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FinalizacaoLocal_NaoDeveChamarEnvioSap()
    {
        string controller = File.ReadAllText(Path.Combine(
            RaizProjeto,
            "Controle",
            "Processo",
            "EntradaProdutoController.cs"));

        int inicio = controller.IndexOf(
            "public async Task<ResultadoFinalizacaoEntrada> FinalizarLeituraAsync",
            StringComparison.Ordinal);
        int fim = controller.IndexOf(
            "public async Task<ResultadoEnvioSapEntrada>",
            inicio,
            StringComparison.Ordinal);

        Assert.True(inicio >= 0 && fim > inicio);
        string metodo = controller[inicio..fim];
        Assert.DoesNotContain("AtualizarPesoItemSapAsync", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("EnviarPesoEntradaParaSapHomologacaoAsync", metodo, StringComparison.Ordinal);
    }

    private static string ObterRaizProjeto()
    {
        DirectoryInfo? diretorio = new(AppContext.BaseDirectory);
        while (diretorio is not null
               && !File.Exists(Path.Combine(diretorio.FullName, "FugaPET_Dev.csproj")))
        {
            diretorio = diretorio.Parent;
        }

        return diretorio?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev não encontrada.");
    }
}
