namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// GATE 048-E: operacao de roteiro (A_ProductionRoutingOperation) reduzida ao necessario para o marcador
/// PP_FORM. <see cref="CodigoTextoPadrao"/> = Trim(OperationStandardTextCode). <see cref="TextoPadraoObtido"/>
/// distingue o VAZIO EXPLICITAMENTE OBTIDO (propriedade veio null/whitespace ⇒ AUTOMATICA) do CAMPO NAO
/// OBTIDO (propriedade ausente por falha de contrato/mapeamento ⇒ FAIL-CLOSED). Nunca colapsar os dois.
/// </summary>
public sealed record OperacaoRoteiroSap
{
    /// <summary>Numero da operacao do roteiro (ex.: "50", "105", "140"). Cruzado com a OP por VALOR normalizado.</summary>
    public string Operacao { get; init; } = string.Empty;

    /// <summary>Trim(OperationStandardTextCode). So tem significado quando <see cref="TextoPadraoObtido"/> = true.</summary>
    public string CodigoTextoPadrao { get; init; } = string.Empty;

    /// <summary>True quando a propriedade OperationStandardTextCode foi realmente lida (mesmo vazia).</summary>
    public bool TextoPadraoObtido { get; init; }
}

/// <summary>
/// GATE 048-E REV2: roteiro resolvido de forma AUTORITATIVA a partir da ProductionVersion da OP
/// (OP → Material/Plant/ProductionVersion → API_PRODUCTION_VERSION → BillOfOperationsType/Group/Variant →
/// ProductionRoutingOperation). NUNCA escolhido pela primeira ocorrencia de ProductionRoutingMatlAssgmt,
/// menor contador, mais recente ou heuristica. Ausencia/ambiguidade em qualquer etapa ⇒ roteiro null (fail-closed).
/// </summary>
public sealed record RoteiroProducaoSap
{
    public string BillOfOperationsGroup { get; init; } = string.Empty;    // ProductionRoutingGroup (ex.: 50000063)
    public string BillOfOperationsVariant { get; init; } = string.Empty;  // ProductionRouting (ex.: 1)
    public IReadOnlyList<OperacaoRoteiroSap> Operacoes { get; init; } = [];
}
