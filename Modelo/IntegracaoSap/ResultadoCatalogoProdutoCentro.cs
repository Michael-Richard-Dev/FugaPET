namespace FugaPET_Dev.Modelo.IntegracaoSap;

/// <summary>Um produto acabado do centro, como a tela de vinculo precisa ve-lo.</summary>
public sealed record ProdutoAcabadoDoCentro
{
    public required string Produto { get; init; }
    public string? Descricao { get; init; }
    public string? TipoMaterial { get; init; }
    public string? UnidadeBase { get; init; }
    public bool MarcadoParaEliminacao { get; init; }
}

/// <summary>Por que a consulta ao catalogo nao devolveu produtos.</summary>
public enum MotivoCatalogoIndisponivel
{
    Nenhum,

    /// <summary>Sem credencial ou sem URL de produto no arquivo de configuracao.</summary>
    NaoConfigurado,

    /// <summary>O SAP respondeu, mas o envelope nao e interpretavel, ou a chamada falhou.</summary>
    FalhaNaConsulta,
}

/// <summary>
/// Resultado da listagem de produtos acabados de um centro.
/// <para>
/// O ponto deste tipo e nao deixar "nao consegui perguntar" parecer "o centro nao tem produtos".
/// Lista vazia com <see cref="Sucesso"/> verdadeiro significa centro realmente sem produto acabado;
/// qualquer outra coisa vem com motivo, e a tela mostra o motivo em vez de uma grade vazia.
/// </para>
/// </summary>
public sealed record ResultadoCatalogoProdutoCentro
{
    public bool Sucesso { get; init; }
    public IReadOnlyList<ProdutoAcabadoDoCentro> Produtos { get; init; } = [];
    public MotivoCatalogoIndisponivel Motivo { get; init; } = MotivoCatalogoIndisponivel.Nenhum;

    /// <summary>Texto ja sanitizado para a tela — nunca carrega credencial nem URL interna.</summary>
    public string MensagemDiagnostico { get; init; } = string.Empty;

    public static ResultadoCatalogoProdutoCentro Ok(IReadOnlyList<ProdutoAcabadoDoCentro> produtos)
        => new() { Sucesso = true, Produtos = produtos };

    public static ResultadoCatalogoProdutoCentro NaoConfigurado()
        => new()
        {
            Sucesso = false,
            Motivo = MotivoCatalogoIndisponivel.NaoConfigurado,
            MensagemDiagnostico =
                "Integração SAP não configurada nesta estação: usuário, senha ou URL de produto ausentes.",
        };

    public static ResultadoCatalogoProdutoCentro Falha(string mensagem)
        => new()
        {
            Sucesso = false,
            Motivo = MotivoCatalogoIndisponivel.FalhaNaConsulta,
            MensagemDiagnostico = mensagem,
        };
}
