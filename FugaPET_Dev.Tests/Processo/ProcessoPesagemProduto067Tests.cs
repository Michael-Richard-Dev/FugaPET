using FugaPET_Dev.AcessoDados.Repositorio;
using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Resolucao do processo de pesagem a partir do vinculo (19/11/2026).
/// <para>
/// O caso que mais importa aqui e o do produto SEM vinculo: ele nao pode parar a pesagem. Cadastro
/// faltando se resolve cadastrando, nao parando a fabrica.
/// </para>
/// </summary>
public sealed class ProcessoPesagemProduto067Tests
{
    [Fact]
    public async Task ProdutoMarcadoComoEnvioUnico_ResolveEnvioUnico()
    {
        ProcessoPesagemProdutoServico servico = new(new RepositorioFalso(ProcessoPesagemProduto.EnvioUnico));

        Assert.Equal(ProcessoPesagemProduto.EnvioUnico, await servico.ObterProcessoAsync("3007", "4000123"));
        Assert.True(await servico.EhEnvioUnicoAsync("3007", "4000123"));
    }

    [Fact]
    public async Task ProdutoMarcadoComoCaixaACaixa_NaoEEnvioUnico()
    {
        ProcessoPesagemProdutoServico servico = new(new RepositorioFalso(ProcessoPesagemProduto.CaixaACaixa));

        Assert.Equal(ProcessoPesagemProduto.CaixaACaixa, await servico.ObterProcessoAsync("3007", "4000123"));
        Assert.False(await servico.EhEnvioUnicoAsync("3007", "4000123"));
    }

    /// <summary>Sem vinculo: nulo no processo, e a pesagem segue no comportamento historico.</summary>
    [Fact]
    public async Task ProdutoSemVinculo_NaoBloqueiaEFicaNoHistorico()
    {
        ProcessoPesagemProdutoServico servico = new(new RepositorioFalso(null));

        Assert.Null(await servico.ObterProcessoAsync("3007", "4000999"));
        Assert.False(await servico.EhEnvioUnicoAsync("3007", "4000999"));
    }

    /// <summary>Ordem ainda nao carregada nao e pergunta legitima: nao vai ao banco.</summary>
    [Theory]
    [InlineData(null, "4000123")]
    [InlineData("3007", null)]
    [InlineData("", "4000123")]
    [InlineData("3007", "   ")]
    public async Task CentroOuProdutoEmBranco_NaoConsultaOBanco(string? centro, string? produto)
    {
        RepositorioFalso repositorio = new(ProcessoPesagemProduto.EnvioUnico);
        ProcessoPesagemProdutoServico servico = new(repositorio);

        Assert.Null(await servico.ObterProcessoAsync(centro, produto));
        Assert.Equal(0, repositorio.Consultas);
    }

    [Fact]
    public async Task CentroEProduto_ChegamAoBancoSemEspacos()
    {
        RepositorioFalso repositorio = new(ProcessoPesagemProduto.EnvioUnico);
        ProcessoPesagemProdutoServico servico = new(repositorio);

        await servico.ObterProcessoAsync("  3007 ", " 4000123  ");

        Assert.Equal("3007", repositorio.UltimoCentro);
        Assert.Equal("4000123", repositorio.UltimoProduto);
    }

    // ------------------------------------------------------------------
    private sealed class RepositorioFalso : VinculoProdutoEtiquetaRepositorio
    {
        private readonly ProcessoPesagemProduto? _processo;

        public RepositorioFalso(ProcessoPesagemProduto? processo) : base(null!) => _processo = processo;

        public int Consultas { get; private set; }
        public string? UltimoCentro { get; private set; }
        public string? UltimoProduto { get; private set; }

        public override Task<VinculoProdutoEtiqueta?> ObterPorProdutoAsync(
            string centro, string produto, CancellationToken cancellationToken = default)
        {
            Consultas++;
            UltimoCentro = centro;
            UltimoProduto = produto;

            return Task.FromResult<VinculoProdutoEtiqueta?>(_processo is ProcessoPesagemProduto processo
                ? new VinculoProdutoEtiqueta { Centro = centro, Produto = produto, ProcessoPesagem = processo }
                : null);
        }
    }
}
