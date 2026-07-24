namespace FugaPET_Dev.Tests.Cadastro;

/// <summary>
/// Tarefa Modelo de Etiqueta: garante por source-scan as regras de banco do repositório — atualização cadastral
/// sem mexer na situação, duplicidade GLOBAL (não filtra só ativos), e UPDATEs atômicos com NOT EXISTS.
/// </summary>
public sealed class ModeloEtiquetaRepositorioTests
{
    [Fact]
    public void AtualizarAsync_NaoAtualizaSituacao()
    {
        string metodo = ExtrairMetodoRepo("public virtual Task<int> AtualizarAsync");
        Assert.DoesNotContain("situacao_modelo_etiqueta = @situacao_modelo_etiqueta", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("situacao_modelo_etiqueta =", metodo, StringComparison.Ordinal);
        Assert.Contains("nome_modelo_etiqueta = @nome_modelo_etiqueta", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void ExisteNomeVersao_NaoFiltraSomenteAtivos()
    {
        string metodo = ExtrairMetodoRepo("public virtual async Task<bool> ExisteNomeVersaoAsync");
        Assert.DoesNotContain("situacao_modelo_etiqueta = true", metodo, StringComparison.Ordinal);
        Assert.Contains("upper(trim(nome_modelo_etiqueta)) = upper(trim(@nome_modelo_etiqueta))", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Inativacao_ContemNotExistsDeEtiquetaAtiva()
    {
        string metodo = ExtrairMetodoRepo("public virtual Task<int> ExcluirAsync");
        Assert.Contains("NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM etiqueta e", metodo, StringComparison.Ordinal);
        Assert.Contains("e.situacao_etiqueta = true", metodo, StringComparison.Ordinal);
    }

    [Fact]
    public void Reativacao_ContemNotExistsDeDuplicidade()
    {
        string metodo = ExtrairMetodoRepo("public virtual Task<int> ReativarAsync");
        Assert.Contains("NOT EXISTS", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM modelo_etiqueta outro", metodo, StringComparison.Ordinal);
        Assert.Contains("upper(trim(outro.nome_modelo_etiqueta)) = upper(trim(m.nome_modelo_etiqueta))", metodo, StringComparison.Ordinal);
    }

    private static string ExtrairMetodoRepo(string assinatura)
    {
        string fonte = File.ReadAllText(Path.Combine(RaizProjeto(), "AcessoDados", "Repositorio", "ModeloEtiquetaRepositorio.cs"));
        int inicio = fonte.IndexOf(assinatura, StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Método não encontrado: {assinatura}");
        int proximo = fonte.IndexOf("\n    public ", inicio + assinatura.Length, StringComparison.Ordinal);
        if (proximo < 0) proximo = fonte.Length;
        return fonte[inicio..proximo];
    }

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
