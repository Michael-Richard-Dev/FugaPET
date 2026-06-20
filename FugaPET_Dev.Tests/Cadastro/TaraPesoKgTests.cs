using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Tests.Cadastro;

public sealed class TaraPesoKgTests
{
    [Fact]
    public void Modelo_DeveExporSomentePesoKgComoPesoDaTara()
    {
        Assert.NotNull(typeof(TaraCadastro).GetProperty(nameof(TaraCadastro.PesoKg)));
        Assert.Null(typeof(TaraCadastro).GetProperty("PesoGrama"));
        Assert.Equal(typeof(decimal), typeof(TaraCadastro).GetProperty(nameof(TaraCadastro.PesoKg))!.PropertyType);
    }

    [Fact]
    public void Repositorio_DeveUsarSomenteColunaPesoKg()
    {
        string repositorio = LerArquivo("AcessoDados", "Repositorio", "TaraRepositorio.cs");

        Assert.Contains("peso_kg", repositorio, StringComparison.Ordinal);
        Assert.DoesNotContain("peso_grama", repositorio, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("information_schema", repositorio, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Telas_DeveExibirTaraEmQuilogramas()
    {
        string selecao = LerArquivo("Tela", "Processo", "SelecaoTaraPesagemForm.cs");
        string multipla = LerArquivo("Tela", "Processo", "PesagemMultiplaItemForm.cs");
        string entrada = LerArquivo("Tela", "Processo", "ProcessoEntradaProdutoForm.cs");
        string cadastroDesigner = LerArquivo("Tela", "Cadastro", "TaraForm.Designer.cs");

        foreach (string conteudo in new[] { selecao, multipla, entrada, cadastroDesigner })
        {
            Assert.DoesNotContain("PesoGrama", conteudo, StringComparison.Ordinal);
        }

        Assert.Contains("Peso (kg)", selecao, StringComparison.Ordinal);
        Assert.Contains("kg)", multipla, StringComparison.Ordinal);
        Assert.Contains(" kg)", entrada, StringComparison.Ordinal);
        Assert.Contains("Peso (kg)", cadastroDesigner, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculoPesoLiquido_DeveSubtrairQuilogramasSemConversao()
    {
        decimal pesoLiquido =
            EntradaProdutoPesagemCalculos.CalcularPesoLiquido(
                pesoBruto: 10.500m,
                pesoTara: 1.250m);

        Assert.Equal(9.250m, pesoLiquido);
    }

    [Fact]
    public void Impressao_DeveUsarPesoLiquidoPersistidoEmKg()
    {
        string impressao = LerArquivo("Servicos", "Operacao", "ImpressaoEntradaServico.cs");

        Assert.Contains("item.PesoLiquidoTotalKg", impressao, StringComparison.Ordinal);
        Assert.DoesNotContain("PesoGrama", impressao, StringComparison.Ordinal);
        Assert.DoesNotContain("* 1000", impressao, StringComparison.Ordinal);
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
