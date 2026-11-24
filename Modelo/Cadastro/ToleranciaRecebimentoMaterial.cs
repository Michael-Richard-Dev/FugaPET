namespace FugaPET_Dev.Modelo.Cadastro;

/// <summary>
/// Decisao do usuario sobre UM material do centro: quanto a pesagem do Recebimento pode divergir da
/// quantidade do pedido de compra, para mais e para menos.
/// <para>
/// O catalogo de materiais NAO mora aqui — vem do SAP, espelhado em <c>produto_catalogo_centro</c>
/// (incremento 066). Esta entidade guarda so a decisao, mais um retrato do que o SAP dizia quando ela
/// foi tomada.
/// </para>
/// <para>
/// PERCENTUAL UNICO, valido para os dois lados (decisao do usuario em 23/11/2026). Zero e valido e
/// significa exigencia exata; a AUSENCIA de tolerancia se expressa pela ausencia de linha, nunca por
/// um valor especial.
/// </para>
/// </summary>
public sealed class ToleranciaRecebimentoMaterial : IToleranciaPercentual
{
    public const string CentroPadrao = "3007";

    /// <summary>Maior percentual aceito. Acima de 100 o limite inferior ficaria negativo.</summary>
    public const decimal PercentualMaximo = 100m;

    public long CodigoTolerancia { get; set; }
    public string Centro { get; set; } = CentroPadrao;
    public string Material { get; set; } = string.Empty;

    public decimal PercentualTolerancia { get; set; }

    /// <summary>Retrato do SAP no momento do cadastro — nunca fonte de verdade do material.</summary>
    public string? DescricaoMaterial { get; set; }
    public string? TipoMaterial { get; set; }

    public bool Situacao { get; set; } = true;
    public DateTime? CriadoEm { get; set; }
    public long? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public long? AtualizadoPor { get; set; }
}

/// <summary>
/// Uma linha da tela de tolerancia: o material como o catalogo local o conhece, somado a decisao ja
/// gravada, se houver. Espelha <c>LinhaVinculoProduto</c>, pela mesma razao: os dois lados vem de
/// fontes diferentes e podem discordar, entao a linha carrega os dois sem tentar reconcilia-los.
/// </summary>
public sealed class LinhaToleranciaMaterial
{
    public required string Material { get; init; }

    public string? DescricaoCatalogo { get; init; }
    public string? TipoMaterialCatalogo { get; init; }

    /// <summary>Decisao gravada. Nulo = material ainda NAO configurado.</summary>
    public ToleranciaRecebimentoMaterial? Tolerancia { get; init; }

    /// <summary>
    /// O material veio do catalogo nesta consulta. Falso significa que a linha existe so porque ha
    /// tolerancia gravada para um material que o centro nao lista mais — mostrado em vez de escondido,
    /// porque uma tolerancia orfa continua decidindo bloqueio no Recebimento.
    /// </summary>
    public bool PresenteNoCatalogo { get; init; }

    /// <summary>Configurado = tem linha E esta ativa. Inativa conta como NAO configurado.</summary>
    public bool Configurado => Tolerancia is not null && Tolerancia.Situacao;

    public string DescricaoParaExibir
        => !string.IsNullOrWhiteSpace(DescricaoCatalogo) ? DescricaoCatalogo!
         : !string.IsNullOrWhiteSpace(Tolerancia?.DescricaoMaterial) ? Tolerancia!.DescricaoMaterial!
         : string.Empty;

    public string ToleranciaParaExibir
        => Tolerancia is null ? "Não configurado"
         : !Tolerancia.Situacao ? "Inativa"
         : $"± {Tolerancia.PercentualTolerancia.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))} %";
}
