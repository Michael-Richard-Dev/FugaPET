using FugaPET_Dev.AcessoDados.Repositorio;

namespace FugaPET_Dev.Servicos.Operacao;

/// <summary>GATE 122C: situacao do palete quanto a pendencia de reconciliacao.</summary>
public enum PendenciaPalete
{
    /// <summary>Estado NAO comprovado (sem linha, status vazio ou status desconhecido). Fail-closed.</summary>
    Indeterminado = 0,

    /// <summary>RASCUNHO: nada enviado. Envio permitido.</summary>
    Limpo = 1,

    /// <summary>ENVIADO_SAP: claim em aberto / resultado nao fechado. Reconciliacao OBRIGATORIA.</summary>
    PendenteReconciliacao = 2,

    /// <summary>ERRO_SAP: erro registrado. Exige liberacao explicita antes de novo envio.</summary>
    PendenteLiberacao = 3,

    /// <summary>CONFIRMADO_SAP: palete ja existe no SAP. Novo envio PROIBIDO.</summary>
    ConfirmadoNoSap = 4,

    /// <summary>CANCELADO: terminal. Novo envio PROIBIDO.</summary>
    Cancelado = 5
}

/// <summary>
/// GATE 122C: veredito COMPROVADO da reconciliacao. Nao existe valor "provavelmente": o unico veredito
/// que autoriza reprocessamento e <see cref="NaoExecutadoNoSap"/>, e ele exige prova de ausencia.
/// </summary>
public enum VeredictoReconciliacaoPalete
{
    /// <summary>Nao foi possivel provar nada. NUNCA libera reprocessamento.</summary>
    Indeterminado = 0,

    /// <summary>Comprovado que o SAP CRIOU a HU pai (UC conhecida). Fecha o palete; nao reprocessa.</summary>
    ConfirmadoNoSap = 1,

    /// <summary>Comprovado que o SAP NAO executou. Unico caso que admite liberar reprocessamento.</summary>
    NaoExecutadoNoSap = 2
}

/// <summary>GATE 122C: resultado de uma operacao de recovery/reconciliacao do palete.</summary>
public sealed record ResultadoRecoveryPalete(
    bool Sucesso,
    PendenciaPalete Pendencia,
    bool RecoveryAdquirido,
    bool ReconciliacaoRegistrada,
    bool ReprocessamentoLiberado,
    string Mensagem)
{
    public static ResultadoRecoveryPalete Bloqueado(PendenciaPalete pendencia, string mensagem)
        => new(false, pendencia, false, false, false, mensagem);
}

/// <summary>
/// GATE 122C (TRACK B): o CAMINHO DE APLICACAO que faltava para reconciliar palete.
/// <para>
/// O 122A provou que as primitivas existiam em tres camadas (funcoes 045 no banco, metodos no store)
/// mas NENHUMA era alcancavel por codigo de producao — so por testes. Consequencia: um POST INT012
/// com resultado indeterminado deixava o palete travado, sem acao possivel pelo app.
/// </para>
/// <para>
/// Este servico NAO cria primitivas novas: usa exclusivamente
/// <see cref="IProdutoAcabadoPipeline045Operacoes"/> (LerEstadoPalete, AdquirirRecoveryPalete,
/// ReassumirClaimPalete, RegistrarReconciliacaoPalete, LiberarReprocessamentoPalete).
/// </para>
/// <para>
/// Nao faz HTTP, nao faz POST, nao chama CPI/SAP e nao reenvia nada. A decisao de reenviar continua
/// sendo do orquestrador, e so depois de liberacao comprovada.
/// </para>
/// </summary>
public sealed class ProdutoAcabadoPaleteRecoveryServico
{
    public const string MotivoEstadoIndeterminado =
        "Estado do palete NAO comprovado (sem registro de estado ou status desconhecido): "
        + "envio bloqueado e reconciliacao exigida. Nenhum POST.";

    public const string MotivoJaConfirmado =
        "Palete ja CONFIRMADO no SAP: novo envio PROIBIDO (nao existe reenvio de palete confirmado).";

    public const string MotivoCancelado =
        "Palete CANCELADO: novo envio PROIBIDO.";

    public const string MotivoNadaAReconciliar =
        "Palete sem pendencia de reconciliacao (RASCUNHO): nada a reconciliar.";

    public const string MotivoRecoveryNaoAdquirido =
        "Recovery do palete NAO adquirido (o banco negou — outro operador ja detem o fencing, ou o "
        + "estado nao admite recovery). Nenhuma reconciliacao registrada, nenhum POST.";

    public const string MotivoReconciliacaoNaoRegistrada =
        "Reconciliacao do palete NAO foi registrada pelo banco: estado permanece pendente. Nenhum POST.";

