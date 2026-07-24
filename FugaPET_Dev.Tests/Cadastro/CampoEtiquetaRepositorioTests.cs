namespace FugaPET_Dev.Tests.Cadastro;

public sealed class CampoEtiquetaRepositorioTests
{
    private static readonly string Repositorio = LerArquivo("AcessoDados", "Repositorio", "CampoEtiquetaRepositorio.cs");

    [Fact]
    public void AtualizarAsync_NaoAlteraSituacaoCampo()
    {
        string metodo = ExtrairEntre("public virtual Task<int> AtualizarAsync", "public virtual Task<int> ExcluirAsync");
        Assert.DoesNotContain("situacao_campo_etiqueta =", metodo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PreencherParametrosEdicao", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicidade_NaoFiltraSomenteAtivos()
    {
        string metodo = ExtrairEntre("ExisteNomeNaEtiquetaAsync", "EtiquetaEstaAtivaAsync");
        Assert.Contains("upper(trim(nome_campo)) = upper(trim(@nome_campo))", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_campo_etiqueta = true", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReativarAsync_ExigeEtiquetaAtivaENaoPermiteDuplicidade()
    {
        string metodo = ExtrairEntre("public virtual Task<int> ReativarAsync", "private static void PreencherParametros");
        Assert.Contains("EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("e.situacao_etiqueta = true", metodo, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("upper(trim(outro.nome_campo)) = upper(trim(campo_etiqueta.nome_campo))", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Queries_ContinuamParametrizadas()
    {
        Assert.Contains("@codigo_etiqueta", Repositorio, StringComparison.Ordinal);
        Assert.Contains("@nome_campo", Repositorio, StringComparison.Ordinal);
        Assert.Contains("ParametroTexto", Repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"SELECT", Repositorio, StringComparison.Ordinal);
    }

    private static string ExtrairEntre(string inicio, string fim)
    {
        int i = Repositorio.IndexOf(inicio, StringComparison.Ordinal);
        int f = Repositorio.IndexOf(fim, i, StringComparison.Ordinal);
        Assert.True(i >= 0 && f > i);
        return Repositorio[i..f];
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine([RaizProjeto(), .. partes]));

    private static string RaizProjeto()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(diretorio))
        {
            if (File.Exists(Path.Combine(diretorio, "FugaPET_Dev.csproj"))) return diretorio;
            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new DirectoryNotFoundException("Raiz do projeto FugaPET_Dev nao encontrada.");
    }
}
