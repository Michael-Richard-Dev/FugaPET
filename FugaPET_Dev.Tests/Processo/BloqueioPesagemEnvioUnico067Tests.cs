using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Quando uma caixa impede seguir, por processo (19/11/2026).
/// <para>
/// DEFEITO QUE ESTES TESTES FECHAM: a regra antiga tratava como impeditiva qualquer caixa que nao
/// estivesse confirmada ou cancelada. No envio unico, onde acumular caixa finalizada E o processo, a
/// SEGUNDA caixa ja travava a pesagem. O usuario encontrou com 25 caixas pesadas e a tela travada.
/// </para>
/// </summary>
public sealed class BloqueioPesagemEnvioUnico067Tests
{
    /// <summary>O caso do defeito: pendentes de envio nao podem travar a pesagem no envio unico.</summary>
    [Fact]
    public void EnvioUnico_CaixasPendentesDeEnvio_NaoImpedemPesarAProxima()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ConfirmadaSap),
             Caixa(2, StatusIntegracaoCaixa.AguardandoAutorizacaoSap),
             Caixa(3, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(4, StatusIntegracaoCaixa.FinalizadaLocal)];

        Assert.False(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(
            ProcessoPesagemProduto.EnvioUnico, caixas));
    }

    /// <summary>E no caixa a caixa a MESMA situacao continua travando, como sempre travou.</summary>
    [Fact]
    public void CaixaACaixa_CaixaPendenteDeEnvio_ContinuaImpedindo()
    {
        List<ProdutoAcabadoCaixa> caixas = [Caixa(1, StatusIntegracaoCaixa.AguardandoAutorizacaoSap)];

        Assert.True(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(
            ProcessoPesagemProduto.CaixaACaixa, caixas));
    }

    /// <summary>Produto sem vinculo segue a regra historica, nao a nova.</summary>
    [Fact]
    public void SemVinculo_SegueARegraHistorica()
    {
        List<ProdutoAcabadoCaixa> caixas = [Caixa(1, StatusIntegracaoCaixa.ProntaParaEnvio)];

        Assert.True(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(processo: null, caixas));
    }

    /// <summary>
    /// Uma caixa por vez NA BALANCA vale nos dois processos: o envio unico afrouxa a integracao, nunca
    /// a pesagem simultanea.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(ProcessoPesagemProduto.CaixaACaixa)]
    [InlineData(ProcessoPesagemProduto.EnvioUnico)]
    public void CaixaAbertaNaBalanca_ImpedeEmQualquerProcesso(ProcessoPesagemProduto? processo)
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ConfirmadaSap), Caixa(2, StatusIntegracaoCaixa.EmPesagem)];

        Assert.True(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(processo, caixas));
    }

    /// <summary>
    /// Estados que exigem decisao humana impedem seguir TAMBEM no envio unico: empilhar caixas sobre um
    /// envio indeterminado esconderia o problema.
    /// </summary>
    [Theory]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada)]
    [InlineData(StatusIntegracaoCaixa.ErroSap)]
    public void EnvioUnico_EstadosQueExigemDecisao_ImpedemSeguir(StatusIntegracaoCaixa status)
    {
        List<ProdutoAcabadoCaixa> caixas = [Caixa(1, status)];

        Assert.True(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(
            ProcessoPesagemProduto.EnvioUnico, caixas));
    }

    [Theory]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap)]
    [InlineData(StatusIntegracaoCaixa.Cancelada)]
    public void CaixaEncerrada_NaoImpedeEmProcessoNenhum(StatusIntegracaoCaixa status)
    {
        List<ProdutoAcabadoCaixa> caixas = [Caixa(1, status)];

        Assert.False(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(null, caixas));
        Assert.False(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(ProcessoPesagemProduto.EnvioUnico, caixas));
        Assert.False(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(ProcessoPesagemProduto.CaixaACaixa, caixas));
    }

    /// <summary>A mensagem precisa NOMEAR a caixa que travou, e nao uma qualquer.</summary>
    [Fact]
    public void PrimeiraQueImpede_NomeiaACaixaCerta()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ConfirmadaSap),
             Caixa(2, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(3, StatusIntegracaoCaixa.ErroSap),
             Caixa(4, StatusIntegracaoCaixa.ProntaParaEnvio)];

        ProdutoAcabadoCaixa? bloqueia = RegraBloqueioPesagemProdutoAcabado.PrimeiraQueImpede(
            ProcessoPesagemProduto.EnvioUnico, caixas);

        Assert.Equal("CX-0003", bloqueia?.CodigoCaixaLocal);
    }

    [Fact]
    public void SemCaixaNenhuma_NadaImpede()
    {
        Assert.False(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(ProcessoPesagemProduto.EnvioUnico, []));
        Assert.False(RegraBloqueioPesagemProdutoAcabado.ImpedeSeguir(null, []));
    }

    // ------------------------------------------------------------------
    private static ProdutoAcabadoCaixa Caixa(int numero, StatusIntegracaoCaixa status) => new()
    {
        CodigoProdutoAcabadoCaixa = numero,
        CodigoCaixaLocal = $"CX-{numero:0000}",
        NumeroCaixa = numero,
        StatusIntegracao = status,
    };
}
