using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>Estado da correlação exata entre a ocorrência da OP e o roteiro V3 (seam 095E-R1).</summary>
public enum EstadoCorrelacaoOcorrencia
{
    /// <summary>Exatamente 1 operação de roteiro casa por Operation+Plant+WorkCenter.</summary>
    Correlacionada,

    /// <summary>Nenhuma operação de roteiro casa a tripla — fail-closed (OPERATION_OCCURRENCE_NOT_FOUND).</summary>
    NaoEncontrada,

    /// <summary>Mais de uma operação de roteiro casa EXATAMENTE a tripla — fail-closed (OPERATION_OCCURRENCE_AMBIGUOUS).</summary>
    Ambigua
}

/// <summary>Resultado da correlação: quando Correlacionada, carrega o roteiro reduzido a UMA operação.</summary>
public sealed record ResultadoCorrelacaoOcorrencia(
    EstadoCorrelacaoOcorrencia Estado,
    RoteiroProducaoSap? RoteiroReduzido,
    int Encontrados);

/// <summary>
/// GATE 095F / seam autoritativo 095E-R1 — componente PURO da correlação da ocorrência. Recebe o roteiro V3
/// COMPLETO e a ocorrência corrente da OP e devolve o roteiro REDUZIDO a exatamente uma operação, correlacionada
/// por três predicados exatos:
///   Normalize(Operation V3) == Normalize(Operation OP)   [Trim + TrimStart('0')]
///   Trim(Plant V3)          == Trim(Plant OP)            [textual]
///   Trim(WorkCenter V3)     == Trim(WorkCenter OP)       [textual — NUNCA parse numérico]
///
/// Cardinalidade (§6): 0 = NaoEncontrada; 1 = Correlacionada; &gt;1 (matches EXATOS) = Ambigua. Duas operações
/// com o mesmo Operation mas WorkCenter diferente NÃO são ambíguas por si — só o match exato da tripla conta.
///
/// Responsabilidade única: NÃO faz HTTP/DB/UI/config, NÃO decide tipo_processo, NÃO faz routing local e NÃO
/// aplica PP_FORM (isso permanece com MarcadorOperacaoManualSap, sobre o roteiro reduzido).
/// </summary>
public static class CorrelacionadorOcorrenciaRoteiroSap
{
    public static ResultadoCorrelacaoOcorrencia Correlacionar(
        RoteiroProducaoSap? roteiro, OperacaoOrdemProducaoSap? ocorrencia)
    {
        if (roteiro is null || ocorrencia is null || roteiro.Operacoes.Count == 0)
        {
            return new ResultadoCorrelacaoOcorrencia(EstadoCorrelacaoOcorrencia.NaoEncontrada, null, 0);
        }

        string operacaoAlvo = MarcadorOperacaoManualSap.NormalizarOperacao(ocorrencia.Operacao);
        string plantAlvo = (ocorrencia.Centro ?? string.Empty).Trim();
        string workCenterAlvo = (ocorrencia.CentroTrabalho ?? string.Empty).Trim();

        List<OperacaoRoteiroSap> exatos = roteiro.Operacoes
            .Where(o =>
                string.Equals(MarcadorOperacaoManualSap.NormalizarOperacao(o.Operacao), operacaoAlvo, StringComparison.Ordinal)
                && string.Equals((o.Plant ?? string.Empty).Trim(), plantAlvo, StringComparison.Ordinal)
                && string.Equals((o.WorkCenter ?? string.Empty).Trim(), workCenterAlvo, StringComparison.Ordinal))
            .ToList();

        return exatos.Count switch
        {
            0 => new ResultadoCorrelacaoOcorrencia(EstadoCorrelacaoOcorrencia.NaoEncontrada, null, 0),
            1 => new ResultadoCorrelacaoOcorrencia(
                    EstadoCorrelacaoOcorrencia.Correlacionada,
                    roteiro with { Operacoes = [exatos[0]] },
                    1),
            _ => new ResultadoCorrelacaoOcorrencia(EstadoCorrelacaoOcorrencia.Ambigua, null, exatos.Count)
        };
    }
}
