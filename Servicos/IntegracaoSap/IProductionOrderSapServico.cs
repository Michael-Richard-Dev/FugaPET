using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.IntegracaoSap;

/// <summary>
/// Entrada unica da integracao de Ordens de Producao SAP (API_PRODUCTION_ORDER_2_SRV) usada pela
/// Tela de Consumo de Materia-Prima. A escolha entre real, demonstracao e configuracao invalida
/// pertence a <see cref="FabricaProductionOrderSapServico"/>.
/// </summary>
public interface IProductionOrderSapServico
{
    bool EhSimulado { get; }
    bool Configurado { get; }

    Task<ResultadoConsultaOrdemProducaoSap> ConsultarOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrdemProducaoSap>> ListarOrdensRelevantesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrdemProducaoSap>>([]);

    /// <summary>
    /// GATE 118B: leitura FRESCA de item + componentes da OP, exigida IMEDIATAMENTE antes do calculo
    /// definitivo do 261. Implementacao DEFAULT e fail-closed: quem nao implementa declara
    /// INDISPONIVEL, e o alocador bloqueia. Nunca devolve estado vazio como se fosse valido.
    /// </summary>
    Task<LeituraFrescaOrdem261> ConsultarEstadoFresco261Async(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
        => Task.FromResult(LeituraFrescaOrdem261.Indisponivel(
            numeroOrdem ?? string.Empty,
            "Leitura fresca da OP nao suportada por este servico SAP: bloqueado (nenhum POST)."));
}

