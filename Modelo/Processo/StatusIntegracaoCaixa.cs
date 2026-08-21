namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Estados da integração de UMA caixa individual de Produto Acabado (Handling Unit), ALINHADOS 1:1 ao
/// contrato de banco 044 (coluna <c>status_hu_caixa</c>). Centraliza o ciclo de vida — nunca usar strings
/// livres. As transições válidas espelham exatamente o gatilho <c>fn_hu_caixa_validar_update</c> e estão
/// em <see cref="TransicaoStatusIntegracaoCaixa"/>. O mapeamento texto↔enum fica em <see cref="MapeadorStatusHuCaixa"/>.
/// </summary>
public enum StatusIntegracaoCaixa
{
    EmPesagem,
    FinalizadaLocal,
    PreviewHuGerado,
    AguardandoAutorizacaoSap,
    ProntaParaEnvio,
    EnviandoSap,
    ConfirmadaSap,
    ErroSap,

    /// <summary>
    /// Timeout APÓS possível POST: resultado indeterminado. Nunca é erro comum e NÃO retrocede
    /// automaticamente — só sai por reconciliação (→ ConfirmadaSap) sob decisão do contrato de banco.
    /// </summary>
    IndeterminadoTimeout,

    Bloqueada,
    Cancelada
}

/// <summary>
/// Origem do material de embalagem da caixa: nunca inventar código. NaoInformada mantém o valor vazio
/// até existir norma SAP, configuração real ou informação explícita aprovada do operador.
/// </summary>
public enum OrigemMaterialEmbalagemCaixa
{
    NaoInformada,
    Sap,
    FallbackControlado, // legado contratual; não utilizado para inventar material
    Operador,
    Configuracao
}

/// <summary>
/// Máquina de transições válidas do <see cref="StatusIntegracaoCaixa"/>, espelhando o contrato de banco
/// (fn_hu_caixa_validar_update). Transição inválida é bloqueada (nunca aplicada). Não há retrocesso
/// silencioso: ERRO_SAP só volta a PRONTA_PARA_ENVIO por reprocessamento e INDETERMINADO_TIMEOUT só segue
/// para CONFIRMADA_SAP por reconciliação.
/// </summary>
public static class TransicaoStatusIntegracaoCaixa
{
    private static readonly IReadOnlyDictionary<StatusIntegracaoCaixa, IReadOnlyList<StatusIntegracaoCaixa>> _validas =
        new Dictionary<StatusIntegracaoCaixa, IReadOnlyList<StatusIntegracaoCaixa>>
        {
            [StatusIntegracaoCaixa.EmPesagem] =
                [StatusIntegracaoCaixa.FinalizadaLocal, StatusIntegracaoCaixa.Cancelada],
            [StatusIntegracaoCaixa.FinalizadaLocal] =
                [StatusIntegracaoCaixa.PreviewHuGerado, StatusIntegracaoCaixa.Bloqueada, StatusIntegracaoCaixa.Cancelada],
            [StatusIntegracaoCaixa.PreviewHuGerado] =
                [StatusIntegracaoCaixa.AguardandoAutorizacaoSap, StatusIntegracaoCaixa.Bloqueada, StatusIntegracaoCaixa.Cancelada],
            [StatusIntegracaoCaixa.AguardandoAutorizacaoSap] =
                [StatusIntegracaoCaixa.ProntaParaEnvio, StatusIntegracaoCaixa.Bloqueada, StatusIntegracaoCaixa.Cancelada],
            [StatusIntegracaoCaixa.ProntaParaEnvio] =
                [StatusIntegracaoCaixa.EnviandoSap, StatusIntegracaoCaixa.Bloqueada, StatusIntegracaoCaixa.Cancelada],
            [StatusIntegracaoCaixa.EnviandoSap] =
                [StatusIntegracaoCaixa.ConfirmadaSap, StatusIntegracaoCaixa.ErroSap, StatusIntegracaoCaixa.IndeterminadoTimeout],
            [StatusIntegracaoCaixa.ErroSap] =
                [StatusIntegracaoCaixa.ProntaParaEnvio, StatusIntegracaoCaixa.Cancelada],
            // INDETERMINADO_TIMEOUT: só reconciliação (→ CONFIRMADA_SAP); jamais retrocesso automático.
            [StatusIntegracaoCaixa.IndeterminadoTimeout] =
                [StatusIntegracaoCaixa.ConfirmadaSap],
            [StatusIntegracaoCaixa.Bloqueada] =
                [StatusIntegracaoCaixa.Cancelada],
            // Estados terminais.
            [StatusIntegracaoCaixa.ConfirmadaSap] = [],
            [StatusIntegracaoCaixa.Cancelada] = []
        };

