namespace FugaPET_Dev.Modelo.Cadastro;

/// <summary>
/// Um material do centro como o SAP o descreveu na ULTIMA sincronizacao.
/// <para>
/// E espelho, nao fonte de verdade: a verdade do cadastro do produto continua no SAP. O que esta
/// aqui serve para a tela abrir sem rede e para a sincronizacao saber o que ja conhece.
/// </para>
/// </summary>
public sealed class ProdutoCatalogoLocal
{
    public long CodigoCatalogo { get; set; }
    public string Centro { get; set; } = string.Empty;
    public string Produto { get; set; } = string.Empty;

    public string? Descricao { get; set; }
    public string? TipoMaterial { get; set; }
    public string? UnidadeBase { get; set; }
    public bool MarcadoEliminacao { get; set; }

    /// <summary>Quando o SAP confirmou, pela ultima vez, que o material existe neste centro.</summary>
    public DateTime VistoEm { get; set; }

    /// <summary>
    /// Quando tipo, unidade e descricao foram lidos. NULO significa "sei que existe no centro, mas
    /// ainda nao perguntei o resto" — e e exatamente esse nulo que torna a sincronizacao incremental.
    /// </summary>
    public DateTime? MestreLidoEm { get; set; }

    /// <summary>
    /// False quando o SAP parou de listar o material neste centro. A linha NAO e apagada: pode haver
    /// vinculo decidido sobre ela, e sumir em silencio e pior do que mostrar que sumiu.
    /// </summary>
    public bool PresenteNoSap { get; set; } = true;

    public bool MestrePendente => MestreLidoEm is null;
}

/// <summary>
/// O que uma sincronizacao fez. Serve para a tela dizer ao operador o que mudou — "nada novo" e uma
/// resposta util, e sem ela ele fica sem saber se o botao funcionou.
/// </summary>
public sealed record ResumoSincronizacaoCatalogo
{
    public bool Sucesso { get; init; }
    public string Mensagem { get; init; } = string.Empty;

    /// <summary>Materiais que o centro passou a ter desde a ultima sincronizacao.</summary>
    public int Novos { get; init; }

    /// <summary>Materiais cujo cadastro foi lido do SAP nesta rodada (os novos e os que faltavam).</summary>
    public int MestresLidos { get; init; }

    /// <summary>Materiais que o SAP deixou de listar neste centro.</summary>
    public int Ausentes { get; init; }

    /// <summary>Total de materiais que o centro tem hoje, segundo o SAP.</summary>
    public int TotalNoCentro { get; init; }

    /// <summary>Quantas chamadas HTTP foram feitas — o numero que o usuario quer ver cair.</summary>
    public int ChamadasSap { get; init; }

    public static ResumoSincronizacaoCatalogo Falha(string mensagem)
        => new() { Sucesso = false, Mensagem = mensagem };
}
