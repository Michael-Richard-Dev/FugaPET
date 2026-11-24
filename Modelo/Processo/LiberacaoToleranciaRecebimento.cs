using FugaPET_Dev.Servicos.Processo;

namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Os fatos do estouro, congelados no momento em que o gerente foi chamado.
/// <para>
/// Congelados de proposito: a tolerancia cadastrada pode mudar depois, e o log tem de continuar
/// explicando a decisao que foi tomada COM os valores de entao. Um log que recalculasse pelo
/// cadastro de hoje contaria outra historia.
/// </para>
/// </summary>
public sealed record ContextoEstouroTolerancia
{
    public required string NumeroPedido { get; init; }
    public required string NumeroItem { get; init; }
    public required string Material { get; init; }

    public decimal? QuantidadePedida { get; init; }
    public decimal? QuantidadeApurada { get; init; }
    public decimal? LimiteAplicado { get; init; }
    public decimal? PercentualTolerancia { get; init; }

    /// <summary>ACIMA_DO_TETO ou ABAIXO_DO_PISO — os dois unicos valores que o banco aceita.</summary>
    public required string TipoEstouro { get; init; }

    public string? LoginOperador { get; init; }
    public long? CodigoUsuarioOperador { get; init; }
    public string? Terminal { get; init; }

    /// <summary>Traduz o veredito da regra no texto que o banco aceita em <c>tipo_estouro</c>.</summary>
    public static string TipoDoResultado(ResultadoTolerancia resultado) => resultado switch
    {
        ResultadoTolerancia.AcimaDoTeto => "ACIMA_DO_TETO",
        ResultadoTolerancia.AbaixoDoPiso => "ABAIXO_DO_PISO",
        _ => throw new ArgumentOutOfRangeException(
            nameof(resultado), resultado,
            "Só estouro de tolerância é liberável. Falta de cadastro se resolve cadastrando — decisão "
            + "do usuário em 24/11/2026, para a liberação não virar caminho permanente de não cadastrar."),
    };
}

/// <summary>Uma linha do log de liberacoes — inclusive as RECUSADAS.</summary>
public sealed record LiberacaoToleranciaRecebimento
{
    public required ContextoEstouroTolerancia Contexto { get; init; }

    /// <summary>AUTORIZADA ou RECUSADA.</summary>
    public required string Resultado { get; init; }

    public string? MotivoRecusa { get; init; }

    /// <summary>Nulo quando o login digitado nem existe — e essa linha e a que mais interessa auditar.</summary>
    public long? CodigoUsuarioAutorizador { get; init; }

    /// <summary>
    /// O login DIGITADO, mesmo quando a tentativa falha. A senha NAO e guardada em lugar nenhum:
    /// quem autorizou e provado pela identidade, e a senha so criaria um alvo.
    /// </summary>
    public required string LoginAutorizador { get; init; }

    public const string Autorizada = "AUTORIZADA";
    public const string Recusada = "RECUSADA";
}

/// <summary>O que a tela recebe depois de o gerente digitar credencial.</summary>
public sealed record ResultadoAutorizacaoTolerancia(bool Autorizada, string Mensagem)
{
    public static ResultadoAutorizacaoTolerancia Ok(string login)
        => new(true, $"Liberação autorizada por {login}.");

    public static ResultadoAutorizacaoTolerancia Nao(string mensagem) => new(false, mensagem);
}
