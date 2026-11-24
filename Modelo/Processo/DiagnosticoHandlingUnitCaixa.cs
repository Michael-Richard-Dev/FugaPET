namespace FugaPET_Dev.Modelo.Processo;

/// <summary>Por que uma HU bipada na Paletizacao nao entrou na selecao.</summary>
public enum MotivoHuIndisponivel
{
    /// <summary>A HU entrou; nao ha motivo.</summary>
    Disponivel,

    /// <summary>Nenhuma caixa com essa HU existe neste sistema.</summary>
    Desconhecida,

    /// <summary>A caixa existe, mas o SAP ainda nao confirmou — e e a HU confirmada que a identifica.</summary>
    NaoConfirmadaNoSap,

    /// <summary>A caixa foi cancelada/inativada.</summary>
    Inativa,

    /// <summary>A caixa ja esta vinculada a um palete.</summary>
    JaPaletizada,
}

/// <summary>
/// O que o sistema sabe sobre uma HU bipada que NAO entrou na selecao da Paletizacao.
/// <para>
/// Existe porque, ate 23/11/2026, todos os casos davam a MESMA resposta — "HU nao encontrada". Com
/// ela, "a caixa ja esta no palete 7", "o SAP ainda nao confirmou" e "essa HU nao existe aqui" eram
/// indistinguiveis, e o operador nao tinha o que fazer a nao ser bipar de novo. Sao problemas
/// diferentes, com acoes diferentes.
/// </para>
/// <para>
/// NAO e uma segunda regra de elegibilidade: a regra continua sendo a da consulta que a tela usa
/// (`fn_pa_047_caixa_buscar_hu_exata` sobre `vw_hu_caixas_sem_palete`). Isto aqui so EXPLICA a
/// recusa, depois dela — e por isso espelha as condicoes da view em vez de inventar as suas.
/// </para>
/// </summary>
public sealed record DiagnosticoHandlingUnitCaixa
{
    public required string HandlingUnit { get; init; }
    public required MotivoHuIndisponivel Motivo { get; init; }

    /// <summary>Status gravado da caixa, quando ela existe. Vazio quando a HU e desconhecida.</summary>
    public string StatusCaixa { get; init; } = string.Empty;

    /// <summary>Palete que ja contem a caixa, quando <see cref="MotivoHuIndisponivel.JaPaletizada"/>.</summary>
    public long? CodigoPalete { get; init; }

    public int NumeroCaixa { get; init; }
    public string NumeroOrdemProducao { get; init; } = string.Empty;
    public string Material { get; init; } = string.Empty;

    /// <summary>
    /// Terminal que pesou a caixa. Vai na mensagem SO quando ajuda a localizar fisicamente a caixa —
    /// nunca como justificativa da recusa, porque o terminal NAO entra na regra: a consulta e do banco
    /// compartilhado e ignora quem pesou (verificado nas tres camadas em 23/11/2026).
    /// </summary>
    public string Terminal { get; init; } = string.Empty;

    /// <summary>
    /// Decide o motivo a partir dos fatos da caixa. A ORDEM E A DA VIEW
    /// <c>vw_hu_caixas_sem_palete</c>: inativa e "nao confirmada" excluem ANTES de o vinculo de palete
    /// ser avaliado. Invertida, a mensagem diria "ja esta no palete 7" de uma caixa cancelada — e
    /// explicaria a recusa errada, mandando o operador procurar no palete em vez de olhar a caixa.
    /// <para>Funcao pura para a ordem ser testavel sem banco.</para>
    /// </summary>
    public static MotivoHuIndisponivel DecidirMotivo(bool ativa, string? status, long? codigoPalete)
        => !ativa ? MotivoHuIndisponivel.Inativa
         : !string.Equals((status ?? string.Empty).Trim(), "CONFIRMADA_SAP", StringComparison.OrdinalIgnoreCase)
             ? MotivoHuIndisponivel.NaoConfirmadaNoSap
         : codigoPalete is not null ? MotivoHuIndisponivel.JaPaletizada
         : MotivoHuIndisponivel.Disponivel;

    /// <summary>Mensagem para o operador: diz o motivo E o que fazer.</summary>
    public string Mensagem() => Motivo switch
    {
        MotivoHuIndisponivel.Desconhecida =>
            $"HU {HandlingUnit} não existe neste sistema. Confira o número ou verifique se a caixa "
            + "foi pesada em outro ambiente.",

        MotivoHuIndisponivel.NaoConfirmadaNoSap =>
            $"HU {HandlingUnit} (caixa {NumeroCaixa}, OP {NumeroOrdemProducao}) ainda não foi "
            + $"confirmada no SAP — está em {StatusCaixa}. Envie a caixa ao SAP antes de paletizar.",

        MotivoHuIndisponivel.Inativa =>
            $"HU {HandlingUnit} (caixa {NumeroCaixa}, OP {NumeroOrdemProducao}) está cancelada e não "
            + "pode ser paletizada.",

        MotivoHuIndisponivel.JaPaletizada =>
            $"HU {HandlingUnit} (caixa {NumeroCaixa}, OP {NumeroOrdemProducao}) já está no palete "
            + $"{CodigoPalete}. Uma caixa não pode entrar em dois paletes.",

        _ => string.Empty,
    };
}
