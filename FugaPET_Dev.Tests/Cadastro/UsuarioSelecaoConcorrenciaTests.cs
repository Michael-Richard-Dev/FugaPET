namespace FugaPET_Dev.Tests.Cadastro;

public sealed class UsuarioSelecaoConcorrenciaTests
{
    [Fact]
    public void SelecaoUsuario_DeveArmazenarTaskECancelarAnterior()
    {
        string tela = LerArquivo(
            "Tela",
            "Cadastro",
            "CadastroUsuarioForm.cs");

        Assert.Contains(
            "private Task _selecaoUsuarioTask = Task.CompletedTask;",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "_selecaoUsuarioTask = SetSelectedProfileRowAsync(rowPanel);",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "selecaoAnterior?.Cancel();",
            tela,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RespostaAntiga_NaoDevePreencherOutroUsuario()
    {
        string tela = LerArquivo(
            "Tela",
            "Cadastro",
            "CadastroUsuarioForm.cs");

        Assert.Contains(
            "UsuarioSolicitadoAindaEhAtual(idSolicitado)",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "_idUsuarioAtual == idUsuario",
            tela,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "_ = CarregarPerfilAtivoDoUsuarioAsync",
            tela,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SelecaoUsuario_DeveUsarDtoAgregado()
    {
        string tela = LerArquivo(
            "Tela",
            "Cadastro",
            "CadastroUsuarioForm.cs");
        string controller = LerArquivo(
            "Controle",
            "Cadastro",
            "UsuarioController.cs");

        Assert.Contains(
            "ObterEdicaoAgregadaAsync(",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "Task<UsuarioEdicaoAgregado?> ObterEdicaoAgregadaAsync(",
            controller,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ListarPerfisAsync(idSolicitado",
            tela,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ListarSetoresAsync(idSolicitado",
            tela,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_DeveLerUsuarioPerfilESetorEmUmComando()
    {
        string repositorio = LerArquivo(
            "AcessoDados",
            "Repositorio",
            "UsuarioRepositorio.cs");
        int inicio = repositorio.IndexOf(
            "public virtual async Task<UsuarioEdicaoAgregado?> ObterEdicaoAgregadaAsync(",
            StringComparison.Ordinal);
        int fim = repositorio.IndexOf(
            "public virtual async Task<bool> ExisteLoginAsync",
            inicio,
            StringComparison.Ordinal);
        string metodo = repositorio[inicio..fim];

        Assert.Equal(1, ContarOcorrencias(metodo, "new(sql, conexao)"));
        Assert.Contains("FROM usuario u", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM usuario_perfil up", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM usuario_setor us", metodo, StringComparison.Ordinal);
        Assert.Contains("setor_padrao = true", metodo, StringComparison.Ordinal);
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine([RaizProjeto(), .. partes]));

    private static int ContarOcorrencias(string texto, string valor)
    {
        int quantidade = 0;
        int indice = 0;
        while ((indice = texto.IndexOf(valor, indice, StringComparison.Ordinal)) >= 0)
        {
            quantidade++;
            indice += valor.Length;
        }

        return quantidade;
    }

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
