namespace FugaPET_Dev.Tests.Cadastro;

public sealed class CampoEtiquetaSelecaoConcorrenciaTests
{
    [Fact]
    public void Selecoes_DeveArmazenarTasksECancelarConsultasAnteriores()
    {
        string tela = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.cs");

        Assert.Contains("private Task _carregarCamposTask = Task.CompletedTask;", tela, StringComparison.Ordinal);
        Assert.Contains("private Task _selecaoCampoTask = Task.CompletedTask;", tela, StringComparison.Ordinal);
        Assert.Contains("await CarregarCamposAsync();", tela, StringComparison.Ordinal);
        Assert.Contains("_selecaoCampoTask = AoSelecionarCampoAsync(campo.CodigoCampoEtiqueta);", tela, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _carregarCamposCts, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _selecaoCampoCts, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("anterior?.Cancel();", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void RespostasAntigas_NaoDevemPreencherOutraEtiquetaOuCampo()
    {
        string tela = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.cs");

        Assert.Contains("EtiquetaSolicitadaAindaEhAtual(atual)", tela, StringComparison.Ordinal);
        Assert.Contains("CampoSolicitadoAindaEhAtual(idSolicitado, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("_etiquetaPreSelecionada > 0", tela, StringComparison.Ordinal);
        Assert.Contains("_idCampoAtual == idCampo", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("_ = CarregarMapeamentoAsync", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void SelecaoCampo_DeveUsarDtoAgregado()
    {
        string tela = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.cs");
        string controller = LerArquivo("Controle", "Cadastro", "CampoEtiquetaController.cs");

        Assert.Contains("CampoEtiquetaEdicaoAgregado?", tela, StringComparison.Ordinal);
        Assert.Contains("ObterEdicaoAgregadaAsync(idSolicitado, atual.Token)", tela, StringComparison.Ordinal);
        Assert.Contains("Task<CampoEtiquetaEdicaoAgregado?> ObterEdicaoAgregadaAsync", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("_mapeamentoController.ObterAtivoPorCampoAsync", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void Repositorio_DeveLerCampoEMapeamentoEmUmComando()
    {
        string repositorio = LerArquivo("AcessoDados", "Repositorio", "CampoEtiquetaRepositorio.cs");
        int inicio = repositorio.IndexOf(
            "public virtual async Task<CampoEtiquetaEdicaoAgregado?> ObterEdicaoAgregadaAsync(",
            StringComparison.Ordinal);
        int fim = repositorio.IndexOf(
            "public virtual async Task<bool> ExisteNomeNaEtiquetaAsync",
            inicio,
            StringComparison.Ordinal);
        string metodo = repositorio[inicio..fim];

        Assert.Equal(1, ContarOcorrencias(metodo, "new(sql, conexao)"));
        Assert.Contains("FROM campo_etiqueta c", metodo, StringComparison.Ordinal);
        Assert.Contains("FROM mapeamento_campo_etiqueta", metodo, StringComparison.Ordinal);
        Assert.Contains("LEFT JOIN LATERAL", metodo, StringComparison.Ordinal);
        Assert.Contains("situacao_mapeamento_campo_etiqueta = true", metodo, StringComparison.Ordinal);
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

