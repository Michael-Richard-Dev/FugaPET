using FugaPET_Dev.Servicos.IntegracaoSap;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Liberação explícita do envio de peso fora de homologação.
/// <para>
/// A regra original era só o nome do banco: "homolog", "_hml" ou final "hml". O DEV110 aponta
/// para um SAP de desenvolvimento próprio (DS), e passou a precisar enviar. Em vez de afrouxar
/// a regra de nome — o que mudaria o comportamento de qualquer ambiente futuro sem ninguém
/// pedir — a abertura é por variável dedicada e declarada.
/// </para>
/// <para>
/// O que estes testes protegem é o <b>fail-closed</b>: só o literal booleano verdadeiro libera.
/// Ausente, vazio, "1", "sim" ou qualquer lixo mantém fechado. É a parte que não pode regredir,
/// porque o custo de errar para o lado permissivo é escrita indevida no SAP.
/// </para>
/// </summary>
public sealed class AmbienteEnvioPesoLiberacaoTests
{
    private static Func<string, string?> Ambiente(string? valor)
        => _ => valor;

    [Fact]
    public void VariavelAusente_NaoLibera()
    {
        Assert.False(AmbienteIntegracaoSap.EnvioPesoLiberadoExplicitamente(Ambiente(null)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("sim")]
    [InlineData("yes")]
    [InlineData("liberado")]
    [InlineData("truthy")]
    public void ValorNaoBooleano_NaoLibera(string valor)
    {
        Assert.False(AmbienteIntegracaoSap.EnvioPesoLiberadoExplicitamente(Ambiente(valor)));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    public void FalseExplicito_NaoLibera(string valor)
    {
        Assert.False(AmbienteIntegracaoSap.EnvioPesoLiberadoExplicitamente(Ambiente(valor)));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("  true  ")]
    public void TrueExplicito_Libera(string valor)
    {
        Assert.True(AmbienteIntegracaoSap.EnvioPesoLiberadoExplicitamente(Ambiente(valor)));
    }

    [Fact]
    public void NomeDaVariavel_EhONamespaceDestaLinha()
    {
        // Cada linha lê o seu próprio namespace. Se este nome vazar entre ambientes, um liga o
        // envio do outro sem ninguém perceber — foi exatamente o que aconteceu com os gates de
        // Produto Acabado quando o DEV passou a ler FUGAPET_SAP_*.
        Assert.Equal("FUGAPET_SAP_ENVIO_PESO_LIBERADO", AmbienteIntegracaoSap.VariavelLiberacaoEnvioPeso);
    }

    [Theory]
    [InlineData("fuga_jales_local_homologacao_q_v1_2", true)]      // QAS110
    [InlineData("fuga_jales_local_homologacao_q130_v1_3", true)]   // QAS130
    [InlineData("fuga_jales_local_homologacao_v1_2", true)]        // HML
    [InlineData("fuga_jales_local_desenvolvimento", false)]        // DEV110
    [InlineData("", false)]
    [InlineData(null, false)]
    public void RegraDeNomeDoBanco_NaoMudou(string? nomeBanco, bool esperado)
    {
        // A liberação explícita NÃO altera a pergunta "este banco é de homologação?".
        Assert.Equal(esperado, AmbienteIntegracaoSap.EhHomologacao(nomeBanco));
    }
}
