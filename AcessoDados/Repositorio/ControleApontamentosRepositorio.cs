using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Processo;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistência do Controle de Apontamentos. NÃO hardcodeia schema: usa nomes de tabela nus,
/// resolvidos pelo search_path da conexão, como os demais repositories.
///
/// Regras estruturais desta camada:
///  - alteração de estado do apontamento e o evento de auditoria correspondente ocorrem na MESMA transação;
///  - conflito de concorrência (unique_violation 23505) vira RESULTADO funcional (null/false), nunca exceção;
///  - horários vêm do BANCO (RETURNING iniciado_em/terminado_em), nunca de DateTime.Now do cliente.
/// </summary>
public sealed class ControleApontamentosRepositorio : RepositorioBase, IControleApontamentosRepositorio
{
    private const string SqlStateUniqueViolation = "23505";

    private const string ColunasApontamento = """
        codigo_apontamento, numero_ordem, item_ordem, produto, sequencia, operacao, suboperacao,
        descricao_operacao, centro_trabalho, tipo_processo, tela_destino, status,
        usuario_inicio, estacao_inicio, iniciado_em, codigo_barras_inicio,
        usuario_termino, estacao_termino, terminado_em, codigo_barras_termino,
        correlation_id, idempotency_key,
        resultado_operacional, codigo_registro_processo, concluido_operacional_em, mensagem_resultado_operacional
        """;

