namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Estados da integração de UMA caixa individual de Produto Acabado (Handling Unit). Centraliza o
/// ciclo de vida — nunca usar strings livres espalhadas pela Form/Controller. As transições válidas
/// estão em <see cref="TransicaoStatusIntegracaoCaixa"/>.
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
/// Máquina de transições válidas do <see cref="StatusIntegracaoCaixa"/>. Transição inválida é bloqueada
/// (nunca aplicada). Não há retrocesso silencioso; ERRO_SAP só volta a PRONTA_PARA_ENVIO (reprocessamento).
/// </summary>
public static class TransicaoStatusIntegracaoCaixa
{
    private static readonly IReadOnlyDictionary<StatusIntegracaoCaixa, IReadOnlyList<StatusIntegracaoCaixa>> _validas =
        new Dictionary<StatusIntegracaoCaixa, IReadOnlyList<StatusIntegracaoCaixa>>
        {
            [StatusIntegracaoCaixa.EmPesagem] =
                [StatusIntegracaoCaixa.FinalizadaLocal, StatusIntegracaoCaixa.Cancelada, StatusIntegracaoCaixa.Bloqueada],
            [StatusIntegracaoCaixa.FinalizadaLocal] =
                [StatusIntegracaoCaixa.PreviewHuGerado, StatusIntegracaoCaixa.Cancelada, StatusIntegracaoCaixa.Bloqueada],
            [StatusIntegracaoCaixa.PreviewHuGerado] =
                [StatusIntegracaoCaixa.AguardandoAutorizacaoSap, StatusIntegracaoCaixa.ProntaParaEnvio,
                 StatusIntegracaoCaixa.Cancelada, StatusIntegracaoCaixa.Bloqueada],
            [StatusIntegracaoCaixa.AguardandoAutorizacaoSap] =
                [StatusIntegracaoCaixa.ProntaParaEnvio, StatusIntegracaoCaixa.Cancelada, StatusIntegracaoCaixa.Bloqueada],
            [StatusIntegracaoCaixa.ProntaParaEnvio] =
                [StatusIntegracaoCaixa.EnviandoSap, StatusIntegracaoCaixa.Cancelada, StatusIntegracaoCaixa.Bloqueada],
            [StatusIntegracaoCaixa.EnviandoSap] =
                [StatusIntegracaoCaixa.ConfirmadaSap, StatusIntegracaoCaixa.ErroSap],
            [StatusIntegracaoCaixa.ErroSap] =
                [StatusIntegracaoCaixa.ProntaParaEnvio, StatusIntegracaoCaixa.Cancelada, StatusIntegracaoCaixa.Bloqueada],
            // Estados terminais/sem saída nesta fase.
            [StatusIntegracaoCaixa.ConfirmadaSap] = [],
            [StatusIntegracaoCaixa.Cancelada] = [],
            [StatusIntegracaoCaixa.Bloqueada] = [StatusIntegracaoCaixa.Cancelada]
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

/// <summary>Transição de status inválida na integração da caixa (bloqueio de fluxo, não erro técnico).</summary>
public sealed class TransicaoStatusInvalidaException(StatusIntegracaoCaixa origem, StatusIntegracaoCaixa destino)
    : Exception($"Transição de status inválida: {origem} → {destino}.")
{
    public StatusIntegracaoCaixa Origem { get; } = origem;
    public StatusIntegracaoCaixa Destino { get; } = destino;
}
