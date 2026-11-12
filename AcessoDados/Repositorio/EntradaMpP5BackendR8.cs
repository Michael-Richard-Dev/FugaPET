using System.Data.Common;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal sealed class EntradaMpP5BackendR8 : IBackendP5
{
    private readonly IExecutorTransacionalP5R8 _provider;
    private readonly IExecutorTransacionalP5R8 _recovery;
    private readonly Func<EscopoP5, CancellationToken, Task<bool>> _d2;
    private readonly HomologacaoP5 _homologacao;
    private readonly DependenciasP5R8 _dependencias;
    private readonly TimeProvider _relogio;

    internal EntradaMpP5BackendR8(IExecutorTransacionalP5R8 provider, IExecutorTransacionalP5R8 recovery,
        Func<EscopoP5, CancellationToken, Task<bool>> d2, HomologacaoP5 homologacao,
        DependenciasP5R8 dependencias, TimeProvider? relogio = null)
    {
        if (ReferenceEquals(provider, recovery) || provider.Papel != PapelP5R8.Provider || recovery.Papel != PapelP5R8.Recovery)
            throw new ArgumentException("P5_R8_IDENTIDADES_NAO_SEPARADAS");
        _provider = provider;
        _recovery = recovery;
        _d2 = d2;
        _homologacao = homologacao;
        _dependencias = dependencias;
        _relogio = relogio ?? TimeProvider.System;
    }

    public bool ResultadoLegadoAtomicoDisponivel => _dependencias.Completa && _homologacao.Completa
        && _provider.FencingAtomicoDisponivel && _provider.CodigoUsuario is > 0;
    public bool ReservaLegadaCoordenadaDisponivel => ResultadoLegadoAtomicoDisponivel;
    public bool RecoveryR6BAprovado => false;

    private void ExigirDisponibilidade()
    {
        if (!ResultadoLegadoAtomicoDisponivel) throw new InvalidOperationException("P5_R8_CONTRATO_INCOMPLETO");
    }

    public Task<bool> D2SemImpedimentoAsync(EscopoP5 escopo, CancellationToken cancellationToken)
        => _d2(escopo, cancellationToken);

    internal static NpgsqlCommand Funcao(string operacao, string nome, params (NpgsqlDbType Tipo, object? Valor)[] parametros)
    {
        string slots = string.Join(",", Enumerable.Range(1, parametros.Length).Select(indice => "$" + indice));
        NpgsqlCommand comando = new($"-- P5R8:{operacao}\nSELECT * FROM homologacao.{nome}({slots})");
        foreach (var parametro in parametros)
            comando.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = parametro.Tipo, Value = parametro.Valor ?? DBNull.Value });
        return comando;
    }

    private static NpgsqlCommand Consulta(string operacao, string sql, params (NpgsqlDbType Tipo, object? Valor)[] parametros)
    {
        NpgsqlCommand comando = new($"-- P5R8:{operacao}\n{sql}");
        foreach (var parametro in parametros)
            comando.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = parametro.Tipo, Value = parametro.Valor ?? DBNull.Value });
        return comando;
    }

    private static (NpgsqlDbType, object?) Texto(string? valor) => (NpgsqlDbType.Text, valor);
    private static (NpgsqlDbType, object?) Longo(long valor) => (NpgsqlDbType.Bigint, valor);
    private static (NpgsqlDbType, object?) Uuid(Guid valor) => (NpgsqlDbType.Uuid, valor);
    private static (NpgsqlDbType, object?)[] Titular(ClaimP5 claim) => [Longo(claim.Tentativa), Uuid(claim.Token), Uuid(claim.Correlacao)];
    private static ClaimP5 ComVersao(ClaimP5 claim, long versao)
        => new(claim.Escopo, claim.Tentativa, claim.Token, claim.Correlacao, claim.GeracaoRecovery, versao);
    private static string Texto(DbDataReader reader, string coluna) => reader.GetString(reader.GetOrdinal(coluna));
    private static string? Opcional(DbDataReader reader, string coluna)
        => reader.IsDBNull(reader.GetOrdinal(coluna)) ? null : Texto(reader, coluna);
    private static void ExigirRetorno(string retorno, string esperado)
    {
        if (retorno != esperado) throw new InvalidOperationException("P5_R8_RETORNO_SQL_NAO_COMPROVADO");
    }
    private static async Task<string> ExecutarFuncao(ISessaoP5R8 sessao, NpgsqlCommand comando, CancellationToken ct)
    {
        using (comando) return await sessao.LerUnicoAsync(comando, reader => reader.GetString(0), ct);
    }

    public async Task<ClaimP5> ReservarAsync(EscopoP5 escopo, ProvaSapP5 prova, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        if (!escopo.Valido || !prova.Negativa || !prova.Valida(escopo.Chave, _relogio.GetUtcNow(), _homologacao.EvidenciaMaxAge)
            || !await _d2(escopo, cancellationToken)) throw new InvalidOperationException("P5_R8_IDENTIDADE_OU_PROVA_INVALIDA");
        return await _provider.ExecutarAsync(async (sessao, ct) =>
        {
            using NpgsqlCommand reserva = Funcao("Reservar", "fn_entrada_mp_p5_reservar",
                Texto(escopo.Chave.Pedido), Texto(escopo.Chave.Item), Longo(escopo.Lancamento), Texto(escopo.Material),
                Texto(prova.Referencia), (NpgsqlDbType.TimestampTz, prova.ObtidaEm.UtcDateTime));
            ClaimP5 parcial = await sessao.LerUnicoAsync(reserva, reader =>
            {
                ExigirRetorno(Texto(reader, "estado_resultado"), "RESERVADO");
                return new ClaimP5(escopo, reader.GetInt64(reader.GetOrdinal("tentativa_id")),
                    reader.GetGuid(reader.GetOrdinal("claim")), reader.GetGuid(reader.GetOrdinal("correlacao")));
            }, ct);
            if (!parcial.Valido) throw new InvalidOperationException("P5_R8_CLAIM_INVALIDO");
            Identidade identidade = await LerIdentidade(sessao, parcial, ct, validarGeracao: false);
            if (identidade.Estado != "RESERVADO") throw new InvalidOperationException("P5_R8_RESERVA_INVALIDA");
            ClaimP5 claim = new(escopo, parcial.Tentativa, parcial.Token, parcial.Correlacao, identidade.Geracao, identidade.Versao);
            using NpgsqlCommand status = Consulta("StatusLegado", """
                SELECT l.status_lancamento FROM homologacao.entrada_produto_lancamento l
                WHERE l.codigo_entrada_produto_lancamento=$1 AND trim(l.numero_pedido)=$2
                  AND l.situacao_entrada_produto_lancamento IS TRUE
                  AND l.status_lancamento IN ('FINALIZADO_LOCAL','ERRO_SAP')
                  AND l.documento_material_sap IS NULL AND l.enviado_sap_em IS NULL
                  AND (SELECT count(*) FROM homologacao.entrada_produto_item i
                       WHERE i.codigo_entrada_produto_lancamento=l.codigo_entrada_produto_lancamento
                         AND i.situacao_entrada_produto_item IS TRUE AND i.status_item<>'CANCELADO')=1
                  AND EXISTS (SELECT 1 FROM homologacao.entrada_produto_item i
                       WHERE i.codigo_entrada_produto_lancamento=l.codigo_entrada_produto_lancamento
                         AND i.situacao_entrada_produto_item IS TRUE
                         AND pg_catalog.btrim(i.numero_item) ~ '^[0-9]{1,5}$'
                         AND pg_catalog.lpad(COALESCE(NULLIF(pg_catalog.ltrim(pg_catalog.btrim(i.numero_item),'0'),''),'0'),5,'0')=$3
                         AND i.codigo_sap_pedido_compra_item IS NOT NULL)
                FOR UPDATE OF l
                """, Longo(escopo.Lancamento), Texto(escopo.Chave.Pedido), Texto(escopo.Chave.Item));
            string anterior = await sessao.LerUnicoAsync(status, reader => Texto(reader, "status_lancamento"), ct);
            if (anterior is not ("FINALIZADO_LOCAL" or "ERRO_SAP")) throw new InvalidOperationException("P5_R8_STATUS_LEGADO_INVALIDO");
            ReciboLegadoP5R8 recibo = await sessao.AplicarLegadoAsync(
                new(OperacaoLegadaP5R8.Reservar, claim, _provider.CodigoUsuario!.Value, anterior), ct);
            if (!recibo.CobreUnicoItem || recibo.VersaoGuarda <= claim.VersaoGuarda)
                throw new InvalidOperationException("P5_R8_RESERVA_LEGADA_NAO_COMPROVADA");
            return new ClaimP5(escopo, claim.Tentativa, claim.Token, claim.Correlacao, claim.GeracaoRecovery, recibo.VersaoGuarda);
        }, cancellationToken);
    }

    private sealed record Identidade(long Geracao, long Versao, string Estado, string? StatusAnterior, bool Selada, DocumentoP5? Documento);

    private static async Task<Identidade> LerIdentidade(ISessaoP5R8 sessao, ClaimP5 claim, CancellationToken ct,
        bool validarGeracao = true)
    {
        if (!claim.Valido || claim.GeracaoRecovery < 0) throw new InvalidOperationException("P5_R8_CLAIM_INVALIDO");
        using NpgsqlCommand comando = Consulta("Identidade", """
            SELECT recovery_generation,versao,estado_tentativa,estado_guarda,reserva_legada_status_anterior,
                   fronteira_confirmada_em,documento_material_sap,exercicio_documento_material_sap,documento_material_item
            FROM homologacao.vw_entrada_mp_p5_d2_recuperacao
            WHERE numero_pedido=$1 AND numero_item=$2 AND codigo_entrada_produto_lancamento=$3
              AND codigo_p5_tentativa=$4 AND correlation_id=$5 AND tentativa_atual=$4 AND lancamento_dono=$3
            """, Texto(claim.Escopo.Chave.Pedido), Texto(claim.Escopo.Chave.Item), Longo(claim.Escopo.Lancamento),
            Longo(claim.Tentativa), Uuid(claim.Correlacao));
        Identidade identidade = await sessao.LerUnicoAsync(comando, reader =>
        {
            string estado = Texto(reader, "estado_tentativa");
            if (estado != Texto(reader, "estado_guarda")) throw new InvalidOperationException("P5_R8_ESTADOS_DIVERGENTES");
            string? documento = Opcional(reader, "documento_material_sap");
            return new Identidade(reader.GetInt64(reader.GetOrdinal("recovery_generation")), reader.GetInt64(reader.GetOrdinal("versao")), estado,
                Opcional(reader, "reserva_legada_status_anterior"), !reader.IsDBNull(reader.GetOrdinal("fronteira_confirmada_em")),
                documento is null ? null : new(documento, Opcional(reader, "exercicio_documento_material_sap") ?? "",
                    Opcional(reader, "documento_material_item") ?? ""));
        }, ct);
        if (identidade.Versao <= 0 || (validarGeracao && identidade.Geracao != claim.GeracaoRecovery))
            throw new InvalidOperationException("P5_R8_FENCE_DIVERGENTE");
        return identidade;
    }

    public Task MarcarEnvioCommitAsync(ClaimP5 claim, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        return _provider.ExecutarAsync(async (sessao, ct) =>
        {
            Identidade identidade = await LerIdentidade(sessao, claim, ct);
            if (identidade.Estado != "RESERVADO" || identidade.Selada) throw new InvalidOperationException("P5_R8_FRONTEIRA_INVALIDA");
            ExigirRetorno(await ExecutarFuncao(sessao, Funcao("MarcarEnvio", "fn_entrada_mp_p5_marcar_envio", Titular(claim)), ct),
                "ENVIO_INICIADO_COMMIT_E_CONFIRMAR_FRONTEIRA_ANTES_DO_HTTP");
            return true;
        }, cancellationToken);
    }

    public Task<EstadoP5> ConfirmarEnvioDuravelAsync(ClaimP5 claim, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        return _provider.ExecutarAsync(async (sessao, ct) =>
        {
            Identidade identidade = await LerIdentidade(sessao, claim, ct);
            if (identidade.Estado != "ENVIO_INICIADO") return EstadoP5.Bloqueado;
            string retorno = await ExecutarFuncao(sessao, Funcao("ConfirmarFronteira", "fn_entrada_mp_p5_confirmar_fronteira", Titular(claim)), ct);
            return !identidade.Selada && retorno == "FRONTEIRA_CONFIRMADA_UM_UNICO_POST_AUTORIZADO"
                ? EstadoP5.EnvioIniciado : EstadoP5.Bloqueado;
        }, cancellationToken);
    }

    public Task<bool> CancelarReservaPreHttpCommitAsync(ClaimP5 claim, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        return _provider.ExecutarAsync(async (sessao, ct) =>
        {
            Identidade identidade = await LerIdentidade(sessao, claim, ct);
            if (identidade.Estado != "RESERVADO" || identidade.Selada
                || identidade.StatusAnterior is not ("FINALIZADO_LOCAL" or "ERRO_SAP"))
                throw new InvalidOperationException("P5_R8_COMPENSACAO_NAO_PRE_HTTP");
            ReciboLegadoP5R8 recibo = await sessao.AplicarLegadoAsync(
                new(OperacaoLegadaP5R8.CompensarPreHttp, ComVersao(claim, identidade.Versao), _provider.CodigoUsuario!.Value, identidade.StatusAnterior), ct);
            if (!recibo.CobreUnicoItem || recibo.VersaoGuarda != identidade.Versao)
                throw new InvalidOperationException("P5_R8_COMPENSACAO_LEGADA_NAO_COMPROVADA");
            ExigirRetorno(await ExecutarFuncao(sessao, Funcao("CancelarPrePost", "fn_entrada_mp_p5_cancelar_pre_post",
                [.. Titular(claim), Texto("P5_R8_ABORT_PRE_HTTP")]), ct),
                "CANCELADO_SEM_ENVIO_REQUIRES_NEW_SAP_PROOF_FOR_RETRY");
            return true;
        }, cancellationToken);
    }

    public async Task RegistrarResultadoCommitAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        bool naoAutorizaConfirmacao = await _provider.ExecutarAsync(async (sessao, ct) =>
        {
            Identidade identidade = await LerIdentidade(sessao, claim, ct);
            DocumentoP5? doc = retorno.Documento;
            if (identidade.Estado != "ENVIO_INICIADO")
            {
                string evidencia = await ExecutarFuncao(sessao, Funcao("RetornoTardio", "fn_entrada_mp_p5_registrar_retorno_tardio",
                    [.. Titular(claim), Texto(claim.Escopo.Chave.Pedido), Texto(claim.Escopo.Chave.Item), Texto(claim.Escopo.Material),
                     (NpgsqlDbType.Integer, retorno.Status), Texto("P5_R8_RETORNO_TARDIO"), Texto(doc?.Numero), Texto(doc?.Ano), Texto(doc?.Item)]), ct);
                if (!evidencia.StartsWith("RETORNO_TARDIO_", StringComparison.Ordinal))
                    throw new InvalidOperationException("P5_R8_RETORNO_TARDIO_NAO_REGISTRADO");
                return true;
            }
            bool confirmado = retorno.Confirmado && identidade.Selada;
            string esperado = confirmado ? "CONFIRMADO_SAP" : "RESULTADO_INDETERMINADO";
            string resultado = await ExecutarFuncao(sessao, Funcao("Resultado", "fn_entrada_mp_p5_resultado_http",
                [.. Titular(claim), Texto(esperado), (NpgsqlDbType.Integer, retorno.Status),
                 Texto(confirmado ? null : "P5_RESULTADO_REMOTO_DESCONHECIDO"), Texto(doc?.Numero), Texto(doc?.Ano), Texto(doc?.Item)]), ct);
            if (resultado != esperado && !(esperado == "RESULTADO_INDETERMINADO"
                && resultado == "RESULTADO_INDETERMINADO_FORCADO_SEM_SELO_DE_FRONTEIRA"))
                throw new InvalidOperationException("P5_R8_RESULTADO_SQL_DIVERGENTE");
            if (confirmado)
            {
                Identidade atualizada = await LerIdentidade(sessao, claim, ct);
                ReciboLegadoP5R8 recibo = await sessao.AplicarLegadoAsync(
                    new(OperacaoLegadaP5R8.ConfirmarResultado, ComVersao(claim, atualizada.Versao), _provider.CodigoUsuario!.Value, documento: doc), ct);
                if (!recibo.CobreUnicoItem || recibo.Documento != doc)
                    throw new InvalidOperationException("P5_R8_CONFIRMACAO_LEGADA_NAO_COMPROVADA");
            }
            return retorno.Confirmado && !identidade.Selada;
        }, cancellationToken);
        if (naoAutorizaConfirmacao) throw new InvalidOperationException("P5_R8_RESULTADO_NAO_AUTORIZA_CONFIRMACAO");
    }

    public async Task<EstadoP5> ConfirmarResultadoPersistidoAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        if (retorno.Confirmado) return EstadoP5.Bloqueado;
        return await _provider.ExecutarAsync(async (sessao, ct) =>
        {
            Identidade identidade = await LerIdentidade(sessao, claim, ct);
            return EntradaMpP5BackendRepositorio.InterpretarEstado(identidade.Estado);
        }, cancellationToken);
    }

    public Task<EstadoP5> ConsultarEstadoAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken)
    {
        ExigirDisponibilidade();
        if (!escopo.Valido || tentativa <= 0 || correlacao == Guid.Empty) throw new InvalidOperationException("P5_R8_RETOMADA_INVALIDA");
        return _provider.ExecutarAsync(async (sessao, ct) =>
        {
            using NpgsqlCommand comando = Consulta("ConsultarEstado", """
                SELECT estado_tentativa FROM homologacao.vw_entrada_mp_p5_d2_recuperacao
                WHERE numero_pedido=$1 AND numero_item=$2 AND codigo_entrada_produto_lancamento=$3
                  AND codigo_p5_tentativa=$4 AND correlation_id=$5 AND tentativa_atual=$4 AND lancamento_dono=$3
                """, Texto(escopo.Chave.Pedido), Texto(escopo.Chave.Item), Longo(escopo.Lancamento), Longo(tentativa), Uuid(correlacao));
            return await sessao.LerUnicoAsync(comando,
                reader => EntradaMpP5BackendRepositorio.InterpretarEstado(Texto(reader, "estado_tentativa")), ct);
        }, cancellationToken);
    }

    public Task<RecoveryP5> AssumirRecoveryCommitAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken)
        => throw new InvalidOperationException("P5_R8_RECOVERY_LEGADO_ATOMICO_NAO_HOMOLOGADO");
    public Task FinalizarRecoveryCommitAsync(RecoveryP5 recovery, ProvaSapP5 prova, CancellationToken cancellationToken)
        => throw new InvalidOperationException("P5_R8_RECOVERY_LEGADO_ATOMICO_NAO_HOMOLOGADO");
    public Task CancelarRecoveryCommitAsync(RecoveryP5 recovery, CancellationToken cancellationToken)
        => throw new InvalidOperationException("P5_R8_RECOVERY_LEGADO_ATOMICO_NAO_HOMOLOGADO");
}
