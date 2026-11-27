using System.Reflection;
using FugaPET_Dev.Servicos.IntegracaoSap;
using Xunit;

namespace FugaPET_Dev.Tests.IntegracaoSap;

/// <summary>
/// DECORADOR QUE EMBRULHA UM SERVICO TEM DE IMPLEMENTAR TODOS OS MEMBROS DA INTERFACE.
/// <para>
/// Regressao real de 27/11/2026 no QAS110: o metodo <c>ConsultarEstadoFresco261Async</c> ganhou o
/// parametro <c>materialEsperado</c> e o decorador <see cref="ProductionOrderSapGovernadoServico"/>
/// ficou com a assinatura antiga, de 2 parametros. Como o membro da interface tem implementacao
/// DEFAULT, o compilador NAO reclamou: o metodo do decorador deixou de implementar o membro e toda
/// chamada pela interface passou a cair no corpo default — "Leitura fresca da OP nao suportada por
/// este servico SAP: bloqueado (nenhum POST)". Nenhuma caixa era enviada, com o servico real
/// intacto atras do decorador.
/// </para>
/// <para>
/// A cobranca e por REFLEXAO e vale para qualquer membro e qualquer decorador futuro: quem tem um
/// campo do tipo da interface existe para REPASSAR, e repassar pelo corpo default e nao repassar.
/// Implementacoes que NAO embrulham ninguem (mock, servico de configuracao invalida) podem usar o
/// default de proposito — nelas o default e o comportamento fail-closed correto.
/// </para>
/// </summary>
public sealed class LeituraFrescaChegaAoServicoRealTests
{
    [Fact]
    public void TodoDecoradorDeProductionOrder_ImplementaTodosOsMembros()
    {
        Type tipoInterface = typeof(IProductionOrderSapServico);
        Type[] decoradores = [.. tipoInterface.Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(tipoInterface.IsAssignableFrom)
            .Where(EmbrulhaOutroServico)];

        Assert.NotEmpty(decoradores);

        List<string> faltando = [];
        foreach (Type decorador in decoradores)
        {
            InterfaceMapping mapa = decorador.GetInterfaceMap(tipoInterface);
            for (int i = 0; i < mapa.InterfaceMethods.Length; i++)
            {
                if (mapa.TargetMethods[i].DeclaringType == tipoInterface)
                {
                    faltando.Add($"{decorador.Name}.{mapa.InterfaceMethods[i].Name}");
                }
            }
        }

        Assert.True(
            faltando.Count == 0,
            "decorador caindo na implementacao DEFAULT da interface em vez de repassar: "
            + string.Join(", ", faltando)
            + ". Confira se a assinatura acompanhou a interface, inclusive parametros opcionais.");
    }

    private static bool EmbrulhaOutroServico(Type tipo)
        => tipo.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Any(campo => campo.FieldType == typeof(IProductionOrderSapServico));
}
