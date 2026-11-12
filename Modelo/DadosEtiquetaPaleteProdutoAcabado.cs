namespace FugaPET_Dev.Modelo;

/// <summary>
/// Uma linha do bloco de lotes da etiqueta do palete. Há uma por lote presente no palete —
/// e, como o lote vem da ordem de produção, na prática uma por OP que contribuiu com caixas.
/// <para>
/// <see cref="DataFabricacao"/> e <see cref="DataVencimento"/> não possuem origem no Produto
/// Acabado: nem a caixa, nem a HU, nem o palete as carregam, e o cadastro de material lido
/// pelo sistema não traz prazo de validade. Enquanto a fonte não for definida, saem como
/// NAO INFORMADA — nunca derivadas de suposição.
/// </para>
/// </summary>
public sealed record LinhaLoteEtiquetaPalete(
    string Lote,
    string DataFabricacao,
    string DataVencimento,
    int QuantidadeCaixas);

/// <summary>
/// Contrato da etiqueta do PALETE de Produto Acabado. Todos os campos derivam do palete
/// PERSISTIDO e das caixas que o compõem.
/// </summary>
public sealed class DadosEtiquetaPaleteProdutoAcabado
{
    /// <summary>Sequencial do palete, como exibido ("17747"). Vem do código do palete no banco.</summary>
    public string Numero { get; init; } = string.Empty;

    /// <summary>Código de barras do palete: prefixo fixo mais o sequencial com zeros à esquerda.</summary>
    public string CodigoBarras { get; init; } = string.Empty;

    /// <summary>Data de formação do palete, derivada da caixa mais recente que ele contém.</summary>
    public string Data { get; init; } = string.Empty;

    public string Material { get; init; } = string.Empty;
    public string DescricaoMaterial { get; init; } = string.Empty;

    /// <summary>Quantidade de caixas (ou sacos) do palete.</summary>
    public string QuantidadeCaixas { get; init; } = string.Empty;

    /// <summary>Soma dos produtos de todas as caixas do palete.</summary>
    public string QuantidadeProdutos { get; init; } = string.Empty;

    public string PesoBruto { get; init; } = string.Empty;
    public string PesoLiquido { get; init; } = string.Empty;

    /// <summary>HU SAP do palete; vazia enquanto o palete não for confirmado.</summary>
    public string HandlingUnitSap { get; init; } = string.Empty;

    public string DataHoraImpressao { get; init; } = string.Empty;
    public string Terminal { get; init; } = string.Empty;

    /// <summary>Um item por lote, na ordem em que os lotes aparecem no palete.</summary>
    public IReadOnlyList<LinhaLoteEtiquetaPalete> Lotes { get; init; } = [];
}
