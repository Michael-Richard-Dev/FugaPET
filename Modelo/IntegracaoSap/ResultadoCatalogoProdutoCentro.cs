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

/// <summary>
/// Codigos dos materiais de um centro, como o SAP os lista hoje.
/// <para>
/// Etapa separada porque e a UNICA que a sincronizacao precisa fazer por inteiro toda vez: e assim
/// que se descobre o que entrou e o que saiu do centro. O cadastro de cada material so e pedido
/// para quem ainda nao se conhece.
/// </para>
/// </summary>
public sealed record ResultadoCodigosCentro
{
    public bool Sucesso { get; init; }
    public IReadOnlyList<string> Codigos { get; init; } = [];
    public MotivoCatalogoIndisponivel Motivo { get; init; } = MotivoCatalogoIndisponivel.Nenhum;
    public string MensagemDiagnostico { get; init; } = string.Empty;

    /// <summary>Chamadas HTTP gastas nesta etapa.</summary>
    public int ChamadasSap { get; init; }

    public static ResultadoCodigosCentro Ok(IReadOnlyList<string> codigos, int chamadas)
        => new() { Sucesso = true, Codigos = codigos, ChamadasSap = chamadas };

    public static ResultadoCodigosCentro Falha(string mensagem, MotivoCatalogoIndisponivel motivo, int chamadas = 0)
        => new() { Sucesso = false, Motivo = motivo, MensagemDiagnostico = mensagem, ChamadasSap = chamadas };
}

/// <summary>Cadastro (tipo, unidade, descricao) de um conjunto de materiais.</summary>
public sealed record ResultadoCadastroProdutos
{
    public bool Sucesso { get; init; }
    public IReadOnlyList<ProdutoAcabadoDoCentro> Itens { get; init; } = [];
    public string MensagemDiagnostico { get; init; } = string.Empty;
    public int ChamadasSap { get; init; }

    public static ResultadoCadastroProdutos Ok(IReadOnlyList<ProdutoAcabadoDoCentro> itens, int chamadas)
        => new() { Sucesso = true, Itens = itens, ChamadasSap = chamadas };

    public static ResultadoCadastroProdutos Falha(string mensagem, int chamadas = 0)
        => new() { Sucesso = false, MensagemDiagnostico = mensagem, ChamadasSap = chamadas };
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
