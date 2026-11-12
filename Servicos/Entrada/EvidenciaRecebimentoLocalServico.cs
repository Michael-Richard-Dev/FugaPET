using System.Data;
using FugaPET_Dev.AcessoDados.Banco;
using FugaPET_Dev.Modelo.Entrada;
using Npgsql;

namespace FugaPET_Dev.Servicos.Entrada;

/// <summary>
/// P5/D2: fonte LOCAL de evidencia de recebimento do mesmo pedido+item.
/// <para>
/// Executa a consulta aprovada em <c>Servicos/Entrada/P5/Contratos/D2LocalR5B.sql</c>, aqui
/// parametrizada em Npgsql em vez de PREPARE global, dentro de transacao REPEATABLE READ
/// READ ONLY — a propria consulta exige isso.
/// </para>
/// <para>
/// A classificacao vem da coluna <c>categoria_local_p5</c>, calculada no SQL. Este servico
/// nao reinterpreta status: apenas traduz a categoria para o cenario do motor e, havendo
/// varias linhas, mantem a MAIS restritiva.
/// </para>
/// FAIL-CLOSED: qualquer erro vira <see cref="CenarioRecebimentoLocal.Indisponivel"/>, que
/// BLOQUEIA. Ausencia de linha local nunca vira prova negativa de recebimento no SAP.
/// </summary>
public sealed class EvidenciaRecebimentoLocalServico : IEvidenciaRecebimentoLocalServico
{
    private readonly IFabricaConexaoBanco _fabrica;
    private readonly Action<string>? _registrarDiagnostico;
    private readonly bool _bancoHabilitado;

    public EvidenciaRecebimentoLocalServico(
        IFabricaConexaoBanco fabricaConexaoBanco,
        bool bancoHabilitado,
        Action<string>? registrarDiagnostico = null)
    {
        _fabrica = fabricaConexaoBanco ?? throw new ArgumentNullException(nameof(fabricaConexaoBanco));
        _bancoHabilitado = bancoHabilitado;
        _registrarDiagnostico = registrarDiagnostico;
    }

