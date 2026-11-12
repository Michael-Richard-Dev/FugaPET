namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// GATE 118B: snapshot FRESCO da Ordem de Producao lido IMEDIATAMENTE antes do calculo definitivo do
/// 261, conforme o contrato fechado pelo gate 118B-A1 (API_PRODUCTION_ORDER_2_SRV, OData V2):
/// STEP 1 = GET A_ProductionOrderItem_2, STEP 2 = GET A_ProductionOrderComponent_2.
/// <para>
/// NAO representa snapshot atomico. O servico SAP nao documenta garantia transacional entre as duas
/// leituras (sem ETag/concurrency-mode nas EntityTypes envolvidas), portanto este tipo carrega as
/// duas leituras separadamente e o alocador e obrigado a validar consistencia cruzada antes do POST.
/// </para>
/// </summary>
public sealed record LeituraFrescaOrdem261
{
    /// <summary>OP efetivamente consultada (eco do filtro), para checagem cruzada.</summary>
    public string NumeroOrdemConsultada { get; init; } = string.Empty;

    /// <summary>true somente quando AS DUAS leituras retornaram 2xx e foram mapeadas.</summary>
    public bool Disponivel { get; init; }

    /// <summary>Motivo objetivo quando <see cref="Disponivel"/> e false. Sanitizado.</summary>
    public string Mensagem { get; init; } = string.Empty;

    /// <summary>Item da ordem (STEP 1). null = leitura indisponivel/vazia.</summary>
    public ItemOrdemProducaoSap? Item { get; init; }

    /// <summary>Componentes (STEP 2) efetivamente materializados.</summary>
    public IReadOnlyList<ComponenteOrdemProducaoSap> Componentes { get; init; } = [];

    /// <summary>
    /// __count devolvido por $inlinecount=allpages na leitura de componentes. null = o servico nao
    /// informou a contagem; nesse caso a completude NAO esta comprovada (fail-closed no alocador).
    /// </summary>
    public int? ComponentesDeclaradosPeloServico { get; init; }

    /// <summary>
    /// true somente quando a contagem declarada existe E bate com a quantidade recebida (sem
    /// truncamento/paginacao). Ausencia de contagem => false, nunca true por omissao.
    /// </summary>
    public bool ComponentesCompletos =>
        ComponentesDeclaradosPeloServico is int declarado && declarado == Componentes.Count;

    public static LeituraFrescaOrdem261 Indisponivel(string numeroOrdem, string mensagem)
        => new() { NumeroOrdemConsultada = numeroOrdem, Disponivel = false, Mensagem = mensagem };
}
