namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tarefa Consumo 22.9.1: classificação de um COMPONENTE da OP para os processos de consumo,
/// a partir do tipo mestre do material (ProductType do Product Master) e, quando necessário,
/// do grupo de material (ProductGroup). NÃO é o papel operacional na OP (isso vem da BOM).
/// </summary>
public enum ClassificacaoConsumoMaterial
{
    /// <summary>Sem ProductType consultado/definido — não libera consumo por chute (bloqueia).</summary>
    Indefinido = 0,

    /// <summary>ROH — matéria-prima consumível.</summary>
    MateriaPrima = 1,

    /// <summary>HIBE (ou ProductGroup químico homologado) — consumível/químico operacional.</summary>
    Quimico = 2,

    /// <summary>VERP — material de embalagem (não libera em MP/Químico sem regra explícita).</summary>
    Embalagem = 3,

    /// <summary>Outros tipos (FERT/HALB/…): não classificados automaticamente como componente de consumo.</summary>
    Outro = 4
}

/// <summary>
/// Classificador central e ISOLADO de componente por modo de consumo, usando ProductType/ProductGroup
/// do Product Master (API_PRODUCT_SRV / A_Product). A regra técnica usa o CÓDIGO do ProductType;
/// a descrição amigável é só para exibição/diagnóstico.
/// </summary>
public static class ClassificadorComponenteConsumo
{
    // Tarefa Consumo 22.9.1 (Ajuste 5): estrutura central para grupos químicos homologados (vazia até validar com SAP).
    // Não inventar grupos: enquanto vazia, HIBE já basta para classificar como Químico.
    private static readonly HashSet<string> _gruposQuimicosHomologados =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ajuste 4: descrição amigável do ProductType (a regra técnica usa o código, não o texto).</summary>
    public static string ObterDescricaoTipoMaterialSap(string? productType)
        => (productType ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ROH" => "Matéria-prima",
            "FERT" => "Produto acabado",
            "HALB" => "Semiacabado / produto intermediário",
            "HAWA" => "Mercadoria para revenda",
            "VERP" => "Material de embalagem",
            "HIBE" => "Suprimento operacional / consumível",
            "ERSA" => "Peça de reposição",
            "NLAG" => "Material não estocável",
            "UNBW" => "Material não valorizado",
            "SERV" => "Serviço",
            "DIEN" => "Serviço",
            "KMAT" => "Material configurável",
            _ => "Tipo SAP não mapeado"
        };

    /// <summary>
    /// Ajuste 5: classifica o componente por ProductType (+ ProductGroup quando aplicável).
    /// ROH→MatériaPrima; HIBE (ou grupo químico homologado)→Químico; VERP→Embalagem; vazio→Indefinido;
    /// demais (FERT/HALB/…)→Outro. Não classifica "componente" por ProductType (papel operacional vem da BOM).
    /// </summary>
    public static ClassificacaoConsumoMaterial ClassificarComponenteParaConsumo(string? productType, string? productGroup)
    {
        string tipo = (productType ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return ClassificacaoConsumoMaterial.Indefinido;
        }

        return tipo switch
        {
            "ROH" => ClassificacaoConsumoMaterial.MateriaPrima,
            "HIBE" => ClassificacaoConsumoMaterial.Quimico,
            "VERP" => ClassificacaoConsumoMaterial.Embalagem,
            _ when GrupoQuimicoHomologado(productGroup) => ClassificacaoConsumoMaterial.Quimico,
            _ => ClassificacaoConsumoMaterial.Outro
        };
    }

    private static bool GrupoQuimicoHomologado(string? productGroup)
        => !string.IsNullOrWhiteSpace(productGroup)
           && _gruposQuimicosHomologados.Contains(productGroup.Trim());
}
