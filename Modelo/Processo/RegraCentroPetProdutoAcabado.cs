namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Regra CENTRAL e explícita (decisão Hera: HU_CAIXA_HML_MODO_OPERACIONAL_REPETITIVO_AUTORIZADO): o fluxo de
/// Produto Acabado HU de caixa é permitido EXCLUSIVAMENTE para OPs PET do centro 3007. Sem gate por OP e sem
/// autorização por nova OP — porém restrito a este centro. Concentra a constante para evitar "3007" espalhado
/// e permitir defesa em profundidade (Controller na consulta, Form na elegibilidade/ação, Service antes do claim).
/// </summary>
public static class RegraCentroPetProdutoAcabado
{
    /// <summary>Único centro PET autorizado ao Produto Acabado HU. NÃO é gate por OP; nenhuma OP é hardcoded.</summary>
    public const string CentroPetHuPermitido = "3007";

    /// <summary>Mensagem sanitizada padrão para OP/caixa fora do centro PET autorizado.</summary>
    public const string MensagemCentroNaoPermitido =
        "OP não pertence ao centro PET autorizado para Produto Acabado.";

    /// <summary>true somente quando o centro (após trim) for exatamente o centro PET autorizado.</summary>
    public static bool CentroPermitido(string? centro)
        => string.Equals((centro ?? string.Empty).Trim(), CentroPetHuPermitido, System.StringComparison.Ordinal);
}
