namespace FugaPET_Dev.Modelo.Cadastro;

/// <summary>
/// Como o produto e pesado. Sao os DOIS processos que o dominio reconhece.
/// </summary>
public enum ProcessoPesagemProduto
{
    /// <summary>Uma pesagem e um envio ao SAP por caixa. E o unico implementado hoje.</summary>
    CaixaACaixa,

    /// <summary>
    /// Junta todas as caixas e faz UM envio ao SAP.
    /// <para>
    /// <b>Cadastravel, nao executavel.</b> Em 18/11/2026 este caminho de envio ainda nao existe no
    /// sistema. O usuario precisa poder marcar a intencao antes de a pesagem ficar pronta, e por
    /// isso o valor e aceito — mas nada no runtime deve supor que marcar aqui habilita o envio.
    /// Quem implementar a pesagem decide o que fazer com os produtos ja marcados.
    /// </para>
    /// </summary>
    EnvioUnico,
}

/// <summary>
/// Decisao do usuario sobre UM produto acabado do centro: qual etiqueta ele usa (ou se nao usa
/// nenhuma) e como e pesado.
/// <para>
/// O catalogo de produtos NAO mora aqui — vem do SAP. Esta entidade guarda so a escolha, mais um
/// retrato do que o SAP dizia quando ela foi tomada.
/// </para>
/// </summary>
public sealed class VinculoProdutoEtiqueta
{
    public const string CentroPadrao = "3007";

    public long CodigoVinculo { get; set; }
    public string Centro { get; set; } = CentroPadrao;
    public string Produto { get; set; } = string.Empty;

    /// <summary>Retrato do SAP no momento do vinculo — nunca fonte de verdade do cadastro.</summary>
    public string? DescricaoProduto { get; set; }
    public string? TipoProduto { get; set; }

    /// <summary>Etiqueta escolhida. Nulo somente quando <see cref="SemEtiqueta"/> e verdadeiro.</summary>
    public long? CodigoEtiqueta { get; set; }

    /// <summary>
    /// O usuario decidiu que este produto NAO leva etiqueta.
    /// <para>
    /// E diferente de "ainda nao decidido", que e a AUSENCIA de vinculo. A distincao existe para a
    /// tela poder cobrar decisao sem confundir quem ja decidiu que nao quer etiqueta.
    /// </para>
    /// </summary>
    public bool SemEtiqueta { get; set; }

    public ProcessoPesagemProduto ProcessoPesagem { get; set; } = ProcessoPesagemProduto.CaixaACaixa;

    public bool Situacao { get; set; } = true;
    public DateTime? CriadoEm { get; set; }
    public long? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public long? AtualizadoPor { get; set; }

    /// <summary>Texto gravado no banco; o CHECK da tabela so aceita estes dois.</summary>
    public static string TextoDoProcesso(ProcessoPesagemProduto processo) => processo switch
    {
        ProcessoPesagemProduto.CaixaACaixa => "CAIXA_A_CAIXA",
        ProcessoPesagemProduto.EnvioUnico => "ENVIO_UNICO",
        _ => throw new ArgumentOutOfRangeException(nameof(processo), processo, "Processo de pesagem desconhecido."),
    };

    /// <summary>
    /// Converte o texto do banco. Valor desconhecido LANCA em vez de cair num padrao: uma linha
    /// gravada com processo que o codigo nao conhece e um defeito, e tratar como "caixa a caixa"
    /// faria a operacao seguir por um caminho que ninguem escolheu.
    /// </summary>
    public static ProcessoPesagemProduto ProcessoDoTexto(string? texto) => (texto ?? string.Empty).Trim() switch
    {
        "CAIXA_A_CAIXA" => ProcessoPesagemProduto.CaixaACaixa,
        "ENVIO_UNICO" => ProcessoPesagemProduto.EnvioUnico,
        var outro => throw new InvalidOperationException(
            $"Processo de pesagem '{outro}' nao e reconhecido pelo sistema."),
    };

    public static string RotuloDoProcesso(ProcessoPesagemProduto processo) => processo switch
    {
        ProcessoPesagemProduto.CaixaACaixa => "Caixa a caixa",
        ProcessoPesagemProduto.EnvioUnico => "Junta as caixas (envio único)",
        _ => string.Empty,
    };
}

/// <summary>
/// Uma linha da tela: o produto como o SAP o devolve AGORA, somado a decisao ja gravada, se houver.
/// <para>
/// Os dois lados vem de fontes diferentes e podem discordar — produto que sumiu do centro, descricao
/// que mudou. Por isso a linha carrega os dois e nao tenta reconcilia-los sozinha.
/// </para>
/// </summary>
public sealed class LinhaVinculoProduto
{
    public required string Produto { get; init; }

    /// <summary>Descricao vinda do SAP nesta consulta; nula quando o produto so existe no vinculo.</summary>
    public string? DescricaoSap { get; init; }
    public string? TipoProdutoSap { get; init; }

    /// <summary>Decisao gravada. Nulo = produto ainda NAO configurado.</summary>
    public VinculoProdutoEtiqueta? Vinculo { get; init; }

    /// <summary>Nome da etiqueta escolhida, para exibicao; vazio quando nao ha etiqueta.</summary>
    public string NomeEtiqueta { get; init; } = string.Empty;

    /// <summary>
    /// O produto veio do SAP nesta consulta. Falso significa que a linha existe so porque ha um
    /// vinculo gravado para um produto que o centro nao lista mais — situacao que a tela mostra em
    /// vez de esconder, porque esconder deixaria um vinculo orfao decidindo pesagem.
    /// </summary>
    public bool PresenteNoSap { get; init; }

    public bool Configurado => Vinculo is not null;

    public string DescricaoParaExibir
        => !string.IsNullOrWhiteSpace(DescricaoSap) ? DescricaoSap!
         : !string.IsNullOrWhiteSpace(Vinculo?.DescricaoProduto) ? Vinculo!.DescricaoProduto!
         : string.Empty;

    public string SituacaoParaExibir
        => Vinculo is null ? "Não configurado"
         : Vinculo.SemEtiqueta ? "Sem etiqueta"
         : NomeEtiqueta;
}
