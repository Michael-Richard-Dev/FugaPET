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
        => Criar(EstadoIntegracaoBanco.ModoDemonstracao);

    internal static IIntegracaoSapServico Criar(bool modoDemonstracao)
    {
        if (!modoDemonstracao)
        {
            throw new InvalidOperationException(
                "A integracao SAP simulada so pode ser criada no modo DEMONSTRACAO.");
        }

        return new IntegracaoSapMockServico();
    }
}
