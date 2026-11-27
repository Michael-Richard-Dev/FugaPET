using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// A LEITURA FRESCA DO 261 USA O ITEM DO MATERIAL DA CAIXA, nao "o primeiro que o SAP devolver".
/// <para>
/// A OP pode ter mais de um item. Medido no SAP em 27/11/2026, OP 1000008 do mandante 130: item 1 =
/// 4000168 (o produto acabado, 200 UN, PA01) e item 2 = 2000183 (co-produto, 0,100 KG, PP05). A
/// leitura fresca pegava <c>itens[0]</c> — e ela alimenta o alocador do 261, que decide POSTAGEM
/// REAL, nao exibicao.
/// </para>
/// </summary>
public sealed class LeituraFresca261ItemDoMaterialTests
{
    [Theory]
    [InlineData("4000168", "1")]
    [InlineData("2000183", "2")]
    [InlineData("  4000168  ", "1")]
    public void ComMaterialDaCaixa_EscolheOItemDaqueleMaterial(string material, string itemEsperado)
        => Assert.Equal(
            itemEsperado,
            ProductionOrderSapApiClient.EscolherItemFresco(ItensDaOrdem(), material)!.ItemOrdem);

    /// <summary>
    /// MATERIAL PEDIDO E NENHUM ITEM DELE ⇒ NULO, e o chamador bloqueia. Escolher "o mais parecido"
    /// seria postar 261 com a quantidade de outro produto.
    /// </summary>
    [Fact]
    public void MaterialQueNaoEstaNaOrdem_NaoEscolheNada()
        => Assert.Null(ProductionOrderSapApiClient.EscolherItemFresco(ItensDaOrdem(), "9999999"));

    /// <summary>
    /// SEM MATERIAL PEDIDO, o de MENOR numero — comparado como NUMERO, senao "10" viria antes de "2".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SemMaterial_PegaOMenorItem(string? material)
        => Assert.Equal("1", ProductionOrderSapApiClient.EscolherItemFresco(ItensDaOrdem(), material)!.ItemOrdem);

    [Fact]
    public void SemMaterial_OrdenaPorNumeroENaoPorTexto()
    {
        ItemOrdemProducaoSap[] itens =
        [
            new() { ItemOrdem = "10", Material = "A" },
            new() { ItemOrdem = "2", Material = "B" }
        ];

        Assert.Equal("2", ProductionOrderSapApiClient.EscolherItemFresco(itens, null)!.ItemOrdem);
    }

    [Fact]
    public void SemItens_NaoEscolheNada()
        => Assert.Null(ProductionOrderSapApiClient.EscolherItemFresco([], "4000168"));

    /// <summary>
    /// O ALOCADOR PASSA O MATERIAL DA CAIXA. Sem isso a correcao acima nao chega onde decide o 261.
    /// </summary>
    [Fact]
    public async Task Alocador_PassaOMaterialDaCaixaParaALeituraFresca()
    {
        FakeOrdemFresca fake = new();
        ProdutoAcabadoController controlador = new(fake);

        await controlador.CalcularAlocacao261FrescaAsync(new ProdutoAcabadoCaixa
        {
            NumeroOrdemProducao = "1000008",
            Material = "4000168",
            ItemOrdemProducao = "1"
        });

        Assert.Equal("4000168", fake.MaterialRecebido);
    }

    private static ItemOrdemProducaoSap[] ItensDaOrdem() =>
    [
        new() { ItemOrdem = "1", Material = "4000168", QuantidadePrevista = 200m, Unidade = "UN", Deposito = "PA01" },
        new() { ItemOrdem = "2", Material = "2000183", QuantidadePrevista = 0.100m, Unidade = "KG", Deposito = "PP05" }
    ];

    private sealed class FakeOrdemFresca : IProductionOrderSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;
        public string? MaterialRecebido { get; private set; }

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem, CancellationToken cancellationToken = default)
            => Task.FromResult(ResultadoConsultaOrdemProducaoSap.NaoEncontrada());

        public Task<LeituraFrescaOrdem261> ConsultarEstadoFresco261Async(
            string numeroOrdem,
            CancellationToken cancellationToken = default,
            string? materialEsperado = null)
        {
            MaterialRecebido = materialEsperado;
            return Task.FromResult(LeituraFrescaOrdem261.Indisponivel(numeroOrdem, "fake"));
        }
    }
}
