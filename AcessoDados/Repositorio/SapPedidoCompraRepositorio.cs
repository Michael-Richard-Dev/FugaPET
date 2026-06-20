using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.AcessoDados.Comum;
using FugaPET_Dev.Modelo.IntegracaoSap;
using Npgsql;
using NpgsqlTypes;

namespace FugaPET_Dev.AcessoDados.Repositorio;

/// <summary>
/// Cache local de pedidos de compra do SAP (sap_pedido_compra).
/// Faz a carga (upsert por numero_pedido) e a leitura para a tela de Entrada de Produto.
/// </summary>
public sealed class SapPedidoCompraRepositorio : RepositorioBase
{
    public SapPedidoCompraRepositorio(IFabricaConexaoBanco fabricaConexaoBanco) : base(fabricaConexaoBanco)
    {
    }

    /// <summary>
    /// Carga atomica dos pedidos vindos do SAP: para cada pedido, resolve a FK do fornecedor
    /// (pelo codigo SAP), e faz UPDATE quando ja existe o numero_pedido ou INSERT caso contrario.
    /// Atualiza status_cache='VALIDO' e sincronizado_em. Retorna a quantidade processada.
    /// </summary>
    public async Task<int> SincronizarAsync(IReadOnlyList<PedidoCompraSap> pedidos, CancellationToken cancellationToken = default)
        => await SincronizarInternoAsync(pedidos, inativarAusentes: false, cancellationToken);

    /// <summary>
    /// Carga completa e atomica: upsert e inativacao dos ausentes ocorrem na mesma transacao.
    /// Somente deve ser chamada depois que o cliente confirmar que toda a paginacao foi recebida.
    /// </summary>
    internal async Task<int> SincronizarCargaCompletaAsync(
        IReadOnlyList<PedidoCompraSap> pedidos,
        CancellationToken cancellationToken = default)
        => await SincronizarInternoAsync(pedidos, inativarAusentes: true, cancellationToken);

