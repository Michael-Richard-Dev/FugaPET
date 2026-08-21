namespace FugaPET_Dev.Modelo.Processo;

/// <summary>Origem da norma de embalagem interpretada (nunca fallback fictício).</summary>
public enum OrigemNormaEmbalagem
{
    Indefinida,
    ApiDev,
    ConsultadaSap
}

/// <summary>
/// Modelo INTERNO da norma de embalagem do Produto Acabado, já INTERPRETADO a partir do DTO cru do SAP.
/// Separado do DTO de resposta (que espelha o JSON). Materiais são strings preservadas.
/// </summary>
public sealed class NormaEmbalagemProdutoAcabado
{
    /// <summary>Material do produto acabado consultado (item tipo I).</summary>
    public string MaterialProduto { get; init; } = string.Empty;

    /// <summary>PackagingInstruction; pode ser vazio quando o SAP não informa.</summary>
    public string CodigoNorma { get; init; } = string.Empty;

    /// <summary>Material da caixa (item tipo P).</summary>
    public string MaterialCaixa { get; init; } = string.Empty;

    /// <summary>Quantidade do item de embalagem (item tipo P).</summary>
    public decimal QuantidadeEmbalagem { get; init; }

    /// <summary>Unidade do item de embalagem (item tipo P).</summary>
    public string UnidadeEmbalagem { get; init; } = string.Empty;

    /// <summary>Quantidade de produtos por caixa (item tipo I).</summary>
    public decimal QuantidadePorCaixa { get; init; }

    /// <summary>Unidade da quantidade por caixa (item tipo I).</summary>
    public string UnidadeQuantidade { get; init; } = string.Empty;

    public OrigemNormaEmbalagem Origem { get; init; } = OrigemNormaEmbalagem.Indefinida;

    /// <summary>Rótulo de status para a tela (ex.: "API DEV", "CONSULTADA SAP").</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Pendências não bloqueantes observadas na interpretação (ex.: PackagingInstruction vazio).</summary>
    public IReadOnlyList<string> Pendencias { get; init; } = [];
}

/// <summary>
/// Resultado da interpretação defensiva da norma. Sucesso ⇒ <see cref="Norma"/> válida e não ambígua;
/// falha ⇒ mensagem identificando material e motivo (nunca prepara preview HU). Sem fallback fictício.
/// </summary>
public sealed class ResultadoNormaEmbalagemProdutoAcabado
{
    private ResultadoNormaEmbalagemProdutoAcabado(
        bool sucesso, string material, string mensagem, NormaEmbalagemProdutoAcabado? norma)
    {
        Sucesso = sucesso;
        Material = material;
        Mensagem = mensagem;
        Norma = norma;
    }

    public bool Sucesso { get; }
    public string Material { get; }
    public string Mensagem { get; }
    public NormaEmbalagemProdutoAcabado? Norma { get; }

    public static ResultadoNormaEmbalagemProdutoAcabado Ok(NormaEmbalagemProdutoAcabado norma)
        => new(true, norma.MaterialProduto, "Norma de embalagem consultada.", norma);

    public static ResultadoNormaEmbalagemProdutoAcabado Bloqueada(string material, string motivo)
        => new(false, material, $"Material {material}: {motivo}", null);
}
