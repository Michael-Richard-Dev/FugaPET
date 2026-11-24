using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Os fatos de um estouro de tolerância NA PRODUÇÃO, congelados no momento em que o gerente foi
/// chamado (incremento 071).
/// <para>
/// Irmão do <see cref="ContextoEstouroTolerancia"/>, não substituto: lá a identidade do caso é
/// pedido de compra + item; aqui é ordem de produção + operação + componente. São grandezas de
/// origens diferentes, e um tipo só carregaria metade dos campos sempre nulos.
/// </para>
/// <para>
/// Congelados de propósito: a tolerância cadastrada pode mudar depois, e o log tem de continuar
/// explicando a decisão tomada COM os valores de então.
/// </para>
/// </summary>
public sealed record ContextoEstouroProducao
{
    /// <summary>CONSUMO ou SEMI_ACABADO — os dois únicos valores que o banco aceita.</summary>
    public required string Processo { get; init; }

    public required string NumeroOrdem { get; init; }

    /// <summary>Operação da ordem, quando o processo a tem. Nulo no Semi-Acabado, que lança pela ordem inteira.</summary>
    public string? Operacao { get; init; }

    public required string Material { get; init; }

    public decimal? QuantidadePrevista { get; init; }
    public decimal? QuantidadeApurada { get; init; }
    public decimal? LimiteAplicado { get; init; }
    public decimal? PercentualTolerancia { get; init; }

    /// <summary>ACIMA_DO_TETO ou ABAIXO_DO_PISO.</summary>
    public required string TipoEstouro { get; init; }

    public string? LoginOperador { get; init; }
    public long? CodigoUsuarioOperador { get; init; }
    public string? Terminal { get; init; }

    /// <summary>
    /// Traduz o veredito da regra no texto que o banco aceita. A mesma tradução do Recebimento, e de
    /// propósito: só ESTOURO é liberável. Falta de cadastro se resolve cadastrando — decisão do
    /// usuário, para a liberação não virar caminho permanente de não cadastrar.
    /// </summary>
    public static string TipoDoResultado(ResultadoTolerancia resultado)
        => ContextoEstouroTolerancia.TipoDoResultado(resultado);
}

/// <summary>Uma linha do log de liberações da produção — inclusive as RECUSADAS.</summary>
public sealed record LiberacaoToleranciaProcesso
{
    public required ContextoEstouroProducao Contexto { get; init; }

    /// <summary>AUTORIZADA ou RECUSADA.</summary>
    public required string Resultado { get; init; }

    public string? MotivoRecusa { get; init; }

    /// <summary>Nulo quando o login digitado nem existe — e essa linha é a que mais interessa auditar.</summary>
    public long? CodigoUsuarioAutorizador { get; init; }

    /// <summary>
    /// O login DIGITADO, mesmo quando a tentativa falha. A senha NÃO é guardada em lugar nenhum:
    /// quem autorizou é provado pela identidade, e a senha só criaria um alvo.
    /// </summary>
    public required string LoginAutorizador { get; init; }

    public const string Autorizada = LiberacaoToleranciaRecebimento.Autorizada;
    public const string Recusada = LiberacaoToleranciaRecebimento.Recusada;
}