    public async Task<ResultadoRecebimentoLocal> ConsultarAsync(
        string numeroPedido,
        string numeroItemPedido,
        long codigoLancamentoAtual,
        CancellationToken cancellationToken = default)
    {
        if (!_bancoHabilitado)
        {
            return ResultadoRecebimentoLocal.Indisponivel("banco desabilitado neste ambiente");
        }

        try
        {
            await using NpgsqlConnection conexao = await _fabrica.CriarConexaoAbertaAsync(cancellationToken);
            await using NpgsqlTransaction transacao =
                await conexao.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);

            await using (var somenteLeitura = new NpgsqlCommand("SET TRANSACTION READ ONLY;", conexao, transacao))
            {
                await somenteLeitura.ExecuteNonQueryAsync(cancellationToken);
            }

            var categorias = new List<string>();
            await using (var comando = new NpgsqlCommand(ConsultaD2, conexao, transacao) { CommandTimeout = 15 })
            {
                comando.Parameters.AddWithValue("pedido", (numeroPedido ?? string.Empty).Trim());
                comando.Parameters.AddWithValue("item", (numeroItemPedido ?? string.Empty).Trim());
                comando.Parameters.AddWithValue("lancamento",
                    codigoLancamentoAtual > 0 ? codigoLancamentoAtual : (object)DBNull.Value);

                await using NpgsqlDataReader leitor = await comando.ExecuteReaderAsync(cancellationToken);
                int indice = leitor.GetOrdinal("categoria_local_p5");
                while (await leitor.ReadAsync(cancellationToken))
                {
                    categorias.Add(leitor.IsDBNull(indice) ? string.Empty : leitor.GetString(indice));
                }
            }

            await transacao.RollbackAsync(cancellationToken);

            ResultadoRecebimentoLocal resultado = Classificar(categorias);
            Registrar($"P5/D2: {categorias.Count} linha(s) local(is); desfecho {resultado.Cenario}.");
            return resultado;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Registrar("P5/D2: falha na consulta local: " + ex.GetType().Name);
            return ResultadoRecebimentoLocal.Indisponivel(
                "falha ao consultar recebimentos locais deste item do pedido");
        }
    }

    /// <summary>
    /// Traduz as categorias do SQL. Havendo varias linhas, a mais restritiva prevalece, nesta
    /// ordem: Indisponivel, Confirmado, Pendente, SemRegistro. Categoria desconhecida e tratada
    /// como Indisponivel — uma categoria nova nunca deve liberar por ser nova.
    /// </summary>
    internal static ResultadoRecebimentoLocal Classificar(IReadOnlyList<string> categorias)
    {
        if (categorias.Count == 0)
        {
            // A consulta sempre devolve ao menos uma linha; nenhuma linha e estado nao explicado.
            return ResultadoRecebimentoLocal.Indisponivel(
                "a consulta local nao devolveu nenhuma linha para este pedido e item");
        }

        bool confirmado = false, pendente = false;

        foreach (string categoria in categorias)
        {
            switch (categoria)
            {
                case "SEM_ENTRADA_LOCAL_NAO_PROVA_SAP":
                case "PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO":
                    break;
                case "RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK":
                    confirmado = true;
                    break;
                case "ENVIO_OU_RESULTADO_REMOTO_INCERTO_BLOCK":
                case "CANCELAMENTO_LOCAL_SEM_PROVA_ESTORNO_BLOCK":
                case "OUTRO_LANCAMENTO_LOCAL_BLOCK":
                    pendente = true;
                    break;
                case "CHAVE_INVALIDA_BLOCK":
                    return ResultadoRecebimentoLocal.Indisponivel(
                        "pedido ou item fora do formato esperado pela consulta local");
                case "STATUS_NAO_CLASSIFICADO_BLOCK":
                    return ResultadoRecebimentoLocal.Indisponivel(
                        "ha lancamento local em status nao classificado para este item");
                default:
                    return ResultadoRecebimentoLocal.Indisponivel(
                        "a consulta local devolveu uma categoria desconhecida: " + categoria);
            }
        }

        if (confirmado)
        {
            return ResultadoRecebimentoLocal.Confirmado(
                "ha documento de material ou lancamento confirmado para este item do pedido");
        }
        if (pendente)
        {
            return ResultadoRecebimentoLocal.Pendente(
                "ha lancamento local enviado, cancelado sem prova de estorno, ou de outro "
                + "lancamento, para este item do pedido");
        }
        return ResultadoRecebimentoLocal.SemRegistro();
    }

    private void Registrar(string mensagem) => _registrarDiagnostico?.Invoke(mensagem);

    /// <summary>
    /// Consulta D2 aprovada (R5B), sem o PREPARE: os tres parametros viram @pedido, @item e
    /// @lancamento. O corpo e mantido fiel ao contrato — inclusive a coluna final
    /// <c>prova_negativa_sap</c> fixa em FALSE, que lembra que nada aqui prova ausencia no SAP.
    /// </summary>
    internal const string ConsultaD2 = """
        WITH entrada AS MATERIALIZED (
            SELECT btrim(@pedido::text) AS pedido_raw,
                   btrim(@item::text) AS item_raw,
                   @lancamento::bigint AS lancamento_atual
        ), p AS MATERIALIZED (
            SELECT pedido_raw AS numero_pedido,
                   CASE WHEN item_raw ~ '^[0-9]{1,5}$'
                        THEN lpad(coalesce(nullif(ltrim(item_raw,'0'),''),'0'),5,'0')
                        ELSE NULL END AS numero_item_norm,
                   lancamento_atual,
                   (pedido_raw ~ '^[0-9]{1,10}$' AND item_raw ~ '^[0-9]{1,5}$') AS chave_valida
            FROM entrada
        ), sap_item AS MATERIALIZED (
            SELECT pi.codigo_sap_pedido_compra_item, pi.numero_item,
                   pi.codigo_produto, pi.status_cache, pi.sincronizado_em,
                   pi.quantidade_pedida, pi.quantidade_recebida, pi.quantidade_pendente
            FROM homologacao.sap_pedido_compra pc
            JOIN homologacao.sap_pedido_compra_item pi
              ON pi.codigo_sap_pedido_compra = pc.codigo_sap_pedido_compra
            JOIN p ON btrim(pc.numero_pedido) = p.numero_pedido
            WHERE pi.numero_item ~ '^[[:space:]]*[0-9]{1,5}[[:space:]]*$'
              AND lpad(coalesce(nullif(ltrim(btrim(pi.numero_item),'0'),''),'0'),5,'0')
                  = p.numero_item_norm
        ), local_item AS MATERIALIZED (
            SELECT l.codigo_entrada_produto_lancamento AS lancamento_id,
                   l.numero_pedido, l.status_lancamento, l.situacao_entrada_produto_lancamento,
                   l.iniciado_em, l.finalizado_em, l.enviado_sap_em AS lancamento_enviado_em,
                   l.documento_material_sap AS lancamento_documento,
                   l.exercicio_documento_material_sap AS lancamento_exercicio,
                   i.codigo_entrada_produto_item AS item_id, i.numero_item,
                   i.material, i.status_item, i.situacao_entrada_produto_item,
                   i.codigo_sap_pedido_compra_item, i.documento_material_item,
                   i.quantidade_recebida AS item_quantidade_recebida
            FROM homologacao.entrada_produto_lancamento l
            JOIN homologacao.entrada_produto_item i
              ON i.codigo_entrada_produto_lancamento=l.codigo_entrada_produto_lancamento
            CROSS JOIN p
            WHERE btrim(l.numero_pedido)=p.numero_pedido
              AND (
                (i.numero_item ~ '^[[:space:]]*[0-9]{1,5}[[:space:]]*$'
                  AND lpad(coalesce(nullif(ltrim(btrim(i.numero_item),'0'),''),'0'),5,'0')=p.numero_item_norm)
                OR EXISTS (
                  SELECT 1 FROM sap_item s
                  WHERE s.codigo_sap_pedido_compra_item=i.codigo_sap_pedido_compra_item
                )
              )
        ), cabecalho_sem_item AS MATERIALIZED (
            SELECT l.codigo_entrada_produto_lancamento
            FROM homologacao.entrada_produto_lancamento l
            CROSS JOIN p
            WHERE btrim(l.numero_pedido)=p.numero_pedido
              AND NOT EXISTS (
                SELECT 1 FROM homologacao.entrada_produto_item i
                WHERE i.codigo_entrada_produto_lancamento=l.codigo_entrada_produto_lancamento
              )
        )
        SELECT
            p.numero_pedido,
            p.numero_item_norm AS numero_item,
            p.chave_valida,
            p.lancamento_atual,
            (SELECT count(*) FROM cabecalho_sem_item) AS lancamentos_sem_item_no_pedido,
            li.lancamento_id, li.item_id,
            (li.lancamento_id IS NOT NULL AND li.lancamento_id=p.lancamento_atual) AS eh_lancamento_atual,
            (li.lancamento_id IS NOT NULL AND li.lancamento_id IS DISTINCT FROM p.lancamento_atual) AS eh_outro_lancamento,
            li.status_lancamento, li.status_item,
            li.lancamento_documento, li.documento_material_item,
            lote.codigo_entrada_produto_lote AS lote_id,
            lote.status_lote,
            lote.documento_material_sap AS lote_documento,
            CASE
              WHEN NOT p.chave_valida THEN 'CHAVE_INVALIDA_BLOCK'
              WHEN li.item_id IS NULL THEN 'SEM_ENTRADA_LOCAL_NAO_PROVA_SAP'
              WHEN coalesce(li.lancamento_documento,'')<>''
                OR coalesce(lote.documento_material_sap,'')<>''
                OR li.status_lancamento='CONFIRMADO_SAP'
                OR li.status_item='CONFIRMADO_SAP'
                OR lote.status_lote='CONFIRMADO_SAP' THEN 'RECEBIMENTO_OU_DOCUMENTO_SAP_BLOCK'
              WHEN coalesce(li.lancamento_enviado_em,lote.enviado_sap_em) IS NOT NULL
                OR li.status_lancamento IN ('ENVIADO_SAP','ERRO_SAP')
                OR li.status_item IN ('ENVIADO_SAP','ERRO_SAP')
                OR lote.status_lote IN ('ENVIANDO_SAP','ERRO_SAP')
                OR lote.payload_enviado_json IS NOT NULL THEN 'ENVIO_OU_RESULTADO_REMOTO_INCERTO_BLOCK'
              WHEN li.status_lancamento='CANCELADO' OR li.status_item='CANCELADO'
                OR lote.status_lote='CANCELADO' THEN 'CANCELAMENTO_LOCAL_SEM_PROVA_ESTORNO_BLOCK'
              WHEN li.lancamento_id IS DISTINCT FROM p.lancamento_atual
                THEN 'OUTRO_LANCAMENTO_LOCAL_BLOCK'
              WHEN li.status_lancamento NOT IN ('ABERTO','EM_PESAGEM','FINALIZADO_LOCAL')
                OR li.status_item NOT IN ('ABERTO','EM_PESAGEM','FINALIZADO_LOCAL')
                OR (lote.status_lote IS NOT NULL
                    AND lote.status_lote NOT IN ('AGUARDANDO_DADOS','LOTE_CONFIRMADO','PESANDO','FINALIZADO_LOCAL'))
                THEN 'STATUS_NAO_CLASSIFICADO_BLOCK'
              ELSE 'PREPARACAO_LOCAL_CORRENTE_SEM_ENVIO'
            END AS categoria_local_p5,
            FALSE AS prova_negativa_sap
        FROM p
        LEFT JOIN local_item li ON TRUE
        LEFT JOIN homologacao.entrada_produto_lote lote
            ON lote.codigo_entrada_produto_item=li.item_id
        ORDER BY li.lancamento_id NULLS FIRST, li.item_id NULLS FIRST,
                 lote.codigo_entrada_produto_lote NULLS FIRST;
        """;
}
