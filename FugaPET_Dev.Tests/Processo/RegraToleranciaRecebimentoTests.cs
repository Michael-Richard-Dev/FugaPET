using FugaPET_Dev.Modelo.Cadastro;
using FugaPET_Dev.Servicos.Processo;
using Xunit;

namespace FugaPET_Dev.Tests.Processo;

/// <summary>
/// Tolerancia de pesagem no Recebimento (pedido do usuario em 23/11/2026).
/// <para>
/// O exemplo que originou a tarefa esta aqui como teste: pedido 100, tolerancia 10%, balanca 110 passa
/// e 112 bloqueia. Os demais testes guardam as decisoes que ele tomou e as que a estrutura da tela
/// impoe — sobretudo a assimetria de MOMENTO entre teto e piso, que parece descuido e nao e.
/// </para>
/// </summary>
public sealed class RegraToleranciaRecebimentoTests
{
    private static ToleranciaRecebimentoMaterial Tol(decimal percentual, bool ativa = true) => new()
    {
        Centro = "3007",
        Material = "X",
        PercentualTolerancia = percentual,
        Situacao = ativa
    };

    // ---------------------------------------------------------------- o exemplo do usuario

    [Theory]
    [InlineData(90)]    // exatamente no piso
    [InlineData(100)]   // exato
    [InlineData(110)]   // exatamente no teto: PASSA, o limite e inclusivo
    public void PedidoCem_ComDezPorCento_AceitaDeNoventaACentoEDez(decimal recebido)
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, recebido, Tol(10m));

        Assert.Equal(ResultadoTolerancia.Dentro, v.Resultado);
        Assert.False(v.Bloqueia);
    }

    [Fact]
    public void PedidoCem_ComDezPorCento_BloqueiaCentoEDoze()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 112m, Tol(10m));

        Assert.Equal(ResultadoTolerancia.AcimaDoTeto, v.Resultado);
        Assert.Equal(90m, v.LimiteInferior);
        Assert.Equal(110m, v.LimiteSuperior);
    }

    [Fact]
    public void AMensagemDeBloqueioTrazOsNumeros()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 112m, Tol(10m));
        string msg = RegraToleranciaRecebimento.Mensagem("4000548", v);

        // Sem os numeros, quem esta na balanca tem de adivinhar de quanto passou.
        Assert.Contains("112", msg, StringComparison.Ordinal);
        Assert.Contains("110", msg, StringComparison.Ordinal);
        Assert.Contains("100", msg, StringComparison.Ordinal);
        Assert.Contains("4000548", msg, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- a assimetria de momento

    /// <summary>
    /// A razao de existir de <c>AvaliarDurantePesagem</c>. Todo item comeca em zero; cobrar o piso
    /// durante a pesagem barraria a PRIMEIRA pesagem de todo recebimento.
    /// </summary>
    [Fact]
    public void DuranteAPesagem_OPisoNaoEhCobrado()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 0m, Tol(10m));

        Assert.Equal(ResultadoTolerancia.Dentro, v.Resultado);
    }

    [Fact]
    public void NaFinalizacao_OPisoEhCobrado()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 89.9m, Tol(10m));

        Assert.Equal(ResultadoTolerancia.AbaixoDoPiso, v.Resultado);
    }

    /// <summary>O teto vale nos DOIS momentos: passar dele e definitivo, peso registrado nao volta.</summary>
    [Fact]
    public void OTetoValeNosDoisMomentos()
    {
        Assert.Equal(ResultadoTolerancia.AcimaDoTeto,
            RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 111m, Tol(10m)).Resultado);
        Assert.Equal(ResultadoTolerancia.AcimaDoTeto,
            RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 111m, Tol(10m)).Resultado);
    }

    // ---------------------------------------------------------------- sem cadastro

    [Fact]
    public void SemCadastro_Bloqueia()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 10m, tolerancia: null);

        Assert.Equal(ResultadoTolerancia.SemCadastro, v.Resultado);
        Assert.True(v.Bloqueia);
    }

    /// <summary>
    /// Desativar a linha NAO pode virar um jeito silencioso de liberar o material: inativa conta como
    /// nao cadastrada, que bloqueia, e nao como "sem limite", que liberaria.
    /// </summary>
    [Fact]
    public void ToleranciaInativa_ContaComoNaoCadastrada()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 10m, Tol(10m, ativa: false));

        Assert.Equal(ResultadoTolerancia.SemCadastro, v.Resultado);
    }

    [Fact]
    public void SemCadastro_AMensagemDizQualMaterialEOndeCadastrar()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 10m, tolerancia: null);
        string msg = RegraToleranciaRecebimento.Mensagem("4000548", v);

        // Bloqueio sem caminho de saida ja prendeu a operacao antes (incidente 063).
        Assert.Contains("4000548", msg, StringComparison.Ordinal);
        Assert.Contains("Tolerância de Recebimento", msg, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- bordas

    /// <summary>
    /// Sem quantidade no pedido nao existe o 100 do exemplo, logo nao existe 90 nem 110. Barrar aqui
    /// seria barrar por um numero que ninguem informou.
    /// </summary>
    // 0d, nao 0: InlineData(0) manda um int, que nao converte para double? e o caso nem roda.
    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    public void PedidoSemQuantidade_NaoBloqueia(double? pedida)
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(
            pedida is null ? null : (decimal)pedida.Value, 999m, Tol(10m));

        Assert.Equal(ResultadoTolerancia.Dentro, v.Resultado);
    }

    /// <summary>Zero por cento e cadastro valido e significa exigencia exata — nao "sem tolerancia".</summary>
    [Fact]
    public void ZeroPorCento_ExigeValorExato()
    {
        Assert.Equal(ResultadoTolerancia.Dentro,
            RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 100m, Tol(0m)).Resultado);
        Assert.Equal(ResultadoTolerancia.AcimaDoTeto,
            RegraToleranciaRecebimento.AvaliarDurantePesagem(100m, 100.001m, Tol(0m)).Resultado);
        Assert.Equal(ResultadoTolerancia.AbaixoDoPiso,
            RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 99.999m, Tol(0m)).Resultado);
    }

    /// <summary>100% leva o piso a zero — ainda um numero. Acima disso o piso ficaria negativo, e o
    /// banco recusa (CHECK do incremento 069).</summary>
    [Fact]
    public void CemPorCento_LevaOPisoAZero()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 0m, Tol(100m));

        Assert.Equal(0m, v.LimiteInferior);
        Assert.Equal(200m, v.LimiteSuperior);
        Assert.Equal(ResultadoTolerancia.Dentro, v.Resultado);
    }

    [Fact]
    public void PercentualFracionado_CalculaSemArredondarCedo()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 102.5m, Tol(2.5m));

        Assert.Equal(97.5m, v.LimiteInferior);
        Assert.Equal(102.5m, v.LimiteSuperior);
        Assert.Equal(ResultadoTolerancia.Dentro, v.Resultado);
    }

    /// <summary>Quantidade quebrada de pedido tambem precisa fechar: 37,5 kg com 8% vai de 34,5 a 40,5.</summary>
    [Fact]
    public void QuantidadeQuebradaDePedido()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(37.5m, 40.5m, Tol(8m));

        Assert.Equal(34.5m, v.LimiteInferior);
        Assert.Equal(40.5m, v.LimiteSuperior);
        Assert.Equal(ResultadoTolerancia.Dentro, v.Resultado);

        Assert.Equal(ResultadoTolerancia.AcimaDoTeto,
            RegraToleranciaRecebimento.AvaliarDurantePesagem(37.5m, 40.6m, Tol(8m)).Resultado);
    }

    [Fact]
    public void DentroDosLimites_NaoGeraMensagem()
    {
        VereditoTolerancia v = RegraToleranciaRecebimento.AvaliarNaFinalizacao(100m, 100m, Tol(10m));

        Assert.Equal(string.Empty, RegraToleranciaRecebimento.Mensagem("X", v));
    }
}
