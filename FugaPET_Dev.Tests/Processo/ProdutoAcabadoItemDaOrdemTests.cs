using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// A TELA DE PRODUTO ACABADO MOSTRA O PRODUTO DA ORDEM, nao um co-produto.
/// <para>
/// Defeito real no QAS130, 27/11/2026: a tela trouxe 2000183 (0,100 KG, deposito PP05) como "produto
/// acabado" da OP 1000008. Consultado o SAP 130, a ordem tem DOIS itens — item 1 = 4000168 (200 UN,
/// PA01), que e o Material do CABECALHO e o produto acabado, e item 2 = 2000183, co-produto. A escolha
/// era <c>Itens.FirstOrDefault()</c>, sem $orderby na consulta e sem vinculo com o material da ordem:
/// qual item aparecia dependia da ordem em que o SAP devolvia a colecao.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoItemDaOrdemTests
{
    /// <summary>O caso REAL da OP 1000008, com os itens na ordem que mostra o defeito.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OItemEscolhido_EODoMaterialDoCabecalho(bool coProdutoPrimeiro)
    {
        ItemOrdemProducaoSap produtoAcabado = new()
        {
            ItemOrdem = "1",
            Material = "4000168",
            QuantidadePrevista = 200m,
            Unidade = "UN",
            Deposito = "PA01"
        };

        ItemOrdemProducaoSap coProduto = new()
        {
            ItemOrdem = "2",
            Material = "2000183",
            QuantidadePrevista = 0.100m,
            Unidade = "KG",
            Deposito = "PP05"
        };

        OrdemProducaoSap ordem = new()
        {
            NumeroOrdem = "1000008",
            MaterialProduzido = "4000168",
            Centro = "3007",
            Itens = coProdutoPrimeiro ? [coProduto, produtoAcabado] : [produtoAcabado, coProduto]
        };

        ItemOrdemProducaoSap? escolhido = ProdutoAcabadoController.EscolherItemDoProdutoAcabado(ordem);

        Assert.NotNull(escolhido);
        Assert.Equal("4000168", escolhido!.Material);
        Assert.Equal("1", escolhido.ItemOrdem);
    }

    /// <summary>
    /// SEM ITEM QUE BATA COM O CABECALHO, vale o de MENOR numero — e por numero, nao por texto, senao
    /// "10" viria antes de "2".
    /// </summary>
    [Fact]
    public void SemItemDoCabecalho_ValeODeMenorNumero()
    {
        OrdemProducaoSap ordem = new()
        {
            NumeroOrdem = "1000009",
            MaterialProduzido = "9999999",
            Itens =
            [
                new ItemOrdemProducaoSap { ItemOrdem = "10", Material = "A" },
                new ItemOrdemProducaoSap { ItemOrdem = "2", Material = "B" }
            ]
        };

        Assert.Equal("2", ProdutoAcabadoController.EscolherItemDoProdutoAcabado(ordem)!.ItemOrdem);
    }

    /// <summary>Sem item nenhum, nao inventa: devolve nulo e o chamador cai no material do cabecalho.</summary>
    [Fact]
    public void SemItens_NaoInventa()
        => Assert.Null(ProdutoAcabadoController.EscolherItemDoProdutoAcabado(
            new OrdemProducaoSap { NumeroOrdem = "1000010", MaterialProduzido = "4000168" }));

    /// <summary>
    /// A CONSULTA DA COLECAO DE ITENS PEDE ORDENACAO. Sem $orderby, a colecao volta na ordem que o SAP
    /// quiser e a escolha do item voltaria a depender disso no caminho de fallback.
    /// </summary>
    [Fact]
    public void AConsultaDeItens_PedeOrdenacao()
    {
        string fonte = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Servicos", "IntegracaoSap", "ProductionOrderSapApiClient.cs"));

        Assert.Contains(
            "MontarUrlColecao(\"A_ProductionOrderItem_2\", numeroOrdem, ordenarPor: \"ManufacturingOrderItem\")",
            fonte,
            StringComparison.Ordinal);
    }

    private static string RaizProjeto()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FugaPET_Dev.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("raiz do projeto nao encontrada");
    }
}
