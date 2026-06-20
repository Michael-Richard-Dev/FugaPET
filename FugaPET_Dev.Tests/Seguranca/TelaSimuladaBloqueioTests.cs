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

    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, true, false)]
    public void PodeUsarDadosSimulados_DeveExigirAsTresCondicoes(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo,
        bool esperado)
    {
        Assert.Equal(
            esperado,
            EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(
                bancoHabilitado,
                modoDemonstracao,
                ambienteDemonstrativo));
    }

    [Fact]
    public void AmbienteNaoDemonstrativo_DeveBloquearMesmoComBancoOffEDemoOn()
    {
        Assert.False(
            EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(
                bancoHabilitado: false,
                modoDemonstracao: true,
                ambienteDemonstrativo: false));
    }
}
