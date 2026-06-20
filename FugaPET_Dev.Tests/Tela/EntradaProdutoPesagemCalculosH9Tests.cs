using FugaPET_Dev.Modelo.Entrada;

namespace FugaPET_Dev.Tests.Tela;

/// <summary>
/// H9 Etapa 6 - regras de pesagem extraidas do Form para EntradaProdutoPesagemCalculos.
/// </summary>
public sealed class EntradaProdutoPesagemCalculosH9Tests
{
    [Fact]
    public void MontarLeitura_DeveCalcularLiquidoESequencia()
    {
        IReadOnlyList<EntradaProdutoPesagem> existentes = [Leitura(1)];

        EntradaProdutoPesagem nova = EntradaProdutoPesagemCalculos.MontarLeitura(
            existentes, pesoBruto: 10m, pesoTara: 2m, codigoTara: 7,
            origem: "BALANCA", codigoBalanca: 5, leituraOriginal: "10", pesadoEm: DateTimeOffset.Now);

        Assert.Equal(2, nova.Sequencia);
        Assert.Equal(8m, nova.PesoLiquidoKg);
        Assert.Equal("VALIDA", nova.StatusPesagem);
        Assert.Equal(7, nova.CodigoTara);
    }

    [Fact]
    public void MontarLeitura_Balanca_DeveGravarCodigoBalanca()
    {
        EntradaProdutoPesagem nova = EntradaProdutoPesagemCalculos.MontarLeitura(
            [], 10m, 2m, codigoTara: 7, origem: "BALANCA", codigoBalanca: 5,
            leituraOriginal: "10", pesadoEm: DateTimeOffset.Now);

        Assert.Equal(5, nova.CodigoBalanca);
    }

    [Fact]
    public void MontarLeitura_Manual_NaoDeveGravarCodigoBalanca()
    {
        EntradaProdutoPesagem nova = EntradaProdutoPesagemCalculos.MontarLeitura(
            [], 10m, 2m, codigoTara: 7, origem: "MANUAL", codigoBalanca: 5,
            leituraOriginal: "10", pesadoEm: DateTimeOffset.Now);

        Assert.Null(nova.CodigoBalanca);
    }

    [Theory]
    [InlineData(11, 1, true)]   // bruto > tara
    [InlineData(2, 2, false)]   // liquido zero
    [InlineData(0, 0, false)]   // bruto zero
    public void LeituraTemPesoValido_DeveSeguirBrutoMaiorQueTara(double bruto, double tara, bool esperado)
    {
        decimal liquido = EntradaProdutoPesagemCalculos.CalcularPesoLiquido((decimal)bruto, (decimal)tara);
        Assert.Equal(esperado, EntradaProdutoPesagemCalculos.LeituraTemPesoValido((decimal)bruto, liquido));
    }

    [Fact]
    public void Cancelar_DeveMarcarTodasComoCancelada()
    {
        IReadOnlyList<EntradaProdutoPesagem> canceladas =
            EntradaProdutoPesagemCalculos.Cancelar([Leitura(1), Leitura(2)]);

        Assert.All(canceladas, leitura => Assert.Equal("CANCELADA", leitura.StatusPesagem));
    }

    [Fact]
    public void DescreverOrigemConsolidada_DeveRefletirQuantidadeEOrigem()
    {
        Assert.Equal(string.Empty, EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada([]));
        Assert.Equal("DIGITADO", EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada([Leitura(1, "MANUAL")]));
        Assert.Equal("LIDO", EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada([Leitura(1, "BALANCA")]));
        Assert.Equal("MULTIPLA", EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada([Leitura(1), Leitura(2)]));
    }

    [Fact]
    public void DescreverOrigemConsolidada_DeveIgnorarCanceladas()
    {
        IReadOnlyList<EntradaProdutoPesagem> leituras =
            [Leitura(1, "MANUAL"), Leitura(2) with { StatusPesagem = "CANCELADA" }];

        Assert.Equal("DIGITADO", EntradaProdutoPesagemCalculos.DescreverOrigemConsolidada(leituras));
    }

    private static EntradaProdutoPesagem Leitura(int sequencia, string origem = "BALANCA")
        => new()
        {
            Sequencia = sequencia,
            PesoBrutoKg = 10m,
            PesoTaraKg = 2m,
            PesoLiquidoKg = 8m,
            Origem = origem,
            StatusPesagem = "VALIDA"
        };
}
