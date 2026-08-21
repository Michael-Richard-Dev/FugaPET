namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// GATE 047-CH: contrato MVP CONGELADO do material de embalagem do palete. Até nova decisão SAP, o ÚNICO
/// PackagingMaterial homologado para a integração INT012 é <see cref="Permitido"/> (Ares 047-CF: existe no
/// A_Product consultado + histórico externo de sucesso; PALLET02/03/04/05 NÃO existem ⇒ HTTP 500 / HUs órfãs).
/// Regra CENTRALIZADA: a UI (campo Material embalagem) e o guard PRÉ-CLAIM reutilizam este único ponto,
/// evitando magic strings duplicadas. NÃO inferir/adicionar outros códigos como válidos.
/// </summary>
public static class PackagingMaterialMvp
{
    /// <summary>Único material de embalagem homologado no MVP.</summary>
    public const string Permitido = "PALLET01";

    /// <summary>True apenas quando o material (trim, case-insensitive) é exatamente <see cref="Permitido"/>. Vazio/NULL ⇒ false.</summary>
    public static bool Autorizado(string? material)
        => string.Equals((material ?? string.Empty).Trim(), Permitido, System.StringComparison.OrdinalIgnoreCase);
}
