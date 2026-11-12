using System.Collections.Concurrent;

namespace FugaPET_Dev.Servicos.Entrada.P5;

internal sealed class PercursoRohR8F(IExecutorRohR8F executor, HomologacaoP5 homologacao, TimeProvider? relogio = null)
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private readonly ConcurrentDictionary<ChaveP5, byte> _entradas = new();
    private readonly ConcurrentDictionary<ChaveP5, byte> _naoCompensaveis = new();

    internal async Task<ResultadoFluxoP5> ExecutarAsync(PedidoReservaRohR8F pedido,
        Func<TitularRohR8F, CancellationToken, Task<RetornoHttpP5>> dispatch, CancellationToken ct = default)
    {
        if (!Disponivel || !PedidoValido(pedido) || !_entradas.TryAdd(pedido.Identidade.Escopo.Chave, 0))
            return new(EstadoP5.Bloqueado, false, "R8F_ENTRADA_NAO_AUTORIZADA");
        ReciboReservaRohR8F reserva;
        try
        {
            reserva = await executor.ExecutarAsync(async (sessao, token) =>
            {
                ReciboReservaRohR8F recibo = await sessao.ReservarIntegralAsync(pedido, token);
                Exigir(ReservaValida(recibo, pedido));
                return recibo;
            }, ct);
        }
        catch (Exception)
        {
            _naoCompensaveis.TryAdd(pedido.Identidade.Escopo.Chave, 0);
            return Incerto();
        }
        if (ct.IsCancellationRequested || !ProvaValida(pedido))
            return await CompensarAsync(reserva, CancellationToken.None);

        TitularRohR8F titular = reserva.Titular;
        _naoCompensaveis.TryAdd(titular.Identidade.Escopo.Chave, 0);
        try
        {
            ReciboEstadoRohR8F envio = await executor.ExecutarAsync(async (sessao, token) =>
            {
                ReciboEstadoRohR8F recibo = await sessao.MarcarEnvioAsync(titular, token);
                ValidarEstado(recibo, titular, exigirSelo: false);
                return recibo;
            }, ct);
            titular = envio.Titular;
            ReciboEstadoRohR8F selo = await executor.ExecutarAsync(async (sessao, token) =>
            {
                ReciboEstadoRohR8F recibo = await sessao.SelarFronteiraAsync(titular, token);
                ValidarEstado(recibo, titular, exigirSelo: true);
                return recibo;
            }, ct);
            titular = selo.Titular;
            ReciboEstadoRohR8F duravel = await executor.ExecutarAsync(async (sessao, token) =>
            {
                ReciboEstadoRohR8F recibo = await sessao.ConsultarTentativaAsync(titular, token);
                ValidarEstado(recibo, titular, exigirSelo: true);
                return recibo;
            }, ct);
            titular = duravel.Titular;
        }
        catch (Exception) { return Incerto(); }
        if (ct.IsCancellationRequested || !ProvaValida(pedido) || !titular.Claim.Consumir()) return Incerto();

        RetornoHttpP5 retorno;
        try { retorno = await dispatch(titular, ct); }
        catch (Exception) { retorno = new(null, null, false); }
        try
        {
            EscritorResultadoRohR8F escritor = new();
            ReciboResultadoRohR8F resultado = await executor.ExecutarAsync((sessao, token) =>
                escritor.EscreverAsync(sessao, new(titular, retorno), token), CancellationToken.None);
            return new(resultado.Estado, true, resultado.Estado == EstadoP5.Confirmado
                ? "R8F_CONFIRMADO_LOCALMENTE" : "R8F_RECONCILIACAO_SEM_REENVIO");
        }
        catch (Exception) { return Incerto(); }
    }

    internal async Task<ResultadoFluxoP5> CompensarAsync(ReciboReservaRohR8F reserva, CancellationToken ct = default)
    {
        if (!Disponivel || reserva?.Titular?.Valido != true || reserva.Estado != EstadoP5.Reservado
            || reserva.StatusAnterior is not ("FINALIZADO_LOCAL" or "ERRO_SAP")
            || _naoCompensaveis.ContainsKey(reserva.Titular.Identidade.Escopo.Chave)) return Incerto();
        try
        {
            await executor.ExecutarAsync(async (sessao, token) =>
            {
                TitularRohR8F titular = reserva.Titular;
                ReciboEstadoRohR8F estado = await sessao.ConsultarTentativaAsync(titular, token);
                ValidarTitular(estado.Titular, titular, estado.VersaoGuardaAnterior);
                Exigir(estado.Estado == EstadoP5.Reservado && estado.EnvioIniciadoEm is null
                    && estado.FronteiraConfirmadaEm is null && !estado.HttpTentado && !estado.CommitIncerto);
                PedidoCompensacaoRohR8F pedido = new(estado.Titular, reserva.StatusAnterior);
                ReciboCompensacaoRohR8F recibo = await sessao.CompensarPreHttpIntegralAsync(pedido, token);
                ValidarTitular(recibo.Titular, pedido.Titular, recibo.VersaoGuardaAnterior);
                Exigir(recibo.Estado == EstadoP5.CanceladoSemEnvio && recibo.StatusLancamento == pedido.StatusAnterior
                    && recibo.ItensAssociados == 1 && recibo.ItensCobertos == 1);
                return recibo;
            }, ct);
            return new(EstadoP5.CanceladoSemEnvio, true, "R8F_COMPENSACAO_COM_COMMIT_CONFIRMADO");
        }
        catch (Exception)
        {
            _naoCompensaveis.TryAdd(reserva.Titular.Identidade.Escopo.Chave, 0);
            return Incerto();
        }
    }

    private bool Disponivel => executor.Disponivel && executor.Papel == PapelP5R8.Provider && homologacao.Completa;
    private bool ProvaValida(PedidoReservaRohR8F pedido) => pedido.Prova is not null && pedido.Prova.Negativa
        && pedido.Prova.Valida(pedido.Identidade.Escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge);
    private bool PedidoValido(PedidoReservaRohR8F pedido) => pedido?.Identidade?.Valida == true && pedido.VersaoGuardaEsperada > 0
        && ClassificacaoValida(pedido.Classificacao, pedido.Identidade) && ProvaValida(pedido);
    private bool ClassificacaoValida(ReciboResolucaoSqlR8D classificacao, IdentidadeItemProdutoSapR8B identidade)
    {
        if (classificacao?.Dados?.Identidade is null) return false;
        DadosEvidenciaSqlR8D dados = classificacao.Dados;
        return dados.Identidade.Corresponde(identidade) && dados.Product == identidade.Escopo.Material
            && dados.ProductType == "ROH" && dados.EvidenciaId > 0 && dados.VersaoPolitica > 0
            && dados.OrigemServico == "API_PRODUCT_SRV/A_Product"
            && !string.IsNullOrWhiteSpace(dados.SapSistema) && !string.IsNullOrWhiteSpace(dados.SapMandante)
            && !string.IsNullOrWhiteSpace(dados.ReferenciaColetaBackend) && dados.CorrelacaoColetaBackend != Guid.Empty
            && dados.RegistradoNoPostgresqlEm != default && dados.ValidadeAte > _relogio.GetUtcNow()
            && classificacao.EstadoVinculo == "VALIDADA" && classificacao.VinculoId > 0
            && classificacao.VersaoVinculo > 0 && classificacao.VinculadoEm != default;
    }
    private bool ReservaValida(ReciboReservaRohR8F recibo, PedidoReservaRohR8F pedido)
        => recibo?.Titular?.Valido == true && recibo.Titular.Identidade == pedido.Identidade
        && recibo.VersaoGuardaAnterior == pedido.VersaoGuardaEsperada
        && recibo.Titular.Claim.VersaoGuarda >= pedido.VersaoGuardaEsperada
        && recibo.Titular.Evidencia == pedido.Classificacao.Dados.EvidenciaId
        && recibo.Titular.Vinculo == pedido.Classificacao.VinculoId
        && recibo.Titular.VersaoVinculo == pedido.Classificacao.VersaoVinculo
        && recibo.Classificacao == pedido.Classificacao && ClassificacaoValida(recibo.Classificacao, pedido.Identidade)
        && recibo.Estado == EstadoP5.Reservado && recibo.ItensAssociados == 1 && recibo.ItensCobertos == 1
        && recibo.StatusAnterior is "FINALIZADO_LOCAL" or "ERRO_SAP";

    private static void ValidarEstado(ReciboEstadoRohR8F recibo, TitularRohR8F esperado, bool exigirSelo)
    {
        ValidarTitular(recibo.Titular, esperado, recibo.VersaoGuardaAnterior);
        Exigir(recibo.Estado == EstadoP5.EnvioIniciado && recibo.EnvioIniciadoEm.HasValue
            && !recibo.CommitIncerto && !recibo.HttpTentado
            && (!exigirSelo || (recibo.FronteiraConfirmadaEm.HasValue
                && recibo.FronteiraConfirmadaEm >= recibo.EnvioIniciadoEm)));
    }
    internal static void ValidarTitular(TitularRohR8F recebido, TitularRohR8F esperado, long versaoAnterior)
        => Exigir(recebido.Corresponde(esperado) && versaoAnterior == esperado.Claim.VersaoGuarda
            && recebido.Claim.VersaoGuarda >= versaoAnterior);
    internal static void Exigir(bool valido)
    {
        if (!valido) throw new InvalidOperationException("R8F_IDENTIDADE_OU_POS_CONDICAO_NAO_COMPROVADA");
    }
    private static ResultadoFluxoP5 Incerto() => new(EstadoP5.Indeterminado, false, "R8F_RECONCILIACAO_OBRIGATORIA_SEM_POST_ADICIONAL");
}

