using FugaPET_Dev.Modelo.Processo;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Resolução da configuração de destino. Quando duas configurações têm a MESMA especificidade para a
/// operação, o resultado é AMBÍGUO e o início deve ser bloqueado — nunca escolher com LIMIT 1.
/// </summary>
public sealed record ResultadoConfiguracaoOperacao(
    ConfiguracaoOperacaoProcesso? Configuracao,
    bool Ambigua,
    int Empatadas);

/// <summary>
/// Persistência do Controle de Apontamentos. Enquanto o pacote Gaia não for aplicado,
/// <see cref="EstruturaDisponivelAsync"/> devolve false e a tela opera em modo consulta
/// (interpreta código, consulta OP, exibe operações) com o início BLOQUEADO.
/// </summary>
public interface IControleApontamentosRepositorio
{
    /// <summary>True quando as tabelas do pacote existem no schema atual (search_path).</summary>
    Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Configuração ATIVA da tela de destino, resolvida por DADOS (centro + tipo de ordem + sequência +
    /// operação + suboperação + centro de trabalho), nunca por descrição. Empate de especificidade = ambígua.
    /// </summary>
    Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoOperacaoAsync(
        string centro,
        string tipoOrdem,
        string sequencia,
        string operacao,
        string suboperacao,
        string centroTrabalho,
        CancellationToken cancellationToken = default);

    /// <summary>Todas as configurações ativas que se aplicam a uma OP (para montar o grid completo).</summary>
    Task<IReadOnlyList<ConfiguracaoOperacaoProcesso>> ListarConfiguracoesAtivasAsync(
        string centro,
        string tipoOrdem,
        CancellationToken cancellationToken = default);

    /// <summary>Apontamento ATIVO (EM_ANDAMENTO/AGUARDANDO_FINALIZACAO) da operação, ou null.</summary>
    Task<OperacaoProducaoApontamento?> ObterApontamentoAtivoAsync(
        string numeroOrdem,
        string sequencia,
        string operacao,
        string suboperacao,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apontamentos ATIVOS por OP + operação, SEM depender de sequência/suboperação (o término não
    /// consulta o SAP). Mais de um = ambiguidade → bloquear.
    /// </summary>
    Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosAtivosPorOperacaoAsync(
        string numeroOrdem,
        string operacao,
        CancellationToken cancellationToken = default);

    /// <summary>Todos os apontamentos da OP (grid completo + validação de sequência).</summary>
    Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosDaOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default);

    /// <summary>True quando o código lido já foi consumido (idempotência da leitura).</summary>
    Task<bool> CodigoJaUtilizadoAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim ATÔMICO do início. Insere o apontamento EM_ANDAMENTO e devolve o registro com o
    /// <c>iniciado_em</c> do BANCO (RETURNING). Devolve null quando outro usuário/estação venceu a corrida
    /// (inclusive por unique_violation 23505) — conflito é resultado funcional, nunca exceção de suporte.
    /// O evento de auditoria é gravado na MESMA transação.
    /// </summary>
    Task<OperacaoProducaoApontamento?> TentarIniciarApontamentoAsync(
        OperacaoProducaoApontamento apontamento,
        CodigoBarrasOperacao codigo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EM_ANDAMENTO → AGUARDANDO_FINALIZACAO, gravando o vínculo com o registro operacional.
    /// True SOMENTE quando exatamente 1 linha foi atualizada. Evento gravado na mesma transação.
    /// </summary>
    Task<bool> TentarMarcarAguardandoFinalizacaoAsync(
        long codigoApontamento,
        ResultadoExecucaoProcesso resultado,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Conclui o apontamento ATIVO e devolve o <c>terminado_em</c> do BANCO. Null quando nenhuma linha
    /// foi atualizada (já concluído por outra estação) ou em conflito 23505 de idempotência do término.
    /// Evento gravado na mesma transação.
    /// </summary>
    Task<DateTimeOffset?> TentarConcluirApontamentoAsync(
        long codigoApontamento,
        string usuarioTermino,
        string estacaoTermino,
        CodigoBarrasOperacao codigoTermino,
        string idempotencyKeyTermino,
        CancellationToken cancellationToken = default);

    /// <summary>EM_ANDAMENTO → CANCELADA (operador recusou a confirmação). True se 1 linha alterada.</summary>
    Task<bool> TentarCancelarApontamentoAsync(
        long codigoApontamento,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigo,
        string motivo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra um evento de leitura AVULSO (tentativas que não alteram apontamento: código inválido,
    /// OP inexistente, sequência bloqueada, retomada, etc.). Código original sempre preservado.
    /// </summary>
    Task RegistrarEventoAsync(
        long? codigoApontamento,
        CodigoBarrasOperacao codigo,
        string usuario,
        string estacao,
        string statusAnterior,
        string statusNovo,
        string resultado,
        string mensagem,
        string correlationId,
        CancellationToken cancellationToken = default);
}
