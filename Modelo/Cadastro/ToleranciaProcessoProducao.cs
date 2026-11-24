namespace FugaPET_Dev.Modelo.Cadastro;

/// <summary>
/// Os processos de produção que têm tolerância própria (incremento 071).
/// <para>
/// São as telas que derivam do Apontamento. O Recebimento NÃO entra aqui: ele tem a sua tolerância
/// desde o 069, com chave e semântica próprias (pedido de compra, não ordem de produção).
/// </para>
/// </summary>
public static class ProcessoToleranciaProducao
{
    public const string Consumo = "CONSUMO";
    public const string SemiAcabado = "SEMI_ACABADO";

    public static readonly IReadOnlyList<string> Todos = [Consumo, SemiAcabado];

    public static string Descrever(string processo) => processo switch
    {
        Consumo => "Consumo de Material",
        SemiAcabado => "Produto Semi-Acabado",
        _ => processo,
    };

    public static bool EhValido(string? processo)
        => processo is not null && Todos.Contains(processo, StringComparer.Ordinal);
}

/// <summary>
/// Quanto a pesagem de UM produto pode divergir do previsto, DENTRO DE UM PROCESSO de produção.
/// <para>
/// A chave inclui o processo por decisão do usuário em 24/11/2026: um semi-acabado produzido numa OP
/// é consumido em outra, então o mesmo código aparece nas duas telas — e "quanto posso produzir a
/// mais" não é a mesma decisão que "quanto posso consumir a mais".
/// </para>
/// <para>
/// Mesma forma do <see cref="ToleranciaRecebimentoMaterial"/> do 069, de propósito: percentual único
/// para os dois lados, zero significando exigência exata, e a AUSÊNCIA de tolerância expressa pela
/// ausência de linha — nunca por um valor especial.
/// </para>
/// </summary>
public sealed class ToleranciaProcessoProducao : IToleranciaPercentual
{
    public const string CentroPadrao = ToleranciaRecebimentoMaterial.CentroPadrao;
    public const decimal PercentualMaximo = ToleranciaRecebimentoMaterial.PercentualMaximo;

    public long CodigoTolerancia { get; set; }
    public string Centro { get; set; } = CentroPadrao;

    /// <summary>Um de <see cref="ProcessoToleranciaProducao"/>.</summary>
    public string Processo { get; set; } = ProcessoToleranciaProducao.Consumo;

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
/// Uma linha da tela de tolerância de processo: o produto como o catálogo local o conhece, somado à
/// decisão já gravada PARA AQUELE PROCESSO, se houver.
/// </summary>
public sealed class LinhaToleranciaProcesso
{
    public required string Processo { get; init; }
    public required string Material { get; init; }

    public string? DescricaoCatalogo { get; init; }
    public string? TipoMaterialCatalogo { get; init; }

    /// <summary>Decisão gravada. Nulo = produto ainda NÃO configurado neste processo.</summary>
    public ToleranciaProcessoProducao? Tolerancia { get; init; }

    /// <summary>
    /// O produto veio do catálogo nesta consulta. Falso significa que a linha existe só porque há
    /// tolerância gravada para um produto que o centro não lista mais — mostrada em vez de escondida,
    /// porque uma tolerância órfã continua decidindo bloqueio na produção.
    /// </summary>
    public bool PresenteNoCatalogo { get; init; }

    /// <summary>Configurado = tem linha E está ativa. Inativa conta como NÃO configurado.</summary>
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
