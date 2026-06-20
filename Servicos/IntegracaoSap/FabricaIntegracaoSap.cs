using FugaPET_Dev.AcessoDados.Banco;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Fabrica legada do contrato de historico/apontamento simulado.
/// Mantida somente para compatibilidade e bloqueada fora do modo demonstracao.
/// </summary>
[Obsolete(
    "Fabrica legada. Para pedidos de compra use FabricaPedidoCompraSapServico.",
    false)]
public static class FabricaIntegracaoSap
{
    public static IIntegracaoSapServico Criar()
        => Criar(
            EstadoIntegracaoBanco.Habilitado,
            EstadoIntegracaoBanco.ModoDemonstracao,
            EstadoIntegracaoBanco.AmbienteDemonstrativo);

    internal static IIntegracaoSapServico Criar(
        bool bancoHabilitado,
        bool modoDemonstracao,
        bool ambienteDemonstrativo)
    {
        bool podeUsarDadosSimulados =
            EstadoIntegracaoBanco.CalcularPodeUsarDadosSimulados(
                bancoHabilitado,
                modoDemonstracao,
                ambienteDemonstrativo);
        if (!podeUsarDadosSimulados)
        {
            throw new InvalidOperationException(
                "A integracao SAP simulada exige banco desabilitado, modo demonstracao ativo e ambiente demonstrativo.");
        }

        return new IntegracaoSapMockServico(podeUsarDadosSimulados);
    }
}