internal sealed class EscritorResultadoRohR8F
{
    internal async Task<ReciboResultadoRohR8F> EscreverAsync(ISessaoRohR8F sessao,
        PedidoResultadoRohR8F pedido, CancellationToken ct)
    {
        PercursoRohR8F.Exigir(pedido.Titular.Valido);
        ReciboResultadoRohR8F recibo = await sessao.RegistrarResultadoIntegralAsync(pedido, ct);
        PercursoRohR8F.ValidarTitular(recibo.Titular, pedido.Titular, recibo.VersaoGuardaAnterior);
        PercursoRohR8F.Exigir(recibo.ItensAssociados == 1 && recibo.ItensCobertos == 1);
        if (pedido.Retorno.Confirmado)
            PercursoRohR8F.Exigir(recibo.Estado == EstadoP5.Confirmado && recibo.StatusLancamento == "CONFIRMADO_SAP"
                && recibo.StatusItem == "CONFIRMADO_SAP" && recibo.Documento == pedido.Retorno.Documento);
        else
            PercursoRohR8F.Exigir(recibo.Estado == EstadoP5.Indeterminado && recibo.Documento is null
                && recibo.StatusLancamento == "ENVIADO_SAP"
                && recibo.StatusItem is "FINALIZADO_LOCAL" or "ENVIADO_SAP" or "ERRO_SAP");
        return recibo;
    }
}

