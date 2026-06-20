namespace FugaPET_Dev.Tests.Cadastro;

public sealed class PermissaoSelecaoConcorrenciaTests
{
    [Fact]
    public void SelecaoPerfil_DeveArmazenarTaskECancelarConsultaAnterior()
    {
        string tela = LerArquivo("Tela", "Cadastro", "PermissaoForm.cs");

        Assert.Contains(
            "private Task _carregarPermissoesPerfilTask = Task.CompletedTask;",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "_carregarPermissoesPerfilTask = CarregarPermissoesDoPerfilSelecionadoAsync();",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "Interlocked.Exchange(ref _carregarPermissoesPerfilCts, atual)",
            tela,
            StringComparison.Ordinal);
        Assert.Contains("anterior?.Cancel();", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void RespostaAntiga_NaoDevePreencherOutroPerfil()
    {
        string tela = LerArquivo("Tela", "Cadastro", "PermissaoForm.cs");

        Assert.Contains(
            "PerfilSolicitadoAindaEhAtual(codigoPerfil, atual)",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "ObterCodigoPerfilSelecionado() == codigoPerfil",
            tela,
            StringComparison.Ordinal);
        Assert.Contains(
            "ListarCodigosPermissaoPorPerfilAsync(codigoPerfil, atual.Token)",
            tela,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FechamentoTela_DeveCancelarConsultaPendente()
    {
        string tela = LerArquivo("Tela", "Cadastro", "PermissaoForm.cs");

        int inicio = tela.IndexOf(
            "protected override void OnFormClosed(FormClosedEventArgs e)",
            StringComparison.Ordinal);
        string fechamento = tela[inicio..];

        Assert.Contains(
            "CancelarCarregamentoPermissoesPerfil();",
            fechamento,
            StringComparison.Ordinal);
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
