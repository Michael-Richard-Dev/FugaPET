using FugaPET_Dev.Servicos.Cadastro;
using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// C13 - configuracao SAP malformada e tratada como ERRO DE IMPLANTACAO: nao cai silenciosamente
/// em "nao configurado" e nao aciona o mock.
/// </summary>
public sealed class ConfiguracaoSapInvalidaC13Tests
{
    [Fact]
    public async Task ConfiguracaoMalformada_NaoDeveAcionarMock_DeveBloquearComMensagemSegura()
    {
        IPedidoCompraSapServico servico = FabricaPedidoCompraSapServico.Criar(
            bancoHabilitado: true,
            modoDemonstracao: false,
            ambienteDemonstrativo: false,
            carregarConfiguracaoSap: () => throw new ConfiguracaoSapInvalidaException());

        Assert.IsNotType<PedidoCompraSapMockServico>(servico);
        Assert.False(servico.EhSimulado);
        Assert.False(servico.SapConfigurado);

        ResultadoOperacao resultado = await servico.SincronizarPedidoAsync("4500000010");

        Assert.False(resultado.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemConfiguracaoInvalida, resultado.Mensagem);
    }

    [Fact]
    public void ConfiguracaoMalformada_EmModoDemonstracao_NaoLeConfiguracao()
    {
        // Em demonstracao o mock e escolhido ANTES de ler a config; uma config malformada nao
        // afeta o modo demo (o mock nao e acionado "por falha de configuracao").
        bool tentouLerConfiguracao = false;

        IPedidoCompraSapServico servico = FabricaPedidoCompraSapServico.Criar(
            bancoHabilitado: false,
            modoDemonstracao: true,
            ambienteDemonstrativo: true,
            carregarConfiguracaoSap: () =>
            {
                tentouLerConfiguracao = true;
                throw new ConfiguracaoSapInvalidaException();
            });

        Assert.IsType<PedidoCompraSapMockServico>(servico);
        Assert.False(tentouLerConfiguracao);
    }

    [Fact]
    public async Task ServicoConfiguracaoInvalida_NaoEscreveNemConsultaSap()
    {
        PedidoCompraSapConfiguracaoInvalidaServico servico = new();

        Assert.False(servico.EhSimulado);
        Assert.False(servico.SapConfigurado);
        Assert.False(servico.EscritaSapHabilitada);

        ResultadoOperacao patch = await servico.AtualizarPesoItemSapAsync("4500000010", "10", 8m, 10m);
        Assert.False(patch.Sucesso);
        Assert.Equal(ConfiguracaoSap.MensagemConfiguracaoInvalida, patch.Mensagem);

        await Assert.ThrowsAsync<IntegracaoSapBloqueadaException>(
            () => servico.ObterPedidoAgregadoAsync("4500000010"));
    }
}
