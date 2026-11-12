using System.Data;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal sealed class EntradaMpP5BackendRepositorio(IFabricaConexaoBanco fabricaBackend,
    EntradaMpP5D2Repositorio d2, HomologacaoP5 homologacao, TimeProvider? relogio = null) : IBackendP5
{
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    internal enum Operacao { Reservar, MarcarEnvio, Resultado, RecoveryAssumir, RecoveryFinalizar, RecoveryCancelar }

    internal static NpgsqlCommand CriarComando(Operacao operacao, NpgsqlConnection conexao,
        NpgsqlTransaction? tx, params (NpgsqlDbType Tipo, object? Valor)[] parametros)
    {
        (string nome, int quantidade) = operacao switch
        {
            Operacao.Reservar => ("fn_entrada_mp_p5_reservar", 6),
            Operacao.MarcarEnvio => ("fn_entrada_mp_p5_marcar_envio", 3),
            Operacao.Resultado => ("fn_entrada_mp_p5_resultado_http", 9),
            Operacao.RecoveryAssumir => ("fn_entrada_mp_p5_recuperar_assumir", 5),
            Operacao.RecoveryFinalizar => ("fn_entrada_mp_p5_recuperar_finalizar", 21),
            Operacao.RecoveryCancelar => ("fn_entrada_mp_p5_recuperar_cancelar_reserva", 8),
            _ => throw new InvalidOperationException("P5_OPERACAO_INVALIDA")
        };
        if (parametros.Length != quantidade) throw new InvalidOperationException("P5_PARAMETROS_INVALIDOS");
        string slots = string.Join(",", Enumerable.Range(1, quantidade).Select(indice => "$" + indice));
        NpgsqlCommand command = new($"SELECT * FROM homologacao.{nome}({slots})", conexao, tx);
        foreach ((NpgsqlDbType tipo, object? valor) in parametros)
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = tipo, Value = valor ?? DBNull.Value });
        return command;
    }

    private void ExigirBackendHomologado()
    {
        if (!BloqueioP5R7.MutacoesHomologadas || !homologacao.Completa)
            throw new InvalidOperationException("P5_BACKEND_NAO_HOMOLOGADO");
    }

    private async Task<T> ExecutarCommitAsync<T>(Operacao operacao,
        (NpgsqlDbType Tipo, object? Valor)[] parametros, Func<NpgsqlDataReader, T> interpretar,
        CancellationToken cancellationToken)
    {
        ExigirBackendHomologado();
        try
        {
            await using NpgsqlConnection conexao = await fabricaBackend.CriarConexaoAbertaAsync(cancellationToken);
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            T retorno;
            await using (NpgsqlCommand command = CriarComando(operacao, conexao, tx, parametros))
            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("P5_RETORNO_AUSENTE");
                retorno = interpretar(reader);
                if (await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("P5_RETORNO_AMBIGUO");
            }
            try
            {
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception) when (retorno is ClaimP5)
            {
                throw new CommitP5IndeterminadoException((ClaimP5)(object)retorno!);
            }
            return retorno;
        }
        catch (CommitP5IndeterminadoException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            throw new InvalidOperationException("P5_COMMIT_NAO_COMPROVADO");
        }
    }

    public async Task<bool> D2SemImpedimentoAsync(EscopoP5 escopo, CancellationToken cancellationToken)
    {
        ResultadoRecebimentoLocal local = await d2.ConsultarAsync(escopo.Chave.Pedido, escopo.Chave.Item, escopo.Lancamento, cancellationToken);
        return local.Cenario == CenarioRecebimentoLocal.SemRegistro;
    }

    public Task<ClaimP5> ReservarAsync(EscopoP5 escopo, ProvaSapP5 prova, CancellationToken cancellationToken)
    {
        if (!escopo.Valido || !prova.Negativa || !prova.Valida(escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge))
            throw new InvalidOperationException("P5_PROVA_INVALIDA");
        return ExecutarCommitAsync(Operacao.Reservar,
            [Texto(escopo.Chave.Pedido), Texto(escopo.Chave.Item), Longo(escopo.Lancamento), Texto(escopo.Material),
             Texto(prova.Referencia), Instante(prova.ObtidaEm)],
            reader => reader.GetString(3) == "RESERVADO"
                ? new ClaimP5(escopo, reader.GetInt64(0), reader.GetGuid(1), reader.GetGuid(2))
                : throw new InvalidOperationException("P5_ESTADO_INVALIDO"), cancellationToken);
    }

    public async Task MarcarEnvioCommitAsync(ClaimP5 claim, CancellationToken cancellationToken)
    {
        ExigirClaim(claim);
        string retorno = await ExecutarCommitAsync(Operacao.MarcarEnvio,
            [Longo(claim.Tentativa), Uuid(claim.Token), Uuid(claim.Correlacao)], reader => reader.GetString(0), cancellationToken);
        if (retorno != "ENVIO_INICIADO_COMMIT_REQUIRED_BEFORE_HTTP") throw new InvalidOperationException("P5_ENVIO_NAO_COMPROVADO");
    }

    public async Task RegistrarResultadoCommitAsync(ClaimP5 claim, RetornoHttpP5 retorno, CancellationToken cancellationToken)
    {
        ExigirClaim(claim);
        string esperado = retorno.Confirmado ? "CONFIRMADO_SAP" : "RESULTADO_INDETERMINADO";
        DocumentoP5? doc = retorno.Confirmado ? retorno.Documento : null;
        string estado = await ExecutarCommitAsync(Operacao.Resultado,
            [Longo(claim.Tentativa), Uuid(claim.Token), Uuid(claim.Correlacao), Texto(esperado),
             (NpgsqlDbType.Integer, retorno.Status), Texto(retorno.Confirmado ? null : "P5_RESULTADO_REMOTO_DESCONHECIDO"),
             Texto(doc?.Numero), Texto(doc?.Ano), Texto(doc?.Item)], reader => reader.GetString(0), cancellationToken);
        if (estado != esperado) throw new InvalidOperationException("P5_RESULTADO_NAO_COMPROVADO");
    }

    public Task<RecoveryP5> AssumirRecoveryCommitAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken)
    {
        if (!escopo.Valido || tentativa <= 0 || correlacao == Guid.Empty) throw new InvalidOperationException("P5_RECOVERY_INVALIDO");
        return ExecutarCommitAsync(Operacao.RecoveryAssumir,
            [Texto(escopo.Chave.Pedido), Texto(escopo.Chave.Item), Longo(escopo.Lancamento), Longo(tentativa), Uuid(correlacao)],
            reader => new RecoveryP5(escopo, tentativa, correlacao, reader.GetGuid(0), reader.GetInt64(1),
                InterpretarEstado(reader.GetString(2)), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3)), cancellationToken);
    }

    public async Task FinalizarRecoveryCommitAsync(RecoveryP5 recovery, ProvaSapP5 prova, CancellationToken cancellationToken)
    {
        if (!recovery.Valido || recovery.EnvioIniciadoEm is null
            || !prova.Valida(recovery.Escopo.Chave, _relogio.GetUtcNow(), homologacao.EvidenciaMaxAge, recovery.EnvioIniciadoEm)
            || (!prova.Negativa && !(prova.Movimentos > 0 && prova.Documento?.Valido == true)))
            throw new InvalidOperationException("P5_RECOVERY_PROVA_INVALIDA");
        string esperado = prova.Negativa ? "NAO_CONTABILIZADO_COMPROVADO" : "CONFIRMADO_SAP";
        string estado = await ExecutarCommitAsync(Operacao.RecoveryFinalizar,
            [Texto(recovery.Escopo.Chave.Pedido), Texto(recovery.Escopo.Chave.Item), Longo(recovery.Escopo.Lancamento),
             Longo(recovery.Tentativa), Uuid(recovery.Correlacao), Uuid(recovery.Token), Longo(recovery.Geracao), Texto(esperado),
             Texto(prova.Referencia), Texto(prova.Fonte), Texto(prova.Chave.Pedido), Texto(prova.Chave.Item),
             Instante(prova.ObtidaEm), Instante(prova.ValidaAte), (NpgsqlDbType.Boolean, prova.HistoricoCompleto),
             (NpgsqlDbType.Boolean, prova.EstornosReconciliados), (NpgsqlDbType.Integer, prova.Movimentos),
             (NpgsqlDbType.Integer, prova.Estornos), Texto(prova.Documento?.Numero), Texto(prova.Documento?.Ano), Texto(prova.Documento?.Item)],
            reader => reader.GetString(0), cancellationToken);
        if (estado != esperado) throw new InvalidOperationException("P5_RECOVERY_NAO_COMPROVADO");
    }

    public async Task CancelarRecoveryCommitAsync(RecoveryP5 recovery, CancellationToken cancellationToken)
    {
        if (!recovery.Valido || recovery.Estado != EstadoP5.Reservado || recovery.EnvioIniciadoEm is not null)
            throw new InvalidOperationException("P5_RECOVERY_NAO_PRE_HTTP");
        string estado = await ExecutarCommitAsync(Operacao.RecoveryCancelar,
            [Texto(recovery.Escopo.Chave.Pedido), Texto(recovery.Escopo.Chave.Item), Longo(recovery.Escopo.Lancamento),
             Longo(recovery.Tentativa), Uuid(recovery.Correlacao), Uuid(recovery.Token), Longo(recovery.Geracao), Texto("P5_RECOVERY_CANCELAMENTO_PRE_HTTP")],
            reader => reader.GetString(0), cancellationToken);
        if (!estado.StartsWith("CANCELADO_SEM_ENVIO", StringComparison.Ordinal)) throw new InvalidOperationException("P5_CANCELAMENTO_NAO_COMPROVADO");
    }

    public async Task<EstadoP5> ConsultarEstadoAsync(EscopoP5 escopo, long tentativa, Guid correlacao, CancellationToken cancellationToken)
    {
        if (!homologacao.Banco || !homologacao.Seguranca) return EstadoP5.Bloqueado;
        const string sql = "SELECT estado_tentativa FROM homologacao.vw_entrada_mp_p5_d2_recuperacao WHERE numero_pedido=$1 AND numero_item=$2 AND codigo_entrada_produto_lancamento=$3 AND codigo_p5_tentativa=$4 AND correlation_id=$5";
        try
        {
            await using NpgsqlConnection conexao = await fabricaBackend.CriarConexaoAbertaAsync(cancellationToken);
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using (NpgsqlCommand readOnly = new("SET TRANSACTION READ ONLY", conexao, tx)) await readOnly.ExecuteNonQueryAsync(cancellationToken);
            await using NpgsqlCommand command = new(sql, conexao, tx);
            foreach (var parametro in new[] { Texto(escopo.Chave.Pedido), Texto(escopo.Chave.Item), Longo(escopo.Lancamento), Longo(tentativa), Uuid(correlacao) })
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = parametro.Tipo, Value = parametro.Valor ?? DBNull.Value });
            EstadoP5 estado;
            await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken)) return EstadoP5.Bloqueado;
                estado = InterpretarEstado(reader.GetString(0));
                if (await reader.ReadAsync(cancellationToken)) return EstadoP5.Bloqueado;
            }
            await tx.RollbackAsync(cancellationToken);
            return estado;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return EstadoP5.Bloqueado; }
    }

    internal static EstadoP5 InterpretarEstado(string estado) => estado switch
    {
        "RESERVADO" => EstadoP5.Reservado, "ENVIO_INICIADO" => EstadoP5.EnvioIniciado,
        "RESULTADO_INDETERMINADO" => EstadoP5.Indeterminado, "CONFIRMADO_SAP" => EstadoP5.Confirmado,
        "NAO_CONTABILIZADO_COMPROVADO" => EstadoP5.NaoContabilizado, "CANCELADO_SEM_ENVIO" => EstadoP5.CanceladoSemEnvio,
        _ => EstadoP5.Bloqueado
    };
    private static void ExigirClaim(ClaimP5 claim) { if (!claim.Valido) throw new InvalidOperationException("P5_CLAIM_INVALIDO"); }
    private static (NpgsqlDbType Tipo, object? Valor) Texto(string? valor) => (NpgsqlDbType.Text, valor);
    private static (NpgsqlDbType Tipo, object? Valor) Longo(long valor) => (NpgsqlDbType.Bigint, valor);
    private static (NpgsqlDbType Tipo, object? Valor) Uuid(Guid valor) => (NpgsqlDbType.Uuid, valor);
    private static (NpgsqlDbType Tipo, object? Valor) Instante(DateTimeOffset valor) => (NpgsqlDbType.TimestampTz, valor.UtcDateTime);
}
