using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Servicos.Processo;
using FugaPET_Dev.Tela.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Envio em lote do processo ENVIO_UNICO (19/11/2026).
/// <para>
/// O que estes testes travam e a REGRA DE PARADA. O resto do caminho (POST, claim, idempotencia) ja e
/// coberto pelo envio individual; o perigo novo do lote e seguir postando depois de algo dar errado.
/// </para>
/// </summary>
public sealed class EnvioLoteCaixas067Tests
{
    [Fact]
    public async Task TodasElegiveis_EnviaTodasNaOrdem()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(2, StatusIntegracaoCaixa.AguardandoAutorizacaoSap),
             Caixa(3, StatusIntegracaoCaixa.ProntaParaEnvio)];

        List<string> tentadas = [];
        ResultadoEnvioLoteCaixas resultado = await new ExecutorEnvioLoteCaixas().ExecutarAsync(
            caixas, (caixa, _) =>
            {
                tentadas.Add(caixa.CodigoCaixaLocal);
                return Task.FromResult<StatusIntegracaoCaixa?>(StatusIntegracaoCaixa.ConfirmadaSap);
            });

        Assert.False(resultado.Interrompeu);
        Assert.Equal(3, resultado.Enviadas.Count);
        Assert.Equal(["CX-0001", "CX-0002", "CX-0003"], tentadas);
    }

    /// <summary>O coracao da regra: a falha de uma caixa impede o POST das seguintes.</summary>
    [Fact]
    public async Task FalhaNoMeio_NaoTentaAsSeguintes()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(2, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(3, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(4, StatusIntegracaoCaixa.ProntaParaEnvio)];

        List<string> tentadas = [];
        ResultadoEnvioLoteCaixas resultado = await new ExecutorEnvioLoteCaixas().ExecutarAsync(
            caixas, (caixa, _) =>
            {
                tentadas.Add(caixa.CodigoCaixaLocal);
                return Task.FromResult<StatusIntegracaoCaixa?>(
                    caixa.CodigoCaixaLocal == "CX-0002"
                        ? StatusIntegracaoCaixa.ErroSap
                        : StatusIntegracaoCaixa.ConfirmadaSap);
            });

        Assert.True(resultado.Interrompeu);
        Assert.Single(resultado.Enviadas);
        Assert.Equal("CX-0002", resultado.ParouEm!.CodigoCaixaLocal);
        Assert.Equal(StatusIntegracaoCaixa.ErroSap, resultado.StatusDoParado);
        Assert.Equal(2, resultado.NaoTentadas.Count);
        Assert.Equal(["CX-0001", "CX-0002"], tentadas); // 3 e 4 nunca foram ao SAP
    }

    /// <summary>
    /// Estado final desconhecido e o pior caso: para, e NAO afirma status nenhum.
    /// </summary>
    [Fact]
    public async Task EstadoIndeterminado_ParaEModoNaoAfirmaStatus()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(2, StatusIntegracaoCaixa.ProntaParaEnvio)];

        ResultadoEnvioLoteCaixas resultado = await new ExecutorEnvioLoteCaixas().ExecutarAsync(
            caixas, (_, _) => Task.FromResult<StatusIntegracaoCaixa?>(null));

        Assert.True(resultado.Interrompeu);
        Assert.Empty(resultado.Enviadas);
        Assert.Null(resultado.StatusDoParado);
        Assert.Single(resultado.NaoTentadas);
    }

    /// <summary>Retomada: confirmada e pulada em silencio, sem POST.</summary>
    [Fact]
    public async Task JaConfirmada_EPuladaSemEnviar()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ConfirmadaSap),
             Caixa(2, StatusIntegracaoCaixa.ProntaParaEnvio)];

        List<string> tentadas = [];
        ResultadoEnvioLoteCaixas resultado = await new ExecutorEnvioLoteCaixas().ExecutarAsync(
            caixas, (caixa, _) =>
            {
                tentadas.Add(caixa.CodigoCaixaLocal);
                return Task.FromResult<StatusIntegracaoCaixa?>(StatusIntegracaoCaixa.ConfirmadaSap);
            });

        Assert.False(resultado.Interrompeu);
        Assert.Single(resultado.Puladas);
        Assert.Equal(["CX-0002"], tentadas);
    }

    /// <summary>
    /// Caixa inelegivel nao interrompe o lote: sai do conjunto e as outras seguem. Travar tudo por
    /// causa de uma bloqueada deixaria caixas prontas sem enviar sem motivo.
    /// </summary>
    [Fact]
    public async Task Inelegivel_FicaForaDoLoteSemInterromper()
    {
        List<ProdutoAcabadoCaixa> caixas =
            [Caixa(1, StatusIntegracaoCaixa.ProntaParaEnvio),
             Caixa(2, StatusIntegracaoCaixa.Bloqueada),
             Caixa(3, StatusIntegracaoCaixa.IndeterminadoTimeout),
             Caixa(4, StatusIntegracaoCaixa.ProntaParaEnvio)];

        List<string> tentadas = [];
        ResultadoEnvioLoteCaixas resultado = await new ExecutorEnvioLoteCaixas().ExecutarAsync(
            caixas, (caixa, _) =>
            {
                tentadas.Add(caixa.CodigoCaixaLocal);
                return Task.FromResult<StatusIntegracaoCaixa?>(StatusIntegracaoCaixa.ConfirmadaSap);
            });

        Assert.False(resultado.Interrompeu);
        Assert.Equal(2, resultado.Enviadas.Count);
        Assert.Equal(2, resultado.ForaDoLote.Count);
        Assert.Equal(["CX-0001", "CX-0004"], tentadas);
    }

    [Fact]
    public async Task ListaVazia_NaoEnviaNada()
    {
        ResultadoEnvioLoteCaixas resultado = await new ExecutorEnvioLoteCaixas().ExecutarAsync(
            [], (_, _) => throw new InvalidOperationException("nao deveria enviar"));

        Assert.True(resultado.NadaTentado);
        Assert.False(resultado.Interrompeu);
    }

    /// <summary>A elegibilidade do lote e EXATAMENTE a do envio individual, nunca mais permissiva.</summary>
    [Theory]
    [InlineData(StatusIntegracaoCaixa.AguardandoAutorizacaoSap, true)]
    [InlineData(StatusIntegracaoCaixa.ProntaParaEnvio, true)]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap, false)]
    [InlineData(StatusIntegracaoCaixa.ErroSap, false)]
    [InlineData(StatusIntegracaoCaixa.EnviandoSap, false)]
    [InlineData(StatusIntegracaoCaixa.IndeterminadoTimeout, false)]
    [InlineData(StatusIntegracaoCaixa.Bloqueada, false)]
    [InlineData(StatusIntegracaoCaixa.Cancelada, false)]
    [InlineData(StatusIntegracaoCaixa.EmPesagem, false)]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal, false)]
    public void Elegibilidade_EAMesmaDoEnvioIndividual(StatusIntegracaoCaixa status, bool esperado)
    {
        ProdutoAcabadoCaixa caixa = Caixa(1, status);

        Assert.Equal(esperado, ExecutorEnvioLoteCaixas.Elegivel(caixa));
        Assert.Equal(esperado, ProcessoProdutoAcabadoForm.CaixaElegivelParaPipeline(caixa));
    }

    [Fact]
    public void Relato_NomeiaACaixaQueParouEOQueNaoFoiTentado()
    {
        ResultadoEnvioLoteCaixas resultado = ResultadoEnvioLoteCaixas.Interrompido(
            [Caixa(1, StatusIntegracaoCaixa.ConfirmadaSap)],
            [],
            [],
            Caixa(2, StatusIntegracaoCaixa.ErroSap),
            StatusIntegracaoCaixa.ErroSap,
            [Caixa(3, StatusIntegracaoCaixa.ProntaParaEnvio)]);

        string relato = ProcessoProdutoAcabadoForm.MontarRelatoDoLote(resultado);

        Assert.Contains("1 caixa(s) enviada(s)", relato, StringComparison.Ordinal);
        Assert.Contains("CX-0002", relato, StringComparison.Ordinal);
        Assert.Contains("1 caixa(s) não foram tentadas", relato, StringComparison.Ordinal);
    }

    [Fact]
    public void Relato_SemInterrupcao_NaoInventaParada()
    {
        ResultadoEnvioLoteCaixas resultado = ResultadoEnvioLoteCaixas.Completo(
            [Caixa(1, StatusIntegracaoCaixa.ConfirmadaSap), Caixa(2, StatusIntegracaoCaixa.ConfirmadaSap)],
            [],
            []);

        string relato = ProcessoProdutoAcabadoForm.MontarRelatoDoLote(resultado);

        Assert.Contains("2 caixa(s) enviada(s)", relato, StringComparison.Ordinal);
        Assert.DoesNotContain("Parou", relato, StringComparison.Ordinal);
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
