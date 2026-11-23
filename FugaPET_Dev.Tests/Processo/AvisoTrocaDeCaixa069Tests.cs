using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Aviso de "troque a caixa" a cada 24 pesagens no ENVIO_UNICO (pedido do usuario em 23/11/2026).
/// <para>
/// O aviso e deliberadamente burro — uma caixa de dialogo com OK, sem travar nada. O que estes testes
/// protegem nao e o aviso aparecer, e ele NAO aparecer onde nao deve: no caixa a caixa, em produto sem
/// vinculo, e nas 23 pesagens entre um aviso e o seguinte.
/// </para>
/// </summary>
public sealed class AvisoTrocaDeCaixa069Tests
{
    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(72)]
    [InlineData(240)]
    public void EnvioUnico_AvisaExatamenteNosMultiplosDe24(int pesadas)
        => Assert.True(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(
            ProcessoPesagemProduto.EnvioUnico, Caixas(pesadas)));

    /// <summary>Entre um aviso e o proximo, silencio — inclusive na vespera e no dia seguinte.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(23)]
    [InlineData(25)]
    [InlineData(47)]
    [InlineData(49)]
    public void EnvioUnico_NaoAvisaForaDosMultiplos(int pesadas)
        => Assert.False(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(
            ProcessoPesagemProduto.EnvioUnico, Caixas(pesadas)));

    /// <summary>Sem caixa nenhuma nao ha o que avisar: zero e multiplo de 24, e nao pode disparar.</summary>
    [Fact]
    public void SemCaixas_NaoAvisa()
        => Assert.False(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(
            ProcessoPesagemProduto.EnvioUnico, []));

    /// <summary>O usuario pediu o aviso SO no envio unico; no caixa a caixa a tela nao muda.</summary>
    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    public void CaixaACaixa_NuncaAvisa(int pesadas)
        => Assert.False(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(
            ProcessoPesagemProduto.CaixaACaixa, Caixas(pesadas)));

    /// <summary>Produto sem vinculo segue o comportamento historico: nada de aviso.</summary>
    [Fact]
    public void SemVinculo_NuncaAvisa()
        => Assert.False(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(processo: null, Caixas(24)));

    /// <summary>
    /// CANCELADA NAO CONTA. O aviso existe porque o recipiente encheu, e caixa cancelada nao foi para
    /// dentro dele — conta-la adiantaria o aviso para antes da hora.
    /// </summary>
    [Fact]
    public void Canceladas_NaoEntramNaContagem()
    {
        List<ProdutoAcabadoCaixa> caixas = [.. Caixas(24)];
        caixas[0].StatusIntegracao = StatusIntegracaoCaixa.Cancelada;

        // 23 validas: ainda nao e hora.
        Assert.False(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(ProcessoPesagemProduto.EnvioUnico, caixas));

        // Pesando mais uma, fecha as 24 validas.
        caixas.Add(Caixa(25, StatusIntegracaoCaixa.AguardandoAutorizacaoSap));
        Assert.True(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(ProcessoPesagemProduto.EnvioUnico, caixas));
    }

    /// <summary>
    /// Caixa ja CONFIRMADA conta igual: ela existe fisicamente. A contagem e do que foi pesado, nao do
    /// que foi enviado — se dependesse do envio, o aviso mudaria de hora conforme o SAP respondesse.
    /// </summary>
    [Fact]
    public void ConfirmadaNoSap_ContaComoQualquerOutra()
    {
        List<ProdutoAcabadoCaixa> caixas = [.. Caixas(24)];
        for (int i = 0; i < 10; i++)
        {
            caixas[i].StatusIntegracao = StatusIntegracaoCaixa.ConfirmadaSap;
        }

        Assert.True(AvisoTrocaDeCaixaEnvioUnico.DeveAvisar(ProcessoPesagemProduto.EnvioUnico, caixas));
    }

    /// <summary>A mensagem diz quantas foram, para o operador conferir com o que tem na mao.</summary>
    [Fact]
    public void Mensagem_DizQuantasCaixasForam()
    {
        string texto = AvisoTrocaDeCaixaEnvioUnico.Mensagem(48);

        Assert.Contains("48", texto, StringComparison.Ordinal);
        Assert.Contains("Troque a caixa", texto, StringComparison.Ordinal);
    }

    /// <summary>O numero veio do usuario e nao do sistema; se mudar, muda aqui e em um lugar so.</summary>
    [Fact]
    public void CaixasPorTroca_SaoVinteEQuatro()
        => Assert.Equal(24, AvisoTrocaDeCaixaEnvioUnico.CaixasPorTroca);

    // ------------------------------------------------------------------
    private static List<ProdutoAcabadoCaixa> Caixas(int quantas)
        => [.. Enumerable.Range(1, quantas).Select(n => Caixa(n, StatusIntegracaoCaixa.AguardandoAutorizacaoSap))];

    private static ProdutoAcabadoCaixa Caixa(int numero, StatusIntegracaoCaixa status) => new()
    {
        CodigoProdutoAcabadoCaixa = numero,
        CodigoCaixaLocal = $"CX-{numero:0000}",
        NumeroCaixa = numero,
        StatusIntegracao = status,
    };
}
