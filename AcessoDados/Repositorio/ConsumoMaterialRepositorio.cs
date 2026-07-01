using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Consumo;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistencia LOCAL do consumo de materia-prima. Salva cabecalho + itens + pesagens em uma unica
/// transacao auditavel (rollback em qualquer erro). Status local PENDENTE_SAP — nao envia SAP.
/// </summary>
public sealed class ConsumoMaterialRepositorio : RepositorioBase, IConsumoMaterialRepositorio
{
    public ConsumoMaterialRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    public async Task<IReadOnlyList<ResumoConsumoMaterialLancamento>> ConsultarLancamentosAsync(
        ConsultaConsumoMaterialFiltro filtro,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT l.codigo_consumo_material_lancamento,
                   l.numero_ordem,
                   l.centro,
                   l.material_produzido,
                   l.status_lancamento,
                   COALESCE(SUM(i.quantidade_consumida_local), 0) AS quantidade_total_consumida_local,
                   l.unidade,
                   l.documento_material_sap,
                   l.exercicio_documento_material_sap,
                   l.consumo_material_lancamento_criado_em,
                   l.enviado_sap_em
              FROM consumo_material_lancamento l
              LEFT JOIN consumo_material_item i
                ON i.codigo_consumo_material_lancamento = l.codigo_consumo_material_lancamento
             WHERE (@numero_ordem IS NULL OR l.numero_ordem ILIKE @numero_ordem)
               AND (@status_lancamento IS NULL OR l.status_lancamento = @status_lancamento)
               AND (@criado_de IS NULL OR l.consumo_material_lancamento_criado_em >= @criado_de)
               AND (@criado_ate IS NULL OR l.consumo_material_lancamento_criado_em <= @criado_ate)
             GROUP BY l.codigo_consumo_material_lancamento,
                      l.numero_ordem,
                      l.centro,
                      l.material_produzido,
                      l.status_lancamento,
                      l.unidade,
                      l.documento_material_sap,
                      l.exercicio_documento_material_sap,
                      l.consumo_material_lancamento_criado_em,
                      l.enviado_sap_em
             ORDER BY l.consumo_material_lancamento_criado_em DESC, l.codigo_consumo_material_lancamento DESC
             LIMIT @limite;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(new NpgsqlParameter("@numero_ordem", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(filtro.NumeroOrdem) ? DBNull.Value : $"%{filtro.NumeroOrdem.Trim()}%"
        });
        comando.Parameters.Add(new NpgsqlParameter("@status_lancamento", NpgsqlDbType.Text)
        {
            Value = string.IsNullOrWhiteSpace(filtro.StatusLancamento) ? DBNull.Value : filtro.StatusLancamento.Trim()
        });
        comando.Parameters.Add(new NpgsqlParameter("@criado_de", NpgsqlDbType.TimestampTz)
        {
            Value = filtro.CriadoDeUtc.HasValue ? NormalizarUtc(filtro.CriadoDeUtc.Value) : DBNull.Value
        });
        comando.Parameters.Add(new NpgsqlParameter("@criado_ate", NpgsqlDbType.TimestampTz)
        {
            Value = filtro.CriadoAteUtc.HasValue ? NormalizarUtc(filtro.CriadoAteUtc.Value) : DBNull.Value
        });
        comando.Parameters.Add(ParametroInteiro("@limite", filtro.Limite));

        List<ResumoConsumoMaterialLancamento> lancamentos = [];
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            lancamentos.Add(new ResumoConsumoMaterialLancamento
            {
                CodigoLancamento = leitor.GetInt64(0),
                NumeroOrdem = leitor.GetString(1),
                Centro = leitor.IsDBNull(2) ? null : leitor.GetString(2),
                MaterialProduzido = leitor.IsDBNull(3) ? null : leitor.GetString(3),
                StatusLancamento = leitor.GetString(4),
                QuantidadeTotalConsumidaLocal = leitor.GetDecimal(5),
                Unidade = leitor.IsDBNull(6) ? null : leitor.GetString(6),
                DocumentoMaterialSap = leitor.IsDBNull(7) ? null : leitor.GetString(7),
                ExercicioDocumentoMaterialSap = leitor.IsDBNull(8) ? null : leitor.GetString(8),
                CriadoEmUtc = NormalizarUtc(leitor.GetDateTime(9)),
                EnviadoSapEmUtc = leitor.IsDBNull(10) ? null : NormalizarUtc(leitor.GetDateTime(10))
            });
        }

