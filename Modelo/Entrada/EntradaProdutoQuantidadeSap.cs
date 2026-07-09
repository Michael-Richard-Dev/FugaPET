using System.Globalization;

namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Tarefa Entrada 23.2: normalização/serialização da quantidade líquida (KG) enviada ao SAP no movimento 101.
/// Centraliza a conversão de tara, a formatação decimal INVARIANTE (nunca cultura pt-BR / separador de milhar)
/// e a guarda de coerência entre a quantidade SAP e o peso líquido calculado. Peso líquido = bruto - tara(KG).
/// </summary>
public static class EntradaProdutoQuantidadeSap
{
    /// <summary>Tolerância de coerência quantidade SAP × peso líquido (Ajuste 5).</summary>
    public const decimal ToleranciaCoerenciaKg = 0.001m;

    /// <summary>
    /// Ajuste 2: converte a tara para KG conforme a unidade. "G"/"GR"/"GRAMAS" → valor/1000; "KG" → valor;
    /// unidade vazia/desconhecida → assume KG e registra diagnóstico (não inventa conversão). Centraliza a regra.
    /// </summary>
    public static decimal ConverterTaraParaKg(decimal valorTara, string? unidadeTara)
    {
        string unidade = (unidadeTara ?? string.Empty).Trim().ToUpperInvariant();
        switch (unidade)
        {
            case "KG":
                return valorTara;
            case "G":
            case "GR":
            case "GRAMAS":
                return valorTara / 1000m;
            case "":
                System.Diagnostics.Trace.TraceInformation(
                    $"[Entrada][Tara] Unidade de tara vazia; assumindo KG para o valor {valorTara.ToString("0.###", CultureInfo.InvariantCulture)}.");
                return valorTara;
            default:
                System.Diagnostics.Trace.TraceWarning(
                    $"[Entrada][Tara] Unidade de tara não mapeada '{unidade}'; assumindo KG para o valor {valorTara.ToString("0.###", CultureInfo.InvariantCulture)}.");
                return valorTara;
        }
    }

    /// <summary>
    /// Ajuste 4: QuantityInEntryUnit a partir do peso líquido em KG, sempre InvariantCulture ("0.###"),
    /// sem separador de milhar. 1.5m → "1.5"; 1.500m → "1.5". NUNCA "1500" nem "1,5".
    /// </summary>
    public static string FormatarQuantidadeSap(decimal pesoLiquidoKg)
        => pesoLiquidoKg.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Ajuste 5: a quantidade SAP (em KG) deve bater com o peso líquido calculado (diferença ≤ 0,001 KG).</summary>
    public static bool QuantidadeCoerente(decimal quantidadeSapKg, decimal pesoLiquidoKg)
        => Math.Abs(quantidadeSapKg - pesoLiquidoKg) <= ToleranciaCoerenciaKg;
}
