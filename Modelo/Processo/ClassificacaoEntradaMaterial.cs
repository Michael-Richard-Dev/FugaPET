namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Tarefa Entrada 24.1 (Ajuste 8): classificação de um ITEM do Pedido de Compra para a Entrada,
/// a partir do tipo mestre do material (ProductType do Product Master / A_Product). NÃO usa o
/// cabeçalho do pedido nem a descrição textual — usa o CÓDIGO do ProductType do material do item.
/// </summary>
public enum ClassificacaoEntradaMaterial
{
    /// <summary>Sem ProductType consultado/definido — não libera o item por chute (bloqueia).</summary>
    Indefinido = 0,

    /// <summary>ROH — matéria-prima.</summary>
    MateriaPrima = 1,

    /// <summary>HIBE — químico / consumível operacional.</summary>
    Quimico = 2,

    /// <summary>VERP — material de embalagem.</summary>
    Embalagem = 3,

    /// <summary>FERT — produto acabado.</summary>
    ProdutoAcabado = 4,

    /// <summary>HALB — semiacabado / produto intermediário.</summary>
    Semiacabado = 5,

    /// <summary>Outros tipos SAP não mapeados para os módulos de Entrada.</summary>
    Outro = 6
}

/// <summary>
/// Tarefa Entrada 24.1 (Ajuste 6/14): classificador central e ISOLADO do item de pedido por ProductType.
/// A regra técnica usa o CÓDIGO do ProductType; a descrição amigável é só para exibição/diagnóstico.
/// </summary>
public static class ClassificadorItemEntradaMaterial
{
    /// <summary>
    /// Ajuste 6: ROH→MatériaPrima; HIBE→Químico; VERP→Embalagem; FERT→ProdutoAcabado; HALB→Semiacabado;
    /// vazio→Indefinido (não libera por chute — Ajuste 15); demais→Outro. A regra usa o código, não o texto.
    /// </summary>
    public static ClassificacaoEntradaMaterial ClassificarPorProductType(string? productType)
    {
        string tipo = (productType ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tipo))
        {
            return ClassificacaoEntradaMaterial.Indefinido;
        }

        return tipo switch
        {
            "ROH" => ClassificacaoEntradaMaterial.MateriaPrima,
            "HIBE" => ClassificacaoEntradaMaterial.Quimico,
            "VERP" => ClassificacaoEntradaMaterial.Embalagem,
            "FERT" => ClassificacaoEntradaMaterial.ProdutoAcabado,
            "HALB" => ClassificacaoEntradaMaterial.Semiacabado,
            _ => ClassificacaoEntradaMaterial.Outro
        };
    }

    /// <summary>True quando a classificação do item corresponde ao modo atual da tela de Entrada.</summary>
    public static bool ItemPertenceAoModo(ClassificacaoEntradaMaterial classificacao, ModoEntradaMaterial modo)
        => modo == ModoEntradaMaterial.Quimico
            ? classificacao == ClassificacaoEntradaMaterial.Quimico
            : classificacao == ClassificacaoEntradaMaterial.MateriaPrima;
}