    public const string MotivoVeredictoIndeterminado =
        "Reconciliacao concluida como INDETERMINADA: bloqueio MANTIDO, reprocessamento NAO liberado. "
        + "Nenhum POST.";

    public const string MotivoConfirmadoReconciliado =
        "Reconciliacao comprovou que o SAP JA criou a HU pai: palete fechado, reprocessamento NAO "
        + "liberado (nao ha o que reenviar).";

    public const string MotivoLiberacaoNegada =
        "Liberacao de reprocessamento NEGADA pelo banco apos reconciliacao: bloqueio mantido. Nenhum POST.";

    /// <summary>Tipo de liberacao registrado quando a ausencia no SAP e comprovada.</summary>
    public const string TipoLiberacaoAusenciaComprovada = "RECONCILIADO_AUSENTE_NO_SAP";

    private readonly IProdutoAcabadoPipeline045Operacoes _ops;

    public ProdutoAcabadoPaleteRecoveryServico(IProdutoAcabadoPipeline045Operacoes ops)
        => _ops = ops ?? throw new ArgumentNullException(nameof(ops));

    /// <summary>
    /// Classifica o status persistido do palete. Ausencia de linha, status vazio ou status desconhecido
    /// => Indeterminado (fail-closed): NUNCA tratado como limpo.
    /// </summary>
    internal static PendenciaPalete ClassificarPendencia(IReadOnlyList<Linha045>? estado)
    {
        if (estado is null || estado.Count == 0)
        {
            return PendenciaPalete.Indeterminado;
        }

        string status = estado[0].ObterTexto("status_hu_palete")?.Trim().ToUpperInvariant() ?? string.Empty;
        return status switch
        {
            "RASCUNHO" => PendenciaPalete.Limpo,
            "ENVIADO_SAP" => PendenciaPalete.PendenteReconciliacao,
            "ERRO_SAP" => PendenciaPalete.PendenteLiberacao,
            "CONFIRMADO_SAP" => PendenciaPalete.ConfirmadoNoSap,
            "CANCELADO" => PendenciaPalete.Cancelado,
            _ => PendenciaPalete.Indeterminado
        };
    }

    /// <summary>Somente RASCUNHO autoriza um POST. Todo o resto bloqueia — inclusive o indeterminado.</summary>
    internal static bool EnvioPermitido(PendenciaPalete pendencia) => pendencia == PendenciaPalete.Limpo;

    /// <summary>Mensagem objetiva do bloqueio, por pendencia.</summary>
    internal static string MensagemBloqueio(PendenciaPalete pendencia) => pendencia switch
    {
        PendenciaPalete.PendenteReconciliacao =>
            "Palete com envio anterior NAO fechado (ENVIADO_SAP): reconciliacao obrigatoria antes de "
            + "qualquer novo POST. Reenvio cego PROIBIDO.",
        PendenciaPalete.PendenteLiberacao =>
            "Palete em ERRO_SAP: exige reconciliacao e liberacao explicita de reprocessamento antes de "
            + "novo POST.",
        PendenciaPalete.ConfirmadoNoSap => MotivoJaConfirmado,
        PendenciaPalete.Cancelado => MotivoCancelado,
        PendenciaPalete.Limpo => string.Empty,
        _ => MotivoEstadoIndeterminado
    };

