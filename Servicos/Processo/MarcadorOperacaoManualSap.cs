using FugaPET_Dev.Modelo.IntegracaoSap;

namespace FugaPET_Dev.Servicos.Processo;

/// <summary>Classificacao de uma operacao da OP quanto ao apontamento manual FugaPET.</summary>
public enum ClassificacaoOperacaoManual
{
    /// <summary>OperationStandardTextCode == "PP_FORM": exige apontamento manual.</summary>
    Manual,

    /// <summary>Standard Text Code obtido, porem diferente de PP_FORM (inclui vazio explicito): automatica no SAP.</summary>
    Automatica,

    /// <summary>Contrato do roteiro nao resolvido (ausencia/ambiguidade/campo nao obtido): FAIL-CLOSED.</summary>
    ContratoNaoResolvido
}

/// <summary>
/// GATE 048-E: DONO UNICO da regra PP_FORM. Nenhuma outra classe (Controller, Form, configuracao local)
/// compara a literal "PP_FORM". A comparacao usa EXCLUSIVAMENTE OperationStandardTextCode; NUNCA
/// OperationControlProfile (YBP1/YBP4/QM01). Nada de numeros de operacao hardcodados.
/// </summary>
public static class MarcadorOperacaoManualSap
{
    /// <summary>Unico literal do marcador manual. Ponto unico da verdade.</summary>
    public const string CodigoTextoPadraoManual = "PP_FORM";

    /// <summary>True quando o Standard Text Code (ja trimado) e exatamente PP_FORM (case-insensitive).</summary>
    public static bool EhCodigoManual(string? codigoTextoPadrao)
        => string.Equals(
            (codigoTextoPadrao ?? string.Empty).Trim(),
            CodigoTextoPadraoManual,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Normaliza o numero da operacao para o cruzamento roteiro↔OP por VALOR (nunca por posicao): remove
    /// espacos e zeros a esquerda. "0050"→"50", "50"→"50", "0105"→"105", ""→"", "0"→"".
    /// </summary>
    public static string NormalizarOperacao(string? valor)
        => (valor ?? string.Empty).Trim().TrimStart('0');

    /// <summary>
    /// Classifica UMA operacao da OP contra o roteiro AUTORITATIVO ja resolvido. Cruza por valor normalizado.
    /// Correspondencia necessaria: ausencia OU ambiguidade (mais de uma operacao de roteiro para o mesmo
    /// numero) ⇒ ContratoNaoResolvido. Campo OperationStandardTextCode nao obtido ⇒ ContratoNaoResolvido.
    /// Vazio/whitespace explicitamente obtido ⇒ Automatica. "PP_FORM" ⇒ Manual.
    /// </summary>
    public static ClassificacaoOperacaoManual ClassificarOperacao(RoteiroProducaoSap? roteiro, string? operacaoOp)
    {
        if (roteiro is null || roteiro.Operacoes.Count == 0)
        {
            return ClassificacaoOperacaoManual.ContratoNaoResolvido;
        }

        string alvo = NormalizarOperacao(operacaoOp);
        if (alvo.Length == 0)
        {
            return ClassificacaoOperacaoManual.ContratoNaoResolvido;
        }

        List<OperacaoRoteiroSap> correspondentes = roteiro.Operacoes
            .Where(o => string.Equals(NormalizarOperacao(o.Operacao), alvo, StringComparison.Ordinal))
            .ToList();

        // Correspondencia unica e obrigatoria (§3/§4): 0 = ausencia; >1 = ambiguidade — ambos fail-closed.
        if (correspondentes.Count != 1)
        {
            return ClassificacaoOperacaoManual.ContratoNaoResolvido;
        }

        OperacaoRoteiroSap operacaoRoteiro = correspondentes[0];

        // CAMPO NAO OBTIDO != NULL EXPLICITO: sem a propriedade nao ha como afirmar automatica com seguranca.
        if (!operacaoRoteiro.TextoPadraoObtido)
        {
            return ClassificacaoOperacaoManual.ContratoNaoResolvido;
        }

        return EhCodigoManual(operacaoRoteiro.CodigoTextoPadrao)
            ? ClassificacaoOperacaoManual.Manual
            : ClassificacaoOperacaoManual.Automatica;
    }

    /// <summary>
    /// Operacoes MANUAIS da OP, enriquecidas com o marcador PP_FORM. Vazio quando o roteiro nao resolve
    /// (fail-closed) — a grade e a sequencia (anterior/proxima) passam a operar SO sobre manuais.
    /// Preserva a ordem de <paramref name="ordem"/>.Operacoes (a ordenacao tecnica e aplicada a jusante).
    /// </summary>
    public static IReadOnlyList<OperacaoOrdemProducaoSap> FiltrarOperacoesManuais(
        OrdemProducaoSap ordem, RoteiroProducaoSap? roteiro)
    {
        if (roteiro is null || roteiro.Operacoes.Count == 0)
        {
            return [];
        }

        List<OperacaoOrdemProducaoSap> manuais = [];
        foreach (OperacaoOrdemProducaoSap operacao in ordem.Operacoes)
        {
            if (ClassificarOperacao(roteiro, operacao.Operacao) == ClassificacaoOperacaoManual.Manual)
            {
                manuais.Add(operacao with { CodigoTextoPadrao = CodigoTextoPadraoManual });
            }
        }

        return manuais;
    }
}
