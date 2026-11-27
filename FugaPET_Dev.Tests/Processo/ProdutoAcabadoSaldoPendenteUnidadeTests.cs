using FugaPET_Dev.Tela.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// A GUARDA DE SALDO PENDENTE COMPARA NA UNIDADE DA ORDEM.
/// <para>
/// Defeito real no QAS130, 27/11/2026: a OP 1000452 esta em UN (56 planejadas, 40 entregues, 16
/// pendentes) e a norma da 8 produtos por caixa. Faltavam as duas ultimas caixas e a tela barrava
/// com "Peso total das caixas ultrapassa o saldo pendente da OP" — ela somava QUILOS e comparava
/// com um saldo em UNIDADES: 2 x 14,400 kg = 28,8 "maior" que 16. Em produto eram 16 de 16,
/// exatamente o saldo.
/// </para>
/// <para>
/// O resumo da propria tela ja distinguia peso x produto (GATE 121A) e mostrava "PRODUTOS
/// REGISTRADOS 016 de 16"; so a guarda ficara de fora. Agora a regra sai de um lugar so.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoSaldoPendenteUnidadeTests
{
    /// <summary>O caso REAL da foto: ordem em UN, 16 pendentes, 8 por caixa.</summary>
    [Theory]
    [InlineData(0, 16, false)]   // 1a caixa: 8 de 16 — passa
    [InlineData(8, 16, false)]   // 2a caixa: 16 de 16 — FECHA o saldo, tem de passar
    [InlineData(16, 16, true)]   // 3a caixa: 24 de 16 — ai sim estoura
    public void OrdemEmUnidade_ComparaProduto_NaoPeso(int produtosJaRegistrados, decimal saldo, bool esperaBloqueio)
    {
        // O peso vai bem acima do saldo de proposito: se a guarda olhar quilo, ela bloqueia tudo.
        decimal pesoJa = produtosJaRegistrados / 8m * 14.400m;

        bool bloqueou = ProcessoProdutoAcabadoForm.CaixaUltrapassaSaldoPendente(
            "UN", saldo, pesoJa, 14.400m, produtosJaRegistrados, 8);

        Assert.Equal(esperaBloqueio, bloqueou);
    }

    /// <summary>Ordem medida em PESO continua comparando peso — o comportamento antigo, onde ele valia.</summary>
    [Theory]
    [InlineData("KG", 20.0, 14.4, 14.4, true)]
    [InlineData("KG", 40.0, 14.4, 14.4, false)]
    [InlineData("KGM", 20.0, 14.4, 14.4, true)]
    [InlineData("G", 20.0, 14.4, 14.4, true)]
    [InlineData("TO", 20.0, 14.4, 14.4, true)]
    [InlineData("", 20.0, 14.4, 14.4, true)]
    public void OrdemEmPeso_ComparaPeso(string unidade, double saldo, double pesoJa, double pesoDaCaixa, bool esperaBloqueio)
        => Assert.Equal(
            esperaBloqueio,
            ProcessoProdutoAcabadoForm.CaixaUltrapassaSaldoPendente(
                unidade, (decimal)saldo, (decimal)pesoJa, (decimal)pesoDaCaixa, produtosAindaNaoContados: 999m, produtosDaCaixa: 999));

    /// <summary>
    /// SALDO INDETERMINADO NAO BLOQUEIA. O pendente e tri-state (GATE 118B): quando o SAP nao
    /// informa o recebido, nao ha limite conhecido — e inventar um pararia a producao.
    /// </summary>
    [Fact]
    public void SaldoIndeterminado_NaoBloqueia()
        => Assert.False(ProcessoProdutoAcabadoForm.CaixaUltrapassaSaldoPendente(
            "UN", null, 1000m, 1000m, 1000, 1000));

    /// <summary>A unidade ausente cai em PESO, como antes — a correcao nao mudou esse lado.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("kg")]
    public void UnidadeAusenteOuPeso_EhPeso(string? unidade)
        => Assert.True(ProcessoProdutoAcabadoForm.OrdemMedidaPorPeso(unidade));

    [Theory]
    [InlineData("UN")]
    [InlineData("un")]
    [InlineData("PC")]
    [InlineData("ST")]
    public void UnidadeDeContagem_NaoEhPeso(string unidade)
        => Assert.False(ProcessoProdutoAcabadoForm.OrdemMedidaPorPeso(unidade));
}