    /// <summary>
    /// Detecta a pendencia do palete lendo o estado PERSISTIDO. Falha de leitura => Indeterminado
    /// (fail-closed), nunca "limpo".
    /// </summary>
    public async Task<PendenciaPalete> DetectarPendenciaAsync(
        long codigoPalete, CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<Linha045> estado = await _ops.LerEstadoPaleteAsync(codigoPalete, cancellationToken);
            return ClassificarPendencia(estado);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Estado nao comprovavel ⇒ bloqueia. Nunca libera por falha de leitura.
            return PendenciaPalete.Indeterminado;
        }
    }

    /// <summary>
    /// GUARD que o orquestrador consulta ANTES do claim/POST. true somente com estado comprovadamente
    /// RASCUNHO. Qualquer pendencia, indeterminacao ou falha de leitura ⇒ false.
    /// </summary>
    public async Task<(bool Permitido, PendenciaPalete Pendencia, string Motivo)> PodeEnviarAsync(
        long codigoPalete, CancellationToken cancellationToken = default)
    {
        PendenciaPalete pendencia = await DetectarPendenciaAsync(codigoPalete, cancellationToken);
        return EnvioPermitido(pendencia)
            ? (true, pendencia, string.Empty)
            : (false, pendencia, MensagemBloqueio(pendencia));
    }

    /// <summary>
    /// Executa a reconciliacao de um palete pendente, na ordem obrigatoria:
    /// detectar pendencia -> adquirir recovery (fencing, uma vez) -> registrar reconciliacao ->
    /// liberar reprocessamento SOMENTE se a ausencia no SAP foi COMPROVADA.
    /// <para>
    /// O veredito e responsabilidade do chamador (quem obteve a evidencia). Este servico NAO adivinha:
    /// Indeterminado mantem o bloqueio, Confirmado fecha sem liberar, e so NaoExecutadoNoSap libera.
    /// </para>
    /// <para>Nenhum POST, nenhum reenvio, nenhuma chamada SAP/CPI acontece aqui.</para>
    /// </summary>
    public async Task<ResultadoRecoveryPalete> ReconciliarAsync(
        long codigoPalete,
        VeredictoReconciliacaoPalete veredicto,
        string? huPai,
        string evidenciaJson,
        long usuario,
        string terminal,
        CancellationToken cancellationToken = default)
    {
        if (codigoPalete <= 0)
        {
            return ResultadoRecoveryPalete.Bloqueado(
                PendenciaPalete.Indeterminado, "Palete invalido para reconciliacao. Nenhum POST.");
        }

        PendenciaPalete pendencia = await DetectarPendenciaAsync(codigoPalete, cancellationToken);

        // Estados que NAO admitem reconciliacao.
        switch (pendencia)
        {
            case PendenciaPalete.Limpo:
                return ResultadoRecoveryPalete.Bloqueado(pendencia, MotivoNadaAReconciliar);
            case PendenciaPalete.ConfirmadoNoSap:
                return ResultadoRecoveryPalete.Bloqueado(pendencia, MotivoJaConfirmado);
            case PendenciaPalete.Cancelado:
                return ResultadoRecoveryPalete.Bloqueado(pendencia, MotivoCancelado);
        }

        // FENCING: o recovery e adquirido do banco UMA vez. Negado ⇒ para aqui, sem registrar nada.
        ResultadoClaim045 recovery = await _ops.AdquirirRecoveryPaleteAsync(
            codigoPalete, usuario, terminal, cancellationToken);
        if (!recovery.Obtido || recovery.Token is not Guid || recovery.Tentativa is not int tentativa || tentativa <= 0)
        {
            return ResultadoRecoveryPalete.Bloqueado(pendencia, MotivoRecoveryNaoAdquirido);
        }

        string resultadoTexto = veredicto switch
        {
            VeredictoReconciliacaoPalete.ConfirmadoNoSap => "CONFIRMADO_NO_SAP",
            VeredictoReconciliacaoPalete.NaoExecutadoNoSap => "NAO_EXECUTADO_NO_SAP",
            _ => "INDETERMINADO"
        };

        bool registrada = await _ops.RegistrarReconciliacaoPaleteAsync(
            codigoPalete,
            resultadoTexto,
            veredicto == VeredictoReconciliacaoPalete.ConfirmadoNoSap ? huPai : null,
            string.IsNullOrWhiteSpace(evidenciaJson) ? "{}" : evidenciaJson,
            veredicto == VeredictoReconciliacaoPalete.Indeterminado ? MotivoVeredictoIndeterminado : null,
            usuario,
            terminal,
            cancellationToken);

        if (!registrada)
        {
            return new ResultadoRecoveryPalete(
                false, pendencia, true, false, false, MotivoReconciliacaoNaoRegistrada);
        }

        // Veredito indeterminado: reconciliacao REGISTRADA, bloqueio MANTIDO. Jamais libera.
        if (veredicto == VeredictoReconciliacaoPalete.Indeterminado)
        {
            return new ResultadoRecoveryPalete(
                false, pendencia, true, true, false, MotivoVeredictoIndeterminado);
        }

        // Confirmado no SAP: o palete existe la. Fecha sem liberar — nao ha o que reenviar.
        if (veredicto == VeredictoReconciliacaoPalete.ConfirmadoNoSap)
        {
            return new ResultadoRecoveryPalete(
                true, pendencia, true, true, false, MotivoConfirmadoReconciliado);
        }

        // Unico caminho que libera: ausencia no SAP COMPROVADA.
        bool liberado = await _ops.LiberarReprocessamentoPaleteAsync(
            codigoPalete,
            TipoLiberacaoAusenciaComprovada,
            "Reconciliacao comprovou ausencia do palete no SAP; reprocessamento liberado.",
            usuario,
            terminal,
            cancellationToken);

        return liberado
            ? new ResultadoRecoveryPalete(
                true, pendencia, true, true, true,
                "Reconciliacao comprovou ausencia no SAP e o reprocessamento foi liberado.")
            : new ResultadoRecoveryPalete(
                false, pendencia, true, true, false, MotivoLiberacaoNegada);
    }
}
