using FugaPET_Dev.Servicos.Entrada.P5;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// Liberação explícita do portão R7.
/// <para>
/// O portão era incondicional no fonte, e a bandeira <c>MutacoesHomologadas</c> nem era consultada
/// pelos pontos que o aplicavam. Por decisão registrada do usuário, a abertura passou a ser
/// declarada por ambiente — e não removida do código, o que abriria o envio em TODOS os ambientes
/// de uma vez.
/// </para>
/// <para>
/// O que estes testes protegem é o <b>fail-closed</b>: o padrão é BLOQUEADO e só o literal booleano
/// verdadeiro libera. Errar para o lado permissivo aqui significa POST real no SAP lançando a
/// quantidade integral do pedido.
/// </para>
/// </summary>
public sealed class BloqueioP5R7LiberacaoTests
{
    private static Func<string, string?> Ambiente(string? valor) => _ => valor;

    [Fact]
    public void VariavelAusente_NaoLibera()
        => Assert.False(BloqueioP5R7.MutacoesLiberadas(Ambiente(null)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("sim")]
    [InlineData("yes")]
    [InlineData("homologado")]
    [InlineData("truthy")]
    public void ValorNaoBooleano_NaoLibera(string valor)
        => Assert.False(BloqueioP5R7.MutacoesLiberadas(Ambiente(valor)));

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    public void FalseExplicito_NaoLibera(string valor)
        => Assert.False(BloqueioP5R7.MutacoesLiberadas(Ambiente(valor)));

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("  true  ")]
    public void TrueExplicito_Libera(string valor)
        => Assert.True(BloqueioP5R7.MutacoesLiberadas(Ambiente(valor)));

    [Fact]
    public void NomeDaVariavel_EhODesteGate()
    {
        // Nome proprio: reusar o de outro gate faria uma liberacao ligar a outra em silencio.
        Assert.Equal("FUGAPET_SAP_P5_R7_LIBERADO", BloqueioP5R7.VariavelLiberacao);
    }

    [Fact]
    public void SemVariavelNoProcesso_OPortaoSegueFechado()
    {
        // Estado real deste processo de teste: nada definido => bloqueado. É a garantia de que
        // nenhum ambiente herda a liberação sem declará-la.
        Assert.False(BloqueioP5R7.MutacoesHomologadas);
    }
}
