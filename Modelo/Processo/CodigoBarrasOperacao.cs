namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Resultado da interpretação do código de barras da operação da OP. Sempre preserva
/// <see cref="CodigoOriginal"/> exatamente como foi lido — o código regenerado NUNCA o substitui.
/// O código completo nunca é convertido para número (perderia zeros à esquerda).
/// </summary>
public sealed class CodigoBarrasOperacao
{
    /// <summary>Código exatamente como lido pelo leitor. Fonte de verdade para auditoria.</summary>
    public string CodigoOriginal { get; init; } = string.Empty;

    /// <summary>OP como aparece no código (com zeros à esquerda). Ex.: 000001001710.</summary>
    public string OrdemNormalizada { get; init; } = string.Empty;

    /// <summary>OP funcional/chave SAP (sem zeros à esquerda). Ex.: 1001710.</summary>
    public string OrdemProducao { get; init; } = string.Empty;

    /// <summary>Operação como aparece no código (com zeros). com zeros preservados.</summary>
    public string Operacao { get; init; } = string.Empty;

    /// <summary>Sufixo do evento como lido. Ex.: 01 / 02.</summary>
    public string CodigoEvento { get; init; } = string.Empty;

    /// <summary>Evento interpretado. Só é significativo quando <see cref="Valido"/>.</summary>
    public TipoEventoOperacao? TipoEvento { get; init; }

    /// <summary>Versão do formato aplicado na interpretação (ex.: OP_OPERACAO_EVENTO_V1).</summary>
    public string FormatoVersao { get; init; } = string.Empty;

    public bool Valido { get; init; }

    /// <summary>Motivo funcional quando inválido. Vazio quando válido.</summary>
    public string MensagemValidacao { get; init; } = string.Empty;

    public static CodigoBarrasOperacao Invalido(string codigoOriginal, string formatoVersao, string mensagem)
        => new()
        {
            CodigoOriginal = codigoOriginal ?? string.Empty,
            FormatoVersao = formatoVersao,
            Valido = false,
            MensagemValidacao = mensagem
        };
}
