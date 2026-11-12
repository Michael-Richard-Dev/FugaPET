namespace FugaPET_Dev.Modelo.Consumo;

/// <summary>
/// GATE 101J — modalidade da resolução READ-ONLY do estado de consumo PERSISTIDO correspondente à
/// ocorrência operacional atual (OP + operação/sequência via conjunto de componentes já filtrado),
/// resolvida por MATCH item-a-item. Fail-closed: nunca escolhe "mais recente"/menor/maior PK.
/// </summary>
public enum ModalidadeRecuperacaoConsumo
{
    /// <summary>Nenhum lançamento persistido corresponde à ocorrência: fluxo novo normal.</summary>
    Nenhum = 0,

    /// <summary>Exatamente UM PENDENTE_SAP elegível (doc/ano nulos, match exato): recuperar esse PK.</summary>
    UmPendente = 1,

    /// <summary>Mais de um PENDENTE_SAP elegível para a MESMA ocorrência: AMBÍGUO, fail-closed.</summary>
    AmbiguoPendente = 2,

    /// <summary>
    /// Existe lançamento ENVIANDO_SAP correspondente: reconciliação pendente. Não recupera para envio,
    /// mas bloqueia novo consumo. Autoridade do bloqueio vem do estado persistido (sobrevive a restart).
    /// </summary>
    EnviandoReconciliacao = 3,

    /// <summary>
    /// GATE 101L — FAIL-CLOSED: NÃO foi possível comprovar com segurança se existe consumo persistido que
    /// bloqueia/precisa de recovery (exceção/falha técnica ao resolver). NUNCA equivale a "não existe estado":
    /// bloqueia toda nova operação até uma nova resolução READ-ONLY concluir COM SUCESSO. Só o sucesso da
    /// consulta pode transitar para <see cref="Nenhum"/>.
    /// </summary>
    FalhaResolucaoPersistida = 4
}

/// <summary>
/// Resultado explícito e fail-closed da resolução contextual do consumo persistido. Só carrega PK quando
/// <see cref="Modalidade"/> == <see cref="ModalidadeRecuperacaoConsumo.UmPendente"/>.
/// </summary>
public sealed class ResultadoRecuperacaoConsumoContexto
{
    public ModalidadeRecuperacaoConsumo Modalidade { get; init; }

    /// <summary>PK do lançamento SOMENTE quando <see cref="Modalidade"/> == UmPendente; caso contrário null.</summary>
    public long? CodigoLancamento { get; init; }

    /// <summary>Códigos PENDENTE_SAP validados quando AmbiguoPendente (diagnóstico; nunca autosseleção).</summary>
    public IReadOnlyList<long> Candidatos { get; init; } = [];

    public static ResultadoRecuperacaoConsumoContexto Nenhum { get; } =
        new() { Modalidade = ModalidadeRecuperacaoConsumo.Nenhum };

    public static ResultadoRecuperacaoConsumoContexto UmPendente(long codigoLancamento)
        => new() { Modalidade = ModalidadeRecuperacaoConsumo.UmPendente, CodigoLancamento = codigoLancamento };

    public static ResultadoRecuperacaoConsumoContexto AmbiguoPendente(IReadOnlyList<long> candidatos)
        => new() { Modalidade = ModalidadeRecuperacaoConsumo.AmbiguoPendente, Candidatos = candidatos };

    public static ResultadoRecuperacaoConsumoContexto EnviandoReconciliacao(IReadOnlyList<long> correspondentes)
        => new() { Modalidade = ModalidadeRecuperacaoConsumo.EnviandoReconciliacao, Candidatos = correspondentes };

    /// <summary>GATE 101L: estado FAIL-CLOSED de falha técnica na resolução (nunca "Nenhum").</summary>
    public static ResultadoRecuperacaoConsumoContexto FalhaResolucaoPersistida { get; } =
        new() { Modalidade = ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida };
}

/// <summary>
/// GATE 101L — política FAIL-CLOSED, PURA e testável, do recovery de consumo. Centraliza as decisões de
/// bloqueio para que a Form e os testes compartilhem exatamente a mesma regra. Regra de ouro: SOMENTE
/// <see cref="ModalidadeRecuperacaoConsumo.Nenhum"/> libera fluxo novo; qualquer outro valor — inclusive um
/// valor de enum NÃO reconhecido — bloqueia (default-deny).
/// </summary>
public static class RecuperacaoConsumoPolitica
{
    /// <summary>True quando a modalidade BLOQUEIA novo consumo (leitura/pesagem/save/lançamento). Fail-closed:
    /// só <see cref="ModalidadeRecuperacaoConsumo.Nenhum"/> não bloqueia; desconhecido ⇒ bloqueia.</summary>
    public static bool BloqueiaNovoConsumo(ModalidadeRecuperacaoConsumo modalidade)
        => modalidade != ModalidadeRecuperacaoConsumo.Nenhum;

    /// <summary>True SOMENTE quando é seguro materializar/executar o envio 261 recuperado: exatamente um
    /// PENDENTE recuperado E com PK. Falha/ambíguo/enviando/desconhecido ⇒ false (zero capability/claim/HTTP).</summary>
    public static bool PermiteEnvioRecuperado(ModalidadeRecuperacaoConsumo modalidade, long? codigoLancamentoRecuperado)
        => modalidade == ModalidadeRecuperacaoConsumo.UmPendente && codigoLancamentoRecuperado.HasValue;

    /// <summary>
    /// GATE 101N — mapeamento CANÔNICO e DEFAULT-DENY da modalidade resolvida para a modalidade EFETIVA que a
    /// tela deve aplicar. Cada valor conhecido mapeia para si mesmo; QUALQUER valor fora do domínio (enum não
    /// reconhecido / resultado inconsistente) mapeia para <see cref="ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida"/>.
    /// NUNCA mapeia um valor desconhecido para <see cref="ModalidadeRecuperacaoConsumo.Nenhum"/>. A Form decide
    /// o wiring por ESTA função, então testá-la é testar a decisão real da tela.
    /// </summary>
    public static ModalidadeRecuperacaoConsumo ModalidadeEfetiva(ModalidadeRecuperacaoConsumo modalidade)
        => modalidade switch
        {
            ModalidadeRecuperacaoConsumo.Nenhum => ModalidadeRecuperacaoConsumo.Nenhum,
            ModalidadeRecuperacaoConsumo.UmPendente => ModalidadeRecuperacaoConsumo.UmPendente,
            ModalidadeRecuperacaoConsumo.AmbiguoPendente => ModalidadeRecuperacaoConsumo.AmbiguoPendente,
            ModalidadeRecuperacaoConsumo.EnviandoReconciliacao => ModalidadeRecuperacaoConsumo.EnviandoReconciliacao,
            ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida => ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida,
            _ => ModalidadeRecuperacaoConsumo.FalhaResolucaoPersistida
        };
}
