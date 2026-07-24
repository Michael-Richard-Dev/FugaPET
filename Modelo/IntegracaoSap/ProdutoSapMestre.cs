namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// Tarefa Consumo 22.9.1 (Ajuste 3): DTO cru do Product Master SAP (API_PRODUCT_SRV / A_Product).
/// Preserva os nomes originais do SAP. Códigos NÃO são convertidos para número (mantêm zeros).
/// </summary>
public sealed class SapProductMasterDto
{
    public string Product { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string ProductGroup { get; set; } = string.Empty;
    public string BaseUnit { get; set; } = string.Empty;
}

/// <summary>
/// Tarefa Consumo 22.10 (Ajuste 1): DTO cru da descrição do material SAP (API_PRODUCT_SRV / A_ProductDescription).
/// A descrição REAL do produto/material vem daqui (ProductDescription), NÃO de A_Product. Código preservado (Trim).
/// </summary>
public sealed class SapProductDescriptionDto
{
    public string Product { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string ProductDescription { get; set; } = string.Empty;
}

/// <summary>
/// Modelo interno do tipo mestre do material (enriquecido a partir do <see cref="SapProductMasterDto"/>).
/// </summary>
public sealed class ProdutoSapMestre
{
    public string CodigoProduto { get; set; } = string.Empty;
    public string TipoMaterialSap { get; set; } = string.Empty;
    public string DescricaoTipoMaterial { get; set; } = string.Empty;
    public string GrupoMaterialSap { get; set; } = string.Empty;
    public string UnidadeBaseSap { get; set; } = string.Empty;

    /// <summary>
    /// Tarefa Consumo 22.10 (Ajuste 2): descrição REAL do produto/material vinda de A_ProductDescription
    /// (ProductDescription). NÃO confundir com <see cref="DescricaoTipoMaterial"/> (texto do ProductType).
    /// </summary>
    public string DescricaoProdutoSap { get; set; } = string.Empty;

    /// <summary>Idioma da descrição escolhida (PT/P/EN/…), apenas para diagnóstico.</summary>
    public string IdiomaDescricaoSap { get; set; } = string.Empty;

    /// <summary>True quando o Product Master foi consultado com sucesso para este material.</summary>
    public bool Consultado { get; set; }

    public static ProdutoSapMestre DeDto(SapProductMasterDto dto)
        => new()
        {
            CodigoProduto = (dto.Product ?? string.Empty).Trim(),
            TipoMaterialSap = (dto.ProductType ?? string.Empty).Trim(),
            GrupoMaterialSap = (dto.ProductGroup ?? string.Empty).Trim(),
            UnidadeBaseSap = (dto.BaseUnit ?? string.Empty).Trim(),
            Consultado = true
        };

    /// <summary>
    /// Tarefa Consumo 22.10 (Ajuste 3): monta o Product Master COMPLETO agregando os dados técnicos de A_Product
    /// (<paramref name="tecnico"/>) com a descrição real de A_ProductDescription (<paramref name="descricao"/>,
    /// já escolhida pelo melhor idioma). A_Product NÃO é fonte de descrição; A_ProductDescription NÃO é fonte de tipo.
    /// </summary>
    public static ProdutoSapMestre Agregar(SapProductMasterDto tecnico, SapProductDescriptionDto? descricao)
    {
        ProdutoSapMestre mestre = DeDto(tecnico);
        if (descricao is not null)
        {
            mestre.DescricaoProdutoSap = (descricao.ProductDescription ?? string.Empty).Trim();
            mestre.IdiomaDescricaoSap = (descricao.Language ?? string.Empty).Trim();
        }

        return mestre;
    }

    /// <summary>
    /// Combina o mestre TÉCNICO (A_Product: ProductType/ProductGroup/BaseUnit) com o mestre de DESCRIÇÃO
    /// (A_ProductDescription). Cada fonte só alimenta o que lhe compete:
    /// <list type="bullet">
    /// <item>técnico → tipo/grupo/unidade e <see cref="Consultado"/>=true (única fonte que libera classificação);</item>
    /// <item>descrição → <see cref="DescricaoProdutoSap"/>/<see cref="IdiomaDescricaoSap"/>.</item>
    /// </list>
    /// Um resultado contendo SOMENTE descrição NUNCA marca <see cref="Consultado"/>=true nem substitui um mestre
    /// técnico já consultado — sem ProductType a classificação permanece Indefinido (bloqueia, não chuta o processo).
    /// O código é preservado como texto (Trim), sem conversão numérica (mantém zeros à esquerda).
    /// </summary>
    public static ProdutoSapMestre Combinar(string codigoProduto, ProdutoSapMestre? tecnico, ProdutoSapMestre? descricao)
    {
        ProdutoSapMestre mestre = new()
        {
            CodigoProduto = (codigoProduto ?? string.Empty).Trim()
        };

        // Dados TÉCNICOS: só de A_Product e só quando efetivamente consultado.
        if (tecnico is not null && tecnico.Consultado)
        {
            mestre.TipoMaterialSap = (tecnico.TipoMaterialSap ?? string.Empty).Trim();
            mestre.GrupoMaterialSap = (tecnico.GrupoMaterialSap ?? string.Empty).Trim();
            mestre.UnidadeBaseSap = (tecnico.UnidadeBaseSap ?? string.Empty).Trim();
            mestre.DescricaoTipoMaterial = tecnico.DescricaoTipoMaterial;
            mestre.Consultado = true;

            if (!string.IsNullOrWhiteSpace(tecnico.CodigoProduto))
            {
                mestre.CodigoProduto = tecnico.CodigoProduto.Trim();
            }
        }

        // DESCRIÇÃO: só de A_ProductDescription; jamais altera tipo/grupo/unidade nem Consultado.
        if (descricao is not null)
        {
            if (!string.IsNullOrWhiteSpace(descricao.DescricaoProdutoSap))
            {
                mestre.DescricaoProdutoSap = descricao.DescricaoProdutoSap.Trim();
            }

            if (!string.IsNullOrWhiteSpace(descricao.IdiomaDescricaoSap))
            {
                mestre.IdiomaDescricaoSap = descricao.IdiomaDescricaoSap.Trim();
            }
        }

        return mestre;
    }
}
