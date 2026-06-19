using FugaPET_Dev.Modelo.IntegracaoSap;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

#pragma warning disable CS0618

/// <summary>
/// Trava o comportamento do contrato interno de integracao SAP (mock). Quando a integracao
/// real chegar, estes testes documentam o contrato esperado pelas telas/servicos.
/// </summary>
public sealed class IntegracaoSapMockServicoTests
{
    private static IIntegracaoSapServico CriarServico() => new IntegracaoSapMockServico();

    [Fact]
    public void FabricaLegada_EmDemonstracao_DeveRetornarImplementacaoSimulada()
    {
        IIntegracaoSapServico servico = FabricaIntegracaoSap.Criar(modoDemonstracao: true);
        Assert.True(servico.EhSimulado);
    }

    [Fact]
    public void FabricaLegada_ForaDaDemonstracao_DeveFalharFechado()
    {
        Assert.Throws<InvalidOperationException>(
            () => FabricaIntegracaoSap.Criar(modoDemonstracao: false));
    }

    [Fact]
    public async Task ConsultarHistorico_SemFiltro_DeveRetornarRegistrosOrdenadosPorDataDesc()
    {
        IIntegracaoSapServico servico = CriarServico();

        IReadOnlyList<RegistroIntegracaoSap> registros =
            await servico.ConsultarHistoricoAsync(new FiltroConsultaIntegracaoSap());

        Assert.NotEmpty(registros);
        // Ordenacao decrescente por data/hora.
        for (int i = 1; i < registros.Count; i++)
        {
            Assert.True(registros[i - 1].DataHora >= registros[i].DataHora);
        }
    }

    [Fact]
    public async Task ConsultarHistorico_ComTermo_DeveFiltrarPorOpProdutoOuLote()
    {
        IIntegracaoSapServico servico = CriarServico();

        IReadOnlyList<RegistroIntegracaoSap> registros =
            await servico.ConsultarHistoricoAsync(new FiltroConsultaIntegracaoSap { Termo = "58895" });

        Assert.NotEmpty(registros);
        Assert.All(registros, r => Assert.Contains("58895", r.OrdemProducao));
    }

    [Fact]
    public async Task ConsultarHistorico_DeveRespeitarLimite()
    {
        IIntegracaoSapServico servico = CriarServico();

        IReadOnlyList<RegistroIntegracaoSap> registros =
            await servico.ConsultarHistoricoAsync(new FiltroConsultaIntegracaoSap { Limite = 3 });

        Assert.True(registros.Count <= 3);
    }

    [Fact]
    public async Task ConsultarProdutoPorOrdem_Existente_DeveRetornarProduto()
    {
        IIntegracaoSapServico servico = CriarServico();

        ProdutoSap? produto = await servico.ConsultarProdutoPorOrdemAsync("58895");

        Assert.NotNull(produto);
        Assert.Equal("58895", produto!.OrdemProducao);
        Assert.False(string.IsNullOrWhiteSpace(produto.DescricaoProduto));
    }

    [Fact]
    public async Task ConsultarProdutoPorOrdem_Inexistente_DeveRetornarNull()
    {
        IIntegracaoSapServico servico = CriarServico();

        ProdutoSap? produto = await servico.ConsultarProdutoPorOrdemAsync("000000");

        Assert.Null(produto);
    }

    [Fact]
    public async Task EnviarApontamento_Valido_DeveRetornarSucessoComProtocolo()
    {
        IIntegracaoSapServico servico = CriarServico();

        ResultadoEnvioSap resultado = await servico.EnviarApontamentoAsync(new ApontamentoSap
        {
            OrdemProducao = "58895",
            CodigoProduto = "SKU-58895",
            Quantidade = 16.0m,
            Origem = "Balanca F12",
            Usuario = "OPERADOR01"
        });

        Assert.True(resultado.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Protocolo));
        Assert.Equal(SituacaoIntegracaoSap.Enviado, resultado.Situacao);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("58895", 0)]
    public async Task EnviarApontamento_Invalido_DeveRetornarFalha(string ordemProducao, double quantidade)
    {
        IIntegracaoSapServico servico = CriarServico();

        ResultadoEnvioSap resultado = await servico.EnviarApontamentoAsync(new ApontamentoSap
        {
            OrdemProducao = ordemProducao,
            Quantidade = (decimal)quantidade,
            Origem = "Manual",
            Usuario = "OPERADOR01"
        });

        Assert.False(resultado.Sucesso);
        Assert.Equal(SituacaoIntegracaoSap.Erro, resultado.Situacao);
    }
}

#pragma warning restore CS0618
