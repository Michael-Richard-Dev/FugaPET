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
        resultado_operacional, codigo_registro_processo, concluido_operacional_em, mensagem_resultado_operacional,
        codigo_perfil_resultado
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

    // GATE 093D — rota funcional por Plant + WorkCenter (centro + centro_trabalho). Operation/Sequence NÃO
    // participam da decisão da rota; o WorkCenter é usado LITERALMENTE (sem transformação). 0 = não configurado,
    // 1 = rota, >1 = ambígua (fail-closed).
    public async Task<ResultadoConfiguracaoOperacao> ObterConfiguracaoRotaPorWorkCenterAsync(
        string centro,
        string centroTrabalho,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_configuracao, centro, tipo_ordem, sequencia_sap, operacao_sap, suboperacao_sap,
                   centro_trabalho, tipo_processo, tela_destino, exige_operacao_anterior, ativo
              FROM operacao_producao_configuracao
             WHERE ativo = true
               AND centro = @centro
               AND centro_trabalho = @centro_trabalho
             ORDER BY codigo_configuracao;
            """;

        List<ConfiguracaoOperacaoProcesso> candidatas = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@centro", centro));
        comando.Parameters.Add(ParametroTexto("@centro_trabalho", centroTrabalho));
        await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                candidatas.Add(LerConfiguracao(leitor));
            }
        }

        return candidatas.Count switch
        {
            0 => new ResultadoConfiguracaoOperacao(null, false, 0),
            1 => new ResultadoConfiguracaoOperacao(candidatas[0], false, 1),
            _ => new ResultadoConfiguracaoOperacao(null, true, candidatas.Count)
        };
    }

    // GATE 093D — perfil de resultado normalizado, exclusivamente de operacao_resultado_perfil, por
    // (codigo_configuracao_rota + ordem_ocorrencia_workcenter). NUNCA lê
    // operacao_producao_configuracao.codigo_perfil_resultado. 0 = ausente, 1 = resolvido, >1 = ambíguo.
    public async Task<ResultadoPerfilResultado> ObterPerfilResultadoAsync(
        long codigoConfiguracaoRota,
        int ordemOcorrenciaWorkCenter,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_perfil_resultado
              FROM operacao_resultado_perfil
             WHERE ativo = true
               AND codigo_configuracao_rota = @codigo_configuracao_rota
               AND ordem_ocorrencia_workcenter = @ordem_ocorrencia_workcenter
             ORDER BY codigo_perfil_resultado;
            """;

        List<long> perfis = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_configuracao_rota", codigoConfiguracaoRota));
        comando.Parameters.Add(ParametroInteiro("@ordem_ocorrencia_workcenter", ordemOcorrenciaWorkCenter));
        await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken))
        {
            while (await leitor.ReadAsync(cancellationToken))
            {
                perfis.Add(leitor.GetInt64(0));
            }
        }

        return perfis.Count switch
        {
            0 => new ResultadoPerfilResultado(null, false, 0),
            1 => new ResultadoPerfilResultado(perfis[0], false, 1),
            _ => new ResultadoPerfilResultado(null, true, perfis.Count)
        };
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
            Ativo = leitor.GetBoolean(10),
            // GATE 089A (drift fix): a coluna codigo_perfil_resultado NÃO existe em operacao_producao_configuracao
            // (provado em 087A/087C — causaria SQLSTATE 42703). O perfil de resultado deixa de ser lido daqui e
            // passa a ser resolvido pela fonte normalizada (operacao_resultado_perfil); enquanto esse lookup não
            // existe no runtime, permanece null → RESULTADO_APONTAMENTO opera FAIL-CLOSED. Sem fallback/default.
            CodigoPerfilResultado = null
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
            MensagemResultadoOperacional = TextoOuVazio(leitor, 25),
            // GATE 093D: snapshot do perfil de resultado (054). Null quando não aplicável/legado pré-054.
            CodigoPerfilResultado = leitor.IsDBNull(26) ? null : leitor.GetInt64(26)
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
                        centro_trabalho, tipo_processo, tela_destino, codigo_perfil_resultado, status, usuario_inicio, estacao_inicio,
                        iniciado_em, codigo_barras_inicio, correlation_id, idempotency_key, criado_em, atualizado_em
                    )
                    SELECT
                        @numero_ordem, @item_ordem, @produto, @sequencia, @operacao, @suboperacao, @descricao_operacao,
                        @centro_trabalho, @tipo_processo, @tela_destino, @codigo_perfil_resultado, 'EM_ANDAMENTO', @usuario_inicio, @estacao_inicio,
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
                comando.Parameters.Add(ParametroLongoNulo("@codigo_perfil_resultado", apontamento.CodigoPerfilResultado));
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

    public async Task<bool> TentarRecuperarConsumoConfirmadoAsync(
        ContextoApontamentoProcesso contexto,
        long codigoLancamento,
        string materialEsperado,
        string reservaEsperada,
        string itemReservaEsperado,
        string loteEsperado,
        ResultadoExecucaoProcesso resultado,
        string usuario,
        string estacao,
        CodigoBarrasOperacao codigoInicio,
        CancellationToken cancellationToken = default)
        => await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            const string sqlApontamento = """
                SELECT codigo_apontamento
                  FROM operacao_producao_apontamento
                 WHERE codigo_apontamento = @codigo_apontamento
                   AND numero_ordem = @numero_ordem
                   AND sequencia = @sequencia
                   AND operacao = @operacao
                   AND coalesce(suboperacao, '') = @suboperacao
                   AND tipo_processo = 'CONSUMO_MATERIA_PRIMA'
                   AND status = 'EM_ANDAMENTO'
                   AND resultado_operacional IS NULL
                   AND codigo_registro_processo IS NULL
                   AND concluido_operacional_em IS NULL
                   AND NOT EXISTS (
                       SELECT 1
                         FROM operacao_producao_apontamento_processo processo
                        WHERE processo.codigo_apontamento = @codigo_apontamento
                   )
                 FOR UPDATE;
                """;

            await using (NpgsqlCommand comando = new(sqlApontamento, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo_apontamento", contexto.CodigoApontamento));
                comando.Parameters.Add(ParametroTexto("@numero_ordem", contexto.NumeroOrdem));
                comando.Parameters.Add(ParametroTexto("@sequencia", contexto.Sequencia));
                comando.Parameters.Add(ParametroTexto("@operacao", contexto.Operacao));
                comando.Parameters.Add(ParametroTexto("@suboperacao", contexto.Suboperacao));
                if (await comando.ExecuteScalarAsync(cancellationToken) is null)
                {
                    return false;
                }
            }

            const string sqlLancamento = """
                SELECT codigo_consumo_material_lancamento
                  FROM consumo_material_lancamento
                 WHERE codigo_consumo_material_lancamento = @codigo_lancamento
                   AND numero_ordem = @numero_ordem
                   AND status_lancamento = 'CONFIRMADO_SAP'
                   AND nullif(btrim(documento_material_sap), '') IS NOT NULL
                   AND nullif(btrim(exercicio_documento_material_sap), '') IS NOT NULL
                 FOR UPDATE;
                """;

            await using (NpgsqlCommand comando = new(sqlLancamento, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@numero_ordem", contexto.NumeroOrdem));
                if (await comando.ExecuteScalarAsync(cancellationToken) is null)
                {
                    return false;
                }
            }

            const string sqlItens = """
                SELECT COUNT(*),
                       COUNT(*) FILTER (
                           WHERE btrim(codigo_material) = @material
                             AND btrim(numero_reserva) = @reserva
                             AND btrim(item_reserva) = @item_reserva
                             AND btrim(lote) = @lote
                       )
                  FROM consumo_material_item
                 WHERE codigo_consumo_material_lancamento = @codigo_lancamento;
                """;

            await using (NpgsqlCommand comando = new(sqlItens, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@material", materialEsperado));
                comando.Parameters.Add(ParametroTexto("@reserva", reservaEsperada));
                comando.Parameters.Add(ParametroTexto("@item_reserva", itemReservaEsperado));
                comando.Parameters.Add(ParametroTexto("@lote", loteEsperado));
                await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
                if (!await leitor.ReadAsync(cancellationToken)
                    || leitor.GetInt64(0) != 1
                    || leitor.GetInt64(1) != 1)
                {
                    return false;
                }
            }

            const string sqlVinculo = """
                INSERT INTO operacao_producao_apontamento_processo
                    (codigo_apontamento, tipo_processo, codigo_registro_processo)
                VALUES
                    (@codigo_apontamento, 'CONSUMO_MATERIA_PRIMA', @codigo_lancamento);
                """;

            await using (NpgsqlCommand comando = new(sqlVinculo, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo_apontamento", contexto.CodigoApontamento));
                comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                if (await comando.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("Vínculo canônico não foi criado.");
                }
            }

            const string sqlAtualizacao = """
                UPDATE operacao_producao_apontamento
                   SET status = 'AGUARDANDO_FINALIZACAO',
                       resultado_operacional = @resultado_operacional,
                       codigo_registro_processo = @codigo_lancamento,
                       concluido_operacional_em = now(),
                       mensagem_resultado_operacional = @mensagem,
                       atualizado_em = now()
                 WHERE codigo_apontamento = @codigo_apontamento
                   AND status = 'EM_ANDAMENTO'
                   AND resultado_operacional IS NULL
                   AND codigo_registro_processo IS NULL
                   AND concluido_operacional_em IS NULL;
                """;

            await using (NpgsqlCommand comando = new(sqlAtualizacao, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo_apontamento", contexto.CodigoApontamento));
                comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@resultado_operacional", resultado.Resultado.ToString()));
                comando.Parameters.Add(ParametroTexto("@mensagem", resultado.Mensagem));
                if (await comando.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new InvalidOperationException("Apontamento deixou de estar elegível para recuperação.");
                }
            }

            await InserirEventoAsync(
                conexao, transacao, contexto.CodigoApontamento, codigoInicio, usuario, estacao,
                StatusApontamentoOperacao.EmAndamento, StatusApontamentoOperacao.AguardandoFinalizacao,
                $"CONCLUSAO_OPERACIONAL_{resultado.Resultado}",
                $"Recovery local do lançamento confirmado {codigoLancamento}. {resultado.Mensagem}",
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

                // GATE 099B: terminado_em é timestamptz. Pelo caminho object de ExecuteScalar o provider Npgsql
                // devolve um DateTime (UTC) encaixotado; o unboxing direto (DateTimeOffset)object lançava
                // InvalidCastException, abortando a transação e deixando o término sem persistir. Lê de forma
                // type-safe com GetFieldValue<DateTimeOffset> (mesmo padrão do mapeador desta classe), sem
                // conversão dependente de locale. 0 linhas / DBNull ⇒ null (fail-closed: já concluído por outra estação).
                DateTimeOffset terminadoEm;
                await using (NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken))
                {
                    if (!await leitor.ReadAsync(cancellationToken) || await leitor.IsDBNullAsync(0, cancellationToken))
                    {
                        return null; // já concluído por outra estação
                    }

                    terminadoEm = leitor.GetFieldValue<DateTimeOffset>(0);
                }

                await InserirEventoAsync(
                    conexao, transacao, codigoApontamento, codigoTermino, usuarioTermino, estacaoTermino,
                    StatusApontamentoOperacao.AguardandoFinalizacao, StatusApontamentoOperacao.Concluida,
                    "SUCESSO_TERMINO", "Término registrado.", string.Empty, cancellationToken);

                return terminadoEm;
            }, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == SqlStateUniqueViolation)
        {
            // Código de término já usado em outro apontamento: conflito funcional.
            return null;
        }
    }

    public async Task RegistrarVinculoProcessoAsync(
        long codigoApontamento,
        string tipoProcesso,
        long codigoRegistroProcesso,
        CancellationToken cancellationToken = default)
        => await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            const string sqlVinculo = """
                INSERT INTO operacao_producao_apontamento_processo
                (codigo_apontamento, tipo_processo, codigo_registro_processo, criado_em, atualizado_em)
                VALUES
                (
                    @codigo_apontamento, @tipo_processo, @codigo_registro_processo, now(), now()
                )
                ON CONFLICT (codigo_apontamento, tipo_processo, codigo_registro_processo) DO NOTHING;
                """;

            await using (NpgsqlCommand comandoVinculo = new(sqlVinculo, conexao, transacao))
            {
                comandoVinculo.Parameters.Add(ParametroLongo("@codigo_apontamento", codigoApontamento));
                comandoVinculo.Parameters.Add(ParametroTexto("@tipo_processo", tipoProcesso));
                comandoVinculo.Parameters.Add(ParametroLongo("@codigo_registro_processo", codigoRegistroProcesso));
                await comandoVinculo.ExecuteNonQueryAsync(cancellationToken);
            }

            const string sqlLegado = """
                UPDATE operacao_producao_apontamento
                   SET codigo_registro_processo = @codigo_registro_processo,
                       atualizado_em = now()
                 WHERE codigo_apontamento = @codigo_apontamento
                   AND codigo_registro_processo IS NULL;
                """;

            await using NpgsqlCommand comandoLegado = new(sqlLegado, conexao, transacao);
            comandoLegado.Parameters.Add(ParametroLongo("@codigo_apontamento", codigoApontamento));
            comandoLegado.Parameters.Add(ParametroLongo("@codigo_registro_processo", codigoRegistroProcesso));
            await comandoLegado.ExecuteNonQueryAsync(cancellationToken);
            return 1;
        }, cancellationToken);

    public async Task<IReadOnlyList<ApontamentoProcesso>> ListarProcessosVinculadosAsync(
        long codigoApontamento,
        CancellationToken cancellationToken = default)
    {
        string[] tiposConsumo =
        [
            TipoProcessoOperacao.ConsumoMateriaPrima,
            TipoProcessoOperacao.ConsumoQuimicos
        ];

        const string sql = """
            SELECT v.codigo_apontamento_processo,
                   v.codigo_apontamento,
                   v.tipo_processo,
                   v.codigo_registro_processo,
                   COALESCE(cml.status_lancamento, '') AS status_lancamento,
                   COALESCE(cml.documento_material_sap, '') AS documento_material_sap,
            COALESCE(cml.exercicio_documento_material_sap, '') AS exercicio_documento,
                   COALESCE(cmi.numero_reserva, '') AS reservation,
                   COALESCE(cmi.item_reserva, '') AS reservation_item
              FROM operacao_producao_apontamento_processo v
              LEFT JOIN consumo_material_lancamento cml
                ON v.tipo_processo = ANY(@tipos_consumo)
               AND cml.codigo_consumo_material_lancamento = v.codigo_registro_processo
              LEFT JOIN consumo_material_item cmi
                ON v.tipo_processo = ANY(@tipos_consumo)
               AND cmi.codigo_consumo_material_lancamento = cml.codigo_consumo_material_lancamento
             WHERE v.codigo_apontamento = @codigo
             ORDER BY v.codigo_apontamento_processo, cmi.codigo_consumo_material_item;
            """;

        List<ApontamentoProcesso> processos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigoApontamento));
        comando.Parameters.Add(new NpgsqlParameter<string[]>("@tipos_consumo", tiposConsumo));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            processos.Add(new ApontamentoProcesso
            {
                CodigoApontamentoProcesso = leitor.GetInt64(0),
                CodigoApontamento = leitor.GetInt64(1),
                TipoProcesso = leitor.GetString(2),
                CodigoRegistroProcesso = leitor.GetInt64(3),
                StatusLancamento = TextoOuVazio(leitor, 4),
                DocumentoMaterialSap = TextoOuVazio(leitor, 5),
                ExercicioMaterialSap = TextoOuVazio(leitor, 6),
                Reservation = TextoOuVazio(leitor, 7),
                ReservationItem = TextoOuVazio(leitor, 8)
            });
        }

        return processos;
    }

    public async Task<ResultadoDecisaoOperacionalConsumo> RegistrarZeroIntencionalAsync(
        ComponenteConsumoDecisaoOperacional decisao,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decisao);
        if (decisao.CodigoApontamento <= 0
            || string.IsNullOrWhiteSpace(decisao.NumeroReserva)
            || string.IsNullOrWhiteSpace(decisao.ItemReserva)
            || string.IsNullOrWhiteSpace(decisao.CodigoMaterial))
        {
            return ResultadoDecisaoOperacionalConsumo.DadosInvalidos("Dados insuficientes para registrar componente não consumido.");
        }

        try
        {
            int afetadas = await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
            {
                const string sql = """
                    INSERT INTO consumo_material_componente_decisao
                    (
                        codigo_apontamento, numero_reserva, item_reserva, codigo_material,
                        decisao_operacional, quantidade, unidade, usuario, estacao, decidido_em
                    )
                    VALUES
                    (
                        @codigo_apontamento, @numero_reserva, @item_reserva, @codigo_material,
                        'ZERO_INTENCIONAL', 0, @unidade, @usuario, @estacao, now()
                    )
                    ON CONFLICT (codigo_apontamento, numero_reserva, item_reserva) DO NOTHING;
                    """;

                await using NpgsqlCommand comando = new(sql, conexao, transacao);
                comando.Parameters.Add(ParametroLongo("@codigo_apontamento", decisao.CodigoApontamento));
                comando.Parameters.Add(ParametroTexto("@numero_reserva", decisao.NumeroReserva));
                comando.Parameters.Add(ParametroTexto("@item_reserva", decisao.ItemReserva));
                comando.Parameters.Add(ParametroTexto("@codigo_material", decisao.CodigoMaterial));
                comando.Parameters.Add(ParametroTexto("@unidade", decisao.Unidade));
                comando.Parameters.Add(ParametroTexto("@usuario", decisao.Usuario));
                comando.Parameters.Add(ParametroTexto("@estacao", decisao.Estacao));
                return await comando.ExecuteNonQueryAsync(cancellationToken);
            }, cancellationToken);

            return afetadas == 0
                ? ResultadoDecisaoOperacionalConsumo.JaExistente()
                : ResultadoDecisaoOperacionalConsumo.Registrada();
        }
        catch (PostgresException ex) when (ex.SqlState == SqlStateUniqueViolation)
        {
            return ResultadoDecisaoOperacionalConsumo.JaExistente();
        }
        catch (PostgresException)
        {
            return ResultadoDecisaoOperacionalConsumo.ErroPersistencia();
        }
    }

    public async Task<IReadOnlyList<ComponenteConsumoDecisaoOperacional>> ListarDecisoesZeroIntencionalAsync(
        long codigoApontamento,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_apontamento, numero_reserva, item_reserva, codigo_material,
                   decisao_operacional, quantidade, unidade, usuario, estacao
              FROM consumo_material_componente_decisao
             WHERE codigo_apontamento = @codigo_apontamento
               AND decisao_operacional = 'ZERO_INTENCIONAL'
             ORDER BY numero_reserva, item_reserva, codigo_material;
            """;

        List<ComponenteConsumoDecisaoOperacional> decisoes = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_apontamento", codigoApontamento));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            decisoes.Add(new ComponenteConsumoDecisaoOperacional
            {
                CodigoApontamento = leitor.GetInt64(0),
                NumeroReserva = TextoOuVazio(leitor, 1),
                ItemReserva = TextoOuVazio(leitor, 2),
                CodigoMaterial = TextoOuVazio(leitor, 3),
                DecisaoOperacional = TextoOuVazio(leitor, 4),
                Quantidade = leitor.GetDecimal(5),
                Unidade = TextoOuVazio(leitor, 6),
                Usuario = TextoOuVazio(leitor, 7),
                Estacao = TextoOuVazio(leitor, 8)
            });
        }

        return decisoes;
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
