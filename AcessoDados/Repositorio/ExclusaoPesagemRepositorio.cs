using System.Data;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Entrada;
using FugaPET_Dev.Servicos.Operacao;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// EXCLUIR PESAGEM (GATE 052) — cancelamento lógico local em UMA transação SERIALIZABLE:
/// SET LOCAL app.usuario_id → lock order LANCAMENTO→ITEM→LOTE(se houver)→PESAGEM (FOR UPDATE) → revalidação
/// pós-lock → guard SAP por correlation_id (fn_entrada_produto_sap_guard_counts) → UPDATE pesagem VALIDA→CANCELADA
/// (rowcount=1) → recálculo quantidade_recebida do item → cascata de vazio operacional (lote/item/lançamento).
/// NUNCA DELETE físico, NUNCA SAP, SEM auto-retry de serialization_failure (retorna EstadoMudou).
/// </summary>
public sealed class ExclusaoPesagemRepositorio : RepositorioBase, IExclusaoPesagemRepositorio
{
    private const string StatusLocalElegivel = "FINALIZADO_LOCAL";
    private const string StatusPesagemValida = "VALIDA";
    private const string StatusCancelado = "CANCELADO";
    private const string StatusPesagemCancelada = "CANCELADA";

    public ExclusaoPesagemRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<ResultadoExclusaoPesagemRepositorio> ExcluirPesagemLocalAsync(
        long codigoPesagem,
        long codigoUsuario,
        CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao =
            await conexao.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            await DefinirUsuarioAppAsync(conexao, transacao, codigoUsuario, cancellationToken);

            // (0) Localiza a pesagem e a árvore (sem lock ainda) — apenas para descobrir os PKs a travar em ordem.
            (long codigoItem, long? codigoLote, long codigoLancamento, bool pesagemValida, bool existe) =
                await LocalizarArvoreAsync(conexao, transacao, codigoPesagem, cancellationToken);
            if (!existe)
            {
                await transacao.RollbackAsync(cancellationToken);
                return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.PesagemInexistente, false);
            }
            if (!pesagemValida)
            {
                await transacao.RollbackAsync(cancellationToken);
                return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.JaCancelada, false);
            }

            // (1) LOCK ORDER: LANCAMENTO → ITEM → LOTE(se houver) → PESAGEM, cada um FOR UPDATE, revalidando.
            EstadoLancamento lanc = await BloquearLancamentoAsync(conexao, transacao, codigoLancamento, cancellationToken);
            if (!lanc.Elegivel)
            {
                await transacao.RollbackAsync(cancellationToken);
                return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.BloqueadoSap, false);
            }

            bool itemElegivel = await BloquearItemElegivelAsync(conexao, transacao, codigoItem, cancellationToken);
            if (!itemElegivel)
            {
                await transacao.RollbackAsync(cancellationToken);
                return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.BloqueadoSap, false);
            }

            Guid? correlationId = null;
            if (codigoLote is long lote)
            {
                (bool loteElegivel, Guid corr) = await BloquearLoteElegivelAsync(conexao, transacao, lote, cancellationToken);
                if (!loteElegivel)
                {
                    await transacao.RollbackAsync(cancellationToken);
                    return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.BloqueadoSap, false);
                }
                correlationId = corr;
            }

            bool aindaValida = await BloquearPesagemValidaAsync(conexao, transacao, codigoPesagem, cancellationToken);
            if (!aindaValida)
            {
                await transacao.RollbackAsync(cancellationToken);
                return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.EstadoMudou, false);
            }

            // (2) GUARD SAP por correlation_id (só quando há lote; sem lote não há outbox/tentativa possíveis).
            if (correlationId is Guid corrId)
            {
                (long outbox, long tentativa) = await ObterGuardCountsAsync(conexao, transacao, corrId, cancellationToken);
                if (outbox != 0 || tentativa != 0)
                {
                    await transacao.RollbackAsync(cancellationToken);
                    return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.BloqueadoSap, false);
                }
            }

            // (3) CANCELAMENTO LÓGICO por PK, condicional a VALIDA/ativa (rowcount == 1).
            int cancelados = await CancelarPesagemAsync(conexao, transacao, codigoPesagem, codigoUsuario, cancellationToken);
            if (cancelados != 1)
            {
                await transacao.RollbackAsync(cancellationToken);
                return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.EstadoMudou, false);
            }

            // (4) RECÁLCULO quantidade_recebida do item (SUM peso_liquido_kg das pesagens VÁLIDAS/ativas).
            await RecalcularQuantidadeItemAsync(conexao, transacao, codigoItem, codigoUsuario, cancellationToken);

            // (5) CASCATA de vazio operacional (nunca DELETE; nunca afeta sibling válido).
            if (codigoLote is long loteCascata)
            {
                await CancelarLoteSeVazioAsync(conexao, transacao, loteCascata, codigoUsuario, cancellationToken);
            }
            await CancelarItemSeVazioAsync(conexao, transacao, codigoItem, codigoUsuario, cancellationToken);
            bool arvoreVazia = await CancelarLancamentoSeVazioAsync(
                conexao, transacao, codigoLancamento, codigoUsuario, cancellationToken);

            await transacao.CommitAsync(cancellationToken);
            return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.Excluida, arvoreVazia);
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "40P01")
        {
            // serialization_failure / deadlock_detected — SEM auto-retry: o usuário revalida e tenta de novo.
            await RollbackSeguroAsync(transacao, cancellationToken);
            return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.EstadoMudou, false);
        }
        catch (OperationCanceledException)
        {
            await RollbackSeguroAsync(transacao, cancellationToken);
            throw;
        }
        catch
        {
            await RollbackSeguroAsync(transacao, cancellationToken);
            return new ResultadoExclusaoPesagemRepositorio(CenarioExclusaoPesagem.FalhaTecnica, false);
        }
    }

    private sealed record EstadoLancamento(bool Elegivel);

    private static async Task<(long codigoItem, long? codigoLote, long codigoLancamento, bool pesagemValida, bool existe)>
        LocalizarArvoreAsync(NpgsqlConnection c, NpgsqlTransaction t, long codigoPesagem, CancellationToken ct)
    {
        const string sql = """
            SELECT p.codigo_entrada_produto_item,
                   p.codigo_entrada_produto_lote,
                   i.codigo_entrada_produto_lancamento,
                   (p.status_pesagem = 'VALIDA' AND p.situacao_entrada_produto_pesagem = true) AS valida
              FROM entrada_produto_pesagem p
              JOIN entrada_produto_item i
                ON i.codigo_entrada_produto_item = p.codigo_entrada_produto_item
             WHERE p.codigo_entrada_produto_pesagem = @codigo_pesagem;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo_pesagem", codigoPesagem));
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return (0, null, 0, false, false);
        }
        long item = r.GetInt64(0);
        long? lote = r.IsDBNull(1) ? null : r.GetInt64(1);
        long lanc = r.GetInt64(2);
        bool valida = r.GetBoolean(3);
        return (item, lote, lanc, valida, true);
    }

    private static async Task<EstadoLancamento> BloquearLancamentoAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoLancamento, CancellationToken ct)
    {
        const string sql = """
            SELECT (status_lancamento = 'FINALIZADO_LOCAL'
                    AND situacao_entrada_produto_lancamento = true
                    AND documento_material_sap IS NULL
                    AND exercicio_documento_material_sap IS NULL
                    AND enviado_sap_em IS NULL) AS elegivel
              FROM entrada_produto_lancamento
             WHERE codigo_entrada_produto_lancamento = @codigo
             FOR UPDATE;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
        object? v = await cmd.ExecuteScalarAsync(ct);
        return new EstadoLancamento(v is bool b && b);
    }

    private static async Task<bool> BloquearItemElegivelAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoItem, CancellationToken ct)
    {
        const string sql = """
            SELECT (status_item = 'FINALIZADO_LOCAL'
                    AND situacao_entrada_produto_item = true
                    AND documento_material_item IS NULL) AS elegivel
              FROM entrada_produto_item
             WHERE codigo_entrada_produto_item = @codigo
             FOR UPDATE;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoItem));
        object? v = await cmd.ExecuteScalarAsync(ct);
        return v is bool b && b;
    }

    private static async Task<(bool elegivel, Guid correlationId)> BloquearLoteElegivelAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoLote, CancellationToken ct)
    {
        // Schema físico do lote NÃO possui coluna booleana de situação: o estado é o próprio status_lote
        // (CHECK inclui 'CANCELADO'). 'FINALIZADO_LOCAL' já exclui qualquer estado SAP/cancelado — elegibilidade
        // local equivalente ao contrato 054/055, sem introduzir um booleano inexistente no banco.
        const string sql = """
            SELECT (status_lote = 'FINALIZADO_LOCAL') AS elegivel,
                   correlation_id
              FROM entrada_produto_lote
             WHERE codigo_entrada_produto_lote = @codigo
             FOR UPDATE;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoLote));
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            return (false, Guid.Empty);
        }
        bool elegivel = !r.IsDBNull(0) && r.GetBoolean(0);
        Guid corr = r.IsDBNull(1) ? Guid.Empty : r.GetGuid(1);
        return (elegivel && corr != Guid.Empty, corr);
    }

    private static async Task<bool> BloquearPesagemValidaAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoPesagem, CancellationToken ct)
    {
        const string sql = """
            SELECT (status_pesagem = 'VALIDA' AND situacao_entrada_produto_pesagem = true) AS valida
              FROM entrada_produto_pesagem
             WHERE codigo_entrada_produto_pesagem = @codigo
             FOR UPDATE;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoPesagem));
        object? v = await cmd.ExecuteScalarAsync(ct);
        return v is bool b && b;
    }

    private static async Task<(long outbox, long tentativa)> ObterGuardCountsAsync(
        NpgsqlConnection c, NpgsqlTransaction t, Guid correlationId, CancellationToken ct)
    {
        const string sql =
            "SELECT outbox_matches, tentativa_matches FROM fn_entrada_produto_sap_guard_counts(@correlation);";
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(new NpgsqlParameter("@correlation", NpgsqlDbType.Uuid) { Value = correlationId });
        await using NpgsqlDataReader r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            // Sem linha = comportamento desconhecido → tratar como bloqueio (fail-closed).
            return (1, 1);
        }
        return (r.GetInt64(0), r.GetInt64(1));
    }

    private static async Task<int> CancelarPesagemAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoPesagem, long usuario, CancellationToken ct)
    {
        const string sql = """
            UPDATE entrada_produto_pesagem
               SET status_pesagem = 'CANCELADA',
                   situacao_entrada_produto_pesagem = false,
                   entrada_produto_pesagem_atualizado_por = @usuario
             WHERE codigo_entrada_produto_pesagem = @codigo
               AND status_pesagem = 'VALIDA'
               AND situacao_entrada_produto_pesagem = true;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoPesagem));
        cmd.Parameters.Add(ParametroLongo("@usuario", usuario));
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task RecalcularQuantidadeItemAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoItem, long usuario, CancellationToken ct)
    {
        const string sql = """
            UPDATE entrada_produto_item item
               SET quantidade_recebida = (
                       SELECT COALESCE(SUM(p.peso_liquido_kg), 0)
                         FROM entrada_produto_pesagem p
                        WHERE p.codigo_entrada_produto_item = item.codigo_entrada_produto_item
                          AND p.situacao_entrada_produto_pesagem = true
                          AND p.status_pesagem = 'VALIDA'
                   ),
                   entrada_produto_item_atualizado_por = @usuario
             WHERE item.codigo_entrada_produto_item = @codigo;
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoItem));
        cmd.Parameters.Add(ParametroLongo("@usuario", usuario));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task CancelarLoteSeVazioAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoLote, long usuario, CancellationToken ct)
    {
        // Cancelamento lógico do lote (nunca DELETE físico). Colunas físicas reais: status_lote + alterado_por
        // (varchar(80)); alterado_em é carimbado automaticamente pela trigger BEFORE UPDATE do lote. Não existe
        // coluna de situação booleana nem *_atualizado_por para o lote. app.usuario_id (SET LOCAL) alimenta a
        // auditoria; alterado_por registra o autor como texto, coerente com o contrato de autoria do lote.
        const string sql = """
            UPDATE entrada_produto_lote lote
               SET status_lote = 'CANCELADO',
                   alterado_por = @usuario
             WHERE lote.codigo_entrada_produto_lote = @codigo
               AND lote.status_lote <> 'CANCELADO'
               AND NOT EXISTS (
                   SELECT 1 FROM entrada_produto_pesagem p
                    WHERE p.codigo_entrada_produto_lote = lote.codigo_entrada_produto_lote
                      AND p.situacao_entrada_produto_pesagem = true
                      AND p.status_pesagem = 'VALIDA');
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoLote));
        cmd.Parameters.Add(ParametroTexto("@usuario", usuario.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task CancelarItemSeVazioAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoItem, long usuario, CancellationToken ct)
    {
        const string sql = """
            UPDATE entrada_produto_item item
               SET status_item = 'CANCELADO',
                   situacao_entrada_produto_item = false,
                   quantidade_recebida = 0,
                   entrada_produto_item_atualizado_por = @usuario
             WHERE item.codigo_entrada_produto_item = @codigo
               AND item.status_item <> 'CANCELADO'
               AND NOT EXISTS (
                   SELECT 1 FROM entrada_produto_pesagem p
                    WHERE p.codigo_entrada_produto_item = item.codigo_entrada_produto_item
                      AND p.situacao_entrada_produto_pesagem = true
                      AND p.status_pesagem = 'VALIDA');
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoItem));
        cmd.Parameters.Add(ParametroLongo("@usuario", usuario));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> CancelarLancamentoSeVazioAsync(
        NpgsqlConnection c, NpgsqlTransaction t, long codigoLancamento, long usuario, CancellationToken ct)
    {
        const string sql = """
            UPDATE entrada_produto_lancamento l
               SET status_lancamento = 'CANCELADO',
                   situacao_entrada_produto_lancamento = false,
                   entrada_produto_lancamento_atualizado_por = @usuario
             WHERE l.codigo_entrada_produto_lancamento = @codigo
               AND l.status_lancamento <> 'CANCELADO'
               AND NOT EXISTS (
                   SELECT 1
                     FROM entrada_produto_item i
                     JOIN entrada_produto_pesagem p
                       ON p.codigo_entrada_produto_item = i.codigo_entrada_produto_item
                    WHERE i.codigo_entrada_produto_lancamento = l.codigo_entrada_produto_lancamento
                      AND p.situacao_entrada_produto_pesagem = true
                      AND p.status_pesagem = 'VALIDA');
            """;
        await using NpgsqlCommand cmd = new(sql, c, t);
        cmd.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
        cmd.Parameters.Add(ParametroLongo("@usuario", usuario));
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    private static async Task RollbackSeguroAsync(NpgsqlTransaction t, CancellationToken ct)
    {
        try { await t.RollbackAsync(ct); } catch { /* provedor pode já ter descartado a transação */ }
    }
}