internal sealed class RecoveryRohR8F(IExecutorRohR8F executor, HomologacaoP5 homologacao, TimeProvider? relogio = null)
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    internal async Task<ResultadoFluxoP5> ReconciliarAsync(PedidoRecoveryRohR8F pedido, CancellationToken ct = default)
    {
        RecoveryP5 recovery = pedido.Recovery;
        ProvaSapP5 prova = pedido.Prova;
        if (!executor.Disponivel || executor.Papel != PapelP5R8.Recovery || !homologacao.Completa
            || !pedido.Identidade.Valida || !recovery.Valido || recovery.Escopo != pedido.Identidade.Escopo
            || recovery.Estado is not (EstadoP5.EnvioIniciado or EstadoP5.Indeterminado)
            || !recovery.EnvioIniciadoEm.HasValue
            || !prova.Valida(recovery.Escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge, recovery.EnvioIniciadoEm)
            || (!prova.Negativa && (prova.Documento?.Valido != true || prova.Movimentos <= 0)))
            return new(EstadoP5.Bloqueado, false, "R8F_RECOVERY_NAO_AUTORIZADO");
        try
        {
            ReciboRecoveryRohR8F recibo = await executor.ExecutarAsync(async (sessao, token) =>
            {
                ReciboRecoveryRohR8F resposta = await sessao.ReconciliarSemReenvioAsync(pedido, token);
                RecoveryP5 titular = resposta.Recovery;
                PercursoRohR8F.Exigir(titular.Valido && titular.Escopo == recovery.Escopo
                    && titular.Tentativa == recovery.Tentativa && titular.Token == recovery.Token
                    && titular.Correlacao == recovery.Correlacao && titular.Geracao == recovery.Geracao
                    && titular.EnvioIniciadoEm == recovery.EnvioIniciadoEm && titular.Estado == recovery.Estado
                    && resposta.Identidade == pedido.Identidade && resposta.ItensAssociados == 1 && resposta.ItensCobertos == 1
                    && !resposta.PermiteNovoPost && resposta.Documento == prova.Documento
                    && resposta.Estado == (prova.Negativa ? EstadoP5.NaoContabilizado : EstadoP5.Confirmado));
                return resposta;
            }, ct);
            return new(recibo.Estado, true, "R8F_RECOVERY_COMMITADO_SEM_REENVIO");
        }
        catch (Exception) { return new(EstadoP5.Indeterminado, false, "R8F_RECOVERY_NAO_COMPROVADO_SEM_REENVIO"); }
    }
}
