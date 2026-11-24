namespace FugaPET_Dev.Tests.Cadastro;

public sealed class CampoEtiquetaSelecaoConcorrenciaTests
{
    [Fact]
    public void Selecoes_DeveArmazenarTasksECancelarConsultasAnteriores()
    {
        string tela = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.cs");

        Assert.Contains("private Task _carregarCamposTask = Task.CompletedTask;", tela, StringComparison.Ordinal);
        Assert.Contains("private Task _selecaoCampoTask = Task.CompletedTask;", tela, StringComparison.Ordinal);
        Assert.Contains("_carregarCamposTask = CarregarCamposAsync();", tela, StringComparison.Ordinal);
        Assert.Contains("_selecaoCampoTask = AoSelecionarCampoAsync();", tela, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _carregarCamposCts, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("Interlocked.Exchange(ref _selecaoCampoCts, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("anterior?.Cancel();", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void RespostasAntigas_NaoDevemPreencherOutraEtiquetaOuCampo()
    {
        string tela = LerArquivo("Tela", "Cadastro", "CamposEtiquetaForm.cs");

        Assert.Contains("EtiquetaSolicitadaAindaEhAtual(idEtiqueta, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("CampoSolicitadoAindaEhAtual(idSolicitado, atual)", tela, StringComparison.Ordinal);
        Assert.Contains("EtiquetaSelecionada == idEtiqueta", tela, StringComparison.Ordinal);
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

        // ANCORA NO NOME DO METODO, nao na assinatura inteira. A ancora antiga exigia
        // "public async Task<...>" e quebrou quando o metodo virou "public VIRTUAL async Task<...>"
        // — mudanca que nada tem a ver com o que este teste afirma (ler campo e mapeamento num
        // comando so). Pior: com inicio = -1 o recorte estourava ArgumentOutOfRange, e a falha
        // aparecia como defeito de codigo em vez de ancora velha. Custou uma investigacao inteira
        // em 24/11/2026.
        int inicio = repositorio.IndexOf(
            "Task<CampoEtiquetaEdicaoAgregado?> ObterEdicaoAgregadaAsync(",
            StringComparison.Ordinal);
        Assert.True(inicio >= 0, "metodo ObterEdicaoAgregadaAsync nao encontrado no repositorio");

        int fim = repositorio.IndexOf(
            "Task<bool> ExisteNomeNaEtiquetaAsync",
            inicio,
            StringComparison.Ordinal);
        Assert.True(fim > inicio, "ancora de fim (ExisteNomeNaEtiquetaAsync) nao encontrada apos o metodo");

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
