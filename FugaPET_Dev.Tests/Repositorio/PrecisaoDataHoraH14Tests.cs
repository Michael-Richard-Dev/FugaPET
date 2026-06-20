namespace FugaPET_Dev.Tests.Repositorio;

public sealed class PrecisaoDataHoraH14Tests
{
    [Fact]
    public void Login_NaoDeveTruncarTimestamp()
    {
        string repositorio = LerArquivo(
            "AcessoDados",
            "Repositorio",
            "UsuarioRepositorio.cs");

        int inicio = repositorio.IndexOf(
            "public async Task AtualizarUltimoLoginAsync",
            StringComparison.Ordinal);
        string metodo = repositorio[inicio..];

        Assert.Contains("ultimo_login_em = clock_timestamp()", metodo, StringComparison.Ordinal);
        Assert.DoesNotContain("date_trunc", metodo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Incremental_DevePriorizarEventosSemAlterarTiposOuIndices()
    {
        string script = LerArquivo(
            "BancoDados",
            "001_incrementais",
            "024_h14_precisao_data_hora_auditoria_PROPOSTA_GAIA.sql");

        Assert.Contains("clock_timestamp()", script, StringComparison.Ordinal);
        Assert.Contains("fn_definir_atualizado_em", script, StringComparison.Ordinal);
        Assert.Contains("entrada_produto_pesagem", script, StringComparison.Ordinal);
        Assert.Contains("pesagem_entrada_item_leitura", script, StringComparison.Ordinal);
        Assert.Contains("auditoria_acao_usuario", script, StringComparison.Ordinal);
        Assert.Contains("sap_sincronizacao_execucao", script, StringComparison.Ordinal);
        Assert.Contains("versao_banco", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER COLUMN pesado_em TYPE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP INDEX", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REINDEX", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Incremental_NaoDeveReescreverHistorico()
    {
        string script = LerArquivo(
            "BancoDados",
            "001_incrementais",
            "024_h14_precisao_data_hora_auditoria_PROPOSTA_GAIA.sql");

        Assert.DoesNotContain(
            "UPDATE desenvolvimento.entrada_produto_pesagem",
            script,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "UPDATE desenvolvimento.auditoria_acao_usuario",
            script,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine([RaizProjeto(), .. partes]));

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