    private async Task<int> SincronizarInternoAsync(
        IReadOnlyList<PedidoCompraSap> pedidos,
        bool inativarAusentes,
        CancellationToken cancellationToken)
    {
        if (pedidos.Count == 0 && !inativarAusentes)
        {
            return 0;
        }

        // Carga set-based: 3 comandos em lote (fornecedores, pedidos, itens) via unnest + ON CONFLICT,
        // em vez de milhares de round-trips por linha. Contra o banco remoto isso reduz a carga de
        // ~260s para ~1s (1144 pedidos / 1079 itens), evitando que a transacao fique aberta tempo demais.
        int total = pedidos.Count;
        string?[] pedNumeros = new string?[total];
        string?[] pedFornecedores = new string?[total];
        string?[] pedDatas = new string?[total];
        string?[] pedMoedas = new string?[total];
        string?[] pedTipos = new string?[total];
        string?[] pedStatus = new string?[total];
        string?[] pedPayloads = new string?[total];

        List<string?> itemPedidoNumero = [];
        List<string?> itemNumero = [];
        List<string?> itemMaterial = [];
        List<string?> itemDescricao = [];
        List<string?> itemQuantidade = [];
        List<string?> itemUnidade = [];
        List<string?> itemPeso = [];
        List<string?> itemCentro = [];
        List<string?> itemDeposito = [];
        List<string?> itemGrupoMaterial = [];
        List<string?> itemPayload = [];

        for (int i = 0; i < total; i++)
        {
            PedidoCompraSap pedido = pedidos[i];
            pedNumeros[i] = pedido.Numero?.Trim();
            pedFornecedores[i] = NuloSeVazio(pedido.FornecedorCodigoSap);
            pedDatas[i] = pedido.DataPedido?.ToString("yyyy-MM-dd");
            pedMoedas[i] = NuloSeVazio(pedido.Moeda);
            pedTipos[i] = NormalizarTipoPedido(pedido.TipoPedido);
            pedStatus[i] = NuloSeVazio(pedido.Status);
            pedPayloads[i] = NuloSeVazio(pedido.PayloadOriginalJson);

            foreach (PedidoCompraSapItem item in pedido.Itens)
            {
                if (string.IsNullOrWhiteSpace(item.NumeroItem))
                {
                    continue;
                }

                itemPedidoNumero.Add(pedido.Numero?.Trim());
                itemNumero.Add(item.NumeroItem.Trim());
                itemMaterial.Add(NuloSeVazio(item.CodigoMaterial));
                itemDescricao.Add(NuloSeVazio(item.Descricao));
                itemQuantidade.Add(item.Quantidade?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                itemUnidade.Add(NuloSeVazio(item.UnidadeMedida));
                itemPeso.Add(item.PesoItem?.ToString(System.Globalization.CultureInfo.InvariantCulture));
                itemCentro.Add(NuloSeVazio(item.Centro));
                itemDeposito.Add(NuloSeVazio(item.Deposito));
                itemGrupoMaterial.Add(NuloSeVazio(item.GrupoMaterial));
                itemPayload.Add(NuloSeVazio(item.PayloadOriginalJson));
            }
        }

        long? usuario = ObterCodigoUsuarioSessao();

        return await ExecutarEmTransacaoAuditavelAsync(async (conexao, transacao) =>
        {
            await UpsertFornecedoresAsync(conexao, transacao, pedFornecedores, usuario, cancellationToken);
            await UpsertPedidosAsync(
                conexao, transacao, pedNumeros, pedFornecedores, pedDatas, pedMoedas, pedTipos, pedStatus, pedPayloads, usuario, cancellationToken);
            await UpsertItensAsync(
                conexao, transacao, [.. itemPedidoNumero], [.. itemNumero], [.. itemMaterial], [.. itemDescricao], [.. itemQuantidade], [.. itemUnidade], [.. itemPeso], [.. itemCentro], [.. itemDeposito], [.. itemGrupoMaterial], [.. itemPayload], usuario, cancellationToken);
            if (inativarAusentes)
            {
                await InativarPedidosForaDaListaValidaAsync(
                    conexao,
                    transacao,
                    pedNumeros,
                    usuario,
                    cancellationToken);
            }

            return pedNumeros.Count(numero => !string.IsNullOrWhiteSpace(numero));
        }, cancellationToken);
    }

    // Inativa pedidos do cache que NAO vieram na carga atual do SAP (sairam do escopo retornado).
    // Nao tem relacao com Incoterms (H6): o status reflete ausencia no SAP, nao a regra de Incoterms.
    private static async Task InativarPedidosForaDaListaValidaAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        string?[] numerosValidos,
        long? usuario,
        CancellationToken cancellationToken)
    {
        string[] numeros = numerosValidos
            .Where(numero => !string.IsNullOrWhiteSpace(numero))
            .Select(numero => numero!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        const string sql = """
            UPDATE sap_pedido_compra
               SET ativo_sap = false,
                   status_cache = 'AUSENTE_SAP',
                   sap_pedido_compra_atualizado_por = @atualizado_por,
                   sap_pedido_compra_atualizado_em = now()
             WHERE ativo_sap = true
               AND situacao_sap_pedido_compra = true
               AND NOT (numero_pedido = ANY(@numeros_validos));
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(new NpgsqlParameter("@numeros_validos", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = numeros
        });
        comando.Parameters.Add(ParametroLongoNulo("@atualizado_por", usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    internal async Task<long> IniciarExecucaoSincronizacaoAsync(
        Guid identificadorExecucao,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO sap_sincronizacao_execucao
                (codigo_servico_integracao, tipo_chave_consulta, chave_consulta, status,
                 sap_sincronizacao_execucao_criado_por)
            SELECT codigo_sap_servico_integracao, 'CARGA_COMPLETA', @identificador, 'EM_EXECUCAO', @usuario
              FROM sap_servico_integracao
             WHERE codigo_servico = 'OP_PURCHASEORDER_0001'
               AND situacao_sap_servico_integracao = true
             ORDER BY codigo_sap_servico_integracao
             LIMIT 1
            RETURNING codigo_sap_sincronizacao_execucao;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@identificador", identificadorExecucao.ToString("D")));
        comando.Parameters.Add(ParametroLongoNulo("@usuario", ObterCodigoUsuarioSessao()));
        object? codigo = await comando.ExecuteScalarAsync(cancellationToken);
        return codigo is long valor
            ? valor
            : throw new InvalidOperationException("Servico SAP de pedido de compra nao configurado no banco.");
    }

    internal async Task FinalizarExecucaoSincronizacaoAsync(
        long codigoExecucao,
        string status,
        int paginas,
        int pedidos,
        int itens,
        long duracaoMs,
        string? erroSanitizado,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE sap_sincronizacao_execucao
               SET finalizado_em = now(),
                   status = @status,
                   mensagem_erro = @mensagem,
                   registros_processados = @pedidos,
                   tempo_resposta_ms = @duracao,
                   sap_sincronizacao_execucao_atualizado_por = @usuario,
                   sap_sincronizacao_execucao_atualizado_em = now()
             WHERE codigo_sap_sincronizacao_execucao = @codigo;
            """;

        string resumo = $"Paginas={paginas}; Itens={itens}.";
        string mensagem = string.IsNullOrWhiteSpace(erroSanitizado)
            ? resumo
            : $"{resumo} {erroSanitizado.Trim()}";

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroLongo("@codigo", codigoExecucao));
        comando.Parameters.Add(ParametroTexto("@status", status));
        comando.Parameters.Add(ParametroTexto("@mensagem", mensagem));
        comando.Parameters.Add(ParametroInteiro("@pedidos", Math.Max(0, pedidos)));
        comando.Parameters.Add(ParametroInteiro("@duracao", (int)Math.Min(int.MaxValue, Math.Max(0, duracaoMs))));
        comando.Parameters.Add(ParametroLongoNulo("@usuario", ObterCodigoUsuarioSessao()));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Numeros de pedido ativos no cache local, para alimentar o combo da tela.</summary>
    public async Task<IReadOnlyList<string>> ListarNumerosAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT numero_pedido
              FROM sap_pedido_compra
             WHERE situacao_sap_pedido_compra = true
               AND ativo_sap = true
             ORDER BY numero_pedido;
            """;

        List<string> numeros = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            numeros.Add(leitor.GetString(0));
        }

        return numeros;
    }

    public async Task<PedidoCompraSapAgregado?> ObterPedidoAgregadoAsync(
        string numeroPedido,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return null;
        }

        const string sql = """
            SELECT p.numero_pedido,
                   COALESCE(f.codigo_fornecedor, p.payload_original ->> 'Supplier', ''),
                   p.data_pedido,
                   COALESCE(
                       p.tipo_pedido,
                       left(trim(p.payload_original ->> 'PurchaseOrderType'), 4),
                       ''
                   ),
                   i.codigo_sap_pedido_compra_item,
                   i.numero_item,
                   COALESCE(i.codigo_produto, ''),
                   i.descricao_produto,
                   i.quantidade_pedida,
                   i.unidade_medida,
                   i.peso_item,
                   i.centro,
                   i.deposito,
                   i.grupo_material
              FROM sap_pedido_compra p
              LEFT JOIN sap_fornecedor f
                ON f.codigo_sap_fornecedor = p.codigo_sap_fornecedor
              LEFT JOIN sap_pedido_compra_item i
                ON i.codigo_sap_pedido_compra = p.codigo_sap_pedido_compra
               AND i.situacao_sap_pedido_compra_item = true
               AND i.ativo_sap = true
               AND length(trim(coalesce(i.codigo_produto, ''))) > 0
             WHERE trim(p.numero_pedido) = trim(@numero_pedido)
               AND p.situacao_sap_pedido_compra = true
               AND p.ativo_sap = true
             ORDER BY i.numero_item;
            """;

        await using NpgsqlConnection conexao =
            await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", numeroPedido));
        await using NpgsqlDataReader leitor =
            await comando.ExecuteReaderAsync(cancellationToken);

        string? numero = null;
        string fornecedor = string.Empty;
        DateOnly? dataPedido = null;
        string tipoPedido = string.Empty;
        List<PedidoCompraSapItem> itens = [];

        while (await leitor.ReadAsync(cancellationToken))
        {
            numero ??= leitor.GetString(0);
            fornecedor = leitor.GetString(1);
            dataPedido = leitor.IsDBNull(2)
                ? null
                : leitor.GetFieldValue<DateOnly>(2);
            tipoPedido = leitor.GetString(3);

            if (leitor.IsDBNull(4))
            {
                continue;
            }

            itens.Add(new PedidoCompraSapItem
            {
                CodigoItem = leitor.GetInt64(4),
                NumeroItem = leitor.GetString(5),
                CodigoMaterial = leitor.GetString(6),
                Descricao = leitor.IsDBNull(7) ? null : leitor.GetString(7),
                Quantidade = leitor.IsDBNull(8) ? null : leitor.GetDecimal(8),
                UnidadeMedida = leitor.IsDBNull(9) ? null : leitor.GetString(9),
                PesoItem = leitor.IsDBNull(10) ? null : leitor.GetDecimal(10),
                Centro = leitor.IsDBNull(11) ? null : leitor.GetString(11),
                Deposito = leitor.IsDBNull(12) ? null : leitor.GetString(12),
                GrupoMaterial = leitor.IsDBNull(13) ? null : leitor.GetString(13)
            });
        }

        return numero is null
            ? null
            : new PedidoCompraSapAgregado
            {
                NumeroPedido = numero,
                Fornecedor = fornecedor,
                DataPedido = dataPedido,
                TipoPedido = tipoPedido,
                Itens = itens
            };
    }

    /// <summary>
    /// Codigo do fornecedor do pedido no cache local. Usa a FK quando ela existe e,
    /// como fallback, o campo Supplier preservado no payload original do SAP.
    /// </summary>
    public async Task<string> ObterFornecedorPorNumeroPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return string.Empty;
        }

        const string sql = """
            SELECT COALESCE(f.codigo_fornecedor, p.payload_original ->> 'Supplier', '') AS fornecedor
              FROM sap_pedido_compra p
              LEFT JOIN sap_fornecedor f
                     ON f.codigo_sap_fornecedor = p.codigo_sap_fornecedor
             WHERE trim(p.numero_pedido) = trim(@numero_pedido)
               AND p.situacao_sap_pedido_compra = true
               AND p.ativo_sap = true
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", numeroPedido));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is string fornecedor ? fornecedor.Trim() : string.Empty;
    }

