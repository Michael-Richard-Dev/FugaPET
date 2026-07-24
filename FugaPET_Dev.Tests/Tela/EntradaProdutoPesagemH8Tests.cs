using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Tests.Tela;

public sealed class EntradaProdutoPesagemH8Tests
{
    [Fact]
    public void DuasLeiturasMesmoItem_DevemDescontarTaraEmCadaLeitura()
    {
        IReadOnlyList<EntradaProdutoPesagem> leituras =
        [
            Leitura(1, bruto: 10m, tara: 1m),
            Leitura(2, bruto: 20m, tara: 1m)
        ];

        Assert.Equal(30m, EntradaProdutoPesagemCalculos.SomarPesoBrutoValido(leituras));
        Assert.Equal(2m, EntradaProdutoPesagemCalculos.SomarPesoTaraValido(leituras));
        Assert.Equal(28m, EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(leituras));
    }

    [Fact]
    public void LeituraCancelada_DevePermanecerMasNaoComporTotal()
    {
        IReadOnlyList<EntradaProdutoPesagem> leituras =
        [
            Leitura(1, bruto: 10m, tara: 1m),
            Leitura(2, bruto: 20m, tara: 1m) with { StatusPesagem = "CANCELADA" }
        ];

        Assert.Equal(2, leituras.Count);
        Assert.Equal("CANCELADA", leituras[1].StatusPesagem);
        Assert.Equal(9m, EntradaProdutoPesagemCalculos.SomarPesoLiquidoValido(leituras));
    }

    [Fact]
    public void Sequencias_DevemSerNormalizadasSemRemoverLeituras()
    {
        IReadOnlyList<EntradaProdutoPesagem> leituras =
            EntradaProdutoPesagemCalculos.ValidarSequencias(
            [
                Leitura(8, bruto: 10m, tara: 1m),
                Leitura(15, bruto: 20m, tara: 1m)
            ]);

        Assert.Equal([1, 2], leituras.Select(leitura => leitura.Sequencia));
    }

    [Fact]
    public void Repositorio_DevePersistirCadaLeituraECalcularTotalDoBanco()
    {
        string conteudo = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "AcessoDados",
            "Repositorio",
            "EntradaProdutoRepositorio.cs"));

        Assert.Contains("foreach (EntradaProdutoPesagem pesagem in pesagens)", conteudo);
        Assert.Contains("status_pesagem", conteudo);
        Assert.Contains("pesado_em", conteudo);
        Assert.Contains("SUM(pesagem.peso_liquido_kg)", conteudo);
        Assert.Contains("pesagem.status_pesagem = 'VALIDA'", conteudo);
    }

    [Fact]
    public void Reimpressao_DeveExigirLancamentoPersistido()
    {
        string conteudo = File.ReadAllText(Path.Combine(
            RaizProjeto(),
            "Tela",
            "Processo",
            "ProcessoEntradaProdutoForm.cs"));

        // Reimpressão persistida agora abre a relação de pesagens e reimprime POR PESAGEM (peso líquido),
        // usando os dados persistidos (não o total do item).
        Assert.Contains("_codigoLancamentoPersistido is long codigoLancamento", conteudo);
        Assert.Contains("ObterItemPersistidoAsync", conteudo);
        Assert.Contains("ListarPesagensPersistidasAsync", conteudo);
        Assert.Contains("MontarEtiquetaPorPesagem(itemPersistido", conteudo);
        // Não usa mais a etiqueta consolidada do item na reimpressão.
        Assert.DoesNotContain("MontarEtiqueta(itemPersistido", conteudo);
    }

    private static EntradaProdutoPesagem Leitura(
        int sequencia,
        decimal bruto,
        decimal tara)
        => new()
        {
            Sequencia = sequencia,
            PesoBrutoKg = bruto,
            PesoTaraKg = tara,
            PesoLiquidoKg = bruto - tara,
            Origem = "BALANCA",
            StatusPesagem = "VALIDA",
            PesadoEm = DateTimeOffset.Now
        };

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
