using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Tela.Comum;

namespace FugaPET_Dev.Tests.Seguranca;

/// <summary>
/// Comportamento de bloqueio das telas simuladas (UsaDadosSimulados): elas so podem abrir em
/// MODO DEMONSTRACAO. No ambiente de teste (sem banco.modo_demonstracao) o modo demonstracao e
/// false, entao a regra de decisao deve BLOQUEAR — prova o comportamento seguro por padrao.
/// </summary>
public sealed class TelaSimuladaBloqueioTests
{
    [Fact] // #7
    public void ForaDoModoDemonstracao_TelaSimuladaDeveSerBloqueada()
    {
        // Pre-condicao do ambiente de teste: nao estamos em modo demonstracao.
        Assert.False(EstadoIntegracaoBanco.ModoDemonstracao);

        // A regra que os 6 forms simulados usam no construtor:
        //   if (!PodeUsarDadosSimulados()) { BloquearTelaSimulada(this); return; }
        // Logo, fora do modo demonstracao a tela simulada NAO carrega (fica bloqueada).
        Assert.False(AvisoDadosSimuladosHelper.PodeUsarDadosSimulados());
    }

    [Fact]
    public void PodeUsarDadosSimulados_DeveRefletirModoDemonstracao()
    {
        // A decisao de liberar dado simulado e exatamente o modo demonstracao (fonte unica).
        Assert.Equal(EstadoIntegracaoBanco.ModoDemonstracao, AvisoDadosSimuladosHelper.PodeUsarDadosSimulados());
    }
}
