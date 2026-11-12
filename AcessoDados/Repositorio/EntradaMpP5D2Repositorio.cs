using System.Data;
using System.Reflection;
using System.Text.Json;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Entrada;
using FugaPET_Dev.Servicos.Entrada.P5;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

internal sealed class EntradaMpP5D2Repositorio(IFabricaConexaoBanco fabrica, HomologacaoP5 homologacao)
    : IEvidenciaRecebimentoLocalServico
{
    internal static string SqlLocal => LerRecurso("D2LocalR5B.sql");
    internal static string SqlRecovery => LerRecurso("D2RecuperacaoR6A1.sql");

    private static string LerRecurso(string nome)
    {
        Assembly assembly = typeof(EntradaMpP5D2Repositorio).Assembly;
        string recurso = assembly.GetManifestResourceNames().Single(item => item.EndsWith("." + nome, StringComparison.Ordinal));
        using Stream stream = assembly.GetManifestResourceStream(recurso)!;
        using StreamReader reader = new(stream);
        string sql = reader.ReadToEnd();
        return nome == "D2LocalR5B.sql" ? sql[sql.IndexOf("WITH entrada AS", StringComparison.Ordinal)..] : sql;
    }

    internal static NpgsqlCommand CriarComando(string sql, ChaveP5 chave, long? lancamento,
        NpgsqlConnection conexao, NpgsqlTransaction? transacao = null)
    {
        NpgsqlCommand command = new(sql, conexao, transacao);
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = chave.Pedido });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Text, Value = chave.Item });
        command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Bigint, Value = (object?)lancamento ?? DBNull.Value });
        return command;
    }

    public async Task<ResultadoRecebimentoLocal> ConsultarAsync(string numeroPedido, string numeroItemPedido,
        long codigoLancamentoAtual, CancellationToken cancellationToken = default)
    {
        if (!homologacao.Banco || !homologacao.Seguranca)
            return ResultadoRecebimentoLocal.Indisponivel("P5_D2_DEPENDENCIAS_NAO_HOMOLOGADAS");
        try
        {
            ChaveP5 chave = new(numeroPedido, numeroItemPedido);
            await using NpgsqlConnection conexao = await fabrica.CriarConexaoAbertaAsync(cancellationToken);
            await using NpgsqlTransaction tx = await conexao.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await using (NpgsqlCommand readOnly = new("SET TRANSACTION READ ONLY", conexao, tx))
                await readOnly.ExecuteNonQueryAsync(cancellationToken);
            List<Dictionary<string, object?>> locais = await ConsultarLinhasAsync(SqlLocal, chave, codigoLancamentoAtual, conexao, tx, cancellationToken);
            List<Dictionary<string, object?>> recovery = await ConsultarLinhasAsync(SqlRecovery, chave, codigoLancamentoAtual, conexao, tx, cancellationToken);
            ResultadoRecebimentoLocal resultado = Classificar(locais, recovery);
            await tx.RollbackAsync(cancellationToken);
            return resultado;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            return ResultadoRecebimentoLocal.Indisponivel("P5_D2_FALHA_CONSULTA");
        }
    }

    private static async Task<List<Dictionary<string, object?>>> ConsultarLinhasAsync(string sql, ChaveP5 chave,
        long lancamento, NpgsqlConnection conexao, NpgsqlTransaction tx, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CriarComando(sql, chave, lancamento, conexao, tx);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        List<Dictionary<string, object?>> result = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            Dictionary<string, object?> row = new(StringComparer.Ordinal);
            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                row.Add(reader.GetName(ordinal), reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal));
            result.Add(row);
        }
        return result;
    }

    internal static ResultadoRecebimentoLocal Classificar(IReadOnlyList<Dictionary<string, object?>> locais,
        IReadOnlyList<Dictionary<string, object?>> recovery)
    {
        if (locais.Count == 0 || recovery.Count != 1)
            return ResultadoRecebimentoLocal.Indisponivel("P5_D2_CARDINALIDADE_INVALIDA");
        foreach (Dictionary<string, object?> row in locais)
        {
            if (!row.TryGetValue("chave_valida", out object? valid) || valid is not true
                || !row.TryGetValue("prova_negativa_sap", out object? negativa) || negativa is not false
                || !row.TryGetValue("categoria_local_p5", out object? category))
                return ResultadoRecebimentoLocal.Indisponivel("P5_D2_CONTRATO_INVALIDO");
            if (category is "RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK")
                return new[] { "status_lancamento", "status_item", "status_lote" }
                    .Any(campo => row.TryGetValue(campo, out object? status) && status is "CONFIRMADO_SAP")
                    ? ResultadoRecebimentoLocal.Confirmado("P5_D2_RECEBIMENTO_LOCAL_CONFIRMADO")
                    : ResultadoRecebimentoLocal.Pendente("P5_D2_DOCUMENTO_EXIGE_RECONCILIACAO");
            if (category is not ("SEM_ENTRADA_LOCAL_NAO_PROVA_SAP" or "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO"))
                return ResultadoRecebimentoLocal.Pendente("P5_D2_HISTORICO_IMPEDITIVO");
            if (!row.TryGetValue("lancamentos_sem_item_no_pedido", out object? semItens) || !Equals(semItens, 0L)
                || !row.TryGetValue("cache_item_count", out object? cache) || !Equals(cache, 1L))
                return ResultadoRecebimentoLocal.Pendente("P5_D2_AMBIGUIDADE_LOCAL");
            if (category is "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO" &&
                (!row.TryGetValue("eh_lancamento_atual", out object? proprio) || proprio is not true
                 || !MaterialECacheCoerentes(row)))
                return ResultadoRecebimentoLocal.Pendente("P5_D2_IDENTIDADE_DIVERGENTE");
        }
        Dictionary<string, object?> state = recovery[0];
        if (!state.TryGetValue("chave_valida", out object? chaveValida) || chaveValida is not true
            || !state.TryGetValue("prova_negativa_sap", out object? prova) || prova is not false
            || !state.TryGetValue("p5_local_decision", out object? decision)
            || decision is not "NULL_BLOCK_NEEDS_AUTHORITATIVE_SAP_PROOF_AND_ATOMIC_GUARD")
            return ResultadoRecebimentoLocal.Pendente("P5_D2_GUARDA_IMPEDITIVA_OU_AUSENTE");
        return ResultadoRecebimentoLocal.SemRegistro();
    }

    private static bool MaterialECacheCoerentes(Dictionary<string, object?> row)
    {
        if (!row.TryGetValue("material_local", out object? material) || material is not string local
            || string.IsNullOrWhiteSpace(local)
            || !row.TryGetValue("cache_itens", out object? cache) || cache is not string json)
            return false;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement item = doc.RootElement.EnumerateArray().Single();
            if (item.GetProperty("material").GetString()?.Trim() != local.Trim()) return false;
            return row.TryGetValue("codigo_sap_pedido_compra_item", out object? fk)
                && fk is long codigo && codigo > 0
                && item.GetProperty("sap_item_id").GetInt64() == codigo;
        }
        catch (Exception) { return false; }
    }
}
