namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>
/// DTO cru do Product Plant SAP (API_PRODUCT_SRV / A_ProductPlant, OData V2). Preserva os nomes
/// originais do SAP. O codigo do material e o centro NAO sao convertidos para numero (mantem zeros
/// a esquerda). Fonte EXCLUSIVA de <c>IsBatchManagementRequired</c> (material × centro) — nunca
/// misturar com dados tecnicos de A_Product.
/// </summary>
public sealed class SapProductPlantDto
{
    public string Product { get; set; } = string.Empty;
    public string Plant { get; set; } = string.Empty;

    /// <summary>Edm.Boolean; null quando ausente/indeterminado na resposta.</summary>
    public bool? IsBatchManagementRequired { get; set; }
}

/// <summary>
/// Modelo interno da configuracao SAP de administracao de lote por material × centro (A_ProductPlant).
/// <see cref="Consultado"/>=true SOMENTE quando o GET retornou o registro; <see cref="IsBatchManagementRequired"/>
/// null significa "indeterminado" (bloqueia o envio — nunca assume true nem false).
/// </summary>
public sealed class ProdutoCentroSapMestre
{
    public string Material { get; init; } = string.Empty;
    public string Centro { get; init; } = string.Empty;

    /// <summary>Administracao de lote exigida no centro; null = indeterminado (bloqueia).</summary>
    public bool? IsBatchManagementRequired { get; init; }

    /// <summary>True quando A_ProductPlant foi consultado com sucesso para este material × centro.</summary>
    public bool Consultado { get; init; }

    /// <summary>Pronto para decidir o payload: consultado e com a flag de lote definida (true/false).</summary>
    public bool AdministracaoLoteDefinida => Consultado && IsBatchManagementRequired.HasValue;

    public static ProdutoCentroSapMestre DeDto(SapProductPlantDto dto)
        => new()
        {
            Material = (dto.Product ?? string.Empty).Trim(),
            Centro = (dto.Plant ?? string.Empty).Trim(),
            IsBatchManagementRequired = dto.IsBatchManagementRequired,
            Consultado = true
        };
}
