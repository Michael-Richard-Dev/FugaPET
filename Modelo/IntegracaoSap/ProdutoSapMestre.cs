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
}
