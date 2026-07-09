namespace FugaPET_Dev.Modelo.Processo;

public enum TipoClassificacaoOrdemConsumo
{
    Desconhecida = 0,
    MateriaPrima = 1,
    Quimico = 2
}

public sealed record ResultadoClassificacaoOrdemConsumo(
    TipoClassificacaoOrdemConsumo Classificacao,
    string CampoUsado,
    string ValorClassificado,
    string Mensagem);

/// <summary>
/// Classifica a OP para o módulo de consumo usando uma fonte centralizada e trocável.
/// Hoje o SAP expõe de forma confiável apenas o material produzido da OP; as listas ficam vazias
/// até Richard/SAP validarem os códigos oficiais. Sem código validado, a OP é bloqueada como desconhecida.
/// </summary>
public sealed class ClassificadorOrdemConsumoMaterial
{
    private const string CampoMaterialProduzido = "MaterialProduzido";

    private readonly HashSet<string> _produtosMateriaPrima;
    private readonly HashSet<string> _produtosQuimicos;

    public static ClassificadorOrdemConsumoMaterial Padrao { get; } = new();

    public ClassificadorOrdemConsumoMaterial(
        IEnumerable<string>? produtosMateriaPrima = null,
        IEnumerable<string>? produtosQuimicos = null)
    {
        _produtosMateriaPrima = NormalizarConjunto(produtosMateriaPrima);
        _produtosQuimicos = NormalizarConjunto(produtosQuimicos);
    }

    public ResultadoClassificacaoOrdemConsumo Classificar(OrdemProducaoConsumo? ordem)
    {
        string produto = NormalizarProduto(ordem?.MaterialProduzido);
        if (string.IsNullOrWhiteSpace(produto))
        {
            return new ResultadoClassificacaoOrdemConsumo(
                TipoClassificacaoOrdemConsumo.Desconhecida,
                CampoMaterialProduzido,
                string.Empty,
                "Produto da OP não informado pelo SAP.");
        }

        bool materiaPrima = _produtosMateriaPrima.Contains(produto);
        bool quimico = _produtosQuimicos.Contains(produto);
        if (materiaPrima && quimico)
        {
            return new ResultadoClassificacaoOrdemConsumo(
                TipoClassificacaoOrdemConsumo.Desconhecida,
                CampoMaterialProduzido,
                produto,
                "Produto da OP classificado em mais de um processo. Revise a parametrização.");
        }

        if (materiaPrima)
        {
            return new ResultadoClassificacaoOrdemConsumo(
                TipoClassificacaoOrdemConsumo.MateriaPrima,
                CampoMaterialProduzido,
                produto,
                "Produto classificado como Matéria-Prima.");
        }

        if (quimico)
        {
            return new ResultadoClassificacaoOrdemConsumo(
                TipoClassificacaoOrdemConsumo.Quimico,
                CampoMaterialProduzido,
                produto,
                "Produto classificado como Químico.");
        }

        return new ResultadoClassificacaoOrdemConsumo(
            TipoClassificacaoOrdemConsumo.Desconhecida,
            CampoMaterialProduzido,
            produto,
            "Produto da OP sem classificação de processo configurada.");
    }

    public bool OrdemPertenceAoModo(
        OrdemProducaoConsumo? ordem,
        ModoConsumoMaterial modo,
        out ResultadoClassificacaoOrdemConsumo classificacao)
    {
        classificacao = Classificar(ordem);
        return classificacao.Classificacao switch
        {
            TipoClassificacaoOrdemConsumo.MateriaPrima => modo == ModoConsumoMaterial.MateriaPrima,
            TipoClassificacaoOrdemConsumo.Quimico => modo == ModoConsumoMaterial.Quimico,
            _ => false
        };
    }

    public static string NomeModo(ModoConsumoMaterial modo)
        => modo == ModoConsumoMaterial.Quimico ? "Consumo Químico" : "Consumo Matéria-Prima";

    public static string NomeClassificacao(TipoClassificacaoOrdemConsumo classificacao)
        => classificacao switch
        {
            TipoClassificacaoOrdemConsumo.MateriaPrima => "Matéria-Prima",
            TipoClassificacaoOrdemConsumo.Quimico => "Químico",
            _ => "Desconhecida"
        };

    private static HashSet<string> NormalizarConjunto(IEnumerable<string>? produtos)
        => produtos is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : produtos
                .Select(NormalizarProduto)
                .Where(produto => !string.IsNullOrWhiteSpace(produto))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizarProduto(string? produto)
        => (produto ?? string.Empty).Trim().ToUpperInvariant();
}
