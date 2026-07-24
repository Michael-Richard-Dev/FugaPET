using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Processo;
using Npgsql;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// PersistÃªncia do Produto Semi-Acabado. NÃƒO hardcodeia schema: usa nomes de tabela nus, resolvidos pelo
/// search_path da conexÃ£o (desenvolvimento no DEV, homologacao no HML), como os demais repositories.
/// </summary>
public sealed class SemiAcabadoRepositorio : RepositorioBase, ISemiAcabadoRepositorio
{
    public SemiAcabadoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco)
        : base(fabricaConexaoBanco)
    {
    }

    public async Task<bool> EstruturaDisponivelAsync(CancellationToken cancellationToken = default)
    {
        // to_regclass respeita o search_path (schema atual), sem interpolar nome de schema.
        const string sql = """
            SELECT to_regclass('semi_acabado_lancamento') IS NOT NULL
               AND to_regclass('semi_acabado_pesagem') IS NOT NULL;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        object? valor = await comando.ExecuteScalarAsync(cancellationToken);
        return valor is bool disponivel && disponivel;
    }

    public async Task<long> SalvarLancamentoLocalAsync(
        LancamentoSemiAcabado lancamento,
        string payloadPreviewJson,
        CancellationToken cancellationToken = default)
    {
        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            const string inserirLancamento = """
                INSERT INTO semi_acabado_lancamento
                (
                    numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
                    item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
                    quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
                    payload_preview_json, usuario_operacao, criado_em, atualizado_em
                )
                VALUES
                (
                    @numero_ordem, @material_produzido, @descricao_material, @centro, @deposito_destino,
                    @item_ordem, @lote, @unidade, @quantidade_planejada_kg, @quantidade_entregue_kg,
                    @quantidade_pendente_kg, @peso_liquido_total_kg, 'FINALIZADO_LOCAL',
                    @payload_preview_json, @usuario_operacao, now(), now()
                )
                RETURNING codigo_semi_acabado_lancamento;
                """;

            await using NpgsqlCommand comandoLancamento = new(inserirLancamento, conexao, transacao);
            comandoLancamento.Parameters.Add(ParametroTexto("@numero_ordem", lancamento.Ordem.NumeroOrdem));
            comandoLancamento.Parameters.Add(ParametroTexto("@material_produzido", lancamento.Ordem.MaterialProduzido));
            comandoLancamento.Parameters.Add(ParametroTexto("@descricao_material", lancamento.Ordem.DescricaoMaterial));
            comandoLancamento.Parameters.Add(ParametroTexto("@centro", lancamento.Ordem.Centro));
            comandoLancamento.Parameters.Add(ParametroTexto("@deposito_destino", lancamento.Ordem.DepositoDestino));
            comandoLancamento.Parameters.Add(ParametroTexto("@item_ordem", lancamento.Ordem.ItemOrdem));
            comandoLancamento.Parameters.Add(ParametroTexto("@lote", lancamento.Ordem.Lote));
            comandoLancamento.Parameters.Add(ParametroTexto("@unidade", lancamento.Ordem.Unidade));
            comandoLancamento.Parameters.Add(ParametroDecimal("@quantidade_planejada_kg", lancamento.Ordem.QuantidadePlanejada));
            comandoLancamento.Parameters.Add(ParametroDecimal("@quantidade_entregue_kg", lancamento.Ordem.QuantidadeEntregue));
            comandoLancamento.Parameters.Add(ParametroDecimal("@quantidade_pendente_kg", lancamento.Ordem.QuantidadePendente));
            comandoLancamento.Parameters.Add(ParametroDecimal("@peso_liquido_total_kg", lancamento.PesoLiquidoTotalKg));
            comandoLancamento.Parameters.Add(ParametroTexto("@payload_preview_json", payloadPreviewJson));
            comandoLancamento.Parameters.Add(ParametroTexto("@usuario_operacao", lancamento.Usuario));

            long codigoLancamento = Convert.ToInt64(await comandoLancamento.ExecuteScalarAsync(cancellationToken));

            foreach (PesagemSemiAcabado pesagem in lancamento.Pesagens)
            {
                const string inserirPesagem = """
                    INSERT INTO semi_acabado_pesagem
                    (
                        codigo_semi_acabado_lancamento, sequencia, codigo_etiqueta, status_pesagem,
                        peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg, origem, leitura_original,
                        codigo_tara, registrado_em, cancelado_em
                    )
                    VALUES
                    (
                        @codigo_semi_acabado_lancamento, @sequencia, @codigo_etiqueta, @status_pesagem,
                        @peso_bruto_kg, @peso_tara_kg, @peso_liquido_kg, @saldo_apos_pesagem_kg, @origem, @leitura_original,
                        @codigo_tara, @registrado_em, @cancelado_em
                    )
                    RETURNING codigo_semi_acabado_pesagem;
                    """;

                await using NpgsqlCommand comandoPesagem = new(inserirPesagem, conexao, transacao);
                comandoPesagem.Parameters.Add(ParametroLongo("@codigo_semi_acabado_lancamento", codigoLancamento));
                comandoPesagem.Parameters.Add(ParametroInteiro("@sequencia", pesagem.Sequencia));
                comandoPesagem.Parameters.Add(ParametroTexto("@codigo_etiqueta", pesagem.CodigoEtiqueta));
                comandoPesagem.Parameters.Add(ParametroTexto("@status_pesagem", pesagem.StatusPesagem));
                comandoPesagem.Parameters.Add(ParametroDecimal("@peso_bruto_kg", pesagem.PesoBrutoKg));
                comandoPesagem.Parameters.Add(ParametroDecimal("@peso_tara_kg", pesagem.PesoTaraKg));
                comandoPesagem.Parameters.Add(ParametroDecimal("@peso_liquido_kg", pesagem.PesoLiquidoKg));
                comandoPesagem.Parameters.Add(ParametroDecimal("@saldo_apos_pesagem_kg", pesagem.SaldoAposPesagemKg));
                comandoPesagem.Parameters.Add(ParametroTexto("@origem", pesagem.Origem));
                comandoPesagem.Parameters.Add(ParametroTexto("@leitura_original", pesagem.LeituraOriginal));
                comandoPesagem.Parameters.Add(ParametroLongoNulo("@codigo_tara", pesagem.CodigoTara));
                comandoPesagem.Parameters.Add(ParametroDataHora("@registrado_em", pesagem.RegistradoEm));
                comandoPesagem.Parameters.Add(ParametroDataHoraNulo("@cancelado_em", pesagem.CanceladoEm));

                pesagem.CodigoSemiAcabadoPesagem = Convert.ToInt64(await comandoPesagem.ExecuteScalarAsync(cancellationToken));
            }

            lancamento.CodigoSemiAcabadoLancamento = codigoLancamento;
            return codigoLancamento;
        }, cancellationToken);
    }

    public async Task<bool> TentarReservarEnvioSapAsync(long codigoLancamento, CancellationToken cancellationToken = default)
    {
        // TransiÃ§Ã£o ATÃ”MICA para ENVIANDO_SAP: sÃ³ FINALIZADO_LOCAL ou ERRO_SAP e sem documento (nunca DIVERGENCIA_SAP).
        const string sql = """
            UPDATE semi_acabado_lancamento
               SET status_lancamento = 'ENVIANDO_SAP',
                   atualizado_em = now()
             WHERE codigo_semi_acabado_lancamento = @codigo
               AND status_lancamento IN ('FINALIZADO_LOCAL', 'ERRO_SAP')
               AND material_document IS NULL
             RETURNING 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlTransaction transacao = await conexao.BeginTransactionAsync(cancellationToken);
        await DefinirUsuarioAppAsync(conexao, transacao, cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
        object? reservado = await comando.ExecuteScalarAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);
        return reservado is not null;
    }

    public async Task<LancamentoSemiAcabado?> ObterLancamentoCompletoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT numero_ordem, material_produzido, descricao_material, centro, deposito_destino,
                   item_ordem, lote, unidade, quantidade_planejada_kg, quantidade_entregue_kg,
                   quantidade_pendente_kg, peso_liquido_total_kg, status_lancamento,
                   material_document, material_document_year, enviado_sap_em, usuario_operacao, criado_em
              FROM semi_acabado_lancamento
             WHERE codigo_semi_acabado_lancamento = @codigo;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        decimal pesoLiquidoTotalPersistido = leitor.GetDecimal(11);
        LancamentoSemiAcabado lancamento = new()
        {
            CodigoSemiAcabadoLancamento = codigoLancamento,
            Ordem = new SemiAcabadoOrdem
            {
                NumeroOrdem = leitor.GetString(0),
                MaterialProduzido = leitor.GetString(1),
                DescricaoMaterial = leitor.GetString(2),
                Centro = leitor.GetString(3),
                DepositoDestino = leitor.GetString(4),
                ItemOrdem = leitor.IsDBNull(5) ? string.Empty : leitor.GetString(5),
                Lote = leitor.IsDBNull(6) ? string.Empty : leitor.GetString(6),
                Unidade = leitor.GetString(7),
                QuantidadePlanejada = leitor.GetDecimal(8),
                QuantidadeEntregue = leitor.GetDecimal(9),
                QuantidadePendente = leitor.GetDecimal(10)
            },
            StatusLancamento = leitor.GetString(12),
            MaterialDocument = leitor.IsDBNull(13) ? null : leitor.GetString(13),
            MaterialDocumentYear = leitor.IsDBNull(14) ? null : leitor.GetString(14),
            EnviadoSapEm = leitor.IsDBNull(15) ? null : leitor.GetDateTime(15),
            Usuario = leitor.IsDBNull(16) ? string.Empty : leitor.GetString(16),
            CriadoEm = leitor.GetDateTime(17),
            Pesagens = await ListarPesagensPorLancamentoAsync(codigoLancamento, cancellationToken)
        };

        if (PesagemSemiAcabadoCalculos.SomarPesoLiquidoValido(lancamento.Pesagens) != pesoLiquidoTotalPersistido)
        {
            throw new InvalidOperationException("SomatÃ³rio persistido do semi-acabado diverge das pesagens vÃ¡lidas.");
        }

        return lancamento;
    }

    public Task MarcarConfirmadoSapAsync(
        long codigoLancamento,
        string materialDocument,
        string materialDocumentYear,
        CancellationToken cancellationToken = default)
    {
        // Confirma SOMENTE se ainda ENVIANDO_SAP e sem documento; exige exatamente 1 linha (defesa anti-duplicidade).
        const string sql = """
            UPDATE semi_acabado_lancamento
               SET status_lancamento = 'CONFIRMADO_SAP',
                   material_document = @material_document,
                   material_document_year = @material_document_year,
                   mensagem_erro_sap = NULL,
                   enviado_sap_em = now(),
                   atualizado_em = now()
             WHERE codigo_semi_acabado_lancamento = @codigo
               AND status_lancamento = 'ENVIANDO_SAP'
               AND material_document IS NULL;
            """;

        return ExecutarUpdateExatamenteUmaLinhaAsync(
            sql,
            comando =>
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@material_document", materialDocument));
                comando.Parameters.Add(ParametroTexto("@material_document_year", materialDocumentYear));
            },
            "confirmar documento SAP do semi-acabado",
            cancellationToken);
    }

    public Task MarcarErroSapAsync(long codigoLancamento, string mensagemErro, CancellationToken cancellationToken = default)
    {
        // ERRO_SAP sÃ³ quando a falha ocorreu ANTES de qualquer possibilidade de documento: exige ENVIANDO_SAP + sem doc.
        const string sql = """
            UPDATE semi_acabado_lancamento
               SET status_lancamento = 'ERRO_SAP',
                   mensagem_erro_sap = @mensagem_erro_sap,
                   atualizado_em = now()
             WHERE codigo_semi_acabado_lancamento = @codigo
               AND status_lancamento = 'ENVIANDO_SAP'
               AND material_document IS NULL;
            """;

        return ExecutarUpdateExatamenteUmaLinhaAsync(
            sql,
            comando =>
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@mensagem_erro_sap", mensagemErro));
            },
            "marcar erro SAP do semi-acabado",
            cancellationToken);
    }

    public Task MarcarDivergenciaSapAsync(long codigoLancamento, string mensagem, CancellationToken cancellationToken = default)
    {
        // HTTP 2xx sem MaterialDocument/Year: estado terminal DIVERGENCIA_SAP â€” NÃƒO elegÃ­vel para reenvio (nÃ£o entra
        // no filtro de TentarReservarEnvioSapAsync). Exige ENVIANDO_SAP + sem doc.
        const string sql = """
            UPDATE semi_acabado_lancamento
               SET status_lancamento = 'DIVERGENCIA_SAP',
                   mensagem_erro_sap = @mensagem_erro_sap,
                   atualizado_em = now()
             WHERE codigo_semi_acabado_lancamento = @codigo
               AND status_lancamento = 'ENVIANDO_SAP'
               AND material_document IS NULL;
            """;

        return ExecutarUpdateExatamenteUmaLinhaAsync(
            sql,
            comando =>
            {
                comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
                comando.Parameters.Add(ParametroTexto("@mensagem_erro_sap", mensagem));
            },
            "marcar divergÃªncia SAP do semi-acabado",
            cancellationToken);
    }


    public async Task<bool> CancelarLancamentoLocalAsync(
        long codigoLancamento,
        string motivo,
        string usuario,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE semi_acabado_lancamento
               SET status_lancamento = 'CANCELADO',
                   cancelado_em = now(),
                   cancelado_por = @usuario,
                   motivo_cancelamento = @motivo,
                   atualizado_em = now()
             WHERE codigo_semi_acabado_lancamento = @codigo
               AND status_lancamento = 'ERRO_SAP'
               AND material_document IS NULL
               AND material_document_year IS NULL;
            """;

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
            comando.Parameters.Add(ParametroTexto("@motivo", motivo));
            comando.Parameters.Add(ParametroTexto("@usuario", usuario));
            int linhas = await comando.ExecuteNonQueryAsync(cancellationToken);
            return linhas == 1;
        }, cancellationToken);
    }
    public async Task<IReadOnlyList<PesagemSemiAcabado>> ListarPesagensPorLancamentoAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_semi_acabado_pesagem, sequencia, codigo_etiqueta, status_pesagem,
                   peso_bruto_kg, peso_tara_kg, peso_liquido_kg, saldo_apos_pesagem_kg, origem, leitura_original,
                   codigo_tara, registrado_em, cancelado_em
              FROM semi_acabado_pesagem
             WHERE codigo_semi_acabado_lancamento = @codigo
             ORDER BY sequencia, registrado_em;
            """;

        List<PesagemSemiAcabado> pesagens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigoLancamento));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            pesagens.Add(new PesagemSemiAcabado
            {
                CodigoSemiAcabadoPesagem = leitor.GetInt64(0),
                Sequencia = leitor.GetInt32(1),
                CodigoEtiqueta = leitor.GetString(2),
                StatusPesagem = leitor.GetString(3),
                PesoBrutoKg = leitor.GetDecimal(4),
                PesoTaraKg = leitor.GetDecimal(5),
                PesoLiquidoKg = leitor.GetDecimal(6),
                SaldoAposPesagemKg = leitor.GetDecimal(7),
                Origem = leitor.IsDBNull(8) ? "MANUAL" : leitor.GetString(8),
                LeituraOriginal = leitor.IsDBNull(9) ? string.Empty : leitor.GetString(9),
                CodigoTara = leitor.IsDBNull(10) ? null : leitor.GetInt64(10),
                RegistradoEm = leitor.GetDateTime(11),
                CanceladoEm = leitor.IsDBNull(12) ? null : leitor.GetDateTime(12)
            });
        }

        return pesagens;
    }

    public async Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_semi_acabado_lancamento, status_lancamento, material_document, material_document_year
              FROM semi_acabado_lancamento
             WHERE numero_ordem = @numero_ordem
               AND coalesce(item_ordem, '') = coalesce(@item_ordem, '')
             ORDER BY codigo_semi_acabado_lancamento DESC
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_ordem", numeroOrdem));
        comando.Parameters.Add(ParametroTexto("@item_ordem", itemOrdem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LancamentoSemiAcabadoPersistido
        {
            CodigoSemiAcabadoLancamento = leitor.GetInt64(0),
            StatusLancamento = leitor.GetString(1),
            MaterialDocument = leitor.IsDBNull(2) ? null : leitor.GetString(2),
            MaterialDocumentYear = leitor.IsDBNull(3) ? null : leitor.GetString(3)
        };
    }

    public async Task<LancamentoSemiAcabadoPersistido?> ObterLancamentoAbertoPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default)
    {
        // Aberto = qualquer status que ainda "prende" a OP/item (impede novo lanÃ§amento paralelo).
        // CONFIRMADO_SAP fica de fora: produÃ§Ãµes parciais posteriores da mesma OP sÃ£o permitidas.
        const string sql = """
            SELECT codigo_semi_acabado_lancamento, status_lancamento, material_document, material_document_year
              FROM semi_acabado_lancamento
             WHERE numero_ordem = @numero_ordem
               AND coalesce(item_ordem, '') = coalesce(@item_ordem, '')
               AND status_lancamento IN ('FINALIZADO_LOCAL', 'ENVIANDO_SAP', 'ERRO_SAP', 'DIVERGENCIA_SAP')
             ORDER BY codigo_semi_acabado_lancamento DESC
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_ordem", numeroOrdem));
        comando.Parameters.Add(ParametroTexto("@item_ordem", itemOrdem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LancamentoSemiAcabadoPersistido
        {
            CodigoSemiAcabadoLancamento = leitor.GetInt64(0),
            StatusLancamento = leitor.GetString(1),
            MaterialDocument = leitor.IsDBNull(2) ? null : leitor.GetString(2),
            MaterialDocumentYear = leitor.IsDBNull(3) ? null : leitor.GetString(3)
        };
    }

    public async Task<IReadOnlyList<LancamentoSemiAcabadoPersistido>> ListarLancamentosPorOpItemAsync(
        string numeroOrdem,
        string itemOrdem,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT codigo_semi_acabado_lancamento, status_lancamento, material_document, material_document_year
              FROM semi_acabado_lancamento
             WHERE numero_ordem = @numero_ordem
               AND coalesce(item_ordem, '') = coalesce(@item_ordem, '')
             ORDER BY codigo_semi_acabado_lancamento DESC;
            """;

        List<LancamentoSemiAcabadoPersistido> lancamentos = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_ordem", numeroOrdem));
        comando.Parameters.Add(ParametroTexto("@item_ordem", itemOrdem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            lancamentos.Add(new LancamentoSemiAcabadoPersistido
            {
                CodigoSemiAcabadoLancamento = leitor.GetInt64(0),
                StatusLancamento = leitor.GetString(1),
                MaterialDocument = leitor.IsDBNull(2) ? null : leitor.GetString(2),
                MaterialDocumentYear = leitor.IsDBNull(3) ? null : leitor.GetString(3)
            });
        }

        return lancamentos;
    }

    private Task ExecutarUpdateExatamenteUmaLinhaAsync(
        string sql,
        Action<NpgsqlCommand> preencherParametros,
        string operacao,
        CancellationToken cancellationToken)
        => ExecutarEmTransacaoAuditavelAsync<int>(async (conexao, transacao) =>
        {
            await using NpgsqlCommand comando = new(sql, conexao, transacao);
            preencherParametros(comando);
            int linhas = await comando.ExecuteNonQueryAsync(cancellationToken);
            if (linhas != 1)
            {
                throw new InvalidOperationException(
                    $"Falha ao {operacao}: esperado 1 linha atualizada, obtido {linhas} (estado divergente do lanÃ§amento).");
            }

            return linhas;
        }, cancellationToken);

    private static NpgsqlParameter ParametroDecimal(string nome, decimal valor)
        => new(nome, valor);

    private static NpgsqlParameter ParametroDataHora(string nome, DateTime valor)
        => new(nome, valor);

    private static NpgsqlParameter ParametroDataHoraNulo(string nome, DateTime? valor)
        => new(nome, valor is null ? DBNull.Value : valor);
}

