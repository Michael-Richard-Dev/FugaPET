namespace FugaPET_Dev.Modelo.Entrada;

/// <summary>
/// Conflito de idempotência da persistência por lotes da Entrada, detectado na CAMADA DE DADOS. Existe
/// para que <c>EntradaProdutoRepositorio</c> não precise depender de <c>Servicos.Operacao</c>: o Service
/// mapeia esta exceção para a exceção operacional própria e faz a auditoria sanitizada.
///
/// SEGURANÇA: <see cref="DivergenciaTecnica"/> carrega SOMENTE diagnóstico controlado (correlation_id,
/// número do item, tipo de divergência). NUNCA armazenar connection string, host, usuário, senha, SQL,
/// payload completo da balança nem stack trace manual — apenas os campos tipados abaixo.
/// </summary>
public sealed class ConflitoPersistenciaEntradaLotesException : Exception
{
    public ConflitoPersistenciaEntradaLotesException(
        string mensagemUsuario,
        string divergenciaTecnica,
        Guid? correlationId = null,
        string? numeroItemSap = null)
        : base(mensagemUsuario)
    {
        MensagemUsuario = mensagemUsuario;
        DivergenciaTecnica = divergenciaTecnica;
        CorrelationId = correlationId;
        NumeroItemSap = numeroItemSap;
    }

    /// <summary>Mensagem SEGURA para exibir ao operador na interface (sem detalhe interno da árvore).</summary>
    public string MensagemUsuario { get; }

    /// <summary>Diagnóstico técnico controlado e sanitizado, destinado à auditoria — nunca à interface.</summary>
    public string DivergenciaTecnica { get; }

    /// <summary>Correlation_id envolvida, quando o diagnóstico permitir identificá-la.</summary>
    public Guid? CorrelationId { get; }

    /// <summary>Número do item SAP envolvido, quando o diagnóstico permitir identificá-lo.</summary>
    public string? NumeroItemSap { get; }
}