    /// <summary>Data do pedido no cache local, preenchida pelo campo PurchaseOrderDate do SAP.</summary>
    public async Task<DateOnly?> ObterDataPorNumeroPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return null;
        }

        const string sql = """
            SELECT data_pedido
              FROM sap_pedido_compra
             WHERE trim(numero_pedido) = trim(@numero_pedido)
               AND situacao_sap_pedido_compra = true
               AND ativo_sap = true
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", numeroPedido));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno switch
        {
            DateOnly data => data,
            DateTime data => DateOnly.FromDateTime(data),
            _ => null
        };
    }

    /// <summary>Tipo do pedido no cache local, preenchido pelo campo PurchaseOrderType do SAP.</summary>
    public async Task<string> ObterTipoPorNumeroPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return string.Empty;
        }

        const string sql = """
            SELECT COALESCE(tipo_pedido, left(trim(payload_original ->> 'PurchaseOrderType'), 4), '') AS tipo_pedido
              FROM sap_pedido_compra
             WHERE trim(numero_pedido) = trim(@numero_pedido)
               AND situacao_sap_pedido_compra = true
               AND ativo_sap = true
             LIMIT 1;
            """;

        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", numeroPedido));
        object? retorno = await comando.ExecuteScalarAsync(cancellationToken);
        return retorno is string tipoPedido ? tipoPedido.Trim() : string.Empty;
    }

    /// <summary>Itens (material + descricao) vinculados ao pedido no cache local, para o grid da tela.</summary>
    public async Task<IReadOnlyList<PedidoCompraSapItem>> ListarItensPorNumeroPedidoAsync(string numeroPedido, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(numeroPedido))
        {
            return [];
        }

        const string sql = """
            SELECT i.codigo_sap_pedido_compra_item, i.numero_item, i.codigo_produto, i.descricao_produto, i.quantidade_pedida, i.unidade_medida, i.peso_item, i.centro, i.deposito, i.grupo_material
              FROM sap_pedido_compra_item i
              JOIN sap_pedido_compra p
                ON p.codigo_sap_pedido_compra = i.codigo_sap_pedido_compra
             WHERE trim(p.numero_pedido) = trim(@numero_pedido)
               AND p.situacao_sap_pedido_compra = true
               AND p.ativo_sap = true
               AND i.situacao_sap_pedido_compra_item = true
               AND i.ativo_sap = true
               AND length(trim(coalesce(i.codigo_produto, ''))) > 0
             ORDER BY i.numero_item;
            """;

        List<PedidoCompraSapItem> itens = [];
        await using NpgsqlConnection conexao = await CriarConexaoAbertaAsync(cancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.Add(ParametroTexto("@numero_pedido", numeroPedido));
        await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(new PedidoCompraSapItem
            {
                CodigoItem = leitor.GetInt64(0),
                NumeroItem = leitor.GetString(1),
                CodigoMaterial = leitor.GetString(2),
                Descricao = leitor.IsDBNull(3) ? null : leitor.GetString(3),
                Quantidade = leitor.IsDBNull(4) ? null : leitor.GetDecimal(4),
                UnidadeMedida = leitor.IsDBNull(5) ? null : leitor.GetString(5),
                PesoItem = leitor.IsDBNull(6) ? null : leitor.GetDecimal(6),
                Centro = leitor.IsDBNull(7) ? null : leitor.GetString(7),
                Deposito = leitor.IsDBNull(8) ? null : leitor.GetString(8),
                GrupoMaterial = leitor.IsDBNull(9) ? null : leitor.GetString(9)
            });
        }

        return itens;
    }

    // Upsert em lote dos fornecedores distintos (resolve/garante a FK antes dos pedidos).
    private static async Task UpsertFornecedoresAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        string?[] codigosFornecedor,
        long? usuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sap_fornecedor
                (codigo_fornecedor, nome_sap_fornecedor, situacao_sap_fornecedor, sap_fornecedor_criado_por)
            SELECT DISTINCT trim(c), NULL, true, @usuario
              FROM unnest(@codigos) AS c
             WHERE length(trim(coalesce(c, ''))) > 0
            ON CONFLICT (codigo_fornecedor) DO UPDATE
               SET situacao_sap_fornecedor = true,
                   sap_fornecedor_atualizado_por = @usuario,
                   sap_fornecedor_atualizado_em = now();
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroArrayTexto("@codigos", codigosFornecedor));
        comando.Parameters.Add(ParametroUsuario(usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    // Upsert em lote dos cabecalhos (numero_pedido e unico); resolve a FK do fornecedor por join.
    private static async Task UpsertPedidosAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        string?[] numeros,
        string?[] fornecedores,
        string?[] datas,
        string?[] moedas,
        string?[] tipos,
        string?[] status,
        string?[] payloads,
        long? usuario,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO sap_pedido_compra
                (codigo_sap_fornecedor, numero_pedido, data_pedido, moeda, tipo_pedido, status_pedido,
                 ativo_sap, status_cache, sincronizado_em, payload_original, sap_pedido_compra_criado_por)
            SELECT f.codigo_sap_fornecedor, trim(d.numero), d.data::date, d.moeda, d.tipo, d.status,
                   true, 'VALIDO', now(), d.payload::jsonb, @usuario
              FROM unnest(@numeros, @fornecedores, @datas, @moedas, @tipos, @status, @payloads)
                       AS d(numero, fornecedor, data, moeda, tipo, status, payload)
              LEFT JOIN sap_fornecedor f ON trim(f.codigo_fornecedor) = trim(d.fornecedor)
             WHERE length(trim(coalesce(d.numero, ''))) > 0
            ON CONFLICT (numero_pedido) DO UPDATE
               SET codigo_sap_fornecedor = EXCLUDED.codigo_sap_fornecedor,
                   data_pedido = EXCLUDED.data_pedido,
                   moeda = EXCLUDED.moeda,
                   tipo_pedido = EXCLUDED.tipo_pedido,
                   status_pedido = EXCLUDED.status_pedido,
                   ativo_sap = true,
                   status_cache = 'VALIDO',
                   sincronizado_em = now(),
                   payload_original = EXCLUDED.payload_original,
                   sap_pedido_compra_atualizado_por = @usuario;
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroArrayTexto("@numeros", numeros));
        comando.Parameters.Add(ParametroArrayTexto("@fornecedores", fornecedores));
        comando.Parameters.Add(ParametroArrayTexto("@datas", datas));
        comando.Parameters.Add(ParametroArrayTexto("@moedas", moedas));
        comando.Parameters.Add(ParametroArrayTexto("@tipos", tipos));
        comando.Parameters.Add(ParametroArrayTexto("@status", status));
        comando.Parameters.Add(ParametroArrayTexto("@payloads", payloads));
        comando.Parameters.Add(ParametroUsuario(usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    // Upsert em lote dos itens; vincula ao pedido por numero_pedido (ja gravado acima).
    private static async Task UpsertItensAsync(
        NpgsqlConnection conexao,
        NpgsqlTransaction transacao,
        string?[] numerosPedido,
        string?[] numerosItem,
        string?[] materiais,
        string?[] descricoes,
        string?[] quantidades,
        string?[] unidades,
        string?[] pesos,
        string?[] centros,
        string?[] depositos,
        string?[] gruposMaterial,
        string?[] payloads,
        long? usuario,
        CancellationToken cancellationToken)
    {
        if (numerosItem.Length == 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO sap_pedido_compra_item
                (codigo_sap_pedido_compra, numero_item, codigo_produto, descricao_produto, quantidade_pedida, unidade_medida,
                 peso_item, centro, deposito, grupo_material, ativo_sap, status_cache, sincronizado_em, payload_original, sap_pedido_compra_item_criado_por)
            SELECT p.codigo_sap_pedido_compra, trim(d.numero_item),
                   NULLIF(trim(coalesce(d.material, '')), ''), NULLIF(trim(coalesce(d.descricao, '')), ''),
                   NULLIF(trim(coalesce(d.quantidade, '')), '')::numeric, NULLIF(trim(coalesce(d.unidade, '')), ''),
                   NULLIF(trim(coalesce(d.peso, '')), '')::numeric,
                   NULLIF(trim(coalesce(d.centro, '')), ''), NULLIF(trim(coalesce(d.deposito, '')), ''), NULLIF(trim(coalesce(d.grupo, '')), ''),
                   true, 'VALIDO', now(), d.payload::jsonb, @usuario
              FROM unnest(@pedidos, @itens, @materiais, @descricoes, @quantidades, @unidades, @pesos, @centros, @depositos, @grupos, @payloads)
                       AS d(pedido, numero_item, material, descricao, quantidade, unidade, peso, centro, deposito, grupo, payload)
              JOIN sap_pedido_compra p ON trim(p.numero_pedido) = trim(d.pedido)
             WHERE length(trim(coalesce(d.numero_item, ''))) > 0
            ON CONFLICT (codigo_sap_pedido_compra, numero_item) DO UPDATE
               SET codigo_produto = EXCLUDED.codigo_produto,
                   descricao_produto = EXCLUDED.descricao_produto,
                   quantidade_pedida = EXCLUDED.quantidade_pedida,
                   unidade_medida = EXCLUDED.unidade_medida,
                   peso_item = EXCLUDED.peso_item,
                   centro = EXCLUDED.centro,
                   deposito = EXCLUDED.deposito,
                   grupo_material = EXCLUDED.grupo_material,
                   ativo_sap = true,
                   status_cache = 'VALIDO',
                   sincronizado_em = now(),
                   payload_original = EXCLUDED.payload_original,
                   sap_pedido_compra_item_atualizado_por = @usuario,
                   sap_pedido_compra_item_atualizado_em = now();
            """;

        await using NpgsqlCommand comando = new(sql, conexao, transacao);
        comando.Parameters.Add(ParametroArrayTexto("@pedidos", numerosPedido));
        comando.Parameters.Add(ParametroArrayTexto("@itens", numerosItem));
        comando.Parameters.Add(ParametroArrayTexto("@materiais", materiais));
        comando.Parameters.Add(ParametroArrayTexto("@descricoes", descricoes));
        comando.Parameters.Add(ParametroArrayTexto("@quantidades", quantidades));
        comando.Parameters.Add(ParametroArrayTexto("@unidades", unidades));
        comando.Parameters.Add(ParametroArrayTexto("@pesos", pesos));
        comando.Parameters.Add(ParametroArrayTexto("@centros", centros));
        comando.Parameters.Add(ParametroArrayTexto("@depositos", depositos));
        comando.Parameters.Add(ParametroArrayTexto("@grupos", gruposMaterial));
        comando.Parameters.Add(ParametroArrayTexto("@payloads", payloads));
        comando.Parameters.Add(ParametroUsuario(usuario));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlParameter ParametroArrayTexto(string nome, string?[] valores)
        => new(nome, NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = valores };

    // Tipado como bigint mesmo quando nulo: evita o SQL inferir text para *_criado_por / *_atualizado_por.
    private static NpgsqlParameter ParametroUsuario(long? usuario)
        => new("@usuario", NpgsqlDbType.Bigint) { Value = usuario.HasValue ? usuario.Value : DBNull.Value };

    private static string? NuloSeVazio(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NormalizarTipoPedido(string? tipoPedido)
    {
        if (string.IsNullOrWhiteSpace(tipoPedido))
        {
            return null;
        }

        string valor = tipoPedido.Trim();
        return valor.Length <= 4 ? valor : valor[..4];
    }
}