    public ControleApontamentosRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    public async Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT to_regclass('operacao_producao_configuracao') IS NOT NULL
               AND to_regclass('operacao_producao_apontamento') IS NOT NULL
               AND to_regclass('operacao_producao_evento') IS NOT NULL;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        object? valor = await comando.ExecuteScalarAsync(cancellationToken);
        return valor is bool disponivel && disponivel;
    }

    // ---------------- Configuração ----------------

    public async Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoOperacaoAsync(
        string centro,
        string tipoOrdem,
        string sequencia,
        string operacao,
        string suboperacao,
        string centroTrabalho,
        CancellationToken cancellationToken = default)
    {
        // Curingas ('') continuam valendo, mas a ESPECIFICIDADE é calculada e o empate NÃO é escondido:
        // trazemos as duas melhores e, se empatarem, devolvemos ambiguidade.
        const string sql = """
            SELECT codigo_configuracao, centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap,
                   centro_trabalho, tipo_processo, tela_destino, exige_operacao_anterior, ativo,
                   (centro <> '')::int + (tipo_ordem <> '')::int + (sequencia_sap <> '')::int
                 + (suboperacao_sap <> '')::int + (centro_trabalho <> '')::int AS especificidade
              FROM operacao_producao_configuracao
             WHERE ativo = true
               AND nullif(ltrim(operacao_sap, '0'), '') = nullif(ltrim(@operacao_sap, '0'), '')
               AND (centro = @centro OR centro = '')
               AND (tipo_ordem = @tipo_ordem OR tipo_ordem = '')
               AND (sequencia_sap = @sequencia_sap OR sequencia_sap = '')
               AND (suboperacao_sap = @suboperacao_sap OR suboperacao_sap = '')
               AND (centro_trabalho = @centro_trabalho OR centro_trabalho = '')
             ORDER BY especificidade DESC, codigo_configuracao;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@centro", centro));
        comando.Parameters.Add(ParametroTexto("@tipo_ordem", tipoOrdem));
        comando.Parameters.Add(ParametroTexto("@sequencia_sap", sequencia));
        comando.Parameters.Add(ParametroTexto("@operacao_sap", operacao));
        comando.Parameters.Add(ParametroTexto("@suboperacao_sap", suboperacao));
        comando.Parameters.Add(ParametroTexto("@centro_trabalho", centroTrabalho));

        List<(ConfiguracaoOperacaoProcesso Config, int Especificidade)> candidatas = [];
        await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                candidatas.Add((LerConfiguracao(leitor), leitor.GetInt32(11)));
            }
        }

        if (candidatas.Count == 0)
        {
            return new ResultadoConfiguracaoOperacao(null, false, 0);
        }

        int melhor = candidatas[0].Especificidade;
        int empatadas = candidatas.Count(c => c.Especificidade == melhor);
        return empatadas > 1
            ? new ResultadoConfiguracaoOperacao(null, true, empatadas)
            : new ResultadoConfiguracaoOperacao(candidatas[0].Config, false, 1);
    }

    public async Task<IReadOnlyList<ConfiguracaoOperacaoProcesso>> ListarConfiguracoesAtivasAsync(
        string centro,
        string tipoOrdem,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_configuracao, centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap,
                   centro_trabalho, tipo_processo, tela_destino, exige_operacao_anterior, ativo
              FROM operacao_producao_configuracao
             WHERE ativo = true
               AND (centro = @centro OR centro = '')
               AND (tipo_ordem = @tipo_ordem OR tipo_ordem = '')
             ORDER BY codigo_configuracao;
            """;

        List<ConfiguracaoOperacaoProcesso> configuracoes = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@centro", centro));
        comando.Parameters.Add(ParametroTexto("@tipo_ordem", tipoOrdem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            configuracoes.Add(LerConfiguracao(leitor));
        }

        return configuracoes;
    }

    private static ConfiguracaoOperacaoProcesso LerConfiguracao(NpgsqlDataReader leitor)
        => new()
        {
            CodigoConfiguracao = leitor.GetInt64(0),
            Centro = leitor.GetString(1),
            TipoOrdem = leitor.GetString(2),
            SequenciaSap = leitor.GetString(3),
            OperacaoSap = leitor.GetString(4),
            SuboperacaoSap = leitor.GetString(5),
            CentroTrabalho = leitor.IsDBNull(6) ? string.Empty : leitor.GetString(6),
            TipoProcesso = leitor.GetString(7),
            TelaDestino = leitor.IsDBNull(8) ? string.Empty : leitor.GetString(8),
            ExigeOperacaoAnterior = leitor.GetBoolean(9),
            Ativo = leitor.GetBoolean(10)
        };

    // ---------------- Consultas de apontamento ----------------

    public async Task<OperacaoProducaoApontamento?> ObterApontamentoAtivoAsync(
        string numeroOrdem,
        string sequencia,
        string operacao,
        string suboperacao,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
            SELECT {ColunasApontamento}
              FROM operacao_producao_apontamento
             WHERE numero_ordem = @numero_ordem
               AND sequencia = @sequencia
               AND operacao = @operacao
               AND coalesce(suboperacao, '') = coalesce(@suboperacao, '')
               AND status IN ('EM_ANDAMENTO', 'AGUARDANDO_FINALIZACAO')
             ORDER BY codigo_apontamento DESC
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_ordem", numeroOrdem));
        comando.Parameters.Add(ParametroTexto("@sequencia", sequencia));
        comando.Parameters.Add(ParametroTexto("@operacao", operacao));
        comando.Parameters.Add(ParametroTexto("@suboperacao", suboperacao));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? LerApontamento(leitor) : null;
    }

    public async Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosAtivosPorOperacaoAsync(
        string numeroOrdem,
        string operacao,
        CancellationToken cancellationToken = default)
    {
        // Sem sequência/suboperação: é o caminho do TÉRMINO, que não depende de nova consulta ao SAP.
        string sql = $"""
            SELECT {ColunasApontamento}
              FROM operacao_producao_apontamento
             WHERE numero_ordem = @numero_ordem
               AND operacao = @operacao
               AND status IN ('EM_ANDAMENTO', 'AGUARDANDO_FINALIZACAO')
             ORDER BY codigo_apontamento;
            """;

        return await ListarApontamentosAsync(sql, comando =>
        {
            comando.Parameters.Add(ParametroTexto("@numero_ordem", numeroOrdem));
            comando.Parameters.Add(ParametroTexto("@operacao", operacao));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosDaOrdemAsync(
        string numeroOrdem,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
            SELECT {ColunasApontamento}
              FROM operacao_producao_apontamento
             WHERE numero_ordem = @numero_ordem
             ORDER BY sequencia, operacao, suboperacao, codigo_apontamento;
            """;

        return await ListarApontamentosAsync(
            sql, comando => comando.Parameters.Add(ParametroTexto("@numero_ordem", numeroOrdem)), cancellationToken);
    }

    private async Task<IReadOnlyList<OperacaoProducaoApontamento>> ListarApontamentosAsync(
        string sql, Action<NpgsqlCommand> preencher, CancellationToken cancellationToken)
    {
        List<OperacaoProducaoApontamento> apontamentos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        preencher(comando);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            apontamentos.Add(LerApontamento(leitor));
        }

        return apontamentos;
    }

    private static OperacaoProducaoApontamento LerApontamento(NpgsqlDataReader leitor)
        => new()
        {
            CodigoApontamento = leitor.GetInt64(0),
            NumeroOrdem = leitor.GetString(1),
            ItemOrdem = TextoOuVazio(leitor, 2),
            Produto = TextoOuVazio(leitor, 3),
            Sequencia = TextoOuVazio(leitor, 4),
            Operacao = leitor.GetString(5),
            Suboperacao = TextoOuVazio(leitor, 6),
            DescricaoOperacao = TextoOuVazio(leitor, 7),
            CentroTrabalho = TextoOuVazio(leitor, 8),
            TipoProcesso = TextoOuVazio(leitor, 9),
            TelaDestino = TextoOuVazio(leitor, 10),
            Status = leitor.GetString(11),
            UsuarioInicio = TextoOuVazio(leitor, 12),
            EstacaoInicio = TextoOuVazio(leitor, 13),
            IniciadoEm = leitor.IsDBNull(14) ? null : leitor.GetFieldValue<DateTimeOffset>(14),
            CodigoBarrasInicio = TextoOuVazio(leitor, 15),
            UsuarioTermino = TextoOuVazio(leitor, 16),
            EstacaoTermino = TextoOuVazio(leitor, 17),
            TerminadoEm = leitor.IsDBNull(18) ? null : leitor.GetFieldValue<DateTimeOffset>(18),
            CodigoBarrasTermino = TextoOuVazio(leitor, 19),
            CorrelationId = TextoOuVazio(leitor, 20),
            IdempotencyKey = TextoOuVazio(leitor, 21),
            ResultadoOperacional = TextoOuVazio(leitor, 22),
            CodigoRegistroProcesso = leitor.IsDBNull(23) ? null : leitor.GetInt64(23),
            ConcluidoOperacionalEm = leitor.IsDBNull(24) ? null : leitor.GetFieldValue<DateTimeOffset>(24),
            MensagemResultadoOperacional = TextoOuVazio(leitor, 25)
        };

    private static string TextoOuVazio(NpgsqlDataReader leitor, int indice)
        => leitor.IsDBNull(indice) ? string.Empty : leitor.GetString(indice);

    public async Task<bool> CodigoJaUtilizadoAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM operacao_producao_apontamento
                 WHERE idempotency_key = @chave OR idempotency_key_termino = @chave
            );
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@chave", idempotencyKey));
        object? valor = await comando.ExecuteScalarAsync(cancellationToken);
        return valor is bool existe && existe;
    }

    // ---------------- Transições de estado (estado + evento na MESMA transação) ----------------

    public async Task<OperacaoProducaoApontamento?> TentarIniciarApontamentoAsync(
        OperacaoProducaoApontamento apontamento,
        CodigoBarrasOperacao codigo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecutarEmTransacaoAuditavelAsync<OperacaoProducaoApontamento?>(async (conexao, transacao) =>
            {
                // ON CONFLICT DO NOTHING resolve a corrida real: quem perder recebe 0 linhas (não exceção).
                // O WHERE NOT EXISTS cobre o índice parcial de "ativo por operação"; o ON CONFLICT cobre a
                // unicidade da idempotency_key. Ambos os índices são a defesa final contra a corrida.
                string sql = $"""
                    INSERT INTO operacao_producao_apontamento
                    (
                        numero_ordem, item_ordem, produto, sequencia, operacao, suboperacao, descricao_operacao,
                        centro_trabalho, tipo_processo, tela_destino, status, usuario_inicio, estacao_inicio,
                        iniciado_em, codigo_barras_inicio, correlation_id, idempotency_key, criado_em, atualizado_em
                    )
                    SELECT
                        @numero_ordem, @item_ordem, @produto, @sequencia, @operacao, @suboperacao, @descricao_operacao,
                        @centro_trabalho, @tipo_processo, @tela_destino, 'EM_ANDAMENTO', @usuario_inicio, @estacao_inicio,
                        now(), @codigo_barras_inicio, @correlation_id, @idempotency_key, now(), now()
                    WHERE NOT EXISTS (
                        SELECT 1 FROM operacao_producao_apontamento
                         WHERE numero_ordem = @numero_ordem
                           AND sequencia = @sequencia
                           AND operacao = @operacao
                           AND coalesce(suboperacao, '') = coalesce(@suboperacao, '')
                           AND status IN ('EM_ANDAMENTO', 'AGUARDANDO_FINALIZACAO')
                    )
                    ON CONFLICT DO NOTHING
                    RETURNING {ColunasApontamento};
                    """;

                await using NpgsqlCommand comando = new(sql, conexao, transacao);
                comando.Parameters.Add(ParametroTexto("@numero_ordem", apontamento.NumeroOrdem));
                comando.Parameters.Add(ParametroTexto("@item_ordem", apontamento.ItemOrdem));
                comando.Parameters.Add(ParametroTexto("@produto", apontamento.Produto));
                comando.Parameters.Add(ParametroTexto("@sequencia", apontamento.Sequencia));
                comando.Parameters.Add(ParametroTexto("@operacao", apontamento.Operacao));
                comando.Parameters.Add(ParametroTexto("@suboperacao", apontamento.Suboperacao));
                comando.Parameters.Add(ParametroTexto("@descricao_operacao", apontamento.DescricaoOperacao));
                comando.Parameters.Add(ParametroTexto("@centro_trabalho", apontamento.CentroTrabalho));
                comando.Parameters.Add(ParametroTexto("@tipo_processo", apontamento.TipoProcesso));
                comando.Parameters.Add(ParametroTexto("@tela_destino", apontamento.TelaDestino));
                comando.Parameters.Add(ParametroTexto("@usuario_inicio", apontamento.UsuarioInicio));
                comando.Parameters.Add(ParametroTexto("@estacao_inicio", apontamento.EstacaoInicio));
                comando.Parameters.Add(ParametroTexto("@codigo_barras_inicio", apontamento.CodigoBarrasInicio));
                comando.Parameters.Add(ParametroTexto("@correlation_id", apontamento.CorrelationId));
                comando.Parameters.Add(ParametroTexto("@idempotency_key", apontamento.IdempotencyKey));

                OperacaoProducaoApontamento? criado = null;
                await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken))
                {
                    if (await leitor.ReadAsync(cancellationToken))
                    {
                        criado = LerApontamento(leitor);
                    }
                }

                if (criado is null)
                {
                    return null; // perdeu a corrida: conflito funcional
                }

                await InserirEventoAsync(
                    conexao, transacao, criado.CodigoApontamento, codigo,
                    apontamento.UsuarioInicio, apontamento.EstacaoInicio,
                    StatusCalculadoOperacao.Liberada, StatusApontamentoOperacao.EmAndamento,
                    "SUCESSO_INICIO", "Início registrado.", apontamento.CorrelationId, cancellationToken);

                return criado;
            }, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == SqlStateUniqueViolation)
        {
            // Corrida decidida pelo índice único: conflito FUNCIONAL, nunca erro de suporte.
            return null;
        }
    }

    public async Task<bool> TentarMarcarAguardandoFinalizacaoAsync(
        long codigoApontamento,
        ResultadoExecucaoProcesso resultado,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigo,
        CancellationToken cancellationToken = default)
        => await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            // Só EM_ANDAMENTO transiciona. Zero linhas = apontamento inexistente ou estado incompatível.
            const string sql = """
                UPDATE operacao_producao_apontamento
                   SET status = 'AGUARDANDO_FINALIZACAO',
                       resultado_operacional = @resultado_operacional,
                       codigo_registro_processo = @codigo_registro_processo,
                       concluido_operacional_em = now(),
                       mensagem_resultado_operacional = @mensagem_resultado_operacional,
                       atualizado_em = now()
                 WHERE codigo_apontamento = @codigo
                   AND status = 'EM_ANDAMENTO';
                """;

            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo", codigoApontamento));
            comando.Parameters.Add(ParametroTexto("@resultado_operacional", resultado.Resultado.ToString()));
            comando.Parameters.Add(ParametroLongoNulo("@codigo_registro_processo", resultado.CodigoRegistroProcesso));
            comando.Parameters.Add(ParametroTexto("@mensagem_resultado_operacional", resultado.Mensagem));

            if (await comando.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return false;
            }

            await InserirEventoAsync(
                conexao, transacao, codigoApontamento, codigo, usuario, estacao,
                StatusApontamentoOperacao.EmAndamento, StatusApontamentoOperacao.AguardandoFinalizacao,
                $"CONCLUSAO_OPERACIONAL_{resultado.Resultado}",
                $"Atividade concluída. Registro do processo: {resultado.CodigoRegistroProcesso?.ToString() ?? "(nenhum)"}. "
                + resultado.Mensagem,
                string.Empty, cancellationToken);

            return true;
        }, cancellationToken);

    public async Task<DateTimeOffset?> TentarConcluirApontamentoAsync(
        long codigoApontamento,
        string usuarioTermino,
        string estacaoTermino,
        CodigoBarrasOperacao codigoTermino,
        string idempotencyKeyTermino,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ExecutarEmTransacaoAuditavelAsync<DateTimeOffset?>(async (conexao, transacao) =>
            {
                // Transição ESTRITA: CONCLUIDA só pode vir de AGUARDANDO_FINALIZACAO. O repository NUNCA
                // conclui um EM_ANDAMENTO direto — a atividade operacional precisa ter sido concluída antes.
                const string sql = """
                    UPDATE operacao_producao_apontamento
                       SET status = 'CONCLUIDA',
                           usuario_termino = @usuario_termino,
                           estacao_termino = @estacao_termino,
                           terminado_em = now(),
                           codigo_barras_termino = @codigo_barras_termino,
                           idempotency_key_termino = @idempotency_key_termino,
                           atualizado_em = now()
                     WHERE codigo_apontamento = @codigo
                       AND status = 'AGUARDANDO_FINALIZACAO'
                    RETURNING terminado_em;
                    """;

                await using NpgsqlCommand comando = new(sql, conexao, transacao);
                comando.Parameters.Add(ParametroLongo("@codigo", codigoApontamento));
                comando.Parameters.Add(ParametroTexto("@usuario_termino", usuarioTermino));
                comando.Parameters.Add(ParametroTexto("@estacao_termino", estacaoTermino));
                comando.Parameters.Add(ParametroTexto("@codigo_barras_termino", codigoTermino.CodigoOriginal));
                comando.Parameters.Add(ParametroTexto("@idempotency_key_termino", idempotencyKeyTermino));

                object? terminadoEm = await comando.ExecuteScalarAsync(cancellationToken);
                if (terminadoEm is null or DBNull)
                {
                    return null; // já concluído por outra estação
                }

                await InserirEventoAsync(
                    conexao, transacao, codigoApontamento, codigoTermino, usuarioTermino, estacaoTermino,
                    StatusApontamentoOperacao.AguardandoFinalizacao, StatusApontamentoOperacao.Concluida,
                    "SUCESSO_TERMINO", "Término registrado.", string.Empty, cancellationToken);

                return (DateTimeOffset)terminadoEm;
            }, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == SqlStateUniqueViolation)
        {
            // Código de término já usado em outro apontamento: conflito funcional.
            return null;
        }
    }

    public async Task<bool> TentarCancelarApontamentoAsync(
        long codigoApontamento,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigo,
        string motivo,
        CancellationToken cancellationToken = default)
        => await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            const string sql = """
                UPDATE operacao_producao_apontamento
                   SET status = 'CANCELADA',
                       atualizado_em = now()
                 WHERE codigo_apontamento = @codigo
                   AND status = 'EM_ANDAMENTO';
                """;

            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo", codigoApontamento));
            if (await comando.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return false;
            }

            await InserirEventoAsync(
                conexao, transacao, codigoApontamento, codigo, usuario, estacao,
                StatusApontamentoOperacao.EmAndamento, StatusApontamentoOperacao.Cancelada,
                "CANCELAMENTO", motivo, string.Empty, cancellationToken);

            return true;
        }, cancellationToken);

    // ---------------- Auditoria ----------------

    public Task RegistrarEventoAsync(
        long? codigoApontamento,
        CodigoBarrasOperacao codigo,
        string usuario,
        string estacao,
        string statusAnterior,
        string statusNovo,
        string resultado,
        string mensagem,
        string correlationId,
        CancellationToken cancellationToken = default)
        => ExecutarEmTransacaoAuditavelAsync<int>(async (conexao, transacao) =>
        {
            await InserirEventoAsync(
                conexao, transacao, codigoApontamento, codigo, usuario, estacao,
                statusAnterior, statusNovo, resultado, mensagem, correlationId, cancellationToken);
            return 1;
        }, cancellationToken);

    /// <summary>INSERT do evento reaproveitável dentro de uma transação já aberta (estado + evento atômicos).</summary>
    private static async Task InserirEventoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long? codigoApontamento,
        CodigoBarrasOperacao codigo,
        string usuario,
        string estacao,
        string statusAnterior,
        string statusNovo,
        string resultado,
        string mensagem,
        string correlationId,
        CancellationToken cancellationToken)
    {
        // O código ORIGINAL é gravado como lido — nunca o regenerado.
        const string sql = """
            INSERT INTO operacao_producao_evento
            (
                codigo_apontamento, codigo_original, formato_codigo, ordem_producao, operacao,
                codigo_evento, usuario, estacao, ocorrido_em, status_anterior, status_novo,
                resultado, mensagem, correlation_id
            )
            VALUES
            (
                @codigo_apontamento, @codigo_original, @formato_codigo, @ordem_producao, @operacao,
                @codigo_evento, @usuario, @estacao, now(), @status_anterior, @status_novo,
                @resultado, @mensagem, @correlation_id
            );
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongoNulo("@codigo_apontamento", codigoApontamento));
        comando.Parameters.Add(ParametroTexto("@codigo_original", codigo.CodigoOriginal));
        comando.Parameters.Add(ParametroTexto("@formato_codigo", codigo.FormatoVersao));
        comando.Parameters.Add(ParametroTexto("@ordem_producao", codigo.OrdemProducao));
        comando.Parameters.Add(ParametroTexto("@operacao", codigo.Operacao));
        comando.Parameters.Add(ParametroTexto("@codigo_evento", codigo.CodigoEvento));
        comando.Parameters.Add(ParametroTexto("@usuario", usuario));
        comando.Parameters.Add(ParametroTexto("@estacao", estacao));
        comando.Parameters.Add(ParametroTexto("@status_anterior", statusAnterior));
        comando.Parameters.Add(ParametroTexto("@status_novo", statusNovo));
        comando.Parameters.Add(ParametroTexto("@resultado", resultado));
        comando.Parameters.Add(ParametroTexto("@mensagem", mensagem));
        comando.Parameters.Add(ParametroTexto("@correlation_id", correlationId));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }
}
