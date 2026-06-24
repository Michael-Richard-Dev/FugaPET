using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.Entrada;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Persistencia da rastreabilidade da Entrada de Produto (entrada_produto_lancamento /
/// _item / _pesagem). Cada lancamento e independente e preserva todas as pesagens.
/// </summary>
public sealed class EntradaProdutoRepositorio : RepositorioBase
{
    public EntradaProdutoRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Grava um lancamento completo (cabecalho + itens + pesagens) em uma unica transacao auditavel.
    /// Retorna o codigo do lancamento criado.
    /// </summary>
    public async Task<long> SalvarLancamentoAsync(EntradaProdutoLancamento lancamento, CancellationToken cancellationToken = default)
    {
        long? usuario = ObterCodigoUsuarioSessao();

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            long codigoLancamento = await InserirLancamentoAsync(conexao, transacao, lancamento, usuario, cancellationToken);

            foreach (EntradaProdutoItem item in lancamento.Itens)
            {
                long codigoItem = await InserirItemAsync(conexao, transacao, codigoLancamento, item, usuario, cancellationToken);

                IReadOnlyList<EntradaProdutoPesagem> pesagens =
                    EntradaProdutoPesagemCalculos.ValidarSequencias(item.Pesagens);
                foreach (EntradaProdutoPesagem pesagem in pesagens)
                {
                    await InserirPesagemAsync(
                        conexao,
                        transacao,
                        codigoItem,
                        pesagem,
                        usuario,
                        cancellationToken);
                }

                await AtualizarTotalRecebidoAsync(
                    conexao,
                    transacao,
                    codigoItem,
                    usuario,
                    cancellationToken);
            }

            return codigoLancamento;
        }, cancellationToken);
    }

    private async Task<long> InserirLancamentoAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        EntradaProdutoLancamento lancamento, long? usuario, CancellationToken cancellationToken)
    {
        // status FINALIZADO_LOCAL: o lancamento e gravado completo (todas as pesagens) ao parar a producao,
        // ainda nao enviado ao SAP. Coerente com a CHECK ck_entrada_lancamento_finalizado_tem_data.
        const string sql = """
            INSERT INTO entrada_produto_lancamento
                (numero_pedido, fornecedor, codigo_setor, terminal, codigo_usuario,
                 iniciado_em, finalizado_em, status_lancamento, entrada_produto_lancamento_criado_por)
            VALUES
                (@numero_pedido, @fornecedor, @codigo_setor, @terminal, @usuario,
                 now(), now(), 'FINALIZADO_LOCAL', @usuario)
            RETURNING codigo_entrada_produto_lancamento;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", lancamento.NumeroPedido));
        comando.Parameters.Add(ParametroTextoNulo("@fornecedor", lancamento.Fornecedor));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_setor", lancamento.CodigoSetor));
        comando.Parameters.Add(ParametroTextoNulo("@terminal", lancamento.Terminal));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Nao foi possivel criar o lancamento de entrada.");
    }

    private async Task<long> InserirItemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoLancamento, EntradaProdutoItem item, long? usuario, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_item
                (codigo_entrada_produto_lancamento, codigo_sap_pedido_compra_item, numero_item, material,
                 centro, deposito, unidade, quantidade_prevista, quantidade_recebida, status_item, entrada_produto_item_criado_por)
            VALUES
                (@lancamento, @sap_item, @numero_item, @material,
                 @centro, @deposito, @unidade, @qtd_prevista, NULL, 'FINALIZADO_LOCAL', @usuario)
            RETURNING codigo_entrada_produto_item;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroLongoNulo("@sap_item", item.CodigoSapPedidoCompraItem));
        comando.Parameters.Add(ParametroTexto("@numero_item", item.NumeroItem));
        comando.Parameters.Add(ParametroTextoNulo("@material", item.Material));
        comando.Parameters.Add(ParametroTextoNulo("@centro", item.Centro));
        comando.Parameters.Add(ParametroTextoNulo("@deposito", item.Deposito));
        comando.Parameters.Add(ParametroTextoNulo("@unidade", item.Unidade));
        comando.Parameters.Add(ParametroNumericoNulo("@qtd_prevista", item.QuantidadePrevista));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is long codigo ? codigo : throw new InvalidOperationException("Nao foi possivel criar o item do lancamento.");
    }

    private async Task InserirPesagemAsync(
        NpgsqlConnection conexao, NpgsqlTransaction transacao,
        long codigoItem, EntradaProdutoPesagem pesagem, long? usuario, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO entrada_produto_pesagem
                (codigo_entrada_produto_item, sequencia, peso_bruto_kg, peso_tara_kg, peso_liquido_kg,
                 codigo_tara, codigo_balanca, origem, status_pesagem, leitura_original,
                 payload_balanca, codigo_usuario, pesado_em, entrada_produto_pesagem_criado_por)
            VALUES
                (@item, @sequencia, @bruto, @tara, @liquido,
                 @codigo_tara, @codigo_balanca, @origem, @status, @leitura_original,
                 @payload_balanca, @usuario, @pesado_em, @usuario);
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@item", codigoItem));
        comando.Parameters.Add(ParametroInteiro("@sequencia", pesagem.Sequencia));
        comando.Parameters.Add(new NpgsqlParameter("@bruto", NpgsqlDbType.Numeric) { Value = pesagem.PesoBrutoKg });
        comando.Parameters.Add(new NpgsqlParameter("@tara", NpgsqlDbType.Numeric) { Value = pesagem.PesoTaraKg });
        comando.Parameters.Add(new NpgsqlParameter("@liquido", NpgsqlDbType.Numeric) { Value = pesagem.PesoLiquidoKg });
        comando.Parameters.Add(ParametroLongoNulo("@codigo_tara", pesagem.CodigoTara));
        comando.Parameters.Add(ParametroLongoNulo("@codigo_balanca", pesagem.CodigoBalanca));
        comando.Parameters.Add(ParametroTexto("@origem", string.IsNullOrWhiteSpace(pesagem.Origem) ? "BALANCA" : pesagem.Origem));
        comando.Parameters.Add(ParametroTexto("@status", string.IsNullOrWhiteSpace(pesagem.StatusPesagem) ? "VALIDA" : pesagem.StatusPesagem));
        comando.Parameters.Add(ParametroTextoNulo("@leitura_original", pesagem.LeituraOriginal));
        comando.Parameters.Add(new NpgsqlParameter("@payload_balanca", NpgsqlDbType.Jsonb)
        {
            Value = string.IsNullOrWhiteSpace(pesagem.PayloadBalanca)
                ? DBNull.Value
                : pesagem.PayloadBalanca
        });
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        comando.Parameters.Add(new NpgsqlParameter("@pesado_em", NpgsqlDbType.TimestampTz)
        {
            Value = pesagem.PesadoEm.ToUniversalTime()
        });
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AtualizarTotalRecebidoAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        long codigoItem,
        long? usuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE entrada_produto_item item
               SET quantidade_recebida = (
                       SELECT COALESCE(SUM(pesagem.peso_liquido_kg), 0)
                         FROM entrada_produto_pesagem pesagem
                        WHERE pesagem.codigo_entrada_produto_item = item.codigo_entrada_produto_item
                          AND pesagem.situacao_entrada_produto_pesagem = true
                          AND pesagem.status_pesagem = 'VALIDA'
                   ),
                   entrada_produto_item_atualizado_por = @usuario
             WHERE item.codigo_entrada_produto_item = @codigo_item;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroLongo("@codigo_item", codigoItem));
        comando.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<EntradaProdutoItemPersistido?> ObterItemPersistidoAsync(
        long codigoLancamento,
        long codigoSapPedidoCompraItem,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT lancamento.codigo_entrada_produto_lancamento,
                   item.codigo_sap_pedido_compra_item,
                   lancamento.numero_pedido,
                   item.numero_item,
                   COALESCE(item.material, ''),
                   COALESCE(item_sap.descricao_produto, ''),
                   COALESCE(lancamento.fornecedor, fornecedor.codigo_fornecedor, ''),
                   pedido_sap.data_pedido,
                   COALESCE(lancamento.terminal, ''),
                   COALESCE(SUM(pesagem.peso_liquido_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0)::numeric(14,3)
              FROM entrada_produto_lancamento lancamento
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_lancamento =
                   lancamento.codigo_entrada_produto_lancamento
              LEFT JOIN sap_pedido_compra_item item_sap
                ON item_sap.codigo_sap_pedido_compra_item =
                   item.codigo_sap_pedido_compra_item
              LEFT JOIN sap_pedido_compra pedido_sap
                ON pedido_sap.codigo_sap_pedido_compra =
                   item_sap.codigo_sap_pedido_compra
              LEFT JOIN sap_fornecedor fornecedor
                ON fornecedor.codigo_sap_fornecedor =
                   pedido_sap.codigo_sap_fornecedor
              LEFT JOIN entrada_produto_pesagem pesagem
                ON pesagem.codigo_entrada_produto_item =
                   item.codigo_entrada_produto_item
             WHERE lancamento.codigo_entrada_produto_lancamento = @codigo_lancamento
               AND item.codigo_sap_pedido_compra_item = @codigo_sap_item
               AND lancamento.situacao_entrada_produto_lancamento = true
               AND item.situacao_entrada_produto_item = true
             GROUP BY
                   lancamento.codigo_entrada_produto_lancamento,
                   item.codigo_sap_pedido_compra_item,
                   lancamento.numero_pedido,
                   item.numero_item,
                   item.material,
                   item_sap.descricao_produto,
                   lancamento.fornecedor,
                   fornecedor.codigo_fornecedor,
                   pedido_sap.data_pedido,
                   lancamento.terminal
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        comando.Parameters.Add(ParametroLongo("@codigo_sap_item", codigoSapPedidoCompraItem));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new EntradaProdutoItemPersistido
        {
            CodigoLancamento = leitor.GetInt64(0),
            CodigoSapPedidoCompraItem = leitor.GetInt64(1),
            NumeroPedido = leitor.GetString(2),
            NumeroItem = leitor.GetString(3),
            Material = leitor.GetString(4),
            DescricaoMaterial = leitor.GetString(5),
            Fornecedor = leitor.GetString(6),
            DataPedido = leitor.IsDBNull(7) ? null : leitor.GetFieldValue<DateOnly>(7),
            Terminal = leitor.GetString(8),
            PesoLiquidoTotalKg = leitor.GetDecimal(9)
        };
    }

    /// <summary>
    /// Itens de um lancamento ja persistido, com pesos consolidados das pesagens VALIDAS, para o
    /// envio CONTROLADO de peso ao SAP. Retorna apenas itens com peso liquido positivo.
    /// </summary>
    public async Task<IReadOnlyList<EntradaProdutoItemEnvioSap>> ListarItensParaEnvioSapAsync(
        long codigoLancamento,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT lancamento.numero_pedido,
                   item.numero_item,
                   COALESCE(SUM(pesagem.peso_liquido_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0)::numeric(14,3) AS peso_liquido,
                   COALESCE(SUM(pesagem.peso_bruto_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0)::numeric(14,3) AS peso_bruto
              FROM entrada_produto_lancamento lancamento
              JOIN entrada_produto_item item
                ON item.codigo_entrada_produto_lancamento =
                   lancamento.codigo_entrada_produto_lancamento
              LEFT JOIN entrada_produto_pesagem pesagem
                ON pesagem.codigo_entrada_produto_item =
                   item.codigo_entrada_produto_item
             WHERE lancamento.codigo_entrada_produto_lancamento = @codigo_lancamento
               AND lancamento.situacao_entrada_produto_lancamento = true
               AND item.situacao_entrada_produto_item = true
             GROUP BY lancamento.numero_pedido, item.numero_item
            HAVING COALESCE(SUM(pesagem.peso_liquido_kg) FILTER (
                       WHERE pesagem.situacao_entrada_produto_pesagem = true
                         AND pesagem.status_pesagem = 'VALIDA'
                   ), 0) > 0
             ORDER BY item.numero_item;
            """;

        List<EntradaProdutoItemEnvioSap> itens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(new EntradaProdutoItemEnvioSap
            {
                NumeroPedido = leitor.GetString(0),
                NumeroItem = leitor.GetString(1),
                PesoLiquidoKg = leitor.GetDecimal(2),
                PesoBrutoKg = leitor.GetDecimal(3)
            });
        }

        return itens;
    }

    public async Task AtualizarStatusAposEnvioSapAsync(
        long codigoLancamento,
        IReadOnlyList<ResultadoItemEnvioSap> resultados,
        CenarioEnvioSapEntrada cenario,
        CancellationToken cancellationToken = default)
    {
        if (codigoLancamento <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(codigoLancamento));
        }

        if (resultados.Count == 0)
        {
            throw new ArgumentException("Informe os resultados dos itens enviados.", nameof(resultados));
        }

        if (cenario is not CenarioEnvioSapEntrada.Enviado
            and not CenarioEnvioSapEntrada.Parcial
            and not CenarioEnvioSapEntrada.Falha)
        {
            throw new ArgumentException("Cenário de envio SAP inválido para atualização local.", nameof(cenario));
        }

        long? usuario = ObterCodigoUsuarioSessao();
        await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            foreach (ResultadoItemEnvioSap resultado in resultados)
            {
                const string sqlItem = """
                    UPDATE entrada_produto_item
                       SET status_item = @status_item,
                           entrada_produto_item_atualizado_por = @usuario
                     WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
                       AND numero_item = @numero_item
                       AND situacao_entrada_produto_item = true;
                    """;

                await using NpgsqlCommand comandoItem = new(sqlItem, conexao, transacao);
                comandoItem.Parameters.Add(ParametroTexto(
                    "@status_item",
                    resultado.Sucesso ? "CONFIRMADO_SAP" : "ERRO_SAP"));
                comandoItem.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
                comandoItem.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                comandoItem.Parameters.Add(ParametroTexto("@numero_item", resultado.NumeroItem));
                int itensAtualizados = await comandoItem.ExecuteNonQueryAsync(cancellationToken);
                if (itensAtualizados != 1)
                {
                    throw new InvalidOperationException(
                        $"Não foi possível atualizar o status local do item {resultado.NumeroItem}.");
                }
            }

            if (cenario is CenarioEnvioSapEntrada.Enviado or CenarioEnvioSapEntrada.Falha)
            {
                const string sqlLancamento = """
                    UPDATE entrada_produto_lancamento
                       SET status_lancamento = @status_lancamento,
                           entrada_produto_lancamento_atualizado_por = @usuario
                     WHERE codigo_entrada_produto_lancamento = @codigo_lancamento
                       AND situacao_entrada_produto_lancamento = true;
                    """;

                await using NpgsqlCommand comandoLancamento = new(sqlLancamento, conexao, transacao);
                comandoLancamento.Parameters.Add(ParametroTexto(
                    "@status_lancamento",
                    cenario == CenarioEnvioSapEntrada.Enviado ? "CONFIRMADO_SAP" : "ERRO_SAP"));
                comandoLancamento.Parameters.Add(ParametroUsuarioObrigatorio("@usuario", usuario));
                comandoLancamento.Parameters.Add(ParametroLongo("@codigo_lancamento", codigoLancamento));
                int lancamentosAtualizados =
                    await comandoLancamento.ExecuteNonQueryAsync(cancellationToken);
                if (lancamentosAtualizados != 1)
                {
                    throw new InvalidOperationException(
                        "Não foi possível atualizar o status local do lançamento.");
                }
            }

            return true;
        }, cancellationToken);
    }

    private static NpgsqlParameter ParametroTextoNulo(string nome, string? valor)
        => new(nome, NpgsqlDbType.Text) { Value = string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim() };

    private static NpgsqlParameter ParametroNumericoNulo(string nome, decimal? valor)
        => new(nome, NpgsqlDbType.Numeric) { Value = valor.HasValue ? valor.Value : DBNull.Value };

    private static NpgsqlParameter ParametroUsuarioObrigatorio(string nome, long? usuario)
        => new(nome, NpgsqlDbType.Bigint)
        {
            Value = usuario ?? throw new InvalidOperationException("Usuario da sessao obrigatorio para gravar a entrada.")
        };
}
