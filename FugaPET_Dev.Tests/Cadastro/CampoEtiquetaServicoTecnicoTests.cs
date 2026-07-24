namespace FugaPET_Dev.Tests.Cadastro;

public sealed class CampoEtiquetaServicoTecnicoTests
{
    private static readonly string Servico = LerArquivo("Servicos", "Cadastro", "CampoEtiquetaServico.cs");
    private static readonly string Modelo = LerArquivo("Modelo", "Cadastro", "CampoEtiquetaCadastro.cs");

    [Fact]
    public void Modelo_DefineLimitesFuncionais()
    {
        Assert.Contains("TamanhoMinimoNome = 2", Modelo, StringComparison.Ordinal);
        Assert.Contains("TamanhoMaximoNome = 80", Modelo, StringComparison.Ordinal);
        Assert.Contains("TamanhoMaximoDescricao = 255", Modelo, StringComparison.Ordinal);
        Assert.Contains("TamanhoMaximoFormatoSaida = 100", Modelo, StringComparison.Ordinal);
    }

    [Fact]
    public void Servico_ValidaNomeTipoOrdemTamanhoDescricaoEFormato()
    {
        Assert.Contains("Nome do campo deve ter entre 2 e 80 caracteres.", Servico, StringComparison.Ordinal);
        Assert.Contains("Tipo de dado inválido", Servico, StringComparison.Ordinal);
        Assert.Contains("Ordem deve ser maior que zero.", Servico, StringComparison.Ordinal);
        Assert.Contains("Tamanho máximo deve ser maior que zero quando informado.", Servico, StringComparison.Ordinal);
        Assert.Contains("Descrição do campo deve ter no máximo 255 caracteres.", Servico, StringComparison.Ordinal);
        Assert.Contains("Formato de saída deve ter no máximo 100 caracteres.", Servico, StringComparison.Ordinal);
    }

    [Fact]
    public void Servico_BloqueiaSituacaoEtiquetaEDuplicidadeGlobal()
    {
        Assert.Contains("MensagemNovaDeveSerAtiva", Servico, StringComparison.Ordinal);
        Assert.Contains("MensagemInativarPelaAcao", Servico, StringComparison.Ordinal);
        Assert.Contains("MensagemReativarPelaAcao", Servico, StringComparison.Ordinal);
        Assert.Contains("EtiquetaEstaAtivaAsync", Servico, StringComparison.Ordinal);
        Assert.Contains("MensagemDuplicidadeGlobal", Servico, StringComparison.Ordinal);
        Assert.Contains("Não é permitido alterar a etiqueta do campo.", Servico, StringComparison.Ordinal);
    }

    [Fact]
    public void Servico_UsaTelaAuditoriaCorreta()
    {
        Assert.Contains("private const string Tela = \"CamposEtiquetaForm\";", Servico, StringComparison.Ordinal);
        Assert.DoesNotContain("private const string Tela = \"EtiquetaForm\";", Servico, StringComparison.Ordinal);
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
