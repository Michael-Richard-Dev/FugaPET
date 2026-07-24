namespace FugaPET_Dev.Tests.Cadastro;

public sealed class MapeamentoCampoEtiquetaRepositorioTests
{
    private static readonly string Repositorio = LerArquivo("AcessoDados", "Repositorio", "MapeamentoCampoEtiquetaRepositorio.cs");

    [Fact]
    public void AtualizarAsync_NaoAlteraSituacao()
    {
        string metodo = ExtrairEntre("public virtual Task<int> AtualizarAsync", "public virtual Task<int> ExcluirAsync");
        Assert.DoesNotContain("situacao_mapeamento_campo_etiqueta =", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExcluirAsync_RealizaInativacaoLogica()
    {
        string metodo = ExtrairEntre("public virtual Task<int> ExcluirAsync", "private static void PreencherParametros");
        Assert.Contains("SET situacao_mapeamento_campo_etiqueta = false", metodo, StringComparison.Ordinal);
        Assert.Contains("AND situacao_mapeamento_campo_etiqueta = true", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Consulta_MantemUmUnicoAtivoPorCampo()
    {
        Assert.Contains("ObterAtivoPorCampoAsync", Repositorio, StringComparison.Ordinal);
        Assert.Contains("situacao_mapeamento_campo_etiqueta = true", Repositorio, StringComparison.Ordinal);
        Assert.Contains("LIMIT 1", Repositorio, StringComparison.Ordinal);
    }

    [Fact]
    public void Queries_ContinuamParametrizadas()
    {
        Assert.Contains("@codigo_campo_etiqueta", Repositorio, StringComparison.Ordinal);
        Assert.Contains("@codigo_mapeamento_campo_etiqueta", Repositorio, StringComparison.Ordinal);
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