        return lancamentos;
    }

    public async Task<long> SalvarConsumoLocalAsync(
        ConsumoMaterialLancamento lancamento,
        CancellationToken cancellationToken = default)
    {
        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long codigoLancamento = await InserirLancamentoAsync(conexao, transacao, lancamento, cancellationToken);

            foreach (ConsumoMaterialItem item in lancamento.Itens)
            {
                long codigoItem = await InserirItemAsync(conexao, transacao, codigoLancamento, item, cancellationToken);

                foreach (ConsumoMaterialPesagem pesagem in item.Pesagens)
                {
                    await InserirPesagemAsync(conexao, transacao, codigoItem, pesagem, cancellationToken);
                }
            }

            return codigoLancamento;
        }, cancellationToken);
    }

    private static async Task<long> InserirLancamentoAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        ConsumoMaterialLancamento lancamento, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO consumo_material_lancamento
                (numero_ordem, centro, material_produzido, lote_ordem, quantidade_prevista,
                 unidade, status_lancamento, observacao, usuario_criacao)
            VALUES
                (@numero_ordem, @centro, @material_produzido, @lote_ordem, @quantidade_prevista,
                 @unidade, 'PENDENTE_SAP', @observacao, @usuario)
            RETURNING codigo_consumo_material_lancamento;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroTexto("@numero_ordem", lancamento.NumeroOrdem));
        comando.Parameters.Add(ParametroTexto("@centro", lancamento.Centro));
        comando.Parameters.Add(ParametroTexto("@material_produzido", lancamento.MaterialProduzido));
        comando.Parameters.Add(ParametroTexto("@lote_ordem", lancamento.LoteOrdem));
        comando.Parameters.Add(ParametroDecimalNulo("@quantidade_prevista", lancamento.QuantidadePrevista));
        comando.Parameters.Add(ParametroTexto("@unidade", lancamento.Unidade));
        comando.Parameters.Add(ParametroTexto("@observacao", lancamento.Observacao));
        comando.Parameters.Add(ParametroTexto("@usuario", lancamento.UsuarioCriacao));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo
            ? codigo
            : throw new InvalidOperationException("Nao foi possivel criar o lancamento de consumo.");
    }

    private static async Task<long> InserirItemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoLancamento, ConsumoMaterialItem item, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO consumo_material_item
                (codigo_consumo_material_lancamento, numero_ordem, codigo_material, descricao_material,
                 centro, deposito_consumo, numero_reserva, item_reserva, lote,
                 quantidade_prevista, quantidade_retirada_sap, quantidade_pendente_sap,
                 quantidade_consumida_local, unidade, tipo_movimento_sap, status_item)
            VALUES
                (@lancamento, @numero_ordem, @codigo_material, @descricao_material,
                 @centro, @deposito, @numero_reserva, @item_reserva, @lote,
                 @qtd_prevista, @qtd_retirada, @qtd_pendente,
                 @qtd_consumida_local, @unidade, @tipo_movimento, 'PENDENTE_SAP')
            RETURNING codigo_consumo_material_item;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroTexto("@numero_ordem", item.NumeroOrdem));
        comando.Parameters.Add(ParametroTexto("@codigo_material", item.CodigoMaterial));
        comando.Parameters.Add(ParametroTexto("@descricao_material", item.DescricaoMaterial));
        comando.Parameters.Add(ParametroTexto("@centro", item.Centro));
        comando.Parameters.Add(ParametroTexto("@deposito", item.DepositoConsumo));
        comando.Parameters.Add(ParametroTexto("@numero_reserva", item.NumeroReserva));
        comando.Parameters.Add(ParametroTexto("@item_reserva", item.ItemReserva));
        comando.Parameters.Add(ParametroTexto("@lote", item.Lote));
        comando.Parameters.Add(ParametroDecimalNulo("@qtd_prevista", item.QuantidadePrevista));
        comando.Parameters.Add(ParametroDecimalNulo("@qtd_retirada", item.QuantidadeRetiradaSap));
        comando.Parameters.Add(ParametroDecimalNulo("@qtd_pendente", item.QuantidadePendenteSap));
        comando.Parameters.Add(new NpgsqlParameter("@qtd_consumida_local", NpgsqlDbType.Numeric) { Value = item.QuantidadeConsumidaLocal });
        comando.Parameters.Add(ParametroTexto("@unidade", item.Unidade));
        comando.Parameters.Add(ParametroTexto("@tipo_movimento", string.IsNullOrWhiteSpace(item.TipoMovimentoSap) ? "261" : item.TipoMovimentoSap));

        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo
            ? codigo
            : throw new InvalidOperationException("Nao foi possivel criar o item de consumo.");
    }

    private static async Task InserirPesagemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoItem, ConsumoMaterialPesagem pesagem, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO consumo_material_pesagem
                (codigo_consumo_material_item, sequencia, peso_bruto_kg, peso_tara_kg, peso_liquido_kg,
                 unidade, origem, status_pesagem, pesado_em, usuario_criacao)
            VALUES
                (@item, @sequencia, @bruto, @tara, @liquido,
                 @unidade, @origem, @status, @pesado_em, @usuario);
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@item", codigoItem));
        comando.Parameters.Add(ParametroInteiro("@sequencia", pesagem.Sequencia));
        comando.Parameters.Add(new NpgsqlParameter("@bruto", NpgsqlDbType.Numeric) { Value = pesagem.PesoBrutoKg });
        comando.Parameters.Add(new NpgsqlParameter("@tara", NpgsqlDbType.Numeric) { Value = pesagem.PesoTaraKg });
        comando.Parameters.Add(new NpgsqlParameter("@liquido", NpgsqlDbType.Numeric) { Value = pesagem.PesoLiquidoKg });
        comando.Parameters.Add(ParametroTexto("@unidade", string.IsNullOrWhiteSpace(pesagem.Unidade) ? "KG" : pesagem.Unidade));
        comando.Parameters.Add(ParametroTexto("@origem", pesagem.Origem));
        comando.Parameters.Add(ParametroTexto("@status", string.IsNullOrWhiteSpace(pesagem.StatusPesagem)
            ? ConsumoMaterialPesagem.StatusRegistradaLocalmente
            : pesagem.StatusPesagem));
        comando.Parameters.Add(new NpgsqlParameter("@pesado_em", NpgsqlDbType.TimestampTz)
        {
            Value = NormalizarUtc(pesagem.PesadoEm)
        });
        comando.Parameters.Add(ParametroTexto("@usuario", pesagem.UsuarioCriacao));

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Normaliza para UTC antes de enviar a coluna timestamptz (Npgsql 9 rejeita DateTimeKind.Local
    /// em timestamptz). Local -> ToUniversalTime; Unspecified -> tratado como UTC (sem deslocar).
    /// </summary>
    public async Task<bool> TentarReservarEnvioSapAsync(
        long codigoLancamento,
        DateTime reservadoEmUtc,
        CancellationToken cancellationToken = default)
    {
        return await ExecutarEmTransacaoAuditavelAsync<bool>(async (conexao, transacao) =>
        {
            // CLAIM atomico: so reserva quem mover PENDENTE_SAP -> ENVIANDO_SAP (sem documento ainda).
            const string sqlLancamento = """
                UPDATE consumo_material_lancamento
                   SET status_lancamento = 'ENVIANDO_SAP'
                 WHERE codigo_consumo_material_lancamento = @codigo
                   AND status_lancamento = 'PENDENTE_SAP'
                   AND documento_material_sap IS NULL
                   AND exercicio_documento_material_sap IS NULL;
                """;
            int linhas;
            await using (NpgsqlCommand comando = new(sqlLancamento, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                linhas = await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            if (linhas != 1)
            {
                return false; // ja em envio / confirmado / nao pendente: nao reservou.
            }

            const string sqlItens = """
                UPDATE consumo_material_item
                   SET status_item = 'ENVIANDO_SAP'
                 WHERE codigo_consumo_material_lancamento = @codigo
                   AND status_item = 'PENDENTE_SAP';
                """;
            await using (NpgsqlCommand comando = new(sqlItens, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            return true;
        }, cancellationToken);
    }

    public async Task MarcarFalhaSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
    {
        await ExecutarEmTransacaoAuditavelAsync<int>(async (conexao, transacao) =>
        {
            const string sqlLancamento = """
                UPDATE consumo_material_lancamento
                   SET status_lancamento = 'FALHA_SAP'
                 WHERE codigo_consumo_material_lancamento = @codigo
                   AND status_lancamento = 'ENVIANDO_SAP';
                """;
            await using (NpgsqlCommand comando = new(sqlLancamento, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            const string sqlItens = """
                UPDATE consumo_material_item
                   SET status_item = 'FALHA_SAP'
                 WHERE codigo_consumo_material_lancamento = @codigo
                   AND status_item = 'ENVIANDO_SAP';
                """;
            await using (NpgsqlCommand comando = new(sqlItens, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            return 0;
        }, cancellationToken);
    }

    public async Task MarcarConsumoConfirmadoSapAsync(
        long codigoLancamento,
        string? documentoMaterialSap,
        string? exercicioDocumentoMaterialSap,
        DateTime enviadoSapEmUtc,
        CancellationToken cancellationToken = default)
    {
        await ExecutarEmTransacaoAuditavelAsync<int>(async (conexao, transacao) =>
        {
            // CONFIRMA somente quem estava ENVIANDO_SAP e ainda sem documento (claim valido).
            const string sqlLancamento = """
                UPDATE consumo_material_lancamento
                   SET status_lancamento = 'CONFIRMADO_SAP',
                       documento_material_sap = @documento,
                       exercicio_documento_material_sap = @exercicio,
                       enviado_sap_em = @enviado
                 WHERE codigo_consumo_material_lancamento = @codigo
                   AND status_lancamento = 'ENVIANDO_SAP'
                   AND documento_material_sap IS NULL
                   AND exercicio_documento_material_sap IS NULL;
                """;
            int linhas;
            await using (NpgsqlCommand comando = new(sqlLancamento, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@documento", documentoMaterialSap));
                comando.Parameters.Add(ParametroTexto("@exercicio", exercicioDocumentoMaterialSap));
                comando.Parameters.Add(new NpgsqlParameter("@enviado", NpgsqlDbType.TimestampTz)
                {
                    Value = NormalizarUtc(enviadoSapEmUtc)
                });
                linhas = await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            if (linhas != 1)
            {
                // Estado inesperado/concorrencia: o documento SAP pode ter sido criado. Erro controlado.
                throw new InvalidOperationException(
                    "Confirmacao local do consumo nao atualizou exatamente um lancamento ENVIANDO_SAP.");
            }

            const string sqlItens = """
                UPDATE consumo_material_item
                   SET status_item = 'CONFIRMADO_SAP'
                 WHERE codigo_consumo_material_lancamento = @codigo
                   AND status_item = 'ENVIANDO_SAP';
                """;
            await using (NpgsqlCommand comando = new(sqlItens, conexao, transacao))
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            // Pesagens NAO sao alteradas. atualizado_em e definido pelos triggers da baseline.
            return 0;
        }, cancellationToken);
    }

    internal static DateTime NormalizarUtc(DateTime valor)
        => valor.Kind switch
        {
            DateTimeKind.Utc => valor,
            DateTimeKind.Local => valor.ToUniversalTime(),
            _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
        };

    public async Task<ConsumoMaterialLancamento?> ObterPorCodigoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_consumo_material_lancamento, numero_ordem, centro, material_produzido,
                   lote_ordem, quantidade_prevista, unidade, status_lancamento, observacao, usuario_criacao,
                   documento_material_sap, exercicio_documento_material_sap
              FROM consumo_material_lancamento
             WHERE codigo_consumo_material_lancamento = @codigo;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);

        ConsumoMaterialLancamento? lancamento;
        await using (NpgsqlCommand comando = new(sql, conexao))
        {
            comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
            await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
            if (!await leitor.ReadAsync(cancellationToken))
            {
                return null;
            }

            lancamento = new ConsumoMaterialLancamento
            {
                Codigo = leitor.GetInt64(0),
                NumeroOrdem = leitor.GetString(1),
                Centro = leitor.IsDBNull(2) ? null : leitor.GetString(2),
                MaterialProduzido = leitor.IsDBNull(3) ? null : leitor.GetString(3),
                LoteOrdem = leitor.IsDBNull(4) ? null : leitor.GetString(4),
                QuantidadePrevista = leitor.IsDBNull(5) ? null : leitor.GetDecimal(5),
                Unidade = leitor.IsDBNull(6) ? null : leitor.GetString(6),
                StatusLancamento = leitor.GetString(7),
                Observacao = leitor.IsDBNull(8) ? null : leitor.GetString(8),
                UsuarioCriacao = leitor.IsDBNull(9) ? null : leitor.GetString(9),
                DocumentoMaterialSap = leitor.IsDBNull(10) ? null : leitor.GetString(10),
                ExercicioDocumentoMaterialSap = leitor.IsDBNull(11) ? null : leitor.GetString(11)
            };
        }

        lancamento.Itens = await LerItensAsync(conexao, codigoLancamento, cancellationToken);
        return lancamento;
    }

    private static async Task<IReadOnlyList<ConsumoMaterialItem>> LerItensAsync(
        NpgsqlConnection conexao, long codigoLancamento, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT codigo_consumo_material_item, numero_ordem, codigo_material, descricao_material,
                   centro, deposito_consumo, numero_reserva, item_reserva, lote,
                   quantidade_prevista, quantidade_retirada_sap, quantidade_pendente_sap,
                   quantidade_consumida_local, unidade, tipo_movimento_sap, status_item
              FROM consumo_material_item
             WHERE codigo_consumo_material_lancamento = @codigo
             ORDER BY codigo_consumo_material_item;
            """;

        List<ConsumoMaterialItem> itens = [];
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(new ConsumoMaterialItem
            {
                Codigo = leitor.GetInt64(0),
                NumeroOrdem = leitor.GetString(1),
                CodigoMaterial = leitor.GetString(2),
                DescricaoMaterial = leitor.IsDBNull(3) ? null : leitor.GetString(3),
                Centro = leitor.IsDBNull(4) ? null : leitor.GetString(4),
                DepositoConsumo = leitor.IsDBNull(5) ? null : leitor.GetString(5),
                NumeroReserva = leitor.IsDBNull(6) ? null : leitor.GetString(6),
                ItemReserva = leitor.IsDBNull(7) ? null : leitor.GetString(7),
                Lote = leitor.IsDBNull(8) ? null : leitor.GetString(8),
                QuantidadePrevista = leitor.IsDBNull(9) ? null : leitor.GetDecimal(9),
                QuantidadeRetiradaSap = leitor.IsDBNull(10) ? null : leitor.GetDecimal(10),
                QuantidadePendenteSap = leitor.IsDBNull(11) ? null : leitor.GetDecimal(11),
                QuantidadeConsumidaLocal = leitor.GetDecimal(12),
                Unidade = leitor.GetString(13),
                TipoMovimentoSap = leitor.GetString(14),
                StatusItem = leitor.GetString(15)
            });
        }

        return itens;
    }

    public async Task<ConsumoMaterialLancamento?> ObterDetalheCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        ConsumoMaterialLancamento? lancamento = await ObterPorCodigoAsync(codigoLancamento, cancellationToken);
        if (lancamento is null)
        {
            return null;
        }

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        foreach (ConsumoMaterialItem item in lancamento.Itens)
        {
            item.Pesagens = await LerPesagensAsync(conexao, item.Codigo, cancellationToken);
        }

        return lancamento;
    }

    private static async Task<IReadOnlyList<ConsumoMaterialPesagem>> LerPesagensAsync(
        NpgsqlConnection conexao,
        long codigoItem,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT codigo_consumo_material_pesagem, sequencia, peso_bruto_kg, peso_tara_kg,
                   peso_liquido_kg, unidade, origem, status_pesagem, pesado_em, usuario_criacao
              FROM consumo_material_pesagem
             WHERE codigo_consumo_material_item = @item
             ORDER BY sequencia, codigo_consumo_material_pesagem;
            """;

        List<ConsumoMaterialPesagem> pesagens = [];
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@item", codigoItem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            pesagens.Add(new ConsumoMaterialPesagem
            {
                Codigo = leitor.GetInt64(0),
                Sequencia = leitor.GetInt32(1),
                PesoBrutoKg = leitor.GetDecimal(2),
                PesoTaraKg = leitor.GetDecimal(3),
                PesoLiquidoKg = leitor.GetDecimal(4),
                Unidade = leitor.GetString(5),
                Origem = leitor.GetString(6),
                StatusPesagem = leitor.GetString(7),
                PesadoEm = NormalizarUtc(leitor.GetDateTime(8)),
                UsuarioCriacao = leitor.IsDBNull(9) ? null : leitor.GetString(9)
            });
        }

        return pesagens;
    }
}
