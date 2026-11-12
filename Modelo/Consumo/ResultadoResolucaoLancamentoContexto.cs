namespace FugaPET_Dev.Modelo.Consumo;

/// <summary>
/// GATE 049 CODE-05 — cardinalidade da resolução de um lançamento PENDENTE_SAP já persistido para a
/// ocorrência operacional atual (Contexto.Operacao + Sequencia), resolvida por MATCH item-a-item.
/// </summary>
public enum CardinalidadeResolucaoLancamento
{
    /// <summary>Nenhum candidato válido: nada é escolhido, envio não é habilitado por recovery.</summary>
    Zero = 0,

    /// <summary>Exatamente um candidato válido: PK resolvida para envio explícito.</summary>
    Um = 1,

    /// <summary>Mais de um candidato válido: NÃO resolver automaticamente; seleção explícita no histórico.</summary>
    Varios = 2
}

/// <summary>
/// Resultado explícito e fail-closed da resolução contextual. NUNCA usa último/maior/mais recente:
/// só cardinalidade do conjunto de candidatos cujos itens casam 1:1 com componentes distintos da ocorrência.
/// </summary>
public sealed class ResultadoResolucaoLancamentoContexto
{
    public CardinalidadeResolucaoLancamento Cardinalidade { get; init; }

    /// <summary>PK do lançamento SOMENTE quando Cardinalidade == Um; caso contrário null.</summary>
    public long? CodigoLancamento { get; init; }

    /// <summary>Códigos previamente validados (usado apenas quando Cardinalidade == Varios).</summary>
    public IReadOnlyList<long> Candidatos { get; init; } = [];

    public static ResultadoResolucaoLancamentoContexto Zero { get; } =
        new() { Cardinalidade = CardinalidadeResolucaoLancamento.Zero };

    public static ResultadoResolucaoLancamentoContexto Um(long codigoLancamento)
        => new() { Cardinalidade = CardinalidadeResolucaoLancamento.Um, CodigoLancamento = codigoLancamento };

    public static ResultadoResolucaoLancamentoContexto Varios(IReadOnlyList<long> candidatos)
        => new() { Cardinalidade = CardinalidadeResolucaoLancamento.Varios, Candidatos = candidatos };
}
