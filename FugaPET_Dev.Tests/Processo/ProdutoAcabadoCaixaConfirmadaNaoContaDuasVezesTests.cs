using FugaPET_Dev.Modelo.Processo;
using FugaPET_Dev.Tela.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// CAIXA JA CONTADA PELO SAP NAO ABATE DE NOVO — nem no saldo exibido, nem na guarda.
/// <para>
/// Caso REAL do QAS110 em 27/11/2026, OP 1000452. Conferido no SAP e no banco:
/// </para>
/// <list type="bullet">
/// <item>a OP tem 56 planejadas e 40 recebidas (saldo 16), item 1, material 4000174;</item>
/// <item>as duas caixas da tela (CX-1000452-0005 e 0006, 8 UN cada) estao CONFIRMADA_SAP e
/// postaram ENTRADA DE MERCADORIA de verdade: documentos 5000001277 e 5000001278, movimento 101,
/// 8 UN cada, na OP 1000452;</item>
/// <item>portanto as 40 recebidas JA INCLUEM essas duas caixas.</item>
/// </list>
/// <para>
/// A guarda somava TODAS as caixas da tela e comparava com o saldo: 16 + 8 = 24 &gt; 16, e barrava a
/// terceira caixa — contando a mesma producao duas vezes. Quem estava certo era o saldo EXIBIDO,
/// que de proposito so abate caixa que o SAP ainda nao contou.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoCaixaConfirmadaNaoContaDuasVezesTests
{
    /// <summary>O caso da foto: duas caixas CONFIRMADA_SAP, saldo 16, proxima caixa de 8 — tem de passar.</summary>
    [Fact]
    public void DuasCaixasConfirmadas_NaoBarramAProxima()
    {
        ProdutoAcabadoCaixa[] caixas =
        [
            Caixa(8, StatusIntegracaoCaixa.ConfirmadaSap),
            Caixa(8, StatusIntegracaoCaixa.ConfirmadaSap)
        ];

        Assert.False(Barra(caixas, saldoPendente: 16m, produtosDaCaixa: 8));
    }

    /// <summary>
    /// Caixa que o SAP AINDA NAO contou abate sim: com 16 pendentes e duas caixas locais de 8, a
    /// terceira estoura. E a guarda continua existindo.
    /// </summary>
    [Theory]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal)]
    [InlineData(StatusIntegracaoCaixa.PreviewHuGerado)]
    [InlineData(StatusIntegracaoCaixa.AguardandoAutorizacaoSap)]
    [InlineData(StatusIntegracaoCaixa.ProntaParaEnvio)]
    public void CaixaLocalAindaNaoEnviada_ContaNaGuarda(StatusIntegracaoCaixa status)
    {
        ProdutoAcabadoCaixa[] caixas = [Caixa(8, status), Caixa(8, status)];

        Assert.True(Barra(caixas, saldoPendente: 16m, produtosDaCaixa: 8));
    }

    /// <summary>Caixa CANCELADA nunca conta — nem no SAP, nem localmente.</summary>
    [Fact]
    public void CaixaCancelada_NaoConta()
    {
        ProdutoAcabadoCaixa[] caixas = [Caixa(8, StatusIntegracaoCaixa.Cancelada), Caixa(8, StatusIntegracaoCaixa.Cancelada)];

        Assert.False(Barra(caixas, saldoPendente: 16m, produtosDaCaixa: 8));
    }

    /// <summary>
    /// A GUARDA E O SALDO EXIBIDO CONTAM A MESMA COISA. Enquanto divergirem, a tela diz um numero e
    /// a mensagem de bloqueio diz outro — foi exatamente o que o usuario viu: "16 pendentes" ao lado
    /// de "ultrapassa o saldo pendente".
    /// </summary>
    [Theory]
    [InlineData(StatusIntegracaoCaixa.ConfirmadaSap, 16)]
    [InlineData(StatusIntegracaoCaixa.FinalizadaLocal, 0)]
    public void SaldoExibido_ESperaOMesmoQueAGuarda(StatusIntegracaoCaixa status, int saldoExibidoEsperado)
    {
        ProdutoAcabadoCaixa[] caixas = [Caixa(8, status), Caixa(8, status)];

        decimal exibido = CalculoSaldoProdutoAcabado.SaldoPendenteExibido(16m, caixas, "UN");
        decimal naoContados = CalculoSaldoProdutoAcabado.ProdutosAindaNaoContadosNoSap(caixas, "UN");

        Assert.Equal(saldoExibidoEsperado, exibido);
        Assert.Equal(16m - exibido, naoContados);
    }

    private static bool Barra(ProdutoAcabadoCaixa[] caixas, decimal saldoPendente, int produtosDaCaixa)
        => ProcessoProdutoAcabadoForm.CaixaUltrapassaSaldoPendente(
            "UN",
            saldoPendente,
            CalculoSaldoProdutoAcabado.PesoLiquidoAindaNaoContadoNoSap(caixas),
            14.400m,
            CalculoSaldoProdutoAcabado.ProdutosAindaNaoContadosNoSap(caixas, "UN"),
            produtosDaCaixa);

    private static ProdutoAcabadoCaixa Caixa(int produtos, StatusIntegracaoCaixa status) => new()
    {
        QuantidadeProdutos = produtos,
        UnidadeQuantidade = "UN",
        PesoLiquidoKg = 14.400m,
        StatusIntegracao = status
    };
}
