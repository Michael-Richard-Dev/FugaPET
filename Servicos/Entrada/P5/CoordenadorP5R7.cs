namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed class CoordenadorP5R7(IBackendP5 backend, IFonteProvaP5 fonte, HomologacaoP5 homologacao,
    Func<ClaimP5, CancellationToken, Task<RetornoHttpP5>> dispatch, TimeProvider? relogio = null)
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;

    internal async Task<ResultadoFluxoP5> ExecutarAsync(EscopoP5 escopo, CancellationToken cancellationToken = default)
    {
        if (!homologacao.Completa || !escopo.Valido)
            return new(EstadoP5.Bloqueado, false, "P5_DEPENDENCIAS_NAO_HOMOLOGADAS");
        if (!backend.ResultadoLegadoAtomicoDisponivel)
            return new(EstadoP5.Bloqueado, false, "P5_ATOMICIDADE_LEGADA_INDISPONIVEL");
        if (!backend.ReservaLegadaCoordenadaDisponivel)
            return new(EstadoP5.Bloqueado, false, "P5_RESERVA_LEGADA_NAO_COORDENADA");

        ClaimP5? claim = null;
        bool fronteiraSolicitada = false;
        ProvaSapP5? prova;
        try
        {
            prova = await fonte.ObterValidadaAsync(escopo, cancellationToken);
            if (prova is null || !prova.Valida(escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge)
                || !prova.Negativa || !await backend.D2SemImpedimentoAsync(escopo, cancellationToken))
                return new(EstadoP5.Bloqueado, false, "P5_EVIDENCIA_INSUFICIENTE");

            if (!prova.Valida(escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge))
                return new(EstadoP5.Bloqueado, false, "P5_PROVA_EXPIROU_ANTES_RESERVA");

            claim = await backend.ReservarAsync(escopo, prova, cancellationToken);
            if (!claim.Valido || claim.Escopo != escopo || !claim.Consumir())
                return new(EstadoP5.Bloqueado, false, "P5_CLAIM_INVALIDO");
            if (!prova.Valida(escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge))
                return await CompensarPreHttpAsync(claim);
            cancellationToken.ThrowIfCancellationRequested();
            fronteiraSolicitada = true;
            await backend.MarcarEnvioCommitAsync(claim, cancellationToken);
            if (await backend.ConfirmarEnvioDuravelAsync(claim, cancellationToken) != EstadoP5.EnvioIniciado)
                return new(EstadoP5.Indeterminado, false, "P5_ENVIO_DURAVEL_NAO_COMPROVADO_SEM_POST");
        }
        catch (CommitP5IndeterminadoException ex)
        {
            if (!ex.Claim.Valido || ex.Claim.Escopo != escopo)
                return new(EstadoP5.Indeterminado, false, "P5_COMMIT_IDENTIDADE_NAO_COMPROVADA");
            ResultadoFluxoP5 observado = await ReconsultarSemDispatchAsync(ex.Claim);
            cancellationToken.ThrowIfCancellationRequested();
            return observado;
        }
        catch (OperationCanceledException)
        {
            if (claim is not null && !fronteiraSolicitada)
                await CompensarPreHttpAsync(claim);
            else if (claim is not null)
                await ReconsultarSemDispatchAsync(claim);
            throw;
        }
        catch (Exception)
        {
            if (claim is not null && !fronteiraSolicitada)
                return await CompensarPreHttpAsync(claim);
            if (claim is not null)
                return await ReconsultarSemDispatchAsync(claim);
            return new(EstadoP5.Indeterminado, false, "P5_RESERVA_OU_COMMIT_NAO_COMPROVADO");
        }

        if (!prova.Valida(escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge))
            return new(EstadoP5.Indeterminado, false, "P5_PROVA_EXPIROU_APOS_COMMIT_SEM_POST");

        RetornoHttpP5 retorno;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            retorno = await dispatch(claim, cancellationToken);
        }
        catch (Exception)
        {
            retorno = new(null, null, false);
        }

        try
        {
            await backend.RegistrarResultadoCommitAsync(claim, retorno, CancellationToken.None);
            return new(retorno.Confirmado ? EstadoP5.Confirmado : EstadoP5.Indeterminado, true,
                retorno.Confirmado ? "P5_CONFIRMADO" : "P5_RECONCILIACAO_OBRIGATORIA");
        }
        catch (Exception)
        {
            EstadoP5 esperado = retorno.Confirmado ? EstadoP5.Confirmado : EstadoP5.Indeterminado;
            try
            {
                EstadoP5 observado = await backend.ConfirmarResultadoPersistidoAsync(claim, retorno, CancellationToken.None);
                if (observado == esperado)
                    return new(observado, true, "P5_RESULTADO_RECONSULTADO_SEM_RETRY");
            }
            catch (Exception) { }
            return new(EstadoP5.Indeterminado, false, "P5_RESULTADO_LOCAL_NAO_COMPROVADO_SEM_RETRY");
        }
    }

    private async Task<ResultadoFluxoP5> CompensarPreHttpAsync(ClaimP5 claim)
    {
        try
        {
            bool comprovada = await backend.CancelarReservaPreHttpCommitAsync(claim, CancellationToken.None);
            return comprovada
                ? new(EstadoP5.CanceladoSemEnvio, true, "P5_CANCELAMENTO_PRE_HTTP_COMPROVADO")
                : new(EstadoP5.Reservado, false, "P5_COMPENSACAO_PRE_HTTP_NAO_COMPROVADA");
        }
        catch (Exception)
        {
            return new(EstadoP5.Reservado, false, "P5_COMPENSACAO_PRE_HTTP_NAO_COMPROVADA");
        }
    }

    private async Task<ResultadoFluxoP5> ReconsultarSemDispatchAsync(ClaimP5 claim)
    {
        try
        {
            EstadoP5 observado = await backend.ConsultarEstadoAsync(
                claim.Escopo, claim.Tentativa, claim.Correlacao, CancellationToken.None);
            return new(observado == EstadoP5.Bloqueado ? EstadoP5.Indeterminado : observado,
                observado != EstadoP5.Bloqueado, "P5_COMMIT_RECONSULTADO_SEM_POST_SEM_RETRY");
        }
        catch (Exception)
        {
            return new(EstadoP5.Indeterminado, false, "P5_COMMIT_INDETERMINADO_SEM_POST");
        }
    }
}

