using FugaPET_Dev.Controle.Processo;
using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// O SALDO DA OP VEM DO SAP DE NOVO DEPOIS DE CADA ENVIO.
/// <para>
/// Pedido do usuario em 27/11/2026: "quando for pesando ja vai atualizando as informacoes de saldo
/// pendente (...) esse calculo, que voce fez pela tela, o mais correto e fazer por API, verificando
/// o que ja foi para o SAP".
/// </para>
/// <para>
/// A tela lia o saldo UMA vez, na consulta da OP, e dai em diante so descontava as caixas LOCAIS.
/// Caixa confirmada deixa de ser local e passa a contar no SAP — mas a base nunca era relida, entao
/// o cabecalho continuava mostrando a entrega de antes do envio (na foto: 40 entregues / 16
/// pendentes com as duas caixas ja ConfirmadaSap).
/// </para>
/// </summary>
public sealed class ProdutoAcabadoReleituraSaldoSapTests
{
    /// <summary>
    /// A RELEITURA TRAZ AS QUANTIDADES FRESCAS — e do item do PRODUTO da ordem, nao de um co-produto.
    /// </summary>
    [Fact]
    public async Task Releitura_TrazQuantidadesFrescasDoItemDoProduto()
    {
        ProdutoAcabadoController controlador = new(new FakeOrdem(OrdemComDoisItens(entregue: 56m)));

        ProdutoAcabadoOrdem? ordem = await controlador.ReconsultarQuantidadesDaOrdemAsync("1000452");

        Assert.NotNull(ordem);
        Assert.Equal("1000452", ordem!.NumeroOrdem);
        Assert.Equal("4000174", ordem.MaterialProduzido);
        Assert.Equal(56m, ordem.QuantidadePlanejada);
        Assert.Equal(56m, ordem.QuantidadeRecebidaSap);
        Assert.Equal(0m, ordem.QuantidadePendente);
    }

    /// <summary>
    /// SALDO ZERADO NAO E FALHA NA RELEITURA. Chegar a zero e o fim normal da producao; a consulta
    /// INICIAL recusa a OP nesse caso, mas recusar aqui apagaria o numero certo da tela.
    /// </summary>
    [Fact]
    public async Task SaldoZerado_ContinuaSendoDevolvido()
    {
        ProdutoAcabadoController controlador = new(new FakeOrdem(OrdemComDoisItens(entregue: 56m)));

        ProdutoAcabadoOrdem? ordem = await controlador.ReconsultarQuantidadesDaOrdemAsync("1000452");

        Assert.NotNull(ordem);
        Assert.Equal(0m, ordem!.QuantidadePendente);
    }

    /// <summary>
    /// SAP INDISPONIVEL DEVOLVE NULO — a tela mantem o ultimo valor conhecido. Inventar numero aqui
    /// ou barra producao legitima, ou deixa produzir alem da ordem.
    /// </summary>
    [Fact]
    public async Task SapIndisponivel_DevolveNulo()
    {
        ProdutoAcabadoController controlador = new(new FakeOrdem(null));

        Assert.Null(await controlador.ReconsultarQuantidadesDaOrdemAsync("1000452"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SemNumeroDeOrdem_NaoChamaOSap(string numero)
    {
        FakeOrdem fake = new(OrdemComDoisItens(entregue: 40m));
        ProdutoAcabadoController controlador = new(fake);

        Assert.Null(await controlador.ReconsultarQuantidadesDaOrdemAsync(numero));
        Assert.Equal(0, fake.Chamadas);
    }

    /// <summary>
    /// OS TRES CAMINHOS DE ENVIO RELEEM. Envio individual, envio em lote e pipeline 261→101→HU: todos
    /// terminam no mesmo lugar, e e la que a base do saldo precisa ser renovada. Fora dos tres, a
    /// pesagem continua abatendo localmente, que ja funcionava.
    /// </summary>
    [Fact]
    public void OsTresCaminhosDeEnvio_ReleemAOp()
    {
        string fonte = File.ReadAllText(Path.Combine(
            RaizProjeto(), "Tela", "Processo", "ProcessoProdutoAcabadoForm.cs"));

        int chamadas = Ocorrencias(fonte, "await ReconsultarQuantidadesDaOpAsync();");

        Assert.True(
            chamadas == 3,
            $"esperava a releitura nos TRES caminhos de envio; achei {chamadas} chamada(s)");
    }

    private static int Ocorrencias(string texto, string trecho)
    {
        int total = 0;
        int posicao = 0;
        while ((posicao = texto.IndexOf(trecho, posicao, StringComparison.Ordinal)) >= 0)
        {
            total++;
            posicao += trecho.Length;
        }

        return total;
    }

    /// <summary>A OP 1000452 como o SAP 130 a devolve: item 1 o produto, item 2 um co-produto.</summary>
    private static OrdemProducaoSap OrdemComDoisItens(decimal entregue) => new()
    {
        NumeroOrdem = "1000452",
        MaterialProduzido = "4000174",
        Centro = "3007",
        Unidade = "UN",
        Liberada = true,
        Itens =
        [
            new ItemOrdemProducaoSap
            {
                ItemOrdem = "1",
                Material = "4000174",
                QuantidadePrevista = 56m,
                QuantidadeRecebidaSap = entregue,
                Unidade = "UN",
                Deposito = "PA01"
            },
            new ItemOrdemProducaoSap
            {
                ItemOrdem = "2",
                Material = "2000183",
                QuantidadePrevista = 0.100m,
                QuantidadeRecebidaSap = 0m,
                Unidade = "KG",
                Deposito = "PP05"
            }
        ]
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

    private sealed class FakeOrdem(OrdemProducaoSap? ordem) : IProductionOrderSapServico
    {
        public bool EhSimulado => false;
        public bool Configurado => true;
        public int Chamadas { get; private set; }

        public Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
            string numeroOrdem,
            CancellationToken cancellationToken = default)
        {
            Chamadas++;
            return Task.FromResult(ordem is null
                ? ResultadoConsultaOrdemProducaoSap.NaoEncontrada()
                : ResultadoConsultaOrdemProducaoSap.Encontrada(ordem));
        }
    }
}
