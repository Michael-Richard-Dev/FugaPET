namespace FugaPET_Dev.Modelo.Processo;

/// <summary>
/// Estados PERSISTIDOS do apontamento e as transições REAIS implementadas.
///
/// Transições implementadas (as únicas que existem hoje):
///   (sem apontamento) --inicio confirmado--> EM_ANDAMENTO
///   EM_ANDAMENTO      --retomada----------> EM_ANDAMENTO            (só evento de auditoria; não altera iniciado_em)
///   EM_ANDAMENTO      --atividade concluída-> AGUARDANDO_FINALIZACAO
///   AGUARDANDO_FINALIZACAO --término------> CONCLUIDA
///   EM_ANDAMENTO      --confirmação recusada/cancelamento--> CANCELADA
///
/// PENDENTE / BLOQUEADA / LIBERADA / ERRO NÃO são estados persistidos por transição: são estados
/// CALCULADOS na tela a partir da OP/configuração/apontamento (uma operação sem apontamento aparece
/// como PENDENTE/LIBERADA/BLOQUEADA conforme a sequência). Ficam no CHECK do banco para uso futuro,
/// mas nenhum código os grava hoje — ver <see cref="StatusCalculadoOperacao"/>.
/// </summary>
public static class StatusApontamentoOperacao
{
    public const string EmAndamento = "EM_ANDAMENTO";
    public const string AguardandoFinalizacao = "AGUARDANDO_FINALIZACAO";
    public const string Concluida = "CONCLUIDA";
    public const string Cancelada = "CANCELADA";

    /// <summary>Estados em que a operação está "ativa" (ocupa a operação e impede novo início).</summary>
    public static readonly string[] Ativos = [EmAndamento, AguardandoFinalizacao];
}

/// <summary>
/// Estados CALCULADOS (só de exibição) para operações da OP que ainda não possuem apontamento.
/// Nunca são gravados no banco.
/// </summary>
public static class StatusCalculadoOperacao
{
    public const string Pendente = "PENDENTE";
    public const string Bloqueada = "BLOQUEADA";
    public const string Liberada = "LIBERADA";
}

/// <summary>
/// Apontamento de uma operação da OP: nasce no evento de início (01) e é CONCLUÍDO pela leitura do
/// código de término (02) — o término nunca cria um novo apontamento.
/// </summary>
public sealed class OperacaoProducaoApontamento
{
    public long CodigoApontamento { get; set; }
    public string NumeroOrdem { get; init; } = string.Empty;
    public string ItemOrdem { get; init; } = string.Empty;
    public string Produto { get; init; } = string.Empty;
    public string Sequencia { get; init; } = string.Empty;
    public string Operacao { get; init; } = string.Empty;
    public string Suboperacao { get; init; } = string.Empty;
    public string DescricaoOperacao { get; init; } = string.Empty;
    public string CentroTrabalho { get; init; } = string.Empty;
    public string TipoProcesso { get; init; } = string.Empty;
    public string TelaDestino { get; init; } = string.Empty;

    public string Status { get; set; } = StatusApontamentoOperacao.EmAndamento;

    public string UsuarioInicio { get; init; } = string.Empty;
    public string EstacaoInicio { get; init; } = string.Empty;

    /// <summary>Horário do banco (RETURNING iniciado_em) — nunca DateTime.Now do cliente.</summary>
    public DateTimeOffset? IniciadoEm { get; set; }

    /// <summary>Código de início EXATAMENTE como lido (nunca regenerado).</summary>
    public string CodigoBarrasInicio { get; init; } = string.Empty;

    public string UsuarioTermino { get; set; } = string.Empty;
    public string EstacaoTermino { get; set; } = string.Empty;

    /// <summary>Horário do banco (RETURNING terminado_em) — nunca DateTime.Now do cliente.</summary>
    public DateTimeOffset? TerminadoEm { get; set; }

    /// <summary>Código de término EXATAMENTE como lido (nunca regenerado).</summary>
    public string CodigoBarrasTermino { get; set; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Chave de idempotência da leitura que originou o apontamento (código + formato).</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    // ---- Vínculo com o resultado da tela operacional (persistido; reconstruível após reinício) ----

    /// <summary>Resultado devolvido pela tela operacional (nome do enum) ou vazio.</summary>
    public string ResultadoOperacional { get; set; } = string.Empty;

    /// <summary>Código do registro criado pelo processo (Consumo: codigo_lancamento). Null quando não houve.</summary>
    public long? CodigoRegistroProcesso { get; set; }

    public DateTimeOffset? ConcluidoOperacionalEm { get; set; }
    public string MensagemResultadoOperacional { get; set; } = string.Empty;

    /// <summary>
    /// Duração entre início e término. Calculada a partir dos horários do BANCO, então permanece
    /// correta depois de recarregar o apontamento.
    /// </summary>
    public TimeSpan? Duracao => IniciadoEm is { } inicio && TerminadoEm is { } fim ? fim - inicio : null;
}