internal sealed class RecoveryP5R7(IBackendP5 backend, IFonteProvaP5 fonte, HomologacaoP5 homologacao,
    TimeProvider? relogio = null)
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;

    internal async Task<ResultadoFluxoP5> ReconciliarAsync(EscopoP5 escopo, long tentativa, Guid correlacao,
        CancellationToken cancellationToken = default)
    {
        if (!homologacao.Completa || !escopo.Valido || tentativa <= 0 || correlacao == Guid.Empty)
            return new(EstadoP5.Bloqueado, false, "P5_RECOVERY_NAO_HOMOLOGADO");
        if (!backend.ResultadoLegadoAtomicoDisponivel)
            return new(EstadoP5.Bloqueado, false, "P5_RECOVERY_ATOMICIDADE_LEGADA_INDISPONIVEL");
        if (!backend.RecoveryR6BAprovado)
            return new(EstadoP5.Bloqueado, false, "P5_RECOVERY_R6B_NAO_APROVADO");
        EstadoP5? fechamentoEsperado = null;
        try
        {
            RecoveryP5 recovery = await backend.AssumirRecoveryCommitAsync(escopo, tentativa, correlacao, cancellationToken);
            if (!recovery.Valido || recovery.Escopo != escopo || recovery.Tentativa != tentativa || recovery.Correlacao != correlacao)
                return new(EstadoP5.Bloqueado, false, "P5_RECOVERY_ESCOPO_INVALIDO");
            if (recovery.Estado == EstadoP5.Reservado && recovery.EnvioIniciadoEm is null)
            {
                fechamentoEsperado = EstadoP5.CanceladoSemEnvio;
                await backend.CancelarRecoveryCommitAsync(recovery, cancellationToken);
                EstadoP5 cancelado = await backend.ConsultarEstadoAsync(escopo, tentativa, correlacao, cancellationToken);
                return new(cancelado, cancelado == EstadoP5.CanceladoSemEnvio, "P5_RECOVERY_RECONSULTADO");
            }
            if (recovery.Estado is not (EstadoP5.EnvioIniciado or EstadoP5.Indeterminado) || recovery.EnvioIniciadoEm is null)
                return new(EstadoP5.Bloqueado, false, "P5_RECOVERY_ESTADO_INVALIDO");
            ProvaSapP5? prova = await fonte.ObterValidadaAsync(escopo, cancellationToken);
            if (prova is null || !prova.Valida(escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge, recovery.EnvioIniciadoEm)
                || (!prova.Negativa && !(prova.Movimentos > 0 && prova.Documento?.Valido == true)))
                return new(EstadoP5.Indeterminado, false, "P5_RECOVERY_PROVA_INSUFICIENTE");
            fechamentoEsperado = prova.Negativa ? EstadoP5.NaoContabilizado : EstadoP5.Confirmado;
            await backend.FinalizarRecoveryCommitAsync(recovery, prova, cancellationToken);
            EstadoP5 estado = await backend.ConsultarEstadoAsync(escopo, tentativa, correlacao, cancellationToken);
            EstadoP5 esperado = prova.Negativa ? EstadoP5.NaoContabilizado : EstadoP5.Confirmado;
            return new(estado, estado == esperado, "P5_RECOVERY_RECONSULTADO_SEM_POST");
        }
        catch (OperationCanceledException) when (fechamentoEsperado is null) { throw; }
        catch (Exception)
        {
            if (fechamentoEsperado.HasValue)
            {
                try
                {
                    EstadoP5 observado = await backend.ConsultarEstadoAsync(escopo, tentativa, correlacao, CancellationToken.None);
                    if (observado == fechamentoEsperado.Value)
                        return new(observado, true, "P5_RECOVERY_COMMIT_RECONCILIADO_POR_LEITURA");
                }
                catch (Exception) { }
            }
            return new(EstadoP5.Indeterminado, false, "P5_RECOVERY_NAO_COMPROVADO");
        }
    }
}