    /// <summary>True quando a transição de <paramref name="origem"/> para <paramref name="destino"/> é permitida.</summary>
    public static bool PodeTransitar(StatusIntegracaoCaixa origem, StatusIntegracaoCaixa destino)
        => origem == destino
            || (_validas.TryGetValue(origem, out IReadOnlyList<StatusIntegracaoCaixa>? destinos)
                && destinos.Contains(destino));

    /// <summary>Aplica a transição ou lança <see cref="TransicaoStatusInvalidaException"/> se inválida.</summary>
    public static StatusIntegracaoCaixa Transitar(StatusIntegracaoCaixa origem, StatusIntegracaoCaixa destino)
        => PodeTransitar(origem, destino)
            ? destino
            : throw new TransicaoStatusInvalidaException(origem, destino);

    /// <summary>Uma caixa nestes estados NÃO pode ser reenviada nem substituída por uma nova caixa ativa.</summary>
    public static bool BloqueiaNovaCaixa(StatusIntegracaoCaixa status)
        => status is not (StatusIntegracaoCaixa.ConfirmadaSap or StatusIntegracaoCaixa.Cancelada);
}

/// <summary>
/// Mapeamento canônico texto do banco (<c>status_hu_caixa</c>) ↔ <see cref="StatusIntegracaoCaixa"/>.
/// Único ponto de tradução — a Form/Controller/Repository nunca comparam strings soltas.
/// </summary>
public static class MapeadorStatusHuCaixa
{
    private static readonly IReadOnlyDictionary<StatusIntegracaoCaixa, string> ParaBanco =
        new Dictionary<StatusIntegracaoCaixa, string>
        {
            [StatusIntegracaoCaixa.EmPesagem] = "EM_PESAGEM",
            [StatusIntegracaoCaixa.FinalizadaLocal] = "FINALIZADA_LOCAL",
            [StatusIntegracaoCaixa.PreviewHuGerado] = "PREVIEW_HU_GERADO",
            [StatusIntegracaoCaixa.AguardandoAutorizacaoSap] = "AGUARDANDO_AUTORIZACAO_SAP",
            [StatusIntegracaoCaixa.ProntaParaEnvio] = "PRONTA_PARA_ENVIO",
            [StatusIntegracaoCaixa.EnviandoSap] = "ENVIANDO_SAP",
            [StatusIntegracaoCaixa.ConfirmadaSap] = "CONFIRMADA_SAP",
            [StatusIntegracaoCaixa.ErroSap] = "ERRO_SAP",
            [StatusIntegracaoCaixa.IndeterminadoTimeout] = "INDETERMINADO_TIMEOUT",
            [StatusIntegracaoCaixa.Bloqueada] = "BLOQUEADA",
            [StatusIntegracaoCaixa.Cancelada] = "CANCELADA"
        };

    private static readonly IReadOnlyDictionary<string, StatusIntegracaoCaixa> DoBanco =
        ParaBanco.ToDictionary(par => par.Value, par => par.Key, StringComparer.OrdinalIgnoreCase);

    public static string ParaTextoBanco(StatusIntegracaoCaixa status) => ParaBanco[status];

    public static StatusIntegracaoCaixa DoTextoBanco(string? status)
        => DoBanco.TryGetValue((status ?? string.Empty).Trim(), out StatusIntegracaoCaixa valor)
            ? valor
            : throw new ArgumentOutOfRangeException(nameof(status), $"status_hu_caixa desconhecido: '{status}'.");
}

/// <summary>Transição de status inválida na integração da caixa (bloqueio de fluxo, não erro técnico).</summary>
public sealed class TransicaoStatusInvalidaException(StatusIntegracaoCaixa origem, StatusIntegracaoCaixa destino)
    : Exception($"Transição de status inválida: {origem} → {destino}.")
{
    public StatusIntegracaoCaixa Origem { get; } = origem;
    public StatusIntegracaoCaixa Destino { get; } = destino;
}
